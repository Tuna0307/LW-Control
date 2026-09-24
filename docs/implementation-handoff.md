# Current implementation handoff — strict parity phase

**Project:** Last War Bot / LW-Control
**Branch:** `research/offline-controller`
**Current checkpoint:** `LWB-R8-020`
**Date:** 2026-09-25

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
