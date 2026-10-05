import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import assert from "node:assert/strict";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";
import { collectAppDependencyClosure } from "./collect-app-dependency-closure.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const pw = createRequire("C:/Users/chimw/AppData/Local/Temp/lwbridge-playwright/package.json");
const { chromium } = pw("playwright");
const port = Number(process.argv[2] || 4391);
const baseUrl = `http://127.0.0.1:${port}/`;
const captureDir = path.join(here, "browser-current");
fs.mkdirSync(captureDir, { recursive: true });

const hash = (bytes) => crypto.createHash("sha256").update(bytes).digest("hex").toUpperCase();
const sourceFiles = collectAppDependencyClosure(repo);
const sourceHashes = Object.fromEntries(sourceFiles.map((relative) => [relative, hash(fs.readFileSync(path.join(repo, relative)))]));
const assertions = [];
const screenshots = [];
const consoleIssues = [];
const check = (name, actual, expected) => {
  assert.deepEqual(actual, expected, name);
  assertions.push({ name, actual, expected });
};

async function openPage(browser, { language = "en", theme = "light", width = 1280, height = 900, state = "", route = "overview" } = {}) {
  const context = await browser.newContext({ viewport: { width, height }, locale: language, timezoneId: "Asia/Singapore", colorScheme: theme, deviceScaleFactor: 1 });
  await context.addInitScript((lang) => {
    localStorage.clear();
    localStorage.setItem("lwbridge.language", lang);
    const activeWindowListeners = new Map();
    const originalAdd = window.addEventListener.bind(window);
    const originalRemove = window.removeEventListener.bind(window);
    window.addEventListener = (type, listener, options) => {
      if (typeof listener === "function" || (listener && typeof listener.handleEvent === "function")) {
        const entries = activeWindowListeners.get(type) || new Set();
        entries.add(listener);
        activeWindowListeners.set(type, entries);
      }
      return originalAdd(type, listener, options);
    };
    window.removeEventListener = (type, listener, options) => {
      activeWindowListeners.get(type)?.delete(listener);
      return originalRemove(type, listener, options);
    };
    window.__m5WindowListenerCount = (type) => activeWindowListeners.get(type)?.size || 0;
  }, language);
  const page = await context.newPage();
  page.on("console", (message) => {
    if (["warning", "error"].includes(message.type())) consoleIssues.push({ language, theme, state, type: message.type(), text: message.text() });
  });
  page.on("pageerror", (error) => consoleIssues.push({ language, theme, state, type: "pageerror", text: error.message }));
  const url = new URL(baseUrl);
  url.searchParams.set("previewPage", route);
  url.searchParams.set("previewTheme", theme);
  if (state) url.searchParams.set("previewState", state);
  await page.goto(url.href);
  await page.locator("main.app-shell").waitFor();
  await page.waitForFunction((lang) => document.documentElement.lang === lang, language);
  await page.evaluate(() => document.fonts.ready);
  return { context, page };
}

async function capture(page, name) {
  const file = `${name}.png`;
  const full = path.join(captureDir, file);
  await page.screenshot({ path: full, fullPage: true });
  const bytes = fs.readFileSync(full);
  screenshots.push({ file, sha256: hash(bytes), bytes: bytes.length });
}

const routes = [
  ["overview", ".quick-actions-panel"],
  ["automation", ".automation-categories"],
  ["map-data", ".map-panel"],
  ["march", ".squad-panel"],
  ["city-layout", ".city-layout-empty, .city-layout-panel"],
  ["hotkeys", ".hotkey-panel[data-hotkey-category='hotkeys']"],
  ["mini-games", ".hotkey-panel[data-hotkey-category='miniGames']"],
  ["settings", ".settings-panel"],
];

async function selectRoute(page, index, selector) {
  await page.locator(".side-nav > button").nth(index).click();
  await page.locator(selector).filter({ visible: true }).first().waitFor();
}

