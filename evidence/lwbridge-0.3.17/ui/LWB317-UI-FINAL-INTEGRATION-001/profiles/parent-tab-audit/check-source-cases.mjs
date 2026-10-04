import fs from "node:fs";
import path from "node:path";
import vm from "node:vm";
import assert from "node:assert/strict";
import crypto from "node:crypto";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url)), repo = path.resolve(here, "../../../../../..");
const req = createRequire(path.join(repo, "src/LWBridge.UI-0.3.17/package.json"));
const { parse } = req("@babel/parser");
const contract = JSON.parse(fs.readFileSync(path.join(here, "source-contract.json"), "utf8"));
const sha = (text) => crypto.createHash("sha256").update(text).digest("hex").toUpperCase();
function walk(node, out = []) { if (!node || typeof node !== "object") return out; if (node.type) out.push(node); for (const value of Object.values(node)) if (Array.isArray(value)) value.forEach((child) => walk(child, out)); else if (value && typeof value === "object") walk(value, out); return out; }
function read(relativePath) { const text = fs.readFileSync(path.join(repo, relativePath), "utf8"); return { relativePath, text, sha256: sha(text), ast: parse(text, { sourceType: "module", plugins: ["jsx"] }) }; }
for (const item of [contract.original, ...Object.values(contract.original.components)]) {
  const actual = read(item.path); assert.equal(actual.sha256, item.sha256);
  const locators = item.declarations || item.parentStates;
  for (const locator of locators) assert.equal(Buffer.from(actual.text).subarray(locator.utf8ByteOffset, locator.utf8ByteOffset + locator.utf8ByteLength).toString(), locator.text);
}
const shapes = {
  automation: { file: "AutomationPage.jsx", entry: "AutomationPage", state: "category", default: "daily", chosen: "trade", currentSetter: "setCategory", selector: "B", stateSymbols: ["at", "ot"], controlledValue: "t", controlledCallback: "n", visited: "V", visitedSetter: "st" },
  squads: { file: "SquadsPage.jsx", entry: "SquadsPage", state: "tab", default: "afk", chosen: "equipment", currentSetter: "setTab", selector: "C", stateSymbols: ["_", "v"], controlledValue: "e", controlledCallback: "t", visited: "y", visitedSetter: "b" },
  map: { file: "MapDataPage.jsx", entry: "MapDataPage", state: "tab", default: "city", chosen: "train", currentSetter: "setTab", selector: "L", stateSymbols: ["$e", "I"], controlledValue: "r", controlledCallback: "i" },
};
const results = [], baseline = { proof: "Exact source expressions/callbacks and actual current initializers/props; not mounted React or native runtime proof", current: {} };
for (const [kind, shape] of Object.entries(shapes)) {
  const original = contract.original.components[kind];
  const current = read(`src/LWBridge.UI-0.3.17/src/${shape.file}`);
  const entry = walk(current.ast).find((node) => node.type === "FunctionDeclaration" && node.id.name === shape.entry);
  const state = walk(entry).find((node) => node.type === "VariableDeclarator" && node.id.type === "ArrayPattern" && node.id.elements[0].name === shape.state);
  const declaredProps = entry.params[0].properties.map((prop) => prop.key.name);
  const currentInitializer = current.text.slice(state.init.start, state.init.end);
  const currentDefault = vm.runInNewContext(currentInitializer, { useState: (value) => [typeof value === "function" ? value() : value, () => {}], equipmentPreview: false, previewState: "", PREVIEW_TAB_BY_STATE: {} })[0];
  assert.equal(currentDefault, shape.default);
  assert.ok(!declaredProps.includes(kind === "automation" ? "activeCategory" : "activeTab"));
  baseline.current[kind] = { path: current.relativePath, sha256: current.sha256, entryProps: declaredProps, stateDeclaration: current.text.slice(state.start, state.end), originalState: shape.default, selectedBeforeSwitch: shape.chosen, afterProfileKeyRemount: currentDefault };
  const selector = original.declarations.find((item) => item.text.startsWith(`${shape.selector}=`)).text.split("=").slice(1).join("=");
  for (const mode of ["single", "multi"]) {
    let parent = shape.default, local = shape.default, visited = new Set([shape.default]);
    const events = [];
    const context = { [shape.controlledValue]: mode === "multi" ? parent : undefined, [shape.stateSymbols[0]]: local, [shape.controlledCallback]: mode === "multi" ? (value) => { events.push("parent"); parent = value; } : undefined, [shape.stateSymbols[1]]: (value) => { events.push("local"); local = value; }, [shape.visitedSetter]: (mutator) => { events.push("visited"); visited = mutator(visited); }, e: shape.chosen };
    // Squad callback's e is the item iterator, not its component activeTab parameter.
    context.e = shape.chosen;
    if (kind === "map") Object.assign(context, { L: shape.default, T: { current: 0 }, Le: { current: 0 }, D: { current: new Map() }, B: 2, H: ["old-row"], en: 55, ye: (_cache, _out, _view, _incoming) => ({ page: 1, rows: [], total: 0 }), V: () => events.push("page"), U: () => events.push("rows"), W: () => events.push("total"), Zt: () => events.push("loading"), q: () => events.push("message") });
    const callback = vm.runInNewContext(`(${original.selectionCallback.text})`, context);
    callback(shape.chosen);
    assert.equal(mode === "multi" ? parent : local, shape.chosen);
    assert.equal(mode === "multi" ? local : parent, shape.default);
    if (kind !== "map") { assert.ok(visited.has(shape.chosen)); assert.deepEqual(events, ["visited", mode === "multi" ? "parent" : "local"]); }
    else assert.deepEqual(events, [mode === "multi" ? "parent" : "local", "page", "rows", "total", "loading", "message"]);
    results.push({ kind, case: `${mode}-exact-callback-ownership-order`, pass: true, events });
    const afterRemount = vm.runInNewContext(selector, { [shape.controlledValue]: mode === "multi" ? parent : undefined, [shape.stateSymbols[0]]: shape.default });
    assert.equal(afterRemount, mode === "multi" ? shape.chosen : shape.default);
    results.push({ kind, case: `${mode}-profile-key-remount`, pass: true, expected: afterRemount, currentBaseline: currentDefault, baselineMismatch: currentDefault !== afterRemount });
  }
  for (const provided of [shape.chosen, "unknown", null, undefined]) {
    const actual = vm.runInNewContext(selector, { [shape.controlledValue]: provided, [shape.stateSymbols[0]]: shape.default });
    assert.equal(actual, provided ?? shape.default);
    results.push({ kind, case: `exact-nullish-precedence-${String(provided)}`, pass: true, actual });
  }
  if (shape.visited) {
    const init = original.declarations.find((item) => item.text.startsWith(`[${shape.visited},`)).text.split("=").slice(1).join("=");
    const hook = { useState: (value) => [value(), () => {}] };
    const context = { x: kind === "automation" ? hook : "afk", O: hook, e: shape.chosen, B: shape.chosen };
    const values = vm.runInNewContext(init, context)[0];
    assert.deepEqual([...values], [shape.chosen]);
    results.push({ kind, case: "controlled-remount-visited-set", pass: true, initialVisited: [...values] });
  }
}
const app = read("src/LWBridge.UI-0.3.17/src/App.jsx");
const pageProps = walk(app.ast).find((node) => node.type === "VariableDeclarator" && node.id.name === "pageProps");
const props = pageProps.init.properties.map((node) => node.key.name);
for (const name of ["activeCategory", "onActiveCategoryChange", "activeTab", "onActiveTabChange"]) assert.ok(!props.includes(name));
const retained = walk(app.ast).find((node) => node.type === "FunctionDeclaration" && node.id.name === "RetainedPages");
assert.match(app.text.slice(retained.start, retained.end), /<Fragment key=\{selectedProfileId\}>/);
baseline.current.app = { path: app.relativePath, sha256: app.sha256, pageProps: props, keyedPages: app.text.slice(retained.start, retained.end) };
const output = { marker: "LWB317_PARENT_TAB_SOURCE_CASES_OK", cases: results.length, pass: results.every((item) => item.pass), distinguishingBaselineMismatches: results.filter((item) => item.baselineMismatch).length, results, limits: baseline.proof };
fs.writeFileSync(path.join(here, "audit-baseline.json"), JSON.stringify(baseline, null, 2) + "\n");
fs.writeFileSync(path.join(here, "source-cases.json"), JSON.stringify(output, null, 2) + "\n");
console.log(JSON.stringify({ marker: output.marker, cases: output.cases, distinguishingBaselineMismatches: output.distinguishingBaselineMismatches }));
