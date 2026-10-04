# Independent lead review — Map toolbar visual delivery

Decision: **CHANGES_REQUIRED**, 2026-10-04.
Reviewed delivery: `24ea7b910e4909404c012857ea52158e46a8cf5a`.
This supersedes the worker's assertion of zero unresolved presentation defects.

## R1 — Treasure checkbox text has the wrong color

Current `MapDataPage.jsx` lines1127–1128 wraps the two Treasure checkbox labels
in spans. Original `MapDataPanel-B4GXEND2.js` renders direct text after the input:
`children:[jsx(input,...),S(key)]`. The original key locators are UTF-8 bytes
52280 (`map.showForeignRadarTreasures`) and 52480
(`map.prioritizeLuckyTreasures`). Current key offsets are 54712 and54944.
The recovered stylesheet has
`.map-counters span,.map-filter-field span{color:var(--muted);font-size:var(--font-body)}`.
Thus these wrappers are not presentation-equivalent: original EN/light text is
rgb(29,29,31), current span text is rgb(95,95,99). Label container and text
rectangles remain equal, explaining why the worker's anchor-only comparison missed it.

Independent PNG decoding confirms 1,734 changed pixels outside both disabled
claim-button rectangles: 1,021 in the foreign label and713 in the lucky label.
Total Treasure changed pixels:9,984. Truck5,875 and Dispatch22,462 changed pixels
are entirely inside their unavailable-action rectangles; those fences are intentional.
The durable lead `check-pixel-fences.py` reproduces the counterexample and fails
normal zero-unclassified-pixels acceptance. `--expect-submitted-defect` explicitly
checks the historical defect; its success is not a parity pass.

Required correction: remove only the two Treasure label span wrappers to recover
direct text. Preserve input values/attributes, callbacks/page resets and native
fences. Preserve all submitted evidence as the pre-correction baseline. Rerender
and recapture affected pairs under a new R1 packet; require zero differing pixels
outside explicitly identified availability boxes. Measure descendant text colors,
not only containing labels. Check EN/JA light/dark for these labels.

## R2 — evidence closeout needs stronger validation

The submitted final validator verifies many saved hashes, but trusts classification
markers/counts and does not recompute pixel-mask or full measured differences.
Pin and validate the actual imported harness/source dependency closure, reference
asset hashes/slices, task scripts, relevant locale values and tooling versions.
Dependencies currently missing from the final closure include original common/
runtime/lib/semantics, the two persistent hook harnesses, GameAssetImage and actual
Map helper imports. Do not rewrite historical manifests to repair this.

Add explicit action-message presentation coverage. The46 toolbar cases include
query errors, but no populated action-message renderer case. The inherited38-case
interaction suite proves behavior, not full message presentation. Use actual
source renderers with disclosed visual-state fixture injection for original
`message`/current `actionMessage`, EN/JA empty/populated Dispatch/Ghost states;
never invoke a disabled/live action merely to populate the label.

## Positive findings and actual verification

Independent source/AST review passed14 assertions: .map-search nesting, integer
result count, recovered plunderable class and unchanged Pagination function.
Original search/class/count locators:50270/54096/56215; grid CSS locator73331.
All three production changes are source-backed. No new callback or provider
contract defect was found. Existing query-error/missing-server/accessibility and
unavailable-provider differences remain disclosed product contracts.

The evidence reviewer reran all46 renderer cases and six paired Chrome captures
with record writes intercepted in memory. Fresh case records, comparison records
and screenshot hashes exactly reproduced the submitted evidence. This confirms
the discrepancy is reproducible, rather than a stale capture.

The lead independently reran the actual mounted EN/light and JA/dark interaction
suite on assistant-owned Vite port4346:34 assertions, two fresh screenshots,
zero console/page errors. Both saved images were visually inspected. The replay
adapter changes only output paths and port; it preserves the worker suite's actual
UI interactions. The assistant-owned service was stopped; owner4335 untouched.

Lead canonical check, fresh build and package integrity pass. Current package:
`a75a9f2ebd959b5ab8057a5eb40072cbf5d9429408b14d6a8ff39b46705bece0` /
`f69c4f2efcd8668f1fc56af160d0f23ebc156d3f828d282d8ee41319d6ae4d8e`.
Request lifetime38, interaction differential38, Treasure216+384+8 and row-action160
replays pass. Worker saved validator still passes; its false-negative limitation
is specifically demonstrated above. Protected-WIP guard10/10 passes.

Evidence: `evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-MAP-TOOLBAR-VISUAL-001/`.
No production code was changed by this lead audit. Required R1 is the first unit
of the owner-requested larger visual finish campaign. Full Map/global/native
runtime parity is not accepted by this review.
