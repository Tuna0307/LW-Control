# Current implementation handoff — strict parity phase

**Project:** Last War Bot / LW-Control
**Branch:** `research/offline-controller`
**Current checkpoint:** `LWB-R8-065`
**Date:** 2026-09-26

## Current directive

Stop designing our own LWBridge.

The verified `lwbridge-0.3.1.exe` is the product specification for every retained feature. Recover the retained original implementation and reproduce it one-for-one. Internal compatibility code may differ only when required for the current Last War client and only if the user-visible/output contract remains the same.

**Explicit owner exception:** do not research, restore, or implement Login, Register/account creation, authentication, account management, license activation/renewal, unbind, logout, auth-state/account UI, multi-license entitlement activation, credential persistence, or any other feature whose purpose is user login/account authentication. Those surfaces are intentionally out of scope even when present in 0.3.1.

Read `docs/strict-parity-recovery.md` and `docs/lwbridge-parity-matrix.md` before touching production code.

## Reference

Path:

`C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.1.exe`

SHA-256:

`2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

Reverified on 2026-09-24.

## What the previous phase accomplished

R1-R7 recovered the original frontend, substantial Rust/Tauri/launcher/proxy architecture, many Map contracts, SQL/query semantics, lifecycle behaviors, current-client Last War call surfaces, and a working Home/Map reconstruction.

That evidence is valuable.

However, the reconstruction also accumulated non-reference decisions: removed original features, added convenience behavior, current-game-specific scan strategies, and performance optimizations that were not first proven to be what LWBridge 0.3.1 did.

The old “0 ordinary partial rows” acceptance statement is therefore not one-to-one completion.

## R8-002 cleanup checkpoint

The pending R7-156 Map work was reconciled instead of left as 30+ GitHub Desktop changes. The known incomplete wide-FOV/68-request production shortcut and the rebuild-only Secret Task Quick Find surface are removed, the complete movement/AOI baseline is restored as a temporary correctness fallback, and the Clear resolver-gap fix is retained. The exploratory Dispatch/Ghost finder probes created during the abandoned redesign discussion were discarded.

This cleanup is not parity proof. The temporary scanner remains a reconstruction until the original LWBridge 0.3.1 Map implementation is recovered.

## R8-007 City Excel parity checkpoint

The original `map_city_export` surface has been restored end-to-end. The generator again preserves the original API wrapper, City-only UI/button/state and all nine locale labels; the desktop host owns the save dialog; backend export uses the recovered 200-row / 1000-page / 200,000-row contract; and the exact six-part XLSX writer/result envelope is regression-tested. See `docs/reviews/2026-09-24-r8-007-restore-city-excel-export.md`.

The secondary Map control-plane research is preserved at `docs/reviews/2026-09-24-r8-map-control-plane-exact-contract.md` and should be the starting authority for Scheduled Plunder, Auto Scan and the remaining Map control-plane corrections.

## R8-008 Map Clear parity checkpoint

The original `map_scan_clear` boundary is restored. Clear now requires a positive requested server equal to the current scan-state server with exact `serverIdSource=live`; active scans preserve the recovered `SCAN_RUNNING` precedence and all other server-gate failures return `SERVER_UNAVAILABLE`. Only that admitted server's `scan_runs` and `map_records` are deleted; player marks and other server datasets survive.

The frontend is also corrected back to the immutable 0.3.1 behavior: one Manual-only Clear control calling the current server ID. The R7-147 Auto Clear / `serverId=0` clear-all override is removed. See `docs/reviews/2026-09-24-r8-008-map-scan-clear-strict-parity.md`.

## R8-009 server_jump result parity checkpoint

The recovered public `server_jump` success envelope is now complete: `{changed, previousServerId, serverId}`. The current-client source already proved the destination server internally; R8-009 carries that validated destination through the service response instead of dropping it. Existing invalid-ID, busy-operation and timeout behavior remains unchanged. See `docs/reviews/2026-09-24-r8-009-server-jump-result-parity.md`.

## R8-010 map_summary parity checkpoint

`map_summary` now follows the recovered original boundary: shared scan state first, exactly eight original count kinds, run-scoped `scan_records` only while the matching scan is active, otherwise published `map_records`, and exactly `{serverId, counts, scanState}` on success. Rebuild-only `savedServerIds`, `saved_profile_index`, saved-server selection, first-live/live-probe fallback and `zombie_boss` counts are removed from this command. See `docs/reviews/2026-09-24-r8-010-map-summary-strict-parity.md`.

Persisted multi-server browsing and its frontend transforms remain separate Auto/result-browsing parity work; R8-010 does not delete that saved data.

## R8-011 map_data_options parity checkpoint

`map_data_options` now follows the recovered original source and envelope contract: exact top-level order `serverId, counts, alliances, names, dispatchLevels, noAllianceCount, rewardItems, treasureTypes, scanProgress`; exactly eight original count kinds; only Resource/Monster name families; active `scan_records` only for the matching reading server with raw nonempty `scanRunId`; otherwise server-scoped published `map_records`. Rebuild-only `zombie_boss` option/count families, public `monsterLevels`, and the R7 `serverId=0` all-published-server aggregate are removed. See `docs/reviews/2026-09-24-r8-011-map-data-options-strict-parity.md`.

## R8-012 Manual Scan public-contract checkpoint

Manual `map_scan_start` is restored to the recovered 0.3.1 public boundary: exactly eight selectable kinds (`city`, `resource`, `monster`, `truck`, `railway`, `dispatch`, `ghost`, `treasure`), original Manual Normal/Fast controls and `lwbridge.mapScanMode` persistence, `{selectedTypes,scanMode}` Start payload, and Normal=8/Fast=20. Rebuild-only public `zombie_boss` and feature-owned `targetServerId` are not part of the Manual request. R8-013 supersedes the earlier native-`retryCount` interpretation: `retryCount:2` is frontend fallback only.

The original executable also closes invalid-mode behavior exactly: missing/null/non-string `scanMode` defaults to Normal; only string values are validated; empty/unknown strings use `INVALID_SCAN_MODE / map scan mode must be normal or fast`. That string validation happens after earlier game/active-scan/world admission and before protected `startMapScan`, so an earlier admission error can win. See `docs/reviews/2026-09-24-r8-012-manual-scan-strict-parity.md`.

Current-client strategy IDs/acquisition remain compatibility internals and may not rewrite observable Manual mode/concurrency. Existing Zombie Boss acquisition/storage compatibility remains internal only; R8-014 removes Zombie Boss from the public `map_search` result-kind contract as well.

## R8-013 Map Scan Status / Stop shared-state checkpoint

`map_scan_status` now refreshes and returns the shared mutable scan-state object rather than a rebuild DTO. R8-013 removes native/public `retryCount`, `scanStrategy`, database-derived `createdAt`/`updatedAt`, restores the explicit `nativeCaptureReady` transition, and models successful completion as observable `publishing` followed by final `idle` with 100% progress.

Public Stop is idempotent when already idle and always publishes the recovered five-field cleanup (`isReading=false`, `phase=idle`, `inflightBlocks=0`, `lastError=null`, `resumeAvailable=false`) while preserving run/config/counter/native/start/world fields. The compatibility engine has no original protected scan session, so active Stop uses the evidenced predicate-false branch rather than inventing a game-side `stopMapScan` primitive. Clear remains a distinct broader reset and preserves mode/concurrency/native-ready.

## R8-014 `map_search` kind/filter checkpoint

Public `map_search` now accepts exactly the original eight kinds and the generated Map Data panel again emits the original kind-owned filters. Rebuild-only public Zombie Boss, `monsterNameKeys`, Monster/Resource level selectors and Resource idle/full/black-tile filters are removed. Monster keyword search again uses the recovered generic name/alliance/UUID/`data_json` predicate. Resource truth extras are inert rather than assigned invented semantics; Resource/Monster level bounds fail closed because the recovered named level fields belong to Dispatch.

This checkpoint does **not** promote all alternate-sort internals to exact parity; those remain explicitly partial in the parity matrix. See `docs/reviews/2026-09-24-r8-014-map-search-filter-parity.md`.

## R8-015 Auto Scan scheduler checkpoint

The final generated Auto config/sanitizer helpers, scheduler-owned state, scheduler effect, and Auto card are restored directly from immutable 0.3.1 bytes. Auto again persists its own Normal/Fast mode (default Fast), Run Now requires Auto enabled and sets `nextRunAt=Date.now()`, the scheduler effect is keyed by profile plus connected state, targets run sequentially, thrown target failures abort the remaining cycle through the original outer catch, and finalization preserves the original return-to-server/deadline behavior. R7 `runOnceRequestedAt`, reconnect-stable ownership, restart markers/recovery, per-target exception isolation, synthetic cycle summary, and dedicated Auto Stop are removed. See `docs/reviews/2026-09-24-r8-015-auto-scan-scheduler-parity.md`.

## R8-016 Scheduled Plunder control-plane checkpoint

Scheduled Plunder is restored as the original ninth result tab, not a scan kind. The rebuild now exposes all five recovered list/schedule/cancel commands, the two profile-scoped change events, and the three durable job/history tables with recovered ordering, validation, upsert/cancel and archive behavior. The Scheduled Plunder API wrapper block, tab/status block and table/component block are byte-identical to immutable 0.3.1, and all nine locale bundles retain the original labels. `DispatchPlunderWorker.cs`, `TruckPlunderWorker.cs`, the old worker-heavy `MapDataStore.Plunder.cs`, and protected action runtimes remain absent. See `docs/reviews/2026-09-25-r8-016-scheduled-plunder-control-plane-parity.md`.

## R8-017 map_search sort-gating checkpoint

R8-017 removes three R7-era backend sort guesses that the newer R8 binary review still classifies as partial: Monster `distance`, City `shield`, and Railway `quality`. Their original 0.3.1 frontend controls remain untouched, but any query containing one now normalizes as original vocabulary and then fails closed with `MAP_QUERY_UNRECOVERED` before SQL assembly. Neighboring evidenced expressions remain admitted. This checkpoint does not promote the complete native multi-sort/null assembly to exact parity. See `docs/reviews/2026-09-25-r8-017-map-search-sort-gating.md`.

Further work on those branches requires stronger permitted reference evidence; otherwise move to another implementation-ready retained subsystem or protected Map lane with exact evidence.

## R8-018 City Layout draft-persistence checkpoint

R8-018 implements the first evidence-backed City Layout backend phase: a per-profile `profile_state` SQLite store plus production `city_layout_draft_get`, `city_layout_draft_save`, and `city_layout_draft_clear`. The exact key is `city_layout_draft_v1`; first save creates revision 1, guarded updates increment exactly once, stale save/clear returns `PROFILE_REVISION_CONFLICT`, and malformed stored JSON returns `PROFILE_DATA_INVALID`. The rebuild maps the recovered per-profile profile-database concept to `%LOCALAPPDATA%\LWBridgeRebuild\profiles\<profileId>\profile.db`. The exact native no-row envelope remains partial; the rebuild returns the minimal frontend-compatible `{profileId,key,revision:0,value:null}`. See `docs/reviews/2026-09-25-r8-018-city-layout-draft-persistence.md`.

The remaining five commands (`snapshot_get`, `validate`, `apply_start`, `apply_status`, `apply_cancel`) are still missing because their game-side providers/planner/executor are protected. They are not stubbed or faked.

## R8-019 Hotkey configuration checkpoint

R8-019 restores the original ten-field Hotkey configuration object, native defaults, `hotkey_config_get/save`, and the exact `INVALID_REQUEST / invalid hotkey config` plus `STATE_UNAVAILABLE / config state is unavailable` error vocabulary. The Hotkey panel remains byte-identical to original 0.3.1. Persistence uses the rebuild-owned per-profile runtime config path while preserving unknown sibling JSON. This checkpoint does **not** implement keyboard hooks or any attack/recall/shield/equipment/relocation/reinforcement game action. See `docs/reviews/2026-09-25-r8-019-hotkey-config-persistence.md`.

## R8-020 Settings visual-metrics checkpoint

R8-020 restores `visual_metrics_config_get/save`, the exact two-field `{showFps,showPing}` object, native `false/false` defaults, `INVALID_REQUEST / invalid visual metrics config`, and shared `STATE_UNAVAILABLE / config state is unavailable` failure behavior. Visual metrics and Hotkeys share the rebuild-owned per-profile runtime config while preserving one another and unknown sibling JSON. The Settings panel remains byte-identical to original 0.3.1. Feedback export, update behavior, account interaction, and rendering internals remain separate. See `docs/reviews/2026-09-25-r8-020-visual-metrics-config-persistence.md`.

## R8-021 updater idle-status checkpoint

R8-021 corrects the production `update_status` response to the native nine-field idle envelope for the verified 0.3.1 reference, including `publishedAt:null` and `currentVersion:"0.3.1"` instead of the rebuild-only `0.3.1-rebuild`. Native constructor/serializer evidence also inventories update phases, cooldown, update-directory/file naming, host, event and error strings. `update_check` and `update_download_and_open` remain deliberately fenced as `COMMAND_NOT_IMPLEMENTED`; network, signature verification, download, replacement, executable handoff and event-transition semantics are not guessed. See `docs/reviews/2026-09-25-r8-021-update-status-idle-contract.md`.

## R8-022 feedback-export contract/fence checkpoint

R8-022 recovers the exact native five-field feedback result `{canceled,path,fileCount,sourceBytes,archiveBytes}`, the five-field `bridge://feedback-export-progress` payload `{exportId,state,processedBytes,totalBytes,percent}`, and the immutable UI states `preparing/exporting/finalizing/completed`. It restores exact missing/wrong-type/blank/Unicode-whitespace `exportId` failure as `FEEDBACK_EXPORT_FAILED / export ID is required`. A valid export ID remains `COMMAND_NOT_IMPLEMENTED` because the original service includes redaction keys, cached/rotated/segmented logs, config summaries, diagnostics, limits, pending/incomplete-export recovery, archive verification and save/open lifecycle; a generic ZIP would be a false and privacy-weaker reconstruction. See `docs/reviews/2026-09-25-r8-022-feedback-export-contract-fence.md`.

