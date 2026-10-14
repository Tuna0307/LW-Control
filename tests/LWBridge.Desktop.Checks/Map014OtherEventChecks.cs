using System.Text.Json;
using System.Text.Json.Nodes;
using LWBridge.Desktop;
using Microsoft.Data.Sqlite;
using Map317 = LWBridge.Map317;

namespace LWBridge.Desktop.Checks;

internal static class Map014OtherEventChecks
{
    private static void Require(bool assertion, string reason)
    {
        if (!assertion) throw new InvalidOperationException("MAP014_OTHER: " + reason);
    }

    private static MapStoredRecord Record(string key, int pointType, int treasureType, int suppliesType, int eventId = 0)
    {
        string cls = pointType == 27 ? "Client.WorldSuppliesPoint" : "Client.TreasurePointInfo";
        string data = JsonSerializer.Serialize(new
        {
            kind = "treasure", uuid = key, serverId = 2213, pointType, runtimeClass = cls,
            treasureType, suppliesType, eventId, x = 0, y = 999,
            workEndTime = pointType == 27 ? 1_800_000_000L : 0L,
            updatedAt = 1000L,
        });
        return new MapStoredRecord("treasure", 2213, key, 101, key,
            null, null, null, null, null, null, null, 1000, data);
    }

    internal static async Task RunAsync()
    {
        Require(Map317.MapScanStateMachine.CreateDefaultIdleState().SelectedTypes
                .SequenceEqual(Map317.MapKinds.DefaultSelection) &&
            Map317.MapKinds.DefaultSelection.Length == 8 &&
            Map317.MapKinds.All.Length == 9,
            "idle/default selection remains eight, while explicit Other is allowed");
        Require(MapScanContract.NormalizeStart(JsonSerializer.SerializeToElement(new { }))
                .SelectedTypes.SequenceEqual(MapScanContract.RecoveredDefaultTypes),
            "desktop implicit scan keeps eight original types");
        Require(MapAutoScanCommandService.NormalizeConfig(null).SelectedTypes!
                .SequenceEqual(MapAutoScanCommandService.DefaultSelectedTypes) &&
            !MapAutoScanCommandService.DefaultSelectedTypes.Contains("other"),
            "native Auto defaults must not silently select Other");
        Require(MapAutoScanCommandService.NormalizeConfig(MapAutoScanConfig.Default with
            { SelectedTypes = ["other", "other", "truck"] }).SelectedTypes!
                .SequenceEqual(["other", "truck"]),
            "explicit Other Auto survives normalization and removes duplicates");
        Require(Map317.MapKinds.IsValidOtherSubtype("supplies:9") &&
            !Map317.MapKinds.IsValidOtherSubtype("supplies:09") &&
            !Map317.MapKinds.IsValidOtherSubtype("supplies:0") &&
            !Map317.MapKinds.IsValidOtherSubtype("event:27015"),
            "Other numeric subtype is canonical and arbitrary event IDs are rejected");
        await AssertHostDefaultsAsync();

        MapStoredRecord ordinary = Record("ordinary", 21, 19, 0, 27015);
        MapStoredRecord party = Record("party", 21, 20, 0, 27000);
        MapStoredRecord ice = Record("ice", 27, 0, 1);
        MapStoredRecord desert = Record("desert", 27, 0, 3);
        MapStoredRecord unknown = Record("unknown-supplies", 27, 0, 9);
        MapScanBlockCapture sourceCapture = new(2213, 0, 0, "{\"recordsInBlock\":5}", [ordinary, party, ice, desert, unknown]);
        var fake = new FakeSource(sourceCapture);
        var wrapper = new MapOtherEventScanSource(fake);
        MapScanExecutionRequest both = new("run-other", 2213, 0, 20, 20,
            ["treasure", "other"], 8);
        MapScanTargetBlock seed = MapScanTraversal.Build(20, 20).Single();
        IReadOnlyList<MapScanBlockCapture> result = await wrapper.CaptureBatchAsync(
            both, seed, new HashSet<int> { 0 }, CancellationToken.None);
        Require(fake.Requests == 1 && fake.LastTypes!.SequenceEqual(["treasure"]),
            "Treasure plus Other must use exactly one native Treasure census");
        Require(result.Count == 1 && result[0].Records.Count == 5 &&
            JsonDocument.Parse(result[0].PayloadJson).RootElement.GetProperty("recordsInBlock").GetInt32() == 5,
            "native source supplies including unknown names survive, and metadata matches partitioned rows");
        Require(result[0].Records.Count(row => row.Kind == "treasure") == 1 &&
            result[0].Records.Single(row => row.Kind == "treasure").RecordKey == "ordinary",
            "27015 remains ordinary Treasure and supplies cannot enter Treasure claim lane");
        Require(result[0].Records.Count(row => row.Kind == "other") == 4,
            "Other has independent native identities for all source-confirmed supplies");
        foreach (MapStoredRecord row in result[0].Records.Where(row => row.Kind == "other"))
        {
            using JsonDocument data = JsonDocument.Parse(row.DataJson);
            Require(data.RootElement.GetProperty("kind").GetString() == "other" &&
                data.RootElement.GetProperty("otherSubtype").GetString() is { Length: > 0 },
                "native row category and numeric source subtype persisted together");
        }
        foreach (MapStoredRecord row in result[0].Records.Where(row => row.Kind == "other"))
        {
            using JsonDocument data = JsonDocument.Parse(row.DataJson);
            Require((row.RecordKey is "ice" or "party") ==
                (data.RootElement.TryGetProperty("otherNameKey", out var key) &&
                    key.ValueKind == JsonValueKind.String && key.GetString() is { Length: > 0 }),
                "only source-verified ice and event 27000 receive localized game name keys");
        }
        Require(MapOtherEventScanSource.ClassifySubtype(party.DataJson) == "event:27000" &&
            MapOtherEventScanSource.ClassifySubtype(ordinary.DataJson) is null &&
            MapOtherEventScanSource.ClassifySubtype(Record("negative", 21, 20, 0, 25098).DataJson) is null,
            "only verified detect_event 27000 belongs to Celebration Party");
        Require(MapOtherEventScanSource.ClassifySubtype(ice.DataJson) == "supplies:1" &&
            MapOtherEventScanSource.ClassifySubtype(desert.DataJson) == "supplies:3" &&
            MapOtherEventScanSource.ClassifySubtype(unknown.DataJson) == "supplies:9" &&
            MapOtherEventScanSource.NameKey("supplies:3") is null &&
            MapOtherEventScanSource.NameKey("supplies:9") is null,
            "all observed supplies types stay visible with unknown names marked unavailable");
        Require(MapOtherEventScanSource.ClassifySubtype(Record("non-qualifying-event",21,0,0,27000).DataJson) is null,
            "config event ID alone does not establish a capturable TreasurePointInfo");

        MapScanBlockCapture otherOnly = (await wrapper.CaptureBatchAsync(
            both with { SelectedTypes = ["other"] }, seed, new HashSet<int> { 0 },
            CancellationToken.None)).Single();
        Require(otherOnly.Records.Count == 4 && otherOnly.Records.All(row => row.Kind == "other") &&
            JsonDocument.Parse(otherOnly.PayloadJson).RootElement.GetProperty("recordsInBlock").GetInt32() == 4,
            "Other-only scans never publish Treasure or start a second native pass");
        MapScanBlockCapture treasureOnly = (await wrapper.CaptureBatchAsync(
            both with { SelectedTypes = ["treasure"] }, seed, new HashSet<int> { 0 },
            CancellationToken.None)).Single();
        Require(treasureOnly.Records.Count == 1 && treasureOnly.Records[0].RecordKey == "ordinary" &&
            JsonDocument.Parse(treasureOnly.PayloadJson).RootElement.GetProperty("recordsInBlock").GetInt32() == 1,
            "Treasure-only scans exclude the event and supplies");

        using var store = Map317.MapStore.CreateInMemory();
        foreach (MapStoredRecord row in result[0].Records)
            store.UpsertRecord(new Map317.MapRecord(row.Kind, row.ServerId, row.RecordKey, row.PointIndex,
                row.Uuid, row.Name, row.AllianceName, row.Level, row.Quality, row.Power, row.Distance,
                row.ShieldEndTime, row.UpdatedAt, row.DataJson));
        Require(store.SummaryCounts(2213)["other"] == 4 &&
            store.SummaryCounts(2213)["treasure"] == 1,
            "SQLite summary counts use independent Other and Treasure kinds");
        Require(store.Search(new Map317.MapQuery("other", 2213, OtherSubtype: "event:27000")).Total == 1 &&
            store.Search(new Map317.MapQuery("other", 2213, OtherSubtype: "supplies:1")).Total == 1 &&
            store.Search(new Map317.MapQuery("other", 2213, OtherSubtype: "supplies:9")).Total == 1,
            "Other subtype SQL filters are exact");
        Require(store.ReadOptions(2213).OtherTypes!.Any(option =>
            option.GetProperty("key").GetString() == "supplies:9" &&
            option.GetProperty("count").GetInt32() == 1),
            "unknown numeric supplies types remain available as subtype filters");
        store.ClearServer(2213);
        Require(store.SummaryCounts(2213)["other"] == 0 &&
            store.SummaryCounts(2213)["treasure"] == 0,
            "Clear removes both independently persisted categories");
        CheckLegacyMigrationAndDuplicate();
        await CheckLateCancelledScanPreservesPublicationAsync();
        await CheckAutoOtherStatusPersistenceAsync();
        await CheckOtherInspectionAndPreviewAsync();
        Console.WriteLine("MAP014_OTHER_EVENT_CHECKS_OK");
    }

