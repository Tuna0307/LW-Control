using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using LWBridge.Desktop;
using LWBridge.Map317;

namespace LWBridge.Desktop.Checks;

/// <summary>
/// COMPLETION-010 G: explicit console-only composition of the real production profile
/// services (isolated root, real xLua provider, real Home Start/Stop) driving the
/// bounded current-server Manual City then Resource slice through the production
/// Map317 command service: acquisition, publication, search/options/summary, filter,
/// pagination, City export, SQLite reopen, profile isolation and exact Stop.
/// No UI automation, Auto scheduler, fixture provider or protected-service access.
/// Observation timeline records durable staging versus progress so a later
/// positive-stage Stop attempt has a distinguishing plan instead of a blind scan.
/// </summary>
internal static class Completion010LivePilot
{
    private static string GameRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FunFly", "Last War-Survival Game");

    internal static async Task RunAsync(string outputFile, string preflightJson, bool positiveStageAttempt)
    {
        string output = Path.GetFullPath(outputFile);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        string attemptId = Guid.NewGuid().ToString("N");
        string profile = "lwb317-comp010-pilot-" + attemptId[..8];
        string root = Path.Combine(Path.GetTempPath(), "LWB317-COMP010-PILOT-" + attemptId);
        DesktopApplicationPaths paths = DesktopApplicationPaths.Create(root);
        var events = new List<object>();
        var report = new Dictionary<string, object?>
        {
            ["workItem"] = "LWB317-FUNCTION-HOME-MAP-COMPLETION-010/G",
            ["attemptId"] = attemptId, ["createdAtUtc"] = DateTimeOffset.UtcNow,
            ["profileId"] = profile, ["isolatedRoot"] = root,
            ["productionMapProvider"] = "ProfileRuntimeOwner.Create -> Map317CommandService(lifecycle) -> CurrentClientMap317ScanProvider",
            ["autoSchedulerStarted"] = false, ["uiInteraction"] = false, ["events"] = events,
        };
        void Record(string kind, object? details = null)
        {
            events.Add(new { atUtc = DateTimeOffset.UtcNow, kind, details });
            File.WriteAllText(output, JsonSerializer.Serialize(report,
                new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine("PILOT010 " + kind);
        }

        // Expected originals come from the python preflight (current-client compat + SHA-256).
        JsonElement pre = JsonDocument.Parse(File.ReadAllText(preflightJson)).RootElement.Clone();
        Assert(pre.GetProperty("allowLiveHomeLaunch").GetBoolean(), "preflight must allow a live launch");
        var files = new Dictionary<string, (string Path, string Sha)>();
        foreach (var item in pre.GetProperty("triplet").EnumerateObject())
            files[item.Name] = (item.Value.GetProperty("path").GetString()!, item.Value.GetProperty("sha256").GetString()!);
        Dictionary<string, string?> Hashes() => files.ToDictionary(
            p => p.Key, p => File.Exists(p.Value.Path) ? HashFile(p.Value.Path) : null);
        bool OriginalMatches(Dictionary<string, string?> h) => files.All(
            p => string.Equals(h[p.Key], p.Value.Sha, StringComparison.OrdinalIgnoreCase));

        string? instance = null; int? ownedPid = null; bool stopSucceeded = false;
        var runs = new List<object>();
        try
        {
            report["installedBefore"] = Hashes();
            Assert(OriginalMatches((Dictionary<string, string?>)report["installedBefore"]!), "installed originals must match the preflight");
            Assert(!Directory.Exists(root), "isolated root must be fresh");
            Assert(Process.GetProcesses().All(p => p.ProcessName is not ("LastWar" or "LastWarLauncher" or "LWBridge.Desktop")),
                "refuses to launch while a game/clone process exists");
            Assert(paths.ExplicitIsolation && !paths.Contains(DesktopApplicationPaths.DefaultRoot), "isolated root must not overlap owner data");

            var seed = LWBridgeLocalConfig.CreateDefault() with
            {
                ProfileId = profile, GameRoot = GameRoot,
                AutoLaunchGame = false, AutoReconnect = false, GameDesiredRunning = false,
            };
            var config = new LocalConfigStore(root, initialValue: seed);
            var admission = new GameInstallationService(config).GetLaunchAdmissionStatus();
            Assert(admission.Valid, "exact game-root admission");
            using var registryStore = new ProfileRegistryStore(paths.ControllerDatabasePath);
            registryStore.EnsureLocalProfile(profile, "Completion 010 pilot", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            registryStore.SelectProfile(profile);
            using var registry = new ProfileRegistryCommandService(registryStore, maxProfiles: 2);
            using var owner = ProfileRuntimeOwner.Create(profile, config, paths.ProfileRoot(profile),
                registry, startAutoScheduler: false, startRecoveryMonitor: true, startBridgeTransport: true,
                applicationDataRoot: paths.Root, overviewRuntimeRoot: paths.OverviewRuntimeRoot,
                overviewEvidenceRoot: paths.OverviewEvidenceRoot, overviewBackupRoot: paths.OverviewBackupRoot);
            Assert(owner.BridgeHostState.IsRpcTransportStarted, "production host listener started");
            Record("real-provider-composed", new { profile, root });

            using JsonDocument emptyDocument = JsonDocument.Parse("{}");
            JsonElement empty = emptyDocument.RootElement;
            using var overall = new CancellationTokenSource(TimeSpan.FromMinutes(28));
            Environment.SetEnvironmentVariable("LWBRIDGE_PIPE_DIAGNOSTIC", "1");

            try
            {
            JsonElement started = J(await owner.OverviewLifecycle.InvokeAsync("profile_instance_start", empty, overall.Token));
            instance = String(started, "instanceId");
            ownedPid = Int(started, "pid");
            Assert(instance is not null && ownedPid is not null, "start session and PID");
            using (Process process = Process.GetProcessById(ownedPid!.Value))
                Assert(string.Equals(Path.GetFullPath(process.MainModule?.FileName ?? ""),
                    Path.GetFullPath(Path.Combine(GameRoot, "Game", "LastWar.exe")), StringComparison.OrdinalIgnoreCase),
                    "owned process is the exact installed LastWar.exe");
            Record("home-start-returned", new { instance, ownedPid, started });

            // Authenticated route (production host -> game) before any Map command.
            DateTimeOffset hostDeadline = DateTimeOffset.UtcNow.AddSeconds(45);
            bool accepted;
            do
            {
                JsonElement status = J(owner.OverviewLifecycle.CreateProfileInstanceStatus());
                accepted = owner.BridgeHostState.AuthenticatedSessionCount > 0 &&
                    owner.BridgeHostState.ConnectedRouteCount > 0 &&
                    String(status, "instanceId") == instance && String(status, "connectionState") == "connected";
                if (accepted) break;
                await Task.Delay(750, overall.Token);
            } while (DateTimeOffset.UtcNow < hostDeadline);
            Record("authenticated-host", new { accepted, owner.BridgeHostState.AuthenticatedSessionCount, owner.BridgeHostState.ConnectedRouteCount });
            Assert(accepted, "authenticated host route required");
            long sentAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            JsonElement? rpc = await owner.BridgeHostState.CallLuaAsync(instance!, "getStatus", empty, timestamp: sentAt,
                createdAt: sentAt, cancellationToken: overall.Token, resultTimeout: TimeSpan.FromSeconds(5),
                timeoutMessage: "pilot getStatus did not return");
            Record("production-getStatus-rpc", new { resultKind = rpc?.ValueKind.ToString() });

            JsonElement context = J(await owner.Map317.InvokeAsync("map_scan_status", empty, overall.Token));
            Record("map-context", context);
            int serverId = Int(context, "serverId") ?? 0;
            Assert(serverId > 0, "live server id");

            bool stagingPositiveBeforeTerminal = false;
            string? cityRunId = null;
            foreach (string kind in new[] { "city", "resource" })
            {
                JsonElement start = J(await owner.Map317.InvokeAsync("map_scan_start",
                    J(new { profileId = profile, scanMode = "normal", selectedTypes = new[] { kind } }), overall.Token));
                string run = String(start, "scanRunId")!;
                int runServer = Int(start, "serverId") ?? 0;
                Assert(!string.IsNullOrWhiteSpace(run) && runServer == serverId, kind + " run identity");
                var timeline = new List<object>();
                var sw = Stopwatch.StartNew();
                bool stagedPositive = false;
                JsonElement state;
                while (true)
                {
                    state = J(owner.Map317.CreateStatus());
                    Assert(String(state, "scanRunId") == run, "same run identity");
                    int staged = StagingCount(paths.MapDatabasePath(profile), run, kind);
                    if (staged > 0 && Bool(state, "isReading")) { stagedPositive = true; stagingPositiveBeforeTerminal = true; }
                    if (timeline.Count == 0 || sw.ElapsedMilliseconds / 5000 > (timeline.Count - 1))
                        timeline.Add(new { tSec = sw.Elapsed.TotalSeconds, completed = Int(state, "completedBlocks"),
                            inflight = Int(state, "inflightBlocks"), failed = Int(state, "failedBlocks"), staged });
                    if (!Bool(state, "isReading")) break;
                    if (sw.Elapsed > TimeSpan.FromMinutes(9)) { Record("run-too-long-stopping", new { kind, run }); await owner.Map317.InvokeAsync("map_scan_stop", empty, overall.Token); break; }
                    await Task.Delay(400, overall.Token);
                }
                JsonElement last = J(owner.Map317.CreateStatus());
                Record(kind + "-terminal", new { run, seconds = sw.Elapsed.TotalSeconds, last, stagedPositiveWhileReading = stagedPositive, timeline });

                // Query surface through the production command service.
                var q = new List<object>();
                JsonElement p1 = J(await owner.Map317.InvokeAsync("map_search", J(new { kind, query = new { serverId, page = 1, pageSize = 50 } }), overall.Token));
                JsonElement p2 = J(await owner.Map317.InvokeAsync("map_search", J(new { kind, query = new { serverId, page = 2, pageSize = 50 } }), overall.Token));
                int total = Int(p1, "total") ?? -1;
                int rows1 = p1.TryGetProperty("rows", out var r1) ? r1.GetArrayLength() : -1;
                int rows2 = p2.TryGetProperty("rows", out var r2) ? r2.GetArrayLength() : -1;
                JsonElement options = J(await owner.Map317.InvokeAsync("map_data_options", J(new { serverId }), overall.Token));
                JsonElement summary = J(await owner.Map317.InvokeAsync("map_summary", empty, overall.Token));
                JsonElement miss = J(await owner.Map317.InvokeAsync("map_search", J(new { kind, query = new { serverId, page = 1, pageSize = 50, keyword = "__comp010_no_such_text__" } }), overall.Token));
                string? probe = kind == "resource"
                    ? FirstString(options, "resource", "key") : FirstRowString(p1, "allianceName", "alliance_name", "alliance");
                JsonElement? filtered = null;
                if (probe is not null)
                    filtered = J(await owner.Map317.InvokeAsync("map_search", J(kind == "resource"
                        ? new { kind, query = new { serverId, page = 1, pageSize = 50, resourceNameKey = probe } }
                        : (object)new { kind, query = new { serverId, page = 1, pageSize = 50, alliance = probe } }), overall.Token));
                int? summaryCount = summary.TryGetProperty("counts", out var counts) ? Int(counts, kind) : null;
                Record(kind + "-query-surface", new
                {
                    total, page1Rows = rows1, page2Rows = rows2, summaryCount,
                    missTotal = Int(miss, "total"), filterProbe = probe, filteredTotal = filtered is null ? (int?)null : Int(filtered.Value, "total"),
                    optionsTopLevelKeys = options.ValueKind == JsonValueKind.Object ? options.EnumerateObject().Select(x => x.Name).ToArray() : [],
                    firstRowKeys = FirstRowKeys(p1),
                });
                bool completed = String(last, "status") == "completed" || !Bool(last, "isReading");
                object? export = null;
                if (kind == "city")
                {
                    cityRunId = run;
                    string xlsx = Path.Combine(root, "city-export.xlsx");
                    try
                    {
                        JsonElement exportPayload = J(new
                        {
                            query = new { serverId, page = 1, pageSize = 50 },
                            headers = new[] { "Name", "Alliance", "Level", "Power", "X", "Y", "Shield", "Updated" },
                        });
                        var request = owner.Map317.PrepareCityExport(exportPayload);
                        object? written = owner.Map317.WriteCityExport(request, xlsx);
                        int sheetRows = -1;
                        using (var zip = ZipFile.OpenRead(xlsx))
                        {
                            var sheet = zip.Entries.FirstOrDefault(e => e.FullName.StartsWith("xl/worksheets/sheet", StringComparison.Ordinal));
                            if (sheet is not null)
                            {
                                using var reader = new StreamReader(sheet.Open());
                                string xml = reader.ReadToEnd();
                                sheetRows = System.Text.RegularExpressions.Regex.Matches(xml, "<row ").Count;
                            }
                        }
                        export = new { path = xlsx, bytes = new FileInfo(xlsx).Length, sheetRows, written = J(written) };
                    }
                    catch (Exception e) { export = new { error = e.GetType().Name, (e as BridgeCommandException)?.Code, e.Message }; }
                    Record("city-export", export);
                }
                runs.Add(new { kind, run, completed, total, page1Rows = rows1, page2Rows = rows2, summaryCount, export });
            }

            // Reopen and profile isolation.
            using (var reopened = new MapStore(paths.MapDatabasePath(profile)))
            {
                var reopenTotals = new Dictionary<string, int>();
                foreach (string kind in new[] { "city", "resource" })
                    reopenTotals[kind] = reopened.Search(new MapQuery(kind, serverId)).Total;
                Record("sqlite-reopen", new { reopenTotals, cityRun = reopened.ReadScanRun(cityRunId!) });
            }
            string otherProfile = "lwb317-comp010-other-" + attemptId[..6];
            Directory.CreateDirectory(Path.GetDirectoryName(paths.MapDatabasePath(otherProfile))!);
            using (var other = new MapStore(paths.MapDatabasePath(otherProfile)))
                Record("profile-isolation", new { otherProfile, cityTotal = other.Search(new MapQuery("city", serverId)).Total,
                    resourceTotal = other.Search(new MapQuery("resource", serverId)).Total });

            // Optional positive-stage Stop with the distinguishing plan: only when the
            // observation timeline proved durable staging precedes terminal completion.
            if (positiveStageAttempt)
            {
                if (!stagingPositiveBeforeTerminal)
                    Record("positive-stage-skipped", new { reason = "durable staging was never observed while reading; a Stop could not cut staged rows" });
                else
                {
                    JsonElement start = J(await owner.Map317.InvokeAsync("map_scan_start",
                        J(new { profileId = profile, scanMode = "normal", selectedTypes = new[] { "resource" } }), overall.Token));
                    string run = String(start, "scanRunId")!;
                    int staged = 0; DateTimeOffset until = DateTimeOffset.UtcNow.AddMinutes(8);
                    while (DateTimeOffset.UtcNow < until && Bool(J(owner.Map317.CreateStatus()), "isReading"))
                    {
                        staged = StagingCount(paths.MapDatabasePath(profile), run, "resource");
                        if (staged > 0) break;
                        await Task.Delay(150, overall.Token);
                    }
                    JsonElement stopped = staged > 0 ? J(await owner.Map317.InvokeAsync("map_scan_stop", empty, overall.Token)) : default;
                    using var reopened = new MapStore(paths.MapDatabasePath(profile));
                    var runRow = reopened.ReadScanRun(run);
                    Record("positive-stage-stop", new { run, stagedBeforeStop = staged, stopped, runRow,
                        stagedAfterStop = StagingCount(paths.MapDatabasePath(profile), run, "resource"),
                        publishedForRun = reopened.Search(new MapQuery("resource", serverId, ScanRunId: run)).Total });
                    if (staged <= 0) await owner.Map317.InvokeAsync("map_scan_stop", empty, CancellationToken.None);
                }
            }
            report["runs"] = runs;
            report["terminal"] = "PILOT_COMPLETED";
            }
            finally
            {
                // Owned exact Stop through the production Home boundary, even after a failure.
                try
                {
                    if (owner.Map317.IsScanActive)
                    {
                        object? stoppedScan = await owner.Map317.InvokeAsync("map_scan_stop", empty, CancellationToken.None);
                        Record("finally-owned-map-stop", J(stoppedScan));
                    }
                }
                catch (Exception e) { Record("finally-map-stop-failed", new { e.GetType().Name, e.Message }); }
                JsonElement status = J(owner.OverviewLifecycle.CreateProfileInstanceStatus());
                if (instance is not null && String(status, "instanceId") == instance && Int(status, "pid") == ownedPid)
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
                else Record("no-owned-session-to-stop", new { instance, ownedPid, status });
            }
        }
        catch (Exception ex)
        {
            report["failure"] = new { type = ex.GetType().Name, code = (ex as BridgeCommandException)?.Code, ex.Message };
            report["terminal"] = "PILOT_FAILED";
            Record("failure", report["failure"]);
        }
        finally
        {
            report["runs"] ??= runs;
            report["recoveryJournalAfter"] = File.Exists(Path.Combine(paths.OverviewRuntimeRoot, "recovery.json"));
            report["installedAfter"] = Hashes();
            report["originalHashesRestored"] = OriginalMatches((Dictionary<string, string?>)report["installedAfter"]!);
            report["ownedGameStopSucceeded"] = stopSucceeded;
            report["finishedAtUtc"] = DateTimeOffset.UtcNow;
            Record("final-restoration-inventory", new
            {
                originalHashesRestored = report["originalHashesRestored"], ownedGameStopSucceeded = stopSucceeded,
                activeProcesses = Process.GetProcesses()
                    .Where(p => p.ProcessName is "LastWar" or "LastWarLauncher")
                    .Select(p => new { p.Id, p.ProcessName }).ToArray(),
            });
        }
    }

    private static int StagingCount(string db, string run, string kind)
    {
        try
        {
            var b = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
            { DataSource = db, Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadOnly, Pooling = false };
            using var c = new Microsoft.Data.Sqlite.SqliteConnection(b.ToString());
            c.DefaultTimeout = 2; c.Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT count(*) FROM scan_records WHERE run_id=$r AND kind=$k";
            cmd.Parameters.AddWithValue("$r", run); cmd.Parameters.AddWithValue("$k", kind);
            return checked((int)(long)(cmd.ExecuteScalar() ?? 0L));
        }
        catch { return 0; }
    }

    private static string? FirstString(JsonElement value, string propertyHint, string key)
    {
        if (value.ValueKind == JsonValueKind.Object)
            foreach (var p in value.EnumerateObject())
            {
                if (p.Name.Contains(propertyHint, StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind == JsonValueKind.Array)
                    foreach (var item in p.Value.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty(key, out var k) && k.ValueKind == JsonValueKind.String)
                            return k.GetString();
                        if (item.ValueKind == JsonValueKind.String) return item.GetString();
                    }
                var nested = FirstString(p.Value, propertyHint, key);
                if (nested is not null) return nested;
            }
        return null;
    }

    private static string? FirstRowString(JsonElement search, params string[] names)
    {
        if (!search.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Array) return null;
        foreach (var row in rows.EnumerateArray())
            foreach (string name in names)
                if (row.ValueKind == JsonValueKind.Object && row.TryGetProperty(name, out var v) &&
                    v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString()))
                    return v.GetString();
        return null;
    }

    private static string[] FirstRowKeys(JsonElement search) =>
        search.TryGetProperty("rows", out var rows) && rows.ValueKind == JsonValueKind.Array && rows.GetArrayLength() > 0 &&
        rows[0].ValueKind == JsonValueKind.Object ? rows[0].EnumerateObject().Select(p => p.Name).ToArray() : [];

    private static string HashFile(string path)
    {
        using var s = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(s)).ToLowerInvariant();
    }
    private static JsonElement J(object? value) => JsonSerializer.SerializeToElement(value, JsonOptions.Default);
    private static string? String(JsonElement v, string p) => v.ValueKind == JsonValueKind.Object && v.TryGetProperty(p, out var x) && x.ValueKind == JsonValueKind.String ? x.GetString() : null;
    private static int? Int(JsonElement v, string p) => v.ValueKind == JsonValueKind.Object && v.TryGetProperty(p, out var x) && x.ValueKind == JsonValueKind.Number && x.TryGetInt32(out int n) ? n : null;
    private static bool Bool(JsonElement v, string p) => v.ValueKind == JsonValueKind.Object && v.TryGetProperty(p, out var x) && x.ValueKind == JsonValueKind.True;
    private static void Assert(bool value, string reason) { if (!value) throw new InvalidDataException("COMPLETION010_PILOT: " + reason); }
}
