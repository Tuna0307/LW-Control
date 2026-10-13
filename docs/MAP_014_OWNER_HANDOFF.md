# MAP-014 owner fixes — partial functional handoff (2026-10-13)

**Disposition: PARTIAL.** Branch `codex/map-owner-fixes-014` based on
`67e07d6f4b562cbbd23af7513ab862883fc6112c`. The nine supplied screenshots
were examined as observations; no game/account action was performed to create a
positive that is currently unavailable. PR #9 remains merged and retired.

## One obligation table

| # | Baseline/reproduction | Cause/source and correction | Executed proof | Remaining limit / witness |
| --- | --- | --- | --- | --- |
| 1 camera | Owner home image shows black/stretched scene after scan/return. Previous `coverage` used `SetFOV(120)` and changed Unity `aspect`, while home-restore success only checked logical tile/network flags. | `tools/current_live_resource_probe.lua` captures actual camera position/transform/rotation/projection/FOV/aspect/zoom/anchors, reapplies captured values after `RefreshCameraAnchor`, checks geometry after the fresh home response, and emits before/after evidence. `CurrentClientMapBlockSource.FastCity.cs` now rejects batches without a complete restoration receipt. | Lua AST and Desktop Release compilation; deliberate gate fails closed on missing geometry evidence. | **No new native/visual positive** for finished scan, Stop, timeout/error, and Auto return. Distorted-view actual cause is a strong source-path candidate, not a live-confirmed repair; camera restoration and subscriptions still need direct native comparison. |
| 2 City 4,076 | Owner City-only completion shows **4,076 all Cities** and **94 for the selected alliance**. Earlier 7,000–7,060 genuine snapshots are comparisons, not a quota. | `GetAllMainBaseList()` emits source/accepted/within-AOI and identity-rejection counts; point-type histogram and `_pointInfos` City count corroborate the retained list. Previously skipped missing owner/index/position candidates now fail the batch. C# validates the independent counts before staging. | Lua source syntax, compiler and controlled existing staging/publication/rollback tests. | **Native acquisition still unqualified**: individual City-only vs mixed, segment replies/cache insertion, subtypes and independently identified missing cities need compatible-session same-window source witness. 100% AOI progress does not certify recall. |
| 3 cross-server warzones | Header Truck-plunderable options flicker during polling. | A transient missing native `LWTrainDataManager` match list is now `known=false`; `Map317CommandService` keeps prior same-run options on unknown/failed/stale polls and adopts genuine empty updates. | `Map014OwnerFixChecks` exercises successful, transient failed, unknown and authoritative empty polls with actual command/overlay. | Actual Cross-server popover idle/Auto/profile-retire clicks need extracted production/live owner observation. |
| 4 remote row | After Auto 2213 → 2212, clicking an old 2213 row previously sent coordinates while still on 2212. | `MapActionControlPlane.CoordinateJumpAsync` switches to the row server while retaining its single operation lease, checks reached server and invokes the existing native server-verified coordinate route. Native switch gets its supported bounded completion window. | Controlled same-x/y-on-2212-vs-2213, failed switch, wrong reached server, overlapping operation checks pass. | Live 2213 stored row → 2212 home → 2213 destination and expiry/session retirement witness pending. |
| 5 Truck Follow, moving Doom | Truck Follow was disabled or fell through coordinate jumping; scan coordinates become stale. Doom `SuperRunningBoss` is sourced from `LWDoomsdayManager.BossVO` with `uid` and `targetPos`. | Mounted UI calls `map_march_follow` for Truck/Railway, keyed by immutable march UUID; polls the supported `GoToUtil.JumpToMarchByUuid` for fresh WorldMarch position, Stop clears polling, scan/arrival/server/tab/profile teardown cancels. No gameplay march. | Real command/call-contract code compiles; existing controlled action-lease and native follow command tests. | **No live moving Truck positive.** Doom boss UID is not proven to be a WorldMarch UUID; do not route it through Truck follow. Needs supported fresh-position tracking command/positive witness. |
| 6 task owner | Secret Task shows long bare number. | Native Dispatch attempts optional `ownerName`, `allianceName`; matching exact decimal owner UID on a same-server stored City row can supply genuine name/alliance. Otherwise UI says `UID …` and preserves digits as text. | Controlled 19-digit exact UID, genuine matching City and unrelated-server exclusion, unknown fallback tests pass. | Native task/player metadata positive and name accuracy still need live verification. |
| 7 Resource filters | Only name/sort before. | Canonical `Map317.MapStore` applies inclusive minimum/maximum level, native amount (`resourceRemainingAmount/resourceFullAmount` + detail-known), native tile (`blackTileKnown/isBlackTile`), with All/Unknown semantics and localized controls. The Resource result table now also displays known `remaining / full`, true native black/normal/unknown terrain and an explicit unknown occupancy, rather than conflating zero/missing/native Idle. | Actual Map317 handler/SQLite combined predicates, unknown states, level, counts and paging; mounted EN/JA test checks missing and genuine values; production UI build. | Native semantics source-mapped; real populated mixed-resource filter counts/screens pending. |
| 8 Truck power | No million-scale cap. | Blank unrestricted; decimal millions validated; 25 = inclusive 25,000,000 through stored `page.power`, unknown NULL cannot compare true. | Actual handler/SQLite `25,000,000` vs `25,000,001` vs NULL, negative and invalid inputs pass controlled cases. | Live native power units and UI field still need packaged owner check. |
| 9 multi rewards | Single item dropdown. | Multi-select retains actual `reward:type:itemId` denomination identities; SQL requires `EXISTS` for every selected key before count/page, with duplicate-key binding and clear/removal. Stack `count` stays separate; item-count sort uses first key. | Actual handler/SQLite both-items vs one-item, inclusive power, 255-record/adversity and page-three count; old `itemKey` path retained. | Live genuine Truck item IDs and quantity/denomination UI checks pending. |
| 10 reward icons | Truck diagram squares blank. | Existing native `ItemTemplateManager`/`RewardManager` config mapping and `LoadPath.ItemPath` feed native `game_asset_image`, which validates genuine PNG signatures. An item with known identity/name now survives an absent icon (placeholder, count and tooltip retained); query options accept NULL icon path. | Static frontend, native project and query tests. | **Icon restoration is not verified**. Need compatible installed game's real reward ID→path/sprite→PNG bytes→rendered extracted UI. No invented icons or private assets. |
| 11 Auto Clear | Clear was Manual-only. | Auto invokes the same `map_scan_clear` command/reset; `MapAutoScanCommandService.AcquireMapClearLease` gates Auto admission for the entire asynchronous clear; busy/error/server/profile ownership protections remain backend-enforced. | Actual Map317 clear handler: active-Auto denial, wrong-server denial, correct-server deletion retaining other server; existing Auto ordering/Stop regression suite. | Mounted extracted Auto click and cycle-start inverse positive pending. |
| 12 scattered supplies | Owner names “Scattered Supplies”. | The installed game-language reference maps `season_s2_ice_supplies_11` to **Scattered Supplies / 散落的物资 / 散乱した物資** (en/zh-CN/ja). Existing read-only world producer stages actual `pointType=27`, `WorldSuppliesPoint` and `LWIceSupplies` `suppliesType=1` under Treasure; type 3+ are separate. The Treasure filter already matches `supplies:1` before pagination. Fixed native-mode UI name loading to **request this derived game key** from its verified game-root/build locale source, even when records omit optional `treasureNameKey`. No ninth synthetic kind or collection. | Exact game-owned locale key/value observed from local read-only `.json.gz`; existing current Lua/C# source chain; Map317 handler/SQLite options, type-1 vs 3 vs ordinary vs foreign-server negatives, and mounted EN/light + JA/dark derived localized filter selection pass controlled checks. | **Source mapped / candidate implemented, no native positive.** Need an actual v24-compatible live season-S2 `WorldSuppliesPoint` whose `LWIceSupplies` config resolves to `type=1`, coordinates/identity, and nonzero delivered rows. Locale alone does not prove a presently spawned point. |
| 13 Celebration Party Watch | Owner requests another map object. | No matching current-client point/event class/config identity positively recovered from checked localization/source; no supported scanner discriminator exists. | Source and existing selected-kind enumeration inspection. | **Unavailable** pending localization key/Chinese original, event config ID, runtime producer identity and a genuine active-event native sample. No fabricated map object or collection. |

