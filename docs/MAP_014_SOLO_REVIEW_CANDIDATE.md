# MAP-014 — solo takeover evidence and lead-review candidate

Status: **READY_FOR_LEAD_FUNCTIONAL_REVIEW for evidenced, available functions;
overall PARTIAL for source/event-dependent acceptance and scanner optimization.** See
[`MAP_014_COVERAGE_TRACKING_CONTINUATION.md`](MAP_014_COVERAGE_TRACKING_CONTINUATION.md)
for post-lead City identity comparisons, sustained live moving Truck tracking,
acquisition failure causes and explicit remaining gates. The repository
remains on `codex/map-owner-fixes-014`, the existing PR is #10. This document
records what the real native game and packaged app proved, what only controlled
tests proved, and what still lacks a live witness. It never changes the old
negative receipts or declares the City census complete.

## Owner issue matrix (source and genuine limits)

| # | Owner issue | Implementation and meaningful inverse | Genuine result / limit |
| --- | --- | --- | --- |
| 1 | Camera distortion | Standard Unity projection is restored with `ResetProjectionMatrix`; custom projection restoration stays conditional. Exact lens/transform Lua inverses. | Independent real camera snapshots before/after the complete City and mixed scans matched. **New active Stop checkpoint** `native-stop-20261013-210642` reached 6.5% acquisition then idle with all captured camera anchors/FOV/aspect/zoom/projection/position/rotation identical; exact Stop and scripts restored. |
| 2 | City completeness | Full 2,500-region shared discovery and identity rejection. **Post-review fix:** native current `_pointInfos` City indices must each match the exact retained `GetAllMainBaseList` index set in the accepted AOI; duplicate retained indices and every native/managed rejection counter fail closed. Inert count-equal substitution inverse passes. | Oct13 City-only 4,029 and packaged 4,032 with shared index/position consistency. **New genuine** `native-causal-20261013-223148` City-rich region had 62 current source indices, 62 retained/delivered identities, zero rejected fields, fresh AOI and restored camera. All 62 already existed in the saved Oct13 DB; **no independent missing current target found in this region**. The historical 7,060-range layout is uncorrelated, never a quota; total recall remains unknown. |
| 3 | Truck warzone flicker | Source-scoped observed warzone overlay survives a transient failed/unknown poll but accepts an authoritative empty list; retiring owner cannot leak choices. | Positive matching warzones retained through 48 genuine packaged UI samples; controlled transient/empty inverses pass. |
| 4 | Stored remote Jump | Saved browsing server owns its search/options/counts independently of the live scanner; idle summaries and Auto return cannot force home. Coordinate Jump switches to the row's server in a serialized action lease. | **One complete packaged Auto cycle** at `artifacts/map014-takeover/native-ui-20261013-125036`: genuine 2213 Resource 7,996 rows, returned to 2212, browsed retained 2213 data, exact-session native Jump to **2213 (987,900)**. All gates passed with no mocked rows. |
| 5 | Moving Follow/Stop, Doom Walker | Truck/Train and source-verified moving BossVO use march UUID; passive Follow retires on Stop, expiry, profile/server change, scan, tab change and disappearance. | `native-ui-20261013-135941` proved **five fresh exact-owned same-UUID Truck positions, four distinct camera tiles** and actual UI Stop. Both mounted expiry/retirement inverses pass. Three bounded new expiry-only native attempts got genuine saved upcoming Truck candidates, but **no uninterrupted ready session through arrival with automatic UI Stop** was proven; errors, recovered scripts and exact limits in the new continuation. Positive moving BossVO row remains unavailable. |
| 6 | Secret Task owner name | Exact same-server City UID/name enrichment, unknown UID left unknown (never fabricated); native game-name lookup. | `native-ui-20261013-130149` passed a new genuine native Dispatch query: **50 of 50** returned rows have nonblank owner names, 1,005 total retained tasks. Earlier native tab timeouts are preserved as genuine negatives, not overwritten. |
| 7 | Resource filters | Min/max level, full/partial/unknown amount and black/non-black/unknown tile status carried through production bridge into SQL; genuine unknowns are not coerced to full/non-black. | Positive packaged five/full/normal query returned 1,190 matching Resources; native and controlled nonmatching/unknown inverses pass. |
| 8 | Escort power | Input `25` means inclusive maximum 25,000,000 native power units. | Genuine two-item/25-million combined request returned **46** matching Truck rows in final packaged browse run (previous genuine run: 72); greater/unknown values rejected by SQL and controlled tests. |
| 9 | Truck rewards ALL | Multiple item identities are ANDed before pagination; single-item mode retained. | Final positive genuine **46-row** combined query, with each returned row carrying both selected keys; earlier 72-row run and controlled 255-record inverse also pass. |
| 10 | Real item images | Genuine game `game_asset_image` requests are owner-scoped and cached per native reader; table and filter menu use actual image paths. | `native-ui-20261013-130149` checks actual nonzero `naturalWidth` for game-owned reward PNGs in **both the table and filter menu** in the packaged WebView. No synthetic icons bundled. |
| 11 | Auto Clear Map Data | Auto can explicitly clear a published saved remote dataset after returning home, after a fresh live-owner read and an idle scan/Auto gate. Legacy ordinary Clear remains strictly same-live-server; profile stores and other server rows are preserved. | Same continuous real Auto run at `native-ui-20261013-125036`: UI Clear on 2213 left **zero** 2213 Resources and unchanged **7,975** Resources on 2212; exact owned Stop and original scripts restored. |
| 12 | Scattered Supplies | Existing Treasure/WorldSuppliesPoint acquisition uses `pointType=27`, `suppliesType=1` and its exact game names. False kinds and conflicting types are rejected. | Canonical native/SQL source inverses pass. No active seasonal Scattered Supplies row exists in the admitted live captures; positive seasonal acquisition remains an availability dependency. |
| 13 | Celebration Party | Treasure `eventId` resolves against `DetectEventTemplateManager` when that Treasure actually exists, but no proven point/config mapping identifies the owner's Celebration Party object. Existing game/event localization candidates cannot serve as a discriminator. No collection/claim action is used. | **Unidentified/unavailable.** Need an actual event-spawned point, authoritative current-game class, event ID and label before supported acquisition or a positive row can be claimed. |

