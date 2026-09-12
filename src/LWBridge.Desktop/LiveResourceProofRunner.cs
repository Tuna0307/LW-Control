using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LWBridge.Desktop;

internal static class LiveResourceProofRunner
{
    public static async Task RunTwiceAsync(string outputPath, string mapKind = "resource")
    {
        if (mapKind is not ("resource" or "city"))
            throw new ArgumentOutOfRangeException(nameof(mapKind));
        string findingId = mapKind == "city" ? "LWB-PC-001" : "LWB-R7-003";
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
            GameRootStatus liveGameRoot = new GameInstallationService(config).GetStatus();
            var service = new LiveResourceProbeCommandService(
                mapData,
                gameRoot: liveGameRoot.Valid ? liveGameRoot.Path : null,
                profileId: config.Snapshot.ProfileId);
            var backend = new LWBridgeBackend(config, asyncCommands: service, mapData: mapData);

            using JsonDocument startPayload = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                profileId = backend.ProfileId,
                selectedTypes = new[] { mapKind },
                scanMode = "normal",
            }, JsonOptions.Default));

            firstStatus = await backend.InvokeAsync(
                "map_scan_start", startPayload.RootElement.Clone(), CancellationToken.None);
            RequireCompletedStatus(firstStatus);
            firstHelper = RequireHelperEvidence(service.LastHelperResult);
            firstResult = ReadResult(service.LiveResultPath);
            int firstServerId = RequiredServerId(firstResult.Value);
            firstSearch = await SearchAsync(backend, firstServerId, mapKind);
            WriteProof(fullOutput, new
            {
                schemaVersion = 1,
                findingId,
                stage = "first_completed",
                generatedAtUtc = DateTimeOffset.UtcNow,
                first = new { status = firstStatus, helper = firstHelper.Value, result = firstResult.Value, search = firstSearch },
                mapDatabase = mapPath,
            }, mapKind == "city");

            object? secondStatus = await backend.InvokeAsync(
                "map_scan_start", startPayload.RootElement.Clone(), CancellationToken.None);
            RequireCompletedStatus(secondStatus);
            JsonElement secondHelper = RequireHelperEvidence(service.LastHelperResult);
            JsonElement secondResult = ReadResult(service.LiveResultPath);
            int secondServerId = RequiredServerId(secondResult);
            object? secondSearch = await SearchAsync(backend, secondServerId, mapKind);
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
                findingId,
                stage = "complete",
                generatedAtUtc = DateTimeOffset.UtcNow,
                implementation = "LWBridge.Desktop LiveResourceProbeCommandService",
                mapKind,
                source = "current game WorldPointManager._pointInfos after a fresh StartViewRequest/UpdateViewRequest(true) response",
                originalPipeClaimed = false,
                first = new { status = firstStatus, helper = firstHelper.Value, result = firstResult.Value, search = firstSearch },
                second = new { status = secondStatus, helper = secondHelper, result = secondResult, search = secondSearch },
                summary,
                probeOnlineAfterSecond = service.IsProbeOnline,
                mapDatabase = mapPath,
            }, mapKind == "city");
        }
        catch (Exception ex)
        {
            WriteProof(fullOutput, new
            {
                schemaVersion = 1,
                findingId,
                stage = "failed",
                generatedAtUtc = DateTimeOffset.UtcNow,
                errorType = ex.GetType().FullName,
                error = ex.Message,
                first = firstResult.HasValue ? new { status = firstStatus, helper = firstHelper, result = firstResult.Value, search = firstSearch } : null,
                mapDatabase = mapPath,
            }, mapKind == "city");
            throw;
        }
    }

    public static async Task RunCityReopenAsync(string outputPath)
    {
        string fullOutput = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullOutput) ?? Directory.GetCurrentDirectory());
        var config = new LocalConfigStore();
        string mapPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LWBridgeRebuild", "profiles", config.Snapshot.ProfileId, "map-data.db");
        using var mapData = new MapDataStore(mapPath);
        var backend = new LWBridgeBackend(config, mapData: mapData);
        object? summary = await SummaryAsync(backend);
        using JsonDocument summaryDocument = JsonDocument.Parse(JsonSerializer.Serialize(summary, JsonOptions.Default));
        JsonElement summaryRoot = summaryDocument.RootElement;
        if (!summaryRoot.TryGetProperty("serverId", out JsonElement serverValue) ||
            !serverValue.TryGetInt32(out int serverId) || serverId <= 0 ||
            !summaryRoot.TryGetProperty("counts", out JsonElement counts) ||
            !counts.TryGetProperty("city", out JsonElement cityCount) ||
            !cityCount.TryGetInt32(out int parsedCityCount) || parsedCityCount <= 0)
        {
            throw new InvalidDataException("Fresh-process reopen did not find persisted Player City context in the active profile.");
        }
        object? search = await SearchAsync(backend, serverId, "city");
        using JsonDocument searchDocument = JsonDocument.Parse(JsonSerializer.Serialize(search, JsonOptions.Default));
        JsonElement searchRoot = searchDocument.RootElement;
        if (!searchRoot.TryGetProperty("total", out JsonElement totalValue) ||
            !totalValue.TryGetInt32(out int total) || total <= 0 ||
            !searchRoot.TryGetProperty("rows", out JsonElement rows) || rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() == 0)
        {
            throw new InvalidDataException("Fresh-process reopen normal map_search did not return the persisted Player City row.");
        }
        WriteProof(fullOutput, new
        {
            schemaVersion = 1,
            findingId = "LWB-PC-002",
            stage = "reopen_complete",
            generatedAtUtc = DateTimeOffset.UtcNow,
            profileId = backend.ProfileId,
            serverId,
            mapDatabase = mapPath,
            summary,
            search,
        }, redactCityIdentity: true);
    }

    private static readonly string[] CityIdentityFields =
        ["ownerName", "ownerUid", "uuid", "allianceName", "allianceId", "playerName"];
    private static void WriteProof(string path, object value, bool redactCityIdentity = false)
    {
        string json = JsonSerializer.Serialize(value, JsonOptions.Indented);
        if (redactCityIdentity)
            json = SanitizeCityProofJsonForSharedEvidence(json);
        File.WriteAllText(path, json);
    }

    internal static string SanitizeCityProofJsonForSharedEvidence(string json)
    {
        JsonNode? node = JsonNode.Parse(json);
        RedactCityIdentity(node);
        return node?.ToJsonString(JsonOptions.Indented) ?? json;
    }

    private static void RedactCityIdentity(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach (string key in CityIdentityFields) obj.Remove(key);
            if (obj.ContainsKey("profileId")) obj["profileId"] = "<active-profile>";
            foreach ((string key, JsonNode? child) in obj.ToArray())
            {
                if (child is JsonValue value && value.TryGetValue<string>(out string? text) && text is not null)
                    obj[key] = RedactCityProofString(text);
                else
                    RedactCityIdentity(child);
            }
            return;
        }
        if (node is JsonArray array)
        {
            for (int i = 0; i < array.Count; i++)
            {
                JsonNode? child = array[i];
                if (child is JsonValue value && value.TryGetValue<string>(out string? text) && text is not null)
                    array[i] = RedactCityProofString(text);
                else
                    RedactCityIdentity(child);
            }
        }
    }

    private static string RedactCityProofString(string value)
    {
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!string.IsNullOrWhiteSpace(userProfile) && value.StartsWith(userProfile, StringComparison.OrdinalIgnoreCase))
            value = "%USERPROFILE%" + value[userProfile.Length..];
        return RedactLocalProfilePathSegment(value, '\\') is string windowsRedacted && !ReferenceEquals(windowsRedacted, value)
            ? windowsRedacted
            : RedactLocalProfilePathSegment(value, '/');
    }

    private static string RedactLocalProfilePathSegment(string value, char separator)
    {
        string marker = $"{separator}profiles{separator}";
        int markerIndex = value.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0) return value;
        int profileStart = markerIndex + marker.Length;
        int profileEnd = value.IndexOf(separator, profileStart);
        if (profileEnd <= profileStart) return value;
        string profile = value[profileStart..profileEnd];
        if (!profile.StartsWith("local-", StringComparison.OrdinalIgnoreCase) ||
            profile.Length <= "local-".Length ||
            !profile["local-".Length..].All(Uri.IsHexDigit))
            return value;
        return value[..profileStart] + "<active-profile>" + value[profileEnd..];
    }

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

    private static async Task<object?> SearchAsync(LWBridgeBackend backend, int serverId, string mapKind)
    {
        using JsonDocument searchPayload = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            profileId = backend.ProfileId,
            kind = mapKind,
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
