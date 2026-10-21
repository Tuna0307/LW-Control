import { assertBridgeProfileOwner } from "./profileConfigDraft.js";
export const AUTOMATION_TASKS = { "Alliance Help": "allianceHelp", "Alliance Gifts": "allianceGift", "Alliance Tech Donations": "allianceDonate" };
export function validAutomationDraft(kind, draft) {
  return typeof draft.enabled === "boolean" && (kind !== "allianceGift" || Number.isInteger(Number(draft.intervalMinutes)) && Number(draft.intervalMinutes) >= 1 && Number(draft.intervalMinutes) <= 1440)
    && (kind !== "allianceDonate" || Number.isInteger(Number(draft.threshold)) && Number(draft.threshold) >= 1 && Number(draft.threshold) <= 30);
}
export function createAutomationAdapter(bridge, profileId, kind, online, valid) {
  const invoke = (command, payload = {}) => {
    assertBridgeProfileOwner(bridge, profileId);
    return bridge.invokeProfileScoped(command, payload);
  };
  return { valid, read: () => invoke("feature018_automation_config_get", { kind }),
    write: async config => {
      config = { ...config };
      if (kind === "allianceGift") config.intervalMinutes = Number(config.intervalMinutes);
      if (kind === "allianceDonate") config.threshold = Number(config.threshold);
      // Stop first: a failed disk write must never leave the old engine running.
      if (online && !config.enabled) await invoke("feature018_automation_stop", { kind });
      const saved = await invoke("feature018_automation_config_save", { kind, config });
      if (online) await invoke("feature018_automation_configure", { kind });
      return saved;
    },
  };
}
