# Home final independent review and acceptance

Owner direction, 2026-10-23: finish independent review, fresh Home checks and
stress tests, fix failures, then call Home done if those gates pass and move to
Map next. Reproducing the original spontaneous disconnect is not a separate
acceptance prerequisite. A passing finite stress campaign establishes the
agreed release scope, not universal proof that a fault can never recur.

Status at this checkpoint: IN PROGRESS. No main merge or release.

## Baseline and review scope

Branch: codex/lwtool-full-improvement / draft PR #16.
Reviewed worker handback: 8138d2628fdf110f3b12249c7f3d4717c3b97de0.
Home implementation: eefa29e4571b28dfcb7a6f7300865b76d9672123.

Independent review covers direct adapter/Lua queues, publication, generation
retirement, bounded memory, strict correlation, fallback, exact owner state,
presentation readiness, command admission, Start/Stop/recovery and Home controls.
One designated controller owns live sessions and installed script restoration.
Raw private diagnostics and exact process receipts remain ignored under
artifacts/home-final-acceptance-20261023/.

## Review findings and required corrections

### Presentation cache revives after an authoritative negative

The actual lifecycle implementation reproduced this sequence: fresh ready
heartbeat -> presentation true; explicit not-ready heartbeat -> false; next
heartbeat read fails -> presentation true from the old remembered heartbeat.
Command admission remained fail-closed, but Home could re-present a checking
state after an authoritative negative. Wrong-owner heartbeat has the same edge.

The correction invalidates only the matching owner observation when either
readiness reader sees contradictory owner/ready evidence. A retired observer
cannot clear a successor's independent cache. Four distinguishing sequences
cover strict/presentation reader x not-ready/wrong-owner, subsequent read loss
and fresh-valid recovery.

### Buffered pipe disposal can stall generation retirement

A fresh changed-path check stalled in RetireCore while buffered FileStream
Dispose flushed into WriteFile under the adapter Gate. The worker was waiting
for that same Gate in its finally block. The captured test process was exact
identified; its mini dump and full stack were retained, then only that task
process was stopped. This run is recorded as interrupted, not passed.

The correction makes pipe writes unbuffered, detaches the exact old stream under
the state lock, cancels only its native I/O outside the lock and leaves disposal
to the owning worker. A successor cannot be cancelled by stale retirement.
The blocked-reader regression keeps the peer alive without reading, requires
bounded retirement and worker exit, observes successful native cancellation,
then verifies a successor. Five focused reruns and the full Desktop suite pass.
A cancellation-disabled negative control fails the distinguishing worker-exit
assertion. Actual in-game Mono qualification remains a live acceptance gate.

### Transport review and added Lua integration checks

No additional direct queue authority/correlation/main-thread defect was found.
A suspected permanent reconnect error was retracted after executing the genuine
Lua pump order: state refresh and the heartbeat guard recover the underlying
failure on the next pump. A publication race can add one pump, not deadlock.

Seven new inert tests execute genuine refresh/ensure/inbound/result/heartbeat
functions and the production pump tail. They cover same-session transport fault
classes, publication races, authentication errors, strict response identity,
stale generation, no replay and unavailable state. Mock/native boundaries remain
explicit; these checks do not substitute for real xLua live evidence.

## Test bookkeeping

The initial concurrent Desktop/UI test attempt hit an infrastructure conflict:
Desktop's build runs npm ci while the UI suite reads the same dependencies.
Subsequent build/UI checks are serialized. No lockfile/package version was
changed. Infrastructure failures and the captured retirement hang are preserved
separately from successful reruns.

Completed preliminary checks:

- full pre-correction Desktop delivery suite: PASS;
- full UI suite after serialized dependency restoration: PASS;
- new genuine-Lua transport checks: 7/7 PASS;
- exact-owned live fault harness checks: 6/6 PASS;
- programme package boundary checks: 2/2 PASS;
- inherited R30 package/privacy checks: 8/8 PASS.

Final corrected offline checks:

- full Desktop delivery suite: PASS, including blocked-write retirement and
  authoritative-negative presentation cache sequences;
- full UI suite: PASS;
- all genuine-Lua feature checks: 49/49 PASS;
- real Windows named-pipe recovery probe: PASS, including idle listener,
  authentication negatives, concurrent profiles, RPC and stale-generation cleanup;
- release UI captures: 41 screenshots across three variants and 16 conditional
  states PASS (UI source unchanged by the two lead corrections).

Final corrected build, remaining regressions, fresh connected stress, all Home
control coverage, clean package and byte-exact restoration are pending at this
checkpoint. The final receipt must replace this status only after those gates
actually complete.

## Fresh packaged acceptance ledger

