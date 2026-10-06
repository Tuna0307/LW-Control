const DEFAULT_TIMEOUT_MS = 30_000;

function unavailableError(mode) {
  const error = new Error(
    mode === "native-unavailable"
      ? "Native WebView transport is unavailable in live mode."
      : "Native backend is unavailable in browser preview mode.",
  );
  error.code = mode === "native-unavailable" ? "NATIVE_TRANSPORT_MISSING" : "PREVIEW_NO_NATIVE_HOST";
  return error;
}

function profileRequiredError() {
  const error = new Error("Select a profile before changing profile-scoped automation settings.");
  error.code = "PROFILE_REQUIRED";
  return error;
}

function profileRetiredError(profileId) {
  const error = new Error(`Profile owner retired before the command completed: ${profileId || "<none>"}.`);
  error.code = "PROFILE_GENERATION_RETIRED";
  return error;
}

export function createBackendBridge(host = {}) {
  const bootstrap = host.__LWBridgeBootstrap || {};
  const nativeWebView = host.chrome?.webview;
  const liveRequested = bootstrap.mode === "live";
  const sessionId = typeof bootstrap.sessionId === "string" ? bootstrap.sessionId : "";
  const available = liveRequested && !!nativeWebView && sessionId.length > 0;
  const mode = available ? "native" : liveRequested ? "native-unavailable" : "preview";
  let profileId = bootstrap.profiles?.selectedProfileId || "";
  let profileGeneration = 0;
  const pending = new Map();
  const listeners = new Map();
  let disposed = false;

  function dispatchEvent(eventName, payload) {
    for (const listener of listeners.get(eventName) || []) listener(payload);
  }

  function onMessage(event) {
    let message = event?.data;
    if (typeof message === "string") {
      try { message = JSON.parse(message); }
      catch { return; }
    }
    if (!message || message.sessionId !== sessionId) return;

    if (message.kind === "response") {
      const request = pending.get(message.id);
      if (!request) return;
      pending.delete(message.id);
      host.clearTimeout?.(request.timer);
      if (message.ok) request.resolve(message.result);
      else {
        const error = new Error(message.error?.message || "Native command failed.");
        error.code = message.error?.code || "NATIVE_COMMAND_FAILED";
        error.details = message.error?.details;
        request.reject(error);
      }
      return;
    }

    if (message.kind === "event" && typeof message.event === "string") {
      dispatchEvent(message.event, message.payload);
    }
  }

  if (available) nativeWebView.addEventListener("message", onMessage);

  function nextRequestId() {
    return host.crypto?.randomUUID?.() || `${Date.now()}-${Math.random()}`;
  }

  function invoke(command, payload = {}, timeoutMs = DEFAULT_TIMEOUT_MS) {
    if (!available || disposed) return Promise.reject(unavailableError(mode));
    const id = nextRequestId();
    return new Promise((resolve, reject) => {
      const timer = host.setTimeout?.(() => {
        pending.delete(id);
        nativeWebView.postMessage({ kind: "cancel", sessionId, id });
        const error = new Error(`Native command timed out: ${command}`);
        error.code = "COMMAND_TIMEOUT";
        reject(error);
      }, timeoutMs);
      pending.set(id, { resolve, reject, timer });
      nativeWebView.postMessage({ kind: "invoke", sessionId, id, command, payload });
    });
  }

  function invokeProfileScoped(command, payload = {}, timeoutMs = DEFAULT_TIMEOUT_MS) {
    if (!profileId) return Promise.reject(profileRequiredError());
    const owner = currentProfileOwner();
    return invoke(command, { ...payload, profileId: owner.profileId }, timeoutMs).then(
      (result) => {
        if (!isCurrentProfileOwner(owner)) throw profileRetiredError(owner.profileId);
        return result;
      },
      (error) => {
        if (!isCurrentProfileOwner(owner)) throw profileRetiredError(owner.profileId);
        throw error;
      },
    );
  }

  function currentProfileOwner() {
    return { profileId, generation: profileGeneration };
  }

  function isCurrentProfileOwner(owner) {
    return !!owner
      && owner.profileId === profileId
      && owner.generation === profileGeneration;
  }

  function setSelectedProfile(nextProfileId) {
    const next = typeof nextProfileId === "string" ? nextProfileId : "";
    if (next === profileId) return currentProfileOwner();
    profileId = next;
    profileGeneration += 1;
    return currentProfileOwner();
  }

  function listen(eventName, callback) {
    if (!available || disposed) return () => {};
    if (!listeners.has(eventName)) {
      listeners.set(eventName, new Set());
      nativeWebView.postMessage({ kind: "listen", sessionId, event: eventName });
    }
    listeners.get(eventName).add(callback);
    return () => {
      const set = listeners.get(eventName);
      set?.delete(callback);
      if (set?.size === 0) {
        listeners.delete(eventName);
        nativeWebView.postMessage({ kind: "unlisten", sessionId, event: eventName });
      }
    };
  }

  function dispose() {
    if (disposed) return;
    disposed = true;
    if (available) {
      for (const eventName of listeners.keys()) {
        nativeWebView.postMessage({ kind: "unlisten", sessionId, event: eventName });
      }
      nativeWebView.removeEventListener?.("message", onMessage);
    }
    listeners.clear();
    for (const [id, request] of pending) {
      host.clearTimeout?.(request.timer);
      const error = new Error("Native bridge closed before the command completed.");
      error.code = "NATIVE_BRIDGE_CLOSED";
      request.reject(error);
      pending.delete(id);
    }
  }

  return {
    mode,
    available,
    sessionId,
    get profileId() { return profileId; },
    get profileGeneration() { return profileGeneration; },
    currentProfileOwner,
    isCurrentProfileOwner,
    setSelectedProfile,
    invoke,
    invokeProfileScoped,
    listen,
    dispose,
  };
}

export const backendBridge = createBackendBridge(typeof window === "undefined" ? {} : window);
