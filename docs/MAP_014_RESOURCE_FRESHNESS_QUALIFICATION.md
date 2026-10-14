# MAP-014 Resource FOV20 freshness qualification — 14 October 2026

**Decision: Do not enable Resource FOV20 as a production default.** The source
supports a large repeatable *point-AOI acquisition* speed improvement with
complete geometric coverage and excellent shared-record field consistency, but
it does not prove that all changing Resource identities were absent from the
native world at the time of each candidate request. A source-attested same-AOI
temporal inverse is still necessary. This is an **offline audit of preserved
genuine evidence**, not a newly run game experiment.

## Immutable inputs, independent checks and reproducibility

- Read only: `artifacts/map014-lead-live/native-benchmark-resource-20261014-024146/`,
  `benchmark.json`, all six `benchmark-{0..5}.db` SQLite stores and their
  `benchmark-{0..5}-probe-metrics.json` receipts. Original evidence is
  unchanged; no new game or app process was started.
- Run `python -B tools/map_014_lead_native/audit_resource_freshness.py artifacts/map014-lead-live/native-benchmark-resource-20261014-024146`
  to emit independent JSON **to stdout only**; it neither overwrites receipts
  nor opens SQLite for writing. The script uses read-only **immutable SQLite**
  and rejects a nonempty WAL, checks original SQLite `record_key`,
  `point_index`, `updated_at`, real `data_json`, server/world/source
  identities, exact `pointId = y * 1000 + x + 1`, `x/y ∈ 0..999`,
  point-bearing AOIs, all 10,000 point AOIs in the metrics, run-ID-bound
  `prepared` native metrics, camera equality, selected Resource, 2,500
  completed blocks, zero failures/drops, count consistency, independently
  matched resource hydration and field correctness.
- `python -B -m unittest test_audit_resource_freshness -v` inside
  `tools/map_014_lead_native` runs nineteen **synthetic negative/positive unit
  fixtures**; those are not genuine scanner witnesses.
- The expanded auditor ties each native `data_json.updatedAt` / SQLite
  `updated_at` to the **independent original camera-before/after
  `capturedAt` UTC window**, native profile/session/process identity and
  server 2212. It refuses out-of-window/stale records, an altered source
  server, cross-run ownership changes and incompatible source-descriptor
  reuse. A point ID is coordinate-derived and **does not distinguish
  successive incarnations of a Resource at the same tile**.

### Six native engine/SQLite observations (server 2212, world 0)

| Round | Lens | Complete Resource rows | Occupied; detail not asserted | Unoccupied; hydrated | Resource-bearing 10×10 AOIs | Validated serial Lua probes | Fresh native Resource detail requests / ready | Full engine duration |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| 0 | baseline FOV120 | 7,989 | 500 | 7,489 | 5,553 | 260 | 7,489 / 7,489 | 151.326s |
| 1 | candidate FOV20 | 7,995 | 505 | 7,490 | 5,559 | 119 | 24 / 7,490 | 73.370s |
| 2 | baseline FOV120 | 8,002 | 507 | 7,495 | 5,563 | 260 | 20 / 7,495 | 149.747s |
| 3 | candidate FOV20 | 7,998 | 505 | 7,493 | 5,563 | 119 | 0 / 7,493 | 73.054s |
| 4 | baseline FOV120 | 7,993 | 500 | 7,493 | 5,560 | 260 | 1 / 7,493 | 149.601s |
| 5 | candidate FOV20 | 7,990 | 496 | 7,494 | 5,559 | 119 | 0 / 7,494 | 73.501s |

Each round had **10,000/10,000 point AOIs**, zero native dropped rows,
zero retries, one peak camera probe and an unchanged independent before/after
camera checkpoint. The AOI figures in the table distinguish **populated
Resource AOIs** from the **10,000 geometrically covered point AOIs**.
All 47,967 SQLite rows have consistent `pointId`, `pointIndex`, native
coordinates, server/world, `ResPointInfo` runtime/source labels and capture
timestamps. Every observed Resource has known occupancy and black-tile state;
every unoccupied Resource has native hydrated remaining/full values with
capacity/config consistency; occupied Resources intentionally carry no such
detail assertion. The source-comparable fields of the 7,970 / 7,998 / 7,990
shared identities have **zero mismatches** for position, point type, resource
config/type, level, max capacity, source/class, world/server and name key.
One exceptional resource type 15 is consistently present with the same source
metadata across all rounds.

