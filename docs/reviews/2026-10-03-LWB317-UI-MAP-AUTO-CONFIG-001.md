# LWB317-UI-MAP-AUTO-CONFIG-001 delivery review — 2026-10-03

Status: **AWAITING_REVIEW**

Scope: complete milestone D for the recovered Map Auto Scan UI/configuration
assignment. A/B/C production checkpoints remain 650fcdf, 8984e56 and f5a1eaf.
The continuation began at ef5cda03a5a8f0788668ae24548efeca287a56dc.

## Result

Milestone D is complete. The saved regression work was preserved and assessed,
the current regression runner was repaired around the two stale historical
extractors, real offline browser controls were completed, the complete
production diff was reviewed, and the evidence packet now has an executable
validator and protected-WIP guard.

No milestone-D production defect was confirmed, so production code was not
changed during this continuation.

## Regression findings and dispositions

1. Historical NAVIGATION-001 strips imports before evaluating the current Map
   page and therefore throws DEFAULT_AUTO_SCAN_CONFIG is not defined. A current
   adapter supplies the real production helper-module exports while leaving the
   historical checker and result unchanged. The replay passes with six retained
   baseline failures, zero current failures and 17 requests.
2. The saved historical milestone-B ownership redirect uses an older App
   harness and throws loadAutoScanConfig is not defined. That reproducer is
   retained and disclosed. The current ownership checker executes the newer App
   harness and current Map page and passes with parent=2, child summary=0, child
   listeners=0 and options [321,321].
3. The full affected regression runner passes 10/10: navigation, scan header,
   retained-goods picker, Treasure picker, refresh feedback, interactions,
   request lifetime, Scheduled Plunder, R1 redirect/Clear and current
   summary/options ownership.
4. R1 preserves all five lifecycle cases, including the accepted redirect/loss
   fencing. Request lifetime reports zero current failures. The Scheduled
   Plunder campaign renders 10,482 cases and detects 51/51 mutations.

Historical writers/results were not weakened or rewritten to obtain these
passes.

## Browser evidence

The existing Vite preview at 127.0.0.1:4319 was reused. Preview providers stayed
offline and Run now remained disabled, so no native action was dispatched.

English/light real controls covered invalid Enter, mixed-separator keyboard
Add/deduplication, chip removal, Manual/Auto draft retention, interval 45,
Fast speed, Resource Point selection, return-to-original, disable/re-enable and
reload persistence. Japanese/dark reloaded the same persisted configuration,
rendered localized controls and completed a real keyboard Add/remove. Console
capture contains zero warnings and zero errors.

The saved English/light 1600x900, Japanese/dark 1600x900 and narrow
English/light 860x900 captures were inspected. The narrow capture supports
layout evidence; the browser connector exposes no viewport-resize action, so
live narrow interaction was not separately driven. The test storage value was
restored byte-for-byte and the owned tab was closed.

## Validation

- Current helper comparison: 29/29 PASS; distinguishing baseline remains 3/12
  PASS and 9/12 FAIL.
- Actual App profile ownership: 7/7 PASS.
- Actual Map Auto controls: 11/11 PASS; original agent-b oracle 12/12 PASS.
- Current refresh ownership: PASS.
- Affected Map regressions: 10/10 PASS.
- npm.cmd run check: PASS.
- npm.cmd run build: PASS. Vite emitted its non-failing large-chunk advisory.
- npm.cmd run check:production-build: PASS.
- Evidence validator: PASS.
- Protected-WIP guard: PASS, seven of seven hashes unchanged.
- git diff --check: PASS after delivery documentation; staged diff check PASS.

## Evidence and limits

Primary packet:

evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-AUTO-CONFIG-001/

Key files are README.md, original-source-locators.json,
current-source-hashes.json, baseline-results.json,
current-helper-results.json, profile-ownership-results.json,
control-results.json, regression-results.json, browser-results.json,
validate-evidence.mjs and check-protected-wip.mjs.

This establishes the assigned source/local UI/configuration behavior. It does
not establish native Auto execution, live Last War server movement/scans,
original protected-runtime pixels, or complete Map/all-page parity. Project
lead review and acceptance remain pending.
