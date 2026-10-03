export const CITY_BASE_CELL_SIZE = 24;

export const HOTKEY_CARDS = Object.freeze([
  { key: "attack", binding: "Q / W / E / R", title: "hotkeys.attack.title", description: "hotkeys.attack.description", warning: null },
  { key: "recall", binding: "A / S / D / F", title: "hotkeys.recall.title", description: "hotkeys.recall.description", warning: null },
  { key: "shieldOverlay", binding: "Space", title: "hotkeys.shield.title", description: "hotkeys.shield.description", warning: null },
  { key: "shieldUse", binding: "F6 / F7 / F8", title: "hotkeys.shieldUse.title", description: "hotkeys.shieldUse.description", warning: "hotkeys.shieldUse.warning" },
  { key: "equipment", binding: "Alt + 1～4", title: "hotkeys.equipment.title", description: "hotkeys.equipment.description", warning: null },
  { key: "randomRelocate", binding: "F9", title: "hotkeys.randomRelocate.title", description: "hotkeys.randomRelocate.description", warning: "hotkeys.relocationWarning" },
  { key: "allianceRelocate", binding: "F10", title: "hotkeys.allianceRelocate.title", description: "hotkeys.allianceRelocate.description", warning: "hotkeys.relocationWarning" },
]);

export const MINI_GAME_HOTKEY_CARDS = Object.freeze([
  { key: "frontlineReinforce", binding: "G", title: "hotkeys.frontlineReinforce.title", description: "hotkeys.frontlineReinforce.description", warning: null },
]);

export function previewHotkeyConfig() {
  return {
    attack: true,
    recall: true,
    shieldOverlay: true,
    shieldUse: false,
    equipment: true,
    randomRelocate: false,
    allianceRelocate: false,
    frontlineReinforce: true,
    treasureChestHint: false,
    attackMarchSpeedupItem: true,
    attackMarchSpeedupDiamond: false,
    previewUnrelatedField: "preserve-me",
  };
}

export function mergeHotkeyField(freshConfig, optimisticConfig, field) {
  return { ...freshConfig, [field]: optimisticConfig[field] };
}

export function sheepStatusKey(status) {
  switch (status?.step) {
    case "solving": return "miniGames.sheep.solving";
    case "executing": return "miniGames.sheep.executing";
    case "completed": return "common.completed";
    case "daily_limit": return "miniGames.sheep.dailyLimit";
    case "all_completed": return "miniGames.sheep.allCompleted";
    case "activity_ended": return "miniGames.sheep.activityEnded";
    case "ui_open": return "miniGames.sheep.uiOpen";
    case "manual_or_state_conflict": return "miniGames.sheep.conflict";
    case "solve_failed": return "miniGames.sheep.solveFailed";
    case "unsupported_client": return "miniGames.sheep.unsupported";
    case "refreshing":
    case "starting":
    case "opening":
    case "initial_delay": return "common.processing";
    default: return status?.running ? "common.processing" : status?.state === "error" ? "common.failed" : "common.stopped";
  }
}

export function formatSheepElapsed(milliseconds) {
  const totalSeconds = Math.max(0, Math.floor(milliseconds / 1000));
  const seconds = totalSeconds % 60;
  const totalMinutes = Math.floor(totalSeconds / 60);
  const minutes = totalMinutes % 60;
  const hours = Math.floor(totalMinutes / 60);
  return hours > 0
    ? `${hours}:${String(minutes).padStart(2, "0")}:${String(seconds).padStart(2, "0")}`
    : `${totalMinutes}:${String(seconds).padStart(2, "0")}`;
}

export function previewSheepStatus(previewState, now = Date.now()) {
  const base = { running: false, state: "idle", step: "idle", currentLevel: 4 };
  switch (previewState) {
    case "mini-games-active": return { ...base, running: true, state: "running", step: "executing", startedAt: now - 31_000, plannedMoves: 42, confirmedMoves: 18 };
    case "mini-games-solving": return { ...base, running: true, state: "running", step: "solving", startedAt: now - 9_000 };
    case "mini-games-executing": return { ...base, running: true, state: "running", step: "executing", startedAt: now - 31_000 };
    case "mini-games-completed": return { ...base, step: "completed", durationMs: 3_661_000 };
    case "mini-games-complete": return { ...base, step: "daily_limit" };
    case "mini-games-all-complete": return { ...base, step: "all_completed" };
    case "mini-games-activity-ended": return { ...base, step: "activity_ended" };
    case "mini-games-ui-open": return { ...base, step: "ui_open" };
    case "mini-games-conflict": return { ...base, step: "manual_or_state_conflict" };
    case "mini-games-solve-failed": return { ...base, state: "error", step: "solve_failed" };
    case "mini-games-unsupported": return { ...base, state: "error", step: "unsupported_client" };
    case "mini-games-refreshing": return { ...base, running: true, state: "running", step: "refreshing", startedAt: now - 4_000 };
    case "mini-games-starting": return { ...base, running: true, state: "running", step: "starting", startedAt: now - 2_000 };
    case "mini-games-opening": return { ...base, running: true, state: "running", step: "opening", startedAt: now - 1_000 };
    case "mini-games-initial-delay": return { ...base, running: true, state: "running", step: "initial_delay", startedAt: now - 6_000 };
    case "mini-games-state-error": return { ...base, state: "error", step: "idle" };
    default: return base;
  }
}

