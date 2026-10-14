# MAP-014: consecutive FOV20 engine integration research

2026-10-14, independent offline worker-5 analysis on
`codex/map-owner-fixes-014`. **Result: implementable diagnostic architecture;
production consecutive mode remains unqualified and disabled.** This document
does not assert an owned live run. No production code, installed game script,
archived receipt or shared continuation was edited for this investigation.

## Native source and causal evidence

| Evidence | Native observation | Supported conclusion |
| --- | --- | --- |
| `artifacts/map-013-live/native-causal-20261014-003430` | FOV120 first retained A(455,425) produced 50 AOIs and 47 point records; individually restored B/C control receipts succeeded with 50 AOIs. | A's single success does not imply B follows correctly. This archive also contains a separate time-separated Resource-churn negative; do not reclassify it as camera loss. |
| `.../native-causal-20261014-003652/sequence-region-2.json` | B logical tile (545,495), intended AOI **4954**, but `_curViewIndex` retained 15 old-A AOIs **4243–4447** after update, target absent. `map013NativeReplyState=waiting`; terminal `map013_native_reply_or_cache_timeout`. Independent B/C restored controls succeeded. | Camera tile/target echo and reset response flags cannot substitute for acquired B footprint and correlated native reply. FOV120 also showed stale physical anchors, per `MAP_014_LEAD_LENS_QUALIFICATION.md`. |
| `.../native-causal-20261014-004239/sequence-region-{1,2,3}.json` | FOV20 native post-lens/post-update A/B/C footprints contain **90** contiguous AOIs each, including targets 4245/4954/5665. All three `map013NativeReplyState=matched`, response flags true; A/B retained, C restored final camera/projection/home state. B and C match independent restored controls. | Source-backed private consecutive success for this particular client/session/geometry. This is **diagnostic evidence**, not application integration. |
| `.../native-causal-20261014-004916/full-sequence-result.json` | 119 diagnostic probes, exactly 10,000/10,000 native AOI union, **49.203 s** discovery, `exactCameraRestored=true`, `publishedProductRows=0`, `productionEnabled=false`, `externalPopulationCensus=false`. | A full-world diagnostic discovery and final restoration were observed. Engine staging, publication, UI, Stop and packaged execution were not exercised by this result. |

The private positive's `sequence-region-*.json` response omits
`coverageFov` even though native `map014SubscriptionStages.post-lens.geometry.fov`
reports **20.0**. The offline gate checks the applied FOV. A production protocol
should return and validate both requested and applied FOV.

**Original Lua protocol** (`tools/current_live_resource_probe.lua`):

- Lines **2480–2551** parse bounded camera parameters and the
  `deferRestoreUntilResponse` contract. Lines **5713–5728** calculate the
  displaced camera and call `TouchCamera.SetCameraPos` then
  `Camera.RefreshCameraAnchor`. This establishes the logical remote tile.
- Lines **5830–5853** apply FOV/aspect and capture the original manager/world
  reply flags. Lines **5853–5860** reset `isRecvViewPoints` and
  `hasReceiveViewPointsReply`; lines **5920–5936** invoke the native
  `WorldPointManager.UpdateViewRequest(true)` request.
- Lines **5193–5278** restore manager flags, changed LOD/count state,
  FOV/aspect/zoom, camera position, anchors, Unity projection/transform
  and geometry checkpoint. `RefreshCameraAnchor` can rebuild projection;
  restoration order in **5233–5250** is intentional.
- Lines **6034–6074** wait for a *separate fresh home-view response*
  after deferred restoration and confirm exact geometry. Lines
  **6372–6428** only write `proven` after response and completion contract.
  Ordinary diagnostic requests therefore restore home **each probe**.
- `fail_bulk_aoi` and the timeout path restore original state before a
  failure result (see **5541** and **6413–6429**; verify the exact current
  source before implementing). That cleanup is per request, so it cannot be
  reused blindly when A/B have intentionally retained the shared camera.

**Private experimental addition** is archived, not installed/production, at
`artifacts/map-013-live/native-causal-20261014-004239/experimental-resource-probe.lua`:

- Lines **9–43** arm/match/retire a correlated native network sampler using
  request ID, profile, session, PID, server, target tile and LOD.
