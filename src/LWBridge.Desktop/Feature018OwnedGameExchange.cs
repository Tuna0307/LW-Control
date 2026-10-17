using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace LWBridge.Desktop;

/// <summary>
/// FEATURE-018 implementation policy: a single outstanding command for the
/// exact authenticated Overview game owner. This is transport and admission,
/// not evidence that a particular game method is available or succeeded.
/// </summary>
internal sealed class Feature018OwnedGameExchange
{
    private static readonly TimeSpan PollPeriod = TimeSpan.FromMilliseconds(50);
    private readonly Func<OverviewMapScanSession?> currentSession;
    private readonly Func<OverviewMapScanSession, bool> isSameSession;
    private readonly Func<OverviewMapScanSession, bool> approvedAccount;
    private readonly string requestPath;
    private readonly string responsePath;
    // The window, retained profile backend and foreground hotkey monitor may
    // each create command services for one exact runtime directory. They must
    // share the ONE file mailbox in sequence, even across service instances.
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim operationGate;
    private readonly Func<DateTimeOffset> utcNow;
    private readonly Func<TimeSpan, CancellationToken, Task> delay;

    internal Feature018OwnedGameExchange(OverviewLifecycleService lifecycle)
        : this(lifecycle.GetReadyMapScanSession,
            lifecycle.MatchesOwnedMapScanSession,
            Path.Combine(lifecycle.RuntimeRoot, "feature018-request.txt"),
            Path.Combine(lifecycle.RuntimeRoot, "feature018-response.json"),
            approvedAccount: owner => FreshApprovedSubaccount(
                Path.Combine(lifecycle.RuntimeRoot, "heartbeat.json"), owner)) { }

