# LWBridge Overview and Map Data feature ledger

Checkpoint: 2026-09-08. Status vocabulary follows `task.md`: **RECOVERED** is
original static behavior, **IMPLEMENTED/OFFLINE-TESTED** is rebuild behavior
proved without a live state change, **LIVE-PROVEN** requires current-client
before/after evidence, and **UNKNOWN/BLOCKED** remains open.

## Project-manager review 4 — implementation `5174cf9` (2026-09-08)

See [the source audit and R1–R10 exit criteria](lwbridge-project-status.md). The tables below credit implemented slices only: no complete Overview lifecycle or production Map Data workflow is LIVE-PROVEN. Milestone A environment hashes below are the earlier recorded baseline, not a fresh runtime fingerprint from this review.

- R1 and independent PM2 checks pass. R3/R4 native-host ownership, responsiveness and preference interaction cases pass, including PM3-01 visible errors and PM3-02 failed-overlap reconciliation/recovery. Real lifecycle/scan worker integration and diagnostics remain open.
- O04/O05 currently save preferences only. O02 start rejects; O03 stop rejects; O06 recovery is constant idle state. `profile_instances_reconcile` is a status read, not startup launch.
- Map start only normalizes/rejects. Map stop returns unavailable state; it has no running service to cancel. Summary zeroes describe unavailable storage, not a successful empty scan.
- Server-jump history is now validated/persisted per local profile. Localization still returns an empty dictionary and logging is a no-op. Do not mark those dependencies complete based on successful RPC responses.
- M03/M05–M09 and durable job-store portions can be recovered/implemented/tested offline during R5 bootstrap research. Their end-to-end/live acceptance remains open; “blocked” must not prevent independent offline work.
- Review 4 freshly reran the standard backend suite, preference provider matrix/original PM3 reproducer, actual isolated WebView host, nine fixtures, 35 browser checks and 32 pixel comparisons. PM3 feedback/reconciliation is accepted. See [review 4 evidence](../evidence/lwbridge-implementation/2026-09-08-pm-review-4.json); no new binary extraction or live-game outcome was claimed.
- `LWB-R6-005` adds offline-tested alliance/no-alliance, resource/monster name-key and truck/railway item predicates to the earlier default search. Remaining filters, native keys, options/summary/export and real ingestion still need work.
- `LWB-R6-006` adds offline-tested dispatch/ghost `specialOnly` and truck/railway `reindeerOnly` predicates. It does not resolve adjacent quality, clock, plunderability, treasure or alternate-sort semantics.
- `LWB-R6-007` adds offline-tested case-insensitive literal-substring keyword search across `name`, `alliance_name`, `uuid` and `data_json`, including the original wildcard escape order and four parameter copies.
- `LWB-R6-008` adds an **IMPLEMENTATION POLICY** read snapshot around count/page SQL; a second WAL connection can commit mid-search without producing mismatched `{rows,total}` generations.
- `LWB-R6-009` recovers the option aggregation SQL family/latest scan-summary query and adds offline-tested treasure option-pair plus dispatch selected-level filtering. Time-bearing option/query branches remain gated.
- `LWB-R5-004` includes prior observed static findings but its new verifier execution was denied; `005` relies on retained bounded disassembly after broader analysis was denied. Preserve those provenance limits. Use [DB-01–06 and SB-01/02](deep-binary-handoff.md) for research routing; do not interpret a model change as permission to replay denied operations.

## Milestone A environment evidence

| Item | Current evidence |
|---|---|
| Reference executable | `lwbridge-0.3.1.exe`, SHA-256 `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff` |
| Branch / starting HEAD | `research/offline-controller` / `cb762a1ad2bb6a856f0834673217ecf8da01e204` |
| Installed root | `%LOCALAPPDATA%\FunFly\Last War-Survival Game` |
| Official launcher | SHA-256 `B6E29176F64C11A6F203D414FB50EE1E02293BE318EFDA9C33CF221C83D763BC` |
| Current `Game\LastWar.exe` | SHA-256 `DF5ABCF8618D48500BEFA9F587B509ED4F58373FF34932BB87CE217F0CF267D5`; file/product version `2019.4.40.16762411` |
| Current original xLua | SHA-256 `21EB704AFDB7E528F4B90FA1B90BF414C221B06BA990D625AAAAED31B292740F` |
| Baseline process state | No matching `LastWar` or `LastWarLauncher` process during the self-check |
| Official runtime evidence | `docs/official-runtime-architecture.md` + `evidence/official-runtime/2026-09-08-official-runtime.json`; read-only inspector reproduces version/hash/PE/container/hot-update/launcher-log evidence |

