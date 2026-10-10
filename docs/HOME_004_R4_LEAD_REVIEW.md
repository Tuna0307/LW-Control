# HOME-004 R4 independent lead review — 2026-10-10

Reviewed worker source: `c7f53530a57911b226bce57d047c08a79ba4be42`.
Disposition: **CHANGES_REQUIRED**. Whole Home remains PARTIAL and PR #6 draft.
The worker correctly withholds whole-Home parity, but the remaining queue also
contains actionable local gaps; not everything left depends on missing original
keys, protected-service responses or live-client capability.

## LEADHOME004R4-01 — stale Close cancels the new pending recovery owner

`OverviewLifecycleService.StopAsync` at lines 1194–1212 accepts Stop during a
pending automatic recovery launch, clears desired intent and invalidates recovery.
The optional explicit instance comparison at lines 1223–1226 occurs only in
the other branch, after an owned game PID is present.

Independent executable inverse holds the real recovery helper using the existing
inert native fixture. Original instance A exits; the real production lifecycle
creates a different pending instance B. A delayed Stop explicitly naming A is
accepted, clears `GameDesiredRunning` and leaves recovery idle instead of
protecting B. This contradicts the existing recovered explicit instance-mismatch
contract and exact-owner cancellation requirement. It is not a live-game result.

Run `dotnet run --project tools/home_004_r4_lead_probe/Probe.csproj -c Release`.
The recorded baseline reports `oldAndPendingOwnerDiffer=true`, no rejection,
`desiredAfterStaleStop=false`, `recoveryState=idle`. Preserve this negative record.
Correct exact target validation before pending cancellation without removing the
valid current-owner/no-ID user Stop path or late-helper cleanup.

## LEADHOME004R4-02 — retained owners do not yet supply the profile controls

At App.jsx:1030–1035 the actual native callback object supplies only select,
reorder and note. No `readInstance`, `onStartProfile`, `onStopProfile` or
`onRestartAll` reaches ProfileSidebar. Its native per-profile polling therefore
does not run and its start/stop controls remain disabled. This is an existing
integration gap exposed by the expanded R4 scope, not a newly introduced sidebar
regression. Retaining A while selecting B is useful but does not finish the
recovered profile instance workflow.

Run `node tools/home_004_r4_lead_probe/check-sidebar.mjs`: it executes the actual
App callback expression and checks the actual JSX consumer. It does not mount
the app or invent missing providers.

The production WebView dispatcher still captures `requestBackend = backend`
(LWBridgeWindow.cs:5095) and calls only that backend at line 5123. An explicit
B status/start/stop request while A is selected consequently cannot target B.
The independent actual-backend probe confirms A rejects B status with
`PROFILE_RUNTIME_UNAVAILABLE` while B's backend returns B's exact instance.
This backend rejection is correct and must remain: fix the dispatcher/owner
lookup rather than widening one backend's profile scope. Current R4 proof reads
status only after selecting each owner, so it misses this cross-profile case.

Wire source-backed callbacks to actual retained-owner command routing and test
unselected profile status/actions plus batch controls through the real App and
native dispatcher. Separate selected-view response fencing from explicit
target-owner commands; no silent profile selection or disposal of other owners.

## Passed checks and bounded credit

Independent current-source runs passed Desktop.Checks, the actual Windows pipe
probe (including simultaneous authenticated A/B RPC), frontend check and
production UI package validation. Worker ZIP SHA matches
`9AB96E868958101440F0C6AE31756D2B0B98B438726AA269644689C9497A6B39`.
Reference 0.3.17 EXE hash matches the target. Direct origin matched worker source
before this review. No current game/launcher/Desktop process was observed.

Inspected the four saved source/extracted EN/light and JA/dark mounted receipts:
controlled two-owner proof, zero genuine game launches/Map scans, both inert
owners stopped, zero active requests/subscriptions, detached events, removed
isolated roots and no shutdown failures. These receipts remain controlled.

The concurrent transport, retained runtime, typed spawn error and staged-root
changes receive bounded implementation/regression credit; this is not final
acceptance of every branch or original native behavior. No new game, desktop
control or owner-installation mutation occurred during this lead review.

Compact immutable results: `proof/HOME-004-R4-LEAD-INVERSES-2026-10-10.json`.
Raw lead logs remain in ignored `artifacts/lead-home004/r4/`.

## Continuation

Next owner-relayed medium correction: `HOME_004_R4_R1_CONTINUATION.md`, both
findings above and focused integration only. Retain the existing branch/checkout,
all prior positives/negatives, original error ordering and technical restoration
boundaries. Do not start another broad evidence campaign or return solely with
a repeated package check while these ready local gaps remain.
