import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { execFileSync, spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import { collectAppDependencyClosure } from "./collect-app-dependency-closure.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const manifestPath = path.join(here, "manifest.json");
const args = new Set(process.argv.slice(2));
if (!args.has("--write")) throw new Error("manifest creation is explicit: pass --write");
if (fs.existsSync(manifestPath) && !args.has("--replace")) {
  throw new Error("manifest.json already exists; validation must not repin it (use --replace only for an intentional pre-review refresh)");
}

const sha256 = (bytes) => crypto.createHash("sha256").update(bytes).digest("hex").toUpperCase();
const describe = (filePath) => {
  const bytes = fs.readFileSync(filePath);
  return { bytes: bytes.length, sha256: sha256(bytes) };
};
const rel = (relativePath) => ({ path: relativePath.replaceAll("\\", "/"), ...describe(path.join(repo, relativePath)) });
const abs = (filePath) => ({ path: filePath.replaceAll("\\", "/"), ...describe(filePath) });
const output = (command, commandArgs, cwd = repo) => execFileSync(command, commandArgs, { cwd, encoding: "utf8" }).trim();
const relativeFromRepo = (filePath) => path.relative(repo, filePath).replaceAll("\\", "/");

const resultPath = path.join(here, "full-app-current-results.json");
const result = JSON.parse(fs.readFileSync(resultPath, "utf8"));
if (result.marker !== "LWB317_REMAINING_M5_FULL_APP_CURRENT_OK") throw new Error("current browser result marker missing");
if (result.assertions.length !== 213 || result.screenshots.length !== 11 || result.consoleIssues.length !== 0) {
  throw new Error("current browser packet must be frozen from the reviewed 213/11/0 state");
}
const productionClosure = collectAppDependencyClosure(repo);
if (JSON.stringify(Object.keys(result.sourceFiles)) !== JSON.stringify(productionClosure)) {
  throw new Error("current browser packet source closure does not match the actual served App dependency closure");
}

const referenceFiles = [
  abs("C:/Users/chimw/OneDrive/Desktop/Github/LW/lwbridge-0.3.17.exe"),
  rel("evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js"),
  rel("evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-rIL9Fpht.css"),
  rel("evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/MapDataPanel-B4GXEND2.js"),
];

const unitBBrowserDir = path.join(repo, "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-b/browser");
const unitBPixelInputs = fs.readdirSync(unitBBrowserDir)
  .filter((name) => name === "measurements.json" || name === "comparison-results.json" || /-(?:original|current)\.png$/.test(name))
  .map((name) => relativeFromRepo(path.join(unitBBrowserDir, name)));
const correctedRawDir = path.join(repo, "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-a/lead-audit/corrected-renderer/raw");
const correctedRawInputs = fs.readdirSync(correctedRawDir)
  .filter((name) => name.endsWith(".html"))
  .map((name) => relativeFromRepo(path.join(correctedRawDir, name)));

const evidenceFiles = [
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-REMAINING-002/milestone-5/run-full-app-current.mjs",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-REMAINING-002/milestone-5/full-app-current-results.json",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-REMAINING-002/milestone-5/full-app-inventory.md",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-REMAINING-002/milestone-5/map-replay-inventory.md",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-REMAINING-002/milestone-5/collect-app-dependency-closure.mjs",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-REMAINING-002/milestone-5/replay-map-pixels-read-only.py",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-REMAINING-002/milestone-5/map-entry-current-harness.mjs",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-REMAINING-002/milestone-5/replay-map-entry-current.mjs",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-REMAINING-002/milestone-5/create-manifest.mjs",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-REMAINING-002/milestone-5/validate-milestone-5.mjs",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-REMAINING-002/milestone-5/mutation-check.mjs",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-REMAINING-002/milestone-4/closeout.md",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-REMAINING-002/milestone-4/shell-source-render/validate-read-only.mjs",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-b/validate-unit-b.mjs",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-b/results.json",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-b/current-results.json",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-b/pixel-results.json",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-b/mounted/browser-interactions.json",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-a/lead-audit/capture-corrected-error.mjs",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-a/lead-audit/corrected-error-browser.json",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-REFRESH-FEEDBACK-001/harness.mjs",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-e/recover-pages.mjs",
  "evidence/lwbridge-0.3.17/ui/LWB317-PENDING-WIP-CLOSEOUT-001/check-archive.mjs",
  ...unitBPixelInputs,
  ...correctedRawInputs,
].map(rel);

const packageFiles = [
  "src/LWBridge.UI-0.3.17/package.json",
  "src/LWBridge.UI-0.3.17/package-lock.json",
  "src/LWBridge.UI-0.3.17/vite.config.js",
].map(rel);

const packageJson = JSON.parse(fs.readFileSync(path.join(repo, "src/LWBridge.UI-0.3.17/package.json"), "utf8"));
const installedPackageVersion = (relativePackageJson) => JSON.parse(fs.readFileSync(path.join(repo, "src/LWBridge.UI-0.3.17/node_modules", relativePackageJson, "package.json"), "utf8")).version;
const playwrightPackage = "C:/Users/chimw/AppData/Local/Temp/lwbridge-playwright/node_modules/playwright/package.json";
const playwrightVersion = fs.existsSync(playwrightPackage)
  ? JSON.parse(fs.readFileSync(playwrightPackage, "utf8")).version
  : "unavailable";
const python = spawnSync("python", ["--version"], { encoding: "utf8" });
const pillow = spawnSync("python", ["-c", "import PIL; print(PIL.__version__)"], { encoding: "utf8" });
const chromeExe = "C:/Program Files/Google/Chrome/Application/chrome.exe";
const chromeVersion = output("powershell.exe", [
  "-NoProfile",
  "-Command",
  `(Get-Item -LiteralPath '${chromeExe.replaceAll("'", "''")}').VersionInfo.FileVersion`,
]);

const manifest = {
  schema: 1,
  task: "LWB317-UI-VISUAL-REMAINING-002",
  milestone: 5,
  createdAt: new Date().toISOString(),
  productionBaseline: output("git", ["rev-parse", "HEAD"]),
  branch: output("git", ["rev-parse", "--abbrev-ref", "HEAD"]),
  expected: {
    marker: "LWB317_REMAINING_M5_FULL_APP_CURRENT_OK",
    assertions: 213,
    screenshots: 11,
    consoleIssues: 0,
    routes: ["overview", "automation", "map-data", "march", "city-layout", "hotkeys", "mini-games", "settings"],
    routeTransitionAssertionsPerMode: 16,
    modes: ["en-light-desktop", "ja-dark-desktop", "en-dark-narrow", "ja-light-narrow"],
  },
  referenceFiles,
  productionSources: result.sourceFiles,
  packageFiles,
  evidenceFiles,
  screenshots: result.screenshots,
  inheritedReplayExpectations: {
    mapUnitB: "LWB317_VISUAL_FINAL_UNIT_B_VALIDATED",
    mapPixels: "LWB317_REMAINING_M5_MAP_PIXELS_READ_ONLY_OK",
    correctedCityError: "CORRECTED_SEARCH_REJECTION_EXACT",
    mapEntryCurrent: "LWB317_REMAINING_M5_MAP_ENTRY_CURRENT_OK",
    unitsEtoH: ["city-layout 30 actual paired render states", "hotkeys 18 actual paired render states", "mini-games 78 actual paired render states", "settings 63 actual paired render states"],
    shellM4: "SHELL_SOURCE_RENDER_VALIDATE_OK",
    archive: "LWB317_LEGACY_WIP_ARCHIVE_OK exactFiles=10",
  },
  tools: {
    node: process.version,
    viteRequested: packageJson.devDependencies?.vite || packageJson.dependencies?.vite || "unlisted",
    vite: installedPackageVersion("vite"),
    react: installedPackageVersion("react"),
    reactDom: installedPackageVersion("react-dom"),
    viteReactPlugin: installedPackageVersion("@vitejs/plugin-react"),
    playwright: playwrightVersion,
    chrome: chromeVersion,
    python: (python.stdout || python.stderr || "").trim(),
    pillow: (pillow.stdout || pillow.stderr || "").trim(),
    git: output("git", ["--version"]),
  },
  limits: result.limits,
};

fs.writeFileSync(manifestPath, JSON.stringify(manifest, null, 2) + "\n");
console.log(JSON.stringify({ marker: "LWB317_REMAINING_M5_MANIFEST_CREATED", productionBaseline: manifest.productionBaseline, sources: Object.keys(manifest.productionSources).length, evidenceFiles: manifest.evidenceFiles.length, screenshots: manifest.screenshots.length }));
