import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const screenshots = path.join(here, "screenshots");
fs.mkdirSync(screenshots, { recursive: true });
const playwrightRequire = createRequire("C:/Users/chimw/AppData/Local/Temp/lwbridge-playwright/package.json");
const { chromium } = playwrightRequire("playwright");
const chromePath = "C:/Program Files/Google/Chrome/Application/chrome.exe";
const baseUrl = "http://127.0.0.1:4336/";

const hashFile = (file) => crypto.createHash("sha256").update(fs.readFileSync(file)).digest("hex");
const results = { task: "LWB317-UI-MAP-TOOLBAR-VISUAL-001", marker: "LWB317_MAP_TOOLBAR_BROWSER_INTERACTIONS_OK", browser: {}, cases: [], screenshots: [], console: [] };
const browser = await chromium.launch({ headless: true, executablePath: chromePath });
results.browser.version = await browser.version();
results.browser.executablePath = chromePath;

function record(name, actual, expected = undefined) {
  if (expected !== undefined) assert.deepEqual(actual, expected, name);
  results.cases.push({ name, actual, ...(expected !== undefined ? { expected } : {}), pass: true });
}

async function mountedPage({ language, theme, state, viewport }) {
  const context = await browser.newContext({ viewport });
  const page = await context.newPage();
  page.on("console", (message) => results.console.push({ language, theme, state, type: message.type(), text: message.text() }));
  page.on("pageerror", (error) => results.console.push({ language, theme, state, type: "pageerror", text: String(error?.stack || error) }));
  const params = new URLSearchParams({ previewPage: "map-data", previewState: state, previewLanguage: language, previewTheme: theme });
  await page.goto(`${baseUrl}?${params}`, { waitUntil: "networkidle" });
  await page.locator(`.map-panel[data-preview-fixture="${state}"]`).waitFor();
  await page.waitForFunction(() => /\d/.test(document.querySelector(".map-result-count")?.textContent || ""));
  await page.evaluate(() => document.fonts?.ready);
  return { context, page };
}

async function tab(page, index) {
  const buttons = page.locator(".map-tabs > button");
  await buttons.nth(index).click();
  await page.waitForFunction((target) => document.querySelectorAll(".map-tabs > button")[target]?.classList.contains("active"), index);
  await page.waitForTimeout(20);
}

async function resultText(page) {
  return (await page.locator(".map-result-count").innerText()).trim();
}

async function search(page) {
  await page.locator(".map-searchbar > button").filter({ hasText: /Search|検索/ }).first().click();
  await page.waitForTimeout(25);
}

async function selectFirstNonempty(select) {
  const value = await select.locator("option").evaluateAll((options) => options.map((option) => option.value).find((entry) => entry !== "" && entry !== "all") || "");
  assert.ok(value, "select has a non-empty option");
  await select.selectOption(value);
  return value;
}

