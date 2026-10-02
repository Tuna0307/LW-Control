import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { createHarness, deferred, nodeText, repo, tick, treeNodes } from "./harness.mjs";
import { createDriver } from "../LWB317-UI-MAP-INTERACTIONS-001/driver.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const sources = {
  baseline: path.join(here, "baseline-c72aae6.MapDataPage.jsx"),
  corrected: path.join(repo, "src/LWBridge.UI-0.3.17/src/MapDataPage.jsx"),
};
const kinds = ["city", "resource", "monster", "truck", "railway", "dispatch", "ghost", "treasure"];
const counts = Object.fromEntries(kinds.map((kind, index) => [kind, 20 + index]));
const storageKeys = {
  foreign: "lwbridge.mapIncludeForeignRadarTreasures",
  lucky: "lwbridge.mapLuckyTreasurePriority",
};
const uriAlliance = "A/B 東京 & %";
const baseOptions = {
  serverId: 321,
  counts,
  alliances: [
    { name: "none", count: 4 },
    { name: "all", count: 3 },
    { name: "Foo", count: 2 },
    { name: uriAlliance, count: 1 },
  ],
  names: {
    resource: [{ key: "r1", count: 3 }, { key: "r2", count: 5 }],
    monster: [{ key: "m1", count: 2 }],
  },
  dispatchLevels: [5, 6],
  rewardItems: {
    truck: [{ key: "gold", name: "Gold" }],
    railway: [{ key: "cargo", name: "Cargo" }],
  },
  treasureTypes: [
    { key: "radar", treasureType: 1, suppliesType: 0, treasureNameKey: "radar-name", name: "Radar", count: 8 },
    { key: "ice", treasureType: 0, suppliesType: 1, treasureNameKey: "ice-name", name: "Ice", count: 2 },
  ],
  noAllianceCount: 2,
  scanProgress: null,
};

function emptyOptions(serverId = 321) {
  return {
    serverId,
    counts: Object.fromEntries(kinds.map((kind) => [kind, 0])),
    alliances: [],
    names: { resource: [], monster: [] },
    dispatchLevels: [],
    rewardItems: { truck: [], railway: [] },
    treasureTypes: [],
    noAllianceCount: 0,
    scanProgress: null,
  };
}

function scanState(serverId = 321) {
  return {
    serverId,
    serverIdSource: "fixture",
    scanRunId: "filter-lifecycle",
    isReading: false,
    phase: "completed",
    selectedTypes: kinds,
    scanMode: "normal",
    progressPercent: 100,
  };
}

const clone = (value) => structuredClone(value);
const sha = (file) => crypto.createHash("sha256").update(fs.readFileSync(file, "utf8").replace(/\r\n/g, "\n")).digest("hex");

async function resolveSearches(h, result = { rows: [], total: 0 }) {
  for (let round = 0; round < 20; round += 1) {
    const open = h.requests.filter((request) => !request.__settled);
    if (!open.length) return;
    for (const request of open) {
      request.__settled = true;
      request.resolve(typeof result === "function" ? result(request) : result);
    }
    await h.settle();
  }
  throw new Error(h.label + ": search requests did not drain");
}

async function boot(file, label, options = {}) {
  const tab = options.tab || "city";
  const h = await createHarness(fs.readFileSync(file, "utf8"), label, {
    storage: options.storage || {},
    dataOptions: clone(options.dataOptions || baseOptions),
    props: { previewState: "map-" + tab, online: options.online ?? true },
  });
  const d = createDriver(h);
  await h.mount();
  await resolveSearches(h);
  return { h, d };
}

function selectNode(h, label) {
  const node = h.findNodes((candidate) => candidate.type === "select" && candidate.props?.["aria-label"] === label)[0];
  assert.ok(node, h.label + ": select " + label);
  return node;
}

function optionValue(h, label, text) {
  const node = selectNode(h, label);
  const option = treeNodes(node).find((candidate) => candidate.type === "option" && nodeText(candidate) === text);
  assert.ok(option, h.label + ": option " + label + " -> " + text);
  return option.props.value;
}

