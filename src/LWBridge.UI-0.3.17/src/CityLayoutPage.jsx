import { CITY_BASE_CELL_SIZE, cityGridRow, cityLayoutIssues, cityMovedOccupiedPoints, cityPlacementMap, cityPlacementSignature, cityRegionForPoint, citySetPlacement, previewCityLayoutFixture } from "./previewRemainingPagesContracts.js";
import { useEffect, useMemo, useRef, useState } from "react";
import { useI18n } from "./i18n.jsx";
import { GameAssetImage } from "./GameAssetImage.jsx";
import { backendBridge } from "./backendBridge.js";
import { translateActionError } from "./mapInteractions.js";
import { visibleCityCells, cityBuildingScrollCenter } from "./cityViewport.js";

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
const CITY_LAYOUT_LOCALIZED_ISSUES = new Set([
  "CITY_LAYOUT_ROAD", "CITY_LAYOUT_FLAG_ONLY", "CITY_LAYOUT_LOCKED", "CITY_LAYOUT_OUTSIDE",
  "CITY_LAYOUT_OVERLAP", "CITY_LAYOUT_IMMOVABLE", "CITY_LAYOUT_ZONE",
  "CITY_LAYOUT_BUILDING_MISSING", "CITY_LAYOUT_NO_BUFFER",
]);

function cityErrorCode(value) {
  if (value && typeof value === "object") {
    for (const key of ["code", "errorCode", "issueCode"]) {
      if (typeof value[key] === "string" && value[key]) return value[key];
    }
  }
  const message = value && typeof value.message === "string" ? value.message : String(value ?? "");
  return message.match(/\bCITY_LAYOUT_[A-Z0-9_]+\b/)?.[0] || "";
}

function cityErrorText(t, value) {
  const code = cityErrorCode(value);
  if (code === "CITY_LAYOUT_STALE") return t("cityLayout.stale");
  if (CITY_LAYOUT_LOCALIZED_ISSUES.has(code)) return t(`cityLayout.issue.${code}`);
  return translateActionError(t, value);
}

