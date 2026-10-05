import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import {
  compile,
  fn,
  raw,
  read,
  require,
  squad,
} from "../../LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-c/accepted-harness.mjs";
import * as afk from "../../../../../src/LWBridge.UI-0.3.17/src/previewAfkContracts.js";
import * as fixtures from "../../../../../src/LWBridge.UI-0.3.17/src/previewAfkCloseoutFixtures.js";
import currentEn from "../../../../../src/LWBridge.UI-0.3.17/src/locales/en.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../../");
const outputDir = path.join(here, "full-compositions");
const outputPath = path.join(here, "whole-afk-composition-results.json");
const verifyOnly = process.argv.includes("--verify");
const sha = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
const relative = (file) => path.relative(repo, file).replaceAll("\\", "/");

const React = require("react");
const jsx = require("react/jsx-runtime");
const { renderToStaticMarkup } = require("react-dom/server");
const h = React.createElement;
const Fragment = React.Fragment;

const main = read("evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js");
const card = read("evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/AutomationCard-LCx_jIi7.js");
const currentSquads = read("src/LWBridge.UI-0.3.17/src/SquadsPage.jsx");
const currentJoinSource = read("src/LWBridge.UI-0.3.17/src/RallyJoinSettings.jsx");
const sharedUi = read("src/LWBridge.UI-0.3.17/src/sharedPageUI.jsx");
const sourceCss = read("evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-rIL9Fpht.css");
const currentCss = `${read("src/LWBridge.UI-0.3.17/src/reference.css")}\n${read("src/LWBridge.UI-0.3.17/src/styles.css")}`;
const localeAsset = read("evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/en-BisSXcTB.js");
const parser = require("@babel/parser");
const mainAst = parser.parse(main, { sourceType: "module" });
function variableInitializer(source, ast, name) {
  const stack = [ast];
  while (stack.length) {
    const node = stack.pop();
    if (!node || typeof node !== "object") continue;
    if (node.type === "VariableDeclarator" && node.id?.name === name) return source.slice(node.init.start, node.init.end);
    for (const value of Object.values(node)) {
      if (Array.isArray(value)) stack.push(...value);
      else if (value && typeof value === "object") stack.push(value);
    }
  }
  throw new Error(`variable initializer not found: ${name}`);
}
const sourceBaseCatalog = (() => {
  const errorTables = [
    ["error", new Function(`return (${variableInitializer(main, mainAst, "Mr")});`)()],
    ["auth.error", new Function(`return (${variableInitializer(main, mainAst, "Nr")});`)()],
    ["update.error", new Function(`return (${variableInitializer(main, mainAst, "Pr")});`)()],
  ];
  return Object.fromEntries(errorTables.flatMap(([namespace, table]) => Object.entries(table).map(([key, translations]) => [`${namespace}.${key}`, translations[0]])));
})();
const localeDecl = (() => {
  const ast = parser.parse(localeAsset, { sourceType: "module" });
  const stack = [ast];
  while (stack.length) {
    const node = stack.pop();
    if (!node || typeof node !== "object") continue;
    if (node.type === "VariableDeclarator" && node.id?.name === "t") return node;
    for (const value of Object.values(node)) {
      if (Array.isArray(value)) stack.push(...value);
      else if (value && typeof value === "object") stack.push(value);
    }
  }
  return null;
})();
assert.ok(localeDecl, "source EN locale object missing");
const sourceCatalog = new Function("e", `return (${localeAsset.slice(localeDecl.init.start, localeDecl.init.end)});`)({ en: sourceBaseCatalog });
const interpolate = (catalog) => (key, vars = {}) => String(catalog[key] ?? key ?? "")
  .replace(/\{(\w+)\}/g, (match, name) => vars[name] ?? match);
const sourceT = interpolate(sourceCatalog);
const currentT = interpolate(currentEn);

function fakeHooks(seed = []) {
  const values = [...seed];
  let cursor = 0;
  return {
    values,
    begin() { cursor = 0; },
    useState(initial) {
      const index = cursor++;
      if (!(index in values)) values[index] = typeof initial === "function" ? initial() : initial;
      return [values[index], (next) => { values[index] = typeof next === "function" ? next(values[index]) : next; }];
    },
    useRef(initial) {
      const index = cursor++;
      if (!(index in values) || values[index] == null) values[index] = { current: initial };
      return values[index];
    },
    useEffect() {},
    useMemo(factory) { return factory(); },
    useCallback(callback) { return callback; },
  };
}

function sourceConfig(draft, { confirmed = structuredClone(draft), error = null, saving = false } = {}) {
  let current = draft;
  const snapshot = () => ({ draft: current, confirmed, error, saving });
  const state = {
    subscribe: () => () => {},
    getSnapshot: snapshot,
    edit(next) { current = typeof next === "function" ? next(current) : next; },
    flush: async () => current,
    refresh: async () => current,
    runAction: async (action, update) => {
      const result = typeof action === "function" ? await action() : undefined;
      if (update) current = update(current);
      return result;
    },
  };
  return {
    get draft() { return current; },
    confirmed,
    error,
    saving,
    state,
  };
}

function currentConfig(draft, { confirmed = structuredClone(draft), error = null, saving = false } = {}) {
  let current = draft;
  const store = {
    getSnapshot: () => ({ draft: current, confirmed, error, saving }),
    edit(next) { current = typeof next === "function" ? next(current) : next; },
    flush: async () => current,
    refresh: async () => current,
  };
  return {
    get draft() { return current; },
    confirmed,
    dirty: false,
    saving,
    error,
    store,
  };
}

