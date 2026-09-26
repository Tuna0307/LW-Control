# Current project status — strict one-to-one recovery

**Date:** 2026-09-27
**Current checkpoint:** `LWB-R8-077`

## Executive status

The project is not complete.

Previous R7 status pages measured whether the reconstructed Home/Map product worked at its chosen scope. On 2026-09-24 the owner reset the goal to exact LWBridge 0.3.1 parity across the retained program. On 2026-09-26 the owner also lifted the earlier blanket auth/entitlement exclusion for dependency recovery: those internals may now be recovered when retained features require them, without inventing credentials, roles, capacity or synthetic premium/admin state. Standalone Login/Register/account-management product UI remains non-priority unless the retained-state pipeline requires it.

The old acceptance matrix remains useful implementation evidence but is no longer completion authority.

## R8-065 fresh current-client acceptance

The owner now requires a stricter operational baseline: a feature is not called working merely because its contract is recovered, its UI exists, or offline checks pass. It must be freshly verified against the real installed game.

R8-065 re-established that baseline on current-v21. Home manual launch/connect/close and startup auto-launch/connect/close both reached `connectionState="connected"` in the dedicated live lifecycle proof. Map initially failed live on a current-v21 3×9 AOI contraction; after a bounded conservative compatibility fix, same-session all-eight Normal and Fast scans both completed 2500/2500 with 0 failed and 0 unread, and published Fast counts survived database reopen. The wide-FOV shortcut remains removed.

This proves the retained Home lifecycle and core Manual Map scan are **LIVE-WORKING / EQUIVALENT_REIMPLEMENTATION** today. It does not prove original Map acquisition parity or whole-program completion. See `docs/reviews/2026-09-26-r8-065-live-home-map-v21.md`.

## R8-066 production UI acceptance

R8-066 then proved the normal Release desktop application's actual user-facing Map path rather than a service-only harness: normal persistent profile, Home lifecycle, recovered Map Data WebView, production Manual scanner, native `map_search`, and rendered Resource table. The final current-v21 run reached `connected`, completed Fast Resource scanning at 2500/2500 with 0 failed and 0 unread, returned 515 Resource rows, correlated the first native row to the rendered six-cell table, exited 0, and left neither Last War nor LWBridge running.

The stronger gate also corrected stale proof infrastructure without changing the product scanner: nullable numeric status readers now respect explicit JSON nulls, startup observation uses the exact R8-042 native `profile_instance_status` projection rather than racing reconcile, and Resource render correlation accepts the current six-cell shape while preserving legacy five-cell coverage.

This upgrades the operational evidence for Map from backend/service execution to the real desktop/WebView path. It still does not close original acquisition parity. See `docs/reviews/2026-09-26-r8-066-production-map-ui-live.md`.

## R8-077 Map acknowledgement host boundary

R8-077 narrows the remaining acknowledgement/drain problem without changing production code. In the original `map.scan.complete` host path, `pendingPoints`, `pendingMarches`, `pendingPointRemovals`, `pendingMarchRemovals` and `pendingAcks` are count fields that are summed into the reported `nativePendingRecords` metric.

The aggregate is serialized into scan state and then its working registers are overwritten before the terminal failure/publication branch. The recovered host decision path does not parse `acks[]` items and does not use `nativePendingRecords > 0` as a terminal admission predicate. Positive `dropped` remains a separate explicit failure/cleanup input.

Therefore acknowledgement item schema, acknowledgement consumption and any acknowledgement-to-block completion/retry linkage are owned below the recovered host event layer, before or while protected game-side code emits `map.scan.progress` / `map.scan.complete`. The next original-Map target is now the producer-side tick/drain boundary, not host publication logic.

See `docs/reviews/2026-09-27-r8-077-map-ack-host-boundary.md` and `evidence/lwbridge-implementation/2026-09-27-r8-077-map-ack-host-boundary.json`.

## R8-076 original Map acquisition ingestion

R8-076 narrows one of the largest remaining parity gaps. Original Map acquisition is no longer wholly UNKNOWN: the host-side production ingestion architecture is now source-linked.

