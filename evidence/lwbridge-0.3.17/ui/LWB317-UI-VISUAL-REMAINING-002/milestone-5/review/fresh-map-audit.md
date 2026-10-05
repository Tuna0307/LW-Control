# Fresh Milestone-5 Map audit

Date: 2026-10-05 SGT
Verdict: **NOT READY**

The inherited Map page-local evidence is sufficient for the eight data tabs and their empty/loading/populated/error matrix, Scheduled Plunder, Manual/Auto composition, toolbar/table/pagination structure, and the two bounded Start Scan native-availability differences. The current corrected `MapDataPage.jsx` remains on the accepted `0619C058...A5BD5` source, the task-local read-only pixel replay reports 13 exact historical pairs plus only two Start Scan fences with zero pixels outside them, and the corrected City rejection replay is exact in all three fresh pairs. The current M5 browser result also contains direct App-level City count/page-2 return and Auto/local-retention assertions. I found no new Map renderer correction from those areas.

Three proof fixes remain before this Map packet is ready:

1. **Regenerate the final current-App result and manifest from the current M5 runner, then pass the read-only validator.** The live packet is internally stale. `run-full-app-current.mjs`, `create-manifest.mjs`, and `validate-milestone-5.mjs` were changed after the current `manifest.json` was created. On this audit, `node .../validate-milestone-5.mjs` fails at `served App dependency closure changed`: the stored result/manifest has the older 32-file `sourceFiles` set, while the current dependency-closure collector resolves the complete served App closure (including `index.html`, `main.jsx`, shell helpers, Map helpers, CSS/assets, and other imported modules). Rerun the current final-App producer first, create a new manifest from that exact result and current scripts, then rerun the read-only validator. Do not repair this by manually changing hashes/counts.

2. **Execute Map-summary-before-transition in the final current App instead of proving it only by source text.** The current `run-full-app-current.mjs` has no `map_summary`/summary-ordering observation. `validate-milestone-5.mjs` only finds the `refreshMapSummary().catch(() => {})` line before `startRouteTransition`, and `mutation-check.mjs` only proves that this textual contract is sensitive to deleting the line. The accepted Map-entry runtime packet predates the current App hash, while `map-replay-inventory.md` MAP-J1 explicitly requires the final App to show summary dispatch before transition and non-blocking rejection. Add a task-local inert/preview bridge trace that enters Map from another route and records the summary request before route activation; also prove a rejected summary does not block the transition. Preserve the accepted active-Map re-click/direct-initial-Map boundaries in that current execution proof.

3. **Complete the current-App profile-boundary Map reset/ownership journey.** The current M5 result proves the App-owned Map tab survives profile replacement and one child-local keyword resets. It does not execute the MAP-J3 requirements for a non-first page and other page-local Map state, nor does it demonstrate retirement of old-profile Map request/effect ownership. Start from a populated Map state with page 2 plus non-default query/filter/local mode state, cross the real preview profile boundary, and assert the App-owned data tab remains while child-local page/query/filter/mode state returns to recovered defaults. Instrument the task harness so any old-profile in-flight search/timer/listener owner is retired and exactly one new-profile owner remains.

Validation performed read-only:

- `node .../unit-b/validate-unit-b.mjs` -> `LWB317_VISUAL_FINAL_UNIT_B_VALIDATED`, 10,482 Scheduled renders, 51/51 mutations, 75 integrated cases, 25 mounted cases.
- `python .../milestone-5/replay-map-pixels-read-only.py` -> `LWB317_REMAINING_M5_MAP_PIXELS_READ_ONLY_OK`, 13 exact, 2 bounded Start Scan differences, 0 outside-accepted pixels; historical City error pair explicitly superseded.
- `node .../unit-a/lead-audit/capture-corrected-error.mjs --read-only` -> `CORRECTED_SEARCH_REJECTION_EXACT`, 3/3 pairs, zero issues.
- `node .../milestone-5/mutation-check.mjs` -> `LWB317_REMAINING_M5_MUTATIONS_DETECTED`, all three mutations detected.
- `node .../milestone-5/validate-milestone-5.mjs` -> **FAIL**, `served App dependency closure changed`, before inherited replay acceptance.

## Follow-up — 2026-10-05 SGT

Verdict: **READY**

The three former blockers are closed in the current packet. The regenerated manifest and browser result both pin the complete 67-file served-App dependency closure, and the read-only Milestone-5 validator now passes with `LWB317_REMAINING_M5_VALIDATION_OK`: 67 sources, 213 assertions, 11 decoded PNGs, zero console/page issues, 64 route-transition assertions, and all inherited replay gates green.

The current-App Map-entry runtime replay now executes the final pinned `App.jsx` and passes all four required cases: `map_summary` starts before preload and route transition; summary rejection does not block Map activation; active-Map re-click creates no new summary request; unavailable-provider mode fences summary transport while leaving local navigation available; and a response owned by the obsolete profile stays retired after profile replacement while the replacement profile response becomes authoritative.

The whole-App profile-boundary journey now starts from non-default Map state: Auto mode, `ssr` quality, plunderable-only enabled, a 75-minute profile-scoped Auto interval, a non-default App-owned Map tab, and a keyword. After the uncached profile replacement, the App-owned Map tab remains selected while keyword, scan mode, quality and plunderable state reset to recovered defaults and the replacement profile reads its own 60-minute Auto interval. The separate populated City journey proves 52 total rows, 50 rows on page 1, two rows on page 2, and retained `Page 2 of 2` plus `52 items` across a top-level route hide/return.

Read-only follow-up validation:

- `node .../milestone-5/replay-map-entry-current.mjs` -> `LWB317_REMAINING_M5_MAP_ENTRY_CURRENT_OK`, 4/4 cases passed on App SHA `E4039BEE...B7A93C`.
- `python .../milestone-5/replay-map-pixels-read-only.py` -> `LWB317_REMAINING_M5_MAP_PIXELS_READ_ONLY_OK`, 13 exact historical pairs, 2 bounded Start Scan fences, 0 outside-accepted pixels.
- `node .../milestone-5/mutation-check.mjs` -> `LWB317_REMAINING_M5_MUTATIONS_DETECTED`, 3/3 mutations detected.
- `node .../milestone-5/validate-milestone-5.mjs` -> `LWB317_REMAINING_M5_VALIDATION_OK`, 67 sources / 213 assertions / 11 decoded PNGs / 0 console issues / 64 route-transition assertions.

Remaining fixes: **none for the Milestone-5 Map proof reviewed here.**
