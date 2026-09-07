using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace LWControl.Core;

public sealed record CurrentWorldMapScanRun
{
    public required string CandidateDirectory { get; init; }
    public required string LiveResultPath { get; init; }
    public required bool GameLeftRunning { get; init; }
    public required string RestoreMode { get; init; }
    public required CurrentWorldMapFullScanResult Result { get; init; }
}

public sealed record CurrentWorldMapRuntimeInspection(
    string RootDirectory,
    bool HeartbeatPresent,
    bool HeartbeatFresh,
    string? RuntimeVersion,
    DateTimeOffset? HeartbeatAt,
    string? RegistrationMethod,
    string StatusCode);

public sealed class CurrentWorldMapScanClient
{
    public const string ExpectedRuntimeVersion = CurrentWorldMapFocusClient.ExpectedProbeVersion;
    public static readonly TimeSpan HeartbeatFreshnessWindow = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan HeartbeatFutureTolerance = TimeSpan.FromSeconds(5);
    private readonly string root;
    private readonly SemaphoreSlim gate = new(1, 1);

    public CurrentWorldMapScanClient(string? rootDirectory = null)
    {
        root = Path.GetFullPath(rootDirectory ?? RuntimePaths.LWControlRuntimeDirectory);
    }

    public CurrentWorldMapRuntimeInspection Inspect(DateTimeOffset? checkedAt = null)
    {
        var now = checkedAt ?? DateTimeOffset.UtcNow;
        string heartbeatPath = Path.Combine(root, "world-map-full-scan-heartbeat.json");
        if (!File.Exists(heartbeatPath))
            return new(root, false, false, null, null, null, "heartbeat_missing");
        try
        {
            using var stream = OpenSharedRead(heartbeatPath);
            using var document = JsonDocument.Parse(stream);
            var value = document.RootElement;
            string? version = value.TryGetProperty("version", out var versionElement)
                && versionElement.ValueKind == JsonValueKind.String ? versionElement.GetString() : null;
            string? registration = value.TryGetProperty("registrationMethod", out var registrationElement)
                && registrationElement.ValueKind == JsonValueKind.String ? registrationElement.GetString() : null;
            bool persistent = value.TryGetProperty("persistent", out var persistentElement)
                && persistentElement.ValueKind == JsonValueKind.True;
            DateTimeOffset? observed = null;
            if (value.TryGetProperty("updated_at", out var timeElement) && timeElement.TryGetInt64(out long unix))
                observed = DateTimeOffset.FromUnixTimeSeconds(unix);
            bool fresh = observed.HasValue
                && observed.Value <= now + HeartbeatFutureTolerance
                && now - observed.Value <= HeartbeatFreshnessWindow;
            string status = !string.Equals(version, ExpectedRuntimeVersion, StringComparison.Ordinal)
                ? "runtime_version_mismatch"
                : !persistent ? "temporary_runtime_loaded"
                : !fresh ? "heartbeat_stale" : "ready";
            return new(root, true, fresh, version, observed, registration, status);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentOutOfRangeException)
        {
            return new(root, true, false, null, null, null, "heartbeat_invalid");
        }
    }

    public async Task<CurrentWorldMapScanRun> RunAsync(
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        TimeSpan wait = timeout ?? TimeSpan.FromMinutes(12);
        if (wait < TimeSpan.FromSeconds(30) || wait > TimeSpan.FromMinutes(20))
            throw new ArgumentOutOfRangeException(nameof(timeout), "World Scan timeout must be between 30 seconds and 20 minutes.");

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!Process.GetProcessesByName("LastWar").Any())
                throw new InvalidOperationException("Last War is not running. Start the game before World Scan.");
            var inspection = Inspect();
            if (inspection.StatusCode != "ready")
                throw new InvalidOperationException($"Persistent World Scan runtime is not ready ({inspection.StatusCode}).");

            Directory.CreateDirectory(root);
            string commandId = $"world-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
            string commandPath = Path.Combine(root, "world-map-scan-command.txt");
            string resultPath = Path.Combine(root, "world-map-full-scan-result.json");
            string statusPath = Path.Combine(root, "world-map-full-scan-status.json");
            if (File.Exists(commandPath))
                throw new InvalidOperationException("Another World Scan command is already pending.");
            File.Delete(resultPath);
            WriteAtomic(commandPath, BuildCommandText(commandId));

            var deadline = DateTimeOffset.UtcNow + wait;
            while (DateTimeOffset.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (File.Exists(resultPath) && ResultMatchesCommand(resultPath, commandId))
                {
                    var result = CurrentWorldMapFullScanResult.Read(resultPath);
                    return new()
                    {
                        CandidateDirectory = root,
                        LiveResultPath = resultPath,
                        GameLeftRunning = Process.GetProcessesByName("LastWar").Any(),
                        RestoreMode = "persistent_runtime",
                        Result = result,
                    };
                }
                if (TryReadTerminalFailure(statusPath, commandId, out string? error))
                    throw new InvalidOperationException($"World Scan runtime failed: {error}");
                await Task.Delay(250, cancellationToken).ConfigureAwait(false);
            }
            throw new TimeoutException("Persistent World Scan runtime did not produce a correlated result before the timeout.");
        }
        finally
        {
            gate.Release();
        }
    }

    public static string BuildCommandText(string commandId)
    {
        if (!IsSafeCommandId(commandId)) throw new ArgumentException("Command ID is invalid.", nameof(commandId));
        return $"schema=1\ncommandId={commandId}\nmode=run_once\n";
    }

    private static bool ResultMatchesCommand(string path, string commandId)
    {
        try
        {
            using var stream = OpenSharedRead(path);
            using var document = JsonDocument.Parse(stream);
            return document.RootElement.TryGetProperty("commandId", out var element)
                && element.ValueKind == JsonValueKind.String
                && string.Equals(element.GetString(), commandId, StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
    }

    private static bool TryReadTerminalFailure(string path, string commandId, out string? error)
    {
        error = null;
        if (!File.Exists(path)) return false;
        try
        {
            using var stream = OpenSharedRead(path);
            using var document = JsonDocument.Parse(stream);
            var value = document.RootElement;
            if (!value.TryGetProperty("commandId", out var commandElement)
                || !string.Equals(commandElement.GetString(), commandId, StringComparison.Ordinal)
                || !value.TryGetProperty("completed", out var completedElement)
                || completedElement.ValueKind != JsonValueKind.True)
                return false;
            string state = value.TryGetProperty("state", out var stateElement)
                ? stateElement.GetString() ?? "unknown" : "unknown";
            if (state == "captured") return false;
            error = value.TryGetProperty("error", out var errorElement) && errorElement.ValueKind == JsonValueKind.String
                ? errorElement.GetString() : state;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
    }

    private static void WriteAtomic(string path, string text)
    {
        string directory = Path.GetDirectoryName(path)!;
        string temporary = Path.Combine(directory, $".world-scan-{Guid.NewGuid():N}.tmp");
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

    private static FileStream OpenSharedRead(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

    private static bool IsSafeCommandId(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 128
        && char.IsAsciiLetterOrDigit(value[0])
        && value.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_');
}
