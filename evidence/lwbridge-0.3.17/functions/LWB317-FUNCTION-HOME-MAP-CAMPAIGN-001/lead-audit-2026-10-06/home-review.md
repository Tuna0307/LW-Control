# Independent lead Home B/C/D source review — 2026-10-06

Reviewed HEAD `540bc53d73053bb1eb70d6a09fea851a9e96d625` plus frozen worker WIP identified by `worker-checkpoint.json`. Read-only inspection of production source, checks and recovered assets; no test/build/browser/native/game/updater execution. Only this review file was written. Earlier `home-lane/recovery-audit.md` was treated as a claim and its important conclusions rechecked.

**Disposition: B/C/D remain unfinished.** The same-host root rebind, idle shared-host retarget, strict Home Start admission and lifecycle callback routing are present. Literal profile replacement is absent; fresh-host installation ownership still has a concrete fallback discrepancy; canonical delayed/error/lifetime evidence is incomplete. Nothing here establishes LIVE_PROVEN or original native-runtime parity.

## Source-backed remaining defects / implementation gaps

### 1. Fresh host can bind a different installation from the native-selected root

`LWBridgeBackend.cs:131-150` correctly binds an in-place picker selection to `installation.Validate(selectedPath, "selected")`, unbinding strict-invalid selection rather than retaining a detected fallback. However, fresh normal composition still calls `new GameInstallationService(config).GetStatus()` at `LWBridgeWindow.cs:251` and constructs the lifecycle from that result at `:262-267`.

`GameInstallationService.GetStatus` (`:65-86`) tries strict validation of configured B, then returns strict-valid default A when B fails. Native public status instead lists saved B first (`:192-208`, `:269-275`), and the clone-internal admission method strictly validates that exact public root (`:284-297`). Thus native-valid/strict-invalid saved B + strict-valid detected A produces Home root B/admission=false but lifecycle root A after restart. Startup reconcile can then call Start on A (`OverviewLifecycleService.cs:424-448`, `:599-610`) when its startup gates are true. This is a source-proven ownership disagreement, not an executed launch result.

Required: align fresh production root selection with exact public/native selected-root launch admission, preserve recovered weak picker predicate and explicit root errors, and distinguish saved/native B from fallback A across restart with actual isolated config and normal composition boundary. Do not simply weaken strict lifecycle admission. Existing `GameRootSelectChecks.cs:226-375` checks in-place selection/rebind; its `without host restart` assertion at `:347` does not cover this path.

### 2. Normal profile selection does not replace the profile-bound owner graph

The normal host constructs config and profile database/runtime services once from `config.Snapshot.ProfileId` (`LWBridgeWindow.cs:135-145`, `:176-215`, `:262-281`). Backend constructs profile-bound status/recovery/history owners once (`LWBridgeBackend.cs:83-103`) and bootstrap exposes only one config-backed profile (`:110-123`). `ProfileRegistryCommandService.cs:60-71` updates registry selection and optionally focuses a window; it does not replace config/lifecycle/provider/store ownership. Canonical App uses immutable `backendBridge.profileId` in normal mode (`App.jsx:101-103`); A/B/A switching callbacks are preview-only (`:114-139`).

Required: finish the recovered owner contract for real selection/replacement and native bootstrap, retire pending requests/events/actions and profile-owned drafts, isolate profile config/status/provider/database ownership, then prove A/B/A with deferred work. Do not promote registry mutation or preview switching to full B/C/D acceptance. Batch/sidebar lifecycle controls likewise require that same owner contract.

### 3. Failed status reads retain stale UI connected availability

`App.readStatusSnapshot` replaces runtime/proxy only on fulfilled reads (`App.jsx:402-411`) and sets an error on rejection (`:420-424`). It never invalidates the failed component. `connectionState` continues deriving `connected` from the last proxy gameRunning=true and last runtime xluaOnline=true (`mapBackend.js:182-188`; `App.jsx:599-602`). After a previously connected status followed by rejected/deferred bridge reads, canonical Map can continue receiving online=true until a successful disconnected result arrives. The campaign D requirement explicitly calls for stale status not to make a disconnected/wrong-profile provider appear available.

This finding concerns UI availability/status honesty, not a demonstrated native dispatch bypass: backend readiness uses exact current lifecycle heartbeat/process identity (`LWBridgeBackend.cs:728-731`; `OverviewLifecycleService.cs:1343-1361`); current Map source obtains a ready session (`OverviewMapScanSession.cs:16-28`; `CurrentClientMapBlockSource.cs:123-135`). Preserve source timing; make failed/retired status ownership explicit and prove recovery to connected after the correct fresh read.

## Verified fixes and contracts that should be retained

