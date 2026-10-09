// Explicit browser-preview presentation fixtures. No scan is started or completed.
export function scanHeaderFixture(previewState, now) {
  if (!["map-scan-reading", "map-scan-completed", "map-scan-publishing", "map-scan-stopped"].includes(previewState)) return null;
  const scanRunId = "fixture-header-run";
  const startedAt = now - 65_000;
  if (previewState === "map-scan-reading") return {
    scanState: { scanRunId, startedAt, isReading: true, phase: "reading", progressPercent: 37.75 },
    scanProgress: null,
  };
  if (previewState === "map-scan-publishing") return {
    scanState: { scanRunId, startedAt, updatedAt: now, isReading: false, phase: "publishing", progressPercent: 99.75 },
    scanProgress: null,
  };
  if (previewState === "map-scan-stopped") return {
    scanState: { scanRunId, startedAt, updatedAt: now, isReading: false, phase: "stopped", progressPercent: 49.5 },
    scanProgress: null,
  };
  return {
    scanState: { scanRunId, isReading: false, phase: "completed", progressPercent: 100, startedAt: 0 },
    scanProgress: { id: scanRunId, serverId: 321, status: "completed", createdAt: startedAt, updatedAt: now, error: null },
  };
}
