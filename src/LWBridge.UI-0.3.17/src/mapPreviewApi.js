import { MAP_KIND_KEYS, normalizeOptions, normalizeScanState, normalizeSearchResult, normalizeSummary } from "./mapBackend.js";
import { plunderFixtureFor, plunderFixtureGameTexts } from "./mapPlunderFixtures.js";
import { scanHeaderFixture } from "./mapScanHeaderFixtures.js";

const FIXTURE_SERVER_ID = 321;
const FIXTURE_TIME = 1_799_000_000_000;
const FIXTURE_COUNTS = Object.freeze(Object.fromEntries(MAP_KIND_KEYS.map((kind, index) => [kind, 52 + index])));
const QUALITY_VALUES = Object.freeze({ n: 1, r: 2, sr: 3, ssr: 4 });

function blocked(action) {
  const error = new Error(`Browser preview cannot execute native Map action: ${action}`);
  error.code = "PREVIEW_NATIVE_ACTION_BLOCKED";
  return Promise.reject(error);
}

function baseRow(kind, index) {
  return {
    serverId: FIXTURE_SERVER_ID,
    recordKey: `${kind}-fixture-${String(index).padStart(3, "0")}`,
    uuid: `${kind}-uuid-${String(index).padStart(3, "0")}`,
    x: 410 + index,
    y: 520 + index,
    updatedAt: FIXTURE_TIME - index * 60_000,
  };
}

function fixtureRow(kind, index) {
  const row = baseRow(kind, index);
  switch (kind) {
    case "city":
      return { ...row, ownerUid: `fixture-city-${index}`, ownerName: `Fixture Commander ${index}`, allianceName: index % 4 === 0 ? "" : index % 2 === 0 ? "TST" : "QA", level: 20 + (index % 11), health: 5_000_000 + index * 125_000, protectEndTime: index % 3 === 0 ? FIXTURE_TIME + index * 60_000 : 0, marked: index % 5 === 0 };
    case "resource":
      return { ...row, resourceNameKey: index % 2 === 0 ? "100282" : "100281", level: 1 + (index % 10), rebuildGatherOccupancyKnown: true, rebuildGatherOccupied: index % 3 === 0, resourceRemainingAmount: 50_000 + index * 2_000, resourceFullAmount: 200_000 };
    case "monster":
      return { ...row, monsterNameKey: index % 2 === 0 ? "Fixture Monster B" : "Fixture Monster A", level: 20 + (index % 16), distanceFromHome: 40 + index * 7 };
    case "truck":
      return { ...row, ownerName: `Truck Owner ${index}`, allianceName: index % 3 === 0 ? "TST" : "QA", quality: 1 + (index % 5), isSpecialURQuality: index % 11 === 0, power: 18_000_000 + index * 175_000, currentGoods: [{ key: index % 2 === 0 ? "fixture-medal" : "fixture-reward", name: index % 2 === 0 ? "Fixture Medal" : "Fixture Reward", count: 1 + (index % 8) }], robTimes: index % 3, maxLootCount: 2, maxRobTimes: 2, remainingLootCount: index % 3 === 2 ? 0 : 2 - (index % 2), arriveTs: FIXTURE_TIME + 600_000 + index * 10_000 };
    case "railway":
      return { ...row, allianceName: index % 2 === 0 ? "TST Alliance" : "QA Alliance", allianceAbbr: index % 2 === 0 ? "TST" : "QA", quality: 1 + (index % 5), power: 24_000_000 + index * 150_000, currentGoods: [{ key: index % 2 === 0 ? "fixture-supply" : "fixture-cargo", name: index % 2 === 0 ? "Fixture Supply" : "Fixture Cargo", count: 1 + (index % 6) }], robTimes: index % 3, maxRobTimes: 2, remainingLootCount: index % 4 === 0 ? 0 : 1, arriveTs: FIXTURE_TIME + 900_000 + index * 10_000, protectTime: FIXTURE_TIME + index * 15_000 };
    case "dispatch":
      return { ...row, ownerUid: `dispatch-owner-${index}`, ownerName: `Task Owner ${index}`, level: 5 + (index % 4), quality: 1 + (index % 5), isSpecial: index % 9 === 0, rewards: [{ name: "Fixture Intel", count: 1 + (index % 3) }], completionTime: index % 2 === 0 ? FIXTURE_TIME - index * 30_000 : FIXTURE_TIME + index * 30_000, plunderAt: FIXTURE_TIME - 60_000, taskExpireTime: FIXTURE_TIME + 3_600_000, maxStealCount: 2, stolenCount: index % 3 };
    case "ghost":
      return { ...row, ownerUid: `ghost-owner-${index}`, ownerName: `Ghost Owner ${index}`, level: 4 + (index % 5), quality: 1 + (index % 5), isSpecial: index % 7 === 0, rewards: [{ name: "Fixture Ghost Reward", count: 1 + (index % 5) }], completionTime: index % 2 === 0 ? FIXTURE_TIME - index * 20_000 : FIXTURE_TIME + index * 20_000 };
    case "treasure":
      return { ...row, treasureNameKey: index % 2 === 0 ? "Fixture Radar Treasure" : "Fixture Lucky Treasure", treasureType: index % 2 === 0 ? 1 : 2, suppliesType: 0, remainingBoxes: 1 + (index % 5), worldClaimState: index % 3 === 0 ? "digging" : "available", playerClaimState: index % 3 === 0 ? "waiting" : "eligible", rewardedCount: index % 6, diggingCount: index % 4, expireTime: FIXTURE_TIME + 1_800_000 + index * 5_000, ownerName: `Treasure Owner ${index}`, allianceAbbr: index % 4 === 0 ? "TST" : "QA", allianceId: index % 4 === 0 ? "fixture-foreign" : "fixture-viewer-alliance", viewerAllianceId: "fixture-viewer-alliance", claimPriority: index % 5 === 0 ? 0 : 1 };
    default:
      return row;
  }
}

