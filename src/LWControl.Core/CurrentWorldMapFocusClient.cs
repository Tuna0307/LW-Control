using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace LWControl.Core;

public sealed record CurrentWorldMapFocusResult(
    string CommandId,
    int X,
    int Y,
    double ObservedX,
    double ObservedY,
    string Route,
    DateTimeOffset CompletedAt);

public sealed class CurrentWorldMapFocusClient
{
    public const string ExpectedProbeVersion = "lwcontrol-world-full-scan-probe-9";
    public static readonly TimeSpan HeartbeatFreshnessWindow = TimeSpan.FromSeconds(5);
    private readonly string root;
    private readonly SemaphoreSlim gate = new(1, 1);

    public CurrentWorldMapFocusClient(string? rootDirectory = null)
    {
        root = Path.GetFullPath(rootDirectory ?? RuntimePaths.LWControlRuntimeDirectory);
    }

    public static string BuildCommandText(string commandId, CurrentWorldMapScanRecord record)
    {
        if (!IsSafeCommandId(commandId)) throw new ArgumentException("World-map focus command ID is invalid.", nameof(commandId));
        if (record.X is < 0 or > 999 || record.Y is < 0 or > 999)
            throw new ArgumentOutOfRangeException(nameof(record), "World-map focus coordinates are outside the normal map.");
        return $"schema=1\ncommandId={commandId}\nx={record.X}\ny={record.Y}\nserverId={record.ServerId}\npointId={record.PointId}\n";
    }

    public async Task<CurrentWorldMapFocusResult> FocusAsync(
        CurrentWorldMapScanRecord record,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        TimeSpan wait = timeout ?? TimeSpan.FromSeconds(8);
        if (wait < TimeSpan.FromSeconds(2) || wait > TimeSpan.FromSeconds(20))
            throw new ArgumentOutOfRangeException(nameof(timeout));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!Process.GetProcessesByName("LastWar").Any())
                throw new InvalidOperationException("Last War is not running. Run World Scan first and keep the game open.");
            VerifyFreshScanRuntime();
            Directory.CreateDirectory(root);
            string commandId = $"focus-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
            string commandPath = Path.Combine(root, "world-map-focus-command.txt");
            string resultPath = Path.Combine(root, "world-map-focus-result.json");
            if (File.Exists(commandPath))
                throw new InvalidOperationException("Another World Map locate command is still pending.");
            File.Delete(resultPath);
            WriteAtomic(commandPath, BuildCommandText(commandId, record));

            var deadline = DateTimeOffset.UtcNow + wait;
            while (DateTimeOffset.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (File.Exists(resultPath))
                {
                    var result = ReadResult(resultPath, commandId, record);
                    File.Delete(resultPath);
                    return result;
                }
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
            throw new TimeoutException("Last War did not return verified camera coordinates for the selected map target.");
        }
        finally
        {
            gate.Release();
        }
    }

    private void VerifyFreshScanRuntime()
    {
        string heartbeatPath = Path.Combine(root, "world-map-full-scan-heartbeat.json");
        if (!File.Exists(heartbeatPath))
            throw new InvalidOperationException("The World Scan runtime is not loaded in the game.");
        using var stream = new FileStream(heartbeatPath, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var document = JsonDocument.Parse(stream);
        var value = document.RootElement;
        string? version = value.TryGetProperty("version", out var versionElement)
            && versionElement.ValueKind == JsonValueKind.String ? versionElement.GetString() : null;
        long? updated = value.TryGetProperty("updated_at", out var updatedElement)
            && updatedElement.TryGetInt64(out long unix) ? unix : null;
        if (!string.Equals(version, ExpectedProbeVersion, StringComparison.Ordinal) || !updated.HasValue)
            throw new InvalidOperationException("The loaded World Scan runtime does not match this desktop build.");
        var observed = DateTimeOffset.FromUnixTimeSeconds(updated.Value);
        if (DateTimeOffset.UtcNow - observed > HeartbeatFreshnessWindow
            || observed > DateTimeOffset.UtcNow + TimeSpan.FromSeconds(2))
            throw new InvalidOperationException("The World Scan runtime heartbeat is stale. Run World Scan again.");
    }

    private static CurrentWorldMapFocusResult ReadResult(
        string path, string commandId, CurrentWorldMapScanRecord record)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1
            || !string.Equals(root.GetProperty("commandId").GetString(), commandId, StringComparison.Ordinal))
            throw new InvalidDataException("World Map locate result does not match the pending command.");
        string state = root.GetProperty("state").GetString() ?? "";
        if (state != "completed")
        {
            string error = root.TryGetProperty("error", out var errorElement)
                ? errorElement.GetString() ?? "unknown error" : "unknown error";
            throw new InvalidOperationException($"World Map locate failed: {error}");
        }
        int x = root.GetProperty("x").GetInt32();
        int y = root.GetProperty("y").GetInt32();
        double observedX = root.GetProperty("observedX").GetDouble();
        double observedY = root.GetProperty("observedY").GetDouble();
        if (x != record.X || y != record.Y
            || Math.Max(Math.Abs(observedX - x), Math.Abs(observedY - y)) > 3)
            throw new InvalidDataException("World Map locate result did not prove the selected camera coordinates.");
        string route = root.TryGetProperty("route", out var routeElement)
            && routeElement.ValueKind == JsonValueKind.String ? routeElement.GetString() ?? "unknown" : "unknown";
        long completedUnix = root.GetProperty("completedAt").GetInt64();
        return new(commandId, x, y, observedX, observedY, route,
            DateTimeOffset.FromUnixTimeSeconds(completedUnix));
    }

    private static void WriteAtomic(string path, string text)
    {
        string directory = Path.GetDirectoryName(path)!;
        string temporary = Path.Combine(directory, $".world-focus-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporary, text, new UTF8Encoding(false));
            File.Move(temporary, path, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static bool IsSafeCommandId(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 128
        && char.IsAsciiLetterOrDigit(value[0])
        && value.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_');
}
