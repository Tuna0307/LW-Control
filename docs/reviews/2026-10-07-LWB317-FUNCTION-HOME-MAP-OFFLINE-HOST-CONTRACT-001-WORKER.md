# LWB317-FUNCTION-HOME-MAP-OFFLINE-HOST-CONTRACT-001 — worker review

Date: 2026-10-07
Branch: `research/offline-controller`
Starting HEAD: `bc44275be9f6c198b237504df063bd307f4fdaf0`
State: **AWAITING_REVIEW**

## Result

The assigned offline host/binding recovery is complete through three sequential
checkpoints. No source-backed production defect was demonstrated, so no product
code, test code, installed client file, runtime configuration, historical
evidence, accepted UIUX behavior or fallback policy was changed.

Home/Map overall feature/live parity remains **PARTIAL**. This review does not
promote `LIVE_PROVEN`, does not begin the prepared live pilot, and does not
claim that a lupa function proves real .NET/xLua marshalling.

Checkpoint commits already pushed and direct-remote verified:

- A — `9388e124fc31ae466f0cb8cd180bd425bd525e49`,
  `docs(lwb317): recover offline host contract`;
- B — `6e5617a726a437edc8dc1ef7bb622d4a818ae2f4`,
  `test(lwb317): verify offline host seams`.

This final checkpoint supplies readiness/dependency status, documentation and
final integrity only. Project-lead acceptance remains required.

## A — recovered exact contracts

### Actual packaged reader

`src/LWBridge.GamePipeAdapter/PipeClientAdapter.cs` constructs
`ReadRuntimeSnapshot` as a public static `Func<string,string>` bound to
`ReadRuntimeSnapshotCore`. The reader is restricted to `control.txt` and
`lease.txt`, uses strict UTF-8 and a 4096-byte ceiling, returns
`ok\n<payload>` on success, exact `busy\n` for Windows sharing violations
32/33, and `unavailable\n` for other failures.

The Overview production Lua reflects both `Connect` and
`ReadRuntimeSnapshot` from `LWBridge.GamePipe.PipeClientAdapter`. Shared
metadata reads call a Lua function directly only when the runtime value is a Lua
function; otherwise they call `reader:Invoke(path)`. Exception, non-string and
unknown-prefix results are unavailable; exact `busy\n` is distinguished; only
`ok\n` exposes a payload.

### Current xLua static contract

The exact installed current `Assembly-CSharp.rdl` hashes to
`bfb740b4570c58bd2bcc7fb83f9b83d8121ce10fb1bf49040e9fb8b08e958b3e`.
Its metadata contains the generic one-argument/one-result
`XLua.DelegateBridge.Func<TArg,TResult>(TArg)`, delegate translator/cache
methods and reflection wrapping machinery. This establishes relevant current
build support statically. It does **not** execute the actual external packaged
`Func<string,string>` across the real game xLua boundary.

That exact crossing therefore remains **UNKNOWN**:
`FieldInfo.GetValue(null) -> packaged Func<string,string> -> current game xLua
-> reader:Invoke(path) -> returned string`.

### Ownership/readiness

The host publishes exact schema/version/profile/session/challenge/PID control
identity and same-owner lease metadata. It refuses fresh foreign ownership and
cleans stale metadata only through exact identity ownership. Ready admission
requires the same profile/session/challenge/PID, fresh timestamp and
`ready=true`; the host refreshes the same owner's lease while waiting.

Production Lua enforces the five-second lease horizon with a five-second future
tolerance. Busy may defer only from a previously verified same owner that is
still within its existing horizon; contention never grants a new horizon.
Replacement/retirement closes the old pipe and abandons/terminalizes owned work
instead of consuming under stale ownership.

### Manual City/Resource path

`current_live_resource_probe.lua` consumes the same Overview shared-reader
contract for control/lease. It validates ownership before command consumption,
uses the successful preflight only within that synchronous pump, and
terminalizes already-admitted lanes if ownership becomes invalid while leaving
unconsumed queued work for a future verified owner.

City/Resource current rebuild acquisition uses fresh `WorldPointManager`
response evidence and restores response flags before publishing a proven result.
City has a bounded same-server targeted view path only when allowed/needed.

