using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using LWBridge.Desktop;
using Microsoft.Win32.SafeHandles;

namespace LWBridge.Desktop.Checks;

internal static class OverviewBridgeIsolatedHandshakeChecks
{
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint OpenExisting = 3;

    internal static async Task<JsonElement> RunAsync()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        string sid = identity.User?.Value ??
            throw new InvalidOperationException(
                "Current Windows SID unavailable for isolated handshake check.");
        string processPath = Environment.ProcessPath ??
            throw new InvalidOperationException("Current process path unavailable.");

        string canonicalProcessPath =
            LWBridgeControlPipeIsolatedHandshake.CanonicalizeExpectedClientPath(
                processPath);
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var successRegistry = new LWBridgeControlPipeRegistry();
        const string SuccessProfile = "profile-success";
        const string SuccessInstance = "instance-success";
        const string SuccessToken = "token-success-123456";
        const string SuccessBuild = "build-success";
        successRegistry.Register(
            SuccessProfile,
            SuccessInstance,
            SuccessToken,
            now + LWBridgeControlPipeRegistry.StartupRegistrationLifetimeMilliseconds);

        object route = new();
        string successPath = NewPipePath("success");
        using SafeFileHandle successServer =
            LWBridgeControlPipeNativeServer.CreateServerInstance(
                successPath,
                sid,
                firstServerInstance: true);
        using LWBridgeControlPipePendingConnect successConnect =
            LWBridgeControlPipeNativeServer.BeginConnect(successServer);
        using SafeFileHandle successClient = OpenClient(successPath);
        Check(
            successConnect.WaitForCompletion(TimeSpan.FromSeconds(2)) ==
                LWBridgePipeConnectDisposition.Connected,
            "success pipe must connect");

        Task<LWBridgeAuthenticatedConnection> successTask =
            LWBridgeControlPipeIsolatedHandshake.AuthenticateAsync(
                successServer,
                successRegistry,
                SuccessBuild,
                canonicalProcessPath,
                route,
                nowMilliseconds: now,
                ackTimestamp: now + 1);

        using (var clientStream = new FileStream(
            successClient,
            FileAccess.ReadWrite,
            bufferSize: 4096,
            isAsync: false))
        {
            byte[] hello = CreateHello(
                SuccessProfile,
                SuccessInstance,
                SuccessToken,
                Environment.ProcessId,
                SuccessBuild,
                now);
            byte[] frame = LWBridgeControlPipeProtocol.EncodeFrame(hello);
            clientStream.Write(frame);
            clientStream.Flush();

            byte[] ackPayload = ReadFrame(clientStream);
            using JsonDocument ackDocument = JsonDocument.Parse(ackPayload);
            JsonElement ack = ackDocument.RootElement;
            Check(ack.GetProperty("version").GetInt32() == 1 &&
                  ack.GetProperty("type").GetString() ==
                    LWBridgeControlPipeProtocol.HelloAckType &&
                  ack.GetProperty("profileId").GetString() == SuccessProfile &&
                  ack.GetProperty("instanceId").GetString() == SuccessInstance &&
                  ack.GetProperty("requestId").GetString() == string.Empty &&
                  ack.GetProperty("timestamp").GetInt64() == now + 1 &&
                  ack.GetProperty("payload").ValueKind == JsonValueKind.Object &&
                  !ack.GetProperty("payload").EnumerateObject().Any(),
                "successful isolated hello must receive the recovered framed hello.ack");
        }

        LWBridgeAuthenticatedConnection success = await successTask;
        Check(success.ProfileId == SuccessProfile &&
              success.InstanceId == SuccessInstance &&
              success.ClientPid == Environment.ProcessId &&
              success.Generation > 0,
            "successful handshake preserves authenticated identity/generation");
        ConnectedRoute? resolved = successRegistry.Resolve(SuccessInstance);
        Check(resolved is not null &&
              resolved.Generation == success.Generation &&
              ReferenceEquals(resolved.Route, route),
            "registry route is installed only after successful authentication");
        Check(successRegistry.RemoveConnected(
                SuccessInstance,
                success.Generation),
            "successful isolated route cleanup is generation-scoped");
        successRegistry.Unregister(SuccessInstance);

