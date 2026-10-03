import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { compile, flatten, fn, Fragment, h, hooks, nodes, raw, read, require, squad, text, transformSync } from "../LWB317-UI-AUTOMATION-AFK-CLOSEOUT-001/harness.mjs";
import * as equipment from "../../../../src/LWBridge.UI-0.3.17/src/previewEquipmentContracts.js";
import en from "../../../../src/LWBridge.UI-0.3.17/src/locales/en.js";
import ja from "../../../../src/LWBridge.UI-0.3.17/src/locales/ja.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const pages = read("src/LWBridge.UI-0.3.17/src/Pages.jsx");
const configHookSource = read("src/LWBridge.UI-0.3.17/src/previewConfigHook.jsx");
const react = require("react");
const results = [];
const record = (name, count) => results.push({ name, count, result: "PASS" });
const tFor = (catalog) => (key, vars = {}) => String(catalog[key] || key || "").replace(/\{(\w+)\}/g, (match, name) => vars[name] ?? match);
const settle = () => new Promise((resolve) => setImmediate(resolve));

assert.equal(react.version, "19.3.0");
assert.equal(typeof react.Activity, "symbol");

const PreviewConfigError = compile(configHookSource, "PreviewConfigError", { h, Promise });

function equipmentRunner(previewState = "squads-equipment", catalog = en, captureEffects = false) {
  const hook = hooks();
  const effects = [];
  const listeners = new Map();
  const timers = [];
  const fakeWindow = {
    setTimeout(callback, ms) { timers.push({ callback, ms }); return timers.length; },
    clearTimeout() {},
    addEventListener(name, callback) { listeners.set(name, callback); },
    removeEventListener(name, callback) { if (listeners.get(name) === callback) listeners.delete(name); },
  };
  const component = compile(pages, "EquipmentContent", {
    h,
    Fragment,
    ...hook,
    ...(captureEffects ? { useEffect: (effect) => effects.push(effect) } : {}),
    useI18n: () => ({ t: tFor(catalog) }),
    ...equipment,
    EquipmentDialog: "Dialog",
    PreviewConfigError,
    window: fakeWindow,
  });
  const render = () => {
    hook.begin();
    if (captureEffects) effects.length = 0;
    return component({ previewEnabled: true, previewState });
  };
  return { hook, effects, listeners, timers, render };
}

function button(tree, label, last = false) {
  const matches = flatten(tree).filter((node) => node.type === "button" && text(node) === label);
  return last ? matches.at(-1) : matches[0];
}

function openRename(runner, catalog = en) {
  let tree = runner.render();
  button(tree, tFor(catalog)("common.rename")).props.onClick();
  tree = runner.render();
  assert.ok(flatten(tree).some((node) => node.type === "Dialog"));
  return tree;
}

function setRename(tree, value) {
  flatten(tree).find((node) => node.type === "input").props.onChange({ target: { value } });
}

function renderConfigError(tree) {
  const node = flatten(tree).find((entry) => entry.type === PreviewConfigError && entry.props.config.error);
  assert.ok(node, "Expected configured save error renderer");
  return node.type(node.props);
}

// The recovered parent uses Activity for first-visited tab retention.
const parentHook = hooks();
const parent = compile(pages, "SquadsPage", {
  h,
  ...parentHook,
  Activity: react.Activity,
  useI18n: () => ({ t: tFor(en) }),
  AfkContent: "AfkContent",
  EquipmentContent: "EquipmentContent",
});
const renderParent = () => { parentHook.begin(); return parent({ previewState: "squads-equipment" }); };
let parentTree = renderParent();
assert.ok(flatten(parentTree).some((node) => node.type === react.Activity && node.props.mode === "visible" && flatten(node.props.children).some((child) => child.type === "EquipmentContent")));
button(parentTree, en["squad.tabAfk"]).props.onClick();
parentTree = renderParent();
assert.ok(flatten(parentTree).some((node) => node.type === react.Activity && node.props.mode === "hidden" && flatten(node.props.children).some((child) => child.type === "EquipmentContent")));
assert.ok(flatten(parentTree).some((node) => node.type === react.Activity && node.props.mode === "visible" && flatten(node.props.children).some((child) => child.type === "AfkContent")));
record("current parent uses React Activity visible/hidden retention boundary", 3);

