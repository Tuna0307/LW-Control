import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";
import en from "../../../../src/LWBridge.UI-0.3.17/src/locales/en.js";
import ja from "../../../../src/LWBridge.UI-0.3.17/src/locales/ja.js";
const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../..");
const require = createRequire(path.join(repo, "src/LWBridge.UI-0.3.17/package.json"));
const { parse } = require("@babel/parser");
const { transformSync } = require("esbuild");
const referencePath = "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js";
const source = fs.readFileSync(path.join(repo, referencePath), "utf8");
const pagesPath = "src/LWBridge.UI-0.3.17/src/Pages.jsx";
const pages = fs.readFileSync(path.join(repo, pagesPath), "utf8");
const digest = (text) => crypto.createHash("sha256").update(text).digest("hex").toUpperCase();
assert.equal(digest(source), "44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6");
const originalAst = parse(source, { sourceType: "module" });
const cloneAst = parse(pages, { sourceType: "module", plugins: ["jsx"] });
const declaration = (ast, name) => ast.program.body.map((entry) => entry.declaration || entry).find((entry) => entry.type === "FunctionDeclaration" && entry.id.name === name);
const localeEvidence = {};
for (const [language, catalog, filename] of [["en", en, "en-BisSXcTB.js"], ["ja", ja, "ja-UrbzJu-m.js"]]) {
  const localePath = `evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/${filename}`;
  const localeText = fs.readFileSync(path.join(repo, localePath), "utf8");
  const ast = parse(localeText, { sourceType: "module" });
  const nodes = [];
  const walk = (node) => { if (!node || typeof node !== "object") return; if (node.type) nodes.push(node); for (const value of Object.values(node)) { if (Array.isArray(value)) value.forEach(walk); else if (value && typeof value === "object") walk(value); } };
  walk(ast);
  const locators = {};
  for (const key of ["common.enabled", "common.disabled", "common.actionFailed"]) {
    const property = nodes.find((node) => node.type === "ObjectProperty" && node.key.value === key);
    assert.ok(property, `${language} ${key}`);
    assert.equal(property.value.quasis[0].value.cooked, catalog[key]);
    locators[key] = { utf8ByteOffset: Buffer.byteLength(localeText.slice(0, property.start)), expression: localeText.slice(property.start, property.end) };
  }
  localeEvidence[language] = { path: localePath, sha256: digest(localeText), locators };
}
const originalNames = ["Kr", "qr", "Ir", "Lr", "Bn"];
const cloneNames = ["Switch", "ToggleRow", "translatedError", "HomePage"];
const originalNodes = originalNames.map((name) => { const node = declaration(originalAst, name); assert.ok(node, name); return node; });
const cloneNodes = cloneNames.map((name) => { const node = declaration(cloneAst, name); assert.ok(node, name); return node; });
const originalCode = originalNodes.map((node) => source.slice(node.start, node.end)).join("\n");
const cloneCode = transformSync(cloneNodes.map((node) => pages.slice(node.start, node.end)).join("\n"), { loader: "jsx", jsxFactory: "h" }).code;
const active = new Set(["waiting", "updating", "repairing", "launching", "verifying", "maintenance"]);
const factory = (type, props, ...children) => typeof type === "function" ? type({ ...props, ...(children.length ? { children } : {}) }) : ({ type, props: { ...props, ...(children.length ? { children } : {}) } });
const M = { jsx: (type, props) => factory(type, props), jsxs: (type, props) => factory(type, props) };
const translator = (catalog) => (key, values = {}) => Object.entries(values).reduce((text, [name, value]) => text.replaceAll(`{${name}}`, String(value)), catalog[key] || key);
function renderers(catalog) {
  const t = translator(catalog);
  const original = new Function("De", "M", "zn", originalCode + "\nreturn qr;")(() => ({ t }), M, (props) => factory("original-switch", props));
  const clone = new Function("useI18n", "h", "previewHomeState", "RECOVERY_ACTIVE_STATES", cloneCode + "\nreturn HomePage;")(() => ({ t }), factory, () => null, active);
  return { original, clone };
}
function flatten(tree, output = []) {
  if (tree == null || typeof tree === "boolean") return output;
  if (Array.isArray(tree)) { tree.forEach((child) => flatten(child, output)); return output; }
  if (typeof tree === "object") { output.push(tree); flatten(tree.props.children, output); }
  return output;
}
function text(tree) {
  if (tree == null || typeof tree === "boolean") return "";
  if (Array.isArray(tree)) return tree.map(text).join("");
  if (typeof tree === "object") return text(tree.props.children);
  return String(tree);
}
function summarize(tree) {
  const nodes = flatten(tree);
  const title = nodes.find((node) => node.props.className === "panel-title");
  const status = flatten(title).find((node) => node.type === "span");
  const controls = nodes.find((node) => node.props.className === "game-controls");
  return {
    status: text(status),
    controls: flatten(controls).filter((node) => node.type === "button").map((node) => ({ text: text(node), disabled: !!node.props.disabled })),
    rootPicker: nodes.some((node) => node.props.className === "game-root-missing"),
    errors: nodes.filter((node) => node.props.className === "game-root-error").map(text),
    switches: nodes.filter((node) => node.props.role === "switch").map((node) => ({ ariaLabel: node.props["aria-label"], checked: node.props["aria-checked"], disabled: !!node.props.disabled })),
    rootText: text(nodes.find((node) => node.props.className === "game-root-missing")),
  };
}
function mappedState(props, busy) {
  return {
    rootResolved: props.gameRootStatus != null, gameRootStatus: props.gameRootStatus,
    proxyStatus: props.proxyStatus, online: props.online, gameRecoveryStatus: props.gameRecoveryStatus,
    busy, error: props.gameActionError || props.gameRootError || "",
    autoLaunchGame: props.autoLaunchGame, autoReconnect: props.autoReconnect, production: true,
  };
}
const english = renderers(en);
const combinations = [];
const differences = { status: [], buttonText: [], fence: [] };
for (const root of [null, { valid: false }, { valid: true }])
for (const gameRunning of [false, true])
for (const online of [false, true])
for (const repairRequired of [false, true])
for (const recovery of ["idle", "waiting", "updating", "repairing", "launching", "verifying", "maintenance", "failed"])
for (const busy of ["", "proxy", "launch", "gameRoot", "autoReconnect"]) {
  const input = { gameRootStatus: root, proxyStatus: { gameRunning, repairRequired }, online,
    gameRecoveryStatus: { state: recovery }, proxyBusy: busy === "proxy", gameLaunchBusy: busy === "launch",
    gameRootBusy: busy === "gameRoot", autoLaunchGame: false, autoReconnect: false };
  const expected = summarize(english.original(input));
  const actual = summarize(english.clone({ homeState: mappedState(input, busy) }));
  assert.equal(actual.rootPicker, expected.rootPicker);
  assert.equal(actual.controls.length, expected.controls.length);
  const entry = { input: { root: root?.valid ?? null, gameRunning, online, repairRequired, recovery, busy }, expected, actual };
  combinations.push(entry);
  if (expected.status !== actual.status) differences.status.push(entry);
  if (expected.controls.map((node) => node.text).join("|") !== actual.controls.map((node) => node.text).join("|")) differences.buttonText.push(entry);
  if (expected.controls.some((node, index) => node.disabled !== actual.controls[index].disabled)) differences.fence.push(entry);
}
assert.equal(combinations.length, 960);
assert.ok(differences.status.length); assert.ok(differences.buttonText.length); assert.ok(differences.fence.length);
const base = { gameRootStatus: { valid: true }, proxyStatus: { gameRunning: false }, gameRecoveryStatus: { state: "idle" }, autoLaunchGame: false, autoReconnect: false };
const unknownError = { ...base, gameActionError: "PM015_UNKNOWN_QA_ERROR" };
const errorComparison = { expected: summarize(english.original(unknownError)), actual: summarize(english.clone({ homeState: mappedState(unknownError, "") })) };
assert.notDeepEqual(errorComparison.actual.errors, errorComparison.expected.errors);
const twoErrors = { ...base, gameRootStatus: { valid: false }, gameRootError: "PM015_ROOT_QA_ERROR", gameActionError: "PM015_ACTION_QA_ERROR" };
const errorScope = { expected: summarize(english.original(twoErrors)), actual: summarize(english.clone({ homeState: mappedState(twoErrors, "") })) };
assert.equal(errorScope.expected.errors.length, 1); assert.equal(errorScope.actual.errors.length, 0);
const japanese = renderers(ja);
const ariaComparison = { expected: summarize(japanese.original(base)).switches, actual: summarize(japanese.clone({ homeState: mappedState(base, "") })).switches };
assert.notEqual(ariaComparison.expected[0].ariaLabel, ariaComparison.actual[0].ariaLabel);
const locators = Object.fromEntries(originalNodes.map((node) => [node.id.name, { utf8ByteOffset: Buffer.byteLength(source.slice(0, node.start)), expression: source.slice(node.start, node.end) }]));
const report = {
  task: "LWB317-PM-015", result: "LWB317_PM015_HOME_AUDIT_OK", baseline: "dd46685b2ba634cc0f70fe9f278d38ba3b166a1e",
  reference: { path: referencePath, sha256: digest(source), locators },
  clone: { path: pagesPath, sha256: digest(pages) }, localeEvidence, combinations: combinations.length,
  differences: Object.fromEntries(Object.entries(differences).map(([name, rows]) => [name, { count: rows.length, examples: rows.slice(0, 3) }])),
  errorComparison, errorScope, ariaComparison,
  limits: "State adapter is disclosed: proxy/root/launch busy map to one existing clone busy value; separate source errors map to the clone's single error. Synthetic combinations include hypothetical lifecycle states, not reachable/live assertions. Native button fencing is intentional and must remain until a real provider is proven. No native/gameplay or original runtime was invoked.",
};
if (process.argv.includes("--record")) fs.writeFileSync(path.join(here, "home-audit.json"), JSON.stringify(report, null, 2) + "\n");
if (process.argv.includes("--verify-record")) {
  const saved = JSON.parse(fs.readFileSync(path.join(here, "home-audit.json"), "utf8"));
  assert.deepEqual(saved, report);
  const bytes = Buffer.from(source);
  for (const locator of Object.values(saved.reference.locators)) assert.equal(bytes.subarray(locator.utf8ByteOffset, locator.utf8ByteOffset + Buffer.byteLength(locator.expression)).toString("utf8"), locator.expression);
  for (const locale of Object.values(saved.localeEvidence)) {
    const bytes = fs.readFileSync(path.join(repo, locale.path));
    assert.equal(digest(bytes), locale.sha256);
    for (const locator of Object.values(locale.locators)) assert.equal(bytes.subarray(locator.utf8ByteOffset, locator.utf8ByteOffset + Buffer.byteLength(locator.expression)).toString("utf8"), locator.expression);
  }
}
console.log(JSON.stringify({ result: report.result, combinations: report.combinations, differences: Object.fromEntries(Object.entries(differences).map(([key, rows]) => [key, rows.length])), findings: ["busy labels", "error translation", "separate error channels", "localized switch descriptions"], nativeFencingPreserved: true }, null, 2));
