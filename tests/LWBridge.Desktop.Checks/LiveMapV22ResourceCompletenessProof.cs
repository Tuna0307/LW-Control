using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

/// <summary>
/// Future live-only consumer for the offline-prepared Resource completeness
/// diagnostics. This type is compiled/tested offline but is never invoked by the
/// LWB317-MAP-RESOURCE-COMPLETENESS-PREP-001 task.
/// </summary>
internal static class LiveMapV22ResourceCompletenessProof
{
    private const string TaskId = "LWB317-LIVE-MAP-V22-RESOURCE-COMPLETENESS-001";
    private const string OfficialV22Sha256 =
        "248f3aeac712b3f14f86bff37a0c365e467897a2248403837c44c1b774f05b22";
    private const string WrappedV22Sha256 =
        "a705d2d64a44081012c4c7e30bda613636960c98e40fa7a367a60577ce3a7d72";
    private const string ResourceCompletenessWrappedV22Sha256 =
        "9a0f28f79c8b48fe21d45fbc649df6833d976d99232e344854399f65c7ba187d";
    private const string GameSha256 =
        "905c98c1f89841f90b492556192ba0642f3d209a873cb8c1f7b3c340aca0733d";

    internal static async Task RunAsync(string outputPath)
    {
        outputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        string stem = Path.GetFileNameWithoutExtension(outputPath);
        string outputDirectory = Path.GetDirectoryName(outputPath)!;
        string sourceReportPath = Path.Combine(outputDirectory, stem + ".resource-source-report.json");
        string publishedRowsPath = Path.Combine(outputDirectory, stem + ".resource-published-rows.json");

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string appData = Directory.GetParent(localAppData)?.FullName
            ?? throw new InvalidOperationException("AppData root is unavailable.");
        string gameRoot = Path.Combine(localAppData, "FunFly", "Last War-Survival Game");
        string gamePath = Path.Combine(gameRoot, "Game", "LastWar.exe");
        string scriptRoot = Path.Combine(appData, "LocalLow", "FunFly", "Last War-Survival Game", "lwScripts");
        string packagePath = Path.Combine(scriptRoot, "LWScripts.data");
        string versionPath = Path.Combine(scriptRoot, "version.txt");
        string probeRuntimeRoot = Path.Combine(localAppData, "LWBridgeRebuild", "live-resource");
        Directory.CreateDirectory(probeRuntimeRoot);
        string profileId = "lwb317-resource-completeness-" + Guid.NewGuid().ToString("N")[..12];
        string databasePath = Path.Combine(
            probeRuntimeRoot,
            "lwb317-resource-completeness-" + Guid.NewGuid().ToString("N") + ".db");

        DateTimeOffset proofStartedAt = DateTimeOffset.UtcNow;
        var evidence = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schemaVersion"] = 1,
            ["taskId"] = TaskId,
            ["proofStartedAt"] = proofStartedAt,
            ["scope"] = new
            {
                resourceOnly = true,
                serverJump = false,
                restartResume = false,
                unrelatedGameplay = false,
            },
        };

        Exception? failure = null;
        OverviewLifecycleService? lifecycle = null;
        Map317CommandService? mapService = null;
        string? instanceId = null;
        int? ownedGamePid = null;

