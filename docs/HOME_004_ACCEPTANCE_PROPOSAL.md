# Whole Home — functional acceptance proposal

Current owner direction: [faithful functional replacement](OWNER_DIRECTION_FUNCTIONAL_REPLACEMENT_2026-10-10.md).
**Whole Home not yet lead accepted. PR #6 remains draft.**

The reference is LWBridge 0.3.17, but exact equality in every obscure original
state is no longer mandatory. The familiar interface, feature purpose and real
working results remain required. Document small differences and own-design
policies in [behavior differences](HOME_004_BEHAVIOR_DIFFERENCES.md).
The old 47 obligations/source matrices remain useful recovery history; they
are not 47 compulsory paired-original live experiments.

## Finite Home functional catalogue

Each gate below needs a current source/implementation, supported input/state,
actual verification, cleanup and explicit remaining limit. Report worker proof
separately from lead acceptance. Do not silently omit a shipped or expected control.

| ID | Feature | Required result and practical verification |
| --- | --- | --- |
| F-01 | Game folder/configuration | Detect/select a supported root, handle cancel/invalid choice without corrupting config, persist/reload, keep running installation ownership separate from a new configured folder. Verify actual native picker/storage wiring and controlled adverse cases. |
| F-02 | Manual Launch -> Connected -> Close | Actual packaged Home starts the real supported game, proves fresh authenticated readiness, displays accurate EN/JA state and closes the exact intended session. No false Connected, orphan relaunch, surviving journal or incorrect script restoration. |
| F-03 | Automatic Launch | Real startup follows the saved user preference and supported enabled profiles. OFF prevents automatic launch; repeated/manual overlapping Start does not create duplicates. Persistence and failure handling tested. |
| F-04 | Automatic Reconnection/recovery | Saved OFF/ON works. Recover actual unexpected exit, hang and supported transport-only loss; show honest progress/failure. Meaningful controlled checks cover thresholds, retry/backoff, disable, failure and cancellation. Stop during pending recovery leaves no successor. Original exact retry schedule is optional; verified reliable policy is mandatory. |
| F-05 | Host restart/adoption | Reopening the host retains/adopts the appropriate supported session without duplicate launch; exact process/build identity and per-profile state preserved. Verify genuine normal flow and controlled stale/corrupt record cases. |
| F-06 | Repair / Update-and-Launch | The advertised repair actually repairs the replacement bridge using a supported compatible input, then genuinely reconnects. Failure/cancel preserve recoverability and exact ownership. Inert success alone is insufficient; no official-game updater experiment is implied. |
| F-07 | Profile controls | Inventory every Home-associated profile control (selection, notes, ordering, enabled state, create/remove if present, per-profile Start/Close, batch controls). Native persistence/routing work; A/B mutations and late replies remain separate. Real concurrent-game capability must be verified if advertised. Original paid capacity is not a requirement; unsupported expected use stays an explicit gap. |
| F-08 | Preferences, status and errors | Correct immediate editing/persistence, latest acknowledged values, honest live/recovery status, useful localised errors, no permanently stuck busy state. Test normal native outcomes and controlled concurrent/failing saves in EN/light and JA/dark. Inventory related local automation settings without claiming unimplemented game-side actions work. |
| F-09 | Final package/integration | Review candidate diff/differences, pass affected native/pipe/frontend/Release/package checks; extract and run source-identified candidate, verify relevant Home native workflows, isolation and cleanup. One final ZIP after corrections settle. |

## Evidence standard and release decision

Actual current production positive paths must work. Controlled/inert failure
tests are acceptable engineering evidence for deliberately unsafe/rare negatives,
clearly labelled. A fixture is not a working game action. Existing useful genuine
receipts may be reused only when source changes do not invalidate their boundary.
Original screenshots/service responses are optional research where unavailable;
missing real client operation is still a functional gap.

An original detail can be unknown while a documented own-design method passes.
No unavailable feature or narrower silently substituted workflow passes.
Supported scope must be explicit; do not change whole-Home scope to make the
checklist green. Home is complete only when all included functionality passes.

The worker returns READY_FOR_LEAD_FUNCTIONAL_REVIEW or PARTIAL for actual remaining
operations/verification. The lead independently reviews and approves any main
merge/publication. This policy change has not accepted the current candidate.
Historical original-parity receipts and negative records remain untouched.
