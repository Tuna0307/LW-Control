using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using LWBridge.Desktop;
using Map317 = LWBridge.Map317;

namespace LWBridge.Desktop.Checks;

/// <summary>
/// Bounded same-session stop/clear/restart proof for LWB317-LIVE-MAP-V22-002.
/// This is test-only orchestration over the production Overview lifecycle and
/// Map317 command service. It never changes production scan behavior.
/// </summary>
internal static class LiveMapV22StopContinuityProof
{
    private const string TaskId = "LWB317-LIVE-MAP-V22-002";
    private const string OfficialV22Sha256 =
        "248f3aeac712b3f14f86bff37a0c365e467897a2248403837c44c1b774f05b22";
    private const string WrappedV22Sha256 =
        "a705d2d64a44081012c4c7e30bda613636960c98e40fa7a367a60577ce3a7d72";
    private const string ExpectedGameSha256 =
        "905c98c1f89841f90b492556192ba0642f3d209a873cb8c1f7b3c340aca0733d";

    internal static async Task RunAsync(string outputPath)
    {
        outputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        DateTimeOffset proofStartedAt = DateTimeOffset.UtcNow;
        int proofPid = Environment.ProcessId;
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string appData = Directory.GetParent(localAppData)?.FullName
            ?? throw new InvalidOperationException("AppData root is unavailable.");
        string gameRoot = Path.Combine(localAppData, "FunFly", "Last War-Survival Game");
        string gamePath = Path.Combine(gameRoot, "Game", "LastWar.exe");
        string scriptRoot = Path.Combine(appData, "LocalLow", "FunFly", "Last War-Survival Game", "lwScripts");
        string packagePath = Path.Combine(scriptRoot, "LWScripts.data");
        string versionPath = Path.Combine(scriptRoot, "version.txt");
        string overviewRuntimeRoot = Path.Combine(localAppData, "LWBridgeRebuild", "overview-bridge");
        string probeRuntimeRoot = Path.Combine(localAppData, "LWBridgeRebuild", "live-resource");
        Directory.CreateDirectory(probeRuntimeRoot);
        string profileId = "lwb317-live-map-v22-002-" + Guid.NewGuid().ToString("N")[..12];
        string databasePath = Path.Combine(
            probeRuntimeRoot,
            "lwb317-live-map-v22-002-" + Guid.NewGuid().ToString("N") + ".db");

        var evidence = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schemaVersion"] = 1,
            ["taskId"] = TaskId,
            ["proofStartedAt"] = proofStartedAt,
            ["safety"] = new
            {
                assistantOwnedSessionRequired = true,
                unrelatedGameplay = false,
                serverJump = false,
                restartResume = false,
                treasureClaim = false,
                ghostPreparation = false,
            },
        };

        Exception? failure = null;
        string? failedOperation = null;
        OverviewLifecycleService? lifecycle = null;
        Map317CommandService? mapService = null;
        CurrentClientMapBlockSource? source = null;
        OverviewMapScanSession? ownedSession = null;
        string? instanceId = null;
        int? gamePid = null;