## R8-023 Equipment configuration checkpoint

R8-023 restores profile-scoped `equipment_config_get/save` on the shared runtime config. Native get projection defaults missing `equipmentPresets` to `[]` and omits missing `initialEquipmentConfig`; save requires an array, validates each preset as an object with nonblank Unicode-trimmed string `id`/`name` and unique trimmed IDs, replaces presets, stores/removes optional initial config, removes legacy `equipmentSchemes`/`squadEquipmentBindings`, preserves unrelated sibling JSON, and returns the projected equipment config. The recovered Squad panel remains byte-identical to 0.3.1. `equipment_preset_apply` and native `equipment_initial_apply` remain deliberately fenced as game-action behavior. See `docs/reviews/2026-09-25-r8-023-equipment-config-persistence.md`.

## R8-024 Monster AFK configuration checkpoint

R8-024 restores profile-scoped `monster_afk_config_save` on the same runtime config. The native validator requires top-level `enabled/strategies/allianceDrill`, exact `farm`/`join` kind/action coupling, nonnegative execution limits, conditional level-range rules, unique squad indexes 1..4, and the recovered Monster-AFK/alliance-drill error vocabulary. Save patches only `/tasks/monsterSweep`, strips routing-only `profileId`, preserves sibling tasks/root JSON and opaque strategy fields, returns the saved config shape, and `get_status.config.tasks` now reads from the same runtime store so the original Squad panel reloads persisted Monster AFK state. `monster_afk_start` and `monster_afk_stop` remain deliberately fenced because live attack/rally execution is still provider/protocol-dependent. See `docs/reviews/2026-09-25-r8-024-monster-afk-config-persistence.md`.

## R8-025 Alliance Garrison configuration checkpoint

R8-025 restores profile-scoped `alliance_garrison_config_save` and `/tasks/allianceGarrison` persistence/status reload. The immutable Squad panel and an original runtime profile agree on the native default `{enabled:false,recallOnDisable:true,squadPriority:[],targets:[]}`. The native validator accepts optional `enabled`/`recallOnDisable`, requires unique integer squad indexes 1..4, validates `allianceBuilding` and `allyCity` targets, applies strict nonblank city-snapshot validation, rejects duplicate normalized identities and overlong target names, and requires at least one target plus squad only when enabled. Save patches only `tasks.allianceGarrison`, strips routing-only `profileId`, preserves sibling/root/opaque JSON, and returns the saved task. Live automation start/stop/garrison/recall execution remains provider/protocol dependent and is not implemented here. See `docs/reviews/2026-09-25-r8-025-alliance-garrison-config-persistence.md`.

## R8-026 Resource Automation configuration/status checkpoint