// EN/light: actual mounted clone with inert map-filter-lifecycle provider.
{
  const { context, page } = await mountedPage({ language: "en", theme: "light", state: "map-filter-lifecycle", viewport: { width: 1440, height: 1000 } });
  try {
    record("EN/light theme", await page.locator("html").getAttribute("data-theme"), "light");
    record("recovered map-search owns table", await page.locator(".map-search > .map-table-scroll").count(), 1);
    record("recovered map-search owns pagination", await page.locator(".map-search > .map-pagination").count(), 1);
    record("initial city total", await resultText(page), "55 items");

    const citySearch = page.locator(".map-searchbar > input").first();
    const cityAlliance = page.locator(".map-searchbar select").first();
    const cityMarked = page.locator(".map-searchbar input[type=checkbox]").first();
    const uriAllianceValue = await cityAlliance.locator("option").evaluateAll((options) => options.map((option) => ({ value: option.value, text: option.textContent })).find((option) => option.text?.includes("A/B"))?.value || "");
    assert.ok(uriAllianceValue.startsWith("name:"), "encoded URI alliance option is available");
    await cityAlliance.selectOption(uriAllianceValue);
    record("encoded City alliance select value", await cityAlliance.inputValue(), uriAllianceValue);
    await cityMarked.check();
    record("City marked checkbox true", await cityMarked.isChecked(), true);
    await cityMarked.uncheck();
    await citySearch.fill("URI Alliance Commander");
    await search(page);
    record("City inert Search applies keyword/alliance", await resultText(page), "1 items");

    // Resource: selecting a name clears keyword; typing keyword clears the selected name.
    await tab(page, 1);
    const resourceSearch = page.locator(".map-searchbar > input").first();
    const resourceName = page.locator(".map-searchbar select").first();
    await resourceSearch.fill("fixture-before-name");
    const resourceValue = await selectFirstNonempty(resourceName);
    record("Resource name select clears keyword", await resourceSearch.inputValue(), "");
    record("Resource name selection retained", await resourceName.inputValue(), resourceValue);
    await resourceSearch.fill("Fixture Resource");
    record("Resource keyword clears name", await resourceName.inputValue(), "");
    await resourceSearch.fill("");

    // Truck retained-goods details: browser-native open, production select callback, production close.
    await tab(page, 3);
    const truckDetails = page.locator(".map-searchbar details.map-item-filter");
    await truckDetails.locator("summary").click();
    record("Truck retained-goods dropdown opens", await truckDetails.evaluate((element) => element.open), true);
    const truckChoice = truckDetails.locator(".map-item-filter-menu button").nth(1);
    const truckChoiceText = (await truckChoice.innerText()).trim();
    await truckChoice.click();
    record("Truck retained-goods selection closes dropdown", await truckDetails.evaluate((element) => element.open), false);
    assert.ok((await truckDetails.locator("summary").innerText()).includes(truckChoiceText), "Truck summary shows selected item");
    record("Truck retained-goods selected label", truckChoiceText);

    // Treasure dropdown and preference checkboxes use their actual mounted component/handlers.
    await tab(page, 7);
    const treasureDetails = page.locator(".map-searchbar details.map-item-filter");
    await treasureDetails.locator("summary").click();
    record("Treasure type dropdown opens", await treasureDetails.evaluate((element) => element.open), true);
    const treasureChoice = treasureDetails.locator(".map-item-filter-menu button").nth(1);
    const treasureChoiceText = (await treasureChoice.innerText()).trim();
    await treasureChoice.click();
    record("Treasure type selection closes dropdown", await treasureDetails.evaluate((element) => element.open), false);
    const treasureSummaryText = (await treasureDetails.locator("summary").innerText()).trim();
    const treasureName = treasureChoiceText.replace(/\s*\(\d+\)\s*$/, "");
    assert.ok(treasureSummaryText.includes(treasureName), "Treasure summary shows selected type");
    record("Treasure selected option active", await treasureChoice.getAttribute("class"), "active");
    const treasureChecks = page.locator(".map-searchbar input[type=checkbox]");
    await treasureChecks.nth(0).check();
    await treasureChecks.nth(1).uncheck();
    record("Treasure foreign checkbox true", await treasureChecks.nth(0).isChecked(), true);
    record("Treasure lucky checkbox false", await treasureChecks.nth(1).isChecked(), false);

    // Dispatch select/checkbox/delay inputs through actual local handlers only.
    await tab(page, 5);
    const dispatchSelects = page.locator(".map-searchbar select");
    await dispatchSelects.nth(0).selectOption("completed");
    const level = await selectFirstNonempty(dispatchSelects.nth(1));
    await dispatchSelects.nth(2).selectOption("sr");
    const dispatchPlunderable = page.locator(".map-searchbar .map-plunderable-filter input[type=checkbox]");
    await dispatchPlunderable.check();
    const delay = page.locator(".map-random-delay-field input[type=number]");
    await delay.fill("17");
    record("Dispatch status select", await dispatchSelects.nth(0).inputValue(), "completed");
    record("Dispatch level select", await dispatchSelects.nth(1).inputValue(), level);
    record("Dispatch quality select", await dispatchSelects.nth(2).inputValue(), "sr");
    record("Dispatch plunderable checkbox", await dispatchPlunderable.isChecked(), true);
    record("Dispatch random delay", await delay.inputValue(), "17");

    // City pagination: first/last disabled boundaries, tab cache, Search reset to page 1.
    await tab(page, 0);
    await page.locator(".map-searchbar > input").first().fill("");
    await page.locator(".map-searchbar select").first().selectOption("all");
    const marked = page.locator(".map-searchbar input[type=checkbox]").first();
    if (await marked.isChecked()) await marked.uncheck();
    await search(page);
    await page.waitForFunction(() => document.querySelector(".map-pagination span")?.textContent?.includes("Page 1 of 2"));
    const previous = page.locator(".map-pagination button").first();
    const next = page.locator(".map-pagination button").last();
    record("pagination first Previous disabled", await previous.isDisabled(), true);
    record("pagination first Next enabled", await next.isEnabled(), true);
    await next.click();
    await page.waitForFunction(() => document.querySelector(".map-pagination span")?.textContent?.includes("Page 2 of 2"));
    record("pagination last Previous enabled", await previous.isEnabled(), true);
    record("pagination last Next disabled", await next.isDisabled(), true);
    await tab(page, 3);
    await tab(page, 0);
    record("City page retained across tab cache", (await page.locator(".map-pagination span").innerText()).trim(), "Page 2 of 2");
    await page.locator(".map-searchbar > input").first().fill("Fixture");
    await search(page);
    await page.waitForFunction(() => document.querySelector(".map-pagination span")?.textContent?.startsWith("Page 1 of"));
    record("Search resets non-first page", (await page.locator(".map-pagination span").innerText()).trim().startsWith("Page 1 of"), true);

    const shot = path.join(screenshots, "vite-en-light-interactions.png");
    await page.locator(".map-panel").screenshot({ path: shot });
    results.screenshots.push({ name: "vite-en-light-interactions", path: shot.replaceAll("\\", "/"), sha256: hashFile(shot), viewport: { width: 1440, height: 1000 } });
  } finally {
    await context.close();
  }
}

