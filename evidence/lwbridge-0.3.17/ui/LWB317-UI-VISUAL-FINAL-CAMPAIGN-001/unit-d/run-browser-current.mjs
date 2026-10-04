import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const outDir = path.join(here, "browser-current");
fs.mkdirSync(outDir, { recursive: true });
const req = createRequire("C:/Users/chimw/AppData/Local/Temp/lwbridge-playwright/package.json");
const { chromium } = req("playwright");
const executablePath = "C:/Program Files/Google/Chrome/Application/chrome.exe";
const baseUrl = "http://127.0.0.1:4336/";
const sha256 = (file) => crypto.createHash("sha256").update(fs.readFileSync(file)).digest("hex");
const originalEnglishPath = path.resolve(here, "../../frontend-package/web/assets/en-BisSXcTB.js");
const originalEnglish = fs.readFileSync(originalEnglishPath, "utf8");
const undiscoveredMatch = /"squad\.afkUndiscovered":`([^`]*)`/.exec(originalEnglish);
assert.ok(undiscoveredMatch, "Recovered undiscovered label must exist");
const undiscoveredLabel = undiscoveredMatch[1];

const modes = [
  { id: "en-light-desktop", language: "en", theme: "light", viewport: { width: 1440, height: 1000 } },
  { id: "ja-dark-desktop", language: "ja", theme: "dark", viewport: { width: 1440, height: 1000 } },
  { id: "en-dark-desktop", language: "en", theme: "dark", viewport: { width: 1280, height: 900 } },
  { id: "ja-light-narrow", language: "ja", theme: "light", viewport: { width: 720, height: 1000 } },
];

const report = {
  result: "LWB317_VISUAL_FINAL_UNIT_D_BROWSER_CURRENT_OK",
  cases: [], screenshots: [], console: [],
  dragProof: "DOM_DISPATCH_ONLY_NOT_PHYSICAL_HTML5_DRAG",
  browser: "",
};
const record = (name, actual, expected) => {
  const pass = typeof expected === "function" ? expected(actual) : Object.is(actual, expected);
  report.cases.push({ name, actual, expected: typeof expected === "function" ? "predicate" : expected, pass });
  assert.ok(pass, `${name}: actual=${JSON.stringify(actual)} expected=${JSON.stringify(expected)}`);
};

async function openPage(browser, mode, state) {
  const context = await browser.newContext({ viewport: mode.viewport });
  const page = await context.newPage();
  page.on("console", (message) => report.console.push({ state, mode: mode.id, type: message.type(), text: message.text() }));
  page.on("pageerror", (error) => report.console.push({ state, mode: mode.id, type: "pageerror", text: String(error?.stack || error) }));
  const params = new URLSearchParams({ previewPage: "march", previewState: state, previewLanguage: mode.language, previewTheme: mode.theme });
  await page.goto(`${baseUrl}?${params}`, { waitUntil: "networkidle" });
  await page.locator("section.squad-panel").waitFor();
  await page.evaluate(() => document.fonts?.ready);
  return { context, page };
}

async function screenshot(page, id, mode, surface) {
  const file = path.join(outDir, `${id}.png`);
  await page.locator("section.squad-panel").screenshot({ path: file });
  report.screenshots.push({ id, mode, surface, path: file.replaceAll("\\", "/"), sha256: sha256(file), viewport: await page.evaluate(() => ({ width: innerWidth, height: innerHeight, dpr: devicePixelRatio })), fonts: await page.evaluate(() => document.fonts?.status || "unknown") });
}

const browser = await chromium.launch({ headless: true, executablePath });
report.browser = await browser.version();

// Required locale/theme/viewport matrix, with both Squads subpanels in every mode.
for (const mode of modes) {
  {
    const { context, page } = await openPage(browser, mode, "squads-profile-members-positive");
    try {
      record(`${mode.id} document language`, await page.locator("html").getAttribute("lang"), mode.language);
      record(`${mode.id} squad tabs`, await page.locator('.squad-tabs [role="tab"]').count(), 2);
      record(`${mode.id} AFK tab selected`, await page.locator('.squad-tabs [role="tab"]').nth(0).getAttribute("aria-selected"), "true");
      record(`${mode.id} AFK compact cards`, await page.locator(".monster-afk-compact-card:visible").count(), 5);
      record(`${mode.id} AFK profiles populated`, await page.locator(".monster-afk-profile-card:visible").count(), (value) => value >= 1);
      record(`${mode.id} AFK editor visible`, await page.locator(".monster-afk-editor:visible").count(), 1);
      if (mode.id === "ja-light-narrow") record("JA narrow AFK no document overflow", await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 2), true);
      await screenshot(page, `${mode.id}-afk`, mode.id, "afk");
    } finally { await context.close(); }
  }
  {
    const { context, page } = await openPage(browser, mode, "squads-equipment");
    try {
      record(`${mode.id} Equipment tab selected`, await page.locator('.squad-tabs [role="tab"]').nth(1).getAttribute("aria-selected"), "true");
      record(`${mode.id} equipment preset buttons`, await page.locator(".equipment-preset-list > button").count(), 4);
      record(`${mode.id} equipment squad groups`, await page.locator(".equipment-preset-squad:visible").count(), 4);
      record(`${mode.id} equipment position cards`, await page.locator(".equipment-position-card:visible").count(), 8);
      record(`${mode.id} equipment slots`, await page.locator(".preset-equipment-slot:visible").count(), 32);
      if (mode.id === "ja-light-narrow") record("JA narrow Equipment no document overflow", await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 2), true);
      await screenshot(page, `${mode.id}-equipment`, mode.id, "equipment");
    } finally { await context.close(); }
  }
}

// AFK local interactions, nested forms, keyboard reorder and Activity retention.
{
  const mode = modes[0];
  const { context, page } = await openPage(browser, mode, "squads-profile");
  try {
    const cards = page.locator(".monster-afk-compact-card:visible");
    record("AFK toolbar has five compact cards", await cards.count(), 5);
    const potion = cards.nth(1);
    await potion.locator("button").click();
    const potionPanel = page.locator("section.monster-afk-toolbar-settings:visible");
    record("Potion settings panel opens", await potionPanel.count(), 1);
    const stamina = potionPanel.locator('input[type="number"]');
    await stamina.fill("42");
    await stamina.blur();
    await cards.nth(2).locator("button").click();
    record("Alliance Drill nested form opens", await page.locator(".automation-squad-priority:visible").count(), 1);
    await cards.nth(1).locator("button").click();
    record("Potion local draft retained across nested panel switch", await page.locator('section.monster-afk-toolbar-settings:visible input[type="number"]').inputValue(), "42");

    const profiles = page.locator(".monster-afk-profile-card:visible");
    record("default AFK fixture profiles", await profiles.count(), 2);
    const beforeFirst = await profiles.nth(0).locator(".monster-afk-profile-heading strong").innerText();
    await profiles.nth(0).locator(".monster-afk-profile-drag").focus();
    await profiles.nth(0).locator(".monster-afk-profile-drag").press("ArrowDown");
    const afterFirst = await page.locator(".monster-afk-profile-card:visible").nth(0).locator(".monster-afk-profile-heading strong").innerText();
    record("AFK keyboard reorder changes profile order", afterFirst !== beforeFirst, true);

    const add = page.locator('.monster-afk-add-control > button[aria-haspopup="menu"]');
    await add.click();
    record("AFK add menu has farm/join choices", await page.locator('.monster-afk-add-menu [role="menuitem"]').count(), 2);
    const countBeforeAdd = await page.locator(".monster-afk-profile-card:visible").count();
    await page.locator('.monster-afk-add-menu [role="menuitem"]').nth(1).click();
    record("AFK Add Join creates local draft profile", await page.locator(".monster-afk-profile-card:visible").count(), countBeforeAdd + 1);

    const editorName = page.locator(".monster-afk-editor:visible input").first();
    await editorName.fill("Retained browser draft");
    await page.locator('.squad-tabs [role="tab"]').nth(1).click();
    record("Equipment visible after AFK Activity hide", await page.locator(".equipment-preset-layout:visible").count(), 1);
    await page.locator('.squad-tabs [role="tab"]').nth(0).click();
    record("AFK editor draft retained through Activity", await page.locator(".monster-afk-editor:visible input").first().inputValue(), "Retained browser draft");
    await screenshot(page, "en-light-afk-interactions", mode.id, "afk-interactions");
  } finally { await context.close(); }
}

// Garrison runtime/modal/selection and explicitly DOM-dispatched drag handlers.
{
  const mode = modes[0];
  const { context, page } = await openPage(browser, mode, "squads-profile-garrison-running");
  try {
    const panel = page.locator(".garrison-settings:visible");
    record("Garrison settings visible", await panel.count(), 1);
    record("Garrison running assignment row", await panel.locator(".garrison-assignment").count(), 1);
    record("Garrison Run now remains native-fenced", await panel.locator('.garrison-actions button[data-preview-action="presentation-only"]').isDisabled(), true);
    const choose = panel.locator(".garrison-target-panel .garrison-section-heading button");
    await choose.click();
    record("Garrison ally picker opens", await page.locator(".garrison-modal:visible").count(), 1);
    const search = page.locator(".garrison-modal:visible input[placeholder]");
    await search.fill("Avery");
    record("Garrison ally search filters locally", await page.locator(".garrison-modal:visible .garrison-ally-list label").count(), 1);
    await page.keyboard.press("Escape");
    record("Garrison Escape listener closes visible picker", await page.locator(".garrison-modal:visible").count(), 0);

    const priorities = panel.locator(".garrison-priority-item");
    if (await priorities.count() >= 2) {
      const before = await priorities.allInnerTexts();
      await priorities.nth(0).dispatchEvent("dragstart");
      await priorities.nth(1).dispatchEvent("dragover");
      await priorities.nth(1).dispatchEvent("drop");
      const after = await panel.locator(".garrison-priority-item").allInnerTexts();
      record("Garrison DOM-dispatched handler drag reorders targets", JSON.stringify(after) !== JSON.stringify(before), true);
    }
    await screenshot(page, "en-light-garrison-running", mode.id, "garrison");
  } finally { await context.close(); }
}

// Garrison and Zombie conditional visual branches.
for (const state of ["squads-profile-garrison-unavailable", "squads-profile-garrison-error", "squads-profile-garrison-no-squads", "squads-profile-garrison-no-targets"]) {
  const { context, page } = await openPage(browser, modes[0], state);
  try {
    const panel = page.locator(".garrison-settings:visible");
    record(`${state} panel visible`, await panel.count(), 1);
    if (state.endsWith("unavailable")) record("Garrison unavailable building controls disabled", (await panel.locator('.garrison-building input:disabled').count()) > 0, true);
    if (state.endsWith("error")) record("Garrison runtime error surfaced", await panel.locator('[role="alert"]').count(), 1);
    if (state.endsWith("no-squads")) record("Garrison no-squads validation copy visible", (await panel.locator(".muted").count()) > 0, true);
    if (state.endsWith("no-targets")) record("Garrison no-target target-required copy visible", (await panel.locator(".garrison-target-panel .muted").count()) > 0, true);
  } finally { await context.close(); }
}
for (const state of ["squads-profile-zombie-running", "squads-profile-zombie-error"]) {
  const { context, page } = await openPage(browser, modes[0], state);
  try {
    const panel = page.locator("section.monster-afk-toolbar-settings:visible");
    record(`${state} settings panel visible`, await panel.count(), 1);
    if (state.endsWith("running")) record("Zombie running assignments table", (await panel.locator("tbody tr").count()) > 0, true);
    if (state.endsWith("error")) record("Zombie error alert", await panel.locator('[role="alert"]').count(), 1);
  } finally { await context.close(); }
}

// Profile target/member/validation branches.
for (const state of ["squads-profile-target-loading", "squads-profile-target-failed", "squads-profile-target-undiscovered", "squads-profile-range-warning", "squads-profile-potion-invalid", "squads-profile-members-empty", "squads-profile-members-left", "squads-profile-members-self", "squads-profile-members-offline"]) {
  const { context, page } = await openPage(browser, modes[0], state);
  try {
    record(`${state} AFK surface visible`, await page.locator(".monster-afk-layout:visible").count(), 1);
    if (state === "squads-profile-target-loading") record("target loading status", (await page.locator(".monster-afk-editor .muted").count()) > 0, true);
    if (state === "squads-profile-target-failed") record("target failed status", (await page.locator(".monster-afk-editor .status-error").count()) > 0, true);
    if (state === "squads-profile-target-undiscovered") record("undiscovered target uses recovered English label", (await page.locator(".monster-afk-profile-card").innerText()).includes(undiscoveredLabel), true);
    if (state === "squads-profile-range-warning") record("range warning class", await page.locator(".monster-afk-level-warning").count(), 1);
    if (state === "squads-profile-potion-invalid") record("invalid potion number exposed", await page.locator('section.monster-afk-toolbar-settings input[type="number"]').count(), 1);
    if (state.startsWith("squads-profile-members-")) record(`${state} join editor rendered`, await page.locator(".monster-afk-editor:visible").count(), 1);
  } finally { await context.close(); }
}

// AFK preview-store save failure: Retry and Discard are local config-store actions only.
{
  const { context, page } = await openPage(browser, modes[0], "squads-profile-toolbar-save-error");
  try {
    const potionCard = page.locator(".monster-afk-compact-card:visible").nth(1);
    await potionCard.locator('label.monster-afk-compact-toggle').click();
    await page.waitForTimeout(100);
    const alert = page.locator('.monster-afk-layout .automation-error[role="alert"]').first();
    await alert.waitFor();
    record("AFK toolbar failed save exposes Retry/Discard", await alert.locator("button").count(), 2);
    await alert.locator("button").first().click();
    await page.waitForFunction(() => !document.querySelector('.monster-afk-layout .automation-error[role="alert"]'));
    record("AFK toolbar Retry clears error", await page.locator('.monster-afk-layout .automation-error[role="alert"]').count(), 0);
  } finally { await context.close(); }
}

// Equipment selection, rename acknowledgement, shortcut, Activity retention and DOM drag proof.
{
  const { context, page } = await openPage(browser, modes[0], "squads-equipment");
  try {
    const presets = page.locator(".equipment-preset-list > button");
    await presets.nth(1).click();
    record("Equipment preset 2 selected", await presets.nth(1).getAttribute("class"), "active");
    await page.locator(".equipment-preset-actions").first().getByRole("button").first().click();
    const dialog = page.locator("dialog.equipment-preset-dialog-backdrop");
    record("Equipment rename dialog opens", await dialog.getAttribute("open"), "");
    const rename = dialog.locator("input");
    await rename.fill("Browser retained preset");
    await dialog.getByRole("button").filter({ hasText: /Save|保存/ }).click();
    await page.waitForFunction(() => !document.querySelector('dialog.equipment-preset-dialog-backdrop[open]'));
    record("Equipment rename acknowledged and closes", await page.locator(".equipment-preset-list > button.active span").innerText(), "Browser retained preset");

    await page.keyboard.press("Alt+3");
    record("Equipment Alt+3 local shortcut", await page.locator(".equipment-preset-layout").getAttribute("data-preview-action"), "apply-all:equipment-preset-fixed-3");

    const slots = page.locator('.preset-equipment-slot[draggable="true"]');
    const source = slots.nth(0);
    const sourceTitle = await source.getAttribute("title");
    let targetIndex = -1;
    for (let index = 1; index < await slots.count(); index += 1) {
      if ((index % 4) === 0) { targetIndex = index; break; }
    }
    if (targetIndex > 0) {
      const target = slots.nth(targetIndex);
      const targetTitle = await target.getAttribute("title");
      await source.dispatchEvent("dragstart");
      await target.dispatchEvent("dragover");
      await target.dispatchEvent("drop");
      record("Equipment DOM-dispatched drag changes source slot", await source.getAttribute("title"), targetTitle);
      record("Equipment DOM-dispatched drag changes target slot", await target.getAttribute("title"), sourceTitle);
    }

    await page.locator('.squad-tabs [role="tab"]').nth(0).click();
    record("Equipment hidden while AFK visible", await page.locator(".equipment-preset-layout:visible").count(), 0);
    await page.locator('.squad-tabs [role="tab"]').nth(1).click();
    record("Equipment renamed moved dirty draft retained after Activity return", await page.locator(".equipment-preset-list > button.active span").innerText(), "Browser retained preset *");
    await screenshot(page, "en-light-equipment-interactions", modes[0].id, "equipment-interactions");
  } finally { await context.close(); }
}

// Equipment acknowledgement/error/disabled/result branches. No native-looking action is clicked.
for (const state of ["squads-equipment-progress", "squads-equipment-result", "squads-equipment-error", "squads-equipment-offline", "squads-equipment-rename-pending", "squads-equipment-rename-error"]) {
  const { context, page } = await openPage(browser, modes[0], state);
  try {
    record(`${state} equipment layout visible`, await page.locator(".equipment-preset-layout:visible").count(), 1);
    if (state === "squads-equipment-progress") {
      record("Equipment progress visible", await page.locator(".equipment-apply-progress").count(), 1);
      record("Equipment progress native-looking primary disabled", await page.locator(".equipment-preset-toolbar button.primary").isDisabled(), true);
    }
    if (state === "squads-equipment-result") record("Equipment result visible", await page.locator(".equipment-result").count(), 1);
    if (state === "squads-equipment-error") record("Equipment error result visible", await page.locator(".equipment-result").count(), 1);
    if (state === "squads-equipment-offline") {
      record("Equipment offline Refresh disabled", await page.locator(".squad-header button").isDisabled(), true);
      record("Equipment offline native-looking actions disabled", (await page.locator(".equipment-preset-toolbar button:disabled").count()) >= 2, true);
    }
    if (state.startsWith("squads-equipment-rename-")) {
      await page.locator(".equipment-preset-actions").first().getByRole("button").first().click();
      const dialog = page.locator("dialog.equipment-preset-dialog-backdrop");
      await dialog.locator("input").fill(state.endsWith("pending") ? "Pending browser rename" : "Error browser rename");
      await dialog.getByRole("button").filter({ hasText: /Save|保存/ }).click();
      if (state.endsWith("pending")) {
        record("Equipment pending rename dialog remains open", await dialog.getAttribute("open"), "");
        record("Equipment pending rename aria-busy", await dialog.getAttribute("aria-busy"), "true");
        record("Equipment pending rename input disabled", await dialog.locator("input").isDisabled(), true);
      } else {
        await page.waitForTimeout(30);
        const alert = dialog.locator('.automation-error[role="alert"]');
        await alert.waitFor();
        record("Equipment rename error Retry/Discard", await alert.locator("button").count(), 2);
        await alert.locator("button").nth(1).click();
        await page.waitForTimeout(30);
        record("Equipment rename Discard clears save error", await dialog.locator('.automation-error[role="alert"]').count(), 0);
      }
    }
  } finally { await context.close(); }
}

await browser.close();
const errors = report.console.filter((entry) => entry.type === "error" || entry.type === "pageerror");
assert.equal(errors.length, 0, `browser console/page errors: ${JSON.stringify(errors)}`);
assert.equal(report.screenshots.length, 11);
assert.ok(report.cases.length >= 75, `expected >=75 browser assertions, got ${report.cases.length}`);
fs.writeFileSync(path.join(here, "browser-current-results.json"), `${JSON.stringify(report, null, 2)}\n`);
console.log(JSON.stringify({ result: report.result, assertions: report.cases.length, screenshots: report.screenshots.length, consoleErrors: errors.length, browser: report.browser, dragProof: report.dragProof }, null, 2));