    internal Feature018OwnedGameExchange(
        Func<OverviewMapScanSession?> currentSession,
        Func<OverviewMapScanSession, bool> isSameSession,
        string requestPath,
        string responsePath,
        Func<DateTimeOffset>? utcNow = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        Func<OverviewMapScanSession, bool>? approvedAccount = null)
    {
        this.currentSession = currentSession;
        this.isSameSession = isSameSession;
        this.approvedAccount = approvedAccount ?? (_ => true);
        this.requestPath = Path.GetFullPath(requestPath);
        this.responsePath = Path.GetFullPath(responsePath);
        operationGate = Gates.GetOrAdd(this.requestPath, _ => new SemaphoreSlim(1, 1));
        this.utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        this.delay = delay ?? Task.Delay;
        if (string.Equals(this.requestPath, this.responsePath, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("FEATURE018 request and response must have distinct paths.");
    }

    /// <remarks>
    /// Only a game producer can return state=confirmed. The method verifies the
    /// producer's exact current session, freshness and request before returning
    /// data. 'accepted' is an acknowledgement, never a completed game action.
    /// </remarks>
    internal async Task<JsonElement> ExchangeAsync(
        string command,
        IReadOnlyDictionary<string, string>? fields,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        bool allowAcceptedDispatch = false)
    {
        if (!ValidToken(command) || timeout <= TimeSpan.Zero || timeout > TimeSpan.FromSeconds(60))
            throw new BridgeCommandException("INVALID_REQUEST", "Invalid FEATURE018 game command or timeout.");
        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            OverviewMapScanSession owner = currentSession() ?? throw new BridgeCommandException(
                "GAME_CONNECTION_UNAVAILABLE", "There is no authenticated owned game session.");
            RequireSameOwner(owner);
            DateTimeOffset issued = utcNow();
            string requestId = "f018" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
            var lines = new List<string>
            {
                "schema=1",
                $"requestId={requestId}",
                $"profileId={owner.ProfileId}",
                $"launchSessionId={owner.SessionId}",
                $"challenge={owner.Challenge}",
                $"gamePid={owner.GamePid.ToString(CultureInfo.InvariantCulture)}",
                $"command={command}",
                $"issuedAt={issued.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)}",
            };
            foreach (var pair in (fields ?? new Dictionary<string, string>()).OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                if (!ValidToken(pair.Key) || pair.Key is "schema" or "requestId" or "profileId" or
                    "launchSessionId" or "challenge" or "gamePid" or "command" or "issuedAt" ||
                    pair.Value is null || pair.Value.Length > 16000 ||
                    pair.Value.IndexOfAny(['\r', '\n', '\0']) >= 0)
                    throw new BridgeCommandException("INVALID_REQUEST", "Invalid FEATURE018 action arguments.");
                lines.Add($"{pair.Key}={pair.Value}");
            }
            // Atomic replacement is confined to this authenticated owner's
            // private Overview runtime directory, never to installed game files.
            string? directory = Path.GetDirectoryName(requestPath);
            if (directory is null) throw new InvalidOperationException("FEATURE018 runtime path unavailable.");
            Directory.CreateDirectory(directory);
            string temporary = requestPath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                await File.WriteAllTextAsync(temporary, string.Join('\n', lines) + "\n",
                    new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
                RequireSameOwner(owner);
                File.Move(temporary, requestPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }

            DateTimeOffset deadline = issued + timeout;
            while (utcNow() < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                RequireSameOwner(owner);
                try
                {
                    using FileStream file = new(responsePath, FileMode.Open, FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete);
                    using JsonDocument parsed = await JsonDocument.ParseAsync(file,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                    JsonElement result = parsed.RootElement;
                    if (!Matches(result, "requestId", requestId))
                    {
                        await delay(PollPeriod, cancellationToken).ConfigureAwait(false);
                        continue;
                    }
                    long timestamp = Number(result, "observedAt");
                    DateTimeOffset observed = DateTimeOffset.FromUnixTimeMilliseconds(timestamp);
                    if (!Matches(result, "schemaVersion", 1) ||
                        !Matches(result, "profileId", owner.ProfileId) ||
                        !Matches(result, "launchSessionId", owner.SessionId) ||
                        !Matches(result, "challenge", owner.Challenge) ||
                        !Matches(result, "gamePid", owner.GamePid) ||
                        !Matches(result, "command", command) ||
                        observed < issued.AddSeconds(-2) || observed > utcNow().AddSeconds(5))
                        throw new BridgeCommandException("GAME_RESPONSE_UNTRUSTED", "A FEATURE018 game response did not match the owned request.");

                    RequireSameOwner(owner);
                    string state = Text(result, "state");
                    if (state == "confirmed" && result.TryGetProperty("data", out JsonElement data))
                        return data.Clone();
                    if (state == "accepted" && allowAcceptedDispatch &&
                        result.TryGetProperty("data", out JsonElement dispatched) &&
                        dispatched.ValueKind == JsonValueKind.Object &&
                        (dispatched.TryGetProperty("requestDispatched", out JsonElement sent) &&
                         sent.ValueKind == JsonValueKind.True ||
                         dispatched.TryGetProperty("accepted", out JsonElement accepted) &&
                         accepted.ValueKind == JsonValueKind.True))
                        return dispatched.Clone();
                    if (state is "unsupported" or "failed")
                        throw new BridgeCommandException(
                            Text(result, "errorCode") is { Length: > 0 } code ? code : "GAME_ACTION_FAILED",
                            Text(result, "error") is { Length: > 0 } message ? message : "Game action failed.",
                            result.TryGetProperty("data", out var diagnostic) ? diagnostic.Clone() : null);
                    // Accepted or queued does not constitute a confirmed game
                    // result and must not be returned as a success.
                    throw new BridgeCommandException("GAME_ACTION_UNCONFIRMED",
                        "Game did not confirm this action or state read.",
                        result.TryGetProperty("data", out var unconfirmed) ? unconfirmed.Clone() : null);
                }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
                catch (JsonException) { }
                catch (IOException) { }
                await delay(PollPeriod, cancellationToken).ConfigureAwait(false);
            }
            throw new BridgeCommandException("GAME_RESPONSE_TIMEOUT", "No correlated game action result arrived.");
        }
        finally { operationGate.Release(); }
    }

    private void RequireSameOwner(OverviewMapScanSession owner)
    {
        if (!isSameSession(owner) || currentSession() != owner)
            throw new BridgeCommandException("GAME_CONNECTION_UNAVAILABLE", "Owned game session changed during FEATURE018 request.");
        if (!approvedAccount(owner))
            throw new BridgeCommandException("GAME_ACCOUNT_NOT_AUTHORIZED",
                "FEATURE018 requires the currently authenticated UID 2553945035002288 on server 2288.");
    }

    internal static bool FreshApprovedSubaccount(string heartbeatPath, OverviewMapScanSession owner,
        Func<string, byte[]>? readAllBytes = null)
    {
        // The game rewrites its heartbeat in place. Only an interrupted read
        // may be retried; a parsed account/session/freshness mismatch is final.
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse((readAllBytes ?? File.ReadAllBytes)(heartbeatPath));
                JsonElement heartbeat = doc.RootElement;
                return OverviewLifecycleService.HeartbeatMatches(heartbeat,
                           owner.ProfileId, owner.SessionId, owner.Challenge, owner.GamePid,
                           DateTimeOffset.UtcNow.ToUnixTimeSeconds()) &&
                       heartbeat.TryGetProperty("gameStateObserved", out var observed) &&
                       observed.ValueKind == JsonValueKind.True &&
                       heartbeat.TryGetProperty("loggedIn", out var loggedIn) &&
                       loggedIn.ValueKind == JsonValueKind.True &&
                       heartbeat.TryGetProperty("connected", out var connected) &&
                       connected.ValueKind == JsonValueKind.True &&
                       heartbeat.TryGetProperty("gameUid", out var uid) &&
                       uid.ValueKind == JsonValueKind.String &&
                       uid.GetString() == "2553945035002288" &&
                       heartbeat.TryGetProperty("serverId", out var server) &&
                       server.TryGetInt32(out int serverId) && serverId == 2288;
            }
            catch (IOException) { }
            catch (JsonException) { }
            catch (UnauthorizedAccessException) { return false; }
            if (attempt < 2) Thread.Yield();
        }
        return false;
    }

    private static bool ValidToken(string value) =>
        !string.IsNullOrEmpty(value) && value.Length <= 128 &&
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-');

    private static bool Matches(JsonElement root, string key, string value) =>
        root.TryGetProperty(key, out JsonElement field) && field.ValueKind == JsonValueKind.String && field.GetString() == value;
    private static bool Matches(JsonElement root, string key, int value) =>
        root.TryGetProperty(key, out JsonElement field) && field.TryGetInt32(out int read) && read == value;
    private static string Text(JsonElement root, string key) =>
        root.TryGetProperty(key, out JsonElement field) && field.ValueKind == JsonValueKind.String ? field.GetString() ?? "" : "";
    private static long Number(JsonElement root, string key) =>
        root.TryGetProperty(key, out JsonElement field) && field.TryGetInt64(out long value) ? value : 0;
}