R8-026 restores the exact two native Resource Automation tasks (`buildingResources`, `armedTruckReward`), `resource_automation_configure`, exact unknown-task and interval validation, canonical `{enabled,intervalMinutes}` persistence in the shared runtime config, the normal native no-`automation-status.json` idle projection, `resource_automation_status`, and `bridge://resource-automation-status` emission after configure. Missing/wrong-type `enabled` follows native behavior and becomes false. The original installed profile confirms the 60-minute disabled defaults and the normal absence of `automation-status.json`. Existing runtime-status parsing remains conservative/partial; `resource_automation_run` and the two native live actions remain provider/protocol-gated and unimplemented. See `docs/reviews/2026-09-25-r8-026-resource-automation-config-status.md`.

## R8-027 Generic Automation status checkpoint

R8-027 restores production `automation_status` for the original normal missing-`runtime/automation-status.json` condition. The native fallback enumerates exactly 18 tasks in recovered order and emits `{name,state:'idle',step:'not_loaded',running:false,enabled}` per task, with top-level `{updatedAt:null,lock:null,tasks}`. `enabled` is true only for an actual persisted JSON boolean true. A valid persisted scheduler document passes through; malformed JSON uses recovered code `INVALID_AUTOMATION_STATUS`, while exact Rust parser wording remains partial. Generic `automation_configure` is deliberately still fenced because the original validates locally and then calls `configureAutomationTask` with a 5,000 ms provider timeout; inspect/start/stop remain provider/result dependent. See `docs/reviews/2026-09-25-r8-027-automation-idle-status.md`.

## R8-028 Profile settings save checkpoint

R8-028 restores `profile_settings_save` for the current local rebuild profile. Native 0.3.1 requires `profileId`, non-negative integer `revision`, and `value`; malformed public input returns `INVALID_REQUEST`, an unknown profile returns `PROFILE_NOT_FOUND`, a non-object settings value returns `INVALID_PROFILE_SETTINGS`, and stale optimistic revision returns `PROFILE_REVISION_CONFLICT`. The original per-profile `profile.db` owns singleton table `settings(id=1, revision, value_json)` and updates it with `UPDATE settings SET value_json = ?, revision = revision + 1 WHERE id = 1 AND revision = ?`, then reads back `{revision,value}`. The rebuild preserves that schema/revision contract in its existing per-profile rebuild storage namespace and preserves sibling `profile_state` rows. Full original controller profile registry/select/enable/primary behavior and the original application-data root path are not claimed yet. See `docs/reviews/2026-09-25-r8-028-profile-settings-save.md`.

## R8-029 Profile list registry checkpoint

R8-029 replaces the rebuild's synthetic one-profile `profile_list` object with a local controller registry for the normal retained single-profile path. Native 0.3.1 reads `profiles` with `ORDER BY display_order, created_at, id`; the public profile exposes `id`, `displayName`, `roleName`, `serverId`, `gameUid`, `note`, `displayOrder`, `enabled`, `lockedReason`, `isPrimary`, and `lastLaunchedAt`, while database-only `created_at`/`updated_at` are not public. The list state exposes `selectedProfileId`, `maxProfiles`, and `profiles`. The rebuild seeds its existing stable local profile and `selected_profile_id` idempotently in `%LOCALAPPDATA%\LWBridgeRebuild\controller.db`, preserves later metadata, removes the old rebuild-only `connectionState` field from `profile_list`, and fixes `maxProfiles=1` because account/entitlement behavior is excluded. Selection repair/fallback and profile select/enable/primary/create/delete/note/reorder remain separate work. See `docs/reviews/2026-09-25-r8-029-profile-list-registry.md`.

## R8-030 Profile note checkpoint

R8-030 restores `profile_note_set` on the controller registry. Native 0.3.1 requires string `profileId` and `note`; malformed public payloads return `INVALID_REQUEST`. The note validator counts Unicode scalar values (Rust `char` semantics), allows 0..80 scalars, and rejects U+0000..U+001F plus U+007F..U+009F with `INVALID_PROFILE_NOTE`. The exact persistence SQL is `UPDATE profiles SET note = ?, updated_at = ? WHERE id = ?`; an absent row returns `PROFILE_NOT_FOUND`. Success refreshes and returns the full `{selectedProfileId,maxProfiles,profiles}` state, matching the original frontend. See `docs/reviews/2026-09-25-r8-030-profile-note-set.md`.

## R8-031 Profile reorder checkpoint

R8-031 restores `profile_reorder`. Native 0.3.1 requires a `profileIds` array, validates every ID as 1..64 UTF-8 bytes containing only ASCII letters/digits/underscore/hyphen (`INVALID_PROFILE_ID` otherwise), and requires the submitted IDs to be an exact permutation of the current registry (`INVALID_PROFILE_ORDER` for duplicate, omission, extra or count mismatch). The native core then starts one transaction, captures one timestamp, and executes `UPDATE profiles SET display_order = ?, updated_at = ? WHERE id = ?` in submitted order using zero-based indexes before commit. Success refreshes the full profile-list state without changing selection. Missing/non-array `profileIds` is exact `INVALID_REQUEST`; exact generic Rust/Tauri wording for a non-string array element remains unclaimed. See `docs/reviews/2026-09-25-r8-031-profile-reorder.md`.

## R8-032 Fixed primary-profile guard checkpoint

R8-032 restores `profile_primary_set` as the native immutable-primary assertion path rather than inventing a primary mutation. Native 0.3.1 first validates `profileId`, reads `SELECT id FROM profiles WHERE is_primary = 1`, and succeeds as a no-op only when the requested profile is already primary. Another existing profile returns `PROFILE_PRIMARY_FIXED`; a missing profile returns `PROFILE_NOT_FOUND`. The rebuild also restores the native partial unique index `idx_profiles_primary ON profiles(is_primary) WHERE is_primary = 1`. Success returns the refreshed profile-list state and does not change `updated_at`, selection, or primary ownership. See `docs/reviews/2026-09-25-r8-032-profile-primary-guard.md`.

## R8-033 Claim-delay configuration checkpoint

R8-033 restores the host-local `red_packet_delay_configure` and `treasure_delay_configure` commands. Native 0.3.1 accepts only numeric `minSeconds`/`maxSeconds`, requires finite ordered ranges within 0..60 seconds for red packets and 0..600 seconds for treasure, and uses `INVALID_REQUEST` with the recovered native detail when invalid. Success returns `{ok:true,range:[min,max]}`. The original writer mirrors each range into both `scheduler.<Kind>ClaimDelaySeconds` and `chat_automation.<kind>.claimDelaySeconds` before saving the current profile's `runtime/config.json`; the rebuild now does the same through the existing `ProfileRuntimeConfigStore` while preserving sibling/unknown fields. `profile_enable_set` remains fenced because it depends on excluded `license_capacity` behavior; R8-062 supersedes the earlier broad `profile_delete` note by closing its destructive runtime/registry/profile-data semantics and showing the public command disables the generic bound-profile guard, while keeping execution fenced on owner-excluded authorization-state admission; Trade Station remains provider-backed. See `docs/reviews/2026-09-25-r8-033-claim-delay-config.md`.

## R8-034 Profile selection/focus checkpoint

R8-034 restores `profile_select` for the retained controller registry. Native 0.3.1 validates `profileId`, requires the row to be enabled with `locked_reason IS NULL`, returns `PROFILE_NOT_FOUND` or `PROFILE_LOCKED` as appropriate, and updates only `controller_state.selected_profile_id`. `focusGame` defaults true when missing or non-boolean. When requested, focus is best effort: native verifies the running PID belongs to the configured `Game\\LastWar.exe`, enumerates visible top-level windows for that PID, calls `ShowWindow(hwnd, 9)`, then `SetForegroundWindow`; failure to find/focus a window does not fail selection. The rebuild reuses existing `GameInstallationService` process/path evidence and does not create a parallel launcher registry. See `docs/reviews/2026-09-25-r8-034-profile-select-focus.md`.

## R8-035 Window-theme checkpoint

R8-035 replaces the rebuild's `set_window_theme` no-op with the recovered native window effect. Parsed semantic values are exactly `light` and `dark`; any other parsed string returns `INVALID_THEME` / `theme must be light or dark`. Native applies `DwmSetWindowAttribute` attributes 20, 35, 36 and 34 in that order, with exact recovered light/dark values for immersive mode, caption, text and border colors. Negative HRESULTs stop the sequence and return `WINDOW_THEME_FAILED` with `DwmSetWindowAttribute failed: <signed decimal HRESULT>`. Success is null/unit. The desktop host applies this to its real top-level window handle; deterministic tests inject the DWM writer and do not change the user's desktop. See `docs/reviews/2026-09-25-r8-035-window-theme.md`.

