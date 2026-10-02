import assert from "node:assert/strict";
import {
  createOriginalHarness,
  drain,
  optionsReply,
  dispatchRow,
  truckRow,
  filterCheckbox,
  treeNodes,
  nodeText,
  SERVER,
} from "../LWB317-UI-MAP-INTERACTIONS-001/original/common.mjs";

const EXPECTED_SHA = "ce74345518be72e417a510b982c591729e5683f69751e190805598c4c05d3089";

const results = {};

function selectNode(h, key) {
  const label = h.translate(key);
  const node = h.findNodes((n) => n.type === "select" && n.props?.["aria-label"] === label)[0];
  assert.ok(node, `select ${key}`);
  return node;
}

async function chooseSelect(h, key, value) {
  selectNode(h, key).props.onChange({ target: { value } });
  await h.settle();
}

function treasureTypeNode(h) {
  const node = h.findNodes((n) => typeof n.type === "function" && n.type.name === "lt")[0];
  assert.ok(node, "treasure type component");
  return node;
}

async function chooseTreasureType(h, value) {
  treasureTypeNode(h).props.onChange(value);
  await h.settle();
}

async function setFilterCheckbox(h, key, checked) {
  const box = filterCheckbox(h, key);
  assert.ok(box, `checkbox ${key}`);
  box.props.onChange({ target: { checked } });
  await h.settle();
}

function state(h, names) {
  return Object.fromEntries(names.map((name) => [name, structuredClone(h.getState(name))]));
}

function defined(query) {
  return Object.fromEntries(Object.entries(query).filter(([, value]) => value !== undefined));
}

function clearResponse(h, serverId = SERVER) {
  return { ...structuredClone(h.props.scanState), serverId, isReading: false };
}

async function triggerOptionsRefresh(h) {
  await h.emitScanState({ isReading: true });
  await h.emitScanState({ isReading: false });
}

async function allianceEncodingScenario() {
  const names = ["none", "all", "联盟 Ω", "A/B? C#D%"];
  const alliances = names.map((name, index) => ({ name, count: index + 1 }));
  const h = await createOriginalHarness({
    online: false,
    stubs: { dataOptions: { mode: "auto", value: optionsReply({ alliances, noAllianceCount: 4 }) } },
  });
  await h.mount();
  await drain(h);
  assert.equal(h.sourceSha256, EXPECTED_SHA);

  const decoder = h.internals.tt;
  const decoderMatrix = {
    allSentinel: decoder("all"),
    noneSentinel: decoder("none"),
    namedNone: decoder("name:none"),
    namedAll: decoder("name:all"),
    unicode: decoder(`name:${encodeURIComponent("联盟 Ω")}`),
    uri: decoder(`name:${encodeURIComponent("A/B? C#D%")}`),
    malformed: decoder("name:%E0%A4%A"),
    nonPrefixed: decoder("raw-alliance"),
  };
  assert.deepEqual(decoderMatrix, {
    allSentinel: "",
    noneSentinel: "",
    namedNone: "none",
    namedAll: "all",
    unicode: "联盟 Ω",
    uri: "A/B? C#D%",
    malformed: "",
    nonPrefixed: "",
  });

  const allianceSelect = selectNode(h, "map.allianceFilter");
  const optionValues = treeNodes(allianceSelect).filter((n) => n.type === "option").map((n) => ({ value: n.props.value, text: nodeText(n) }));
  for (const name of names) assert.ok(optionValues.some((entry) => entry.value === `name:${encodeURIComponent(name)}`), `encoded option ${name}`);

  const namedQueries = {};
  for (const name of names) {
    await chooseSelect(h, "map.allianceFilter", `name:${encodeURIComponent(name)}`);
    const request = h.requests.at(-1);
    namedQueries[name] = { alliance: request.query.alliance, withoutAlliance: request.query.withoutAlliance };
    assert.equal(request.query.alliance, name);
    assert.equal(request.query.withoutAlliance, undefined);
    await drain(h);
  }

  await chooseSelect(h, "map.allianceFilter", "none");
  assert.equal(h.requests.at(-1).query.alliance, undefined);
  assert.equal(h.requests.at(-1).query.withoutAlliance, true);
  await drain(h);

  await chooseSelect(h, "map.allianceFilter", "name:%E0%A4%A");
  const malformedQuery = h.requests.at(-1).query;
  assert.equal(malformedQuery.alliance, undefined);
  assert.equal(malformedQuery.withoutAlliance, undefined);
  await drain(h);
  h.setStubBehavior("dataOptions", { mode: "auto", value: optionsReply({ alliances: [], noAllianceCount: 0 }) });
  await triggerOptionsRefresh(h);
  assert.equal(h.getState("allianceFilter"), "name:%E0%A4%A", "decode failure is retained by options validation");

  results.allianceEncoding = { decoderMatrix, optionValues, namedQueries, malformedAfterRefresh: h.getState("allianceFilter") };
}

