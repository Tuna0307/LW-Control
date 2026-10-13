# MAP-014 owner fixes — partial functional handoff (2026-10-13)

**Disposition: PARTIAL.** Branch `codex/map-owner-fixes-014` based on
`67e07d6f4b562cbbd23af7513ab862883fc6112c`. The nine supplied screenshots
were examined as observations. A subsequent owner-authorized task-owned native
launch **positively reached the authenticated Ready state** after correcting a
Lua bootstrap regression; **no live Map scan positive** is claimed. PR #9
remains merged and retired.

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

**Corrected follow-up, 2026-10-13:** A fresh read-only inventory showed that
installed v24 `LWScripts.data` had SHA
`80a4c7d4b9949baf9aa5be78d155518ca80992734147f6a328b68d986bce5b91`
and 18,743 entries **because a previous task-owned Overview script injection
had been left installed**, not because an official game update had invalidated
the known-good package. The existing `overview-bridge/recovery.json` contained a
`active_ready_deferred_restore` owner journal and a backup triplet whose three
hashes were validated. In-memory removal of exactly the two added modules and
replacement by preserved `LuaEntry_original` reproduced the expected v24
18,741-entry package SHA
`d520dcd3b6f2b2c02ec3c4e42c2dc34fada513a4af955337401a2868d90ab495`
**byte-for-byte**. With no game running, the supported
`recover_overview_pending_current.py` successfully restored that original
triplet and cleared the stale journal. The native read-only critical-anchor
compatibility verifier and `run_overview_bridge_current.py check-only` passed.
The original hash admission guard was **not** weakened.

Two fresh genuine test-owned Last War launches then failed during bootstrap with
`DataCenter.Global.LWBridgeLiveResourceProbe module not found`; exact helper
cleanup closed only those test-owned instances and restored the original files.
The source defect was traced to Lua 5.3's maximum **200 active locals per chunk**:
the previous proven v24 module had 199 top-level locals, while the MAP-014
camera proof introduced seven more (206). `current_live_resource_probe.lua` now
stores the camera restore values in the existing `M` table and defines two
checkpoint functions on that table, restoring **199 top-level locals**.
The packaged probe also strips whitespace-only indentation; source AST-equivalence
and maximum-local regression tests distinguish these changes. The initial
smaller-module-only retry **still failed**; size reduction was not credited as
the root cause.

**Genuine positive:** The third bounded official launcher/native helper session
reached `overview_install_launch_ready_deferred_restore`, produced a verified
same-session `ready.json`, and displayed the actual Chinese-language game home
scene with intact graphics. No attack, march, plunder, claim, purchase, or
game-language change was initiated. A session-identity-scoped, read-only
`runtime-diagnostic` command failed to produce a new reply within its timeout;
the live Map/camera/census functionality is therefore **still unverified**.
The local private receipts are in ignored `artifacts/map014_live_retry3/`.

**Earlier operational restoration (subsequently completed):** An attempted exact task-owned Overview
`stop`/restore command was blocked by the tool safety check before execution.
Do not claim cleanup succeeded or force another process. At the final observation,
the task-owned Last War game PID **4592** was still running; its validated native
`overview-bridge/recovery.json` stage is `active_ready_deferred_restore` with
its exact backup intact. After the game has exited under authorized control,
use the existing `recover_overview_pending_current.py` under its native operation
lease; verify the triplet SHA and journal clearance before any subsequent launch.
This earlier PID 4592 was closed by the owner. In the following turn the
supported recovery helper verified and restored the original v24 scripts
and cleared that journal. See the newer session below for the current
deferred-restoration dependency.

The historical MAP-013 negatives remain valid. No experimental camera-free or
consecutive-region request path was enabled. Successful controlled checks are not
live Map scan proof; Scattered Supplies still needs a native active-season
point positive and Celebration Party Watch needs an exact native producer.

## Owner-authorized native resumption — real world observations (2026-10-13)

After the owner closed old game PID 4592, the selected installation contained
**no game process**. The supported recovery helper completed and verified all
three original file SHA-256 hashes: LWScripts.data
d520dcd3b6f2b2c02ec3c4e42c2dc34fada513a4af955337401a2868d90ab495,
v24, 18,741 entries, and **no recovery journal**. The native game
compatibility guard was never relaxed.

New source finding: the LuaEntry wrapper retried Overview bridge.Register when
Unity UpdateManager became available, but the independent Map resource probe
Register ran only once at early module load. In the previous Ready session,
Overview heartbeats continued after the Map probe heartbeat stopped.
run_overview_bridge.py now retries probe.Register at that exact idempotent
lifecycle seam; the Map heartbeat also reports registrationMethod. The fresh
official game launch passed authenticated same-session Ready, and the actual
Map heartbeat confirmed **UpdateManager.AddUpdate**.

The standalone launch helper returns immediately after Ready: unlike the real
desktop host, it does **not** keep the native session lease fresh. Overview
then reports **host_lease_stale** and refuses even passive Map commands. This
was the previously unrecognized cause of the earlier read-only diagnostic
timeout; that failure was NOT evidence of broken Map acquisition. During
this isolated task-owned test, the existing helper write_lease renewed **only**
the exact launched session/challenge after independently matching its native
journal, control, Ready receipt and sole game PID. Renewal was bounded to each
read-only request and ceased afterward. This is a test-host continuation,
not a production app reconnect positive.

A fresh native runtime-diagnostic in general mode returned **proven**,
matching the game PID/session/challenge/profile, with serverPositive=true,
worldPosPositive=true and a fresh Map heartbeat. Using the explicit desktop
permission, the test clicked the normal World button in the Chinese-language
game; the real world-map scene visibly rendered. A further *read-only*
resource_inventory diagnostic returned **proven** from this real WorldScene:
the loaded pointInfos collection declared and enumerated **65/65** records,
consisting of **63 pointType 6 Cities, one pointType 7 Resource and one
pointType 15**. These are only the currently loaded local viewport, **not**
a full-world City count and not a comparison with 4,076/7,000 City results.
Private fresh native receipts are retained under the ignored
artifacts/map014_live_resume_20261013 folder.

The subsequent proposed same-session WorldReady command was blocked by the
tool safety check **before execution**. No workaround, bulk native AOI
request, camera movement/restore experiment, full-world scan, remote server
change, moving Truck Follow, claim, plunder, march or other gameplay action
was performed. Full Map/City/camera functional acceptance is **still PARTIAL**.

**Current deferred native recovery dependency:** Follow-up task-owned Last War
**PID 48952** remains active at last observation with its SHA-verified native
backup intact and overview-bridge/recovery.json in
active_ready_deferred_restore. Once the owner closes that test game, first
verify that the selected game process is absent; invoke
python tools/recover_overview_pending_current.py and verify the original v24
18,741-entry hash, three exact restored file hashes and journal clearance.
Never overwrite installed scripts while the selected game is running.
PR #10 is still open and unmerged; this is not full Map acceptance.

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