        try
        {
            JsonElement preflightProcesses = CaptureRelevantProcesses(proofPid, null, proofStartedAt);
            evidence["preflight"] = new
            {
                capturedAt = DateTimeOffset.UtcNow,
                processes = preflightProcesses,
                packageSha256 = Sha256File(packagePath),
                versionMarker = ReadText(versionPath),
                recoveryJournal = ReadRecoveryJournal(overviewRuntimeRoot),
            };
            RequireNoPreexistingRuntime(preflightProcesses, proofPid);

            string pristinePackageHash = Sha256File(packagePath);
            if (!string.Equals(pristinePackageHash, OfficialV22Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    $"Expected pristine validated v22 package {OfficialV22Sha256}, observed {pristinePackageHash}.");
            string gameHash = Sha256File(gamePath);
            if (!string.Equals(gameHash, ExpectedGameSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("LastWar.exe identity changed before live proof.");

            lifecycle = new OverviewLifecycleService(profileId, gameRoot);
            using var operationCts = new CancellationTokenSource(TimeSpan.FromMinutes(8));
            using JsonDocument empty = JsonDocument.Parse("{}");

            failedOperation = "profile_instance_start";
            _ = await lifecycle.InvokeAsync(
                "profile_instance_start", empty.RootElement.Clone(), operationCts.Token).ConfigureAwait(false);
            ownedSession = await WaitForMapReadyAsync(lifecycle, operationCts.Token).ConfigureAwait(false);
            await lifecycle.WaitForHealthyMapScanSessionAsync(ownedSession, operationCts.Token).ConfigureAwait(false);
            instanceId = ownedSession.SessionId;
            gamePid = ownedSession.GamePid;

            evidence["session"] = new
            {
                profileId = ownedSession.ProfileId,
                instanceId = ownedSession.SessionId,
                gamePid = ownedSession.GamePid,
                gamePath = ownedSession.GamePath,
                gameStartedAtUtc = ownedSession.GameStartedAtUtc,
                gameSha256 = gameHash,
                validatedContentVersion = 22,
                officialPackageSha256 = pristinePackageHash,
                runtimePackageSha256 = Sha256File(packagePath),
                runtimePackageState = ClassifyPackage(Sha256File(packagePath)),
                runtimeVersionMarker = ReadText(versionPath),
                processes = CaptureRelevantProcesses(proofPid, ownedSession.GamePid, proofStartedAt),
            };

            mapService = new Map317CommandService(databasePath, lifecycle);
            source = new CurrentClientMapBlockSource(lifecycle);

            failedOperation = "ready_before_scan_1";
            JsonElement readyBefore1 = await CaptureReadinessAsync(
                "ready_before_scan_1", lifecycle, source, ownedSession,
                overviewRuntimeRoot, packagePath, versionPath, proofPid, proofStartedAt,
                operationCts.Token, requireHealthy: true).ConfigureAwait(false);
            evidence["readinessBeforeScan1"] = readyBefore1;
            int serverId = readyBefore1.GetProperty("worldContext").GetProperty("serverId").GetInt32();

            JsonElement scanPayload = JsonSerializer.SerializeToElement(new
            {
                profileId,
                selectedTypes = new[] { "resource" },
                scanMode = "normal",
                resume = false,
            }, JsonOptions.Default);

            failedOperation = "map_scan_start_1";
            DateTimeOffset scan1StartedAt = DateTimeOffset.UtcNow;
            JsonElement start1 = ToJson(await mapService.InvokeAsync(
                "map_scan_start", scanPayload, operationCts.Token).ConfigureAwait(false));
            string run1 = RequiredString(start1, "scanRunId");
            JsonElement active1 = await WaitForGenuineActiveScanAsync(
                mapService, run1, probeRuntimeRoot, operationCts.Token).ConfigureAwait(false);
            evidence["scan1"] = new
            {
                startRequestedAt = scan1StartedAt,
                request = new { selectedTypes = new[] { "resource" }, scanMode = "normal", resume = false },
                start = start1,
                active = active1,
                partialReadRequirement = new
                {
                    requestedWherePractical = true,
                    observed = active1.GetProperty("status").GetProperty("readBlocks").GetInt32() > 0,
                    note = "Current 1000x1000 Resource acquisition uses the full-world batch provider; block checkpoints publish only after the batch returns. Correlated live probe command plus a non-completed provider task proves active acquisition before stop.",
                },
            };

            failedOperation = "map_scan_stop_1";
            DateTimeOffset stop1RequestedAt = DateTimeOffset.UtcNow;
            JsonElement stop1 = ToJson(await mapService.InvokeAsync(
                "map_scan_stop", empty.RootElement.Clone(), operationCts.Token).ConfigureAwait(false));
            JsonElement stop1Provider = CaptureProviderState(mapService);
            Map317.MapScanRun? durableRun1 = GetStore(mapService).ReadScanRun(run1);
            RequireStoppedState(stop1, run1, serverId, stop1Provider, durableRun1);
            evidence["stop1"] = new
            {
                requestedAt = stop1RequestedAt,
                completedAt = DateTimeOffset.UtcNow,
                result = stop1,
                provider = stop1Provider,
                durableRun = durableRun1,
                searchAfterStop = await SearchResourceAsync(mapService, serverId, operationCts.Token).ConfigureAwait(false),
            };

            failedOperation = "readiness_after_stop_1";
            JsonElement readyAfterStop1 = await CaptureReadinessAsync(
                "readiness_after_stop_1", lifecycle, source, ownedSession,
                overviewRuntimeRoot, packagePath, versionPath, proofPid, proofStartedAt,
                operationCts.Token, requireHealthy: true).ConfigureAwait(false);
            evidence["readinessAfterStop1"] = readyAfterStop1;

            failedOperation = "map_scan_clear_1";
            JsonElement clearPayload = JsonSerializer.SerializeToElement(new { serverId }, JsonOptions.Default);
            DateTimeOffset clear1RequestedAt = DateTimeOffset.UtcNow;
            JsonElement clear1 = ToJson(await mapService.InvokeAsync(
                "map_scan_clear", clearPayload, operationCts.Token).ConfigureAwait(false));
            JsonElement searchAfterClear1 = await SearchResourceAsync(mapService, serverId, operationCts.Token).ConfigureAwait(false);
            JsonElement summaryAfterClear1 = ToJson(await mapService.InvokeAsync(
                "map_summary", empty.RootElement.Clone(), operationCts.Token).ConfigureAwait(false));
            Map317.MapScanRun? removedRun1 = GetStore(mapService).ReadScanRun(run1);
            RequireClearedState(clear1, searchAfterClear1, summaryAfterClear1, removedRun1);
            evidence["clear1"] = new
            {
                requestedAt = clear1RequestedAt,
                completedAt = DateTimeOffset.UtcNow,
                result = clear1,
                search = searchAfterClear1,
                summary = summaryAfterClear1,
                priorRunStillPresent = removedRun1 is not null,
            };

            failedOperation = "readiness_after_clear_1";
            JsonElement readyAfterClear1 = await CaptureReadinessAsync(
                "readiness_after_clear_1", lifecycle, source, ownedSession,
                overviewRuntimeRoot, packagePath, versionPath, proofPid, proofStartedAt,
                operationCts.Token, requireHealthy: true).ConfigureAwait(false);
            evidence["readinessAfterClear1"] = readyAfterClear1;

            failedOperation = "map_scan_start_2";
            DateTimeOffset scan2StartedAt = DateTimeOffset.UtcNow;
            JsonElement start2 = ToJson(await mapService.InvokeAsync(
                "map_scan_start", scanPayload, operationCts.Token).ConfigureAwait(false));
            string run2 = RequiredString(start2, "scanRunId");
            if (string.Equals(run1, run2, StringComparison.Ordinal))
                throw new InvalidDataException("Second scan reused the first scan run ID.");
            JsonElement active2 = await WaitForGenuineActiveScanAsync(
                mapService, run2, probeRuntimeRoot, operationCts.Token).ConfigureAwait(false);
            JsonElement sameSessionDuring2 = await CaptureReadinessAsync(
                "readiness_during_scan_2", lifecycle, source, ownedSession,
                overviewRuntimeRoot, packagePath, versionPath, proofPid, proofStartedAt,
                operationCts.Token, requireHealthy: true).ConfigureAwait(false);
            evidence["scan2"] = new
            {
                startRequestedAt = scan2StartedAt,
                request = new { selectedTypes = new[] { "resource" }, scanMode = "normal", resume = false },
                start = start2,
                active = active2,
                sameSessionReadiness = sameSessionDuring2,
                newRunId = true,
                sameProfileId = ownedSession.ProfileId == profileId,
                sameInstanceId = sameSessionDuring2.GetProperty("sessionMatchesExpected").GetBoolean(),
                sameGamePid = sameSessionDuring2.GetProperty("gameProcess").GetProperty("pid").GetInt32() == ownedSession.GamePid,
                sameLiveServer = sameSessionDuring2.GetProperty("liveServerId").GetInt32() == serverId,
            };

            failedOperation = "map_scan_stop_2";
            DateTimeOffset stop2RequestedAt = DateTimeOffset.UtcNow;
            JsonElement stop2 = ToJson(await mapService.InvokeAsync(
                "map_scan_stop", empty.RootElement.Clone(), operationCts.Token).ConfigureAwait(false));
            JsonElement stop2Provider = CaptureProviderState(mapService);
            Map317.MapScanRun? durableRun2 = GetStore(mapService).ReadScanRun(run2);
            RequireStoppedState(stop2, run2, serverId, stop2Provider, durableRun2);
            evidence["stop2"] = new
            {
                requestedAt = stop2RequestedAt,
                completedAt = DateTimeOffset.UtcNow,
                result = stop2,
                provider = stop2Provider,
                durableRun = durableRun2,
                searchAfterStop = await SearchResourceAsync(mapService, serverId, operationCts.Token).ConfigureAwait(false),
            };

            failedOperation = "readiness_after_stop_2";
            evidence["readinessAfterStop2"] = await CaptureReadinessAsync(
                "readiness_after_stop_2", lifecycle, source, ownedSession,
                overviewRuntimeRoot, packagePath, versionPath, proofPid, proofStartedAt,
                operationCts.Token, requireHealthy: true).ConfigureAwait(false);

            failedOperation = "map_scan_clear_2";
            DateTimeOffset clear2RequestedAt = DateTimeOffset.UtcNow;
            JsonElement clear2 = ToJson(await mapService.InvokeAsync(
                "map_scan_clear", clearPayload, operationCts.Token).ConfigureAwait(false));
            JsonElement searchAfterClear2 = await SearchResourceAsync(mapService, serverId, operationCts.Token).ConfigureAwait(false);
            JsonElement summaryAfterClear2 = ToJson(await mapService.InvokeAsync(
                "map_summary", empty.RootElement.Clone(), operationCts.Token).ConfigureAwait(false));
            Map317.MapScanRun? removedRun2 = GetStore(mapService).ReadScanRun(run2);
            RequireClearedState(clear2, searchAfterClear2, summaryAfterClear2, removedRun2);
            evidence["clear2"] = new
            {
                requestedAt = clear2RequestedAt,
                completedAt = DateTimeOffset.UtcNow,
                result = clear2,
                search = searchAfterClear2,
                summary = summaryAfterClear2,
                priorRunStillPresent = removedRun2 is not null,
            };

            failedOperation = "final_readiness";
            evidence["finalReadiness"] = await CaptureReadinessAsync(
                "final_readiness", lifecycle, source, ownedSession,
                overviewRuntimeRoot, packagePath, versionPath, proofPid, proofStartedAt,
                operationCts.Token, requireHealthy: true).ConfigureAwait(false);
            evidence["connectionLossReproduced"] = false;
            evidence["state"] = "proven";
        }
        catch (Exception error)
        {
            failure = error;
            evidence["state"] = "failed";
            evidence["failure"] = ErrorObject(error, failedOperation);
            if (lifecycle is not null && source is not null && ownedSession is not null)
            {
                try
                {
                    evidence["connectionLossDiagnostics"] = await CaptureFailureDiagnosticsAsync(
                        failedOperation ?? "unknown",
                        error,
                        lifecycle,
                        source,
                        ownedSession,
                        overviewRuntimeRoot,
                        probeRuntimeRoot,
                        packagePath,
                        versionPath,
                        proofPid,
                        proofStartedAt).ConfigureAwait(false);
                }
                catch (Exception diagnosticError)
                {
                    evidence["connectionLossDiagnosticsError"] = diagnosticError.Message;
                }
            }
        }
        finally
        {
            try { mapService?.Dispose(); }
            catch (Exception error) { evidence["mapServiceDisposeError"] = error.Message; }
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            if (lifecycle is not null && !string.IsNullOrWhiteSpace(instanceId))
            {
                try
                {
                    using var stopCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                    JsonElement stopPayload = JsonSerializer.SerializeToElement(new { instanceId }, JsonOptions.Default);
                    await lifecycle.InvokeAsync(
                        "profile_instance_stop", stopPayload, stopCts.Token).ConfigureAwait(false);
                }
                catch (Exception stopError)
                {
                    evidence["sessionStopError"] = ErrorObject(stopError, "profile_instance_stop_cleanup");
                    failure ??= stopError;
                    evidence["state"] = "failed";
                }
            }
            lifecycle?.Dispose();

            JsonElement cleanupProcesses = CaptureRelevantProcesses(proofPid, gamePid, proofStartedAt);
            bool tempDatabaseDeleted = TryDeleteDatabase(databasePath);
            evidence["cleanupBeforeProofExit"] = new
            {
                capturedAt = DateTimeOffset.UtcNow,
                processes = cleanupProcesses,
                expectedProofSelfStillRunning = true,
                ownedGamePid = gamePid,
                ownedGamePidAlive = gamePid is int pid && IsProcessAlive(pid),
                lastWarRuntimeOrphanCount = CountRuntimeOrphans(cleanupProcesses, proofPid),
                recoveryJournal = ReadRecoveryJournal(overviewRuntimeRoot),
                finalPackageSha256 = File.Exists(packagePath) ? Sha256File(packagePath) : "missing",
                finalPackageRestoredToOfficialV22 = File.Exists(packagePath) &&
                    string.Equals(Sha256File(packagePath), OfficialV22Sha256, StringComparison.OrdinalIgnoreCase),
                finalVersionMarker = ReadText(versionPath),
                tempDatabaseDeleted,
                tempDatabaseArtifacts = DatabaseArtifacts(databasePath),
            };
            evidence["proofFinishedAt"] = DateTimeOffset.UtcNow;
            await File.WriteAllTextAsync(
                outputPath,
                JsonSerializer.Serialize(evidence, new JsonSerializerOptions(JsonOptions.Default) { WriteIndented = true }));
        }

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            ok = failure is null,
            taskId = TaskId,
            outputPath,
            state = evidence["state"],
        }, JsonOptions.Default));
        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static async Task<OverviewMapScanSession> WaitForMapReadyAsync(
        OverviewLifecycleService lifecycle,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMinutes(4);
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OverviewMapScanSession? session = lifecycle.GetReadyMapScanSession();
            if (session is not null && lifecycle.GetLiveServerId() is > 0)
                return session;
            object? status = lifecycle.CreateProfileInstanceStatus();
            if (status is not null)
            {
                JsonElement json = ToJson(status);
                string? error = OptionalString(json, "lastError");
                if (!string.IsNullOrWhiteSpace(error))
                    throw new InvalidOperationException("Owned lifecycle entered an error state: " + error);
            }
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }
        throw new TimeoutException("Owned Last War lifecycle did not reach Map-ready state.");
    }

    private static async Task<JsonElement> CaptureReadinessAsync(
        string label,
        OverviewLifecycleService lifecycle,
        CurrentClientMapBlockSource source,
        OverviewMapScanSession expected,
        string overviewRuntimeRoot,
        string packagePath,
        string versionPath,
        int proofPid,
        DateTimeOffset proofStartedAt,
        CancellationToken cancellationToken,
        bool requireHealthy)
    {
        DateTimeOffset capturedAt = DateTimeOffset.UtcNow;
        OverviewMapScanSession? current = lifecycle.GetReadyMapScanSession();
        bool sessionMatches = current == expected;
        int? liveServerId = lifecycle.GetLiveServerId();
        bool healthy = false;
        CurrentClientMapContext? context = null;
        string? healthError = null;
        try
        {
            if (requireHealthy)
                await lifecycle.WaitForHealthyMapScanSessionAsync(expected, cancellationToken).ConfigureAwait(false);
            context = await source.GetCurrentContextAsync(cancellationToken).ConfigureAwait(false);
            healthy = true;
        }
        catch (Exception error)
        {
            healthError = error.Message;
            if (requireHealthy) throw;
        }

        JsonElement process = ReadProcessIdentity(expected.GamePid);
        JsonElement heartbeat = ReadHeartbeat(overviewRuntimeRoot);
        JsonElement value = JsonSerializer.SerializeToElement(new
        {
            label,
            capturedAt,
            sessionMatchesExpected = sessionMatches,
            readySession = current is null ? null : new
            {
                current.ProfileId,
                instanceId = current.SessionId,
                current.GamePid,
                current.GamePath,
                current.GameStartedAtUtc,
            },
            profileInstanceStatus = lifecycle.CreateProfileInstanceStatus(),
            healthyMapHeartbeat = healthy,
            healthError,
            liveServerId,
            worldContext = context is null ? null : new
            {
                context.ServerId,
                context.WorldId,
                context.TileWidth,
                context.TileHeight,
                context.PlayerTileX,
                context.PlayerTileY,
                context.LaunchSessionId,
            },
            gameProcess = process,
            heartbeat,
            packageSha256 = Sha256File(packagePath),
            packageState = ClassifyPackage(Sha256File(packagePath)),
            versionMarker = ReadText(versionPath),
            recoveryJournal = ReadRecoveryJournal(overviewRuntimeRoot),
            processes = CaptureRelevantProcesses(proofPid, expected.GamePid, proofStartedAt),
        }, JsonOptions.Default);

        if (requireHealthy)
        {
            if (!sessionMatches || !healthy || liveServerId is null || context is null ||
                liveServerId != context.ServerId || context.LaunchSessionId != expected.SessionId ||
                process.GetProperty("alive").GetBoolean() == false)
                throw new InvalidDataException("Map readiness continuity check failed for " + label + ".");
        }
        return value.Clone();
    }

    private static async Task<JsonElement> WaitForGenuineActiveScanAsync(
        Map317CommandService service,
        string runId,
        string probeRuntimeRoot,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(20);
        JsonElement lastStatus = default;
        JsonElement lastProvider = default;
        JsonElement lastProbe = default;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lastStatus = ToJson(service.CreateStatus());
            lastProvider = CaptureProviderState(service);
            lastProbe = ReadCorrelatedProbeCommand(probeRuntimeRoot, runId);
            bool reading = lastStatus.GetProperty("isReading").GetBoolean();
            string phase = RequiredString(lastStatus, "phase");
            bool providerRunning = lastProvider.GetProperty("activeTaskPresent").GetBoolean() &&
                                   !lastProvider.GetProperty("activeTaskCompleted").GetBoolean();
            bool probeCorrelated = lastProbe.GetProperty("correlated").GetBoolean();
            if (reading && phase == "scanning" && providerRunning && probeCorrelated)
            {
                return JsonSerializer.SerializeToElement(new
                {
                    capturedAt = DateTimeOffset.UtcNow,
                    status = lastStatus,
                    provider = lastProvider,
                    liveProbeCommand = lastProbe,
                    genuinelyActive = true,
                }, JsonOptions.Default).Clone();
            }
            if (!reading && phase is "completed" or "failed" or "idle")
                throw new InvalidDataException(
                    $"Scan {runId} became terminal before a correlated active acquisition could be stopped: {phase}.");
            await Task.Delay(25, cancellationToken).ConfigureAwait(false);
        }
        throw new TimeoutException(
            "Scan did not expose a correlated live acquisition command while its provider task was active. " +
            $"status={lastStatus.GetRawText()}, provider={lastProvider.GetRawText()}, probe={lastProbe.GetRawText()}");
    }

    private static void RequireStoppedState(
        JsonElement result,
        string runId,
        int serverId,
        JsonElement provider,
        Map317.MapScanRun? durableRun)
    {
        if (result.GetProperty("isReading").GetBoolean() ||
            RequiredString(result, "phase") != "idle" ||
            RequiredString(result, "scanRunId") != runId ||
            result.GetProperty("serverId").GetInt32() != serverId ||
            result.GetProperty("inflightBlocks").GetInt32() != 0 ||
            OptionalString(result, "lastError") is not null)
            throw new InvalidDataException("map_scan_stop did not return the recovered idle-with-run-identity state.");
        if (provider.GetProperty("activeTaskPresent").GetBoolean() ||
            provider.GetProperty("activeCancellationPresent").GetBoolean() ||
            provider.GetProperty("pendingAcceptedRunPresent").GetBoolean())
            throw new InvalidDataException("map_scan_stop returned while acquisition ownership was still active.");
        if (durableRun is null || !string.Equals(durableRun.Status, "cancelled", StringComparison.Ordinal))
            throw new InvalidDataException("map_scan_stop did not persist the active run as cancelled.");
    }

    private static void RequireClearedState(
        JsonElement clear,
        JsonElement search,
        JsonElement summary,
        Map317.MapScanRun? priorRun)
    {
        if (clear.GetProperty("isReading").GetBoolean() || RequiredString(clear, "phase") != "idle" ||
            clear.GetProperty("serverId").GetInt32() != 0 || RequiredString(clear, "scanRunId").Length != 0 ||
            search.GetProperty("total").GetInt32() != 0 || priorRun is not null)
            throw new InvalidDataException("map_scan_clear did not fully reset the stopped scan state/data.");
        if (summary.GetProperty("serverId").GetInt32() != 0)
            throw new InvalidDataException("map_scan_clear summary retained a live server ID.");
        foreach (JsonProperty pair in summary.GetProperty("counts").EnumerateObject())
            if (pair.Value.GetInt32() != 0)
                throw new InvalidDataException("map_scan_clear summary retained Map data for " + pair.Name + ".");
    }

    private static async Task<JsonElement> SearchResourceAsync(
        Map317CommandService service,
        int serverId,
        CancellationToken cancellationToken)
    {
        JsonElement payload = JsonSerializer.SerializeToElement(new
        {
            kind = "resource",
            query = new
            {
                serverId,
                keyword = string.Empty,
                page = 1,
                pageSize = 3,
                sorts = new[] { new { sortBy = "updatedAt", sortOrder = "desc" } },
            },
        }, JsonOptions.Default);
        return ToJson(await service.InvokeAsync("map_search", payload, cancellationToken).ConfigureAwait(false));
    }

    private static JsonElement CaptureProviderState(Map317CommandService service)
    {
        CurrentClientMap317ScanProvider provider = GetProvider(service);
        Type type = typeof(CurrentClientMap317ScanProvider);
        Task? activeTask = (Task?)RequireField(type, "activeTask").GetValue(provider);
        CancellationTokenSource? activeCancellation =
            (CancellationTokenSource?)RequireField(type, "activeCancellation").GetValue(provider);
        object? pending = RequireField(type, "pending").GetValue(provider);
        return JsonSerializer.SerializeToElement(new
        {
            capturedAt = DateTimeOffset.UtcNow,
            activeTaskPresent = activeTask is not null,
            activeTaskCompleted = activeTask?.IsCompleted ?? false,
            activeTaskStatus = activeTask?.Status.ToString(),
            activeCancellationPresent = activeCancellation is not null,
            cancellationRequested = activeCancellation?.IsCancellationRequested ?? false,
            pendingAcceptedRunPresent = pending is not null,
        }, JsonOptions.Default).Clone();
    }

    private static CurrentClientMap317ScanProvider GetProvider(Map317CommandService service) =>
        (CurrentClientMap317ScanProvider)(RequireField(typeof(Map317CommandService), "scanProvider").GetValue(service)
            ?? throw new InvalidOperationException("Map317 scan provider field is null."));

    private static Map317.MapStore GetStore(Map317CommandService service) =>
        (Map317.MapStore)(RequireField(typeof(Map317CommandService), "store").GetValue(service)
            ?? throw new InvalidOperationException("Map317 store field is null."));

    private static FieldInfo RequireField(Type type, string name) =>
        type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException($"Required diagnostic field {type.FullName}.{name} was not found.");

    private static JsonElement ReadCorrelatedProbeCommand(string probeRuntimeRoot, string runId)
    {
        string path = Path.Combine(probeRuntimeRoot, "bulk-aoi-diagnostic.txt");
        if (!File.Exists(path))
            return JsonSerializer.SerializeToElement(new { path, exists = false, correlated = false }, JsonOptions.Default);
        try
        {
            string text = ReadSharedText(path);
            return JsonSerializer.SerializeToElement(new
            {
                path,
                exists = true,
                lastWriteTimeUtc = File.GetLastWriteTimeUtc(path),
                correlated = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                    .Any(line => string.Equals(line, "scanRunId=" + runId, StringComparison.Ordinal)),
                requestId = ReadKeyValue(text, "requestId"),
                scanRunId = ReadKeyValue(text, "scanRunId"),
                serverId = ReadKeyValue(text, "serverId"),
            }, JsonOptions.Default);
        }
        catch (Exception error)
        {
            return JsonSerializer.SerializeToElement(new
            {
                path,
                exists = true,
                correlated = false,
                error = error.Message,
            }, JsonOptions.Default);
        }
    }

    private static async Task<object> CaptureFailureDiagnosticsAsync(
        string operation,
        Exception error,
        OverviewLifecycleService lifecycle,
        CurrentClientMapBlockSource source,
        OverviewMapScanSession expected,
        string overviewRuntimeRoot,
        string probeRuntimeRoot,
        string packagePath,
        string versionPath,
        int proofPid,
        DateTimeOffset proofStartedAt)
    {
        object? profileStatus = null;
        try { profileStatus = lifecycle.CreateProfileInstanceStatus(); } catch { }
        OverviewMapScanSession? ready = null;
        try { ready = lifecycle.GetReadyMapScanSession(); } catch { }
        int? liveServerId = null;
        try { liveServerId = lifecycle.GetLiveServerId(); } catch { }
        object? world = null;
        object? worldError = null;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            CurrentClientMapContext context = await source.GetCurrentContextAsync(cts.Token).ConfigureAwait(false);
            world = new
            {
                context.ServerId,
                context.WorldId,
                context.TileWidth,
                context.TileHeight,
                context.PlayerTileX,
                context.PlayerTileY,
                context.LaunchSessionId,
            };
        }
        catch (Exception contextError)
        {
            worldError = ErrorObject(contextError, "diagnostic_world_context");
        }

        return new
        {
            capturedAt = DateTimeOffset.UtcNow,
            operationImmediatelyPrecedingLoss = operation,
            exception = ErrorObject(error, operation),
            expectedSession = new
            {
                expected.ProfileId,
                instanceId = expected.SessionId,
                expected.GamePid,
                expected.GamePath,
                expected.GameStartedAtUtc,
            },
            readySession = ready,
            readySessionStillMatchesExpected = ready == expected,
            profileInstanceStatus = profileStatus,
            liveServerId,
            gameProcess = ReadProcessIdentity(expected.GamePid),
            heartbeat = ReadHeartbeat(overviewRuntimeRoot),
            worldContext = world,
            worldContextError = worldError,
            packageSha256 = File.Exists(packagePath) ? Sha256File(packagePath) : "missing",
            packageState = File.Exists(packagePath) ? ClassifyPackage(Sha256File(packagePath)) : "missing",
            versionMarker = ReadText(versionPath),
            recoveryJournal = ReadRecoveryJournal(overviewRuntimeRoot),
            runtimeFiles = ReadRuntimeFileInventory(overviewRuntimeRoot, probeRuntimeRoot),
            relevantPlayerLogTail = ReadRelevantPlayerLogTail(),
            processes = CaptureRelevantProcesses(proofPid, expected.GamePid, proofStartedAt),
        };
    }

    private static JsonElement ReadProcessIdentity(int pid)
    {
        try
        {
            using Process process = Process.GetProcessById(pid);
            return JsonSerializer.SerializeToElement(new
            {
                pid,
                alive = !process.HasExited,
                processName = process.ProcessName,
                path = SafeProcessPath(process),
                startTimeUtc = SafeStartTimeUtc(process),
            }, JsonOptions.Default).Clone();
        }
        catch (Exception error)
        {
            return JsonSerializer.SerializeToElement(new
            {
                pid,
                alive = false,
                error = error.Message,
            }, JsonOptions.Default).Clone();
        }
    }

    private static JsonElement CaptureRelevantProcesses(
        int proofPid,
        int? expectedGamePid,
        DateTimeOffset proofStartedAt)
    {
        string command = """
            $names=@('LastWar.exe','LastWarLauncher.exe','LWBridge.exe','LWBridge.Desktop.exe','LWBridge.Desktop.Checks.exe');
            @(Get-CimInstance Win32_Process | Where-Object { $names -contains $_.Name } | Select-Object ProcessId,ParentProcessId,Name,CreationDate,ExecutablePath,CommandLine) | ConvertTo-Json -Compress -Depth 4
            """;
        string raw = RunPowerShell(command);
        JsonElement[] items = ParseProcessArray(raw);
        var classified = new List<object>();
        foreach (JsonElement item in items)
        {
            int pid = item.GetProperty("ProcessId").GetInt32();
            string name = item.GetProperty("Name").GetString() ?? string.Empty;
            string role;
            bool taskOwned;
            bool expectedInfrastructure;
            string matchReason = "process_name";
            if (pid == proofPid)
            {
                role = "proof_self";
                taskOwned = true;
                expectedInfrastructure = true;
            }
            else if (expectedGamePid == pid)
            {
                role = "owned_lastwar";
                taskOwned = true;
                expectedInfrastructure = false;
            }
            else if (string.Equals(name, "LastWarLauncher.exe", StringComparison.OrdinalIgnoreCase))
            {
                role = "launcher_seen_during_owned_proof";
                taskOwned = true;
                expectedInfrastructure = false;
            }
            else if (string.Equals(name, "LastWar.exe", StringComparison.OrdinalIgnoreCase))
            {
                role = "unexpected_lastwar";
                taskOwned = false;
                expectedInfrastructure = false;
            }
            else
            {
                role = "unexpected_lwbridge_or_checker";
                taskOwned = false;
                expectedInfrastructure = false;
            }
            classified.Add(new
            {
                pid,
                parentPid = item.TryGetProperty("ParentProcessId", out JsonElement parent) && parent.TryGetInt32(out int parentPid)
                    ? parentPid : (int?)null,
                name,
                creationDate = item.TryGetProperty("CreationDate", out JsonElement created) ? created.GetString() : null,
                executablePath = item.TryGetProperty("ExecutablePath", out JsonElement executable) && executable.ValueKind == JsonValueKind.String
                    ? executable.GetString() : null,
                commandLine = item.TryGetProperty("CommandLine", out JsonElement commandLine) && commandLine.ValueKind == JsonValueKind.String
                    ? commandLine.GetString() : null,
                matchReason,
                role,
                taskOwned,
                expectedInfrastructure,
                proofStartedAt,
            });
        }
        return JsonSerializer.SerializeToElement(classified, JsonOptions.Default).Clone();
    }

    private static void RequireNoPreexistingRuntime(JsonElement processes, int proofPid)
    {
        foreach (JsonElement item in processes.EnumerateArray())
        {
            if (item.GetProperty("pid").GetInt32() == proofPid) continue;
            throw new InvalidOperationException(
                "Pre-existing Last War/LWBridge/check process makes session ownership ambiguous: " + item.GetRawText());
        }
    }

    private static int CountRuntimeOrphans(JsonElement processes, int proofPid)
    {
        int count = 0;
        foreach (JsonElement item in processes.EnumerateArray())
        {
            if (item.GetProperty("pid").GetInt32() == proofPid) continue;
            string name = RequiredString(item, "name");
            if (name is "LastWar.exe" or "LastWarLauncher.exe" or "LWBridge.exe" or "LWBridge.Desktop.exe" or "LWBridge.Desktop.Checks.exe")
                count++;
        }
        return count;
    }

    private static string RunPowerShell(string command)
    {
        var start = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-Command");
        start.ArgumentList.Add(command);
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Failed to start PowerShell diagnostic process.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException("PowerShell diagnostic failed: " + error);
        return output.Trim();
    }

    private static JsonElement[] ParseProcessArray(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<JsonElement>();
        using JsonDocument document = JsonDocument.Parse(raw);
        JsonElement root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Array)
            return root.EnumerateArray().Select(item => item.Clone()).ToArray();
        if (root.ValueKind == JsonValueKind.Object)
            return new[] { root.Clone() };
        return Array.Empty<JsonElement>();
    }

    private static JsonElement ReadHeartbeat(string overviewRuntimeRoot)
    {
        string path = Path.Combine(overviewRuntimeRoot, "heartbeat.json");
        if (!File.Exists(path))
            return JsonSerializer.SerializeToElement(new { path, exists = false }, JsonOptions.Default);
        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
            JsonElement root = document.RootElement;
            return JsonSerializer.SerializeToElement(new
            {
                path,
                exists = true,
                fileLastWriteTimeUtc = File.GetLastWriteTimeUtc(path),
                profileId = OptionalString(root, "profileId"),
                sessionId = OptionalString(root, "sessionId"),
                gamePid = OptionalInt(root, "gamePid"),
                updatedAt = OptionalLong(root, "updatedAt"),
                connected = OptionalBool(root, "connected"),
                gameReady = OptionalBool(root, "gameReady"),
                loggedIn = OptionalBool(root, "loggedIn"),
                ready = OptionalBool(root, "ready"),
                serverId = OptionalInt(root, "serverId"),
                bridgeVersion = OptionalString(root, "bridgeVersion"),
            }, JsonOptions.Default).Clone();
        }
        catch (Exception error)
        {
            return JsonSerializer.SerializeToElement(new { path, exists = true, error = error.Message }, JsonOptions.Default);
        }
    }

    private static object ReadRecoveryJournal(string overviewRuntimeRoot)
    {
        string path = Path.Combine(overviewRuntimeRoot, "recovery.json");
        if (!File.Exists(path)) return new { path, present = false };
        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
            JsonElement root = document.RootElement;
            return new
            {
                path,
                present = true,
                stage = OptionalString(root, "stage"),
                profileId = OptionalString(root, "profileId"),
                sessionId = OptionalString(root, "sessionId"),
                gamePid = OptionalInt(root, "gamePid"),
                updatedAtUtc = OptionalString(root, "updatedAtUtc"),
            };
        }
        catch (Exception error)
        {
            return new { path, present = true, error = error.Message };
        }
    }

    private static object ReadRuntimeFileInventory(string overviewRuntimeRoot, string probeRuntimeRoot)
    {
        string[] overviewNames =
        [
            "ready.json", "heartbeat.json", "recovery.json", "lease.txt", "control.txt",
            "world-ready-result.json", "world-state-result.json", "pipe-transport.json",
        ];
        string[] probeNames =
        [
            "bulk-aoi-diagnostic.txt", "bulk-aoi-diagnostic-result.json",
            "resource-scan-detail.txt", "resource-scan-detail-result.json",
        ];
        return new
        {
            overview = overviewNames.Select(name => FileInfoObject(Path.Combine(overviewRuntimeRoot, name))).ToArray(),
            probe = probeNames.Select(name => FileInfoObject(Path.Combine(probeRuntimeRoot, name))).ToArray(),
        };
    }

    private static object FileInfoObject(string path)
    {
        var info = new FileInfo(path);
        return new
        {
            path,
            exists = info.Exists,
            length = info.Exists ? info.Length : 0,
            lastWriteTimeUtc = info.Exists ? info.LastWriteTimeUtc : (DateTime?)null,
        };
    }

    private static string[] ReadRelevantPlayerLogTail()
    {
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string appData = Directory.GetParent(local)?.FullName ?? string.Empty;
        string path = Path.Combine(appData, "LocalLow", "FunFly", "Last War-Survival Game", "Player.log");
        if (!File.Exists(path)) return Array.Empty<string>();
        try
        {
            string[] lines = File.ReadAllLines(path);
            return lines.TakeLast(600)
                .Where(line => line.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                               line.Contains("exception", StringComparison.OrdinalIgnoreCase) ||
                               line.Contains("disconnect", StringComparison.OrdinalIgnoreCase) ||
                               line.Contains("quit", StringComparison.OrdinalIgnoreCase) ||
                               line.Contains("exit", StringComparison.OrdinalIgnoreCase) ||
                               line.Contains("LWBridge", StringComparison.OrdinalIgnoreCase) ||
                               line.Contains("Curl", StringComparison.OrdinalIgnoreCase))
                .TakeLast(80)
                .ToArray();
        }
        catch (Exception error)
        {
            return new[] { "Player.log diagnostic read failed: " + error.Message };
        }
    }

    private static object ErrorObject(Exception error, string? operation) => new
    {
        capturedAt = DateTimeOffset.UtcNow,
        operation,
        type = error.GetType().FullName,
        code = error is BridgeCommandException bridge ? bridge.Code : null,
        error.Message,
    };

    private static string[] DatabaseArtifacts(string databasePath) =>
        new[] { databasePath, databasePath + "-wal", databasePath + "-shm" }
            .Where(File.Exists)
            .ToArray();

    private static bool TryDeleteDatabase(string path)
    {
        bool ok = true;
        foreach (string candidate in new[] { path, path + "-wal", path + "-shm" })
        {
            try { if (File.Exists(candidate)) File.Delete(candidate); }
            catch { ok = false; }
        }
        return ok && DatabaseArtifacts(path).Length == 0;
    }

    private static bool IsProcessAlive(int pid)
    {
        try
        {
            using Process process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch { return false; }
    }

    private static string? SafeProcessPath(Process process)
    {
        try { return process.MainModule?.FileName; } catch { return null; }
    }

    private static DateTime? SafeStartTimeUtc(Process process)
    {
        try { return process.StartTime.ToUniversalTime(); } catch { return null; }
    }

    private static string Sha256File(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string ClassifyPackage(string sha256) =>
        string.Equals(sha256, OfficialV22Sha256, StringComparison.OrdinalIgnoreCase)
            ? "validated_official_v22"
            : string.Equals(sha256, WrappedV22Sha256, StringComparison.OrdinalIgnoreCase)
                ? "validated_overview_wrapped_v22"
                : "unexpected";

    private static string? ReadText(string path) => File.Exists(path) ? File.ReadAllText(path).Trim() : null;

    private static string ReadSharedText(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string? ReadKeyValue(string text, string key)
    {
        string prefix = key + "=";
        return text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(line => line.StartsWith(prefix, StringComparison.Ordinal))?[prefix.Length..];
    }

    private static JsonElement ToJson(object? value) => JsonSerializer.SerializeToElement(value, JsonOptions.Default).Clone();

    private static string RequiredString(JsonElement root, string name) =>
        root.GetProperty(name).GetString() ?? throw new InvalidDataException($"{name} is null.");

    private static string? OptionalString(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out JsonElement value) &&
        value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static int? OptionalInt(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out JsonElement value) && value.TryGetInt32(out int parsed)
            ? parsed : null;

    private static long? OptionalLong(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out JsonElement value) && value.TryGetInt64(out long parsed)
            ? parsed : null;

    private static bool? OptionalBool(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out JsonElement value) &&
        value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : null;
}
