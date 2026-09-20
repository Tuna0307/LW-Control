using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json;
using LWBridge.Desktop;
using Microsoft.Win32.SafeHandles;

namespace LWBridge.Desktop.Checks;

internal static class OverviewBridgeRpcSessionTransportChecks
{
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint OpenExisting = 3;

    internal static async Task<JsonElement> RunAsync()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        string sid = identity.User?.Value ??
            throw new InvalidOperationException(
                "Current Windows SID unavailable for RPC session transport check.");
        string processPath = Environment.ProcessPath ??
            throw new InvalidOperationException(
                "Current process path unavailable.");

        string canonicalProcessPath =
            LWBridgeControlPipeIsolatedHandshake.CanonicalizeExpectedClientPath(
                processPath);

        const string Profile = "profile-rpc-session";
        const string Instance = "instance-rpc-session";
        const string Token = "token-rpc-session-123456";
        const string Build = "build-rpc-session";
        const long Now = 3_000_000;

        var routeRegistry = new LWBridgeControlPipeRegistry();
        routeRegistry.Register(
            Profile,
            Instance,
            Token,
            Now + LWBridgeControlPipeRegistry
                .StartupRegistrationLifetimeMilliseconds);

        string pipePath =
            @"\\.\pipe\lwbridge-control-v1-rpc-session-" +
            Guid.NewGuid().ToString("N");

        using SafeFileHandle server =
            LWBridgeControlPipeNativeServer.CreateServerInstance(
                pipePath,
                sid,
                firstServerInstance: true);
        using LWBridgeControlPipePendingConnect connect =
            LWBridgeControlPipeNativeServer.BeginConnect(server);
        using SafeFileHandle client = OpenClient(pipePath);
        Check(
            connect.WaitForCompletion(TimeSpan.FromSeconds(2)) ==
                LWBridgePipeConnectDisposition.Connected,
            "temporary RPC session pipe must connect");

        using var session =
            new LWBridgeControlPipeAcceptedSession(server);

        Task<LWBridgeAuthenticatedConnection> authenticate =
            LWBridgeControlPipeIsolatedHandshake.AuthenticateAsync(
                server,
                routeRegistry,
                Build,
                canonicalProcessPath,
                session,
                nowMilliseconds: Now,
                ackTimestamp: Now + 1,
                streamOverride: session.Stream);

        using var clientStream = new FileStream(
            client,
            FileAccess.ReadWrite,
            bufferSize: 4096,
            isAsync: false);

        byte[] hello = CreateEnvelope(
            LWBridgeControlPipeProtocol.HelloType,
            Profile,
            Instance,
            string.Empty,
            Now,
            new
            {
                token = Token,
                pid = Environment.ProcessId,
                buildId = Build,
            });
        await WriteFrameAsync(clientStream, hello);

        byte[] ackPayload =
            await ReadFrameAsync(clientStream);
        using (JsonDocument ackDoc =
               JsonDocument.Parse(ackPayload))
        {
            Check(
                ackDoc.RootElement.GetProperty("type").GetString() ==
                    LWBridgeControlPipeProtocol.HelloAckType,
                "authenticated client receives hello.ack before RPC transport");
        }

        LWBridgeAuthenticatedConnection authenticated =
            await authenticate;
        session.Bind(authenticated);

        using var calls =
            new LWBridgeControlPipeCallRegistry(
                initialCommandCounter: 6);
        await using LWBridgeControlPipeRpcSessionTransport transport =
            session.AttachRpcTransport(calls);
        Task runTask = transport.StartAsync();

        JsonElement emptyArgs =
            JsonSerializer.SerializeToElement(new { });
        Task<JsonElement?> successTask =
            transport.CallLuaAsync(
                "getStatus",
                emptyArgs,
                timestamp: Now + 10,
                createdAt: Now + 9);

        byte[] commandPayload =
            await ReadFrameAsync(clientStream);
        using JsonDocument commandDocument =
            JsonDocument.Parse(commandPayload);
        JsonElement command = commandDocument.RootElement;
        JsonElement commandBody = command.GetProperty("payload");
        Check(
            command.GetProperty("version").GetInt32() == 1 &&
            command.GetProperty("type").GetString() ==
                LWBridgeControlPipeProtocol.CommandType &&
            command.GetProperty("profileId").GetString() == Profile &&
            command.GetProperty("instanceId").GetString() == Instance &&
            command.GetProperty("requestId").GetString() == "cmd_7" &&
            command.GetProperty("timestamp").GetInt64() == Now + 10 &&
            commandBody.GetProperty("id").GetString() == "cmd_7" &&
            commandBody.GetProperty("kind").GetString() ==
                LWBridgeControlPipeProtocol.CallKind &&
            commandBody.GetProperty("fn").GetString() == "getStatus" &&
            commandBody.GetProperty("createdAt").GetInt64() == Now + 9,
            "first outbound RPC frame matches recovered command/call schema");

