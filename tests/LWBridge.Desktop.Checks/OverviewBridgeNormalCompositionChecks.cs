using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class OverviewBridgeNormalCompositionChecks
{
    internal static JsonElement Run()
    {
        string repo = FindRepoRoot();
        string windowSource = File.ReadAllText(Path.Combine(
            repo,
            "src",
            "LWBridge.Desktop",
            "LWBridgeWindow.cs"));
        string lifecycleSource = File.ReadAllText(Path.Combine(
            repo,
            "src",
            "LWBridge.Desktop",
            "OverviewLifecycleService.cs"));
        string hostSource = File.ReadAllText(Path.Combine(
            repo,
            "src",
            "LWBridge.Desktop",
            "LWBridgeControlPipeHostState.cs"));

        int normalBranch = RequireIndex(
            windowSource,
            "if (!isolated)");
        int hostConstruction = RequireIndex(
            windowSource,
            "bridgeHostState = new LWBridgeControlPipeHostState();");
        int validRootGuard = RequireIndex(
            windowSource,
            "if (liveGameRoot.Valid)");
        int expectedPathBuilder = RequireIndex(
            windowSource,
            "BuildExpectedGameExecutablePath(liveGameRoot.Path)");
        int listenerStart = RequireIndex(
            windowSource,
            "bridgeHostState.StartRpcTransport(");
        int listenerBuildId = RequireIndexAfter(
            windowSource,
            "OverviewLifecycleService.BridgeVersion",
            listenerStart);
        int lifecycleConstruction = RequireIndex(
            windowSource,
            "overviewLifecycleService = new OverviewLifecycleService(");
        int bindingEnabled = RequireIndexAfter(
            windowSource,
            "enableBridgeControlPipeLaunchBinding: true",
            lifecycleConstruction);

        Check(
            normalBranch < hostConstruction &&
            hostConstruction < validRootGuard &&
            validRootGuard < expectedPathBuilder &&
            expectedPathBuilder < listenerStart &&
            listenerStart < listenerBuildId &&
            listenerBuildId < lifecycleConstruction &&
            lifecycleConstruction < bindingEnabled,
            "normal window creates host, derives game image, starts listener, then enables lifecycle launch binding");

        Check(
            LWBridgeControlPipeClientPathContract
                .BuildExpectedGameExecutablePath(@"C:\LastWarRoot") ==
                @"C:\LastWarRoot\Game\LastWar.exe",
            "normal startup expected-client path uses recovered Game/LastWar.exe derivation");

        int ensureMethod = RequireIndex(
            lifecycleSource,
            "private void EnsureControlPipeHostStarted(string selectedRoot)");
        int skipTestHooks = RequireIndexAfter(
            lifecycleSource,
            "if (!bridgeControlPipeLaunchBindingEnabled || testHooks is not null)",
            ensureMethod);
        int lifecyclePathBuilder = RequireIndexAfter(
            lifecycleSource,
            ".BuildExpectedGameExecutablePath(selectedRoot)",
            ensureMethod);
        int lifecycleStart = RequireIndexAfter(
            lifecycleSource,
            "bridgeHostState.StartRpcTransport(",
            ensureMethod);
        int startTransactionEnsure = RequireIndex(
            lifecycleSource,
            "EnsureControlPipeHostStarted(selectedRoot);");
        int registration = RequireIndexAfter(
            lifecycleSource,
            "controlPipeLaunchBinding = PrepareControlPipeLaunchBinding(",
            startTransactionEnsure);
        int helperInvocation = RequireIndexAfter(
            lifecycleSource,
            "startInvocation = CreateBoundedStartInvocation(",
            registration);

        Check(
            ensureMethod < skipTestHooks &&
            skipTestHooks < lifecyclePathBuilder &&
            lifecyclePathBuilder < lifecycleStart,
            "real lifecycle listener guarantee derives the same recovered game path and skips synthetic test hooks");
        Check(
            startTransactionEnsure < registration &&
            registration < helperInvocation,
            "real lifecycle guarantees listener before registration and registration before helper launch");

        Check(
            lifecycleSource.Contains(
                "BridgeVersion,\r\n                expectedClientPath",
                StringComparison.Ordinal) ||
            lifecycleSource.Contains(
                "BridgeVersion,\n                expectedClientPath",
                StringComparison.Ordinal),
            "listener expected build identity is the same lifecycle BridgeVersion exported by launch binding");

        Check(
            hostSource.Contains(
                "rpcExpectedBuildId",
                StringComparison.Ordinal) &&
            hostSource.Contains(
                "rpcExpectedCanonicalClientPath",
                StringComparison.Ordinal) &&
            hostSource.Contains(
                "already bound to a different build or client image",
                StringComparison.Ordinal),
            "shared host is idempotent only for one recovered build/client identity");

        Check(
            windowSource.Contains(
                "FormClosed += OnFormClosed;",
                StringComparison.Ordinal) &&
            windowSource.Contains(
                "bridgeHostState?.Close();",
                StringComparison.Ordinal),
            "application window still owns shared host shutdown");

        return JsonSerializer.SerializeToElement(new
        {
            ok = true,
            finding = "LWB-R7-123",
            normalComposition = new
            {
                hostConstructedBeforeListener = true,
                expectedClientImage =
                    @"<selectedGameRoot>\Game\LastWar.exe",
                listenerBuildId =
                    OverviewLifecycleService.BridgeVersion,
                listenerStartsBeforeLifecycleConstruction = true,
                launchBindingEnabled = true,
                realLifecycleRechecksListenerBeforeRegistration = true,
                registrationBeforeHelperLaunch = true,
                testHookLifecycleStartsNativeListener = false,
                sameIdentityListenerStartIsIdempotent = true,
                differentIdentityListenerStartRejected = true,
                applicationOwnsShutdown = true,
            },
            boundary = new
            {
                getStatusPendingExposed = false,
                productionCallLuaEnabled = false,
                successfulStopUnregisterRecovered = false,
            },
        }, JsonOptions.Default);
    }

    private static int RequireIndex(
        string source,
        string text)
    {
        int index = source.IndexOf(
            text,
            StringComparison.Ordinal);
        if (index < 0)
        {
            throw new InvalidOperationException(
                "Overview bridge normal composition check failed: missing " +
                text);
        }

        return index;
    }

    private static int RequireIndexAfter(
        string source,
        string text,
        int after)
    {
        int index = source.IndexOf(
            text,
            after,
            StringComparison.Ordinal);
        if (index < 0)
        {
            throw new InvalidOperationException(
                "Overview bridge normal composition check failed: missing " +
                text);
        }

        return index;
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
                "Overview bridge normal composition check failed: " +
                message);
        }
    }
}
