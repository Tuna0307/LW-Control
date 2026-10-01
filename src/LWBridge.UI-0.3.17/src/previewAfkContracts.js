// Recovered join normalization and validation, SquadPanel-HC3-DJei.js oe/se.
export function normalizeJoinRestrictions(e2, t2 = 1, n2 = false) {
  let r2 = e2?.mode === `slotRange`, i2 = e2?.slotRange ?? [t2 + 1, t2 + 1];
  return { enabled: e2?.enabled ?? !n2, mode: r2 ? `slot` : e2?.mode ?? (n2 ? `delay` : `slot`), slotRange: r2 ? [i2[0], i2[0]] : [i2[0], i2[1]], slotDelaySeconds: r2 ? 0 : e2?.slotDelaySeconds ?? 0, delaySeconds: e2?.delaySeconds ?? (n2 ? [1, 3] : [0, 0]), maxWaitMinutes: e2?.maxWaitMinutes ?? (n2 ? 5 : 0), leaderListMode: e2?.leaderListMode ?? `off`, leaders: e2?.leaders ?? [], skipSoloLeader: e2?.skipSoloLeader ?? false, skipKicked: e2?.skipKicked ?? n2 };
}
export function validJoinRestrictions(e2) {
  return typeof e2.enabled == `boolean` && [`slot`, `delay`].includes(e2.mode) && Number.isFinite(e2.slotDelaySeconds) && e2.slotDelaySeconds >= 0 && e2.slotDelaySeconds <= 6e3 && Math.abs(e2.slotDelaySeconds * 100 - Math.round(e2.slotDelaySeconds * 100)) < 1e-6 && Array.isArray(e2.slotRange) && e2.slotRange.length === 2 && e2.slotRange.every((e3) => Number.isSafeInteger(e3) && e3 >= 2 && e3 <= 5) && e2.slotRange[1] >= e2.slotRange[0] && Array.isArray(e2.delaySeconds) && e2.delaySeconds.length === 2 && e2.delaySeconds.every((e3) => Number.isFinite(e3) && e3 >= 0 && e3 <= 6e3 && Math.abs(e3 * 100 - Math.round(e3 * 100)) < 1e-6) && e2.delaySeconds[1] >= e2.delaySeconds[0] && Number.isFinite(e2.maxWaitMinutes) && e2.maxWaitMinutes >= 0 && [`off`, `blacklist`, `whitelist`].includes(e2.leaderListMode) && Array.isArray(e2.leaders) && e2.leaders.every((e3) => typeof e3.uid == `string` && /^[1-9]\d*$/.test(e3.uid) && (e3.name === void 0 || typeof e3.name == `string`)) && new Set(e2.leaders.map((e3) => e3.uid)).size === e2.leaders.length && typeof e2.skipSoloLeader == `boolean` && typeof e2.skipKicked == `boolean`;
}

// Target rows below are disclosed synthetic data; Ce/Te define factory/validation.
export const previewAfkTargets = [
  { group: "normal", source: "search", action: "attack", rally: false, monsterType: 0, monsterNameKey: "fixture-steel", monsterIds: [], attackMinLevel: 1, attackMaxLevel: 10, key: "steel", kind: "farm", labelKey: "squad.afkResourceMetal", minLevel: 1, maxLevel: 10, searchable: true },
  { group: "normal", source: "search", action: "attack", rally: false, monsterType: 0, monsterNameKey: "fixture-food", monsterIds: [], attackMinLevel: 1, attackMaxLevel: 10, key: "food", kind: "farm", labelKey: "squad.afkResourceFood", minLevel: 1, maxLevel: 10, searchable: true },
  { group: "elite", source: "map", action: "attack", rally: false, monsterType: 0, monsterNameKey: "fixture-gold", monsterIds: [], key: "gold", kind: "farm", labelKey: "squad.afkResourceGold", minLevel: 1, maxLevel: 10, searchable: true },
  { group: "leader", source: "map", action: "rally", rally: true, monsterType: 8, monsterNameKey: "fixture-boss", monsterIds: [], key: "boss", kind: "join", labelKey: "squad.afkResourceBoss", minLevel: 1, maxLevel: 99, searchable: false },
];

function previewJoinRestrictions() {
  return normalizeJoinRestrictions(undefined, 1, true);
}

export function makePreviewAfkProfile(id, name, kind = "farm", targetKey = kind === "join" ? "boss" : "steel") {
  const target = previewAfkTargets.find((entry) => entry.key === targetKey) || previewAfkTargets.find((entry) => entry.kind === kind);
  return {
    id, name, enabled: true, kind, targetKey: target?.key || "", lastListTargetKey: target?.key || "", customTarget: false, targetNameQuery: "", monsterType: target?.monsterType ?? 0, monsterNameKey: target?.monsterNameKey, monsterIds: [...(target?.monsterIds ?? [])], source: target?.source, action: target?.action, rally: target?.rally === true,
    searchable: target?.searchable === true, minLevel: target?.minLevel ?? 1, maxLevel: target?.maxLevel ?? 99,
    squadIndexes: [1], executionLimit: 0, attackEnabled: kind === "farm", joinEnabled: kind === "join", continuousAttack: kind === "farm" && !target?.rally, continuousJoin: kind === "join",
    levelFilterEnabled: false, progressiveLevels: false, distanceFilterEnabled: false, maxDistance: 200,
    joinRestrictions: kind === "join" ? previewJoinRestrictions() : undefined,
  };
}

export function previewAfkProfileValid(profile) {
  const numeric = [profile.executionLimit, profile.minMembers, profile.minLevel, profile.maxLevel, profile.maxDistance];
  if (numeric.some((value) => typeof value === "number" && !Number.isFinite(value))) return false;
  if (!profile.name.trim() || !profile.targetKey.trim() || !Number.isInteger(profile.executionLimit) || profile.executionLimit < 0 || profile.squadIndexes.length === 0) return false;
  if (profile.targetKey.startsWith("query:") && !profile.targetNameQuery?.trim()) return false;
  if (profile.levelFilterEnabled && (!(profile.minLevel > 0) || (!profile.progressiveLevels && profile.maxLevel < profile.minLevel))) return false;
  if (profile.distanceFilterEnabled && !(profile.maxDistance > 0)) return false;
  if (!Number.isInteger(profile.monsterType) || profile.monsterType < 0) return false;
  if (profile.kind === "join" && !validJoinRestrictions(normalizeJoinRestrictions(profile.joinRestrictions))) return false;
  return true;
}


export function applyAfkTarget(profile, target) {
  return { ...profile, targetKey: target.key, lastListTargetKey: target.key, customTarget: false, targetNameQuery: "", monsterNameKey: target.monsterNameKey, monsterType: target.monsterType, monsterIds: [...target.monsterIds], source: target.source, action: target.action, rally: target.rally, searchable: target.searchable, continuousAttack: target.rally ? false : profile.continuousAttack, minLevel: target.minLevel, maxLevel: target.maxLevel };
}