## Scanner alternatives and acceptance

The production scanner remains a shared, camera-assisted, serial LOD0 region
sweep with explicit per-region home restoration. Mixed kinds share a single
discovery pass followed by kind-specific enrichment. Normal/Fast controls are
unchanged.

| Route | Evidence / classification |
| --- | --- |
| A. Corrected serial home-restored | **Interim accuracy-qualified.** Genuine City and mixed 2,500-block runs completed with no failed blocks and independent matching camera snapshots. They did encounter internal retry/rejection events, so block completion is not proof of complete City recall. |
| B. A→B→C→D, final restoration only | **Rejected for observed populated second region.** The new post-camera-correction source-correlated sequence in `artifacts/map-013-live/native-causal-20261013-194843` independently proved baseline regions (32, 30, 27 records); the retained-camera first region returned 32, but second region had no qualifying fresh native reply/cache and failed safe. The attempt exited through exact owned Stop with original scripts restored. The remaining regions were not falsely accepted. |
| C. Zoom/FOV/viewport expansion | `coverage` uses standard-projection FOV 120 with a bounded aspect expansion, full camera restoration, and native response freshness checks. **New native experiment** `artifacts/map-013-live/native-causal-20261013-210353`: first control 31 records, second coverage 30, then ZoomMax second region produced **zero qualified records** with `map013_native_reply_or_cache_timeout`. The three-pair schedule stopped at first native accuracy failure by design. Exact owned Stop/restoration passed. ZoomMax remains disabled; no speed claim. |
| D. Direct AOI/list/search/known-object | Historical camera-free AOI failed fresh target-region reply; per-kind explicit lists and known-object reads are **complementary only**, not evidence of a complete mixed population or replacement for the shared discovery. Previous negative and new reply/cache evidence are preserved in MAP-013. |
| E. Remote context | Real Auto with an authenticated server-view switch scanned 2213 and returned to 2212 with a source-matched published dataset. A full remote no-switch acquisition remains **unqualified**; server IDs echoed in outbound requests cannot replace independent native world/LOD/bounds/row provenance. |

Measured real serial controls, **not paired comparative speed claims**:

| Selection | 2,500 completed blocks | Observed total |
| --- | ---: | ---: |
| City alone, native diagnostic | 2,500 / 2,500, failed 0 | 147.4 seconds; 4,029 Cities |
| City+Resource+Monster+Truck+Railway+Dispatch, native diagnostic | 2,500 / 2,500, failed 0 | 177.6 seconds; Cities 4,030, Resources 7,985, Monsters 9,078, Trucks 277, Railway 1, Dispatch 1,089 |

