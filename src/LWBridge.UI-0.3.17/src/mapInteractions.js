// Recovered Map interaction rules: selection ownership, random delay and action predicates.
// Exact source: evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/MapDataPanel-B4GXEND2.js
// (SHA-256 CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089); binding names below are
// expanded for readability. Locators and the original-component differential live under
// evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/.
//
// Nothing here talks to a provider. Original UI predicates are kept separate from the canonical runtime
// availability fence (a provider that does not implement scheduling/sharing leaves those buttons disabled).

export const NAME_FILTER_KINDS = Object.freeze(["resource", "monster"]);

export function isNameFilterKind(kind) {
  return kind === "resource" || kind === "monster";
}

// Original Dispatch/Ghost callback `or` and Truck callback `sr` store `${serverId}:${uuid || ""}` -> row.
export function selectionStorageKey(row) {
  return `${row.serverId}:${row.uuid || ""}`;
}

// Original table checkbox `me` looks membership up with a TRIMMED uuid (checked: keys.has(`${serverId}:${uuid.trim()}`)),
// so a uuid with surrounding whitespace is stored under one key and looked up under another.
export function selectionMembershipKey(row) {
  return `${row.serverId}:${String(row.uuid || "").trim()}`;
}

export function toggleSelection(selection, row) {
  const key = selectionStorageKey(row);
  if (selection[key]) {
    const next = { ...selection };
    delete next[key];
    return next;
  }
  return { ...selection, [key]: row };
}

export function selectionCount(selection) {
  return Object.keys(selection).length;
}

export function selectionKeySet(selection) {
  return new Set(Object.keys(selection));
}

// Original `or` receives the table row with `taskKind` forced to ghost/dispatch from the active table kind.
export function dispatchSelectionPayload(kind, row) {
  return { ...row, taskKind: kind === "ghost" ? "ghost" : "dispatch" };
}

// Original share success: keep only entries whose `String(row.uuid || "")` is not in `sharedUuids`.
export function removeSharedSelection(selection, sharedUuids) {
  const shared = new Set(sharedUuids);
  return Object.fromEntries(Object.entries(selection).filter(([, row]) => !shared.has(String(row.uuid || ""))));
}

// Original: `Gn = Number(delayText); Kn = Number.isSafeInteger(Gn) && Gn >= 0`.
export const DEFAULT_RANDOM_DELAY_TEXT = "0";
export function parseRandomDelay(text) {
  const seconds = Number(text);
  return { seconds, valid: Number.isSafeInteger(seconds) && seconds >= 0 };
}

// Original Dispatch/Ghost "Schedule selected" button:
// disabled = count === 0 || busyKey === "schedule" || sharing || !delayValid. Its label never changes.
export function scheduleSelectedDisabled({ count, busyKey, sharing, delayValid }) {
  return count === 0 || busyKey === "schedule" || sharing || !delayValid;
}

// Original Dispatch "Share to alliance" button:
// disabled = !online || isReading || count === 0 || busyKey === "schedule" || sharing; label flips while sharing.
export function shareAllianceDisabled({ online, isReading, count, busyKey, sharing }) {
  return !online || isReading || count === 0 || busyKey === "schedule" || sharing;
}
export function shareAllianceLabelKey(sharing) {
  return sharing ? "map.sharingAlliance" : "map.shareAlliance";
}

// Original Truck "Schedule selected trucks" button: disabled = count === 0 || busyKey === "schedule-truck".
export function scheduleTrucksDisabled({ count, busyKey }) {
  return count === 0 || busyKey === "schedule-truck";
}

// Canonical runtime availability: a provider has to implement the operation before its button can enable.
export const SCHEDULING_PROVIDER_METHODS = Object.freeze({
  dispatch: ["scheduleDispatchPlunder"],
  truck: ["scheduleTruckPlunder"],
  share: ["shareDispatchToAlliance"],
  jobActions: ["cancelDispatchPlunder", "cancelTruckPlunder", "clearDispatchPlunderHistory", "clearTruckPlunderHistory", "scheduleTruckPlunder"],
});

export function providerSupports(mapApi, methods) {
  return methods.every((name) => typeof mapApi?.[name] === "function");
}
