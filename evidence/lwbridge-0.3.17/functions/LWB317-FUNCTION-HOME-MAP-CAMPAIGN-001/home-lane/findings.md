# Home/native readiness findings — Milestones A/B/C/D

Scope: read-only source recovery and composition audit for `LWB317-FUNCTION-HOME-MAP-CAMPAIGN-001`. Production was not edited. No live Last War, browser/native UI control, updater, owner installation change, or Git operation was performed. The only writable artifact for this worker is this file.

The recovered 0.3.17 frontend authority used here is `evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js`, SHA-256 `44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6`. The original executable identity recorded by the campaign is SHA-256 `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.

Current-source note: `HomePage.jsx` and `App.jsx` changed in the shared worktree while this audit was running. The current findings below use the later re-read in which canonical Home lifecycle callbacks and startup reconcile are already present. The earlier campaign baseline (`lifecycleProviderAvailable=false` / no lifecycle callbacks) is historical context, not the live-current conclusion.

## Result

Home/native is substantially implemented below the canonical UI. The production host already composes one `OverviewLifecycleService`, the shared authenticated control-pipe host, profile/config stores, proxy/recovery status, request cancellation/teardown, and recovery ownership. The current frontend also now invokes the recovered start/stop/update-and-restart/reconcile commands.

Two concrete native blockers remain in A/B/C/D readiness:

1. **Native game-root selection persists a root without rebinding the already-constructed lifecycle.** `LWBridgeWindow.SelectGameRootAsync` calls `LWBridgeBackend.SaveNativeGameRootSelection`, which calls `GameInstallationService.SaveNativeSelection`; that path only persists `GameRoot`. The safe stopped-boundary `OverviewLifecycleService.RebindGameRoot` is only reached by the separate `LWBridgeBackend.SaveGameRoot` path. A normal host started with no strict-valid root therefore keeps lifecycle `gameRoot=null` after a native selection; a host started on root A can keep lifecycle root A after UI selection of B.
2. **Even `SaveGameRoot -> RebindGameRoot` does not retarget the production shared control-pipe listener from root A to B.** `LWBridgeWindow` starts `LWBridgeControlPipeHostState` against the original `<root>\Game\LastWar.exe`; `StartRpcTransport` is idempotent only for the same build/client identity and throws for a different client path. The existing lifecycle rebind test uses `OverviewLifecycleTestHooks`, while `EnsureControlPipeHostStarted` explicitly skips transport startup whenever test hooks are present. Thus the green A→B rebind test does not exercise the production listener constraint.
There is also a bounded profile-composition limit: production bootstraps one immutable `LocalConfigStore.ProfileId`; `ProfileRegistryCommandService` exists, but canonical bootstrap/App does not use registry selection to reconstruct or retarget the backend/lifecycle/profile stores. Multi-profile replacement therefore remains unproven/unreachable in normal product composition. This matters to the campaign's profile-replacement acceptance cases, but should not be solved by importing excluded original auth/licensing UI.

## Recovered original Home command/caller contract

State: **EXACT_BYTES** for the recovered asset listed above.

| Operation | Exact recovered locator | Recovered request / owner behavior |
| --- | --- | --- |
| Profile instance status | asset byte `202578` | `profile_instance_status({profileId})` |
| Start | byte `202643` | `profile_instance_start({profileId, closeUnmanaged:true})` |
| Update and restart | byte `202724` | global `profile_instances_update_and_restart` with no payload |
| Startup reconcile | byte `202788` | global `profile_instances_reconcile({autoLaunchAll})` |
| Stop | byte `202863` | `profile_instance_stop({profileId, instanceId})` |
| Game recovery status | byte `203215` | `game_recovery_status({profileId})` |
| Game-root status | byte `205405` | global `game_root_status` |
| Game-root picker | byte `205448` | global `game_root_select` |
| Auto Launch local key | byte `246758` | `lwbridge.autoLaunchGame`; only literal stored `"false"` disables it |
| Manual start caller | byte `365794`, `async function Nt()` | sets shared Home proxy/action busy, clears action error, starts selected profile, refreshes proxy, releases busy |
| Manual stop caller | byte `365962`, `async function Pt()` | sets proxy busy, reads owned instance status, stops exact `instanceId`, refreshes proxy, releases busy |
| Repair/update caller | byte `366209`, `async function Ft()` | sets proxy busy, invokes update/restart, refreshes proxy, surfaces first returned error, releases busy |
| Home busy bindings | bytes `373350` / `373362` | caller passes `proxyBusy:p` and `gameLaunchBusy:n.gameLaunchBusy` separately |

Historical recovery audit `docs/reviews/2026-09-27-r8-116-auth-launch-frontend.md` records the source-backed startup trigger: after authorization/grace the original performs a best-effort entitlement read and then `profile_instances_reconcile({autoLaunchAll:<Auto Launch setting>})`. The clone intentionally does not add excluded original login/licensing UI, so current startup placement is an adaptation; the command/payload and Auto Launch setting are still source-backed.

The recovered Home renderer keeps two busy concepts separate. Manual lifecycle work uses `proxyBusy`; auth/startup reconciliation contributes to `gameLaunchBusy`. Start is admitted only after root resolution/validity, when the game is stopped, recovery is inactive, and both busy values are false. Stop is admitted while running/recovering and both busy values are false. Repair/update replaces the ordinary close action only for running + repair-required + not-recovering.

## Current canonical UI and bridge

### Home state and callbacks

State: **IMPLEMENTED_NOT_VALIDATED** in the shared live current source.

- `src/LWBridge.UI-0.3.17/src/HomePage.jsx:67-96` accepts the three lifecycle callbacks and derives current root/running/recovery/busy state. `:91-94` now treats the provider as available only when production mode is true and all three callbacks exist. `:125-128` binds Start / Stop / Update-and-Launch to those callbacks.
- `src/LWBridge.UI-0.3.17/src/App.jsx:619-692` now implements current start/stop/update-and-restart callbacks with recovered command names/payloads and a 360-second lifecycle timeout.
- `App.jsx:389-413` invokes startup `profile_instances_reconcile({autoLaunchAll:autoLaunchGame})` once per App lifetime and refreshes status after completion.
- `App.jsx:415-427` owns `bridge://game-recovery` plus the initial `game_recovery_status({profileId})` read.
- `App.jsx:429-434` reads `game_root_status` when the selected profile changes.
- `App.jsx:457-485` owns the 5-second status/proxy polling loop with an in-flight guard and selected-profile retirement check.
- `App.jsx:552-586` preserves Auto Launch's immediate local write/state update, serializes native `local_config_set({autoLaunchGame})`, and rolls back to the last native-confirmed value when the latest save fails.
- `App.jsx:588-592` drives Automatic Reconnection through the profile-keyed config draft adapter and native `set_automation` flush.
- `App.jsx:594-611` invokes `game_root_select`, preserves cancel, reports invalid selection separately, and refreshes `game_root_status` after valid selection.

