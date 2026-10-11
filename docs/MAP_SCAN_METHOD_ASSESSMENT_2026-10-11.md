# Current Map scanner — origin and efficiency assessment

2026-10-11 lead source inspection at checkpoint `dd2a0ff9`, followed by MAP-006
worker reassessment after `c6955ebd`. The original comparative baselines below
are **saved prior genuine-client pilot receipts**. A fresh controlled **two-server
Truck-only passive Auto** run was subsequently made in separate isolated trial
`artifacts/map-006/trial-methods_20261011/` as detailed below. No direct-request
head-to-head benchmark was executed. Original reference is LWBridge 0.3.17; current provider is
an admitted-client adaptation/own-design implementation, not a decrypted copy
of the original protected traversal controller.

## What is known versus our implementation

Recovered host/API/storage rules and scan-result UI are separate from how the
game data is acquired. MAP-005 receipt explicitly does not claim original protected
traversal equivalence. `CurrentClientMap317ScanProvider.cs` likewise describes
traversal/capture as current-client implementation details. Actual row-major order,
camera/AOI batching, adaptive coverage steps and retry orchestration must not be
called original LWBridge facts merely because they work.

`MapScanTraversal.cs:18` calls the grid an IMPLEMENTATION POLICY: recovered
20-tile cardinality is used for a deterministic covering grid. Current full-world
batch acquisition uses the actual client WorldPointManager response footprints,
accumulates coverage and reads typed records from the game; it is not screenshot
recognition or a bot visually reading each tile.

## Why the map view moves

`CurrentClientMapBlockSource.FastCity.cs:746` sends requestMode=coverage with a
target map position. `tools/current_live_resource_probe.lua` contains the real
client SendViewRequest/UpdateViewRequest path and SetCameraPos/restore handling.
The current approach uses camera/view state to request distant area data and
verify its footprint. Movement is of the view, not evidence of troop marching.
Reading only currently cached point data cannot by itself prove fresh world-wide
coverage; stale/partial cache results must not be called a full scan.

## Other available/candidate paths

| Path | Actual source status / limitation |
| --- | --- |
| View-assisted region requests | Current full-world path with recorded live populations and exact AOI union. Supported baseline, not proven fastest or original-equivalent. |
| Direct region request without visibly moving the view | Lua has direct `SendViewRequest` and reflected `SendAoiRequest` modes, but these candidates do **not** demonstrate camera-free acquisition: for the ordinary path they still pass through an internal camera shift and native response-flag handling. `SendAoiRequest` also temporarily mutates `_addViewIndex` and `_curViewIndex`. They have no proven full-world positive live witness. |
| Type-specific lists | Current Railway acquisition explicitly refreshes official `LWTrainDataManager.TryGetTrainList(true)` after area capture; Lua waits for `RefreshTrainListData`, and native result validation checks the session/live server/matchServers. `CaptureFullTrainListMapAsync` exists but inspection found no caller; do not present that unused whole-scan shortcut as deployed. Truck-only acquisition should be assessed separately before optimizing. |
| Coarse map / lower detail | A specialised Monster route exists. Source comments explicitly state coarse snapshots miss some generic Monster populations, so it is not a universal replacement for detailed region coverage. |
| Cached game records | Useful for displaying/refreshing known data; incomplete/stale cache alone does not establish fresh full-world acquisition. |

No inspected source proves a universal official "fetch all map objects" operation
for all kinds. Do not claim none can exist, or invent an endpoint in its absence.
Different record types may legitimately require different acquisition paths.

## Evidence-based comparison on current implementation

