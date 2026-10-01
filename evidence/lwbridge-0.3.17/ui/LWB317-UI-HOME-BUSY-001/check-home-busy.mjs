import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { createRequire } from "node:module";
import { fileURLToPath, pathToFileURL } from "node:url";
const here = path.dirname(fileURLToPath(import.meta.url)), repo = path.resolve(here, "../../../..");
const require = createRequire(path.join(repo, "src/LWBridge.UI-0.3.17/package.json"));
const { parse } = require("@babel/parser"), { transformSync } = require("esbuild");
const referencePath = "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js";
const source = fs.readFileSync(path.join(repo, referencePath), "utf8");
const pages = fs.readFileSync(path.join(repo, "src/LWBridge.UI-0.3.17/src/Pages.jsx"), "utf8").replace(/\r\n/g, "\n");
const app = fs.readFileSync(path.join(repo, "src/LWBridge.UI-0.3.17/src/App.jsx"), "utf8").replace(/\r\n/g, "\n");
const hash = bytes => crypto.createHash("sha256").update(bytes).digest("hex").toUpperCase();
assert.equal(hash(source), "44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6");
const originalAst = parse(source, { sourceType: "module" }), pageAst = parse(pages, { sourceType: "module", plugins: ["jsx"] });
function walk(node, output = []) { if (!node || typeof node !== "object") return output; if (node.type) output.push(node); for (const child of Object.values(node)) { if (Array.isArray(child)) child.forEach(value => walk(value, output)); else if (child && typeof child === "object") walk(child, output); } return output; }
const declaration = (ast, name) => { const node = ast.program.body.map(n => n.declaration || n).find(n => n.type === "FunctionDeclaration" && n.id.name === name); assert.ok(node, name); return node; };
const locator = node => ({ utf8ByteOffset: Buffer.byteLength(source.slice(0, node.start)), expression: source.slice(node.start, node.end) });
const originalNodes = ["Ir", "Lr", "Kr", "qr"].map(name => declaration(originalAst, name));
const parent = declaration(originalAst, "Gi"), parentNodes = walk(parent);
const caller = parentNodes.find(n => n.type === "CallExpression" && n.arguments[0]?.name === "qr"); assert.ok(caller);
const rootState = parentNodes.find(n => n.type === "VariableDeclarator" && n.id.type === "ArrayPattern" && n.id.elements[0]?.name === "_");
const proxyState = parentNodes.find(n => n.type === "VariableDeclarator" && n.id.type === "ArrayPattern" && n.id.elements[0]?.name === "p");
const launchProperty = walk(originalAst).find(n => n.type === "ObjectProperty" && n.key.name === "gameLaunchBusy" && n.value.type === "BinaryExpression");
assert.equal(source.slice(launchProperty.start, launchProperty.end), "gameLaunchBusy:u>0");
const homeNode = declaration(pageAst, "HomePage"), homeNodes = walk(homeNode);
const expression = name => { const node = homeNodes.find(n => n.type === "VariableDeclarator" && n.id.name === name); assert.ok(node, name); return pages.slice(node.init.start, node.init.end); };
assert.equal(expression("lifecycleProviderAvailable"), "false");
assert.equal(expression("rootBusy"), 'state.busy === "gameRoot"');
assert.equal(expression("proxyBusy"), "state.proxyBusy === true");
assert.equal(expression("launching"), "state.gameLaunchBusy === true");
const predicateNames = ["rootResolved", "rootValid", "gameRunning", "recovering", "repairRequired", "proxyBusy", "launching", "lifecycleProviderAvailable"];
const predicates = new Function(...predicateNames, `return {showRootPicker:${expression("showRootPicker")},canStart:${expression("canStart")},canStop:${expression("canStop")},repairOnClose:${expression("repairOnClose")}};`);
const originalCode = originalNodes.map(n => source.slice(n.start, n.end)).join("\n");
const cloneCode = transformSync(["translatedError", "previewHomeState", "HomePage"].map(name => { const node = declaration(pageAst, name); return pages.slice(node.start, node.end); }).join("\n"), { loader: "jsx", jsxFactory: "h" }).code;
const h = (type, props, ...children) => ({ type, props: { ...props, ...(children.length ? { children } : {}) } });
const M = { jsx: h, jsxs: h }, active = new Set(["waiting", "updating", "repairing", "launching", "verifying", "maintenance"]);
function nodes(tree, output = []) { if (Array.isArray(tree)) tree.forEach(n => nodes(n, output)); else if (tree && typeof tree === "object") { output.push(tree); nodes(tree.props.children, output); } return output; }
function text(tree) { if (tree == null || typeof tree === "boolean") return ""; if (Array.isArray(tree)) return tree.map(text).join(""); if (typeof tree === "object") return text(tree.props.children); return String(tree); }
function display(tree) {
  const flat = nodes(tree), title = flat.find(n => n.props.className === "panel-title"), controls = flat.find(n => n.props.className === "game-controls"), picker = flat.find(n => n.props.className === "game-root-missing"), status = nodes(title).find(n => n.type === "span");
  return { status: text(status), statusClass: status.props.className, controls: nodes(controls).filter(n => n.type === "button").map(n => ({ text: text(n), disabled: !!n.props.disabled })), repairHint: text(nodes(controls).find(n => n.props.className === "muted")), rootPicker: !!picker, pickerButton: picker ? nodes(picker).filter(n => n.type === "button").map(n => ({ text: text(n), disabled: !!n.props.disabled }))[0] : null };
}
const languages = ["en", "zh-CN", "zh-TW", "ja", "ko", "vi", "id", "ru", "pt"];
const localeResults = [], samples = [], fixtureResults = [];
let predicateCases = 0, renderComparisons = 0, preferencesChecks = 0;
const fixtureNames = ["home-launching", "home-proxy-busy-stopped", "home-proxy-busy-running", "home-proxy-busy-repair", "home-busy-overlap", "home-root-busy-missing", "home-root-busy-valid"];
for (const language of languages) {
  const { default: catalog } = await import(pathToFileURL(path.join(repo, `src/LWBridge.UI-0.3.17/src/locales/${language}.js`)));
  const t = (key, values = {}) => (catalog[key] || key).replace(/\{(\w+)\}/g, (match, name) => String(values[name] ?? match));
  const original = new Function("M", "De", "Bn", originalCode + "\nreturn {Home:qr, predicates:Kr};")(M, () => ({ t }), "original-switch");
  const clone = new Function("h", "useI18n", "ToggleRow", "RECOVERY_ACTIVE_STATES", cloneCode + "\nreturn {Home:HomePage, preview:previewHomeState};")(h, () => ({ t }), "clone-switch", active);
  let localeComparisons = 0;
  for (const root of [null, { valid: false }, { valid: true }]) for (const gameRunning of [false, true]) for (const online of [false, true]) for (const repairRequired of [false, true]) for (const recoveryState of ["idle", "waiting", "updating", "repairing", "launching", "verifying", "maintenance", "failed"]) for (const proxyBusy of [false, true]) for (const gameLaunchBusy of [false, true]) for (const gameRootBusy of [false, true]) {
    const input = { gameRootStatus: root, proxyStatus: { gameRunning, repairRequired }, online, gameRecoveryStatus: { state: recoveryState }, proxyBusy, gameLaunchBusy, gameRootBusy };
    const homeState = { ...input, rootResolved: root !== null, busy: gameRootBusy ? "gameRoot" : "", production: true };
    const expected = display(original.Home(input)), actual = display(clone.Home({ homeState }));
    assert.deepEqual({ ...actual, controls: actual.controls.map(({ text }) => text) }, { ...expected, controls: expected.controls.map(({ text }) => text) });
    assert.ok(actual.controls.every(n => n.disabled), "native lifecycle fencing must remain");
    renderComparisons++; localeComparisons++;
    if (language === "en" && !online && !gameRootBusy) {
      const expectedPredicates = original.predicates({ rootResolved: root !== null, rootValid: root?.valid === true, gameRunning, needsRepair: repairRequired, recovering: active.has(recoveryState), busy: proxyBusy, launching: gameLaunchBusy });
      const inputs = [root !== null, root?.valid === true, gameRunning, active.has(recoveryState), repairRequired, proxyBusy, gameLaunchBusy];
      assert.deepEqual(predicates(...inputs, true), expectedPredicates, "actual clone predicate expressions with isolated available-provider input");
      const fenced = predicates(...inputs, false); assert.equal(fenced.canStart, false); assert.equal(fenced.canStop, false); predicateCases++;
    }
    if (language === "en" && root?.valid && gameRunning && online && repairRequired && recoveryState === "idle") samples.push({ input, expected, actual });
  }
  for (const busy of ["autoLaunchGame", "autoReconnect", "gameRoot"]) {
    const tree = clone.Home({ homeState: { rootResolved: true, gameRootStatus: { valid: true }, proxyStatus: { gameRunning: false }, busy, production: true, autoLaunchGame: false, autoReconnect: false } });
    assert.equal(display(tree).status, t("setup.gameStopped"));
    const toggles = nodes(tree).filter(n => n.type === "clone-switch");
    assert.deepEqual(toggles.map(n => n.props.disabled), [busy === "autoLaunchGame", busy === "autoReconnect"]); preferencesChecks++;
  }
  for (const fixture of fixtureNames) {
    const preview = clone.preview(fixture); assert.equal(preview.production, false);
    const tree = clone.Home({ previewState: fixture });
    assert.ok(nodes(tree).filter(n => n.type === "button" || n.type === "clone-switch").every(n => n.props.disabled));
    fixtureResults.push({ language, fixture, actual: display(tree) });
  }
  localeResults.push({ language, comparisons: localeComparisons, pass: true });
}
const appAst = parse(app, { sourceType: "module", plugins: ["jsx"] }), appNodes = walk(declaration(appAst, "App"));
const busyWrites = appNodes.filter(n => n.type === "CallExpression" && n.callee.name === "setHomeBusy").map(n => n.arguments[0].value);
assert.deepEqual([...new Set(busyWrites)].sort(), ["", "autoLaunchGame", "autoReconnect", "gameRoot"]);
const homeState = appNodes.find(n => n.type === "JSXAttribute" && n.name.name === "homeState").value.expression;
assert.ok(!homeState.properties.some(n => ["proxyBusy", "gameLaunchBusy"].includes(n.key.name)), "no lifecycle busy producer invented");
assert.match(app, /previewState=\{backendBridge\.mode === "preview" \?[^\n]+: ""\}/, "native host cannot select preview fixtures");
const report = { task: "LWB317-UI-HOME-BUSY-001", result: "LWB317_HOME_BUSY001_OK", reference: { path: referencePath, sha256: hash(source), locators: Object.fromEntries([...originalNodes.map(n => [n.id.name, locator(n)]), ["HomeCaller", locator(caller)], ["RootBusyState", locator(rootState)], ["ProxyBusyState", locator(proxyState)], ["LaunchBusyProperty", locator(launchProperty)]]) }, counts: { renderComparisons, predicateCases, preferencesChecks, previewFixtures: fixtureResults.length }, localeResults, samples, fixtureResults, actualPredicates: Object.fromEntries(["rootBusy", "proxyBusy", "launching", "showRootPicker", "repairOnClose", "canStart", "canStop"].map(name => [name, expression(name)])), currentProducers: { busyWrites, rootSelection: 'existing App setHomeBusy("gameRoot") maps to rootBusy', proxyBusy: "no production producer; recovered render input only", gameLaunchBusy: "no production producer; recovered render input only", appSha256NormalizedLF: hash(app) }, limits: "Render/predicate combinations and explicit browser-preview fixtures only. Isolated predicate evaluation with lifecycleProviderAvailable=true is a test input; actual product lifecycleProviderAvailable remains false. No native/gameplay/auth function invoked; no live lifecycle reachability or original pixels proven." };
if (process.argv.includes("--record")) fs.writeFileSync(path.join(here, "busy-results.json"), JSON.stringify(report, null, 2) + "\n");
if (process.argv.includes("--verify-record")) assert.deepEqual(JSON.parse(fs.readFileSync(path.join(here, "busy-results.json"), "utf8")), report);
console.log(JSON.stringify({ result: report.result, ...report.counts }, null, 2));
