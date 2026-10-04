import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const src = path.join(repo, "src/LWBridge.UI-0.3.17/src");
const rawSourceDir = path.join(here, "raw");
const rawDir = path.join(here, "browser-raw");
const generatedDir = path.join(here, "browser-generated");
fs.mkdirSync(rawDir, { recursive: true });
fs.mkdirSync(generatedDir, { recursive: true });

const hashBytes = (bytes) => crypto.createHash("sha256").update(bytes).digest("hex");
const hashFile = (file) => hashBytes(fs.readFileSync(file));
const rel = (file) => path.relative(repo, file).replaceAll("\\", "/");
const writeJson = (file, value) => fs.writeFileSync(file, JSON.stringify(value, null, 2) + "\n");
const readMarkup = (file) => fs.readFileSync(file, "utf8").trim();

const originalCssFile = path.join(repo, "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-rIL9Fpht.css");
const referenceCssFile = path.join(src, "reference.css");
const stylesCssFile = path.join(src, "styles.css");
const originalCss = fs.readFileSync(originalCssFile, "utf8");
const currentCss = fs.readFileSync(referenceCssFile, "utf8") + "\n" + fs.readFileSync(stylesCssFile, "utf8");

const selected = [
  { id: "city-populated-en-light", source: "en-city-populated", language: "en", theme: "light", width: 1280, height: 900, tab: "city" },
  { id: "resource-populated-ja-dark", source: "ja-resource-populated", language: "ja", theme: "dark", width: 1280, height: 900, tab: "resource" },
  { id: "monster-populated-en-dark", source: "en-monster-populated", language: "en", theme: "dark", width: 1280, height: 900, tab: "monster" },
  { id: "truck-populated-ja-light-375", source: "ja-truck-populated", language: "ja", theme: "light", width: 375, height: 1100, tab: "truck" },
  { id: "railway-populated-en-light", source: "en-railway-populated", language: "en", theme: "light", width: 1280, height: 900, tab: "railway" },
  { id: "dispatch-populated-ja-dark", source: "ja-dispatch-populated", language: "ja", theme: "dark", width: 1280, height: 900, tab: "dispatch" },
  { id: "ghost-populated-en-dark", source: "en-ghost-populated", language: "en", theme: "dark", width: 1280, height: 900, tab: "ghost" },
  { id: "treasure-populated-ja-light-375", source: "ja-treasure-populated", language: "ja", theme: "light", width: 375, height: 1100, tab: "treasure" },
  { id: "city-empty-en-light", source: "en-city-empty", language: "en", theme: "light", width: 1280, height: 900, tab: "city" },
  { id: "city-loading-ja-dark", source: "ja-city-loading", language: "ja", theme: "dark", width: 1280, height: 900, tab: "city" },
  { id: "city-error-en-dark", source: "en-city-error", language: "en", theme: "dark", width: 1280, height: 900, tab: "city" },
  { id: "scheduled-empty-en-light", source: "en-map-scheduled", language: "en", theme: "light", width: 1280, height: 1000, tab: "scheduledPlunder" },
  { id: "scheduled-populated-ja-dark", source: "ja-map-scheduled-populated", language: "ja", theme: "dark", width: 1280, height: 1200, tab: "scheduledPlunder" },
  { id: "scheduled-conditional-en-dark", source: "en-map-scheduled-conditional", language: "en", theme: "dark", width: 1280, height: 1200, tab: "scheduledPlunder" },
  { id: "auto-configured-en-dark", source: "en-auto-configured", language: "en", theme: "dark", width: 1280, height: 1100, tab: "city" },
  { id: "auto-configured-ja-light-375", source: "ja-auto-configured", language: "ja", theme: "light", width: 375, height: 1200, tab: "treasure" }
];

const makeDocument = ({ spec, markup, side }) => {
  const css = side === "original" ? originalCss : currentCss;
  return '<!doctype html><html lang="' + spec.language + '" data-theme="' + spec.theme + '"><head>' +
    '<meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">' +
    '<title>' + spec.id + ' ' + side + '</title><style>' + css + '</style></head><body>' +
    '<main class="app-shell"><div class="app-layout single-profile"><nav class="nav-rail"></nav><section class="main-view">' +
    markup + '</section></div></main></body></html>';
};

const pairs = [];
for (const spec of selected) {
  const sourceFiles = {
    original: path.join(rawSourceDir, spec.source + "-original.html"),
    current: path.join(rawSourceDir, spec.source + "-current.html")
  };
  for (const side of ["original", "current"]) assert.ok(fs.existsSync(sourceFiles[side]), spec.id + "/" + side + ": source render");
  const markup = { original: readMarkup(sourceFiles.original), current: readMarkup(sourceFiles.current) };
  for (const side of ["original", "current"]) {
    assert.ok(markup[side].startsWith('<section class="panel map-panel"'), spec.id + "/" + side + ": full Map panel root");
    fs.writeFileSync(path.join(rawDir, spec.id + "-" + side + ".html"), markup[side] + "\n");
    fs.writeFileSync(path.join(generatedDir, spec.id + "-" + side + ".html"), makeDocument({ spec, markup: markup[side], side }));
  }
  pairs.push({
    ...spec,
    viewport: { width: spec.width, height: spec.height, deviceScaleFactor: 1 },
    sourceFiles: Object.fromEntries(Object.entries(sourceFiles).map(([side, file]) => [side, { path: rel(file), sha256: hashFile(file) }])),
    generatedFiles: Object.fromEntries(["original", "current"].map((side) => {
      const file = path.join(generatedDir, spec.id + "-" + side + ".html");
      return [side, { path: rel(file), sha256: hashFile(file) }];
    })),
    rawExact: markup.original === markup.current
  });
}

const manifest = {
  task: "LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-b",
  marker: "LWB317_VISUAL_FINAL_UNIT_B_INTEGRATED_BROWSER_PAIRS",
  generatedAt: "2026-10-04",
  scope: "Whole source-rendered Map panel including Manual/Auto header, summary, filters, actual MapTable or ScheduledPlunder bodies, result count and pagination. Original uses recovered CSS asset; current uses production reference.css then styles.css.",
  fixedEnvironment: {
    timezone: "Asia/Singapore",
    deviceScaleFactor: 1,
    originalCss: { path: rel(originalCssFile), sha256: hashFile(originalCssFile) },
    currentCssOrder: [rel(referenceCssFile), rel(stylesCssFile)],
    currentCssSha256: hashBytes(Buffer.from(currentCss, "utf8"))
  },
  pairs
};
writeJson(path.join(here, "integrated-browser-pairs.json"), manifest);
console.log(JSON.stringify({ marker: manifest.marker, pairs: pairs.length, rawExactPairs: pairs.filter((pair) => pair.rawExact).length }, null, 2));
