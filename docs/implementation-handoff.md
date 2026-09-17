# ChatGPT Web implementation task — shared Manual Scan engine

## Current superseding owner follow-up - LWB-R7-019, 2026-09-17

The exact current-v18 behavior behind the owner's click observation is recovered: `WorldMonsterDes.RefreshData` sends `MonsterInvasionBossDetail(view.ctrl.serverId,data.uuid)` only for exact `WorldMonsterSpecialType.MonsterInvasionBoss`; the response enters `MonsterProtectionManager:OnGetDetail`, and UI Remaining derives from `GetMonsterProtectionEndTime(uuid)` relative to server time. Production now sends this automatically while each eligible boss AOI is loaded, deduplicates and run-binds UUIDs, allows only a 100 ms host settle plus one 40 ms unanswered retry, and performs one final collection. These two pacing values are IMPLEMENTATION POLICY, not recovered constants. Optional detail failure cannot fail or reset base acquisition, and unknown/inactive protection cannot fabricate Remaining.

Final current-v18 Normal proof completed 2,500/2,500 blocks with zero failed/unread blocks in 82.9616908 seconds, published/reopened exactly 10,350 dynamic Monster rows, issued 81 automatic detail requests plus 81 bounded retries and captured 50 authoritative replies. The population contained no active positive protection deadline, so a visibly ticking positive Remaining is not yet LIVE-PROVEN; `-` is correct for inactive/unanswered rows. Deterministic known-active coverage proves a received positive deadline persists and renders through the existing countdown. Evidence: `2026-09-17-r7-zombie-boss-protection-batch.json`.

**Owner next check:** run one normal Monster scan in the delivered app and inspect Zombie Boss Remaining without clicking. The technical scan/time gate is closed; owner-visible positive Remaining can only be confirmed while the live server actually exposes an active protected boss. After that visual check, proceed to Railway/Train on the shared scanner.

## Current superseding owner follow-up - LWB-R7-018, 2026-09-17

The owner exposed a real Monster regression after the v18 update: progress repeatedly reached ~4.5%, reset to 0%, and repeated. Persisted scan checkpoints identify the exact cause as `monster_invasion_protection_response_timeout` on the new Zombie Boss protection enrichment, not broken v18 AOI acquisition. Protection detail is optional enrichment and now cannot discard the base Monster scan: timeout/transport failure leaves protection unknown, clears pending correlation, preserves diagnostic target/request/ready counts, and continues. Eligible Invasion Zombie Boss rows with unknown protection do not fall back to unrelated `zMBossInfo`. Deterministic full-world coverage, Lua parse and Release build are green. The interrupted candidate was recovered to exact original v18 and current compatibility is `ok=true`. Evidence: `2026-09-17-r7-monster-protection-scan-resilience.json`.

**Owner next test:** run one normal Monster scan. First acceptance is that progress no longer resets/stalls and reaches completion. Then inspect Zombie Boss Remaining without clicking/Jumping. A positive protection countdown remains unproven and must not be claimed until that owner test succeeds. Do not begin Railway/Train before this Monster gate.

## Current prerequisite correction - LWB-OVR-017, 2026-09-17

A content-only Last War update from Lua v17 to v18 exposed a regression in `LWB-OVR-016`: because the installed v17 package still exactly matched the cached settle marker, LWBridge skipped untouched official settlement, installed its temporary candidate, and then let the official launcher attempt the `18 <- 17` delta against modified bytes. The patch download itself matched its advertised size/CRC, but the reconstructed temporary package failed CRC. Running the same official launcher once against restored untouched v17 bytes immediately applied v18 successfully. Current v18 (`e4f875a8...d9d2a1`, size 41,296,021, CRC 738537259) passes the dynamic critical-anchor compatibility gate.

Production now detects a new `LWLua decode failed: crc mismatch` during the helper-owned candidate launch, completes exact recovery, invalidates the settle marker, forces one untouched official settle/update, revalidates compatibility, and retries the bridge helper exactly once. Other failures do not enter this retry. Dedicated settle checks and the complete deterministic suite pass; Release build is 0-warning/0-error. Evidence: `2026-09-17-v18-auto-update-recovery.json`. Resume the separate Zombie Boss Remaining owner test after this prerequisite correction.

