import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../../");
const outputPath = path.join(here, "mounted-afk-interactions-results.json");
const screenshotDir = path.join(here, "mounted-interactions");
const verifyOnly = process.argv.includes("--verify");
const portArg = process.argv.find((arg) => arg.startsWith("--port="));
const port = Number(portArg?.slice("--port=".length) || 4451);
const baseUrl = `http://127.0.0.1:${port}/`;
const req = createRequire("C:/Users/chimw/AppData/Local/Temp/lwbridge-playwright/package.json");
const { chromium } = req("playwright");
const sha = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
const relative = (file) => path.relative(repo, file).replaceAll("\\", "/");

const report = {
  marker: "LWB317_FINAL_CLOSEOUT_MOUNTED_AFK_INTERACTIONS_OK",
  browser: "",
  assertions: [],
  screenshots: [],
  console: [],
};
function record(name, actual, expected) {
  const pass = typeof expected === "function" ? expected(actual) : Object.is(actual, expected);
  report.assertions.push({ name, actual, expected: typeof expected === "function" ? "predicate" : expected, pass });
  assert.ok(pass, `${name}: actual=${JSON.stringify(actual)} expected=${JSON.stringify(expected)}`);
}
async function openPage(browser, state) {
  const context = await browser.newContext({ viewport: { width: 1280, height: 900 }, reducedMotion: "reduce" });
  const page = await context.newPage();
  page.on("console", (message) => {
    if (["warning", "error"].includes(message.type())) report.console.push({ state, type: message.type(), text: message.text() });
  });
  page.on("pageerror", (error) => report.console.push({ state, type: "pageerror", text: String(error?.stack || error) }));
  const params = new URLSearchParams({ previewPage: "march", previewState: state, previewLanguage: "en", previewTheme: "light" });
  await page.goto(`${baseUrl}?${params}`, { waitUntil: "domcontentloaded" });
  await page.locator("section.squad-panel").waitFor();
  await page.evaluate(() => document.fonts?.ready);
  return { context, page };
}
async function capture(page, id) {
  const file = path.join(screenshotDir, `${id}.png`);
  const buffer = await page.locator("section.squad-panel").screenshot();
  if (!verifyOnly) {
    fs.mkdirSync(screenshotDir, { recursive: true });
    fs.writeFileSync(file, buffer);
  }
  report.screenshots.push({ id, path: relative(file), sha256: sha(buffer), bytes: buffer.length });
}

