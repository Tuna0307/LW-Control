globalThis.GameAssetImage = ()=>null; // Actual image nodes separately source/pixel-verified; historical predicates do not inspect asset rendering.
import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { compile, evaluate, flatten, hooks, jsx, nodes, raw, read, text } from "file:///C:/Users/chimw/OneDrive/Desktop/Github/LW-Control/evidence/lwbridge-0.3.17/ui/LWB317-UI-AUTOMATION-AFK-CLOSEOUT-001/harness.mjs";
import en from "file:///C:/Users/chimw/OneDrive/Desktop/Github/LW-Control/src/LWBridge.UI-0.3.17/src/locales/en.js";
import {
  MINI_GAME_HOTKEY_CARDS,
  formatSheepElapsed,
  previewHotkeyConfig,
  previewSheepStatus,
  sheepStatusKey,
} from "file:///C:/Users/chimw/OneDrive/Desktop/Github/LW-Control/src/LWBridge.UI-0.3.17/src/previewRemainingPagesContracts.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const source = read("evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/HotkeyPanel-XA8idRHB.js");
const pages = ["CityLayoutPage.jsx","HotkeyPages.jsx","SettingsPage.jsx","Pages.jsx"].map(file=>read("src/LWBridge.UI-0.3.17/src/"+file).replace(/^import .*?;\r?\n/gm,"" )).join("\n");
const contracts = read("src/LWBridge.UI-0.3.17/src/previewRemainingPagesContracts.js");
const hash = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
const t = (key, vars = {}) => String(en[key] ?? key).replace(/\{(\w+)\}/g, (match, name) => vars[name] ?? match);
const results = [];
const record = (name, count) => results.push({ name, count, result: "PASS" });

const originalStatusKey = compile(source, "h");
const originalElapsed = compile(source, "g");
const statusCases = [
  null,
  {},
  { running: true },
  { state: "error" },
  { step: "solving" },
  { step: "executing" },
  { step: "completed" },
  { step: "daily_limit" },
  { step: "all_completed" },
  { step: "activity_ended" },
  { step: "ui_open" },
  { step: "manual_or_state_conflict" },
  { step: "solve_failed" },
  { step: "unsupported_client" },
  { step: "refreshing" },
  { step: "starting" },
  { step: "opening" },
  { step: "initial_delay" },
];
for (const status of statusCases) assert.equal(sheepStatusKey(status), originalStatusKey(status), JSON.stringify(status));
record("exact recovered Sheep status-key precedence", statusCases.length);

const elapsedCases = [-1000, 0, 999, 1000, 59_999, 60_000, 3_599_000, 3_600_000, 3_661_999, 86_401_000];
for (const milliseconds of elapsedCases) assert.equal(formatSheepElapsed(milliseconds), originalElapsed(milliseconds), `${milliseconds}`);
record("exact recovered Sheep elapsed formatter", elapsedCases.length);

const stateCases = [
  ["mini-games-solving", "miniGames.sheep.solving"],
  ["mini-games-executing", "miniGames.sheep.executing"],
  ["mini-games-completed", "common.completed"],
  ["mini-games-complete", "miniGames.sheep.dailyLimit"],
  ["mini-games-all-complete", "miniGames.sheep.allCompleted"],
  ["mini-games-activity-ended", "miniGames.sheep.activityEnded"],
  ["mini-games-ui-open", "miniGames.sheep.uiOpen"],
  ["mini-games-conflict", "miniGames.sheep.conflict"],
  ["mini-games-solve-failed", "miniGames.sheep.solveFailed"],
  ["mini-games-unsupported", "miniGames.sheep.unsupported"],
  ["mini-games-refreshing", "common.processing"],
  ["mini-games-starting", "common.processing"],
  ["mini-games-opening", "common.processing"],
  ["mini-games-initial-delay", "common.processing"],
  ["mini-games-state-error", "common.failed"],
];
for (const [previewState, expectedKey] of stateCases) {
  const status = previewSheepStatus(previewState, 100_000);
  assert.equal(sheepStatusKey(status), expectedKey, previewState);
  assert.equal(sheepStatusKey(status), originalStatusKey(status), `${previewState} source differential`);
}
const active = previewSheepStatus("mini-games-active", 100_000);
assert.equal(active.running, true);
assert.equal(active.plannedMoves, 42);
assert.equal(active.confirmedMoves, 18);
assert.equal(100_000 - active.startedAt, 31_000);
record("source-shaped preview Sheep branch fixtures", stateCases.length * 2 + 4);