Target-selection research also proved two important fences. Native `profile_create` is capacity-gated through shared account/profile state (`PROFILE_LIMIT_REACHED`, same subsystem as `license_capacity`), so the rebuild's fixed single-profile quota must not be substituted. Native `server_jump_history_get`, `server_jump_history_set`, and `server_jump_history_import` all resolve a profile runtime through the bridge-pipe runtime map; the current rebuild local-config history owner is not yet proven equivalent and must be revisited with runtime-owner recovery rather than extended with a synthetic get path.

## R8-036 Server-jump history checkpoint

R8-036 resolves the storage-owner caveat recorded during R8-035. Native `server_jump_history_get/set/import` resolve the requested profile runtime and use that runtime's per-profile `map-data.db`; the value is `app_settings['serverJumpHistory']`. The rebuild already owns the recovered per-profile Map Data schema, so public history commands now use its `app_settings` table instead of `LocalConfigStore.ServerJumpHistory`.

The recovered normalizer accepts integer server IDs 1..99999, removes duplicates in first-seen order, ignores invalid elements and caps output at five. Missing/non-array `history` becomes `[]`. Get returns `[]` for a missing setting without creating it. Set normalizes, upserts and returns. Import is migration-only: an existing setting wins unchanged; only a missing setting imports the supplied legacy history. This matches the retained frontend's `lastwar.serverJumpHistory` localStorage migration, which removes the browser key only after import succeeds.

Stable native profile/runtime codes are also restored for this family: `PROFILE_ID_REQUIRED` and `PROFILE_RUNTIME_UNAVAILABLE`. Malformed stored JSON uses `INVALID_SETTING`. The legacy rebuild LocalConfig history field remains readable for backward compatibility but is no longer a public-command owner. See `docs/reviews/2026-09-25-r8-036-server-jump-history.md`.

## R8-037 append_log checkpoint

R8-037 replaces the rebuild's `append_log` no-op with the recovered profile-runtime logging side effect. The shared frontend invoke wrapper injects the selected `profileId`; native resolves that profile runtime, requires a string `message`, takes at most 2,000 Unicode scalar values, replaces each CR and LF with one space, and appends UTF-8 to `logs/xlua-bridge.log`.

The exact normal-path line is `[<unix-ms>] [bridge-app] <sanitized-message>\n`. Empty strings are valid. Stable profile failures are `PROFILE_ID_REQUIRED` and `PROFILE_RUNTIME_UNAVAILABLE`. Native log I/O is best effort, so file/open/write failures do not turn a valid append request into a command failure. The rebuild now routes this to its existing retained profile runtime directory rather than a global LocalConfig log.

The wider native log manager exposes segment/rotation machinery, but R8-037 does not invent its unclosed retention policy. Malformed `message` is rejected before native writer execution by Tauri argument deserialization; the rebuild rejects it at its transformed boundary without claiming the framework-generated text/code is byte-identical. See `docs/reviews/2026-09-25-r8-037-append-log.md`.

## R8-038 game_root_status checkpoint

R8-038 replaces the rebuild-specific public installation-status object with the recovered native `game_root_status` projection. The public command now returns exactly `{root, source, valid, candidates}`, and each candidate is exactly `{path, source}`. With no valid candidate, native returns empty strings for `root` and `source`, `valid=false`, and an empty candidate list. Shared path-state absence remains `STATE_UNAVAILABLE` / `path state is unavailable`.

The native public validity predicate is deliberately lightweight: `Game/LastWar.exe` must exist and `Game/LastWar_Data/Plugins/x86_64` must be a directory. It does not perform the rebuild's launcher/xLua/PE/fingerprint admission checks. R8-038 therefore keeps the existing stricter `GameRootStatus` path for launch/repair internals and routes only the public command through `NativeGameRootStatus`.

Recovered source order is `saved`, `environment`, `nearby`, `bridge-root-file`, `default`, then discovered `process` and `registry` candidates. The original persists through `game-root.txt` and performs PowerShell/CIM plus registry discovery; the rebuild uses its existing LocalConfig persistence and equivalent C# process/registry discovery, so those storage/discovery mechanisms are classified as equivalent rather than byte-identical. Exact `game_root_select` remains separate. See `docs/reviews/2026-09-25-r8-038-game-root-status.md`.

## R8-039 game_root_select checkpoint

R8-039 restores the recovered public `game_root_select` result and save boundary. The command now returns exactly `{canceled, path, valid}`. Cancel is `{canceled:true,path:null,valid:false}`; an ordinary invalid selection is `{canceled:false,path:<normalized>,valid:false}` and is not persisted; a valid selection is `{canceled:false,path:<normalized>,valid:true}` and is persisted.

Normalization and validity reuse the native root logic recovered in R8-038. Only valid selections enter shared path-state persistence. Missing path state is `STATE_UNAVAILABLE` / `path state is unavailable`; a path that cannot be normalized/revalidated at the persistence boundary uses `INVALID_GAME_ROOT` / `select the folder containing Game\\LastWar.exe`.

The public picker no longer routes through the rebuild's stricter `SaveGameRoot` lifecycle/rebind path. That stricter path remains internal launch/repair safety. The original dialog/storage plumbing is not claimed byte-identical: native uses its own dialog stack and `game-root.txt`; the rebuild keeps `FolderBrowserDialog` plus LocalConfig as equivalent plumbing. See `docs/reviews/2026-09-25-r8-039-game-root-select.md`.

## R8-040 proxy_status checkpoint

R8-040 restores the native public `proxy_status` projection. The full launch-component branch returns exactly `state`, `installed`, `resourceAvailable`, `targetExists`, `originalExists`, `installedMode`, `gameRunning`, `targetPath`, `runtimeManaged`, `repairRequired`; the reduced no-launch-component branch omits `installedMode` and returns the remaining nine fields. The old rebuild-only `launcherRunning`, `bridgeOnline`, `gamePid`, and `launcherPid` fields are removed from this command.

Recovered state vocabulary and precedence are `resourceMissing`, `targetMissing`, `needsRepair`, and `installed`. The target is `Game\\LastWar_Data\\Plugins\\x86_64\\xlua.dll`, the preserved original is `xlua_.dll`, and `installedMode` is `secure`, `plain`, or null according to target-vs-resource hashes. `repairRequired` is exactly `!runtimeManaged && gameRunning && state == needsRepair`; it is not the rebuild Overview recovery-journal flag.

The command now exposes native profile/runtime validation codes `PROFILE_ID_REQUIRED`, `PROFILE_RUNTIME_UNAVAILABLE`, and `STATE_UNAVAILABLE`. Exact prose for the first two remains `UNKNOWN`. Resource discovery is read-only equivalent plumbing over a complete recovered proxy resource set; proxy install/overwrite/backup/restore, original launcher ownership, and close/exit restoration remain incomplete. See `docs/reviews/2026-09-26-r8-040-proxy-status.md`.

## R8-041 game_recovery_status checkpoint

R8-041 restores the native public `game_recovery_status` / `bridge://game-recovery` projection. The exact field order is `state`, `reason`, `updateDetected`, `restarted`, `startedAt`, `completedAt`, `attempts`, `nextRetryAt`, `error`, `noticeId`, `noticeVisible`. Static serializer recovery proves `startedAt` is a scalar integer and `noticeId` is a scalar numeric identifier; the older rebuild nullable-start/string-notice model was non-native.

The exact default object is `state="idle"`, `reason=null`, both booleans false, `startedAt=0`, nullable completion/retry/error fields null, `attempts=0`, `noticeId=0`, and `noticeVisible=false`. Recovery start increments the notice identifier, stamps `startedAt`, publishes `waiting`, and keeps the notice visible. The same notice identity is preserved across that recovery attempt; native terminal publication is `succeeded` or `failed`, not the rebuild's former return-to-idle simplification. Disabling recovery automation resets to the native idle defaults while preserving the current notice identifier.

The command now follows the native selected-profile runtime path and exposes `PROFILE_ID_REQUIRED`, `PROFILE_RUNTIME_UNAVAILABLE`, and exact `STATE_UNAVAILABLE` / `game recovery state is unavailable` rather than fabricating idle state when no recovery state exists. Exact prose for the first two runtime-resolution errors remains `UNKNOWN`.

