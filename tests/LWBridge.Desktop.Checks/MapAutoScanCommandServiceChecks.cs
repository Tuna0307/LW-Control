using System.Text.Json;
using LWBridge.Desktop;
using Map317 = LWBridge.Map317;

namespace LWBridge.Desktop.Checks;

internal static class MapAutoScanCommandServiceChecks
{
    internal static async Task RunAsync()
    {
        RecoveredConstantsAndNormalizationStayExact();
        await BackendCommandRouteIsProfileScopedAsync();
        await EnableDisableRunNowAndPersistenceStayProfileOwnedAsync();
        await OrdinaryEditsPreserveCompletedDeadlineAsync();
        await PersistenceFailureDoesNotMutateLiveStateAsync();
        await DisabledConfigCannotAdmitStaleDueCycleAsync();
        await TerminalLastErrorContinuesAcrossServersAsync();
        await ThrownTargetFailuresAbortRemainingServersAsync();
        await ExplicitTargetsRunWhenCurrentServerIsUnknownAsync();
        await TimeoutStopsOnlyTheOwnedAutoScanAsync();
        await DisableStopsOwnedAutoScanButNeverManualScanAsync();
        await StaleAutoOwnerCannotStopReplacementManualScanAsync();
        await RealMap317StaleAutoOwnerCannotStopReplacementManualScanAsync();
        await RealMap317MultiServerCompletionReturnsOriginAsync();
        await RealMap317CancelStopsExactOwnedScanAsync();
        await RealMap317DisableStopsExactOwnedScanAsync();
        await RealMap317TimeoutStopsExactOwnedScanAsync();
        await RealMap317RetirementStopsExactOwnedScanAsync();
        await RealMap317BetweenTargetManualOwnershipCannotBeMovedAsync();
        await ManualOwnershipAfterAdmissionCannotBeMovedAsync();
        await AdmissionIsSerializedAsync();
        await ExplicitCancelStopsOwnedScanAndSchedulesNextCycleAsync();
        await RestartStateIsReconciledWithoutMovingTheDeadlineAsync();
        await ProfilePathsStayIsolatedAsync();
        await RetirementStopsOwnedWorkAndLeavesDueDeadlineForRestartAsync();
        await RealSchedulerLoopAdmitsDueCycleAndRetiresAsync();
    }

    private static void RecoveredConstantsAndNormalizationStayExact()
    {
        Check(MapAutoScanCommandService.MinimumIntervalMinutes == 20 &&
              MapAutoScanCommandService.MaximumIntervalMinutes == 1440 &&
              MapAutoScanCommandService.MaximumServerIds == 20 &&
              MapAutoScanCommandService.MinimumServerId == 1 &&
              MapAutoScanCommandService.MaximumServerId == 99999 &&
              MapAutoScanCommandService.DueTickMilliseconds == 5_000 &&
              MapAutoScanCommandService.ScanPollMilliseconds == 2_000 &&
              MapAutoScanCommandService.ScanTimeoutMilliseconds == 2_700_000,
            "recovered Auto Scan limits/timings changed");

        MapAutoScanConfig defaults = MapAutoScanCommandService.NormalizeConfig(null);
        Check(!defaults.Enabled && defaults.IntervalMinutes == 60 &&
              defaults.ServerIds!.Count == 0 &&
              defaults.SelectedTypes!.SequenceEqual(new[] { "truck", "railway", "dispatch", "ghost", "treasure" }) &&
              defaults.ScanMode == "fast" && defaults.ReturnToOriginalServer && defaults.NextRunAt == 0,
            "recovered Auto Scan defaults changed");

        MapAutoScanConfig normalized = MapAutoScanCommandService.NormalizeConfig(new MapAutoScanConfig(
            true,
            2,
            new[] { 9, 9, 0, 10, 100000, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28 },
            new[] { "treasure", "bad", "city", "treasure", "resource" },
            "NORMAL",
            false,
            -4));
        Check(normalized.IntervalMinutes == 20 &&
              normalized.ServerIds!.SequenceEqual(Enumerable.Range(9, 20)) &&
              normalized.SelectedTypes!.SequenceEqual(new[] { "treasure", "city", "resource" }) &&
              normalized.ScanMode == "fast" && !normalized.ReturnToOriginalServer && normalized.NextRunAt == 0,
            "Auto Scan normalization must clamp while preserving server/type order and deduplication");

        MapAutoScanConfig upper = MapAutoScanCommandService.NormalizeConfig(normalized with
        {
            IntervalMinutes = 9999,
            SelectedTypes = Array.Empty<string>(),
            ScanMode = "normal",
        });
        Check(upper.IntervalMinutes == 1440 &&
              upper.SelectedTypes!.SequenceEqual(MapAutoScanCommandService.DefaultSelectedTypes) &&
              upper.ScanMode == "normal",
            "Auto Scan upper clamp/type fallback/exact normal mode changed");
    }

    private static async Task BackendCommandRouteIsProfileScopedAsync()
    {
        string root = TempRoot("backend-route");
        try
        {
            var clock = new FakeClock(900_000);
            var execution = new FakeExecution { Online = false, CurrentServerId = 73 };
            await using var service = Service(Path.Combine(root, "state.json"), execution, clock);
            var config = new LocalConfigStore(persistent: false);
            var backend = new LWBridgeBackend(
                config,
                new CompositeAsyncCommandService(service));

            JsonElement scoped = JsonSerializer.SerializeToElement(
                new { profileId = backend.ProfileId }, JsonOptions.Default);
            object? rawStatus = await backend.InvokeAsync(
                "local_map_auto_scan_status", scoped, CancellationToken.None);
            JsonElement status = JsonSerializer.SerializeToElement(rawStatus, JsonOptions.Default);
            Check(!status.GetProperty("config").GetProperty("enabled").GetBoolean(),
                "clone-internal Auto Scan status must route through the real backend composite");

            JsonElement update = JsonSerializer.SerializeToElement(new
            {
                profileId = backend.ProfileId,
                config = new
                {
                    enabled = true,
                    intervalMinutes = 45,
                    serverIds = new[] { 8, 7 },
                    selectedTypes = new[] { "truck", "treasure" },
                    scanMode = "fast",
                    returnToOriginalServer = true,
                    nextRunAt = 0L,
                },
            }, JsonOptions.Default);
            object? rawUpdated = await backend.InvokeAsync(
                "local_map_auto_scan_config_set", update, CancellationToken.None);
            JsonElement updated = JsonSerializer.SerializeToElement(rawUpdated, JsonOptions.Default);
            Check(updated.GetProperty("config").GetProperty("enabled").GetBoolean() &&
                  updated.GetProperty("config").GetProperty("serverIds").EnumerateArray().Select(v => v.GetInt32()).SequenceEqual(new[] { 8, 7 }),
                "backend Auto Scan config route must preserve normalized profile-owned state");

            JsonElement foreign = JsonSerializer.SerializeToElement(
                new { profileId = "foreign-profile" }, JsonOptions.Default);
            try
            {
                _ = await backend.InvokeAsync(
                    "local_map_auto_scan_status", foreign, CancellationToken.None);
                throw new InvalidOperationException("foreign profile Auto Scan command was not rejected");
            }
            catch (BridgeCommandException error) when (error.Code == "PROFILE_SCOPE_MISMATCH") { }
        }
        finally { DeleteRoot(root); }
    }

    private static async Task EnableDisableRunNowAndPersistenceStayProfileOwnedAsync()
    {
        string root = TempRoot("deadline");
        try
        {
            var clock = new FakeClock(1_000_000);
            var execution = new FakeExecution { Online = false, CurrentServerId = 73 };
            string path = Path.Combine(root, "profile-a.json");
            await using (var service = Service(path, execution, clock))
            {
                MapAutoScanSnapshot enabled = await service.UpdateConfigAsync(MapAutoScanConfig.Default with
                {
                    Enabled = true,
                    IntervalMinutes = 45,
                    ServerIds = new[] { 5, 4, 5 },
                });
                Check(enabled.Config.Enabled && enabled.Config.NextRunAt == 1_000_000 &&
                      enabled.Config.ServerIds!.SequenceEqual(new[] { 5, 4 }),
                    "disabled -> enabled must set nextRunAt to now and persist ordered/deduped servers");

                clock.Now = 1_000_777;
                MapAutoScanSnapshot runNow = await service.RunNowAsync();
                Check(runNow.Config.NextRunAt == 1_000_777 && !runNow.Running,
                    "Run Now must be deadline-now admission and stay pending while offline");

                MapAutoScanSnapshot disabled = await service.UpdateConfigAsync(runNow.Config with { Enabled = false });
                Check(!disabled.Config.Enabled && disabled.Config.NextRunAt == 0,
                    "disabling Auto Scan must clear nextRunAt");
            }

            await using var reopened = Service(path, execution, clock);
            MapAutoScanSnapshot persisted = await reopened.GetSnapshotAsync();
            Check(!persisted.Config.Enabled && persisted.Config.IntervalMinutes == 45 &&
                  persisted.Config.ServerIds!.SequenceEqual(new[] { 5, 4 }) && persisted.Config.NextRunAt == 0,
                "profile-owned Auto Scan config must survive service restart");
        }
        finally { DeleteRoot(root); }
    }

