import { assertBridgeProfileOwner } from "./profileConfigDraft.js";

const clone = (value) => structuredClone(value);
const list = (value) => Array.isArray(value) ? value : [];
export const equipmentSlots = Object.freeze([1, 2, 3, 4]);
export function equipmentPositionSnapshot(snapshot) {
  return list(snapshot?.squads).map((squad) => ({
    squadIndex: squad.squadIndex,
    positions: list(squad.positions).map((position) => ({
      position: position.position, heroUuid: position.heroUuid,
      // Walk the four native slot types explicitly so a capture never becomes
      // "whatever entries happened to be present in an old rendered array".
      // Empty slots remain empty (the persisted native schema intentionally
      // stores no fake UUID), while every equipped slot is copied once.
      equips: equipmentSlots.flatMap((slot) => {
        const item = list(position.equips).find((entry) => entry.slot === slot);
        return item ? [{ slot, equipUuid: String(item.equipUuid) }] : [];
      }),
    })),
  }));
}
export function equipmentPresetCoversSnapshot(preset, snapshot) {
  const liveSquads = equipmentPositionSnapshot(snapshot);
  if (!preset || !liveSquads.length) return false;
  return liveSquads.every((liveSquad) => {
    const savedSquad = list(preset.squads).find((entry) => entry.squadIndex === liveSquad.squadIndex);
    return savedSquad && savedSquad.positions.length === liveSquad.positions.length &&
      liveSquad.positions.every((livePosition) => {
        const saved = savedSquad.positions.find((entry) => entry.position === livePosition.position);
        return saved?.heroUuid === livePosition.heroUuid;
      });
  }) && list(preset.squads).length === liveSquads.length;
}
function savedPosition(preset, squadIndex, position) {
  return list(preset?.squads).find((entry) => entry.squadIndex === squadIndex)
    ?.positions.find((entry) => entry.position === position);
}
export function swapNativeEquipmentTarget(config, presetIndex, dragged, target) {
  if (!dragged || !target || dragged.kind !== target.kind || dragged.kind === "squad")
    return { config, handled: false, error: "" };
  if (dragged.squadIndex === target.squadIndex && dragged.position === target.position)
    return { config, handled: false, error: "" };
  if (dragged.kind === "equip" && dragged.slot !== target.slot)
    return { config, handled: true, error: "squad.sameSlotRequired" };
  const next = clone(config), preset = next.equipmentPresets[presetIndex];
  const source = savedPosition(preset, dragged.squadIndex, dragged.position);
  const destination = savedPosition(preset, target.squadIndex, target.position);
  if (!source || !destination || !source.heroUuid || !destination.heroUuid)
    return { config, handled: false, error: "" };
  if (dragged.kind === "loadout") {
    [source.equips, destination.equips] = [destination.equips, source.equips];
    return { config: next, handled: true, error: "" };
  }
  const slot = Number(dragged.slot);
  const sourceIndex = source.equips.findIndex((entry) => entry.slot === slot);
  if (sourceIndex < 0) return { config, handled: false, error: "" };
  const targetIndex = destination.equips.findIndex((entry) => entry.slot === slot);
  const sourceItem = source.equips[sourceIndex], targetItem = targetIndex >= 0 ? destination.equips[targetIndex] : null;
  if (targetItem) source.equips[sourceIndex] = targetItem;
  else source.equips.splice(sourceIndex, 1);
  if (targetIndex >= 0) destination.equips[targetIndex] = sourceItem;
  else destination.equips.push(sourceItem);
  source.equips.sort((left, right) => left.slot - right.slot);
  destination.equips.sort((left, right) => left.slot - right.slot);
  return { config: next, handled: true, error: "" };
}
export function swapNativeEquipmentSquads(config, presetIndex, sourceSquadIndex, targetSquadIndex, snapshot) {
  if (sourceSquadIndex === targetSquadIndex) return { config, handled: false };
  const next = clone(config), preset = next.equipmentPresets[presetIndex];
  if (!preset || !equipmentPresetCoversSnapshot(preset, snapshot)) return { config, handled: false };
  const sourceLive = list(snapshot?.squads).find((entry) => entry.squadIndex === sourceSquadIndex);
  const targetLive = list(snapshot?.squads).find((entry) => entry.squadIndex === targetSquadIndex);
  const targetPositions = new Set(list(targetLive?.positions).map((entry) => entry.position));
  const shared = list(sourceLive?.positions).map((entry) => entry.position).filter((position) => targetPositions.has(position)).slice(0, 5);
  if (!shared.length) return { config, handled: false };
  for (const position of shared) {
    const source = savedPosition(preset, sourceSquadIndex, position);
    const destination = savedPosition(preset, targetSquadIndex, position);
    const sourceHero = sourceLive.positions.find((entry) => entry.position === position)?.heroUuid;
    const targetHero = targetLive.positions.find((entry) => entry.position === position)?.heroUuid;
    if (!source || !destination || source.heroUuid !== sourceHero || destination.heroUuid !== targetHero)
      return { config, handled: false };
    [source.equips, destination.equips] = [destination.equips, source.equips];
  }
  return { config: next, handled: true };
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