### Current busy ownership

State: **IMPLEMENTED_NOT_VALIDATED**, source-aligned in the final live-current re-read.

The shared worktree changed again during this audit and the coordinator's later edit now restores the recovered split:

- `App.jsx:158-159` owns separate `proxyBusy` and `gameLaunchBusy` state.
- `App.jsx:391-425` adds the selected profile to the lifecycle in-flight set and sets `gameLaunchBusy=true` for startup reconcile, then clears both ownership markers when it settles/tears down.
- `App.jsx:632-695+` uses `proxyBusy` for manual start/stop/update-and-restart while retaining the per-profile in-flight dispatch guard.
- `App.jsx:781-795`, especially `:790-791`, passes both busy values separately into Home.

This closes the earlier source drift and startup/manual overlap finding. It still needs the coordinator's ordinary frontend/native validation before acceptance, but it is no longer a blocker from this source audit.

### WebView request lifetime

State: **IMPLEMENTED_NOT_VALIDATED** for this campaign; deterministic source/tests are strong.

- `src/LWBridge.UI-0.3.17/src/backendBridge.js` emits `{kind:"invoke", sessionId, id, command, payload}`; timeout/abort emits `{kind:"cancel", sessionId, id}`; listen/unlisten are session-owned.
- `src/LWBridge.Desktop/LWBridgeWindow.cs:2411-2537` validates current document/session, routes listen/unlisten/cancel/invoke, uses the per-document `NativeRequestExecutor`, suppresses stale document completion, and returns structured native errors.
- `LWBridgeWindow.cs:1553-1561` closes the prior document session before creating a new one on same-origin navigation.
- `LWBridgeWindow.cs:2893-2917` owns subscriptions + request executor per document; closing a document cancels native requests.
- `tests/LWBridge.Desktop.Checks/Program.cs:3212-3365` contains deterministic duplicate-ID, explicit-cancel, late-completion, teardown and cancel/completion race coverage.

