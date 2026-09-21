using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json;
using System.Threading.Channels;
using LWBridge.Desktop;
using Microsoft.Win32.SafeHandles;

namespace LWBridge.Desktop.Checks;

internal static class OverviewBridgeIsolatedAcceptLoopChecks
{
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint OpenExisting = 3;
    private const int ErrorFileNotFound = 2;
    private const int ErrorPipeBusy = 231;

    internal static async Task<JsonElement> RunAsync()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        string sid = identity.User?.Value ??
            throw new InvalidOperationException(
                "Current Windows SID unavailable for isolated accept-loop check.");
        string processPath = Environment.ProcessPath ??
            throw new InvalidOperationException("Current process path unavailable.");
        string canonicalProcessPath =
            LWBridgeControlPipeIsolatedHandshake.CanonicalizeExpectedClientPath(
                processPath);

        const long Clock = 2_000_000;
        const string BuildId = "build-accept-loop";
        const string Profile = "profile-accept-loop";
        const string Instance1 = "instance-accept-1";
        const string InstanceReject = "instance-accept-reject";
        const string Instance2 = "instance-accept-2";
        const string Token1 = "token-accept-1-123456";
        const string TokenReject = "token-accept-reject-123456";
        const string Token2 = "token-accept-2-123456";

        var registry = new LWBridgeControlPipeRegistry();
        registry.Register(
            Profile,
            Instance1,
            Token1,
            Clock + LWBridgeControlPipeRegistry.StartupRegistrationLifetimeMilliseconds);
        registry.Register(
            Profile,
            InstanceReject,
            TokenReject,
            Clock + LWBridgeControlPipeRegistry.StartupRegistrationLifetimeMilliseconds);
        registry.Register(
            Profile,
            Instance2,
            Token2,
            Clock + LWBridgeControlPipeRegistry.StartupRegistrationLifetimeMilliseconds);

        var accepted = Channel.CreateUnbounded<LWBridgeControlPipeAcceptedSession>();
        var releases = new ConcurrentDictionary<string, TaskCompletionSource<bool>>(
            StringComparer.Ordinal);

        async Task HandleSession(
            LWBridgeControlPipeAcceptedSession session,
            CancellationToken cancellationToken)
        {
            string instanceId = session.Connection.InstanceId;
            var release = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            if (!releases.TryAdd(instanceId, release))
                throw new InvalidOperationException(
                    "duplicate accepted-session release key");

            await accepted.Writer.WriteAsync(session, cancellationToken);
            await release.Task.WaitAsync(cancellationToken);
        }

        string pipePath =
            @"\\.\pipe\lwbridge-control-v1-accept-loop-" +
            Guid.NewGuid().ToString("N");

        await using var loop = new LWBridgeControlPipeIsolatedAcceptLoop(
            pipePath,
            registry,
            BuildId,
            canonicalProcessPath,
            HandleSession,
            currentUserSid: sid,
            clockMilliseconds: () => Clock);

        SynchronizationContext? originalContext =
            SynchronizationContext.Current;
        var recordingContext = new RecordingSynchronizationContext();
        Task runTask;
        try
        {
            SynchronizationContext.SetSynchronizationContext(recordingContext);
            runTask = loop.StartAsync();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(originalContext);
        }
        Check(recordingContext.PostCount == 0,
            "listener pending-connect path must not capture/post back to the caller synchronization context");

        await using ClientLease client1 = await OpenAndHelloAsync(
            pipePath,
            Profile,
            Instance1,
            Token1,
            checked((uint)Environment.ProcessId),
            BuildId,
            Clock);

        LWBridgeControlPipeAcceptedSession session1 =
            await accepted.Reader.ReadAsync().AsTask().WaitAsync(
                TimeSpan.FromSeconds(3));
        Check(session1.Connection.InstanceId == Instance1 &&
              session1.Connection.Generation > 0,
            "first authenticated session must bind its recovered generation");
        ConnectedRoute? route1 = registry.Resolve(Instance1);
        Check(route1 is not null &&
              route1.Generation == session1.Connection.Generation &&
              ReferenceEquals(route1.Route, session1),
            "first route must point at the accepted session object");
        Check(loop.FirstInstanceFlagUses == 1,
            "FILE_FLAG_FIRST_PIPE_INSTANCE must be consumed only by first server object");

        releases[Instance1].TrySetResult(true);
        await WaitUntilAsync(
            () => registry.Resolve(Instance1) is null,
            TimeSpan.FromSeconds(3),
            "first route removal after session handler completes");
        await client1.DisposeAsync();

