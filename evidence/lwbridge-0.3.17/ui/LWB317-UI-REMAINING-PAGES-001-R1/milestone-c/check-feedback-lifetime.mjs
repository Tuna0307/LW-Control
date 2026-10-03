import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { Fragment, compile, flatten, h, hooks, nodes, raw, read, text } from "../../LWB317-UI-AUTOMATION-AFK-CLOSEOUT-001/harness.mjs";
import * as contracts from "../../../../../src/LWBridge.UI-0.3.17/src/previewRemainingPagesContracts.js";
import en from "../../../../../src/LWBridge.UI-0.3.17/src/locales/en.js";
import ja from "../../../../../src/LWBridge.UI-0.3.17/src/locales/ja.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const pages = read("src/LWBridge.UI-0.3.17/src/Pages.jsx");
const hotkeySource = read("evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/HotkeyPanel-XA8idRHB.js");
const settingsSource = read("evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/SettingsPanel-DqxIWv_E.js");
const tFor = (catalog) => (key, vars = {}) => String(catalog[key] ?? key).replace(/\{(\w+)\}/g, (match, name) => String(vars[name] ?? match));
const stateSet = (name) => new Function(`return (${raw(pages, nodes(pages).find((node) => node.type === "VariableDeclarator" && node.id.name === name).init)});`)();
const settle = () => new Promise((resolve) => setImmediate(resolve));
const fakeWindow = { setInterval: () => 1, clearInterval() {} };
const results = [];
const record = (name, cases) => results.push({ name, cases, result: "PASS" });

for (const sourceGuard of [
  "F(v(`hotkeys.loadFailed`))",
  "F(v(`hotkeys.saveFailed`))",
  "D(v(`miniGames.landCellSent`,{id:e.cellId}))",
  "F(v(`miniGames.landCellFailed`))",
  "j(v(`miniGames.sheep.failed`))",
]) assert.ok(hotkeySource.includes(sourceGuard), `missing recovered Hotkey producer ${sourceGuard}`);
for (const sourceGuard of [
  "_(i(`settings.visualMetrics.loadFailed`))",
  "_(i(`settings.visualMetrics.saveFailed`))",
]) assert.ok(settingsSource.includes(sourceGuard), `missing recovered Settings producer ${sourceGuard}`);
record("recovered producers translate before storing retained feedback", ["Hotkey load/save", "Land success/failure", "Sheep failure", "Settings visual load/save"]);

function hotkeyRunner(previewState, category = "hotkeys") {
  let locale = "en";
  const hook = hooks();
  const Component = compile(pages, "RecoveredHotkeyPanel", {
    h, Fragment, ...hook, ...contracts,
    useI18n: () => ({ t: tFor(locale === "en" ? en : ja) }),
    MINI_GAMES_PREVIEW_STATES: stateSet("MINI_GAMES_PREVIEW_STATES"),
    HotkeyCard: "Card", ToggleRow: "ToggleRow", PanelTitle: "PanelTitle", window: fakeWindow,
  });
  const render = () => { hook.begin(); return Component({ category, previewState, online: false }); };
  return { hook, render, setLocale: (next) => { locale = next; } };
}

function settingsRunner(previewState) {
  let locale = "en";
  const hook = hooks();
  const Component = compile(pages, "SettingsPage", {
    h, Fragment, ...hook, ...contracts,
    useI18n: () => ({ language: locale, t: tFor(locale === "en" ? en : ja) }),
    SETTINGS_PREVIEW_STATES: stateSet("SETTINGS_PREVIEW_STATES"),
    initialFeedbackState: compile(pages, "initialFeedbackState"),
    PanelTitle: "PanelTitle", ToggleRow: "ToggleRow", window: fakeWindow,
  });
  const render = () => { hook.begin(); return Component({ previewState, showProfileFocus: false, focusGameOnProfileSelect: true }); };
  return { hook, render, setLocale: (next) => { locale = next; } };
}

let runner = hotkeyRunner("hotkeys-save-error");
let tree = runner.render();
let card = flatten(tree).find((node) => node.type === "Card");
card.props.onSaveField({ ...card.props.config, attack: !card.props.config.attack }, "attack");
await settle();
tree = runner.render();
assert.equal(text(flatten(tree).find((node) => node.props?.role === "alert")), en["hotkeys.saveFailed"]);
runner.setLocale("ja");
tree = runner.render();
assert.equal(text(flatten(tree).find((node) => node.props?.role === "alert")), en["hotkeys.saveFailed"]);
card = flatten(tree).find((node) => node.type === "Card");
card.props.onSaveField({ ...card.props.config, attack: !card.props.config.attack }, "attack");
await settle();
tree = runner.render();
assert.equal(text(flatten(tree).find((node) => node.props?.role === "alert")), ja["hotkeys.saveFailed"]);
record("Hotkeys event-time save failure lifetime", ["EN failure", "JA rerender retains EN", "new JA failure stores JA"]);