const FIXTURE_ROWS = Object.freeze(Object.fromEntries(MAP_KIND_KEYS.map((kind) => [kind, Object.freeze(Array.from({ length: FIXTURE_COUNTS[kind] }, (_, index) => Object.freeze(fixtureRow(kind, index + 1))))])));

function itemCount(row, itemKey) {
  return (row.currentGoods || []).filter((item) => String(item.key) === String(itemKey)).reduce((sum, item) => sum + Number(item.count || 0), 0);
}

function filterRows(kind, query, fixtureRows = FIXTURE_ROWS[kind], fixtureTime = FIXTURE_TIME) {
  const keyword = String(query.keyword || "").toLowerCase();
  return (fixtureRows || []).filter((row) => {
    if (keyword && !`${row.ownerName || ""} ${row.allianceName || ""} ${row.uuid || ""} ${JSON.stringify(row)}`.toLowerCase().includes(keyword)) return false;
    if (query.alliance != null && row.allianceName !== query.alliance) return false;
    if (query.withoutAlliance && row.allianceName) return false;
    if (query.markedOnly && row.marked !== true) return false;
    if (query.resourceNameKey != null && String(row.resourceNameKey) !== String(query.resourceNameKey)) return false;
    if (query.monsterNameKey != null && String(row.monsterNameKey) !== String(query.monsterNameKey)) return false;
    if (Number(query.suppliesType) > 0 && Number(row.suppliesType) !== Number(query.suppliesType)) return false;
    if (Number(query.treasureType) > 0 && (Number(row.treasureType) !== Number(query.treasureType) || Number(row.suppliesType || 0) !== 0)) return false;
    if (kind === "treasure" && !query.includeForeignRadarTreasures && Number(row.treasureType) === 1) {
      const viewerAllianceId = query.viewerAllianceId || row.viewerAllianceId;
      if (!viewerAllianceId || String(row.allianceId || "") !== String(viewerAllianceId)) return false;
    }
    if (query.quality != null) {
      if (query.quality === "ur") {
        if (!(Number(row.quality) >= 5) || (kind === "truck" && row.isSpecialURQuality === true)) return false;
      } else if (QUALITY_VALUES[query.quality] == null || Number(row.quality) !== QUALITY_VALUES[query.quality]) return false;
    }
    if (query.specialOnly && row.isSpecial !== true) return false;
    if (query.reindeerOnly && row.isSpecialURQuality !== true) return false;
    if (query.itemKey != null && itemCount(row, query.itemKey) <= 0) return false;
    if (query.completionStatus === "pending" && !(Number(row.completionTime || 0) <= 0 || Number(row.completionTime) > fixtureTime)) return false;
    if (query.completionStatus === "completed" && !(Number(row.completionTime) > 0 && Number(row.completionTime) <= fixtureTime)) return false;
    if (query.completionStatus != null && !["pending", "completed"].includes(query.completionStatus)) return false;
    if (query.plunderableOnly && (kind === "truck" || kind === "railway") && !(row.arriveTs != null && Number(row.remainingLootCount ?? Math.max(Number(row.maxLootCount || 0) - Number(row.robTimes || 0), 0)) > 0)) return false;
    if (query.plunderableOnly && kind === "dispatch" && !(Number(row.completionTime || 0) > 0 && Number(row.plunderAt || row.completionTime || 0) > 0 && (Number(row.taskExpireTime || 0) <= 0 || Number(row.taskExpireTime) > fixtureTime) && (Number(row.maxStealCount || 0) <= 0 || Number(row.stolenCount || 0) < Number(row.maxStealCount)))) return false;
    if (query.minLevel != null && Number(row.level) < Number(query.minLevel)) return false;
    if (query.maxLevel != null && Number(row.level) > Number(query.maxLevel)) return false;
    return true;
  });
}

