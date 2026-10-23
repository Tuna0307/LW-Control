using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed record Feature018AuthenticatedGameAccount(string GameUid, int ServerId);

/// <summary>
/// FEATURE-018 implementation policy: a single outstanding command for the
/// exact authenticated Overview game owner. This is transport and admission,
/// not evidence that a particular game method is available or succeeded.
/// </summary>
internal sealed class Feature018OwnedGameExchange
{
    private static readonly TimeSpan PollPeriod = TimeSpan.FromMilliseconds(50);
    private readonly Func<OverviewMapScanSession?> currentSession;
    private readonly Func<OverviewMapScanSession?>? retainedSession;
    private readonly Func<OverviewMapScanSession, bool> isSameSession;
    private readonly Func<OverviewMapScanSession, bool> approvedAccount;
    private readonly Func<OverviewMapScanSession, Feature018AuthenticatedGameAccount?>? currentAccount;
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
            currentAccount: owner => ReadFreshAuthenticatedAccount(
                Path.Combine(lifecycle.RuntimeRoot, "heartbeat.json"), owner),
            retainedSession: () => lifecycle.GetOwnedMapScanSession()) { }

    internal Feature018OwnedGameExchange(
        Func<OverviewMapScanSession?> currentSession,
        Func<OverviewMapScanSession, bool> isSameSession,
        string requestPath,
        string responsePath,
        Func<DateTimeOffset>? utcNow = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        Func<OverviewMapScanSession, bool>? approvedAccount = null,
        Func<OverviewMapScanSession, Feature018AuthenticatedGameAccount?>? currentAccount = null,
        Func<OverviewMapScanSession?>? retainedSession = null)
    {
        this.currentSession = currentSession;
        this.retainedSession = retainedSession;
        this.isSameSession = isSameSession;
        this.approvedAccount = approvedAccount ?? (_ => true);
        this.currentAccount = currentAccount;
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
        bool allowAcceptedDispatch = false,
        Feature018AuthenticatedGameAccount? expectedAccount = null)
    {
        if (!ValidToken(command) || timeout <= TimeSpan.Zero || timeout > TimeSpan.FromSeconds(60))
            throw new BridgeCommandException("INVALID_REQUEST", "Invalid FEATURE018 game command or timeout.");
        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            bool cityRequest = command.StartsWith("city_layout_", StringComparison.Ordinal);
            DateTimeOffset deadline = utcNow() + timeout;
            OverviewMapScanSession owner = currentSession() ?? (cityRequest ? retainedSession?.Invoke() : null) ?? throw new BridgeCommandException(
                "GAME_CONNECTION_UNAVAILABLE", "There is no authenticated owned game session.");
            async Task<Feature018AuthenticatedGameAccount?> RequireOwnerAsync(Feature018AuthenticatedGameAccount? expected = null)
            {
                if (!cityRequest) return RequireSameOwner(owner, expected);
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    OverviewMapScanSession? ready = currentSession();
                    if (!isSameSession(owner) || (ready is not null && ready != owner))
                        throw new BridgeCommandException("GAME_CONNECTION_UNAVAILABLE", "City session changed; pending actions are never replayed.");
                    if (!approvedAccount(owner))
                        throw new BridgeCommandException("GAME_ACCOUNT_NOT_AUTHORIZED", "City account is not authorized.");
                    Feature018AuthenticatedGameAccount? fresh = currentAccount?.Invoke(owner);
                    if (fresh is not null && expected is not null && fresh != expected)
                        throw new BridgeCommandException("GAME_CONNECTION_UNAVAILABLE", "City UID/server changed; pending actions are never replayed.");
                    if (ready == owner && (currentAccount is null || fresh is not null)) return fresh;
                    if (utcNow() >= deadline)
                        throw new BridgeCommandException("GAME_ACCOUNT_NOT_AUTHORIZED", "City request paused for an unavailable account; no request was replayed.");
                    await delay(PollPeriod, cancellationToken).ConfigureAwait(false);
                }
            }
            Feature018AuthenticatedGameAccount? account = await RequireOwnerAsync(expectedAccount).ConfigureAwait(false);
            if (expectedAccount is not null && account != expectedAccount)
                throw new BridgeCommandException("GAME_CONNECTION_UNAVAILABLE",
                    "The game UID or server changed since FEATURE018 input admission.");
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
            // The native mailbox admits every FEATURE018 action against its
            // current authenticated game UID. Stamp the account observed by
            // the desktop host for ALL commands, including City, shield and
            // Mini Games reads. A caller is never allowed to choose this UID.
            if (account is not null)
                lines.Add($"authorizedUid={account.GameUid}");
            foreach (var pair in (fields ?? new Dictionary<string, string>()).OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                if (!ValidToken(pair.Key) || pair.Key is "schema" or "requestId" or "profileId" or
                    "launchSessionId" or "challenge" or "gamePid" or "command" or "issuedAt" ||
                    pair.Value is null || pair.Value.Length > 16000 ||
                    pair.Value.IndexOfAny(['\r', '\n', '\0']) >= 0)
                    throw new BridgeCommandException("INVALID_REQUEST", "Invalid FEATURE018 action arguments.");
                if (pair.Key == "authorizedUid" && account is not null)
                {
                    if (!string.Equals(pair.Value, account.GameUid, StringComparison.Ordinal))
                        throw new BridgeCommandException("INVALID_REQUEST",
                            "FEATURE018 game UID must come from the authenticated session.");
                    continue; // Already written once from fresh authenticated heartbeat.
                }
                lines.Add($"{pair.Key}={pair.Value}");
            }
            // Atomic replacement is confined to this authenticated owner's
            // private Overview runtime directory, never to installed game files.
            string? directory = Path.GetDirectoryName(requestPath);
            if (directory is null) throw new InvalidOperationException("FEATURE018 runtime path unavailable.");
            string temporary = requestPath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                for (int attempt = 0; ; attempt++)
                {
                    await RequireOwnerAsync(account).ConfigureAwait(false);
                    try
                    {
                        Directory.CreateDirectory(directory);
                        await File.WriteAllTextAsync(temporary, string.Join('\n', lines) + "\n",
                            new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
                        await RequireOwnerAsync(account).ConfigureAwait(false);
                        File.Move(temporary, requestPath, overwrite: true);
                        break; // Published once. Never repeat a published command.
                    }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                    {
                        // A reader/indexer can briefly hold the previous mailbox.
                        // Retry this SAME envelope only before atomic publication.
                        if (attempt >= 5 || utcNow() >= deadline)
                            throw new BridgeCommandException("GAME_COMMAND_NOT_PUBLISHED",
                                "The game command file is busy. No change was sent. Try again.",
                                new { requestDispatched = false });
                        await delay(TimeSpan.FromMilliseconds(50 * (attempt + 1)), cancellationToken)
                            .ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                // Cleanup must not turn a successful publication into an
                // ambiguous error (or hide the original pre-publication error).
                try { File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }

            while (utcNow() < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await RequireOwnerAsync(account).ConfigureAwait(false);
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

                    await RequireOwnerAsync(account).ConfigureAwait(false);
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
                catch (UnauthorizedAccessException) { }
                await delay(PollPeriod, cancellationToken).ConfigureAwait(false);
            }
            throw new BridgeCommandException("GAME_RESPONSE_TIMEOUT", "No correlated game action result arrived.");
        }
        finally { operationGate.Release(); }
    }

    private Feature018AuthenticatedGameAccount? RequireSameOwner(
        OverviewMapScanSession owner, Feature018AuthenticatedGameAccount? expected = null)
    {
        if (!isSameSession(owner) || currentSession() != owner)
            throw new BridgeCommandException("GAME_CONNECTION_UNAVAILABLE", "Owned game session changed during FEATURE018 request.");
        if (!approvedAccount(owner))
            throw new BridgeCommandException("GAME_ACCOUNT_NOT_AUTHORIZED",
                "The authenticated game account is unavailable for this FEATURE018 request.");
        if (currentAccount is null) return null; // Inert test transport with an injected owner only.
        Feature018AuthenticatedGameAccount account = currentAccount(owner) ??
            throw new BridgeCommandException("GAME_ACCOUNT_NOT_AUTHORIZED",
                "A fresh logged-in game UID and server are required for FEATURE018.");
        if (expected is not null && account != expected)
            throw new BridgeCommandException("GAME_CONNECTION_UNAVAILABLE",
                "The authenticated game UID or server changed during FEATURE018 request.");
        return account;
    }

    internal static Feature018AuthenticatedGameAccount? ReadFreshAuthenticatedAccount(
        string heartbeatPath, OverviewMapScanSession owner,
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
                if (!OverviewLifecycleService.HeartbeatMatches(heartbeat,
                           owner.ProfileId, owner.SessionId, owner.Challenge, owner.GamePid,
                           DateTimeOffset.UtcNow.ToUnixTimeSeconds()) ||
                    !heartbeat.TryGetProperty("gameStateObserved", out var observed) ||
                    observed.ValueKind != JsonValueKind.True ||
                    !heartbeat.TryGetProperty("loggedIn", out var loggedIn) ||
                    loggedIn.ValueKind != JsonValueKind.True ||
                    !heartbeat.TryGetProperty("connected", out var connected) ||
                    connected.ValueKind != JsonValueKind.True ||
                    !heartbeat.TryGetProperty("gameUid", out var uid) ||
                    uid.ValueKind != JsonValueKind.String ||
                    !heartbeat.TryGetProperty("serverId", out var server) ||
                    server.ValueKind != JsonValueKind.Number ||
                    !server.TryGetInt32(out int serverId) || serverId <= 0)
                    return null;
                string? gameUid = uid.GetString();
                if (gameUid is null || gameUid.Length is < 1 or > 20 ||
                    gameUid[0] == '0' || !gameUid.All(char.IsAsciiDigit))
                    return null;
                return new Feature018AuthenticatedGameAccount(gameUid, serverId);
            }
            catch (IOException) { }
            catch (JsonException) { }
            catch (UnauthorizedAccessException) { return null; }
            if (attempt < 2) Thread.Yield();
        }
        return null;
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
