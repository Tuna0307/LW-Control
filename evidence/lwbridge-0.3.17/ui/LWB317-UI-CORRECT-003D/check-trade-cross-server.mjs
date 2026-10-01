import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";
import { createConfigDraft } from "../../../../src/LWBridge.UI-0.3.17/src/previewConfig.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../..");
const ui = path.join(repo, "src/LWBridge.UI-0.3.17");
const require = createRequire(path.join(ui, "package.json"));
const { parse } = require("@babel/parser");

const panelPath = path.resolve(here, "../frontend-package/web/assets/AutomationPanel-BJ0gIqFh.js");
const sharedPath = path.resolve(here, "../frontend-package/web/assets/index-BVfnK1wp.js");
const pagesPath = path.join(ui, "src/Pages.jsx");
const configHookPath = path.join(ui, "src/previewConfigHook.jsx");

const panel = fs.readFileSync(panelPath, "utf8");
const shared = fs.readFileSync(sharedPath, "utf8");
const pages = fs.readFileSync(pagesPath, "utf8");
const configHook = fs.readFileSync(configHookPath, "utf8");
const sha256 = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
const byteOffset = (text, needle) => {
  const index = text.indexOf(needle);
  assert.notEqual(index, -1, `missing source anchor: ${needle}`);
  return Buffer.byteLength(text.slice(0, index), "utf8");
};
const clone = (value) => structuredClone(value);
const waitFor = async (predicate, label) => {
  for (let attempt = 0; attempt < 100; attempt += 1) {
    if (predicate()) return;
    await new Promise((resolve) => setImmediate(resolve));
  }
  throw Error(`Timed out waiting for ${label}`);
};

assert.equal(sha256(panel), "6DB2328191AF33E5631D507271C36D7008B287DAA7C8CD1A957B23EF6F871725");
assert.equal(sha256(shared), "44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6");

const sourceAnchors = {
  defaultConfig: "ce={enabled:!1,crossServerEnabled:!1,selectedItemIds:[],selectedCurrencyIds:[15,650053]}",
  immediateSave: "async function C(e){l.state.edit(e,!1),await l.state.flush().catch(()=>void 0)}",
  crossServerLabel: "automation.tradeStation.crossServer",
  crossServerSwitch: "disabled:!r,onChange:e=>{C({...u,crossServerEnabled:e})}",
};
const sourceLocators = Object.fromEntries(Object.entries(sourceAnchors).map(([name, needle]) => [name, byteOffset(panel, needle)]));
assert.deepEqual(sourceLocators, {
  defaultConfig: 400,
  immediateSave: 7213,
  crossServerLabel: 8264,
  crossServerSwitch: 8330,
});
assert.match(shared, /configSave\.failed/);
assert.match(shared, /common\.retry/);
assert.match(shared, /configSave\.discard/);

const pagesAst = parse(pages, { sourceType: "module", plugins: ["jsx"] });
let tradeFunction = null;
function findTradeFunction(node) {
  if (!node || typeof node !== "object" || tradeFunction) return;
  if (node.type === "FunctionDeclaration" && node.id?.name === "TradeStationCard") {
    tradeFunction = node;
    return;
  }
  for (const value of Object.values(node)) {
    if (Array.isArray(value)) value.forEach(findTradeFunction);
    else if (value && typeof value === "object") findTradeFunction(value);
  }
}
findTradeFunction(pagesAst);
assert.ok(tradeFunction, "TradeStationCard not found");

let saveExpression = null;
let switchOpening = null;
function collectTradeExpressions(node) {
  if (!node || typeof node !== "object") return;
  if (node.type === "VariableDeclarator" && node.id?.name === "save") {
    saveExpression = pages.slice(node.init.start, node.init.end);
  }
  if (node.type === "JSXOpeningElement" && node.name?.name === "ToggleRow") {
    const label = node.attributes.find((attribute) => attribute.name?.name === "label")?.value?.expression;
    if (label?.type === "CallExpression" && label.arguments?.[0]?.value === "automation.tradeStation.crossServer") {
      switchOpening = node;
    }
  }
  for (const value of Object.values(node)) {
    if (Array.isArray(value)) value.forEach(collectTradeExpressions);
    else if (value && typeof value === "object") collectTradeExpressions(value);
  }
}
collectTradeExpressions(tradeFunction.body);
assert.ok(saveExpression, "Trade save callback not found");
assert.ok(switchOpening, "Trade cross-server ToggleRow not found");