**Performance claim boundary:** The per-round acquisition portions are about
145.626/144.286/143.973s (baseline) versus
67.786/67.442/67.917s (FOV20); the full-engine medians are **149.747s
versus 73.370s (51.00% shorter)**. This is not a shipping app speed claim.
Native Resource detail requests are 7,489 in the very first pass and only
24/20/0/1/0 later while `resourceDetailNativeReady` stays ~7,490:
the benchmark plainly includes an evolving source-detail cache, so the
different request counts must not be attributed to the lens or compared as
independent cold-cache trials. The **260 versus 119 validated AOI probe
responses** distinguish the acquisition strategy even in warmed-cache pairs;
`rawNetworkRequestCount` is null. Metrics' `acquisitionPolicy` still says
`conservative_aoi_step` for both because this field records step mode, not
the camera FOV. The actual FOV choice is the alternating explicit
`ExperimentalBoundedCameraLens` / `ForceLegacyFullWorldLens` source
in `tools/map_014_lead_native/Program.cs:125-139`, with request
`coverageFov=20/120` in
`CurrentClientMapBlockSource.FastCity.cs:830-835,849-863`. These compact
metrics alone do not include independently echoed per-request FOV.

## Exact identity changes and what source fields establish

| Pair (baseline→candidate) | Shared | Baseline-only IDs | Native occupied among baseline-only | Native unoccupied among baseline-only | Candidate-only IDs | Occupied among candidate-only | Shared occupied-state flips |
|---|---:|---:|---:|---:|---:|---:|---:|
| 0→1 | 7,970 | 19 | 3 | 16 | 25 | 1 | 7 false→true |
| 2→3 | 7,998 | 4 | **4** | 0 | 0 | 0 | 2 false→true |
| 4→5 | 7,990 | 3 | **3** | 0 | 0 | 0 | 1 true→false |

Each shared Resource was captured later in the candidate than baseline
(pairwise increases ~70–156 seconds), and no shared immutable-field drift was
seen. Source field `gatherOccupied` agreed with
`rebuildGatherOccupied` on **all** rows.

**Pair 1 missing: the 19 genuinely observed baseline-only point IDs are**
`374999, 431014, 440072, 448502, 462089, 536440, 539425,
674043, 676239, 780866, 780908, 822169, 822334, 845903,
850074, 852922, 909704, 913155, 926109`.
Of these, **16 were source-confirmed unoccupied and fully detailed** in
baseline 0; three (`431014, 536440, 539425`) were source-confirmed
occupied with unknown live amounts. All 19 were absent not only from the
candidate but also from **every subsequent baseline and candidate** (rounds
1–5). This is consistent with real turnover, but **does not prove the exact
moment/cause of their removal**: baseline-only records do not carry a
game-owned spawn/expiry/deletion timestamp. In particular, **do not claim
all 19 were explained by Resource occupation**.

**Pair 1 candidate-only: the 25 IDs are**
`20128, 422056, 482072, 488008, 496511, 530405, 542436, 556439,
666217, 680034, 704957, 740985, 758876, 805958, 821149, 828766,
835392, 848996, 874063, 915981, 919129, 953111, 965378,
968229, 982738`. Exactly **24 are unoccupied, fully hydrated
and persist across every later round 2–5**, including the next FOV120
baseline. One (`20128`) is observed *occupied* in rounds 1–4 and
disappears from round 5. Thus these are genuine native Resource identities
in the candidate, not malformed/duplicate IDs or a candidate-only fictitious
class; however, the snapshots alone do not establish whether each existed
when the earlier baseline AOI was refreshed. **Do not infer that baseline
necessarily missed them.**

**Pair 2 missing:** `95464, 260995, 843004, 971592` are all source-marked
occupied in baseline 2 and absent from later rounds 3–5.
**Pair 3 missing:** `1108, 20128, 32482` are all source-marked occupied
in baseline 4 and absent from round 5. Such losses are strongly
**occupation-associated**; with no native destruction event or synchronized
point presence snapshots, they are not **causally proven** exhaustion,
departure, expiry or scan miss. The observed net occupied-state counts also
change across scans (500→505→507→505→500→496).

