import { assertBridgeProfileOwner } from "./profileConfigDraft.js";

const clone = (value) => structuredClone(value);
const list = (value) => Array.isArray(value) ? value : [];
export const equipmentSlots = Object.freeze([1, 2, 3, 4]);
export function equipmentPositionSnapshot(snapshot) {
  return list(snapshot?.squads).map((squad) => ({
    squadIndex: squad.squadIndex,
    positions: list(squad.positions).map((position) => ({
      position: position.position, heroUuid: position.heroUuid,
      equips: list(position.equips).map((item) => ({ slot: item.slot, equipUuid: String(item.equipUuid) })),
    })),
  }));
}
export function normalizeEquipmentConfig(value, presetName = (index) => `Preset ${index}`) {
  const saved = list(value?.equipmentPresets).slice(0, 4);
  return {
    equipmentPresets: equipmentSlots.map((slot) => {
      const preset = saved[slot - 1];
      return preset && typeof preset.id === "string" && typeof preset.name === "string"
        ? { ...clone(preset), squads: list(preset.squads) }
        : { id: `equipment-slot-${slot}`, name: presetName(slot), squads: [] };
    }),
    ...(value?.initialEquipmentConfig ? { initialEquipmentConfig: clone(value.initialEquipmentConfig) } : {}),
  };
}
export function setEquipmentSlot(config, presetIndex, squadIndex, positionNumber, slot, equipUuid, snapshot) {
  const next = clone(config), preset = next.equipmentPresets[presetIndex];
  // A fresh preset is editable immediately. Retain all native hero/position
  // identities, with empty loadouts, so editing one slot never copies other
  // current gear implicitly or sends an incomplete formation on Apply.
  if (preset && !preset.squads.length && snapshot) {
    preset.squads = equipmentPositionSnapshot(snapshot).map((squad) => ({
      ...squad, positions: squad.positions.map((position) => ({ ...position, equips: [] })),
    }));
  }
  let squad = preset?.squads.find((entry) => entry.squadIndex === squadIndex);
  let position = squad?.positions.find((entry) => entry.position === positionNumber);
  if (preset && !position && snapshot) {
    const native = equipmentPositionSnapshot(snapshot).find((entry) => entry.squadIndex === squadIndex)?.positions.find((entry) => entry.position === positionNumber);
    if (native) {
      if (!squad) { squad = { squadIndex, positions: [] }; preset.squads.push(squad); }
      position = { ...native, equips: [] }; squad.positions.push(position);
      squad.positions.sort((left, right) => left.position - right.position);
    }
  }
  if (!position) throw new Error("Load the current equipment into this preset before editing it.");
  // A native equipment UUID can belong to only one saved position. Moving it
  // clears its former slot instead of creating a preset the native sender rejects.
  for (const squad of preset.squads) for (const entry of squad.positions)
    entry.equips = entry.equips.filter((item) => !(entry === position && item.slot === slot) && (!equipUuid || String(item.equipUuid) !== equipUuid));
  if (equipUuid) position.equips.push({ slot, equipUuid });
  position.equips.sort((left, right) => left.slot - right.slot);
  return next;
}
export function createEquipmentRuntime(bridge, profileId, { wait = (ms) => new Promise((resolve) => setTimeout(resolve, ms)), now = Date.now, timeoutMs = 30000 } = {}) {
  assertBridgeProfileOwner(bridge, profileId);
  const owner = bridge.currentProfileOwner();
  let pending = null;
  const assertOwner = () => {
    assertBridgeProfileOwner(bridge, profileId);
    if (!bridge.isCurrentProfileOwner(owner)) {
      const error = new Error("Equipment profile changed while an operation was pending.");
      error.code = "PROFILE_GENERATION_RETIRED"; throw error;
    }
  };
  const invoke = async (command, payload = {}) => {
    assertOwner();
    const result = await bridge.invokeProfileScoped(command, payload);
    assertOwner(); return result;
  };
  const snapshot = async () => {
    const value = await invoke("feature018_equipment_snapshot");
    if (value?.readOnly !== true || typeof value.ownerUid !== "string" || !Number.isInteger(value.serverId) || !Array.isArray(value.squads) || !Array.isArray(value.items)) throw new Error("Native equipment snapshot is unavailable.");
    return value;
  };
  const save = async (config, baseline) => {
    const next = clone(config);
    if (!next.initialEquipmentConfig && baseline) next.initialEquipmentConfig = { ownerUid: baseline.ownerUid, serverId: baseline.serverId, capturedAt: now(), squads: equipmentPositionSnapshot(baseline) };
    return invoke("equipment_config_save", next);
  };
  const poll = async (first) => {
    const deadline = now() + timeoutMs;
    let result = first;
    while (result?.equipmentApplied !== true) {
      if (now() >= deadline) {
        const error = new Error("Equipment confirmation is still pending. Resume confirmation before another apply.");
        error.code = "EQUIPMENT_CONFIRMATION_PENDING"; throw error;
      }
      await wait(150); assertOwner();
      result = await invoke(pending.command, { ...pending.payload, queryOnly: true });
    }
    pending = null;
    return { result, snapshot: await snapshot() };
  };
  const begin = async (command, payload) => {
    if (pending) throw new Error("An equipment transaction is unresolved. Resume its confirmation first.");
    assertOwner(); pending = { command, payload };
    let first;
    try { first = await invoke(command, payload); }
    catch (error) {
      // A timeout/disconnect may hide an accepted send. Keep the one-shot fence.
      if (!["COMMAND_TIMEOUT", "GAME_CONNECTION_UNAVAILABLE", "GAME_DISCONNECTED", "PROFILE_GENERATION_RETIRED"].includes(error.code)) pending = null;
      throw error;
    }
    return poll(first);
  };
  return {
    assertOwner, snapshot,
    readConfig: () => invoke("equipment_config_get"),
    save,
    apply: async (slot, config, baseline) => {
      if (!equipmentSlots.includes(slot)) throw new Error("Equipment preset slot must be 1–4.");
      if (pending) throw new Error("An equipment transaction is unresolved. Resume its confirmation first.");
      const saved = await save(config, baseline); assertOwner();
      return { ...(await begin("feature018_hotkey_action", { action: "equipment", slot })), config: saved };
    },
    restore: () => begin("feature018_equipment_restore", {}),
    resume: async () => {
      if (!pending) throw new Error("No equipment transaction is awaiting confirmation.");
      return { ...(await poll(await invoke(pending.command, { ...pending.payload, queryOnly: true }))), config: await invoke("equipment_config_get") };
    },
    get pending() { return pending !== null; },
    localize: (language, keys) => invoke("lastwar_map_localize", { language, keys }),
  };
}
