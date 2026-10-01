import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
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
const sourceAst = parse(source, { sourceType: "module" });
const cloneAst = parse(pages, { sourceType: "module", plugins: ["jsx"] });
const hash = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();

assert.equal(hash(source), "44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6");

function fn(ast, name) {
  const node = ast.program.body
    .map((item) => item.declaration || item)
    .find((item) => item.type === "FunctionDeclaration" && item.id.name === name);
  assert.ok(node, `missing function ${name}`);
  return node;
}

const sourceNodes = Object.fromEntries(["Ir", "Lr", "Kr", "qr"].map((name) => [name, fn(sourceAst, name)]));
assert.equal(Buffer.byteLength(source.slice(0, sourceNodes.Ir.start)), 328453);
assert.equal(Buffer.byteLength(source.slice(0, sourceNodes.Lr.start)), 328684);
assert.equal(Buffer.byteLength(source.slice(0, sourceNodes.qr.start)), 336694);

const cloneNodes = Object.fromEntries(["translatedError", "previewHomeState", "HomePage"].map((name) => [name, fn(cloneAst, name)]));
const sourceCode = ["Ir", "Lr", "Kr", "qr"].map((name) => source.slice(sourceNodes[name].start, sourceNodes[name].end)).join("\n");
const cloneCode = transformSync(
  ["translatedError", "previewHomeState", "HomePage"].map((name) => pages.slice(cloneNodes[name].start, cloneNodes[name].end)).join("\n"),
  { loader: "jsx", jsxFactory: "h" },
).code;

const originalError = new Function(`${sourceCode}\nreturn Lr;`)();
const productionError = new Function(`${cloneCode}\nreturn translatedError;`)();
const tFor = (catalog) => (key, values = {}) => String(catalog[key] ?? key).replace(/\{(\w+)\}/g, (match, name) => String(values[name] ?? match));

const syntheticCatalog = {
  "error.QA_A": "error A",
  "error.qa_a": "lowercase object code",
  "auth.error.QA_B": "auth B",
  "update.error.QA_B": "update B",
  "auth.error.QA_C": "auth C",
  "update.error.QA_C": "update C",
  "update.error.QA_D": "update D",
  "common.actionFailed": "generic",
};
const syntheticT = tFor(syntheticCatalog);
const errorWithCode = Object.assign(new Error("QA_B QA_A"), { code: "QA_A" });
const cases = [
  ["string direct general namespace", "QA_A", "error A"],
  ["same-code auth beats update", "QA_C", "auth C"],
  ["update namespace fallback", "QA_D", "update D"],
  ["later code beats earlier code across namespaces", "QA_A QA_B", "auth B"],
  ["dedupe then reverse keeps last distinct priority", "QA_A QA_B QA_A", "auth B"],
  ["plain object code ignores message property", { code: "QA_A", message: "QA_B" }, "error A"],
  ["Error code plus message reverses distinct code priority", errorWithCode, "auth B"],
  ["Error message token extraction", new Error("prefix QA_A suffix"), "error A"],
  ["plain object message property is not Error.message", { message: "QA_A" }, "generic"],
  ["custom object stringification participates", { toString: () => "QA_A QA_B" }, "auth B"],
  ["object code field is not restricted by uppercase token regex", { code: "qa_a" }, "lowercase object code"],
  ["lowercase string does not match token regex", "qa_a", "generic"],
  ["unknown token uses localized generic key", "REVIEW_UNKNOWN_CODE", "generic"],
];

const caseResults = cases.map(([name, value, explicitExpected]) => {
  const expected = originalError(syntheticT, value);
  const actual = productionError(syntheticT, value);
  assert.equal(expected, explicitExpected, `${name}: source expectation`);
  assert.equal(actual, expected, `${name}: production mismatch`);
  return { name, expected, actual };
});

function findLocaleProperty(localeSource, key) {
  const ast = parse(localeSource, { sourceType: "module" });
  let found = null;
  const visit = (node) => {
    if (!node || typeof node !== "object" || found) return;
    if (node.type === "ObjectProperty") {
      const nodeKey = node.key.type === "StringLiteral" ? node.key.value : node.key.name;
      if (nodeKey === key) found = node;
    }
    for (const value of Object.values(node)) {
      if (Array.isArray(value)) value.forEach(visit);
      else if (value && typeof value === "object") visit(value);
    }
  };
  visit(ast);
  assert.ok(found, `missing locale property ${key}`);
  const value = found.value.type === "TemplateLiteral" && found.value.expressions.length === 0
    ? found.value.quasis[0].value.cooked
    : found.value.type === "StringLiteral"
      ? found.value.value
      : null;
  assert.notEqual(value, null, `unsupported locale value for ${key}`);
  return {
    value,
    utf8ByteOffset: Buffer.byteLength(localeSource.slice(0, found.start)),
    expression: localeSource.slice(found.start, found.end),
  };
}

