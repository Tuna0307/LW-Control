using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json;
using LWBridge.Desktop;
using Microsoft.Win32.SafeHandles;

namespace LWBridge.Desktop.Checks;

internal static class OverviewStatusTransportChecks
{
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint OpenExisting = 3;

    internal static async Task<JsonElement> RunAsync()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        string sid = identity.User?.Value ??
            throw new InvalidOperationException("Current Windows SID unavailable.");
        string processPath = Environment.ProcessPath ??
            throw new InvalidOperationException("Current process path unavailable.");

        const long Now = 5_000_000;
        string pipePath =
            @"\\.\pipe\lwbridge-control-v1-status-" +
            Guid.NewGuid().ToString("N");

        var config = new LocalConfigStore(persistent: false);
        var registry = new LWBridgeControlPipeRegistry();
        using var host = new LWBridgeControlPipeHostState(
            pipePath,
            registry,
            pipeTokenEntropyFactory: () =>
                Enumerable.Range(32, 32)
                    .Select(value => (byte)value)
                    .ToArray());

        Task listener = host.StartRpcTransport(
            OverviewLifecycleService.BridgeVersion,
            processPath,
            currentUserSid: sid,
            clockMilliseconds: () => Now);

        var backend = new LWBridgeBackend(
            config,
            bridgeHostState: host);

        const string Instance = "status-transport-instance";
        LWBridgeControlPipeLaunchBinding binding =
            host.PrepareLaunchBinding(
                backend.ProfileId,
                Instance,
                OverviewLifecycleService.BridgeVersion,
                Now);

        using SafeFileHandle client =
            OpenClientWithRetry(pipePath, TimeSpan.FromSeconds(2));
        using var clientStream = new FileStream(
            client,
            FileAccess.ReadWrite,
            bufferSize: 4096,
            isAsync: false);

        await WriteFrameAsync(
            clientStream,
            CreateEnvelope(
                LWBridgeControlPipeProtocol.HelloType,
                backend.ProfileId,
                Instance,
                string.Empty,
                Now,
                new
                {
                    token = binding.PipeToken,
                    pid = Environment.ProcessId,
                    buildId = OverviewLifecycleService.BridgeVersion,
                }));
        byte[] ack = await ReadFrameAsync(clientStream);
        using (JsonDocument ackDoc = JsonDocument.Parse(ack))
        {
            Check(
                ackDoc.RootElement.GetProperty("type").GetString() ==
                    LWBridgeControlPipeProtocol.HelloAckType,
                "backend proof receives hello.ack");
        }

        await WaitUntilAsync(
            () => host.ConnectedRouteCount == 1,
            TimeSpan.FromSeconds(2),
            "connected route");

        JsonElement profilePayload =
            JsonSerializer.SerializeToElement(
                new { profileId = backend.ProfileId });
        JsonElement before = JsonSerializer.SerializeToElement(
            await backend.InvokeAsync(
                "get_status",
                profilePayload,
                CancellationToken.None),
            JsonOptions.Default);
        Check(
            before.GetProperty("pending").GetInt32() == 0,
            "get_status.pending starts at live host count zero");

        JsonElement callPayload =
            JsonSerializer.SerializeToElement(
                new
                {
                    profileId = backend.ProfileId,
                    fnName = "getStatus",
                    args = new { },
                },
                JsonOptions.Default);
        Task<object?> callTask = backend.InvokeAsync(
            "call_lua",
            callPayload,
            CancellationToken.None);

        byte[] commandPayload = await ReadFrameAsync(clientStream);
        using JsonDocument commandDoc =
            JsonDocument.Parse(commandPayload);
        JsonElement command = commandDoc.RootElement;
        Check(
            command.GetProperty("type").GetString() ==
                LWBridgeControlPipeProtocol.CommandType &&
            command.GetProperty("payload")
                .GetProperty("id").GetString() == "cmd_1" &&
            command.GetProperty("payload")
                .GetProperty("fn").GetString() == "getStatus" &&
            command.GetProperty("payload")
                .GetProperty("args").GetRawText() == "{}",
            "backend exact getStatus call reaches recovered command/call frame");

        JsonElement during = JsonSerializer.SerializeToElement(
            await backend.InvokeAsync(
                "get_status",
                profilePayload,
                CancellationToken.None),
            JsonOptions.Default);
        Check(
            during.GetProperty("pending").GetInt32() == 1,
            "get_status.pending reports the exact outstanding Lua call");