## Current superseding owner follow-up - LWB-R7-017, 2026-09-17

Monster owner follow-up is complete. The immutable LWBridge 0.3.1 Monster table was rechecked and contains Coordinate, Name, Level, Distance and Updated only: it has no original Remaining column and no original level filter. In the rebuild, Remaining therefore stays an explicit usability extension tied only to current-v17 `WorldMarch.zMBossInfo.shieldEndTime`; the current `ZMBossInfo` type was statically recovered with real `shieldHp`, `shieldMaxHp`, `shieldEndTime` and related stage fields. Generic march `endTime` and `nextStageTime` are not substituted. The collector now prefers the normal xLua `zMBossInfo` field getter with a reflected same-field fallback.

The Monster level selector now presents `Any Level`, then `5, 10, 15, ...` through the smallest five-level bucket covering the observed source-backed maximum Monster level; selection remains an inclusive maximum (`10` means `level <= 10`). Distance remains game-owned `WorldScene.TileDistance(homeTile, monsterTile)`, where `homeTile` comes from the player's authoritative base/home world point (`CS.GameEntry.Data.Player.PlayerWorldPointId`, with `WorldPointManager.GetMyPointInfo().pointIndex` fallback). A new deterministic regression requires that home tile on every full-Monster request.

Fresh current-v17 ordinary Monster Manual Start completed all 2,500 logical blocks with zero failed/unread blocks in 86.819 s Normal, published 12,017 rows, reopened exactly 12,017, and all 12,017 carried Distance (`min=2`, `max=1055.8356933594`). This live population contained zero `ZMBossInfo` rows / shield deadlines, so an active shield countdown is not fabricated or claimed; Remaining truthfully shows `-` until a shielded Zombie Boss is actually present. Release builds are 0-warning/0-error, deterministic suite `ok=true`, and all 36 browser checks pass. Evidence: `2026-09-17-r7-monster-owner-followup.json`.

**Superseded by R7-019:** the technical v18 full-scan retest is complete. Railway/Train follows the remaining owner-visible Monster page check, then Dispatch/Secret Task, Ghost Ops and Treasure on the shared scanner.

## Prior checkpoint - LWB-R7-016, 2026-09-17

Truck is now LIVE-PROVEN through the same ordinary Manual Start engine as City, Monster and Resource. Current-v17 Truck-only Start completed all 2,500 logical blocks with zero failures/unread blocks in 71.321 s Normal, published 175 Truck rows, and reopened exactly 175. All 175 carried source-backed Truck type, quality, power, train config and raw train data; all 175 also exposed the newly proven normalization `trainDataJson.arriveTime -> arriveTs` and `trainDataJson.marchInfo.robTimes -> robTimes`. The dynamic count is not an invariant. Evidence is aggregate-only: `2026-09-17-r7-truck-full-manual.json`.

Truck capture enumerates `WorldScene.MarchDataManager.GetAllMarchesByCS` inside the same 200-request / exact-10,000-AOI full-world planner. Classification uses top-level `trainType` when present and otherwise `trainDataJson.type`; unclassifiable rows stay unknown, and current-v17 type `1` is Truck. Do not guess the still-unproven recovered row fields `maxLootCount`, `protectTime`, `isSpecialURQuality`, or UI-ready `currentGoods`.

**Next:** Railway/Train, then Dispatch/Secret Task, Ghost Ops and Treasure on this same scanner; then mixed/all-eight and Auto Scan. Do not add a second acquisition engine.

## Prior checkpoint - LWB-R7-015, 2026-09-17

Resource is now LIVE-PROVEN through the same ordinary Manual Start engine as City and Monster. Current-v17 Resource-only Start completed all 2,500 logical blocks with zero failures in 70.958 s Normal, published 442 Resource rows, and reopened exactly 442 from the persisted database. All 442 carried source-backed `resourceTypeId`, level, and known gather occupancy from current `ResPointInfo`; 71 were occupied in this live population. Resource rows are collected inside the same 200-request / exact-10,000-AOI union planner by filtering `WorldPointManager._pointInfos` point types 1/7/26 and using `GetResourcePointInfoByIndex` when available. Evidence is aggregate-only: `2026-09-17-r7-resource-full-manual.json`.

