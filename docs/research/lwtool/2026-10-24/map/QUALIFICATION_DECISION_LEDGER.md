# Map qualification and method decision ledger — 2026-10-24

Scope: sanitized reconciliation of the current Map assignment against tracked source, accepted lead audits, deterministic production-path checks and retained accepted receipts. This ledger does not promote an inert check into a live result and does not treat an older nine-kind package as proof of today's ten-kind combined selection.

Current checkout at review time: `codex/lwtool-full-improvement`. The working tree contains the current ten-kind qualification harness and Map staging changes, so a final source-pinned package remains a separate evidence gate.

## Status vocabulary

- **ACCEPTED LIVE** — genuine native/current-client evidence was accepted in the tracked MAP audits.
- **ACCEPTED PACKAGE (historical source)** — a genuine source-pinned Windows package was accepted for that behavior on an earlier Map source. It remains evidence of the capability, but it does not certify the current dirty worktree.
- **QUALIFIED DETERMINISTIC** — the production path is exercised by controlled/inert checks with distinguishing failure inverses. This is not a new live-game observation.
- **PENDING CURRENT PACKAGE** — current live evidence is complete for this ledger, but the final source-pinned package still needs the stated genuine receipt.
- **REJECTED / DEFERRED** — a tested alternative lost accuracy/performance or lacks the source/correlation needed for promotion.

## Current Map kinds

The production contract has ten explicit kinds: `city`, `resource`, `monster`, `truck`, `railway`, `dispatch`, `ghost`, `treasure`, `other`, and `zombie_boss` (`src/LWBridge.Map-0.3.17/MapContracts.cs`). The implicit/default selection intentionally remains the original eight kinds; `other` and `zombie_boss` require explicit selection. Auto also preserves that compatibility rule.

| Kind | Qualification already supported | Method/decision | Current remaining gate |
|---|---|---|---|
| City | **ACCEPTED LIVE.** MAP-019 same-session current-vs-camera comparison matched 4,395 rows with 0–2 cross-method identity drift and complete field comparison. Historical packaged scans/browse also passed. | Retain the current camera-free point census, atomic publication and SQL browse contract. | No City-specific live rerun merely for coverage. Final packaged all-ten qualification still includes City. |
| Resource | **ACCEPTED LIVE.** MAP-019 current-vs-camera identity differences stayed within observed time-separated drift; prior source/freshness and package filter checks passed. | Retain full census plus fail-closed dynamic hydration. | No Resource-specific live rerun. Final package persistence sanity remains pending. |
| Monster | **ACCEPTED LIVE.** MAP-019 current-vs-camera comparison was equal within moving-world drift. | Retain current native point source and detail handling. | No kind-specific rerun unless the source changes. |
| Zombie Boss | **ACCEPTED LIVE.** Populated remote-server comparisons matched within drift. The MAP-019 D1/D2 fix also accepted a genuinely verified empty home-server result after complete source coverage instead of forcing a camera failure. | Retain the dedicated verified source. Retain verified-empty acceptance only where the source-alive/coverage proof is present. | No new Boss-only run required for the present batching change. Do not generalize its verified-empty rule to unrelated kinds without source proof. |
| Dispatch / Secret Task | **ACCEPTED LIVE.** MAP-019 compared the production Dispatch/Secret Task population against camera and found equivalent identities; historical package/filter/name evidence also exists. | Retain the complete native census/publication path rather than the competitor's filtered high-value subset. | No kind-specific rerun. |
| Ghost | **ACCEPTED LIVE.** MAP-019 current-vs-camera comparison was equal within drift; historical packaged reward/filter rendering was accepted. | Retain the current native source and authoritative stored reward filters. | No new populated Ghost is required merely to satisfy the current task. A genuinely empty Ghost-only run may still take the camera fallback; do not present that as the Zombie-Boss empty optimization. |
| Treasure | **ACCEPTED LIVE** for scanning/comparison and **QUALIFIED DETERMINISTIC** for read-only inspection/preview boundaries. | Retain the independent Treasure source. Inspection stays read-only; claim execution is outside this Map qualification. | Seasonal/current availability must not be manufactured. No live claim is required or implied. |
| Railway | **ACCEPTED LIVE.** MAP-019 found the current 625-rectangle source returned more trains than the camera control in the measured session; MAP-016 also accepted a genuine remote positive and exact return. | Retain the typed 625-rectangle sweep with bounded repair and exact cleanup. | No Railway-specific live rerun. Final packaged all-ten/Auto evidence remains pending. |
| Truck | **ACCEPTED LIVE.** MAP-019 and MAP-015 establish the typed 625-rectangle source, current-vs-camera advantage, arrival filtering and repair behavior. Historical packaged Truck-only/filter and mid-flight Stop evidence exists. | Retain 20 requests/s plus bounded missing-rectangle repair; camera remains the guarded fallback when source admission fails. | **PENDING CURRENT PACKAGE** Stop replay because the current staging sink changed. Do not infer that the old package alone certifies the new staging transaction boundary. |
| Other | **ACCEPTED LIVE.** Current-source same-session ABBA at `artifacts/map014-lead-live/native-benchmark-review-20261024-025620/` completed direct **16.918 / 16.606 s** versus camera **122.780 / 122.466 s**, with **4 rows in every round** and exact cleanup. The deterministic classification/persistence/filter/migration/inspection checks also remain valid. | Retain `MapOtherEventScanSource` / shared source. Only source-confirmed `event:27000` and canonical positive `supplies:<n>` subtypes are classified; unknown names remain unknown. | No Other-specific live rerun is required for the current source. Final packaged all-ten evidence remains pending. |

