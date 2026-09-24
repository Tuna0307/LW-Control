# Current project status — strict one-to-one recovery

**Date:** 2026-09-24
**Current checkpoint:** `LWB-R8-014`

## Executive status

The project is not complete.

Previous R7 status pages measured whether the reconstructed Home/Map product worked at its chosen scope. On 2026-09-24 the owner reset the goal to exact LWBridge 0.3.1 parity across the retained program. Account/Login/Authentication and all account-purpose activation, renewal, unbind, logout, entitlement, credential-persistence and account UI/backend surfaces are explicitly excluded from retained scope.

The old acceptance matrix remains useful implementation evidence but is no longer completion authority.

## Reference authority

`C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.1.exe`

Verified SHA-256:

`2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

## Strongest parity already achieved

The recovered frontend is the closest portion to true one-to-one recovery. Original React/Vite chunks, stylesheet, icons and nine locale bundles were extracted, and prior visual comparison showed 31/32 tested feature-region pairs pixel-identical with the remaining pair negligible.

That proof is limited because the rebuild intentionally transformed the main/API boundary and removed/changed some product surfaces.

## Largest parity gaps

1. Full plaintext/original handler recovery from `bridge-scripts.dat`.
2. Exact host/proxy request-result protocol and readiness semantics.
3. Original Map Scan internals and per-kind acquisition strategy.
4. Removal of rebuild-only additions such as Secret Task Quick Find.
5. Restoration of remaining retained product features previously retired/customized. City Excel is restored in R8-007, Map Clear in R8-008, `server_jump` in R8-009, `map_summary` in R8-010, `map_data_options` in R8-011, Manual Scan public contract in R8-012, status/Stop lifecycle in R8-013, and `map_search` public kind/filter ownership in R8-014. Alternate-sort internals remain partial; Auto Scan and Scheduled Plunder remain. Account/Login/Authentication is intentionally excluded.
6. Whole-program backend parity for Automation, Squads/AFK, City Layout, Hotkeys, Mini-games and Settings. City Layout research is now implementation-ready at the host/persistence boundary: its original UI is byte-identical, but all eight production backend handlers are missing; see `docs/reviews/2026-09-24-r8-city-layout-exact-contract.md`.
7. Exact reference-vs-rebuild behavior validation across connected/live states.

## Map status under the new goal

The current Map implementation is a reconstruction, not a recovered copy of the original algorithm. R8-012 closes the Manual `map_scan_start` kind/mode/error/UI boundary; R8-013 closes the shared status/Stop public-state lifecycle; R8-014 closes public `map_search` eight-kind/filter/query-builder ownership and restores the generic keyword predicate. Remaining alternate-sort details and protected acquisition/traversal are still partial/unknown.

The R7-151 wide-FOV work demonstrated why coverage metrics are insufficient: a scan could report full logical coverage while returning only a fraction of Player Cities. The old traversal still found roughly the expected full population.

Therefore no R7 performance optimization is accepted as original parity unless tied to reference evidence.

## Protected package status

Earlier R8 work recovered substantial package/crypto structure, but the remaining LWKE1 field map/AAD is evidence-limited and requires a genuinely new permitted artifact/source. Do not keep repeating the same searches, cross the protected boundary, or expand this lane into Account/Login/Authentication recovery.

The package lane is parked while the main researcher restores Map and other retained product surfaces. It may resume for retained non-account runtime compatibility only when new permitted evidence exists.

## Completion rule

The current whole-program parity matrix is `docs/lwbridge-parity-matrix.md`.

A final release requires every required retained original feature to be classified as exact or proven equivalent, no unexplained rebuild-only deviations in retained scope, and a working current-client product.
