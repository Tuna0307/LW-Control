// Shared semantic tables for the ORIGINAL LWBridge 0.3.17 Map panel (function R, exported MapDataPanel).
// Names below are the ORIGINAL minified identifiers (the re-printed reference file appends "2" to the
// identifiers that collide with module-level names, e.g. original `L` is `L2` in MapDataPanel.pretty.js).
//
// STATE_SEMANTICS: position in the sequence of `(0,y.useState)` declarators of R -> original names.
// recover-contract.mjs asserts every row against the AST of the asset, so this table cannot drift.

export const STATE_SEMANTICS = [
  // [semantic name, original value identifier, original setter identifier, initializer source (asserted)]
  ["selectedScanTypes", "Ke", "qe", "()=>rt(C.selectedTypes)"],
  ["scanMode", "F", "Qe", "()=>{let e=localStorage.getItem(Me);return e===`normal`||e===`fast`?e:C.scanMode||`normal`}"],
  ["uncontrolledTab", "$e", "I", "`city`"],
  ["keyword", "et", "nt", "``"],
  ["dataServerId", "R", "ut", "()=>C.serverId"],
  ["alliances", "dt", "ft", "[]"],
  ["nameOptions", "pt", "mt", "{resource:[],monster:[]}"],
  ["dispatchLevels", "ht", "gt", "[]"],
  ["counts", "_t", "vt", "()=>Ee||{}"],
  ["countsLoaded", "yt", "bt", "Ee!==null"],
  ["rewardItems", "xt", "St", "{truck:[],railway:[]}"],
  ["treasureTypes", "Ct", "wt", "[]"],
  ["noAllianceCount", "Tt", "Et", "0"],
  ["scanProgress", "z", "Dt", "null"],
  ["allianceFilter", "Ot", "kt", "`all`"],
  ["nameSelection", "At", "jt", "{}"],
  ["markedOnly", "Mt", "Nt", "!1"],
  ["playerMarkRevision", "Pt", "Ft", "0"],
  ["qualityFilter", "It", "Lt", "{}"],
  ["itemFilter", "Rt", "zt", "{}"],
  ["treasureTypeKey", "Bt", "Vt", "``"],
  ["completionStatus", "Ht", "Ut", "{}"],
  ["plunderableOnly", "Wt", "Gt", "{}"],
  ["dispatchLevel", "Kt", "qt", "``"],
  ["sorts", "Jt", "Yt", "Be"],
  ["page", "B", "V", "1"],
  ["rows", "H", "U", "[]"],
  ["loading", "Xt", "Zt", "()=>C.serverId>0"],
  ["gameTexts", "Qt", "$t", "{}"],
  ["total", "en", "W", "0"],
  ["jumpingKey", "tn", "nn", "``"],
  ["currentTime", "rn", "an", "Date.now()"],
  ["dispatchSelection", "G", "on", "{}"],
  ["truckSelection", "sn", "cn", "{}"],
  ["scheduledDispatchJobs", "ln", "un", "[]"],
  ["scheduledTruckJobs", "dn", "fn", "[]"],
  ["busyKey", "pn", "K", "``"],
  ["randomDelay", "mn", "hn", "`0`"],
  ["rowsRevision", "gn", "_n", "0"],
  ["optionsRevision", "vn", "yn", "0"],
  ["scanError", "bn", "xn", "``"],
  ["treasureClaimBusy", "Sn", "Cn", "!1"],
  ["treasureClaimMessage", "wn", "Tn", "``"],
  ["message", "En", "q", "``"],
  ["exporting", "Dn", "On", "!1"],
  ["sharing", "kn", "An", "!1"],
  ["treasureStatesRefreshing", "jn", "Mn", "!1"],
  ["includeForeignRadarTreasures", "Nn", "Pn", "()=>localStorage.getItem(Ne)===`true`"],
  ["luckyFirst", "J", "Fn", "()=>localStorage.getItem(Pe)!==`false`"],
  ["treasureViewer", "In", "Ln", "{playerUid:``,allianceId:``}"],
  ["scanModeTab", "Y", "Rn", "`manual`"],
  ["autoScanServerInput", "zn", "Bn", "``"],
];

// Position in the sequence of `(0,y.useRef)` declarators of R.
export const REF_SEMANTICS = [
  ["onStateRef", "De", "_"],
  ["onCountsRef", "ke", "v"],
  ["onLogRef", "w", "b"],
  ["searchGeneration", "T", "0"],
  ["tabCache", "D", "new Map"],
  ["treasureRefreshGeneration", "Le", "0"],
  ["treasureContextKey", "Re", "``"],
  ["optionsGeneration", "O", "0"],
  ["localeGeneration", "M", "0"],
  ["scanTypesTouched", "P", "!1"],
  ["wasReading", "Vn", "C.isReading"],
  ["scanProgressTimer", "X", "null"],
];

