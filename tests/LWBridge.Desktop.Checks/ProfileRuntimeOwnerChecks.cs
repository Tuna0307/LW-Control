using System.Text.Json;
using LWBridge.Desktop;
using Microsoft.Data.Sqlite;
using Map317 = LWBridge.Map317;

namespace LWBridge.Desktop.Checks;

internal static class ProfileRuntimeOwnerChecks
{
    internal static async Task RunAsync()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "lwb317-profile-runtime-owner-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            VerifyInactiveLifecycleCleanupIsolation(root);
            await VerifyProfileReplacementRetirementAsync(root).ConfigureAwait(false);

            string controllerPath = Path.Combine(root, "controller.db");
            using var registryStore = new ProfileRegistryStore(controllerPath);
            registryStore.EnsureLocalProfile("profile-A", "Profile A", 1_800_000_000_000);
            InsertSecondaryProfile(
                controllerPath,
                "profile-B",
                "Profile B",
                1_800_000_000_100);
            using var registry = new ProfileRegistryCommandService(registryStore, maxProfiles: 3);

            string profileARoot = Path.Combine(root, "profiles", "profile-A");
            string profileBRoot = Path.Combine(root, "profiles", "profile-B");
            SeedEightKinds(profileARoot, 317, "A", "Alpha Runtime");
            SeedCity(profileBRoot, 318, "city-B", "Beta Runtime");

            var installationHooks = new GameInstallationTestHooks
            {
                DefaultRoot = Path.Combine(root, "missing-default"),
                GetEnvironmentVariable = _ => null,
                NearbyRoot = Path.Combine(root, "missing-nearby"),
                LocalAppData = Path.Combine(root, "missing-local"),
                DiscoveredCandidates = Array.Empty<GameRootCandidate>(),
            };

            var configA = new LocalConfigStore(
                Path.Combine(profileARoot, "local-config"),
                initialValue: LWBridgeLocalConfig.CreateDefault() with
                {
                    ProfileId = "profile-A",
                    AutoLaunchGame = true,
                });
            using (ProfileRuntimeOwner ownerA = CreateOwner(
                "profile-A", configA, profileARoot, registry, 317, installationHooks))
            {
                Require(ownerA.Backend.ProfileId == "profile-A",
                    "A runtime backend owns profile A");
                _ = await ownerA.Backend.InvokeAsync(
                    "local_config_set",
                    JsonSerializer.SerializeToElement(new { autoLaunchGame = false }, JsonOptions.Default),
                    CancellationToken.None).ConfigureAwait(false);
                MapAutoScanSnapshot autoA = await ownerA.MapAutoScan.UpdateConfigAsync(
                    MapAutoScanConfig.Default with
                    {
                        IntervalMinutes = 45,
                        ServerIds = new[] { 317 },
                    }).ConfigureAwait(false);
                Require(autoA.Config.IntervalMinutes == 45,
                    "A runtime owns its native Auto configuration");
                Require(await SearchSingleCityAsync(ownerA.Backend, "profile-A", 317).ConfigureAwait(false) == "Alpha Runtime",
                    "A runtime reads only A Map store");
                await VerifyNativeMapServiceBoundaryAsync(ownerA, root).ConfigureAwait(false);
                await ExpectProfileScopeMismatchAsync(ownerA.Backend, "profile-B").ConfigureAwait(false);
            }
            SeedCity(profileARoot, 317, "city-A", "Alpha Runtime");

