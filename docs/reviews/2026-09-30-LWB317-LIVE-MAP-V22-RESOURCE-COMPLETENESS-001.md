# LWB317-LIVE-MAP-V22-RESOURCE-COMPLETENESS-001 — current-v22 Resource acquisition completeness

Date: 2026-09-30

## Result

Task state: `COMPLETE / AWAITING_REVIEW`.

The current-v22 Resource acquisition defect is `LIVE_PROVEN`, and the corrected
current-client Resource path is `CURRENT_PATH_COMPLETE`: every Resource
observation exposed through the corrected acquisition route is internally
accounted for by the full-world scan, deduplication and published Map317 counts.

This result does **not** promote a stronger claim than the evidence supports:

- `GAME_UNIVERSE_COMPLETE = UNKNOWN` — no evidence proves that every Resource
  object the game could ever retain or know about is observable through this
  current acquisition route;
- original protected/private LWBridge traversal equivalence remains `UNKNOWN`;
- the already recovered public Map317 behavior remains `EXACT_CONTRACT` where
  established by the 0.3.17 recovery work.

No third full live scan was run during resume. The latest corrected full proof
finished after every material Resource acquisition/diagnostic source change and
there are already two independent corrected full-world successes.

## Why the earlier 678 result was misleading

`LWB317-LIVE-MAP-V22-001` completed all 2,500 logical scan blocks and published
678 Resource rows. That proved the lifecycle/query pipeline worked, but it did
not prove that the Resource acquisition population was complete.

The later completeness instrumentation reproduced the same acquisition scheme
with enough accounting to show the actual loss mechanism. The baseline
`live-proof.json` also completed `2500/2500`, with zero failed/unread blocks, but
observed:

| Counter | Baseline value |
|---|---:|
| raw `_pointInfos` occurrences | 19,117 |
| non-Resource occurrences | 17,414 |
| Resource candidates | 1,703 |
| accepted before dedupe | 296 |
| rejected Resource candidates | 1,407 |
| duplicate accepted keys | 0 |
| final unique Resources | 296 |
| populated native AOIs | 206 |
| zero-Resource native AOIs | 9,794 |

Every one of the 1,407 rejected Resource candidates was rejected as
`outside_selected_aoi`. The baseline accepted population covered only
`x=170..998`, `y=0..799` instead of the full `0..999` world. Published source,
search, summary and options counts all reconciled at 296, which means the loss
occurred during acquisition/association before publication.

The failure therefore was not a level, fullness, black-tile or Resource-type
filter. The scanner could report a completed 2,500-block run while the returned
runtime population belonged to the wrong/current camera AOI footprint.

## Live spot-check proof of the root cause — `LIVE_PROVEN`

`runtime-inspection-immediate-coverage.json` captured three AOIs that the
baseline scan had reported as zero-Resource. Holding/navigating each target view
showed a genuine `ResPointInfo`, while the production-equivalent remote coverage
request immediately restored the camera and returned zero matching Resources:

| Requested AOI | Live Resource | Resource coordinate | Immediate result | Rejected candidates |
|---|---:|---|---:|---:|
| around `(55,55)` | `58054` | `(53,58)` | 0 Resources | 15 `outside_selected_aoi` |
| around `(505,505)` | `508510` | `(509,508)` | 0 Resources | 16 `outside_selected_aoi` |
| around `(905,905)` | `901901` | `(900,901)` | 0 Resources | 12 `outside_selected_aoi` |

All three live rows were `pointType=7`, runtime class `ResPointInfo`, and world
type `WorldResource`. The old diagnostic records
`positionRestoredBeforeResponse=true` for the production-equivalent request.

The proven defect is response association caused by early camera restoration:
the remote view was moved and requested, then restored in the same tick before
the correlated remote AOI response had been fully captured/serialized. When the
response arrived, Resource candidates could therefore be measured against a
different/current AOI footprint and be rejected as `outside_selected_aoi`.

## Production correction — `LIVE_PROVEN`

The correction is deliberately Resource-scoped.

`CurrentClientMapBlockSource.FastCity.cs` sets
`deferRestoreUntilResponse=true` only when Resource acquisition is selected and
requires the returned proof to match that choice. The Lua bridge rejects a
deferred-restore request unless `includeResource=true`.

