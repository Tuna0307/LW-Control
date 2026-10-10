import { useEffect, useRef, useState } from "react";
import { useI18n } from "./i18n.jsx";

// Recovered from index-BVfnK1wp.js: ce/le/ue at bytes 194447/194621/194751.
export function profileDisplay(profile, t) {
  return {
    name: profile.roleName || profile.displayName,
    server: profile.serverId ? t("profile.server", { server: profile.serverId }) : t("profile.serverUnbound"),
    note: profile.note || t("profile.noNote"),
  };
}

export function reorderProfileIds(ids, sourceId, targetId) {
  if (sourceId === targetId || !ids.includes(sourceId) || !ids.includes(targetId)) return ids;
  const next = ids.filter((id) => id !== sourceId);
  next.splice(next.indexOf(targetId), 0, sourceId);
  return next;
}

export function profileHasStopTarget(instance) {
  const failed = instance?.phase === "error" && instance.pid == null;
  const owned = typeof instance?.instanceId === "string" && instance.instanceId.length > 0;
  // F-04/F-07: native recovery can still own a relaunch after restoring the
  // old instance ID. It is cancellable even without a live game process.
  return (owned && !failed) || instance?.connectionState === "recovering";
}

export function profileBatchIds(action, profiles, instances) {
  return profiles.flatMap((profile) => {
    const instance = instances[profile.id];
    const canStop = profileHasStopTarget(instance);
    return (action === "start" ? profile.enabled && !profile.lockedReason && !canStop : canStop) ? [profile.id] : [];
  });
}

function errorCodes(value) {
  const codes = [];
  if (value && typeof value === "object" && "code" in value && typeof value.code === "string") codes.push(value.code);
  const message = value instanceof Error ? value.message : String(value ?? "");
  codes.push(...message.match(/\b[A-Z][A-Z0-9_]{2,}\b/g) || []);
  // Original shared Ir selects later message tokens before earlier ones and
  // prefers those over a structured code pushed before message extraction.
  return [...new Set(codes)].reverse();
}

function profileError(t, error) {
  for (const code of errorCodes(error)) {
    for (const namespace of ["error", "auth.error", "update.error"]) {
      const key = `${namespace}.${code}`;
      if (t(key) !== key) return t(key);
    }
  }
  return t("common.actionFailed");
}

function ProfileIcon({ name }) {
  return <svg className="ui-icon" viewBox="0 0 16 16" aria-hidden="true" focusable="false">
    {name === "add" ? <path d="M8 3v10M3 8h10" /> : null}
    {name === "drag" ? <path d="M5 4h.01M11 4h.01M5 8h.01M11 8h.01M5 12h.01M11 12h.01" /> : null}
    {name === "edit" ? <><path d="m3 11-.5 2.5L5 13l7.5-7.5-2-2L3 11Z" /><path d="m9.5 4.5 2 2" /></> : null}
    {name === "enabled" ? <path d="m3 8 3 3 7-7" /> : null}
    {name === "disabled" ? <path d="M3 8h10" /> : null}
    {name === "remove" ? <path d="m4 4 8 8M12 4l-8 8" /> : null}
    {name === "play" ? <path d="m5 3 8 5-8 5V3Z" /> : null}
    {name === "stop" ? <rect x="4" y="4" width="8" height="8" rx="1" /> : null}
    {name === "chevron-left" ? <path d="m10 3-5 5 5 5" /> : null}
    {name === "chevron-right" ? <path d="m6 3 5 5-5 5" /> : null}
  </svg>;
}