The 10×10 coordinate->AOI membership of every Resource matches the
recovered `pointId` geometry. For pair 1, 11/19 missing IDs and 11/25
candidate-only IDs had **some other Resource in that AOI** in the opposite
snapshot; pair 2 had 4/4 and pair 3 had 2/3. An AOI containing other
Resource rows independently supports local sampling, but it is **not
proof that an individual absent point was available to be sampled**.

## New per-AOI temporal cohort bracketing and independent wall clocks

The new `audit_resource_freshness.py` output records, **for each changing
point ID and each of the six rounds**, whether that ID was observed, the
number of archived Resource rows in exactly the same 10×10 AOI,
their **minimum/maximum native point-capture milliseconds** (or `null` if
none), the independent full-scan camera-before/after wall-clock bound, and
the spatial point IDs whose source descriptors remain equal in the
baseline→FOV20→following-baseline sequence. This is a **cohort of
native-observed Resource rows**, not an event-history log. The importer
derives `updatedAt` from the native result's `capturedAt` timestamp at
`src/LWBridge.Desktop/FirstLiveResultImporter.cs:87-95,134-152,238-281`;
it does **not** contain the original Resource's server spawn/destruction
time or an authenticated lifetime UUID.

| Baseline→FOV20 pair | Absent baseline Resource IDs with a *different* same-AOI Resource captured by FOV20 | Same-AOI source-field matching anchors in baseline→FOV20→next baseline | Candidate-only Resource IDs with earlier baseline same-AOI source cohort | Matching anchors through next baseline |
|---|---:|---:|---:|---:|
| 0→1 | **11/19** (10 of 16 unoccupied, 1 of 3 occupied) | **11/19** | **11/25** (10 of 24 unoccupied; 1 occupied `20128`) | **11/25** |
| 2→3 | **4/4** (all previously occupied) | **4/4** | 0 | 0 |
| 4→5 | **2/3** (previously occupied) | No following FOV120 in this archive | 0 | 0 |

In those source-cohort cases, archived Resource rows in the same AOI carry
a single common capture-second value within each individual round. The
matching anchor ID is a **different Resource position** with unchanged
source fields across the cited snapshots; it is *not* the disappearing or
new Resource itself. Even a source row co-captured in the same AOI cannot
prove that an absent Resource existed at that instant. For the **other
8/19 missing** and **14/25 new** in pair 1, there is **no archived
Resource-bearing same-AOI capture in the opposite scan**. The scanner's
10,000 AOI geometry is still valid, but the only defensible temporal
boundary in these cases is the full scan's camera window.

**Exact source-timestamped examples (UTC, 13 October 2026):**

- Unoccupied, fully detailed `448502` was present in FOV120 round 0
  at **18:43:33**. Stable same-AOI Resource `443502` appeared in the
  FOV20 round-1 cohort at **18:45:31**, and later baseline round 2
  at **18:47:19**; `448502` is absent throughout rounds 1–5.
  This attests later captures of the area but **not the moment of
  `448502`'s removal**.
- Unoccupied, fully detailed `374999` was recorded at **18:43:26**
  in baseline round 0. Its own 10×10 AOI has **zero archived Resource
  rows** in each later round. There is no same-AOI point-source timestamp
  for it after baseline, even though point-AOI coverage completed.
- The **candidate-only occupied** `20128` has a different same-AOI
  source anchor `25127` captured at **18:42:30** in round 0, while
  `20128` itself first appears source-marked occupied at **18:45:01**
  in FOV20 round 1; the same point ID is observed occupied again in
  FOV120 round 2 **18:46:17**, FOV20 round 3 **18:48:47** and FOV120
  round 4 **18:50:03**. At **18:52:32**, round-5 FOV20 again captures
  `25127` in this same AOI, **without `20128`**. The observations
  bound its *recorded presence* across multiple full-world scans, but
  neither its actual first spawn nor subsequent destruction is proven.
  A matching coordinate `pointId` is not a guaranteed lifetime identifier.
