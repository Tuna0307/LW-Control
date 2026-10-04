import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import assert from "node:assert/strict";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const ui = path.join(repo, "src/LWBridge.UI-0.3.17");
const { parse } = createRequire(path.join(ui, "package.json"))("@babel/parser");
const hash = (bytes) => crypto.createHash("sha256").update(bytes).digest("hex").toUpperCase();
const originals = ["MapDataPanel-B4GXEND2.js", "AutomationPanel-BJ0gIqFh.js", "SquadPanel-HC3-DJei.js"];
const currentFiles = ["MapDataPage.jsx", "MapRetainedGoodsFilter.jsx", "ScheduledPlunder.jsx", "DispatchAssistManual.jsx", "Pages.jsx"];
function inspect(file, original) {
  const bytes = fs.readFileSync(file), source = bytes.toString("utf8");
  const ast = parse(source, { sourceType: "module", plugins: original ? [] : ["jsx"] });
  const nodes = [], parent = new Map();
  function walk(node, owner = null) {
    if (!node || typeof node !== "object") return;
    if (node.type) { nodes.push(node); if (owner) parent.set(node, owner); }
    for (const value of Object.values(node)) {
      if (Array.isArray(value)) value.forEach((child) => walk(child, node));
      else if (value && typeof value === "object") walk(value, node);
    }
  }
  walk(ast);
  const importNode = ast.program.body.find((node) => node.type === "ImportDeclaration" && node.source.value.includes("GameAssetImage"));
  const alias = importNode?.specifiers[0]?.local.name;
  const calls = [];
  for (const node of nodes) {
    let props = null;
    if (original && node.type === "CallExpression" && node.arguments[0]?.name === alias && node.arguments[1]?.type === "ObjectExpression") {
      props = Object.fromEntries(node.arguments[1].properties.map((property) => [property.key.name || property.key.value, source.slice(property.value.start, property.value.end)]));
    } else if (!original && node.type === "JSXElement" && node.openingElement.name.name === alias) {
      props = Object.fromEntries(node.openingElement.attributes.filter((attribute) => attribute.type === "JSXAttribute").map((attribute) => [attribute.name.name, attribute.value === null ? "true" : attribute.value.type === "StringLiteral" ? JSON.stringify(attribute.value.value) : source.slice(attribute.value.expression.start, attribute.value.expression.end)]));
    } else if (!original && node.type === "CallExpression" && node.arguments[0]?.name === alias && node.arguments[1]?.type === "ObjectExpression") {
      props = Object.fromEntries(node.arguments[1].properties.map((property) => [property.key.name || property.key.value, source.slice(property.value.start, property.value.end)]));
    }
    if (!props) continue;
    const owner = parent.get(node);
    const condition = owner?.type === "LogicalExpression" && owner.operator === "&&" && owner.right === node ? source.slice(owner.left.start, owner.left.end)
      : owner?.type === "ConditionalExpression" && owner.consequent === node ? source.slice(owner.test.start, owner.test.end) : null;
    const locator = { utf8ByteOffset: Buffer.byteLength(source.slice(0, node.start)), utf8ByteLength: Buffer.byteLength(source.slice(node.start, node.end)), sha256: hash(source.slice(node.start, node.end)) };
    calls.push({ props, condition, ...locator });
  }
  return { file: path.relative(repo, file).replaceAll("\\", "/"), sha256: hash(bytes), calls };
}
const originalRecords = originals.map((name) => inspect(path.join(repo, "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets", name), true));
const currentRecords = currentFiles.map((name) => inspect(path.join(ui, "src", name), false));
const tradeSource = fs.readFileSync(path.join(repo, "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/AutomationPanel-BJ0gIqFh.js"), "utf8");
const resolverStart = tradeSource.indexOf("function C(e,t,n,r)");
const resolverEnd = tradeSource.indexOf("function ue(", resolverStart);
const resolverBytes = tradeSource.slice(resolverStart, resolverEnd);
const resolveTradeName = new Function(resolverBytes + ";return C;")();
function evaluate(expressions, context) {
  const names = Object.keys(context), values = Object.values(context);
  const props = Object.fromEntries(Object.entries(expressions).map(([key, expression]) => [key, new Function(...names, `return (${expression});`)(...values)]));
  delete props.key;
  return props;
}
const comparisons = [], issues = [];
for (const record of currentRecords) for (const call of record.calls) {
  const className = evaluate({ className: call.props.className }, {}).className;
  const originalRecord = originalRecords.find((entry) => entry.file.includes(className.startsWith("equipment-") ? "SquadPanel" : className.startsWith("trade-station-") || className.startsWith("automation-") || record.file.includes("DispatchAssistManual") ? "AutomationPanel" : "MapDataPanel"));
  const candidates = originalRecord.calls.filter((entry) => evaluate({ className: entry.props.className }, {}).className === className);
  assert.ok(candidates.length, "no source image caller for " + className);
  for (const name of ["Name", "", "<color=red>Name</color>"]) for (const quality of [0, 3]) for (const iconPath of ["", "icons/item.png"]) for (const gameTexts of [{}, { translationKey: "Localized" }]) {
    const item = { iconPath, currencyIconPath: iconPath, name, key: "item-key", nameKey: "translationKey", itemId: 7, quality, equipUuid: 7 };
    const resolved = name || "item-key";
    const tradeName = resolveTradeName(gameTexts, item.nameKey, item.name, item.itemId);
    const currentContext = { item, reward: item, purchase: item, equip: item, hero: item, selected: item, currency: item, e: item,
      e2: item, e3: item, name: resolved, itemName: tradeName, fixture: { gameTexts }, resolveTradeName };
    const currentProps = evaluate(call.props, currentContext);
    let match = null;
    for (const candidate of candidates) {
      const originalContext = { e: item, t: item, n: item, i: item, r: item, o: item, l: tradeName, a: tradeName };
      if (originalRecord.file.includes("MapDataPanel")) originalContext.n = resolved;
      const originalProps = evaluate(candidate.props, originalContext);
      const keys = [...new Set([...Object.keys(currentProps), ...Object.keys(originalProps)])].sort();
      const currentGuard = call.condition ? Boolean(evaluate({ guard: call.condition }, currentContext).guard) : null;
      const originalGuard = candidate.condition ? Boolean(evaluate({ guard: candidate.condition }, originalContext).guard) : null;
      if (keys.every((key) => currentProps[key] === originalProps[key]) && currentGuard === originalGuard) { match = candidate; break; }
    }
    const detail = { file: record.file, className, name, quality, iconPath, textOverride: Boolean(gameTexts.translationKey), currentByte: call.utf8ByteOffset };
    if (match) comparisons.push({ ...detail, originalByte: match.utf8ByteOffset, result: "PASS" });
    else issues.push({ ...detail, currentProps, result: "MISMATCH" });
  }
}
const contract = { original: originalRecords, current: currentRecords, originalTradeResolver: { utf8ByteOffset: Buffer.byteLength(tradeSource.slice(0, resolverStart)), utf8ByteLength: Buffer.byteLength(resolverBytes), sha256: hash(resolverBytes) }, comparisons, issues,
  requiredConditions: ["Trade Goods and Purchased frame use quality > 0 and cfm_tongyong_daojukuang_${quality}", "Currency-selection and Purchased currency images require currencyIconPath", "Trade Goods/Purchased frame+item icons and Purchased currency use deferUntilVisible:true", "Train/Assist reward alt uses name || key", "Equipment missing item remains a plain placeholder"],
  limits: "Exact source/canonical prop-expression execution with controlled field values; not whole renderer lifecycle, condition reachability, native asset availability, provider integration, original pixels, or gameplay proof. Only direct image caller guards are recorded. No native image operation is invoked." };
if (process.argv.includes("--record")) fs.writeFileSync(path.join(here, "caller-contract.json"), JSON.stringify(contract, null, 2) + "\n");
console.log(JSON.stringify({ marker: issues.length ? "LWB317_IMAGE_CALLERS_MISMATCH" : "LWB317_IMAGE_CALLERS_OK", originalCalls: originalRecords.reduce((sum, entry) => sum + entry.calls.length, 0), currentCalls: currentRecords.reduce((sum, entry) => sum + entry.calls.length, 0), comparisons: comparisons.length, issues }));
if (issues.length) process.exitCode = 1;
