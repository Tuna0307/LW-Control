export const MAP_COMMANDS = Object.freeze({
  scanStart: "map_scan_start",
  scanStop: "map_scan_stop",
  scanClear: "map_scan_clear",
  scanStatus: "map_scan_status",
  search: "map_search",
  summary: "map_summary",
  dataOptions: "map_data_options",
  savedServers: "map_saved_servers",
  status: "get_status",
  proxyStatus: "proxy_status",
  serverJump: "server_jump",
  serverJumpHistorySet: "server_jump_history_set",
  serverJumpHistoryImport: "server_jump_history_import",
  coordinateJump: "map_coordinate_jump",
  marchFollow: "map_march_follow",
  playerMarkSet: "map_player_mark_set",
  cityExport: "map_city_export",
  treasureStateRefresh: "map_treasure_state_refresh",
  treasureStateRefreshAll: "map_treasure_state_refresh_all",
  treasureClaimStatus: "map_treasure_claim_status",
  treasureClaim: "map_treasure_claim",
  treasureClaimPreview: "map_treasure_claim_preview",
  localAutoScanStatus: "local_map_auto_scan_status",
  localAutoScanConfigSet: "local_map_auto_scan_config_set",
  localAutoScanRunNow: "local_map_auto_scan_run_now",
  localAutoScanCancel: "local_map_auto_scan_cancel",
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
  { key: "other", label: "Other" },
  { key: "zombie_boss", label: "Zombie Boss" },
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
  { key: "other", label: "Other" },
  { key: "zombie_boss", label: "Zombie Boss" },
]);

export const MAP_KIND_KEYS = Object.freeze(MAP_SCAN_TYPES.map(({ key }) => key));
// Other and Zombie Boss are opt-in. Preserve the eight recovered types for an implicit scan.
export const MAP_DEFAULT_SCAN_TYPES = Object.freeze(MAP_KIND_KEYS.filter((key) => key !== "other" && key !== "zombie_boss"));
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
  selectedTypes: MAP_DEFAULT_SCAN_TYPES,
  totalBlocks: 0,
  completedBlocks: 0,
  readBlocks: 0,
  failedBlocks: 0,
  unreadBlocks: 0,
  inflightBlocks: 0,
  scanMode: "fast",
  concurrency: 8,
  scanRate: 0,
  progressPercent: 0,
  acquisitionProgressPercent: null,
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
    selectedTypes: selected.length ? selected : [...MAP_DEFAULT_SCAN_TYPES],
    totalBlocks: integer(source.totalBlocks),
    completedBlocks: integer(source.completedBlocks ?? source.readBlocks),
    readBlocks: integer(source.readBlocks ?? source.completedBlocks),
    failedBlocks: integer(source.failedBlocks),
    unreadBlocks: integer(source.unreadBlocks),
    inflightBlocks: integer(source.inflightBlocks),
    scanMode: "fast",
    concurrency: integer(source.concurrency, source.scanMode === "fast" ? 20 : 8),
    scanRate: finiteNumber(source.scanRate),
    progressPercent: Math.max(0, Math.min(100, finiteNumber(source.progressPercent))),
    acquisitionProgressPercent: source.acquisitionProgressPercent == null
      ? null : Math.max(0, Math.min(98, finiteNumber(source.acquisitionProgressPercent))),
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
    otherTypes: Array.isArray(source.otherTypes) ? source.otherTypes : [],
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

export function connectionState(status, proxyStatus, bridgeMode = "native", freshPair = true, error = "") {
  if (bridgeMode === "preview") return "preview";
  if (bridgeMode !== "native") return "unavailable";
  if (error) return "unavailable";
  if (!freshPair) return "checking";
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
  return selected.length ? selected : [...MAP_DEFAULT_SCAN_TYPES];
}

export function updateSelectedTypes(types, kind, checked) {
  const selected = [...new Set((types || []).filter((entry) => MAP_KIND_KEYS.includes(entry)))];
  if (!MAP_KIND_KEYS.includes(kind)) return selected;
  if (checked) return selected.includes(kind) ? selected : [...selected, kind];
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
    // An omitted legacy selection means the recovered default eight kinds.
    // An explicitly unchecked manual selection means no kinds: pass [] to the
    // native admission fence instead of silently launching a full-world scan.
    selectedTypes: Array.isArray(selectedTypes) && selectedTypes.length === 0
      ? [] : normalizeSelectedTypes(selectedTypes),
    scanMode: "fast",
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
    "allServers", "resourceNameKey", "monsterNameKey", "treasureType", "suppliesType", "otherSubtype", "alliance",
    "withoutAlliance", "markedOnly", "quality", "specialOnly", "reindeerOnly", "itemKey",
    "completionStatus", "plunderableOnly", "includeForeignRadarTreasures", "luckyFirst",
    "viewerUid", "viewerAllianceId", "minLevel", "maxLevel",
    "resourceAmountState", "resourceTileState", "escortPowerMaxMillions", "itemKeys",
    "rewardFilters", "rewardMatch", "protectionState",
  ]) {
    if (query[key] !== undefined && query[key] !== null && query[key] !== "") normalized[key] = query[key];
  }
  return { kind, query: normalized };
}

