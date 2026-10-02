import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import {
  createHarness,
  deferred,
  repo,
} from "../../LWB317-UI-MAP-REFRESH-FEEDBACK-001/harness.mjs";
import {
  DEFAULT_SCAN_STATE,
  EMPTY_COUNTS,
  MAP_KIND_KEYS,
} from "../../../../../src/LWBridge.UI-0.3.17/src/mapBackend.js";
import {
  createAppHarness,
  mapSummary,
} from "./app-harness.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const pagePath = path.join(repo, "src/LWBridge.UI-0.3.17/src/MapDataPage.jsx");
const appPath = path.join(repo, "src/LWBridge.UI-0.3.17/src/App.jsx");
const originalPanelPath = path.join(
  repo,
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/reference/MapDataPanel.pretty.js",
);
const originalIndexPath = path.join(
  repo,
  "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js",
);
const pageSource = fs.readFileSync(pagePath, "utf8");
const appSource = fs.readFileSync(appPath, "utf8");
const originalPanel = fs.readFileSync(originalPanelPath, "utf8");
const originalIndex = fs.readFileSync(originalIndexPath, "utf8");
const baselinePage = execFileSync(
  "git",
  ["show", "123459d:src/LWBridge.UI-0.3.17/src/MapDataPage.jsx"],
  { cwd: repo, encoding: "utf8" },
);

const sha256 = (value) => crypto.createHash("sha256").update(value).digest("hex");
const normalizedHash = (value) => sha256(value.replace(/\r\n/g, "\n"));
const counts = (value) => Object.fromEntries(MAP_KIND_KEYS.map((kind) => [kind, value]));
const scan = (patch = {}) => ({
  ...DEFAULT_SCAN_STATE,
  serverId: 321,
  serverIdSource: "fixture",
  selectedTypes: [...MAP_KIND_KEYS],
  scanMode: "normal",
  ...patch,
});
const summary = (state, count = 1) => ({ serverId: state.serverId, counts: counts(count), scanState: state });
const options = (serverId, count = 2) => ({
  serverId,
  counts: counts(count),
  alliances: [],
  names: { resource: [], monster: [] },
  dispatchLevels: [],
  rewardItems: { truck: [], railway: [] },
  treasureTypes: [],
  noAllianceCount: 0,
});

// Exact recovered-source anchors. Behavioral assertions below execute the current
// App and panel; these anchors keep the expected ownership tied to 0.3.17.
assert.match(originalPanel, /function _e\(e2\) \{\s*return \{ rows: true, options: e2 === `scan-complete` \};/);
assert.match(originalPanel, /\}, \[R2, vn\]\), \(0, y\.useEffect\)/);
assert.match(originalPanel, /ke2\.current\(t2\.serverId, t2\.counts\)/);
assert.match(originalIndex, /async function _t\(e=r\.selectedProfileId\)/);
assert.match(originalIndex, /if\(!at\)return;let e=!1,t=async\(\)=>/);
assert.match(originalIndex, /window\.setInterval\(\(\)=>\{t\(\)\},5e3\)/);
assert.match(baselinePage, /await loadOptions\(summary\.serverId\)/);

// The immutable 123459d baseline is the ownership counterexample: it bootstraps
// summary/listener work inside MapDataPage and nests options under summary.
let baselineSummaryCalls = 0;
let baselineListenerCalls = 0;
const baselineOptionServers = [];
const baseline = await createHarness(baselinePage, "refresh-ownership-baseline-123459d", {
  serverId: 321,
  props: { previewState: "map-city", online: true, currentServerId: 321 },
  apiExtensions: () => ({
    summary: async () => {
      baselineSummaryCalls += 1;
      return summary(scan(), 1);
    },
    listenScanStatus: () => {
      baselineListenerCalls += 1;
      return () => {};
    },
    dataOptions: async (serverId) => {
      baselineOptionServers.push(serverId);
      return options(serverId, 1);
    },
  }),
});
await baseline.mount();
assert.ok(baselineSummaryCalls >= 1, "baseline must demonstrate child summary ownership");
assert.ok(baselineListenerCalls >= 1, "baseline must demonstrate child scan-listener ownership");
await baseline.unmount();

// Execute the actual parent App lifecycle. Bootstrap and the connected poll may
// overlap, but the poll itself is guarded. A newer producer owns the summary.
const app = await createAppHarness(appSource, "refresh-ownership-app");
await app.mount();
assert.equal(app.summaryRequests.length, 2, "selected-profile bootstrap plus connected immediate poll");
const mountBootstrap = app.summaryRequests[0];
const connectedPoll = app.summaryRequests[1];
assert.equal(mountBootstrap.profileId, "profile-a");
assert.equal(connectedPoll.profileId, "profile-a");

