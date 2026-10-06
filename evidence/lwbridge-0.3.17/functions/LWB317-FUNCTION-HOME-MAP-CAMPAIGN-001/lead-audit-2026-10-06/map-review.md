# Map E-I independent lead source audit — 2026-10-06

Scope: committed checkpoint `540bc53d73053bb1eb70d6a09fea851a9e96d625` plus the frozen WIP identified by `worker-checkpoint.json`. Source review only; this helper ran no builds, tests, host/game/browser processes or Git mutations. It edited only this report. Main lead owns execution and acceptance.

**Result: useful implementation exists, but Map closeout remains AWAITING_REVIEW.** The older `map-lane/recovery-audit.md` predates important WIP fixes and must not be repeated as current fact. Two remaining source-supported ownership findings need a distinguishing regression case before E/F/H closeout.

## Previously reported defects corrected in WIP

1. **Fresh-server context:** `MapScanStateMachine.RefreshContextAsync` (`src/LWBridge.Map-0.3.17/MapScanStateMachine.cs:116-148`) now resolves/validates an idle provider context and updates the underlying state. `MapControlPlane.cs:38-39` exposes it; `Map317CommandService.ReadScanStatusAsync` (`:470-482`) invokes it, and `map_summary` / `map_scan_clear` (`:221-229`) first read status. This addresses the original server-0 summary/fresh-clear implementation defect. `MapCampaignCanonicalChecks.cs:301-329` adds a fresh control/store refresh/summary/clear case. That case calls the control directly, so a fresh **native command service** restart case remains useful; it is not evidence that the old defect persists.
2. **Auto jump/start admission:** `MapAutoScanExecutionBoundary` now exposes one `StartTargetScanAsync`, rather than separate jump/start callbacks (`MapAutoScanCommandService.cs:46-53`, `:469-474`). Production maps it to `Map317CommandService.StartAutoScanTargetAsync` (`LWBridgeWindow.cs:283-294`). That method holds `scanTransitionGate`, acquires the shared process scan lease **before** movement, then starts the scan (`Map317CommandService.cs:143-174`). Manual Start uses the same gate/lease (`:410-437`); return movement uses the same gate and a temporary process lease (`:176-195`). The old movement-after-manual-admission claim is therefore corrected structurally. The added fake-boundary case (`MapAutoScanCommandServiceChecks.cs:353-382`) models manual ownership before target start, but does not exercise the actual native lease boundary.
3. **Real Auto loop:** `RealSchedulerLoopAdmitsDueCycleAndRetiresAsync` now exists (`MapAutoScanCommandServiceChecks.cs:528-595`) and starts the actual service scheduler at `:575` with injected delay/clock. The earlier statement that all tests use `startScheduler:false` is stale. Main lead must record its execution result.
4. **Workbook edges:** `MapCampaignCanonicalChecks.VerifyCityWorkbookEdges` (`:388-517`) adds a 205-row multipage City workbook, Unicode/large UID, null fields, second-server same-key isolation, empty output and write failure. The earlier two-row-only statement is stale. Native dialog cancellation remains explicitly unexecuted (`:75-76`).

## P1 — Auto cancellation can stop a newly admitted manual run

This is a separate run-identity defect after the movement fix.

- Auto sets only `ownsActiveScan=1` after Start (`MapAutoScanCommandService.cs:475`). It waits between polls (`:486-497`) and clears that flag only after seeing `IsReading=false` (`:499`).
- The runtime DTO carries server/read/error, with no `ScanRunId` (`:41-44`), and Start returns no ownership token (`:51`). `StopOwnedScanAsync` checks only the boolean and invokes unscoped Stop (`:671-679`). Disable/cancel/retire reach it through `CancelCycleAndStopOwnedScanAsync` (`:662-668`); timeout also reaches it (`:488-491`).
- Production `Map317CommandService.StopAutoScanAsync` stops the current control unconditionally (`:197-199`). `MapScanStateMachine.StopAsync` cancels the **current** `before.ScanRunId` (`:360-365`).
- Production provider publishes completion (`CurrentClientMap317ScanProvider.cs:298`), then emits `RunTerminated` (`:239-250`), which the service subscribes to lease release (`Map317CommandService.cs:60`, `:506-514`). A manual Start can then acquire the released lease before Auto's next status poll.

Concrete interleaving: Auto run A completes and releases the lease; manual run M starts; Auto still has `ownsActiveScan=1`; disabling/retiring Auto or reaching its deadline invokes unscoped Stop and cancels M. If it merely polls, M's `IsReading=true` is also mistaken for A continuing.

The existing fake hides this interleaving: it completes Auto only **inside** `ReadStatusAsync` (`MapAutoScanCommandServiceChecks.cs:682-686`), so it cannot model external provider completion followed by manual replacement before the poll. `ManualOwnershipAfterAdmissionCannotBeMovedAsync` is an admission test, not a completion/replacement test.

Continuation: propagate an internal run-scoped owner token from atomic native target Start and implement an atomic native stop/status-if-owned boundary. A boolean recheck alone retains the race. Add a deterministic barrier case through the real injected `Map317CommandService` boundary: finish A, admit M before Auto poll, then disable/retire/timeout Auto; assert M remains active and unstopped, no movement occurs, and Auto retires its own token. No live game is required.

## P2 — unavailable refresh leaves stale live authorization for Clear