- For later pair 2, the occupied `95464` is observed at **18:46:22**
  in baseline round 2 and absent from candidate round 3's source cohort
  containing different, stable same-AOI anchor `90462` at
  **18:48:53** (then again in baseline round 4). Again,
  **occupation-associated disappearance is not a proven death event**.

The scanner host's own before/after camera receipts supply additional
**independent wall-clock bounds**, which were missing from the earlier
point-identity-only audit:

| Round | Lens | Independent game-camera capture window (UTC, 13 Oct 2026) |
|---|---|---|
| 0 | FOV120 | 18:42:25–18:44:56 |
| 1 | FOV20 | 18:44:57–18:46:11 |
| 2 | FOV120 | 18:46:12–18:48:42 |
| 3 | FOV20 | 18:48:43–18:49:56 |
| 4 | FOV120 | 18:49:57–18:52:27 |
| 5 | FOV20 | 18:52:28–18:53:42 |

Every one of 47,967 SQLite point capture timestamps fits its **own**
round's independent camera wall-clock interval, and those six windows do
not overlap. That defeats a stale-row-from-previous-run explanation for
the **observed** records; it cannot prove a negative for an **absent**
Resource where no native point row is saved. The only additional raw
per-request `live-resource/bulk-aoi-diagnostic-result.json` preserved at
the root is **the final FOV20 round-5 request**, `requestMode=coverage`,
`coverageFov=20`, `capturedAt=2026-10-13T18:53:39Z`, with 40 native
requested AOIs and 36 `point_records`. **None of the 50 distinct changed
Resource point IDs from the three pairs is in its requested AOI
rectangle**. It cannot be repurposed into simultaneous source evidence
for an earlier missing/extra ID. The per-round compact metrics do not
retain individual AOI request IDs or native positive/negative object
snapshots sufficient for that causal attribution.

**No proven positive source-presence-versus-candidate-absence inverse is
present in these archives.** A positive same-AOI *other Resource*, exact
point count, second-resolution point observation, later source descriptor
agreement, or later reappearance are not equivalent to authoritative
simultaneous presence of a missing identity. The new auditor therefore
sets `nativePointLifecycleCausality: "unknown"` and
`simultaneousAuthoritativePresenceAndAbsenceProof: false` for every
changed ID and retains potential spatial-point reincarnation separately.
Controlled tests reject stale capture dates, wrong source/camera server,
changed native session, and incorrectly counting a re-used point ID with
different resource configuration as the same living object.

### Managed Resource source-accounting publication gate (controlled, 14 Oct)

The C# `ResourceCompletenessReport.RequireReconciledBeforePublication` previously
checked aggregate equality, deduplicated count and a boolean for each recorded
duplicate, but did **not** verify a staged Resource's own server/world, exact
point-derived identity, coordinate/AOI, native source fields or observation
timestamp. A forged row with the correct count and `srcServerId=2213` could
pass a report for server 2212. It also admitted invalid Resource candidates
whose diagnostic rejection was counted but whose native point was omitted.

The **diagnostic accounting publication gate** now checks the raw manager's
runtime-class and point-type enumeration histograms, excludes any positive
Resource candidate rejection other than `outside_selected_aoi`, verifies that
each retained row agrees with its report owner/server/world and 0–999 spatial
point identity, its native 10×10 AOI and logical block, and any imported raw
source kind, point ID, timestamp, occupancy and native detail amounts. Duplicate
occurrences must have corresponding same-identity comparisons with consistent
captured and stored times. This does not add an FOV20 selector or establish
individual-point source freshness beyond the scanner's own request window.

The *actual read-only round-5 final native per-request reply* at
`live-resource/bulk-aoi-diagnostic-result.json` demonstrates the expected
histogram semantics: 79 observed/79 enumerated `_pointInfos` entries, raw
runtime-class histogram 1+1+38+2+36+1 = 79, point-type histogram
1+2+1+1+38+36 = 79, 36 type-7 `ResPointInfo` Resource candidates,
36 accepted, **zero candidate rejects**, and 36 source lookup hits. Its
`rejectionReasonCounts` is the correct empty `[]` form; the parser permits it.
The native per-request rows carry `pointId`, `srcServerId=2212`, `worldId=0`,
`source`, `resourceSourceType` and `gatherOccupied`, which are normalized by
`FirstLiveResultImporter` with `kind=resource`, `sourceKind=resource_point`,
`pointIndex`, `recordKey`, `updatedAt` and rebuilt occupancy flags. The audited
47,967 rows across six databases have internally consistent source capture
timestamps and identity metadata, and only real unoccupied points assert
hydrated amounts. The **full C# per-batch histograms and managed report output
for every request were not separately archived**; these checks are therefore
source-schema compatible with the inspected native reply and managed importer,
not a retrospective C# execution proof for all six runs.