async function setSelect(h, label, value) {
  selectNode(h, label).props.onChange({ target: { value } });
  await h.settle();
}

function checkbox(h, text) {
  const label = h.findNodes((candidate) => candidate.type === "label" && nodeText(candidate).trim() === text)[0];
  assert.ok(label, h.label + ": checkbox label " + text);
  const input = treeNodes(label).find((candidate) => candidate.type === "input" && candidate.props?.type === "checkbox");
  assert.ok(input, h.label + ": checkbox input " + text);
  return input;
}

async function setCheckbox(h, text, checked) {
  checkbox(h, text).props.onChange({ target: { checked } });
  await h.settle();
}

function button(h, text) {
  const node = h.findNodes((candidate) => candidate.type === "button" && nodeText(candidate) === text)[0];
  assert.ok(node, h.label + ": button " + text);
  return node;
}

function pick(query, keys) {
  return Object.fromEntries(keys.filter((key) => query[key] !== undefined).map((key) => [key, query[key]]));
}

function snapshot(h) {
  return {
    tab: h.getState("tab"),
    keyword: h.getState("keyword"),
    resourceNameKey: h.getState("resourceNameKey"),
    monsterNameKey: h.getState("monsterNameKey"),
    alliance: h.getState("alliance"),
    markedOnly: h.getState("markedOnly"),
    treasureType: h.getState("treasureType"),
    completionStatusByKind: clone(h.getState("completionStatusByKind")),
    qualityByKind: clone(h.getState("qualityByKind")),
    itemKeyByKind: clone(h.getState("itemKeyByKind")),
    plunderableOnlyByKind: clone(h.getState("plunderableOnlyByKind")),
    includeForeignRadarTreasures: h.getState("includeForeignRadarTreasures"),
    luckyFirst: h.getState("luckyFirst"),
    minLevel: h.getState("minLevel"),
    dispatchSelectionKeys: Object.keys(h.getState("dispatchSelection")).sort(),
    truckSelectionKeys: Object.keys(h.getState("truckSelection")).sort(),
    sortsByKind: clone(h.getState("sortsByKind")),
    page: h.getState("page"),
    rows: clone(h.getState("rows")),
    total: h.getState("total"),
    loading: h.getState("loading"),
  };
}

async function allianceScenario(file, label) {
  const { h } = await boot(file, label + "-alliance");
  const out = {};
  for (const name of ["none", "all", uriAlliance]) {
    const item = baseOptions.alliances.find((entry) => entry.name === name);
    const value = optionValue(h, "map.allianceFilter", name + " (" + item.count + ")");
    await setSelect(h, "map.allianceFilter", value);
    out[name] = {
      optionValue: value,
      query: pick(h.currentRequest().query, ["alliance", "withoutAlliance"]),
    };
    await resolveSearches(h);
  }
  await setSelect(h, "map.allianceFilter", "name:%E0%A4%A");
  out.malformed = pick(h.currentRequest().query, ["alliance", "withoutAlliance"]);
  await resolveSearches(h);
  return out;
}

async function exportAllianceScenario(file, label) {
  const { h } = await boot(file, label + "-export-alliance");
  const calls = [];
  h.api.exportCities = async (query) => { calls.push(clone(query)); return {}; };
  const run = async (value) => {
    await setSelect(h, "map.allianceFilter", value);
    await resolveSearches(h);
    button(h, "map.exportExcel").props.onClick();
    await tick();
    await h.settle();
    return pick(calls.at(-1), ["alliance", "withoutAlliance"]);
  };
  const namedNone = await run(optionValue(h, "map.allianceFilter", "none (4)"));
  const namedAll = await run(optionValue(h, "map.allianceFilter", "all (3)"));
  const uri = await run(optionValue(h, "map.allianceFilter", uriAlliance + " (1)"));
  const noAlliance = await run("none");
  return { namedNone, namedAll, uri, noAlliance };
}

