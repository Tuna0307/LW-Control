# HOME-004 R4-R2 — actual pending recovery and native profile controls

Date: 2026-10-10. Branch: `codex/home-complete-delivery-004`.
Starting source: `f12dd566bd1410c2ccb17e054cd85978ace1c8a4`.
Target: original LWBridge **0.3.17** EXE SHA-256
`4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783`.
Disposition: **local corrections verified, whole Home PARTIAL_NEEDS_INPUT**.
Only the independent lead may accept or merge. Draft PR #6 remains draft.

## Distinguishing inverse and correction

**LEADHOME004R4-01, H-14/H-16/H-34/H-40/H-47.** The immutable
`tools/home_004_r4_lead_probe` reproduced an old explicit Close during a
held native recovery launch clearing the newer pending owner's desired state
without an `INSTANCE_MISMATCH`. `OverviewLifecycleService.StopAsync` now checks
the explicit string target against the *current pending recovery owner* before
changing desired-running/recovery. Matching ID, missing ID, and non-string ID
retain valid pending user Stop. Actual `HomeR2RecoveryChecks` holds production
recovery helpers and distinguishes all three accepted inputs from stale Close;
it verifies active-run preservation after rejected manual Start, late helper
success and failure, host-close cleanup, old PID/record restoration, and exact
no-adoption of a cancelled successor. Rejected manual Start now retains the
active recovery notice until a new Start is genuinely admitted. The original
`PROFILE_ALREADY_RUNNING` Start code is preserved. Prior negative evidence was
not changed.

**LEADHOME004R4-02, H-19/H-22/H-23/H-38/H-39/H-40/H-42/H-45.** The
immutable `check-sidebar.mjs` demonstrated the actual native callback object
omitted read/status, Start, Stop and restart-all. The actual production
`App.jsx` now passes four providers and polls/acts by explicit profile ID.
The WebView dispatcher resolves registered enabled/unlocked Start to the exact
retained target backend, does not select the target, rejects invalid/unknown
and locked admission, and still leaves each backend's foreign-profile guard
intact. Responses to explicit target-owner commands and global restart are
independent of selected-Home view generations; selected-view responses retain
their old fencing.

The previous native global update-and-restart repaired only whichever owner
was selected. Recovered original H-19 visits enabled/unlocked profiles in
registry order. The production ordered service now aggregates each owner's
own `restarted` and `errors`, validates owner attribution and continues after
another owner's failure. The injected native backend test verifies ordered
success/error attribution and rejects foreign restart result IDs.

## Executed evidence and limits

The **actual packaged frontend and production WebView command dispatcher** in
isolated `--home-map-campaign-home-only` runs passed for EN/light and JA/dark.
Receipt paths under ignored `artifacts/home-004/`:

- `r4-r2-final-en-light.json` and its `-home-connected.png` image.
- `r4-r2-final-ja-dark.json` and its `-home-connected.png` image.

Each receipt confirms A/B independently retained and polled; A remains the
selected owner while native sidebar starts/stops B; a stale explicit B Stop
is rejected; missing/invalid/unknown target IDs receive native error codes;
an actual B status response held across A→B→A selection returns B's own
session; both selected/unselected profile Starts respect registry locks; a
native Start-All A failure does not prevent B; Stop-All restores the owner;
global native Update-and-Restart repairs and starts **both A and B in order**
then Stop-All restores both. The original Home Launch/Close, running-root
selection and Update-and-Launch controls remain in the mounted proof.

The final `shutdown` receipt for each run reports `sessionClosed=true`,
`requestRegistryClosed=true`, `activeRequests=0`, `activeSubscriptions=0`,
`profileRuntimeEventsDetached=true`, `isolatedRootRemoved=true`, no cleanup
failures. Zero genuine game launches and zero Map scans were performed. This
does **not** prove simultaneous real Last War clients, original protected
admission, current-client offline-only recovery, or original 0.3.17 pixel
equivalence. No installation, original account service, or real game was
modified for this correction.

The `LWBridge.Desktop.Checks` suite and actual Windows shared named-pipe
probe pass, including authenticated concurrent A/B RPC and generation/shutdown
cases. The canonical `npm run check` passes all static/Home/Map/UI/draft
checks; three Python launch-spawn producer tests pass. The source-identified
Release ZIP and extracted Home smoke are separate final delivery gates; see
the normal `artifacts/release` receipt and direct remote Git SHA for the
current branch.

## Whole Home remaining input gates

See the maintained [47-row queue](HOME_004_R4_QUEUE.md) and original locators
in [the contract matrix](HOME_004_CONTRACT_MATRIX.md). The ready R4-R2
corrections above are complete; the following are **not accepted as done**:

- H-05/H-13 (and protected H-28/H-33 portions): an authorized successful
  original 0.3.17 entitlement/lease response and original finalizer callback,
  error/lease-release result, and legitimate original controller decryption
  inputs are absent. Archived schema fragments are insufficient to implement
  licensed behavior; no service access, decryption bypass or fabricated result.
- H-06–H-09/H-44/H-46: no authenticated original/current-client launcher
  self-restart, Lua update, timeout/descriptor/mutex negative, or supported
  updater trace providing the typed producer and exact original serialization.
  Current typed OS spawn error is narrower and remains verified.
- H-03/H-04/H-10/H-12/H-15/H-17/H-18–H-20/H-25/H-41: no safe second
  compatible installation and backed-up supported outdated client/failing
  updater/registration/restore witness. Existing current-client successful
  Launch/Close/adoption is preserved, without causing an owner-installation
  failure to manufacture extra proof.
- H-24/H-27/H-29/H-38/H-39: original commercial capacity/ticket/transport
  authority and a legitimate independently supported simultaneous real-client
  witness are absent. The 30-second adapter pipe idle timer is derived from
  **0.3.1**, not established as the original 0.3.17 reader timeout.
- H-01/H-02/H-11/H-14/H-16/H-23/H-30–H-37/H-40/H-42/H-43/H-45/H-47:
  remaining adverse live/original-sided pending Stop, offline-only still-alive
  transport loss, prolonged maintenance/retry, busy/save interleaving, errors
  and all conditional Home pixels lack paired genuine 0.3.17/local client
  inputs. Existing native exact threshold, rollback, disconnected-alive and
  cleanup checks remain **controlled**, even where a separate accepted
  current-client hang/exit positive exists.

Legitimate next action when those inputs become available: verify source and
client version/backup identity, reproduce one missing original-to-current
distinction in the already bounded Home scope, correct it if necessary and
rerun the current positive/negative gates. Do not mark the 47 rows complete
without those witnesses or independent lead review.

Read-only original-input alternative checked on
`origin/research/offline-controller` (without changing product checkout):
`docs/reviews/2026-09-28-r8-141-lwbridge-0317-session-entitlement-parity.md`
supplies the generated five-field `EntitlementResponse` names and seven-field
`SessionV2` evidence but **no legitimate successful entitlement/lease response**.
The indexed `r1-2026-10-08/original-controller-boundary.json` similarly
documents the missing signed envelope and matching CNG private key, while
`finalizer-original-contract.json` contains only source-level locators. These
alternatives are insufficient to reconstruct an operational protected finalizer
or decrypt the controller; they were not treated as positive live evidence.
