# Campaign 007 – original native Home/Map recovery boundary

## Proven original 0.3.17 outer host handles

Source: **exact hash-gated** original `lwbridge-0.3.17.exe`, SHA-256 `4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783`, recovered JS assets and PE machine-code xrefs. Fresh discovery `original-home-native-xrefs.json` reuses `inspect_map_surface.py:native_surface`; `original-host-three-handlers.json` uses `inspect_map_native_functions.py` against the accepted original `surface-discovery.json`. This is **exact executable provenance and handler/xref discovery, NOT source-level recovery of the encrypted Lua controller or proof of all inner host branches.**

| Original command | Original 0.3.17 handler candidate RVA | Evidence classification |
|---|---|---|
| `profile_instance_start` | `0x20715F–0x207CA9` | native xref; original frontend invokes selected profile with `closeUnmanaged:true` |
| `profile_instance_stop` | `0x199627–0x19AC62` | native xref; exact selected `instanceId` required at frontend |
| `profile_instance_status` | `0x1A0DA4–0x1A213C` | native xref |
| `profile_instances_reconcile` | `0x203021–0x2057DC` | native xref; original `autoLaunchAll` marker |
| `profile_instances_update_and_restart` | `0x2057DC–0x20715F` | native xref; updater execution prohibited in 007 |
| `profile_list` / `profile_select` | `0x14F19A–0x14FA46` / `0x10B70B–0x10C27F` | original host profile commands, compared with owned native profile graph tests |
| `game_root_select` / `game_root_status` | `0x188F12–0x18A2AD` / `0x12816E–0x128A38` | public weak-root predicate vs clone's separate strict launch admission is a documented adaptation |
| `game_recovery_status` | `0x154905–0x155144` | native xref; clone's retry threshold/cadence is NOT recovered by this |
| `map_scan_start` | `0x143D67–0x144C41` | native xref; host proven to call provider `enterWorldMap/startMapScan`; not provider body |
| `map_scan_stop` | `0x13A8C1–0x13B390` | native xref; host durable cancellation transaction independently reconstructed |
| `map_search` | `0x1761C1–0x176ED4` | native xref; query pagination/filter/storage semantics in RE-MAP-003 |

Fresh names `local_game_launch_status`, `local_config_get`, `local_config_set` and `BRIDGE_HOST_BUSY` do not appear as exact original command strings. They are **clone internals**, not quietly promoted to original command names. The `GAME_OPERATION_IN_PROGRESS` string has a native candidate at `0x41B3BC–0x41B451`; the string does not by itself establish the Home original start-in-flight policy in every state.

## Distinguish actual originally observed behavior from policies

Recovered original public Home source: `evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js`, hash `44c4e4043825b7db296b64171951b27176f8850ff8d7d337cc991df4765524c6`; accepted `docs/reviews/2026-10-02-LWB317-PM-015-home-audit.md` and `home-lane/findings.md` record original source bytes for Launch, Close, reconcile, error/busy and preference actions. Current canonical `src/LWBridge.UI-0.3.17/src/App.jsx` is source/local accepted; newly executed `--home-campaign-lifecycle-check`, `--profile-runtime-owner-check`, `--overview-reconnect-policy-check`, `--overview-close-timing-check`, `--overview-fault-admission-check` and `--overview-bridge-host-transport-check` are **clone production tests**, not actual original process failures.

Known **current rebuild policy** examples: `OverviewLifecycleRecovery.cs` `RecoveryMonitorCadence = 1s`; `MapScanTraversal.cs` row-major 20-tile logical targets; `MapScanEngine.cs` sequential pending-block consumption; `CurrentClientMap317ScanProvider.cs` max attempts per block = 2; `CurrentClientMapBlockSource.FastCity.cs` session settle 3s, AOI lease/view wait 500/150ms, multi-view footprint gap filling. Host normal/fast *reported concurrency* 8/20 is recovered original but does **not** prove current native request-level overlap/ordering equals original.

Original Home Auto Launch's local key semantics and frontend state/draft transitions are source-recovered. The clone additionally maintains a native acknowledged mirror, failure rollback, strict root admission, generation fencing and ownership. These are **adaptations/safety and observable-policy candidates**, not inferred original behaviors. No correction was made without an original semantic counterexample. The only newly demonstrated and corrected public Map query edge is numerical `page<1`, with recorded prior `INVALID_MAP_QUERY` and corrected `page=1` real Resource command replay.

## Actual private body recovery

Accepted `ORIGINAL-LUA-RECOVERY-006`: encrypted original LWBP `bridge-scripts.dat` payload and loader/crypto chain are byte-recovered. Neither valid matching signed `package-key.envelope` nor authorized matching persisted P-256 CNG state is in supplied verifiable inputs. No plaintext original Lua controller has been recovered. Therefore **original tile traversal, producer selection, request scheduling, retry/backoff, serialized row schema/exclusion and private Stop timing remain UNKNOWN**. The decoded current Last War v22 Lua is the game's code, *not* the commercial bridge's protected controller, and cannot stand in for it.

None of the above constitutes login/licensing service bypass, original dynamic game runtime access or a source-proven original 8,008-row expected census.