async function optionsValidationScenario() {
  const gold = { key: "gold", treasureType: 7, suppliesType: 3, treasureNameKey: "gold", count: 2 };
  const initial = optionsReply({
    alliances: [{ name: "Foo", count: 2 }],
    noAllianceCount: 2,
    dispatchLevels: [5, 9],
    treasureTypes: [gold],
  });
  const h = await createOriginalHarness({ online: false, stubs: { dataOptions: { mode: "auto", value: initial } } });
  await h.mount();
  await drain(h);

  await chooseSelect(h, "map.allianceFilter", `name:${encodeURIComponent("Foo")}`);
  await drain(h);
  await h.clickTab("dispatch");
  await drain(h);
  await chooseSelect(h, "map.level", "5");
  await drain(h);
  await h.clickTab("treasure");
  await drain(h);
  await chooseTreasureType(h, "gold");
  await drain(h);

  h.setStubBehavior("dataOptions", { mode: "auto", value: initial });
  await triggerOptionsRefresh(h);
  const kept = state(h, ["allianceFilter", "dispatchLevel", "treasureTypeKey"]);
  assert.deepEqual(kept, { allianceFilter: "name:Foo", dispatchLevel: "5", treasureTypeKey: "gold" });
  await drain(h);

  h.setStubBehavior("dataOptions", { mode: "auto", value: optionsReply({ alliances: [], noAllianceCount: 0, dispatchLevels: [], treasureTypes: [] }) });
  const requestsBeforeDrop = h.requests.length;
  await triggerOptionsRefresh(h);
  const dropped = state(h, ["allianceFilter", "dispatchLevel", "treasureTypeKey"]);
  assert.deepEqual(dropped, { allianceFilter: "all", dispatchLevel: "", treasureTypeKey: "" });
  assert.ok(h.requests.length > requestsBeforeDrop, "option replacement causes search activity");
  await drain(h);

  h.setStubBehavior("dataOptions", { mode: "auto", value: optionsReply({ alliances: [], noAllianceCount: 3, dispatchLevels: [], treasureTypes: [] }) });
  await triggerOptionsRefresh(h);
  await h.clickTab("city");
  await drain(h);
  await chooseSelect(h, "map.allianceFilter", "none");
  await drain(h);
  await triggerOptionsRefresh(h);
  assert.equal(h.getState("allianceFilter"), "none", "none sentinel retained while count > 0");
  h.setStubBehavior("dataOptions", { mode: "auto", value: optionsReply({ alliances: [], noAllianceCount: 0, dispatchLevels: [], treasureTypes: [] }) });
  await triggerOptionsRefresh(h);
  assert.equal(h.getState("allianceFilter"), "all", "none sentinel reset when count reaches zero");

  const stale = await createOriginalHarness({ online: false, stubs: { dataOptions: { mode: "manual" } } });
  await stale.mount();
  const oldCall = stale.callsNamed("dataOptions").find((call) => call.args[0] === 321 && call.pending);
  assert.ok(oldCall);
  await stale.emitServer(456);
  const newCall = stale.callsNamed("dataOptions").findLast((call) => call.args[0] === 456 && call.pending);
  assert.ok(newCall);
  await stale.resolveCall(oldCall, optionsReply({ alliances: [{ name: "STALE", count: 1 }] })(321));
  assert.deepEqual(stale.getState("alliances"), [], "old-server options reply is generation-fenced");
  await stale.resolveCall(newCall, optionsReply({ alliances: [{ name: "CURRENT", count: 1 }] })(456));
  assert.deepEqual(stale.getState("alliances"), [{ name: "CURRENT", count: 1 }]);

  results.optionsValidation = {
    validSelectionsKept: kept,
    disappearingSelectionsDropped: dropped,
    noneSentinelAfterZeroCount: h.getState("allianceFilter"),
    staleOldServerIgnored: true,
    currentServerApplied: stale.getState("alliances"),
  };
}

