# RECOVERY-002 Map/native source review — 2026-10-07

The coordinating lead supplied current checkpoint `59d3cde5` and prior review baseline `da629bce`. This reviewer inspected current files and the preserved RECOVERY-001 findings, AGENTS and RECOVERY-002 checkpoint 4. No tests/builds, native/browser/game processes, Git edits or production edits were performed. The only output is this new report. Test execution and final acceptance belong to the lead.

**Disposition: substantial corrections and authentic new offline native proof are present. MR-01 and MR-02 should not be repeated as unchanged defects. One remaining production Stop cancellation interleaving needs correction/inverse proof before unconditional native lifetime closeout.** Additional coverage limits below are distinguished from that defect and from external/live limits.

## M2-01 — cancellation after durable Stop can skip provider termination

**P1 source-confirmed permitted production interleaving; not executed by this reviewer.**

Source chain:

- `src/LWBridge.Map-0.3.17/MapScanStateMachine.cs:355-365` acquires the operation gate and commits authoritative local cancellation of a reading run.
- The concrete sink checks caller cancellation **before** its synchronous database operation, then returns after `store.CancelScan` (`src/LWBridge.Map-0.3.17/MapControlPlane.cs:195-199`). It does not guarantee the token remains uncanceled after commit.
- The state machine next passes that same token into `provider.StopMapScanAsync` (`MapScanStateMachine.cs:371-375`), swallows all provider exceptions (`:377-380`), and changes shared state to `IsReading=false`, `Phase=idle`, `Error=null` (`:383-397`).
- The new production provider now begins Stop with `cancellationToken.ThrowIfCancellationRequested()` (`src/LWBridge.Desktop/CurrentClientMap317ScanProvider.cs:174-176`), before it even captures/cancels `activeCancellation` (`:179-186`). Its subsequent noncancelable wait correctly protects termination once this initial check has passed (`:187-194`), but does not protect the earlier boundary.

Concrete interleaving:

1. Manual run A is actively capturing under its process lease.
2. A native `map_scan_stop` request enters both gates with a live caller token and commits the local durable cancellation.
3. The request token is canceled after that commit and before the provider's line 176 check. Document/request retirement can supply such cancellation; ordinary thread scheduling permits this interval.
4. Provider Stop throws without canceling A's capture token. The state machine swallows the exception and publishes idle.
5. Native capture A continues until an independent completion/failure/disposal, and the process lease remains owned. A second Stop sees `before.IsReading=false` and therefore skips provider Stop entirely. Another Start rejects `SCAN_RUNNING` despite idle status; Clear can be admitted from idle state while the old capture is still unwinding/running.

This does **not** revive the old early-lease-release defect: the new run-scoped lease remains held. The discrepancy is that a committed Stop reports idle while its capture was never signaled to stop. Auto's internal owned Stop uses `CancellationToken.None` (`MapAutoScanCommandService.cs:679-683`), so the direct affected path is the cancelable manual/native Stop command, not the stale Auto-owner regression.

The new `StopWaitsForExactProviderTerminalBeforeLeaseReuseAsync` test cancels only **after** `provider.StopEntered` (`tests/LWBridge.Desktop.Checks/Map317NativeBoundaryChecks.cs:434-444`), where its delayed provider already passed its initial check (`:536-542`). It proves the corrected later wait, but misses cancellation between the local commit and provider entry. The packaged inert provider repeats the same initial canceled-token check (`LWBridgeWindow.cs:5742-5745`).

Required correction: preserve cancelable admission/local persistence, but once local Stop has committed, ensure the exact run's provider cancellation/terminalization is driven independently of the retired caller token. Add an explicit post-local-commit/pre-provider barrier inverse through the native service and a controlled run provider. Assert that capture is signaled, exact terminal callback releases the lease, repeated Stop is coherent, and Start can admit a new run only after termination. Do not weaken run-scoped lease ownership or mark provider success on exception. No game or protected provider is needed.

## Previous findings corrected in current source

### MR-01 — actual shared run lifetime now exists

