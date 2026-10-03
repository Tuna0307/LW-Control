import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { Fragment, compile, flatten, fn, h, hooks, jsx, nodes, raw, read, squad, text, transformSync } from "../LWB317-UI-AUTOMATION-AFK-CLOSEOUT-001/harness.mjs";
import * as equipment from "../../../../src/LWBridge.UI-0.3.17/src/previewEquipmentContracts.js";
import en from "../../../../src/LWBridge.UI-0.3.17/src/locales/en.js";
import ja from "../../../../src/LWBridge.UI-0.3.17/src/locales/ja.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const pages = read("src/LWBridge.UI-0.3.17/src/Pages.jsx");
const currentContracts = read("src/LWBridge.UI-0.3.17/src/previewEquipmentContracts.js");
const indexSource = read("evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js");
const hash = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
const tFor = (catalog) => (key, vars = {}) => String(catalog[key] || key || "").replace(/\{(\w+)\}/g, (match, name) => vars[name] ?? match);
const results = [];
const locators = {};
const record = (name, count) => results.push({ name, count, result: "PASS" });

function locator(asset, source, node) {
  return {
    asset,
    utf8ByteOffset: Buffer.byteLength(source.slice(0, node.start)),
    utf8ByteLength: Buffer.byteLength(raw(source, node)),
    sha256: hash(raw(source, node)),
  };
}

for (const name of ["ad", "od", "sd", "cd", "ld", "ud", "dd", "fd", "pd"]) {
  const node = fn(squad, name);
  assert.ok(node, `Recovered function ${name} missing`);
  locators[`Squad.${name}`] = locator("SquadPanel-HC3-DJei.js", squad, node);
}
const dialogNode = fn(indexSource, "In");
assert.ok(dialogNode, "Recovered shared dialog In missing");
locators["Index.In"] = locator("index-BVfnK1wp.js", indexSource, dialogNode);

function nestedFunction(parentSource, marker) {
  const node = nodes(parentSource).find((entry) => entry.type === "FunctionDeclaration" && entry.start > 0 && raw(parentSource, entry).includes(marker));
  assert.ok(node, `Nested recovered function missing marker ${marker}`);
  return node;
}

const fdNode = fn(squad, "fd");
const fdSource = raw(squad, fdNode);
const squadSwapNode = nestedFunction(fdSource, "w?.kind!==`squad`");
const targetSwapNode = nestedFunction(fdSource, "squad.sameSlotRequired");
const renameNode = nestedFunction(fdSource, "le.trim()");
for (const [name, node] of [["Squad.fd.squadSwap", squadSwapNode], ["Squad.fd.targetSwap", targetSwapNode], ["Squad.fd.rename", renameNode]]) {
  locators[name] = {
    asset: "SquadPanel-HC3-DJei.js",
    utf8ByteOffset: Buffer.byteLength(squad.slice(0, fdNode.start)) + Buffer.byteLength(fdSource.slice(0, node.start)),
    utf8ByteLength: Buffer.byteLength(raw(fdSource, node)),
    sha256: hash(raw(fdSource, node)),
  };
}

function compileRawFunction(source, node, env = {}) {
  const sourceCode = raw(source, node);
  const code = transformSync(sourceCode, { loader: "js" }).code;
  return new Function(...Object.keys(env), `${code}\nreturn ${node.id.name};`)(...Object.values(env));
}

const originalSnapshot = compile(squad, "od");
const originalItemCount = compile(squad, "ld");
const originalPositionCount = compile(squad, "ud");
const originalMatch = compile(squad, "dd", { nd: [1, 2, 3, 4] });
const originalFindSquad = compile(squad, "sd");
const originalEnsurePosition = compile(squad, "cd", { sd: originalFindSquad });

let helperCases = 0;
const fixture = equipment.previewEquipmentFixture("squads-equipment", tFor(en));
assert.deepEqual(equipment.equipmentSnapshotFromSquads(fixture.squads), originalSnapshot(fixture.squads)); helperCases++;
for (const preset of fixture.presets) {
  assert.equal(equipment.equipmentItemCount(preset), originalItemCount(preset));
  assert.equal(equipment.equipmentPositionCount(preset), originalPositionCount(preset));
  helperCases += 2;
  for (const liveSquad of fixture.squads) {
    const presetSquad = equipment.findEquipmentSquad(preset.squads, liveSquad.index);
    assert.equal(equipment.equipmentSquadMatches(presetSquad, liveSquad), originalMatch(presetSquad, liveSquad));
    helperCases++;
  }
}
const emptyLive = { index: 1, heroes: [] };
assert.equal(equipment.equipmentSquadMatches(fixture.presets[0].squads[0], emptyLive), originalMatch(fixture.presets[0].squads[0], emptyLive)); helperCases++;
record("actual original/current snapshot counts and current-equipment predicate", helperCases);

