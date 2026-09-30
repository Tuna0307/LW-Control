# LWB317-MAP-RESOURCE-COMPLETENESS-PREP-001 — offline Resource completeness diagnostics

Date: 2026-09-30

## Result

Task state: `IMPLEMENTED_NOT_VALIDATED`.

This task was performed strictly offline. It did not launch Last War,
LastWarLauncher, LWBridge, a live Map session, or any live scan, and it did not
use network-dependent research.

The current-client Resource path is now instrumented so the next separately
authorized v22 live scan can explain the Resource total without changing which
Resource candidates are accepted. The instrumentation records raw
`WorldPointManager._pointInfos` occurrence counts, exact current-adapter candidate
rejection reasons, typed-resource lookup fallback, duplicate record-key collapse,
accepted-row distributions and spatial coverage, and preserves every final
accepted Resource row. A future test-only live harness also pages and saves every
published Map317 Resource row before clear so the source-side and published totals
can be independently reconciled.

The new diagnostics are not promoted to live proof here. Whether the previous
678 rows were the complete Resource population exposed by the current-client
acquisition path remains `UNKNOWN` until another live scan consumes this
instrumentation. Whether that current-client population equals every Resource in
the game universe, or equals the protected original LWBridge traversal, also
remains `UNKNOWN`.

Starting repository state was clean on `research/offline-controller`, with local
HEAD and `origin/research/offline-controller` both at:

`d75127eb916bb8f1648ec610ebbc07de59fd93d6`

## Plain-language answers

### 1. What exactly did the previous `678` mean?

The previous 001 run completed all 2,500 logical Resource scan blocks and then
queried `map_search(kind=resource)` without a Resource-name filter. The reported
total `678` matched the Map summary/options count. Therefore **678 means 678
unique published Map317 Resource rows for that current-v22 scan/server after the
current-client adapter's acceptance and record-key deduplication**.

It does **not** mean:

- 678 raw `_pointInfos` objects;
- 678 raw Resource sightings before overlapping AOI responses were merged;
- 678 idle-only resources;
- 678 full-only resources;
- 678 non-black resources;
- or an independently proven count of every Resource object present anywhere in
  the game state.

`tests/LWBridge.Desktop.Checks/LiveMapV22Proof.cs` collected the unfiltered total
from the first Resource search page and only retained representative/query slices.
An offline recount of the untouched 001 raw evidence finds 29 Resource-row
occurrences across those saved slices but only 23 distinct record keys, while the
published total was 678. Thus 655 of the 678 distinct published row bodies were
not preserved in 001 evidence.

### 2. Was there a level filter?

No acquisition-time level filter exists in the current-client Resource path.
Level is read from `collectResourceInfo` or `GetResLevel`/field fallback and may
remain missing; the row is still accepted.

For the recovered current 0.3.17 public query contract, Resource level filtering
is not a Resource-owned frontend filter. `resourceNameKey` is the Resource-specific
filter; `minLevel`/`maxLevel` are recovered for Dispatch, not Resource
(`MapDataQueryContract.cs:300-333` and
`docs/reviews/2026-09-24-r8-014-map-search-filter-parity.md:23-36`).

Classification: public filter ownership is `EXACT_CONTRACT`; the current-client
acquisition implementation is adapter behavior, not a claim about protected
original traversal.

### 3. Was there a black-tile filter?

No. `resource_source_metadata` attempts `LuaEntry.Player.IsInBlackRange` and
records known true, known false, or unknown. `resource_aoi_records` does not use
that value as an inclusion gate. A black Resource, non-black Resource, or Resource
whose black status cannot be determined is emitted if it passes the identity,
tile, AOI and server gates.

The earlier rebuild-only public `excludeBlackTile` behavior was explicitly
removed from strict public parity; it is not a current original Resource query
feature (`r8-014` lines 23-36).

### 4. Was there an occupied/unoccupied filter?

No. The probe reads `gatherMarchUuid` and `gatherUid`; occupancy is known only
when both recovered fields are readable. Known occupied, known unoccupied, and
unknown occupancy Resources are all accepted.

Occupancy only controls **detail enrichment**: the current scan requests
remaining/full amount detail for known-idle Resources. It does not control whether
the Resource row enters the scan population
(`CurrentClientMapBlockSource.FastCity.cs:511-530`).