function sortValue(kind, key, row, query) {
  const table = {
    city: { level: () => row.level, health: () => row.health || null, shield: () => Number(row.protectEndTime || 0) > FIXTURE_TIME ? row.protectEndTime : null, updatedAt: () => row.updatedAt },
    resource: { level: () => row.level, updatedAt: () => row.updatedAt },
    monster: { level: () => row.level, distance: () => row.distanceFromHome, updatedAt: () => row.updatedAt },
    truck: { quality: () => row.isSpecialURQuality ? 100 : row.quality, power: () => row.power, itemCount: () => itemCount(row, query.itemKey), remainingLootCount: () => row.remainingLootCount ?? 0, arriveTime: () => row.arriveTs || null, updatedAt: () => row.updatedAt },
    railway: { quality: () => row.quality, power: () => row.power, itemCount: () => itemCount(row, query.itemKey), protectTime: () => row.protectTime || null, updatedAt: () => row.updatedAt },
    dispatch: { level: () => row.level, quality: () => row.isSpecial ? 100 : row.quality, completionTime: () => row.completionTime || null, updatedAt: () => row.updatedAt },
    ghost: { level: () => row.level, quality: () => row.isSpecial ? 100 : row.quality, completionTime: () => row.completionTime || null, updatedAt: () => row.updatedAt },
    treasure: { updatedAt: () => row.updatedAt },
  };
  const getter = table[kind]?.[key];
  if (key === "itemCount" && !query.itemKey) {
    const error = new Error(`unsupported ${kind} sort column '${key}'`);
    error.code = "INVALID_REQUEST";
    throw error;
  }
  if (!getter) {
    const error = new Error(`unsupported ${kind} sort column '${key}'`);
    error.code = "INVALID_REQUEST";
    throw error;
  }
  return getter();
}

