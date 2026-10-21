import { assertBridgeProfileOwner } from "./profileConfigDraft.js";

// R30 performance: every visible native Automation card shares ONE status read per
// bridge/profile every 5 s instead of one game round-trip per card.
const hubs = new Map();

export function subscribeAutomationStatus(bridge, profileId, onServices, onError) {
  const key = bridge;
  let hub = hubs.get(key);
  if (!hub || hub.profileId !== profileId) {
    if (hub) clearInterval(hub.timer);
    hub = { profileId, listeners: new Set(), polling: false, timer: null, last: null };
    const read = async () => {
      if (hub.polling || hub.listeners.size === 0) return;
      hub.polling = true;
      try {
        assertBridgeProfileOwner(bridge, profileId);
        const result = await bridge.invokeProfileScoped("feature018_automation_status");
        hub.last = result.services ?? null;
        for (const listener of hub.listeners) listener.onServices(hub.last);
      } catch (error) {
        for (const listener of hub.listeners) listener.onError?.(error);
      } finally { hub.polling = false; }
    };
    hub.read = read;
    hub.timer = setInterval(read, 5000);
    hubs.set(key, hub);
  }
  const listener = { onServices, onError };
  hub.listeners.add(listener);
  if (hub.last) onServices(hub.last);
  void hub.read();
  return () => {
    hub.listeners.delete(listener);
    if (hub.listeners.size === 0) { clearInterval(hub.timer);if (hubs.get(key) === hub) hubs.delete(key); }
  };
}
