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
const cardPath = path.resolve(here, "../frontend-package/web/assets/AutomationCard-LCx_jIi7.js");
const sharedPath = path.resolve(here, "../frontend-package/web/assets/index-BVfnK1wp.js");
const pagesPath = path.join(ui, "src/Pages.jsx");
const configHookPath = path.join(ui, "src/previewConfigHook.jsx");

const panel = fs.readFileSync(panelPath, "utf8");
const card = fs.readFileSync(cardPath, "utf8");
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
assert.equal(sha256(card), "24ED773623C237F8219EFF5443FAC3BA7B55B6E99E168E52FF066FAF2ABE7A61");
assert.equal(sha256(shared), "44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6");

const sourceAnchors = {
  defaultConfig: "ce={enabled:!1,crossServerEnabled:!1,selectedItemIds:[],selectedCurrencyIds:[15,650053]}",
  normalizedCurrencies: "selectedCurrencyIds:e?.selectedCurrencyIds?.length?e.selectedCurrencyIds:ce.selectedCurrencyIds",
  goodsFilterSort: ".filter(e=>n||!e.offers.every(e=>e.exclusive)).sort((e,t)=>t.quality-e.quality||e.itemId-t.itemId)",
  goodsDisabled: "disabled:!e||t||c,onChange:e=>o(n.itemId,e.target.checked)",
  currencyDisabled: "disabled:!e||t||s&&n.length===1,onChange:e=>a(o,e.target.checked)",
  immediateSave: "async function C(e){l.state.edit(e,!1),await l.state.flush().catch(()=>void 0)}",
  goodsHandler: "function pe(e,t){let n=t?[...new Set([...u.selectedItemIds,e])].sort((e,t)=>e-t):u.selectedItemIds.filter(t=>t!==e);C({...u,enabled:n.length>0&&u.enabled,selectedItemIds:n})}",
  currencyHandler: "function me(e,t){if(!t&&u.selectedCurrencyIds.length===1)return;let n=t?[...new Set([...u.selectedCurrencyIds,e])].sort((e,t)=>e-t):u.selectedCurrencyIds.filter(t=>t!==e);C({...u,selectedCurrencyIds:n})}",
  enableGuard: "he=u.enabled||u.selectedItemIds.length>0",
  currencyCallerNoSavingLock: "saving:!1,selectedCurrencyIds:u.selectedCurrencyIds",
  goodsCallerNoSavingLock: "saving:!1,showExclusive:h,selectedItemIds:u.selectedItemIds",
  showExclusive: "checked:h,onChange:e=>_(e.target.checked)",
  tradeConfigDisabledOfflineOnly: "configDisabled:!r",
};
const sourceLocators = Object.fromEntries(Object.entries(sourceAnchors).map(([name, needle]) => [name, byteOffset(panel, needle)]));

assert.match(card, /M=!l\|\|!!d/);
assert.match(shared, /configSave\.failed/);
assert.match(shared, /common\.retry/);
assert.match(shared, /configSave\.discard/);