## Manual and Auto scanning

| Surface | Supported state | Qualification / decision | Current remaining gate |
|---|---|---|---|
| Manual individual selections | **ACCEPTED LIVE / QUALIFIED DETERMINISTIC**, depending on kind as above. Selection validation, duplicate-start fencing and selected-kind publication are exercised through the production command/state/store path; Other now has its own current-source ABBA witness. | Retain one exact run owner, selected-kind atomic replacement and previously published unrelated kinds. | No blanket rerun of all ten singles. |
| Manual combined historical all-nine | **ACCEPTED PACKAGE (historical source).** MAP-015/MAP-017 genuine packaged all-nine scans completed and browsed the nine then-supported combined kinds (all current kinds except Other). | Keep as historical capability evidence only. | Must not be relabeled all-ten. |
| Manual all-ten | **ACCEPTED LIVE for the current source.** After restoring production to **20 requests/s with fence25**, the corrected direct-only run `artifacts/map014-lead-live/native-benchmark-review-20261024-031605/` completed in **101.706 / 82.686 s**. The final same-session ABBA `artifacts/map014-lead-live/native-benchmark-review-20261024-032125/` completed direct **90.713 / 91.327 s** versus camera **142.766 / 142.512 s**; every round was **2500/2500 with zero failures**. Mean direct **91.020 s** versus camera **142.639 s** is **36.19% lower wall time**. | Retain the exact ten-kind selection and current staging/publication path. The final ABBA populated City, Resource, Monster, Truck, Railway, Dispatch, Treasure and Other; Ghost and Zombie Boss were genuinely empty at that availability point and must not be turned into positive-population claims. | **PENDING CURRENT PACKAGE only:** genuine packaged Manual all-ten remains required. |
| Auto one-off / schedule | **ACCEPTED PACKAGE (historical source)** for genuine all-nine remote Run Now, server return and scheduler behavior; **QUALIFIED DETERMINISTIC** for empty selection, schedule-off one-off, no unwanted repeat, config persistence and disable/cancel ordering. | Retain exact owned-run Stop-before-return ordering and saved Auto config semantics. | **PENDING CURRENT PACKAGE:** genuine `auto-all-ten` remote Run Now containing Other and exact return on the final current package. |

