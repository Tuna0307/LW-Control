# R8-089 — recover safe host↔proxy heartbeat boundary

**Date:** 2026-09-27
**Reference:** verified LWBridge 0.3.1
**Status:** RECOVERED CONTRACT
**Scope:** non-executable original-host bridge-pipe metadata plus safe secure/plain proxy heartbeat classification. No production behavior change.

## Result

R8-089 narrows the remaining heartbeat gap without claiming the still-protected Lua/package serializer.

The original host's non-executable `src\services\bridge_pipe.rs` metadata contains the exact heartbeat JSON pointer:

`/payload/time`

in the same local bridge-pipe metadata cluster as pipe activity state and the idle-timeout error vocabulary.

Both verified embedded proxies also contain the same safe native classifier at `RVA 0x1A3C0-0x1A74A`. That function receives an already serialized message string, recognizes exact type string `heartbeat` as category 2 and exact type string `result` as category 1, then forwards the serialized message through the next native layer. It is not the heartbeat JSON serializer itself.
## Host bridge-pipe metadata

Hash-locked host raw locators include:

- `0xD091A4`: `PIPE_FRAME_INVALIDsrc\services\bridge_pipe.rs`
- `0xD0923D`: `pipe_connected=`
- `0xD0924E`: ` pipe_generation=`
- `0xD09261`: ` activity_generation=`
- `0xD09278`: ` pipe_activity_at=`
- `0xD0928C`: ` pipe_activity_age_ms=`
- `0xD092A4`: ` pipe_heartbeat=`
- `0xD092B6`: ` inbound_frames=`
- `0xD092C8`: ` writer_ready=`
- `0xD092F0`: `inbound frame is available`
- `0xD09358`: `/payload/time`

The adjacent timeout cluster contains exact `PIPE_IDLE_TIMEOUT` and `named pipe received no frames before the activity timeout`.

R7-099 remains authoritative for the exact **30,000 ms** idle/activity timeout. R8-089 does not re-derive that numeric value from metadata; it ties the already-recovered timeout to the exact no-inbound-frame semantics visible beside the heartbeat/activity metadata.
## Generic envelope context

R5/R7 remain authoritative for the authenticated version-1 envelope:

- `version`
- `type`
- `profileId`
- `instanceId`
- `requestId`
- numeric `timestamp`
- object `payload`

The R8-089 host metadata independently contains `version`, `requestId`, `timestamp`, and the generic invalid-envelope diagnostic in the same bridge-pipe region, but this checkpoint does not claim to supersede the earlier exact envelope recovery.

The new heartbeat-specific fact is the source-linked `/payload/time` path. This metadata evidence alone does **not** prove the JSON numeric width/range or semantic clock source for `time`.
## Proxy native classifier

For both secure and plain proxies:

- `0x1A526`: require candidate type length 9;
- `0x1A52F`: reference exact `heartbeat`;
- `0x1A536`: compare helper;
- `0x1A53F`: successful heartbeat classification sets category 2;
- `0x1A552`: result candidate length 6;
- `0x1A55B`: reference exact `result`;
- result equality maps to category 1;
- `0x1A698-0x1A69E`: the category and already serialized string are passed onward.

The literal RVAs are identical in both images: `heartbeat` at `0x71A90`, `result` at `0x71A9C`.

This is important ownership evidence: the safe native proxy layer does not construct the heartbeat JSON here. It classifies a string produced below/behind this boundary, consistent with the original Lua/package side owning heartbeat serialization.
## What remains unknown

R8-089 does **not** establish:

- the exact JSON type/range or clock source of `payload.time`;
- the heartbeat `requestId` value;
- whether top-level `timestamp` equals `payload.time`;
- the protected Lua/package heartbeat serializer;
- the exact heartbeat emission cadence;
- whether public/native `lastHeartbeatAt` stores `payload.time`, host arrival/activity time, or another value.

R8-042 remains authoritative that public `profile_instance_status` treats heartbeat as fresh when `now - lastHeartbeatAt < 15001` ms. R8-089 does not bridge that final ownership gap.

The current uncommitted five-second heartbeat writer in `tools/current_overview_bridge.lua` is therefore still **EQUIVALENT_REIMPLEMENTATION**, not exact parity. Its `payload.time` shape is now source-compatible at the field-path level, but its cadence and value/field-equality choices remain implementation policy.
## Verification

Hash-gated verifier:

`tools/inspect_lwbridge_pipe_heartbeat_boundary.py`

Durable evidence:

`evidence/lwbridge-implementation/2026-09-27-r8-089-pipe-heartbeat-boundary.json`

Reference SHA-256:

`2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

Verified embedded proxies:

- secure: `481c636b8d0fa9bf4b8f88cba77145054b33e3701ffa42737567b211859ac400`
- plain: `c9a88ec449cc6a9929fb2f2800ebd443f9b9dcdac849e3d3e8da94281dea9794`

No production behavior changes in this checkpoint.