        byte[] successResult = CreateEnvelope(
            LWBridgeControlPipeProtocol.ResultType,
            Profile,
            Instance,
            "outer-request-id-is-not-asserted",
            Now + 11,
            new
            {
                id = "cmd_7",
                ok = true,
                result = new { state = "ready" },
            });
        await WriteFrameAsync(clientStream, successResult);

        JsonElement? success = await successTask.WaitAsync(
            TimeSpan.FromSeconds(2));
        Check(
            success?.GetProperty("state").GetString() == "ready" &&
            calls.PendingCount == 0,
            "inbound result payload.id completes first pending call");

        Task<JsonElement?> failureTask =
            transport.CallLuaAsync(
                "getStatus",
                emptyArgs,
                timestamp: Now + 20,
                createdAt: Now + 19);
        byte[] failureCommand =
            await ReadFrameAsync(clientStream);
        using (JsonDocument failureCommandDoc =
               JsonDocument.Parse(failureCommand))
        {
            Check(
                failureCommandDoc.RootElement
                    .GetProperty("payload")
                    .GetProperty("id")
                    .GetString() == "cmd_8",
                "second outbound call advances monotonic command ID");
        }

        byte[] failedResult = CreateEnvelope(
            LWBridgeControlPipeProtocol.ResultType,
            Profile,
            Instance,
            "cmd_8",
            Now + 21,
            new
            {
                id = "cmd_8",
                ok = false,
                error = "synthetic lua failure",
            });
        await WriteFrameAsync(clientStream, failedResult);
        await ExpectBridgeErrorAsync(
            "LUA_CALL_FAILED",
            "transport-correlated Lua failure",
            async () => _ = await failureTask.WaitAsync(
                TimeSpan.FromSeconds(2)));

        byte[] heartbeat = CreateEnvelope(
            "heartbeat",
            Profile,
            Instance,
            string.Empty,
            Now + 30,
            new { });
        await WriteFrameAsync(clientStream, heartbeat);
        await WaitUntilAsync(
            () => transport.HeartbeatsObserved >= 1,
            TimeSpan.FromSeconds(2),
            "heartbeat observation");

        byte[] unknownResult = CreateEnvelope(
            LWBridgeControlPipeProtocol.ResultType,
            Profile,
            Instance,
            "different-outer-id",
            Now + 31,
            new
            {
                id = "cmd_999999",
                ok = true,
                result = new { ignored = true },
            });
        await WriteFrameAsync(clientStream, unknownResult);
        await WaitUntilAsync(
            () => transport.UnknownResults >= 1,
            TimeSpan.FromSeconds(2),
            "unknown-result observation");

        Check(
            transport.OutboundFramesWritten == 2 &&
            transport.InboundFramesRead >= 4 &&
            transport.ResultsCorrelated == 2 &&
            transport.UnknownResults == 1 &&
            transport.HeartbeatsObserved == 1,
            "session transport counters reflect command/result/heartbeat flow");

        await transport.StopAsync();
        await runTask.WaitAsync(TimeSpan.FromSeconds(2));

        Check(calls.PendingCount == 0,
            "transport proof ends with no outstanding Lua calls");
        calls.Stop();
        routeRegistry.Unregister(Instance);

        string repo = FindRepoRoot();
        string hostSource = File.ReadAllText(Path.Combine(
            repo,
            "src",
            "LWBridge.Desktop",
            "LWBridgeControlPipeHostState.cs"));
        string windowSource = File.ReadAllText(Path.Combine(
            repo,
            "src",
            "LWBridge.Desktop",
            "LWBridgeWindow.cs"));
        Check(
            !hostSource.Contains(
                "LWBridgeControlPipeRpcSessionTransport",
                StringComparison.Ordinal) &&
            !windowSource.Contains(
                "LWBridgeControlPipeRpcSessionTransport",
                StringComparison.Ordinal),
            "normal application composition remains disconnected from isolated RPC transport");

