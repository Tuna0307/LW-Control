# Map benchmark evidence summary — 2026-10-24

Scope: sanitized summary of accepted performance/accuracy evidence relevant to the current LwTool comparison assignment. Numbers below come from tracked accepted MAP reports and the completed 2026-10-24 current-source live review; raw account rows, native UUIDs, process dumps and private runtime evidence remain outside Git.

## What the accepted benchmark actually compares

MAP-019 compared LW-Control's current current-client acquisition routes against the older camera-assisted route inside owned same-game sessions. Accuracy was judged with typed identity sets, stored fields and same-method drift controls, not row count alone. Moving/spawning/expiring records can legitimately differ between sequential runs, so a cross-method difference was interpreted against same-method drift and explicit lifetime fields.

The accepted timer is the scan measurement recorded by the MAP-019 harness through terminal production publication. It is not a claim about login, game startup, package startup or a competitor's UI timer. Exact network bytes were not measured.

## Accepted current vs camera measurements

Source: `docs/MAP_019_SCAN_METHOD_REVIEW.md`. “Current” below is LW-Control's accuracy-qualified camera-free/current-client path for that kind; “Camera” is the retained older camera-assisted control.

| Workload / server context | Current | Camera | Population/accuracy observation | Accepted conclusion |
|---|---:|---:|---|---|
| City, home | 20.8–23.5 s | 196.9–199.1 s | 4,395 / 4,395; 0–2 cross-method identity diff | Equivalent within drift; roughly 9× faster in these trials. |
| Resource, home | 21.9–22.6 s | 195.9–198.8 s | 7,996–7,999 / 8,001–8,004; time-separated drift explains the larger pair differences | Equivalent within observed drift. |
| Monster, home | 24.0 s | 201.0–201.3 s | 9,583–9,595 / 9,605–9,642; moving-world differences within controls | Equivalent within drift. |
| Dispatch / Secret Task, home | 16.9 s | 188.9 s | 349 / 349; cross diff 2 | Equivalent; roughly 11× faster in this trial. |
| Ghost, home | 16.6–16.9 s | 191.4 s | 208–209 / 209; cross diff 0–1 | Equivalent within drift. |
| Treasure, home | 16.5–16.9 s | 193.7 s | 24–25 / 25; cross diff 0–1 | Equivalent within drift. |
| Railway, home | 60.4–61.2 s | 204.6 s | 4 / 2 | Current source found more trains in the measured session. |
| Truck, home | 52.2–79.0 s | 197.9 s | 196 / 74; moving-object temporal analysis favors the current source | Current typed rectangle source found materially more Trucks than the camera route. |
| Zombie Boss, remote 2180 | 54.7–55.3 s | 199.0 s | 220–226 / 225 | Equivalent within drift. |
| Zombie Boss, remote 2198 | 54.0–55.0 s | 197.5 s | 278–286 / 279 | Equivalent within drift. |
| Zombie Boss, remote 2203 | 33.5–36.9 s | 197.9 s | 121–127 / 124 | Equivalent within drift. |
| Zombie Boss, remote 2175 | 31.8–35.4 s | 213.6–213.7 s | 90–103 / 93 | Equivalent within the larger observed drift control. |

Fields were audited as well as identities. Accepted MAP-019 reports City name/level/owner/alliance/protection/shield parity, with naturally changing health; moving Monster positions, Treasure remaining-box state, Secret Task completion and Zombie Boss protection timing were treated as time-varying rather than forced equal.

## Accepted combined-workload observations