The observed counts are dynamic, not invariants. The numeric `resourceTypeId` -> original public `resourceNameKey` mapping remains unresolved downstream presentation work and is not guessed. Native ack/removal/drain and original scheduler parity also remain separate open contracts.

**Next:** Truck, then Railway/Train, Dispatch/Secret Task, Ghost Ops and Treasure on this same scanner; then mixed/all-eight and Auto Scan. Do not add a second acquisition engine.

## Current superseding checkpoint - LWB-R7-014 / LWB-OVR-016, 2026-09-17

Owner-reported Monster/Home corrections are now the active completed-code checkpoint pending final Git delivery. Monster **Remaining** is narrowed to the game-owned `zMBossInfo.shieldEndTime` source and ticks locally only when that deadline is positive; generic march `endTime` is no longer displayed as the shield timer. The Monster level selector is an inclusive maximum (`60` means `<=60`), Level/Distance sorting is wired through the persisted query instead of clearing rows, and failed searches preserve the current table. Current-client world readiness now recovers the player's home tile through `CS.GameEntry.Data.Player.PlayerWorldPointId` with `WorldPointManager.GetMyPointInfo().pointIndex` fallback, and a current-v17 complete run LIVE-PROVES real `distanceFromHome` on all 12,731 published/reopened Monster rows.

The zero-hold full-world request path restores the internal camera transform in the same Lua callback after the native request is queued; complete 2,500-block live runs prove acquisition survives this anti-flicker change, while owner-visible flicker reduction remains a later visual check. `LWB-OVR-016` also prevents the update-safety warm-up from launching/closing the official game on every unchanged start: read-only recovery/compatibility still runs every time, while the destructive warm-up is reused only for the exact validated game root + package hash. Evidence: `2026-09-17-r7-owner-map-fixes.json`.

**Next:** complete Resource on this same shared engine, then Truck/Railway/Dispatch/Ghost/Treasure, mixed/all-eight and Auto Scan.

## Current superseding checkpoint - LWB-R7-013, 2026-09-17

Monster result usability is now the newest completed slice on top of the R7-012 shared scanner. A current-v17 full Monster acquisition exposed source-backed positive `endTime` deadlines on 11 of 3,853 sanitized live rows; no row identities or coordinates are retained. Production `map_data_options` now supplies Monster levels from the recovered persisted/staging source selector, the rebuild supports exact-level Monster filtering, localized visible-name keyword search avoids raw-JSON schema-key false matches such as `zombieRushId`, and Monster rows show a locally ticking countdown when a supported positive deadline exists (otherwise `-`). The timer-field acquisition is LIVE-PROVEN; the search/filter/countdown UI behavior is IMPLEMENTED/OFFLINE-TESTED. Exact-level and visible-keyword overrides are explicit rebuild usability policy, not claimed original 0.3.1 frontend behavior. Evidence: `2026-09-17-r7-monster-usability.json`.

**Next:** Resource complete-run acceptance on the same shared engine, then Truck/Railway/Dispatch/Ghost/Treasure, mixed selections and all eight. Do not add a second scanner.

## Current superseding checkpoint - LWB-R7-012, 2026-09-16

The owner resumed Map Data and exposed two real integration problems: the ~one-minute performance work had not yet been delivered, and one mid-scan Stop could leave the UI in `cancelling` until a second click. `LWB-R7-012` now closes this checkpoint. Current-v17 live measurements show each production coverage request returns a stable 5x10/50-AOI `_curViewIndex` footprint, so the full-world planner uses 200 sequential requests instead of 250 while retaining the exact 10,000/10,000 union gate. Ordinary full-Monster Manual Start completes at 43.620 s Normal in one live population and 70.746 s Fast in another; exact row count and wall time vary with live state. A dedicated current-v17 proof also completes one real block then returns `idle` after one Stop. Production emits `bridge://map-scan-status`, periodic `map_summary` follows the active Manual run, and Stop waits for terminal ownership release. Evidence: `2026-09-16-r7-map-scan-speed-stop.json`.

