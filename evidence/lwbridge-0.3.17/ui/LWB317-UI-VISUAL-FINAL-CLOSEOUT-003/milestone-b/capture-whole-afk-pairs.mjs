import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath, pathToFileURL } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const verifyOnly = process.argv.includes("--verify");
const render = JSON.parse(fs.readFileSync(path.join(here, "whole-afk-composition-results.json"), "utf8"));
const outputPath = path.join(here, "whole-afk-browser-results.json");
const screenshotDir = path.join(here, "paired-screenshots");
const hash = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
const req = createRequire("C:/Users/chimw/AppData/Local/Temp/lwbridge-playwright/package.json");
const { chromium } = req("playwright");

const selected = [
  { id: "selected-existing", viewport: { width: 1280, height: 900 }, theme: "light" },
  { id: "selected-new", viewport: { width: 1280, height: 900 }, theme: "dark" },
  { id: "discovered-squads-34", viewport: { width: 375, height: 900 }, theme: "dark" },
  { id: "runtime-translated", viewport: { width: 1280, height: 900 }, theme: "light" },
  { id: "runtime-error", viewport: { width: 375, height: 900 }, theme: "light" },
  { id: "join-members-positive", viewport: { width: 1280, height: 900 }, theme: "light" },
  { id: "potion-open", viewport: { width: 1280, height: 900 }, theme: "dark" },
  { id: "drill-waiting", viewport: { width: 375, height: 900 }, theme: "dark" },
  { id: "garrison-localized", viewport: { width: 1280, height: 900 }, theme: "light" },
  { id: "garrison-running", viewport: { width: 1280, height: 900 }, theme: "dark" },
  { id: "zombie-error", viewport: { width: 1280, height: 900 }, theme: "dark" },
  { id: "zombie-running", viewport: { width: 375, height: 900 }, theme: "dark" },
  { id: "target-discovery-loading", viewport: { width: 375, height: 900 }, theme: "light" },
  { id: "target-discovery-failed", viewport: { width: 375, height: 900 }, theme: "light" },
];
const providerFenceCases = new Set(["garrison-localized", "garrison-running"]);
for (const item of selected) assert.ok(render.cases.some((entry) => entry.id === item.id), `${item.id} render case missing`);