// Execute the actual Equipment Alt effect, cleanup, and re-install path.
const effectsRunner = equipmentRunner("squads-equipment", en, true);
let effectTree = effectsRunner.render();
let cleanup = effectsRunner.effects[0]();
assert.equal(effectsRunner.listeners.size, 1);
let prevented = 0;
effectsRunner.listeners.get("keydown")({ altKey: true, repeat: false, key: "2", target: { tagName: "BODY" }, preventDefault() { prevented += 1; } });
effectTree = effectsRunner.render();
assert.equal(prevented, 1);
assert.equal(effectTree.props["data-preview-action"], "apply-all:equipment-preset-fixed-2");
cleanup();
assert.equal(effectsRunner.listeners.size, 0);
prevented = 0;
assert.equal(effectsRunner.listeners.get("keydown"), undefined);
effectTree = effectsRunner.render();
cleanup = effectsRunner.effects[0]();
assert.equal(effectsRunner.listeners.size, 1);
effectsRunner.listeners.get("keydown")({ altKey: true, repeat: false, key: "3", target: { tagName: "BODY" }, preventDefault() { prevented += 1; } });
effectTree = effectsRunner.render();
assert.equal(prevented, 1);
assert.equal(effectTree.props["data-preview-action"], "apply-all:equipment-preset-fixed-3");
cleanup();
record("actual Equipment Alt effect cleanup and single re-install", 8);

for (const event of [
  { altKey: true, repeat: true, key: "1", target: { tagName: "BODY" } },
  { altKey: true, repeat: false, key: "1", target: { tagName: "INPUT" } },
]) {
  const runner = equipmentRunner("squads-equipment", en, true);
  let tree = runner.render();
  const dispose = runner.effects[0]();
  let blocked = false;
  runner.listeners.get("keydown")({ ...event, preventDefault() { blocked = true; } });
  tree = runner.render();
  assert.equal(blocked, false);
  assert.equal(tree.props["data-preview-action"], undefined);
  dispose();
}
const busyRunner = equipmentRunner("squads-equipment-progress", en, true);
let busyTree = busyRunner.render();
const disposeBusy = busyRunner.effects[0]();
let busyPrevented = false;
busyRunner.listeners.get("keydown")({ altKey: true, repeat: false, key: "1", target: { tagName: "BODY" }, preventDefault() { busyPrevented = true; } });
busyTree = busyRunner.render();
assert.equal(busyPrevented, false);
assert.equal(busyTree.props["data-preview-action"], undefined);
disposeBusy();
record("Alt repeat/input/busy negative predicates remain inert", 6);

// Execute the original rename callback as the acknowledgement oracle.
const originalFd = raw(squad, fn(squad, "fd"));
const renameNode = nodes(originalFd).find((node) => node.type === "FunctionDeclaration" && node.start > 0 && raw(originalFd, node).includes("le.trim()"));
assert.ok(renameNode);
const renameCode = transformSync(raw(originalFd, renameNode), { loader: "js" }).code;
const fixture = equipment.previewEquipmentFixture("squads-equipment", tFor(en));

async function originalRenameCase(acknowledgement) {
  const state = { busy: "", closed: false };
  const env = {
    le: "Renamed fixture",
    f: { equipmentPresets: fixture.presets },
    _: "",
    ce: true,
    N: fixture.presets[0],
    j: [],
    ve: () => { state.closed = true; },
    v: (value) => { state.busy = value; },
    P: () => acknowledgement,
  };
  const originalRename = new Function(...Object.keys(env), `${renameCode}; return ${renameNode.id.name};`)(...Object.values(env));
  const pending = originalRename();
  assert.deepEqual(state, { busy: "rename", closed: false });
  await pending;
  return state;
}

