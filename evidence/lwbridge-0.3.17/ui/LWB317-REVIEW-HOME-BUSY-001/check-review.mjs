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

const referencePath = "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js";
const pagesPath = "src/LWBridge.UI-0.3.17/src/Pages.jsx";
const appPath = "src/LWBridge.UI-0.3.17/src/App.jsx";
const historicalDir = path.join(repo, "evidence/lwbridge-0.3.17/ui/LWB317-UI-HOME-BUSY-001");
const source = fs.readFileSync(path.join(repo, referencePath), "utf8");
const pages = fs.readFileSync(path.join(repo, pagesPath), "utf8").replace(/\r\n/g, "\n");
const app = fs.readFileSync(path.join(repo, appPath), "utf8").replace(/\r\n/g, "\n");
const hash = value => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();

assert.equal(hash(source), "44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6");

function walk(node, output = []) {
  if (!node || typeof node !== "object") return output;
  if (node.type) output.push(node);
  for (const value of Object.values(node)) {
    if (Array.isArray(value)) value.forEach(child => walk(child, output));
    else if (value && typeof value === "object") walk(value, output);
  }
  return output;
}

function declaration(ast, name) {
  const node = ast.program.body.map(item => item.declaration || item)
    .find(item => item.type === "FunctionDeclaration" && item.id?.name === name);
  assert.ok(node, `missing function ${name}`);
  return node;
}

function sourceLocator(node) {
  return {
    utf8ByteOffset: Buffer.byteLength(source.slice(0, node.start)),
    expression: source.slice(node.start, node.end),
  };
}

const originalAst = parse(source, { sourceType: "module" });
const pageAst = parse(pages, { sourceType: "module", plugins: ["jsx"] });
const appAst = parse(app, { sourceType: "module", plugins: ["jsx"] });
const krNode = declaration(originalAst, "Kr");
const qrNode = declaration(originalAst, "qr");
const irNode = declaration(originalAst, "Ir");
const lrNode = declaration(originalAst, "Lr");
const giNode = declaration(originalAst, "Gi");
const giNodes = walk(giNode);
const callerNode = giNodes.find(node => node.type === "CallExpression" && node.arguments[0]?.name === "qr");
const rootStateNode = giNodes.find(node => node.type === "VariableDeclarator" && node.id.type === "ArrayPattern" && node.id.elements[0]?.name === "_");
const proxyStateNode = giNodes.find(node => node.type === "VariableDeclarator" && node.id.type === "ArrayPattern" && node.id.elements[0]?.name === "p");
const launchPropertyNode = walk(originalAst).find(node => node.type === "ObjectProperty" && node.key?.name === "gameLaunchBusy" && node.value?.type === "BinaryExpression");
assert.ok(callerNode && rootStateNode && proxyStateNode && launchPropertyNode);

const sourceLocators = {
  Kr: sourceLocator(krNode),
  qr: sourceLocator(qrNode),
  HomeCaller: sourceLocator(callerNode),
  RootBusyState: sourceLocator(rootStateNode),
  ProxyBusyState: sourceLocator(proxyStateNode),
  LaunchBusyProperty: sourceLocator(launchPropertyNode),
};
assert.deepEqual(Object.fromEntries(Object.entries(sourceLocators).map(([key, value]) => [key, value.utf8ByteOffset])), {
  Kr: 336469,
  qr: 336694,
  HomeCaller: 373291,
  RootBusyState: 361581,
  ProxyBusyState: 361529,
  LaunchBusyProperty: 248345,
});
assert.equal(sourceLocators.LaunchBusyProperty.expression, "gameLaunchBusy:u>0");

const homeNode = declaration(pageAst, "HomePage");
const homeNodes = walk(homeNode);
function currentExpression(name) {
  const node = homeNodes.find(item => item.type === "VariableDeclarator" && item.id?.name === name);
  assert.ok(node, `missing HomePage expression ${name}`);
  return pages.slice(node.init.start, node.init.end);
}
const currentExpressions = Object.fromEntries([
  "rootBusy", "proxyBusy", "launching", "showRootPicker", "repairOnClose",
  "lifecycleProviderAvailable", "canStart", "canStop",
].map(name => [name, currentExpression(name)]));
assert.deepEqual(currentExpressions, {
  rootBusy: 'state.busy === "gameRoot"',
  proxyBusy: "state.proxyBusy === true",
  launching: "state.gameLaunchBusy === true",
  showRootPicker: "rootResolved && !rootValid",
  repairOnClose: "rootResolved && rootValid && gameRunning && repairRequired && !recovering",
  lifecycleProviderAvailable: "false",
  canStart: "lifecycleProviderAvailable && rootResolved && rootValid && !gameRunning && !recovering && !proxyBusy && !launching",
  canStop: "lifecycleProviderAvailable && rootResolved && rootValid && (gameRunning || recovering) && !proxyBusy && !launching",
});

