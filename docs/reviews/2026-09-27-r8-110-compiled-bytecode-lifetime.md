# R8-110 — close compiled bridge-bytecode lifetime and retention

**Date:** 2026-09-27
**Reference:** verified LWBridge 0.3.1
**Scope:** original secure/plain proxy `0x16C10` bridge-load wrapper, compiler output format, xLua handoff and owned lifetime. No live process-memory access, auth/network activity, or protected envelope-consumer analysis.

## Result

R8-110 closes the proposed compiled-bytecode route as a later durable recovery artifact.

`0x16C10` has two loading branches:

1. when proxy branch flag `0x90D3A` (secure; `0x91D3A` plain) is nonzero, the wrapper calls the resolved `luaL_loadbufferx` slot directly with mode `"t"`; no compiled-bytecode vector is created;
2. otherwise, the wrapper invokes the R8-098 compiler, transforms the compiler output into the proxy's `LENC` representation, passes that vector synchronously to `xluaL_loadbuffer`, then frees the vector before returning.

## Compiler-output ownership

The compiled branch initializes a three-pointer byte vector as a wrapper stack local at `rbp-0x31`. `0x16CCF -> 0x26380` writes `lua_dump` output into that vector via writer callback `0x26360` / append helper `0x260B0`.

On success, `0x16D01 -> 0x27150` finalizes the vector.

`0x27150` inserts four bytes at **vector.begin**, not at the end:

```text
4C 45 4E 43   "LENC"
```

It then calls transform `0x26740` on `vector.begin + 4` for `vector.size - 4`, leaving the `LENC` prefix untransformed.

## LENC transform

The transform contains the classic ChaCha constant words spelling `expand 32-byte k` and the ChaCha quarter-round rotation pattern (right rotations 16/20/24/25, equivalent to left rotations 16/12/8/7). At `0x270C0` it XORs the generated stream into the vector in place.

Therefore the compiled representation handed to xLua is:

```text
"LENC" || ChaCha-family-transform(lua_dump bytecode)
```

R8-110 does not assign a stronger cryptographic label than the code supports; the recovered fact required for ownership is that the transform is in-place on the same local vector.

## xLua handoff

The encoded-load slot is resolved from the exact symbol `xluaL_loadbuffer` and stored at secure RVA `0x90E28` / plain `0x91E28`.

Immediately before the call:

- vector end is read from `rbp-0x29`;
- vector begin is read from `rbp-0x31`;
- length is calculated as end minus begin;
- chunk name is forwarded unchanged;
- the same vector bytes are passed synchronously to `xluaL_loadbuffer`.

The wrapper returns only the loader status. It never returns or assigns the byte vector.

## Lifetime

After `xluaL_loadbuffer` returns, the wrapper destroys its error string and releases the compiled vector allocation. The vector begin pointer is read again at `0x16D82`, and heap storage is freed through allocator helper `0x48B90` before the wrapper returns at `0x16DE0`.

For `@bridge-scripts.dat`, bootstrap calls this wrapper at `0x15A3E`. The R8-104 source-global zeroizer is not reached until later at `0x16183-0x1618A` in the caller.

Therefore, by **owned lifetime**, the compiled `LENC` vector disappears **earlier** than the assembled Lua source global.

Important caveat: the vector is freed, not explicitly zeroized. R8-110 does not rule out transient unowned allocator residue. That is not a stable ownership/cache boundary, and recovering it would require live process-memory observation, which is not an available path in the current environment.

## No later proxy dump/export surface

`lua_dump` occurs as one exact symbol string. Its compiler API slot has exactly three direct proxy references:

- `0x16C7B` — wrapper loads the slot into the temporary compiler API table;
- `0x17F4D` — resolver stores the function pointer;
- `0x17F87` — availability/initialization use.

No later native proxy routine exposes a `lua_dump`-based function export after bootstrap.

`xluaL_loadbuffer` may of course retain the loaded Lua function/prototype semantically inside Lua state. R8-110 does **not** claim that Lua's internal executable representation disappears. The recovered negative fact is narrower: the proxy keeps no owned raw `LENC` vector and exposes no later function-dump/cache surface for it.

## Wrapper caller inventory

`0x16C10` has exactly three direct callers in both proxies:

- `0x15A3E` — `@bridge-scripts.dat`;
- `0x18F58` — `@lwbridge-host-diagnostic`;
- `0x19EEE` — dynamic-source load path.

All three share the same wrapper-local bytecode lifetime.

## Current-client corroboration

This is supporting current-client evidence, not reference authority.

Installed current `xlua.dll` SHA-256:

`d22d912f031c60f2649fdaf76d359d695511f7a37b93cd637b557f8346569d45`

The current client contains `LENC` code references; at RVA `0x159F` its loader compares the first four input bytes against `LENC` before processing the remainder. This corroborates that the recovered reference proxy's encoded load format remains recognized by the installed client.

## Recovery impact

Do **not** pivot from R8-104 to compiled-bytecode cache recovery. The encoded buffer is shorter-lived by ownership than the assembled source and has no recovered persistence/cache/export surface.

The preferred authentic artifact remains the R8-104 assembled source global. If a new permitted artifact appears, preserve it, but do not spend another block searching for a proxy-owned compiled-bytecode cache without new evidence.

## Verification

Tool: `tools/inspect_lwbridge_compiled_bytecode_lifetime.py`

Evidence: `evidence/lwbridge-implementation/2026-09-27-r8-110-compiled-bytecode-lifetime.json`

Reference SHA-256: `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

## Status

**RECOVERED NEGATIVE LIFETIME.**

Map remains **NOT WORKING**. R8-110 closes compiled-bytecode retention as a better/later source-recovery route; no original Map Lua bytes were recovered.