### 5. Was there a full-resource-only filter?

No. Fullness is not known during primary candidate admission. Authoritative
remaining/full amounts are fetched later through the Resource detail path for
known-idle Resources. If detail is unavailable, times out, or is partial, the
Resource row remains present. `resourceFull=true` is only enrichment when an
authoritative detail says remaining amount equals full amount.

### 6. Which filters were acquisition-time versus query-time only?

The current-client **acquisition-time candidate gates** are only:

1. raw point type is `1`, `7`, or `26`;
2. a positive point identity can be read;
3. point-index-to-tile conversion succeeds;
4. the tile falls inside the current native AOI grid;
5. that AOI belongs to the selected native response footprint;
6. a positive server ID exists, using the point's server or current-server
   fallback.

After the Lua row reaches C#, normalization requires a usable positive public
server identity, positive point ID, and non-negative coordinates. The fast-batch
validator also requires the normalized row's server to match the active request
and its coordinate-derived AOI to belong to the response footprint. Such C#
validation failures fail the batch rather than silently removing just one row.

The following are **not acquisition filters**:

- level;
- Resource type ID;
- Resource name/config lookup;
- black-tile state;
- occupied/unoccupied state;
- remaining/full amount;
- full versus partial versus empty detail;
- source server;
- Resource detail lookup success.

`worldId` is also **not a per-Resource admission gate** in this current adapter.
The Lua row preserves a point `worldId` when present (otherwise `0`), and the new
accepted-row diagnostic reports its distribution. The C# scan engine validates the
batch/capture world against the active request, but `resource_aoi_records` does not
drop an otherwise accepted Resource because its row-level `worldId` differs. This
distinction is intentional in the diagnostic: a surprising row-level world value
must be visible as evidence, not silently converted into a new exclusion rule.

The recovered public Resource-specific **query-time** filter is
`resourceNameKey`. Generic keyword searching is also query-time. Normal UI paging
and sorting likewise operate only after records are published. The exact query
predicate for Resource name is in `MapStore.Query.cs:337`; recovered Resource
frontend ownership is locked in `MapDataQueryContract.cs:305`.

### 7. Which point types are considered Resource candidates?

The **current-client adapter** treats raw `pointType` values exactly `1`, `7`, and
`26` as Resource candidates (`tools/current_live_resource_probe.lua` around
`resource_aoi_records`, currently lines 2303-2419). All other raw point types are
counted as non-Resource observations by the new diagnostic and are not emitted as
Resource rows.

This is deliberately not described as the original LWBridge classifier. Separate
recovered native evidence proves that original/native Resource getter population
uses `GetResType` / `GetResLevel` under a `pointType == 7` branch, but that does
not prove that the protected original scanner's complete Resource admission rule
was exactly `{1,7,26}`. Exact original game-side Resource classification and
traversal remain `UNKNOWN`.

### 8. What information was missing from 001 evidence?

001 proved a complete 2,500/2,500 logical scan and 678 published Resource rows,
but it did not preserve enough raw/source evidence to explain why the final
population was 678. It lacked:

- the total `_pointInfos` objects observed per native response;
- runtime-class distribution;
- raw `pointType` distribution;
- Resource candidate occurrence count before admission gates;
- rejection/skip counts by concrete reason;
- typed `GetResourcePointInfoByIndex` hit versus raw-point fallback count;
- accepted Resource occurrences before record-key deduplication;
- duplicate record-key occurrence count;
- full accepted population split by level/name/type/raw point type;
- full black true/false/unknown distribution;
- full occupancy occupied/unoccupied/unknown distribution;
- full/partial/empty/detail-unknown distribution;
- source-server/world distribution;
- Resource count per native AOI and logical block;
- populated versus empty AOI count and coordinate bounding box;
- all 678 final accepted row bodies;
- a source-side accepted count reconciled directly to staging/publication/search.

The 001 scan's 2,500 logical-block completion is geometric progress. The current
fast full-world adapter independently requires its union of native 10×10 AOIs to
reach all 10,000 cells (`CurrentClientMapBlockSource.FastCity.cs:478`). Neither
fact by itself proves that `_pointInfos` materialized every Resource entity.

