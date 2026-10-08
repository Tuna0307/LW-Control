# 009-R2 worker report — AWAITING_REVIEW (Home/Map original A→A parity remains PARTIAL)

Static / headless / inert, solo. No game launch or Stop, desktop capture/input/focus, protected-service access or owner-state mutation; the owner's
`LastWar.exe` was never touched. Evidence: `evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-PARITY-CLOSEOUT-009/r2/`; all R1 fixes and the lead's
immutable negatives (`r1/lead-review-2026-10-08/`) are preserved. Start `3a7e23ad`.

## Dispositions
| finding | disposition |
|---|---|
| LEAD009R1-01 extra native AutoLaunchGame gate | **CORRECTED.** Reconcile admission = payload `autoLaunchAll` (default true) only. `--reconcile-admission-check`: 8 payload variants × native true/false (expected = actual, consumed-once, failure reported, owned game not double-started). The only test that asserted the gate now asserts the original rule on an inert helper; no fixture invokes reconcile against a real helper. |
| LEAD009R1-02 readiness window | **CORRECTED (timer contract); live event mapping separate.** Helper: acquisition keeps `--timeout-seconds`; at the game report a wall-clock deadline `ms+90000` governs `await_ready` (`now>=deadline? → lookup → 250 ms`, final lookup after the loop), written to `game-reported.txt`; host refreshes the pending registration to the same deadline (`refresh_pending`), a claimed registration is `PIPE_REGISTRATION_INVALID`; outer supervision = 120+90+15 s with acquisition reduced first. Both lead cases now match; 20 timer cases, production `run_start` readiness-deadline assertions (12 stages), 6 host groups. Not a 120→90 swap. |
| LEAD009R1-03 launcher / finalizer / report / restartRequired / reconcile recovery | **RECOVERED statically; no product change demonstrated; three small items INCOMPLETE.** Launcher: 2 attempts, retry only after attempt 1 on `OFFICIAL_LAUNCHER_RESTARTED`; report `pid`/ticket fields and `LAUNCH_REPORT_FAILED`; post-connect = read+validate+update+commit `recovery.json` (game pid), instances.insert, complete recovery (awaitingIdentity→running, pid tracked), failure terminates the game; finalizer role; `restartRequired` = restored game with outdated bridge build, reconcile's restartRequired handling and its `RECOVERY_*` errors. Clone retained as ADAPTATION where the capability (session adoption, per-instance record fields, lease) does not exist. |
| Coupled ownership (E) | New defect fixed: a start whose owner was closed while the 5 s close wait was parked still launched (`r2/unmanaged-close-closed-owner-BEFORE-fix.txt`); now `GAME_OPERATION_CANCELLED`. |

## Executed (r2/final/final-checks.json: 38/38 exit 0)
Release build incl. canonical frontend/package embedding; 230-scenario actual trace + normalized compare (0 mismatches, limits unchanged); 19 native checks
(R1's 13 plus reconcile-admission, bridge-ready-window, lifecycle-launch-binding, normal-composition, production-root-isolation, map-auto-scan-campaign);
the default Program.cs flow; Python stop-ownership (19), lifecycle, close comparator (21), original-byte/recovery/launch/profile/launch-R2 contracts,
readiness-window (20), start-failure matrix (stale harness repaired, 12 stages); frontend check + production-build check; mounted App 20 cases EN/light + JA/dark;
current-client runtime contract; scratch Release publish (deleted).

## Remaining (individually)
Static INCOMPLETE: finalizer frame states `0x1DDF66–0x1DE357`; recovery-record schema beyond the PID; launch-failure `error` shape (`0x2054CB`) and the
`RECOVERY_PROCESS_MISMATCH` condition. Protected/capability/live: lease bodies and entitlement capacity; session adoption (needed before `restartRequired` can
drive an automatic restart); launcher self-restart trigger; live ready-event and `closeUnmanaged` proofs.
