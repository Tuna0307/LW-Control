import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../../");
const outDir = path.join(here, "browser-current");
fs.mkdirSync(outDir, { recursive: true });
const req = createRequire("C:/Users/chimw/AppData/Local/Temp/lwbridge-playwright/package.json");
const { chromium } = req("playwright");
const executablePath = "C:/Program Files/Google/Chrome/Application/chrome.exe";
const baseUrl = "http://127.0.0.1:4387/";
const sha256 = (file) => crypto.createHash("sha256").update(fs.readFileSync(file)).digest("hex");

const categories = ["daily", "alliance", "resourceGather", "resources", "chat", "trade", "system"];
const expectedCards = [7, 8, 1, 2, 3, 1, 2];
const modes = [
  { id: "en-light-desktop", language: "en", theme: "light", viewport: { width: 1440, height: 1000 } },
  { id: "ja-dark-desktop", language: "ja", theme: "dark", viewport: { width: 1440, height: 1000 } },
  { id: "en-dark-desktop", language: "en", theme: "dark", viewport: { width: 1280, height: 900 } },
  { id: "ja-light-narrow", language: "ja", theme: "light", viewport: { width: 860, height: 1000 } },
];

const results = {
  task: "LWB317-UI-VISUAL-REMAINING-002/milestone-3",
  marker: "LWB317_REMAINING_M3_BROWSER_CURRENT_OK",
  browser: {}, cases: [], screenshots: [], styles: [], console: [], blockers: [
    "Untouched recovered frontend bundle cannot boot in a plain browser because its protected desktop bridge is unavailable; actual recovered renderer/source functions remain the original-side oracle.",
  ],
};
results.sourceFiles = Object.fromEntries([
  "src/LWBridge.UI-0.3.17/src/AutomationPage.jsx",
  "src/LWBridge.UI-0.3.17/src/previewAutomationContracts.js",
  "src/LWBridge.UI-0.3.17/src/previewAutomationFixtures.js",
].map((relative) => [relative, sha256(path.join(repo, relative))]));

function record(name, actual, expected) {
  assert.deepEqual(actual, expected, name);
  results.cases.push({ name, actual, expected, pass: true });
}

const styleSnapshot = async (page, selector) => page.locator(selector).first().evaluate((node, selectorText) => {
  const style = getComputedStyle(node);
  const rect = node.getBoundingClientRect();
  return {
    selector: selectorText,
    rect: { x: rect.x, y: rect.y, width: rect.width, height: rect.height },
    display: style.display, color: style.color, backgroundColor: style.backgroundColor,
    borderColor: style.borderColor, borderRadius: style.borderRadius, padding: style.padding,
    gap: style.gap, fontSize: style.fontSize, fontWeight: style.fontWeight, lineHeight: style.lineHeight,
  };
}, selector);

async function screenshot(page, id, mode, category) {
  const file = path.join(outDir, `${id}.png`);
  await page.locator("section.panel").screenshot({ path: file });
  results.screenshots.push({ id, mode, category, path: file.replaceAll("\\", "/"), sha256: sha256(file) });
}

async function openPage(browser, mode, state = "automation-config") {
  const context = await browser.newContext({ viewport: mode.viewport });
  const page = await context.newPage();
  page.on("console", (message) => results.console.push({ mode: mode.id, state, type: message.type(), text: message.text() }));
  page.on("pageerror", (error) => results.console.push({ mode: mode.id, state, type: "pageerror", text: String(error?.stack || error) }));
  const params = new URLSearchParams({ previewPage: "automation", previewState: state, previewLanguage: mode.language, previewTheme: mode.theme });
  await page.goto(`${baseUrl}?${params}`, { waitUntil: "networkidle" });
  await page.locator("section.panel .automation-categories").waitFor();
  await page.evaluate(() => document.fonts?.ready);
  return { context, page };
}

const browser = await chromium.launch({ headless: true, executablePath });
results.browser.version = await browser.version();
results.browser.executablePath = executablePath;