const SourceSwitch = compile(main, "zn", { M: jsx });
const SourceToggle = compile(main, "Bn", { M: jsx, De: () => ({ t: sourceT }), zn: SourceSwitch });
const SourceIcon = compile(main, "Vr", { M: jsx });
const SourceConfigError = compile(main, "Oe", { M: jsx, b: React, De: () => ({ t: sourceT }) });
const SourceErrorCodes = compile(main, "Ir");
const SourceTranslatedError = compile(main, "Lr", { Ir: SourceErrorCodes });
const SourceCompact = compile(card, "l", { s: jsx, i: SourceIcon });
const SourceNormalizeJoin = compile(squad, "oe");
const SourceValidJoin = compile(squad, "se");
const SourceNormalizeProfileBase = compile(squad, "ce", { oe: SourceNormalizeJoin });
const SourceRallyTarget = compile(squad, "P");
const SourceNormalizeProfile = compile(squad, "F", { ce: SourceNormalizeProfileBase, P: SourceRallyTarget });
const SourceNormalizeMonster = compile(squad, "xe", { F: SourceNormalizeProfile, oe: SourceNormalizeJoin });
const SourceNormalizePotion = compile(squad, "Se");
const SourceTranslateTargets = compile(squad, "le");
const SourceResolveTarget = compile(squad, "ue");
const SourceReorderProfiles = compile(squad, "be");
const SourceNewProfile = compile(squad, "Ce", { oe: SourceNormalizeJoin, crypto });
const SourceLevelOutOfRange = compile(squad, "we");
const SourceDistanceValid = compile(squad, "k");
const SourceValidProfile = compile(squad, "Te", { k: SourceDistanceValid, se: SourceValidJoin, oe: SourceNormalizeJoin });
const SourceTargetKey = compile(squad, "M");
const SourceReorder = compile(squad, "de");
const SourceRefresh = compile(squad, "N");
const SourceBuildingName = compile(squad, "fe");
const SourceModal = compile(main, "In", { M: jsx, b: React });

function sourceJoinComponent(caseData) {
  const memberFixture = fixtures.previewMemberFixture(caseData.previewState);
  const hooks = fakeHooks([
    memberFixture.ready ? { profileId: 1, data: { membersReady: true, members: memberFixture.members, selfUid: memberFixture.selfUid } } : null,
    false,
    "",
    new Set(),
    memberFixture.failed,
  ]);
  hooks.begin();
  return compile(squad, "ye", {
    A: jsx,
    O: hooks,
    d: () => ({ t: sourceT }),
    c: () => 1,
    a: async () => ({ membersReady: false, members: [], selfUid: "" }),
    w: SourceToggle,
    se: SourceValidJoin,
    y: SourceModal,
    window: { setInterval: () => 0, clearInterval() {} },
  });
}

function sourceGarrisonComponent(caseData) {
  const runtime = fixtures.previewGarrisonRuntime(caseData.previewState);
  const members = fixtures.previewGarrisonMemberFixture(caseData.previewState);
  const hooks = fakeHooks([
    { buildings: runtime.buildings, allies: members.members },
    { guardingCount: runtime.guardingCount, assignments: runtime.assignments, lastError: runtime.lastError },
    caseData.availableSquads,
    runtime.gameTexts || {},
    false,
    "",
    new Set(),
    "",
    runtime.discoveryError ? sourceT(runtime.discoveryError) : "",
    "",
    null,
  ]);
  hooks.begin();
  return compile(squad, "pe", {
    A: jsx,
    O: hooks,
    d: () => ({ language: "en", t: sourceT }),
    c: () => 1,
    j: { enabled: false, recallOnDisable: true, squadPriority: [], targets: [] },
    M: SourceTargetKey,
    de: SourceReorder,
    N: SourceRefresh,
    fe: SourceBuildingName,
    e: (_scope, initial) => sourceConfig(initial),
    m: async () => ({}),
    S: async () => ({}),
    te: async () => ({ buildings: runtime.buildings, allies: members.members }),
    f: async () => ({ squads: caseData.availableSquads.map((index) => ({ index })) }),
    u: async () => runtime.gameTexts || {},
    C: async () => ({ tasks: { allianceGarrison: { guardingCount: runtime.guardingCount, assignments: runtime.assignments } } }),
    re: async () => ({}),
    b: async () => ({}),
    _: SourceTranslatedError,
    w: SourceToggle,
    T: SourceIcon,
    ne: SourceConfigError,
    y: SourceModal,
    window: { setInterval: () => 0, clearInterval() {} },
  });
}

function sourceZombieComponent(caseData) {
  const runtime = fixtures.previewZombieBusRuntime(caseData.previewState);
  const hooks = fakeHooks([
    { busAssignments: runtime.assignments, lastError: runtime.lastError },
    runtime.gameTexts || {},
  ]);
  hooks.begin();
  const Table = compile(squad, "me", { A: jsx, d: () => ({ t: sourceT }) });
  return compile(squad, "he", {
    A: jsx,
    O: hooks,
    d: () => ({ language: "en", t: sourceT }),
    c: () => 1,
    e: (_scope, initial) => sourceConfig(initial),
    m: async () => ({}),
    D: async () => ({}),
    C: async () => ({ tasks: { zombieBus: { busAssignments: runtime.assignments, lastError: runtime.lastError } } }),
    u: async () => runtime.gameTexts || {},
    ie: SourceCompact,
    me: Table,
    ne: SourceConfigError,
    window: { setInterval: () => 0, clearInterval() {} },
  });
}