| Acquisition | Coverage and freshness actually established | Elapsed and request count | Camera disturbance and cleanup | Decision |
| --- | --- | --- | --- | --- |
| **Deployed adaptive LOD0 AOI union (eight kinds)** | Runtime validates each native footprint and exact **10,000/10,000 AOIs** before publishing 2,500 logical blocks. Saved genuine server-2212 mixed run `9da7ae565efa46e09b0dc08677bbaa5a` published City **7,059**, Resource **8,011**, Monster **8,799**, Truck **458**, Dispatch **2,309**, Treasure **43**; Railway/Ghost **0**. The captured game session, server, fresh timestamp, response flags and native footprint are checked per request. | Saved run `1791692468663..1791692723659`: **254.996 seconds** (end-to-end). One `ProbeFastCityBatchAsync` per AOI probe attempt; retries can add more. Exact network/request totals were **not recorded**. **2,500 logical blocks are not 2,500 requests**. | The Lua producer temporarily moves the *internal map camera* to the target; `coverage` adjusts FOV/aspect and restores them. For Resource it defers camera restoration until response; other kinds restore same tick. Validation requires `cameraTileStable`, `responseFlagsTransitioned`, matching geometry and appropriate restoration state. The later pilot cleanup restored installed script hashes, no journal, no game processes. This does not quantify visible motion. | **Keep as reference implementation.** |
| **Deployed same-server Truck-only Auto** | Saved genuine run `5e2d231caf174d979c1106962bac258a` completed **2,500/2,500** logical blocks, published **434 Trucks**, no failures. Its selected-kind completeness is proved, but the changing population makes it an invalid speed comparison against the mixed run. | `1791693487042..1791693617616`: **130.574 seconds**. Exact AOI and network request totals not recorded. | Same AOI camera/recovery path; Auto later disabled and task original restored. | **Useful separate functional witness, not a direct-path speed result.** |
| **Fresh two-server Truck-only Auto** | New packaged source-built native UI profile `map-006-methods_20261011`: server **2212** run `7a2cc2754d89442588e88f19418425f6` completed 2,500/2,500 and published **401 unique Trucks**; server **2213** run `e1ae25038ec640bda181b6539cc7cd93` completed 2,500/2,500 and published **162 unique Trucks**; no failed blocks or staging residuals. Auto cycle `outcome=completed` with completed IDs `[2212,2213]`; proven game `server-jump-result` explicitly returns from 2213 to original 2212. | Scan DB run lifetimes **110.191 s** and **106.658 s** respectively. Cycle lifetime **225.100 s**, including transition/return. Counts are not an acquisition request metric. | `bulk-aoi-diagnostic-result.json` on final recorded region: `requestMethod=WorldPointManager.UpdateViewRequest(true)+same-tick-camera-restore`, `requestMode=coverage`, `cameraTileStable=true`, pre/post camera `(557,471)` and `positionRestoredBeforeResponse=true`; native flags transitioned. Auto disabled, game and host closed, original Lua hashes restored, no recovery journal. | **Additional genuine same-method multi-server confirmation, not a faster alternative.** |
| **Reflected direct `SendAoiRequest` / `SendViewRequest`** | Existing Lua candidate code queues bounded regions, but there is no saved whole-world exact 10,000-AOI union, record-identity/freshness equivalence, or positive multi-type production witness using it. Direct temporarily writes `_addViewIndex` / `_curViewIndex`; ordinary `SendViewRequest` uses an explicit view-level argument. | **No comparable elapsed or request-count measurement.** One Lua invoke is not evidence of one successful server response per requested region. | Still uses camera shift/restore for the ordinary path and shared response flags. A high-view-level alternate path bypasses some camera setup, but its type and LOD completeness is unproved. Restoration of modified internal collections/flags requires an adverse-case test. | **Do not replace LOD0 production coverage.** |
| **Official Train list (type-specific)** | Refreshed `TryGetTrainList(true)` event and `ls`/`allianceTrainList` distinguish Truck/Railway and enforce server-match validation. It is merged **after full AOI scan for Railway**. Fresh isolated genuine `trial-missing_20261011` Train-list refresh showed `refreshObserved=true`, `railwayServerIds=[2197]` and one source row from server 2212. Complete selective Auto scans then published two unique Railway rows each on 2213 and 2197. This proves positive Railway acquisition but **does not** prove list-only world coverage or equivalence for Trucks. Whole-list shortcut remains unused. | One explicit Train refresh per `ProbeTrainListAsync` attempt; no saved standalone list elapsed/request totals. Railway/Ghost full AOI scan lifetimes **109.803 s** (2213) and **69.613 s** (2197); these timings cannot be assigned to the list refresh alone. | Train-list fetch itself does not move camera. The full AOI scan still invokes its camera/response path. Refresh event listener cleans up on observed result or timeout. | **Keep Railway supplement, do not substitute a list-only shortcut until full identity/coverage test.** |
| **LOD2 / cached snapshots** | Zombie Boss-only coarse snapshot is a specialized route; current code warns generic Monster/Doom Walker instances can be absent at LOD2. Cached maps alone cannot establish fresh all-kind coverage. | No like-for-like duration/request counts. | LOD transition has original-camera/LOD restoration checks, but differs from normal full-world footprints. | **Keep specialized; cannot be universal fallback.** |

The elapsed values measure database scan-run lifetimes, **not** a controlled
network microbenchmark. The fresh two-server run uses the same Truck-only scan
configuration, but different servers and times and therefore does not isolate
any acquisition method's effect. The final native probe snapshot of server 2213
records `baselineNetSendCount=145` and `netSendCount=148`, receive count **305 →
309** during that *one* observation. Counters include concurrent ordinary
game traffic and the file is overwritten per region, so **3 sends / 4 receives
are not attributable whole-scan totals** and cannot be extrapolated to 2,500
blocks. No defensible speedup ratio exists. Existing
proof establishes positive populations for **seven** of eight kinds after the
fresh selective Railway trials; there is still **no positive Ghost** record or
equivalence to the original encrypted scanner traversal.

Before any direct replacement: use a new isolated official-client pilot; hold the
same type/server/version and admit a current authenticated session; record target,
native request/response counts and retries, timestamps, exact AOI union, unique
row identities/fields, observed reached server, return camera tile/LOD/FOV/aspect
and modified manager flags/collections; compare acquisition in both orders to
account for changing rows. Stop or mark the trial failed on any unproved cell,
stale reply, un-restored state, or missing kind, retaining the published baseline.
Worker live claim, plunder and sharing remain excluded.

## Efficiency acceptance

There is no completed like-for-like benchmark establishing the current method as
best. Before changing the baseline, compare suitable direct/list/region routes on
the admitted client for actual coverage, row identities/fields, freshness, elapsed
time, game request counts where measurable, camera disturbance and cancellation/
restoration. Account for changing live populations instead of assuming identical
row counts across different times. More speed with missed/old rows is not success.

Use type-specific direct sources where they actually cover the required population;
consider grouped region requests and restoring the original view without visible
movement when current-client evidence supports them. Do not simply increase
concurrency or change request limits without validation. Preserve the existing
successful data/cancellation/publication contract.

Current owner permits server switching for passive scan/Auto assessment; no live
claims/plunder/sharing. This assessment is an additional MAP-006 obligation, not
a declaration that a faster, direct, or original-equivalent method has been proved.