// Original shared In dialog, byte 209017. Note editor never dismisses on backdrop.
function ProfileNoteDialog({ busy, onClose, children }) {
  const ref = useRef(null);
  useEffect(() => {
    const dialog = ref.current;
    const previous = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    dialog.showModal();
    return () => {
      dialog.close();
      if (previous?.isConnected) previous.focus({ preventScroll: true });
    };
  }, []);
  return <dialog ref={ref} className="app-dialog profile-dialog-backdrop" role="dialog" aria-modal="true" aria-labelledby="profile-note-title" aria-busy={busy}
    onCancel={(event) => { event.preventDefault(); if (!busy) onClose(); }}
    onKeyDown={(event) => {
      event.stopPropagation();
      if (event.key !== "Tab") return;
      const controls = [...event.currentTarget.querySelectorAll("button, [href], input, select, textarea, [tabindex]")]
        .filter((element) => element.tabIndex >= 0 && !element.matches(":disabled") && element.getClientRects().length > 0);
      const first = controls[0];
      const last = controls[controls.length - 1];
      if (!first) event.preventDefault();
      else if (event.shiftKey && (document.activeElement === first || !controls.includes(document.activeElement))) {
        event.preventDefault(); last.focus();
      } else if (!event.shiftKey && (document.activeElement === last || !controls.includes(document.activeElement))) {
        event.preventDefault(); first.focus();
      }
    }}>{children}</dialog>;
}

const CONNECTION_KEYS = {
  offline: "profile.connection.offline", starting: "profile.connection.starting",
  recovering: "profile.connection.recovering", awaitingLogin: "profile.connection.awaitingLogin",
  connected: "profile.connection.connected", reconnecting: "profile.connection.reconnecting",
  grace: "profile.connection.grace", locked: "profile.connection.locked", error: "profile.connection.error",
};
const EMPTY_INSTANCES = Object.freeze({});
const EMPTY_ERRORS = Object.freeze([]);