const CurrentSwitch = compile(sharedUi, "Switch", { h, Fragment });
const CurrentToggle = compile(sharedUi, "ToggleRow", { h, Fragment, useI18n: () => ({ t: currentT }), Switch: CurrentSwitch });
const CurrentPreviewError = compile(read("src/LWBridge.UI-0.3.17/src/previewConfigHook.jsx"), "PreviewConfigError", { h, Fragment });
const CurrentSettingsGlyph = compile(currentSquads, "SettingsGlyph", { h, Fragment });
const CurrentCompact = compile(currentSquads, "CompactAfkCard", {
  h, Fragment, useI18n: () => ({ t: currentT }), SettingsGlyph: CurrentSettingsGlyph,
});
const CurrentJoinModal = compile(currentJoinSource, "JoinModal", { A: jsx, useEffect: React.useEffect, useRef: React.useRef });
const CurrentJoin = compile(currentJoinSource, "RallyJoinSettings", {
  A: jsx,
  useEffect: React.useEffect,
  useMemo: React.useMemo,
  useState: React.useState,
  useI18n: () => ({ t: currentT }),
  se: afk.validJoinRestrictions,
  previewMemberFixture: fixtures.previewMemberFixture,
  ToggleRow: CurrentToggle,
  JoinModal: CurrentJoinModal,
  window: { addEventListener() {}, removeEventListener() {} },
});
const CurrentEditor = compile(currentSquads, "AfkProfileEditor", {
  h, Fragment,
  useState: React.useState,
  useEffect: React.useEffect,
  useI18n: () => ({ t: currentT }),
  RallyJoinSettings: CurrentJoin,
  ToggleRow: CurrentToggle,
  ...afk,
});
const CurrentDrill = compile(currentSquads, "AllianceDrillPreviewSettings", {
  h, Fragment,
  useState: React.useState,
  useI18n: () => ({ t: currentT }),
  RallyJoinSettings: CurrentJoin,
  ToggleRow: CurrentToggle,
  previewDrillRuntime: fixtures.previewDrillRuntime,
  normalizeJoinRestrictions: afk.normalizeJoinRestrictions,
});
const CurrentGarrisonDialog = compile(currentSquads, "GarrisonPickerDialog", {
  h, Fragment, useRef: React.useRef, useEffect: React.useEffect,
});
const CurrentGarrison = compile(currentSquads, "GarrisonPreviewSettings", {
  h, Fragment,
  useState: React.useState,
  useEffect: React.useEffect,
  useI18n: () => ({ t: currentT }),
  previewGarrisonRuntime: fixtures.previewGarrisonRuntime,
  previewGarrisonMemberFixture: fixtures.previewGarrisonMemberFixture,
  refreshGarrisonTargets: afk.refreshGarrisonTargets,
  ToggleRow: CurrentToggle,
  PreviewConfigError: CurrentPreviewError,
  GarrisonPickerDialog: CurrentGarrisonDialog,
  window: { addEventListener() {}, removeEventListener() {} },
});
const CurrentZombie = compile(currentSquads, "ZombieBusPreviewSettings", {
  h, Fragment,
  useI18n: () => ({ t: currentT }),
  previewZombieBusRuntime: fixtures.previewZombieBusRuntime,
  PreviewConfigError: CurrentPreviewError,
});
const CurrentTranslatedAfkError = compile(currentSquads, "translatedAfkError");
const CurrentValidMonster = compile(currentSquads, "validMonsterAfkConfig", {
  previewAfkProfileValid: afk.previewAfkProfileValid,
  validJoinRestrictions: afk.validJoinRestrictions,
});
const CurrentValidPotion = compile(currentSquads, "validPotionConfig");
const CurrentValidGarrison = compile(currentSquads, "validGarrisonConfig");

function profile(id = "steel", { enabled = true, squadIndexes = [1], executionLimit = 3 } = {}) {
  return {
    ...afk.makePreviewAfkProfile(id, id === "gold" ? "Fixture Gold Hunt" : "Fixture Steel Hunt", "farm", id === "gold" ? "gold" : "steel"),
    enabled,
    squadIndexes,
    executionLimit,
  };
}

function joinProfile(id = "join-boss", { enabled = true, squadIndexes = [1], executionLimit = 0 } = {}) {
  return {
    ...afk.makePreviewAfkProfile(id, "Fixture Rally Join", "join", "boss"),
    enabled,
    squadIndexes,
    executionLimit,
    joinRestrictions: afk.normalizeJoinRestrictions({ enabled: true, leaderListMode: "whitelist", leaders: [] }, 1, true),
  };
}

function caseDefinition(id, previewState, overrides = {}) {
  const toolbar = fixtures.initialAfkToolbarConfig(previewState);
  const availableSquads = overrides.availableSquads ?? fixtures.previewAfkAvailableSquads(previewState);
  const profiles = overrides.profiles ?? [profile("steel", { squadIndexes: availableSquads.length ? [availableSquads[0]] : [] })];
  const runtimeRows = overrides.runtimeRows ?? [
    ...fixtures.previewAfkProfileRuntime(previewState),
    ...fixtures.previewDrillRuntime(previewState),
  ];
  return {
    id,
    previewState,
    online: overrides.online ?? true,
    availableSquads,
    profiles,
    confirmedProfiles: overrides.confirmedProfiles ?? profiles,
    runtimeRows,
    runtimeNames: overrides.runtimeNames ?? fixtures.previewAfkRuntimeNames(previewState),
    toolbarPanel: overrides.toolbarPanel ?? (previewState.includes("potion") ? "potion" : previewState.includes("drill") ? "drill" : previewState.includes("garrison") ? "garrison" : previewState.includes("zombie") ? "zombieBus" : null),
    compositionError: overrides.compositionError ?? (previewState === "squads-profile-target-failed" ? "common.actionFailed" : ""),
    selectedId: overrides.selectedId ?? profiles[0]?.id ?? "",
    profileAction: overrides.profileAction ?? "",
    addMenuOpen: overrides.addMenuOpen ?? false,
    afkError: overrides.afkError ?? null,
    potionError: overrides.potionError ?? null,
    toolbar: overrides.toolbar ?? toolbar,
  };
}

