import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { createHarness, nodeText, treeNodes } from "../LWB317-UI-MAP-REFRESH-FEEDBACK-001/harness.mjs";
import { DEFAULT_SCAN_STATE } from "../../../../src/LWBridge.UI-0.3.17/src/mapBackend.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../..");
const pagePath = path.join(repo, "src/LWBridge.UI-0.3.17/src/MapDataPage.jsx");
const source = fs.readFileSync(pagePath, "utf8");
const originalResults = JSON.parse(fs.readFileSync(path.join(here, "../LWB317-UI-MAP-CLOSEOUT-002/agent-b/case-results.json"), "utf8"));

const baseConfig = {
  enabled: true,
  intervalMinutes: 60,
  serverIds: [8],
  selectedTypes: ["truck", "railway", "dispatch", "ghost", "treasure"],
  scanMode: "fast",
  returnToOriginalServer: true,
  nextRunAt: 1_900_000_000_000,
};
const emitted = [];
const runningChanges = [];
const scanState = { ...DEFAULT_SCAN_STATE, serverId: 321, serverIdSource: "fixture", selectedTypes: [...DEFAULT_SCAN_STATE.selectedTypes] };
const h = await createHarness(source, "auto-controls-current", {
  now: 1_800_000_000_000,
  props: {
    previewState: "map-auto-scheduled",
    online: false,
    scanState,
    autoScanConfig: baseConfig,
    autoScanRunning: false,
    onAutoScanConfig: (value) => emitted.push(structuredClone(value)),
    onAutoScanRunningChange: (value) => runningChanges.push(value),
  },
});
await h.mount();

const results = [];
function record(name, detail) { results.push({ name, status: "PASS", detail }); }
function nodes(predicate) { return h.findNodes(predicate); }
function one(label, predicate) {
  const found = nodes(predicate);
  assert.equal(found.length, 1, `${label}: expected one node, got ${found.length}`);
  return found[0];
}
function targetInput() { return one("target server input", (node) => node.type === "input" && node.props?.inputMode === "numeric"); }
function addButton() { return one("Add button", (node) => node.type === "button" && nodeText(node) === "common.add"); }
function intervalInput() { return one("interval input", (node) => node.type === "input" && node.props?.type === "number" && node.props?.min === 20 && node.props?.max === "1440"); }
function speedSelect() { return one("Auto speed select", (node) => node.type === "select" && ["normal", "fast"].includes(node.props?.value) && treeNodes(node).some((child) => child.type === "option" && child.props?.value === "fast")); }
function masterLabel() { return one("Auto master", (node) => node.type === "label" && node.props?.className === "map-auto-scan-master"); }
function runNowButton() { return one("Run now", (node) => node.type === "button" && nodeText(node) === "map.runAutoScanNow"); }
function nextScanText() { return one("Next scan", (node) => node.type === "small" && nodeText(node).startsWith("map.nextAutoScan")); }
function labelInput(text) {
  const label = one(`label ${text}`, (node) => node.type === "label" && nodeText(node).includes(text));
  const input = treeNodes(label).find((node) => node.type === "input");
  assert.ok(input, `${text}: input`);
  return input;
}
function typeInput(labelKey) { return labelInput(labelKey); }

assert.equal(targetInput().props.placeholder, "321");
assert.equal(targetInput().props.inputMode, "numeric");
assert.equal(addButton().props.disabled, true);
record("target-input-contract", { placeholder: "321", inputMode: "numeric", emptyAddDisabled: true });

targetInput().props.onChange({ target: { value: "bad only" } });
await h.settle();
assert.equal(addButton().props.disabled, true);
let prevented = 0;
targetInput().props.onKeyDown({ key: "Enter", preventDefault: () => { prevented += 1; } });
await h.settle();
assert.equal(prevented, 1);
assert.equal(emitted.length, 0);
assert.equal(h.getState("autoServerInput"), "bad only");
record("invalid-enter-keeps-draft-and-does-not-emit", { prevented, draft: h.getState("autoServerInput") });

targetInput().props.onChange({ target: { value: "8 15；120,0x10" } });
await h.settle();
assert.equal(addButton().props.disabled, false);
prevented = 0;
targetInput().props.onKeyDown({ key: "Enter", preventDefault: () => { prevented += 1; } });
await h.settle();
assert.deepEqual(emitted.at(-1).serverIds, [8, 15, 120, 16]);
assert.equal(h.getState("autoServerInput"), "");
record("valid-enter-parses-appends-and-clears", { serverIds: emitted.at(-1).serverIds, prevented });

const twenty = Array.from({ length: 20 }, (_, index) => index + 1);
await h.setProps({ autoScanConfig: { ...baseConfig, serverIds: twenty } });
targetInput().props.onChange({ target: { value: "20；21" } });
await h.settle();
const beforeCapEmit = emitted.length;
addButton().props.onClick();
await h.settle();
assert.equal(emitted.length, beforeCapEmit + 1);
assert.deepEqual(emitted.at(-1).serverIds, twenty);
assert.equal(h.getState("autoServerInput"), "");
record("valid-add-at-cap-still-emits-and-clears", { count: emitted.at(-1).serverIds.length });

