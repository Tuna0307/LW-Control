import { assertBridgeProfileOwner } from "./profileConfigDraft.js";

// Four cards consume one read. An A -> B -> A selection still retires A's
// previous generation, including an already in-flight result.
export function createChatStatusSubscription(clock = globalThis) {
  const hubs = new WeakMap();
  return (bridge, profileId, onStatus, onError) => {
    assertBridgeProfileOwner(bridge, profileId);
    const owner = bridge.currentProfileOwner?.() ?? { profileId };
    const current = hub => bridge.profileId === profileId &&
      (bridge.isCurrentProfileOwner ? bridge.isCurrentProfileOwner(hub.owner) :
        bridge.currentProfileOwner ? bridge.currentProfileOwner().generation === hub.owner.generation : true);
    const stop = hub => {
      hub.stopped = true;
      clock.clearInterval(hub.timer);
      if (hubs.get(bridge) === hub) hubs.delete(bridge);
    };
    let hub = hubs.get(bridge);
    if (hub && (hub.owner.profileId !== profileId || !current(hub))) { stop(hub); hub = null; }
    if (!hub) {
      hub = { owner, listeners: new Set(), stopped: false, polling: false, last: null };
      hub.read = async () => {
        if (hub.stopped || hub.polling || !hub.listeners.size) return;
        if (!current(hub)) { stop(hub); return; }
        hub.polling = true;
        try {
          const status = await bridge.invokeProfileScoped("feature018_chat_status");
          if (hub.stopped) return;
          if (!current(hub)) { stop(hub); return; }
          hub.last = status;
          for (const listener of hub.listeners) listener.onStatus(status);
        } catch (error) {
          if (hub.stopped) return;
          if (!current(hub)) { stop(hub); return; }
          for (const listener of hub.listeners) listener.onError?.(error);
        } finally { hub.polling = false; }
      };
      hub.timer = clock.setInterval(hub.read, 2000);
      hubs.set(bridge, hub);
    }
    const listener = { onStatus, onError };
    hub.listeners.add(listener);
    if (hub.last) onStatus(hub.last);
    void hub.read();
    return () => {
      hub.listeners.delete(listener);
      if (!hub.listeners.size) stop(hub);
    };
  };
}

export const subscribeChatStatus = createChatStatusSubscription();
