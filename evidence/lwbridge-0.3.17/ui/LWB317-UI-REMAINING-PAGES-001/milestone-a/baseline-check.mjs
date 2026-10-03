import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const pagesPath = path.join(repo, "src/LWBridge.UI-0.3.17/src/Pages.jsx");
const contractPath = path.join(repo, "src/LWBridge.UI-0.3.17/src/previewRemainingPagesContracts.js");
const pages = fs.readFileSync(pagesPath, "utf8");
const contracts = fs.existsSync(contractPath) ? fs.readFileSync(contractPath, "utf8") : "";
const sha256 = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();

const cases = [
  { id: "CITY-BL-001", page: "City Layout", source: "City.m/re", pass: contracts.includes("cityMovedOccupiedPoints") && contracts.includes("cityLayoutIssues") && !pages.includes("function cityCellKind"), detail: "Dispatch uses guessed 8x8 cell-kind constants instead of the recovered cell/occupied-point validation contract." },
  { id: "CITY-BL-002", page: "City Layout", source: "City.ue zoom", pass: pages.includes("Math.max(4") && pages.includes("Math.min(72") && pages.includes("Math.round(zoom / CITY_BASE_CELL_SIZE * 100)"), detail: "Dispatch zoom is 16..40 by 2 and renders px; original is 4..72 by 4 and renders percentage of base 24." },
  { id: "CITY-BL-003", page: "City Layout", source: "City.ue selection/history", pass: pages.includes("city-layout-lasso") && pages.includes("event.ctrlKey") && pages.includes("citySelectionState"), detail: "Dispatch selection uses a simplified fixed-position rectangle and HTML5 drag path instead of recovered pointer/grid selection ownership." },
  { id: "CITY-BL-004", page: "City Layout", source: "City.ue warnings/progress", pass: pages.includes("cityLayout.stale") && pages.includes("cityLayout.enterCity") && pages.includes("cityLayout.applyProgress"), detail: "Dispatch omits stale draft, outside-city and apply progress/cancel presentation." },
  { id: "HOTKEY-BL-001", page: "Hotkeys", source: "Hotkey._ card catalog", pass: contracts.includes("HOTKEY_CARDS") && pages.includes("t(card.title)"), detail: "Dispatch hard-codes English title/description/warning strings instead of recovered locale keys." },
  { id: "HOTKEY-BL-002", page: "Hotkeys", source: "Hotkey._ save L/R", pass: pages.includes("pendingField") && pages.includes("saveHotkeyField") && !pages.includes("disabled={!previewEnabled || !enabled}"), detail: "Dispatch stores each card independently and wrongly gates attack speedups on attack enabled/offline preview state." },
  { id: "HOTKEY-BL-003", page: "Hotkeys", source: "Hotkey._ loading/error", pass: pages.includes("hotkeyConfig === null") && pages.includes("hotkeys.loadFailed") && pages.includes("hotkeys.saveFailed"), detail: "Dispatch has static preview errors rather than source-shaped shared config loading/save rollback state." },
  { id: "HOTKEY-BL-004", page: "Hotkeys", source: "Hotkey._ status", pass: pages.includes("online ? \"status.gameConnected\" : \"hotkeys.offlineHint\"") && pages.includes("disabled={pendingField !== null}"), detail: "Dispatch disables shortcut switches whenever preview is unavailable; original offline state only changes the status text." },
  { id: "MINI-BL-001", page: "Mini Games", source: "Index mini route + Hotkey._", pass: pages.includes("category=\"miniGames\"") || pages.includes("category = \"miniGames\""), detail: "Dispatch duplicates Mini Games rather than sharing the recovered HotkeyPanel category contract." },
  { id: "MINI-BL-002", page: "Mini Games", source: "Hotkey.h/g", pass: contracts.includes("sheepStatusKey") && contracts.includes("formatSheepElapsed") && pages.includes("formatSheepElapsed"), detail: "Dispatch uses a partial state map and hard-coded 00:31/00:00 instead of recovered status precedence and duration formatter." },
  { id: "MINI-BL-003", page: "Mini Games", source: "Hotkey._ treasure config", pass: pages.includes("treasureChestHint") && pages.includes("!online || !hotkeyConfig || pendingField !== null"), detail: "Dispatch treasure toggle is isolated local state and misses config/load/offline/pending ownership." },
  { id: "MINI-BL-004", page: "Mini Games", source: "Hotkey._ Land/Sheep", pass: pages.includes("miniGames.landCellOpening") && pages.includes("sheepActionError") && !pages.includes("className={foodRunning ? \"danger\" : \"primary\"}"), detail: "Dispatch omits Land busy copy, Sheep action error and uses a non-source danger class for Stop." },
  { id: "SET-BL-001", page: "Settings", source: "Settings.m visual metrics", pass: pages.includes("visualBusy") && pages.includes("settings.visualMetrics.saveFailed") && pages.includes("previousVisual"), detail: "Dispatch has no optimistic save pending/rollback state for visual metrics." },
  { id: "SET-BL-002", page: "Settings", source: "Settings.p feedback", pass: contracts.includes("formatDiagnosticBytes") && pages.includes("feedback.progress.${feedbackProgress.state}") && pages.includes("feedbackProgress.percent"), detail: "Dispatch export progress renders three phase labels at once and no recovered percent/progress structure." },
  { id: "SET-BL-003", page: "Settings", source: "Index.Rn updater phases", pass: pages.includes("update.publishedAt") && pages.includes("update.releaseNotes") === false && pages.includes("update.downloadDirectory") && pages.includes("update.checkCooldown"), detail: "Dispatch omits updater metadata, opening/cooldown and error-download predicates." },
  { id: "SET-BL-004", page: "Settings", source: "Index.Rn updater actions", pass: pages.includes("updateBusy") && pages.includes("showUpdateDownload") && pages.includes("update.downloadAndOpen"), detail: "Dispatch reduces updater actions to disabled static buttons and hard-coded versions/progress." },
];

const failures = cases.filter((entry) => !entry.pass).map((entry) => entry.id);
const result = {
  workItem: "LWB317-UI-REMAINING-PAGES-001",
  pagesSha256: sha256(pages),
  contractsPresent: Boolean(contracts),
  cases,
  failures,
};
if (process.argv.includes("--record")) {
  fs.writeFileSync(path.join(here, "baseline-results.json"), `${JSON.stringify(result, null, 2)}\n`);
}
console.log(JSON.stringify(result, null, 2));
if (failures.length) process.exitCode = 1;