const report = {
  marker: "LWB317_FINAL_CLOSEOUT_WHOLE_AFK_BROWSER_OK",
  browser: "",
  cases: [],
  console: [],
};
const browser = await chromium.launch({ headless: true, executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe" });
report.browser = await browser.version();
try {
  for (const spec of selected) {
    const item = { ...spec, sides: {} };
    for (const side of ["original", "current"]) {
      const context = await browser.newContext({ viewport: spec.viewport, reducedMotion: "reduce" });
      const page = await context.newPage();
      page.on("console", (message) => {
        if (["warning", "error"].includes(message.type())) report.console.push({ id: spec.id, side, type: message.type(), text: message.text() });
      });
      page.on("pageerror", (error) => report.console.push({ id: spec.id, side, type: "pageerror", text: String(error) }));
      try {
        const htmlPath = path.join(here, "full-compositions", `${spec.id}-${side}.html`);
        await page.goto(pathToFileURL(htmlPath).href);
        await page.evaluate((theme) => { document.documentElement.dataset.theme = theme; }, spec.theme);
        await page.evaluate(() => document.fonts.ready);
        const metrics = await page.locator(".monster-afk-layout").evaluate((root) => {
          const rect = root.getBoundingClientRect();
          const toolbar = root.querySelector(":scope > .monster-afk-toolbar");
          const editor = root.querySelector(":scope > .monster-afk-editor");
          const settings = [...root.querySelectorAll(".monster-afk-toolbar-settings")];
          return {
            rect: [Math.round(rect.x), Math.round(rect.y), Math.round(rect.width), Math.round(rect.height)],
            directChildren: [...root.children].map((node) => `${node.tagName}:${node.className || ""}`),
            toolbarChildClasses: toolbar ? [...toolbar.children].map((node) => String(node.className || "")) : [],
            editorDirect: Boolean(editor),
            toolbarSettingsCount: settings.length,
            toolbarSettingsOwned: settings.every((node) => node.parentElement === toolbar || node.parentElement?.classList.contains("monster-afk-toolbar-settings")),
            profileCards: root.querySelectorAll(".monster-afk-profile-card").length,
            assignmentButtons: root.querySelectorAll(".monster-afk-editor .monster-afk-squad-toggle").length,
            runtimeOkRows: root.querySelectorAll(".monster-afk-profile-select .status-ok").length,
            compositionErrors: root.querySelectorAll(":scope > .monster-afk-error").length,
            garrisonSettings: root.querySelectorAll(".garrison-settings").length,
            zombieTables: root.querySelectorAll(".zombie-bus-assignments").length,
            garrisonRunNowDisabled: root.querySelector(".garrison-actions .primary-action")?.disabled ?? null,
            addDisabled: root.querySelector('.monster-afk-add-control > button[aria-haspopup="menu"]')?.disabled ?? null,
            horizontalOverflow: root.scrollWidth > root.clientWidth + 1,
          };
        });
        const buffer = await page.locator(".monster-afk-layout").screenshot();
        const screenshot = {
          path: `paired-screenshots/${spec.id}-${spec.theme}-${spec.viewport.width}-${side}.png`,
          sha256: hash(buffer),
          bytes: buffer.length,
        };
        let providerFenceNormalizedSha256 = null;
        if (providerFenceCases.has(spec.id)) {
          await page.locator(".garrison-actions .primary-action").evaluate((button) => { button.disabled = false; });
          providerFenceNormalizedSha256 = hash(await page.locator(".monster-afk-layout").screenshot());
        }
        if (!verifyOnly) {
          fs.mkdirSync(screenshotDir, { recursive: true });
          fs.writeFileSync(path.join(here, screenshot.path), buffer);
        }
        item.sides[side] = { metrics, screenshot, providerFenceNormalizedSha256 };
      } finally {
        await context.close();
      }
    }
    const original = item.sides.original.metrics;
    const current = item.sides.current.metrics;
    assert.equal(current.editorDirect, original.editorDirect, `${spec.id}: editor direct-child ownership differs`);
    assert.equal(current.profileCards, original.profileCards, `${spec.id}: profile card count differs`);
    assert.equal(current.assignmentButtons, original.assignmentButtons, `${spec.id}: discovered editor squad control count differs`);
    assert.equal(current.runtimeOkRows, original.runtimeOkRows, `${spec.id}: runtime status row count differs`);
    assert.equal(current.compositionErrors, original.compositionErrors, `${spec.id}: composition error count differs`);
    assert.equal(current.zombieTables, original.zombieTables, `${spec.id}: zombie table count differs`);
    assert.equal(current.addDisabled, original.addDisabled, `${spec.id}: Add disabled state differs`);
    assert.ok(original.toolbarSettingsOwned, `${spec.id}: source toolbar setting escaped toolbar`);
    assert.ok(current.toolbarSettingsOwned, `${spec.id}: current toolbar setting escaped toolbar`);
    assert.equal(original.horizontalOverflow, false, `${spec.id}: source has horizontal overflow`);
    assert.equal(current.horizontalOverflow, false, `${spec.id}: current has horizontal overflow`);
    item.measurementDifference = {
      directChildrenEqual: JSON.stringify(original.directChildren) === JSON.stringify(current.directChildren),
      toolbarChildClassesEqual: JSON.stringify(original.toolbarChildClasses) === JSON.stringify(current.toolbarChildClasses),
      rectEqual: JSON.stringify(original.rect) === JSON.stringify(current.rect),
      pngBytesEqual: item.sides.original.screenshot.sha256 === item.sides.current.screenshot.sha256,
    };
    if (providerFenceCases.has(spec.id)) {
      assert.equal(original.garrisonRunNowDisabled, false, `${spec.id}: recovered Run Now should remain available`);
      assert.equal(current.garrisonRunNowDisabled, true, `${spec.id}: current unavailable provider action must remain fenced`);
      assert.equal(item.measurementDifference.pngBytesEqual, false, `${spec.id}: the explicit provider fence should be visible`);
      assert.equal(
        item.sides.current.providerFenceNormalizedSha256,
        item.sides.original.providerFenceNormalizedSha256,
        `${spec.id}: screenshot must become exact when only the unavailable-provider disabled state is normalized`,
      );
      item.declaredDifference = "current-only unavailable-provider fence: Garrison Run Now remains disabled; normalizing only that disabled state makes the full screenshot exact";
    } else {
      assert.equal(item.measurementDifference.pngBytesEqual, true, `${spec.id}: original/current screenshot bytes differ outside a declared provider fence`);
      item.declaredDifference = null;
    }
    if (spec.id === "target-discovery-loading") assert.equal(current.addDisabled, true, "first-load target discovery must disable Add while no targets are available");
    if (spec.id === "target-discovery-failed") assert.equal(current.addDisabled, false, "failed target refresh must keep Add enabled when retained targets exist");
    report.cases.push(item);
  }
} finally {
  await browser.close();
}
assert.equal(report.console.length, 0, JSON.stringify(report.console));

if (verifyOnly) {
  const recorded = JSON.parse(fs.readFileSync(outputPath, "utf8"));
  assert.deepEqual(report, recorded, "recorded whole-AFK browser proof is stale");
} else {
  fs.writeFileSync(outputPath, `${JSON.stringify(report, null, 2)}\n`);
}
console.log(JSON.stringify({ marker: report.marker, browser: report.browser, cases: report.cases.length, consoleErrors: report.console.length, verified: verifyOnly }));
