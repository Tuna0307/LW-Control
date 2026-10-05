import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";
import { createConfigDraft } from "file:///C:/Users/chimw/OneDrive/Desktop/Github/LW-Control/src/LWBridge.UI-0.3.17/src/previewConfig.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = "C:\\Users\\chimw\\OneDrive\\Desktop\\Github\\LW-Control";
const uiRoot = path.join(repo, "src/LWBridge.UI-0.3.17");
const assetsRoot = path.join(repo, "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets");
const leadRoot = path.join(repo, "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/lead-review");
const args = new Map(process.argv.slice(2).map((arg) => {
  const [key, ...rest] = arg.replace(/^--/, "").split("=");
  return [key, rest.join("=") || true];
}));
const baseUrl = String(args.get("base-url") || "http://127.0.0.1:4451");
const verifyOnly = args.has("verify");

const sha256 = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
const read = (value) => fs.readFileSync(value);
const readText = (value) => read(value).toString("utf8");
const hashFile = (value) => sha256(read(value));

const expected = {
  exe: "4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783",
  squadAsset: "ED466E353AA896D8C3FC783FF1802930156FB9E9EB54327AEF73F2FF86FA06C7",
  mainAsset: "44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6",
  originalLocators: { T: 191297, re: 193714, me: 195132, I: 28070 },
};

const referenceExe = path.resolve(repo, "../LW/lwbridge-0.3.17.exe");
const squadAsset = path.join(assetsRoot, "SquadPanel-HC3-DJei.js");
const mainAsset = path.join(assetsRoot, "index-BVfnK1wp.js");
assert.equal(hashFile(referenceExe), expected.exe, "reference executable changed");
assert.equal(hashFile(squadAsset), expected.squadAsset, "SquadPanel asset changed");
assert.equal(hashFile(mainAsset), expected.mainAsset, "main asset changed");

const requireUi = createRequire(path.join(uiRoot, "package.json"));
const parser = requireUi("@babel/parser");
const main = readText(mainAsset);
const squad = readText(squadAsset);
const mainAst = parser.parse(main, { sourceType: "module" });
const squadAst = parser.parse(squad, { sourceType: "module" });
const mainFunctions = mainAst.program.body.filter((node) => node.type === "FunctionDeclaration");
const mainFn = (name) => {
  const node = mainFunctions.find((candidate) => candidate.id?.name === name);
  assert.ok(node, `missing original ${name}`);
  return node;
};
const byteOffset = (text, charOffset) => Buffer.byteLength(text.slice(0, charOffset));
const execute = (text, node, env, prefix = "") => new Function(...Object.keys(env), `${prefix}return (${text.slice(node.start, node.end)});`)(...Object.values(env));

const originalT = mainFn("T");
const originalRe = mainFn("re");
const originalMe = mainFn("me");
const locators = {
  T: byteOffset(main, originalT.start),
  re: byteOffset(main, originalRe.start),
  me: byteOffset(main, originalMe.start),
  I: byteOffset(squad, squad.indexOf("function I(")),
};
assert.deepEqual(locators, expected.originalLocators, "original function locators drifted");

const OriginalStore = execute(main, originalT, { w: JSON.stringify });
const OriginalRegistry = execute(main, originalRe, { ee: new Map(), T: OriginalStore, D: new Set() }, "let E=0;");
const OriginalUseConfig = execute(main, originalMe, {
  T: OriginalStore,
  re: OriginalRegistry,
  A: () => "A",
  window: {},
  b: { useSyncExternalStore: (_subscribe, getSnapshot) => getSnapshot(), useEffect() {} },
});

const originalScopeOwners = {
  "task:monsterSweep": { ownerToken: "g", index: squad.indexOf("task:monsterSweep") },
  "task:staminaPotion": { ownerToken: "g", index: squad.indexOf("task:staminaPotion") },
  "task:allianceGarrison": { ownerToken: "p", index: squad.indexOf("task:allianceGarrison") },
  "task:zombieBus": { ownerToken: "l", index: squad.indexOf("task:zombieBus") },
};
for (const [scope, info] of Object.entries(originalScopeOwners)) {
  assert.ok(info.index >= 0, `missing original ${scope}`);
  const tail = squad.slice(info.index, info.index + 500);
  assert.match(tail, new RegExp(`[,}]${info.ownerToken}\\)`), `${scope} does not use recovered selected-profile owner ${info.ownerToken}`);
}

