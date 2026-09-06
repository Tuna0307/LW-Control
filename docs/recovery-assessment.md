# LWControl managed-code recovery assessment

This follow-up advances the initial string-based assessment by parsing the
application's bundle directory and the CLI metadata of two embedded assemblies.
No uploaded code was executed. Missing developer documentation does not block
this kind of static analysis.

## Confirmed bundle structure

The .NET bundle signature occurs at byte 8,351,608 of `LWControl.exe`. Its header
pointer resolves to byte 102,861,360. The directory declares bundle version 6.0
and 269 entries. Parsing those entries terminates at byte 102,876,940, exactly
the end of the supplied executable. Entry offsets and stored lengths were
checked against the containing file's bounds.

The directory contains 264 entries classified as assemblies, three native
entries, one runtime-configuration entry, and one dependency-configuration entry.
Bundle format version 6 is distinct from the application's .NET runtime version.

Application-specific assembly entries include:

| Assembly | Declared uncompressed bytes |
| --- | ---: |
| `LWControl.dll` | 43,364,352 |
| `LWControl.r2r.dll` | 72,130,560 |
| `LastWarControl.Core.dll` | 380,928 |
| `LastWarControl.Game.dll` | 4,894,720 |
| `LastWarControl.Bridge.dll` | 24,576 |
| `LastWarControl.Auth.dll` | 110,592 |

Only `LWControl.dll` and `LastWarControl.Core.dll` were copied out for further
metadata inspection. Their output lengths match their bundle entries, and both
have PE and CLI metadata headers. These local analysis copies are not published
in this repository. The original hash remains recorded in `evidence.json`.

This supersedes the initial report's uncertainty about whether the application
is a .NET bundle: its directory is now parsed, not merely inferred from strings.

## Metadata and method-body findings

| Assembly | Type definitions | Method definitions | Methods with IL body RVAs |
| --- | ---: | ---: | ---: |
| `LWControl.dll` | 389 | 3,765 | 3,752 |
| `LastWarControl.Core.dll` | 256 | 2,510 | 2,471 |

Both assemblies contain standard metadata streams: `#~`, `#Strings`, `#US`,
`#GUID`, and `#Blob`. The counts above come from the TypeDef and MethodDef tables,
not from matching printable strings. Every nonzero method-body RVA in these
two assemblies maps to a recognized tiny or fat IL header form. Counts include
property accessors, constructors, and compiler-generated code; they are not
counts of distinct user-facing features.

These checks establish retained metadata and candidate IL bodies. They do not
validate every instruction, resolve every reference, prove successful C#
decompilation, or establish that the reconstructed code compiles. The original
source files, comments, project structure, and tests have not been recovered.

## Feature structure is now visible in type metadata

The core assembly defines separate components for:

- Daily free claims: options, eligibility checks, planners, adapters, verification,
  and recovery policy.
- Radar: configuration, task/state models, planning, startup recovery, and verification.
- Rally participation: configuration, candidate/team state, planning, and verification.
- Troop promotion: barracks/inventory models, options, planning, verification, and recovery.
- Shared automation: scheduling, feature running, resource locks, and runtime/configuration stores.

Daily claims is the selected first candidate for a bounded feature study. Its
`DailyFreeClaimsFeature` type declares `Detect`, `CanRun`, `BuildPlan`,
`ExecuteAsync`, `Verify`, `Recover`, and `Stop`. Related types include
`DailyFreeClaimsPlanner`, `FreeEligibilityVerifier`, and `FreeClaimVerifier`.

Named adapter types cover VIP daily rewards, store free packs, daily and weekly
task chests, login rewards, tavern free recruitment, and campaign idle rewards.
This establishes a feature decomposition. It does not yet reveal their exact
eligibility rules or demonstrate a live action.

## Implication for the reconstruction

LWControl remains the preferred reference because it demonstrably retains named
managed types and method bodies. Comparable source-level recoverability has not
been established for the Rust lwbridge executable. This is a reasoned choice,
not a guarantee of full feature parity or an estimate of completion time.

The next technical work is to recover and understand a bounded set of daily-claim
decision methods, identify their inputs and outputs, and derive behavioral tests
from the actual logic. The current Python example remains a synthetic prototype;
it must not be presented as recovered behavior.

An SDK is not necessary to continue static study of ordinary feature logic,
configuration formats, and application structure. Live integration remains
unimplemented and requires separate investigation and Windows testing. The
metadata findings do not establish that a complete, working bot can be rebuilt
without additional dependencies or runtime observations. This assessment does
not implement game injection, license bypass, or protection circumvention.

## 2026-09-06 world-map scanner checkpoint

The current installed game now provides a stronger reconstruction boundary than
the earlier targeted-search work. Static inspection of the current
`Assembly-CSharp.rdl` proves that `WorldPointManager` owns structured loaded
world-point state and drives an AOI/block request pipeline through
`WorldGetBlockMessage`, `HandleViewPointsReply`, `ParseWorldGetBlock`, and
`AddPointInfo`. The current generated `Protobuf.WorldPointInfo` also exposes
structured point identity/routing fields and typed payloads.

