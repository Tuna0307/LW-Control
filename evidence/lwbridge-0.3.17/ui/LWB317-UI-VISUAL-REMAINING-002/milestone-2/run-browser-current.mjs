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
const baseUrl = "http://127.0.0.1:4319/";
const sha256 = (file) => crypto.createHash("sha256").update(fs.readFileSync(file)).digest("hex");

const modes = [
  { id: "en-light-desktop", language: "en", theme: "light", viewport: { width: 1440, height: 1000 } },
  { id: "ja-dark-desktop", language: "ja", theme: "dark", viewport: { width: 1440, height: 1000 } },
  { id: "en-dark-narrow", language: "en", theme: "dark", viewport: { width: 720, height: 1000 } },
  { id: "ja-light-narrow", language: "ja", theme: "light", viewport: { width: 720, height: 1000 } },
];

const report = {
  marker: "LWB317_REMAINING_M2_BROWSER_CURRENT_OK",
  cases: [], screenshots: [], console: [], browser: "",
  dragProof: "DOM_OR_KEYBOARD_HANDLER_ONLY_NOT_PHYSICAL_HTML5_POINTER_DRAG",
  current: {
    squadsPage: { path: "src/LWBridge.UI-0.3.17/src/SquadsPage.jsx", sha256: sha256(path.resolve("src/LWBridge.UI-0.3.17/src/SquadsPage.jsx")) },
    fixtures: { path: "src/LWBridge.UI-0.3.17/src/previewAfkCloseoutFixtures.js", sha256: sha256(path.resolve("src/LWBridge.UI-0.3.17/src/previewAfkCloseoutFixtures.js")) },
  },
};
function record(name, actual, expected) {
  const pass = typeof expected === "function" ? expected(actual) : Object.is(actual, expected);
  report.cases.push({ name, actual, expected: typeof expected === "function" ? "predicate" : expected, pass });
  assert.ok(pass, `${name}: actual=${JSON.stringify(actual)} expected=${JSON.stringify(expected)}`);
}
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
  report.screenshots.push({ id, mode, surface, path: file.replaceAll("\\", "/"), sha256: sha256(file) });
}

const browser = await chromium.launch({ headless: true, executablePath });
report.browser = await browser.version();

for (const mode of modes) {
  const { context, page } = await openPage(browser, mode, "squads-profile-runtime");
  try {
    record(`${mode.id} html lang`, await page.locator("html").getAttribute("lang"), mode.language);
    record(`${mode.id} five compact cards`, await page.locator(".monster-afk-compact-card:visible").count(), 5);
    record(`${mode.id} two profile cards`, await page.locator(".monster-afk-profile-card:visible").count(), 2);
    record(`${mode.id} runtime rows`, await page.locator(".monster-afk-profile-select > .status-ok, .monster-afk-profile-select > .muted").count(), 2);
    record(`${mode.id} drag SVG count`, await page.locator(".monster-afk-profile-drag svg.ui-icon:visible").count(), 2);
    record(`${mode.id} editor sibling ancestry`, await page.evaluate(() => {
      const layout = document.querySelector(".monster-afk-layout");
      const profiles = document.querySelector(".monster-afk-profiles");
      const editor = document.querySelector(".monster-afk-editor");
      return Boolean(layout && profiles && editor && profiles.parentElement === layout && editor.parentElement === layout && !profiles.contains(editor));
    }), true);
    if (mode.id.includes("narrow")) record(`${mode.id} no document overflow`, await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 2), true);
    await screenshot(page, `${mode.id}-runtime`, mode.id, "runtime-profile-composition");
  } finally { await context.close(); }
}

{
  const { context, page } = await openPage(browser, modes[0], "squads-profile-runtime-translated");
  try {
    record("Translated runtime target name preferred over fallback", (await page.locator(".monster-afk-profile-select").first().innerText()).includes("Fixture Translated Leader"), true);
  } finally { await context.close(); }
}

{
  const { context, page } = await openPage(browser, modes[0], "squads-profile-runtime-error");
  try {
    record("Runtime error uses recovered error translator", (await page.locator(".monster-afk-profile-select").first().innerText()).includes("game connection is temporarily unavailable"), true);
  } finally { await context.close(); }
}

{
  const { context, page } = await openPage(browser, modes[0], "squads-profile-potion-positive");
  try {
    record("Potion settings inside toolbar", await page.evaluate(() => Boolean(document.querySelector(".monster-afk-toolbar > section.monster-afk-toolbar-settings"))), true);
    record("Potion keeps profiles visible", await page.locator(".monster-afk-profiles:visible").count(), 1);
    record("Potion keeps sibling editor visible", await page.locator(".monster-afk-editor:visible").count(), 1);
    const stamina = page.locator('.monster-afk-toolbar > section.monster-afk-toolbar-settings input[type="number"]');
    await stamina.fill("42");
    await page.locator(".monster-afk-compact-card").nth(2).locator('button[aria-label="Settings"]').click();
    await page.locator(".monster-afk-compact-card").nth(1).locator('button[aria-label="Settings"]').click();
    record("Potion debounced draft retained across panel switch", await stamina.inputValue(), "42");
    await screenshot(page, "en-light-potion", modes[0].id, "potion");
  } finally { await context.close(); }
}