`src/LWBridge.Desktop/IMap317RunScopedProvider.cs:9-13` extends the provider with `IDisposable`, exact-run terminal events, and post-durable-admission activation. Normal `CurrentClientMap317ScanProvider` implements it (`:10,19,109-120,255`); injected composition recognizes/subscribes the same contract (`Map317CommandService.cs:96-108`). Accepting non-run-scoped providers now fail instead of silently entering an unfinishable scan (`:503-505`). This is an explicit clone/internal execution seam, not an invented original command.

The service registers the admitted run before activation (`Map317CommandService.cs:506-507`, `:614-623`), and `ReleaseScanLease(expectedRunId)` refuses another run's terminal callback (`:626-638`). Disposal waits for the provider's own Dispose before releasing the remaining lease/store (`:439-452`). Manual Stop now shares the transition gate (`:519-529`). Normal provider Stop cancels and awaits its engine without caller cancellation cutting the wait short (`CurrentClientMap317ScanProvider.cs:186-194`), subject to M2-01 above.

The old reflection-based native completion helper is gone from the current Auto test. Controlled providers call their injected public activation/control/terminal boundary (`MapAutoScanCommandServiceChecks.cs:1366-1401`). This is legitimate controlled provider substitution, not a private service-lease escape.

### MR-02 — failed lease initialization is exception-safe

`Map317ScanProcessLease.cs:23-37` separates opening from initialization. Initialization errors dispose the successfully opened stream before translating I/O/access failures to `MAP_SCAN_LOCK_FAILED` (`:39-65`). The owner wrapper is created only on success (`:58`). `Map317RestartChecks.cs:25-38,47-62` injects both metadata-write and flush failures and requires immediate successful reacquisition. This directly distinguishes the prior leaked-handle fault; real disk corruption is unnecessary. The initializer remains a module-initializer check, while the new native suites have explicit runner branches (`Program.cs:338-355`).

### MA-01/MA-02/deadline corrections are preserved

Run-token status/stop, manual/Auto atomic Start/return admission, fresh authorization for Clear, and scheduler-owned ordinary-edit deadlines remain present. The new fixes do not revert those previously credited corrections. The specific new Stop caller-cancellation finding is independent of stale token rejection and unavailable-provider Clear authorization.

## New checks: authentic proof and its boundaries

| Assigned branch | Inspected actual proof | Assessment |
| --- | --- | --- |
| Native acquisition/publication/reopen | `Map317NativeBoundaryChecks.cs:32-129` invokes actual native Start; its controlled activated provider stages a City row, reports progress, completes, and emits exact termination (`:550-581`). A fresh native service queries summary/search, writes XLSX and Clears without reseeding. | Substantive native-service proof at the explicit inert external-provider seam. This now closes the previously missing connection; it is not current-client algorithm/live proof. |
| Partial/provider terminal failure | `Map317NativeBoundaryChecks.cs:132-188` fails an activated run after staging, checks exact error/lease release, starts another run on the same service, and reopens partial publication. | Genuine storage/service failure wiring; synthetic failure is honestly labeled. No game disconnect/capture transport was exercised. |
| Pending admission interruption | `:272-323` holds actual provider Start, verifies the lease remains held, cancels caller acceptance, then verifies immediate reacquisition and retry. | Useful native pending-Start cancellation test. It is not a persisted interrupted-running-row restart test. |
| Stop/disposal/isolation | `:415-470` checks delayed Stop/caller cancellation/lease reuse/Start -> Stop -> Start; `:234-269` disposes an active controlled run and retries fresh; `:326-374` holds two independent profile DB acquisitions and stops them independently. | The previously missing normal injected termination/reuse/disposal wiring is covered. Disposal's provider has a held logical run rather than an asynchronous block-capture task; the delayed Stop barrier tests a real unfinished provider operation. M2-01 remains uncovered. |
| Native Auto positive cycle | `MapAutoScanCommandServiceChecks.cs:593-689` runs the real Auto service over the real Map317 service, completes two exact controlled runs, checks order/origin return, and reopens native config/store without reseeding. | Meaningful positive multi-server native proof, unlike the previous fake-only cycle. |
| Native Auto cancellation/timeout/retirement | `:693-879` drives actual Map317 service Stop and terminal callbacks, checks exact owned IDs, errors/deadlines and no retirement return. | Real native proof of active Auto cancellation paths. Distinguish these from replacing Auto with a Manual run before cancellation. |
| Native replacement/manual movement | Stale A -> M -> disable at `:498-589`; first target complete -> M before status/next target at `:883-942`, checking M stays reading and neither next target nor origin return moves/stops it. | Authentic callback-driven replacement cases without reflection; prior stale-owner finding is structurally and behaviorally addressed. |
| Due plunder workers | `Map317PlunderWorkerBoundaryChecks.cs:26-73` schedules due jobs, waits for actual workers, checks zero arm calls unavailable, then successful arm/drain/events after reopen. `:110-164` holds both arms, disposes the owner, records token cancellation, releases late successes and proves they are not consumed, then verifies restart and B isolation. | Substantive actual native-worker/control/store proof; the old future-job-only finding is resolved. Providers are instrumented and inert. |
| Eight-kind missing/null/default/order/grouping | `Map317DtoMatrixChecks.cs:17-76,132-270` uses a hash-pinned contract-derived fixture, asserts absence versus null, default query normalization, same-key A317/A318/B317 isolation, and category-specific filter/order/options rules. | Much broader than one positive row per kind. The fixture explicitly calls itself synthetic (`Fixtures/map317-recovery-dto-matrix.json:3`); operation matrix agrees. It is not falsely claimed to be captured original runtime rows. |

