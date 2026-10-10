using System.Security.Cryptography;
using System.Text.Json;

namespace LWBridge.Desktop;

// LWB-R7-110 + R7-117: application-global ownership shell for the recovered original
// bridge host. The original application constructs one bridge host during app
// startup (RVA 0x4100B5 -> 0x3C30BE), while profile start consumes that shared
// host. This shell owns the single registry and pipe identity for the desktop
// process. R7-121 composes the recovered listener/RPC layers behind explicit
// startup inputs. R7-122 closes the expected client-path source and original
// zero command-counter seed. R7-123 wires the same host into normal window
// startup before Overview launch registration.
internal sealed class LWBridgeControlPipeHostState : IDisposable
{
    private readonly object gate = new();
    private readonly LWBridgeControlPipeRegistry registry;
    private readonly Func<byte[]> pipeTokenEntropyFactory;
    private LWBridgeControlPipeCallRegistry? callRegistry;
    private LWBridgeControlPipeIsolatedAcceptLoop? acceptLoop;
    private Task? acceptLoopTask;
    private string? rpcExpectedBuildId;
    private string? rpcExpectedCanonicalClientPath;
    private bool stopped;

    internal LWBridgeControlPipeHostState(
        string? pipePath = null,
        LWBridgeControlPipeRegistry? registry = null,
        Func<byte[]>? pipeTokenEntropyFactory = null)
    {
        PipePath = string.IsNullOrWhiteSpace(pipePath)
            ? LWBridgeControlPipeContract.GetCurrentUserFullPath()
            : pipePath;
        this.registry = registry ?? new LWBridgeControlPipeRegistry();
        this.pipeTokenEntropyFactory = pipeTokenEntropyFactory ??
            (() => RandomNumberGenerator.GetBytes(
                LWBridgeProxyLaunchEnvironmentContract.PipeTokenEntropyBytes));
    }

    public string PipePath { get; }

    public bool IsStopped
    {
        get { lock (gate) return stopped; }
    }

    public int PendingRegistrationCount => registry.PendingCount;

    public int ConnectedRouteCount => registry.ConnectedCount;

    internal bool IsRouteConnected(string instanceId) =>
        !string.IsNullOrWhiteSpace(instanceId) &&
        registry.Resolve(instanceId) is not null;

    // HOME004 F-04: task-only fault entrypoint. A failed authenticated transport
    // must be produced by closing the actual pipe, rather than lying about the
    // route status. The caller must first check an exact owned game/session.
    // This method is never exposed to a normal frontend command.
    internal bool DisconnectExactAuthenticatedRouteForIsolatedTest(string instanceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        LWBridgeControlPipeAcceptedSession? session;
        lock (gate)
        {
            if (registry.Resolve(instanceId)?.Route is not
                LWBridgeControlPipeAcceptedSession accepted)
                return false;
            session = accepted;
            // Reauthentication of the old token must remain impossible while
            // the real game continues to write fresh heartbeat. A replacement
            // Start creates its own new registration.
            registry.Unregister(instanceId);
        }
        session.Dispose();
        return true;
    }

    internal long? GetPendingExpiration(string instanceId) =>
        registry.GetPendingExpiration(instanceId);

    internal int ServerInstanceCount
    {
        get { lock (gate) return acceptLoop?.ServerInstancesCreated ?? 0; }
    }

    internal int FirstInstanceFlagUseCount
    {
        get { lock (gate) return acceptLoop?.FirstInstanceFlagUses ?? 0; }
    }

    internal string? LastConnectInitialDisposition
    {
        get { lock (gate) return acceptLoop?.LastConnectInitialDisposition; }
    }

    internal int LastConnectInitialError
    {
        get { lock (gate) return acceptLoop?.LastConnectInitialError ?? 0; }
    }

    internal int FailedConnectCount
    {
        get { lock (gate) return acceptLoop?.FailedConnects ?? 0; }
    }

    internal int RejectedHandshakeCount
    {
        get { lock (gate) return acceptLoop?.RejectedHandshakes ?? 0; }
    }

    internal int AuthenticatedSessionCount
    {
        get { lock (gate) return acceptLoop?.AuthenticatedSessions ?? 0; }
    }

