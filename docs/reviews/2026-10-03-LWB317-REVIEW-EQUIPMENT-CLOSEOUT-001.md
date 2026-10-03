# Equipment closeout — independent project-lead review

Decision: **REVIEW_COMPLETE / CHANGES_REQUIRED** for submitted commit
`4bafcd437929f2f905b557ca700657467afbdcbc`. The worker finished its delivery and
pushed the exact reviewed revision. It has not yet passed the Equipment acceptance
gate. The correction is a finite Equipment-only follow-up, not a restart.

## Confirmed defect: effects remain active in the hidden tab

Current `SquadsPage` retains Equipment in a `display:none` div. Its window keydown
effect stays installed after Equipment → AFK. Executing the actual parent tab
callback and actual Equipment effect, then Alt+2 on BODY, still prevents default
and changes the hidden Equipment preview action to
`apply-all:equipment-preset-fixed-2`. This is P2 UI interaction parity, even though
the preview action is inert and no gameplay/provider operation was invoked.

Original `SquadPanel-HC3-DJei.js` function `pd`, UTF-8 byte 191313, length 1517,
retains visited tabs inside `O.Activity` visible/hidden boundaries. These retain
state while suspending effects in hidden mode. Original `fd` Alt effect's call
starts at UTF-8 byte 179906 (its callback starts at 179922) and removes its listener
on cleanup. The independent runner executes that cleanup and confirms removal.
Installed React 19.3.0 exports `Activity`; no dependency replacement is needed.
The lead runner is inert proof of the actual callbacks/effects and original
boundary, not a full React DOM/browser lifecycle test. The R1 worker must exercise
the corrected mounted boundary as well as state retention.

## Missing rename acknowledgement states

Original `fd.ye`, byte 180434, length 234, sets busy `rename`, awaits `P`, closes
only on a truthy acknowledgement, and clears busy in finally. `fd.P` edits the
draft immediately and waits for `flush(false)`, returning null on rejection.
The independent runner confirms the original stays open/busy while pending and
stays open after a failed/null acknowledgement. Current `saveRename` immediately
confirms every preset and closes, with no actual pending/failure producer.

The delivered happy callback and pre-busy fixture do not prove this lifecycle.
This is a recoverable frontend branch within the assigned rename scope. R1 must
recover/implement and test local acknowledgement-driven pending/success/failure
states without adding a native Equipment save provider. Do not treat native
persistence exclusion as evidence that these UI states match.

## Verification and evidence qualification

- Lead reran the Equipment checker: 105 assertions pass. The harness stubs
  `useEffect`; its two timer/Alt checks are source markers. Therefore those passes
  do not cover tab suspension or mounted effects.
- All nine dispatch baseline predicates currently pass, but they are source-marker
  checks. Preserve their immutable failing baseline; do not present them as nine
  executed original/current behavior cases.
- Affected AFK checker: 729 cases pass; recovered Join/Assist renderers pass.
- Canonical `check` and current `check:production-build` pass. The worker build
  record is retained; this review changed no production code.
- All seven protected WIP hashes pass. No native/gameplay control was executed.
- Actual EXE identity, six source assets, thirteen byte slices, current production
  hashes and baseline JSON were independently verified. The lead validator pins
  the worker packet, all three images and independent results. The worker packet
  lacks its assigned executable evidence validator: `check-closeout.mjs` produces
  a manifest with `--record` but does not validate the existing evidence. R1 should
  add a validator for its new packet; do not rewrite historical manifests.

The three saved screenshots were inspected. They show the rename modal and
Japanese dark/narrow layout. The dark image does not expose the partial-result
panel below the fold; its browser record is a separate worker observation. No
independent browser interaction was performed by this lead review. Physical HTML5
drag, original post-auth pixels, native assets/providers/persistence/gameplay remain
unverified. `App.jsx` clears preview state outside preview bridge mode, so no new
fixture/native separation escape was found.

## Continuation

`docs/work-items/LWB317-UI-EQUIPMENT-CLOSEOUT-001-R1.md` is the next medium worker
assignment. Fix hidden-tab effect suspension, close the local rename acknowledgement
gap, and validate the new evidence. Reuse the passing transformation/AFK proofs.
Equipment parent is CHANGES_REQUIRED; City Layout and the remaining finish queue
have not started. Full UI/native/pixel acceptance is unchanged.

Evidence: `evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-EQUIPMENT-CLOSEOUT-001/`.
`check-review.mjs` intentionally reproduces the reviewed failing implementation;
its CHANGES_REQUIRED output is historical evidence, not a future corrected test.
Preserve it and add distinguishing original/submitted/corrected cases in R1.
