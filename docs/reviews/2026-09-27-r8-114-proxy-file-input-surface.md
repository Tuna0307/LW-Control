# R8-114 — close native proxy file-based source/eval shortcut

**Date:** 2026-09-27
**Reference:** verified LWBridge 0.3.1
**Scope:** product-owned native secure/plain proxy bootstrap/runtime file inputs relevant to source recovery. Generic CRT/internal filesystem plumbing is not treated as a product input surface. The protected envelope-consumer body `0x3F8E0-0x40A6D` remains uninspected.

## Result

R8-114 closes the remaining native file-based bootstrap shortcut suggested after R8-113.

Both verified proxies own the same exact static product paths:

- `\LastWar-xLua-Bridge\bridge-scripts.dat` — RVA `0x71270`;
- `\bridge-runtime\authorization.ticket` — RVA `0x712F0`;
- `\bridge-runtime\authorization.challenge` — RVA `0x71390`;
- `\bridge-runtime\package-key.envelope` — RVA `0x71770`;
- `\bridge-runtime\build.manifest` — RVA `0x717C0`;
- `\Game\LastWar_Data\Plugins\x86_64\xlua-proxy-bundle.json` — RVA `0x71840`;
- `\logs\xlua-proxy.log` — RVA `0x73C10`;
- `\bridge-runtime\logs\xlua-proxy.log` — RVA `0x73C40`.

Dynamic path inputs recovered in R8-113 are also semantically typed: original xLua DLL path, proxy-bundle path, multi-proof path and profile runtime root. None is an arbitrary Lua source/chunk path.

## Bounded text reader

Reader `0x1BD80` has exactly two direct callers in both proxies:

```text
0x1C45B  package-key.envelope
0x1D969  LWBRIDGE_MULTI_PROOF_PATH
```

Both callers set the exact maximum `0x1000` before invoking the reader.

`LWBRIDGE_MULTI_PROOF_PATH` is not a generic script path. The surrounding multi-admission path first checks inline `LWBRIDGE_MULTI_PROOF`; when file-backed proof is needed, it reads the path through `0x1BD80` and feeds the text into the multi-proof/admission validator under launch/renewal context.

Therefore the recovered bounded file-text reader is not an alternate Lua source/eval loader.

## `bridge-scripts.dat` reader ownership

The encrypted package path is constructed by `0x1E390` and copied into proxy-owned package-path global:

- secure global RVA `0x8F150`;
- plain global RVA `0x90150`.

The only dedicated package reader is `0x1B910`. It has exactly one direct caller:

```text
0x1CAFD -> 0x1B910
```

inside package/auth processing.

The resulting package flow remains:

```text
bridge-scripts.dat
  -> dedicated package reader 0x1B910
  -> build manifest / proxy bundle identity validation (R8-102)
  -> package-key.envelope consumer boundary
  -> 0x3D260 package decrypt
  -> 0x125C0 module table parser
  -> assembled @bridge-scripts.dat source
```

`0x1B910` is not exposed as a second arbitrary-path script loader.

## Other typed file inputs

- `authorization.ticket` has a dedicated reader `0x1BB20` with exactly one direct caller `0x202A6` and is already structurally classified by the authorization-ticket recovery.
- `authorization.challenge` is owned by the launch-nonce generation/reuse persistence path; its path constructor flows to the recovered runtime persistence helper.
- `build.manifest` and proxy-bundle JSON feed the R8-102 build/proxy identity validator at `0x40A70`.
- `LWBRIDGE_XLUA_ORIGINAL_PATH` supplies a DLL/library path to the original-xLua loader, not Lua source text.
- `LWBRIDGE_PROFILE_RUNTIME_ROOT` supplies runtime/log ownership, including `xlua-proxy.log`.

## Recovery consequence

No recovered product-owned native file path or file-reader route provides:

- arbitrary Lua source text;
- arbitrary eval source;
- alternate `.lua`/chunk loading;
- diagnostic-script file override;
- alternate bridge-script package bypassing the authentic package/decrypt path.

This is deliberately narrower than claiming every CRT/internal filesystem operation in the proxy is classified. It closes the product-owned bootstrap/runtime **source-recovery file surface**.

After R8-113 and R8-114, there is no environment-variable or native file-input shortcut around the authentic package/source path. The preferred target remains the R8-104 assembled-source window when a legitimate successful package-processing state becomes reachable.

## Verification

Tool: `tools/inspect_lwbridge_proxy_file_input_surface.py`

Evidence: `evidence/lwbridge-implementation/2026-09-27-r8-114-proxy-file-input-surface.json`

Reference SHA-256: `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

Both secure and plain embedded proxy hashes are reverified.

## Status

**RECOVERED NEGATIVE FILE-SOURCE SURFACE.**

Map remains **NOT WORKING**. No original Lua source bytes or original Map acquisition logic were recovered in this checkpoint.