This request layer should be reused as-is for Home. No parallel Home-specific cancellation mechanism is needed.

## Native game-root status and selection

### Already implemented public/native behavior

State: **IMPLEMENTED_NOT_VALIDATED** for current campaign; existing tests encode the intended reconstructed contract.

`src/LWBridge.Desktop/GameInstallationService.cs` intentionally has two validity levels:

- Strict launch admission: `GetStatus` / `Validate`, `:65-147`, requires `LastWarLauncher.exe`, `Game\LastWar.exe`, `Game\LastWar_Data\Plugins\x86_64\xlua.dll`, AMD64 images and readable game image.
- Native/public root discovery/selection: `SaveNativeSelection` `:149-175`, `GetNativeStatus` `:177+`, and `IsNativeRootValid` `:302-320` require only `Game\LastWar.exe` plus the `Plugins\x86_64` directory. `GetNativeStatus` preserves candidate source ordering and normalization.

`tests/LWBridge.Desktop.Checks/GameRootSelectChecks.cs:65-84` deliberately proves a native-minimal selected root can return `valid=true` while strict `Validate` returns `GAME_ROOT_REQUIRED_FILES_MISSING`. That separation must not be accidentally erased by routing the folder picker straight to strict `SaveGameRoot` unless source/contract authority is updated.

### Missing lifecycle rebind on actual picker route

State: **BLOCKED**.

Actual canonical chain:

`Home App game_root_select` → `backendBridge.invoke` → `LWBridgeWindow.OnWebMessageReceived` special case (`LWBridgeWindow.cs:2477-2481`) → `SelectGameRootAsync` (`:2541-2582`) → `backend.SaveNativeGameRootSelection(selectedPath)` (`:2578-2581`) → `GameInstallationService.SaveNativeSelection` (`GameInstallationService.cs:152-175`) → config persistence only.

Existing but bypassed safe lifecycle chain:

`LWBridgeBackend.SaveGameRoot` (`LWBridgeBackend.cs:138-160`) → strict `installation.Validate` → `OverviewLifecycleService.RebindGameRoot` (`OverviewLifecycleService.cs:163-217`) → guarded persistence at a stopped/recovery-safe boundary.

The normal host constructs lifecycle once from strict `GameInstallationService.GetStatus` in `LWBridgeWindow.cs:238-256`. Therefore:

- no strict-valid root at process start + later native selection: lifecycle remains bound to `null` even though config/public native status can say the selection is valid;
- strict-valid root A at process start + later native selection B: lifecycle can remain bound to A while config/public status says B.

The current Home `rootValid` uses public/native `game_root_status`, and current provider availability only checks production + callback presence. A native-minimal root can therefore enable Start even though the lifecycle has no strict-admitted root. The campaign explicitly requires provider prerequisites rather than merely enabling a button, so launch-admission availability still needs an honest current-host signal or equivalent safe composition rule. Do not silently change the recovered four-field public root status shape to solve this.

### Missing shared control-pipe retarget

State: **BLOCKED** and independent from the picker bypass.