async function refreshScenario(file, label) {
  const { h } = await boot(file, label + "-refresh");
  const foo = optionValue(h, "map.allianceFilter", "Foo (2)");
  await setSelect(h, "map.allianceFilter", foo); await resolveSearches(h);
  await h.clickTab("dispatch"); await resolveSearches(h);
  await setSelect(h, "map.level", "5"); await resolveSearches(h);
  await h.clickTab("treasure"); await resolveSearches(h);
  await setSelect(h, "map.treasureType", "radar"); await resolveSearches(h);
  h.api.dataOptions = async () => ({ ...emptyOptions(321), alliances: [{ name: "Other", count: 1 }] });
  await h.advance(5000);
  await resolveSearches(h);
  return {
    alliance: h.getState("alliance"),
    level: h.getState("minLevel"),
    treasureType: h.getState("treasureType"),
  };
}

async function validRefreshScenario(file, label) {
  const { h } = await boot(file, label + "-valid-refresh");
  const foo = optionValue(h, "map.allianceFilter", "Foo (2)");
  await setSelect(h, "map.allianceFilter", foo); await resolveSearches(h);
  await h.clickTab("dispatch"); await resolveSearches(h);
  await setSelect(h, "map.level", "5"); await resolveSearches(h);
  await h.clickTab("treasure"); await resolveSearches(h);
  await setSelect(h, "map.treasureType", "radar"); await resolveSearches(h);
  h.api.dataOptions = async (serverId) => ({ ...clone(baseOptions), serverId });
  await h.advance(5000);
  await resolveSearches(h);
  return {
    alliancePreserved: h.getState("alliance") === foo,
    levelPreserved: h.getState("minLevel") === "5",
    treasurePreserved: h.getState("treasureType") === "radar",
  };
}

async function refreshEdgeScenario(file, label) {
  const sentinel = await boot(file, label + "-refresh-none");
  await setSelect(sentinel.h, "map.allianceFilter", "none");
  await resolveSearches(sentinel.h);
  sentinel.h.api.dataOptions = async () => emptyOptions(321);
  await sentinel.h.advance(5000);
  await resolveSearches(sentinel.h);

  const malformed = await boot(file, label + "-refresh-malformed");
  await setSelect(malformed.h, "map.allianceFilter", "name:%E0%A4%A");
  await resolveSearches(malformed.h);
  malformed.h.api.dataOptions = async () => emptyOptions(321);
  await malformed.h.advance(5000);
  await resolveSearches(malformed.h);
  return {
    noneAfterCountDisappears: sentinel.h.getState("alliance"),
    malformedAfterRefresh: malformed.h.getState("alliance"),
    malformedQuery: pick(malformed.h.currentRequest().query, ["alliance", "withoutAlliance"]),
  };
}

async function staleOptionScenario(file, label) {
  const { h } = await boot(file, label + "-stale-options");
  const pending = [];
  h.api.dataOptions = (serverId) => {
    const call = deferred();
    pending.push({ serverId, ...call });
    return call.promise;
  };
  await h.advance(5000);
  assert.equal(pending[0]?.serverId, 321);
  await h.emitServer(322);
  const old = pending.find((call) => call.serverId === 321);
  old.resolve({ ...clone(baseOptions), serverId: 321, alliances: [{ name: "STALE-321", count: 99 }] });
  await tick();
  await h.settle();
  const afterOld = h.getState("options")?.alliances?.map((item) => item.name) || [];
  const fresh = pending.find((call) => call.serverId === 322);
  if (fresh) {
    fresh.resolve({ ...clone(baseOptions), serverId: 322, alliances: [{ name: "FRESH-322", count: 1 }] });
    await tick();
    await h.settle();
  }
  return {
    optionCalls: pending.map((call) => call.serverId),
    afterOld,
    afterFresh: h.getState("options")?.alliances?.map((item) => item.name) || [],
  };
}