The corrected-transport package completed its full fresh 1800-second run:
1801 samples, one game PID, zero failed layers, zero sampled route/bridge/game
failures, direct mode active in all samples, no numbered mailboxes and no sampled
login/network errors. All 320 actual game-thread read-only calls passed.
The complete 20-cycle reconnect repeat passed without preflight retries:
same exact game/session, 1.303-1.472 seconds, median 1.377 seconds.

Additional live passes: 12-second process pause, Stop during route reconnect
with exact restoration/no successor, actual persisted auto-launch on host
restart, forced host crash followed by same-game/session authenticated
readoption, 20-second pre-ready delay and automatic game-exit recovery.

The remaining live controls found two more defects, now corrected for final
packaged replay:

- **Exited owned session inaccessible from Home:** with auto-reconnect OFF,
  the game correctly stayed stopped for 35 seconds, but Home enabled Start
  and disabled Close despite native `runtimeManaged=true`. Native exact Stop
  worked. Home now blocks Start while that exact session remains managed,
  enables the normal confirmed Close and gives localized cleanup feedback.
  Mounted EN/ZH tests cover Cancel, confirm, busy/recovery precedence,
  cleanup completion and unchanged repair action; all nine locales updated.
- **Pre-ready cancellation uses graceful close:** a genuine automatic
  replacement suspended before ready could not process `CloseMainWindow`.
  Stop correctly refused success, but left cleanup incomplete. A valid
  session/challenge cancellation now uses the existing exact PID/path/start
  Stop termination primitive, requires signalled exit and no selected-install
  survivor, then permits restoration. Other startup failures retain their
  prior graceful-close policy. Five inert tests pass, including the genuine
  run_start failure/cleanup path and fail-closed exit/survivor negatives; the
  old graceful-only implementation fails both genuine-start regressions.

After the failed cancellation, the exact game exited on resume. The normal
pending-recovery helper then restored all original files, and the task host
closed cleanly. No unowned process or manual script replacement was used.
Final-package OFF-exit/Close and pre-ready cancellation replay are still pending.

Production source/package commit: 2a621f94aba10416fd7172f1c13e6b50edd04f28.
Fresh publish, source/product-version/UI/29 linked bridge checks and ZIP
per-entry integrity/privacy checks pass. ZIP SHA-256:
`a467837e9b14d3a57f6f99ab03b1fe17c1534806011c268aefafeb7442eb37af`.
The exact extracted package is used for the following live tests, with a new
isolated root, an initially stopped game and fresh official-script backups.

| Home feature/boundary | Independent verification |
| --- | --- |
| Actual Home Start | Live DOM button launches an exact healthy game; direct mode active. |
| Home state and disabled controls | Real native connected state matches Home; Start disabled/Close enabled. Full UI error/recovery/disabled-state suite also passes. |
| Game folder | Live native picker Cancel and accept-current both close normally, preserve configured root and keep the same healthy game. Invalid-root/stale-owner boundaries pass inert native tests. |
| Close confirmation Cancel | Live real modal opens/cancels; exact owner remains healthy. Confirm/Stop pending. |
| Auto-launch setting | Live Home toggle saves true in isolated configuration. Actual host restart/startup pending. |
| Auto-reconnect setting | Initial true observed from native config and Home. Fresh off/on exit behavior pending. |
| Game/account idle | Strict and presentation readiness true; pending=0; no enabled gameplay task flags. |
| Real game RPC | 100 sequential + 100 in groups of four read-only getStatus calls: 200/200 successful. |
| Reconnect stress | First 20-cycle attempt completed 13 fault/recoveries; stopped at cycle 14's single heartbeat precondition read before injecting that fault. No full-run PASS claimed; repeat pending. |
| Scheduling/late readiness, process exit, Stop during reconnect, repair/restart | Pending fresh final-package trials. |
| Corrected-package 30-minute soak and final restoration | Running/pending. |

The real game RPC timings include WebView/native dispatch and the game-thread
pump: sequential P50/P95/P99 = 110.35/128.80/143.70 ms; concurrency four =
112.50/133.70/154.30 ms. This is a different workload from the earlier host-only
sub-millisecond probe. Neither proves an advantage over LwTool without an
equivalent legitimately idle competitor run.

The aborted stress attempt is retained under
`artifacts/home-final-acceptance-20261023/route-cycles-first-attempt.json`.
The later exact-owner recheck passed; the native monitor retained the same
process, authenticated route and healthy game. A single unavailable precondition
read is not counted as a successful cycle or attributed to an established
spontaneous fault cause. The earlier worker candidate also captured a short
heartbeat publication/read transient; keep these observations separate from
the owner's original visible intermittent symptom.
