import { GameAssetImage } from "./GameAssetImage.jsx";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  DEFAULT_SCAN_STATE,
  EMPTY_COUNTS,
  MAP_KIND_KEYS,
  MAP_PAGE_SIZE,
  MAP_SCAN_TYPES,
  MAP_TABS,
  cycleSort,
  mapPageCount,
  normalizeScanState,
  updateSelectedTypes,
} from "./mapBackend.js";
import {
  DEFAULT_AUTO_SCAN_CONFIG,
  advanceAutoScanDeadline,
  appendAutoServerIds,
  autoScanShouldRun,
  autoScanTargetServers,
  formatAutoScanDate,
  parseAutoServerIds,
  removeAutoServerId,
} from "./mapAutoConfig.js";
import { useI18n } from "./i18n.jsx";
import { buildMapColumns, lookupMapText, mapNumber, mapResourceStatus, mapRewardCount, mapRewardName, mapTaskLabel, mapTaskSelectable, mapTaskState } from "./mapTablePresentation.js";
import {
  DEFAULT_RANDOM_DELAY_TEXT,
  SCHEDULING_PROVIDER_METHODS,
  dispatchSelectionPayload,
  isNameFilterKind,
  parseRandomDelay,
  providerSupports,
  removeSharedSelection,
  scheduleSelectedDisabled,
  scheduleTrucksDisabled,
  selectionCount,
  selectionKeySet,
  selectionMembershipKey,
  shareAllianceDisabled,
  shareAllianceLabelKey,
  toggleSelection,
  translateActionError,
} from "./mapInteractions.js";
import { ScheduledPlunder } from "./ScheduledPlunder.jsx";
import { MapTreasureTypeFilter } from "./MapTreasureTypeFilter.jsx";
import { MapRetainedGoodsFilter } from "./MapRetainedGoodsFilter.jsx";
import { mapScanPresentation, scanDateTitle, scanDuration, scanIsoTime, scanSameDay, scanTimeText } from "./mapScanPresentation.js";

const EMPTY_GAME_TEXTS = Object.freeze({});

const DEFAULT_SORTS = Object.freeze(
  Object.fromEntries(MAP_KIND_KEYS.map((kind) => [kind, [{ sortBy: "updatedAt", sortOrder: "desc" }]])),
);
const INCLUDE_FOREIGN_RADAR_STORAGE_KEY = "lwbridge.mapIncludeForeignRadarTreasures";
const LUCKY_TREASURE_STORAGE_KEY = "lwbridge.mapLuckyTreasurePriority";
const SCAN_TYPE_LABEL_KEYS = Object.freeze({
  city: "map.playerCity",
  resource: "map.resourcePoint",
  monster: "map.monster",
  truck: "map.truck",
  railway: "map.allianceTrain",
  dispatch: "map.secretTask",
  ghost: "map.ghostScout",
  treasure: "map.treasure",
});
const TAB_LABEL_KEYS = Object.freeze({
  city: "map.city",
  resource: "map.resource",
  monster: "map.monster",
  truck: "map.truck",
  railway: "map.allianceTrain",
  dispatch: "map.secretTask",
  ghost: "map.ghostScout",
  treasure: "map.treasure",
  scheduledPlunder: "map.scheduledPlunder",
});

function allianceFilterValue(name) {
  return `name:${encodeURIComponent(name)}`;
}

function decodeAllianceFilter(value) {
  if (!String(value || "").startsWith("name:")) return "";
  try {
    return decodeURIComponent(String(value).slice(5));
  } catch {
    return "";
  }
}

function errorText(error) {
  if (!error) return "";
  const code = typeof error.code === "string" ? error.code : "";
  const message = typeof error.message === "string" ? error.message : String(error);
  return [code, message].filter(Boolean).join(": ");
}

function coordinateText(row) {
  const x = Number(row?.x);
  const y = Number(row?.y);
  return Number.isFinite(x) && Number.isFinite(y) ? `${x},${y}` : "-";
}