const dispatchRow = {
  serverId: 321, uuid: "dispatch-1", ownerUid: "u1", ownerName: "Dispatch 1", level: 5, quality: 5,
  completionTime: Date.now() + 60_000, plunderAt: Date.now() - 1000, taskExpireTime: Date.now() + 600_000,
};
const truckRow = {
  serverId: 321, uuid: "truck-1", ownerName: "Truck 1", quality: 5, arriveTs: Date.now() + 600_000,
  remainingLootCount: 1, maxLootCount: 2, robTimes: 0, currentGoods: [{ key: "gold", count: 1 }],
};

async function primeClearState(run) {
  const { h, d } = run;
  await setSelect(h, "map.allianceFilter", optionValue(h, "map.allianceFilter", "Foo (2)")); await resolveSearches(h);
  await d.setMarkedOnly(true); await resolveSearches(h);
  await h.clickTab("resource"); await resolveSearches(h);
  await d.selectName("r2"); await resolveSearches(h);
  await h.clickTab("treasure"); await resolveSearches(h);
  await setSelect(h, "map.treasureType", "radar"); await resolveSearches(h);
  await h.clickTab("dispatch"); await resolveSearches(h);
  await setSelect(h, "map.level", "5"); await resolveSearches(h);
  await setSelect(h, "map.quality", "ur"); await resolveSearches(h);
  await setSelect(h, "common.status", "pending"); await resolveSearches(h);
  await setCheckbox(h, "map.plunderableOnly", true); await resolveSearches(h);
  d.tableProps().onSort("level"); await h.settle(); await resolveSearches(h);
  await h.clickTab("truck"); await resolveSearches(h);
  await setSelect(h, "map.itemFilter", "gold"); await resolveSearches(h);
  await d.toggleRow("truck", truckRow);
  await h.clickTab("dispatch"); await resolveSearches(h);
  await d.toggleRow("dispatch", dispatchRow);
  await d.typeKeyword("kw");
}

async function clearSuccessScenario(file, label) {
  const run = await boot(file, label + "-clear-success");
  await primeClearState(run);
  const { h } = run;
  const before = snapshot(h);
  h.api.dataOptions = async (serverId) => emptyOptions(serverId);
  h.api.clear = async () => scanState(321);
  button(h, "map.clearServer").props.onClick();
  await h.settle();
  await h.settle();
  await resolveSearches(h);
  return { before, after: snapshot(h), scanError: h.getState("scanError") };
}

async function clearFailureScenario(file, label) {
  const run = await boot(file, label + "-clear-failure");
  await primeClearState(run);
  const { h } = run;
  const before = snapshot(h);
  const clearCall = deferred();
  h.api.clear = () => clearCall.promise;
  button(h, "map.clearServer").props.onClick();
  await h.settle();
  const pending = snapshot(h);
  clearCall.reject(Object.assign(new Error("clear failed"), { code: "CLEAR_FAILED" }));
  await tick();
  await h.settle();
  return { before, pending, afterFailure: snapshot(h), scanError: h.getState("scanError") };
}

async function staleSearchAfterClear(file, label) {
  const { h, d } = await boot(file, label + "-clear-stale-search");
  await d.clickSearch();
  const old = h.currentRequest();
  h.api.dataOptions = async (serverId) => emptyOptions(serverId);
  h.api.clear = async () => scanState(321);
  button(h, "map.clearServer").props.onClick();
  await h.settle();
  old.__settled = true;
  old.resolve({ rows: [{ serverId: 321, uuid: "stale-row", ownerName: "STALE", x: 1, y: 1 }], total: 1 });
  await tick();
  await h.settle();
  return {
    staleRowApplied: h.getState("rows").some((row) => row.uuid === "stale-row"),
    total: h.getState("total"),
    loading: h.getState("loading"),
    newerRequests: h.requests.filter((request) => request.id > old.id).length,
  };
}