        var pidRegistry = new LWBridgeControlPipeRegistry();
        const string PidInstance = "instance-pid-reject";
        const string PidToken = "token-pid-reject-123456";
        pidRegistry.Register(
            "profile-pid-reject",
            PidInstance,
            PidToken,
            now + LWBridgeControlPipeRegistry.StartupRegistrationLifetimeMilliseconds);

        string pidPath = NewPipePath("pid-reject");
        using SafeFileHandle pidServer =
            LWBridgeControlPipeNativeServer.CreateServerInstance(
                pidPath,
                sid,
                firstServerInstance: true);
        using LWBridgeControlPipePendingConnect pidConnect =
            LWBridgeControlPipeNativeServer.BeginConnect(pidServer);
        using SafeFileHandle pidClient = OpenClient(pidPath);
        Check(
            pidConnect.WaitForCompletion(TimeSpan.FromSeconds(2)) ==
                LWBridgePipeConnectDisposition.Connected,
            "pid-reject pipe must connect");

        Task<LWBridgeAuthenticatedConnection> pidTask =
            LWBridgeControlPipeIsolatedHandshake.AuthenticateAsync(
                pidServer,
                pidRegistry,
                "build-pid-reject",
                canonicalProcessPath,
                new object(),
                now,
                now + 2);

        using (var clientStream = new FileStream(
            pidClient,
            FileAccess.ReadWrite,
            bufferSize: 4096,
            isAsync: false))
        {
            uint wrongPid = Environment.ProcessId == int.MaxValue
                ? checked((uint)Environment.ProcessId - 1)
                : checked((uint)Environment.ProcessId + 1);
            byte[] hello = CreateHello(
                "profile-pid-reject",
                PidInstance,
                PidToken,
                wrongPid,
                "build-pid-reject",
                now);
            clientStream.Write(LWBridgeControlPipeProtocol.EncodeFrame(hello));
            clientStream.Flush();
        }

        await ExpectBridgeErrorAsync(
            "PIPE_HANDSHAKE_REJECTED",
            "OS pipe client PID mismatch",
            async () => _ = await pidTask);
        Check(pidRegistry.ConnectedCount == 0 &&
              pidRegistry.IsPending(PidInstance),
            "rejected PID does not claim/install the pending registration");
        pidRegistry.Unregister(PidInstance);

        var timeoutRegistry = new LWBridgeControlPipeRegistry();
        const string TimeoutInstance = "instance-timeout";
        timeoutRegistry.Register(
            "profile-timeout",
            TimeoutInstance,
            "token-timeout-123456",
            now + LWBridgeControlPipeRegistry.StartupRegistrationLifetimeMilliseconds);

        string timeoutPath = NewPipePath("timeout");
        using SafeFileHandle timeoutServer =
            LWBridgeControlPipeNativeServer.CreateServerInstance(
                timeoutPath,
                sid,
                firstServerInstance: true);
        using LWBridgeControlPipePendingConnect timeoutConnect =
            LWBridgeControlPipeNativeServer.BeginConnect(timeoutServer);
        using SafeFileHandle timeoutClient = OpenClient(timeoutPath);
        Check(
            timeoutConnect.WaitForCompletion(TimeSpan.FromSeconds(2)) ==
                LWBridgePipeConnectDisposition.Connected,
            "timeout pipe must connect");

        await ExpectBridgeErrorAsync(
            "PIPE_HANDSHAKE_TIMEOUT",
            "connected client sends no hello",
            async () =>
            {
                _ = await LWBridgeControlPipeIsolatedHandshake.AuthenticateAsync(
                    timeoutServer,
                    timeoutRegistry,
                    "build-timeout",
                    canonicalProcessPath,
                    new object(),
                    now,
                    now + 3,
                    timeoutOverride: TimeSpan.FromMilliseconds(150));
            });
        Check(timeoutRegistry.ConnectedCount == 0 &&
              timeoutRegistry.IsPending(TimeoutInstance),
            "handshake timeout leaves pending registration unclaimed");
        timeoutRegistry.Unregister(TimeoutInstance);

