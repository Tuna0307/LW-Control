import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

// Milestone D is intentionally a host-only verifier. It consumes accepted page-local
// packets by identity and exercises only boundaries that later App / shared-host changes
// can invalidate. It never records screenshots or rewrites an evidence file.

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const ui = path.join(repo, "src/LWBridge.UI-0.3.17");
const verifyOnly = process.argv.includes("--verify");
const record = process.argv.includes("--record");
const replace = process.argv.includes("--replace");
assert.notEqual(record, verifyOnly, "pass exactly one of --record or --verify");
const resultPath = path.join(here, "host-reconciliation-results.json");
const manifestPath = path.join(here, "host-reconciliation-manifest.json");
if (record && fs.existsSync(resultPath) && !replace) throw new Error("host-reconciliation-results.json already exists; use --replace only for an intentional pre-review refresh");
if (record && fs.existsSync(manifestPath) && !replace) throw new Error("host-reconciliation-manifest.json already exists; use --replace only for an intentional pre-review refresh");
const portArg = process.argv.find((arg) => arg.startsWith("--port="));
const port = Number(portArg?.slice("--port=".length) || process.env.LWB317_MILESTONE_D_PORT || 4391);
assert.ok(Number.isInteger(port) && port > 0 && port <= 65535, "valid --port is required");
const baseUrl = `http://127.0.0.1:${port}/`;

const hash = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
const fileHash = (relative) => hash(fs.readFileSync(path.join(repo, relative)));
const readJson = (relative) => JSON.parse(fs.readFileSync(path.join(repo, relative), "utf8"));
const slash = (value) => value.replaceAll("\\", "/");
const describeFile = (filePath) => ({ bytes: fs.statSync(filePath).size, sha256: hash(fs.readFileSync(filePath)) });

