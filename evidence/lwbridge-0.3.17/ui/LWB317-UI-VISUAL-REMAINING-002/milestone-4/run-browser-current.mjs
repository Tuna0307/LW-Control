import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import assert from "node:assert/strict";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const pw = createRequire("C:/Users/chimw/AppData/Local/Temp/lwbridge-playwright/package.json");
const { chromium } = pw("playwright");
const baseUrl = "http://127.0.0.1:4391/";
const outputDir = path.join(here, "browser-current");
fs.mkdirSync(outputDir, { recursive: true });
const sourceFiles = [
  "src/LWBridge.UI-0.3.17/src/App.jsx",
  "src/LWBridge.UI-0.3.17/src/HomePage.jsx",
  "src/LWBridge.UI-0.3.17/src/ShellPresentation.jsx",
  "src/LWBridge.UI-0.3.17/src/ProfileSidebar.jsx",
  "src/LWBridge.UI-0.3.17/src/ProfileSwitchState.jsx",
  "src/LWBridge.UI-0.3.17/src/AppExitDialog.jsx",
  "src/LWBridge.UI-0.3.17/src/sharedPageUI.jsx",
  "src/LWBridge.UI-0.3.17/src/NavIcon.jsx",
  "src/LWBridge.UI-0.3.17/src/shellState.js",
  "src/LWBridge.UI-0.3.17/src/profileConfigDraft.js",
];
const hash = (bytes) => crypto.createHash("sha256").update(bytes).digest("hex").toUpperCase();
const sourceHashes = Object.fromEntries(sourceFiles.map((relative) => [relative, hash(fs.readFileSync(path.join(repo, relative)))]));
const cases = [];
const screenshots = [];
const consoleIssues = [];
const record = (name, actual, expected) => {
  assert.deepEqual(actual, expected, name);
  cases.push({ name, actual, expected });
};

async function openPage(browser, { language = "en", theme = "light", width = 1280, height = 900, state = "", pageName = "overview" }) {
  const context = await browser.newContext({ viewport: { width, height }, locale: language, timezoneId: "Asia/Singapore", colorScheme: theme, deviceScaleFactor: 1 });
  await context.addInitScript((lang) => {
    localStorage.clear();
    localStorage.setItem("lwbridge.language", lang);
  }, language);
  const page = await context.newPage();
  page.on("console", (message) => {
    if (["warning", "error"].includes(message.type())) consoleIssues.push({ state, language, theme, type: message.type(), text: message.text() });
  });
  page.on("pageerror", (error) => consoleIssues.push({ state, language, theme, type: "pageerror", text: error.message }));
  const url = new URL(baseUrl);
  url.searchParams.set("previewPage", pageName);
  if (state) url.searchParams.set("previewState", state);
  url.searchParams.set("previewTheme", theme);
  await page.goto(url.href);
  await page.locator(".app-shell").waitFor();
  await page.waitForFunction((lang) => document.documentElement.lang === lang, language);
  return { context, page };
}

async function shot(page, name) {
  const file = `${name}.png`;
  await page.screenshot({ path: path.join(outputDir, file), fullPage: true });
  const bytes = fs.readFileSync(path.join(outputDir, file));
  screenshots.push({ file, sha256: hash(bytes), length: bytes.length });
}

