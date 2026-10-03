# LWB317-UI-MAP-AUTO-CONFIG-001 evidence

Milestone D status: **PASS / AWAITING_REVIEW** on 2026-10-03. This packet
closes the UI/configuration assignment only. Native Auto execution, Last War
control, original protected-runtime pixels, and global Map/all-page parity are
outside this evidence.

## Source and distinguishing proof

- original-source-locators.json pins the recovered 0.3.17 frontend assets and
  exact Auto defaults/helpers/ownership/control locators.
- baseline-cf75b4e.MapDataPage.jsx is the immutable production baseline used
  for distinguishing checks.
- baseline-results.json keeps the useful baseline counterexample visible:
  3/12 pass and 9/12 fail against the recovered original contract.
- current-helper-results.json is 29/29 PASS against the source-backed helper
  cases.
- profile-ownership-results.json is 7/7 PASS while executing the actual App
  body across two profiles, same-mount switches and fresh remount.
- control-results.json is 11/11 current PASS while executing the actual
  MapDataPage body; the original agent-b oracle remains 12/12 PASS.
- current-source-hashes.json records the delivered production/checker and
  screenshot hashes. validate-evidence.mjs recomputes them.

## Regression adapters and results

run-regressions.mjs executes ten affected accepted Map checks. The current
result is regression-results.json, status PASS.

The historical NAVIGATION-001 extractor predates Auto imports and throws
ReferenceError: DEFAULT_AUTO_SCAN_CONFIG is not defined when pointed directly
at the current MapDataPage. replay-navigation-current.mjs leaves the historical
checker/result untouched, injects the real current production helper module
exports for its stripped-import harness, and supplies inert identities only for
child JSX components that the navigation harness does not execute. The separate
maintained component regressions still execute those components. The replay
preserves the historical distinguishing result and reports baseline=6,
current=0, requests=17.

The saved replay-refresh-ownership-redirect.mjs is intentionally retained as
the interrupted-work reproducer for the other stale harness: the historical
milestone-B App harness throws ReferenceError: loadAutoScanConfig is not
defined. It is not part of the current runner. The maintained
check-refresh-ownership-current.mjs instead executes the current App harness
and current Map page and writes regression-refresh-ownership.json; that result
is PASS with parent mount requests 2, child summary/listener ownership 0/0 and
options [321,321].

replay-r1-redirect.mjs redirects the historical R1 writer into this packet and
asserts the historical result bytes do not change. The result preserves the
accepted five lifecycle cases, including server redirect/loss fencing and the
delayed-Clear boundary.

The other current results cover scan header timing, retained-goods picker,
Treasure picker, refresh feedback, the 38 interaction scenarios, request
lifetime, and the full Scheduled Plunder mutation campaign. The Scheduled
campaign rendered 10,482 cases and detected all 51 mutations with no survivors.

## Browser evidence

browser-results.json records the live offline-preview actions. English/light
covered invalid Enter, keyboard Add with mixed separators and dedupe, chip
removal, Manual/Auto draft retention, interval/speed/type/return edits,
disable/re-enable, and reload persistence. Japanese/dark reloaded the persisted
configuration with localized labels and performed a real keyboard Add/remove.
Run now remained disabled throughout the offline preview and no native action
was dispatched.

Three saved captures were inspected:

- screenshots/auto-en-light.png — 1600x900.
- screenshots/auto-ja-dark.png — 1600x900.
- screenshots/auto-narrow-en-light.png — 860x900; visual layout evidence only.
  Interaction claims are limited to the live full-width sessions.

Console capture from debugger attachment through the English reload and
Japanese/dark pass contained zero warnings and zero errors. Only Vite
connect/connected debug entries and the React DevTools informational message
were present. The test localStorage value was restored byte-for-byte, the owned
browser tab was closed, and the pre-existing Vite process was left running.

## Milestone-D self-review

The complete A/B/C production diff from cf75b4e through f5a1eaf was reviewed
again together with the current regression results. No production correction
was justified in milestone D.

Findings and dispositions:

1. Direct NAVIGATION-001 replay fails because its import-stripping harness lacks
   newer production dependencies. Disposition: current adapter added; historical
   checker/result unchanged; distinguishing baseline retained.
2. The interrupted ownership redirect fails because its historical App harness
   lacks loadAutoScanConfig. Disposition: stale reproducer retained and
   disclosed; current App/Map ownership harness is the regression authority.
3. Profile leakage/load-before-save risk. Disposition: 7/7 actual-App cases
   pass, including same-mount profile replacement with zero added writes.
4. Map ownership/request/redirect/Clear regression risk. Disposition: all ten
   affected regression entries pass; no source mismatch found.
5. Auto control/parser/timing/status risk. Disposition: 29/29 helpers and 11/11
   control cases pass, including invalid/valid Enter, cap behavior, sole-type
   guard, running editability, Run-now gate and next-time display.
6. Browser presentation/persistence risk. Disposition: offline live checks and
   inspected captures pass within the explicitly recorded viewport limits.

## Reproduction

From the repository root:

    node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-AUTO-CONFIG-001/check-helpers.mjs
    node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-AUTO-CONFIG-001/check-profile-ownership.mjs
    node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-AUTO-CONFIG-001/check-controls.mjs
    node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-AUTO-CONFIG-001/run-regressions.mjs
    node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-AUTO-CONFIG-001/check-protected-wip.mjs
    node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-AUTO-CONFIG-001/validate-evidence.mjs

Canonical package checks are run from src/LWBridge.UI-0.3.17:

    npm.cmd run check
    npm.cmd run build
    npm.cmd run check:production-build

The seven protected WIP hashes are pinned in protected-wip-before.json and
repeated in protected-wip-after.json; check-protected-wip.mjs recomputes all
seven before delivery.
