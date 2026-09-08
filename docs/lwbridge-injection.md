# LWBridge injection/bootstrap recovery

## Recovered launch sequence

Static recovery from the verified `lwbridge-profile-launcher.exe` shows an intentional staged bootstrap rather than a best-effort late attach.

1. Validate launch descriptor, parent PID, game path/runtime paths, launch proof/ticket and hook SHA-256.
2. Prepare runtime proxy files and xLua redirect configuration.
3. Create hook `ready`, `go` and `failed` events plus a hook result path.
4. Create the official launcher/game suspended.
5. Allocate the hook DLL path in the target with `VirtualAllocEx`.
6. Write the path with `WriteProcessMemory`.
7. Start target `LoadLibraryW` with `CreateRemoteThread`.
8. Wait for the inject thread and require successful module load.
9. Wait for the hook-ready event; abort on timeout/failure.
10. Signal the hook-go event.
11. Resume the official launcher/game.
12. The hook redirects `xlua.dll` loads to the LWBridge runtime proxy.
13. The xLua proxy establishes its named-pipe hello and host bridge state.

Recovered launcher imports directly support this sequence: `CreateProcessW`, `VirtualAllocEx`, `WriteProcessMemory`, `CreateRemoteThread`, `OpenProcess`, `ResumeThread`, `LoadLibraryA`, `CreateEventW`, `WaitForSingleObject`, and `SetEvent`.

## Recovered fail-closed states

The launcher/hook contains explicit failures for descriptor/proof/ticket integrity, invalid paths, hook allocation/write/thread/load timeouts, hook-ready timeout, hook initialization, registry/single-instance isolation, proxy installation, xLua ABI mismatch, launcher restart, game PID handoff, and ticket consumption.

This is the key reliability property to copy: process creation does not equal bridge readiness.

## Recovered timeout contract

Static disassembly of the embedded profile launcher recovers these wait values:

- injection thread/module-load wait: `30,000 ms`
- hook ready/fail race: `30,000 ms`
- official launcher/game PID handoff waits: `300,000 ms`
- cleanup/thread-close waits: `5,000 ms`

These are RECOVERED static values. They are not yet evidence of repeated current-build live success.

## Recovered xLua proxy selection

LWBridge does not safely reduce to “always use secure” or “always use plain”. Static recovery ties proxy selection to the original game's `xlua.dll` PE/export-table ABI fingerprint. The bundle contains separate `secure` and `plain` proxy choices plus their expected hashes/fingerprints.

The launcher/proxy path fails closed when the original ABI is unsupported (`GAME_XLUA_ABI_UNSUPPORTED`) or when the proxy bundle cannot be verified (`PROXY_VERIFY_FAILED`). The rebuild must reproduce this ABI/fingerprint decision before selecting a proxy instead of hard-coding one mode.

## Reliability target for the rebuild

“100% success” should mean there is never a silent half-injected state. Every attempt must end in one of two observable outcomes:

- `READY`: hook loaded, ready/go handshake completed, game resumed, xLua proxy connected, named-pipe identity accepted, bridge heartbeat fresh.
- `FAILED/RECOVERING`: a concrete stage/error is persisted and the game is not treated as automated until a clean bootstrap completes.

For mid-run loss, persist active Map Scan state first, stop scheduling new blocks, relaunch through the full bootstrap, reconnect, then resume unresolved blocks. Do not attempt to continue from a stale proxy heartbeat.

## Remaining recovery work

- Recover exact launch-proof/ticket generation, child-launcher input decoding and semantic validation. The outer-host envelope serializer itself is recovered below.
- Recover any stage-specific retry policy beyond the recovered wait constants.
- Recover the exact xLua export-fingerprint algorithm and descriptor fields used by the secure/plain decision.
- Implement the staged bootstrap independently.
- Validate forced failure at every stage, followed by clean recovery.
- Run repeated cold-start and restart cycles before calling injection stable.

## 2026-09-08 descriptor recovery checkpoint

Static inspection of the verified embedded `lwbridge-profile-launcher.exe`
recovers the serialized field names for the original launch descriptor. The
binary identifies a `LaunchDescriptor` with 26 elements:

`profileId`, `instanceId`, `pipeToken`, `gamePath`, `hookPath`, `allowedRoot`,
`allowedHookRoot`, `runtimeRoot`, `allowedRuntimeRoot`, `registryRoot`,
`gameRegistryPrefix`, `singleInstanceObjects`, `buildId`, `hookSha256`,
`secureProxySha256`, `plainProxySha256`, `secureExportFingerprint`,
`plainExportFingerprint`, `legacyXluaSha256`, `directLaunch`, `multiRequired`,
`proofPath`, `clearLogin`, `hookReadyEvent`, `expiresAtMs`, and `parentPid`.

