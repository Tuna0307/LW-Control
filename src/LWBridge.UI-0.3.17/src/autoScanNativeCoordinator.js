function errorMessage(error) {
  return error?.message || String(error);
}

function snapshotRevision(snapshot) {
  const value = Number(snapshot?.revision);
  return Number.isSafeInteger(value) && value >= 0 ? value : null;
}

export function createAutoScanNativeCoordinator({
  saveConfig,
  readStatus,
  runNow,
  onConfigSnapshot,
  onRuntimeSnapshot,
  onWriteError,
  onActionError,
}) {
  let active = true;
  let editRevision = 0;
  let committedEditRevision = 0;
  let nativeRevision = -1;
  let chain = Promise.resolve();

  const acceptSnapshot = (snapshot, allowConfig) => {
    if (!active || !snapshot) return false;
    const revision = snapshotRevision(snapshot);
    if (revision !== null && revision < nativeRevision) return false;
    if (revision !== null) nativeRevision = Math.max(nativeRevision, revision);
    onRuntimeSnapshot(snapshot);
    if (allowConfig && editRevision === committedEditRevision) onConfigSnapshot(snapshot);
    return true;
  };

  const receive = (snapshot) => acceptSnapshot(
    snapshot,
    editRevision === committedEditRevision,
  );

  const save = (config) => {
    if (!active) return Promise.resolve(null);
    const revision = ++editRevision;
    const task = chain
      .catch(() => undefined)
      .then(() => (active ? saveConfig(config) : null))
      .then(async (snapshot) => {
        if (!active || snapshot === null) return snapshot;
        if (editRevision !== revision) {
          acceptSnapshot(snapshot, false);
          return snapshot;
        }

        committedEditRevision = revision;
        onWriteError("");
        if (!acceptSnapshot(snapshot, true)) {
          try {
            const current = await readStatus();
            if (active && editRevision === revision) acceptSnapshot(current, true);
          } catch {
            // A stale acknowledgement does not turn a successful write into a
            // write failure. A later native event/status can still converge it.
          }
        }
        return snapshot;
      })
      .catch((error) => {
        if (active && editRevision === revision) onWriteError(errorMessage(error));
        return null;
      });
    chain = task;
    return task;
  };

  const run = () => {
    if (!active) return Promise.resolve(null);
    const actionEditRevision = editRevision;
    const task = chain
      .catch(() => undefined)
      .then(() => (active ? runNow() : null))
      .then((snapshot) => {
        if (!active || snapshot === null) return snapshot;
        acceptSnapshot(
          snapshot,
          editRevision === actionEditRevision && committedEditRevision === editRevision,
        );
        return snapshot;
      })
      .catch((error) => {
        if (active) onActionError(errorMessage(error));
        return null;
      });
    chain = task;
    return task;
  };

  return {
    receive,
    save,
    run,
    retire() { active = false; },
    get pendingConfig() { return editRevision !== committedEditRevision; },
    get nativeRevision() { return nativeRevision; },
  };
}