Normal production composition in `LWBridgeWindow.cs:238-256` creates one `LWBridgeControlPipeHostState`. When strict root is valid, it immediately starts the RPC transport against `BuildExpectedGameExecutablePath(liveGameRoot.Path)` before constructing lifecycle.

`OverviewLifecycleService.StartAsync` calls `EnsureControlPipeHostStarted(selectedRoot)` (`OverviewLifecycleService.cs:579+`, call at `:612`). `EnsureControlPipeHostStarted` `:882-913` derives the selected root's executable path and calls `bridgeHostState.StartRpcTransport`.

`LWBridgeControlPipeHostState.StartRpcTransport` (`src/LWBridge.Desktop/LWBridgeControlPipeHostState.cs:113-167`) returns the existing loop only when build id and canonical expected client path both match. A different path throws `InvalidOperationException("The shared bridge RPC transport is already bound to a different build or client image.")` at `:145-146`. `StopRpcTransportAsync` already exists at `:242-262` and clears the expected identity/loop ownership.

Therefore lifecycle `RebindGameRoot(A→B)` by itself is not enough: first subsequent production `StartAsync` for B tries to ensure B while the host is still bound to A and is translated to `BRIDGE_HOST_UNAVAILABLE`.

`tests/LWBridge.Desktop.Checks/OverviewBridgeNormalCompositionChecks.cs:70-126` actually records both relevant facts: lifecycle skips listener startup when `testHooks` exists, and the shared host rejects different client identity. The later integration matrix in `Program.cs:6705-6840` nevertheless constructs lifecycle with test hooks, then calls `integrated.SaveGameRoot`, so it never distinguishes the real A→B listener-retarget failure.

This is a test-composition blind spot, not evidence that the lower-level rebind logic is wrong.

## Lifecycle, ownership and recovery capabilities already present

State: **IMPLEMENTED_NOT_VALIDATED** for the canonical campaign integration. These paths have substantial deterministic/historical coverage but were not executed by this worker.

`OverviewLifecycleService` already owns the recovered command surface; `OverviewLifecycleService.cs:113-115` handles start, stop, status, startup reconcile and update/restart, and `:312-320` dispatches them.

- `CreateInstanceStatus`, `:322-388`, distinguishes stopped, starting/error, exact owned running state, unmanaged selected-path process, maintenance and bridge-disconnected cases.
- `ReconcileStartupAsync`, `:411-445`, is one-shot per lifecycle, honors both recovered `autoLaunchAll` and persisted native `AutoLaunchGame`, avoids launching over running/start state, and leaves a correlated repair journal for the repair route.
- `StartAsync`, `:579+`, refuses missing root, concurrent lifecycle work, already-owned game and same-root unmanaged game; it establishes a fresh session/challenge, guarantees the control-pipe host before launch registration, runs the existing official/current-client settle path in non-test production, and only publishes owned state through the lifecycle.
- `StopAsync`, around `:975+`, requires the exact current instance identity and performs owned cleanup rather than closing an arbitrary same-named process.
- `UpdateAndRestartAsync`, `:447-504`, only acts on a correlated repair snapshot, restores via the stop helper, clears the old runtime identity, then starts a fresh owned instance; error entries preserve recovered result style.
- `OverviewLifecycleProfileInstanceStatus.cs:7-91` exposes exact current owned instance identity/state and derives bridge-connected/fresh-heartbeat/identity-confirmed connection state.
- `OverviewLifecycleRecovery.cs:100-113` persists `GameDesiredRunning` and gates reconnect on `AutoReconnect && GameDesiredRunning`. Recovery then runs serialized, exact-owned cleanup/restart with status transitions and stable verification; the launch loop is visible at `OverviewLifecycleRecovery.cs:350-430`.
- `GameRecoveryStatusCommandService.cs:21-56` requires exact current profile runtime and returns current lifecycle recovery state; foreign/missing runtime fails as `PROFILE_RUNTIME_UNAVAILABLE`.
- `ProxyStatusCommandService.cs:40-104` is profile-scoped, combines public native-root/proxy-resource state, exact selected-root process status, lifecycle `RuntimeManaged`, and repair-required derivation.
- `LWBridgeBackend.InvokeAsync:197-205` routes lifecycle first, validates command scope, then the async composite; the old synchronous blocked start/stop fallback is therefore not used in normal production while lifecycle is composed.

