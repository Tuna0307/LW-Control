# Overview startup and automatic-reconnection recovery

This document records the evidence used for O04 **Open games at startup** and O05 **Automatic Reconnection**. It supplements the accepted manual Overview lifecycle; it does not replace `LWB-OVL-003` or claim the still-unrecovered original protected launch proof/ticket protocol.

## LWB-OVR-005 — original reconnect gates, reasons and thresholds — 2026-09-12

**Status:** RECOVERED (original static). **Source:** `../LW/lwbridge-0.3.1.exe`, SHA-256 `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`, preferred image base `0x140000000`.

**Locators.** Recovery classifier `0x14033883A-0x140338CB0`; config getter `0x140339789-0x140339839`; persisted desired-running getter `0x140339DD6-0x14033A045`; worker `0x1400E579C-0x1400E6B0F`; process watcher `0x14033BF89-0x14033C37A`.

**Result.** `auto_force_update_reload` and `game_desired_running` are separate gates. Process exit needs two consecutive missing-process observations. The classifier carries `processExit`, `hang`, and `disconnect`; event requests additionally carry `forceUpdate`, `crossDisconnect`, and `exitPrompt`. The recovered classifier comparisons are 30,000/29,999 ms for hang, 59,999 ms for disconnect, and 180,000 ms for the longer connection/login-unavailable branch. Worker constants additionally prove a 15-minute update-no-activity boundary, 15-second stable verification, a 60-second disconnected-process wait, normal retry delays `15s,30s,60s,2m,5m`, and maintenance/update delays `2m,5m,10m`.

**Reproduction.** `python tools\inspect_lwbridge_game_recovery.py ..\LW\lwbridge-0.3.1.exe --pretty`. Durable output: `evidence/lwbridge-implementation/2026-09-12-overview-recovery-static.json`.

**Limits/impact.** These are original static contracts. They authorize the corresponding rebuild thresholds and two-gate/manual-stop behavior, but do not by themselves prove the current game exposes equivalent connection signals.

## LWB-OVR-006 — current-client connection observation contract — 2026-09-12

**Status:** RECOVERED current-client static + IMPLEMENTED/OFFLINE-TESTED observation path. **Sources:** current build `1.0.361 / 1078` `Assembly-CSharp.rdl`, size `15,640,576`, SHA-256 `871efe06819fbac438413eb96b7df8193d0be56094f3a44d5ff141e6219adcbd`; current `LWScripts.data`, size `41,269,242`, SHA-256 `09ddc4d1727bc0676ef6320db79814852cacc5c82b53551c703722052ebdbace`.

**Locators.** `GameEntry` field 2442 / method 3726 (`get_Network`, RVA `0x502a3`) and xLua row 24689 / RVA `0x24b340`; `NetworkManager::get_Logined` row 5740 / RVA `0xacd4f` and xLua row 26289 / RVA `0x27d194`; `get_IsConnected` row 5742 / RVA `0xacd60` and xLua row 26290 / RVA `0x27d1f0`; `get_IsConnecting` row 5743 / RVA `0xacd77` and xLua row 26291 / RVA `0x27d24c`. Player access reuses the recovered `GameEntry.Data -> CustomDataManager.Player -> DCPlayer` chain: `GameEntry::get_Data` row 3736, `DCPlayer::GetUid` row 3418, `GetCurServerId` row 3426, and `PlayerWorldPointId` field/property 2239/3416.

**Reproduction.** Run `python tools\inspect_lastwar_rdl_metadata.py <current Assembly-CSharp.rdl> --member Logined`, then `--member IsConnected`, `--member IsConnecting`, and `--type GameEntry`; the player owner/access chain is independently recorded by `LWB-R6-035`. `tools/current_overview_bridge.lua` reads only these xLua-visible current-client members and writes them into the already session-correlated heartbeat.

**Result.** The rebuild can observe `loggedIn`, `connected`, `connecting`, non-empty player UID, positive current server ID and positive world position without changing the accepted Overview readiness banner. Unknown/unavailable members produce `gameStateObserved=false`; the host deliberately does not reinterpret an unknown observation as a disconnect.

