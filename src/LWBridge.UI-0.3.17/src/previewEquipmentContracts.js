export const EQUIPMENT_SLOTS = Object.freeze([1, 2, 3, 4]);
export const EQUIPMENT_PRESET_LIMIT = 4;

export function cloneEquipmentValue(value) {
  return structuredClone(value);
}

export function equipmentSnapshotFromSquads(squads) {
  return [...squads]
    .filter((squad) => squad.index >= 1 && squad.index <= 4)
    .sort((left, right) => left.index - right.index)
    .map((squad) => ({
      squadIndex: squad.index,
      positions: squad.heroes.slice(0, 5).map((hero, index) => ({
        position: index + 1,
        equips: hero.equips.map((equip) => ({
          slot: equip.slot,
          equipUuid: equip.uuid,
          name: equip.name,
          level: equip.level,
          quality: equip.quality,
          iconPath: equip.iconPath,
          promote: equip.promote,
          heroType: equip.heroType,
        })),
      })),
    }));
}

export function findEquipmentSquad(squads, squadIndex) {
  return squads.find((squad) => squad.squadIndex === squadIndex);
}

export function ensureEquipmentPosition(squads, squadIndex, position) {
  let squad = findEquipmentSquad(squads, squadIndex);
  if (!squad) {
    squad = { squadIndex, positions: [] };
    squads.push(squad);
  }
  let target = squad.positions.find((entry) => entry.position === position);
  if (!target) {
    target = { position, equips: [] };
    squad.positions.push(target);
    squad.positions.sort((left, right) => left.position - right.position);
  }
  return target;
}

export function equipmentItemCount(preset) {
  return preset.squads.reduce((count, squad) => count + squad.positions.reduce((sum, position) => sum + position.equips.length, 0), 0);
}

export function equipmentPositionCount(preset) {
  return preset.squads.reduce((count, squad) => count + squad.positions.filter((position) => position.equips.length > 0).length, 0);
}

export function equipmentSquadMatches(presetSquad, liveSquad) {
  if (!presetSquad || !liveSquad.heroes.length) return false;
  return liveSquad.heroes.slice(0, 5).every((hero, index) => {
    const position = presetSquad.positions.find((entry) => entry.position === index + 1);
    if (!position) return false;
    const current = new Map(hero.equips.map((equip) => [equip.slot, String(equip.uuid)]));
    const saved = new Map(position.equips.map((equip) => [equip.slot, String(equip.equipUuid)]));
    return EQUIPMENT_SLOTS.every((slot) => (current.get(slot) || "") === (saved.get(slot) || ""));
  });
}

export function equipmentDirtyPresetIds(presets, confirmedPresets) {
  return presets
    .filter((preset) => JSON.stringify(preset) !== JSON.stringify(confirmedPresets.find((confirmed) => confirmed.id === preset.id)))
    .map((preset) => preset.id);
}

export function currentEquipmentPresetMatches(presets, selectedPreset, liveSquads) {
  const ordered = selectedPreset
    ? [selectedPreset, ...presets.filter((preset) => preset.id !== selectedPreset.id)]
    : presets;
  const result = new Map();
  for (const liveSquad of liveSquads) {
    const match = ordered.find((preset) => equipmentSquadMatches(findEquipmentSquad(preset.squads, liveSquad.index), liveSquad));
    if (match) result.set(liveSquad.index, match);
  }
  return result;
}

export function currentEquipmentPresetLabel(matches, squadIndexes, t) {
  const matched = squadIndexes.map((index) => matches.get(index)).filter(Boolean);
  if (!matched.length) return t("squad.unmatchedEquipmentPreset");
  if (matched.length !== squadIndexes.length || new Set(matched.map((preset) => preset.id)).size > 1) return t("squad.mixedEquipmentPreset");
  return matched[0]?.name || t("squad.unmatchedEquipmentPreset");
}