// Product-side contracts recovered above: Trade defaults, exclusive filter, and
// save-time selectors. Offline disable is supplied by AutomationCard's fieldset.
assert.match(pages, /selectedItemIds: previewState === "automation-trade-positive" \|\| previewState === "automation-trade-history" \? \[7001\] : \[\], selectedCurrencyIds: \[15, 650053\]/);
assert.match(pages, /showExclusive \|\| !item\.offers\.every\(\(offer\) => offer\.exclusive\)/);
assert.match(pages, /disabled=\{selected && config\.draft\.selectedCurrencyIds\.length === 1\}/);
assert.match(pages, /disabled=\{exclusive\} onChange=\{\(event\) => toggleItem/);
assert.doesNotMatch(pages, /disabled=\{config\.saving \|\| \(selected && config\.draft\.selectedCurrencyIds\.length === 1\)\}/);
assert.doesNotMatch(pages, /disabled=\{config\.saving \|\| exclusive\}/);

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

const handlerExpressions = {};
function collectTradeHandlers(node) {
  if (!node || typeof node !== "object") return;
  if (node.type === "VariableDeclarator" && ["save", "toggleItem", "toggleCurrency"].includes(node.id?.name)) {
    handlerExpressions[node.id.name] = pages.slice(node.init.start, node.init.end);
  }
  for (const value of Object.values(node)) {
    if (Array.isArray(value)) value.forEach(collectTradeHandlers);
    else if (value && typeof value === "object") collectTradeHandlers(value);
  }
}
collectTradeHandlers(tradeFunction.body);
assert.deepEqual(Object.keys(handlerExpressions).sort(), ["save", "toggleCurrency", "toggleItem"]);

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

function makeTradeHandlers(store) {
  const config = { store, get draft() { return store.getSnapshot().draft; } };
  const enabled = config.draft.enabled === true;
  const save = new Function("config", `return (${handlerExpressions.save});`)(config);
  return {
    config,
    save,
    toggleItem: new Function("config", "save", "enabled", `return (${handlerExpressions.toggleItem});`)(config, save, enabled),
    toggleCurrency: new Function("config", "save", `return (${handlerExpressions.toggleCurrency});`)(config, save),
  };
}
const makeErrorHandler = (expression, store) => new Function("config", `return (${expression});`)({ store });
const defaultTrade = { enabled: false, crossServerEnabled: false, selectedItemIds: [], selectedCurrencyIds: [15, 650053] };

// Currency handler: immediate write, last-currency guard, edit while saving,
// and queued newer draft after the older acknowledgement.
{
  let server = clone(defaultTrade);
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
  makeTradeHandlers(store).toggleCurrency(650053, false);
  assert.equal(writes.length, 1, "currency edit did not dispatch immediately");
  assert.equal(store.getSnapshot().saving, true);
  assert.deepEqual(store.getSnapshot().draft.selectedCurrencyIds, [15]);
  makeTradeHandlers(store).toggleCurrency(15, false);
  assert.equal(writes.length, 1, "last currency was allowed to dispatch removal");
  assert.deepEqual(store.getSnapshot().draft.selectedCurrencyIds, [15]);
  makeTradeHandlers(store).toggleCurrency(650053, true);
  assert.equal(writes.length, 1, "new currency write should queue behind the pending write");
  assert.deepEqual(store.getSnapshot().draft.selectedCurrencyIds, [15, 650053]);
  pending.shift()();
  await waitFor(() => writes.length === 2 && pending.length === 1, "queued currency write");
  assert.deepEqual(store.getSnapshot().confirmed.selectedCurrencyIds, [15]);
  assert.deepEqual(store.getSnapshot().draft.selectedCurrencyIds, [15, 650053]);
  pending.shift()();
  await waitFor(() => !store.getSnapshot().saving, "currency save completion");
  assert.deepEqual(store.getSnapshot().confirmed.selectedCurrencyIds, [15, 650053]);
  store.dispose();
}

// Goods handler: selection/unselection is sorted and unselecting the final good
// turns an enabled Trade Station draft off before writing it.
{
  let server = { ...clone(defaultTrade), enabled: true, selectedItemIds: [7001] };
  const writes = [];
  const store = createConfigDraft(clone(server), {
    valid: () => true,
    read: async () => clone(server),
    write: async (draft) => { server = clone(draft); writes.push(clone(draft)); return clone(draft); },
  });
  makeTradeHandlers(store).toggleItem(7002, true);
  await waitFor(() => !store.getSnapshot().saving, "goods add save");
  assert.deepEqual(writes.at(-1).selectedItemIds, [7001, 7002]);
  assert.equal(writes.at(-1).enabled, true);
  makeTradeHandlers(store).toggleItem(7002, false);
  await waitFor(() => !store.getSnapshot().saving, "goods remove save");
  assert.deepEqual(writes.at(-1).selectedItemIds, [7001]);
  makeTradeHandlers(store).toggleItem(7001, false);
  await waitFor(() => !store.getSnapshot().saving, "final goods remove save");
  assert.deepEqual(writes.at(-1).selectedItemIds, []);
  assert.equal(writes.at(-1).enabled, false);
  store.dispose();
}

// A failure originates from the actual goods callback. Retry is the actual
// shared error-button callback and confirms the retained selection.
{
  let server = clone(defaultTrade);
  let failFirst = true;
  const store = createConfigDraft(clone(server), {
    valid: () => true,
    read: async () => clone(server),
    write: async (draft) => {
      if (failFirst) { failFirst = false; throw Error("fixture trade save failed"); }
      server = clone(draft);
      return clone(server);
    },
  });
  makeTradeHandlers(store).toggleItem(7001, true);
  await waitFor(() => !!store.getSnapshot().error, "trade goods failure");
  assert.equal(store.getSnapshot().dirty, true);
  assert.deepEqual(store.getSnapshot().draft.selectedItemIds, [7001]);
  await makeErrorHandler(retryExpression, store)();
  assert.equal(store.getSnapshot().error, null);
  assert.equal(store.getSnapshot().dirty, false);
  assert.deepEqual(store.getSnapshot().confirmed.selectedItemIds, [7001]);
  store.dispose();
}

// A failure originates from the actual currency callback. Discard is the
// actual shared error-button callback and restores the confirmed currencies.
{
  const initial = clone(defaultTrade);
  const store = createConfigDraft(clone(initial), {
    valid: () => true,
    read: async () => clone(initial),
    write: async () => { throw Error("fixture trade save failed"); },
  });
  makeTradeHandlers(store).toggleCurrency(650053, false);
  await waitFor(() => !!store.getSnapshot().error, "trade currency failure");
  assert.deepEqual(store.getSnapshot().draft.selectedCurrencyIds, [15]);
  await makeErrorHandler(discardExpression, store)();
  assert.equal(store.getSnapshot().error, null);
  assert.equal(store.getSnapshot().dirty, false);
  assert.deepEqual(store.getSnapshot().draft.selectedCurrencyIds, [15, 650053]);
  store.dispose();
}

console.log(JSON.stringify({
  result: "LWB317_UI_CORRECT003B_TRADE_SELECTION_OK",
  sourceHashes: { panel: sha256(panel), card: sha256(card), shared: sha256(shared) },
  sourceLocators,
  handlers: handlerExpressions,
  recoveryCallbacks: { retry: retryExpression, discard: discardExpression },
}, null, 2));