## Shared / Overview matrix

| ID | Original contract / trigger | Rebuild status | Proof / remaining gate |
|---|---|---|---|
| S01 Game status | `get_status(profileId)`, `proxy_status(profileId)`, `bridge://status` | IMPLEMENTED/OFFLINE-TESTED foundation | Native adapter reports real process existence separately from `xluaOnline=false`; bridge heartbeat/session readiness is still UNKNOWN/BLOCKED until bootstrap transport is complete. |
| S02 Pending-task counter | `get_status.pending` | UNKNOWN/BLOCKED | Exact original semantics still need recovery; do not treat an arbitrary queue length as authoritative. |
| S03 Refresh Status | status + proxy + recovery calls; recovered frontend also uses allowlisted `call_lua("getStatus", ...)` | PARTIAL IMPLEMENTED | Real local status calls route through native RPC. Current runtime `getStatus` call is BLOCKED on bridge bootstrap. |
| S04 Theme | original local UI state + `set_window_theme` | IMPLEMENTED/OFFLINE-TESTED | Existing fixture captures remain clean after native adapter integration. |
| S05 Language | nine recovered bundles + `lastwar_localize` | PARTIAL | Nine UI bundles preserved. Runtime-derived names remain BLOCKED until live bridge/localization path is available. |
| S06 Cross-server | `server_jump({serverId})`, history commands | PARTIAL IMPLEMENTED / BLOCKED live travel | Recovered history migration/set contract is persisted per local profile: integer server IDs 1–99999, unique, capped at five. Authoritative current-server travel and conflict handling remain unimplemented. |
| S07 Navigation | recovered React `Activity` views/listeners | IMPLEMENTED/OFFLINE-TESTED foundation | All nine deterministic desktop captures pass after adapter integration. Live scheduler ownership still needs audit. |
| O01 Game root | `game_root_status()`, `game_root_select()` | RECOVERED result contract + PARTIAL IMPLEMENTED/OFFLINE-TESTED | Recovered Overview reads `{canceled,valid}` from picker result. Native folder picker now preserves cancel as `{canceled:true}` and returns invalid status for the recovered `INVALID_GAME_ROOT` branch; real WebView probe verifies busy/cancel/invalid. Required-file + AMD64 (`0x8664`) PE32+ checks pass. `LWB-R5-002` now reproduces the exact xLua export ABI selector and classifies current build `1.0.361 / 1078` as the bundle's `secure` ABI. |
| O02 Launch Game | `profile_instance_start({profileId,closeUnmanaged:true})` | RECOVERED API + host envelope/ABI selector + partial proof/ticket lifecycle / UNKNOWN-BLOCKED bootstrap | Original staged launcher/hook/proxy sequence recovered. `LWB-R5-001` proves the outer host `LaunchEnvelope` producer/handoff. `LWB-R5-002` proves the `LWXE1\n` + ordinal/name SHA-256 ABI algorithm and exact secure/plain values; the current official xLua matches `secure`. `LWB-R5-003` proves child `LaunchEnvelope` parsing, descriptor expiry/shape gates, two-segment proof signature/timing checks and `LWLT1`/`LWLT2` ticket grammar. `LWB-R5-004` records previously observed static outer `LeaseActivationResponse.launchProof` propagation, the exact `primary_official` / `cached_reusable` / `independent_official` ticket-source taxonomy, `ticket_missing` transition and cached `pid`/ticket/expiry fields before fallback. `LWB-R5-005` records from retained bounded disassembly the child post-start poll branches: length/sequence mismatch -> `LAUNCH_TICKET_OWNERSHIP_CHANGED`, stored-deadline crossing -> `LAUNCH_TICKET_CONSUMPTION_TIMEOUT`, and mapped result variants -> `LAUNCH_TICKET_CONSUMPTION_FAILED`; opaque input identities, units and variant semantics remain open. Producer/consumer and external trust-service boundaries, remaining descriptor semantics, `LWLT2` extra-field meaning, poll caller/input/unit/variant meanings and child argument construction remain open (DB-01/02). The `004` verifier and broader `005` analysis have the recorded SB-01/02 restrictions; neither was retried in review 4. Rebuild fails closed with `OVERVIEW_LAUNCH_BOOTSTRAP_UNRECOVERED`; no shortcut launch. |
| O03 Close Game | `profile_instance_stop({profileId,instanceId})` | RECOVERED API / BLOCKED | No owned instance exists yet; rebuild refuses to stop an unmanaged game. Ownership/cleanup comes after O02. |
| O04 Launch at app startup | original default on unless stored false; reconcile/start lifecycle | PARTIAL IMPLEMENTED/OFFLINE-TESTED preference | Native config persists the preference; actual React/WebView tests verify visible rejected-save feedback, rollback to the last confirmed persisted value, ordered rapid saves, both-fail/mixed failures and successful recovery. Fixture/probe modes suppress live startup. Reconcile still does not launch until O02 is complete. |
| O05 Auto reconnect | `set_automation(name:"autoForceUpdateReload",enabled)` / `auto_force_update_reload` | RECOVERED mapping + PARTIAL IMPLEMENTED | Preference is persisted and surfaced. Recovery state machine and live disconnect handling remain BLOCKED on O02/bridge readiness. |
| O06 Recovery/repair UI | `game_recovery_status`, `bridge://game-recovery` and recovered state labels | PARTIAL | Native event boundary exists; real waiting/updating/repairing/launching/verifying/maintenance transitions remain UNKNOWN/BLOCKED. |

