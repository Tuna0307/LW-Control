using System.Text;
using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed record OwnerEvidenceResourceTarget(
    int ServerId,
    string RecordKey,
    int? PointIndex,
    int X,
    int Y,
    int? Level,
    long UpdatedAt);

internal sealed record OwnerEvidenceCityTableRow(
    bool IsEmpty,
    string Coordinate,
    string Level,
    string UpdatedAt);

internal sealed record OwnerEvidenceCityTableSnapshot(
    bool Busy,
    IReadOnlyList<OwnerEvidenceCityTableRow> Rows);

internal static class OwnerEvidenceResourceContract
{
    private static readonly HashSet<string> BlockedOwnerCommands = new(StringComparer.Ordinal)
    {
        "game_root_select", "profile_create", "profile_delete", "profile_instance_start",
        "profile_instance_stop", "profile_instances_reconcile", "profile_instances_update_and_restart",
        "set_automation", "automation_configure", "local_config_set", "call_lua",
        "map_scan_start", "map_scan_stop", "map_scan_clear", "map_player_mark_set",
        "server_jump", "server_jump_history_set", "server_jump_history_import",
        "map_coordinate_jump", "map_march_follow", "map_treasure_claim",
        "map_dispatch_share_alliance"
    };

    internal static bool IsBlockedOwnerCommand(string command) => BlockedOwnerCommands.Contains(command);

    internal static bool IsResourceSearch(JsonElement payload)
    {
        try { return MapDataQueryContract.NormalizeSearch(payload).Kind == "resource"; }
        catch { return false; }
    }

    internal static bool IsCitySearch(JsonElement payload)
    {
        try { return MapDataQueryContract.NormalizeSearch(payload).Kind == "city"; }
        catch { return false; }
    }

    internal static object SanitizeCitySearchPayload(JsonElement payload)
    {
        MapDataQueryOptions query = MapDataQueryContract.NormalizeSearch(payload);
        return new
        {
            kind = query.Kind,
            query = new { serverId = query.ServerId, page = query.Page, pageSize = query.PageSize, sorts = query.Sorts }
        };
    }

    // This City snapshot deliberately never reads the player/alliance cells.
    internal const string CityTableSnapshotScript = """
        (() => {
          const table = document.querySelector('.map-table--city');
          if (!table) return null;
          return {
            busy: table.getAttribute('aria-busy') === 'true',
            rows: [...table.querySelectorAll('tbody tr')].map(row => {
              const cells = [...row.querySelectorAll('td')];
              return {
                isEmpty: !!row.querySelector('td.map-empty'),
                coordinate: (cells[1]?.querySelector('.map-coordinate-button span:not(.map-coordinate-icon)')?.textContent || cells[1]?.innerText || '').trim(),
                level: (cells[4]?.innerText || '').trim(),
                updatedAt: (cells[7]?.innerText || '').trim()
              };
            })
          };
        })()
        """;

    internal static bool IsCityCorrelated(
        OwnerEvidenceResourceTarget? target,
        bool resultIsEmpty,
        OwnerEvidenceCityTableSnapshot snapshot,
        string? expectedUpdatedText)
    {
        if (snapshot.Busy) return false;
        if (target is null)
            return resultIsEmpty && snapshot.Rows.Count > 0 && snapshot.Rows.All(row => row.IsEmpty);
        if (string.IsNullOrWhiteSpace(expectedUpdatedText)) return false;
        string coordinate = $"{target.X},{target.Y}";
        string level = target.Level?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-";
        return snapshot.Rows.Any(row =>
            !row.IsEmpty && row.Coordinate == coordinate && row.Level == level && row.UpdatedAt == expectedUpdatedText);
    }

