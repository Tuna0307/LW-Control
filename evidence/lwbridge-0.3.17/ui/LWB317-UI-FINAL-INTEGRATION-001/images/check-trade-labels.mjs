import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import assert from "node:assert/strict";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";
import { execFileSync } from "node:child_process";

const here = path.dirname(fileURLToPath(import.meta.url)), repo = path.resolve(here, "../../../../.."), ui = path.join(repo, "src/LWBridge.UI-0.3.17");
const req = createRequire(path.join(ui, "package.json"));
const { parse } = req("@babel/parser"), { transformSync } = req("esbuild"), React = req("react"), S = req("react/jsx-runtime");
const { renderToStaticMarkup } = req("react-dom/server");
const { JSDOM } = createRequire(process.env.LWB317_JSDOM_PACKAGE || "C:/Users/chimw/AppData/Local/Temp/lwb317-shell-mount-deps/package.json")("jsdom");
const asset = fs.readFileSync(path.join(repo, "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/AutomationPanel-BJ0gIqFh.js"), "utf8");
const current = fs.readFileSync(path.join(ui, "src/Pages.jsx"), "utf8");
const baseline = execFileSync("git", ["show", "147e5cf:src/LWBridge.UI-0.3.17/src/Pages.jsx"], { cwd: repo, encoding: "utf8" });
const hash = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
const originalAst = parse(asset, { sourceType: "module" });
const declarations = Object.fromEntries(originalAst.program.body.filter((node) => node.type === "FunctionDeclaration").map((node) => [node.id.name, node]));
const originalText = (name) => asset.slice(declarations[name].start, declarations[name].end);
const resolveTradeName = new Function(originalText("C") + ";return C;")();
const t = (key) => key;
let captured;
function Image(props) { captured.push(props); return React.createElement("i", { "data-image-alt": props.alt }); }
const original = new Function("S", "b", "te", originalText("C") + "\n" + originalText("ue") + "\n" + originalText("fe") + ";return {goods:ue,currencies:fe};")(S, Image, () => ({ t }));
function findJsx(source, className) {
  const ast = parse(source, { sourceType: "module", plugins: ["jsx"] });
  let found;
  function walk(node) {
    if (!node || typeof node !== "object") return;
    if (node.type === "JSXElement" && node.openingElement.attributes.some((attribute) => attribute.name?.name === "className" && attribute.value?.value === className)) found = node;
    for (const value of Object.values(node)) Array.isArray(value) ? value.forEach(walk) : value && typeof value === "object" && walk(value);
  }
  walk(ast); assert.ok(found, className);
  return { text: source.slice(found.start, found.end), byte: Buffer.byteLength(source.slice(0, found.start)), hash: hash(source.slice(found.start, found.end)) };
}
function compile(source, kind) {
  const expression = findJsx(source, kind === "goods" ? "trade-station-good-grid" : "trade-station-currencies");
  const code = transformSync("return (" + expression.text + ");", { loader: "jsx", jsxFactory: "h", target: "es2022" }).code;
  return { expression, run(context) { return new Function("h", "GameAssetImage", ...Object.keys(context), code)(React.createElement, Image, ...Object.values(context)); } };
}
const compiled = { current: { goods: compile(current, "goods"), currencies: compile(current, "currencies") }, baseline: { goods: compile(baseline, "goods"), currencies: compile(baseline, "currencies") } };
const names = ["Raw", "", "<color=red>Raw</color>", "<Raw>"];
const results = [], baselineFailures = { goodsName: 0, possibleCurrencies: 0, currencyChoices: 0 };
for (const name of names) for (const override of [false, true]) {
  const texts = override ? { itemKey: "Localized item", currencyKey: "Localized currency" } : {};
  const offers = [
    { currencyId: 15, currencyName: name, currencyNameKey: "currencyKey", currencyIconPath: "currency.png", exclusive: false },
    { currencyId: 15, currencyName: "Ignored duplicate", currencyNameKey: "ignored", currencyIconPath: "ignored.png", exclusive: false },
    { currencyId: 9, currencyName: "Other", currencyNameKey: "other", currencyIconPath: "", exclusive: false },
  ];
  const goods = [{ itemId: 7, name, nameKey: "itemKey", iconPath: "item.png", quality: 3, offers }];
  const currencyMap = new Map();
  for (const item of goods) for (const offer of item.offers) if (offer.currencyId > 0 && !currencyMap.has(offer.currencyId)) currencyMap.set(offer.currencyId, offer);
  const currencies = [...currencyMap.values()].sort((a, b) => a.currencyId - b.currencyId);
  function render(kind, version) {
    captured = [];
    const context = { t, fixture: { gameTexts: texts }, config: { draft: { selectedItemIds: [7], selectedCurrencyIds: [9, 15] } }, visibleGoods: goods, currencies, resolveTradeName,
      toggleItem: () => {}, toggleCurrency: () => {} };
    const node = version === "original" ? kind === "goods"
      ? original.goods({ online: true, saving: false, showExclusive: true, selectedItemIds: [7], items: goods, gameTexts: texts, onToggle: () => {} })
      : original.currencies({ online: true, saving: false, selectedCurrencyIds: [9, 15], items: goods, gameTexts: texts, onToggle: () => {} })
      : compiled[version][kind].run(context);
    const html = renderToStaticMarkup(node), dom = new JSDOM(html).window.document;
    const output = kind === "goods" ? { goodsName: dom.querySelector(".trade-station-good-copy b").textContent,
      possibleCurrencies: dom.querySelectorAll(".trade-station-good-copy small")[1].textContent.replace("automation.tradeStation.possibleCurrencies", "").trim(),
      imageProps: captured }
      : { currencyChoices: [...dom.querySelectorAll("label > span")].map((element) => element.textContent), imageProps: captured };
    return output;
  }
  for (const kind of ["goods", "currencies"]) {
    const expected = render(kind, "original"), actual = render(kind, "current"), old = render(kind, "baseline");
    assert.deepEqual(actual, expected, JSON.stringify({ kind, name, override }));
    for (const field of ["goodsName", "possibleCurrencies", "currencyChoices"]) if (JSON.stringify(old[field]) !== JSON.stringify(expected[field])) baselineFailures[field]++;
    results.push({ kind, name, override, result: "PASS", original: expected, current: actual, baseline: old });
  }
}
for (const [field, count] of Object.entries(baselineFailures)) assert.ok(count > 0, "baseline distinguishes " + field);
const report = { marker: "LWB317_TRADE_IMAGE_LABELS_OK", cases: results.length, baselineFailures,
  original: { assetSha256: hash(asset), declarations: Object.fromEntries(["C", "ue", "fe"].map((name) => [name, { byte: Buffer.byteLength(asset.slice(0, declarations[name].start)), length: Buffer.byteLength(originalText(name)), sha256: hash(originalText(name)) }])) },
  current: { PagesSha256: hash(current), expressions: Object.fromEntries(Object.entries(compiled.current).map(([name, value]) => [name, value.expression])) },
  baseline: { commit: "147e5cf", PagesSha256: hash(baseline), expressions: Object.fromEntries(Object.entries(compiled.baseline).map(([name, value]) => [name, value.expression])) }, results,
  limits: "Actual exact original Goods/currency renderers and actual extracted canonical JSX executed with source C, React and inert image/control bindings. No native asset/provider/game action, full page effects or original pixels. Controlled DOM parser only observes rendered labels and captured source-shaped image props." };
if (process.argv.includes("--record")) fs.writeFileSync(path.join(here, "trade-label-results.json"), JSON.stringify(report, null, 2) + "\n");
console.log(JSON.stringify({ marker: report.marker, cases: results.length, baselineFailures }));
