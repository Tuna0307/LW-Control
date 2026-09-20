using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json;
using LWBridge.Desktop;
using Microsoft.Win32.SafeHandles;

namespace LWBridge.Desktop.Checks;

internal static class OverviewBridgeHostTransportChecks
{
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint OpenExisting = 3;

    internal static async Task<JsonElement> RunAsync()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        string sid = identity.User?.Value ??
            throw new InvalidOperationException(
                "Current Windows SID unavailable for host transport check.");
        string processPath = Environment.ProcessPath ??
            throw new InvalidOperationException(
                "Current process path unavailable.");

        const string Profile = "profile-host-rpc";
        const string Instance = "instance-host-rpc";
        const string Build = "build-host-rpc";
        const long Now = 4_000_000;

        string pipePath =
            @"\\.\pipe\lwbridge-control-v1-host-rpc-" +
            Guid.NewGuid().ToString("N");
        byte[] entropy = Enumerable.Range(0, 32)
            .Select(value => (byte)value)
            .ToArray();

        using var host = new LWBridgeControlPipeHostState(
            pipePath,
            new LWBridgeControlPipeRegistry(),
            pipeTokenEntropyFactory: () => entropy.ToArray());

        Task listener = host.StartRpcTransport(
            Build,
            processPath,
            currentUserSid: sid,
            clockMilliseconds: () => Now);

        Check(
            host.IsRpcTransportStarted &&
            host.PendingCallCount == 0,
            "host start owns listener and empty pending-call registry");

        LWBridgeControlPipeLaunchBinding binding =
            host.PrepareLaunchBinding(
                Profile,
                Instance,
                Build,
                Now);
        Check(
            host.PendingRegistrationCount == 1 &&
            binding.PipeToken ==
                "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8",
            "registration is prepared after shared host listener is active");

