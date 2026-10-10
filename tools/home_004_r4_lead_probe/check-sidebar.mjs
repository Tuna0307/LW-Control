import fs from "node:fs";
import path from "node:path";

// Execute the actual current App callback expression, without mounting a UI,
// then inspect its actual JSX consumer. No synthetic callback is supplied.
const root = path.resolve(import.meta.dirname, "../..");
const app = fs.readFileSync(path.join(root, "src/LWBridge.UI-0.3.17/src/App.jsx"), "utf8");
const sidebar = fs.readFileSync(path.join(root, "src/LWBridge.UI-0.3.17/src/ProfileSidebar.jsx"), "utf8");
const match = app.match(/const nativeProfileCallbacks = ([\s\S]*?);\r?\n  const profileCallbacks/);
if (!match || !app.includes("{...profileCallbacks}")) throw new Error("Current App callback extraction failed");
const callback = () => {};
const actual = new Function("backendBridge", "selectNativeProfile", "reorderNativeProfiles", "updateNativeProfileNote",
  "return (" + match[1] + ");")({ mode: "native" }, callback, callback, callback);
const missing = ["readInstance", "onStartProfile", "onStopProfile", "onRestartAll"]
  .filter(key => typeof actual[key] !== "function");
const actualSidebarGates = sidebar.includes("if (!state || !readInstance) return undefined") &&
  sidebar.includes("!onStartProfile}") && sidebar.includes("!onStopProfile}");
const report = {
  proof: "HOME004_R4_LEAD_ACTUAL_NATIVE_SIDEBAR_CALLBACKS",
  gameLaunches: 0,
  desktopActions: 0,
  actualNativeCallbacks: Object.keys(actual),
  missing,
  consumerFencesUnprovidedPollingAndBatchActions: actualSidebarGates,
  defectReproduced: missing.length === 4 && actualSidebarGates,
};
console.log(JSON.stringify(report, null, 2));
if (!report.defectReproduced) throw new Error("Recorded incomplete native sidebar inverse did not reproduce");