            var configB = new LocalConfigStore(
                Path.Combine(profileBRoot, "local-config"),
                initialValue: LWBridgeLocalConfig.CreateDefault() with
                {
                    ProfileId = "profile-B",
                    AutoLaunchGame = true,
                });
            using (ProfileRuntimeOwner ownerB = CreateOwner(
                "profile-B", configB, profileBRoot, registry, 318, installationHooks))
            {
                Require(ownerB.Backend.ProfileId == "profile-B",
                    "B replacement backend owns profile B");
                JsonElement config = JsonSerializer.SerializeToElement(
                    await ownerB.Backend.InvokeAsync(
                        "local_config_get",
                        JsonSerializer.SerializeToElement(new { }, JsonOptions.Default),
                        CancellationToken.None).ConfigureAwait(false),
                    JsonOptions.Default);
                Require(config.GetProperty("autoLaunchGame").GetBoolean(),
                    "B config is isolated from A native config edits");
                MapAutoScanSnapshot autoB = await ownerB.MapAutoScan.GetSnapshotAsync().ConfigureAwait(false);
                Require(autoB.Config.IntervalMinutes == 60 && autoB.Config.ServerIds!.Count == 0,
                    "B Auto store is isolated from A Auto state");
                Require(await SearchSingleCityAsync(ownerB.Backend, "profile-B", 318).ConfigureAwait(false) == "Beta Runtime",
                    "B replacement reads only B Map store");
                await ExpectProfileScopeMismatchAsync(ownerB.Backend, "profile-A").ConfigureAwait(false);
            }