assert.deepEqual(await originalRenameCase(Promise.resolve({ ok: true })), { busy: "", closed: true });
assert.deepEqual(await originalRenameCase(Promise.resolve(null)), { busy: "", closed: false });
record("recovered rename acknowledgement success/null oracle", 4);

// Current controlled pending acknowledgement: dialog stays open and busy.
const pendingRunner = equipmentRunner("squads-equipment-rename-pending");
let tree = openRename(pendingRunner);
setRename(tree, "Pending rename");
tree = pendingRunner.render();
button(tree, en["common.saveConfig"], true).props.onClick();
tree = pendingRunner.render();
assert.equal(tree.props["data-equipment-busy"], "rename");
assert.ok(flatten(tree).some((node) => node.type === "Dialog"));
assert.equal(flatten(tree).find((node) => node.type === "input").props.disabled, true);
const pendingDialog = flatten(tree).find((node) => node.type === "Dialog");
pendingDialog.props.onClose();
tree = pendingRunner.render();
assert.ok(flatten(tree).some((node) => node.type === "Dialog"));
record("current rename pending acknowledgement retains busy dialog", 4);

// Current rejected acknowledgement exposes exact failure/retry presentation and stays open.
const errorRunner = equipmentRunner("squads-equipment-rename-error");
tree = openRename(errorRunner);
setRename(tree, "Rejected rename");
tree = errorRunner.render();
button(tree, en["common.saveConfig"], true).props.onClick();
assert.equal(errorRunner.render().props["data-equipment-busy"], "rename");
await settle();
tree = errorRunner.render();
assert.equal(tree.props["data-equipment-busy"], undefined);
assert.ok(flatten(tree).some((node) => node.type === "Dialog"));
let errorTree = renderConfigError(tree);
assert.ok(text(errorTree).includes(en["squad.equipmentPresets"]));
assert.ok(text(errorTree).includes(en["configSave.failed"]));
assert.ok(button(errorTree, en["common.retry"]));
assert.ok(button(errorTree, en["configSave.discard"]));
button(errorTree, en["common.retry"]).props.onClick();
await settle();
tree = errorRunner.render();
assert.ok(flatten(tree).some((node) => node.type === "Dialog"));
assert.ok(!flatten(tree).some((node) => node.type === PreviewConfigError && node.props.config.error));
button(tree, en["common.saveConfig"], true).props.onClick();
tree = errorRunner.render();
assert.ok(!flatten(tree).some((node) => node.type === "Dialog"));
record("current rename rejection clears busy, retains dialog, and Retry acknowledges draft", 9);

// Discard restores the confirmed preset without closing the dialog.
const discardRunner = equipmentRunner("squads-equipment-rename-error");
tree = openRename(discardRunner);
setRename(tree, "Discarded rename");
tree = discardRunner.render();
button(tree, en["common.saveConfig"], true).props.onClick();
await settle();
tree = discardRunner.render();
errorTree = renderConfigError(tree);
button(errorTree, en["configSave.discard"]).props.onClick();
await settle();
tree = discardRunner.render();
assert.ok(flatten(tree).some((node) => node.type === "Dialog"));
assert.ok(text(tree).includes("Fixture equipment preset 1"));
assert.ok(!text(button(tree, en["common.rename"]).props?.children).includes("Discarded rename"));
record("rename failure Discard restores confirmed draft and retains dialog", 3);

// Already-dirty item movement is included in the same full-draft rename acknowledgement.
for (const catalog of [en, ja]) {
  const runner = equipmentRunner("squads-equipment", catalog);
  tree = runner.render();
  let slots = flatten(tree).filter((node) => typeof node.props?.className === "string" && node.props.className.includes("preset-equipment-slot"));
  slots[0].props.onDragStart({ stopPropagation() {} });
  tree = runner.render();
  slots = flatten(tree).filter((node) => typeof node.props?.className === "string" && node.props.className.includes("preset-equipment-slot"));
  slots[4].props.onDrop({ preventDefault() {}, stopPropagation() {} });
  tree = runner.render();
  const activeBefore = flatten(tree).find((node) => node.type === "button" && node.props.className === "active");
  assert.ok(text(activeBefore).includes(" *"));
  button(tree, tFor(catalog)("common.rename")).props.onClick();
  tree = runner.render();
  setRename(tree, "  Renamed and moved  ");
  tree = runner.render();
  button(tree, tFor(catalog)("common.saveConfig"), true).props.onClick();
  tree = runner.render();
  assert.ok(!flatten(tree).some((node) => node.type === "Dialog"));
  const activeAfter = flatten(tree).find((node) => node.type === "button" && node.props.className === "active");
  assert.ok(text(activeAfter).includes("Renamed and moved"));
  assert.ok(!text(activeAfter).includes(" *"));
  assert.ok(text(tree).includes("Fixture equipment preset 2"));
}
record("already-dirty moved draft plus rename acknowledges full draft in English/Japanese", 12);

