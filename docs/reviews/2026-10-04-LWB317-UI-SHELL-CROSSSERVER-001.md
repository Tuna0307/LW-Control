# Worker delivery awaiting review — Cross-server popover, 2026-10-04

Decision: **AWAITING_REVIEW** for `LWB317-UI-SHELL-CROSSSERVER-001`.
Project-lead acceptance is not claimed. Whole-product native/original-pixel status is unchanged.

## Delivered behavior

The worker recovered the exact verified 0.3.17 shell bytes before editing the
canonical UI. Original source locators and hashes are in
`evidence/lwbridge-0.3.17/ui/LWB317-UI-SHELL-CROSSSERVER-001/recovery/source-locators.json`;
the distinguishing branch matrix is beside it. The immutable `55c9ca1` App blob and
dispatch App normalized equal before correction, so the recovery packet demonstrates
the mismatch against a single historical/current baseline rather than manufacturing
a code delta.

The recovered original uses document `pointerdown` to close outside the header root,
does not implement Escape/autofocus/focus trapping, keeps the number input and trigger
enabled during offline/scan/busy states, gates Enter through the same availability
predicate as action buttons, translates stored errors at event time, resets/imports
history on profile identity changes and normalizes legacy history to five valid unique
IDs. Season/plunderable current-server buttons are disabled; recent buttons are not
current-special-cased.

Its acknowledgement order is `server_jump` -> awaited `map_summary` refresh ->, when
changed, awaited history persistence -> clear input/close. Unchanged results still wait
for summary before close. Summary refresh failure is caught at the parent boundary and
does not become a popover action error; jump/history failures keep the popover/input and
show the event-time localized action failure. The current implementation now follows
those contracts and no longer runs an extra `refreshStatus` in this action path.

## Verification

Checkpoint A recovery/evidence is `a74aba9b3a5abaf7a12979c7938e8bcc2ca465c7`.
Checkpoint B implementation/focused proof is `5318467c43598993b8c9cad499643e037fbc7dde`.
Both were pushed and directly verified against `refs/heads/research/offline-controller`
at their delivery point.

The post-fix actual-App mount passes 12 focused cases. A task-local current regression
adapter passes three affected boundaries: accepted retained-page/profile structure,
parent Map status/summary/scan ownership, and Home auto-reconnect acknowledgement via
the real controlled `backendBridge`. Canonical package check, build,
`check:production-build`, the seven-file protected-WIP guard and repository diff check
pass. Historical result files are not rewritten.

Bounded real browser QA stayed in true offline Preview mode. EN/light and JA/dark open
states, offline Enter, deliberate no-Escape close, Map navigation click-away, Home
return/input retention and a directly measured 800x543 narrow CSS viewport pass. Three
JPEGs were saved and inspected. The main 1920x855 CSS viewport is distinct from the
connector's 1600x713 scaled JPEG exports; the narrow capture is native 800x543. Both
fresh browser consoles have zero warnings/errors.

Evidence README:
`evidence/lwbridge-0.3.17/ui/LWB317-UI-SHELL-CROSSSERVER-001/README.md`.
Current validator:
`evidence/lwbridge-0.3.17/ui/LWB317-UI-SHELL-CROSSSERVER-001/milestone-c/validate-current.mjs`.

## Limits and lead continuation

No real Last War/server jump/scan/gameplay/native-provider action was performed.
Positive, unchanged, deferred and failure action branches use actual production
handlers/mounts with controlled inert responses. Direct original post-auth runtime
pixels remain unavailable, and this focused unit does not accept broader profile
architecture, Map notices/scheduling, other pages, final inventory or native functions.

Project lead should inspect this review and the evidence README, run the current
validator/current-check runner and protected-WIP guard from the pushed delivery SHA,
review the owned App diff against the recovery matrix, then accept or return focused
findings. Do not restart accepted SHELL-RETENTION-001 or broaden this review into the
later shared-shell/final-inventory campaign.