function searchFixture(kind, query = {}, fixtureRows, fixtureTime = FIXTURE_TIME) {
  if (query.quality != null && !["n", "r", "sr", "ssr", "ur"].includes(query.quality)) {
    const error = new Error("invalid map quality");
    error.code = "INVALID_REQUEST";
    throw error;
  }
  if (query.completionStatus != null && !["pending", "completed"].includes(query.completionStatus)) {
    const error = new Error("invalid completion status");
    error.code = "INVALID_REQUEST";
    throw error;
  }
  const sorts = Array.isArray(query.sorts) && query.sorts.length ? query.sorts : [{ sortBy: "updatedAt", sortOrder: "desc" }];
  // Validate before filtering: an empty or single-row result must still reject
  // a query that the production Map store rejects.
  for (const sort of sorts) {
    if (!["asc", "desc"].includes(sort.sortOrder)) {
      const error = new Error("invalid map sort order");
      error.code = "INVALID_REQUEST";
      throw error;
    }
    sortValue(kind, sort.sortBy, {}, query);
  }
  const rows = filterRows(kind, query, fixtureRows, fixtureTime);
  rows.sort((left, right) => {
    if (kind === "treasure" && query.luckyFirst) {
      const priority = Number(left.claimPriority ?? 1) - Number(right.claimPriority ?? 1);
      if (priority !== 0) return priority;
    }
    for (const sort of sorts) {
      if (!['asc', 'desc'].includes(sort.sortOrder)) {
        const error = new Error("invalid map sort order");
        error.code = "INVALID_REQUEST";
        throw error;
      }
      const a = sortValue(kind, sort.sortBy, left, query);
      const b = sortValue(kind, sort.sortBy, right, query);
      if (a == null && b != null) return 1;
      if (a != null && b == null) return -1;
      if (a == null && b == null) continue;
      if (a === b) continue;
      const direction = kind === "monster" && sort.sortBy === "distance" ? 1 : sort.sortOrder === "asc" ? 1 : -1;
      return a < b ? -direction : direction;
    }
    return String(left.recordKey).localeCompare(String(right.recordKey));
  });
  const page = Math.max(1, Number.parseInt(query.page, 10) || 1);
  const requestedPageSize = Number.parseInt(query.pageSize, 10);
  const pageSize = Math.min(200, Math.max(1, Number.isFinite(requestedPageSize) && requestedPageSize > 0 ? requestedPageSize : 50));
  const offset = (page - 1) * pageSize;
  return normalizeSearchResult({ rows: rows.slice(offset, offset + pageSize), total: rows.length });
}

function fixtureSummary(scanOverride = {}) {
  return normalizeSummary({
    serverId: FIXTURE_SERVER_ID,
    counts: FIXTURE_COUNTS,
    scanState: {
      serverId: FIXTURE_SERVER_ID,
      serverIdSource: "fixture",
      isReading: false,
      phase: "completed",
      selectedTypes: MAP_KIND_KEYS,
      totalBlocks: 256,
      completedBlocks: 256,
      readBlocks: 256,
      scanMode: "normal",
      progressPercent: 100,
      ...scanOverride,
    },
  });
}

function fixtureOptions(fixtureRows = FIXTURE_ROWS) {
  const cities = fixtureRows.city;
  const countBy = (rows, field) => Object.entries(rows.reduce((counts, row) => { const key = String(row[field] ?? ""); counts[key] = (counts[key] || 0) + 1; return counts; }, {}));
  return normalizeOptions({
    serverId: FIXTURE_SERVER_ID,
    counts: FIXTURE_COUNTS,
    alliances: countBy(cities.filter((row) => row.allianceName), "allianceName").map(([name, count]) => ({ name, count })),
    noAllianceCount: cities.filter((row) => !row.allianceName).length,
    names: {
      resource: countBy(fixtureRows.resource, "resourceNameKey").map(([key, count]) => ({ key, count })),
      monster: countBy(fixtureRows.monster, "monsterNameKey").map(([key, count]) => ({ key, count })),
    },
    dispatchLevels: [5, 6, 7, 8],
    rewardItems: { truck: [{ key: "fixture-reward", name: "Fixture Reward" }, { key: "fixture-medal", name: "Fixture Medal" }], railway: [{ key: "fixture-cargo", name: "Fixture Cargo" }, { key: "fixture-supply", name: "Fixture Supply" }] },
    treasureTypes: [
      { key: 2, treasureType: 2, suppliesType: 0, treasureNameKey: "Fixture Lucky Treasure", name: "Fixture Lucky Treasure", count: 28 },
      { key: 1, treasureType: 1, suppliesType: 0, treasureNameKey: "Fixture Radar Treasure", name: "Fixture Radar Treasure", count: 31 },
    ],
  });
}

function filterLifecycleFixtureRows() {
  const specialCities = [
    { ...baseRow("city", 901), ownerUid: "fixture-sentinel-none", ownerName: "Sentinel Alliance none", allianceName: "none", level: 31, health: 9_100_000, marked: false },
    { ...baseRow("city", 902), ownerUid: "fixture-sentinel-all", ownerName: "Sentinel Alliance all", allianceName: "all", level: 32, health: 9_200_000, marked: false },
    { ...baseRow("city", 903), ownerUid: "fixture-uri-alliance", ownerName: "URI Alliance Commander", allianceName: "A/B 東京 & %", level: 33, health: 9_300_000, marked: false },
  ];
  return Object.fromEntries(MAP_KIND_KEYS.map((kind) => [
    kind,
    kind === "city" ? [...specialCities, ...FIXTURE_ROWS.city] : FIXTURE_ROWS[kind],
  ]));
}