for (const mode of modes) {
  const { context, page } = await openPage(browser, mode);
  try {
    record(`${mode.id}: language`, await page.locator("html").getAttribute("lang"), mode.language);
    record(`${mode.id}: theme`, await page.locator("html").getAttribute("data-theme"), mode.theme);
    record(`${mode.id}: seven category tabs`, await page.locator(".automation-categories > button").count(), 7);
    const visitAll = mode.id === "en-light-desktop";
    const indexes = visitAll ? categories.map((_, index) => index) : [0, 5];
    for (const index of indexes) {
      const tabs = page.locator(".automation-categories > button");
      await tabs.nth(index).click();
      await page.waitForFunction((target) => document.querySelectorAll(".automation-categories > button")[target]?.getAttribute("aria-selected") === "true", index);
      const visibleCards = page.locator(".automation-card:visible");
      record(`${mode.id}: ${categories[index]} visible cards`, await visibleCards.count(), expectedCards[index]);
      if (visitAll || index === 5) await screenshot(page, `${mode.id}-${categories[index]}`, mode.id, categories[index]);
      if (index === 0 && mode.id === "en-light-desktop") {
        results.styles.push({ mode: mode.id, category: "daily", panel: await styleSnapshot(page, "section.panel"), card: await styleSnapshot(page, ".automation-card:visible"), header: await styleSnapshot(page, ".automation-card:visible .automation-card-header"), state: await styleSnapshot(page, ".automation-card:visible .automation-state") });
      }
    }

    if (mode.id === "en-light-desktop") {
      // Expand every collapsible settings surface in each category once and capture the resulting layout.
      for (let index = 0; index < categories.length; index += 1) {
        const tabs = page.locator(".automation-categories > button");
        await tabs.nth(index).click();
        const triggers = page.locator(".automation-card:visible .automation-config-trigger");
        const triggerCount = await triggers.count();
        for (let t = 0; t < triggerCount; t += 1) {
          if ((await triggers.nth(t).getAttribute("aria-expanded")) !== "true") await triggers.nth(t).click();
        }
        record(`expanded ${categories[index]} settings triggers`, await page.locator('.automation-card:visible .automation-config-trigger[aria-expanded="true"]').count(), triggerCount);
        await screenshot(page, `en-light-expanded-${categories[index]}`, mode.id, categories[index]);
      }

      // Retention: Trucks expansion survives an Activity category hide/show cycle.
      await page.locator(".automation-categories > button").nth(0).click();
      const trucks = page.locator(".automation-card").filter({ hasText: "Trucks" }).first();
      const trucksTrigger = trucks.locator(".automation-config-trigger");
      if ((await trucksTrigger.getAttribute("aria-expanded")) !== "true") await trucksTrigger.click();
      const truckDelay = trucks.locator('.automation-config-body input[type="number"]').first();
      await truckDelay.fill("17");
      record("Trucks draft becomes dirty before Activity hide", await trucks.locator(".automation-config-body").getAttribute("data-draft-dirty"), "true");
      await page.locator(".automation-categories > button").nth(1).click();
      record("Trucks edited value survives while Daily Activity is hidden", await truckDelay.inputValue(), "17");
      await page.locator(".automation-categories > button").nth(0).click();
      record("Trucks collapse/expand state retained through category Activity", await trucksTrigger.getAttribute("aria-expanded"), "true");
      record("Trucks edited value retained through category Activity", await truckDelay.inputValue(), "17");
    }
  } finally {
    await context.close();
  }
}

// A save may complete after its Activity becomes hidden.  The page-level error
// remains visible and the hidden card retains the failed draft until recovery.
{
  const mode = modes[0];
  const { context, page } = await openPage(browser, mode, "automation-save-error");
  try {
    const trucks = page.locator(".automation-card").filter({ hasText: "Trucks" }).first();
    const trigger = trucks.locator(".automation-config-trigger");
    if ((await trigger.getAttribute("aria-expanded")) !== "true") await trigger.click();
    const delay = trucks.locator('.automation-config-body input[type="number"]').first();
    await delay.fill("29");
    await page.locator(".automation-categories > button").nth(1).click();
    const hiddenError = page.locator('section.panel > .automation-error[role="alert"]').filter({ hasText: "Trucks" }).first();
    await hiddenError.waitFor();
    record("Hidden Daily save error remains page-owned", await hiddenError.count(), 1);
    record("Daily is hidden when Trucks save failure is reported", await page.locator(".automation-categories > button").nth(0).getAttribute("aria-selected"), "false");
    record("Failed hidden Trucks draft retains edited value", await delay.inputValue(), "29");
    await page.locator(".automation-categories > button").nth(0).click();
    record("Failed Trucks draft survives Activity restore", await delay.inputValue(), "29");
  } finally { await context.close(); }
}

