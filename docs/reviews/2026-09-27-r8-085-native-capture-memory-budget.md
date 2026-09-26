# R8-085 — recover native-capture memory budget

**Date:** 2026-09-27
**Reference:** verified LWBridge 0.3.1
**Status:** RECOVERED CONTRACT
**Scope:** safe-region point/march pending-record memory accounting. No production scanner change.

## Result

Both verified proxies enforce one shared **32 MiB (0x02000000-byte)** accounting budget for pending full point and march records.

The shared byte counter is plain RVA `0x91FB8` and secure RVA `0x90FB8`. It is increased when point/march entries are inserted or replaced and decreased by the exact record footprint when those entries are drained.

This is separate from R8-084's 65,536 aggregate pending-record count ceiling.
## Exact footprint formulas

Point footprint is:

`0x3A0 + sum(stringCapacity + 1 for each present optional string)`

The 12 charged point strings are `ownerUid`, `allianceId`, `playerName`, `alAbbr`, `name`, `gatherUid`, `gatherAllianceId`, `runtimeClass`, `eventId`, `allianceAbbr`, `ownerName`, and `killerId`.

March footprint is:

`0x1B0 + sum(stringCapacity + 1 for each present optional string)`

The five charged march strings are `ownerUid`, `ownerName`, `allianceUid`, `allianceName`, and `allianceAbbr`.

The capacity term is the native string object's `+0x18` capacity field, not logical text length at `+0x10`. R8-085 verifies that distinction against the same optional-string layout used by R8-083.
## Replacement and overflow semantics

The budget check is growth-only. If a replacement has equal or smaller footprint than the already-pending record, it is admitted without requiring free budget. If it grows, only `newFootprint - oldFootprint` is compared with `32 MiB - currentAccountedBytes`.

Point producer uses shared helper `0x36C70`; march producer inlines the same arithmetic. Both overflow branches call native dropped helper `0x3A210`, which increments the same `dropped` counter serialized by the native-capture envelope.

When records drain, point footprint helper `0x3B320` and march footprint helper `0x3B280` return the amount subtracted from the shared counter.

Queue-node allocation includes an extra `0x18` bytes of linkage: drained point nodes free `0x3B8` bytes and march nodes `0x1C8`, while accounting bases remain `0x3A0` and `0x1B0`. Therefore that container-node overhead is outside this 32 MiB accounting formula.
## Reset and limits

On native-capture run-ID change, the serializer path clears all four pending queues, then zeroes the dropped counter and the shared byte counter before continuing with the new run.

Removal queues contain fixed scalar identities and are not charged by this point/march byte counter.

This checkpoint does not inspect or cross protected native region `0x3F8E0-0x40A6D`. It does not recover block traversal/order/coordinates, per-tick request pacing, retry/backoff, or protected acknowledgement semantics inside `XluaBridgeMapScanTick`.

## Verification

Hash-gated verifier: `tools/inspect_lwbridge_native_capture_memory_budget.py`

Durable evidence: `evidence/lwbridge-implementation/2026-09-27-r8-085-native-capture-memory-budget.json`

Reference SHA-256: `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

No production scanner behavior is changed.