const cases = [
  caseDefinition("selected-existing", "squads-profile-positive"),
  caseDefinition("selected-new", "squads-profile-positive", { profiles: [profile("source-new", { squadIndexes: [1] })], confirmedProfiles: [] }),
  caseDefinition("profile-disabled", "squads-profile-positive", { profiles: [profile("steel", { enabled: false, squadIndexes: [1] })] }),
  caseDefinition("discovered-squads-34", "squads-profile-squads-34", { profiles: [profile("steel", { squadIndexes: [3] })] }),
  caseDefinition("discovered-squads-empty", "squads-profile-squads-empty", { profiles: [profile("steel", { squadIndexes: [] })] }),
  caseDefinition("runtime-translated", "squads-profile-runtime-translated", {
    profiles: [profile("steel", { squadIndexes: [1], executionLimit: 3 }), profile("gold", { squadIndexes: [2], executionLimit: 3 })],
  }),
  caseDefinition("runtime-error", "squads-profile-runtime-error", {
    profiles: [profile("steel", { squadIndexes: [1], executionLimit: 3 }), profile("gold", { squadIndexes: [2], executionLimit: 3 })],
  }),
  caseDefinition("master-stop-busy", "squads-profile-master-stop-busy", { profileAction: "stop" }),
  caseDefinition("join-members-loading", "squads-profile-members-loading", { profiles: [joinProfile()] }),
  caseDefinition("join-members-failed", "squads-profile-members-failed", { profiles: [joinProfile()] }),
  caseDefinition("join-members-positive", "squads-profile-members-positive", { profiles: [joinProfile()] }),
  caseDefinition("potion-open", "squads-profile-potion-open"),
  caseDefinition("potion-invalid", "squads-profile-potion-invalid"),
  caseDefinition("drill-waiting", "squads-profile-drill-waiting-translated"),
  caseDefinition("drill-empty", "squads-profile-drill-empty"),
  caseDefinition("drill-empty-error", "squads-profile-drill-empty", { compositionError: "squad.afkAllianceDrillSquadRequired" }),
  caseDefinition("profile-save-error", "squads-profile-positive", { afkError: new Error("SAVE") }),
  caseDefinition("potion-save-error", "squads-profile-potion-open", { potionError: new Error("SAVE") }),
  caseDefinition("target-discovery-loading", "squads-profile-target-loading", { availableSquads: [] }),
  caseDefinition("target-discovery-failed", "squads-profile-target-failed", { availableSquads: [], compositionError: "common.actionFailed" }),
  caseDefinition("empty-profiles", "squads-profile-positive", { profiles: [], selectedId: "" }),
  caseDefinition("garrison-pending", "squads-profile-garrison-discovery-pending"),
  caseDefinition("garrison-failed", "squads-profile-garrison-discovery-failed"),
  caseDefinition("garrison-localized", "squads-profile-garrison-localized"),
  caseDefinition("garrison-no-targets", "squads-profile-garrison-no-targets"),
  caseDefinition("garrison-no-squads", "squads-profile-garrison-no-squads"),
  caseDefinition("garrison-running", "squads-profile-garrison-running"),
  caseDefinition("zombie-waiting", "squads-profile-zombie-waiting"),
  caseDefinition("zombie-error", "squads-profile-zombie-error"),
  caseDefinition("zombie-running", "squads-profile-zombie-running"),
];

const validationResults = cases.map((caseData) => {
  const monster = {
    enabled: caseData.toolbar.masterEnabled,
    strategies: caseData.profiles,
    allianceDrill: caseData.toolbar.allianceDrill,
  };
  const potion = {
    enabled: caseData.toolbar.potionEnabled,
    minStamina: caseData.toolbar.minStamina,
    preferFifty: caseData.toolbar.preferFifty,
  };
  const garrison = caseData.toolbar.garrison;
  const sourceMonsterValid = monster.strategies.every(SourceValidProfile)
    && SourceValidJoin(SourceNormalizeJoin(monster.allianceDrill.joinRestrictions));
  const sourcePotionValid = Number.isInteger(potion.minStamina) && potion.minStamina >= 0 && potion.minStamina <= 9999;
  const sourceGarrisonValid = !garrison.enabled || (garrison.targets.length > 0 && garrison.squadPriority.length > 0);
  const currentMonsterValid = CurrentValidMonster(monster);
  const currentPotionValid = CurrentValidPotion(potion);
  const currentGarrisonValid = CurrentValidGarrison(garrison);
  assert.equal(currentMonsterValid, sourceMonsterValid, `${caseData.id}: Monster validator differs from recovered store contract`);
  assert.equal(currentPotionValid, sourcePotionValid, `${caseData.id}: Potion validator differs from recovered store contract`);
  assert.equal(currentGarrisonValid, sourceGarrisonValid, `${caseData.id}: Garrison validator differs from recovered store contract`);
  assert.deepEqual(
    afk.normalizeJoinRestrictions(monster.allianceDrill.joinRestrictions),
    SourceNormalizeJoin(monster.allianceDrill.joinRestrictions),
    `${caseData.id}: join normalization differs`,
  );
  for (const entry of caseData.profiles) {
    assert.equal(afk.previewAfkProfileValid(entry), SourceValidProfile(entry), `${caseData.id}/${entry.id}: profile validation differs`);
    assert.equal(
      afk.previewAfkLevelOutOfRange(entry, afk.resolvePreviewAfkTarget(afk.previewAfkTargets, entry)),
      SourceLevelOutOfRange(entry, SourceResolveTarget(afk.previewAfkTargets, entry)),
      `${caseData.id}/${entry.id}: target range validation differs`,
    );
  }
  return {
    id: caseData.id,
    monster: sourceMonsterValid,
    potion: sourcePotionValid,
    garrison: sourceGarrisonValid,
    profiles: caseData.profiles.map((entry) => ({ id: entry.id, valid: SourceValidProfile(entry) })),
  };
});