## Compatibility evidence and boundaries

Executed controlled verification: `dotnet run --project
tests/LWBridge.Desktop.Checks/LWBridge.Desktop.Checks.csproj -c Release
--no-restore` passes the full Home/Map/Auto suite including MAP014's actual
command/store inverse cases. `dotnet run --project
tools/map_008_scan_checks/Probe.csproj -c Release --no-restore` reports
`MAP008_SCAN_CONTROLLED_PASS selections=255 adverse=15` against the real
strategy/planner/engine, without a game launch. `npm.cmd run check` includes
`check-map014-mounted.mjs`, a real mounted React Map page check for EN/light
1280 px and JA/dark 375 px with native-command stubs: filter combinations,
genuine/unknown Resource presentation, game-root/owner-localized season supply
selection (`suppliesType=1`), multi-items, Follow/Stop and Auto Clear; both
runs finish with zero owned intervals. Edited Lua helpers parse with `luaparser`. The MAP-013 source-lock
tests use new, explicitly identified candidate hashes and retain the prior
source fingerprints in comments. These results are **controlled only**.

The canonical `npm.cmd run build` and `node scripts/check-release-ui.mjs`
headless Edge sweep passed **41 captures**, including `map-resource`,
`map-truck` and running Auto. EN/light 1365 px, JA/dark 640 px and JA/dark
375 px had zero captured errors or document overflow; the conditional JA
narrow Auto screen confirmed Stop enabled and Clear disabled during the cycle.
Screen images/coverage remain in ignored
`artifacts/map-014-ui-review/` (also a fresh private temp proof). Inspected
Resource, Truck, JA/narrow Auto and default Map captures. They are fixture
visual checks, not real installed-game item PNGs or native scan evidence.