The common native Map event handler distinguishes `map.records`, `map.scan.diagnostic`, `map.native.capture`, `map.scan.progress`, `map.scan.complete`, and `map.scan.error`. Direct-scan `map.records` batches carry event `scanRunId` and `records`; each accepted record is a `{type,payload}` entry filtered against the original eight kinds/current selection, augmented with scan-state server/dimensions, normalized by the shared record builder, and transactionally upserted into run-scoped `scan_records`.

The separate `map.native.capture` event uses the same normalized builder but writes published `map_records`, proving that passive native-hook updates and direct full-scan staging are distinct original paths.

The protected remaining work is game-side block traversal/order/coordinates, `XluaBridgeMapScanTick` registration/scheduling/pacing, native point/march/removal/ack queue-to-batch drain/retry semantics, and remaining per-kind serializer details. The current v21 movement/AOI scanner remains LIVE-WORKING EQUIVALENT_REIMPLEMENTATION and is not replaced by guesses.

See `docs/reviews/2026-09-26-r8-076-original-map-acquisition-ingestion.md` and `evidence/lwbridge-implementation/2026-09-26-r8-076-original-map-acquisition-ingestion.json`.

## R8-075 host↔proxy protocol re-audit

R8-075 narrows the previously broad host/proxy protocol gap. The source-backed host wire now includes exact pipe identity/framing, `hello` and `hello.ack`, PID/path/build/token authentication, protected listener/accept-loop semantics, `command/call`, first `cmd_1`, `result`, `payload.id` correlation, queue/byte limits, principal timeouts, reconnect generation and terminal route cleanup. R7-127 already live-proves the production authenticated named-pipe route and correlated read-only `getStatus` response against the real game.

Public readiness semantics are also split exactly: R8-043 proves `get_status.backend/xluaOnline` are route-presence state, while R8-042 proves retained `profile_instance_status.connectionState=connected` additionally requires identity confirmation and a heartbeat fresher than 15,001 ms.

The exact protocol backlog is therefore reduced to three items: wire-`heartbeat` payload/native activity ownership; disconnect/write-failure to outstanding result-channel/public-call completion mapping; and original secure/plain xLua script/provider dispatch. The current GamePipeAdapter/mailbox/Lua command handler remains live-working equivalent plumbing, not original proxy parity.

See `docs/reviews/2026-09-26-r8-075-host-proxy-protocol-reaudit.md` and `evidence/lwbridge-implementation/2026-09-26-r8-075-host-proxy-protocol-reaudit.json`.

## R8-074 retained frontend routing inventory closed

R8-074 closes the final two genuinely-unclosed retained frontend contracts: `map_dispatch_share_alliance` and `map_treasure_claim`.

Dispatch Share now has exact authorization-first admission, complete row validation, sequential 5-second `shareDispatchTaskToAlliance` calls, exact `shared=true` success predicate and aggregate result. Treasure Claim now has exact authorization-first admission, server/scope/default validation, 5-second `getCurrentServerId` preflight, dedicated candidate-query ownership, 5-second `claimTreasures` request, ten immediate counters and the frontend's 1-second / 1800-iteration polling contract.

No live routes are added because both state-changing actions require owner-excluded authorization-state admission before protected provider execution. The retained frontend routing inventory is now fully classified: **61 specifically routed, 33 audited/fenced, 0 genuinely unclosed**.

This does not mean the project is complete. Native-only commands, host/proxy protocol, script/proxy internals, original Map acquisition/action internals, reconstruction drift and final one-to-one validation remain open.

See `docs/reviews/2026-09-26-r8-074-final-retained-frontend-routing-close.md` and `evidence/lwbridge-implementation/2026-09-26-r8-074-final-retained-frontend-routing-close.json`.

## R8-073 provider-backed Automation/Squad fences

R8-073 closes `equipment_preset_apply`, `resource_automation_run`, and `trade_station_configure` far enough to move them from genuinely unclosed to audited/fenced.

Equipment Apply now has exact preset admission, live `getSquads` and `applyHeroEquipment` 5-second calls, `bridge://equipment-apply-progress`, and provider-result boundaries; the remaining multi-hero planner/aggregation is protected. Resource Run now has exact two-task mapping, unknown-task/state-unavailable/busy behavior, dedicated 10-second runner boundary and status-event ownership, but its live request contains owner-excluded authorization-derived `premium/admin`. Trade Configure now has exact retained fields, enabling validation, shared `trade_station`/`config.json` ownership and 5-second `configureTradeStation` calls.

