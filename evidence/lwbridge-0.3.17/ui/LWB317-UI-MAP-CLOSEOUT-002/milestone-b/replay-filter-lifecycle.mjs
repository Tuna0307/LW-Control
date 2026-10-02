import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import {
  createHarness,
  deferred,
  nodeText,
  repo,
  tick,
} from "../../LWB317-UI-MAP-FILTER-LIFECYCLE-001/harness.mjs";
import {
  DEFAULT_SCAN_STATE,
  MAP_KIND_KEYS,
} from "../../../../../src/LWBridge.UI-0.3.17/src/mapBackend.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const pagePath = path.join(repo, "src/LWBridge.UI-0.3.17/src/MapDataPage.jsx");
const protectedResult = path.join(
  repo,
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-FILTER-LIFECYCLE-001/filter-lifecycle-results.json",
);
const protectedBefore = fs.readFileSync(protectedResult);
const source = fs.readFileSync(pagePath, "utf8");

function emptyOptions(serverId) {
  return {
    serverId,
    counts: Object.fromEntries(MAP_KIND_KEYS.map((kind) => [kind, 0])),
    alliances: [],
    names: { resource: [], monster: [] },
    dispatchLevels: [],
    rewardItems: { truck: [], railway: [] },
    treasureTypes: [],
    noAllianceCount: 0,
    scanProgress: null,
  };
}

function scanState(serverId) {
  return {
    ...DEFAULT_SCAN_STATE,
    serverId,
    serverIdSource: serverId > 0 ? "fixture" : "none",
    selectedTypes: [...MAP_KIND_KEYS],
  };
}

function button(h, text) {
  const node = h.findNodes((candidate) => candidate.type === "button" && nodeText(candidate) === text)[0];
  assert.ok(node, h.label + ": button " + text);
  return node;
}

const h = await createHarness(source, "closeout-delayed-clear", {
  serverId: 321,
  props: { previewState: "map-city", online: true, currentServerId: 321 },
  dataOptions: emptyOptions(321),
});
await h.mount();

const optionCalls = [];
h.api.dataOptions = async (serverId) => {
  optionCalls.push(serverId);
  return emptyOptions(serverId);
};
const clearCall = deferred();
h.api.clear = () => clearCall.promise;

button(h, "map.clearServer").props.onClick();
await h.settle();
await h.emitServer(322);
clearCall.resolve(scanState(321));
await tick();
await h.settle();

const trace = {
  scanServerId: h.getState("scanState").serverId,
  browseServerId: h.getState("browseServerId"),
  optionCalls,
};
assert.deepEqual(trace, {
  scanServerId: 321,
  browseServerId: 321,
  optionCalls: [322, 321],
});
assert.deepEqual(fs.readFileSync(protectedResult), protectedBefore, "protected historical result changed");
await h.unmount();

fs.writeFileSync(path.join(here, "delayed-clear-current-results.json"), JSON.stringify({ status: "PASS", trace }, null, 2) + "\n");
console.log("LWB317_MAP_CLOSEOUT_DELAYED_CLEAR_OK options=322,321");
