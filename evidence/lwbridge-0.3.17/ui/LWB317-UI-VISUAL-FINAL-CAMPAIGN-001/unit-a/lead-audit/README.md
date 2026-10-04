# Independent lead audit of Units A–B, 2026-10-05

This packet preserves all submitted worker files. Its scripts never rewrite the old
validators, input manifests, comparison results, screenshots or passing markers.
`MapDataPage.baseline.jsx` is the exact current production file before the correction.

## Findings and correction

The two Treasure direct-text changes and three Automation `is-enabled` changes are
source-backed. Original strings were independently recovered from all nine locale
asset object literals and the main asset's shared-error spread: **12,447/12,447
values exactly equal current catalogs**. Sharing the current translator therefore
does not conceal a catalog defect at this pinned checkpoint.

The worker A validator rewrites its dependency manifest and reads stored pixel/count
claims. The B validator also trusts stored claims. The B pixel checker masks a large
downstream panel region in `city-error-en-dark`. Actual original search rejection
only clears rows/total and logs; the clone added a visible banner. The original
catch is **EXACT_BYTES**, MapDataPanel byte **39114+93**, pinned in
`source-search-catch.json`. The immutable submitted screenshot differs by **99,918
pixels** outside any unavailable native action control. No panel/text/asset region
is accepted as a mask here.

Production correction removes only `setQueryError(errorText(error))` in the ordinary
search rejection callback. Generation ownership, rows, totals, loading, filters and
other operation feedback remain unchanged. The current renderer was re-executed
across all **75** source/current cases into `corrected-renderer/`, with original
submitted outputs untouched. Three fresh error pairs (EN/dark desktop, JA/dark
desktop, JA/light 375px) have **byte-identical screenshots**, identical panel/search/
table geometry, and no visible query banner, without masks. Zero browser issues.

## Independent replay and actual coverage

- Submitted decoded PNGs: 28 pairs recomputed. A's four Treasure pairs differ only
  inside two individually evidenced native claim-button boxes. Eight action-message
  pairs are exact. B's 13 exact pairs stay exact; its two Scheduled fences differ
  only inside the exact Start Scan button. The immutable City failure remains a
  distinguishing positive control; three corrected pairs now pass unmasked.
- Fresh browser measurement covers every element descendant, its geometry, value,
  checked/disabled attributes, computed styles and both pseudo-elements in all 28
  submitted pairs. A differences are confined to unavailable BUTTON styles/states.
  B raw structural differences remain explicit (Manual label spans, accessibility
  attributes, and synthetic native availability). Counts are not a claim that every
  descendant matches; index alignment diverges after a structural insertion.
- Independent original Scheduled replay passes **10,482 renders, 61 source fixture
  branches and 51/51 mutations**, zero React warnings. Command:
  `node unit-b/check-scheduled-current.mjs` from the campaign root.
- C's accepted source overlap replay independently passed 186 comparisons. This
  does **not** close C's full visual task: current-only browser images and current
  card class mutation controls do not replace original/current whole-card pairs.

## Read-only verification

From repository root:

```text
node evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-a/lead-audit/validate-read-only.mjs
```

This verifies frozen input hashes, decodes the submitted PNGs afresh, launches a
fresh task-owned headless browser to remeasure all descendants, rerenders the
corrected source-generated error HTML into three unmasked screenshot pairs in
memory, and independently compares original locale values. It writes no files.
`check-pixels.py` alone intentionally exits 1 because it reproduces the immutable
City defect. The parent validator requires that exact positive-control failure,
then requires the corrected pairs to pass.

`freeze-inputs.mjs` is an explicitly named checkpoint writer, never called by the
validator. It pins a conservative dependency superset: complete canonical UI src,
original assets, actual historical oracle/harness trees, submitted A/B inputs,
package locks, material tools, and this audit's inputs/scripts. A future source
change invalidates the snapshot; review and produce a new snapshot deliberately.

No native/gameplay/original protected runtime is exercised. This packet closes
bounded source/local evidence gaps; lead global/campaign acceptance remains separate.