const LOCAL_EXTENSIONS = ["", ".js", ".jsx", ".mjs", ".css", ".json", ".png", ".svg", ".webp", ".jpg", ".jpeg", ".gif"];
function resolveLocalSpecifier(importer, specifier) {
  if (!specifier.startsWith(".")) return null;
  const clean = specifier.split(/[?#]/, 1)[0];
  const base = path.resolve(path.dirname(importer), clean);
  for (const extension of LOCAL_EXTENSIONS) {
    const candidate = `${base}${extension}`;
    if (fs.existsSync(candidate) && fs.statSync(candidate).isFile()) return candidate;
  }
  for (const extension of [".js", ".jsx", ".mjs", ".css", ".json"]) {
    const candidate = path.join(base, `index${extension}`);
    if (fs.existsSync(candidate) && fs.statSync(candidate).isFile()) return candidate;
  }
  throw new Error(`unresolved local dependency ${specifier} from ${importer}`);
}

function moduleSpecifiers(source) {
  const found = new Set();
  for (const pattern of [
    /\b(?:import|export)\s+[\s\S]*?\bfrom\s*["']([^"']+)["']/g,
    /\bimport\s*["']([^"']+)["']/g,
    /\bimport\s*\(\s*["']([^"']+)["']\s*\)/g,
  ]) {
    let match;
    while ((match = pattern.exec(source))) found.add(match[1]);
  }
  return [...found];
}

function cssSpecifiers(source) {
  const found = [];
  const pattern = /url\(\s*["']?([^"')]+)["']?\s*\)/g;
  let match;
  while ((match = pattern.exec(source))) {
    const specifier = match[1].trim();
    if (!specifier || specifier.startsWith("data:") || specifier.startsWith("http:") || specifier.startsWith("https:") || specifier.startsWith("#") || specifier.startsWith("/")) continue;
    found.push(specifier);
  }
  return found;
}

function collectAppDependencyClosure() {
  const entry = path.join(ui, "src/main.jsx");
  const indexHtml = path.join(ui, "index.html");
  const visited = new Set([indexHtml]);
  const queue = [entry];
  while (queue.length) {
    const filePath = queue.shift();
    if (visited.has(filePath)) continue;
    visited.add(filePath);
    const extension = path.extname(filePath).toLowerCase();
    if (![".js", ".jsx", ".mjs", ".css"].includes(extension)) continue;
    const source = fs.readFileSync(filePath, "utf8");
    const specifiers = extension === ".css" ? cssSpecifiers(source) : moduleSpecifiers(source);
    for (const specifier of specifiers) {
      const resolved = resolveLocalSpecifier(filePath, specifier);
      if (resolved && !visited.has(resolved)) queue.push(resolved);
    }
  }
  return [...visited].map((filePath) => slash(path.relative(repo, filePath))).sort();
}

function sourceClosureSnapshot() {
  const files = collectAppDependencyClosure();
  const hashes = Object.fromEntries(files.map((relative) => [relative, fileHash(relative)]));
  return { files, hashes, digest: hash(JSON.stringify(hashes)) };
}

const inheritedProofPaths = [
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-b/results.json",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-b/current-results.json",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-e/current/inventory.json",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-f/current/inventory.json",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-g/current/inventory.json",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-h/current/inventory.json",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-REMAINING-002/milestone-4/home-source-render/render-results.json",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-REMAINING-002/milestone-4/shell-source-render/dependencies.json",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/milestone-a/shell-home-corrections-results.json",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/r1/whole-afk-composition-results.json",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/r1/join-modal-contract-results.json",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/r1/mounted-afk-interactions-results.json",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/milestone-c/conditional-composition-results.json",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/milestone-c/targeted-corrections-results.json",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/milestone-c/browser-current-results.json",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/milestone-c/config-store-ownership-results.json",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/r1/profile-ownership-results.json",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/r1/equipment-regression-results.json",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/r1/whole-afk-browser-results.json",
];

const inheritedLivePins = {
  "src/LWBridge.UI-0.3.17/src/MapDataPage.jsx": "0619C0589FED7A6DA37C7018D021D890A2A2396AC77D21A64E6D77CE348A5BD5",
  "src/LWBridge.UI-0.3.17/src/ScheduledPlunder.jsx": "9D6814A70A62C9198720CE15FAA84403F43E1DE172A88E285179151D017B5534",
  "src/LWBridge.UI-0.3.17/src/CityLayoutPage.jsx": "C2C3E9445F607C82954A4319E19C6A8A1BE64368038293D7B1FADC8BE9372061",
  "src/LWBridge.UI-0.3.17/src/HotkeyPages.jsx": "5DAE6E4FFE8875BCEBC366109AD4C1E3FFF690EA17273EBB50226E9107551634",
  "src/LWBridge.UI-0.3.17/src/SettingsPage.jsx": "8622BE39E06FE82FCF31BD2E092358987F3F304D447ADA061685727436737771",
  "src/LWBridge.UI-0.3.17/src/HomePage.jsx": "F140264E693E89F37CBB357470F58FE5DCA46F3DFDA29A8A5D2C7918CE2393FF",
  "src/LWBridge.UI-0.3.17/src/ShellPresentation.jsx": "B1DD1278F274A1FD347AD4C27787B5AE8DC1DB32230D49B55F7A471DE34A904F",
  "src/LWBridge.UI-0.3.17/src/ProfileSidebar.jsx": "96E480F3BBA897AB18BE29891A346651E933C6C743A908DE36BAC67B0E86DA9D",
  "src/LWBridge.UI-0.3.17/src/ProfileSwitchState.jsx": "01FC87F704CC45BE3476BCAF461FA057F70DCE57A7453DDD00188B756F9E83F5",
  "src/LWBridge.UI-0.3.17/src/SquadsPage.jsx": "920892FCC3069D4F98B0662E4502910DDF5901EE707C8A663B751BBB3A2E4623",
  "src/LWBridge.UI-0.3.17/src/RallyJoinSettings.jsx": "1579FA189EB6F5A34F62AF211662CC995FE314C6C13C8681C29A85B6A7A6F11A",
  "src/LWBridge.UI-0.3.17/src/reference.css": "3D87E9F65B39EACE6A1A254BFC90A38ACB72613D1FFF7236C7CEE7AB9BFAF545",
  "src/LWBridge.UI-0.3.17/src/styles.css": "B50910D37A4DD02D7BB49B506735EA42615EF0F9E85714E880049481AECE2CCC",
};

function proofHashSnapshot() {
  return Object.fromEntries(inheritedProofPaths.map((relative) => [relative, fileHash(relative)]));
}

function assertInheritedProofs() {
  for (const [relative, expected] of Object.entries(inheritedLivePins)) {
    assert.equal(fileHash(relative), expected, `inherited live pin drifted: ${relative}`);
  }

  const scheduled = readJson(inheritedProofPaths[0]);
  assert.equal(scheduled.result, "LWB317_SCHEDULED_PLUNDER_SOURCE_LOCAL_OK", "Scheduled inherited marker");
  assert.equal(scheduled.counts.renders, 10482, "Scheduled inherited render count");
  assert.equal(scheduled.counts.mutationsDetected, 51, "Scheduled inherited mutation count");

  const map = readJson(inheritedProofPaths[1]);
  assert.equal(map.marker, "LWB317_VISUAL_FINAL_UNIT_B_INTEGRATED_MAP_EXECUTED", "Map Unit B inherited marker");
  assert.equal(map.requiredCaseCount, 70, "Map Unit B required cases");
  assert.equal(map.supplementalCaseCount, 5, "Map Unit B supplemental cases");

  const pageInventories = [
    [inheritedProofPaths[2], "city-layout", "src/LWBridge.UI-0.3.17/src/CityLayoutPage.jsx"],
    [inheritedProofPaths[3], "hotkeys", "src/LWBridge.UI-0.3.17/src/HotkeyPages.jsx"],
    [inheritedProofPaths[4], "mini-games", "src/LWBridge.UI-0.3.17/src/HotkeyPages.jsx"],
    [inheritedProofPaths[5], "settings", "src/LWBridge.UI-0.3.17/src/SettingsPage.jsx"],
  ];
  for (const [relative, page, sourcePath] of pageInventories) {
    const inventory = readJson(relative);
    assert.equal(inventory.page, page, `${page} inherited inventory`);
    assert.equal(inventory.currentSource.path, sourcePath, `${page} current source path`);
    assert.equal(inventory.currentSource.sha256.toUpperCase(), fileHash(sourcePath), `${page} current source pin`);
  }

  const home = readJson(inheritedProofPaths[6]);
  assert.equal(home.marker, "LWB317_REMAINING_M4_HOME_SOURCE_RENDER_BUILT", "Home inherited marker");
  const homePin = home.current.find((entry) => entry.path === "src/LWBridge.UI-0.3.17/src/HomePage.jsx");
  assert.ok(homePin, "Home inherited current pin");
  assert.equal(homePin.sha256.toUpperCase(), fileHash(homePin.path), "Home source still inherited");

  const shell = readJson(inheritedProofPaths[7]);
  assert.deepEqual(shell.currentAppReturn, {
    byteLength: 8525,
    sha256: "B28E3437E4D3374EC2E574ED09595991F60A42470D65B41C64CD7801A415DB8B",
  }, "accepted M4 App return composition anchor");

  const shellHome = readJson(inheritedProofPaths[8]);
  assert.equal(shellHome.marker, "LWB317_FINAL_CLOSEOUT_SHELL_HOME_CORRECTIONS_OK", "Milestone A shell/Home marker");
  const shellHomeDependencies = Object.fromEntries(shellHome.dependencies.map((entry) => [entry.path, entry.sha256.toUpperCase()]));
  const appSource = fs.readFileSync(path.join(repo, "src/LWBridge.UI-0.3.17/src/App.jsx"), "utf8");
  assert.ok(appSource.includes("automation: { profileId: selectedProfileId, activeCategory: automationCategory"), "current App keeps accepted Automation profile handoff");
  assert.ok(appSource.includes("profileId: selectedProfileId") && appSource.includes("march: {"), "current App keeps R1 Squads profile handoff");
  assert.equal(shellHomeDependencies["src/LWBridge.UI-0.3.17/src/mapBackend.js"], fileHash("src/LWBridge.UI-0.3.17/src/mapBackend.js"), "Milestone A mapBackend dependency is current");

  const afk = readJson(inheritedProofPaths[9]);
  assert.equal(afk.marker, "LWB317_FINAL_CLOSEOUT_WHOLE_AFK_COMPOSITION_OK", "Milestone B whole AFK marker");
  const afkDependencies = Object.fromEntries(afk.dependencies.map((entry) => [entry.path, entry.sha256.toUpperCase()]));
  for (const relative of ["src/LWBridge.UI-0.3.17/src/SquadsPage.jsx", "src/LWBridge.UI-0.3.17/src/RallyJoinSettings.jsx"]) {
    assert.equal(afkDependencies[relative], fileHash(relative), `Milestone B dependency is current: ${relative}`);
  }

  const join = readJson(inheritedProofPaths[10]);
  assert.equal(join.marker, "LWB317_FINAL_CLOSEOUT_JOIN_MODAL_CONTRACT_OK", "Milestone B Join modal marker");
  assert.equal(join.original.sha256, "E42C17AAC67AFE7A73D525902C9E757644E40838422730CEB32EC47830C4F8A2", "recovered shared dialog anchor");

  const mounted = readJson(inheritedProofPaths[11]);
  assert.equal(mounted.marker, "LWB317_FINAL_CLOSEOUT_MOUNTED_AFK_INTERACTIONS_OK", "Milestone B mounted marker");
  assert.equal(mounted.assertions.length, 25, "Milestone B mounted assertion count");

  const conditional = readJson(inheritedProofPaths[12]);
  assert.equal(conditional.marker, "LWB317_FINAL_CLOSEOUT_MILESTONE_C_CONDITIONAL_COMPOSITION_OK", "Milestone C conditional marker");
  assert.equal(conditional.caseCount, 24, "Milestone C conditional case count");
  const conditionalDependencies = Object.fromEntries(conditional.dependencies.map((entry) => [entry.path, entry.sha256.toUpperCase()]));
  for (const relative of ["src/LWBridge.UI-0.3.17/src/AutomationPage.jsx", "src/LWBridge.UI-0.3.17/src/sharedPageUI.jsx"]) {
    assert.equal(conditionalDependencies[relative], fileHash(relative), `Milestone C conditional dependency is current: ${relative}`);
  }

  const targeted = readJson(inheritedProofPaths[13]);
  assert.equal(targeted.marker, "LWB317_FINAL_CLOSEOUT_MILESTONE_C_TARGETED_CORRECTIONS_OK", "Milestone C targeted marker");
  const targetedDependencies = Object.fromEntries(targeted.dependencies.map((entry) => [entry.path, entry.sha256.toUpperCase()]));
  for (const relative of ["src/LWBridge.UI-0.3.17/src/AutomationPage.jsx", "src/LWBridge.UI-0.3.17/src/sharedPageUI.jsx"]) {
    assert.equal(targetedDependencies[relative], fileHash(relative), `Milestone C targeted dependency is current: ${relative}`);
  }

  const cBrowser = readJson(inheritedProofPaths[14]);
  assert.equal(cBrowser.marker, "LWB317_FINAL_CLOSEOUT_MILESTONE_C_BROWSER_CURRENT_OK", "Milestone C browser marker");
  assert.equal(conditional.browserBinding.resultSha256.toUpperCase(), fileHash(inheritedProofPaths[14]), "Milestone C conditional/browser binding");
  assert.equal(String(conditional.browserBinding.sourceFiles["src/LWBridge.UI-0.3.17/src/AutomationPage.jsx"]).toUpperCase(), fileHash("src/LWBridge.UI-0.3.17/src/AutomationPage.jsx"), "Milestone C browser/current Automation binding");
  assert.equal(String(conditional.browserBinding.sourceFiles["src/LWBridge.UI-0.3.17/src/sharedPageUI.jsx"]).toUpperCase(), fileHash("src/LWBridge.UI-0.3.17/src/sharedPageUI.jsx"), "Milestone C browser/current shared UI binding");

  const ownership = readJson(inheritedProofPaths[15]);
  assert.equal(ownership.marker, "LWB317_FINAL_CLOSEOUT_MILESTONE_C_CONFIG_STORE_OWNERSHIP_OK", "Milestone C config-store ownership marker");
  assert.deepEqual(ownership.current, ownership.original, "Milestone C recovered/current config-store ownership trace");
  assert.equal(conditional.configStoreOwnershipBinding.resultSha256.toUpperCase(), fileHash(inheritedProofPaths[15]), "Milestone C conditional/config-store binding");
  assert.equal(conditional.configStoreOwnershipBinding.producerSha256.toUpperCase(), fileHash("evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/milestone-c/config-store-ownership.mjs"), "Milestone C config-store producer binding");

  const r1Ownership = readJson(inheritedProofPaths[16]);
  assert.equal(r1Ownership.status, "PASS", "R1 profile ownership marker");
  assert.equal(r1Ownership.immutableFailureBaseline.counterexample, true, "R1 binds the independent lead counterexample");
  assert.deepEqual(r1Ownership.currentScopes.sort(), ["task:allianceGarrison", "task:monsterSweep", "task:staminaPotion", "task:zombieBus"].sort(), "R1 source-owned AFK scopes");
  assert.equal(r1Ownership.browser.bClean.potion, "50", "R1 profile B remains clean");
  assert.equal(r1Ownership.browser.aRetained.potion, "10000", "R1 profile A draft returns only to A");
  assert.equal(r1Ownership.browser.bStillClean.potion, "50", "R1 cached profile B stays clean");
  assert.ok(r1Ownership.deferred.equipment?.bStayedClean === 0, "R1 Equipment deferred store is profile-isolated");

  const equipment = readJson(inheritedProofPaths[17]);
  assert.equal(equipment.marker, "LWB317_FINAL_CLOSEOUT_R1_EQUIPMENT_REGRESSION_OK", "R1 Equipment regression marker");
  assert.equal(equipment.issues.length, 0, "R1 Equipment browser issues");
  assert.equal(equipment.assertions.length, 17, "R1 Equipment regression assertion count");

  const afkBrowser = readJson(inheritedProofPaths[18]);
  assert.equal(afkBrowser.marker, "LWB317_FINAL_CLOSEOUT_WHOLE_AFK_BROWSER_OK", "R1 AFK browser-pair marker");
  assert.equal(afkBrowser.cases.length, 14, "R1 AFK browser-pair case count");
  assert.equal(afkBrowser.console.length, 0, "R1 AFK browser-pair console issues");
}

const sourceClosureStart = sourceClosureSnapshot();
const proofHashesStart = proofHashSnapshot();
assertInheritedProofs();

const playwrightPackage = process.env.LWB317_PLAYWRIGHT_PACKAGE || "C:/Users/chimw/AppData/Local/Temp/lwbridge-playwright/package.json";
const chromePath = process.env.LWB317_CHROME_PATH || "C:/Program Files/Google/Chrome/Application/chrome.exe";
const verifierPath = fileURLToPath(import.meta.url);
const toolSnapshot = {
  node: process.version,
  playwrightPackage: { path: slash(playwrightPackage), ...describeFile(playwrightPackage), version: JSON.parse(fs.readFileSync(playwrightPackage, "utf8")).version },
  chromeExecutable: { path: slash(chromePath), ...describeFile(chromePath) },
};
let frozenManifest = null;
if (verifyOnly) {
  assert.ok(fs.existsSync(resultPath) && fs.existsSync(manifestPath), "Milestone D recorded result/manifest are required for --verify");
  frozenManifest = JSON.parse(fs.readFileSync(manifestPath, "utf8"));
  assert.equal(frozenManifest.marker, "LWB317_FINAL_CLOSEOUT_MILESTONE_D_MANIFEST", "Milestone D manifest marker");
  assert.deepEqual(frozenManifest.sourceClosure, sourceClosureStart, "served App source closure drifted before Milestone D verification");
  assert.deepEqual(frozenManifest.inheritedProofFiles, proofHashesStart, "inherited proof dependency drifted before Milestone D verification");
  assert.deepEqual(frozenManifest.inheritedLivePins, inheritedLivePins, "Milestone D inherited live-pin set drifted");
  assert.deepEqual(frozenManifest.tools, toolSnapshot, "Milestone D tool identity drifted");
  assert.equal(frozenManifest.verifier.sha256, hash(fs.readFileSync(verifierPath)), "Milestone D verifier drifted");
}
const requirePlaywright = createRequire(playwrightPackage);
const { chromium } = requirePlaywright("playwright");

const assertions = [];
const consoleIssues = [];
const check = (name, actual, expected) => {
  assert.deepEqual(actual, expected, name);
  assertions.push({ name, actual, expected });
};

const routes = [
  ["overview", ".quick-actions-panel"],
  ["automation", ".automation-categories"],
  ["map-data", ".map-panel"],
  ["march", ".squad-panel"],
  ["city-layout", ".city-layout-empty, .city-layout-panel"],
  ["hotkeys", ".hotkey-panel[data-hotkey-category='hotkeys']"],
  ["mini-games", ".hotkey-panel[data-hotkey-category='miniGames']"],
  ["settings", ".settings-panel"],
];
const routedSurfaceSelector = routes.map(([, selector]) => selector).join(", ");

async function openPage(browser, { language = "en", theme = "light", width = 1280, height = 900, state = "", route = "overview" } = {}) {
  const context = await browser.newContext({
    viewport: { width, height },
    locale: language,
    timezoneId: "Asia/Singapore",
    colorScheme: theme,
    deviceScaleFactor: 1,
  });
  await context.addInitScript((lang) => {
    localStorage.clear();
    localStorage.setItem("lwbridge.language", lang);

    const targetMaps = { window: new Map(), document: new Map() };
    const installTargetTracker = (targetName, target) => {
      const active = targetMaps[targetName];
      const originalAdd = target.addEventListener.bind(target);
      const originalRemove = target.removeEventListener.bind(target);
      target.addEventListener = (type, listener, options) => {
        if (typeof listener === "function" || (listener && typeof listener.handleEvent === "function")) {
          const listeners = active.get(type) || new Set();
          listeners.add(listener);
          active.set(type, listeners);
        }
        return originalAdd(type, listener, options);
      };
      target.removeEventListener = (type, listener, options) => {
        active.get(type)?.delete(listener);
        return originalRemove(type, listener, options);
      };
    };
    installTargetTracker("window", window);
    installTargetTracker("document", document);

    const activeIntervals = new Map();
    const originalSetInterval = window.setInterval.bind(window);
    const originalClearInterval = window.clearInterval.bind(window);
    window.setInterval = (...args) => {
      const id = originalSetInterval(...args);
      activeIntervals.set(id, { delay: Number(args[1]), callback: String(args[0]), stack: new Error("interval-owner").stack || "" });
      return id;
    };
    window.clearInterval = (id) => {
      activeIntervals.delete(id);
      return originalClearInterval(id);
    };
    window.__dOwnerCount = (target, type) => targetMaps[target]?.get(type)?.size || 0;
    window.__dIntervalCount = () => activeIntervals.size;
    window.__dIntervalOwners = () => [...activeIntervals.values()];
  }, language);

  const page = await context.newPage();
  page.on("console", (message) => {
    if (["warning", "error"].includes(message.type())) consoleIssues.push({ language, theme, state, type: message.type(), text: message.text() });
  });
  page.on("pageerror", (error) => consoleIssues.push({ language, theme, state, type: "pageerror", text: error.message }));

  const url = new URL(baseUrl);
  url.searchParams.set("previewPage", route);
  url.searchParams.set("previewTheme", theme);
  if (state) url.searchParams.set("previewState", state);
  await page.goto(url.href);
  await page.locator("main.app-shell").waitFor();
  await page.waitForFunction((lang) => document.documentElement.lang === lang, language);
  await page.waitForFunction((wanted) => document.documentElement.dataset.theme === wanted, theme);
  await page.evaluate(() => document.fonts.ready);
  return { context, page };
}

async function selectRoute(page, index, selector = routes[index][1]) {
  await page.locator(".side-nav > button").nth(index).click();
  await page.locator(selector).filter({ visible: true }).first().waitFor();
}

async function selectedIndex(page, selector) {
  return page.locator(`${selector} > button[aria-selected='true']`).evaluate((node) => [...node.parentElement.children].indexOf(node));
}

async function twoFrames(page) {
  await page.evaluate(() => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve))));
}

