using System.Text.Json;
using LWBridge.Desktop;
using Map317 = LWBridge.Map317;

namespace LWBridge.Desktop.Checks;

internal static class Map317NativeBoundaryChecks
{
    internal static async Task RunAsync()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "lwb317-map-native-boundary-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await CompletedRunPublishesAndFreshServiceOwnsDataAsync(root).ConfigureAwait(false);
            await TerminalFailurePublishesRecoveredPartialRowsAsync(root).ConfigureAwait(false);
            await ActivationFailureReleasesLeaseAsync(root).ConfigureAwait(false);
            await InterruptedStartupReleasesLeaseAsync(root).ConfigureAwait(false);
            await IndependentDatabaseAcquisitionsStayIsolatedAsync(root).ConfigureAwait(false);
            await AcceptingProviderRequiresRunScopedLifetimeAsync(root).ConfigureAwait(false);
            await StopWaitsForExactProviderTerminalBeforeLeaseReuseAsync(root).ConfigureAwait(false);
            await CommittedStopRetiresCallerWithoutAbandoningCaptureAsync(root).ConfigureAwait(false);
            await DisposalTerminatesCaptureAndReleasesLeaseAsync(root).ConfigureAwait(false);
            await Completion010OriginalHostContractsAsync(root).ConfigureAwait(false);
            await Completion010ServerChangedGuardAsync(root).ConfigureAwait(false);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    private static async Task CompletedRunPublishesAndFreshServiceOwnsDataAsync(string root)
    {
        string database = Path.Combine(root, "complete", "map-data.db");
        Directory.CreateDirectory(Path.GetDirectoryName(database)!);
        var provider = new PublishingProvider(317, ProviderMode.Complete);
        using (var service = Service(database, provider))
        {
            JsonElement start = JsonSerializer.SerializeToElement(
                await service.InvokeAsync(
                    "map_scan_start",
                    JsonSerializer.SerializeToElement(new
                    {
                        selectedTypes = new[] { "city" },
                        scanMode = "fast",
                        resume = false,
                    }, JsonOptions.Default),
                    CancellationToken.None).ConfigureAwait(false),
                JsonOptions.Default);
            string runId = start.GetProperty("scanRunId").GetString() ?? string.Empty;
            Require(!string.IsNullOrWhiteSpace(runId) && provider.TerminatedRunIds.SequenceEqual(new[] { runId }),
                "completed provider run must terminate the exact durable scan owner");
            JsonElement status = JsonSerializer.SerializeToElement(
                await service.InvokeAsync("map_scan_status", Empty(), CancellationToken.None).ConfigureAwait(false),
                JsonOptions.Default);
            Require(!status.GetProperty("isReading").GetBoolean() &&
                    status.GetProperty("scanRunId").GetString() == runId,
                "completed provider run must publish terminal native scan state");
        }

        var freshProvider = new PublishingProvider(317, ProviderMode.Hold);
        using var fresh = Service(database, freshProvider);
        JsonElement summary = JsonSerializer.SerializeToElement(
            await fresh.InvokeAsync("map_summary", Empty(), CancellationToken.None).ConfigureAwait(false),
            JsonOptions.Default);
        Require(summary.GetProperty("serverId").GetInt32() == 317 &&
                summary.GetProperty("counts").GetProperty("city").GetInt32() == 1,
            "fresh native service must rehydrate the live server and see provider-published City data without reseeding");

        JsonElement search = JsonSerializer.SerializeToElement(
            await fresh.InvokeAsync(
                "map_search",
                JsonSerializer.SerializeToElement(new
                {
                    kind = "city",
                    query = new
                    {
                        serverId = 317,
                        page = 1,
                        pageSize = 50,
                        sorts = new[] { new { sortBy = "updatedAt", sortOrder = "desc" } },
                    },
                }, JsonOptions.Default),
                CancellationToken.None).ConfigureAwait(false),
            JsonOptions.Default);
        Require(search.GetProperty("total").GetInt32() == 1 &&
                search.GetProperty("rows")[0].GetProperty("ownerName").GetString() == "Provider City",
            "fresh native service search must read the provider-published record");

        string exportPath = Path.Combine(root, "complete", "provider-city.xlsx");
        Map317CityExportRequest export = fresh.PrepareCityExport(
            JsonSerializer.SerializeToElement(new
            {
                headers = new[]
                {
                    "Server", "X", "Y", "Player", "UID", "UUID",
                    "Alliance", "Level", "HP", "Shield Ends", "Marked", "Updated At",
                },
                sheetName = "Cities",
                yesLabel = "Yes",
                noLabel = "No",
                query = new
                {
                    serverId = 317,
                    page = 1,
                    pageSize = 50,
                    sorts = new[] { new { sortBy = "updatedAt", sortOrder = "desc" } },
                },
            }, JsonOptions.Default));
        JsonElement exported = JsonSerializer.SerializeToElement(fresh.WriteCityExport(export, exportPath), JsonOptions.Default);
        Require(File.Exists(exportPath) && exported.GetProperty("rowCount").GetInt32() == 1,
            "fresh native service export must write the provider-published City row");

        _ = await fresh.InvokeAsync(
            "map_scan_clear",
            JsonSerializer.SerializeToElement(new { serverId = 317 }, JsonOptions.Default),
            CancellationToken.None).ConfigureAwait(false);
        JsonElement cleared = JsonSerializer.SerializeToElement(
            await fresh.InvokeAsync(
                "map_search",
                JsonSerializer.SerializeToElement(new
                {
                    kind = "city",
                    query = new { serverId = 317, page = 1, pageSize = 50 },
                }, JsonOptions.Default),
                CancellationToken.None).ConfigureAwait(false),
            JsonOptions.Default);
        Require(cleared.GetProperty("total").GetInt32() == 0,
            "fresh native service Clear must remove only the current provider-authorized server data");
    }