export function CityLayoutPage({ previewState = "", online = false }) {
  const { t, language } = useI18n();
  const chinese = language?.startsWith("zh");
  const previewEnabled = CITY_LAYOUT_PREVIEW_STATES.has(previewState);
  const fixture = useMemo(() => previewEnabled && previewState !== "city-layout-loading" && previewState !== "city-layout-error" ? previewCityLayoutFixture(previewState) : null, [previewEnabled, previewState]);
  const [liveLayout, setLiveLayout] = useState(null);
  const [liveLoading, setLiveLoading] = useState(false);
  const layout = previewEnabled ? fixture : liveLayout;
  const loading = previewEnabled ? previewState === "city-layout-loading" : liveLoading;
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
  const [liveApplyStatus, setLiveApplyStatus] = useState(null);
  const [restoreReview, setRestoreReview] = useState(null);
  const [restoreReviewBusy, setRestoreReviewBusy] = useState(false);
  const restoreReviewInFlight = useRef(false);
  const [reloadRevision, setReloadRevision] = useState(0);
  const [localizedNames, setLocalizedNames] = useState({ context: "", values: {}, error: "", loading: false });
  const [nameRetry, setNameRetry] = useState(0);
  const nameGeneration = useRef(0);
  const draftQueueRef = useRef(Promise.resolve());
  const draftRevisionRef = useRef(fixture?.draftRevision ?? 0);
  const dragStateRef = useRef(null);
  const setDragState = (next) => {
    const value = typeof next === "function" ? next(dragStateRef.current) : next;
    dragStateRef.current = value;
    setRenderedDragState(value);
  };
  const viewportRef = useRef(null);
  const [viewport, setViewport] = useState(null);
  const centeredRegion = useRef("");
  const gridRef = useRef(null);
  const draftTimerRef = useRef(0);
  const savedSignatureRef = useRef(cityPlacementSignature(fixture?.placements ?? []));
  const requireCityOwner = (owner) => {
    if (backendBridge.isCurrentProfileOwner(owner)) return;
    const error = new Error("City Layout profile changed while an action was pending.");
    error.code = "PROFILE_GENERATION_RETIRED";
    throw error;
  };
  const placements = history.present;
  const applyStatus = previewEnabled ? (previewState === "city-layout-applying" ? { state: "running", totalMoves: 4, completedMoves: 2, jobId: "preview-city-layout" } : null) : liveApplyStatus;
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
  const renderedCells = useMemo(() => layout && region ? visibleCityCells(layout.cells, region, viewport, zoom) : [], [layout, region, viewport, zoom]);
  useEffect(() => {
    const element = viewportRef.current;
    if (!element || !region) return undefined;
    let frame = 0;
    const measure = () => {
      frame = 0;
      const scope = `${backendBridge.profileId}|${backendBridge.profileGeneration}|${region.id}`;
      if (element.clientWidth > 0 && element.clientHeight > 0 && centeredRegion.current !== scope) {
        const center = cityBuildingScrollCenter(regionBuildings, cellsByPoint, region.bounds, zoom, element.clientWidth, element.clientHeight, placementMap);
        if (center) { element.scrollLeft = center.left; element.scrollTop = center.top; }
        centeredRegion.current = scope;
      }
      const next = { left: element.scrollLeft, top: element.scrollTop, width: element.clientWidth, height: element.clientHeight };
      setViewport((current) => current && Object.keys(next).every((key) => current[key] === next[key]) ? current : next);
    };
    const schedule = () => { if (!frame) frame = window.requestAnimationFrame(measure); };
    measure();
    element.addEventListener("scroll", schedule, { passive: true });
    const observer = typeof ResizeObserver === "function" ? new ResizeObserver(schedule) : null;
    observer?.observe(element);
    window.addEventListener("resize", schedule);
    return () => { element.removeEventListener("scroll", schedule); window.removeEventListener("resize", schedule); observer?.disconnect(); if (frame) window.cancelAnimationFrame(frame); };
  }, [region, cellsByPoint, zoom, regionBuildings, placementMap]);
  const issues = useMemo(() => layout ? cityLayoutIssues(layout, placements) : [], [layout, placements]);
  const validationIssue = issues[0] ?? (serverValidation?.valid === false ? serverValidation.issues?.[0] : null);
  const validationInvalid = issues.length > 0 || serverValidation?.valid === false;
  const validationIssueText = validationIssue?.code === "CITY_LAYOUT_STALE" ? t("cityLayout.stale")
    : CITY_LAYOUT_LOCALIZED_ISSUES.has(validationIssue?.code) ? t(`cityLayout.issue.${validationIssue.code}`)
      : t("common.actionFailed");
  const changedIds = useMemo(() => new Set(placements.map((placement) => placement.uuid)), [placements]);
  const nameKeys = useMemo(() => [...new Set((layout?.buildings || []).map((building) => building.nameKey).filter((key) => typeof key === "string" && key))].sort(), [layout]);
  const nameSignature = nameKeys.join("\0");
  const nameContext = `${backendBridge.profileId}|${backendBridge.profileGeneration}|${language}|${reloadRevision}|${nameSignature}`;
  const currentNameContext = useRef(nameContext);
  currentNameContext.current = nameContext;
  const names = localizedNames.context === nameContext ? localizedNames.values : {};
  const displayName = (building) => names[building?.nameKey] && names[building.nameKey] !== building.nameKey
    ? names[building.nameKey] : building?.name || building?.nameKey || (building?.itemId != null ? `#${building.itemId}` : building?.uuid || "");
  useEffect(() => {
    const generation = ++nameGeneration.current;
    let retired = false;
    const current = () => !retired && nameGeneration.current === generation && currentNameContext.current === nameContext;
    setLocalizedNames({ context: nameContext, values: {}, error: "", loading: false });
    if (previewEnabled || !online || !backendBridge.profileId || !nameKeys.length) return undefined;
    const owner = backendBridge.currentProfileOwner();
    setLocalizedNames({ context: nameContext, values: {}, error: "", loading: true });
    const read = async (keys) => {
      requireCityOwner(owner);
      const response = await backendBridge.invokeProfileScoped("lastwar_map_localize", { language, keys });
      requireCityOwner(owner);
      if (response?.language !== language || !response.buildId || !response.gameRoot || !response.localeSha256 || !response.englishSha256 || !response.texts || keys.some((key) => typeof response.texts[key] !== "string")) throw new Error("Installed game building text source is unavailable.");
      return response;
    };
    const identity = (value) => [value.buildId, value.gameRoot.toLowerCase(), value.localeSha256, value.englishSha256].join("|");
    (async () => {
      try {
        const expected = identity(await read([])), values = {};
        for (let offset = 0; offset < nameKeys.length; offset += 200) {
          if (!current()) return;
          const response = await read(nameKeys.slice(offset, offset + 200));
          if (identity(response) !== expected) throw new Error("Installed game building locale changed while names were loading.");
          Object.assign(values, response.texts);
        }
        if (current()) setLocalizedNames({ context: nameContext, values, error: "", loading: false });
      } catch (error) { if (current()) setLocalizedNames({ context: nameContext, values: {}, error: cityErrorText(t, error), loading: false }); }
    })();
    return () => { retired = true; nameGeneration.current += 1; };
  // Translation and presentation are independent of the saved movement plan.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [previewEnabled, online, language, nameContext, nameSignature, nameRetry]);

  const commitPlacements = (next) => {
    if (cityPlacementSignature(next) === cityPlacementSignature(placements)) return;
    setHistory((current) => ({ past: [...current.past, current.present], present: next, future: [] }));
    setServerValidation(null);
  };

  const undo = () => {
    if (busy) return;
    setServerValidation(null);
    setHistory((current) => {
    const previous = current.past[current.past.length - 1];
    return previous ? { past: current.past.slice(0, -1), present: previous, future: [current.present, ...current.future] } : current;
    });
  };
  const redo = () => {
    if (busy) return;
    setServerValidation(null);
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
        else commitPlacements(next);
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

  const saveDraftNow = async () => {
    if (!layout || stale || busy) return;
    if (draftTimerRef.current) window.clearTimeout(draftTimerRef.current);
    const signature = cityPlacementSignature(placements);
    if (previewEnabled) {
      savedSignatureRef.current = signature;
      setDraftRevision((revision) => revision + 1);
      setErrorText("");
      return;
    }
    try {
      await enqueueDraftSave(placements, layout.layoutRevision);
      setErrorText("");
    } catch (error) {
      setErrorText(cityErrorText(t, error));
    }
  };

  const enqueueDraftSave = (nextPlacements, baseRevision) => {
    const owner = backendBridge.currentProfileOwner();
    const value = { version: 1, baseRevision, placements: nextPlacements, updatedAt: Date.now() };
    const signature = cityPlacementSignature(nextPlacements);
    const save = () => {
      // A deferred write must retain its originating profile. The shared
      // bridge selects a profile when invoked, which might now be different.
      requireCityOwner(owner);
      return backendBridge.invokeProfileScoped("city_layout_draft_save", {
      revision: draftRevisionRef.current, value,
    }).then((record) => {
      requireCityOwner(owner);
      draftRevisionRef.current = record.revision;
      setDraftRevision(record.revision);
      savedSignatureRef.current = signature;
      return record;
    });
    };
    const queued = draftQueueRef.current.then(save);
    draftQueueRef.current = queued.catch(() => {});
    return queued;
  };

  useEffect(() => {
    if (previewEnabled || !online) {
      setLiveLayout(null);
      return undefined;
    }
    let active = true;
    setLiveLoading(true);
    setErrorText("");
    const owner = backendBridge.currentProfileOwner();
    const load = async () => {
      try {
        await draftQueueRef.current;
        requireCityOwner(owner);
        const snapshot = await backendBridge.invokeProfileScoped("city_layout_snapshot_get");
        if (!snapshot || !Array.isArray(snapshot.cells) || !Array.isArray(snapshot.buildings) ||
          !Array.isArray(snapshot.regions) || snapshot.layoutRevision == null) {
          throw new Error("CITY_LAYOUT_INVALID: game did not return a valid city snapshot");
        }
        requireCityOwner(owner);
        const draft = await backendBridge.invokeProfileScoped("city_layout_draft_get");
        requireCityOwner(owner);
        const status = await backendBridge.invokeProfileScoped("city_layout_apply_status");
        if (!active || !backendBridge.isCurrentProfileOwner(owner)) return;
        const saved = draft?.value?.version === 1 ? draft.value : null;
        const mismatch = saved != null && saved.baseRevision !== snapshot.layoutRevision;
        const next = saved?.placements && Array.isArray(saved.placements) ? saved.placements : [];
        setLiveLayout(snapshot);
        setHistory({ past: [], present: next, future: [] });
        savedSignatureRef.current = cityPlacementSignature(next);
        draftRevisionRef.current = Number(draft?.revision) || 0;
        setDraftRevision(draftRevisionRef.current);
        setStale(mismatch);
        setLiveApplyStatus(status);
        setServerValidation(null);
        // A completed/failed native job may trigger a snapshot refresh. Keep
        // the game-produced failure visible after that refresh replaces the
        // local status; otherwise the screen quietly returns to edit mode.
        setErrorText(status?.state === "failed"
          ? cityErrorText(t, { code: status.errorCode, message: status.error }) : "");
      } catch (error) {
        if (active) {
          setLiveLayout(null);
          setErrorText(cityErrorText(t, error));
        }
      } finally {
        if (active) setLiveLoading(false);
      }
    };
    void load();
    return () => { active = false; };
  }, [previewEnabled, online, reloadRevision, backendBridge.profileGeneration]);

  useEffect(() => {
    if (previewEnabled || !online || !applyRunning) return undefined;
    let active = true;
    const poll = async () => {
      try {
        const status = await backendBridge.invokeProfileScoped("city_layout_apply_status", {}, 5000);
        if (!active) return;
        setLiveApplyStatus(status);
        if (status?.state !== "running" && status?.state !== "cancelling") {
          setReloadRevision((value) => value + 1);
        }
      } catch (error) {
        if (active) setErrorText(cityErrorText(t, error));
      }
    };
    const timer = window.setInterval(() => void poll(), 500);
    return () => { active = false; window.clearInterval(timer); };
  }, [previewEnabled, online, applyRunning]);

  const discardStaleDraft = async () => {
    if (previewEnabled) {
      setHistory({ past: [], present: [], future: [] }); setDraftRevision(0);
      savedSignatureRef.current = ""; setStale(false); setErrorText(""); return;
    }
    const owner = backendBridge.currentProfileOwner();
    try {
      await draftQueueRef.current;
      requireCityOwner(owner);
      await backendBridge.invokeProfileScoped("city_layout_draft_clear", { revision: draftRevisionRef.current });
      requireCityOwner(owner);
      draftRevisionRef.current = 0;
      setDraftRevision(0);
      setHistory({ past: [], present: [], future: [] });
      savedSignatureRef.current = "";
      setStale(false);
      setErrorText("");
    } catch (error) { setErrorText(cityErrorText(t, error)); }
  };

  const prepareApply = async () => {
    if (!layout || stale || busy || !placements.length) return;
    setApplyConfirmOpen(false);
    setApplyPreparing(true);
    setErrorText("");
    const owner = backendBridge.currentProfileOwner();
    try {
      if (!previewEnabled) await draftQueueRef.current;
      requireCityOwner(owner);
      const nextIssues = cityLayoutIssues(layout, placements);
      if (nextIssues.length) {
        setServerValidation({ valid: false, issues: nextIssues, totalMoves: 0, temporaryMoves: 0 });
        return;
      }
      const validation = previewEnabled
        ? { valid: true, issues: [], totalMoves: placements.length, temporaryMoves: 0 }
        : await backendBridge.invokeProfileScoped("city_layout_validate", { baseRevision: layout.layoutRevision, placements });
      requireCityOwner(owner);
      setServerValidation(validation);
      if (!validation.valid) return;
      setApplyConfirmOpen(true);
    } catch (error) {
      setErrorText(cityErrorText(t, error));
    } finally {
      setApplyPreparing(false);
    }
  };

  useEffect(() => {
    if (!previewEnabled && (!layout || !online)) return undefined;
    if (stale) return undefined;
    const signature = cityPlacementSignature(placements);
    if (signature === savedSignatureRef.current) return undefined;
    draftTimerRef.current = window.setTimeout(() => {
      if (previewEnabled) {
        savedSignatureRef.current = signature;
        setDraftRevision((revision) => revision + 1);
        setErrorText("");
      } else {
        void enqueueDraftSave(placements, layout.layoutRevision)
          .then(() => setErrorText(""))
          .catch((error) => setErrorText(cityErrorText(t, error)));
      }
      draftTimerRef.current = 0;
    }, 500);
    return () => {
      if (draftTimerRef.current) window.clearTimeout(draftTimerRef.current);
      draftTimerRef.current = 0;
    };
  }, [placements, previewEnabled, stale, layout, online]);

  const applyConfirmed = async () => {
    setApplyConfirmOpen(false);
    if (previewEnabled) { setErrorText(t("common.actionFailed")); return; }
    setApplyPreparing(true);
    const owner = backendBridge.currentProfileOwner();
    try {
      await draftQueueRef.current;
      requireCityOwner(owner);
      if (savedSignatureRef.current !== cityPlacementSignature(placements)) {
        await enqueueDraftSave(placements, layout.layoutRevision);
      }
      requireCityOwner(owner);
      const result = await backendBridge.invokeProfileScoped("city_layout_apply_start", {
        baseRevision: layout.layoutRevision, placements,
      });
      requireCityOwner(owner);
      if (!result?.accepted) {
        setServerValidation({ valid: false, issues: result?.issues || [], totalMoves: 0, temporaryMoves: 0 });
        setErrorText(t("common.actionFailed"));
        return;
      }
      setLiveApplyStatus(result.status);
      setRestoreReview(null);
    } catch (error) {
      setErrorText(cityErrorText(t, error));
    } finally { setApplyPreparing(false); }
  };

  const cancelApply = async () => {
    if (previewEnabled || !applyStatus?.jobId) return;
    try {
      const result = await backendBridge.invokeProfileScoped("city_layout_apply_cancel", { jobId: applyStatus.jobId });
      if (!result?.accepted || !result?.status) throw new Error("Game did not acknowledge this layout cancellation");
      setLiveApplyStatus(result.status);
      setRestoreReview(null);
      setErrorText("");
    } catch (error) { setErrorText(cityErrorText(t, error)); }
  };
  const inspectOriginalRestoration = async () => {
    if (previewEnabled || !online || !applyStatus?.jobId ||
        applyRunning || restoreReviewInFlight.current) return;
    const jobId = applyStatus.jobId;
    const owner = backendBridge.currentProfileOwner();
    restoreReviewInFlight.current = true;
    setRestoreReviewBusy(true);
    setRestoreReview(null);
    setErrorText("");
    try {
      const result = await backendBridge.invokeProfileScoped(
        "city_layout_restore_preflight", { jobId }, 10000);
      requireCityOwner(owner);
      if (result?.jobId !== jobId || result.preflightOnly !== true ||
          result.requestDispatched !== false ||
          result.historicalRequestsReconciled !== false ||
          typeof result.ready !== "boolean" ||
          typeof result.restorationRequired !== "boolean" ||
          typeof result.pendingNativeMove !== "boolean" ||
          !Array.isArray(result.plannedMoves) ||
          result.plannedMoves.some((step) =>
            !/^[1-9]\d{0,19}$/.test(String(step.uuid || "")) ||
            !Number.isInteger(step.pointId) || step.pointId <= 0))
        throw new Error("Original City restoration source returned no trustworthy read-only receipt");
      setRestoreReview(result);
    } catch (error) {
      if (backendBridge.isCurrentProfileOwner(owner))
        setErrorText(cityErrorText(t, error));
    } finally {
      restoreReviewInFlight.current = false;
      if (backendBridge.isCurrentProfileOwner(owner)) setRestoreReviewBusy(false);
    }
  };

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

  const restoreIssue = restoreReview?.issues?.[0] || (restoreReview?.issueCode
    ? { code: restoreReview.issueCode, message: restoreReview.error } : null);
  const restoreIssueText = restoreIssue ? cityErrorText(t, restoreIssue) : "";

  if (!previewEnabled && !online) return <div className="panel city-layout-empty">{t("cityLayout.offline")}</div>;
  if (!layout) return <div className="panel city-layout-empty" role="status">{loading ? t("common.processing") : errorText || t("cityLayout.noData")}{!loading && !previewEnabled && online ? <button type="button" onClick={() => setReloadRevision((revision) => revision + 1)}>{t("common.refresh")}</button> : null}</div>;
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
          <button type="button" disabled={loading || busy} onClick={() => { setErrorText(""); setServerValidation(null); if (!previewEnabled) setReloadRevision((value) => value + 1); }}>{t("common.refresh")}</button>
          <button type="button" disabled={!history.past.length || busy} onClick={undo}>{t("cityLayout.undo")}</button>
          <button type="button" disabled={!history.future.length || busy} onClick={redo}>{t("cityLayout.redo")}</button>
          <button type="button" disabled={!placements.length || busy} onClick={restoreInitial}>{t("cityLayout.restoreInitial")}</button>
          <button type="button" disabled={stale || busy || !layout} onClick={() => void saveDraftNow()}>{t("cityLayout.saveDraft")}</button>
          <button type="button" className="primary" disabled={stale || !placements.length || busy || !layout} onClick={() => void prepareApply()}>{t("cityLayout.apply")}</button>
        </div>
      </header>
      {stale ? <div className="city-layout-warning">{t("cityLayout.stale")} <button type="button" onClick={() => void discardStaleDraft()}>{t("cityLayout.discardDraft")}</button></div> : null}
      {layout.isInCity ? null : <div className="city-layout-warning">{t("cityLayout.enterCity")}</div>}
      {errorText ? <div className="city-layout-error">{errorText}</div> : null}
      {!previewEnabled && localizedNames.context === nameContext && localizedNames.error ? <div className="city-layout-error" role="alert">{t("map.namesUnavailable", { detail: localizedNames.error })} <button type="button" onClick={() => setNameRetry((value) => value + 1)}>{t("map.namesRetry")}</button></div> : null}
      {!previewEnabled && localizedNames.context === nameContext && localizedNames.loading ? <div className="city-layout-name-preview" role="status">{t("map.namesLoading")}</div> : null}
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
                // Each empty grid cell is a child span. Accept that surface
                // for box selection while leaving building buttons untouched.
                if (busy || (event.target !== event.currentTarget &&
                  !event.target.classList?.contains("city-layout-cell"))) return;
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
              {renderedCells.map((cell) => <span key={cell.pointId} className={`city-layout-cell${cell.unlocked ? "" : " locked"}${cell.road ? " road" : ""}${cell.flagOnly ? " flag-only" : ""}${cell.obstructed ? " obstructed" : ""}`} style={{ gridColumn: cell.x - region.bounds.minX + 1, gridRow: cityGridRow(region.bounds, cell.y), backgroundImage: cell.obstructed ? "repeating-linear-gradient(45deg, transparent 0 4px, var(--warning-line) 4px 8px)" : undefined }} />)}
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
              ><GameAssetImage assetPath={building.iconPath} alt="" className="city-layout-grid-icon" loading="eager" deferUntilVisible />{(maxX - minX + 1) * zoom >= 64 && (maxY - minY + 1) * zoom >= 48 ? <strong className="city-layout-building-name">{displayName(building)}</strong> : null}<small className="city-layout-building-level" title={t("cityLayout.level", { level: building.level })} aria-label={t("cityLayout.level", { level: building.level })}>{building.level}</small></button>;
              })}
            </div>
          </div>
          <div className="city-layout-legend"><span><i className="available" />{t("cityLayout.cell.available")}</span><span><i className="road" />{t("cityLayout.cell.road")}</span><span><i className="flag-only" />{t("cityLayout.cell.flagOnly")}</span><span><i className="locked" />{t("cityLayout.cell.locked")}</span></div>
        </div>
        <aside className="city-layout-inspector">
          <strong>{t("cityLayout.properties")}</strong>
          {selected ? <><div className="city-layout-selected"><GameAssetImage assetPath={selected.iconPath} alt={displayName(selected)} className="city-layout-selected-icon" loading="eager" /><span><strong>{displayName(selected)}</strong><small>{t("cityLayout.level", { level: selected.level })}</small></span></div><dl><div><dt>{t("cityLayout.footprint")}</dt><dd>{selected.tileX}×{selected.tileY}</dd></div><div><dt>{t("cityLayout.movable")}</dt><dd>{t(selected.movable ? "common.yes" : "common.no")}</dd></div><div><dt>{t("cityLayout.rule")}</dt><dd>{t(selected.isFlag ? "cityLayout.rule.flag" : "cityLayout.rule.normal")}</dd></div></dl></> : null}
          <div className={`city-layout-validation ${validationInvalid ? "invalid" : "valid"}`} role={validationInvalid ? "alert" : undefined}>{validationInvalid ? validationIssueText : t("cityLayout.validation.valid")}</div>
          {serverValidation?.valid ? <div className="city-layout-validation valid">{t("cityLayout.validation.server", { moves: serverValidation.totalMoves, temporary: serverValidation.temporaryMoves })}</div> : null}
          <strong>{t("cityLayout.changes", { count: placements.length })}</strong>
          <div className="city-layout-changes">{placements.length === 0 ? <span>{t("cityLayout.noChanges")}</span> : placements.map((placement) => { const building = buildingsByUuid.get(placement.uuid); return building ? <div key={placement.uuid}><span>{displayName(building)}</span><small>{t("cityLayout.level", { level: building.level })}</small></div> : null; })}</div>
          {applyRunning ? <div className="city-layout-progress"><span>{t("cityLayout.applyProgress", { current: applyStatus.completedMoves, total: applyStatus.totalMoves })}</span><progress max={applyStatus.totalMoves || 1} value={applyStatus.completedMoves} /><button type="button" onClick={() => void cancelApply()} disabled={previewEnabled || applyStatus.state === "cancelling"}>{t("cityLayout.cancelApply")}</button></div> : null}
          {applyStatus?.jobId && !applyRunning ? <div className="city-layout-progress" data-feature018-restore-review>
            <button type="button" disabled={restoreReviewBusy || previewEnabled || !online}
              onClick={() => void inspectOriginalRestoration()}>
              {chinese ? "只读核对原始内城布局及复原方案" : "Inspect original-layout restoration (read-only)"}
            </button>
            {restoreReview ? <div role="status">
              <small>{restoreReview.ready
                ? restoreReview.originalLayoutConfirmed === true
                  ? chinese ? "原始布局已核实" : "Original layout verified"
                  : chinese ? "仅生成复原预览，未派遣移动" : "Restoration plan only; no move sent"
                : chinese ? "复原方案尚不能安全执行" : "Restoration cannot be safely planned"}</small>
              {restoreIssueText ? <small>{restoreIssueText}</small> : null}
              {restoreReview.restorationRequired && restoreReview.ready ?
                <small>{chinese ? "原生规划步骤" : "Source-valid planned steps"}:
                  {" "}{restoreReview.plannedMoves.length}</small> : null}
              {restoreReview.historicalUncertaintyInPlan === true ?
                <small>{chinese ? "含历史回复不明的建筑，禁止推断可安全重发" :
                  "Historical unresolved building reply: never infer safe resend"}</small> : null}
              <small>{chinese ? "历史发送无唯一确认，本功能不执行任何移动" :
                "Historical requests remain uncorrelated; this control never sends a move"}</small>
            </div> : null}
          </div> : null}
        </aside>
      </div>
      <footer className="city-layout-footer"><span>{t("cityLayout.draftRevision", { revision: draftRevision })}</span><span>{t("cityLayout.conflicts", { count: issues.length })}</span></footer>
      {applyConfirmOpen ? <div className="scheme-modal-backdrop" data-preview-only-confirm={previewEnabled ? "city-layout" : undefined}><section className="scheme-modal" role="dialog" aria-modal="true"><strong>{t("cityLayout.applyConfirm", { count: serverValidation?.totalMoves ?? placements.length })}</strong><div className="scheme-modal-actions"><button type="button" onClick={() => setApplyConfirmOpen(false)}>{t("common.cancel")}</button><button className="primary" type="button" onClick={() => void applyConfirmed()}>{t("cityLayout.apply")}</button></div></section></div> : null}
    </section>
  );
}
