# LWB317-UI-MAP-AUTO-CONFIG-001 — complete Auto Scan UI/configuration

Status: ASSIGNED. One worker, sequential milestones, no subagents.

## Goal and start

Complete the recovered LWBridge 0.3.17 Map Auto Scan presentation, interaction
and profile-scoped frontend configuration. The owner wants UI/UX reproduction
before function integration. Finish this whole feature, then return it to the
project lead through the owner's manual relay.

Repository: C:/Users/chimw/OneDrive/Desktop/Github/LW-Control.
Branch: research/offline-controller.
Production baseline: cf75b4e3cd8b602c17cef9a244385e6db7c02daf. The assignment
arrives in a later documentation-only commit. Inspect actual HEAD and preserve
later changes; never reset to the baseline.

Read AGENTS.md, task.md, docs/AI_WORK_PROTOCOL.md, docs/PROJECT_LEAD.md,
docs/implementation-handoff.md, docs/lwbridge-ui.md and this entire assignment.
Check git status. Work alone: do not spawn subagents, delegate or open another
worker chat. Earlier permission to use two subagents is superseded.

Reference EXE: C:/Users/chimw/OneDrive/Desktop/Github/LW/lwbridge-0.3.17.exe.
Required SHA-256: 4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783.
Verify the supplied assets and source locators yourself. The original login,
account and licensing UI are excluded; no original auth/entitlement bypass.

## Evidence and intentional behavior

Start from the committed CLOSEOUT-002/agent-b packet under
evidence/lwbridge-0.3.17/ui, especially README.md, source-locators.json,
recover-auto-contract.mjs and auto-contract-cases.mjs. Its historical directory
name does not authorize delegation. The 12 passing cases establish recovered
original behavior, not current clone parity. Preserve that packet unchanged.
Use the exact frontend-package/web/assets/index-BVfnK1wp.js and
MapDataPanel-B4GXEND2.js as authority, not the clone's existing helpers.

Recovered starting contracts, all requiring byte/hash verification:

- Manual/Auto mode starts Manual. Switching does not save configuration and
  preserves the local server draft; timing and summary remain shared.
- Defaults: disabled, 60 minutes, no server IDs, ordered types truck/railway/
  dispatch/ghost/treasure, fast mode, return enabled, nextRunAt zero.
- Parser Ci (index byte 359071) splits whitespace and ASCII/fullwidth comma or
  semicolon; Number conversion, integer range 1..99999, first-occurrence order,
  deduplication, cap 20. Appender wi and remover Ti follow it at 359234/359284.
- Add/Enter callback lr (panel 40544): invalid input remains; valid input emits
  and clears even when the cap prevents insertion. Enter prevents default.
  Add availability follows parseability. Recover inputMode and placeholder.
- Normalizer Ei (index 359327) clamps/truncates interval to 20..1440, applies
  exact boolean/type/default rules and preserves the source's NaN behavior.
  Malformed structural stored values can throw and make the loader fall back
  to fresh defaults. Preserve these distinctions; do not replace them with
  guessed validation or a generic fallback value.
- Panel edits shallow-merge; parent Ct (364217) normalizes, sets nextRunAt to
  Date.now() on disabled-to-enabled, clears it on disable, otherwise preserves
  it, updates state/ref and saves through ji.
- Load/save Ai/ji (360013/360170) use lwbridge.mapAutoScan.${profileId}, without
  a default suffix. Profile layout load (370501) precedes new-profile writes.
  Loading never writes. There is no Reset control to add.
- All eight scan types render; the sole selected type cannot be unchecked.
  Preserve exact speed transformations and return checkbox behavior.
- Running wins over enabled/waiting/disabled. Configuration remains editable
  while running; Run now has the separate online/enabled/running/reading gate.
  Run now only edits nextRunAt. Next scan is '-' unless enabled and positive;
  formatter N (panel 7885) distinguishes seconds/milliseconds and locale.

Preserve accepted summary/options ownership at cf75b4e, header/row timing,
pickers, server redirects [321,322,321], delayed Clear [322,321], counts,
request disposal/cache, filter lifecycle, Treasure preferences, selection,
Scheduled Plunder presentation, localization and offline/native action fences.
One canonical production UI; no legacy fallback or redesign.

## Sequential milestones

### A — exact helpers and distinguishing baseline

Recheck the original packet against exact bytes. Pin an immutable cf75b4e
baseline in the new evidence packet. Execute original helpers and current
production helpers with discriminating parser, append/remove, normalization,
load/save and timestamp cases. Include mixed separators, numeric forms,
duplicates/cap, nullish/invalid values, corrupt JSON and structural errors.
Retain baseline failures. Implement directly necessary canonical helpers.
Commit a coherent tested checkpoint; do not stop merely because A is done.

