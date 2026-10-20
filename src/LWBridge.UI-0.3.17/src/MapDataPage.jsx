import { GameAssetImage } from "./GameAssetImage.jsx";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  DEFAULT_SCAN_STATE,
  EMPTY_COUNTS,
  MAP_DEFAULT_SCAN_TYPES,
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
  appendAutoServerIds,
  formatAutoScanDate,
  parseAutoServerIds,
  removeAutoServerId,
} from "./mapAutoConfig.js";
import { useI18n } from "./i18n.jsx";
import { buildMapColumns, lookupMapText, mapNumber, mapResourceStatus, mapRewardCount, mapRewardName, mapTaskLabel, mapTaskState, otherTypeName, suppliesNameKey, validMapJumpCoordinates } from "./mapTablePresentation.js";
import { isNameFilterKind, plainErrorDetail, translateActionError } from "./mapInteractions.js";
import { MapTreasureTypeFilter } from "./MapTreasureTypeFilter.jsx";
import { MapRetainedGoodsFilter } from "./MapRetainedGoodsFilter.jsx";
import { MapGhostRewardFilter } from "./MapGhostRewardFilter.jsx";
import { GHOST_REWARD_MATCH_DEFAULT, buildGhostRewardQuery, pruneGhostRewards, setGhostRewardMinimum, toggleGhostReward } from "./mapGhostRewards.js";
import { mapOwnerFilterLabel, parseEscortMillions } from "./mapOwnerFilters.js";
import { mapScanPresentation, scanDateTitle, scanDuration, scanIsoTime, scanSameDay, scanTimeText } from "./mapScanPresentation.js";

const EMPTY_GAME_TEXTS = Object.freeze({});
// Zombie Boss templates exist for levels 5, 10, ... 150 (game table lw_world_monster).
const ZOMBIE_BOSS_LEVELS = Object.freeze(Array.from({ length: 30 }, (_, index) => (index + 1) * 5));

