# Map Data — strict parity status

Current 0.3.17 lead checkpoint, 2026-10-06: recovered-source/local Map UI is accepted; native E-I integration remains **PARTIAL / CHANGES_REQUIRED**. Frozen WIP now has context rehydration, atomic Auto jump/start lease, controlled scheduler loop and expanded canonical workbook tests. Focused checks pass, but Auto cancellation can stop a replacement Manual run; frontend snapshot ordering/native deadline, service/profile/server/DTO and genuine desktop proof remain. Preserve historical browse semantics and unavailable execution fences. Continue FUNCTION-HOME-MAP-CAMPAIGN-001-RECOVERY-001; see the dated lead review. Earlier client/native status entries below are historical, not fresh live campaign validation.


**Current through:** `LWB-R8-097`, 2026-09-27.

This page supersedes the former performance-oriented Map status. Map Data is now judged only against the original LWBridge 0.3.1 behavior.

## Direction correction

Do not continue designing a new scanner.

Do not use LW Atlas as product authority.

Do not promote a Last War-native API, manager, finder, wide-FOV trick, no-jump route, or other optimization merely because it is faster.

Recover the original LWBridge Map implementation first. Current-game APIs are allowed only as compatibility mechanisms for reproducing that recovered behavior.

R8-065/R8-066 proved that the equivalent current-client scanner can operate live, including the production UI path. Under R8-097, Map is **NOT WORKING** for owner acceptance because the recovered original LWBridge acquisition engine is not yet the production path. The equivalent scanner is evidence/comparison tooling only and must not be used as a fallback.

## What is actually exact today

| Area | Parity classification | Note |
|---|---|---|
| Original Map frontend component family | EXACT_BYTES-derived | Original chunks/assets recovered, but rebuild transformations/customizations remain |
| `startMapScan` request fields | EXACT_CONTRACT | `scanRunId/serverId/worldId/scanMode/concurrency/selectedTypes/tileWidth/tileHeight` |
| Accepted gate / 5 s bridge-call behavior | EXACT_CONTRACT | Recovered from original host |
| Selected type allowlist | EXACT_CONTRACT | city/resource/monster/truck/railway/dispatch/ghost/treasure |
| Normal/Fast concurrency | EXACT_CONTRACT | normal=8, fast=20 |
| `map_scan_clear` | EXACT_CONTRACT + EQUIVALENT_REIMPLEMENTATION | R8-008 restores active-scan precedence, positive current-live-server gate, server-scoped deletion, player-mark preservation and Manual-only frontend Clear |
| `server_jump` public result | EXACT_CONTRACT + EQUIVALENT_REIMPLEMENTATION | R8-009 restores destination `serverId` with `previousServerId` and `changed`; protected travel internals and exact timeout duration remain unresolved |
| `map_summary` | EXACT_CONTRACT + EQUIVALENT_REIMPLEMENTATION | R8-010 restores exact `{serverId, counts, scanState}`, eight original count kinds, shared-state ownership and run-scoped staging vs published counts; scanState serialization remains separate |
| Native capture vocabulary/hooks | PARTIAL EXACT_CONTRACT | Recovered from original proxy |
| Query/storage/filter contracts | PARTIAL EXACT_CONTRACT | Significant original SQL/normalization recovered |
| Current-client Manual Normal/Fast operation | NOT WORKING / historical LIVE-PROVEN EQUIVALENT_REIMPLEMENTATION | R8-065 completed same-session Normal and Fast all-eight scans at 2500/2500, 0 failed, 0 unread, with persisted results surviving database reopen |
| Original acquisition algorithms | UNKNOWN | Current production scanner is our reconstruction |

## R8-065 current-v21 live compatibility proof

The fresh live gate first failed on server 2212 because current-v21 returned a contiguous 3×9 AOI footprint where the conservative scanner still required exactly ten rows. R8-065 adds a bounded compatibility rule: accept only contiguous rectangular 9- or 10-row footprints, preserve the existing 2-5-column bound, and when only the band's bottom AOI remains missing, issue one 10-tile vertical cleanup request and union the result. Exact 10,000-AOI coverage remains mandatory. The removed R7-151 wide-FOV shortcut was not restored.

Final same-session live proof:

- Normal: 2500/2500 read, 0 failed, 0 unread.
- Fast: 2500/2500 read, 0 failed, 0 unread.
- Fast published counts matched after database close/reopen.
- Proof exited with code 0.

See `docs/reviews/2026-09-26-r8-065-live-home-map-v21.md` and `evidence/lwbridge-implementation/2026-09-26-r8-065-live-home-map-v21.json`.

## R8-066 production desktop/WebView live proof

R8-066 raises the live acceptance level from direct service proof to the actual user-facing production path. A new proof mode keeps the normal persistent profile, Home lifecycle service, recovered Map Data WebView and production `ManualMapScanCommandService` wired exactly as the normal Release app does. It drives the rendered Resource checkbox and Start Reading button, waits for the real production scan, triggers the rendered Resource Search control, correlates the native `map_search` result to the DOM table, captures a screenshot, and then stops the owned game.

The final current-v21 run reached `connected`, completed a Fast Resource scan on server 2212 at 2500/2500 with 0 failed and 0 unread, returned 515 Resource search rows, and rendered the correlated first row in the current six-column Resource table. The desktop proof exited 0 and both Last War and LWBridge were absent afterward.

While building the gate, failure evidence showed the existing proof matcher was stale: current Resource rows contain six cells because a resource-amount column now precedes status and Updated At. The matcher and passive owner-evidence correlation now accept both the legacy five-cell and current six-cell shapes without weakening coordinate/level/timestamp correlation.