### 9. What will the new diagnostic capture on the next live run?

The new probe-side `resourceCompleteness` envelope records, for every successful
native response:

- observed and enumerated `_pointInfos` occurrence counts;
- runtime-class counts;
- raw point-type counts;
- Resource candidate occurrences;
- accepted occurrences;
- rejected candidate occurrences;
- non-Resource occurrences;
- exact candidate rejection reasons;
- accepted counts by native AOI;
- typed Resource-source lookup hits;
- raw-source fallback occurrences.

Its equations are checked before the batch is trusted:

`observed = resource candidates + non-resource`

`resource candidates = accepted + rejected`

`rejected = sum(rejection reasons)`

`accepted = sum(accepted AOI counts)`

`accepted = typed lookup hits + raw-source fallbacks`

At the C# full-world layer, the diagnostic additionally counts duplicate accepted
record-key occurrences before latest-wins merging. The final source report then
captures every unique accepted row plus distributions for:

- level;
- Resource name key;
- Resource type ID;
- accepted raw point type;
- black true / false / unknown;
- occupancy occupied / unoccupied / unknown;
- detail full / partial / empty / unknown;
- server, source server, and world ID;
- native AOI and logical block.

Spatial evidence includes:

- populated native AOI count;
- zero-Resource native AOI count;
- Resource count per native AOI;
- min/max count among populated AOIs;
- count per 20-tile logical block;
- coordinate bounding box.

The future test-only live consumer is:

`tests/LWBridge.Desktop.Checks/LiveMapV22ResourceCompletenessProof.cs`

After a clean completed Resource scan it writes a lossless source-side report and
pages `map_search` at page size 200 until it has **every** published Resource row.
The published rows are saved as a second machine-readable sidecar, sorted
canonically for stable hashing. The main proof records sidecar paths, SHA-256
hashes, row counts, summary/options, and the compact reconciliation metrics.

This new machinery is `IMPLEMENTED_NOT_VALIDATED`: deterministic/offline tests
exercise the accounting and serialization, but this task did not invoke its live
mode.

### 10. What remains impossible to know until another live scan?

The following remain `UNKNOWN` now:

- whether v22 live `_pointInfos` produces 678 accepted unique Resources again;
- raw v22 `_pointInfos` cardinality and raw point-type/runtime-class distributions
  during full-world acquisition;
- how many Resource candidate occurrences are rejected by each gate;
- how many accepted sightings collapse under duplicate record keys;
- how the 678 population is distributed across all 10,000 native AOIs;
- whether Resources that are retained elsewhere by the game are absent from
  `_pointInfos` when the adapter inspects each current-view response;
- whether Resource-like live objects exist under a point type outside the current
  `{1,7,26}` adapter classifier;
- whether two semantically distinct live Resource entities can reuse the same
  point ID and therefore collapse under the current record identity;
- whether the protected original LWBridge traversal/classifier would enumerate
  exactly the same population.

Even after a perfectly reconciled future run, `gameUniverseComplete` remains
`UNKNOWN` unless an independent authoritative source enumerates Resources that
never materialize in `_pointInfos`. The next run can prove completeness relative
to the **current-client acquisition path it actually observes**, not invisible
game state by assumption.

## Actual current-client Resource pipeline

### Geometry and source

For a 1000×1000 full-world scan with all 2,500 logical blocks pending,
`CurrentClientMapBlockSource.CaptureBatchAsync` takes the fast full-world path.
`CanUseFastCityBatch` is a provider-policy check; scan mode itself is not the
Resource admission rule (`CurrentClientMapBlockSource.FastCity.cs:36-81` and
`:711+`).

The full-world loop advances from measured native current-view footprints and
requires the union to reach exactly 10,000 10×10 AOI cells. Records are taken
from each successful native response rather than inferred from logical block
completion.

Resource source collection:

`WorldPointManager._pointInfos`

This boundary matters. The same Lua file documents that current-client City
responses can omit retained City rows from `_pointInfos`, so City deliberately
uses `WorldPointManager.GetAllMainBaseList()` as a retained unique-main-base
source. Resource currently has no analogous retained Resource-list fallback.

