import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";
import en from "../../../../src/LWBridge.UI-0.3.17/src/locales/en.js";
import ja from "../../../../src/LWBridge.UI-0.3.17/src/locales/ja.js";
import { previewTradeFixture } from "../../../../src/LWBridge.UI-0.3.17/src/previewAutomationFixtures.js";
import { buildTradePurchaseDays, resolveTradeName, tradePurchaseRowKey } from "../../../../src/LWBridge.UI-0.3.17/src/tradePurchaseHistory.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../..");
const require = createRequire(path.join(repo, "src/LWBridge.UI-0.3.17/package.json"));
const { parse } = require("@babel/parser");
const { transformSync } = require("esbuild");
const panelPath = "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/AutomationPanel-BJ0gIqFh.js";
const pagesPath = "src/LWBridge.UI-0.3.17/src/Pages.jsx";
const panel = fs.readFileSync(path.join(repo, panelPath), "utf8");
const pages = fs.readFileSync(path.join(repo, pagesPath), "utf8");
const hash = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
assert.equal(hash(panel), "6DB2328191AF33E5631D507271C36D7008B287DAA7C8CD1A957B23EF6F871725");
const originalAst = parse(panel, { sourceType: "module" });
const productionAst = parse(pages, { sourceType: "module", plugins: ["jsx"] });
const original = originalAst.program.body.find((n) => n.type === "FunctionDeclaration" && n.id?.name === "pe" && n.params[0]?.properties?.some((p) => p.key?.name === "profileId"));
const production = productionAst.program.body.find((n) => n.type === "FunctionDeclaration" && n.id?.name === "TradeStationCard");
assert.ok(original); assert.ok(production);
const originalHelper = (name) => originalAst.program.body.find((n) => n.type === "FunctionDeclaration" && n.id?.name === name);
function nodes(root) {
  const result = [];
  const walk = (node) => {
    if (!node || typeof node !== "object") return;
    if (node.type) result.push(node);
    for (const child of Object.values(node)) Array.isArray(child) ? child.forEach(walk) : walk(child);
  };
  walk(root); return result;
}
const raw = (node) => panel.slice(node.start, node.end);
const sourceNodes = nodes(original);
const locator = (node) => ({ utf8ByteOffset: Buffer.byteLength(panel.slice(0, node.start)), expression: raw(node) });
const effect = sourceNodes.find((n) => n.type === "CallExpression" && n.callee?.type === "SequenceExpression" && n.callee.expressions.at(-1)?.property?.name === "useEffect" && raw(n).includes("o(e).then"));
const stats = sourceNodes.find((n) => n.type === "ObjectExpression" && n.properties.some((p) => p.key?.name === "className" && p.value?.quasis?.[0]?.value.raw === "trade-station-stats"));
const lastResult = sourceNodes.find((n) => n.type === "VariableDeclarator" && n.id?.name === "w");
const loading = sourceNodes.find((n) => n.type === "LogicalExpression" && raw(n).startsWith("ie&&") && raw(n).includes("automation.tradeStation.loading"));
const noGoods = sourceNodes.find((n) => n.type === "LogicalExpression" && raw(n).startsWith("!ie&&d.length===0&&"));
const fetchError = sourceNodes.find((n) => n.type === "LogicalExpression" && raw(n).startsWith("se&&"));
const tabBranch = sourceNodes.find((n) => n.type === "ConditionalExpression" && raw(n).startsWith("p===`goods`?"));
for (const n of [effect, stats, lastResult, loading, noGoods, fetchError, tabBranch]) assert.ok(n);

