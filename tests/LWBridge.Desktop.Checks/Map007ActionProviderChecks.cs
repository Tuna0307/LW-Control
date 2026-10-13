using System.Text.Json;
using LWBridge.Desktop;
using Map317 = LWBridge.Map317;

namespace LWBridge.Desktop.Checks;

// Real production Map317CommandService + MapActionControlPlane + SQLite;
// game reads and results are an explicitly inert controlled provider.
internal static class Map007ActionProviderChecks
{
    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException("MAP007_HANDLERS: " + reason);
    }

    private static async Task<JsonElement> Call(Map317CommandService service, string command, object payload) =>
        JsonSerializer.SerializeToElement(await service.InvokeAsync(
            command, JsonSerializer.SerializeToElement(payload, JsonOptions.Default), CancellationToken.None));

    private static async Task Reject(Func<Task> attempt, string expected)
    {
        try { await attempt(); }
        catch (Map317.BridgeCommandException error) when (error.Code == expected) { return; }
        catch (BridgeCommandException error) when (error.Code == expected) { return; }
        throw new InvalidOperationException("MAP007_HANDLERS expected " + expected);
    }

    internal static async Task RunAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "lw-map007-actions-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "map.db");
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        try
        {
            using (var store = new Map317.MapStore(path))
            {
                for (int i = 1; i <= 101; i++)
                {
                    bool box = i % 2 == 0;
                    string payload = JsonSerializer.Serialize(new
                    {
                        kind = "treasure", serverId = 317, pointIndex = i,
                        uuid = i.ToString(), suppliesType = box ? 1 : 0,
                        treasureType = box ? 0 : 1, complete = true,
                        expireTime = now + 100_000, viewerUid = "viewer-007",
                    });
                    store.UpsertRecord(new Map317.MapRecord("treasure", 317, i.ToString(), i,
                        i.ToString(), null, null, null, null, null, null, null, now, payload));
                }
            }
            int server = 317, inspections = 0, claimSends = 0, dispatchSends = 0;
            string identity = "viewer-007";
            bool changeSecondBatchIdentity = false;
            bool duplicateSecondBatchUuid = false;
            var readGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            bool hold = false;
            var boundary = new Map317.MapActionProviderAdapter
            {
                GotoWorldCoordinate = (_, _, _, _) => ValueTask.CompletedTask,
                GotoWorldMarch = (_, _, _) => ValueTask.CompletedTask,
                GetCurrentServerId = _ => ValueTask.FromResult(server),
                GotoServer = (_, _) => ValueTask.CompletedTask,
                InspectTreasureStates = async (rows, _, cancellationToken) =>
                {
                    inspections++;
                    Require(rows.Count <= 100 && rows.Count > 0, "inspection batch was not bounded");
                    if (hold)
                    {
                        readGate.TrySetResult(true);
                        await releaseGate.Task.WaitAsync(cancellationToken);
                    }
                    var result = rows.Select(row => JsonSerializer.SerializeToElement(new
                    {
                        uuid = duplicateSecondBatchUuid && rows.Count == 1 ? "1" : row.GetProperty("uuid").ToString(),
                        worldClaimState = "claimable",
                        playerClaimState = row.GetProperty("uuid").ToString() == "2" ? "claimed" : "unclaimed",
                        claimBlockReason = row.GetProperty("uuid").ToString() == "4" ? "other_alliance" : "",
                    })).ToArray();
                    return new Map317.TreasureInspectionResult(
                        changeSecondBatchIdentity && rows.Count == 1 ? "wrong-viewer" : identity,
                        "alliance-007", result);
                },
                GetTreasureClaimStatus = _ => throw new InvalidOperationException("Must use real inspect, not unsupported claim status"),
                ClaimTreasures = (_, _) =>
                {
                    claimSends++;
                    return ValueTask.FromException<JsonElement>(new Map317.BridgeCommandException(
                        "GAME_PROVIDER_UNAVAILABLE", "controlled claim terminal protocol unavailable"));
                },
                ShareDispatchTaskToAlliance = (_, _) => throw new InvalidOperationException("No sharing"),
                PrepareGhostPlunderTasks = (rows, _) =>
                {
                    try { return ValueTask.FromResult(CurrentClientMap317ActionProvider.PrepareGhostPlunderRows(rows)); }
                    catch (InvalidDataException error)
                    {
                        return ValueTask.FromException<IReadOnlyList<JsonElement>>(
                            new Map317.BridgeCommandException("INVALID_REQUEST", error.Message));
                    }
                },
                GetMapPlunderServerDayStart = _ => ValueTask.FromResult<Map317.MapPlunderServerDayProviderResult?>(null),
                ArmDispatchPlunder = (_, _) =>
                {
                    dispatchSends++;
                    throw new InvalidOperationException("No plunder");
                },
                ArmTruckPlunder = (_, _) => throw new InvalidOperationException("No plunder"),
                DrainDispatchPlunderResults = _ => ValueTask.FromResult<IReadOnlyList<Map317.DispatchPlunderResultEvent>>([]),
                DrainTruckPlunderResults = _ => ValueTask.FromResult<IReadOnlyList<Map317.TruckPlunderResultEvent>>([]),
                ClearTruckPlunderPending = (_, _, _, _) => ValueTask.CompletedTask,
            };
            using var service = new Map317CommandService(path, Map317.UnavailableMapProvider.Instance,
                boundary, startPlunderWorkers: false);
            JsonElement snapshot = await Call(service, "map_treasure_claim_status", new { });
            Require(snapshot.GetProperty("candidateCount").GetInt32() == 101 &&
                snapshot.GetProperty("inspectedCount").GetInt32() == 101 && inspections == 2 &&
                !snapshot.GetProperty("claimExecutionAvailable").GetBoolean(),
                "real controller failed full 100+1 read-only inspection");
            JsonElement preview = await Call(service, "map_treasure_claim_preview", new
            {
                serverId = 317, claimScope = "boxes", prioritizeLuckySlots = true,
            });
            Require(preview.GetProperty("candidateCount").GetInt32() == 50 &&
                preview.GetProperty("eligibleCount").GetInt32() == 48 &&
                preview.GetProperty("pendingCount").GetInt32() == 2 &&
                preview.GetProperty("rows").EnumerateArray().All(row => !row.GetProperty("execute").GetBoolean()) &&
                claimSends == 0,
                "box preview fabricated a claim or bypassed claimed/alliance blockers");
            JsonElement single = await Call(service, "map_treasure_claim_preview", new
            {
                serverId = 317, claimScope = "single", targetUuid = "2",
            });
            Require(single.GetProperty("candidateCount").GetInt32() == 1 &&
                single.GetProperty("eligibleCount").GetInt32() == 0, "already claimed single incorrectly eligible");
            JsonElement season = await Call(service, "map_treasure_claim_preview", new
            {
                serverId = 317, claimScope = "season",
            });
            Require(season.GetProperty("candidateCount").GetInt32() == 51, "season dry-run scope mismatch");
            // The first validated batch must not be persisted when the next
            // batch changes session/player or replays another UUID.
            changeSecondBatchIdentity = true;
            await Reject(async () => _ = await Call(service, "map_treasure_claim_status", new { }),
                "TREASURE_STATE_UNAVAILABLE");
            changeSecondBatchIdentity = false;
            duplicateSecondBatchUuid = true;
            await Reject(async () => _ = await Call(service, "map_treasure_claim_status", new { }),
                "TREASURE_STATE_UNAVAILABLE");
            duplicateSecondBatchUuid = false;
            await Reject(async () => _ = await Call(service, "map_treasure_claim_preview", new
            { serverId = 318, claimScope = "boxes" }), "SERVER_MISMATCH");
            await Reject(async () => _ = await Call(service, "map_treasure_claim_preview", new
            { serverId = 317, claimScope = "single", targetUuid = "missing" }), "NOT_FOUND");
            await Reject(async () => _ = await Call(service, "map_treasure_claim", new
            { serverId = 317, claimScope = "boxes" }), "GAME_PROVIDER_UNAVAILABLE");
            await Reject(async () => _ = await Call(service, "map_dispatch_plunder_schedule", new
            { rows = new[] { new { taskKind = "ghost", uuid = "12345", serverId = 317,
                completionTime = now - 1_000, plunderAt = now + 10_000,
                taskExpireTime = now + 60_000, stolenCount = 0, maxStealCount = 2 } } }),
                "GHOST_PLUNDER_PROVIDER_UNAVAILABLE");

            var ghost = new
            {
                kind = "ghost", taskKind = "ghost", pointType = 29,
                runtimeClass = "GhostreconPointInfo", serverId = 317,
                uuid = "123456", ownerServer = 317,
                completionTime = now - 1_000, protectTime = 30,
                plunderAt = now + 29_000, taskExpireTime = now + 120_000,
                stealListCount = 0, stolenCount = 0, stealMaxTimes = 2, maxStealCount = 2,
            };
            JsonElement prepared = await Call(service, "map_ghost_plunder_prepare", new { rows = new[] { ghost } });
            Require(prepared.GetArrayLength() == 1 && prepared[0].GetProperty("uuid").GetString() == "123456" &&
                dispatchSends == 0, "Ghost preparation was routed as Dispatch game transport");
            await Reject(async () => _ = await Call(service, "map_ghost_plunder_prepare", new
            { rows = new[] { ghost, ghost } }), "INVALID_REQUEST");
            JsonElement wrong = JsonSerializer.SerializeToElement(ghost);
            using var changed = JsonDocument.Parse(wrong.GetRawText().Replace("GhostreconPointInfo", "DispatchPointInfo"));
            await Reject(async () => _ = await Call(service, "map_ghost_plunder_prepare", new
            { rows = new[] { changed.RootElement.Clone() } }), "INVALID_REQUEST");

            hold = true;
            Task<JsonElement> held = Call(service, "map_treasure_claim_status", new { });
            await readGate.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Reject(async () => _ = await Call(service, "map_ghost_plunder_prepare", new
            { rows = new[] { ghost } }), "GAME_OPERATION_IN_PROGRESS");
            identity = "different-user";
            releaseGate.TrySetResult(true);
            await held;
            Require(claimSends == 1 && dispatchSends == 0, "read-only preview sent an extra action request");
            Console.WriteLine("MAP_007_REAL_COMMAND_GHOST_PREP_TREASURE_INSPECT_PREVIEW_OK");
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