    private static async Task TerminalFailurePublishesRecoveredPartialRowsAsync(string root)
    {
        string database = Path.Combine(root, "terminal-failure", "map-data.db");
        Directory.CreateDirectory(Path.GetDirectoryName(database)!);
        var provider = new PublishingProvider(317, ProviderMode.Fail);
        using (var service = Service(database, provider))
        {
            JsonElement first = JsonSerializer.SerializeToElement(await service.InvokeAsync(
                "map_scan_start",
                JsonSerializer.SerializeToElement(new
                {
                    selectedTypes = new[] { "city" },
                    scanMode = "fast",
                    resume = false,
                }, JsonOptions.Default),
                CancellationToken.None).ConfigureAwait(false), JsonOptions.Default);
            string firstRunId = first.GetProperty("scanRunId").GetString() ?? string.Empty;
            JsonElement status = JsonSerializer.SerializeToElement(
                await service.InvokeAsync("map_scan_status", Empty(), CancellationToken.None).ConfigureAwait(false),
                JsonOptions.Default);
            Require(!status.GetProperty("isReading").GetBoolean() &&
                    status.GetProperty("lastError").GetString() == "synthetic provider terminal failure",
                "provider terminal failure must remain visible in native scan status");
            using (Map317ScanProcessLease? afterFailure = Map317ScanProcessLease.TryAcquire(database))
            {
                Require(afterFailure is not null,
                    "provider terminal failure must release its process lease before service disposal");
            }
            JsonElement second = JsonSerializer.SerializeToElement(await service.InvokeAsync(
                "map_scan_start",
                JsonSerializer.SerializeToElement(new
                {
                    selectedTypes = new[] { "city" },
                    scanMode = "fast",
                    resume = false,
                }, JsonOptions.Default),
                CancellationToken.None).ConfigureAwait(false), JsonOptions.Default);
            string secondRunId = second.GetProperty("scanRunId").GetString() ?? string.Empty;
            Require(secondRunId != firstRunId && provider.TerminatedRunIds.SequenceEqual(new[] { firstRunId, secondRunId }),
                "a failed terminal run must permit a distinct later run in the same native service lifetime");
        }

        using var fresh = Service(database, new PublishingProvider(317, ProviderMode.Hold));
        _ = await fresh.InvokeAsync("map_summary", Empty(), CancellationToken.None).ConfigureAwait(false);
        JsonElement search = JsonSerializer.SerializeToElement(
            await fresh.InvokeAsync(
                "map_search",
                JsonSerializer.SerializeToElement(new
                {
                    kind = "city",
                    query = new { serverId = 317, page = 1, pageSize = 50 },
                }, JsonOptions.Default),
                CancellationToken.None).ConfigureAwait(false),
            JsonOptions.Default);
        Require(search.GetProperty("total").GetInt32() == 1 &&
                search.GetProperty("rows")[0].GetProperty("ownerName").GetString() == "Provider City",
            "provider terminal failure must preserve the recovered partial-row publication contract while retaining terminal failure state");
    }

    private static async Task ActivationFailureReleasesLeaseAsync(string root)
    {
        string database = Path.Combine(root, "activation-failure", "map-data.db");
        Directory.CreateDirectory(Path.GetDirectoryName(database)!);
        using (var service = Service(database, new PublishingProvider(317, ProviderMode.ThrowOnActivate)))
        {
            bool failed = false;
            try
            {
                _ = await service.InvokeAsync(
                    "map_scan_start",
                    JsonSerializer.SerializeToElement(new
                    {
                        selectedTypes = new[] { "city" },
                        scanMode = "fast",
                        resume = false,
                    }, JsonOptions.Default),
                    CancellationToken.None).ConfigureAwait(false);
            }
            catch (InvalidOperationException error) when (error.Message.Contains("activation failure", StringComparison.Ordinal))
            {
                failed = true;
            }
            Require(failed, "provider activation failure must surface instead of fabricating scan success");
            using Map317ScanProcessLease? immediate = Map317ScanProcessLease.TryAcquire(database);
            Require(immediate is not null,
                "provider activation failure must release the scan lease before service disposal");
        }

        using var retry = Service(database, new PublishingProvider(317, ProviderMode.Complete));
        _ = await retry.InvokeAsync(
            "map_scan_start",
            JsonSerializer.SerializeToElement(new
            {
                selectedTypes = new[] { "city" },
                scanMode = "fast",
                resume = false,
            }, JsonOptions.Default),
            CancellationToken.None).ConfigureAwait(false);
        Require(!retry.IsScanActive,
            "activation failure must release the scan lease so a fresh service can acquire and complete a new run");
    }

