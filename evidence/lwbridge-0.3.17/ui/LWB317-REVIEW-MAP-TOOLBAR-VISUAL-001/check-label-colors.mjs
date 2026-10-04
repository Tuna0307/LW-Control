import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath, pathToFileURL } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../..");
const manifestFile = path.join(repo, "evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-TOOLBAR-VISUAL-001/milestone-c/browser-pairs.json");
const manifest = JSON.parse(fs.readFileSync(manifestFile, "utf8"));
const pair = manifest.pairs.find((item) => item.id === "treasure-en-light-desktop");
const require = createRequire("C:/Users/chimw/AppData/Local/Temp/lwbridge-playwright/package.json");
const { chromium } = require("playwright");
const browser = await chromium.launch({ headless: true, executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe" });
const records = [];
try {
  const context = await browser.newContext({ viewport: { width: pair.width, height: pair.height }, timezoneId: "Asia/Singapore", deviceScaleFactor: 1 });
  try {
    for (const side of ["original", "current"]) {
      const pin = pair.generatedFiles[side];
      const file = path.join(repo, pin.path);
      assert.equal(crypto.createHash("sha256").update(fs.readFileSync(file)).digest("hex"), pin.sha256);
      const page = await context.newPage();
      await page.goto(pathToFileURL(file).href);
      await page.evaluate(() => document.fonts.ready);
      const labels = await page.locator(".map-searchbar > label.map-filter-field").evaluateAll((elements) => elements.map((label) => {
        const textNode = [...label.childNodes].find((node) => node.nodeType === Node.TEXT_NODE && node.textContent.trim()) || label.querySelector("span")?.firstChild;
        if (!textNode) throw new Error("No label text node");
        const range = document.createRange();
        range.selectNodeContents(textNode);
        const rect = range.getBoundingClientRect();
        return { text: textNode.textContent, parentTag: textNode.parentElement.tagName, color: getComputedStyle(textNode.parentElement).color, fontSize: getComputedStyle(textNode.parentElement).fontSize, rect: { x: rect.x, y: rect.y, width: rect.width, height: rect.height } };
      }));
      assert.equal(labels.length, 2);
      records.push({ side, labels });
      await page.close();
    }
  } finally { await context.close(); }
} finally { await browser.close(); }
const [original, current] = records;
for (let index = 0; index < 2; index++) {
  assert.equal(original.labels[index].text, current.labels[index].text);
  assert.deepEqual(original.labels[index].rect, current.labels[index].rect);
  assert.equal(original.labels[index].color, "rgb(29, 29, 31)");
  assert.equal(current.labels[index].color, "rgb(95, 95, 99)");
}
const report = { marker: "LWB317_LEAD_TREASURE_LABEL_COLOR_COUNTEREXAMPLE", manifestSha256: crypto.createHash("sha256").update(fs.readFileSync(manifestFile)).digest("hex"), records, acceptance: "CHANGES_REQUIRED: equal text rectangles but unequal source/current text colors" };
if (process.argv.includes("--record")) fs.writeFileSync(path.join(here, "label-color-results.json"), JSON.stringify(report, null, 2) + "\n");
console.log(JSON.stringify(report, null, 2));