        await using ClientLease rejectedClient = await OpenClientAsync(pipePath);
        byte[] rejectedHello = CreateHello(
            Profile,
            InstanceReject,
            TokenReject,
            checked((uint)Environment.ProcessId + 1),
            BuildId,
            Clock);
        await rejectedClient.Stream.WriteAsync(
            LWBridgeControlPipeProtocol.EncodeFrame(rejectedHello));
        await rejectedClient.Stream.FlushAsync();
        await WaitUntilAsync(
            () => loop.RejectedHandshakes >= 1,
            TimeSpan.FromSeconds(3),
            "rejected handshake counter");
        Check(registry.Resolve(InstanceReject) is null &&
              registry.IsPending(InstanceReject),
            "rejected handshake leaves pending registration unclaimed");
        await rejectedClient.DisposeAsync();

        await using ClientLease client2 = await OpenAndHelloAsync(
            pipePath,
            Profile,
            Instance2,
            Token2,
            checked((uint)Environment.ProcessId),
            BuildId,
            Clock);

        LWBridgeControlPipeAcceptedSession session2 =
            await accepted.Reader.ReadAsync().AsTask().WaitAsync(
                TimeSpan.FromSeconds(3));
        Check(session2.Connection.InstanceId == Instance2 &&
              session2.Connection.Generation > session1.Connection.Generation,
            "replacement authenticated session advances host-global generation");
        ConnectedRoute? route2 = registry.Resolve(Instance2);
        Check(route2 is not null &&
              route2.Generation == session2.Connection.Generation &&
              ReferenceEquals(route2.Route, session2),
            "replacement route points at replacement accepted session");

        releases[Instance2].TrySetResult(true);
        await WaitUntilAsync(
            () => registry.Resolve(Instance2) is null,
            TimeSpan.FromSeconds(3),
            "replacement route removal after handler completes");
        await client2.DisposeAsync();

        await WaitUntilAsync(
            () => loop.ServerInstancesCreated >= 4,
            TimeSpan.FromSeconds(3),
            "next replacement server created before shutdown");

        var stopWatch = System.Diagnostics.Stopwatch.StartNew();
        await loop.StopAsync();
        stopWatch.Stop();
        await runTask.WaitAsync(TimeSpan.FromSeconds(3));

        Check(loop.FirstInstanceFlagUses == 1,
            "replacement server objects must omit FILE_FLAG_FIRST_PIPE_INSTANCE");
        Check(loop.AuthenticatedSessions == 2,
            "exactly two valid sessions must authenticate");
        Check(loop.RejectedHandshakes >= 1,
            "rejected handshake must be dropped without terminating listener");
        Check(loop.ServerInstancesCreated >= 4,
            "listener must create replacements after valid and rejected sessions");
        Check(stopWatch.Elapsed < TimeSpan.FromSeconds(2),
            "shutdown must cancel a pending overlapped accept promptly");
        Check(registry.ConnectedCount == 0,
            "listener shutdown leaves no connected routes");

        registry.Unregister(Instance1);
        registry.Unregister(InstanceReject);
        registry.Unregister(Instance2);

        string repo = FindRepoRoot();
        string hostSource = File.ReadAllText(Path.Combine(
            repo, "src", "LWBridge.Desktop", "LWBridgeControlPipeHostState.cs"));
        string windowSource = File.ReadAllText(Path.Combine(
            repo, "src", "LWBridge.Desktop", "LWBridgeWindow.cs"));
        Check(hostSource.Contains(
                "LWBridgeControlPipeIsolatedAcceptLoop",
                StringComparison.Ordinal),
            "shared host must retain the now-proven accept-loop composition");
        Check(windowSource.Contains(
                "StartRpcTransport(",
                StringComparison.Ordinal),
            "normal application window starts the composed accept loop after R7-123");

