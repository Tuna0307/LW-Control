import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";
import en from "../../../../../src/LWBridge.UI-0.3.17/src/locales/en.js";
import { previewTradeFixture, previewTradeGoods, previewTradePurchases } from "../../../../../src/LWBridge.UI-0.3.17/src/previewAutomationFixtures.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const require = createRequire(path.join(repo, "src/LWBridge.UI-0.3.17/package.json"));
const { parse } = require("@babel/parser");
const { transformSync } = require("esbuild");
const sourcePath = "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/AutomationPanel-BJ0gIqFh.js";
const source = fs.readFileSync(path.join(repo, sourcePath), "utf8");
const pages = fs.readFileSync(path.join(repo, "src/LWBridge.UI-0.3.17/src/AutomationPage.jsx"), "utf8");
const hash = (data) => crypto.createHash("sha256").update(data).digest("hex").toUpperCase();
assert.equal(hash(source), "6DB2328191AF33E5631D507271C36D7008B287DAA7C8CD1A957B23EF6F871725");
const originalAst = parse(source, { sourceType: "module" });
const cloneAst = parse(pages, { sourceType: "module", plugins: ["jsx"] });
const originalTrade = originalAst.program.body.find((node) => node.type === "FunctionDeclaration" && node.id.name === "pe" && node.params[0]?.properties?.some((property) => property.key.name === "profileId"));
const cloneTrade = cloneAst.program.body.find((node) => node.type === "FunctionDeclaration" && node.id.name === "TradeStationCard");
assert.ok(originalTrade); assert.ok(cloneTrade);
const originalNodes = []; const cloneNodes = [];
function walk(node, nodes) {
  if (!node || typeof node !== "object") return;
  if (node.type) nodes.push(node);
  for (const value of Object.values(node)) {
    if (Array.isArray(value)) value.forEach((child) => walk(child, nodes));
    else if (value && typeof value === "object") walk(value, nodes);
  }
}
walk(originalTrade.body, originalNodes); walk(cloneTrade.body, cloneNodes);
const rawOriginal = (node) => source.slice(node.start, node.end);
const rawClone = (node) => pages.slice(node.start, node.end);
const sourceStats = originalNodes.find((node) => node.type === "CallExpression" && node.arguments[1]?.type === "ObjectExpression" && node.arguments[1].properties.some((property) => property.key?.name === "className" && property.value?.quasis?.[0]?.value.raw === "trade-station-stats"));
const cloneStats = cloneNodes.find((node) => node.type === "JSXElement" && node.openingElement.attributes.some((attribute) => attribute.name?.name === "className" && attribute.value?.value === "trade-station-stats"));
const sourceBranch = (key) => originalNodes.find((node) => node.type === "LogicalExpression" && node.right.type === "CallExpression" && rawOriginal(node.right).includes(key));
const cloneBranch = (key) => cloneNodes.find((node) => node.type === "ConditionalExpression" && node.consequent.type === "JSXElement" && (key.startsWith("automation.") ? node.consequent.openingElement.name.name === "span" : node.consequent.openingElement.attributes.some((attribute) => attribute.name?.name === "className" && attribute.value?.value === "automation-error")) && rawClone(node.consequent).includes(key));
const pairs = {
  stats: [sourceStats, cloneStats],
  loading: [sourceBranch("automation.tradeStation.loading"), cloneBranch("automation.tradeStation.loading")],
  noGoods: [sourceBranch("automation.tradeStation.noGoods"), cloneBranch("automation.tradeStation.noGoods")],
  fetchError: [sourceBranch("className:`automation-error`"), cloneBranch('className="automation-error"')],
};
for (const [name, [original, current]] of Object.entries(pairs)) { assert.ok(original, `original ${name}`); assert.ok(current, `production ${name}`); }
const h = (type, props, ...children) => ({ type, props: { ...props, children } });
const S = { jsx: (type, props) => ({ type, props }), jsxs: (type, props) => ({ type, props }) };
const t = (key) => en[key] || key;
function textOf(node) {
  if (node == null || typeof node === "boolean") return "";
  if (Array.isArray(node)) return node.map(textOf).join("");
  if (typeof node === "object") return textOf(node.props.children);
  return String(node);
}
const originalRender = (node, fixture) => new Function("S", "c", "a", "w", "ie", "d", "se", `return (${rawOriginal(node)});`)(S, t, fixture.status, fixture.status?.lastResult?.state, fixture.loading, fixture.goods, fixture.error);
const productionRender = (node, fixture) => {
  const code = transformSync(`const result = (${rawClone(node)});`, { loader: "jsx", jsxFactory: "h" }).code;
  return new Function("h", "t", "fixture", code + "\nreturn result;")(h, t, fixture);
};
const states = ["automation-trade-positive", "automation-trade-empty", "automation-trade-status-absent", "automation-trade-loading", "automation-trade-loading-retained", "automation-trade-error", "automation-trade-error-retained", "automation-trade-confirmed-timeout", "automation-trade-status-failed", "automation-trade-status-skipped", "automation-trade-save-error"];
const results = [];
for (const state of states) {
  const fixture = previewTradeFixture(state);
  const rendered = {};
  for (const [name, [original, current]] of Object.entries(pairs)) {
    const expected = textOf(originalRender(original, fixture));
    const actual = textOf(productionRender(current, fixture));
    assert.equal(actual, expected, `${state}: ${name}`);
    rendered[name] = actual;
  }
  results.push({ state, ...rendered, goods: fixture.goods.length, purchases: fixture.purchases.length });
}
// Direct missing/null field inputs validate the actual production defaults.
for (const status of [undefined, null, {}, { detectedCount: null, attemptedCount: null, succeededCount: null, lastResult: {} }]) {
  const fixture = { ...previewTradeFixture("automation-trade-positive"), status };
  assert.equal(textOf(productionRender(cloneStats, fixture)), textOf(originalRender(sourceStats, fixture)));
  assert.match(textOf(productionRender(cloneStats, fixture)), /Detected: 0Attempted: 0Succeeded: 0Last result: -/);
}
for (const state of ["automation-trade-loading", "automation-trade-loading-retained", "automation-trade-error", "automation-trade-error-retained"]) {
  const fixture = previewTradeFixture(state);
  assert.equal(fixture.purchases, previewTradePurchases, "template fetch must not erase independent purchases");
  assert.equal(fixture.status.lastResult.state, "success", "template error must not become a purchase result");
}
for (const state of ["automation-trade-loading-retained", "automation-trade-error-retained"]) assert.equal(previewTradeFixture(state).goods, previewTradeGoods);
assert.equal(previewTradeFixture("automation-trade-status-failed").error, "");
assert.equal(previewTradeFixture("automation-trade-save-error").error, "");
assert.equal(previewTradeFixture("automation-trade-error").error, "Error: Fixture Trade goods request failed");
for (const state of ["success", "confirmed_after_timeout", "failed", "skipped"]) assert.ok(en[`automation.tradeStation.state.${state}`]);
const effectAnchor = "o(e).then(e=>{t||ne(e.items)}).catch(e=>{t||b(String(e))}).finally(()=>{t||ae(!1)})";
const effectIndex = source.indexOf(effectAnchor); assert.ok(effectIndex >= 0);
const localePath = "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/en-BisSXcTB.js";
const localeSource = fs.readFileSync(path.join(repo, localePath), "utf8");
const localeAst = parse(localeSource, { sourceType: "module" });
const localeNodes = []; walk(localeAst, localeNodes);
const localeLocators = {};
for (const state of ["success", "confirmed_after_timeout", "failed", "skipped"]) {
  const key = `automation.tradeStation.state.${state}`;
  const property = localeNodes.find((node) => node.type === "ObjectProperty" && node.key.value === key);
  assert.ok(property);
  assert.equal(property.value.quasis[0].value.cooked, en[key]);
  localeLocators[key] = { utf8ByteOffset: Buffer.byteLength(localeSource.slice(0, property.start)), expression: localeSource.slice(property.start, property.end) };
}
const sourceLocators = Object.fromEntries(Object.entries(pairs).map(([name, [node]]) => [name, { utf8ByteOffset: Buffer.byteLength(source.slice(0, node.start)), expression: rawOriginal(node) }]));
sourceLocators.fetchEffect = { utf8ByteOffset: Buffer.byteLength(source.slice(0, effectIndex)), expression: effectAnchor };
const report = { task: "LWB317-UI-CORRECT-003E", result: "LWB317_UI_CORRECT003E_TRADE_PRESENTATION_OK", sourceHash: hash(source), results, missingStatusCases: 4, sourceLocators };
if (process.argv.includes("--record")) {
  fs.writeFileSync(path.join(here, "render-results.json"), JSON.stringify(report, null, 2) + "\n");
  fs.writeFileSync(path.join(here, "source-locators.json"), JSON.stringify({ task: report.task, reference: { path: sourcePath, sha256: report.sourceHash }, sourceLocators, locale: { path: localePath, sha256: hash(localeSource), locators: localeLocators }, resultTranslations: Object.fromEntries(Object.entries(en).filter(([key]) => key.startsWith("automation.tradeStation.state."))), limits: "All values are local QA fixtures. No native/template provider or purchase execution is implemented." }, null, 2) + "\n");
}
console.log(JSON.stringify({ result: report.result, scenarios: results.length, missingStatusCases: 4, sourceHash: report.sourceHash }, null, 2));
