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

- Recover exact launch descriptor serialization and launch-proof format required by the original profile launcher.
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
