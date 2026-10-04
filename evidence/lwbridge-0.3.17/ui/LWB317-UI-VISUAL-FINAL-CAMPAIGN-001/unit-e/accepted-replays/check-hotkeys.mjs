globalThis.GameAssetImage = ()=>null; // Actual image nodes separately source/pixel-verified; historical predicates do not inspect asset rendering.
import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { Fragment, compile, evaluate, flatten, h, hooks, jsx, nodes, raw, read, text } from "file:///C:/Users/chimw/OneDrive/Desktop/Github/LW-Control/evidence/lwbridge-0.3.17/ui/LWB317-UI-AUTOMATION-AFK-CLOSEOUT-001/harness.mjs";
import en from "file:///C:/Users/chimw/OneDrive/Desktop/Github/LW-Control/src/LWBridge.UI-0.3.17/src/locales/en.js";
import ja from "file:///C:/Users/chimw/OneDrive/Desktop/Github/LW-Control/src/LWBridge.UI-0.3.17/src/locales/ja.js";
import { HOTKEY_CARDS, MINI_GAME_HOTKEY_CARDS, mergeHotkeyField, previewHotkeyConfig } from "file:///C:/Users/chimw/OneDrive/Desktop/Github/LW-Control/src/LWBridge.UI-0.3.17/src/previewRemainingPagesContracts.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const source = read("evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/HotkeyPanel-XA8idRHB.js");
const pages = ["CityLayoutPage.jsx","HotkeyPages.jsx","SettingsPage.jsx","Pages.jsx"].map(file=>read("src/LWBridge.UI-0.3.17/src/"+file).replace(/^import .*?;\r?\n/gm,"" )).join("\n");
const contracts = read("src/LWBridge.UI-0.3.17/src/previewRemainingPagesContracts.js");
const hash = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
const tFor = (catalog) => (key, vars = {}) => String(catalog[key] ?? key).replace(/\{(\w+)\}/g, (match, name) => vars[name] ?? match);
const results = [];
const record = (name, count) => results.push({ name, count, result: "PASS" });

function recoveredArray(name) {
  const declaration = nodes(source).find((node) => node.type === "VariableDeclarator" && node.id?.name === name);
  assert.ok(declaration?.init, `Missing original array ${name}`);
  return evaluate(raw(source, declaration.init));
}

const originalCards = recoveredArray("p");
const originalMiniCards = recoveredArray("m");
assert.deepEqual(HOTKEY_CARDS, originalCards);
assert.deepEqual(MINI_GAME_HOTKEY_CARDS, originalMiniCards);
record("exact recovered shortcut and shared Mini Games card catalogs", HOTKEY_CARDS.length + MINI_GAME_HOTKEY_CARDS.length);

const base = previewHotkeyConfig();
const fresh = { ...base, recall: false, previewUnrelatedField: "fresh-server-value", externalRevision: 12 };
const optimistic = { ...base, attack: false, recall: true, previewUnrelatedField: "stale-client-value" };
const merged = mergeHotkeyField(fresh, optimistic, "attack");
assert.equal(merged.attack, false);
assert.equal(merged.recall, false);
assert.equal(merged.previewUnrelatedField, "fresh-server-value");
assert.equal(merged.externalRevision, 12);
record("production single-field merge preserves unrelated fresh fields", 4);