const cache = new Map();

function tableStateFixtureRows(now) {
  return Object.fromEntries(MAP_KIND_KEYS.map(kind => [kind, FIXTURE_ROWS[kind].map((row, index) => {
    if (kind === "resource" && index === 0) return { ...row, rebuildGatherOccupancyKnown: false };
    if (kind === "truck") return { ...row, maxLootCount: 3, robTimes: index === 1 ? 3 : 1, protectTime: index === 0 ? now + 90_000 : 0 };
    if (kind === "dispatch" || kind === "ghost") return { ...row, uuid: String(30000 + index), completionTime: index === 0 ? now + 60_000 : now - 60_000, plunderAt: index === 2 ? now + 60_000 : now - 30_000, taskExpireTime: index === 3 ? now - 1000 : now + 600_000, stolenCount: index === 1 ? 2 : 0, maxStealCount: 2 };
    if (kind === "treasure") return { ...row, worldClaimState: ["charging", "claimable", "depleted", "expired"][index % 4], chargePercent: 0.375, playerClaimState: index % 4 === 1 ? "claimed" : "unclaimed", claimBlockReason: index % 4 === 2 ? "other_alliance" : "" };
    return row;
  })]));
}

function rowActionFixtureRows(fixtureTime) {
  // Synthetic inputs exercise recovered render branches; no native action runs.
  const rows = tableStateFixtureRows(fixtureTime);
  for (const kind of ["truck", "railway"]) {
    rows[kind] = [
      { ...fixtureRow(kind, 1), uuid: "1001", marchUuid: "" },
      { ...fixtureRow(kind, 2), uuid: "1002", marchUuid: "fixture-follow" },
      { ...fixtureRow(kind, 3), uuid: "1003", marchUuid: "fixture-following" },
      { ...fixtureRow(kind, 4), serverId: 0, uuid: "", ownerName: "", allianceName: "", marchUuid: "   " },
    ];
  }
  rows.city = [
    { ...fixtureRow("city", 1), marked: true, trackerState: "missing" },
    { ...fixtureRow("city", 2), marked: false, trackerState: "replaced" },
    { ...fixtureRow("city", 3), marked: true },
    { ...fixtureRow("city", 4), x: 0 },
    { ...fixtureRow("city", 5), y: 2.5 },
  ];
  return rows;
}

function treasureCheckingFixtureRows(now) {
  const rows = tableStateFixtureRows(now);
  // The recovered refreshing flag fills missing states only; known states and
  // blocking reasons keep their existing precedence. All inputs are synthetic.
  rows.treasure = rows.treasure.map((row, index) => index === 0 || index === 2
    ? { ...row, worldClaimState: "", playerClaimState: "", claimBlockReason: index === 2 ? "other_alliance" : "" }
    : row);
  return rows;
}