For a Resource batch the flow is now:

1. move the temporary camera to the remote target;
2. issue the native AOI request;
3. wait for the correlated remote response;
4. enumerate and serialize the Resource result while that response is current;
5. restore the original camera/native state;
6. force a normal home-view `UpdateViewRequest(true)`;
7. wait until the restored camera tile, manager/world response flags and split
   request state prove that the home response completed;
8. only then publish success and allow the next batch.

The corrected live evidence records
`positionRestoredBeforeResponse=false`,
`positionRestoredAfterResponse=true`, and request method
`WorldPointManager.UpdateViewRequest(true)+deferred-restore-after-response`.
The corrected runtime inspection's three zero-AOI probes returned 32, 51 and 50
Resource rows from their production-equivalent remote coverage calls with zero
Resource-candidate rejections; after the restored-home transition, none of the
held-view Resource IDs was missing from the corrected remote coverage result.

The managed side still requires a fresh result correlated by request ID and
honors cancellation while polling. The Lua side fails closed on the remote
response timeout, restoration failure, or a three-second restored-home response
timeout. Non-Resource-only fast batches retain their existing same-tick
restoration contract. No Map317 public request/result contract changed.

## Corrected full-world proofs

Two corrected full-world Resource proofs completed successfully.

| Proof | Resource candidates | Accepted occurrences | Rejected | Duplicate occurrences | Unique Resources | Populated AOIs | Zero-Resource AOIs | Bounds |
|---|---:|---:|---:|---:|---:|---:|---:|---|
| `corrected-live-proof-success1.json` | 8,062 | 8,062 | 0 | 54 | 8,008 | 5,532 | 4,468 | `(0,0)..(999,999)` |
| `corrected-live-proof.json` | 8,060 | 8,060 | 0 | 53 | 8,007 | 5,540 | 4,460 | `(0,0)..(999,999)` |

Both scans completed `2500/2500` logical blocks with zero failed, unread or
inflight blocks and zero native pending/dropped records. Their source/search/
summary/options counts reconcile to their respective unique Resource totals.

The one-record difference is not evidence of scanner loss. The two independent
live snapshots have different occupancy, level and Resource distributions while
each independently reconciles with zero rejected candidates and full spatial
coverage. The evidence therefore supports live-world snapshot variation; it does
not establish an immutable total Resource count.

Proof #1 also contained one `resourceNameKey=800817` / Resource type `15` row;
that row/type is absent from the latest proof. Its complete level distribution
also included special levels 13, 14, 15, 21, 23, 25, 28 and 33. The latest
snapshot's exact distribution is reported below rather than assuming the two
live snapshots should be identical.

## Latest corrected Resource population

The latest proof (`corrected-live-proof.json`, finished
`2026-09-30T13:37:48Z`) contains 8,007 unique Resources.

Resource name-key distribution:

| Name key | Count |
|---|---:|
| `100281` | 2,718 |
| `100317` | 2,676 |
| `129027` | 2,613 |

Resource type distribution:

| Resource type | Count |
|---|---:|
| `1` | 2,718 |
| `14` | 2,676 |
| `2` | 2,613 |

Complete latest level distribution:

| Level | Count | Level | Count |
|---:|---:|---:|---:|
| 1 | 963 | 10 | 305 |
| 2 | 923 | 14 | 4 |
| 3 | 1,752 | 15 | 3 |
| 4 | 710 | 23 | 1 |
| 5 | 1,308 | 25 | 5 |
| 6 | 512 | 27 | 2 |
| 7 | 808 | 28 | 1 |
| 8 | 376 | 33 | 5 |
| 9 | 329 |  |  |

Black-tile diagnostic:

- false: 8,007;
- true: 0;
- unknown: 0.

Occupancy:

- occupied: 411;
- unoccupied: 7,596.

Resource detail/fullness:

- full: 5,837;
- partial: 1,759;
- empty: 0;
- unknown: 411.

The unknown-detail and occupied counts are both 411 in this snapshot; no
acquisition filter removes either population.

## Duplicate analysis

