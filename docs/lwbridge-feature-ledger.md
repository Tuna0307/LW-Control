# LWBridge Overview and Map Data feature ledger

Checkpoint: 2026-09-08. Status vocabulary follows `task.md`: **RECOVERED** is
original static behavior, **IMPLEMENTED/OFFLINE-TESTED** is rebuild behavior
proved without a live state change, **LIVE-PROVEN** requires current-client
before/after evidence, and **UNKNOWN/BLOCKED** remains open.

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
| S06 Cross-server | `server_jump({serverId})`, history commands | RECOVERED API / BLOCKED | UI contract recovered; authoritative current-server travel and conflict handling not implemented. |
| S07 Navigation | recovered React `Activity` views/listeners | IMPLEMENTED/OFFLINE-TESTED foundation | All nine deterministic desktop captures pass after adapter integration. Live scheduler ownership still needs audit. |
| O01 Game root | `game_root_status()`, `game_root_select()` | IMPLEMENTED/OFFLINE-TESTED | Validates official launcher, game exe, original xLua, PE32+ architecture, readability; detects current installed root; invalid selection is not persisted. UI folder picker is native. |
| O02 Launch Game | `profile_instance_start({profileId,closeUnmanaged:true})` | RECOVERED API / UNKNOWN-BLOCKED bootstrap | Original staged launcher/hook/proxy sequence recovered. Official `Launcher.log` now independently proves manifest/table/Lua/pack verification precedes current-style game launches and records graceful relaunch preparation. Exact descriptor/proof/ticket decoding and official command-line/environment contract remain open. Rebuild fails closed with `OVERVIEW_LAUNCH_BOOTSTRAP_UNRECOVERED`; no shortcut launch. |
| O03 Close Game | `profile_instance_stop({profileId,instanceId})` | RECOVERED API / BLOCKED | No owned instance exists yet; rebuild refuses to stop an unmanaged game. Ownership/cleanup comes after O02. |
| O04 Launch at app startup | original default on unless stored false; reconcile/start lifecycle | PARTIAL IMPLEMENTED | Preference is now persisted by native config and fixture mode always ignores live startup. Reconcile currently does not launch until O02 is complete. |
| O05 Auto reconnect | `set_automation(name:"autoForceUpdateReload",enabled)` / `auto_force_update_reload` | RECOVERED mapping + PARTIAL IMPLEMENTED | Preference is persisted and surfaced. Recovery state machine and live disconnect handling remain BLOCKED on O02/bridge readiness. |
| O06 Recovery/repair UI | `game_recovery_status`, `bridge://game-recovery` and recovered state labels | PARTIAL | Native event boundary exists; real waiting/updating/repairing/launching/verifying/maintenance transitions remain UNKNOWN/BLOCKED. |

## Map Data matrix

| ID | Original contract / trigger | Status | Next proof required |
|---|---|---|---|
| M01 Start/stop/mode/type selection | `map_scan_start(payload)`, `map_scan_stop()` | RECOVERED API / BLOCKED | Bridge-ready runtime and exact scan payload/state ownership. |
| M02 Native capture | proxy/native map capture path | RECOVERED architecture / BLOCKED | Current-client capture records and handler correlation. |
| M03 Typed persistent index | map index service | UNKNOWN/BLOCKED | Recover per-kind authoritative schemas and stable identity/removal rules. |
| M04 Progress/failure/stop/resume | `map_scan_status`, `bridge://map-scan-status` | RECOVERED API / BLOCKED | Current run identity, checkpoint, drain/commit completion gates. |
| M05 Clear | `map_scan_clear({serverId})` | RECOVERED API / BLOCKED | Transactional per-server store implementation and late-response rejection. |
| M06 Result tabs/actions | `map_data_options`, `map_search` plus conditional commands | RECOVERED API / BLOCKED | Per-kind column/row semantics and live representative samples. |
| M07 Search/filter/sort/page/live refresh | `map_search({kind,query})` | RECOVERED API / BLOCKED | Typed query schema, stable sorting and stale-response suppression. |
| M08 Mark/unmark | `map_player_mark_set` | RECOVERED API / BLOCKED | Persistence identity behavior across rescan/restart/relocate. |
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
| WebView command/event transport | original Tauri invoke/listen wrapper | `LWBridgeWindow.cs` + `WebUi/preview-host.js` | Request/session IDs, origin validation, structured errors, timeouts/cancellation, event allowlist/listener lifecycle. Release build passes. |
| Fixture isolation | rebuild requirement | `preview-host.js`, bootstrap `mode=fixture` under `--capture` | Nine desktop captures pass; capture mode ignores WebView native messages and never falls through to live backend. |
| Stable local profile | original implicit profile routing | persisted local profile ID in `LocalConfigStore.cs`; `Le(selectedProfileId)` remains active in recovered frontend | Foreign profile self-check rejects with `PROFILE_SCOPE_MISMATCH`. |
| Game-root validation | `game_root_status`, `game_root_select` | `GameInstallationService.cs` | Current install validates; generated missing path fails closed; 64-bit game/xLua confirmed. |
| Launch safety gate | `profile_instance_start` | `LWBridgeBackend.cs` | Self-check requires `OVERVIEW_LAUNCH_BOOTSTRAP_UNRECOVERED`; no unmanaged launch is attempted. |

## Current verification commands

```powershell
python tools/build_lwbridge_frontend.py --check
python tools/inspect_official_runtime.py --output evidence/official-runtime/2026-09-08-official-runtime.json
dotnet build src/LWBridge.Desktop/LWBridge.Desktop.csproj --configuration Release
dotnet run --project tests/LWBridge.Desktop.Checks/LWBridge.Desktop.Checks.csproj --configuration Release
./tools/capture_lwbridge_ui.ps1
```

At this checkpoint all five complete successfully. The deterministic captures
cover presentation/fixture behavior only; they are not live-game proof.