**Next:** Resource complete-run acceptance on this same shared engine, then Truck/Railway/Dispatch/Ghost/Treasure, mixed selections and all eight. Do not add a second scanner.

## Current superseding prerequisite - LWB-OVR-015, 2026-09-16

The owner paused Map Data performance work to repair the Home prerequisite after Last War advanced its Lua content package from v16 to v17. `LWB-OVR-015` now TECHNICALLY LIVE-PROVES both production lifecycle paths on current v17: manual `profile_instance_start` reached connected/ready then exact owned Close/restoration, and startup `profile_instances_reconcile({autoLaunchAll:true})` independently reached connected/ready then exact Close/restoration. The old exact package hash pin is replaced by a fail-closed critical-anchor compatibility policy, and a future incompatible update surfaces `GAME_UPDATE_UNSUPPORTED` instead of the generic action-failed message. Evidence: `2026-09-16-home-v17-auto-compat.json`.

**Superseded by R7-012:** the owner resumed Map Data, so the performance/progress/Stop work is no longer paused. The Home v17 lifecycle evidence remains valid.

## Current superseding checkpoint - LWB-R7-011, 2026-09-16

`LWB-R7-004/005/006` supersede the older one-view/resource sequencing below for current work. Ordinary production `map_scan_start/status/stop` routes through `ManualMapScanCommandService` and the shared `MapScanEngine`; current-v16 live context, arbitrary-target AOI-backed Resource and Player City blocks, proven zero-city current-view cells, Player City `_curViewIndex` footprint reuse with one fresh response covering all four planned LOD0 cells, one real Normal engine block, and bounded Stop-to-idle are LIVE-PROVEN. Normal/Fast share the service with recovered concurrency `8/20`; camera acquisition remains intentionally single-owner/sequential because original `XluaBridgeMapScanTick` pacing is unrecovered.

`LWB-R7-008` supersedes that integration target: ordinary `ManualMapScanCommandService`/`MapScanEngine` now LIVE-PROVES a complete Player City run over all 10,000 AOIs / 2,500 logical blocks, zero failed blocks, transactional publication and exact DB reopen. The production run published/reopened 2,592 Cities in 51.524 s scan wall time; a later dedicated comparator independently covered 10,000/10,000 AOIs in 250 requests and published/reopened 2,254 Cities. Exact City count is not a stable invariant across sequential live scans. The next acceptance work is truthful incremental progress plus Resource/remaining kinds on this same engine; native acknowledgement/removal/drain semantics remain open. Evidence: `2026-09-16-r7-manual-full-city-proof.json`.

`LWB-R7-009` then exposes truthful native AOI-union progress and fixes owner selection polling plus the coordinate Jump backend. `LWB-R7-010` now LIVE-PROVES the same ordinary Manual Start path for **Monster**: 10,000/10,000 AOIs, 2,500/2,500 logical blocks, zero failed blocks, 6,934 game-classified monsters/bosses published and exactly 6,934 after DB reopen in 71.161 s. Monster acquisition uses `WorldScene.MarchDataManager.GetAllMarchesByCS`, game-owned `IsMonsterOrBoss`/`IsMonster`/`IsBoss` classification, and `DataCenter.MonsterTemplateManager.TryGetMonsterTemplate(monsterId)` for config/name-key/level/type metadata. It is generic and not limited to Zombie Invasion. Evidence: `2026-09-16-r7-manual-full-monster-proof.json`.

`LWB-R7-011` closes the downstream readable-name blocker at IMPLEMENTED/OFFLINE-TESTED scope. The original `lastwar_localize` path accepts at most 200 keys and resolves requested locale -> English -> key; all nine reference locale blobs are verified from the embedded manifest and production now serves them from a fail-closed rebuild cache outside the repository. Real-cache backend proof passes for English/`zh-CN`, and isolated Map Data browser proof shows Monster names re-localize after UI language change. Immediate owner-requested work returns to full-world scan performance toward roughly one minute while preserving exact AOI coverage/publication; Resource remains the next category acceptance after that performance pass. Evidence: `2026-09-16-r7-monster-localization.json`.

## Current owner priority — Player City active, 2026-09-13

