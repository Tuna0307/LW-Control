# R8-098 — recover proxy bridge compiler and bridge-scripts text-load boundary

**Date:** 2026-09-27
**Reference:** verified LWBridge 0.3.1
**Scope:** embedded secure/plain xLua proxies; no production scanner change.

## Result

The remaining Map engine still lives below the host `startMapScan` boundary, but this checkpoint recovers a new direct route to the original script source.

Both embedded xLua proxies contain the same native Lua source-to-bytecode compiler at RVA `0x26380-0x26735`. The proxy loader resolves five functions from the real game xLua module and supplies them to that compiler in this exact order:

1. `luaL_newstate`
2. `lua_close`
3. `luaL_loadbufferx`
4. `lua_dump`
5. `lua_tolstring`

The compiler:

- rejects a missing API/source as `bridge compiler unavailable`;
- creates a temporary Lua state with `luaL_newstate`;
- loads the supplied source with `luaL_loadbufferx(..., mode="t")`;
- on compile failure reads the Lua error with `lua_tolstring(state,-1,&len)` and reports `bridge compile failed`;
- on success calls `lua_dump` with writer callback RVA `0x26360` and `strip=0`;
- closes the temporary state with `lua_close`;
- rejects an unsuccessful or empty dump as `bridge bytecode dump failed`.

The shared wrapper at RVA `0x16C10-0x16DE1` feeds those recovered compiler bytes into the original xLua load path.

## bridge-scripts.dat boundary

The original bootstrap at RVA `0x14D50-0x16287` calls that compiler wrapper with:

- chunk name exactly `@bridge-scripts.dat`;
- mode exactly `"t"`;
- source pointer and length taken from one proxy-owned `std::string` global.

The source string layout is read directly as:

- inline/object address = source global;
- length = `sourceGlobal + 0x10`;
- capacity = `sourceGlobal + 0x18`;
- heap pointer selected when capacity > 15.

Per-proxy source globals:

- secure proxy: RVA `0x8F170`;
- plain proxy: RVA `0x90170`.

The authorization/package function at RVA `0x1C0A0-0x1D46F` owns the same source global. It contains the exact package-path markers `package decrypt failed` and `loaded script package format=`, and its successful package branch replaces/assigns the same source string later consumed by the bootstrap.

Therefore the original bridge-script payload exists as contiguous Lua **text source** in proxy memory after successful package processing and before/during bootstrap compilation. This is a substantially narrower recovery target than reconstructing `XluaBridgeMapScanTick` from its outputs.

## Secure/plain parity

Hash-locked verification passed for both embedded proxies:

- secure SHA-256 `481c636b8d0fa9bf4b8f88cba77145054b33e3701ffa42737567b211859ac400`;
- plain SHA-256 `c9a88ec449cc6a9929fb2f2800ebd443f9b9dcdac849e3d3e8da94281dea9794`.

The relevant code RVAs are identical. Data globals differ by the expected image-layout shift:

- compiler API slots secure `0x90E00..0x90E20`, plain `0x91E00..0x91E20`;
- source global secure `0x8F170`, plain `0x90170`.

## Implication for Map recovery

Do **not** design another traversal algorithm.

The next direct target is now the original source string itself. If an authentic original package reaches the successful package-processing state, capture/read the proxy-owned source `std::string` and preserve its bytes before modifying any production behavior. Then locate and recover `XluaBridgeMapScanTick` directly from that original source.

This checkpoint does not prove that the current machine can presently pass the original package/auth admission path. It also does not recover the plaintext bytes yet.

## Verification

Tool:

`tools/inspect_lwbridge_proxy_bridge_compiler.py`

Evidence:

`evidence/lwbridge-implementation/2026-09-27-r8-098-proxy-bridge-compiler.json`

Reference SHA-256:

`2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

Reproduction:

`python tools\inspect_lwbridge_proxy_bridge_compiler.py ..\LW\lwbridge-0.3.1.exe --json`

## Status

**RECOVERED CONTRACT.**

Map remains **NOT WORKING** under the owner acceptance rule. No fallback or equivalent scanner is promoted by this finding.