assert.equal(validationResults.find((entry) => entry.id === "potion-invalid")?.potion, false, "invalid Potion input must be rejected by both validators");
const validationNegatives = [
  {
    id: "garrison-enabled-no-targets",
    source: false,
    current: CurrentValidGarrison({ enabled: true, recallOnDisable: true, targets: [], squadPriority: [1] }),
  },
  {
    id: "garrison-enabled-no-squads",
    source: false,
    current: CurrentValidGarrison({ enabled: true, recallOnDisable: true, targets: [{ kind: "allianceBuilding", buildId: 1 }], squadPriority: [] }),
  },
  {
    id: "monster-invalid-join",
    source: false,
    current: CurrentValidMonster({
      enabled: false,
      strategies: [profile("steel")],
      allianceDrill: { ...cases[0].toolbar.allianceDrill, joinRestrictions: { ...cases[0].toolbar.allianceDrill.joinRestrictions, delaySeconds: [-1, 3] } },
    }),
  },
];
for (const entry of validationNegatives) assert.equal(entry.current, entry.source, `${entry.id}: current validator must match recovered rejection`);
const drillEmptyStore = {
  enabled: false,
  strategies: [profile("steel")],
  allianceDrill: { ...cases[0].toolbar.allianceDrill, enabled: true, squadIndexes: [] },
};
const drillEmptySourceValid = drillEmptyStore.strategies.every(SourceValidProfile)
  && SourceValidJoin(SourceNormalizeJoin(drillEmptyStore.allianceDrill.joinRestrictions));
assert.equal(drillEmptySourceValid, true, "recovered store validator permits Drill-empty draft; toggle interaction owns the user-facing rejection");
assert.equal(CurrentValidMonster(drillEmptyStore), drillEmptySourceValid, "current Monster store validator must preserve recovered Drill-empty ownership");
assert.deepEqual(SourceReorderProfiles([1, 2, 3], 1, 3), afk.reorderPreviewAfkSquads?.([1, 2, 3], 1, 3) ?? [2, 3, 1], "recovered reorder helper drifted");

function sourceRender(caseData) {
  const drill = caseData.toolbar.allianceDrill;
  const monsterDraft = { enabled: caseData.toolbar.masterEnabled, strategies: caseData.profiles, allianceDrill: drill };
  const potionDraft = { enabled: caseData.toolbar.potionEnabled, minStamina: caseData.toolbar.minStamina, preferFifty: caseData.toolbar.preferFifty };
  const monsterConfirmed = { ...monsterDraft, strategies: caseData.confirmedProfiles };
  const monster = sourceConfig(monsterDraft, { confirmed: monsterConfirmed, error: caseData.afkError });
  const potion = sourceConfig(potionDraft, { error: caseData.potionError });
  const hooks = fakeHooks([
    caseData.toolbarPanel,
    caseData.previewState === "squads-profile-target-loading" ? [] : afk.previewAfkTargets.filter((entry) => entry.group !== "drill"),
    caseData.previewState !== "squads-profile-target-failed" && caseData.previewState !== "squads-profile-target-loading",
    { current: false },
    caseData.addMenuOpen,
    caseData.selectedId || null,
    { current: caseData.selectedId || null },
    caseData.runtimeRows,
    caseData.runtimeNames,
    caseData.availableSquads,
    caseData.profileAction,
    caseData.compositionError ? sourceT(caseData.compositionError) : "",
    { current: null },
    { current: null },
    { current: "" },
    null,
    null,
    "",
  ]);
  let configCalls = 0;
  const SourceGarrison = sourceGarrisonComponent(caseData);
  const SourceZombie = sourceZombieComponent(caseData);
  const SourceJoin = sourceJoinComponent(caseData);
  const Original = compile(squad, "I", {
    A: jsx,
    O: hooks,
    d: () => ({ language: "en", t: sourceT }),
    c: () => 1,
    e: () => (configCalls++ === 0 ? monster : potion),
    xe: SourceNormalizeMonster,
    Se: SourceNormalizePotion,
    Te: SourceValidProfile,
    se: SourceValidJoin,
    oe: SourceNormalizeJoin,
    m: async () => ({}),
    v: async (value) => value,
    D: async () => {},
    p: async () => ({ options: [] }),
    f: async () => ({ squads: [] }),
    u: async () => ({}),
    le: SourceTranslateTargets,
    C: async () => ({ tasks: {} }),
    ue: SourceResolveTarget,
    _e: ["normal", "elite", "running", "leader", "ally", "drill", "invader", "other"],
    be: SourceReorderProfiles,
    Ce: SourceNewProfile,
    we: SourceLevelOutOfRange,
    ne: SourceConfigError,
    ie: SourceCompact,
    pe: SourceGarrison,
    he: SourceZombie,
    w: SourceToggle,
    T: SourceIcon,
    ye: SourceJoin,
    _: SourceTranslatedError,
    ge: {
      waiting_join_delay: "squad.join.waiting",
      waiting_nearby_target: "squad.afkStep.waitingNearbyTarget",
      scanning_nearby: "squad.afkStep.scanningNearby",
      scanning_next_target: "squad.afkStep.scanningNextTarget",
      scanning_mine_route: "squad.afkStep.scanningMineRoute",
      marching_via_mine: "squad.afkStep.marchingViaMine",
      redirecting_to_monster: "squad.afkStep.redirectingToMonster",
      recovering_from_mine: "squad.afkStep.recoveringFromMine",
      scanning_return_mine: "squad.afkStep.scanningReturnMine",
      boosting_return: "squad.afkStep.boostingReturn",
      returning: "squad.afkStep.returning",
    },
    window: { setInterval: () => 0, clearInterval() {}, setTimeout: (callback) => callback(), confirm: () => true },
    document: { addEventListener() {}, removeEventListener() {} },
  });
  hooks.begin();
  const tree = Original({
    online: caseData.online,
    serverId: 321,
    config: monsterDraft,
    staminaPotionConfig: potionDraft,
    garrisonConfig: caseData.toolbar.garrison,
    zombieBusConfig: caseData.toolbar.zombieBus,
    onLog() {},
  });
  return renderToStaticMarkup(tree);
}

