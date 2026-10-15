# MAP-014 owner defects (4 reports) — status, 2026-10-15

Branch `codex/map-owner-fixes-014`, draft PR #10. Checkpoints: `a7edcd0a` (predecessor's 8-row work), then the
commit that adds this file's work. Whole Map remains PARTIAL (owner-only actions and providers unchanged).
All timings below are from a **diagnostic host** (real native game, real Map pipeline, no packaged WebView)
unless stated; the packaged-app result is recorded separately at the end.

## 1. Truck / Secret Task slowness — root cause proven, fixed, live-verified

**Cause.** The native AOI window scales with camera height (area ~ height^2). The harness game sits at zoom 105;
the owner's camera was zoomed in to 81.3 (saved owner replies in
`%LOCALAPPDATA%\LWBridgeRebuild\live-resource\batch-negatives`: 8 rows x 2-3 columns). The pre-fix validator rejected
windows < 20 AOIs / < 9 rows, full-world capture threw, the engine marked block 0 failed and every later block ran
the legacy per-band route whose probes were rejected the same way. Each rejection counted as a failed block and
advanced the displayed progress.

**Genuine reproduction (pre-fix code `f7f80fef`, game camera set to zoom 81.31 by a harness-only fixture,
Truck-only, Fast):** first failure at 6 s; **5.4 % at 153.9 s (owner's screenshot: 5.4 % at 2:33)**; 10.0 % at
281 s; completed 0, failed 257 — run ended early by the operator. At default zoom the same pre-fix code completes
normally (192.7 s, 340 probes), so this is a zoom-dependent failure, not a global regression.

**Correction (all committed).**
1. Validator accepts 7-12 row windows that overlap the band and contain the target; exact 10,000-AOI union stays the
   coverage proof. 2. Probe `coverageZoom`: every coverage probe uses canonical lens height 130
   (`CanonicalCoverageZoom`), the probe echoes requested + applied zoom, C# verifies both, whole camera geometry is
   restored and verified. Lens matrix (`artifacts/map014-lead-live/native-lens-matrix-20261015-193845`): lens 130 gives
   10x3 windows for players at zoom 81/60 (11x3 at 105), LOD 0, geometry restored in every probe; lens 105 only
   reached 9x3. 3. Vertical placement calibrates from the observed window offset (retry once, then proven centre
   placement), because without it every column step needed 2 probes (680 probes). 4. A fatal full-world source
   error is terminal (no fake failed-block progress). 5. Footprint histogram + newly-covered-AOI metrics.

**Live results after the fix (Fast mode, 10,000/10,000 AOIs, 0 failed blocks, 0 retries, camera restored
bit-exactly, scripts restored, exact Stop):**

| Selection | Player zoom | Rounds (s) | Probes |
| --- | --- | --- | --- |
| Truck + Secret Task | 81.31 | 218.1, 216.9 (an earlier 130-lens run without calibration: 380.7, 388.4 with 680 probes) | 375 |
| Truck only | 81.31 | 205.5, 204.8 | 375 |
| Secret Task only | 81.31 | 210.3, 211.2 | 375 |
| Zombie Boss only | default | 199.2 | 340 |
| Ghost only | default | 183.9 | — |
| All kinds mixed (city, resource, monster, zombie boss, truck, train, secret task, ghost) | default | 212.6 | 340 |

Where the time goes (mixed, default zoom): acquisition 202 s of 212 s (about half moving the camera to the target,
half restoring it), hydration ~1.3 s, SQLite checkpoints 4 s, publication 1.3 s. Truck counts differ between rounds
(e.g. 266 vs 230) because Trucks move/expire; every round covered all 10,000 AOIs. Concurrent subagent builds
overlapped one run (Truck+Secret 20:22); results were consistent across rounds.

Test-only harness (not shipped): `map014_zoom_fixture.lua`, `map014_terrain_fixture.lua`, `lens-matrix` /
`terrain-probe` scenarios, env `LWBRIDGE_MAP014_BENCH_MODE|BENCH_ROUNDS|FIXTURE_ZOOM|COVERAGE_ZOOM|TERRAIN_FIXTURE`.

## 2. Resource "Black tile" — cause found, fixed, live-verified
`LuaEntry.Player:IsInBlackRange(pos)` ignores its argument and only reports whether the scanning account's own city
is in the black land, so all 8,010 stored rows said "not black". The game's own "gather in the Contaminated Land"
warning (`world_tip10004`, `UI/UIWorldPoint/Component/UIWorldPointBtn.luac`) uses
`SceneUtils.IsInBlackRange(pointIndex, serverId)` (rectangle test) or a temporary alliance-skill black area
(`AllianceSkillManager:GetBlackAreaOverTime > 1000`, NOT scanned). Live read-only terrain probe
(`native-terrain-probe-20261015-203209`): true exactly for x 450-549, y 450-549 on server 2212 (boundary tiles
verified). The -80°C is the global blizzard ambient (0.0 everywhere in one pass, -70.0 at sampled tiles 60 s later;
home furnace tiles 3.5): time-varying, not terrain, so no temperature filter. Owner's X398 Y483 area is outside the
black land. Fix: probe uses `SceneUtils.IsInBlackRange(x + y*1000 + 1, serverId)` (index formula verified against all
8,010 rows), source label changed to `...SceneUtils.IsInBlackRange` (two strict C# validators + fixtures updated),
legacy rows labelled `...Player.IsInBlackRange` read as UNKNOWN in query, old store and table.
Live mixed scan: 88 black, 7,911 normal, all 88 inside the rectangle, 0 mismatches. Temporary alliance black
areas are not included (documented limitation).

## 3. Zombie Boss (喪屍首領) — implemented and live-verified (no rewards, per owner)
Config `lw_world_monster`, text key 2901012, `special == 10`; ordinary 入侵喪屍 (special 9) stays in Monster.
New opt-in kind `zombie_boss` (last scan checkbox, tab after Other so existing positions are unchanged; default
selection remains the eight recovered kinds). Level filter (5-150 step 5), power, discoverer, lifetime
(`refreshTime`), protection state (only when game-confirmed). Routing is per row inside the shared LOD0 sweep (no
extra pass). The LOD2 shortcut is now an explicit diagnostic opt-in (`UseCoarseZombieBossMap`) because it was only
proven on v18/v19. Live: 150 rows in the mixed scan, 212 in the Zombie-only scan; level 85 power = 34,500,000
(matches the owner's screenshot). Expiry removal after `refreshTime` not separately observed.

## 4. Ghost Ops rewards — filter and authoritative rewards verified live
`base_reward_show` (`itemId;rewardType;count|...`, decoded from the installed table, 81 cfgIds) is parsed with the
game's `RewardManager:ParseRewardsStr`; live: 205/205 tasks carry resolved reward lists, task owner server (e.g. 2192)
distinct from source server (2212). Filter: `rewardFilters` + `rewardMatch` (all = default AND, any = explicit),
minimum counts, options per server, 9 locales; controlled/inert tests pass. Skill Medal (`reward:27:5001`) arrives
named by its text key `180372`; the UI helper `mapRewardName` now resolves all-digit names through the game text
(Skill Medal). Passive scan only; no Ghost plunder enabled.

## 5. Intermittent packaged-scan abort found and fixed (alliance collection node)
Three of seven packaged proofs aborted before publication with "Resource native source accounting ... did not
reconcile". The gate now names the failing rule/row (no rule loosened): the single failing row was tile (10,202),
native **pointType 26, class `WorldAllianceCollectResource`** (an alliance collection node with no resource config or
level) that appeared in the world during the session; the validators demanded `ResPointInfo` for every node. The
design deliberately requires every native type 1/7/26 point to be an accepted Resource (three separate gates), so the
fix accepts the alliance class **only for type 26** (config/level stay unknown); every other type must still be
`ResPointInfo`. An earlier attempt to exclude the node broke the type-accounting invariant and was reverted.
Controlled tests: positive case + two negatives (unknown class on 26, alliance class on type 7).

## 6. Packaged real-app proof (published EXE, real game, normal mode, English clone UI, Chinese game)
Passing run `artifacts/map014-takeover/native-ui-20261015-140953/` (source `dd3a962a`): 2,500/2,500 blocks, 0 failed,
213 s; gates passed: Other tab, warzone popover, Resource combined filters, Dispatch owner names, Zombie Boss tab +
level filter (207 rows; level 75 -> 46), Ghost two-reward AND filter (251 tasks; selection Tech speed-up + Fiona
shards -> 13) with the Skill Medal option labelled "Skill Medal", Truck icons, filter-menu icons, Truck Follow->Stop,
genuine Truck combined filters; original scripts restored, no recovery journal. The Ghost/Zombie gates are opt-in
(`LWBRIDGE_MAP014_NEW_KINDS_UI=1`); the selection then adds Ghost (index 6) and Zombie Boss (index 9).
The Skill Medal reward name arrives as game text key 180372; the Map page now loads reward text keys with its other
game text (`MapDataPage.jsx`) and `mapRewardName` resolves all-digit names.

## Not done / limits
Natural Zombie Boss expiry removal, temporary alliance-skill black areas, Auto-scan packaged proof for the new kinds,
Ghost plunder (intentionally not enabled), whole-Map completion. Final ZIP path/SHA-256 and extracted-app results are
in the final report and `artifacts/release-map014-*/release-integrity.json` (they cannot be inside a commit that the
EXE's version string must match).