That asymmetry is a source-backed **hypothesis** for why a geometrically complete
scan might have a smaller Resource population, but it is not proof that v22
actually omitted Resources.

### Candidate-to-record steps

For each `_pointInfos` object:

1. record runtime class and raw point type for diagnostics;
2. non-`1/7/26` point types are not Resource candidates;
3. resolve positive point ID;
4. convert point index to tile;
5. calculate native AOI and require it inside the runtime grid;
6. require that AOI to be part of the selected native response footprint;
7. resolve positive server ID from the row or current server;
8. optionally load typed `ResPointInfo` by index; failure uses raw `PointInfo`;
9. enrich occupancy, level, type, config/name/capacity and black status when
   available;
10. emit the Resource row regardless of whether those enrichment fields are
    known;
11. for known-idle rows only, queue Resource amount detail independently;
12. C# normalizes the emitted row and maps point ID to `recordKey`;
13. overlapping accepted sightings merge by `recordKey`, with the latest
    `UpdatedAt` row retained;
14. final unique rows are assigned to logical blocks by coordinate, staged into
    the Map317 run, and published on successful completion.

`GetResourcePointInfoByIndex` failure is therefore **not** a Resource rejection;
the new diagnostic counts it as `resourceSourceLookupFallbackCount`. Resource
detail unavailable is likewise **not** a candidate rejection; it produces detail
state `unknown` while retaining the row.

## Acquisition-time rejection accounting

Every intentional per-candidate rejection currently present in
`resource_aoi_records` has a concrete measurable diagnostic reason:

| Current adapter condition | Diagnostic reason | Effect |
|---|---|---|
| missing/nonpositive point identity | `invalid_or_missing_point_id` | candidate skipped |
| point-index-to-tile conversion returns no tile | `tile_conversion_failed` | candidate skipped |
| tile resolves outside runtime AOI grid | `outside_aoi_grid` | candidate skipped |
| valid AOI is not in this response's selected native footprint | `outside_selected_aoi` | candidate skipped |
| row/current-server fallback yields no positive server | `invalid_or_missing_server_id` | candidate skipped |

Unsupported/non-Resource point types are counted separately as
`nonResourcePointCount` and in the raw point-type distribution; they are not
misrepresented as rejected Resource candidates.

The following abort the response/run path rather than silently rejecting one
candidate and are therefore not per-candidate rejection buckets:

- `_pointInfos` unavailable;
- invalid collection count;
- collection enumeration mismatch;
- invalid/mismatched bulk AOI geometry;
- source snapshot count mismatch during C# normalization;
- normalized row server mismatch;
- normalized row outside the native response AOI footprint.

## Dedupe and publication

`FirstLiveResultImporter.PrepareResourceSnapshot` maps the accepted point ID to
the public `recordKey` (`FirstLiveResultImporter.cs:139,238+`). During full-world
acquisition, `resourceRecords` is keyed by record key and retains the latest
accepted occurrence (`CurrentClientMapBlockSource.FastCity.cs:426-436`). Map317
staging/published storage has another uniqueness boundary based on run/kind/server/
record key and then kind/server/record key.

The new report therefore distinguishes:

- accepted Resource **occurrences before dedupe**;
- duplicate accepted record-key occurrences;
- final accepted **unique records**.

The offline accumulator requires:

`accepted occurrences - duplicate record-key occurrences = final unique records`

before it describes the source-side population as reconciled.

## Recovered original contract versus adapter policy

### `EXACT_CONTRACT`

Current 0.3.17 evidence establishes the host-visible Map contracts used here:

- public Resource kind;
- 20-tile logical block cardinality/state accounting;
- scan staging/publication/clear families;
- normalized record identity/storage/query semantics;
- Resource-specific public `resourceNameKey` filtering;
- protected provider entry points rather than a recovered public game traversal.

Separate historical/native evidence also establishes the point serializer and
the `pointType == 7` gate used when native capture populates `GetResType` and
`GetResLevel` fields.

### `IMPLEMENTED_NOT_VALIDATED`

The following are current-client implementation/diagnostic policy, not promoted
to original contract parity:

