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

const DEFAULT_SORTS = Object.freeze(
  Object.fromEntries(MAP_KIND_KEYS.map((kind) => [kind, [{ sortBy: "updatedAt", sortOrder: "desc" }]])),
);

function errorText(error) {
  if (!error) return "";
  const code = typeof error.code === "string" ? error.code : "";
  const message = typeof error.message === "string" ? error.message : String(error);
  return [code, message].filter(Boolean).join(": ");
}

function uiLocale() {
  return typeof document !== "undefined" && document.documentElement.lang
    ? document.documentElement.lang
    : "en";
}

function numberText(value) {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed.toLocaleString(uiLocale()) : "-";
}

function dateText(value) {
  const parsed = Number(value);
  if (!Number.isFinite(parsed) || parsed <= 0) return "-";
  return new Date(parsed < 1_000_000_000_000 ? parsed * 1000 : parsed).toLocaleString(uiLocale());
}

function coordinateText(row) {
  const x = Number(row?.x);
  const y = Number(row?.y);
  return Number.isFinite(x) && Number.isFinite(y) ? `${x},${y}` : "-";
}

function qualityText(value) {
  const quality = Number(value);
  return ({ 1: "N", 2: "R", 3: "SR", 4: "SSR", 5: "UR" })[quality] || (quality > 0 ? String(quality) : "-");
}

function resourceStatus(row) {
  if (row?.rebuildGatherOccupancyKnown === false) return "—";
  const occupied = row?.rebuildGatherOccupancyKnown === true
    ? row?.rebuildGatherOccupied === true
    : [row?.gatherMarchUuid, row?.gatherUid].some((value) => value != null && String(value) !== "" && String(value) !== "0");
  return occupied ? "Gathering" : "Idle";
}

function amountText(row) {
  const remaining = Number(row?.resourceRemainingAmount);
  const full = Number(row?.resourceFullAmount);
  return Number.isFinite(remaining) && Number.isFinite(full)
    ? `${remaining.toLocaleString(uiLocale())} / ${full.toLocaleString(uiLocale())}`
    : "—";
}

function rowColumns(kind) {
  const coordinates = { label: "Coordinates", coordinate: true };
  const updated = { label: "Updated At", sortBy: "updatedAt", value: (row) => dateText(row.updatedAt) };
  switch (kind) {
    case "city":
      return [
        coordinates,
        { label: "Player", value: (row) => row.ownerName || row.ownerUid || "-" },
        { label: "Alliance", value: (row) => row.allianceName || row.allianceAbbr || "-" },
        { label: "Level", sortBy: "level", value: (row) => numberText(row.level) },
        { label: "HP", sortBy: "health", value: (row) => numberText(row.health) },
        updated,
      ];
    case "resource":
      return [
        coordinates,
        { label: "Resource", value: (row) => row.resourceNameKey || row.name || "Unknown resource" },
        { label: "Level", sortBy: "level", value: (row) => numberText(row.level) },
        { label: "Amount", value: amountText },
        { label: "Status", value: resourceStatus },
        updated,
      ];
    case "monster":
      return [
        coordinates,
        { label: "Name", value: (row) => row.monsterNameKey || row.name || "Unknown monster" },
        { label: "Level", sortBy: "level", value: (row) => numberText(row.level) },
        { label: "Distance", value: (row) => numberText(row.distanceFromHome) },
        updated,
      ];
    case "truck":
      return [
        coordinates,
        { label: "Player / Alliance", value: (row) => row.ownerName || row.allianceName || "-" },
        { label: "Quality", sortBy: "quality", value: (row) => qualityText(row.quality) },
        { label: "Escort Power", sortBy: "power", value: (row) => numberText(row.power) },
        updated,
      ];
    case "railway":
      return [
        coordinates,
        { label: "Alliance", value: (row) => row.allianceName || row.allianceAbbr || "-" },
        { label: "Quality", value: (row) => qualityText(row.quality) },
        { label: "Power", sortBy: "power", value: (row) => numberText(row.power) },
        updated,
      ];
    case "treasure":
      return [
        coordinates,
        { label: "Treasure", value: (row) => row.treasureNameKey || row.name || row.treasureType || "-" },
        { label: "Remaining Boxes", value: (row) => numberText(row.remainingBoxes) },
        updated,
      ];
    case "dispatch":
    case "ghost":
      return [
        coordinates,
        { label: "Owner", value: (row) => row.ownerName || row.ownerUid || "-" },
        { label: "Level", sortBy: "level", value: (row) => numberText(row.level) },
        { label: "Quality", sortBy: "quality", value: (row) => qualityText(row.quality) },
        { label: "Status", value: (row) => row.completionStatus || row.status || "-" },
        updated,
      ];
    default:
      return [coordinates, updated];
  }
}