These are two separate genuine runs with moving populations, not three
alternating paired observations. No median, spread, speedup or global optimum
can legitimately be inferred. The observed source can distinguish target
response versus return-home restoration (`NativePhaseSamples` and
`NativeTargetResponseMilliseconds`/`NativeHomeRestorationMilliseconds`);
these timing counters are meaningful only when their matching native replies,
unique objects, completeness and restored camera are independently accepted.
A fast zero-row candidate is rejected.

## Verification at solo handoff

The default Release Desktop/Home/Map/Auto command suite passed (including the
MAP014 SQLite, owner, clear, stale reply and negative cases). The controlled
MAP008 scanner planner passed **255 selections, 15 adverse cases**; these are
not performance or genuine-object witnesses. Mounted MAP011 saved-server and
MAP014 filter/Follow/Clear UI suites passed, including both EN/light 1280 and
JA/dark 375. The MAP013 source-locked observer tests passed **8/8** after
pinning the digest of the current corrected camera source. Two Lua
projection-restoration inverses and independent Lua 5.3 syntax compilation of
the production probe, Overview bridge and trace source passed.

At the final handoff attempt, an additional full `npm.cmd run check` was
**blocked before execution by the tool safety gate**. It must not be silently
counted as a pass, and its rejection was not retried or rerouted. Earlier
full-frontend checks and the current separately completed mounted tests are
distinct evidence. Canonical Release/ProductionUi build, local ZIP and package
identity checks must be reported separately against the eventual source commit.

## Evidence locations

- Original takeover and historical negatives:
  `docs/MAP_014_LEAD_TAKEOVER.md`,
  `docs/MAP_014_OWNER_HANDOFF.md`,
  `docs/MAP_013_NATIVE_CAUSAL_TRACE_CONTINUATION.md`.
- Actual diagnostic City/mixed camera and scan reports:
  `artifacts/map014-lead-live/native-city-20261013-180338` and
  `artifacts/map014-lead-live/native-mixed-20261013-180909`.
- Genuine packaged resource/six-kind datasets and earlier positive
  warzone/Dispatch evidence: `artifacts/map014-lead-ui/native-20261013-104453`
  and `native-20261013-110718`.
- Saved-server negative-to-positive progression:
  `artifacts/map014-takeover/native-ui-20261013-121630` (successful genuine
  scan/return, wrong tab in test driver),
  `native-ui-20261013-123802` (positive native row Jump, stale UI getter in
  test driver), and `native-ui-20261013-124843` (browse/Jump/Auto Clear all
  passed using preserved genuine 2212+2213 data, no new full scan).
- Continuous unmocked packaged Auto 2213 scan→2212 home→saved source search→
  native 2213/(987,900) Jump→home→Auto Clear with another 2212 server preserved:
  `artifacts/map014-takeover/native-ui-20261013-125036`.
  `native-ui-map.json` records `state=proven` and one new remote scan;
  `owned-exit.json` reports exit 0, no recovery journal, and all three
  installed scripts restored to the admitted original hashes.
- Final strengthened packaged genuine browse proof, with **zero** new scans:
  `artifacts/map014-takeover/native-ui-20261013-130149`.
  Positive Dispatch names 50/50, Resource 1,190 and Truck 46 matched rows,
  exact native Follow and UI Stop, table/filter-menu original item images,
  stable warzone overlay. Exit 0 and clean scripts/journal.
- Actual Stop at ~6.5% acquisition with independent identical full camera
  before/after checkpoints: `artifacts/map014-lead-live/native-stop-20261013-210642`;
  no retained partial scan was published, Stop settled idle.
- Post-camera-correction consecutive control and failed next-region attempt:
  `artifacts/map-013-live/native-causal-20261013-194843`.
  Source-backed ZoomMax native reply failure:
  `artifacts/map-013-live/native-causal-20261013-210353`.
- Source tests: `scripts/check-map011-saved-owner.mjs`,
  `scripts/check-map014-mounted.mjs`,
  `tests/LWBridge.Desktop.Checks/Map014OwnerFixChecks.cs`.

Raw acquisition databases and screenshots are intentionally ignored, private
evidence on the same controlled machine. They are not embedded in the shipped
binary or committed to Git. A review outside this workspace needs fresh
authorized native sessions. No claims, attacks, marches, paid operations,
collection or cross-owner window actions were performed.
