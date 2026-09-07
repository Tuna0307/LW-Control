using System.Diagnostics;
using System.Text.Json;

namespace LWControl.Core;

public sealed record LocalBridgeInspection(
    string RootDirectory,
    bool GameRunning,
    int? GameProcessId,
    DateTimeOffset? GameStartedAt,
    bool RootPresent,
    int PendingCommandCount,
    DateTimeOffset? HeartbeatAt,
    double? HeartbeatAgeSeconds,
    bool BridgeHealthy,
    string? DailyFreeClaimsVersion,
    string StatusCode);

public static class LocalBridgeInspector
{
    public const string RecoveredDailyFreeClaimsVersion = "lwc2-daily-free-claims-20";
    public static readonly TimeSpan HeartbeatFreshnessWindow = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan HeartbeatFutureTolerance = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan ProcessStartTolerance = TimeSpan.FromSeconds(2);

    public static string DefaultRootDirectory => Path.Combine(
        RuntimePaths.CanonicalLocalApplicationData, "LastWarControl");

    public static LocalBridgeInspection Inspect(string? rootDirectory = null, DateTimeOffset? checkedAt = null)
    {
        var root = Path.GetFullPath(rootDirectory ?? DefaultRootDirectory);
        var now = checkedAt ?? DateTimeOffset.UtcNow;
        var (gameRunning, processId, processStartedAt) = ReadGameProcess();
        bool rootPresent = Directory.Exists(root);
        int pending = rootPresent ? CountFiles(Path.Combine(root, "commands", "pending"), "slot-*.json") : 0;
        var heartbeatPath = Path.Combine(root, "runtime", "lua-heartbeat.json");
        var heartbeatAt = ReadHeartbeatWriteTime(heartbeatPath);
        var dailyVersion = ReadDailyFreeClaimsVersion(heartbeatPath);
        bool healthy = IsBridgeHealthy(gameRunning, processStartedAt, heartbeatAt, now);
        double? ageSeconds = heartbeatAt.HasValue ? (now - heartbeatAt.Value).TotalSeconds : null;

        string status = !gameRunning ? "game_not_running"
            : !rootPresent ? "bridge_root_missing"
            : !heartbeatAt.HasValue ? "heartbeat_missing"
            : !healthy ? "heartbeat_stale_or_uncorrelated"
            : !string.Equals(dailyVersion, RecoveredDailyFreeClaimsVersion, StringComparison.Ordinal)
                ? "daily_free_claims_version_mismatch"
                : "ready_read_only";

        return new(root, gameRunning, processId, processStartedAt, rootPresent, pending,
            heartbeatAt, ageSeconds, healthy, dailyVersion, status);
    }

    public static bool IsBridgeHealthy(bool gameRunning, DateTimeOffset? processStartedAt,
        DateTimeOffset? heartbeatAt, DateTimeOffset checkedAt)
    {
        if (!gameRunning || !heartbeatAt.HasValue) return false;
        if (heartbeatAt.Value > checkedAt + HeartbeatFutureTolerance) return false;
        if (checkedAt - heartbeatAt.Value > HeartbeatFreshnessWindow) return false;
        return !processStartedAt.HasValue
            || heartbeatAt.Value >= processStartedAt.Value - ProcessStartTolerance;
    }

    private static (bool Running, int? ProcessId, DateTimeOffset? StartedAt) ReadGameProcess()
    {
        var processes = Process.GetProcessesByName("LastWar");
        try
        {
            Process? selected = null;
            DateTimeOffset? selectedStartedAt = null;
            foreach (var process in processes)
            {
                DateTimeOffset? startedAt = TryReadStartTime(process);
                if (selected is null || (startedAt.HasValue &&
                    (!selectedStartedAt.HasValue || startedAt > selectedStartedAt)))
                {
                    selected = process;
                    selectedStartedAt = startedAt;
                }
            }
            return selected is null ? (false, null, null) : (true, selected.Id, selectedStartedAt);
        }
        finally
        {
            foreach (var process in processes) process.Dispose();
        }
    }

    private static DateTimeOffset? TryReadStartTime(Process process)
    {
        try { return process.StartTime.ToUniversalTime(); }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception
            or NotSupportedException) { return null; }
    }

    private static int CountFiles(string directory, string pattern)
    {
        try { return Directory.Exists(directory) ? Directory.EnumerateFiles(directory, pattern).Count() : 0; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return 0; }
    }

    private static DateTimeOffset? ReadHeartbeatWriteTime(string path)
    {
        try
        {
            return File.Exists(path)
                ? new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero)
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or NotSupportedException) { return null; }
    }

    private static string? ReadDailyFreeClaimsVersion(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var document = JsonDocument.Parse(stream);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("daily_free_claims", out var value)
                && value.ValueKind == JsonValueKind.String
                    ? value.GetString()
                    : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }
}
