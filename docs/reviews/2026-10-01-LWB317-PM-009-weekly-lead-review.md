# PM-009 — weekly settings lead review

Date: 2026-10-01. Reviewed delivery:
`2f439ced3b867fcb504f48fb081aefccea980eaf`.
Disposition: **CHANGES_REQUIRED**, limited to R1 below and a small locator cleanup.

Local HEAD and direct remote lookup match that commit. Existing uncommitted
previewAfkFixtures.js, .scratch-lwb317 and parent screenshots were preserved.
No product source was changed by this lead review. No game or native provider was
launched; no new physical browser reproduction was performed during this review.

## Retained verified progress

The source-backed removal of config.saving from the weekly selector lock is correct.
AutomationPanel-BJ0gIqFh.js at UTF-8 byte 32571 defines task-disabled as offline or
matching task-busy, and the weekly renderer at 33030 adds its runtime-running flag.
Config saving itself is absent. The shared error component does separately lock
Retry/Discard while saving. Exact source contexts were read directly, not inferred
from the submitted explanation.

Monday–Sunday order, both default arrays, options, controlled one-day edits and
local retry/discard/store-generation checks pass. Canonical npm check and the
focused check-weekly-state.mjs were independently rerun. Existing production
package validation passes the reported source/artifact fingerprints:
`6a4fc5e801931f1aff9f42c9109d8facb5ac8ea7dadcd9a2dd5c4c60d7acabdb` /
`fac008d67b2792cf10bb1957cf71960beef2b9f9e871c7dcf1d103b08cc2391f`.
The reference executable SHA-256 was rechecked against AGENTS.md. All three
submitted source-file hashes and both screenshot hashes match; the save-error
screenshot visibly supports the recorded error presentation. Worker browser
outcomes are retained as worker evidence, not newly independent browser proof.

## R1 — actual weekly UI change does not start the original immediate write

Original source AutomationPanel-BJ0gIqFh.js:

- UTF-8 byte 33073: weekly day change calls `W(e,{weeklyQualities:r})`.
- UTF-8 byte 27880: W calls edit with debounce disabled, then immediately flushes
  the task store and handles rejection.

Current Pages.jsx lines 430 and 442 instead call only config.store.edit in both
WeeklyQualityPreview onChange callbacks. The store's default is a 400ms timer;
the surrounding wrapper also flushes on blur. Neither is the original immediate
change handler. This concerns dispatch timing while the select stays focused.

Independent reproduction invokes the actual JSX callback expressions extracted
with the installed Babel parser, using the real createConfigDraft and a deferred
writer. No flush/blur/timer wait is manually added. Both callbacks produce a dirty
edited draft but **zero immediate writes and saving=false**, whereas the source
requires one started write. Reproduction exits 1 for this expected mismatch.

Harness runtime: Node v24.18.0, installed @babel/parser 7.29.9 from the canonical
UI dependencies. No recovery tool installation or original bundle execution.

Evidence:
`evidence/lwbridge-0.3.17/ui/LWB317-UI-CORRECT-003A/lead-review/check-weekly-onchange.mjs`
and `onchange-baseline.json`.

The submitted deferred-store test manually calls edit(...,false) and flush(false).
It verifies the store but bypasses the actual UI callback, so its passing result
does not close R1. Fix only both weekly callback paths; retain selector editability
during saving, no global debounce change and no unrelated form changes.

## Locator cleanup

Two index-BVfnK1wp.js offsets point inside their excerpts rather than at the excerpt
start. Retry is recorded at 199469 while the excerpt begins at 199457; Discard is
recorded at 199603 while its excerpt begins at 199591. The behavioral claim and
source bytes are correct, but the records need an explicit anchor or consistent
excerpt-start offsets for mechanical validation. The lead evidence verifier
reports this precisely; all other supplied byte anchors validate.

## Continuation

Assign LWB317-UI-CORRECT-003A-R1: immediate weekly change writes, actual-handler
regression proof and the two locator records only. Do not begin Trade/Assist/AFK
or resume the parent backlog. CORRECT-003 remains PARTIAL; UI-CORRECT-003A is
CHANGES_REQUIRED until this focused correction returns. Original pixels/native
persistence/full product parity remain unproved.
