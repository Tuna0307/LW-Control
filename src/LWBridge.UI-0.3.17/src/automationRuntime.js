import { assertBridgeProfileOwner } from "./profileConfigDraft.js";
export const AUTOMATION_TASKS = { "Alliance Help": "allianceHelp" };
export function createAutomationAdapter(bridge, profileId, kind, online, valid) {
  const invoke = (command, payload = {}) => {
    assertBridgeProfileOwner(bridge, profileId);
    return bridge.invokeProfileScoped(command, payload);
  };
  return { valid, read: () => invoke("feature018_automation_config_get", { kind }),
    write: async config => {
      // Stop first: a failed disk write must never leave the old engine running.
      if (online && !config.enabled) await invoke("feature018_automation_stop", { kind });
      const saved = await invoke("feature018_automation_config_save", { kind, config });
      if (online) await invoke("feature018_automation_configure", { kind });
      return saved;
    },
  };
}
