globalThis.GameAssetImage = ()=>null; // Actual image nodes separately source/pixel-verified; historical predicates do not inspect asset rendering.
import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { Fragment, compile, flatten, fn, h, hooks, raw, read, text } from "file:///C:/Users/chimw/OneDrive/Desktop/Github/LW-Control/evidence/lwbridge-0.3.17/ui/LWB317-UI-AUTOMATION-AFK-CLOSEOUT-001/harness.mjs";
import en from "file:///C:/Users/chimw/OneDrive/Desktop/Github/LW-Control/src/LWBridge.UI-0.3.17/src/locales/en.js";
import ja from "file:///C:/Users/chimw/OneDrive/Desktop/Github/LW-Control/src/LWBridge.UI-0.3.17/src/locales/ja.js";
import {
  CITY_BASE_CELL_SIZE,
  cityGridRow,
  cityLayoutIssues,
  cityMovedOccupiedPoints,
  cityPlacementMap,
  cityPlacementSignature,
  cityRegionForPoint,
  citySetPlacement,
  previewCityLayoutFixture,
} from "file:///C:/Users/chimw/OneDrive/Desktop/Github/LW-Control/src/LWBridge.UI-0.3.17/src/previewRemainingPagesContracts.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const pages = ["CityLayoutPage.jsx","HotkeyPages.jsx","SettingsPage.jsx","Pages.jsx"].map(file=>read("src/LWBridge.UI-0.3.17/src/"+file).replace(/^import .*?;\r?\n/gm,"" )).join("\n");
const citySource = read("evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/CityLayoutPanel-DoNWkywK.js");
const currentContracts = read("src/LWBridge.UI-0.3.17/src/previewRemainingPagesContracts.js");
const tFor = (catalog) => (key, vars = {}) => String(catalog[key] ?? key).replace(/\{(\w+)\}/g, (match, name) => vars[name] ?? match);
const hash = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
const results = [];
const record = (name, count) => results.push({ name, count, result: "PASS" });

const originalPlacementMap = compile(citySource, "te");
const originalSetPlacement = compile(citySource, "ne");
const originalMovedOccupied = compile(citySource, "m");
const originalIssues = compile(citySource, "re", { te: originalPlacementMap, m: originalMovedOccupied });
const originalRegion = compile(citySource, "ie");
const originalGridRow = compile(citySource, "oe");

const fixture = previewCityLayoutFixture("city-layout-populated");
const cellsByPoint = new Map(fixture.cells.map((cell) => [cell.pointId, cell]));
const cellsByCoordinate = new Map(fixture.cells.map((cell) => [`${cell.x}:${cell.y}`, cell]));

let helperCases = 0;
for (const building of fixture.buildings) {
  for (const cell of fixture.cells) {
    assert.deepEqual(
      cityMovedOccupiedPoints(building, cell.pointId, cellsByPoint, cellsByCoordinate),
      originalMovedOccupied(building, cell.pointId, cellsByPoint, cellsByCoordinate),
      `occupied points ${building.uuid} -> ${cell.pointId}`,
    );
    helperCases += 1;
    const currentPlacement = citySetPlacement([], building, cell.pointId);
    const originalPlacement = originalSetPlacement([], building, cell.pointId);
    assert.deepEqual(currentPlacement, originalPlacement, `placement ${building.uuid} -> ${cell.pointId}`);
    assert.deepEqual(cityLayoutIssues(fixture, currentPlacement), originalIssues(fixture, originalPlacement), `issues ${building.uuid} -> ${cell.pointId}`);
    helperCases += 2;
  }
}
assert.deepEqual([...cityPlacementMap([{ uuid: "b", targetPointId: 2 }, { uuid: "a", targetPointId: 1 }])], [...originalPlacementMap([{ uuid: "b", targetPointId: 2 }, { uuid: "a", targetPointId: 1 }])]); helperCases += 1;
for (const cell of fixture.cells) {
  assert.equal(cityRegionForPoint(fixture, cell.pointId), originalRegion(fixture, cell.pointId));
  assert.equal(cityGridRow(fixture.regions[0].bounds, cell.y), originalGridRow(fixture.regions[0].bounds, cell.y));
  helperCases += 2;
}
record("actual recovered/current placement translation validation region and grid helpers", helperCases);

let issueCases = 0;
const byUuid = new Map(fixture.buildings.map((building) => [building.uuid, building]));
const scenarios = [
  ["initial", []],
  ["valid move", citySetPlacement([], byUuid.get("preview-hospital"), 405)],
  ["overlap", citySetPlacement([], byUuid.get("preview-hospital"), 705)],
  ["road", citySetPlacement([], byUuid.get("preview-hospital"), 401)],
  ["locked", citySetPlacement([], byUuid.get("preview-hospital"), 103)],
  ["flag only non-flag", citySetPlacement([], byUuid.get("preview-hospital"), 407)],
  ["flag allowed", citySetPlacement([], byUuid.get("preview-flag"), 408)],
  ["immovable", citySetPlacement([], byUuid.get("preview-hq"), 706)],
  ["outside", citySetPlacement([], byUuid.get("preview-barracks"), 708)],
];
for (const [name, placements] of scenarios) {
  const actual = cityLayoutIssues(fixture, placements);
  const expected = originalIssues(fixture, placements);
  assert.deepEqual(actual, expected, name);
  issueCases += 1;
}
assert.equal(cityLayoutIssues(fixture, scenarios[1][1]).length, 0);
assert.ok(cityLayoutIssues(fixture, scenarios[2][1]).some((entry) => entry.code === "CITY_LAYOUT_OVERLAP"));
assert.ok(cityLayoutIssues(fixture, scenarios[3][1]).some((entry) => entry.code === "CITY_LAYOUT_ROAD"));
assert.ok(cityLayoutIssues(fixture, scenarios[4][1]).some((entry) => entry.code === "CITY_LAYOUT_LOCKED"));
assert.ok(cityLayoutIssues(fixture, scenarios[5][1]).some((entry) => entry.code === "CITY_LAYOUT_FLAG_ONLY"));
assert.ok(cityLayoutIssues(fixture, scenarios[7][1]).some((entry) => entry.code === "CITY_LAYOUT_IMMOVABLE"));
assert.ok(cityLayoutIssues(fixture, scenarios[8][1]).some((entry) => entry.code === "CITY_LAYOUT_OUTSIDE"));
issueCases += 7;
record("source-equivalent valid overlap road locked flag immovable and outside scenarios", issueCases);

