import fs from "node:fs";
import path from "node:path";
import assert from "node:assert/strict";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const appPath = path.join(repo, "src/LWBridge.UI-0.3.17/src/App.jsx");
const app = fs.readFileSync(appPath, "utf8");

function contracts(source) {
  const selectStart = source.indexOf("const selectRoute = useCallback");
  const selectEnd = source.indexOf("const acknowledgeMapScan", selectStart);
  const selectBody = source.slice(selectStart, selectEnd);
  return {
    keyedRetention: source.includes("<Fragment key={selectedProfileId}>"),
    activityRetention: source.includes('<Activity key={route.key} mode={route.key === activeRoute ? "visible" : "hidden"}>'),
    mapRefreshBeforeTransition: selectBody.indexOf('if (routeKey === "map-data") refreshMapSummary().catch(() => {});') >= 0
      && selectBody.indexOf('if (routeKey === "map-data") refreshMapSummary().catch(() => {});') < selectBody.indexOf("startRouteTransition(() => {"),
    automationParentOwner: source.includes("automation: { activeCategory: automationCategory, onActiveCategoryChange: setAutomationCategory }"),
    mapParentOwner: source.includes('"map-data": { activeTab: mapTab, onActiveTabChange: setMapTab }'),
    squadsParentOwner: source.includes("march: { activeTab: squadTab, onActiveTabChange: setSquadTab }"),
  };
}

const baseline = contracts(app);
assert.ok(Object.values(baseline).every(Boolean), JSON.stringify(baseline));
const mutations = [
  {
    name: "remove selected-profile retained subtree key",
    source: app.replace("<Fragment key={selectedProfileId}>", "<Fragment>"),
    contract: "keyedRetention",
  },
  {
    name: "remove Map summary dispatch before route transition",
    source: app.replace('    if (routeKey === "map-data") refreshMapSummary().catch(() => {});\n', ""),
    contract: "mapRefreshBeforeTransition",
  },
  {
    name: "remove App-owned Map tab selection",
    source: app.replace('    "map-data": { activeTab: mapTab, onActiveTabChange: setMapTab },\n', ""),
    contract: "mapParentOwner",
  },
];

const detections = mutations.map((mutation) => {
  const mutated = contracts(mutation.source);
  return { name: mutation.name, contract: mutation.contract, detected: mutated[mutation.contract] === false };
});
assert.ok(detections.every((entry) => entry.detected), JSON.stringify(detections));
console.log(JSON.stringify({ marker: "LWB317_REMAINING_M5_MUTATIONS_DETECTED", baseline, detections }));