            using (ProfileRuntimeOwner returnedA = CreateOwner(
                "profile-A",
                new LocalConfigStore(Path.Combine(profileARoot, "local-config")),
                profileARoot,
                registry,
                317,
                installationHooks))
            {
                JsonElement config = JsonSerializer.SerializeToElement(
                    await returnedA.Backend.InvokeAsync(
                        "local_config_get",
                        JsonSerializer.SerializeToElement(new { }, JsonOptions.Default),
                        CancellationToken.None).ConfigureAwait(false),
                    JsonOptions.Default);
                Require(!config.GetProperty("autoLaunchGame").GetBoolean(),
                    "A/B/A replacement reopens A persisted config rather than B state");
                MapAutoScanSnapshot autoA = await returnedA.MapAutoScan.GetSnapshotAsync().ConfigureAwait(false);
                Require(autoA.Config.IntervalMinutes == 45 && autoA.Config.ServerIds!.SequenceEqual(new[] { 317 }),
                    "A/B/A replacement reopens A native Auto state");
                Require(await SearchSingleCityAsync(returnedA.Backend, "profile-A", 317).ConfigureAwait(false) == "Alpha Runtime",
                    "A/B/A replacement reopens A Map store");
                object? status = await returnedA.Backend.InvokeAsync(
                    "get_status",
                    JsonSerializer.SerializeToElement(new { profileId = "profile-A" }, JsonOptions.Default),
                    CancellationToken.None).ConfigureAwait(false);
                Require(status is not null,
                    "returned A Home status owner is available through the replacement backend");
            }
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); }
            catch { }
        }
    }

    private static ProfileRuntimeOwner CreateOwner(
        string profileId,
        LocalConfigStore config,
        string profileRoot,
        ProfileRegistryCommandService registry,
        int serverId,
        GameInstallationTestHooks installationHooks,
        bool startPlunderWorkers = true)
    {
        var provider = new Map317.MapProviderAdapter(
            _ => ValueTask.FromResult(new Map317.MapProviderContext(
                true, true, serverId, "live", 1, 100, 100, 1)),
            _ => ValueTask.FromResult(new Map317.MapProviderContext(
                true, true, serverId, "live", 1, 100, 100, 1)),
            (_, _) => ValueTask.FromResult(new Map317.MapProviderStartResult(
                false, 1, false, 0, 0, "isolated profile runtime does not execute scans")),
            _ => ValueTask.CompletedTask);
        return ProfileRuntimeOwner.Create(
            profileId,
            config,
            profileRoot,
            registry,
            mapProvider: provider,
            mapActionProvider: Map317.UnavailableMapActionProvider.Instance,
            startPlunderWorkers: startPlunderWorkers,
            startAutoScheduler: false,
            startRecoveryMonitor: false,
            startBridgeTransport: false,
            installationTestHooks: installationHooks,
            overviewRuntimeRoot: Path.Combine(profileRoot, "isolated-overview-runtime"),
            overviewEvidenceRoot: Path.Combine(profileRoot, "isolated-overview-evidence"));
    }

    private static void VerifyInactiveLifecycleCleanupIsolation(string root)
    {
        string runtimeRoot = Path.Combine(root, "foreign-overview-runtime");
        string evidenceRoot = Path.Combine(root, "foreign-overview-evidence");
        Directory.CreateDirectory(runtimeRoot);
        Directory.CreateDirectory(Path.Combine(evidenceRoot, "foreign-session"));

        const string foreignSession = "foreign-session";
        const string foreignChallenge = "foreign-challenge";
        var sentinels = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [Path.Combine(runtimeRoot, "lease.txt")] =
                $"schema=1\nbridgeVersion={OverviewLifecycleService.BridgeVersion}\nsessionId={foreignSession}\nchallenge={foreignChallenge}\nupdatedAt=1\n",
            [Path.Combine(runtimeRoot, "lease.txt.tmp-foreign")] =
                $"schema=1\nsessionId={foreignSession}\nchallenge={foreignChallenge}\n",
            [Path.Combine(runtimeRoot, "cancel-start.txt")] =
                $"schema=1\nsessionId={foreignSession}\nchallenge={foreignChallenge}\n",
            [Path.Combine(runtimeRoot, "cancel-start.txt.tmp-foreign")] =
                $"schema=1\nsessionId={foreignSession}\nchallenge={foreignChallenge}\n",
            [Path.Combine(runtimeRoot, "control.txt")] =
                $"schema=1\nsessionId={foreignSession}\nchallenge={foreignChallenge}\n",
            [Path.Combine(runtimeRoot, "ready.json")] =
                JsonSerializer.Serialize(new { sessionId = foreignSession, challenge = foreignChallenge, ready = true }),
            [Path.Combine(runtimeRoot, "heartbeat.json")] =
                JsonSerializer.Serialize(new { sessionId = foreignSession, challenge = foreignChallenge, ready = true }),
            [Path.Combine(runtimeRoot, "unrelated.txt")] = "leave-me-alone",
            [Path.Combine(evidenceRoot, foreignSession, "host-start.json")] = "foreign-evidence",
        };
        foreach ((string path, string contents) in sentinels)
            File.WriteAllText(path, contents);

        using (var lifecycle = new OverviewLifecycleService(
            "inactive-owner",
            gameRoot: null,
            startRecoveryMonitor: false,
            runtimeRoot: runtimeRoot,
            evidenceRoot: evidenceRoot))
        {
            lifecycle.Close();
        }

        foreach ((string path, string contents) in sentinels)
        {
            Require(File.Exists(path), $"inactive lifecycle Close must preserve foreign sentinel {Path.GetFileName(path)}");
            Require(File.ReadAllText(path) == contents,
                $"inactive lifecycle Close must not rewrite foreign sentinel {Path.GetFileName(path)}");
        }
    }

    private static async Task VerifyProfileReplacementRetirementAsync(string root)
    {
        string lifecycleRoot = Path.Combine(root, "profile-retirement");
        string gameDirectory = Path.Combine(lifecycleRoot, "Game");
        Directory.CreateDirectory(gameDirectory);
        const string profileId = "retirement-A";
        const int gamePid = 59101;
        const string startedAtUtc = "2026-10-06T04:30:00.0000000Z";
        string gamePath = Path.Combine(gameDirectory, "LastWar.exe");
        string? session = null;
        string? challenge = null;
        bool processAlive = false;
        var helperEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var helperRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hooks = new OverviewLifecycleTestHooks
        {
            RunOfficialRecoverAsync = (_, _) => Task.CompletedTask,
            RunOfficialSettleAsync = (_, _) => Task.CompletedTask,
            RunHelperAsync = async (invocation, cancellationToken) =>
            {
                if (invocation.Operation == "start")
                {
                    session = invocation.SessionId;
                    challenge = invocation.Challenge;
                    helperEntered.TrySetResult();
                    await helperRelease.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                    processAlive = true;
                    return JsonSerializer.SerializeToElement(new
                    {
                        ok = true,
                        mode = "overview_install_launch_ready_deferred_restore",
                        bridgeVersion = OverviewLifecycleService.BridgeVersion,
                        profileId,
                        sessionId = invocation.SessionId,
                        challengeSha256 = Convert.ToHexString(
                            System.Security.Cryptography.SHA256.HashData(
                                System.Text.Encoding.UTF8.GetBytes(invocation.Challenge!))).ToLowerInvariant(),
                        gamePid,
                        launcherPid = gamePid + 10_000,
                        gamePath,
                        gameStartedAtUtc = startedAtUtc,
                        gameRunning = true,
                        installedFilesChanged = true,
                        restore = new
                        {
                            restored = false,
                            deferred = true,
                            stage = "active_ready_deferred_restore",
                        },
                        ready = new
                        {
                            schemaVersion = 1,
                            bridgeVersion = OverviewLifecycleService.BridgeVersion,
                            profileId,
                            sessionId = invocation.SessionId,
                            challenge = invocation.Challenge,
                            gamePid,
                            ready = true,
                            messageVisible = true,
                            messageText = OverviewLifecycleService.ReadyMessage,
                            readyAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                            updatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        },
                    }, JsonOptions.Default);
                }

                bool wasAlive = processAlive;
                processAlive = false;
                return JsonSerializer.SerializeToElement(new
                {
                    ok = true,
                    mode = "overview_exact_pid_close_restore",
                    bridgeVersion = OverviewLifecycleService.BridgeVersion,
                    profileId,
                    sessionId = invocation.SessionId,
                    gamePid,
                    gamePath,
                    gameStartedAtUtc = startedAtUtc,
                    close = wasAlive
                        ? new
                        {
                            method = "synthetic",
                            accepted = true,
                            processExited = true,
                            alreadyExited = false,
                        }
                        : new
                        {
                            method = "already_exited",
                            accepted = false,
                            processExited = true,
                            alreadyExited = true,
                        },
                    restore = new { restored = true },
                    gameRunning = false,
                    installedFilesChanged = false,
                }, JsonOptions.Default);
            },
            ProcessMatches = (pid, path, startedAt) =>
                processAlive && pid == gamePid &&
                string.Equals(Path.GetFullPath(path), Path.GetFullPath(gamePath), StringComparison.OrdinalIgnoreCase) &&
                string.Equals(startedAt, startedAtUtc, StringComparison.Ordinal),
            ReadAllBytes = path => path.EndsWith("recovery.json", StringComparison.OrdinalIgnoreCase)
                ? throw new FileNotFoundException(path)
                : JsonSerializer.SerializeToUtf8Bytes(new
                {
                    schemaVersion = 1,
                    bridgeVersion = OverviewLifecycleService.BridgeVersion,
                    profileId,
                    sessionId = session,
                    challenge,
                    gamePid,
                    ready = true,
                    messageVisible = true,
                    messageText = OverviewLifecycleService.ReadyMessage,
                    updatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                }, JsonOptions.Default),
            WriteLease = (_, _, _) => { },
            DeleteFile = _ => { },
        };

        using var lifecycle = new OverviewLifecycleService(
            profileId,
            lifecycleRoot,
            helperPath: Path.Combine(lifecycleRoot, "fake-overview-helper.py"),
            requireCurrentClientEvidence: false,
            testHooks: hooks,
            startRecoveryMonitor: false,
            runtimeRoot: Path.Combine(lifecycleRoot, "overview-runtime"),
            evidenceRoot: Path.Combine(lifecycleRoot, "overview-evidence"));
        JsonElement payload = JsonSerializer.SerializeToElement(new { profileId }, JsonOptions.Default);
        Task<object?> starting = lifecycle.InvokeAsync("profile_instance_start", payload, CancellationToken.None);
        await helperEntered.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        Require(CaptureReplacementAdmissionError(lifecycle) == "GAME_OPERATION_IN_PROGRESS",
            "profile replacement must fail closed while Start owns the lifecycle admission");

        helperRelease.TrySetResult();
        JsonElement running = JsonSerializer.SerializeToElement(
            await starting.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false),
            JsonOptions.Default);
        Require(CaptureReplacementAdmissionError(lifecycle) == "GAME_OPERATION_IN_PROGRESS",
            "profile replacement must fail closed while the owned game is running");

        string instanceId = running.GetProperty("instanceId").GetString()!;
        _ = await lifecycle.InvokeAsync(
            "profile_instance_stop",
            JsonSerializer.SerializeToElement(new { profileId, instanceId }, JsonOptions.Default),
            CancellationToken.None).ConfigureAwait(false);
        lifecycle.BeginProfileReplacement();
        string pendingStart = string.Empty;
        try
        {
            _ = await lifecycle.InvokeAsync("profile_instance_start", payload, CancellationToken.None).ConfigureAwait(false);
        }
        catch (BridgeCommandException error)
        {
            pendingStart = error.Code;
        }
        Require(pendingStart == "GAME_OPERATION_IN_PROGRESS",
            "profile replacement admission must quiesce late Start before replacement construction begins");
        lifecycle.CancelProfileReplacement();
        lifecycle.BeginProfileReplacement();
        lifecycle.CommitProfileReplacement();
        string retiredStart = string.Empty;
        try
        {
            _ = await lifecycle.InvokeAsync("profile_instance_start", payload, CancellationToken.None).ConfigureAwait(false);
        }
        catch (BridgeCommandException error)
        {
            retiredStart = error.Code;
        }
        Require(retiredStart == "GAME_OPERATION_CANCELLED",
            "atomic profile retirement must prevent any late Start from entering the retired lifecycle");
    }

    private static string CaptureReplacementAdmissionError(OverviewLifecycleService lifecycle)
    {
        try
        {
            lifecycle.BeginProfileReplacement();
            return string.Empty;
        }
        catch (BridgeCommandException error)
        {
            return error.Code;
        }
    }

    private static async Task VerifyNativeMapServiceBoundaryAsync(
        ProfileRuntimeOwner owner,
        string root)
    {
        const string profileId = "profile-A";
        const int serverId = 317;
        foreach (string kind in Map317.MapKinds.All)
        {
            object? result = await owner.Backend.InvokeAsync(
                "map_search",
                JsonSerializer.SerializeToElement(new
                {
                    profileId,
                    kind,
                    query = new
                    {
                        serverId,
                        page = 1,
                        pageSize = 50,
                        sorts = new[] { new { sortBy = "updatedAt", sortOrder = "desc" } },
                    },
                }, JsonOptions.Default),
                CancellationToken.None).ConfigureAwait(false);
            JsonElement json = JsonSerializer.SerializeToElement(result, JsonOptions.Default);
            Require(json.GetProperty("total").GetInt32() >= 1,
                $"injected native service must expose recovered {kind} rows");
        }

        JsonElement options = JsonSerializer.SerializeToElement(
            await owner.Backend.InvokeAsync(
                "map_data_options",
                JsonSerializer.SerializeToElement(new { profileId, serverId }, JsonOptions.Default),
                CancellationToken.None).ConfigureAwait(false),
            JsonOptions.Default);
        Require(options.GetProperty("serverId").GetInt32() == serverId,
            "native Map options are scoped to the injected live server");

        int markEvents = 0;
        owner.Map317.PlayerMarkChanged += () => markEvents++;
        _ = await owner.Backend.InvokeAsync(
            "map_player_mark_set",
            JsonSerializer.SerializeToElement(new
            {
                profileId,
                row = new { serverId, ownerUid = "city-A-owner", ownerName = "Alpha Runtime" },
                marked = true,
            }, JsonOptions.Default),
            CancellationToken.None).ConfigureAwait(false);
        Require(markEvents == 1,
            "native Map player-mark command must publish its service event exactly once");

        _ = await owner.Backend.InvokeAsync(
            "server_jump_history_set",
            JsonSerializer.SerializeToElement(new { profileId, history = new[] { 317, 318, 317 } }, JsonOptions.Default),
            CancellationToken.None).ConfigureAwait(false);
        JsonElement history = JsonSerializer.SerializeToElement(
            await owner.Backend.InvokeAsync(
                "server_jump_history_get",
                JsonSerializer.SerializeToElement(new { profileId }, JsonOptions.Default),
                CancellationToken.None).ConfigureAwait(false),
            JsonOptions.Default);
        Require(history.EnumerateArray().Select(value => value.GetInt32()).SequenceEqual(new[] { 317, 318 }),
            "native Map jump history preserves recovered de-duplication/order");

        await ExpectBridgeErrorAsync(
            owner.Backend,
            "server_jump",
            new { profileId, serverId = 318 },
            "GAME_CONNECTION_UNAVAILABLE",
            "unavailable injected action provider must reject server navigation").ConfigureAwait(false);

        int dispatchEvents = 0;
        int truckEvents = 0;
        owner.Map317.DispatchPlunderChanged += () => dispatchEvents++;
        owner.Map317.TruckPlunderChanged += () => truckEvents++;
        long future = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds();
        _ = await owner.Backend.InvokeAsync(
            "map_dispatch_plunder_schedule",
            JsonSerializer.SerializeToElement(new
            {
                profileId,
                rows = new[]
                {
                    new
                    {
                        serverId,
                        ownerServer = serverId,
                        uuid = "7001",
                        ownerName = "Native Dispatch",
                        completionTime = future - 10_000,
                        plunderAt = future,
                        taskExpireTime = future + 60_000,
                        stolenCount = 0,
                        maxStealCount = 2,
                        rewards = Array.Empty<object>(),
                    },
                },
            }, JsonOptions.Default),
            CancellationToken.None).ConfigureAwait(false);
        _ = await owner.Backend.InvokeAsync(
            "map_truck_plunder_schedule",
            JsonSerializer.SerializeToElement(new
            {
                profileId,
                rows = new[]
                {
                    new
                    {
                        serverId,
                        uuid = "8001",
                        ownerName = "Native Truck",
                        executeAt = future,
                        maxLootCount = 2,
                        robTimes = 0,
                        expireAt = future + 60_000,
                    },
                },
            }, JsonOptions.Default),
            CancellationToken.None).ConfigureAwait(false);
        Require(dispatchEvents == 1 && truckEvents == 1,
            "workers-enabled native service publishes durable Dispatch/Truck scheduling events");
        JsonElement jobs = JsonSerializer.SerializeToElement(
            await owner.Backend.InvokeAsync(
                "map_plunder_jobs_list",
                JsonSerializer.SerializeToElement(new { profileId }, JsonOptions.Default),
                CancellationToken.None).ConfigureAwait(false),
            JsonOptions.Default);
        Require(jobs.GetProperty("dispatchJobs").GetArrayLength() == 1 &&
                jobs.GetProperty("truckJobs").GetArrayLength() == 1,
            "workers-enabled native service exposes both durable job stores");

        string exportPath = Path.Combine(root, "native-service-city.xlsx");
        Map317CityExportRequest export = owner.Map317.PrepareCityExport(
            JsonSerializer.SerializeToElement(new
            {
                profileId,
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
                    serverId,
                    page = 1,
                    pageSize = 50,
                    sorts = new[] { new { sortBy = "updatedAt", sortOrder = "desc" } },
                },
            }, JsonOptions.Default));
        JsonElement exported = JsonSerializer.SerializeToElement(
            owner.Map317.WriteCityExport(export, exportPath),
            JsonOptions.Default);
        Require(File.Exists(exportPath) && exported.GetProperty("rowCount").GetInt32() == 1,
            "native service export writes an actual workbook from persisted Map rows");
        string blockedExport = Path.Combine(root, "blocked-export-parent");
        File.WriteAllText(blockedExport, "not-a-directory");
        bool exportFailed = false;
        try { _ = owner.Map317.WriteCityExport(export, Path.Combine(blockedExport, "cities.xlsx")); }
        catch (BridgeCommandException error) when (error.Code == "MAP_EXPORT_FAILED") { exportFailed = true; }
        Require(exportFailed,
            "native service export write failure must be observable as MAP_EXPORT_FAILED");

        _ = await owner.Backend.InvokeAsync(
            "map_scan_clear",
            JsonSerializer.SerializeToElement(new { profileId, serverId }, JsonOptions.Default),
            CancellationToken.None).ConfigureAwait(false);
        foreach (string kind in Map317.MapKinds.All)
        {
            object? cleared = await owner.Backend.InvokeAsync(
                "map_search",
                JsonSerializer.SerializeToElement(new
                {
                    profileId,
                    kind,
                    query = new { serverId, page = 1, pageSize = 50 },
                }, JsonOptions.Default),
                CancellationToken.None).ConfigureAwait(false);
            Require(JsonSerializer.SerializeToElement(cleared, JsonOptions.Default)
                    .GetProperty("total").GetInt32() == 0,
                $"freshly authorized Clear removes only current server {kind} rows");
        }
    }

    private static void SeedCity(string profileRoot, int serverId, string key, string name)
    {
        string databasePath = Path.Combine(profileRoot, "map-data", "map-data.db");
        using var store = new Map317.MapStore(databasePath);
        store.UpsertRecord(new Map317.MapRecord(
            "city",
            serverId,
            key,
            1,
            key + "-uuid",
            name,
            "QA",
            30,
            null,
            1_000_000,
            1.0,
            null,
            1_800_000_000_000,
            JsonSerializer.Serialize(new
            {
                serverId,
                recordKey = key,
                uuid = key + "-uuid",
                ownerUid = key + "-owner",
                ownerName = name,
            })));
    }

    private static void SeedEightKinds(
        string profileRoot,
        int serverId,
        string suffix,
        string cityName)
    {
        string databasePath = Path.Combine(profileRoot, "map-data", "map-data.db");
        using var store = new Map317.MapStore(databasePath);
        long now = 1_800_000_000_000;
        Map317.MapRecord[] records =
        [
            new("city", serverId, $"city-{suffix}", 1, $"city-{suffix}-uuid", cityName, "QA", 30, null, 1_000_000, 1, null, now + 1,
                JsonSerializer.Serialize(new { serverId, recordKey = $"city-{suffix}", uuid = $"city-{suffix}-uuid", ownerUid = $"city-{suffix}-owner", ownerName = cityName, allianceName = "QA", level = 30, updatedAt = now + 1 })),
            new("resource", serverId, $"resource-{suffix}", 2, $"resource-{suffix}-uuid", null, null, 10, null, null, 2, null, now + 2,
                JsonSerializer.Serialize(new { serverId, recordKey = $"resource-{suffix}", uuid = $"resource-{suffix}-uuid", resourceNameKey = "resource.iron", level = 10, updatedAt = now + 2 })),
            new("monster", serverId, $"monster-{suffix}", 3, $"monster-{suffix}-uuid", null, null, 20, null, null, 3, null, now + 3,
                JsonSerializer.Serialize(new { serverId, recordKey = $"monster-{suffix}", uuid = $"monster-{suffix}-uuid", monsterNameKey = "monster.doom", level = 20, updatedAt = now + 3 })),
            new("truck", serverId, $"truck-{suffix}", 4, $"truck-{suffix}-uuid", null, null, null, 5, null, 4, null, now + 4,
                JsonSerializer.Serialize(new { serverId, recordKey = $"truck-{suffix}", uuid = $"truck-{suffix}-uuid", quality = 5, arriveTs = now + 100_000, maxLootCount = 2, robTimes = 0, updatedAt = now + 4 })),
            new("railway", serverId, $"railway-{suffix}", 5, $"railway-{suffix}-uuid", null, null, null, 4, null, 5, null, now + 5,
                JsonSerializer.Serialize(new { serverId, recordKey = $"railway-{suffix}", uuid = $"railway-{suffix}-uuid", quality = 4, arriveTs = now + 100_000, maxLootCount = 2, robTimes = 0, updatedAt = now + 5 })),
            new("dispatch", serverId, $"dispatch-{suffix}", 6, $"dispatch-{suffix}-uuid", null, null, 5, 5, null, 6, null, now + 6,
                JsonSerializer.Serialize(new { serverId, recordKey = $"dispatch-{suffix}", uuid = $"dispatch-{suffix}-uuid", level = 5, quality = 5, completionTime = now - 1_000, taskExpireTime = now + 100_000, stolenCount = 0, maxStealCount = 2, updatedAt = now + 6 })),
            new("ghost", serverId, $"ghost-{suffix}", 7, $"ghost-{suffix}-uuid", null, null, 6, 4, null, 7, null, now + 7,
                JsonSerializer.Serialize(new { serverId, recordKey = $"ghost-{suffix}", uuid = $"ghost-{suffix}-uuid", level = 6, quality = 4, completionTime = now + 10_000, updatedAt = now + 7 })),
            new("treasure", serverId, $"treasure-{suffix}", 8, $"treasure-{suffix}-uuid", null, null, null, null, null, 8, null, now + 8,
                JsonSerializer.Serialize(new { serverId, recordKey = $"treasure-{suffix}", uuid = $"treasure-{suffix}-uuid", treasureType = 5, suppliesType = 0, treasureNameKey = "treasure.five", complete = true, updatedAt = now + 8 })),
        ];
        foreach (Map317.MapRecord record in records) store.UpsertRecord(record);
    }

    private static async Task ExpectBridgeErrorAsync(
        LWBridgeBackend backend,
        string command,
        object payload,
        string code,
        string label)
    {
        string actual = string.Empty;
        try
        {
            _ = await backend.InvokeAsync(
                command,
                JsonSerializer.SerializeToElement(payload, JsonOptions.Default),
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (BridgeCommandException error)
        {
            actual = error.Code;
        }
        Require(actual == code, $"{label}: expected {code}, got {actual}");
    }

    private static void InsertSecondaryProfile(
        string databasePath,
        string profileId,
        string displayName,
        long now)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO profiles(
                id, display_name, role_name, server_id, game_uid,
                note, display_order, enabled, locked_reason,
                is_primary, created_at, updated_at, last_launched_at)
            VALUES (
                $id, $displayName, NULL, NULL, NULL,
                '', 1, 1, NULL,
                0, $now, $now, NULL)
            """;
        command.Parameters.AddWithValue("$id", profileId);
        command.Parameters.AddWithValue("$displayName", displayName);
        command.Parameters.AddWithValue("$now", now);
        command.ExecuteNonQuery();
    }

    private static async Task<string> SearchSingleCityAsync(
        LWBridgeBackend backend,
        string profileId,
        int serverId)
    {
        object? result = await backend.InvokeAsync(
            "map_search",
            JsonSerializer.SerializeToElement(new
            {
                profileId,
                kind = "city",
                query = new
                {
                    serverId,
                    page = 1,
                    pageSize = 50,
                    sorts = new[] { new { sortBy = "updatedAt", sortOrder = "desc" } },
                },
            }, JsonOptions.Default),
            CancellationToken.None).ConfigureAwait(false);
        JsonElement json = JsonSerializer.SerializeToElement(result, JsonOptions.Default);
        Require(json.GetProperty("total").GetInt32() == 1 &&
                json.GetProperty("rows").GetArrayLength() == 1,
            "profile Map query returns exactly one isolated row");
        return json.GetProperty("rows")[0].GetProperty("ownerName").GetString() ?? string.Empty;
    }

    private static async Task ExpectProfileScopeMismatchAsync(
        LWBridgeBackend backend,
        string foreignProfileId)
    {
        string code = string.Empty;
        try
        {
            _ = await backend.InvokeAsync(
                "get_status",
                JsonSerializer.SerializeToElement(new { profileId = foreignProfileId }, JsonOptions.Default),
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (BridgeCommandException error)
        {
            code = error.Code;
        }
        Require(code == "PROFILE_SCOPE_MISMATCH",
            "replacement backend rejects status reads for retired/foreign profile owner");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