        return JsonSerializer.SerializeToElement(new
        {
            ok = true,
            finding = "LWB-R7-120",
            windowsProof = new
            {
                authenticated = true,
                commandFramesWritten =
                    transport.OutboundFramesWritten,
                inboundFramesRead =
                    transport.InboundFramesRead,
                resultsCorrelated =
                    transport.ResultsCorrelated,
                unknownResults =
                    transport.UnknownResults,
                heartbeatsObserved =
                    transport.HeartbeatsObserved,
                successResult = true,
                luaFailureResult = true,
                outerRequestIdDifferentButPayloadIdCorrelated = true,
            },
            limits = new
            {
                outboundItems =
                    LWBridgeControlPipeTransportLimits
                        .OutboundQueueItemCapacity,
                outboundBytes =
                    LWBridgeControlPipeTransportLimits
                        .OutboundByteBudget,
                inboundItems =
                    LWBridgeControlPipeTransportLimits
                        .InboundQueueItemCapacity,
                inboundBytes =
                    LWBridgeControlPipeTransportLimits
                        .InboundQueueByteBudget,
                writeTimeoutSeconds =
                    LWBridgeControlPipeTransportLimits
                        .WriteTimeout.TotalSeconds,
                inboundBackpressureSeconds =
                    LWBridgeControlPipeTransportLimits
                        .InboundBackpressureTimeout.TotalSeconds,
                idleSeconds =
                    LWBridgeControlPipeTransportLimits
                        .IdleActivityTimeout.TotalSeconds,
                luaCallTimeoutMilliseconds =
                    LWBridgeControlPipeCallRegistry
                        .CallTimeoutMilliseconds,
            },
            boundary = new
            {
                normalHostRunsRpcSession = false,
                productionPendingExposed = false,
                productionCallLuaEnabled = false,
            },
        }, JsonOptions.Default);
    }

    private static byte[] CreateEnvelope(
        string type,
        string profileId,
        string instanceId,
        string requestId,
        long timestamp,
        object payload) =>
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            version = LWBridgeControlPipeProtocol.ProtocolVersion,
            type,
            profileId,
            instanceId,
            requestId,
            timestamp,
            payload,
        });

    private static async Task WriteFrameAsync(
        Stream stream,
        byte[] payload)
    {
        byte[] frame =
            LWBridgeControlPipeProtocol.EncodeFrame(payload);
        await stream.WriteAsync(frame);
        await stream.FlushAsync();
    }

    private static async Task<byte[]> ReadFrameAsync(
        Stream stream)
    {
        byte[] prefix =
            new byte[LWBridgeControlPipeProtocol.FramePrefixLength];
        await ReadExactlyAsync(stream, prefix)
            .WaitAsync(TimeSpan.FromSeconds(2));
        uint length =
            BinaryPrimitives.ReadUInt32LittleEndian(prefix);
        Check(
            length is > 0 and <=
                LWBridgeControlPipeProtocol.MaxFramePayloadLength,
            "test client frame length remains inside recovered range");

        byte[] payload =
            new byte[checked((int)length)];
        await ReadExactlyAsync(stream, payload)
            .WaitAsync(TimeSpan.FromSeconds(2));
        return payload;
    }

    private static async Task ReadExactlyAsync(
        Stream stream,
        Memory<byte> buffer)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int read =
                await stream.ReadAsync(buffer[offset..]);
            if (read == 0)
            {
                throw new EndOfStreamException(
                    "temporary RPC client pipe closed during frame read");
            }

            offset += read;
        }
    }

    private static SafeFileHandle OpenClient(
        string pipePath)
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
            return handle;

        int error = Marshal.GetLastWin32Error();
        handle.Dispose();
        throw new Win32Exception(
            error,
            $"CreateFileW RPC client open failed (Win32 {error}).");
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
            {
                throw new TimeoutException(
                    "Overview bridge RPC session transport check timed out: " +
                    name);
            }

            await Task.Delay(10);
        }
    }

    private static async Task ExpectBridgeErrorAsync(
        string expectedCode,
        string name,
        Func<Task> action)
    {
        try
        {
            await action();
            throw new InvalidOperationException(
                $"Overview bridge RPC session transport check failed: {name} unexpectedly succeeded");
        }
        catch (BridgeCommandException error)
        {
            Check(
                error.Code == expectedCode,
                $"{name} expected {expectedCode}, got {error.Code}");
        }
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? current =
            new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(
                    Path.Combine(current.FullName, "task.md")) &&
                Directory.Exists(
                    Path.Combine(
                        current.FullName,
                        "src",
                        "LWBridge.Desktop")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            "could not locate LW-Control repository root");
    }

    private static void Check(
        bool condition,
        string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(
                "Overview bridge RPC session transport check failed: " +
                message);
        }
    }

    [DllImport(
        "kernel32.dll",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);
}
