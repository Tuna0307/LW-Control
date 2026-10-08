# Campaign 007 R1 independent lead review — 2026-10-08

Reviewed checkpoint: `57fc69b0219e5c2457ba13d0a24bb7acc3a58de3`.
Decision: **ACCEPT R007-02 and R007-03 for their bounded production/inert/headless scope. R007-01 remains OPEN / READY for further static semantic recovery.**
Overall Home/Map A→A status remains **PARTIAL**.

## Cancellation correction accepted

The production diff is two small changes: a cancellation check immediately after asynchronous capture returns in MapScanEngine, and an exact run/server/status check inside MapStore.StageRecord's existing gate. Stop's durable cancellation and StageRecord use the same store gate in this production owner; a write cannot recreate staging after that same store commits Stop. Completed/failed/cancelled/missing runs cannot accept later staging.

Fresh Release tests execute the actual engine, Map317 sink, control plane and SQLite with controlled providers: cooperative deferred cancellation; late return ignoring cancellation; positive staging followed by Stop; and overlap at publishing. All four pass and preserve the pre-existing published row with zero remaining staged rows. The separate positive-stage storage test, canonical Map campaign, native boundary, full standalone Map checks, profile owner and Home lifecycle pass. These are inert production proofs, not a new live scan or execution of the original protected provider.

The worker's retained baseline reports cancelled/staged=2/published=1; the lead preserved this negative record and reran corrected distinguishing cases, not a second old-source build. The source diff independently supports the reported late-write mechanism.

No additional defect in the reviewed production correction was demonstrated. This acceptance does not claim cross-process multi-writer equivalence or full original scan algorithm parity.

## Integration and metadata correction accepted

The fresh mounted actual App/Home/Map run passes 20 cases with English/light and Japanese/dark: archived actual 8,008-row Resource results, profile A→B→A, old fulfilled/rejected replies while B is selected, recovery and teardown. Transport is explicitly inert. These checks do not establish native/live profile switching or protected original WebView equivalence.

The unchanged worker preservation/closeout validator passes, including six dishonest-claim probes. Only its output destination was redirected into this lead packet; historical inputs and assertions were preserved. It correctly distinguishes one historical 007 launch from zero R1 launches, both numeric query corrections and structural guard tests. Current metadata is useful bounded proof; passing its label checks does not establish complete original semantics.

## Home native recovery remains ready, not externally blocked

The lead independently matched all **129** recorded native slices: five primary bodies, 123 bounded direct callees and one shared large callee, against the original EXE's required SHA-256. This is EXACT_BYTES integrity, not control-flow semantics.

The worker identified useful error/identity/lease locators, but its tools emit sampled instructions, string references, a bounded branch list and direct-call ranges. They do not reconstruct async poll/state-machine paths, indirect continuation calls or duration/retry-table value flow. The worker explicitly acknowledges that available body 0x1D5009–0x1DDBB2 and continuations remain undecoded.

Production OverviewRecoveryPolicy still labels its timeout/retry constants as recovered from 0.3.1. A 0.3.17 contradiction has not been proven, so changing them arbitrarily would be wrong; keeping them does not prove parity either. Further static semantic work is therefore assigned as **HOME-NATIVE-SEMANTICS-008**, not another broad Home/Map restart.

Encrypted original Map controller inputs, original extractor/traversal semantics, native UI comparisons and naturally observable live positive-stage Stop remain separate unresolved dependencies. No new blind live run is justified by this review.

## Fresh lead execution and preservation

- Release Desktop.Checks build: zero warnings/errors.
- Four engine/sink/Stop cases and separate positive-stage cancellation test: pass.
- Map317 native boundary, canonical Map campaign, profile owner, Home lifecycle: pass.
- Complete standalone Map317 Store/Query/Export/Scan/ControlPlane/Action/Worker checks: pass.
- Mounted actual App profile test: 20 cases pass using final numeric-corrected archived payload.
- Canonical frontend check and production-package integrity: pass.
- Worker preservation validator: pass, six metadata probes, output-only redirect.
- Original native slice integrity and historical packet preservation: pass; **77 tracked evidence files unchanged**.
- No lead game launch, desktop input/capture/focus or installed-game mutation. Passive process inventory found no LastWar/LWBridge processes.

See the new lead-r1-review-2026-10-08 packet for fresh JSON and reproducible integrity/validator checks. Worker A/B/C records and all earlier negative witnesses remain unchanged.

## Continuation

One focused solo assignment: `LWB317-FUNCTION-HOME-NATIVE-SEMANTICS-008`.
Recover value/control-flow semantics from available native Home code; compare the original rules with actual current policy; correct only demonstrated differences and preserve accepted cancellation/query/integration fixes. No full Home/Map acceptance or LIVE_PROVEN promotion.
