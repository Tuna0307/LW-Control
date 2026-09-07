using System.Text.Json;

namespace LWControl.Core;

/// <summary>
/// Clean-room implementation of the file command transport recovered from
/// LastWarControl.Bridge. The game-side LWC2 bridge consumes 64 atomic JSON
/// queue slots under %LOCALAPPDATA%\LastWarControl\commands.
/// </summary>
public sealed class RecoveredGameCommandBridge
{
    private const int SchemaVersion = 1;
    private const int QueueSlotCount = 64;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    private readonly SemaphoreSlim enqueueGate = new(1, 1);

    public RecoveredGameCommandBridge(string? rootDirectory = null)
    {
        RootDirectory = Path.GetFullPath(rootDirectory ?? LocalBridgeInspector.DefaultRootDirectory);
        CommandsDirectory = Path.Combine(RootDirectory, "commands");
        PendingDirectory = Path.Combine(CommandsDirectory, "pending");
        ProcessingDirectory = Path.Combine(CommandsDirectory, "processing");
        ResultsDirectory = Path.Combine(CommandsDirectory, "results");
        LedgerDirectory = Path.Combine(CommandsDirectory, "ledger");
        CancelledDirectory = Path.Combine(CommandsDirectory, "cancelled");
        RuntimeDirectory = Path.Combine(RootDirectory, "runtime");
    }

    public string RootDirectory { get; }
    public string CommandsDirectory { get; }
    public string PendingDirectory { get; }
    public string ProcessingDirectory { get; }
    public string ResultsDirectory { get; }
    public string LedgerDirectory { get; }
    public string CancelledDirectory { get; }
    public string RuntimeDirectory { get; }

    public LocalBridgeInspection Inspect() => LocalBridgeInspector.Inspect(RootDirectory);

