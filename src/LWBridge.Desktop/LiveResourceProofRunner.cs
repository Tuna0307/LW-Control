using System.Globalization;
using System.Text.Json;

namespace LWBridge.Desktop;

internal static class LiveResourceProofRunner
{
    public static async Task RunTwiceAsync(string outputPath)
    {
        string fullOutput = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullOutput) ?? Directory.GetCurrentDirectory());
        object? firstStatus = null;
        object? firstSearch = null;
        JsonElement? firstResult = null;
        JsonElement? firstHelper = null;
        string? mapPath = null;
        try
        {
            var config = new LocalConfigStore();
            mapPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LWBridgeRebuild", "profiles", config.Snapshot.ProfileId, "map-data.db");
            using var mapData = new MapDataStore(mapPath);
            var service = new LiveResourceProbeCommandService(mapData);
            var backend = new LWBridgeBackend(config, asyncCommands: service, mapData: mapData);

            using JsonDocument startPayload = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                profileId = backend.ProfileId,
                selectedTypes = new[] { "resource" },
                scanMode = "normal",
            }, JsonOptions.Default));

            firstStatus = await backend.InvokeAsync(
                "map_scan_start", startPayload.RootElement.Clone(), CancellationToken.None);
            RequireCompletedStatus(firstStatus);
            firstHelper = RequireHelperEvidence(service.LastHelperResult);
            firstResult = ReadResult(service.LiveResultPath);
            int firstServerId = RequiredServerId(firstResult.Value);
            firstSearch = await SearchAsync(backend, firstServerId);
            WriteProof(fullOutput, new
            {
                schemaVersion = 1,
                findingId = "LWB-R7-003",
                stage = "first_completed",
                generatedAtUtc = DateTimeOffset.UtcNow,
                first = new { status = firstStatus, helper = firstHelper.Value, result = firstResult.Value, search = firstSearch },
                mapDatabase = mapPath,
            });

            object? secondStatus = await backend.InvokeAsync(
                "map_scan_start", startPayload.RootElement.Clone(), CancellationToken.None);
            RequireCompletedStatus(secondStatus);
            JsonElement secondHelper = RequireHelperEvidence(service.LastHelperResult);
            JsonElement secondResult = ReadResult(service.LiveResultPath);
            int secondServerId = RequiredServerId(secondResult);
            object? secondSearch = await SearchAsync(backend, secondServerId);
            object? summary = await SummaryAsync(backend);

            string? firstRequest = firstResult.Value.GetProperty("requestId").GetString();
            string? secondRequest = secondResult.GetProperty("requestId").GetString();
            if (string.IsNullOrWhiteSpace(firstRequest) || string.IsNullOrWhiteSpace(secondRequest) || firstRequest == secondRequest)
                throw new InvalidDataException("The two live acquisitions did not produce distinct app request identifiers.");
            if (firstResult.Value.GetProperty("state").GetString() != "proven" || secondResult.GetProperty("state").GetString() != "proven")
                throw new InvalidDataException("Both bounded live acquisitions must be correlated proven results.");
            DateTimeOffset firstCapturedAt = DateTimeOffset.Parse(firstResult.Value.GetProperty("capturedAt").GetString()!, CultureInfo.InvariantCulture);
            DateTimeOffset secondCapturedAt = DateTimeOffset.Parse(secondResult.GetProperty("capturedAt").GetString()!, CultureInfo.InvariantCulture);
            if (secondCapturedAt <= firstCapturedAt)
                throw new InvalidDataException("The second live acquisition did not have a newer capture time.");

            WriteProof(fullOutput, new
            {
                schemaVersion = 1,
                findingId = "LWB-R7-003",
                stage = "complete",
                generatedAtUtc = DateTimeOffset.UtcNow,
                implementation = "LWBridge.Desktop LiveResourceProbeCommandService",
                source = "current game WorldPointManager._pointInfos after a fresh StartViewRequest/UpdateViewRequest(true) response",
                originalPipeClaimed = false,
                first = new { status = firstStatus, helper = firstHelper.Value, result = firstResult.Value, search = firstSearch },
                second = new { status = secondStatus, helper = secondHelper, result = secondResult, search = secondSearch },
                summary,
                probeOnlineAfterSecond = service.IsProbeOnline,
                mapDatabase = mapPath,
            });
        }
        catch (Exception ex)
        {
            WriteProof(fullOutput, new
            {
                schemaVersion = 1,
                findingId = "LWB-R7-003",
                stage = "failed",
                generatedAtUtc = DateTimeOffset.UtcNow,
                errorType = ex.GetType().FullName,
                error = ex.Message,
                first = firstResult.HasValue ? new { status = firstStatus, helper = firstHelper, result = firstResult.Value, search = firstSearch } : null,
                mapDatabase = mapPath,
            });
            throw;
        }
    }

    private static void WriteProof(string path, object value) =>
        File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions.Indented));

    private static JsonElement ReadResult(string path)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
        return document.RootElement.Clone();
    }

    private static int RequiredServerId(JsonElement result)
    {
        JsonElement point = result.GetProperty("point_records")[0];
        if (!point.TryGetProperty("serverId", out JsonElement server) || !server.TryGetInt32(out int value) || value <= 0)
            throw new InvalidDataException("Live resource result did not contain a positive serverId.");
        return value;
    }

    private static async Task<object?> SearchAsync(LWBridgeBackend backend, int serverId)
    {
        using JsonDocument searchPayload = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            profileId = backend.ProfileId,
            kind = "resource",
            query = new { serverId, page = 1, pageSize = 50 },
        }, JsonOptions.Default));
        return await backend.InvokeAsync("map_search", searchPayload.RootElement.Clone(), CancellationToken.None);
    }

    private static async Task<object?> SummaryAsync(LWBridgeBackend backend)
    {
        using JsonDocument payload = JsonDocument.Parse(JsonSerializer.Serialize(
            new { profileId = backend.ProfileId }, JsonOptions.Default));
        return await backend.InvokeAsync("map_summary", payload.RootElement.Clone(), CancellationToken.None);
    }

    private static void RequireCompletedStatus(object? status)
    {
        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(status, JsonOptions.Default));
        JsonElement root = document.RootElement;
        if (!root.TryGetProperty("isReading", out JsonElement isReading) || isReading.ValueKind != JsonValueKind.False ||
            !root.TryGetProperty("phase", out JsonElement phase) || phase.ValueKind != JsonValueKind.String || phase.GetString() != "idle" ||
            !root.TryGetProperty("serverId", out JsonElement serverId) || !serverId.TryGetInt32(out int parsedServerId) || parsedServerId <= 0)
        {
            throw new InvalidDataException("Completed live acquisition did not return an idle, non-reading status with a positive serverId.");
        }
    }

    private static JsonElement RequireHelperEvidence(JsonElement? helper)
    {
        if (!helper.HasValue)
            throw new InvalidDataException("Live acquisition did not preserve helper provenance.");
        JsonElement root = helper.Value;
        if (!root.TryGetProperty("ok", out JsonElement ok) || ok.ValueKind != JsonValueKind.True ||
            !root.TryGetProperty("installedFilesChanged", out JsonElement changed) || changed.ValueKind != JsonValueKind.False)
        {
            throw new InvalidDataException("Live acquisition helper did not prove a successful restored client state.");
        }
        if (root.TryGetProperty("restore", out JsonElement restore) &&
            (!restore.TryGetProperty("restored", out JsonElement restored) || restored.ValueKind != JsonValueKind.True))
        {
            throw new InvalidDataException("Live acquisition helper restore metadata did not report success.");
        }
        return root.Clone();
    }
}