These capabilities should be connected/fixed, not rebuilt in a parallel service.

## Status, event and shutdown composition

State: **IMPLEMENTED_NOT_VALIDATED**.

- `LWBridgeWindow.cs:23-33` marks `bridge://status` and `bridge://game-recovery` profile-scoped.
- `LWBridgeWindow.cs:2657-2670` publishes subscribed status/recovery after relevant native commands.
- `LWBridgeWindow.cs:2771-2786` forwards asynchronous `OverviewLifecycleService.RecoveryStatusChanged` to the current document only.
- `LWBridgeWindow.cs:2806-2817` wraps profile-scoped events as `{profileId,payload}`, matching the frontend's profile retirement/unwrap model.
- Current App's 5-second status loop is owned by selected profile and retires stale completion. Because it is App-level rather than Home-page-local, it continues while Home is hidden, matching retained page behavior.
- `LWBridgeWindow.cs:2831-2865` closes the document request owner first, unsubscribes native events, disposes Map workers, closes lifecycle, then closes the application-owned shared bridge host. This ordering is appropriate for graceful host shutdown.

One current semantic gap remains in the frontend busy owner around startup reconcile as described above; status/recovery transport itself is already present.

## Preferences and persistence

### Auto Launch

Recovered frontend behavior is source-backed: localStorage key `lwbridge.autoLaunchGame`, default enabled unless exact stored string `false`, with immediate UI mutation. Current App preserves immediate local state and adds serialized native acknowledgement/rollback through `local_config_set`, while `LocalConfigStore.cs:8-28` persists `AutoLaunchGame` and `LWBridgeBackend.SetLocalConfig` persists it.

Current startup reconcile is dual-gated: current App sends local `autoLaunchGame`; lifecycle `ReconcileStartupAsync` also checks `config.Snapshot.AutoLaunchGame`. This makes native persistence authoritative against stale local true state and is a reasonable clone adaptation, but fresh-host/restart persistence still needs the campaign's isolated end-to-end proof.

### Automatic Reconnection

Recovered frontend command ownership is profile-scoped `set_automation` for `autoForceUpdateReload`. Current App keeps a profile-keyed draft adapter and current backend validates the profile before `SetAutomation`; `LWBridgeBackend.cs:650-658` stores `LocalConfigStore.AutoReconnect` and calls `OverviewLifecycleService.NotifyAutomationChanged`.

`LocalConfigStore.cs:15-22` stores `ProfileId`, `GameRoot`, `AutoLaunchGame`, `AutoReconnect`, and `GameDesiredRunning` in one local config. This is coherent for the current one-profile production composition. It is not a future multi-profile persistence model: if production profile replacement is enabled later, AutoReconnect/root/lifecycle must not continue sharing one immutable local config owner.

## Profile/bootstrap composition

State: single-profile **IMPLEMENTED_NOT_VALIDATED**; multi-profile/profile replacement **BLOCKED** in normal composition.

- `LWBridgeWindow.cs:126-135` creates one persistent `LocalConfigStore` for normal mode.
- `LWBridgeWindow.cs:171-179` creates `ProfileRegistryCommandService` and ensures the current config profile exists in `controller.db`; default capacity remains one.
- `LWBridgeBackend.GetBootstrap`, `LWBridgeBackend.cs:105-118`, returns exactly the config-backed current profile, not the `ProfileRegistryStore` selected-profile snapshot.
- `src/LWBridge.UI-0.3.17/src/shellState.js:17-32` documents that native bootstrap currently contains one profile; positive multi-profile examples are preview-only.
- Profile-specific settings/runtime stores are constructed under `LWBridgeRebuild/profiles/<config.ProfileId>/...` once in `LWBridgeWindow.cs:180-237`, as are lifecycle/map services later in construction.
- Although `ProfileRegistryCommandService` implements `profile_list`/`profile_select`, selecting its controller-db row does not replace `LocalConfigStore.ProfileId`, reconstruct lifecycle, retarget profile runtime stores, or alter `backend.ProfileId`.

