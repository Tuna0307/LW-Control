# R8-104 — recover authentic assembled-source capture window

**Date:** 2026-09-27
**Reference:** verified LWBridge 0.3.1
**Scope:** bootstrap-owned assembled Lua-source lifetime; no production behavior change.

## Result

R8-104 recovers the exact lifetime window of the authentic assembled `@bridge-scripts.dat` Lua source after package processing and before zeroizing cleanup.

In both hash-verified embedded proxies, bootstrap is unwind-delimited at `0x14D50-0x16287` and contains the full sequence:

1. `0x153E2 -> 0x1C0A0`: package/auth refresh call;
2. `0x1547A`: direct check of the source global logical length at `source+0x10`;
3. `0x15A08-0x15A32`: direct reads of the same source `std::string` pointer/capacity/length;
4. `0x15A3E -> 0x16C10`: compiler wrapper call for `@bridge-scripts.dat`;
5. `0x16183`: address of the same source global;
6. `0x1618A -> 0x3F350`: final zeroizing source cleanup.

The only direct bootstrap RIP-relative references to the source object are exactly:

`0x1547A`, `0x15A08`, `0x15A0F`, `0x15A17`, `0x15A32`, and `0x16183`.
## Package-to-source ownership

R8-099 proves `0x125C0` assembles the format-2 module table into final Lua source. R8-098 proves the package/auth path owns the same proxy-global source string. R8-104 re-verifies the two replacement branches: each zeroizes the old global and assigns parser output into that same global through helper `0x87C0`.

Therefore package processing does not need to leave raw decrypted module bytes alive for later compilation. The parser output is promoted into the proxy-owned assembled-source global, and bootstrap consumes that later source directly.

## Capture impact

When bootstrap reaches `0x1547A` and observes a nonzero source length, an authentic assembled Lua source is already present in:

- secure proxy: source global RVA `0x8F170`;
- plain proxy: source global RVA `0x90170`.

That same source remains the compiler input at `0x15A08-0x15A3E` and is not zeroized until `0x16183-0x1618A`.

This creates a later and cleaner authentic capture boundary than the R8-103 raw decrypted module-table window. If an original successful package/bootstrap state can be reached through a permitted path, preserving the assembled source during this pre-zeroization window is sufficient to recover the original modules/handlers without first preserving the raw AES plaintext.

## Restriction boundary

This checkpoint does not inspect, invoke, emulate, patch, or reconstruct secure-proxy `0x3F8E0-0x40A6D`. It does not recover any source bytes and does not prove the current machine can enter the original successful package/bootstrap state.

## Verification

Tool: `tools/inspect_lwbridge_proxy_source_capture_window.py`

Evidence: `evidence/lwbridge-implementation/2026-09-27-r8-104-source-capture-window.json`

Reference SHA-256: `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

Both secure/plain proxy hashes, bootstrap unwind range, package call, source length gate, package source-assignment anchors, compiler reads/call, exact bootstrap source-reference inventory, and final zeroizer are asserted.

## Status

**RECOVERED CONTRACT.** Map remains **NOT WORKING** because the authentic source bytes themselves are still not recovered and the original source path has not run successfully against the current live game.