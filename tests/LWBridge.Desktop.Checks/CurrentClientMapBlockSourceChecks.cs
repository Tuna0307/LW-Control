using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class CurrentClientMapBlockSourceChecks
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 2, 0, 0, TimeSpan.Zero);
    private static readonly OverviewMapScanSession Session = new(
        "default", "session_1", "challenge_1", 4242, @"C:\Game\LastWar.exe", "2026-09-14T01:00:00.0000000Z");

    [ModuleInitializer]
    internal static void Run()
    {
        RunAsync().GetAwaiter().GetResult();
    }

    private static async Task RunAsync()
    {
        await ResourceViewEnumeratesAndFilters();
        await EmptyResourceViewIsAZeroRowCapture();
        await LodZeroAcquiresAllFourCells();
        await LodTwoFiltersSupersetToRequestedBlock();
        await TargetedCityFallbackFailsClosed();
        await HealthyGateRunsBeforeWorldReadyProtocol();
        await MissingOwnedSessionFailsClosed();
    }

    private static MapScanExecutionRequest Request(string kind, long width = 20, long height = 20) =>
        new("run_1", 2212, 7, width, height, [kind], 8, 2);

    private static MapScanTargetBlock Block() =>
        new(0, 0, 0, 0, 0, 19, 19, 9, 9);

    private static async Task ResourceViewEnumeratesAndFilters()
    {
        int probeCalls = 0;
        CurrentClientMapBlockSource source = CreateSource((fields, mapKind) =>
        {
            probeCalls++;
            return ProvenResourceSnapshot(fields, (101, 4, 5), (102, 24, 5), (103, 8, 0));
        });

        MapScanBlockCapture capture = await source.CaptureAsync(Request("resource"), Block(), CancellationToken.None);
        Check(probeCalls == 1, "resource block source should consume one complete fresh candidate snapshot");
        Check(capture.ServerId == 2212 && capture.WorldId == 7 && capture.BlockIndex == 0,
            "resource capture identity changed");
        Check(capture.Records.Count == 2 &&
              capture.Records.Select(record => record.RecordKey).Order().SequenceEqual(new[] { "101", "103" }),
            "resource capture should retain in-block records, including valid coordinate zero");
        using JsonDocument payload = JsonDocument.Parse(capture.PayloadJson);
        Check(payload.RootElement.GetProperty("cells")[0].GetProperty("matchedCount").GetInt32() == 3 &&
              payload.RootElement.GetProperty("recordsInBlock").GetInt32() == 2 &&
              payload.RootElement.GetProperty("coverage").GetString() == "planned_aoi_cells_pending_live_footprint_proof" &&
              payload.RootElement.GetProperty("serverLod").GetInt32() == 1 &&
              payload.RootElement.GetProperty("aoiBlockSize").GetInt32() == 20 &&
              payload.RootElement.GetProperty("cells").GetArrayLength() == 1,
            "resource checkpoint must preserve truthful planned AOI coverage metadata");
    }

    private static async Task EmptyResourceViewIsAZeroRowCapture()
    {
        CurrentClientMapBlockSource source = CreateSource((fields, _) => FailedEmptyResource(fields));
        MapScanBlockCapture capture = await source.CaptureAsync(Request("resource"), Block(), CancellationToken.None);
        Check(capture.Records.Count == 0, "fresh empty resource view should produce zero staged rows");
        using JsonDocument payload = JsonDocument.Parse(capture.PayloadJson);
        Check(payload.RootElement.GetProperty("cells")[0].GetProperty("matchedCount").GetInt32() == 0,
            "fresh empty resource view should checkpoint zero candidates for its AOI cell");
    }

    private static async Task LodZeroAcquiresAllFourCells()
    {
        int probeCalls = 0;
        CurrentClientMapBlockSource source = CreateSource((fields, _) =>
        {
            probeCalls++;
            return FailedEmptyResource(fields);
        }, currentLod: 4, serverLod: 0);

        MapScanBlockCapture capture = await source.CaptureAsync(Request("resource"), Block(), CancellationToken.None);
        Check(probeCalls == 4, "server LOD 0 should acquire all four planned 10-tile AOI cells");
        using JsonDocument payload = JsonDocument.Parse(capture.PayloadJson);
        JsonElement cells = payload.RootElement.GetProperty("cells");
        Check(payload.RootElement.GetProperty("serverLod").GetInt32() == 0 &&
              payload.RootElement.GetProperty("aoiBlockSize").GetInt32() == 10 &&
              cells.GetArrayLength() == 4,
            "server LOD 0 checkpoint should preserve its four-cell AOI plan");
        var actual = cells.EnumerateArray()
            .Select(cell => (cell.GetProperty("minX").GetInt32(), cell.GetProperty("minY").GetInt32(),
                             cell.GetProperty("maxX").GetInt32(), cell.GetProperty("maxY").GetInt32()))
            .ToArray();
        Check(actual.SequenceEqual(new[] { (0, 0, 9, 9), (10, 0, 19, 9), (0, 10, 9, 19), (10, 10, 19, 19) }),
            "server LOD 0 source cell bounds changed");
    }

    private static async Task LodTwoFiltersSupersetToRequestedBlock()
    {
        int probeCalls = 0;
        CurrentClientMapBlockSource source = CreateSource((fields, _) =>
        {
            probeCalls++;
            return ProvenResourceSnapshot(fields, (301, 5, 5), (302, 25, 5));
        }, currentLod: 6, serverLod: 2);

        MapScanBlockCapture capture = await source.CaptureAsync(Request("resource", 40, 40), Block(), CancellationToken.None);
        Check(probeCalls == 1, "server LOD 2 should consume one complete superset candidate snapshot");
        Check(capture.Records.Count == 1 && capture.Records[0].RecordKey == "301",
            "server LOD 2 should filter the larger AOI response back to the requested scan block");
        using JsonDocument payload = JsonDocument.Parse(capture.PayloadJson);
        JsonElement cell = payload.RootElement.GetProperty("cells")[0];
        Check(payload.RootElement.GetProperty("serverLod").GetInt32() == 2 &&
              payload.RootElement.GetProperty("aoiBlockSize").GetInt32() == 1000 &&
              cell.GetProperty("minX").GetInt32() == 0 && cell.GetProperty("minY").GetInt32() == 0 &&
              cell.GetProperty("maxX").GetInt32() == 39 && cell.GetProperty("maxY").GetInt32() == 39 &&
              payload.RootElement.GetProperty("recordsInBlock").GetInt32() == 1,
            "server LOD 2 checkpoint should preserve the clamped superset AOI cell and filtered result count");
    }

    private static async Task TargetedCityFallbackFailsClosed()
    {
        CurrentClientMapBlockSource source = CreateSource((fields, _) => ProvenCityTargeted(fields));
        try
        {
            _ = await source.CaptureAsync(Request("city"), Block(), CancellationToken.None);
            throw new InvalidOperationException("targeted Player City fallback should not be accepted as block coverage");
        }
        catch (InvalidDataException error) when (
            error.Message == "Player City block acquisition left the requested current view.")
        {
        }
    }

    private static async Task HealthyGateRunsBeforeWorldReadyProtocol()
    {
        bool healthGatePassed = false;
        bool worldReadyObserved = false;
        CurrentClientMapBlockSource source = CreateSource(
            (fields, _) => FailedEmptyResource(fields),
            waitForHealthySession: (session, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                Check(session == Session, "health gate received a different owned session");
                healthGatePassed = true;
                return Task.CompletedTask;
            },
            onProtocolWrite: path =>
            {
                if (!path.EndsWith("world-ready.txt", StringComparison.OrdinalIgnoreCase)) return;
                worldReadyObserved = true;
                Check(healthGatePassed, "world-ready protocol ran before the game-health gate");
            });

        _ = await source.CaptureAsync(Request("resource"), Block(), CancellationToken.None);
        Check(healthGatePassed && worldReadyObserved,
            "current-client block source must gate world entry on healthy game readiness");
    }

    private static async Task MissingOwnedSessionFailsClosed()
    {
        var source = new CurrentClientMapBlockSource(
            () => null,
            @"C:\overview",
            @"C:\probe",
            new CurrentClientMapBlockSourceHooks());
        try
        {
            _ = await source.CaptureAsync(Request("resource"), Block(), CancellationToken.None);
            throw new InvalidOperationException("missing owned Overview session should fail closed");
        }
        catch (BridgeCommandException error) when (
            error.Code == "GAME_CONNECTION_UNAVAILABLE" && error.Message == "game connection unavailable")
        {
        }
    }

    private static CurrentClientMapBlockSource CreateSource(
        Func<IReadOnlyDictionary<string, string>, string, string> probeResult,
        int currentLod = 5,
        int serverLod = 1,
        Func<OverviewMapScanSession, CancellationToken, Task>? waitForHealthySession = null,
        Action<string>? onProtocolWrite = null)
    {
        var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        string overviewRoot = @"C:\overview";
        string probeRoot = @"C:\probe";        var hooks = new CurrentClientMapBlockSourceHooks
        {
            UtcNow = () => Now,
            DelayAsync = (_, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            },
            ReadAllBytes = path => files.TryGetValue(path, out byte[]? bytes)
                ? bytes
                : throw new FileNotFoundException("test protocol file unavailable", path),
            WriteTextAtomic = (path, text) =>
            {
                onProtocolWrite?.Invoke(path);
                IReadOnlyDictionary<string, string> fields = ParseKv(text);
                if (string.Equals(path, Path.Combine(overviewRoot, "world-ready.txt"), StringComparison.OrdinalIgnoreCase))
                {
                    string result = WorldReadyResult(fields);
                    files[Path.Combine(overviewRoot, "world-ready-result.json")] = Encoding.UTF8.GetBytes(result);
                    return;
                }
                if (string.Equals(path, Path.Combine(overviewRoot, "map-navigation.txt"), StringComparison.OrdinalIgnoreCase))
                {
                    string result = NavigationResult(fields, currentLod, serverLod);
                    files[Path.Combine(overviewRoot, "map-navigation-result.json")] = Encoding.UTF8.GetBytes(result);
                    return;
                }
                if (string.Equals(path, Path.Combine(probeRoot, "command.txt"), StringComparison.OrdinalIgnoreCase))
                {
                    string mapKind = fields["mapKind"];
                    string result = probeResult(fields, mapKind);
                    files[Path.Combine(probeRoot, "result.json")] = Encoding.UTF8.GetBytes(result);
                    return;
                }
                throw new InvalidOperationException("unexpected protocol write: " + path);
            },
        };
        return new CurrentClientMapBlockSource(
            () => Session,
            overviewRoot,
            probeRoot,
            hooks,
            waitForHealthySession);
    }

    private static IReadOnlyDictionary<string, string> ParseKv(string text) =>
        text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);

    private static string WorldReadyResult(IReadOnlyDictionary<string, string> fields) =>
        JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            bridgeVersion = "lwbridge-overview-bridge-1",
            profileId = fields["profileId"],
            sessionId = fields["sessionId"],
            challenge = fields["challenge"],
            gamePid = int.Parse(fields["gamePid"]),
            requestId = fields["requestId"],
            state = "proven",
            method = "already_world_scene",
            serverId = 2212,
            worldId = 0,
            tileWidth = 1000,
            tileHeight = 1000,
        }, JsonOptions.Default);

    private static string NavigationResult(
        IReadOnlyDictionary<string, string> fields,
        int currentLod,
        int serverLod) =>
        JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            bridgeVersion = "lwbridge-overview-bridge-1",
            profileId = fields["profileId"],
            sessionId = fields["sessionId"],
            challenge = fields["challenge"],
            gamePid = int.Parse(fields["gamePid"]),
            requestId = fields["requestId"],
            state = "proven",
            serverId = int.Parse(fields["serverId"]),
            worldId = long.Parse(fields["worldId"]),
            liveCurServerId = int.Parse(fields["serverId"]),
            liveSelfServerId = int.Parse(fields["serverId"]),
            liveWorldId = 0,
            targetX = int.Parse(fields["targetX"]),
            targetY = int.Parse(fields["targetY"]),
            currentX = 137,
            currentY = 674,
            verifiedTargetX = int.Parse(fields["targetX"]),
            verifiedTargetY = int.Parse(fields["targetY"]),
            expectedUniqueTileX = int.Parse(fields["targetX"]),
            expectedUniqueTileY = int.Parse(fields["targetY"]),
            verifiedUniqueTileX = int.Parse(fields["targetX"]),
            verifiedUniqueTileY = int.Parse(fields["targetY"]),
            expectedGotoWorldX = 219.0,
            expectedGotoWorldY = 0.0,
            expectedGotoWorldZ = 219.0,
            targetWorldX = 219.0,
            targetWorldY = 0.0,
            targetWorldZ = 219.0,
            postCurTargetX = 219.0,
            postCurTargetY = 0.0,
            postCurTargetZ = 219.0,
            verifyCurTargetX = 219.0,
            verifyCurTargetY = 0.0,
            verifyCurTargetZ = 219.0,
            currentLod,
            serverLod,
            lwAoiBlockSizeArray = new[] { 10, 20, 1000 },
            method = "SceneUtils.TileToWorld(ForceChangeScene.World,serverId)+GoToUtil.GotoWorldPos(serverId,worldId)+completionCallbackCurTarget",
        }, JsonOptions.Default);

    private static string ProvenResourceSnapshot(
        IReadOnlyDictionary<string, string> fields,
        params (int PointId, int X, int Y)[] points) => JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            probeVersion = "lwbridge-live-resource-probe-2",
            requestId = fields["requestId"],
            launchSessionId = fields["launchSessionId"],
            profileId = fields["profileId"],
            gamePid = int.Parse(fields["gamePid"]),
            mapKind = "resource",
            acquisitionOrdinal = 1,
            state = "proven",
            capturedAt = Timestamp(),
            requestRoute = "WorldPointManager.StartViewRequest+UpdateViewRequest(true)",
            responseEvidence = "isRecvViewPoints=false->true;hasReceiveViewPointsReply=false->true",
            source = "WorldPointManager._pointInfos",
            loadedPointCount = points.Length,
            resourcePointCount = points.Length,
            selectedResourceIndex = 1,
            point_records = points.Select(point => new
            {
                kind = "resource_point",
                serverId = 2212,
                pointId = point.PointId,
                x = point.X,
                y = point.Y,
                level = 3,
                name = "Resource",
                source = "WorldPointManager._pointInfos",
            }).ToArray(),
        }, JsonOptions.Default);

    private static string FailedEmptyResource(IReadOnlyDictionary<string, string> fields) =>
        JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            probeVersion = "lwbridge-live-resource-probe-2",
            requestId = fields["requestId"],
            launchSessionId = fields["launchSessionId"],
            profileId = fields["profileId"],
            gamePid = int.Parse(fields["gamePid"]),
            mapKind = "resource",            state = "failed",
            capturedAt = Timestamp(),
            error = "no resource point is loaded after the fresh view response",
        }, JsonOptions.Default);

    private static string ProvenCityTargeted(IReadOnlyDictionary<string, string> fields) =>
        JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            probeVersion = "lwbridge-live-resource-probe-2",
            requestId = fields["requestId"],
            launchSessionId = fields["launchSessionId"],
            profileId = fields["profileId"],
            gamePid = int.Parse(fields["gamePid"]),
            mapKind = "city",
            acquisitionOrdinal = 1,
            state = "proven",
            capturedAt = Timestamp(),
            requestRoute = "WorldPointManager.StartViewRequest+UpdateViewRequest(true)+SendViewRequest(PlayerWorldPointId,currentLOD,currentServerId)",
            responseEvidence = "isRecvViewPoints=false->true;hasReceiveViewPointsReply=false->true;targetedSameServerView=false->true",
            source = "WorldPointManager._pointInfos",
            loadedPointCount = 1,
            cityPointCount = 1,
            selectedCityIndex = 1,
            cityTargetedView = true,
            point_records = new[]
            {
                new
                {
                    kind = "player_base",
                    pointType = 6,
                    serverId = 2212,
                    pointId = 200,
                    x = 9,
                    y = 9,                    ownerUid = "u1",
                    ownerName = "Player",
                    level = 30,
                    source = "WorldPointManager._pointInfos",
                },
            },
        }, JsonOptions.Default);

    private static string Timestamp() => Now.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