## Map Data matrix

| ID | Original contract / trigger | Status | Next proof required |
|---|---|---|---|
| M01 Start/stop/mode/type selection | `map_scan_start(payload)`, `map_scan_stop()` | RECOVERED API / BLOCKED | Bridge-ready runtime and exact scan payload/state ownership. |
| M02 Native capture | proxy/native map capture path | RECOVERED architecture / BLOCKED | Current-client capture records and handler correlation. |
| M03 Typed persistent index | map index service | RECOVERED SCHEMA / IMPLEMENTED-OFFLINE-TESTED FOUNDATION | Original SQLite tables/indexes and stored identity `(kind,server_id,record_key)` are recovered and reproduced. Restart/upsert/clear tests pass. Per-kind native `record_key` derivation and full normalization remain UNKNOWN/BLOCKED. |
| M04 Progress/failure/stop/resume | `map_scan_status`, `bridge://map-scan-status` | RECOVERED API / BLOCKED | Current run identity, checkpoint, drain/commit completion gates. |
| M05 Clear | `map_scan_clear({serverId})` | IMPLEMENTED/OFFLINE-TESTED SLICE | Backend/store transaction deletes selected-server scan runs and records, preserving marks and other servers; tests pass. Active-run cancellation, generation/late-response rejection and failure/restart cases remain open. |
| M06 Result tabs/actions | `map_data_options`, `map_search` plus conditional commands | PARTIAL IMPLEMENTED/OFFLINE-TESTED | `LWB-R6-003` pins the query-builder field/sort vocabulary and supported persisted `map_search` slice. `LWB-R6-004` pins the consumed `map_data_options` response envelope/nested fields and all eight real tab query families. `LWB-R6-005/006/007/009` add verified backend predicates, and `009` also recovers alliance/name/dispatch-level/treasure/reward option SQL plus newest non-discarded scan-progress selection. Public options remain blocked on the reward `arriveTs` cutoff producer/unit; conditional actions, per-kind native normalization and live representative samples remain open. |
| M07 Search/filter/sort/page/live refresh | `map_search({kind,query})` | PARTIAL IMPLEMENTED/OFFLINE-TESTED | `LWB-R6-003` implements server/kind scope, recovered `LIMIT/OFFSET`, `updatedAt asc|desc` plus `record_key ASC`, `{rows,total}`, city mark projection and `markedOnly`. `LWB-R6-004` recovers the frontend stale-search guard and eight-tab envelope distinctions. `LWB-R6-005/006/007/009` recover and offline-test keyword literal-substring search, city alliance/no-alliance, resource/monster name keys, truck/railway retained-item and reindeer-only, dispatch/ghost special-only, treasure option-pair filtering and dispatch selected-level filtering. `LWB-R6-008` keeps count/page on one SQLite read snapshot as an explicit rebuild policy and proves it against a concurrent WAL writer. Remaining time-dependent filters, quality/treasure visibility semantics and non-`updatedAt` sorts stay fail-closed with IMPLEMENTATION POLICY `MAP_QUERY_UNRECOVERED`. |
| M08 Mark/unmark | `map_player_mark_set`, `bridge://player-mark-changed` | IMPLEMENTED/OFFLINE-TESTED | Original mark key `(server_id,owner_uid)`, upsert/delete, clear-survival and frontend refresh event are recovered and reproduced. Live player-tracker relocation/state transitions remain unproven. |
| M09 Export | `map_city_export` | RECOVERED API / BLOCKED | Full filtered snapshot export and reopen verification. |
| M10 Jump/follow | `map_coordinate_jump`, `map_march_follow` | RECOVERED API / BLOCKED | Authoritative game focus/follow outcome. |
| M11 Auto Scan | recovered frontend orchestration + scan/server-jump calls | RECOVERED frontend / BLOCKED | Single-owner durable scheduler, multi-server travel, return-to-origin. |
| M12 Treasure | treasure refresh/claim/status commands | RECOVERED API / BLOCKED | Per-player state and authoritative claim result with suitable target. |
| M13 Dispatch plunder schedule | dispatch schedule/cancel commands | RECOVERED API / BLOCKED | Durable job identity, expiry/eligibility and outcome reconciliation. |
| M14 Truck/train actions | truck schedule/cancel and recovered row actions | RECOVERED API / BLOCKED | Current eligibility and authoritative plunder/train outcomes. |
| M15 Scheduled Plunder tab | `map_plunder_jobs_list` | RECOVERED API / BLOCKED | Durable job store and state reconciliation. |
| M16 Alliance share | `map_dispatch_share_alliance` | RECOVERED API / BLOCKED | Offline payload validation first; live delivery only under messaging authorization. |

