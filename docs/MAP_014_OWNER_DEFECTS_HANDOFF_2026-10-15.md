# MAP-014 owner defects (4 reports) — handoff, 2026-10-15

Branch `codex/map-owner-fixes-014`, draft PR #10. Checkpoint commit `a7edcd0a` (predecessor's uncommitted
8-row work, committed unchanged). Everything below this line in "uncommitted" is NOT committed and NOT final.

## 1. Truck / Secret Task slowness — ROOT CAUSE PROVEN, fix partly verified

**Cause (genuine evidence).** The native AOI window scales with camera height (area ~ height^2). The harness
game runs at zoom 105 (window 10 rows x 4-5 cols); the owner's camera was zoomed in to 81.3 (saved owner
replies in `%LOCALAPPDATA%\LWBridgeRebuild\live-resource\batch-negatives`: 8 rows x 2-3 cols = 16-24 AOIs).
Pre-fix code (`f7f80fef`) rejects any reply < 20 AOIs / < 9 rows. Full-world capture throws, the engine
retries block 0, marks it failed, and every later block runs the legacy per-band route whose probes are
rejected the same way. Each rejection counts as a "failed block" and advances the displayed progress.

**Genuine reproduction.** Pre-fix export, test game camera set to zoom 81.31 (harness-only fixture),
Truck-only, Fast: first failure at 6 s; **5.4 % at 153.9 s (identical to the owner's screenshot)**; 10.0 % at
280 s; completed = 0, failed = 257. Progress sampler: `scratchpad/before-zoom81-progress.csv` (not in repo).
Baseline pre-fix at default zoom 105: completes 192.7 s, 340 probes, 0 retries (so not a general regression).

**Fix status.**
- Committed (`a7edcd0a`): accept 8-row windows, placement adapts, probe ceiling 200, fatal full-world error is
  terminal (no fake failed-block progress), Truck/Railway no longer mislabelled as the unused Train-list route.
- Uncommitted, live-tested once: probe `coverageZoom` (canonical 105, `CanonicalCoverageZoom` in
  `CurrentClientMapBlockSource.cs`, echoed as `coverageZoom`/`coverageZoomApplied`, validated in C#; exact camera
  restoration verified). Post-fix at owner zoom 81.31: **completes, 10,000/10,000 AOIs, 364 Trucks, 0 failed,
  camera bit-identical, 391 s, 680 probes** (artifacts/map014-lead-live/native-benchmark-truck-20261015-192958).
  So correctness is restored, but 680 probes vs 340 at default: **setting `Zoom` did not reproduce the default-zoom
  window** (likely the transform/anchors don't update in the same Lua tick). Open question.
- Next: a `lens-matrix` diagnostic scenario was added (tools/map_014_lead_native/Program.cs) and started
  (output under artifacts/map014-lead-live/native-lens-matrix-*): per-probe window rows x cols for player zoom
  {default, 81.31, 60} x lens {none, 105, 130, 81.31}. Read it to decide: (a) lens via `SetCameraPos` with a
  raised y and compensated z, (b) larger FOV, or (c) accept the slower-but-correct 8-row path. Do not claim a speed
  fix until Truck-only, Secret-Task-only, Truck+Secret and a broader mixed run are timed at BOTH zooms.
- New instrumentation (uncommitted): `footprintHistogram`, `newlyCoveredAois` in map-fullworld-probe-metrics.json.
- Harness additions (uncommitted): `tools/map_014_lead_native/map014_zoom_fixture.lua` (harness-only, appended to
  the private trace copy only when `LWBRIDGE_MAP014_FIXTURE_ZOOM` is set), `LWBRIDGE_MAP014_BENCH_MODE=fast`,
  `..._BENCH_ROUNDS`, `LWBRIDGE_MAP014_COVERAGE_ZOOM=off|<n>`.
- Probe LENC size: 62,072 / 65,536 bytes after the zoom edit (cap is the 0xFFFF entry limit in
  `tools/run_overview_bridge.py:192`). Ghost + Zombie + terrain edits must share ~3.4 KB.
- Honest note: an earlier tool denial recorded in docs concerned an "intermediate zoom" test; none occurred this
  session, but the new zoom lens is adjacent to it — if any call is denied, stop and report.

## 2. Resource "Black tile" — ROOT CAUSE FOUND (research agent, offline)
`LuaEntry.Player:IsInBlackRange(worldPos)` ignores its argument (PlayerInfo.luac) and returns whether the
SCANNING ACCOUNT'S OWN city is in the black land, so it is identical for every tile (all 8,010 stored rows = false;
19 rows near X398 Y483 included). The real tile predicate is `SceneUtils.IsInBlackRange(pointIndex, serverId)`
(central rect, e.g. x450-549 y450-549 for the season rows that define it; which season applies to 2212 is
unknown). The owner's -80°C is most likely `TemperatureManager:GetTemperatureByIndex(x + y*1000 + 1)` in
season snow mode (global blizzard ambient modified by furnace ranges, time-varying) — PROBABLE, not a fixed terrain
flag. Plan: probe change to `pcall(SceneUtils.IsInBlackRange, pointIndex, serverId)` + separate nullable
`temperatureC`; bump producer version so old rows read as UNKNOWN; add a harness-only read-only terrain probe
(tile list in the research report) to confirm at X398 Y483 vs controls; ask the owner which concept they mean.
Evidence: `artifacts/map014-blacktile-research/evidence/`. Predecessor's strict-JSON-boolean SQL predicate is
committed and stays.

## 3. Zombie Boss (喪屍首領) — identified; implementation IN PROGRESS by a subagent (uncommitted)
Config table `lw_world_monster`, text key 2901012, `special == 10` (MonsterInvasionBoss), 51 templates; level 85
= ids 1030019/1031017 with recommend_power 34,500,000 (matches the screenshot). Defects found in current code:
`PrepareMonsterRecords` silently drops special-10 rows; `zMBoss*` fields belong to Zone Mobilization (do not reuse).
Plan: add `zombie_boss` kind, route rows individually, UI tab/locales, decouple detail queue from Doomsday.
Live positive and expiry proof NOT done. A `MAP014_OTHER ... selection remains eight` inert check currently
fails because `AllTypes` was extended mid-work — reconcile (expected default stays 8) before committing.

## 4. Ghost reward filter — backend + UI built by a subagent (uncommitted), blocked on data
Filter (`rewardFilters`, `rewardMatch` all|any, minCount, options, UI, 9 locales via supplement, controlled tests
passing). Genuine rows carry no resolved reward list (`rewardConfig` raw scalar only), so every real Ghost row is
"unknown". A subagent was asked to decode `base_reward_show` offline from the installed config table and emit
`rewards` from `ghost_aoi_records`. No genuine Ghost record verified; passive scan only, no plunder.

## Not done
Truck/Secret/mixed repeat timings, Black tile live probe, Zombie live positive, Ghost live positive, final
source-pinned ZIP + extracted-app tests, Home regression. Run the full inert suite after agents stop; commit only
when green. Whole Map remains PARTIAL.
