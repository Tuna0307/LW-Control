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
