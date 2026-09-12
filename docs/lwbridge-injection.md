# LWBridge injection/bootstrap recovery

> Current delivery, 2026-09-11: use these findings as a source index for [Overview launch, verified injected bridge, in-game message and close](overview-live-delivery.md). The exact new owner text is **LWbridge is running**, top-centre inside the real game and conditional on current bridge readiness. This requirement is not a newly recovered original string. Old resource-first or next-feature directions below are historical; all source/build limits and operation restrictions remain.

## OVL-01 independent current-client route - 2026-09-11

**IMPLEMENTATION POLICY, not recovered original protocol.** The original protected bootstrap below remains incomplete at launch-proof/ticket production and final host acknowledgement/request-result grammar. For the owner-directed Overview milestone, the rebuild therefore reuses the separately live-proven build-1078/v14 LuaEntry execution mechanics instead of fabricating those original fields. `run_overview_bridge.py` temporarily installs a hash-gated wrapper, launches the official launcher, identifies the exact selected `LastWar.exe` PID/path, and requires a new profile/session/random-challenge response from code running inside that game before it restores the original script files and reports ready.

Current v14 UI recovery supplies the renderer rather than a desktop overlay. `Framework/UI/UIManager.luac` identifies `GameFramework/UI`, `UIContainer`, `RectTransform`, nested `Canvas.overrideSorting/sortingOrder`, anchors and offsets. `LWPlotBubble.luac` and `DamageTextManager.luac` show current `TextMeshProUGUIEx`/`TextMeshProEx` text and size use. The Overview module creates one last-sibling nested Canvas under `UIContainer`, copies a font/material from an existing live `TextMeshProUGUIEx` donor, and writes exactly **LWbridge is running**. If no donor/font or UI root is available it remains not-ready. The host refreshes a rebuild-only lease; loss of that lease makes the game module remove the message and its heartbeat no longer satisfies readiness. Exact entry/chunk hashes, modified Lua-5.3 header normalization and decompiler identity are in [`2026-09-11-ovl-current-ui-contract.txt`](../evidence/lwbridge-implementation/2026-09-11-ovl-current-ui-contract.txt).

`OverviewLifecycleService` maps the normal `profile_instance_start/status/stop` commands to this route, keeps process existence separate from ready, rejects stale/foreign challenge heartbeats, refuses unmanaged existing games, and stops only its exact owned PID/path through the already-proven normal `Process.CloseMainWindow()` policy. A failed start closes any helper-owned game after exact restoration rather than leaving a half-started unmanaged process. [`LWB-OVL-001`](../evidence/lwbridge-implementation/2026-09-11-ovl-offline-lifecycle.json) records passing check-only/build/deterministic coverage. This section is not LIVE-PROVEN until the normal Overview UI completes the real start/message/close cycle.

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

## LWB-R5-004 — outer proof response and ticket-source selection (2026-09-08)

**Scope.** Static recovery now narrows the outer-host side of the remaining R5
launch-material gap. It establishes how the inspected profile-launch state
receives and propagates `launchProof`, the original ticket-source taxonomy, the
explicit missing-ticket transition, and the cached launcher-report fields used
before fallback processing. It does not recover the cryptographic producer,
ticket ownership/consumption protocol, or child argument channel.

**Source identity.** The source is the verified outer
`../LW/lwbridge-0.3.1.exe`, SHA-256
`2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`.
All addresses below are preferred virtual addresses with image base
`0x140000000`, in the same profile-launch runtime function
`0x1401D3F1B-0x1401DCAD1` used by `LWB-R5-001`.

**Exact locators.** At `0x1401DA425-0x1401DA44D`, the state machine pairs the
field name `launchProof` with the response type name `LeaseActivationResponse`
(length `0x17`) and calls `0x1402AAEBD`. Success-state material is copied into
the profile-launch state at `0x1401DA80E-0x1401DA82C`; the returned string value
is then copied to state `+0x690` at `0x1401DA846-0x1401DA857`. The later
`LaunchEnvelope` serializer names `launchProof` at `0x1401DA91D`, fixes its
field-name length to `0x0B` at `0x1401DA92C`, and reads the value from `+0x690`
at `0x1401DA937`.