`tests/LWBridge.Desktop.Checks/Map014ResourceSourceCompletenessChecks.cs`
provides one positive and 17 **synthetic** publication inverses, including
foreign server/world, shifted point/record key, 1000-edge overflow, wrong AOI,
two stale timestamps, fabricated source, contradictory occupancy, impossible
remaining amount, dropped native candidate and omitted duplicate comparison.
The same tests preserve an accepted `outside_selected_aoi` source observation:
the manager may legitimately retain points in neighboring regions. An
initial controlled RED showed foreign `srcServerId` was admitted; GREEN passed
all 17 inverses with the corrected gate. This evidence does not promote
Resource FOV20 or reclassify any of the 19/4/3 disappearing point IDs as
proven stale, spawned or destroyed.

### Production Resource acquisition/source and hydration admission correction

The 14 October source review found two further **production** false-acceptance
paths in `CurrentClientMapBlockSource.FastCity.cs`, present on the guarded
FOV120 route as well as experimental FOV20. First, a native Resource batch
could claim an accepted Resource total and complete requested-AOI geometry
while its `acceptedAoiCounts` histogram disagreed with the actual imported
Resource rows. The managed receiver checked only the total and overall
footprint, then staged the rows. It now requires the exact per-response
accepted-AOI histogram to agree with the imported coordinate-derived AOIs,
verifies the individual current server/world/source identities and capture
timestamp, prevents duplicate source point IDs within a reply, and rejects
native producer candidate exclusions other than normal
`outside_selected_aoi` observations. Because `_pointInfos` can contain points
outside the requested footprint, that specific exclusion remains legitimate.

Second, an enrichment timeout, invalid source response, partial native result
or absent currently idle key was previously downgraded to an error string and
all Resource rows were still published as a completed scan, with missing
Resource detail silently presented as unknown. On the actual native contract,
`resource_scan_detail_runtime.queue` enqueues known idle points and its
result provides `recordKey`, `received`, `resourceConfigId`,
`resourceNameKey`, `resourceMaxAmount` and actual remaining/full amounts.
The receiver now fails the publication attempt when **any current known idle
Resource** has no received detail, the detail report is partial/failed, its
ready counter disagrees with received rows, or source config/name/capacity
differs from the retained point. Already occupied points retain unknown
amounts, because no live idle amount is justified for them. Older queued
identities outside the current final retained set are not used to claim
current presence. The historical six-run SQLite receipts each have exactly
7,489/7,490/7,495/7,493/7,493/7,494 hydrated **unoccupied** points and
zero occupied amount assertions, structurally consistent with the new gate.

The real Lua `coverage` protocol clears native
`WorldPointManager.isRecvViewPoints` and
`world.hasReceiveViewPointsReply`, then records their true transition and
the `_curViewIndex`/`requestedIndices` snapshot before source records are
serialized. `RequireFreshCaptureTime` admits only a correlated current probe
captured between the request start minus two seconds and the observed host
time plus five seconds; native `capturedAt` has second precision. **The six
archives do not contain an independent unique request/reply ID or original
server-created point lifespan timestamps per AOI**. Therefore complete
10,000-AOI union and even per-response matching source histograms do not
prove contemporaneous absence of any of the 19/4/3 disappearing Resources
or creation timing of the 25 added points (including occupied `20128`).

