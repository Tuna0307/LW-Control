globalThis.GameAssetImage = ()=>null; // Actual image nodes separately source/pixel-verified; historical predicates do not inspect asset rendering.
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { Fragment, compile, flatten, h, hooks, nodes, raw, read, text } from "file:///C:/Users/chimw/OneDrive/Desktop/Github/LW-Control/evidence/lwbridge-0.3.17/ui/LWB317-UI-AUTOMATION-AFK-CLOSEOUT-001/harness.mjs";
import * as contracts from "file:///C:/Users/chimw/OneDrive/Desktop/Github/LW-Control/src/LWBridge.UI-0.3.17/src/previewRemainingPagesContracts.js";
import en from "file:///C:/Users/chimw/OneDrive/Desktop/Github/LW-Control/src/LWBridge.UI-0.3.17/src/locales/en.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const pages = ["CityLayoutPage.jsx","HotkeyPages.jsx","SettingsPage.jsx","Pages.jsx"].map(file=>read("src/LWBridge.UI-0.3.17/src/"+file).replace(/^import .*?;\r?\n/gm,"" )).join("\n");
const t = (key, vars = {}) => String(en[key] ?? key).replace(/\{(\w+)\}/g, (match, name) => String(vars[name] ?? match));
const settle = () => new Promise((resolve) => setImmediate(resolve));
const stateSet = (name) => new Function(`return (${raw(pages, nodes(pages).find((node) => node.type === "VariableDeclarator" && node.id.name === name).init)});`)();
const fakeWindow = { setInterval: () => 1, clearInterval() {}, getComputedStyle: () => ({ paddingLeft: "0", paddingTop: "0" }) };
const results = [];
const record = (name, cases) => results.push({ name, cases, result: "PASS" });

function renderMini(props) {
  const hook = hooks();
  const Component = compile(pages, "RecoveredHotkeyPanel", {
    h, Fragment, ...hook, ...contracts,
    useI18n: () => ({ t }),
    MINI_GAMES_PREVIEW_STATES: stateSet("MINI_GAMES_PREVIEW_STATES"),
    HotkeyCard: "HotkeyCard",
    ToggleRow: "ToggleRow",
    PanelTitle: "PanelTitle",
    window: fakeWindow,
  });
  const render = () => { hook.begin(); return Component({ category: "miniGames", ...props }); };
  return { hook, render };
}

const miniUnavailableCases = [
  ["preview-empty", { previewState: "", online: false, bridgeMode: "preview", backendAvailable: false }],
  ["native-connected", { previewState: "", online: true, bridgeMode: "native", backendAvailable: true }],
  ["native-unavailable", { previewState: "", online: false, bridgeMode: "native-unavailable", backendAvailable: false }],
  ["unrecognized-fixture", { previewState: "mini-games-unknown", online: true, bridgeMode: "preview", backendAvailable: false }],
];
for (const [name, props] of miniUnavailableCases) {
  const runner = renderMini(props);
  let tree = runner.render();
  assert.equal(tree.props["data-preview-fixture"], undefined, name);
  const land = flatten(tree).find((node) => node.type === "button" && text(node) === t("miniGames.landCellAction"));
  const sheep = flatten(tree).find((node) => node.type === "button" && text(node) === t("common.start"));
  assert.equal(land.props.disabled, true, `${name} Land disabled`);
  assert.equal(sheep.props.disabled, true, `${name} Sheep disabled`);
  assert.ok(!text(tree).includes(t("miniGames.sheep.level", { level: 4 })), `${name} no synthetic level`);
  land.props.onClick();
  sheep.props.onClick();
  await settle();
  tree = runner.render();
  assert.ok(!text(tree).includes(t("miniGames.landCellSent", { id: 17 })), `${name} direct Land callback inert`);
  assert.ok(!text(tree).includes(t("miniGames.sheep.failed")), `${name} direct Sheep callback inert`);
}
record("Mini Games unavailable-provider boundaries", miniUnavailableCases.map(([name]) => name));

let runner = renderMini({ previewState: "mini-games-active", online: false, bridgeMode: "preview", backendAvailable: false });
let tree = runner.render();
let land = flatten(tree).find((node) => node.type === "button" && text(node) === t("miniGames.landCellAction"));
assert.equal(land.props.disabled, false);
assert.ok(text(tree).includes(t("miniGames.sheep.level", { level: 4 })));
land.props.onClick();
await settle();
tree = runner.render();
assert.ok(text(tree).includes(t("miniGames.landCellSent", { id: 17 })));
runner = renderMini({ previewState: "mini-games-start-failed", online: false, bridgeMode: "preview", backendAvailable: false });
tree = runner.render();
let sheep = flatten(tree).find((node) => node.type === "button" && text(node) === t("common.start"));
assert.equal(sheep.props.disabled, false);
sheep.props.onClick();
await settle();
tree = runner.render();
assert.ok(text(tree).includes(t("miniGames.sheep.failed")));
record("Mini Games recognized preview acknowledgements", ["Land success", "Sheep controlled failure"]);