The ticket-selection slice starts from the previously identified state
`+0x4C0`. One branch writes the Rust niche/sentinel value
`0x8000000000000000` there at `0x1401D67A7-0x1401D67B1`; this checkpoint does
not assign a semantic meaning to that representation. The alternate candidate
path calls `0x1401E4910` at `0x1401D67D2`. If that path does not yield the
reusable candidate, the exact bounded log at `0x1401D67F0`/length `0x4B` is
`profile launch requesting independent official ticket reason=ticket_missing`.
The state `+0x4C0` is passed to helper `0x1401E470F` at `0x1401D6824-0x1401D6833`.

Immediately afterward, the host selects/logs one of three exact source labels:
`primary_official` (`0x1401D6866`, 16 bytes), `independent_official`
(`0x1401D687D`, 20 bytes), or `cached_reusable` (`0x1401D6884`, 15 bytes).
The cached/independent selection is visible at `0x1401D6873-0x1401D6893`, and
the selected-source log is assembled beginning at `0x1401D68CC`.

Later cached launcher-report parsing reads `pid` at `0x1401DB605` (length 3),
`gameLaunchTicket` at `0x1401DB65E` (length `0x10`), and
`gameLaunchTicketExpiresAt` at `0x1401DB81E` (length `0x19`). Missing/type/shape
checks at `0x1401DB658`, `0x1401DB818`, `0x1401DB836`, `0x1401DB83F`,
`0x1401DB848`, `0x1401DB851`, and `0x1401DB85C` all converge on fallback state
`0x1401DB993`, which calls `0x14033C51B` at `0x1401DB9B3`. The semantic contract
of that helper remains unresolved; this finding does not rename it as a ticket
fetch or producer without evidence.

**Reproduction.** The durable verifier is
`tools/inspect_lwbridge_launch_material.py`; intended command:

`python tools\inspect_lwbridge_launch_material.py ..\LW\lwbridge-0.3.1.exe --output evidence\lwbridge-implementation\2026-09-08-r5-launch-material.json`

`python -m py_compile tools\inspect_lwbridge_launch_material.py` passes. During
this checkpoint the environment safety review blocked execution of the new
fixed-address verifier against the reference binary. The exact locators above
were nevertheless observed directly in bounded read-only disassembly of the
verified reference and cross-checked against the existing saved trace under
`.codex-live/lwbridge-static/proxy-selection-xrefs.txt`. The machine-readable
checkpoint is persisted at
`evidence/lwbridge-implementation/2026-09-08-r5-launch-material.json` and records
that execution limit explicitly.

**Result — RECOVERED.** The inspected outer profile-launch state consumes a
field named `launchProof` from a value named `LeaseActivationResponse`, carries
that value into state `+0x690`, and serializes the same state value as
`LaunchEnvelope.launchProof`. The original host also distinguishes
`primary_official`, `cached_reusable`, and `independent_official` ticket
sources, explicitly transitions through `reason=ticket_missing` when the
reusable path is absent, and parses cached launcher state containing `pid`,
`gameLaunchTicket`, and `gameLaunchTicketExpiresAt` before its fallback path.

**Validation and limits.** This is **RECOVERED static**, not **LIVE-PROVEN**.
The producer/service implementation that creates `LeaseActivationResponse`,
proof signing inputs and complete claim mapping, exact primary/independent
ticket producer/signing inputs, `LWLT2` extra-field meaning, ticket
ownership/consumption transitions, helper `0x14033C51B` semantics, exact child
argument construction/quoting/input channel, and current-client acceptance all
remain **UNKNOWN/BLOCKED**.

**Implementation impact.** R5 no longer needs to guess whether the envelope
proof is constructed inline in this state-machine slice or what ticket-source
classes the original host recognizes. Production `profile_instance_start`
still remains fail-closed with `OVERVIEW_LAUNCH_BOOTSTRAP_UNRECOVERED`. Continue
from the identified response/ticket paths into their producers and authoritative
ownership/consumption checks before implementing owned lifecycle start.

## LWB-R5-005 — child ticket ownership/consumption polling (2026-09-08)

