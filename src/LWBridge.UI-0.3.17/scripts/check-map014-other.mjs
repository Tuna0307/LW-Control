// Inert MAP-014 UI inverses. Does not launch a browser or game or build assets.
import assert from "node:assert/strict";
import {
  MAP_KIND_KEYS, MAP_DEFAULT_SCAN_TYPES, DEFAULT_SCAN_STATE,
  normalizeScanState, normalizeSelectedTypes, normalizeCounts, buildStartPayload,
} from "../src/mapBackend.js";
import { AUTO_SCAN_DEFAULT_TYPES, normalizeAutoScanConfig } from "../src/mapAutoConfig.js";
import {
  buildMapColumns, validMapJumpCoordinates, otherTypeName, otherName, otherWorkEnd, otherExpiry,
} from "../src/mapTablePresentation.js";
import { withMapOtherTexts } from "../src/locales/map014-other.js";

assert.equal(MAP_KIND_KEYS.length, 10); // eight recovered kinds + Other + Zombie Boss
assert.equal(MAP_DEFAULT_SCAN_TYPES.length, 8);
assert.equal(MAP_DEFAULT_SCAN_TYPES.includes("other"), false);
assert.deepEqual(DEFAULT_SCAN_STATE.selectedTypes, MAP_DEFAULT_SCAN_TYPES);
assert.deepEqual(normalizeScanState({}).selectedTypes, MAP_DEFAULT_SCAN_TYPES);
assert.deepEqual(normalizeScanState({ selectedTypes: [] }).selectedTypes, MAP_DEFAULT_SCAN_TYPES);
assert.deepEqual(normalizeSelectedTypes([]), MAP_DEFAULT_SCAN_TYPES);
assert.deepEqual(buildStartPayload(undefined, "normal").selectedTypes, MAP_DEFAULT_SCAN_TYPES);
assert.deepEqual(normalizeSelectedTypes(["other", "other"]), ["other"]);
assert.deepEqual(normalizeScanState({ selectedTypes: ["other"] }).selectedTypes, ["other"]);
assert.equal(normalizeCounts({ other: 3 }).other, 3);
assert.equal(normalizeAutoScanConfig(null).selectedTypes.includes("other"), false);
assert.deepEqual(normalizeAutoScanConfig({ selectedTypes: ["other", "other"] }).selectedTypes, ["other"]);
assert.deepEqual(AUTO_SCAN_DEFAULT_TYPES, ["truck", "railway", "dispatch", "ghost", "treasure"]);

assert.equal(validMapJumpCoordinates({ x: 0, y: 999 }), true);
assert.equal(validMapJumpCoordinates({ x: 999, y: 0 }), true);
assert.equal(validMapJumpCoordinates({ x: "0", y: "0" }), true);
for (const row of [
  { x: -1, y: 4 }, { x: 1000, y: 4 }, { x: 5, y: 1000 },
  { x: 5.5, y: 4 }, { x: "NaN", y: 3 }, { x: "", y: 5 },
  { y: 5 }, { x: 5 }, { x: null, y: null }, { x: Infinity, y: 0 },
]) assert.equal(validMapJumpCoordinates(row), false);

const en = withMapOtherTexts("en", {});
const zh = withMapOtherTexts("zh-CN", {});
const t = (key, values = {}) =>
  (en[key] || key).replace(/\{(\w+)\}/g, (_, name) => String(values[name] ?? ""));
assert.equal(otherTypeName(t, "supplies:1"), "Scattered Supplies");
assert.equal(otherTypeName(t, "event:27000"), "Celebration Party");
assert.equal(otherTypeName(t, "supplies:3"), "Supplies (type 3; name unavailable)");
assert.equal(otherTypeName(t, "supplies:9"), "Supplies (type 9; name unavailable)");
assert.equal(otherName(t, { season_s2_ice_supplies_11: "Real Game Name" },
  { otherSubtype: "supplies:1", otherNameKey: "season_s2_ice_supplies_11" }), "Real Game Name");
assert.equal(otherName(t, {}, { otherSubtype: "supplies:4" }),
  "Supplies (type 4; name unavailable)");
assert.ok(zh["map.otherSubtypeUnidentifiedSupplies"].includes("{type}"));

const workEnd = { otherSubtype: "supplies:1", workEndTime: 1800000000, expireTime: 1900000000 };
const party = { otherSubtype: "event:27000", expireTime: 1900000000 };
assert.equal(otherWorkEnd(workEnd, "en"), new Date(1800000000 * 1000).toLocaleString("en"));
assert.equal(otherExpiry(workEnd, "en"), "-");
assert.equal(otherWorkEnd(party, "en"), "-");
assert.equal(otherExpiry(party, "en"), new Date(1900000000 * 1000).toLocaleString("en"));
assert.equal(otherWorkEnd({ otherSubtype: "supplies:9" }, "en"), "-");
assert.equal(otherExpiry({ otherSubtype: "event:27000" }, "en"), "-");
const columns = buildMapColumns("other", t, "en", {}, null, false);
assert.ok(columns.some((column) => column.label === "Work ends"));
assert.ok(columns.some((column) => column.label === "map.expireTime"));

console.log("MAP014_OTHER_FRONTEND_CHECKS_OK");