| Workload | Measurement | What it establishes |
|---|---|---|
| City + Resource, home | 25.0–27.4 s | Shared current acquisition can serve a mixed point selection without summing the two single-kind wall times. This is an observation, not an all-kind result. |
| Historical all-nine, remote 2175 | 114.0–120.5 s | Genuine combined current path completed City, Resource, Monster, Zombie Boss, Truck, Railway, Dispatch, Ghost and Treasure in one run. Other was not part of this historical all-nine contract. |
| Historical all-nine, home before empty-Boss fix | failed after 306–315 s; nothing published | Exposed the D2 atomic failure when the Boss source could not admit a verified empty result. This negative remains part of the evidence. |
| Historical all-nine, home after D1/D2 fix | 83.9 / 84.1 s; all nine published, 0 Bosses | Confirms the verified-empty Boss correction and successful combined publication on that source. It still excludes Other. |

The all-nine label in these accepted measurements must not be rewritten as “all supported kinds.” The current product has ten explicit kinds because Other is now an independent kind.

## Verified-empty defect correction

MAP-019 originally observed a home-server Zombie Boss scan with no bosses fall through to the slow camera route and fail after about 224–225 s. The same condition made all-nine fail after about five minutes and publish nothing.

The accepted D1/D2 correction admits zero Zombie Boss rows only after the full source coverage/aliveness conditions prove that zero is meaningful:

- Zombie Boss-only home: **20.4 / 20.5 s, 0 rows**, with the camera control also empty;
- all-nine home: **83.9 / 84.1 s, all selected kinds published, 0 Boss rows**;
- populated remote Boss behavior remained intact (2203: 34.3 s, 124 rows in the cited post-fix check).

This is a source-specific verified-empty rule. A different genuinely empty kind, such as a Ghost-only scan, can still take the camera fallback before returning zero. No broader empty-source speed claim is supported.

## Alternative-method evidence and decisions

| Candidate | Accepted observation | Decision |
|---|---|---|
| 80 areas/request instead of 160 | 41.1–42.9 s vs 22.3–22.6 s for the same 4,395 Cities | **Reject**: ~87% slower in the matched case. |
| 320/640 areas/request | Earlier tests returned almost no useful population; 160 is the admitted working shape in current evidence | **Reject as tested.** |
| Coarse LOD2 Zombie Boss | 13.7–14.4 s but only 27 Bosses vs 90–103 on the complete current route | **Reject**: speed came with roughly 60–75% population loss. |
| Truck 15 requests/s | 48.5–48.8 s, 179–193 Trucks vs 20/s at 38.7–47.6 s, 180–192 Trucks | **Keep 20/s**: lower rate was not faster and did not improve population. |
| Multiple point requests in flight with polling | Prior attempts lost about half / 7% of points depending on variant | **Reject as tested.** |
| Immutable reply-time correlated parallel capture | Not yet accepted with repeated exact coverage/field parity | **Defer** as a distinct future experiment; do not inherit the failed polling result or claim a win. |
| Game nearest-search/list APIs | Source review shows nearest/recommendation/self/alliance semantics rather than a complete unfiltered census | **Complement only**, not a scanner replacement. |
| Incremental create/change/remove updates | Real update messages exist, but no global revision cursor/replay-gap guarantee was established | **Do not use as the sole full refresh.** |
| Direct off-server census without switching | Current parser/source path drops the needed foreign population before normal cache ingestion | **Keep switch → scan → return** for the supported production path. |

MAP-019 therefore supports retaining the current acquisition methods among the alternatives actually qualified. It does not establish a globally optimal scanner.

## Why LwTool's “few seconds” claim is not a matched benchmark

Recovered shipped-client evidence shows that LwTool's quick Map button uses a materially different boundary:

- the local game must be connected, but quick Map submits the selected server to LwTool's own scanner service rather than locally walking the game map;
- the recovered UI describes that service as using separate accounts / multiple IPs in parallel;
- the client submits one selected server and streams completed server chunks back into its in-memory result table;
- quick Map skips the normal local `get.user.info.multi` hydration pass because name/alliance/power/troop-power fields arrive in the remote payload (with limited local cache supplementation);
- the displayed quick timer starts inside the background job after initial UI/RPC setup and ends at the quick-scan completion event, before later UI paint cost;
- identical submissions can share/replay a client-side in-memory submit result for roughly the first 30 seconds;
- the shipped client exposes an incomplete-server state, but the remote service's exact AOI traversal, account concurrency, cache lifetime, source freshness and “freshly scanned every tile” behavior are not present in recovered client code.