function MapTable({ kind, rows, loading, sorts, onSort, onCoordinateJump, onPlayerMark, actionBusy, actionDisabled, jumpingKey = "", liveTargetDisabled = true, selectedKeys, onSelect, gameTexts = EMPTY_GAME_TEXTS, itemKey = "", treasureStatesRefreshing = false }) {
  const { language, t } = useI18n();
  const [currentTime, setCurrentTime] = useState(() => Date.now());
  useEffect(() => {
    if (!["truck", "dispatch", "ghost"].includes(kind)) return undefined;
    setCurrentTime(Date.now());
    const timer = window.setInterval(() => setCurrentTime(Date.now()), 1000);
    return () => window.clearInterval(timer);
  }, [kind]);
  const columns = useMemo(() => buildMapColumns(kind, t, language, gameTexts, itemKey, treasureStatesRefreshing), [kind, t, language, gameTexts, itemKey, treasureStatesRefreshing]);
  const columnWidths = columns.map((column) => Number(column.width.match(/\d+/)[0]) + 8);
  const rowKey = (row) => `${kind}:${row.serverId}:${String(row.uuid || row.marchUuid || row.recordKey || `${row.pointIndex || ""}:${row.ownerUid || ""}:${row.updatedAt || ""}`)}`;
  function coordinateCell(row) {
    if (kind === "truck" || kind === "railway") {
      const marchUuid = String(row.marchUuid || "").trim();
      if (!marchUuid) return "-";
      return <button className="map-coordinate-button" type="button" disabled={actionDisabled || actionBusy || liveTargetDisabled} onClick={() => onCoordinateJump(row)}><span className="map-coordinate-icon" aria-hidden="true" /><strong>{t(jumpingKey === `${row.serverId}:${marchUuid}` ? "map.following" : "map.follow")}</strong></button>;
    }
    const x = Number(row.x);
    const y = Number(row.y);
    if (!Number.isInteger(x) || !Number.isInteger(y) || x < 1 || y < 1) return "-";
    return <button className="map-coordinate-button" type="button" disabled={actionDisabled || actionBusy} onClick={() => onCoordinateJump(row)}><span className="map-coordinate-icon" aria-hidden="true" /><span>{coordinateText(row)}</span><strong>{t(jumpingKey === `${row.serverId}:${x}:${y}` ? "map.jumping" : "map.jump")}</strong></button>;
  }
  function markTitle(row) {
    const label = t(row.marked ? "map.unmarkPlayer" : "map.markPlayer");
    const position = row.trackerState === "missing" ? t("map.positionMissing") : row.trackerState === "replaced" ? t("map.positionReplaced") : "";
    return position ? `${label} · ${position}` : label;
  }
  return (
    <div className="map-table-scroll">
      <table className={`map-table map-table--${kind}`} style={{ minWidth: columnWidths.reduce((total, width) => total + width, 0) }} aria-label={t(SCAN_TYPE_LABEL_KEYS[kind])} aria-busy={loading}>
        <colgroup>{columnWidths.map((width, index) => <col key={index} style={{ width }} />)}</colgroup>
        <thead>
          <tr className="map-row map-head">
            {columns.map((column) => {
              const sortIndex = column.sortBy ? sorts.findIndex((sort) => sort.sortBy === column.sortBy) : -1;
              const sort = sortIndex >= 0 ? sorts[sortIndex] : null;
              return (
                <th key={column.label} scope="col" className={column.className || ""} aria-sort={sortIndex === 0 ? (sort.sortOrder === "asc" ? "ascending" : "descending") : undefined}>
                  {column.sortBy ? (
                    <button
                      type="button"
                      className={`map-sort-button${sort ? " active" : ""}`}
                      aria-label={sort ? t("map.sortDescription", { column: column.label, direction: t(sort.sortOrder === "asc" ? "map.sortAscending" : "map.sortDescending"), priority: sortIndex + 1 }) : column.label}
                      onClick={() => onSort(column.sortBy)}
                    >
                      {column.label}
                      {sort ? <span className="map-sort-priority" aria-hidden="true">{sortIndex + 1}</span> : null}
                      <span className="map-sort-arrows" aria-hidden="true">
                        <span className={`map-sort-arrow${sort?.sortOrder === "asc" ? " active" : ""}`}><svg className="ui-icon" viewBox="0 0 16 16" aria-hidden="true" focusable="false"><path d="M8 13.5v-11M3.5 7 8 2.5 12.5 7" /></svg></span>
                        <span className={`map-sort-arrow${sort?.sortOrder === "desc" ? " active" : ""}`}><svg className="ui-icon" viewBox="0 0 16 16" aria-hidden="true" focusable="false"><path d="M8 2.5v11M3.5 9 8 13.5 12.5 9" /></svg></span>
                      </span>
                    </button>
                  ) : column.label}
                </th>
              );
            })}
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr className={`map-row${row.marked === true ? " is-marked" : ""}${row.trackerState === "missing" ? " is-missing" : ""}${row.trackerState === "replaced" ? " is-replaced" : ""}`} key={rowKey(row)}>
              {columns.map((column) => (
                <td key={column.label} className={[column.className, column.rewards ? "map-reward-cell" : ""].filter(Boolean).join(" ")}>
                  {column.select ? (
                    <input type="checkbox" aria-label={t("map.selectNamedTask", { name: String(row.ownerName || row.allianceName || row.uuid), server: row.serverId })} checked={selectedKeys?.has(selectionMembershipKey(row)) || false} disabled={!mapTaskSelectable(kind, row, currentTime)} onChange={() => onSelect?.(row)} />
                  ) : column.mark ? (
                    <button className={`map-mark-button${row.marked ? " active" : ""}`} type="button" disabled={actionDisabled || !row.ownerUid || actionBusy} title={markTitle(row)} aria-label={t(row.marked ? "map.unmarkPlayer" : "map.markPlayer")} onClick={() => onPlayerMark(row)}>
                      <svg className={`ui-icon${row.marked ? " is-filled" : ""}`} viewBox="0 0 16 16" aria-hidden="true" focusable="false"><path d="m8 2 1.8 3.65 4.03.59-2.92 2.84.69 4.02L8 11.2l-3.6 1.9.69-4.02-2.92-2.84 4.03-.59L8 2Z" /></svg>
                    </button>
                  ) : column.coordinate ? (
                    coordinateCell(row)
                  ) : column.action ? (
                    <button className="map-schedule-button" type="button" disabled>{t("map.claimTreasure")}</button>
                  ) : column.status ? (
                    <span className={`map-task-status ${mapTaskState(row, currentTime)}`}>{mapTaskLabel(mapTaskState(row, currentTime), t)}</span>
                  ) : column.rewards ? (
                    !Array.isArray(row[column.rewards]) || row[column.rewards].length === 0 ? "-" : <span className="map-reward-list map-reward-list--retained">{[...row[column.rewards]].sort((left, right) => column.rewards === "currentGoods" && itemKey ? Number(right.key === itemKey) - Number(left.key === itemKey) : 0).map((item, rewardIndex) => { const name = mapRewardName(item, gameTexts); const description = `${name} ×${mapNumber(item.count, language)}`; return <span className="map-reward-item" key={`${item.key}:${rewardIndex}`} title={description} aria-label={description}><GameAssetImage assetPath={item.iconPath} alt={name} className="map-reward-icon" /><strong>×{mapRewardCount(item.count)}</strong></span>; })}</span>
                  ) : kind === "resource" && column.label === t("common.status") ? mapResourceStatus(row, t, gameTexts) : column.value?.(row)}
                </td>
              ))}
            </tr>
          ))}
          {rows.length === 0 ? (
            <tr><td className="map-empty" colSpan={columns.length}>{loading ? t("common.processing") : t("map.empty")}</td></tr>
          ) : null}
        </tbody>
      </table>
    </div>
  );
}

function Pagination({ page, total, onPage }) {
  const { t } = useI18n();
  const totalPages = mapPageCount(total);
  if (totalPages <= 1) return null;
  return (
    <div className="map-pagination">
      <button type="button" disabled={page <= 1} onClick={() => onPage(page - 1)}>{t("map.previousPage")}</button>
      <span>{t("map.pageInfo", { page, total: totalPages })}</span>
      <button type="button" disabled={page >= totalPages} onClick={() => onPage(page + 1)}>{t("map.nextPage")}</button>
    </div>
  );
}

const PREVIEW_TAB_BY_STATE = Object.freeze({
  "map-city": "city",
  "map-resource": "resource",
  "map-monster": "monster",
  "map-truck": "truck",
  "map-railway": "railway",
  "map-dispatch": "dispatch",
  "map-ghost": "ghost",
  "map-treasure": "treasure",
  "map-filter-lifecycle": "city",
  "map-table-states": "treasure",
  "map-treasure-checking": "treasure",
  "map-row-actions": "truck",
  "map-scheduled": "scheduledPlunder",
  "map-scheduled-populated": "scheduledPlunder",
  "map-scheduled-conditional": "scheduledPlunder",
  "map-actions-ready": "dispatch",
  "map-actions-schedule-busy": "dispatch",
  "map-actions-share-busy": "dispatch",
  "map-actions-truck-busy": "truck",
  "map-actions-message": "dispatch",
  "map-actions-message-partial": "dispatch",
});