{
  const { context, page } = await openPage(browser, modes[0], "squads-profile-drill-empty");
  try {
    record("Drill empty has no premature inline validation", await page.locator(".monster-afk-toolbar .status-error").count(), 0);
    record("Drill empty has no pre-action composition error", await page.locator(".monster-afk-error").count(), 0);
    const drillCard = page.locator(".monster-afk-compact-card").nth(2);
    await drillCard.locator('input[type="checkbox"]').click({ force: true });
    record("Drill invalid enable creates final composition error", await page.locator(".monster-afk-error").count(), 1);
    record("Drill settings remain selected after invalid enable", await page.locator(".monster-afk-toolbar > section.monster-afk-toolbar-settings:visible").count(), 1);
    await screenshot(page, "en-light-drill-empty-error", modes[0].id, "drill-empty-error");
  } finally { await context.close(); }
}

{
  const { context, page } = await openPage(browser, modes[0], "squads-profile-garrison-no-targets");
  try {
    const panel = page.locator(".garrison-settings:visible");
    record("Garrison target-required nested inside priority list", await panel.locator(".garrison-priority-list > .muted").count(), 1);
    record("Garrison empty selected allies uses span", await panel.locator(".garrison-selected-allies > span.muted").count(), 1);
    record("Garrison always shows squad hint", await panel.getByText(/Garrison squads are not used|駐屯部隊/).count(), 1);
    record("Garrison recovered drag SVG present", (await panel.locator("svg.ui-icon").count()) > 0, true);
    record("Garrison settings precede Zombie compact in toolbar", await page.evaluate(() => {
      const toolbar = document.querySelector(".monster-afk-toolbar");
      const settings = toolbar?.querySelector(".garrison-settings");
      const zombie = [...(toolbar?.querySelectorAll(".monster-afk-compact-card") || [])].find((card) => card.querySelector("h3")?.textContent?.includes("Zombie"));
      return Boolean(settings && zombie && (settings.compareDocumentPosition(zombie) & Node.DOCUMENT_POSITION_FOLLOWING));
    }), true);
    await page.locator(".monster-afk-compact-card").nth(3).locator('button[aria-label="Settings"]').click();
    record("Garrison settings closed", await page.locator(".garrison-settings:visible").count(), 0);
    await page.locator(".monster-afk-compact-card").nth(3).locator('input[type="checkbox"]').click({ force: true });
    record("Invalid Garrison enable does not auto-open settings", await page.locator(".garrison-settings:visible").count(), 0);
    await screenshot(page, "en-light-garrison-no-targets", modes[0].id, "garrison-no-targets");
  } finally { await context.close(); }
}

{
  const { context, page } = await openPage(browser, modes[0], "squads-profile-garrison-running");
  try {
    const panel = page.locator(".garrison-settings:visible");
    await panel.locator(".garrison-target-panel .garrison-section-heading button").click();
    record("Garrison picker uses native dialog", await page.locator("dialog.app-dialog.garrison-modal-backdrop:visible").count(), 1);
    record("Garrison picker is modal-open", await page.locator("dialog.app-dialog.garrison-modal-backdrop").getAttribute("open"), "");
    const search = page.locator(".garrison-modal:visible input[placeholder]");
    await search.fill("10003");
    record("Garrison ally UID-only search returns no source row", await page.locator(".garrison-modal:visible .garrison-ally-list label").count(), 0);
    await search.fill("");
    record("Garrison modal availability uses em column", (await page.locator(".garrison-modal:visible .garrison-ally-list label > em").count()) > 0, true);
    await page.keyboard.press("Escape");
    record("Garrison Escape closes picker", await page.locator(".garrison-modal:visible").count(), 0);
  } finally { await context.close(); }
}

for (const state of ["squads-profile-garrison-discovery-pending", "squads-profile-garrison-discovery-failed"]) {
  const { context, page } = await openPage(browser, modes[0], state);
  try {
    const garrisonCard = page.locator(".monster-afk-compact-card").nth(3);
    record(`${state} saved-target summary fallback`, (await garrisonCard.innerText()).includes("0/1"), true);
    const panel = page.locator(".garrison-settings:visible");
    if (state.endsWith("failed")) record("Garrison discovery failure uses local inline error", await panel.locator(".inline-error").count(), 1);
    else record("Garrison discovery pending has no invented loading row", await panel.locator(".inline-error, .status-error").count(), 0);
  } finally { await context.close(); }
}

{
  const { context, page } = await openPage(browser, modes[0], "squads-profile-garrison-localized");
  try {
    record("Garrison building uses translated name plus alliance abbreviation", (await page.locator(".garrison-building").first().innerText()).includes("[FX] Fixture Translated Center"), true);
  } finally { await context.close(); }
}