No runtime routes are added because reproducing any of these exactly would still require excluded authorization state and/or unrecovered live planner/shared-config semantics. The 33 unrouted retained frontend commands now split into 31 audited/fenced and 2 genuinely unclosed: `map_dispatch_share_alliance` and `map_treasure_claim`.

See `docs/reviews/2026-09-26-r8-073-final-provider-actions-fence.md` and `evidence/lwbridge-implementation/2026-09-26-r8-073-final-provider-actions-fence.json`.

## R8-072 VIP18 Base Apply / Restore fence

R8-072 closes `vip18_base_apply` and `vip18_base_restore` at the retained host/provider/config boundary. Apply validates `skinId`, calls `setLocalVip18BaseSkin({skinId})` at 10 seconds, then patches `selectedSkinId`. Restore calls `restoreLocalBaseSkin({})` at 10 seconds, then patches `selectedSkinId=null` and `autoApplyOnStart=false`.

Both commands perform the game action before shared VIP18 config reconciliation. A later config-state failure can therefore occur after the in-game change; full success returns the provider JSON through the shared generic converter.

No runtime route is added because exact execution still needs owner-excluded authorization-state admission and the unrecovered shared `config.json` migration/merge/write owner. These two commands move to audited/fenced, leaving 5 genuinely-unclosed retained frontend routing gaps.

See `docs/reviews/2026-09-26-r8-072-vip18-base-actions-fence.md` and `evidence/lwbridge-implementation/2026-09-26-r8-072-vip18-base-actions-fence.json`.

## R8-071 Dispatch Assist action fence

R8-071 closes the recoverable host/database/live-helper boundaries for `dispatch_assist_schedule`, `dispatch_assist_cancel`, and `dispatch_assist_retry`. Native `getAllianceDispatchTasks` and `armAllianceDispatchAssist` helpers use 5-second deadlines; Cancel uses `cancelAllianceDispatchAssist` at 5 seconds. The persisted `dispatch_assist_jobs` read/insert/retry/cancel/delete helpers and `bridge://dispatch-assist-changed` publication are now mapped.

Schedule’s 1–200 UUID validation, missing-live-task and already-scheduled failures are recovered; Retry’s missing-job/task-unavailable/not-retryable failures are recovered; Cancel’s scheduled-job-not-found boundary is recovered.

No runtime route is added. Schedule/Retry still require the unrecovered live task projection already fenced in R8-045, and all three retain mandatory owner-excluded authorization-state admission. These three commands move to audited/fenced, leaving 7 genuinely-unclosed retained frontend routing gaps.

See `docs/reviews/2026-09-26-r8-071-dispatch-assist-actions-fence.md` and `evidence/lwbridge-implementation/2026-09-26-r8-071-dispatch-assist-actions-fence.json`.

## R8-070 City Layout live-command fence

R8-070 corrects the older “implementation-ready” classification for `city_layout_validate`, `city_layout_apply_start`, and `city_layout_apply_cancel`. Fresh native xrefs prove all three first await the shared owner-excluded authorization-state future, then resolve the selected runtime and invoke their protected game providers.

The recoverable contracts are now exact at the host boundary: Validate sends `{baseRevision,placements}` to `validateCityLayout` at 10 seconds; Apply Start performs the fresh-snapshot/`isInCity` gate before `startCityLayoutApply` at 10 seconds; Cancel sends `{jobId}` to `cancelCityLayoutApply` at 5 seconds. Normal results use the shared generic JSON converter.

No runtime route is added because bypassing mandatory authorization-state admission would change native public behavior. These three commands move to audited/fenced, leaving 10 genuinely-unclosed retained frontend routing gaps.

See `docs/reviews/2026-09-26-r8-070-city-layout-live-fence.md` and `evidence/lwbridge-implementation/2026-09-26-r8-070-city-layout-live-fence.json`.

## R8-069 Chat Automation fence

