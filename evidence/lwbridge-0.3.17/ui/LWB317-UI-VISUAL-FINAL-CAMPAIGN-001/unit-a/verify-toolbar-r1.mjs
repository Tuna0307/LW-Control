import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const task = path.resolve(here, "../../LWB317-UI-MAP-TOOLBAR-VISUAL-001");
const baselineRaw = path.join(task, "milestone-a/raw");
const currentRaw = path.join(here, "raw");
const domRequire = createRequire("C:/Users/chimw/AppData/Local/Temp/lwb317-shell-mount-deps/package.json");
const { JSDOM } = domRequire("jsdom");

const firstLine = (file) => fs.readFileSync(file, "utf8").split(/\r?\n/, 1)[0];
const raw = (root, name, side) => firstLine(path.join(root, `${name}-${side}.html`));
const documentFor = (markup) => new JSDOM(`<body>${markup}</body>`).window.document;
const cases = [];
function check(name, actual, expected) {
  assert.deepEqual(actual, expected, name);
  cases.push({ name, actual, expected, pass: true });
}

const baselineTruck = raw(baselineRaw, "en-configured-truck", "current");
const originalTruck = raw(currentRaw, "en-configured-truck", "original");
const correctedTruck = raw(currentRaw, "en-configured-truck", "current");
check("baseline retained recovered plunderable class mismatch", baselineTruck.includes("map-plunderable-filter"), false);
check("corrected plunderable class restored", correctedTruck.includes("map-filter-field map-plunderable-filter"), true);
check("original plunderable class", originalTruck.includes("map-filter-field map-plunderable-filter"), true);
check("baseline formatted result count mismatch", baselineTruck.includes("1,202 items"), true);
check("corrected direct integer result count", correctedTruck.includes("1202 items"), true);
check("original direct integer result count", originalTruck.includes("1202 items"), true);

const baselinePage = documentFor(raw(baselineRaw, "en-pagination-middle", "current"));
const originalPage = documentFor(raw(currentRaw, "en-pagination-middle", "original"));
const correctedPage = documentFor(raw(currentRaw, "en-pagination-middle", "current"));
check("baseline pagination outside map-search", baselinePage.querySelectorAll(".map-search > .map-pagination").length, 0);
check("corrected pagination inside map-search", correctedPage.querySelectorAll(".map-search > .map-pagination").length, 1);
check("original pagination inside map-search", originalPage.querySelectorAll(".map-search > .map-pagination").length, 1);
check("corrected middle page text", correctedPage.querySelector(".map-pagination span")?.textContent, "Page 2 of 4");
check("original middle page text", originalPage.querySelector(".map-pagination span")?.textContent, "Page 2 of 4");

// Accepted differences remain visible rather than normalized away.
const errorOriginal = documentFor(raw(currentRaw, "en-query-error", "original"));
const errorCurrent = documentFor(raw(currentRaw, "en-query-error", "current"));
check("accepted original query error has no visible banner", errorOriginal.querySelectorAll(".map-scan-error").length, 0);
check("accepted current query error remains visible", errorCurrent.querySelectorAll(".map-scan-error").length, 1);
const missingOriginal = documentFor(raw(currentRaw, "en-missing-server", "original"));
const missingCurrent = documentFor(raw(currentRaw, "en-missing-server", "current"));
check("original missing-server Search enabled", missingOriginal.querySelector(".map-searchbar > button")?.disabled, false);
check("current missing-server Search fence preserved", missingCurrent.querySelector(".map-searchbar > button")?.disabled, true);
const truckOriginal = documentFor(originalTruck);
const truckCurrent = documentFor(correctedTruck);
const originalTruckAction = [...truckOriginal.querySelectorAll("button")].find((button) => button.textContent.includes("Plunder selected trucks"));
const currentTruckAction = [...truckCurrent.querySelectorAll("button")].find((button) => button.textContent.includes("Plunder selected trucks"));
check("original source action enabled", originalTruckAction?.disabled, false);
check("current protected truck provider fence preserved", currentTruckAction?.disabled, true);
check("current provider fence attribute retained", currentTruckAction?.getAttribute("data-runtime-fenced"), "true");


const treasureBaseline = documentFor(raw(baselineRaw, "en-configured-treasure", "current"));
const treasureOriginal = documentFor(raw(currentRaw, "en-configured-treasure", "original"));
const treasureCurrent = documentFor(raw(currentRaw, "en-configured-treasure", "current"));
const baselineTreasureLabels = [...treasureBaseline.querySelectorAll("label.map-filter-field")].filter((label) => /other-alliance|lucky/i.test(label.textContent));
const originalTreasureLabels = [...treasureOriginal.querySelectorAll("label.map-filter-field")].filter((label) => /other-alliance|lucky/i.test(label.textContent));
const currentTreasureLabels = [...treasureCurrent.querySelectorAll("label.map-filter-field")].filter((label) => /other-alliance|lucky/i.test(label.textContent));
check("baseline Treasure checkbox wrappers present", baselineTreasureLabels.filter((label) => label.querySelector(":scope > span")).length, 2);
check("original Treasure checkbox direct text", originalTreasureLabels.filter((label) => label.querySelector(":scope > span")).length, 0);
check("corrected Treasure checkbox direct text", currentTreasureLabels.filter((label) => label.querySelector(":scope > span")).length, 0);
check("corrected Treasure checkbox labels count", currentTreasureLabels.length, 2);
const report = {
  task: "LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-a",
  marker: "LWB317_VISUAL_FINAL_UNIT_A_CORRECTION_OK",
  cases,
  remainingDifferences: [
    "Current accessibility attributes and explicit button types are retained.",
    "Current query-error banner remains visible; recovered renderer logs the failure only.",
    "Missing-server Search and unavailable native/provider actions remain disabled in current production.",
    "Treasure claim buttons remain disabled and provider-fence attributes remain in raw DOM.",
    "Treasure checkbox labels now use recovered direct text; action availability fences remain visible and separately classified.",
  ],
};
fs.writeFileSync(path.join(here, "correction-results.json"), `${JSON.stringify(report, null, 2)}\n`);
console.log(JSON.stringify({ marker: report.marker, cases: cases.length, remainingDifferences: report.remainingDifferences.length }, null, 2));