See `docs/reviews/2026-09-26-r8-066-production-map-ui-live.md` and `evidence/lwbridge-implementation/2026-09-26-r8-066-production-map-ui-live.json`.

## Known deviations

- R7-151 Secret Task Quick Find was a rebuild-only product feature.
- R7-151 wide-FOV/68-request scan strategy was our optimization, not a recovered original algorithm.
- Direct Train-list/no-jump routing is a current-game optimization until reference evidence proves original equivalence.
- City Excel export was previously removed despite being part of the original product; R8-007 restores its recovered original API/UI, dialog, pagination, workbook and result contract.
- Scheduled Plunder was removed in R7 despite being part of the original product. R8-016 restores its recoverable control plane and original result-tab UI; the protected runtime remains a separate evidence-bound lane.
- R8-017 removes R7 guesses for Monster distance, City shield and Railway quality sorting; the original UI controls remain, but those branches now fail closed until exact native expressions are recovered.
- Manual/Auto UX and other owner workflow changes made during R7 must be audited against the reference rather than retained automatically.

## Completeness regression lesson

The wide-FOV work could report complete logical coverage while returning only tens of Player Cities where the older complete traversal returned thousands. This proves that our own coverage model is not a substitute for original behavior or semantic completeness.

The old complete traversal may remain only as an isolated comparison oracle/test harness while original Map code is recovered. It must not execute as a production fallback, and its success never changes Map from NOT WORKING.

## Per-kind parity status

| Kind | Current implementation status | Parity status |
|---|---|---|
| City | Working reconstruction; full population possible with older traversal | Original acquisition UNKNOWN |
| Resource | Working reconstruction | Original acquisition UNKNOWN |
| Monster / Doom Walker | Working reconstruction | Original acquisition UNKNOWN |
| Zombie Boss | Working dedicated reconstruction | Original acquisition UNKNOWN |
| Truck | Working direct-list reconstruction | Original source/semantics UNKNOWN |
| Railway | Working direct-list reconstruction | Original source/semantics UNKNOWN |
| Dispatch / Secret Task | Current R7 optimization regressed semantic completeness in broader use | Original acquisition UNKNOWN |
| Ghost Ops | Working/partially population-gated reconstruction | Original acquisition UNKNOWN |
| Treasure / Supplies | Strong partial contract recovery | Protected/orchestration pieces UNKNOWN |

## Immediate P0

1. Recover the original `XluaBridgeMapScanTick` traversal/work engine: traversal/order/coordinates, queue/work scheduling, request pacing, retry/backoff, drop/removal handling and exact completion.
2. Recover exact host/proxy scan control and result grammar.
3. Identify original acquisition path for every selected type.
4. Restore original Map UI/features previously removed.
5. Remove every rebuild-only Map feature.
6. Compare original and rebuild results/identities/errors/defaults.
7. Only then consider invisible performance work that preserves exact parity.

Historical R7 performance and acceptance data remains evidence, not current product authority.

## 0.3.17 UI navigation checkpoint — 2026-10-02

Historical submission (now accepted after correction by PM-026):
LWB317-UI-MAP-NAVIGATION-001 was AWAITING_REVIEW for a bounded source/local UI
correction. Recovered 0.3.17 behavior keeps a component-local normal-tab cache of
page/rows/total. Leaving a normal tab stores its current view; returning restores
that view and then performs a fresh search. An uncached tab starts at page 1 with
empty rows/loading, while clicking the current tab is a no-op.

The production page now also matches the recovered query-generation boundary:
obsolete search success, failure and finally callbacks cannot alter the active
tab. Data-server change or loss clears all cached tab views. Scheduled Plunder
does not participate in the normal cache/search/pagination flow and is handled
only as an entry/exit navigation boundary in this work item.

Evidence under LWB317-UI-MAP-NAVIGATION-001 records exact 0.3.17 byte locators,
an immutable failing baseline, persistent production callback/effect tests and
real offline preview navigation at server 321 with populated Truck rows.
Scheduling, keyword/debounce recovery, selection/plunder lifecycle, native
Treasure wiring and original post-auth pixels remain separate scopes.

## 0.3.17 Map interactions checkpoint — 2026-10-02

LWB317-UI-MAP-INTERACTIONS-001 is COMPLETE / ACCEPTED for focused source/local UI scope by PM-026. Source-recovered (`MapDataPanel-B4GXEND2.js`) behavior now
implemented in `src/LWBridge.UI-0.3.17/src/MapDataPage.jsx`, `mapInteractions.js`, `ScheduledPlunder.jsx`,
`mapPlunderPresentation.js` and the offline fixtures in `mapPlunderFixtures.js`:

* Search: keyword (raw, untrimmed, shared by tabs) is absent from the effect dependencies (34816-34869) and there is
  no timer; Search at page 1 searches directly (54355-54377); the query uses the text currently typed.
* Names: selecting clears the keyword and resets the page (51184-51243); typing drops the name (50967-51031);
  Resource/Monster independent; option text from game text or raw key.
* Selection: Dispatch/Ghost map reset by every tab change (34121-34182); Truck map persistent; keys/payload
  (39593-39866); membership uses a trimmed key (17072-17832).
* Delay and actions: raw string default `0`, valid when a safe non-negative integer (32925-32959); schedule/share/truck
  predicates (55466-56076).
* Scheduled Plunder: `ot` (20712) and `st` (24391) tables, status/result chains, error translators, counts, loading
  on mount/events/entry.

Provider-dependent operations stay fenced; the job producer and its row schema are UNKNOWN. Differences not
reproduced (extra search after each options reply, scan-time row refresh, hidden search errors, `serverId:0` search,
other filters' refresh validation) are listed in
`docs/reviews/2026-10-02-LWB317-UI-MAP-INTERACTIONS-001.md`.