Accordingly, canonical profile A/B/A replacement cannot yet be claimed. A bounded future profile lane needs an explicit host composition/reconstruction owner; it should not mutate the existing backend's profile id in place while services remain bound to the old stores.

## Deterministic checks: useful coverage and risky blind spots

No test runner was executed in this source-recovery task. That is deliberate: the monolithic Desktop checks contain process-launch cases (`tests/LWBridge.Desktop.Checks/Program.cs:6656+` starts temporary copied `cmd.exe` images to exercise exact process ownership), and this helper phase forbids process control. Legacy live proof helpers are also outside this phase.

Useful existing coverage:

- `GameRootNativeStatusChecks.cs` and `GameRootSelectChecks.cs` cover native root candidate order, public schema, normalization, cancel/invalid/save/state-unavailable, and the intentional native-vs-strict validity split.
- `Program.cs:6705-6840` covers strict `SaveGameRoot` → lifecycle rebind at a stopped boundary, missing→valid and A→B, invalid selection preservation, immediate test-hook launch from selected root, and active-session retarget refusal.
- `Program.cs:3212-3365` covers request lifetime/cancel/teardown/races.
- Overview lifecycle/recovery tests use isolated config and `OverviewLifecycleTestHooks` to prove exact ownership without touching the owner's game.
- `OverviewBridgeNormalCompositionChecks.cs` statically verifies normal shared-host/lifecycle ordering and explicitly records that a different listener client identity is rejected.

Blind spots that matter to A/B/C/D:

1. `GameRootSelectChecks.cs:135-166` creates `new LWBridgeBackend(backendConfig)` without lifecycle. It therefore proves persistence/envelope but cannot catch native picker → lifecycle rebind loss.
2. `Program.cs:6705-6840` exercises `SaveGameRoot`, not the actual `LWBridgeWindow` picker path.
3. That rebind matrix supplies `OverviewLifecycleTestHooks`; `OverviewLifecycleService.EnsureControlPipeHostStarted` skips the real listener whenever hooks exist. It therefore cannot catch production A→B host binding rejection.
4. Static `OverviewBridgeNormalCompositionChecks` knows the different-client listener is rejected but has no root-retarget composition case connecting that fact to `RebindGameRoot`.
5. Browser/host probe paths are useful later for Milestone K, but they are browser/native host exercises and were not run in this helper phase.

The later implementation should add a focused root-retarget test that distinguishes actual native selection, lifecycle root ownership and shared-host expected-client retarget without launching Last War. Avoid relying only on the monolithic `Program.cs` runner for this distinction.

## Smallest safe implementation sequence

1. **Separate native-root probe from commit in `GameInstallationService.cs`.** Preserve exact native selection normalization/`{canceled,path,valid}` semantics and four-field public native status. Expose enough internal logic for the composition owner to decide whether/when persistence and launch binding can move atomically. Do not replace the native contract with strict `SaveGameRoot` as a shortcut.
2. **Add one native composition coordinator for root replacement, preferably where the existing backend already owns installation + lifecycle + shared host (`LWBridgeBackend.cs`), with `LWBridgeWindow.SelectGameRootAsync` calling it.** At a stopped/recovery-safe boundary, it must prevent config/lifecycle/transport disagreement. For strict launch-admitted B after A, retarget the shared control-pipe host using its existing stop/start ownership before B can launch, with rollback/failure ordering that never leaves lifecycle pointed at a different root than the persisted root. For native-valid but strict-launch-invalid selections, preserve the public native selection contract while explicitly making lifecycle admission unavailable rather than retaining a stale A lifecycle.
3. **Expose launch-admission/provider availability honestly to canonical Home without changing recovered root-status schema.** If an extra clone-internal capability/status signal is necessary, label it clone-internal and keep it separate from recovered original commands. Home must not enable lifecycle solely because a weak native root is `valid` and callbacks exist.
4. **Add focused deterministic tests for the exact missing edges.** Cover native selection no-root→B, A→B, native-valid/strict-invalid, active-owned retarget rejection, transport A→B retarget, persistence/transport failure rollback, and no stale old-root launch. Use isolated roots and inert host/service boundaries only. Retain a frontend source/current-App check distinguishing `proxyBusy` manual work from `gameLaunchBusy` startup reconcile so the now-correct split cannot regress.
5. **Then run bounded B/C/D offline integration.** Exercise canonical request registry → backend → lifecycle/status/recovery with isolated config/profile roots, cancellation, repeated click, startup/manual overlap, stale document/profile retirement, hidden Home polling, restart persistence and graceful close. Live Last War/updater validation remains a later explicit boundary.
6. **Treat production multi-profile replacement as a separate host-composition dependency.** The current one-profile bootstrap is coherent. If the campaign requires A/B/A in normal native mode, implement profile-owner reconstruction/selection explicitly across bootstrap, backend/lifecycle/runtime stores instead of merely exposing the existing registry command.

