globalThis.GameAssetImage = ()=>null; // Actual image nodes separately source/pixel-verified; historical predicates do not inspect asset rendering.
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { Fragment, compile, flatten, h, hooks, jsx, nodes, raw, read, text } from "file:///C:/Users/chimw/OneDrive/Desktop/Github/LW-Control/evidence/lwbridge-0.3.17/ui/LWB317-UI-AUTOMATION-AFK-CLOSEOUT-001/harness.mjs";
import * as contracts from "file:///C:/Users/chimw/OneDrive/Desktop/Github/LW-Control/src/LWBridge.UI-0.3.17/src/previewRemainingPagesContracts.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const pages = ["CityLayoutPage.jsx","HotkeyPages.jsx","SettingsPage.jsx","Pages.jsx"].map(file=>read("src/LWBridge.UI-0.3.17/src/"+file).replace(/^import .*?;\r?\n/gm,"" )).join("\n");
const source = read("evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/CityLayoutPanel-DoNWkywK.js");
const fixture = contracts.previewCityLayoutFixture();
const fakeWindow = {
  getComputedStyle: () => ({ paddingLeft: "0", paddingTop: "0" }),
  setInterval: () => 1,
  clearInterval() {},
  setTimeout: () => 1,
  clearTimeout() {},
  addEventListener() {},
  removeEventListener() {},
};
const settle = () => new Promise((resolve) => setImmediate(resolve));
const stateSet = (name) => new Function(`return (${raw(pages, nodes(pages).find((node) => node.type === "VariableDeclarator" && node.id.name === name).init)});`)();
const results = [];
const record = (name, cases) => results.push({ name, cases, result: "PASS" });

function cityRunner(original, state = "city-layout-populated", placements = [], overrides = {}) {
  const hook = hooks(original ? [fixture, { past: [], present: placements, future: [] }, 24, "", undefined, fixture.buildings[0].uuid, [fixture.buildings[0].uuid], {}, 6, fixture.layoutRevision, false, false, "", null, null, false] : []);
  const env = { window: fakeWindow, document: { activeElement: null } };
  let Component;
  if (original) {
    for (const name of ["te", "ne", "m", "re", "ie", "ae", "oe", "le", "g"]) env[name] = compile(source, name, env);
    Object.assign(env, {
      p: { ...hook, useCallback: (fn) => fn },
      h: jsx,
      s: () => ({ t: (key) => key, language: "en" }),
      se: { past: [], present: [], future: [] },
      ce: 24,
      ee: () => null,
      f: overrides.originalValidate ?? (() => new Promise(() => {})),
      ...overrides.originalEnv,
    });
    Component = compile(source, "ue", env);
  } else {
    Object.assign(env, {
      h, Fragment, ...hook, ...contracts,
      useI18n: () => ({ t: (key) => key }),
      CITY_LAYOUT_PREVIEW_STATES: stateSet("CITY_LAYOUT_PREVIEW_STATES"),
      cityLayoutIssues: overrides.currentIssues ?? contracts.cityLayoutIssues,
    });
    Component = compile(pages, "CityLayoutPage", env);
  }
  const render = () => {
    hook.begin();
    const tree = Component(original ? { profileId: "inert", online: true, onLog() {} } : { previewState: state, online: false });
    const grid = flatten(tree).find((node) => /^city-layout-grid(?: dragging)?$/.test(node.props?.className));
    if (grid) grid.props.ref.current = { getBoundingClientRect: () => ({ left: 0, top: 0 }) };
    return tree;
  };
  return { hook, render };
}

const hasWorkbench = (tree) => flatten(tree).some((node) => node.props?.className === "city-layout-workbench");
for (const state of ["city-layout-loading", "city-layout-error"]) {
  const original = cityRunner(true);
  original.hook.values[0] = null;
  original.hook.values[11] = state.endsWith("loading");
  original.hook.values[12] = state.endsWith("error") ? "controlled-error" : "";
  const current = cityRunner(false, state);
  assert.equal(hasWorkbench(original.render()), false, `${state} original null branch`);
  assert.equal(hasWorkbench(current.render()), false, `${state} current null branch`);
}
record("null-layout loading/error branch", ["loading", "error"]);