const browser = await chromium.launch({
  executablePath: chromePath,
  headless: true,
});
const browserVersion = browser.version();

try {
  // D1: all eight source-order routes retain DOM identity through Activity hide/return in the four required modes.
  for (const mode of [
    { id: "en-light-desktop", language: "en", theme: "light", width: 1280, height: 900 },
    { id: "ja-dark-desktop", language: "ja", theme: "dark", width: 1280, height: 900 },
    { id: "en-dark-narrow", language: "en", theme: "dark", width: 375, height: 1000 },
    { id: "ja-light-narrow", language: "ja", theme: "light", width: 375, height: 1000 },
  ]) {
    const { context, page } = await openPage(browser, mode);
    try {
      check(`${mode.id}: exactly eight navigation routes`, await page.locator(".side-nav > button").count(), 8);
      check(`${mode.id}: shared top bar exists once`, await page.locator("header.top-bar").count(), 1);
      const ids = new Map();
      for (let index = 0; index < routes.length; index++) {
        const [route, selector] = routes[index];
        await selectRoute(page, index, selector);
        const panel = page.locator(selector).filter({ visible: true }).first();
        const token = `d-${mode.id}-${route}`;
        await panel.evaluate((node, value) => node.setAttribute("data-d-host-id", value), token);
        ids.set(route, token);
        check(`${mode.id}/${route}: selected nav owns aria-current`, await page.locator(".side-nav > button").nth(index).getAttribute("aria-current"), "page");
        check(`${mode.id}/${route}: exactly one routed surface is visible`, await page.locator(routedSurfaceSelector).filter({ visible: true }).count(), 1);
        check(`${mode.id}/${route}: language remains settled`, await page.locator("html").getAttribute("lang"), mode.language);
        check(`${mode.id}/${route}: theme remains settled`, await page.locator("html").getAttribute("data-theme"), mode.theme);
        if (route === "hotkeys" || route === "settings") {
          check(`${mode.id}/${route}: shared PanelTitle default remains muted`, await panel.locator(".panel-title > span").first().getAttribute("class"), "muted");
        }
      }
      for (let index = routes.length - 1; index >= 0; index--) {
        const [route, selector] = routes[index];
        await selectRoute(page, index, selector);
        check(`${mode.id}/${route}: Activity return preserves DOM identity`, await page.locator(`[data-d-host-id='${ids.get(route)}']`).count(), 1);
      }
      if (mode.width <= 760) {
        check(`${mode.id}: no document horizontal overflow`, await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth), false);
      }
    } finally {
      await context.close();
    }
  }

  // D2: Automation category ownership plus a retained local draft, without invoking any run/action provider.
  {
    const { context, page } = await openPage(browser, { state: "automation-training-ready", route: "automation" });
    try {
      const draft = page.locator(".automation-config-body:visible input[type='number']:not([disabled])").first();
      await draft.waitFor();
      await draft.fill("17");
      await draft.evaluate((node) => node.setAttribute("data-d-automation-draft", "true"));
      check("Automation local draft edit applies", await draft.inputValue(), "17");
      await page.locator(".automation-categories > button").nth(1).click();
      check("Automation parent category changes", await selectedIndex(page, ".automation-categories"), 1);
      check("Automation hidden category keeps edited draft mounted", await page.locator("[data-d-automation-draft='true']").count(), 1);
      await selectRoute(page, 0);
      check("Automation retained page remains mounted while top route hidden", await page.locator(".automation-categories").count(), 1);
      await selectRoute(page, 1);
      check("Automation parent category survives top-route return", await selectedIndex(page, ".automation-categories"), 1);
      await page.locator(".automation-categories > button").nth(0).click();
      check("Automation retained draft survives category and top-route return", await page.locator("[data-d-automation-draft='true']").inputValue(), "17");
    } finally {
      await context.close();
    }
  }

  // D3: AFK <-> Equipment parent tab, AFK draft retention, top-route retention and browser-observable Alt listener ownership.
  {
    const { context, page } = await openPage(browser, { state: "squads-profile-normal", route: "march" });
    try {
      const afkDraft = page.locator(".monster-afk-editor input[type='number']:not([disabled])").first();
      await afkDraft.waitFor();
      await afkDraft.fill("13");
      await afkDraft.evaluate((node) => node.setAttribute("data-d-afk-draft", "true"));
      const afkKeydownOwners = await page.evaluate(() => window.__dOwnerCount("window", "keydown"));
      await page.locator(".squad-tabs > button").nth(1).click();
      await page.locator(".equipment-preset-layout").waitFor();
      check("Squads parent tab selects Equipment", await selectedIndex(page, ".squad-tabs"), 1);
      check("Equipment visible Activity installs one Alt key owner", await page.evaluate(() => window.__dOwnerCount("window", "keydown")), afkKeydownOwners + 1);
      await selectRoute(page, 0);
      check("hidden Squads Activity removes Equipment Alt key owner", await page.evaluate(() => window.__dOwnerCount("window", "keydown")), afkKeydownOwners);
      await selectRoute(page, 3);
      check("Squads Equipment parent tab survives top-route return", await selectedIndex(page, ".squad-tabs"), 1);
      check("Squads return restores exactly one Equipment Alt key owner", await page.evaluate(() => window.__dOwnerCount("window", "keydown")), afkKeydownOwners + 1);
      await page.locator(".squad-tabs > button").nth(0).click();
      check("AFK draft survives Equipment and top-route round trip", await page.locator("[data-d-afk-draft='true']").inputValue(), "13");
      check("AFK visible removes hidden Equipment Alt key owner", await page.evaluate(() => window.__dOwnerCount("window", "keydown")), afkKeydownOwners);
    } finally {
      await context.close();
    }
  }

  // D4: representative Map data-tab/filter + Manual/Auto state survives ordinary route hiding.
  {
    const { context, page } = await openPage(browser, { language: "ja", theme: "dark", state: "map-filter-lifecycle", route: "map-data" });
    try {
      await page.locator(".map-tabs > button").nth(3).click();
      const search = page.locator(".map-searchbar input").first();
      await search.fill("Fixture");
      await page.locator(".map-searchbar select").filter({ visible: true }).first().selectOption("ssr");
      await page.locator(".map-plunderable-filter input[type='checkbox']").check();
      await page.locator(".map-scan-tabs > button").nth(1).click();
      await page.locator(".map-auto-scan-grid input[type='number']").fill("75");
      check("Map representative data tab selected", await selectedIndex(page, ".map-tabs"), 3);
      check("Map representative Auto mode selected", await selectedIndex(page, ".map-scan-tabs"), 1);
      await selectRoute(page, 0);
      await selectRoute(page, 2);
      check("Map data tab survives route return", await selectedIndex(page, ".map-tabs"), 3);
      check("Map keyword survives route return", await search.inputValue(), "Fixture");
      check("Map quality filter survives route return", await page.locator(".map-searchbar select").filter({ visible: true }).first().inputValue(), "ssr");
      check("Map plunderable filter survives route return", await page.locator(".map-plunderable-filter input[type='checkbox']").isChecked(), true);
      check("Map Manual/Auto state survives route return", await selectedIndex(page, ".map-scan-tabs"), 1);
      check("Map Auto interval survives route return", await page.locator(".map-auto-scan-grid input[type='number']").inputValue(), "75");
      check("Map preview does not expose an executable Auto action", await page.locator(".map-auto-scan-options button").isDisabled(), true);
    } finally {
      await context.close();
    }
  }

  // D5: representative Map 52-row pagination remains page-local and retained.
  {
    const { context, page } = await openPage(browser, { state: "map-city", route: "map-data" });
    try {
      await page.locator(".map-pagination").waitFor();
      check("Map City fixture exposes 52 rows", await page.locator(".map-result-count").innerText(), "52 items");
      check("Map City page one renders 50 rows", await page.locator(".map-table tbody tr").count(), 50);
      await page.locator(".map-pagination button").last().click();
      await page.waitForFunction(() => document.querySelectorAll(".map-table tbody tr").length === 2);
      check("Map advances to page two", await page.locator(".map-pagination span").innerText(), "Page 2 of 2");
      await selectRoute(page, 0);
      await selectRoute(page, 2);
      check("Map page two survives route return", await page.locator(".map-pagination span").innerText(), "Page 2 of 2");
      check("Map page-two count survives route return", await page.locator(".map-result-count").innerText(), "52 items");
    } finally {
      await context.close();
    }
  }

  // D6: Scheduled Plunder enters through the real Map tab and existing preview provider only.
  {
    const { context, page } = await openPage(browser, { state: "map-scheduled-populated", route: "map-data" });
    try {
      await page.locator(".map-tabs > button").last().click();
      await page.locator(".map-scheduled-group").first().waitFor();
      check("Scheduled Plunder is the selected Map tab", await selectedIndex(page, ".map-tabs"), 8);
      check("Scheduled Plunder renders Dispatch/Ghost/Truck groups", await page.locator(".map-scheduled-group").count(), 3);
      check("Scheduled Plunder uses three recovered table groups", await page.locator(".map-table--scheduled-plunder").count(), 3);
      check("Scheduled Plunder populated fixture has rows", (await page.locator(".map-table--scheduled-plunder tbody tr").count()) > 3, true);
    } finally {
      await context.close();
    }
  }

  // D7: profile boundary distinguishes keyed page-local reset from App-owned parent tab/category ownership.
  {
    const { context, page } = await openPage(browser, { state: "shell-profiles", route: "overview" });
    try {
      await page.locator(".profile-collapse").click();
      const profiles = page.locator(".profile-item");
      const home = page.locator(".quick-actions-panel");
      await home.evaluate((node) => node.setAttribute("data-d-home-profile-owner", "first"));
      await page.evaluate(() => {
        window.__dSawUncachedLoading = false;
        window.__dProfileObserver = new MutationObserver(() => {
          if (document.querySelector(".profile-switch-loading")) window.__dSawUncachedLoading = true;
        });
        window.__dProfileObserver.observe(document.body, { childList: true, subtree: true });
      });
      await profiles.nth(1).click();
      await page.waitForFunction(() => window.__dSawUncachedLoading === true);
      await page.waitForFunction(() => !document.querySelector(".profile-switch-loading"));
      await page.evaluate(() => window.__dProfileObserver?.disconnect?.());
      check("Home profile switch crosses uncached loading boundary", await page.evaluate(() => window.__dSawUncachedLoading), true);
      check("Home stays the App-owned top route across profile switch", await page.locator(".side-nav > button").nth(0).getAttribute("aria-current"), "page");
      check("Home keyed page subtree remounts for replacement profile", await page.locator("[data-d-home-profile-owner='first']").count(), 0);
      check("Replacement profile owns exactly one Home page", await page.locator(".quick-actions-panel").count(), 1);

      await selectRoute(page, 1);
      await page.locator(".automation-categories > button").nth(1).click();
      check("Automation App-owned category precondition", await selectedIndex(page, ".automation-categories"), 1);

      await selectRoute(page, 2);
      await page.locator(".map-tabs > button").nth(3).click();
      await page.locator(".map-searchbar input").first().fill("profile-local-map-keyword");
      await page.locator(".map-searchbar select").filter({ visible: true }).first().selectOption("ssr");
      await page.locator(".map-plunderable-filter input[type='checkbox']").check();
      await page.locator(".map-scan-tabs > button").nth(1).click();
      await page.locator(".map-auto-scan-grid input[type='number']").fill("75");

      await selectRoute(page, 3);
      await page.locator(".squad-tabs > button").nth(1).click();
      check("Squads App-owned subtab precondition", await selectedIndex(page, ".squad-tabs"), 1);
      await selectRoute(page, 2);

      await page.evaluate(() => {
        window.__dSawCachedLoading = false;
        window.__dCachedObserver = new MutationObserver(() => {
          if (document.querySelector(".profile-switch-loading")) window.__dSawCachedLoading = true;
        });
        window.__dCachedObserver.observe(document.body, { childList: true, subtree: true });
      });
      await profiles.nth(0).click();
      await twoFrames(page);
      await page.evaluate(() => window.__dCachedObserver?.disconnect?.());
      check("cached profile return bypasses loading", await page.evaluate(() => window.__dSawCachedLoading), false);
      check("profile switch preserves current App-owned Map route", await page.locator(".side-nav > button").nth(2).getAttribute("aria-current"), "page");
      check("profile switch preserves App-owned Map data tab", await selectedIndex(page, ".map-tabs"), 3);
      check("profile switch resets Map keyword", await page.locator(".map-searchbar input").first().inputValue(), "");
      check("profile switch resets Map quality filter", await page.locator(".map-searchbar select").filter({ visible: true }).first().inputValue(), "");
      check("profile switch resets Map plunderable filter", await page.locator(".map-plunderable-filter input[type='checkbox']").isChecked(), false);
      check("profile switch resets Map Manual/Auto child state", await selectedIndex(page, ".map-scan-tabs"), 0);
      await page.locator(".map-scan-tabs > button").nth(1).click();
      check("replacement profile reads its own Auto interval", await page.locator(".map-auto-scan-grid input[type='number']").inputValue(), "60");
      await selectRoute(page, 1);
      check("profile switch preserves App-owned Automation category", await selectedIndex(page, ".automation-categories"), 1);
      await selectRoute(page, 3);
      check("profile switch preserves App-owned Squads subtab", await selectedIndex(page, ".squad-tabs"), 1);
    } finally {
      await context.close();
    }
  }

  // D8: Home ordinary top-route hiding retains the same page instance.
  {
    const { context, page } = await openPage(browser, { state: "home-connected", route: "overview" });
    try {
      const home = page.locator(".quick-actions-panel");
      await home.evaluate((node) => node.setAttribute("data-d-home-retained", "true"));
      await selectRoute(page, 1);
      await selectRoute(page, 0);
      check("Home retains DOM identity across ordinary route hiding", await page.locator("[data-d-home-retained='true']").count(), 1);
      check("Home remains below profile-view-context", await page.locator(".profile-view-context .quick-actions-panel").count(), 1);
    } finally {
      await context.close();
    }
  }

  // D9: representative hidden Activity effect owner cleanup for City Layout.
  {
    const { context, page } = await openPage(browser, { state: "city-layout-populated", route: "city-layout" });
    try {
      const zoom = page.locator(".city-layout-zoom");
      await zoom.waitFor();
      const visibleKeydownOwners = await page.evaluate(() => window.__dOwnerCount("window", "keydown"));
      await zoom.locator("button").nth(1).click();
      const editedZoom = await zoom.innerText();
      await selectRoute(page, 0);
      check("City hidden Activity removes exactly one keydown owner", await page.evaluate(() => window.__dOwnerCount("window", "keydown")), visibleKeydownOwners - 1);
      await selectRoute(page, 4, ".city-layout-panel");
      check("City return restores exactly one keydown owner", await page.evaluate(() => window.__dOwnerCount("window", "keydown")), visibleKeydownOwners);
      check("City local zoom survives ordinary route hiding", await page.locator(".city-layout-zoom").innerText(), editedZoom);
    } finally {
      await context.close();
    }
  }

  // D10: shared header/sidebar/nav, Cross-server popover, profile dialog focus, and live locale/theme retention.
  {
    const { context, page } = await openPage(browser, { state: "shell-profiles", route: "overview" });
    try {
      check("shared header exists once", await page.locator("header.top-bar").count(), 1);
      check("shared profile sidebar exists once", await page.locator("aside.profile-sidebar").count(), 1);
      check("shared navigation exposes eight routes", await page.locator(".side-nav > button").count(), 8);

      const jumpTrigger = page.locator(".server-jump > button");
      await jumpTrigger.click();
      check("Cross-server popover mounts once", await page.locator(".server-jump-popover").count(), 1);
      check("Cross-server unavailable preview is source-blocked", await page.locator(".server-jump-error").count(), 1);
      check("Cross-server trigger retains focus on open", await jumpTrigger.evaluate((node) => document.activeElement === node), true);
      const serverInput = page.locator(".server-jump-popover input");
      await serverInput.focus();
      await page.keyboard.press("Escape");
      check("Cross-server Escape remains inert", await page.locator(".server-jump-popover").count(), 1);
      await page.locator(".brand-lockup").click({ position: { x: 4, y: 4 } });
      await page.locator(".server-jump-popover").waitFor({ state: "detached" });

      await page.locator(".profile-collapse").click();
      const row = page.locator(".profile-row").first();
      await row.hover();
      const noteTrigger = row.locator(".profile-note-edit");
      await noteTrigger.focus();
      await noteTrigger.click();
      const noteDialog = page.locator("dialog.profile-dialog-backdrop");
      await noteDialog.waitFor();
      check("profile note dialog stays inside shared shell and outside routed page", await page.locator("main.app-shell dialog.profile-dialog-backdrop").count(), 1);
      check("profile note dialog is owned by shared profile sidebar", await page.locator("aside.profile-sidebar dialog.profile-dialog-backdrop").count(), 1);
      check("profile note dialog is absent from keyed routed profile context", await page.locator(".profile-view-context dialog.profile-dialog-backdrop").count(), 0);
      check("profile note dialog owns focus", await noteDialog.evaluate((node) => node.contains(document.activeElement)), true);
      await page.keyboard.press("Tab");
      check("profile note Tab keeps focus inside dialog", await noteDialog.evaluate((node) => node.contains(document.activeElement)), true);
      await page.keyboard.press("Escape");
      await noteDialog.waitFor({ state: "detached" });
      check("profile note cleanup restores trigger focus", await noteTrigger.evaluate((node) => document.activeElement === node), true);

      await selectRoute(page, 2);
      await page.locator(".map-panel").evaluate((node) => node.setAttribute("data-d-live-theme-locale", "true"));
      await page.locator(".language-select select").selectOption("ja");
      await page.waitForFunction(() => document.documentElement.lang === "ja");
      await page.locator(".theme-toggle").click();
      await page.waitForFunction(() => document.documentElement.dataset.theme === "dark");
      await page.waitForFunction(() => !document.documentElement.classList.contains("theme-transition"));
      check("live locale change keeps retained Map instance", await page.locator("[data-d-live-theme-locale='true']").count(), 1);
      check("live locale becomes Japanese", await page.locator("html").getAttribute("lang"), "ja");
      check("live theme becomes dark", await page.locator("html").getAttribute("data-theme"), "dark");
    } finally {
      await context.close();
    }
  }

  // D10a: hidden Activity cleanup for the two page-local 1s timer owners.
  for (const [state, routeIndex, label] of [
    ["mini-games-active", 6, "Mini Games running timer"],
    ["settings-update-cooldown", 7, "Settings update-cooldown timer"],
  ]) {
    const { context, page } = await openPage(browser, { state, route: routes[routeIndex][0] });
    try {
      await page.locator(routes[routeIndex][1]).filter({ visible: true }).first().waitFor();
      await page.waitForFunction(() => window.__dIntervalOwners().filter((owner) => owner.delay === 1000).length === 1);
      check(`${label} has exactly one visible 1s page interval`, await page.evaluate(() => window.__dIntervalOwners().filter((owner) => owner.delay === 1000).length), 1);
      await selectRoute(page, 1);
      await page.waitForFunction(() => window.__dIntervalOwners().every((owner) => owner.delay !== 1000));
      check(`${label} removes its 1s page interval while hidden`, await page.evaluate(() => window.__dIntervalOwners().filter((owner) => owner.delay === 1000).length), 0);
      await selectRoute(page, routeIndex);
      await page.waitForFunction(() => window.__dIntervalOwners().filter((owner) => owner.delay === 1000).length === 1);
      check(`${label} restores exactly one 1s page interval on return`, await page.evaluate(() => window.__dIntervalOwners().filter((owner) => owner.delay === 1000).length), 1);
    } finally {
      await context.close();
    }
  }

  // D11: exit dialogs are shared siblings of the shell. No confirmation/provider action is invoked.
  for (const [state, busy] of [["shell-exit", false], ["shell-exit-busy", true]]) {
    const { context, page } = await openPage(browser, {
      language: busy ? "ja" : "en",
      theme: busy ? "dark" : "light",
      width: busy ? 740 : 1280,
      height: busy ? 650 : 900,
      state,
    });
    try {
      const dialog = page.locator("dialog.app-exit-backdrop");
      await dialog.waitFor();
      check(`${state}: exit dialog is sibling after shell`, await page.locator("main.app-shell ~ dialog.app-exit-backdrop").count(), 1);
      check(`${state}: exit dialog owns focus`, await dialog.evaluate((node) => node.contains(document.activeElement)), true);
      await page.keyboard.press("Escape");
      if (busy) check("busy exit remains mounted after Escape", await dialog.count(), 1);
      else await dialog.waitFor({ state: "detached" });
    } finally {
      await context.close();
    }
  }
} finally {
  await browser.close();
}

