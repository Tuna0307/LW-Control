# Home Milestones B/C/D recovery audit

Campaign: `LWB317-FUNCTION-HOME-MAP-CAMPAIGN-001`

Scope: independent offline/source audit of the live working tree after the Home continuation work. This audit re-read the current `HomePage`/`App` bridge ownership, native game-root selection/admission, `OverviewLifecycleService`, shared control-pipe composition, profile/config ownership, recovery/status paths, `HomeCampaignLifecycleChecks.cs`, `GameRootSelectChecks.cs`, `OverviewBridgeHostTransportChecks.cs`, `check-home-integration.mjs`, and `home-lane/acceptance.md`. No live/browser/game/updater/provider action or test runner was executed by this reviewer.

## Result

The focused one-profile lifecycle/root deterministic implementation no longer has the two production blockers recorded in the original recovery findings:

- Native picker replacement now binds the lifecycle only to the exact selected root. During this audit a live-current correction changed `LWBridgeBackend.SaveNativeGameRootSelection` to use `installation.Validate(selectedPath, "selected")` rather than the legacy fallback resolver (`LWBridgeBackend.cs:139-150`). `GameRootSelectChecks.cs:226-375` now includes the distinguishing native-valid/strict-invalid B versus strict-valid fallback A case: B persists, lifecycle Start returns `GAME_ROOT_NOT_FOUND`, and no helper Start occurs. A later strict-valid B is launchable, while active owned retarget remains rejected.
- Shared control-pipe client-image replacement is now supported at the Start boundary. `OverviewLifecycleService.cs:895-925` derives the selected root's exact `Game/LastWar.exe` path and calls `LWBridgeControlPipeHostState.EnsureRpcTransportAsync`; `LWBridgeControlPipeHostState.cs:195-248` retains same-identity idempotence, permits an idle identity rebind, and fails closed as `BRIDGE_HOST_BUSY` while a registration, connected route, or RPC call is active. `OverviewBridgeHostTransportChecks.cs:44-94` has a focused idle rebind case.
- Canonical Home now has a separate strict launch-admission signal without changing recovered `game_root_status`: `local_game_launch_status` delegates to `GameInstallationService.GetLaunchAdmissionStatus` (`LWBridgeBackend.cs:316-317`; `GameInstallationService.cs:278-298`), and `HomePage.jsx:80-97` requires that strict signal for Start. A weak public-valid root is therefore not presented as launchable.

The remaining concrete blocker to literal full B/C/D acceptance is production profile replacement. Normal production still constructs one persistent `LocalConfigStore`, one backend/lifecycle graph, and profile-owned stores from `config.Snapshot.ProfileId` once (`LWBridgeWindow.cs:129-205`). `LWBridgeBackend.GetBootstrap` returns only that config-backed profile (`LWBridgeBackend.cs:110-123`). `ProfileRegistryCommandService.profile_select` changes the controller registry row but does not reconstruct or replace those already-created owners (`ProfileRegistryCommandService.cs:42-71`). Canonical `App` also uses `backendBridge.profileId` in normal mode; its A/B/A switching path is preview-only (`App.jsx:98-139`). The current `home-lane/acceptance.md:27-41` records this boundary honestly. Therefore the campaign must not mark the normal-production A/B/A replacement requirement proven from the current owner graph.

## Milestone B audit

Game-root selection/cancel/validation is source-composed through the native picker and now preserves the public weak predicate while making strict launch admission exact. The current root-rebind correction specifically closes the stale-fallback launch hazard: selecting a weak B cannot leave the lifecycle bound to detected A.

Preference storage itself is durable and serialized. `LocalConfigStore` persists `AutoLaunchGame`/`AutoReconnect`, refreshes the committed baseline under its cross-instance lock, and existing deterministic checks in `Program.cs:793-885` cover restart persistence, competing writers, and backend partial saves. Canonical `App` also serializes Auto Launch writes, tracks a native-commit epoch/revision, and rolls the visible/local preference back only when the latest native save rejects (`App.jsx:620-661`). Automatic Reconnection uses the profile-keyed draft adapter and native `set_automation` (`App.jsx:663-667`; `LWBridgeBackend.cs:692-700`).

