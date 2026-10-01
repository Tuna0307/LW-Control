import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { createRequire } from "node:module";
import { fileURLToPath, pathToFileURL } from "node:url";
const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../..");
const require = createRequire(path.join(repo, "src/LWBridge.UI-0.3.17/package.json"));
const { parse } = require("@babel/parser");
const { transformSync } = require("esbuild");
const sourcePath = "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js";
const source = fs.readFileSync(path.join(repo, sourcePath), "utf8");
const pages = fs.readFileSync(path.join(repo, "src/LWBridge.UI-0.3.17/src/Pages.jsx"), "utf8");
const app = fs.readFileSync(path.join(repo, "src/LWBridge.UI-0.3.17/src/App.jsx"), "utf8");
assert.match(app, /previewState=\{backendBridge\.mode === "preview" \?[^\n]+: ""\}/, "native mode cannot select Home QA fixtures");
const hash = (bytes) => crypto.createHash("sha256").update(bytes).digest("hex").toUpperCase();
assert.equal(hash(source), "44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6");
const sourceAst = parse(source, { sourceType: "module" });
const cloneAst = parse(pages, { sourceType: "module", plugins: ["jsx"] });
const sharedNodes = ["jr", "Mr", "Nr", "Pr"].map((name) => {
  const declaration = sourceAst.program.body.filter((node) => node.type === "VariableDeclaration").flatMap((node) => node.declarations).find((node) => node.id.name === name);
  assert.ok(declaration, name); return declaration;
});
const [languages, generalErrors, authErrors, updateErrors] = sharedNodes.map((node) => new Function(`return (${source.slice(node.init.start, node.init.end)});`)());
const sharedMessages = Object.fromEntries(languages.map((language, index) => [language, Object.fromEntries([["error", generalErrors], ["auth.error", authErrors], ["update.error", updateErrors]].flatMap(([namespace, table]) => Object.entries(table).map(([code, values]) => [`${namespace}.${code}`, values[index]])))]));
const fn = (ast, name) => { const node = ast.program.body.map((node) => node.declaration || node).find((node) => node.type === "FunctionDeclaration" && node.id.name === name); assert.ok(node, name); return node; };
const sourceNames = ["Ir", "Lr", "Kr", "qr"];
const sourceNodes = sourceNames.map((name) => fn(sourceAst, name));
const sourceCode = sourceNodes.map((node) => source.slice(node.start, node.end)).join("\n");
const cloneNodes = ["translatedError", "previewHomeState", "HomePage"].map((name) => fn(cloneAst, name));
const cloneCode = transformSync(cloneNodes.map((node) => pages.slice(node.start, node.end)).join("\n"), { loader: "jsx", jsxFactory: "h" }).code;
const originalError = new Function(sourceCode + "\nreturn Lr;")();
const currentError = new Function(cloneCode + "\nreturn translatedError;")();
const tFor = (catalog) => (key, values = {}) => (catalog[key] || key).replace(/\{(\w+)\}/g, (match, name) => String(values[name] ?? match));
const codedError = Object.assign(new Error("QA_B QA_A"), { code: "QA_A" });
const cases = [
  ["undefined", undefined], ["null", null], ["empty", ""], ["false", false], ["zero", 0],
  ["unknown", "HOME_ERROR001_UNKNOWN_QA"], ["plain message", "Something went wrong"],
  ["direct code", "QA_A"], ["embedded code", "Failure: QA_B while waiting"],
  ["two codes", "QA_A QA_B"], ["duplicates before reverse", "QA_A QA_B QA_A"],
  ["object code", { code: "QA_A", message: "QA_B" }], ["object message only", { message: "QA_A" }],
  ["non-string object code", { code: 7 }], ["Error message", new Error("Failure: QA_B")],
  ["Error code and message", codedError], ["custom toString", { toString: () => "QA_A QA_B" }],
  ["lowercase code", "qa_a"], ["short tokens", "AB A"], ["boundary", "xQA_Ax"],
];
const catalogs = [
  { "error.QA_A": "first error", "error.QA_B": "second error", "auth.error.QA_A": "first auth", "update.error.QA_A": "first update", "common.actionFailed": "generic" },
  { "auth.error.QA_A": "auth", "update.error.QA_A": "update", "common.actionFailed": "generic" },
  { "update.error.QA_A": "update", "common.actionFailed": "generic" },
];
const syntheticResults = [];
for (const [catalogIndex, catalog] of catalogs.entries()) for (const [name, value] of cases) {
  const t = tFor(catalog); const expected = originalError(t, value); const actual = currentError(t, value);
  assert.equal(actual, expected, `${catalogIndex} ${name}`);
  syntheticResults.push({ catalogIndex, name, expected, actual });
}
assert.equal(currentError(tFor(catalogs[0]), "QA_A QA_B QA_A"), "second error");
assert.equal(currentError(tFor(catalogs[0]), { code: "QA_A", message: "QA_B" }), "first error");
assert.equal(currentError(tFor(catalogs[1]), "QA_A"), "auth");
assert.equal(currentError(tFor(catalogs[2]), "QA_A"), "update");
const h = (type, props, ...children) => ({ type, props: { ...props, ...(children.length ? { children } : {}) } });
const M = { jsx: h, jsxs: h };
function nodes(tree, result = []) { if (Array.isArray(tree)) tree.forEach((child) => nodes(child, result)); else if (tree && typeof tree === "object") { result.push(tree); nodes(tree.props.children, result); } return result; }
function text(tree) { if (tree == null || typeof tree === "boolean") return ""; if (Array.isArray(tree)) return tree.map(text).join(""); if (typeof tree === "object") return text(tree.props.children); return String(tree); }
const errors = (tree) => nodes(tree).filter((node) => node.props.className === "game-root-error").map(text);
const active = new Set(["waiting", "updating", "repairing", "launching", "verifying", "maintenance"]);
const localeResults = []; const localeEvidence = {};
const assets = path.join(repo, "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets");
for (const language of ["en", "zh-CN", "zh-TW", "ja", "ko", "vi", "id", "ru", "pt"]) {
  const { default: catalog } = await import(pathToFileURL(path.join(repo, `src/LWBridge.UI-0.3.17/src/locales/${language}.js`)));
  const t = tFor(catalog);
  const localeFile = fs.readdirSync(assets).filter((name) => name.startsWith(`${language}-`) && name.endsWith(".js")); assert.equal(localeFile.length, 1);
  const localeSource = fs.readFileSync(path.join(assets, localeFile[0]), "utf8");
  const localeAst = parse(localeSource, { sourceType: "module" }); const properties = [];
  const localeObject = localeAst.program.body.filter((node) => node.type === "VariableDeclaration").flatMap((node) => node.declarations).find((node) => node.id.name === "t"); assert.ok(localeObject);
  const originalMessages = new Function("e", `return (${localeSource.slice(localeObject.init.start, localeObject.init.end)});`)(sharedMessages);
  assert.deepEqual(catalog, originalMessages, `${language} full shared-plus-local composition`);
  const visit = (node) => { if (!node || typeof node !== "object") return; if (node.type === "ObjectProperty") properties.push(node); for (const value of Object.values(node)) { if (Array.isArray(value)) value.forEach(visit); else if (value && typeof value === "object") visit(value); } }; visit(localeAst);
  const locators = {};
  for (const key of ["common.actionFailed", "recovery.failedDetail"]) {
    const property = properties.find((node) => node.key.value === key); assert.ok(property, `${language} ${key}`);
    assert.equal(property.value.quasis[0].value.cooked, catalog[key]);
    locators[key] = { utf8ByteOffset: Buffer.byteLength(localeSource.slice(0, property.start)), expression: localeSource.slice(property.start, property.end) };
  }
  localeEvidence[language] = { path: `evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/${localeFile[0]}`, sha256: hash(localeSource), locators };
  let checks = 0;
  for (const [name, value] of cases) { assert.equal(currentError(t, value), originalError(t, value), `${language} ${name}`); checks++; }
  for (const key of Object.keys(catalog).filter((key) => /^(error|auth\.error|update\.error)\./.test(key))) {
    const code = key.replace(/^(error|auth\.error|update\.error)\./, "");
    for (const value of [code, new Error(`QA fixture: ${code}`), { code }]) { assert.equal(currentError(t, value), originalError(t, value)); checks++; }
  }
  const originalHome = new Function("M", "De", "Bn", sourceCode + "\nreturn qr;")(M, () => ({ t }), "source-toggle");
  const cloneHome = new Function("h", "useI18n", "ToggleRow", "RECOVERY_ACTIVE_STATES", cloneCode + "\nreturn HomePage;")(h, () => ({ t }), "clone-toggle", active);
  const renderResults = [];
  for (const stateName of ["home-error-unknown", "home-error-embedded", "home-recovery-error-unknown"]) {
    const preview = new Function(cloneCode + "\nreturn previewHomeState;")()(stateName);
    const expected = errors(originalHome({ gameRootStatus: preview.gameRootStatus, proxyStatus: preview.proxyStatus, gameRecoveryStatus: preview.gameRecoveryStatus, gameActionError: preview.gameActionError, autoLaunchGame: false, autoReconnect: false }));
    const actual = errors(cloneHome({ previewState: stateName })); assert.deepEqual(actual, expected, `${language} ${stateName}`);
    renderResults.push({ stateName, expected, actual });
  }
  assert.deepEqual(errors(cloneHome({ previewState: "home-connected" })), [], "no spurious error when input absent");
  for (const error of ["", "HOME_ERROR001_UNKNOWN_QA"]) {
    const state = { rootResolved: true, gameRootStatus: { valid: false }, gameRecoveryStatus: { state: "idle" }, gameRootError: error, production: false };
    const expectedTree = originalHome({ gameRootStatus: state.gameRootStatus, gameRootError: error });
    const actualTree = cloneHome({ homeState: state });
    const rootText = (tree) => text(nodes(tree).find((node) => node.props.className === "game-root-missing"));
    assert.equal(rootText(actualTree), rootText(expectedTree)); assert.deepEqual(errors(actualTree), []);
  }
  localeResults.push({ language, helperChecks: checks, renderResults, noErrorGuard: "PASS", missingRootErrorGuards: "PASS" });
}
const report = { task: "LWB317-UI-HOME-ERROR-001", result: "LWB317_HOME_ERROR001_OK", reference: { path: sourcePath, sha256: hash(source), locators: Object.fromEntries([...sourceNodes.map((node) => [node.id.name, { utf8ByteOffset: Buffer.byteLength(source.slice(0, node.start)), expression: source.slice(node.start, node.end) }]), ...sharedNodes.map((node) => [node.id.name, { utf8ByteOffset: Buffer.byteLength(source.slice(0, node.start)), expression: source.slice(node.start, node.end) }])]) }, localeEvidence, syntheticResults, localeResults, limits: "Actual helper and Home render expressions; synthetic QA only. No state-channel refactor, producer/native proof or original runtime pixels." };
if (process.argv.includes("--record")) fs.writeFileSync(path.join(here, "helper-render-results.json"), JSON.stringify(report, null, 2) + "\n");
if (process.argv.includes("--verify-record")) assert.deepEqual(JSON.parse(fs.readFileSync(path.join(here, "helper-render-results.json"), "utf8")), report);
console.log(JSON.stringify({ result: report.result, syntheticChecks: syntheticResults.length, localeChecks: localeResults.reduce((total, row) => total + row.helperChecks, 0), locales: localeResults.length, homeRenderStates: localeResults.reduce((total, row) => total + row.renderResults.length, 0) }, null, 2));
