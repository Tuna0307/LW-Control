using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class OverviewBridgeHostOwnershipChecks
{
    internal static JsonElement Run()
    {
        const string PipePath = @"\\.\pipe\lwbridge-control-v1-test";
        var registry = new LWBridgeControlPipeRegistry();
        using var host = new LWBridgeControlPipeHostState(PipePath, registry);

        Check(host.PipePath == PipePath, "host pins one process-wide pipe identity");
        Check(!host.IsStopped, "host starts active without opening transport");
        Check(ReferenceEquals(host.RequireActiveRegistry(), registry),
            "host owns exactly one registry instance");

        registry.Register("profile-a", "instance-a", "token-123", 91_000);
        Check(host.PendingRegistrationCount == 1 && host.ConnectedRouteCount == 0,
            "host registry state is shared through the owner");

        using (var lifecycle = new OverviewLifecycleService(
            "profile-a",
            gameRoot: null,
            helperPath: "offline-test-helper.py",
            requireCurrentClientEvidence: false,
            startRecoveryMonitor: false,
            bridgeHostState: host))
        {
            Check(ReferenceEquals(lifecycle.BridgeHostState, host),
                "profile lifecycle receives the application-owned bridge host");
            var backend = new LWBridgeBackend(
                new LocalConfigStore(persistent: false),
                overviewLifecycle: lifecycle,
                bridgeHostState: host);
            Check(ReferenceEquals(backend.BridgeHostState, host),
                "backend receives the same application-owned bridge host for future status/call routing");
            lifecycle.Close();
            Check(!host.IsStopped,
                "closing one profile lifecycle does not stop the application-owned bridge host");
        }

        host.Close();
        host.Close();
        Check(host.IsStopped, "application host close is idempotent");
        ExpectBridgeError("BRIDGE_STOPPED", "registry access after host shutdown", () =>
            host.RequireActiveRegistry());

        string repo = FindRepoRoot();
        string windowSource = File.ReadAllText(Path.Combine(
            repo, "src", "LWBridge.Desktop", "LWBridgeWindow.cs"));
        Check(
            windowSource.Contains("bridgeHostState = new LWBridgeControlPipeHostState();", StringComparison.Ordinal) &&
            windowSource.Contains("bridgeHostState: bridgeHostState", StringComparison.Ordinal) &&
            windowSource.Contains("backend = new LWBridgeBackend(", StringComparison.Ordinal) &&
            windowSource.Contains("overviewLifecycleService?.Close();", StringComparison.Ordinal) &&
            windowSource.Contains("bridgeHostState?.Close();", StringComparison.Ordinal) &&
            windowSource.IndexOf("overviewLifecycleService?.Close();", StringComparison.Ordinal) <
                windowSource.IndexOf("bridgeHostState?.Close();", StringComparison.Ordinal),
            "normal application window must create one host, lend it to profile lifecycle, and close it after profile shutdown");

        return JsonSerializer.SerializeToElement(new
        {
            ok = true,
            finding = "LWB-R7-110",
            recovered = new
            {
                owner = "LWBridgeWindow/application lifetime",
                listenerScope = LWBridgeControlPipeStartupContract.ListenerOwnership,
                registryOwnership = "one registry per shared bridge host",
                profileLifecycleOwnership = "borrowed; profile close does not stop host",
                shutdownOwnership = "application owner closes shared host after profile/scan lifecycles",
            },
            boundary = new
            {
                nativePipeOpened = false,
                acceptLoopStarted = false,
                proxyEnvironmentBound = false,
                productionCallLuaEnabled = false,
                pendingCallCollectionImplemented = false,
            },
        }, JsonOptions.Default);
    }

    private static void ExpectBridgeError(string code, string name, Func<object> action)
    {
        try
        {
            _ = action();
            throw new InvalidOperationException(
                $"Overview bridge host ownership check failed: {name} unexpectedly succeeded");
        }
        catch (BridgeCommandException error)
        {
            Check(error.Code == code, $"{name} expected {code}, got {error.Code}");
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
                "Overview bridge host ownership check failed: " + message);
    }
}