await app.advance(5000);
assert.equal(app.summaryRequests.length, 2, "five-second poll must not overlap its unresolved predecessor");
await app.resolveSummary(connectedPoll, mapSummary(321, 2));
assert.equal(app.getState("mapSummary").counts.city, 2);
await app.resolveSummary(mountBootstrap, mapSummary(321, 1));
assert.equal(app.getState("mapSummary").counts.city, 2, "obsolete bootstrap reply must not replace the newer summary owner");

await app.advance(5000);
assert.equal(app.summaryRequests.length, 3, "resolved poll rearms the next five-second summary request");
const offlinePending = app.summaryRequests[2];
await app.emitStatus({ xluaOnline: false });
await app.resolveSummary(offlinePending, mapSummary(321, 3));
assert.equal(app.getState("mapSummary").counts.city, 3, "same-profile in-flight poll may settle after connected-effect cleanup, matching the original producer");
await app.advance(5000);
assert.equal(app.summaryRequests.length, 3, "offline state has no summary poll");

await app.emitStatus({ xluaOnline: true });
assert.equal(app.summaryRequests.length, 4, "reconnection starts one immediate summary poll");
await app.resolveSummary(app.summaryRequests[3], mapSummary(321, 4));

const reading = scan({ isReading: true, readBlocks: 1, scanRunId: "run-1" });
await app.emitScan(reading);
assert.equal(app.summaryRequests.length, 4, "scan progress acknowledgement does not fetch parent summary");
assert.equal(app.pageProps().scanState.isReading, true);
const stopped = scan({ isReading: false, readBlocks: 2, scanRunId: "run-1" });
await app.emitScan(stopped);
assert.equal(app.summaryRequests.length, 5, "reading-to-stopped acknowledgement triggers one completion summary");
await app.resolveSummary(app.summaryRequests[4], summary(stopped, 5));
assert.equal(app.getState("mapSummary").counts.city, 5);

const parentBoundary = app.pageProps();
parentBoundary.onCounts(321, counts(7));
await app.settle();
assert.equal(app.getState("mapSummary").counts.city, 7, "panel count acknowledgement updates the parent-owned matching summary");

// Keep one old-profile poll pending while the selected profile changes. The new
// profile bootstrap/immediate poll become the owner; both old-profile and older
// same-profile replies are generation-fenced.
await app.advance(5000);
assert.equal(app.summaryRequests.length, 6);
const oldProfilePending = app.summaryRequests[5];
await app.setProfileId("profile-b");
assert.equal(app.getState("mapSummary"), null, "profile replacement clears the prior summary before bootstrap settles");
assert.equal(app.summaryRequests.length, 8, "new profile owns bootstrap plus connected immediate poll");
const profileBootstrap = app.summaryRequests[6];
const profilePoll = app.summaryRequests[7];
assert.equal(profileBootstrap.profileId, "profile-b");
assert.equal(profilePoll.profileId, "profile-b");
await app.resolveSummary(profilePoll, mapSummary(555, 8));
assert.equal(app.getState("mapSummary").serverId, 555);
await app.resolveSummary(oldProfilePending, mapSummary(321, 99));
assert.equal(app.getState("mapSummary").serverId, 555, "old profile reply cannot overwrite the replacement profile");
await app.resolveSummary(profileBootstrap, mapSummary(555, 6));
assert.equal(app.getState("mapSummary").counts.city, 8, "older replacement-profile bootstrap cannot overwrite its newer poll");

await app.advance(5000);
const rejectionAfterCleanup = app.summaryRequests.at(-1);
await app.emitStatus({ xluaOnline: false });
await app.rejectSummary(rejectionAfterCleanup, new Error("fixture summary failure after cleanup"));
assert.equal(app.getState("mapSummary").counts.city, 8, "deferred rejection after poll cleanup preserves current parent state");

await app.emitStatus({ xluaOnline: true });
const unmountPending = app.summaryRequests.at(-1);
const beforeUnmount = structuredClone(app.getState("mapSummary"));
await app.unmount();
await app.resolveSummary(unmountPending, mapSummary(777, 77));
assert.deepEqual(app.getState("mapSummary"), beforeUnmount, "deferred success after App cleanup cannot replace unmounted owner state");
const listenerCounts = app.listenerCounts();
assert.ok(listenerCounts.statusSubscribes >= 2 && listenerCounts.scanSubscribes >= 2, "profile replacement must reinstall profile-owned listeners");
assert.equal(listenerCounts.statusSubscribes, listenerCounts.statusUnsubscribes, "status listeners disposed on cleanup");
assert.equal(listenerCounts.scanSubscribes, listenerCounts.scanUnsubscribes, "scan listeners disposed on cleanup");

