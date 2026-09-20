using System.Security.Cryptography;
using System.Text.Json;

namespace LWBridge.Desktop;

// LWB-R7-110 + R7-117: application-global ownership shell for the recovered original
// bridge host. The original application constructs one bridge host during app
// startup (RVA 0x4100B5 -> 0x3C30BE), while profile start consumes that shared
// host. This shell owns the single registry and pipe identity for the desktop
// process. R7-121 composes the recovered listener/RPC layers behind explicit
// startup inputs; normal LWBridgeWindow composition remains disabled until the
// expected client-path source and original command-counter seed are pinned.
internal sealed class LWBridgeControlPipeHostState : IDisposable
{
    private readonly object gate = new();
    private readonly LWBridgeControlPipeRegistry registry;
    private readonly Func<byte[]> pipeTokenEntropyFactory;
    private LWBridgeControlPipeCallRegistry? callRegistry;
    private LWBridgeControlPipeIsolatedAcceptLoop? acceptLoop;
    private Task? acceptLoopTask;
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
                return acceptLoop is not null;
        }
    }

    internal Task StartRpcTransport(
        string expectedBuildId,
        string expectedClientPath,
        ulong initialCommandCounter,
        string? currentUserSid = null,
        Func<long>? clockMilliseconds = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedBuildId);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedClientPath);

        lock (gate)
        {
            ThrowIfStopped();
            if (acceptLoop is not null)
            {
                throw new InvalidOperationException(
                    "The shared bridge RPC transport is already started.");
            }

            string canonicalClientPath =
                LWBridgeControlPipeIsolatedHandshake
                    .CanonicalizeExpectedClientPath(expectedClientPath);
            var calls = new LWBridgeControlPipeCallRegistry(
                initialCommandCounter);
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
            acceptLoopTask = loop.StartAsync();
            return acceptLoopTask;
        }
    }

    internal async Task<JsonElement?> CallLuaAsync(
        string route,
        string functionName,
        JsonElement args,
        long timestamp,
        long createdAt,
        CancellationToken cancellationToken = default)
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
                createdAt).WaitAsync(cancellationToken)
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
        long nowMilliseconds)
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
            registry.Register(profileId, instanceId, token, expiresAt);

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

    internal void CancelLaunchBinding(string instanceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        // Explicit launch-failure cleanup remains legal while the host is
        // stopping, matching the recovered unregister cleanup futures.
        registry.Unregister(instanceId);
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
