import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const uiPackage = path.join(repo, "src/LWBridge.UI-0.3.17/package.json");
const requireUi = createRequire(uiPackage);
const parser = requireUi("@babel/parser");
const playwrightPackage = "C:/Users/chimw/AppData/Local/Temp/lwbridge-playwright/package.json";
const requirePlaywright = createRequire(playwrightPackage);
const { chromium } = requirePlaywright("playwright");
const chromeExe = "C:/Program Files/Google/Chrome/Application/chrome.exe";
const resultPath = path.join(here, "equipment-save-contract-results.json");
const screenshotDir = path.join(here, "equipment-save-contract");
const sha256 = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
const jsonSha = (value) => sha256(Buffer.from(JSON.stringify(value)));
const clone = (value) => structuredClone(value);
const slash = (value) => value.replaceAll("\\", "/");
const describe = (file) => {
  const bytes = fs.readFileSync(file);
  return { path: slash(path.relative(repo, file)), bytes: bytes.length, sha256: sha256(bytes) };
};
const summarize = (snapshot) => ({
  dirty: snapshot.dirty,
  saving: snapshot.saving,
  error: snapshot.error ? String(snapshot.error.message || snapshot.error) : null,
  draftSha256: jsonSha(snapshot.draft),
  confirmedSha256: jsonSha(snapshot.confirmed),
});

const mainAsset = path.join(repo, "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js");
const squadAsset = path.join(repo, "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/SquadPanel-HC3-DJei.js");
const mainSource = fs.readFileSync(mainAsset, "utf8");
const squadSource = fs.readFileSync(squadAsset, "utf8");
const mainAst = parser.parse(mainSource, { sourceType: "module" });
const squadAst = parser.parse(squadSource, { sourceType: "module" });
const Tnode = mainAst.program.body.find((node) => node.type === "FunctionDeclaration" && node.id?.name === "T");
const fd = squadAst.program.body.find((node) => node.type === "FunctionDeclaration" && node.id?.name === "fd");
const Pnode = fd?.body.body.find((node) => node.type === "FunctionDeclaration" && node.id?.name === "P");
assert.ok(Tnode && Pnode, "recovered config store T and Equipment helper P must exist");
const originalPByte = Buffer.byteLength(squadSource.slice(0, Pnode.start));
const originalPSource = squadSource.slice(Pnode.start, Pnode.end);
assert.equal(originalPByte, 180274, "recovered Equipment helper P UTF-8 byte");
assert.equal(originalPSource, "async function P(e){u.state.edit(e,!1);try{return await u.state.flush(!1)}catch{return null}}", "recovered Equipment helper P source");
const OriginalT = new Function("w", `return (${mainSource.slice(Tnode.start, Tnode.end)});`)(JSON.stringify);

function originalHarness(initial, { rejectFirst = false, gateFirst = false } = {}) {
  let failurePending = rejectFirst;
  let release = null;
  const gate = gateFirst ? new Promise((resolve) => { release = resolve; }) : Promise.resolve();
  let confirmed = clone(initial);
  const store = OriginalT(clone(initial), {
    valid: () => true,
    read: async () => clone(confirmed),
    write: async (draft) => {
      await gate;
      if (failurePending) {
        failurePending = false;
        throw new Error("R2_CONTROLLED_SAVE_FAILED");
      }
      confirmed = clone(draft);
      return clone(confirmed);
    },
  });
  const P = new Function("u", `return (${originalPSource});`)({ state: store });
  return { store, P, release: () => release?.() };
}

