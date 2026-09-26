# R8-082 — recover native-capture service and flush gates

**Date:** 2026-09-27
**Reference:** verified LWBridge 0.3.1
**Scope:** secure/plain native world-capture serializer service/emission policy. No production scanner change.

## Result

Both verified proxies use the same native-capture serializer at `RVA 0x38AB0-0x39F15`.

Before drain/serialization work, the function calls `KERNEL32!GetTickCount64`, subtracts the previous service timestamp, and returns an empty result when elapsed time is below `0x10` = **16 ms**. When service is admitted, the current tick is stored as the new service timestamp.

This 16 ms clock is separate from the later active/idle forced-emission clocks.

## scanRunId transition

The serializer compares the incoming run ID against a stored run-ID string by length and content. When the value differs, the change path updates the stored run ID, resets the active forced-emission timestamp to zero, and sets a one-shot wake flag.

Later in the same service pass, that stored run ID is copied into the local string serialized immediately after the exact JSON prefix `{"scanRunId":"`. The local string length at `[rbp+0xB8]` is therefore the active-versus-idle discriminator used by the emission gates.
The one-shot flag set by a run-ID transition is loaded and cleared before emission evaluation.

## Active versus idle evaluation

A nonempty `scanRunId` selects the active path. Active service passes always reach the final emission test. The active forced-emission threshold is:

- `0xFA` = **250 ms** since the last active envelope.

An empty `scanRunId` selects the idle path. Idle service reaches the final emission test only when either:

- the captured run-ID-change one-shot flag is nonzero; or
- the idle forced-emission threshold is due.

The idle forced-emission threshold is:

- `0x3E8` = **1000 ms** since the last idle envelope.

## Final emission test

After the R8-081 shared 1024-item drain, the final OR-chain emits a native-capture envelope when any of these is true:

- any drained points/marches/point-removals/march-removals vector is nonempty;
- any remaining pending count for those four queues is nonzero;
- the captured run-ID-change one-shot flag is nonzero;
- the active 250 ms forced-emission gate is due;
- the idle 1000 ms forced-emission gate is due.
If the applicable evaluation/final-emission gates do not require an envelope, the function explicitly constructs an empty output result.

After a real JSON envelope is assembled, the current service timestamp is written to the selected forced-emission clock:

- nonempty `scanRunId` → active clock;
- empty `scanRunId` → idle clock.

Thus the 250 ms / 1000 ms values are measured from the last emitted envelope for the corresponding state, while the 16 ms value is the minimum serializer service interval.

## Relationship to earlier Map recovery

R8-080 established the outer `XluaBridgeMapScanTick` pump gate at 50 ms.

R8-081 established one shared 1024-item native-capture drain budget across points, marches, point removals, and march removals.

R8-082 now establishes the capture service/publication policy around that drain: service no more frequently than every 16 ms, run-ID transitions force a wake/reset, active capture is periodically emitted at least every 250 ms when otherwise quiet, and idle capture is periodically emitted at least every 1000 ms.

This still does not recover the block-generation body inside `XluaBridgeMapScanTick`, coordinate order, per-tick work quota, or retry/backoff policy.
## Verification

Hash-locked verifier:

`tools/inspect_lwbridge_native_capture_flush.py`

Durable machine evidence:

`evidence/lwbridge-implementation/2026-09-27-r8-082-native-capture-flush.json`

Reference executable SHA-256:

`2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

The verifier also locks both embedded proxy hashes and confirms the service timer import resolves to `KERNEL32.dll!GetTickCount64`.

No production scanner code changes in this checkpoint.