await h.setProps({ autoScanConfig: { ...baseConfig, serverIds: [15, 8, 120] } });
const chips = one("server chips", (node) => node.type === "span" && node.props?.className === "map-auto-scan-server-chips");
const chipButtons = treeNodes(chips).filter((node) => node.type === "button");
assert.deepEqual(chipButtons.map((node) => node.props["aria-label"]), ["common.remove 15", "common.remove 8", "common.remove 120"]);
chipButtons[1].props.onClick();
assert.deepEqual(emitted.at(-1).serverIds, [15, 120]);
record("chips-preserve-order-and-remove-one", { labels: chipButtons.map((node) => node.props["aria-label"]), remaining: [15, 120] });

await h.setProps({ autoScanConfig: baseConfig, autoScanRunning: true });
targetInput().props.onChange({ target: { value: "99" } });
await h.settle();
assert.notEqual(targetInput().props.disabled, true);
assert.equal(addButton().props.disabled, false);
assert.notEqual(intervalInput().props.disabled, true);
assert.notEqual(speedSelect().props.disabled, true);
assert.notEqual(labelInput("map.returnAfterAutoScan").props.disabled, true);
for (const labelKey of ["map.playerCity", "map.resourcePoint", "map.monster", "map.truck", "map.allianceTrain", "map.secretTask", "map.ghostScout", "map.treasure"]) {
  assert.notEqual(typeInput(labelKey).props.disabled, true, `${labelKey} remains editable while running`);
}
assert.ok(nodeText(masterLabel()).includes("map.autoScanRunning"));
assert.equal(runNowButton().props.disabled, true);
record("running-status-wins-and-config-remains-editable", { runningText: true, runNowDisabled: true });

const beforeInterval = emitted.length;
intervalInput().props.onChange({ target: { value: "19" } });
assert.equal(emitted.length, beforeInterval + 1);
assert.equal(emitted.at(-1).intervalMinutes, 19);
speedSelect().props.onChange({ target: { value: "unexpected" } });
assert.equal(emitted.at(-1).scanMode, "normal");
record("interval-and-speed-emit-raw-panel-values", { intervalMinutes: 19, fallbackSpeed: "normal" });

await h.setProps({ autoScanConfig: { ...baseConfig, selectedTypes: ["city"] }, autoScanRunning: false });
assert.equal(typeInput("map.playerCity").props.disabled, true);
assert.equal(typeInput("map.resourcePoint").props.disabled, false);
typeInput("map.resourcePoint").props.onChange({ target: { checked: true } });
assert.deepEqual(emitted.at(-1).selectedTypes, ["city", "resource"]);
await h.setProps({ autoScanConfig: { ...baseConfig, selectedTypes: ["city", "resource"] } });
typeInput("map.playerCity").props.onChange({ target: { checked: false } });
assert.deepEqual(emitted.at(-1).selectedTypes, ["resource"]);
record("all-eight-types-and-sole-selection-guard", { rendered: 8, append: ["city", "resource"], remove: ["resource"] });

await h.setProps({ autoScanConfig: baseConfig, autoScanRunning: false, online: true, scanState: { ...scanState, isReading: false } });
assert.equal(runNowButton().props.disabled, false);
const beforeRunNow = emitted.length;
runNowButton().props.onClick();
assert.equal(emitted.length, beforeRunNow + 1);
assert.equal(emitted.at(-1).nextRunAt, h.now());
assert.equal(runNowButton().props.className, "primary");
await h.setProps({ online: false });
assert.equal(runNowButton().props.disabled, true);
await h.setProps({ online: true, autoScanRunning: true });
assert.equal(runNowButton().props.disabled, true);
await h.setProps({ autoScanRunning: false, scanState: { ...scanState, isReading: true } });
assert.equal(runNowButton().props.disabled, true);
record("run-now-exact-gate-and-deadline-edit", { onlineEnabled: true, runningDisabled: true, readingDisabled: true, nextRunAt: h.now() });

await h.setProps({ scanState: { ...scanState, isReading: false }, autoScanConfig: { ...baseConfig, enabled: false, nextRunAt: 1_800_000_123_000 } });
assert.ok(nodeText(nextScanText()).endsWith("-"));
await h.setProps({ autoScanConfig: { ...baseConfig, enabled: true, nextRunAt: 1_800_000_123_000 } });
assert.ok(!nodeText(nextScanText()).endsWith("-"));
record("next-scan-display-requires-enabled-and-positive-deadline", { disabled: "-", enabled: nodeText(nextScanText()) });

assert.ok(!source.includes("autoScanReset"));
record("no-reset-control-token", { present: false });
assert.equal(originalResults.failed, 0);
assert.equal(originalResults.passed, originalResults.cases.length);
assert.ok(originalResults.cases.every((entry) => entry.status === "PASS"));

const report = {
  status: "PASS",
  scope: "Actual production MapDataPage body and rendered Auto controls in the persistent hook adapter; parent normalization/persistence is tested separately. Native Auto execution is not invoked.",
  originalAgentB: {
    path: "evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-CLOSEOUT-002/agent-b/case-results.json",
    status: originalResults.failed === 0 ? "PASS" : "FAIL",
    cases: originalResults.cases?.length ?? null,
  },
  currentCases: results,
};
fs.writeFileSync(path.join(here, "control-results.json"), `${JSON.stringify(report, null, 2)}\n`);
console.log(`PASS ${results.length}/${results.length} current Auto control cases; original agent-b ${originalResults.passed}/${originalResults.cases?.length ?? "?"} PASS`);
await h.unmount();