export function equipmentCatalog(initialEquipmentConfig, liveSquads) {
  const result = new Map();
  for (const squad of initialEquipmentConfig?.squads || []) {
    for (const position of squad.positions) {
      for (const equip of position.equips) result.set(equip.equipUuid, equip);
    }
  }
  for (const squad of liveSquads) {
    for (const hero of squad.heroes) {
      for (const equip of hero.equips) {
        result.set(equip.uuid, {
          slot: equip.slot,
          equipUuid: equip.uuid,
          name: equip.name,
          level: equip.level,
          quality: equip.quality,
          iconPath: equip.iconPath,
          promote: equip.promote,
          heroType: equip.heroType,
        });
      }
    }
  }
  return result;
}

export function swapEquipmentTarget(presets, selectedPresetId, dragged, target) {
  if (!dragged || dragged.kind === "squad" || dragged.kind !== target.kind) return { presets, handled: false, successKeys: [], error: "" };
  const sourceKey = `${dragged.squadIndex}-${dragged.position}`;
  const targetKey = `${target.squadIndex}-${target.position}`;
  if (sourceKey === targetKey) return { presets, handled: false, successKeys: [], error: "" };
  if (dragged.kind === "equip" && target.kind === "equip" && dragged.slot !== target.slot) {
    return { presets, handled: true, successKeys: [], error: "squad.sameSlotRequired" };
  }
  const next = cloneEquipmentValue(presets);
  const preset = next.find((entry) => entry.id === selectedPresetId);
  if (!preset) return { presets, handled: false, successKeys: [], error: "" };
  const sourcePosition = ensureEquipmentPosition(preset.squads, dragged.squadIndex, dragged.position);
  const targetPosition = ensureEquipmentPosition(preset.squads, target.squadIndex, target.position);
  if (dragged.kind === "loadout") {
    [sourcePosition.equips, targetPosition.equips] = [targetPosition.equips, sourcePosition.equips];
  } else {
    const slot = Number(dragged.slot);
    const sourceIndex = sourcePosition.equips.findIndex((equip) => equip.slot === slot);
    if (sourceIndex < 0) return { presets, handled: false, successKeys: [], error: "" };
    const targetIndex = targetPosition.equips.findIndex((equip) => equip.slot === slot);
    const sourceEquip = sourcePosition.equips[sourceIndex];
    const targetEquip = targetIndex >= 0 ? targetPosition.equips[targetIndex] : null;
    if (targetEquip) sourcePosition.equips[sourceIndex] = targetEquip;
    else sourcePosition.equips.splice(sourceIndex, 1);
    if (targetIndex >= 0) targetPosition.equips[targetIndex] = sourceEquip;
    else targetPosition.equips.push(sourceEquip);
    sourcePosition.equips.sort((left, right) => left.slot - right.slot);
    targetPosition.equips.sort((left, right) => left.slot - right.slot);
  }
  return { presets: next, handled: true, successKeys: [sourceKey, targetKey], error: "" };
}

export function swapEquipmentSquads(presets, selectedPresetId, sourceSquadIndex, targetSquadIndex, liveSquads) {
  if (sourceSquadIndex === targetSquadIndex) return { presets, handled: false, successKeys: [] };
  const next = cloneEquipmentValue(presets);
  const preset = next.find((entry) => entry.id === selectedPresetId);
  if (!preset) return { presets, handled: false, successKeys: [] };
  const sourceSquad = findEquipmentSquad(preset.squads, sourceSquadIndex);
  const targetSquad = findEquipmentSquad(preset.squads, targetSquadIndex);
  const sourceHeroCount = liveSquads.find((squad) => squad.index === sourceSquadIndex)?.heroes.length || 0;
  const targetHeroCount = liveSquads.find((squad) => squad.index === targetSquadIndex)?.heroes.length || 0;
  const count = Math.min(sourceHeroCount, targetHeroCount, 5);
  if (!sourceSquad || !targetSquad || !count) return { presets, handled: false, successKeys: [] };
  const successKeys = [];
  for (let position = 1; position <= count; position += 1) {
    const sourcePosition = ensureEquipmentPosition(preset.squads, sourceSquadIndex, position);
    const targetPosition = ensureEquipmentPosition(preset.squads, targetSquadIndex, position);
    [sourcePosition.equips, targetPosition.equips] = [targetPosition.equips, sourcePosition.equips];
    successKeys.push(`${sourceSquadIndex}-${position}`, `${targetSquadIndex}-${position}`);
  }
  return { presets: next, handled: true, successKeys };
}