**Limits/impact.** This proves the current-client data surface, not that original LWBridge used these exact managed accessors. The mapping is a rebuild implementation route from source-backed current-client fields to the separately recovered original recovery predicates. Live disconnect behavior still requires bounded current-client verification.

## LWB-OVR-007 — original event-driven recovery entry contract — 2026-09-12

**Status:** RECOVERED (original static; production event watcher not yet integrated). **Source:** the same verified `lwbridge-0.3.1.exe` SHA-256 `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`.

**Locators.** Event parser/dispatcher `0x1403450C2-0x140345708` references `forceUpdate` at `0x140345569`, `crossDisconnect` at `0x1403455A8`, and `exitPrompt` at `0x1403456B5`; `disconnect` shares the event reason vocabulary adjacent to preferred VA `0x140C99D65`. Recovery start `0x140338FD4-0x140339356` initializes state text `waiting`. The worker comparison at `0x1400E6467` is `60,000 ms` before its disconnected-process action branch.

**Result.** Event-requested `disconnect`, `crossDisconnect`, `forceUpdate`, and `exitPrompt` enter recovery as `waiting`; the event path does not begin by force-terminating the game. `forceUpdate` sets update-detected true while the other three reasons do not. Original window-name fallbacks map `UIDisconnect -> disconnect`, `UICrossDisconnect -> crossDisconnect`, and `UIForceUpdateTip -> forceUpdate`. The current build-1078 `LWScripts.data` still contains those exact windows under `UI/UIDisconnect/...` and `UI/UIForceUpdateTip/...`.

**Reproduction.** The event assertions are part of `python tools\inspect_lwbridge_game_recovery.py ..\LW\lwbridge-0.3.1.exe --pretty`; current-package presence is reproduced by bounded string enumeration of the three named `LWScripts.data` entries. Durable original output remains `evidence/lwbridge-implementation/2026-09-12-overview-recovery-static.json`.

**Limits/impact.** The exact in-place-versus-terminate worker transition for every event reason is still under investigation. Production must not route these event reasons through the immediate hang termination path until that transition is source-attributed.

## LWB-OVR-008 — rebuild recovery core — 2026-09-12

**Status:** IMPLEMENTED/OFFLINE-TESTED; live O05 acceptance still pending. **Implementation:** `src/LWBridge.Desktop/OverviewLifecycleRecovery.cs`, `OverviewRecoveryPolicy.cs`, `OverviewLifecycleService.cs`, `LWBridgeBackend.cs`, `LWBridgeWindow.cs`, `tools/current_overview_bridge.lua`, and deterministic coverage in `tests/LWBridge.Desktop.Checks/Program.cs`.

**Result.** Successful manual/startup Launch persists desired-running true. Intentional Close clears it before cleanup. Reconnect OFF cancels active/future recovery without erasing the separate desired-running intent. Unexpected process loss requires two observations, restores the exact journaled session before relaunch, and relaunches only through the proven Overview lifecycle. Running-process recovery implements the recovered 30-second hung condition, 60-second bridge-offline condition, 180-second observed game-health condition, exact PID/path force termination only for recovery-eligible cases, official launcher/updater/sync suppression, recovered retry tables and 15-second stability verification. `game_recovery_status` and `bridge://game-recovery` now expose real recovery state.

**Validation.** `dotnet build src\LWBridge.Desktop\LWBridge.Desktop.csproj -c Release` succeeds with zero warnings/errors. `dotnet run --project tests\LWBridge.Desktop.Checks\LWBridge.Desktop.Checks.csproj -c Release -- --verify-real-config-unchanged` returns `ok:true` and no failures. Fake-clock tests cover 30s/60s/180s boundaries, unknown-state fail-safe, updater suppression, process-exit recovery, manual-Close non-resurrection, reconnect-disable cancellation, normal/maintenance retry families and stable verification. Durable offline result: evidence/lwbridge-implementation/2026-09-12-overview-recovery-core-offline.json.

**Limits/impact.** This checkpoint intentionally excludes production event-driven handling for `forceUpdate`, `crossDisconnect`, and `exitPrompt` until the remaining original worker transition is mapped. It is not LIVE-PROVEN. A bounded real-game crash/disconnect/reconnect test and owner-visible O04/O05 verification remain required before Overview is accepted.
