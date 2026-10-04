import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { createOriginalHarness, optionsReply } from "../LWB317-UI-MAP-INTERACTIONS-001/original/common.mjs";
import { createHarness, treeNodes } from "../LWB317-UI-MAP-FILTER-LIFECYCLE-001/harness.mjs";
import crypto from "node:crypto";
import { loadPanel, parse, rawOf, walkAll } from "../LWB317-UI-MAP-INTERACTIONS-001/original/lib.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../..");
const panel = loadPanel();
const locator = (source, file, node, name) => {
  const text = source.slice(node.start, node.end);
  return { name, file, utf8ByteOffset: Buffer.byteLength(source.slice(0, node.start)), byteLength: Buffer.byteLength(text), sha256: crypto.createHash("sha256").update(text).digest("hex"), text };
};
const sourceLocators = [];
walkAll(panel.R, (node) => {
  if (node.type === "ArrowFunctionExpression" && node.body.type === "BlockStatement" && rawOf(panel.entry, node).includes("P.current=!0,qe(")) sourceLocators.push(locator(panel.entry.source, panel.entry.relative, node, "original Manual checkbox onChange"));
});
assert.equal(sourceLocators.length, 1);
const helperFile = "src/LWBridge.UI-0.3.17/src/mapBackend.js";
const helperSource = fs.readFileSync(path.join(repo, helperFile), "utf8");
for (const entry of parse(helperSource, { sourceType: "module" }).program.body) {
  const node = entry.declaration;
  if (["normalizeSelectedTypes", "updateSelectedTypes", "buildStartPayload"].includes(node?.id?.name)) sourceLocators.push(locator(helperSource, helperFile, node, node.id.name));
}
assert.equal(sourceLocators.length, 4);
const now = Date.UTC(2026, 9, 4, 6, 30);
const scan = { serverId: 321, isReading: false, phase: "idle", selectedTypes: ["city"], scanMode: "fast", progressPercent: 0, startedAt: 0, updatedAt: 0 };
const summary = { serverId: 321, counts: {}, scanState: scan };
const original = await createOriginalHarness({ online: true, now, scanState: scan, summary, stubs: { dataOptions: { mode: "auto", value: optionsReply() } } });
const current = await createHarness(fs.readFileSync(path.join(repo, "src/LWBridge.UI-0.3.17/src/MapDataPage.jsx"), "utf8"), "manual-selection-counter-evidence", {
  now, serverId: 321, dataOptions: optionsReply()(321),
  props: { bridgeMode: "native", backendAvailable: true, online: true, currentServerId: 321, scanState: scan, summary, onState: () => {}, onCounts: () => {}, activeTab: "city", onActiveTabChange: () => {}, previewState: "" },
});
try {
  await original.mount();
  await current.mount();
  const input = (h) => {
    const group = treeNodes(h.tree()).find((n) => n.props?.className === "map-types map-types--compact");
    return treeNodes(group).find((n) => n.type === "input");
  };
  const left = input(original);
  const right = input(current);
  const initial = { originalDisabled: left.props.disabled, currentDisabled: right.props.disabled };
  assert.deepEqual(initial, { originalDisabled: false, currentDisabled: true });
  // Direct inert callback invocation distinguishes the helper guard in addition
  // to the rendered disabled predicate. A browser user cannot click current.
  left.props.onChange({ target: { checked: false } });
  right.props.onChange({ target: { checked: false } });
  await original.settle();
  await current.settle();
  const after = { original: original.getState("Ke"), current: current.getState("selectedTypes") };
  assert.deepEqual(after, { original: [], current: ["city"] });
  const report = {
    marker: "LWB317_MANUAL_SELECTION_COUNTER_EVIDENCE_OK", initial, after, sourceLocators,
    controlsInvoked: ["original Manual city checkbox onChange", "current Manual city checkbox onChange directly despite disabled predicate"],
    nativeActionHandlersInvoked: [],
    limitation: "Inert persistent-hook execution; no mounted browser interaction or native Start/Stop/Clear action. This proves the current mismatch, not a corrected implementation.",
  };
  if (process.argv.includes("--record")) fs.writeFileSync(path.join(here, "selection-counter-evidence.json"), `${JSON.stringify(report, null, 2)}\n`);
  console.log(JSON.stringify(report));
} finally {
  await original.unmount();
  await current.unmount();
}