// Blank, unchanged, Enter and busy-close branches stay source-shaped.
const blankRunner = equipmentRunner();
tree = openRename(blankRunner);
setRename(tree, "   ");
tree = blankRunner.render();
assert.equal(button(tree, en["common.saveConfig"], true).props.disabled, true);
button(tree, en["common.saveConfig"], true).props.onClick();
assert.ok(flatten(blankRunner.render()).some((node) => node.type === "Dialog"));

const unchangedRunner = equipmentRunner();
tree = openRename(unchangedRunner);
button(tree, en["common.saveConfig"], true).props.onClick();
assert.ok(!flatten(unchangedRunner.render()).some((node) => node.type === "Dialog"));

const enterRunner = equipmentRunner();
tree = openRename(enterRunner);
setRename(tree, "Enter rename");
tree = enterRunner.render();
let enterPrevented = false;
flatten(tree).find((node) => node.type === "input").props.onKeyDown({ key: "Enter", preventDefault() { enterPrevented = true; } });
assert.equal(enterPrevented, true);
assert.ok(!flatten(enterRunner.render()).some((node) => node.type === "Dialog"));
record("rename blank unchanged Enter and pending-close guards", 7);

// The save-only consumer shares the same failure/retry acknowledgement path.
const saveOnlyRunner = equipmentRunner("squads-equipment-rename-error");
tree = saveOnlyRunner.render();
let slots = flatten(tree).filter((node) => typeof node.props?.className === "string" && node.props.className.includes("preset-equipment-slot"));
slots[0].props.onDragStart({ stopPropagation() {} });
tree = saveOnlyRunner.render();
slots = flatten(tree).filter((node) => typeof node.props?.className === "string" && node.props.className.includes("preset-equipment-slot"));
slots[4].props.onDrop({ preventDefault() {}, stopPropagation() {} });
tree = saveOnlyRunner.render();
assert.ok(text(flatten(tree).find((node) => node.type === "button" && node.props.className === "active")).includes(" *"));
button(tree, en["squad.saveEquipmentConfig"]).props.onClick();
await settle();
tree = saveOnlyRunner.render();
errorTree = renderConfigError(tree);
assert.ok(text(errorTree).includes(en["configSave.failed"]));
assert.ok(!flatten(tree).some((node) => typeof node.props?.className === "string" && node.props.className.includes("equipment-toast")));
button(errorTree, en["common.retry"]).props.onClick();
await settle();
tree = saveOnlyRunner.render();
assert.ok(!text(flatten(tree).find((node) => node.type === "button" && node.props.className === "active")).includes(" *"));
record("save-only consumer shares failure and Retry acknowledgement without native provider", 5);

const report = {
  result: "LWB317_EQUIPMENT_R1_ACTUAL_OK",
  results,
  source: {
    activityUtf8Byte: 191313,
    altEffectUtf8Byte: 179906,
    renameUtf8Byte: 180434,
  },
  limits: "Actual current callbacks/effects plus recovered original rename callback. React Activity suspension/state retention is additionally exercised in the real browser mount; no native Equipment provider, gameplay, or original post-auth pixel comparison is used.",
};

if (process.argv.includes("--record")) fs.writeFileSync(path.join(here, "r1-results.json"), `${JSON.stringify(report, null, 2)}\n`);
console.log(JSON.stringify(report, null, 2));