// JA/dark: second required local QA mode, with actual dropdown interaction.
{
  const { context, page } = await mountedPage({ language: "ja", theme: "dark", state: "map-truck", viewport: { width: 1100, height: 900 } });
  try {
    record("JA/dark theme", await page.locator("html").getAttribute("data-theme"), "dark");
    const labels = await page.locator(".map-tab-label").allInnerTexts();
    record("JA locale applied", labels.includes("輸送車"), true);
    const details = page.locator(".map-searchbar details.map-item-filter");
    await details.locator("summary").click();
    record("JA retained-goods dropdown opens", await details.evaluate((element) => element.open), true);
    await details.locator(".map-item-filter-menu button").nth(1).click();
    record("JA retained-goods dropdown closes after select", await details.evaluate((element) => element.open), false);
    const shot = path.join(screenshots, "vite-ja-dark-interactions.png");
    await page.locator(".map-panel").screenshot({ path: shot });
    results.screenshots.push({ name: "vite-ja-dark-interactions", path: shot.replaceAll("\\", "/"), sha256: hashFile(shot), viewport: { width: 1100, height: 900 } });
  } finally {
    await context.close();
  }
}

await browser.close();
const errorConsole = results.console.filter((entry) => entry.type === "error" || entry.type === "pageerror");
record("browser console/page errors", errorConsole.length, 0);
fs.writeFileSync(path.join(here, "browser-interactions.json"), `${JSON.stringify(results, null, 2)}\n`);
console.log(JSON.stringify({ marker: results.marker, cases: results.cases.length, screenshots: results.screenshots.length, consoleErrors: errorConsole.length, browser: results.browser.version }, null, 2));