`Map014ResourceNativeSourceChecks.Run()` exercises 14 synthetic native
source/AOI inverses and nine synthetic partial/stale hydration inverses,
alongside positive 0/999 map seams and the legitimate occupied/no-detail
case. The full desktop checks suite passed with
`MAP014_RESOURCE_NATIVE_SOURCE_CHECKS_OK nativeInverses=14 hydrationInverses=9
native=false` and `HOME_FEATURE_DELIVERY_CHECKS_OK` using the existing
`SkipCanonicalProductionUiBuild=true` test flag. These controlled checks
provide no new live Resource capture. Resource FOV20 remains disabled by
default and source freshness qualification remains **partial**.

### Additional native Resource hydration receipt provenance admission

An independent continuation at clean `d31f564f` found that the managed
Resource detail parser reconciled the *declared* `requestCount`,
`cacheBeforeCount`, `sendFailureCount`, `targetCount` and `readyCount` but did
not compare them to the corresponding **per-target** Lua fields
`requestIssued`, `cacheBefore`, `sendFailed` and `received`. This admitted a
synthetic reply in which a known idle Resource was listed as a cache hit while
the same reply claimed it had issued a network request. Lua's
`resource_scan_detail_runtime.queue` records exactly one of those three
target origins; `resource_scan_detail_runtime.rows` emits those same flags
along with the actual manager detail and reception boolean. The managed
reader now requires each flag to be a native boolean, exactly one origin per
target, sum-by-origin agreement with the reply header, exact ready/received
agreement, no asserted detail for an unreceived target, and no claimed
success from a failed send. A contradictory target aborts publication before
known-idle hydration, including mixed selections.

The **preserved native final round-5**
`live-resource/resource-scan-detail-result.json` has 7,494 targets, 7,494
ready, 0 network requests, 7,494 pre-existing cache entries and 0 send
failures. Every target carries
`requestIssued=false,cacheBefore=true,sendFailed=false,received=true`,
matching the new rule. The other five rounds have original native
SQLite/scan metrics but **do not retain these per-target detail-result
files**, so their detail provenance cannot be re-proven from the preserved
evidence. In isolated synthetic Resource checks, two positive reply shapes
(native cache hit and issued request) and 10 inverses cover forged counts,
absent/double origins, malformed flags, failed sends and contradictory
received/amount state. Controlled RED with the gate temporarily bypassed
failed on the false request-vs-cache header; the restored full suite reported
`MAP014_RESOURCE_NATIVE_SOURCE_CHECKS_OK nativeInverses=14 hydrationInverses=9 provenanceInverses=10 native=false`.

This correction verifies self-consistency of producer-described detail
origins, **not** the server response age for a cached detail, Resource
spawn/destruction times, or authoritative same-AOI native presence-versus-
absence. The 19/4/3 missing and 25 first-pair new Resource identities retain
their earlier classifications; FOV20 remains diagnostic only.

### Native Resource point-type histogram admission (b2847873 follow-up)

A further receiver-side false acceptance was found in the **actual native
producer's source-accounting contract**. In
`resource_aoi_records`, each enumerated `_pointInfos` entry increments the
`rawPointTypeCounts` histogram first; that *same* `pointType` places an entry
in `resourceCandidateCount` if and only if its integer type is **1, 7 or 26**.
The prior managed gate verified the sum of histogram counts and the candidate
sum independently, but did not require the Resource candidate count to equal
the raw histogram's combined 1/7/26 counts. It could therefore accept two
ostensibly valid Resource records when the raw native histogram reported
**only two type-6 City entries**. This would be impossible from the producer's
single `_pointInfos` enumeration, even though all aggregate counters balanced.

`RequireResourceNativeSourceCohort` now fails closed unless the sum of the raw
type-1/7/26 histogram matches `ResourceCandidateCount`. It also ensures the
number of **accepted** records of each of these three types does not exceed
the count of that type in the native raw histogram (accepted points are a
subset of the original enumeration, not a separate inventory). The positive
synthetic contract includes legitimate types 1 and 26; **the fix does not
assume only type 7 is valid**. Four new inverse inputs cover the all-City
histogram, an undercounted Resource histogram, and accepted type-1/type-26
rows absent from the claimed raw histogram.