function MapTable({ kind, rows, loading, sorts, onSort }) {
  const columns = useMemo(() => rowColumns(kind), [kind]);
  return (
    <div className="map-table-scroll">
      <table className={`map-table map-table--${kind}`} aria-label={MAP_TABS.find((item) => item.key === kind)?.label} aria-busy={loading}>
        <thead>
          <tr className="map-row map-head">
            {columns.map((column) => {
              const sortIndex = column.sortBy ? sorts.findIndex((sort) => sort.sortBy === column.sortBy) : -1;
              const sort = sortIndex >= 0 ? sorts[sortIndex] : null;
              return (
                <th key={column.label} scope="col" aria-sort={sortIndex === 0 ? (sort.sortOrder === "asc" ? "ascending" : "descending") : undefined}>
                  {column.sortBy ? (
                    <button
                      type="button"
                      className={`map-sort-button${sort ? " active" : ""}`}
                      onClick={() => onSort(column.sortBy)}
                    >
                      {column.label}
                      {sort ? <span className="map-sort-priority" aria-hidden="true">{sortIndex + 1}</span> : null}
                      <span className="map-sort-arrows" aria-hidden="true">
                        <span className={`map-sort-arrow${sort?.sortOrder === "asc" ? " active" : ""}`}>↑</span>
                        <span className={`map-sort-arrow${sort?.sortOrder === "desc" ? " active" : ""}`}>↓</span>
                      </span>
                    </button>
                  ) : column.label}
                </th>
              );
            })}
          </tr>
        </thead>
        <tbody>
          {rows.map((row, index) => (
            <tr className="map-row" key={`${kind}:${row.serverId ?? 0}:${row.recordKey || row.uuid || row.pointIndex || index}`}>
              {columns.map((column) => (
                <td key={column.label}>
                  {column.coordinate ? (
                    <button className="map-coordinate-button" type="button" disabled title="Map navigation is not enabled in this integration task.">
                      <span className="map-coordinate-icon" aria-hidden="true" />
                      <span>{coordinateText(row)}</span>
                    </button>
                  ) : column.value?.(row)}
                </td>
              ))}
            </tr>
          ))}
          {rows.length === 0 ? (
            <tr><td className="map-empty" colSpan={columns.length}>{loading ? "Processing…" : "No saved data for this type."}</td></tr>
          ) : null}
        </tbody>
      </table>
    </div>
  );
}

function Pagination({ page, total, onPage }) {
  const totalPages = mapPageCount(total);
  if (totalPages <= 1) return null;
  return (
    <div className="map-pagination">
      <button type="button" disabled={page <= 1} onClick={() => onPage(page - 1)}>Previous</button>
      <span>Page {page} of {totalPages}</span>
      <button type="button" disabled={page >= totalPages} onClick={() => onPage(page + 1)}>Next</button>
    </div>
  );
}