assert.deepEqual(consoleIssues, [], JSON.stringify(consoleIssues, null, 2));

const sourceClosureEnd = sourceClosureSnapshot();
const proofHashesEnd = proofHashSnapshot();
assert.deepEqual(sourceClosureEnd, sourceClosureStart, "served App source closure changed during Milestone D verification");
assert.deepEqual(proofHashesEnd, proofHashesStart, "inherited proof dependencies changed during Milestone D verification");
assertInheritedProofs();

const result = {
  marker: "LWB317_FINAL_CLOSEOUT_MILESTONE_D_HOST_RECONCILIATION_OK",
  assertions,
  consoleIssues,
  browserVersion,
  tools: toolSnapshot,
  sourceClosure: {
    count: sourceClosureStart.files.length,
    digest: sourceClosureStart.digest,
    files: sourceClosureStart.hashes,
  },
  dependencies: {
    inheritedProofFiles: proofHashesStart,
    inheritedLivePins,
    verifier: {
      path: slash(path.relative(repo, fileURLToPath(import.meta.url))),
      sha256: hash(fs.readFileSync(fileURLToPath(import.meta.url))),
    },
  },
  inheritedCoverage: {
    map: "Unit B: 10,482 Scheduled renders, 51/51 mutations, 75 integrated page cases; D rechecks host retention/profile/representative Map states only.",
    cityHotkeysMiniSettings: "Units E-H: 30 / 18 / 78 / 63 paired source-render states; D rechecks route host, shared PanelTitle default, narrow modes, and representative hidden-effect ownership only.",
    home: "M4 Home source renderer retained by HomePage pin; D rechecks route retention and profile-key remount at the current host boundary.",
    squads: "Milestone B: 30 whole-I cases, 14 browser pairs, Join dialog contract, 25 mounted assertions; D rechecks AFK/Equipment and top-route/profile ownership only.",
    automation: "Milestone C: 24-case recovered-Ae/live conditional composition plus targeted corrections and its bound current-browser packet; D rechecks Automation host category/draft/profile ownership only.",
    shellHome: "Milestone A shell/Home ownership packet remains bound to the live App/mapBackend sources; D rechecks mounted host/profile transitions without replaying the callback harness.",
  },
  limits: [
    "Host/integration reconciliation only. Exact original page-local visual authority remains in the referenced inherited packets.",
    "Uses existing preview states/providers only. No native/gameplay/updater/server-jump/scan/plunder/OS-hotkey action is clicked or invoked.",
    "The verifier assumes an already-running current UI server at --port (default 4391); it does not start or own a server process.",
    "--record writes only this Milestone D result/manifest; --verify is rewrite-free and rejects source/proof/tool/verifier/result drift.",
  ],
};

const resultText = `${JSON.stringify(result, null, 2)}\n`;
if (record) {
  fs.writeFileSync(resultPath, resultText);
  const manifest = {
    marker: "LWB317_FINAL_CLOSEOUT_MILESTONE_D_MANIFEST",
    sourceClosure: sourceClosureStart,
    inheritedProofFiles: proofHashesStart,
    inheritedLivePins,
    tools: toolSnapshot,
    verifier: { path: slash(path.relative(repo, verifierPath)), sha256: hash(fs.readFileSync(verifierPath)) },
    result: { path: slash(path.relative(repo, resultPath)), ...describeFile(resultPath) },
  };
  fs.writeFileSync(manifestPath, `${JSON.stringify(manifest, null, 2)}\n`);
} else {
  const recorded = JSON.parse(fs.readFileSync(resultPath, "utf8"));
  assert.deepEqual(result, recorded, "recorded Milestone D host result is stale");
  assert.equal(frozenManifest.result.sha256, hash(fs.readFileSync(resultPath)), "Milestone D recorded result hash drifted");
  assert.equal(frozenManifest.result.bytes, fs.statSync(resultPath).size, "Milestone D recorded result size drifted");
}
console.log(JSON.stringify(result, null, 2));
