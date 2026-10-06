# Milestones E-J recovery audit

Campaign: `LWB317-FUNCTION-HOME-MAP-CAMPAIGN-001`
Scope: independent source/uncommitted-acceptance audit of Map Milestones E/F/G/H/I/J
Mode: offline/inert source review only; no browser, game, native host, live flag, network/provider action, or Git mutation

## Result

**BLOCKED** for E/F/H closeout. The current tree has two concrete production ownership defects that are not covered by the new deterministic acceptance, plus bounded G/E/H/I proof gaps that must stay visible before the campaign is called complete.

The canonical ownership direction itself is sound: normal production composes `Map317CommandService` and the profile-owned native `MapAutoScanCommandService`; the legacy `MapDataStore` is excluded from normal Map ownership; the production page no longer contains the recovered browser Auto Scan timer/cycle; Treasure claim/status and Ghost preparation remain fail-closed at the current-client provider; and `compatibility.md` keeps Railway/Ghost/Treasure positive-output and state-changing action limits explicit.

## Blocker 1 — fresh-process Map317 command state loses the live/persisted server

Milestones E and F require database-backed summary/per-server ownership and fresh-process query/summary/export/clear behavior. The store data survives reopen, but the production command/control layer does not restore a usable server context after process restart.

- `src/LWBridge.Map-0.3.17/MapScanStateMachine.cs:86-109` initializes the control-plane scan state with `ServerId=0` / no live server.
- `src/LWBridge.Desktop/Map317CommandService.cs:42-76` opens the durable Map317 store and reconciles interrupted scan rows, but does not hydrate `control.ScanState` from either the live current-client context or the most recent durable server.
- `Map317CommandService.ReadScanStatusAsync` (`:363-383`) overlays live server/world fields only onto the value it returns. It does not update the underlying `MapControlPlane` state.
- `map_summary` (`Map317CommandService.cs:144-148`) calls `MapControlPlane.ReadSummary()`. `MapControlPlane.cs:138-145` returns server `0` with eight zero counts whenever the in-memory scan state server is `<= 0`, even when the reopened SQLite database contains a completed published run.
- `map_scan_clear` (`Map317CommandService.cs:141-143`) delegates to `MapScanStateMachine.ClearAsync`. `MapScanStateMachine.cs:366-384` rejects unless the in-memory state already has the requested live server and `ServerIdSource == "live"`. A fresh process therefore cannot clear durable rows for the currently connected server until another scan has first established that transient state.
- The new `MapCampaignCanonicalChecks` reopen proof (`tests/LWBridge.Desktop.Checks/MapCampaignCanonicalChecks.cs:162+`) reads `MapStore` directly. It therefore proves storage durability but cannot detect this production command/control restart defect.

This needs a source-backed server-context reconciliation at the canonical control boundary, plus deterministic fresh-service acceptance that seeds a completed run, reconstructs the production-equivalent control lifetime, resolves the current server through an inert provider, and proves summary/search/options/export/clear without requiring a new scan first. The fix should not fabricate a live server when the provider is unavailable.

## Blocker 2 — Auto Scan can move the server after a manual scan acquires ownership

Milestone H requires no overlap with manual scan ownership. `MapAutoScanCommandService` currently has a time-of-check/time-of-use gap around every server transition.

- `MapAutoScanCommandService.TryAdmitDueCycleAsync` checks `execution.IsMapScanActive()` before admission (`src/LWBridge.Desktop/MapAutoScanCommandService.cs:402-426`).
- Once admitted, `RunCycleAsync` iterates targets and calls `JumpServerAsync` before `StartScanAsync` (`:460-474`) without rechecking or holding a shared manual/Auto ownership lease across that pair.
- After one Auto target finishes, the loop repeats the same unowned gap before the next jump. A manual scan can start after initial Auto admission or between targets. Auto can then move the game to another server before its subsequent Map317 start is rejected as already running.
- Final return-to-origin is also an unconditional best-effort jump for a live Auto owner (`:529-543`), so a manual scan that gained ownership during the gap can be moved again during Auto finalization.
- `MapAutoScanCommandServiceChecks` proves manual-busy rejection only at ordinary admission and proves disable does not stop a pre-existing manual scan (`:278-348`). Its fake service helper always sets `startScheduler:false` (`:494-512`) and has no barrier/race case in which manual ownership appears after Auto admission or between target scans.

The manual Map scan lease/control plane and Auto transition need one atomic ownership/admission contract covering the server-jump/start boundary (and return movement), followed by a deterministic barrier test that makes manual ownership win in each gap and proves Auto performs no server movement. A plain second boolean check without an ownership handoff would retain the race.