const browser = await chromium.launch({ headless: true, executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe" });
report.browser = await browser.version();
try {
  {
    const { context, page } = await openPage(browser, "squads-profile-members-positive");
    try {
      const choose = page.locator(".rally-join-leaders > button").first();
      await choose.focus();
      await choose.click();
      const dialog = page.locator("dialog.app-dialog.garrison-modal-backdrop");
      record("Join picker opens as native modal dialog", await dialog.getAttribute("open"), "");
      record("Join picker reports aria-busy false", await dialog.getAttribute("aria-busy"), "false");
      const search = dialog.locator("input").first();
      record("Join picker keeps focus inside native modal", await dialog.evaluate((node) => node === document.activeElement || node.contains(document.activeElement)), true);
      await search.fill("10003");
      record("Join picker UID search finds Casey", await dialog.locator(".garrison-ally-list label").count(), 1);
      record("Join picker UID result is Casey", (await dialog.locator(".garrison-ally-list label").innerText()).includes("Fixture Casey"), true);
      await dialog.locator('.garrison-ally-list input[type="checkbox"]').check();
      await dialog.locator(".garrison-modal-actions .primary-action").click();
      record("Join confirm closes modal", await dialog.isVisible(), false);
      record("Join confirm commits selected member", (await page.locator(".rally-join-leaders").innerText()).includes("Fixture Casey"), true);

      await choose.focus();
      await choose.click();
      const reopened = page.locator("dialog.app-dialog.garrison-modal-backdrop");
      await reopened.evaluate((node) => {
        node.dispatchEvent(new PointerEvent("pointerdown", { bubbles: true }));
        node.click();
      });
      record("Join modal backdrop click is inert by recovered default", await reopened.isVisible(), true);
      const last = reopened.locator(".garrison-modal-actions .primary-action");
      await last.focus();
      await page.keyboard.press("Tab");
      record("Join modal Tab wraps to first focusable control", await page.evaluate(() => document.activeElement?.textContent?.trim()), "Cancel");
      await page.keyboard.press("Escape");
      record("Join Escape closes modal through native cancel", await reopened.isVisible(), false);
      record("Join modal cleanup restores trigger focus", await page.evaluate(() => document.activeElement === document.querySelector(".rally-join-leaders > button")), true);
      await capture(page, "join-picker-confirmed");
    } finally {
      await context.close();
    }
  }

  {
    const { context, page } = await openPage(browser, "squads-profile-garrison-running");
    try {
      const panel = page.locator(".garrison-settings:visible");
      await panel.locator(".garrison-target-panel .garrison-section-heading button").click();
      const dialog = page.locator("dialog.app-dialog.garrison-modal-backdrop:visible");
      record("Garrison picker opens as native modal dialog", await dialog.count(), 1);
      const search = dialog.locator("input[placeholder]");
      await search.fill("Casey");
      record("Garrison picker name search finds Casey", await dialog.locator(".garrison-ally-list label").count(), 1);
      await dialog.locator('.garrison-ally-list input[type="checkbox"]').check();
      await dialog.locator(".garrison-modal-actions .primary-action").click();
      record("Garrison confirm closes picker", await page.locator("dialog.app-dialog.garrison-modal-backdrop:visible").count(), 0);
      record("Garrison confirm commits selected ally", (await panel.locator(".garrison-selected-allies").innerText()).includes("Fixture Casey"), true);
      await capture(page, "garrison-picker-confirmed");
    } finally {
      await context.close();
    }
  }

  for (const action of ["retry", "discard"]) {
    const { context, page } = await openPage(browser, "squads-profile-save-error");
    try {
      const masterToggle = page.locator(".monster-afk-compact-card").first().locator('input[type="checkbox"]');
      await masterToggle.click({ force: true });
      const error = page.locator('.monster-afk-layout > .automation-error[role="alert"]').first();
      await error.waitFor();
      record(`${action} fixture exposes failed store acknowledgement`, await error.isVisible(), true);
      record(`${action} keeps optimistic Master draft`, await masterToggle.isChecked(), true);
      if (action === "retry") {
        await error.getByRole("button", { name: "Retry" }).click();
        await error.waitFor({ state: "hidden" });
        record("Retry clears error after successful second acknowledgement", await page.locator('.monster-afk-layout > .automation-error[role="alert"]').count(), 0);
        record("Retry preserves acknowledged Master draft", await masterToggle.isChecked(), true);
      } else {
        await error.getByRole("button", { name: "Discard" }).click();
        await error.waitFor({ state: "hidden" });
        record("Discard clears error", await page.locator('.monster-afk-layout > .automation-error[role="alert"]').count(), 0);
        record("Discard restores confirmed Master value", await masterToggle.isChecked(), false);
      }
    } finally {
      await context.close();
    }
  }

  {
    const { context, page } = await openPage(browser, "squads-profile-runtime");
    try {
      const name = page.locator(".monster-afk-editor:visible input").first();
      await name.fill("Closeout retained AFK draft");
      await page.locator('.squad-tabs [role="tab"]').nth(1).click();
      record("Equipment mounts while AFK Activity hides", await page.locator(".equipment-preset-layout:visible").count(), 1);
      await page.locator('.squad-tabs [role="tab"]').nth(0).click();
      record("AFK draft survives Equipment round trip", await page.locator(".monster-afk-editor:visible input").first().inputValue(), "Closeout retained AFK draft");
      await capture(page, "afk-equipment-retained-draft");
    } finally {
      await context.close();
    }
  }
} finally {
  await browser.close();
}

assert.equal(report.console.length, 0, `browser console/page errors: ${JSON.stringify(report.console)}`);
assert.ok(report.assertions.length >= 20, `expected at least 20 mounted assertions, got ${report.assertions.length}`);
report.dependencies = [
  "src/LWBridge.UI-0.3.17/src/SquadsPage.jsx",
  "src/LWBridge.UI-0.3.17/src/RallyJoinSettings.jsx",
  "src/LWBridge.UI-0.3.17/src/previewConfigHook.jsx",
  "src/LWBridge.UI-0.3.17/src/previewConfig.js",
  "src/LWBridge.UI-0.3.17/src/previewAfkContracts.js",
  "src/LWBridge.UI-0.3.17/src/previewAfkCloseoutFixtures.js",
].map((name) => {
  const file = path.join(repo, name);
  return { path: name, sha256: sha(fs.readFileSync(file)) };
});
report.scriptSha256 = sha(fs.readFileSync(fileURLToPath(import.meta.url)));

if (verifyOnly) {
  assert.deepEqual(report, JSON.parse(fs.readFileSync(outputPath, "utf8")), "recorded mounted AFK interactions are stale");
} else {
  fs.writeFileSync(outputPath, `${JSON.stringify(report, null, 2)}\n`);
}
console.log(JSON.stringify({ marker: report.marker, assertions: report.assertions.length, screenshots: report.screenshots.length, consoleErrors: report.console.length, browser: report.browser, verified: verifyOnly }));