## Remaining coverage distinctions within checkpoint 4

These are scope/proof limits, not newly demonstrated production defects:

1. **Auto replacement barrier completeness:** the new timeout/cancel/retire native tests stop a still-current Auto run. Only disable and between-target/return cases install a replacement Manual M. The checkpoint specifically requests completion/replacement barriers for status, disable, timeout, cancel, retirement, between targets and return. If accepted through common stop-if-owned source plus native branch tests, record that reasoning; otherwise add the missing A-complete -> M -> timeout/cancel/retire inverses. Do not describe all three as independently executed replacement scenarios.
2. **Durable interrupted-running restart:** disposing the held run then constructing a fresh native service does exercise startup reconciliation implicitly (`Map317NativeBoundaryChecks.cs:234-269`), but the test does not read/assert the prior run's interrupted status/error, preserved partial rows or reconciliation before the next Start. The pending-acceptance interruption case creates no durable running row. An explicit interrupted-row assertion remains useful if the closeout claims that specific assigned restart contract.
3. **DTO provenance precision:** fixture byte hashing and metadata assertions are real (`Map317DtoMatrixChecks.cs:17-28,84-128`), but the fixture's cited source anchors often support filter families rather than every asserted COALESCE/fallback formula. For example Truck's missing `remainingLootCount` fallback is asserted at `:179-187` and documented in fixture `:73-80`; cited review `2026-09-30-LWB317-RE-MAP-003-storage-query-export.md:125,127-134,155-157` names plunderability/special-UR/sort families, not the complete `max(maxLootCount-robTimes,0)` predicate. Treasure fixture `:129-136` cites query/option field lists rather than the full null/default/grouping expression. Keep synthetic rule fixtures credited, but provide exact native SQL/source locators for those specific defaults if upgrading their formulas to EXACT_CONTRACT. Hashing an authored fixture alone does not establish original behavior.
4. **DTO proof layer:** the new matrix runs `MapStore.Search/ReadOptions` directly. It verifies persisted DTO field/default/query behavior and isolation; it does not assert every canonical UI/native query normalization route. Existing native all-eight transport checks remain supplementary evidence, not a claim that this particular matrix itself drives the mounted UI.

The Map report does not independently accept the mounted Home lifecycle or whole-session package driver; those belong to the other review lanes/main lead. It likewise does not assert builds/tests PASS merely from reading their source or worker logs.

## External boundaries and suggested lead disposition

Treasure claim/status, Ghost preparation, current live population, current-client scans/movement and updater execution remain outside the offline review. None of the new source finding or coverage distinctions requires a protected/live provider. Preserve the worker's implemented milestones and historical negative evidence; correct M2-01 with a distinguishing offline inverse, and reconcile the narrower coverage claims above rather than relabeling old resolved faults as current.

No full parity, whole-clone or LIVE_PROVEN result follows from this source-only review. Final disposition remains with the lead.