Read-only installed v24 `LWLF` source gate (from `tools/map_009_offline/inspect_current.py`)
expects `d520dcd3b6f2b2c02ec3c4e42c2dc34fada513a4af955337401a2868d90ab495`,
but this machine currently supplies
`80a4c7d4b9949baf9aa5be78d155518ca80992734147f6a328b68d986bce5b91`.
Three hash-locked current-body/scene/detail tests deliberately reject the changed
installed package. **Do not alter the expected game hash or bypass admission to
obtain a live positive.** Earlier MAP-013 negative packet/cache receipts remain
valid historical negatives; no camera-free or consecutive-region route was enabled.

Successful controlled checks do not prove a fresh game outcome. No game instance
was created under this branch because compatibility admission failed. No owner
process was stopped; there was no task-owned live session or restoration journal
to clean up. The official game's Chinese language was left unchanged.

**2026-10-13 follow-up:** Rechecked the actual installed header (v24, 18,743
entries) and native read-only inspection rejected the changed SHA before any
game launch. Local `.json.gz` localization was inspected read-only and cannot
override source admission. The new game-text loader proof is a controlled
identity-valid fixture, **not** a live native game-locale response. Source-traced
Scattered Supplies is available for seasonal capture under the existing Treasure
kind; Celebration Party Watch is still blocked on its exact game identity.

## Difference from accepted baseline

The owner-facing additions are the new Map filters and Auto Clear control, stable
warzone choices under missing native reads, remote server-aware row navigation,
source-keyed passive Truck/Railway Follow, optional native Task owner metadata,
and stronger camera/census rejection of incomplete acquisition. The last two
deliberately fail closed where source evidence is missing; they may reject a scan
previous builds reported complete. This is a CURRENT_CLIENT_ADAPTATION, **not**
an original private scanner equivalence or full-Map acceptance.

Historical accepted Home, existing published SQLite files, Auto Stop-before-return,
per-profile ownership, active gameplay exclusions and MAP-013 negatives remain
preserved. All game action/plunder/claim/attack paths remain out of this work item.

## Lead/owner native retest when compatible

Use a separately task-owned authenticated session after the existing compatibility
gate accepts it. First compare native home camera checkpoint before/after a single
completed City-only scan, active Stop, forced acquisition error/timeout and Auto
remote→home. Require full transform/projection/anchor/subscription confirmation and
an undistorted image. Then compare native City-only and mixed captures in the same
populated regions, enumerate missing UUID/point IDs and independent response/cache
admission, and audit published SQL. Only then exercise exact 2213 remote row,
moving Truck Follow/Stop and real reward icon bytes in the extracted Windows UI.
Inspect EN/light, JA/dark and narrow controls independently, without touching
the official game's Chinese language. A native discrepancy is a failure, not a
reason to fill a historical quota.
