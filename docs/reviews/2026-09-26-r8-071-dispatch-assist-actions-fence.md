# R8-071 — fence Dispatch Assist Schedule / Cancel / Retry on live-provider and authorization boundaries

**Date:** 2026-09-26
**Reference:** verified LWBridge 0.3.1

R8-071 closes the recoverable host/database/action contract for the three retained Dispatch Assist mutations without fabricating the still-unrecovered live alliance-dispatch task provider.

## Exact handlers

- `dispatch_assist_cancel`: `0x14010DFC8-0x14010F017`
- `dispatch_assist_schedule`: `0x140134912-0x140136EC4`
- `dispatch_assist_retry`: `0x14019D007-0x14019E6F4`

The immutable frontend payloads are `{taskUuid}` for Cancel/Retry and `{taskUuids}` for Schedule.

## Shared live-provider helpers

The native refresh helper `0x1400EEAEE-0x1400EF486` calls `getAllianceDispatchTasks` with an exact 5,000 ms result deadline. It owns the recovered `dispatch assist state unavailable` / `dispatch assist refresh unavailable` boundaries.

The arming helper `0x1400EF486-0x1400F0120` calls `armAllianceDispatchAssist` with an exact 5,000 ms deadline. The request/result vocabulary includes `targetServer`, `assistAt`, `maximumLeadMs`, `armed`, `fireAt`, `attempts`, and `leadMs`. It also owns the `dispatch assist timer unavailable` path.

All three public handlers await the shared authorization-state future before their retained work. Skipping that admission would change native public error precedence.

## Persisted job mutations

Native profile-map-data helpers prove the local job ownership:

- job read `0x14025E85C-0x14025EAEC`: reads `task_json, assist_at, status, attempts, last_error, created_at, updated_at`;
- retry update `0x14026225B-0x140262625`: rewrites the job to `status='scheduled'` with a new `assist_at`;
- cancel update `0x140262625-0x1402629D7`: rewrites the job to `status='cancelled'`, clears `last_error`, and updates the timestamp;
- delete helper `0x1402629D7-0x140262C90`;
- schedule insert `0x14026793E-0x140268029`: inserts a new `dispatch_assist_jobs` row.

The action paths publish `bridge://dispatch-assist-changed` after mutation/action transitions.

## Public validation/error evidence

Schedule requires `taskUuids`; missing/empty input uses `INVALID_REQUEST / alliance dispatch tasks are required`, and the accepted count is 1 through 200 (`INVALID_REQUEST / select between 1 and 200 tasks`). Each UUID must exist in the live task set or native returns `NOT_FOUND / alliance dispatch task unavailable`. Duplicate scheduling is rejected with `INVALID_REQUEST / dispatch assist task already scheduled`.

Retry requires a persisted job and matching live task. Exact recovered failures include `NOT_FOUND / dispatch assist job not found`, `NOT_FOUND / alliance dispatch task unavailable`, and `INVALID_REQUEST / dispatch assist job cannot be retried`.

Cancel requires a scheduled persisted job. The recovered missing-job boundary is `NOT_FOUND / scheduled assist job not found`. The live provider method is `cancelAllianceDispatchAssist` with an exact 5,000 ms result deadline; correlated normal results use the shared generic JSON converter.

## Why runtime remains fenced

R8-045 already proved that the public Dispatch Assist state combines persisted jobs with a live provider-owned task/counter/time projection. Schedule and Retry require that live task set to validate targets and derive scheduling/arming data. All three also retain mandatory authorization-state admission.

A DB-only implementation, frontend-derived task projection, or authorization bypass would therefore be observable non-reference behavior. No production route is added.

R8-071 moves `dispatch_assist_schedule`, `dispatch_assist_cancel`, and `dispatch_assist_retry` from genuinely unclosed to audited/fenced. Remaining genuinely-unclosed retained frontend routing gaps: **7**.

Evidence: `evidence/lwbridge-implementation/2026-09-26-r8-071-dispatch-assist-actions-fence.json`.
