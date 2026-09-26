# R8-077 — recover original Map acknowledgement host boundary

**Date:** 2026-09-27
**Reference:** verified LWBridge 0.3.1
**Scope:** determine what the original host itself does with native capture acknowledgement state during `map.scan.complete`. No production scanner change is made.

## Result

The original host does not parse acknowledgement items or translate `acks[]` entries into block completion/retry state in the recovered complete-event path.

Instead, the host reads five pending queue counts:

- `pendingPoints`
- `pendingMarches`
- `pendingPointRemovals`
- `pendingMarchRemovals`
- `pendingAcks`

and sums them into the public `nativePendingRecords` metric.
## Exact host dataflow

The relevant `map.scan.complete` window is `0x140340990-0x140341390`.

Exact lookups are:

- `pendingPoints` at `0x1403409AF`
- `pendingMarches` at `0x1403409CC`
- `pendingPointRemovals` at `0x1403409E7`
- `pendingMarchRemovals` at `0x1403409FF`
- `pendingAcks` at `0x140340A20`
- `dropped` at `0x140340A3B`

The pending categories are accumulated through the additions ending at `0x140340A38`. The result is serialized as `nativePendingRecords` at `0x140340AA6-0x140340ACF`.
Immediately afterwards, `0x140340AD3-0x140340ADB` overwrites the working registers with the separately parsed `dropped` value. The remainder of the recovered terminal decision window performs no later `nativePendingRecords` lookup.

Positive `dropped` remains a real terminal failure/cleanup input at `0x140340CBC-0x140340CC5`. Existing R6 evidence also remains authoritative for native-capture stopped/error handling, positive `failedBlocks`, nonempty `lastError`, and direct-completion coverage.

## Ownership conclusion

This changes the recovery target materially.

The host receives already-produced `completedBlocks`, `failedBlocks` and `inflightBlocks` through `map.scan.progress` / `map.scan.complete`, normalizes those counters, and separately reports the five native pending queue counts.

It does **not** recover block completion by consuming `acks[]` itself.
Therefore exact acknowledgement item schema, acknowledgement consumption order, queue-drain scheduling and any acknowledgement-to-block completion linkage are below the recovered host event layer. They must be recovered from the protected game-side proxy/scheduler boundary rather than invented from host status fields.

R8-077 also confirms why `nativePendingRecords > 0` must not be promoted into a host-side publication blocker: the original complete-event host path reports the aggregate but does not use it as a terminal admission predicate.

This does not prove the protected game-side code may emit `map.scan.complete` before its queues are acceptably drained. It only proves that any such drain requirement is not enforced by the recovered host decision branch.

## Verification

`tools/inspect_lwbridge_map_ack_host_boundary.py` is hash-locked to SHA-256 `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`.

It pins the six native capture count lookups, the five-category pending sum, `nativePendingRecords` serialization, aggregate-register overwrite, the positive-`dropped` gate, and absence of a later `nativePendingRecords` lookup in the bounded complete-event decision window.

The verifier does not inspect the protected RVA boundary `0x3F8E0-0x40A6D`.
## Implementation decision

No production scanner code changes in R8-077.

The current-v21 scanner remains LIVE-WORKING / EQUIVALENT_REIMPLEMENTATION. Replacing it before the protected acknowledgement/tick/traversal contract is recovered would still trade known live behavior for speculation.

The next Map target is narrower: recover the protected producer that turns block work into `map.scan.progress` / `map.scan.complete`, with emphasis on acknowledgement generation/consumption and queue-drain ownership.