async function staleOptionsAfterClear(file, label) {
  const { h } = await boot(file, label + "-clear-stale-options");
  const pending = [];
  h.api.dataOptions = (serverId) => {
    const call = deferred();
    pending.push({ serverId, ...call });
    return call.promise;
  };
  await h.advance(5000);
  const stale = pending[0];
  assert.equal(stale?.serverId, 321);
  h.api.clear = async () => scanState(321);
  button(h, "map.clearServer").props.onClick();
  await h.settle();
  stale.resolve({ ...clone(baseOptions), serverId: 321, alliances: [{ name: "STALE-AFTER-CLEAR", count: 1 }] });
  await tick();
  await h.settle();
  const afterStale = h.getState("options")?.alliances?.map((item) => item.name) || [];
  const fresh = pending.find((call) => call !== stale);
  assert.ok(fresh, label + ": fresh post-clear options request");
  fresh.resolve(emptyOptions(321));
  await tick();
  await h.settle();
  return {
    optionCalls: pending.map((call) => call.serverId),
    staleApplied: afterStale.includes("STALE-AFTER-CLEAR"),
    afterFresh: h.getState("options")?.alliances?.map((item) => item.name) || [],
  };
}

async function delayedClearAckAfterServerChange(file, label) {
  const { h } = await boot(file, label + "-delayed-clear");
  const clearCall = deferred();
  h.api.clear = () => clearCall.promise;
  button(h, "map.clearServer").props.onClick();
  await h.settle();
  await h.emitServer(322);
  clearCall.resolve(scanState(321));
  await tick();
  await h.settle();
  return {
    scanServerId: h.getState("scanState").serverId,
    browseServerId: h.getState("browseServerId"),
    alliance: h.getState("alliance"),
  };
}

async function treasureScenario(file, label, storage = {}) {
  const { h } = await boot(file, label + "-treasure", { tab: "treasure", storage, online: false });
  const initial = {
    foreign: checkbox(h, "map.showForeignRadarTreasures").props.checked,
    lucky: checkbox(h, "map.prioritizeLuckyTreasures").props.checked,
    query: pick(h.currentRequest().query, ["includeForeignRadarTreasures", "luckyFirst"]),
    storage: Object.fromEntries(h.storage),
  };
  await setCheckbox(h, "map.showForeignRadarTreasures", !initial.foreign); await resolveSearches(h);
  await setCheckbox(h, "map.prioritizeLuckyTreasures", !initial.lucky); await resolveSearches(h);
  const afterToggle = {
    foreign: checkbox(h, "map.showForeignRadarTreasures").props.checked,
    lucky: checkbox(h, "map.prioritizeLuckyTreasures").props.checked,
    query: pick(h.currentRequest().query, ["includeForeignRadarTreasures", "luckyFirst"]),
    storage: Object.fromEntries(h.storage),
    writes: h.storageWrites.filter(([key]) => key === storageKeys.foreign || key === storageKeys.lucky),
  };
  await h.clickTab("city"); await resolveSearches(h);
  const cityQuery = pick(h.currentRequest().query, ["includeForeignRadarTreasures", "luckyFirst"]);
  await h.clickTab("treasure"); await resolveSearches(h);
  const treasureQueryAfterReturn = pick(h.currentRequest().query, ["includeForeignRadarTreasures", "luckyFirst"]);
  const reload = await boot(file, label + "-treasure-reload", { tab: "treasure", storage: afterToggle.storage, online: false });
  const reloaded = {
    foreign: checkbox(reload.h, "map.showForeignRadarTreasures").props.checked,
    lucky: checkbox(reload.h, "map.prioritizeLuckyTreasures").props.checked,
    query: pick(reload.h.currentRequest().query, ["includeForeignRadarTreasures", "luckyFirst"]),
  };
  return { initial, afterToggle, cityQuery, treasureQueryAfterReturn, reloaded };
}

async function treasureTypeScenario(file, label) {
  const { h } = await boot(file, label + "-treasure-type", { tab: "treasure" });
  await setSelect(h, "map.treasureType", "radar");
  const radar = pick(h.currentRequest().query, ["treasureType", "suppliesType"]);
  await resolveSearches(h);
  await setSelect(h, "map.treasureType", "ice");
  const ice = pick(h.currentRequest().query, ["treasureType", "suppliesType"]);
  return { radar, ice };
}

