# Home final independent acceptance

> **Status (2026-10-27):** merged to `main` through PR #16 and released as `lw-control-v1.1-2026-10-25`. Statements below about branches, draft PRs or "not merged" describe the state on the note's date.

**ACCEPTED for the owner's agreed Home scope on 2026-10-24.**
Home is done for this scope; Map comparison/improvement is next. No main merge
or public release was performed. Production/package source is
`ae7eceb464ce1c06cb8a1854a3d686b2017cbeeb`, on
`codex/lwtool-full-improvement`, draft PR #16.

The owner explicitly accepted successful fresh stress/recovery qualification as
the completion criterion without requiring reproduction of the original random
visible disconnect. That criterion is met. This is finite release acceptance,
not proof that every future environment or disconnect cause is covered.

## Independent review and corrections

The worker handback at `8138d262` was independently reviewed. Four real defects
were corrected and distinguishing regressions/live replays passed:

1. **Retirement could hang:** buffered pipe disposal flushed into blocked I/O
   while holding the adapter lock. Writes are now unbuffered; retirement detaches
   the exact old stream, cancels only its I/O outside the state lock, and lets its
   worker dispose it. A peer that stays connected without reading cannot stall
   retirement; successor ownership remains fenced. Five focused reruns and the
   full Desktop suite passed, with a cancellation-disabled negative control.
2. **Ready cache could revive after contrary evidence:** a readable wrong-owner
   or explicitly not-ready heartbeat now invalidates the matching remembered
   presentation observation. Subsequent read loss cannot resurrect it. Strict
   command authority still fails closed at five seconds; UI grace stays separate.
3. **Exited-session cleanup was inaccessible:** auto-reconnect OFF correctly
   left the game stopped, but Home disabled Close and enabled a Start that native
   ownership would reject. Home now uses native `runtimeManaged` to keep confirmed
   Close available, block Start until cleanup, and give concise feedback in all
   nine languages. Busy, recovery and repair precedence is preserved.
4. **Pre-ready cancellation could leave cleanup incomplete:** the startup helper
   used graceful `CloseMainWindow` on a suspended replacement. A valid schema,
   session and challenge cancellation marker now uses the existing exact
   PID/path/start-time Stop terminator. Exit proof and no installation survivor
   are required before restoration retry; uncertain cleanup retains the journal.
   Ordinary failures without that marker keep their previous graceful policy.

The final cancellation/UI correction received a further read-only independent
review: ACCEPT, with no critical new finding. It introduces no image-wide kill
or gameplay command. Initial restore ordering is unchanged; the new cancellation
proof gate protects the force-close/restoration-retry branch.

## Qualification

| Gate | Result |
| --- | --- |
| Full final Desktop delivery suite | PASS |
| Full final UI suite and nine-language completeness | PASS |
| Genuine-Lua suites / added direct-transport checks | 49/49 and 7/7 PASS |
| Added cancellation-helper checks | 5/5 PASS; old graceful-only path fails both genuine-start regressions |
| Real authenticated Windows pipe recovery | PASS: idle listener, auth negatives, RPC, concurrent profiles, stale cleanup |
| Actual single-write pipe messages at production quotas | 32 KiB, 256 KiB and 1 MiB PASS |
| Real connected transport soak | 1800 seconds / 1801 samples, one PID, zero failed layers, all direct mode |
| Long-run read-only game requests | 200 burst/sequential + 120 periodic: 320/320 PASS |
| Same-game local transport fault campaign | 20/20 PASS; 1.303-1.472 s, median 1.377 s |
| Final extracted ZIP reconnect replay | 5/5 PASS; median 1.342 s, max 1.462 s, same exact owner |
| 12-second game scheduling pause | Same exact game returned healthy |
| 20-second pre-ready suspension | Same reported game reached healthy readiness |
| Auto-launch preference and real host restart | PASS: game auto-started and became healthy |
| Forced task-host crash/reopen | PASS: authenticated readoption of the same running game/session |
| Auto-reconnect ON + exact game exit | Healthy new PID/session/start identity recovered |
| Auto-reconnect OFF + exact game exit | No adopted successor for 35 s; final UI Close restored files and re-enabled Start |
| Final ZIP Stop during pre-ready automatic recovery | PASS: paused replacement exited, files restored, session cleaned, no successor for 15 s |
| Stop during local reconnect | Exact owner exited, desired-running false, no successor, exact restoration |
| Home controls | Real Start, normal confirmed Close, Close Cancel, folder Cancel/accept-current and toggles PASS |
| Repair/update/restart and failure/disabled states | Genuine native producer/adoption regressions and mounted UI checks PASS; no artificial vendor update manufactured |
| Final cleanup | Three original script hashes match; no journal/adoption/target game or host processes remain |