const h = (type, props, ...children) => ({ type, props: { ...(props || {}), children } });
const S = { jsx: (type, props) => ({ type, props }), jsxs: (type, props) => ({ type, props }) };
const text = (value) => {
  if (value == null || typeof value === "boolean") return "";
  if (Array.isArray(value)) return value.map(text).join("");
  if (typeof value === "object") return text(value.props?.children);
  return String(value);
};
function find(root, predicate) {
  if (!root || typeof root !== "object") return [];
  if (Array.isArray(root)) return root.flatMap((n) => find(n, predicate));
  return [...(predicate(root) ? [root] : []), ...find(root.props?.children, predicate)];
}
const cclass = (name) => (node) => node.props?.className === name;
const defConfig = { enabled: false, crossServerEnabled: false, selectedItemIds: [], selectedCurrencyIds: [15, 650053] };
const componentNames = { card: function Card() {}, config: function Config() {}, currency: function Currency() {}, goods: function Goods() {}, purchases: function Purchases() {}, switch: function Toggle() {} };
function stateRunner(initial) {
  const values = [...initial]; const effects = []; const writes = [];
  let cursor = 0;
  return {
    values, effects, writes,
    begin() { cursor = 0; effects.length = 0; },
    hooks: {
      useState(defaultValue) {
        const index = cursor++;
        if (index >= values.length) values[index] = typeof defaultValue === "function" ? defaultValue() : defaultValue;
        return [values[index], (value) => { values[index] = typeof value === "function" ? value(values[index]) : value; writes.push({ index, value: values[index] }); }];
      },
      useEffect(fn) { effects.push(fn); },
    },
  };
}
const translate = (catalog) => (key, params = {}) => Object.entries(params).reduce((value, [name, entry]) => value.replaceAll(`{${name}}`, entry), catalog[key] ?? key);
function makeOriginal(fixture, language, catalog, fetch = async () => ({ items: fixture.goods })) {
  const runner = stateRunner([fixture.goods, "goods", false, fixture.gameTexts || {}, fixture.loading, fixture.error]);
  const store = { edit() {}, async flush() {} };
  const helperRuntime = { jsx: (type, props) => typeof type === "function" ? type(props) : ({ type, props }), jsxs: (type, props) => typeof type === "function" ? type(props) : ({ type, props }) };
  const nameResolver = new Function(`return (${raw(originalHelper("C"))});`)();
  const makeHelper = (name) => new Function("te", "S", "C", "b", `return (${raw(originalHelper(name))});`)(() => ({ language, t: translate(catalog) }), helperRuntime, nameResolver, () => null);
  const originalGoods = makeHelper("ue"); const originalPurchases = makeHelper("de");
  const runtime = { jsx: (type, props) => [originalGoods, originalPurchases].includes(type) ? type(props) : ({ type, props }), jsxs: (type, props) => [originalGoods, originalPurchases].includes(type) ? type(props) : ({ type, props }) };
  const fn = new Function("te", "n", "le", "f", "t", "o", "x", "ee", "S", "oe", "y", "g", "fe", "ue", "de", `return (${raw(original)});`)(
    () => ({ language, t: translate(catalog) }), () => ({ draft: defConfig, state: store }), (i) => i, async () => ({}), async () => ({}), fetch,
    runner.hooks, async () => ({}), runtime, componentNames.config, componentNames.card, componentNames.switch, componentNames.currency, originalGoods, originalPurchases,
  );
  return { runner, render(online = true, profileId = "fixture-review") { runner.begin(); return fn({ profileId, online, config: defConfig, status: fixture.status ? { ...fixture.status, purchases: fixture.purchases } : undefined }); } };
}
function makeProduction(fixture, language, catalog) {
  const runner = stateRunner([]);
  const code = transformSync(pages.slice(production.start, production.end), { loader: "jsx", jsxFactory: "h" }).code;
  const fn = new Function("h", "useI18n", "previewTradeFixture", "usePreviewConfig", "useState", "buildTradePurchaseDays", "resolveTradeName", "tradePurchaseRowKey", "PreviewConfigError", "Switch", "ToggleRow", `${code};return TradeStationCard;`)(
    h, () => ({ language, t: translate(catalog) }), fixture ? () => fixture : previewTradeFixture,
    () => ({ draft: defConfig, store: { edit() {}, async flush() {}, getSnapshot: () => ({ draft: defConfig }) } }), runner.hooks.useState,
    buildTradePurchaseDays, resolveTradeName, tradePurchaseRowKey, componentNames.config, componentNames.switch, componentNames.switch,
  );
  return { runner, render(previewEnabled = true, previewState = "automation-trade-review") { runner.begin(); return fn({ previewEnabled, previewState }); } };
}
function snapshot(tree, isOriginal) {
  const tabs = find(tree, (n) => n.props?.role === "tab");
  const panelNode = find(tree, (n) => n.props?.role === "tabpanel")[0];
  const goods = find(tree, (n) => n.props?.className?.split(" ").includes("trade-station-good"));
  const purchases = find(tree, cclass("trade-station-purchase"));
  return {
    stats: text(find(tree, cclass("trade-station-stats"))),
    tabs: tabs.map((n) => ({ label: text(n), selected: n.props["aria-selected"] })),
    loadingOrEmpty: text(find(panelNode, (n) => n.type === "span" && n.props.className === "muted")),
    fetchError: text(find(tree, cclass("automation-error"))),
    fetchErrorInsidePanel: find(panelNode, cclass("automation-error")).length > 0,
    goodsCount: goods?.length || 0,
    purchasesCount: purchases?.length || 0,
    purchaseCopy: text(find(tree, cclass("trade-station-purchase-copy"))),
  };
}