This checkpoint changes recovery status/publication semantics only. Existing recovered disconnect/hang/update thresholds, retry tables, termination gates, updater suppression and process-control actions remain unchanged; original proxy install/backup/restore and launcher lifecycle remain incomplete. See `docs/reviews/2026-09-26-r8-041-game-recovery-status.md`.

## R8-042 profile_instance_status checkpoint

R8-042 restores the native public `profile_instance_status` result. No active/recoverable native instance serializes as JSON `null`; an active record serializes exactly `profileId`, `instanceId`, `phase`, `pid`, `startedAt`, `lastError`, `identityConfirmed`, `leaseRequired`, `connectionState`, `bridgeConnected`, `lastHeartbeatAt`, `leaseActive` in that order. The rebuild-only stopped/offline and unmanaged-process status objects are no longer exposed by this command.

Native `startedAt` and `lastHeartbeatAt` are Unix-millisecond integers. Recovered phases are `starting`, `awaitingIdentity`, `running`, `recovering`, and `error`; there is no native stopped/stopping instance-record phase. In retained single-profile mode, the ordinary classifier is connected only when the exact instance has a bridge route, a heartbeat fresher than 15,001 ms, and confirmed identity; otherwise it is reconnecting. `starting`, `recovering`, `awaitingIdentity`, and `lastError` map to the recovered special connection states first.

The command now uses native `PROFILE_ID_REQUIRED` and `PROFILE_RUNTIME_UNAVAILABLE` boundary codes. Native multi-entitlement lease state remains outside retained scope and is not fabricated; the retained single-profile projection keeps `leaseRequired=false` and `leaseActive=false`. Internal rebuild lifecycle diagnostics and start/stop/reconcile process-control behavior remain unchanged. See `docs/reviews/2026-09-26-r8-042-profile-instance-status.md`.

## R8-043 get_status contract fence

R8-043 closes the native top-level `get_status` / `bridge://status` projection. The exact field order is `ok`, `backend`, `runtimeRoot`, `pending`, `xluaOnline`, `lastXluaActivity`, `config`. Successful native status writes `ok=true`; `backend` is `offline` or `named-pipe` from exact route presence and `xluaOnline` mirrors that route decision; `pending` is a scalar bridge-state counter; `lastXluaActivity` is Unix milliseconds and uses the maximum of stored bridge activity and selected-route activity. Missing bridge state is exact `STATE_UNAVAILABLE` / `bridge state is unavailable`.

Native `config` is not a small fixed DTO. It loads `<runtimeRoot>\\config.json`, requires an object-shaped configuration, then migrates/normalizes a broad retained configuration tree through `0x1403AD60C-0x1403B4FCA` before publishing the resulting generic JSON value. The missing-file seed `{enable_eval:false, auto_shield:true, auto_red_packet_treasure:true}` is recovered, but the complete migration/default/preservation table is not yet closed.

Because that config value is directly consumed by multiple retained frontend panels, R8-043 does not replace the rebuild's current status object with another partial projection. The current rebuild `get_status` remains explicitly classified `DEVIATION` until the native config normalizer is recovered one-for-one. See `docs/reviews/2026-09-26-r8-043-get-status-contract-fence.md`.

## R8-044 game_asset_image checkpoint

R8-044 restores the native public `game_asset_image` boundary. `assetPath` and `spriteName` are optional string fields: missing/null/non-string values are absent, strings are trimmed, and exactly one trimmed source is required. Neither/both sources fail with exact `INVALID_REQUEST` / `exactly one image source is required`. A cache miss with no game route fails with exact `GAME_DISCONNECTED` / `game disconnected`.

Native cache lookup happens before game admission. The selected runtime owns an `asset-cache` directory of `.png` entries; native filenames are full lowercase SHA-256 hex plus `.png`, maintenance is gated to once per 60,000 ms, and cleanup activates above the 256 MiB boundary. The exact SHA-256 preimage is not byte-closed, so the rebuild's canonical source-key digest and file-I/O/eviction ordering remain explicitly `EQUIVALENT_REIMPLEMENTATION`, not exact native bytes.

On a cache miss, native issues one `getAssetImage` request with an exact 15,000 ms timeout and no handler retry loop. Successful bytes are admitted by the exact eight-byte PNG signature; invalid bytes fail with `INVALID_ASSET` / `invalid PNG asset`. Public success is exactly `{dataUrl}`, using `data:image/png;base64,` plus the PNG bytes. Rebuild-only IHDR/dimension/4096-pixel gates and the three-attempt RAM-cache retry policy are removed. See `docs/reviews/2026-09-26-r8-044-game-asset-image.md`.

## R8-045 dispatch_assist_state fence

R8-045 closes the native read-only `dispatch_assist_state` top-level contract without inventing its missing live provider. Successful native state is assembled in exact order as `tasks`, `todayCount`, `dailyLimit`, `serverTime`, `updatedAt`, `jobs`. The live provider owns `tasks` plus the daily/server-time scalar state; persisted `jobs` are loaded separately from the profile `dispatch_assist_jobs` table and merged into that public result.

If the live dispatch-assist state is unavailable, native returns exact `STATE_UNAVAILABLE` / `dispatch assist state unavailable`. The persisted job reader selects `task_json,assist_at,status,attempts,last_error,created_at,updated_at` and orders active `scheduled`/`waiting_connection`/`running` jobs before other statuses, then `assist_at ASC, updated_at DESC`.

The rebuild already has the matching SQLite table/index but no production live dispatch-assist provider or exact task/job-row projector. R8-045 therefore makes no runtime-code change: a synthetic idle state or DB-only success object would be non-native behavior. See `docs/reviews/2026-09-26-r8-045-dispatch-assist-state-fence.md`.

## R8-046 squad_list checkpoint

R8-046 restores native `squad_list` as a private retained game call rather than widening public `call_lua`. Missing/null/non-string/blank `profileId` returns exact `PROFILE_ID_REQUIRED` / `PROFILE_ID_REQUIRED`; an unknown runtime returns exact `PROFILE_RUNTIME_UNAVAILABLE` / `PROFILE_RUNTIME_UNAVAILABLE`; and no selected game route returns exact `GAME_DISCONNECTED` / `game disconnected`.

Native issues exactly one `getSquads` call with empty `{}` args and a 5,000 ms result deadline. Timeout is exact `LUA_CALL_TIMEOUT` / `lua call result unknown after timeout: getSquads`. The handler does not build a host squad DTO; it returns the correlated game JSON through the generic result converter, so the rebuild forwards the result unchanged. Separate native processing confirms the game payload uses `squads`, `index`, `positions`, `heroes`, `squadIndex`, `uuid`, `name`, `position`, and `equips`, but R8-046 does not impose those as a host rewrite schema.

The authenticated .NET bridge transport remains `EQUIVALENT_REIMPLEMENTATION`. R8-046 adds a command-specific result deadline/message to that transport while leaving the existing default timer and public `call_lua(getStatus,{})` boundary unchanged. See `docs/reviews/2026-09-26-r8-046-squad-list.md`.

## R8-047 monster_catalog_options checkpoint

R8-047 restores the native read-only `monster_catalog_options` command over the same private retained game-call path. It uses the same exact profile/runtime admission as `squad_list`, returns exact `GAME_DISCONNECTED` / `game disconnected` without a route, then issues one `getMonsterCatalogOptions` call with `{}` and a 5,000 ms result deadline. Timeout is exact `LUA_CALL_TIMEOUT` / `lua call result unknown after timeout: getMonsterCatalogOptions`.

Native returns the correlated game JSON through the generic converter rather than rebuilding option rows. The frontend consumes `result.options` and fields including `key`, `group`, `monsterNameKey`, `monsterType`, level bounds and searchability, but the host preserves the provider JSON unchanged. The authenticated .NET transport remains equivalent plumbing and public `call_lua` is not widened. See `docs/reviews/2026-09-26-r8-047-monster-catalog-options.md`.

## R8-048 trade_station_catalog checkpoint

R8-048 restores the native read-only `trade_station_catalog` command without enabling Trade Station mutation. It uses the same exact selected-profile runtime admission as R8-046/047, returns exact `GAME_DISCONNECTED` / `game disconnected` through the native game-route helper, then issues one `getTradeStationCatalog` call with empty `{}` args.