    public async Task<RecoveredBridgeReceipt> EnqueueAsync(
        string commandId,
        string featureId,
        IReadOnlyDictionary<string, string?> arguments,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(commandId, nameof(commandId));
        ValidateIdentity(featureId, nameof(featureId));

        await enqueueGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureDirectories();
            string admissionPath = Path.Combine(LedgerDirectory, commandId + ".enqueue.lock");
            string temporary = Path.Combine(PendingDirectory, $".{commandId}.{Guid.NewGuid():N}.tmp");
            FileStream? admissionLock = null;
            try
            {
                try
                {
                    admissionLock = new FileStream(admissionPath, FileMode.CreateNew, FileAccess.ReadWrite,
                        FileShare.None, 1, FileOptions.WriteThrough | FileOptions.DeleteOnClose);
                }
                catch (IOException) when (File.Exists(admissionPath))
                {
                    return new(commandId, false, DateTimeOffset.UtcNow, "command_id_admission_in_progress");
                }

                string? reusedAt = FindCommandIdReuse(commandId);
                if (reusedAt is not null)
                    return new(commandId, false, DateTimeOffset.UtcNow, $"command_id_already_used:{reusedAt}");

                var envelope = new RecoveredCommandEnvelope(
                    SchemaVersion,
                    commandId,
                    featureId,
                    DateTimeOffset.UtcNow,
                    new Dictionary<string, string?>(arguments, StringComparer.Ordinal));

                await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                    FileShare.None, 16 * 1024, FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    await JsonSerializer.SerializeAsync(stream, envelope, JsonOptions, cancellationToken)
                        .ConfigureAwait(false);
                    await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                    stream.Flush(flushToDisk: true);
                }
                for (int slot = 0; slot < QueueSlotCount; slot++)
                {
                    string path = Path.Combine(PendingDirectory, $"slot-{slot:00}.json");
                    try
                    {
                        File.Move(temporary, path, false);
                        return new(commandId, true, DateTimeOffset.UtcNow, null);
                    }
                    catch (IOException) when (File.Exists(path)) { }
                }
                return new(commandId, false, DateTimeOffset.UtcNow, $"game_command_queue_full:{QueueSlotCount}");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return new(commandId, false, DateTimeOffset.UtcNow, ex.Message);
            }
            finally
            {
                TryDelete(temporary);
                admissionLock?.Dispose();
                TryDelete(admissionPath);
            }
        }
        finally
        {
            enqueueGate.Release();
        }
    }

    public async Task<RecoveredBridgeResult?> WaitForResultAsync(
        string commandId,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(commandId, nameof(commandId));
        string path = Path.Combine(ResultsDirectory, commandId + ".json");
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RecoveredBridgeResult? result = TryReadResultPath(path);
            if (result is not null) return result;
            await Task.Delay(150, cancellationToken).ConfigureAwait(false);
        }
        return TryReadResultPath(path);
    }

    public RecoveredBridgeResult? TryReadResult(string commandId) =>
        TryReadResultPath(Path.Combine(ResultsDirectory, commandId + ".json"));

    private static RecoveredBridgeResult? TryReadResultPath(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using JsonDocument document = JsonDocument.Parse(stream);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;

            string commandId = ReadString(root, "command_id");
            string featureId = ReadString(root, "feature_id");
            string status = ReadString(root, "status");
            bool transportOk = ReadBool(root, "transport_ok");
            bool actionOk = ReadBool(root, "action_ok");
            string? error = EmptyToNull(ReadString(root, "error"));
            JsonElement output = root.TryGetProperty("output", out JsonElement value)
                ? value.Clone()
                : default;
            return new(commandId, featureId, status, transportOk, actionOk, error, output);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private void EnsureDirectories()
    {
        Directory.CreateDirectory(PendingDirectory);
        Directory.CreateDirectory(ProcessingDirectory);
        Directory.CreateDirectory(ResultsDirectory);
        Directory.CreateDirectory(LedgerDirectory);
        Directory.CreateDirectory(CancelledDirectory);
        Directory.CreateDirectory(RuntimeDirectory);
    }

    private string? FindCommandIdReuse(string commandId)
    {
        if (File.Exists(Path.Combine(ResultsDirectory, commandId + ".json"))) return "result";
        if (Directory.Exists(LedgerDirectory)
            && Directory.EnumerateFiles(LedgerDirectory, commandId + ".*.json").Any()) return "ledger";
        if (Directory.Exists(CancelledDirectory)
            && Directory.EnumerateFiles(CancelledDirectory, commandId + "*.json").Any()) return "cancelled";

        if (Directory.Exists(PendingDirectory)
            && Directory.EnumerateFiles(PendingDirectory, "." + commandId + ".*.tmp").Any())
            return "pending write";

        foreach ((string Directory, string Pattern) item in new[]
                 {
                     (PendingDirectory, "slot-*.json"),
                     (ProcessingDirectory, "*.json"),
                     (CancelledDirectory, "*.json"),
                 })
        {
            if (!Directory.Exists(item.Directory)) continue;
            foreach (string path in Directory.EnumerateFiles(item.Directory, item.Pattern))
            {
                try
                {
                    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
                    if (string.Equals(ReadString(document.RootElement, "command_id"), commandId,
                            StringComparison.Ordinal))
                        return Path.GetFileName(item.Directory);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
            }
        }
        return null;
    }

    private static void ValidateIdentity(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128
            || !char.IsAsciiLetterOrDigit(value[0])
            || value.Any(ch => !(char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_')))
            throw new ArgumentException($"{name} is not safe for the recovered file transport.", name);
    }

    private static string ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static bool ReadBool(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            && value.GetBoolean();

    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private sealed record RecoveredCommandEnvelope(
        int SchemaVersion,
        string CommandId,
        string FeatureId,
        DateTimeOffset RequestedAt,
        Dictionary<string, string?> Arguments);
}

public sealed record RecoveredBridgeReceipt(
    string CommandId,
    bool Accepted,
    DateTimeOffset ReceivedAt,
    string? Error);

public sealed record RecoveredBridgeResult(
    string CommandId,
    string FeatureId,
    string Status,
    bool TransportOk,
    bool ActionOk,
    string? Error,
    JsonElement Output)
{
    public bool OutputOk => Output.ValueKind == JsonValueKind.Object
        && Output.TryGetProperty("ok", out JsonElement value)
        && value.ValueKind is JsonValueKind.True or JsonValueKind.False
        && value.GetBoolean();

    public string? OutputError => ReadOutputString("error");
    public string? OutputMessage => ReadOutputString("message");

    public int Total
    {
        get
        {
            if (Output.ValueKind != JsonValueKind.Object || !Output.TryGetProperty("total", out JsonElement value))
                return 0;
            return value.TryGetInt32(out int total) ? total : 0;
        }
    }

    public string? TaskResultStatus => ReadTaskResultString("status");
    public string? TaskResultMessage => ReadTaskResultString("message");

    public bool IsBlocked =>
        Status.Contains("blocked", StringComparison.OrdinalIgnoreCase)
        || string.Equals(TaskResultStatus, "Blocked", StringComparison.OrdinalIgnoreCase)
        || string.Equals(TaskResultStatus, "Waiting", StringComparison.OrdinalIgnoreCase);

    public bool IsSucceeded
    {
        get
        {
            if (!TransportOk || IsBlocked) return false;
            if (string.Equals(TaskResultStatus, "ConfirmedSuccess", StringComparison.OrdinalIgnoreCase)) return true;
            if (ActionOk && OutputOk) return true;
            return OutputOk && (Status.Contains("observed", StringComparison.OrdinalIgnoreCase)
                || Status.Contains("success", StringComparison.OrdinalIgnoreCase)
                || Status.Contains("complete", StringComparison.OrdinalIgnoreCase));
        }
    }

    public bool ContractComplete => IsSucceeded && (ActionOk
        || string.Equals(TaskResultStatus, "ConfirmedSuccess", StringComparison.OrdinalIgnoreCase)
        || Status.Contains("effect_observed", StringComparison.OrdinalIgnoreCase));

    public string Describe() => TaskResultMessage
        ?? OutputMessage
        ?? OutputError
        ?? Error
        ?? (string.IsNullOrWhiteSpace(Status) ? "Game bridge returned a result." : Status);

    private string? ReadOutputString(string name)
    {
        if (Output.ValueKind != JsonValueKind.Object || !Output.TryGetProperty(name, out JsonElement value)
            || value.ValueKind != JsonValueKind.String) return null;
        string? text = value.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private string? ReadTaskResultString(string name)
    {
        if (Output.ValueKind != JsonValueKind.Object
            || !Output.TryGetProperty("task_result", out JsonElement task)
            || task.ValueKind != JsonValueKind.Object
            || !task.TryGetProperty(name, out JsonElement value)
            || value.ValueKind != JsonValueKind.String) return null;
        string? text = value.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
