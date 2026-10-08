# 009-R1 worker report — AWAITING_REVIEW (Home/Map original A→A parity remains PARTIAL)

Static / headless / inert, solo. No game launch, desktop capture/input/focus, protected-service access, credential search or
owner-state mutation. An owner `LastWar.exe` was running during the work and was never touched. Evidence:
`evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-PARITY-CLOSEOUT-009/r1/` (the lead's immutable negatives and the worker 009
records are unchanged). Start `cdde6736`; commits `5ecf179b` (A/B), `7c3c33c4` (C), `a9f7f799` (E part 1) and the closing commit
(see `git log`).

## Dispositions

| finding | disposition | summary |
|---|---|---|
| LEAD009-01 restoration before confirmed exit | **CORRECTED** | `run_stop` withholds restoration unless the captured process exit is proven on its handle (or the PID is absent / a foreign image) **and** no other selected-installation process runs; journal kept at `closing_owned_game_for_restore` with `stopOutcome`; restore failure retains `restoring_after_owned_game_exit`, retry succeeds. Original close policy (unreadable image == gone) still reproduced by the 21-case reconstructed comparator. |
| LEAD009-02 PID incarnation replacement | **CORRECTED** (Python + native) | One handle is image- and creation-checked, terminated and waited. Native `TerminateOwnedProcessAsync`/`TerminateUpdateProcessesAsync` previously validated a `Process` then `OpenProcess(pid)`; they now use `OwnedProcessTermination` (error codes preserved). |
| LEAD009-03 launch/readiness | **RECOVERED (static) + one parity fix + one open difference** | `0x1D5009` is the profile-launch async body; stage map, `closeUnmanaged` (5 s/100 ms close wait, `GAME_CLOSE_TIMEOUT`), bridge-connect wait (90 s/250 ms, `BRIDGE_START_TIMEOUT`), registry functions `0x2C75CA/0x2C6A65` and the pid filter/sort `0x2A2887` are byte-asserted. **Discrepancy fixed:** the shipped Start button sends `closeUnmanaged:true`, the clone refused; it now closes unmanaged games exactly as recovered. **Difference identified, not corrected:** the clone's readiness window (one 120 s window from launcher start) vs the original 90 s window from the launcher-reported game PID. Protected: lease/ticket responses, `max_profiles`. Undecoded static: finalizer `0x1DDDE3`, launcher stage line map, post-connect report. |
| LEAD009-04 intent/profile/lifetime | **RECOVERED; one adaptation decision open** | Original reads SQL `ORDER BY display_order, created_at, id`, selects `enabled` profiles with `restartRequired` regardless of / no reason only if `autoLaunchAll`, launches sequentially with `closeUnmanaged=false`. The earlier "global AutoLaunchGame gate at [cfg+0x140]" was wrong (lock poison flag); the original has no native persisted preference. The clone's extra native `AutoLaunchGame` AND-gate is non-original and kept as a safety fence (lead decision requested). Current A→B→A / deferred / rejected / Close-during-scan behaviours re-executed (current adaptation). |
| LEAD009-05 coverage / closeout | **CORRECTED + MAPPED** | New held-async checks found and fixed a real ownership defect: a late terminate completion cleaned a **newer** session after Stop→Start (and issued helper effects after Close). 22 retired assertion groups mapped to executed replacements (`retired-assertion-map.md`); one consciously superseded (unknown game state ⇒ invalid record ⇒ 180 s recovery, as in the original). Boundary replacements are millisecond-exact (30 000 / >59 999 / 180 000 / 900 000 / 15 000 stable, retry table cap, wall-clock jumps). |

## Before / after proofs (distinct classes)

* Reconstructed-original: `home009_close_compare.py` 21/21, trace compare 230/230 (normalized — not parity), launch/profile/original-byte contracts.
* Production-boundary inverses: lead `stop-ownership-negative.json` (immutable), `r1/stop-ownership-before-after.json` (replay against `cdde6736`: 5 behavioural
  discriminators, 4 field-only), `r1/recovery-async-ownership-BEFORE-fix.txt` (immutable failing run).
* Current adaptation: native handle binding (12), unmanaged close (9), async ownership (8), boundary (10), mounted App (20 cases EN/light + JA/dark).
* Not done: live proof; real PID-reuse witness; original execution.

## Executed (all exit 0; `r1/final/final-checks.json`, 28/28)

Release build with canonical frontend/package embedding; actual 230-scenario trace + compare; native: process-ownership, close-timing,
reconnect-policy, home-campaign-lifecycle, profile-runtime-owner, map-campaign-canonical, map317-native-boundary, launch-spam,
official-settle, unmanaged-close, owned-process-handle-binding, recovery-async-ownership, recovery-boundary; Python: stop-ownership-r1 (19), overview lifecycle,
close comparator (21), original-byte, recovery, launch and profile contracts; frontend check and production-build check; mounted App profiles; current-client runtime
contract; scratch Release publish (deleted). `git diff --check` clean.

## Remaining (individually)

Static: finalizer `0x1DDDE3`; launcher stage `0x1DC2BC–0x1DCDA0`; reconcile recovery-record sub-path `0x203B30–0x2043C8`; status reason producer `0x2A0CB7`;
post-connect report `0x1E71D6`. Protected/external: `/api/multi/leases`, entitlement `max_profiles` (multi-profile and `PROFILE_LIMIT_REACHED`), live witness of the current-client
spawn→ready interval and of `closeUnmanaged`. Decisions for the lead: native `AutoLaunchGame` AND-gate; whether to align the 120 s readiness window once a witness exists.
Known stale pre-existing check: `tools/test_overview_start_failure_matrix.py` (fails at `cdde6736` too; start-failure cleanup was audited, not changed).
