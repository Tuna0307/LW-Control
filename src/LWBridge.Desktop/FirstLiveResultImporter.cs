using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LWBridge.Desktop;

internal sealed record FirstLiveResultImport(
    int ServerId,
    string RecordKey,
    int PointIndex,
    int X,
    int Y,
    int? Level,
    long CapturedAtUnixMilliseconds,
    string CaptureSha256,
    string SourcePath,
    string? ProbeVersion,
    string? DeclaredSourceCaptureSha256,
    string DataJson);

internal sealed record FirstLivePreparedResource(
    FirstLiveResultImport Import,
    MapStoredRecord Record);

internal static class FirstLiveResultImporter
{
    // Validates a live-probe result against the requesting scan before its first
    // point is prepared (moved here from the retired live-resource probe service).
    private const string ExpectedProbeVersion = "lwbridge-live-resource-probe-2";

    internal static FirstLivePreparedResource PrepareCorrelatedResult(
        string resultPath,
        string requestId,
        out int? acquisitionOrdinal,
        Func<string, byte[]>? readAllBytes = null,
        int? expectedServerId = null,
        DateTimeOffset? operationStartedAtUtc = null,
        DateTimeOffset? nowUtc = null,
        string? expectedProfileId = null,
        string? expectedLaunchSessionId = null,
        int? expectedGamePid = null,
        string mapKind = "resource")
    {
        if (mapKind is not ("resource" or "city"))
            throw new ArgumentOutOfRangeException(nameof(mapKind));
        byte[] resultBytes = (readAllBytes ?? File.ReadAllBytes)(resultPath);
        using JsonDocument resultDocument = JsonDocument.Parse(resultBytes);
        JsonElement root = resultDocument.RootElement;
        if (!root.TryGetProperty("schemaVersion", out JsonElement schemaValue) ||
            !schemaValue.TryGetInt32(out int schemaVersion) || schemaVersion != 1 ||
            !root.TryGetProperty("probeVersion", out JsonElement probeVersionValue) ||
            probeVersionValue.ValueKind != JsonValueKind.String ||
            probeVersionValue.GetString() != ExpectedProbeVersion)
        {
            throw new InvalidDataException("Live resource result did not match the supported probe schema/version.");
        }
        if (root.TryGetProperty("mapKind", out JsonElement resultMapKind))
        {
            if (resultMapKind.ValueKind != JsonValueKind.String || !string.Equals(resultMapKind.GetString(), mapKind, StringComparison.Ordinal))
                throw new InvalidDataException("Live map result did not match the requested map kind.");
        }
        else if (mapKind != "resource")
            throw new InvalidDataException("Live city result did not identify its requested map kind.");

        if (!root.TryGetProperty("requestId", out JsonElement requestIdValue) ||
            requestIdValue.ValueKind != JsonValueKind.String ||
            !string.Equals(requestIdValue.GetString(), requestId, StringComparison.Ordinal) ||
            !root.TryGetProperty("state", out JsonElement stateValue) ||
            stateValue.ValueKind != JsonValueKind.String ||
            stateValue.GetString() != "proven")
        {
            throw new InvalidDataException("Live resource result did not match the app request correlation contract.");
        }
        if (expectedProfileId is not null &&
            (!root.TryGetProperty("profileId", out JsonElement profileValue) ||
             profileValue.ValueKind != JsonValueKind.String ||
             !string.Equals(profileValue.GetString(), expectedProfileId, StringComparison.Ordinal)))
        {
            throw new InvalidDataException("Live resource result did not match the selected app profile identity.");
        }
        if (expectedLaunchSessionId is not null &&
            (!root.TryGetProperty("launchSessionId", out JsonElement sessionValue) ||
             sessionValue.ValueKind != JsonValueKind.String ||
             !string.Equals(sessionValue.GetString(), expectedLaunchSessionId, StringComparison.Ordinal)))
        {
            throw new InvalidDataException("Live resource result did not match the helper-owned launch session identity.");
        }
        if (expectedGamePid.HasValue &&
            (!root.TryGetProperty("gamePid", out JsonElement pidValue) ||
             !pidValue.TryGetInt32(out int resultPid) || resultPid != expectedGamePid.Value))
        {
            throw new InvalidDataException("Live resource result did not match the helper-owned game PID identity.");
        }
        const string currentViewRoute = "WorldPointManager.StartViewRequest+UpdateViewRequest(true)";
        const string targetedCityRoute = currentViewRoute + "+SendViewRequest(PlayerWorldPointId,currentLOD,currentServerId)";
        if (!root.TryGetProperty("requestRoute", out JsonElement routeValue) || routeValue.ValueKind != JsonValueKind.String)
            throw new InvalidDataException("Live map result did not prove a recovered current-view request route.");
        string? requestRoute = routeValue.GetString();
        bool supportedRoute = mapKind == "resource"
            ? requestRoute == currentViewRoute
            : requestRoute is currentViewRoute or targetedCityRoute;
        if (!supportedRoute)
            throw new InvalidDataException($"Live {mapKind} result did not prove a supported recovered request route.");
        if (!root.TryGetProperty("source", out JsonElement sourceValue) ||
            sourceValue.ValueKind != JsonValueKind.String ||
            sourceValue.GetString() != "WorldPointManager._pointInfos")
        {
            throw new InvalidDataException("Live resource result did not identify the proven WorldPointManager._pointInfos source.");
        }
        if (!root.TryGetProperty("capturedAt", out JsonElement capturedAtValue) ||
            capturedAtValue.ValueKind != JsonValueKind.String ||
            !DateTimeOffset.TryParse(
                capturedAtValue.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset capturedAt))
        {
            throw new InvalidDataException("Live resource result did not contain a valid capture timestamp.");
        }
        if (operationStartedAtUtc.HasValue && capturedAt < operationStartedAtUtc.Value.AddSeconds(-2))
            throw new InvalidDataException("Live resource result predates the active app acquisition and is stale.");
        DateTimeOffset freshnessNow = nowUtc ?? DateTimeOffset.UtcNow;
        if (capturedAt > freshnessNow.AddSeconds(5))
            throw new InvalidDataException("Live resource result capture timestamp is implausibly in the future.");

        if (!root.TryGetProperty("point_records", out JsonElement records) || records.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Live map result is missing point_records.");
        string expectedPointKind = mapKind == "city" ? "player_base" : "resource_point";
        JsonElement? selectedPoint = null;
        foreach (JsonElement candidate in records.EnumerateArray())
        {
            if (candidate.ValueKind == JsonValueKind.Object && candidate.TryGetProperty("kind", out JsonElement kind) &&
                kind.ValueKind == JsonValueKind.String && kind.GetString() == expectedPointKind)
            { selectedPoint = candidate; break; }
        }
        if (!selectedPoint.HasValue || !selectedPoint.Value.TryGetProperty("serverId", out JsonElement serverValue) ||
            !serverValue.TryGetInt32(out int serverId) || serverId is < 1 or > 99999)
            throw new InvalidDataException($"Live {mapKind} result does not contain a supported positive serverId.");
        if (!selectedPoint.Value.TryGetProperty("source", out JsonElement pointSource) || pointSource.ValueKind != JsonValueKind.String ||
            pointSource.GetString() != "WorldPointManager._pointInfos")
            throw new InvalidDataException($"Live {mapKind} point does not identify the proven WorldPointManager._pointInfos source.");
        if (mapKind == "city" && (!selectedPoint.Value.TryGetProperty("pointType", out JsonElement pt) || !pt.TryGetInt32(out int pointType) || pointType != 6))
            throw new InvalidDataException("Live city result did not contain current-client player-base pointType 6.");
        if (expectedServerId.HasValue && serverId != expectedServerId.Value)
            throw new InvalidDataException($"Live {mapKind} result belongs to a different server than the active bounded session.");
        acquisitionOrdinal = root.TryGetProperty("acquisitionOrdinal", out JsonElement ordinalValue) &&
            ordinalValue.TryGetInt32(out int parsedOrdinal) && parsedOrdinal > 0 ? parsedOrdinal : null;
        return mapKind == "city" ? FirstLiveResultImporter.PrepareOneCity(resultBytes, resultPath) : FirstLiveResultImporter.PrepareOneResource(resultBytes, resultPath);
    }

    internal static FirstLivePreparedResource PrepareOneResource(
        ReadOnlyMemory<byte> diagnosticsBytes,
        string sourcePath)
    {
        if (diagnosticsBytes.IsEmpty)
            throw new InvalidDataException("First-live diagnostics bytes are empty.");
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("First-live diagnostics source path is required.", nameof(sourcePath));

        string normalizedSourcePath = Path.GetFullPath(sourcePath);
        string captureSha256 = Convert.ToHexString(SHA256.HashData(diagnosticsBytes.Span)).ToLowerInvariant();

        using JsonDocument document = JsonDocument.Parse(diagnosticsBytes);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("First-live diagnostics root must be a JSON object.");

        if (!root.TryGetProperty("capturedAt", out JsonElement capturedAtValue) ||
            capturedAtValue.ValueKind != JsonValueKind.String ||
            !DateTimeOffset.TryParse(
                capturedAtValue.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset capturedAt))
        {
            throw new InvalidDataException("First-live diagnostics are missing a valid capturedAt timestamp.");
        }

        if (!root.TryGetProperty("point_records", out JsonElement records) || records.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("First-live diagnostics are missing point_records.");

        JsonElement? selected = null;
        foreach (JsonElement candidate in records.EnumerateArray())
        {
            if (candidate.ValueKind != JsonValueKind.Object ||
                !candidate.TryGetProperty("kind", out JsonElement kind) ||
                kind.ValueKind != JsonValueKind.String ||
                !string.Equals(kind.GetString(), "resource_point", StringComparison.Ordinal))
            {
                continue;
            }

            // IMPLEMENTATION POLICY: this bounded replay importer currently
            // accepts positive Int32 identity values and non-negative Int32 map
            // coordinates. Tile zero is a valid current-world edge coordinate.
            // resource and present a later row as though the whole source had
            // been accepted.
            int candidateServerId = RequirePositiveInt32(candidate, "serverId");
            if (candidateServerId > 99999)
            {
                throw new InvalidDataException(
                    "First-live replay resource serverId must be within the recovered public Map Data range 1 through 99999.");
            }
            RequirePositiveInt32(candidate, "pointId");
            RequireNonNegativeInt32(candidate, "x");
            RequireNonNegativeInt32(candidate, "y");

            selected = candidate.Clone();
            break;
        }

        if (selected is null)
            throw new InvalidDataException("First-live diagnostics contain no resource_point with a usable server/point/coordinate identity.");

        JsonElement point = selected.Value;
        TryReadPositiveInt(point, "serverId", out int serverId);
        TryReadPositiveInt(point, "pointId", out int pointIndex);
        TryReadNonNegativeInt(point, "x", out int x);
        TryReadNonNegativeInt(point, "y", out int y);
        string recordKey = pointIndex.ToString(CultureInfo.InvariantCulture);
        long updatedAt = capturedAt.ToUnixTimeMilliseconds();

        JsonObject normalized = JsonNode.Parse(point.GetRawText())?.AsObject()
            ?? throw new InvalidDataException("Selected resource point could not be normalized as a JSON object.");
        normalized["sourceKind"] = "resource_point";
        // IMPLEMENTATION POLICY: this bounded first-live adapter maps the prior
        // read-only probe's resource_point category to LWBridge's recovered public
        // resource kind. Game-derived fields stay source-backed; unsupported fields
        // such as resourceNameKey remain absent rather than guessed.
        normalized["kind"] = "resource";
        normalized["recordKey"] = recordKey;
        normalized["pointIndex"] = pointIndex;
        normalized["updatedAt"] = updatedAt;
        // IMPLEMENTATION POLICY: the bounded current-view route marks occupancy
        // known only when its current ResPointInfo source successfully exposes both
        // recovered gatherMarchUuid and gatherUid fields and the probe records the
        // derived occupied boolean. Older captures and incomplete source reads stay
        // unknown rather than inheriting the original formatter's Idle fallback.
        bool sourceGatherOccupied = false;
        bool gatherOccupancyKnown =
            TryReadBool(point, "gatherOccupancyKnown", out bool sourceOccupancyKnown) &&
            sourceOccupancyKnown &&
            TryReadBool(point, "gatherOccupied", out sourceGatherOccupied);
        normalized["rebuildGatherOccupancyKnown"] = gatherOccupancyKnown;
        if (gatherOccupancyKnown)
            normalized["rebuildGatherOccupied"] = sourceGatherOccupied;
        string dataJson = normalized.ToJsonString(JsonOptions.Default);

        int? level = TryReadInt(point, "level", out int parsedLevel) ? parsedLevel : null;
        string? name = ReadNonEmptyString(point, "name");
        MapStoredRecord record = new(
            "resource",
            serverId,
            recordKey,
            pointIndex,
            Uuid: null,
            name,
            AllianceName: null,
            level,
            Quality: null,
            Power: null,
            Distance: null,
            ShieldEndTime: null,
            updatedAt,
            dataJson);

        FirstLiveResultImport import = new(
            serverId,
            recordKey,
            pointIndex,
            x,
            y,
            level,
            updatedAt,
            captureSha256,
            normalizedSourcePath,
            ReadNonEmptyString(root, "probeVersion"),
            ReadNonEmptyString(root, "sourceCaptureSha256"),
            dataJson);
        return new FirstLivePreparedResource(import, record);
    }

    internal static FirstLivePreparedResource PrepareOneCity(ReadOnlyMemory<byte> diagnosticsBytes, string sourcePath)
    {
        if (diagnosticsBytes.IsEmpty) throw new InvalidDataException("First-live city diagnostics bytes are empty.");
        string normalizedSourcePath = Path.GetFullPath(sourcePath);
        string captureSha256 = Convert.ToHexString(SHA256.HashData(diagnosticsBytes.Span)).ToLowerInvariant();
        using JsonDocument document = JsonDocument.Parse(diagnosticsBytes);
        JsonElement root = document.RootElement;
        if (!root.TryGetProperty("capturedAt", out JsonElement capturedAtValue) || capturedAtValue.ValueKind != JsonValueKind.String ||
            !DateTimeOffset.TryParse(capturedAtValue.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset capturedAt))
            throw new InvalidDataException("First-live city diagnostics are missing a valid capturedAt timestamp.");
        if (!root.TryGetProperty("point_records", out JsonElement records) || records.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("First-live city diagnostics are missing point_records.");
        JsonElement? selected = null;
        foreach (JsonElement candidate in records.EnumerateArray())
            if (candidate.ValueKind == JsonValueKind.Object && candidate.TryGetProperty("kind", out JsonElement kind) && kind.ValueKind == JsonValueKind.String && kind.GetString() == "player_base") { selected = candidate.Clone(); break; }
        if (!selected.HasValue) throw new InvalidDataException("First-live city diagnostics contain no player_base point.");
        JsonElement point=selected.Value;
        int serverId=RequirePositiveInt32(point,"serverId"), pointIndex=RequirePositiveInt32(point,"pointId");
        int x=RequireNonNegativeInt32(point,"x"), y=RequireNonNegativeInt32(point,"y");
        if (serverId>99999) throw new InvalidDataException("First-live city serverId is outside the public Map Data range.");
        string ownerUid=ReadNonEmptyString(point,"ownerUid") ?? throw new InvalidDataException("First-live city is missing ownerUid.");
        string ownerName=ReadNonEmptyString(point,"ownerName") ?? throw new InvalidDataException("First-live city is missing ownerName.");
        string recordKey=pointIndex.ToString(CultureInfo.InvariantCulture); long updatedAt=capturedAt.ToUnixTimeMilliseconds();
        JsonObject normalized=JsonNode.Parse(point.GetRawText())?.AsObject() ?? throw new InvalidDataException("Selected city point could not be normalized.");
        normalized["sourceKind"]="player_base"; normalized["kind"]="city"; normalized["recordKey"]=recordKey;
        normalized["pointIndex"]=pointIndex; normalized["ownerUid"]=ownerUid; normalized["ownerName"]=ownerName; normalized["updatedAt"]=updatedAt;
        string dataJson=normalized.ToJsonString(JsonOptions.Default);
        int? level=TryReadInt(point,"level",out int parsedLevel)?parsedLevel:null;
        string? uuid=ReadNonEmptyString(point,"uuid"), allianceName=ReadNonEmptyString(point,"allianceName");
        long? shieldEnd=TryReadLong(point,"protectEndTime",out long protectEnd)?protectEnd:null;
        MapStoredRecord record=new("city",serverId,recordKey,pointIndex,uuid,ownerName,allianceName,level,null,null,null,shieldEnd,updatedAt,dataJson);
        FirstLiveResultImport import=new(serverId,recordKey,pointIndex,x,y,level,updatedAt,captureSha256,normalizedSourcePath,
            ReadNonEmptyString(root,"probeVersion"),ReadNonEmptyString(root,"sourceCaptureSha256"),dataJson);
        return new FirstLivePreparedResource(import,record);
    }

    internal static IReadOnlyList<FirstLivePreparedResource> PrepareResourceSnapshot(
        ReadOnlyMemory<byte> diagnosticsBytes,
        string sourcePath) =>
        PrepareSnapshot(diagnosticsBytes, sourcePath, "resource_point", PrepareOneResource);

    internal static IReadOnlyList<FirstLivePreparedResource> PrepareCitySnapshot(
        ReadOnlyMemory<byte> diagnosticsBytes,
        string sourcePath) =>
        PrepareSnapshot(diagnosticsBytes, sourcePath, "player_base", PrepareOneCity);

    private static IReadOnlyList<FirstLivePreparedResource> PrepareSnapshot(
        ReadOnlyMemory<byte> diagnosticsBytes,
        string sourcePath,
        string pointKind,
        Func<ReadOnlyMemory<byte>, string, FirstLivePreparedResource> prepareOne)
    {
        if (diagnosticsBytes.IsEmpty)
            throw new InvalidDataException("Live candidate snapshot bytes are empty.");
        string captureSha256 = Convert.ToHexString(SHA256.HashData(diagnosticsBytes.Span)).ToLowerInvariant();
        using JsonDocument document = JsonDocument.Parse(diagnosticsBytes);
        JsonElement root = document.RootElement;
        if (!root.TryGetProperty("point_records", out JsonElement records) || records.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Live candidate snapshot is missing point_records.");

        var prepared = new List<FirstLivePreparedResource>();
        foreach (JsonElement candidate in records.EnumerateArray())
        {
            if (candidate.ValueKind != JsonValueKind.Object ||
                !candidate.TryGetProperty("kind", out JsonElement kind) ||
                kind.ValueKind != JsonValueKind.String ||
                !string.Equals(kind.GetString(), pointKind, StringComparison.Ordinal))
                continue;

            JsonObject singleRoot = JsonNode.Parse(root.GetRawText())?.AsObject()
                ?? throw new InvalidDataException("Live candidate snapshot root could not be normalized.");
            var singleRecords = new JsonArray();
            singleRecords.Add(JsonNode.Parse(candidate.GetRawText()));
            singleRoot["point_records"] = singleRecords;
            byte[] singleBytes = Encoding.UTF8.GetBytes(singleRoot.ToJsonString(JsonOptions.Default));
            FirstLivePreparedResource item = prepareOne(singleBytes, sourcePath);
            prepared.Add(new FirstLivePreparedResource(
                item.Import with { CaptureSha256 = captureSha256 },
                item.Record));
        }

        if (prepared.Count == 0)
            throw new InvalidDataException($"Live candidate snapshot contains no {pointKind} records.");
        return prepared;
    }

    private static int RequirePositiveInt32(JsonElement value, string propertyName)
    {
        if (!TryReadPositiveInt(value, propertyName, out int result))
        {
            throw new InvalidDataException(
                $"First-live replay resource {propertyName} must be a positive Int32 in the bounded LWB-R7-001 importer.");
        }
        return result;
    }

    private static int RequireNonNegativeInt32(JsonElement value, string propertyName)
    {
        if (!TryReadNonNegativeInt(value, propertyName, out int result))
            throw new InvalidDataException(
                $"First-live map coordinate {propertyName} must be a non-negative Int32.");
        return result;
    }

    private static bool TryReadPositiveInt(JsonElement value, string propertyName, out int result) =>
        TryReadInt(value, propertyName, out result) && result > 0;

    private static bool TryReadNonNegativeInt(JsonElement value, string propertyName, out int result) =>
        TryReadInt(value, propertyName, out result) && result >= 0;

    private static bool TryReadInt(JsonElement value, string propertyName, out int result)
    {
        result = 0;
        return value.TryGetProperty(propertyName, out JsonElement property) &&
               property.ValueKind == JsonValueKind.Number &&
               property.TryGetInt32(out result);
    }

    private static bool TryReadLong(JsonElement value, string propertyName, out long result)
    {
        result = 0;
        return value.TryGetProperty(propertyName, out JsonElement property) && property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out result);
    }

    private static bool TryReadBool(JsonElement value, string propertyName, out bool result)
    {
        result = false;
        if (!value.TryGetProperty(propertyName, out JsonElement property))
            return false;
        if (property.ValueKind == JsonValueKind.True)
        {
            result = true;
            return true;
        }
        if (property.ValueKind == JsonValueKind.False)
            return true;
        return false;
    }

    private static string? ReadNonEmptyString(JsonElement value, string propertyName)
    {
        if (!value.TryGetProperty(propertyName, out JsonElement property) || property.ValueKind != JsonValueKind.String)
            return null;
        string? result = property.GetString();
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }
}
