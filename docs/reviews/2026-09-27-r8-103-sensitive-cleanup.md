# R8-103 — recover package-key and plaintext zeroizing cleanup

**Date:** 2026-09-27
**Reference:** verified LWBridge 0.3.1
**Scope:** caller-side sensitive-buffer lifetime after package processing; no production behavior change.

## Result

R8-103 recovers the common cleanup behavior for the two sensitive buffers that matter to original Map source recovery.

In both hash-verified embedded proxies, the enclosing loader reaches:

- `0x1D0EB`: address of the caller-owned package-key vector at `rsp+0x48`;
- `0x1D0F0 -> 0x3F4A0`: package-key vector cleanup;
- `0x1D0F5`: address of decrypted package output at `rbp-0x60`;
- `0x1D0F9 -> 0x3F350`: decrypted plaintext/string cleanup.

These are explicit zeroizing helpers rather than ordinary free-only destructors.

## Package-key cleanup

`0x3F4A0` reads vector begin/end from `[rcx]` and `[rcx+8]`. While begin != end, it writes byte `0` at the current pointer, advances one byte and decrements the remaining length. It then sets the vector end back to begin before later capacity/storage cleanup.

Therefore the exact package-key bytes produced into `rsp+0x48` are intentionally overwritten before the allocation is released.
## Plaintext cleanup

`0x3F350` reads the string logical length from `+0x10`, selects inline versus heap storage using the standard `+0x18` capacity rule, and writes byte `0` across every logical source byte. It then sets logical length to zero and writes a terminating zero before later storage cleanup.

R8-102 already proves that this `rbp-0x60` object is the `0x3D260` decrypted package output and is passed directly to `0x125C0` as the format-2 module-table input.

Therefore the authentic decrypted module table is also intentionally scrubbed on the common cleanup path.

## Recovery impact

The original implementation does not leave the package key or decrypted module table as intended post-return memory residue.

A legitimate runtime capture of either buffer must occur while the enclosing package-processing call still owns it, before `0x1D0EB/0x1D0F5` cleanup. Searching allocator residue after loader return should not be treated as the primary authentic recovery route.

This does not change the preserved restriction on secure-proxy `0x3F8E0-0x40A6D`; that body was not inspected or invoked.

## Verification

Tool: `tools/inspect_lwbridge_proxy_sensitive_cleanup.py`

Evidence: `evidence/lwbridge-implementation/2026-09-27-r8-103-sensitive-cleanup.json`

Reference SHA-256: `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

Both secure/plain proxy hashes, loader callsites, cleanup targets, zeroing loops and the R8-102 decrypt/parser ownership anchors are asserted.

## Status

**RECOVERED CONTRACT.** Map remains **NOT WORKING**; no package-key value or plaintext bytes are recovered by this checkpoint.