For adjacent quick kinds, completion boundaries are not uniform: recovered Secret Task/Ghost code can continue reward/detail or owner-name hydration after the table is already visible. Alliance Secret uses a different game-backed route with fixed waits rather than the generic quick endpoint.

Consequently, no accepted evidence supports the statement “LwTool freshly performs the same full ten-kind, field-complete, durable work in a few seconds.” It also would be incorrect to conclude that the remote service cannot do so. The server implementation and its cache/freshness policy are unknown from the supplied client artifacts.

The fair comparison implication is to compare equivalent selected kinds, coverage, fields, source freshness, cache state and completion boundaries. A remote cached/parallel service can be useful, but its UI timer is not directly comparable to LW-Control's terminal native acquisition + hydration + durable publication measurement without those controls.

No live LwTool Quick Map timing was obtained in this qualification. The recovered quick route is **Premium-only**, while safely readable local session metadata reports the cached plan as **free**. No protected-session decryption, entitlement bypass or license manipulation was attempted. This means the LwTool live side is **unmeasured**, not failed; the source-derived timer/cache/completion caveats above remain the comparison boundary.

## Current-source 2026-10-24 benchmark completion

The previously open Other and true all-ten current-source benchmark gates are now closed by genuine owned live evidence.

### Other ABBA

`artifacts/map014-lead-live/native-benchmark-review-20261024-025620/` completed the current-source Other sequence:

| Round | Method | Time | Rows |
|---|---|---:|---:|
| A1 | direct | 16.918 s | 4 |
| B1 | camera | 122.780 s | 4 |
| B2 | camera | 122.466 s | 4 |
| A2 | direct | 16.606 s | 4 |

All four rounds completed 2500/2500 with zero failures and exact cleanup. The same owned run also attempted all-ten, but those two requested direct rounds are **invalid negative controls**: a temporary unqualified fence10 production edit made Railway fail closed, so the effective method was camera fallback (**213.118 / 198.370 s**). They are retained as failure evidence and excluded from direct all-ten performance claims. Audit: `artifacts/map019/20261024-other-allten-broken-fence-audit.json`.

Production was then restored to the qualified **20 requests/s with fence25** setting.

### Corrected all-ten direct and final ABBA

`artifacts/map014-lead-live/native-benchmark-review-20261024-031605/` provided the corrected direct-only control after restoring production: **101.706 / 82.686 s**, both 2500/2500 with zero failures and genuine direct evidence. Audit: `artifacts/map019/20261024-allten-corrected-audit.json`.

The decisive matched result is `artifacts/map014-lead-live/native-benchmark-review-20261024-032125/`:

| Round | Method | Time | Completion |
|---|---|---:|---|
| A1 | direct | 90.713 s | 2500/2500, 0 failed |
| B1 | camera | 142.766 s | 2500/2500, 0 failed |
| B2 | camera | 142.512 s | 2500/2500, 0 failed |
| A2 | direct | 91.327 s | 2500/2500, 0 failed |

Mean direct wall time is **91.020 s** versus **142.639 s** for camera, or **36.19% lower wall time** in this same-session ABBA. City, Resource, Monster, Truck, Railway, Dispatch, Treasure and Other were populated; Ghost and Zombie Boss were empty at that availability point. The latter are valid observed empty availability, not positive-population witnesses. Audit `artifacts/map019/20261024-allten-final-abba-audit.json` has `provisionalUntilFinalStop=false`, exact Stop, original-script restoration and zero external account actions.