**PM17 correction delivery is complete.** `LWB-PM17-001/002` are IMPLEMENTED/OFFLINE-TESTED at code revision `f24fef39bbe41a8655595da2c9c9da1bcb9e9811`; local/origin/remote matched and GitHub Actions `34711920482` completed SUCCESS on that exact SHA. The corrected-build normal Overview Launch -> exact in-game message -> Close/restoration regression remains prepared but NOT RUN, so historical live results are not promoted to this build.

**Active assignment: Map Data, Player City first.** Work one end-to-end path: fresh real city acquisition -> correct profile/server persistence -> normal Search/display -> a distinct newer acquisition/result -> same-profile reopen. Reuse existing recovered contracts and current-client evidence, fix the first broken link, and capture source/session/store/query/render evidence automatically.

**S03 complete Refresh Status and S06 cross-server travel remain PENDING/DEFERRED.** S02 remains unfinished/unassigned. Do not expand those areas unless a minimal evidence-backed dependency is directly required by Player City, and do not claim the whole Overview or Map Data feature complete.

### Retained earlier resource/PM15 checkpoint — not the active assignment


Read [AGENTS.md](../AGENTS.md), [task.md](../task.md), [BACKLOG.md](../BACKLOG.md), [current audit](lwbridge-project-status.md), [first-live-result contract](first-live-result.md) and the relevant evidence. Audit base: `7ca6d5c`; inspect actual HEAD/worktree before editing. Preserve concurrent work. All 47 acceptance cases remain required.

## Current assignment - Player City end-to-end

`LWB-PM17-001/002` are delivered at `f24fef39bbe41a8655595da2c9c9da1bcb9e9811` with CI `34711920482` SUCCESS. The corrected-build normal Overview regression remains prepared but unperformed. Work directly on Player City; do not return to the old PM15/resource queue or wait for another permission gate.

## Owner clarification — current, 2026-09-11

Web now owns implementation and all technical verification/capture. There is no separate Sol task. The owner can only follow plain UI steps, describe visible outcomes and provide screenshots. PM audits rather than implementing or testing.

**Delivered foundation:** automatic collection and the beginner guide exist. The two review-15 collector fixes are now delivered offline and await PM audit. Read [team-workflow.md](team-workflow.md), fill [live-test-handoff.md](live-test-handoff.md) and [user-test-checklist.md](user-test-checklist.md). Never ask the owner to obtain command output, inspect JSON/SQLite, calculate hashes or debug recovery. Web implements the scripts, validates actual collection and handles interpretation.

**Current delivery:** the owner completed the first permitted passive check against reviewed build `2d3915d` in attempt `20260911T085114Z-eb1cd35e-7f21ad24`. The selected profile had no published map server and the normal UI showed `MAP_SAVED_CONTEXT_UNAVAILABLE`; no scan/game action ran. `LWB-PM13-009` fixes the package defect exposed by that attempt: the frontend deliberately does not send `map_search` when no saved server exists, so the old collector incorrectly returned `INCOMPLETE`. The repaired collector records the normal `map_summary` error plus empty published-server state as `COMPLETE_NO_SAVED_CONTEXT`, and the guide now tells the owner to screenshot/close before Search on that branch. **Do not ask the owner to repeat the current check.** Fresh acquisition remains SB-97 BLOCKED/NOT_RUN and is not part of this repair.

The diagnosis at `537a5b9` is complete within saved-log limits. Navigation worked; an observation was rejected before local execution and no fresh Start was sent. Do not repeat closed work, guess a code defect or use an owner-run collector to reroute the restriction.

## One active function

**Resource scan -> storage -> normal Resource Search/display -> second fresh refresh -> reopen.** This function remains partial. Do not switch to monster research, export, auto-update, broad original-pipe parity or unrelated fixes while it remains active. The next function is monster acquisition plus search, after resource acceptance; monster search alone cannot create missing game records.

Accepted: bounded current-client acquisition, PM12-A restoration/ownership, PM12-C immutable result/session correlation, source-backed idle for the observed point, and PM12-B bounded-route lifecycle closure after `LWB-PM13-003`. Do not repeat those recoveries. PM12-D normal-window completion remains open. Read the audit before treating contributor checkbox claims as full acceptance.

