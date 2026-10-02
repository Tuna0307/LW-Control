# LWB317-UI-MAP-CLOSEOUT-002 — worker assignment

Status: PARTIAL / SPLIT after interrupted worker, 2026-10-03. A review completed; C source recovery completed but implementation deferred. Pending B continues ONLY under LWB317-UI-MAP-REFRESH-OWNERSHIP-001. Do not execute this whole original assignment again. See the dated lead recovery review.
Project lead: the AI in the owner's lead chat. Return findings to that lead through committed files and the owner-relayed delivery report. Your review is a recommendation; project-lead acceptance is separate.

## Goal and context

Complete a substantial, bounded Map frontend campaign: independently review four recent lead implementations, reconcile summary/options refresh ownership, and reproduce the recovered Auto Scan configuration UI. The owner wants the UI/UX copied before native/game functions. Work through all three milestones, preserve coherent commits, and stop at this assignment's completion boundary. Do not start an all-project or native campaign.

Repository: C:/Users/chimw/OneDrive/Desktop/Github/LW-Control.
Branch: research/offline-controller.
Current production baseline: f8f3f4e235c938a709e881227490d91351de3136. The dispatch commit containing this assignment may be a later documentation-only descendant. Inspect actual HEAD before work; preserve subsequent changes.

Target: C:/Users/chimw/OneDrive/Desktop/Github/LW/lwbridge-0.3.17.exe.
Required SHA-256: 4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783.
The clone excludes login/account/licensing UI. Relevant local dependency research is allowed under AGENTS.md; original-service auth/entitlement bypass is not authorized.

Read first: AGENTS.md, task.md, docs/AI_WORK_PROTOCOL.md, docs/PROJECT_LEAD.md, docs/implementation-handoff.md, docs/lwbridge-ui.md. Inspect git status and hashes. Historical documents can be stale: prefer current code, dated evidence and exact original bytes.

## Intentional decisions and preservation

One canonical production frontend; no fallback or redesign. Preserve recovered labels, defaults, timing, ordering, interactions and error priority, even when counterintuitive. Keep accepted server redirects, delayed-Clear lifecycle, tab cache/request disposal, per-kind filters, Treasure preferences, selection/scheduling presentation and row-action fences.

Preserve these pre-existing unrelated paths byte-for-byte and unstaged:
- src/LWBridge.UI-0.3.17/src/previewAfkFixtures.js
- .scratch-lwb317/
- evidence/lwbridge-0.3.17/ui/LWB317-UI-CORRECT-003/screenshots/
- evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-FILTER-LIFECYCLE-001/filter-lifecycle-results.json (existing normalization-only status).

Use the protected-wip.json from MAP-REFRESH-FEEDBACK-001 as a starting guard; inventory and preserve actual additional WIP if present. Do not reset, clean, stash, stage broadly, rewrite historical evidence or force-push.

## Parallel arrangement

You may summon up to TWO subagents. This is authorized for this assignment.
- Subagent A: independent review of Milestone A, counter-evidence and distinguishing cases. Initially read-only production files; write only its own review/evidence directory.
- Subagent B: recover Auto Scan frontend contracts and independent oracle/cases for Milestone C; write only its own evidence/helpers until you explicitly allocate disjoint implementation files.
- Parent worker: production integration, Milestone B, browser checks and final verification. Only one editor owns MapDataPage.jsx and shared master documents. Do not let agents merely reproduce your assumptions. Have them execute actual source/diffs/tests when possible.

## Milestone A — independent checkpoint review

Review these four implementations using actual commits/diffs, pinned original assets and distinguishing checks:
1. MAP-TREASURE-PICKER-001, commit fbde5d6.
2. MAP-GOODS-PICKER-001, commit 315c5a7.
3. MAP-SCAN-HEADER-001, commit 123459d.
4. MAP-REFRESH-FEEDBACK-001, commit f8f3f4e.

Their work items, dated reviews and evidence packets are under docs/work-items, docs/reviews and evidence/lwbridge-0.3.17/ui. Compare each delivery with its own parent; do not misattribute later changes. Re-run relevant maintained checks on current production. Add independent cases targeted at strict keys, picker change/close behavior, stored-run matching, optional state, publishing/progress, error priority, export cancellation/busy/concurrent completion, timer boundaries/cleanup and request disposal as relevant.

Do not equate passing the author's harness with independent proof. Record valid/invalid findings and why. Correct confirmed in-scope defects in coherent commits; retain failing baseline proof. A missing original pixel/native provider is a validation limit, not grounds to invent replacement behavior. Return ACCEPT or CHANGES_REQUIRED recommendations per unit; do not grant global acceptance yourself.

## Milestone B — summary/options refresh ownership

Known current gap: MapDataPage.refreshSummary reloads options on periodic summaries. Original separates a five-second parent summary poll from component-local options revisions/server transitions. REFRESH-FEEDBACK-001 matches row revision timing, but explicitly records differing bootstrap and completion query inventories.

Recover the full assigned frontend contract from:
- evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/MapDataPanel-B4GXEND2.js
- evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js
- actual MapDataPage.jsx, mapBackend.js and relevant App frontend ownership.