export function createMapApi(bridge) {
  const currentOwner = () => bridge?.currentProfileOwner?.() || {
    profileId: bridge?.profileId || "",
    generation: 0,
  };
  const scoped = (payload = {}) => {
    const profileId = bridge?.profileId || "";
    return profileId ? { ...payload, profileId } : payload;
  };
  const invokeScoped = (command, payload = {}) => bridge?.invokeProfileScoped
    ? bridge.invokeProfileScoped(command, payload)
    : bridge.invoke(command, scoped(payload));
  const listenScoped = (eventName, callback, normalize = (value) => value) => {
    const owner = currentOwner();
    return bridge.listen(eventName, (event) => {
      if (bridge?.isCurrentProfileOwner && !bridge.isCurrentProfileOwner(owner)) return;
      const payload = unwrapProfileEvent(event, bridge?.profileId || "");
      if (payload !== null) callback(normalize(payload));
    });
  };
  return {
    bridge,
    get profileId() { return bridge?.profileId || ""; },
    readStatus: () => invokeScoped(MAP_COMMANDS.status),
    readProxyStatus: () => invokeScoped(MAP_COMMANDS.proxyStatus),
    scanStatus: () => invokeScoped(MAP_COMMANDS.scanStatus).then(normalizeScanState),
    summary: () => invokeScoped(MAP_COMMANDS.summary).then(normalizeSummary),
    dataOptions: (serverId) => invokeScoped(MAP_COMMANDS.dataOptions, { serverId }).then(normalizeOptions),
    savedServers: () => invokeScoped(MAP_COMMANDS.savedServers).then((result) =>
      Array.isArray(result?.servers) ? result.servers.filter((server) => Number.isInteger(server) && server > 0) : []),
    start: (selectedTypes, scanMode) => invokeScoped(
      MAP_COMMANDS.scanStart,
      buildStartPayload(selectedTypes, scanMode),
    ).then(normalizeScanState),
    stop: () => invokeScoped(MAP_COMMANDS.scanStop).then(normalizeScanState),
    clear: (serverId, clearSavedServer = false, allServers = false) => invokeScoped(MAP_COMMANDS.scanClear,
      allServers ? { serverId: 0, allServers: true }
        : clearSavedServer ? { serverId, clearSavedServer: true } : { serverId }).then(normalizeScanState),
    search: (kind, query) => invokeScoped(
      MAP_COMMANDS.search,
      buildSearchPayload(kind, query),
    ).then(normalizeSearchResult),
    jumpServer: (serverId) => invokeScoped(MAP_COMMANDS.serverJump, { serverId }),
    importServerJumpHistory: (history) => invokeScoped(MAP_COMMANDS.serverJumpHistoryImport, { history }),
    setServerJumpHistory: (history) => invokeScoped(MAP_COMMANDS.serverJumpHistorySet, { history }),
    coordinateJump: (row) => invokeScoped(MAP_COMMANDS.coordinateJump, { serverId: row.serverId, x: row.x, y: row.y }),
    marchFollow: (row) => invokeScoped(MAP_COMMANDS.marchFollow, { serverId: row.serverId, marchUuid: String(row.marchUuid) }),
    setPlayerMark: (row, marked) => invokeScoped(MAP_COMMANDS.playerMarkSet, { row, marked }),
    listenPlayerMarkChanged: (callback) => listenScoped("bridge://player-mark-changed", callback),
    exportCities: (query, options) => invokeScoped(MAP_COMMANDS.cityExport, { query, ...options }),
    refreshTreasureStates: (serverId, records) => invokeScoped(
      MAP_COMMANDS.treasureStateRefresh,
      { serverId, records },
    ),
    refreshAllTreasureStates: (serverId) => invokeScoped(
      MAP_COMMANDS.treasureStateRefreshAll,
      { serverId },
    ),
    treasureClaimStatus: () => invokeScoped(MAP_COMMANDS.treasureClaimStatus),
    claimTreasure: (serverId, claimScope, prioritizeLuckySlots, targetUuid = "") => invokeScoped(
      MAP_COMMANDS.treasureClaim,
      { serverId, claimScope, prioritizeLuckySlots, targetUuid },
    ),
    previewTreasureClaim: (serverId, claimScope, prioritizeLuckySlots, targetUuid = "") => invokeScoped(
      MAP_COMMANDS.treasureClaimPreview,
      { serverId, claimScope, prioritizeLuckySlots, targetUuid },
    ),
    autoScanStatus: () => invokeScoped(MAP_COMMANDS.localAutoScanStatus),
    updateAutoScanConfig: (config) => invokeScoped(
      MAP_COMMANDS.localAutoScanConfigSet,
      { config },
    ),
    runAutoScanNow: () => invokeScoped(MAP_COMMANDS.localAutoScanRunNow),
    cancelAutoScan: () => invokeScoped(MAP_COMMANDS.localAutoScanCancel),
    listenAutoScanChanged: (callback) => listenScoped("bridge://local-map-auto-scan-changed", callback),
    listenScanStatus: (callback) => listenScoped("bridge://map-scan-status", callback, normalizeScanState),
    listenStatus: (callback) => listenScoped("bridge://status", callback),
  };
}
