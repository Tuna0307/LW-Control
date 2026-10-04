# LWB317-UI-HOME-PREFERENCE-LIFETIME-001A

Status: ASSIGNED to the owner's manually relayed worker, 2026-10-04.
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
