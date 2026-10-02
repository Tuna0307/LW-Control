// Exact 0.3.17 MapDataPanel helpers and qn/Z/Yn/Xn/Zn presentation expressions.
// Timestamps are displayed locally; elapsed arithmetic preserves source units.
export function scanTimestamp(value) {
  const number = Number(value);
  return !Number.isFinite(number) || number <= 0 ? 0 : number < 1_000_000_000_000 ? number * 1000 : number;
}

export function scanDateTitle(value, language) {
  const number = Number(value);
  if (!Number.isFinite(number) || number <= 0) return "-";
  return new Date(number < 1_000_000_000_000 ? number * 1000 : number).toLocaleString(language);
}

export function scanDuration(value) {
  if (!Number.isFinite(value) || value < 0) return "-";
  const seconds = Math.floor(value / 1000);
  return [Math.floor(seconds / 3600), Math.floor(seconds % 3600 / 60), seconds % 60]
    .map((part) => String(part).padStart(2, "0")).join(":");
}

export function scanTimeText(value, showDate = true) {
  const timestamp = scanTimestamp(value);
  if (!timestamp) return "-";
  const date = new Date(timestamp);
  const pad = (part) => String(part).padStart(2, "0");
  const time = `${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`;
  return showDate ? `${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${time}` : time;
}

export function scanSameDay(start, end) {
  const left = scanTimestamp(start), right = scanTimestamp(end);
  if (!left || !right) return false;
  const a = new Date(left), b = new Date(right);
  return a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate();
}

export function scanIsoTime(value) {
  const timestamp = scanTimestamp(value);
  return timestamp ? new Date(timestamp).toISOString() : undefined;
}

export function mapScanPresentation(scanState, stored, browseServerId, now) {
  const storedCurrent = !scanState.isReading && stored != null && stored.serverId === browseServerId
    && stored.id === scanState.scanRunId && stored.status !== "running";
  const start = storedCurrent ? stored.createdAt : scanState.startedAt || 0;
  const end = scanState.isReading ? 0 : storedCurrent ? stored.updatedAt : scanState.updatedAt || 0;
  return {
    serverId: storedCurrent ? stored.serverId : scanState.serverId,
    start,
    end,
    duration: start > 0 ? Math.max(0, (scanState.isReading ? now : end || start) - start) : -1,
    progress: Math.max(0, Math.min(100, Number(scanState.progressPercent) || 0)),
    statusKey: scanState.phase === "publishing" ? "common.processing" : scanState.isReading ? "map.reading"
      : storedCurrent && stored.status === "completed" ? "common.completed" : "common.stopped",
  };
}
