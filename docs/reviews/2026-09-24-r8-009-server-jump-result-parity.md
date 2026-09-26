# R8-009 — restore original server_jump success envelope

**Date:** 2026-09-24
**Reference:** verified LWBridge 0.3.1 executable
**Scope:** public `server_jump` request/result identity only; protected game-side travel implementation remains outside this checkpoint.

## Authority

Primary authority:
- `docs/reviews/2026-09-24-r8-map-control-plane-exact-contract.md`
- immutable recovered Map frontend/API bytes.

Recovered public request:

`{serverId}`

Recovered successful response fields consumed by the original frontend:

`{changed, previousServerId, serverId}`

The destination `serverId` is observable: Auto Scan logs either a switch from `previousServerId` to `serverId` or an already-on-`serverId` result.
## Existing recovered errors

R8-009 does not redefine the already recovered validation/error surface:
- invalid server ID => `INVALID_SERVER_ID / server ID must be an integer from 1 to 99999`;
- conflicting game operation => `GAME_OPERATION_IN_PROGRESS / another game operation is already in progress`;
- authoritative travel never reaches target => `SERVER_JUMP_TIMEOUT / the game did not switch to the target server`.

The exact original numeric server-jump timeout remains UNKNOWN.
The rebuild's current timing is implementation policy and is not claimed as original parity.

## Deviation corrected

Before R8-009, the current-client source validated the target server internally but reduced the successful result to:

`{previousServerId, changed}`

The public Manual Map service therefore omitted original field `serverId`.

R8-009 carries the validated destination through `CurrentClientServerJumpResult` and publishes all three recovered success fields.
## Regression coverage

Deterministic tests now prove:
- same-server no-op returns `previousServerId == serverId == target` and `changed=false`;
- changed transition returns the original previous server, exact destination `serverId`, and `changed=true`;
- the service-level public result includes destination `serverId`;
- existing invalid-ID, busy-operation and timeout contracts remain intact;
- live proof helpers require the destination field for same-server, cross-server, return-home and multi-server Auto flows.

No frontend transform was needed. The recovered original frontend already expects `serverId`; this checkpoint fixes the backend envelope to satisfy it.

## Validation

Passed on 2026-09-24:
- `git diff --check`
- `dotnet build tests/LWBridge.Desktop.Checks/LWBridge.Desktop.Checks.csproj -c Release --nologo`
  - 0 warnings
  - 0 errors
- full deterministic checks DLL
  - `ok=true`
  - `failures=[]`
- `python tools/build_lwbridge_frontend.py --check`
- R7-147 Map Data owner-workflow browser regression
- R7-156 Auto navigation / refresh / reconnect / jump-first browser regression
- R7-136 Auto app/process restart browser regression

## Not claimed by R8-009

This checkpoint does not claim the current Last War travel mechanism is the original 0.3.1 protected implementation.

Still separate:
- exact original server-jump timeout duration;
- protected travel internals;
- `map_summary` exact result envelope;
- `map_data_options` exact result/selector behavior;
- original Auto Scan state machine;
- Scheduled Plunder;
- original Map acquisition internals.
