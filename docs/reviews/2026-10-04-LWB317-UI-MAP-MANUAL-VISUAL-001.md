# LWB317-UI-MAP-MANUAL-VISUAL-001 — lead takeover and review

COMPLETE / ACCEPTED for the bounded offline comparison packet. The worker saved
renderers/results and four browser pairs but had not committed or completed the
integrity/documentation closeout. The lead reviewed its source extraction,
reran actual comparisons/browser/package checks, added counter-evidence and
finished the packet. No production code changed. Overall UI remains PARTIAL.

## Findings

Reference: LWBridge 0.3.17 EXE SHA-256
4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783.
Original MapDataPanel-B4GXEND2.js SHA-256
CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089.
Full locators and exact slice hashes are in the packet's pinned-inputs.json and
selection-counter-evidence.json; byte offsets below are UTF-8 offsets.

| Finding | Original locator | Current locator | Disposition |
|---|---|---|---|
| Last selected Manual type is disabled and retained | Manual checkbox byte50063; actual onChange byte50135 directly filters to empty | MapDataPage.jsx byte51702/line1085; mapBackend.js updateSelectedTypes byte7345 | Real open UI defect. Inert callback proof: original [] vs current [city]. Fix both guards in a separate focused unit. |
| Provider/Auto fences alter Manual disabled states | Original Start/Stop/Clear bytes45657/45777/45892 and speed fragment45106 | MapDataPage.jsx lines997,1002-1004 | Recorded implementation differences; retain provider fences. Auto predicate equivalence not established by this task. |
| Current action buttons add explicit type | Same three original action slices | Same three current action lines | Raw DOM difference, no sampled visual effect; no correction justified here. |
| Scan labels add span and omit empty class | Original label50000/input50063 | MapDataPage.jsx lines1085-1086 | Raw DOM difference, no sampled geometry/style effect. |
| Host data-bridge-mode metadata | Original root43855 | MapDataPage.jsx byte43806/line978 | Retained host adaptation; no sampled visual effect. |

Twelve core comparisons and three provider cases execute exact original body
and actual current JSX. Complete raw DOM/view parity is 0/12 because recorded
metadata/markup differences exist in every case. This is not a failed harness
and is not converted into a blanket renderer pass.

Browser measurements cover EN/light idle/unavailable and JA/light completed at
1280x720, EN/dark reading and JA/dark lastError at 375x1000. Three screenshot
pairs are byte-identical. Every sampled anchor rectangle matches strictly;
only Start opacity 1→0.45 and cursor pointer→not-allowed differ in unavailable
state. All eight saved screenshots were inspected. Fresh console issues: zero.

## Validation and preservation

- compare-renderers.mjs: twelve core/three provider cases, differences retained.
- check-selection-counter-evidence.mjs: actual checkbox predicates/callbacks
  reproduce current defect; no native action handler invoked.
- capture-browser.mjs: four pairs, matching document/font readiness, strict
  rectangle comparison, three identical screenshot pairs, two style deltas.
- validate-evidence.mjs: executable input/slice/dependency/raw/browser integrity
  and recomputed comparison totals. Exact counts printed by the validator.
- npm.cmd --prefix src/LWBridge.UI-0.3.17 run check: PASS.
- npm.cmd --prefix src/LWBridge.UI-0.3.17 run check:production-build: PASS.
- Existing HOME-PREFERENCE-LIFETIME-001B WIP guard: ten of ten byte-preserved.
- Repository/staged diff checks and explicit owned-path staging are required at
  delivery. Existing production package remains e98a0dcc… / 7ecbdf9d….

Historical packets/results and all unrelated WIP remain unchanged. Only owned
headless test helpers were used; owner port4335 remains untouched. No production
rebuild was needed for evidence-only changes.

## Limits and next step

Source/hook rendering and inert online inputs are EXACT_BYTES/EXACT_CONTRACT
evidence, not LIVE_PROVEN native/runtime behavior. Both sides use current EN/JA
catalog inputs. Browser container is the scoped panel directly under body;
full shell/Map, protected original runtime pixels and native behavior are not
accepted. Error case covers lastError/status only; local action scanError alert
is not exercised. Reading/completed times use a fixed Asia/Singapore clock.

Next bounded correction: MMV-BEHAVIOR-001 Manual type editing, checking empty→
single-type edits and source Start normalization without invoking native scans.
Preserve the separate Auto configuration last-type contract and provider fences.
Then continue remaining Map/page offline visuals. No next worker is dispatched
by this review; this packet is complete and needs no further worker approval.