Post-review checkpoints `LWB-PM13-001` and `LWB-PM13-001B` close PM13-01 saved browsing. The exact review-13 config/database was rediscovered in the Codex package-local cache; byte-identical persisted bytes were exercised through two ordinary app sessions, the Resource tab and explicit Search after restart, then the temporary direct-desktop copy was removed to restore prestate. `LWB-PM13-002` closes PM13-01b feedback, `LWB-PM13-003` closes PM13-03, and `LWB-PM13-004` closes PM13-02 at IMPLEMENTED/OFFLINE-TESTED scope. `LWB-PM13-006` corrects the real coordinate-button render shape and adds a repeatable ordinary-window saved-reopen verifier. The proof now binds each acquisition to an explicit normal Resource Search request/result and exact rendered row. PM13-04 is the active final resource gate.

## Repair status — begin with PM13-04, not the closed items

1. **PM13-01 — COMPLETED:** the exact source-backed saved row is visible on ordinary Resource Search after restart. Saved context is labelled separately from live readiness; zero/multiple-server cases remain explicit, and no fixture/manual row/new scan substituted for the persisted database. Evidence: `LWB-PM13-001` / `LWB-PM13-001B`.
2. **PM13-01b — COMPLETED (IMPLEMENTED/OFFLINE-TESTED):** `LWB-PM13-002` preserves the structured `LIVE_RESOURCE_TYPES_UNSUPPORTED` error, provides localized Resource-only guidance in all nine recovered languages, retains the recovered no-records state, and separates missing-context from query-failure feedback. No category fallback or synthetic row was added. Connected normal-window revalidation remains PM13-04.
3. **PM13-03 — COMPLETED (IMPLEMENTED/OFFLINE-TESTED):** `LWB-PM13-003` validates/correlates into a prepared row before mutation, makes `reading -> committing` the atomic completion-versus-cancel decision, and reserves helper cleanup ownership before `Process.Start`. Deterministic Stop/Close-before-commit, Stop-after-commit-decision and Close-during-helper-registration cases pass with prior PM12 lifecycle regressions.
4. **PM13-02 — COMPLETED (IMPLEMENTED/OFFLINE-TESTED):** `LWB-PM13-004` explicitly clicks Resource Search after each fresh acquisition, records its native request ID/payload/result, revalidates the request-owned immutable result tuple, requires exact server/record/point/coordinate/level/updatedAt agreement, and then requires the rendered five-cell row to match the correlated acquisition timestamp. `LWB-PM13-006` fixes the real first-cell shape by extracting the coordinate span from the recovered coordinate button instead of comparing the full `481,32\nJump` td text, and provides the separate saved-reopen verifier. Empty/loading/unrelated-stale/same-point-stale cases remain rejected.
5. **PM13-04 — ACTIVE/BLOCKED FINAL GATE:** `LWB-PM13-005` safely revalidated the ordinary rebuilt window for unsupported all-category Start and missing saved context; both show the new specific guidance, keep empty state separate, start no game/helper process, and restore the direct desktop root to prestate. `LWB-PM13-006` proves the corrected saved-reopen verifier against the exact historical server-2212 row and leaves the direct root clean. The remaining required operation is now exactly a permitted real resource-only Start -> correlated row -> second newer Start/Search/render, followed by that verifier after app reopen. SB-97 still blocks replay of the prepared live persistent-window proof. `LWB-PM13-007` confirms the bundled Computer Use skill is installed locally, but the contributor's web GPT session did not expose the required `node_repl` bridge; do not substitute Remote Desktop Commander, ordinary Node, WebView automation or a helper protocol.

If a required operation is actually restricted, document its exact tool/reason/target and the permitted alternative or required external change. Keep the function ACTIVE/BLOCKED and continue only its direct permitted dependencies. Do not endlessly generate unrelated research checkpoints, self-approve an escalation, or reroute a denied action through another executor/model. Existing user permissions do not need renewal.

## Current Overview continuation — 2026-09-12