There is still an acceptance gap around the canonical Auto Launch UI acknowledgement lifetime. `check-home-integration.mjs` exercises native bridge request/error envelopes and the reconnect draft adapter, and source-inspects the startup reconcile snapshot, but it does not mount/call the actual `App.updateAutoLaunch` closure through delayed success/rejection. `HomeCampaignLifecycleChecks.cs` proves fresh-host reconnect/desired-running persistence, not a delayed/rejected Auto Launch edit. This is a missing campaign proof, not a source defect identified by this audit.

The full B profile-replacement case remains blocked by the immutable normal host profile composition described above.

## Milestone C audit

The new lifecycle acceptance has meaningful ownership distinctions rather than implementation mirrors:

- a pending Start admits one helper only; repeat Start is `GAME_OPERATION_IN_PROGRESS`; post-publication duplicate is `GAME_RUNNING`;
- Close during pre-publication Start uses the exact session/challenge cancellation marker and leaves no fabricated owned PID/instance;
- foreign Stop is rejected before helper cleanup; stale process identity retains the original session/PID/path/creation tuple for exact Stop;
- a native Start rejection leaves error/no PID and retry uses fresh session/challenge;
- wrong/missing profile work retires before lifecycle helper execution;
- recovery cleanup/relaunch and shutdown cancellation preserve exact ownership and fresh-host intent.

Those cases are in `HomeCampaignLifecycleChecks.cs:52-724` and match the lifecycle gates in `OverviewLifecycleService`/`OverviewLifecycleRecovery`. The runner is now wired in the live working tree at `Program.cs:310-315` as `--home-campaign-lifecycle-check`.

`home-lane/acceptance.md:59-78` is stale on that point: it still says `Program.cs` was intentionally not edited and describes the runner as a future proposal. It also records compilation only, not execution. The evidence should be updated by its owner after the coordinator runs the bounded focused check; this reviewer did not run it under the assigned no-process/no-live constraints.

Provider composition is source-present: normal `LWBridgeWindow` creates the application-owned host before `OverviewLifecycleService` and enables launch binding; lifecycle Start rechecks/retargets the listener before registering the launch binding and invoking the helper. `local_game_launch_status` proves strict selected-root validity only. It must not be described as proof that the shared host is presently idle: a valid root can still receive an explicit transient `BRIDGE_HOST_BUSY` at Start if another route/registration/RPC call prevents host retarget. That behavior is fail-closed and source-backed, but it is a narrower contract than general provider-readiness.

## Milestone D audit

No new source blocker was found in the current one-profile status/recovery/shutdown composition:

- App-level status polling owns an in-flight fence and selected-profile/current-effect retirement, so polling remains active while the retained Home page is hidden (`App.jsx:515-543`).
- recovery status owns a separate selected-profile initial read and `bridge://game-recovery` subscription with profile-envelope/current-effect guards (`App.jsx:464-476`); periodic status refresh does not overwrite that owner;
- startup reconcile is one-shot and uses the immutable startup Auto Launch snapshot while retiring stale completion (`App.jsx:430-462`);
- lifecycle/manual actions have per-profile in-flight ownership and retire stale profile UI acknowledgement (`App.jsx:696-769`);
- `LWBridgeWindow.EmitOverviewStateAsync` and asynchronous recovery publication use the current document, and application shutdown closes the document/request owner, unsubscribes event sources, drains Map workers, closes the lifecycle, then closes the shared host (`LWBridgeWindow.cs:2683-2696`, `:2817-2831`, `:2877-2904`).

`check-home-integration.mjs:143-191` statically guards the current App recovery/polling ownership and strict Start predicate. The lifecycle acceptance covers service Close during Start and active recovery. It does not instantiate the WinForms document owner or prove hidden-page/unmount/graceful-host behavior dynamically. That remains a deterministic integration-proof gap if the work item requires executed host/document acceptance rather than source + focused component checks.