### B — parent profile ownership

Move canonical configuration ownership to the profile-aware App boundary and
pass it through the real Pages/Map path. Reproduce load-before-save ordering,
state/ref consistency and the single user-edit normalization/persistence path.
Prove two profiles with different stored configs, first load without writes,
profile switch while Map is mounted/unmounted, remount/reload, enable/disable,
enabled-to-enabled edits and controlled Run-now timestamps. The previous
profile must never be saved to the new key. Verify the actual App hooks and
production callbacks, including StrictMode behavior where applicable.

Existing runAutoCycle/native scheduler logic is outside this assignment. Only
mechanical config access/update bindings needed by this ownership migration
may change; document them and preserve its algorithm, admission checks and
provider fences. Do not execute, expand or implement native Auto scheduling,
scan execution or server jumps. Expose running presentation through controlled
inert test state. Do not connect a provider to make Run now perform scans.
If a source-backed ownership defect cannot be separated from executor work,
record the exact dependency and checkpoint instead of expanding scope.

### C — complete Auto controls and presentation

Wire every listed control to the canonical config path. Match exact original
rendered ordering, labels, defaults, classes/layout and disabled rules. Cover
mode/draft retention, keyboard Add, chips, interval, all types, speed, return,
master status, Run now and Next scan. Remove only source-disproved current
behavior. Prove original/current actual handlers and renders across running,
reading, offline and malformed optional inputs. Use existing recovered styles
and assets; disclose unavailable assets. Do not add unrelated UI features.

### D — integrated verification and delivery

Review the complete diff yourself for regressions and design conflicts. Build
adversarial cases that would catch profile leakage, wrong parser/cap, interval
repair, last-selection errors, config disabled during running, wrong status
priority, missing next-time gating and duplicate writes. Use actual original
code as oracle; copied formulas alone are insufficient. Keep writer output in
this new packet so historical result files remain unchanged.

Perform real offline-browser controls in English/light and Japanese/dark plus
a narrow viewport: Add and Enter, invalid draft, chips, interval, type selection,
mode round-trip, enable/disable and reload persistence. Running and online
Run-now predicates may use inert harness inputs, explicitly disclosed; browser
preview stays online:false and every native action rejects. Save and inspect
meaningful screenshots and capture console errors. Never launch/control Last
War or enter the protected original runtime to collect pixels.

## Checks, preservation and outputs

Replay maintained ownership/Clear, header, picker, feedback, R1/PM-027,
request-lifetime and interaction cases affected by App/Map changes. Preserve
the accepted redirect/Clear traces. Use current adapters for historical
extractors; disclose stale scripts rather than rewrite old hashes/results.
Do not execute a historical writer against protected WIP: redirect results to
the new packet or use a reviewed adapter. No broad unrelated regression campaign.

Run npm.cmd run check, npm.cmd run build and npm.cmd run check:production-build
from src/LWBridge.UI-0.3.17, plus git diff --check and staged diff checks.

Inventory and hash all existing WIP before work. Preserve byte-for-byte and
unstaged: src/LWBridge.UI-0.3.17/src/previewAfkFixtures.js, .scratch-lwb317/,
CORRECT-003/screenshots/, FILTER-LIFECYCLE-001/filter-lifecycle-results.json and
FILTER-LIFECYCLE-001-R1/independent-results.json under the UI evidence tree.
Start with REFRESH-FEEDBACK-001/protected-wip.json and add actual extra paths
to your own manifest. No reset, clean, stash, force-push or broad staging.

Create evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-AUTO-CONFIG-001/ with original
locators/hashes, baseline/current results, actual App/panel cases, browser
records/screenshots, protected hashes and an executable validator. Write a
dated delivery review. Update relevant current master/matrix/ledger/handoff
conservatively: source/local proof does not establish original pixels, native
Auto behavior or full Map/all-page parity.

Commit and push coherent sequential milestones to origin/research/offline-controller;
verify direct remote SHA. Continue until all assigned acceptance checks pass
or a concrete blocker prevents progress. No fixed time block. If interrupted,
save a coherent checkpoint with exact next command/case and remaining changes.
Clean up only task-owned browser/process/storage resources.

Final report: AWAITING_REVIEW; milestone completion, changes, baseline failures
versus original/current results, valid/invalid self-review findings and fixes,
browser/canonical checks, evidence/review paths, exact commit/remote SHA,
protected-WIP status and remaining UI/native/pixel limits. Stop at this feature;
the project lead makes acceptance decisions and assigns further work.