const localeResults = {};
const assetDir = path.join(repo, "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets");
for (const language of ["en", "ja"]) {
  const files = fs.readdirSync(assetDir).filter((name) => name.startsWith(`${language}-`) && name.endsWith(".js"));
  assert.equal(files.length, 1, `${language} locale asset count`);
  const localePath = path.join(assetDir, files[0]);
  const localeSource = fs.readFileSync(localePath, "utf8");
  const { default: cloneCatalog } = await import(pathToFileURL(path.join(repo, `src/LWBridge.UI-0.3.17/src/locales/${language}.js`)));
  const common = findLocaleProperty(localeSource, "common.actionFailed");
  const recovery = findLocaleProperty(localeSource, "recovery.failedDetail");
  assert.equal(cloneCatalog["common.actionFailed"], common.value, `${language} common.actionFailed`);
  assert.equal(cloneCatalog["recovery.failedDetail"], recovery.value, `${language} recovery.failedDetail`);
  assert.equal(productionError(tFor(cloneCatalog), "REVIEW_UNKNOWN_CODE"), common.value, `${language} generic fallback`);
  localeResults[language] = {
    path: path.relative(repo, localePath).replaceAll("\\", "/"),
    sha256: hash(localeSource),
    commonActionFailed: common,
    recoveryFailedDetail: recovery,
  };
}

const h = (type, props, ...children) => ({ type, props: { ...props, ...(children.length ? { children } : {}) } });
const M = { jsx: h, jsxs: h };
function nodes(tree, out = []) {
  if (Array.isArray(tree)) tree.forEach((item) => nodes(item, out));
  else if (tree && typeof tree === "object") {
    out.push(tree);
    nodes(tree.props?.children, out);
  }
  return out;
}
function text(tree) {
  if (tree == null || typeof tree === "boolean") return "";
  if (Array.isArray(tree)) return tree.map(text).join("");
  if (typeof tree === "object") return text(tree.props?.children);
  return String(tree);
}
const errors = (tree) => nodes(tree).filter((node) => node.props?.className === "game-root-error").map(text);
const active = new Set(["waiting", "updating", "repairing", "launching", "verifying", "maintenance"]);
const { default: ja } = await import(pathToFileURL(path.join(repo, "src/LWBridge.UI-0.3.17/src/locales/ja.js")));
const jaT = tFor(ja);
const originalHome = new Function("M", "De", "Bn", `${sourceCode}\nreturn qr;`)(M, () => ({ t: jaT }), "source-toggle");
const productionHome = new Function("h", "useI18n", "ToggleRow", "RECOVERY_ACTIVE_STATES", `${cloneCode}\nreturn HomePage;`)(h, () => ({ t: jaT }), "clone-toggle", active);
const recoveryError = "REVIEW_UNKNOWN_CODE";
const originalTree = originalHome({
  gameRootStatus: { valid: true },
  proxyStatus: { gameRunning: false, repairRequired: false },
  gameRecoveryStatus: { state: "failed", error: recoveryError },
  autoLaunchGame: false,
  autoReconnect: false,
});
const productionTree = productionHome({
  homeState: {
    rootResolved: true,
    gameRootStatus: { valid: true },
    proxyStatus: { gameRunning: false, repairRequired: false },
    gameRecoveryStatus: { state: "failed", error: recoveryError },
    autoLaunchGame: false,
    autoReconnect: false,
    production: false,
  },
});
const sourceRecoveryErrors = errors(originalTree);
const productionRecoveryErrors = errors(productionTree);
assert.deepEqual(productionRecoveryErrors, sourceRecoveryErrors);
assert.deepEqual(productionRecoveryErrors, [ja["recovery.failedDetail"].replace("{error}", ja["common.actionFailed"])]);

const report = {
  task: "LWB317-REVIEW-HOME-ERROR-001",
  result: "REVIEW_HOME_ERROR_MATCH",
  reference: {
    path: sourcePath,
    sha256: hash(source),
    locators: Object.fromEntries(["Ir", "Lr", "qr"].map((name) => [name, {
      utf8ByteOffset: Buffer.byteLength(source.slice(0, sourceNodes[name].start)),
      expression: source.slice(sourceNodes[name].start, sourceNodes[name].end),
    }])),
  },
  production: {
    translatedError: pages.slice(cloneNodes.translatedError.start, cloneNodes.translatedError.end),
    recoveryExpression: pages.slice(cloneNodes.HomePage.start, cloneNodes.HomePage.end).match(/t\("recovery\.failedDetail"[^\n]+/)[0],
  },
  cases: caseResults,
  locales: localeResults,
  recoveryComposition: {
    language: "ja",
    input: recoveryError,
    expected: sourceRecoveryErrors,
    actual: productionRecoveryErrors,
  },
  limits: "Focused source/local review only. App rejection-to-string reduction, native producer contracts, original post-auth runtime pixels, and lifecycle behavior are outside this review.",
};

const recordPath = path.join(here, "independent-cases.json");
if (process.argv.includes("--record")) fs.writeFileSync(recordPath, `${JSON.stringify(report, null, 2)}\n`);
if (process.argv.includes("--verify-record")) assert.deepEqual(JSON.parse(fs.readFileSync(recordPath, "utf8")), report);
console.log(JSON.stringify({ result: report.result, cases: report.cases.length, locales: Object.keys(report.locales), recovery: report.recoveryComposition.actual }, null, 2));
