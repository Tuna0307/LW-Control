# HOME-004 R13 — stale selected-profile snapshots and ordered exact-owner note writes

Date 2026-10-10. Assigned campaign `HOME-COMPLETE-DELIVERY-004`, original
LWBridge **0.3.17**, EXE SHA256
`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
Start at clean local/direct-origin R12
`057a0d5c61b1e97e3ff14d2c4db64bb59b060ac3`;
branch `codex/home-complete-delivery-004`, draft PR #6, solo.
**Whole Home remains PARTIAL_NEEDS_INPUT** pending original/genuine acceptance.

## Substantial original/current source and production audit

Inspected original archived 0.3.17 `Yr` sidebar in
`origin/research/offline-controller:evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js`
around bytes 314700–319500 and archived H-39 original native
`profile_select` 0x10b70b, `profile_reorder` 0x12709b,
`profile_note_set` 0x1a4d26. Original JSX dispatches explicit
`r.select(profileId)`, independent `r.reorder(profileIds)`, and
note-dialog `r.updateNote(profileId, value)`; the note Submit button
is disabled for global `r.busy`, **not** while an individual save
promise is pending. Repeated same-dialog Submit is therefore possible
in the supplied original frontend. This is source/UI contract
authority, not an original licensed concurrent server witness.

Compared actual `App.jsx`, `ProfileSidebar.jsx`,
`LWBridgeWindow.cs` dispatch/generation gate,
`ProfileRegistryCommandService`/SQLite and retained A/B runtimes.
Also reviewed Home `Nt` Start and `Pt` Close, original `Kr`
128-condition gate oracle and `Jr` 9 connection labels,
`Ir` **reversed** error priority, H-18 manual first-**global**
repair error versus H-23 selected-only startup reconcile,
per-owner sidebar 3-second polling versus Home 5 seconds,
native R7 exact busy admission, H-34 still-alive offline
60-second and H-35 120/300/600/600-second controlled retry
threshold tests, official-launcher typed producer and H-07
two-attempt/restart-only policy, 0.3.17 encrypted transport,
global A+B reconciliation and packaged source/cleanup.
No speculative changes made to already passing native or Map paths.

## Distinguishing negative 1: late completed B selection erases newer order

R12 correctly allows a latest B selection to complete even when
independent registry revisions advance, but adopts the **whole**
`profile_select` snapshot at completion. In the mounted original
React JSX:

1. Start actual drag of B before A; click actual selected B row.
   A new **home-only test-owned** native `profile_select` **reply**
   hold occurs *after* native selection finished and captured an
   A,B snapshot but before delivery to WebView.
2. Drop actual dragged B over A. Real native `profile_reorder`
   commits SQLite B,A and the actual React sidebar shows B,A.
3. Release the older native B selection result. Before R13,
   `selectNativeProfile` adopts its stale whole A,B profile list,
   erasing B,A from visible React despite the registry remaining B,A.
   The clicked owner B is correct but current metadata regresses.

Preserved ignored **before-fix product negative**:
`artifacts/home-004/r13-selection-snapshot-after-reorder-inverse-2-en-light.json.error.txt`
(`R13 stale B selection snapshot cannot erase newer B,A registry order`
timeout). An earlier setup record
`r13-selection-snapshot-after-reorder-inverse-en-light.json.error.txt`
held the wrong side of the explicit profile ID; test hook fixed
without changing production expectations or overwriting evidence.

Smallest correction: `adoptNativeProfileSnapshot` may preserve
existing profile objects when the selection request was overtaken
by an independent metadata request **and** exact roster cardinality,
IDs and selected owner membership all match the native authoritative
response. It still validates the expected selected B ID, advances
actual selected owner/generation and preserves selected-view status
fencing; it does not replace a changed roster with invented entries.
R12 inverse order (reorder commits *before* B select dispatch) remains
unchanged/passing.

## Distinguishing negative 2: two rapid A note Saves commit backwards

The original `Yr` modal permits two successive Saves while its
first update promise is pending. On the rebuilt production WebView
open real A Edit dialog, input X and submit; hold its first
`profile_note_set(A,X)` *before* native execution; change to Y and
submit again via the actual React form. Prior App dispatched both
concurrently. Native Y committed first, JSX displayed Y,
then delayed older X committed last, leaving durable SQLite
note **X** while visible A showed **Y**. B's note was unaffected.

Preserved ignored **before-fix product negative**:
`artifacts/home-004/r13-two-note-order-before-fix-en-light.json.error.txt`
(`R13 older concurrent note overwrote newer exact A persistent note`).

Smallest correction: `App.jsx` per-exact-ID
`nativeNoteWriteChainsRef` chains native note writes in the
same user's submit order; failed first writes do not prevent
later retry (`previous.catch(...).then(...)`). Other profile
owners remain independent, no global lock. R11 latest-per-owner
UI revision and R10 order-only projection remain in place.
Native note successes and failures continue through existing
dialog/error handling.

## Validation and scope boundaries

Actual mounted EN/light
`artifacts/home-004/r13-final-en-light.json` and JA/dark
`r13-final-ja-dark.json` both PASS **both** new distinguishing
inverses, native B/visible B,A and exact A,B restore, durable
second note Y on exact A, unchanged B note, plus all R4–R12
actual owner/lifecycle/recovery/repair/error controls.
Both inert owners stopped, native requests and subscriptions 0,
listeners detached, test roots removed, genuine game launches
and Map scans 0. Frontend source checks assert exact roster-safe
selection merge and per-profile note write queue.

The added `profile_select` post-execution reply hold is explicitly
gated to `--home-map-campaign-home-only` and has no unarmed
production mutation. It does **not** represent an original protected
server or commercial two-game runtime.

### Acceptance and originally missing inputs

R13 affects H-39/H-40/H-45 (selected vs independent metadata and
visible UX); other obligations' bounded evidence remains. Needed
for whole Home acceptance: original **successful protected**
ticket/lease/finalizer callback, entitlement and capacity
(H-05/H-13/H-22/H-28/H-38), original signed controller
runtime callback/plaintext (H-13/H-29/H-33/H-36),
genuine official launcher restart/updater/Lua/descriptor/mutex
typed faults (H-06–09/H-18–20/H-44/H-46), two actually
compatible independent legitimate real game installations
and supported concurrent admission (H-24/H-37–40),
and paired original/current late Start/Stop, offline-only
still-alive transport recovery, maintenance, rollback,
failed restoration, original conditional EN/JA pixels and other
row-specific adversarial witnesses (H-01–04/H-10/H-12/H-14–17/
H-23/H-25/H-30–36/H-40–47).

Available legitimate alternatives inspected: recovered archived
original frontend and original native locator/semantic evidence,
existing bounded genuine **one-owner** Launch/Connected/Close,
adoption, unexpected-exit and hang recovery receipts, controlled
original timing/source oracles, native Windows pipe/typed helper
tests, and actual packaged isolated WebView/SQLite with two **inert**
retained owners. No original successful protected response or
legitimate two-real-game capability appears in these sources.
Original encrypted 0.3.17 reader timeout remains unknown; current
30-second adaptation is 0.3.1-derived only. No protected access,
entitlement spoofing, game/process disturbance, unsafe updater,
Map feature, main merge or public release.

Final source SHA, direct origin, frontend/EXE/ZIP hashes, extracted
Home, normal disabled-registry GUI PID/precise cleanup and draft PR
are in generated ignored
`artifacts/home-004/r13-final-package-receipt.json`.
