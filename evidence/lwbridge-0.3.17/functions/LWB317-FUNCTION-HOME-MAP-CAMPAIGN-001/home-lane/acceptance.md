# Home lifecycle B/C/D deterministic acceptance

Campaign: `LWB317-FUNCTION-HOME-MAP-CAMPAIGN-001`

Scope: source-ready, offline acceptance for the existing `OverviewLifecycleService` and `LWBridgeBackend` lifecycle ownership boundary. The check uses only `OverviewLifecycleTestHooks`, isolated temporary config roots, in-memory helper results and cancellation tokens. It does not launch Last War, an updater, an original service, a browser/native host, or any live provider action.

Implementation: `tests/LWBridge.Desktop.Checks/HomeCampaignLifecycleChecks.cs`.

## Covered deterministic cases

| Campaign edge | Acceptance case | Distinguishing assertion | Source contract |
| --- | --- | --- | --- |
| Pending/delayed Start | `RunPendingRepeatedStartAsync` | A blocked helper leaves one `starting` instance with no PID. A repeated Start returns `GAME_OPERATION_IN_PROGRESS`, calls no second helper, and only the admitted Start may publish `running`. A later duplicate returns `GAME_RUNNING`. | `OverviewLifecycleService.cs:592-619`, `:727-776` |
| Repeated/in-flight ownership | `RunPendingRepeatedStartAsync` | Helper Start count remains exactly one while the first transaction is blocked; status retains the same in-flight session. | `OverviewLifecycleService.cs:600-619` |
| Close/cancel during startup | `RunCloseDuringStartAsync` | `Close()` writes the exact session/challenge cancellation marker; the helper surfaces `GAME_OPERATION_CANCELLED`; pre-publication cancellation leaves no PID/instance and performs no invented compensating Stop. | `OverviewLifecycleService.cs:403-420`, `:778-804`, `:874-893` |
| Stale process identity | `RunStaleIdentityAndExactStopAsync` | Two deterministic missing-process observations preserve the owned instance while changing it to `error/recovering` with `GAME_EXITED_RESTORE_REQUIRED`; AutoReconnect=false does not create a replacement Start. | `OverviewLifecycleService.cs:1405-1415`; `OverviewLifecycleRecovery.cs:115-155`, `:316-325` |
| Non-owner refusal | `RunStaleIdentityAndExactStopAsync` | Foreign `instanceId` returns `INSTANCE_NOT_OWNED` before any helper Stop. | `OverviewLifecycleService.cs:988-1005` |
| Exact instance Stop | `RunStaleIdentityAndExactStopAsync` | Cleanup after stale identity receives the original session, PID, executable path and process creation timestamp, then ends `stopped` and clears desired-running. | `OverviewLifecycleService.cs:1008-1033` |
| Native rejection/error and retry | `RunNativeRejectionRetryAsync` | Synthetic `BRIDGE_START_TIMEOUT` leaves `error` with no PID; a retry uses a fresh session/challenge, succeeds, and remains exactly stoppable. | `OverviewLifecycleService.cs:597-619`, `:727-819` |
| Wrong/missing profile scope | `RunProfileMismatchAsync` | Backend rejects foreign Start as `PROFILE_SCOPE_MISMATCH`, missing Start as `PROFILE_REQUIRED`, and foreign status as `PROFILE_RUNTIME_UNAVAILABLE` before the lifecycle helper runs. | `LWBridgeBackend.cs:237-245`, `:1094-1130`, `:1186-1197` |
| Recovery restart | `RunRecoveryAndShutdownAsync` | With reconnect enabled, confirmed process loss performs exact old-session cleanup, starts once through normal `StartAsync`, uses a fresh session, and reaches recovery `succeeded`. | `OverviewLifecycleRecovery.cs:125-153`, `:328-447` |
| Shutdown during recovery | `RunRecoveryAndShutdownAsync` | A recovery relaunch is forced to fail and block in its retry delay. `Close()` cancels that active recovery; it returns to idle, leaves no managed process, and later observations cannot relaunch from the closed lifecycle. | `OverviewLifecycleService.cs:403-420`; `OverviewLifecycleRecovery.cs:81-88`, `:328-447` |
| Fresh-host restart persistence | `RunRecoveryAndShutdownAsync` | A second `LocalConfigStore` owner observes persisted reconnect + desired-running state. A fresh lifecycle consumes startup reconcile once, launches through the same lifecycle contract, and an exact Stop clears desired-running again. | `LocalConfigStore.cs:51-107`; `OverviewLifecycleService.cs:424-458` |

The fixtures return helper JSON in the same validated start/stop shapes consumed by `ValidateStartResult` / `ValidateStopResult`; process identity is supplied only by `ProcessMatches`. No test child process is used.

## Boundaries that this lifecycle-only file must not fabricate

### Canonical profile retirement / A-B-A replacement

The lifecycle test surface can prove that the current backend rejects a foreign profile before helper execution, but it cannot prove production profile replacement. Normal production still constructs one `LocalConfigStore`/backend/lifecycle owner graph for the current config profile. Registry selection does not reconstruct those profile-bound services. The existing campaign source audit records this as a production composition dependency in `home-lane/findings.md:178-189` and `:240-245`.