runner = settingsRunner("settings-visual-save-error");
tree = runner.render();
let toggle = flatten(tree).find((node) => node.type === "ToggleRow" && node.props.label === en["settings.visualMetrics.showFps"]);
toggle.props.onChange(true);
await settle();
tree = runner.render();
assert.equal(text(flatten(tree).find((node) => node.props?.role === "alert")), en["settings.visualMetrics.saveFailed"]);
runner.setLocale("ja");
tree = runner.render();
assert.equal(text(flatten(tree).find((node) => node.props?.role === "alert")), en["settings.visualMetrics.saveFailed"]);
toggle = flatten(tree).find((node) => node.type === "ToggleRow" && node.props.label === ja["settings.visualMetrics.showFps"]);
toggle.props.onChange(true);
await settle();
tree = runner.render();
assert.equal(text(flatten(tree).find((node) => node.props?.role === "alert")), ja["settings.visualMetrics.saveFailed"]);
record("Settings event-time save failure lifetime", ["EN failure", "JA rerender retains EN", "new JA failure stores JA"]);

for (const [state, category, key] of [
  ["hotkeys-load-error", "hotkeys", "hotkeys.loadFailed"],
  ["mini-games-load-error", "miniGames", "hotkeys.loadFailed"],
  ["mini-games-land-error", "miniGames", "miniGames.landCellFailed"],
  ["mini-games-start-failed", "miniGames", "miniGames.sheep.failed"],
]) {
  runner = hotkeyRunner(state, category);
  tree = runner.render();
  let alert = flatten(tree).find((node) => node.props?.role === "alert");
  assert.equal(text(alert), en[key], `${state} EN`);
  runner.setLocale("ja");
  tree = runner.render();
  alert = flatten(tree).find((node) => node.props?.role === "alert");
  assert.equal(text(alert), en[key], `${state} retained`);
}
runner = settingsRunner("settings-visual-error");
tree = runner.render();
assert.equal(text(flatten(tree).find((node) => node.props?.role === "alert")), en["settings.visualMetrics.loadFailed"]);
runner.setLocale("ja");
tree = runner.render();
assert.equal(text(flatten(tree).find((node) => node.props?.role === "alert")), en["settings.visualMetrics.loadFailed"]);
record("retained load/action failure fields do not retroactively retranslate", ["Hotkey load", "Mini config load", "Land failure", "Sheep failure", "Settings visual load"]);

runner = hotkeyRunner("mini-games-active", "miniGames");
tree = runner.render();
let landButton = flatten(tree).filter((node) => node.type === "button").find((node) => text(node) === en["miniGames.landCellAction"]);
landButton.props.onClick();
await settle();
tree = runner.render();
assert.ok(text(tree).includes(tFor(en)("miniGames.landCellSent", { id: 17 })));
runner.setLocale("ja");
tree = runner.render();
assert.ok(text(tree).includes(tFor(en)("miniGames.landCellSent", { id: 17 })));
landButton = flatten(tree).filter((node) => node.type === "button").find((node) => text(node) === ja["miniGames.landCellAction"]);
landButton.props.onClick();
await settle();
tree = runner.render();
assert.ok(text(tree).includes(tFor(ja)("miniGames.landCellSent", { id: 17 })));
record("Mini Land success result lifetime", ["EN success", "JA rerender retains EN", "new JA success stores JA"]);

const report = {
  workItem: "LWB317-UI-REMAINING-PAGES-001-R1",
  milestone: "C",
  result: "PASS",
  results,
  renderTranslatedStructuralState: ["Sheep status/progress", "diagnostic export phase/result formatting", "updater message codes"],
  submittedBaseline: "Lead packet at afd65b6 records Hotkeys and Settings EN failure becoming JA on locale rerender; historical packet remains immutable.",
  limits: "All callbacks are inert preview/rejected-save transitions. No native preference write, archive, updater, OS hotkey, gameplay, network, or service action is invoked."
};
if (process.argv.includes("--record")) fs.writeFileSync(path.join(here, "results.json"), `${JSON.stringify(report, null, 2)}\n`);
console.log(JSON.stringify(report, null, 2));
