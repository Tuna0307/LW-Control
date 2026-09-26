# R8-081 — recover native-capture drain budget and order

**Date:** 2026-09-27
**Reference:** verified LWBridge 0.3.1
**Scope:** secure/plain native world-capture serializer pre-serialization drain. No production scanner change.

## Result

The native-capture serializer uses one shared drain budget of exactly **1024 items** per snapshot.

The budget counter is initialized once at `0x38C83` with `0x400` and is then reused, without reset, across four destructive drain loops in this exact order:

1. points;
2. marches;
3. point removals;
4. march removals.
Every copied item decrements the same remaining budget. If an earlier queue consumes the budget, later queues are not drained in that snapshot.

Each drained item is also removed from its native pending container and decrements that queue's pending counter.

## Queue details

Points are copied into the local vector in `0x3A0`-byte elements and consume the shared budget at `0x38DA1`.

Marches use `0x1B0`-byte elements and consume the same budget at `0x38EF3`.

Point removals append 4-byte values and consume the budget at `0x38FF6`.

March removals append 8-byte entries and consume the budget at `0x3911D`.
The pending counters are adjacent queue-state fields. Their RVAs differ by the secure/plain data-layout shift:

- secure: points `0x91100`, marches `0x91140`, point removals `0x91180`, march removals `0x911C0`;
- plain: points `0x92100`, marches `0x92140`, point removals `0x92180`, march removals `0x921C0`.

The counter for the corresponding queue is decremented once for every destructively removed entry.

## Removal item encoding

The JSON serializer proves two removal item shapes:

- `pointRemovals` reads each element as a 32-bit value and serializes it numerically;
- `marchRemovals` reads each 8-byte entry through the string serializer and emits it between quote characters.

R8-079 remains authoritative for ACK fields: native capture emits `acks=[]` and `pendingAcks=0`, so ACKs do not participate in this 1024-item drain.
## Impact

This closes most of the previously open native queue→batch sequencing contract.

The rebuild must preserve:

- one shared 1024-item maximum across all four native capture categories;
- fixed priority order points → marches → point removals → march removals;
- destructive removal from the pending queues;
- post-drain pending counts;
- numeric point-removal and string march-removal encoding.

This still does not recover how `XluaBridgeMapScanTick` creates block work, traversal coordinates, retry/backoff behavior, or exact publication/flush timing.

## Verification

Hash-locked verifier:

`tools/inspect_lwbridge_native_capture_drain.py`

Durable machine evidence:

`evidence/lwbridge-implementation/2026-09-27-r8-081-native-capture-drain.json`
