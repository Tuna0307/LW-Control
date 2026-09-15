using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class LiveManualMapScanProof
{
    internal static async Task<object> RunAsync(
        CurrentClientMapBlockSource source,
        CurrentClientMapContext context,
        CancellationToken cancellationToken)
    {
        using MapDataStore store = MapDataStore.CreateInMemory();
        var service = new ManualMapScanCommandService(
            store,
            source.GetCurrentContextAsync,
            source);
        try
        {
            JsonElement payload = JsonSerializer.SerializeToElement(new
            {
                profileId = "current-block-live-proof",
                scanMode = "normal",
                selectedTypes = new[] { "resource" },
            }, JsonOptions.Default);

            object? start = await service.InvokeAsync(
                "map_scan_start",
                payload,
                cancellationToken).ConfigureAwait(false);            JsonElement startStatus = JsonSerializer.SerializeToElement(start, JsonOptions.Default);
            int expectedTotal = checked((int)MapScanGeometry.FromTileDimensions(
                context.TileWidth,
                context.TileHeight).TotalBlocks);
            if (ReadInt(startStatus, "serverId") != context.ServerId ||
                ReadInt(startStatus, "concurrency") != 8 ||
                ReadInt(startStatus, "totalBlocks") != expectedTotal ||
                ReadString(startStatus, "scanMode") != "normal")
            {
                throw new InvalidDataException(
                    "Production Manual Scan Start did not preserve live context and Normal-mode geometry.");
            }

            JsonElement activeStatus = default;
            DateTimeOffset progressDeadline = DateTimeOffset.UtcNow.AddSeconds(60);
            while (DateTimeOffset.UtcNow < progressDeadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                activeStatus = Status(service);
                string phase = ReadString(activeStatus, "phase");
                int failed = ReadInt(activeStatus, "failedBlocks");
                if (phase == "error" || failed > 0)
                    throw new InvalidDataException(
                        "Production Manual Scan failed before completing its first live block: " +
                        ReadOptionalString(activeStatus, "lastError"));
                if (ReadInt(activeStatus, "readBlocks") >= 1) break;
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }            int completedBeforeStop = ReadInt(activeStatus, "readBlocks");
            if (completedBeforeStop < 1)
                throw new TimeoutException(
                    "Production Manual Scan did not complete a live block before the bounded proof deadline.");

            _ = await service.InvokeAsync(
                "map_scan_stop",
                payload,
                cancellationToken).ConfigureAwait(false);

            JsonElement finalStatus = default;
            DateTimeOffset stopDeadline = DateTimeOffset.UtcNow.AddSeconds(20);
            while (DateTimeOffset.UtcNow < stopDeadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                finalStatus = Status(service);
                string phase = ReadString(finalStatus, "phase");
                if (!ReadBool(finalStatus, "isReading") && phase is "idle" or "completed")
                    break;
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }

            if (ReadBool(finalStatus, "isReading"))
                throw new TimeoutException(
                    "Production Manual Scan Stop did not release live scan ownership.");            return new
            {
                proof = "production_manual_scan_start_stop_first_live_block",
                serverId = context.ServerId,
                worldId = context.WorldId,
                tileWidth = context.TileWidth,
                tileHeight = context.TileHeight,
                scanMode = "normal",
                concurrency = 8,
                totalBlocks = expectedTotal,
                completedBlocksBeforeStop = completedBeforeStop,
                finalPhase = ReadString(finalStatus, "phase"),
                stopped = !ReadBool(finalStatus, "isReading"),
            };
        }
        finally
        {
            service.Close();
        }
    }

    private static JsonElement Status(ManualMapScanCommandService service) =>
        JsonSerializer.SerializeToElement(service.CreateStatus(), JsonOptions.Default);

    private static int ReadInt(JsonElement root, string name) =>
        root.GetProperty(name).GetInt32();
    private static bool ReadBool(JsonElement root, string name) =>
        root.GetProperty(name).GetBoolean();

    private static string ReadString(JsonElement root, string name) =>
        root.GetProperty(name).GetString() ?? string.Empty;

    private static string ReadOptionalString(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
}
