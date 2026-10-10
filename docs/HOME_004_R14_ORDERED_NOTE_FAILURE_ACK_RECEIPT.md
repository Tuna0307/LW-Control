# HOME-004 R14 — ordered note acknowledgements after a failed successor

2026-10-10. Campaign `HOME-COMPLETE-DELIVERY-004`, original LWBridge
**0.3.17**, original EXE SHA256
`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
Start: verified same-branch R13 `4bbd386e0400ba40c3af845ee326ba4f4727024f`,
direct origin equal and clean except the preserved uncommitted R14 test/continuation.
Whole Home: **PARTIAL_NEEDS_INPUT**, not ready for lead merge/release.

## Original and current authority

Original 0.3.17 archived `Yr` sidebar in
`origin/research/offline-controller:evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js`
around UTF-8 bytes 314700–320300 calls explicit
`r.updateNote(profileId,value)`. The note Submit is disabled by global
`r.busy`, not an individual note write pending flag, allowing repeated
submissions from the same dialog. Native original handler
`profile_note_set` is at 0x1a4d26 / 0x327c9b; recovered profile registry
returns a refreshed profile-list snapshot. This supports an exact-owner
convergent note contract, **not** proof of licensed-original asynchronous
failure ordering or a successful protected-controller response.

Production path: `App.jsx` `updateNativeProfileNote` → actual
`ProfileSidebar.jsx` note form → `backendBridge.invoke("profile_note_set")`
→ `LWBridgeWindow.cs` request dispatcher → native
`ProfileRegistryCommandService` → SQLite `ProfileRegistryStore`.
R13 had already fixed X/Y execution inversion using per-ID serialized
`nativeNoteWriteChainsRef`, while keeping A/B independent. R11/R13
also tracked newest *requested* per-ID note revision when projecting a
successful older native note response.

## Preserved new actual-boundary negative

Using the actual mounted EN/light production WebView, open A's real JSX
note modal; submit X=`r14-committed-A-note`, hold its first command before
native dispatcher execution, submit Y=`r14-rejected-A-note`. Release
the first write while a home-only isolated rejection hook skips X,
rejects Y (code `CAMPAIGN_PROOF_REJECTED`). Native SQLite durably
contains X; the dialog presents an actual error; React A note remains
older because X's successful callback was discarded when the *requested*
per-ID revision of still-pending/failing Y was larger.

**Immutable before-fix actual failure**:
`artifacts/home-004/r14-failed-newer-note-before-fix-en-light.json.error.txt`
with timeout:
`R14 durable successful first A note visible after rejected later Save`
at `LWBridgeWindow.HomeR4Proof.cs` R14 assertion. The paired
`r14-failed-newer-note-before-fix-en-light-home-connected.png`
is the controlled initial Home capture, **not** a screenshot of the
intermediate failing note state. Neither is original licensed evidence.
The previous executor's rejected command attempt was an actual tool
safety block; the renewed identical, explicitly requested command ran
normally without any workaround. Do not overwrite either record.

## Production correction

In `App.jsx`, retain R13 per-exact-profile promise chain and requested
note revision. Add separate *acknowledged successful* per-profile revision
`nativeNoteAcknowledgedRevisionsRef`. Each ordered successful native
write now applies **only its saved exact profile's note** when its
revision exceeds the last successfully acknowledged revision, regardless
of a later requested write or selected owner/registry request. If Y
succeeds later, its ordered acknowledgement projects Y; if Y fails,
the already durable X remains visible. Failed older requests do not
replace newer exact-owner errors; rejected writes never advance the
acknowledged revision. No full stale note snapshot can replace selected
B/order/another profile's notes. No global serialization, modal locking,
protected producer, launcher or game change.

The canonical source assertion was updated to require the successful
acknowledgement fence and forbid note-call full-snapshot adoption,
while retaining per-ID write chain/error revision and R6–R13 safeguards.

## Actual integrated proof and diagnosed harness race

Mounted source/WebView/native/SQLite R14 EN/light:
`artifacts/home-004/r14-note-two-failure-orders-final-en-light.json`.
Mounted JA/dark:
`artifacts/home-004/r14-note-two-failure-orders-final-ja-dark.json`.
Both emit `HOME_004_R4_MOUNTED_HOME_OK` and report:

- `rejectedNewerNotePreservesEarlierDurableOwnerValue=true`:
  first X succeeds and persists, later Y fails, visible React displays X;
- `failedFirstNoteAllowsNewerDurableRetry=true`:
  first X is rejected, later queued Y completes and persists, React
  displays Y; B's note remains independent;
- retained R13 two-successful-note order, R13 stale selected B
  snapshot/order, R12 inverse reorder/selection busy, R11/R10
  note/order-only, R9 independent registry acknowledgement, R8 Home
  captured A Close ABA, R7 per-owner busy and R6 original reversed
  errors/global manual repair continue to pass;
- two inert A/B owners stopped, zero genuine games and Map starts,
  with existing mounted shutdown/isolated root checks unchanged.

The *first* corrected EN/light run,
`r14-note-ack-corrected-en-light.json.error.txt`, failed later at
the unrelated pre-existing B-only global repair assertion: A's
sequential relaunch finished before the injected B-failure callback
was visited. Source harness now waits boundedly for **both** A
success and consumption of B's injected failure, preserving
all prior successful-A/failed-B/error-visibility assertions. The
earlier harness failure remains saved separately. Subsequent mounted
EN/light and JA/dark both pass the full harness.

Canonical frontend/locales and Release Desktop build passed (0 warnings,
0 errors). Native recovery/lifecycle/Map safety suite and typed launcher
3/3 passed; actual Windows named-pipe shared-listener two-owner probe
passed, with zero game launches. These are controlled current-client
correctness witnesses, not original encrypted pipe constant proof:
the adapter's 30-second reader idle timing derives from 0.3.1.

## Other Home boundaries audited and remaining acceptance

Inspected R13 selection/reorder/note snapshot paths and selected-profile
revision independence, original `Yr`/H-39, R6 reverse error priority
and distinct H-18 *first global manual repair* vs H-23 *selected startup
reconciliation*; R8 owner Close and R7 per-profile busy; R4 native
dispatcher reply fencing; actual recovery/poll and typed launcher
receipt. Two-profile JSX reorder cannot necessarily express two
different in-flight desired orders until the first visible reorder
changes; do not claim an unexecuted third commercially entitled owner.
Source evidence alone does not prove concurrent original client
schedule/error serialization. No speculative change to an already
verified Home/Map lifecycle or genuine launcher path was made.

Outstanding original/protected/genuine gates remain mapped per all 47
rows in `HOME_004_R4_QUEUE.md`: H-05/H-13/H-22/H-28/H-38 original
ticket/lease/capacity/finalizer; H-13/H-29/H-33/H-36 original encrypted
controller and vtable; H-06–H-09/H-18–H-20/H-44/H-46 authenticated
official launcher/update/error stages; H-24/H-37–H-40 compatible real
two-owner games; and row-specific original/current adverse repair,
recovery/pending Stop/maintenance, exact error and conditional EN/JA
pixel evidence, including H-14–H-17/H-23/H-25/H-30–H-36/H-40–H-47.
Already attempted safe alternatives are recovered archived static
original functions, controlled actual native/WebView/SQLite, named
pipe, typed launcher and saved bounded single-owner real observations.
No protected response, signed controller plaintext/private CNG key,
licensed capacity or unsafe official updater behavior is invented.

Do not treat a successful R14 progress checkpoint as whole-Home
completion. Keep draft PR #6, no merge/public release, and continue
only new evidence-supported/authorized obligations.
