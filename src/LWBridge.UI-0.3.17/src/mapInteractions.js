// Retained Map filter and action-error presentation. Scheduling/sharing were removed by MAP-016 owner direction.
// Exact source: evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/MapDataPanel-B4GXEND2.js
// (SHA-256 CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089); binding names below are
// expanded for readability. Locators and the original-component differential live under
// evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/.
//
// This module only formats retained filters and errors; no provider actions are dispatched.

export const NAME_FILTER_KINDS = Object.freeze(["resource", "monster"]);

export function isNameFilterKind(kind) {
  return kind === "resource" || kind === "monster";
}

// Preserve catalog translations, explain Auto failure codes, then retain native error details.
export function translateActionError(t, value, fallbackKey = "common.actionFailed") {
  const codes = [];
  if (value && typeof value === "object" && "code" in value && typeof value.code === "string") codes.push(value.code);
  const message = value && typeof value.message === "string" ? value.message : String(value ?? "");
  codes.push(...message.match(/\b[A-Z][A-Z0-9_]{2,}\b/g) || []);
  for (const code of [...new Set(codes)].reverse()) {
    for (const namespace of ["error", "auth.error", "update.error"]) {
      const key = `${namespace}.${code}`;
      const translated = t(key);
      if (translated !== key) return translated;
    }
  }
  const autoErrors = {
    MAP_AUTO_SCAN_NOT_PUBLISHED: "map.autoScanNotPublished",
    MAP_AUTO_SCAN_SERVER_UNAVAILABLE: "map.autoScanServerUnavailable",
    MAP_AUTO_SCAN_TIMEOUT: "map.autoScanTimedOut",
    MAP_AUTO_SCAN_OWNER_MISMATCH: "map.autoScanOwnerChanged",
    MAP_AUTO_SCAN_TARGET_OWNER_MISMATCH: "map.autoScanOwnerChanged",
    MAP_AUTO_SCAN_RETURN_OWNER_MISMATCH: "map.autoScanOwnerChanged",
    MAP_AUTO_SCAN_RETURN_FAILED: "map.autoScanReturnFailed",
    MAP_AUTO_SCAN_STOP_FAILED: "map.autoScanStopFailed",
  };
  for (const code of Object.keys(autoErrors)) {
    if (!codes.includes(code)) continue;
    const text = t(autoErrors[code]);
    return `${text} (${message.trim()})`;
  }
  // Native plain-language errors must remain visible, including retained errors
  // loaded on the Auto tab. A missing catalog entry must never hide their cause.
  return message.trim() || t(fallbackKey);
}