R8-069 closes the retained native/public boundaries of `chat_automation_configure` and `chat_automation_run_pending`. Retained kinds, host-side Configure validation, provider methods, exact 5-second deadlines and generic result conversion are recovered. Both handlers still require the shared authorization-state future before selected-runtime/game-route admission.

No `premium/admin` provider fields are observed here; the fence is the authorization-state admission itself. Skipping it changes native error precedence and recreating it crosses the explicit owner exclusion. No runtime route is added.

The 33 unrouted retained frontend commands now split into 20 audited/fenced and 13 genuinely unclosed.

See `docs/reviews/2026-09-26-r8-069-chat-automation-fence.md`.

## R8-068 generic Automation live-command fence

R8-068 closes the recoverable native/public contract for `automation_configure`, `automation_start`, and `automation_stop`. Their exact handlers, immutable frontend payload shapes, shared authorization/profile/runtime/game-route admission, provider methods, 5-second deadlines, timeout behavior and generic provider-result conversion are now identified.

Configure additionally runs the native task-config validator and normalization path before its provider call; the task-specific validation vocabulary is substantially recovered. The live request boundary remains intentionally fenced because native request construction includes authorization-derived `premium` / `admin` material from the same owner-excluded authorization-state surface previously identified by R8-051. Omitting or hard-coding that material would not be one-to-one behavior.

No runtime route is added. The three commands move from the genuinely-unclosed bucket to audited/fenced. The 33 unrouted retained frontend commands now split into 18 audited/fenced and 15 genuinely unclosed.

See `docs/reviews/2026-09-26-r8-068-generic-automation-live-fence.md` and `evidence/lwbridge-implementation/2026-09-26-r8-068-generic-automation-live-fence.json`.

## R8-067 command inventory checkpoint

R8-067 closes the exact recovered frontend command/event inventory and turns the production routing gap into a counted queue. The authoritative reference API asset contains 104 unique command literals; 10 are explicitly excluded Account/Login/Authentication or account-purpose activation/entitlement commands, leaving 94 retained frontend commands. Across the current production backend, host-special branches and composed command services, 61 of those 94 have a specific route and 33 do not.

The 33 unrouted retained commands split into 15 whose native boundary has already been materially recovered and deliberately fenced, and 18 genuinely unclosed routing gaps. The 18 are now the concrete retained command queue rather than an unspecified whole-program backend gap.

The frontend inventory is not the complete native inventory. Existing exact native checkpoints prove at least eight original handlers have no matching literal anywhere in the recovered frontend assets, so the backlog's complete command/service inventory remains open. Raw executable string splitting was tested and rejected as authority because packed/adjacent strings merge command names with neighboring text.

See `docs/reviews/2026-09-26-r8-067-reference-command-inventory.md` and `evidence/lwbridge-implementation/2026-09-26-r8-067-reference-command-inventory.json`.

## Reference authority

`C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.1.exe`

Verified SHA-256:

