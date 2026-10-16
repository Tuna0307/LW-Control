using System.Text;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

// Actual managed producer/projector, with inert native receipts. No game I/O.
internal static class Map016RailwayChecks
{
    internal static async Task RunAsync()
    {
        await CaptureAsync(positive: false);
        await CaptureAsync(positive: true);
        Console.WriteLine("MAP016_RAILWAY_CONTROLLED_OK empty=2500 typedPositive=1 camera=0 stationExpiry=retained");
    }

    private static async Task CaptureAsync(bool positive)
    {
        var session = new OverviewMapScanSession("railway-profile", "railway-session", "railway-challenge",
            37123, @"C:\nonexistent-map016-game.exe", DateTimeOffset.UtcNow.ToString("O"));
        var request = new MapScanExecutionRequest("railway-run", 2211, 0, 1000, 1000,
            ["railway"], 20, LaunchSessionId: session.SessionId, LiveServerId: 2211);
        int commands = 0;
        byte[]? receipt = null;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var source = new CurrentClientMapBlockSource(() => session, "inert-overview", "inert-resource",
            new CurrentClientMapBlockSourceHooks
            {
                UtcNow = () => now,
                WriteTextAtomic = (path, command) =>
                {
                    if (Path.GetFileName(path) != "fast-truck-scan.txt")
                        throw new Exception("Railway unexpectedly used a camera/list command: " + path);
                    commands++;
                    string Value(string name) => command.Split('\n').Single(line => line.StartsWith(name + "=", StringComparison.Ordinal))[(name.Length + 1)..];
                    Require(Value("trainType") == "2" && Value("includeRailway") == "true",
                        "Railway selected the Truck-only native source");
                    var rows = new JsonArray();
                    if (positive)
                        rows.Add(new JsonObject
                        {
                            ["uuid"] = "visible-railway", ["marchUuid"] = "visible-railway",
                            ["serverId"] = 2211, ["worldId"] = 0, ["x"] = 623, ["y"] = 300,
                            ["positionIndex"] = 300624, ["trainType"] = 2,
                            ["trainQuality"] = 5, ["trainCfgId"] = 152, ["carriageNum"] = 6,
                            ["ownerName"] = "Native Driver", ["allianceName"] = "Native Alliance",
                            ["endTime"] = 1, ["arriveTs"] = now.ToUnixTimeMilliseconds() + 60000,
                            ["capturedAtMs"] = now.ToUnixTimeMilliseconds(),
                            ["source"] = "WorldScene.MarchDataManager.GetAllMarchesByCS+WorldMarch.train",
                        });
                    receipt = Encoding.UTF8.GetBytes(new JsonObject
                    {
                        ["schemaVersion"] = 1, ["probeVersion"] = "lwbridge-live-resource-probe-2",
                        ["requestId"] = Value("requestId"), ["profileId"] = session.ProfileId,
                        ["launchSessionId"] = session.SessionId, ["challenge"] = session.Challenge,
                        ["gamePid"] = session.GamePid, ["scanRunId"] = request.RunId,
                        ["serverId"] = 2211, ["worldId"] = 0,
                        ["state"] = "proven", ["cameraStable"] = true, ["curViewRestored"] = true,
                        ["initialRequestsSent"] = 625, ["rectanglesCompleted"] = 625,
                        ["unprovenRectangles"] = 0, ["requestsPerSecond"] = 20,
                        ["responseFenceSize"] = 25, ["responseAttribution"] = "bounded_sequential_fences",
                        ["repairPasses"] = 0, ["requestsSent"] = 625, ["repairRequestsSent"] = 0,
                        ["pushesObserved"] = 625, ["trainRecords"] = new JsonArray(), ["trainRecordCount"] = 0,
                        ["railwayRecords"] = rows, ["railwayRecordCount"] = rows.Count,
                        ["railwayIncluded"] = true, ["railwayOnly"] = true,
                        ["capturedAt"] = now.ToString("O"),
                    }.ToJsonString());
                },
                ReadAllBytes = path => Path.GetFileName(path) == "fast-truck-scan-result.json" && receipt is not null
                    ? receipt : throw new FileNotFoundException("No inert receipt", path),
                DelayAsync = (_, _) => Task.CompletedTask,
            });
        IReadOnlyList<MapScanTargetBlock> blocks = MapScanTraversal.Build(1000, 1000);
        IReadOnlyList<MapScanBlockCapture> captured = await source.CaptureBatchAsync(
            request, blocks[0], blocks.Select(block => block.BlockIndex).ToHashSet(), CancellationToken.None);
        Require(commands == 1 && captured.Count == 2500, "Railway did not use one complete native sweep");
        MapStoredRecord[] records = captured.SelectMany(block => block.Records).ToArray();
        Require(records.Length == (positive ? 1 : 0), "Railway typed record count changed");
        if (!positive) return;
        Require(records[0].Kind == "railway" && records[0].ServerId == 2211,
            "Railway changed kind/server");
        using JsonDocument data = JsonDocument.Parse(records[0].DataJson);
        Require(data.RootElement.GetProperty("captureProtocol").GetString() == "current_fast_railway_625_v1" &&
                data.RootElement.GetProperty("endTime").GetInt64() == 1,
            "Railway lost provenance or inherited Truck segment expiry");
        CheckRejectedRailwayReceipts(receipt!, request, now);
    }

    private static void CheckRejectedRailwayReceipts(byte[] receipt, MapScanExecutionRequest request, DateTimeOffset now)
    {
        MethodInfo importer = typeof(CurrentClientMapBlockSource).GetMethod("PrepareFastRailwayRows",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        foreach (Action<JsonObject> corrupt in new Action<JsonObject>[]
        {
            root => root["rectanglesCompleted"] = 624,
            root => root["railwayRecordCount"] = 2,
            root => root["railwayIncluded"] = false,
            root => root["railwayRecords"]![0]!["trainType"] = 1,
            root => root["railwayRecords"]![0]!["serverId"] = 2212,
            root => root["railwayRecords"]![0]!["source"] = "unattributed",
            root => root["railwayRecords"]![0]!["capturedAtMs"] = now.ToUnixTimeMilliseconds() + 10000,
        })
        {
            JsonObject damaged = JsonNode.Parse(receipt)!.AsObject();
            corrupt(damaged);
            try
            {
                importer.Invoke(null, [JsonSerializer.SerializeToElement(damaged), request, now, true]);
                throw new InvalidOperationException("MAP016_RAILWAY: unsafe Railway receipt was accepted");
            }
            catch (TargetInvocationException error) when (error.InnerException is InvalidDataException) { }
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("MAP016_RAILWAY: " + message);
    }
}
