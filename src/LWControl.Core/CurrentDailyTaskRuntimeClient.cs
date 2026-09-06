using System.Text;
using System.Text.Json;

namespace LWControl.Core;

public sealed record CurrentDailyTaskRuntimeInspection(
    string RootDirectory,
    bool HeartbeatPresent,
    bool HeartbeatFresh,
    string? RuntimeVersion,
    DateTimeOffset? HeartbeatAt,
    string? RegistrationMethod,
    string StatusCode);

public sealed record CurrentDailyTaskRuntimeTarget
{
    public required string Kind { get; init; }
    public string? TaskId { get; init; }
    public int? Stage { get; init; }

    public void Validate()
    {
        if (Kind == "DailyTask")
        {
            if (string.IsNullOrWhiteSpace(TaskId) || TaskId.Length > 128 || Stage.HasValue)
                throw new InvalidDataException("Daily task runtime result contains an invalid task target.");
            return;
        }
        if (Kind == "DailyQuestStage")
        {
            if (!Stage.HasValue || Stage.Value is < 1 or > CurrentDailyTaskState.DailyBoxCount || TaskId is not null)
                throw new InvalidDataException("Daily task runtime result contains an invalid chest target.");
            return;
        }
        throw new InvalidDataException($"Daily task runtime result contains unsupported target kind '{Kind}'.");
    }
}

public sealed record CurrentDailyTaskRuntimeResult
{
    public required int SchemaVersion { get; init; }
    public required string RuntimeVersion { get; init; }
    public required string CommandId { get; init; }
    public required string State { get; init; }
    public required string Message { get; init; }
    public required int ConfirmedClaims { get; init; }
    public required int RewardSendCount { get; init; }
    public required int RefreshSendCount { get; init; }
    public required IReadOnlyList<CurrentDailyTaskRuntimeTarget> ClaimedTargets { get; init; }
    public CurrentDailyTaskSnapshot? FinalSnapshot { get; init; }
    public required DateTimeOffset CompletedAt { get; init; }

    public void Validate(string expectedCommandId)
    {
        if (SchemaVersion != 1)
            throw new InvalidDataException("Unsupported Daily Task runtime result schema.");
        if (!string.Equals(CommandId, expectedCommandId, StringComparison.Ordinal))
            throw new InvalidDataException("Daily Task runtime result command ID mismatch.");
        if (string.IsNullOrWhiteSpace(RuntimeVersion) || RuntimeVersion.Length > 128)
            throw new InvalidDataException("Daily Task runtime result version is invalid.");
        if (State is not ("completed" or "failed" or "unknown"))
            throw new InvalidDataException("Daily Task runtime result state is invalid.");
        if (Message is null || Message.Length > 512)
            throw new InvalidDataException("Daily Task runtime result message is too large.");
        if (ConfirmedClaims is < 0 or > 20 || RewardSendCount is < 0 or > 20 || RefreshSendCount is < 0 or > 21)
            throw new InvalidDataException("Daily Task runtime result counters are invalid.");
        if (ConfirmedClaims > RewardSendCount || ClaimedTargets is null || ClaimedTargets.Count != ConfirmedClaims
            || ClaimedTargets.Any(target => target is null))
            throw new InvalidDataException("Daily Task runtime result claim counters do not correlate.");
        foreach (var target in ClaimedTargets) target.Validate();
        if (State == "completed")
        {
            if (FinalSnapshot is null)
                throw new InvalidDataException("Completed Daily Task runtime result is missing final authoritative state.");
            FinalSnapshot.Validate(CompletedAt, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5));
            foreach (var target in ClaimedTargets)
            {
                bool confirmed = target.Kind switch
                {
                    "DailyTask" => FinalSnapshot.Tasks.Any(task =>
                        string.Equals(task.TaskId, target.TaskId, StringComparison.Ordinal)
                        && task.State == CurrentTaskState.Received),
                    "DailyQuestStage" => FinalSnapshot.Boxes.Any(box =>
                        box.Index == target.Stage && box.State == CurrentTaskState.Received)
                        && FinalSnapshot.ReceivedStages.Contains(target.Stage!.Value),
                    _ => false,
                };
                if (!confirmed)
                    throw new InvalidDataException("Daily Task runtime final state does not confirm every claimed target.");
            }
        }
    }
}

public sealed class CurrentDailyTaskRuntimeClient
{
    public const string ExpectedRuntimeVersion = "lwcontrol-daily-task-runtime-1";
    public static readonly TimeSpan HeartbeatFreshnessWindow = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan HeartbeatFutureTolerance = TimeSpan.FromSeconds(5);
    private readonly string root;
    private readonly SemaphoreSlim gate = new(1, 1);

    public CurrentDailyTaskRuntimeClient(string? rootDirectory = null)
    {
        root = Path.GetFullPath(rootDirectory ?? DefaultRootDirectory);
    }

    public static string DefaultRootDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LWControl", "runtime");