const previewStates = new Set([
  "city-layout-populated", "city-layout-populated-conflict", "city-layout-populated-moved", "city-layout-stale",
  "city-layout-outside-city", "city-layout-server-valid", "city-layout-applying", "city-layout-loading", "city-layout-error",
]);

function renderCity(state, catalog = en) {
  const hook = hooks();
  const listeners = new Map();
  const timers = [];
  const fakeWindow = {
    setTimeout: (callback, ms) => { timers.push({ callback, ms }); return timers.length; },
    clearTimeout() {},
    addEventListener: (name, callback) => listeners.set(name, callback),
    removeEventListener: (name) => listeners.delete(name),
    getComputedStyle: () => ({ paddingLeft: "0", paddingTop: "0" }),
  };
  const component = compile(pages, "CityLayoutPage", {
    h, Fragment, ...hook,
    useI18n: () => ({ t: tFor(catalog) }),
    CITY_LAYOUT_PREVIEW_STATES: previewStates,
    CITY_BASE_CELL_SIZE,
    cityGridRow,
    cityLayoutIssues,
    cityMovedOccupiedPoints,
    cityPlacementMap,
    cityPlacementSignature,
    cityRegionForPoint,
    citySetPlacement,
    previewCityLayoutFixture,
    window: fakeWindow,
    document: { activeElement: null },
  });
  const render = () => { hook.begin(); return component({ previewState: state, online: false }); };
  return { hook, render, listeners, timers };
}

let renderCases = 0;
for (const catalog of [en, ja]) {
  let runner = renderCity("city-layout-populated-moved", catalog);
  let tree = runner.render();
  assert.ok(text(tree).includes(tFor(catalog)("cityLayout.title")));
  assert.ok(text(tree).includes("100%"));
  const buttons = flatten(tree).filter((node) => node.type === "button");
  const restore = buttons.find((node) => text(node) === tFor(catalog)("cityLayout.restoreInitial"));
  assert.equal(restore.props.disabled, false);
  restore.props.onClick();
  tree = runner.render();
  assert.ok(text(tree).includes(tFor(catalog)("cityLayout.noChanges")));
  renderCases += 4;

  runner = renderCity("city-layout-stale", catalog);
  tree = runner.render();
  assert.ok(text(tree).includes(tFor(catalog)("cityLayout.stale")));
  const apply = flatten(tree).filter((node) => node.type === "button").find((node) => text(node) === tFor(catalog)("cityLayout.apply"));
  assert.equal(apply.props.disabled, true);
  renderCases += 2;

  runner = renderCity("city-layout-applying", catalog);
  tree = runner.render();
  assert.ok(flatten(tree).some((node) => node.props?.className === "city-layout-progress"));
  assert.ok(text(tree).includes(tFor(catalog)("cityLayout.applyProgress", { current: 2, total: 4 })));
  renderCases += 2;
}
record("actual production callbacks and supplied stale/progress presentation in English/Japanese", renderCases);

const movedRunner = renderCity("city-layout-populated-moved");
let movedTree = movedRunner.render();
const zoomGroup = flatten(movedTree).find((node) => node.props?.className === "city-layout-zoom");
const zoomButtons = flatten(zoomGroup).filter((node) => node.type === "button");
zoomButtons[1].props.onClick();
movedTree = movedRunner.render();
assert.ok(text(movedTree).includes("117%"));
const rerenderedZoomGroup = flatten(movedTree).find((node) => node.props?.className === "city-layout-zoom");
flatten(rerenderedZoomGroup).filter((node) => node.type === "button")[0].props.onClick();
movedTree = movedRunner.render();
assert.ok(text(movedTree).includes("100%"));
record("production zoom callbacks use recovered 4px step and 24px percentage base", 2);

const cityPageSource = raw(pages, fn(pages, "CityLayoutPage"));
assert.ok(cityPageSource.includes("window.setTimeout(() =>"));
assert.ok(cityPageSource.includes("}, 500);"));
assert.ok(cityPageSource.includes("window.removeEventListener(\"keydown\", onKeyDown)"));
assert.ok(!cityPageSource.includes("window.confirm("));
record("source-located 500ms draft debounce, key cleanup, and native-confirm fence", 4);

const report = {
  result: "LWB317_CITY_LAYOUT_SOURCE_LOCAL_OK",
  results,
  current: {
    pagesSha256: hash(pages),
    contractsSha256: hash(currentContracts),
  },
  limits: "Recovered helper functions are executed directly against the source-shaped fixture and compared to production helpers. Browser/native-provider proof is separate. No native city-layout provider, persistence, apply job, gameplay action, or original post-auth pixel comparison is exercised.",
};
if (process.argv.includes("--record")) fs.writeFileSync(path.join(here, "results.json"), `${JSON.stringify(report, null, 2)}\n`);
console.log(JSON.stringify(report, null, 2));
