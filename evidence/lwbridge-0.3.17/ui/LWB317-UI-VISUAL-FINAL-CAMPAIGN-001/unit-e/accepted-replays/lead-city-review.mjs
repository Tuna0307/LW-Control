globalThis.GameAssetImage = ()=>null; // Actual image nodes separately source/pixel-verified; historical predicates do not inspect asset rendering.
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import { Fragment, compile, flatten, h, hooks, jsx, nodes, raw, read, repo, text } from "file:///C:/Users/chimw/OneDrive/Desktop/Github/LW-Control/evidence/lwbridge-0.3.17/ui/LWB317-UI-AUTOMATION-AFK-CLOSEOUT-001/harness.mjs";
import * as contracts from "file:///C:/Users/chimw/OneDrive/Desktop/Github/LW-Control/src/LWBridge.UI-0.3.17/src/previewRemainingPagesContracts.js";

const submittedRevision = "20298d59ddd5777e134406a75e0ff7fb144eccb1";
const submittedPages = execFileSync("git", ["show", `${submittedRevision}:src/LWBridge.UI-0.3.17/src/Pages.jsx`], { cwd: repo, encoding: "utf8" });
const currentPages = ["CityLayoutPage.jsx","HotkeyPages.jsx","SettingsPage.jsx","Pages.jsx"].map(file=>read("src/LWBridge.UI-0.3.17/src/"+file).replace(/^import .*?;\r?\n/gm,"" )).join("\n");
const originalSource = read("evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/CityLayoutPanel-DoNWkywK.js");
const fixture = contracts.previewCityLayoutFixture();
const settle = () => new Promise((resolve) => setImmediate(resolve));
const here = path.dirname(fileURLToPath(import.meta.url));
const button = (tree, key) => flatten(tree).find((node) => node.type === "button" && text(node) === key);
const hasDialog = (tree) => flatten(tree).some((node) => node.props?.role === "dialog");
const pointer = (x, y) => ({ clientX: x, clientY: y, pointerId: 1, ctrlKey: false, shiftKey: false, stopPropagation() {}, currentTarget: { setPointerCapture() {} } });

function runner(pages, { original = false, moved = false, confirm = () => false } = {}) {
  const placements = moved ? [{ uuid: "preview-hospital", targetPointId: 405 }] : [];
  const hook = hooks(original ? [fixture, { past: [], present: placements, future: [] }, 24, "", undefined, fixture.buildings[0].uuid, [fixture.buildings[0].uuid], {}, 6, fixture.layoutRevision, false, false, "", null, null, false] : []);
  const listeners = new Map();
  const fakeWindow = {
    getComputedStyle: () => ({ paddingLeft: "0", paddingTop: "0" }),
    setInterval: () => 1, clearInterval() {}, setTimeout: () => 1, clearTimeout() {},
    addEventListener: (name, handler) => listeners.set(name, handler),
    removeEventListener: (name) => listeners.delete(name), confirm,
  };
  const env = { window: fakeWindow, document: { activeElement: null } };
  let Component;
  if (original) {
    for (const name of ["te", "ne", "m", "re", "ie", "ae", "oe", "le", "g"]) env[name] = compile(originalSource, name, env);
    Object.assign(env, {
      p: { ...hook, useCallback: (fn) => fn }, h: jsx,
      s: () => ({ t: (key) => key, language: "en" }),
      se: { past: [], present: [], future: [] }, ce: 24, ee: () => null,
      f: () => Promise.resolve({ valid: true, issues: [], totalMoves: 1, temporaryMoves: 0 }),
    });
    Component = compile(originalSource, "ue", env);
  } else {
    const state = nodes(pages).find((node) => node.type === "VariableDeclarator" && node.id.name === "CITY_LAYOUT_PREVIEW_STATES");
    Object.assign(env, {
      h, Fragment, ...hook, ...contracts, useI18n: () => ({ t: (key) => key }),
      CITY_LAYOUT_PREVIEW_STATES: new Function(`return (${raw(pages, state.init)});`)(),
      // Execute the actual current keyboard effect; timers remain inert. Each
      // render replaces the captured listener. This is not a React scheduler.
      useEffect: (effect) => effect(),
    });
    Component = compile(pages, "CityLayoutPage", env);
  }
  const render = () => {
    hook.begin();
    const tree = Component(original ? { profileId: "inert", online: true, onLog() {} } : { previewState: moved ? "city-layout-populated-moved" : "city-layout-populated", online: false });
    const grid = flatten(tree).find((node) => /^city-layout-grid(?: dragging)?$/.test(node.props?.className));
    if (grid) grid.props.ref.current = { getBoundingClientRect: () => ({ left: 0, top: 0 }) };
    return tree;
  };
  return { hook, render, listeners, historyIndex: original ? 1 : 0 };
}

async function originalConfirmation() {
  let during;
  const original = runner(null, { original: true, moved: true, confirm: () => {
    const tree = original.render();
    during = { undoDisabled: button(tree, "cityLayout.undo").props.disabled, refreshDisabled: button(tree, "common.refresh").props.disabled, preparing: original.hook.values[15] };
    return false;
  } });
  original.hook.values[1].past = [[]];
  button(original.render(), "cityLayout.apply").props.onClick();
  await settle();
  return { during, afterCancelPreparing: original.hook.values[15], afterCancelPlacements: structuredClone(original.hook.values[1].present) };
}

