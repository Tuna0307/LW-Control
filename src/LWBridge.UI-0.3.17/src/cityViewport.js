// Rendering only. All movement/selection validation still uses the complete
// native cell map, including cells outside the viewport.
export function visibleCityCells(cells, region, viewport, zoom) {
  const source = cells.filter((cell) => cell.regionId === region.id);
  if (!viewport?.width || !viewport?.height) return source;
  const margin = zoom * 3;
  return source.filter((cell) => {
    const x = 14 + (cell.x - region.bounds.minX) * zoom;
    const y = 14 + (region.bounds.maxY - cell.y) * zoom;
    return x + zoom >= viewport.left - margin && x <= viewport.left + viewport.width + margin &&
      y + zoom >= viewport.top - margin && y <= viewport.top + viewport.height + margin;
  });
}

export function cityBuildingScrollCenter(buildings, cellsByPoint, bounds, zoom, width, height, placements = new Map()) {
  const cells = buildings.map((building) => cellsByPoint.get(placements.get(building.uuid) ?? building.pointId)).filter(Boolean);
  if (!cells.length) return null;
  const x = (Math.min(...cells.map((cell) => cell.x)) + Math.max(...cells.map((cell) => cell.x))) / 2;
  const y = (Math.min(...cells.map((cell) => cell.y)) + Math.max(...cells.map((cell) => cell.y))) / 2;
  return { left: Math.max(0, 14 + (x - bounds.minX) * zoom - width / 2),
    top: Math.max(0, 14 + (bounds.maxY - y) * zoom - height / 2) };
}