export function formatDiagnosticBytes(bytes) {
  return bytes < 1024
    ? `${bytes} B`
    : bytes < 1024 * 1024
      ? `${(bytes / 1024).toFixed(1)} KiB`
      : `${(bytes / (1024 * 1024)).toFixed(1)} MiB`;
}

export function updateManualCooldownSeconds(status, now = Date.now()) {
  return status.nextManualCheckAt
    ? Math.max(0, Math.ceil((Date.parse(status.nextManualCheckAt) - now) / 1000))
    : 0;
}

export function updateStatusBusy(status) {
  return status.phase === "checking" || status.phase === "downloading" || status.phase === "opening";
}

export function updateDownloadVisible(status) {
  return status.phase === "available"
    || (status.phase === "error" && status.latestVersion !== null && status.latestVersion !== status.currentVersion);
}

export function previewUpdateStatus(previewState, now = Date.now()) {
  const base = {
    phase: previewState.startsWith("settings-") ? "upToDate" : "idle",
    currentVersion: "0.3.17",
    latestVersion: "0.3.17",
    releaseNotes: "",
    publishedAt: null,
    progress: null,
    message: null,
    nextManualCheckAt: null,
    downloadDirectory: "Preview/updates",
  };
  const available = {
    ...base,
    latestVersion: "0.3.18",
    releaseNotes: "Preview release notes for 0.3.18.",
    publishedAt: "2026-10-03T09:00:00.000Z",
  };
  switch (previewState) {
    case "settings-update-idle": return { ...base, phase: "idle", latestVersion: null };
    case "settings-update-checking": return { ...base, phase: "checking" };
    case "settings-update-available": return { ...available, phase: "available" };
    case "settings-update-downloading": return { ...available, phase: "downloading", progress: 42 };
    case "settings-update-opening": return { ...available, phase: "opening", progress: 100 };
    case "settings-update-error": return { ...available, phase: "error", message: "UPDATE_STATUS_FAILED" };
    case "settings-update-check-error": return { ...base, phase: "error", message: "UPDATE_CHECK_FAILED" };
    case "settings-update-download-error": return { ...available, phase: "error", message: "UPDATE_DOWNLOAD_FAILED" };
    case "settings-update-cooldown": return { ...base, nextManualCheckAt: new Date(now + 30_000).toISOString() };
    default: return base;
  }
}

export function cityPlacementMap(placements) {
  return new Map(placements.map((placement) => [placement.uuid, placement.targetPointId]));
}

export function citySetPlacement(placements, building, targetPointId) {
  const next = placements.filter((placement) => placement.uuid !== building.uuid);
  if (targetPointId !== building.pointId) next.push({ uuid: building.uuid, targetPointId });
  return next.sort((left, right) => left.uuid.localeCompare(right.uuid));
}

export function cityMovedOccupiedPoints(building, targetPointId, cellsByPoint, cellsByCoordinate) {
  const target = cellsByPoint.get(targetPointId);
  if (!target) return [];
  const occupied = [];
  for (const pointId of building.occupiedPoints) {
    const source = cellsByPoint.get(pointId);
    if (!source) return [];
    const cell = cellsByCoordinate.get(`${target.x + source.x - building.x}:${target.y + source.y - building.y}`);
    if (!cell) return [];
    occupied.push(cell.pointId);
  }
  return occupied.sort((left, right) => left - right);
}

