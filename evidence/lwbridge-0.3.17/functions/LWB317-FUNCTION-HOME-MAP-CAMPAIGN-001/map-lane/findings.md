# Map lane readiness audit — Milestones A / E / F / G / H / I

Campaign: `LWB317-FUNCTION-HOME-MAP-CAMPAIGN-001`  
Baseline supplied by the campaign: `4dc1a67645dea2ff7419ecd03bc61f8b0b1a76f9` on `research/offline-controller`  
Audit mode: source/evidence recovery only. No live Last War, browser/process control, update/install action, or Git operation was performed.

## Result

The normal production Map owner is already the `LWBridge.Map317` stack composed by `LWBridge.Desktop.Map317CommandService`, backed by the profile-scoped Map317 SQLite database and current-client scan/action providers. The older Desktop `MapDataStore` is not the normal production Map owner and should not be used as the target for E-I implementation work.

Milestones E/F/G have substantial production implementation. The largest composition gap is the canonical UI bridge: `src/LWBridge.UI-0.3.17/src/mapBackend.js:createMapApi()` exposes the scan/query/navigation/mark/export subset but omits the already-implemented Treasure, Dispatch share, scheduled-plunder, truck-plunder, job-list/action methods and related change-event subscriptions. Consequently `MapDataPage` correctly keeps those controls runtime-fenced.

Milestone H is not ready as a native control plane. The recovered 0.3.17 frontend contract itself used `localStorage`, and current UI still preserves that contract, but the campaign explicitly raises ownership to a profile-owned native scheduler. Today the browser page owns admission, the 5 s timer, server iteration, 2 s scan polling, 45-minute timeout, return-to-origin, and next deadline. A window close/reload therefore destroys the active worker and there is no native disable/stop/restart/error owner.

Milestone I has two hard current-client provider blockers: Treasure claim/status execution and Ghost plunder preparation deliberately fail closed in `CurrentClientMap317ActionProvider`. Treasure state inspection/refresh, Dispatch share, Dispatch/Truck plunder worker execution, local job persistence, and the command surface are implemented. Canonical UI composition must be restored before any state-changing live validation is considered.

## Authority and recovered contracts

- `docs/work-items/LWB317-FUNCTION-HOME-MAP-CAMPAIGN-001.md` defines E-I acceptance and requires source-located readiness before implementation/live work.
- `evidence/lwbridge-0.3.17/map/frontend-host-contract.json:210-310` pins the original Treasure/share/plunder host payloads. In particular, `map_dispatch_plunder_schedule` receives rows whose `plunderAt`, `maxRandomDelaySeconds`, and `randomDelaySeconds` have already been computed by the frontend adapter.
- `evidence/lwbridge-0.3.17/ui/LWB317-UI-SHELL-CROSSSERVER-001/recovery/original-api-byte204900-207400.js:1` contains the recovered API adapter. Its Dispatch scheduler chooses an integer random delay from zero through the bounded maximum, caps it by task expiry and JS safe-integer range, advances `plunderAt`, and sends both configured and chosen delay seconds in each row.
- `evidence/lwbridge-0.3.17/map/frontend-host-contract.json:496-536` pins Auto Scan defaults/normalization and frontend timing: profile-keyed `lwbridge.mapAutoScan.${profileId}`, 60-minute default, 20-1440 minute range, up to 20 servers, default Truck/Railway/Dispatch/Ghost/Treasure, Fast mode, return-to-origin, 5 s scheduler cadence, 2 s completion poll, 2,700,000 ms completion timeout.
- `evidence/lwbridge-0.3.17/map/action-handler-summary.json:1501-1555`, `:2320-2370`, and `:3112-3175` retain original native handler locators for Treasure claim/state refresh and Dispatch schedule. These establish original command existence and strings; they do not establish current-client runtime behavior.

## Canonical production composition

### Normal Desktop owner

