import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const outDir = path.join(here, "enabled-class-current");
fs.mkdirSync(outDir, { recursive: true });
const baseline = JSON.parse(fs.readFileSync(path.join(here, "enabled-class-baseline.json"), "utf8"));
const recoveredCssPath = path.resolve(here, "../../frontend-package/web/assets/index-rIL9Fpht.css");
const currentCssPath = path.resolve(here, "../../../../../src/LWBridge.UI-0.3.17/src/reference.css");
const recoveredCss = fs.readFileSync(recoveredCssPath, "utf8");
const currentCss = fs.readFileSync(currentCssPath, "utf8");
const req = createRequire("C:/Users/chimw/AppData/Local/Temp/lwbridge-playwright/package.json");
const { chromium } = req("playwright");
const executablePath = "C:/Program Files/Google/Chrome/Application/chrome.exe";
const baseUrl = "http://127.0.0.1:4336/";
const sha256 = (data) => crypto.createHash("sha256").update(data).digest("hex");

const report = {
  task: "LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-c",
  result: "LWB317_VISUAL_FINAL_UNIT_C_ENABLED_CLASS_FIXED",
  css: {
    recoveredSha256: sha256(recoveredCss),
    currentSha256: sha256(currentCss),
    rule: ".automation-card.is-enabled{border-color:var(--line-bright);box-shadow:var(--shadow-card), 0 0 0 1px var(--info-line)}",
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

async function capture(card, id) {
  await card.page().mouse.move(0, 0);
  await card.page().waitForTimeout(300);
  const style = await card.evaluate((node) => {
    const computed = getComputedStyle(node);
    return { className: node.className, borderColor: computed.borderColor, boxShadow: computed.boxShadow };
  });
  assert.equal(style.className.includes("is-enabled"), true, `${id} must carry recovered enabled class`);
  const file = path.join(outDir, `${id}.png`);
  await card.screenshot({ path: file });
  const digest = sha256(fs.readFileSync(file));
  const before = baseline.cases.find((entry) => entry.id === id);
  assert.ok(before, `${id} immutable baseline missing`);
  assert.equal(before.current.className.includes("is-enabled"), false, `${id} immutable baseline must prove missing class`);

  // Mutation control: remove only the recovered enabled class.  The exact recovered
  // CSS rule must visibly change the card after transitions settle.
  await card.evaluate((node) => node.classList.remove("is-enabled"));
  await card.page().waitForTimeout(300);
  const mutationStyle = await card.evaluate((node) => {
    const computed = getComputedStyle(node);
    return { className: node.className, borderColor: computed.borderColor, boxShadow: computed.boxShadow };
  });
  const mutationFile = path.join(outDir, `${id}-without-enabled-class.png`);
  await card.screenshot({ path: mutationFile });
  assert.equal(mutationStyle.className.includes("is-enabled"), false);
  assert.notEqual(style.boxShadow, mutationStyle.boxShadow, `${id} recovered enabled class must change rendered shadow`);
  await card.evaluate((node) => node.classList.add("is-enabled"));

  report.cases.push({
    id,
    style,
    mutationStyle,
    screenshot: { path: file.replaceAll("\\", "/"), sha256: digest },
    mutationScreenshot: { path: mutationFile.replaceAll("\\", "/"), sha256: sha256(fs.readFileSync(mutationFile)) },
    immutableBaselineCurrentSha256: before.currentScreenshot.sha256,
  });
}

const browser = await chromium.launch({ headless: true, executablePath });
assert.equal(report.css.recoveredSha256, report.css.currentSha256, "production CSS must remain byte-identical to recovered CSS");
assert.equal(recoveredCss.includes(report.css.rule), true, "recovered enabled-card CSS rule missing");
try {
  {
    const { context, page } = await openPage(browser, "automation-config");
    try {
      const card = page.locator(".automation-card:visible").filter({ hasText: "Auto Training" }).first();
      const toggle = card.locator('.automation-header-switch[role="switch"]');
      await toggle.click();
      assert.equal(await toggle.getAttribute("aria-checked"), "true");
      await capture(card, "generic-auto-training");
    } finally { await context.close(); }
  }
  {
    const { context, page } = await openPage(browser, "automation-gather-runtime_wait");
    try {
      await page.locator(".automation-categories > button").nth(2).click();
      const card = page.locator(".automation-card:visible").first();
      assert.equal(await card.locator('.automation-header-switch[role="switch"]').getAttribute("aria-checked"), "true");
      await capture(card, "resource-gather");
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
      await capture(card, "trade-station");
    } finally { await context.close(); }
  }
} finally {
  await browser.close();
}

const errors = report.console.filter((entry) => entry.type === "error" || entry.type === "pageerror");
assert.equal(errors.length, 0);
fs.writeFileSync(path.join(here, "enabled-class-current.json"), `${JSON.stringify(report, null, 2)}\n`);
console.log(JSON.stringify({ result: report.result, cases: report.cases.length, consoleErrors: errors.length }, null, 2));
