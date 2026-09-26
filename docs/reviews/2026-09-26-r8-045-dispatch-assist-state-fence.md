# R8-045 - fence dispatch_assist_state live-provider boundary

**Date:** 2026-09-26
**Reference:** verified LWBridge 0.3.1
**Scope:** close the native read-only `dispatch_assist_state` top-level projection, unavailable-state error, and persisted job ownership without inventing the unrecovered live alliance-dispatch provider.

## Authority

Primary native evidence:

- command handler `0x14017C7A8-0x14017DA63`;
- selected-profile runtime resolver `0x1402AE43C`;
- exact unavailable-state branch around `0x14017CDCB-0x14017CDEA`;
- persisted job list provider `0x140260FC6-0x140261342`;
- retained frontend polling/consumption in `AutomationPanel-D06CxhPI.js`.

R8-045 stays outside the protected package-key RVA lane and does not execute dispatch-help actions.

## Exact public top-level state

On a successful native state read, the handler builds a JSON object in this order:

1. `tasks`
2. `todayCount`
3. `dailyLimit`
4. `serverTime`
5. `updatedAt`
6. `jobs`

`tasks` is supplied by the live dispatch-assist state provider. The numeric counters/time fields are projected from that same live state. `jobs` is independently loaded from the profile map-data database and merged into the public state.

The retained frontend polls this command while the Daily automation tab is open. It directly consumes `tasks` and `jobs`; job rows drive scheduled/waiting/retry/running/failed/expired UI and manual cancel/retry controls.
## Exact unavailable-state behavior

If the dispatch-assist live state cannot be acquired, native fails with:

- code: `STATE_UNAVAILABLE`
- message: `dispatch assist state unavailable`

The handler also travels through the same selected-profile runtime machinery used by other recovered host commands. R8-045 does not invent profile/runtime error prose beyond already recovered stable resolver codes.

## Persisted jobs ownership

The native job provider reads the profile `dispatch_assist_jobs` table with this observable query ownership:

`SELECT task_json,assist_at,status,attempts,last_error,created_at,updated_at FROM dispatch_assist_jobs ORDER BY CASE WHEN status IN ('scheduled','waiting_connection','running') THEN 0 ELSE 1 END, assist_at ASC, updated_at DESC`

The rebuild already creates the matching table/index schema in `MapDataStore`, but no production command/service currently owns native dispatch-assist state or job-list projection.

The persisted row alone is not the complete public state: native combines it with live alliance-dispatch tasks and daily/server-time counters. Therefore exposing DB jobs with a fabricated empty live state would not be one-for-one behavior.
## Current rebuild boundary

`dispatch_assist_state`, `dispatch_assist_schedule`, `dispatch_assist_cancel`, and `dispatch_assist_retry` exist in the recovered frontend API, but the C# backend currently has no production dispatch-assist command implementation. The only related rebuild artifact is the database table/index plus generic automation config/status naming.

R8-045 intentionally makes no runtime-code change. The success path remains fenced because the live provider that supplies alliance Secret Task rows and daily/server-time state is not recovered one-for-one. A synthetic idle object, frontend-derived task list, or DB-only projection would be a new product behavior and is forbidden by the strict-parity goal.

## Remaining evidence required before implementation

- exact live provider acquisition and refresh semantics;
- exact task-row JSON shape and normalization;
- exact numeric fallback/default behavior for `todayCount`, `dailyLimit`, `serverTime`, and `updatedAt`;
- exact job-row enrichment fields merged into `task_json` (`assistAt`, scheduling status/source, attempts/error/timestamps);
- schedule/cancel/retry validation and mutation semantics before exposing those action commands.

Once those are closed, `dispatch_assist_state` can be implemented without guessing; until then it remains an explicit retained backend gap.