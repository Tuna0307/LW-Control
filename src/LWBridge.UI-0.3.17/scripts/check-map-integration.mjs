import assert from "node:assert/strict";
import fs from "node:fs";
import { createBackendBridge } from "../src/backendBridge.js";
import { getMapPreviewProvider } from "../src/mapPreviewApi.js";
import {
  MAP_COMMANDS,
  MAP_KIND_KEYS,
  MAP_PAGE_SIZE,
  MAP_SCAN_TYPES,
  buildSearchPayload,
  buildStartPayload,
  connectionState,
  createMapApi,
  cycleSort,
  mapPageCount,
  normalizeScanState,
  normalizeSearchResult,
  normalizeSummary,
  updateSelectedTypes,
} from "../src/mapBackend.js";

assert.deepEqual(
  MAP_SCAN_TYPES.map(({ key }) => key),
  ["city", "resource", "monster", "truck", "railway", "dispatch", "ghost", "treasure"],
  "scan type mapping must remain the recovered eight-kind contract",
);

const mapPageSource = fs.readFileSync(new URL("../src/MapDataPage.jsx", import.meta.url), "utf8");
assert.match(mapPageSource, /localStorage\.getItem\("lwbridge\.mapScanMode"\)/,
  "canonical Map UI must restore the exact recovered persisted Manual scan-mode key");
assert.match(mapPageSource, /localStorage\.setItem\("lwbridge\.mapScanMode", speed\)/,
  "canonical Map UI must persist Manual scan mode with the exact recovered key");
for (const unsupportedKey of ["selectedTypes", "browseServer", "resultTab", "scanTab"]) {
  assert.doesNotMatch(mapPageSource, new RegExp(`localStorage\\.(?:getItem|setItem)\\([^\\n]*${unsupportedKey}`),
    `canonical Map UI must not invent recovered persistence for ${unsupportedKey}`);
}
assert.match(mapPageSource, /lwbridge\.mapAutoScan\.\$\{mapApi\.profileId \|\| "default"\}/,
  "canonical Auto Scan must use the recovered per-profile local-storage key");
assert.match(mapPageSource, /const AUTO_DEFAULT_TYPES = \["truck", "railway", "dispatch", "ghost", "treasure"\]/,
  "canonical Auto Scan must preserve recovered default selected types");
assert.match(mapPageSource, /interval >= 20 && interval <= 1440 \? interval : 60/,
  "canonical Auto Scan must preserve the recovered 20..1440 minute interval contract");
assert.match(mapPageSource, /window\.setInterval\(tick, 5000\)/,
  "canonical Auto Scan scheduler must preserve the recovered five-second due check");
assert.match(mapPageSource, /2_700_000/,
  "canonical Auto Scan must preserve the recovered 45-minute scan timeout");
assert.deepEqual(
  MAP_SCAN_TYPES.map(({ label }) => label),
  ["Player City", "Resource Point", "Monster", "Truck", "Train", "Secret Task", "Ghost Ops", "Treasure"],
  "scan labels must remain source-faithful",
);

assert.deepEqual(buildStartPayload(["resource", "resource", "bogus"], "fast"), {
  selectedTypes: ["resource"],
  scanMode: "fast",
});
assert.deepEqual(updateSelectedTypes(["resource"], "resource", false), ["resource"], "last scan type cannot be removed");
assert.deepEqual(updateSelectedTypes(["city"], "resource", true), ["city", "resource"]);

assert.deepEqual(cycleSort([{ sortBy: "updatedAt", sortOrder: "desc" }], "level"), [
  { sortBy: "level", sortOrder: "desc" },
  { sortBy: "updatedAt", sortOrder: "desc" },
]);
assert.deepEqual(cycleSort([{ sortBy: "level", sortOrder: "desc" }], "level"), [{ sortBy: "level", sortOrder: "asc" }]);
assert.deepEqual(cycleSort([{ sortBy: "level", sortOrder: "asc" }], "level"), []);

const resourceQuery = buildSearchPayload("resource", {
  serverId: 7,
  keyword: "iron",
  resourceNameKey: "100281",
  page: 3,
  sorts: [{ sortBy: "level", sortOrder: "asc" }, { sortBy: "updatedAt", sortOrder: "desc" }],
});
assert.deepEqual(resourceQuery, {
  kind: "resource",
  query: {
    serverId: 7,
    keyword: "iron",
    page: 3,
    pageSize: MAP_PAGE_SIZE,
    sorts: [{ sortBy: "level", sortOrder: "asc" }, { sortBy: "updatedAt", sortOrder: "desc" }],
    resourceNameKey: "100281",
  },
});
assert.equal(mapPageCount(0), 1);
assert.equal(mapPageCount(50), 1);
assert.equal(mapPageCount(51), 2);
assert.equal(mapPageCount(8007), 161);
assert.deepEqual(normalizeSearchResult({ rows: [{ recordKey: "r1" }], total: 51 }), { rows: [{ recordKey: "r1" }], total: 51 });

