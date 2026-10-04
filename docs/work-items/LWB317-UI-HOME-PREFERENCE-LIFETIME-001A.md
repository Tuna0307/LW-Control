# LWB317-UI-HOME-PREFERENCE-LIFETIME-001A

Status: COMPLETE / ACCEPTED by the project lead, 2026-10-04.
Reviewed implementation: c0d9082ee8f8b4b8985e8025657967a6898a4097.
Decision and proof limits: docs/reviews/2026-10-04-LWB317-REVIEW-HOME-PREFERENCE-LIFETIME-001A.md.
Parent HOME-PREFERENCE-LIFETIME-001 remains PARTIAL. This medium unit covers
only Open games at startup / Auto Launch. Automatic Reconnection is a later unit.

Goal: recover and reproduce the original local Auto Launch preference default,
storage and immediate visible value, including a second edit during an unfinished
existing native persistence request. Correct saving-only disablement. Preserve
unavailable-provider fencing and all unrelated Home behavior.

Read AGENTS.md, docs/AI_WORK_PROTOCOL.md, parent work item and the current
OFFLINE-VISUAL packet README/independent-review/home evidence. Starting lead
checkpoint is74cc52a76e5e7f03641b07e10a414aacec747e8d. Confirm actual HEAD/status;
do not reset changes or assume the branch has not advanced.

Exact source anchors:main qr336694/Bn213332; local setter248381 and Sr246842.
Recover the storage key/default and authoritative producer/consumer from bytes.
Current App updateAutoLaunch awaits local_config_set before updating localConfig;
Home disables the switch when busy is autoLaunchGame. Both distinguish from the
original synchronous storage/state setter. Auto Reconnect uses a different
profile draft contract and must not be folded into the same preference abstraction.

Allowed production scope:App.jsx, HomePage.jsx and a focused local preference
helper if justified. Preserve all Auto Reconnect code/disabled predicates/transport
behavior, root/action error isolation, picker/ack/cancel lifetime, profile selections,
Map polling/ownership, lazy/Activity/motion and native command/result contracts.
No new native provider, command, game action, login/auth bypass or fallback.
Do not turn a failed native persistence request into reported success. Distinguish
the source-local preference value from separately unproved native persistence;
document the adapter policy and verify error/ack effects rather than guessing.

Work alone; no subagents. Sequential milestones:
A exact recovery plus immutable actual baseline; B canonical correction and
actual callback/mounted proof; C affected checks, real inert browser QA, evidence,
dated review, delivery/handoff checkpoint and commit/push/remote verification.
No elapsed-time stop; do not broaden into Automatic Reconnection or visual queue.

Verify default/storage/reload, immediate off→on→off while a first request is pending,
late/out-of-order acknowledgements, rejection, unrelated config preservation,
five-second config polling and profile replacement. Add only source-distinguishing
cases. Positive/deferred native responses must be controlled and inert; preview
and unavailable native actions remain fenced. Use actual original setter and actual
production callbacks/rendering, not duplicated pseudocode or manual snapshots.

Run canonical check/build/production-package, affected Home/root-ack/translation/
switch/current-App checks and protected guard. Historical scripts/records may pin
old shapes/hashes:preserve them; add narrow current adapters/results when needed.
Do not rerun historical record writers or modify old assertions to force passes.

Protect the seven existing WIP paths and additionally the two untracked vendor
LICENSE/NOTICE MD files. Inventory/pin at start; none may be staged or edited.
Preserve existing preview servers/tabs/session ownership. Avoid JavaScript confirm
or destructive UI controls during testing; no Last War process may be launched.

Write evidence under LWB317-UI-HOME-PREFERENCE-LIFETIME-001A and a dated review.
Update only relevant delivery/handoff notes; do not announce global UI completion
or change earlier accepted scopes without exact counter-evidence. Review the full
diff, commit explicit owned paths, push research/offline-controller and verify SHA.
Return AWAITING_REVIEW with actual files/checks/source locators/proof limits/full
SHA and exact continuation. The project lead reviews; stop at this unit.

## Delivery — 2026-10-04

State: **AWAITING_REVIEW**. Implementation and worker verification complete; project
lead review remains external to this unit.

Recovered exact source contract is recorded under
`evidence/lwbridge-0.3.17/ui/LWB317-UI-HOME-PREFERENCE-LIFETIME-001A/`:
key `lwbridge.autoLaunchGame` at byte 246758; storage/get/set block 246750;
`useState(xr)` 247632; synchronous storage-then-state setter 248381; Home binding
338639; switch `Bn` 213332. Missing storage defaults true and only literal `"false"`
reloads false.

Production correction is bounded to `App.jsx`, `HomePage.jsx` and new focused
`autoLaunchPreference.js`. Auto Launch now owns the recovered local storage/state
value immediately and remains editable while the existing `local_config_set` mirror
is pending. Native writes are serialized and revision-fenced; the latest failed write
rolls storage/UI back to the last native-confirmed value and reports the error. Polling
may update that rollback reference but cannot replace the visible local value.

Current mounted proof passes seven cases, including off→on→off under a deferred first
request, stale success/rejection, latest rejection, exact default/reload, five-second
polling, late stale-poll fencing after a native acknowledgement, profile replacement
and unrelated Auto Reconnect preservation. Real inert
browser QA passes deferred success and rejection flows with zero clean-page console
errors. Dispatch preservation comparison passes ten unrelated callbacks plus root,
profile, retained-page, listener/timer and Auto Reconnect invariants.

Canonical `check`, production `build`, and `check:production-build` pass. Build/package
fingerprints: `7c31d29fbab27d5f785f97323e7a4f9824f74ad7400358b74a2372482019fedc` /
`f1e58a73dc134eb4436e91187ef35d241a4a95e2cf041b9c04196fe3a9fdaa8d`.
The task WIP guard passes all ten assignment-start dirty/untracked files, including
all seven protected paths and both required vendor LICENSE/NOTICE files.

Historical HOME-ERROR-001 and SWITCH-LOCALE execution checkers are preserved and
topology-stale before this unit (old inline App preview shape / old Pages-owned
ToggleRow); their saved validators pass. Current adapters exercise the actual current
App/Home/shared switch rather than rewriting historical evidence.

Continuation: lead acceptance is complete. The separate Automatic Reconnection
unit is assigned under `LWB317-UI-HOME-PREFERENCE-LIFETIME-001B` through the owner.
The worker delivery above is historical; this acceptance does not establish live
native persistence, original failure policy or protected-original pixel parity.
