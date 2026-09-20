using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json;
using LWBridge.Desktop;
using Microsoft.Win32.SafeHandles;

namespace LWBridge.Desktop.Checks;

internal static class OverviewBridgeNativeConnectChecks
{
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint OpenExisting = 3;

    internal static JsonElement Run()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        string sid = identity.User?.Value ??
            throw new InvalidOperationException("Current Windows SID unavailable for native connect check.");

        string pendingPath = @"\\.\pipe\lwbridge-control-v1-connect-pending-" + Guid.NewGuid().ToString("N");
        using SafeFileHandle pendingServer = LWBridgeControlPipeNativeServer.CreateServerInstance(
            pendingPath, sid, firstServerInstance: true);
        using LWBridgeControlPipePendingConnect pending =
            LWBridgeControlPipeNativeServer.BeginConnect(pendingServer);

        Check(
            pending.InitialDisposition == LWBridgePipeConnectDisposition.Pending &&
            pending.InitialLastError == LWBridgeControlPipeListenerContract.ErrorIoPending,
            "ConnectNamedPipe without a client must enter recovered ERROR_IO_PENDING path");

        using SafeFileHandle pendingClient = OpenClient(pendingPath);
        Check(
            pending.WaitForCompletion(TimeSpan.FromSeconds(2)) ==
                LWBridgePipeConnectDisposition.Connected,
            "overlapped pending connect completes after the client opens the pipe");

        string preconnectedPath = @"\\.\pipe\lwbridge-control-v1-connect-race-" + Guid.NewGuid().ToString("N");
        using SafeFileHandle preconnectedServer = LWBridgeControlPipeNativeServer.CreateServerInstance(
            preconnectedPath, sid, firstServerInstance: true);
        using SafeFileHandle preconnectedClient = OpenClient(preconnectedPath);
        using LWBridgeControlPipePendingConnect preconnected =
            LWBridgeControlPipeNativeServer.BeginConnect(preconnectedServer);

        Check(
            preconnected.InitialDisposition == LWBridgePipeConnectDisposition.Connected &&
            preconnected.InitialLastError == LWBridgeControlPipeListenerContract.ErrorPipeConnected,
            "client-before-ConnectNamedPipe race must map Win32 ERROR_PIPE_CONNECTED to completed");

        string repo = FindRepoRoot();
        string hostStateSource = File.ReadAllText(Path.Combine(
            repo, "src", "LWBridge.Desktop", "LWBridgeControlPipeHostState.cs"));
        Check(!hostStateSource.Contains("BeginConnect(", StringComparison.Ordinal),
            "production host state must not accept clients before handshake/route ownership is implemented");

        return JsonSerializer.SerializeToElement(new
        {
            ok = true,
            finding = "LWB-R7-112",
            windowsProof = new
            {
                pendingConnectInitialError = pending.InitialLastError,
                pendingConnectCompleted = true,
                preconnectedInitialError = preconnected.InitialLastError,
                preconnectedMappedCompleted = true,
            },
            recovered = new
            {
                errorIoPending = LWBridgeControlPipeListenerContract.ErrorIoPending,
                errorPipeConnected = LWBridgeControlPipeListenerContract.ErrorPipeConnected,
                errorNoData = LWBridgeControlPipeListenerContract.ErrorNoData,
                overlappedMemoryOwnedUntilCompletion = true,
                eventOwnedUntilCompletion = true,
                disposeCancelsOutstandingPendingIo = true,
            },
            boundary = new
            {
                productionHostAcceptsClients = false,
                handshakeImplemented = false,
                proxyIdentityAdmitted = false,
                routeTransportImplemented = false,
                productionCallLuaEnabled = false,
            },
        }, JsonOptions.Default);
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
        throw new Win32Exception(error, $"CreateFileW client open failed (Win32 {error}).");
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
                "Overview bridge native connect check failed: " + message);
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