const attrExpression = (name) => {
  const attribute = switchOpening.attributes.find((entry) => entry.name?.name === name);
  assert.ok(attribute?.value?.expression, `missing ${name} expression`);
  return pages.slice(attribute.value.expression.start, attribute.value.expression.end);
};
const disabledExpression = attrExpression("disabled");
const onChangeExpression = attrExpression("onChange");
assert.equal(disabledExpression, "!previewEnabled");
assert.match(onChangeExpression, /save\(\{ \.\.\.config\.store\.getSnapshot\(\)\.draft, crossServerEnabled: value \}\)/);

const hookAst = parse(configHook, { sourceType: "module", plugins: ["jsx"] });
const errorCallbacks = [];
function collectErrorCallbacks(node) {
  if (!node || typeof node !== "object") return;
  if (node.type === "JSXOpeningElement" && node.name?.name === "button") {
    const onClick = node.attributes.find((attribute) => attribute.name?.name === "onClick")?.value?.expression;
    if (onClick) errorCallbacks.push(configHook.slice(onClick.start, onClick.end));
  }
  for (const value of Object.values(node)) {
    if (Array.isArray(value)) value.forEach(collectErrorCallbacks);
    else if (value && typeof value === "object") collectErrorCallbacks(value);
  }
}
collectErrorCallbacks(hookAst);
assert.equal(errorCallbacks.length, 2, "expected Retry and Discard callbacks");
const [retryExpression, discardExpression] = errorCallbacks;

function makeCrossServerHandler(store) {
  const config = {
    store,
    get draft() { return store.getSnapshot().draft; },
    get saving() { return store.getSnapshot().saving; },
  };
  const save = new Function("config", `return (${saveExpression});`)(config);
  const onChange = new Function("config", "save", `return (${onChangeExpression});`)(config, save);
  return { config, save, onChange };
}
const makeErrorHandler = (expression, store) => new Function("config", `return (${expression});`)({ store });
const isDisabled = (previewEnabled, config) => new Function("previewEnabled", "config", `return (${disabledExpression});`)(previewEnabled, config);
const defaultTrade = { enabled: false, crossServerEnabled: false, selectedItemIds: [], selectedCurrencyIds: [15, 650053] };

assert.deepEqual(defaultTrade, {
  enabled: false,
  crossServerEnabled: false,
  selectedItemIds: [],
  selectedCurrencyIds: [15, 650053],
});

// The actual switch callback dispatches immediately, stays interactive during
// the first write, preserves the rest of the Trade draft, and queues the newer
// value until the older acknowledgement has been incorporated.
{
  let server = { enabled: true, crossServerEnabled: false, selectedItemIds: [7001, 7002], selectedCurrencyIds: [15] };
  const writes = [];
  const pending = [];
  const store = createConfigDraft(clone(server), {
    valid: () => true,
    read: async () => clone(server),
    write: (draft) => new Promise((resolve) => {
      const written = clone(draft);
      writes.push(written);
      pending.push(() => { server = clone(written); resolve(clone(written)); });
    }),
  });
  const handler = makeCrossServerHandler(store);

  assert.equal(isDisabled(true, handler.config), false, "online switch should start enabled");
  assert.equal(isDisabled(false, handler.config), true, "offline switch should be disabled");

  handler.onChange(true);
  assert.equal(writes.length, 1, "cross-server edit did not dispatch immediately");
  assert.equal(store.getSnapshot().saving, true);
  assert.equal(isDisabled(true, handler.config), false, "saving must not disable the online switch");
  assert.deepEqual(store.getSnapshot().draft, {
    enabled: true,
    crossServerEnabled: true,
    selectedItemIds: [7001, 7002],
    selectedCurrencyIds: [15],
  });

  handler.onChange(false);
  assert.equal(writes.length, 1, "second toggle should wait behind the active write");
  assert.deepEqual(store.getSnapshot().draft, {
    enabled: true,
    crossServerEnabled: false,
    selectedItemIds: [7001, 7002],
    selectedCurrencyIds: [15],
  });

  pending.shift()();
  await waitFor(() => writes.length === 2 && pending.length === 1, "queued cross-server write");
  assert.equal(store.getSnapshot().confirmed.crossServerEnabled, true, "older acknowledgement was not recorded");
  assert.deepEqual(store.getSnapshot().draft, {
    enabled: true,
    crossServerEnabled: false,
    selectedItemIds: [7001, 7002],
    selectedCurrencyIds: [15],
  }, "older acknowledgement overwrote the newer switch draft");

  pending.shift()();
  await waitFor(() => !store.getSnapshot().saving, "cross-server save completion");
  assert.deepEqual(store.getSnapshot().confirmed, {
    enabled: true,
    crossServerEnabled: false,
    selectedItemIds: [7001, 7002],
    selectedCurrencyIds: [15],
  });
  store.dispose();
}