async function originalNoSecondSave(firstDraft, laterDraft, initialConfirmed) {
  const harness = originalHarness(initialConfirmed, { gateFirst: true });
  const first = harness.P(clone(firstDraft));
  harness.store.edit(clone(laterDraft), false);
  harness.release();
  await first;
  const afterFirst = clone(harness.store.getSnapshot());
  assert.equal(afterFirst.dirty, true, "exact original leaves later unrequested edit dirty");
  assert.equal(jsonSha(afterFirst.confirmed), jsonSha(firstDraft), "exact original confirms only first requested draft");
  await harness.P(clone(laterDraft));
  const afterExplicit = clone(harness.store.getSnapshot());
  assert.equal(afterExplicit.dirty, false, "exact original later explicit Save confirms later edit");
  assert.equal(jsonSha(afterExplicit.confirmed), jsonSha(laterDraft), "exact original later explicit Save confirms later draft");
  return { afterFirst: summarize(afterFirst), afterExplicit: summarize(afterExplicit) };
}

async function originalQueuedSave(firstDraft, queuedDraft, initialConfirmed) {
  const harness = originalHarness(initialConfirmed, { gateFirst: true });
  const first = harness.P(clone(firstDraft));
  const second = harness.P(clone(queuedDraft));
  harness.release();
  await Promise.all([first, second]);
  const settled = clone(harness.store.getSnapshot());
  assert.equal(settled.dirty, false, "exact original drains explicitly queued second Save");
  assert.equal(jsonSha(settled.confirmed), jsonSha(queuedDraft), "exact original queued Save confirms queued draft");
  return summarize(settled);
}

async function originalErrorContracts(initialConfirmed, rejectedDraft) {
  const retry = originalHarness(initialConfirmed, { rejectFirst: true });
  const firstResult = await retry.P(clone(rejectedDraft));
  assert.equal(firstResult, null, "exact original P converts write rejection to null");
  const rejected = clone(retry.store.getSnapshot());
  assert.equal(rejected.dirty, true, "exact original rejection retains dirty draft");
  assert.ok(rejected.error, "exact original rejection retains error");
  await retry.P(clone(rejectedDraft));
  const retried = clone(retry.store.getSnapshot());
  assert.equal(retried.dirty, false, "exact original Retry-equivalent explicit P confirms retained draft");

  const discard = originalHarness(initialConfirmed, { rejectFirst: true });
  await discard.P(clone(rejectedDraft));
  await discard.store.refresh(true);
  const discarded = clone(discard.store.getSnapshot());
  assert.equal(discarded.dirty, false, "exact original forced refresh discards rejected draft");
  assert.equal(jsonSha(discarded.confirmed), jsonSha(initialConfirmed), "exact original discard keeps prior confirmed value");
  return { rejected: summarize(rejected), retried: summarize(retried), discarded: summarize(discarded) };
}

async function twoFrames(page) {
  await page.evaluate(() => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve))));
}