## Live-boundary risk — production Map317 construction starts the plunder scheduler

Milestone I requires inert validation and explicitly forbids proving the feature by running a production worker against an account. Normal `Map317CommandService` construction is already live-capable and must be treated as such by every campaign runner.

- `Map317CommandService.cs:60-75` creates the real current-client action provider, reconciles plunder restart state, and immediately starts independent Dispatch and Truck worker tasks.
- `MapPlunderWorker.TickMilliseconds` is 100 ms (`src/LWBridge.Map-0.3.17/MapPlunderWorker.cs:14`). Both `RunDispatchOnceAsync` and `RunTruckOnceAsync` drain provider results and call `IsConnectedAsync` even when no durable job is due (`:76-118`, `:121-177`).
- `IsConnectedAsync` calls `IMapActionProvider.GetCurrentServerIdAsync` (`:467-477`); the production provider resolves that through `CurrentClientMapBlockSource.GetMapStatusContextAsync` (`src/LWBridge.Desktop/CurrentClientMap317ActionProvider.cs:42-48`). Persisted due jobs can proceed to the real arm boundaries once connected.
- `MapCampaignCanonicalChecks` deliberately constructs `MapStore` / `MapActionControlPlane` / `MapPlunderWorker` with an inert provider directly. That is safe, but it does not prove production `Map317CommandService` composition with an unavailable execution provider and must not be replaced by a normal-host launch for acceptance.

This is not evidence of a duplicate browser scheduler; only the native worker owns scheduled plunder in the audited tree. It is an acceptance/safety boundary: production-composition tests need an injectable/inert provider seam or an equally isolated owner fixture, and the campaign must not instantiate the normal current-client worker as an offline check. If the always-on 100 ms current-server polling is retained, its source parity and cost should be explicit rather than inferred from the local job state machine.

## G acceptance gaps — workbook and native-dialog matrix is incomplete

The new canonical workbook check is useful but narrower than Milestone G's required matrix.

- `MapCampaignCanonicalChecks.VerifyCityWorkbook` (`:288-343`) verifies a real two-row XLSX, required package parts, ordering, ordinary representative values and mark labels.
- It does not exercise Unicode, missing/null fields, an empty export, a large/row-limit export, a second selected server, or a write failure.
- The check and `map-lane/acceptance.md` explicitly leave native save-dialog cancellation unexecuted. The production cancellation branch is host-owned in `LWBridgeWindow.ExportMap317CityAsync` (`src/LWBridge.Desktop/LWBridgeWindow.cs:2611-2645`).
- `tests/LWBridge.Map-0.3.17.Checks/ExportChecks.cs` is also only a small canonical one-row exporter check. `tests/LWBridge.Desktop.Checks/CityExportWorkbookChecks.cs` exercises a broader matrix against the legacy Desktop store/writer path, which is not sufficient by itself to close the canonical Map317 exporter/host composition requirement.

Before G is accepted, map the required cases to canonical Map317 checks (reusing exact writer assertions where appropriate) and retain an explicit host-level cancellation/write-failure proof. No real user destination is needed; isolated temporary paths are sufficient for all non-dialog cases.

## E acceptance gap — profile isolation is proved, server isolation/DTO edges are not