const browser = await chromium.launch({ executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe", headless: true });
try {
  // Complete source-order route traversal + reverse return in all required locale/theme/viewport modes.
  for (const mode of [
    { id: "en-light-desktop", language: "en", theme: "light", width: 1280, height: 900 },
    { id: "ja-dark-desktop", language: "ja", theme: "dark", width: 1280, height: 900 },
    { id: "en-dark-narrow", language: "en", theme: "dark", width: 375, height: 1000 },
    { id: "ja-light-narrow", language: "ja", theme: "light", width: 375, height: 1000 },
  ]) {
    const { context, page } = await openPage(browser, mode);
    try {
      check(`${mode.id} has exact eight source-order navigation buttons`, await page.locator(".side-nav > button").count(), 8);
      const ids = new Map();
      for (let index = 0; index < routes.length; index++) {
        const [route, selector] = routes[index];
        await selectRoute(page, index, selector);
        const panel = page.locator(selector).filter({ visible: true }).first();
        const token = `m5-${mode.id}-${route}`;
        await panel.evaluate((node, value) => node.setAttribute("data-m5-retained-id", value), token);
        ids.set(route, token);
        check(`${mode.id}/${route} selected nav owns aria-current`, await page.locator(".side-nav > button").nth(index).getAttribute("aria-current"), "page");
        check(`${mode.id}/${route} language stays settled`, await page.locator("html").getAttribute("lang"), mode.language);
        check(`${mode.id}/${route} theme stays settled`, await page.locator("html").getAttribute("data-theme"), mode.theme);
      }
      for (let index = routes.length - 1; index >= 0; index--) {
        const [route, selector] = routes[index];
        await selectRoute(page, index, selector);
        check(`${mode.id}/${route} return preserves retained DOM identity`, await page.locator(`[data-m5-retained-id='${ids.get(route)}']`).count(), 1);
      }
      check(`${mode.id} owns one retained Map page`, await page.locator(".map-panel").count(), 1);
      check(`${mode.id} owns one retained Automation category strip`, await page.locator(".automation-categories").count(), 1);
      check(`${mode.id} owns one retained Squads page`, await page.locator(".squad-panel").count(), 1);
      check(`${mode.id} owns one retained Settings page`, await page.locator(".settings-panel").count(), 1);
      check(`${mode.id} owns one retained Hotkeys page`, await page.locator(".hotkey-panel[data-hotkey-category='hotkeys']").count(), 1);
      check(`${mode.id} owns one retained Mini Games page`, await page.locator(".hotkey-panel[data-hotkey-category='miniGames']").count(), 1);
      if (mode.width <= 760) check(`${mode.id} has no document horizontal overflow`, await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth), false);
      await capture(page, `${mode.id}-eight-route-return`);
    } finally { await context.close(); }
  }

  // Parent-owned Automation/Map/Squads selection survives route returns and profile replacement;
  // page-local Map keyword resets at the selected-profile boundary.
  {
    const { context, page } = await openPage(browser, { language: "en", theme: "light", width: 1280, height: 900, state: "shell-profiles" });
    try {
      await selectRoute(page, 1, ".automation-categories");
      await page.locator(".automation-categories > button").nth(1).click();
      check("Automation parent category changes locally", await page.locator(".automation-categories > button[aria-selected='true']").count(), 1);
      const automationLabel = await page.locator(".automation-categories > button[aria-selected='true']").innerText();

      await selectRoute(page, 2, ".map-panel");
      await page.locator(".map-tabs > button").nth(3).click();
      check("Map parent tab changes to fourth data tab", await page.locator(".map-tabs > button[aria-selected='true']").evaluate((node) => [...node.parentElement.children].indexOf(node)), 3);
      const search = page.locator(".map-searchbar input").first();
      await search.fill("retained-map-keyword");
      await page.locator(".map-searchbar select").filter({ visible: true }).first().selectOption("ssr");
      await page.locator(".map-plunderable-filter input[type='checkbox']").check();
      await page.locator(".map-scan-tabs > button").nth(1).click();
      await page.locator(".map-auto-scan-grid input[type='number']").fill("75");
      check("Map profile-boundary precondition owns Auto local mode", await page.locator(".map-scan-tabs > button[aria-selected='true']").evaluate((node) => [...node.parentElement.children].indexOf(node)), 1);
      check("Map profile-boundary precondition owns non-default quality filter", await page.locator(".map-searchbar select").filter({ visible: true }).first().inputValue(), "ssr");
      check("Map profile-boundary precondition owns plunderable filter", await page.locator(".map-plunderable-filter input[type='checkbox']").isChecked(), true);
      check("Map profile-boundary precondition owns profile-scoped Auto interval", await page.locator(".map-auto-scan-grid input[type='number']").inputValue(), "75");

      await selectRoute(page, 3, ".squad-panel");
      await page.locator(".squad-tabs > button").nth(1).click();
      check("Squads parent subtab changes to Equipment", await page.locator(".squad-tabs > button[aria-selected='true']").evaluate((node) => [...node.parentElement.children].indexOf(node)), 1);
      check("Equipment native refresh remains fenced without provider", await page.locator(".squad-header > button").isDisabled(), true);

      await selectRoute(page, 0, ".quick-actions-panel");
      await selectRoute(page, 1, ".automation-categories");
      check("Automation category survives route round trip", await page.locator(".automation-categories > button[aria-selected='true']").innerText(), automationLabel);
      await selectRoute(page, 2, ".map-panel");
      check("Map tab survives route round trip", await page.locator(".map-tabs > button[aria-selected='true']").evaluate((node) => [...node.parentElement.children].indexOf(node)), 3);
      check("Map keyword survives route round trip", await page.locator(".map-searchbar input").first().inputValue(), "retained-map-keyword");
      await selectRoute(page, 3, ".squad-panel");
      check("Squads subtab survives route round trip", await page.locator(".squad-tabs > button[aria-selected='true']").evaluate((node) => [...node.parentElement.children].indexOf(node)), 1);

      await page.locator(".profile-collapse").click();
      const profiles = page.locator(".profile-item");
      await selectRoute(page, 2, ".map-panel");
      await page.evaluate(() => {
        window.__m5SawLoading = false;
        window.__m5Observer = new MutationObserver(() => { if (document.querySelector(".profile-switch-loading")) window.__m5SawLoading = true; });
        window.__m5Observer.observe(document.body, { childList: true, subtree: true });
      });
      await profiles.nth(1).click();
      await page.waitForFunction(() => window.__m5SawLoading === true);
      await page.waitForFunction(() => !document.querySelector(".profile-switch-loading"));
      await page.evaluate(() => window.__m5Observer?.disconnect?.());
      check("uncached profile replacement crossed loading boundary", await page.evaluate(() => window.__m5SawLoading), true);
      check("App-owned Map tab persists across profile replacement", await page.locator(".map-tabs > button[aria-selected='true']").evaluate((node) => [...node.parentElement.children].indexOf(node)), 3);
      check("page-local Map keyword resets across profile replacement", await page.locator(".map-searchbar input").first().inputValue(), "");
      check("page-local Map scan mode resets across profile replacement", await page.locator(".map-scan-tabs > button[aria-selected='true']").evaluate((node) => [...node.parentElement.children].indexOf(node)), 0);
      check("page-local Map quality filter resets across profile replacement", await page.locator(".map-searchbar select").filter({ visible: true }).first().inputValue(), "");
      check("page-local Map plunderable filter resets across profile replacement", await page.locator(".map-plunderable-filter input[type='checkbox']").isChecked(), false);
      await page.locator(".map-scan-tabs > button").nth(1).click();
      check("profile-scoped Map Auto interval resets for replacement profile", await page.locator(".map-auto-scan-grid input[type='number']").inputValue(), "60");
      await selectRoute(page, 1, ".automation-categories");
      check("App-owned Automation category persists across profile replacement", await page.locator(".automation-categories > button[aria-selected='true']").innerText(), automationLabel);
      await selectRoute(page, 3, ".squad-panel");
      check("App-owned Squads subtab persists across profile replacement", await page.locator(".squad-tabs > button[aria-selected='true']").evaluate((node) => [...node.parentElement.children].indexOf(node)), 1);
      await page.evaluate(() => {
        window.__m5SawCachedLoading = false;
        window.__m5CachedObserver = new MutationObserver(() => {
          if (document.querySelector(".profile-switch-loading")) window.__m5SawCachedLoading = true;
        });
        window.__m5CachedObserver.observe(document.body, { childList: true, subtree: true });
      });
      await profiles.nth(0).click();
      await page.evaluate(() => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve))));
      check("cached Local 1 return bypasses loading", await page.evaluate(() => window.__m5SawCachedLoading), false);
      await profiles.nth(1).click();
      await page.evaluate(() => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve))));
      check("cached Local 2 return bypasses loading", await page.evaluate(() => window.__m5SawCachedLoading), false);
      await page.evaluate(() => window.__m5CachedObserver?.disconnect?.());
      check("cached profile returns preserve current Squads route", await page.locator(".side-nav > button").nth(3).getAttribute("aria-current"), "page");
      await capture(page, "en-light-profile-boundary-parent-tabs");
    } finally { await context.close(); }
  }

  // Map-specific local retention: query/tab/Manual-Auto state remains owned by the retained Map page.
  {
    const { context, page } = await openPage(browser, { language: "ja", theme: "dark", width: 1280, height: 900, state: "map-filter-lifecycle", route: "map-data" });
    try {
      const map = page.locator(".map-panel");
      await map.waitFor();
      await page.locator(".map-tabs > button").nth(1).click();
      const search = page.locator(".map-searchbar input").first();
      await search.fill("Fixture");
      await page.locator(".map-scan-tabs > button").nth(1).click();
      await page.locator(".map-auto-scan-grid input[type='number']").fill("75");
      check("Map Auto interval local edit applies", await page.locator(".map-auto-scan-grid input[type='number']").inputValue(), "75");
      await selectRoute(page, 0, ".quick-actions-panel");
      await selectRoute(page, 2, ".map-panel");
      check("Map selected data tab survives route return", await page.locator(".map-tabs > button[aria-selected='true']").evaluate((node) => [...node.parentElement.children].indexOf(node)), 1);
      check("Map keyword survives route return", await page.locator(".map-searchbar input").first().inputValue(), "Fixture");
      check("Map Manual/Auto tab survives route return", await page.locator(".map-scan-tabs > button[aria-selected='true']").evaluate((node) => [...node.parentElement.children].indexOf(node)), 1);
      check("Map Auto interval survives route return", await page.locator(".map-auto-scan-grid input[type='number']").inputValue(), "75");
      check("Map Run-now remains fenced while offline", await page.locator(".map-auto-scan-options button").isDisabled(), true);
      await capture(page, "ja-dark-map-retention-auto-local");
    } finally { await context.close(); }
  }

  // Fresh current-App Map count/pagination proof on the default 52-row City fixture.
  {
    const { context, page } = await openPage(browser, { language: "en", theme: "light", width: 1280, height: 900, state: "map-city", route: "map-data" });
    try {
      await page.locator(".map-pagination").waitFor();
      check("Map City result count exposes all fixture rows", await page.locator(".map-result-count").innerText(), "52 items");
      check("Map City first page renders 50 rows", await page.locator(".map-table tbody tr").count(), 50);
      await page.locator(".map-pagination button").last().click();
      await page.waitForFunction(() => document.querySelectorAll(".map-table tbody tr").length === 2);
      check("Map pagination advances to second page", await page.locator(".map-pagination span").innerText(), "Page 2 of 2");
      check("Map City second page renders remainder", await page.locator(".map-table tbody tr").count(), 2);
      await selectRoute(page, 0, ".quick-actions-panel");
      await selectRoute(page, 2, ".map-panel");
      check("Map pagination survives ordinary route hiding", await page.locator(".map-pagination span").innerText(), "Page 2 of 2");
      check("Map result count survives ordinary route hiding", await page.locator(".map-result-count").innerText(), "52 items");
      await capture(page, "en-light-map-pagination-return");
    } finally { await context.close(); }
  }

  // Representative E-H child-local state plus hidden Activity effect cleanup in the real App.
  {
    const { context, page } = await openPage(browser, { language: "en", theme: "light", width: 1280, height: 900, state: "city-layout-populated", route: "city-layout" });
    try {
      const zoom = page.locator(".city-layout-zoom");
      await zoom.waitFor();
      const visibleKeydownOwners = await page.evaluate(() => window.__m5WindowListenerCount("keydown"));
      await zoom.locator("button").nth(1).click();
      const editedZoom = await zoom.innerText();
      await selectRoute(page, 0, ".quick-actions-panel");
      const hiddenKeydownOwners = await page.evaluate(() => window.__m5WindowListenerCount("keydown"));
      check("City hidden Activity cleans exactly one window keydown owner", hiddenKeydownOwners, visibleKeydownOwners - 1);
      await selectRoute(page, 4, ".city-layout-panel");
      check("City return restores exactly one window keydown owner", await page.evaluate(() => window.__m5WindowListenerCount("keydown")), visibleKeydownOwners);
      check("City local zoom edit survives route hiding", await page.locator(".city-layout-zoom").innerText(), editedZoom);
      await capture(page, "en-light-city-local-retention-owner-return");
    } finally { await context.close(); }
  }

  // Representative Automation draft retained by Activity without invoking its provider-backed action.
  {
    const { context, page } = await openPage(browser, { language: "en", theme: "light", width: 1280, height: 900, state: "automation-training-ready", route: "automation" });
    try {
      const draft = page.locator(".automation-config-body:visible input[type='number']:not([disabled])").first();
      await draft.waitFor();
      await draft.fill("17");
      check("Automation local draft edit applies", await draft.inputValue(), "17");
      await selectRoute(page, 0, ".quick-actions-panel");
      await selectRoute(page, 1, ".automation-categories");
      check("Automation local draft survives ordinary route hiding", await page.locator(".automation-config-body:visible input[type='number']:not([disabled])").first().inputValue(), "17");
      await capture(page, "en-light-automation-draft-return");
    } finally { await context.close(); }
  }

  // Representative Squads/AFK editor draft retained by Activity without Run/Stop/Recall.
  {
    const { context, page } = await openPage(browser, { language: "en", theme: "light", width: 1280, height: 900, state: "squads-profile-normal", route: "march" });
    try {
      const draft = page.locator(".monster-afk-editor input[type='number']:not([disabled])").first();
      await draft.waitFor();
      await draft.fill("13");
      check("Squads AFK local editor draft applies", await draft.inputValue(), "13");
      await selectRoute(page, 0, ".quick-actions-panel");
      await selectRoute(page, 3, ".squad-panel");
      check("Squads AFK local editor draft survives ordinary route hiding", await page.locator(".monster-afk-editor input[type='number']:not([disabled])").first().inputValue(), "13");
      await capture(page, "en-light-squads-afk-draft-return");
    } finally { await context.close(); }
  }

  // Cross-server, profile-note dialog and live locale/theme are exercised in the real App host.
  {
    const { context, page } = await openPage(browser, { language: "en", theme: "light", width: 1280, height: 900, state: "shell-profiles" });
    try {
      await page.locator(".server-jump > button").click();
      check("Cross-server popover mounts once", await page.locator(".server-jump-popover").count(), 1);
      check("Cross-server unavailable path is source-blocked", await page.locator(".server-jump-error").count(), 1);
      await page.locator(".brand-lockup").click({ position: { x: 4, y: 4 } });
      await page.locator(".server-jump-popover").waitFor({ state: "detached" });

      await page.locator(".profile-collapse").click();
      const row = page.locator(".profile-row").first();
      await row.hover();
      await row.locator(".profile-note-edit").click();
      const noteDialog = page.locator("dialog.profile-dialog-backdrop");
      await noteDialog.waitFor();
      check("profile note dialog is outside routed page content", await page.locator("main.app-shell dialog.profile-dialog-backdrop").count(), 1);
      await page.keyboard.press("Escape");
      await noteDialog.waitFor({ state: "detached" });

      await selectRoute(page, 2, ".map-panel");
      await page.locator(".language-select select").selectOption("ja");
      await page.waitForFunction(() => document.documentElement.lang === "ja");
      await page.locator(".theme-toggle").click();
      await page.waitForFunction(() => document.documentElement.dataset.theme === "dark");
      await page.waitForTimeout(280);
      check("retained Map survives live locale change", await page.locator(".map-panel").count(), 1);
      check("retained Map survives live theme change", await page.locator(".map-panel").count(), 1);
      check("live locale is Japanese", await page.locator("html").getAttribute("lang"), "ja");
      check("live theme is dark", await page.locator("html").getAttribute("data-theme"), "dark");
      await capture(page, "ja-dark-live-theme-locale-retained-map");
    } finally { await context.close(); }
  }

  // Exit idle/busy host boundary remains outside main.app-shell. No confirmation is clicked.
  for (const [state, busy] of [["shell-exit", false], ["shell-exit-busy", true]]) {
    const { context, page } = await openPage(browser, { language: busy ? "ja" : "en", theme: busy ? "dark" : "light", width: busy ? 740 : 1280, height: busy ? 650 : 900, state });
    try {
      const dialog = page.locator("dialog.app-exit-backdrop");
      await dialog.waitFor();
      check(`${state} exit dialog is sibling after shell`, await page.locator("main.app-shell ~ dialog.app-exit-backdrop").count(), 1);
      check(`${state} exit action fence`, await dialog.locator("button:disabled").count(), busy ? 2 : 1);
      if (!busy) {
        await page.keyboard.press("Escape");
        await dialog.waitFor({ state: "detached" });
      } else {
        await page.keyboard.press("Escape");
        check("busy exit remains mounted after Escape", await dialog.count(), 1);
      }
    } finally { await context.close(); }
  }
} finally {
  await browser.close();
}

