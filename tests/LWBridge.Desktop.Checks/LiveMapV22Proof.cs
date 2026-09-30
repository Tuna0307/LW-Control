using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

/// <summary>
/// Bounded current-v22 live proof for LWB317-LIVE-MAP-V22-001.
/// This harness uses the same OverviewLifecycleService + Map317CommandService
/// pair as the normal production Desktop window and performs only Map/camera
/// validation actions.
/// </summary>
internal static class LiveMapV22Proof
{
    private const string TaskId = "LWB317-LIVE-MAP-V22-001";
    private const string OfficialV22Sha256 =
        "248f3aeac712b3f14f86bff37a0c365e467897a2248403837c44c1b774f05b22";
    private const string KnownOverviewWrappedV22Sha256 =
        "a705d2d64a44081012c4c7e30bda613636960c98e40fa7a367a60577ce3a7d72";
    private const string ExpectedGameSha256 =
        "905c98c1f89841f90b492556192ba0642f3d209a873cb8c1f7b3c340aca0733d";

    internal static async Task RunAsync(string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        outputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        DateTimeOffset proofStartedAt = DateTimeOffset.UtcNow;
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string appData = Directory.GetParent(localAppData)?.FullName
            ?? throw new InvalidOperationException("AppData root is unavailable.");
        string gameRoot = Path.Combine(localAppData, "FunFly", "Last War-Survival Game");
        string gamePath = Path.Combine(gameRoot, "Game", "LastWar.exe");
        string scriptRoot = Path.Combine(appData, "LocalLow", "FunFly", "Last War-Survival Game", "lwScripts");
        string packagePath = Path.Combine(scriptRoot, "LWScripts.data");
        string versionPath = Path.Combine(scriptRoot, "version.txt");
        string overviewRuntimeRoot = Path.Combine(localAppData, "LWBridgeRebuild", "overview-bridge");
        string proofRoot = Path.Combine(localAppData, "LWBridgeRebuild", "live-resource");
        Directory.CreateDirectory(proofRoot);
        string databasePath = Path.Combine(
            proofRoot,
            "lwb317-live-map-v22-001-" + Guid.NewGuid().ToString("N") + ".db");
        string profileId = "lwb317-live-map-v22-001-" + Guid.NewGuid().ToString("N")[..12];

        var evidence = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schemaVersion"] = 1,
            ["taskId"] = TaskId,
            ["proofStartedAt"] = proofStartedAt,
            ["safety"] = new
            {
                assistantOwnedSessionRequired = true,
                cameraNavigationOnly = true,
                unrelatedGameplay = false,
                serverJump = false,
                treasureClaim = false,
                ghostPreparation = false,
            },
        };

