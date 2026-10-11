# MAP-ACTION-PROVIDERS-007 — remaining Ghost/Treasure implementation

Latest lead disposition: scoped worker delivery at fbbfa983 is **CHANGES_REQUIRED**
for LEADMAP007-01/02. See ../reviews/2026-10-11-MAP-007-LEAD-REVIEW.md.
Correct those, then continue LWB317-MAP-SCAN-METHODS-008.md on this same branch.
Do not restart all historical provider recovery; owner now prioritises scanner
methods/accuracy and Normal/Fast. The assignment below is retained as background.

ASSIGNED for the owner's next relay. Repository:
`C:\Users\chimw\OneDrive\Desktop\Github\LW-Control`.
Start from current main containing accepted MAP-006 R1. Create/reuse only
`codex/map-action-providers-007` in the existing checkout; verify main is clean
and contains `70b952ab44dde084ea95e3c613b9fe2d702ab426`. No new worktree.
The old MAP-006 branch is merged; do not resume its completed release loop.

## Goal and intentional decisions

Finish the remaining Ghost/Treasure production providers and local workflows
where supported current-client evidence allows, so the owner can subsequently
verify real actions. Work on actual missing functionality, not another large
documentation-only campaign. Faithful functional replacement is the current
policy: original purpose/defaults/UI matter; tested own orchestration is allowed
and labelled OWN_DESIGN. Unknown encrypted-original details alone are not a
reason to leave a supported current-client operation unavailable.

Read AGENTS.md, OWNER_DIRECTION_FUNCTIONAL_REPLACEMENT_2026-10-10.md,
MAP_006_CURRENT_STATUS.md, MAP_006_OPERATION_MATRIX.md, MAP_006_LEAD_ACCEPTANCE_2026-10-11.md
and MAP_006_OWNER_ONLY_VERIFICATION_CHECKLIST.md. Inspect the real source before
assuming a prior research conclusion still applies. Older inspector/tools/evidence
may be read with git show research/offline-controller:<path>; do not merge research
or bulk-copy it. Relevant historical reviews are the 2026-10-07 provider-semantics,
blocker-boundaries and expiry-correlation worker/lead reports. Any v22 conclusions
must be checked against the admitted current v24 client where they affect behavior.

External worker works solo: no subagents, other AI chats, GPT Work or Codex delegation.
Use compact continuation/differences records and preserve all genuine negative evidence.

## Authority

This assignment is implementation/static/offline/controlled testing. Do not execute
live claims, plunder, sharing, marches, spending, combat or updater actions. Do not
arm any live action scheduler merely to test wiring. Owner will perform action
witnesses. Ghost positive acquisition is deferred to owner availability on
2026-10-15; full seasonal Treasure acceptance remains deferred. Do not presume
Season 4 eligibility from the owner's tentative explanation. No protected-service
access or authentication/entitlement bypass. No new live scan needed for this task.

## A — Ghost preparation and dedicated result path

Inspect CurrentClientMap317ActionProvider.PrepareGhostPlunderTasksAsync,
PrepareGhostPlunderRows, the scanner's Ghost projection, MapActionControlPlane,
MapPlunderWorker and the admitted client's actual task/request/result handlers.
Recover the valid hydration/normalization/eligibility fields and implement a real
preparation path where these can be established. Preserve positive ownerServer,
protection timing/counters and the accepted zero/non-positive expiry rule.
Preparation and execution are separate capabilities; missing terminal execution
correlation is not automatically a reason to forbid valid preparation.

Implement dedicated Ghost execution only if request semantics and terminal identity
can be established. Ghost must never enter Dispatch transport. Connection-local
future IDs are not durable identities across reconnect; bind any result to exact
profile/session/connection/job/attempt. Missing or ambiguous identity must fail
honestly. Preserve queued/cancelled/failed/succeeded distinctions and retire stale
results without affecting successors. Do not expose a success button backed by a stub.

## B — Treasure owned status and claim controller

Inspect GetTreasureClaimStatusAsync, ClaimTreasuresAsync, existing state inspection,
Map action contracts, UI consumers and admitted client claim/response/reward producers.
Implement supported status ownership/cache, batch progress and request orchestration
for the expected single/boxes/season modes, lucky priority, eligible targets, limits,
reset/cooldown and partial failures. Own controller design is permitted; document
user-visible differences without requiring the encrypted original controller.

Never count a sent request, accepted queue or launched march as a claimed reward.
Require the actual supported terminal claimed-state/reward edge; if that edge is
unavailable, keep only that execution capability pending and finish supported
inspection/status/preparation work. Cancellation, reconnect/profile replacement,
late callbacks and batch counters must remain truthful. No live claim is authorised.

## C — integration and owner readiness

Wire supported production providers through actual command handlers and mounted
controls. Add distinguishing inverse tests for changed logic through real boundaries,
controlled client hooks and decoded bodies where applicable. Cover incorrect/missing
identity, server/profile changes, concurrent requests, late success/failure, restart,
cancel/clear/retry and preserved completed data. Test isolation must prevent a real
game request even if a real owner game happens to be running.

Update the owner-only checklist to cover newly implemented actions and exact observable
success/failure/cleanup. Label controlled-only capabilities and live-pending rows.
Preserve accepted Home, six scanners, browse/export/marks, stable status polling,
Auto Stop/return and exact restoration. Do not tune scanner concurrency or reopen
the accepted finite speed comparison as unrelated work.

Diagnose -> supported fix -> distinguishing test -> affected regressions -> self-review
-> coherent checkpoint -> next ready item. Ordinary failures should be solved and
retested without another relay. If a required capability remains unavailable, identify
the exact input/result edge and supported approaches actually attempted; do not
produce fabricated data or endlessly restate old encrypted-original blockers.

Run applicable current gates after product changes:

- npm.cmd --prefix src/LWBridge.UI-0.3.17 run check
- npm.cmd --prefix src/LWBridge.UI-0.3.17 run build
- npm.cmd --prefix src/LWBridge.UI-0.3.17 run check:production-build
- dotnet run --project tests/LWBridge.Desktop.Checks -c Release
- dotnet run --project tools/home_004_pipe_recovery_probe/Probe.csproj -c Release
- dotnet build src/LWBridge.Desktop/LWBridge.Desktop.csproj -c Release
- new affected production-handler/controller checks and git diff --check

Use current packaged/mounted inert EN/light and JA/dark checks for changed controls.
One final source-identified Windows package only after useful code settles; reuse
unchanged accepted receipts, never generate a ZIP per small fix. Keep private game
inputs/data and credentials out of Git/package. Verify no task processes/jobs/roots
remain; preserve owner state and genuine historical failures.

Commit coherent changes, push normally, verify direct remote SHA, open one PR to main
and report implementation changes, controlled proof, genuine evidence (if any), live
pending owner checks, exact unresolved capabilities and continuation. Return
READY_FOR_LEAD_FUNCTIONAL_REVIEW only for the truthful supported scope; otherwise
PARTIAL with the exact remaining technical edge. Worker does not merge/publish.
Do not call whole Map complete while Ghost/Treasure or owner-only live gates remain.
