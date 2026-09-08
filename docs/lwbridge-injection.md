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
and child profile-launcher input channel remain **UNKNOWN/BLOCKED**. The xLua
fingerprint/selector gap from this checkpoint is resolved separately by
`LWB-R5-002` below.

**Implementation impact.** R5's host-side `LaunchEnvelope` producer item can be
marked recovered. Production `profile_instance_start` must remain fail-closed
with `OVERVIEW_LAUNCH_BOOTSTRAP_UNRECOVERED` until the unresolved proof/ticket,
child decoding and remaining launch contracts are recovered and then validated.

## LWB-R5-002 — xLua export ABI fingerprint selector (2026-09-08)

**Scope.** Static recovery now establishes the exact byte stream hashed by the
profile launcher to classify the original game's `xlua.dll`, plus the
secure/plain bundle mapping. This closes the R5 selector-recovery item without
claiming that launch or injection is operational.

**Source identity.** The outer reference is
`../LW/lwbridge-0.3.1.exe`, SHA-256
`2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`.
Its embedded `lwbridge-profile-launcher.exe` is SHA-256
`8f42adb9ed678445425e529cdee8f12a097c63053f9d4de314a758a8dfe362de`.
The current official game input used for the current-build correlation is
`%LOCALAPPDATA%\FunFly\Last War-Survival Game\Game\LastWar_Data\Plugins\x86_64\xlua.dll`,
SHA-256 `21eb704afdb7e528f4b90fa1b90bf414c221b06ba990d625aaaaed31b292740f`.

**Exact locator.** Addresses below are preferred launcher VAs with image base
`0x140000000`. The classifier runtime function is
`0x140030FD0-0x14003205C`.

- Export `Base` is read from the PE export directory `+0x10` at
  `0x1400314EA`, with the value retained at `0x140031512`.
- Each name's `AddressOfNameOrdinals[i]` entry is read at
  `0x1400317DC`/`0x140031810`; `0x140031817` adds the export `Base`, and
  `0x14003183D` stores the resulting 32-bit export ordinal in the record.
- The record vector is sorted by `0x14003187D -> 0x140032720`; the small-sort
  path fixes each record width at `0x20` bytes (`0x140032745`) and calls the
  insertion comparator at `0x140034980`. That comparator compares the ordinal
  dword at `0x140034992-0x140034994` first and uses the export-name bytes as
  the equal-ordinal tie-break.
- The canonical buffer begins with six bytes `LWXE1\n`, constructed by
  `0x1400318F4` (`31 0A`) and `0x1400318FA` (`LWXE`). The formatter loop uses
  record offset `+0x08` for the export-name string (`0x140031A91`) and formats
  each record before appending it (`0x140031AD9 -> 0x1400760D0`).
- SHA-256 initialization begins at `0x140031C35`; the referenced state at RVA
  `0x89318` is the standard eight-word SHA-256 IV
  `6a09e667 bb67ae85 3c6ef372 a54ff53a 510e527f 9b05688c 1f83d9ab 5be0cd19`.

**Recovered algorithm.** Build the canonical bytes as:

`LWXE1\n` followed by one line for every named PE export, sorted by ascending
actual export ordinal (name-byte order breaks an equal-ordinal tie):
`<decimal export ordinal>:<UTF-8 export name>\n`. The export ordinal is
`IMAGE_EXPORT_DIRECTORY.Base + AddressOfNameOrdinals[i]`. SHA-256 of the full
canonical buffer is the ABI fingerprint.

**Reproduction.** Run the deterministic read-only inspector:

`python tools\inspect_lwbridge_xlua_abi.py ..\LW\lwbridge-0.3.1.exe --game-xlua "$env:LOCALAPPDATA\FunFly\Last War-Survival Game\Game\LastWar_Data\Plugins\x86_64\xlua.dll" --game-xlua-label "%LOCALAPPDATA%\FunFly\Last War-Survival Game\Game\LastWar_Data\Plugins\x86_64\xlua.dll" --output evidence\lwbridge-implementation\2026-09-08-r5-xlua-abi-selector.json`

**Result — RECOVERED, with current-build correlation.** The bundle's expected
fingerprints are:

- secure: `69c9f22bdd71eb1e9ce6f577ef03531685a8528349ff04ccfbeda7de6d788da9`
- plain: `dbf663268c49da286f1a282786e69e18c885a719c7734234b89c43fd521932dd`

The embedded legacy xLua and plain proxy both reproduce the plain fingerprint
exactly. The currently installed official `xlua.dll` reproduces the secure
fingerprint exactly, so current build `1.0.361 / 1078` classifies as `secure`.
The secure proxy's own export table hashes differently because it contains
additional forwarding/wrapper exports; proxy self-exports are not the selector
input.