        Exception? failure = null;
        string? instanceId = null;
        int? gamePid = null;
        Map317CommandService? mapService = null;
        OverviewLifecycleService? lifecycle = null;
        try
        {
            Process[] preexistingGame = Process.GetProcessesByName("LastWar");
            Process[] preexistingLauncher = Process.GetProcessesByName("LastWarLauncher");
            try
            {
                evidence["preflight"] = new
                {
                    preexistingLastWarPids = preexistingGame.Select(process => process.Id).ToArray(),
                    preexistingLauncherPids = preexistingLauncher.Select(process => process.Id).ToArray(),
                    pristinePackageSha256 = Sha256File(packagePath),
                    versionMarker = ReadText(versionPath),
                    recoveryJournalPresent = File.Exists(Path.Combine(overviewRuntimeRoot, "recovery.json")),
                };
                if (preexistingGame.Length != 0 || preexistingLauncher.Length != 0)
                    throw new InvalidOperationException(
                        "Live proof requires no pre-existing Last War/launcher session so ownership remains unambiguous.");
            }
            finally
            {
                foreach (Process process in preexistingGame) process.Dispose();
                foreach (Process process in preexistingLauncher) process.Dispose();
            }

            string officialPackageHash = Sha256File(packagePath);
            if (!string.Equals(officialPackageHash, OfficialV22Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    $"Expected pristine validated v22 package {OfficialV22Sha256}, observed {officialPackageHash}.");
            string gameHash = Sha256File(gamePath);
            if (!string.Equals(gameHash, ExpectedGameSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    $"LastWar.exe identity changed before live proof: {gameHash}.");

            lifecycle = new OverviewLifecycleService(profileId, gameRoot);
            using var operationCts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
            using JsonDocument empty = JsonDocument.Parse("{}");
            _ = await lifecycle.InvokeAsync(
                "profile_instance_start", empty.RootElement.Clone(), operationCts.Token).ConfigureAwait(false);

            JsonElement connectedStatus = await WaitForMapReadyAsync(lifecycle, operationCts.Token).ConfigureAwait(false);
            OverviewMapScanSession session = lifecycle.GetReadyMapScanSession()
                ?? throw new InvalidDataException("Connected lifecycle did not expose a ready owned Map session.");
            await lifecycle.WaitForHealthyMapScanSessionAsync(session, operationCts.Token).ConfigureAwait(false);
            instanceId = session.SessionId;
            gamePid = session.GamePid;

            using (Process game = Process.GetProcessById(session.GamePid))
            {
                string? livePath = game.MainModule?.FileName;
                if (string.IsNullOrWhiteSpace(livePath) ||
                    !string.Equals(Path.GetFullPath(livePath), Path.GetFullPath(gamePath), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Owned live PID did not resolve to the expected LastWar.exe path.");
                evidence["session"] = new
                {
                    profileId = session.ProfileId,
                    instanceId = session.SessionId,
                    gamePid = session.GamePid,
                    gamePath = livePath,
                    gameStartedAtUtc = session.GameStartedAtUtc,
                    processStartTimeUtc = game.StartTime.ToUniversalTime(),
                    profileInstanceStatus = connectedStatus,
                    ownedMapSessionReady = true,
                    healthyMapHeartbeat = true,
                };
            }

            string runtimePackageHash = Sha256File(packagePath);
            evidence["clientIdentity"] = new
            {
                validatedContentVersion = InferValidatedContentVersion(runtimePackageHash),
                packagePath,
                officialPackageSha256 = officialPackageHash,
                runtimePackageSha256 = runtimePackageHash,
                runtimePackageState = string.Equals(runtimePackageHash, KnownOverviewWrappedV22Sha256, StringComparison.OrdinalIgnoreCase)
                    ? "verified_overview_wrapped_v22"
                    : string.Equals(runtimePackageHash, OfficialV22Sha256, StringComparison.OrdinalIgnoreCase)
                        ? "official_v22"
                        : "unexpected",
                versionMarker = ReadText(versionPath),
                gameSha256 = gameHash,
            };
            if (InferValidatedContentVersion(runtimePackageHash) != 22)
                throw new InvalidDataException("Live session package did not match either validated v22 identity.");

            mapService = new Map317CommandService(databasePath, lifecycle);
            var source = new CurrentClientMapBlockSource(lifecycle);
            CurrentClientMapContext context =
                await source.GetCurrentContextAsync(operationCts.Token).ConfigureAwait(false);
            int? heartbeatServerId = lifecycle.GetLiveServerId();
            if (heartbeatServerId != context.ServerId)
                throw new InvalidDataException(
                    $"Heartbeat server {heartbeatServerId?.ToString() ?? "null"} != world context server {context.ServerId}.");
            evidence["world"] = new
            {
                serverId = context.ServerId,
                heartbeatServerId,
                worldId = context.WorldId,
                tileWidth = context.TileWidth,
                tileHeight = context.TileHeight,
                playerTileX = context.PlayerTileX,
                playerTileY = context.PlayerTileY,
                launchSessionId = context.LaunchSessionId,
                ready = true,
            };

            (int targetX, int targetY) = SelectSafeTarget(context);
            JsonElement jumpPayload = JsonSerializer.SerializeToElement(new
            {
                serverId = context.ServerId,
                x = targetX,
                y = targetY,
            }, JsonOptions.Default);
            JsonElement jumpResult = ToJson(await mapService.InvokeAsync(
                "map_coordinate_jump", jumpPayload, operationCts.Token).ConfigureAwait(false));
            JsonElement jumpRuntime = ReadNavigationEvidence(overviewRuntimeRoot);
            if (jumpResult.GetProperty("serverId").GetInt32() != context.ServerId ||
                jumpResult.GetProperty("x").GetInt32() != targetX ||
                jumpResult.GetProperty("y").GetInt32() != targetY)
                throw new InvalidDataException("Map317 coordinate jump result did not match the requested target.");

            object? returnEvidence = null;
            if (context.PlayerTileX is int playerX && context.PlayerTileY is int playerY &&
                playerX > 0 && playerY > 0 && (playerX != targetX || playerY != targetY))
            {
                JsonElement returnPayload = JsonSerializer.SerializeToElement(new
                {
                    serverId = context.ServerId,
                    x = playerX,
                    y = playerY,
                }, JsonOptions.Default);
                JsonElement returnResult = ToJson(await mapService.InvokeAsync(
                    "map_coordinate_jump", returnPayload, operationCts.Token).ConfigureAwait(false));
                JsonElement returnRuntime = ReadNavigationEvidence(overviewRuntimeRoot);
                returnEvidence = new
                {
                    requested = new { x = playerX, y = playerY },
                    result = returnResult,
                    runtime = returnRuntime,
                };
            }
            evidence["coordinateNavigation"] = new
            {
                requested = new { serverId = context.ServerId, x = targetX, y = targetY },
                result = jumpResult,
                runtime = jumpRuntime,
                returnedToPlayerTile = returnEvidence,
            };

            JsonElement scanPayload = JsonSerializer.SerializeToElement(new
            {
                profileId,
                selectedTypes = new[] { "resource" },
                scanMode = "fast",
                resume = false,
            }, JsonOptions.Default);
            Stopwatch scanClock = Stopwatch.StartNew();
            JsonElement scanStart = ToJson(await mapService.InvokeAsync(
                "map_scan_start", scanPayload, operationCts.Token).ConfigureAwait(false));
            var progress = new List<JsonElement> { scanStart.Clone() };
            int nextProgressBlock = 250;
            string lastPhase = scanStart.GetProperty("phase").GetString() ?? string.Empty;
            JsonElement finalStatus = scanStart;
            DateTimeOffset scanDeadline = DateTimeOffset.UtcNow.AddMinutes(6);
            while (DateTimeOffset.UtcNow < scanDeadline)
            {
                operationCts.Token.ThrowIfCancellationRequested();
                JsonElement status = ToJson(await mapService.InvokeAsync(
                    "map_scan_status", empty.RootElement.Clone(), operationCts.Token).ConfigureAwait(false));
                int readBlocks = status.GetProperty("readBlocks").GetInt32();
                string phase = status.GetProperty("phase").GetString() ?? string.Empty;
                if (phase != lastPhase || readBlocks >= nextProgressBlock)
                {
                    progress.Add(status.Clone());
                    lastPhase = phase;
                    while (readBlocks >= nextProgressBlock) nextProgressBlock += 250;
                }
                finalStatus = status;
                if (!status.GetProperty("isReading").GetBoolean()) break;
                await Task.Delay(100, operationCts.Token).ConfigureAwait(false);
            }
            scanClock.Stop();
            if (progress.Count == 0 || progress[^1].GetProperty("readBlocks").GetInt32() != finalStatus.GetProperty("readBlocks").GetInt32())
                progress.Add(finalStatus.Clone());
            RequireCompletedScan(finalStatus);

            int serverId = finalStatus.GetProperty("serverId").GetInt32();
            JsonElement summary = ToJson(await mapService.InvokeAsync(
                "map_summary", empty.RootElement.Clone(), operationCts.Token).ConfigureAwait(false));
            JsonElement optionsPayload = JsonSerializer.SerializeToElement(new { serverId }, JsonOptions.Default);
            JsonElement options = ToJson(await mapService.InvokeAsync(
                "map_data_options", optionsPayload, operationCts.Token).ConfigureAwait(false));

            JsonElement page1 = await SearchAsync(
                mapService, serverId, page: 1, pageSize: 3,
                sorts: new[] { new { sortBy = "updatedAt", sortOrder = "desc" } },
                resourceNameKey: null, operationCts.Token).ConfigureAwait(false);
            int totalRecords = page1.GetProperty("total").GetInt32();
            if (totalRecords <= 0)
                throw new InvalidDataException("Fresh v22 Resource scan returned no records.");
            JsonElement[] representativeRows = page1.GetProperty("rows").EnumerateArray()
                .Take(3).Select(row => row.Clone()).ToArray();
            if (representativeRows.Length == 0)
                throw new InvalidDataException("Fresh v22 Resource query returned an empty first page.");

            JsonElement page2 = await SearchAsync(
                mapService, serverId, page: 2, pageSize: 3,
                sorts: new[] { new { sortBy = "updatedAt", sortOrder = "desc" } },
                resourceNameKey: null, operationCts.Token).ConfigureAwait(false);
            string[] page1Keys = RowKeys(page1);
            string[] page2Keys = RowKeys(page2);
            if (totalRecords > 3 && page2Keys.Length == 0)
                throw new InvalidDataException("Pagination proof expected a nonempty second page.");
            if (page1Keys.Intersect(page2Keys, StringComparer.Ordinal).Any())
                throw new InvalidDataException("Pagination proof returned overlapping first/second-page record keys.");

            JsonElement sorted = await SearchAsync(
                mapService, serverId, page: 1, pageSize: Math.Min(10, Math.Max(1, totalRecords)),
                sorts: new[]
                {
                    new { sortBy = "level", sortOrder = "asc" },
                    new { sortBy = "updatedAt", sortOrder = "desc" },
                },
                resourceNameKey: null, operationCts.Token).ConfigureAwait(false);
            RequireNondecreasingLevels(sorted);

            string? resourceNameKey = TryString(representativeRows[0], "resourceNameKey");
            JsonElement filtered = resourceNameKey is null
                ? default
                : await SearchAsync(
                    mapService, serverId, page: 1, pageSize: 10,
                    sorts: new[] { new { sortBy = "updatedAt", sortOrder = "desc" } },
                    resourceNameKey, operationCts.Token).ConfigureAwait(false);
            if (resourceNameKey is not null)
            {
                if (filtered.GetProperty("total").GetInt32() <= 0)
                    throw new InvalidDataException("Resource-name filter unexpectedly returned no rows.");
                foreach (JsonElement row in filtered.GetProperty("rows").EnumerateArray())
                    if (!string.Equals(TryString(row, "resourceNameKey"), resourceNameKey, StringComparison.Ordinal))
                        throw new InvalidDataException("Resource-name filter returned a row with a different resourceNameKey.");
            }

            evidence["scan"] = new
            {
                request = new { selectedTypes = new[] { "resource" }, scanMode = "fast", resume = false },
                start = scanStart,
                progress,
                final = finalStatus,
                wallSeconds = scanClock.Elapsed.TotalSeconds,
            };
            evidence["records"] = new
            {
                total = totalRecords,
                representative = representativeRows,
            };
            object filterEvidence = new Dictionary<string, object?>
            {
                ["available"] = resourceNameKey is not null,
                ["resourceNameKey"] = resourceNameKey,
                ["total"] = resourceNameKey is null ? 0 : filtered.GetProperty("total").GetInt32(),
                ["rows"] = resourceNameKey is null
                    ? Array.Empty<JsonElement>()
                    : filtered.GetProperty("rows").EnumerateArray().Select(row => row.Clone()).ToArray(),
            };
            evidence["queries"] = new
            {
                summary,
                options,
                page1,
                page2,
                pagination = new { pageSize = 3, page1Keys, page2Keys },
                sort = new
                {
                    sorts = new[] { "level asc", "updatedAt desc" },
                    result = sorted,
                    verified = true,
                },
                filter = filterEvidence,
            };

            JsonElement clearPayload = JsonSerializer.SerializeToElement(new { serverId }, JsonOptions.Default);
            JsonElement clearResult = ToJson(await mapService.InvokeAsync(
                "map_scan_clear", clearPayload, operationCts.Token).ConfigureAwait(false));
            JsonElement postClearSearch = await SearchAsync(
                mapService, serverId, page: 1, pageSize: 3,
                sorts: new[] { new { sortBy = "updatedAt", sortOrder = "desc" } },
                resourceNameKey: null, operationCts.Token).ConfigureAwait(false);
            JsonElement postClearSummary = ToJson(await mapService.InvokeAsync(
                "map_summary", empty.RootElement.Clone(), operationCts.Token).ConfigureAwait(false));
            if (postClearSearch.GetProperty("total").GetInt32() != 0 ||
                clearResult.GetProperty("isReading").GetBoolean() ||
                !string.Equals(clearResult.GetProperty("phase").GetString(), "idle", StringComparison.Ordinal))
                throw new InvalidDataException("map_scan_clear did not produce an empty idle result.");
            evidence["clear"] = new
            {
                result = clearResult,
                postClearSearch,
                postClearSummary,
                proven = true,
            };

            object stopEvidence;
            if (lifecycle.GetReadyMapScanSession() is null)
            {
                stopEvidence = new
                {
                    attempted = false,
                    liveReadingObserved = false,
                    proven = false,
                    reason = "owned Map session was unavailable after the required completed scan and clear proof",
                };
            }
            else
            {
                try
                {
                    JsonElement stopStart = ToJson(await mapService.InvokeAsync(
                        "map_scan_start", scanPayload, operationCts.Token).ConfigureAwait(false));
                    if (stopStart.GetProperty("isReading").GetBoolean())
                    {
                        await Task.Delay(100, operationCts.Token).ConfigureAwait(false);
                        JsonElement preStop = ToJson(await mapService.InvokeAsync(
                            "map_scan_status", empty.RootElement.Clone(), operationCts.Token).ConfigureAwait(false));
                        if (preStop.GetProperty("isReading").GetBoolean())
                        {
                            JsonElement stopResult = ToJson(await mapService.InvokeAsync(
                                "map_scan_stop", empty.RootElement.Clone(), operationCts.Token).ConfigureAwait(false));
                            if (stopResult.GetProperty("isReading").GetBoolean() ||
                                !string.Equals(stopResult.GetProperty("phase").GetString(), "idle", StringComparison.Ordinal))
                                throw new InvalidDataException("map_scan_stop returned a false-success non-idle state.");
                            JsonElement stopClear = ToJson(await mapService.InvokeAsync(
                                "map_scan_clear", clearPayload, operationCts.Token).ConfigureAwait(false));
                            JsonElement stopPostClear = await SearchAsync(
                                mapService, serverId, page: 1, pageSize: 3,
                                sorts: new[] { new { sortBy = "updatedAt", sortOrder = "desc" } },
                                resourceNameKey: null, operationCts.Token).ConfigureAwait(false);
                            if (stopPostClear.GetProperty("total").GetInt32() != 0)
                                throw new InvalidDataException("Stopped-scan cleanup left Resource rows behind.");
                            stopEvidence = new
                            {
                                attempted = true,
                                liveReadingObserved = true,
                                start = stopStart,
                                preStop,
                                result = stopResult,
                                postStopClear = stopClear,
                                postStopSearch = stopPostClear,
                                proven = true,
                            };
                        }
                        else
                        {
                            stopEvidence = new
                            {
                                attempted = true,
                                liveReadingObserved = false,
                                start = stopStart,
                                preStop,
                                proven = false,
                                reason = "second Resource scan completed before a controlled stop could be issued",
                            };
                        }
                    }
                    else
                    {
                        stopEvidence = new
                        {
                            attempted = true,
                            liveReadingObserved = false,
                            start = stopStart,
                            proven = false,
                            reason = "second Resource scan was already terminal at start return",
                        };
                    }
                }
                catch (BridgeCommandException error) when (
                    string.Equals(error.Code, "GAME_CONNECTION_UNAVAILABLE", StringComparison.Ordinal))
                {
                    stopEvidence = new
                    {
                        attempted = true,
                        liveReadingObserved = false,
                        proven = false,
                        reason = "game connection became unavailable after the required completed scan and clear proof",
                        errorCode = error.Code,
                        error = error.Message,
                    };
                }
            }
            evidence["stop"] = stopEvidence;
            evidence["state"] = "proven";
        }
        catch (Exception error)
        {
            failure = error;
            evidence["state"] = "failed";
            evidence["failure"] = new
            {
                type = error.GetType().FullName,
                error.Message,
            };
        }
        finally
        {
            try { mapService?.Dispose(); }
            catch (Exception error) { evidence["mapServiceDisposeError"] = error.Message; }

            if (lifecycle is not null && !string.IsNullOrWhiteSpace(instanceId))
            {
                try
                {
                    using var stopCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                    JsonElement stopPayload = JsonSerializer.SerializeToElement(new { instanceId }, JsonOptions.Default);
                    await lifecycle.InvokeAsync(
                        "profile_instance_stop", stopPayload, stopCts.Token).ConfigureAwait(false);
                }
                catch (Exception error)
                {
                    evidence["sessionStopError"] = error.Message;
                    failure ??= error;
                    evidence["state"] = "failed";
                }
            }
            lifecycle?.Dispose();

            bool gamePidAlive = gamePid is int pid && IsProcessAlive(pid);
            string finalPackageHash = File.Exists(packagePath) ? Sha256File(packagePath) : "missing";
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            evidence["cleanup"] = new
            {
                requestedInstanceId = instanceId,
                gamePid,
                gamePidAlive,
                anyLastWarProcess = Process.GetProcessesByName("LastWar").Any(),
                recoveryJournalPresent = File.Exists(Path.Combine(overviewRuntimeRoot, "recovery.json")),
                finalPackageSha256 = finalPackageHash,
                finalPackageRestoredToOfficialV22 = string.Equals(
                    finalPackageHash, OfficialV22Sha256, StringComparison.OrdinalIgnoreCase),
                tempDatabaseDeleted = TryDeleteDatabase(databasePath),
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

    private static async Task<JsonElement> WaitForMapReadyAsync(
        OverviewLifecycleService lifecycle,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMinutes(4);
        JsonElement last = default;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            object? value = lifecycle.CreateProfileInstanceStatus();
            if (value is not null)
            {
                last = JsonSerializer.SerializeToElement(value, JsonOptions.Default);
                string phase = TryString(last, "phase") ?? string.Empty;
                string connection = TryString(last, "connectionState") ?? string.Empty;
                string? error = TryString(last, "lastError");
                if (!string.IsNullOrWhiteSpace(error))
                    throw new InvalidOperationException($"Owned Last War lifecycle failed: {phase}/{connection}: {error}");
                if (phase == "running" && lifecycle.GetReadyMapScanSession() is not null &&
                    lifecycle.GetLiveServerId() is > 0)
                    return last.Clone();
            }
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }
        throw new TimeoutException(
            "Owned Last War lifecycle did not reach Map-ready state. Last status: " +
            (last.ValueKind == JsonValueKind.Undefined ? "null" : last.GetRawText()));
    }

    private static async Task<JsonElement> SearchAsync(
        Map317CommandService service,
        int serverId,
        int page,
        int pageSize,
        object[] sorts,
        string? resourceNameKey,
        CancellationToken cancellationToken)
    {
        var query = new Dictionary<string, object?>
        {
            ["serverId"] = serverId,
            ["keyword"] = string.Empty,
            ["page"] = page,
            ["pageSize"] = pageSize,
            ["sorts"] = sorts,
        };
        if (!string.IsNullOrWhiteSpace(resourceNameKey)) query["resourceNameKey"] = resourceNameKey;
        JsonElement payload = JsonSerializer.SerializeToElement(new
        {
            kind = "resource",
            query,
        }, JsonOptions.Default);
        return ToJson(await service.InvokeAsync("map_search", payload, cancellationToken).ConfigureAwait(false));
    }

    private static void RequireCompletedScan(JsonElement status)
    {
        string phase = status.GetProperty("phase").GetString() ?? string.Empty;
        bool reading = status.GetProperty("isReading").GetBoolean();
        int total = status.GetProperty("totalBlocks").GetInt32();
        int read = status.GetProperty("readBlocks").GetInt32();
        int failed = status.GetProperty("failedBlocks").GetInt32();
        int unread = status.GetProperty("unreadBlocks").GetInt32();
        string? error = TryString(status, "lastError");
        if (reading || phase != "completed" || total <= 0 || read != total || failed != 0 || unread != 0 ||
            !string.IsNullOrWhiteSpace(error))
            throw new InvalidDataException(
                $"Fresh Resource scan did not complete cleanly: phase={phase}, reading={reading}, read={read}/{total}, failed={failed}, unread={unread}, error={error ?? "null"}.");
    }

    private static (int X, int Y) SelectSafeTarget(CurrentClientMapContext context)
    {
        if (context.TileWidth <= 2 || context.TileHeight <= 2)
            throw new InvalidDataException("Live world dimensions are too small for coordinate navigation proof.");
        int originX = context.PlayerTileX is > 0 ? context.PlayerTileX.Value : context.TileWidth / 2;
        int originY = context.PlayerTileY is > 0 ? context.PlayerTileY.Value : context.TileHeight / 2;
        int maxX = context.TileWidth - 1;
        int maxY = context.TileHeight - 1;
        int x = originX + 37 <= maxX ? originX + 37 : Math.Max(1, originX - 37);
        int y = originY + 53 <= maxY ? originY + 53 : Math.Max(1, originY - 53);
        x = Math.Clamp(x, 1, maxX);
        y = Math.Clamp(y, 1, maxY);
        return (x, y);
    }

    private static JsonElement ReadNavigationEvidence(string overviewRuntimeRoot)
    {
        string path = Path.Combine(overviewRuntimeRoot, "map-navigation-result.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
        JsonElement root = document.RootElement;
        return JsonSerializer.SerializeToElement(new
        {
            requestId = TryString(root, "requestId"),
            state = TryString(root, "state"),
            serverId = TryInt(root, "serverId"),
            worldId = TryLong(root, "worldId"),
            liveCurServerId = TryInt(root, "liveCurServerId"),
            liveWorldId = TryInt(root, "liveWorldId"),
            targetX = TryInt(root, "targetX"),
            targetY = TryInt(root, "targetY"),
            verifiedTargetX = TryInt(root, "verifiedTargetX"),
            verifiedTargetY = TryInt(root, "verifiedTargetY"),
            preTargetTileX = TryInt(root, "preTargetTileX"),
            preTargetTileY = TryInt(root, "preTargetTileY"),
            postTargetTileX = TryInt(root, "postTargetTileX"),
            postTargetTileY = TryInt(root, "postTargetTileY"),
            currentLod = TryInt(root, "currentLod"),
            serverLod = TryInt(root, "serverLod"),
            method = TryString(root, "method"),
            error = TryString(root, "error"),
        }, JsonOptions.Default);
    }

    private static void RequireNondecreasingLevels(JsonElement result)
    {
        int? previous = null;
        foreach (JsonElement row in result.GetProperty("rows").EnumerateArray())
        {
            if (!row.TryGetProperty("level", out JsonElement value) || !value.TryGetInt32(out int current)) continue;
            if (previous is int prior && current < prior)
                throw new InvalidDataException("Resource level ascending sort returned a decreasing pair.");
            previous = current;
        }
    }

    private static string[] RowKeys(JsonElement result) =>
        result.GetProperty("rows").EnumerateArray()
            .Select(row => TryString(row, "recordKey") ?? string.Empty)
            .Where(value => value.Length != 0)
            .ToArray();

    private static JsonElement ToJson(object? value) =>
        JsonSerializer.SerializeToElement(value, JsonOptions.Default).Clone();

    private static string Sha256File(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static int? InferValidatedContentVersion(string packageHash) =>
        string.Equals(packageHash, OfficialV22Sha256, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(packageHash, KnownOverviewWrappedV22Sha256, StringComparison.OrdinalIgnoreCase)
            ? 22
            : null;

    private static string? ReadText(string path) =>
        File.Exists(path) ? File.ReadAllText(path).Trim() : null;

    private static string? TryString(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out JsonElement value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? TryInt(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out JsonElement value) && value.TryGetInt32(out int parsed)
            ? parsed
            : null;

    private static long? TryLong(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out JsonElement value) && value.TryGetInt64(out long parsed)
            ? parsed
            : null;

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
