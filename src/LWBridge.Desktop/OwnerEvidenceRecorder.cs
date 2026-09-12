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

internal static class OwnerEvidenceResourceContract
{
    private static readonly HashSet<string> BlockedOwnerCommands = new(StringComparer.Ordinal)
    {
        "game_root_select", "profile_create", "profile_delete", "profile_instance_start",
        "profile_instance_stop", "profile_instances_reconcile", "profile_instances_update_and_restart",
        "set_automation", "automation_configure", "local_config_set", "call_lua",
        "map_scan_start", "map_scan_stop", "map_scan_clear", "map_player_mark_set",
        "map_city_export", "server_jump", "server_jump_history_set", "server_jump_history_import",
        "map_coordinate_jump", "map_march_follow", "map_treasure_state_refresh",
        "map_treasure_state_refresh_all", "map_treasure_claim",
        "map_dispatch_plunder_schedule", "map_dispatch_plunder_cancel", "map_dispatch_share_alliance",
        "map_truck_plunder_schedule", "map_truck_plunder_cancel"
    };

    internal static bool IsBlockedOwnerCommand(string command) => BlockedOwnerCommands.Contains(command);

    internal static bool IsResourceSearch(JsonElement payload)
    {
        try { return MapDataQueryContract.NormalizeSearch(payload).Kind == "resource"; }
        catch { return false; }
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
            !row.IsEmpty && row.Cells.Count == 5 &&
            row.Cells[0] == coordinate && row.Cells[2] == level && row.Cells[4] == expectedUpdatedText &&
            !string.IsNullOrWhiteSpace(row.Cells[1]) && !string.IsNullOrWhiteSpace(row.Cells[3]));
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