    // A due read must not reserve a cycle from a stale config after disable
    // has committed, even when the final admission lock has not run yet.
    private static async Task DisabledConfigCannotAdmitStaleDueCycleAsync()
    {
        string root = TempRoot("atomic-due-disable");
        try
        {
            var clock = new FakeClock(500_000);
            var execution = new FakeExecution { Online = false, CurrentServerId = 317 };
            var readDue = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseAdmission = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await using var service = new MapAutoScanCommandService(
                Path.Combine(root, "state.json"),
                execution.Boundary,
                new MapAutoScanSchedulerHooks
                {
                    UtcNowMilliseconds = () => clock.Now,
                    BeforeDueAdmissionAsync = async () =>
                    {
                        readDue.TrySetResult();
                        await releaseAdmission.Task.ConfigureAwait(false);
                    },
                },
                startScheduler: false);
            _ = await service.UpdateConfigAsync(MapAutoScanConfig.Default with
            {
                Enabled = true,
                ServerIds = new[] { 318 },
            });
            execution.Online = true;
            Task<bool> admit = service.CheckDueAsync();
            await readDue.Task.WaitAsync(TimeSpan.FromSeconds(5));
            MapAutoScanSnapshot disabled = await service.UpdateConfigAsync(
                MapAutoScanConfig.Default with { Enabled = false });
            Check(!disabled.Config.Enabled && disabled.Config.NextRunAt == 0,
                "disable must commit and clear the due deadline before admission resumes");
            releaseAdmission.TrySetResult();
            bool started = await admit.WaitAsync(TimeSpan.FromSeconds(5));
            await service.WaitForIdleAsync(new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);
            MapAutoScanSnapshot final = await service.GetSnapshotAsync();
            Check(!started && !final.Running && final.LastCycle is null &&
                  execution.StartAttempts.Count == 0,
                "old enabled/due observation must not launch even a ghost cycle after disable commits");
        }
        finally { DeleteRoot(root); }
    }

    private static async Task TerminalLastErrorContinuesAcrossServersAsync()
    {
        string root = TempRoot("terminal-error");
        try
        {
            var clock = new FakeClock(20_000);
            var execution = new FakeExecution { CurrentServerId = 7 };
            execution.TerminalErrors[11] = "server 11 terminal error";
            await using var service = Service(Path.Combine(root, "state.json"), execution, clock);

            await service.UpdateConfigAsync(MapAutoScanConfig.Default with
            {
                Enabled = true,
                ServerIds = new[] { 11, 12, 13 },
                IntervalMinutes = 20,
                ReturnToOriginalServer = true,
            });
            await service.WaitForIdleAsync();

            MapAutoScanSnapshot snapshot = await service.GetSnapshotAsync();
            Check(execution.StartAttempts.SequenceEqual(new[] { 11, 12, 13 }),
                "terminal lastError must not stop later Auto Scan targets");
            Check(execution.JumpAttempts.SequenceEqual(new[] { 11, 12, 13, 7 }),
                "Auto Scan must preserve target order and return to the original server");
            Check(snapshot.LastCycle?.Outcome == "completed_with_error" &&
                  snapshot.LastCycle.CompletedServerIds.SequenceEqual(new[] { 11, 12, 13 }) &&
                  snapshot.LastError == "server 11 terminal error" &&
                  snapshot.Config.NextRunAt == clock.Now + 20 * 60_000L,
                "terminal lastError must be persisted while a completed cycle schedules from final now");
        }
        finally { DeleteRoot(root); }
    }

    private static async Task OrdinaryEditsPreserveCompletedDeadlineAsync()
    {
        string root = TempRoot("deadline-owner");
        try
        {
            var clock = new FakeClock(10_000);
            var execution = new FakeExecution { CurrentServerId = 7 };
            await using var service = Service(Path.Combine(root, "state.json"), execution, clock);
            await service.UpdateConfigAsync(MapAutoScanConfig.Default with
            {
                Enabled = true,
                IntervalMinutes = 20,
                ServerIds = new[] { 11 },
                ReturnToOriginalServer = false,
            });
            await service.WaitForIdleAsync();

            MapAutoScanSnapshot completed = await service.GetSnapshotAsync();
            long completedDeadline = completed.Config.NextRunAt;
            long completedRevision = completed.Revision;
            int startCount = execution.StartAttempts.Count;
            Check(completedDeadline > clock.Now,
                "completed Auto cycle must own a future native deadline");

            MapAutoScanSnapshot edited = await service.UpdateConfigAsync(completed.Config with
            {
                IntervalMinutes = 45,
                NextRunAt = 0,
            });
            Check(edited.Config.IntervalMinutes == 45 && edited.Config.NextRunAt == completedDeadline,
                "ordinary config edit must retain the native deadline even when the browser carries stale nextRunAt");
            Check(edited.Revision > completedRevision && execution.StartAttempts.Count == startCount,
                "ordinary edit must advance native revision without starting an unintended extra cycle");
        }
        finally { DeleteRoot(root); }
    }

    private static async Task PersistenceFailureDoesNotMutateLiveStateAsync()
    {
        string root = TempRoot("persist-failure");
        try
        {
            string blockedParent = Path.Combine(root, "blocked-parent");
            File.WriteAllText(blockedParent, "not-a-directory");
            string statePath = Path.Combine(blockedParent, "state.json");
            var clock = new FakeClock(30_000);
            var execution = new FakeExecution { Online = false, CurrentServerId = 7 };

            await using (var service = Service(statePath, execution, clock))
            {
                MapAutoScanSnapshot before = await service.GetSnapshotAsync();
                bool failed = false;
                try
                {
                    _ = await service.UpdateConfigAsync(MapAutoScanConfig.Default with
                    {
                        Enabled = true,
                        IntervalMinutes = 45,
                        ServerIds = new[] { 12 },
                    });
                }
                catch (IOException)
                {
                    failed = true;
                }
                catch (UnauthorizedAccessException)
                {
                    failed = true;
                }

                MapAutoScanSnapshot after = await service.GetSnapshotAsync();
                Check(failed,
                    "Auto config persistence failure must be observable to the caller");
                Check(after.Config == before.Config && after.Revision == before.Revision,
                    "failed persistence must not publish or retain an unsaved in-memory config");
            }

            File.Delete(blockedParent);
            Directory.CreateDirectory(blockedParent);
            await using var reopened = Service(statePath, execution, clock);
            MapAutoScanSnapshot disk = await reopened.GetSnapshotAsync();
            Check(!disk.Config.Enabled && disk.Config.IntervalMinutes == 60 && disk.Config.ServerIds!.Count == 0,
                "reopen after failed persistence must observe the last committed config only");
        }
        finally { DeleteRoot(root); }
    }

    private static async Task ThrownTargetFailuresAbortRemainingServersAsync()
    {
        foreach (string failure in new[] { "jump", "start", "status" })
        {
            string root = TempRoot("throw-" + failure);
            try
            {
                var clock = new FakeClock(40_000);
                var execution = new FakeExecution { CurrentServerId = 7 };
                if (failure == "jump") execution.ThrowJumpServers.Add(12);
                if (failure == "start") execution.ThrowStartServers.Add(12);
                if (failure == "status") execution.ThrowStatusServers.Add(12);

                await using var service = Service(Path.Combine(root, "state.json"), execution, clock);
                await service.UpdateConfigAsync(MapAutoScanConfig.Default with
                {
                    Enabled = true,
                    ServerIds = new[] { 11, 12, 13 },
                    IntervalMinutes = 20,
                    ReturnToOriginalServer = true,
                });
                await service.WaitForIdleAsync();

                MapAutoScanSnapshot snapshot = await service.GetSnapshotAsync();
                Check(snapshot.LastCycle?.Outcome == "failed" && snapshot.LastError == failure + " failure on 12",
                    $"thrown {failure} failure must be the persisted cycle failure");
                Check(!execution.StartAttempts.Contains(13) && !execution.JumpAttempts.Contains(13),
                    $"thrown {failure} failure must abort later targets");
                Check(snapshot.LastCycle!.CompletedServerIds.SequenceEqual(new[] { 11 }),
                    $"thrown {failure} failure must retain only targets completed before the throw");
                Check(execution.JumpAttempts.Last() == 7,
                    $"thrown {failure} failure must still attempt recovered return-to-origin finalization");

                if (failure == "status")
                {
                    Check(snapshot.OwnsActiveScan,
                        "a thrown status wait must retain Auto ownership because the accepted scan has no terminal observation");
                    await service.UpdateConfigAsync(snapshot.Config with { Enabled = false });
                    Check(execution.StopCalls == 0,
                        "original Auto has no Stop: disabling after a thrown status wait must not stop the scan");
                }
            }
            finally { DeleteRoot(root); }
        }
    }