    private static async Task DisposalTerminatesCaptureAndReleasesLeaseAsync(string root)
    {
        string database = Path.Combine(root, "dispose-active", "map-data.db");
        Directory.CreateDirectory(Path.GetDirectoryName(database)!);
        var provider = new PublishingProvider(317, ProviderMode.Hold);
        var service = Service(database, provider);
        JsonElement start = JsonSerializer.SerializeToElement(
            await service.InvokeAsync(
                "map_scan_start",
                JsonSerializer.SerializeToElement(new
                {
                    selectedTypes = new[] { "city" },
                    scanMode = "fast",
                    resume = false,
                }, JsonOptions.Default),
                CancellationToken.None).ConfigureAwait(false),
            JsonOptions.Default);
        string runId = start.GetProperty("scanRunId").GetString() ?? string.Empty;
        Require(service.IsScanActive && provider.ActiveRunId == runId,
            "held provider capture must own the exact accepted native run before disposal");
        service.Dispose();
        Require(provider.Disposed && provider.TerminatedRunIds.SequenceEqual(new[] { runId }),
            "native service disposal must terminate the injected active capture and its exact run owner");

        using var retry = Service(database, new PublishingProvider(317, ProviderMode.Complete));
        _ = await retry.InvokeAsync(
            "map_scan_start",
            JsonSerializer.SerializeToElement(new
            {
                selectedTypes = new[] { "city" },
                scanMode = "fast",
                resume = false,
            }, JsonOptions.Default),
            CancellationToken.None).ConfigureAwait(false);
        Require(!retry.IsScanActive,
            "native service disposal must release the owned process lease for the next service lifetime");
    }

    private static async Task InterruptedStartupReleasesLeaseAsync(string root)
    {
        string database = Path.Combine(root, "interrupted-startup", "map-data.db");
        Directory.CreateDirectory(Path.GetDirectoryName(database)!);
        var provider = new PublishingProvider(317, ProviderMode.HoldStartupUntilCancelled);
        using (var service = Service(database, provider))
        using (var cancellation = new CancellationTokenSource())
        {
            Task<object?> starting = service.InvokeAsync(
                "map_scan_start",
                JsonSerializer.SerializeToElement(new
                {
                    selectedTypes = new[] { "city" },
                    scanMode = "fast",
                    resume = false,
                }, JsonOptions.Default),
                cancellation.Token);
            await provider.StartEntered.Task.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            using (Map317ScanProcessLease? whileStarting = Map317ScanProcessLease.TryAcquire(database))
            {
                Require(whileStarting is null,
                    "interrupted startup must retain the process lease while provider acceptance is pending");
            }
            cancellation.Cancel();
            bool cancelled = false;
            try
            {
                _ = await starting.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }
            Require(cancelled && !service.IsScanActive && provider.ActiveRunId is null,
                "interrupted provider startup must surface cancellation without fabricating an active scan");
            using Map317ScanProcessLease? immediate = Map317ScanProcessLease.TryAcquire(database);
            Require(immediate is not null,
                "interrupted startup must release the process lease before service disposal");
        }

        using var retry = Service(database, new PublishingProvider(317, ProviderMode.Complete));
        _ = await retry.InvokeAsync(
            "map_scan_start",
            JsonSerializer.SerializeToElement(new
            {
                selectedTypes = new[] { "city" },
                scanMode = "fast",
                resume = false,
            }, JsonOptions.Default),
            CancellationToken.None).ConfigureAwait(false);
        Require(!retry.IsScanActive,
            "interrupted startup must release the process lease for the next native service lifetime");
    }

    private static async Task IndependentDatabaseAcquisitionsStayIsolatedAsync(string root)
    {
        string databaseA = Path.Combine(root, "lease-isolation", "profile-A", "map-data.db");
        string databaseB = Path.Combine(root, "lease-isolation", "profile-B", "map-data.db");
        Directory.CreateDirectory(Path.GetDirectoryName(databaseA)!);
        Directory.CreateDirectory(Path.GetDirectoryName(databaseB)!);
        var providerA = new PublishingProvider(317, ProviderMode.Hold);
        var providerB = new PublishingProvider(317, ProviderMode.Hold);
        using var serviceA = Service(databaseA, providerA);
        using var serviceB = Service(databaseB, providerB);

        _ = await serviceA.InvokeAsync(
            "map_scan_start",
            JsonSerializer.SerializeToElement(new
            {
                selectedTypes = new[] { "city" },
                scanMode = "fast",
                resume = false,
            }, JsonOptions.Default),
            CancellationToken.None).ConfigureAwait(false);
        _ = await serviceB.InvokeAsync(
            "map_scan_start",
            JsonSerializer.SerializeToElement(new
            {
                selectedTypes = new[] { "city" },
                scanMode = "fast",
                resume = false,
            }, JsonOptions.Default),
            CancellationToken.None).ConfigureAwait(false);

        Require(serviceA.IsScanActive && serviceB.IsScanActive &&
                providerA.ActiveRunId is not null && providerB.ActiveRunId is not null &&
                providerA.ActiveRunId != providerB.ActiveRunId,
            "independent profile database paths must own distinct simultaneous native scan acquisitions");
        using (Map317ScanProcessLease? blockedA = Map317ScanProcessLease.TryAcquire(databaseA))
        using (Map317ScanProcessLease? blockedB = Map317ScanProcessLease.TryAcquire(databaseB))
        {
            Require(blockedA is null && blockedB is null,
                "each active isolated database must block only a second acquisition of its own lease");
        }

        _ = await serviceA.InvokeAsync("map_scan_stop", Empty(), CancellationToken.None).ConfigureAwait(false);
        Require(!serviceA.IsScanActive && serviceB.IsScanActive,
            "stopping profile A acquisition must not retire profile B's independent scan owner");
        _ = await serviceB.InvokeAsync("map_scan_stop", Empty(), CancellationToken.None).ConfigureAwait(false);
        using Map317ScanProcessLease? releasedA = Map317ScanProcessLease.TryAcquire(databaseA);
        using Map317ScanProcessLease? releasedB = Map317ScanProcessLease.TryAcquire(databaseB);
        Require(releasedA is not null && releasedB is not null,
            "both isolated leases must be independently reusable after their exact owners stop");
    }