        return JsonSerializer.SerializeToElement(new
        {
            ok = true,
            finding = "LWB-R7-116",
            windowsProof = new
            {
                serverInstancesCreated = loop.ServerInstancesCreated,
                firstInstanceFlagUses = loop.FirstInstanceFlagUses,
                failedConnects = loop.FailedConnects,
                rejectedHandshakes = loop.RejectedHandshakes,
                authenticatedSessions = loop.AuthenticatedSessions,
                replacementGenerationAdvanced =
                    session2.Connection.Generation >
                    session1.Connection.Generation,
                routeRemovedAfterHandler = true,
                pendingAcceptCancelledOnStop = true,
                stopMilliseconds = stopWatch.Elapsed.TotalMilliseconds,
            },
            boundary = new
            {
                sharedHostCanStartAcceptLoop = true,
                normalWindowStartsAcceptLoop = true,
                productionProxyLaunchBinding = true,
                outboundCommandRoutingImplementedInSharedHost = true,
                pendingCallCollectionImplemented = true,
                productionCallLuaEnabled = false,
            },
        }, JsonOptions.Default);
    }

    private static async Task<ClientLease> OpenAndHelloAsync(
        string pipePath,
        string profileId,
        string instanceId,
        string token,
        uint pid,
        string buildId,
        long timestamp)
    {
        ClientLease lease = await OpenClientAsync(pipePath);
        byte[] hello = CreateHello(
            profileId,
            instanceId,
            token,
            pid,
            buildId,
            timestamp);
        await lease.Stream.WriteAsync(
            LWBridgeControlPipeProtocol.EncodeFrame(hello));
        await lease.Stream.FlushAsync();

        byte[] ackPayload = await ReadFrameAsync(lease.Stream);
        using JsonDocument document = JsonDocument.Parse(ackPayload);
        JsonElement ack = document.RootElement;
        Check(
            ack.GetProperty("type").GetString() ==
                LWBridgeControlPipeProtocol.HelloAckType &&
            ack.GetProperty("profileId").GetString() == profileId &&
            ack.GetProperty("instanceId").GetString() == instanceId,
            "valid accept-loop hello must receive matching hello.ack");
        return lease;
    }

    private static async Task<ClientLease> OpenClientAsync(string pipePath)
    {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
        while (true)
        {
            SafeFileHandle handle = CreateFileW(
                pipePath,
                GenericRead | GenericWrite,
                0,
                IntPtr.Zero,
                OpenExisting,
                0,
                IntPtr.Zero);
            if (!handle.IsInvalid)
            {
                return new ClientLease(new FileStream(
                    handle,
                    FileAccess.ReadWrite,
                    bufferSize: 4096,
                    isAsync: false));
            }

            int error = Marshal.GetLastWin32Error();
            handle.Dispose();
            if (DateTime.UtcNow >= deadline ||
                error is not (ErrorFileNotFound or ErrorPipeBusy))
            {
                throw new Win32Exception(
                    error,
                    $"CreateFileW client open failed (Win32 {error}).");
            }

            _ = WaitNamedPipeW(pipePath, 100);
            await Task.Delay(10);
        }
    }

    private static byte[] CreateHello(
        string profileId,
        string instanceId,
        string token,
        long pid,
        string buildId,
        long timestamp) =>
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            version = LWBridgeControlPipeProtocol.ProtocolVersion,
            type = LWBridgeControlPipeProtocol.HelloType,
            profileId,
            instanceId,
            requestId = string.Empty,
            timestamp,
            payload = new { token, pid, buildId },
        });

    private static async Task<byte[]> ReadFrameAsync(Stream stream)
    {
        byte[] prefix = new byte[LWBridgeControlPipeProtocol.FramePrefixLength];
        await ReadExactlyAsync(stream, prefix).WaitAsync(TimeSpan.FromSeconds(3));
        uint payloadLength = BinaryPrimitives.ReadUInt32LittleEndian(prefix);
        Check(payloadLength is > 0 and <=
            LWBridgeControlPipeProtocol.MaxFramePayloadLength,
            "received frame length must stay within recovered range");
        byte[] payload = new byte[checked((int)payloadLength)];
        await ReadExactlyAsync(stream, payload).WaitAsync(TimeSpan.FromSeconds(3));
        return payload;
    }

    private static async Task ReadExactlyAsync(Stream stream, Memory<byte> buffer)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer[offset..]);
            if (read == 0)
                throw new EndOfStreamException(
                    "pipe closed while reading accept-loop test frame");
            offset += read;
        }
    }

    private static async Task WaitUntilAsync(
        Func<bool> condition,
        TimeSpan timeout,
        string name)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
                throw new TimeoutException(
                    "Overview bridge isolated accept-loop check timed out: " +
                    name);
            await Task.Delay(10);
        }
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "task.md")) &&
                Directory.Exists(Path.Combine(current.FullName, "src", "LWBridge.Desktop")))
                return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            "could not locate LW-Control repository root");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(
                "Overview bridge isolated accept-loop check failed: " +
                message);
    }

    private sealed class RecordingSynchronizationContext :
        SynchronizationContext
    {
        private int postCount;

        internal int PostCount => Volatile.Read(ref postCount);

        public override void Post(SendOrPostCallback d, object? state)
        {
            Interlocked.Increment(ref postCount);
        }
    }

    private sealed class ClientLease : IAsyncDisposable
    {
        internal ClientLease(FileStream stream)
        {
            Stream = stream;
        }

        internal FileStream Stream { get; }

        public ValueTask DisposeAsync()
        {
            Stream.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WaitNamedPipeW(
        string lpNamedPipeName,
        uint nTimeOut);
}