function makeFixtureHero(squadIndex, position, equipCount = 2) {
  return {
    uuid: `fixture-hero-${squadIndex}-${position}`,
    name: `Fixture Hero ${squadIndex}-${position}`,
    level: 31 - position,
    iconPath: "",
    equips: Array.from({ length: equipCount }, (_, index) => ({
      slot: index + 1,
      uuid: `fixture-equip-${squadIndex}-${position}-${index + 1}`,
      name: `Fixture Item ${squadIndex}-${position}-${index + 1}`,
      level: 40 - position - index,
      quality: position === 1 && index === 0 ? 5 : 4,
      iconPath: "",
      promote: position === 1 && index === 0 ? 1 : 0,
      heroType: squadIndex,
    })),
  };
}

function makeFixturePreset(id, name, snapshot, variant) {
  const squads = cloneEquipmentValue(snapshot);
  if (variant === "alternate") {
    for (const squad of squads) for (const position of squad.positions) for (const equip of position.equips) equip.equipUuid = `${equip.equipUuid}-alt`;
  } else if (variant === "empty") {
    for (const squad of squads) for (const position of squad.positions) position.equips = [];
  } else if (variant === "rotated") {
    for (const squad of squads) {
      const sets = squad.positions.map((position) => position.equips);
      if (sets.length > 1) squad.positions.forEach((position, index) => { position.equips = sets[(index + 1) % sets.length]; });
    }
  }
  return { id, name, squads };
}

export function previewEquipmentFixture(previewState, t) {
  if (!String(previewState || "").startsWith("squads-equipment")) {
    return { online: false, squads: [], presets: [], confirmedPresets: [], initialEquipmentConfig: null, busyKey: "", result: null, progress: null, renameOpen: false };
  }
  const squads = [
    { index: 1, heroes: [makeFixtureHero(1, 1), makeFixtureHero(1, 2), makeFixtureHero(1, 3, 1)] },
    { index: 2, heroes: [makeFixtureHero(2, 1), makeFixtureHero(2, 2)] },
    { index: 3, heroes: [makeFixtureHero(3, 1)] },
    { index: 4, heroes: [makeFixtureHero(4, 1), makeFixtureHero(4, 2, 1)] },
  ];
  const snapshot = equipmentSnapshotFromSquads(squads);
  const presets = [
    makeFixturePreset("equipment-preset-fixed-1", "Fixture equipment preset 1", snapshot, "current"),
    makeFixturePreset("equipment-preset-fixed-2", "Fixture equipment preset 2", snapshot, "alternate"),
    makeFixturePreset("equipment-preset-fixed-3", "Fixture equipment preset 3", snapshot, "empty"),
    makeFixturePreset("equipment-preset-fixed-4", "Fixture equipment preset 4", snapshot, "rotated"),
  ];
  const result = previewState === "squads-equipment-result"
    ? { state: "partial", applied: 7, requested: 8, failedHeroName: squads[3].heroes[1].name, reason: "EQUIPMENT_STATE_CHANGED" }
    : previewState === "squads-equipment-error"
      ? { state: "rejected", applied: 0, requested: 8, failedHeroName: "", reason: "EQUIPMENT_REQUEST_REJECTED" }
      : null;
  const progress = previewState === "squads-equipment-progress"
    ? { phase: "running", current: 3, total: 8, heroName: squads[1].heroes[0].name }
    : null;
  return {
    online: previewState !== "squads-equipment-offline",
    squads,
    presets,
    confirmedPresets: cloneEquipmentValue(presets),
    initialEquipmentConfig: { capturedAt: 1, squads: cloneEquipmentValue(snapshot) },
    busyKey: previewState === "squads-equipment-rename-busy" ? "rename" : progress ? "apply-all" : "",
    result,
    progress,
    renameOpen: previewState === "squads-equipment-rename" || previewState === "squads-equipment-rename-busy",
  };
}