let identityCases = 0;
const selected = fixture.presets[1];
const matches = equipment.currentEquipmentPresetMatches(fixture.presets, selected, fixture.squads);
for (const squadIndex of [1, 2, 3, 4]) {
  assert.equal(matches.get(squadIndex)?.id, fixture.presets[0].id);
  identityCases++;
}
assert.equal(equipment.currentEquipmentPresetLabel(matches, [1, 2, 3, 4], tFor(en)), fixture.presets[0].name); identityCases++;
const mixed = new Map([[1, fixture.presets[0]], [2, fixture.presets[1]]]);
assert.equal(equipment.currentEquipmentPresetLabel(mixed, [1, 2, 3, 4], tFor(en)), en["squad.mixedEquipmentPreset"]); identityCases++;
assert.equal(equipment.currentEquipmentPresetLabel(new Map(), [1, 2, 3, 4], tFor(en)), en["squad.unmatchedEquipmentPreset"]); identityCases++;
assert.deepEqual(equipment.equipmentDirtyPresetIds(fixture.presets, fixture.confirmedPresets), []); identityCases++;
const dirtyPresets = structuredClone(fixture.presets);
dirtyPresets[2].name += " changed";
assert.deepEqual(equipment.equipmentDirtyPresetIds(dirtyPresets, fixture.confirmedPresets), [dirtyPresets[2].id]); identityCases++;
record("selected identity current matching mixed/unmatched and derived dirty state", identityCases);

function runOriginalSquadSwap(config, selectedPreset, dragged, targetSquadIndex, liveSquads) {
  let saved = null;
  let success = null;
  const timers = [];
  const env = {
    f: structuredClone(config), N: selectedPreset, w: dragged, _: "", n: liveSquads,
    sd: originalFindSquad, cd: originalEnsurePosition,
    p: (value) => { saved = value; }, ie: (value) => { success = value; },
    window: { setTimeout: (_callback, ms) => timers.push(ms) },
  };
  const original = compileRawFunction(fdSource, squadSwapNode, env);
  original(targetSquadIndex);
  return { saved, success, timers };
}

async function runOriginalTargetSwap(config, selectedPreset, dragged, target) {
  let saved = null;
  let success = null;
  let toast = "";
  const timers = [];
  const env = {
    f: structuredClone(config), N: selectedPreset, w: dragged, _: "",
    cd: originalEnsurePosition,
    p: (value) => { saved = value; }, ie: (value) => { success = value; },
    se: (value) => { toast = value; }, o: (key) => key,
    window: { setTimeout: (_callback, ms) => timers.push(ms) },
  };
  const original = compileRawFunction(fdSource, targetSwapNode, env);
  await original(target);
  return { saved, success, toast, timers };
}

let moveCases = 0;
const baseConfig = { equipmentPresets: structuredClone(fixture.presets) };
const selectedPreset = baseConfig.equipmentPresets[0];
for (const target of [
  { dragged: { kind: "loadout", squadIndex: 1, position: 1 }, target: { kind: "loadout", squadIndex: 2, position: 1 } },
  { dragged: { kind: "equip", squadIndex: 1, position: 1, slot: 1 }, target: { kind: "equip", squadIndex: 2, position: 1, slot: 1 } },
  { dragged: { kind: "equip", squadIndex: 1, position: 1, slot: 2 }, target: { kind: "equip", squadIndex: 3, position: 1, slot: 2 } },
]) {
  const original = await runOriginalTargetSwap(baseConfig, selectedPreset, target.dragged, target.target);
  const current = equipment.swapEquipmentTarget(baseConfig.equipmentPresets, selectedPreset.id, target.dragged, target.target);
  assert.equal(current.handled, true);
  assert.deepEqual(current.presets, original.saved.equipmentPresets);
  assert.deepEqual(current.successKeys, original.success);
  assert.deepEqual(original.timers, [450]);
  moveCases += 4;
}
const wrongSlot = { kind: "equip", squadIndex: 1, position: 1, slot: 1 };
const wrongOriginal = await runOriginalTargetSwap(baseConfig, selectedPreset, wrongSlot, { kind: "equip", squadIndex: 2, position: 1, slot: 2 });
const wrongCurrent = equipment.swapEquipmentTarget(baseConfig.equipmentPresets, selectedPreset.id, wrongSlot, { kind: "equip", squadIndex: 2, position: 1, slot: 2 });
assert.equal(wrongOriginal.toast, "squad.sameSlotRequired");
assert.equal(wrongCurrent.error, "squad.sameSlotRequired");
assert.equal(wrongOriginal.saved, null);
assert.equal(wrongCurrent.handled, true); moveCases += 4;
const selfOriginal = await runOriginalTargetSwap(baseConfig, selectedPreset, wrongSlot, { ...wrongSlot });
const selfCurrent = equipment.swapEquipmentTarget(baseConfig.equipmentPresets, selectedPreset.id, wrongSlot, { ...wrongSlot });
assert.equal(selfOriginal.saved, null); assert.equal(selfCurrent.handled, false); moveCases += 2;
const originalSquad = runOriginalSquadSwap(baseConfig, selectedPreset, { kind: "squad", squadIndex: 1 }, 2, fixture.squads);
const currentSquad = equipment.swapEquipmentSquads(baseConfig.equipmentPresets, selectedPreset.id, 1, 2, fixture.squads);
assert.deepEqual(currentSquad.presets, originalSquad.saved.equipmentPresets);
assert.deepEqual(currentSquad.successKeys, originalSquad.success);
assert.deepEqual(originalSquad.timers, [450]);
assert.deepEqual(currentSquad.successKeys, ["1-1", "2-1", "1-2", "2-2"]); moveCases += 4;
record("actual original/current item loadout squad transformations wrong-slot self-drop and 450ms success", moveCases);