- Lines **5304–5358** use a session-owned group, retain **the first A** full
  camera/projection/manager snapshot and provide original-home restore on
  failure or idle deadline. This scope matters: B's “original” position is
  A's remote location and is *not* home.
- Lines **5933–5975** refresh anchors and `StartViewRequest` **after**
  applying the lens, then call `UpdateViewRequest(true)`. Lines
  **6296–6331** require matched native reply, both response flags, split
  completion and native target AOI membership, with bounded re-arm. Lines
  **6531–6568** keep A/B's camera and restore A's original home on final C.
  These actions, including native response/window matching, are a unit;
  repeating `SetCameraPos` alone is insufficient.

The positive is source-consistent with an anchor-and-subscription refresh
under FOV20. It does **not** decode every native request path; the FOV120
negative cannot support general FOV20 capability for mobile march caches.
The 12-second private idle guard runs when no bulk request is active
(`_map013SequenceGuard`); it is a research recovery mechanism, **not**
a sufficient Stop guarantee for a production run with an active pending request.

## Current production boundary and integration shape

`src/LWBridge.Desktop/CurrentClientMapBlockSource.FastCity.cs:836–921`
builds a **new** `bulk-aoi-diagnostic.txt` request for each target,
unconditionally sets `deferRestoreUntilResponse=true` (line **881**) and
polls for the request ID. Its validator at **1451–1516** insists on per-probe
`positionRestoredAfterResponse=true`, `cameraTileStable`, exact restored
geometry/projection and the deferred-home request method. A private keep
receipt correctly has `cameraTileStable=false`, so passing it through the
current validator would be a contract bypass, not integration.

`CaptureFullCityMapAsync` at **300–510** owns the serial AOI-union planner,
per-probe retry and coverage/record merges. `FastFullWorldResumeState` at
**2346–2402** binds cached acquisition to an exact run/session/server/world
and selected kinds; it currently has no group camera-cleanup obligation.
`MapScanEngine.cs:46–151` invokes batch source, checks cancellation after
capture and before checkpoint/publish; all 2,500 logical blocks must complete
before publication. `MapDataStore.ScanEngine.cs:32–76,97–139` writes staging
and publishes only after its completion check. `MapDataStore.ScanEngine.cs:174–211`
discards *that run's* staging on failure/Stop. These guards should be retained.

Suggested minimal production architecture (implementation belongs to prime):

1. Add a separate explicit opt-in `ExperimentalConsecutiveFov20` selector
   to test hooks. Defaults and mobile/mixed-kind route remain unchanged.
   Admission requires the exact owned Overview session, LOD0 server/world,
   standard Unity projection, stable camera instance, serialized scan lease,
   compatible FOV20 footprint, and proven point/march source gating for each
   selected kind. Do not piggyback on `ExperimentalBoundedCameraLens`.
2. A **single run-scoped Lua group** captures original `SetCameraPos`,
   FOV/aspect/zoom, Unity transform/projection, physical anchors, manager
   `startViewRequest`, block count and reply flags **before A**. Every A/B
   request receives fresh, one-at-a-time request ID and native sampler window.
   Its transition is:
   `SetCameraPos(target) → lens(FOV20/aspect4) → RefreshCameraAnchor →
   StartViewRequest → reset native reply flags → UpdateViewRequest(true) →
   source-matched native reply + processed cache + target _curViewIndex →
   snapshot all selected source records → retire reply window → next region`.
   Capture the exact native AOI indices, rectangles, target membership and
   per-field native source timestamps; do not rely on logical target echo,
   flag reset alone or a hardcoded count of 90 near world edges.
3. The group returns two kinds of receipts: `kept` for intermediate regions,
   with native target/reply proof and intentionally non-home camera; `final`
   after **one** original-home restore followed by fresh home subscription
   reply, matching tile, projection, position, rotation, zoom, aspect/FOV,
   original manager flags, empty split queue, and native LOD0 geometry.
   The managed validator branches on its *explicit* position in the group:
   it must not weaken the existing per-probe restoration validator.
