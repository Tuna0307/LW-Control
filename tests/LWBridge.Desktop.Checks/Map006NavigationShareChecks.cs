using System.Text.Json;
using LWBridge.Desktop;
using Map317 = LWBridge.Map317;

namespace LWBridge.Desktop.Checks;

// Actual Map317 command/action plane with controlled provider acknowledgements.
// No game navigation, server movement, alliance share, Treasure claim or plunder.
internal static class Map006NavigationShareChecks
{
    private static void Require(bool valid, string why)
    {
        if (!valid) throw new InvalidOperationException("MAP_006_ACTION_CONTROL: " + why);
    }

    private static async Task<JsonElement> Invoke(
        Map317CommandService service, string command, object payload) =>
        JsonSerializer.SerializeToElement(await service.InvokeAsync(command,
            JsonSerializer.SerializeToElement(payload, JsonOptions.Default), CancellationToken.None),
            JsonOptions.Default);

    private static async Task MustReject(
        Func<Task> action, string code, string why)
    {
        try { await action(); }
        catch (Map317.BridgeCommandException error) when (error.Code == code) { return; }
        catch (BridgeCommandException error) when (error.Code == code) { return; }
        throw new InvalidOperationException("MAP_006_ACTION_CONTROL: expected " + code + " for " + why);
    }

    internal static async Task RunAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "lwbridge-map006-actions-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            int server = 317;
            bool rejectMarch = true;
            bool rejectCoordinate = false;
            int worldMoves = 0;
            int marches = 0;
            int serverMoves = 0;
            int shareCalls = 0;
            var heldEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var heldRelease = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            bool holdCoordinate = false;
            var actions = new Map317.MapActionProviderAdapter
            {
                GotoWorldCoordinate = async (target, x, y, cancellation) =>
                {
                    worldMoves++;
                    Require(target == 317 && x == 2 && y == 3,
                        "coordinate provider received altered target");
                    if (holdCoordinate)
                    {
                        heldEntered.TrySetResult(true);
                        await heldRelease.Task.WaitAsync(cancellation);
                    }
                    if (rejectCoordinate)
                        throw new Map317.BridgeCommandException("MAP_NAVIGATION_FAILED", "target view not reached");
                },
                GotoWorldMarch = (target, march, cancellation) =>
                {
                    marches++;
                    Require(target == 317 && march == "123456", "march id changed in provider");
                    return rejectMarch
                        ? ValueTask.FromException(new Map317.BridgeCommandException(
                            "MARCH_FOLLOW_FAILED", "no authoritative camera result"))
                        : ValueTask.CompletedTask;
                },
                GetCurrentServerId = _ => ValueTask.FromResult(server),
                GotoServer = (target, _) =>
                {
                    serverMoves++;
                    server = target;
                    return ValueTask.CompletedTask;
                },
                InspectTreasureStates = (_, _, _) => ValueTask.FromResult(
                    new Map317.TreasureInspectionResult("owner", "alliance", Array.Empty<JsonElement>())),
                GetTreasureClaimStatus = _ => ValueTask.FromException<JsonElement>(
                    new Map317.BridgeCommandException("GAME_PROVIDER_UNAVAILABLE", "claim status not available")),
                ClaimTreasures = (_, _) => ValueTask.FromException<JsonElement>(
                    new Map317.BridgeCommandException("GAME_PROVIDER_UNAVAILABLE", "claim result not available")),
                ShareDispatchTaskToAlliance = (row, _) =>
                {
                    shareCalls++;
                    string id = row.GetProperty("uuid").GetString()!;
                    if (id == "3")
                        return ValueTask.FromException<Map317.DispatchShareProviderResult>(
                            new Map317.BridgeCommandException("GAME_PROVIDER_ERROR", "share rejected"));
                    return ValueTask.FromResult(new Map317.DispatchShareProviderResult(id == "1"));
                },
                PrepareGhostPlunderTasks = (_, _) => ValueTask.FromException<IReadOnlyList<JsonElement>>(
                    new Map317.BridgeCommandException("GAME_PROVIDER_UNAVAILABLE", "Ghost provider unavailable")),
                GetMapPlunderServerDayStart = _ => ValueTask.FromResult<Map317.MapPlunderServerDayProviderResult?>(null),
                ArmDispatchPlunder = (_, _) => throw new InvalidOperationException("No game plunder permitted"),
                ArmTruckPlunder = (_, _) => throw new InvalidOperationException("No game plunder permitted"),
                DrainDispatchPlunderResults = _ => ValueTask.FromResult<IReadOnlyList<Map317.DispatchPlunderResultEvent>>([]),
                DrainTruckPlunderResults = _ => ValueTask.FromResult<IReadOnlyList<Map317.TruckPlunderResultEvent>>([]),
                ClearTruckPlunderPending = (_, _, _, _) => ValueTask.CompletedTask,
            };
            using var service = new Map317CommandService(
                Path.Combine(root, "map-data.db"), Map317.UnavailableMapProvider.Instance,
                actions, startPlunderWorkers: false);