- `_pointInfos` as the Resource discovery collection;
- `{1,7,26}` as the current adapter Resource candidate set;
- measured 10,000-cell AOI union strategy;
- latest-wins duplicate merge in the current adapter;
- the new raw/rejection/distribution accounting;
- full source/published row sidecars and reconciliation verdict.

### `UNKNOWN`

Protected original LWBridge game-side traversal order, pacing, native
acknowledgement/drain behavior, and exact Resource admission classifier remain
unknown. The current adapter must not be described as byte-for-byte equivalent to
the original scanner.

## Suspicious conditions worth measuring, not changing

No deterministic bug was found that justifies changing Resource inclusion during
this task. The strongest source-backed hypotheses for a low total are therefore
left unchanged and made observable:

1. **Retained-row visibility.** City is known to need a retained-list source
   because `_pointInfos` can omit retained City rows. Resource has no equivalent
   fallback. Whether v22 omits retained Resources is `UNKNOWN`.
2. **Point-type classifier coverage.** The current adapter only classifies
   `1/7/26`. The raw point-type/runtime-class distribution will show what else is
   present, but whether another type semantically means Resource is `UNKNOWN`
   without stronger source evidence.
3. **Identity collapse.** Repeated accepted sightings with the same point ID are
   intentionally deduplicated. The next run will quantify those duplicates. It is
   `UNKNOWN` whether distinct live Resource entities could share that identity.

Black tile, occupation state, fullness, level, Resource name/type enrichment, and
typed Resource lookup failure do **not** explain the count through filtering in
the current implementation because none is an inclusion gate.

## Deterministic coverage

`ResourceCompletenessDiagnosticsChecks` uses synthetic local rows and validates:

- raw counters reconcile;
- candidate rejection totals equal named rejection reasons;
- an unattributed rejection fails the diagnostic instead of disappearing;
- accepted AOI totals reconcile with accepted occurrences;
- typed Resource lookup hit/fallback counts reconcile;
- duplicate accepted record keys reconcile against final unique rows;
- level distribution including unknown;
- Resource name/type distribution;
- black true/false/unknown separation;
- occupancy occupied/unoccupied/unknown separation;
- detail full/partial/empty/unknown separation;
- sparse native AOI counts, populated/zero AOI totals and min/max density;
- coordinate bounding box;
- all accepted rows are preserved;
- serialized report output is stable and round-trips.

Existing fast-Resource synthetic response fixtures now emit the same reconciled
`resourceCompleteness` envelope required from the next real probe result. The
validator was kept strict rather than accepting missing diagnostics.

The repository's existing Lua bootstrap check initially caught an offline
implementation error when two new top-level Lua helper functions raised the probe
to 201 top-level locals, beyond Lua 5.3's 200-local chunk limit. The helpers were
moved inside `resource_aoi_records`; the final approach preserves identical
diagnostic behavior while keeping the probe loadable.

## Prepared next live consumer — not executed here

The exact future command prepared by this task is:

```text
tests\LWBridge.Desktop.Checks\bin\Release\net10.0-windows10.0.17763.0\LWBridge.Desktop.Checks.exe --live-map-v22-resource-completeness-proof evidence\lwbridge-0.3.17\map\LWB317-LIVE-MAP-V22-RESOURCE-COMPLETENESS-001\live-proof.json
```

It belongs to the future task:

`LWB317-LIVE-MAP-V22-RESOURCE-COMPLETENESS-001`

This offline prep task did **not** run that command and does not authorize it.

When separately authorized, that consumer will write:

- the main proof JSON;
- `live-proof.resource-source-report.json` containing the full source-side
  completeness report and all final accepted rows;
- `live-proof.resource-published-rows.json` containing every published Map317
  Resource row collected without normal UI pagination limits.

The main proof will retain sidecar counts and hashes and will state separately:

- whether current-client-path accounting reconciled;
- `gameUniverseComplete = UNKNOWN`;
- `originalLwbridgeTraversalEquivalent = UNKNOWN`.

That separation prevents a future successful accounting run from being
overstated as proof about entities the current source never exposed.

## Scope boundary

No live Last War process, launcher, LWBridge session, Map scan, server jump,
restart/resume, auth/login/licensing work, unrelated subsystem, or gameplay action
was performed in this task. Resource inclusion semantics were not changed; only
observability and future evidence preservation were added.
