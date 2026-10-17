import fs from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { withMapNameFeedback, MAP_NAME_FEEDBACK } from "../src/locales/map010-feedback.js";
import { withMapOtherTexts } from "../src/locales/map014-other.js";
import { withGhostRewardTexts, GHOST_REWARD_MESSAGES } from "../src/locales/map014-ghost-rewards.js";
import { withZombieBossTexts, ZOMBIE_BOSS_MESSAGES } from "../src/locales/map014-zombie-boss.js";
import { withHomeLaunchFeedback } from "../src/locales/home-launch-feedback.js";
import { withMapScanControlTexts } from "../src/locales/map017-scan-controls.js";
import { withFrontlineCountTexts } from "../src/locales/feature018-g-count.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, "..");
const pageModules = ["Pages.jsx", "sharedPageUI.jsx", "HomePage.jsx", "AutomationPage.jsx", "SquadsPage.jsx", "CityLayoutPage.jsx", "HotkeyPages.jsx", "SettingsPage.jsx", "MapRoutePage.jsx"];
const sourceFiles = ["src/App.jsx", ...pageModules.map((file) => `src/${file}`), "src/MapDataPage.jsx", "src/mapTablePresentation.js", "src/previewConfigHook.jsx", "src/AutomationMeta.jsx", "src/RallyJoinSettings.jsx", "src/DispatchAssistManual.jsx", "src/previewEquipmentContracts.js"];
const localeCodes = ["en", "zh-CN", "zh-TW", "ja", "ko", "vi", "id", "ru", "pt"];