async function localConfirmation(pages, keyboard = false) {
  const current = runner(pages, { moved: true });
  current.render();
  current.hook.values[0].past = [[]];
  button(current.render(), "cityLayout.apply").props.onClick();
  await settle();
  let tree = current.render();
  const before = structuredClone(current.hook.values[0].present);
  const during = { dialogOpen: hasDialog(tree), undoDisabled: button(tree, "cityLayout.undo").props.disabled, refreshDisabled: button(tree, "common.refresh").props.disabled };
  if (keyboard) current.listeners.get("keydown")({ key: "z", ctrlKey: true, metaKey: false, shiftKey: false, preventDefault() {} });
  else button(tree, "cityLayout.undo").props.onClick();
  tree = current.render();
  const afterUndo = structuredClone(current.hook.values[0].present);
  const dialogStillOpen = hasDialog(tree);
  const dialog = flatten(tree).find((node) => node.props?.role === "dialog");
  button(dialog, "common.cancel").props.onClick();
  tree = current.render();
  return { during, before, afterUndo, dialogStillOpen, afterCancel: { dialogOpen: hasDialog(tree), undoDisabled: button(tree, "cityLayout.undo").props.disabled, placements: structuredClone(current.hook.values[0].present) } };
}

function drag(pages, original = false, renderBetween = true) {
  const current = runner(pages, { original });
  let tree = current.render();
  flatten(tree).find((node) => node.type === "button" && node.props.title?.startsWith("Barracks")).props.onPointerDown(pointer(108, 36));
  tree = current.render();
  let grid = flatten(tree).find((node) => node.props?.className === "city-layout-grid dragging");
  grid.props.onPointerMove(pointer(180, 84));
  if (renderBetween) {
    tree = current.render();
    grid = flatten(tree).find((node) => node.props?.className === "city-layout-grid dragging");
  }
  grid.props.onPointerUp();
  current.render();
  return structuredClone(current.hook.values[current.historyIndex].present);
}

const original = await originalConfirmation();
const submitted = { confirmation: await localConfirmation(submittedPages), keyboard: await localConfirmation(submittedPages, true), noRenderDrag: drag(submittedPages, false, false), renderedDrag: drag(submittedPages) };
const current = { confirmation: await localConfirmation(currentPages), keyboard: await localConfirmation(currentPages, true), noRenderDrag: drag(currentPages, false, false), renderedDrag: drag(currentPages) };
const originalDrags = { noRenderDrag: drag(null, true, false), renderedDrag: drag(null, true, true) };
const report = {
  submittedRevision, original, submitted, current, originalDrags,
  sourceLocators: {
    busy: "CityLayoutPanel-DoNWkywK.js byte2648 U=!!ze||Oe; ct byte9199 ke(true), window.confirm, finally ke(false)",
    move: "CityLayoutPanel-DoNWkywK.js at byte8219 writes V.current=n synchronously before M(n)",
    release: "CityLayoutPanel-DoNWkywK.js ot byte8564 reads V.current synchronously",
  },
  limits: "Actual recovered/submitted/current callbacks in inert hooks. Keyboard effect executes with inert timers; original confirm is an injected observation callback returning false. No browser, native/gameplay/service, real confirmation or React scheduler is invoked. The no-render callback discrepancy is established; physical scheduler reachability is unproved. Run existing B checker separately for full drag/null/preparation regressions.",
};
assert.deepEqual(original.during, { undoDisabled: true, refreshDisabled: true, preparing: true });
assert.equal(original.afterCancelPreparing, false);
assert.equal(submitted.confirmation.during.undoDisabled, false);
assert.deepEqual(submitted.confirmation.afterUndo, []);
assert.deepEqual(submitted.keyboard.afterUndo, []);
assert.equal(submitted.confirmation.dialogStillOpen, true);
assert.deepEqual(submitted.noRenderDrag, []);
assert.deepEqual(originalDrags.noRenderDrag, [{ uuid: "preview-barracks", targetPointId: 505 }]);
assert.deepEqual(submitted.renderedDrag, originalDrags.renderedDrag);
assert.equal(current.confirmation.during.undoDisabled, true, "current must retain busy while confirmation is open");
assert.equal(current.confirmation.during.refreshDisabled, true);
assert.deepEqual(current.confirmation.afterUndo, current.confirmation.before);
assert.deepEqual(current.keyboard.afterUndo, current.keyboard.before);
assert.equal(current.confirmation.afterCancel.dialogOpen, false);
assert.equal(current.confirmation.afterCancel.undoDisabled, false);
assert.deepEqual(current.confirmation.afterCancel.placements, current.confirmation.before);
assert.deepEqual(current.noRenderDrag, originalDrags.noRenderDrag, "current release must consume the latest synchronously accepted target");
assert.deepEqual(current.renderedDrag, originalDrags.renderedDrag);
if (process.argv.includes("--record")) fs.writeFileSync(path.join(here, "lead-city-review-results.json"), `${JSON.stringify(report, null, 2)}\n`);
console.log(JSON.stringify(report, null, 2));