const browser = await chromium.launch({ executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe", headless: true });

// Combined Home + shell ancestry in distinguishing states, desktop and narrow.
for (const mode of [
  { id: "en-light-desktop", language: "en", theme: "light", width: 1280, height: 900 },
  { id: "ja-dark-narrow", language: "ja", theme: "dark", width: 740, height: 600 },
]) {
  for (const state of ["home-checking", "home-errors-both", "home-busy-overlap", "home-recovery-failed", "home-connected", "home-running-disconnected"]) {
    const { context, page } = await openPage(browser, { ...mode, state });
    try {
      record(`${mode.id}/${state} has top bar`, await page.locator(".top-bar").count(), 1);
      record(`${mode.id}/${state} has eight nav entries`, await page.locator(".side-nav > button").count(), 8);
      record(`${mode.id}/${state} Home is under profile-view-context`, await page.locator(".profile-view-context .quick-actions-panel").count(), 1);
      record(`${mode.id}/${state} Home fixture identity`, await page.locator(".quick-actions-panel").getAttribute("data-preview-fixture"), state);
      record(`${mode.id}/${state} language settled`, await page.locator("html").getAttribute("lang"), mode.language);
      record(`${mode.id}/${state} theme settled`, await page.locator("html").getAttribute("data-theme"), mode.theme);
      if (mode.width <= 760) {
        const overflow = await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth);
        record(`${mode.id}/${state} has no document horizontal overflow`, overflow, false);
      }
      if (state === "home-errors-both") record(`${mode.id}/${state} keeps independent action error`, await page.locator(".game-root-error").count(), 1);
      if (state === "home-recovery-failed") record(`${mode.id}/${state} recovery detail visible`, await page.locator(".game-root-error").count(), 1);
      await shot(page, `${mode.id}-${state}`);
    } finally { await context.close(); }
  }
}

// Four shell flag stores fail independently above the active Home page.
{
  const { context, page } = await openPage(browser, { language: "en", theme: "light", width: 1280, height: 900, state: "shell-save-errors" });
  try {
    const alerts = page.locator(".profile-view-context > .automation-error[role='alert']");
    await page.waitForFunction(() => document.querySelectorAll(".profile-view-context > .automation-error[role='alert']").length === 4);
    record("shell flag save errors render four ordered banners", await alerts.count(), 4);
    record("shell flag errors precede Home in ancestry", await page.evaluate(() => {
      const root = document.querySelector(".profile-view-context");
      return [...root.children].slice(0, 4).every((child) => child.classList.contains("automation-error")) && root.querySelector(".quick-actions-panel") !== null;
    }), true);
    record("each shell save error exposes Retry/Discard", await alerts.locator("button").count(), 8);
    await alerts.first().locator("button").nth(1).click();
    await page.waitForFunction(() => document.querySelectorAll(".profile-view-context > .automation-error[role='alert']").length === 3);
    record("discard reconciles one shell flag store independently", await alerts.count(), 3);
    await shot(page, "en-light-shell-save-errors-after-discard");
  } finally { await context.close(); }
}

// Profile sidebar, note dialog, profile selection and switch-loading share the shell.
{
  const { context, page } = await openPage(browser, { language: "en", theme: "light", width: 1280, height: 900, state: "shell-profiles" });
  try {
    const sidebar = page.locator(".profile-list");
    record("profile list has recovered aside rail ancestor", await page.locator("aside.profile-sidebar > section.profile-list").count(), 1);
    record("profile shell starts compact", (await sidebar.getAttribute("class")).includes("collapsed"), true);
    record("profile compact list has two rows", await sidebar.locator(".profile-compact-item").count(), 2);
    await sidebar.locator(".profile-collapse").click();
    record("profile shell expands locally", (await sidebar.getAttribute("class")).includes("collapsed"), false);
    record("expanded profile list has two rows", await sidebar.locator(".profile-row").count(), 2);
    record("profile native start controls remain fenced", await sidebar.locator(".profile-run:not(:disabled)").count(), 0);
    const firstProfileRow = sidebar.locator(".profile-row").first();
    await firstProfileRow.hover();
    const noteButton = firstProfileRow.locator(".profile-note-edit");
    await noteButton.click();
    const dialog = page.locator("dialog.profile-dialog-backdrop");
    await dialog.waitFor();
    record("profile note dialog is mounted/open", await dialog.evaluate((node) => node.open), true);
    record("profile note input owns autofocus", await dialog.locator("input").evaluate((node) => document.activeElement === node), true);
    await page.keyboard.press("Escape");
    await dialog.waitFor({ state: "detached" });
    record("profile note Escape closes when idle", await page.locator("dialog.profile-dialog-backdrop").count(), 0);
    const secondProfile = sidebar.locator(".profile-item").nth(1);
    await page.evaluate(() => {
      window.__m4SawProfileSwitchLoading = false;
      window.__m4ProfileSwitchObserver?.disconnect?.();
      window.__m4ProfileSwitchObserver = new MutationObserver(() => {
        if (document.querySelector(".profile-switch-loading")) window.__m4SawProfileSwitchLoading = true;
      });
      window.__m4ProfileSwitchObserver.observe(document.body, { childList: true, subtree: true });
    });
    await secondProfile.click();
    await page.waitForFunction(() => window.__m4SawProfileSwitchLoading === true);
    record("uncached local profile selection enters recovered loading branch", await page.evaluate(() => window.__m4SawProfileSwitchLoading), true);
    await page.waitForFunction(() => !document.querySelector(".profile-switch-loading"));
    await page.evaluate(() => window.__m4ProfileSwitchObserver?.disconnect?.());
    record("uncached local profile load restores retained page after completion", await page.locator(".quick-actions-panel").count(), 1);
    record("local profile selection changes active row", await sidebar.locator(".profile-row.active").count(), 1);
    await sidebar.locator(".profile-item").nth(0).click();
    record("cached Local 1 return bypasses loading", await page.locator(".profile-switch-loading").count(), 0);
    await sidebar.locator(".profile-item").nth(1).click();
    record("cached Local 2 return bypasses loading", await page.locator(".profile-switch-loading").count(), 0);
    record("profile selection keeps Home route", await page.locator(".side-nav > button[aria-current='page']").getAttribute("class"), "active");
    await shot(page, "en-light-profiles-expanded-selected");
  } finally { await context.close(); }
}
{
  const { context, page } = await openPage(browser, { language: "ja", theme: "dark", width: 740, height: 600, state: "shell-profiles-loading" });
  try {
    record("profile loading keeps sidebar", await page.locator(".profile-list").count(), 1);
    record("profile loading replaces routed page", await page.locator(".profile-switch-loading").count(), 1);
    record("profile loading hides Home composition", await page.locator(".quick-actions-panel").count(), 0);
    record("profile loading narrow has no document overflow", await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth), false);
    await shot(page, "ja-dark-narrow-profile-switch-loading");
  } finally { await context.close(); }
}

