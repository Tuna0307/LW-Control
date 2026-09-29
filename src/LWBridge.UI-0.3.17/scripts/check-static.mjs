import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, "..");
const required = [
  "index.html",
  "src/main.jsx",
  "src/App.jsx",
  "src/routes.js",
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

console.log("LWBridge 0.3.17 static UI scaffold checks passed.");