The latest source report contains 53 duplicate accepted-key occurrences.
Every duplicate comparison is classified
`repeated_same_resource_identity`, and all 53 have
`sameSemanticIdentity=true` across the semantic identity fields captured by the
diagnostic.

Current evidence therefore does not show distinct Resources being incorrectly
collapsed by the point-ID record key. Deduplication was left unchanged.

## Alternate Resource-source investigation

The corrected runtime inspection performed a read-only inventory after a full
Resource scan. It checked `_pointInfos`, `allViewPoints`, `outOfViewPoints`,
`outOfViewPointsObj`, `yellowLand`, `uuidInfoMap`, `uuidInfoDesertMap`,
`timeOutPoints`, `GetAllCollectRangePoint`, `GetAllDragonResourceList`,
`GetCollectPoint`, `GetResourcePointInfoByIndex`, and related
`WorldPointManager` state.

Observed retained Resources did not reveal an additional population beyond the
known scan set:

- `_pointInfos`: 8 recognized Resources, all 8 overlapping the known scan, zero
  additional;
- `allViewPoints`: 8 recognized Resources, all 8 overlapping the known scan,
  zero additional;
- `outOfViewPoints`, `outOfViewPointsObj`, `uuidInfoDesertMap` and
  `timeOutPoints`: zero retained Resources in the inspected state;
- `yellowLand`: 9,744 alliance-city entries, zero Resources;
- `uuidInfoMap`: 514 non-Resource entries, zero Resources;
- `GetAllDragonResourceList`: successful call, zero entries;
- `GetAllCollectRangePoint` for observed Resource types 1, 2 and 14: successful
  calls, zero entries;
- `GetCollectPoint` for those types: successful calls returning point ID 0;
- typed `GetResourcePointInfoByIndex` lookups recognize live `ResPointInfo`
  entries already present in the observed Resource set.

This is evidence against a proven retained-list fallback analogous to the City
source. It is not proof that no other game-internal Resource universe can exist,
so `GAME_UNIVERSE_COMPLETE` remains `UNKNOWN` and no speculative fallback was
implemented.

Earlier low-coverage runtime inspections did find `_pointInfos`/`allViewPoints`
Resources absent from the low-coverage completed scan; after the correction the
same comparison reports zero additional-to-known-scan Resources. That before/
after result is consistent with the acquisition defect and correction rather
than evidence for a separate retained Resource population.

## Point-type investigation

The latest corrected full scan observed raw point types `6`, `7`, `11`, `17`,
`21`, `25`, `30` and `31`. All 8,007 accepted Resources are point type `7`.

Raw occurrence counts were: type 6 = 6,897; type 7 = 8,060; type 11 = 91;
type 17 = 684; type 21 = 40; type 25 = 319; type 30 = 23; and type 31 = 46.

Live reflection and exact current-client enum evidence classify the relevant
types as:

| Point type | Current-client meaning | Resource candidate? |
|---:|---|---|
| 6 | `PlayerBuilding` / `BuildPointInfo` | no |
| 7 | `WorldResource` / `ResPointInfo` | yes |
| 11 | `WORLD_ALLIANCE_CITY` / observed `AllyCityPointInfo` | no |
| 17 | `HERO_DISPATCH` / `HeroDispatchMissionPointInfo` | no |
| 21 | `TREASURE` / observed `TreasurePointInfo` | no |
| 25 | `WORLD_CITY_STRONGHOLD` | no |
| 30 | `CITY_ATTACHMENT_BUILD` | no |
| 31 | `CITY_ATTACHMENT_WALL` | no |

Candidate types were not broadened merely to raise the Resource count.

## Current-path completeness classification

`CURRENT_PATH_COMPLETE = LIVE_PROVEN` means all Resource observations exposed by
the corrected current-client Resource acquisition route are accounted for:

- all 2,500 logical blocks / 10,000 native AOIs are represented;
- corrected scans have zero Resource-candidate rejections;
- both coordinate axes reach the full `0..999` world bounds;
- raw candidate, accepted, duplicate and unique counters reconcile;
- all accepted distributions reconcile to the unique count;
- source unique, `map_search`, `map_summary` and `map_data_options` counts agree.