function currentRender(caseData) {
  const afkDraft = { enabled: caseData.toolbar.masterEnabled, strategies: caseData.profiles, allianceDrill: caseData.toolbar.allianceDrill };
  const potionDraft = { enabled: caseData.toolbar.potionEnabled, minStamina: caseData.toolbar.minStamina, preferFifty: caseData.toolbar.preferFifty };
  const configs = [
    currentConfig(afkDraft, { confirmed: { ...afkDraft, strategies: caseData.confirmedProfiles }, error: caseData.afkError }),
    currentConfig(potionDraft, { error: caseData.potionError }),
    currentConfig(caseData.toolbar.garrison),
    currentConfig(caseData.toolbar.zombieBus),
  ];
  let configCalls = 0;
  const hooks = fakeHooks([
    true,
    caseData.selectedId,
    caseData.profileAction,
    "",
    "",
    caseData.addMenuOpen,
    caseData.toolbarPanel,
    caseData.compositionError,
    "",
    { current: 1 },
    { current: null },
    { current: null },
  ]);
  const Current = compile(currentSquads, "AfkContent", {
    h,
    Fragment,
    ...hooks,
    useI18n: () => ({ t: currentT }),
    usePreviewConfig: () => configs[configCalls++],
    initialAfkProfiles: () => caseData.profiles,
    initialMonsterAfkConfig: () => afkDraft,
    validMonsterAfkConfig: () => true,
    initialPotionConfig: () => potionDraft,
    validPotionConfig: () => true,
    initialGarrisonConfig: () => caseData.toolbar.garrison,
    validGarrisonConfig: () => true,
    initialZombieConfig: () => caseData.toolbar.zombieBus,
    translatedAfkError: CurrentTranslatedAfkError,
    AFK_RUNTIME_STEP_KEYS: {
      waiting_join_delay: "squad.join.waiting",
      waiting_nearby_target: "squad.afkStep.waitingNearbyTarget",
      scanning_nearby: "squad.afkStep.scanningNearby",
      scanning_next_target: "squad.afkStep.scanningNextTarget",
      scanning_mine_route: "squad.afkStep.scanningMineRoute",
      marching_via_mine: "squad.afkStep.marchingViaMine",
      redirecting_to_monster: "squad.afkStep.redirectingToMonster",
      recovering_from_mine: "squad.afkStep.recoveringFromMine",
      scanning_return_mine: "squad.afkStep.scanningReturnMine",
      boosting_return: "squad.afkStep.boostingReturn",
      returning: "squad.afkStep.returning",
    },
    ...afk,
    ...fixtures,
    previewAfkProfileRuntime: () => caseData.runtimeRows.filter((row) => row.activity !== "allianceDrill"),
    previewDrillRuntime: () => caseData.runtimeRows.filter((row) => row.activity === "allianceDrill"),
    previewAfkRuntimeNames: () => caseData.runtimeNames,
    previewGarrisonRuntime: fixtures.previewGarrisonRuntime,
    previewGarrisonMemberFixture: fixtures.previewGarrisonMemberFixture,
    previewZombieBusRuntime: fixtures.previewZombieBusRuntime,
    PreviewConfigError: CurrentPreviewError,
    CompactAfkCard: CurrentCompact,
    AfkProfileEditor: CurrentEditor,
    AllianceDrillPreviewSettings: CurrentDrill,
    GarrisonPreviewSettings: CurrentGarrison,
    ZombieBusPreviewSettings: CurrentZombie,
    window: { confirm: () => true, setTimeout: (callback) => callback() },
    document: { addEventListener() {}, removeEventListener() {} },
  });
  hooks.begin();
  const tree = Current({ previewEnabled: true, previewState: caseData.previewState, availableSquads: caseData.availableSquads });
  return renderToStaticMarkup(tree);
}

const count = (html, needle) => html.split(needle).length - 1;
const textIncludes = (html, value) => html.includes(value);
function addButtonDisabled(html, label) {
  const end = html.indexOf(`>${label}</button>`);
  assert.ok(end >= 0, `Add button text missing: ${label}`);
  const start = html.lastIndexOf("<button", end);
  assert.ok(start >= 0, `Add button tag missing: ${label}`);
  const close = html.indexOf(">", start);
  const tag = html.slice(start, close + 1);
  assert.ok(tag.includes('aria-haspopup="menu"'), `Add button marker missing: ${tag}`);
  return tag.includes('disabled=""');
}
const caseResults = [];

