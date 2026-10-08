# Pipe connect 003: independent lead review

Delivery: `e51ab590f1c2866136c2f57e6f936add2945dd11`.
Disposition: **ACCEPTED connection repair and bounded current-client live proof**.
Home/Map remains PARTIAL. Resource and canonical UI acceptance remain incomplete.

Inspected the Lua/adapter/runner diff, three attempts, opt-in receipts, auditor and
tests. Fresh delegate checks 4/4, file ownership 5/5 and production Lua leases 6/6
pass. Reran the three-attempt auditor in an external copy to preserve historical
audit bytes; direct-call attempts authenticate and the final production getStatus
RPC is evidenced. All three exact restorations validate against retained backup
manifests/databases. Fresh installed script triplet matches originals; current
process inventory has no LastWar/LWBridge process. Direct origin equals delivery.
No live game launch or desktop action was performed by this review. The worker's
broader Release/frontend/package/host suites were not all independently rerun here.

The actual-game negative/positive receipts support the diagnosis: userdata Connect
via the earlier reflection route returns without native entry, while one direct
call enters the adapter, opens the pipe, writes hello and reaches genuine host
authentication/RPC. Void/nil result does not trigger a second call. Token/PID/path/
build/profile checks remain intact. This is current-client clone binding proof,
not decrypted original-provider semantics or universal future-game compatibility.

## BW004-01: world-input blocker not established

The runner returns on `!isInWorld` before invoking `map_scan_start`.
Production already supports automatic world entry:

- `src/LWBridge.Map-0.3.17/MapScanStateMachine.cs` StartAsync, lines 166-167:
  when context.IsInWorld is false, call EnterWorldMapAsync.
- `src/LWBridge.Desktop/CurrentClientMap317ScanProvider.cs` EnterWorldMapAsync:
  delegates to source.GetCurrentContextAsync.
- `CurrentClientMapBlockSource.WorldReady.cs` GetCurrentContextAsync and
  EnsureWorldReadyAsync: write an identity-bound world-ready request and validate
  correlated response, including server/dimensions/session.
- `tools/current_overview_bridge.lua` begin_world_ready:
  uses the existing SceneUtils.ChangeToWorld(callback) path, then observes WorldScene.

Therefore `isInWorld=false` alone does not prove desktop input is required.
The existing transition might succeed or report a real dependency; it was not
exercised in these attempts. Credit the connection fix and genuine negative scene
observation, but do not accept the blanket external/UI-blocker conclusion.

The owner's background restriction concerns shared-desktop input/capture/focus.
The original pilot already authorizes the ordinary same-server city-to-world
transition. Exercising that existing provider path without OS input is in scope;
do not bypass a legitimate login/update/world failure or fabricate readiness.

Next solo task: BACKGROUND-WORLD-RESOURCE-004, correct the proof runner's premature
gate, exercise the production world-entry path and continue fresh Resource proof,
completed/cancelled distinction, owned Stop and restoration. No new desktop access,
cross-server activity, claim/plunder, recurring Auto or protected-service access.
