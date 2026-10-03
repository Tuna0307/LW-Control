import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import en from "../../../../../src/LWBridge.UI-0.3.17/src/locales/en.js";
import zhCN from "../../../../../src/LWBridge.UI-0.3.17/src/locales/zh-CN.js";
import zhTW from "../../../../../src/LWBridge.UI-0.3.17/src/locales/zh-TW.js";
import ja from "../../../../../src/LWBridge.UI-0.3.17/src/locales/ja.js";
import ko from "../../../../../src/LWBridge.UI-0.3.17/src/locales/ko.js";
import vi from "../../../../../src/LWBridge.UI-0.3.17/src/locales/vi.js";
import id from "../../../../../src/LWBridge.UI-0.3.17/src/locales/id.js";
import ru from "../../../../../src/LWBridge.UI-0.3.17/src/locales/ru.js";
import pt from "../../../../../src/LWBridge.UI-0.3.17/src/locales/pt.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const pages = fs.readFileSync(path.join(repo, "src/LWBridge.UI-0.3.17/src/Pages.jsx"), "utf8");
const contracts = fs.readFileSync(path.join(repo, "src/LWBridge.UI-0.3.17/src/previewRemainingPagesContracts.js"), "utf8");
const inventory = JSON.parse(fs.readFileSync(path.join(here, "../milestone-a/inventory.json"), "utf8")).inventory;
const browser = JSON.parse(fs.readFileSync(path.join(here, "browser-results.json"), "utf8"));

assert.equal(inventory.reduce((sum, entry) => sum + entry.count, 0), 54);
assert.deepEqual(inventory.map((entry) => entry.page), ["City Layout", "Hotkeys", "Mini Games", "Settings"]);

const catalogs = { en, "zh-CN": zhCN, "zh-TW": zhTW, ja, ko, vi, id, ru, pt };
const requiredKeys = [
  "cityLayout.title", "cityLayout.offline", "cityLayout.stale", "cityLayout.enterCity", "cityLayout.applyProgress", "cityLayout.cancelApply", "cityLayout.applyConfirm",
  "hotkeys.title", "hotkeys.description", "hotkeys.offlineHint", "hotkeys.loadFailed", "hotkeys.saveFailed", "hotkeys.attack.title", "hotkeys.attack.description", "hotkeys.attackSpeedupItem", "hotkeys.attackSpeedupDiamond", "hotkeys.attackSpeedupWarning", "hotkeys.recall.title", "hotkeys.recall.description", "hotkeys.shield.title", "hotkeys.shield.description", "hotkeys.shieldUse.title", "hotkeys.shieldUse.description", "hotkeys.shieldUse.warning", "hotkeys.equipment.title", "hotkeys.equipment.description", "hotkeys.randomRelocate.title", "hotkeys.randomRelocate.description", "hotkeys.allianceRelocate.title", "hotkeys.allianceRelocate.description", "hotkeys.relocationWarning", "hotkeys.frontlineReinforce.title", "hotkeys.frontlineReinforce.description",
  "miniGames.title", "miniGames.description", "miniGames.treasureChest.title", "miniGames.treasureChest.description", "miniGames.treasureChest.enabled", "miniGames.landCell.title", "miniGames.landCell.description", "miniGames.landCellAction", "miniGames.landCellOpening", "miniGames.landCellSent", "miniGames.landCellFailed", "miniGames.sheep.title", "miniGames.sheep.description", "miniGames.sheep.level", "miniGames.sheep.elapsed", "miniGames.sheep.progress", "miniGames.sheep.solving", "miniGames.sheep.executing", "miniGames.sheep.dailyLimit", "miniGames.sheep.allCompleted", "miniGames.sheep.activityEnded", "miniGames.sheep.uiOpen", "miniGames.sheep.conflict", "miniGames.sheep.solveFailed", "miniGames.sheep.unsupported", "miniGames.sheep.failed",
  "settings.title", "settings.description", "settings.visualMetrics.title", "settings.visualMetrics.description", "settings.visualMetrics.showFps", "settings.visualMetrics.showPing", "settings.visualMetrics.loadFailed", "settings.visualMetrics.saveFailed", "settings.accountInteraction.title", "settings.accountInteraction.description", "settings.accountInteraction.focusGameOnProfileSelect",
  "feedback.title", "feedback.description", "feedback.privacyNotice", "feedback.export", "feedback.exporting", "feedback.success", "feedback.failed", "feedback.progress.label", "feedback.progress.preparing", "feedback.progress.exporting", "feedback.progress.finalizing",
  "update.title", "update.currentVersion", "update.latestVersion", "update.idle", "update.checking", "update.upToDate", "update.available", "update.opening", "update.publishedAt", "update.downloading", "update.downloadDirectory", "update.check", "update.checkCooldown", "update.downloadAndOpen", "update.error.UPDATE_STATUS_FAILED", "update.error.UPDATE_CHECK_FAILED", "update.error.UPDATE_DOWNLOAD_FAILED", "update.error.default"
];

const localeSummary = {};
for (const [language, catalog] of Object.entries(catalogs)) {
  const missing = requiredKeys.filter((key) => typeof catalog[key] !== "string" || catalog[key].length === 0);
  assert.deepEqual(missing, [], `${language} missing recovered four-page keys: ${missing.join(", ")}`);
  localeSummary[language] = { totalCatalogKeys: Object.keys(catalog).length, recoveredKeysChecked: requiredKeys.length };
}

for (const marker of [
  "cityMovedOccupiedPoints", "cityLayoutIssues", "citySelectionState", "window.removeEventListener(\"keydown\", onKeyDown)",
  "HOTKEY_CARDS", "saveHotkeyField", "pendingField", "category=\"miniGames\"", "formatSheepElapsed", "window.clearInterval(interval)",
  "visualBusy", "previousVisual", "formatDiagnosticBytes", "feedbackProgress.percent", "updateStatusBusy", "updateDownloadVisible", "update.checkCooldown"
]) assert.ok(pages.includes(marker) || contracts.includes(marker), `Missing final production marker: ${marker}`);

assert.equal(browser.screenshots.length, 12);
for (const screenshot of browser.screenshots) {
  const file = path.join(here, screenshot.file);
  assert.ok(fs.existsSync(file), `Missing screenshot ${screenshot.file}`);
  assert.ok(fs.statSync(file).size > 10_000, `Screenshot unexpectedly small ${screenshot.file}`);
}
assert.equal(browser.routeLeaveReturn.consoleErrors.length, 0);
assert.equal(browser.routeLeaveReturn.consoleWarnings.length, 0);
assert.equal(browser.visualInspection.horizontalClippingObserved, false);
const remainingPageSlice = pages.slice(pages.indexOf("export function CityLayoutPage"), pages.indexOf("export function PageForRoute"));
assert.ok(remainingPageSlice.length > 0);
assert.ok(!remainingPageSlice.includes("window.confirm("), "Remaining-page production path must not add native confirmation");

const result = {
  result: "LWB317_REMAINING_PAGES_CLOSEOUT_OK",
  inventoryBranches: 54,
  pages: inventory.map(({ page, count }) => ({ page, count })),
  localeSummary,
  screenshots: browser.screenshots.length,
  narrowViewport: browser.narrowViewport.pageCssViewport,
  routeConsole: { errors: 0, warnings: 0 },
  knownLimit: browser.routeLeaveReturn.knownShellLimit
};
fs.writeFileSync(path.join(here, "closeout-results.json"), `${JSON.stringify(result, null, 2)}\n`);
console.log(JSON.stringify(result, null, 2));