async function clearLifecycleScenario() {
  const initialOptions = optionsReply({
    alliances: [{ name: "Foo", count: 2 }],
    noAllianceCount: 2,
    dispatchLevels: [5],
    rewardItems: { truck: [{ key: "item1", count: 2 }], railway: [] },
    treasureTypes: [{ key: "gold", treasureType: 7, suppliesType: 3, treasureNameKey: "gold", count: 2 }],
  });
  const h = await createOriginalHarness({ online: false, stubs: { dataOptions: { mode: "auto", value: initialOptions } } });
  await h.mount();
  await drain(h);

  await chooseSelect(h, "map.allianceFilter", "name:Foo");
  await setFilterCheckbox(h, "map.markedOnly", true);
  await drain(h);

  await h.clickTab("resource"); await drain(h);
  await h.selectName("r2"); await drain(h);

  await h.clickTab("treasure"); await drain(h);
  await chooseTreasureType(h, "gold");
  await setFilterCheckbox(h, "map.showForeignRadarTreasures", true);
  await setFilterCheckbox(h, "map.prioritizeLuckyTreasures", false);
  await drain(h);

  await h.clickTab("truck"); await drain(h, { rows: [truckRow("t1")], total: 1 });
  const item = h.findNodes((n) => typeof n.type === "function" && n.type.name === "ct")[0];
  assert.ok(item, "truck item filter");
  item.props.onChange("item1"); await h.settle(); await drain(h, { rows: [truckRow("t1")], total: 1 });
  await h.toggleRow(0);

  await h.clickTab("dispatch"); await drain(h, { rows: [dispatchRow("1001")], total: 1 });
  await chooseSelect(h, "map.level", "5");
  await chooseSelect(h, "map.quality", "ur");
  await chooseSelect(h, "common.status", "completed");
  await setFilterCheckbox(h, "map.plunderableOnly", true);
  await h.setRandomDelay("7");
  await h.typeKeyword("kw");
  h.tableNode().props.onSort("level"); await h.settle();
  await drain(h, { rows: [dispatchRow("1001")], total: 1 });
  await h.toggleRow(0);

  const retainedNames = ["keyword", "markedOnly", "qualityFilter", "completionStatus", "plunderableOnly", "sorts", "randomDelay", "includeForeignRadarTreasures", "luckyFirst"];
  const resetNames = ["allianceFilter", "nameSelection", "dispatchLevel", "itemFilter", "treasureTypeKey", "alliances", "nameOptions", "dispatchLevels", "rewardItems", "treasureTypes", "noAllianceCount", "scanProgress", "selectedDispatchKeys", "selectedTruckKeys"];
  const beforeFailure = { retained: state(h, retainedNames), resetCandidates: state(h, resetNames), rows: structuredClone(h.getState("rows")), total: h.getState("total") };

  h.setStubBehavior("scanClear", { mode: "reject", error: new Error("clear failed") });
  h.findButton("map.clearServer").props.onClick();
  await h.settle(); await h.settle();
  assert.equal(h.getState("scanError"), "Error: clear failed");
  assert.deepEqual(state(h, retainedNames), beforeFailure.retained);
  assert.deepEqual(state(h, resetNames), beforeFailure.resetCandidates);

  h.setStubBehavior("dataOptions", { mode: "manual" });
  await triggerOptionsRefresh(h);
  const oldOptions = h.callsNamed("dataOptions").findLast((call) => call.pending);
  assert.ok(oldOptions, "pre-clear options request pending");
  await drain(h, { rows: [dispatchRow("1001")], total: 1 });

  await h.clickSearch();
  const oldSearch = h.currentRequest();
  assert.ok(oldSearch, "pre-clear search request pending");
  const beforePendingClear = state(h, [...retainedNames, ...resetNames, "rowsRevision", "optionsRevision"]);
  const optionGenerationBefore = h.getRef("optionsGeneration").current;
  const searchGenerationBefore = h.getRef("searchGeneration").current;

  h.setStubBehavior("scanClear", { mode: "manual" });
  h.findButton("map.clearServer").props.onClick();
  await h.settle();
  const clearCall = h.callsNamed("scanClear").findLast((call) => call.pending);
  assert.ok(clearCall, "deferred clear call");
  assert.equal(h.getState("scanError"), "", "starting clear clears prior scan error");
  assert.deepEqual(state(h, retainedNames), Object.fromEntries(retainedNames.map((name) => [name, beforePendingClear[name]])));
  assert.deepEqual(state(h, resetNames), Object.fromEntries(resetNames.map((name) => [name, beforePendingClear[name]])));

  await h.resolveCall(clearCall, clearResponse(h));
  const afterAck = state(h, [...retainedNames, ...resetNames, "page", "rows", "total", "loading", "rowsRevision", "optionsRevision"]);
  assert.deepEqual(Object.fromEntries(retainedNames.map((name) => [name, afterAck[name]])), Object.fromEntries(retainedNames.map((name) => [name, beforePendingClear[name]])), "retained state survives acknowledged clear");
  assert.equal(afterAck.allianceFilter, "all");
  assert.deepEqual(afterAck.nameSelection, {});
  assert.equal(afterAck.dispatchLevel, "");
  assert.deepEqual(afterAck.itemFilter, {});
  assert.equal(afterAck.treasureTypeKey, "");
  assert.deepEqual(afterAck.alliances, []);
  assert.deepEqual(afterAck.nameOptions, { resource: [], monster: [] });
  assert.deepEqual(afterAck.dispatchLevels, []);
  assert.deepEqual(afterAck.rewardItems, { truck: [], railway: [] });
  assert.deepEqual(afterAck.treasureTypes, []);
  assert.equal(afterAck.noAllianceCount, 0);
  assert.equal(afterAck.scanProgress, null);
  assert.deepEqual(afterAck.selectedDispatchKeys, []);
  assert.deepEqual(afterAck.selectedTruckKeys, []);
  assert.equal(afterAck.page, 1);
  assert.deepEqual(afterAck.rows, []);
  assert.equal(afterAck.total, 0);
  assert.ok(afterAck.rowsRevision > beforePendingClear.rowsRevision);
  assert.ok(afterAck.optionsRevision > beforePendingClear.optionsRevision);
  assert.equal(h.getRef("optionsGeneration").current, optionGenerationBefore + 2, "clear invalidates old options then launches the post-clear options request");
  assert.equal(h.getRef("searchGeneration").current, searchGenerationBefore + 1, "post-clear rows revision launches a newer search");
  assert.equal(afterAck.loading, true, "tr writes false, then the post-clear search effect immediately sets loading true");
  assert.equal(h.getRef("tabCache").current.size, 0);

  oldOptions.pending && await h.resolveCall(oldOptions, optionsReply({ alliances: [{ name: "STALE", count: 9 }], dispatchLevels: [99] })(SERVER));
  assert.deepEqual(h.getState("alliances"), [], "pre-clear options response is fenced by O.current increment");
  oldSearch.done = true;
  await h.resolveRequest(oldSearch, { rows: [dispatchRow("STALE")], total: 99 });
  assert.deepEqual(h.getState("rows"), [], "pre-clear search response is fenced by the newer post-clear rr generation");
  assert.equal(h.getState("loading"), true, "stale search finally does not clear the current request's loading state");

  const freshOptions = h.callsNamed("dataOptions").findLast((call) => call.pending);
  assert.ok(freshOptions && freshOptions !== oldOptions, "post-clear options request pending");
  await h.resolveCall(freshOptions, optionsReply({ alliances: [], noAllianceCount: 0, dispatchLevels: [], treasureTypes: [], names: { resource: [], monster: [] } })(SERVER));
  await drain(h);
  assert.equal(h.getState("loading"), false);

  const msg = await createOriginalHarness({ online: false, stubs: {
    dataOptions: { mode: "auto", value: optionsReply() },
    cityExport: { mode: "auto", value: () => ({ canceled: false, rowCount: 1, path: "C:/x.xlsx" }) },
    scanClear: { mode: "auto", value: () => clearResponse(msg) },
  } });
  // The scanClear closure above cannot run until msg is initialized; it is only invoked after initialization.
  await msg.mount(); await drain(msg);
  msg.findButton("map.exportExcel").props.onClick(); await msg.settle(); await msg.settle();
  assert.equal(msg.getState("message"), "map.exportExcelSuccess");
  msg.findButton("map.clearServer").props.onClick(); await msg.settle(); await msg.settle();
  assert.equal(msg.getState("message"), "map.exportExcelSuccess", "general action message is retained by tr");

  const treasureMsg = await createOriginalHarness({ online: false, stubs: { dataOptions: { mode: "auto", value: optionsReply() } } });
  await treasureMsg.mount(); await drain(treasureMsg); await treasureMsg.clickTab("treasure"); await drain(treasureMsg);
  treasureMsg.findButton("map.claimTreasureBoxes").props.onClick(); await treasureMsg.settle(); await treasureMsg.settle();
  assert.equal(treasureMsg.getState("treasureClaimMessage"), "map.treasuresQueued");
  treasureMsg.findButton("map.clearServer").props.onClick(); await treasureMsg.settle(); await treasureMsg.settle();
  assert.equal(treasureMsg.getState("treasureClaimMessage"), "map.treasuresQueued", "treasure claim message is retained by tr");

  const deferred = await createOriginalHarness({ online: false, stubs: { dataOptions: { mode: "auto", value: optionsReply() }, scanClear: { mode: "manual" } } });
  await deferred.mount(); await drain(deferred); await deferred.typeKeyword("keep-me");
  deferred.findButton("map.clearServer").props.onClick(); await deferred.settle();
  const delayedClear = deferred.callsNamed("scanClear").findLast((call) => call.pending);
  await deferred.emitServer(456);
  await deferred.resolveCall(delayedClear, clearResponse(deferred, 321));
  assert.equal(deferred.getState("dataServerId"), 321, "original has no clear-request generation fence; acknowledged old-server clear can restore its response server");
  assert.equal(deferred.getState("keyword"), "keep-me");

  results.clearLifecycle = {
    failurePreservedFiltersAndData: true,
    pendingPreservedFiltersAndData: true,
    resetAfterAck: Object.fromEntries(resetNames.map((name) => [name, afterAck[name]])),
    retainedAfterAck: Object.fromEntries(retainedNames.map((name) => [name, afterAck[name]])),
    postAck: { page: afterAck.page, rows: afterAck.rows, total: afterAck.total, loading: afterAck.loading, rowsRevision: afterAck.rowsRevision, optionsRevision: afterAck.optionsRevision },
    stalePreClearOptionsIgnored: true,
    stalePreClearSearchIgnoredIncludingFinally: true,
    actionMessageRetained: msg.getState("message"),
    treasureClaimMessageRetained: treasureMsg.getState("treasureClaimMessage"),
    deferredOldServerClearAckDataServerId: deferred.getState("dataServerId"),
  };
}