Starting source locators (verify exact bytes yourself): main index summary producer UTF-8 byte 363957 and visible-Map poll 370275. MAP-REFRESH-FEEDBACK-001/source-locators.json pins hashes. Original Map completion and progress effects are at 34224 and 34459; panel options and search dependencies are nearby.

Establish which actions/events trigger summary, options and rows; poll admission/overlap; count acknowledgement ownership; mount/backend/profile/provider/server/tab transitions; start, completion and Clear; request retirement and obsolete success/rejection/finally behavior. Reproduce source-like in-scope timing and query side effects in the canonical frontend without introducing duplicate polling or unsafe native paths.

Do not patch only the documented request counts until they look equal. Recover causes and compare actual original/current callback/effect traces with deferred replies and controlled clocks. Include positive/lost server, options-only redirect, obsolete redirect, and delayed Clear cases. Preserve the accepted original [321,322,321] redirect trace and [322,321] delayed-Clear trace when those same test inputs apply. If a prior accepted assumption is contradicted by exact original evidence, document the counterexample and resolve it narrowly; do not silently weaken old checks.

## Milestone C — Auto Scan UI and configuration parity

Recover and implement the complete source-recoverable Auto Scan presentation/configuration scope:
- Manual/Auto switching and preservation of timing/summary.
- Master switch and disabled/waiting/running labels.
- Target server input parsing, Add/Enter behavior, chips/removal/order/deduplication, empty/invalid boundaries and placeholders.
- Interval control, defaults, limits and source normalization.
- Auto scan types, their defaults and last-selection rules; speed and return-to-original-server controls.
- Next-run display/time formatting and profile-scoped configuration load/save/reset transitions where the supplied frontend establishes them.

These are questions to recover, not pre-approved assumptions. Current normalizeAutoConfig/local storage/manual scheduling code is not an oracle. Trace actual original pure helpers, component callbacks and parent config producers/consumers. Do not copy the current clone's caps, defaults, storage keys or persistence shape unless revalidated. Compare original/current renders and handlers, including malformed persisted values, profile changes and the distinction between user edits and normalized stored state.

Runtime executor/native admission, server jumps, real scans and gameplay are OUT OF SCOPE. Do not run or expand runAutoCycle or build a native scheduler. You may pass controlled state/acknowledgements through inert frontend providers to prove UI/config transitions. Keep explicit browser fixtures offline and reject every action. Disclose any unreachable native-produced state; do not claim fixture success is a working feature.

## Required evidence and acceptance

Create evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-CLOSEOUT-002/ and a dated delivery/review document. Include exact artifact hashes and UTF-8 source locators, immutable baseline evidence, actual original-versus-production comparisons, independently authored cases, deferred lifetime/timer traces, browser records and screenshot hashes, protected-WIP guards, and an executable validator.

Prefer the existing exact original-component runner in MAP-INTERACTIONS-001/original. The old FILTER-LIFECYCLE production harness does not execute timeouts when advance is called. REFRESH-FEEDBACK-001/harness.mjs fixes chronological interval+timeout execution in a new packet; use or verify that fix. Do not rely on an interval-only adapter to prove timeout behavior.

Replay maintained header, both pickers, REFRESH-FEEDBACK, filter-lifecycle/R1, request lifetime, navigation, interactions/integration, scheduled tables, historical filter/table/Checking/row/state checks as applicable. Use current replay adapters where old fixed imports are stale. Preserve historical hashes and results; label historical pinned validators separately from current behavioral replays. Tests must catch distinguishing defects, not only mirror your implementation.

Run npm.cmd run check, npm.cmd run build and npm.cmd run check:production-build from src/LWBridge.UI-0.3.17. Run git diff --check and staged diff checks. Complete real browser QA for changed controls in English/light and Japanese/dark plus a narrow viewport. Test actual controls, keyboard paths, busy/errors and navigation where applicable. Save and inspect meaningful screenshots, capture console errors, and disclose any synthetic acknowledgement/drag limitations. No original protected-runtime access or Last War launch/control.

Update relevant current UI master/matrix/ledger and handoff conservatively. Separate EXACT_BYTES/EXACT_CONTRACT, locally verified implementation, UNKNOWN/native limits and original-pixel limitations. Do not mark the entire Map or all UI finished because this campaign passes.

## Delivery and completion boundary

Commit and push coherent A/B/C milestones to origin/research/offline-controller; verify direct remote SHA. No fixed twenty-minute block or elapsed-time stop. Continue until all assigned milestones meet acceptance or an exact external blocker prevents progress. If interrupted, commit a coherent checkpoint and record the precise continuation and remaining cases; do not label partial work complete.

Before final submission, independently review the complete production diff for regressions, edge cases, missing changes and design conflicts. Resolve valid assigned-scope findings and rerun affected checks. Clean up only task-owned browser/process/storage resources; preserve owner activity and WIP.

Final report: status and milestone completion; changes; independent findings with valid/invalid reasoning and corrections; original/current distinguishing results; browser and canonical checks; remaining UI/native/pixel gaps; review/evidence paths; commit and exact verified remote SHA; protected-WIP status. Use AWAITING_REVIEW for the completed worker delivery. The lead will inspect and decide acceptance. Stop at this campaign; do not choose another project task.
