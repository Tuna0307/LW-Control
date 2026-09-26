# R8-078 — recover Map diagnostic/error and failed-run persistence split

**Date:** 2026-09-27
**Reference:** verified LWBridge 0.3.1
**Scope:** original host handling after `map.scan.diagnostic`, `map.scan.error`, and terminal failure discovered from `map.scan.complete`. No production scanner change.

## Result

The original host has two materially different failed-scan persistence paths.

A direct `map.scan.error` event uses the ordinary **fail map scan** transaction: mark the running scan run failed, clear that run's `scan_records` staging, reset visible scan state, and request `stopMapScan` when the recovered active bridge/session predicate allows it. It does **not** publish staged records into `map_records`.

A terminal failure discovered only after `map.scan.complete` uses **preserve failed map scan** instead: mark the run failed, copy the staged rows into published `map_records`, clear staging, reset visible scan state, and do not send another `stopMapScan`.
## Diagnostic event is a log side channel

The common Map event handler recognizes `map.scan.diagnostic` in the branch at `0x1403400CB-0x140340168`.

That branch formats the event payload with the exact prefix:

`map scan diagnostic `

through the Rust formatting helper at `0x1400276B0`, then forwards the resulting string to host logging helper `0x1403CE0D2`. The logging helper contains the `xlua-bridge.log` / segment path machinery.

The diagnostic branch then exits the common event handler. It does not update scan counters, lifecycle state, publication state, or failed-run persistence in this bounded path. Therefore arbitrary diagnostic text must not be promoted into retry/completion semantics without producer-side evidence.
## `map.scan.error` message precedence

The packed event compare at `0x140340C5B-0x140340C82` dispatches `map.scan.error` to `0x140341182`.

The host chooses the failure message in this exact order:

1. event field `error` when it is a JSON string;
2. current scan-state `lastError` when it is a JSON string;
3. exact fallback literal `map scan failed`.

The second step is implemented by helper `0x1403810D5`: when the caller does not provide a usable event string, the helper reads `lastError` from the current scan-state object and returns it only when it is a string.

The final literal fallback is selected at `0x14034174E-0x140341766`.
## Shared cleanup flags

Both failure families call shared cleanup helper `0x14033FBE8`, but with opposite flag pairs.

For `map.scan.error`:

- caller stack flag at `[rsp+0x28]` = 1;
- caller stack flag at `[rsp+0x30]` = 0.

Inside the helper these become the flag that enables conditional `stopMapScan` dispatch and the flag that selects ordinary failure persistence, respectively.

For terminal failure after `map.scan.complete`:

- `[rsp+0x28]` = 0;
- `[rsp+0x30]` = 1.

That disables redundant native Stop and selects the preserve-failed transaction.
The shared helper still performs the already recovered visible cleanup: `isReading=false`, idle phase, `inflightBlocks=0`, writes the selected failure message into `lastError`, sets `resumeAvailable=false`, and publishes the refreshed state.

## Ordinary fail transaction

Helper `0x140277E21-0x1402785BA` contains exact transition SQL:

`UPDATE scan_runs SET status='failed',error=?1,updated_at=?2 WHERE id=?3 AND status='running'`

and exact staging cleanup:

`DELETE FROM scan_records WHERE run_id=?1`

The bounded helper contains no direct `INSERT OR REPLACE INTO map_records(` publication xref. Its transaction diagnostics include `begin fail map scan`, `fail map scan`, `clear failed scan staging`, and `commit fail map scan`.
## Preserve-failed transaction

Helper `0x14026A12E-0x14026AAC0` uses the same conditional failed-run transition, then executes:

`INSERT OR REPLACE INTO map_records(...)`

with the matching staged projection:

`SELECT kind,server_id,record_key,point_index,uuid,name,alliance_name, level,quality,power,distance,shield_end_time,updated_at,data_json FROM scan_records WHERE run_id=?1`

and finally:

`DELETE FROM scan_records WHERE run_id=?1`

before commit.

This proves that a scan which reaches `map.scan.complete` but subsequently fails the host terminal-admission checks can still preserve the staged scan data as published map data while the run itself is recorded as failed. That behavior is distinct from a producer `map.scan.error`, which discards staging.
## Impact on remaining Map recovery

R8-078 further narrows the producer/host boundary.

The host does not infer retries from diagnostics, and it treats producer error events as abortive failures that discard staging. By contrast, once the producer has emitted `map.scan.complete`, host-detected terminal defects are treated as a completed acquisition worth preserving even though the run status becomes failed.

This still does **not** reveal producer-side block order, coordinates, `XluaBridgeMapScanTick` pacing, acknowledgement item schema/consumption, queue-drain timing, or retry scheduling. Those remain below the host event boundary and are the next acquisition target.

## Verification

`tools/inspect_lwbridge_map_scan_error_failure.py` is hash-locked to SHA-256 `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`.

Durable output:

`evidence/lwbridge-implementation/2026-09-27-r8-078-map-scan-error-failure.json`

No production scanner code changes in this checkpoint.