    public CurrentDailyTaskRuntimeInspection Inspect(DateTimeOffset? checkedAt = null)
    {
        var now = checkedAt ?? DateTimeOffset.UtcNow;
        var heartbeatPath = Path.Combine(root, "daily-task-runtime-heartbeat.json");
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
            DateTimeOffset? observed = null;
            if (value.TryGetProperty("updatedAt", out var timeElement) && timeElement.TryGetInt64(out long unix))
                observed = DateTimeOffset.FromUnixTimeSeconds(unix);
            bool fresh = observed.HasValue
                && observed.Value <= now + HeartbeatFutureTolerance
                && now - observed.Value <= HeartbeatFreshnessWindow;
            string status = !string.Equals(version, ExpectedRuntimeVersion, StringComparison.Ordinal)
                ? "runtime_version_mismatch"
                : !fresh ? "heartbeat_stale" : "ready";
            return new(root, true, fresh, version, observed, registration, status);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentOutOfRangeException)
        {
            return new(root, true, false, null, null, null, "heartbeat_invalid");
        }
    }

    public async Task<CurrentDailyTaskRuntimeResult> RunOnceAsync(
        int maximumClaims,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        if (maximumClaims is < 1 or > 20)
            throw new ArgumentOutOfRangeException(nameof(maximumClaims), "Maximum claims must be between 1 and 20.");
        TimeSpan wait = timeout ?? TimeSpan.FromMinutes(2);
        if (wait < TimeSpan.FromSeconds(5) || wait > TimeSpan.FromMinutes(5))
            throw new ArgumentOutOfRangeException(nameof(timeout), "Runtime result timeout must be between 5 seconds and 5 minutes.");

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var inspection = Inspect();
            if (inspection.StatusCode != "ready")
                throw new InvalidOperationException($"Daily Task runtime is not ready ({inspection.StatusCode}).");

            Directory.CreateDirectory(root);
            string commandId = $"daily-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
            string commandPath = Path.Combine(root, "daily-task-command.txt");
            ClearCompletedCommandOrThrowBusy(commandPath);
            WriteCommandAtomic(commandPath, commandId, maximumClaims);

            string resultPath = Path.Combine(root, $"daily-task-result-{commandId}.json");
            var deadline = DateTimeOffset.UtcNow + wait;
            while (DateTimeOffset.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (File.Exists(resultPath))
                {
                    var result = JsonFiles.Read<CurrentDailyTaskRuntimeResult>(resultPath);
                    result.Validate(commandId);
                    DeleteCommandIfOwned(commandPath, commandId);
                    return result;
                }
                await Task.Delay(250, cancellationToken).ConfigureAwait(false);
            }
            throw new TimeoutException("Daily Task runtime did not produce a correlated result before the timeout.");
        }
        finally
        {
            gate.Release();
        }
    }

    public static string BuildCommandText(string commandId, int maximumClaims)
    {
        if (!IsSafeCommandId(commandId)) throw new ArgumentException("Command ID is invalid.", nameof(commandId));
        if (maximumClaims is < 1 or > 20) throw new ArgumentOutOfRangeException(nameof(maximumClaims));
        return $"schema=1\ncommandId={commandId}\nmode=run_once\nmaximumClaims={maximumClaims}\n";
    }

    private void ClearCompletedCommandOrThrowBusy(string commandPath)
    {
        if (!File.Exists(commandPath)) return;
        string? existing = ReadCommandId(commandPath);
        if (existing is not null && File.Exists(Path.Combine(root, $"daily-task-result-{existing}.json")))
        {
            File.Delete(commandPath);
            return;
        }
        throw new InvalidOperationException("A Daily Task runtime command is already pending or its state is unknown.");
    }

    private static void WriteCommandAtomic(string commandPath, string commandId, int maximumClaims)
    {
        string directory = Path.GetDirectoryName(commandPath)!;
        string temporary = Path.Combine(directory, $".daily-task-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporary, BuildCommandText(commandId, maximumClaims), new UTF8Encoding(false));
            File.Move(temporary, commandPath, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static string? ReadCommandId(string commandPath)
    {
        try
        {
            using var stream = OpenSharedRead(commandPath);
            if (stream.Length > 4096) return null;
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            foreach (string? line in ReadLines(reader))
                if (line.StartsWith("commandId=", StringComparison.Ordinal))
                    return line["commandId=".Length..];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return null;
    }

    private static IEnumerable<string> ReadLines(StreamReader reader)
    {
        string? line;
        while ((line = reader.ReadLine()) is not null) yield return line;
    }

    private static void DeleteCommandIfOwned(string commandPath, string commandId)
    {
        if (string.Equals(ReadCommandId(commandPath), commandId, StringComparison.Ordinal))
            File.Delete(commandPath);
    }

    private static FileStream OpenSharedRead(string path) => new(
        path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

    private static bool IsSafeCommandId(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 128
        && char.IsAsciiLetterOrDigit(value[0])
        && value.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_');
}