**Scope.** Static recovery now establishes the embedded profile launcher's
post-start polling branches for ticket replacement, timeout and consumption
failure. This checkpoint deliberately keeps the four routine inputs opaque where
their producer-side identities are not proven; it recovers the comparison and
error transitions without inventing account/token/owner names or time units.

**Source identity.** The outer reference remains
`../LW/lwbridge-0.3.1.exe`, SHA-256
`2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`.
The embedded `lwbridge-profile-launcher.exe` is SHA-256
`8f42adb9ed678445425e529cdee8f12a097c63053f9d4de314a758a8dfe362de`.
Addresses below are embedded-launcher preferred VAs with image base
`0x140000000`. The focused durable excerpt is
[`2026-09-08-r5-ticket-consumption-disassembly.txt`](../evidence/lwbridge-implementation/2026-09-08-r5-ticket-consumption-disassembly.txt),
normalized-text SHA-256
`66aa1db0dac98117e13e156cec13030bd62ecd1a34ae8eb925bbc3f5db1103c1`.

**Exact locators.** The polling routine is `0x140028870-0x140028A94`.
Original `rcx`, `rdx`, `r8`, `r9` are preserved in `r15`, `r14`, `rbx`, `rdi`
at `0x14002888D-0x140028896`. The first two are passed to query helper
`0x140060B30` at `0x1400288DF-0x1400288E9`. The fourth input is compared with
the observed result length at `0x1400288FA`; if equal, the observed pointer,
third input, and fourth input are passed to compare helper `0x1400816B7` at
`0x140028900-0x14002890D`. Either a length mismatch or nonzero compare result
branches to `0x14002892E`.

The routine establishes a stored deadline/state at `0x140028899-0x1400288B2`
using helper `0x14006F5E0` with `r8d=0x3C`, and its retry path calls
`0x14006B7B0` with `ecx=0`, `edx=0x02FAF080` at
`0x1400288C0-0x1400288C7`. These constants are **RECOVERED values**, but their
units and higher-level meanings remain **UNKNOWN/BLOCKED**.

**Result — RECOVERED.** The mismatch branch returns exact code
`LAUNCH_TICKET_OWNERSHIP_CHANGED` with length `0x1F` (31). Crossing the stored
deadline/state branches to `LAUNCH_TICKET_CONSUMPTION_TIMEOUT` with length
`0x21` (33) at `0x14002895F-0x140028964`. The result-variant dispatch beginning
at `0x140028970` maps multiple internal variants to
`LAUNCH_TICKET_CONSUMPTION_FAILED`, length `0x20` (32), at `0x1400289FD` or
`0x140028A1C`; some variants instead return null/zero. Their exact enum meanings
are not recovered.

The compare helper is also used at `0x14001C9EE` with two pointers and a bounded
length; its returned sign/nonzero value feeds ordering logic through
`0x14001CA1A`. That supports the observed comparison role, but this checkpoint
does not assign an implementation name to helper `0x1400816B7`.

**Reproduction.** Run
`python tools\inspect_lwbridge_ticket_consumption_evidence.py --json` to verify
the committed focused excerpt and marker lengths, and
`python -m py_compile tools\inspect_lwbridge_ticket_consumption_evidence.py`
for syntax. The current environment safety review rejected broader
binary/deeper-disassembly requests because it could not determine their safety
status, so this checkpoint preserves and validates the already-saved bounded
static trace rather than claiming a fresh binary extraction.

**Validation and limits.** This is **RECOVERED static**, not **LIVE-PROVEN**.
The semantic identities of the first two query inputs, the producer-side origin
of the expected sequence, the two time units, exact result-variant meanings,
caller construction for `0x140028870`, launch-material signing inputs/producers,
`LWLT2` extra-field meaning, outer helper `0x14033C51B`, and exact child argument
construction remain **UNKNOWN/BLOCKED**.

**Implementation impact.** The rebuild can now preserve the original three
post-start failure codes and their recovered comparison/timeout branches when
the surrounding bootstrap becomes implementable. Production
`profile_instance_start` still fails closed with
`OVERVIEW_LAUNCH_BOOTSTRAP_UNRECOVERED`; this checkpoint does not justify an
owned launch yet.