const originalOwnership = {};
for (const scope of Object.keys(originalScopeOwners)) {
  const initial = { marker: 0 };
  const adapter = { valid: () => true, read: async () => initial, write: async (value) => value };
  const a = OriginalUseConfig(scope, initial, adapter, "A");
  a.state.edit((value) => ({ ...value, marker: 1 }), false);
  const b = OriginalUseConfig(scope, initial, adapter, "B");
  const returnedA = OriginalUseConfig(scope, initial, adapter, "A");
  const returnedB = OriginalUseConfig(scope, initial, adapter, "B");
  assert.notEqual(a.state, b.state, `${scope} original A/B stores must differ`);
  assert.equal(returnedA.draft.marker, 1, `${scope} original A draft must be retained`);
  assert.equal(returnedB.draft.marker, 0, `${scope} original B draft must remain clean`);
  originalOwnership[scope] = {
    distinctStores: a.state !== b.state,
    aEdited: returnedA.draft.marker,
    bClean: returnedB.draft.marker,
  };
}

const baselineScript = path.join(leadRoot, "afk-profile-browser-lead.mjs");
const baselineResultFile = path.join(leadRoot, "afk-profile-browser-results.json");
const baselineResult = JSON.parse(readText(baselineResultFile));
assert.equal(baselineResult.counterexample, true, "immutable lead baseline no longer records the defect");
assert.equal(baselineResult.profileA.value, "10000");
assert.equal(baselineResult.profileB.value, "10000");
assert.equal(baselineResult.recovered.profileB, 50);

