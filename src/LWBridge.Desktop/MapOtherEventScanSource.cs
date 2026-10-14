using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LWBridge.Desktop;

// One native Treasure-point census supplies both independent output categories.
// No second camera traversal, event guess, claim eligibility, or parallel scan.
internal sealed class MapOtherEventScanSource : IMapScanProgressBatchSource
{
    private readonly IMapScanBlockSource source;

    internal MapOtherEventScanSource(IMapScanBlockSource source) =>
        this.source = source ?? throw new ArgumentNullException(nameof(source));

    private static MapScanExecutionRequest NativeRequest(MapScanExecutionRequest request)
    {
        if (!request.SelectedTypes.Contains("other", StringComparer.Ordinal)) return request;
        return request with
        {
            SelectedTypes = request.SelectedTypes
                .Select(kind => kind == "other" ? "treasure" : kind)
                .Distinct(StringComparer.Ordinal).ToArray(),
        };
    }

    public async Task<MapScanBlockCapture> CaptureAsync(
        MapScanExecutionRequest request, MapScanTargetBlock block, CancellationToken cancellationToken)
    {
        MapScanBlockCapture capture = await source.CaptureAsync(NativeRequest(request), block, cancellationToken)
            .ConfigureAwait(false);
        return Partition(capture, request);
    }

    public Task<IReadOnlyList<MapScanBlockCapture>> CaptureBatchAsync(
        MapScanExecutionRequest request, MapScanTargetBlock seedBlock,
        IReadOnlySet<int> pendingBlockIndices, CancellationToken cancellationToken) =>
        CaptureBatchAsync(request, seedBlock, pendingBlockIndices, null, cancellationToken);

    public async Task<IReadOnlyList<MapScanBlockCapture>> CaptureBatchAsync(
        MapScanExecutionRequest request, MapScanTargetBlock seedBlock,
        IReadOnlySet<int> pendingBlockIndices, Action<MapScanSourceProgress>? progress,
        CancellationToken cancellationToken)
    {
        MapScanExecutionRequest native = NativeRequest(request);
        IReadOnlyList<MapScanBlockCapture> captures = source switch
        {
            IMapScanProgressBatchSource progressive =>
                await progressive.CaptureBatchAsync(native, seedBlock, pendingBlockIndices, progress, cancellationToken)
                    .ConfigureAwait(false),
            IMapScanBatchSource batch =>
                await batch.CaptureBatchAsync(native, seedBlock, pendingBlockIndices, cancellationToken)
                    .ConfigureAwait(false),
            _ => [await source.CaptureAsync(native, seedBlock, cancellationToken).ConfigureAwait(false)],
        };
        cancellationToken.ThrowIfCancellationRequested();
        return captures.Select(capture => Partition(capture, request)).ToArray();
    }

    internal static MapScanBlockCapture Partition(MapScanBlockCapture capture, MapScanExecutionRequest request)
    {
        if (!request.SelectedTypes.Contains("treasure", StringComparer.Ordinal) &&
            !request.SelectedTypes.Contains("other", StringComparer.Ordinal))
            return capture;

        var records = new List<MapStoredRecord>(capture.Records.Count);
        foreach (MapStoredRecord record in capture.Records)
        {
            if (record.Kind != "treasure")
            {
                records.Add(record);
                continue;
            }

            string? subtype = ClassifySubtype(record.DataJson);
            if (subtype is null)
            {
                // Unsupported supplies variants are not Treasure claim targets.
                // Retain only confirmed original Treasure identities here.
                if (!IsNativeSuppliesPoint(record.DataJson) &&
                    request.SelectedTypes.Contains("treasure", StringComparer.Ordinal))
                    records.Add(record);
            }
            else if (request.SelectedTypes.Contains("other", StringComparer.Ordinal))
            {
                string? nameKey = NameKey(subtype);
                JsonObject data = JsonNode.Parse(record.DataJson)?.AsObject()
                    ?? throw new InvalidDataException("Other event record has invalid JSON.");
                data["kind"] = "other";
                data["otherSubtype"] = subtype;
                // Only type 1 and event 27000 have verified current-game names.
                // Preserve every other config-derived supplies type as visible
                // Other with an explicit unavailable-name fallback in the UI.
                if (nameKey is null) data.Remove("otherNameKey");
                else data["otherNameKey"] = nameKey;
                records.Add(record with { Kind = "other", DataJson = data.ToJsonString(JsonOptions.Default) });
            }
        }
        string payload = capture.PayloadJson;
        if (payload.Contains("\"recordsInBlock\"", StringComparison.Ordinal))
        {
            JsonObject? metadata = JsonNode.Parse(payload) as JsonObject;
            if (metadata is not null && metadata.ContainsKey("recordsInBlock"))
            {
                metadata["recordsInBlock"] = records.Count;
                payload = metadata.ToJsonString(JsonOptions.Default);
            }
        }
        return capture with { Records = records, PayloadJson = payload };
    }

    internal static string? ClassifySubtype(string dataJson)
    {
        using JsonDocument document = JsonDocument.Parse(dataJson);
        JsonElement row = document.RootElement;
        if (row.ValueKind != JsonValueKind.Object) return null;
        int? pointType = ReadInteger(row, "pointType");
        string runtimeClass = row.TryGetProperty("runtimeClass", out JsonElement cls) &&
            cls.ValueKind == JsonValueKind.String ? cls.GetString() ?? string.Empty : string.Empty;
        if (pointType == 27 && runtimeClass.EndsWith("WorldSuppliesPoint", StringComparison.Ordinal) &&
            ReadInteger(row, "treasureType") == 0)
        {
            int? suppliedType = ReadInteger(row, "suppliesType");
            return suppliedType is > 0
                ? "supplies:" + suppliedType.Value.ToString(CultureInfo.InvariantCulture)
                : null;
        }
        return pointType == 21 &&
            runtimeClass.EndsWith("TreasurePointInfo", StringComparison.Ordinal) &&
            ReadInteger(row, "suppliesType") == 0 &&
            ReadInteger(row, "treasureType") is > 0 &&
            ReadInteger(row, "eventId") == 27000
                ? "event:27000" : null;
    }

    internal static string? NameKey(string subtype) => subtype switch
    {
        "supplies:1" => "season_s2_ice_supplies_11",
        "event:27000" => "activity_wajueji_27000_name",
        _ when LWBridge.Map317.MapKinds.IsValidOtherSubtype(subtype) => null,
        _ => throw new ArgumentOutOfRangeException(nameof(subtype)),
    };

    private static bool IsNativeSuppliesPoint(string dataJson)
    {
        using JsonDocument document = JsonDocument.Parse(dataJson);
        JsonElement row = document.RootElement;
        return row.ValueKind == JsonValueKind.Object && ReadInteger(row, "pointType") == 27;
    }

    private static int? ReadInteger(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out JsonElement value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number))
            return number;
        if (value.ValueKind == JsonValueKind.String &&
            int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            return parsed;
        return null;
    }
}
