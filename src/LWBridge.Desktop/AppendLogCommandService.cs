using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed class AppendLogCommandService
{
    private const int MaximumScalars = 2000;
    private static readonly ConcurrentDictionary<string, object> LogGates =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly string profileId;
    private readonly string? runtimeDirectory;
    private readonly Func<long> nowMilliseconds;
    private readonly string? logPath;
    private readonly object? logGate;

    internal AppendLogCommandService(
        string profileId,
        string? runtimeDirectory,
        Func<long>? nowMilliseconds = null)
    {
        if (string.IsNullOrWhiteSpace(profileId))
            throw new ArgumentException("profileId is required.", nameof(profileId));

        this.profileId = profileId;
        this.runtimeDirectory = string.IsNullOrWhiteSpace(runtimeDirectory)
            ? null
            : Path.GetFullPath(runtimeDirectory);
        this.nowMilliseconds =
            nowMilliseconds ?? RecoveredWallClock.UnixTimeMilliseconds;

        if (this.runtimeDirectory is not null)
        {
            logPath = Path.Combine(
                this.runtimeDirectory,
                "logs",
                "xlua-bridge.log");
            logGate = LogGates.GetOrAdd(
                logPath,
                static _ => new object());
        }
    }

    internal object? Invoke(JsonElement payload)
    {
        RequireRuntime(payload);

        if (!payload.TryGetProperty("message", out JsonElement value) ||
            value.ValueKind != JsonValueKind.String)
        {
            throw new BridgeCommandException(
                "INVALID_PAYLOAD",
                "message must be a string.");
        }

        string message = value.GetString() ?? string.Empty;
        string normalized = NormalizeMessage(message);
        string line =
            "[" +
            nowMilliseconds().ToString(CultureInfo.InvariantCulture) +
            "] [bridge-app] " +
            normalized +
            "\n";

        BestEffortAppend(line);
        return null;
    }

    internal static string NormalizeMessage(string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var builder = new StringBuilder(
            Math.Min(message.Length, MaximumScalars * 2));
        int count = 0;
        foreach (Rune rune in message.EnumerateRunes())
        {
            if (count >= MaximumScalars)
                break;

            builder.Append(
                rune.Value is '\r' or '\n'
                    ? ' '
                    : rune.ToString());
            count++;
        }
        return builder.ToString();
    }

    private void BestEffortAppend(string line)
    {
        if (logPath is null || logGate is null)
            return;

        lock (logGate)
        {
            try
            {
                Directory.CreateDirectory(
                    Path.GetDirectoryName(logPath)!);
                byte[] bytes = Encoding.UTF8.GetBytes(line);
                using var stream = new FileStream(
                    logPath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.ReadWrite);
                stream.Write(bytes);
                stream.Flush();
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (NotSupportedException) { }
            catch (System.Security.SecurityException) { }
        }
    }

    private void RequireRuntime(JsonElement payload)
    {
        if (!payload.TryGetProperty("profileId", out JsonElement property) ||
            property.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new BridgeCommandException(
                "PROFILE_ID_REQUIRED",
                "profileId is required");
        }

        if (!string.Equals(
                property.GetString(),
                profileId,
                StringComparison.Ordinal) ||
            runtimeDirectory is null)
        {
            throw new BridgeCommandException(
                "PROFILE_RUNTIME_UNAVAILABLE",
                "profile runtime unavailable");
        }
    }
}