// Exact Ae owns dispatch and dispatchAssist as separate config stores.  A failed
// immediate Assist save must surface under the Dispatch Assist label while a
// simultaneous Secret Task draft remains independently intact.
{
  const mode = modes[0];
  const { context, page } = await openPage(browser, mode, "automation-assist-save-error");
  try {
    const secret = page.locator(".automation-card:visible").filter({ hasText: "Secret Task" }).first();
    const trigger = secret.locator(".automation-config-trigger");
    if ((await trigger.getAttribute("aria-expanded")) !== "true") await trigger.click();
    const mainDelay = secret.locator('.automation-config-body input[type="number"]').first();
    await mainDelay.fill("37");
    const assistToggle = secret.locator('.toggle-row[role="switch"]').first();
    const assistLabel = (await assistToggle.locator("span").first().innerText()).trim();
    await assistToggle.click();
    const assistAlert = page.locator('section.panel > .automation-error[role="alert"]').filter({ hasText: assistLabel }).first();
    await assistAlert.waitFor({ timeout: 1000 });
    record("Assist save error has separate Dispatch Assist ownership", await assistAlert.count(), 1);
    record("Assist save error exposes separate Retry and Discard", await assistAlert.locator("button").count(), 2);
    record("Assist failure uses exact Assist label", (await assistAlert.locator("strong").innerText()).trim(), assistLabel);
    await assistAlert.locator("button").nth(1).click();
    await assistAlert.waitFor({ state: "detached" });
    record("Assist Discard clears only Assist store error", await page.locator('section.panel > .automation-error[role="alert"]').filter({ hasText: assistLabel }).count(), 0);
    record("Secret Task draft survives independent Assist Discard", await mainDelay.inputValue(), "37");
    await screenshot(page, "en-light-assist-independent-store", mode.id, "daily");
  } finally { await context.close(); }
}

// Save failure / Retry is real preview-store interaction, not a native action.
{
  const mode = modes[0];
  const { context, page } = await openPage(browser, mode, "automation-save-error");
  try {
    const card = page.locator(".automation-card:visible").first();
    const trigger = card.locator(".automation-config-trigger");
    if (await trigger.count() && (await trigger.getAttribute("aria-expanded")) !== "true") await trigger.click();
    const input = card.locator('input[type="number"]:not([disabled])').first();
    await input.fill("1001");
    await input.blur();
    await page.waitForTimeout(650);
    const alert = page.locator('section.panel > .automation-error[role="alert"]').first();
    await alert.waitFor();
    record("save-error is owned above panel title", await alert.evaluate((node) => node.parentElement?.classList.contains("panel") && node.nextElementSibling?.classList.contains("panel-title")), true);
    record("save-error exposes Retry and Discard", await alert.locator("button").count(), 2);
    await alert.locator("button").first().click();
    await page.waitForFunction(() => !document.querySelector('section.panel > .automation-error[role="alert"]'));
    record("Retry clears preview save error", await page.locator('section.panel > .automation-error[role="alert"]').count(), 0);
    await screenshot(page, "en-light-save-error-retried", mode.id, "daily");
  } finally { await context.close(); }
}

// Separate context proves Discard restores confirmed draft after the same first-save failure.
{
  const mode = modes[0];
  const { context, page } = await openPage(browser, mode, "automation-weekly-save-error");
  try {
    const trucks = page.locator(".automation-card:visible").filter({ hasText: "Trucks" }).first();
    const trigger = trucks.locator(".automation-config-trigger");
    await trigger.click();
    const first = trucks.locator(".automation-weekly-quality select").first();
    const before = await first.inputValue();
    const next = before === "ur" ? "ssr" : "ur";
    await first.selectOption(next);
    await page.waitForTimeout(150);
    const alert = page.locator('section.panel > .automation-error[role="alert"]').filter({ hasText: "Trucks" }).first();
    await alert.waitFor();
    record("weekly edit retained after failed save", await first.inputValue(), next);
    await alert.locator("button").nth(1).click();
    await page.waitForFunction(() => ![...document.querySelectorAll('section.panel > .automation-error[role="alert"]')].some((alert) => alert.innerText.includes('Trucks')));
    record("Discard restores confirmed weekly value", await first.inputValue(), before);
    await screenshot(page, "en-light-weekly-discarded", mode.id, "daily");
  } finally { await context.close(); }
}

