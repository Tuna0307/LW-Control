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
  `tools/map_014_lead_native` runs eleven **synthetic negative/positive unit
  fixtures**; those are not genuine scanner witnesses.

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