The exact original 0.3.17 executable remains
`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
Hash-gated 0.3.17 recovery independently retains current start/status/stop
handlers, current Map services and the `enterWorldMap` / `startMapScan`
provider boundary. Historical 0.3.1 RVAs were not copied forward.

## Corrected findings / evidence discipline

Two negative attempts are intentionally preserved rather than hidden:

1. `rg` was not installed. Search continued with `git grep`/PowerShell; this
   was a tooling limitation only.
2. Three historical native Map inspectors correctly rejected the required
   0.3.17 hash because they are pinned to an older 0.3.1 outer executable.
   Their gates were not weakened. Exact 0.3.17 evidence uses the existing
   `tools/lwbridge317/*` hash-gated inspectors instead.

Checkpoint B also preserves the first default-Python Lua-harness failure:
`ModuleNotFoundError: No module named 'lupa'`. Nothing was installed. The
already-existing RECOVERY-003 task venv was then used successfully
(Python 3.12.14, lupa 2.8, Lua 5.4).

No recovered fact justifies a production correction in this path.

## B — executed offline production seams

All execution was inert/offline and began/ended with no LastWar/LWBridge process
observed.

Against the existing Release Desktop check binary:

- `--profile-runtime-owner-check`: PASS;
- `--map317-native-boundary-check`: PASS;
- `--home-campaign-lifecycle-check`: PASS;
- `--overview-bridge-launch-binding-check`: PASS;
- `--overview-bridge-lifecycle-launch-binding-check`: PASS;
- `--overview-bridge-host-transport-check`: PASS;
- `--overview-bridge-normal-composition-check`: PASS;
- `--map-campaign-canonical-check`: PASS with
  `providerMode=inert-local` and `externalProviderCalls=0`.

The real packaged adapter is loaded/reflected by the ownership check without
calling `Connect`. Its actual `ReadRuntimeSnapshot` delegate returns exact
`busy\n` under a Windows `FileShare.None` collision and exact
`unavailable\n` for missing metadata. Controlled same-owner publication
proves the reader waits for a complete publication, validates afterward and
cannot extend a previously validated lease beyond its freshness horizon.

`tests/home_runtime_file_ownership_checks.py`: 4/4 PASS.

`tests/home_runtime_lease_lua_checks.py` under the pre-existing task venv:
6/6 PASS. These are complete production Lua modules with isolated roots and
prove result/ownership consumer behavior, retired-owner fencing and lane
cleanup. Their injected reader is a Lua function, so they do not close the
real-xLua conversion dependency.

Because product code did not change, the work item's conditional requirement to
rerun affected Release builds and canonical frontend check/build/package checks
was not triggered. The applicable native/production-seam checks above were run
instead.

## C — readiness

The concrete matrix and future witness plan are:

`evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-OFFLINE-HOST-CONTRACT-001/checkpoint-c-dependency-matrix.md`

The remaining live dependencies are deliberately narrow:

- actual current-game xLua conversion/invocation of the packaged reflected
  `Func<string,string>`;
- actual same-owner in-game readiness/lease observation under the production
  scheduling boundary;
- one positive owned Manual Resource acquisition with restoration/cleanup;
- one positive owned Manual City acquisition with restoration/cleanup.

Exact original protected traversal/wire equivalence remains separately
**UNKNOWN / NOT CLAIMED**. Functional live success must not be relabeled exact
original protected-wire parity without separate evidence.

The prepared `LWB317-FUNCTION-HOME-MAP-LIVE-PILOT-001` remains
**ON_HOLD_BY_OWNER**. Its future preflight should check both Remote Desktop
Commander and Windows-MCP before reporting a desktop capability limitation.
No desktop call, game attachment, live test, gameplay, updater, owner-runtime
mutation or protected-service action occurred here.

## Preserved accepted behavior

RECOVERY-003 R3-01 through R3-05 remain accepted for offline/inert scope.
Recovered-source/local UIUX remains accepted. Global Auto Launch intent, Auto
Scan semantic rebase, exact runtime ownership, committed Stop behavior,
compatibility admission and restoration protections are unchanged. No fallback
was introduced.

## Evidence

Primary task evidence is under:

`evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-OFFLINE-HOST-CONTRACT-001/`

Checkpoint A records source/current-client hashes, exact contracts, current
compatibility, 0.3.17 native handlers/progress and static xLua metadata.
Checkpoint B records raw check output and seam conclusions. Checkpoint C records
the dependency/witness matrix and final integrity.

## Disposition

**AWAITING_REVIEW.** The worker-side offline assignment is complete. The project
lead should independently review the source/static classifications, B seam
evidence, final integrity and future witness matrix. If accepted, keep Home/Map
live parity PARTIAL and leave LIVE-PILOT-001 on hold until the owner explicitly
resumes live/shared-desktop work.
