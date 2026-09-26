using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class OverviewBridgeListenerContractChecks
{
    internal static JsonElement Run()
    {
        Check(LWBridgeControlPipeListenerContract.GetOpenMode(firstServerInstance: true) == 0x40080003u,
            "first server uses duplex + overlapped + first-instance flags");
        Check(LWBridgeControlPipeListenerContract.GetOpenMode(firstServerInstance: false) == 0x40000003u,
            "replacement servers use duplex + overlapped without first-instance flag");
        Check(LWBridgeControlPipeListenerContract.PipeRejectRemoteClients == 8,
            "pipe mode rejects remote clients");
        Check(LWBridgeControlPipeListenerContract.MaxInstances == 255,
            "pipe max instances is 255");
        Check(LWBridgeControlPipeListenerContract.OutboundBufferBytes == 65536 &&
              LWBridgeControlPipeListenerContract.InboundBufferBytes == 65536,
            "pipe in/out buffers are 64 KiB");
        Check(LWBridgeControlPipeListenerContract.DefaultTimeoutMilliseconds == 0,
            "CreateNamedPipe default timeout is zero");
        Check(LWBridgeControlPipeListenerContract.SecurityAttributesLength == 24,
            "SECURITY_ATTRIBUTES length is 24 bytes on x64");

        const string Sid = "S-1-5-21-1-2-3-1001";
        Check(LWBridgeControlPipeListenerContract.GetSecurityDescriptorSddlForSid(Sid) ==
              "D:P(A;;GA;;;SY)(A;;GA;;;S-1-5-21-1-2-3-1001)",
            "listener DACL grants GA only to SYSTEM and the current user SID");

        Check(LWBridgeControlPipeListenerContract.UseFirstPipeInstanceFlag(0),
            "initial server instance owns FILE_FLAG_FIRST_PIPE_INSTANCE");
        Check(!LWBridgeControlPipeListenerContract.UseFirstPipeInstanceFlag(1) &&
              !LWBridgeControlPipeListenerContract.UseFirstPipeInstanceFlag(99),
            "replacement server instances omit FILE_FLAG_FIRST_PIPE_INSTANCE");

        Check(LWBridgeControlPipeListenerContract.ClassifyConnectResult(true, 0) ==
              LWBridgePipeConnectDisposition.Connected,
            "ConnectNamedPipe immediate success is connected");
        Check(LWBridgeControlPipeListenerContract.ClassifyConnectResult(false, 232) ==
              LWBridgePipeConnectDisposition.Connected,
            "ERROR_NO_DATA follows the original completed path");
        Check(LWBridgeControlPipeListenerContract.ClassifyConnectResult(false, 535) ==
              LWBridgePipeConnectDisposition.Connected,
            "ERROR_PIPE_CONNECTED follows the original completed path");
        Check(LWBridgeControlPipeListenerContract.ClassifyConnectResult(false, 997) ==
              LWBridgePipeConnectDisposition.Pending,
            "ERROR_IO_PENDING enters overlapped pending");
        Check(LWBridgeControlPipeListenerContract.ClassifyConnectResult(false, 5) ==
              LWBridgePipeConnectDisposition.Failed,
            "other Win32 connect errors fail the server instance");
        Check(LWBridgeControlPipeListenerContract.HandshakeTimeout == TimeSpan.FromSeconds(5),
            "initial hello/read handshake deadline is five seconds");

        return JsonSerializer.SerializeToElement(new
        {
            ok = true,
            finding = "LWB-R7-100",
            recovered = new
            {
                pipeName = "R5-006 per-user SHA-256 SID suffix contract",
                firstOpenMode = "0x40080003",
                replacementOpenMode = "0x40000003",
                pipeMode = "0x00000008",
                maxInstances = 255,
                inboundBufferBytes = 65536,
                outboundBufferBytes = 65536,
                defaultTimeoutMilliseconds = 0,
                securityDescriptor = "D:P(A;;GA;;;SY)(A;;GA;;;<current-user-SID>)",
                handshakeTimeoutSeconds = 5,
            },
            connect = new
            {
                immediateTrue = "connected",
                errorNoData232 = "completed",
                errorPipeConnected535 = "completed",
                errorIoPending997 = "pending",
                other = "failed; accept loop retries with a replacement server",
            },
            boundary = new
            {
                productionNamedPipeListenerImplemented = false,
                productionCallLuaEnabled = false,
                proxyLaunchEnvironmentBound = false,
            },
        }, JsonOptions.Default);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("Overview bridge listener contract check failed: " + message);
    }
}