// Yr, byte 339285: supplied local profile state replaces original service entitlement.
// Account Upgrade is deliberately excluded. Absent providers fence actions before dispatch.
export function ProfileSidebar({
  state = null, busy = false, error = "", instances = EMPTY_INSTANCES,
  profileLaunchErrors = EMPTY_ERRORS, focusGameOnProfileSelect = true,
  onSelect = null, onCreate = null, onRemove = null, onReorder = null, onUpdateNote = null,
  onSetEnabled = null,
  readInstance = null, onStartProfile = null, onStopProfile = null, onRestartAll = null,
  onClearProfileLaunchErrors = null,
}) {
  const { t } = useI18n();
  const [instanceState, setInstanceState] = useState(instances);
  // Concurrent retained owners must keep independent visible busy states.
  // One owner's completed Start/Stop cannot clear another owner's pending UI.
  const [runBusyIds, setRunBusyIds] = useState(() => new Set());
  const [batchBusy, setBatchBusy] = useState("");
  const [actionError, setActionError] = useState("");
  const [enabledBusyId, setEnabledBusyId] = useState("");
  const [restartRequired, setRestartRequired] = useState(false);
  const [noteId, setNoteId] = useState("");
  const [noteValue, setNoteValue] = useState("");
  const [draggedId, setDraggedId] = useState("");
  const [dragOverId, setDragOverId] = useState("");
  const [collapsed, setCollapsed] = useState(true);
  const instanceRevisionsRef = useRef(new Map());
  const profileActionsRef = useRef(new Set());
  function beginProfileAction(id) {
    if (profileActionsRef.current.has(id)) {
      const failure = new Error("A game lifecycle operation is already in progress.");
      failure.code = "GAME_OPERATION_IN_PROGRESS";
      throw failure;
    }
    profileActionsRef.current.add(id);
    setRunBusyIds((current) => new Set(current).add(id));
    instanceRevisionsRef.current.set(id, (instanceRevisionsRef.current.get(id) || 0) + 1);
  }
  function endProfileAction(id) {
    instanceRevisionsRef.current.set(id, (instanceRevisionsRef.current.get(id) || 0) + 1);
    profileActionsRef.current.delete(id);
    setRunBusyIds((current) => {
      const next = new Set(current);
      next.delete(id);
      return next;
    });
  }
  useEffect(() => { setInstanceState(instances); }, [instances]);
  useEffect(() => {
    if (!state || !readInstance) return undefined;
    let closed = false;
    let polling = false;
    const poll = async () => {
      if (polling) return;
      polling = true;
      const revisions = new Map(state.profiles.map((profile) =>
        [profile.id, instanceRevisionsRef.current.get(profile.id) || 0]));
      try {
        const entries = await Promise.all(state.profiles.map(async (profile) => {
          try { return [profile.id, true, await readInstance(profile.id)]; }
          catch { return [profile.id, false, null]; }
        }));
        if (!closed) setInstanceState((current) => {
          const next = { ...current };
          for (const [id, ok, result] of entries) {
            // A poll begun before Start/Stop cannot overwrite that action's
            // newer exact-owner result, even if its native response arrives last.
            if (ok && !profileActionsRef.current.has(id) &&
              revisions.get(id) === (instanceRevisionsRef.current.get(id) || 0)) next[id] = result;
          }
          return next;
        });
      } finally { polling = false; }
    };
    poll();
    const timer = window.setInterval(poll, 3000);
    return () => { closed = true; window.clearInterval(timer); };
  }, [state, readInstance]);
  if (!state) return null;
  const profiles = state.profiles;
  const atCapacity = profiles.length >= state.maxProfiles;
  const startIds = profileBatchIds("start", profiles, instanceState);
  const stopIds = profileBatchIds("stop", profiles, instanceState);
  const launchError = profileLaunchErrors.map((entry) => `${profiles.find((profile) => profile.id === entry.profileId)?.roleName || entry.profileId}：${profileError(t, entry.error)}`).join("\n");
  const isCollapsed = collapsed && !error && !launchError && !actionError && !restartRequired;

  async function runBatch(action) {
    if (action === "start" ? !onStartProfile : !onStopProfile) return;
    const ids = action === "start" ? startIds : stopIds;
    const next = { ...instanceState };
    const failures = [];
    onClearProfileLaunchErrors?.();
    setBatchBusy(action); setActionError(""); setRestartRequired(false);
    for (const id of ids) {
      let acquired = false;
      try {
        beginProfileAction(id);
        acquired = true;
        if (action === "start") next[id] = await onStartProfile(id);
        else if (next[id]) { next[id] = await onStopProfile(id, next[id].instanceId); }
        setInstanceState((current) => ({ ...current, [id]: next[id] }));
      } catch (failure) {
        const name = profiles.find((profile) => profile.id === id)?.roleName || id;
        failures.push(`${name}：${profileError(t, failure)}`);
        if (errorCodes(failure).includes("LAUNCH_TICKET_RESTART_REQUIRED")) setRestartRequired(true);
      } finally {
        if (acquired) endProfileAction(id);
      }
    }
    setActionError(failures.join("\n")); setBatchBusy("");
  }

  async function runProfile(profile, instance, running) {
    const provider = running ? onStopProfile : onStartProfile;
    if (!provider) return;
    if (profileActionsRef.current.has(profile.id)) return;
    beginProfileAction(profile.id);
    onClearProfileLaunchErrors?.(); setActionError(""); setRestartRequired(false);
    try {
      const next = running ? await provider(profile.id, instance?.instanceId) : await provider(profile.id);
      setInstanceState((current) => ({ ...current, [profile.id]: next }));
    } catch (failure) {
      setActionError(profileError(t, failure));
      setRestartRequired(errorCodes(failure).includes("LAUNCH_TICKET_RESTART_REQUIRED"));
    } finally { endProfileAction(profile.id); }
  }

  async function removeProfile(profile, displayName) {
    if (!onRemove || !window.confirm(t("profile.deleteConfirm", { name: displayName }))) return;
    await onRemove(profile.id);
  }

  return <section className={`profile-list${isCollapsed ? " collapsed" : ""}${busy ? " busy" : ""}`} aria-label={t("profile.accounts")}>
    {isCollapsed ? <div className="profile-compact-items">{profiles.map((profile) => {
      const display = profileDisplay(profile, t);
      const connection = profile.lockedReason ? "locked" : instanceState[profile.id]?.connectionState ?? "offline";
      const status = t(CONNECTION_KEYS[connection]);
      return <button type="button" className={`profile-compact-item${profile.id === state.selectedProfileId ? " active" : ""}`} disabled={busy || !profile.enabled || !onSelect} title={`${display.name} · ${display.server} · ${status}`} aria-label={`${display.name} · ${display.server} · ${status}`} onClick={() => onSelect?.(profile.id, focusGameOnProfileSelect)} key={profile.id}>
        <span className={`profile-dot ${connection}`} /><span className="profile-compact-copy"><strong>{display.name}</strong><span className="profile-compact-state">{status}</span></span>
      </button>;
    })}</div> : <>
      <div className="profile-list-heading"><div className="profile-heading-title"><strong>{t("profile.accounts")}</strong><span className="profile-quota-badge">{profiles.length}/{state.maxProfiles}</span></div></div>
      <div className="profile-batch-actions">
        <button type="button" disabled={!!batchBusy || startIds.length === 0 || !onStartProfile} onClick={() => runBatch("start")}><ProfileIcon name="play" />{t(batchBusy === "start" ? "profile.startingAll" : "profile.startAll")}</button>
        <button type="button" disabled={!!batchBusy || stopIds.length === 0 || !onStopProfile} onClick={() => runBatch("stop")}><ProfileIcon name="stop" />{t(batchBusy === "stop" ? "profile.stoppingAll" : "profile.stopAll")}</button>
      </div>
      <div className="profile-items">{profiles.map((profile) => {
        const display = profileDisplay(profile, t);
        const instance = instanceState[profile.id];
        const running = profileHasStopTarget(instance);
        const connection = profile.lockedReason ? "locked" : instance?.connectionState ?? "offline";
        return <div className={`profile-row${profile.id === state.selectedProfileId ? " active" : ""}${draggedId === profile.id ? " dragging" : ""}${dragOverId === profile.id ? " drag-over" : ""}`} key={profile.id}
          onDragOver={(event) => { if (onReorder && draggedId && draggedId !== profile.id) { event.preventDefault(); setDragOverId(profile.id); } }}
          onDrop={(event) => {
            event.preventDefault();
            const next = reorderProfileIds(profiles.map((entry) => entry.id), draggedId, profile.id);
            setDraggedId(""); setDragOverId("");
            if (onReorder && next.some((id, index) => id !== profiles[index]?.id)) onReorder(next);
          }}>
          <span className="profile-drag-handle" draggable={!busy && !!onReorder} aria-label={t("profile.dragLabel", { name: display.name })} title={t("profile.dragTitle")}
            onDragStart={(event) => { if (!onReorder) return; event.dataTransfer.effectAllowed = "move"; event.dataTransfer.setData("text/plain", profile.id); setDraggedId(profile.id); }}
            onDragEnd={() => { setDraggedId(""); setDragOverId(""); }}><ProfileIcon name="drag" /></span>
          <button type="button" className="profile-item" disabled={busy || !profile.enabled || !onSelect} onClick={() => onSelect?.(profile.id, focusGameOnProfileSelect)}>
            <span className={`profile-dot ${connection}`} /><span className="profile-copy"><strong>{display.name}</strong><div className="profile-sub-row"><span className="profile-server">{display.server}</span><span className={`profile-state-label ${connection}`}>{t(CONNECTION_KEYS[connection])}</span>{display.note ? <span className="profile-note" title={display.note}>· {display.note}</span> : null}</div></span>
          </button>
          <div className="profile-row-actions">
            <button type="button" className="profile-note-edit profile-enable-toggle"
              aria-pressed={profile.enabled === true}
              disabled={busy || !!enabledBusyId || !onSetEnabled || !!profile.lockedReason}
              title={`${display.name} · ${t(profile.enabled ? "common.enabled" : "common.disabled")}`}
              aria-label={`${display.name} · ${t(profile.enabled ? "common.enabled" : "common.disabled")}`}
              onClick={(event) => {
                event.stopPropagation();
                setEnabledBusyId(profile.id); setActionError("");
                Promise.resolve().then(() => onSetEnabled(profile.id, !profile.enabled))
                  .catch((failure) => setActionError(profileError(t, failure)))
                  .finally(() => setEnabledBusyId(""));
              }}><ProfileIcon name={profile.enabled ? "enabled" : "disabled"} /></button>
            <button type="button" className="profile-note-edit" disabled={!onUpdateNote} title={t("profile.editNote")} aria-label={t("profile.editNoteLabel", { name: display.name })} onClick={(event) => { event.stopPropagation(); if (onUpdateNote) { setNoteId(profile.id); setNoteValue(profile.note); } }}><ProfileIcon name="edit" /></button>
            <button type="button" className="profile-delete" disabled={busy || !onRemove} title={t("common.delete")} aria-label={`${t("common.delete")} ${display.name}`} onClick={(event) => {
              event.stopPropagation(); setActionError("");
              removeProfile(profile, display.name).catch((failure) => setActionError(profileError(t, failure)));
            }}><ProfileIcon name="remove" /></button>
          </div>
          {/* Selection/metadata may be busy on A while retained B still needs
              independent Start/Stop. Native per-owner admission, not the
              global view-transition flag, guards concurrent deletion. */}
          <button type="button" className={`profile-run${running ? " is-running" : ""}`} disabled={!!batchBusy || runBusyIds.has(profile.id) || (!running && (!profile.enabled || !!profile.lockedReason)) || !(running ? onStopProfile : onStartProfile)} title={t(running ? "profile.stopAccount" : "profile.startAccount")} aria-label={t(running ? "profile.stopAccount" : "profile.startAccount")} onClick={(event) => { event.stopPropagation(); runProfile(profile, instance, running); }}><ProfileIcon name={running ? "stop" : "play"} /></button>
        </div>;
      })}</div>
      <button type="button" className="profile-add" disabled={busy || atCapacity || !onCreate} onClick={() => {
        setActionError("");
        Promise.resolve().then(() => onCreate?.()).catch((failure) => setActionError(profileError(t, failure)));
      }}>{!atCapacity ? <ProfileIcon name="add" /> : null}{t(atCapacity ? "profile.limitReached" : "profile.addAccount")}</button>
      {error ? <small className="profile-error">{profileError(t, error)}</small> : null}
      {launchError || actionError ? <small className="profile-error">{[launchError, actionError].filter(Boolean).join("\n")}</small> : null}
      {restartRequired ? <button type="button" className="profile-add" disabled={!!batchBusy || !onRestartAll} onClick={async () => {
        if (!onRestartAll) return;
        setBatchBusy("start");
        try { await onRestartAll(); setRestartRequired(false); setActionError(""); }
        catch (failure) { setActionError(profileError(t, failure)); }
        finally { setBatchBusy(""); }
      }}>{t("profile.restartAll")}</button> : null}
    </>}
    <button type="button" className="profile-collapse" aria-expanded={!isCollapsed} aria-label={t(isCollapsed ? "profile.expand" : "profile.collapse")} title={t(isCollapsed ? "profile.expand" : "profile.collapse")} onClick={() => setCollapsed((value) => !value)}><ProfileIcon name={isCollapsed ? "chevron-right" : "chevron-left"} /></button>
    {noteId ? <ProfileNoteDialog busy={busy} onClose={() => setNoteId("")}><form className="profile-dialog" onSubmit={(event) => {
      event.preventDefault(); setActionError("");
      if (!onUpdateNote) return;
      Promise.resolve(onUpdateNote(noteId, noteValue)).then(() => setNoteId("")).catch((failure) => setActionError(profileError(t, failure)));
    }}><h3 id="profile-note-title">{t("profile.noteDialogTitle")}</h3><input aria-label={t("profile.noteDialogTitle")} value={noteValue} maxLength={80} autoFocus placeholder={t("profile.notePlaceholder")} onChange={(event) => setNoteValue(event.target.value)} /><div className="profile-dialog-actions"><button type="button" disabled={busy} onClick={() => setNoteId("")}>{t("common.cancel")}</button><button type="submit" disabled={busy || !onUpdateNote}>{t("profile.saveNote")}</button></div></form></ProfileNoteDialog> : null}
  </section>;
}