Unlike the previous two commands, native gives this call a **10,000 ms** result deadline (`0x2710`). Timeout is exact `LUA_CALL_TIMEOUT` / `lua call result unknown after timeout: getTradeStationCatalog`. Success is the correlated game JSON passed through the generic converter unchanged; the frontend reads `result.items`. The authenticated .NET transport remains equivalent plumbing, generic `call_lua` stays closed, and `trade_station_configure` remains fenced. See `docs/reviews/2026-09-26-r8-048-trade-station-catalog.md`.

## R8-049 VIP18 boundary fence

R8-049 researched the hidden VIP18 base read family far enough to identify its dependency boundary, then stopped without production implementation. `vip18_base_config_get` projects `selectedSkinId`, `autoApplyOnStart`, and `favoriteSkinIds`, but it reads the same shared native config-state/migration layer that remains incomplete under R8-043; unavailable config state is exact `STATE_UNAVAILABLE` / `config state is unavailable`.

## R8-050 city_layout_snapshot_get fence

R8-050 closes the recoverable host boundary of the read-only `city_layout_snapshot_get` command. The frontend supplies no payload. Native first awaits shared authorization state; unavailable state is exact `STATE_UNAVAILABLE` / `authorization state is unavailable`. That authorization-state result is then carried as the argument to the City Layout game helper, so its exact request shape is owner-excluded and intentionally unclaimed.

After that dependency, native resolves the selected profile runtime, requires a connected game route (`GAME_DISCONNECTED` / `game disconnected`), and issues one `getCityLayoutSnapshot` call with a 10,000 ms deadline. The correlated provider JSON is returned through the shared generic converter unchanged. No runtime implementation is added because substituting `{}`, a profile ID, or frontend-derived fields would not match the original authorization-derived request. See `docs/reviews/2026-09-26-r8-050-city-layout-snapshot-fence.md`.

## R8-051 automation_inspect fence

R8-051 closes the recoverable read-only `automation_inspect` host boundary. Native awaits the same shared authorization-state future before request construction; unavailable state is exact `STATE_UNAVAILABLE` / `authorization state is unavailable`. It directly extracts `task`, resolves the selected runtime, requires a connected game route (`GAME_DISCONNECTED` / `game disconnected`), then calls `inspectAutomationTask` with a 5,000 ms deadline. The shared timeout is `LUA_CALL_TIMEOUT` / `lua call result unknown after timeout: inspectAutomationTask`.

A generic raw pass-through is not exact. Native explicitly special-cases task `allianceGarrison`, parses `allies` rows and fields including `uuid`, `serverId`, `uid`, and later `updatedAt`, then rebuilds result content. In addition, request-building state carries authorization-derived role material; nearby native vocabulary includes `premium` and `admin`. Those authorization/role details are owner-excluded, and the exact `allianceGarrison` rewrite is still partial, so R8-051 makes no production implementation. See `docs/reviews/2026-09-26-r8-051-automation-inspect-fence.md`.

## R8-052 city_layout_apply_status fence

R8-052 closes the observable read-only `city_layout_apply_status` boundary. The frontend supplies no payload. Native first awaits the shared authorization-state future; unavailable state is exact `STATE_UNAVAILABLE` / `authorization state is unavailable`. After that succeeds, native resolves the selected runtime, requires a connected game route (`GAME_DISCONNECTED` / `game disconnected`), and calls `getCityLayoutApplyStatus` with a 5,000 ms deadline. Timeout follows the shared exact `LUA_CALL_TIMEOUT` / `lua call result unknown after timeout: getCityLayoutApplyStatus` path, and successful provider JSON goes through the generic converter without a host DTO rewrite.

No runtime implementation is added: bypassing native authorization admission or fabricating an idle status from local draft state would be observable non-native behavior, and the live apply provider/status schema remains unrecovered. See `docs/reviews/2026-09-26-r8-052-city-layout-apply-status-fence.md`.

## R8-053 profile_settings_get fence

R8-053 closes the read-only `profile_settings_get` persistence/public contract. Native requires JSON-string `profileId`; missing/wrong-type input uses exact `INVALID_REQUEST`. After native authorization-state admission, the command reads the same per-profile singleton settings row recovered in R8-028 (`SELECT revision, value_json FROM settings WHERE id = 1`) and serializes exactly `{profileId, revision, value}`. Malformed stored data can surface native `PROFILE_DATA_INVALID`.

The rebuild already has an equivalent local settings read from R8-028, but native first awaits shared authorization state and returns exact `STATE_UNAVAILABLE` / `authorization state is unavailable` when that state is unavailable. Because authorization/account-state recovery is owner-excluded, R8-053 does not wire a getter that silently bypasses this admission rule. See `docs/reviews/2026-09-26-r8-053-profile-settings-get-fence.md`.

## R8-054 lastwar_localize strict audit

R8-054 reclassifies the existing R7 Last War localization implementation under strict one-to-one parity. Native confirms the exact 200-key cap (`TOO_MANY_LOCALE_KEYS` above it), requested-locale→English→key fallback, and the existing locale-loader error vocabulary/cache verification behavior. The rebuild's verified `%LOCALAPPDATA%\\LWBridgeRebuild\\locales` cache remains equivalent plumbing.

Two strict deviations remain. Native first awaits shared authorization state (`STATE_UNAVAILABLE` / `authorization state is unavailable`), while the rebuild exposes localization without that owner-excluded admission. Native is also permissive for non-object payload, wrong/missing `language` (default `en`), and wrong/missing `keys` container (empty list), whereas the rebuild currently throws `INVALID_PAYLOAD`; exact non-string elements inside a valid `keys` array remain unclosed. R8-054 makes no runtime change rather than partially rewriting the parser. See `docs/reviews/2026-09-26-r8-054-lastwar-localize-audit.md`.

## R8-055 map_treasure_claim_status strict audit

R8-055 closes the native read-only Treasure status boundary without enabling protected claim execution. The frontend supplies no feature payload. Native first awaits shared authorization state (`STATE_UNAVAILABLE` / `authorization state is unavailable`), resolves the selected runtime, then calls `getTreasureClaimStatus` once with a 5,000 ms deadline. Timeout is exact `LUA_CALL_TIMEOUT` / `lua call result unknown after timeout: getTreasureClaimStatus`.

The provider result is inspected for `playerUid` and `states`; native transactionally refreshes `treasure_claim_states`, including expired-positive-row cleanup and upsert. Serialization failure uses `MAP_DATA_INVALID` with `serialize treasure claim state: ` detail. After a successful cache update, native returns the original provider JSON through the generic converter, preserving provider-owned fields such as frontend-recognized optional `batch`.

The current rebuild remains a documented deviation: it adds rebuild-only scan/operation/server admission guards, uses an 8-second current-client probe, reconstructs exactly `{playerUid,allianceId,states}`, cannot preserve provider-owned extras, and couples status to generic refresh/live-server plumbing. R8-055 is audit-only; protected `map_treasure_claim` execution remains absent. See `docs/reviews/2026-09-26-r8-055-treasure-claim-status-audit.md`.

## R8-056 watermark_lookup fence

R8-056 closes the retained `watermark_lookup` input/service/result boundary without recreating excluded authorization behavior. The frontend sends `{traceCode}`. Native treats missing/null/non-string `traceCode` as an empty string, trims it, and serializes that string directly into the request body.

Before the service call, native awaits shared authorization state (`STATE_UNAVAILABLE` / `authorization state is unavailable`) and checks authorization `accessRole`; a missing required role returns `ROLE_REQUIRED`. The exact role policy and authorization transport state are owner-excluded and intentionally unimplemented.

The handler sends JSON to exact path `/api/watermark/lookup` through the shared HTTP service helper. That helper exposes native error vocabulary including `REQUEST_TIMEOUT`, `NETWORK_ERROR`, `SERVICE_UNAVAILABLE`, `SESSION_INVALID`, and service `/error/code`/`/error/detail`. On success, the service JSON is returned through the generic converter; the host does not construct a fixed watermark DTO. R8-056 therefore makes no runtime change. See `docs/reviews/2026-09-26-r8-056-watermark-lookup-fence.md`.

## R8-057 update_status state-machine audit

R8-057 extends R8-021 from exact idle output to the full observable read-only updater status machine. Native `update_status` only snapshots the updater service and serializes the same nine fields in exact order: `phase`, `currentVersion`, `latestVersion`, `releaseNotes`, `publishedAt`, `progress`, `message`, `nextManualCheckAt`, `downloadDirectory`.

