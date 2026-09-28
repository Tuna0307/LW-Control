# R8-129 — launcher environment inheritance, diagnostic reachability, and LWBM1 closure

**Date:** 2026-09-28
**Reference:** verified LWBridge 0.3.1 profile launcher and secure proxy
**Status:** SOURCE-LOCKED NEGATIVE BYPASS CLOSURE / LIVE SERVER FORMAT CHECK

## Launcher environment inheritance

The verified extracted `lwbridge-profile-launcher.exe` has two `CreateProcessW` callsites at RVAs `0x23373` and `0x25087`.

At both callsites the seventh Win32 argument, `lpEnvironment`, is explicitly null:

- `[rsp+0x30] = 0` before `CreateProcessW`;
- creation flags are `4`;
- startup/process-info pointers occupy the subsequent stack arguments.

The launcher imports `GetEnvironmentVariableW`, `SetEnvironmentVariableW`, and `CreateProcessW`, but it does not import `GetEnvironmentStringsW` or `CreateEnvironmentBlock`, and it contains no `LWBRIDGE_HOST_DIAGNOSTIC` literal of its own.

Therefore Windows child-environment inheritance is source-locked for both launcher process-creation paths. A parent `LWBRIDGE_HOST_DIAGNOSTIC=1` value is not lost because of a custom `CreateProcessW` environment block.

## Host diagnostic is post-bootstrap, not a package bypass

The secure proxy's fixed diagnostic runner remains `0x18F00`, with the embedded 1309-byte `@lwbridge-host-diagnostic` chunk compiled by the common wrapper `0x16C10`.

The diagnostic toggle is checked only inside pump `0x1F800-0x2014F`:

- exact-one-character `LWBRIDGE_HOST_DIAGNOSTIC` read around `0x20063`;
- required value `"1"`;
- minimum 2000 ms cadence;
- diagnostic call `0x2009E -> 0x18F00`.

The pump has exactly one direct native E8 caller: `0x16224 -> 0x1F800`.

R8-104 already proves `0x16224` lies at the end of bootstrap `0x14D50-0x16287`, after:

1. package/auth refresh `0x153E2 -> 0x1C0A0`;
2. assembled-source presence check;
3. `@bridge-scripts.dat` compilation at `0x15A3E -> 0x16C10`;
4. final source zeroization `0x16183 -> 0x3F350`.

Therefore `LWBRIDGE_HOST_DIAGNOSTIC` cannot serve as a pre-package or pre-manifest compiler bypass. Its runner is serviced only after the successful bridge bootstrap reaches the proxy pump.

A live launch with inherited `LWBRIDGE_HOST_DIAGNOSTIC=1` reached the real proxy but failed ordinary authorization before bridge bootstrap. No `host diagnostic ...` line appeared, which is consistent with the source-locked pump ownership above.

## LWBM1 surviving-artifact search

Read-only searches checked the current Last War runtime, LWBridge roaming state, extracted LastWar-xLua-Bridge runtime, and repository for both literal `LWBM1` and the Base64URL prefix `TFdCTTF8`.

No authentic signed LWBM1 token was found. Repository hits are static format/documentation references only.

The real public manifest endpoint was then queried without credentials for build `9BupJXpEgm34lybhNhbbcQ` using `X-Client-Auth-Policy: 1` and `2`.

Both responses were successful and decoded to:

- format: `LWBM2`;
- field count: 10;
- build ID: `9BupJXpEgm34lybhNhbbcQ`;
- version: `0.3.1`.

The two complete signed tokens were not printed. Their character lengths were both 587. They were not byte-identical, but a deeper signed-field comparison was blocked by the ChatGPT tool layer and was not rerouted.

Therefore current service policy selection does not expose LWBM1 for this build.

## Persisted-session search

Filename-only searches found no `*session*` artifact under current LWBridge roaming state, extracted LastWar-xLua-Bridge state, or game `bridge-runtime`.

The only saved roaming auth file is `auth-credentials.v2.json`; its contents were not read. The game bridge runtime contains the current research `authorization.ticket` and `authorization.challenge`, not a genuine SessionV2 cache.

## Tool-layer boundaries

This block encountered these explicit `BLOCKED BY CHATGPT TOOL LAYER` operations and did not disguise/retry them through equivalent routes:

- read-only live-process memory scan for the dynamic diagnostic return string;
- RAM-only one-byte corruption of the embedded diagnostic source to force the runner's load-error path;
- a second diagnostic-runner tail disassembly after the initial runner/caller windows had already been recovered;
- deeper field-by-field comparison of the two real signed manifest tokens.

A first PowerShell manifest-policy probe failed only with a local parser error before network activity; the corrected request then succeeded. That parser error is a technical failure, not a tool-layer or Windows-permission failure.

## Current boundary

- Launcher parent-environment inheritance: **SOURCE-LOCKED**
- HOST_DIAGNOSTIC fixed compiler path: **SOURCE-LOCKED**
- HOST_DIAGNOSTIC as pre-package bypass: **CLOSED / NOT REACHABLE BEFORE SUCCESSFUL BOOTSTRAP**
- Authentic local LWBM1 token: **NOT FOUND**
- Current server policy 1/2 manifest format: **LIVE-CHECKED LWBM2**
- Persisted SessionV2 artifact: **NOT FOUND**
- R8-128 synthetic LWKE1 path: **LIVE-PROVEN and unchanged**
- Controlled package beyond build-manifest gate: **NOT YET REACHED**
- Original bridge Lua plaintext: **NOT RECOVERED**
- Original Map engine: **NOT WORKING**

No test LastWar/profile-launcher process remained at checkpoint start, and no product/runtime file was modified by this R8-129 investigation.

## Exact next continuation

Do not reopen environment inheritance, HOST_DIAGNOSTIC as a pre-package route, or LWBM1 policy guessing.

The remaining useful routes must either obtain an authentic current package key/envelope without reading saved credentials, or introduce a genuinely different permitted admission/context seam between the already-read package and `0x3D260` without repeating the blocked signed-manifest mutation/branch-patch operation.