`MapCampaignCanonicalChecks` uses two profile database paths, but all positive data is on the single constant `ServerId = 317` (`:16`, `:26-38`, `:98-160`). That proves profile isolation, not the required per-profile/**server** ownership. It also seeds one representative row for most kinds, so it cannot establish the Milestone E null/default/order/grouping contract merely from successful rendering/querying.

Add a second server in the same profile database with conflicting record keys/marks/options and prove summary/search/options/export isolation. Add source-pinned edge DTOs for absent/null/default fields and the recovered grouping/order cases that materially affect the eight category contracts. The current UI integration script proves command names/payload routing but cannot substitute for persisted DTO semantics.

## H proof/evidence drift

The production UI/native ownership move is present in source, but the H evidence still describes an earlier state.

- `src/LWBridge.UI-0.3.17/scripts/check-map-integration.mjs:57-76` asserts production native Auto persistence/status/Run Now and explicitly rejects a `MapDataPage` browser `runAutoCycle` / five-second timer.
- `src/LWBridge.Desktop/LWBridgeWindow.cs:269-286` composes the profile-owned native Auto service with Map317 status/jump/start/stop boundaries, and `App.jsx` subscribes to the native Auto event/status rather than running the browser scheduler.
- `map-lane/auto-scan.md:12-17`, `:70-72`, and `:94-100` still describe the browser page as the current scheduler owner and list UI/host integration as remaining coordinator work. Those statements are stale against the current uncommitted source.
- The focused Auto suite invokes all services with `startScheduler:false`, so it validates scheduler core decisions but not the actual production `SchedulerLoopAsync` start/due/disposal path. The explicit `--map-auto-scan-campaign-check` dispatch now exists in `tests/LWBridge.Desktop.Checks/Program.cs:317-328` alongside canonical Map acceptance.

Update H evidence after the blocker above is fixed, and add one deterministic real-loop test through injected clock/delay hooks so the production `startScheduler:true` ownership path is actually proved.

## Bounded items that are correctly fail-closed

- Manual scan resume is still unavailable because no true persisted producer/checkpoint contract exists; `ResumeAvailable` remains false. This is a named limitation rather than fabricated resume success.
- Treasure read-only state refresh is now routed through canonical Map317/current-client inspection, while Treasure claim status/claim remain `GAME_PROVIDER_UNAVAILABLE` in `CurrentClientMap317ActionProvider.cs:90-96`. The UI claim controls remain disabled.
- Ghost plunder preparation remains `GAME_PROVIDER_UNAVAILABLE` (`CurrentClientMap317ActionProvider.cs:117-121`), and current integration checks keep Ghost scheduling fenced.
- No second browser Auto scheduler was found in the current `App.jsx` / `MapDataPage.jsx`; production persistence is native and browser localStorage remains preview/recovery-only.
- `compatibility.md` keeps current-client identity/hash policy, all-eight producer provenance, generic-fallback limits, population-gated Railway/Ghost/Treasure evidence, and state-changing blockers separated. I found no additional J contract overclaim in that file.

## Acceptance consequence

Do not mark E/F/H complete while the fresh-process server-context defect and Auto/manual transition race remain. G still needs the explicit workbook/dialog failure matrix. Milestone I can retain its local durability acceptance, but production-worker validation must stay behind an inert provider boundary. J is suitable as bounded compatibility evidence, with its listed population/action blockers retained.

## Coordinator resolution after the audit — 2026-10-06

This audit remains the preserved independent counterexample record. The recovery
subsequently closed its actionable source-supported gaps:

- **Fresh-service server context:** Map317 now reconciles the current inert provider
  context at the native service boundary. `--map-campaign-canonical-check` proves
  fresh-service server rehydration plus summary/search/options/export/Clear without a
  rescan, while provider-unavailable authorization still fails closed.
- **Auto/manual transition race:** Manual and Auto now share atomic transition
  admission. Auto tracks the admitted scan-run ID and stale Auto cleanup cannot stop
  a replacement Manual run. The focused Auto race test explicitly executes
  Auto-A-completes -> Manual-M-current -> stale Auto retirement and verifies M remains
  active with no stop against M.
- **Production-worker safety:** `ProfileRuntimeOwner.Create` exposes injected Map
  provider/action-provider, scheduler/worker and transport boundaries. The direct
  owner check and packaged desktop proof therefore exercise the production
  composition with inert providers and temporary roots, without constructing a live
  current-client action worker against the owner session.
- **Workbook/dialog matrix:** canonical Map acceptance now covers 205 rows, Unicode,
  large UID, missing/null fields, empty output, second-server isolation and write
  failure. The host-level packaged proof drives the actual City Export control through
  cancel, isolated write failure and successful real XLSX creation.
- **Server/profile/category isolation:** the native owner suite seeds all eight kinds
  for A and a distinct B/server state; the canonical suite covers second-server and
  profile isolation. The package drives every one of the eight rendered tabs, clears
  B only, and returns to A with A data intact.
- **H integration:** the native scheduler is the production owner; UI/browser Auto
  timers are absent. Run-token ownership, atomic movement/start admission, scheduler
  deadline authority, persisted revision/error truth, UI save ordering and profile
  retirement all have focused checks. The accepted package then exercises the real
  Auto controls under A/B/A.
- **Jobs/events/restart/shutdown:** the native owner/canonical checks cover inert
  Dispatch/Truck durable admission, event/state transitions, restart, retry,
  cancellation and clear. The package records document/request/event cleanup on final
  shutdown.

The bounded fail-closed items identified by this audit remain unchanged: manual scan
resume has no recovered producer/checkpoint contract; Treasure claim/status and Ghost
plunder preparation remain unavailable protected-provider actions; positive current
Railway/Ghost/Treasure populations remain live-state/population gated. None is
promoted to success by this recovery.
