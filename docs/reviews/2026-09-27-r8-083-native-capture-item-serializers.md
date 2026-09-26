# R8-083 — recover native-capture point/march/train serializers

**Date:** 2026-09-27
**Reference:** verified LWBridge 0.3.1
**Scope:** secure/plain native-capture item JSON serializers. No production scanner change.

## Result

Both verified embedded proxies use identical serializers and field layouts for the item arrays emitted inside the R8-076/R8-079 native-capture envelope.

Serializer functions:

- point: `RVA 0x30F70-0x31FD3`
- march: `RVA 0x30820-0x30F65`
- nested train: `RVA 0x31FE0-0x321FB`

The R8-081 array strides match these layouts:

- point element: `0x3A0` bytes
- march element: `0x1B0` bytes
- nested train object: `0x38` bytes

The recovered JSON order is deterministic and identical in secure/plain.
## Field contracts

Point records contain exactly **57 ordered keys**. The first four are non-nullable: `pointIndex` (32-bit integer), `mainIndex` (32-bit integer), `isMainPoint` (boolean), and `pointType` (32-bit integer). The remaining point scalars use explicit presence state and serialize a value or JSON `null`. Two UUID-family values are deliberately emitted as quoted 64-bit numbers, two speed fields use the float writer, and the optional text fields use the shared string helper.

March records contain exactly **25 ordered keys**. March `uuid` is a required quoted 64-bit value. The remaining numeric and boolean values are nullable, followed by five nullable text fields and a nullable nested `train` object.

The nested train object contains exactly **6 ordered keys**. Its UUID is a nullable quoted 64-bit value; its remaining five fields use the nullable 32-bit integer writer.

The durable JSON evidence contains the complete ordered key arrays plus native value offsets and presence-byte offsets for every field.
## Representation semantics

The serializer uses separate 32-bit integer, 64-bit integer and float writers. Quoted identifier fields deliberately place the 64-bit writer between JSON quote characters.

The shared optional-string helper at `0x387C0` tests the string-slot presence byte at offset `+0x20`. Present strings are passed through the native JSON escaping helper and emitted between quote characters; absent strings are emitted as literal JSON `null`. The key itself is still emitted, so a missing optional string is not omitted from the object.

## Impact and limits

R8-083 closes the native-capture point/march/train **serialization contract**: exact key order, representation family, nullability, value offsets and presence offsets are now mechanically derived from both verified proxies.

This does not establish the semantic meaning or population source of every captured field. It also does not recover block order/coordinates, per-tick request work, retry/backoff, or other logic inside `XluaBridgeMapScanTick`.

The later host-normalization schema is a separate layer. Producer field names must not be silently replaced with normalized host names inside the native-capture envelope.

## Verification

Hash-gated extractor/verifier: `tools/inspect_lwbridge_native_capture_item_serializers.py`

Durable evidence: `evidence/lwbridge-implementation/2026-09-27-r8-083-native-capture-item-serializers.json`

Reference SHA-256: `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

The verifier requires the complete 57/25/6 key order, point/march capture strides, exact item-serializer call targets, and optional-string quote/escape/null behavior in both proxy hashes. No production scanner behavior is changed.