const originalHook = hooks([base, null, false, "", false, "", Date.now(), ""]);
const originalWrites = [];
const originalFresh = { ...base, recall: false, previewUnrelatedField: "fresh-original-value", externalRevision: 21 };
const OriginalPanel = compile(source, "_", {
  d: originalHook,
  f: jsx,
  p: originalCards,
  m: originalMiniCards,
  i: () => ({ t: tFor(en) }),
  l: ({ checked }) => h("switch", { checked }),
  s: ({ label, checked, disabled, onChange }) => h("toggle", { label, checked, disabled, onChange }),
  r: async () => ({ ...originalFresh }),
  c: async (next) => { originalWrites.push(next); return next; },
  a: async () => ({ cellId: 1, cellType: "preview" }),
  o: async () => {},
  u: async () => {},
  h: () => "common.stopped",
  g: () => "0:00",
  window: { setInterval: () => 1, clearInterval: () => {} },
});
originalHook.begin();
const originalTree = OriginalPanel({ category: "hotkeys", online: false, onLog: () => {}, sheepStatus: null });
const originalArticles = flatten(originalTree).filter((node) => node.type === "article");
assert.equal(originalArticles.length, 7);
assert.deepEqual(originalArticles.map((article) => flatten(article).find((node) => node.type === "h3")).map((heading) => text(heading)), HOTKEY_CARDS.map((card) => tFor(en)(card.title)));
assert.ok(text(originalTree).includes(tFor(en)("hotkeys.offlineHint")));
const originalSwitches = flatten(originalTree).filter((node) => node.type === "button" && node.props?.className === "hotkey-switch");
assert.equal(originalSwitches.length, 7);
assert.ok(originalSwitches.every((button) => button.props.disabled === false));
originalSwitches[0].props.onClick();
await Promise.resolve();
await Promise.resolve();
assert.equal(originalWrites.length, 1);
assert.equal(originalWrites[0].attack, false);
assert.equal(originalWrites[0].recall, false);
assert.equal(originalWrites[0].previewUnrelatedField, "fresh-original-value");
assert.equal(originalWrites[0].externalRevision, 21);
record("actual recovered renderer order/offline editability and save-fresh-field semantics", 15);

function Switch({ checked }) { return h("span", { "data-switch-checked": checked }); }
const cardHook = hooks();
const ProductionCard = compile(pages, "HotkeyCard", {
  h, Fragment, ...cardHook,
  useI18n: () => ({ t: tFor(en), english: (value) => value }),
  Switch,
});
const calls = [];
const renderCard = (pendingField = null, config = base) => {
  cardHook.begin();
  return ProductionCard({ card: HOTKEY_CARDS[0], config, pendingField, onSaveField: (next, field) => calls.push({ next, field }), previewEnabled: true });
};
let productionTree = renderCard();
let productionSwitch = flatten(productionTree).find((node) => node.type === "button" && node.props?.className === "hotkey-switch");
assert.equal(productionSwitch.props.disabled, false);
productionSwitch.props.onClick();
assert.equal(calls[0].field, "attack");
assert.equal(calls[0].next.attack, false);
const attackInputs = flatten(productionTree).filter((node) => node.type === "input");
assert.equal(attackInputs.length, 2);
assert.ok(attackInputs.every((input) => input.props.disabled === false));
attackInputs[1].props.onChange();
assert.equal(calls[1].field, "attackMarchSpeedupDiamond");
assert.equal(calls[1].next.attackMarchSpeedupDiamond, true);
productionTree = renderCard("attack", { ...base, attack: false });
productionSwitch = flatten(productionTree).find((node) => node.type === "button" && node.props?.className === "hotkey-switch");
assert.equal(productionSwitch.props.disabled, true);
assert.ok(flatten(productionTree).filter((node) => node.type === "input").every((input) => input.props.disabled === true));
record("actual production controlled card callbacks and shared pending gate", 9);

for (const catalog of [en, ja]) {
  for (const card of HOTKEY_CARDS) {
    assert.notEqual(catalog[card.title], undefined, `${card.title} missing`);
    assert.notEqual(catalog[card.description], undefined, `${card.description} missing`);
    if (card.warning) assert.notEqual(catalog[card.warning], undefined, `${card.warning} missing`);
  }
}
record("English/Japanese recovered card locale keys", 34);

const report = {
  result: "LWB317_HOTKEYS_SOURCE_LOCAL_OK",
  results,
  current: { pagesSha256: hash(pages), contractsSha256: hash(contracts) },
  limits: "Original HotkeyPanel is executed with controlled config/provider stubs and production HotkeyCard callbacks are invoked directly. No OS/global listener, game-window key injection, attack, recall, shield, equipment or relocation action is registered or executed.",
};
if (process.argv.includes("--record")) fs.writeFileSync(path.join(here, "results.json"), `${JSON.stringify(report, null, 2)}\n`);
console.log(JSON.stringify(report, null, 2));