    private static async Task CheckOtherInspectionAndPreviewAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "lwbridge-other-inspection-" + Guid.NewGuid().ToString("N"));
        string database = Path.Combine(root, "map-data.db");
        Directory.CreateDirectory(root);
        MapStoredRecord Inspectable(string key, int pointType, int suppliesType, int eventId = 0,
            Action<JsonObject>? mutate = null, bool legacy = false)
        {
            MapStoredRecord row = Record(key, pointType, pointType == 21 ? 20 : 0, suppliesType, eventId);
            JsonObject data = JsonNode.Parse(row.DataJson)!.AsObject();
            data["complete"] = true;
            data["expireTime"] = 0;
            if (!legacy)
            {
                data["kind"] = "other";
                data["otherSubtype"] = pointType == 27 ? $"supplies:{suppliesType}" : "event:27000";
            }
            mutate?.Invoke(data);
            return row with { Kind = legacy ? "treasure" : "other", DataJson = data.ToJsonString() };
        }
        try
        {
            using (var seed = new Map317.MapStore(database))
            {
                // Reopen exercises the actual legacy-to-Other migration too.
                seed.UpsertRecord(ToMapRecord(Inspectable("ice", 27, 1, legacy: true)));
                seed.UpsertRecord(ToMapRecord(Inspectable("ordinary", 21, 0, 27015, legacy: true)));
                seed.UpsertRecord(ToMapRecord(Inspectable("supply3", 27, 3)));
                seed.UpsertRecord(ToMapRecord(Inspectable("supply4", 27, 4)));
                seed.UpsertRecord(ToMapRecord(Inspectable("party", 21, 0, 27000)));
                seed.UpsertRecord(ToMapRecord(Inspectable("unsupported", 27, 9)));
                seed.UpsertRecord(ToMapRecord(Inspectable("expired", 27, 1, mutate: data => data["expireTime"] = 2000)));
                seed.UpsertRecord(ToMapRecord(Inspectable("incompleteParty", 21, 0, 27000, data => data["complete"] = false)));
                seed.UpsertRecord(ToMapRecord(Inspectable("wrongClass", 27, 1, mutate: data => data["runtimeClass"] = "TreasurePointInfo")));
                seed.UpsertRecord(ToMapRecord(Inspectable("wrongClassSuffix", 27, 1, mutate: data => data["runtimeClass"] = "WorldSuppliesPointFake")));
                seed.UpsertRecord(ToMapRecord(Inspectable("wrongSubtype", 27, 1, mutate: data => data["otherSubtype"] = "supplies:3")));
                seed.UpsertRecord(ToMapRecord(Inspectable("wrongPoint", 27, 1, mutate: data => data["pointType"] = 6)));
                seed.UpsertRecord(ToMapRecord(Inspectable("wrongEvent", 21, 0, 27015)));
                seed.UpsertRecord(ToMapRecord(Inspectable("wrongTreasureType", 27, 1, mutate: data => data["treasureType"] = 20)));
                seed.UpsertRecord(ToMapRecord(Inspectable("fractionalType", 27, 1, mutate: data => data["suppliesType"] = "1.5")));
            }
            using var store = new Map317.MapStore(database);
            string[] expected = ["ice", "ordinary", "party", "supply3", "supply4"];
            string[] candidates = store.ReadTreasureClaimCandidates(2213, 3000)
                .Select(row => row.GetProperty("uuid").GetString()!).Order(StringComparer.Ordinal).ToArray();
            Require(candidates.SequenceEqual(expected),
                "source-qualified Other remains inspectable; malformed/expired/unsupported rows cannot become candidates" +
                $"; expected=[{string.Join(',', expected)}] actual=[{string.Join(',', candidates)}]");
            Require(store.ReadSeasonSupplyTreasureRows(2213)
                    .Select(row => row.GetProperty("uuid").GetString()!).Order(StringComparer.Ordinal)
                    .SequenceEqual(new[] { "expired", "ice", "supply3", "supply4" }),
                "Refresh All preserves its existing supplies scope without arbitrary Other or Party admission");

            int claimCalls = 0;
            var provider = new Map317.MapActionProviderAdapter
            {
                GetCurrentServerId = _ => ValueTask.FromResult(2213),
                InspectTreasureStates = (rows, _, _) => ValueTask.FromResult(new Map317.TreasureInspectionResult(
                    "viewer-other", "alliance-other", rows.Select(row => JsonSerializer.SerializeToElement(new
                    { uuid = row.GetProperty("uuid").GetString(), worldClaimState = "claimable",
                      playerClaimState = "unclaimed", claimBlockReason = "" })).ToArray())),
                GotoWorldCoordinate = (_, _, _, _) => throw new InvalidOperationException("No navigation"),
                GotoWorldMarch = (_, _, _) => throw new InvalidOperationException("No Follow"),
                GotoServer = (_, _) => throw new InvalidOperationException("No server switch"),
                GetTreasureClaimStatus = _ => throw new InvalidOperationException("Use read-only inspection"),
                ClaimTreasures = (_, _) => { claimCalls++; throw new InvalidOperationException("No claims"); },
                ShareDispatchTaskToAlliance = (_, _) => throw new InvalidOperationException("No sharing"),
                PrepareGhostPlunderTasks = (_, _) => throw new InvalidOperationException("No Ghost"),
                GetMapPlunderServerDayStart = _ => ValueTask.FromResult<Map317.MapPlunderServerDayProviderResult?>(null),
                ArmDispatchPlunder = (_, _) => throw new InvalidOperationException("No plunder"),
                ArmTruckPlunder = (_, _) => throw new InvalidOperationException("No plunder"),
                DrainDispatchPlunderResults = _ => ValueTask.FromResult<IReadOnlyList<Map317.DispatchPlunderResultEvent>>([]),
                DrainTruckPlunderResults = _ => ValueTask.FromResult<IReadOnlyList<Map317.TruckPlunderResultEvent>>([]),
                ClearTruckPlunderPending = (_, _, _, _) => ValueTask.CompletedTask,
            };
            var control = new Map317.MapActionControlPlane(store, provider, () => 3000);
            JsonElement status = await control.TreasureClaimStatusAsync();
            Require(status.GetProperty("inspectedCount").GetInt32() == 5 &&
                !status.GetProperty("claimExecutionAvailable").GetBoolean(), "Other inspection never enables claims");
            JsonElement boxes = await control.PreviewTreasureClaimAsync(2213, "boxes");
            JsonElement season = await control.PreviewTreasureClaimAsync(2213, "season");
            Require(boxes.GetProperty("candidateCount").GetInt32() == 3 &&
                season.GetProperty("candidateCount").GetInt32() == 2 &&
                boxes.GetProperty("rows").EnumerateArray().All(row => !row.GetProperty("execute").GetBoolean()) &&
                claimCalls == 0, "boxes and Party previews preserve workflow without a native action");
            using var persisted = new SqliteConnection(new SqliteConnectionStringBuilder
                { DataSource = database, Mode = SqliteOpenMode.ReadOnly }.ToString());
            persisted.Open();
            using var count = persisted.CreateCommand();
            count.CommandText = "SELECT COUNT(*) FROM treasure_claim_states WHERE server_id=2213 AND player_uid='viewer-other'";
            Require(Convert.ToInt32(count.ExecuteScalar()) == 5 &&
                store.SummaryCounts(2213)["treasure"] == 1 && store.SummaryCounts(2213)["other"] == 14,
                "repeated status/preview upserts five UUID states without duplicating migrated map records");
            Map317.TreasureInspectionResult refreshed = await control.RefreshAllTreasureStatesAsync(2213);
            Require(refreshed.States.Select(row => row.GetProperty("uuid").GetString()!)
                    .Order(StringComparer.Ordinal).SequenceEqual(new[] { "expired", "ice", "supply3", "supply4" }) &&
                claimCalls == 0, "Refresh All reaches classified Supplies through the existing passive inspector");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static async Task CheckAutoOtherStatusPersistenceAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "lwbridge-other-auto-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(root, "auto.json");
        Directory.CreateDirectory(root);
        var noGameBoundary = new MapAutoScanExecutionBoundary
        {
            IsOnline = () => false,
            IsMapScanActive = () => false,
            ReadStatusAsync = (_, _) => throw new InvalidOperationException("No game access permitted"),
            StartTargetScanAsync = (_, _, _, _) => throw new InvalidOperationException("No game scans permitted"),
            ReturnServerAsync = (_, _) => throw new InvalidOperationException("No game navigation permitted"),
            StopScanIfOwnedAsync = (_, _) => throw new InvalidOperationException("No game Stop permitted"),
        };
        try
        {
            await using (var scheduler = new MapAutoScanCommandService(
                path, noGameBoundary, startScheduler: false))
            {
                MapAutoScanSnapshot before = await scheduler.GetSnapshotAsync();
                Require(before.Config.SelectedTypes!.SequenceEqual(MapAutoScanCommandService.DefaultSelectedTypes),
                    "Auto status begins with original five default kinds");
                MapAutoScanSnapshot after = await scheduler.UpdateConfigAsync(
                    before.Config with { SelectedTypes = ["other", "truck"], Enabled = false });
                Require(after.Config.SelectedTypes!.SequenceEqual(["other", "truck"]) && !after.Running,
                    "Auto status keeps explicit Other selection without launching game");
            }
            await using var reopened = new MapAutoScanCommandService(
                path, noGameBoundary, startScheduler: false);
            MapAutoScanSnapshot persisted = await reopened.GetSnapshotAsync();
            Require(persisted.Config.SelectedTypes!.SequenceEqual(["other", "truck"]) &&
                !persisted.Running,
                "reopened Auto status retains explicit Other without changing default selection");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static async Task CheckLateCancelledScanPreservesPublicationAsync()
    {
        using var store = Map317.MapStore.CreateInMemory();
        MapScanExecutionRequest request = new("preexisting", 2213, 0, 40, 20, ["treasure", "other"], 8);
        MapStoredRecord previousOther = MapOtherEventScanSource.Partition(
            new MapScanBlockCapture(2213, 0, 0, "{}", [Record("previousOther", 27, 0, 1)]),
            request).Records.Single();
        store.UpsertRecord(ToMapRecord(previousOther));
        store.UpsertRecord(ToMapRecord(Record("previousTreasure", 21, 19, 0, 27015)));

        using var cancel = new CancellationTokenSource();
        var context = new Map317.MapProviderContext(true, true, 2213, "live", 0, 40, 20, 2);
        var provider = new Map317.MapProviderAdapter(
            _ => ValueTask.FromResult(context),
            _ => ValueTask.FromResult(context),
            (_, _) => ValueTask.FromResult(new Map317.MapProviderStartResult(true, 2)),
            _ => { cancel.Cancel(); return ValueTask.CompletedTask; });
        using var control = new Map317.MapControlPlane(store, provider);
        Map317.MapScanState state = await control.StartScanAsync(
            new Map317.MapScanStartRequest(["treasure", "other"]));
        var held = new HeldSecondSource();
        var engine = new MapScanEngine(new MapOtherEventScanSource(held), new Map317ScanRunSink(control));
        Task engineTask = engine.ExecuteAsync(request with { RunId = state.ScanRunId }, cancel.Token);
        await held.SecondCaptureEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Require(store.ReadOptions(2213, state.ScanRunId).Counts["other"] == 1 &&
            store.ReadOptions(2213, state.ScanRunId).Counts["treasure"] == 1,
            "first block has both real kinds staged before late cancellation");
        await control.StopScanAsync();
        try { await engineTask; }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested) { }
        Require(!control.ScanState.IsReading &&
            store.SummaryCounts(2213)["other"] == 1 &&
            store.SummaryCounts(2213)["treasure"] == 1,
            "late cancelled mixed scan preserved the previous published counts");
        Require(store.Search(new Map317.MapQuery("other", 2213)).Rows
                .Single().GetProperty("uuid").GetString() == "previousOther" &&
            store.Search(new Map317.MapQuery("treasure", 2213)).Rows
                .Single().GetProperty("uuid").GetString() == "previousTreasure" &&
            store.ReadOptions(2213, state.ScanRunId).Counts["other"] == 0,
            "no partial staged Other/Treasure data leaked into publication after Stop");
    }

    private static async Task AssertHostDefaultsAsync()
    {
        var starts = new List<IReadOnlyList<string>>();
        var context = new Map317.MapProviderContext(true, true, 2213, "live", 0, 20, 20, 1);
        var provider = new Map317.MapProviderAdapter(
            _ => ValueTask.FromResult(context),
            _ => ValueTask.FromResult(context),
            (request, _) =>
            {
                starts.Add(request.SelectedTypes.ToArray());
                return ValueTask.FromResult(new Map317.MapProviderStartResult(true, 1));
            },
            _ => ValueTask.CompletedTask);
        var machine = new Map317.MapScanStateMachine(provider, new NoopLocalSink());
        var defaultRun = await machine.StartAsync();
        Require(defaultRun.SelectedTypes.SequenceEqual(Map317.MapKinds.DefaultSelection) &&
            starts.Single().SequenceEqual(Map317.MapKinds.DefaultSelection),
            "real host implicit Start preserves eight kinds through provider boundary");
        await machine.StopAsync();
        var optedIn = await machine.StartAsync(new Map317.MapScanStartRequest(["other"]));
        Require(optedIn.SelectedTypes.SequenceEqual(["other"]) &&
            starts.Last().SequenceEqual(["other"]),
            "real host passes an explicitly requested Other selection");
        await machine.StopAsync();
    }

    private static void CheckLegacyMigrationAndDuplicate()
    {
        string root = Path.Combine(Path.GetTempPath(), "lwbridge-other-migration-" + Guid.NewGuid().ToString("N"));
        string database = Path.Combine(root, "map-data.db");
        Directory.CreateDirectory(root);
        try
        {
            using (var original = new Map317.MapStore(database))
            {
                MapStoredRecord legacy = Record("legacy9", 27, 0, 9);
                original.UpsertRecord(ToMapRecord(legacy));
                MapStoredRecord duplicate = Record("existing3", 27, 0, 3);
                original.UpsertRecord(ToMapRecord(duplicate));
                original.UpsertRecord(ToMapRecord(duplicate with
                {
                    Kind = "other",
                    DataJson = JsonSerializer.Serialize(new
                    {
                        kind = "other", uuid = "existing3", serverId = 2213,
                        pointType = 27, runtimeClass = "WorldSuppliesPoint",
                        otherSubtype = "supplies:3", otherNameKey = "season_s3_supplies_1",
                        suppliesType = 3, x = 0, y = 999, origin = "original-other-wins",
                    }),
                }));
                original.UpsertRecord(ToMapRecord(Record("party-other", 21, 20, 0, 27000)));
                original.UpsertRecord(ToMapRecord(Record("unresolved-party", 21, 0, 0, 27000)));
            }
            for (int reopen = 0; reopen < 2; reopen++)
            {
                using var migrated = new Map317.MapStore(database);
                Require(migrated.SummaryCounts(2213)["treasure"] == 1 &&
                    migrated.SummaryCounts(2213)["other"] == 3,
                    "migration deduplicates supported sources and leaves nonpositive unverified event class unclassified");
                var distinct = migrated.Search(new Map317.MapQuery("other", 2213));
                Require(distinct.Rows.Select(row => row.GetProperty("uuid").GetString())
                    .Distinct(StringComparer.Ordinal).Count() == 3,
                    "migration preserves one stored identity per source key");
                var unknown = migrated.Search(new Map317.MapQuery("other", 2213, OtherSubtype: "supplies:9"));
                Require(unknown.Total == 1 &&
                    unknown.Rows[0].GetProperty("otherNameKey").ValueKind == JsonValueKind.Null,
                    "legacy numeric supplies 9 gets no invented name");
                var existing = migrated.Search(new Map317.MapQuery("other", 2213, OtherSubtype: "supplies:3"));
                Require(existing.Total == 1 &&
                    existing.Rows[0].GetProperty("origin").GetString() == "original-other-wins" &&
                    !existing.Rows[0].TryGetProperty("otherNameKey", out _),
                    "older Other winner survives collision and guessed name gets scrubbed");
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static Map317.MapRecord ToMapRecord(MapStoredRecord record) => new(
        record.Kind, record.ServerId, record.RecordKey, record.PointIndex,
        record.Uuid, record.Name, record.AllianceName, record.Level, record.Quality,
        record.Power, record.Distance, record.ShieldEndTime, record.UpdatedAt,
        record.DataJson);

    private sealed class NoopLocalSink : Map317.IMapScanLocalSink
    {
        public ValueTask CancelScanAsync(string _, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask ClearServerAsync(int _, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    private sealed class HeldSecondSource : IMapScanBlockSource
    {
        internal TaskCompletionSource<bool> SecondCaptureEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<MapScanBlockCapture> CaptureAsync(
            MapScanExecutionRequest request, MapScanTargetBlock block, CancellationToken cancellationToken)
        {
            if (block.BlockIndex == 1)
            {
                SecondCaptureEntered.TrySetResult(true);
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }
            return new MapScanBlockCapture(2213, 0, block.BlockIndex, "{\"recordsInBlock\":2}",
                [Record("newlyStagedSupplies", 27, 0, 9),
                 Record("newlyStagedTreasure", 21, 19, 0, 27015)]);
        }
    }

    private sealed class FakeSource(MapScanBlockCapture capture) : IMapScanProgressBatchSource
    {
        internal int Requests { get; private set; }
        internal IReadOnlyList<string>? LastTypes { get; private set; }

        public Task<MapScanBlockCapture> CaptureAsync(
            MapScanExecutionRequest request, MapScanTargetBlock block, CancellationToken cancellationToken)
        {
            Requests++;
            LastTypes = request.SelectedTypes;
            return Task.FromResult(capture);
        }

        public Task<IReadOnlyList<MapScanBlockCapture>> CaptureBatchAsync(
            MapScanExecutionRequest request, MapScanTargetBlock seedBlock,
            IReadOnlySet<int> pendingBlockIndices, CancellationToken cancellationToken) =>
            CaptureBatchAsync(request, seedBlock, pendingBlockIndices, null, cancellationToken);

        public Task<IReadOnlyList<MapScanBlockCapture>> CaptureBatchAsync(
            MapScanExecutionRequest request, MapScanTargetBlock seedBlock,
            IReadOnlySet<int> pendingBlockIndices, Action<MapScanSourceProgress>? progress,
            CancellationToken cancellationToken)
        {
            Requests++;
            LastTypes = request.SelectedTypes;
            return Task.FromResult<IReadOnlyList<MapScanBlockCapture>>([capture]);
        }
    }
}
