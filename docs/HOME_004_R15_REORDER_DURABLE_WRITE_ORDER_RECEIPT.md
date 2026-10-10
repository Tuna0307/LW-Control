# HOME-004 R15 — concurrent JSX reorders must commit in submit order

Date: 2026-10-10. Assigned whole-Home `HOME-COMPLETE-DELIVERY-004`,
original LWBridge **0.3.17** EXE SHA256
`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
Starting last delivered, clean local and direct-origin R14
`68834c148e774118efaea0fccc4692ca5d2f8d8f`.
Branch `codex/home-complete-delivery-004`, draft PR #6, solo.
**Whole Home remains PARTIAL_NEEDS_INPUT.**

## Recoverable original authority and affected current code

Original 0.3.17 recovered `Yr` in
`origin/research/offline-controller:evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js`
bytes ~314700–320300 exposes independent `r.reorder(profileIds)`;
original native `profile_reorder` handler 0x12709b / 0x327460.
The supplied frontend permits a subsequent drag/drop while an
earlier reorder promise remains in flight, subject to its global
busy gate, not a per-reorder pending fence. The recovered contract
preserves ordered registry profiles on successful persistence.
Original licensed 0.3.17 interleaved native scheduling and
commercial multi-profile capacity **are not available**; the
following is an actual current-client convergence correction only.

`ProfileSidebar.jsx` directly calls `onReorder(next)` after actual
drag/drop; it does not serialize `onReorder` promises. `App.jsx`
`reorderNativeProfiles` previously launched native
`profile_reorder` requests immediately, independent of earlier
in-flight reorders. It guarded latest **React** rendering with
`nativeReorderRevisionRef`, but an older native request could still
execute *after* a later intended reorder and persist stale SQLite
profile order. `LWBridgeWindow.cs` correctly treats
`profile_reorder` as an independent persistent-registry reply,
not a selected-runtime Home status result. `ProfileRegistryCommandService`
invokes SQLite `ProfileRegistryStore.Reorder` for each request.

## Immutable before-fix actual native/WebView/SQLite inverse

Mounted actual production EN/light with two retained inert A/B:

1. Start real JSX B→A drag/drop, hold its first native
   `profile_reorder(B,A)` before execution.
2. Drag B→A again via actual JSX while first remains held.
   Second native write commits B,A; current JSX visibly B,A.
3. Drag A→B in the actual sidebar. Third native write commits
   SQLite A,B and JSX displays A,B, the latest user's intent.
4. Release older held first write. It commits B,A after the new
   A,B, while React correctly ignores its stale earlier revision.
   **SQLite B,A and React A,B diverge**.

Historical negative preserved at
`artifacts/home-004/r15-overlapping-reorders-before-fix-en-light.json.error.txt`:
`System.IO.InvalidDataException: R15 late older B,A reorder overwrote newer actual JSX A,B durable registry intent.`
Associated `r15-overlapping-reorders-before-fix-en-light-home-connected.png`
is only initial controlled connected Home, not an original pixel or
an intermediate divergence capture.

## Minimal production correction

`App.jsx` now holds `nativeReorderWriteChainRef`, one promise
chain **only for the global shared registry display-order field**:
`previous.catch(() => undefined).then(() =>
backendBridge.invoke("profile_reorder", {profileIds}))`.
No new lock covers independent A/B note writes, selection,
lifecycle, native pipe sessions, status reads or recovery.
The R10 latest acknowledged reorder order-only projection and
R12 selection busy, R13 exact-roster selected metadata, R14
ordered successful note acknowledgements remain unchanged.
Failure of an older reorder cannot poison a later legitimate
reorder, and no original schema or protected value is invented.
Canonical source check demands the reorder-only serialized
native write, failure-tolerant queue and latest UI projection.

## Distinguishing positive and retained controls

The same mounted harness has an explicit corrected branch:
while the first B,A command is held, second JSX B,A cannot
overtake and persist. Release first; both complete in order,
actual JSX shows B,A. New third actual JSX A→B completes
SQLite and visible A,B. Selected owner stays A; no A/B notes
or process lifecycle are changed.

Mounted EN/light:
`artifacts/home-004/r15-ordered-reorder-corrected-en-light.json`.
Mounted JA/dark:
`artifacts/home-004/r15-ordered-reorder-corrected-ja-dark.json`.
Both report `HOME_004_R4_MOUNTED_HOME_OK` and
`sidebarProfileCommands.overlappingReordersPreserveNewestDurableOrder=true`.
The entire earlier R4–R14 harness, including both R14
successful/failed note orderings, R13 note/selection inverses,
R12 B selection busy, R10 visible reorder, exact-owner A/B
Start/Stop/Close and Home repair/error control gates passes.
Inert A/B both stopped, no real games, no Map scan; existing
shutdown proof records zero requests/subscriptions, detached
events and removed isolated roots.

## Independent audit and acceptance limits

Also reviewed conditional original `Kr` 128 Home button gates,
`Jr` nine sidebar localization keys, original `Ir` reversed
error priority, H-18 first global manual repair versus H-23
selected startup reconcile, 3-second sidebar versus 5-second
Home polling, lifecycle exact owner and native dispatcher
generation fences, typed launcher/recovery/real Windows
named-pipe adaptation, protected ticket/controller/capacity
and stale reply risks. No speculative third commercial profile,
new game, Map scan, official updater or protected network
producer was used.

This current-client H-39/H-40/H-45 correction is not a
successful original licensed parallel-profile witness,
original encrypted callback/lease/reader timing contract, paired
genuine adverse recovery/repair, nor original EN/JA conditional
pixel comparison. The 47 original row-specific dependencies
remain in `docs/HOME_004_R4_QUEUE.md` and
`docs/HOME_004_CONTRACT_MATRIX.md`.
Do not declare whole Home READY or merge/publish without
those inputs and independent lead assessment.
