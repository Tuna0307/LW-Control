# R8-113 — close native proxy environment/config source-input surface

**Date:** 2026-09-27
**Reference:** verified LWBridge 0.3.1
**Scope:** exhaustive native secure/plain proxy `GetEnvironmentVariableW` surface and constant environment-key ownership. No game launch, pipe traffic, live memory, auth/network activity, or protected envelope-consumer analysis.

## Result

Both original proxies expose the same exact 13 UTF-16 `LWBRIDGE_*` environment variables:

- `LWBRIDGE_PROFILE_ID` — RVA `0x6FF98`
- `LWBRIDGE_INSTANCE_ID` — RVA `0x6FFC0`
- `LWBRIDGE_PIPE_TOKEN` — RVA `0x6FFF0`
- `LWBRIDGE_GAME_ROOT` — RVA `0x70F58`
- `LWBRIDGE_XLUA_ORIGINAL_PATH` — RVA `0x70FF0`
- `LWBRIDGE_XLUA_PROXY_BUNDLE_PATH` — RVA `0x71800`
- `LWBRIDGE_MULTI_REQUIRED` — RVA `0x718B8`
- `LWBRIDGE_BUILD_ID` — RVA `0x718E8`
- `LWBRIDGE_DESCRIPTOR_SHA256` — RVA `0x71910`
- `LWBRIDGE_MULTI_PROOF_PATH` — RVA `0x71948`
- `LWBRIDGE_MULTI_PROOF` — RVA `0x71980`
- `LWBRIDGE_HOST_DIAGNOSTIC` — RVA `0x71B98`
- `LWBRIDGE_PROFILE_RUNTIME_ROOT` — RVA `0x72428`

`GetEnvironmentVariableW` is imported at IAT RVA `0x6F100` in both proxies.

## Reader closure

The proxy has two generic environment-string readers, `0x149D0` and `0x14BE0`. Every direct caller supplies a constant key from the 13-name inventory above.

`0x149D0` callers:

```text
0x15874  LWBRIDGE_PROFILE_RUNTIME_ROOT
0x16398  LWBRIDGE_GAME_ROOT
0x16E54  LWBRIDGE_XLUA_ORIGINAL_PATH
0x180E5  LWBRIDGE_XLUA_ORIGINAL_PATH
0x1B5A8  LWBRIDGE_XLUA_PROXY_BUNDLE_PATH
0x1D742  LWBRIDGE_MULTI_PROOF_PATH
```

`0x14BE0` callers:

```text
0x1D4AA  LWBRIDGE_MULTI_REQUIRED
0x1D563  LWBRIDGE_BUILD_ID
0x1D5DC  LWBRIDGE_PROFILE_ID
0x1D653  LWBRIDGE_INSTANCE_ID
0x1D6CC  LWBRIDGE_DESCRIPTOR_SHA256
0x1D790  LWBRIDGE_MULTI_PROOF
```

The remaining direct `GetEnvironmentVariableW` reads are also members of that same inventory:

- hello/pipe identity: `PROFILE_ID`, `INSTANCE_ID`, `PIPE_TOKEN`;
- diagnostic toggle: `HOST_DIAGNOSTIC`;
- runtime-log path construction: `PROFILE_RUNTIME_ROOT`.

There is therefore no hidden non-prefixed/dynamic environment key flowing through the recovered native readers.

## Host diagnostic toggle

`LWBRIDGE_HOST_DIAGNOSTIC` is narrowly defined:

- two-wide-character buffer including terminator;
- environment API result length must equal `1`;
- first wide character must equal `0x31` (`"1"`);
- diagnostic execution is gated to at least `0x7D0` = **2000 ms** between runs;
- runner is native `0x18F00`, the embedded `@lwbridge-host-diagnostic` path investigated in R8-111 scratch.

This toggle enables the fixed embedded diagnostic. It does not provide a source path, source string, chunk name, or eval payload.

## Runtime-root secondary owner

`LWBRIDGE_PROFILE_RUNTIME_ROOT` is also consumed by `0x42B40-0x42FEA` to construct the normal proxy log path:

```text
\logs\xlua-proxy.log
```

with bridge-runtime fallback:

```text
\bridge-runtime\logs\xlua-proxy.log
```

This owner is log-path construction, not source/eval input.

## Recovery consequence

The native proxy environment surface contains **no variable for**:

- Lua source text;
- eval source/input;
- alternate bridge-script package/source;
- `.lua`/chunk source path;
- diagnostic source-file override;
- runtime replacement for `@bridge-scripts.dat`.

This closes the environment/config idea raised after R8-112. It does not prove the absence of every possible file, registry, or protocol input elsewhere in the product; it proves the complete native proxy **environment-variable** surface.

R8-111 remains scratch-only because its verifier execution was blocked. R8-113 does not depend on that blocked verifier.

## Verification

Tool: `tools/inspect_lwbridge_proxy_environment_surface.py`

Evidence: `evidence/lwbridge-implementation/2026-09-27-r8-113-proxy-environment-surface.json`

Reference SHA-256: `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

Both secure and plain embedded proxy hashes are reverified.

## Status

**RECOVERED NEGATIVE CONFIG SURFACE.**

Map remains **NOT WORKING**. No authentic Lua source or original Map acquisition logic was recovered by this checkpoint.