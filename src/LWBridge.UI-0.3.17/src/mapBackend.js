export const MAP_COMMANDS = Object.freeze({
  scanStart: "map_scan_start",
  scanStop: "map_scan_stop",
  scanClear: "map_scan_clear",
  scanStatus: "map_scan_status",
  search: "map_search",
  summary: "map_summary",
  dataOptions: "map_data_options",
  status: "get_status",
  proxyStatus: "proxy_status",
  serverJump: "server_jump",
  serverJumpHistorySet: "server_jump_history_set",
  serverJumpHistoryImport: "server_jump_history_import",
});

export const MAP_SCAN_TYPES = Object.freeze([
  { key: "city", label: "Player City" },
  { key: "resource", label: "Resource Point" },
  { key: "monster", label: "Monster" },
  { key: "truck", label: "Truck" },
  { key: "railway", label: "Train" },
  { key: "dispatch", label: "Secret Task" },
  { key: "ghost", label: "Ghost Ops" },
  { key: "treasure", label: "Treasure" },
]);

export const MAP_TABS = Object.freeze([
  { key: "city", label: "City" },
  { key: "resource", label: "Resource" },
  { key: "monster", label: "Monster" },
  { key: "truck", label: "Truck" },
  { key: "railway", label: "Train" },
  { key: "dispatch", label: "Secret Task" },
  { key: "ghost", label: "Ghost Ops" },
  { key: "treasure", label: "Treasure" },
  { key: "scheduledPlunder", label: "Scheduled Plunder" },
]);

export const MAP_KIND_KEYS = Object.freeze(MAP_SCAN_TYPES.map(({ key }) => key));
export const MAP_PAGE_SIZE = 50;

export function mapPageCount(total) {
  return Math.max(1, Math.ceil(Math.max(0, Number(total) || 0) / MAP_PAGE_SIZE));
}

export const DEFAULT_SCAN_STATE = Object.freeze({
  serverId: 0,
  serverIdSource: "none",
  scanRunId: "",
  isReading: false,
  phase: "idle",
  selectedTypes: MAP_KIND_KEYS,
  totalBlocks: 0,
  completedBlocks: 0,
  readBlocks: 0,
  failedBlocks: 0,
  unreadBlocks: 0,
  inflightBlocks: 0,
  scanMode: "normal",
  concurrency: 8,
  scanRate: 0,
  progressPercent: 0,
  nativeCaptureReady: false,
  nativePendingRecords: 0,
  nativeDroppedRecords: 0,
  resumeAvailable: false,
  lastError: null,
  startedAt: 0,
});

export const EMPTY_COUNTS = Object.freeze(
  Object.fromEntries(MAP_KIND_KEYS.map((key) => [key, 0])),
);

function finiteNumber(value, fallback = 0) {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : fallback;
}

function integer(value, fallback = 0) {
  const parsed = Number(value);
  return Number.isInteger(parsed) ? parsed : fallback;
}

export function normalizeScanState(value) {
  const source = value && typeof value === "object" ? value : {};
  const selected = Array.isArray(source.selectedTypes)
    ? [...new Set(source.selectedTypes.filter((kind) => MAP_KIND_KEYS.includes(kind)))]
    : [];
  return {
    ...DEFAULT_SCAN_STATE,
    ...source,
    serverId: integer(source.serverId),
    serverIdSource: typeof source.serverIdSource === "string" ? source.serverIdSource : "none",
    scanRunId: typeof source.scanRunId === "string" ? source.scanRunId : "",
    isReading: source.isReading === true,
    phase: typeof source.phase === "string" ? source.phase : "idle",
    selectedTypes: selected.length ? selected : MAP_KIND_KEYS,
    totalBlocks: integer(source.totalBlocks),
    completedBlocks: integer(source.completedBlocks ?? source.readBlocks),
    readBlocks: integer(source.readBlocks ?? source.completedBlocks),
    failedBlocks: integer(source.failedBlocks),
    unreadBlocks: integer(source.unreadBlocks),
    inflightBlocks: integer(source.inflightBlocks),
    scanMode: source.scanMode === "fast" ? "fast" : "normal",
    concurrency: integer(source.concurrency, source.scanMode === "fast" ? 20 : 8),
    scanRate: finiteNumber(source.scanRate),
    progressPercent: Math.max(0, Math.min(100, finiteNumber(source.progressPercent))),
    nativeCaptureReady: source.nativeCaptureReady === true,
    nativePendingRecords: integer(source.nativePendingRecords),
    nativeDroppedRecords: integer(source.nativeDroppedRecords),
    resumeAvailable: source.resumeAvailable === true,
    lastError: typeof source.lastError === "string" && source.lastError ? source.lastError : null,
    startedAt: finiteNumber(source.startedAt),
    isInWorld: source.isInWorld === true,
    homeServerId: integer(source.homeServerId),
    seasonServerIds: Array.isArray(source.seasonServerIds) ? source.seasonServerIds.map(Number).filter((value) => Number.isInteger(value) && value >= 1 && value <= 99999) : [],
    truckMatchServerIds: Array.isArray(source.truckMatchServerIds) ? source.truckMatchServerIds.map(Number).filter((value) => Number.isInteger(value) && value >= 1 && value <= 99999) : [],
  };
}

