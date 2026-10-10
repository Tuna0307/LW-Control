# Home — current functional status

Updated owner direction, 2026-10-10: faithful, fully functional replacement.
Read [owner direction](OWNER_DIRECTION_FUNCTIONAL_REPLACEMENT_2026-10-10.md),
[functional catalogue](HOME_004_ACCEPTANCE_PROPOSAL.md) and
[current campaign](HOME_004_LOOP_CAMPAIGN.md).

**Home functional status: PARTIAL — real F-04/F-06/F-07 gates still open.
No automatic approval or main merge.**
The former PARTIAL_NEEDS_INPUT for strict original A->A does not by itself mean
the product fails the new functional criteria. It also does not prove completion.

## Current checkpoint and retained work

- Branch codex/home-complete-delivery-004; one checkout; PR #6 draft.
- Revised goal start checkpoint: `d568cad68a77c8ee75b2ba8025f99b2c087f5d1f`;
  local and direct origin matched before this worker pass.
- **Latest corrected compiled candidate source**:
  `d09b8328ce1752cd6c305acae8f52acf827e6c87` (D-15 durable Add/Delete acknowledgement across selected-owner changes).
- Latest candidate on the Windows checkout:
  `artifacts/release/home004-functional-d09b8328/LW-Control-HOME004-FUNCTIONAL-RC-d09b8328ce17.zip`.
  SHA-256 `0F871CDCADACBCF0C3685726608F7AA36D1B96744E42EFC8859801C16EA4FA43`,
  9,555,554 bytes / 83 archive entries / 81 runtime files.
  Extracted EXE SHA-256 `66AEE4C45FCA50156982827801712D341B89ACA3ED5EFEA6D318B48214CEDC39`;
  extracted `SOURCE-COMMIT.txt` and `ProductVersion=1.0.0+d09b8328ce1752cd6c305acae8f52acf827e6c87` match.
- Previous `a6e76b6a` (D-14), `a64ee00a` (D-12/D-13) candidates, native/pipe
  tests and EN/JA receipts remain historical. D-14 was found because a source-
  mounted real WebView Add click failed while lower-level SQLite tests passed.
- The previous `74bcfa49` verified candidate is retained with its complete
  positive receipts and historically failed `e3ab51d3` positional-note receipt.
- `c0fed3f4` and previous verified functional ZIPs remain intact. The superseded
  `e3ab51d3` extracted EN proof failed before the new toggle assertion because
  an old proof clicked the first action instead of Edit Note; failure receipt
  `artifacts/home-004/functional-e3ab51d3-en-light.json.error.txt` is retained.
  `74bcfa49` updates the exact Edit Note selectors; both corrected proofs pass.
- Prior corrected functional candidate `5fc9b0d2` and its original verified ZIP
  remain retained historical evidence; its published source is not this latest build.