    private static async Task AcceptingProviderRequiresRunScopedLifetimeAsync(string root)
    {
        string database = Path.Combine(root, "plain-provider-rejected", "map-data.db");
        Directory.CreateDirectory(Path.GetDirectoryName(database)!);
        var provider = new Map317.MapProviderAdapter(
            _ => ValueTask.FromResult(new Map317.MapProviderContext(true, true, 317, "live", 1, 100, 100, 1)),
            _ => ValueTask.FromResult(new Map317.MapProviderContext(true, true, 317, "live", 1, 100, 100, 1)),
            (_, _) => ValueTask.FromResult(new Map317.MapProviderStartResult(true, 1, true)),
            _ => ValueTask.CompletedTask);
        using var service = new Map317CommandService(
            database,
            provider,
            Map317.UnavailableMapActionProvider.Instance,
            startPlunderWorkers: false);
        string error = string.Empty;
        try
        {
            _ = await service.InvokeAsync(
                "map_scan_start",
                JsonSerializer.SerializeToElement(new
                {
                    selectedTypes = new[] { "city" },
                    scanMode = "fast",
                    resume = false,
                }, JsonOptions.Default),
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            error = ex.Message;
        }
        Require(error.Contains("run-scoped terminal lifetime", StringComparison.Ordinal) && !service.IsScanActive,
            "an accepting injected provider without the run-scoped lifetime seam must fail closed");
        using Map317ScanProcessLease? immediate = Map317ScanProcessLease.TryAcquire(database);
        Require(immediate is not null,
            "rejecting a non-run-scoped accepting provider must release its process lease immediately");
    }

    private static async Task StopWaitsForExactProviderTerminalBeforeLeaseReuseAsync(string root)
    {
        string database = Path.Combine(root, "delayed-stop-terminal", "map-data.db");
        Directory.CreateDirectory(Path.GetDirectoryName(database)!);
        var provider = new PublishingProvider(317, ProviderMode.HoldDelayedStop);
        using var service = Service(database, provider);
        JsonElement first = JsonSerializer.SerializeToElement(
            await service.InvokeAsync(
                "map_scan_start",
                JsonSerializer.SerializeToElement(new
                {
                    selectedTypes = new[] { "city" },
                    scanMode = "fast",
                    resume = false,
                }, JsonOptions.Default),
                CancellationToken.None).ConfigureAwait(false),
            JsonOptions.Default);
        string firstRunId = first.GetProperty("scanRunId").GetString() ?? string.Empty;

        using var callerCancellation = new CancellationTokenSource();
        Task<object?> stopping = service.InvokeAsync("map_scan_stop", Empty(), callerCancellation.Token);
        await provider.StopEntered.Task.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        callerCancellation.Cancel();
        using (Map317ScanProcessLease? whileStopping = Map317ScanProcessLease.TryAcquire(database))
        {
            Require(whileStopping is null,
                "Start→Stop must retain the process lease until the exact provider run reaches its terminal callback");
        }

        provider.ReleaseStop();
        _ = await stopping.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        Require(provider.TerminatedRunIds.SequenceEqual(new[] { firstRunId }),
            "delayed Stop must terminalize exactly the first accepted provider run");
        using (Map317ScanProcessLease? afterStop = Map317ScanProcessLease.TryAcquire(database))
        {
            Require(afterStop is not null,
                "the process lease must become reusable immediately after the exact provider terminal callback");
        }

        JsonElement second = JsonSerializer.SerializeToElement(
            await service.InvokeAsync(
                "map_scan_start",
                JsonSerializer.SerializeToElement(new
                {
                    selectedTypes = new[] { "city" },
                    scanMode = "fast",
                    resume = false,
                }, JsonOptions.Default),
                CancellationToken.None).ConfigureAwait(false),
            JsonOptions.Default);
        string secondRunId = second.GetProperty("scanRunId").GetString() ?? string.Empty;
        Require(secondRunId != firstRunId && service.IsScanActive,
            "Start→Stop→Start must admit a distinct second run only after the first provider lifetime is terminal");
        _ = await service.InvokeAsync("map_scan_stop", Empty(), CancellationToken.None).ConfigureAwait(false);
        Require(provider.TerminatedRunIds.SequenceEqual(new[] { firstRunId, secondRunId }),
            "second Stop must release only the second accepted provider run");
    }

    private static async Task CommittedStopRetiresCallerWithoutAbandoningCaptureAsync(string root)
    {
        string database = Path.Combine(root, "committed-stop-retired-caller", "map-data.db");
        Directory.CreateDirectory(Path.GetDirectoryName(database)!);
        var provider = new PublishingProvider(317, ProviderMode.HoldDelayedStop);
        using var service = Service(database, provider);
        JsonElement StartPayload() => JsonSerializer.SerializeToElement(new
        {
            selectedTypes = new[] { "city" },
            scanMode = "fast",
            resume = false,
        }, JsonOptions.Default);

        JsonElement first = JsonSerializer.SerializeToElement(
            await service.InvokeAsync("map_scan_start", StartPayload(), CancellationToken.None).ConfigureAwait(false),
            JsonOptions.Default);
        string firstRunId = first.GetProperty("scanRunId").GetString() ?? string.Empty;

        // Cancellation before native Stop admission must retain the active local
        // run and must never reach the provider's committed-Stop boundary.
        using (var beforeAdmission = new CancellationTokenSource())
        {
            beforeAdmission.Cancel();
            bool rejected = false;
            try
            {
                _ = await service.InvokeAsync("map_scan_stop", Empty(), beforeAdmission.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { rejected = true; }
            using var persisted = new Map317.MapStore(database);
            Require(rejected && service.IsScanActive && persisted.ReadScanRun(firstRunId)?.Status == "running" &&
                    provider.StopCalls == 0,
                "caller cancellation before Stop admission must preserve the local run and provider capture");
        }

        using var retiredCaller = new CancellationTokenSource();
        bool committedBarrierReached = false;
        provider.BeforeCaptureStop = providerToken =>
        {
            using var persisted = new Map317.MapStore(database);
            Require(persisted.ReadScanRun(firstRunId)?.Status == "cancelled",
                "the controlled pre-capture-Stop barrier must follow the actual native durable cancellation commit");
            Require(service.IsScanActive && provider.ActiveRunId == firstRunId,
                "the exact accepted capture must remain owned before provider Stop is signaled");
            committedBarrierReached = true;
            retiredCaller.Cancel();
            Require(!providerToken.CanBeCanceled,
                "committed native Stop must pass an independent terminalization token to the capture provider");
        };

        Task<object?> stopping = service.InvokeAsync("map_scan_stop", Empty(), retiredCaller.Token);
        try
        {
            await provider.StopEntered.Task.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            Require(committedBarrierReached && retiredCaller.IsCancellationRequested &&
                    provider.CaptureCancellationRunIds.SequenceEqual(new[] { firstRunId }) &&
                    provider.ActiveRunId == firstRunId && !stopping.IsCompleted,
                "retiring the caller after local commit must signal exact capture cancellation and await its termination");
            Require(provider.TerminatedRunIds.Count == 0,
                "capture cancellation alone must not fabricate a provider terminal callback");
            using (Map317ScanProcessLease? whileUnwinding = Map317ScanProcessLease.TryAcquire(database))
            {
                Require(whileUnwinding is null,
                    "the exact run lease must remain owned while capture unwinds after caller retirement");
            }

            // A new native Start must still wait behind Stop; cancel this attempt
            // while the old capture is held so it cannot become a second owner.
            using var blockedStartCancellation = new CancellationTokenSource();
            Task<object?> blockedStart = service.InvokeAsync("map_scan_start", StartPayload(), blockedStartCancellation.Token);
            Require(!blockedStart.IsCompleted,
                "new native Start must not be admitted before committed Stop observes exact capture termination");
            blockedStartCancellation.Cancel();
            bool startCancelled = false;
            try { _ = await blockedStart.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false); }
            catch (OperationCanceledException) { startCancelled = true; }
            Require(startCancelled && provider.ActiveRunId == firstRunId,
                "canceling a pending replacement Start must leave the first capture and its lease owned");
        }
        finally
        {
            // Always release this test-owned barrier, including on an assertion
            // failure, so disposal cannot strand an unfinished inert provider.
            provider.ReleaseStop();
            _ = await stopping.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        }

        JsonElement stopped = JsonSerializer.SerializeToElement(
            await stopping.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false), JsonOptions.Default);
        Require(!stopped.GetProperty("isReading").GetBoolean() &&
                stopped.GetProperty("scanRunId").GetString() == firstRunId &&
                provider.TerminatedRunIds.SequenceEqual(new[] { firstRunId }),
            "committed Stop must publish idle only after the exact signaled run terminates, despite caller retirement");
        using (Map317ScanProcessLease? afterTerminal = Map317ScanProcessLease.TryAcquire(database))
        {
            Require(afterTerminal is not null,
                "the process lease must become reusable after the exact provider terminal callback");
        }

        _ = await service.InvokeAsync("map_scan_stop", Empty(), CancellationToken.None).ConfigureAwait(false);
        Require(provider.StopCalls == 1 && provider.TerminatedRunIds.SequenceEqual(new[] { firstRunId }),
            "repeated idle Stop must not re-signal or re-terminalize the completed provider run");
        provider.BeforeCaptureStop = null;
        JsonElement second = JsonSerializer.SerializeToElement(
            await service.InvokeAsync("map_scan_start", StartPayload(), CancellationToken.None).ConfigureAwait(false),
            JsonOptions.Default);
        string secondRunId = second.GetProperty("scanRunId").GetString() ?? string.Empty;
        Require(secondRunId != firstRunId && service.IsScanActive && provider.ActiveRunId == secondRunId,
            "a distinct replacement run must be admitted after the exact first capture terminal callback");
        _ = await service.InvokeAsync("map_scan_stop", Empty(), CancellationToken.None).ConfigureAwait(false);
        Require(provider.StopCalls == 2 &&
                provider.CaptureCancellationRunIds.SequenceEqual(new[] { firstRunId, secondRunId }) &&
                provider.TerminatedRunIds.SequenceEqual(new[] { firstRunId, secondRunId }),
            "the next Stop must signal and release only the new exact run owner");
    }


    // COMPLETION-010 D/E: original 0.3.17 host-validation facts (EXE strings, RE5/RE3
    // and the original Auto-scan frontend) through the actual production command service.
    private static async Task Completion010OriginalHostContractsAsync(string root)
    {
        string database = Path.Combine(root, "completion010", "map-data.db");
        Directory.CreateDirectory(Path.GetDirectoryName(database)!);
        var provider = new PublishingProvider(317, ProviderMode.Complete);
        using var service = Service(database, provider);

        static (string? Code, string? Message) Failure(Func<Task> action)
        {
            try { action().GetAwaiter().GetResult(); }
            catch (Exception error)
            {
                var type = error.GetType();
                return (type.GetProperty("Code")?.GetValue(error) as string, error.Message);
            }
            return (null, null);
        }

        const string serverText = "server ID must be an integer from 1 to 99999";
        foreach (object? bad in new object?[] { null, 0, 100000, -3, "5", 5.5 })
        {
            var payload = bad is null
                ? JsonSerializer.SerializeToElement(new { }, JsonOptions.Default)
                : JsonSerializer.SerializeToElement(new { serverId = bad }, JsonOptions.Default);
            (string? code, string? message) = Failure(() => service.InvokeAsync(
                "server_jump", payload, CancellationToken.None));
            Require(code == "INVALID_SERVER_ID" && message == serverText,
                $"server_jump with serverId={bad ?? "<missing>"} must be INVALID_SERVER_ID/'{serverText}' but was {code}/{message}");
        }

        (string? scopeCode, string? scopeMessage) = Failure(() => service.InvokeAsync(
            "map_treasure_claim",
            JsonSerializer.SerializeToElement(new { serverId = 317 }, JsonOptions.Default),
            CancellationToken.None));
        Require(scopeCode == "INVALID_TREASURE_CLAIM_SCOPE" && scopeMessage == "treasure claim scope is invalid",
            "map_treasure_claim without claimScope must be INVALID_TREASURE_CLAIM_SCOPE");
        (string? singleCode, _) = Failure(() => service.InvokeAsync(
            "map_treasure_claim",
            JsonSerializer.SerializeToElement(new { serverId = 317, claimScope = "single" }, JsonOptions.Default),
            CancellationToken.None));
        Require(singleCode == "INVALID_TREASURE_CLAIM_SCOPE", "single claim without a target UUID must be rejected");
        (string? claimServerCode, string? claimServerMessage) = Failure(() => service.InvokeAsync(
            "map_treasure_claim",
            JsonSerializer.SerializeToElement(new { claimScope = "boxes" }, JsonOptions.Default),
            CancellationToken.None));
        Require(claimServerCode == "INVALID_SERVER_ID" && claimServerMessage == serverText,
            "map_treasure_claim without a server must be INVALID_SERVER_ID with the original text");

        string[] twelve = Enumerable.Range(1, 12).Select(n => "H" + n).ToArray();
        // Original 0x129FA7 order: headers are validated BEFORE the export server.
        (string? headerCode, string? headerMessage) = Failure(() =>
        {
            service.PrepareCityExport(JsonSerializer.SerializeToElement(new
            {
                query = new { serverId = 0 }, headers = new[] { "a" },
            }, JsonOptions.Default));
            return Task.CompletedTask;
        });
        Require(headerCode == "MAP_EXPORT_FAILED" && headerMessage == "city export headers are invalid",
            $"City export with bad headers must fail on headers first but was {headerCode}/{headerMessage}");

        // A non-string selectedTypes entry is ignored (not an unstructured crash).
        JsonElement started = JsonSerializer.SerializeToElement(
            await service.InvokeAsync(
                "map_scan_start",
                JsonSerializer.SerializeToElement(new
                {
                    selectedTypes = new object[] { 1, "city" },
                    scanMode = "fast",
                    resume = false,
                }, JsonOptions.Default),
                CancellationToken.None).ConfigureAwait(false),
            JsonOptions.Default);
        Require(!string.IsNullOrWhiteSpace(started.GetProperty("scanRunId").GetString()),
            "selectedTypes [1,'city'] must start a City scan instead of throwing");

        // The scan published the live server's City rows; the shared-state server is 317.
        JsonElement Search(object payload) => JsonSerializer.SerializeToElement(
            service.InvokeAsync("map_search", JsonSerializer.SerializeToElement(payload, JsonOptions.Default),
                CancellationToken.None).GetAwaiter().GetResult(), JsonOptions.Default);
        int Total(JsonElement result) => result.GetProperty("total").GetInt32();

        Require(Total(Search(new { kind = "city", query = new { serverId = 317 } })) == 1, "baseline search");
        // Original coercion (0x3EB60E/0x3E1144-0x3E1211): nothing below may throw.
        Require(Total(Search(new { kind = "city", query = new { serverId = 0 } })) == 1,
            "serverId 0 is replaced by the shared-state server (not rejected)");
        Require(Total(Search(new { kind = "city", query = new { serverId = "317", page = "abc", pageSize = 99999 } })) == 1,
            "numeric-string server, non-numeric page and huge pageSize are coerced");
        Require(Total(Search(new { kind = "city", query = new { } })) == 1, "missing serverId uses the shared-state server");
        Require(Total(Search(new { kind = "city" })) == 1, "missing query is an empty query object");
        Require(Total(Search(new { kind = "not-a-kind", query = new { serverId = 317 } })) == 0,
            "unknown kind answers an empty page (original swallows INVALID_MAP_KIND)");
        Require(Total(Search(new { kind = 5, query = new { serverId = 317 } })) == 0, "non-string kind answers an empty page");
        Require(Total(Search(new { kind = "city", query = new { serverId = 100000 } })) == 0, "server above 99999 matches nothing");
        JsonElement paged = Search(new { kind = "city", query = new { serverId = 317, page = -4, pageSize = 0 } });
        Require(Total(paged) == 1 && paged.GetProperty("rows").GetArrayLength() == 1, "page<1 and pageSize<1 clamp to 1");

        // Export server derivation: query.serverId <= 0 / missing -> shared-state server (317).
        Map317CityExportRequest derived = service.PrepareCityExport(JsonSerializer.SerializeToElement(new
        {
            headers = twelve, query = new { serverId = 0 },
        }, JsonOptions.Default));
        Require(derived.DefaultFileName.Contains("-317-", StringComparison.Ordinal),
            "export server falls back to the shared-state server");
        Map317CityExportRequest noQuery = service.PrepareCityExport(JsonSerializer.SerializeToElement(new
        {
            headers = twelve,
        }, JsonOptions.Default));
        Require(noQuery.DefaultFileName.Contains("-317-", StringComparison.Ordinal), "missing export query is {}");

        // No shared-state server at all -> original error.
        var unknownServer = new PublishingProvider(0, ProviderMode.Hold);
        using var offline = Service(Path.Combine(root, "completion010", "offline.db"), unknownServer);
        (string? exportCode, string? exportMessage) = Failure(() =>
        {
            offline.PrepareCityExport(JsonSerializer.SerializeToElement(new
            {
                headers = twelve, query = new { serverId = 0 },
            }, JsonOptions.Default));
            return Task.CompletedTask;
        });
        Require(exportCode == "MAP_EXPORT_FAILED" && exportMessage == "city export server is unavailable",
            $"City export without any positive server must be MAP_EXPORT_FAILED/'city export server is unavailable' but was {exportCode}/{exportMessage}");
    }


    // Original status service 0xF8CB2: live server change while reading fails the run
    // (staging discarded), stops the provider scan and leaves the shared state idle.
    private static async Task Completion010ServerChangedGuardAsync(string root)
    {
        string database = Path.Combine(root, "completion010-guard", "map-data.db");
        Directory.CreateDirectory(Path.GetDirectoryName(database)!);
        var provider = new PublishingProvider(317, ProviderMode.Hold);
        using var service = Service(database, provider);
        JsonElement started = JsonSerializer.SerializeToElement(
            await service.InvokeAsync("map_scan_start",
                JsonSerializer.SerializeToElement(new { selectedTypes = new[] { "city" }, scanMode = "fast", resume = false },
                    JsonOptions.Default), CancellationToken.None).ConfigureAwait(false), JsonOptions.Default);
        string run = started.GetProperty("scanRunId").GetString()!;
        var reading = (LWBridge.Map317.MapScanState)service.CreateStatus();
        Require(reading.IsReading && reading.ServerId == 317, "precondition: reading on server 317");

        // Non-triggers leave the state unchanged and stop nothing.
        foreach ((bool inWorld, int live, string why) in new[]
        {
            (true, 317, "same server"), (true, 0, "live id unavailable"), (false, 318, "not in world"),
        })
        {
            var unchanged = await service.ApplyLiveServerGuardAsync(reading, inWorld, live, CancellationToken.None)
                .ConfigureAwait(false);
            Require(unchanged.IsReading && provider.StopCalls == 0, "guard must not fire: " + why);
        }

        var failed = await service.ApplyLiveServerGuardAsync(reading, true, 318, CancellationToken.None).ConfigureAwait(false);
        Require(!failed.IsReading && failed.Phase == "idle" && !failed.ResumeAvailable &&
                failed.Error == "current server changed during map scan",
            "server change must leave idle state with the original error text");
        Require(provider.StopCalls == 1, "server change must stop the provider scan once");
        using var reopened = new LWBridge.Map317.MapStore(database);
        var runRow = reopened.ReadScanRun(run);
        Require(runRow?.Status == "failed" && runRow.Error == "current server changed during map scan",
            "durable run must be failed with the original text");
        Require(reopened.Search(new LWBridge.Map317.MapQuery("city", 317)).Total == 0,
            "failed run must not publish or preserve staged rows");
    }

    private static Map317CommandService Service(string database, IMap317RunScopedProvider provider) =>
        new(database, provider, Map317.UnavailableMapActionProvider.Instance, startPlunderWorkers: false);

    private static JsonElement Empty() => JsonSerializer.SerializeToElement(new { }, JsonOptions.Default);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Map317 native boundary check failed: " + message);
    }