It does not mean `GAME_UNIVERSE_COMPLETE`, and it does not prove the protected
original LWBridge used the same private traversal implementation.

## Implementation/evidence inventory

Task-owned changes separate cleanly into these roles:

- production/current-client correction:
  `src/LWBridge.Desktop/CurrentClientMapBlockSource.FastCity.cs` and the runtime
  bridge behavior in `tools/current_live_resource_probe.lua`;
- diagnostics:
  `src/LWBridge.Desktop/ResourceCompletenessDiagnostics.cs` plus duplicate and
  distribution evidence emitted by the probe;
- deterministic tests:
  `tests/LWBridge.Desktop.Checks/CurrentClientMapBlockSourceChecks.cs` and
  `tests/LWBridge.Desktop.Checks/ResourceCompletenessDiagnosticsChecks.cs`;
- full proof harness:
  `tests/LWBridge.Desktop.Checks/LiveMapV22ResourceCompletenessProof.cs`;
- read-only runtime inspection harness:
  `tests/LWBridge.Desktop.Checks/LiveMapV22ResourceRuntimeInspectionProof.cs`;
- CLI harness registration:
  `tests/LWBridge.Desktop.Checks/Program.cs`;
- preserved raw/baseline/corrected evidence under
  `evidence/lwbridge-0.3.17/map/LWB317-LIVE-MAP-V22-RESOURCE-COMPLETENESS-001/`.

The two historical success/baseline proof JSON files preserve hashes for their
run-specific suffixed source/published sidecars even though their embedded
generic sidecar path strings were later reused by the final run. The preserved
suffixed sidecars match those historical hashes, so this bookkeeping detail does
not lose evidence or require another scan.

## Deterministic/offline verification

Resume verification ran with no Last War/LWBridge runtime active:

- `dotnet run --project tests/LWBridge.Map-0.3.17.Checks/LWBridge.Map-0.3.17.Checks.csproj -c Release`
  — PASS, `LWB317_MAP_CHECKS_OK`;
- `dotnet build src/LWBridge.Map-0.3.17/LWBridge.Map-0.3.17.csproj -c Release --nologo`
  — PASS, 0 warnings / 0 errors;
- `dotnet build tests/LWBridge.Desktop.Checks/LWBridge.Desktop.Checks.csproj -c Release --nologo`
  — PASS, 0 warnings / 0 errors;
- full built `LWBridge.Desktop.Checks.exe` — exit 0, `ok=true`, all six
  deterministic groups true, installed diagnostic valid, `failures=[]`, game
  and launcher absent;
- `tools/current_live_resource_probe.lua` — `LUA_PARSE_OK` with `luaparser`;
- final `git diff --check` is required again immediately before commit.

## Cleanup

The two corrected proof files each record their owned game process stopped,
their temporary DB deleted, and the official v22 package restored. Resume cleanup
is preserved in `cleanup-resume-final.json` and independently confirms:

- no matching Last War/LWBridge/launcher/proof process;
- no task-prefixed `lwb317-resource-completeness-*` or
  `lwb317-resource-runtime-*` DB/WAL/SHM artifact;
- no shared Overview recovery journal at
  `%LOCALAPPDATA%/LWBridgeRebuild/overview-bridge/recovery.json`;
- installed `LWScripts.data` SHA-256 is
  `248f3aeac712b3f14f86bff37a0c365e467897a2248403837c44c1b774f05b22`,
  the validated official v22 package.

## UI integration status and remaining Map work

The real Desktop Map backend is currently wired through
`src/LWBridge.Desktop/WebUi` and its production command/API bundle includes
`map_scan_start`, `map_scan_stop`, `map_scan_clear`, `map_search`, `map_summary`
and `map_data_options` (plus the other recovered Map commands).

`src/LWBridge.UI-0.3.17` remains the clean/static 0.3.17 UI reconstruction. Its
source does not wire those real backend Map commands yet. This task does not
perform that UI integration.

The overall `LWB317-RE-MAP-001` Goal therefore remains `IN_PROGRESS`. Remaining
Map work includes server jump, restart/resume, live validation of the other Map
acquisition categories and remaining supported actions, the existing fail-closed
Treasure claim/status and Ghost preparation gaps, and final clean UI integration.