export function normalizeCounts(value) {
  const source = value && typeof value === "object" ? value : {};
  return Object.fromEntries(MAP_KIND_KEYS.map((key) => [key, Math.max(0, integer(source[key]))]));
}

export function normalizeSummary(value) {
  const source = value && typeof value === "object" ? value : {};
  const scanState = normalizeScanState(source.scanState);
  return {
    serverId: integer(source.serverId, scanState.serverId),
    counts: normalizeCounts(source.counts),
    scanState,
  };
}

export function normalizeOptions(value) {
  const source = value && typeof value === "object" ? value : {};
  return {
    serverId: integer(source.serverId),
    counts: normalizeCounts(source.counts),
    alliances: Array.isArray(source.alliances) ? source.alliances : [],
    names: {
      resource: Array.isArray(source.names?.resource) ? source.names.resource : [],
      monster: Array.isArray(source.names?.monster) ? source.names.monster : [],
    },
    dispatchLevels: Array.isArray(source.dispatchLevels) ? source.dispatchLevels : [],
    noAllianceCount: Math.max(0, integer(source.noAllianceCount)),
    rewardItems: source.rewardItems && typeof source.rewardItems === "object" ? source.rewardItems : {},
    treasureTypes: Array.isArray(source.treasureTypes) ? source.treasureTypes : [],
    scanProgress: source.scanProgress || null,
  };
}

export function normalizeSearchResult(value) {
  const source = value && typeof value === "object" ? value : {};
  return {
    rows: Array.isArray(source.rows) ? source.rows : [],
    total: Math.max(0, integer(source.total)),
  };
}

export function connectionState(status, proxyStatus, bridgeMode = "native") {
  if (bridgeMode === "preview") return "preview";
  if (bridgeMode !== "native") return "unavailable";
  if (proxyStatus?.gameRunning == null) return "checking";
  if (proxyStatus.gameRunning !== true) return "stopped";
  if (status?.xluaOnline == null) return "checking";
  return status.xluaOnline === true ? "connected" : "disconnected";
}

export function unwrapProfileEvent(value, profileId) {
  if (!value || typeof value !== "object" || !("payload" in value) || !("profileId" in value)) return value;
  if (profileId && value.profileId !== profileId) return null;
  return value.payload;
}

export function normalizeSelectedTypes(types) {
  const selected = [...new Set((types || []).filter((kind) => MAP_KIND_KEYS.includes(kind)))];
  return selected.length ? selected : [...MAP_KIND_KEYS];
}

export function updateSelectedTypes(types, kind, checked) {
  const selected = normalizeSelectedTypes(types);
  if (!MAP_KIND_KEYS.includes(kind)) return selected;
  if (checked) return normalizeSelectedTypes([...selected, kind]);
  if (selected.length === 1 && selected[0] === kind) return selected;
  return selected.filter((entry) => entry !== kind);
}

