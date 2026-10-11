# Independent lead review — MAP-005 and available MAP-006

Reviewed source: MAP-005 `023e34d0fae328bfeef5be36858b773a22a8b520` and
MAP-006 `cdb0cfccccd998264571896c9a0bd8523b86e2e4`. 2026-10-11.

**PR #7 ACCEPTED, merged and published. PR #8 CHANGES_REQUIRED.**
The corrected candidate must come back for independent acceptance; the passing
general suites do not close the distinguishing failure below.

## LEADMAP006-01 — cross-server Auto Stop returns before Stop settles (P1)

`CancelCycleAndStopOwnedScanAsync` cancels the cycle token, then awaits the
exact native Stop. Independently, RunCycleAsync catches cancellation and enters
its finally block, calling ReturnServerAsync whenever a foreign target was
attempted. That path can therefore run while native Stop still holds the scan
lease. The production return boundary rejects an active lease/reading scan.

This is the same teardown-order class as the worker's saved first-origin failure,
but the latest fix only skips return when no other target was attempted. It
does not settle the cross-server ordering. Existing Cancel test uses [317,318]
and stops at 317 with an immediately fulfilled Stop; no return is attempted.

Independent controlled reproduction links the exact current production scheduler:

```
dotnet run --project tools/map_006_lead_stop_probe/Probe.csproj -c Release
```

Start at 317, scan target 318, hold native Stop pending, cancel Auto. Result:
returnBeforeStop=true; one premature return; currentServer=318;
LastCycle.Outcome=canceled; LastError=MAP_AUTO_SCAN_RETURN_FAILED:
SCAN_RUNNING: native Stop still owns lease. Native Stop later finishes but
there is no second correctly ordered return. No game or desktop action occurs.

Immutable negative receipt:
`docs/proof/MAP-006-LEAD-OFF-ORIGIN-STOP-2026-10-11.json`.
Scheduler source SHA-256:
`5747DB5B75702C0B0B3529D131A13878DDBFCB00A353C01342DEF9D110287402`.

Required correction: settle the exact captured native run and lease before a
requested return, without deadlocks, duplicate Stops, touching a successor,
or hiding genuine failed Stop/return errors. Cover both cancellation orderings,
foreign first and later targets, held Start/Stop, failures and retirement.
Do not add blind retry or skip a requested foreign-server return to pass.

## LEADMAP006-02 — fastest-method assertion is not established

No completed like-for-like alternative-method benchmark exists. The worker's
assessment is candid: saved elapsed times and a two-server same-method Auto
run do not establish the fastest path. Logical blocks are not request counts.
The latest six-type run took 232.346 seconds; Truck-only runs took roughly
107–131 seconds at different servers/times. These are functional measurements.

Native full-world coverage and unique records are valuable, but counts alone
do not establish perfect recall/field fidelity against the original or every
live object. Compare suitable type-specific official lists and detailed AOI
routes for freshness, coverage, unique identities/fields, requests/retries,
elapsed time and camera disturbance. Use paired ordering and explain changing
populations. A faster incomplete cache is not an accepted optimization.
This is an owner-requested assessment, not a claim that all paths are available
or a universal optimality gate. Keep the reliable baseline unless a candidate
actually passes the accuracy, ownership, cancellation and restoration gates.

## Independent positive checks and limits

Fresh canonical frontend five-group check, production UI integrity, Release
Desktop build (0 warnings/errors), default Desktop native suite and real Windows
pipe recovery/concurrent-session probe all passed. No new live game was started.

Reopened a read-only copy of the genuine six-kind DB: City 7,060, Resource 8,003,
Monster 8,850, Truck 459, Railway 2, Dispatch 1,710; all keys unique, completed
2,500/2,500 run, zero failed/staged rows. Actual native search/options, pagination,
name-key/level filters and reopened 12-column City XLSX passed. Expiring arrivals
are bracketed by query timestamps; this is a valid independent count check,
not deletion of records to force equality. Current query-visible counts can be
smaller than historical publication as arrivals expire.

Both ZIPs independently extracted: 83 entries / 81 files, source identities,
EXE hashes and no original scripts/private map DB. MAP-005 extracted EXE rendered
an inert Map screenshot and exited 0 with zero game processes. Its actual capture
language was Simplified Chinese, so it is not new EN/JA native evidence.
MAP-006 ZIP integrity passes but does not remove LEADMAP006-01.

Reviewed saved genuine completion/Auto Stop/navigation/restoration receipts;
did not rerun complete live scans or claim original-runtime comparisons. The
Connected polling change retains the last acknowledged same-owner status and
still invalidates on actual rejection/offline/profile change in the current
callback checks; no new blocking status defect found.

Treasure seasonal/full acceptance and Ghost positive availability are deferred;
Ghost/Treasure action providers remain incomplete. Plunder/sharing live witnesses
are owner-only. Unknown Resource/Monster labels are disclosed, not invented.
Whole Map remains PARTIAL. Single-game Home remains accepted; two-game F-07 is
separate. No promotion of these pending areas follows from six positive kinds.

## Delivery and continuation

MAP-005 main merge `da96fd109ea88691a378a2529fac9365e307e915` and release
`manual-map-v0.5.0` are complete; the uploaded SHA-256 equals the checked ZIP.
Main's documentation receipt is `b56d2549c11be51c2b09f3f4567b7dcde1dedc65`.
Merged local/remote MAP-005 branch retired after ancestry checks; ignored
private artifacts/packages preserved. Main merged into the existing MAP-006
branch without product changes; no additional branch/worktree created.

Continue only the existing MAP-006 branch with the R1 work item, retain this
negative evidence, fix the exact Stop ordering and perform the bounded efficiency
assessment. Return READY_FOR_LEAD_FUNCTIONAL_REVIEW for that scope; do not merge
or publish PR #8 as the worker.