export function MapDataPage({ mapApi, bridgeMode, backendAvailable, online }) {
  const [scanTab, setScanTab] = useState("manual");
  const [speed, setSpeed] = useState(() => {
    const saved = window.localStorage.getItem("lwbridge.mapScanMode");
    return saved === "normal" || saved === "fast" ? saved : "normal";
  });
  const [selectedTypes, setSelectedTypes] = useState(() => [...MAP_KIND_KEYS]);
  const [scanState, setScanState] = useState(() => ({ ...DEFAULT_SCAN_STATE }));
  const [counts, setCounts] = useState(() => ({ ...EMPTY_COUNTS }));
  const [summaryReady, setSummaryReady] = useState(false);
  const [options, setOptions] = useState(null);
  const [browseServerId, setBrowseServerId] = useState(0);
  const [tab, setTab] = useState("city");
  const [keyword, setKeyword] = useState("");
  const [submittedKeyword, setSubmittedKeyword] = useState("");
  const [resourceNameKey, setResourceNameKey] = useState("");
  const [monsterNameKey, setMonsterNameKey] = useState("");
  const [alliance, setAlliance] = useState("all");
  const [markedOnly, setMarkedOnly] = useState(false);
  const [page, setPage] = useState(1);
  const [sortsByKind, setSortsByKind] = useState(() => Object.fromEntries(
    Object.entries(DEFAULT_SORTS).map(([kind, sorts]) => [kind, sorts.map((sort) => ({ ...sort }))]),
  ));
  const [rows, setRows] = useState([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [scanError, setScanError] = useState("");
  const [queryError, setQueryError] = useState("");
  const [searchRevision, setSearchRevision] = useState(0);
  const selectionTouched = useRef(false);
  const previousReading = useRef(false);
  const summaryGeneration = useRef(0);

  const dataServerId = browseServerId || scanState.serverId;
  const activeSorts = sortsByKind[tab] || [{ sortBy: "updatedAt", sortOrder: "desc" }];

  useEffect(() => {
    window.localStorage.setItem("lwbridge.mapScanMode", speed);
  }, [speed]);

  const loadOptions = useCallback(async (serverId) => {
    if (!backendAvailable || !serverId) return;
    try {
      setOptions(await mapApi.dataOptions(serverId));
    } catch (error) {
      setQueryError(errorText(error));
    }
  }, [backendAvailable, mapApi]);

  const refreshSummary = useCallback(async () => {
    if (!backendAvailable) return;
    const generation = ++summaryGeneration.current;
    try {
      const summary = await mapApi.summary();
      if (generation !== summaryGeneration.current) return;
      setScanState(summary.scanState);
      setCounts(summary.counts);
      setSummaryReady(true);
      if (!selectionTouched.current) {
        setSelectedTypes(summary.scanState.selectedTypes);
        setSpeed(summary.scanState.scanMode);
      }
      if (summary.serverId > 0) {
        setBrowseServerId(summary.serverId);
        await loadOptions(summary.serverId);
      }
      if (generation !== summaryGeneration.current) return;
      setScanError(summary.scanState.lastError || "");
    } catch (error) {
      if (generation !== summaryGeneration.current) return;
      setScanError(errorText(error));
    }
  }, [backendAvailable, loadOptions, mapApi]);

  useEffect(() => {
    if (!backendAvailable) return undefined;
    let closed = false;
    previousReading.current = scanState.isReading;
    const unsubscribe = mapApi.listenScanStatus((next) => {
      if (closed) return;
      summaryGeneration.current++;
      const wasReading = previousReading.current;
      previousReading.current = next.isReading;
      setScanState(next);
      setScanError(next.lastError || "");
      if (next.serverId > 0) setBrowseServerId(next.serverId);
      if (wasReading && !next.isReading) refreshSummary();
    });
    refreshSummary();
    const timer = window.setInterval(refreshSummary, 5000);
    return () => {
      closed = true;
      window.clearInterval(timer);
      unsubscribe();
    };
  // scanState is deliberately event-owned after the first subscription.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [backendAvailable, mapApi, refreshSummary]);

  useEffect(() => {
    setPage(1);
    setRows([]);
    setTotal(0);
    setQueryError("");
  }, [tab, dataServerId]);

  useEffect(() => {
    if (!backendAvailable || !dataServerId || tab === "scheduledPlunder") return undefined;
    let cancelled = false;
    setLoading(true);
    setQueryError("");
    const query = {
      serverId: dataServerId,
      keyword: submittedKeyword,
      page,
      sorts: activeSorts,
    };
    if (tab === "resource" && resourceNameKey) query.resourceNameKey = resourceNameKey;
    if (tab === "monster" && monsterNameKey) query.monsterNameKey = monsterNameKey;
    if (tab === "city") {
      if (alliance === "none") query.withoutAlliance = true;
      else if (alliance !== "all") query.alliance = alliance;
      if (markedOnly) query.markedOnly = true;
    }
    mapApi.search(tab, query).then((result) => {
      if (cancelled) return;
      const totalPages = mapPageCount(result.total);
      if (page > totalPages) {
        setPage(totalPages);
        return;
      }
      setRows(result.rows);
      setTotal(result.total);
    }).catch((error) => {
      if (!cancelled) {
        setRows([]);
        setTotal(0);
        setQueryError(errorText(error));
      }
    }).finally(() => {
      if (!cancelled) setLoading(false);
    });
    return () => { cancelled = true; };
  }, [
    activeSorts, alliance, backendAvailable, dataServerId, mapApi, markedOnly, monsterNameKey,
    page, resourceNameKey, searchRevision, submittedKeyword, tab,
  ]);

  async function startScan() {
    setScanError("");
    try {
      const next = await mapApi.start(selectedTypes, speed);
      summaryGeneration.current++;
      previousReading.current = next.isReading;
      setScanState(next);
      if (next.serverId > 0) setBrowseServerId(next.serverId);
    } catch (error) {
      setScanError(errorText(error));
    }
  }

  async function stopScan() {
    setScanError("");
    try {
      const next = await mapApi.stop();
      summaryGeneration.current++;
      previousReading.current = next.isReading;
      setScanState(next);
      if (next.serverId > 0) setBrowseServerId(next.serverId);
      await refreshSummary();
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
      setScanState(next);
      setCounts({ ...EMPTY_COUNTS });
      setOptions(null);
      setRows([]);
      setTotal(0);
      setPage(1);
      await refreshSummary();
      setSearchRevision((value) => value + 1);
    } catch (error) {
      setScanError(errorText(error));
    }
  }

  function toggleType(kind, checked) {
    selectionTouched.current = true;
    setSelectedTypes((current) => updateSelectedTypes(current, kind, checked));
  }

  function submitSearch() {
    setSubmittedKeyword(keyword);
    setPage(1);
    setSearchRevision((value) => value + 1);
  }

  function changeSort(sortBy) {
    setSortsByKind((current) => ({ ...current, [tab]: cycleSort(current[tab], sortBy) }));
    setPage(1);
  }

  const progress = Math.max(0, Math.min(100, Math.round(Number(scanState.progressPercent) || 0)));
  const statusLabel = scanState.isReading
    ? scanState.phase === "publishing" ? "Processing" : "Reading"
    : scanState.phase === "completed" ? "Completed" : "Stopped";
  const phaseLabel = scanState.phase || "idle";

  return (
    <section className="panel map-panel" data-bridge-mode={bridgeMode}>
      <div className="map-scan-tabs" role="tablist" aria-label="Map scan mode">
        <button type="button" role="tab" className={scanTab === "manual" ? "active" : ""} aria-selected={scanTab === "manual"} onClick={() => setScanTab("manual")}>Manual Scan</button>
        <button type="button" role="tab" className={scanTab === "auto" ? "active" : ""} aria-selected={scanTab === "auto"} onClick={() => setScanTab("auto")}>Auto Scan</button>
      </div>

      <div className="map-header">
        <h2>World Map Data</h2>
        <div className="map-actions">
          {scanTab === "manual" ? (
            <>
              <fieldset className={`map-speed-toggle${speed === "fast" ? " fast" : ""}`} disabled={scanState.isReading}>
                <span className="map-speed-slider" aria-hidden="true" />
                <label><input type="radio" name="map-scan-speed" checked={speed === "normal"} onChange={() => { selectionTouched.current = true; setSpeed("normal"); }} /><span>Normal</span></label>
                <label><input type="radio" name="map-scan-speed" checked={speed === "fast"} onChange={() => { selectionTouched.current = true; setSpeed("fast"); }} /><span>Fast</span></label>
              </fieldset>
              <button type="button" className={!scanState.isReading ? "primary" : ""} disabled={!online || scanState.isReading} onClick={startScan}>Start Scan</button>
              <button type="button" className={scanState.isReading ? "danger" : ""} disabled={!backendAvailable || !scanState.isReading} onClick={stopScan}>Stop</button>
              <button type="button" disabled={!backendAvailable || !dataServerId || scanState.isReading} onClick={clearData}>Clear Map Data</button>
            </>
          ) : null}
        </div>
      </div>

      {scanTab === "auto" ? (
        <div className="map-auto-scan-card" data-runtime-state="server-jump-unvalidated">
          <label className="map-auto-scan-master">
            <input type="checkbox" disabled />
            <strong>Enable automatic scanning</strong>
            <span>Disabled</span>
          </label>
          <div className="map-auto-scan-grid">
            <div className="map-auto-scan-server-field">
              <span>Target servers</span>
              <div className="map-auto-scan-server-input"><input disabled /><button type="button" disabled>Add</button></div>
              <small>Enter server IDs and click Add. Commas add several at once; × removes one. No entries scans the current server.</small>
            </div>
            <label><span>Interval (minutes)</span><input value="60" readOnly disabled /></label>
            <label><span>Speed</span><select value="fast" disabled readOnly><option value="normal">Normal</option><option value="fast">Fast</option></select></label>
          </div>
          <div className="map-controls">
            <span className="map-controls-label">Scan contents</span>
            <div className="map-types map-types--compact">
              {MAP_SCAN_TYPES.map(({ key, label }) => (
                <label key={key} className="disabled"><input type="checkbox" checked={["truck", "railway", "dispatch", "ghost", "treasure"].includes(key)} readOnly disabled /><span>{label}</span></label>
              ))}
            </div>
          </div>
          <div className="map-auto-scan-options">
            <label><input type="checkbox" checked readOnly disabled />Return to the original server after scanning</label>
            <button type="button" disabled>Run now</button>
          </div>
          <small>Each target server is entered before scanning; a server ID alone cannot scan another server.</small>
          <small>Next scan: -</small>
        </div>
      ) : null}

      <div className="map-scan-summary">
        <span className={`map-status-pill${scanState.isReading ? " active" : ""}`}>{statusLabel}</span>
        <span>Server <strong>{scanState.serverId || "-"}</strong></span>
        <div className={`map-progress${progress < 50 ? " low" : ""}${scanState.isReading ? " active" : ""}`}>
          <progress className="map-progress-bar" max="100" value={scanState.totalBlocks > 0 || scanState.isReading ? progress : undefined} aria-label="Scan progress" />
          <span>{scanState.totalBlocks > 0 || scanState.isReading ? `${progress}%` : "—"}</span>
        </div>
      </div>

      <div className="map-counters" aria-label="Scan details">
        <span><small>Phase</small><strong>{phaseLabel}</strong></span>
        <span><small>Total blocks</small><strong>{numberText(scanState.totalBlocks)}</strong></span>
        <span><small>Read</small><strong>{numberText(scanState.readBlocks)}</strong></span>
        <span><small>Failed</small><strong>{numberText(scanState.failedBlocks)}</strong></span>
        <span><small>Unread</small><strong>{numberText(scanState.unreadBlocks)}</strong></span>
        <span><small>Mode</small><strong>{scanState.scanMode}</strong></span>
      </div>

      {scanError ? <div className="map-scan-error" role="alert">{scanError}</div> : null}
      {!scanError && !backendAvailable ? (
        <div className="map-scan-error" role="status">
          {bridgeMode === "preview" ? "Browser preview mode. Native Map actions are unavailable." : "Native backend unavailable."}
        </div>
      ) : null}
      {!scanError && backendAvailable && !online ? <div className="map-scan-error" role="status">Game disconnected. Start Scan is disabled; saved Map data remains available.</div> : null}

      {scanTab === "manual" ? (
        <div className="map-controls">
          <span className="map-controls-label">Scan contents</span>
          <div className="map-types map-types--compact">
            {MAP_SCAN_TYPES.map(({ key, label }) => (
              <label key={key}>
                <input type="checkbox" checked={selectedTypes.includes(key)} disabled={selectedTypes.length === 1 && selectedTypes[0] === key} onChange={(event) => toggleType(key, event.target.checked)} />
                <span>{label}</span>
              </label>
            ))}
          </div>
        </div>
      ) : null}

      <div className="map-search">
        <div className="map-tabs" role="tablist" aria-label="Map data types">
          {MAP_TABS.map(({ key, label }) => (
            <button key={key} type="button" role="tab" className={tab === key ? "active" : ""} aria-selected={tab === key} onClick={() => setTab(key)}>
              <span className="map-tab-label">{label}</span>
              <span className="map-tab-count">{key === "scheduledPlunder" ? "0" : summaryReady ? counts[key] || 0 : "—"}</span>
            </button>
          ))}
        </div>

        {tab !== "scheduledPlunder" ? (
          <div className="map-searchbar">
            <input value={keyword} onChange={(event) => setKeyword(event.target.value)} aria-label="Search map data" placeholder="Search name, Alliance, or UUID" />
            {tab === "resource" ? (
              <select aria-label="Resource name" value={resourceNameKey} onChange={(event) => { setResourceNameKey(event.target.value); setPage(1); }}>
                <option value="">All Names</option>
                {(options?.names?.resource || []).map((item) => <option key={item.key} value={item.key}>{item.key} ({item.count})</option>)}
              </select>
            ) : null}
            {tab === "monster" ? (
              <select aria-label="Monster name" value={monsterNameKey} onChange={(event) => { setMonsterNameKey(event.target.value); setPage(1); }}>
                <option value="">All Names</option>
                {(options?.names?.monster || []).map((item) => <option key={item.key} value={item.key}>{item.key} ({item.count})</option>)}
              </select>
            ) : null}
            {tab === "city" ? (
              <>
                <select aria-label="Filter by Alliance" value={alliance} onChange={(event) => { setAlliance(event.target.value); setPage(1); }}>
                  <option value="all">All Alliances</option>
                  {(options?.noAllianceCount || 0) > 0 ? <option value="none">No Alliance ({options.noAllianceCount})</option> : null}
                  {(options?.alliances || []).map((item) => <option key={item.name} value={item.name}>{item.name} ({item.count})</option>)}
                </select>
                <label className="map-filter-field"><input type="checkbox" checked={markedOnly} onChange={(event) => { setMarkedOnly(event.target.checked); setPage(1); }} /> <span>Marked only</span></label>
              </>
            ) : null}
            <button type="button" disabled={!backendAvailable || !dataServerId || loading} onClick={submitSearch}>Search</button>
            {tab === "city" ? <button type="button" disabled>Export Excel</button> : null}
            <span className="map-result-count">{total.toLocaleString()} items</span>
          </div>
        ) : (
          <div className="map-searchbar"><span className="map-result-count">0 items</span></div>
        )}
      </div>

      {queryError ? <div className="map-scan-error" role="alert">{queryError}</div> : null}
      {tab !== "scheduledPlunder" ? (
        <>
          <MapTable kind={tab} rows={rows} loading={loading} sorts={activeSorts} onSort={changeSort} />
          <Pagination page={page} total={total} onPage={setPage} />
        </>
      ) : (
        <div className="map-table-scroll"><table className="map-table map-table--scheduled-plunder"><tbody><tr><td className="map-empty">No scheduled plunder jobs.</td></tr></tbody></table></div>
      )}
    </section>
  );
}