const originalCode = [irNode, lrNode, krNode, qrNode].map(node => source.slice(node.start, node.end)).join("\n");
const currentCode = transformSync(["translatedError", "previewHomeState", "HomePage"].map(name => {
  const node = declaration(pageAst, name);
  return pages.slice(node.start, node.end);
}).join("\n"), { loader: "jsx", jsxFactory: "h" }).code;
const h = (type, props, ...children) => ({ type, props: { ...(props || {}), ...(children.length ? { children } : {}) } });
const M = { jsx: h, jsxs: h };
const activeRecoveryStates = new Set(["waiting", "updating", "repairing", "launching", "verifying", "maintenance"]);

function flatten(tree, output = []) {
  if (Array.isArray(tree)) tree.forEach(child => flatten(child, output));
  else if (tree && typeof tree === "object") {
    output.push(tree);
    flatten(tree.props?.children, output);
  }
  return output;
}
function text(tree) {
  if (tree == null || typeof tree === "boolean") return "";
  if (Array.isArray(tree)) return tree.map(text).join("");
  if (typeof tree === "object") return text(tree.props?.children);
  return String(tree);
}
function display(tree) {
  const nodes = flatten(tree);
  const title = nodes.find(node => node.props?.className === "panel-title");
  const status = flatten(title).find(node => node.type === "span");
  const controls = nodes.find(node => node.props?.className === "game-controls");
  const picker = nodes.find(node => node.props?.className === "game-root-missing");
  const switches = nodes.filter(node => node.type === "original-switch" || node.type === "clone-switch");
  return {
    status: status ? text(status) : "",
    statusClass: status?.props?.className || "",
    controls: controls ? flatten(controls).filter(node => node.type === "button").map(node => ({ text: text(node), disabled: !!node.props.disabled })) : [],
    picker: picker ? flatten(picker).filter(node => node.type === "button").map(node => ({ text: text(node), disabled: !!node.props.disabled })) : [],
    repairHint: controls ? text(flatten(controls).find(node => node.props?.className === "muted")) : "",
    switches: switches.map(node => ({ text: text(node.props?.label), disabled: !!node.props?.disabled })),
  };
}

const { default: catalog } = await import(pathToFileURL(path.join(repo, "src/LWBridge.UI-0.3.17/src/locales/en.js")));
const t = (key, values = {}) => (catalog[key] || key).replace(/\{(\w+)\}/g, (match, name) => String(values[name] ?? match));
const original = new Function("M", "De", "Bn", `${originalCode}\nreturn { Home: qr, predicates: Kr };`)(M, () => ({ t }), "original-switch");
const current = new Function("h", "useI18n", "ToggleRow", "RECOVERY_ACTIVE_STATES", `${currentCode}\nreturn { Home: HomePage };`)(h, () => ({ t }), "clone-switch", activeRecoveryStates);
const currentPredicates = new Function(
  "rootResolved", "rootValid", "gameRunning", "recovering", "repairRequired", "proxyBusy", "launching", "lifecycleProviderAvailable",
  `return {showRootPicker:${currentExpressions.showRootPicker},repairOnClose:${currentExpressions.repairOnClose},canStart:${currentExpressions.canStart},canStop:${currentExpressions.canStop}};`,
);