let dialogCases = 0;
const originalDialog = compile(indexSource, "In", { b: { useRef: () => ({ current: null }), useEffect: () => {} }, M: jsx });
const dialogHook = hooks();
const currentDialog = compile(pages, "EquipmentDialog", { h, ...dialogHook });
for (const busy of [false, true]) {
  let originalClosed = 0;
  let currentClosed = 0;
  const originalTree = originalDialog({ children: "content", className: "equipment-preset-dialog-backdrop", labelledBy: "equipment-preset-title", busy, onClose: () => originalClosed++ });
  dialogHook.begin();
  const currentTree = currentDialog({ children: "content", busy, onClose: () => currentClosed++ });
  const event = { preventDefault() {} };
  originalTree.props.onCancel(event);
  currentTree.props.onCancel(event);
  assert.equal(currentClosed, originalClosed);
  assert.equal(currentTree.props["aria-busy"], originalTree.props["aria-busy"]);
  assert.equal(currentTree.props["aria-modal"], originalTree.props["aria-modal"]);
  if (originalTree.props.onPointerDown) originalTree.props.onPointerDown({ target: "backdrop", currentTarget: "backdrop" });
  if (originalTree.props.onClick) originalTree.props.onClick({ target: "backdrop", currentTarget: "backdrop" });
  assert.equal(originalClosed, busy ? 0 : 1, "Equipment original default backdrop must not add a close");
  dialogCases += 4;
}
record("actual original/current Equipment dialog cancel busy and default no-backdrop-dismiss behavior", dialogCases);

function renderEquipment(state, catalog = en) {
  const hook = hooks();
  const timers = [];
  const listeners = new Map();
  const fakeWindow = {
    setTimeout: (_callback, ms) => { timers.push(ms); return timers.length; },
    clearTimeout() {},
    addEventListener: (name, callback) => listeners.set(name, callback),
    removeEventListener: (name) => listeners.delete(name),
  };
  const component = compile(pages, "EquipmentContent", {
    h, Fragment, ...hook,
    useI18n: () => ({ t: tFor(catalog) }),
    ...equipment,
    EquipmentDialog: "Dialog",
    window: fakeWindow,
  });
  const render = () => { hook.begin(); return component({ previewEnabled: true, previewState: state }); };
  return { hook, render, timers, listeners };
}