The current qualification harness already contains `all-ten` and `auto-all-ten` modes in `tools/map_014_lead_native/run_ui.py` and `tools/map_015_package_ui.py`, with fail-closed unit coverage. That implementation support is not itself a live/package pass.

## Browse, persistence and read-only operations

| Behavior | Status | Evidence boundary / decision | Current remaining gate |
|---|---|---|---|
| Search, kind filters, compound filters, sorting and paging | **ACCEPTED PACKAGE (historical source) / QUALIFIED DETERMINISTIC.** Production `map_search` uses validated kind-specific filters and snapshot count + LIMIT/OFFSET paging; default page size 50, capped at 200. Historical real UI/package receipts exercised representative Resource, Zombie Boss, Ghost and Truck filters. | Retain SQL-backed snapshot paging and exact filter semantics. | No fresh live gate unless these paths are edited. |
| Saved-server selection | **ACCEPTED PACKAGE (historical source).** Remote saved rows were browsed after return; saved servers derive from published data instead of fabricated empty scans. | Retain published-data semantics. | Final current-package persistence sanity below. |
| Clear selected server | **ACCEPTED PACKAGE (historical source).** MAP-016 accepted genuine remote Clear/reopen behavior. | Retain atomic server snapshot clear. | Replay one final persistence/clear scenario on current package because staging transactions changed. |
| Clear all servers | **QUALIFIED DETERMINISTIC / existing mounted qualification.** Production store clears a consistent all-server snapshot and refuses unsafe overlap with active reading/ownership. | Retain all-server atomic clear; reject per-table clearing that creates mixed-age state. | No separate live destructive proof is required. |
| City XLSX export | **QUALIFIED DETERMINISTIC / existing production audit.** Export reads one database snapshot even while another connection rewrites rows; prior MapStore reopen/query/export audit passed. | Retain City-only export and snapshot consistency. | No new package run unless export code changes. |
| Read-only Inspect / preview | **QUALIFIED DETERMINISTIC** and supported by historical UI evidence. Treasure/Other state inspection is source/identity-fenced and does not produce claims/rewards. | Keep inspection separate from gameplay execution. | No live claim/action positive is required. |
| Coordinate Jump | **ACCEPTED PACKAGE (historical source) / QUALIFIED DETERMINISTIC.** Real remote row Jump was accepted; controlled production action checks enforce reached-view acknowledgment, inclusive coordinates 0–999 and overlap fencing. | Retain passive navigation only. | No new live Jump gate unless navigation code changes. |
| Truck Follow / Stop / expiry | **ACCEPTED LIVE / historical package.** Same-UUID movement, multiple positions/camera tiles, manual Stop and natural-arrival automatic Stop with no late Follow requests were accepted. | Retain bounded passive follow and retirement on server/scan/arrival/error change. | No repeat Follow gate from current batching alone. Final scan Stop replay remains pending separately. |
| Transactional staging | **QUALIFIED DETERMINISTIC + CURRENT LIVE PUBLICATION.** The inert matched benchmark staged 24,362 deterministic records with median 672.559 ms versus 2483.671 ms for the old per-record path, preserving canonical hashes/row counts and cancellation/invalid-input inverses. The final current-source all-ten ABBA then exercised genuine **2500/2500, zero-failure** publication through the current path. | Retain batched SQLite staging with the same exact-run semantics. The inert benchmark remains local persistence evidence; the live all-ten run closes only the production-publication linkage. | **PENDING CURRENT PACKAGE:** one packaged mid-flight Stop must still bind late-cancel semantics to the final executable. |

## Cancellation, freshness, profile and repeated-run boundaries

These are **QUALIFIED DETERMINISTIC** production-path properties and are deliberately not additional live-failure gates for this assignment:

- acquisition/hydration/publication cancellation discards unpublished staging while preserving the last completed publication;
- a late source/progressive callback after Stop cannot publish into the retired run;
- missing or mismatched native reply/correlation evidence fails closed rather than being assigned to a run;
- duplicate Start is rejected while the owned scan lease is active;
- rapid profile/runtime changes are generation-fenced, so retired callbacks/status updates cannot mutate the replacement profile;
- Auto Stop targets the exact owned run before any remote-server return; a successor/owner mismatch is refused;
- a schedule-off one-off does not replay later or after reopen;
- repeated successful scans replace only the selected kinds while unrelated previously published kinds remain intact;
- a failed or disconnected repeated scan retires its staging and leaves the previous complete publication intact.

Primary distinguishing checks include `MapManualScanDeliveryChecks`, `Map014SharedAcquisitionChecks`, `Map014OtherEventChecks`, `MapAutoScanR1StopOrderingChecks`, `Map017ScanControlChecks` and the profile-generation integration checks in the Desktop suite. Existing accepted qualification records that the full Desktop checks, canonical UI checks and 49 Lua suites have passed. A new live failure should be manufactured only if later source changes create a new causal question.

## Current 2026-10-24 live qualification

The current live matrix is now complete for the previously open Other/all-ten benchmark boundary:

- `artifacts/map014-lead-live/native-benchmark-review-20261024-025620/` is a **split receipt**. Its Other ABBA is valid and matched 4 rows in all four rounds. Its all-ten “direct” rounds are **invalid negative controls** for performance because a temporary, unqualified fence10 production edit made Railway fail closed and forced camera fallback (**213.118 / 198.370 s**). Exact cleanup still passed. Audit: `artifacts/map019/20261024-other-allten-broken-fence-audit.json`.
- Production was restored to the qualified **20 requests/s, fence25** Truck/Railway settings. `artifacts/map014-lead-live/native-benchmark-review-20261024-031605/` then produced two genuine direct all-ten completions at **101.706 / 82.686 s**, 2500/2500 with zero failures. Audit: `artifacts/map019/20261024-allten-corrected-audit.json`.
- `artifacts/map014-lead-live/native-benchmark-review-20261024-032125/` is the decisive current-source same-session all-ten ABBA: direct **90.713 / 91.327 s**, camera **142.766 / 142.512 s**, all four rounds 2500/2500 with zero failures. Audit `artifacts/map019/20261024-allten-final-abba-audit.json` is non-provisional after final exact Stop, original-script restoration and zero external account actions.

These receipts close the current live Other/all-ten benchmark gate. They do not certify a final Windows package, populated Ghost/Zombie Boss availability, or a globally optimal method.

## Server context and restoration

Remote full-map acquisition currently uses guarded switch → scan → return because the admitted current parser drops ordinary off-server cache ingestion. Genuine MAP-016/017 Auto and remote proofs accepted the switch/scan/return behavior and original-server restoration. The MAP-019 source review found no accuracy-qualified direct remote full-census replacement.

Decision: **RETAIN** server switching with exact return/restoration. Direct remote-without-switch remains **REJECTED AS CURRENTLY SUPPORTED**, not proven impossible in every hypothetical protocol implementation.

The final `auto-all-ten` package must re-establish exact original-server return for the current source. That one scenario is sufficient; do not add a redundant remote diagnostic all-ten run if it already proves the same boundary.

## Home regression boundary

Home has independent finite-scope acceptance recorded in `docs/reviews/2026-10-23-HOME-FINAL-ACCEPTANCE.md`. Existing Map-era Desktop checks include the accepted Home invariants, but the current assignment explicitly requires one bounded genuine integration smoke after the final Map integration.

**PENDING CURRENT PACKAGE:** on the final source-pinned package, prove at least two authenticated same-game recoveries, then production Stop during reconnect with exact owned-process exit, no successor, exact original-script restoration and no recovery journal.

## LwTool live comparison boundary

