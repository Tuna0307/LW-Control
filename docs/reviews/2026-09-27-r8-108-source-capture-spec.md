# R8-108 — formalize the assembled-source capture specification

**Date:** 2026-09-27
**Reference:** verified LWBridge 0.3.1
**Scope:** offline specification of the R8-104 authentic assembled-source capture boundary. No live process access, authentication, network request, injection, or target-memory write.

## Result

R8-108 converts the R8-104 source lifetime into an exact capture specification that can be applied only if a permitted live observation mechanism is available.

For both hash-verified original proxies, the capture object is the proxy-global MSVC `std::string`:

- secure proxy SHA-256 `481c636b8d0fa9bf4b8f88cba77145054b33e3701ffa42737567b211859ac400`, source global RVA `0x8F170`;
- plain proxy SHA-256 `c9a88ec449cc6a9929fb2f2800ebd443f9b9dcdac849e3d3e8da94281dea9794`, source global RVA `0x90170`.

## Exact string layout

The compiler window at `0x15A08-0x15A3E` proves the source object layout:

- object size: 32 bytes;
- data/heap-pointer field: `+0x00`;
- logical length: `+0x10`;
- capacity: `+0x18`;
- small-string threshold: capacity `<= 15`;
- when capacity `<= 15`, source bytes are inline at `object+0x00`;
- when capacity `> 15`, `object+0x00` is the heap pointer.

`0x1547A` checks the same logical-length field before compilation. `0x16183-0x1618A` then passes the same source object to the zeroizer.

## Strict identity and acceptance rules

A future capture implementation must:

1. identify the loaded target module by exact on-disk SHA-256 matching one of the two recovered proxy hashes; filename alone is insufficient;
2. derive the source object as `moduleBase + sourceGlobalRva`;
3. accept only a nonzero source during the R8-104 pre-zeroization window;
4. read the 32-byte object, read exactly the declared logical source bytes, then re-read the 32-byte object and reject the observation if the object changed during the read;
5. require the exact R8-099 assembled-source prefix:

```lua
local __bridge_preload = package.preload
```

6. preserve the captured bytes without overwriting an earlier capture artifact;
7. search the authentic assembled source for `XluaBridgeMapScanTick`; absence would be a finding, not permission to use fallback/custom scanner source.

## Live-reader boundary

During this checkpoint a read-only live process-memory reader was started, but the environment blocked further implementation. That rejected operation was not rerouted through another editor, shell, helper, or tool, and the incomplete live-reader file was deleted.

Therefore R8-108 does **not** provide a working live capture implementation.

Current status:

- offline capture specification: **READY**;
- live reader: **NOT AVAILABLE**;
- authentic source bytes captured: **NO**.

## Verification

Tool: `tools/inspect_lwbridge_source_capture_spec.py`

Evidence: `evidence/lwbridge-implementation/2026-09-27-r8-108-source-capture-spec.json`

Reference SHA-256: `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

The verifier re-locks both proxy hashes, source-global RVAs, ready-length field, SSO capacity threshold, inline/heap selection, logical-length read, compiler call and final source zeroizer.

## Status

**CAPTURE SPECIFICATION READY / LIVE CAPTURE NOT AVAILABLE.**

Map remains **NOT WORKING**. R8-108 does not recover any original Lua source bytes and does not satisfy the project's WORKING definition.