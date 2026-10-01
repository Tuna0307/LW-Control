# LWB317-UI-HOME-ERROR-002-R1 — root acknowledgement and polling

Owner: returning worker. State: AWAITING_REVIEW. Date: 2026-10-02.
Review baseline: db3aae317321d9ac21774f421dd363daa8a146c3;
PM-018 assignment documentation follows it. Use current HEAD without reset.

## Goal and inputs

Fix only the root-status producer/clearing defect confirmed by independent review
and PM-018. Separate Home errors are useful but HOME-ERROR-002 is CHANGES_REQUIRED.
Translation and shared switch localization are accepted; busy/Trade review is
separate. Do not resume the broad CORRECT-003 campaign.

Repo: C:\Users\chimw\OneDrive\Desktop\Github\LW-Control.
Read AGENTS.md, task.md, docs/AI_WORK_PROTOCOL.md, docs/PROJECT_LEAD.md,
docs/lwbridge-ui.md, this assignment, and dated REVIEW-HOME-ERROR-002 / PM-018
reviews. Inspect git status and the independent review checker before editing.

Reference exe: C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe,
SHA-256 4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783.
Frontend: evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js,
SHA-256 44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6.
UTF-8 bytes: Jt 367489, Xt 367703, root request 369540, poll 369979, qr 336694.
The enclosing original effect is gated by selected profile and depends on its ID.
Inspect this context yourself; do not interpret "one-time" as an unconditional
process-wide request or create a new profile-management subsystem.

## Allowed change

App.jsx root-status retrieval and acknowledgement wiring, focused correction QA
and conservative documentation. Remove game_root_status from the repeated
refreshStatus request list. Retrieve it on the source-supported initial/profile
effect path using the existing local bridge/bootstrap contract. Successful root
acknowledgement sets status and clears only gameRootError; its failure follows the
recovered root-error path. Reuse that success behavior for the explicit
post-selection root-status reply. Preserve canceled/invalid/failed selection
outcomes, independent action errors, preference/profile/busy/ack ordering.

Keep existing non-root polling, listeners, manual refresh and recovery/config/Map
behavior intact. Document any necessary root-only manual-refresh consequence.
Do not remove other repeated commands merely because the original poll differs.
No native changes, new command/provider, Pages redesign, native picker, Last War
launch/control, original authentication bypass/service access, fallback or subagents.

## Acceptance

Use extracted actual production callbacks/effect wiring and real frontend bridge
with synthetic local responses. Verify successful/failed/deferred initial root
acknowledgement; initial request gating and timer request inventory; cancellation
preserves a prior root error across periodic refreshes; invalid/selection/status
failures preserve the action channel; valid selection clears root error only when
its status acknowledgement succeeds. Retain preference/reconnect profile injection,
missing-profile rejection, acknowledgement ordering and native-unavailable fencing.
Compare root acknowledgement/error semantics to exact original Jt/effect source.

Preserve all prior saved reports/checkers/evidence. The old independent review
checker intentionally reproduces the defect and is historical after correction.
HOME-ERROR-002 saved callbacks and HOME-BUSY-001 App hash may necessarily change:
use a new R1 harness/adapter executing current callbacks and preserving the former
scenario expectations, with the changed fields disclosed. Do not regenerate old
records or disable assertions to get green. Run existing busy checker without
--verify-record to retain its actual render/predicate assertions without rewriting
the historical App-hash snapshot. Accepted translation and switch checkers should
still pass --verify-record; run their evidence validators too.

Run npm.cmd run check, npm.cmd run build, npm.cmd run check:production-build,
new focused evidence/source/JSON validation and git diff --check. Brief local browser
recheck of existing simultaneous/missing-root error fixtures is sufficient for
unchanged rendering; synthetic bridge tests establish this correction, not a
browser fixture pretending to execute native callbacks. No full pixel/native claim.

## Delivery boundary

Create docs/reviews/<actual-date>-LWB317-UI-HOME-ERROR-002-R1.md and
evidence/lwbridge-0.3.17/ui/LWB317-UI-HOME-ERROR-002-R1/ with exact source locators,
request/result checks and commands. Update this assignment/current UI masters,
matrix/ledger and handoff conservatively to AWAITING_REVIEW for this correction.
Complete only this unit, without a fixed time limit; return for project-lead review.

Preserve src/LWBridge.UI-0.3.17/src/previewAfkFixtures.js, .scratch-lwb317/ and
evidence/lwbridge-0.3.17/ui/LWB317-UI-CORRECT-003/screenshots/ unchanged/unstaged.
Do not stop unowned preview processes. Use research/offline-controller; review
diff, stage explicit owned paths, commit/push and verify direct remote SHA.
Return status, behavior changed, tests, exact evidence paths, SHA and remaining limits.