export function cycleSort(sorts, sortBy) {
  const current = Array.isArray(sorts) ? sorts : [];
  const index = current.findIndex((sort) => sort.sortBy === sortBy);
  if (index < 0) return [{ sortBy, sortOrder: "desc" }, ...current];
  const active = current[index];
  if (active.sortOrder === "desc") {
    return [{ ...active, sortOrder: "asc" }, ...current.filter((_, itemIndex) => itemIndex !== index)];
  }
  return current.filter((_, itemIndex) => itemIndex !== index);
}

export function buildStartPayload(selectedTypes, scanMode) {
  return {
    selectedTypes: normalizeSelectedTypes(selectedTypes),
    scanMode: scanMode === "fast" ? "fast" : "normal",
  };
}

export function buildSearchPayload(kind, query = {}) {
  const page = Math.max(1, integer(query.page, 1));
  const sorts = Array.isArray(query.sorts) && query.sorts.length
    ? query.sorts.map(({ sortBy, sortOrder }) => ({ sortBy, sortOrder: sortOrder === "asc" ? "asc" : "desc" }))
    : [{ sortBy: "updatedAt", sortOrder: "desc" }];
  const normalized = {
    serverId: Math.max(0, integer(query.serverId)),
    keyword: typeof query.keyword === "string" ? query.keyword : "",
    page,
    pageSize: MAP_PAGE_SIZE,
    sorts,
  };
  for (const key of [
    "resourceNameKey", "monsterNameKey", "treasureType", "suppliesType", "alliance",
    "withoutAlliance", "markedOnly", "quality", "specialOnly", "reindeerOnly", "itemKey",
    "completionStatus", "plunderableOnly", "includeForeignRadarTreasures", "luckyFirst",
    "viewerUid", "viewerAllianceId", "minLevel", "maxLevel",
  ]) {
    if (query[key] !== undefined && query[key] !== null && query[key] !== "") normalized[key] = query[key];
  }
  return { kind, query: normalized };
}

export function createMapApi(bridge) {
  const profileId = bridge?.profileId || "";
  const scoped = (payload = {}) => profileId ? { ...payload, profileId } : payload;
  return {
    bridge,
    readStatus: () => bridge.invoke(MAP_COMMANDS.status, scoped()),
    readProxyStatus: () => bridge.invoke(MAP_COMMANDS.proxyStatus, scoped()),
    scanStatus: () => bridge.invoke(MAP_COMMANDS.scanStatus, scoped()).then(normalizeScanState),
    summary: () => bridge.invoke(MAP_COMMANDS.summary, scoped()).then(normalizeSummary),
    dataOptions: (serverId) => bridge.invoke(MAP_COMMANDS.dataOptions, scoped({ serverId })).then(normalizeOptions),
    start: (selectedTypes, scanMode) => bridge.invoke(
      MAP_COMMANDS.scanStart,
      scoped(buildStartPayload(selectedTypes, scanMode)),
    ).then(normalizeScanState),
    stop: () => bridge.invoke(MAP_COMMANDS.scanStop, scoped()).then(normalizeScanState),
    clear: (serverId) => bridge.invoke(MAP_COMMANDS.scanClear, scoped({ serverId })).then(normalizeScanState),
    search: (kind, query) => bridge.invoke(
      MAP_COMMANDS.search,
      scoped(buildSearchPayload(kind, query)),
    ).then(normalizeSearchResult),
    jumpServer: (serverId) => bridge.invoke(MAP_COMMANDS.serverJump, scoped({ serverId })),
    importServerJumpHistory: (history) => bridge.invoke(MAP_COMMANDS.serverJumpHistoryImport, scoped({ history })),
    setServerJumpHistory: (history) => bridge.invoke(MAP_COMMANDS.serverJumpHistorySet, scoped({ history })),
    listenScanStatus: (callback) => bridge.listen("bridge://map-scan-status", (event) => {
      const payload = unwrapProfileEvent(event, profileId);
      if (payload) callback(normalizeScanState(payload));
    }),
    listenStatus: (callback) => bridge.listen("bridge://status", (event) => {
      const payload = unwrapProfileEvent(event, profileId);
      if (payload) callback(payload);
    }),
  };
}
