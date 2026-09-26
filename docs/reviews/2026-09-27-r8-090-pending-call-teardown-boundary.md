# R8-090 — bound pending-call teardown at the permitted metadata limit

**Date:** 2026-09-27
**Reference:** verified LWBridge 0.3.1
**Status:** RECOVERED BOUNDARY / BOUNDED NEGATIVE
**Scope:** non-executable Rust/source-location metadata for original `bridge_store.rs`, reconciled with the already-recovered connection-layer failure vocabulary. No production behavior change.

## Result

R8-090 does **not** find source-backed evidence that an ordinary named-pipe disconnect or write failure immediately completes every outstanding Lua-call waiter.

What it does recover more precisely is the original bridge-store call-wait boundary:

- `BRIDGE_STOPPED` with exact message `bridge result channel closed` is a distinct call-wait terminal branch;
- `LUA_CALL_TIMEOUT` is a separate timeout branch;
- read-only `bridge_store.rs` source-location metadata places the closed-channel branch around lines 498–501;
- a second metadata copy exposes source locations paired with `call` at line 554, `command` at line 605, and `timeout` at line 740;
- `PIPE_DISCONNECTED` and `PIPE_WRITE_FAILED` remain separately recovered connection-layer failure vocabulary.

No permitted read-only metadata link proves that generation-scoped route removal on ordinary pipe loss is the same operation as closing all result receivers.

## Recovered metadata

Primary `src\services\bridge_store.rs` source locations:

- line 498, column 34;
- line 499, column 34;
- line 501, column 15;
- line 979, column 33;
- line 980, column 28;
- line 983, column 13;
- line 991, column 16.

The same read-only cluster contains exact:

- `BRIDGE_STOPPED`;
- `bridge result channel closed`;
- `LUA_CALL_TIMEOUT`.

A second `bridge_store.rs` metadata copy exposes:

- line 168, column 13;
- line 554, column 13, adjacent descriptor `call`;
- line 605, column 24, adjacent descriptor `command`;
- line 740, column 25, adjacent descriptor `timeout`;
- line 979, column 5.

These locations are useful ownership evidence, but they do not reconstruct the executable control-flow edge from pipe teardown to result-channel lifetime.

## Teardown decision

R8-075 deliberately avoided eager route-wide pending-call failure because the causal mapping was unproven. R8-090 preserves that decision.

On current evidence it would still be an invention to claim any of the following:

- ordinary `PIPE_DISCONNECTED` closes every outstanding result receiver immediately;
- `PIPE_WRITE_FAILED` closes every outstanding result receiver immediately;
- generation-scoped route removal and bridge result-channel closure are one operation;
- every pending public call should immediately surface `BRIDGE_STOPPED` when one pipe session disappears.

The rebuild should therefore continue to:

- fail the write being performed when that write itself fails;
- remove the dead route/session according to the recovered transport lifecycle;
- avoid synthesizing eager `BRIDGE_STOPPED` for unrelated outstanding calls unless new source evidence proves it;
- allow those other calls to reach their command-specific result deadline under the current equivalent plumbing.

This is a preservation decision, not a claim that the rebuild's timeout behavior is exact original parity.

## Channel-type metadata

The binary contains global Rust metadata for Tokio/futures oneshot and mpsc implementations. This pass does not source-link a particular oneshot/mpsc concrete type to the bridge-store result registry strongly enough to make a contract claim.

Accordingly, R8-090 does not infer channel type, sender-drop timing, or receiver cancellation semantics from those global library strings.

## Verification

Hash-gated verifier:

`tools/inspect_lwbridge_pending_call_teardown_boundary.py`

Durable evidence:

`evidence/lwbridge-implementation/2026-09-27-r8-090-pending-call-teardown-boundary.json`

Reference SHA-256:

`2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

No production behavior changes in this checkpoint.