const cases = [
  { name: "root-unresolved", root: null, expected: { statusKey: "setup.checking", showRootPicker: false, canStart: false, canStop: false, repairOnClose: false, controls: [] } },
  { name: "root-invalid-idle", root: { valid: false, root: "" }, expected: { statusKey: "setup.gameRootMissing", showRootPicker: true, canStart: false, canStop: false, repairOnClose: false, pickerKey: "setup.gameRootSelect", pickerDisabled: false, controls: [] } },
  { name: "root-invalid-folder-busy", root: { valid: false, root: "" }, currentBusy: "gameRoot", gameRootBusy: true, expected: { statusKey: "setup.gameRootMissing", showRootPicker: true, canStart: false, canStop: false, repairOnClose: false, pickerKey: "common.processing", pickerDisabled: true, controls: [] } },
  { name: "root-valid-folder-busy", root: { valid: true }, currentBusy: "gameRoot", gameRootBusy: true, expected: { statusKey: "setup.gameStopped", showRootPicker: false, canStart: true, canStop: false, repairOnClose: false, controls: ["top.launchGame", "setup.closeGameAction"] } },
  { name: "stopped-proxy-busy", root: { valid: true }, proxyBusy: true, expected: { statusKey: "common.processing", showRootPicker: false, canStart: false, canStop: false, repairOnClose: false, controls: ["common.processing", "setup.closeGameAction"] } },
  { name: "running-proxy-busy-offline", root: { valid: true }, gameRunning: true, proxyBusy: true, expected: { statusKey: "common.processing", showRootPicker: false, canStart: false, canStop: false, repairOnClose: false, controls: ["common.processing", "common.processing"] } },
  { name: "running-proxy-busy-online", root: { valid: true }, gameRunning: true, online: true, proxyBusy: true, expected: { statusKey: "common.processing", showRootPicker: false, canStart: false, canStop: false, repairOnClose: false, controls: ["common.processing", "common.processing"] } },
  { name: "stopped-launch-busy", root: { valid: true }, launching: true, expected: { statusKey: "setup.launchingGame", showRootPicker: false, canStart: false, canStop: false, repairOnClose: false, controls: ["setup.launchingGame", "setup.closeGameAction"] } },
  { name: "overlap-launch-proxy-running", root: { valid: true }, gameRunning: true, proxyBusy: true, launching: true, expected: { statusKey: "setup.launchingGame", showRootPicker: false, canStart: false, canStop: false, repairOnClose: false, controls: ["setup.launchingGame", "common.processing"] } },
  { name: "running-repair", root: { valid: true }, gameRunning: true, repairRequired: true, expected: { statusKey: "setup.repairRequired", showRootPicker: false, canStart: false, canStop: true, repairOnClose: true, controls: ["setup.updateAndLaunch"], repairHintKey: "setup.updateCloseGame" } },
  { name: "running-repair-proxy-busy", root: { valid: true }, gameRunning: true, repairRequired: true, proxyBusy: true, expected: { statusKey: "common.processing", showRootPicker: false, canStart: false, canStop: false, repairOnClose: true, controls: ["common.processing"], repairHintKey: "setup.updateCloseGame" } },
  { name: "running-recovery", root: { valid: true }, gameRunning: true, recoveryState: "waiting", expected: { statusKey: "recovery.state.waiting", showRootPicker: false, canStart: false, canStop: true, repairOnClose: false, controls: ["top.launchGame", "setup.closeGameAction"] } },
  { name: "repair-suppressed-by-recovery", root: { valid: true }, gameRunning: true, repairRequired: true, recoveryState: "waiting", expected: { statusKey: "recovery.state.waiting", showRootPicker: false, canStart: false, canStop: true, repairOnClose: false, controls: ["top.launchGame", "setup.closeGameAction"] } },
  { name: "running-offline", root: { valid: true }, gameRunning: true, online: false, expected: { statusKey: "setup.bridgeDisconnected", showRootPicker: false, canStart: false, canStop: true, repairOnClose: false, controls: ["top.launchGame", "setup.closeGameAction"] } },
  { name: "running-online", root: { valid: true }, gameRunning: true, online: true, expected: { statusKey: "status.gameRunning", showRootPicker: false, canStart: false, canStop: true, repairOnClose: false, controls: ["top.launchGame", "setup.closeGameAction"] } },
  { name: "preference-auto-launch-busy", root: { valid: true }, currentBusy: "autoLaunchGame", expected: { statusKey: "setup.gameStopped", showRootPicker: false, canStart: true, canStop: false, repairOnClose: false, controls: ["top.launchGame", "setup.closeGameAction"], currentSwitchDisabled: [true, false] } },
  { name: "preference-auto-reconnect-busy", root: { valid: true }, currentBusy: "autoReconnect", expected: { statusKey: "setup.gameStopped", showRootPicker: false, canStart: true, canStop: false, repairOnClose: false, controls: ["top.launchGame", "setup.closeGameAction"], currentSwitchDisabled: [false, true] } },
];

function expectedOriginalControlDisabled(spec) {
  if (!spec.expected.controls.length) return [];
  if (spec.expected.repairOnClose) return [!spec.expected.canStop];
  return [!spec.expected.canStart, !spec.expected.canStop];
}