const nativeFixture = { goods: [], purchases: [], gameTexts: {}, loading: false, error: "", status: undefined };
const nativeExpected = snapshot(makeOriginal(nativeFixture, "en", en).render(false), true);
const nativeActual = snapshot(makeProduction(undefined, "en", en).render(false, ""), false);
if (process.argv.includes("--record-baseline")) {
  assert.ok(!fs.existsSync(path.join(here, "native-fence-baseline.json")), "Historical failing baseline already exists; preserve it");
  const baseline = { task: "LWB317-REVIEW-TRADE-STATUS-001", case: "fresh-native-unavailable-default", productionHash: hash(pages), original: nativeExpected, current: nativeActual, matches: JSON.stringify(nativeActual) === JSON.stringify(nativeExpected), scope: "App passes empty previewState in native modes; current original offline renderer starts with no goods/status." };
  fs.writeFileSync(path.join(here, "native-fence-baseline.json"), JSON.stringify(baseline, null, 2) + "\n");
  console.log(JSON.stringify(baseline, null, 2));
  process.exit(0);
}
assert.deepEqual(nativeActual, nativeExpected, "native mode must not receive positive Trade QA data");
// Caller fencing must resist a supplied Trade preview string when disabled.
assert.deepEqual(snapshot(makeProduction(undefined, "en", en).render(false, "automation-trade-error-retained"), false), nativeExpected, "disabled preview ignores supplied positive/failure fixtures");

const statusCases = [
  ["missing", undefined], ["null", null], ["empty-object", {}],
  ["null-fields", { detectedCount: null, attemptedCount: null, succeededCount: null, lastResult: { state: null } }],
  ["zero-fields", { detectedCount: 0, attemptedCount: 0, succeededCount: 0, lastResult: {} }],
  ["distinct-counters", { detectedCount: 17, attemptedCount: 5, succeededCount: 2 }],
  ...["success", "failed", "skipped", "confirmed_after_timeout"].map((state) => [state, { detectedCount: 17, attemptedCount: 5, succeededCount: 2, lastResult: { state } }]),
];
const reports = [];
for (const [language, catalog] of [["en", en], ["ja", ja]]) {
  for (const [name, status] of statusCases) {
    const fixture = { goods: [], purchases: [], error: "", loading: false, status };
    const originalOutput = snapshot(makeOriginal(fixture, language, catalog).render(), true);
    const currentOutput = snapshot(makeProduction(fixture, language, catalog).render(), false);
    assert.deepEqual(currentOutput, originalOutput, `${language}/${name}`);
    reports.push({ language, name, original: originalOutput, current: currentOutput });
  }
  for (const state of ["automation-trade-loading", "automation-trade-loading-retained", "automation-trade-error", "automation-trade-error-retained", "automation-trade-empty", "automation-trade-save-error"]) {
    const fixture = previewTradeFixture(state);
    const originalComponent = makeOriginal(fixture, language, catalog);
    const currentComponent = makeProduction(fixture, language, catalog);
    for (const tab of ["goods", "purchases", "goods"]) {
      if (tab !== "goods" || originalComponent.runner.values[1] !== "goods") {
        for (const component of [originalComponent, currentComponent]) {
          const tabs = find(component.render(), (n) => n.props?.role === "tab");
          tabs[tab === "goods" ? 0 : 1].props.onClick();
        }
      }
      const expected = snapshot(originalComponent.render(), true);
      const actual = snapshot(currentComponent.render(), false);
      assert.deepEqual(actual, expected, `${language}/${state}/${tab}`);
      assert.equal(actual.fetchErrorInsidePanel, false);
      reports.push({ language, name: `${state}/${tab}`, original: expected, current: actual });
    }
    // A producer update must not reset the selected tab. Actual callbacks/hooks,
    // rather than a detached expression, run on both sides.
    for (const component of [originalComponent, currentComponent]) find(component.render(), (n) => n.props?.role === "tab")[1].props.onClick();
    fixture.status = { detectedCount: 23, attemptedCount: 8, succeededCount: 6, lastResult: { state: "skipped" } };
    assert.deepEqual(snapshot(currentComponent.render(), false), snapshot(originalComponent.render(), true), `${language}/${state}/status-update`);
    reports.push({ language, name: `${state}/status-update-retains-purchases-tab`, pass: true });
  }
}