Reconnect behavior itself has deeper existing deterministic coverage in `OverviewReconnectPolicyChecks.cs`; the new lifecycle file adds exact recovery restart, close-during-retry, persistence, and fresh-host reconcile. No updater was invoked here. The source recovery path observes updater/update activity and uses the already-recovered lifecycle, but current-client/updater compatibility remains outside this offline audit.

## Evidence corrections and remaining boundaries

1. **Closed during this audit — picker fallback split.** The live-current backend/test pair now proves native-valid/strict-invalid selection cannot bind a detected fallback installation.
2. **Closed from the earlier findings — shared host A→B retarget.** Idle retarget is an explicit host primitive and lifecycle Start calls it before launch registration/helper execution; busy retarget fails closed.
3. **Closed from the earlier findings — strict Home launch admission.** `local_game_launch_status` is separate from recovered weak root status and gates Start.
4. **Still blocked — normal production A/B/A profile replacement.** Registry selection does not reconstruct the profile-owned runtime graph; preview switching is not production proof.
5. **Still missing acceptance — canonical Auto Launch delayed/rejected acknowledgement.** The source logic is coherent and low-level persistence is tested, but the current campaign evidence does not execute that actual App closure/lifetime.
6. **Evidence drift — Home lifecycle runner.** `Program.cs` now contains the focused runner, while `home-lane/acceptance.md` still calls it proposed/unmodified and records no execution result.
7. **Not proved here — full native host/current-client/updater path.** Lifecycle hooks intentionally bypass the real shared pipe/current game; this review made no live provider or current-client compatibility claim.

## Blocker disposition

For the bounded current **single-profile lifecycle/root deterministic sub-scope**, I found no remaining production ownership defect after the concurrent exact-selected-root fix. The lifecycle edge cases, strict admission, and idle/busy shared-host retarget contracts have source-backed focused coverage.

For the work item's **literal full Milestones B/C/D acceptance**, two items remain before closeout: normal-production profile replacement/A-B-A is not implemented by the current host owner graph, and the campaign still lacks executed canonical Auto Launch delayed/rejected acknowledgement proof. The WinForms document/hidden-page/full-host shutdown behavior is source-coherent but should remain described as source/focused-check evidence unless the coordinator supplies the requested deterministic integration execution.

Live/current-client/updater compatibility remains separately blocked by the campaign's offline boundary and was not exercised or inferred here.

## Coordinator resolution after the audit — 2026-10-06

This audit is preserved as the independent snapshot that drove the recovery. Its
production gaps were subsequently resolved in the coordinator-owned implementation:

- `ProfileRuntimeOwner` plus the normal `LWBridgeWindow` selection callback now
  replaces config/backend/lifecycle/status/Map/Auto/draft/settings owners. The focused
  `--profile-runtime-owner-check` proves isolated A/B/A storage/runtime replacement,
  acknowledgement/rollback, restart selection and provider fences.
- The canonical UI bridge now owns a profile generation as well as a profile ID.
  Same-document delayed replies/events from first A are retired across A -> B -> A,
  and profile-owned effects use that generation at dispatch/completion time.
- The isolated packaged WebView proof drives the real profile sidebar A -> B -> A,
  deliberately releases a first-A delayed Map request after B is current, then proves
  returned A is a new native generation.
- Document replacement is executed, not inferred: a delayed B request is cancelled by
  the real document request registry, reload reopens persisted B, and final shutdown
  records a closed registry, zero active requests/subscriptions, detached runtime
  events and zero cleanup failures.
- Home Auto Launch is changed through the actual React control and verified against
  profile-local native config, including return-to-A persistence and non-inheritance
  by B. The existing optimistic/deferred/rejection source contract remains covered by
  the Home/draft integration checks; no live lifecycle is fabricated.
- HA-03 is covered both deterministically and in the packaged host: a fresh paired
  read invalidates stale availability while deferred, an injected paired read failure
  renders unavailable, and a later fresh pair recovers to the current disconnected
  state. The deterministic UI matrix additionally proves the fresh-connected case.

The final recovery still does not claim a live Last War launch, updater execution or
protected-service validation. Those boundaries remain intentionally outside this
assignment.