const normalizedScan = normalizeScanState({ serverId: 9, isReading: true, readBlocks: 12, totalBlocks: 20, scanMode: "fast", progressPercent: 60 });
assert.equal(normalizedScan.serverId, 9);
assert.equal(normalizedScan.isReading, true);
assert.equal(normalizedScan.readBlocks, 12);
assert.equal(normalizedScan.completedBlocks, 12);
assert.equal(normalizedScan.concurrency, 20);
assert.deepEqual(normalizeScanState({}).selectedTypes, MAP_KIND_KEYS);

const normalizedSummary = normalizeSummary({
  serverId: 9,
  counts: { resource: 321 },
  scanState: { serverId: 9, phase: "completed" },
});
assert.equal(normalizedSummary.counts.resource, 321);
assert.equal(normalizedSummary.counts.city, 0);
assert.equal(normalizedSummary.scanState.phase, "completed");

assert.equal(connectionState(null, null, "preview"), "preview");
assert.equal(connectionState(null, null, "native-unavailable"), "unavailable");
assert.equal(connectionState({}, { gameRunning: false }, "native"), "stopped");
assert.equal(connectionState({}, { gameRunning: true }, "native"), "checking");
assert.equal(connectionState({ xluaOnline: false }, { gameRunning: true }, "native"), "disconnected");
assert.equal(connectionState({ xluaOnline: true }, { gameRunning: true }, "native"), "connected");

const calls = [];
const fakeBridge = {
  profileId: "profile-test",
  invoke: async (command, payload) => {
    calls.push({ command, payload });
    if (command === MAP_COMMANDS.scanStart) return { serverId: 7, isReading: true, selectedTypes: payload.selectedTypes, scanMode: payload.scanMode };
    if (command === MAP_COMMANDS.scanStop) return { serverId: 7, isReading: false, phase: "idle" };
    if (command === MAP_COMMANDS.scanClear) return { serverId: 0, isReading: false, phase: "idle" };
    if (command === MAP_COMMANDS.scanStatus) return { serverId: 7, phase: "completed" };
    if (command === MAP_COMMANDS.summary) return { serverId: 7, counts: { resource: 2 }, scanState: { serverId: 7 } };
    if (command === MAP_COMMANDS.dataOptions) return { serverId: 7, counts: { resource: 2 }, names: { resource: [{ key: "100281", count: 2 }] } };
    if (command === MAP_COMMANDS.search) return { rows: [{ recordKey: "r1" }], total: 1 };
    return {};
  },
  listen: () => () => {},
};
const api = createMapApi(fakeBridge);
await api.start(["resource"], "fast");
await api.stop();
await api.clear(7);
await api.scanStatus();
await api.summary();
await api.dataOptions(7);
await api.search("resource", { serverId: 7, page: 2, resourceNameKey: "100281" });
await api.jumpServer(9);
await api.importServerJumpHistory([9, 8]);
await api.setServerJumpHistory([9, 8, 7]);
await api.coordinateJump({ serverId: 7, x: 12, y: 34 });
await api.setPlayerMark({ serverId: 7, ownerUid: "u1" }, true);
await api.exportCities({ serverId: 7, page: 1, pageSize: 200, sorts: [] }, { headers: ["Server"], sheetName: "City", yesLabel: "Yes", noLabel: "No" });
assert.deepEqual(calls.map(({ command }) => command), [
  "map_scan_start", "map_scan_stop", "map_scan_clear", "map_scan_status", "map_summary", "map_data_options", "map_search",
  "server_jump", "server_jump_history_import", "server_jump_history_set",
  "map_coordinate_jump", "map_player_mark_set", "map_city_export",
]);
assert.deepEqual(calls[0].payload, { selectedTypes: ["resource"], scanMode: "fast", profileId: "profile-test" });
assert.deepEqual(calls[1].payload, { profileId: "profile-test" });
assert.deepEqual(calls[2].payload, { serverId: 7, profileId: "profile-test" });
assert.equal(calls[6].payload.kind, "resource");
assert.equal(calls[6].payload.query.page, 2);
assert.equal(calls[6].payload.query.pageSize, 50);
assert.equal(calls[6].payload.query.resourceNameKey, "100281");
assert.equal(calls[6].payload.profileId, "profile-test");
assert.deepEqual(calls[7].payload, { serverId: 9, profileId: "profile-test" });
assert.deepEqual(calls[8].payload, { history: [9, 8], profileId: "profile-test" });
assert.deepEqual(calls[9].payload, { history: [9, 8, 7], profileId: "profile-test" });
assert.deepEqual(calls[10].payload, { serverId: 7, x: 12, y: 34, profileId: "profile-test" });
assert.equal(calls[11].payload.marked, true);
assert.equal(calls[12].payload.query.serverId, 7);

