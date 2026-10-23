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