    private enum ProviderMode
    {
        Complete,
        Fail,
        ThrowOnActivate,
        HoldStartupUntilCancelled,
        Hold,
        HoldDelayedStop,
    }

    private sealed class PublishingProvider(int serverId, ProviderMode mode) : IMap317RunScopedProvider
    {
        private Map317.MapProviderStartRequest? pending;
        private Map317.MapControlPlane? activeControl;

        public event Action<string>? RunTerminated;

        internal string? ActiveRunId { get; private set; }
        internal bool Disposed { get; private set; }
        internal int StopCalls { get; private set; }
        internal Action<CancellationToken>? BeforeCaptureStop { get; set; }
        internal List<string> CaptureCancellationRunIds { get; } = [];
        internal List<string> TerminatedRunIds { get; } = [];
        internal TaskCompletionSource StartEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource StopEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource stopRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private CancellationTokenSource? captureCancellation;

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

        public async ValueTask<Map317.MapProviderStartResult> StartMapScanAsync(
            Map317.MapProviderStartRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (mode == ProviderMode.HoldStartupUntilCancelled)
            {
                StartEntered.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            }
            pending = request;
            return new Map317.MapProviderStartResult(
                Accepted: true,
                TotalBlocks: 1,
                NativeCaptureReady: true);
        }