`2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

## Strongest parity already achieved

The recovered frontend is the closest portion to true one-to-one recovery. Original React/Vite chunks, stylesheet, icons and nine locale bundles were extracted, and prior visual comparison showed 31/32 tested feature-region pairs pixel-identical with the remaining pair negligible.

That proof is limited because the rebuild intentionally transformed the main/API boundary and removed/changed some product surfaces.

## Largest parity gaps

1. Full plaintext/original handler recovery from `bridge-scripts.dat`.
2. Remaining exact host/proxy parity: wire-heartbeat payload/activity ownership, disconnect/write-failure to outstanding-call completion mapping, and original secure/plain xLua script/provider dispatch. R8-075 confirms the rest of the retained host wire/session contract is recovered and the read-only production route is live-working.
3. Remaining original Map Scan internals: game-side traversal/order/coordinates, `XluaBridgeMapScanTick` scheduling/pacing, native queue→batch/ack drain and per-kind serializer details. R8-076 closes the host event/normalization/staging architecture and moves the lane to PARTIAL EXACT_CONTRACT.
4. Removal of rebuild-only additions such as Secret Task Quick Find.
5. Restoration of remaining retained product features previously retired/customized. City Excel is restored in R8-007, Map Clear in R8-008, `server_jump` in R8-009, `map_summary` in R8-010, `map_data_options` in R8-011, Manual Scan public contract in R8-012, status/Stop lifecycle in R8-013, `map_search` public kind/filter ownership in R8-014, the original Auto Scan frontend scheduler/control plane in R8-015, and Scheduled Plunder list/schedule/cancel persistence/events/original UI in R8-016. R8-017 removes speculative Monster-distance, City-shield and Railway-quality sort implementations and gates those branches pending stronger evidence. R8-044 restores the native `game_asset_image` public request/result/error contract, one 15-second game call, PNG-signature validation and persistent profile-runtime `asset-cache` behavior; the exact native SHA-256 cache-key preimage and original transport/file-I/O plumbing remain unclaimed. Complete native multi-sort assembly and protected Plunder execution remain partial/unknown. R8-054 re-audits the existing `lastwar_localize` path: requested-locale→English→key fallback, the exact 200-key cap and locale-cache error vocabulary remain evidence-backed, but the command is only partial/equivalent because the rebuild bypasses native owner-excluded authorization-state admission and has stricter outer payload parsing. R8-055 additionally audits `map_treasure_claim_status`: native awaits owner-excluded authorization state, calls `getTreasureClaimStatus` once with a 5-second deadline, transactionally maintains `treasure_claim_states`, then returns the original provider JSON. The current rebuild status route remains a documented deviation because it adds scan/operation/server guards, uses an 8-second inspection path, rebuilds a fixed DTO, and cannot preserve provider extras such as optional `batch`. R8-074 now closes the original `map_treasure_claim` host contract but keeps live claim/scout execution fenced on owner-excluded authorization state plus unrecovered protected executor internals. Account/Login/Authentication is intentionally excluded.
6. Whole-program backend parity for Automation, Squads/AFK, City Layout, Hotkeys, Mini-games and Settings. R8-018 restores City Layout drafts; R8-019 Hotkeys; R8-020 visual metrics; R8-021 updater idle status; R8-022 feedback-export contract/fence; R8-023 equipment config; R8-024 Monster AFK config; R8-025 Alliance Garrison config; R8-026 Resource Automation configure/persistence plus exact normal idle status/event behavior; R8-027 generic `automation_status` exact normal missing-file idle projection for all 18 native tasks and valid persisted-status passthrough; R8-028 `profile_settings_save` revisioned per-profile settings persistence for the current local profile; R8-029 normal retained single-profile `profile_list` controller-registry persistence/public field shape; R8-030 exact `profile_note_set` Unicode validation, note persistence, errors and refreshed profile-list result; R8-031 exact `profile_reorder` profile-ID/permutation validation and transactional display-order persistence; R8-032 fixed `profile_primary_set` guard plus native primary uniqueness invariant; R8-033 exact red-packet/treasure claim-delay validation and mirrored scheduler/chat runtime-config persistence; R8-034 exact `profile_select` selection guards, selected-profile persistence, and best-effort verified game-window focus; R8-035 exact `set_window_theme` DWM attributes/colors plus native semantic/failure errors; R8-036 exact server-jump history normalization/get/set/import migration plus per-profile `map-data.db/app_settings` ownership; R8-037 exact `append_log` runtime file path, line format, 2,000-scalar cap, CR/LF sanitation and best-effort write behavior; R8-038 exact `game_root_status` public four-field/candidate schema, source ordering, empty state, lightweight root predicate and `STATE_UNAVAILABLE`, with equivalent LocalConfig/process/registry plumbing and stricter launch admission kept separate; R8-039 exact `game_root_select` three-field cancel/invalid/valid results, path normalization, native lightweight validation, valid-only persistence and exact `STATE_UNAVAILABLE`/`INVALID_GAME_ROOT` vocabulary, with equivalent folder-dialog/LocalConfig plumbing; R8-040 exact native `proxy_status` full/reduced field projection, proxy target/original path layout, state precedence, secure/plain hash classification, runtime-managed projection, repair predicate and stable profile/runtime error codes, with read-only resource discovery as equivalent plumbing; R8-041 exact `game_recovery_status`/`bridge://game-recovery` 11-field order, scalar/null representation, idle defaults, numeric notice identity/visibility, terminal `succeeded`/`failed` publication and strict profile/runtime/`STATE_UNAVAILABLE` handling, with existing recovery process-control thresholds/actions unchanged; R8-042 restores exact `profile_instance_status` null-or-12-field public output and retained single-profile active connection-state projection while leaving start/stop/reconcile process control unchanged; R8-043 closes the exact native `get_status` seven-field top-level projection (`ok`, `backend`, `runtimeRoot`, `pending`, `xluaOnline`, `lastXluaActivity`, `config`), route-backed offline/named-pipe ownership, Unix-ms activity rule and `STATE_UNAVAILABLE`, but fences implementation because the full native config migration/normalization routine remains partial and the current rebuild status result is therefore still a documented deviation. R8-045 closes the native `dispatch_assist_state` six-field top-level schema, exact unavailable-state error and persisted-job selection/order, but leaves success unimplemented because the live alliance-dispatch provider/task-row projection is not yet recovered. R8-046 restores native `squad_list` profile/runtime/disconnect errors, one `getSquads` call with empty args, exact 5-second timeout and raw correlated JSON result forwarding through the retained authenticated transport. R8-047 restores native `monster_catalog_options` with the same strict profile/runtime admission, exact disconnect behavior, one `getMonsterCatalogOptions` call with empty args, exact 5-second timeout and raw correlated JSON forwarding. R8-048 restores native read-only `trade_station_catalog` profile/runtime/disconnect behavior, one `getTradeStationCatalog` call with empty args, exact 10-second timeout and raw correlated JSON forwarding while leaving `trade_station_configure` fenced. R8-049 fences the hidden VIP18 base read family after proving its public/cache/refresh behavior depends on two boundaries we must not fake: an excluded authorization-state-dependent path for the catalog-cache identity and the still-unrecovered shared config-state migration/normalizer for config get. R8-050 additionally closes the observable `city_layout_snapshot_get` host boundary—exact authorization-state unavailable error, game disconnect error, `getCityLayoutSnapshot`, 10-second deadline, and raw provider JSON forwarding—but leaves implementation fenced because native supplies an owner-excluded authorization-state-derived request argument. R8-051 closes the observable `automation_inspect` authorization-unavailable/disconnect/`inspectAutomationTask`/5-second boundary and identifies the special `allianceGarrison` result rewrite over `allies`/`uuid`/`serverId`/`uid`/`updatedAt`, but leaves implementation fenced because authorization-derived request material is owner-excluded and the exact rewrite is not yet closed. R8-052 closes the observable `city_layout_apply_status` authorization-unavailable/disconnect/`getCityLayoutApplyStatus`/5-second/generic-result boundary, but leaves runtime status fenced because native still requires owner-excluded authorization-state admission and the live apply provider/status schema is unrecovered. R8-053 closes `profile_settings_get` required `profileId`, the native singleton settings read, `PROFILE_DATA_INVALID` vocabulary and exact `{profileId,revision,value}` result, but leaves the public getter fenced because native first requires owner-excluded authorization-state admission. R8-056 closes `watermark_lookup` trace-code normalization, authorization/role admission, `/api/watermark/lookup` request ownership, generic service error vocabulary and raw JSON success return, while runtime remains fenced at the excluded authorization-state boundary. R8-057 closes native `update_status` beyond idle: exact nine-field read-only snapshots, seven phases (`idle`, `checking`, `upToDate`, `available`, `downloading`, `opening`, `error`), 60-second manual-check cooldown, manifest metadata, download progress/directory, opening at 100%, generic/fixed error transitions and `bridge://update-status` publication. The rebuild remains exact only at idle because `update_check` and `update_download_and_open` are still fenced. R8-058 closes the native main-window close request and global `app_exit_confirm` boundary: `app://close-requested` with numeric `instanceCount`, per-managed-runtime `bridge_exit`, exact game-close timeout, original-proxy restoration requirement, `APP_EXIT_FAILED`, and JSON-null success; implementation remains fenced because the rebuild still lacks the original proxy install/backup/restore mutation lifecycle. R8-059 closes `monopoly_cell_open` selected-profile injection, authorization/profile/runtime/disconnect admission order, one `openMonopolyCell` `{}` call at 5 seconds, shared Lua-call failure mapping and raw provider JSON return, while runtime remains fenced because native authorization-state admission is owner-excluded. R8-060 closes `construction_rewards_claim` under the same retained wrapper/admission pattern with one exact `claimConstructionRewards` `{}` call at 5 seconds and raw provider JSON return, while the live claim remains fenced on owner-excluded authorization state. R8-061 closes hidden `vip18_base_config_save` field validation, exact invalid skin/auto-apply errors, `vip18_profiles`/`config.json` persistence ownership, config-state unavailable behavior and normalized three-field success projection; runtime save remains fenced on owner-excluded authorization admission plus the unrecovered shared config-state migration/normalizer. R8-062 closes native `profile_delete` authorization-first admission, running/not-found/primary guards, the fact that public delete disables the generic bound-profile guard, transactional registry/selection mutation, post-commit profile-data cleanup with `IO_ERROR`, runtime reconciliation, and refreshed profile-list success; destructive execution remains fenced on owner-excluded authorization state. R8-063 closes native `profile_enable_set` as capacity reconciliation: required `profileIds` array, capacity domain 1/2/5, primary/selection invariants, per-profile enabled/`license_capacity` locking, selected-profile repair and refreshed list result; execution remains fenced because capacity is owner-excluded authorization/license state. R8-064 closes native `profile_create` authorization-derived capacity rejection, 16-byte unpadded Base64URL ID generation, next-order `账号 N` naming, exact enabled/unlocked/non-primary row defaults, 13-field created-profile success and separate frontend `profile_select(created.id)` behavior; execution remains fenced because native `maxProfiles` is entitlement-derived and exact post-create runtime failure cleanup remains unclosed. City Layout validate/apply-start/cancel providers, Hotkey actions, live Squads/AFK/Resource actions, generic Automation configure/start/stop and Dispatch Assist live state/actions are now audited/fenced rather than unknown; remaining profile registry mutations and original proxy install/backup/restore plus launcher lifecycle, full feedback/updater execution behavior, exact log rotation/segment retention, and remaining Mini-game handlers remain incomplete.
7. Exact reference-vs-rebuild behavior validation across connected/live states.

