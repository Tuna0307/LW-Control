# R8-075 — re-audit host↔proxy protocol and narrow remaining parity gaps

**Date:** 2026-09-26
**Reference:** verified LWBridge 0.3.1
**Scope:** reconcile the older broad “recover host↔proxy protocol” backlog item against R5/R7 live transport work and R8 public-status audits. No approximate protocol change is introduced.

## Result

The retained host-side protocol is substantially recovered and already live-working against the current game. The backlog was stale in still listing `hello.ack`, request/result grammar, basic correlation and listener/session construction as if they were unknown.

Recovered/live-proven layers include:

- per-user control-pipe identity and host-server/proxy-client direction;
- 4-byte little-endian framing with payload range 1..`0x800000`;
- exact version-1 `hello` envelope and authentication fields;
- exact `hello.ack` envelope with empty payload;
- exact `command` / `kind=call` request envelope;
- monotonic `cmd_<n>` IDs with first ID `cmd_1`;
- exact `result` payload fields and `payload.id` correlation ownership;
- protected listener ACL/options, five-second handshake deadline, reconnect generation and route removal;
- outbound/inbound queue and byte limits plus recovered I/O timeout values;
- production named-pipe authentication and read-only `getStatus` request/result transport, live-proven in R7-127.

This checkpoint does not relabel the whole original proxy/script implementation as recovered.
## Exact wire/session authority

The relevant earlier source-backed checkpoints remain authoritative:

- R5-006: per-user `\\.\pipe\lwbridge-control-v1-<sid-hash-prefix>` identity.
- R5-007: secure-proxy frame parser and exact `hello` construction.
- R7-097: `hello.ack`, `command/call`, `result`, result failure mapping, and `payload.id` correlation.
- R7-098: instance registration, claim, reconnect generation, default route, ordinary disconnect and explicit unregister.
- R7-099: outbound 4096 / 64 MiB, inbound 4096 / 32 MiB, 30-second inbound backpressure, 10-second write timeout and 30-second idle/activity timeout.
- R7-100: protected CreateNamedPipeW contract, accept/retry lifecycle, five-second handshake timeout and terminal generation-scoped route cleanup.
- R7-114/115: exact client-image canonicalization plus PID/path/build/token hello admission.
- R7-119/122: pending-call registry, native generic 200 ms call timeout, zero counter seed and first `cmd_1`.
- R7-120/121/123: authenticated duplex RPC session, host-global listener composition and normal-window startup binding.
- R7-124/125: live pending-count ownership, narrow `call_lua(getStatus,{})`, and successful-stop unregister.
- R7-127: real-game authenticated route and correlated `getStatus` result live proof.

R7-097 deliberately did not prove that inbound outer `requestId` must equal `payload.id`; the retained implementation therefore correlates by authenticated route plus `payload.id` and does not invent that equality check.
## Readiness and activity semantics

R8-043 proves that native `get_status.backend` / `xluaOnline` are selected from named-pipe route presence only. A route means `backend="named-pipe"` and `xluaOnline=true`; no route means `offline/false`.

R8-042 separately proves the retained single-profile `profile_instance_status.connectionState="connected"` rule:

`bridgeConnected && identityConfirmed && lastHeartbeatAt > 0 && nowMs - lastHeartbeatAt < 15001`

So public “transport online” and public “instance connected” are not the same predicate.

What remains unrecovered is the exact original wire-level `heartbeat` payload/schema and the exact native ownership chain that updates route activity / `lastHeartbeatAt` from that message. The rebuild currently obtains instance heartbeat evidence through its current-game Overview bridge plumbing, which R8-042 explicitly classifies as equivalent reimplementation.

## Disconnect / write / pending-call boundary

Native pipe vocabulary and terminal cleanup are exact at the connection layer:

- `PIPE_DISCONNECTED / named pipe connection closed`;
- `PIPE_WRITE_FAILED`;
- generation-scoped route removal from the terminal connection future;
- common connection I/O cleanup.

Native generic call waiting has distinct `BRIDGE_STOPPED / bridge result channel closed` and `LUA_CALL_TIMEOUT` branches. The current evidence does **not** yet prove the precise causal mapping from every connection disconnect/write failure to outstanding per-call result senders.
Therefore R8-075 does not change current pending-call teardown. The rebuild already fails the command being written when its write fails and drains all calls on application-host stop; other outstanding calls can otherwise reach their command-specific result deadline. Changing that to eager route-wide failure without source proof would risk introducing a new observable deviation.

## Original script-dispatch ownership remains open

The production rebuild transport is live-working but not the original game-side implementation.

Current retained path:

- `LWBridge.GamePipeAdapter.dll` runs a background named-pipe client inside LastWar.exe;
- it forwards authenticated host frames into `pipe-inbound-XXXXXXXX.json` mailbox files;
- `tools/current_overview_bridge.lua` consumes those files;
- the current Lua bridge accepts only `hello.ack` and the allowlisted `command/call getStatus {}` path;
- it writes `result` envelopes through `pipe-outbound-XXXXXXXX.json`;
- the adapter forwards those frames back through the recovered named pipe.

Those mailbox/lease/parser mechanics are explicitly rebuild implementation policy. They preserve the recovered wire for the live-proven read-only path but do not establish the original secure/plain xLua proxy’s internal `XluaBridgeHandlePipeMessage` / script-dispatch implementation.

## Classification after R8-075

Host protocol items no longer open: pipe identity, framing, hello, hello.ack, authentication, listener construction, command/call envelope, command ID seed, result envelope, payload-id correlation, queue/byte limits, principal timeouts, reconnect generation and route cleanup.

Still open for strict original parity:

1. exact wire `heartbeat` payload and native activity-update ownership;
2. exact disconnect/write-failure → outstanding-result-channel/public-call failure mapping;
3. original secure/plain xLua script-dispatch/provider implementation below the recovered host wire.

The host transport remains **LIVE-WORKING / EXACT_CONTRACT + EQUIVALENT_REIMPLEMENTATION plumbing**, not exact original proxy internals.