// Execute the panel in its controlled parent-supplied shape with the corrected
// interval+timeout runtime. Summary polling/listening stay at the parent; options
// stay at [dataServer, optionsRevision], and progress refreshes rows only.
let childSummaryCalls = 0;
let childListenerCalls = 0;
const optionServers = [];
const parentStates = [];
const parentCounts = [];
const initialScan = scan();
const initialSummary = summary(initialScan, 1);
const panel = await createHarness(pageSource, "closeout-refresh-ownership", {
  serverId: 321,
  props: {
    previewState: "map-city",
    online: true,
    currentServerId: 321,
    scanState: initialScan,
    summary: initialSummary,
    onState: (value) => parentStates.push(structuredClone(value)),
    onCounts: (serverId, value) => parentCounts.push({ serverId, counts: structuredClone(value) }),
  },
  apiExtensions: () => ({
    summary: async () => {
      childSummaryCalls += 1;
      return initialSummary;
    },
    listenScanStatus: () => {
      childListenerCalls += 1;
      return () => {};
    },
    dataOptions: async (serverId) => {
      optionServers.push(serverId);
      return options(serverId, 2);
    },
  }),
});
await panel.mount();
assert.equal(childSummaryCalls, 0, "controlled Map page must not own summary bootstrap/poll");
assert.equal(childListenerCalls, 0, "controlled Map page must not install a second scan listener");
assert.deepEqual(optionServers, [321], "data server owns one options bootstrap request");
assert.equal(parentCounts.at(-1)?.serverId, 321);

await panel.advance(5000);
assert.equal(childSummaryCalls, 0, "five-second child timers must not poll summary");
assert.deepEqual(optionServers, [321], "five-second parent summary cadence must not reload panel options");

await panel.setProps({ summary: summary(initialScan, 9) });
assert.deepEqual(optionServers, [321], "summary-only update must not reload options");
assert.equal(panel.getState("counts").city, 9);

const progress = scan({ isReading: true, readBlocks: 1, scanRunId: "run-panel" });
await panel.setProps({ scanState: progress, summary: summary(progress, 9) });
const progressSearchBefore = panel.requestCount();
await panel.advance(999);
assert.equal(panel.requestCount(), progressSearchBefore, "progress trailing row refresh waits one second");
await panel.advance(1);
assert.equal(panel.requestCount(), progressSearchBefore + 1, "progress timeout refreshes rows once");
assert.deepEqual(optionServers, [321], "scan progress timeout does not reload options");

const completed = scan({ isReading: false, readBlocks: 2, scanRunId: "run-panel" });
await panel.setProps({ scanState: completed, summary: summary(completed, 9) });
assert.deepEqual(optionServers, [321, 321], "scan completion advances the local options revision exactly once");

// The recovered summary-count effect clears local count readiness when parent
// summary ownership is reset (for example during profile replacement).
await panel.setProps({ scanState: { ...DEFAULT_SCAN_STATE }, summary: null });
assert.deepEqual(panel.getState("counts"), { ...EMPTY_COUNTS }, "missing parent summary clears panel counts");
assert.equal(panel.getState("summaryReady"), false, "missing parent summary clears count readiness");
await panel.unmount();

// Mount-already-reading must still treat the first later false edge as one scan
// completion, without inventing an options refresh on mount.
const readingOptionServers = [];
const mountedReading = scan({ isReading: true, readBlocks: 4, scanRunId: "already-reading" });
const readingPanel = await createHarness(pageSource, "closeout-mount-already-reading", {
  serverId: 321,
  props: {
    previewState: "map-city",
    online: true,
    currentServerId: 321,
    scanState: mountedReading,
    summary: summary(mountedReading, 4),
    onState: () => {},
    onCounts: () => {},
  },
  apiExtensions: () => ({
    dataOptions: async (serverId) => {
      readingOptionServers.push(serverId);
      return options(serverId, 4);
    },
  }),
});
await readingPanel.mount();
assert.deepEqual(readingOptionServers, [321]);
await readingPanel.setProps({
  scanState: scan({ isReading: false, readBlocks: 5, scanRunId: "already-reading" }),
});
assert.deepEqual(readingOptionServers, [321, 321], "mount-already-reading completion advances options once");
await readingPanel.unmount();

