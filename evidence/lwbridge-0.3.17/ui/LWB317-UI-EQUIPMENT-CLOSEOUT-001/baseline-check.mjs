import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../..");
const pagesPath = path.join(repo, "src/LWBridge.UI-0.3.17/src/Pages.jsx");
const sourcePath = path.join(repo, ".scratch-lwb317/SquadPanel.pretty.js");
const indexPath = path.join(repo, "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js");
const pages = fs.readFileSync(pagesPath, "utf8");
const source = fs.readFileSync(sourcePath, "utf8");
const indexSource = fs.readFileSync(indexPath, "utf8");

const cases = [
  {
    id: "EQ-BL-001",
    source: "SquadPanel.pretty.js:4060-4069",
    pass: pages.includes("currentEquipmentPresetMatches") || pages.includes("equipmentSquadMatches"),
    detail: "Current clone hard-codes unmatched current-equipment labels instead of computing source-derived matches."
  },
  {
    id: "EQ-BL-002",
    source: "SquadPanel.pretty.js:4060,4237-4254",
    pass: !pages.includes("Hero {squad.squadIndex}-{position.position}"),
    detail: "Current clone renders synthetic hero labels/levels from preset positions rather than current squad hero inputs."
  },
  {
    id: "EQ-BL-003",
    source: "SquadPanel.pretty.js:4060,4236-4255",
    pass: pages.includes("availableSquadIndexes") || pages.includes("liveSquads"),
    detail: "Current clone drives squad/position rendering from preset data instead of current squad inputs."
  },
  {
    id: "EQ-BL-004",
    source: "index-BVfnK1wp.js shared dialog In; SquadPanel.pretty.js:4256-4258",
    pass: !pages.includes("event.target === event.currentTarget) closeRename()"),
    detail: "Current rename dialog dismisses on backdrop; original Equipment dialog leaves dismissOnBackdrop false."
  },
  {
    id: "EQ-BL-005",
    source: "SquadPanel.pretty.js:4060,4133-4145",
    pass: !pages.includes("setDirtyPresetIds") && pages.includes("confirmedPresets"),
    detail: "Current clone tracks dirty IDs manually instead of deriving JSON differences from draft vs confirmed state."
  },
  {
    id: "EQ-BL-006",
    source: "SquadPanel.pretty.js:4176-4187,4248",
    pass: !pages.includes("markDropSuccess([`squad-${dragged.squadIndex}`"),
    detail: "Current squad-loadout success state targets squad cards; original highlights each swapped position."
  },
  {
    id: "EQ-BL-007",
    source: "SquadPanel.pretty.js:4110-4118",
    pass: /addEventListener\(["`]keydown["`]/.test(pages.slice(pages.indexOf("function EquipmentContent"), pages.indexOf("const CITY_PREVIEW_BUILDINGS"))),
    detail: "Current Equipment component displays Alt+1-4 copy but has no matching keydown handler."
  },
  {
    id: "EQ-BL-008",
    source: "SquadPanel.pretty.js:4260-4285",
    pass: pages.includes("visitedTabs") || pages.includes("mountedTabs"),
    detail: "Current Squads page mounts both AFK and Equipment immediately instead of mounting each tab on first visit and retaining it."
  },
  {
    id: "EQ-BL-009",
    source: "SquadPanel.pretty.js:4153-4171,4236-4246",
    pass: pages.includes("equipmentApplying") && pages.includes("busyKey"),
    detail: "Current progress fixture does not drive original busy/disabled/applying presentation."
  }
];

if (!indexSource.includes("dismissOnBackdrop:o=!1") || !source.includes("j2 = (f2?.equipmentPresets ?? []).filter")) {
  throw new Error("Recovered Equipment/source dialog oracle changed; re-locate baseline anchors.");
}

const failures = cases.filter((entry) => !entry.pass);
console.log(JSON.stringify({ pagesPath, sourcePath, indexPath, cases, failures: failures.map((entry) => entry.id) }, null, 2));
if (failures.length) process.exitCode = 1;