- Superseded former H-33 ZIP and first pre-correction functional ZIP remain historical
  (the latter's preserved JA/dark regression is explained in D-09).
- R16 and systematic metadata/lifecycle corrections are now committed, not dirty.
  H-42 native request/status and visible version were corrected; H-33 effective
  local flag is persisted before acknowledgement. Do not restart older rounds.

## Evidence already available, requiring appropriate review

[Final correction receipt](HOME_004_FINAL_CORRECTION_RECEIPT.md) records actual
controlled EN/light and JA/dark native/WebView/SQLite concurrency and packaged
proof, native/pipe/launcher checks and exact cleanup. Controlled A/B proof is
not genuine simultaneous-game evidence.

It also records a genuine pending-recovery Home Close failure followed by a
current-client cancellation/cleanup correction and a genuine successful follow-up
without late replacement. Original and launch-time/preflight script differences
are recorded separately. These saved worker results need independent review;
the lead did not rerun a native experiment during this direction update.

Earlier scoped Launch/Connected/Close, automatic startup, same-build adoption
and unexpected-exit/hang recovery receipts remain within their accepted bounds.
Source composition changes may require affected revalidation.

## What changes under the new goal

| Previous concern | Current disposition |
| --- | --- |
| Exact original licensed ticket/lease/finalizer output | Optional original research unless it actually prevents an included operation. Use verified local ownership rather than fake entitlement. |
| Original exact private transport/retry constant unavailable | Document a supported current-client/own-design policy and test it. Missing original equality alone is not a release blocker. |
| Original protected EN/JA conditional pixels unavailable | Retain recovered UI and current actual state/render checks; record visual limits. Not a compulsory protected-runtime pairing gate. |
| Still-running responsive transport loss/reconnect | Remains genuine functional verification, distinct from hang or process exit. |
| Repair/Update-and-Launch only proved with inert helper | Needs actual supported repair outcome if this feature is advertised; no official updater action is implied. |
| Multiple profiles only tested as inert owners | Verify included native controls/capability; original paid limit is excluded, actual unsupported use cannot be silently called complete. |
| Game-side action actually unavailable | Functional gap; recover/implement a supported method or identify exact technical dependency. A disabled action is not success. |

## Active finite continuation

The F-01–F-09 inventory below is the current worker checklist; preserve its
precise genuinely unverified gates rather than reopening old original-only
questions. The tested candidate is retained for lead review while those gaps
remain. The branch and PR remain draft/unmerged.

The 47-row original matrices and detailed correction receipts remain historical
authority, not automatic release gates. Main requires independently reviewed
working functionality with clear differences and supported scope. The earlier
policy-only update performed no code changes; the worker correction below did.

## Revised functional audit — continued worker pass from d568cad6 (2026-10-10)

This section supersedes the earlier *next concrete action* above. The original
research matrix remains a historical resource, **not** a current release gate.
Classification here distinguishes a working implementation, a *controlled*
regression, a prior genuine current-client witness and an unproven required
real operation. Full Home still **PARTIAL** until applicable genuine gates pass.

| Gate | Every Home/sidebar control and actual production path | Worker disposition; exact remaining functional gate |
| --- | --- | --- |
| **F-01** folder | Valid-root status, missing-root selection, cancel, invalid selection, save and reload; newly accessible **Select game folder** on a valid root for each selected profile; staged change preserves running owner binding. | **WORKER_VERIFIED source/controlled** via native root selection tests, mounted picker and root gate checks. No genuine second compatible installation was supplied for selecting and running B independently. |
| **F-02** manual lifecycle | Home Launch, Connected/Offline status, Close; sidebar individual Play/Stop and exact instance owner routing, including Start/Close busy fences. | **Prior genuine Launch→authenticated Connected→Close**, pending genuine recovered-launch Stop post-fix; native owner/recovery regressions pass. **New F-04 frontend optional-ID Stop path controlled**, not a new live game witness. |
| **F-03** auto launch | Home Auto Launch Game switch (saved local config + UI preference/rollback); startup `profile_instances_reconcile` iterates enabled/unlocked registry owners, OFF and one-time startup semantics; sidebar Start All/Stop All. | **Prior genuine single-profile startup/adoption + native ordered/disabled/collision checks**. Newly available 4-slot local roster not proven on two different actual games; OFF preserved. |
| **F-04** recovery | Home Auto Reconnect ON/OFF, native monitored missing process/hung/still-alive offline 60s, error/retry/maintenance feedback, disable, user Close while recovery pending (including no instance ID). | **WORKER_VERIFIED controlled** exact 59,999/60,000-ms threshold, retry and pending cleanup; prior genuine exit/hang/pending Stop. D-10 enables optional-ID sidebar cancellation. **D-12 fixes a real production monitor inconsistency:** `IsSnapshotReady` required authenticated pipe, but recovery had used fresh heartbeat alone as online; it now requires the authenticated native pipe route too. Controlled fresh heartbeat + absent route proves OFF, ON, exact restoration, successor and Stop. **NEEDS_REAL_VERIFICATION:** authenticated transport-only loss with otherwise responsive genuine current game and recovery/Stop OFF/ON; no network or protected-runtime fault was induced. |
| **F-05** host restart/adoption | Same-build re-open, exact process/build ownership, durable journal and adoption vs rejected stale identity; independent owner selected view. | **Prior genuine selected-game restart/adoption** plus native stale/obsolete/wrong-owner cases; new code did not alter adoption decision mechanics. |
| **F-06** repair | Home **Update-and-Launch** when native proxy_status signals game running + repairRequired; original recovered pending journal stop→restore→relaunch path, new **OWN_DESIGN** journal-free exact-owner Start/install attempt with mandatory actual `connected` result; explicit native error, cancel and busy reset. | **WORKER_VERIFIED controlled** original journal repair + executed production JSX callback. **D-13** now also rejects no-op native repair when no selected owner restarts and its repair-required status disappears/turns unavailable (formerly silent success); other-owner-only restart is likewise rejected. **NEEDS_REAL_VERIFICATION:** safely backed-up genuinely damaged bridge repaired through packaged button and authenticated reconnect; no official updater or simulated acceptance. |
| **F-07** sidebar/profile | Collapse/expand, profile list/status and **user-editable local enabled state** (D-11), locked admission, Select/focus-game option, note modal edit/save/cancel and late acknowledgements, reorder via drag, **Add/Remove local profiles** (native SQLite, 4 metadata slots), primary/selected/active/journal deletion fences and retained data, per-profile Play/Stop, Start All/Stop All and global Update-and-Launch. | **WORKER_VERIFIED controlled + packaged:** native CRUD/persistence and ordered R4–R16 A/B WebView metadata. D-10 fixes ID-less recovery cancellation, D-11 enables saved per-profile ON/OFF. **D-14 resolves a newly reproduced actual Add failure:** backend global command scope rejected `profile_create {}` before SQLite. The corrected extracted EN/light and JA/dark production UI click Add (3/4), Cancel Delete (retained), Confirm Delete (2/4), read exact native SQLite A/B restoration and clean cleanup. The confirmation response is supplied by the isolated test page only; production confirms normally. The per-target native deletion gate still rejects active/primary/selected/pending restoration. **NEEDS_REAL_VERIFICATION:** two distinct supported roots and two independent genuine game/bridge sessions. Four local metadata slots are not two real games. |
| **F-08** preferences/status/errors | AutoLaunch/AutoReconnect immediate draft and durable native commits/rollback, local `autoClosePopup` forced OFF storage, Home process/root/recovery/status/errors/disabled/busy, sidebar note/edit/save/errors, native locale EN/light and JA/dark. | **WORKER_VERIFIED controlled** status, locale (all 9 catalogs), 128-state source gate, save/reorder races; native `set_automation` connected game-side forwarding for *other* automation names remains `AUTOMATION_NOT_IMPLEMENTED` and is **not** a working Home preference. Home only exposes the supported two switches. Live responsive offline and repair UI feedback waits for their genuine gate. |
| **F-09** package | Canonical React UI compiled into Windows desktop, real WebView/native dispatcher, EN/light and JA/dark, extracted ZIP and normal one-window smoke, exact cleanup, one source-linked final candidate. | **WORKER_VERIFIED packaged**: latest `a6e76b6a` ZIP exact source/EXE/hash verified. Extracted production Home EN/light and JA/dark receipts `artifacts/home-004/functional-a6e76b6a-{en-light,ja-dark}.json` both PASS including real sidebar Add/Cancel/Delete with isolated SQLite and enabled toggle/native denial, zero genuine game launches, Map scans, active requests/subscriptions or cleanup errors; two inert owners stopped and isolated root removed. Extracted screenshot `artifacts/home-004/functional-a6e76b6a-capture/home.png` PASS without temporary roots. Earlier real named-pipe RPC and genuine-game outcomes remain historical; **latest read-only supported LastWar v23 `check-only` passed** with `installedFilesChanged:false`, not a genuine game launch. F-04/F-06/F-07 remain open. |

### Ready corrections and unresolved real inputs

This worker corrected a proven silent Home Close during pending recovery before
any instance ID; implemented normal native Add/Delete with guarded stopped-owner
retirement and local four-profile metadata capacity; exposed the valid-root
picker; and made no-journal repair perform a real exact-owner installed-root
Start attempt rather than acknowledge a no-op. All are **OWN_DESIGN** where
the original reference is different or insufficient; details, effects and
test classifications are in [the behavior differences ledger](HOME_004_BEHAVIOR_DIFFERENCES.md).
The `c0fed3f4` continuation additionally fixes actual sidebar cancellation
during an ID-less native recovery (D-10), so a still-running retry cannot be
misclassified as ready for Start All. Inert native and frontend tests pass.
The `74bcfa49` continuation closes the separately actionable F-07 local enabled
control omission (D-11): users can now persist per-profile admission ON/OFF
through a native sidebar button. It never stops an existing owned game or
redirects selection, and its acknowledged flag cannot overwrite unrelated
late profile notes, orders or owner changes.
The `a64ee00a` continuation adds F-04 authenticated-route monitoring (D-12),
ensuring fresh game-side heartbeat alone cannot conceal a lost native pipe
connection. It also rejects F-06 selected-owner no-op repair acknowledgements
when the selected repair status disappears (D-13). Both are controlled-tested;
neither has a new genuine game outcome.

Actual **still-open** gates are current-client responsive offline-only recovery
(F-04), genuine journal-free damaged-bridge repair (F-06), and independent
two-game support/admission if shipped as concurrent (F-07). Original protected
tickets, finalizer return bytes, exact retry constants and conditional original
screenshots are **research-only**, not functional blockers. This worker has not
changed any installed LastWar bytes or induced genuine updater/gameplay changes
in this pass. The lead alone can accept scope and release status.

### One final source-identified delivery / remaining actual dependencies

- **F-04** requires an independently safe still-responsive genuine game where
  the authenticated current-client pipe alone is absent for 60 s. Controlled
  clock/state regression and a genuine hung-game run are **not** that outcome;
  no supported way to induce this condition without altering owner networking
  or the real game's protected runtime was authorised/provided.
- **F-06** requires a safely backed-up *actual* supported bridge-damaged game
  installation to exercise Home Update-and-Launch and confirm fresh authenticated
  Connected plus exact byte restoration/cancel. The current install is a working
  owner installation; the read-only compatibility preflight did not authorise
  damaging it or triggering the official updater. Our no-journal fallback is
  **implemented and controlled-tested**, not genuinely accepted.
- **F-07** requires independently compatible second LastWar installation and
  verified distinct current-client game sessions if concurrent use is shipped.
  Four local SQLite metadata slots and inert A/B lifecycle do **not** establish
  two real games. The Add/Delete commands are native with verified rollback,
  persistence and cleanup safeguards; packaged EN/JA exercises existing A/B,
  but isolated packaged Add/Delete clicks and genuine two-game use are still
  outside the final positive evidence.

**Recorded negative→correction:** the first source `cb438bfb` packaged JA/dark
run failed because a newly added global busy-disable blocked independent
Profile B Start during Profile A Close. Original failure preserved under
`artifacts/home-004/functional-packaged-ja-dark.json.error.txt`; corrected
`5fc9b0d2` changed the guard to native per-owner admission, and EN/light +
JA/dark *source-mounted and extracted packaged* controlled proofs now pass.
Earlier genuine pending-Stop negative and genuine follow-up remain unchanged.

**Checks:** `dotnet run --project tests/LWBridge.Desktop.Checks -c Release`
PASS (including F-07 SQLite + unknown/pending journal), Release desktop
build/publish PASS zero warnings/errors, frontend `npm run check` PASS all five
groups/nine locale catalogs, fixed EN/JA production-mounted R4–R16 PASS,
extracted final EN/JA PASS, `tools/check_packaged_home.ps1` PASS,
normal GUI start/stop PASS, read-only `tools/run_overview_bridge_current.py
check-only` PASS with original v23 `LWScripts.data` SHA-256
`2187de71f426741eb61482f6e85639314526e6bdefc9445319b1ff52b31cfac0`.
No genuine game process or installed-game file was changed by this pass.

**Latest `c0fed3f4` verification:** repeat Release native checks PASS; `npm.cmd
run check` PASS (five frontend groups); canonical Release publish and production
UI build identity PASS; exact source-identified final ZIP extracted and verified
(83 ZIP entries / 81 runtime files). Both extracted production EN/light and
JA/dark controlled Home R4 mounted proofs PASS: exact owner status, repair,
profile metadata, independent A/B lifecycle and cleanup, no real game launch,
Map scan or orphaned request. Inert extracted Home PNG capture PASS. D-10 native
pending-recovery Stop and actual sidebar functions PASS. All original negative
receipts remain intact; none of these inert checks certify a new genuine game.
`dotnet run --project tools/home_004_pipe_recovery_probe/Probe.csproj -c Release`
PASS against real isolated Windows named pipes: authentication rejection,
malformed frame, 30-second idle retirement, fresh reconnection and RPC,
simultaneous A/B authenticated sessions, obsolete generation protection and
listener cleanup; `gameLaunches=0`.

**Latest `a64ee00a` checks:** `dotnet run --project
tests/LWBridge.Desktop.Checks -c Release` PASS, including fresh-heartbeat/pipe
absent controlled F-04 ON/OFF at 59,999/60,000ms, exact old-owner stop and
restoration, replacement Connected and user Stop. `npm.cmd run check` PASS,
including executed F-06 no-restart/no-target and other-profile-restarted
false-success inverses. Release publish/canonical UI and exact 83-entry ZIP
identity PASS. The extracted native/WebView Home EN/light and JA/dark receipts
both PASS all sidebar/repair fixture controls, no genuine game launches, Map
scans, leftover requests/subscriptions or cleanup failures, and both inert
owners stopped. `check_packaged_home.ps1` PASS with zero residual roots. Real
isolated named-pipe host/reconnect/two-profile RPC probe PASS. The current
owner's game scripts, settings, process and updater were untouched.

**Latest D-15 source `d09b8328` (2026-10-11):** A controlled mounted test
held a native Add response after actual SQLite insertion, then switched
the display profile A→B. After correcting the proof helper to correlate
each native request ID (rather than reusing one shared response variable),
the production dispatcher returned `PROFILE_GENERATION_RETIRED` despite
the successful durable Add. Negative preserved:
`artifacts/home-004/crud-switch-correlated-negative-en-light.json.error.txt`.
The earlier `crud-switch-negative-en-light.json.error.txt` was a
harness-order negative whose apparent follow-up success was not a valid
concurrent-call proof; retain both with this distinction. Corrected
`LWBridgeWindow` now preserves committed controller Add/Delete/primary
acknowledgements across view changes without changing lifecycle generation
fencing or native owner deletion gates. Actual source-mounted EN/light and
JA/dark `crud-switch-fixed-*.json` and extracted `functional-d09b8328-*.json`
PASS normal sidebar Add/Cancel/Delete and newly correlated Add/Delete
after commit across profile selection. Both stopped inert owners, exact
durable SQLite roster, zero game/Map starts, requests, subscriptions and
cleanup failures. Release native/frontend checks PASS. Source-identified
ZIP, EN/JA extracted app, capture and root cleanup PASS. Genuine F-04,
F-06, F-07 have not been witnessed in this continuation.

**Previous `a6e76b6a` D-14 verification and live-evidence classification:** a
new actual production WebView Add test found a **failed native command**, not a
stale React display: `local-crud-proof-source-en-light.json.error.txt` and
diagnostic `local-crud-proof-diagnostic-en-light.json.error.txt` preserve the
negative and show native roster `2/4`, visible `2/4`, no Add. Root cause was
missing `profile_create`/`profile_delete` in `LWBridgeBackend.GlobalCommands`,
so generic selected-runtime validation rejected controller Add `{}`. Corrected
backend and native Release test now actually dispatch both commands through
`LWBridgeBackend.InvokeAsync` (not registry alone). Corrected source-mounted
`local-crud-proof-confirm-{en-light,ja-dark}.json` PASS. The test-page-only
WebView confirm stub verifies Cancel preserves the new owner and Accept removes
the exact new stopped owner. Final **extracted ZIP** EN/light and JA/dark
`functional-a6e76b6a-{en-light,ja-dark}.json` both PASS all prior Home
assertions and new `nativeAddDeleteConfirmedAndPersisted`, zero live game
launches, Map scans, lingering native requests/subscriptions or failed cleanup.
`functional-a6e76b6a-capture/home.png` PASS, no leftover capture roots.

The **genuine installed current LastWar v23** was independently checked via
the existing read-only `python tools/run_overview_bridge_current.py check-only`
and returned `ok:true`, `mode:check_only`, `installedFilesChanged:false`,
package SHA-256 `2187de71f426741eb61482f6e85639314526e6bdefc9445319b1ff52b31cfac0`.
This validates installed source compatibility, NOT a newly launched/authenticated
game. The earlier genuine manual Launch/Connected/Close, adoption, unexpected
exit/hang and corrected pending Stop receipts remain historical evidence.
There was **no new live LastWar session** in this pass; only the real Windows
desktop/WebView/SQLite application and live installed-file read-only gate ran.

**Latest `74bcfa49` D-11 verification:** native Release checks PASS including
selected-primary toggle, disabled secondary Start rejection, invalid input and
SQLite reopen; frontend five-group check PASS including *executed* late
toggle vs B selection/note/reorder and failed-write preservation. Canonical
Release publish and source-matched production frontend PASS. Fresh mounted
source EN and both **extracted** packaged EN/light + JA/dark Home native/WebView
proofs PASS, all sidebar checks true; new D-11 button genuinely clicked in the
packaged UI and validated against native `profile_list`, native denied Start,
re-enable and preserved selected A. Zero game launches, Map scans, outstanding
requests/subscriptions and cleanup failures, both inert owners stopped. The
extracted fixture Home PNG and zero temporary-root check PASS. A pre-fix
`e3ab51d3` extracted EN proof failed at the old positional note selector;
the error receipt is retained and the selector was corrected before packaging
`74bcfa49`. No genuine game or user installation was modified or launched.

Decision: **PARTIAL**, not READY_FOR_LEAD_FUNCTIONAL_REVIEW. The reasons are
specific genuine operations/verification, not absent original protected data.
Do not merge, publish, bypass account/licensing or induce updater/network
changes. PR #6 stays draft for independent lead review.