- `src/LWBridge.Desktop/LWBridgeWindow.cs:259-265` constructs `Map317CommandService` with `%LOCALAPPDATA%/LWBridgeRebuild/profiles/<profile>/map-data/map-data.db` in the normal non-isolated UI path.
- `src/LWBridge.Desktop/LWBridgeWindow.cs:287-310` places that service in the normal async-command composite. `LWBridgeBackend.InvokeAsync` dispatches to the composed async service before its legacy synchronous fallback (`src/LWBridge.Desktop/LWBridgeBackend.cs:197-207`).
- `src/LWBridge.Desktop/LWBridgeWindow.cs:125-150` shows the older `MapDataStore` is `null` in the normal UI path; it is created only by isolated/replay/legacy proof paths. E-I production changes should therefore target Map317, not extend the old MapDataStore plane.
- `src/LWBridge.Desktop/LWBridgeWindow.cs:332-337` subscribes Map317 scan, mark, Dispatch-plunder, and Truck-plunder change signals. The window emits `bridge://map-scan-status`, `bridge://player-mark-changed`, `bridge://dispatch-plunder-changed`, and `bridge://truck-plunder-changed` (`LWBridgeWindow.cs:2702-2760`).

### Canonical UI bridge gap

- `src/LWBridge.UI-0.3.17/src/mapBackend.js:1-17` defines only the basic Map command names.
- `src/LWBridge.UI-0.3.17/src/mapBackend.js:234-270` exposes status/summary/options, start/stop/clear/search, server jump/history, coordinate jump, player mark, City export, and scan/status listeners. It does **not** expose Treasure refresh/claim/status, Dispatch share, plunder job list/schedule/cancel/clear/retry, Truck equivalents, or mark/plunder event subscriptions.
- `src/LWBridge.UI-0.3.17/src/MapDataPage.jsx:869-872` probes these missing methods using the recovered provider-method groups. `MapDataPage.jsx:1157` fences Dispatch/Ghost scheduling when absent, while Treasure single/bulk claim controls are explicitly disabled at `:178` and `:1160`.
- Native support already exists in `src/LWBridge.Desktop/Map317CommandService.cs:146-199`; this is a composition defect, not a missing host command family.

Smallest safe UI bridge correction after E/F/G data contracts are frozen: add the recovered command/method/event surface to `mapBackend.js`; preserve the recovered per-row Dispatch random-delay transformation in that adapter; subscribe mark/Dispatch/Truck events and refresh only the affected local view/job snapshot. Do not place randomization inside the native scheduler because the recovered host receives already-transformed rows.

## E — canonical Map data/control readiness

`src/LWBridge.Map-0.3.17/MapControlPlane.cs` + `MapStore.Query.cs` are the canonical data plane. They own active-run staging versus published rows, summary/options/search, marks, server history, and durable SQLite state. `Map317CommandService.cs:112-145` routes the public commands to this plane.

The UI query builder already carries the recovered filter family into `map_search`: resource/monster name, Treasure type/supplies, alliance and no-alliance sentinel, marked-only, quality/special/reindeer/item, completion, plunderable-only, foreign-radar inclusion, lucky-first/viewer context, Dispatch level bounds, sorting and page/page-size. `Map317CommandService.cs:391-419` converts the normalized Desktop query contract to `Map317.MapQuery`.

### Eight-category producer matrix

| Recovered kind | Current production producer path | Deterministic coverage | Readiness / blocker |
| --- | --- | --- | --- |
| `city` | `CurrentClientMapBlockSource` current-view fallback plus fast full-world batch | `CurrentClientMapBlockSourceChecks`: current-view, LOD0, targeted fail-closed, fast full-map/band/all-eight | **IMPLEMENTED_NOT_VALIDATED for this campaign baseline.** Historical V22 category proof had 6,850 rows. |
| `resource` | current-view fallback plus fast full-world batch with resource-detail collection | resource enumerate/filter/zero-row, fast full-map/all-eight; historical dedicated resource proof | Historical evidence classifies complete resource scan/query **LIVE_PROVEN**, but campaign requires current-baseline reconciliation. |
| `monster` | fast batch world producer; zombie-boss source folds into recovered Monster semantics where admitted | fast full-map/coarse/boss/protection/retry tests | **IMPLEMENTED_NOT_VALIDATED.** Historical V22 category proof had 9,568 rows. If fast-batch admission is unavailable there is no equivalent generic fallback. |
| `truck` | fast batch/train-list source | fast Truck full-map and all-eight tests | **IMPLEMENTED_NOT_VALIDATED.** Historical V22 category proof had 61 rows. Generic fallback absent. |
| `railway` | fast batch/train-list source | fast Railway full-map and all-eight tests | **IMPLEMENTED_NOT_VALIDATED.** Historical seven-kind run completed but returned zero Railway rows, so positive current producer output is not established by that artifact. Generic fallback absent. |
| `dispatch` | fast AOI batch source | fast Dispatch full-map and all-eight tests | **IMPLEMENTED_NOT_VALIDATED.** Historical V22 category proof had 18 rows. Generic fallback absent. |
| `ghost` | fast AOI batch source | fast Ghost full-map and all-eight tests | Scan producer is **IMPLEMENTED_NOT_VALIDATED**; historical seven-kind run returned zero Ghost rows. Scheduled Ghost plunder is separately **BLOCKED** by missing preparation provider. |
| `treasure` | fast AOI batch source | fast Treasure full-map and all-eight tests | Scan/state-inspection path is **IMPLEMENTED_NOT_VALIDATED**; historical seven-kind run returned zero Treasure rows. Claim/status execution is **BLOCKED**. |

