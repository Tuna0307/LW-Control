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

        Task<object?> squadTask = backend.InvokeAsync(
            "squad_list",
            profilePayload,
            CancellationToken.None);
        byte[] squadCommandPayload = await ReadFrameAsync(clientStream);
        using (JsonDocument squadCommandDoc =
               JsonDocument.Parse(squadCommandPayload))
        {
            JsonElement squadCommand = squadCommandDoc.RootElement;
            Check(
                squadCommand.GetProperty("type").GetString() ==
                    LWBridgeControlPipeProtocol.CommandType &&
                squadCommand.GetProperty("payload")
                    .GetProperty("id").GetString() == "cmd_2" &&
                squadCommand.GetProperty("payload")
                    .GetProperty("fn").GetString() == "getSquads" &&
                squadCommand.GetProperty("payload")
                    .GetProperty("args").GetRawText() == "{}",
                "squad_list emits exact getSquads empty-args call");
        }

        await WriteFrameAsync(
            clientStream,
            CreateEnvelope(
                LWBridgeControlPipeProtocol.ResultType,
                backend.ProfileId,
                Instance,
                "outer-squad-not-required",
                Now + 2,
                new
                {
                    id = "cmd_2",
                    ok = true,
                    result = new
                    {
                        squads = new[]
                        {
                            new
                            {
                                index = 2,
                                positions = new[] { 1, 2 },
                                heroes = new[]
                                {
                                    new
                                    {
                                        uuid = "hero-1",
                                        name = "Alpha",
                                    },
                                },
                            },
                        },
                        opaque = "preserved",
                    },
                }));
        JsonElement squadResult = JsonSerializer.SerializeToElement(
            await squadTask.WaitAsync(TimeSpan.FromSeconds(2)),
            JsonOptions.Default);
        Check(
            squadResult.GetProperty("squads")[0]
                .GetProperty("index").GetInt32() == 2 &&
            squadResult.GetProperty("squads")[0]
                .GetProperty("heroes")[0]
                .GetProperty("uuid").GetString() == "hero-1" &&
            squadResult.GetProperty("opaque").GetString() == "preserved",
            "squad_list forwards the correlated getSquads JSON result unchanged");

        Task<object?> squadTimeoutTask = backend.InvokeAsync(
            "squad_list",
            profilePayload,
            CancellationToken.None);
        byte[] squadTimeoutPayload = await ReadFrameAsync(clientStream);
        using (JsonDocument squadTimeoutDoc =
               JsonDocument.Parse(squadTimeoutPayload))
        {
            Check(
                squadTimeoutDoc.RootElement.GetProperty("payload")
                    .GetProperty("id").GetString() == "cmd_3" &&
                squadTimeoutDoc.RootElement.GetProperty("payload")
                    .GetProperty("fn").GetString() == "getSquads",
                "second squad_list call remains independently correlated");
        }

        var squadTimeoutWatch = System.Diagnostics.Stopwatch.StartNew();
        BridgeCommandException squadTimeout = await ExpectBridgeErrorAsync(
            "LUA_CALL_TIMEOUT",
            "squad_list native deadline",
            async () => await squadTimeoutTask.WaitAsync(
                TimeSpan.FromSeconds(7)));
        squadTimeoutWatch.Stop();
        Check(
            squadTimeout.Message ==
                "lua call result unknown after timeout: getSquads" &&
            squadTimeoutWatch.Elapsed >= TimeSpan.FromSeconds(4.5) &&
            squadTimeoutWatch.Elapsed < TimeSpan.FromSeconds(6.5),
            "squad_list uses the native 5000 ms result deadline and message");

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

        foreach (JsonElement missingProfile in new[]
                 {
                     JsonSerializer.SerializeToElement(new { }),
                     JsonSerializer.SerializeToElement(
                         new { profileId = 123 }),
                     JsonSerializer.SerializeToElement(
                         new { profileId = "   " }),
                 })
        {
            BridgeCommandException required =
                await ExpectBridgeErrorAsync(
                    "PROFILE_ID_REQUIRED",
                    "squad_list missing profile",
                    () => noHostBackend.InvokeAsync(
                        "squad_list",
                        missingProfile,
                        CancellationToken.None));
            Check(
                required.Message == "PROFILE_ID_REQUIRED",
                "squad_list missing profile uses native message");
        }

        BridgeCommandException unknownProfile =
            await ExpectBridgeErrorAsync(
                "PROFILE_RUNTIME_UNAVAILABLE",
                "squad_list unknown profile",
                () => noHostBackend.InvokeAsync(
                    "squad_list",
                    JsonSerializer.SerializeToElement(
                        new { profileId = "other-profile" }),
                    CancellationToken.None));
        Check(
            unknownProfile.Message == "PROFILE_RUNTIME_UNAVAILABLE",
            "squad_list unknown runtime uses native message");

        BridgeCommandException disconnected =
            await ExpectBridgeErrorAsync(
                "GAME_DISCONNECTED",
                "squad_list disconnected",
                () => noHostBackend.InvokeAsync(
                    "squad_list",
                    JsonSerializer.SerializeToElement(
                        new { profileId = noHostBackend.ProfileId }),
                    CancellationToken.None));
        Check(
            disconnected.Message == "game disconnected",
            "squad_list disconnected uses native message");

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
                squadList = new
                {
                    commandId = "cmd_2",
                    functionName = "getSquads",
                    args = "{}",
                    rawResultPassThrough = true,
                    timeoutCommandId = "cmd_3",
                    timeoutMilliseconds = 5000,
                    timeoutCode = "LUA_CALL_TIMEOUT",
                    timeoutMessage =
                        "lua call result unknown after timeout: getSquads",
                    nativeProfileErrors = true,
                    disconnectedError = "GAME_DISCONNECTED",
                },
            },
            boundary = new
            {
                genericCallLuaEnabled = false,
                onlyGetStatusEmptyArgsEnabled = true,
                squadListUsesPrivateRecoveredGameCall = true,
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

    private static async Task<BridgeCommandException> ExpectBridgeErrorAsync(
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
            return error;
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