        string repo = FindRepoRoot();
        string hostSource = File.ReadAllText(Path.Combine(
            repo, "src", "LWBridge.Desktop", "LWBridgeControlPipeHostState.cs"));
        Check(!hostSource.Contains("AuthenticateAsync(", StringComparison.Ordinal),
            "normal application host must not start isolated authenticated hello yet");

        return JsonSerializer.SerializeToElement(new
        {
            ok = true,
            finding = "LWB-R7-115",
            windowsProof = new
            {
                success = true,
                framedHelloAck = true,
                osClientPidVerified = true,
                canonicalClientImageVerified = true,
                buildIdVerified = true,
                registryTokenAdmission = true,
                generationScopedRouteInstalled = true,
                pidMismatchRejected = true,
                noHelloTimeoutRejected = true,
            },
            recovered = new
            {
                handshakeTimeoutSeconds =
                    LWBridgeControlPipeListenerContract.HandshakeTimeout.TotalSeconds,
                successRouteGeneration = success.Generation,
                helloAckType = LWBridgeControlPipeProtocol.HelloAckType,
            },
            boundary = new
            {
                productionHostStartsHandshake = false,
                productionAcceptLoopStarted = false,
                outboundCommandRoutingImplemented = false,
                pendingCallCollectionImplemented = false,
                productionCallLuaEnabled = false,
            },
        }, JsonOptions.Default);
    }

    private static byte[] CreateHello(
        string profileId,
        string instanceId,
        string token,
        long pid,
        string buildId,
        long timestamp)
    {
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            version = LWBridgeControlPipeProtocol.ProtocolVersion,
            type = LWBridgeControlPipeProtocol.HelloType,
            profileId,
            instanceId,
            requestId = string.Empty,
            timestamp,
            payload = new
            {
                token,
                pid,
                buildId,
            },
        });
    }

    private static byte[] ReadFrame(Stream stream)
    {
        byte[] prefix = new byte[LWBridgeControlPipeProtocol.FramePrefixLength];
        ReadExactly(stream, prefix);
        uint payloadLength = BinaryPrimitives.ReadUInt32LittleEndian(prefix);
        Check(payloadLength is > 0 and <=
            LWBridgeControlPipeProtocol.MaxFramePayloadLength,
            "received frame length must stay within recovered bounds");
        byte[] payload = new byte[checked((int)payloadLength)];
        ReadExactly(stream, payload);
        return payload;
    }

    private static void ReadExactly(Stream stream, byte[] buffer)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int read = stream.Read(buffer, offset, buffer.Length - offset);
            if (read == 0)
                throw new EndOfStreamException("pipe closed while reading test frame");
            offset += read;
        }
    }

    private static SafeFileHandle OpenClient(string pipePath)
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
            $"CreateFileW client open failed (Win32 {error}).");
    }

    private static string NewPipePath(string suffix) =>
        @"\\.\pipe\lwbridge-control-v1-handshake-" +
        suffix + "-" + Guid.NewGuid().ToString("N");

    private static async Task ExpectBridgeErrorAsync(
        string expectedCode,
        string name,
        Func<Task> action)
    {
        try
        {
            await action();
            throw new InvalidOperationException(
                $"Overview bridge isolated handshake check failed: {name} unexpectedly succeeded");
        }
        catch (BridgeCommandException error)
        {
            Check(error.Code == expectedCode,
                $"{name} expected {expectedCode}, got {error.Code}");
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

        throw new DirectoryNotFoundException("could not locate LW-Control repository root");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(
                "Overview bridge isolated handshake check failed: " + message);
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
}