        await WriteFrameAsync(
            clientStream,
            CreateEnvelope(
                LWBridgeControlPipeProtocol.ResultType,
                backend.ProfileId,
                Instance,
                "outer-not-required",
                Now + 1,
                new
                {
                    id = "cmd_1",
                    ok = true,
                    result = new { state = "lua-ready" },
                }));

        JsonElement result = JsonSerializer.SerializeToElement(
            await callTask.WaitAsync(TimeSpan.FromSeconds(2)),
            JsonOptions.Default);
        Check(
            result.GetProperty("state").GetString() == "lua-ready",
            "backend returns the correlated Lua result");

        JsonElement after = JsonSerializer.SerializeToElement(
            await backend.InvokeAsync(
                "get_status",
                profilePayload,
                CancellationToken.None),
            JsonOptions.Default);
        Check(
            after.GetProperty("pending").GetInt32() == 0,
            "get_status.pending returns to zero after correlation");

        await ExpectBridgeErrorAsync(
            "COMMAND_NOT_IMPLEMENTED",
            "other Lua function blocked",
            () => backend.InvokeAsync(
                "call_lua",
                JsonSerializer.SerializeToElement(
                    new
                    {
                        profileId = backend.ProfileId,
                        fnName = "otherFunction",
                        args = new { },
                    }),
                CancellationToken.None));

        await ExpectBridgeErrorAsync(
            "COMMAND_NOT_IMPLEMENTED",
            "getStatus non-empty args blocked",
            () => backend.InvokeAsync(
                "call_lua",
                JsonSerializer.SerializeToElement(
                    new
                    {
                        profileId = backend.ProfileId,
                        fnName = "getStatus",
                        args = new { refresh = true },
                    }),
                CancellationToken.None));

        var noHostBackend =
            new LWBridgeBackend(new LocalConfigStore(persistent: false));
        await ExpectBridgeErrorAsync(
            "LUA_CALL_FAILED",
            "exact getStatus without transport",
            () => noHostBackend.InvokeAsync(
                "call_lua",
                JsonSerializer.SerializeToElement(
                    new
                    {
                        profileId = noHostBackend.ProfileId,
                        fnName = "getStatus",
                        args = new { },
                    }),
                CancellationToken.None));

        await host.StopRpcTransportAsync();
        await listener.WaitAsync(TimeSpan.FromSeconds(2));
        Check(
            host.PendingCallCount is null &&
            host.ConnectedRouteCount == 0,
            "backend proof tears down shared transport cleanly");

        return JsonSerializer.SerializeToElement(new
        {
            ok = true,
            finding = "LWB-R7-124",
            proof = new
            {
                pendingSequence = new[] { 0, 1, 0 },
                commandId = "cmd_1",
                functionName = "getStatus",
                args = "{}",
                successResult = "lua-ready",
                otherFunctionBlocked = true,
                nonEmptyArgsBlocked = true,
                noTransportError = "LUA_CALL_FAILED",
            },
            boundary = new
            {
                genericCallLuaEnabled = false,
                onlyGetStatusEmptyArgsEnabled = true,
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

    private static async Task<byte[]> ReadFrameAsync(Stream stream)
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
            "frame length remains in recovered range");

        byte[] payload = new byte[checked((int)length)];
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
            int read = await stream.ReadAsync(buffer[offset..]);
            if (read == 0)
                throw new EndOfStreamException(
                    "status transport client closed during frame read");
            offset += read;
        }
    }

    private static SafeFileHandle OpenClientWithRetry(
        string pipePath,
        TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
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
                return handle;

            int error = Marshal.GetLastWin32Error();
            handle.Dispose();
            if (DateTime.UtcNow >= deadline)
            {
                throw new Win32Exception(
                    error,
                    $"CreateFileW status client open failed (Win32 {error}).");
            }

            Thread.Sleep(10);
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
                    "Overview status transport check timed out: " + name);
            await Task.Delay(10);
        }
    }

    private static async Task ExpectBridgeErrorAsync(
        string expectedCode,
        string name,
        Func<Task<object?>> action)
    {
        try
        {
            _ = await action();
            throw new InvalidOperationException(
                $"Overview status transport check failed: {name} unexpectedly succeeded");
        }
        catch (BridgeCommandException error)
        {
            Check(
                error.Code == expectedCode,
                $"{name} expected {expectedCode}, got {error.Code}");
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(
                "Overview status transport check failed: " + message);
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
