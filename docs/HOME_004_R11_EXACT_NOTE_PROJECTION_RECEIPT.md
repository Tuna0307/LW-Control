# HOME-004 R11 — exact-owner note projection after selected-view change

Date 2026-10-10. R10 source baseline:
`a052a7d5670f97c6347842e4d7ec3fed39e52c74`.
Original target LWBridge 0.3.17, EXE SHA256
`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
Work solo on `codex/home-complete-delivery-004`, draft PR #6.
**Whole Home remains PARTIAL_NEEDS_INPUT**.

## Audit and actual before-fix negative

Compared archived original 0.3.17 `Yr` sidebar source note editor:
the dialog invokes `r.updateNote(profileId, note)` and closes only after
its promise resolves. Native original `profile_note_set` validates the
profile ID first (0x1a4d26); the cloned note operation is persistent
and explicit-owner, distinct from the selected Home view.

R9 fixed native A note mutation falsely returning
`PROFILE_GENERATION_RETIRED` after B selection. The React
`App.jsx updateNativeProfileNote` still discarded a successful native
note acknowledgement when a newer selection incremented the shared
`nativeProfileRequestRef`. The real dialog closed as successful, SQLite
stored new note for **A**, but sidebar retained **old A note** while
B was selected.

**Production native/WebView inverse** uses actual rendered React JSX
profile A note Edit, input change and Submit, held native
`profile_note_set` before dispatch, and a programmatically activated
actual React B select handler while the modal is pending. Releasing
the native mutation then confirms persistence and dialog close and
demands the exact new A note visible on its own sidebar row with
B still selected and unchanged. Activation while the modal is pending
is an intentionally **controlled scheduling inverse**, not a claim
that a human can click through a modal. A normal native/host selection
can interleave with a held registry mutation.

Immutable ignored before-fix negative:
`artifacts/home-004/r11-native-note-ui-inverse-en-light.json.error.txt`
reported `R11 committed A note visibly owned by A with B still
selected` timeout; the test verified SQLite saved the new A note and
dialog closed before that assertion. No original commercial service,
real game, Map scan or dangerous updater was touched.

## Minimal correction and retained guarantees

`App.jsx` tracks latest note mutation revision per **exact profile
ID**. An older native note acknowledgement superseded *only by a
newer selected-view request* now applies the saved note field to its
same exact profile in current React state, preserving the selected B
view, profile order, other owners' notes and all current metadata.
Older superseded notes cannot replace newer same-owner edits.
The usual same-request full native snapshot flow is unchanged.
R10 order-only acknowledgement, R9 native independent registry
replies, R8 clicked-owner A Close across B/ABA, R7 per-owner busy,
R6 original reversed error priority and H-18 first-global versus H-23
selected-only repair remain intact. H-39/H-40/H-45 are the affected
bounded obligations.

Actual corrected mounted EN/light receipt
`artifacts/home-004/r11-note-visible-final-en-light.json` PASS.
JA/dark initial run reached an existing late B-only repair assertion;
its failure record `artifacts/home-004/r11-note-visible-final-ja-dark.json.error.txt`
was preserved. Diagnostic retry exposed a distinct transient mounted
selector-click timing before React had enabled the compact row;
the test helper now **waits for enabled real target** before clicking,
without loosening any native ownership or error assertions.
Corrected JA/dark production mounted receipt:
`artifacts/home-004/r11-note-visible-final-ja-dark-retest.json` PASS.
Source check in `check-home-integration.mjs` requires an
exact-owner latest-note revision and note-only field projection.

The corrected WebView tests are controlled/inert and **not** paired
original 0.3.17 concurrent note/UI behavior; that genuine original
observation is not available. Protected ticket/lease/finalizer/
controller/capacity, supported multi-client operations, original
launcher/update failure, offline-only and maintenance fault traces
and original conditional EN/JA pixels remain per the complete
47-row [queue](HOME_004_R4_QUEUE.md) and the R8–R10 receipts.
Do not infer whole original Home acceptance from these tests.

Commit/source package, actual ZIP/EXE hashes, direct origin, remaining
validation/PR status and task-owned cleanup are recorded separately
only after successful verification. No merge or published release.
