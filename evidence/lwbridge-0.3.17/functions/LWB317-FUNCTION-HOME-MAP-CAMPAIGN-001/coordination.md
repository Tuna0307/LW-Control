# Coordination

One coordinating worker owns integration, validation, master status updates and
Git delivery. Up to two helper lanes are initially recommended: Home/native and
Map/store/provider. They may inspect original contracts independently. Assign
exclusive editable files/evidence directories and separate browser/process ownership.
Reserve shared App, host composition, bridge interfaces and common tests for the
coordinator unless ownership is explicitly transferred. No concurrent edits.

Write the exact agent assignments here before delegation. Verify every returned
finding/change independently. Recover a failed helper's checkpoint rather than
stalling the whole queue. Only the coordinator commits/pushes. No messages to an
external worker chat; manual owner relay remains the cross-chat mechanism.

Offline/inert service/provider integration only. Inspect old tests/helpers before
execution; historical live flags or default game side effects are not allowed by
this assignment. Preserve owner sessions/listeners/storage and archived research.

## Recorded helper assignments

Home/native lane: worker-1, source-recovery only. Exclusive writable file:
`home-lane/findings.md`. It audited canonical Home/App, root/config/profile/status,
Overview lifecycle/control-pipe composition and deterministic tests. No production,
Git, browser/process or live-game action was delegated.

Map/store/provider lane: worker-2, source-recovery only. Exclusive writable file:
`map-lane/findings.md`. It audited canonical Map/App, Map317 service/store,
current-client scan/action providers, all eight producers, manual/Auto Scan and
scheduled-job/Treasure contracts. No production, Git, browser/process or live-game
action was delegated.

Coordinator independently re-read both results, implemented shared frontend/host
composition, added focused tests, and alone committed/pushed checkpoint
`c4509dac4d0b7fb1aa09b343c39fb9a1e00615bc`.

## Second implementation lanes

Home/native worker-1 is transferred exclusive ownership of two new acceptance
artifacts only:
`tests/LWBridge.Desktop.Checks/HomeCampaignLifecycleChecks.cs` and
`home-lane/acceptance.md`. It may add deterministic synthetic lifecycle cases for
pending/cancel/rejection/stale process/non-owner/profile retirement, but may not edit
Program.cs, production code, shared tests, Git, or start browser/live-game processes.

Map/store/provider worker-2 is transferred exclusive ownership of three new files:
`src/LWBridge.Desktop/MapAutoScanCommandService.cs`,
`tests/LWBridge.Desktop.Checks/MapAutoScanCommandServiceChecks.cs`, and
`map-lane/auto-scan.md`. The service must preserve the recovered Auto Scan
defaults/timing, use injected execution/clock/delay boundaries for deterministic
cycles, persist profile-owned config/state under an isolated caller-supplied path,
serialize admission, cancel only its own operation, and never contain a preview/live
fallback. It may not edit LWBridgeWindow/App/mapBackend/Program/shared Map317 files,
run live flags, perform Git operations, or touch the owner game/config.

Coordinator retains all shared composition, command registration, frontend migration,
master docs, integration validation, commits and pushes.

## Recovery continuation lanes

After resuming the interrupted campaign, the coordinator found the second-lane
implementation artifacts still present as uncommitted work and took ownership of
their integration. Two fresh helpers are assigned bounded independent review only:

- Home/native recovery reviewer: exclusive writable file
  `home-lane/recovery-audit.md`. Re-read the current Home production path plus the
  uncommitted lifecycle acceptance check and report concrete remaining B/C/D gaps,
  source-contract drift, unsafe side effects, or false evidence claims. No production,
  shared tests, Git, browser/process or live-game actions.
- Map/store/provider recovery reviewer: exclusive writable file
  `map-lane/recovery-audit.md`. Re-read the current Map317 production path plus the
  uncommitted canonical acceptance/Auto Scan work and report concrete remaining
  E/F/G/H/I/J gaps, contract drift, duplicate ownership, or live-boundary risks.
  No production, shared tests, Git, browser/process or live-game actions.

The coordinator retains every pre-existing uncommitted production/test file and all
integration, validation, documentation, commit and push ownership.

## Recovery final integration

The two recovery review lanes completed and no helper report remains outstanding.
Their counterexamples were independently reproduced/reconciled by the coordinator
before implementation closeout.

The completed UI profile/status lane was integrated without re-delegation:
`backendBridge.js`, `mapBackend.js`, `App.jsx` and the Home integration check now
use profile ID **and generation** for current-owner acceptance. The coordinator bound
that UI acknowledgement to real native runtime replacement and verified it through
the packaged desktop.

The Map review lane's actionable findings were incorporated into the pushed Auto,
Map service and profile-runtime checkpoints. Protected Treasure/Ghost actions remain
fail-closed and were never delegated to a live runner.

Prime/coordinator alone performed final package builds, inert WebView runs, screenshot
inspection, staging, commits and pushes. A final independent package review found
three concrete gaps in the earlier packet: profile-retired Home Auto Launch callbacks,
disabled Auto/plunder execution in the isolated composition, and route/event proof
that was too shallow. Worker-1 then received exclusive ownership of only
`tests/LWBridge.Desktop.Checks/MapAutoScanCommandServiceChecks.cs` and added the real
Map317 Auto-A -> Manual-M stale-owner regression; worker-2 remained read-only for
package review. The coordinator integrated the remaining production/test-host fixes.

Final package-proof checkpoint
`eaa73ce571d0c419a0e2880e212c46151ee7fd1f` was pushed and explicitly verified equal
to `origin/research/offline-controller` before evidence/master reconciliation. The v3
packaged proof runs with inert Auto scheduler and plunder workers enabled while keeping
recovery transport and all external game actions disabled. No live game/updater or
owner installation/process was controlled by this campaign.
