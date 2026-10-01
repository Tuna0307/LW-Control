# LWB317-UI-HOME-STATES-001 — complete source-backed Home UI states

Project-lead assignment: 2026-10-01. State: SUPERSEDED.

The owner broadened the next task to all eight primary pages and removed fixed
time stops. Use `LWB317-UI-COMPLETE-001.md` instead. The text below preserves the
prior proposed assignment as history; it is not current worker authorization.

## Goal

Complete the statically recoverable Home UI states in the canonical
`src/LWBridge.UI-0.3.17` frontend. Its present `HomePage` implements only the
unresolved loading state and disabled switches. The target is the recovered
0.3.17 component/CSS/locale contract, not a redesigned dashboard.

## Required inputs

Read `AGENTS.md`, `task.md`, `docs/README.md`, `docs/PROJECT_LEAD.md`,
`docs/AI_WORK_PROTOCOL.md`, `docs/strict-parity-recovery.md`, `docs/lwbridge-ui.md`,
the takeover audit `docs/reviews/2026-10-01-LWB317-PM-003-project-lead-takeover.md`,
the Home inventory `docs/reviews/2026-09-29-LWB317-UI-002A-home-inventory.md`,
and the current parity matrix/feature ledger/handoff. Inspect exact recovered
frontend bytes; inventory summaries are locators, not substitutes for the source.

Reference: `C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe`.
SHA-256: `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
Repository: `C:\Users\chimw\OneDrive\Desktop\Github\LW-Control`.
Branch and remote: `research/offline-controller`,
`origin/research/offline-controller`. The dispatch prompt supplies the exact
starting revision. Check branch, HEAD, remote and cleanliness before editing;
preserve unexpected changes and stop on an unexplained baseline mismatch.

## Allowed scope

1. Recover Home rendering conditions, text precedence, class hierarchy, controls,
   disabled/busy state rules and switches from exact 0.3.17 bytes. Record locators.
2. Build a Home presentation component/model covering source-proven unresolved,
   missing-root, valid/stopped, running/disconnected, connected, launching/busy,
   repair and recovery/error states where actually present. Do not invent a
   state because this list names it; let recovered predicates control the result.
3. Keep the normal unresolved production state honest until a separately
   authorized native integration task supplies real state. No fabricated running
   or connected game state. Production actions remain unavailable in this task.
4. Use an explicitly isolated preview/test entry point to render state variants.
   Keep fixtures out of native production behavior. Reuse the exact CSS/assets.
5. Capture fixed-viewport clone screenshots/DOM evidence for light/dark states,
   exact labels, visibility and relevant disabled rules; call them clone QA.
6. Update current UI master, Home matrix/ledger row, detailed review and handoff.
   Preserve Map behavior, native bridge/bootstrap and historical WebUI source.
7. If an exact Home rendering predicate consumes auth-related local state,
   inspect the needed state producer/consumer contract and record its locator.
   Do not stop solely because it touches auth-related code. Keep this task UI-only;
   there is no authorization for unrelated service/protocol research or bypass.
8. Use one canonical UI implementation; do not add legacy fallback behavior.
   Retirement of the existing host `--legacy-ui` switch is a separate task.

## Non-goals

No Last War/reference executable launch or desktop gameplay; no native launch,
close, repair, folder/config mutation or automatic reconnect; no Home backend
recovery/integration yet; no login/account/licensing UI, unrelated auth/service
research or access-control bypass; no other page
redesign/localization campaign; no Map scan/server-jump/plunder campaign;
no moving/deleting historical code/evidence; no new long campaign or subagents.

## Acceptance and bounded delivery

- Exact reference hash; source/locator table for every recovered Home predicate.
- Complete recovered Home state presentation with no changed default/runtime
  claims and no synthetic native results. Preview isolation is demonstrated.
- State QA verifies meaningful branch/label/visibility rules, rather than only
  searching the implementation for matching strings.
- `npm.cmd ci --prefix src/LWBridge.UI-0.3.17 --no-audit --no-fund` if needed.
- `npm.cmd run check --prefix src/LWBridge.UI-0.3.17`.
- `npm.cmd run build --prefix src/LWBridge.UI-0.3.17`.
- `npm.cmd run check:production-build --prefix src/LWBridge.UI-0.3.17`.
- `git diff --check`; inspect the diff and ensure no unintended Map/host change.
- Review: `docs/reviews/2026-10-01-LWB317-UI-HOME-STATES-001.md`.
- Evidence: `evidence/lwbridge-0.3.17/ui/LWB317-UI-HOME-STATES-001/`.
- One roughly 20-minute work block. Near minute 17-18 stop opening new branches,
  preserve a coherent checkpoint and document the exact remaining work. If more
  work is needed, report partial completion; do not launch a loop campaign.
- Commit/push verified coherent changes to `origin/research/offline-controller`;
  verify actual remote revision and clean tree. No force push/reset/clean.
- Return ID, status, source identities/locators, files/evidence, implemented
  branches, unknown/blocked states, exact checks/results, commit/remote equality,
  cleanup and next continuation. Do not call this original runtime pixel parity
  or a working game launcher. Await project-lead independent review.