The same binary identifies a `LaunchEnvelope` with three fields:
`descriptorJson`, `launchProof`, and `gameLaunchTicket`. Additional recovered
runtime variables include `LWBRIDGE_PROFILE_ID`, `LWBRIDGE_INSTANCE_ID`,
`LWBRIDGE_PIPE_TOKEN`, `LWBRIDGE_BUILD_ID`, `LWBRIDGE_DESCRIPTOR_SHA256`,
`LWBRIDGE_PROFILE_RUNTIME_ROOT`, `LWBRIDGE_HOOK_SHA256`, the hook coordination
event/result variables, and the xLua proxy/original/bundle paths.

These are **RECOVERED static facts**, not yet a usable launch contract. Bounded
black-box tests using missing/invalid descriptors, object/string envelopes,
three positional arguments, arrays, stdin, and descriptor/envelope file paths
all failed closed with `DESCRIPTOR_INVALID` and did not start the game. The
binary has distinct `DESCRIPTOR_REQUIRED` and `DESCRIPTOR_INVALID` branches,
so exact input decoding plus semantic field types/constraints still require
recovery. Production `profile_instance_start` therefore remains intentionally
blocked with `OVERVIEW_LAUNCH_BOOTSTRAP_UNRECOVERED` instead of falling back to
an unmanaged `Start-Process` launch.

## LWB-R5-001 — host `LaunchEnvelope` producer (2026-09-08)

**Scope.** Static recovery now identifies the host-side producer and JSON handoff
for the three-field `LaunchEnvelope`. This closes the first R5 producer item; it
does not recover the proof/ticket algorithms or the child launcher's input
decoding contract.

**Source identity.** The source is the verified outer
`../LW/lwbridge-0.3.1.exe`, SHA-256
`2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`.
All addresses below use the preferred virtual-address coordinate system with
image base `0x140000000`; raw file offsets are stated where the field strings
reside.

**Exact locator.** PE exception metadata places every recovered field write in
the same Rust async/profile-launch state-machine function at preferred VA
`0x1401D3F1B–0x1401DCAD1`.

- `secureExportFingerprint`: raw `0x8341DB`, string VA `0x140834DDB`, name
  xref `0x1401D8D2C`; its descriptor value comes from the launch context at
  `+0x60` (`0x1401D8D46`/`0x1401D8D4D`).
- `plainExportFingerprint`: raw `0x8341F2`, string VA `0x140834DF2`, name xref
  `0x1401D8DC0`; its descriptor value comes from the launch context at `+0x78`
  (`0x1401D8DDA`/`0x1401D8DE1`).
- `descriptorJson`: raw `0x8342AF`, string VA `0x140834EAF`, name xref
  `0x1401DA895`; value source is profile-launch state `+0x678` at
  `0x1401DA8AF`.
- `launchProof`: raw `0x8342D8`, string VA `0x140834ED8`, name xref
  `0x1401DA91D`; value source is profile-launch state `+0x690` at
  `0x1401DA937`.
- `gameLaunchTicket`: raw `0x8342E3`, string VA `0x140834EE3`, name xref
  `0x1401DA9A5`; value source is profile-launch state `+0x4C0` at
  `0x1401DA9BF`.

The host completes the object at `0x1401DAA4B` and calls the recovered JSON
serializer at `0x1401DAA5B -> 0x1401D296D`. The resulting owned JSON text is
copied into profile-launch state at `0x1401DACC4–0x1401DACD3` (state offset
`+0x730`). Immediately before the launch task proceeds, that serialized value is
cloned from `+0x730` at `0x1401DB119`, with clone call
`0x1401DB12B -> 0x14002A2C0`.

**Reproduction.** Run:

`python tools\inspect_lwbridge_launch_contract.py ..\LW\lwbridge-0.3.1.exe --json`

The deterministic result is persisted at
`evidence/lwbridge-implementation/2026-09-08-r5-launch-envelope-producer.json`.

**Result — RECOVERED.** The outer LWBridge host constructs an object containing
`descriptorJson`, `launchProof`, and `gameLaunchTicket`, serializes that object
to owned JSON text, and hands that serialized value into the profile-launch
state machine. The same host state machine also supplies distinct secure/plain
export-fingerprint strings to the descriptor from two launch-context fields.

**Validation and limits.** This is static-only recovery against the immutable
reference. It is not **LIVE-PROVEN**. The exact `launchProof` producer and
validation semantics, `gameLaunchTicket` producer/representation/reuse rules,
child profile-launcher input channel, xLua export-fingerprint algorithm and
secure/plain classification predicate remain **UNKNOWN/BLOCKED**. Simple
candidate export-name SHA-256 encodings did not match an embedded reference
constant, so no substitute algorithm is being inferred.

**Implementation impact.** R5's host-side `LaunchEnvelope` producer item can be
marked recovered. Production `profile_instance_start` must remain fail-closed
with `OVERVIEW_LAUNCH_BOOTSTRAP_UNRECOVERED` until the unresolved proof/ticket,
child decoding and xLua ABI contracts are recovered and then validated.