const results = {
  sources: Object.fromEntries(Object.entries(sources).map(([key, file]) => [key, {
    path: path.relative(repo, file).replaceAll("\\", "/"),
    sha256: sha(file),
  }])),
  baseline: {},
  corrected: {},
};

for (const [kind, file] of Object.entries(sources)) {
  results[kind].alliance = await allianceScenario(file, kind);
  results[kind].exportAlliance = await exportAllianceScenario(file, kind);
  results[kind].refresh = await refreshScenario(file, kind);
  results[kind].validRefresh = await validRefreshScenario(file, kind);
  results[kind].refreshEdges = await refreshEdgeScenario(file, kind);
  results[kind].staleOptions = await staleOptionScenario(file, kind);
  results[kind].clearSuccess = await clearSuccessScenario(file, kind);
  results[kind].clearFailure = await clearFailureScenario(file, kind);
  results[kind].staleSearchAfterClear = await staleSearchAfterClear(file, kind);
  results[kind].treasureMissing = await treasureScenario(file, kind + "-missing");
  results[kind].treasureSeeded = await treasureScenario(file, kind + "-seeded", {
    [storageKeys.foreign]: "true",
    [storageKeys.lucky]: "false",
  });
  const unexpected = await boot(file, kind + "-unexpected-storage", {
    tab: "treasure",
    storage: { [storageKeys.foreign]: "TRUE", [storageKeys.lucky]: "0" },
    online: false,
  });
  results[kind].treasureUnexpected = {
    foreign: checkbox(unexpected.h, "map.showForeignRadarTreasures").props.checked,
    lucky: checkbox(unexpected.h, "map.prioritizeLuckyTreasures").props.checked,
    normalizedStorage: {
      foreign: unexpected.h.storage.get(storageKeys.foreign),
      lucky: unexpected.h.storage.get(storageKeys.lucky),
    },
  };
  results[kind].treasureType = await treasureTypeScenario(file, kind);
}

results.corrected.staleOptionsAfterClear = await staleOptionsAfterClear(sources.corrected, "corrected");
results.corrected.delayedClearAckAfterServerChange = await delayedClearAckAfterServerChange(sources.corrected, "corrected");

assert.deepEqual(results.baseline.alliance.none.query, { withoutAlliance: true });
assert.deepEqual(results.baseline.alliance.all.query, {});
assert.deepEqual(results.baseline.exportAlliance.namedNone, { withoutAlliance: true });
assert.deepEqual(results.baseline.refresh, { alliance: "Foo", level: "5", treasureType: "radar" });
assert.equal(results.baseline.refreshEdges.noneAfterCountDisappears, "none");
assert.ok(results.baseline.staleOptions.afterOld.includes("STALE-321"));
assert.equal(results.baseline.clearSuccess.after.alliance, results.baseline.clearSuccess.before.alliance);
assert.equal(results.baseline.clearSuccess.after.minLevel, "5");
assert.equal(results.baseline.treasureMissing.initial.lucky, false);
assert.deepEqual(results.baseline.treasureMissing.initial.query, {});

assert.equal(results.corrected.alliance.none.optionValue, "name:none");
assert.deepEqual(results.corrected.alliance.none.query, { alliance: "none" });
assert.equal(results.corrected.alliance.all.optionValue, "name:all");
assert.deepEqual(results.corrected.alliance.all.query, { alliance: "all" });
assert.equal(results.corrected.alliance[uriAlliance].optionValue, "name:" + encodeURIComponent(uriAlliance));
assert.deepEqual(results.corrected.alliance[uriAlliance].query, { alliance: uriAlliance });
assert.deepEqual(results.corrected.alliance.malformed, {});
assert.deepEqual(results.corrected.exportAlliance, {
  namedNone: { alliance: "none" },
  namedAll: { alliance: "all" },
  uri: { alliance: uriAlliance },
  noAlliance: { withoutAlliance: true },
});
assert.deepEqual(results.corrected.refresh, { alliance: "all", level: "", treasureType: "" });
assert.deepEqual(results.corrected.validRefresh, { alliancePreserved: true, levelPreserved: true, treasurePreserved: true });
assert.deepEqual(results.corrected.refreshEdges, {
  noneAfterCountDisappears: "all",
  malformedAfterRefresh: "name:%E0%A4%A",
  malformedQuery: {},
});
assert.ok(!results.corrected.staleOptions.afterOld.includes("STALE-321"));
assert.deepEqual(results.corrected.staleOptions.afterFresh, ["FRESH-322"]);