`src/LWBridge.Desktop/CurrentClientMapBlockSource.FastCity.cs:717-720` admits the fast multi-kind producer only for the expected current world shape (`worldId == 0`, `1000x1000`) and selected supported source kinds. The generic `CaptureAsync` fallback in `CurrentClientMapBlockSource.cs` handles only City or Resource one-kind acquisition. Therefore Monster/Truck/Railway/Dispatch/Ghost/Treasure need a deliberate fail-closed error when fast-batch admission or current-client source compatibility is unavailable; there is no safe fallback producer to synthesize.

Historical evidence must be kept bounded. `evidence/lwbridge-0.3.17/map/LWB317-MAP-CATEGORIES-V22-001/live-categories-proof.json:1` completed a seven-kind Fast scan with positive City/Monster/Truck/Dispatch rows and zero Railway/Ghost/Treasure rows. `LWB317-LIVE-MAP-V22-001/live-result.json:17-28` classifies resource acquisition/query/clear as LIVE_PROVEN but Treasure claim status and Ghost preparation as BLOCKED. These artifacts predate this campaign baseline and are readiness evidence, not Milestone E acceptance by themselves.

## F — manual scan lifecycle / durability

Implemented production path:

1. UI `MapDataPage` invokes the canonical `mapApi.start/stop/clear` methods; `mapBackend.js:246-250` routes them to `map_scan_start`, `map_scan_stop`, and `map_scan_clear`.
2. `Map317CommandService.cs:291-329` obtains a process lease, reconciles interrupted runs, normalizes selected types/mode/resume, starts `MapControlPlane`, then activates `CurrentClientMap317ScanProvider` for the accepted run.
3. `MapScanStateMachine.cs:120-190` validates live/world/owned-server context, normalizes the recovered eight kinds, assigns Normal concurrency 8 or Fast concurrency 20, validates map dimensions/block count, and publishes the scanning state.
4. `CurrentClientMap317ScanProvider` drives `MapScanEngine` and stages rows through the Map317 control plane; completion publishes the accepted staged run atomically. Stop cancels local scan ownership first and treats provider stop as best effort (`MapScanStateMachine.cs:315-359`).
5. `Map317CommandService.cs:331-351` overlays live current-server/world metadata onto durable scan state when available, while preserving durable state if the current source is temporarily unavailable.

Restart/resume is not actually implemented. `MapScanStateMachine.cs:140` only forwards resume when the previous in-memory state reports `ResumeAvailable`; every start/progress/complete/fail/stop path currently leaves `ResumeAvailable=false` (`:186`, `:243`, `:284`, `:306`, `:351`). `ScanStateChecks.ResumeRemainsUnavailableWithoutTrueProducerAsync` explicitly pins this fail-closed behavior. The process lease/reconciliation safely prevents stale active ownership, but Milestone F must not claim resumable scans until a real producer/checkpoint contract exists.

Historical `LWB317-LIVE-MAP-V22-002/live-result.json:5-20` does prove active stop/cancel, clear-after-stop, second start in the same session, and cleanup in that historical bounded run; it still classified restart/resume as IMPLEMENTED_NOT_VALIDATED. That evidence supports the stop/clear design but does not close current campaign restart/resume acceptance.