assert.deepEqual(consoleIssues, [], JSON.stringify(consoleIssues, null, 2));
const result = {
  marker: "LWB317_REMAINING_M5_FULL_APP_CURRENT_OK",
  assertions,
  screenshots,
  consoleIssues,
  sourceFiles: sourceHashes,
  inheritedReplays: {
    mapUnitB: "10,482 Scheduled renders; 51/51 mutations; 75 integrated cases; 15 historical browser pairs replayed as 13 exact + 2 Start Scan bounded; superseded City query-error replaced by 3 exact corrected pairs; 25 mounted assertions",
    unitsEtoH: "City 30; Hotkeys 18; Mini Games 78; Settings 63 paired states via unit-e/recover-pages.mjs --verify",
  },
  limits: [
    "Current-App integration/retention proof only; exact original page-local visual authority remains the accepted inherited source-render packets.",
    "No native/gameplay/updater/server-jump/scan/plunder/OS-hotkey action is invoked. Unavailable provider actions stay disabled/fenced.",
  ],
};
fs.writeFileSync(path.join(here, "full-app-current-results.json"), JSON.stringify(result, null, 2) + "\n");
console.log(JSON.stringify({ marker: result.marker, assertions: assertions.length, screenshots: screenshots.length, consoleErrors: consoleIssues.length }));
