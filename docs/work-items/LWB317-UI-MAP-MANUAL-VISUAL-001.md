# LWB317-UI-MAP-MANUAL-VISUAL-001

Status: COMPLETE / ACCEPTED as a comparison packet after lead takeover, 2026-10-04.
Size: medium; one worker, one bounded Map region.
Dispatch baseline: f04f79565ca59d9f2404943879f4b7d89cd1da1d.

## Goal

Establish an executable offline visual comparison of the original and current
Manual scan header region. Existing formatter and table checks do not establish
this region's complete DOM, CSS and geometry parity. Return exact discrepancies
for project-lead review. This assignment is comparison/evidence only; production
corrections are a separate bounded task after the findings are reviewed.

## Allowed scope

Only the Map scan-mode strip's presentation, header title/timing/speed/action
toolbar while Manual is selected, status/server/progress strip, scan-error
presentation, and compact scan-type checkbox strip. Exercise source-recovered
idle/unavailable, idle with server, reading below 50%, reading above 50%, completed
timing and error cases. Use English and Japanese. Derive actual state shapes,
boundaries and labels from source; the case names are investigation targets,
not permission to invent fields or defaults.

Exclude the Auto configuration card, browse tabs, filters/search, tables,
pagination, Scheduled Plunder, shell redesign and all native implementation.
Preserve accepted Home preference, Map lifecycle, filters, Auto and table work.
Do not launch/control Last War, invoke scan/start/stop/clear, contact protected
services or bypass authentication. Render positive states with inert inputs only.

## Inputs

- Read AGENTS.md, task.md, docs/AI_WORK_PROTOCOL.md, docs/lwbridge-ui.md and
  docs/UI_FINISH_CHECKLIST.md before work.
- Reference EXE: C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe;
  expected SHA-256 is the exact value in AGENTS.md section 1.
- Original asset: evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/MapDataPanel-B4GXEND2.js;
  inspect its actual dependency imports and recover exact slices with UTF-8 byte
  locators, lengths and hashes. Include original main-asset helpers when needed.
- Original CSS: evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-rIL9Fpht.css.
- Current: src/LWBridge.UI-0.3.17/src/MapDataPage.jsx, mapScanPresentation.js,
  mapScanHeaderFixtures.js, reference.css, styles.css and main.jsx.
- Prior evidence patterns: evidence/lwbridge-0.3.17/ui/LWB317-UI-OFFLINE-VISUAL-001/
  and evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-SCAN-HEADER-001/.
  Historical evidence is immutable. Reuse through new adapters/output paths;
  do not overwrite historical records to make newer code pass.
- Starting-WIP guard:
  evidence/lwbridge-0.3.17/ui/LWB317-UI-HOME-PREFERENCE-LIFETIME-001B/check-protected-wip.mjs
  and protected-wip-start.json. All ten pinned files must remain byte-preserved.
  Inventory any additional starting dirty/untracked paths too.

## Sequential work

1. Pin original/current inputs and recover the actual original rendering fragment
   and its dependencies. Execute exact original source and actual current JSX;
   do not handwrite an imitation oracle or copy clone markup into the reference.
   Keep original source logic unchanged. Explicitly document harness substitutions
   for clocks, translation, hooks, surrounding container and unavailable producers.
2. Compare the six named state families in both languages with the same clock,
   timezone and inputs. Compare text, DOM structure, classes, attributes and
   conditional controls. Any normalization must be narrowly justified and raw
   differences retained. Record provider-fencing differences separately; never
   remove them silently or disable current fences to force parity.
3. Capture four paired browser views covering both themes/languages and desktop
   plus a measured narrow viewport (375 CSS px). Include idle, reading, completed
   timing and error. Use original reference CSS on the original; use current
   production CSS in its actual main.jsx import order on the current. Compare
   element rectangles and relevant computed styles as well as screenshots;
   record font/timezone/device-scale/viewport setup. Inspect saved captures.
   Differences caused by surrounding-container substitutions must be explicit.

Use existing tooling first. A source-rendered reference is an offline comparison,
not proof of protected original runtime pixels. Do not label simulated online
inputs LIVE_PROVEN. If exact extraction cannot be achieved, preserve the locator,
failure and continuation point; do not manufacture a matching reference.

## Outputs and acceptance

Own only this work item, its evidence packet and dated review:

- evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-MANUAL-VISUAL-001/ containing README,
  pinned input manifest, reproducible comparison scripts, raw and interpreted
  results, browser measurements/console records, paired screenshots, and a
  validator that checks actual source/evidence hashes and reported results.
- docs/reviews/2026-10-04-LWB317-UI-MAP-MANUAL-VISUAL-001.md with each mismatch's
  original/current locator, reproducer, impact and proposed correction scope.
- Work-item delivery section with exact runnable commands and continuation.

Acceptance requires actual-original/current execution, the twelve state-family
comparisons or explicit source-backed exclusions, four inspected browser pairs,
and truthful mismatch accounting. Zero mismatches is not a prerequisite for a
complete review packet. Finding defects is useful progress. No production edits,
global status upgrade or additional page is authorized in this task.

Run the new comparison/validator, existing WIP guard without --record,
npm.cmd --prefix src/LWBridge.UI-0.3.17 run check,
npm.cmd --prefix src/LWBridge.UI-0.3.17 run check:production-build,
and git diff --check. Record unavailable/stale checks rather than rewriting old
evidence. No new production build is necessary for evidence-only changes.

Commit coherent evidence checkpoints, review the explicit staged allowlist,
push to origin/research/offline-controller and verify the exact full remote SHA.
Never stage unrelated WIP. Preserve the owner's browser/server on port 4335;
choose an available owned port and clean up only your own helpers/tabs/storage.

Stop after this packet. Return AWAITING_REVIEW to the project lead via the owner,
with results, mismatches, proof limits, commands, full commit/remote SHA and exact
continuation. Work alone with no subagents or delegation. No fixed time limit.

## Delivery

The lead finished the interrupted packet. Twelve core/three provider comparisons
retain all raw differences. Four inspected browser pairs have strict equal
rectangles; three PNG pairs match, with two unavailable-Start style differences
in the fourth. Inert actual selection callbacks prove the open last-type defect.
No production code changed. Canonical check/package and ten-path WIP guard pass.
Run exact commands in the packet README and validate-evidence.mjs. Review is
docs/reviews/2026-10-04-LWB317-UI-MAP-MANUAL-VISUAL-001.md.
Next: focused correction for MMV-BEHAVIOR-001, preserving separate Auto rules and
native fences; no broad Map/global/native acceptance follows from this delivery.