## G — query/filter/sort/page, marks, export, navigation

### Query/filter/page

`MapStore.Query.cs` is SQLite-backed and supports active-run versus published queries, page/page-size, recovered expression sorts and deterministic tie breakers. Deterministic `tests/LWBridge.Map-0.3.17.Checks/QueryChecks.cs:5-50` covers marked-only, alliance/no-alliance, keyword, City health/shield, Monster distance, active staging isolation, options and summary. This test is useful but does not exhaust every category/filter combination, so Milestone G should add focused canonical Map317 cases for the UI payload matrix rather than rely on the old Desktop MapDataStore parity suite.

`tests/LWBridge.Desktop.Checks/MapSearchSortParityChecks.cs` targets the older `MapDataStore` and deliberately labels some sorts unrecovered that canonical Map317 now implements. It is historical compatibility evidence and must not define current production readiness.

### Marks

`Map317CommandService.cs:365-389` persists City player marks through Map317. The window already emits `bridge://player-mark-changed`; canonical `mapBackend.js` currently does not subscribe to it. Add the event to the bridge API so other views/windows do not require incidental refetches to converge.

### City export

`Map317CommandService` intentionally rejects direct `map_city_export` with `NATIVE_DIALOG_REQUIRED`; `LWBridgeWindow` owns the native save-dialog composition and Map317 export. `tests/LWBridge.Map-0.3.17.Checks/ExportChecks.cs:7-39` deterministically verifies XLSX creation, UID-as-text content, mark label and recovered filename format. This ownership should remain in Desktop host composition.

### Coordinate/server/history

Coordinate jump and server jump route through `CurrentClientMap317ActionProvider` to source-backed current-client navigation. Historical evidence includes a camera-only coordinate proof (`evidence/lwbridge-0.3.17/map/LWB317-MAP-ACTIONS-UI-001-live-coordinate-proof.json:1`) and an owned-session server-jump roundtrip (`LWB317-MAP-SERVER-JUMP-001/live-server-jump-proof.json:1`). They are useful provenance, but no live action should be repeated during this source-recovery phase.

## H — Auto Scan control-plane gap

Recovered original UI behavior is source-located and current UI reproduces it:

- `mapAutoConfig.js:64-79` stores profile-keyed config in browser `localStorage`.
- `MapDataPage.jsx:798-846` owns the active cycle: determine configured/current servers, jump, start scan, poll status every 2 s, stop after 45 minutes, advance deadline, optionally return to original server; a page timer tests due state every 5 s.
- `frontend-host-contract.json:496-536` proves these defaults/timings are recovered behavior.
- Historical `LWB317-MAP-AUTO-SCAN-001/normal-ui-auto-scan.json:1-94` proves one current-server-only Fast Dispatch UI-driven cycle from the older implementation, then disabled/cleared the config.

Campaign H explicitly requires **profile-owned persistence/admission/scheduling** and disable/stop/restart/error ownership. No current native service owns this. `rg` finds no Map Auto Scan config/control service under `src/LWBridge.Desktop` or `src/LWBridge.Map-0.3.17`; ownership is only `App.jsx`/`mapAutoConfig.js`/`MapDataPage.jsx`.

Proposed owner after E/F freeze:

- Add a profile-scoped Desktop Auto Scan command/config service alongside the existing profile config services, or add a narrowly bounded scheduler component owned by `Map317CommandService` if its lifetime remains exactly one profile service lifetime.
- Persist the recovered config fields natively: enabled, intervalMinutes, serverIds, selectedTypes, scanMode, returnToOriginalServer, nextRunAt. Preserve the recovered normalization/defaults.
- Make the native owner the sole due/admission state machine. Serialize with manual scan through the existing Map317 scan lease/control plane; never start a second scan concurrently.
- Persist enough cycle state to recover safely after Desktop restart. On disable/stop, cancel only the Auto Scan-owned operation; do not stop an unrelated manual scan. On error, persist/emit a bounded error and compute the next deadline according to the accepted recovered policy.
- UI becomes config/status plus Run Now intent; remove the page-owned 5 s worker once native acceptance exists. Keep presentation state local.