const pointer = (x, y, extras = {}) => ({ clientX: x, clientY: y, pointerId: 1, ctrlKey: false, shiftKey: false, stopPropagation() {}, currentTarget: { setPointerCapture() {} }, ...extras });
function findBuilding(tree, name) {
  return flatten(tree).find((node) => node.type === "button" && node.props.title?.startsWith(name));
}
function drag(original, name, down, moves, { cancel = false, group = false } = {}) {
  const runner = cityRunner(original);
  let tree = runner.render();
  if (group) {
    for (const member of ["Hospital", "Barracks"]) {
      findBuilding(tree, member).props.onPointerDown(pointer(0, 0, { ctrlKey: true }));
      tree = runner.render();
    }
  }
  findBuilding(tree, name).props.onPointerDown(pointer(down[0], down[1]));
  tree = runner.render();
  let grid = flatten(tree).find((node) => node.props?.className === "city-layout-grid dragging");
  assert.ok(grid, `${name} drag starts`);
  for (const [x, y] of moves) {
    grid.props.onPointerMove(pointer(x, y));
    tree = runner.render();
    grid = flatten(tree).find((node) => node.props?.className === "city-layout-grid dragging");
  }
  if (cancel) grid.props.onPointerCancel();
  else grid.props.onPointerUp(pointer(moves.at(-1)?.[0] ?? down[0], moves.at(-1)?.[1] ?? down[1]));
  tree = runner.render();
  return {
    placements: runner.hook.values[original ? 1 : 0].present,
    dragging: flatten(tree).some((node) => node.props?.className === "city-layout-grid dragging"),
    tree,
  };
}

const exactOffsetOriginal = drag(true, "Hospital", [84, 108], [[84, 108]]);
const exactOffsetCurrent = drag(false, "Hospital", [84, 108], [[84, 108]]);
assert.deepEqual(exactOffsetOriginal.placements, []);
assert.deepEqual(exactOffsetCurrent.placements, exactOffsetOriginal.placements);

const exactFallbackOriginal = drag(true, "Barracks", [108, 36], [[180, 84]]);
const exactFallbackCurrent = drag(false, "Barracks", [108, 36], [[180, 84]]);
assert.deepEqual(exactFallbackOriginal.placements, [{ uuid: "preview-barracks", targetPointId: 505 }]);
assert.deepEqual(exactFallbackCurrent.placements, exactFallbackOriginal.placements);
record("exact lead pointer offset/fallback cases", ["Hospital 84,108 no-op", "Barracks 108,36 -> 180,84 target505"]);

const releaseCases = [
  ["road rejection", "Hospital", [60, 108], [[12, 108]]],
  ["locked rejection", "Hospital", [60, 108], [[60, 180]]],
  ["overlap rejection", "Hospital", [60, 108], [[108, 36]]],
  ["boundary candidate/fallback", "Hospital", [60, 108], [[180, 12]]],
  ["previous accepted target retained", "Barracks", [108, 36], [[108, 12], [180, 12]]],
];
for (const [name, building, down, moves] of releaseCases) {
  const original = drag(true, building, down, moves);
  const current = drag(false, building, down, moves);
  assert.deepEqual(current.placements, original.placements, name);
}
record("release validation and previous-target behavior", releaseCases.map(([name]) => name));

const invalidGroupOriginal = drag(true, "Hospital", [60, 108], [[180, 12]], { group: true });
const invalidGroupCurrent = drag(false, "Hospital", [60, 108], [[180, 12]], { group: true });
assert.deepEqual(invalidGroupCurrent.placements, invalidGroupOriginal.placements, "invalid group boundary");
assert.deepEqual(invalidGroupCurrent.placements, []);
record("invalid group candidate", ["Hospital+Barracks boundary move"]);

const cancelOriginal = drag(true, "Hospital", [60, 108], [[108, 108]], { cancel: true });
const cancelCurrent = drag(false, "Hospital", [60, 108], [[108, 108]], { cancel: true });
assert.deepEqual(cancelCurrent.placements, cancelOriginal.placements);
assert.equal(cancelOriginal.dragging, false);
assert.equal(cancelCurrent.dragging, false);
record("pointer cancel cleanup", ["drag state cleared", "history unchanged"]);

