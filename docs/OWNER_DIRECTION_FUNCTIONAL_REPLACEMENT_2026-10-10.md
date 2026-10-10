# Owner direction — faithful, fully functional replacement

Date: 2026-10-10. Applies prospectively to all feature work and releases.
This is the current product direction, not a claim that existing code is accepted.

## Owner clarification

The owner explained that 1:1 was requested because they had not used the original
and depended on the AIs to discover its behavior. The owner now permits slight
differences if our method is better, wants every feature to work, and has asked
the lead to tidy the project for this revised direction.

The new target is a **faithful, reliable, fully functional replacement** using
LWBridge 0.3.17 as the behavior/design reference and the verified current game as
the compatibility target. Exact equality in every obscure original state is no
longer a universal release gate. Do not replace it with an arbitrary percentage.

## What remains required

- Discover original feature purpose, workflow, controls and results from original
  artifacts where available. Preserve the familiar UI and established workflows.
- Every included released feature performs its advertised action through the
  actual supported production path. Settings persist, outputs are correct and
  independent profiles remain separate. Stop, cleanup and restoration must work.
- Reliability and truthful success are mandatory. Disabled providers, placeholders,
  simulated Connected, archived data or clone-only tests cannot establish a
  missing live capability. Controlled adverse tests remain useful, clearly labelled.
- Keep login/licensing UI and the original commercial account system excluded.
  No credentials, protected-service access or access-control bypass is authorised.
- Use one canonical implementation, with no pretend-success fallback.

## Differences now permitted

Internal mechanisms, reasonable retry/timing/error handling and current-game
adaptations may differ when they preserve the feature's purpose and provide a
verified benefit or a supported working implementation of an unknown original
detail. These choices no longer need to wait for inaccessible original code.
Do not call a method better than an unknown original algorithm without evidence;
state the measured improvement or the supported working behavior instead.

Record the original evidence/unknown, chosen behavior, reason, user-visible effect
and verification in HOME_004_BEHAVIOR_DIFFERENCES.md or the relevant future feature
ledger. Mark OWN_DESIGN explicitly; do not label it recovered original parity.
General permission for minor improvements does not require repeated owner approval
of each technical choice. The lead verifies them independently.

Removing an expected feature, changing its purpose or meaning of success,
introducing spending/gameplay actions, or silently narrowing supported use is
not a minor improvement. Identify the gap and seek owner direction where needed;
do not count it as complete by relabelling it.

## What counts as a blocker now

A missing original artifact is a functional blocker only when a specifically
traced required operation cannot be implemented and verified using available
legitimate sources/current-client interfaces. Otherwise retain it as an original
parity research gap, document the supported implementation and proceed.

Original commercial tickets, exact licensed limits, finalizer response identity
and protected-runtime pixel pairing are not automatic gates for a local working
replacement. Actual current-client multi-owner support, accurate state, successful
repair and dependable recovery remain real technical obligations where included.
Do not reclassify an operational failure as a historical research gap.

## Delivery and existing boundaries

Home first, then subsequent features by lead assignment. One finite functional
acceptance checklist replaces endless exact-parity campaigns. Work in the existing
branch/checkout, preserve evidence and continue the diagnose/fix/retest loop.
Build one final candidate when the ready correction pass settles.

Main receives independently verified working features with explicit scope and
documented differences; complete exact-original proof is no longer required for
every detail. The owner has not authorised an automatic merge in this direction
change. PR #6 and whole-Home status remain pending lead functional review.

Existing live scope, identities, isolated roots, backup/restoration, explicit
pause and actual approval-denial boundaries are unchanged. No new Map scans,
unrelated gameplay, updater or protected-service activity is authorised here.
Historical source facts, negative evidence and previous acceptance receipts stay
unchanged. This direction supersedes conflicting strict A->A acceptance rules
in earlier AGENTS, work items, queues and research documents.