async function openMounted(browser, baseUrl, { id, language = "en", theme = "light", owner = "A", state = "squads-equipment" }, issues) {
  const context = await browser.newContext({ viewport: { width: 1280, height: 1000 }, reducedMotion: "reduce" });
  const page = await context.newPage();
  page.on("pageerror", (error) => issues.push({ id, type: "pageerror", text: String(error?.stack || error) }));
  page.on("console", (message) => {
    if (["error", "warning"].includes(message.type())) issues.push({ id, type: message.type(), text: message.text() });
  });
  const url = new URL(baseUrl);
  url.search = new URLSearchParams({ previewPage: "march", previewState: state, previewLanguage: language, previewTheme: theme }).toString();
  await page.goto(url.href, { waitUntil: "domcontentloaded" });
  await page.locator(".equipment-preset-layout").waitFor();
  await page.evaluate(async ({ initialOwner, initialState }) => {
    const src = await (await fetch("/src/main.jsx")).text();
    const reactURL = src.match(/from \"([^\"]+react\.js[^\"]*)\"/)[1];
    const domURL = src.match(/from \"([^\"]+react-dom_client\.js[^\"]*)\"/)[1];
    const rm = await import(reactURL);
    const dm = await import(domURL);
    const R = rm.default;
    const D = dm.default;
    const { I18nProvider } = await import("/src/i18n.jsx");
    const { SquadsPage } = await import("/src/SquadsPage.jsx");
    const { usePreviewConfigAdapter } = await import("/src/previewConfigHook.jsx");
    document.getElementById("root").style.display = "none";
    const host = document.createElement("div");
    host.id = "r2-equipment";
    document.body.append(host);
    const root = D.createRoot(host);
    window.__r2RenderOwner = (nextOwner, nextState = initialState) => root.render(R.createElement(I18nProvider, null, R.createElement(SquadsPage, { key: nextOwner, profileId: nextOwner, previewState: nextState, activeTab: "equipment" })));
    window.__r2CaptureStore = async (scopeOwner, scopeState = initialState) => {
      const probeHost = document.createElement("div");
      document.body.append(probeHost);
      const probeRoot = D.createRoot(probeHost);
      function Probe() {
        const current = usePreviewConfigAdapter([], { valid: () => true, read: async () => [], write: async (value) => value }, JSON.stringify([scopeOwner, `equipment:${scopeState}`]));
        window.__r2EquipmentStore = current.store;
        return null;
      }
      probeRoot.render(R.createElement(Probe));
      await new Promise((resolve) => setTimeout(resolve, 30));
      probeRoot.unmount();
      probeHost.remove();
    };
    window.__r2InstallDeferredFirstWrite = () => {
      const store = window.__r2EquipmentStore;
      let release;
      const gate = new Promise((resolve) => { release = resolve; });
      window.__r2ReleaseFirstWrite = () => release();
      store.setAdapter({
        valid: () => true,
        read: async () => structuredClone(store.getSnapshot().confirmed),
        write: async (draft) => {
          await gate;
          return structuredClone(draft);
        },
      });
    };
    window.__r2RenderOwner(initialOwner, initialState);
  }, { initialOwner: owner, initialState: state });
  const panel = page.locator("#r2-equipment");
  await panel.locator(".equipment-preset-layout").waitFor();
  await twoFrames(page);
  return { context, page, panel };
}

async function renderOwner(page, panel, owner, state) {
  await page.evaluate(({ nextOwner, nextState }) => window.__r2RenderOwner(nextOwner, nextState), { nextOwner: owner, nextState: state });
  await panel.locator(".equipment-preset-layout").waitFor();
  await twoFrames(page);
}

async function captureStore(page, owner, state) {
  await page.evaluate(({ scopeOwner, scopeState }) => window.__r2CaptureStore(scopeOwner, scopeState), { scopeOwner: owner, scopeState: state });
  return page.evaluate(() => structuredClone(window.__r2EquipmentStore.getSnapshot()));
}

async function storeSnapshot(page) {
  return page.evaluate(() => structuredClone(window.__r2EquipmentStore.getSnapshot()));
}

async function startRenameSave(page, panel, name) {
  await panel.locator(".equipment-preset-toolbar .equipment-preset-actions button").nth(0).click();
  const dialog = panel.locator("dialog.equipment-preset-dialog-backdrop");
  await dialog.waitFor();
  await dialog.locator("input").fill(name);
  await dialog.locator(".equipment-preset-actions .primary").click();
}

async function makeRenderedMove(page, panel) {
  await panel.locator(".equipment-loadout-handle").first().evaluate((node) => {
    const key = Object.keys(node).find((entry) => entry.startsWith("__reactProps$"));
    node[key].onDragStart();
  });
  await twoFrames(page);
  await panel.locator(".equipment-position-card").nth(1).evaluate((node) => {
    const key = Object.keys(node).find((entry) => entry.startsWith("__reactProps$"));
    node[key].onDrop({ preventDefault() {} });
  });
  await twoFrames(page);
}

async function clickToolbarSave(panel) {
  await panel.locator(".equipment-preset-toolbar .equipment-preset-actions button").nth(2).click();
}

async function capturePanel(page, panel, id, record, screenshots) {
  await page.evaluate(() => document.fonts?.ready);
  await twoFrames(page);
  const bytes = await panel.locator(".equipment-preset-layout").screenshot();
  const file = path.join(screenshotDir, `${id}.png`);
  if (record) {
    fs.mkdirSync(screenshotDir, { recursive: true });
    fs.writeFileSync(file, bytes);
  }
  screenshots.push({ id, file: slash(path.relative(repo, file)), bytes: bytes.length, sha256: sha256(bytes) });
}

export async function runEquipmentSaveContract({ browser, baseUrl, record = false } = {}) {
  assert.ok(browser, "browser is required");
  assert.ok(baseUrl, "baseUrl is required");
  const assertions = [];
  const issues = [];
  const screenshots = [];
  const cases = {};
  const check = (name, actual, expected) => {
    assert.deepEqual(actual, expected, name);
    assertions.push({ name, actual, expected });
  };

  // Distinguishing inverse case: first Save requested, later move not requested.
  {
    const state = "squads-equipment";
    const ownerA = "R2-no-second-A";
    const ownerB = "R2-no-second-B";
    const { context, page, panel } = await openMounted(browser, baseUrl, { id: "no-second-save", owner: ownerA, state, language: "en", theme: "light" }, issues);
    try {
      const bInitialName = await (async () => {
        await renderOwner(page, panel, ownerB, state);
        const value = (await panel.locator(".equipment-preset-list > button").first().innerText()).split("\n")[0].trim();
        await renderOwner(page, panel, ownerA, state);
        return value;
      })();
      await captureStore(page, ownerA, state);
      await page.evaluate(() => window.__r2InstallDeferredFirstWrite());
      await startRenameSave(page, panel, "R2 pending A owned");
      await page.waitForFunction(() => window.__r2EquipmentStore.getSnapshot().saving === true);
      const pending = await storeSnapshot(page);
      check("R2 no-second first Save is pending", pending.saving, true);
      await renderOwner(page, panel, ownerB, state);
      check("R2 no-second profile B stays on its initial Equipment preset", (await panel.locator(".equipment-preset-list > button").first().innerText()).includes("R2 pending A owned"), false);
      check("R2 no-second profile B initial name remains unchanged", (await panel.locator(".equipment-preset-list > button").first().innerText()).split("\n")[0].trim(), bInitialName);
      await renderOwner(page, panel, ownerA, state);
      check("R2 no-second profile A retains pending rename after return", (await panel.locator(".equipment-preset-list > button").first().innerText()).includes("R2 pending A owned"), true);
      await makeRenderedMove(page, panel);
      const edited = await storeSnapshot(page);
      check("R2 no-second rendered move changes draft during first pending Save", jsonSha(edited.draft) === jsonSha(pending.draft), false);
      check("R2 no-second rendered move is dirty before acknowledgement", edited.dirty, true);
      await page.evaluate(() => window.__r2ReleaseFirstWrite());
      await page.waitForFunction(() => window.__r2EquipmentStore.getSnapshot().saving === false);
      const settled = await storeSnapshot(page);
      check("R2 no-second first acknowledgement leaves later move dirty", settled.dirty, true);
      check("R2 no-second first acknowledgement confirms only requested rename", jsonSha(settled.confirmed), jsonSha(pending.draft));
      check("R2 no-second later moved draft remains present", jsonSha(settled.draft), jsonSha(edited.draft));
      await capturePanel(page, panel, "en-light-first-ack-leaves-move-dirty", record, screenshots);
      const original = await originalNoSecondSave(pending.draft, edited.draft, pending.confirmed);
      check("R2 no-second exact original and current agree on dirty after first ack", original.afterFirst.dirty, settled.dirty);
      check("R2 no-second exact original and current agree on first confirmed draft", original.afterFirst.confirmedSha256, summarize(settled).confirmedSha256);
      await clickToolbarSave(panel);
      await page.waitForFunction(() => window.__r2EquipmentStore.getSnapshot().saving === false && window.__r2EquipmentStore.getSnapshot().dirty === false);
      const explicit = await storeSnapshot(page);
      check("R2 later explicit Save confirms the previously dirty move", jsonSha(explicit.confirmed), jsonSha(edited.draft));
      check("R2 later explicit Save clears dirty state", explicit.dirty, false);
      check("R2 later explicit Save matches exact original confirmed draft", summarize(explicit).confirmedSha256, original.afterExplicit.confirmedSha256);
      await renderOwner(page, panel, ownerB, state);
      check("R2 no-second profile B remains isolated after A acknowledgement and explicit Save", (await panel.locator(".equipment-preset-list > button").first().innerText()).split("\n")[0].trim(), bInitialName);
      cases.noSecondSave = { pending: summarize(pending), edited: summarize(edited), afterFirstAck: summarize(settled), afterExplicitSave: summarize(explicit), original };
    } finally {
      await context.close();
    }
  }

  // Explicit second Save while the first is still in flight must queue and drain.
  {
    const state = "squads-equipment";
    const ownerA = "R2-queued-A";
    const ownerB = "R2-queued-B";
    const { context, page, panel } = await openMounted(browser, baseUrl, { id: "queued-save", owner: ownerA, state, language: "ja", theme: "dark" }, issues);
    try {
      await captureStore(page, ownerA, state);
      await page.evaluate(() => window.__r2InstallDeferredFirstWrite());
      await startRenameSave(page, panel, "R2 queued A owned");
      await page.waitForFunction(() => window.__r2EquipmentStore.getSnapshot().saving === true);
      const pending = await storeSnapshot(page);
      await renderOwner(page, panel, ownerB, state);
      check("R2 queued profile B does not inherit A rename", (await panel.locator(".equipment-preset-list > button").first().innerText()).includes("R2 queued A owned"), false);
      await renderOwner(page, panel, ownerA, state);
      await makeRenderedMove(page, panel);
      const edited = await storeSnapshot(page);
      check("R2 queued rendered move changes draft", jsonSha(edited.draft) === jsonSha(pending.draft), false);
      await clickToolbarSave(panel);
      check("R2 queued explicit second Save remains in same in-flight store", (await storeSnapshot(page)).saving, true);
      const original = await originalQueuedSave(pending.draft, edited.draft, pending.confirmed);
      await page.evaluate(() => window.__r2ReleaseFirstWrite());
      await page.waitForFunction(() => window.__r2EquipmentStore.getSnapshot().saving === false);
      const settled = await storeSnapshot(page);
      check("R2 queued explicit second Save clears dirty state", settled.dirty, false);
      check("R2 queued explicit second Save confirms moved draft", jsonSha(settled.confirmed), jsonSha(edited.draft));
      check("R2 queued current and exact original agree on final confirmed draft", summarize(settled).confirmedSha256, original.confirmedSha256);
      await capturePanel(page, panel, "ja-dark-explicit-queued-save-confirmed", record, screenshots);
      await renderOwner(page, panel, ownerB, state);
      check("R2 queued profile B remains isolated after A queued Save drains", (await panel.locator(".equipment-preset-list > button").first().innerText()).includes("R2 queued A owned"), false);
      cases.queuedSave = { pending: summarize(pending), edited: summarize(edited), settled: summarize(settled), original };
    } finally {
      await context.close();
    }
  }

  // Rejection and Retry retain the owning dirty draft without crossing profiles.
  {
    const state = "squads-equipment-rename-error";
    const ownerA = "R2-retry-A";
    const ownerB = "R2-retry-B";
    const { context, page, panel } = await openMounted(browser, baseUrl, { id: "retry-isolation", owner: ownerA, state }, issues);
    try {
      const initial = await captureStore(page, ownerA, state);
      // The probe intentionally shares the cached store; remount A once so the
      // actual Equipment adapter regains ownership before exercising rejection.
      await renderOwner(page, panel, ownerB, state);
      await renderOwner(page, panel, ownerA, state);
      await startRenameSave(page, panel, "R2 retry A owned");
      await page.waitForFunction(() => window.__r2EquipmentStore.getSnapshot().error !== null);
      const rejected = await storeSnapshot(page);
      check("R2 rejection retains dirty Equipment draft", rejected.dirty, true);
      check("R2 rejection exposes save error", Boolean(rejected.error), true);
      await renderOwner(page, panel, ownerB, state);
      check("R2 rejection does not leak A draft to profile B", (await panel.locator(".equipment-preset-list > button").first().innerText()).includes("R2 retry A owned"), false);
      await renderOwner(page, panel, ownerA, state);
      const error = panel.locator(".automation-error");
      await error.waitFor();
      check("R2 rejected profile A restores retained dirty marker", (await panel.locator(".equipment-preset-list > button").first().innerText()).includes("*"), true);
      await error.locator("button").nth(0).click();
      await error.waitFor({ state: "hidden" });
      await page.waitForFunction(() => window.__r2EquipmentStore.getSnapshot().dirty === false);
      const retried = await storeSnapshot(page);
      check("R2 Retry confirms only profile A retained draft", retried.dirty, false);
      check("R2 Retry retains A rename", (await panel.locator(".equipment-preset-list > button").first().innerText()).includes("R2 retry A owned"), true);
      await renderOwner(page, panel, ownerB, state);
      check("R2 Retry leaves profile B unchanged", (await panel.locator(".equipment-preset-list > button").first().innerText()).includes("R2 retry A owned"), false);
      const original = await originalErrorContracts(initial.confirmed, rejected.draft);
      check("R2 Retry exact original also retains dirty error before Retry", original.rejected.dirty, true);
      check("R2 Retry exact original also clears dirty after explicit Retry", original.retried.dirty, false);
      cases.retry = { initial: summarize(initial), rejected: summarize(rejected), retried: summarize(retried), original: { rejected: original.rejected, retried: original.retried } };
    } finally {
      await context.close();
    }
  }

  // Rejection and Discard restore the owning confirmed value without crossing profiles.
  {
    const state = "squads-equipment-rename-error";
    const ownerA = "R2-discard-A";
    const ownerB = "R2-discard-B";
    const { context, page, panel } = await openMounted(browser, baseUrl, { id: "discard-isolation", owner: ownerA, state }, issues);
    try {
      const initial = await captureStore(page, ownerA, state);
      await renderOwner(page, panel, ownerB, state);
      await renderOwner(page, panel, ownerA, state);
      const initialName = (await panel.locator(".equipment-preset-list > button").first().innerText()).split("\n")[0].trim();
      await startRenameSave(page, panel, "R2 discard A owned");
      await page.waitForFunction(() => window.__r2EquipmentStore.getSnapshot().error !== null);
      const rejected = await storeSnapshot(page);
      await renderOwner(page, panel, ownerB, state);
      check("R2 Discard rejection does not leak A draft to profile B", (await panel.locator(".equipment-preset-list > button").first().innerText()).includes("R2 discard A owned"), false);
      await renderOwner(page, panel, ownerA, state);
      const error = panel.locator(".automation-error");
      await error.waitFor();
      await error.locator("button").nth(1).click();
      await error.waitFor({ state: "hidden" });
      await page.waitForFunction(() => window.__r2EquipmentStore.getSnapshot().dirty === false);
      const discarded = await storeSnapshot(page);
      check("R2 Discard restores profile A confirmed preset name", (await panel.locator(".equipment-preset-list > button").first().innerText()).split("\n")[0].trim(), initialName);
      check("R2 Discard clears profile A dirty state", discarded.dirty, false);
      check("R2 Discard restores profile A confirmed bytes", jsonSha(discarded.draft), jsonSha(initial.confirmed));
      await renderOwner(page, panel, ownerB, state);
      check("R2 Discard leaves profile B unchanged", (await panel.locator(".equipment-preset-list > button").first().innerText()).includes("R2 discard A owned"), false);
      const original = await originalErrorContracts(initial.confirmed, rejected.draft);
      check("R2 Discard exact original forced refresh also clears dirty", original.discarded.dirty, false);
      check("R2 Discard exact original forced refresh keeps initial confirmed bytes", original.discarded.confirmedSha256, jsonSha(initial.confirmed));
      cases.discard = { initial: summarize(initial), rejected: summarize(rejected), discarded: summarize(discarded), original: { discarded: original.discarded } };
    } finally {
      await context.close();
    }
  }

  assert.deepEqual(issues, [], `R2 Equipment save-contract browser issues: ${JSON.stringify(issues)}`);
  return {
    marker: "LWB317_FINAL_CLOSEOUT_R2_EQUIPMENT_SAVE_CONTRACT_OK",
    recovered: {
      PByte: originalPByte,
      PSource: originalPSource,
      main: describe(mainAsset),
      squad: describe(squadAsset),
    },
    current: {
      squads: describe(path.join(repo, "src/LWBridge.UI-0.3.17/src/SquadsPage.jsx")),
      draftEngine: describe(path.join(repo, "src/LWBridge.UI-0.3.17/src/previewConfig.js")),
      hook: describe(path.join(repo, "src/LWBridge.UI-0.3.17/src/previewConfigHook.jsx")),
    },
    assertions,
    cases,
    screenshots,
    issues,
    limits: "Actual mounted current SquadsPage with rendered Rename/Save/Retry/Discard and rendered React HTML5 drop handlers, plus exact recovered T/P execution. Controlled local adapters only; no native/gameplay provider or physical HTML5 drag claim.",
  };
}

async function main() {
  const args = new Map(process.argv.slice(2).map((arg) => {
    const [key, ...rest] = arg.replace(/^--/, "").split("=");
    return [key, rest.join("=") || true];
  }));
  const record = args.has("record");
  const verify = args.has("verify");
  assert.notEqual(record, verify, "pass exactly one of --record or --verify");
  const port = Number(args.get("port") || 4452);
  assert.ok(Number.isInteger(port) && port > 0 && port <= 65535, "valid --port is required");
  if (record && fs.existsSync(resultPath) && !args.has("replace")) throw new Error("equipment-save-contract-results.json already exists; use --replace only for intentional pre-review refresh");
  const browser = await chromium.launch({ headless: true, executablePath: chromeExe });
  try {
    const result = await runEquipmentSaveContract({ browser, baseUrl: `http://127.0.0.1:${port}/`, record });
    result.browserVersion = await browser.version();
    if (record) {
      fs.writeFileSync(resultPath, `${JSON.stringify(result, null, 2)}\n`);
      console.log(`R2_EQUIPMENT_SAVE_CONTRACT_OK assertions=${result.assertions.length} screenshots=${result.screenshots.length}`);
    } else {
      assert.ok(fs.existsSync(resultPath), "recorded R2 Equipment save-contract result is required");
      const recorded = JSON.parse(fs.readFileSync(resultPath, "utf8"));
      assert.deepEqual(result, recorded, "recorded R2 Equipment save-contract result drifted");
      for (const screenshot of recorded.screenshots) {
        const bytes = fs.readFileSync(path.join(repo, screenshot.file));
        assert.equal(bytes.length, screenshot.bytes, `${screenshot.id} screenshot byte count drift`);
        assert.equal(sha256(bytes), screenshot.sha256, `${screenshot.id} screenshot hash drift`);
      }
      console.log(`R2_EQUIPMENT_SAVE_CONTRACT_VERIFY_OK assertions=${result.assertions.length} screenshots=${result.screenshots.length}`);
    }
  } finally {
    await browser.close();
  }
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) await main();
