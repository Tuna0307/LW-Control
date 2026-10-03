# LWB317-UI-SHELL-MAP-ENTRY-001 — Map navigation request timing

Status: COMPLETE / ACCEPTED for assigned source/local UI scope, 2026-10-04 lead
takeover. Worker product 147e5cf retained; C packet completed and checks rerun.
See docs/reviews/2026-10-04-LWB317-UI-SHELL-MAP-ENTRY-001.md. Native/original
pixels and broader shell/inventory acceptance remain separate.

## Goal and context

You are the worker AI on LW-Control. Restore only the source-recoverable Map-entry
summary request timing in canonical App navigation. Work in
`C:/Users/chimw/OneDrive/Desktop/Github/LW-Control` on `research/offline-controller`.
Reviewed baseline: `2931c78a6b0c50537f53490d941defe11c45cdff`; inspect actual HEAD
and preserve newer work. Never reset to this baseline.

Read AGENTS.md, docs/AI_WORK_PROTOCOL.md, task.md, docs/PROJECT_LEAD.md,
docs/UI_FINISH_CHECKLIST.md, docs/implementation-handoff.md, this work item,
and the dated SHELL-RETENTION-001 and REVIEW-SHELL-CROSSSERVER-001 reviews.
Retention and Cross-server are accepted for their bounded source/local scopes.
The phase remains UI reproduction, before native/gameplay integration.

Reference EXE: `C:/Users/chimw/OneDrive/Desktop/Github/LW/lwbridge-0.3.17.exe`.
SHA-256: `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
Original shell: `evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js`.
SHA-256: `44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6`.

## Exact scope

Audit and correct the summary request initiated by selecting Map from another route,
including active-Map no-op, dispatch ordering, rejection handling, rapid route changes,
and initial-route behavior. Allowed production scope is App.jsx navigation/entry effect
and minimal necessary helpers. Add task-local evidence, current adapters and docs.

Lead checked exact original `Tt` at UTF-8 byte 364377. It calls `_t().catch(...)`
when selecting a different `map-data` route, then `Wi(e)` and the React transition.
**It starts the summary request before transition and does not await settlement.**
Do not turn this into acknowledgement-before-navigation or disable navigation while
the request is pending. Reverify the bytes and surrounding producers yourself;
older descriptions saying "before transition" are not proof of an await.

Current App.selectRoute transitions immediately; a separate activeRoute effect
starts summary after Map becomes active. Recover the complete relevant entry contract
before deciding which effect to remove or retain. Do not accidentally create two
entry requests. Distinguish entry traffic from independent bootstrap/connected poll.
The original preload call is part of the recovered order; record its relationship
without expanding this task into a page-loading/bundler architecture rewrite.

Preserve intentional behavior: local profile/bootstrap state instead of original
commercial accounts; visited-page Activity retention and profile identity; current
summary generation/profile guards; parent-owned bootstrap, connected five-second
poll and scan-completion refresh; Map-owned query/options revisions; accepted Auto,
Cross-server summary-before-history ordering, Home error/busy behavior and all native
action fencing. Do not "improve" source-observed races or interactions without evidence.

## A — recovery and distinguishing baseline

Pin actual original slices/hashes and an immutable current App git blob. Execute the
actual original navigation function with inert summary/preload/transition bindings.
Compare the current actual callback/effect behavior, identifying a failing ordering
case. Record source-backed expectations separately from preserved implementation
guards and unknown/native behavior. Commit/push this coherent recovery checkpoint.

## B — correction and executable verification

Make the smallest source-backed production change. Use actual current App callbacks/
effects or mounts, with controlled inert replies and observable request/transition
events. Include different-route Map entry, already-active Map no-op, non-Map navigation,
deferred success, rejection without blocking transition, leaving/returning while
pending, obsolete profile reply, offline/native-unavailable fencing, direct initial
Map route, and request attribution separate from bootstrap/polling. Recover the
original contract for each expectation instead of inventing a cancellation policy.

Keep historical scripts/results/hash pins unchanged. Reuse current harness scaffolds
with new task-local outputs when App changes invalidate old fixed extractors. Commit/
push the correction with distinguishing baseline/current and affected tests passing.

## C — bounded browser proof and delivery

Use true offline browser preview for Home → Map → Home → Map, active Map re-click,
and Cross-server open/click-away. Check EN/light and JA/dark, a measured narrow CSS
viewport, preserved page state and fresh console errors. Offline navigation is browser
proof; synthetic positive summary timing remains mounted callback/effect proof.
Do not make preview report online or invent native successes. Capture only useful
settled images and inspect them; omit previewLanguage during interactive locale QA.

Run focused entry checks and affected current App summary ownership, retention,
Cross-server and Home acknowledgement checks; canonical npm.cmd run check, build,
check:production-build, protected-WIP guard and git diff --check. Do not replay
expensive unchanged Scheduled/whole-page suites. Create an executable integrity
validator, README, continuation and dated delivery review under this work item's
evidence root: `evidence/lwbridge-0.3.17/ui/LWB317-UI-SHELL-MAP-ENTRY-001/`.
Update relevant current tracking/handoff documents to AWAITING_REVIEW, preserving
the broader shell/UI/native/pixel dispositions. Commit/push and verify the direct
remote SHA equals local HEAD. Return changes, checks, exact continuation and limits;
then stop for project-lead review of this unit only.

## Boundaries and preservation

No native providers, Last War launch/control, live scans/jumps, protected service
access/auth bypass, updater, export, OS hotkeys, account/licensing UI, legacy fallback,
profile architecture, theme redesign, Map notices or final inventory work. An unknown
branch is documented, not guessed. No fixed time limit and no quality shortcuts.

Record dirty/untracked paths before work. Run
`node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-AUTO-CONFIG-001/check-protected-wip.mjs`
before/after; preserve all seven pinned WIP hashes and unrelated normalization-only
status. Do not stage previewAfkFixtures.js, .scratch-lwb317/, CORRECT-003/screenshots/
or old Map results. No reset/stash/clean, broad staging, unrelated deletion or force
push. Close only helper processes/tabs you own. Keep continuation current at each
sequential checkpoint so a fresh chat can resume without restarting accepted work.