    internal string? LastHandshakeError
    {
        get { lock (gate) return acceptLoop?.LastHandshakeError; }
    }

    internal int FailedRpcSessionCount
    {
        get { lock (gate) return acceptLoop?.FailedRpcSessions ?? 0; }
    }

    internal string? LastRpcSessionError
    {
        get { lock (gate) return acceptLoop?.LastRpcSessionError; }
    }

    public int? PendingCallCount
    {
        get
        {
            lock (gate)
                return callRegistry?.PendingCount;
        }
    }

    public bool IsRpcTransportStarted
    {
        get
        {
            lock (gate)
                return acceptLoop is not null && acceptLoopTask is { IsCompleted: false };
        }
    }

    internal Task StartRpcTransport(
        string expectedBuildId,
        string expectedClientPath,
        string? currentUserSid = null,
        Func<long>? clockMilliseconds = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedBuildId);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedClientPath);

        string canonicalClientPath =
            LWBridgeControlPipeIsolatedHandshake
                .CanonicalizeExpectedClientPath(expectedClientPath);

        lock (gate)
        {
            ThrowIfStopped();
            if (acceptLoop is not null)
            {
                ThrowIfRpcListenerEnded();
                if (string.Equals(
                        rpcExpectedBuildId,
                        expectedBuildId,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        rpcExpectedCanonicalClientPath,
                        canonicalClientPath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return acceptLoopTask ??
                        throw new InvalidOperationException(
                            "The shared bridge RPC transport task is missing.");
                }

                throw new InvalidOperationException(
                    "The shared bridge RPC transport is already bound to a different build or client image.");
            }

            var calls = new LWBridgeControlPipeCallRegistry();
            var loop = new LWBridgeControlPipeIsolatedAcceptLoop(
                PipePath,
                registry,
                expectedBuildId,
                canonicalClientPath,
                (session, cancellationToken) =>
                    RunAuthenticatedRpcSessionAsync(
                        session,
                        calls,
                        cancellationToken),
                currentUserSid,
                clockMilliseconds);

            callRegistry = calls;
            acceptLoop = loop;
            rpcExpectedBuildId = expectedBuildId;
            rpcExpectedCanonicalClientPath = canonicalClientPath;
            acceptLoopTask = loop.StartAsync();

            if (acceptLoopTask.IsCompleted)
            {
                try
                {
                    acceptLoopTask.GetAwaiter().GetResult();
                }
                catch
                {
                    calls.Stop();
                    loop.DisposeAsync()
                        .AsTask()
                        .GetAwaiter()
                        .GetResult();
                    callRegistry = null;
                    acceptLoop = null;
                    acceptLoopTask = null;
                    rpcExpectedBuildId = null;
                    rpcExpectedCanonicalClientPath = null;
                    throw;
                }
            }

            return acceptLoopTask;
        }
    }

    internal async Task EnsureRpcTransportAsync(
        string expectedBuildId,
        string expectedClientPath,
        string? currentUserSid = null,
        Func<long>? clockMilliseconds = null,
        bool permitPerRegistrationClientPath = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedBuildId);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedClientPath);

        string canonicalClientPath =
            LWBridgeControlPipeIsolatedHandshake
                .CanonicalizeExpectedClientPath(expectedClientPath);
        bool restart;
        lock (gate)
        {
            ThrowIfStopped();
            if (acceptLoop is null)
            {
                restart = false;
            }
            else if (string.Equals(
                         rpcExpectedBuildId,
                         expectedBuildId,
                         StringComparison.Ordinal) &&
                     string.Equals(
                         rpcExpectedCanonicalClientPath,
                         canonicalClientPath,
                         StringComparison.OrdinalIgnoreCase) ||
                     (permitPerRegistrationClientPath &&
                      string.Equals(rpcExpectedBuildId, expectedBuildId,
                          StringComparison.Ordinal)))
            {
                ThrowIfRpcListenerEnded();
                return;
            }
            else
            {
                if (registry.PendingCount != 0 ||
                    registry.ConnectedCount != 0 ||
                    (callRegistry?.PendingCount ?? 0) != 0)
                {
                    throw new BridgeCommandException(
                        "BRIDGE_HOST_BUSY",
                        "The shared bridge host cannot change client image while a route, launch, or RPC call is active.");
                }
                restart = true;
            }
        }

        if (restart)
            await StopRpcTransportAsync().ConfigureAwait(false);

        _ = StartRpcTransport(
            expectedBuildId,
            expectedClientPath,
            currentUserSid,
            clockMilliseconds);
    }

    internal async Task<JsonElement?> CallLuaAsync(
        string route,
        string functionName,
        JsonElement args,
        long timestamp,
        long createdAt,
        CancellationToken cancellationToken = default,
        TimeSpan? resultTimeout = null,
        string? timeoutMessage = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(route);
        ArgumentException.ThrowIfNullOrWhiteSpace(functionName);

        LWBridgeControlPipeAcceptedSession session;
        lock (gate)
        {
            ThrowIfStopped();
            if (callRegistry is null || acceptLoop is null)
            {
                throw new InvalidOperationException(
                    "The shared bridge RPC transport is not started.");
            }

            ConnectedRoute? connected = registry.Resolve(route);
            if (connected?.Route is not LWBridgeControlPipeAcceptedSession
                accepted)
            {
                throw new InvalidOperationException(
                    "No authenticated bridge route is connected.");
            }

            session = accepted;
        }

        LWBridgeControlPipeRpcSessionTransport transport =
            await session.WaitForRpcTransportAsync(cancellationToken)
                .ConfigureAwait(false);
        return await transport.CallLuaAsync(
                functionName,
                args,
                timestamp,
                createdAt,
                resultTimeout,
                timeoutMessage).WaitAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    internal async Task StopRpcTransportAsync()
    {
        LWBridgeControlPipeIsolatedAcceptLoop? loop;
        LWBridgeControlPipeCallRegistry? calls;

        lock (gate)
        {
            loop = acceptLoop;
            calls = callRegistry;
            acceptLoop = null;
            acceptLoopTask = null;
            callRegistry = null;
            rpcExpectedBuildId = null;
            rpcExpectedCanonicalClientPath = null;
        }

        calls?.Stop();

        if (loop is not null)
            await loop.DisposeAsync().ConfigureAwait(false);
    }

    private static async Task RunAuthenticatedRpcSessionAsync(
        LWBridgeControlPipeAcceptedSession session,
        LWBridgeControlPipeCallRegistry calls,
        CancellationToken cancellationToken)
    {
        await using LWBridgeControlPipeRpcSessionTransport transport =
            session.AttachRpcTransport(calls);
        Task running = transport.StartAsync();

        try
        {
            await running.WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            await transport.StopAsync().ConfigureAwait(false);
        }
    }

    internal LWBridgeControlPipeLaunchBinding PrepareLaunchBinding(
        string profileId,
        string instanceId,
        string buildId,
        long nowMilliseconds,
        string? expectedClientPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(buildId);
        if (nowMilliseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(nowMilliseconds));

        lock (gate)
        {
            ThrowIfStopped();

            byte[] entropy = pipeTokenEntropyFactory();
            if (entropy.Length !=
                LWBridgeProxyLaunchEnvironmentContract.PipeTokenEntropyBytes)
            {
                throw new InvalidOperationException(
                    "LWBridge pipe-token entropy source returned an invalid byte count.");
            }

            string token =
                LWBridgeProxyLaunchEnvironmentContract.EncodePipeToken(entropy);
            long expiresAt = checked(
                nowMilliseconds +
                LWBridgeControlPipeRegistry
                    .StartupRegistrationLifetimeMilliseconds);
            string? exactExpectedClientPath = expectedClientPath is null ? null :
                LWBridgeControlPipeIsolatedHandshake
                    .CanonicalizeExpectedClientPath(expectedClientPath);
            registry.Register(profileId, instanceId, token, expiresAt,
                exactExpectedClientPath);

            IReadOnlyDictionary<string, string> environment =
                LWBridgeProxyLaunchEnvironmentContract.CreateBindings(
                    profileId,
                    instanceId,
                    token,
                    buildId);

            return new LWBridgeControlPipeLaunchBinding(
                profileId,
                instanceId,
                token,
                buildId,
                expiresAt,
                environment);
        }
    }

    // HOME009 R3 C: re-create ONLY an exact original token from a current-user
    // protected prior successful launch. Do not mint a new identity for a live game.
    internal ulong RestoreLaunchBinding(
        OverviewAdoptionSnapshot record, long nowMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.BuildId != OverviewLifecycleService.BridgeVersion ||
            nowMilliseconds < 0)
            throw new BridgeCommandException("PIPE_REGISTRATION_INVALID",
                "The retained launch binding is not compatible with this host.");
        lock (gate)
        {
            ThrowIfStopped();
            string expectedImage =
                LWBridgeControlPipeIsolatedHandshake.CanonicalizeExpectedClientPath(
                    record.GameExecutable);
            return registry.Register(record.ProfileId, record.InstanceId, record.PipeToken,
                checked(nowMilliseconds + LWBridgeControlPipeRegistry.StartupRegistrationLifetimeMilliseconds),
                expectedImage);
        }
    }

    internal long RefreshLaunchBinding(
        string instanceId,
        long nowMilliseconds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        if (nowMilliseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(nowMilliseconds));

        lock (gate)
        {
            ThrowIfStopped();
            long expiresAt = checked(
                nowMilliseconds +
                LWBridgeControlPipeRegistry
                    .StartupRegistrationLifetimeMilliseconds);
            registry.RefreshPending(instanceId, expiresAt);
            return expiresAt;
        }
    }

    // HOME 009 R2 D: 0x1DD152 refresh_pending(key, deadline) - the pending registration expires at the exact
    // wall-clock deadline (report + 90 s) the launcher-report handler also uses for its own wait.
    internal void RefreshLaunchBindingUntil(
        string instanceId,
        long expiresAtMilliseconds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        lock (gate)
        {
            ThrowIfStopped();
            registry.RefreshPending(instanceId, expiresAtMilliseconds);
        }
    }

    internal void CancelLaunchBinding(string instanceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        // Explicit launch-failure cleanup remains legal while the host is
        // stopping, matching the recovered unregister cleanup futures.
        registry.Unregister(instanceId);
    }

    // Registration-scoped retirement for a restored (adopted) binding: a late
    // owner can remove only the exact registration it created.
    internal bool CancelLaunchBinding(string instanceId, ulong registrationSerial)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        return registry.Unregister(instanceId, registrationSerial);
    }

    internal LWBridgeControlPipeRegistry RequireActiveRegistry()
    {
        lock (gate)
        {
            ThrowIfStopped();
            return registry;
        }
    }

    private void ThrowIfStopped()
    {
        if (stopped)
        {
            throw new BridgeCommandException(
                "BRIDGE_STOPPED",
                "The shared bridge host is stopped.");
        }
    }

    // Fatal listener construction/host failures are never treated as healthy
    // idempotent startup. Session-level failures are handled inside the loop.
    private void ThrowIfRpcListenerEnded()
    {
        if (acceptLoopTask is not { IsCompleted: true } ended)
            return;

        // Re-throw the original fatal fault, if present, rather than silently
        // preserving or replacing a dead shared listener.
        ended.GetAwaiter().GetResult();
        throw new InvalidOperationException(
            "The shared bridge RPC listener ended without host shutdown.");
    }

    public void Close()
    {
        LWBridgeControlPipeIsolatedAcceptLoop? loop;
        LWBridgeControlPipeCallRegistry? calls;

        lock (gate)
        {
            if (stopped)
                return;

            stopped = true;
            loop = acceptLoop;
            calls = callRegistry;
            acceptLoop = null;
            acceptLoopTask = null;
            callRegistry = null;
            rpcExpectedBuildId = null;
            rpcExpectedCanonicalClientPath = null;
        }

        calls?.Stop();
        if (loop is not null)
        {
            loop.DisposeAsync()
                .AsTask()
                .GetAwaiter()
                .GetResult();
        }
    }

    public void Dispose() => Close();
}