Transition code closes seven phases: `idle`, `checking`, `upToDate`, `available`, `downloading`, `opening`, and `error`. Manual check entry clears progress/message, sets `nextManualCheckAt` to sampled time + 60,000 ms, and emits `bridge://update-status`; an attempt within that cooldown returns the current snapshot without starting a new check. Successful manifest processing chooses `upToDate` when no update is needed or `available` when one is, filling `latestVersion`, `releaseNotes`, and `publishedAt` while clearing progress/message.

Download status is also exact at the host boundary: `downloading` starts at progress 0, progress updates republish, `downloadDirectory` updates republish, and verified download transitions to `opening` with progress 100 before the executable-open helper. Error transitions publish `phase="error"` with either fixed `UPDATE_NOT_AVAILABLE` or a native/service supplied detail. The current rebuild remains exact only at the R8-021 idle state; it has no real updater state owner, so R8-057 makes no runtime change and keeps `update_check` / `update_download_and_open` fenced. See `docs/reviews/2026-09-26-r8-057-update-status-state-machine-audit.md`.

## R8-058 app_exit_confirm lifecycle fence

R8-058 closes the retained main-window close/confirm boundary without inventing the missing host mutation lifecycle. Native main-window close emits exact event `app://close-requested` with numeric `{instanceCount}` instead of immediately taking the ordinary close path. The frontend then invokes global/no-payload `app_exit_confirm`.

Confirmed exit is a managed shutdown sequence, not a simple window close. Native iterates managed runtimes, dispatches exact `bridge_exit`, performs game/instance shutdown, exposes exact `GAME_CLOSE_TIMEOUT` / `The game did not close in time.`, and invokes an original-proxy restoration helper whose native strings include `originalExists` and `app exit restored original proxy`. Failures are wrapped under exact code `APP_EXIT_FAILED`; the universal fixed message/detail mapping remains unclosed. Successful completion serializes JSON `null`.

The rebuild still owns only R8-040 read-only proxy discovery and does not implement the original install/backup/restore mutation primitive. R8-058 therefore adds no `app_exit_confirm` runtime handler: a direct `Close()` implementation would skip `bridge_exit`, managed-game cleanup and original-proxy restoration, making shutdown observably non-native. See `docs/reviews/2026-09-26-r8-058-app-exit-confirm-fence.md`.

## R8-059 monopoly_cell_open fence

R8-059 closes the first retained Mini-game live-action boundary. The per-command wrapper supplies no Monopoly-specific arguments, while shared API wrapper `U()` injects the currently selected `profileId`. Native then awaits authorization state before profile/runtime resolution, so exact `STATE_UNAVAILABLE` / `authorization state is unavailable` has precedence over profile/runtime and route errors.

After admission, native requires the selected runtime (`PROFILE_ID_REQUIRED` / `PROFILE_RUNTIME_UNAVAILABLE`), then connected game transport (`GAME_DISCONNECTED` / `game disconnected`). It issues exactly one `openMonopolyCell` call with `{}` args and a 5,000 ms deadline; timeout is exact `LUA_CALL_TIMEOUT` / `lua call result unknown after timeout: openMonopolyCell`, provider failures use the shared `LUA_CALL_FAILED` path, and success forwards the correlated provider JSON unchanged through the generic converter.

No runtime action is added because original LWBridge requires owner-excluded authorization-state admission before enabling this live action. A direct provider call would weaken observable admission semantics. See `docs/reviews/2026-09-26-r8-059-monopoly-cell-open-fence.md`.

## R8-060 construction_rewards_claim fence

R8-060 closes the retained Construction Rewards claim action in the Automation API block. The feature wrapper supplies no claim-specific fields; shared `U()` normalizes to `{}` and injects the selected `profileId`. Native awaits authorization state first, then resolves profile/runtime and connected game route, preserving exact `STATE_UNAVAILABLE`, `PROFILE_ID_REQUIRED`, `PROFILE_RUNTIME_UNAVAILABLE`, and `GAME_DISCONNECTED` precedence.

After admission, native issues exactly one `claimConstructionRewards` call with `{}` args and a 5,000 ms result deadline. Timeout is exact `LUA_CALL_TIMEOUT` / `lua call result unknown after timeout: claimConstructionRewards`; provider failures use the shared `LUA_CALL_FAILED` mapping, and correlated success JSON is forwarded unchanged through the generic converter.

R8-060 makes no production action change because bypassing the owner-excluded authorization-state admission would make a live reward-claim action callable in states where native rejects it. See `docs/reviews/2026-09-26-r8-060-construction-rewards-claim-fence.md`.

## R8-061 vip18_base_config_save fence

R8-061 extends the hidden VIP18 family beyond R8-049's read boundaries. The frontend passes a config object through shared `U()`, which injects selected `profileId`. Native first awaits authorization state (`STATE_UNAVAILABLE` / `authorization state is unavailable`), then resolves the selected runtime.

The dedicated save helper owns retained fields `selectedSkinId`, `autoApplyOnStart`, and `favoriteSkinIds`. Invalid supplied skin IDs use exact `INVALID_REQUEST` / `invalid base skin id`; invalid or missing/non-boolean auto-apply uses exact `INVALID_REQUEST` / `invalid base skin auto-apply setting`. Favorites pass through a dedicated native sequence normalizer; its exact element coercion/drop/duplicate semantics remain unclosed and are not guessed.

Persistence belongs to the shared config-state owner under top-level `vip18_profiles` in `config.json`. If that state is unavailable, exact failure is `STATE_UNAVAILABLE` / `config state is unavailable`. On success, native returns the same normalized three-field VIP18 config projection as config-get rather than a generic acknowledgment. Because authorization admission is owner-excluded and the shared config migration/default/merge layer remains incomplete under R8-043/R8-049, R8-061 adds no production save implementation. See `docs/reviews/2026-09-26-r8-061-vip18-base-config-save-fence.md`.

`vip18_base_list` parses optional boolean `refresh` with false fallback and returns a public `items` projection. Its persistent cache record uses `version`, `updatedAt`, and `items`; successful live refresh writes version 1 plus current Unix-ms. `refresh=false` is cache-only; `refresh=true` uses cached items when the game is disconnected, and when connected calls `getVip18BaseSkins` with a 10,000 ms deadline. Before constructing the cache identity, however, native awaits authorization state and can return exact `STATE_UNAVAILABLE` / `authorization state is unavailable`. The cache filename is `base-skin-catalog-<dynamic-identity>.json`; the identity is carried across an authorization-state-dependent path, but its exact source is intentionally not decoded because authorization/account-state recovery is owner-excluded. See `docs/reviews/2026-09-26-r8-049-vip18-boundary-fence.md`.

## R8-062 profile_delete fence

R8-062 closes the destructive native `profile_delete` path without exposing it in the rebuild. Native awaits shared authorization state before parsing `profileId`; unavailable state is exact `STATE_UNAVAILABLE` / `authorization state is unavailable`, and missing/wrong-type `profileId` then uses exact `INVALID_REQUEST`.

Deletion rejects a running profile with `PROFILE_RUNNING`, a missing row with `PROFILE_NOT_FOUND`, and the primary profile with `PROFILE_PRIMARY_REQUIRED`. The native target query is `SELECT is_primary, game_uid IS NULL FROM profiles WHERE id = ?`. The generic target helper contains an optional `PROFILE_ALREADY_BOUND` guard, but public `profile_delete` passes that guard flag as false; the command therefore does not reject a bound non-primary profile solely because `game_uid` is present. This corrects the older broad R8-033 research note.

Native deletes the controller row transactionally, repairs `selected_profile_id` to the surviving primary profile when the deleted profile was selected, commits, then removes the profile-data directory. Profile-data cleanup failure is `IO_ERROR` with `delete profile data` context and can therefore occur after the registry change already committed. Native then reconciles runtime state and returns the same refreshed `{selectedProfileId,maxProfiles,profiles}` projection as `profile_list`.

No production delete is added because the owner-excluded authorization-state admission has precedence over every destructive side effect. See `docs/reviews/2026-09-26-r8-062-profile-delete-fence.md`.

## R8-063 profile_enable_set fence

R8-063 closes the native capacity-reconciliation contract for `profile_enable_set`. It is not a `{profileId,enabled}` toggle: native requires a `profileIds` array, awaits authorization-derived capacity, and the controller core accepts only capacities `1`, `2`, or `5` (`INVALID_PROFILE_CAPACITY` otherwise). A missing primary profile returns `PROFILE_PRIMARY_MISSING`.

