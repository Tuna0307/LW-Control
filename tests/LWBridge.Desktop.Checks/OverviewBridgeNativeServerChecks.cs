using System.ComponentModel;
using System.Security.Principal;
using System.Text.Json;
using LWBridge.Desktop;
using Microsoft.Win32.SafeHandles;

namespace LWBridge.Desktop.Checks;

internal static class OverviewBridgeNativeServerChecks
{
    internal static JsonElement Run()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        string sid = identity.User?.Value ??
            throw new InvalidOperationException("Current Windows SID unavailable for native server check.");
        string pipePath = @"\\.\pipe\lwbridge-control-v1-check-" + Guid.NewGuid().ToString("N");

        Check(
            LWBridgeControlPipeListenerContract.GetOpenMode(firstServerInstance: true) == 0x40080003u &&
            LWBridgeControlPipeListenerContract.GetOpenMode(firstServerInstance: false) == 0x40000003u &&
            LWBridgeControlPipeListenerContract.PipeRejectRemoteClients == 0x8u,
            "native server check must use the recovered initial/replacement open mode and pipe mode");

        using SafeFileHandle first = LWBridgeControlPipeNativeServer.CreateServerInstance(
            pipePath, sid, firstServerInstance: true);
        Check(!first.IsInvalid && !first.IsClosed,
            "initial exact native server instance opens successfully");

        using SafeFileHandle replacement = LWBridgeControlPipeNativeServer.CreateServerInstance(
            pipePath, sid, firstServerInstance: false);
        Check(!replacement.IsInvalid && !replacement.IsClosed,
            "replacement server instance opens successfully without FILE_FLAG_FIRST_PIPE_INSTANCE");

        bool duplicateFirstRejected = false;
        int duplicateFirstError = 0;
        try
        {
            using SafeFileHandle impossible = LWBridgeControlPipeNativeServer.CreateServerInstance(
                pipePath, sid, firstServerInstance: true);
        }
        catch (Win32Exception error)
        {
            duplicateFirstRejected = true;
            duplicateFirstError = error.NativeErrorCode;
        }
        Check(duplicateFirstRejected && duplicateFirstError != 0,
            "Windows rejects a second FILE_FLAG_FIRST_PIPE_INSTANCE while the first server exists");

        string repo = FindRepoRoot();
        string hostStateSource = File.ReadAllText(Path.Combine(
            repo, "src", "LWBridge.Desktop", "LWBridgeControlPipeHostState.cs"));
        Check(!hostStateSource.Contains("CreateServerInstance(", StringComparison.Ordinal),
            "production host state must not open the pipe before accept/handshake ownership is implemented");

        return JsonSerializer.SerializeToElement(new
        {
            ok = true,
            finding = "LWB-R7-111",
            proven = new
            {
                nativeCreateNamedPipe = true,
                protectedSddlConversion = true,
                firstServerInstanceCreated = true,
                replacementServerInstanceCreated = true,
                duplicateFirstInstanceRejectedByWindows = true,
                duplicateFirstError,
            },
            recovered = new
            {
                firstOpenMode = "0x40080003",
                replacementOpenMode = "0x40000003",
                pipeMode = "0x00000008",
                maxInstances = LWBridgeControlPipeListenerContract.MaxInstances,
                outboundBufferBytes = LWBridgeControlPipeListenerContract.OutboundBufferBytes,
                inboundBufferBytes = LWBridgeControlPipeListenerContract.InboundBufferBytes,
                defaultTimeoutMilliseconds = LWBridgeControlPipeListenerContract.DefaultTimeoutMilliseconds,
                security = "protected SYSTEM + current-user GENERIC_ALL",
            },
            boundary = new
            {
                productionHostOpensPipe = false,
                connectNamedPipeImplemented = false,
                acceptLoopStarted = false,
                proxyTrafficAccepted = false,
                productionCallLuaEnabled = false,
            },
        }, JsonOptions.Default);
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
                "Overview bridge native server check failed: " + message);
    }
}
