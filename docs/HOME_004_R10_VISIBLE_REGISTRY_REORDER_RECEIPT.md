# HOME-004 R10 — visible profile order after native reorder + view switch

Date 2026-10-10. Current owner-relayed Home-only solo continuation.
Starts at R9 source `8630dafcb8f031146b173280289026ad9cb03e78`.
Reference original LWBridge 0.3.17, EXE SHA256
`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
Whole Home **PARTIAL_NEEDS_INPUT**; draft PR #6, no merge/release.

## Separate actual H-39/H-45 defect after R9 native reply fix

Original 0.3.17 frontend `Yr` profile sidebar invokes its profile
registry `r.reorder` when dragging an account before another (original
`index-BVfnK1wp.js`; H-39 archive references). The clone's React
`ProfileSidebar` correctly invokes `reorderNativeProfiles`, but
`App.jsx` used one `nativeProfileRequestRef` for both order updates
and selecting a profile. R9 fixed native `profile_reorder` returning an
independent successful result, yet if B selection happened while
reordering B ahead of A, the frontend *discarded* that older request's
acknowledgement altogether. The selected Home view was correctly B,
but the visible sidebar stayed A,B despite the real registry now
persisting B,A. This is a current-client UI/native consistency defect,
not an original commercial capacity witness.

**Actual production WebView/native negative**, not a unit-level mock:
expand real sidebar with A/B inert owners; dispatch actual DragStart
for B then accepted Drop on A (React `onDragStart` and `onDrop`);
hold the resulting `profile_reorder` before native dispatch; select
B through the actual row while reorder remains pending; release native
reorder. The SQLite registry verifies order B,A and the native selected
owner B, but the mounted DOM failed to show reordered B first.

Immutable ignored negative:
`artifacts/home-004/r10-visual-reorder-inverse-2-en-light.json.error.txt`,
`R4 mounted Home did not reach R10 completed registry order visible
on still selected B`. An earlier attempt used the boolean return of
cancelable `dispatchEvent` to detect Drop; React correctly called
`preventDefault` making it false. The harness was corrected to
require the actual native reorder command rather than treating the
handled default-prevented Drop as a product failure. Both records
remain preserved; only the second is the distinguishing product inverse.

## Minimal exact-owner correction

`App.jsx` `reorderNativeProfiles` now independently tracks the
latest native reorder revision. When the global selected-profile
request generation has advanced because B was selected, it keeps the
selection result fenced but applies **only the authoritative ordered
profile IDs** to the current set of React profile objects. It preserves
B as selected, each profile's current exact note/metadata and all
profile-owned settings; it refuses an inconsistent membership set
rather than silently inventing a profile. Superseded older reorder
results cannot replace a newer reorder. The normal same-generation
full native acknowledgement path is unchanged.

Expanded actual mounted EN/light and JA/dark proof now confirms:

- React DragStart+Drop B ahead of A yields the actual native command.
- B selection while native reorder is pending remains independent.
- After completion, **both durable registry and the visible DOM show B,A**
  with B still the active owner.
- Test-only display order is exactly restored A,B; A selected again.
- Existing R9 note/reorder success through native A→B still passes,
  as do R8 captured-owner Home Close across B/ABA, R7 per-profile
  busy, R6 reverse error priority and manual/global repair distinction,
  retained owner Start/Stop/recovery, exact A/B host teardown and
  no genuine game launches or Map scans.

Final generated ignored receipts:
`artifacts/home-004/r10-reorder-visual-corrected-en-light.json`,
`artifacts/home-004/r10-reorder-visual-corrected-ja-dark.json`.
Both report `sidebarProfileCommands.sidebarReorderVisibleAfterSelection=true`,
both inert owners stopped, native requests/subscriptions zero, root
removed and cleanup failures empty.

## Verification and acceptance limit

Run canonical frontend and affected native/Home/Map regressions,
real concurrent Windows named-pipe and typed launcher checks if
changed, one source-identified Release package, extracted inert
Home smoke and isolated normal GUI exact identity/cleanup.
The ignored `artifacts/home-004/r10-final-package-receipt.json`
records final identity.

Source-backed original profile reorder caller and real clone
WebView/native/SQLite/UI consistency are confirmed. Original 0.3.17
exact late profile-select/reorder reply behavior, commercial entitlement,
supported simultaneous real clients and adverse original/current
EN/JA conditional pixels remain **unavailable**, not claimed.
All other unresolved H-IDs, original/protected inputs and permitted
alternatives remain as recorded in [47-row queue](HOME_004_R4_QUEUE.md),
[R9 original-input receipt](HOME_004_R9_INDEPENDENT_REGISTRY_REPLY_RECEIPT.md)
and [R8 original-source receipt](HOME_004_R8_CAPTURED_CLOSE_AND_HOME_CONTRACT_RECEIPT.md).
Whole Home **PARTIAL_NEEDS_INPUT** until the genuinely paired original
and working-package gates pass independent lead review.