The actual preserved final round-5 native AOI reply records **79 observed,
79 enumerated**, with 36 Resource candidate/accepted entries and a raw
type-7 count of 36. Its Resource lookup reports 36 hits and zero fallback.
The other five scans retain their SQLite Resource rows and 10,000-AOI
coverage, **not a complete original native histogram for every probe**.
The independent six-round read-only audit remains unchanged: 19/4/3
baseline-only identities, 25 first-pair candidate-only identities (including
occupied 20128), no demonstrated same-time lifecycle or missing-point
causality and **Resource FOV20 unqualified**. A separate optional lookup
fallback is reported by Lua but is *not* equivalent to proof of source
staleness, and this follow-up does not reject fallback merely on suspicion.

**Validation limitation:** The new four inverses are added to
`Map014ResourceNativeSourceChecks.Run()` (18 total source inverses), but their
RED/GREEN execution is **not newly established**. The attempted pre-fix
combined run stopped earlier at an unrelated Auto ownership check, and a
post-fix run stopped earlier at an unrelated shared-native group-admission
check. The prime subsequently reported an OpenAI denial of another offline
.NET test invocation; no further build/test variant, bypass or reroute was
attempted. Code and tests need a normally authorized isolated execution
before claiming a verified RED→GREEN for this particular correction. No live
game/native test, Lua change or FOV20 promotion occurred.

### First native AOI census versus Resource source enumeration (999370b2 continuation)

The current Lua `coverage` producer takes **two independent, sequential**
`WorldPointManager._pointInfos` enumerations for a Resource selection.
`point_aoi_counts` first filters the native `requestedIndices` AOI footprint,
records all matched point types in `nativePointTypeHistogram`, and counts
types **1/7/26** as Resources (`tools/current_live_resource_probe.lua:2604-2635`).
`resource_aoi_records` then re-enumerates the manager against the same AOI
lookup, accepts Resource identities, records their per-AOI counts, and returns
`point_records` (`:2846-2965`). The producer **overwrites**
`details.matchedResourceCount` with the second enumeration's record count at
`:6294-6303`. Previously, the managed receiver validated the second
enumeration against its own histogram and records but never compared these
accepted Resource rows with the *first* native AOI type histogram. A changed
source cohort between the two passes (or a contradictory received result)
could be reported as `state=proven` with internally consistent second-pass
counts, silently treating an inconsistent AOI acquisition as authoritative.

`CurrentClientMapBlockSource.FastCity.cs` now requires
`nativePointTypeHistogram` on each Resource batch and checks the **exact
per-type first-pass counts for 1, 7 and 26** against the second-pass imported
accepted Resource rows. It accepts the genuine empty Lua `[]` histogram only
for zero Resource rows, validates all histogram counts as nonnegative
integers, and rejects repeated/malformed source keys. Inconsistent batches
fail before staging, including when Resource is part of a mixed selection.
Nine new isolated **synthetic inverse controls** cover reduced/increased
first-pass Resource counts, a non-Resource cohort, count-preserving Resource
type substitution, null/noninteger/negative counts, duplicate keys and a
nonempty array. Positive controls preserve native type 7, mixed 1/26, and an
empty Resource AOI.

The archived native final round-5 per-AOI reply is independently consistent:
its `nativePointTypeHistogram` contains **36 type-7 Resource entries**, and
the later `resourceCompleteness` reports **36 accepted type-7 Resources**;
the reply has 40 requested AOIs. The six saved scan-metrics receipts show
**10,000 unique AOIs** per round and Resource row counts
7,989/7,995/8,002/7,998/7,993/7,990. The archived 19/4/3 baseline-only
and 25 first-pair candidate-only identities are unchanged. There is no
original first/second-pass per-identity native ledger for all 119 probes of
each round, so **matching type counts cannot prove the same Resource IDs or
point absence**, nor rule out changes that preserve type counts. This is a
bounded **inconsistent-receipt admission fix**, not a demonstrated live
data race or Resource accuracy proof. Resource FOV20 remains **OFF**.

**Verification boundary:** The existing .NET test operation was previously
denied, and the new synthetic C# inverses have **not been executed** in this
continuation; no alternative .NET/Git/npm/recovery action was attempted.
Static contract review plus the preserved native round-5 reply support this
correction, but no new RED→GREEN or packaged/live qualification is claimed.

### Same-enumeration Resource runtime-class source membership (source-only continuation)