// Trade purchase-history tab and local cross-server switch.
{
  const mode = modes[0];
  const { context, page } = await openPage(browser, mode, "automation-trade-history");
  try {
    await page.locator(".automation-categories > button").nth(5).click();
    const card = page.locator(".automation-card:visible");
    record("Trade history fixture has one Trade card", await card.count(), 1);
    const tradeTrigger = card.locator(".automation-config-trigger");
    if ((await tradeTrigger.getAttribute("aria-expanded")) !== "true") await tradeTrigger.click();
    const tradeTabs = card.locator('.trade-station-tabs [role="tab"]');
    record("Trade has Goods/Purchased tabs", await tradeTabs.count(), 2);
    await tradeTabs.nth(1).click();
    record("Trade purchase history groups render", (await card.locator(".trade-station-purchase-day").count()) > 0, true);
    record("Trade purchase rows render", (await card.locator(".trade-station-purchase").count()) >= 6, true);
    const cross = card.locator('.automation-config-body [role="switch"]').first();
    const before = await cross.getAttribute("aria-checked");
    await cross.click();
    record("Trade cross-server local switch toggles", await cross.getAttribute("aria-checked"), before === "true" ? "false" : "true");
    await screenshot(page, "en-light-trade-history-purchases", mode.id, "trade");
  } finally { await context.close(); }
}

// Card structure distinctions and important nested local-only forms.
{
  const mode = modes[0];
  const { context, page } = await openPage(browser, mode, "automation-config");
  try {
    const categoryTab = (index) => page.locator(".automation-categories > button").nth(index);
    const visibleCard = (title) => page.locator(".automation-card:visible").filter({ hasText: title }).first();

    await categoryTab(0).click();
    const treatment = visibleCard("Automatic Treatment");
    record("non-collapsible Treatment settings body visible", await treatment.locator(".automation-config-body:visible").count(), 1);
    record("non-collapsible Treatment has no settings trigger", await treatment.locator(".automation-config-trigger").count(), 0);
    const stamina = visibleCard("Free Stamina");
    record("Free Stamina has no settings body", await stamina.locator(".automation-config-body").count(), 0);
    record("Free Stamina has no settings trigger", await stamina.locator(".automation-config-trigger").count(), 0);

    const construction = visibleCard("Automatic Construction");
    record("Construction defaults expanded in preview", await construction.locator(".automation-config-trigger").getAttribute("aria-expanded"), "true");
    const constructionDetails = construction.locator("details.construction-type-select");
    record("Construction building type chooser exists", await constructionDetails.count(), 1);
    await constructionDetails.locator("summary").click();
    const constructionTabs = construction.locator('.construction-category-tabs [role="tab"]');
    record("Construction category tabs", await constructionTabs.count(), 6);
    await constructionTabs.first().focus();
    await constructionTabs.first().press("ArrowRight");
    record("Construction ArrowRight moves category tab", await constructionTabs.nth(1).getAttribute("aria-selected"), "true");

    const training = visibleCard("Auto Training");
    const trainingTrigger = training.locator(".automation-config-trigger");
    if ((await trainingTrigger.getAttribute("aria-expanded")) !== "true") await trainingTrigger.click();
    record("Auto Training fixture camps", await training.locator(".soldier-training-camp").count(), 2);
    record("Auto Training progress surface present", (await training.locator('[role="status"]').count()) > 0, true);

    await categoryTab(1).click();
    for (const title of ["Alliance Tech Donations", "Automatic Official Application", "Alliance Gifts", "Excavation Stronghold Resources", "Alliance Center Resources"]) {
      const card = visibleCard(title);
      record(`${title} non-collapsible body visible`, await card.locator(".automation-config-body:visible").count(), 1);
      record(`${title} non-collapsible has no trigger`, await card.locator(".automation-config-trigger").count(), 0);
    }
    const help = visibleCard("Alliance Help");
    record("Alliance Help has no settings body", await help.locator(".automation-config-body").count(), 0);

    await categoryTab(3).click();
    for (const title of ["Building Resource Collection", "Armed Truck"]) {
      const card = visibleCard(title);
      record(`${title} non-collapsible body visible`, await card.locator(".automation-config-body:visible").count(), 1);
      record(`${title} has no settings trigger`, await card.locator(".automation-config-trigger").count(), 0);
    }

    await categoryTab(6).click();
    for (const title of ["Weekend Shield", "Attack Shield"]) {
      const card = visibleCard(title);
      record(`${title} has no settings body`, await card.locator(".automation-config-body").count(), 0);
      record(`${title} has no settings trigger`, await card.locator(".automation-config-trigger").count(), 0);
    }
  } finally { await context.close(); }
}

