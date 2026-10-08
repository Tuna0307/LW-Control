using System.Security.Cryptography;
using System.Text.Json;
using LWBridge.Desktop;
using Microsoft.Data.Sqlite;
using Map317 = LWBridge.Map317;

namespace LWBridge.Desktop.Checks;

/// <summary>
/// Production map command dispatcher replay over a UNIQUE WRITABLE COPY of a
/// SQLite read-only backup of the accepted, actual v22 captured Resource run.
/// Game/world/action provider is EXPLICIT INERT: there are NO scans/game actions.
/// </summary>
internal static class Campaign007RealResourceCommandChecks
{
    private const int Server = 2212;
    private const int ExpectedRows = 8008;
    private const string ExpectedRun = "b439bfa57cd54405a14e803d21964c9d";
    private static JsonElement J(object? value) =>
        JsonSerializer.SerializeToElement(value, JsonOptions.Default);
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException("CAMPAIGN007: " + message);
    }
    private static string[] Keys(JsonElement response) =>
        response.GetProperty("rows").EnumerateArray()
            .Select(row => row.GetProperty("recordKey").GetString() ?? string.Empty)
            .ToArray();

    private static async Task<JsonElement> Command(Map317CommandService service,
        string name, object payload) =>
        J(await service.InvokeAsync(name, J(payload), CancellationToken.None));

    private static async Task<JsonElement> Query(Map317CommandService service, object query) =>
        await Command(service, "map_search", new { kind = "resource", query });

    private static Map317CommandService Open(string database) =>
        new(database, Map317.UnavailableMapProvider.Instance,
            Map317.UnavailableMapActionProvider.Instance, startPlunderWorkers: false);

    internal static async Task RunAsync(string backupReport, string reportPath)
    {
        using var metadata = JsonDocument.Parse(File.ReadAllText(backupReport));
        string snapshot = metadata.RootElement.GetProperty("snapshotPath").GetString()!;
        Require(File.Exists(snapshot), "SQLite snapshot absent");
        string initialSha = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(snapshot))).ToLowerInvariant();
        Require(initialSha == metadata.RootElement.GetProperty("snapshotSha256").GetString(),
            "snapshot hash mismatched safe original");
        string root = Path.Combine(Path.GetTempPath(), "LWB317-A-TO-A-CAMPAIGN-007-COMMAND-" +
            Guid.NewGuid().ToString("N"));
        string a = Path.Combine(root, "profiles", "A", "map-data", "map-data.db");
        string b = Path.Combine(root, "profiles", "B", "map-data", "map-data.db");
        string expected = Path.Combine(root, "readonly-comparator", "map-data.db");
        Directory.CreateDirectory(Path.GetDirectoryName(a)!);
        Directory.CreateDirectory(Path.GetDirectoryName(b)!);
        Directory.CreateDirectory(Path.GetDirectoryName(expected)!);
        File.Copy(snapshot, a);
        File.Copy(snapshot, expected);
        string? exception = null;
        object? outcome = null;
        bool allClosed = false;
        try
        {
            using var comparator = new Map317.MapStore(expected);
            Map317.MapScanRun run = comparator.ReadScanRun(ExpectedRun)
                ?? throw new InvalidDataException("actual completed run missing");
            Require(run.Status == "completed" && run.CompletedBlocks == run.TotalBlocks &&
                run.TotalBlocks == 2500 && run.FailedBlocks == 0,
                "real completed run gate");
            int expectedCount = comparator.Search(new Map317.MapQuery("resource", Server)).Total;
            Require(expectedCount == ExpectedRows, "baseline changed");

            JsonElement first, second, filtered, impossible, optionsA, offlineSummary, defaultPage;
            string key;
            string[] keys1,keys2;
            using (var serviceA = Open(a))
            {
                try
                {
                    _ = await Command(serviceA, "map_search",
                        new { kind = "resource", serverId = Server, page = 1, pageSize = 50 });
                    throw new InvalidDataException("old malformed envelope was accepted");
                }
                catch (BridgeCommandException e) when (e.Code == "INVALID_MAP_QUERY") { }
                first = await Query(serviceA, new { serverId = Server, page = 1, pageSize = 50 });
                second = await Query(serviceA, new { serverId = Server, page = 2, pageSize = 50 });
                defaultPage = await Query(serviceA, new { serverId = Server });
                Require(first.GetProperty("total").GetInt32() == ExpectedRows &&
                    second.GetProperty("total").GetInt32() == ExpectedRows &&
                    defaultPage.GetProperty("total").GetInt32() == ExpectedRows,
                    "positive native command must return real row count");
                keys1 = Keys(first);
                keys2 = Keys(second);
                Require(keys1.Length == 50 && keys2.Length == 50 &&
                    keys1.Distinct().Count() == 50 && keys2.Distinct().Count() == 50 &&
                    !keys1.Intersect(keys2).Any(), "native commands pages must not overlap");
                Require(Keys(defaultPage).SequenceEqual(keys1), "missing page/pageSize default to original 1/50");
                Require(keys1.SequenceEqual(comparator.Search(new Map317.MapQuery("resource", Server,Page:1)).Rows
                    .Select(x => x.GetProperty("recordKey").GetString() ?? "")),
                    "native result first page does not match independent store query");
                Require(keys2.SequenceEqual(comparator.Search(new Map317.MapQuery("resource", Server,Page:2)).Rows
                    .Select(x => x.GetProperty("recordKey").GetString() ?? "")),
                    "native result second page does not match independent store query");

                key = first.GetProperty("rows")[0].GetProperty("resourceNameKey").GetString()!;
                Require(!string.IsNullOrWhiteSpace(key), "positive native filter key absent");
                filtered = await Query(serviceA, new { serverId = Server, page = 1,
                    pageSize = 50, resourceNameKey = key });
                impossible = await Query(serviceA, new { serverId = Server, page = 1,
                    pageSize = 50, resourceNameKey = "__campaign007_no_such_resource_key__" });
                int expectedFilterCount = comparator.Search(new Map317.MapQuery("resource",
                    Server, ResourceNameKey: key)).Total;
                Require(filtered.GetProperty("total").GetInt32() == expectedFilterCount &&
                    expectedFilterCount > 0 && expectedFilterCount < ExpectedRows,
                    "positive name-key filter must match persisted real keys");
                Require(impossible.GetProperty("total").GetInt32() == 0 &&
                    Keys(impossible).Length == 0, "impossible native filter must be empty");
                optionsA = await Command(serviceA, "map_data_options", new { serverId = Server });
                offlineSummary = await Command(serviceA, "map_summary", new { });
                Require(optionsA.GetProperty("counts").GetProperty("resource").GetInt32() == ExpectedRows &&
                    optionsA.GetProperty("names").GetProperty("resource").GetArrayLength() > 0,
                    "options command should enumerate real published Resource names and count");
                Require(offlineSummary.GetProperty("serverId").GetInt32() == 0,
                    "offline no-live-context summary must not pretend to have server 2212");
            }
            // Explicitly separate a never-populated profile B.
            JsonElement emptyB;
            using (var serviceB = Open(b))
            {
                emptyB = await Query(serviceB, new { serverId = Server, page = 1, pageSize = 50 });
                Require(emptyB.GetProperty("total").GetInt32() == 0 && Keys(emptyB).Length == 0,
                    "profile B illegally inherited A published data");
                JsonElement optionsB = await Command(serviceB, "map_data_options", new { serverId = Server });
                Require(!optionsB.GetRawText().Contains("\"8008\"", StringComparison.Ordinal),
                    "profile B options inherited A total");
            }
            JsonElement reopened;
            using (var reopenedA = Open(a))
            {
                reopened = await Query(reopenedA, new { serverId = Server, page = 1, pageSize = 50 });
                Require(reopened.GetProperty("total").GetInt32() == ExpectedRows &&
                    Keys(reopened).SequenceEqual(keys1), "profile A did not persist after A->B->A reopen");
            }
            string snapshotShaAfter = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(snapshot))).ToLowerInvariant();
            Require(snapshotShaAfter == initialSha, "original safe snapshot unexpectedly changed");
            string transportPayloadPath = Path.ChangeExtension(reportPath, ".payload.json");
            File.WriteAllText(transportPayloadPath,
                JsonSerializer.Serialize(new
                {
                    proofType = "REAL_V22_CAPTURED_ROWS_CURRENT_PRODUCTION_MAP_COMMAND_REPLAY_NO_LIVE_GAME",
                    profileA = new
                    {
                        firstPage = first, secondPage = second, positiveResourceFilter = filtered,
                        impossibleResourceFilter = impossible, options = optionsA,
                        offlineSummary
                    },
                    profileB = new { firstPage = emptyB },
                    actualCommand = "Map317CommandService.InvokeAsync"
                }, new JsonSerializerOptions { WriteIndented = true }));
            outcome = new {
                workItem = "LWB317-FUNCTION-HOME-MAP-A-TO-A-CAMPAIGN-007",
                proofType = "real captured v22 records -> actual Map317CommandService.InvokeAsync -> actual MapControlPlane + MapStore, inert external game/action provider",
                originalReferenceParity = "UNKNOWN_NOT_PROVEN",
                realSourceRunId = ExpectedRun, snapshotSha256BeforeAfter = initialSha,
                serverId = Server,
                profileA = new {
                    total = first.GetProperty("total").GetInt32(),
                    page1Count=keys1.Length, page2Count=keys2.Length,
                    firstRecordKey=keys1[0], pagesNonoverlap = true, defaults50 = true,
                    comparatorExactPageMatches = true, filteredResourceNameKey = key,
                    positiveFilterCount=filtered.GetProperty("total").GetInt32(),
                    impossibleFilterCount=impossible.GetProperty("total").GetInt32(),
                    optionsResourceNames = optionsA.GetProperty("names").GetProperty("resource").GetArrayLength(),
                    optionResourceCount=optionsA.GetProperty("counts").GetProperty("resource").GetInt32(),
                    summaryOfflineServerId=offlineSummary.GetProperty("serverId").GetInt32(),
                    reopenTotal=reopened.GetProperty("total").GetInt32()
                },
                oldEnvelopeRejected = true,
                profileBTotal=emptyB.GetProperty("total").GetInt32(),
                schedulesDisabled = true, externalGameActions = 0
            };
            Console.WriteLine("CAMPAIGN007_REAL_COMMAND_POSITIVE_PASS 8008; malformed rejected, page2 and filter positive, A/B/A preserved");
        }
        catch (Exception error)
        {
            exception = error.ToString();
            throw;
        }
        finally
        {
            // Retire SQLite pool handles before removing this exclusively owned temp tree.
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(root, recursive:true); allClosed = !Directory.Exists(root); }
            catch (IOException) { allClosed = false; }
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
            File.WriteAllText(reportPath, JsonSerializer.Serialize(new {
                result = outcome, failure = exception, ownedRoot = root,
                sourceSnapshot = snapshot, sourceSnapshotPreserved = File.Exists(snapshot),
                ownedTempCleanupSucceeded = allClosed,
                cleanupPolicy = "clear SQLite connection pools and delete only newly allocated temp replay root"
            },new JsonSerializerOptions {WriteIndented=true}));
            Console.WriteLine("CAMPAIGN007_COMMAND_OWNED_TEMP_CLEANUP=" + allClosed);
            if (!allClosed && exception is null) throw new IOException("Campaign007 temp root not cleaned");
        }
    }
}
