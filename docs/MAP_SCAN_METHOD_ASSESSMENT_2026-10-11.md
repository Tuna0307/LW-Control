# Current Map scanner — origin and efficiency assessment

2026-10-11 lead source inspection at checkpoint `dd2a0ff9`. No new live benchmark
or game experiment. Original reference is LWBridge 0.3.17; current provider is
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
| View-assisted region requests | Current full-world path with recorded live populations. Supported baseline, not proven fastest or original-equivalent. |
| Direct region request without visibly moving the view | Lua contains direct SendViewRequest and reflected SendAoiRequest modes. Presence of code is not full-world positive proof without camera movement; assess actual validation, returned footprint and restoration. |
| Type-specific lists | Current Railway acquisition explicitly refreshes the official train list after area capture. `CaptureFullTrainListMapAsync` exists but inspection found no caller; do not present that unused whole-scan shortcut as a deployed positive witness. Truck-only acquisition should be assessed separately before optimizing. |
| Coarse map / lower detail | A specialised Monster route exists. Source comments explicitly state coarse snapshots miss some generic Monster populations, so it is not a universal replacement for detailed region coverage. |
| Cached game records | Useful for displaying/refreshing known data; incomplete/stale cache alone does not establish fresh full-world acquisition. |

No inspected source proves a universal official "fetch all map objects" operation
for all kinds. Do not claim none can exist, or invent an endpoint in its absence.
Different record types may legitimately require different acquisition paths.

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
permission to start a benchmark during the current owner discussion.