## Map status under the new goal

The current Map acquisition implementation is still a reconstruction, not a recovered copy of the original protected traversal algorithm. R8-012 closes the Manual `map_scan_start` kind/mode/error/UI boundary; R8-013 closes the shared status/Stop lifecycle; R8-014 closes public `map_search` eight-kind/filter/query-builder ownership; R8-015 restores the original Auto Scan frontend scheduler/control state machine byte-for-byte; R8-016 restores Scheduled Plunder list/schedule/cancel persistence, events and original result-tab UI while protected action execution remains absent; R8-017 removes three unsupported alternate-sort guesses and makes Monster distance, City shield and Railway quality fail closed until their native expressions are recovered; R8-044 restores the read-only `game_asset_image` public boundary and native-style profile-runtime PNG cache, while the exact cache-key preimage remains unclaimed. R8-076 now closes the original host event/normalization/staging split between run-scoped `map.records` → `scan_records` and passive `map.native.capture` → `map_records`. Complete multi-sort details and protected traversal/tick/ack/travel/action internals are still partial/unknown.

The R7-151 wide-FOV work demonstrated why coverage metrics are insufficient: a scan could report full logical coverage while returning only a fraction of Player Cities. The old traversal still found roughly the expected full population.

Therefore no R7 performance optimization is accepted as original parity unless tied to reference evidence.

## Protected package status

Earlier R8 work recovered substantial package/crypto structure, but the remaining LWKE1 field map/AAD is evidence-limited and requires a genuinely new permitted artifact/source. Do not keep repeating the same searches, cross the protected boundary, or expand this lane into Account/Login/Authentication recovery.

The package lane is parked while the main researcher restores Map and other retained product surfaces. It may resume for retained non-account runtime compatibility only when new permitted evidence exists.

## Completion rule

The current whole-program parity matrix is `docs/lwbridge-parity-matrix.md`.

A final release requires every required retained original feature to be classified as exact or proven equivalent, no unexplained rebuild-only deviations in retained scope, and a working current-client product.