export function cityLayoutIssues(layout, placements) {
  const cellsByPoint = new Map(layout.cells.map((cell) => [cell.pointId, cell]));
  const cellsByCoordinate = new Map(layout.cells.map((cell) => [`${cell.x}:${cell.y}`, cell]));
  const placementMap = cityPlacementMap(placements);
  const occupiedByUuid = new Map();
  const issues = [];
  for (const building of layout.buildings) {
    const moved = placementMap.has(building.uuid);
    const targetPointId = placementMap.get(building.uuid) ?? building.pointId;
    if (moved && !building.movable) {
      issues.push({ uuid: building.uuid, code: "CITY_LAYOUT_IMMOVABLE", pointId: targetPointId });
      continue;
    }
    const occupiedPoints = moved
      ? cityMovedOccupiedPoints(building, targetPointId, cellsByPoint, cellsByCoordinate)
      : building.occupiedPoints;
    if (moved && occupiedPoints.length !== building.occupiedPoints.length) {
      issues.push({ uuid: building.uuid, code: "CITY_LAYOUT_OUTSIDE", pointId: targetPointId });
      continue;
    }
    for (const pointId of occupiedPoints) {
      if (moved) {
        const cell = cellsByPoint.get(pointId);
        const code = cell.unlocked
          ? cell.road
            ? "CITY_LAYOUT_ROAD"
            : cell.flagOnly && !building.isFlag
              ? "CITY_LAYOUT_FLAG_ONLY"
              : ""
          : "CITY_LAYOUT_LOCKED";
        if (code) issues.push({ uuid: building.uuid, code, pointId });
      }
      const previous = occupiedByUuid.get(pointId);
      if (previous && previous !== building.uuid) issues.push({ uuid: building.uuid, code: "CITY_LAYOUT_OVERLAP", pointId });
      else occupiedByUuid.set(pointId, building.uuid);
    }
  }
  return issues;
}

export function cityRegionForPoint(layout, pointId) {
  return layout.cells.find((cell) => cell.pointId === pointId)?.regionId ?? layout.regions[0]?.id ?? "";
}

export function cityGridRow(bounds, y) {
  return bounds.maxY - y + 1;
}

export function cityPlacementSignature(placements) {
  return placements.map((placement) => `${placement.uuid}:${placement.targetPointId}`).join("|");
}

function previewCellPointId(x, y) {
  return y * 100 + x;
}

function previewOccupiedPoints(x, y, tileX, tileY) {
  const points = [];
  for (let offsetY = 0; offsetY < tileY; offsetY += 1) {
    for (let offsetX = 0; offsetX < tileX; offsetX += 1) points.push(previewCellPointId(x + offsetX, y - offsetY));
  }
  return points;
}

export function previewCityLayoutFixture(previewState = "city-layout-populated") {
  const regionId = "preview-main";
  const cells = [];
  for (let y = 1; y <= 8; y += 1) {
    for (let x = 1; x <= 8; x += 1) {
      cells.push({
        pointId: previewCellPointId(x, y),
        x,
        y,
        regionId,
        unlocked: y !== 1,
        road: x === 1 && y !== 1,
        flagOnly: y === 4 && x >= 7,
      });
    }
  }
  const buildings = [
    { uuid: "preview-hq", name: "HQ", nameKey: "preview.city.hq", level: 30, x: 2, y: 7, pointId: previewCellPointId(2, 7), occupiedPoints: previewOccupiedPoints(2, 7, 2, 2), tileX: 2, tileY: 2, movable: false, isFlag: false },
    { uuid: "preview-barracks", name: "Barracks", nameKey: "preview.city.barracks", level: 28, x: 5, y: 7, pointId: previewCellPointId(5, 7), occupiedPoints: previewOccupiedPoints(5, 7, 2, 2), tileX: 2, tileY: 2, movable: true, isFlag: false },
    { uuid: "preview-hospital", name: "Hospital", nameKey: "preview.city.hospital", level: 27, x: 3, y: 4, pointId: previewCellPointId(3, 4), occupiedPoints: previewOccupiedPoints(3, 4, 2, 1), tileX: 2, tileY: 1, movable: true, isFlag: false },
    { uuid: "preview-flag", name: "Alliance Flag", nameKey: "preview.city.flag", level: 1, x: 7, y: 4, pointId: previewCellPointId(7, 4), occupiedPoints: previewOccupiedPoints(7, 4, 1, 1), tileX: 1, tileY: 1, movable: true, isFlag: true },
  ];
  let placements = [];
  if (previewState === "city-layout-populated-conflict") {
    placements = [{ uuid: "preview-hospital", targetPointId: previewCellPointId(5, 7) }];
  } else if (previewState === "city-layout-populated-moved" || previewState === "city-layout-stale" || previewState === "city-layout-server-valid" || previewState === "city-layout-applying") {
    placements = [{ uuid: "preview-hospital", targetPointId: previewCellPointId(5, 4) }];
  }
  return {
    layoutRevision: "preview-layout-r1",
    isInCity: previewState !== "city-layout-outside-city",
    regions: [{ id: regionId, pointIds: cells.map((cell) => cell.pointId), bounds: { minX: 1, maxX: 8, minY: 1, maxY: 8 } }],
    cells,
    buildings,
    placements,
    draftRevision: placements.length ? 7 : 6,
  };
}