            await MustReject(async () => _ = await Invoke(service, "map_coordinate_jump",
                new { serverId = 0, x = 2, y = 3 }), "INVALID_COORDINATE", "zero server");
            await MustReject(async () => _ = await Invoke(service, "map_coordinate_jump",
                new { serverId = 317, x = 2, y = 0 }), "INVALID_COORDINATE", "invalid tile");
            Require(worldMoves == 0, "invalid navigation reached current-client boundary");
            rejectCoordinate = true;
            await MustReject(async () => _ = await Invoke(service, "map_coordinate_jump",
                new { serverId = 317, x = 2, y = 3 }), "MAP_NAVIGATION_FAILED", "camera not reached");
            rejectCoordinate = false;
            JsonElement coordinate = await Invoke(service, "map_coordinate_jump", new { serverId = 317, x = 2, y = 3 });
            Require(coordinate.GetProperty("x").GetInt32() == 2 &&
                coordinate.GetProperty("y").GetInt32() == 3 && worldMoves == 2,
                "positive camera acknowledgement did not reach host result");

            holdCoordinate = true;
            Task<JsonElement> held = Invoke(service, "map_coordinate_jump", new { serverId = 317, x = 2, y = 3 });
            await heldEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await MustReject(async () => _ = await Invoke(service, "map_march_follow",
                new { serverId = 317, marchUuid = "123456" }),
                "GAME_OPERATION_IN_PROGRESS", "overlapping navigation");
            heldRelease.TrySetResult(true);
            await held;
            holdCoordinate = false;

            await MustReject(async () => _ = await Invoke(service, "map_march_follow",
                new { serverId = 317, marchUuid = "123456" }), "MARCH_FOLLOW_FAILED", "not-followed march");
            rejectMarch = false;
            JsonElement followed = await Invoke(service, "map_march_follow",
                new { serverId = 317, marchUuid = "123456" });
            Require(followed.GetProperty("marchUuid").GetString() == "123456" && marches == 2,
                "authoritatively followed march was not returned");

            await MustReject(async () => _ = await Invoke(service, "server_jump",
                new { serverId = 0 }), "INVALID_SERVER_ID", "invalid jump target");
            JsonElement noMove = await Invoke(service, "server_jump", new { serverId = 317 });
            Require(!noMove.GetProperty("changed").GetBoolean() && serverMoves == 0,
                "same-server request unexpectedly moved current client");
            JsonElement moved = await Invoke(service, "server_jump", new { serverId = 318 });
            Require(moved.GetProperty("changed").GetBoolean() &&
                moved.GetProperty("previousServerId").GetInt32() == 317 &&
                moved.GetProperty("serverId").GetInt32() == 318 && serverMoves == 1,
                "controlled jump did not verify the reached server");

            var rows = new[]
            {
                new { uuid = "1", serverId = 317, x = 2, y = 3, cfgId = 4 },
                new { uuid = "2", serverId = 317, x = 2, y = 3, cfgId = 4 },
                new { uuid = "3", serverId = 317, x = 2, y = 3, cfgId = 4 },
            };
            JsonElement shared = await Invoke(service, "map_dispatch_share_alliance", new { rows });
            Require(shared.GetProperty("shared").GetInt32() == 1 &&
                shared.GetProperty("failed").GetInt32() == 2 &&
                shared.GetProperty("sharedUuids").GetArrayLength() == 1 &&
                shared.GetProperty("sharedUuids")[0].GetString() == "1" && shareCalls == 3,
                "share batch counted a rejected/unacknowledged request as success");
            await MustReject(async () => _ = await Invoke(service, "map_treasure_claim_status",
                new { }), "GAME_PROVIDER_UNAVAILABLE", "unavailable claim provider");
            Console.WriteLine("MAP_006_CONTROLLED_NAVIGATION_SHARE_ACK_GATE_OK");
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
