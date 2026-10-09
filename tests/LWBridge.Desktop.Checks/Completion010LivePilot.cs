using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
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
        // The preflight backs up this exact root. Never fabricate a second root
        // without reversible backups or silently operate on owner/default data.
        JsonElement pre = JsonDocument.Parse(File.ReadAllText(preflightJson)).RootElement.Clone();
        string profile = "lwb317-comp010-pilot-" + attemptId[..8];
        string root = Path.GetFullPath(pre.GetProperty("isolatedRoot").GetString()!);
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
        Assert(pre.GetProperty("allowLiveHomeLaunch").GetBoolean(), "preflight must allow a live launch");
        var files = new Dictionary<string, (string Path, string Sha)>();
        foreach (var item in pre.GetProperty("triplet").EnumerateObject())
            files[item.Name] = (item.Value.GetProperty("path").GetString()!, item.Value.GetProperty("sha256").GetString()!);
        Dictionary<string, string?> Hashes() => files.ToDictionary(
            p => p.Key, p => File.Exists(p.Value.Path) ? HashFile(p.Value.Path) : null);
        bool OriginalMatches(Dictionary<string, string?> h) => files.All(
            p => string.Equals(h[p.Key], p.Value.Sha, StringComparison.OrdinalIgnoreCase));

        string? instance = null; int? ownedPid = null;
        DateTime? ownedProcessCreationUtc = null;
        bool stopSucceeded = false, exactExit = false, preflightVerified = false;
        bool mapStopSucceeded = true, requestedStageStopVerified = !positiveStageAttempt;
        bool requiredRunsPositive = false, rootRemoved = false;
        var runs = new List<object>();
        var runProofs = new Dictionary<string, Completion010PilotAssertions.RunProof>(StringComparer.Ordinal);
        try
        {
            report["installedBefore"] = Hashes();
            Assert(OriginalMatches((Dictionary<string, string?>)report["installedBefore"]!), "installed originals must match the preflight");
            Assert(Directory.Exists(root) && Path.GetRelativePath(Path.GetTempPath(), root) is { } relative &&
                !relative.StartsWith("..", StringComparison.Ordinal) &&
                Path.GetFileName(root).StartsWith("LWB317-", StringComparison.OrdinalIgnoreCase),
                "preflight must provide its own temporary isolated root");
            Assert(pre.GetProperty("existingIsolatedFilesBeforeGate").GetArrayLength() == 0,
                "preflight root must have been fresh before backing up");
            foreach (var item in pre.GetProperty("verifiedReversibleBackups").EnumerateObject())
            {
                string path = Path.GetFullPath(item.Value.GetProperty("path").GetString()!);
                Assert(Path.GetRelativePath(root, path) is { } relativeBackup &&
                    !relativeBackup.StartsWith("..", StringComparison.Ordinal) &&
                    File.Exists(path) && files.TryGetValue(item.Name, out var expected) &&
                    string.Equals(HashFile(path), expected.Sha, StringComparison.OrdinalIgnoreCase),
                    "reversible backup must match the actual execution root and original file: " + item.Name);
            }
            Assert(pre.GetProperty("verifiedReversibleBackups").EnumerateObject().Count() == 3,
                "three reversible backups required");
            preflightVerified = true;
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
                {
                Assert(string.Equals(Path.GetFullPath(process.MainModule?.FileName ?? ""),
                    Path.GetFullPath(Path.Combine(GameRoot, "Game", "LastWar.exe")), StringComparison.OrdinalIgnoreCase),
                    "owned process is the exact installed LastWar.exe");
                ownedProcessCreationUtc = process.StartTime.ToUniversalTime();
            }
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

            string? stagedKindForStop = null;
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
                    if (staged > 0 && Bool(state, "isReading"))
                    { stagedPositive = true; stagedKindForStop ??= kind; }
                    if (timeline.Count == 0 || sw.ElapsedMilliseconds / 5000 > (timeline.Count - 1))
                        timeline.Add(new { tSec = sw.Elapsed.TotalSeconds, completed = Int(state, "completedBlocks"),
                            inflight = Int(state, "inflightBlocks"), failed = Int(state, "failedBlocks"), staged });
                    if (!Bool(state, "isReading")) break;
                    if (sw.Elapsed > TimeSpan.FromMinutes(9))
                    {
                        Record("run-timed-out-stopping", new { kind, run });
                        await owner.Map317.InvokeAsync("map_scan_stop", empty, overall.Token);
                        throw new TimeoutException(kind + " scan timed out before durable completion");
                    }
                    await Task.Delay(400, overall.Token);
                }
                JsonElement last = J(owner.Map317.CreateStatus());
                Record(kind + "-terminal", new { run, seconds = sw.Elapsed.TotalSeconds, last, stagedPositiveWhileReading = stagedPositive, timeline });

                // Query surface through the production command service.
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
                Assert(Int(summary, "serverId") == serverId && summaryCount is not null,
                    kind + " summary must resolve the active server and kind");
                Assert(options.ValueKind == JsonValueKind.Object && options.EnumerateObject().Any(),
                    kind + " native options must be present");
                Record(kind + "-query-surface", new
                {
                    total, page1Rows = rows1, page2Rows = rows2, summaryCount,
                    missTotal = Int(miss, "total"), filterProbe = probe, filteredTotal = filtered is null ? (int?)null : Int(filtered.Value, "total"),
                    optionsTopLevelKeys = options.ValueKind == JsonValueKind.Object ? options.EnumerateObject().Select(x => x.Name).ToArray() : [],
                    firstRowKeys = FirstRowKeys(p1),
                });
                using var terminalStore = new MapStore(paths.MapDatabasePath(profile));
                string? durableStatus = terminalStore.ReadScanRun(run)?.Status;
                string terminalClass = Completion010PilotAssertions.TerminalClass(durableStatus, timedOut: false);
                bool completed = terminalClass == "completed" && !Bool(last, "isReading");
                Record(kind + "-durable-terminal", new { durableStatus, terminalClass, completed });
                Assert(completed, kind + " requires durable completed (not cancelled, failed or idle)");
                object? export = null;
                int exportedCount = -1, workbookRows = -1;
                string[] workbookHeaders = [];
                bool workbookContentsValid = false;
                if (kind == "city")
                {
                    cityRunId = run;
                    string xlsx = Path.Combine(root, "city-export.xlsx");
                    try
                    {
                        JsonElement exportPayload = J(new
                        {
                            query = new { serverId, page = 1, pageSize = 50 },
                            headers = Completion010PilotAssertions.CityHeaders,
                        });
                        var request = owner.Map317.PrepareCityExport(exportPayload);
                        object? written = owner.Map317.WriteCityExport(request, xlsx);
                        exportedCount = Int(J(written), "rowCount") ?? -1;
                        using (var zip = ZipFile.OpenRead(xlsx))
                        {
                            var sheet = zip.GetEntry("xl/worksheets/sheet1.xml") ??
                                throw new InvalidDataException("City workbook worksheet missing");
                            using var stream = sheet.Open();
                            XDocument xml = XDocument.Load(stream);
                            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                            var sheetRows = xml.Descendants(ns + "sheetData").Elements(ns + "row").ToArray();
                            workbookRows = sheetRows.Length;
                            workbookHeaders = sheetRows[0].Elements(ns + "c")
                                .Select(c => string.Concat(c.Descendants(ns + "t").Select(t => t.Value))).ToArray();
                            var expectedRows = new List<JsonElement>();
                            using (var snapshot = new MapStore(paths.MapDatabasePath(profile)))
                            {
                                for (int page = 1; page <= 1000; page++)
                                {
                                    LWBridge.Map317.MapSearchResult slice = snapshot.Search(
                                        new MapQuery("city", serverId, Page: page, PageSize: 200),
                                        cityExportRows: true);
                                    expectedRows.AddRange(slice.Rows);
                                    if (slice.Rows.Count == 0 || expectedRows.Count >= slice.Total) break;
                                }
                            }
                            workbookContentsValid = expectedRows.Count == sheetRows.Length - 1 &&
                                sheetRows.Skip(1).Select((row, i) =>
                                    MatchCityCells(row, expectedRows[i], ns)).All(ok => ok);
                        }
                        export = new { path = xlsx, bytes = new FileInfo(xlsx).Length,
                            workbookRows, workbookHeaders, workbookContentsValid, exportedCount };
                    }
                    catch (Exception e)
                    {
                        Record("city-export-error", new { e.GetType().Name, (e as BridgeCommandException)?.Code, e.Message });
                        throw;
                    }
                    Record("city-export", export);
                }
                runProofs[kind] = new Completion010PilotAssertions.RunProof(
                    kind, durableStatus, false, total, rows1, rows2, summaryCount ?? -1,
                    Int(miss, "total") ?? -1, filtered is null ? null : Int(filtered.Value, "total"),
                    probe is not null, PageKeys(p1), PageKeys(p2), -1, exportedCount,
                    workbookRows, workbookHeaders, workbookContentsValid);
                runs.Add(new { kind, run, durableStatus, completed, total, page1Rows = rows1,
                    page2Rows = rows2, summaryCount, export });
            }

            // Reopen and profile isolation.
            using (var reopened = new MapStore(paths.MapDatabasePath(profile)))
            {
                var reopenTotals = new Dictionary<string, int>();
                foreach (string kind in new[] { "city", "resource" })
                    reopenTotals[kind] = reopened.Search(new MapQuery(kind, serverId)).Total;
                Record("sqlite-reopen", new { reopenTotals, cityRun = reopened.ReadScanRun(cityRunId!) });
                foreach (string kind in new[] { "city", "resource" })
                {
                    var proof = runProofs[kind] with { ReopenedTotal = reopenTotals[kind] };
                    runProofs[kind] = proof;
                    string[] errors = Completion010PilotAssertions.RunErrors(proof).ToArray();
                    Record(kind + "-proof-gate", new { errors, proof });
                    Assert(errors.Length == 0, kind + " proof failed: " + string.Join("; ", errors));
                }
                requiredRunsPositive = true;
            }
            string otherProfile = "lwb317-comp010-other-" + attemptId[..6];
            Directory.CreateDirectory(Path.GetDirectoryName(paths.MapDatabasePath(otherProfile))!);
            using (var other = new MapStore(paths.MapDatabasePath(otherProfile)))
            {
                int cityTotal = other.Search(new MapQuery("city", serverId)).Total;
                int resourceTotal = other.Search(new MapQuery("resource", serverId)).Total;
                Record("profile-isolation", new { otherProfile, cityTotal, resourceTotal });
                Assert(cityTotal == 0 && resourceTotal == 0, "foreign profile must have no pilot rows");
            }

            // Optional positive-stage Stop with the distinguishing plan: only when the
            // observation timeline proved durable staging precedes terminal completion.
            if (positiveStageAttempt)
            {
                if (stagedKindForStop is null)
                {
                    requestedStageStopVerified = false;
                    Record("positive-stage-pending", new { reason = "neither kind had a naturally observed staging window" });
                }
                else
                {
                    JsonElement start = J(await owner.Map317.InvokeAsync("map_scan_start",
                        J(new { profileId = profile, scanMode = "normal", selectedTypes = new[] { stagedKindForStop! } }), overall.Token));
                    string run = String(start, "scanRunId")!;
                    int staged = 0; DateTimeOffset until = DateTimeOffset.UtcNow.AddMinutes(8);
                    while (DateTimeOffset.UtcNow < until && Bool(J(owner.Map317.CreateStatus()), "isReading"))
                    {
                        staged = StagingCount(paths.MapDatabasePath(profile), run, stagedKindForStop!);
                        if (staged > 0) break;
                        await Task.Delay(150, overall.Token);
                    }
                    JsonElement stopped = staged > 0 ? J(await owner.Map317.InvokeAsync("map_scan_stop", empty, overall.Token)) : default;
                    using var reopened = new MapStore(paths.MapDatabasePath(profile));
                    var runRow = reopened.ReadScanRun(run);
                    int remainingStaged = StagingCount(paths.MapDatabasePath(profile), run, stagedKindForStop!);
                    int publishedForRun = reopened.Search(new MapQuery(stagedKindForStop!, serverId, ScanRunId: run)).Total;
                    requestedStageStopVerified = staged > 0 && !Bool(stopped, "isReading") &&
                        runRow?.Status == "cancelled" && remainingStaged == 0 && publishedForRun == 0;
                    Record("positive-stage-stop", new { kind = stagedKindForStop, run, stagedBeforeStop = staged,
                        stopped, runRow, remainingStaged, publishedForRun, requestedStageStopVerified });
                    if (staged <= 0 && owner.Map317.IsScanActive)
                        await owner.Map317.InvokeAsync("map_scan_stop", empty, CancellationToken.None);
                }
            }
            report["runs"] = runs;
            report["terminal"] = "PILOT_PROOFS_PASSED_PENDING_STOP_RESTORATION";
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
                catch (Exception e)
                {
                    mapStopSucceeded = false;
                    Record("finally-map-stop-failed", new { e.GetType().Name, e.Message });
                }
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
            if (ownedPid is { } capturedPid)
            {
                try
                {
                    using Process process = Process.GetProcessById(capturedPid);
                    // PID reuse is not evidence that the original owned process survived.
                    exactExit = ownedProcessCreationUtc.HasValue &&
                        process.StartTime.ToUniversalTime() != ownedProcessCreationUtc.Value;
                }
                catch (ArgumentException) { exactExit = true; }
            }
            var processes = Process.GetProcesses()
                .Where(p => p.ProcessName is "LastWar" or "LastWarLauncher")
                .Select(p => new { p.Id, p.ProcessName }).ToArray();
            bool journalAbsent = !File.Exists(Path.Combine(paths.OverviewRuntimeRoot, "recovery.json"));
            report["recoveryJournalAfter"] = !journalAbsent;
            report["installedAfter"] = Hashes();
            bool restored = OriginalMatches((Dictionary<string, string?>)report["installedAfter"]!);
            report["originalHashesRestored"] = restored;
            report["ownedGameStopSucceeded"] = stopSucceeded;
            report["confirmedExactProcessExit"] = exactExit;
            report["mapStopSucceeded"] = mapStopSucceeded;
            report["requiredRunsPositive"] = requiredRunsPositive;
            report["requestedPositiveStopVerified"] = positiveStageAttempt ? requestedStageStopVerified : null;
            Record("final-restoration-inventory", new
            {
                originalHashesRestored = restored, ownedGameStopSucceeded = stopSucceeded,
                confirmedExactProcessExit = exactExit, mapStopSucceeded, journalAbsent, processes,
            });

            // The preflight root was empty before its three backed-up files. Only
            // clean that proven task-owned root after exit and restoration; otherwise
            // preserve the backup and recovery journal for manual recovery.
            if (preflightVerified && exactExit && restored && journalAbsent && processes.Length == 0)
            {
                try
                {
                    Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                    Directory.Delete(root, recursive: true);
                    rootRemoved = !Directory.Exists(root);
                }
                catch (Exception error)
                {
                    Record("task-owned-cleanup-failed", new { error.GetType().Name, error.Message });
                }
            }
            var proof = new Completion010PilotAssertions.FinalProof(
                preflightVerified, instance is not null && ownedPid.HasValue,
                stopSucceeded && mapStopSucceeded, exactExit, restored, journalAbsent,
                rootRemoved, processes.Length == 0, requiredRunsPositive,
                positiveStageAttempt ? requestedStageStopVerified : null);
            var errors = Completion010PilotAssertions.FinalErrors(proof).ToList();
            if (report.ContainsKey("failure")) errors.Add("earlier pilot exception");
            report["finalProofErrors"] = errors;
            report["terminal"] = errors.Count == 0 ? "PILOT_COMPLETED" : "PILOT_FAILED";
            report["finishedAtUtc"] = DateTimeOffset.UtcNow;
            Record("final-proof-gate", new { proof, errors });
        }
        if (!string.Equals(report["terminal"] as string, "PILOT_COMPLETED", StringComparison.Ordinal))
            throw new InvalidDataException("COMPLETION010_PILOT: required witnesses or Stop/restoration/cleanup failed; see " + output);
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
        catch (Exception error)
        {
            throw new InvalidDataException(
                $"COMPLETION010_PILOT: failed to inspect SQLite staging for {kind}/{run} at {db}", error);
        }
    }

    // Independent actual DB export-row -> XLSX cell comparison, all twelve
    // columns, including exact inline strings and Excel serial timestamps.
    // This rejects correctly sized but shifted/corrupted export workbooks.
    private static bool MatchCityCells(XElement worksheetRow, JsonElement source, XNamespace ns)
    {
        XElement[] cells = worksheetRow.Elements(ns + "c").ToArray();
        if (cells.Length != 12) return false;
        string? Value(int index) => cells[index].Element(ns + "v")?.Value;
        string Text(int index) =>
            string.Concat(cells[index].Descendants(ns + "t").Select(t => t.Value));
        bool Number(int index, string key)
        {
            if (!source.TryGetProperty(key, out JsonElement expected) ||
                expected.ValueKind != JsonValueKind.Number ||
                !expected.TryGetDouble(out double original) ||
                !double.IsFinite(original))
                return Value(index) is null;
            return NumericSame(Value(index), original);
        }
        bool String(int index, string key) =>
            Text(index) == (source.TryGetProperty(key, out JsonElement expected) &&
                expected.ValueKind == JsonValueKind.String ? expected.GetString() : "") &&
            cells[index].Attribute("t")?.Value == "inlineStr";
        bool Timestamp(int index, JsonElement? raw)
        {
            if (raw is not { ValueKind: JsonValueKind.Number } value ||
                !value.TryGetDouble(out double timestamp) ||
                !double.IsFinite(timestamp) || timestamp <= 0)
                return Value(index) is null;
            double ms = timestamp < 100_000_000_000d ? timestamp * 1_000d : timestamp;
            return NumericSame(Value(index), ms / 86_400_000d + 25_569d);
        }
        JsonElement? JsonValue(string name) =>
            source.TryGetProperty(name, out JsonElement value) ? value : null;
        JsonElement? protect =
            source.TryGetProperty("protectEndTime", out JsonElement primary)
                ? primary : JsonValue("shieldEndTime");
        bool marked = source.TryGetProperty("marked", out JsonElement flag) &&
            flag.ValueKind == JsonValueKind.True;
        return Number(0, "serverId") && Number(1, "x") && Number(2, "y") &&
            String(3, "ownerName") && String(4, "ownerUid") &&
            String(5, "uuid") && String(6, "allianceName") &&
            Number(7, "level") && Number(8, "health") &&
            Timestamp(9, protect) && Text(10) == (marked ? "Yes" : "No") &&
            Timestamp(11, JsonValue("updatedAt"));
    }

    private static bool NumericSame(string? cell, double expected) =>
        cell is not null &&
        double.TryParse(cell, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out double actual) &&
        double.IsFinite(actual) &&
        Math.Abs(actual - expected) <= 1e-8 * Math.Max(1.0, Math.Abs(expected));

    private static string[] PageKeys(JsonElement search)
    {
        if (!search.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Array)
            return [];
        return rows.EnumerateArray().Select(row =>
        {
            if (row.ValueKind != JsonValueKind.Object) return "";
            foreach (string key in new[] { "recordKey", "uuid", "ownerUid", "id" })
                if (row.TryGetProperty(key, out var property))
                    return property.ToString();
            return "";
        }).ToArray();
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
