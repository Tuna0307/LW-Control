import fs from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, "..");
const sourceFiles = ["src/App.jsx", "src/Pages.jsx", "src/MapDataPage.jsx"];
const localeCodes = ["en", "zh-CN", "zh-TW", "ja", "ko", "vi", "id", "ru", "pt"];

const en = (await import(pathToFileURL(path.join(root, "src", "locales", "en.js")))).default;
const literalKeys = new Set();
for (const relative of sourceFiles) {
  const source = fs.readFileSync(path.join(root, relative), "utf8");
  for (const match of source.matchAll(/\bt\("([^"]+)"/g)) literalKeys.add(match[1]);
}

const missing = [...literalKeys].filter((key) => !key.includes("${") && !(key in en)).sort();
if (missing.length) throw new Error(`Missing recovered English locale keys:\n${missing.join("\n")}`);

const localeCounts = {};
let englishKeys = null;
for (const code of localeCodes) {
  const messages = (await import(pathToFileURL(path.join(root, "src", "locales", `${code}.js`)))).default;
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

const pages = fs.readFileSync(path.join(root, "src", "Pages.jsx"), "utf8");
const mapPage = fs.readFileSync(path.join(root, "src", "MapDataPage.jsx"), "utf8");
const mapPreview = fs.readFileSync(path.join(root, "src", "mapPreviewApi.js"), "utf8");
const previewCoverageSource = [pages, mapPage, mapPreview].join("\n");
for (const marker of [
  "home-missing", "home-connected", "home-repair", "home-recovery-failed",
  "automation-config", "automation-saving", "automation-saved", "automation-save-error", "automation-validation-error",
  "map-city", "map-auto-scheduled", "map-loading", "map-error",
  "squads-profile", "squads-equipment", "city-layout-populated", "city-layout-populated-conflict",
  "squads-equipment-rename", "squads-equipment-rename-busy", "squads-equipment-result", "squads-equipment-progress",
  "hotkeys-connected", "hotkeys-load-error", "hotkeys-save-error",
  "mini-games-active", "mini-games-land-success", "mini-games-land-error", "mini-games-solve-failed",
  "settings-update-available", "settings-update-error", "settings-visual-error", "settings-feedback-success", "settings-feedback-error",
]) {
  if (!previewCoverageSource.includes(marker)) throw new Error(`Missing preview coverage marker: ${marker}`);
}

console.log(`LWB317_UI_COMPLETE_CHECKS_OK ${literalKeys.size} referenced keys ${JSON.stringify(localeCounts)}`);