const caseResults = [];
for (const spec of cases) {
  const recoveryState = spec.recoveryState || "idle";
  const recovering = activeRecoveryStates.has(recoveryState);
  const rootResolved = spec.root !== null;
  const rootValid = spec.root?.valid === true;
  const gameRunning = spec.gameRunning === true;
  const repairRequired = spec.repairRequired === true;
  const proxyBusy = spec.proxyBusy === true;
  const launching = spec.launching === true;
  const online = spec.online === true;
  const originalInput = {
    proxyStatus: rootResolved ? { gameRunning, repairRequired } : null,
    online,
    gameRecoveryStatus: { state: recoveryState },
    proxyBusy,
    gameLaunchBusy: launching,
    gameRootStatus: spec.root,
    gameRootBusy: spec.gameRootBusy === true,
    gameRootError: "",
    gameActionError: "",
    autoLaunchGame: false,
    autoReconnect: false,
  };
  const currentState = {
    rootResolved,
    gameRootStatus: spec.root,
    proxyStatus: originalInput.proxyStatus,
    online,
    gameRecoveryStatus: { state: recoveryState },
    proxyBusy,
    gameLaunchBusy: launching,
    busy: spec.currentBusy || "",
    gameRootError: "",
    gameActionError: "",
    autoLaunchGame: false,
    autoReconnect: false,
    production: true,
  };

  const sourcePredicates = original.predicates({ rootResolved, rootValid, gameRunning, needsRepair: repairRequired, recovering, busy: proxyBusy, launching });
  const currentParityPredicates = currentPredicates(rootResolved, rootValid, gameRunning, recovering, repairRequired, proxyBusy, launching, true);
  const currentFencedPredicates = currentPredicates(rootResolved, rootValid, gameRunning, recovering, repairRequired, proxyBusy, launching, false);
  const expectedPredicates = {
    showRootPicker: spec.expected.showRootPicker,
    canStart: spec.expected.canStart,
    canStop: spec.expected.canStop,
    repairOnClose: spec.expected.repairOnClose,
  };
  assert.deepEqual(sourcePredicates, expectedPredicates, `${spec.name}: source predicates`);
  assert.deepEqual(currentParityPredicates, expectedPredicates, `${spec.name}: current predicates with provider available`);
  assert.equal(currentFencedPredicates.canStart, false, `${spec.name}: current start must fail closed`);
  assert.equal(currentFencedPredicates.canStop, false, `${spec.name}: current stop must fail closed`);

  const sourceDisplay = display(original.Home(originalInput));
  const currentDisplay = display(current.Home({ homeState: currentState }));
  const expectedStatus = t(spec.expected.statusKey);
  const expectedControlText = spec.expected.controls.map(key => t(key));
  const expectedPicker = spec.expected.pickerKey ? [{ text: t(spec.expected.pickerKey), disabled: spec.expected.pickerDisabled }] : [];
  const expectedHint = spec.expected.repairHintKey ? t(spec.expected.repairHintKey) : "";
  const expectedStatusClass = gameRunning && online ? "status-ok" : "muted";

  assert.equal(sourceDisplay.status, expectedStatus, `${spec.name}: source header`);
  assert.equal(currentDisplay.status, expectedStatus, `${spec.name}: current header`);
  assert.equal(sourceDisplay.statusClass, expectedStatusClass, `${spec.name}: source status class`);
  assert.equal(currentDisplay.statusClass, expectedStatusClass, `${spec.name}: current status class`);
  assert.deepEqual(sourceDisplay.controls.map(item => item.text), expectedControlText, `${spec.name}: source button labels`);
  assert.deepEqual(currentDisplay.controls.map(item => item.text), expectedControlText, `${spec.name}: current button labels`);
  assert.deepEqual(sourceDisplay.controls.map(item => item.disabled), expectedOriginalControlDisabled(spec), `${spec.name}: source button disabled`);
  assert.deepEqual(currentDisplay.controls.map(item => item.disabled), expectedControlText.map(() => true), `${spec.name}: current provider fence`);
  assert.deepEqual(sourceDisplay.picker, expectedPicker, `${spec.name}: source picker`);
  assert.deepEqual(currentDisplay.picker, expectedPicker, `${spec.name}: current picker`);
  assert.equal(sourceDisplay.repairHint, expectedHint, `${spec.name}: source repair hint`);
  assert.equal(currentDisplay.repairHint, expectedHint, `${spec.name}: current repair hint`);
  assert.deepEqual(currentDisplay.switches.map(item => item.disabled), spec.expected.currentSwitchDisabled || [false, false], `${spec.name}: current preference isolation`);

  caseResults.push({
    name: spec.name,
    inputs: { root: spec.root, gameRunning, online, repairRequired, recoveryState, proxyBusy, launching, gameRootBusy: spec.gameRootBusy === true, currentBusy: spec.currentBusy || "" },
    expected: { statusKey: spec.expected.statusKey, status: expectedStatus, statusClass: expectedStatusClass, predicates: expectedPredicates, controlKeys: spec.expected.controls, pickerKey: spec.expected.pickerKey || null, repairHintKey: spec.expected.repairHintKey || null },
    original: sourceDisplay,
    current: currentDisplay,
    currentParityPredicates,
    currentFencedPredicates,
  });
}