## I — Treasure / Scheduled Plunder readiness

### Treasure

Host/control plane is largely present:

- `Map317CommandService.cs:146-161` handles state refresh, refresh-all, claim status and claim.
- `MapActionControlPlane` overlays inspected state into the Map store and owns claim candidate selection/orchestration.
- `CurrentClientMap317ActionProvider.cs:56-88` implements source-backed Treasure state inspection in batches of 100 with identity consistency.

Hard blockers:

- `CurrentClientMap317ActionProvider.cs:90-96`: claim-status and claim providers explicitly return `GAME_CONNECTION_UNAVAILABLE`/provider unavailable. Do not connect the UI claim buttons to these commands and then call the feature complete.
- `tests/LWBridge.Desktop.Checks/TreasureClaimContractChecks.cs:153-207` pins original frontend polling and row eligibility, but its `ProductionClaimRouteRemainsBlocked` assertion only inspects the legacy `ManualMapScanCommandService`; canonical `Map317CommandService` now has the route. Treat the test as contract evidence, not proof that the canonical route is absent.
- Current `MapDataPage` no longer performs the recovered post-search Treasure state refresh/overlay or claim-status polling composition; claim controls are disabled. Restoring this UI flow should wait for a real current-client claim/status provider or keep claim actions explicitly fenced while state refresh can be enabled separately.

### Dispatch share / Dispatch and Truck scheduled plunder

Implemented native plane:

- `Map317CommandService.cs:162-199` exposes Dispatch share, job snapshot, Dispatch schedule/cancel/clear/list/retry, and Truck schedule/cancel/clear/list/retry.
- `MapActionControlPlane` persists scheduled jobs and emits Dispatch/Truck change events.
- `MapPlunderWorker` owns asynchronous execution/results. `tests/LWBridge.Map-0.3.17.Checks/ActionChecks.cs:73-121` covers share aggregation and local Dispatch/Ghost/Truck schedule persistence with fake providers. `PlunderWorkerChecks` covers arming, result ownership, timeout, reconnect/restart, pruning and truck merge behavior with deterministic providers.
- `CurrentClientMap317ActionProvider.cs:98-114`, `:123-273` contains source-backed Dispatch share, server-day, Dispatch arm/result drain, Truck arm/result drain/cleanup.

Blocker:

- `CurrentClientMap317ActionProvider.cs:117-121` deliberately rejects Ghost plunder preparation. A mixed Dispatch/Ghost schedule must not silently pretend Ghost is executable. Keep Ghost scheduling fenced or fail closed per-row until that producer exists.

Canonical UI bridge work is still required. `MapDataPage.jsx:875-959` already contains recovered callback composition, but `mapBackend.js` does not supply the required methods. When adding them, also expose the Desktop Dispatch/Truck change events so the Scheduled Plunder panel refreshes from host-owned job changes rather than UI guesses.

## Deterministic checks worth retaining/expanding

- `tests/LWBridge.Map-0.3.17.Checks/ControlPlaneChecks.cs`: durable accepted run, persist-before-event progress, active staging isolation, publish completion, history normalization.
- `tests/LWBridge.Map-0.3.17.Checks/ScanStateChecks.cs`: modes/concurrency, stop/clear/state transitions, fail-closed resume.
- `tests/LWBridge.Map-0.3.17.Checks/QueryChecks.cs`: representative query semantics; extend to the full canonical UI matrix.
- `tests/LWBridge.Map-0.3.17.Checks/ExportChecks.cs`: Map317 XLSX output.
- `tests/LWBridge.Map-0.3.17.Checks/ActionChecks.cs` and `PlunderWorkerChecks.cs`: deterministic action/job orchestration with fake providers.
- `tests/LWBridge.Desktop.Checks/CurrentClientMapBlockSourceChecks.cs:21-74`: deterministic injected-source coverage for City/Resource/Monster/Truck/Railway/Dispatch/Ghost/Treasure/all-eight plus current-client navigation/session behavior.
- `tests/LWBridge.Desktop.Checks/Map317RestartChecks.cs`: process lease reuse only; it is **not** scan resume/durable-cycle proof.

