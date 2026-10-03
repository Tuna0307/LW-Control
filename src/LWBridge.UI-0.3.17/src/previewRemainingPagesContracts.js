export const CITY_BASE_CELL_SIZE = 24;

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
