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
for (const code of localeCodes) {
  const messages = (await import(pathToFileURL(path.join(root, "src", "locales", `${code}.js`)))).default;
  const keys = Object.keys(messages);
  if (keys.length === 0) throw new Error(`Recovered ${code} locale catalog is empty.`);
  localeCounts[code] = keys.length;
}

const app = fs.readFileSync(path.join(root, "src", "App.jsx"), "utf8");
if (!app.includes('backendBridge.mode === "preview" ? new URLSearchParams(window.location.search).get("previewState") || "" : ""')) {
  throw new Error("Preview state must remain fenced to browser preview mode.");
}

const pages = fs.readFileSync(path.join(root, "src", "Pages.jsx"), "utf8");
for (const marker of [
  "home-missing", "home-connected", "home-repair", "home-recovery-failed",
  "automation-config", "squads-profile", "squads-equipment", "city-layout-populated",
  "hotkeys-connected", "mini-games-active", "settings-update-available",
]) {
  if (!pages.includes(marker)) throw new Error(`Missing preview coverage marker: ${marker}`);
}

console.log(`LWB317_UI_COMPLETE_CHECKS_OK ${literalKeys.size} referenced keys ${JSON.stringify(localeCounts)}`);