const clearBefore = results.corrected.clearSuccess.before;
const clearAfter = results.corrected.clearSuccess.after;
assert.equal(clearAfter.alliance, "all");
assert.equal(clearAfter.resourceNameKey, "");
assert.equal(clearAfter.monsterNameKey, "");
assert.equal(clearAfter.minLevel, "");
assert.deepEqual(clearAfter.itemKeyByKind, {});
assert.equal(clearAfter.treasureType, "");
assert.deepEqual(clearAfter.dispatchSelectionKeys, []);
assert.deepEqual(clearAfter.truckSelectionKeys, []);
assert.equal(clearAfter.page, 1);
assert.equal(clearAfter.total, 0);
assert.equal(clearAfter.keyword, clearBefore.keyword);
assert.equal(clearAfter.markedOnly, clearBefore.markedOnly);
assert.deepEqual(clearAfter.qualityByKind, clearBefore.qualityByKind);
assert.deepEqual(clearAfter.completionStatusByKind, clearBefore.completionStatusByKind);
assert.deepEqual(clearAfter.plunderableOnlyByKind, clearBefore.plunderableOnlyByKind);
assert.deepEqual(clearAfter.sortsByKind, clearBefore.sortsByKind);
assert.equal(clearAfter.tab, clearBefore.tab);
assert.deepEqual(results.corrected.clearFailure.pending, results.corrected.clearFailure.before);
assert.deepEqual(results.corrected.clearFailure.afterFailure, results.corrected.clearFailure.before);
assert.match(results.corrected.clearFailure.scanError, /CLEAR_FAILED.*clear failed/);
assert.equal(results.corrected.staleSearchAfterClear.staleRowApplied, false);
assert.deepEqual(results.corrected.staleOptionsAfterClear, {
  optionCalls: [321, 321],
  staleApplied: false,
  afterFresh: [],
});
assert.deepEqual(results.corrected.delayedClearAckAfterServerChange, {
  scanServerId: 321,
  browseServerId: 321,
  alliance: "all",
});

assert.deepEqual(results.corrected.treasureMissing.initial.query, { includeForeignRadarTreasures: false, luckyFirst: true });
assert.equal(results.corrected.treasureMissing.initial.foreign, false);
assert.equal(results.corrected.treasureMissing.initial.lucky, true);
assert.deepEqual(results.corrected.treasureSeeded.initial.query, { includeForeignRadarTreasures: true, luckyFirst: false });
assert.deepEqual(results.corrected.treasureMissing.cityQuery, {});
assert.deepEqual(results.corrected.treasureMissing.treasureQueryAfterReturn, results.corrected.treasureMissing.afterToggle.query);
assert.deepEqual(results.corrected.treasureMissing.reloaded.query, results.corrected.treasureMissing.afterToggle.query);
assert.deepEqual(results.corrected.treasureUnexpected, {
  foreign: false,
  lucky: true,
  normalizedStorage: { foreign: "false", lucky: "true" },
});
assert.deepEqual(results.corrected.treasureType, {
  radar: { treasureType: 1, suppliesType: 0 },
  ice: { treasureType: 0, suppliesType: 1 },
});

fs.writeFileSync(path.join(here, "filter-lifecycle-results.json"), JSON.stringify(results, null, 2) + "\n");
console.log("LWB317_UI_MAP_FILTER_LIFECYCLE_CHECKS_OK");
