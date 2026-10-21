import { assertBridgeProfileOwner } from "./profileConfigDraft.js";
import { MISSION_KINDS, normalizeMissionDraft, validMissionDraft } from "./NativeMissionFields.jsx";
export const AUTOMATION_TASKS = { "Alliance Help": "allianceHelp", "Alliance Gifts": "allianceGift", "Alliance Tech Donations": "allianceDonate", "Automatic Official Application": "officialPosition", "Automatic Treatment": "treatment",
  "Secret Task": "dispatch", "Ghost Ops": "ghostRecon", "Excavation Stronghold Resources": "strongholdResource", "Alliance Center Resources": "allianceCenterResource" };
const intervalKinds = ["allianceGift", "strongholdResource", "allianceCenterResource"];
export function validAutomationDraft(kind, draft) {
  if (MISSION_KINDS.includes(kind)) return validMissionDraft(kind, draft);
  if (kind === "resourceGather") {
    const squads = Array.isArray(draft.squads) ? draft.squads : [];
    return typeof draft.enabled === "boolean" && [50, 100, 150, 200, 250, 300, 400, 500].includes(Number(draft.scanRadius ?? 200))
      && squads.every(s => [1, 2, 3, 4].includes(s.squadIndex) && typeof s.enabled === "boolean" && ["metal", "food", "gold"].includes(s.resource) && Number.isInteger(s.level) && s.level >= 1 && s.level <= 10)
      && (!draft.enabled || squads.some(s => s.enabled));
  }
  if (kind === "tradeStation") return typeof draft.enabled === "boolean" && Array.isArray(draft.selectedItemIds) && Array.isArray(draft.selectedCurrencyIds)
    && draft.selectedCurrencyIds.length > 0 && (!draft.enabled || draft.selectedItemIds.length > 0);
  return typeof draft.enabled === "boolean" && (!intervalKinds.includes(kind) || Number.isInteger(Number(draft.intervalMinutes)) && Number(draft.intervalMinutes) >= 1 && Number(draft.intervalMinutes) <= 1440)
    && (kind !== "treatment" || Number.isInteger(Number(draft.amountPerArmy)) && Number(draft.amountPerArmy) >= 1 && Number(draft.amountPerArmy) <= 1000000)
    && (kind !== "allianceDonate" || Number.isInteger(Number(draft.threshold)) && Number(draft.threshold) >= 1 && Number(draft.threshold) <= 30)
    && (kind !== "officialPosition" || [0, 10002, 10003, 10004, 10005, 10006, 10007].includes(Number(draft.positionId)) && (!draft.enabled || Number(draft.positionId) !== 0));
}
export function createAutomationAdapter(bridge, profileId, kind, online, valid) {
  const invoke = (command, payload = {}) => {
    assertBridgeProfileOwner(bridge, profileId);
    return bridge.invokeProfileScoped(command, payload);
  };
  return { valid, read: () => invoke("feature018_automation_config_get", { kind }),
    write: async config => {
      config = { ...config };
      if (intervalKinds.includes(kind)) config.intervalMinutes = Number(config.intervalMinutes);
      if (MISSION_KINDS.includes(kind)) config = normalizeMissionDraft(kind, config);
      if (kind === "allianceDonate") config.threshold = Number(config.threshold);
      if (kind === "officialPosition") config.positionId = Number(config.positionId);
      if (kind === "treatment") config.amountPerArmy = Number(config.amountPerArmy);
      // Stop first: a failed disk write must never leave the old engine running.
      if (online && !config.enabled) await invoke("feature018_automation_stop", { kind });
      const saved = await invoke("feature018_automation_config_save", { kind, config });
      if (online) await invoke("feature018_automation_configure", { kind });
      return saved;
    },
  };
}
