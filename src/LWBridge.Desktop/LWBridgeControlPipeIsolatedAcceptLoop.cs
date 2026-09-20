using System.IO.Pipes;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace LWBridge.Desktop;

// LWB-R7-116 + R7-123: host-global accept-loop lifecycle. R7-116 proved the
// native server/connect and authenticated hello path in isolation; R7-123
// composes that proven loop into the application-owned shared host.
internal sealed class LWBridgeControlPipeIsolatedAcceptLoop : IAsyncDisposable
{
    private readonly string pipePath;
    private readonly string currentUserSid;
    private readonly LWBridgeControlPipeRegistry registry;
    private readonly string expectedBuildId;
    private readonly string expectedCanonicalClientPath;
    private readonly Func<LWBridgeControlPipeAcceptedSession, CancellationToken, Task>
        sessionHandler;
    private readonly Func<long> clockMilliseconds;
    private readonly object gate = new();
    private readonly CancellationTokenSource stopSource = new();

    private Task? runTask;
    private bool disposed;
    private int serverInstancesCreated;
    private int firstInstanceFlagUses;
    private int failedConnects;
    private int rejectedHandshakes;
    private int authenticatedSessions;

    internal LWBridgeControlPipeIsolatedAcceptLoop(
        string pipePath,
        LWBridgeControlPipeRegistry registry,
        string expectedBuildId,
        string expectedCanonicalClientPath,
        Func<LWBridgeControlPipeAcceptedSession, CancellationToken, Task>
            sessionHandler,
        string? currentUserSid = null,
        Func<long>? clockMilliseconds = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipePath);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedBuildId);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedCanonicalClientPath);
        ArgumentNullException.ThrowIfNull(sessionHandler);

        this.pipePath = pipePath;
        this.registry = registry;
        this.expectedBuildId = expectedBuildId;
        this.expectedCanonicalClientPath = expectedCanonicalClientPath;
        this.sessionHandler = sessionHandler;
        this.clockMilliseconds = clockMilliseconds ??
            (() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        if (string.IsNullOrWhiteSpace(currentUserSid))
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            this.currentUserSid = identity.User?.Value ??
                throw new InvalidOperationException(
                    "Current Windows SID unavailable for bridge listener.");
        }
        else
        {
            this.currentUserSid = currentUserSid;
        }
    }

    public int ServerInstancesCreated => Volatile.Read(ref serverInstancesCreated);
    public int FirstInstanceFlagUses => Volatile.Read(ref firstInstanceFlagUses);
    public int FailedConnects => Volatile.Read(ref failedConnects);
    public int RejectedHandshakes => Volatile.Read(ref rejectedHandshakes);
    public int AuthenticatedSessions => Volatile.Read(ref authenticatedSessions);

    internal Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (runTask is not null)
                return runTask;

            CancellationTokenSource linked =
                CancellationTokenSource.CreateLinkedTokenSource(
                    stopSource.Token,
                    cancellationToken);
            runTask = RunAsync(linked);
            return runTask;
        }
    }

    internal async Task StopAsync()
    {
        Task? running;
        lock (gate)
        {
            if (disposed)
                return;
            stopSource.Cancel();
            running = runTask;
        }

        if (running is not null)
        {
            try
            {
                await running.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stopSource.IsCancellationRequested)
            {
            }
        }
    }

    private async Task RunAsync(CancellationTokenSource linkedSource)
    {
        using (linkedSource)
        {
            CancellationToken cancellationToken = linkedSource.Token;
            while (!cancellationToken.IsCancellationRequested)
            {
                bool firstServerInstance = ServerInstancesCreated == 0;
                SafeFileHandle? server = null;
                LWBridgeControlPipePendingConnect? pendingConnect = null;
                try
                {
                    server = LWBridgeControlPipeNativeServer.CreateServerInstance(
                        pipePath,
                        currentUserSid,
                        firstServerInstance);
                    Interlocked.Increment(ref serverInstancesCreated);
                    if (firstServerInstance)
                        Interlocked.Increment(ref firstInstanceFlagUses);

                    pendingConnect =
                        LWBridgeControlPipeNativeServer.BeginConnect(server);

                    LWBridgePipeConnectDisposition disposition =
                        await WaitForConnectAsync(
                            pendingConnect,
                            cancellationToken).ConfigureAwait(false);
                    if (disposition == LWBridgePipeConnectDisposition.Failed)
                    {
                        Interlocked.Increment(ref failedConnects);
                        continue;
                    }

                    using var session =
                        new LWBridgeControlPipeAcceptedSession(server);
                    long now = clockMilliseconds();

                    LWBridgeAuthenticatedConnection authenticated;
                    try
                    {
                        authenticated =
                            await LWBridgeControlPipeIsolatedHandshake
                                .AuthenticateAsync(
                                    server,
                                    registry,
                                    expectedBuildId,
                                    expectedCanonicalClientPath,
                                    session,
                                    nowMilliseconds: now,
                                    ackTimestamp: now,
                                    cancellationToken: cancellationToken,
                                    streamOverride: session.Stream)
                                .ConfigureAwait(false);
                    }
                    catch (BridgeCommandException error) when (
                        error.Code is "PIPE_HANDSHAKE_REJECTED" or
                                      "PIPE_HANDSHAKE_TIMEOUT")
                    {
                        Interlocked.Increment(ref rejectedHandshakes);
                        continue;
                    }
                    catch (InvalidDataException)
                    {
                        Interlocked.Increment(ref rejectedHandshakes);
                        continue;
                    }
                    catch (EndOfStreamException)
                    {
                        Interlocked.Increment(ref rejectedHandshakes);
                        continue;
                    }

                    session.Bind(authenticated);
                    Interlocked.Increment(ref authenticatedSessions);

                    try
                    {
                        await sessionHandler(session, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    finally
                    {
                        _ = registry.RemoveConnected(
                            authenticated.InstanceId,
                            authenticated.Generation);
                    }
                }
                catch (OperationCanceledException) when (
                    cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                finally
                {
                    pendingConnect?.Dispose();
                    server?.Dispose();
                }
            }
        }
    }

    private static async Task<LWBridgePipeConnectDisposition> WaitForConnectAsync(
        LWBridgeControlPipePendingConnect pending,
        CancellationToken cancellationToken)
    {
        LWBridgePipeConnectDisposition disposition = pending.InitialDisposition;
        if (disposition != LWBridgePipeConnectDisposition.Pending)
            return disposition;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            disposition = pending.WaitForCompletion(TimeSpan.FromMilliseconds(50));
            if (disposition != LWBridgePipeConnectDisposition.Pending)
                return disposition;
            await Task.Yield();
        }
    }

    public async ValueTask DisposeAsync()
    {
        lock (gate)
        {
            if (disposed)
                return;
        }

        await StopAsync().ConfigureAwait(false);
        lock (gate)
            disposed = true;
        stopSource.Dispose();
    }
}

internal sealed class LWBridgeControlPipeAcceptedSession : IDisposable
{
    private readonly SafePipeHandle streamHandle;
    private readonly TaskCompletionSource<LWBridgeControlPipeRpcSessionTransport>
        rpcTransportReady = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
    private LWBridgeAuthenticatedConnection? connection;
    private LWBridgeControlPipeRpcSessionTransport? rpcTransport;

    internal LWBridgeControlPipeAcceptedSession(SafeFileHandle pipeHandle)
    {
        PipeHandle = pipeHandle;
        streamHandle = new SafePipeHandle(
            pipeHandle.DangerousGetHandle(),
            ownsHandle: false);
        Stream = new NamedPipeServerStream(
            PipeDirection.InOut,
            isAsync: true,
            isConnected: true,
            streamHandle);
    }

    internal SafeFileHandle PipeHandle { get; }

    internal NamedPipeServerStream Stream { get; }

    internal LWBridgeAuthenticatedConnection Connection =>
        connection ?? throw new InvalidOperationException(
            "Bridge accepted session has not completed authentication.");

    internal LWBridgeControlPipeRpcSessionTransport RpcTransport =>
        rpcTransport ?? throw new InvalidOperationException(
            "Bridge accepted session RPC transport is not attached.");

    internal void Bind(LWBridgeAuthenticatedConnection value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (Interlocked.CompareExchange(ref connection, value, null) is not null)
            throw new InvalidOperationException(
                "Bridge accepted session authentication is already bound.");
    }

    internal LWBridgeControlPipeRpcSessionTransport AttachRpcTransport(
        LWBridgeControlPipeCallRegistry calls)
    {
        _ = Connection;
        var transport =
            new LWBridgeControlPipeRpcSessionTransport(this, calls);
        if (Interlocked.CompareExchange(
                ref rpcTransport,
                transport,
                null) is not null)
        {
            throw new InvalidOperationException(
                "Bridge accepted session RPC transport is already attached.");
        }

        rpcTransportReady.TrySetResult(transport);
        return transport;
    }

    internal Task<LWBridgeControlPipeRpcSessionTransport>
        WaitForRpcTransportAsync(CancellationToken cancellationToken) =>
        rpcTransportReady.Task.WaitAsync(cancellationToken);

    public void Dispose()
    {
        Stream.Dispose();
        streamHandle.Dispose();
    }
}