        using SafeFileHandle client =
            OpenClientWithRetry(
                pipePath,
                TimeSpan.FromSeconds(2));
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
                token = binding.PipeToken,
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
                "host-owned listener authenticates and returns hello.ack");
        }

        await WaitUntilAsync(
            () => host.ConnectedRouteCount == 1,
            TimeSpan.FromSeconds(2),
            "host connected route");

        JsonElement empty =
            JsonSerializer.SerializeToElement(new { });

        Task<JsonElement?> firstCall =
            host.CallLuaAsync(
                Instance,
                "getStatus",
                empty,
                timestamp: Now + 10,
                createdAt: Now + 9,
                cancellationToken:
                    new CancellationTokenSource(
                        TimeSpan.FromSeconds(2)).Token);

        byte[] firstCommand =
            await ReadFrameAsync(clientStream);
        using (JsonDocument firstDoc =
               JsonDocument.Parse(firstCommand))
        {
            JsonElement root = firstDoc.RootElement;
            Check(
                root.GetProperty("type").GetString() ==
                    LWBridgeControlPipeProtocol.CommandType &&
                root.GetProperty("requestId").GetString() ==
                    "cmd_1" &&
                root.GetProperty("payload")
                    .GetProperty("id").GetString() ==
                    "cmd_1" &&
                root.GetProperty("payload")
                    .GetProperty("fn").GetString() ==
                    "getStatus",
                "host instance route emits first recovered command frame");
        }

        Check(
            host.PendingCallCount == 1,
            "host pending count is authoritative while first result is outstanding");

        await WriteFrameAsync(
            clientStream,
            CreateEnvelope(
                LWBridgeControlPipeProtocol.ResultType,
                Profile,
                Instance,
                "not-required-to-equal-payload-id",
                Now + 11,
                new
                {
                    id = "cmd_1",
                    ok = true,
                    result = new { state = "ready" },
                }));

        JsonElement? firstResult =
            await firstCall.WaitAsync(
                TimeSpan.FromSeconds(2));
        Check(
            firstResult?.GetProperty("state").GetString() ==
                "ready" &&
            host.PendingCallCount == 0,
            "host instance route correlates result and decrements pending");

        Task<JsonElement?> defaultCall =
            host.CallLuaAsync(
                LWBridgeControlPipeRegistry.DefaultRoute,
                "getStatus",
                empty,
                timestamp: Now + 20,
                createdAt: Now + 19,
                cancellationToken:
                    new CancellationTokenSource(
                        TimeSpan.FromSeconds(2)).Token);

        byte[] defaultCommand =
            await ReadFrameAsync(clientStream);
        using (JsonDocument defaultDoc =
               JsonDocument.Parse(defaultCommand))
        {
            Check(
                defaultDoc.RootElement
                    .GetProperty("payload")
                    .GetProperty("id")
                    .GetString() == "cmd_2",
                "unique default route reaches same authenticated session");
        }

        await WriteFrameAsync(
            clientStream,
            CreateEnvelope(
                LWBridgeControlPipeProtocol.ResultType,
                Profile,
                Instance,
                "cmd_2",
                Now + 21,
                new
                {
                    id = "cmd_2",
                    ok = true,
                    result = new { route = "default" },
                }));
        JsonElement? defaultResult =
            await defaultCall.WaitAsync(
                TimeSpan.FromSeconds(2));
        Check(
            defaultResult?.GetProperty("route").GetString() ==
                "default",
            "default route returns correlated result");

        Task<JsonElement?> shutdownCall =
            host.CallLuaAsync(
                Instance,
                "getStatus",
                empty,
                timestamp: Now + 30,
                createdAt: Now + 29,
                cancellationToken:
                    CancellationToken.None);

        byte[] shutdownCommand =
            await ReadFrameAsync(clientStream);
        using (JsonDocument shutdownDoc =
               JsonDocument.Parse(shutdownCommand))
        {
            Check(
                shutdownDoc.RootElement
                    .GetProperty("payload")
                    .GetProperty("id")
                    .GetString() == "cmd_3" &&
                host.PendingCallCount == 1,
                "third command is outstanding before host transport stop");
        }

        await host.StopRpcTransportAsync();

        await ExpectBridgeErrorAsync(
            "APP_SHUTTING_DOWN",
            "host transport shutdown drain",
            async () => _ = await shutdownCall.WaitAsync(
                TimeSpan.FromSeconds(2)));

        await listener.WaitAsync(TimeSpan.FromSeconds(2));
        Check(
            !host.IsRpcTransportStarted &&
            host.PendingCallCount is null &&
            host.ConnectedRouteCount == 0,
            "host stop removes listener/call registry and tears down route");

        Check(
            !host.IsStopped &&
            host.RequireActiveRegistry() is not null,
            "transport stop alone does not stop application-global host state");

        host.Close();
        Check(host.IsStopped,
            "host Close marks application-global bridge host stopped");
        ExpectBridgeError(
            "BRIDGE_STOPPED",
            "transport start after host Close",
            () => host.StartRpcTransport(
                Build,
                processPath,
                currentUserSid: sid,
                clockMilliseconds: () => Now));

        string repo = FindRepoRoot();
        string windowSource = File.ReadAllText(Path.Combine(
            repo,
            "src",
            "LWBridge.Desktop",
            "LWBridgeWindow.cs"));
        Check(
            !windowSource.Contains(
                "StartRpcTransport(",
                StringComparison.Ordinal) &&
            !windowSource.Contains(
                "enableBridgeControlPipeLaunchBinding: true",
                StringComparison.Ordinal),
            "normal application composition remains disabled pending separate startup integration proof");

        return JsonSerializer.SerializeToElement(new
        {
            ok = true,
            finding = "LWB-R7-121",
            windowsProof = new
            {
                hostListenerStarted = true,
                launchRegistrationPreparedAfterListenerStart = true,
                authenticated = true,
                instanceRouteCall = true,
                defaultRouteCall = true,
                commandIds = new[]
                {
                    "cmd_1",
                    "cmd_2",
                    "cmd_3",
                },
                pendingObserved = 1,
                shutdownDrainedPendingWith =
                    "APP_SHUTTING_DOWN",
                routeRemovedOnStop = true,
                listenerTaskCompleted = listener.IsCompleted,
            },
            recoveredProductionInputs = new
            {
                expectedClientPathSource = @"<gameRoot>\Game\LastWar.exe",
                initialCommandCounterSeed =
                    LWBridgeControlPipeCallRegistry.InitialCommandCounter,
                firstCommandId =
                    LWBridgeControlPipeCallRegistry.FirstCommandId,
            },
            boundary = new
            {
                LWBridgeWindowStartsTransport = false,
                normalOverviewLaunchBindingEnabled = false,
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
            "test client frame length stays in recovered range");

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
                    "temporary host RPC client closed during frame read");
            }

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
                    $"CreateFileW host RPC client open failed (Win32 {error}).");
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
            {
                throw new TimeoutException(
                    "Overview bridge host transport check timed out: " +
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
                $"Overview bridge host transport check failed: {name} unexpectedly succeeded");
        }
        catch (BridgeCommandException error)
        {
            Check(
                error.Code == expectedCode,
                $"{name} expected {expectedCode}, got {error.Code}");
        }
    }

    private static void ExpectBridgeError(
        string expectedCode,
        string name,
        Func<object?> action)
    {
        try
        {
            _ = action();
            throw new InvalidOperationException(
                $"Overview bridge host transport check failed: {name} unexpectedly succeeded");
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
                "Overview bridge host transport check failed: " +
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