for (const caseData of cases) {
  const original = sourceRender(caseData);
  const current = currentRender(caseData);
  const originalStats = {
    profileCards: count(original, "monster-afk-profile-card "),
    editors: count(original, "monster-afk-editor"),
    assignmentButtons: count(original, "monster-afk-squad-toggle"),
    toolbarSettings: count(original, "monster-afk-toolbar-settings"),
    compositionErrors: count(original, "monster-afk-error"),
    configErrors: count(original, "class=\"automation-error\" role=\"alert\""),
    runtimeStatus: count(original, "status-ok"),
    garrisonSettings: count(original, "garrison-settings"),
    zombieTables: count(original, "zombie-bus-assignments"),
  };
  const currentStats = {
    profileCards: count(current, "monster-afk-profile-card "),
    editors: count(current, "monster-afk-editor"),
    assignmentButtons: count(current, "monster-afk-squad-toggle"),
    toolbarSettings: count(current, "monster-afk-toolbar-settings"),
    compositionErrors: count(current, "monster-afk-error"),
    configErrors: count(current, "class=\"automation-error\" role=\"alert\""),
    runtimeStatus: count(current, "status-ok"),
    garrisonSettings: count(current, "garrison-settings"),
    zombieTables: count(current, "zombie-bus-assignments"),
  };
  for (const key of ["profileCards", "editors", "assignmentButtons", "compositionErrors", "runtimeStatus", "zombieTables"]) {
    assert.equal(currentStats[key], originalStats[key], `${caseData.id}: ${key} differs`);
  }
  if (!caseData.previewState.startsWith("squads-profile-garrison-")) {
    assert.equal(currentStats.toolbarSettings, originalStats.toolbarSettings, `${caseData.id}: toolbar settings ancestry/count differs`);
  }
  if (caseData.afkError || caseData.potionError) {
    assert.equal(currentStats.configErrors, originalStats.configErrors, `${caseData.id}: config error count differs`);
  }
  if (originalStats.editors) {
    assert.ok(original.includes("</section><div class=\"monster-afk-editor"), `${caseData.id}: source editor is not profiles sibling`);
    assert.ok(current.includes("</section><div class=\"monster-afk-editor"), `${caseData.id}: current editor is not profiles sibling`);
  }
  if (caseData.id === "discovered-squads-34") {
    assert.equal(originalStats.assignmentButtons, 2);
    assert.equal(currentStats.assignmentButtons, 2);
    assert.ok(textIncludes(original, ">3</button>") && textIncludes(original, ">4</button>"));
    assert.ok(textIncludes(current, ">3</button>") && textIncludes(current, ">4</button>"));
  }
  if (caseData.id === "discovered-squads-empty") {
    assert.equal(originalStats.assignmentButtons, 0);
    assert.equal(currentStats.assignmentButtons, 0);
  }
  if (caseData.id === "runtime-translated") {
    assert.ok(original.includes("Fixture Translated Leader"), "source translated runtime target missing");
    assert.ok(current.includes("Fixture Translated Leader"), "current translated runtime target missing");
  }
  if (caseData.id === "selected-new") {
    assert.ok(original.includes("monster-afk-editor is-new"), "source new-profile editor marker missing");
    assert.ok(current.includes("monster-afk-editor is-new"), "current new-profile editor marker missing");
  }
  if (caseData.id === "runtime-error") {
    assert.ok(original.includes(sourceT("error.GAME_CONNECTION_UNAVAILABLE")), "source recovered runtime error translation missing");
    assert.ok(current.includes(currentT("error.GAME_CONNECTION_UNAVAILABLE")), "current recovered runtime error translation missing");
  }
  if (caseData.id === "master-stop-busy") {
    assert.ok(original.includes("disabled=\"\""), "source busy branch did not disable controls");
    assert.ok(current.includes("disabled=\"\""), "current busy branch did not disable controls");
  }
  if (caseData.id === "join-members-loading") {
    assert.ok(original.includes(sourceT("squad.join.membersLoading")), "source member-loading copy missing");
    assert.ok(current.includes(currentT("squad.join.membersLoading")), "current member-loading copy missing");
  }
  if (caseData.id === "join-members-failed") {
    assert.ok(original.includes(sourceT("squad.join.membersFailed")), "source member-failure copy missing");
    assert.ok(current.includes(currentT("squad.join.membersFailed")), "current member-failure copy missing");
  }
  if (caseData.id === "join-members-positive") {
    assert.ok(original.includes(sourceT("squad.join.chooseMembers")), "source member picker affordance missing");
    assert.ok(current.includes(currentT("squad.join.chooseMembers")), "current member picker affordance missing");
  }
  if (caseData.id === "potion-invalid") {
    assert.ok(original.includes("value=\"10000\""), "source invalid Potion value missing");
    assert.ok(current.includes("value=\"10000\""), "current invalid Potion value missing");
  }
  if (caseData.id === "garrison-no-targets") {
    assert.ok(original.includes(sourceT("garrison.targetRequired")), "source Garrison target validation copy missing");
    assert.ok(current.includes(currentT("garrison.targetRequired")), "current Garrison target validation copy missing");
  }
  if (caseData.id === "garrison-running") {
    assert.ok(original.includes("garrison-assignment"), "source Garrison running assignment missing");
    assert.ok(current.includes("garrison-assignment"), "current Garrison running assignment missing");
  }
  if (caseData.id === "zombie-running") {
    assert.ok(originalStats.zombieTables > 0, "source Zombie running table missing");
    assert.ok(currentStats.zombieTables > 0, "current Zombie running table missing");
  }
  if (caseData.id === "target-discovery-loading") {
    assert.ok(!original.includes(sourceT("status.checking")), "source target-loading must not add inline checking copy");
    assert.ok(!current.includes(currentT("status.checking")), "current target-loading must not add inline checking copy");
    assert.equal(addButtonDisabled(original, sourceT("common.add")), true, "source first-load target discovery must disable Add while target list is empty");
    assert.equal(addButtonDisabled(current, currentT("common.add")), true, "current first-load target discovery must disable Add while target list is empty");
  }
  if (caseData.id === "target-discovery-failed") {
    assert.equal(count(original, sourceT("common.actionFailed")), 1, "source target failure must surface once at composition level");
    assert.equal(count(current, currentT("common.actionFailed")), 1, "current target failure must surface once at composition level");
    assert.equal(addButtonDisabled(original, sourceT("common.add")), false, "source failed refresh must retain targets and keep Add available");
    assert.equal(addButtonDisabled(current, currentT("common.add")), false, "current failed refresh must retain targets and keep Add available");
  }
  if (caseData.id === "profile-save-error") {
    assert.ok(original.includes(`<strong>${sourceT("squad.afkMaster")} `), "source Master save-error label missing");
    assert.ok(current.includes(`<strong>${currentT("squad.afkMaster")} `), "current Master save-error label missing");
  }
  if (caseData.id === "potion-save-error") {
    assert.ok(original.includes(`<strong>${sourceT("automation.autoUsePotion")} `), "source Potion save-error label missing");
    assert.ok(current.includes(`<strong>${currentT("automation.autoUsePotion")} `), "current Potion save-error label missing");
  }
  caseResults.push({
    id: caseData.id,
    previewState: caseData.previewState,
    inputs: {
      availableSquads: caseData.availableSquads,
      profileIds: caseData.profiles.map((entry) => entry.id),
      confirmedProfileIds: caseData.confirmedProfiles.map((entry) => entry.id),
      profileSquads: caseData.profiles.map((entry) => entry.squadIndexes),
      toolbarPanel: caseData.toolbarPanel,
      compositionError: caseData.compositionError,
      runtimeRows: caseData.runtimeRows.length,
    },
    originalStats,
    currentStats,
    originalHtmlSha256: sha(original),
    currentHtmlSha256: sha(current),
  });
  if (!verifyOnly) {
    fs.mkdirSync(outputDir, { recursive: true });
    for (const [side, markup, css] of [["original", original, sourceCss], ["current", current, currentCss]]) {
      const html = `<!doctype html><html lang="en" data-theme="light"><head><meta charset="utf-8"><style>${css}</style></head><body><section class="panel squad-panel">${markup}</section></body></html>`;
      fs.writeFileSync(path.join(outputDir, `${caseData.id}-${side}.html`), html);
    }
  }
}