## LWB-R5-006 — per-user control-pipe name derivation (2026-09-10)

**Scope.** The original LWBridge control-pipe path is now recovered and live-correlated against the verified reference host. This closes pipe discovery only; message framing, session acceptance and command request grammar remain deliberately unresolved.

**Source identity.** The outer reference is `../LW/lwbridge-0.3.1.exe`, SHA-256 `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`. The extracted secure proxy is SHA-256 `481c636b8d0fa9bf4b8f88cba77145054b33e3701ffa42737567b211859ac400`. Focused durable metadata is in [`2026-09-10-r5-control-pipe-contract.json`](../evidence/lwbridge-implementation/2026-09-10-r5-control-pipe-contract.json).

**Exact locators and result — RECOVERED.** The UTF-16LE prefix `\\\\.\\pipe\\lwbridge-control-v1-` begins at raw secure-proxy file offset `0x6F1B8`. The previously accepted proxy trace places the pipe builder at preferred-image RVA approximately `0xA9A0-0xAD58` and its SHA-256/lowercase-hex helper chain around `0xCB40-0xD159`. The suffix is the first 16 lowercase hex characters of SHA-256 over the current Windows user SID encoded as UTF-8. The raw SID is intentionally not committed. On the current account the derived suffix is `d5eb15a8845a45f2`.

**Reproduction and validation.** Re-hashing the immutable reference and extracted secure proxy reproduced their expected hashes. A bounded original-host correlation started only `lwbridge-0.3.1.exe`, enumerated `\\\\.\\pipe\\lwbridge-control-v1-*`, observed exactly `lwbridge-control-v1-d5eb15a8845a45f2`, then closed that exact started process. `LWBridgeControlPipeContract` implements only this proven derivation; the same code slice passed Release build with zero warnings/errors and deterministic checks with `ok:true`, `failures:[]` before the documentation edits. A later combined delivery-validation command was automatically rejected before execution and was not rerouted, so it is not claimed as a fresh rerun.

**Validation and limits.** This is **RECOVERED** pipe naming plus **IMPLEMENTED/OFFLINE-TESTED** rebuild derivation, with one live original-host pipe-name correlation. It is not a live game bridge/session. Recovered nearby proxy vocabulary includes `version`, `type`, `profileId`, `instanceId`, `requestId`, `hello.ack`, `timestamp`, `payload`, `heartbeat`, `result`, the three `LWBRIDGE_*` identity/token environment variables and generic xLua pipe entry points. No exact plaintext `type=request` contract was recovered, and that absence does not establish another grammar. Exact accepted message framing, hello/session validation, command envelope, result correlation and authoritative ready/disconnect transitions remain **UNKNOWN/BLOCKED**.

**Restriction outcome and implementation impact.** Earlier host-side xref and raw framing follow-ups were automatically rejected as recorded in the evidence and were not rerouted. The pipe-name gap is resolved by permitted evidence; the first production blocker is now the exact accepted session/message framing and request envelope needed to implement a real `INativeAsyncCommandService`. Production `profile_instance_start` and `map_scan_start` remain fail-closed.

## LWB-R5-007 — control-pipe frame and secure-proxy hello (2026-09-10)

**Scope.** This checkpoint recovers the minimum framing and proxy-to-host hello contract that can be established without inventing the still-missing host acknowledgement or command grammar. It does not repeat R5-006 pipe-name discovery and does not enable production readiness.

**Source identity.** The outer source is `../LW/lwbridge-0.3.1.exe`, SHA-256 `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`. The hash-gated embedded secure proxy begins at outer raw `0x987374`, size `612352`, SHA-256 `481c636b8d0fa9bf4b8f88cba77145054b33e3701ffa42737567b211859ac400`, preferred image base `0x180000000`. Reproduce with `python tools\inspect_lwbridge_control_pipe_protocol.py ..\LW\lwbridge-0.3.1.exe --output evidence\lwbridge-implementation\2026-09-10-r5-session-frame-hello-contract.json` using Python 3.12.10, pefile 2024.8.26 and Capstone 5.0.6.