const DEFAULT_SORTS = Object.freeze(
  Object.fromEntries(MAP_KIND_KEYS.map((kind) => [kind, [{ sortBy: "updatedAt", sortOrder: "desc" }]])),
);
const INCLUDE_FOREIGN_RADAR_STORAGE_KEY = "lwbridge.mapIncludeForeignRadarTreasures";
const LUCKY_TREASURE_STORAGE_KEY = "lwbridge.mapLuckyTreasurePriority";
const SCAN_TYPE_LABEL_KEYS = Object.freeze({
  city: "map.playerCity",
  resource: "map.resourcePoint",
  monster: "map.monster",
  zombie_boss: "map.zombieBoss",
  truck: "map.truck",
  railway: "map.allianceTrain",
  dispatch: "map.secretTask",
  ghost: "map.ghostScout",
  treasure: "map.treasure",
  other: "map.other",
});
const TAB_LABEL_KEYS = Object.freeze({
  city: "map.city",
  resource: "map.resource",
  monster: "map.monster",
  zombie_boss: "map.zombieBoss",
  truck: "map.truck",
  railway: "map.allianceTrain",
  dispatch: "map.secretTask",
  ghost: "map.ghostScout",
  treasure: "map.treasure",
  other: "map.other",
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
  return plainErrorDetail(error);
}

function coordinateText(row) {
  const x = Number(row?.x);
  const y = Number(row?.y);
  return Number.isFinite(x) && Number.isFinite(y) ? `${x},${y}` : "-";
}

function MapTable({ kind, rows, loading, sorts, onSort, onCoordinateJump, onPlayerMark, onTreasurePreview = () => {}, treasurePreviewAvailable = false, actionBusy, actionDisabled, markDisabled = actionDisabled, jumpingKey = "", liveTargetDisabled = true, followingKey = "", allServers = false, gameTexts = EMPTY_GAME_TEXTS, itemKey = "", treasureStatesRefreshing = false }) {
  const { language, t } = useI18n();
  const [currentTime, setCurrentTime] = useState(() => Date.now());
  useEffect(() => {
    // The arrival guard below also applies to native Railway and moving Monster
    // rows. Keep their visible Follow eligibility current while the tab is open.
    if (!["truck", "railway", "monster", "zombie_boss", "dispatch", "ghost"].includes(kind)) return undefined;
    setCurrentTime(Date.now());
    const timer = window.setInterval(() => setCurrentTime(Date.now()), 1000);
    return () => window.clearInterval(timer);
  }, [kind]);
  const columns = useMemo(() => {
    const values = buildMapColumns(kind, t, language, gameTexts, itemKey, treasureStatesRefreshing);
    return allServers ? [{ label: t("map.server"), width: "64px", sortBy: "serverId", value: row => row.serverId }, ...values] : values;
  }, [kind, t, language, gameTexts, itemKey, treasureStatesRefreshing, allServers]);
  const rowKey = (row) => `${kind}:${row.serverId}:${String(row.uuid || row.marchUuid || row.recordKey || `${row.pointIndex || ""}:${row.ownerUid || ""}:${row.updatedAt || ""}`)}`;
  function coordinateCell(row) {
    if (kind === "truck" || kind === "railway" || (kind === "monster" && row.marchUuid)) {
      const marchUuid = String(row.marchUuid || "").trim();
      if (!marchUuid) return "-";
      const key = `${row.serverId}:${marchUuid}`;
      const knownArrival = Number(row.arriveTs);
      const arrived = Number.isFinite(knownArrival) && knownArrival > 0 && knownArrival <= currentTime;
      // A stored row can expire while visible. Keep an ongoing Follow's Stop
      // available, but never offer a new Follow after native final arrival.
      return <button className="map-coordinate-button" type="button" disabled={actionBusy || (followingKey !== key && (actionDisabled || liveTargetDisabled || arrived))} onClick={() => onCoordinateJump(row)}><span className="map-coordinate-icon" aria-hidden="true" /><strong>{followingKey === key ? t("common.stop") : t(jumpingKey === key ? "map.following" : "map.follow")}</strong></button>;
    }
    const x = Number(row.x);
    const y = Number(row.y);
    if (!validMapJumpCoordinates(row)) return "-";
    return <button className="map-coordinate-button" type="button" disabled={actionDisabled || actionBusy} onClick={() => onCoordinateJump(row)}><span className="map-coordinate-icon" aria-hidden="true" /><span>{coordinateText(row)}</span><strong>{t(jumpingKey === `${row.serverId}:${x}:${y}` ? "map.jumping" : "map.jump")}</strong></button>;
  }
  function markTitle(row) {
    const label = t(row.marked ? "map.unmarkPlayer" : "map.markPlayer");
    const position = row.trackerState === "missing" ? t("map.positionMissing") : row.trackerState === "replaced" ? t("map.positionReplaced") : "";
    return position ? `${label} · ${position}` : label;
  }
  return (
    <div className="map-table-scroll">
      <table className={`map-table map-table--${kind}`} aria-label={t(SCAN_TYPE_LABEL_KEYS[kind])} aria-busy={loading}>
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
                  {column.mark ? (
                    <button className={`map-mark-button${row.marked ? " active" : ""}`} type="button" disabled={markDisabled || !row.ownerUid || actionBusy} title={markTitle(row)} aria-label={t(row.marked ? "map.unmarkPlayer" : "map.markPlayer")} onClick={() => onPlayerMark(row)}>
                      <svg className={`ui-icon${row.marked ? " is-filled" : ""}`} viewBox="0 0 16 16" aria-hidden="true" focusable="false"><path d="m8 2 1.8 3.65 4.03.59-2.92 2.84.69 4.02L8 11.2l-3.6 1.9.69-4.02-2.92-2.84 4.03-.59L8 2Z" /></svg>
                    </button>
                  ) : column.coordinate ? (
                    coordinateCell(row)
                  ) : column.action ? (
                    <button className="map-schedule-button" type="button" disabled={actionDisabled || actionBusy || !treasurePreviewAvailable} onClick={() => onTreasurePreview("single", String(row.uuid || ""), row.serverId)}>{t("map.previewTreasureClaims")}</button>
                  ) : column.status ? (
                    <span className={`map-task-status ${mapTaskState(row, currentTime)}`}>{mapTaskLabel(mapTaskState(row, currentTime), t)}</span>
                  ) : column.rewards ? (
                    !Array.isArray(row[column.rewards]) || row[column.rewards].length === 0 ? (kind === "ghost" ? <span className="map-reward-unknown" title={t("map.rewardsUnknown")}>{t("map.rewardsUnknown")}</span> : "-") : <span className="map-reward-list map-reward-list--retained">{[...row[column.rewards]].sort((left, right) => column.rewards === "currentGoods" && itemKey ? Number(right.key === itemKey) - Number(left.key === itemKey) : 0).map((item, rewardIndex) => { const name = mapRewardName(item, gameTexts); const description = `${name} ×${mapNumber(item.count, language)}`; return <span className="map-reward-item" key={`${item.key}:${rewardIndex}`} title={description} aria-label={description}><GameAssetImage assetPath={item.iconPath} alt={name} className="map-reward-icon" /><strong>×{mapRewardCount(item.count)}</strong></span>; })}</span>
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
  "map-zombie-boss": "zombie_boss",
  "map-truck": "truck",
  "map-railway": "railway",
  "map-dispatch": "dispatch",
  "map-ghost": "ghost",
  "map-treasure": "treasure",
  "map-filter-lifecycle": "city",
  "map-table-states": "treasure",
  "map-treasure-checking": "treasure",
  "map-row-actions": "truck",
  "map-actions-ready": "dispatch",
  "map-actions-message": "dispatch",
  "map-actions-message-partial": "dispatch",
  "map-providers-ghost": "ghost",
  "map-providers-treasure": "treasure",
});

function mergeTreasureStates(rows, states) {
  const values = Array.isArray(states) ? states : Object.values(states || {});
  const byUuid = new Map(values.map((state) => [String(state?.uuid || ""), state]));
  if (byUuid.size === 0) return rows;
  return rows.map((row) => {
    const state = byUuid.get(String(row?.uuid || ""));
    return state ? { ...row, ...state } : row;
  });
}

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
  autoScanError = "",
  onAutoScanConfig = () => {},
  onAutoScanRunNow = () => {},
  onAutoScanCancel = () => {},
  previewState = "",
  gameTexts = EMPTY_GAME_TEXTS,
  gameTextLoader = null,
  gameTextOwner = "",
  gameTextRoot = "",
  previewJumpingKeys = null,
  previewTreasureStatesRefreshing = false,
  previewBusyKey = "",
  previewActionMessage = null,
}) {
  const { language, t } = useI18n();
  const previewFixture = bridgeMode === "preview" && previewState.startsWith("map-");
  const [scanTab, setScanTab] = useState(previewState.startsWith("map-auto") ? "auto" : "manual");
  const [selectedTypes, setSelectedTypes] = useState(() => [...MAP_DEFAULT_SCAN_TYPES]);
  const [scanState, setScanState] = useState(() => suppliedScanState ? { ...suppliedScanState } : { ...DEFAULT_SCAN_STATE });
  const externallyManaged = suppliedScanState !== null && typeof onState === "function";
  const [scanStateAvailable, setScanStateAvailable] = useState(suppliedScanState !== null);
  const [counts, setCounts] = useState(() => ({ ...EMPTY_COUNTS }));
  const [summaryReady, setSummaryReady] = useState(false);
  const [options, setOptions] = useState(null);
  const [localizedNames, setLocalizedNames] = useState({ context: "", values: EMPTY_GAME_TEXTS, loading: false, error: "" });
  const [localeRetry, setLocaleRetry] = useState(0);
  const localeCache = useRef(new Map());
  const localeRequestGeneration = useRef(0);
  const [browseServerId, setBrowseServerId] = useState(() => suppliedScanState?.serverId > 0 ? suppliedScanState.serverId : 0);
  const [savedServers, setSavedServers] = useState([]);
  const [savedServersRevision, setSavedServersRevision] = useState(0);
  const [localTab, setLocalTab] = useState(PREVIEW_TAB_BY_STATE[previewState] || "city");
  const tab = activeTab ?? localTab;
  const [keyword, setKeyword] = useState("");
  const [resourceNameKey, setResourceNameKey] = useState("");
  const [monsterNameKey, setMonsterNameKey] = useState("");
  const [alliance, setAlliance] = useState("all");
  const [markedOnly, setMarkedOnly] = useState(false);
  const [treasureType, setTreasureType] = useState("");
  const [otherSubtype, setOtherSubtype] = useState("");
  const [completionStatusByKind, setCompletionStatusByKind] = useState({});
  const [qualityByKind, setQualityByKind] = useState({});
  const [itemKeyByKind, setItemKeyByKind] = useState({});
  const [truckItemKeys, setTruckItemKeys] = useState([]);
  // Ghost reward constraints: [{ key, min }] plus an explicit "all" (default) / "any" mode.
  const [ghostRewards, setGhostRewards] = useState([]);
  const [ghostRewardMatch, setGhostRewardMatch] = useState(GHOST_REWARD_MATCH_DEFAULT);
  const [resourceMinLevel, setResourceMinLevel] = useState("");
  const [resourceMaxLevel, setResourceMaxLevel] = useState("");
  const [resourceAmountState, setResourceAmountState] = useState("");
  const [resourceTileState, setResourceTileState] = useState("");
  // Zombie Boss filters: inclusive level range and game-confirmed protection state.
  const [zombieMinLevel, setZombieMinLevel] = useState("");
  const [zombieMaxLevel, setZombieMaxLevel] = useState("");
  const [zombieProtection, setZombieProtection] = useState("");
  const [escortMaxMillions, setEscortMaxMillions] = useState("");
  const [plunderableOnlyByKind, setPlunderableOnlyByKind] = useState({});
  const [includeForeignRadarTreasures, setIncludeForeignRadarTreasures] = useState(
    () => window.localStorage.getItem(INCLUDE_FOREIGN_RADAR_STORAGE_KEY) === "true",
  );
  const [luckyFirst, setLuckyFirst] = useState(
    () => window.localStorage.getItem(LUCKY_TREASURE_STORAGE_KEY) !== "false",
  );
  const [minLevel, setMinLevel] = useState("");
  const [busyKey, setBusyKey] = useState(() => previewFixture ? previewBusyKey : "");
  const [actionMessage, setActionMessage] = useState(() => previewFixture && previewActionMessage?.key ? t(previewActionMessage.key, previewActionMessage.values) : "");
  const [currentTime, setCurrentTime] = useState(() => Date.now());
  const [page, setPage] = useState(1);
  const [sortsByKind, setSortsByKind] = useState(() => Object.fromEntries(
    Object.entries(DEFAULT_SORTS).map(([kind, sorts]) => [kind, sorts.map((sort) => ({ ...sort }))]),
  ));
  const [rows, setRows] = useState([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [treasureStatesRefreshing, setTreasureStatesRefreshing] = useState(false);
  const [scanError, setScanError] = useState("");
  const [queryError, setQueryError] = useState("");
  const [actionBusy, setActionBusy] = useState("");
  const [following, setFollowing] = useState(null);
  const followGeneration = useRef(0);
  const [exporting, setExporting] = useState(false);
  const localeContext = `${gameTextOwner}|${gameTextRoot}|${language}`;
  const localeContextRef = useRef(localeContext);
  localeContextRef.current = localeContext; // retire old owner before effects run
  const knownNameKeys = useMemo(() => [...new Set([
    ...(options?.names?.resource || []).map((item) => item.key),
    ...(options?.names?.monster || []).map((item) => item.key),
    // WorldSuppliesPoint config type has a game-owned localization key even
    // when a retained row has no optional treasureNameKey. Request that key
    // from the verified installed-build locale loader before showing labels.
    ...(options?.treasureTypes || []).map((item) => suppliesNameKey(item.suppliesType) || item.treasureNameKey),
    ...rows.map((row) => row.kind === "other" ? row.otherNameKey :
      suppliesNameKey(row.suppliesType) || row.resourceNameKey || row.monsterNameKey || row.treasureNameKey),
    // Some native reward names (e.g. Skill Medal) are game text keys: load them with the other labels.
    ...(options?.rewardItems?.ghost || []).map((item) => /^\d+$/.test(String(item?.name ?? "")) ? String(item.name) : ""),
    ...rows.flatMap((row) => Array.isArray(row.rewards) ? row.rewards.map((reward) => /^\d+$/.test(String(reward?.name ?? "")) ? String(reward.name) : "") : []),
    "activity_wajueji_27000_name",
    "300039", "372138",
  ].filter((key) => typeof key === "string" && key.trim().length > 0))].sort(), [options, rows]);
  const nameKeySignature = knownNameKeys.join("\u0000");
  const resolvedGameTexts = previewFixture ? gameTexts
    : localizedNames.context === localeContext ? localizedNames.values : EMPTY_GAME_TEXTS;
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
  const treasureRefreshGeneration = useRef(0);
  const previewOperationRef = useRef(null);
  const tabViewCache = useRef(new Map());
  const dataServerIdRef = useRef(currentServerId);
  // An old page may have a held native reply while its replacement mounts.
  // Never let that reply update App-level counts (setState alone on an
  // unmounted page is harmless but an onCounts callback is not).
  const mountedOwnerRef = useRef(true);
  useEffect(() => {
    mountedOwnerRef.current = true;
    return () => { mountedOwnerRef.current = false; };
  }, []);

  const commitScanState = useCallback((next) => {
    setScanState(next);
    if (typeof onState === "function") onState(next);
  }, [onState]);

  const allServers = browseServerId === "all";
  const dataServerId = allServers ? 0 : externallyManaged
    ? browseServerId || scanState.serverId || 0
    : summaryReady ? browseServerId : browseServerId || scanState.serverId || currentServerId;
  const dataAvailable = allServers || dataServerId > 0;
  useEffect(() => {
    // On owner remount this is cleared before the first asynchronous read,
    // including errors/empty stores/offline transitions.
    setSavedServers([]);
    if (!backendAvailable || scanState.isReading || typeof mapApi?.savedServers !== "function") {
      setBrowseServerId(scanState.serverId > 0 ? scanState.serverId : 0);
      return undefined;
    }
    let retired = false;
    mapApi.savedServers().then((servers) => {
      if (retired || !mountedOwnerRef.current) return;
      const available = Array.isArray(servers)
        ? [...new Set(servers.filter((server) => Number.isSafeInteger(server) && server > 0))]
        : [];
      setSavedServers(available);
      setBrowseServerId((current) => current === "all" && available.length ? current : available.includes(current) ? current :
        (available.includes(scanState.serverId) ? scanState.serverId : available[0] ?? 0));
    }).catch(() => {
      if (retired || !mountedOwnerRef.current) return;
      setSavedServers([]);
      setBrowseServerId(0);
    });
    return () => { retired = true; };
  }, [backendAvailable, mapApi, scanState.serverId, scanState.isReading, savedServersRevision]);
  useEffect(() => {
    if (!gameTextLoader || !gameTextOwner || !gameTextRoot || !backendAvailable) return undefined;
    const generation = ++localeRequestGeneration.current;
    let retired = false;
    const current = () => !retired && generation === localeRequestGeneration.current && localeContextRef.current === localeContext;
    const cache = localeCache.current;
    if (cache.size > 3 && !cache.has(localeContext)) cache.clear();
    const entry = cache.get(localeContext) || { values: {}, identity: "" };
    cache.set(localeContext, entry);
    setLocalizedNames({ context: localeContext, values: { ...entry.values }, loading: true, error: "" });
    (async () => {
      try {
        // Even if all requested names are cached, check the installed build and source
        // hash. A new game update at the same root must invalidate old translations.
        const header = await gameTextLoader({ language, keys: [] });
        if (!current()) return;
        const accepted = (result) => {
          if (result?.language !== language || !result?.buildId || !result?.localeSha256 ||
              !result?.englishSha256 || typeof result?.texts !== "object" || !result.texts ||
              String(result.gameRoot || "").toLowerCase().replace(/[\\/]+$/, "") !==
                gameTextRoot.toLowerCase().replace(/[\\/]+$/, "")) {
            throw new Error("Game names came from a different or unverified game build/root/language");
          }
          return `${result.buildId}|${result.localeSha256}|${result.englishSha256}`;
        };
        const identity = accepted(header);
        if (entry.identity !== identity) {
          entry.identity = identity;
          entry.values = {};
        }
        const missing = knownNameKeys.filter((key) => !Object.hasOwn(entry.values, key));
        for (let offset = 0; offset < missing.length; offset += 200) {
          const batch = missing.slice(offset, offset + 200);
          const result = await gameTextLoader({ language, keys: batch });
          if (!current()) return;
          if (accepted(result) !== entry.identity || batch.some((key) => typeof result.texts[key] !== "string")) {
            throw new Error("Game locale changed during name loading or omitted a requested key");
          }
          entry.values = { ...entry.values, ...result.texts };
          setLocalizedNames({ context: localeContext, values: { ...entry.values }, loading: true, error: "" });
        }
        if (current()) setLocalizedNames({ context: localeContext, values: { ...entry.values }, loading: false, error: "" });
      } catch (error) {
        if (current()) setLocalizedNames({ context: localeContext, values: { ...entry.values }, loading: false, error: translateActionError(t, error) });
      }
    })();
    return () => { retired = true; localeRequestGeneration.current += 1; };
  // A stable signature avoids reloading when the option array is recreated unchanged.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [gameTextLoader, gameTextOwner, gameTextRoot, backendAvailable, language, localeContext, nameKeySignature, localeRetry]);
  // Updated during render, so even a reply preceding React's effect cleanup
  // cannot publish into a replacement tab, API owner or browse server.
  const previewContextRef = useRef(null);
  previewContextRef.current = { mapApi, serverId: dataServerId, tab };
  const activeSorts = sortsByKind[tab] || [{ sortBy: "updatedAt", sortOrder: "desc" }];
  const completionStatus = tab === "dispatch" || tab === "ghost" ? completionStatusByKind[tab] || "" : "";
  const quality = ["truck", "railway", "dispatch", "ghost"].includes(tab) ? qualityByKind[tab] || "" : "";
  const itemKey = tab === "truck" ? truckItemKeys[0] || "" : tab === "railway" ? itemKeyByKind[tab] || "" : "";
  const plunderableOnly = plunderableOnlyByKind[tab] === true;

  useEffect(() => {
    window.localStorage.setItem(INCLUDE_FOREIGN_RADAR_STORAGE_KEY, String(includeForeignRadarTreasures));
  }, [includeForeignRadarTreasures]);

  useEffect(() => {
    window.localStorage.setItem(LUCKY_TREASURE_STORAGE_KEY, String(luckyFirst));
  }, [luckyFirst]);

  const emitAutoConfig = useCallback((patch, operation = null) => {
    onAutoScanConfig(patch, operation);
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
    if (matchingCounts) {
      setCounts(matchingCounts);
      setSummaryReady(true);
    } else if (options?.serverId !== dataServerId) {
      // A live home-server summary is not authoritative for a retained
      // remote dataset. Preserve genuine remote options/counts instead of
      // blanking them on every parent summary tick after Auto returns.
      setCounts({ ...EMPTY_COUNTS });
      setSummaryReady(false);
    }
  }, [dataServerId, externallyManaged, options?.serverId, suppliedSummary]);

  useEffect(() => {
    if (selectionTouched.current) return;
    setSelectedTypes(scanState.selectedTypes);
  }, [scanState.selectedTypes]);

  const loadOptions = useCallback(async (serverId) => {
    if (!backendAvailable || (serverId <= 0 && !allServers)) return;
    const generation = optionsGeneration.current + 1;
    optionsGeneration.current = generation;
    try {
      const loaded = await mapApi.dataOptions(serverId);
      if (generation !== optionsGeneration.current || !mountedOwnerRef.current) return;
      if (loaded.serverId !== serverId) {
        // Never adopt a server supplied by a stale/unrelated options reply.
        setOptions(null);
        setSummaryReady(false);
        return;
      }
      setOptions(loaded);
      setCounts(loaded.counts || { ...EMPTY_COUNTS });
      setSummaryReady(true);
      if (!allServers && typeof onCounts === "function") onCounts(loaded.serverId, loaded.counts || { ...EMPTY_COUNTS });
      // Original options handler drops a selected Resource/Monster name that is no longer offered.
      setResourceNameKey((current) => !current || loaded.names.resource.some((item) => item.key === current) ? current : "");
      setMonsterNameKey((current) => !current || loaded.names.monster.some((item) => item.key === current) ? current : "");
      setAlliance((current) => {
        if (current === "none") return loaded.noAllianceCount > 0 ? current : "all";
        const name = decodeAllianceFilter(current);
        return !name || loaded.alliances.some((item) => item.name === name) ? current : "all";
      });
      setMinLevel((current) => !current || loaded.dispatchLevels.includes(Number(current)) ? current : "");
      // A reward selected for one saved server must never constrain another server's tasks.
      setGhostRewards((current) => pruneGhostRewards(current, loaded.rewardItems?.ghost));
      setTreasureType((current) => !current || loaded.treasureTypes.some((item) => item.key === current) ? current : "");
      setOtherSubtype((current) => !current || (loaded.otherTypes || []).some((item) => item.key === current) ? current : "");
    } catch (error) {
      if (generation === optionsGeneration.current && mountedOwnerRef.current) setQueryError(translateActionError(t, error));
    }
  }, [backendAvailable, mapApi, onCounts, allServers]);

  const refreshSummary = useCallback(async () => {
    if (!backendAvailable) return;
    const generation = ++summaryGeneration.current;
    try {
      const summary = await mapApi.summary();
      if (generation !== summaryGeneration.current || !mountedOwnerRef.current) return;
      commitScanState(summary.scanState);
      setScanStateAvailable(true);
      setCounts(summary.counts);
      setSummaryReady(true);
      // A summary describes the live scanner, not the dataset the owner chose.
      // An active scan may establish its target; idle polls and Auto's home
      // return must preserve a separately selected saved server.
      if (summary.scanState?.isReading && summary.scanState.serverId > 0) {
        setBrowseServerId(summary.scanState.serverId);
      }
      if (!selectionTouched.current) {
        setSelectedTypes(summary.scanState.selectedTypes);
      }
    } catch (error) {
      if (generation !== summaryGeneration.current || !mountedOwnerRef.current) return;
      setScanError(translateActionError(t, error));
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
      if (next.isReading && next.serverId > 0) setBrowseServerId(next.serverId);
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
    const viewKey = allServers ? "all" : dataServerId;
    if (dataServerIdRef.current === viewKey) return;
    dataServerIdRef.current = viewKey;
    setGhostRewards((current) => current.length === 0 ? current : []);
    optionsGeneration.current += 1;
    searchGeneration.current += 1;
    treasureRefreshGeneration.current += 1;
    setTreasureStatesRefreshing(false);
    tabViewCache.current.clear();
    setPage(1);
    setRows([]);
    setTotal(0);
    setLoading(dataAvailable);
    setQueryError("");
  }, [dataServerId, allServers, dataAvailable]);

  useEffect(() => {
    if (!backendAvailable || !dataAvailable) return undefined;
    loadOptions(dataServerId);
  }, [backendAvailable, dataAvailable, dataServerId, loadOptions, optionsRevision]);

  // Original `rr`: the query uses the keyword of the render that invoked it; typing alone never searches.
  function runSearch(requestedPage = page) {
    const traceSearch = (stage, detail = "") => {
      if (Array.isArray(window.__map014SearchEvents)) {
        window.__map014SearchEvents.push({ stage, detail, tab, dataServerId, page: requestedPage, at: Date.now() });
        if (window.__map014SearchEvents.length > 100) window.__map014SearchEvents.shift();
      }
    };
    traceSearch("entered", `${backendAvailable}:${tab}`);
    if (!backendAvailable || !dataAvailable) return;
    const generation = searchGeneration.current + 1;
    searchGeneration.current = generation;
    setLoading(true);
    setQueryError("");
    const query = {
      serverId: dataServerId,
      allServers,
      keyword,
      page: requestedPage,
      sorts: activeSorts,
    };
    if (tab === "resource") {
      if (resourceNameKey) query.resourceNameKey = resourceNameKey;
      if (resourceMinLevel !== "") query.minLevel = Number(resourceMinLevel);
      if (resourceMaxLevel !== "") query.maxLevel = Number(resourceMaxLevel);
      if (resourceAmountState) query.resourceAmountState = resourceAmountState;
      if (resourceTileState) query.resourceTileState = resourceTileState;
    }
    if (tab === "monster" && monsterNameKey) query.monsterNameKey = monsterNameKey;
    if (tab === "zombie_boss") {
      if (zombieMinLevel !== "") query.minLevel = Number(zombieMinLevel);
      if (zombieMaxLevel !== "") query.maxLevel = Number(zombieMaxLevel);
      if (zombieProtection) query.protectionState = zombieProtection;
    }
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
    if (tab === "other" && otherSubtype) query.otherSubtype = otherSubtype;
    if (tab === "dispatch" || tab === "ghost" || tab === "truck" || tab === "railway") {
      if (completionStatus && (tab === "dispatch" || tab === "ghost")) query.completionStatus = completionStatus;
      if (quality && quality !== "special" && quality !== "reindeer") query.quality = quality;
      if (quality === "special") query.specialOnly = true;
      if (quality === "reindeer") query.reindeerOnly = true;
      if (tab === "truck" && truckItemKeys.length) query.itemKeys = truckItemKeys;
      else if (itemKey) query.itemKey = itemKey;
      if (tab === "ghost") {
        const reward = buildGhostRewardQuery(ghostRewards, ghostRewardMatch);
        if (!reward.valid) {
          setQueryError(t("map.ghostRewardInvalidMinimum"));
          setLoading(false);
          return;
        }
        Object.assign(query, reward.fragment);
      }
      if (tab === "truck" && escortMaxMillions !== "") {
        const power = parseEscortMillions(escortMaxMillions);
        if (!power.valid) {
          setQueryError(mapOwnerFilterLabel(language, "invalidPower"));
          setLoading(false);
          return;
        }
        query.escortPowerMaxMillions = power.value;
      }
      if (plunderableOnly && (tab === "dispatch" || tab === "truck" || tab === "railway")) query.plunderableOnly = true;
      if (minLevel && tab === "dispatch") {
        query.minLevel = Number(minLevel);
        query.maxLevel = Number(minLevel);
      }
    }
    traceSearch("issued", JSON.stringify(query));
    mapApi.search(tab, query).then((result) => {
      traceSearch("resolved", `total:${result?.total},stale:${generation !== searchGeneration.current}`);
      if (generation !== searchGeneration.current || !mountedOwnerRef.current) return;
      const totalPages = mapPageCount(result.total);
      if (requestedPage > totalPages) {
        setPage(totalPages);
        return;
      }
      setRows(result.rows);
      setTotal(result.total);
      if (
        tab === "treasure" && online && !scanState.isReading &&
        scanState.serverId === dataServerId && result.rows.length > 0 &&
        typeof mapApi.refreshTreasureStates === "function"
      ) {
        const refreshGeneration = treasureRefreshGeneration.current + 1;
        treasureRefreshGeneration.current = refreshGeneration;
        setTreasureStatesRefreshing(true);
        mapApi.refreshTreasureStates(dataServerId, result.rows).then((refreshed) => {
          if (refreshGeneration === treasureRefreshGeneration.current) {
            setRows((current) => mergeTreasureStates(current, refreshed?.states));
          }
        }).catch((error) => {
          const message = String(error);
          if (!message.includes("SCAN_RUNNING") && !message.includes("stop the map scan first")) {
            // Recovered source logs this failure only; the canonical page has no log surface.
          }
        }).finally(() => {
          if (refreshGeneration === treasureRefreshGeneration.current) setTreasureStatesRefreshing(false);
        });
      } else if (tab === "treasure") {
        setTreasureStatesRefreshing(false);
      }
    }).catch((error) => {
      traceSearch("rejected", errorText(error));
      if (generation === searchGeneration.current && mountedOwnerRef.current) {
        if (tab === "treasure") setTreasureStatesRefreshing(false);
        setRows([]);
        setTotal(0);
        // An aggregate Saved-server query can fail independently of its cached
        // options. Surface the real rejection instead of claiming zero rows.
        setQueryError(translateActionError(t, error));
      }
    }).finally(() => {
      if (generation === searchGeneration.current && mountedOwnerRef.current) setLoading(false);
    });
  }

  // The recovered dependency list has no keyword and no timer: a keyword is applied by the Search button or by
  // any listed dependency changing. Disposal (dependency change, backend loss, provider replacement, unmount)
  // retires the in-flight request; the generation also fences tab and server transitions that advance it earlier.
  // The original data server is state set by its server effect (page reset in the same update), so its first search for a new
  // server already uses page 1. Here the data server is derived, so the render that sees a new server forces page 1 too.
  const serverTransitioning = dataServerIdRef.current !== (allServers ? "all" : dataServerId);
  useEffect(() => {
    runSearch(serverTransitioning ? 1 : page);
    return () => { searchGeneration.current += 1; };
  // keyword is deliberately absent (recovered effect dependencies); runSearch closes over the committing render.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [
    activeSorts, alliance, allServers, dataAvailable, backendAvailable, completionStatus, dataServerId, includeForeignRadarTreasures, itemKey, truckItemKeys,
    ghostRewards, ghostRewardMatch,
    luckyFirst, mapApi, markedOnly, minLevel, monsterNameKey, online, page, plunderableOnly, quality, resourceNameKey,
    resourceMinLevel, resourceMaxLevel, resourceAmountState, resourceTileState, escortMaxMillions, language, searchRevision, tab, treasureType, otherSubtype,
    zombieMinLevel, zombieMaxLevel, zombieProtection,
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
    if (!scanState.isReading && !["dispatch", "ghost", "truck"].includes(tab)) return undefined;
    setCurrentTime(Date.now());
    const timer = window.setInterval(() => setCurrentTime(Date.now()), 1000);
    return () => window.clearInterval(timer);
  }, [tab, scanState.isReading]);

  useEffect(() => {
    if (typeof mapApi.listenPlayerMarkChanged !== "function") return undefined;
    return mapApi.listenPlayerMarkChanged(() => setSearchRevision((value) => value + 1));
  }, [mapApi]);

  async function startScan() {
    // Empty manual selection is an intentional user choice, never an implicit
    // request to scan all eight default kinds.
    if (!selectedTypes.length) return;
    setOptions((current) => current ? { ...current, scanProgress: null } : current);
    setScanError("");
    try {
      const next = await mapApi.start(selectedTypes, "fast");
      summaryGeneration.current++;
      previousReading.current = next.isReading;
      commitScanState(next);
      setScanStateAvailable(true);
      if (next.isReading && next.serverId > 0) setBrowseServerId(next.serverId);
    } catch (error) {
      setScanError(translateActionError(t, error));
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
      // Stop may settle while a saved remote dataset is selected. Keep that
      // choice; startScan/active-status already select an actual new target.
      if (!externallyManaged) await refreshSummary();
    } catch (error) {
      setScanError(translateActionError(t, error));
    }
  }

  async function clearData() {
    if (!dataServerId) return;
    setScanError("");
    try {
      // Auto may show a saved remote dataset while the game has already
      // returned home. Backend independently validates its published owner.
      const next = await mapApi.clear(dataServerId, scanTab === "auto" || (currentServerId > 0 && dataServerId !== currentServerId));
      summaryGeneration.current++;
      optionsGeneration.current++;
      searchGeneration.current += 1;
      commitScanState(next);
      setScanStateAvailable(true);
      setCounts({ ...EMPTY_COUNTS });
      // Clearing a saved remote store does not clear the live server's counts.
      if (typeof onCounts === "function") onCounts(dataServerId, { ...EMPTY_COUNTS });
      setSummaryReady(true);
      setOptions(null);
      setAlliance("all");
      setResourceNameKey("");
      setMonsterNameKey("");
      setMinLevel("");
      setItemKeyByKind({});
      setTruckItemKeys([]);
      setGhostRewards([]);
      setResourceMinLevel("");
      setResourceMaxLevel("");
      setResourceAmountState("");
      setResourceTileState("");
      setEscortMaxMillions("");
      setTreasureType("");
      setBrowseServerId(next.serverId);
      setSavedServersRevision((revision) => revision + 1);
      tabViewCache.current.clear();
      setRows([]);
      setTotal(0);
      setPage(1);
      setLoading(false);
      setSearchRevision((value) => value + 1);
      setOptionsRevision((value) => value + 1);
    } catch (error) {
      setScanError(translateActionError(t, error));
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

  function changeTab(nextTab) {
    if (nextTab === tab) return;
    followGeneration.current += 1;
    setFollowing(null);
    setActionBusy((value) => value.startsWith("jump:") ? "" : value);
    retirePreviewOperation();
    searchGeneration.current += 1;
    treasureRefreshGeneration.current += 1;
    setTreasureStatesRefreshing(false);
    const outgoingTab = tab;
    const incomingTab = nextTab;
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
    if (tab === "truck") {
      setTruckItemKeys((current) => !value ? [] : current.includes(value) ? current.filter((key) => key !== value) : [...current, value]);
      if (!value && activeSorts.some((sort) => sort.sortBy === "itemCount")) {
        setSortsByKind((current) => ({ ...current, [tab]: current[tab].filter((sort) => sort.sortBy !== "itemCount") }));
      }
      setPage(1);
      return;
    }
    setItemKeyByKind((current) => ({ ...current, [tab]: value || undefined }));
    if (!value && activeSorts.some((sort) => sort.sortBy === "itemCount")) {
      setSortsByKind((current) => ({ ...current, [tab]: current[tab].filter((sort) => sort.sortBy !== "itemCount") }));
    }
    setPage(1);
  }

  const ghostRewardItems = options?.serverId === dataServerId && Array.isArray(options?.rewardItems?.ghost) ? options.rewardItems.ghost : [];
  const toggleGhostRewardFilter = (key) => { setGhostRewards((current) => toggleGhostReward(current, key)); setPage(1); };
  const clearGhostRewardFilter = () => { setGhostRewards([]); setPage(1); };
  const changeGhostRewardMinimum = (key, raw) => { setGhostRewards((current) => setGhostRewardMinimum(current, key, raw)); setPage(1); };
  const changeGhostRewardMatch = (value) => { setGhostRewardMatch(value); setPage(1); };

  async function coordinateJump(row) {
    const moving = tab === "truck" || tab === "railway" || (tab === "monster" && Boolean(row.marchUuid));
    const marchUuid = moving ? String(row.marchUuid || "").trim() : "";
    const followKey = moving ? `${row.serverId}:${marchUuid}` : "";
    if (moving && following?.key === followKey) {
      followGeneration.current += 1;
      setFollowing(null);
      return;
    }
    // Independently fence a stale click from a still-mounted scan result.
    const nativeArrival = Number(row.arriveTs);
    if (moving && Number.isFinite(nativeArrival) && nativeArrival > 0 && Date.now() >= nativeArrival) return;
    followGeneration.current += 1;
    const generation = followGeneration.current;
    const issuedApi = mapApi;
    const issuedTab = tab;
    const currentNavigation = () => mountedOwnerRef.current && generation === followGeneration.current &&
      previewContextRef.current?.mapApi === issuedApi && previewContextRef.current?.tab === issuedTab;
    setFollowing(null);
    setActionBusy(moving ? `jump:${followKey}` : `jump:${row.serverId}:${row.x}:${row.y}`);
    setQueryError("");
    try {
      if (moving) {
        if (!marchUuid || typeof mapApi?.marchFollow !== "function") throw new Error("MARCH_FOLLOW_UNAVAILABLE");
        await mapApi.marchFollow(row);
        if (!currentNavigation()) return;
        // The native command binds session, server and march UUID. A fresh
        // status read authorizes subsequent passive tracking after Auto return.
        const status = await mapApi.scanStatus();
        if (!currentNavigation()) return;
        if (status.serverId !== row.serverId || status.isReading) throw new Error("MARCH_FOLLOW_SERVER_CHANGED");
        commitScanState(status);
        setFollowing({ key: followKey, kind: tab, serverId: row.serverId, marchUuid, arriveTs: Number(row.arriveTs) || 0, mapApi });
      } else await mapApi.coordinateJump(row);
    }
    catch (error) { if (currentNavigation()) setQueryError(translateActionError(t, error)); }
    finally { if (currentNavigation()) setActionBusy(""); }
  }

  // Passive observation: each poll resolves the same March UUID through the
  // game's *fresh* WorldMarch location. It never uses the stored scan tile or
  // issues a march/attack. Stop on disappearance, server/profile change,
  // arrival, scan start or unmount. Only one poll is in flight at a time.
  useEffect(() => {
    if (!following) return undefined;
    if (following.mapApi !== mapApi || following.kind !== tab || !online || autoRunning ||
        scanState.isReading || scanState.serverId !== following.serverId ||
        (following.arriveTs > 0 && Date.now() >= following.arriveTs)) {
      followGeneration.current += 1;
      setFollowing(null);
      return undefined;
    }
    let retired = false;
    let busy = false;
    const token = followGeneration.current;
    const interval = window.setInterval(async () => {
      if (busy || retired) return;
      if (following.arriveTs > 0 && Date.now() >= following.arriveTs) {
        followGeneration.current += 1;
        setFollowing(null);
        return;
      }
      busy = true;
      try {
        await following.mapApi.marchFollow(following);
      } catch (error) {
        if (!retired && token === followGeneration.current && mountedOwnerRef.current) {
          setQueryError(translateActionError(t, error));
          followGeneration.current += 1;
          setFollowing(null);
        }
      } finally { busy = false; }
    }, 3000);
    return () => { retired = true; window.clearInterval(interval); };
  }, [autoRunning, following, mapApi, online, scanState.isReading, scanState.serverId, tab]);

  async function togglePlayerMark(row) {
    setActionBusy(`mark:${row.serverId}:${row.ownerUid}`);
    setQueryError("");
    try {
      await mapApi.setPlayerMark(row, !row.marked);
      setSearchRevision((value) => value + 1);
    } catch (error) { setQueryError(translateActionError(t, error)); }
    finally { setActionBusy(""); }
  }

  async function exportCities() {
    if (tab !== "city" || !dataAvailable) return;
    setExporting(true);
    setActionMessage("");
    try {
      const allianceName = decodeAllianceFilter(alliance);
      const result = await mapApi.exportCities({
        serverId: dataServerId,
        allServers,
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

  function addAutoServers() {
    if (parseAutoServerIds(autoServerInput).length === 0) return;
    emitAutoConfig(
      { serverIds: appendAutoServerIds(autoConfig.serverIds, autoServerInput) },
      { kind: "appendServers", serverIds: parseAutoServerIds(autoServerInput) },
    );
    setAutoServerInput("");
  }

  function toggleAutoType(kind, checked) {
    emitAutoConfig({
      selectedTypes: checked
        ? [...autoConfig.selectedTypes, kind]
        : autoConfig.selectedTypes.filter((value) => value !== kind),
    }, { kind: "setType", type: kind, checked });
  }

  function retirePreviewOperation(expected = null) {
    const operation = previewOperationRef.current;
    if (!operation || (expected && operation !== expected)) return;
    previewOperationRef.current = null;
    setBusyKey((key) => key === operation.busyKey ? "" : key);
  }

  function currentPreviewOperation(operation) {
    const context = previewContextRef.current;
    return previewOperationRef.current === operation &&
      context?.mapApi === operation.api &&
      context.serverId === operation.serverId && context.tab === operation.tab;
  }

  function beginPreviewOperation(busyKey) {
    if (previewOperationRef.current) return null;
    const operation = {
      api: mapApi, serverId: dataServerId, tab, busyKey,
    };
    previewOperationRef.current = operation;
    setBusyKey(busyKey);
    setActionMessage("");
    return operation;
  }

  useEffect(() => {
    // Cleanup only the retired owner's operation. A successor that began on
    // the replacement view must retain its own busy flag and eventual result.
    return () => {
      const operation = previewOperationRef.current;
      if (operation?.api === mapApi && operation.serverId === dataServerId && operation.tab === tab)
        retirePreviewOperation(operation);
    };
  }, [mapApi, dataServerId, tab]);

  async function previewTreasureClaims(scope, uuid = "", targetServerId = dataServerId) {
    if (tab !== "treasure" || !targetServerId || !online ||
        typeof mapApi?.previewTreasureClaim !== "function") return;
    const operation = beginPreviewOperation("preview-treasure");
    if (!operation) return;
    try {
      const preview = await mapApi.previewTreasureClaim(targetServerId, scope, luckyFirst, uuid);
      if (currentPreviewOperation(operation)) {
        setActionMessage(t("map.treasurePreviewReady", {
          eligible: preview?.eligibleCount ?? 0, count: preview?.candidateCount ?? 0,
        }));
        setSearchRevision((value) => value + 1);
      }
    } catch (error) {
      if (currentPreviewOperation(operation)) setActionMessage(translateActionError(t, error));
    } finally {
      retirePreviewOperation(operation);
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
              <details className="map-category-picker">
                <summary>{t("map.scanTypes")} <strong>{selectedTypes.length}</strong><span aria-hidden="true">⌄</span></summary>
                <div className="map-category-menu">{MAP_SCAN_TYPES.map(({ key }) => <label key={key}><input type="checkbox" checked={selectedTypes.includes(key)} disabled={scanState.isReading || autoRunning} onChange={event => toggleType(key, event.target.checked)} /><span>{t(SCAN_TYPE_LABEL_KEYS[key])}</span></label>)}</div>
              </details>
              <button type="button" className={!scanState.isReading ? "primary" : ""} disabled={!online || scanState.isReading || autoRunning || selectedTypes.length === 0} onClick={startScan}>{t("map.startReading")}</button>
              {selectedTypes.length === 0 ? <small data-map-empty-selection="manual" role="status">{t("map.scanCategoryRequired")}</small> : null}
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
                    <button type="button" aria-label={`${t("common.remove")} ${id}`} onClick={() => emitAutoConfig({ serverIds: removeAutoServerId(autoConfig.serverIds, id) }, { kind: "removeServer", id })}>
                      <svg className="ui-icon" viewBox="0 0 16 16" aria-hidden="true" focusable="false">
                        <path d="m4 4 8 8M12 4l-8 8" />
                      </svg>
                    </button>
                  </span>
                ))}
              </span>
            </div>
            <label><span>{t("map.scanIntervalMinutes")}</span><input type="number" min={20} max="1440" value={autoConfig.intervalMinutes} onChange={(event) => emitAutoConfig({ intervalMinutes: Number(event.target.value) })} /></label>
          </div>
          <div className="map-auto-scan-options">
            <details className="map-category-picker">
              <summary>{t("map.scanTypes")} <strong>{autoConfig.selectedTypes.length}</strong><span aria-hidden="true">⌄</span></summary>
              <div className="map-category-menu">{MAP_SCAN_TYPES.map(({ key }) => <label key={key}><input type="checkbox" checked={autoConfig.selectedTypes.includes(key)} disabled={autoRunning} onChange={event => toggleAutoType(key, event.target.checked)} /><span>{t(SCAN_TYPE_LABEL_KEYS[key])}</span></label>)}</div>
            </details>
            <button type="button" className="primary" disabled={!online || autoConfig.selectedTypes.length === 0 || autoRunning || scanState.isReading} onClick={onAutoScanRunNow}>{t("map.runAutoScanNow")}</button>
            {autoConfig.selectedTypes.length === 0 ? <small data-map-empty-selection="auto" role="status">{t("map.scanCategoryRequired")}</small> : null}
            <button type="button" disabled={!autoRunning} onClick={onAutoScanCancel}>{t("common.stop")}</button>
            <button type="button" disabled={!backendAvailable || !dataServerId || autoRunning || scanState.isReading} onClick={clearData}>{t("map.clearServer")}</button>
          </div>
          <label className="map-auto-return"><input type="checkbox" checked={autoConfig.returnToOriginalServer} onChange={(event) => emitAutoConfig({ returnToOriginalServer: event.target.checked })} />{t("map.returnAfterAutoScan")}</label>
          <small>{t("map.autoScanNavigationNotice")}</small>
          <small>{t("map.nextAutoScan")}: {autoConfig.enabled && autoConfig.nextRunAt > 0 ? formatAutoScanDate(autoConfig.nextRunAt, language) : "-"}</small>
        </div>
      ) : null}

      <div className="map-scan-summary">
        <span className={`map-status-pill${scanState.isReading ? " active" : ""}`}>{t(scanView.statusKey)}</span>
        <span>{t("map.server")} <strong>{scanView.serverId || "-"}</strong></span>
        {scanState.isReading ? <span className="map-active-categories">{t(autoRunning ? "map.autoScan" : "map.manualScan")} · {scanState.selectedTypes.map(key => t(SCAN_TYPE_LABEL_KEYS[key])).join(", ")}</span> : null}
        <div className={`map-progress${progress < 50 ? " low" : ""}${scanState.isReading ? " active" : ""}`}>
          <progress className="map-progress-bar" max={100} value={scanStateAvailable ? progress : undefined} aria-label={t("map.scanProgress")} />
          <span>{scanStateAvailable ? `${progress}%` : "—"}</span>
        </div>
      </div>

      {(scanTab === "auto" ? autoScanError : scanError) ? <div className="map-scan-error" role="alert">{translateActionError(t, scanTab === "auto" ? autoScanError : scanError)}</div> : null}
      {!(scanTab === "auto" ? autoScanError : scanError) && scanView.error ? <div className="map-scan-error" role="status">{translateActionError(t, scanView.error)}</div> : null}

      <div className="map-search">
        <div className="map-tabs" role="tablist" aria-label={t("map.title")}>
          {MAP_TABS.map(({ key }) => (
            <button key={key} type="button" role="tab" className={tab === key ? "active" : ""} aria-selected={tab === key} onClick={() => changeTab(key)}>
              <span className="map-tab-label">{t(TAB_LABEL_KEYS[key])}</span>
              <span className="map-tab-count">{summaryReady ? counts[key] || 0 : "—"}</span>
            </button>
          ))}
        </div>

        <div className="map-searchbar">
            {!scanState.isReading && savedServers.length > 0 ? (
              <label className="map-filter-field"><span>{t("map.savedServer")}</span>
                <select data-map-saved-server aria-label={t("map.savedServer")} value={allServers ? "all" : dataServerId}
                  onChange={(event) => { setBrowseServerId(event.target.value === "all" ? "all" : Number(event.target.value)); setPage(1); }}>
                  <option value="all">{t("map.allServers")}</option>
                  {savedServers.map((server) => <option key={server} value={server}>{server}</option>)}
                </select>
              </label>
            ) : null}
            <input value={keyword} onChange={(event) => changeKeyword(event.target.value)} aria-label={t("map.searchLabel")} placeholder={t("map.searchLabel")} />
            {isNameFilterKind(tab) ? (
              <select aria-label={t("common.name")} value={tab === "resource" ? resourceNameKey : monsterNameKey} onChange={(event) => selectName(tab, event.target.value)}>
                <option value="">{t("map.allNames")}</option>
                {(options?.names?.[tab] || []).map((item) => <option key={item.key} value={item.key}>{lookupMapText(resolvedGameTexts, item.key, item.key)} ({item.count})</option>)}
              </select>
            ) : null}
            {tab === "resource" ? <>
              <label className="map-filter-field"><span>{mapOwnerFilterLabel(language, "minimum")}</span><input aria-label={mapOwnerFilterLabel(language, "minimum")} type="number" min="1" step="1" value={resourceMinLevel} onChange={(event) => { setResourceMinLevel(event.target.value); setPage(1); }} /></label>
              <label className="map-filter-field"><span>{mapOwnerFilterLabel(language, "maximum")}</span><input aria-label={mapOwnerFilterLabel(language, "maximum")} type="number" min="1" step="1" value={resourceMaxLevel} onChange={(event) => { setResourceMaxLevel(event.target.value); setPage(1); }} /></label>
              <select aria-label={mapOwnerFilterLabel(language, "amount")} value={resourceAmountState} onChange={(event) => { setResourceAmountState(event.target.value); setPage(1); }}>
                <option value="">{mapOwnerFilterLabel(language, "all")}</option><option value="full">{mapOwnerFilterLabel(language, "full")}</option><option value="partial">{mapOwnerFilterLabel(language, "partial")}</option><option value="unknown">{mapOwnerFilterLabel(language, "unknown")}</option>
              </select>
              <select aria-label={mapOwnerFilterLabel(language, "terrain")} value={resourceTileState} onChange={(event) => { setResourceTileState(event.target.value); setPage(1); }}>
                <option value="">{mapOwnerFilterLabel(language, "all")}</option><option value="black">{mapOwnerFilterLabel(language, "black")}</option><option value="normal">{mapOwnerFilterLabel(language, "normal")}</option><option value="unknown">{mapOwnerFilterLabel(language, "unknown")}</option>
              </select>
            </> : null}
            {tab === "zombie_boss" ? <>
              <label className="map-filter-field"><span>{mapOwnerFilterLabel(language, "minimum")}</span><select aria-label={t("map.zombieBossMinLevel")} value={zombieMinLevel} onChange={(event) => { setZombieMinLevel(event.target.value); setPage(1); }}><option value="">{t("map.zombieBossAnyLevel")}</option>{ZOMBIE_BOSS_LEVELS.map((level) => <option key={level} value={String(level)}>{level}</option>)}</select></label>
              <label className="map-filter-field"><span>{mapOwnerFilterLabel(language, "maximum")}</span><select aria-label={t("map.zombieBossMaxLevel")} value={zombieMaxLevel} onChange={(event) => { setZombieMaxLevel(event.target.value); setPage(1); }}><option value="">{t("map.zombieBossAnyLevel")}</option>{ZOMBIE_BOSS_LEVELS.map((level) => <option key={level} value={String(level)}>{level}</option>)}</select></label>
              <select aria-label={t("map.zombieBossProtectionFilter")} value={zombieProtection} onChange={(event) => { setZombieProtection(event.target.value); setPage(1); }}>
                <option value="">{t("map.zombieBossProtectionAll")}</option><option value="active">{t("map.zombieBossProtectionActive")}</option><option value="inactive">{t("map.zombieBossNotProtected")}</option><option value="unknown">{t("map.zombieBossProtectionUnknown")}</option>
              </select>
            </> : null}
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
                <MapTreasureTypeFilter items={options?.treasureTypes || []} value={treasureType} gameTexts={resolvedGameTexts} onChange={(value) => { setTreasureType(value); setPage(1); }} />
                <label className="map-filter-field"><input type="checkbox" checked={includeForeignRadarTreasures} onChange={(event) => { setIncludeForeignRadarTreasures(event.target.checked); setPage(1); }} />{t("map.showForeignRadarTreasures")}</label>
                <label className="map-filter-field"><input type="checkbox" checked={luckyFirst} onChange={(event) => { setLuckyFirst(event.target.checked); setPage(1); }} />{t("map.prioritizeLuckyTreasures")}</label>
              </>
            ) : null}
            {tab === "other" ? (
              <select aria-label={t("map.otherEventType")} value={otherSubtype} onChange={(event) => { setOtherSubtype(event.target.value); setPage(1); }}>
                <option value="">{t("map.allOtherTypes")}</option>
                {(options?.otherTypes || []).map((item) => (
                  <option key={item.key} value={item.key}>{otherTypeName(t, item.key)} ({item.count})</option>
                ))}
              </select>
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
                {tab === "ghost" ? (
                  <>
                    <MapGhostRewardFilter items={ghostRewardItems} filters={ghostRewards} match={ghostRewardMatch} gameTexts={resolvedGameTexts} onToggle={toggleGhostRewardFilter} onClear={clearGhostRewardFilter} onMinimum={changeGhostRewardMinimum} onMatch={changeGhostRewardMatch} />
                    {options?.serverId === dataServerId && ghostRewardItems.length === 0 && (counts.ghost || 0) > 0 ? <span className="map-ghost-reward-note" role="status">{t("map.ghostRewardNoData")}</span> : null}
                  </>
                ) : null}
                {(tab === "truck" || tab === "railway") ? (
                  <MapRetainedGoodsFilter label={t("map.itemFilter")} allLabel={t("map.allRetainedGoods")} items={options?.rewardItems?.[tab] || []} value={itemKey} values={truckItemKeys} multiple={tab === "truck"} selectedLabel={mapOwnerFilterLabel(language, "itemsAll")} clearLabel={mapOwnerFilterLabel(language, "clearItems")} onChange={changeItemFilter} />
                ) : null}
                {tab === "truck" ? <label className="map-filter-field"><span>{mapOwnerFilterLabel(language, "escort")}</span><input type="text" inputMode="decimal" aria-label={mapOwnerFilterLabel(language, "escort")} value={escortMaxMillions} onChange={(event) => { setEscortMaxMillions(event.target.value.trim()); setPage(1); }} /></label> : null}
                {(tab === "truck" || tab === "railway" || tab === "dispatch") ? <label className="map-filter-field map-plunderable-filter"><input type="checkbox" checked={plunderableOnly} onChange={(event) => { setPlunderableOnlyByKind((current) => ({ ...current, [tab]: event.target.checked || undefined })); setPage(1); }} /><span>{t("map.plunderableOnly")}</span></label> : null}
              </>
            ) : null}
            <button type="button" disabled={!backendAvailable || !dataAvailable} onClick={submitSearch}>{t("common.search")}</button>
            {tab === "city" ? <button type="button" disabled={!backendAvailable || !dataAvailable || scanState.isReading || exporting} onClick={exportCities}>{t(exporting ? "map.exportingExcel" : "map.exportExcel")}</button> : null}
            {tab === "treasure" ? <><button className="map-schedule-button" type="button" disabled={!online || !dataServerId || Boolean(busyKey) || typeof mapApi?.previewTreasureClaim !== "function"} onClick={() => previewTreasureClaims("boxes")}>{t("map.previewTreasureBoxes")}</button><button className="map-schedule-button" type="button" disabled={!online || !dataServerId || Boolean(busyKey) || typeof mapApi?.previewTreasureClaim !== "function"} onClick={() => previewTreasureClaims("season")}>{t("map.previewSeasonTreasures")}</button></> : null}
            {actionMessage ? <span className="map-claim-result" role="status">{actionMessage}</span> : null}
            <span className="map-result-count">{t("common.itemCount", { count: total })}</span>
          </div>
        {queryError ? <div className="map-scan-error" role="alert">{queryError}</div> : null}
        {!previewFixture && backendAvailable && gameTextLoader && !gameTextRoot ?
          <div className="map-scan-error" role="alert">{t("map.namesMissingRoot")}</div>
          : null}
        {!previewFixture && localizedNames.context === localeContext && localizedNames.error ?
          <div className="map-scan-error" role="alert">{t("map.namesUnavailable", { detail: localizedNames.error })} <button type="button" onClick={() => setLocaleRetry((value) => value + 1)}>{t("map.namesRetry")}</button></div>
          : null}
        {!previewFixture && localizedNames.context === localeContext && localizedNames.loading ?
          <span className="muted" role="status">{t("map.namesLoading")}</span> : null}
        <>
            <MapTable kind={tab} gameTexts={resolvedGameTexts} itemKey={itemKey} rows={rows} loading={loading} treasureStatesRefreshing={previewFixture ? previewState === "map-treasure-checking" && tab === "treasure" && previewTreasureStatesRefreshing === true : treasureStatesRefreshing} sorts={activeSorts} onSort={changeSort} onCoordinateJump={coordinateJump} onPlayerMark={togglePlayerMark} onTreasurePreview={previewTreasureClaims} treasurePreviewAvailable={typeof mapApi?.previewTreasureClaim === "function"} actionBusy={Boolean(actionBusy || busyKey)} actionDisabled={!online || scanState.isReading} markDisabled={!backendAvailable || !dataAvailable || scanState.isReading || typeof mapApi?.setPlayerMark !== "function"} liveTargetDisabled={typeof mapApi?.marchFollow !== "function"} followingKey={following?.key || ""} jumpingKey={previewFixture && previewState === "map-row-actions" ? previewJumpingKeys?.[tab] || "" : actionBusy.startsWith("jump:") ? actionBusy.slice(5) : ""} allServers={allServers} />
            <Pagination page={page} total={total} onPage={setPage} />
          </>
      </div>
    </section>
  );
}