// Exit dialog event behavior inside the actual shell.
{
  const { context, page } = await openPage(browser, { language: "en", theme: "light", width: 1280, height: 900, state: "shell-exit" });
  try {
    const dialog = page.locator("dialog.app-exit-backdrop");
    await dialog.waitFor();
    record("exit dialog idle opens in shell", await dialog.evaluate((node) => node.open), true);
    record("exit dialog is recovered sibling of app-shell", await page.locator("main.app-shell ~ dialog.app-exit-backdrop").count(), 1);
    record("exit dialog has two actions", await dialog.locator("button").count(), 2);
    await page.keyboard.press("Escape");
    await dialog.waitFor({ state: "detached" });
    record("idle exit Escape dismisses producer fixture", await page.locator("dialog.app-exit-backdrop").count(), 0);
  } finally { await context.close(); }
}
{
  const { context, page } = await openPage(browser, { language: "ja", theme: "dark", width: 740, height: 600, state: "shell-exit-busy" });
  try {
    const dialog = page.locator("dialog.app-exit-backdrop");
    await dialog.waitFor();
    record("busy exit dialog is recovered sibling of app-shell", await page.locator("main.app-shell ~ dialog.app-exit-backdrop").count(), 1);
    record("busy exit disables both actions", await dialog.locator("button:not(:disabled)").count(), 0);
    await page.keyboard.press("Escape");
    record("busy exit remains mounted after physical Escape", await page.locator("dialog.app-exit-backdrop").count(), 1);
    record("busy exit source cancel predicate keeps dialog open in headless Chrome", await dialog.evaluate((node) => node.open), true);
    await shot(page, "ja-dark-narrow-exit-busy-after-escape");
  } finally { await context.close(); }
}

// Cross-server exact event rules: Escape is inert; outside pointer-down dismisses.
{
  const { context, page } = await openPage(browser, { language: "en", theme: "light", width: 1280, height: 900 });
  try {
    await page.locator(".server-jump > button").click();
    const popover = page.locator(".server-jump-popover");
    await popover.waitFor();
    record("Cross-server opens in Home shell", await popover.count(), 1);
    record("offline Cross-server shows one source-owned block message", await popover.locator(".server-jump-error").count(), 1);
    await page.keyboard.press("Escape");
    record("Cross-server Escape is inert", await popover.count(), 1);
    await page.locator(".brand-lockup").click({ position: { x: 4, y: 4 } });
    await popover.waitFor({ state: "detached" });
    record("Cross-server outside pointer-down dismisses", await page.locator(".server-jump-popover").count(), 0);
  } finally { await context.close(); }
}

// Locale/theme changes are live shell state and settle without remounting Home.
{
  const { context, page } = await openPage(browser, { language: "en", theme: "light", width: 1280, height: 900, state: "home-checking" });
  try {
    await page.locator(".language-select select").selectOption("ja");
    await page.waitForFunction(() => document.documentElement.lang === "ja");
    record("locale changes live to Japanese", await page.locator("html").getAttribute("lang"), "ja");
    record("Home fixture survives locale change", await page.locator(".quick-actions-panel").getAttribute("data-preview-fixture"), "home-checking");
    await page.locator(".theme-toggle").click();
    await page.waitForFunction(() => document.documentElement.dataset.theme === "dark");
    await page.waitForTimeout(280);
    record("theme changes live to dark", await page.locator("html").getAttribute("data-theme"), "dark");
    record("theme transition class settles", await page.locator("html").getAttribute("class") || "", "");
    await shot(page, "ja-dark-live-locale-theme-home");
  } finally { await context.close(); }
}

// Header update presentation is source-shaped and unavailable updater action is fenced.
{
  const { context, page } = await openPage(browser, { language: "en", theme: "light", width: 1280, height: 900, state: "settings-update-downloading" });
  try {
    const update = page.locator(".top-update-button");
    record("downloading update control visible in whole shell", await update.count(), 1);
    record("downloading update progress is 42 percent", (await update.innerText()).includes("42%"), true);
    record("unavailable updater action remains fenced", await update.isDisabled(), true);
    await shot(page, "en-light-update-downloading-home");
  } finally { await context.close(); }
}

await browser.close();
assert.equal(consoleIssues.length, 0, JSON.stringify(consoleIssues, null, 2));
const result = {
  marker: "LWB317_REMAINING_M4_BROWSER_CURRENT_OK",
  cases,
  screenshots,
  consoleIssues,
  sourceFiles: sourceHashes,
  browser: { version: await pw("playwright/package.json").version },
  limits: [
    "Browser preview uses inert offline state and never invokes native/gameplay providers.",
    "Busy native dialog Escape remains open in headless Chrome under the recovered preventDefault predicate; the older IAB exact-original/current packet separately records that environment's shared DOM-close quirk.",
  ],
};
fs.writeFileSync(path.join(here, "browser-current-results.json"), JSON.stringify(result, null, 2) + "\n");
console.log(JSON.stringify({ marker: result.marker, cases: cases.length, screenshots: screenshots.length, consoleErrors: consoleIssues.length }));