const expectedFailure = Object.assign(new Error("query failed"), { code: "MAP_QUERY_FAILED", details: { field: "resourceNameKey" } });
const failingApi = createMapApi({
  profileId: "p",
  invoke: async () => { throw expectedFailure; },
  listen: () => () => {},
});
await assert.rejects(() => failingApi.search("resource", { serverId: 7 }), (error) => error === expectedFailure);

const previewBridge = createBackendBridge({ __LWBridgeBootstrap: { mode: "fixture" } });
assert.equal(previewBridge.available, false);
assert.equal(previewBridge.mode, "preview");
await assert.rejects(() => previewBridge.invoke("map_summary", {}), (error) => error.code === "PREVIEW_NO_NATIVE_HOST");

assert.equal(getMapPreviewProvider("native", "map-city"), null, "native bootstrap must ignore preview Map fixture query state");
assert.equal(getMapPreviewProvider("native-unavailable", "map-city"), null, "unavailable native bootstrap must ignore preview Map fixture query state");
const previewProvider = getMapPreviewProvider("preview", "map-city");
assert.equal(previewProvider.online, false, "browser-only Map fixture must never claim a live game connection");
assert.equal(previewProvider.mapApi.previewFixture, true);
const previewRows = await previewProvider.mapApi.search("city", { page: 1 });
assert.equal(previewRows.rows.length, 2);
assert.ok(previewRows.total > MAP_PAGE_SIZE, "browser-only Map fixture should cover pagination");
await assert.rejects(() => previewProvider.mapApi.start(["city"], "normal"), (error) => error.code === "PREVIEW_NATIVE_ACTION_BLOCKED");
await assert.rejects(() => previewProvider.mapApi.coordinateJump(previewRows.rows[0]), (error) => error.code === "PREVIEW_NATIVE_ACTION_BLOCKED");

const missingNativeBridge = createBackendBridge({ __LWBridgeBootstrap: { mode: "live", sessionId: "s" } });
assert.equal(missingNativeBridge.available, false);
assert.equal(missingNativeBridge.mode, "native-unavailable");
await assert.rejects(() => missingNativeBridge.invoke("map_summary", {}), (error) => error.code === "NATIVE_TRANSPORT_MISSING");

let messageHandler;
const posted = [];
const nativeHost = {
  __LWBridgeBootstrap: { mode: "live", sessionId: "session-1", profiles: { selectedProfileId: "profile-native" } },
  chrome: { webview: {
    addEventListener: (name, handler) => { if (name === "message") messageHandler = handler; },
    removeEventListener: () => {},
    postMessage: (message) => posted.push(message),
  } },
  setTimeout,
  clearTimeout,
  crypto: { randomUUID: () => "request-1" },
};
const nativeBridge = createBackendBridge(nativeHost);
const response = nativeBridge.invoke("map_summary", { profileId: "profile-native" });
assert.deepEqual(posted[0], {
  kind: "invoke",
  sessionId: "session-1",
  id: "request-1",
  command: "map_summary",
  payload: { profileId: "profile-native" },
});
messageHandler({ data: { kind: "response", sessionId: "session-1", id: "request-1", ok: true, result: { serverId: 7 } } });
assert.deepEqual(await response, { serverId: 7 });

const failed = nativeBridge.invoke("map_search", {});
messageHandler({ data: {
  kind: "response", sessionId: "session-1", id: "request-1", ok: false,
  error: { code: "INVALID_MAP_QUERY", message: "bad query", details: { feature: "sorts" } },
} });
await assert.rejects(() => failed, (error) => error.code === "INVALID_MAP_QUERY" && error.details?.feature === "sorts");
nativeBridge.dispose();

console.log("LWB317_MAP_UI_INTEGRATION_CHECKS_OK");
