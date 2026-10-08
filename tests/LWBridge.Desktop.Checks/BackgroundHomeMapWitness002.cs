using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using LWBridge.Desktop;
using LWBridge.Map317;

namespace LWBridge.Desktop.Checks;

/// <summary>
/// Explicit, test-owned, console-only composition of real production profile services.
/// No fixture map provider, no default-root fallback, no UI automation or Auto scheduler.
/// </summary>
internal static class BackgroundHomeMapWitness002
{
    private static string GameRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FunFly", "Last War-Survival Game");
    private static readonly IReadOnlyDictionary<string, string> OriginalHashes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["LWScripts.data"] = "248f3aeac712b3f14f86bff37a0c365e467897a2248403837c44c1b774f05b22",
            ["LWScripts.txt"] = "d2f9bcc165f41427d4b3b7b9243e4a483bf316519a16b4ce9be658ba9afa783a",
            ["version.txt"] = "785f3ec7eb32f30b90cd0fcf3657d388b5ff4297f2f9716ff66e9b69c05ddd09",
        };

    internal static void RunInverseChecks()
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        JsonElement good = J(new {
            profileId = "witness-profile", instanceId = "witness-session",
            pid = 1001, startedAt = now - 1000, lastHeartbeatAt = now,
            bridgeConnected = true, connectionState = "connected", identityConfirmed = true
        });
        Assert(Correlated(good, "witness-profile", "witness-session", 1001, now, 1, 1), "valid host witness");
        Assert(!Correlated(good, "other-profile", "witness-session", 1001, now, 1, 1), "profile mismatch");
        Assert(!Correlated(good, "witness-profile", "other-session", 1001, now, 1, 1), "session mismatch");
        Assert(!Correlated(good, "witness-profile", "witness-session", 1002, now, 1, 1), "PID mismatch");
        Assert(!Correlated(good, "witness-profile", "witness-session", 1001, now, 0, 0), "no host ACK");
        Assert(!Correlated(good, "witness-profile", "witness-session", 1001, now + 16000, 1, 1), "stale heartbeat");
        Assert(!Correlated(J(new {
            profileId = "witness-profile", instanceId = "witness-session", pid = 1001,
            startedAt = now - 1000, lastHeartbeatAt = now, bridgeConnected = false,
            connectionState = "reconnecting", identityConfirmed = true
        }), "witness-profile", "witness-session", 1001, now, 1, 1), "hello without route");
        Console.WriteLine("BACKGROUND_WITNESS_002_INVERSES_PASS 7/7 (inert observation classifier; host inverse tests separate)");
    }

    private static bool Correlated(JsonElement status, string profile, string instance, int pid,
        long now, int authenticated, int routes)
    {
        return authenticated > 0 && routes > 0 &&
            String(status, "profileId") == profile &&
            String(status, "instanceId") == instance &&
            Int(status, "pid") == pid &&
            Bool(status, "bridgeConnected") && Bool(status, "identityConfirmed") &&
            String(status, "connectionState") == "connected" &&
            Long(status, "startedAt") is long started && started <= now &&
            Long(status, "lastHeartbeatAt") is long heartbeat &&
            heartbeat <= now + 2000 && now - heartbeat < 15001;
    }

    internal static async Task RunAsync(string outputFile, bool launch)
    {
        string output = Path.GetFullPath(outputFile);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        string attemptId = Guid.NewGuid().ToString("N");
        string profile = "lwb317-background-witness-002-" + attemptId[..8];
        string root = Path.Combine(Path.GetTempPath(), "LWB317-BACKGROUND-WITNESS-002-" + attemptId);
        DesktopApplicationPaths paths = DesktopApplicationPaths.Create(root);
        var events = new List<object>();
        var report = new Dictionary<string, object?>
        {
            ["workItem"] = "LWB317-FUNCTION-HOME-MAP-BACKGROUND-WITNESS-002",
            ["attemptId"] = attemptId, ["mode"] = launch ? "explicit-real-launch" : "isolated-preflight",
            ["createdAtUtc"] = DateTimeOffset.UtcNow, ["profileId"] = profile,
            ["isolatedRoot"] = root, ["outputFile"] = output,
            ["productionMapProvider"] = "ProfileRuntimeOwner.Create mapProvider:null -> Map317CommandService(lifecycle) -> CurrentClientMap317ScanProvider",
            ["autoSchedulerStarted"] = false, ["uiInteraction"] = false, ["events"] = events
        };
        void Record(string kind, object? details = null)
        {
            events.Add(new { atUtc = DateTimeOffset.UtcNow, kind, details });
            File.WriteAllText(output, JsonSerializer.Serialize(report,
                new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine("WITNESS_002 " + kind);
        }

        string scripts = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "AppData", "LocalLow", "FunFly", "Last War-Survival Game", "lwScripts");
        var files = OriginalHashes.Keys.ToDictionary(n => n, n => Path.Combine(scripts, n));
        Dictionary<string, string?> Hashes() => files.ToDictionary(
            p => p.Key,
            p => File.Exists(p.Value) ? HashFile(p.Value) : null);
        bool OriginalMatches(IReadOnlyDictionary<string, string?> hashes) =>
            OriginalHashes.All(pair => hashes.TryGetValue(pair.Key, out string? value) &&
                                  string.Equals(value, pair.Value, StringComparison.OrdinalIgnoreCase));
        string? instance = null;
        int? ownedPid = null;
        string? ownedStartUtc = null;
        string? run = null;
        int serverId = 0;
        bool stopSucceeded = false;
        bool mapStopSucceeded = false;
        try
        {
            report["installedBefore"] = Hashes();
            Assert(OriginalMatches((Dictionary<string, string?>)report["installedBefore"]!), "installed files must exactly match original triplet before composition");
            Assert(!Directory.Exists(root), "isolated root must be fresh");
            string originalsBackup = Path.Combine(paths.OverviewBackupRoot, "preflight-originals-" + attemptId);
            Directory.CreateDirectory(originalsBackup);
            foreach (var file in files)
                File.Copy(file.Value, Path.Combine(originalsBackup, file.Key), overwrite: false);
            Dictionary<string, string?> backupHashes = files.ToDictionary(pair => pair.Key, pair => (string?)HashFile(Path.Combine(originalsBackup, pair.Key)));
            Assert(OriginalMatches(backupHashes), "immutable independent preflight backups match original hashes");
            report["preflightOriginalBackup"] = new { path = originalsBackup, hashes = backupHashes };
            report["recoveryJournalBefore"] = File.Exists(Path.Combine(paths.OverviewRuntimeRoot, "recovery.json"));
            Assert(!(bool)report["recoveryJournalBefore"]!, "no pending recovery journal in fresh isolated root");
            Record("independent-exact-original-backup", report["preflightOriginalBackup"]);            string[] prior = Process.GetProcesses()
                .Where(p => p.ProcessName is "LastWar" or "LastWarLauncher" or "LWBridge.Desktop")
                .Select(p => { try { return p.ProcessName + ":" + p.Id + ":" + p.StartTime.ToUniversalTime().ToString("O"); }
                    catch { return p.ProcessName + ":" + p.Id + ":unreadable"; } })
                .ToArray();
            report["priorProcesses"] = prior;
            if (launch)
                Assert(prior.Length == 0, "pilot refuses launch while game/clone processes exist");

            Assert(paths.ExplicitIsolation && !paths.Contains(DesktopApplicationPaths.DefaultRoot),
                "isolated root must not overlap owner default data");
            var seed = LWBridgeLocalConfig.CreateDefault() with
            {
                ProfileId = profile,
                GameRoot = GameRoot,
                AutoLaunchGame = false,
                AutoReconnect = false,
                GameDesiredRunning = false
            };
            var config = new LocalConfigStore(root, initialValue: seed);
            var admission = new GameInstallationService(config).GetLaunchAdmissionStatus();
            report["gameAdmission"] = new { admission.Valid, admission.Path, admission.Error, admission.GamePath,
                admission.XluaPath, admission.Is64Bit };
            Assert(admission.Valid &&
                string.Equals(Path.GetFullPath(admission.Path), Path.GetFullPath(GameRoot), StringComparison.OrdinalIgnoreCase),
                "exact game-root admission");
            using var registryStore = new ProfileRegistryStore(paths.ControllerDatabasePath);
            registryStore.EnsureLocalProfile(profile, "Background Witness 002", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            registryStore.SelectProfile(profile);
            using var registry = new ProfileRegistryCommandService(registryStore, maxProfiles: 2);
            using var owner = ProfileRuntimeOwner.Create(profile, config, paths.ProfileRoot(profile),
                registry, startAutoScheduler: false, startRecoveryMonitor: true, startBridgeTransport: true,
                applicationDataRoot: paths.Root, overviewRuntimeRoot: paths.OverviewRuntimeRoot,
                overviewEvidenceRoot: paths.OverviewEvidenceRoot, overviewBackupRoot: paths.OverviewBackupRoot);
            Assert(owner.OverviewLifecycle.ApplicationDataRoot == paths.Root &&
                owner.OverviewLifecycle.RuntimeRoot == paths.OverviewRuntimeRoot &&
                owner.OverviewLifecycle.BackupRoot == paths.OverviewBackupRoot,
                "production lifecycle remains isolated");
            Assert(owner.BridgeHostState.IsRpcTransportStarted, "production host listener started");
            Assert(!config.Snapshot.AutoLaunchGame && !config.Snapshot.AutoReconnect &&
                !config.Snapshot.GameDesiredRunning, "Auto and desired-running are off");
            Record("real-provider-composed", new { profile, root, hostStarted = owner.BridgeHostState.IsRpcTransportStarted,
                owner.BridgeHostState.PendingRegistrationCount, owner.BridgeHostState.ConnectedRouteCount });

            using JsonDocument emptyDocument = JsonDocument.Parse("{}");
            JsonElement empty = emptyDocument.RootElement;
            try
            {
                await owner.OverviewLifecycle.InvokeAsync("profile_instance_stop", J(new { instanceId = "stale-witness-session" }), CancellationToken.None);
                throw new InvalidDataException("stale stop was accepted");
            }
            catch (BridgeCommandException ex) when (ex.Code == "INSTANCE_NOT_OWNED")
            {
                Record("stale-stop-rejected", new { ex.Code });
            }
            if (!launch)
            {
                try
                {
                    await owner.Map317.InvokeAsync("map_scan_start", J(new { selectedTypes = new[] { "resource" }, scanMode = "normal" }), CancellationToken.None);
                    throw new InvalidDataException("disconnected provider accepted Resource acquisition");
                }
                catch (BridgeCommandException ex)
                {
                    Record("disconnected-resource-rejected", new { ex.Code, ex.Message });
                }
                report["terminal"] = "PREFLIGHT_ONLY_NO_GAME";
                Record("preflight-complete");
                return;
            }

            using var overall = new CancellationTokenSource(TimeSpan.FromMinutes(7));
            try
            {
                JsonElement started = J(await owner.OverviewLifecycle.InvokeAsync("profile_instance_start", empty, overall.Token));
                instance = String(started, "instanceId");
                ownedPid = Int(started, "pid");
                if (ownedPid is int pid)
                {
                    using Process process = Process.GetProcessById(pid);
                    ownedStartUtc = process.StartTime.ToUniversalTime().ToString("O");
                    Assert(string.Equals(Path.GetFullPath(process.MainModule?.FileName ?? string.Empty),
                        Path.GetFullPath(Path.Combine(GameRoot, "Game", "LastWar.exe")),
                        StringComparison.OrdinalIgnoreCase), "exact selected process executable must match");
                }
                Record("real-home-start-returned", new { instance, ownedPid, ownedStartUtc, started });
                Assert(!string.IsNullOrWhiteSpace(instance) && ownedPid is not null, "start session and game PID required");
                JsonElement profileStatus = J(owner.OverviewLifecycle.CreateProfileInstanceStatus());
                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                bool accepted = Correlated(profileStatus, profile, instance!, ownedPid.GetValueOrDefault(), now,
                    owner.BridgeHostState.AuthenticatedSessionCount, owner.BridgeHostState.ConnectedRouteCount);
                Record("authenticated-host-observation", new
                {
                    profile, instance, ownedPid, ownedStartUtc,
                    profileStatus, accepted,
                    owner.BridgeHostState.AuthenticatedSessionCount,
                    owner.BridgeHostState.ConnectedRouteCount,
                    owner.BridgeHostState.RejectedHandshakeCount,
                    owner.BridgeHostState.PendingCallCount,
                    owner.BridgeHostState.LastHandshakeError
                });
                if (!accepted)
                {
                    report["terminal"] = "BLOCKED_NO_CORRELATED_HOST_ACK";
                    return;
                }
                object? observedMap = await owner.Map317.InvokeAsync("map_scan_status", empty, overall.Token);
                JsonElement context = J(observedMap);
                Record("production-map-context-rpc-attempt", new { context, owner.BridgeHostState.PendingCallCount,
                    protocol = ReadRuntimeProtocolSummaries(paths, profile, instance!, ownedPid.GetValueOrDefault()) });
                if (!Bool(context, "isInWorld") || Int(context, "serverId") is not > 0)
                {
                    report["terminal"] = "BLOCKED_WORLD_OR_LOGIN_READINESS";
                    return;
                }
                object? startedScan = await owner.Map317.InvokeAsync("map_scan_start",
                    J(new { profileId = profile, scanMode = "normal", selectedTypes = new[] { "resource" } }), overall.Token);
                JsonElement start = J(startedScan);
                run = String(start, "scanRunId");
                serverId = Int(start, "serverId") ?? 0;
                Assert(!string.IsNullOrWhiteSpace(run) && serverId > 0, "new durable Resource run identity");
                Record("resource-start", new { run, serverId, start });
                DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMinutes(3);
                while (DateTimeOffset.UtcNow < deadline && !overall.Token.IsCancellationRequested)
                {
                    JsonElement state = J(owner.Map317.CreateStatus());
                    Assert(String(state, "scanRunId") == run, "same Resource run identity until termination");
                    if (Int(state, "readBlocks") is > 0 ||
                        !Bool(state, "isReading"))
                    {
                        Record("resource-capture-or-terminal", new { state,
                        protocol = ReadRuntimeProtocolSummaries(paths, profile, instance!, ownedPid.GetValueOrDefault()) });
                        break;
                    }
                    await Task.Delay(1500, overall.Token);
                }
                JsonElement last = J(owner.Map317.CreateStatus());
                if (Bool(last, "isReading"))
                {
                    Assert(String(last, "scanRunId") == run, "stop must target the owned active run");
                    object? stopped = await owner.Map317.InvokeAsync("map_scan_stop", empty, overall.Token);
                    mapStopSucceeded = true;
                    Record("production-map-stop", J(stopped));
                }
                else
                {
                    Record("resource-natural-terminal", last);
                }
                MapQuery q = new("resource", serverId);
                object? search = await owner.Map317.InvokeAsync("map_search", J(new { kind = "resource", serverId, page = 1, pageSize = 50 }), overall.Token);
                Record("production-resource-query", new { run, result = J(search),
                    protocol = ReadRuntimeProtocolSummaries(paths, profile, instance!, ownedPid.GetValueOrDefault()) });
                object? second = await owner.Map317.InvokeAsync("map_search", J(new { kind = "resource", serverId, page = 2, pageSize = 50 }), overall.Token);
                Record("production-resource-page-2", J(second));
                object? filtered = await owner.Map317.InvokeAsync("map_search", J(new { kind = "resource", serverId, page = 1, pageSize = 50, keyword = "__witness002_unlikely_name__" }), overall.Token);
                Record("production-resource-filter", J(filtered));
                using (var reopened = new MapStore(paths.MapDatabasePath(profile)))
                {
                    MapScanRun? durable = reopened.ReadScanRun(run!);
                    LWBridge.Map317.MapSearchResult rows = reopened.Search(q);
                    report["reopenedPersistence"] = new { run, durable, total = rows.Total, firstPageCount = rows.Rows.Count };
                    Record("sqlite-reopened-actual-run", report["reopenedPersistence"]);
                }
                report["resourceExport"] = "NOT_AVAILABLE: production Map317 exporter is City-only; no Resource export or UI claim";
                report["terminal"] = "RESOURCE_REQUEST_OBSERVED";
            }
            catch (Exception ex)
            {
                report["liveException"] = new { type = ex.GetType().Name,
                    code = (ex as BridgeCommandException)?.Code, ex.Message };
                report["terminal"] = "BLOCKED_LIVE_PREREQUISITE_OR_FAILURE";
                Record("real-boundary-failure", report["liveException"]);
            }
            finally
            {
                if (run is not null && owner.Map317.IsScanActive && !mapStopSucceeded)
                {
                    try
                    {
                        JsonElement scanStopStatus = J(owner.Map317.CreateStatus());
                        if (String(scanStopStatus, "scanRunId") == run)
                        {
                            object? stopped = await owner.Map317.InvokeAsync("map_scan_stop", empty, CancellationToken.None);
                            mapStopSucceeded = true;
                            Record("finally-owned-resource-stop", J(stopped));
                        }
                    }
                    catch (Exception e) { Record("resource-stop-failed", new { e.GetType().Name, e.Message }); }
                }
                JsonElement? status = J(owner.OverviewLifecycle.CreateProfileInstanceStatus());
                if (instance is not null && status.HasValue &&
                    String(status.Value, "instanceId") == instance &&
                    Int(status.Value, "pid") == ownedPid)
                {
                    try
                    {
                        object? stopped = await owner.OverviewLifecycle.InvokeAsync(
                            "profile_instance_stop", J(new { instanceId = instance }), CancellationToken.None);
                        stopSucceeded = true;
                        Record("owned-home-stop", J(stopped));
                    }
                    catch (Exception e) { Record("owned-home-stop-failed", new { e.GetType().Name, e.Message }); }
                }
                else
                    Record("no-owned-session-to-stop", new { instance, ownedPid, status });
            }
        }
        catch (Exception ex)
        {
            report["failure"] = new { type = ex.GetType().Name,
                code = (ex as BridgeCommandException)?.Code, ex.Message };
            report["terminal"] = "PREFLIGHT_OR_COMPOSITION_FAILED";
            Record("preflight-or-composition-failed", report["failure"]);
        }
        finally
        {
            report["recoveryJournalAfter"] = File.Exists(Path.Combine(paths.OverviewRuntimeRoot, "recovery.json"));
            report["installedAfter"] = Hashes();
            report["originalHashesRestored"] = OriginalMatches((Dictionary<string, string?>)report["installedAfter"]!);
            report["ownedGameStopSucceeded"] = stopSucceeded;
            report["resourceStopSucceeded"] = mapStopSucceeded;
            report["finishedAtUtc"] = DateTimeOffset.UtcNow;
            Record("final-restoration-inventory", new
            {
                originalHashesRestored = report["originalHashesRestored"], ownedGameStopSucceeded = report["ownedGameStopSucceeded"],
                installedAfter = report["installedAfter"],
                activeProcesses = Process.GetProcesses()
                    .Where(p => p.ProcessName is "LastWar" or "LastWarLauncher" or "LWBridge.Desktop")
                    .Select(p => new { p.Id, p.ProcessName }).ToArray()
            });
        }
        if (string.Equals(report.GetValueOrDefault("terminal")?.ToString(), "PREFLIGHT_OR_COMPOSITION_FAILED", StringComparison.Ordinal))
            throw new InvalidDataException("Background witness preflight/composition failed; see preserved JSON.");
    }

    // Only source-backed, nonsensitive telemetry fields: never copy challenges, Lua command bodies or tokens.
    private static object[] ReadRuntimeProtocolSummaries(DesktopApplicationPaths paths,
        string profileId, string sessionId, int pid)
    {
        string[] relativePaths =
        [
            Path.Combine("overview-bridge", "world-state-result.json"),
            Path.Combine("overview-bridge", "world-ready-result.json"),
            Path.Combine("live-resource", "result.json"),
            Path.Combine("live-resource", "resource-scan-detail-result.json"),
        ];
        var summaries = new List<object>();
        foreach (string relative in relativePaths)
        {
            string path = Path.Combine(paths.Root, relative);
            if (!File.Exists(path)) continue;
            try
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
                JsonElement content = document.RootElement;
                summaries.Add(new
                {
                    file = relative, fileUpdatedAtUtc = File.GetLastWriteTimeUtc(path),
                    requestId = String(content, "requestId"),
                    profileMatched = String(content, "profileId") == profileId,
                    sessionMatched = String(content, "sessionId") == sessionId,
                    gamePidMatched = Int(content, "gamePid") == pid,
                    status = String(content, "status"),
                    state = String(content, "state"),
                    reason = String(content, "error"),
                    serverId = Int(content, "serverId")
                });
            }
            catch (IOException) { }
            catch (JsonException) { }
        }
        return summaries.ToArray();
    }
    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
    private static JsonElement J(object? value) =>
        JsonSerializer.SerializeToElement(value, JsonOptions.Default);
    private static string? String(JsonElement value, string property) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty(property, out var x) &&
        x.ValueKind == JsonValueKind.String ? x.GetString() : null;
    private static int? Int(JsonElement value, string property) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty(property, out var x) &&
        x.ValueKind == JsonValueKind.Number && x.TryGetInt32(out int n) ? n : null;
    private static long? Long(JsonElement value, string property) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty(property, out var x) &&
        x.ValueKind == JsonValueKind.Number && x.TryGetInt64(out long n) ? n : null;
    private static bool Bool(JsonElement value, string property) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty(property, out var x) &&
        x.ValueKind == JsonValueKind.True;
    private static void Assert(bool value, string reason)
    {
        if (!value) throw new InvalidDataException("BACKGROUND_WITNESS_002: " + reason);
    }
}