LwTool Quick Map was **not measured live** in this qualification. Recovered client behavior makes the quick route a **Premium-only** entitlement, while the safely readable local session metadata reports a cached plan of **free**. No protected session decryption, entitlement bypass or license manipulation was attempted. The absence of a live LwTool timing is therefore an entitlement boundary, not evidence that Quick Map is slow or nonfunctional.

## Package, privacy and cleanup ledger

Historical MAP-015/016/017 packages prove that source-pinned Windows packaging, Map scanning/browsing, remote return, Stop, filters and Clear/reopen have worked on accepted earlier sources. They are not the final package for this worktree.

The final package remains **PENDING CURRENT PACKAGE** until all of the following are in one coherent source-pinned checkpoint:

1. clean pushed exact branch HEAD and a fresh win-x64 framework-dependent publish;
2. package receipt plus reopened ZIP identity, per-entry hashes and canonical production UI/source/helper identity;
3. privacy gates rejecting retained test/account identifiers, credentials, recovered competitor source/binaries and private evidence from the package;
4. genuine packaged Manual all-ten and Auto all-ten (Other included), current-stage Stop, and saved-data Clear/reopen sanity;
5. required Home connect/reconnect/Stop smoke;
6. exact task-owned process exit, original installed scripts restored byte-for-byte, no recovery journal and no task-owned target process left running.

No current final ZIP/hash is claimed by this ledger.

## Method decisions carried into the final qualification

| Candidate | Decision | Supported reason |
|---|---|---|
| Current camera-free point census (160 AOIs/request, serial reply admission) | **RETAIN** | MAP-019 measured equivalent accuracy to the camera control for point kinds with large wall-time reduction. It is the best accuracy-qualified route among the tested current alternatives, not a proof of global optimality. |
| Current typed Truck/Railway rectangle sweep | **RETAIN** | Accepted live evidence finds at least camera-equivalent and often larger moving-object populations, with typed fields, bounded repair and exact cleanup. Current production is restored to the qualified **20 requests/s, fence25** settings; the temporary fence10 experiment is rejected. |
| Smaller 80-area point requests | **REJECT** | Same City population but ~87% slower than the 160-area route in MAP-019. |
| Requests above the admitted 160-area shape | **REJECT AS TESTED** | Earlier tests returned insufficient source data; do not increase the batch merely from arithmetic. |
| Coarse LOD2 Zombie Boss map | **REJECT** | Faster in the bounded test but missed roughly 60–75% of bosses. |
| 15 Truck requests/s instead of 20/s | **REJECT** | Not faster and did not improve measured population. |
| Polling-style multiple point requests in flight | **REJECT AS TESTED** | Lost rows/points. |
| Immutable reply-time correlated parallel capture | **DEFER** | A distinct hypothesis, but no accepted repeated coverage/field comparison exists yet. |
| Game nearest-search/list APIs as a census | **REJECT AS REPLACEMENT** | Source review found nearest/recommendation/alliance/self semantics, not a complete all-world list. They remain complementary. |
| Incremental updates as the only full refresh | **DEFER / REJECT AS FULL CENSUS** | Create/change/remove messages exist, but no global revision/replay-gap guarantee is established. |
| Direct remote full census without context switch | **REJECT AS CURRENTLY SUPPORTED** | Current parser/source path does not admit the required foreign population with field provenance. |

## Final unresolved qualification set

The remaining concrete evidence work is finite:

1. fresh source-pinned final Windows package;
2. packaged Manual all-ten and Auto all-ten with Other and exact return;
3. packaged mid-flight Stop against current batched staging;
4. packaged saved-data Clear/reopen persistence sanity;
5. final-package Home connect/reconnect/Stop integration smoke;
6. package identity/hash/privacy and final cleanup receipt.

Everything else above is either already supported by accepted evidence, deterministically qualified, or deliberately rejected/deferred. Passing these remaining gates would support a reviewable current candidate; it would not prove a globally fastest scanner or competitor-wide superiority.
