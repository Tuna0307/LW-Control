# Lead Map finish review — 2026-10-24

Status: candidate changes pass controlled checks; new genuine package scenarios
are still required. No acceptance, merge or release is implied by this checkpoint.

The interrupted worker's pending current-version official-settlement auditor,
negative test and source-pinned handoff were preserved and reviewed. The old
recovery-journal blocker is resolved. The final `d018ea5a` Home Stop/reconnect
receipt reports the exact owner exited, no successor appeared for 15 seconds,
the official script triplet was restored and no recovery journal remained.

## Source-backed candidates

Focused read-only decoding verified the installed Lua v27 package SHA-256
`8f61102f401f3052598365ca9750c2c496969895b5add2da138cb2147d045493`.
Assembly identity remains the previously admitted current binary. Private decoded
bodies are retained under `artifacts/map-lead-finish-20261024/native/`.

- `NetworkManager.SendLuaMessage`, token `0x06001678`, obtains a native future
  ID and puts `_id` into the outgoing SFS object. `MessageFactory.DispatchResponse`,
  token `0x06001830`, reads returned `_id`, records the native future response,
  then forwards the same object to the normal message handler.
- `FutureManager.getFutureId`, token `0x0600bb9d`, increments `_futureId`.
  `NetworkManager.Connect`, token `0x06001672`, replaces `_config` before resetting
  that counter; `Reconnect` delegates to Connect. Configuration and proxy object
  identity therefore distinguish a connection reset from numeric-ID reuse.
- Resource hydration now issues a new native request for every target, even
  when the pointId-only game cache already contains a value. It observes the
  counter before/after the synchronous send, accepts only one new ID, matches
  raw response ID and point, rejects contradictory server echoes, checks the
  exact run/owner/connection scope and freezes the original handler's result
  immediately. Cached/stale values cannot supply readiness. Missing correlation
  fails closed. Native handlers and their return/error behavior are preserved.
- The native v27 WorldGetDetail OnCreate signature takes point, server, world,
  point type and UID. The old extra zero argument shifted type/UID; the new send
  follows the recovered signature without retrying a possibly-sent call.
- `WorldPointManager.AddPointInfo`, token `0x0600356e`, indexes only item IDs
  10100000 and 735000 into `baseMainBuild`; `GetAllMainBaseList` returns that index.
  Type 6 alone is broader than this main-base contract. The City cross-check now
  excludes a known positive non-main item ID, recording its aggregate category.
  Missing metadata and missing canonical identities for either main item still
  fail closed. A live counterexample is still needed before claiming this fixes
  the previously observed City failure.

## Controlled checks

- Full Release `LWBridge.Desktop.Checks` passed through
  `HOME_FEATURE_DELIVERY_CHECKS_OK` (22 native source, 10 hydration and 15
  per-row provenance rejection cases in the Resource cohort).
- Five actual-source Lua correlation tests passed: stale cache/late foreign
  overwrite, wrong point/server/missing ID, connection-reset ID reuse, retired
  run versus successor and replaced hook/foreign owner.
- Two actual-source City tests passed, including Lua 5.3 compilation of the full
  production probe, known non-main exclusion and both main-item negative cases.
- 31 package-auditor tests passed, including the interrupted worker's current
  version official-settlement source and exact prior-restoration checks.

These are controlled evidence, not new live-game positives or speed measurements.
The original fail-closed receipts and earlier benchmark limitations are retained.

## Initial live candidate and bounded repair

The `ed2a5b41` packaged saved Clear/reopen attempt reached a genuine home Resource
sweep. The new observer accepted 7,934 of 7,936 native target replies with unique
native IDs; two missing replies correctly prevented publication. This establishes
that native correlation is available in the actual runtime, but is an incomplete
scan, not acceptance. The retained run is `native-ui-20261024-110205`.

The follow-up repairs only unreceived issued targets, at most twice with an
eight-second bound per pass. Each repair uses a new observed native ID and removes
the superseded ID from the pending table; late old-attempt replies remain refused.
Seven actual-source correlation tests cover bounded exhaustion and ID retirement.
The existing pointId cache remains unused as source data.
