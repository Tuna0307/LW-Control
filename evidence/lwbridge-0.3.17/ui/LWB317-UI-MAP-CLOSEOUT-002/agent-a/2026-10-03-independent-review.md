# LWB317-UI-MAP-CLOSEOUT-002 — Agent A independent Milestone A review

Date: 2026-10-03

Scope: read-only production review of the four assigned Map UI commits. This note and the executable/result files in this directory are the only files owned by this worker. The review compares every delivery to its own immutable parent and executes the exact original 0.3.17 component where practical. Concurrent parent-worker Milestone B/C edits that appeared in the shared working tree after this review started are excluded from the four commit dispositions.

## Reference identity

- `lwbridge-0.3.17.exe` SHA-256: `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
- `MapDataPanel-B4GXEND2.js` SHA-256: `CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089`.
- `index-BVfnK1wp.js` SHA-256: `44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6`.
- `GameAssetImage-Diy9VTIr.js` SHA-256: `2F92A87C3268497DF6425B1175DB6140E10AABCBDFAE02615F065D00E16458E0`.

`independent-cases.mjs` re-hashes the three source assets before running its cases. The executable hash was independently checked with `Get-FileHash` before the source review.

## Per-unit disposition

### MAP-TREASURE-PICKER-001 — `fbde5d6525fcd46b29307a6cf9e08abbb61e8d96`

Parent: `ff4ed369a132f4b3646188d5afdc81ad2d59383d`.

Recommendation: **ACCEPT** for the assigned source/local UI scope.

Exact original authority is `lt` near UTF-8 byte 29030 and its parent caller near byte 52026 in the pinned Map panel. Independent execution confirms strict key identity (`2` and `"2"` select different rows), the original zero-key truthiness oddity (All and key `0` both receive `active`), missing-key fallback to All, raw-key callback, and successful `onChange` before removing `open`. A throwing callback leaves the details menu open and a null details ref is safe. The parent is independently pinned as a native `<select>` implementation and lacks `MapTreasureTypeFilter`, preserving the pre-fix mismatch rather than relying only on the author's baseline record.

Maintained author check also passes independently on the reviewed production revision:

`node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-TREASURE-PICKER-001/check-picker.mjs`

Result: `LWB317_MAP_TREASURE_PICKER_OK comparisons=28 parent=PASS baselineMismatch=YES`.

No counterexample was found. Native Treasure actions, original protected-runtime pixels and native persistence remain outside this unit's proof.

### MAP-GOODS-PICKER-001 — `315c5a7df6ccf69f30d5f1045274d9789efc1a18`

Parent: `fbde5d6525fcd46b29307a6cf9e08abbb61e8d96`.

Recommendation: **ACCEPT** for the assigned source/local UI scope.

Exact original authority is `ct` near UTF-8 byte 28232 and the parent caller near byte 53802. The unloaded image branch is pinned to `GameAssetImage-Diy9VTIr.js`. Independent cases reproduce numeric/string/zero/missing strict-key behavior, raw-key change-before-close ordering, callback-throw retention and null-ref safety. The original source's zero-key active-class oddity is preserved. The immutable parent still contains the native item-filter `<select>` and has no `MapRetainedGoodsFilter`, providing an independent negative baseline.

Maintained check:

`node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-GOODS-PICKER-001/check-picker.mjs`

Result: `LWB317_MAP_GOODS_PICKER_OK comparisons=144 parent=PASS placeholders=6 baselineMismatch=YES`.

No counterexample was found. Loaded native game icons, original pixels and native scan/game behavior remain unproved by this unit.

### MAP-SCAN-HEADER-001 — `123459d1864f9073b35e12808dcacba2b5588a31`

Parent: `315c5a7df6ccf69f30d5f1045274d9789efc1a18`.

Recommendation: **ACCEPT** for the assigned source/local UI scope.

The pinned original expressions around UTF-8 byte 32960 establish strict stored-run qualification: not reading, non-null stored run, strict server equality, strict run-ID equality and status other than `running`. The page clock is around 33895; header timing is near 44467 and summary status/progress near 48969. Independent cases confirm strict stored server/run mismatch rejection, running-stored rejection, publishing-first status even when not reading, unrounded `99.75%`, provided zero state as `0%`, absent optional state as indeterminate/em dash, and the exact one-second clock boundary. The clock does not advance at 999 ms, advances at 1000 ms, stops after reading ends and remains stopped after unmount.

The immutable parent produces a concrete source mismatch for a stopped publishing state: original/delivery render `common.processing`, server `321`, `99.75%`; the parent renders `common.stopped`, server `321`, indeterminate progress. This preserves a distinguishing parent failure.

Maintained checks:

`node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-SCAN-HEADER-001/check-header.mjs`

Result: `LWB317_MAP_SCAN_HEADER_OK comparisons=270 baselineMismatches=29 clock=6 unmount=PASS`.

`node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-SCAN-HEADER-001/check-fixtures.mjs`

Result: `LWB317_SCAN_HEADER_FIXTURES_OK nativeFences=8 rejected=28`.

No counterexample was found. Native timing/provider delivery, protected-runtime pixels and live scan execution remain outside this proof.

### MAP-REFRESH-FEEDBACK-001 — `f8f3f4e235c938a709e881227490d91351de3136`

Parent: `123459d1864f9073b35e12808dcacba2b5588a31`.

Recommendation: **ACCEPT** for the assigned source/local UI scope.

The exact source policy is pinned at `_e` byte 413, `Fe=1e3` byte 5640, completion/timer effects around 34224/34459, Start at 36787, Export at 41134 and error rendering near 49591. Independent cases verify:

- canceled export is silent and releases its dedicated busy flag;
- two programmatically concurrent exports reproduce the original completion race exactly: the second completion clears busy and shows its message while the first remains pending, then the later first completion overwrites the message;
- a matching non-running stored-run error outranks the live scan-state error;
- a local Start rejection renders as an alert and remains above later stored/live acknowledgements;
- a `readBlocks` change at 999 ms does not postpone the already armed 1000 ms trailing refresh;
- completion at 999 ms emits exactly one immediate row refresh and cancels the trailing timeout;
- a timer-generated table request retired by tab navigation cannot mutate the new tab after a late success, matching original request disposal behavior.

The immutable parent has two independently reproduced failures: successful export returns with no result message where the original reports `map.exportExcelSuccess`, and after entering reading state plus 1000 ms it has row-revision delta `0` while original/delivery have delta `1`.

Maintained checks:

`node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-REFRESH-FEEDBACK-001/check-feedback.mjs`

Result: `LWB317_MAP_REFRESH_FEEDBACK_OK export=45 scan=19 refresh=11 baselineDefects=15`.

`node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-REFRESH-FEEDBACK-001/check-fixtures.mjs`

Result: `LWB317_MAP_FEEDBACK_FIXTURES_OK nativeFences=4 rejected=14`.

No defect attributable to `f8f3f4e` was found in its assigned scope.

One independent trace confirms the already documented **separate summary/options ownership gap** that belongs MAP-CLOSEOUT-002 Milestone B: when the component mounts already reading, original `Vn` starts from `C.isReading` and completion causes one additional original `dataOptions` call. In the `f8f3f4e` local wrapper, `previousReading` is still `false` before that first completion and the options generation remains `1 -> 1` (delta `0`). This behavior predates the feedback commit's assigned broad-options non-goal and is not used to reject MAP-REFRESH-FEEDBACK-001. The parent worker began editing this ownership path concurrently during this audit, so later working-tree changes are intentionally not folded into the Milestone A verdict.

## Maintained regression replay

The following were run against the clean `f8f3f4e` production state before concurrent parent-worker Milestone B/C changes appeared in the shared tree. All exited `0`:

- `node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/check-request-lifetime.mjs` → `LWB317_REQUEST_LIFETIME_OK baselineFailures=0 deliveryFailures=4 currentFailures=0 scenarios=38`.
- `node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/check-navigation-replay.mjs` → `LWB317_NAVIGATION_REPLAY_OK baseline=6 current=0 requests=17`.
- `node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/check-interactions.mjs` → `LWB317_INTERACTIONS PASS scenarios=38 currentMismatches=0 baselineMismatches=31 originalErrors=0`.
- `node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/check-integration.mjs` → `LWB317_INTEGRATION_OK 14 scenarios`.

The four unit checkers and two fixture checkers listed above also exited `0` in the same run.

Independent distinguishing command:

`node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-CLOSEOUT-002/agent-a/independent-cases.mjs --record`

Final result: `LWB317_MAP_CLOSEOUT_AGENT_A_OK treasure=8 goods=8 header=6 export=2 errorPriority=2 refresh=2 disposal=2`.

The first attempted invocation of this new checker stopped before executing cases because its task-owned `mapTablePresentation.js` relative import was one directory short. The path was corrected in this checker only; the final recorded run above is the validation result.

Static validation:

- `node --check evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-CLOSEOUT-002/agent-a/independent-cases.mjs` → exit `0`.
- `git diff --check -- evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-CLOSEOUT-002/agent-a/` → exit `0` before this note was added; rerun at delivery.

## Files and limits

Task-owned files:

- `evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-CLOSEOUT-002/agent-a/independent-cases.mjs`
- `evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-CLOSEOUT-002/agent-a/independent-results.json`
- `evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-CLOSEOUT-002/agent-a/2026-10-03-independent-review.md`

No production/master file was edited by this worker. No Last War process, native scan/export action or protected original post-auth runtime was launched. Source/local component proof does not upgrade native or original-pixel status.

Final Milestone A recommendations: **Treasure ACCEPT; Goods ACCEPT; Scan Header ACCEPT; Refresh Feedback ACCEPT.** No unit-level blockers found. The summary/options mount-reading completion observation remains a separate Milestone B closeout item.