**Validation and limits.** The classifier algorithm and current installed ABI
mapping are byte-for-byte reproduced. This does not establish a successful
launch, hook load, bridge handshake or heartbeat, and it does not recover the
remaining proof/ticket or child-input contracts.

**Implementation impact.** R5 xLua secure/plain selector recovery is complete.
Future lifecycle code can fail closed on any fingerprint other than the two
bundle values and select `secure` for the identified current client. Production
`profile_instance_start` remains blocked on the other unresolved launch
contracts.

## LWB-R5-003 — child launch validation checkpoint (2026-09-08)

**Scope.** Static recovery now closes a substantial part of the embedded
profile-launcher's child-side validation path. It confirms the three-field
`LaunchEnvelope` parser, recovered descriptor safety gates, the launch-proof
framing/signature/timing path, and the accepted launch-ticket grammar. This is
a recovery checkpoint, not a claim that production lifecycle startup works.

**Source identity.** The outer reference is `../LW/lwbridge-0.3.1.exe`, SHA-256
`2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`.
The embedded `lwbridge-profile-launcher.exe` is extracted from outer RVA
`0xB72B52`, size `0xA7200`, and hashes to
`8f42adb9ed678445425e529cdee8f12a097c63053f9d4de314a758a8dfe362de`.
Addresses below are embedded-launcher preferred VAs with image base
`0x140000000`.

**Exact locators.** The child `LaunchEnvelope` parser is
`0x1400184B0-0x140019154`; its field comparisons at `0x140018625`,
`0x1400186E3`, and `0x140018655` recover `descriptorJson`, `launchProof`, and
`gameLaunchTicket`. The descriptor validator is
`0x14002DB30-0x14002E072`. The launch-ticket parser is
`0x14002E080-0x14002EB4C`. The launch-proof validator is
`0x14002EF70-0x14002F823`.

**Result — RECOVERED.** The descriptor validator has an explicit
`LAUNCH_EXPIRED` gate, constrains the recovered instance identifier to 1–96
bytes, and requires the launch-token field to be at least 32 bytes. Additional
path, isolation, mode and proof-path checks exist in the same function, but
their complete field-to-semantic mapping remains open.

The launch proof is exactly two dot-separated encoded segments: the validator
reads two segments and rejects a third, decodes both through the same recovered
decoder helper, then reaches the signature-verification call at
`0x14002F306 -> 0x14004FF20`. The decoded payload is pipe-delimited and begins
with `LWPM1|launch|...`. Three numeric fields are parsed; the timing gates use a
30,000 ms allowance and reject a recovered timestamp span above 300,000 ms.
After signature/timing checks, recovered claims are compared against the
deserialized descriptor and failures separate `LAUNCH_PROOF_INVALID`,
`LAUNCH_PROOF_EXPIRED`, and `LAUNCH_PROOF_MISMATCH`.

The launch ticket is pipe-delimited. `LWLT1` has six fields and `LWLT2` has
seven. Both require the exact product string `lastwar.windows`. The recovered
issue/expiry span must be 1–300 seconds. A 32-character hexadecimal field and a
128-character hexadecimal signature field are required. `LWLT2` adds a
positive decimal field; its semantic meaning is still **UNKNOWN/BLOCKED**.
Separate downstream errors prove that ticket validity alone is not completion:
`LAUNCH_TICKET_OWNERSHIP_CHANGED`, `LAUNCH_TICKET_CONSUMPTION_FAILED`, and
`LAUNCH_TICKET_CONSUMPTION_TIMEOUT` remain part of the later launch state
machine.

**Reproduction.** Run:

`python tools\inspect_lwbridge_launch_validation.py ..\LW\lwbridge-0.3.1.exe --output evidence\lwbridge-implementation\2026-09-08-r5-launch-validation.json`

The inspector verifies both source hashes, the runtime-function ranges, exact
preferred-VA instruction/call locators, grammar constants, timing bounds and
error markers without executing LWBridge, the embedded launcher, or the game.

**Validation and limits.** This checkpoint is **RECOVERED static**, not
**LIVE-PROVEN**. Complete descriptor field type/semantic mapping is unfinished.
The exact host-side proof producer, decoded claim names/order beyond the
recovered prefix/numeric/timing/match gates, launch-ticket signing inputs,
`LWLT2` extra-field meaning, ownership/consumption protocol, exact outer-host
child argument construction/quoting and current-client acceptance remain
**UNKNOWN/BLOCKED**.

**Implementation impact.** The validator side is now sufficiently bounded that
future lifecycle code must reproduce these gates rather than invent a token
format. Production `profile_instance_start` still fails closed with
`OVERVIEW_LAUNCH_BOOTSTRAP_UNRECOVERED`. The next R5 trace is the outer-host
proof/ticket producer plus ticket ownership/consumption state machine; only
after those prerequisites are recovered should owned start/status/stop be
implemented.