    // Original index-BVfnK1wp.js Di(): explicit servers are used even when the
    // current server id is 0; only an EMPTY resolved list fails with
    // MAP_AUTO_SCAN_SERVER_UNAVAILABLE. Return-home needs a positive original id.
    private static async Task ExplicitTargetsRunWhenCurrentServerIsUnknownAsync()
    {
        string root = TempRoot("explicit-unknown-current");
        try
        {
            var clock = new FakeClock(50_000);
            var explicitRun = new FakeExecution { CurrentServerId = 0 };
            await using (var service = Service(Path.Combine(root, "explicit.json"), explicitRun, clock))
            {
                await service.UpdateConfigAsync(MapAutoScanConfig.Default with
                {
                    Enabled = true,
                    ServerIds = new[] { 71, 72 },
                    ReturnToOriginalServer = true,
                });
                await service.WaitForIdleAsync();
                MapAutoScanSnapshot snapshot = await service.GetSnapshotAsync();
                Check(explicitRun.StartAttempts.SequenceEqual(new[] { 71, 72 }) &&
                      snapshot.LastCycle?.Outcome == "completed" && snapshot.LastError is null,
                    "explicit Auto targets must scan even when the current server id is 0");
                Check(!explicitRun.JumpAttempts.Contains(0),
                    "return-home must be skipped when the original server id is not positive");
            }

            var emptyRun = new FakeExecution { CurrentServerId = 0 };
            await using (var service = Service(Path.Combine(root, "empty.json"), emptyRun, clock))
            {
                await service.UpdateConfigAsync(MapAutoScanConfig.Default with { Enabled = true });
                await service.WaitForIdleAsync();
                MapAutoScanSnapshot snapshot = await service.GetSnapshotAsync();
                Check(emptyRun.StartAttempts.Count == 0 && snapshot.LastCycle?.Outcome == "failed" &&
                      snapshot.LastError == "MAP_AUTO_SCAN_SERVER_UNAVAILABLE",
                    "empty resolved target list must fail with MAP_AUTO_SCAN_SERVER_UNAVAILABLE");
            }
        }
        finally { DeleteRoot(root); }
    }

    private static async Task TimeoutStopsOnlyTheOwnedAutoScanAsync()
    {
        string root = TempRoot("timeout");
        try
        {
            var clock = new FakeClock(60_000);
            var execution = new FakeExecution { CurrentServerId = 8 };
            execution.NeverCompleteServers.Add(21);
            Task FastForwardDelay(TimeSpan delay, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                Check(delay == TimeSpan.FromMilliseconds(MapAutoScanCommandService.ScanPollMilliseconds),
                    "cycle must poll scan completion at the recovered two-second cadence");
                clock.Advance(MapAutoScanCommandService.ScanTimeoutMilliseconds);
                return Task.CompletedTask;
            }

            await using var service = Service(Path.Combine(root, "state.json"), execution, clock, FastForwardDelay);
            await service.UpdateConfigAsync(MapAutoScanConfig.Default with
            {
                Enabled = true,
                ServerIds = new[] { 21, 22 },
                ReturnToOriginalServer = false,
            });
            await service.WaitForIdleAsync();

            MapAutoScanSnapshot snapshot = await service.GetSnapshotAsync();
            Check(execution.StopCalls == 0 && !execution.StartAttempts.Contains(22),
                "original 45-minute Auto timeout issues no Stop (scan keeps running) and aborts later targets");
            Check(snapshot.LastCycle?.Outcome == "failed" &&
                  snapshot.LastError == "MAP_AUTO_SCAN_TIMEOUT" && !snapshot.OwnsActiveScan,
                "Auto timeout must persist the original MAP_AUTO_SCAN_TIMEOUT error and release Auto ownership marker");
        }
        finally { DeleteRoot(root); }
    }

    private static async Task DisableStopsOwnedAutoScanButNeverManualScanAsync()
    {
        string root = TempRoot("disable");
        try
        {
            var clock = new FakeClock(80_000);
            var execution = new FakeExecution { CurrentServerId = 9 };
            var pollRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Task BlockingDelay(TimeSpan delay, CancellationToken token) => pollRelease.Task.WaitAsync(token);

            await using (var service = Service(Path.Combine(root, "auto.json"), execution, clock, BlockingDelay))
            {
                await service.UpdateConfigAsync(MapAutoScanConfig.Default with
                {
                    Enabled = true,
                    ServerIds = new[] { 31, 32 },
                    ReturnToOriginalServer = false,
                });
                await execution.ScanStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
                MapAutoScanSnapshot beforeDisable = await service.GetSnapshotAsync();
                Check(beforeDisable.OwnsActiveScan, "accepted Auto scan must record Auto ownership");

                await service.UpdateConfigAsync(beforeDisable.Config with { Enabled = false });
                MapAutoScanSnapshot mid = await service.GetSnapshotAsync();
                Check(execution.StopCalls == 0 && mid.OwnsActiveScan && mid.Config.NextRunAt == 0,
                    "original disable only clears the deadline: the in-flight Auto scan is NOT stopped");
                pollRelease.TrySetResult();
                await service.WaitForIdleAsync();
                MapAutoScanSnapshot disabled = await service.GetSnapshotAsync();
                Check(execution.StopCalls == 0 && disabled.Config.NextRunAt == 0 &&
                      disabled.LastCycle?.Outcome == "disabled" && !disabled.OwnsActiveScan &&
                      execution.StartAttempts.SequenceEqual(new[] { 31 }) &&
                      disabled.LastCycle.CompletedServerIds.SequenceEqual(new[] { 31 }),
                    "original loop observes enabled BETWEEN targets: first scan completes, second never starts");
            }

            var manual = new FakeExecution { CurrentServerId = 9, ManualScanActive = true };
            await using var manualService = Service(Path.Combine(root, "manual.json"), manual, clock);
            await manualService.UpdateConfigAsync(MapAutoScanConfig.Default with { Enabled = true });
            await manualService.UpdateConfigAsync((await manualService.GetSnapshotAsync()).Config with { Enabled = false });
            Check(manual.StopCalls == 0,
                "disable must never route Stop to a manual scan the Auto service did not start");
        }
        finally { DeleteRoot(root); }
    }

    private static async Task AdmissionIsSerializedAsync()
    {
        string root = TempRoot("serialized");
        try
        {
            var clock = new FakeClock(100_000);
            var execution = new FakeExecution { CurrentServerId = 10 };
            var pollRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Task BlockingDelay(TimeSpan delay, CancellationToken token) => pollRelease.Task.WaitAsync(token);
            await using var service = Service(Path.Combine(root, "state.json"), execution, clock, BlockingDelay);

            await service.UpdateConfigAsync(MapAutoScanConfig.Default with
            {
                Enabled = true,
                ServerIds = new[] { 41 },
                ReturnToOriginalServer = false,
            });
            await execution.ScanStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

            await Task.WhenAll(
                service.RunNowAsync(),
                service.RunNowAsync(),
                service.CheckDueAsync(),
                service.CheckDueAsync());
            Check(execution.StartAttempts.Count == 1,
                "serialized admission must reject overlapping Run Now/due checks while an Auto cycle is active");

            await service.UpdateConfigAsync((await service.GetSnapshotAsync()).Config with { Enabled = false });
            pollRelease.TrySetResult(); // disable no longer stops the scan; let the cycle finish
            await service.WaitForIdleAsync();
        }
        finally { DeleteRoot(root); }
    }

    private static async Task StaleAutoOwnerCannotStopReplacementManualScanAsync()
    {
        string root = TempRoot("stale-owner-manual");
        try
        {
            var clock = new FakeClock(90_000);
            var execution = new FakeExecution { CurrentServerId = 9 };
            var pollEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var pollRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task BlockingPoll(TimeSpan delay, CancellationToken token)
            {
                Check(delay == TimeSpan.FromMilliseconds(MapAutoScanCommandService.ScanPollMilliseconds),
                    "stale-owner race must pause on the first Auto completion poll");
                pollEntered.TrySetResult();
                await pollRelease.Task.WaitAsync(token).ConfigureAwait(false);
            }

            await using var service = Service(Path.Combine(root, "state.json"), execution, clock, BlockingPoll);
            await service.UpdateConfigAsync(MapAutoScanConfig.Default with
            {
                Enabled = true,
                ServerIds = new[] { 31 },
                ReturnToOriginalServer = false,
            });
            await execution.ScanStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await pollEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));

            string autoRunId = execution.CurrentRunId ??
                throw new InvalidOperationException("Auto target did not expose its run owner token");
            execution.ReplaceCompletedAutoWithManual("manual-M");

            MapAutoScanSnapshot beforeDisable = await service.GetSnapshotAsync();
            Check(beforeDisable.OwnsActiveScan,
                "Auto must still hold its stale A token until the completion poll retires it");
            await service.UpdateConfigAsync(beforeDisable.Config with { Enabled = false });
            pollRelease.TrySetResult();
            await service.WaitForIdleAsync();