const currentApply = cityRunner(false, "city-layout-populated-moved");
let tree = currentApply.render();
currentApply.hook.values[0].past = [[]];
tree = currentApply.render();
let apply = flatten(tree).find((node) => node.type === "button" && text(node) === "cityLayout.apply");
apply.props.onClick();
tree = currentApply.render();
let undo = flatten(tree).find((node) => node.type === "button" && text(node) === "cityLayout.undo");
let refresh = flatten(tree).find((node) => node.type === "button" && text(node) === "common.refresh");
assert.equal(undo.props.disabled, true, "pending preparation disables Undo");
assert.equal(refresh.props.disabled, true, "pending preparation disables Refresh");
const beforeDirectUndo = structuredClone(currentApply.hook.values[0]);
undo.props.onClick();
assert.deepEqual(currentApply.hook.values[0], beforeDirectUndo, "disabled Undo callback is inert while busy");
await settle();
tree = currentApply.render();
const dialog = flatten(tree).find((node) => node.props?.role === "dialog");
assert.ok(dialog, "local inert confirmation opens after preparation");
const cancel = flatten(dialog).find((node) => node.type === "button" && text(node) === "common.cancel");
cancel.props.onClick();
tree = currentApply.render();
undo = flatten(tree).find((node) => node.type === "button" && text(node) === "cityLayout.undo");
assert.equal(undo.props.disabled, false, "cancel clears preparation busy");
assert.deepEqual(currentApply.hook.values[0].present, contracts.previewCityLayoutFixture("city-layout-populated-moved").placements, "cancel retains draft");
record("Apply preparation/cancel busy lifecycle", ["pending Undo/Refresh gate", "direct Undo inert", "confirmation", "cancel clears busy", "draft retained"]);

const invalidApply = cityRunner(false, "city-layout-populated-conflict");
tree = invalidApply.render();
const invalidDraft = structuredClone(invalidApply.hook.values[0].present);
apply = flatten(tree).find((node) => node.type === "button" && text(node) === "cityLayout.apply");
apply.props.onClick();
assert.equal(flatten(invalidApply.render()).find((node) => node.type === "button" && text(node) === "cityLayout.undo").props.disabled, true);
await settle();
tree = invalidApply.render();
assert.ok(!flatten(tree).some((node) => node.props?.role === "dialog"));
assert.deepEqual(invalidApply.hook.values[0].present, invalidDraft);
record("Apply rejected validation cleanup", ["busy enters", "no confirm", "draft retained"]);

let failPreparation = false;
const failedApply = cityRunner(false, "city-layout-populated-moved", [], { currentIssues: (...args) => {
  if (failPreparation) throw new Error("controlled preparation failure");
  return contracts.cityLayoutIssues(...args);
} });
tree = failedApply.render();
const failedDraft = structuredClone(failedApply.hook.values[0].present);
apply = flatten(tree).find((node) => node.type === "button" && text(node) === "cityLayout.apply");
failPreparation = true;
apply.props.onClick();
await settle();
failPreparation = false;
tree = failedApply.render();
assert.ok(text(tree).includes("common.actionFailed"));
assert.deepEqual(failedApply.hook.values[0].present, failedDraft);
assert.equal(flatten(tree).find((node) => node.type === "button" && text(node) === "common.refresh").props.disabled, false);
record("Apply preparation failure cleanup", ["error surfaced", "busy clears", "draft retained"]);

const report = {
  workItem: "LWB317-UI-REMAINING-PAGES-001-R1",
  milestone: "B",
  result: "PASS",
  results,
  sourceLocators: {
    nullLayout: "CityLayoutPanel-DoNWkywK.js ue null-layout branch byte19149",
    dragCandidate: "CityLayoutPanel-DoNWkywK.js it byte7661 / at byte8219 / tt source-local region predicate",
    grabOffset: "CityLayoutPanel-DoNWkywK.js pointer-down producer around byte15627",
    applyPreparation: "CityLayoutPanel-DoNWkywK.js U=!!ze||Oe and ct around byte9199",
  },
  submittedBaseline: "Preserved lead packet records populated loading/error fixtures, missing grab offset, whole-move rejection, and missing Apply preparation busy gate at afd65b6.",
  limits: "Actual recovered/current component callbacks are compared in an inert harness. Confirmation remains local/inert. No native validation, City apply, gameplay, service, or window.confirm is invoked.",
};
if (process.argv.includes("--record")) fs.writeFileSync(path.join(here, "results.json"), `${JSON.stringify(report, null, 2)}\n`);
console.log(JSON.stringify(report, null, 2));