Retaining old-server **browse data** is intentional and is not itself a defect (`map-lane/findings.md:74`; search/options/export accept an explicit stored server). The narrower issue is the retained `ServerIdSource="live"` being reused as current Clear authorization.

After a successful context refresh, make the provider unavailable. `RefreshContextAsync` fails before changing state (`MapScanStateMachine.cs:126-128`, `:520-531`); `ReadScanStatusAsync` swallows that failure and retains the previous state (`Map317CommandService.cs:479-481`). `map_scan_clear` then continues (`:221-224`). `ClearAsync` validates only retained server/source values (`MapScanStateMachine.cs:417-423`) and deletes that server's durable rows. Native backend scope validation checks profile identity, not connectivity (`LWBridgeBackend.cs:1099-1114`).

Continuation: first confirm the recovered Clear ownership contract; preserve offline history browsing. If Clear requires current live ownership as this implementation declares, distinguish stale historical identity from current provider authorization and fail closed on unavailable/foreign context. Add native-service live-context → unavailable → Clear and live-context → different server → Clear cases. The existing fresh-context test covers only the positive control path (`MapCampaignCanonicalChecks.cs:301-329`).

## Remaining coverage and documentation boundary

| Milestone | Present source/check coverage | Remaining deliverable |
| --- | --- | --- |
| E, all eight | All-eight staged publication and reopen; category filters/options/marks/cache overlay in `MapCampaignCanonicalChecks.cs:110-299`, synthetic rows `:671-771`. Existing `QueryChecks.cs` additionally covers City mark/alliance/health/shield, Monster distance, staging/publication isolation. | Recovered-record hash/source locators for acceptance fixtures; category DTO absence/default/order/grouping matrix. Same-profile second-server isolation now has **City/export** proof (`:388-517`), but not all-eight options/marks/filter isolation. Do not label one representative row per kind full contract coverage. |
| F, manual scan | Existing `ScanStateChecks.cs:44-274` covers modes/types, duplicate Start, derived progress, stop/restart/clear, local cancellation failure, resume unavailable and provider failure. Current WIP adds context refresh and command lease integration. | Native-service/provider/store acquisition → publication → fresh-service summary/query/options/export/clear proof, interrupted startup, provider loss/replacement, disposal and run identity. Resume stays BLOCKED without a true producer checkpoint; no fabricated success. |
| G, export/actions | New workbook edges cover many former gaps. Action checks cover inert navigation/treasure/share, unavailable provider and protected timeout (`ActionChecks.cs:16-145`). | Native save-dialog cancel and host write-failure/ack path remain unproved (`LWBridgeWindow.cs:2964-2997`); actual service routing/restart/profile/error cases, alliance sentinel decoding and source DTO edge mapping must be recorded. Layer-only checks are useful, not full desktop proof. |
| H, Auto | Native profile config/persistence/cycle core, manual-before-target fake case, and real deterministic scheduler-loop case exist. Canonical production binds the real native seam. | Fix run replacement cancellation finding; prove native lease barriers between targets/return, positive production-equivalent native cycle with real config/store and controlled acquisition, stop/dispose/restart/deferred events. Refresh stale `auto-scan.md`, acceptance and queue after lead execution. |
| I, jobs/Treasure | Canonical job durability/duplicate/restart/failure/retry/cancel/history cases (`MapCampaignCanonicalChecks.cs:531-665`), broader worker timeout/disconnect/server-day cases (`PlunderWorkerChecks.cs:18-368`). Native isolated constructor now exists (`Map317CommandService.cs:84-120`). | Production-equivalent injected service with workers enabled and unavailable action provider must prove no dispatch, event/profile ownership and shutdown/restart. Isolated desktop fixture turns workers off (`LWBridgeWindow.cs:325-329`), so cannot close that case. Treasure refresh/current-client state proof and protected provider blockers remain distinct. |

Normal `Map317CommandService` still starts its real Dispatch and Truck workers during construction (`:61-76`). The newly injected constructor permits inert provider and optional worker startup; this resolves the earlier lack-of-seam concern. Offline acceptance must use that seam, never normal account composition. The isolated desktop mode also makes Auto offline and disables its scheduler (`LWBridgeWindow.cs:330-341`), so it proves UI/native config transport, not a positive Auto cycle.

Treasure claim/status and Ghost preparation remain correctly fail-closed in `CurrentClientMap317ActionProvider.cs:90-96`, `:117-121`. `compatibility.md` keeps positive Railway/Ghost/Treasure output and live actions separate from installed-anchor/source compatibility. This helper did not independently hash installed artifacts or validate historical live artifacts.

Queue H currently says no native persistence/admission/scheduler exists; current source contradicts it. `map-lane/auto-scan.md`, `acceptance.md` and `recovery-audit.md` describe earlier states. Update campaign queue/progress/continuation and operation evidence after testing, with honest `IMPLEMENTED_NOT_VALIDATED`, `BLOCKED` and offline proof boundaries. No Map master status upgrade follows from this source-only report.

## Recommended continuation order

1. Preserve frozen WIP; fix and regression-prove Auto run identity and stale Clear authorization.
2. Exercise the new injected native service boundary, including positive acquisition/durable reopen, actual manual/Auto leases and absent-provider enabled workers.
3. Close the all-eight DTO/server/profile matrix and canonical host export cancel/failure cases.
4. Complete genuine desktop adversarial/restart/event proof, reconcile stale documentation, then main lead validates and independently accepts the milestone. Keep live actions and population/provider blockers explicit.