            Check(execution.ManualScanActive && execution.CurrentRunId == "manual-M" &&
                  execution.StopCalls == 0 && execution.StopAttempts.Count == 0,
                "disabling stale Auto A must issue no Stop at all and leave replacement Manual M running");
            Check(!(await service.GetSnapshotAsync()).OwnsActiveScan,
                "stale Auto owner token must retire when its completion poll observes A is no longer reading");
        }
        finally { DeleteRoot(root); }
    }

    private static async Task RealMap317StaleAutoOwnerCannotStopReplacementManualScanAsync()
    {
        string root = TempRoot("real-map317-stale-owner-manual");
        try
        {
            var clock = new FakeClock(92_000);
            var providers = new ControlledMap317Providers(initialServerId: 31);
            using var map = new Map317CommandService(
                Path.Combine(root, "map-data.db"),
                providers.MapProvider,
                providers.ActionProvider,
                startPlunderWorkers: false);
            var pollEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var staleRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task BlockingPoll(TimeSpan delay, CancellationToken token)
            {
                Check(delay == TimeSpan.FromMilliseconds(MapAutoScanCommandService.ScanPollMilliseconds),
                    "real Map317 replacement race must pause on the first Auto completion poll");
                pollEntered.TrySetResult();
                await staleRelease.Task.WaitAsync(token).ConfigureAwait(false);
            }

            await using var auto = new MapAutoScanCommandService(
                Path.Combine(root, "auto-state.json"),
                new MapAutoScanExecutionBoundary
                {
                    IsOnline = () => true,
                    IsMapScanActive = () => map.IsScanActive,
                    ReadStatusAsync = map.ReadAutoScanStatusAsync,
                    StartTargetScanAsync = map.StartAutoScanTargetAsync,
                    ReturnServerAsync = map.ReturnAutoScanToServerAsync,
                    StopScanIfOwnedAsync = map.StopAutoScanIfOwnedAsync,
                },
                new MapAutoScanSchedulerHooks
                {
                    UtcNowMilliseconds = () => clock.Now,
                    DelayAsync = BlockingPoll,
                },
                startScheduler: false);

            await auto.UpdateConfigAsync(MapAutoScanConfig.Default with
            {
                Enabled = true,
                ServerIds = new[] { 31 },
                SelectedTypes = new[] { "city" },
                ScanMode = "fast",
                ReturnToOriginalServer = false,
            });
            await pollEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));

            Check(providers.StartRequests.Count == 1 && map.IsScanActive,
                "real Map317 Auto A must own the accepted native scan before replacement");
            string autoRunId = providers.StartRequests.Single().ScanRunId;

            providers.CompleteRun(autoRunId);
            Check(!map.IsScanActive,
                "controlled provider completion must retire Auto A native scan ownership before Manual M starts");

            object? rawManual = await map.InvokeAsync(
                "map_scan_start",
                JsonSerializer.SerializeToElement(new
                {
                    selectedTypes = new[] { "city" },
                    scanMode = "fast",
                    resume = false,
                }, JsonOptions.Default),
                CancellationToken.None);
            JsonElement manual = JsonSerializer.SerializeToElement(rawManual, JsonOptions.Default);
            string manualRunId = manual.GetProperty("scanRunId").GetString() ??
                throw new InvalidOperationException("real Map317 Manual M did not expose a scan owner token");
            Check(manualRunId != autoRunId && map.IsScanActive,
                "Manual M must replace completed Auto A with a distinct active native run");

            MapAutoScanSnapshot beforeDisable = await auto.GetSnapshotAsync();
            Check(beforeDisable.OwnsActiveScan,
                "Auto scheduler must still retain stale A ownership until its blocked completion poll resumes");
            await auto.UpdateConfigAsync(beforeDisable.Config with { Enabled = false });
            staleRelease.TrySetResult();
            await auto.WaitForIdleAsync(new CancellationTokenSource(TimeSpan.FromSeconds(2)).Token);

            MapAutoScanRuntimeStatus manualStatus =
                await map.ReadAutoScanStatusAsync(null, CancellationToken.None);
            Check(manualStatus.IsReading && manualStatus.ScanRunId == manualRunId && map.IsScanActive,
                "disabling stale Auto A must leave replacement Manual M current and reading in the real Map317 service");
            Check(providers.StopCalls == 0,
                "original Auto never stops: disabling stale Auto A must not reach the provider stop boundary");
            Check(!(await auto.GetSnapshotAsync()).OwnsActiveScan,
                "real Map317 stale Auto A token must retire when its completion poll observes A is finished");

            _ = await map.InvokeAsync(
                "map_scan_stop",
                JsonSerializer.SerializeToElement(new { }, JsonOptions.Default),
                CancellationToken.None);
        }
        finally { DeleteRoot(root); }
    }

    private static async Task RealMap317MultiServerCompletionReturnsOriginAsync()
    {
        string root = TempRoot("real-map317-multiserver");
        try
        {
            var clock = new FakeClock(93_000);
            string databasePath = Path.Combine(root, "map-data.db");
            string statePath = Path.Combine(root, "auto-state.json");
            string[] runIds;
            var providers = new ControlledMap317Providers(initialServerId: 30, publishCityOnComplete: true);
            using (var map = new Map317CommandService(
                databasePath,
                providers.MapProvider,
                providers.ActionProvider,
                startPlunderWorkers: false))
            {
                int completedPolls = 0;
                Task CompleteCurrentRun(TimeSpan delay, CancellationToken token)
                {
                    token.ThrowIfCancellationRequested();
                    Check(delay == TimeSpan.FromMilliseconds(MapAutoScanCommandService.ScanPollMilliseconds),
                        "real Map317 positive cycle must poll each accepted target at the recovered cadence");
                    string runId = providers.ActiveRunId ??
                        throw new InvalidOperationException("real Map317 positive cycle has no active provider run to complete");
                    providers.CompleteRun(runId);
                    completedPolls++;
                    return Task.CompletedTask;
                }

                await using var auto = RealMapAutoService(statePath, map, clock, CompleteCurrentRun);
                await auto.UpdateConfigAsync(MapAutoScanConfig.Default with
                {
                    Enabled = true,
                    IntervalMinutes = 20,
                    ServerIds = new[] { 31, 32 },
                    SelectedTypes = new[] { "city", "treasure" },
                    ScanMode = "fast",
                    ReturnToOriginalServer = true,
                });
                await auto.WaitForIdleAsync(new CancellationTokenSource(TimeSpan.FromSeconds(2)).Token);

                MapAutoScanSnapshot snapshot = await auto.GetSnapshotAsync();
                runIds = providers.StartRequests.Select(request => request.ScanRunId).ToArray();
                Check(completedPolls == 2 && providers.StartRequests.Select(request => request.ServerId).SequenceEqual(new[] { 31, 32 }) &&
                      runIds.Distinct(StringComparer.Ordinal).Count() == 2,
                    "real Map317 positive cycle must own a distinct accepted scan run for each target in configured order");
                Check(providers.TerminatedRunIds.SequenceEqual(runIds) && providers.StopCalls == 0 && !map.IsScanActive,
                    "real Map317 positive completion must terminally release each exact run without routing provider Stop");
                Check(providers.JumpAttempts.SequenceEqual(new[] { 31, 32, 30 }) && providers.CurrentServerId == 30,
                    "real Map317 positive multi-server cycle must return to its original server after both exact target owners terminate");
                Check(snapshot.LastCycle?.Outcome == "completed" &&
                      snapshot.LastCycle.OriginalServerId == 30 &&
                      snapshot.LastCycle.TargetServerIds.SequenceEqual(new[] { 31, 32 }) &&
                      snapshot.LastCycle.CompletedServerIds.SequenceEqual(new[] { 31, 32 }) &&
                      !snapshot.OwnsActiveScan,
                    "real Map317 positive multi-server cycle must persist both completed target owners and retire Auto ownership");
            }

            var reopenedProviders = new ControlledMap317Providers(initialServerId: 30);
            using var reopenedMap = new Map317CommandService(
                databasePath,
                reopenedProviders.MapProvider,
                reopenedProviders.ActionProvider,
                startPlunderWorkers: false);
            await using var reopenedAuto = RealMapAutoService(
                statePath,
                reopenedMap,
                clock,
                (delay, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    return Task.CompletedTask;
                });
            MapAutoScanSnapshot persisted = await reopenedAuto.GetSnapshotAsync();
            Check(persisted.Config.Enabled && persisted.Config.IntervalMinutes == 20 &&
                  persisted.Config.ServerIds!.SequenceEqual(new[] { 31, 32 }) &&
                  persisted.Config.SelectedTypes!.SequenceEqual(new[] { "city", "treasure" }) &&
                  persisted.Config.ReturnToOriginalServer && persisted.LastCycle?.Outcome == "completed" &&
                  persisted.LastCycle.CompletedServerIds.SequenceEqual(new[] { 31, 32 }),
                "reopened Auto state must preserve the completed multi-server config/cycle without reseeding");
            foreach (int serverId in new[] { 31, 32 })
            {
                JsonElement search = JsonSerializer.SerializeToElement(
                    await reopenedMap.InvokeAsync(
                        "map_search",
                        JsonSerializer.SerializeToElement(new
                        {
                            kind = "city",
                            query = new { serverId, page = 1, pageSize = 50 },
                        }, JsonOptions.Default),
                        CancellationToken.None),
                    JsonOptions.Default);
                Check(search.GetProperty("total").GetInt32() == 1 &&
                      search.GetProperty("rows")[0].GetProperty("recordKey").GetString() == $"auto-city-{serverId}",
                    $"reopened Map store must preserve the provider-published server {serverId} Auto row without reseeding");
            }
        }
        finally { DeleteRoot(root); }
    }

    private static async Task RealMap317CancelStopsExactOwnedScanAsync()
    {
        string root = TempRoot("real-map317-cancel");
        try
        {
            var clock = new FakeClock(94_000);
            var providers = new ControlledMap317Providers(initialServerId: 40);
            using var map = new Map317CommandService(
                Path.Combine(root, "map-data.db"),
                providers.MapProvider,
                providers.ActionProvider,
                startPlunderWorkers: false);
            var pollEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task BlockingPoll(TimeSpan delay, CancellationToken token)
            {
                Check(delay == TimeSpan.FromMilliseconds(MapAutoScanCommandService.ScanPollMilliseconds),
                    "real Map317 cancel must reach the accepted run completion poll before cancellation");
                pollEntered.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token).ConfigureAwait(false);
            }

            await using var auto = RealMapAutoService(
                Path.Combine(root, "auto-state.json"), map, clock, BlockingPoll);
            await auto.UpdateConfigAsync(MapAutoScanConfig.Default with
            {
                Enabled = true,
                IntervalMinutes = 30,
                ServerIds = new[] { 41 },
                SelectedTypes = new[] { "city" },
                ReturnToOriginalServer = false,
            });
            await pollEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            string runId = providers.ActiveRunId ??
                throw new InvalidOperationException("real Map317 cancel did not expose its accepted run owner");

            await auto.CancelAsync();
            MapAutoScanSnapshot snapshot = await auto.GetSnapshotAsync();
            Check(providers.StopCalls == 1 && providers.StoppedRunIds.SequenceEqual(new[] { runId }) &&
                  providers.TerminatedRunIds.SequenceEqual(new[] { runId }) && !map.IsScanActive,
                "explicit Auto cancel must stop and terminally release only the exact real Map317 run it owns");
            Check(snapshot.Config.Enabled && snapshot.LastCycle?.Outcome == "canceled" &&
                  snapshot.Config.NextRunAt == clock.Now + 30 * 60_000L && !snapshot.OwnsActiveScan,
                "explicit real Map317 cancel must keep Auto enabled, schedule the next cycle, and retire scan ownership");
        }
        finally { DeleteRoot(root); }
    }

    private static async Task RealMap317DisableStopsExactOwnedScanAsync()
    {
        string root = TempRoot("real-map317-disable");
        try
        {
            var clock = new FakeClock(95_000);
            var providers = new ControlledMap317Providers(initialServerId: 42);
            using var map = new Map317CommandService(
                Path.Combine(root, "map-data.db"),
                providers.MapProvider,
                providers.ActionProvider,
                startPlunderWorkers: false);
            var pollEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var disableRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task BlockingPoll(TimeSpan delay, CancellationToken token)
            {
                Check(delay == TimeSpan.FromMilliseconds(MapAutoScanCommandService.ScanPollMilliseconds),
                    "real Map317 disable must reach the accepted run completion poll before disabling");
                pollEntered.TrySetResult();
                await disableRelease.Task.WaitAsync(token).ConfigureAwait(false);
            }

            await using var auto = RealMapAutoService(
                Path.Combine(root, "auto-state.json"), map, clock, BlockingPoll);
            await auto.UpdateConfigAsync(MapAutoScanConfig.Default with
            {
                Enabled = true,
                ServerIds = new[] { 43, 44 },
                SelectedTypes = new[] { "city" },
                ReturnToOriginalServer = false,
            });
            await pollEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            string runId = providers.ActiveRunId ??
                throw new InvalidOperationException("real Map317 disable did not expose its accepted run owner");

            MapAutoScanSnapshot beforeDisable = await auto.GetSnapshotAsync();
            await auto.UpdateConfigAsync(beforeDisable.Config with { Enabled = false });
            Check(providers.StopCalls == 0 && map.IsScanActive && (await auto.GetSnapshotAsync()).OwnsActiveScan,
                "original disable must not stop the in-flight real Map317 scan");
            providers.CompleteRun(runId);
            disableRelease.TrySetResult();
            await auto.WaitForIdleAsync(new CancellationTokenSource(TimeSpan.FromSeconds(2)).Token);
            MapAutoScanSnapshot snapshot = await auto.GetSnapshotAsync();
            Check(providers.StopCalls == 0 && providers.StartRequests.Count == 1 &&
                  providers.StartRequests.Single().ServerId == 43 && !map.IsScanActive,
                "disabled Auto lets its accepted real Map317 run finish and starts no later target");
            Check(!snapshot.Config.Enabled && snapshot.Config.NextRunAt == 0 &&
                  snapshot.LastCycle?.Outcome == "disabled" && !snapshot.OwnsActiveScan,
                "real Map317 disable must persist disabled terminal state without retaining scan ownership");
        }
        finally { DeleteRoot(root); }
    }

    private static async Task RealMap317TimeoutStopsExactOwnedScanAsync()
    {
        string root = TempRoot("real-map317-timeout");
        try
        {
            var clock = new FakeClock(96_000);
            var providers = new ControlledMap317Providers(initialServerId: 50);
            using var map = new Map317CommandService(
                Path.Combine(root, "map-data.db"),
                providers.MapProvider,
                providers.ActionProvider,
                startPlunderWorkers: false);
            Task FastForwardTimeout(TimeSpan delay, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                Check(delay == TimeSpan.FromMilliseconds(MapAutoScanCommandService.ScanPollMilliseconds),
                    "real Map317 timeout must use the recovered two-second completion poll");
                clock.Advance(MapAutoScanCommandService.ScanTimeoutMilliseconds);
                return Task.CompletedTask;
            }

            await using var auto = RealMapAutoService(
                Path.Combine(root, "auto-state.json"), map, clock, FastForwardTimeout);
            await auto.UpdateConfigAsync(MapAutoScanConfig.Default with
            {
                Enabled = true,
                ServerIds = new[] { 51, 52 },
                SelectedTypes = new[] { "city" },
                ReturnToOriginalServer = false,
            });
            await auto.WaitForIdleAsync(new CancellationTokenSource(TimeSpan.FromSeconds(2)).Token);

            MapAutoScanSnapshot snapshot = await auto.GetSnapshotAsync();
            string runId = providers.StartRequests.Single().ScanRunId;
            Check(providers.StartRequests.Single().ServerId == 51 && providers.StopCalls == 0 &&
                  map.IsScanActive && providers.ActiveRunId == runId,
                "original Auto timeout issues no Stop: the real Map317 first-target run keeps running");
            Check(snapshot.LastCycle?.Outcome == "failed" && snapshot.LastError == "MAP_AUTO_SCAN_TIMEOUT" &&
                  snapshot.LastCycle.CompletedServerIds.Count == 0 && !snapshot.OwnsActiveScan,
                "real Map317 timeout must persist the recovered timeout and abort before a later target starts");
        }
        finally { DeleteRoot(root); }
    }

    private static async Task RealMap317RetirementStopsExactOwnedScanAsync()
    {
        string root = TempRoot("real-map317-retire");
        try
        {
            var clock = new FakeClock(97_000);
            var providers = new ControlledMap317Providers(initialServerId: 60);
            using var map = new Map317CommandService(
                Path.Combine(root, "map-data.db"),
                providers.MapProvider,
                providers.ActionProvider,
                startPlunderWorkers: false);
            var pollEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task BlockingPoll(TimeSpan delay, CancellationToken token)
            {
                Check(delay == TimeSpan.FromMilliseconds(MapAutoScanCommandService.ScanPollMilliseconds),
                    "real Map317 retirement must own a running target before profile retirement");
                pollEntered.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token).ConfigureAwait(false);
            }

            await using var auto = RealMapAutoService(
                Path.Combine(root, "auto-state.json"), map, clock, BlockingPoll);
            await auto.UpdateConfigAsync(MapAutoScanConfig.Default with
            {
                Enabled = true,
                ServerIds = new[] { 61 },
                SelectedTypes = new[] { "city" },
                ReturnToOriginalServer = true,
            });
            await pollEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            string runId = providers.ActiveRunId ??
                throw new InvalidOperationException("real Map317 retirement did not expose its active Auto run");
            long dueBeforeRetire = (await auto.GetSnapshotAsync()).Config.NextRunAt;

            await auto.RetireAsync();
            MapAutoScanSnapshot snapshot = await auto.GetSnapshotAsync();
            Check(providers.StopCalls == 1 && providers.StoppedRunIds.SequenceEqual(new[] { runId }) &&
                  providers.TerminatedRunIds.SequenceEqual(new[] { runId }) && !map.IsScanActive,
                "retiring Auto must stop and terminally release only its exact active real Map317 owner");
            Check(snapshot.Config.Enabled && snapshot.Config.NextRunAt == dueBeforeRetire &&
                  snapshot.LastCycle?.Outcome == "retired" && !snapshot.OwnsActiveScan,
                "real Map317 retirement must preserve the enabled due deadline while persisting retired terminal state");
            Check(providers.JumpAttempts.SequenceEqual(new[] { 61 }) && providers.CurrentServerId == 61,
                "retired Auto ownership must not perform a post-retirement return-to-origin movement");
            await ExpectAsync<ObjectDisposedException>(() => auto.CheckDueAsync(),
                "retired real Map317 Auto owner must reject new scheduler admission");
        }
        finally { DeleteRoot(root); }
    }

    private static async Task RealMap317BetweenTargetManualOwnershipCannotBeMovedAsync()
    {
        string root = TempRoot("real-map317-between-target-manual");
        try
        {
            var clock = new FakeClock(98_000);
            var providers = new ControlledMap317Providers(initialServerId: 70);
            using var map = new Map317CommandService(
                Path.Combine(root, "map-data.db"),
                providers.MapProvider,
                providers.ActionProvider,
                startPlunderWorkers: false);
            string? firstAutoRunId = null;
            string? manualRunId = null;
            int pollCount = 0;
            async Task ReplaceFirstAutoWithManual(TimeSpan delay, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                Check(delay == TimeSpan.FromMilliseconds(MapAutoScanCommandService.ScanPollMilliseconds),
                    "real Map317 between-target barrier must pause after the first accepted Auto target");
                Check(Interlocked.Increment(ref pollCount) == 1,
                    "manual replacement must prevent Auto from reaching a second target completion poll");
                firstAutoRunId = providers.ActiveRunId ??
                    throw new InvalidOperationException("first real Map317 Auto target has no active run owner");
                providers.CompleteRun(firstAutoRunId);
                manualRunId = await StartManualMapScanAsync(map).ConfigureAwait(false);
            }

            await using var auto = RealMapAutoService(
                Path.Combine(root, "auto-state.json"), map, clock, ReplaceFirstAutoWithManual);
            await auto.UpdateConfigAsync(MapAutoScanConfig.Default with
            {
                Enabled = true,
                ServerIds = new[] { 71, 72 },
                SelectedTypes = new[] { "city" },
                ReturnToOriginalServer = true,
            });
            await auto.WaitForIdleAsync(new CancellationTokenSource(TimeSpan.FromSeconds(2)).Token);

            MapAutoScanSnapshot snapshot = await auto.GetSnapshotAsync();
            Check(firstAutoRunId is not null && manualRunId is not null && firstAutoRunId != manualRunId,
                "between-target replacement must create a distinct manual run after first Auto completion");
            Check(providers.StartRequests.Count == 2 &&
                  providers.StartRequests[0].ServerId == 71 && providers.StartRequests[0].ScanRunId == firstAutoRunId &&
                  providers.StartRequests[1].ServerId == 71 && providers.StartRequests[1].ScanRunId == manualRunId,
                "real Map317 manual replacement must become the only active owner before Auto can start target 72");
            MapAutoScanRuntimeStatus manual = await map.ReadAutoScanStatusAsync(null, CancellationToken.None);
            Check(manual.IsReading && manual.ScanRunId == manualRunId && map.IsScanActive && providers.ActiveRunId == manualRunId,
                "manual replacement must remain the current real Map317 scan owner after Auto target admission fails");
            Check(providers.StopCalls == 0 && providers.JumpAttempts.SequenceEqual(new[] { 71 }) && providers.CurrentServerId == 71,
                "between-target manual ownership must block target 72 and return-to-origin before either can move or stop the manual run");
            Check(snapshot.LastCycle?.Outcome == "failed" &&
                  snapshot.LastCycle.CompletedServerIds.SequenceEqual(new[] { 71 }) && !snapshot.OwnsActiveScan,
                "Auto must retire its first target token and fail the cycle without claiming the replacement manual owner");

            _ = await map.InvokeAsync(
                "map_scan_stop",
                JsonSerializer.SerializeToElement(new { }, JsonOptions.Default),
                CancellationToken.None);
        }
        finally { DeleteRoot(root); }
    }

    private static async Task ManualOwnershipAfterAdmissionCannotBeMovedAsync()
    {
        string root = TempRoot("manual-after-admission");
        try
        {
            var clock = new FakeClock(95_000);
            var execution = new FakeExecution
            {
                CurrentServerId = 9,
                ActivateManualBeforeNextTarget = true,
            };
            await using var service = Service(Path.Combine(root, "state.json"), execution, clock);

            await service.UpdateConfigAsync(MapAutoScanConfig.Default with
            {
                Enabled = true,
                ServerIds = new[] { 31 },
                ReturnToOriginalServer = true,
            });
            await service.WaitForIdleAsync();

            MapAutoScanSnapshot snapshot = await service.GetSnapshotAsync();
            Check(execution.ManualScanActive && execution.CurrentServerId == 9 &&
                  execution.JumpAttempts.Count == 0 && execution.StartAttempts.Count == 0 &&
                  execution.StopCalls == 0,
                "manual ownership gained after due admission must block target and return transitions before any server movement");
            Check(snapshot.LastCycle?.Outcome == "failed" && !snapshot.OwnsActiveScan,
                "post-admission manual ownership must fail the Auto cycle without claiming or stopping the manual scan");
        }
        finally { DeleteRoot(root); }
    }

    private static async Task ExplicitCancelStopsOwnedScanAndSchedulesNextCycleAsync()
    {
        string root = TempRoot("cancel");
        try
        {
            var clock = new FakeClock(120_000);
            var execution = new FakeExecution { CurrentServerId = 10 };
            var pollRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Task BlockingDelay(TimeSpan delay, CancellationToken token) => pollRelease.Task.WaitAsync(token);
            await using var service = Service(Path.Combine(root, "state.json"), execution, clock, BlockingDelay);

            await service.UpdateConfigAsync(MapAutoScanConfig.Default with
            {
                Enabled = true,
                IntervalMinutes = 30,
                ServerIds = new[] { 51 },
                ReturnToOriginalServer = false,
            });
            await execution.ScanStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await service.CancelAsync();

            MapAutoScanSnapshot snapshot = await service.GetSnapshotAsync();
            Check(execution.StopCalls == 1 && snapshot.Config.Enabled &&
                  snapshot.LastCycle?.Outcome == "canceled" &&
                  snapshot.Config.NextRunAt == clock.Now + 30 * 60_000L,
                "explicit cancel must stop only owned work while leaving Auto enabled for its next interval");
        }
        finally { DeleteRoot(root); }
    }

    private static async Task RestartStateIsReconciledWithoutMovingTheDeadlineAsync()
    {
        string root = TempRoot("restart");
        try
        {
            string path = Path.Combine(root, "state.json");
            Directory.CreateDirectory(root);
            File.WriteAllText(path, JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                config = new
                {
                    enabled = true,
                    intervalMinutes = 60,
                    serverIds = new[] { 61, 62 },
                    selectedTypes = new[] { "truck" },
                    scanMode = "fast",
                    returnToOriginalServer = true,
                    nextRunAt = 1234L,
                },
                lastError = (string?)null,
                lastCycle = (object?)null,
                recoveredInterruptedCycle = false,
                restart = new
                {
                    active = true,
                    startedAt = 1000L,
                    originalServerId = 60,
                    targetServerIds = new[] { 61, 62 },
                    nextTargetIndex = 1,
                    lastError = (string?)null,
                },
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

            var clock = new FakeClock(9_000);
            var execution = new FakeExecution { Online = false, CurrentServerId = 60 };
            await using var service = Service(path, execution, clock);
            MapAutoScanSnapshot snapshot = await service.GetSnapshotAsync();
            Check(snapshot.RecoveredInterruptedCycle && snapshot.LastCycle?.Outcome == "interrupted" &&
                  snapshot.LastCycle.StartedAt == 1000 && snapshot.LastCycle.CompletedAt == 9000 &&
                  snapshot.LastCycle.CompletedServerIds.SequenceEqual(new[] { 61 }) &&
                  snapshot.LastError == "Auto scan interrupted by restart" && snapshot.Config.NextRunAt == 1234,
                "restart reconciliation must record interruption while preserving the already-persisted due deadline");

            string persisted = File.ReadAllText(path);
            Check(persisted.Contains("\"active\": false", StringComparison.Ordinal),
                "restart reconciliation must durably retire the stale active-cycle marker");
        }
        finally { DeleteRoot(root); }
    }

    private static async Task ProfilePathsStayIsolatedAsync()
    {
        string root = TempRoot("profiles");
        try
        {
            var clock = new FakeClock(150_000);
            var execution = new FakeExecution { Online = false, CurrentServerId = 1 };
            string pathA = Path.Combine(root, "a", "auto.json");
            string pathB = Path.Combine(root, "b", "auto.json");

            await using (var serviceA = Service(pathA, execution, clock))
                await serviceA.UpdateConfigAsync(MapAutoScanConfig.Default with { Enabled = true, ServerIds = new[] { 71 } });
            await using (var serviceB = Service(pathB, execution, clock))
                await serviceB.UpdateConfigAsync(MapAutoScanConfig.Default with { Enabled = true, ServerIds = new[] { 72 } });

            await using var reopenedA = Service(pathA, execution, clock);
            await using var reopenedB = Service(pathB, execution, clock);
            Check((await reopenedA.GetSnapshotAsync()).Config.ServerIds!.SequenceEqual(new[] { 71 }) &&
                  (await reopenedB.GetSnapshotAsync()).Config.ServerIds!.SequenceEqual(new[] { 72 }),
                "caller-supplied profile state paths must remain isolated");
        }
        finally { DeleteRoot(root); }
    }

    private static async Task RetirementStopsOwnedWorkAndLeavesDueDeadlineForRestartAsync()
    {
        string root = TempRoot("retire");
        try
        {
            var clock = new FakeClock(180_000);
            var execution = new FakeExecution { CurrentServerId = 10 };
            var pollRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Task BlockingDelay(TimeSpan delay, CancellationToken token) => pollRelease.Task.WaitAsync(token);
            string path = Path.Combine(root, "state.json");
            var service = Service(path, execution, clock, BlockingDelay);

            await service.UpdateConfigAsync(MapAutoScanConfig.Default with
            {
                Enabled = true,
                ServerIds = new[] { 81 },
                ReturnToOriginalServer = true,
            });
            await execution.ScanStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            long dueBeforeRetire = (await service.GetSnapshotAsync()).Config.NextRunAt;
            await service.RetireAsync();

            Check(execution.StopCalls == 1,
                "profile retirement must stop an Auto-owned active scan");
            await ExpectAsync<ObjectDisposedException>(() => service.CheckDueAsync(),
                "retired profile must reject new scheduler admission");
            await service.DisposeAsync();

            var offline = new FakeExecution { Online = false, CurrentServerId = 10 };
            await using var reopened = Service(path, offline, clock);
            MapAutoScanSnapshot snapshot = await reopened.GetSnapshotAsync();
            Check(snapshot.Config.Enabled && snapshot.Config.NextRunAt == dueBeforeRetire &&
                  snapshot.LastCycle?.Outcome == "retired",
                "profile retirement must persist completion state without advancing the enabled deadline");
        }
        finally { DeleteRoot(root); }
    }

    private static async Task RealSchedulerLoopAdmitsDueCycleAndRetiresAsync()
    {
        string root = TempRoot("scheduler-loop");
        string path = Path.Combine(root, "state.json");
        try
        {
            var clock = new FakeClock(7_005_000);
            var offline = new FakeExecution { Online = false, CurrentServerId = 9 };
            await using (var seed = Service(path, offline, clock))
            {
                _ = await seed.UpdateConfigAsync(MapAutoScanConfig.Default with
                {
                    Enabled = true,
                    ServerIds = new[] { 91 },
                    ReturnToOriginalServer = true,
                });
            }

            clock.Now = 7_000_000;

            var execution = new FakeExecution { Online = true, CurrentServerId = 9 };
            var firstTickEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseFirstTick = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            int schedulerDelays = 0;
            async Task Delay(TimeSpan value, CancellationToken token)
            {
                if ((long)value.TotalMilliseconds == MapAutoScanCommandService.DueTickMilliseconds)
                {
                    int occurrence = Interlocked.Increment(ref schedulerDelays);
                    if (occurrence == 1)
                    {
                        firstTickEntered.TrySetResult();
                        await releaseFirstTick.Task.WaitAsync(token).ConfigureAwait(false);
                        clock.Advance(MapAutoScanCommandService.DueTickMilliseconds);
                        return;
                    }
                    await Task.Delay(Timeout.InfiniteTimeSpan, token).ConfigureAwait(false);
                    return;
                }

                token.ThrowIfCancellationRequested();
                clock.Advance((long)value.TotalMilliseconds);
                await Task.Yield();
            }

            await using var service = Service(path, execution, clock, Delay, startScheduler: true);
            await firstTickEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Check(execution.StartAttempts.Count == 0,
                "production scheduler loop must not admit a cycle before its persisted due deadline");
            releaseFirstTick.TrySetResult();
            await execution.ScanStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await service.WaitForIdleAsync(new CancellationTokenSource(TimeSpan.FromSeconds(2)).Token);

            MapAutoScanSnapshot snapshot = await service.GetSnapshotAsync();
            Check(execution.StartAttempts.SequenceEqual(new[] { 91 }) &&
                  execution.CurrentServerId == 9 &&
                  snapshot.LastCycle?.Outcome == "completed" &&
                  schedulerDelays >= 2,
                "real scheduler loop must wake on its 5-second tick, run the due target, return origin, and continue owning its loop");

            await service.RetireAsync();
            Check(!snapshot.OwnsActiveScan && !execution.AutoScanActive,
                "scheduler retirement must leave no Auto-owned scan running");
        }
        finally { DeleteRoot(root); }
    }

    private static MapAutoScanCommandService Service(
        string path,
        FakeExecution execution,
        FakeClock clock,
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        bool startScheduler = false) =>
        new(
            path,
            execution.Boundary,
            new MapAutoScanSchedulerHooks
            {
                UtcNowMilliseconds = () => clock.Now,
                DelayAsync = delay ?? ((value, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    clock.Advance((long)value.TotalMilliseconds);
                    return Task.CompletedTask;
                }),
            },
            startScheduler: startScheduler);

    private static MapAutoScanCommandService RealMapAutoService(
        string path,
        Map317CommandService map,
        FakeClock clock,
        Func<TimeSpan, CancellationToken, Task> delay) =>
        new(
            path,
            new MapAutoScanExecutionBoundary
            {
                IsOnline = () => true,
                IsMapScanActive = () => map.IsScanActive,
                ReadStatusAsync = map.ReadAutoScanStatusAsync,
                StartTargetScanAsync = map.StartAutoScanTargetAsync,
                ReturnServerAsync = map.ReturnAutoScanToServerAsync,
                StopScanIfOwnedAsync = map.StopAutoScanIfOwnedAsync,
            },
            new MapAutoScanSchedulerHooks
            {
                UtcNowMilliseconds = () => clock.Now,
                DelayAsync = delay,
            },
            startScheduler: false);

    private static async Task<string> StartManualMapScanAsync(Map317CommandService map)
    {
        object? raw = await map.InvokeAsync(
            "map_scan_start",
            JsonSerializer.SerializeToElement(new
            {
                selectedTypes = new[] { "city" },
                scanMode = "fast",
                resume = false,
            }, JsonOptions.Default),
            CancellationToken.None).ConfigureAwait(false);
        JsonElement started = JsonSerializer.SerializeToElement(raw, JsonOptions.Default);
        return started.GetProperty("scanRunId").GetString() ??
            throw new InvalidOperationException("manual Map317 replacement did not expose a scan owner token");
    }

    private static async Task ExpectAsync<T>(Func<Task> action, string message) where T : Exception
    {
        try { await action(); }
        catch (T) { return; }
        throw new InvalidOperationException(message);
    }

    private static string TempRoot(string label)
    {
        string root = Path.Combine(Path.GetTempPath(), "lwb317-map-auto-" + label + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteRoot(string root)
    {
        try { Directory.Delete(root, recursive: true); } catch { }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class FakeClock(long now)
    {
        internal long Now { get; set; } = now;
        internal void Advance(long milliseconds) => Now = checked(Now + milliseconds);
    }

    private sealed class ControlledMap317Providers : IMap317RunScopedProvider
    {
        private Map317.MapControlPlane? activeControl;
        private string? activeRunId;
        private readonly bool publishCityOnComplete;

        internal ControlledMap317Providers(int initialServerId, bool publishCityOnComplete = false)
        {
            CurrentServerId = initialServerId;
            this.publishCityOnComplete = publishCityOnComplete;
            ActionProvider = new Map317.MapActionProviderAdapter
            {
                GotoWorldCoordinate = (_, _, _, _) => ValueTask.CompletedTask,
                GotoWorldMarch = (_, _, _) => ValueTask.CompletedTask,
                GetCurrentServerId = _ => ValueTask.FromResult(CurrentServerId),
                GotoServer = (serverId, _) =>
                {
                    JumpAttempts.Add(serverId);
                    CurrentServerId = serverId;
                    return ValueTask.CompletedTask;
                },
                InspectTreasureStates = (_, _, _) => ValueTask.FromResult(
                    new Map317.TreasureInspectionResult(string.Empty, string.Empty, Array.Empty<JsonElement>())),
                GetTreasureClaimStatus = _ => ValueTask.FromResult(JsonSerializer.SerializeToElement(new { })),
                ClaimTreasures = (_, _) => ValueTask.FromResult(JsonSerializer.SerializeToElement(new { })),
                ShareDispatchTaskToAlliance = (_, _) => ValueTask.FromResult(new Map317.DispatchShareProviderResult(false)),
                PrepareGhostPlunderTasks = (_, _) => ValueTask.FromResult<IReadOnlyList<JsonElement>>(Array.Empty<JsonElement>()),
                GetMapPlunderServerDayStart = _ => ValueTask.FromResult<Map317.MapPlunderServerDayProviderResult?>(null),
                ArmDispatchPlunder = (_, _) => ValueTask.FromResult<IReadOnlyList<Map317.DispatchPlunderArmResult>>(Array.Empty<Map317.DispatchPlunderArmResult>()),
                ArmTruckPlunder = (_, _) => ValueTask.FromResult(new Map317.TruckPlunderArmResult(false)),
                DrainDispatchPlunderResults = _ => ValueTask.FromResult<IReadOnlyList<Map317.DispatchPlunderResultEvent>>(Array.Empty<Map317.DispatchPlunderResultEvent>()),
                DrainTruckPlunderResults = _ => ValueTask.FromResult<IReadOnlyList<Map317.TruckPlunderResultEvent>>(Array.Empty<Map317.TruckPlunderResultEvent>()),
                ClearTruckPlunderPending = (_, _, _, _) => ValueTask.CompletedTask,
            };
        }

        internal int CurrentServerId { get; private set; }
        internal int StopCalls { get; private set; }
        internal string? ActiveRunId => activeRunId;
        internal List<int> JumpAttempts { get; } = [];
        internal List<Map317.MapProviderStartRequest> StartRequests { get; } = [];
        internal List<string> StoppedRunIds { get; } = [];
        internal List<string> TerminatedRunIds { get; } = [];
        internal Map317.IMapProvider MapProvider => this;
        internal Map317.MapActionProviderAdapter ActionProvider { get; }

        public event Action<string>? RunTerminated;

        public ValueTask<Map317.MapProviderContext> GetContextAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Context());
        }

        public ValueTask<Map317.MapProviderContext> EnterWorldMapAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Context());
        }

        public ValueTask<Map317.MapProviderStartResult> StartMapScanAsync(
            Map317.MapProviderStartRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StartRequests.Add(request);
            return ValueTask.FromResult(new Map317.MapProviderStartResult(
                Accepted: true,
                TotalBlocks: 1,
                NativeCaptureReady: true));
        }

        public ValueTask StopMapScanAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StopCalls++;
            string? runId = activeRunId;
            activeRunId = null;
            activeControl = null;
            if (runId is not null)
            {
                StoppedRunIds.Add(runId);
                TerminatedRunIds.Add(runId);
                RunTerminated?.Invoke(runId);
            }
            return ValueTask.CompletedTask;
        }

        public void ActivateAcceptedRun(Map317.MapControlPlane control, string scanRunId)
        {
            Check(activeRunId is null, "controlled Map317 provider must own at most one active run");
            Check(control.ScanState.IsReading && control.ScanState.ScanRunId == scanRunId,
                "controlled Map317 activation must match the accepted durable run");
            activeControl = control;
            activeRunId = scanRunId;
        }

        internal void CompleteRun(string expectedRunId)
        {
            Map317.MapControlPlane control = activeControl ??
                throw new InvalidOperationException("controlled completion requires an active Map317 control owner");
            Check(activeRunId == expectedRunId,
                "controlled completion must target the exact accepted Auto run");
            if (publishCityOnComplete)
                control.StageRecord(AutoCityRecord(CurrentServerId));
            Map317.MapScanState completed = control.CompleteScan();
            Check(!completed.IsReading && completed.ScanRunId == expectedRunId,
                "controlled provider completion must publish the exact Auto run");
            activeControl = null;
            activeRunId = null;
            TerminatedRunIds.Add(expectedRunId);
            RunTerminated?.Invoke(expectedRunId);
        }

        public void Dispose()
        {
            string? runId = activeRunId;
            activeControl = null;
            activeRunId = null;
            if (runId is not null)
            {
                TerminatedRunIds.Add(runId);
                RunTerminated?.Invoke(runId);
            }
        }

        private Map317.MapProviderContext Context() =>
            new(
                IsAvailable: true,
                IsInWorld: true,
                ServerId: CurrentServerId,
                ServerIdSource: "live",
                WorldId: 1,
                TileWidth: 100,
                TileHeight: 100,
                ExpectedTotalBlocks: 1);

        private static Map317.MapRecord AutoCityRecord(int serverId)
        {
            long updatedAt = 1_800_000_200_000L + serverId;
            string recordKey = $"auto-city-{serverId}";
            return new Map317.MapRecord(
                "city",
                serverId,
                recordKey,
                serverId,
                recordKey + "-uuid",
                $"Auto City {serverId}",
                "QA",
                20,
                null,
                1_000_000 + serverId,
                1.0,
                null,
                updatedAt,
                JsonSerializer.Serialize(new
                {
                    serverId,
                    recordKey,
                    uuid = recordKey + "-uuid",
                    ownerUid = recordKey + "-owner",
                    ownerName = $"Auto City {serverId}",
                    allianceName = "QA",
                    level = 20,
                    power = 1_000_000 + serverId,
                    x = serverId,
                    y = serverId + 1,
                    updatedAt,
                }, JsonOptions.Default));
        }
    }

    private sealed class FakeExecution
    {
        internal bool Online { get; set; } = true;
        internal bool ManualScanActive { get; set; }
        internal bool ActivateManualBeforeNextTarget { get; set; }
        internal bool AutoScanActive { get; private set; }
        internal string? CurrentRunId { get; private set; }
        internal int CurrentServerId { get; set; }
        internal int StopCalls { get; private set; }
        internal List<string> StopAttempts { get; } = [];
        internal List<int> JumpAttempts { get; } = [];
        internal List<int> StartAttempts { get; } = [];
        internal HashSet<int> ThrowJumpServers { get; } = [];
        internal HashSet<int> ThrowStartServers { get; } = [];
        internal HashSet<int> ThrowStatusServers { get; } = [];
        internal HashSet<int> NeverCompleteServers { get; } = [];
        internal Dictionary<int, string> TerminalErrors { get; } = [];
        internal TaskCompletionSource ScanStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal MapAutoScanExecutionBoundary Boundary => new()
        {
            IsOnline = () => Online,
            IsMapScanActive = () => ManualScanActive || AutoScanActive,
            ReadStatusAsync = ReadStatusAsync,
            StartTargetScanAsync = StartTargetScanAsync,
            ReturnServerAsync = ReturnServerAsync,
            StopScanIfOwnedAsync = StopScanIfOwnedAsync,
        };

        private Task<MapAutoScanRuntimeStatus> ReadStatusAsync(string? scanRunId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool readingOwnedRun = AutoScanActive &&
                (string.IsNullOrWhiteSpace(scanRunId) || string.Equals(scanRunId, CurrentRunId, StringComparison.Ordinal));
            if (readingOwnedRun && ThrowStatusServers.Contains(CurrentServerId))
                throw new InvalidOperationException("status failure on " + CurrentServerId);
            if (readingOwnedRun && NeverCompleteServers.Contains(CurrentServerId))
                return Task.FromResult(new MapAutoScanRuntimeStatus(CurrentServerId, true, null, CurrentRunId));
            if (readingOwnedRun)
            {
                string? completedRunId = CurrentRunId;
                AutoScanActive = false;
                CurrentRunId = null;
                TerminalErrors.TryGetValue(CurrentServerId, out string? lastError);
                return Task.FromResult(new MapAutoScanRuntimeStatus(CurrentServerId, false, lastError, completedRunId));
            }
            if (!string.IsNullOrWhiteSpace(scanRunId))
                return Task.FromResult(new MapAutoScanRuntimeStatus(CurrentServerId, false, null, scanRunId));
            return Task.FromResult(new MapAutoScanRuntimeStatus(CurrentServerId, ManualScanActive, null, CurrentRunId));
        }

        private Task JumpServerAsync(int serverId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            JumpAttempts.Add(serverId);
            if (ThrowJumpServers.Contains(serverId))
                throw new InvalidOperationException("jump failure on " + serverId);
            CurrentServerId = serverId;
            return Task.CompletedTask;
        }

        private async Task<string> StartTargetScanAsync(
            int serverId,
            IReadOnlyList<string> selectedTypes,
            string scanMode,
            CancellationToken cancellationToken)
        {
            if (ActivateManualBeforeNextTarget)
            {
                ActivateManualBeforeNextTarget = false;
                ManualScanActive = true;
            }
            if (ManualScanActive)
                throw new InvalidOperationException("manual scan active");
            await JumpServerAsync(serverId, cancellationToken).ConfigureAwait(false);
            await StartScanAsync(selectedTypes, scanMode, cancellationToken).ConfigureAwait(false);
            return CurrentRunId!;
        }

        private Task ReturnServerAsync(int serverId, CancellationToken cancellationToken)
        {
            if (ManualScanActive)
                throw new InvalidOperationException("manual scan active");
            return JumpServerAsync(serverId, cancellationToken);
        }

        private Task StartScanAsync(
            IReadOnlyList<string> selectedTypes,
            string scanMode,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Check(selectedTypes.Count > 0 && (scanMode == "normal" || scanMode == "fast"),
                "Auto execution boundary received invalid recovered start config");
            StartAttempts.Add(CurrentServerId);
            if (ThrowStartServers.Contains(CurrentServerId))
                throw new InvalidOperationException("start failure on " + CurrentServerId);
            AutoScanActive = true;
            CurrentRunId = "auto-" + (StartAttempts.Count).ToString(System.Globalization.CultureInfo.InvariantCulture);
            ScanStarted.TrySetResult();
            return Task.CompletedTask;
        }

        private Task<bool> StopScanIfOwnedAsync(string scanRunId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StopAttempts.Add(scanRunId);
            if (!AutoScanActive || !string.Equals(CurrentRunId, scanRunId, StringComparison.Ordinal))
                return Task.FromResult(false);
            StopCalls++;
            AutoScanActive = false;
            CurrentRunId = null;
            return Task.FromResult(true);
        }

        internal void ReplaceCompletedAutoWithManual(string manualRunId)
        {
            AutoScanActive = false;
            ManualScanActive = true;
            CurrentRunId = manualRunId;
        }
    }
}
