import { MAP_KIND_KEYS, normalizeOptions, normalizeScanState, normalizeSearchResult, normalizeSummary } from "./mapBackend.js";

const FIXTURE_SERVER_ID = 321;
const FIXTURE_TIME = 1_799_000_000_000;
const FIXTURE_COUNTS = Object.freeze(Object.fromEntries(MAP_KIND_KEYS.map((kind, index) => [kind, 52 + index])));

function blocked(action) {
  const error = new Error(`Browser preview cannot execute native Map action: ${action}`);
  error.code = "PREVIEW_NATIVE_ACTION_BLOCKED";
  return Promise.reject(error);
}

function baseRow(kind, index) {
  return {
    serverId: FIXTURE_SERVER_ID,
    recordKey: `${kind}-fixture-${index}`,
    uuid: `${kind}-uuid-${index}`,
    x: 410 + index,
    y: 520 + index,
    updatedAt: FIXTURE_TIME - index * 60_000,
  };
}

function rowsFor(kind, page = 1) {
  const offset = Math.max(0, page - 1) * 2;
  const a = baseRow(kind, offset + 1);
  const b = baseRow(kind, offset + 2);
  switch (kind) {
    case "city":
      return [
        { ...a, ownerUid: "fixture-city-1", ownerName: "Fixture Commander", allianceName: "QA", level: 30, health: 12_500_000, protectEndTime: FIXTURE_TIME + 7_200_000, marked: true },
        { ...b, ownerUid: "fixture-city-2", ownerName: "Second Commander", allianceName: "", level: 28, health: 8_400_000, marked: false },
      ];
    case "resource":
      return [
        { ...a, resourceNameKey: "100281", level: 10, rebuildGatherOccupancyKnown: true, rebuildGatherOccupied: false, resourceRemainingAmount: 180_000, resourceFullAmount: 200_000 },
        { ...b, resourceNameKey: "100282", level: 9, rebuildGatherOccupancyKnown: true, rebuildGatherOccupied: true, resourceRemainingAmount: 75_000, resourceFullAmount: 180_000 },
      ];
    case "monster":
      return [
        { ...a, monsterNameKey: "Fixture Monster A", level: 35, distanceFromHome: 128 },
        { ...b, monsterNameKey: "Fixture Monster B", level: 31, distanceFromHome: 244 },
      ];
    case "truck":
      return [
        { ...a, ownerName: "Truck Owner", allianceName: "QA", quality: 5, power: 22_400_000, currentGoods: [{ name: "Fixture Reward", count: 3 }], robTimes: 0, maxRobTimes: 2, arriveTs: FIXTURE_TIME + 600_000 },
        { ...b, ownerName: "Reindeer Owner", isSpecialURQuality: true, quality: 5, power: 26_100_000, currentGoods: [{ name: "Fixture Medal", count: 8 }], robTimes: 2, maxRobTimes: 2, arriveTs: FIXTURE_TIME + 900_000 },
      ];
    case "railway":
      return [
        { ...a, allianceName: "QA Alliance", quality: 5, power: 31_000_000, currentGoods: [{ name: "Fixture Cargo", count: 4 }], protectTime: FIXTURE_TIME + 300_000 },
        { ...b, allianceAbbr: "TST", quality: 4, power: 27_500_000, currentGoods: [{ name: "Fixture Supply", count: 2 }], protectTime: FIXTURE_TIME + 120_000 },
      ];
    case "dispatch":
      return [
        { ...a, ownerUid: "dispatch-owner-1", ownerName: "Task Owner", level: 8, quality: 5, completionStatus: "pending", rewards: [{ name: "Fixture Intel", count: 2 }], completionTime: FIXTURE_TIME + 1_200_000 },
        { ...b, ownerUid: "dispatch-owner-2", ownerName: "Completed Owner", level: 7, quality: 4, completionStatus: "completed", rewards: [{ name: "Fixture Chest", count: 1 }], completionTime: FIXTURE_TIME - 300_000 },
      ];
    case "ghost":
      return [
        { ...a, ownerUid: "ghost-owner-1", ownerName: "Ghost Owner", level: 6, quality: 5, isSpecial: true, completionStatus: "pending", rewards: [{ name: "Fixture Ghost Reward", count: 5 }], completionTime: FIXTURE_TIME + 600_000 },
        { ...b, ownerUid: "ghost-owner-2", ownerName: "Ghost Ally", level: 5, quality: 4, completionStatus: "completed", rewards: [{ name: "Fixture Token", count: 3 }], completionTime: FIXTURE_TIME - 600_000 },
      ];
    case "treasure":
      return [
        { ...a, treasureNameKey: "Fixture Lucky Treasure", remainingBoxes: 5, worldClaimState: "available", playerClaimState: "eligible", rewardedCount: 2, diggingCount: 1, expireTime: FIXTURE_TIME + 3_600_000, ownerName: "Treasure Owner", allianceAbbr: "QA" },
        { ...b, treasureNameKey: "Fixture Radar Treasure", remainingBoxes: 1, worldClaimState: "digging", playerClaimState: "waiting", rewardedCount: 4, diggingCount: 3, expireTime: FIXTURE_TIME + 1_800_000, ownerName: "Radar Owner", allianceAbbr: "TST" },
      ];
    default:
      return [];
  }
}

function fixtureSummary() {
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
    },
  });
}

function fixtureOptions() {
  return normalizeOptions({
    serverId: FIXTURE_SERVER_ID,
    counts: FIXTURE_COUNTS,
    alliances: [{ name: "QA", count: 21 }, { name: "TST", count: 14 }],
    noAllianceCount: 9,
    names: {
      resource: [{ key: "100281", count: 18 }, { key: "100282", count: 12 }],
      monster: [{ key: "Fixture Monster A", count: 11 }, { key: "Fixture Monster B", count: 8 }],
    },
    dispatchLevels: [5, 6, 7, 8],
    rewardItems: {
      truck: [{ key: "fixture-reward", name: "Fixture Reward" }],
      railway: [{ key: "fixture-cargo", name: "Fixture Cargo" }],
    },
    treasureTypes: [{ key: "lucky", name: "Fixture Lucky Treasure" }, { key: "radar", name: "Fixture Radar Treasure" }],
  });
}

const cache = new Map();

export function getMapPreviewProvider(bridgeMode, previewState) {
  if (bridgeMode !== "preview" || !String(previewState || "").startsWith("map-")) return null;
  if (cache.has(previewState)) return cache.get(previewState);

  const summary = fixtureSummary();
  const api = {
    profileId: "preview-map-profile",
    previewFixture: true,
    summary: async () => summary,
    dataOptions: async () => fixtureOptions(),
    search: async (kind, query = {}) => {
      if (previewState === "map-loading") return new Promise(() => {});
      if (previewState === "map-error") {
        const error = new Error("Deterministic browser-only Map query failure");
        error.code = "MAP_FIXTURE_QUERY_FAILED";
        throw error;
      }
      return normalizeSearchResult({ rows: rowsFor(kind, query.page), total: FIXTURE_COUNTS[kind] || 0 });
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
  };
  const provider = {
    mapApi: api,
    backendAvailable: true,
    online: false,
    currentServerId: FIXTURE_SERVER_ID,
  };
  cache.set(previewState, provider);
  return provider;
}