{
  const { context, page } = await openPage(browser, modes[0], "squads-profile-garrison-error");
  try {
    record("Garrison status lastError is not invented into settings", await page.locator(".garrison-settings .automation-error").count(), 0);
  } finally { await context.close(); }
}

{
  const { context, page } = await openPage(browser, modes[0], "squads-profile-squads-34");
  try {
    const add = page.locator('.monster-afk-add-control > button[aria-haspopup="menu"]');
    await add.click();
    await page.locator('.monster-afk-add-menu [role="menuitem"]').first().click();
    record("New AFK profile binds first discovered squad", (await page.locator(".monster-afk-profile-select").last().innerText()).includes("Bound squads: 3"), true);
    await page.locator(".monster-afk-compact-card").nth(2).locator('button[aria-label="Settings"]').click();
    record("Drill choices use selected plus discovered squad indexes", await page.locator(".automation-squad-priority-item:visible").count(), 3);
    record("Drill discovered choices exclude squad 2", (await page.locator(".automation-squad-priority-item:visible").allInnerTexts()).some((value) => value.includes("Squad 2")), false);
  } finally { await context.close(); }
}

{
  const { context, page } = await openPage(browser, modes[0], "squads-profile-master-stop-busy");
  try {
    const master = page.locator(".monster-afk-compact-card").first();
    record("Master busy fixture starts enabled", await master.locator('input[type="checkbox"]').isChecked(), true);
    await master.locator('input[type="checkbox"]').click({ force: true });
    record("Master stop busy disables first three compact toggles", await page.locator('.monster-afk-compact-card').evaluateAll((cards) => cards.slice(0, 3).every((card) => card.querySelector('input[type="checkbox"]')?.disabled)), true);
    record("Master stop busy disables Add", await page.locator('.monster-afk-add-control > button').isDisabled(), true);
    record("Master stop busy disables profile delete", await page.locator('.monster-afk-profile-card .danger').first().isDisabled(), true);
    record("Master stop busy disables profile drag", await page.locator('.monster-afk-profile-drag').first().isDisabled(), true);
  } finally { await context.close(); }
}

{
  const { context, page } = await openPage(browser, modes[0], "squads-profile-zombie-error");
  try {
    record("Zombie open settings section", await page.locator(".monster-afk-toolbar > section.monster-afk-toolbar-settings:visible").count(), 1);
    record("Zombie open error is separate sibling", await page.locator(".monster-afk-toolbar > div.monster-afk-toolbar-settings:visible .automation-error").count(), 1);
    record("Zombie open error absent inside settings section", await page.locator(".monster-afk-toolbar > section.monster-afk-toolbar-settings .automation-error").count(), 0);
    await screenshot(page, "en-light-zombie-error", modes[0].id, "zombie-open-error");
  } finally { await context.close(); }
}

{
  const { context, page } = await openPage(browser, modes[0], "squads-profile-runtime");
  try {
    const firstName = page.locator(".monster-afk-editor:visible input").first();
    await firstName.fill("Milestone 2 retained draft");
    const drag = page.locator(".monster-afk-profile-drag").first();
    await drag.focus();
    await drag.press("ArrowDown");
    record("Profile keyboard reorder changes first profile", await page.locator(".monster-afk-profile-heading strong").first().innerText(), (value) => !value.includes("Steel"));
    const add = page.locator('.monster-afk-add-control > button[aria-haspopup="menu"]');
    await add.click();
    record("Add menu opens", await page.locator(".monster-afk-add-menu:visible").count(), 1);
    await page.locator(".monster-section-title > strong").click();
    record("Pointer outside Add control closes menu", await page.locator(".monster-afk-add-menu:visible").count(), 0);
    await page.locator('.squad-tabs [role="tab"]').nth(1).click();
    record("Equipment visible after AFK hidden", await page.locator(".equipment-preset-layout:visible").count(), 1);
    await page.locator('.squad-tabs [role="tab"]').nth(0).click();
    record("AFK editor draft retained through Activity", await page.locator(".monster-afk-editor:visible input").first().inputValue(), "Milestone 2 retained draft");
    await screenshot(page, "en-light-interactions", modes[0].id, "afk-activity-interactions");
  } finally { await context.close(); }
}

await browser.close();
const errors = report.console.filter((entry) => entry.type === "error" || entry.type === "pageerror");
assert.equal(errors.length, 0, `browser console/page errors: ${JSON.stringify(errors)}`);
assert.ok(report.cases.length >= 50, `expected >=50 assertions, got ${report.cases.length}`);
assert.equal(report.screenshots.length, 9);
fs.writeFileSync(path.join(here, "browser-current-results.json"), `${JSON.stringify(report, null, 2)}\n`);
console.log(JSON.stringify({ marker: report.marker, assertions: report.cases.length, screenshots: report.screenshots.length, consoleErrors: errors.length, browser: report.browser }, null, 2));
