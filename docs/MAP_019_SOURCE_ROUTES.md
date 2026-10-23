# MAP-019 current shipped-source alternative routes (2026-10-23)

Static evidence only. This companion does not establish live speed, completeness or field parity; those belong
in `MAP_019_SCAN_METHOD_REVIEW.md`. No production behavior or installed game file was changed by this inspection.

Reproduce from the repository root:

```powershell
python tools/map_019_review/source_routes.py
```

The command reads the installed RGMD assembly and Lua package, then writes ignored receipts to
`artifacts/map019/source-routes/`. `manifest.json` binds each decoded method/module receipt to its SHA-256,
metadata token or Lua prototype/source lines. The snapshot contains 22 decoded IL methods and 276 selected Lua
modules (an inventory, not a claim that every method was interpreted). Current assembly SHA-256:
`bfb740b4570c58bd2bcc7fb83f9b83d8121ce10fb1bf49040e9fb8b08e958b3e`.
Lua content is 3/27; preserved original entry SHA-256:
`50f3ae906a8e9898549c4ea740eedc772a88eb2979e165eb35733192d100a137`.
Whole package SHA-256 is retained in the manifest; it can change with an independently installed bridge.
The script admits known assembly bytes and recognized content/original entry before decoding.

REA `inspect_managed_artifact` was attempted on the actual installed `.rdl` and returned
`target_unavailable`, reason `unsupported binary format`; exact result is `rea_rdl_format.json`.
The following observations come from the repository's RGMD encrypted-operand decoder and Lua bytecode parser,
not REA pseudocode. No REA Evidence ID or runtime execution is claimed.

