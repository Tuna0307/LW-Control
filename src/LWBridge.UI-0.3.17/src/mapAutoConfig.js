import { MAP_KIND_KEYS } from "./mapBackend.js";

export const AUTO_SCAN_DEFAULT_TYPES = Object.freeze([
  "truck",
  "railway",
  "dispatch",
  "ghost",
  "treasure",
]);

export const DEFAULT_AUTO_SCAN_CONFIG = Object.freeze({
  enabled: false,
  intervalMinutes: 60,
  serverIds: Object.freeze([]),
  selectedTypes: AUTO_SCAN_DEFAULT_TYPES,
  scanMode: "fast",
  returnToOriginalServer: true,
  nextRunAt: 0,
});

export function freshAutoScanDefaults() {
  return {
    ...DEFAULT_AUTO_SCAN_CONFIG,
    serverIds: [],
    selectedTypes: [...AUTO_SCAN_DEFAULT_TYPES],
  };
}

export function parseAutoServerIds(value) {
  const ids = [];
  for (const token of String(value).split(/[\s,，;；]+/)) {
    const id = Number(token);
    if (Number.isInteger(id) && id >= 1 && id <= 99999 && !ids.includes(id)) ids.push(id);
  }
  return ids.slice(0, 20);
}

export function appendAutoServerIds(serverIds, draft) {
  return parseAutoServerIds(`${serverIds.join(",")},${draft}`);
}

export function removeAutoServerId(serverIds, id) {
  return serverIds.filter((value) => value !== id);
}

export function normalizeAutoScanConfig(value) {
  const intervalMinutes = Math.min(
    1440,
    Math.max(20, Math.trunc(Number(value?.intervalMinutes ?? DEFAULT_AUTO_SCAN_CONFIG.intervalMinutes))),
  );
  const serverIds = parseAutoServerIds((value?.serverIds || []).join(","));
  const selectedTypes = [...new Set((value?.selectedTypes || []).filter((kind) => MAP_KIND_KEYS.includes(kind)))];
  return {
    enabled: value?.enabled === true,
    intervalMinutes,
    serverIds,
    selectedTypes: selectedTypes.length > 0 ? selectedTypes : [...AUTO_SCAN_DEFAULT_TYPES],
    scanMode: value?.scanMode === "normal" ? "normal" : "fast",
    returnToOriginalServer: value?.returnToOriginalServer !== false,
    nextRunAt: Math.max(0, Math.trunc(Number(value?.nextRunAt) || 0)),
  };
}

export function autoScanStorageKey(profileId) {
  return `lwbridge.mapAutoScan.${profileId}`;
}

export function loadAutoScanConfig(profileId, storage = window.localStorage) {
  try {
    return normalizeAutoScanConfig(JSON.parse(storage.getItem(autoScanStorageKey(profileId)) || "null"));
  } catch {
    return freshAutoScanDefaults();
  }
}

export function saveAutoScanConfig(profileId, config, storage = window.localStorage) {
  const normalized = normalizeAutoScanConfig(config);
  storage.setItem(autoScanStorageKey(profileId), JSON.stringify(normalized));
  return normalized;
}

export function applyAutoScanConfigEdit(previous, candidate, now = Date.now()) {
  const next = normalizeAutoScanConfig(candidate);
  if (next.enabled && !previous.enabled) next.nextRunAt = now;
  if (!next.enabled) next.nextRunAt = 0;
  return next;
}

export function autoScanTargetServers(serverIds, currentServerId) {
  return serverIds.length > 0
    ? serverIds
    : Number.isInteger(currentServerId) && currentServerId > 0
      ? [currentServerId]
      : [];
}

export function advanceAutoScanDeadline(config, now = Date.now()) {
  const normalized = normalizeAutoScanConfig(config);
  return { ...normalized, nextRunAt: now + normalized.intervalMinutes * 60_000 };
}

export function autoScanShouldRun(config, now, online, autoScanRunning, isReading) {
  return config.enabled && online && !autoScanRunning && !isReading && now >= config.nextRunAt;
}

export function formatAutoScanDate(value, locale) {
  const parsed = Number(value);
  if (!Number.isFinite(parsed) || parsed <= 0) return "-";
  return new Date(parsed < 1_000_000_000_000 ? parsed * 1000 : parsed).toLocaleString(locale);
}