const effectReports = [];
function deferred() { let resolve, reject; const promise = new Promise((r, j) => { resolve = r; reject = j; }); return { promise, resolve, reject }; }
const drain = async () => { for (let i = 0; i < 8; i++) await Promise.resolve(); };
for (const mode of ["success", "Error-rejection", "string-rejection", "cancel-success", "cancel-failure", "offline"]) {
  const fixture = previewTradeFixture("automation-trade-error-retained");
  const request = deferred(); const calls = [];
  const component = makeOriginal(fixture, "en", en, (id) => { calls.push(id); return request.promise; });
  component.render(mode !== "offline");
  assert.equal(component.runner.effects.length, 2);
  const cleanup = component.runner.effects[0]();
  assert.equal(component.runner.values[5], "");
  assert.equal(component.runner.values[0], fixture.goods, "fetch start preserves goods");
  assert.equal(fixture.purchases.length, 2, "purchase status independent");
  assert.equal(component.runner.values[4], mode !== "offline");
  if (mode.startsWith("cancel")) cleanup();
  if (mode === "offline") assert.deepEqual(calls, []);
  else {
    assert.deepEqual(calls, ["fixture-review"]);
    if (mode === "Error-rejection" || mode === "cancel-failure") request.reject(new Error("review supplied failure"));
    else if (mode === "string-rejection") request.reject("review string failure");
    else request.resolve({ items: [{ itemId: 9123, offers: [] }] });
    await drain();
    if (mode === "success") assert.equal(component.runner.values[0][0].itemId, 9123);
    else assert.equal(component.runner.values[0], fixture.goods, "rejection/cancellation retains goods");
    assert.equal(component.runner.values[5], mode === "Error-rejection" ? "Error: review supplied failure" : mode === "string-rejection" ? "review string failure" : "");
    assert.equal(component.runner.values[4], mode.startsWith("cancel"), "cancellation suppresses late finally");
  }
  effectReports.push({ mode, fetchCalls: calls, retainedGoods: component.runner.values[0] === fixture.goods, error: component.runner.values[5], loading: component.runner.values[4], pass: true });
}

const localeReports = [];
for (const [language, file, catalog] of [["en", "en-BisSXcTB.js", en], ["ja", "ja-UrbzJu-m.js", ja]]) {
  const localePath = `evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/${file}`;
  const locale = fs.readFileSync(path.join(repo, localePath), "utf8");
  const properties = nodes(parse(locale, { sourceType: "module" }));
  const entries = [];
  for (const key of ["detected", "attempted", "succeeded", "lastResult", "loading", "noGoods", "goods", "purchasedItems", "state.success", "state.failed", "state.skipped", "state.confirmed_after_timeout"]) {
    const full = `automation.tradeStation.${key}`;
    const property = properties.find((n) => n.type === "ObjectProperty" && n.key?.value === full);
    assert.ok(property, full);
    const value = property.value.type === "TemplateLiteral" ? property.value.quasis[0].value.cooked : property.value.value;
    assert.equal(catalog[full], value);
    entries.push({ key: full, utf8ByteOffset: Buffer.byteLength(locale.slice(0, property.start)), expression: locale.slice(property.start, property.end), value });
  }
  localeReports.push({ language, path: localePath, sha256: hash(locale), entries });
}

const report = {
  task: "LWB317-REVIEW-TRADE-STATUS-001", recommendation: "AWAITING_REVIEW", result: "LWB317_REVIEW_TRADE_STATUS_OK",
  reference: { path: panelPath, sha256: hash(panel) },
  current: { path: pagesPath, sha256: hash(pages), scopeSha256: hash(pages.slice(production.start, production.end)) },
  sourceLocators: { component: locator(original), fetchEffect: locator(effect), stats: locator(stats), lastResult: locator(lastResult), loading: locator(loading), emptyGoods: locator(noGoods), fetchError: locator(fetchError), tabs: locator(tabBranch) },
  locales: localeReports, renderComparisons: reports, fetchEffectCases: effectReports, nativeFenceComparisons: [{ name: "fresh-native-default", original: nativeExpected, current: nativeActual }, { name: "disabled-preview-supplied-state", pass: true }],
  limits: "Synthetic local full-component hooks/render checks and original fetch-effect contracts. No clone native fetch provider, purchases, persistence, original protected runtime or pixel comparison. Purchase grouping/control behavior independently checked by existing regression harnesses, not duplicated here.",
};
const appPath = "src/LWBridge.UI-0.3.17/src/App.jsx";
const app = fs.readFileSync(path.join(repo, appPath), "utf8");
const appGate = nodes(parse(app, { sourceType: "module", plugins: ["jsx"] })).find((n) => n.type === "JSXAttribute" && n.name?.name === "previewState");
assert.ok(appGate);
report.producerBoundary = { path: appPath, sha256: hash(app), utf8ByteOffset: Buffer.byteLength(app.slice(0, appGate.start)), expression: app.slice(appGate.start, appGate.end) };
const fixturesPath = "src/LWBridge.UI-0.3.17/src/previewAutomationFixtures.js";
report.fixtures = { path: fixturesPath, sha256: hash(fs.readFileSync(path.join(repo, fixturesPath))) };
if (process.argv.includes("--record")) fs.writeFileSync(path.join(here, "independent-results.json"), JSON.stringify(report, null, 2) + "\n");
console.log(JSON.stringify({ result: report.result, renderComparisons: reports.length, originalFetchEffectCases: effectReports.length, locales: localeReports.length, recommendation: report.recommendation }, null, 2));