const appNode = declaration(appAst, "App");
const appNodes = walk(appNode);
const busyWrites = appNodes
  .filter(node => node.type === "CallExpression" && node.callee?.name === "setHomeBusy")
  .map(node => node.arguments[0]?.value)
  .filter(value => typeof value === "string");
assert.deepEqual([...new Set(busyWrites)].sort(), ["", "autoLaunchGame", "autoReconnect", "gameRoot"]);
const homeStateAttr = appNodes.find(node => node.type === "JSXAttribute" && node.name?.name === "homeState");
assert.equal(homeStateAttr?.value?.expression?.type, "ObjectExpression");
const homeStateKeys = homeStateAttr.value.expression.properties.map(property => property.key?.name || property.key?.value);
assert.ok(homeStateKeys.includes("busy"));
assert.ok(!homeStateKeys.includes("proxyBusy"));
assert.ok(!homeStateKeys.includes("gameLaunchBusy"));
const previewAttr = appNodes.find(node => node.type === "JSXAttribute" && node.name?.name === "previewState");
const previewExpression = app.slice(previewAttr.value.expression.start, previewAttr.value.expression.end);
assert.match(previewExpression, /^backendBridge\.mode === "preview" \?/);

const historical = JSON.parse(fs.readFileSync(path.join(historicalDir, "busy-results.json"), "utf8"));
assert.equal(historical.reference.sha256, hash(source));
for (const locator of Object.values(historical.reference.locators)) {
  const bytes = Buffer.from(source, "utf8");
  assert.equal(bytes.subarray(locator.utf8ByteOffset, locator.utf8ByteOffset + Buffer.byteLength(locator.expression)).toString("utf8"), locator.expression);
}
const historicalBrowser = JSON.parse(fs.readFileSync(path.join(historicalDir, "browser-results.json"), "utf8"));
const historicalScreenshots = historicalBrowser.screenshots.map(entry => {
  const bytes = fs.readFileSync(path.join(historicalDir, entry.file));
  assert.equal(hash(bytes), entry.sha256);
  return { file: entry.file, sha256: entry.sha256, verified: true };
});
const currentAppHash = hash(app);
assert.notEqual(currentAppHash, historical.currentProducers.appSha256NormalizedLF, "historical App hash should remain stale after accepted root correction");

const report = {
  task: "LWB317-REVIEW-HOME-BUSY-001",
  result: "LWB317_REVIEW_HOME_BUSY001_OK",
  reference: {
    path: referencePath,
    sha256: hash(source),
    locators: sourceLocators,
  },
  current: {
    pagesPath,
    pagesSha256NormalizedLF: hash(pages),
    appPath,
    appSha256NormalizedLF: currentAppHash,
    homeExpressions: currentExpressions,
    appInputMapping: {
      homeStateKeys,
      setHomeBusyWrites: busyWrites,
      previewExpression,
      productionProxyBusy: false,
      productionGameLaunchBusy: false,
      previewOnlyBusyInputs: ["proxyBusy", "gameLaunchBusy"],
    },
  },
  cases: caseResults,
  counts: { distinguishingCases: caseResults.length, sourceLocatorChecks: Object.keys(sourceLocators).length },
  historicalEvidence: {
    savedAppSha256NormalizedLF: historical.currentProducers.appSha256NormalizedLF,
    currentAppSha256NormalizedLF: currentAppHash,
    appHashMatchesSavedReport: false,
    sourceLocatorsVerified: Object.keys(historical.reference.locators).length,
    screenshots: historicalScreenshots,
  },
  limits: "Recovered source and current local presentation/predicate review only. Current lifecycleProviderAvailable is false, so current start/stop controls remain disabled. proxyBusy/gameLaunchBusy are render inputs without App production producers. Preview fixtures do not establish lifecycle reachability or original runtime pixels.",
};

if (process.argv.includes("--record")) {
  fs.writeFileSync(path.join(here, "review-results.json"), JSON.stringify(report, null, 2) + "\n");
}
if (process.argv.includes("--verify-record")) {
  assert.deepEqual(JSON.parse(fs.readFileSync(path.join(here, "review-results.json"), "utf8")), report);
}
console.log(JSON.stringify({ result: report.result, ...report.counts, currentAppSha256NormalizedLF: currentAppHash }, null, 2));