let callbackCases = 0;
for (const catalog of [en, ja]) {
  const runner = renderEquipment("squads-equipment", catalog);
  let tree = runner.render();
  const presetButtons = flatten(tree).filter((node) => node.type === "button" && node.props?.className !== undefined && node.props?.className !== "primary" && text(node).includes("Alt+"));
  assert.equal(presetButtons.length, 4);
  presetButtons[1].props.onClick();
  tree = runner.render();
  const active = flatten(tree).find((node) => node.type === "button" && node.props?.className === "active");
  assert.ok(text(active).includes("Fixture equipment preset 2"));
  assert.ok(text(tree).includes(tFor(catalog)("squad.currentEquipmentPreset", { name: "Fixture equipment preset 1" })));
  callbackCases += 3;

  const rename = flatten(tree).find((node) => node.type === "button" && text(node) === tFor(catalog)("common.rename"));
  rename.props.onClick();
  tree = runner.render();
  const input = flatten(tree).find((node) => node.type === "input");
  assert.equal(input.props.value, "Fixture equipment preset 2");
  input.props.onChange({ target: { value: "  Renamed fixture  " } });
  tree = runner.render();
  const save = flatten(tree).filter((node) => node.type === "button" && text(node) === tFor(catalog)("common.saveConfig")).at(-1);
  save.props.onClick();
  tree = runner.render();
  assert.ok(text(tree).includes("Renamed fixture"));
  assert.ok(!flatten(tree).some((node) => node.type === "Dialog"));
  callbackCases += 4;
}
record("actual production preset selection current-match and rename callbacks in English/Japanese", callbackCases);

let actionCases = 0;
for (const state of ["squads-equipment", "squads-equipment-progress", "squads-equipment-result", "squads-equipment-error", "squads-equipment-offline", "squads-equipment-rename-busy"]) {
  const runner = renderEquipment(state);
  const tree = runner.render();
  const buttons = flatten(tree).filter((node) => node.type === "button");
  const readCurrent = buttons.find((node) => text(node) === en["squad.loadCurrentEquipment"]);
  const saveApply = buttons.find((node) => [en["squad.saveAndApplyEquipmentConfig"], en["squad.equipmentApplying"]].includes(text(node)));
  assert.ok(readCurrent && saveApply, state);
  const busy = state === "squads-equipment-progress" || state === "squads-equipment-rename-busy";
  const offline = state === "squads-equipment-offline";
  assert.equal(readCurrent.props.disabled, busy || offline);
  assert.equal(saveApply.props.disabled, busy || offline);
  if (state === "squads-equipment-progress") {
    assert.equal(text(saveApply), en["squad.equipmentApplying"]);
    assert.ok(flatten(tree).some((node) => node.props?.className === "equipment-apply-progress"));
  }
  if (state === "squads-equipment-result") assert.ok(flatten(tree).some((node) => node.props?.className === "equipment-result equipment-result-partial"));
  if (state === "squads-equipment-error") assert.ok(flatten(tree).some((node) => node.props?.className === "equipment-result equipment-result-rejected"));
  actionCases += 4;
}
record("production supplied-state partial/error/progress and action-enabled presentation", actionCases);

// Timer and keydown expressions stay source-located even though hook-only renderer execution does not schedule effects.
assert.ok(fdSource.includes("window.setTimeout(()=>se(``),1800)"), "Recovered toast timer changed");
assert.ok(pages.includes("window.setTimeout(() => setToast(\"\"), 1800)"), "Current toast timer differs from recovered 1800 ms");
assert.ok(fdSource.includes("!e.altKey||e.repeat||!/^[1-4]$/.test(e.key)"), "Recovered Alt+1..4 predicate changed");
assert.ok(pages.includes("!event.altKey || event.repeat || !/^[1-4]$/.test(event.key)"), "Current Alt+1..4 predicate differs");
record("source-located 1800ms toast cleanup and Alt+1..4 negative predicate", 2);

const assetPaths = [
  "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/SquadPanel-HC3-DJei.js",
  "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js",
  "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/GameAssetImage-Diy9VTIr.js",
  "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-rIL9Fpht.css",
  "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/en-BisSXcTB.js",
  "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/ja-UrbzJu-m.js",
];
const assets = assetPaths.map((assetPath) => ({ path: assetPath, sha256: hash(read(assetPath)) }));
const sourceManifest = {
  referenceExecutableSha256: "4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783",
  locators,
  assets,
  current: {
    "src/LWBridge.UI-0.3.17/src/Pages.jsx": hash(pages),
    "src/LWBridge.UI-0.3.17/src/previewEquipmentContracts.js": hash(currentContracts),
  },
};
const report = {
  result: "LWB317_EQUIPMENT_ACTUAL_SOURCE_OK",
  results,
  limits: "Exact original helper execution plus nested original/current transformation and production callback proof. Browser record separately distinguishes physical HTML5 drag from DOM/handler proof. No live game or native Equipment provider is exercised.",
};
if (process.argv.includes("--record")) {
  fs.writeFileSync(path.join(here, "actual-source-results.json"), `${JSON.stringify(report, null, 2)}\n`);
  fs.writeFileSync(path.join(here, "source-manifest.json"), `${JSON.stringify(sourceManifest, null, 2)}\n`);
}
console.log(JSON.stringify(report, null, 2));
