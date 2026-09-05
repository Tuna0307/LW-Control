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

## Reference for bundle entry layout

The .NET runtime's own
[FileEntry implementation](https://github.com/dotnet/runtime/blob/main/src/installer/managed/Microsoft.NET.HostModel/Bundle/FileEntry.cs)
documents and writes bundle-entry offsets, sizes, compression lengths, types,
and relative names. Artifact-specific counts and addresses above come from the
uploaded executable, not from that external source.