const miniDeclaration = nodes(source).find((node) => node.type === "VariableDeclarator" && node.id?.name === "m");
assert.ok(miniDeclaration?.init);
assert.deepEqual(MINI_GAME_HOTKEY_CARDS, evaluate(raw(source, miniDeclaration.init)));
record("exact recovered Frontline card catalog", 1);

function ToggleStub() {}
function SwitchStub() {}
function renderOriginal({ config, online, sheepStatus, seedOverrides = [] }) {
  const seed = [config, null, false, "", false, "", 100_000, ""];
  for (const [index, value] of seedOverrides) seed[index] = value;
  const hook = hooks(seed);
  const Panel = compile(source, "_", {
    d: hook,
    f: jsx,
    p: [],
    m: MINI_GAME_HOTKEY_CARDS,
    i: () => ({ t }),
    l: SwitchStub,
    s: ToggleStub,
    r: async () => config,
    c: async (next) => next,
    a: async () => ({ cellId: 17, cellType: "preview" }),
    o: async () => {},
    u: async () => {},
    h: originalStatusKey,
    g: originalElapsed,
    window: { setInterval: () => 1, clearInterval: () => {} },
  });
  hook.begin();
  return Panel({ category: "miniGames", online, onLog: () => {}, sheepStatus });
}

const base = previewHotkeyConfig();
let tree = renderOriginal({ config: base, online: true, sheepStatus: active });
let articles = flatten(tree).filter((node) => node.type === "article");
assert.equal(articles.length, 4);
assert.ok(text(tree).includes(t("hotkeys.frontlineReinforce.title")));
assert.ok(text(tree).includes(t("miniGames.sheep.progress", { confirmed: 18, total: 42 })));
assert.ok(text(tree).includes(t("miniGames.sheep.elapsed", { time: "0:31" })));
const treasureToggle = flatten(tree).find((node) => node.type === ToggleStub);
assert.equal(treasureToggle.props.disabled, false);
const buttons = flatten(tree).filter((node) => node.type === "button");
const landButton = buttons.find((node) => text(node) === t("miniGames.landCellAction"));
const stopButton = buttons.find((node) => text(node) === t("common.stop"));
assert.equal(landButton.props.disabled, false);
assert.equal(stopButton.props.disabled, false);
assert.equal(stopButton.props.className, "");
record("actual recovered Mini renderer populated/running branch", 9);

tree = renderOriginal({ config: null, online: false, sheepStatus: null });
articles = flatten(tree).filter((node) => node.type === "article");
assert.equal(articles.length, 3);
assert.ok(!text(tree).includes(t("hotkeys.frontlineReinforce.title")));
const offlineToggle = flatten(tree).find((node) => node.type === ToggleStub);
assert.equal(offlineToggle.props.disabled, true);
assert.ok(text(tree).includes(t("common.stopped")));
assert.ok(text(tree).includes(t("hotkeys.offlineHint")));
record("actual recovered Mini renderer config-null/offline branch", 5);

const sourceGuards = [
  'category="miniGames"',
  "!online || !hotkeyConfig || pendingField !== null",
  'window.setInterval(() => setNow(Date.now()), 1000)',
  "window.clearInterval(interval)",
  'landBusy ? "miniGames.landCellOpening" : "miniGames.landCellAction"',
  'sheepStatus?.step === "completed" ? "hotkey-state enabled" : "hotkey-state"',
  'sheepBusy ? "common.processing" : sheepRunning ? "common.stop" : "common.start"',
  "sheepActionError",
];
for (const guard of sourceGuards) assert.ok(pages.includes(guard), `Missing production guard: ${guard}`);
assert.ok(!pages.includes('className={foodRunning ? "danger" : "primary"}'));
record("production shared Mini ownership/timer/action source guards", sourceGuards.length + 1);

const report = {
  result: "LWB317_MINI_GAMES_SOURCE_LOCAL_OK",
  results,
  current: { pagesSha256: hash(pages), contractsSha256: hash(contracts) },
  limits: "Recovered Sheep helpers and the original shared Mini Games renderer are executed directly. Current production actions are preview-local acknowledgements only; no native Land unlock, Sheep start/stop, gameplay producer or game-window input is invoked.",
};
if (process.argv.includes("--record")) fs.writeFileSync(path.join(here, "results.json"), `${JSON.stringify(report, null, 2)}\n`);
console.log(JSON.stringify(report, null, 2));