export function MapDataPage({
  mapApi,
  bridgeMode,
  backendAvailable,
  online,
  activeTab,
  onActiveTabChange,
  currentServerId = 0,
  scanState: suppliedScanState = null,
  summary: suppliedSummary = null,
  onState = null,
  onCounts = null,
  autoScanConfig = DEFAULT_AUTO_SCAN_CONFIG,
  autoScanRunning = false,
  onAutoScanConfig = () => {},
  onAutoScanRunningChange = () => {},
  previewState = "",
  gameTexts = EMPTY_GAME_TEXTS,
  previewJumpingKeys = null,
  previewTreasureStatesRefreshing = false,
  previewBusyKey = "",
  previewSharing = false,
  previewActionMessage = null,
  previewPlunderOnline = null,
}) {
  const { language, t } = useI18n();
  const previewFixture = bridgeMode === "preview" && previewState.startsWith("map-");
  const [scanTab, setScanTab] = useState(previewState.startsWith("map-auto") ? "auto" : "manual");
  const [speed, setSpeed] = useState(() => {
    if (previewFixture) return "normal";
    const saved = window.localStorage.getItem("lwbridge.mapScanMode");
    return saved === "normal" || saved === "fast" ? saved : "normal";
  });
  const [selectedTypes, setSelectedTypes] = useState(() => [...MAP_KIND_KEYS]);
  const [scanState, setScanState] = useState(() => suppliedScanState ? { ...suppliedScanState } : { ...DEFAULT_SCAN_STATE });
  const externallyManaged = suppliedScanState !== null && typeof onState === "function";
  const [scanStateAvailable, setScanStateAvailable] = useState(suppliedScanState !== null);
  const [counts, setCounts] = useState(() => ({ ...EMPTY_COUNTS }));
  const [summaryReady, setSummaryReady] = useState(false);
  const [options, setOptions] = useState(null);
  const [browseServerId, setBrowseServerId] = useState(() => suppliedScanState?.serverId > 0 ? suppliedScanState.serverId : 0);
  const [localTab, setLocalTab] = useState(PREVIEW_TAB_BY_STATE[previewState] || "city");
  const tab = activeTab ?? localTab;
  const [keyword, setKeyword] = useState("");
  const [resourceNameKey, setResourceNameKey] = useState("");
  const [monsterNameKey, setMonsterNameKey] = useState("");
  const [alliance, setAlliance] = useState("all");
  const [markedOnly, setMarkedOnly] = useState(false);
  const [treasureType, setTreasureType] = useState("");
  const [completionStatusByKind, setCompletionStatusByKind] = useState({});
  const [qualityByKind, setQualityByKind] = useState({});
  const [itemKeyByKind, setItemKeyByKind] = useState({});
  const [plunderableOnlyByKind, setPlunderableOnlyByKind] = useState({});
  const [includeForeignRadarTreasures, setIncludeForeignRadarTreasures] = useState(
    () => window.localStorage.getItem(INCLUDE_FOREIGN_RADAR_STORAGE_KEY) === "true",
  );
  const [luckyFirst, setLuckyFirst] = useState(
    () => window.localStorage.getItem(LUCKY_TREASURE_STORAGE_KEY) !== "false",
  );
  const [minLevel, setMinLevel] = useState("");
  // Recovered ownership: Dispatch/Ghost and Truck selections are separate maps of `${serverId}:${uuid}` -> row payload.
  const [dispatchSelection, setDispatchSelection] = useState({});
  const [truckSelection, setTruckSelection] = useState({});
  const [randomDelay, setRandomDelay] = useState(DEFAULT_RANDOM_DELAY_TEXT);
  const [busyKey, setBusyKey] = useState(() => previewFixture ? previewBusyKey : "");
  const [sharing, setSharing] = useState(() => previewFixture && previewSharing === true);
  const [actionMessage, setActionMessage] = useState(() => previewFixture && previewActionMessage?.key ? t(previewActionMessage.key, previewActionMessage.values) : "");
  const [plunderJobs, setPlunderJobs] = useState({ dispatchJobs: [], truckJobs: [] });
  const [currentTime, setCurrentTime] = useState(() => Date.now());
  const [page, setPage] = useState(1);
  const [sortsByKind, setSortsByKind] = useState(() => Object.fromEntries(
    Object.entries(DEFAULT_SORTS).map(([kind, sorts]) => [kind, sorts.map((sort) => ({ ...sort }))]),
  ));
  const [rows, setRows] = useState([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [scanError, setScanError] = useState("");
  const [queryError, setQueryError] = useState("");
  const [actionBusy, setActionBusy] = useState("");
  const [exporting, setExporting] = useState(false);
  const autoConfig = autoScanConfig;
  const autoRunning = autoScanRunning;
  const [autoServerInput, setAutoServerInput] = useState("");
  const autoConfigRef = useRef(autoConfig);
  autoConfigRef.current = autoConfig;
  const [searchRevision, setSearchRevision] = useState(0);
  const [optionsRevision, setOptionsRevision] = useState(0);
  const selectionTouched = useRef(false);
  const previousReading = useRef(false);
  const previousRowsReading = useRef(false);
  const scanProgressTimer = useRef(null);
  const summaryGeneration = useRef(0);
  const optionsGeneration = useRef(0);
  const searchGeneration = useRef(0);
  const tabViewCache = useRef(new Map());
  const dataServerIdRef = useRef(currentServerId);

  const commitScanState = useCallback((next) => {
    setScanState(next);
    if (typeof onState === "function") onState(next);
  }, [onState]);

  const dataServerId = externallyManaged
    ? browseServerId || scanState.serverId || 0
    : summaryReady ? browseServerId : browseServerId || scanState.serverId || currentServerId;
  const activeSorts = sortsByKind[tab] || [{ sortBy: "updatedAt", sortOrder: "desc" }];
  const completionStatus = tab === "dispatch" || tab === "ghost" ? completionStatusByKind[tab] || "" : "";
  const quality = ["truck", "railway", "dispatch", "ghost"].includes(tab) ? qualityByKind[tab] || "" : "";
  const itemKey = tab === "truck" || tab === "railway" ? itemKeyByKind[tab] || "" : "";
  const plunderableOnly = plunderableOnlyByKind[tab] === true;

  useEffect(() => {
    window.localStorage.setItem("lwbridge.mapScanMode", speed);
  }, [speed]);

  useEffect(() => {
    window.localStorage.setItem(INCLUDE_FOREIGN_RADAR_STORAGE_KEY, String(includeForeignRadarTreasures));
  }, [includeForeignRadarTreasures]);

  useEffect(() => {
    window.localStorage.setItem(LUCKY_TREASURE_STORAGE_KEY, String(luckyFirst));
  }, [luckyFirst]);

  const emitAutoConfig = useCallback((patch) => {
    onAutoScanConfig({ ...autoConfigRef.current, ...patch });
  }, [onAutoScanConfig]);

  useEffect(() => {
    if (suppliedScanState === null) return;
    setScanState(suppliedScanState);
    setScanStateAvailable(true);
  }, [suppliedScanState]);

  useEffect(() => {
    if (!externallyManaged) return;
    const matchingCounts = suppliedSummary && dataServerId > 0 && suppliedSummary.serverId === dataServerId
      ? suppliedSummary.counts || { ...EMPTY_COUNTS }
      : null;
    setCounts(matchingCounts || { ...EMPTY_COUNTS });
    setSummaryReady(matchingCounts !== null);
  }, [dataServerId, externallyManaged, suppliedSummary]);

  useEffect(() => {
    if (selectionTouched.current) return;
    setSelectedTypes(scanState.selectedTypes);
  }, [scanState.selectedTypes]);

  const loadOptions = useCallback(async (serverId) => {
    if (!backendAvailable || !serverId) return;
    const generation = optionsGeneration.current + 1;
    optionsGeneration.current = generation;
    try {
      const loaded = await mapApi.dataOptions(serverId);
      if (generation !== optionsGeneration.current) return;
      if (loaded.serverId !== serverId) {
        setBrowseServerId(loaded.serverId);
        return;
      }
      setOptions(loaded);
      setCounts(loaded.counts || { ...EMPTY_COUNTS });
      setSummaryReady(true);
      if (typeof onCounts === "function") onCounts(loaded.serverId, loaded.counts || { ...EMPTY_COUNTS });
      // Original options handler drops a selected Resource/Monster name that is no longer offered.
      setResourceNameKey((current) => !current || loaded.names.resource.some((item) => item.key === current) ? current : "");
      setMonsterNameKey((current) => !current || loaded.names.monster.some((item) => item.key === current) ? current : "");
      setAlliance((current) => {
        if (current === "none") return loaded.noAllianceCount > 0 ? current : "all";
        const name = decodeAllianceFilter(current);
        return !name || loaded.alliances.some((item) => item.name === name) ? current : "all";
      });
      setMinLevel((current) => !current || loaded.dispatchLevels.includes(Number(current)) ? current : "");
      setTreasureType((current) => !current || loaded.treasureTypes.some((item) => item.key === current) ? current : "");
    } catch (error) {
      if (generation === optionsGeneration.current) setQueryError(errorText(error));
    }
  }, [backendAvailable, mapApi, onCounts]);

  const refreshSummary = useCallback(async () => {
    if (!backendAvailable) return;
    const generation = ++summaryGeneration.current;
    try {
      const summary = await mapApi.summary();
      if (generation !== summaryGeneration.current) return;
      commitScanState(summary.scanState);
      setScanStateAvailable(true);
      setCounts(summary.counts);
      setSummaryReady(true);
      setBrowseServerId(summary.serverId > 0 ? summary.serverId : 0);
      if (!selectionTouched.current) {
        setSelectedTypes(summary.scanState.selectedTypes);
        setSpeed(summary.scanState.scanMode);
      }
    } catch (error) {
      if (generation !== summaryGeneration.current) return;
      setScanError(errorText(error));
    }
  }, [backendAvailable, commitScanState, mapApi]);

  useEffect(() => {
    if (externallyManaged || !backendAvailable) return undefined;
    let closed = false;
    previousReading.current = scanState.isReading;
    const unsubscribe = mapApi.listenScanStatus((next) => {
      if (closed) return;
      summaryGeneration.current++;
      const wasReading = previousReading.current;
      previousReading.current = next.isReading;
      commitScanState(next);
      setScanStateAvailable(true);
      setBrowseServerId(next.serverId > 0 ? next.serverId : 0);
      if (wasReading && !next.isReading) refreshSummary();
    });
    refreshSummary();
    const timer = window.setInterval(refreshSummary, 5000);
    return () => {
      closed = true;
      optionsGeneration.current += 1;
      window.clearInterval(timer);
      unsubscribe();
    };
  // scanState is deliberately event-owned after the first subscription.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [backendAvailable, commitScanState, externallyManaged, mapApi, refreshSummary]);

  useEffect(() => {
    if (scanState.serverId === dataServerId) return;
    setBrowseServerId(scanState.serverId > 0 ? scanState.serverId : 0);
  }, [dataServerId, scanState.serverId]);

  useEffect(() => {
    if (dataServerIdRef.current === dataServerId) return;
    dataServerIdRef.current = dataServerId;
    if (dataServerId > 0) optionsGeneration.current += 1;
    searchGeneration.current += 1;
    tabViewCache.current.clear();
    setPage(1);
    setRows([]);
    setTotal(0);
    setLoading(dataServerId > 0);
    setQueryError("");
  }, [dataServerId]);

  useEffect(() => {
    if (!backendAvailable || dataServerId <= 0) return undefined;
    loadOptions(dataServerId);
  }, [backendAvailable, dataServerId, loadOptions, optionsRevision]);

  // Original `rr`: the query uses the keyword of the render that invoked it; typing alone never searches.
  function runSearch(requestedPage = page) {
    if (!backendAvailable || !dataServerId || tab === "scheduledPlunder") return;
    const generation = searchGeneration.current + 1;
    searchGeneration.current = generation;
    setLoading(true);
    setQueryError("");
    const query = {
      serverId: dataServerId,
      keyword,
      page: requestedPage,
      sorts: activeSorts,
    };
    if (tab === "resource" && resourceNameKey) query.resourceNameKey = resourceNameKey;
    if (tab === "monster" && monsterNameKey) query.monsterNameKey = monsterNameKey;
    if (tab === "city") {
      if (alliance === "none") query.withoutAlliance = true;
      else {
        const allianceName = decodeAllianceFilter(alliance);
        if (allianceName) query.alliance = allianceName;
      }
      if (markedOnly) query.markedOnly = true;
    }
    if (tab === "treasure") {
      const selectedTreasureType = options?.treasureTypes?.find((item) => item.key === treasureType);
      if (selectedTreasureType) {
        query.treasureType = selectedTreasureType.treasureType;
        query.suppliesType = selectedTreasureType.suppliesType;
      }
      query.includeForeignRadarTreasures = includeForeignRadarTreasures;
      query.luckyFirst = luckyFirst;
    }
    if (tab === "dispatch" || tab === "ghost" || tab === "truck" || tab === "railway") {
      if (completionStatus && (tab === "dispatch" || tab === "ghost")) query.completionStatus = completionStatus;
      if (quality && quality !== "special" && quality !== "reindeer") query.quality = quality;
      if (quality === "special") query.specialOnly = true;
      if (quality === "reindeer") query.reindeerOnly = true;
      if (itemKey) query.itemKey = itemKey;
      if (plunderableOnly && (tab === "dispatch" || tab === "truck" || tab === "railway")) query.plunderableOnly = true;
      if (minLevel && tab === "dispatch") {
        query.minLevel = Number(minLevel);
        query.maxLevel = Number(minLevel);
      }
    }
    mapApi.search(tab, query).then((result) => {
      if (generation !== searchGeneration.current) return;
      const totalPages = mapPageCount(result.total);
      if (requestedPage > totalPages) {
        setPage(totalPages);
        return;
      }
      setRows(result.rows);
      setTotal(result.total);
    }).catch((error) => {
      if (generation === searchGeneration.current) {
        setRows([]);
        setTotal(0);
        setQueryError(errorText(error));
      }
    }).finally(() => {
      if (generation === searchGeneration.current) setLoading(false);
    });
  }

  // The recovered dependency list has no keyword and no timer: a keyword is applied by the Search button or by
  // any listed dependency changing. Disposal (dependency change, backend loss, provider replacement, unmount)
  // retires the in-flight request; the generation also fences tab and server transitions that advance it earlier.
  // The original data server is state set by its server effect (page reset in the same update), so its first search for a new
  // server already uses page 1. Here the data server is derived, so the render that sees a new server forces page 1 too.
  const serverTransitioning = dataServerIdRef.current !== dataServerId;
  useEffect(() => {
    runSearch(serverTransitioning ? 1 : page);
    return () => { searchGeneration.current += 1; };
  // keyword is deliberately absent (recovered effect dependencies); runSearch closes over the committing render.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [
    activeSorts, alliance, backendAvailable, completionStatus, dataServerId, includeForeignRadarTreasures, itemKey,
    luckyFirst, mapApi, markedOnly, minLevel, monsterNameKey, online, page, plunderableOnly, quality, resourceNameKey,
    searchRevision, tab, treasureType,
  ]);

  // Original Vn/X effects: one coalesced trailing refresh, rearmed only by a
  // reading/readBlocks change; completion refreshes rows and cancels the timer.
  useEffect(() => {
    const wasReading = previousRowsReading.current;
    previousRowsReading.current = scanState.isReading;
    if (wasReading && !scanState.isReading) {
      setSearchRevision((value) => value + 1);
      setOptionsRevision((value) => value + 1);
    }
  }, [scanState.isReading]);

  useEffect(() => {
    if (!scanState.isReading) {
      if (scanProgressTimer.current !== null) window.clearTimeout(scanProgressTimer.current);
      scanProgressTimer.current = null;
      return;
    }
    if (scanProgressTimer.current === null) {
      scanProgressTimer.current = window.setTimeout(() => {
        scanProgressTimer.current = null;
        setSearchRevision((value) => value + 1);
      }, 1000);
    }
  }, [scanState.isReading, scanState.readBlocks]);

  useEffect(() => () => {
    if (scanProgressTimer.current !== null) window.clearTimeout(scanProgressTimer.current);
  }, []);

  // Original page clock (`rn`, 1 s). MapTable keeps its own accepted 1 s clock for the three task tables, so the
  // Recovered page clock also drives elapsed scan time, even on City/Resource.
  useEffect(() => {
    if (!scanState.isReading && !["dispatch", "ghost", "truck", "scheduledPlunder"].includes(tab)) return undefined;
    setCurrentTime(Date.now());
    const timer = window.setInterval(() => setCurrentTime(Date.now()), 1000);
    return () => window.clearInterval(timer);
  }, [tab, scanState.isReading]);

  // Original: the Dispatch/Ghost selection is emptied on every tab change (the Truck selection is not).
  useEffect(() => {
    setDispatchSelection((current) => selectionCount(current) === 0 ? current : {});
    if (tab === "scheduledPlunder") loadPlunderJobs();
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [tab]);

  // Original: jobs load on mount and whenever the provider announces a change; no provider means no jobs.
  useEffect(() => {
    loadPlunderJobs();
    const unsubscribe = typeof mapApi.listenPlunderJobsChanged === "function" ? mapApi.listenPlunderJobsChanged(loadPlunderJobs) : undefined;
    return () => { if (typeof unsubscribe === "function") unsubscribe(); };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [mapApi]);

  async function loadPlunderJobs() {
    if (typeof mapApi.listPlunderJobs !== "function") return;
    try {
      const result = await mapApi.listPlunderJobs();
      setPlunderJobs({
        dispatchJobs: Array.isArray(result?.dispatchJobs) ? result.dispatchJobs : [],
        truckJobs: Array.isArray(result?.truckJobs) ? result.truckJobs : [],
      });
    } catch {
      // The original only logs "dispatch plunder list error"; the canonical page has no log surface.
    }
  }

  async function startScan() {
    setOptions((current) => current ? { ...current, scanProgress: null } : current);
    setScanError("");
    try {
      const next = await mapApi.start(selectedTypes, speed);
      summaryGeneration.current++;
      previousReading.current = next.isReading;
      commitScanState(next);
      setScanStateAvailable(true);
      if (next.serverId > 0) setBrowseServerId(next.serverId);
    } catch (error) {
      setScanError(String(error));
    }
  }

  async function stopScan() {
    setScanError("");
    try {
      const next = await mapApi.stop();
      summaryGeneration.current++;
      previousReading.current = next.isReading;
      commitScanState(next);
      setScanStateAvailable(true);
      if (next.serverId > 0) setBrowseServerId(next.serverId);
      if (!externallyManaged) await refreshSummary();
    } catch (error) {
      setScanError(errorText(error));
    }
  }

  async function clearData() {
    if (!dataServerId) return;
    setScanError("");
    try {
      const next = await mapApi.clear(dataServerId);
      summaryGeneration.current++;
      optionsGeneration.current++;
      searchGeneration.current += 1;
      commitScanState(next);
      setScanStateAvailable(true);
      setCounts({ ...EMPTY_COUNTS });
      if (typeof onCounts === "function") onCounts(next.serverId, { ...EMPTY_COUNTS });
      setSummaryReady(true);
      setOptions(null);
      setAlliance("all");
      setResourceNameKey("");
      setMonsterNameKey("");
      setMinLevel("");
      setItemKeyByKind({});
      setTreasureType("");
      setBrowseServerId(next.serverId);
      tabViewCache.current.clear();
      setRows([]);
      setTotal(0);
      setPage(1);
      setLoading(false);
      setDispatchSelection({});
      setTruckSelection({});
      setSearchRevision((value) => value + 1);
      setOptionsRevision((value) => value + 1);
    } catch (error) {
      setScanError(errorText(error));
    }
  }

  function toggleType(kind, checked) {
    selectionTouched.current = true;
    setSelectedTypes((current) => updateSelectedTypes(current, kind, checked));
  }

  function changeKeyword(value) {
    setKeyword(value);
    // Original keyword onChange: typing drops the selected Resource/Monster name of the active tab.
    if (tab === "resource" && resourceNameKey) setResourceNameKey("");
    if (tab === "monster" && monsterNameKey) setMonsterNameKey("");
  }

  function selectName(kind, value) {
    // Original name onChange: select it, empty the keyword text, go to page 1.
    if (kind === "resource") setResourceNameKey(value);
    else setMonsterNameKey(value);
    setKeyword("");
    setPage(1);
  }

  // Original Search button: on page 1 search directly, otherwise page 1 triggers the effect. Never disabled by loading.
  function submitSearch() {
    if (page === 1) runSearch(1);
    else setPage(1);
  }

  const toggleDispatchRow = (row) => setDispatchSelection((current) => toggleSelection(current, dispatchSelectionPayload(tab, row)));
  const toggleTruckRow = (row) => setTruckSelection((current) => toggleSelection(current, row));

  function changeTab(nextTab) {
    if (nextTab === tab) return;
    searchGeneration.current += 1;
    const outgoingTab = tab === "scheduledPlunder" ? null : tab;
    const incomingTab = nextTab === "scheduledPlunder" ? null : nextTab;
    const incomingCached = incomingTab ? tabViewCache.current.has(incomingTab) : true;
    const currentView = { page, rows, total };
    if (outgoingTab) tabViewCache.current.set(outgoingTab, currentView);
    const restoredView = incomingTab ? tabViewCache.current.get(incomingTab) || { page: 1, rows: [], total: 0 } : currentView;
    if (onActiveTabChange) onActiveTabChange(nextTab);
    else setLocalTab(nextTab);
    setPage(restoredView.page);
    setRows(restoredView.rows);
    setTotal(restoredView.total);
    setLoading(Boolean(incomingTab && !incomingCached));
    setQueryError("");
    setActionMessage("");
  }

  function changeSort(sortBy) {
    setSortsByKind((current) => ({ ...current, [tab]: cycleSort(current[tab], sortBy) }));
    setPage(1);
  }

  function changeItemFilter(value) {
    setItemKeyByKind((current) => ({ ...current, [tab]: value || undefined }));
    if (!value && activeSorts.some((sort) => sort.sortBy === "itemCount")) {
      setSortsByKind((current) => ({ ...current, [tab]: current[tab].filter((sort) => sort.sortBy !== "itemCount") }));
    }
    setPage(1);
  }

  async function coordinateJump(row) {
    setActionBusy(`jump:${row.serverId}:${row.x}:${row.y}`);
    setQueryError("");
    try { await mapApi.coordinateJump(row); }
    catch (error) { setQueryError(errorText(error)); }
    finally { setActionBusy(""); }
  }

  async function togglePlayerMark(row) {
    setActionBusy(`mark:${row.serverId}:${row.ownerUid}`);
    setQueryError("");
    try {
      await mapApi.setPlayerMark(row, !row.marked);
      setSearchRevision((value) => value + 1);
    } catch (error) { setQueryError(errorText(error)); }
    finally { setActionBusy(""); }
  }

  async function exportCities() {
    if (tab !== "city" || !dataServerId) return;
    setExporting(true);
    setActionMessage("");
    try {
      const allianceName = decodeAllianceFilter(alliance);
      const result = await mapApi.exportCities({
        serverId: dataServerId,
        keyword,
        alliance: allianceName || undefined,
        withoutAlliance: alliance === "none" || undefined,
        markedOnly: markedOnly || undefined,
        page: 1,
        pageSize: 200,
        sorts: activeSorts,
      }, {
        headers: [t("map.server"), "X", "Y", t("map.player"), "UID", "UUID", t("map.alliance"), t("map.level"), "HP", t("automation.shieldEnds"), t("map.marked"), t("map.updatedAt")],
        sheetName: t("map.city"),
        yesLabel: t("common.yes"),
        noLabel: t("common.no"),
      });
      if (!result.canceled) setActionMessage(t("map.exportExcelSuccess", { count: result.rowCount, path: result.path }));
    } catch (error) { setActionMessage(translateActionError(t, error)); }
    finally { setExporting(false); }
  }

  async function runAutoCycle() {
    if (autoRunning || scanState.isReading || !online) return;
    onAutoScanRunningChange(true);
    setScanError("");
    let originalServerId = 0;
    try {
      const admissionState = await mapApi.scanStatus();
      originalServerId = admissionState.serverId;
      if (!originalServerId) throw new Error("Current server is unavailable");
      const targets = autoScanTargetServers(autoConfig.serverIds, originalServerId);
      for (const serverId of targets) {
        await mapApi.jumpServer(serverId);
        await mapApi.start(autoConfig.selectedTypes, autoConfig.scanMode);
        const deadline = Date.now() + 2_700_000;
        for (;;) {
          await new Promise((resolve) => window.setTimeout(resolve, 2000));
          if (Date.now() >= deadline) {
            await mapApi.stop();
            throw new Error("Auto scan timed out after 45 minutes");
          }
          const state = await mapApi.scanStatus();
          setScanState(state);
          setScanStateAvailable(true);
          if (!state.isReading) {
            if (state.lastError) setScanError(state.lastError);
            break;
          }
        }
        if (!autoConfigRef.current.enabled) break;
      }
    } catch (error) {
      setScanError(errorText(error));
    } finally {
      if (autoConfig.returnToOriginalServer && originalServerId > 0) {
        try { await mapApi.jumpServer(originalServerId); } catch {}
      }
      onAutoScanConfig(advanceAutoScanDeadline(autoConfigRef.current, Date.now()));
      onAutoScanRunningChange(false);
      await refreshSummary();
    }
  }

  useEffect(() => {
    if (!autoConfig.enabled || !online || scanState.isReading || autoRunning) return undefined;
    const tick = () => { if (autoScanShouldRun(autoConfigRef.current, Date.now(), online, autoRunning, scanState.isReading)) runAutoCycle(); };
    tick();
    const timer = window.setInterval(tick, 5000);
    return () => window.clearInterval(timer);
  }, [autoConfig.enabled, autoConfig.nextRunAt, autoRunning, currentServerId, online, scanState.isReading]);

  function addAutoServers() {
    if (parseAutoServerIds(autoServerInput).length === 0) return;
    emitAutoConfig({ serverIds: appendAutoServerIds(autoConfig.serverIds, autoServerInput) });
    setAutoServerInput("");
  }

  function toggleAutoType(kind, checked) {
    emitAutoConfig({
      selectedTypes: checked
        ? [...autoConfig.selectedTypes, kind]
        : autoConfig.selectedTypes.filter((value) => value !== kind),
    });
  }

  // Recovered action callbacks. They exist only to define selection retention and busy/message ownership; the
  // provider methods they call are absent from the production map API, so the buttons stay fenced (disabled).
  const dispatchCount = selectionCount(dispatchSelection);
  const truckCount = selectionCount(truckSelection);
  const dispatchKeys = useMemo(() => selectionKeySet(dispatchSelection), [dispatchSelection]);
  const truckKeys = useMemo(() => selectionKeySet(truckSelection), [truckSelection]);
  const delay = parseRandomDelay(randomDelay);
  const scheduleProvider = providerSupports(mapApi, SCHEDULING_PROVIDER_METHODS.dispatch);
  const truckProvider = providerSupports(mapApi, SCHEDULING_PROVIDER_METHODS.truck);
  const shareProvider = providerSupports(mapApi, SCHEDULING_PROVIDER_METHODS.share);
  const jobActionsEnabled = providerSupports(mapApi, SCHEDULING_PROVIDER_METHODS.jobActions);
  const scheduledCount = plunderJobs.dispatchJobs.length + plunderJobs.truckJobs.length;

  async function scheduleSelectedDispatch() {
    const selected = Object.values(dispatchSelection);
    if (selected.length === 0 || !delay.valid || !scheduleProvider) return;
    setBusyKey("schedule");
    try {
      await mapApi.scheduleDispatchPlunder(selected, delay.seconds);
      setDispatchSelection({});
      await loadPlunderJobs();
      changeTab("scheduledPlunder");
    } catch {
      // The original logs "dispatch plunder schedule error"; selection is kept.
    } finally {
      setBusyKey("");
    }
  }

  async function scheduleSelectedTrucks() {
    const selected = Object.values(truckSelection);
    if (selected.length === 0 || !truckProvider) return;
    setBusyKey("schedule-truck");
    try {
      await mapApi.scheduleTruckPlunder(selected);
      setTruckSelection({});
      await loadPlunderJobs();
      changeTab("scheduledPlunder");
    } catch {
      // The original logs "truck plunder schedule error"; selection is kept.
    } finally {
      setBusyKey("");
    }
  }

  async function shareSelectedDispatch() {
    const selected = Object.values(dispatchSelection);
    if (selected.length === 0 || !shareProvider) return;
    setSharing(true);
    setActionMessage("");
    try {
      const result = await mapApi.shareDispatchToAlliance(selected);
      setDispatchSelection((current) => removeSharedSelection(current, result.sharedUuids));
      setActionMessage(result.failed > 0 ? t("map.shareAlliancePartial", { shared: result.shared, failed: result.failed }) : t("map.shareAllianceSuccess", { count: result.shared }));
    } catch (error) {
      setActionMessage(translateActionError(t, error));
    } finally {
      setSharing(false);
    }
  }

  async function clearPlunderHistory(kind) {
    setBusyKey(`clear:${kind}`);
    setActionMessage("");
    try {
      const before = Date.now();
      if (kind === "truck") await mapApi.clearTruckPlunderHistory(before);
      else await mapApi.clearDispatchPlunderHistory(before, kind);
      await loadPlunderJobs();
    } catch (error) {
      setActionMessage(translateActionError(t, error));
    } finally {
      setBusyKey("");
    }
  }

  async function cancelDispatchJob(job) {
    setBusyKey(`${job.serverId}:${job.uuid}`);
    try {
      await mapApi.cancelDispatchPlunder(job.serverId, job.taskKind === "ghost" ? `ghost:${job.uuid}` : job.uuid);
      await loadPlunderJobs();
    } catch {
      // The original logs "dispatch plunder cancel error".
    } finally {
      setBusyKey("");
    }
  }

  async function cancelTruckJob(job) {
    setBusyKey(`truck:${job.serverId}:${job.uuid}`);
    try {
      await mapApi.cancelTruckPlunder(job.serverId, job.uuid);
      await loadPlunderJobs();
    } catch {
      // The original logs "truck plunder cancel error".
    } finally {
      setBusyKey("");
    }
  }

  async function plunderTruckAgain(job) {
    setBusyKey(`truck:${job.serverId}:${job.uuid}`);
    try {
      await mapApi.scheduleTruckPlunder([job]);
      await loadPlunderJobs();
    } catch {
      // The original logs "truck plunder reschedule error".
    } finally {
      setBusyKey("");
    }
  }

  const scanView = mapScanPresentation(scanState, options?.scanProgress, dataServerId, currentTime);
  const progress = scanView.progress;

  return (
    <section className="panel map-panel" data-bridge-mode={bridgeMode} data-preview-fixture={previewFixture ? previewState : undefined}>
      <div className="map-scan-tabs" role="tablist" aria-label={t("map.scanModeTabs")}>
        <button type="button" role="tab" className={scanTab === "manual" ? "active" : ""} aria-selected={scanTab === "manual"} onClick={() => setScanTab("manual")}>{t("map.manualScan")}</button>
        <button type="button" role="tab" className={scanTab === "auto" ? "active" : ""} aria-selected={scanTab === "auto"} onClick={() => setScanTab("auto")}>{t("map.autoScan")}</button>
      </div>

      <div className="map-header">
        <h2>{t("map.title")}</h2>
        <div className="map-actions">
          {scanView.start > 0 ? (
            <div className="map-scan-timing">
              <span title={scanDateTitle(scanView.start, language)}><small>{t("map.startTime")}</small><time dateTime={scanIsoTime(scanView.start)}>{scanTimeText(scanView.start)}</time></span>
              <i aria-hidden="true">→</i>
              <span title={scanDateTitle(scanView.end, language)}><small>{t("map.endTime")}</small><time dateTime={scanIsoTime(scanView.end)}>{scanTimeText(scanView.end, !scanSameDay(scanView.start, scanView.end))}</time></span>
              <span className="map-scan-duration"><small>{t("map.totalDuration")}</small><time>{scanDuration(scanView.duration)}</time></span>
            </div>
          ) : null}
          {scanTab === "manual" ? (
            <>
              <fieldset className={`map-speed-toggle${speed === "fast" ? " fast" : ""}`} aria-label={t("map.speed")} disabled={scanState.isReading || autoRunning}>
                <span className="map-speed-slider" aria-hidden="true" />
                <label><input type="radio" name="map-scan-speed" checked={speed === "normal"} onChange={() => { selectionTouched.current = true; setSpeed("normal"); }} /><span>{t("map.normalSpeed")}</span></label>
                <label><input type="radio" name="map-scan-speed" checked={speed === "fast"} onChange={() => { selectionTouched.current = true; setSpeed("fast"); }} /><span>{t("map.fastSpeed")}</span></label>
              </fieldset>
              <button type="button" className={!scanState.isReading ? "primary" : ""} disabled={!online || scanState.isReading || autoRunning} onClick={startScan}>{t("map.startReading")}</button>
              <button type="button" className={scanState.isReading ? "danger" : ""} disabled={!backendAvailable || !scanState.isReading} onClick={stopScan}>{t("common.stop")}</button>
              <button type="button" disabled={!backendAvailable || !dataServerId || scanState.isReading || autoRunning} onClick={clearData}>{t("map.clearServer")}</button>
            </>
          ) : null}
        </div>
      </div>

      {scanTab === "auto" ? (
        <div className="map-auto-scan-card">
          <label className="map-auto-scan-master">
            <input type="checkbox" checked={autoConfig.enabled} onChange={(event) => emitAutoConfig({ enabled: event.target.checked })} />
            <strong>{t("map.enableAutoScan")}</strong>
            <span>{autoRunning ? t("map.autoScanRunning") : autoConfig.enabled ? t("map.autoScanWaiting") : t("map.autoScanDisabled")}</span>
          </label>
          <div className="map-auto-scan-grid">
            <div className="map-auto-scan-server-field">
              <span>{t("map.targetServers")}</span>
              <span className="map-auto-scan-server-input">
                <input
                  value={autoServerInput}
                  inputMode="numeric"
                  placeholder={scanState.serverId > 0 ? String(scanState.serverId) : "8, 15, 120"}
                  onChange={(event) => setAutoServerInput(event.target.value)}
                  onKeyDown={(event) => {
                    if (event.key === "Enter") {
                      event.preventDefault();
                      addAutoServers();
                    }
                  }}
                />
                <button type="button" disabled={parseAutoServerIds(autoServerInput).length === 0} onClick={addAutoServers}>{t("common.add")}</button>
              </span>
              <small>{t("map.targetServersHint")}</small>
              <span className="map-auto-scan-server-chips">
                {autoConfig.serverIds.map((id) => (
                  <span key={id}>
                    {id}
                    <button type="button" aria-label={`${t("common.remove")} ${id}`} onClick={() => emitAutoConfig({ serverIds: removeAutoServerId(autoConfig.serverIds, id) })}>
                      <svg className="ui-icon" viewBox="0 0 16 16" aria-hidden="true" focusable="false">
                        <path d="m4 4 8 8M12 4l-8 8" />
                      </svg>
                    </button>
                  </span>
                ))}
              </span>
            </div>
            <label><span>{t("map.scanIntervalMinutes")}</span><input type="number" min={20} max="1440" value={autoConfig.intervalMinutes} onChange={(event) => emitAutoConfig({ intervalMinutes: Number(event.target.value) })} /></label>
            <label><span>{t("map.speed")}</span><select value={autoConfig.scanMode} onChange={(event) => emitAutoConfig({ scanMode: event.target.value === "fast" ? "fast" : "normal" })}><option value="normal">{t("map.normalSpeed")}</option><option value="fast">{t("map.fastSpeed")}</option></select></label>
          </div>
          <div className="map-controls">
            <span className="map-controls-label">{t("map.scanTypes")}</span>
            <div className="map-types map-types--compact">
              {MAP_SCAN_TYPES.map(({ key }) => (
                <label key={key}><input type="checkbox" checked={autoConfig.selectedTypes.includes(key)} disabled={autoConfig.selectedTypes.length === 1 && autoConfig.selectedTypes[0] === key} onChange={(event) => toggleAutoType(key, event.target.checked)} />{t(SCAN_TYPE_LABEL_KEYS[key])}</label>
              ))}
            </div>
          </div>
          <div className="map-auto-scan-options">
            <label><input type="checkbox" checked={autoConfig.returnToOriginalServer} onChange={(event) => emitAutoConfig({ returnToOriginalServer: event.target.checked })} />{t("map.returnAfterAutoScan")}</label>
            <button type="button" className="primary" disabled={!online || !autoConfig.enabled || autoRunning || scanState.isReading} onClick={() => emitAutoConfig({ nextRunAt: Date.now() })}>{t("map.runAutoScanNow")}</button>
          </div>
          <small>{t("map.autoScanNavigationNotice")}</small>
          <small>{t("map.nextAutoScan")}: {autoConfig.enabled && autoConfig.nextRunAt > 0 ? formatAutoScanDate(autoConfig.nextRunAt, language) : "-"}</small>
        </div>
      ) : null}

      <div className="map-scan-summary">
        <span className={`map-status-pill${scanState.isReading ? " active" : ""}`}>{t(scanView.statusKey)}</span>
        <span>{t("map.server")} <strong>{scanView.serverId || "-"}</strong></span>
        <div className={`map-progress${progress < 50 ? " low" : ""}${scanState.isReading ? " active" : ""}`}>
          <progress className="map-progress-bar" max={100} value={scanStateAvailable ? progress : undefined} aria-label={t("map.scanProgress")} />
          <span>{scanStateAvailable ? `${progress}%` : "—"}</span>
        </div>
      </div>

      {scanError ? <div className="map-scan-error" role="alert">{translateActionError(t, scanError)}</div> : null}
      {!scanError && scanView.error ? <div className="map-scan-error" role="status">{translateActionError(t, scanView.error)}</div> : null}

      {scanTab === "manual" ? (
        <div className="map-controls">
          <span className="map-controls-label">{t("map.scanTypes")}</span>
          <div className="map-types map-types--compact">
            {MAP_SCAN_TYPES.map(({ key }) => (
              <label key={key}>
                <input type="checkbox" checked={selectedTypes.includes(key)} onChange={(event) => toggleType(key, event.target.checked)} />
                <span>{t(SCAN_TYPE_LABEL_KEYS[key])}</span>
              </label>
            ))}
          </div>
        </div>
      ) : null}

      <div className="map-search">
        <div className="map-tabs" role="tablist" aria-label={t("map.title")}>
          {MAP_TABS.map(({ key }) => (
            <button key={key} type="button" role="tab" className={tab === key ? "active" : ""} aria-selected={tab === key} onClick={() => changeTab(key)}>
              <span className="map-tab-label">{t(TAB_LABEL_KEYS[key])}</span>
              <span className="map-tab-count">{key === "scheduledPlunder" ? scheduledCount : summaryReady ? counts[key] || 0 : "—"}</span>
            </button>
          ))}
        </div>

        {tab !== "scheduledPlunder" ? (
          <div className="map-searchbar">
            <input value={keyword} onChange={(event) => changeKeyword(event.target.value)} aria-label={t("map.searchLabel")} placeholder={t("map.searchLabel")} />
            {isNameFilterKind(tab) ? (
              <select aria-label={t("common.name")} value={tab === "resource" ? resourceNameKey : monsterNameKey} onChange={(event) => selectName(tab, event.target.value)}>
                <option value="">{t("map.allNames")}</option>
                {(options?.names?.[tab] || []).map((item) => <option key={item.key} value={item.key}>{lookupMapText(gameTexts, item.key, item.key)} ({item.count})</option>)}
              </select>
            ) : null}
            {tab === "city" ? (
              <>
                <select aria-label={t("map.allianceFilter")} value={alliance} onChange={(event) => { setAlliance(event.target.value); setPage(1); }}>
                  <option value="all">{t("map.allAlliances")}</option>
                  {(options?.noAllianceCount || 0) > 0 ? <option value="none">{t("map.noAlliance")} ({options.noAllianceCount})</option> : null}
                  {(options?.alliances || []).map((item) => <option key={item.name} value={allianceFilterValue(item.name)}>{item.name} ({item.count})</option>)}
                </select>
                <label className="map-filter-field"><input type="checkbox" checked={markedOnly} onChange={(event) => { setMarkedOnly(event.target.checked); setPage(1); }} /> <span>{t("map.markedOnly")}</span></label>
              </>
            ) : null}
            {tab === "treasure" ? (
              <>
                <MapTreasureTypeFilter items={options?.treasureTypes || []} value={treasureType} gameTexts={gameTexts} onChange={(value) => { setTreasureType(value); setPage(1); }} />
                <label className="map-filter-field"><input type="checkbox" checked={includeForeignRadarTreasures} onChange={(event) => { setIncludeForeignRadarTreasures(event.target.checked); setPage(1); }} />{t("map.showForeignRadarTreasures")}</label>
                <label className="map-filter-field"><input type="checkbox" checked={luckyFirst} onChange={(event) => { setLuckyFirst(event.target.checked); setPage(1); }} />{t("map.prioritizeLuckyTreasures")}</label>
              </>
            ) : null}
            {["truck", "railway", "dispatch", "ghost"].includes(tab) ? (
              <>
                {(tab === "dispatch" || tab === "ghost") ? (
                  <select aria-label={t("common.status")} value={completionStatus} onChange={(event) => { setCompletionStatusByKind((current) => ({ ...current, [tab]: event.target.value || undefined })); setPage(1); }}>
                    <option value="">{t("common.status")}</option><option value="completed">{t("common.completed")}</option><option value="pending">{t("common.inProgress")}</option>
                  </select>
                ) : null}
                {tab === "dispatch" ? (
                  <select aria-label={t("map.level")} value={minLevel} onChange={(event) => { setMinLevel(event.target.value); setPage(1); }}>
                    <option value="">{t("squad.afkAnyLevel")}</option>{(options?.dispatchLevels || []).map((level) => <option key={level} value={level}>{level}</option>)}
                  </select>
                ) : null}
                <select aria-label={t("map.quality")} value={quality} onChange={(event) => { setQualityByKind((current) => ({ ...current, [tab]: event.target.value || undefined })); setPage(1); }}>
                  <option value="">{t("map.allQualities")}</option><option value="n">N</option><option value="r">R</option><option value="sr">SR</option><option value="ssr">SSR</option><option value="ur">UR</option>
                  {(tab === "dispatch" || tab === "ghost") ? <option value="special">{t("map.specialQuality")}</option> : null}
                  {tab === "truck" ? <option value="reindeer">{t("map.reindeerQuality")}</option> : null}
                </select>
                {(tab === "truck" || tab === "railway") ? (
                  <MapRetainedGoodsFilter label={t("map.itemFilter")} allLabel={t("map.allRetainedGoods")} items={options?.rewardItems?.[tab] || []} value={itemKey} onChange={changeItemFilter} />
                ) : null}
                {(tab === "truck" || tab === "railway" || tab === "dispatch") ? <label className="map-filter-field map-plunderable-filter"><input type="checkbox" checked={plunderableOnly} onChange={(event) => { setPlunderableOnlyByKind((current) => ({ ...current, [tab]: event.target.checked || undefined })); setPage(1); }} /><span>{t("map.plunderableOnly")}</span></label> : null}
              </>
            ) : null}
            <button type="button" disabled={!backendAvailable || !dataServerId} onClick={submitSearch}>{t("common.search")}</button>
            {tab === "city" ? <button type="button" disabled={!backendAvailable || !dataServerId || scanState.isReading || exporting} onClick={exportCities}>{t(exporting ? "map.exportingExcel" : "map.exportExcel")}</button> : null}
            {(tab === "dispatch" || tab === "ghost") ? <label className="map-random-delay-field"><span>{t("map.randomDelaySeconds")}</span><input type="number" min="0" step="1" aria-label={t("map.randomDelaySeconds")} value={randomDelay} onChange={(event) => setRandomDelay(event.target.value)} /></label> : null}
            {(tab === "dispatch" || tab === "ghost") ? <button className="map-schedule-button" type="button" data-runtime-fenced={!scheduleProvider} disabled={scheduleSelectedDisabled({ count: dispatchCount, busyKey, sharing, delayValid: delay.valid }) || !scheduleProvider} onClick={scheduleSelectedDispatch}>{t("map.scheduleSelected", { count: dispatchCount })}</button> : null}
            {tab === "dispatch" ? <button className="map-schedule-button" type="button" data-runtime-fenced={!shareProvider} disabled={shareAllianceDisabled({ online, isReading: scanState.isReading, count: dispatchCount, busyKey, sharing }) || !shareProvider} onClick={shareSelectedDispatch}>{t(shareAllianceLabelKey(sharing))}</button> : null}
            {tab === "truck" ? <button className="map-schedule-button" type="button" data-runtime-fenced={!truckProvider} disabled={scheduleTrucksDisabled({ count: truckCount, busyKey }) || !truckProvider} onClick={scheduleSelectedTrucks}>{t("map.scheduleSelectedTrucks", { count: truckCount })}</button> : null}
            {tab === "treasure" ? <><button className="map-schedule-button" type="button" disabled>{t("map.claimTreasureBoxes")}</button><button className="map-schedule-button" type="button" disabled>{t("map.claimSeasonTreasures")}</button></> : null}
            {actionMessage ? <span className="map-claim-result" role="status">{actionMessage}</span> : null}
            <span className="map-result-count">{t("common.itemCount", { count: total })}</span>
          </div>
        ) : (
          <div className="map-searchbar">
            {actionMessage ? <span className="map-claim-result" role="status">{actionMessage}</span> : null}
            <span className="map-result-count">{t("common.itemCount", { count: scheduledCount })}</span>
          </div>
        )}
        {queryError ? <div className="map-scan-error" role="alert">{queryError}</div> : null}
        {tab !== "scheduledPlunder" ? (
          <>
            <MapTable kind={tab} gameTexts={gameTexts} itemKey={itemKey} rows={rows} loading={loading} treasureStatesRefreshing={previewFixture && previewState === "map-treasure-checking" && tab === "treasure" && previewTreasureStatesRefreshing === true} sorts={activeSorts} onSort={changeSort} onCoordinateJump={coordinateJump} onPlayerMark={togglePlayerMark} actionBusy={Boolean(actionBusy)} actionDisabled={!online || scanState.isReading} jumpingKey={previewFixture && previewState === "map-row-actions" ? previewJumpingKeys?.[tab] || "" : actionBusy.startsWith("jump:") ? actionBusy.slice(5) : ""} selectedKeys={tab === "truck" ? truckKeys : dispatchKeys} onSelect={tab === "truck" ? toggleTruckRow : toggleDispatchRow} />
            <Pagination page={page} total={total} onPage={setPage} />
          </>
        ) : (
          <ScheduledPlunder
            dispatchJobs={plunderJobs.dispatchJobs}
            truckJobs={plunderJobs.truckJobs}
            gameTexts={gameTexts}
            currentTime={currentTime}
            online={previewFixture && previewPlunderOnline !== null ? previewPlunderOnline : online}
            busyKey={busyKey}
            actionsEnabled={jobActionsEnabled}
            onCancelDispatch={cancelDispatchJob}
            onCancelTruck={cancelTruckJob}
            onPlunderAgain={plunderTruckAgain}
            onClear={clearPlunderHistory}
          />
        )}
      </div>
    </section>
  );
}
