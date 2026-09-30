import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, "..");
const required = [
  "index.html",
  "src/main.jsx",
  "src/App.jsx",
  "src/backendBridge.js",
  "src/mapBackend.js",
  "src/MapDataPage.jsx",
  "src/NavIcon.jsx",
  "src/Pages.jsx",
  "src/routes.js",
  "src/reference.css",
  "src/styles.css",
];

for (const relativePath of required) {
  const fullPath = path.join(root, relativePath);
  if (!fs.existsSync(fullPath)) {
    throw new Error(`Missing scaffold file: ${relativePath}`);
  }
}

const routesSource = fs.readFileSync(path.join(root, "src/routes.js"), "utf8");
for (const label of [
  "Home",
  "Automation",
  "Map Data",
  "Squads / AFK",
  "City Layout",
  "Hotkeys",
  "Mini Games",
  "Settings",
]) {
  if (!routesSource.includes(label)) {
    throw new Error(`Missing exact recovered navigation label: ${label}`);
  }
}

if (!routesSource.includes('initialRouteKey = "overview"')) {
  throw new Error("The recovered default Home/overview route is missing.");
}

const appSource = fs.readFileSync(path.join(root, "src/App.jsx"), "utf8");
if (!appSource.includes('get("previewPage")')) {
  throw new Error("The clone evidence preview-page selector is missing.");
}

const mapSource = fs.readFileSync(path.join(root, "src/mapBackend.js"), "utf8");
for (const command of [
  "map_scan_start", "map_scan_stop", "map_scan_clear", "map_scan_status",
  "map_search", "map_summary", "map_data_options",
]) {
  if (!mapSource.includes(`\"${command}\"`)) {
    throw new Error(`Clean Map integration is missing backend command: ${command}`);
  }
}

const referenceCss = fs.readFileSync(path.join(root, "src/reference.css"), "utf8");
for (const selector of [".app-shell", ".top-bar", ".app-layout", ".side-nav", ".main-view"]) {
  if (!referenceCss.includes(selector)) {
    throw new Error(`Recovered shell selector is missing: ${selector}`);
  }
}

function sha256(relativePath) {
  return crypto
    .createHash("sha256")
    .update(fs.readFileSync(path.join(root, relativePath)))
    .digest("hex")
    .toUpperCase();
}

const exactAssets = new Map([
  ["src/reference.css", "3D87E9F65B39EACE6A1A254BFC90A38ACB72613D1FFF7236C7CEE7AB9BFAF545"],
  ["src/assets/dot-offline.png", "F118D321CCD185313695DB337823E1CFD04CCA96EB9FA3447B65455E2673C31D"],
  ["src/assets/dot-online.png", "2875CC9945B55F113A7D1E194EE2A13050BF9DE6AAEC94D1DE1D664112A027D6"],
]);

for (const [relativePath, expectedHash] of exactAssets) {
  if (sha256(relativePath) !== expectedHash) {
    throw new Error(`Recovered asset hash mismatch: ${relativePath}`);
  }
}

console.log("LWBridge 0.3.17 static UI scaffold checks passed.");