const initialFeedbackState = compile(pages, "initialFeedbackState");
function renderSettings(props) {
  const hook = hooks();
  const Component = compile(pages, "SettingsPage", {
    h, Fragment, ...hook, ...contracts,
    useI18n: () => ({ language: "en", t }),
    SETTINGS_PREVIEW_STATES: stateSet("SETTINGS_PREVIEW_STATES"),
    initialFeedbackState,
    PanelTitle: "PanelTitle",
    ToggleRow: "ToggleRow",
    window: fakeWindow,
  });
  const render = () => { hook.begin(); return Component(props); };
  return { hook, render };
}

const settingsUnavailableCases = [
  ["preview-empty", { previewState: "", bridgeMode: "preview", backendAvailable: false, online: false }],
  ["native-connected", { previewState: "", bridgeMode: "native", backendAvailable: true, online: true }],
  ["native-unavailable", { previewState: "", bridgeMode: "native-unavailable", backendAvailable: false, online: false }],
  ["unrecognized-fixture", { previewState: "settings-unknown", bridgeMode: "preview", backendAvailable: false, online: false }],
];
for (const [name, props] of settingsUnavailableCases) {
  const settings = renderSettings(props);
  let current = settings.render();
  assert.equal(current.props["data-preview-fixture"], undefined, name);
  const exportButton = flatten(current).find((node) => node.type === "button" && text(node) === t("feedback.export"));
  const checkButton = flatten(current).find((node) => node.type === "button" && text(node) === t("update.check"));
  assert.equal(exportButton.props.disabled, true, `${name} export disabled`);
  assert.equal(checkButton.props.disabled, true, `${name} update disabled`);
  assert.deepEqual(settings.hook.values[5], { phase: "idle", result: null, progress: null }, `${name} feedback initial`);
  assert.deepEqual(settings.hook.values[6], contracts.emptyUpdateStatus(), `${name} original empty updater Ln`);
  exportButton.props.onClick();
  checkButton.props.onClick();
  await settle();
  settings.render();
  assert.deepEqual(settings.hook.values[5], { phase: "idle", result: null, progress: null }, `${name} direct export inert`);
  assert.deepEqual(settings.hook.values[6], contracts.emptyUpdateStatus(), `${name} direct update inert`);
}
assert.deepEqual(contracts.previewUpdateStatus(""), contracts.emptyUpdateStatus());
record("Settings unavailable-provider and initial Ln boundaries", settingsUnavailableCases.map(([name]) => name));

runner = renderSettings({ previewState: "settings-feedback-canceled", bridgeMode: "preview", backendAvailable: false, online: false });
tree = runner.render();
let button = flatten(tree).find((node) => node.type === "button" && text(node) === t("feedback.export"));
assert.equal(button.props.disabled, false);
button.props.onClick();
await settle();
tree = runner.render();
assert.equal(runner.hook.values[5].phase, "idle");
runner = renderSettings({ previewState: "settings-update-idle", bridgeMode: "preview", backendAvailable: false, online: false });
tree = runner.render();
button = flatten(tree).find((node) => node.type === "button" && text(node) === t("update.check"));
assert.equal(button.props.disabled, false);
button.props.onClick();
await settle();
runner.render();
assert.equal(runner.hook.values[6].phase, "upToDate");
runner = renderSettings({ previewState: "settings-update-available", bridgeMode: "preview", backendAvailable: false, online: false });
tree = runner.render();
button = flatten(tree).find((node) => node.type === "button" && text(node) === t("update.downloadAndOpen"));
button.props.onClick();
await settle();
runner.render();
assert.equal(runner.hook.values[6].phase, "opening");
record("Settings recognized preview acknowledgements", ["feedback cancel", "update check", "download/open preview"]);

function Stub() {}
const Route = compile(pages, "PageForRoute", {
  h,
  HomePage: Stub, LazyHotkeyPanel: Stub, LazySettingsPage: Stub,
  AutomationPage: Stub,
  getMapPreviewProvider: () => null,
  MapDataPage: Stub,
  SquadsPage: Stub,
  CityLayoutPage: Stub,
  HotkeysPage: Stub,
  MiniGamesPage: Stub,
  SettingsPage: Stub,
});
for (const routeKey of ["mini-games", "settings"]) {
  const node = Route({ routeKey, bridgeMode: "native", backendAvailable: true, online: true, previewState: "" });
  assert.equal(node.props.bridgeMode, "native");
  assert.equal(node.props.backendAvailable, true);
  assert.equal(node.props.online, true);
  assert.equal(node.props.previewState, "");
}
record("PageForRoute runtime prop forwarding", ["mini-games", "settings"]);

const report = {
  workItem: "LWB317-UI-REMAINING-PAGES-001-R1",
  milestone: "A",
  result: "PASS",
  results,
  originalContract: {
    miniLandSheepRequireProviders: true,
    settingsFeedbackUpdaterRequireProviders: true,
    updateInitial: contracts.emptyUpdateStatus(),
  },
  submittedBaseline: "Preserved in ../LWB317-REVIEW-REMAINING-PAGES-001/independent-results.json; native/default callbacks fabricated success and Sheep/updater state.",
  limits: "Recognized preview fixtures exercise controlled synthetic acknowledgements. Native/default/unrecognized paths have no provider and remain inert. No archive, updater, game input, native provider, network, or gameplay action is invoked.",
};
if (process.argv.includes("--record")) fs.writeFileSync(path.join(here, "results.json"), `${JSON.stringify(report, null, 2)}\n`);
console.log(JSON.stringify(report, null, 2));
