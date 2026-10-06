# Canonical Map317 deterministic campaign acceptance

Work item: `LWB317-FUNCTION-HOME-MAP-CAMPAIGN-001`
Lane: Map G/I deterministic acceptance
Entrypoint: `LWBridge.Desktop.Checks.MapCampaignCanonicalChecks.RunAsync()`

## Execution boundary

`MapCampaignCanonicalChecks` is intentionally **not** a `ModuleInitializer`. A coordinator must invoke `RunAsync()` from an explicit non-live flag. This lane does not edit `Program.cs`, production code, existing checks, or Git state.

The check uses only canonical `LWBridge.Map317` storage/export/control/action primitives. It does not use the legacy Desktop `MapDataStore`, does not open a browser/WebView, does not launch a process, and does not talk to a game or network provider. All asynchronous provider-facing work is backed by an in-memory inert `IMapActionProvider`, and the top-level acceptance owns a five-second cancellation bound while protected worker calls use a 250 ms bound.

This implementation was source-audited only in this bounded lane. It was **not executed or built here** because the lane explicitly prohibits process actions. Coordinator execution/compilation is therefore still required before recording a green runtime result.

## G — canonical Map data/store/export acceptance

The check creates a unique temporary runtime root and two profile IDs, deriving both database paths with `MapStore.DatabasePathForRuntimeRoot`. Profile A receives the campaign data; Profile B is opened separately and must remain empty, including player marks.

Profile A uses the canonical scan-store publication transaction rather than direct legacy storage: one `MapScanRun` selects `MapKinds.All`, representative rows are staged with `MapStore.StageRecord`, and `MapStore.CompleteScan` atomically publishes the run. The fixture contains every original kind — City, Resource, Monster, Truck, Railway, Dispatch, Ghost and Treasure — with two City rows for deterministic sort/page proof and two Treasure rows for ordinary plus season-supply read paths.

After closing and reopening the SQLite store, acceptance proves:

- exact all-eight summary counts survive reopen;
- City `updatedAt desc` paging returns Alpha then Beta one row per page;
- City keyword/alliance/marked filtering uses the persisted player mark;
- Resource and Monster name-key filters return their canonical rows;
- Truck and Railway quality/item/plunderable filters execute against persisted rows;
- Dispatch completion/plunderable/special/level filtering executes against persisted rows;
- Ghost is exercised only as a read-only query/filter row, with no Ghost action/provider execution;
- Map options preserve all-eight counts, City alliances, Resource/Monster names, Dispatch levels, Truck/Railway reward items, Treasure type options and the completed scan-run progress row;
- the player mark survives reopen;
- a locally seeded Treasure claim-state cache is overlaid by Treasure search after reopen, and `ReadSeasonSupplyTreasureRows` remains a local read-only list operation.

### City workbook

The acceptance invokes the canonical `MapExporter.ExportCitiesToPath` with a real temporary `.xlsx` path. It requires a non-canceled two-row `CityExportResult`, a non-empty file, and the exact six recovered XLSX package parts:

`[Content_Types].xml`, `_rels/.rels`, `xl/workbook.xml`, `xl/_rels/workbook.xml.rels`, `xl/styles.xml`, and `xl/worksheets/sheet1.xml`.

The generated worksheet is reopened through `ZipArchive`/XML and must contain dimension `A1:L3`, exactly one header plus two data rows, Alpha/owner-alpha/city-alpha/Yes on the first row and Beta/No on the second. The temporary workbook is deleted after inspection.

The native save-dialog cancellation branch is **not claimed** here. `MapExporter` accepts an already-resolved path; the user-cancel decision belongs to the Desktop window host above this Map317 layer. A host-owned deterministic cancellation test is still required if campaign acceptance needs that exact UI branch.

## I — canonical durable Dispatch/Truck plunder acceptance

The plunder section uses `MapActionControlPlane`, `MapPlunderWorker`, `MapStore`, and a deterministic inert `IMapActionProvider`. The provider reports one local current server, returns no result events/server-day refresh, and never performs network or game activity. Dispatch/Truck arm calls return synthetic local results only. Every unrelated provider action is fail-closed with `TEST_PROVIDER_BLOCKED`.