## Proposed bounded production ownership

If the prime opens a Home/native implementation lane after review, a safe ownership split is:

- **Coordinator/shared frontend:** `src/LWBridge.UI-0.3.17/src/App.jsx` only for any shared launch-capability consumption or follow-up validation; the recovered busy ownership is already restored in the final live-current read. `HomePage.jsx` only if the capability predicate itself needs adjustment; preserve accepted layout/labels.
- **Home/native lane:** `src/LWBridge.Desktop/GameInstallationService.cs`, `LWBridgeBackend.cs`, `LWBridgeControlPipeHostState.cs`, and a tightly bounded part of `LWBridgeWindow.cs` for the picker/composition handoff. `OverviewLifecycleService.cs` should only change if a new safe unbind/rebind primitive is demonstrably required; reuse its existing state gate rather than duplicating lifecycle ownership.
- **Focused checks:** `tests/LWBridge.Desktop.Checks/GameRootSelectChecks.cs` plus a new/appropriate focused root-retarget composition check. Avoid modifying broad historical proof solely to make a new path pass.
- **Profile reconstruction:** reserve as a separate bounded lane because it touches `LWBridgeWindow` service construction, `LocalConfigStore`, profile registry/bootstrap and many profile-owned stores; do not mix it into the root/busy fix unless the prime deliberately takes that dependency.

## Validation / unknowns

- Read-only source recovery verified recovered bundle command strings/payloads and exact byte offsets listed above.
- Live-current source was re-read after concurrent frontend changes; lifecycle callbacks/startup reconcile are present and the later coordinator edit restores the recovered `proxyBusy` manual / `gameLaunchBusy` startup split.
- Native picker, backend rebind, shared-host path binding, lifecycle start/reconcile/recovery, profile/bootstrap, request lifetime and shutdown composition were traced directly in source.
- Existing deterministic checks were inspected, including their test-hook boundaries. No tests were executed because this helper phase explicitly disallows process/native/browser activity and the broad runner contains process-launch cases.
- No Git operation was performed, so this worker does not claim whether concurrent live-current frontend edits are committed, staged, or only working-tree changes.
- No live Last War/current-client/provider claim is made here. The existing lifecycle's production helper/current-client path is implemented, but its current campaign acceptance remains offline/unvalidated until the coordinator runs the allowed bounded proofs and later owner-gated live validation if needed.

## Blockers

- **B/C:** native picker persistence is not lifecycle-rebind-safe.
- **B/C/D:** A→B strict root replacement is not shared-control-pipe-retarget-safe even through existing `SaveGameRoot`/`RebindGameRoot`.
- **B/C/D profile replacement acceptance:** canonical production remains one immutable config/profile composition; registry selection does not reconstruct the runtime owner graph.
- **Provider availability:** public native root validity is intentionally weaker than lifecycle launch admission; canonical Home currently has no separate dynamic launch-admission signal, so a native-minimal root can look actionable when the real lifecycle cannot launch it.
