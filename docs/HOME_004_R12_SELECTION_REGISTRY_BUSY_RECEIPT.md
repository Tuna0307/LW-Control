# HOME-004 R12 — selection acknowledgement independent of registry reorder

Date 2026-10-10. Original target LWBridge 0.3.17, original EXE
SHA256 `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
R11 starting SHA `0b6282db76c93386e19d761d7a10123b709a1118`, existing
solo branch `codex/home-complete-delivery-004`, draft PR #6.
**Whole Home PARTIAL_NEEDS_INPUT**, not original complete.

## Audit and distinguishing mounted production failure

Read original 0.3.17 `Yr` sidebar profile selection/dragging, source
`index-BVfnK1wp.js`; its persistent order and selected profile
operations are independent Home responsibilities. Reviewed 47-row
queue, R6 original reversed error code choice, R8 captured-owner
Close, R9 independent native note/reorder, R10 order-only projection,
R11 note-only projection, original sidebar three-second poll and
batch ordering, native dispatcher/registry and protected launcher
gates. This is a current-client native/WebView consistency fix,
**not** a matched original commercial race witness.

R11 `App.jsx selectNativeProfile` incremented a single
`nativeProfileRequestRef`, also incremented by profile reorder and
note operations. Selection `then`, `catch`, and critically `finally`
would accept/release its result **only** if this global registry
counter had not advanced. Consequently an independent registry
mutation finishing during B selection could both silently abandon
B's selected-owner acknowledgement and leave `nativeProfileBusy`
indefinitely true, visibly disabling the sidebar.

Actual production mounted native/WebView inverse:

1. Start B's real JSX sidebar drag and hold `profile_select(B)`
   before actual native dispatcher execution with selected A.
2. While the selection is pending, finish real JSX B-before-A Drop;
   verified native registry persists B,A independently.
3. Release held B native selection, demand selected B in **both**
   backend and actual React row, B,A DOM/SQLite matching, and
   no busy/disabled profile controls.
4. Restore native A,B profile order, select A through actual JSX,
   verify visible active A and compact sidebar, then continue all
   original R4–R11 checks.

Preserved ignored before-fix product inverse:
`artifacts/home-004/r12-selection-reorder-busy-inverse-2-en-light.json.error.txt`,
`R12 exact selected B and persisted B,A order visible` timeout,
even though dispatcher ultimately selected B and registry stored
B,A. Prior `r12-selection-reorder-busy-inverse-en-light.json.error.txt`
was a harness-only attempt to hold the unsupported
`profile_select` on-reply path; replaced with documented correct
before-dispatch hold and **not rewritten**.

## Exact narrow correction and tests

`App.jsx` retains the existing global metadata snapshot revision,
but adds `nativeProfileSelectionRevisionRef` to govern only
selected-owner `profile_select` acknowledgement, error and busy
release. A registry note/reorder no longer retires a legitimate
latest B selection, even if its metadata snapshot finishes later.
A **newer selection** alone may retire an old selection; latest
selection still checks native expected exact B and cannot adopt
wrong-owner success. Registry operations retain R10/R11
field-only projection, native per-profile safety and existing busy
admission. Added source check requiring selection-specific revision
for success and cleanup and forbidding global request-based busy
release.

Actual mounted EN/light `artifacts/home-004/r12-final-en-light.json`
and JA/dark `r12-final-ja-dark.json` both PASS. Exact B selected,
SQLite+visible order B,A and busy released, then A,B plus A selection
restored. R6 global/manual error, R7 independent per-owner busy,
R8 A Close after B/ABA, R9 independent registry replies, R10
visible order and R11 exact note remain true. Both inert owners
stopped; zero native requests/subscriptions or isolated roots
remain, zero Last War game launches and zero Map scans.

The first corrected run reached the *cleanup* step and failed
because the harness directly reset native order A,B then expected
React to update without a UI operation. Kept the failed
`r12-selection-reorder-corrected-en-light.json.error.txt`.
The harness now checks native A,B reset, executes actual selection
A to refresh the UI and demands visible A,B/active A; no original
production invariant or negative assertion was weakened.

## Remaining original/genuine gates

Full H-01–H-47 queue still controls acceptance. H-05/H-13/H-28
protected ticket/lease/finalizer; H-22/H-38 licensed capacity;
H-33/H-36 actual signed controller; H-06–09/H-18–20/H-44/H-46
authentic official launcher restart/update/Lua/descriptor/mutex
failure producers; H-24/H-29/H-37–40 genuinely supported concurrent
installations/original encrypted transport; row-specific original
and current-client paired offline-only, maintenance, pending Stop,
recovery failure and conditional original EN/JA pixel states
remain unverified. Archive/static/frontend/native inert A/B tests
cannot substitute for those legitimate inputs. No bypass, simulated
licensed response, game manipulation, unsafe updater, new Map work,
merge or release publication.

Final commit, direct origin, source-identified Windows ZIP/EXE
fingerprints, extracted Home, normal GUI PID and exact cleanup
are recorded after successful packaging in generated ignored
`artifacts/home-004/r12-final-package-receipt.json`.