Beyond the **nine separate first-versus-second AOI type-histogram inverses**
above, a further second-enumeration acceptance mismatch was identified. The
actual Lua `resource_aoi_records` producer increments
`runtimeClassCounts[reflected_type_name(info)]` for **every** enumerated native
`_pointInfos` entry at `tools/current_live_resource_probe.lua:2874-2878`, and
then uses precisely the same `reflected_type_name(info)` as the
`runtimeClass` field of each accepted `resource_point` row at `:2947-2950`.
The managed source gate previously checked only that this row's runtime class
was nonblank and that the total native histogram size reconciled. Therefore
it could publish two accepted `ResPointInfo` Resource rows with a claimed
same-pass histogram of **two `BuildPointInfo` entries and zero `ResPointInfo`**,
despite valid overall candidate/type/AOI counts. Such contradictory class
provenance is impossible from the native producer's own single enumeration.

`RequireResourceNativeSourceCohort` now additionally checks that the number
of accepted rows for **each exact runtime-class string** does not exceed the
number of raw native entries of that class. A heterogeneous synthetic positive
permits different reflected class strings when their histogram counts reconcile;
the optional `GetResourcePointInfoByIndex` lookup's `resourceSourceType` is
**not** conflated with `runtimeClass` because it can come from a different
object. Four additional **unexecuted** synthetic inverse cases cover a
histogram containing only foreign classes, an undercounted accepted class,
an unrepresented row runtime class, and a case-only source identity mismatch.
The isolated native-source inverse expectation changes from **18 to 22**;
the preceding first-snapshot inverse count stays **nine**.

The saved final round-5 native reply shows 36 `ResPointInfo` entries in
`runtimeClassCounts` and 36 accepted Resource rows with
`runtimeClass=ResPointInfo`, consistent with the check. The six-round
19/4/3 missing and 25 candidate-only identities remain **unclassified**:
matching class counts cannot establish shared identity, exact native
lifetimes or contemporaneous absence. No .NET runner, blocked Git inspection,
npm, recovery, game/native action or commit was invoked; source review and
static test additions are **not** a RED→GREEN result. Resource FOV20 remains
**OFF**.

## Production path and recommended exact native inverse

- FOV20 remains **diagnostic opt-in for Resource**:
  `CurrentClientMapBlockSource.FastCity.cs:824-835` selects bounded full
  world only when `ExperimentalBoundedCameraLens` is true, or when **City
  alone** is selected. The baseline `ForceLegacyFullWorldLens` override
  blocks bounded selection. `tests/LWBridge.Desktop.Checks/Map014BoundedLensChecks.cs:51-64`
  asserts Resource, Truck, Other and mixed are not silently opted in.
- The planner validates current requested AOI geometry and 10,000 exact AOIs
  (`CurrentClientMapBlockSource.FastCity.cs:330-386,496-511,754-766`);
  physical AOI coverage is **not** a same-time native Resource population
  check. Resource detail targets are hydrated only for known idle points,
  with explicit request/cache/ready counter validation
  (`CurrentClientMapBlockSource.FastCity.cs:555-590,1388-1442`).

For a discriminating **new qualified experiment**, under the prime's *single*
safe owned session only, select a currently source-confirmed **unoccupied**
Resource identity/config and its exact 10×10 AOI. Make a short alternating
**fresh FOV120 → fresh FOV20 → fresh FOV120** request trio for the identical
AOI, using unique native request IDs, response timestamps, same
profile/session/server/world, AOI rectangle provenance and independently
verified camera restoration on *every* request. Read the native
`WorldPointManager._pointInfos` snapshot near each response, correlate the
point ID/coordinates, compare `ResPointInfo`, `GatherResource` and
availability. A bounded miss **while the point is contemporaneously observed
in the source on both sides** is actionable evidence of candidate omission.
If it disappears independently from later source snapshots, the negative
supports a native lifetime transition. If no suitable current point or
unambiguous source capture is available, record **unavailable** rather than
asserting accuracy. These historical IDs are **not** a valid assumption
about currently spawned points.

**Gate remains open:** exact positive same-session Resource freshness
qualification plus Stop, failure/rollback, retirement and packaged UI
regression for an eventual Resource-only FOV20 production selector. Do not
change production selection based on timing or unlabeled point count parity.