        try
        {
            RequireNoPreexistingGameRuntime();
            string packageBefore = Sha256File(packagePath);
            string gameHash = Sha256File(gamePath);
            if (!string.Equals(packageBefore, OfficialV22Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    $"Resource completeness proof requires pristine official v22 package {OfficialV22Sha256}, observed {packageBefore}.");
            if (!string.Equals(gameHash, GameSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("LastWar.exe identity changed before Resource completeness proof.");
            evidence["preflight"] = new
            {
                packageSha256 = packageBefore,
                versionMarker = ReadText(versionPath),
                gameSha256 = gameHash,
                noPreexistingGameRuntime = true,
            };

            lifecycle = new OverviewLifecycleService(profileId, gameRoot);
            using var operationCts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
            using JsonDocument empty = JsonDocument.Parse("{}");
            _ = await lifecycle.InvokeAsync(
                "profile_instance_start", empty.RootElement.Clone(), operationCts.Token).ConfigureAwait(false);
            OverviewMapScanSession session = await WaitForMapReadyAsync(lifecycle, operationCts.Token).ConfigureAwait(false);
            await lifecycle.WaitForHealthyMapScanSessionAsync(session, operationCts.Token).ConfigureAwait(false);
            instanceId = session.SessionId;
            ownedGamePid = session.GamePid;
            evidence["ownedSession"] = new
            {
                profileId = session.ProfileId,
                instanceId = session.SessionId,
                gamePid = session.GamePid,
                gamePath = session.GamePath,
                gameStartedAtUtc = session.GameStartedAtUtc,
                healthyHeartbeat = true,
            };

            // Keep the readiness/context probe ahead of Map317CommandService creation.
            // The service starts scheduled action workers immediately; creating a second
            // CurrentClientMapBlockSource beside those workers would otherwise introduce
            // a harness-only race on the shared single-slot world-state transport.
            var source = new CurrentClientMapBlockSource(lifecycle);
            CurrentClientMapContext context = await source.GetCurrentContextAsync(operationCts.Token).ConfigureAwait(false);
            mapService = new Map317CommandService(databasePath, lifecycle);
            evidence["session"] = new
            {
                profileId = session.ProfileId,
                instanceId = session.SessionId,
                gamePid = session.GamePid,
                gamePath = session.GamePath,
                gameStartedAtUtc = session.GameStartedAtUtc,
                serverId = context.ServerId,
                worldId = context.WorldId,
                tileWidth = context.TileWidth,
                tileHeight = context.TileHeight,
                playerTileX = context.PlayerTileX,
                playerTileY = context.PlayerTileY,
                runtimePackageSha256 = Sha256File(packagePath),
                runtimePackageState = ClassifyPackage(Sha256File(packagePath)),
                runtimeVersionMarker = ReadText(versionPath),
            };

            JsonElement scanPayload = JsonSerializer.SerializeToElement(new
            {
                profileId,
                selectedTypes = new[] { "resource" },
                scanMode = "fast",
                resume = false,
            }, JsonOptions.Default);
            JsonElement start = ToJson(await mapService.InvokeAsync(
                "map_scan_start", scanPayload, operationCts.Token).ConfigureAwait(false));
            string runId = start.GetProperty("scanRunId").GetString()
                ?? throw new InvalidDataException("Resource completeness scan start omitted scanRunId.");
            JsonElement final = await WaitForCompletedScanAsync(mapService, operationCts.Token).ConfigureAwait(false);
            ResourceCompletenessReport report = mapService.LastResourceCompletenessReport
                ?? throw new InvalidDataException("Completed Resource scan did not publish completeness diagnostics.");
            if (!string.Equals(report.ScanRunId, runId, StringComparison.Ordinal) ||
                report.ServerId != context.ServerId || report.FinalAcceptedUniqueRecords <= 0)
                throw new InvalidDataException("Resource completeness report identity/count does not match the completed scan.");

            JsonElement[] publishedRows = await ReadAllPublishedResourcesAsync(
                mapService, context.ServerId, operationCts.Token).ConfigureAwait(false);
            JsonElement summary = ToJson(await mapService.InvokeAsync(
                "map_summary", empty.RootElement.Clone(), operationCts.Token).ConfigureAwait(false));
            JsonElement optionsPayload = JsonSerializer.SerializeToElement(new { serverId = context.ServerId }, JsonOptions.Default);
            JsonElement options = ToJson(await mapService.InvokeAsync(
                "map_data_options", optionsPayload, operationCts.Token).ConfigureAwait(false));
            int summaryResourceCount = ReadResourceCount(summary);
            int optionsResourceCount = ReadResourceCount(options);

            if (publishedRows.Length != report.FinalAcceptedUniqueRecords)
                throw new InvalidDataException(
                    $"Published Resource rows {publishedRows.Length} != diagnostic unique accepted {report.FinalAcceptedUniqueRecords}.");
            if (summaryResourceCount != report.FinalAcceptedUniqueRecords ||
                optionsResourceCount != report.FinalAcceptedUniqueRecords)
                throw new InvalidDataException(
                    $"Resource publication counts disagree: source={report.FinalAcceptedUniqueRecords}, " +
                    $"search={publishedRows.Length}, summary={summaryResourceCount}, options={optionsResourceCount}.");
            if (!report.RawObservationCountersReconcile || !report.FinalDeduplicationReconciles ||
                !report.AcceptedDistributionsReconcile)
                throw new InvalidDataException("Resource completeness accounting did not reconcile.");

            await File.WriteAllTextAsync(sourceReportPath, report.ToStableJson(), operationCts.Token).ConfigureAwait(false);
            JsonElement[] canonicalPublished = publishedRows
                .OrderBy(row => ReadString(row, "recordKey"), StringComparer.Ordinal)
                .Select(row => row.Clone())
                .ToArray();
            await File.WriteAllTextAsync(
                publishedRowsPath,
                JsonSerializer.Serialize(new
                {
                    schemaVersion = 1,
                    taskId = TaskId,
                    scanRunId = runId,
                    serverId = context.ServerId,
                    count = canonicalPublished.Length,
                    rows = canonicalPublished,
                }, new JsonSerializerOptions(JsonOptions.Default) { WriteIndented = true }),
                operationCts.Token).ConfigureAwait(false);

            evidence["scan"] = new { request = new { selectedTypes = new[] { "resource" }, scanMode = "fast", resume = false }, start, final };
            evidence["resourceCompleteness"] = new
            {
                scope = "current_client_acquisition_path",
                report.RawObservedPointInfoOccurrences,
                report.NonResourcePointOccurrences,
                report.ResourceCandidateOccurrences,
                report.AcceptedResourceOccurrencesBeforeDeduplication,
                report.RejectedResourceCandidateOccurrences,
                report.DuplicateAcceptedRecordKeyOccurrences,
                report.FinalAcceptedUniqueRecords,
                report.ResourceSourceLookupHitOccurrences,
                report.ResourceSourceLookupFallbackOccurrences,
                resourceDetail = new
                {
                    report.ResourceDetailTargetCount,
                    report.ResourceDetailRequestCount,
                    report.ResourceDetailCacheBeforeCount,
                    report.ResourceDetailSendFailureCount,
                    report.ResourceDetailReadyCount,
                    report.ResourceDetailError,
                },
                report.RawObservationCountersReconcile,
                report.FinalDeduplicationReconciles,
                report.AcceptedDistributionsReconcile,
                report.PopulatedNativeAoiBlocks,
                report.ZeroResourceNativeAoiBlocks,
                report.MinResourcesPerPopulatedNativeAoiBlock,
                report.MaxResourcesPerPopulatedNativeAoiBlock,
                boundingBox = new { report.MinX, report.MinY, report.MaxX, report.MaxY },
                report.RawRuntimeClassOccurrences,
                report.RawPointTypeOccurrences,
                report.RejectionReasonOccurrences,
                report.LevelDistribution,
                report.ResourceNameKeyDistribution,
                report.ResourceTypeDistribution,
                report.AcceptedRawPointTypeDistribution,
                report.BlackTileDistribution,
                report.OccupancyDistribution,
                report.ResourceDetailDistribution,
                report.ServerDistribution,
                report.SourceServerDistribution,
                report.WorldDistribution,
                report.ResourcesByNativeAoiIndex,
                sourceReport = new
                {
                    path = Path.GetFileName(sourceReportPath),
                    sha256 = Sha256File(sourceReportPath),
                    acceptedRows = report.AcceptedRows.Count,
                },
                publishedRows = new
                {
                    path = Path.GetFileName(publishedRowsPath),
                    sha256 = Sha256File(publishedRowsPath),
                    count = canonicalPublished.Length,
                },
                publishedSummary = summary,
                publishedOptions = options,
                publishedCountReconciliation = new
                {
                    sourceUnique = report.FinalAcceptedUniqueRecords,
                    searchRows = publishedRows.Length,
                    summaryResourceCount,
                    optionsResourceCount,
                },
                currentClientPathReconciled = true,
                gameUniverseComplete = "UNKNOWN",
                originalLwbridgeTraversalEquivalent = "UNKNOWN",
            };
            evidence["state"] = "proven";
        }
        catch (Exception error)
        {
            failure = error;
            evidence["state"] = "failed";
            evidence["failure"] = new
            {
                type = error.GetType().FullName,
                message = error.Message,
                code = error is BridgeCommandException bridge ? bridge.Code : null,
            };
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
                    await lifecycle.InvokeAsync("profile_instance_stop", stopPayload, stopCts.Token).ConfigureAwait(false);
                }
                catch (Exception stopError)
                {
                    evidence["cleanupStopError"] = stopError.Message;
                    failure ??= stopError;
                    evidence["state"] = "failed";
                }
            }
            lifecycle?.Dispose();
            evidence["cleanup"] = new
            {
                ownedGamePid,
                ownedGamePidAlive = ownedGamePid is int pid && IsProcessAlive(pid),
                packageSha256 = File.Exists(packagePath) ? Sha256File(packagePath) : null,
                packageRestoredToOfficialV22 = File.Exists(packagePath) &&
                    string.Equals(Sha256File(packagePath), OfficialV22Sha256, StringComparison.OrdinalIgnoreCase),
                versionMarker = ReadText(versionPath),
                tempDatabaseDeleted = TryDeleteDatabase(databasePath),
            };
            evidence["proofFinishedAt"] = DateTimeOffset.UtcNow;
            await File.WriteAllTextAsync(
                outputPath,
                JsonSerializer.Serialize(evidence, new JsonSerializerOptions(JsonOptions.Default) { WriteIndented = true }));
        }

        Console.WriteLine(JsonSerializer.Serialize(new { ok = failure is null, taskId = TaskId, outputPath, state = evidence["state"] }, JsonOptions.Default));
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
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
            if (session is not null && lifecycle.GetLiveServerId() is > 0) return session;
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }
        throw new TimeoutException("Owned Last War lifecycle did not reach Map-ready state.");
    }

    private static async Task<JsonElement> WaitForCompletedScanAsync(
        Map317CommandService service,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMinutes(7);
        JsonElement last = default;
        using JsonDocument empty = JsonDocument.Parse("{}");
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            last = ToJson(await service.InvokeAsync("map_scan_status", empty.RootElement.Clone(), cancellationToken).ConfigureAwait(false));
            if (!last.GetProperty("isReading").GetBoolean()) break;
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }
        if (last.ValueKind == JsonValueKind.Undefined ||
            last.GetProperty("isReading").GetBoolean() ||
            !string.Equals(last.GetProperty("phase").GetString(), "completed", StringComparison.Ordinal) ||
            last.GetProperty("failedBlocks").GetInt32() != 0 ||
            last.GetProperty("unreadBlocks").GetInt32() != 0 ||
            last.GetProperty("readBlocks").GetInt32() != last.GetProperty("totalBlocks").GetInt32())
            throw new InvalidDataException("Resource completeness scan did not reach a clean completed state.");
        return last.Clone();
    }

    private static async Task<JsonElement[]> ReadAllPublishedResourcesAsync(
        Map317CommandService service,
        int serverId,
        CancellationToken cancellationToken)
    {
        var rows = new List<JsonElement>();
        int? expectedTotal = null;
        for (int page = 1; page <= 1000; page++)
        {
            JsonElement payload = JsonSerializer.SerializeToElement(new
            {
                kind = "resource",
                query = new
                {
                    serverId,
                    keyword = string.Empty,
                    page,
                    pageSize = 200,
                    sorts = new[] { new { sortBy = "updatedAt", sortOrder = "desc" } },
                },
            }, JsonOptions.Default);
            JsonElement result = ToJson(await service.InvokeAsync("map_search", payload, cancellationToken).ConfigureAwait(false));
            int total = result.GetProperty("total").GetInt32();
            expectedTotal ??= total;
            if (expectedTotal.Value != total)
                throw new InvalidDataException("Published Resource total changed while collecting full-row evidence.");
            JsonElement[] pageRows = result.GetProperty("rows").EnumerateArray().Select(row => row.Clone()).ToArray();
            rows.AddRange(pageRows);
            if (rows.Count >= total || pageRows.Length == 0) break;
        }
        if (expectedTotal is null || rows.Count != expectedTotal.Value)
            throw new InvalidDataException($"Full Resource row collection returned {rows.Count}/{expectedTotal?.ToString() ?? "unknown"} rows.");
        string[] keys = rows.Select(row => ReadString(row, "recordKey")).ToArray();
        if (keys.Any(string.IsNullOrWhiteSpace) || keys.Distinct(StringComparer.Ordinal).Count() != keys.Length)
            throw new InvalidDataException("Full Resource row collection contains a missing or duplicate record key.");
        return rows.ToArray();
    }

    private static void RequireNoPreexistingGameRuntime()
    {
        string[] names = ["LastWar", "LastWarLauncher", "LWBridge", "LWBridge.Desktop"];
        foreach (string name in names)
        {
            Process[] processes = Process.GetProcessesByName(name);
            try
            {
                if (processes.Length > 0)
                    throw new InvalidOperationException($"Refusing Resource completeness proof because pre-existing {name} process ownership is ambiguous.");
            }
            finally
            {
                foreach (Process process in processes) process.Dispose();
            }
        }
    }

    private static string ClassifyPackage(string hash) =>
        string.Equals(hash, WrappedV22Sha256, StringComparison.OrdinalIgnoreCase)
            ? "validated_overview_wrapped_v22"
            : string.Equals(hash, ResourceCompletenessWrappedV22Sha256, StringComparison.OrdinalIgnoreCase)
                ? "validated_resource_completeness_wrapped_v22"
            : string.Equals(hash, OfficialV22Sha256, StringComparison.OrdinalIgnoreCase)
                ? "validated_official_v22"
                : "unexpected";

    private static string ReadString(JsonElement row, string name) =>
        row.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static int ReadResourceCount(JsonElement root)
    {
        if (!root.TryGetProperty("counts", out JsonElement counts) || counts.ValueKind != JsonValueKind.Object ||
            !counts.TryGetProperty("resource", out JsonElement resource) || !resource.TryGetInt32(out int count) || count < 0)
            throw new InvalidDataException("Map Resource summary/options count is unavailable.");
        return count;
    }

    private static JsonElement ToJson(object? value) => JsonSerializer.SerializeToElement(value, JsonOptions.Default).Clone();

    private static string Sha256File(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string? ReadText(string path) => File.Exists(path) ? File.ReadAllText(path).Trim() : null;

    private static bool IsProcessAlive(int pid)
    {
        try
        {
            using Process process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool TryDeleteDatabase(string path)
    {
        bool ok = true;
        foreach (string candidate in new[] { path, path + "-wal", path + "-shm" })
        {
            try
            {
                if (File.Exists(candidate)) File.Delete(candidate);
            }
            catch
            {
                ok = false;
            }
        }
        return ok && !File.Exists(path) && !File.Exists(path + "-wal") && !File.Exists(path + "-shm");
    }
}
