# R8-079 — recover native-capture ACK constants

**Date:** 2026-09-27
**Reference:** verified LWBridge 0.3.1
**Scope:** embedded secure/plain xLua proxy native-capture JSON serializer. No production scanner change.

## Result

The original native world-capture serializer does not expose a dynamic acknowledgement queue in its emitted capture envelope.

In both verified embedded proxies, serializer function `RVA 0x38AB0-0x39F15` emits:

- `"acks":[]` as a literal empty array;
- `"pendingAcks":0` as a literal zero.

By contrast, `pendingPoints`, `pendingMarches`, `pendingPointRemovals`, and `pendingMarchRemovals` are each followed by a dynamic integer serialization call.
## Exact serializer sequence

The ACK array branch is source-backed at:

- `0x399D3`: load literal `],"acks":[],"ready":`;
- `0x399DE`: append that literal;
- the selected `true` / `false` value is then appended for `ready`.

There is no ACK-array iteration or ACK-item serializer between the preceding `marchRemovals` loop and the `ready` value in this serializer sequence.

The pending counters then serialize in this order:

1. `pendingPoints` — label at `0x399F1`, dynamic integer at `0x39A03`;
2. `pendingMarches` — label at `0x39A0B`, dynamic integer at `0x39A1D`;
3. `pendingPointRemovals` — label at `0x39A25`, dynamic integer at `0x39A37`;
4. `pendingMarchRemovals` — label at `0x39A3F`, dynamic integer at `0x39A52`;
5. `pendingAcks` — literal `,"pendingAcks":0` loaded at `0x39A5A` and appended at `0x39A61`.

Immediately after the pending-ACK literal, the serializer moves to `dropped` at `0x39A69`; no dynamic integer write occurs for `pendingAcks`.
## Secure/plain parity

The result is identical in both hash-verified embedded proxies:

- secure SHA-256 `481c636b8d0fa9bf4b8f88cba77145054b33e3701ffa42737567b211859ac400`;
- plain SHA-256 `c9a88ec449cc6a9929fb2f2800ebd443f9b9dcdac849e3d3e8da94281dea9794`.

Both use the same serializer range and the same ACK/pending-count xref RVAs.

This analysis is outside the historically restricted `0x3F8E0-0x40A6D` LWKE1/package-key consumer and does not inspect that body.
## Impact on R8-077

R8-077 proved the original host reads `pendingAcks` only as one count in reported `nativePendingRecords`, does not parse `acks[]`, and does not use the pending aggregate as terminal admission.

R8-079 now proves that the native-capture serializer itself supplies an empty ACK list and zero pending-ACK count. Therefore the rebuild must not invent native-capture ACK items, ACK queue depth, or ACK-driven block completion from those fields.

This does **not** prove that no acknowledgement concept exists elsewhere in the protected script/`XluaBridgeMapScanTick` layer. It also does not recover block traversal, scheduling/pacing, queue-drain timing, or retry/backoff behavior.
## Verification

Hash-locked verifier:

`tools/inspect_lwbridge_native_capture_ack_constants.py`

Durable machine evidence:

`evidence/lwbridge-implementation/2026-09-27-r8-079-native-capture-ack-constants.json`

Reference executable SHA-256:

`2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

No production scanner code changes in this checkpoint.
