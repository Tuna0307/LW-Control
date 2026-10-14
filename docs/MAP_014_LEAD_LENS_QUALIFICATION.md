# MAP-014 lead takeover — ground-facing lens qualification

2026-10-14. Same solo branch `codex/map-owner-fixes-014`, draft PR #10.
The worker is stopped. No account-changing action, mouse input, foreground
forcing, merge or publication. Preserve all previous owner-fix positives.

## Newly resolved causal boundary

Private stage observation `native-causal-20261014-003652` records the actual
camera transform moving from (455,425) to (545,495), while corner anchors
remain at old-A x coordinates. The post-update native subscription therefore
contains old-A AOIs and no target 4954. The camera points down 45 degrees;
the former experimental FOV120 spans rays above the ground horizon. This is
observed anchor staleness, not proof that every native internal branch is decoded.

The diagnostic also omitted the first complete `M._map014CameraRestore`
snapshot. Its corrected helper retains that snapshot alongside the original
camera position. The failed second-region observation now restores exact
geometry, independently verified in its saved before/after snapshots.

FOV40 produces fresh but oversized/split footprints and fails the same-session
footprint comparison (`native-causal-20261014-004008`). It is not enabled.
FOV20 keeps ground-facing rays and yields a complete 15-column/6-row interior
footprint in this v24 setup. Three fresh A/B/C consecutive observations match
the individually restored native controls, with one exact final restoration:
`native-causal-20261014-004239`. A time-separated resource churn failure from
`native-causal-20261014-003430` is preserved; diagnostic continuation never
changes an accuracy failure into qualification.

## Whole-world discovery and actual engine checks

`artifacts/map-013-live/native-causal-20261014-004916`:

- Exact union of 10,000 fresh LOD0 AOIs; target membership and contiguous
  native rectangles required on every observation.
- 119 consecutive region probes, one final home-region response/restoration.
- 49.203 seconds, 4,110 native City identities and 8,000 Resource identities.
- No product Map publication. This is a diagnostic acquisition measurement,
  excluding production enrichment, persistence and UI overhead.
- Exact owned Stop and original-script restoration passed.

`artifacts/map014-lead-live/native-city-20261014-005619` separately exercised
the actual production providers, engine, staging/publication and SQLite with
an opt-in FOV20/two-dimensional uncovered-cell planner. It retained per-probe
home restoration: 2,500 completed logical blocks, zero failures, 4,110 stored
Cities, independent equal camera snapshots, approximately 76.4 seconds at the
five-second status polling boundary. Both whole-world observations found the
same count; they are not an external simultaneous server population census.

## Deployment boundary and next gate

Later source correction and rejected cache-supplement experiment are recorded
in [MAP_014_CANONICAL_CITY_CORRECTION.md](MAP_014_CANONICAL_CITY_CORRECTION.md).
Read that before treating the first baseline benchmark failure as scanner
recall loss. The game's canonical owner resolver identifies superseded cache
locations; unresolved identities still fail. Repeated comparisons are ongoing.

Normal production remains the original guarded serial path. FOV20 is admitted
only by `ExperimentalBoundedCameraLens` in the diagnostic host; default hooks
remain absent/false. The alternative loop validates the requested uncovered
cell, target, unique bounded rectangular footprint and exact 10,000-cell union.
Every ordinary production probe still waits for its response and restores the
full camera before acceptance. No relaxed restoration or fake success path.

The benchmark host runs three alternating baseline-FOV120 / candidate-FOV20
pairs in one exact owned session, saving independent camera checkpoints,
separate SQLite databases, coverage/probe metrics and timings. Available kinds
and mixed selection must pass accuracy, Stop, restoration and packaged-app
gates before selecting a new production default. The consecutive prototype
additionally needs integration/retirement/cancellation qualification; its
49.203-second result is not the packaged application's speed.

## Celebration Party source identification

Installed config archive SHA-256
`f30ae484c4527aeec1db90f3e567d38b7718b96bbfd2364eafd29084146ec41f`
contains `detect_event` row **27000**, type **23**, name
`activity_wajueji_27000_name`, translated by the game's English catalog as
**Celebration Party**. Existing actual game UI maps TreasurePointInfo.eventId
through DetectEventTemplateManager/GetRealName; existing scanner uses that same
template name key. This resolves the earlier unidentified config discriminator.
The two previously observed Treasure points had events 27015 and 25098 and
must not be relabelled Party. Positive event-27000 acquisition is still pending.

`ice_supplies` row 1 independently confirms type 1 and key
`season_s2_ice_supplies_11`. The literal-only config reader rejects calls,
globals, closures and branches; no game code is executed to read these tables.
Raw selected rows and per-table hashes remain in ignored task artifacts.

## Verification / state

Actual diagnostic helpers, Lua 5.3 compile and package-size/source-anchor tests
pass. Controlled full-world/negative footprint checks pass. Desktop and native
diagnostic Release builds pass with zero warnings/errors; the default inert
Home/Map/Auto/storage/provider suite passes, including the five new rectangle
inverses. Canonical frontend build/package validation passes; no new UI parity
claim is made. No repeated whole release ZIP.

**Overall still PARTIAL while performance/production integration and positive
event availability gates remain open.** Continue the actual benchmark and
qualification loop; do not repeat the old FOV120 anchor failure or report this
as whole-Map completion.
