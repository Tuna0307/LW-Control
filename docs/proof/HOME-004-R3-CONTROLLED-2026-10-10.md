# HOME-004 R3 — still-running monitor and delayed cleanup, 2026-10-10

**Disposition: PARTIAL.** Original-supported still-running thresholds and
successor ownership are verified through the actual production
`OverviewLifecycleService` observation/run/helper paths with controlled
inputs. A task-owned **packaged live hang/offline** witness and its
EN/light plus JA/dark **recovery** captures have not been obtained.
LEADHOME004R2-01 remains OPEN. Whole Home remains PARTIAL.

## Recovered 0.3.17 authority and controlled contract

Original `research/offline-controller` source:
`evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-PARITY-CLOSEOUT-009/monitor-recovery-analysis.md`.
Monitor `0x41a8a0` ticks at 2 seconds (`0xe5650`); process-present
health is classified only after enabled/desired/armed, inactive-run,
available-root and tracked-game gates. With live PID still present:

* Offline + OS window-hung for **29,999 ms** remains idle; both clocks
  at **30,000 ms** produce `hang` (original `0x41abf8` /
  `0x41ac0b`, priority over `disconnect`). Its run init immediately
  calls termination and exact old helper restoration.
* With the same live PID, offline but *not* window-hung at **59,999 ms**
  remains idle; at **60,000 ms**, `disconnect` starts (`0x41ac22`).
* Connected transport with invalid game state requires **180,000 ms**
  before `disconnect`. Process-exit classification instead requires
  two consecutive missing PID observations; these are distinct conditions.
* Original run loop `0xe5884` has 2-second cadence, 15-second healthy
  verification, normal retry 15/30/60/120/300 seconds, and maintenance
  retry 120/300/600 seconds. User Stop clears desired-running and retires
  the current recovery run; disabling auto-reconnect mid-iteration is
  honored at the iteration-end gate.

`tests/LWBridge.Desktop.Checks/HomeR2RecoveryChecks.cs` invokes real
`RunRecoveryObservationForTestAsync`,
`RunRecoveryRunTickForTestAsync`, `profile_instance_start` and
`profile_instance_stop` against the production service, with isolated
controlled clocks, process observations and inert helper effects. It
passes the exact thresholds above, first retry at 15,000 ms but not
14,999 ms, stable verification, OFF/ON, Stop and stale owner invalidation.
**Zero actual game launches** occur in this suite.

### New R3 delayed-acknowledgement inverse

The R2 test previously parked **old process termination**; R3 additionally
parks a **returned Stop-helper acknowledgement** after the production
30-second `hang` detector actually reaches cleanup. While that old
acknowledgement is pending, the actual service processes user Stop,
clearing desire and invalidating the old run. A new `Start` creates a
different PID, session/challenge, real serialized protected
`adoption.json`, actual `LWBridgeControlPipeHostState` pending registration,
and a successor `lease.txt` written by the service's renewal timer.
Releasing the obsolete acknowledgement then proves all of the following:

* Successor is still alive and its phase/recovery state was not retired.
* Its protected adoption still deserializes to the new session.
* The new pipe registration is still pending under the new session ID.
* The lease file still contains the successor session and challenge.
* A deliberate new-owner Stop subsequently retires its own adoption,
  registration and lease.

This is the original `0x41bbff` Stop intent/retirement policy plus the
current-client exact owner-scoped cleanup
(`OverviewLifecycleRecovery.cs:887-922`,
`OverviewLifecycleService.cs:1760-1796`). A controlled helper
acknowledgement is *not* a live game/pipe/authentication receipt.
Native Release delivery suite: **PASS**, `HOME004_R2_NATIVE_MONITOR_OK`
and `HOME_FEATURE_DELIVERY_CHECKS_OK`; all tests used inert processes.

## Actual Windows live gates — blocked, without substitution

Fresh read-only compatibility, before any new task-owned game start:
`python tools/run_overview_bridge_current.py check-only` produced
`ok=true`, current contentVersion **23** and
`installedFilesChanged=false`, with the expected game/xlua/assembly
and four critical Lua hashes. The original `lwbridge-0.3.17.exe`
SHA-256 and all three installed original script hashes also matched;
initial process list was empty. No installed script was changed by
this R3 worker.

At local **2026-10-10 00:52** the proposed `r3-live` isolated root was
already present with another preflight receipt and a `setup-window.png`;
our `prepare` refused to overwrite it. At approximately **00:53**,
passive Windows inspection found packaged
`r2-final-rc/publish/LWBridge.Desktop.exe` host **PID 10036**, created
outside this worker's commands. That process later exited. A new
isolated `r3-live-solo` gate was used to avoid touching the other
session, but its `prepare` was refused because a fresh
`LWBridge.Desktop.exe` **PID 55796** had appeared. No task-owned
R3-solo backup/admission/launch was made; no game PID or session
identity was authorized for perturbation. No external process was
stopped, suspended, adopted or taken over. The pre-existing
`r3-live` evidence remains untouched.

This is an **isolation/ownership preflight denial**. It does not prove
any live 30/60-second detector, OFF suppression, ON authenticated
successor, 15-second native stable interval, pending-recovery Stop, or
verified end-of-attempt process exit. The R2 Chinese/light and
Japanese/dark screenshots cannot close the required EN/light R2
recovery gap. The other session's setup image is not recovery proof.
No native recovery screenshot or timestamped ON/OFF status timeline is
claimed by this worker.

For a later explicitly owned attempt, the bounded
`tools/home_004_r3_bounded_hang.py` validates the exact same Win32
handle's PID/path/100ns creation and R3 profile/session/backup against
the saved adoption and journal, insists on single game and host, and
limits OS process suspension to 65 or 75 seconds with a `finally`
resume. It collects only real handle liveness/window-hung
observations; it is **unexecuted** here. It cannot replace native UI
status and authenticated heartbeat receipts.

## Verification, remaining work, and handoff

Fresh Release native checks: PASS, including R1 typed repair, R2/R3
monitor and ownership, ordered-profile and unchanged Map checks.
Frontend `npm.cmd run check`: PASS, five check groups, nine locale
catalogues each with 1,383 keys. Production build integrity: PASS,
source fingerprint `cc7ce094a249a68e32fd4e1ba1efaf69bd39a8a9c4e261db2b0e4aa2d2a88af6`,
product fingerprint
`7d854d2b8031a73ef625c70bc9b052bb0548ddb39a1000fd46c8eafe8ad08c0f`.

**Still required for R3 unit readiness:** exclusive live ownership,
fresh R3-only script backup and confirmed isolated admission, actual
alive-PID hang/offline classification in the packaged UI for
Reconnection OFF and ON, native authenticated successor and stable
verification, timestamped transport/status events, EN/light and
JA/dark recovery captures, pending user Stop/cancel when reachable,
and the exact end-of-attempt process/restore/journal gate. Independent
lead review must still approve the unit. No whole-Home parity or merge
is claimed.