- Same-host weak B no longer retains strict fallback A: exact picker validation/rebind at `LWBridgeBackend.cs:139-150`; distinguishing regression is present at `GameRootSelectChecks.cs:311-326`. Fresh-host limitation above remains separate.
- Shared pipe idle A→B retarget is implemented by `OverviewLifecycleService.cs:895-925` calling `EnsureRpcTransportAsync`; `LWBridgeControlPipeHostState.cs:195-248` preserves same-identity idempotence and rejects retarget while registrations/routes/calls exist with `BRIDGE_HOST_BUSY`. Strict selected-root validity does not promise the host is currently idle.
- Start/Stop/update callbacks use recovered commands (`App.jsx:696-769`). Exact native ownership still rejects foreign/stale operations rather than synthesizing success (`OverviewLifecycleService.cs:592-619`, `:988-1033`). Native service Close and focused helper cancellation are source-present; executing tests remains the coordinator's job.
- Polling is App-owned, serialized and independent of Home visibility (`App.jsx:515-541`); recovery reads/events have separate closed/profile guards (`:464-476`). Shutdown closes document requests, unsubscribes events, drains Map owners, closes lifecycle, then shared host (`LWBridgeWindow.cs:3230-3257`). These are source findings, not executed document shutdown proof.

## Original source verification

Read the recovered bundle directly and verified SHA-256 `44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6` for `evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js`.

EXACT_BYTES frontend anchors: profile status/start/update/reconcile/stop command wrappers around bytes `202578`, `202643`, `202724`, `202788`, `202863`; public root wrapper around `205405`; manual start caller around `365794`; local preference key at byte `246758` and adjacent `getItem(...) !== "false"` / immediate `setItem(String(value))`; profile reconnect read/write adapter at bytes `362308-362444`. Those bytes support frontend command/payload/local preference ownership. They do not prove the original native service implementation or current-client compatibility.

Preserve original Auto Launch immediate local-storage contract/default true. Canonical App intentionally reads it at `App.jsx:158`, retains immutable startup snapshot at `:190`, and serializes native acknowledgement/rollback at `:627-661`. Native bootstrap also exposes Auto Launch (`LWBridgeBackend.cs:117`; `LWBridgeWindow.cs:436-444`), but App does not consume that field; polling updates only committed rollback baseline (`App.jsx:413-418`). Native/local disagreement therefore needs fresh-host/restart evidence and a documented convergence policy that preserves the original local contract. Treat it as an unresolved acceptance/transport issue, not permission to redesign the preference as native-only.

## Required remaining checks / proof gaps

1. **B:** add fresh-host saved weak B/detected strict A regression above; prove cancellation/failure/deferred picker ack, reload/restart persistence with actual isolated stores; execute actual App Auto Launch closure for delayed success/rejection, overlapping edits, native/local startup disagreement and latest-commit rollback.
2. **C:** execute focused lifecycle runner now wired at `tests/LWBridge.Desktop.Checks/Program.cs:310-315`, plus root/transport checks. Verify canonical callback request owner, pending repeated clicks, cancel/shutdown, wrong profile and stale A/B/A acknowledgements through a real isolated native composition. Helper-hooks proof is not current-client/native pipe parity.
3. **D:** execute connected→rejected/deferred→disconnected→connected status cases, reconnect mid-work/profile replacement, recovery read/event ordering, hidden retained Home, document replacement/unmount and graceful shutdown. Manual callbacks and picker check profile identity incompletely for future A/B/A/document replacement (`App.jsx:669-769`); add owner-generation/lifetime proof when replacement is implemented.
4. Current `check-home-integration.mjs:143-191` largely source-inspects App lifetime/predicate strings; it does not execute the actual App acknowledgement closures. The new desktop campaign proof explicitly creates no Home lifecycle (`LWBridgeWindow.cs:310-314`) and invokes raw native commands for local config (`:2462-2469`); it cannot supply provider-positive Home launch/recovery or UI toggle proof. Keep those proof boundaries explicit.
5. `main.jsx:8-13` wraps App in StrictMode. Its development effect replay interacts with the startup one-shot ref (`App.jsx:431-462`): cleanup closes the first completion while replay returns early. Check this development/integration seam dynamically if using a dev harness; do not claim a packaged production StrictMode defect from source alone.
6. Correct evidence drift: `home-lane/acceptance.md` still calls lifecycle runner unmodified/proposed and records compilation only, whereas its flag exists in WIP. `queue.json` still leaves B/C/D IN_PROGRESS without concrete profile blocker disposition. Update execution results, precise continuation, Home master/parity ledger/handoff after integration; no claim of accepted B/C/D merely because focused checks/build pass.

External updater/live current-client positive behavior stays outside the unattended assignment. Preserve its dependency record and future live plan without updater/game execution. Remaining profile composition, fresh-root startup, status invalidation and canonical offline acceptance are implementable/provable work, not reasons to stop the entire campaign.