// Resource Gathering validation and runtime-state presentation use only browser preview state.
for (const state of ["automation-gather-no-squads", "automation-gather-manual_wait", "automation-gather-shield_paused", "automation-gather-recalling", "automation-gather-recall_failed", "automation-gather-state_unconfirmed"]) {
  const mode = modes[0];
  const { context, page } = await openPage(browser, mode, state);
  try {
    await page.locator(".automation-categories > button").nth(2).click();
    const card = page.locator(".automation-card:visible");
    const trigger = card.locator(".automation-config-trigger");
    if ((await trigger.getAttribute("aria-expanded")) !== "true") await trigger.click();
    if (state === "automation-gather-no-squads") {
      record("Gather no-squads fixture exposes zero squad rows", await card.locator(".automation-resource-gather-squad").count(), 0);
      await card.locator('.automation-header-switch[role="switch"]').click();
      record("Gather enable requires a configured squad", await card.locator('.automation-error[role="alert"]').count(), 1);
    } else {
      const text = await card.locator(".automation-resource-gather-state").first().innerText();
      record(`Gather runtime ${state} has state text`, text.trim().length > 0, true);
      if (state === "automation-gather-manual_wait") record("Gather manual wait exposes resume detail", await card.locator(".automation-resource-gather-state small").count(), 1);
      if (state === "automation-gather-shield_paused") record("Gather shield pause exposes shield detail", await card.locator(".automation-resource-gather-state small").count(), 1);
    }
  } finally { await context.close(); }
}

// Secret Task Assist browser branches: source replay covers exact row structure; mounted browser confirms layout/state controls.
for (const state of ["automation-assist-schedule", "automation-assist-waiting", "automation-assist-retry-wait", "automation-assist-running", "automation-assist-failed", "automation-assist-expired", "automation-assist-busy"]) {
  const mode = modes[0];
  const { context, page } = await openPage(browser, mode, state);
  try {
    const secret = page.locator(".automation-card:visible").filter({ hasText: "Secret Task" }).first();
    const trigger = secret.locator(".automation-config-trigger");
    if ((await trigger.getAttribute("aria-expanded")) !== "true") await trigger.click();
    record(`Assist ${state} settings rendered`, await secret.locator(".automation-config-body:visible").count(), 1);
    record(`Assist ${state} manual task rows rendered`, (await secret.locator(".automation-assist-task-row").count()) > 0, true);
    if (state === "automation-assist-busy") record("Assist busy disables local task controls", (await secret.locator("input:disabled, button:disabled").count()) > 0, true);
  } finally { await context.close(); }
}

// Auto Training conditional browser branches.
for (const state of ["automation-training-no-camps", "automation-training-data-unavailable", "automation-training-open-failed", "automation-training-unavailable-level", "automation-training-error", "automation-training-no-order", "automation-validation-error"]) {
  const mode = modes[0];
  const { context, page } = await openPage(browser, mode, state);
  try {
    const training = page.locator(".automation-card:visible").filter({ hasText: "Auto Training" }).first();
    const trigger = training.locator(".automation-config-trigger");
    if ((await trigger.getAttribute("aria-expanded")) !== "true") await trigger.click();
    record(`Training ${state} body visible`, await training.locator(".automation-config-body:visible").count(), 1);
    if (state === "automation-training-no-camps") record("Training no-camps has zero camp rows", await training.locator(".soldier-training-camp").count(), 0);
    if (state === "automation-training-data-unavailable") record("Training data unavailable has status message", (await training.locator('[role="status"]').count()) > 0, true);
    if (state === "automation-training-error") record("Training error has alert", (await training.locator('[role="alert"]').count()) > 0, true);
    if (state === "automation-validation-error") record("Training validation error is surfaced", (await training.locator('[role="alert"]').count()) > 0, true);
  } finally { await context.close(); }
}