This closes the current-source all-ten completion/timing gap and ties the present publication path to genuine 2500/2500 zero-failure scans. It does not replace final packaged Manual/Auto all-ten, packaged Stop, persistence or Home evidence.

### Transactional staging evidence

The inert transactional-staging benchmark in `artifacts/map019/staging-benchmark/README.md` remains useful for isolating the local persistence component. It uses the same deterministic 24,362-record workload in fresh SQLite databases:

| Local persistence method | Matched runs | Median staging time | Semantic result |
|---|---:|---:|---|
| Old per-record `StageRecord` path | 6 | 2483.671 ms | Reference staged/published row counts and canonical hashes |
| Current batched `StageRecords` path | 6 | 672.559 ms | Same row counts and canonical staged/published hashes in every run; staging empty after successful publication |

The candidate-only semantic pass additionally rejects a foreign-server batch, invalid JSON before any write and a late batch after cancellation, and accepts a valid zero-record verified-empty publication. This inert result still supports only the local SQLite bottleneck claim; the live ABBA above separately supplies the genuine current-source publication evidence.

## Final package evidence still pending

Benchmark acceptance alone does not close the assignment. Current-source final evidence still needs:

1. a clean source-pinned Windows package with reopened ZIP identity/hashes and privacy gates;
2. genuine packaged Manual all-ten, browsable with Other present;
3. genuine packaged Auto all-ten on the approved remote context with exact original-server return;
4. one mid-flight packaged Stop proving the current staging transaction cannot publish late rows;
5. one saved-data Clear/reopen persistence sanity after the staging change;
6. the required final-package Home connect/reconnect/Stop smoke;
7. exact final cleanup: owned processes ended, original scripts restored and no recovery journal.

Historical all-nine, Truck Stop, remote Auto/return, filters and Clear/reopen remain valid capability evidence, but they are not substituted for these current-source final-package receipts.

## Evidence anchors

- `docs/MAP_019_SCAN_METHOD_REVIEW.md` — accepted live timings, typed identity/field comparisons, D1/D2 correction and alternative-method decisions.
- `docs/MAP_019_SOURCE_ROUTES.md` — current shipped-source alternatives and their exact decision boundaries.
- `docs/reviews/2026-10-16-MAP-015-LEAD-AUDIT.md` — accepted camera-free/Truck/package and historical all-nine evidence.
- `docs/reviews/2026-10-17-MAP-016-LEAD-AUDIT.md` — accepted Railway, remote return, saved Clear and Auto behavior.
- `docs/reviews/2026-10-17-MAP-017-LEAD-AUDIT.md` — accepted owner-scope scanning/browse/filter/Jump/Clear/one-off controls.
- `docs/research/lwtool/2026-10-24/map/QUALIFICATION_REMAINING_GAPS.md` — reconciliation of the remaining final package/Home gates after current live qualification.
- `docs/research/lwtool/2026-10-24/map/SCAN_METHOD_REPORT.md` — canonical sanitized recovered LwTool quick-scan mechanics, timer/cache/completion boundaries, provenance and fair-comparison limits.
- `artifacts/map019/staging-benchmark/README.md` — inert matched old-vs-batched staging timings and semantic-equivalence boundary.
- `artifacts/map014-lead-live/native-benchmark-review-20261024-025620/` and `artifacts/map019/20261024-other-allten-broken-fence-audit.json` — valid Other ABBA plus retained invalid fence10 all-ten negative control.
- `artifacts/map014-lead-live/native-benchmark-review-20261024-031605/` and `artifacts/map019/20261024-allten-corrected-audit.json` — corrected direct-only all-ten after restoring 20 req/s fence25.
- `artifacts/map014-lead-live/native-benchmark-review-20261024-032125/` and `artifacts/map019/20261024-allten-final-abba-audit.json` — final current-source all-ten ABBA and exact cleanup.

These anchors support the claims above without publishing proprietary recovered source, raw account rows or private runtime identifiers.
