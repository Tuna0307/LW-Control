# Map Manual header offline visual comparison

COMPLETE / ACCEPTED as a comparison packet after project-lead takeover.
The implementation is not fully equivalent: recorded differences remain.
No production code changed in this task.

## Results

The actual original MapDataPanel asset body and actual current MapDataPage JSX
were executed with persistent mocked hooks and inert producers. Twelve core
comparisons cover six state families in EN/JA, plus three provider-fence cases.
No complete raw markup/view pair matches; all differences remain in the results.

Four isolated browser pairs use original CSS on the original and current
reference.css then styles.css in production import order on the clone. Three
PNG pairs are byte-identical. All measured anchor rectangles match strictly;
the unavailable Start control differs in opacity and cursor. Console issues: 0.
All eight saved images were inspected by the lead and regenerated hashes match.
Narrow captures measure 375x1000 CSS pixels; desktop captures measure 1280x720.

MMV-BEHAVIOR-001 is a real open UI behavior defect: the original permits the
last Manual type to be unchecked; current JSX and updateSelectedTypes prevent
it. Independent inert callback execution reproduces [] versus [city]. A focused
correction must inspect both guards and subsequent empty-selection edits, while
preserving the separate source-backed Auto type rule and native action fences.

Other recorded differences are provider/Auto fences, explicit action button
types, scan-type label markup and host metadata. No measured visual effect is
attributable to the last three in the samples. Provider fences are retained;
Auto-running predicate equivalence is not upgraded by this packet.

## Reproduce from repository root

```powershell
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-MANUAL-VISUAL-001/compare-renderers.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-MANUAL-VISUAL-001/check-selection-counter-evidence.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-MANUAL-VISUAL-001/capture-browser.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-MANUAL-VISUAL-001/validate-evidence.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-UI-HOME-PREFERENCE-LIFETIME-001B/check-protected-wip.mjs
npm.cmd --prefix src/LWBridge.UI-0.3.17 run check
npm.cmd --prefix src/LWBridge.UI-0.3.17 run check:production-build
```

capture-browser.mjs requires free ports 4338/4339 and installed Chrome at its
recorded path. It launches only an owned headless browser/profile, waits for
the exact document and fonts, and cleans its own server/browser/temp profile.
Owner port 4335 is preserved. It never clicks Start/Stop/Clear.

Historical evidence is immutable. record-integrity.mjs creates the final
manifest after an intentional evidence update; do not rerun it to conceal a
hash failure. validate-evidence.mjs independently recomputes browser geometry
and style differences, verifies raw HTML association, source byte slices,
reused harness/current imports, screenshots and JSON hashes.
This packet's .gitattributes preserves its pinned bytes across Git checkouts.

## Limits and continuation

This is isolated offline source-rendered proof. The wider shell, full Map,
protected original runtime and live/native actions are outside this acceptance.
Current EN/JA catalogs are injected into both renderers. Clocks/hooks and all
producers are substituted as described in pinned-inputs.json. Online inputs
are inert samples, not LIVE_PROVEN sessions. Error comparison exercises
scanState.lastError with role=status; local action scanError/role=alert was
not exercised and broader error parity is not established.

Next: a bounded Manual scan-type correction for MMV-BEHAVIOR-001, then continue
the remaining Map/page visual inventory. Overall UI status remains PARTIAL.
Review: docs/reviews/2026-10-04-LWB317-UI-MAP-MANUAL-VISUAL-001.md.