async function treasureStorageScenario() {
  const cases = [
    { label: "missing", storage: {}, foreign: false, lucky: true, normalized: { foreign: "false", lucky: "true" } },
    { label: "true-false", storage: { "lwbridge.mapIncludeForeignRadarTreasures": "true", "lwbridge.mapLuckyTreasurePriority": "false" }, foreign: true, lucky: false, normalized: { foreign: "true", lucky: "false" } },
    { label: "false-true", storage: { "lwbridge.mapIncludeForeignRadarTreasures": "false", "lwbridge.mapLuckyTreasurePriority": "true" }, foreign: false, lucky: true, normalized: { foreign: "false", lucky: "true" } },
    { label: "unexpected", storage: { "lwbridge.mapIncludeForeignRadarTreasures": "TRUE", "lwbridge.mapLuckyTreasurePriority": "0" }, foreign: false, lucky: true, normalized: { foreign: "false", lucky: "true" } },
  ];
  const observed = [];
  for (const entry of cases) {
    const h = await createOriginalHarness({ online: false, storage: entry.storage, stubs: { dataOptions: { mode: "auto", value: optionsReply() } } });
    await h.mount();
    assert.equal(h.getState("includeForeignRadarTreasures"), entry.foreign, `${entry.label} foreign init`);
    assert.equal(h.getState("luckyFirst"), entry.lucky, `${entry.label} lucky init`);
    assert.equal(h.storage.get("lwbridge.mapIncludeForeignRadarTreasures"), entry.normalized.foreign, `${entry.label} foreign normalized write`);
    assert.equal(h.storage.get("lwbridge.mapLuckyTreasurePriority"), entry.normalized.lucky, `${entry.label} lucky normalized write`);
    observed.push({ label: entry.label, foreign: h.getState("includeForeignRadarTreasures"), lucky: h.getState("luckyFirst"), writes: h.storageWrites.filter(([key]) => key.includes("Treasure")) });
  }

  const h = await createOriginalHarness({ online: false, stubs: { dataOptions: { mode: "auto", value: optionsReply() } } });
  await h.mount(); await drain(h);
  const cityQuery = h.requests.at(-1).query;
  assert.equal(defined(cityQuery).includeForeignRadarTreasures, undefined);
  assert.equal(defined(cityQuery).luckyFirst, undefined);
  await h.clickTab("treasure"); await drain(h);
  const initialTreasureQuery = h.requests.at(-1).query;
  assert.equal(initialTreasureQuery.includeForeignRadarTreasures, false);
  assert.equal(initialTreasureQuery.luckyFirst, true);

  await setFilterCheckbox(h, "map.showForeignRadarTreasures", true);
  await drain(h);
  await setFilterCheckbox(h, "map.prioritizeLuckyTreasures", false);
  const toggledQuery = h.requests.at(-1).query;
  assert.equal(toggledQuery.includeForeignRadarTreasures, true);
  assert.equal(toggledQuery.luckyFirst, false);
  assert.equal(h.storage.get("lwbridge.mapIncludeForeignRadarTreasures"), "true");
  assert.equal(h.storage.get("lwbridge.mapLuckyTreasurePriority"), "false");

  const persistedStorage = Object.fromEntries(h.storage);
  const remount = await createOriginalHarness({ online: false, storage: persistedStorage, stubs: { dataOptions: { mode: "auto", value: optionsReply() } } });
  await remount.mount();
  assert.equal(remount.getState("includeForeignRadarTreasures"), true);
  assert.equal(remount.getState("luckyFirst"), false);
  await remount.clickTab("treasure");
  const remountQuery = remount.requests.findLast((request) => request.kind === "treasure").query;
  assert.equal(remountQuery.includeForeignRadarTreasures, true);
  assert.equal(remountQuery.luckyFirst, false);
  await remount.clickTab("city");
  const cityAfter = remount.requests.findLast((request) => request.kind === "city").query;
  assert.equal(cityAfter.includeForeignRadarTreasures, undefined);
  assert.equal(cityAfter.luckyFirst, undefined);

  results.treasureStorage = {
    initializationCases: observed,
    initialTreasureQuery: { includeForeignRadarTreasures: initialTreasureQuery.includeForeignRadarTreasures, luckyFirst: initialTreasureQuery.luckyFirst },
    toggledTreasureQuery: { includeForeignRadarTreasures: toggledQuery.includeForeignRadarTreasures, luckyFirst: toggledQuery.luckyFirst },
    remount: { includeForeignRadarTreasures: remount.getState("includeForeignRadarTreasures"), luckyFirst: remount.getState("luckyFirst") },
    unrelatedTabOmitsFields: cityAfter.includeForeignRadarTreasures === undefined && cityAfter.luckyFirst === undefined,
  };
}

await allianceEncodingScenario();
await optionsValidationScenario();
await clearLifecycleScenario();
await treasureStorageScenario();

console.log("LWB317_SUBAGENT_SOURCE_LIFECYCLE_OK");
console.log(JSON.stringify(results, null, 2));