Acceptance covers the source-supported durable semantics below:

1. Schedule one Dispatch and one Truck job and confirm list persistence.
2. Reschedule the same identities while still `scheduled`; each identity remains one active durable row. Truck receives a fresh job ID without creating history for the non-terminal prior schedule.
3. Run one inert successful arm pass. Both jobs become `running`, proving the worker/store transition without live provider activity.
4. Attempt the same identities while `running`; Dispatch and Truck both fail admission with `MAP_DATA_ERROR` rather than replacing owned running work.
5. Close/reopen the database while both rows are `running`; the durable running rows must still be present before reconciliation.
6. Create a fresh worker and call `RecoverAfterRestart()`. Dispatch becomes `waiting_connection` with `DISPATCH_PLUNDER_CLIENT_RESTARTED`; Truck becomes `waiting_connection` with `client restarted`.
7. Run one inert rejected arm pass. Both jobs become `failed` with synthetic test-only error codes and the second durable attempt count.
8. Retry both jobs. Dispatch returns to `scheduled`; Truck creates a fresh active job ID and archives the failed prior attempt.
9. Cancel the retried active jobs, then clear terminal history. Exactly one Dispatch row and the current plus archived Truck rows are removed, leaving both lists empty.

This is local durability/control acceptance only. It does not claim real Dispatch send results, Truck battles/rewards, server-day provider behavior, or current-client transport behavior.

## Explicit blockers retained

Treasure claim/status and Treasure refresh are not executed. They cross the protected/current-client `IMapActionProvider` boundary; this lane proves only local persisted cache overlay and read-only list behavior.

Ghost execution/plunder scheduling is not executed. Canonical Ghost scheduling requires `PrepareGhostPlunderTasksAsync`, which is a protected/current-client provider action. Ghost remains covered only by persisted Map query/filter acceptance.

Native City save-dialog cancellation is not executable below the Desktop window host and remains a separate host-level proof gap.

## Coordinator contract

The coordinator may dispatch the new check under its campaign-owned explicit non-live flag by awaiting:

```csharp
JsonElement result = await LWBridge.Desktop.Checks.MapCampaignCanonicalChecks.RunAsync();
```

No flag registration is added by this lane. A successful coordinator run should require `result.ok == true`; the returned JSON also records that live-provider calls, Treasure claim/status execution, Ghost execution, and native dialog cancellation are all intentionally absent.

## Recovery coordinator resolution — 2026-10-06

The coordinator added the explicit `--map-campaign-canonical-check` dispatch and ran
it successfully after the recovery fixes. The final canonical result covers profile
and second-server isolation, eight-kind staged publication/reopen, sort/filter/page,
options, marks, fresh-service server rehydration, fresh destructive Clear,
205-row/Unicode/large-UID/null/empty XLSX cases, write failure, and inert durable
Dispatch/Truck duplicate/restart/retry/cancel/clear behavior.

The host-owned dialog branch that this layer could not execute is now covered above
Map317 by the packaged desktop packet: the actual City Export React control takes the
cancel path, a real isolated write-failure path, then writes a real XLSX successfully.
The same packet drives the actual Clear control against profile B, confirms all eight
native B counts are zero, and returns to A with A's rows intact.

Treasure claim/status and Ghost preparation remain correctly blocked at the protected
current-client provider. They are not promoted by this recovery.

## RECOVERY-002 resolution — 2026-10-07

RECOVERY-002 adds the previously missing executable boundaries. The real controlled Map317CommandService now proves provider completion/partial failure, fresh-service read/export/clear, interrupted startup, exact-run Stop/disposal and independent database ownership. Auto tests cover positive multiserver completion, disable, timeout/cancel/retirement, between-target/manual-owner barriers and return-to-origin. Actual due/in-flight plunder workers preserve durable restart/profile state without forbidden protected calls. The all-eight DTO fixture is hash/source-located and tests missing/null/default/order/grouping plus server/profile isolation.

The fresh packaged proof also covers edit-before-hydration, independent Auto action/runtime errors, real isolated export and full-session issue history. Treasure claim/status and Ghost preparation remain protected-provider blockers and were not executed.
