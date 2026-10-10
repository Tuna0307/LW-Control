# HOME-004 R5 — second-pass Home corrections and input fence

Date: 2026-10-10. Branch: `codex/home-complete-delivery-004`.
Reviewed baseline: `c8b56e252886785528b527582f071c618302007b`;
original campaign ancestor: `f12dd566bd1410c2ccb17e054cd85978ace1c8a4`.
Target original version: **LWBridge 0.3.17**, reference executable SHA-256
`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
Disposition: **new local corrections tested; whole Home PARTIAL_NEEDS_INPUT**.
Draft PR #6; no merge, release or original service access.

## Recovered authority and second-pass findings

Re-read AGENTS, original H-01–H-47 matrix, R4 lead inverse, R4-R2 receipt,
R4 A–G full continuation and archived 0.3.17 recovery/error locators.
Read-only `git show origin/research/offline-controller` recovered
`...COMPLETION-010/c-handlers/handlers-locale.json` with all nine connection
display states (`offline`, `starting`, `recovering`, `awaitingLogin`,
`connected`, `reconnecting`, `grace`, `locked`, `error`), 101 original error
locale entries and no original/clone EN text difference. It did **not** supply
the protected original entitlement/lease/finalizer responses, current client
typed official launcher self-restart, or private controller decryption inputs.
Original 0.3.17 monitor/retry locators are retained verbatim in the matrix.

### Concrete before/after production inverses

1. **H-46 original error first-match order.** Actual `HomePage.jsx` and
   `ProfileSidebar.jsx` helpers previously inspected matching code-like words
   *in reverse*, choosing a later code over an earlier one. The additional
   `check-home-integration.mjs` test first failed with `LAUNCH_TASK_FAILED`
   instead of first `GAME_CLOSE_FAILED`; corrected helpers now prefer explicit
   structured `error.code`, then take the **first** recognized message code,
   and retain the original translated generic fallback. Both caller tests pass.
2. **H-38/39/40/42/45 sidebar late poll.** Actual production mounted B status
   read was held at the *reply* boundary after it had already read B running.
   Unselected B Stop succeeded, but releasing the old poll brought back the
   running icon. The saved immutable inverse is
   `artifacts/home-004/r5-race-inverse-en-light.json.error.txt`.
   `ProfileSidebar` now fences per-profile polling revisions across single and
   batch Start/Stop, merges fresh native results without replacing unrelated
   profiles, keeps last acknowledged state on transient poll failure, and
   uses the actual Stop provider's returned status. The **same** mounted native
   inverse passes EN/light and JA/dark. Real registry disabled B Start rejects
   with `PROFILE_LOCKED`, global update/restart skips B; locked A and B Start
   rejection and exact DB fixture restoration still pass.
3. **H-42 active recovering status.** Native process exit started a genuine
   *controlled* automatic waiting run but `CreateProfileInstanceStatus`
   projected `error` solely from the old instance's
   `GAME_EXITED_RESTORE_REQUIRED`. A new actual lifecycle check failed first.
   The producer now returns `connectionState=recovering` during active recovery
   while preserving original exact-owner `lastError` for diagnosis. Native
   regression passes; terminal failure still maps to `error` outside an active
   recovery.
4. **H-11/H-19/H-23 atomic owner errors.** Real ordered native result
   projection previously appended an owner's apparently valid `error` or
   `restarted` value before a later foreign owner item invalidated the same
   response. New mixed-owner tests failed on both reconciliation and global
   restart. Each owner response now validates completely before publication;
   malformed/empty entries and duplicate success are rejected. A later owner
   still proceeds after a previous failure. Both native checks pass.
5. **H-35/H-36 controlled long recovery.** The actual native monitor and
   recovery run now have deterministic test coverage for `Player.log`
   maintenance marker → 120-second initial retry, subsequent 120/300/600/600
   second failure ladder with 1 ms pre-deadline negative, and an active updater
   reaching the original 15-minute no-activity threshold, calling the isolated
   updater termination hook once and scheduling a normal 15-second retry.
   These source-backed timing assertions passed. The hook runs **no real
   updater or game process** and is deliberately classified CONTROLLED.

## Executed gates, retained positives and isolation

- `dotnet run --project tests/LWBridge.Desktop.Checks -c Release`: passed
  existing accepted Home launch/root, protected adoption/repair, pending stale
  Close and late successor, actual native monitor, ordered owner routing and
  token-bound transport tests, plus all R5 new inverses. Existing Map
  regression checks also pass without a new Map scan.
- `npm.cmd run check`: static frontend, Home source helpers, Map UI regression,
  recovered UI completeness and draft concurrency checks pass.
- Actual production bundled WebView/host Home-only inert A/B campaign:
  `artifacts/home-004/r5-final-en-light.json` and
  `artifacts/home-004/r5-final-ja-dark.json`; both report the late-polled B
  Stop fix and disabled restart admission, A/B stopped, no live Last War or Map
  scans, native request registry closed, active requests and subscriptions 0,
  event handlers detached, isolated roots removed, no cleanup failures.
- The real shared Windows pipe probe, final commit/push, exact source-bound
  ZIP and extracted normal GUI/capture receipts form the final R5 package
  verification; see the ignored `artifacts/home-004/r5-*-receipt.json` or
  the appended durable campaign continuation.

The earlier lead-accepted current-client Launch → authenticated Connected →
Close, auto-startup, same-build adoption, process-exit recovery, and
R3-R1 hang/connected recovery remain accepted at **their previous scope**.
This R5 work used inert owners. It did not rerun accepted real fault witnesses,
change installed game scripts or acquire original licensing service access.

## Remaining original input requirements, after second-pass inventory

The maintained [47-row action queue](HOME_004_R4_QUEUE.md) states the next
original/per-client input for each still-open obligation. Priority remaining
inputs are **authorized successful** original 0.3.17 ticket/lease/capacity
response and finalizer serialized callbacks (H-05/H-13/H-28); legitimately
available original controller plaintext inputs for game-side forwarded
automation and original native transition details (H-33/H-36); actual
supported original/current official launcher self-restart, Lua/update,
descriptor/mutex and failed helper traces (H-06–09/H-44/H-46); a supported
independent real game installation and commercial multi-profile admission
witness (H-22/H-24/H-38/39); and genuinely paired offline-only still-alive
loss, prolonged maintenance/update retry, repair/rollback and conditional
EN/JA Home original UI observations (H-18–20/H-34–36/H-40–47).

Alternative source read-only original schema recovery, actual local helper
spawn tests, live historical single-profile receipts, native synthetic-log
monitor and mounted two inert owners **do not satisfy** those missing genuine
original-client inputs. No fake entitlement, typed self-restart trigger,
protected endpoint request, risky induced update or guessed original numeric
capacity was used. Independent lead review is required; whole Home stays
**PARTIAL_NEEDS_INPUT**, not completed A→A.