4. Add an explicit native `abort-group`/cleanup acknowledgment bound to
   run ID, profile, launchSessionId, game PID and original group generation.
   On Stop, cancellation, timeout, source validation failure, server/session
   change, native request error, and process retirement: stop accepting new
   region results, retire outstanding network windows, restore original A
   snapshot **while the same owned world is still available**, wait for a
   correlated home-view update, and record durable cleanup outcome.
   Cleanup uses a bounded **non-cancelled** token; passing the cancelled
   scan token into cleanup would skip it. If the owned process/world has
   vanished, report restoration **unverifiable** and require re-admission;
   do not mutate another session or claim camera success.
5. Extend `MapScanEngine` source lifetime with explicit
   `IMapScanAcquisitionCleanup` (`Complete/AbortAsync`, run-bound);
   ensure `ExecuteAsync` invokes cleanup in `finally` before terminal
   Stop/Fail, and only `Publish` after final native restore acknowledgment,
   captured native records, exact AOI union and owner/session continuity.
   In the current engine, `OperationCanceledException` immediately
   `sink.Stop` without awaiting a native cleanup
   (`MapScanEngine.cs:165–171`). Put cleanup at the source/session boundary
   and preserve exact-run persistence state transitions. Await Stop completion
   through the existing `Map317CommandService.cs:576–586` transition path.
6. Publish via the existing 2,500-block projection/staging transaction once
   all source cells pass. Keep per-selection completeness diagnostics and
   comparative source fields intact, especially latest City owner location,
   Resource occupancy/fullness and distinct Truck/Train native march
   acquisition freshness. No partial or cancelled run may replace published
   rows; a late result from a retired group fails owner/generation validation.

### Required independent qualification before enabling

Start with a private opt-in in the exact current client and save every phase
receipt, then run through **actual engine and SQLite**, and finally a Windows
package in the approved owned session. Tests need A→B→C interior and
0/999 edges, overlap/row seams, both standalone City and Resource fields,
an independently refreshed source snapshot and paired restored baseline
identities, all 10,000 AOIs and all 2,500 blocks; measure separate native
response, cleanup, hydration, projection, checkpoint and publication durations.
Deliberately force cancellation/Stop immediately after A and after B, during
an outstanding response, during final restore, and before publish; assert
exact original camera/projection, native home subscription and zero new
published rows each time. Test mismatched run/session/PID, late old-region
reply, stale old-A indices, wrong/missing target AOI, missing home response,
native split pending, missing Unity camera and cleanup timeout. Check explicit
session retirement and installed original-script hashes at the owner handoff.
No fabricated native replies or arbitrary sleeps qualify a live gate.

**Known blocker:** Mobile kinds are independently disqualified from FOV20
by the six-round mixed evidence: `native-benchmark-mixed-20261014-021149`
had baseline Truck 103 versus FOV20 Truck 39 and 92 baseline UUIDs absent,
90 still unexpired at the candidate completion boundary. A point AOI union
does not validate the WorldMarch request/processing queue. Keep the currently
qualified serial route for mixed mobile selections until independent native
march-source freshness is demonstrated; Resource identity/field qualification
is also a separate ongoing workstream. The private 49.203-second discovery
number is not a qualified production or packaged scanner speed.

## Read-only inverse tests

`tools/map_014_consecutive_research/receipt_gate.py` checks archived
diagnostic A/B/C as native-source evidence only. It checks unique request IDs
and session identity, original-home continuity, applied native FOV20,
correlated native-reply status, native post-update rectangular AOI coverage and
target membership, and the final camera tile and serialized checkpoint against
the original. Its reply correlation is based on the archived native sampler's
reported status; the checker does not independently replay network packets.
`test_receipt_gate.py` intentionally alters correlated native reply state,
response flag, post-update `_curViewIndex`, target membership, final camera
geometry/projection, group session identity, duplicated request IDs, applied
FOV, and malformed native AOI indices. It also rejects the real 003652 stale-A
B receipt. No game/app install, external request or source change is made. Run:

```powershell
python -m unittest discover -s tools/map_014_consecutive_research -v
python tools/map_014_consecutive_research/receipt_gate.py artifacts/map-013-live/native-causal-20261014-004239
python tools/map_014_consecutive_research/receipt_gate.py artifacts/map-013-live/native-causal-20261014-004916
```

Passing this offline gate cannot establish live cancellation, persistence,
mobile freshness, packaged app performance or final release qualification.