O04/O05 remain technically live-proven/owner-visible accepted under `LWB-OVR-010/011`. `LWB-OVR-012/014` retain the recovered repair contract and successful live repair path. Review 16 reopened O01/O06 edge closure; PM16-02 and PM16-01 are now corrected/offline-tested as `LWB-PM16-001` and `LWB-PM16-002`, and `LWB-PM16-003` reconciles their evidence/status. **Historical review-16 return note:** S02/S03/S06 remained open at that checkpoint. The owner has since explicitly resumed scope and PM17 is delivered; Player City is now active while S03/S06 stay deferred.

## Readiness decision and test plan

Read [the resource test checklist](user-test-checklist.md). The implementation is ready as a controlled-test candidate, not a proven live release. The standard AI may test Computer Use availability, but must deliver the resource result or an exact unresolved execution blocker. Check the current installed client before acquisition. SB-97 remains a distinct restriction; neither tool discovery nor PM approval clears it. Never ask the owner to execute the rejected harness as a workaround. An independently permitted live method or an applicable environment change is required.

## Computer Use capability correction

On review 13 the PM successfully used the installed `computer-use` skill via `mcp__node_repl__js` and `@oai/sky` to inspect and click the real Windows app. Read the installed SKILL.md and guidance/API first; discover whether that tool is callable in **your** environment. Browser-only `mcp__cua_repl` native limitations do not establish that all installed native capabilities are absent. If sky is unavailable, record the exact capability gap and use permitted offline checks/prepare the UI handoff. Do not invoke the native helper executable or custom helper protocol as a workaround. SB-97's rejected live proof remains a separate restriction and is not cleared by native capability discovery.

## Resource function exit criteria

- Existing source-backed saved row survives app restart and displays with correct profile/server/coordinates/level/time and truthful saved status.
- Unsupported selection and unavailable context give clear, accurate UI feedback; no synthetic data appears in production.
- Stop/timeout/Close/duplicate Start tests cover late commit and cleanup ownership, not only request cancellation.
- A permitted fresh Start and a second fresh read reach the normal table with durable source/request/result/query/render correlation. The second read may return the same point; its acquisition time and query/render evidence must be newer.
- Required cleanup/integrity checks pass; missing names and unobserved Gathering remain explicit limitations. No full-map or all-category claim follows from this bounded result.
- Documentation, focused checks, coherent commit, push and remote verification complete. Only then submit the evidence to PM for acceptance and selection of the next user function. An external blocker is recorded, not counted as passing this gate.

## Next function, queued only: monster scan and search

Recover current-client monster acquisition/type/identity/field mappings and original LWBridge normalization/query semantics. Trace Start -> real response -> monster records -> persisted index -> normal Monster Search. Distinguish an empty scanned area from absent acquisition and from filter/context errors. Prove a real monster appears, matching filters find it, nonmatching filters exclude it, refresh updates it, and scope is maintained; never manufacture a mob or claim an empty result proves acquisition. Stay on this function until its stated scope works or an exact external blocker is recorded. Regular AI owns it; Daybreak only receives a complete reviewed ESC packet for a specific unresolved question.

## Checkpoints and reporting

Run checks appropriate to changes. Sequence builds sharing output. Baseline: Release desktop build; desktop checks with `--verify-real-config-unchanged`; three PM12 isolated recovery/session/scoped-close regressions; frontend `--check`; preference and both transport checks; browser verifier using the existing Playwright installation and Edge channel. The historical audit reproducer is retained as a known-failure demonstration. The replacement proof checks now reject empty/loading/stale results; do not reopen the fixed predicate without a new failure.

At each checkpoint report: current function; what the user can actually do; what changed; live versus offline evidence; precise remaining failure; next action within this same function; commit and verified remote. Save confirmed findings immediately. Commit/push coherent checkpoints without a fresh PM permission gate; a research checkpoint does not close the function.

## Role-specific prompts and continuation

Use [team-workflow.md](team-workflow.md) as the single current prompt source: Web prepares verified automatic collection and the owner guide, the owner supplies permitted UI observations/screenshots, Web diagnoses/fixes and PM audits. [live-test-handoff.md](live-test-handoff.md) records the current owner, build identity, pending step and results so disconnects do not erase progress. No Daybreak task is currently assigned.