const originalINode = fn(squad, "I");
const originalI = raw(squad, originalINode);
assert.equal(Buffer.byteLength(squad.slice(0, originalINode.start)), 28070, "recovered I byte offset drifted");
assert.equal(Buffer.byteLength(originalI), 22554, "recovered I byte length drifted");
assert.equal(sha(originalI), "0A4895C21AD443AA7A1C05B54CFF2E8DD89F9038BED10DBFFFB9001A4128EE2E", "recovered I hash drifted");

const dependencies = [
  "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/SquadPanel-HC3-DJei.js",
  "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js",
  "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/AutomationCard-LCx_jIi7.js",
  "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-rIL9Fpht.css",
  "src/LWBridge.UI-0.3.17/src/SquadsPage.jsx",
  "src/LWBridge.UI-0.3.17/src/RallyJoinSettings.jsx",
  "src/LWBridge.UI-0.3.17/src/previewAfkContracts.js",
  "src/LWBridge.UI-0.3.17/src/previewAfkCloseoutFixtures.js",
  "src/LWBridge.UI-0.3.17/src/previewConfigHook.jsx",
  "src/LWBridge.UI-0.3.17/src/reference.css",
  "src/LWBridge.UI-0.3.17/src/styles.css",
].map((name) => {
  const file = path.join(repo, name);
  return { path: relative(file), sha256: sha(fs.readFileSync(file)) };
});

const result = {
  marker: "LWB317_FINAL_CLOSEOUT_WHOLE_AFK_COMPOSITION_OK",
  originalI: {
    path: "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/SquadPanel-HC3-DJei.js",
    utf8ByteOffset: 28070,
    utf8ByteLength: 22554,
    sha256: sha(originalI),
  },
  scope: "Exact recovered whole function I is executed with exact recovered compact/config-error/Garrison/Zombie/Join children. Live canonical AfkContent is executed with live editor/Drill/Garrison/Zombie children. Inputs are matched from the current task-owned source-valid fixture adapters. Native actions remain inert/fenced.",
  cases: caseResults,
  validation: validationResults,
  validationNegatives: [...validationNegatives, { id: "drill-enabled-empty-store", source: drillEmptySourceValid, current: CurrentValidMonster(drillEmptyStore) }],
  dependencies,
  scriptSha256: sha(fs.readFileSync(fileURLToPath(import.meta.url))),
};

if (verifyOnly) {
  const recorded = JSON.parse(fs.readFileSync(outputPath, "utf8"));
  assert.deepEqual(result, recorded, "recorded whole-AFK composition proof is stale");
} else {
  fs.mkdirSync(here, { recursive: true });
  fs.writeFileSync(outputPath, `${JSON.stringify(result, null, 2)}\n`);
}
console.log(JSON.stringify({ marker: result.marker, cases: caseResults.length, verified: verifyOnly }));
