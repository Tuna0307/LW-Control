import { CITY_BASE_CELL_SIZE, cityGridRow, cityLayoutIssues, cityMovedOccupiedPoints, cityPlacementMap, cityPlacementSignature, cityRegionForPoint, citySetPlacement, previewCityLayoutFixture } from "./previewRemainingPagesContracts.js";
import { useEffect, useMemo, useRef, useState } from "react";
import { useI18n } from "./i18n.jsx";

const CITY_LAYOUT_PREVIEW_STATES = new Set([
  "city-layout-populated",
  "city-layout-populated-conflict",
  "city-layout-populated-moved",
  "city-layout-stale",
  "city-layout-outside-city",
  "city-layout-server-valid",
  "city-layout-applying",
  "city-layout-loading",
  "city-layout-error",
]);

export function CityLayoutPage({ previewState = "", online = false }) {
  const { t } = useI18n();
  const previewEnabled = CITY_LAYOUT_PREVIEW_STATES.has(previewState);
  const fixture = useMemo(() => previewEnabled && previewState !== "city-layout-loading" && previewState !== "city-layout-error" ? previewCityLayoutFixture(previewState) : null, [previewEnabled, previewState]);
  const layout = fixture;
  const loading = previewState === "city-layout-loading";
  const [history, setHistory] = useState(() => ({ past: [], present: fixture?.placements ?? [], future: [] }));
  const [zoom, setZoom] = useState(CITY_BASE_CELL_SIZE);
  const [selectedUuid, setSelectedUuid] = useState(() => fixture?.buildings[0]?.uuid ?? "");
  const [selectedUuids, setSelectedUuids] = useState(() => fixture?.buildings[0] ? [fixture.buildings[0].uuid] : []);
  const [hoveredUuid, setHoveredUuid] = useState("");
  const [citySelectionState, setCitySelectionState] = useState(null);
  const [dragState, setRenderedDragState] = useState(null);
  const [serverValidation, setServerValidation] = useState(() => previewState === "city-layout-server-valid" ? { valid: true, issues: [], totalMoves: 1, temporaryMoves: 0 } : null);
  const [draftRevision, setDraftRevision] = useState(() => fixture?.draftRevision ?? 0);
  const [stale, setStale] = useState(previewState === "city-layout-stale");
  const [errorText, setErrorText] = useState(previewState === "city-layout-error" ? t("common.actionFailed") : "");
  const [applyConfirmOpen, setApplyConfirmOpen] = useState(false);
  const [applyPreparing, setApplyPreparing] = useState(false);
  const dragStateRef = useRef(null);
  const setDragState = (next) => {
    const value = typeof next === "function" ? next(dragStateRef.current) : next;
    dragStateRef.current = value;
    setRenderedDragState(value);
  };
  const viewportRef = useRef(null);
  const gridRef = useRef(null);
  const draftTimerRef = useRef(0);
  const savedSignatureRef = useRef(cityPlacementSignature(fixture?.placements ?? []));
  const placements = history.present;
  const applyStatus = previewState === "city-layout-applying" ? { state: "running", totalMoves: 4, completedMoves: 2, jobId: "preview-city-layout" } : null;
  const applyRunning = applyStatus?.state === "running" || applyStatus?.state === "cancelling";
  const busy = applyPreparing || applyConfirmOpen || applyRunning;
  const cellsByPoint = useMemo(() => new Map(layout?.cells.map((cell) => [cell.pointId, cell]) ?? []), [layout]);
  const cellsByCoordinate = useMemo(() => new Map(layout?.cells.map((cell) => [`${cell.x}:${cell.y}`, cell]) ?? []), [layout]);
  const buildingsByUuid = useMemo(() => new Map(layout?.buildings.map((building) => [building.uuid, building]) ?? []), [layout]);
  const placementMap = useMemo(() => cityPlacementMap(placements), [placements]);
  const selectedSet = useMemo(() => new Set(selectedUuids), [selectedUuids]);
  const selected = buildingsByUuid.get(selectedUuid) ?? null;
  const region = layout?.regions[0] ?? null;
  const regionBuildings = useMemo(() => !layout || !region ? [] : layout.buildings.filter((building) => cityRegionForPoint(layout, placementMap.get(building.uuid) ?? building.pointId) === region.id), [layout, placementMap, region]);
  const issues = useMemo(() => layout ? cityLayoutIssues(layout, placements) : [], [layout, placements]);
  const changedIds = useMemo(() => new Set(placements.map((placement) => placement.uuid)), [placements]);
  const displayName = (building) => building?.name || building?.nameKey || "";

  const commitPlacements = (next) => {
    if (cityPlacementSignature(next) === cityPlacementSignature(placements)) return;
    setHistory((current) => ({ past: [...current.past, current.present], present: next, future: [] }));
    setServerValidation(null);
  };

  const undo = () => {
    if (busy) return;
    setHistory((current) => {
    const previous = current.past[current.past.length - 1];
    return previous ? { past: current.past.slice(0, -1), present: previous, future: [current.present, ...current.future] } : current;
    });
  };
  const redo = () => {
    if (busy) return;
    setHistory((current) => {
    const next = current.future[0];
    return next ? { past: [...current.past, current.present], present: next, future: current.future.slice(1) } : current;
    });
  };

  const restoreInitial = () => {
    if (!busy && placements.length) commitPlacements([]);
  };

  const toggleSelection = (building, additive = false) => {
    if (!additive) {
      setSelectedUuid(building.uuid);
      setSelectedUuids([building.uuid]);
      return;
    }
    setSelectedUuids((current) => {
      if (current.includes(building.uuid)) {
        const next = current.filter((uuid) => uuid !== building.uuid);
        if (selectedUuid === building.uuid) setSelectedUuid(next[0] ?? "");
        return next;
      }
      setSelectedUuid(building.uuid);
      return [...current, building.uuid];
    });
  };

  const pointFromClient = (clientX, clientY) => {
    if (!gridRef.current || !region) return 0;
    const rect = gridRef.current.getBoundingClientRect();
    const style = window.getComputedStyle(gridRef.current);
    const paddingLeft = Number.parseFloat(style.paddingLeft) || 0;
    const paddingTop = Number.parseFloat(style.paddingTop) || 0;
    const width = region.bounds.maxX - region.bounds.minX + 1;
    const height = region.bounds.maxY - region.bounds.minY + 1;
    const column = Math.floor((clientX - rect.left - paddingLeft) / zoom);
    const row = Math.floor((clientY - rect.top - paddingTop) / zoom);
    if (column < 0 || column >= width || row < 0 || row >= height) return 0;
    const x = region.bounds.minX + column;
    const y = region.bounds.maxY - row;
    return cellsByCoordinate.get(`${x}:${y}`)?.pointId ?? 0;
  };

  const dragPointFromClient = (clientX, clientY, grabOffset) => {
    if (!gridRef.current || !region) return 0;
    const rect = gridRef.current.getBoundingClientRect();
    const style = window.getComputedStyle(gridRef.current);
    const paddingLeft = Number.parseFloat(style.paddingLeft) || 0;
    const paddingTop = Number.parseFloat(style.paddingTop) || 0;
    const width = region.bounds.maxX - region.bounds.minX + 1;
    const height = region.bounds.maxY - region.bounds.minY + 1;
    const column = Math.min(width - 1, Math.max(0, Math.floor((clientX - rect.left - paddingLeft) / zoom)));
    const row = Math.min(height - 1, Math.max(0, Math.floor((clientY - rect.top - paddingTop) / zoom)));
    const x = Math.min(region.bounds.maxX, Math.max(region.bounds.minX, region.bounds.minX + column + (grabOffset?.x ?? 0)));
    const y = Math.min(region.bounds.maxY, Math.max(region.bounds.minY, region.bounds.maxY - row + (grabOffset?.y ?? 0)));
    return cellsByCoordinate.get(`${x}:${y}`)?.pointId ?? 0;
  };

  const groupTargetPointFor = (anchorUuid, targetPointId, uuid) => {
    const anchor = buildingsByUuid.get(anchorUuid);
    const anchorSource = cellsByPoint.get(placementMap.get(anchorUuid) ?? anchor?.pointId);
    const anchorTarget = cellsByPoint.get(targetPointId);
    const building = buildingsByUuid.get(uuid);
    const source = cellsByPoint.get(placementMap.get(uuid) ?? building?.pointId);
    if (!anchor || !anchorSource || !anchorTarget || !building || !source) return 0;
    return cellsByCoordinate.get(`${anchorTarget.x + source.x - anchorSource.x}:${anchorTarget.y + source.y - anchorSource.y}`)?.pointId ?? 0;
  };

  const groupFitsRegion = (anchorUuid, targetPointId, uuids) => {
    if (!region || !targetPointId || !uuids.length) return false;
    return uuids.every((uuid) => {
      const building = buildingsByUuid.get(uuid);
      const memberTarget = groupTargetPointFor(anchorUuid, targetPointId, uuid);
      if (!building || !memberTarget) return false;
      const occupied = cityMovedOccupiedPoints(building, memberTarget, cellsByPoint, cellsByCoordinate);
      return occupied.length === building.tileX * building.tileY && occupied.every((pointId) => cellsByPoint.get(pointId)?.regionId === region.id);
    });
  };

  const groupPlacementFor = (anchorUuid, targetPointId, uuids) => {
    if (!groupFitsRegion(anchorUuid, targetPointId, uuids)) return null;
    let next = placements;
    for (const uuid of uuids) {
      const building = buildingsByUuid.get(uuid);
      const memberTarget = groupTargetPointFor(anchorUuid, targetPointId, uuid);
      if (!building || !memberTarget) return null;
      next = citySetPlacement(next, building, memberTarget);
    }
    return next;
  };

  const moveDragToClient = (clientX, clientY) => {
    setDragState((current) => {
      if (!current) return current;
      const candidate = dragPointFromClient(clientX, clientY, current.grabOffset);
      let accepted = groupFitsRegion(current.anchorUuid, candidate, current.uuids) ? candidate : 0;
      if (!accepted) {
        const previous = cellsByPoint.get(current.targetPointId);
        const candidateCell = cellsByPoint.get(candidate);
        if (previous && candidateCell) {
          const sameX = cellsByCoordinate.get(`${previous.x}:${candidateCell.y}`)?.pointId ?? 0;
          const sameY = cellsByCoordinate.get(`${candidateCell.x}:${previous.y}`)?.pointId ?? 0;
          if (sameX !== current.targetPointId && groupFitsRegion(current.anchorUuid, sameX, current.uuids)) accepted = sameX;
          else if (sameY !== current.targetPointId && groupFitsRegion(current.anchorUuid, sameY, current.uuids)) accepted = sameY;
        }
      }
      return accepted ? { ...current, targetPointId: accepted } : current;
    });
  };

  const finishPointer = () => {
    const currentDrag = dragStateRef.current;
    if (currentDrag) {
      const next = groupPlacementFor(currentDrag.anchorUuid, currentDrag.targetPointId, currentDrag.uuids);
      if (next) {
        const nextIssues = cityLayoutIssues(layout, next);
        if (nextIssues.length) setServerValidation({ valid: false, issues: nextIssues, totalMoves: 0, temporaryMoves: 0 });
        else {
          commitPlacements(next);
          setServerValidation({ valid: true, issues: [], totalMoves: next.length, temporaryMoves: 0 });
        }
      }
      setDragState(null);
      return;
    }
    if (!citySelectionState || !layout || !region) return;
    const start = cellsByPoint.get(citySelectionState.startPointId);
    const current = cellsByPoint.get(citySelectionState.currentPointId);
    if (start && current) {
      const minX = Math.min(start.x, current.x);
      const maxX = Math.max(start.x, current.x);
      const minY = Math.min(start.y, current.y);
      const maxY = Math.max(start.y, current.y);
      const hits = regionBuildings.filter((building) => building.movable).filter((building) => {
        const occupied = cityMovedOccupiedPoints(building, placementMap.get(building.uuid) ?? building.pointId, cellsByPoint, cellsByCoordinate);
        return occupied.some((pointId) => {
          const cell = cellsByPoint.get(pointId);
          return cell && cell.x >= minX && cell.x <= maxX && cell.y >= minY && cell.y <= maxY;
        });
      }).map((building) => building.uuid);
      setSelectedUuids((currentSelection) => citySelectionState.additive ? [...new Set([...currentSelection, ...hits])] : hits);
      setSelectedUuid(hits[0] ?? (citySelectionState.additive ? selectedUuid : ""));
    }
    setCitySelectionState(null);
  };

  const saveDraftNow = () => {
    if (!previewEnabled || stale || busy) return;
    if (draftTimerRef.current) window.clearTimeout(draftTimerRef.current);
    savedSignatureRef.current = cityPlacementSignature(placements);
    setDraftRevision((revision) => revision + 1);
  };

  const prepareApply = async () => {
    if (!layout || stale || busy || !placements.length || !previewEnabled) return;
    setApplyConfirmOpen(false);
    setApplyPreparing(true);
    try {
      await Promise.resolve();
      const nextIssues = cityLayoutIssues(layout, placements);
      if (nextIssues.length) {
        setServerValidation({ valid: false, issues: nextIssues, totalMoves: 0, temporaryMoves: 0 });
        return;
      }
      setServerValidation({ valid: true, issues: [], totalMoves: placements.length, temporaryMoves: 0 });
      setApplyConfirmOpen(true);
    } catch {
      setErrorText(t("common.actionFailed"));
    } finally {
      setApplyPreparing(false);
    }
  };

  useEffect(() => {
    if (!previewEnabled || stale) return undefined;
    const signature = cityPlacementSignature(placements);
    if (signature === savedSignatureRef.current) return undefined;
    draftTimerRef.current = window.setTimeout(() => {
      savedSignatureRef.current = signature;
      setDraftRevision((revision) => revision + 1);
      draftTimerRef.current = 0;
    }, 500);
    return () => {
      if (draftTimerRef.current) window.clearTimeout(draftTimerRef.current);
      draftTimerRef.current = 0;
    };
  }, [placements, previewEnabled, stale]);

  useEffect(() => {
    const onKeyDown = (event) => {
      if (event.key === "Escape") {
        setDragState(null);
        setCitySelectionState(null);
        setSelectedUuids([]);
        setSelectedUuid("");
        return;
      }
      if (busy || !(event.ctrlKey || event.metaKey)) return;
      if (event.key.toLowerCase() === "z") {
        event.preventDefault();
        if (event.shiftKey) redo();
        else undo();
      } else if (event.key.toLowerCase() === "y") {
        event.preventDefault();
        redo();
      }
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [busy]);

  if (!previewEnabled && !online) return <div className="panel city-layout-empty">{t("cityLayout.offline")}</div>;
  if (!layout) return <div className="panel city-layout-empty">{loading ? t("common.processing") : errorText || t("cityLayout.noData")}</div>;
  const currentHover = buildingsByUuid.get(hoveredUuid) ?? selected;
  const lassoStyle = (() => {
    if (!citySelectionState || !region) return null;
    const start = cellsByPoint.get(citySelectionState.startPointId);
    const current = cellsByPoint.get(citySelectionState.currentPointId);
    if (!start || !current) return null;
    const minX = Math.min(start.x, current.x);
    const maxX = Math.max(start.x, current.x);
    const minY = Math.min(start.y, current.y);
    const maxY = Math.max(start.y, current.y);
    return { gridColumn: `${minX - region.bounds.minX + 1} / span ${maxX - minX + 1}`, gridRow: `${cityGridRow(region.bounds, maxY)} / span ${maxY - minY + 1}` };
  })();
  return (
    <section className="city-layout-panel" data-preview-fixture={previewEnabled ? previewState : undefined}>
      <header className="city-layout-header">
        <div><h2>{t("cityLayout.title")}</h2><span>{t("cityLayout.stats", { cells: layout.cells.filter((cell) => cell.unlocked).length, buildings: layout.buildings.length, movable: layout.buildings.filter((item) => item.movable).length })}</span></div>
        <div className="city-layout-actions">
          <button type="button" disabled={loading || busy} onClick={() => { setErrorText(""); setServerValidation(null); }}>{t("common.refresh")}</button>
          <button type="button" disabled={!history.past.length || busy} onClick={undo}>{t("cityLayout.undo")}</button>
          <button type="button" disabled={!history.future.length || busy} onClick={redo}>{t("cityLayout.redo")}</button>
          <button type="button" disabled={!placements.length || busy} onClick={restoreInitial}>{t("cityLayout.restoreInitial")}</button>
          <button type="button" disabled={stale || busy || !previewEnabled} onClick={saveDraftNow}>{t("cityLayout.saveDraft")}</button>
          <button type="button" className="primary" disabled={stale || !placements.length || busy || !previewEnabled} onClick={() => void prepareApply()}>{t("cityLayout.apply")}</button>
        </div>
      </header>
      {stale ? <div className="city-layout-warning">{t("cityLayout.stale")} <button type="button" onClick={() => { setHistory({ past: [], present: [], future: [] }); setDraftRevision(0); savedSignatureRef.current = ""; setStale(false); }}>{t("cityLayout.discardDraft")}</button></div> : null}
      {layout.isInCity ? null : <div className="city-layout-warning">{t("cityLayout.enterCity")}</div>}
      {errorText ? <div className="city-layout-error">{errorText}</div> : null}
      <div className="city-layout-workbench">
        <div className="city-layout-center">
          <div className="city-layout-main-heading">
            <strong>{t("cityLayout.region.main")}</strong>
            <div className="city-layout-zoom" role="group" aria-label={t("cityLayout.zoom")}>
              <button type="button" disabled={zoom <= 4 || Boolean(dragState) || Boolean(citySelectionState)} onClick={() => setZoom((value) => Math.max(4, value - 4))}>{t("cityLayout.zoomOut")}</button>
              <output aria-live="polite">{Math.round(zoom / CITY_BASE_CELL_SIZE * 100)}%</output>
              <button type="button" disabled={zoom >= 72 || Boolean(dragState) || Boolean(citySelectionState)} onClick={() => setZoom((value) => Math.min(72, value + 4))}>{t("cityLayout.zoomIn")}</button>
              <button type="button" disabled={Boolean(dragState) || Boolean(citySelectionState)} onClick={() => { const element = viewportRef.current; if (!element || !region) return; const width = region.bounds.maxX - region.bounds.minX + 1; const height = region.bounds.maxY - region.bounds.minY + 1; setZoom(Math.max(4, Math.min(72, Math.floor(Math.min((element.clientWidth - 28) / width, (element.clientHeight - 28) / height))))); element.scrollTo?.(0, 0); }}>{t("cityLayout.fitCanvas")}</button>
            </div>
          </div>
          <div className="city-layout-name-preview" aria-live="polite">{currentHover ? `${displayName(currentHover)} · ${t("cityLayout.level", { level: currentHover.level })}` : t("cityLayout.selectBuilding")}</div>
          <div className="city-layout-controls"><strong>{t("cityLayout.controls.title")}</strong><span>{t("cityLayout.controls.boxSelect")}</span><span>{t("cityLayout.controls.addSelect")}</span><span>{t("cityLayout.controls.toggle")}</span><span>{t("cityLayout.controls.move")}</span><span>{t("cityLayout.controls.history")}</span></div>
          <div className="city-layout-grid-viewport" ref={viewportRef}>
            <div
              ref={gridRef}
              className={`city-layout-grid${dragState ? " dragging" : ""}`}
              style={{ gridTemplateColumns: `repeat(${region.bounds.maxX - region.bounds.minX + 1}, ${zoom}px)`, gridTemplateRows: `repeat(${region.bounds.maxY - region.bounds.minY + 1}, ${zoom}px)` }}
              onPointerDown={(event) => {
                if (event.target !== event.currentTarget || busy) return;
                const pointId = pointFromClient(event.clientX, event.clientY);
                if (!pointId) return;
                event.currentTarget.setPointerCapture?.(event.pointerId);
                setCitySelectionState({ startPointId: pointId, currentPointId: pointId, additive: event.ctrlKey });
              }}
              onPointerMove={(event) => {
                if (dragStateRef.current) moveDragToClient(event.clientX, event.clientY);
                else if (citySelectionState) {
                  const pointId = pointFromClient(event.clientX, event.clientY);
                  if (pointId) setCitySelectionState((current) => current ? { ...current, currentPointId: pointId } : current);
                }
              }}
              onPointerUp={finishPointer}
              onPointerCancel={() => { setDragState(null); setCitySelectionState(null); }}
            >
              {layout.cells.filter((cell) => cell.regionId === region.id).map((cell) => <span key={cell.pointId} className={`city-layout-cell${cell.unlocked ? "" : " locked"}${cell.road ? " road" : ""}${cell.flagOnly ? " flag-only" : ""}`} style={{ gridColumn: cell.x - region.bounds.minX + 1, gridRow: cityGridRow(region.bounds, cell.y) }} />)}
              {lassoStyle ? <span className="city-layout-lasso" style={lassoStyle} /> : null}
              {regionBuildings.map((building) => {
                const previewTarget = dragState?.uuids.includes(building.uuid) ? (() => {
                  const next = groupPlacementFor(dragState.anchorUuid, dragState.targetPointId, dragState.uuids);
                  return cityPlacementMap(next ?? placements).get(building.uuid) ?? building.pointId;
                })() : placementMap.get(building.uuid) ?? building.pointId;
                const occupied = cityMovedOccupiedPoints(building, previewTarget, cellsByPoint, cellsByCoordinate).map((pointId) => cellsByPoint.get(pointId)).filter(Boolean);
                if (!occupied.length) return null;
                const minX = Math.min(...occupied.map((cell) => cell.x));
                const maxX = Math.max(...occupied.map((cell) => cell.x));
                const minY = Math.min(...occupied.map((cell) => cell.y));
                const maxY = Math.max(...occupied.map((cell) => cell.y));
                return <button
                key={building.uuid}
                data-city-building-id={building.uuid}
                type="button"
                title={`${displayName(building)} · ${t("cityLayout.level", { level: building.level })}`}
                className={`city-layout-building${selectedSet.has(building.uuid) ? " selected" : ""}${changedIds.has(building.uuid) ? " changed" : ""}`}
                style={{ gridColumn: `${minX - region.bounds.minX + 1} / span ${maxX - minX + 1}`, gridRow: `${cityGridRow(region.bounds, maxY)} / span ${maxY - minY + 1}` }}
                aria-label={`${displayName(building)} · ${t("cityLayout.level", { level: building.level })}`}
                aria-pressed={selectedSet.has(building.uuid)}
                onMouseEnter={() => setHoveredUuid(building.uuid)}
                onMouseLeave={(event) => { if (document.activeElement !== event.currentTarget) setHoveredUuid(""); }}
                onFocus={() => setHoveredUuid(building.uuid)}
                onBlur={() => setHoveredUuid("")}
                onClick={(event) => { event.stopPropagation(); if (event.detail === 0 && !busy) toggleSelection(building, event.ctrlKey); }}
                onPointerDown={(event) => {
                  if (!building.movable || busy) return;
                  event.stopPropagation();
                  if (event.shiftKey) {
                    const pointId = pointFromClient(event.clientX, event.clientY);
                    if (!pointId) return;
                    event.currentTarget.setPointerCapture?.(event.pointerId);
                    setCitySelectionState({ startPointId: pointId, currentPointId: pointId, additive: true });
                    return;
                  }
                  if (event.ctrlKey) { toggleSelection(building, true); return; }
                  event.currentTarget.setPointerCapture?.(event.pointerId);
                  const uuids = (selectedSet.has(building.uuid) ? selectedUuids : [building.uuid]).filter((uuid) => buildingsByUuid.get(uuid)?.movable);
                  if (!selectedSet.has(building.uuid)) setSelectedUuids([building.uuid]);
                  setSelectedUuid(building.uuid);
                  const pointerPoint = cellsByPoint.get(pointFromClient(event.clientX, event.clientY));
                  const anchorPoint = cellsByPoint.get(placementMap.get(building.uuid) ?? building.pointId);
                  const grabOffset = pointerPoint && anchorPoint ? { x: anchorPoint.x - pointerPoint.x, y: anchorPoint.y - pointerPoint.y } : { x: 0, y: 0 };
                  setDragState({ anchorUuid: building.uuid, uuids, targetPointId: placementMap.get(building.uuid) ?? building.pointId, grabOffset });
                }}
                onPointerMove={(event) => {
                  if (!dragStateRef.current || dragStateRef.current.anchorUuid !== building.uuid) return;
                  moveDragToClient(event.clientX, event.clientY);
                }}
                onPointerUp={(event) => {
                  event.stopPropagation();
                  finishPointer();
                }}
              >{(maxX - minX + 1) * zoom >= 64 && (maxY - minY + 1) * zoom >= 48 ? <strong className="city-layout-building-name">{displayName(building)}</strong> : null}<small className="city-layout-building-level">{t("cityLayout.level", { level: building.level })}</small></button>;
              })}
            </div>
          </div>
          <div className="city-layout-legend"><span><i className="available" />{t("cityLayout.cell.available")}</span><span><i className="road" />{t("cityLayout.cell.road")}</span><span><i className="flag-only" />{t("cityLayout.cell.flagOnly")}</span><span><i className="locked" />{t("cityLayout.cell.locked")}</span></div>
        </div>
        <aside className="city-layout-inspector">
          <strong>{t("cityLayout.properties")}</strong>
          {selected ? <><div className="city-layout-selected"><span><strong>{displayName(selected)}</strong><small>{t("cityLayout.level", { level: selected.level })}</small></span></div><dl><div><dt>{t("cityLayout.footprint")}</dt><dd>{selected.tileX}×{selected.tileY}</dd></div><div><dt>{t("cityLayout.movable")}</dt><dd>{t(selected.movable ? "common.yes" : "common.no")}</dd></div><div><dt>{t("cityLayout.rule")}</dt><dd>{t(selected.isFlag ? "cityLayout.rule.flag" : "cityLayout.rule.normal")}</dd></div></dl></> : null}
          <div className={`city-layout-validation ${issues.length === 0 ? "valid" : "invalid"}`}>{issues.length === 0 ? t("cityLayout.validation.valid") : t(`cityLayout.issue.${issues[0].code}`)}</div>
          {serverValidation?.valid ? <div className="city-layout-validation valid">{t("cityLayout.validation.server", { moves: serverValidation.totalMoves, temporary: serverValidation.temporaryMoves })}</div> : null}
          <strong>{t("cityLayout.changes", { count: placements.length })}</strong>
          <div className="city-layout-changes">{placements.length === 0 ? <span>{t("cityLayout.noChanges")}</span> : placements.map((placement) => { const building = buildingsByUuid.get(placement.uuid); return building ? <div key={placement.uuid}><span>{displayName(building)}</span><small>{t("cityLayout.level", { level: building.level })}</small></div> : null; })}</div>
          {applyRunning ? <div className="city-layout-progress"><span>{t("cityLayout.applyProgress", { current: applyStatus.completedMoves, total: applyStatus.totalMoves })}</span><progress max={applyStatus.totalMoves || 1} value={applyStatus.completedMoves} /><button type="button" onClick={() => setErrorText(t("common.actionFailed"))}>{t("cityLayout.cancelApply")}</button></div> : null}
        </aside>
      </div>
      <footer className="city-layout-footer"><span>{t("cityLayout.draftRevision", { revision: draftRevision })}</span><span>{t("cityLayout.conflicts", { count: issues.length })}</span></footer>
      {applyConfirmOpen ? <div className="scheme-modal-backdrop" data-preview-only-confirm="city-layout"><section className="scheme-modal" role="dialog" aria-modal="true"><strong>{t("cityLayout.applyConfirm", { count: serverValidation?.totalMoves ?? placements.length })}</strong><div className="scheme-modal-actions"><button type="button" onClick={() => setApplyConfirmOpen(false)}>{t("common.cancel")}</button><button className="primary" type="button" onClick={() => { setApplyConfirmOpen(false); setErrorText(t("common.actionFailed")); }}>{t("cityLayout.apply")}</button></div></section></div> : null}
    </section>
  );
}