const withMapExtras = (code, messages) => withFrontlineCountTexts(code, withMapScanControlTexts(code, withHomeLaunchFeedback(code, withZombieBossTexts(code, withGhostRewardTexts(code, withMapOtherTexts(code, withMapNameFeedback(code, messages)))))));
// Ghost reward filter labels: every one of the nine clone languages must carry the
// complete key set with the same {placeholders} as English (no silent English fallback).
for (const code of localeCodes) {
  const set = GHOST_REWARD_MESSAGES[code];
  if (!set || Object.keys(set).length !== Object.keys(GHOST_REWARD_MESSAGES.en).length ||
      Object.keys(GHOST_REWARD_MESSAGES.en).some((key) => typeof set[key] !== "string" || !set[key].trim() ||
        JSON.stringify(set[key].match(/\{\w+\}/g)?.sort() ?? []) !== JSON.stringify(GHOST_REWARD_MESSAGES.en[key].match(/\{\w+\}/g)?.sort() ?? [])))
    throw new Error(`Ghost reward filter message coverage incomplete for ${code}`);
}
// Zombie Boss labels: all nine clone languages carry the full key set (no silent English fallback).
for (const code of localeCodes) {
  const set = ZOMBIE_BOSS_MESSAGES[code];
  if (!set || Object.keys(set).length !== Object.keys(ZOMBIE_BOSS_MESSAGES.en).length ||
      Object.keys(ZOMBIE_BOSS_MESSAGES.en).some((key) => typeof set[key] !== "string" || !set[key].trim()))
    throw new Error(`Zombie Boss message coverage incomplete for ${code}`);
}
const en = withMapExtras("en", (await import(pathToFileURL(path.join(root, "src", "locales", "en.js")))).default);
const literalKeys = new Set();
for (const relative of sourceFiles) {
  const source = fs.readFileSync(path.join(root, relative), "utf8");
  for (const match of source.matchAll(/\bt\(["`]([A-Za-z0-9._]+)["`]/g)) literalKeys.add(match[1]);
}

// Exact original Gather me requests common.loading, absent from the original
// English catalog. Preserve its key-as-text result rather than invent a label.
const recoveredMissingKeys = new Set(["common.loading"]);
const originalPanel = fs.readFileSync(path.join(root, "../../evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/AutomationPanel-BJ0gIqFh.js"), "utf8");
if (!originalPanel.includes("a(`common.loading`)") || "common.loading" in en) throw new Error("Revalidate original Gather missing-key exception.");
const missing = [...literalKeys].filter((key) => !key.includes("${") && !(key in en) && !recoveredMissingKeys.has(key)).sort();
if (missing.length) throw new Error(`Missing recovered English locale keys:\n${missing.join("\n")}`);

const localeCounts = {};
let englishKeys = null;
for (const code of localeCodes) {
  const recovered = (await import(pathToFileURL(path.join(root, "src", "locales", `${code}.js`)))).default;
  const messages = withMapExtras(code, recovered);
  if (!MAP_NAME_FEEDBACK[code] || Object.keys(MAP_NAME_FEEDBACK[code]).length !== Object.keys(MAP_NAME_FEEDBACK.en).length ||
      Object.keys(MAP_NAME_FEEDBACK.en).some((key) => !(key in MAP_NAME_FEEDBACK[code])))
    throw new Error(`MAP-010 message coverage incomplete for ${code}`);
  const keys = Object.keys(messages);
  if (keys.length === 0) throw new Error(`Recovered ${code} locale catalog is empty.`);
  if (code === "en") englishKeys = keys.sort();
  else {
    const missingFromLocale = englishKeys.filter((key) => !(key in messages));
    if (missingFromLocale.length) throw new Error(`Recovered ${code} locale is missing inherited keys:\n${missingFromLocale.join("\n")}`);
  }
  localeCounts[code] = keys.length;
}
for (const inheritedKey of [
  "error.OFFICIAL_LAUNCHER_HOOK_FAILED",
  "auth.error.ACCOUNT_BANNED",
  "update.error.UPDATE_STATUS_FAILED",
]) {
  for (const code of localeCodes) {
    const messages = (await import(pathToFileURL(path.join(root, "src", "locales", `${code}.js`)))).default;
    if (!(inheritedKey in messages)) throw new Error(`Recovered ${code} locale lost inherited key ${inheritedKey}.`);
  }
}

const app = fs.readFileSync(path.join(root, "src", "App.jsx"), "utf8");
if (!app.includes('backendBridge.mode === "preview" ? new URLSearchParams(window.location.search).get("previewState") || "" : ""')) {
  throw new Error("Preview state must remain fenced to browser preview mode.");
}

const pages = pageModules.map((file) => fs.readFileSync(path.join(root, "src", file), "utf8")).join("\n");
const mapPage = fs.readFileSync(path.join(root, "src", "MapDataPage.jsx"), "utf8");
if (fs.existsSync(path.join(root, "src", "ScheduledPlunder.jsx"))) throw new Error("Owner-removed Scheduled Plunder component must be deleted");
const mapPreview = fs.readFileSync(path.join(root, "src", "mapPreviewApi.js"), "utf8");
const equipmentPreview = fs.readFileSync(path.join(root, "src", "previewEquipmentContracts.js"), "utf8");
const previewCoverageSource = [app, pages, mapPage, mapPreview, equipmentPreview].join("\n");
for (const marker of [
  "home-missing", "home-connected", "home-repair", "home-recovery-failed",
  "automation-config", "automation-saving", "automation-saved", "automation-save-error", "automation-validation-error",
  "map-city", "map-resource", "map-monster", "map-truck", "map-railway", "map-dispatch", "map-ghost", "map-treasure",
  "map-auto-scheduled", "map-loading", "map-error",
  "squads-profile", "squads-equipment", "city-layout-populated", "city-layout-populated-conflict",
  "squads-equipment-rename", "squads-equipment-rename-busy", "squads-equipment-result", "squads-equipment-error", "squads-equipment-progress", "squads-equipment-offline",
  "hotkeys-connected", "hotkeys-load-error", "hotkeys-save-error",
  "mini-games-active", "mini-games-solving", "mini-games-executing", "mini-games-complete", "mini-games-all-complete",
  "mini-games-activity-ended", "mini-games-ui-open", "mini-games-conflict", "mini-games-solve-failed", "mini-games-unsupported", "mini-games-start-failed",
  "mini-games-land-success", "mini-games-land-error",
  "settings-multiprofile", "settings-complete", "settings-visual-error", "settings-feedback", "settings-feedback-success", "settings-feedback-error",
  "settings-update-checking", "settings-update-available", "settings-update-downloading", "settings-update-error",
]) {
  if (!previewCoverageSource.includes(marker)) throw new Error(`Missing preview coverage marker: ${marker}`);
}

console.log(`LWB317_UI_COMPLETE_CHECKS_OK ${literalKeys.size} referenced keys ${JSON.stringify(localeCounts)}`);