// Trade loading/error/empty variants remain browser-visible inert fixture states.
for (const state of ["automation-trade-empty", "automation-trade-loading", "automation-trade-loading-retained", "automation-trade-error", "automation-trade-error-retained", "automation-trade-history-empty", "automation-trade-status-absent"]) {
  const mode = modes[0];
  const { context, page } = await openPage(browser, mode, state);
  try {
    await page.locator(".automation-categories > button").nth(5).click();
    const card = page.locator(".automation-card:visible");
    record(`Trade ${state} card visible`, await card.count(), 1);
    const tradeTrigger = card.locator(".automation-config-trigger");
    if ((await tradeTrigger.getAttribute("aria-expanded")) !== "true") await tradeTrigger.click();
    if (state.includes("loading")) record(`Trade ${state} loading copy`, (await card.locator(".trade-station-goods .muted").count()) > 0, true);
    if (state.includes("error")) {
      record(`Trade ${state} error visible`, await card.locator('.automation-error').count(), 1);
      record(`Trade ${state} fetch error has exact non-alert DOM`, await card.locator('.automation-error[role="alert"]').count(), 0);
    }
    if (state === "automation-trade-history-empty") {
      await card.locator('.trade-station-tabs [role="tab"]').nth(1).click();
      record("Trade empty history has no purchase day groups", await card.locator(".trade-station-purchase-day").count(), 0);
    }
    if (state === "automation-trade-status-absent") record("Trade absent status renders dash last result", (await card.locator(".trade-station-stats").innerText()).includes("-"), true);
  } finally { await context.close(); }
}

// Railway's running composition disables both Weekly quality edits and the
// depart-when-insufficient setting while keeping the native action fenced.
{
  const mode = modes[0];
  const { context, page } = await openPage(browser, mode, "automation-runtime-running");
  try {
    const trucks = page.locator(".automation-card:visible").filter({ hasText: "Trucks" }).first();
    const trigger = trucks.locator(".automation-config-trigger");
    if ((await trigger.getAttribute("aria-expanded")) !== "true") await trigger.click();
    record("Running Railway header reflects enabled config", await trucks.locator('.automation-header-switch[role="switch"]').getAttribute("aria-checked"), "true");
    record("Running Railway disables all Weekly quality selects", await trucks.locator('.automation-weekly-quality select:not(:disabled)').count(), 0);
    record("Running Railway disables depart-when-insufficient", await trucks.locator('.automation-config-body input[type="checkbox"]:not(:disabled)').count(), 0);
    record("Running Railway uses recovered Stop action label", (await trucks.locator(".automation-run-action").innerText()).trim(), "Stop");
    await screenshot(page, "en-light-railway-running", mode.id, "daily");
  } finally { await context.close(); }
}

// Whole-Ae correction: recovered status state renders through AutomationCard state,
// not seven invented generic action-failed alert blocks.
{
  const mode = modes[0];
  const { context, page } = await openPage(browser, mode, "automation-runtime-error");
  try {
    record("generic runtime error fixture has no invented daily alert blocks", await page.locator('.automation-card:visible > .automation-error[role="alert"]').count(), 0);
    record("inactive runtime-error fixture remains source-disabled", await page.locator('.automation-card:visible .automation-state.state-disabled').count(), 7);
  } finally { await context.close(); }
}