const appSourceFile = path.join(uiRoot, "src/App.jsx");
const squadsSourceFile = path.join(uiRoot, "src/SquadsPage.jsx");
const appSource = readText(appSourceFile);
const squadsSource = readText(squadsSourceFile);
assert.match(appSource, /march:\s*\{[\s\S]{0,180}profileId:\s*selectedProfileId/, "App must pass selected profile to Squads");
assert.match(squadsSource, /SquadsPage\(\{\s*previewState = "", profileId = ""/, "SquadsPage must accept profile owner");
assert.match(squadsSource, /AfkContent[^\n]+profileId=\{profileId\}/, "SquadsPage must pass owner to AFK child");
assert.match(squadsSource, /JSON\.stringify\(\[profileScope, `\$\{scope\}:\$\{previewState\}`\]\)/, "AFK store identity must use profile as first registry dimension");
for (const scope of Object.keys(originalScopeOwners)) assert.ok(squadsSource.includes(`storeScope("${scope}")`), `current ${scope} owner scope missing`);
const originalEquipmentNode = squadAst.program.body.find((node) => node.type === "FunctionDeclaration" && node.id?.name === "fd");
assert.ok(originalEquipmentNode, "missing original Equipment function fd");
assert.equal(byteOffset(squad, originalEquipmentNode.start), 176937, "original Equipment function locator drifted");
const originalEquipmentSlice = squad.slice(originalEquipmentNode.start, originalEquipmentNode.end);
assert.equal(Buffer.byteLength(originalEquipmentSlice), 14376, "original Equipment function length drifted");
assert.equal(sha256(Buffer.from(originalEquipmentSlice)), "64968D742C5AAE80AAC5A87C90EEF4E2FCCD75115F6844C35372660B0D4912B0", "original Equipment function changed");
assert.match(originalEquipmentSlice.slice(0, 400), /l=c\(\),u=e\(`equipment`,null,[\s\S]*,l\)/, "original Equipment config must use selected-profile owner");
assert.match(squadsSource, /EquipmentContent[^\n]+profileId=\{profileId\}/, "SquadsPage must pass owner to Equipment");
assert.match(squadsSource, /function EquipmentContent\(\{ previewEnabled, previewState = "", profileId = "" \}\)/, "EquipmentContent must accept profile owner");
assert.match(squadsSource, /JSON\.stringify\(\[profileId \|\| "default", `equipment:\$\{previewState\}`\]\)/, "Equipment config store must use profile as first registry dimension");

function makeDeferred() {
  let resolve;
  let reject;
  const promise = new Promise((ok, fail) => { resolve = ok; reject = fail; });
  return { promise, resolve, reject };
}

async function deferredIsolation(scope) {
  const stores = new Map();
  const previewState = "shell-profiles";
  const key = (profile) => JSON.stringify([profile || "default", `${scope}:${previewState}`]);
  const get = (profile, initial, adapter) => {
    const id = key(profile);
    const existing = stores.get(id);
    if (existing) {
      existing.setAdapter(adapter);
      return existing;
    }
    const store = createConfigDraft(structuredClone(initial), adapter);
    stores.set(id, store);
    return store;
  };

  const memoryA = { value: 0 };
  const memoryB = { value: 0 };
  const success = makeDeferred();
  const a = get("A", memoryA, {
    valid: () => true,
    read: async () => structuredClone(memoryA),
    write: async (value) => { const released = await success.promise; Object.assign(memoryA, released ? value : memoryA); return structuredClone(memoryA); },
  });
  const b = get("B", memoryB, { valid: () => true, read: async () => structuredClone(memoryB), write: async (value) => { Object.assign(memoryB, value); return structuredClone(memoryB); } });
  a.edit({ value: 1 }, false);
  const pendingSuccess = a.flush();
  assert.equal(a.getSnapshot().saving, true);
  assert.deepEqual(b.getSnapshot(), { draft: { value: 0 }, confirmed: { value: 0 }, dirty: false, saving: false, error: null });
  success.resolve(true);
  await pendingSuccess;
  assert.equal(a.getSnapshot().confirmed.value, 1);
  assert.equal(b.getSnapshot().confirmed.value, 0);

  const rejected = makeDeferred();
  a.setAdapter({
    valid: () => true,
    read: async () => structuredClone(memoryA),
    write: async () => rejected.promise,
  });
  a.edit({ value: 2 }, false);
  const pendingReject = a.flush();
  assert.equal(a.getSnapshot().saving, true);
  assert.equal(b.getSnapshot().error, null);
  rejected.reject(new Error("R1_DEFERRED_REJECTION"));
  await assert.rejects(pendingReject, /R1_DEFERRED_REJECTION/);
  assert.match(String(a.getSnapshot().error), /R1_DEFERRED_REJECTION/);
  assert.equal(b.getSnapshot().error, null);

  let failRetry = true;
  a.setAdapter({
    valid: () => true,
    read: async () => structuredClone(memoryA),
    write: async (value) => {
      if (failRetry) { failRetry = false; throw new Error("R1_RETRY_ONCE"); }
      Object.assign(memoryA, value);
      return structuredClone(memoryA);
    },
  });
  a.edit({ value: 3 }, false);
  await assert.rejects(a.flush(), /R1_RETRY_ONCE/);
  assert.equal(b.getSnapshot().draft.value, 0);
  await a.flush();
  assert.equal(a.getSnapshot().confirmed.value, 3);
  assert.equal(b.getSnapshot().confirmed.value, 0);
  a.edit({ value: 4 }, false);
  await a.refresh(true);
  assert.equal(a.getSnapshot().draft.value, 3);
  assert.equal(a.getSnapshot().dirty, false);
  assert.equal(b.getSnapshot().draft.value, 0);

  for (const store of stores.values()) store.dispose();
  return {
    keyA: key("A"),
    keyB: key("B"),
    successConfirmedA: 1,
    rejectionStayedOnA: true,
    retryConfirmedA: 3,
    discardReturnedA: 3,
    bStayedClean: 0,
  };
}

const deferred = {};
for (const scope of Object.keys(originalScopeOwners)) deferred[scope] = await deferredIsolation(scope);
deferred.equipment = await deferredIsolation("equipment");

const playwrightRequire = createRequire("C:/Users/chimw/AppData/Local/Temp/lwbridge-playwright/package.json");
const { chromium } = playwrightRequire("playwright");
const browser = await chromium.launch({ headless: true, executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe" });
const context = await browser.newContext({ viewport: { width: 1280, height: 1000 } });
const page = await context.newPage();
const issues = [];
page.on("pageerror", (error) => issues.push(`pageerror:${String(error)}`));
page.on("console", (message) => { if (["error", "warning"].includes(message.type())) issues.push(`${message.type()}:${message.text()}`); });

const cards = () => page.locator(".monster-afk-compact-card");
const checkbox = (index) => cards().nth(index).locator('input[type="checkbox"]');
async function openSettings(index) {
  const card = cards().nth(index);
  if (!(await card.evaluate((node) => node.classList.contains("is-selected")))) await card.locator('button[aria-label="Settings"]').click();
}
async function potionValue() {
  await openSettings(1);
  return page.locator('.monster-afk-toolbar-settings input[type="number"]').inputValue();
}
const potionError = () => page.locator(".monster-afk-layout > .automation-error").filter({ hasText: "Auto Use Stamina Potion" });
async function snapshot() {
  return {
    master: await checkbox(0).isChecked(),
    potion: await potionValue(),
    garrison: await checkbox(3).isChecked(),
    potionError: await potionError().count() ? (await potionError().allTextContents()).join(" | ") : "",
    zombie: await checkbox(4).isChecked(),
    dirty: await page.locator(".monster-afk-layout").getAttribute("data-draft-dirty"),
    saving: await page.locator(".monster-afk-layout").getAttribute("data-draft-saving"),
  };
}
async function selectProfile(index) {
  const profiles = page.locator(".profile-item");
  if (await profiles.count() === 0) await page.locator(".profile-collapse").click();
  await profiles.first().waitFor();
  await profiles.nth(index).click();
  await page.waitForFunction(() => !document.querySelector(".profile-switch-loading"));
  await page.locator(".monster-afk-toolbar").waitFor();
}

let browserProof;
try {
  await page.goto(`${baseUrl}/?previewPage=march&previewState=shell-profiles&previewLanguage=en&previewTheme=light`);
  await page.locator(".monster-afk-toolbar").waitFor();
  assert.equal(await cards().count(), 5, "expected five AFK toolbar cards");
  const initialA = await snapshot();
  assert.deepEqual({ master: initialA.master, potion: initialA.potion, garrison: initialA.garrison, zombie: initialA.zombie, dirty: initialA.dirty }, { master: false, potion: "50", garrison: false, zombie: false, dirty: "false" });

  await openSettings(1);
  const potionInput = page.locator('.monster-afk-toolbar-settings input[type="number"]');
  await potionInput.evaluate((node) => {
    Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value").set.call(node, "10000");
    node.dispatchEvent(new InputEvent("input", { bubbles: true, inputType: "insertText", data: "10000" }));
    node.dispatchEvent(new Event("change", { bubbles: true }));
  });
  await page.waitForFunction(() => document.querySelector(".monster-afk-layout")?.getAttribute("data-draft-saving") !== "true");
  await page.waitForFunction(() => document.querySelector(".monster-afk-layout")?.getAttribute("data-draft-dirty") === "true");
  const aEdited = await snapshot();
  assert.deepEqual({ master: aEdited.master, potion: aEdited.potion, garrison: aEdited.garrison, zombie: aEdited.zombie, dirty: aEdited.dirty }, { master: false, potion: "10000", garrison: false, zombie: false, dirty: "true" });

  await page.getByRole("tab", { name: /Equipment/i }).click();
  await page.getByRole("tab", { name: /AFK/i }).click();
  const aAfterHideReturn = await snapshot();
  assert.deepEqual({ potion: aAfterHideReturn.potion, dirty: aAfterHideReturn.dirty }, { potion: "10000", dirty: "true" });

  await selectProfile(1);
  const bClean = await snapshot();
  assert.deepEqual({ master: bClean.master, potion: bClean.potion, garrison: bClean.garrison, zombie: bClean.zombie, dirty: bClean.dirty }, { master: false, potion: "50", garrison: false, zombie: false, dirty: "false" });
  assert.equal(bClean.potionError, "");

  await selectProfile(0);
  const aRetained = await snapshot();
  assert.deepEqual({ potion: aRetained.potion, dirty: aRetained.dirty }, { potion: "10000", dirty: "true" });

  await selectProfile(1);
  const bStillClean = await snapshot();
  assert.deepEqual({ master: bStillClean.master, potion: bStillClean.potion, garrison: bStillClean.garrison, zombie: bStillClean.zombie, dirty: bStillClean.dirty }, { master: false, potion: "50", garrison: false, zombie: false, dirty: "false" });
  assert.equal(bStillClean.potionError, "");
  browserProof = { initialA, aEdited, aAfterHideReturn, bClean, aRetained, bStillClean };
} finally {
  await context.close();
  await browser.close();
}
assert.deepEqual(issues, [], `browser issues: ${JSON.stringify(issues)}`);

const result = {
  status: "PASS",
  baseUrl,
  sourceIdentity: {
    referenceExe: expected.exe,
    squadAsset: expected.squadAsset,
    mainAsset: expected.mainAsset,
    appSource: hashFile(appSourceFile),
    squadsSource: hashFile(squadsSourceFile),
    originalLocators: locators,
  },
  immutableFailureBaseline: {
    scriptSha256: hashFile(baselineScript),
    resultSha256: hashFile(baselineResultFile),
    counterexample: baselineResult.counterexample,
    currentAtLead: { profileA: baselineResult.profileA, profileB: baselineResult.profileB },
    originalAtLead: baselineResult.recovered,
  },
  originalOwnership,
  currentScopes: Object.keys(originalScopeOwners),
  deferred,
  browser: browserProof,
  issues,
  limits: [
    "Browser edits are inert local consumer events against preview-disabled controls; they do not claim native/gameplay edit availability.",
    "Deferred success/rejection uses the recovered createConfigDraft implementation with the current profile/scope key contract and controlled local adapters; no native provider runs.",
    "Protected original-runtime pixels and native/provider-positive execution remain outside this R1 proof.",
  ],
};

const resultFile = path.join(here, "profile-ownership-results.json");
if (verifyOnly) {
  const recorded = JSON.parse(readText(resultFile));
  assert.deepEqual(recorded, result, "recorded profile ownership result drifted");
  console.log(`R1_PROFILE_OWNERSHIP_VERIFY_OK scopes=${Object.keys(originalScopeOwners).length}`);
} else {
  fs.writeFileSync(resultFile, `${JSON.stringify(result, null, 2)}\n`);
  console.log(`R1_PROFILE_OWNERSHIP_OK scopes=${Object.keys(originalScopeOwners).length}`);
}