This moves the World Map Scan feature beyond loaded-state discovery: the bulk
source is identified, the read-only snapshot contract is implemented and proven
live, and bounded multi-block, serial multi-batch, plus a two-inflight
concurrent wave are now proven end to end. The detailed
evidence, PROVEN/UNKNOWN boundary, request fields, and schema are recorded in
[`current-world-map-snapshot.md`](current-world-map-snapshot.md) and
[`current-world-map-static-evidence.json`](current-world-map-static-evidence.json).
No game launch, network send, or installed-file modification was used for this
checkpoint.

The dedicated current-build world-map probe first proved the loaded-state boundary
live. Reverse engineering of the current `UIMainChangeScene`/`SceneUtils`
bytecode showed that the current UI uses a Lua `UIButton:SetOnClick` callback and
that the canonical City-to-World routine is `SceneUtils.ChangeToWorld`; the
older raw Unity `Button.onClick:Invoke()` assumption did not change the current
scene. Candidate a13 used the current routine once, observed authoritative
`CS.SceneManager.CurrSceneID == SceneManagerSceneID.World`, resolved
`CS.SceneManager.World.PointManager`, waited for `_pointInfos` to stabilize at
86 records, and emitted a schema-v1 snapshot. Exact installed hashes were
restored afterward. The evidence and remaining wider-AOI boundary are recorded
in [`current-world-map-live-capture.md`](current-world-map-live-capture.md) and
[`current-world-map-live-evidence.json`](current-world-map-live-evidence.json).

Candidate a20 then proved the next transport boundary. With the visible AOI at
blocks X `53..57`, Y `46..49`, it selected adjacent logical block `(58,47)` and
used the original scanner's recovered minimum 5-by-4 padded transport envelope,
X `56..60`, Y `46..49`. A small managed bridge supplied the exact current C#
argument types to `WorldPointManager.SendAoiRequest`. One request produced one
correlated `world.get.block` response whose authoritative nested
`serverPointArr` bounds covered exactly that padded envelope. The manager then
contained 13 identities not present before the request. There were no retries or
camera moves, the response hook and touched manager flag were restored, and the
installed game/script hashes were restored exactly afterward.

The recovered scanner policy is now implemented offline in
`src/LWControl.Core/RecoveredWorldMapScan.cs`. The clean-room planner mirrors
the original centered odd scan window, row-major block IDs, 160-index native
batch limit, minimum 5-by-4 transport padding, serpentine batch order, and
logical-block response coverage accumulation. Its checks reproduce the proven
a20 request geometry and verify that partial response coverage does not become a
successful batch.

A bounded three-logical-block candidate (`a21`, package SHA-256
`3969bfefb53a2c4da757f184ca6a891e4b4c20d836b0150640233e2488c466ad`)
was then executed live. One padded request covered all three requested logical
blocks through a correlated authoritative `world.get.block` envelope, with zero
retry/camera activity and exact post-run hash restoration.

Candidate `a22` then exercised the recovered 160-index split with the smallest
odd square above that ceiling: 13 by 13 = 169 logical blocks. The planner
produced 156 + 13 logical blocks. Batch 1 reached 156/156 correlated coverage
before batch 2 was sent; batch 2 reached 13/13. The final result was 169/169,
exactly two sends, zero retries, zero camera moves, restored hook/manager state,
and exact installed-file hash restoration. Package SHA-256 was
`6e8abb9bbbedb7292d959f6006ddb9db04370c8e5a7e6e94e2af935f9b08defd`.

Candidate `w23` then exercised the recovered serial-first/concurrent scheduling
shape using a 19-by-19 logical window. The recovered split was 152 + 152 + 57.
Batch 1 completed 152/152 first. Batches 2 and 3 were then both sent before the
first concurrent response arrived: send completion events were 2 and 4, the
wave completed at event 5, and responses arrived at events 6 and 7. The final
result was 361/361, peak in-flight width 2, exactly three sends, zero retries,
zero camera moves, and exact hook/manager/file restoration. Package SHA-256 was
`c67b7c71dc1e422e46d86f5a54ecba330c6c1a369f4419d37ee7aa679acd33fd`.

The remaining scanner work is broader orchestration and full-world completion
proof. The wider original concurrency policy, retry/camera fallback, and
terminal full-world coverage remain unproven.

## Reference for bundle entry layout

The .NET runtime's own
[FileEntry implementation](https://github.com/dotnet/runtime/blob/main/src/installer/managed/Microsoft.NET.HostModel/Bundle/FileEntry.cs)
documents and writes bundle-entry offsets, sizes, compression lengths, types,
and relative names. Artifact-specific counts and addresses above come from the
uploaded executable, not from that external source.