    internal static object SanitizeCitySearchResult(object? result)
    {
        JsonElement root = JsonSerializer.SerializeToElement(result, JsonOptions.Default);
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("rows", out JsonElement rows) || rows.ValueKind != JsonValueKind.Array)
            return new { rows = Array.Empty<object>(), total = 0 };
        var sanitized = rows.EnumerateArray()
            .Where(row => row.ValueKind == JsonValueKind.Object)
            .Select(row => (object)new
            {
                serverId = Int32(row, "serverId"),
                recordKey = String(row, "recordKey"),
                pointIndex = Int32(row, "pointIndex"),
                x = Int32(row, "x"),
                y = Int32(row, "y"),
                level = NullableInt32(row, "level") is int level && level != int.MinValue ? level : (int?)null,
                updatedAt = Int64(row, "updatedAt"),
            })
            .ToArray();
        int total = root.TryGetProperty("total", out JsonElement totalValue) && totalValue.TryGetInt32(out int parsedTotal)
            ? parsedTotal : sanitized.Length;
        return new { rows = sanitized, total };
    }

    internal static OwnerEvidenceResourceTarget? TryGetFirstTarget(JsonElement result)
    {
        if (result.ValueKind != JsonValueKind.Object ||
            !result.TryGetProperty("rows", out JsonElement rows) || rows.ValueKind != JsonValueKind.Array)
            return null;
        JsonElement row = rows.EnumerateArray().FirstOrDefault();
        if (row.ValueKind != JsonValueKind.Object) return null;
        int? serverId = Int32(row, "serverId");
        string? recordKey = String(row, "recordKey");
        int? x = Int32(row, "x");
        int? y = Int32(row, "y");
        long? updatedAt = Int64(row, "updatedAt");
        if (serverId is null || recordKey is null || x is null || y is null || updatedAt is null) return null;
        return new OwnerEvidenceResourceTarget(
            serverId.Value, recordKey, Int32(row, "pointIndex"), x.Value, y.Value,
            NullableInt32(row, "level"), updatedAt.Value);
    }

    internal static bool IsEmptyResult(JsonElement result) =>
        result.ValueKind == JsonValueKind.Object &&
        result.TryGetProperty("rows", out JsonElement rows) && rows.ValueKind == JsonValueKind.Array && rows.GetArrayLength() == 0 &&
        result.TryGetProperty("total", out JsonElement total) && total.TryGetInt32(out int count) && count == 0;

    internal static bool IsCorrelated(
        OwnerEvidenceResourceTarget? target,
        bool resultIsEmpty,
        NormalUiResourceProofTableSnapshot snapshot,
        string? expectedUpdatedText)
    {
        if (snapshot.Busy) return false;
        if (target is null)
            return resultIsEmpty && snapshot.Rows.Count > 0 && snapshot.Rows.All(row => row.IsEmpty);
        if (string.IsNullOrWhiteSpace(expectedUpdatedText)) return false;
        string coordinate = $"{target.X},{target.Y}";
        string level = target.Level?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-";
        return snapshot.Rows.Any(row =>
            !row.IsEmpty && row.Cells.Count is 5 or 6 &&
            row.Cells[0] == coordinate && row.Cells[2] == level &&
            row.Cells[^1] == expectedUpdatedText &&
            !string.IsNullOrWhiteSpace(row.Cells[1]) &&
            !string.IsNullOrWhiteSpace(row.Cells[3]) &&
            (row.Cells.Count == 5 || !string.IsNullOrWhiteSpace(row.Cells[4])));
    }

    private static int? Int32(JsonElement row, string name) =>
        row.TryGetProperty(name, out JsonElement value) && value.TryGetInt32(out int parsed) ? parsed : null;
    private static int? NullableInt32(JsonElement row, string name) =>
        !row.TryGetProperty(name, out JsonElement value) || value.ValueKind == JsonValueKind.Null
            ? null : value.TryGetInt32(out int parsed) ? parsed : int.MinValue;
    private static long? Int64(JsonElement row, string name) =>
        row.TryGetProperty(name, out JsonElement value) && value.TryGetInt64(out long parsed) ? parsed : null;
    private static string? String(JsonElement row, string name) =>
        row.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}

internal sealed class OwnerEvidenceRecorder : IDisposable
{
    private readonly object gate = new();
    private readonly StreamWriter writer;
    private bool disposed;

    public OwnerEvidenceRecorder(string directory)
    {
        directory = Path.GetFullPath(directory);
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"ui-session-{Environment.ProcessId}.jsonl");
        writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read), new UTF8Encoding(false))
        {
            AutoFlush = true
        };
    }

    public void Record(string eventType, object details) => Write(new
    {
        timestampUtc = DateTimeOffset.UtcNow,
        eventType,
        details
    });

    public void RecordSearch(string requestId, JsonElement payload, object? result) => Write(new
    {
        timestampUtc = DateTimeOffset.UtcNow,
        eventType = "resource-search-response",
        requestId,
        payload,
        result
    });

    public void RecordCitySearch(string requestId, JsonElement payload, object? result) => Write(new
    {
        timestampUtc = DateTimeOffset.UtcNow,
        eventType = "city-search-response",
        requestId,
        payload = OwnerEvidenceResourceContract.SanitizeCitySearchPayload(payload),
        result = OwnerEvidenceResourceContract.SanitizeCitySearchResult(result)
    });

    public void RecordCityRender(
        string requestId,
        JsonElement payload,
        object sanitizedResult,
        OwnerEvidenceCityTableSnapshot? snapshot,
        bool correlated,
        string? reason,
        OwnerEvidenceResourceTarget? target,
        string? expectedUpdatedText) => Write(new
    {
        timestampUtc = DateTimeOffset.UtcNow,
        eventType = "city-render-observation",
        requestId,
        correlated,
        reason,
        target,
        expectedUpdatedText,
        snapshot,
        payload = OwnerEvidenceResourceContract.SanitizeCitySearchPayload(payload),
        result = sanitizedResult
    });

    public void RecordCommandError(string requestId, string command, string code, string message, object? details) => Write(new
    {
        timestampUtc = DateTimeOffset.UtcNow,
        eventType = "command-error",
        requestId,
        command,
        code,
        message,
        details
    });

    public void RecordRender(
        string requestId,
        JsonElement payload,
        JsonElement result,
        NormalUiResourceProofTableSnapshot? snapshot,
        bool correlated,
        string? reason,
        OwnerEvidenceResourceTarget? target,
        string? expectedUpdatedText) => Write(new
    {
        timestampUtc = DateTimeOffset.UtcNow,
        eventType = "resource-render-observation",
        requestId,
        correlated,
        reason,
        target,
        expectedUpdatedText,
        snapshot,
        payload,
        result
    });

    private void Write(object value)
    {
        string line = JsonSerializer.Serialize(value, JsonOptions.Default);
        lock (gate)
        {
            if (disposed) return;
            writer.WriteLine(line);
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            writer.Dispose();
        }
    }
}
