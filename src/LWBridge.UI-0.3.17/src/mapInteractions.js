// Retained Map filter and action-error presentation. Scheduling/sharing were removed by MAP-016 owner direction.
// Exact source: archive/research-offline-controller:evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/MapDataPanel-B4GXEND2.js
// (SHA-256 CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089); binding names below are
// expanded for readability. Locators and the original-component differential live under
// archive/research-offline-controller:evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/.
//
// This module only formats retained filters and errors; no provider actions are dispatched.

export const NAME_FILTER_KINDS = Object.freeze(["resource", "monster"]);

export function isNameFilterKind(kind) {
  return kind === "resource" || kind === "monster";
}

const INTERNAL_ERROR_CODE = /\b[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)+\b/g;

function errorParts(value) {
  const codes = [];
  if (value && typeof value === "object") {
    for (const key of ["code", "errorCode", "issueCode"]) {
      if (typeof value[key] === "string" && value[key]) codes.push(value[key]);
    }
  }
  const message = value && typeof value === "object"
    ? (typeof value.message === "string" ? value.message : "")
    : String(value ?? "");
  codes.push(...message.match(INTERNAL_ERROR_CODE) || []);
  return { codes: [...new Set(codes)], message };
}

function escapeRegex(value) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

// Internal bridge/native codes are useful for choosing a localized message but
// should never be presented as user-facing prose. Preserve any accompanying
// human detail and fall back to the caller's localized action failure when a
// response contains only an implementation code.
export function plainErrorDetail(value) {
  const { codes, message } = errorParts(value);
  let text = message.trim();
  for (const code of codes) text = text.replace(new RegExp(`\\b${escapeRegex(code)}\\b`, "g"), " ");
  return text
    .replace(/\(\s*\)|\[\s*\]|\{\s*\}/g, " ")
    .replace(/\s+([,.;!?])/g, "$1")
    .replace(/^[\s:;,.()[\]{}\-–—]+/, "")
    .replace(/[\s:;,.()[\]{}\-–—]+$/, "")
    .replace(/\s{2,}/g, " ")
    .trim();
}

// Preserve catalog translations, explain Auto failure codes, then retain only
// the plain native detail after removing implementation codes.
export function translateActionError(t, value, fallbackKey = "common.actionFailed") {
  const { codes } = errorParts(value);
  const detail = plainErrorDetail(value);
  for (const code of [...codes].reverse()) {
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
    return detail ? `${text} (${detail})` : text;
  }
  // Native plain-language errors must remain visible, including retained errors
  // loaded on the Auto tab. A missing catalog entry must never hide their cause.
  return detail || t(fallbackKey);
}