// Name invalidation belongs to the options refresh boundary, not the five-second
// parent summary poll. A completion-driven options refresh drops a selection that
// the refreshed provider no longer offers.
let resourceOptions = [{ key: "100282", count: 3 }, { key: "100281", count: 2 }];
const namePanel = await createHarness(pageSource, "closeout-option-name-invalidation", {
  serverId: 321,
  props: {
    previewState: "map-resource",
    online: true,
    currentServerId: 321,
    scanState: initialScan,
    summary: initialSummary,
    onState: () => {},
    onCounts: () => {},
  },
  apiExtensions: () => ({
    dataOptions: async (serverId) => ({ ...options(serverId, 2), names: { resource: resourceOptions, monster: [] } }),
  }),
});
await namePanel.mount();
const nameSelect = namePanel.findNodes((node) => node.type === "select" && node.props?.["aria-label"] === "common.name")[0];
assert.ok(nameSelect, "resource name select");
nameSelect.props.onChange({ target: { value: "100282" } });
await namePanel.settle();
assert.equal(namePanel.getState("resourceNameKey"), "100282");
resourceOptions = [{ key: "100281", count: 2 }];
await namePanel.setProps({ scanState: scan({ isReading: true, readBlocks: 1, scanRunId: "name-refresh" }) });
await namePanel.setProps({ scanState: scan({ isReading: false, readBlocks: 2, scanRunId: "name-refresh" }) });
assert.equal(namePanel.getState("resourceNameKey"), "", "completion options refresh clears removed Resource selection");
await namePanel.unmount();

// Provider replacement starts a new options generation. A deferred reply from
// the replaced provider cannot overwrite the new provider's server/count owner.
const oldOptions = deferred();
const providerPanel = await createHarness(pageSource, "closeout-provider-replacement", {
  serverId: 321,
  props: {
    previewState: "map-city",
    online: true,
    currentServerId: 321,
    scanState: initialScan,
    summary: initialSummary,
    onState: () => {},
    onCounts: () => {},
  },
  apiExtensions: () => ({ dataOptions: () => oldOptions.promise }),
});
await providerPanel.mount();
const replacementApi = {
  ...providerPanel.api,
  dataOptions: async (serverId) => options(serverId, 22),
};
await providerPanel.setProps({ mapApi: replacementApi });
assert.equal(providerPanel.getState("counts").city, 22);
oldOptions.resolve(options(321, 11));
await providerPanel.settle();
assert.equal(providerPanel.getState("counts").city, 22, "obsolete provider options reply cannot overwrite replacement provider");
await providerPanel.unmount();

const result = {
  status: "PASS",
  source: {
    originalPanelSha256: sha256(originalPanel),
    originalIndexSha256: sha256(originalIndex),
    currentPageSha256: normalizedHash(pageSource),
    currentAppSha256: normalizedHash(appSource),
    baselineCommit: "123459d",
  },
  baselineCounterexample: {
    status: "EXPECTED_FAIL_OWNERSHIP",
    summaryCalls: baselineSummaryCalls,
    scanListenerCalls: baselineListenerCalls,
    optionServers: baselineOptionServers,
    nestedSummaryOptions: true,
  },
  parentLifecycle: {
    mountSummaryRequests: 2,
    pollOverlapSuppressed: true,
    sameProfileOfflineSettlement: true,
    completionSummaryRefresh: true,
    profileReplacementFenced: true,
    deferredRejectionPreservedState: true,
    deferredUnmountSuccessFenced: true,
    listenerCounts,
  },
  controlledBoundary: {
    summaryCalls: childSummaryCalls,
    scanListenerCalls: childListenerCalls,
    optionServers,
    parentStateCallbacks: parentStates.length,
    parentCountCallbacks: parentCounts.length,
    progressTimeoutRowsOnly: true,
    mountAlreadyReadingOptions: readingOptionServers,
    completionOptionNameInvalidation: true,
    providerReplacementFenced: true,
    emptyCountsShape: EMPTY_COUNTS,
  },
};

fs.mkdirSync(here, { recursive: true });
fs.writeFileSync(path.join(here, "refresh-ownership-results.json"), `${JSON.stringify(result, null, 2)}\n`);
JSON.parse(fs.readFileSync(path.join(here, "refresh-ownership-results.json"), "utf8"));
console.log(
  `LWB317_MAP_CLOSEOUT_REFRESH_OWNERSHIP_OK parent=${result.parentLifecycle.mountSummaryRequests} childSummary=${childSummaryCalls} childListeners=${childListenerCalls} options=${optionServers.join(",")}`,
);