Do not use the default Desktop checks wholesale as a substitute for the canonical Map317 suite. `tests/LWBridge.Desktop.Checks/Program.cs:457-486` mixes newer Map checks with legacy MapDataStore parity checks and the executable also contains many opt-in live modes.

## LIVE helpers that must not run in this source-only phase

Do not invoke any `tests/LWBridge.Desktop.Checks` live Map helper, including the families/flags for:

- `--live-map-v22-proof`, resource runtime/completeness/stop-continuity, `--live-map-categories-v22`, `--live-current-client-all-eight-modes`, mixed-all-eight, current-client full City/Resource/Monster/ZombieBoss/Truck/Railway/Dispatch/Ghost/Treasure scans;
- `--live-auto-zombie-cycle-proof`, multi-server Auto Scan cycles, native-failure continuation, or bridge-loss-during-scan;
- live server jump, coordinate jump, native transition, current-client block/current-city sample/manual-stop, train-list population/no-jump, bulk-AOI/full-map/runtime diagnostic helpers;
- live Treasure state refresh, and any helper that can claim Treasure, share Dispatch, schedule/execute plunder, move/attack, spend resources, or otherwise mutate live game state.

Historical JSON/PNG evidence may be read. It must not be regenerated during Milestone A helper recovery.

## Smallest safe implementation sequence

1. **Freeze E canonical ownership and query DTOs.** Treat Map317 as production authority; add/extend deterministic canonical tests for all eight query/filter/sort/page envelopes and fail-closed unsupported source shapes. Do not extend legacy `MapDataStore` as the production path.
2. **Close F lifecycle/durability semantics.** Keep current process lease/staging/stop/clear behavior. Decide the recovered/campaign expectation for restart resume explicitly; either implement a real persisted producer checkpoint or record resume as intentionally unavailable. Prove fresh-process published reads and interrupted-run reconciliation with isolated databases.
3. **Close G composition.** Preserve Desktop ownership of native XLSX save dialog; add canonical event subscription for marks and deterministic UI/API composition checks for filters/pages/marks/export payloads/navigation. Use inert fakes for provider actions.
4. **Restore safe I bridge methods first.** Add Treasure state refresh (read/inspection), job list, and change-event subscriptions to `mapBackend.js`; add Dispatch/Truck schedule/cancel/clear/retry/share methods with exact recovered payload adaptation. Keep Treasure claim/status and Ghost schedule fenced while their providers are unavailable.
5. **Implement H native Auto Scan owner.** Move persistence/admission/timer/server-cycle/error/restart ownership out of the page, reusing Map317 scan/server-jump primitives and existing process ownership. UI becomes config/status/Run Now only.
6. **Recover the two provider blockers separately.** Implement Treasure claim/status only from current-client source-backed behavior with deterministic transport/result checks. Implement Ghost preparation from a source-backed current-client contract. Until then, return explicit unavailable errors and keep UI actions disabled.
7. **Only after isolated deterministic/adversarial acceptance**, schedule separately authorized bounded live validation. Positive producer validation is especially needed for Railway/Ghost/Treasure on a game state where rows exist; state-changing Treasure/Share/Plunder validation requires its own explicit safe plan and must not be bundled into scan proof.

## Blockers / limits

- **Canonical bridge blocker:** current `createMapApi()` omits already-implemented Treasure/plunder/share/job methods and mark/plunder event subscriptions.
- **Treasure action blocker:** current-client claim/status providers are explicitly unavailable.
- **Ghost action blocker:** current-client Ghost plunder preparation is explicitly unavailable.
- **Auto Scan ownership blocker:** the campaign-required native profile scheduler/control plane does not exist; browser/localStorage owns it today.
- **Resume blocker:** manual scan restart/resume has no true producer/checkpoint and `ResumeAvailable` is always false.
- **Producer fallback limit:** only City/Resource have the generic current-view capture fallback; the other recovered kinds depend on fast-batch/train-list current-client paths and must fail closed when those are unavailable.
- **Evidence limit:** older live category runs are valuable but not equivalent to current campaign-baseline acceptance; zero-row Railway/Ghost/Treasure observations do not prove positive record production.
- **Legacy-test limit:** old Desktop `MapDataStore` parity checks and legacy `ManualMapScanCommandService` assertions do not define normal production Map317 behavior.

No production file was edited by this audit.