// Exact Ae owns one discovered squad-index set shared by Alliance Gather and
// Treasure dispatch; selected entries precede newly discovered entries and the
// Treasure list is a draggable priority list, not a fixed 1..4 checkbox group.
{
  const mode = modes[0];
  const { context, page } = await openPage(browser, mode, "automation-squads-34");
  try {
    const tabs = page.locator(".automation-categories > button");
    await tabs.nth(1).click();
    const gather = page.locator(".automation-card:visible").filter({ hasText: "Alliance Gathering Dispatch" }).first();
    const gatherTrigger = gather.locator(".automation-config-trigger");
    if ((await gatherTrigger.getAttribute("aria-expanded")) !== "true") await gatherTrigger.click();
    record("Alliance Gather uses discovered non-contiguous squad set", await gather.locator(".automation-squad-priority-item").allTextContents(), ["Squad 3", "Squad 4"]);
    record("Alliance Gather selected squad has recovered drag handle", await gather.locator(".automation-squad-drag-handle").count(), 1);

    await tabs.nth(2).click();
    const resourceGather = page.locator(".automation-card:visible").first();
    const resourceTrigger = resourceGather.locator(".automation-config-trigger");
    if ((await resourceTrigger.getAttribute("aria-expanded")) !== "true") await resourceTrigger.click();
    const resourceSquadLabels = await resourceGather.locator(".automation-resource-gather-squad").evaluateAll((rows) => rows.map((row) => row.getAttribute("aria-label")));
    record("Resource Gather receives discovered Squad 3", resourceSquadLabels.some((label) => /3/.test(label || "")), true);
    record("Resource Gather receives discovered Squad 4", resourceSquadLabels.some((label) => /4/.test(label || "")), true);

    await tabs.nth(4).click();
    const treasure = page.locator(".automation-card:visible").filter({ hasText: "Treasure" }).first();
    const treasureTrigger = treasure.locator(".automation-config-trigger");
    if ((await treasureTrigger.getAttribute("aria-expanded")) !== "true") await treasureTrigger.click();
    record("Treasure uses discovered non-contiguous squad set", await treasure.locator(".automation-squad-priority-item").allTextContents(), ["Squad 3", "Squad 4"]);
    record("Treasure selected squad row is draggable", await treasure.locator('.automation-squad-priority-item.selected').first().getAttribute("draggable"), "true");
    record("Treasure selected squad has recovered drag handle", await treasure.locator(".automation-squad-drag-handle").count(), 1);
    record("Treasure fixed Squad 1 is absent", await treasure.getByText("Squad 1", { exact: true }).count(), 0);
    record("Treasure fixed Squad 2 is absent", await treasure.getByText("Squad 2", { exact: true }).count(), 0);
    await screenshot(page, "en-light-squads-34-treasure", mode.id, "chat");

    // Exact discovery retains the last non-empty set when a later discovery is
    // empty.  Change preview state without remounting the retained Automation page,
    // force an App rerender through route navigation, then return to Automation.
    await page.evaluate(() => {
      const url = new URL(window.location.href);
      url.searchParams.set("previewState", "automation-squads-empty");
      window.history.replaceState({}, "", url);
    });
    await page.locator(".side-nav > button").nth(0).click();
    await page.locator(".side-nav > button").nth(1).click();
    await page.locator("section.panel .automation-categories").waitFor();
    const retainedTabs = page.locator(".automation-categories > button");
    await retainedTabs.nth(1).click();
    const retainedGather = page.locator(".automation-card:visible").filter({ hasText: "Alliance Gathering Dispatch" }).first();
    record("Empty rediscovery retains prior Alliance squad provider", await retainedGather.locator(".automation-squad-priority-item").allTextContents(), ["Squad 3", "Squad 4"]);
    await retainedTabs.nth(4).click();
    const retainedTreasure = page.locator(".automation-card:visible").filter({ hasText: "Treasure" }).first();
    record("Empty rediscovery retains prior Treasure squad provider", await retainedTreasure.locator(".automation-squad-priority-item").allTextContents(), ["Squad 3", "Squad 4"]);
  } finally { await context.close(); }
}

// Card-owned validation errors remain outside the settings fieldset, matching
// recovered AutomationCard error ancestry even when settings are collapsed.
{
  const mode = modes[0];
  const { context, page } = await openPage(browser, mode, "automation-config");
  try {
    const construction = page.locator(".automation-card:visible").filter({ hasText: "Automatic Construction" }).first();
    const maxBuilders = construction.locator('.automation-config-body input[type="number"][max="4"]');
    await maxBuilders.fill("5");
    const alert = construction.locator(':scope > .automation-error[role="alert"]');
    record("Construction validation error is card-owned", await alert.count(), 1);
    record("Construction validation error is outside config body", await construction.locator('.automation-config-body .automation-error[role="alert"]').count(), 0);
  } finally { await context.close(); }
}

await browser.close();
const errors = results.console.filter((entry) => entry.type === "error" || entry.type === "pageerror");
// Vite/React warnings would make this gate fail; favicon/network noise is not expected on this app.
record("browser console/page errors", errors.length, 0);
fs.writeFileSync(path.join(here, "browser-current-results.json"), `${JSON.stringify(results, null, 2)}\n`);
console.log(JSON.stringify({ marker: results.marker, cases: results.cases.length, screenshots: results.screenshots.length, styleSets: results.styles.length, consoleErrors: errors.length, browser: results.browser.version }, null, 2));
