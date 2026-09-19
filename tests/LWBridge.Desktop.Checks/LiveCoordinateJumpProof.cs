using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class LiveCoordinateJumpProof
{
    internal static async Task RunAsync()
    {
        string gameRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FunFly", "Last War-Survival Game");
        using var lifecycle = new OverviewLifecycleService("coordinate-jump-proof", gameRoot);
        using var operationCts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        string? instanceId = null;
        Exception? operationError = null;

        try
        {
            using JsonDocument empty = JsonDocument.Parse("{}");
            object? start = await lifecycle.InvokeAsync(
                "profile_instance_start", empty.RootElement, operationCts.Token).ConfigureAwait(false);
            JsonElement startJson = JsonSerializer.SerializeToElement(start, JsonOptions.Default);
            instanceId = startJson.GetProperty("instanceId").GetString();

            using MapDataStore store = MapDataStore.CreateInMemory();
            var service = new ManualMapScanCommandService(lifecycle, store);
            var source = new CurrentClientMapBlockSource(lifecycle);
            try
            {
                CurrentClientMapContext initial =
                    await source.GetCurrentContextAsync(operationCts.Token).ConfigureAwait(false);
                (int targetX, int targetY) = SelectTarget(initial);

                JsonElement payload = JsonSerializer.SerializeToElement(
                    new
                    {
                        serverId = initial.ServerId,
                        x = targetX,
                        y = targetY,
                    },
                    JsonOptions.Default);
                object? jumpResult = await service.InvokeAsync(
                    "map_coordinate_jump", payload, operationCts.Token).ConfigureAwait(false);
                JsonElement jumped = JsonSerializer.SerializeToElement(jumpResult, JsonOptions.Default);
                if (jumped.GetProperty("serverId").GetInt32() != initial.ServerId ||
                    jumped.GetProperty("x").GetInt32() != targetX ||
                    jumped.GetProperty("y").GetInt32() != targetY)
                {
                    throw new InvalidDataException(
                        "map_coordinate_jump did not return the requested current-server tile.");
                }

                bool returnAttempted = false;
                bool returnProven = false;
                int? returnX = null;
                int? returnY = null;
                if (initial.PlayerTileX is int playerX &&
                    initial.PlayerTileY is int playerY &&
                    (playerX != targetX || playerY != targetY))
                {
                    returnAttempted = true;
                    returnX = playerX;
                    returnY = playerY;
                    JsonElement returnPayload = JsonSerializer.SerializeToElement(
                        new
                        {
                            serverId = initial.ServerId,
                            x = playerX,
                            y = playerY,
                        },
                        JsonOptions.Default);
                    object? returnResult = await service.InvokeAsync(
                        "map_coordinate_jump", returnPayload, operationCts.Token).ConfigureAwait(false);
                    JsonElement returned = JsonSerializer.SerializeToElement(returnResult, JsonOptions.Default);
                    returnProven =
                        returned.GetProperty("serverId").GetInt32() == initial.ServerId &&
                        returned.GetProperty("x").GetInt32() == playerX &&
                        returned.GetProperty("y").GetInt32() == playerY;
                    if (!returnProven)
                        throw new InvalidDataException(
                            "map_coordinate_jump did not prove the return-to-player tile.");
                }

                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    ok = true,
                    proof = "map_coordinate_jump_owned_session_callback",
                    serverId = initial.ServerId,
                    worldId = initial.WorldId,
                    tileWidth = initial.TileWidth,
                    tileHeight = initial.TileHeight,
                    playerTileX = initial.PlayerTileX,
                    playerTileY = initial.PlayerTileY,
                    targetX,
                    targetY,
                    returned = new
                    {
                        attempted = returnAttempted,
                        proven = returnProven,
                        x = returnX,
                        y = returnY,
                    },
                    safety = new
                    {
                        cameraNavigationOnly = true,
                        mapScanStarted = false,
                        attackOrPlunder = false,
                        claimOrCollect = false,
                        messaging = false,
                    },
                }, JsonOptions.Default));
            }
            finally
            {
                service.Close();
            }
        }
        catch (Exception error)
        {
            operationError = error;
            throw;
        }
        finally
        {
            instanceId ??= lifecycle.GetReadyMapScanSession()?.SessionId;
            if (!string.IsNullOrWhiteSpace(instanceId))
            {
                using var stopCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                using JsonDocument stopPayload = JsonDocument.Parse(
                    JsonSerializer.Serialize(new { instanceId }, JsonOptions.Default));
                try
                {
                    await lifecycle.InvokeAsync(
                        "profile_instance_stop", stopPayload.RootElement, stopCts.Token).ConfigureAwait(false);
                }
                catch (Exception stopError)
                {
                    Console.Error.WriteLine("LIVE_COORDINATE_JUMP_STOP_FAILED: " + stopError.Message);
                    if (operationError is null) throw;
                }
            }
        }
    }

    private static (int X, int Y) SelectTarget(CurrentClientMapContext context)
    {
        if (context.TileWidth <= 1 || context.TileHeight <= 1)
            throw new InvalidDataException("Live map dimensions are too small for a coordinate-jump proof.");

        int originX = context.PlayerTileX ?? context.TileWidth / 2;
        int originY = context.PlayerTileY ?? context.TileHeight / 2;
        int maxX = context.TileWidth - 1;
        int maxY = context.TileHeight - 1;

        int targetX = originX + 37 <= maxX ? originX + 37 : Math.Max(1, originX - 37);
        int targetY = originY + 53 <= maxY ? originY + 53 : Math.Max(1, originY - 53);
        targetX = Math.Clamp(targetX, 1, maxX);
        targetY = Math.Clamp(targetY, 1, maxY);

        if (targetX == originX && targetY == originY)
        {
            targetX = originX == 1 ? Math.Min(2, maxX) : 1;
            targetY = originY == 1 ? Math.Min(2, maxY) : 1;
        }
        return (targetX, targetY);
    }
}