The 30-minute soak ran the corrected transport at `2a621f94`; the final package
adds only UI controls/localization and the failed-start cancellation helper path.
Desktop/adapter C# and normal Lua transport sources are identical between that
soak and `ae7eceb4` (verified Git diff). The final extracted package separately
replayed both newly corrected failures, normal Home Start/Close and five route
recoveries. The earlier soak is not relabelled as a 30-minute final-ZIP run.

Heartbeat age in the soak: p95 0.743 s, p99 0.784 s, max 1.069 s. No sampled
route, bridge, game/server-health failure, numbered-mailbox backlog or matching
login/network error marker occurred. Sequential real game-thread request RTT
P50/P95/P99 was 110.35/128.80/143.70 ms; concurrency four was
112.50/133.70/154.30 ms. These include WebView/native/game-pump work and are not
the earlier sub-millisecond host-only measurement.

Three final-package starts reached healthy state in 34.063, 96.360 and 27.844 s;
retain the slow run as real startup variance. Controller private-memory medians
across consecutive ten-minute soak bands were 44.02, 47.52 and 47.43 MiB.
The game's private bytes grew from about 2097 to 2478 million bytes during the
observation. These finite observations do not establish an overall memory or
startup-speed advantage, or attribute the game's growth to this change.

## Scope and comparison limits

The original visible spontaneous symptom was not naturally reproduced. Under
the owner's accepted criterion it is closed for this Home scope after the
passing qualification above. A recurrence should retain its actual first-failure
timeline; no spontaneous root cause is invented from an injected fault.

LW-Control now has qualified direct in-memory adapter/Lua exchange, corroborated
presentation, same-game reattachment and exact-owner recovery/Stop. Useful
LwTool mechanisms informed these changes. LwTool's image-wide Stop is excluded.
There is still no matched legitimately idle competitor benchmark, so no claim
that this is globally faster, lighter or better in every respect is supported.

The complete-frame producer contract is important: the production writer sends
each frame in one WriteAsync. Splitting a 256 KiB frame into sequential 16 KiB
writes stalled in an inert counterexample; current single-frame writes did not.
Do not introduce split writes without qualifying a streaming receiver.

## Source-pinned package and retained evidence

- ZIP: `artifacts/home-final-acceptance-20261023/package-ae7eceb4/LW-Control-lwtool-improvement-ae7eceb464ce.zip`
- SHA-256: `53bfa7972592614a6a391276232eec9ed3090b550dda3a540df7c22074b6c294`
- Windows x64, framework-dependent; 10,361,578 bytes, 105 entries, 29 bridge
  identities verified. Source/UI fingerprints, privacy rules, CRC and every
  payload hash passed. No competitor payload, account state or diagnostic dump.
- The immutable package manifest records creation before independent review;
  this report is its separate final acceptance receipt, not a modified ZIP.
- Sanitized results: [2026-10-23-home-final-acceptance.json](evidence/2026-10-23-home-final-acceptance.json).
- Raw own-session evidence, mini dump, negative controls, actual UI captures,
  exact process receipts and one-off controllers remain ignored in
  `artifacts/home-final-acceptance-20261023/`.

Retain the negatives: the first 20-cycle attempt stopped before fault 14 after
13 successful cycles because its single-read heartbeat precondition failed;
no full-run PASS was claimed. The complete repeat passed without retries.
Both genuine live cleanup failures were fixed and replayed separately. Test
infrastructure interruptions (npm-ci/UI collision, Windows text decoding and
exclusive config reads) were corrected, not counted as product successes.
A restoration assertion accidentally run during a later Start observed the
expected installed candidate; the definitive post-Stop restoration passed.

Rollback is by reviewed reversion of the Home implementation/corrections on the
feature branch. Installed originals are already restored; no installed rollback
operation is pending. Main remains `45e151c8`; Map changes are not accepted by
this Home receipt.