        public async ValueTask StopMapScanAsync(CancellationToken cancellationToken = default)
        {
            // Test-only entry barrier: native local Stop has already committed,
            // but this controlled capture has not yet checked/signaled Stop.
            BeforeCaptureStop?.Invoke(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            StopCalls++;
            if (ActiveRunId is { } stoppingRunId)
            {
                captureCancellation?.Cancel();
                Require(captureCancellation?.IsCancellationRequested == true,
                    "controlled capture Stop must signal its accepted run token");
                CaptureCancellationRunIds.Add(stoppingRunId);
            }
            if (mode == ProviderMode.HoldDelayedStop)
            {
                StopEntered.TrySetResult();
                await stopRelease.Task.ConfigureAwait(false);
            }
            string? runId = ActiveRunId;
            ActiveRunId = null;
            activeControl = null;
            captureCancellation?.Dispose();
            captureCancellation = null;
            if (runId is not null) Terminate(runId);
        }

        public void ActivateAcceptedRun(Map317.MapControlPlane control, string scanRunId)
        {
            Map317.MapProviderStartRequest request = pending ??
                throw new InvalidOperationException("provider activation has no accepted request");
            pending = null;
            Require(request.ScanRunId == scanRunId && control.ScanState.ScanRunId == scanRunId,
                "provider activation must match the exact durable scan run");
            if (mode == ProviderMode.ThrowOnActivate)
                throw new InvalidOperationException("synthetic provider activation failure");

            activeControl = control;
            ActiveRunId = scanRunId;
            captureCancellation = new CancellationTokenSource();
            if (mode is ProviderMode.Hold or ProviderMode.HoldDelayedStop) return;

            control.StageRecord(CityRecord(request.ServerId));
            control.ReportProgress(new Map317.MapScanProgressUpdate(
                CompletedBlocks: 1,
                ReadBlocks: 1,
                FailedBlocks: 0,
                InflightBlocks: 0,
                ScanRate: 1,
                NativeCaptureReady: true,
                NativePendingRecords: 0,
                NativeDroppedRecords: 0,
                Phase: mode == ProviderMode.Complete ? "completed" : "failed"));
            if (mode == ProviderMode.Complete)
                _ = control.CompleteScan();
            else
                _ = control.FailScan("synthetic provider terminal failure");
            activeControl = null;
            ActiveRunId = null;
            captureCancellation.Dispose();
            captureCancellation = null;
            Terminate(scanRunId);
        }

        public void Dispose()
        {
            if (Disposed) return;
            Disposed = true;
            pending = null;
            string? runId = ActiveRunId;
            ActiveRunId = null;
            activeControl = null;
            captureCancellation?.Cancel();
            captureCancellation?.Dispose();
            captureCancellation = null;
            if (runId is not null) Terminate(runId);
        }

        internal void ReleaseStop() => stopRelease.TrySetResult();

        private void Terminate(string runId)
        {
            TerminatedRunIds.Add(runId);
            RunTerminated?.Invoke(runId);
        }

        private Map317.MapProviderContext Context() => new(
            IsAvailable: true,
            IsInWorld: true,
            ServerId: serverId,
            ServerIdSource: "live",
            WorldId: 1,
            TileWidth: 100,
            TileHeight: 100,
            ExpectedTotalBlocks: 1);

        private static Map317.MapRecord CityRecord(int serverId)
        {
            const long updatedAt = 1_800_000_100_000;
            return new Map317.MapRecord(
                "city",
                serverId,
                "provider-city",
                1,
                "provider-city-uuid",
                "Provider City",
                "QA",
                30,
                null,
                9_999_999,
                1.5,
                null,
                updatedAt,
                JsonSerializer.Serialize(new
                {
                    serverId,
                    recordKey = "provider-city",
                    uuid = "provider-city-uuid",
                    ownerUid = "provider-city-owner",
                    ownerName = "Provider City",
                    allianceName = "QA",
                    level = 30,
                    power = 9_999_999,
                    x = 11,
                    y = 12,
                    updatedAt,
                }, JsonOptions.Default));
        }
    }
}