Canonical frontend retirement is also outside `OverviewLifecycleTestHooks`. Current App owns that guard with the selected-profile generation/ref checks in `src/LWBridge.UI-0.3.17/src/App.jsx:263-283`, startup reconcile retirement in `:393-425`, recovery event retirement in `:427-439`, and serial profile-owned status polling in `:478-506`. Exercising those requires the frontend/native request-owner integration harness, not a lifecycle helper stub.

### Document/unmount retirement and full host shutdown ordering

`OverviewLifecycleService.Close()` is directly covered, including startup and active-recovery cancellation. The WinForms document owner itself is not injectable through lifecycle hooks. `LWBridgeWindow.cs:2828-2855` owns the current-document check and graceful close ordering: close the document request owner, unsubscribe events, drain Map workers, close the lifecycle, then close the application-owned shared bridge host. A deterministic host/document test belongs to the campaign desktop-integration lane; this file does not claim it.

### Real shared-control-pipe, updater and current-game paths

Test-hook lifecycle construction deliberately bypasses the production shared control-pipe startup branch (`OverviewLifecycleService.cs:895-925`) and cannot validate current Last War compatibility, updater behavior or live provider readiness. Those remain separate campaign dependencies and were not invoked here.

## Validation performed

No test runner or `--live-*` flag was executed.

The first ordinary compile attempt:

`dotnet build tests\LWBridge.Desktop.Checks\LWBridge.Desktop.Checks.csproj --no-restore`

was blocked before C# compilation because the Desktop project invokes `npm ci` and Windows reported `EPERM` while trying to unlink the already-in-use `src\LWBridge.UI-0.3.17\node_modules\@esbuild\win32-x64\esbuild.exe`.

The project explicitly supports skipping that unrelated production-UI target for source compilation (`src/LWBridge.Desktop/LWBridge.Desktop.csproj:47-60`). The bounded compile therefore used:

`dotnet build tests\LWBridge.Desktop.Checks\LWBridge.Desktop.Checks.csproj --no-restore -p:SkipCanonicalProductionUiBuild=true`

Result after the final source edit: **PASS — 0 warnings, 0 errors**. `LWBridge.Desktop.Checks.dll` was produced successfully.

## Proposed isolated runner integration

`Program.cs` was intentionally not edited. To execute this acceptance later without the broad/default runner, add one isolated non-live dispatch beside the existing focused Overview flags around `tests/LWBridge.Desktop.Checks/Program.cs:268-307`:

```csharp
if (args.Contains("--home-campaign-lifecycle-check", StringComparer.OrdinalIgnoreCase))
{
    JsonElement result = await LWBridge.Desktop.Checks.HomeCampaignLifecycleChecks.RunAsync();
    Console.WriteLine(JsonSerializer.Serialize(
        result,
        new JsonSerializerOptions { WriteIndented = true }));
    return 0;
}
```

Then the bounded invocation is:

`dotnet run --project tests\LWBridge.Desktop.Checks\LWBridge.Desktop.Checks.csproj -p:SkipCanonicalProductionUiBuild=true -- --home-campaign-lifecycle-check`

That runner change is outside this worker's editable-file assignment, so the new acceptance is compiled/source-ready but not executed in this lane.

## Recovery coordinator resolution — 2026-10-06

The limitations above are retained as historical worker-lane boundaries, not current
campaign gaps. The coordinator subsequently registered and executed
`--home-campaign-lifecycle-check`; it passed with the pending/repeated Start,
close-during-start cancellation, stale/non-owner identity, native rejection/retry,
profile mismatch, recovery restart and shutdown cases described here.

Production profile ownership is now separately covered by `ProfileRuntimeOwner` plus
the normal `LWBridgeWindow` replacement path. `--profile-runtime-owner-check` passes
registry acknowledgement/rollback and real isolated owner A/B/A replacement. The
packaged recovery packet in `../integration/recovery-closeout.md` additionally drives
the normal profile sidebar through A/B/A, proves first-A request retirement across the
same-document owner swap, cancels an exact B request on document replacement, reopens
the persisted B selection after reload, holds B through a delayed first-A Home Auto
Launch acknowledgement, rolls back a rejected returned-A save, and records zero
outstanding request/listener owners at shutdown. HA-03 now executes connected ->
deferred -> rejected -> disconnected -> connected through those real packaged
controls in addition to the deterministic UI checks.

## RECOVERY-002 resolution — 2026-10-07

The later lead findings are now covered by isolated distinguishing proof. Runtime, evidence and backup roots are injectable; Close removes only exact-owned artifacts. --profile-runtime-owner-check plus the schema-v4 package prove active lifecycle replacement refusal, A/B/A generation retirement and stale/fresh picker ownership. The mounted package drives actual inert lifecycle Launch, Close, Update-and-Launch and recovery. Latest status fencing and the recovered global Auto Launch visible preference are also exercised in the current package. These are offline/inert results pending lead review, not a live Last War/updater acceptance.