export function getMapPreviewProvider(bridgeMode, previewState) {
  if (bridgeMode !== "preview" || !String(previewState || "").startsWith("map-")) return null;
  if (cache.has(previewState)) return cache.get(previewState);

  const tableTime = Date.now();
  const headerFixture = scanHeaderFixture(previewState, tableTime);
  const summary = fixtureSummary(previewState === "map-scan-feedback-error" ? { lastError: "MAP_SCAN_START_FAILED" } : headerFixture?.scanState);
  const tableRows = previewState === "map-filter-lifecycle" ? filterLifecycleFixtureRows() : previewState === "map-row-actions" ? rowActionFixtureRows(tableTime) : previewState === "map-treasure-checking" ? treasureCheckingFixtureRows(tableTime) : previewState === "map-table-states" || previewState.startsWith("map-actions-") ? tableStateFixtureRows(tableTime) : null;
  const plunderFixture = plunderFixtureFor(previewState, tableTime) || { dispatchJobs: [], truckJobs: [], online: null, busyKey: "" };
  const api = {
    profileId: "preview-map-profile",
    previewFixture: true,
    summary: async () => summary,
    dataOptions: async () => ({ ...fixtureOptions(tableRows || FIXTURE_ROWS), ...(headerFixture ? { scanProgress: headerFixture.scanProgress } : {}) }),
    search: async (kind, query = {}) => {
      if (previewState === "map-loading") return new Promise(() => {});
      if (previewState === "map-error") {
        const error = new Error("Deterministic browser-only Map query failure");
        error.code = "MAP_FIXTURE_QUERY_FAILED";
        throw error;
      }
      return searchFixture(kind, query, tableRows?.[kind], tableRows ? tableTime : FIXTURE_TIME);
    },
    scanStatus: async () => normalizeScanState(summary.scanState),
    listenScanStatus: () => () => {},
    start: () => blocked("start"),
    stop: () => blocked("stop"),
    clear: () => blocked("clear"),
    jumpServer: () => blocked("server jump"),
    coordinateJump: () => blocked("coordinate jump"),
    setPlayerMark: () => blocked("player mark"),
    exportCities: () => blocked("city export"),
    // Scheduled Plunder: the list is a deterministic offline fixture (empty unless the state names one).
    listPlunderJobs: async () => ({ dispatchJobs: plunderFixture.dispatchJobs, truckJobs: plunderFixture.truckJobs }),
    listenPlunderJobsChanged: () => () => {},
    // Scheduling/sharing/cancel/clear methods exist ONLY for the dedicated action/scheduled fixture states, so
    // the recovered button predicates can be presented; every one is rejected exactly like the other native
    // actions. All other preview states behave like production, where these buttons stay fenced (disabled).
    ...(previewState.startsWith("map-actions-") || previewState.startsWith("map-scheduled") ? {
      scheduleDispatchPlunder: () => blocked("dispatch plunder schedule"),
      scheduleTruckPlunder: () => blocked("truck plunder schedule"),
      shareDispatchToAlliance: () => blocked("dispatch alliance share"),
      cancelDispatchPlunder: () => blocked("dispatch plunder cancel"),
      cancelTruckPlunder: () => blocked("truck plunder cancel"),
      clearDispatchPlunderHistory: () => blocked("dispatch plunder history clear"),
      clearTruckPlunderHistory: () => blocked("truck plunder history clear"),
    } : {}),
  };
  const provider = {
    previewTreasureStatesRefreshing: previewState === "map-treasure-checking",
    previewJumpingKeys: previewState === "map-row-actions" ? { truck: `${FIXTURE_SERVER_ID}:fixture-following`, railway: `${FIXTURE_SERVER_ID}:fixture-following`, city: `${FIXTURE_SERVER_ID}:413:523` } : null,
    // Synthetic labels for browser-only presentation checks, not recovered game text.
    gameTexts: { "100282": "Fixture Resource A", "100281": "Fixture Resource B", "Fixture Monster A": "Fixture Monster A Label", "Fixture Monster B": "Fixture Monster B Label", "Fixture Radar Treasure": "Fixture Radar Treasure Label", "Fixture Lucky Treasure": "Fixture Lucky Treasure Label" , ...(previewState.startsWith("map-scheduled") ? plunderFixtureGameTexts : {}) },
    // Explicit presentation inputs (disclosed in the evidence): they seed render state only and never execute anything.
    previewPlunderOnline: plunderFixture.online,
    previewBusyKey: plunderFixture.busyKey || (previewState === "map-actions-schedule-busy" ? "schedule" : previewState === "map-actions-truck-busy" ? "schedule-truck" : ""),
    previewSharing: previewState === "map-actions-share-busy",
    previewActionMessage: previewState === "map-actions-message" ? { key: "map.shareAllianceSuccess", values: { count: 3 } }
      : previewState === "map-export-feedback" ? { key: "map.exportExcelSuccess", values: { count: 37, path: "C:/Fixture/東京 export.xlsx" } }
      : previewState === "map-actions-message-partial" ? { key: "map.shareAlliancePartial", values: { shared: 2, failed: 1 } } : null,
    scanState: normalizeScanState(summary.scanState),
    summary,
    mapApi: api,
    backendAvailable: true,
    online: false,
    currentServerId: FIXTURE_SERVER_ID,
  };
  cache.set(previewState, provider);
  return provider;
}
