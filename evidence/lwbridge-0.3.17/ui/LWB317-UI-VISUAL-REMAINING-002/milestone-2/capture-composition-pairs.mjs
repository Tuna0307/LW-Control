import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { createRequire } from "node:module";
import { fileURLToPath, pathToFileURL } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const req = createRequire("C:/Users/chimw/AppData/Local/Temp/lwbridge-playwright/package.json");
const { chromium } = req("playwright");
const render = JSON.parse(fs.readFileSync(path.join(here, "composition-pair-render.json"), "utf8"));
const outDir = path.join(here, "composition-pair-screenshots");
fs.mkdirSync(outDir, { recursive: true });
const hash = (value) => crypto.createHash("sha256").update(value).digest("hex");
const report = { marker: "LWB317_REMAINING_M2_COMPOSITION_PAIRED_BROWSER", browser: "", pairs: [], console: [] };
const browser = await chromium.launch({ headless: true, executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe" });
report.browser = await browser.version();
try {
  for (const input of render.cases) {
    const pair = { id: input.id, allowedDifference: input.allowedDifference, viewport: { width: 1280, height: 900 }, sides: {} };
    for (const side of ["original", "current"]) {
      const context = await browser.newContext({ viewport: pair.viewport, reducedMotion: "reduce" });
      const page = await context.newPage();
      page.on("console", (message) => { if (["warning", "error"].includes(message.type())) report.console.push({ id: input.id, side, type: message.type(), text: message.text() }); });
      page.on("pageerror", (error) => report.console.push({ id: input.id, side, type: "pageerror", text: String(error) }));
      try {
        await page.goto(pathToFileURL(path.join(here, "composition-pairs", `${input.id}-${side}.html`)).href);
        await page.evaluate(() => document.fonts.ready);
        const root = page.locator(".monster-afk-toolbar");
        const measured = await root.evaluate((element) => [element, ...element.querySelectorAll("*")].map((node) => {
          const rect = node.getBoundingClientRect(), style = getComputedStyle(node);
          return { tag: node.tagName, className: node.className?.baseVal ?? node.className ?? "", rect: [rect.x, rect.y, rect.width, rect.height], text: [...node.childNodes].filter((child) => child.nodeType === 3).map((child) => child.textContent).join(""), style: Object.fromEntries(["display","color","backgroundColor","fontSize","fontWeight","lineHeight","padding","margin","border","gap","opacity"].map((key) => [key, style[key]])) };
        }));
        const screenshotPath = path.join(outDir, `${input.id}-${side}.png`);
        await root.screenshot({ path: screenshotPath });
        pair.sides[side] = { measured, screenshot: { path: path.relative(here, screenshotPath).replaceAll("\\", "/"), sha256: hash(fs.readFileSync(screenshotPath)) } };
      } finally { await context.close(); }
    }
    pair.measurementDifferenceCount = pair.sides.original.measured.reduce((count, row, index) => count + (JSON.stringify(row) === JSON.stringify(pair.sides.current.measured[index]) ? 0 : 1), Math.max(0, pair.sides.current.measured.length - pair.sides.original.measured.length));
    pair.pngBytesEqual = pair.sides.original.screenshot.sha256 === pair.sides.current.screenshot.sha256;
    report.pairs.push(pair);
  }
} finally { await browser.close(); }
assert.equal(report.console.length, 0, JSON.stringify(report.console));
fs.writeFileSync(path.join(here, "composition-paired-browser.json"), `${JSON.stringify(report, null, 2)}\n`);
console.log(JSON.stringify({ marker: report.marker, browser: report.browser, pairs: report.pairs.map((pair) => ({ id: pair.id, measurementDifferenceCount: pair.measurementDifferenceCount, pngBytesEqual: pair.pngBytesEqual })), consoleErrors: report.console.length }, null, 2));
