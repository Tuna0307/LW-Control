# R8-016 — restore Scheduled Plunder control plane and original UI

**Date:** 2026-09-25
**Reference:** verified LWBridge 0.3.1 retained Map contract
**Scope:** Scheduled Plunder list/schedule/cancel persistence, events, API wrappers, result-tab UI and locale surface. Protected robbery execution remains intentionally unimplemented.

## Authority

Primary authority:

- original `lwbridge-0.3.1.exe`, SHA-256 `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`;
- immutable `evidence/lwbridge-0.3.1/frontend/assets/api-ClPPi2JT.js`;
- immutable `evidence/lwbridge-0.3.1/frontend/assets/MapDataPanel-C1HVeNHr.js`;
- immutable nine locale bundles;
- `docs/reviews/2026-09-24-r8-map-control-plane-exact-contract.md`, section 7.

The recovered frontend establishes that `scheduledPlunder` is a ninth **result tab**, never a scan kind.

## Restored public commands

R8-016 restores exactly the recovered Scheduled Plunder public control plane:

- `map_plunder_jobs_list()`;
- `map_dispatch_plunder_schedule({rows})`;
- `map_dispatch_plunder_cancel({serverId,taskUuid})`;
- `map_truck_plunder_schedule({rows})`;
- `map_truck_plunder_cancel({serverId,trainUuid})`.

The Map command service now owns all five so schedule/cancel success can raise the exact profile-scoped UI refresh events without reviving any worker.

## Durable store

Restored tables:

- `dispatch_plunder_jobs`;
- `truck_plunder_jobs`;
- `truck_plunder_history`.

Restored indexes:

- `idx_dispatch_plunder_due`;
- `idx_truck_plunder_due`;
- `idx_truck_plunder_history_updated`.

`map_plunder_jobs_list` returns `{dispatchJobs, truckJobs}`.

Dispatch rows are ordered with `scheduled|waiting_connection|running` first, then `plunder_at ASC`, then `updated_at DESC`.

Truck rows are the exact `truck_plunder_jobs UNION ALL truck_plunder_history` read surface, ordered with active states first, then `execute_at ASC`, then `updated_at DESC`.

Returned target JSON receives scheduler overlays:

- `scheduleStatus`;
- `attempts`;
- `lastError`;
- `scheduledAt`;
- `scheduleUpdatedAt`;
- Dispatch `plunderAt` when present;
- Truck `executeAt` when present.

Existing Truck result JSON can additionally carry `jobId`, `battleWon` and `plunderRewards`; new schedule attempts remove stale battle/reward fields before persistence.

## Dispatch schedule/cancel contract

The Dispatch frontend owns random-delay selection. It computes the final `plunderAt` before invoking the host. The backend does not add a second delay.

The host validates the complete 1..200 batch before the first write. Exact errors are:

- missing rows: `INVALID_REQUEST / secret task rows are required`;
- count outside 1..200: `INVALID_REQUEST / select between 1 and 200 secret tasks`;
- invalid row: `INVALID_REQUEST / secret task scheduling data is invalid`.

Recovered row admission is preserved: positive integer-like server ID, decimal-string UUID, positive completion time, `plunderAt >= completionTime`, expiry after `plunderAt` when present, and remaining steal capacity when a maximum is positive.

Persistence is sequential after full validation. The recovered upsert only replaces an existing `scheduled` or `waiting_connection` row. A guarded write that cannot update/insert produces:

`MAP_DATA_ERROR / scheduled plunder job is missing`.

Earlier rows remain durable if a later persistence operation fails. The Dispatch change event is emitted only after the entire loop succeeds.

Cancel validates positive signed-64-bit server ID plus decimal task UUID, changes only `scheduled|waiting_connection` to `cancelled`, clears `last_error`, and updates time. Missing/noncancellable rows return:

`NOT_FOUND / scheduled plunder job not found`.

Success emits `bridge://dispatch-plunder-changed`.

## Truck schedule/cancel contract

The restored frontend sets:

`executeAt = max(Date.now(), Number(protectTime)||0)`

before invoking the host. The backend also preserves the recovered service fallback from absent/nonpositive `executeAt` to positive `protectTime`.

The complete 1..200 batch validates before persistence. Exact errors are:

- missing rows: `INVALID_REQUEST / truck rows are required`;
- count outside 1..200: `INVALID_REQUEST / select between 1 and 200 trucks`;
- invalid row: `INVALID_REQUEST / truck scheduling data is invalid`.

Recovered row predicates are positive integer-like server ID, decimal-string UUID, positive effective execute time, `maxLootCount > 0`, and literal `robTimes < maxLootCount`.

The persistent service keeps the recovered active-reschedule semantics:

- a new attempt receives `truck-{unixTimeMilliseconds}-{lowerHex}`;
- terminal `succeeded|failed|cancelled|expired` attempts are archived before replacement;
- `scheduled|waiting_connection` reschedules are not archived;
- stale `battleWon`, `plunderRewards` and compatibility completion metadata are removed;
- the active upsert never replaces a `running` row;
- a guarded/mismatched write produces `MAP_DATA_ERROR / scheduled truck job is missing`.

The lower-hex job-ID shape is recovered. The entropy source is **not** recovered; the rebuild continues to use a local cryptographic random u64 and does not claim byte-for-byte entropy parity.

Truck cancel changes only `scheduled|waiting_connection`. Missing/noncancellable rows return:

`NOT_FOUND / scheduled truck job not found`.

Success emits `bridge://truck-plunder-changed`.

## Frontend restoration

The final generated frontend restores Scheduled Plunder instead of applying the historical R7-149 retirement transform.

Byte-identical comparisons against immutable 0.3.1 pass for:

- the 681-byte Scheduled Plunder API-wrapper block;
- the 2,467-byte tab/status definition block;
- the 7,039-byte Scheduled Plunder table/component block.

All nine original locale bundles retain the recovered Scheduled Plunder labels.

The panel again subscribes to:

- `bridge://dispatch-plunder-changed`;
- `bridge://truck-plunder-changed`.

Either event reloads `map_plunder_jobs_list()`.

The original frontend random-delay control, Dispatch schedule button, Truck schedule button, cancel eligibility, Truck Plunder Again eligibility, result/reward presentation and Scheduled Plunder count surface are restored from the recovered chunk.

The eight Map Scan kinds remain unchanged: City, Resource, Monster, Truck, Railway, Dispatch, Ghost and Treasure. `scheduledPlunder` is not added to any scan request.

## Protected execution boundary

R8-016 deliberately does **not** restore the deleted R7 workers or any replacement executor.

Absent production files:

- `DispatchPlunderWorker.cs`;
- `TruckPlunderWorker.cs`;
- old worker-heavy `MapDataStore.Plunder.cs`.

The new `MapDataStore.PlunderControlPlane.cs` contains no arm/run/expire/restart/execution helpers such as `ReadArmable*`, `TryMark*`, `RecordTruckPlunderSuccess` or recovery worker state.

No `truck-quick-rob.txt`, `dispatch-plunder.txt`, `dispatch_plunder_runtime` or `pump_truck_quick_rob` runtime is restored.

Actual robbery/steal execution, game requests, ambiguous-send ownership and protected action semantics remain a separate evidence-bound lane.

## Validation

Passed during R8-016 implementation:

- focused `tools/check_scheduled_plunder_r8016.cjs`;
- frontend generator reproducibility check;
- Release build: 0 warnings / 0 errors;
- full deterministic checks: `ok=true`, `failures=[]`.

The deterministic service tests cover:

- all five commands;
- restored tables/indexes;
- Dispatch full-batch validation before persistence;
- Dispatch returned row metadata and change events;
- Dispatch cancellation;
- positive server IDs above 99,999;
- Truck `protectTime` fallback;
- stale result-field stripping;
- Truck cancellation;
- terminal-attempt archival and active-first ordering;
- Truck full-batch validation before persistence.

Final diff/staging/JSON checks are recorded in the machine-readable checkpoint evidence.

## Remaining boundaries

R8-016 does not claim:

- protected Dispatch/Truck robbery execution;
- exact Truck job-ID entropy generation;
- protected Map acquisition/traversal/extraction internals;
- remaining `map_search` alternate-sort internals;
- complete `server_jump` protected travel internals;
- Login / Account / Authentication behavior.

The next retained Map work should finish evidence-backed alternate-sort/query details or move to another retained whole-program subsystem whose host contract is implementation-ready, without inventing protected behavior.