When total profiles fit within capacity, native enables all profiles. When over capacity at 1, it keeps the primary profile. When over capacity at 2 or 5, the submitted unique profile-ID set must contain exactly `capacity` existing IDs and include the primary; otherwise native returns `PROFILE_SELECTION_REQUIRED`. Exact serde detail for individual invalid array elements remains unclosed.

The capacity transaction updates every row using `UPDATE profiles SET enabled = ?, locked_reason = ?, updated_at = ? WHERE id = ?`: selected members become enabled/unlocked, non-members become disabled with `locked_reason="license_capacity"`, and `updated_at` is current Unix-ms. After commit, native repairs `selected_profile_id` to the surviving primary if the previous selected profile became locked, then returns the same refreshed `{selectedProfileId,maxProfiles,profiles}` projection as `profile_list`.

No production implementation is added because the governing capacity is authorization/license-derived and owner-excluded. See `docs/reviews/2026-09-26-r8-063-profile-enable-set-fence.md`.

## R8-064 profile_create fence

R8-064 closes native `profile_create` far beyond the earlier quota-only fence. Native compares the current controller profile count directly against authorization/entitlement-derived `maxProfiles` and returns exact `PROFILE_LIMIT_REACHED` when `count >= capacity`; that capacity source remains owner-excluded and is not replaced with the rebuild's retained `maxProfiles=1` adaptation.

The local create transaction is now exact: native generates 16 random bytes and encodes them as 22-character Base64URL without padding; reads `SELECT COALESCE(MAX(display_order) + 1, 0) FROM profiles`; formats the default name with literal `账号 ` plus `displayOrder+1`; creates the profile root/`profile.db`; and inserts an enabled, unlocked, non-primary row with role/server/game UID unset, empty note, timestamps, and null `lastLaunchedAt`.

The successful command serializes the created profile in exact 13-field order: `id`, `displayName`, `roleName`, `serverId`, `gameUid`, `note`, `displayOrder`, `enabled`, `lockedReason`, `isPrimary`, `createdAt`, `updatedAt`, `lastLaunchedAt`. The retained frontend then explicitly calls `profile_select(created.id)`; the controller create SQL itself does not update `selected_profile_id`.

After local creation, native still performs substantial post-create runtime/state provisioning through `0x1403AC07B`, which can return `STATE_UNAVAILABLE`; exact failure cleanup/rollback across the local row/directory/runtime phase remains unclosed. No production implementation is added. See `docs/reviews/2026-09-26-r8-064-profile-create-fence.md`.

## R8-065 fresh live Home/Map checkpoint

The owner requires fresh real-game proof before anything is called working. R8-065 therefore ran the current Release build against the installed current-v21 Last War client rather than relying on historical R7 evidence.

Home passed `--live-overview-home-proof`: manual launch reached `connected`, closed cleanly, then startup auto-launch reached `connected` and closed cleanly. Map initially failed `--live-current-client-all-eight-modes` because server 2212 returned a contiguous 3×9 AOI footprint at `(665,875)` while the conservative scanner still required exactly ten rows. A targeted one-AOI-row downward probe proved the missing bottom row can be covered by the overlapping current-v21 footprint.

Production keeps the conservative complete-coverage scanner. The fix accepts only contiguous rectangular 9- or 10-row footprints within the existing 2-5-column bound; when only the band's final row is missing it nudges the target down 10 tiles and unions that bounded footprint. Exact 10,000-AOI coverage is still mandatory. The removed R7-151 wide-FOV shortcut was not restored. The live proof harness was also corrected to the R8-013 native lifecycle (`publishing` → `idle`) instead of a stale `completed` terminal phase.

Final live proof exited 0: Normal and Fast both read 2500/2500 with 0 failed and 0 unread in the same owned game session, and Fast counts were unchanged after SQLite reopen. Offline regression remained green (`ok=true`, `failures=[]`, zero build warnings/errors, frontend check passed, `git diff --check` passed).

Classification remains deliberately split: Home lifecycle and core Manual Map scan are **LIVE-WORKING / EQUIVALENT_REIMPLEMENTATION**; the original Map acquisition algorithm is still `UNKNOWN`; whole-program one-to-one parity is still not complete. See `docs/reviews/2026-09-26-r8-065-live-home-map-v21.md` and `evidence/lwbridge-implementation/2026-09-26-r8-065-live-home-map-v21.json`.

## Parked protected package-key lane

The protected package/key lane is evidence-limited and must remain separate from current Map implementation. Do not blindly repeat artifact searches or cross the protected boundary; resume it only if genuinely new permitted evidence appears.

The long-term retained-runtime goal remains recovery of the non-account portions of the original protected `bridge-scripts.dat` implementation where required for product compatibility.

Already known:

- LWBP package version 2.
- `package-key.envelope` runtime path and bounded reader.
- Microsoft Software Key Storage Provider.
- persistent key `{2D337A4D-7E6C-49EF-9486-54F0A00D8A41}`.
- ECK1/ECCPUBLICBLOB public-key format.
- 32-byte derived material.
- AES-GCM with 32-byte key, 12-byte nonce and 16-byte tag.
- an identified package decrypt/validation consumer.
- `LWBP2|`, package-integrity and package-build validation strings.

R8-003 closes the package binary layout/AES ownership. R8-004 closes the host-side `LWKE1` token framing and auth persistence. R8-005 closes the exact client P-256 public-key and challenge encodings. R8-006 proves caller-side output ownership: the opaque `package-key.envelope` consumer receives a zero-initialized vector as arg4; on success that exact vector becomes package arg3 and then the required 32-byte AES key. The independent helper lane additionally closes the exact `NCryptSecretAgreement` + `NCryptDeriveKey(L"TRUNCATE", NULL, ..., 32)` KDF contract, generic envelope AES-GCM argument sizes, `LWAT2` authorization-context ordering, raw-envelope equality guard, and manifest/output separation. Remaining evidence-limited unknowns are the exact `LWKE1` peer-key/nonce/ciphertext/tag field indexes/encodings, actual AES-GCM AAD and second-segment meaning. See `docs/reviews/2026-09-24-r8-lwke1-package-key-independent-recovery.md`.

Historical SB-79 records one exact operation rejected by a previous environment. Do not reroute that forbidden operation. Continue the underlying recovery through genuinely permitted methods.

## Map Data correction

Do not continue the LW Atlas-inspired redesign.

Do not further optimize the wide-FOV scanner.

Do not treat the current Train list, Dispatch finder or any other Last War-native source as the intended product design unless the original LWBridge evidence proves that mapping.

The old complete traversal may remain as a temporary safety/reference oracle, but production parity must ultimately follow the recovered original LWBridge behavior.

## Whole-program scope

The project is no longer limited to Home and Map Data. Automation, Squads/AFK, City Layout, Hotkeys, Mini-games, Settings, and retained conditional/nested features in the reference are part of the parity target.

Account/Login/Authentication and all related activation, renewal, unbind, logout, entitlement, credential-persistence and account UI/backend surfaces are intentionally excluded by current owner direction and must not be reintroduced as parity backlog.

Other previously retired original features remain parity gaps unless separately excluded by a current explicit owner directive.

## Completed helper City Layout lane

The helper's exact original City Layout recovery is complete and preserved byte-for-byte at `docs/reviews/2026-09-24-r8-city-layout-exact-contract.md`. The current `CityLayoutPanel-B4B03XEi.js` is byte-identical to original 0.3.1 (`97dc2c5e0bf4fc5e3b3a058e704c02e21385b9c9511aeb9b3297ec3272c78022`) and all eight original API wrappers remain. R8-018 now implements the three draft persistence handlers; five gameplay-facing handlers remain missing/protected. The report recovers exact revisioned `city_layout_draft_v1` persistence, 500 ms autosave/status polling, bridge method names/timeouts, request/result boundaries, local issue codes and UI behavior. Protected placement planning/execution remains `PROTECTED_UNKNOWN` and must not be guessed.

## Worktree state

R8-002 reconciled the old pending R7 Map work and removed the abandoned finder probes. Begin each new checkpoint by confirming `git status`; do not accumulate unrelated experiments in the production worktree.

## Delivery

Every coherent checkpoint must update the parity matrix, recovery finding, backlog and handoff; run applicable checks; commit only its own files; push to `origin/research/offline-controller`; and verify the remote revision.