| Route | Current source observation and exact locator | Decision boundary |
|---|---|---|
| Fold-up points | `WorldPointManager.ParseWorldPointFoldUp` token `0x06003554`, IL `000b–0038` reads integer `pointIds`; IL `005e–006f` calls `RemoveFromBuildList`, `FoldUpBuildObject`, `RemovePointInfo`. `HandleViewUpdateNotify` `0x06003557`, IL `00a3–00b4` dispatches `type=foldUp` to this function. | **SOURCE_LIMITED:** this is removal/retirement of existing objects, not a compressed full-object discovery reply. Do not benchmark calling it as an alternative census. |
| LOD 1/2 | `GetServerLod` `0x06003567`: input <=4 returns 0, input 5 returns 1, higher input returns 2. `RecalculateBlockArg` `0x060035b2` clamps LOD 0–2 and indexes the Lua-supplied AOI block-size array. `get_lwAoiBlockSizeArray` `0x060035a9` calls `CSharpCallLuaInterface.GetLWAoiBlockSizeArray`. | **UNRESOLVED:** client supports these LODs; static code does not prove which complete families/fields the server emits at each LOD. Compare native received identities and fields against LOD0, not declared footprint alone. |
| Big3000 AOI | `UpdateLWAoi_Big3000` `0x0600351c`, IL `0007–001f` recalculates for world extent 3000, then IL `0024–00eb` reads camera anchors and computes AOI corners. | **SOURCE_LIMITED:** a 3000-world camera/AOI geometry path; its name is not evidence of a larger request limit or all-world records. Do not silently apply 3000-world geometry to the ordinary 1000-world scan. |
| Coarse Zombie Boss opt-in | Repository `CurrentClientMapBlockSource.FastCity.cs`, `ProbeFastCityBatchAsync` opt-in `UseCoarseZombieBossMap`, then `CaptureFullMonsterMapViaCoarseLodAsync` around line 315. It calls `ProbeCoarseMonsterMapAsync`, deduplicates identities, hydrates protection where eligible, then projects 2500 logical blocks. Source comment records LOD2 proof only on v18/v19 and defaults v26 to LOD0. | **UNRESOLVED CURRENT BUILD:** this is a meaningful existing opt-in for a fresh v27 same-session comparison. Static inspection establishes neither current field loss nor current success. Projecting 2500 blocks is not source population proof. Keep opt-in until fresh native coverage, fields, timing and restoration qualify. |
| Monster search | `Net/Msgs/FindMonsterMessage.luac`, `OnCreate root/0`, source lines 10–39: pointId, level, isBoss, optional searchRange and monsterType. `HandleMessage root/1`, lines 41–55 returns one pointId/uuid through END_SEARCH. | **COMPLEMENTARY:** one filtered result, no census cursor/page token in these methods. No basis for all generic Monster/Zombie Boss/Doomsday populations. |
| Resource search | `FindResourceMessage.luac root/0` lines 10–15 sends level/resourceType; root/1 lines 17–30 returns one pointId/uuid. `FindResourcePointMessage.luac root/0` lines 8–15 sends type/level/optional param; root/1 lines 17–42 handles one pointId. | **COMPLEMENTARY:** no unfiltered all-world enumeration or paging contract here. The latter's ordinary handler calls `GotoWorldPos`; any read-only diagnostic must isolate receipt capture from UI navigation. |
| Secret Task nearest search | `Net/Msgs/DispatchTask/DispatchFindNearestPointMessage.luac root/0` lines 5–7 has no census range/cursor; root/1 lines 9–29 reads one pointId/serverId and calls `CloseAllWindows` and `GotoWorldPos`. | **COMPLEMENTARY:** nearest-point lookup, not a full Secret Task list. Do not invoke its ordinary UI side effects as a silent census query. |
| Radar Boss list | `Net/Msgs/RadarCenter/FindMonsterBossMessage.luac root/0` lines 9–18 sends optional level/radarSearch; root/1 lines 20–23 forwards to `RadarCenterDataManager.HandleFindMonsterBossBack`. Radar manager methods and disassembly retained. | **COMPLEMENTARY/UNRESOLVED:** player radar/search context is supported; complete same-session map-wide identities and required fields are not proved by this static handler. |
| Truck/Train list | `Net/Msgs/Railway/GetTrainListMessage.luac root/0` lines 11–14 sends isRefresh; root/1 lines 16–33 reads ls/allianceTrainList/trainServers. `LWTrainDataManager.OnTrainListGet root/18` lines 224–247 replaces enemyTrucks/enemyTrains. `GetEnemyTruckList root/21` lines 261–263 only returns that stored list. | **COMPLEMENTARY:** no cursor, all-world promise or identity coverage proof in these methods. `TryGetTrainList root/17` lines 210–221 also updates local REFRESH_TRAIN_LIST_TIME on refresh; prefer existing measured list receipts over repeating this unchanged route. |
| Own/home march refresh | `WorldMarchDataManager.WorldGetMarchInfos` `0x060032aa`, IL `0000–0049` sends home x/y via `WorldGetRectMarchInfosMessage`; `CSSetData` `0x060019a6` serializes x/y, needCross=true and future _id. `HandleWorldGetRectMarchInfos` `0x0600327b` resets own/to-me/owner caches, reads marchInfos and explicitly classifies IsMyMarch/IsTargetForMine. | **SOURCE_LIMITED:** home-centered own/incoming-march refresh; no all-Truck global enumeration parameter. The handler also changes native cache state. Absence of a full-world query here is not proof that the protocol has no other route. |
| Truck push collection | `HandleWorldMarchGet` `0x06003278` reads serverMarchArr, uuidSet, marchInfos and combines them. `HandleWorldMarchGetImpl` `0x06003279`, IL around `008a–0155` reconciles uuidSet and destroys absent marches. `ParseDataWorldMarchGet` `0x0600327a` populates allMarches. | **PROVEN STATIC REPLACEMENT:** sampling a persistent cache once per frame can miss records replaced between pushes. Capture each push before retirement. Static source does not establish the server's geographical Truck selection; preserve MAP-015 native rectangle measurements for that claim. |
| Remote without context switch | `ParseWorldGetBlock` `0x06003553`, IL `0000–001c` clears temporary lists, IL `0021–005e` reads serverId and, when WorldScene.IsSingleServerMode is true, returns if it differs from DCPlayer.GetCurServerId. `SendViewRequest` `0x0600356a` also substitutes cross-server/world context in its request construction. | **SOURCE_LIMITED CURRENT PARSER:** ordinary single-server cache ingestion drops foreign replies. This is not proof that the server rejects explicit foreign requests, nor proof that a private pre-parser collector would receive complete foreign data. Such a candidate requires native source/server correlation and separate qualification; do not weaken the production guard. |
| Incremental refresh | `HandleViewUpdateNotify` `0x06003557` routes create/change/relocate to `ParseWorldPointUpdate` `0x06003556`, remove to `ParseWorldPointRemove` `0x06003555`, foldUp to `0x06003554`; it applies sid/cross-server/battlefield/season conditions. March add/delete implementations are `0x06003280`/`0x06003284`. | **COMPLEMENTARY:** actual incremental messages exist. No global revision cursor, replay-gap recovery or guarantee of notifications for every previously swept AOI was established. A full discovery snapshot plus passive updates cannot be called an accurate full refresh without subscription-scope/gap proof and a drift comparison. |
| Minimap | `UI/UIMainMiniMap/Component/WorldMiniMapComp.luac`: `OnCreate root/0` calls WorldSendGetALPointsRequest; `LegacyGetCityPointDataFast root/8` lines 146–197 asks GetAllMainBaseListByType for self/alliance/alliance leader; `NewGetCityPointDataFast root/9` lines 203–280 uses current/self server, own position and GetALMemberPoints. Main controller also consumes alliance members. | **COMPLEMENTARY:** examined City minimap path is alliance/self-oriented. It supplies no all-player/all-kind census here. Other seasonal/battlefield minimap modules were inventoried, not exhaustively analyzed. |
| Ghost/event/activity lists | Current `GhostreconGetTaskListMessage` routes to ActGhostreconManager; alliance manager `GhostReconGetAllianceTaskList` and push handlers retained. Doomsday manager has event/main/superboss/alliance-style state handlers. `DispatchTreasureGetSelfInfoMessage` routes to SplinterExchangeManager.RefreshSelfInfo; alliance exchange message similarly represents exchange state. | **COMPLEMENTARY/UNKNOWN:** self/alliance/task/activity lists cannot be promoted to all-map Ghost/Treasure/Doomsday completeness by name. Current event availability, map-family scope and required fields need native positive witnesses. DispatchTreasure exchange state is not automatically the Map Treasure family. |

Request-shape prototype boundary: production Lua currently uses
`math.min(9999, s.nextAoi + 159)` and C# `CurrentClientMapBlockSource.CameraFree.cs` validates exactly 63 replies.
A private 80-area acquisition diagnostic would require 125 independently fresh/covered replies. It may compare
raw typed source records and bytes/timing without passing that fixed production admission gate. Such a result
must be labeled acquisition-only, with field and drift checks; do not edit the default or loosen its gate in
phase 1. Reply-time capture addresses a known cache-retirement mechanism, but does not alone establish safe
parallel server behavior or correct correlation of overlapping replies. Neither LOD nor parallelism is ruled
out for all kinds by this static analysis.

This inspection supplies source-backed exclusions and concrete candidate boundaries. It does not justify
"globally best", "all alternatives exhausted", a new per-kind speed verdict, or READY_FOR_LEAD_AUDIT for the
whole work item without the primary report's measured matrix.