// A failed actual switch callback retains the edited draft; the actual shared
// Retry callback flushes that same draft without changing goods/currencies/enabled.
{
  let server = { enabled: true, crossServerEnabled: false, selectedItemIds: [7001], selectedCurrencyIds: [15, 650053] };
  let failFirst = true;
  const store = createConfigDraft(clone(server), {
    valid: () => true,
    read: async () => clone(server),
    write: async (draft) => {
      if (failFirst) { failFirst = false; throw Error("fixture cross-server save failed"); }
      server = clone(draft);
      return clone(server);
    },
  });
  makeCrossServerHandler(store).onChange(true);
  await waitFor(() => !!store.getSnapshot().error, "cross-server failure");
  assert.deepEqual(store.getSnapshot().draft, {
    enabled: true,
    crossServerEnabled: true,
    selectedItemIds: [7001],
    selectedCurrencyIds: [15, 650053],
  });
  await makeErrorHandler(retryExpression, store)();
  assert.equal(store.getSnapshot().error, null);
  assert.equal(store.getSnapshot().dirty, false);
  assert.deepEqual(store.getSnapshot().confirmed, server);
  assert.equal(server.crossServerEnabled, true);
  store.dispose();
}

// Discard uses the actual shared callback and restores the confirmed/server
// value after a failed switch save, including all unrelated Trade fields.
{
  const initial = { enabled: true, crossServerEnabled: false, selectedItemIds: [7001], selectedCurrencyIds: [650053] };
  const store = createConfigDraft(clone(initial), {
    valid: () => true,
    read: async () => clone(initial),
    write: async () => { throw Error("fixture cross-server save failed"); },
  });
  makeCrossServerHandler(store).onChange(true);
  await waitFor(() => !!store.getSnapshot().error, "cross-server discard failure");
  assert.equal(store.getSnapshot().draft.crossServerEnabled, true);
  await makeErrorHandler(discardExpression, store)();
  assert.equal(store.getSnapshot().error, null);
  assert.equal(store.getSnapshot().dirty, false);
  assert.deepEqual(store.getSnapshot().draft, initial);
  assert.deepEqual(store.getSnapshot().confirmed, initial);
  store.dispose();
}

console.log(JSON.stringify({
  result: "LWB317_UI_CORRECT003D_TRADE_CROSS_SERVER_OK",
  sourceHashes: { panel: sha256(panel), shared: sha256(shared) },
  sourceLocators,
  productionExpressions: {
    save: saveExpression,
    disabled: disabledExpression,
    onChange: onChangeExpression,
    retry: retryExpression,
    discard: discardExpression,
  },
  scenarios: {
    deferredSecondToggle: "PASS",
    olderAcknowledgementPreservesNewerDraft: "PASS",
    retry: "PASS",
    discard: "PASS",
    preservesEnabledGoodsCurrencies: "PASS",
    offlineDisabled: "PASS",
  },
}, null, 2));
