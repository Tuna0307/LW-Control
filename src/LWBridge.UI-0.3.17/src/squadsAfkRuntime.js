import { assertBridgeProfileOwner } from "./profileConfigDraft.js";
import { normalizeJoinRestrictions } from "./previewAfkContracts.js";

export const emptyMonsterAfkConfig = () => ({ enabled: false, strategies: [], allianceDrill: { enabled: false, activeRally: false, squadIndexes: [], joinRestrictions: normalizeJoinRestrictions(undefined, 1, true) } });
export const emptyStaminaConfig = () => ({ enabled: false, minStamina: 10, preferFifty: false });
export function normalizeMonsterAfkConfig(value) {
  const config = value && typeof value === "object" ? value : emptyMonsterAfkConfig();
  const strategies = (Array.isArray(config.strategies) ? config.strategies : []).map((profile) => ({
    targetKey: "", name: "", targetNameQuery: "", monsterIds: [], squadIndexes: [],
    executionLimit: 0, minLevel: 1, maxLevel: 99, maxDistance: 200,
    ...profile,
    ...(profile.kind === "join" ? { joinRestrictions: normalizeJoinRestrictions(profile.joinRestrictions, 1, true) } : {}),
  }));
  return { ...emptyMonsterAfkConfig(), ...config, strategies, allianceDrill: { ...emptyMonsterAfkConfig().allianceDrill, ...config.allianceDrill, joinRestrictions: normalizeJoinRestrictions(config.allianceDrill?.joinRestrictions, 1, true) } };
}
export function normalizeAfkCatalog(value = {}) {
  return { ...value, options: (Array.isArray(value.options) ? value.options : []).map((target) => ({ ...target, monsterIds: Array.isArray(target.monsterIds) ? target.monsterIds : [] })), unlockedSquads: Array.isArray(value.unlockedSquads) ? value.unlockedSquads : [] };
}
function connectionUnavailableError() {
  return Object.assign(new Error("Game connection is unavailable."), { code: "GAME_CONNECTION_UNAVAILABLE" });
}
export function createMonsterAfkAdapter(bridge, profileId, valid, isOnline = () => true) {
  const invoke = (command, payload = {}) => { assertBridgeProfileOwner(bridge, profileId); if (!isOnline()) throw connectionUnavailableError(); return bridge.invokeProfileScoped(command, payload); };
  return {
    valid,
    read: async () => normalizeMonsterAfkConfig((await invoke("get_status"))?.config?.tasks?.monsterSweep),
    write: async (draft) => {
      // Stop is sent first: a disk failure must not leave new sends enabled.
      if (!draft.enabled) {
        try { await invoke("automation_stop", { taskName: "monsterSweep" }); }
        catch (error) { if (!["GAME_CONNECTION_UNAVAILABLE", "GAME_DISCONNECTED"].includes(error.code)) throw error; }
      }
      if (!draft.allianceDrill.enabled) {
        try { await invoke("automation_stop", { taskName: "allianceDrill" }); }
        catch (error) { if (!["GAME_CONNECTION_UNAVAILABLE", "GAME_DISCONNECTED"].includes(error.code)) throw error; }
      }
      const saved = normalizeMonsterAfkConfig(await invoke("monster_afk_config_save", draft));
      if (saved.allianceDrill.enabled) await invoke("automation_start", { taskName: "allianceDrill" });
      if (saved.enabled) await invoke("feature018_afk_configure");
      return saved;
    },
  };
}
export function createStaminaPotionAdapter(bridge, profileId, valid, isOnline = () => true) {
  const invoke = (command, payload = {}) => { assertBridgeProfileOwner(bridge, profileId); if (!isOnline()) throw connectionUnavailableError(); return bridge.invokeProfileScoped(command, payload); };
  return {
    valid,
    read: async () => ({ ...emptyStaminaConfig(), ...((await invoke("get_status"))?.config?.tasks?.staminaPotion || {}) }),
    write: async (draft) => {
      if (!draft.enabled) {
        try { await invoke("automation_stop", { taskName: "staminaPotion" }); }
        catch (error) { if (!["GAME_CONNECTION_UNAVAILABLE", "GAME_DISCONNECTED"].includes(error.code)) throw error; }
      }
      const saved = await invoke("stamina_potion_config_save", draft);
      if (saved.enabled) await invoke("automation_start", { taskName: "staminaPotion" });
      return saved;
    },
  };
}
