# 009-R1 E — retired Program.cs recovery assertions → executed replacement proof

Source of the retired text: `git diff b15c13e4…1d9b54c6 -- tests/LWBridge.Desktop.Checks/Program.cs` (351 deleted lines, 38 `Check(...)`
messages). The retired block drove the *pre-port inline* recovery and mixed original-contract facts with 0.3.1-derived values and
current-client adaptations. This table classifies every message; none is silently dropped.

Proof classes: **O** = reconstructed-original value/rule (byte-asserted by `home009_recovery_contract_check.py` / `home009_contract.py`);
**T** = 230-scenario trace compare (normalized: status/effect shape only; cleanup events, most error text and ordering are removed — the
lead's LEAD009-05 limit); **B** = millisecond-exact boundary replacement (`--recovery-boundary-check`, new in R1); **A** = held-async
ownership (`--recovery-async-ownership-check`, new in R1); **N** = other pre-existing native check.

| # | retired obligation | class / status | executed replacement |
|---|---|---|---|
| 1 | one missing-process observation does not recover; second does | O (0x41aafe) | B `two-miss-rule`; T `process-exit-*` |
| 2 | second miss restores the exited session and relaunches through the lifecycle | O+adaptation (cleanup is a current-client effect) | B `process-exit-sequence-…` asserts exactly one helper stop of the exited session and exactly one relaunch (not covered by T, which drops cleanup events) |
| 3 | success publishes waiting/repairing/launching/verifying then succeeded (restarted, visible notice) | O | B `process-exit-sequence-…` (restarted, notice visible, id > 0); T status sequence |
| 4 | 15 s stable verification window | O (0xe6285) | B 14 999 ms not succeeded / 15 000 ms succeeded |
| 5 | unconfirmed event cannot replace terminal status | adaptation of `game.recovery_requested` | T `event-unconfirmed-ignored` |
| 6 | ambiguous confirmed event fails closed | adaptation | T `event-ambiguous-ignored` |
| 7 | unknown confirmed reason rejected | adaptation | T `event-unsupported-reason-ignored` |
| 8 | crossDisconnect enters waiting, verifies in place, no restart, not before 15 s | O+adaptation | T `event-crossdisconnect-healthy-in-place`; B stable-window |
| 9 | exitPrompt respects the first-miss rule and preserves the reason | adaptation | T `event-exitprompt-then-exit`; B two-miss-rule |
| 10 | confirmed disconnect waits while unrecovered; escalates after 60 s | O (0xe654f) | T `event-disconnect-unhealthy-escalates`; B disconnect 59 999/60 000 |
| 11 | forceUpdate keeps `updating`, updater suppresses termination, provenance retained | O+adaptation | T `event-forceupdate-with-updater`, `update-process-running-offline` |
| 12 | 15 min update stall; fingerprint change resets; error text; first normal retry | O (0xe6096, 0xe61d1, 0xd67b08) | B `update-stall-900000ms-and-fingerprint-reset`, `update-stall-error-text-and-first-retry`; T `update-stall`, `update-activity-keeps-alive` |
| 13 | disabling reconnect cancels a pending event without clearing desired-running | adaptation (native config) | T `event-while-reconnect-disabled`, `reconnect-disabled-mid-recovery`; N `OverviewReconnectPolicyChecks` |
| 14 | hang: not before 30 s, exact owned PID terminated once, reason hang | O (0x41abf8/0x41ac0b) | B `hang-30000ms-boundary` (also asserts the terminate call carries the owned PID and happens 0×/1×); T `hang-from-*`, `terminate-fails-hang` |
| 15 | disconnect: not before 60 s | O (0x41ac34 `> 59999`) | B `disconnect-60000ms-boundary` |
| 16 | updater/launcher activity suppresses disconnect termination | O (0x41ab5b `either`) | B `updater-suppresses-disconnect`; T `update-process-running-offline` |
| 17 | **unknown current-client game state is not converted into a disconnect recovery** | **SUPERSEDED by the original rule**, not an oversight: the ported monitor treats "state never observed" as an invalid game-state record (`UpdateHealthRecord`, 0x41ba64) and a live bridge with an invalid record recovers after 180 s (0x41ac53). The retired assertion encoded the 0.3.1-era fail-open adaptation. | B `login-unavailable-…-unobserved-equals-invalid` (observed and unobserved behave identically at 179 999/180 000 ms) |
| 18 | observed unhealthy: not before 180 s | O (0x41ac53) | B login-unavailable boundary; T `login-unavailable-from-*` |
| 19 | intentional Close clears desired-running before cleanup; cannot be resurrected | adaptation of 0x41bbff | N `OverviewReconnectPolicyChecks`, `OverviewCloseTimingChecks`; A `stop-restoration-failure-is-retryable-and-not-resurrected` |
| 20 | disabling reconnect during recovery uses 15 s first retry, cancels cleanly, suppresses later launches | O+adaptation | B `retry-table-and-cap` (15/30/60/120/300/300/300 s); T `reconnect-disabled-mid-recovery`, `launch-fails-*` |
| 21 | update-process recovery uses the 2-minute maintenance delay and publishes `updating` | O (0xd681b0) | T `update-process-running-offline`, `server-unavailable-log-after-exit`; table bytes by `home009_recovery_contract_check.py` |
| 22 | `game_recovery_status` exposes terminal state with numeric notice | N | `GameRecoveryStatusChecks` |

Not replaced because still not decidable from available original evidence: exact error *text*/order of every status event
(original builders `0x41c0ab`/`0x41cacb` are asserted for shape only), and process-exit relaunch cleanup order against the original
(cleanup is a current-client restoration effect with no original counterpart).
