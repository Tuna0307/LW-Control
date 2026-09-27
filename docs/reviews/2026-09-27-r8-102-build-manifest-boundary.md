# R8-102 — separate build-manifest admission from package plaintext ownership

**Date:** 2026-09-27
**Reference:** verified LWBridge 0.3.1
**Scope:** permitted static analysis outside the preserved envelope-consumer body; no production behavior change.

## Result

R8-102 closes the false lead immediately after the protected envelope-consumer boundary.

In both hash-verified embedded proxies, the enclosing package/auth loader has this exact success ordering:

1. `0x1CC14 -> 0x40A70`
2. `0x1CC3A -> 0x3D260`
3. `0x1CC54 -> 0x125C0`

`0x40A70` is not a package-key or plaintext producer. It is a build-manifest/proxy-bundle validator.

Its own exact diagnostics include:

- `build manifest missing`
- `build manifest invalid`
- `build manifest mismatch`

Its internal validators recognize `LWBM1` and `LWBM2`, and inspect bundle identity fields including `compositeSha256`, `proxySha256`, and `exportFingerprint`.
## Plaintext ownership

The caller dataflow separates the build-manifest validator outputs from the package plaintext buffer.

`0x40A70` receives caller-owned output locals at `rbp+0xA0`, `rbp+0xC0`, and `rbp+0x40`.

The subsequent package function at `0x3D260` instead receives `rbp-0x60` as its fourth/output argument. R8-003/R6-045 already recover that function as the LWBP2 validation/AES-GCM decrypt boundary.

Immediately afterward, the loader passes the same `rbp-0x60` address as argument 1 to `0x125C0`. R8-099 proves `0x125C0` parses the decrypted format-2 module table.

Therefore the authentic module-table ownership chain is now explicit:

```text
0x3D260 package decrypt
    -> plaintext output rbp-0x60
    -> 0x125C0 arg1
    -> format-2 module table
    -> assembled @bridge-scripts.dat source
```

`0x40A70` is a separate admission prerequisite before this chain and should not be pursued as a hidden source-cache boundary.
## Restriction boundary

This checkpoint does not inspect, invoke, emulate, patch, or reconstruct the protected secure-proxy envelope-consumer body at `0x3F8E0-0x40A6D`.

It also does not recover the package key, decrypted module bytes, or `XluaBridgeMapScanTick` source.

## Verification

Tool: `tools/inspect_lwbridge_proxy_build_manifest_boundary.py`

Evidence: `evidence/lwbridge-implementation/2026-09-27-r8-102-build-manifest-boundary.json`

Reference SHA-256: `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

Both secure/plain proxy hashes are reverified, the call ordering and buffer ownership anchors are asserted, and the manifest literals/fields are verified directly.

## Status

**RECOVERED CONTRACT.**

Map remains **NOT WORKING**. The useful consequence is narrower search scope: the next authentic-source path must reach the existing `0x3D260` plaintext output through a permitted package-processing state or another genuinely new authentic artifact; `0x40A70` is closed as a false plaintext lead.