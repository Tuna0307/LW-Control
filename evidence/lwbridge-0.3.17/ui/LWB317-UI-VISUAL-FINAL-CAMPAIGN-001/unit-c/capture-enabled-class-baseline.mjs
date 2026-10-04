import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";
import { fn, raw } from "./accepted-harness.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const outDir = path.join(here, "enabled-class-baseline");
fs.mkdirSync(outDir, { recursive: true });
const req = createRequire("C:/Users/chimw/AppData/Local/Temp/lwbridge-playwright/package.json");
const { chromium } = req("playwright");
const executablePath = "C:/Program Files/Google/Chrome/Application/chrome.exe";
const baseUrl = "http://127.0.0.1:4336/";
const sha256 = (data) => crypto.createHash("sha256").update(data).digest("hex");
const cardAsset = fs.readFileSync(path.resolve(here, "../../frontend-package/web/assets/AutomationCard-LCx_jIi7.js"), "utf8");
const cardNode = fn(cardAsset, "c");
const recoveredCardSource = raw(cardAsset, cardNode);

const report = {
  task: "LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-c",
  result: "EXPECTED_FAIL_SOURCE_ENABLED_CLASS_MISSING",
  recovered: {
    asset: "AutomationCard-LCx_jIi7.js",
    utf8ByteOffset: Buffer.byteLength(cardAsset.slice(0, cardNode.start)),
    utf8ByteLength: Buffer.byteLength(recoveredCardSource),
    sha256: sha256(recoveredCardSource),
    expression: "className:`automation-card${i?` is-enabled`:``}`",
  },
  cases: [],
  console: [],
};

async function openPage(browser, state) {
  const context = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
  const page = await context.newPage();
  page.on("console", (message) => report.console.push({ state, type: message.type(), text: message.text() }));
  page.on("pageerror", (error) => report.console.push({ state, type: "pageerror", text: String(error?.stack || error) }));
  const params = new URLSearchParams({ previewPage: "automation", previewState: state, previewLanguage: "en", previewTheme: "light" });
  await page.goto(`${baseUrl}?${params}`, { waitUntil: "networkidle" });
  await page.locator("section.panel .automation-categories").waitFor();
  await page.evaluate(() => document.fonts?.ready);
  return { context, page };
}

async function snapshotPair(page, card, id) {
  const readStyle = () => card.evaluate((node) => {
    const style = getComputedStyle(node);
    return { className: node.className, borderColor: style.borderColor, boxShadow: style.boxShadow };
  });
  const current = await readStyle();
  const currentFile = path.join(outDir, `${id}-current.png`);
  await card.screenshot({ path: currentFile });
  await card.evaluate((node) => node.classList.add("is-enabled"));
  const sourceClassControl = await readStyle();
  const controlFile = path.join(outDir, `${id}-source-class-control.png`);
  await card.screenshot({ path: controlFile });
  assert.equal(current.className.includes("is-enabled"), false, `${id} baseline must demonstrate the missing source class`);
  assert.equal(sourceClassControl.className.includes("is-enabled"), true);
  assert.notEqual(current.boxShadow, sourceClassControl.boxShadow, `${id} source class must change rendered shadow`);
  report.cases.push({
    id,
    expectedEnabledClass: true,
    current,
    sourceClassControl,
    currentScreenshot: { path: currentFile.replaceAll("\\", "/"), sha256: sha256(fs.readFileSync(currentFile)) },
    sourceClassControlScreenshot: { path: controlFile.replaceAll("\\", "/"), sha256: sha256(fs.readFileSync(controlFile)) },
  });
}

const browser = await chromium.launch({ headless: true, executablePath });
try {
  {
    const { context, page } = await openPage(browser, "automation-config");
    try {
      const card = page.locator(".automation-card:visible").filter({ hasText: "Auto Training" }).first();
      const toggle = card.locator('.automation-header-switch[role="switch"]');
      await toggle.click();
      assert.equal(await toggle.getAttribute("aria-checked"), "true");
      await snapshotPair(page, card, "generic-auto-training");
    } finally { await context.close(); }
  }
  {
    const { context, page } = await openPage(browser, "automation-gather-runtime_wait");
    try {
      await page.locator(".automation-categories > button").nth(2).click();
      const card = page.locator(".automation-card:visible").first();
      assert.equal(await card.locator('.automation-header-switch[role="switch"]').getAttribute("aria-checked"), "true");
      await snapshotPair(page, card, "resource-gather");
    } finally { await context.close(); }
  }
  {
    const { context, page } = await openPage(browser, "automation-trade-positive");
    try {
      await page.locator(".automation-categories > button").nth(5).click();
      const card = page.locator(".automation-card:visible").first();
      const toggle = card.locator('.automation-header-switch[role="switch"]');
      await toggle.click();
      assert.equal(await toggle.getAttribute("aria-checked"), "true");
      await snapshotPair(page, card, "trade-station");
    } finally { await context.close(); }
  }
} finally {
  await browser.close();
}

const errors = report.console.filter((entry) => entry.type === "error" || entry.type === "pageerror");
assert.equal(errors.length, 0);
fs.writeFileSync(path.join(here, "enabled-class-baseline.json"), `${JSON.stringify(report, null, 2)}\n`);
console.log(JSON.stringify({ result: report.result, cases: report.cases.length, consoleErrors: errors.length }, null, 2));