// Import header of the asset: local alias -> { from, exported, internal, kind, name }.
//   kind "pure"   : real function extracted from the asset bytes and executed (hash-asserted)
//   kind "stub"   : inert deterministic harness stub that only records the call
//   kind "react"  : React/runtime plumbing
// `internal` is the minified name inside index-BVfnK1wp.js that the export statement
// `export{<internal> as <exported>}` binds (resolved by recover-contract.mjs from the asset).
export const IMPORT_ALIASES = {
  e:  { exported: "A",  stub: "marchFollow",              note: "map_march_follow {serverId, marchUuid}" },
  t:  { exported: "B",  stub: "treasureClaimStatus",      note: "map_treasure_claim_status" },
  n:  { exported: "C",  stub: "truckPlunderClear",        note: "map_truck_plunder_clear {before}" },
  r:  { exported: "Ct", pure: "Ie",                       note: "truck state function (invalid/expired/full/protected/ready)" },
  i:  { exported: "Et", react: "jsx-runtime",             note: "require_jsx_runtime(); E = i()" },
  a:  { exported: "It", pure: "u",                        note: "__toESM interop helper; y = a(o())" },
  o:  { exported: "Mt", react: "react",                   note: "require_react (lazy CJS factory); supplied from the hook runtime" },
  s:  { exported: "Q",  stub: "localize",                 note: "lastwar_localize {language, keys}" },
  c:  { exported: "S",  stub: "scanClear",                note: "map_scan_clear {serverId}" },
  l:  { exported: "St", pure: "Fe",                       note: "max loot count of a truck row" },
  u:  { exported: "Tt", react: "useI18n",                 note: "useI18n(): {language, t}" },
  d:  { exported: "X",  stub: "plunderJobsList",          note: "map_plunder_jobs_list -> {dispatchJobs, truckJobs}" },
  f:  { exported: "Y",  stub: "coordinateJump",           note: "map_coordinate_jump" },
  p:  { exported: "Z",  stub: "listen",                   note: "Pe(eventName, handler) -> unsubscribe" },
  ee: { exported: "_",  stub: "dispatchPlunderCancel",    note: "map_dispatch_plunder_cancel {serverId, taskUuid}" },
  te: { exported: "bt", stub: "scanStop",                 note: "map_scan_stop" },
  m:  { exported: "c",  pure: "Lr",                       note: "error translator (uses Ir)" },
  h:  { exported: "dt", stub: "dispatchPlunderSchedule",  note: "map_dispatch_plunder_schedule(rows, maxRandomDelaySeconds)" },
  ne: { exported: "ft", stub: "truckPlunderSchedule",     note: "map_truck_plunder_schedule(rows)" },
  re: { exported: "ht", stub: "dispatchShareAlliance",    note: "map_dispatch_share_alliance(rows)" },
  ie: { exported: "k",  stub: "cityExport",               note: "map_city_export(query, options)" },
  ae: { exported: "mt", stub: "playerMarkSet",            note: "map_player_mark_set(row, marked)" },
  oe: { exported: "n",  pure: "Ci",                       note: "parse server id list" },
  g:  { exported: "nt", stub: "treasureStateRefresh",     note: "map_treasure_state_refresh {serverId, records}" },
  se: { exported: "o",  component: "Icon",                note: "SVG icon component (inert typed element)" },
  ce: { exported: "pt", stub: "search",                   note: "map_search {kind, query}" },
  le: { exported: "r",  pure: "Ti",                       note: "array remove" },
  ue: { exported: "t",  pure: "wi",                       note: "add parsed server ids" },
  de: { exported: "tt", stub: "treasureStateRefreshAll",  note: "map_treasure_state_refresh_all {serverId}" },
  fe: { exported: "v",  stub: "truckPlunderCancel",       note: "map_truck_plunder_cancel {serverId, trainUuid}" },
  pe: { exported: "vt", stub: "scanStart",                note: "map_scan_start {selectedTypes, scanMode}" },
  me: { exported: "x",  stub: "dispatchPlunderClear",     note: "map_dispatch_plunder_clear {before, taskKind}" },
  he: { exported: "y",  stub: "treasureClaim",            note: "map_treasure_claim {serverId, claimScope, prioritizeLuckySlots, targetUuid}" },
  ge: { exported: "z",  stub: "dataOptions",              note: "map_data_options {serverId}" },
  _:  { from: "rewardDisplay", exported: "t", pure: "rewardDisplay.e", note: "compact count formatter" },
  v:  { from: "GameAssetImage", exported: "t", component: "GameAssetImage", note: "reward/item icon component (inert typed element)" },
};

// Tab label keys, ze[] in the asset (`ze` object) plus scheduledPlunder.
export const TAB_LABEL_KEYS = {
  city: "map.city", resource: "map.resource", monster: "map.monster", truck: "map.truck",
  railway: "map.allianceTrain", dispatch: "map.secretTask", ghost: "map.ghostScout",
  treasure: "map.treasure", scheduledPlunder: "map.scheduledPlunder",
};