## Native adapter implementation ledger

| Capability | Original source | Rebuild implementation | Validation |
|---|---|---|---|
| Command names/payload wrappers | `evidence/lwbridge-0.3.1/frontend/assets/api-ClPPi2JT.js` | `src/LWBridge.Desktop/LWBridgeBackend.cs` | Recovered wrapper strings inspected; unsupported production commands reject explicitly. |
| WebView command/event transport | original Tauri invoke/listen wrapper + rebuild host policy | `LWBridgeWindow.cs` + `WebUi/preview-host.js` + request/subscription registries | Active-profile injection/event envelopes restored. Isolated native WebView proves duplicate active-ID rejection, reload rotation/cancel/subscription reset, stale-session and external-navigation rejection, structured errors, responsive slow storage and no late publication after real window close. Real lifecycle-worker state mutation remains gated on R5/R7. |
| Fixture/read-only isolation | rebuild requirement | `preview-host.js`, `LocalConfigStore`, bootstrap/probe modes | Fixture commands cannot reach live handlers; capture/read-only modes remain isolated. The native host probe uses a unique temp config plus in-memory map DB, reports startup auto-launch suppressed, touches no user config and performs no live-game command. Requested live without native transport still fails visibly. |
| Stable local profile | original profile routing + rebuild persistence policy | versioned config plus recovered U/W routing | Specified PM2 config/identity regressions pass. Single-save rollback, visible errors, rapid both-fail/mixed-success reconciliation and post-failure recovery pass in provider and real native-host tests. |
| Game-root validation | `game_root_status`, `game_root_select` | `GameInstallationService.cs` + native picker | Installed/missing-root diagnostics and recovered cancel/invalid picker behavior pass in the isolated real WebView host. Both installed `LastWar.exe` and `xlua.dll` report AMD64 `0x8664/PE32+`; `LWB-R5-002` separately proves the exact export ABI fingerprint selector and current secure classification. |
| Launch safety gate | `profile_instance_start` | `LWBridgeBackend.cs` | Self-check requires `OVERVIEW_LAUNCH_BOOTSTRAP_UNRECOVERED`; no unmanaged launch is attempted. |

## Current verification commands

```powershell
python tools/build_lwbridge_frontend.py --check
python tools/inspect_official_runtime.py --output evidence/official-runtime/2026-09-08-official-runtime.json
dotnet build src/LWBridge.Desktop/LWBridge.Desktop.csproj --configuration Release
dotnet run --project tests/LWBridge.Desktop.Checks/LWBridge.Desktop.Checks.csproj --configuration Release
node tools/check_lwbridge_live_transport.cjs
node tools/check_lwbridge_transport_boundary.cjs
./tools/capture_lwbridge_ui.ps1
```

Review 4 reran generation/build, the standard backend suite, the provider matrix,
the original PM3 reproducer, actual isolated WebView host, nine fixtures and the
35-browser/32-pixel comparison matrix. Console storage is isolated; installed-client
diagnostics are optional unless `--require-installed` is supplied. The standard
suite covers the PM2 fixes; the standalone PM2 reproducer was last rerun in the
earlier audit. No live-game functionality is proven by these checks. See
[review 4 evidence](../evidence/lwbridge-implementation/2026-09-08-pm-review-4.json)
for current results and the separate recorded binary-analysis restrictions.