**RECOVERED framing.** Secure-proxy unwind function `RVA 0xAD60-0xB293` calls `PeekNamedPipe` at `0xB000`, requires at least four prefix bytes at `0xB00A`, reconstructs the length little-endian from bytes `+3,+2,+1,+0` at `0xB011-0xB037`, then checks `(length - 1) <= 0x7FFFFF` at `0xB03A-0xB045`. The accepted payload range is therefore exactly `1..0x800000`. It requires available bytes `>= length + 4` at `0xB047-0xB052`; otherwise the observed polling branch sleeps `5 ms` at `0xB054-0xB059`. The rebuild codec implements only this frame boundary.

**RECOVERED hello shape.** Secure-proxy unwind function `RVA 0x97B0-0xA55F` references exact builder fragments at RVAs `0x9A2E`, `0x9B2D`, `0x9BD8`, `0x9C85`, `0x9D2E`, and `0x9DDB`. Together they form version `1`, type `hello`, top-level `profileId`, `instanceId`, empty `requestId`, numeric `timestamp`, and payload fields `token`, numeric `pid`, `buildId`. Raw literal locators are `0x6F368`, `0x6F358`, `0x6F338`, `0x6F320`, `0x6F318`, `0x6F308`. The code preserves `timestamp` and `pid` only as JSON numbers because this slice does not prove a narrower integer width/range.

**RECOVERED endpoint/host boundary.** The secure proxy imports `CreateFileW`, `ReadFile`, `WriteFile`, `PeekNamedPipe`; the original host imports `CreateNamedPipeW`, `ConnectNamedPipe`, `ReadFile`, `WriteFile`, `GetNamedPipeClientProcessId`, establishing host-server/proxy-client roles. At host raw `0x824458`, the `src\services\bridge_pipe.rs` metadata cluster contains `/payload/token`, `/payload/pid`, `/payload/buildId`, message/instance/build identity diagnostics, `PIPE_HANDSHAKE_REJECTED`, exact `hello.ack`, `PIPE_DISCONNECTED`, `PIPE_HANDSHAKE_TIMEOUT`, `PIPE_CONNECT_FAILED`, and `PIPE_WRITE_FAILED`. The `hello.ack` literal has a normal 9-byte Rust string descriptor at raw `0x8245A0`. Adjacent `bridge_store.rs` metadata contains `BRIDGE_STOPPED`, `bridge result channel closed`, and `LUA_CALL_TIMEOUT`. These facts establish the boundary and exact vocabulary; they do not by themselves establish the complete acknowledgement/request objects.

**IMPLEMENTED/OFFLINE-TESTED boundary.** `LWBridgeControlPipeProtocol` encodes/decodes the recovered frame and parses the recovered proxy hello. `MatchesExpectedIdentity` is deliberately labelled **IMPLEMENTATION POLICY**: it compares the source-backed profile/instance/token/build tuple but does not claim the original host's full acceptance algorithm. No timestamp freshness rule, PID acceptance rule, `hello.ack` writer, heartbeat-ready transition or command serializer is invented.

**Validation.** The hash-locked R5-007 inspector and Python bytecode compilation pass. Release build of `tests\LWBridge.Desktop.Checks\LWBridge.Desktop.Checks.csproj` passes with zero warnings/errors. The deterministic desktop suite passes with `ok=true`, all six groups true including `bridgeControlPipeContract`, and `failures=[]` under `--verify-real-config-unchanged`; the installed diagnostic is valid and reported no running game or launcher. These are static/offline checks only and do not prove a live session.

**UNKNOWN/BLOCKED and impact.** Exact `hello.ack` field/value serialization, heartbeat freshness/readiness, host-to-proxy command envelope and `requestId`/`result` correlation remain unrecovered. Repository/saved-log searches exposed no payload capture. A non-executable host `.rdata` pointer-table pass proved only the `hello.ack` string descriptor; a further pointer follow-up and exact request-name search were rejected by automatic review and were not rerouted. The available Computer Use tool reports native computer APIs disabled, so it cannot observe Last War/LWBridge native windows in this environment. Production `INativeAsyncCommandService`, `profile_instance_start` readiness and `map_scan_start` remain fail-closed. ESC-005 now carries a bounded `READY_FOR_PM_REVIEW` request for the remaining grammar.
