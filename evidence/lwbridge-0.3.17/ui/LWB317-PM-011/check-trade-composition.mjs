import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../..");
const ui = path.join(repo, "src/LWBridge.UI-0.3.17");
const require = createRequire(path.join(ui, "package.json"));
const { parse } = require("@babel/parser");
const panelBytes = fs.readFileSync(path.join(repo, "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/AutomationPanel-BJ0gIqFh.js"));
const panel = panelBytes.toString("utf8");
const sourceHash = crypto.createHash("sha256").update(panelBytes).digest("hex").toUpperCase();
assert.equal(sourceHash, "6DB2328191AF33E5631D507271C36D7008B287DAA7C8CD1A957B23EF6F871725");
const anchors = {
  firstCurrencyOffer: "for(let e of r)for(let t of e.offers)t.currencyId>0&&!s.has(t.currencyId)&&s.set(t.currencyId,t)",
  goodsCurrenciesById: "n.offers.forEach(e=>{ee.has(e.currencyId)||(ee.add(e.currencyId),u.push(C(a,e.currencyNameKey,e.currencyName,e.currencyId)))})",
};
const sourceLocators = {};
for (const [name, needle] of Object.entries(anchors)) {
  const index = panel.indexOf(needle);
  assert.notEqual(index, -1, name);
  sourceLocators[name] = Buffer.byteLength(panel.slice(0, index), "utf8");
}
const pages = fs.readFileSync(path.join(ui, "src/Pages.jsx"), "utf8");
const ast = parse(pages, { sourceType: "module", plugins: ["jsx"] });
const trade = ast.program.body.find((node) => node.type === "FunctionDeclaration" && node.id?.name === "TradeStationCard");
assert.ok(trade);
const expressions = {};
function visit(node) {
  if (!node || typeof node !== "object") return;
  if (node.type === "VariableDeclarator" && ["currencies", "possibleCurrencies"].includes(node.id?.name)) {
    expressions[node.id.name] = pages.slice(node.init.start, node.init.end);
  }
  for (const value of Object.values(node)) {
    if (Array.isArray(value)) value.forEach(visit);
    else if (value && typeof value === "object") visit(value);
  }
}
visit(trade.body);
assert.ok(expressions.currencies);
assert.ok(expressions.possibleCurrencies);
const composeCurrencies = new Function("fixture", `return (${expressions.currencies});`);
const composeNames = new Function("item", `return (${expressions.possibleCurrencies});`);
const cases = [
  {
    name: "currency selector keeps the first offer for a repeated currency ID",
    expected: [{ currencyId: 15, currencyName: "First QA name" }],
    actual: composeCurrencies({ goods: [
      { offers: [{ currencyId: 15, currencyName: "First QA name" }] },
      { offers: [{ currencyId: 15, currencyName: "Later QA name" }] },
    ] }),
  },
  {
    name: "goods currency labels keep the first label per currency ID",
    expected: ["First QA name"],
    actual: composeNames({ offers: [
      { currencyId: 15, currencyName: "First QA name" },
      { currencyId: 15, currencyName: "Later QA name" },
    ] }),
  },
  {
    name: "distinct currency IDs are retained even when labels match",
    expected: ["Shared QA name", "Shared QA name"],
    actual: composeNames({ offers: [
      { currencyId: 15, currencyName: "Shared QA name" },
      { currencyId: 650053, currencyName: "Shared QA name" },
    ] }),
  },
];
for (const test of cases) {
  try { assert.deepEqual(test.actual, test.expected); test.pass = true; }
  catch { test.pass = false; }
}
const report = { task: "LWB317-PM-011", sourceHash, sourceLocators, expressions, cases, pass: cases.every((test) => test.pass), note: "Synthetic QA metadata; not current-game currency claims. Actual production expressions are evaluated." };
const json = JSON.stringify(report, null, 2) + "\n";
if (process.argv[2]) fs.writeFileSync(path.resolve(process.argv[2]), json);
console.log(json);
process.exitCode = report.pass ? 0 : 1;
