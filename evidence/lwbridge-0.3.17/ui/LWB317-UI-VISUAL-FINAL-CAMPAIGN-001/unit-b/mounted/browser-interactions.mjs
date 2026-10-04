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

const results = {
  task: "LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-b",
  marker: "LWB317_VISUAL_FINAL_UNIT_B_MOUNTED_INTERACTIONS_OK",
  browser: {}, cases: [], screenshots: [], console: [],
};
const browser = await chromium.launch({ headless: true, executablePath: chromePath });
results.browser.version = await browser.version();
results.browser.executablePath = chromePath;

function record(name, actual, expected = undefined) {
  if (expected !== undefined) assert.deepEqual(actual, expected, name);
  results.cases.push({ name, actual, ...(expected !== undefined ? { expected } : {}), pass: true });
}

async function open({ language, theme, state, viewport }) {
  const context = await browser.newContext({ viewport });
  const page = await context.newPage();
  page.on("console", (message) => results.console.push({ language, theme, state, type: message.type(), text: message.text() }));
  page.on("pageerror", (error) => results.console.push({ language, theme, state, type: "pageerror", text: String(error?.stack || error) }));
  const params = new URLSearchParams({ previewPage: "map-data", previewState: state, previewLanguage: language, previewTheme: theme });
  await page.goto(`${baseUrl}?${params}`, { waitUntil: "networkidle" });
  await page.locator(`.map-panel[data-preview-fixture="${state}"]`).waitFor();
  await page.waitForFunction((expectedLanguage) => document.documentElement.lang === expectedLanguage, language);
  await page.evaluate(() => document.fonts?.ready);
  return { context, page };
}

async function capture(page, name, viewport) {
  const file = path.join(screenshots, `${name}.png`);
  await page.locator(".map-panel").screenshot({ path: file });
  results.screenshots.push({ name, path: file.replaceAll("\\", "/"), sha256: hashFile(file), viewport });
}

// EN/light conditional fixture: actual local tab/scan-mode handlers and full Scheduled presentation.
{
  const viewport = { width: 1440, height: 1200 };
  const { context, page } = await open({ language: "en", theme: "light", state: "map-scheduled-conditional", viewport });
  try {
    record("EN/light theme", await page.locator("html").getAttribute("data-theme"), "light");
    const tabs = page.locator(".map-tabs > button");
    record("all nine Map data tabs mounted", await tabs.count(), 9);
    record("Scheduled tab initially active", await tabs.nth(8).getAttribute("class"), "active");
    record("three Scheduled groups mounted", await page.locator(".map-scheduled-group").count(), 3);
    record("three Scheduled group tables mounted", await page.locator(".map-scheduled-group table").count(), 3);
    record("conditional fixture has rows", await page.locator(".map-scheduled-group tbody tr").count() > 20, true);
    record("offline fixture fences Start Scan", await page.locator(".map-actions > button").first().isDisabled(), true);

    // Exercise every recovered top-tab local handler without invoking any row/native action.
    for (let index = 0; index < 9; index += 1) {
      await tabs.nth(index).click();
      await page.waitForFunction((target) => document.querySelectorAll(".map-tabs > button")[target]?.classList.contains("active"), index);
      record(`Map top tab ${index + 1} activates`, await tabs.nth(index).getAttribute("aria-selected"), "true");
    }
    await tabs.nth(8).click();
    await page.locator(".map-scheduled-group").first().waitFor();

    const scanTabs = page.locator(".map-scan-tabs > button");
    await scanTabs.nth(1).click();
    record("Auto Scan local tab opens card", await page.locator(".map-auto-scan-card").count(), 1);
    await scanTabs.nth(0).click();
    record("Manual Scan local tab restores actions", await page.locator(".map-actions > button").count(), 3);
    await capture(page, "vite-en-light-scheduled-conditional", viewport);
  } finally {
    await context.close();
  }
}

// JA/dark populated fixture: locale/theme and full populated Scheduled composition.
{
  const viewport = { width: 1180, height: 1100 };
  const { context, page } = await open({ language: "ja", theme: "dark", state: "map-scheduled-populated", viewport });
  try {
    record("JA/dark theme", await page.locator("html").getAttribute("data-theme"), "dark");
    record("JA locale applied", await page.locator("html").getAttribute("lang"), "ja");
    record("JA Scheduled tab active", await page.locator(".map-tabs > button").nth(8).getAttribute("aria-selected"), "true");
    record("JA populated has all Scheduled groups", await page.locator(".map-scheduled-group").count(), 3);
    record("JA populated fixture has rows", await page.locator(".map-scheduled-group tbody tr").count() > 10, true);
    record("Scheduled result count is nonzero", /[1-9]/.test(await page.locator(".map-result-count").innerText()), true);
    await capture(page, "vite-ja-dark-scheduled-populated", viewport);
  } finally {
    await context.close();
  }
}

await browser.close();
const errors = results.console.filter((entry) => entry.type === "error" || entry.type === "pageerror");
record("browser console/page errors", errors.length, 0);
fs.writeFileSync(path.join(here, "browser-interactions.json"), `${JSON.stringify(results, null, 2)}\n`);
console.log(JSON.stringify({ marker: results.marker, cases: results.cases.length, screenshots: results.screenshots.length, consoleErrors: errors.length, browser: results.browser.version }, null, 2));
