using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class OverviewBridgeStartupContractChecks
{
    internal static JsonElement Run()
    {
        Check(
            LWBridgeControlPipeStartupContract.ApplicationStartupFunctionRva == 0x40F546 &&
            LWBridgeControlPipeStartupContract.HostConstructorCallRva == 0x4100B5 &&
            LWBridgeControlPipeStartupContract.HostConstructorRva == 0x3C30BE,
            "original application startup owns the shared bridge host/listener constructor");

        Check(
            LWBridgeControlPipeStartupContract.ProfileInstanceStartWrapperRva == 0x1FF192 &&
            LWBridgeControlPipeStartupContract.ProfileInstanceStartLaunchPollCallRva == 0x1FF83B &&
            LWBridgeControlPipeStartupContract.ProfileLaunchPollRva == 0x1D3F1B,
            "profile_instance_start is pinned to the recovered launch future");

        Check(
            LWBridgeControlPipeStartupContract.StartupRegistrationCallRva == 0x1D7C94 &&
            LWBridgeControlPipeStartupContract.StartupRegistrationWrapperRva == 0x3CCA38,
            "startup registration callsite remains pinned");

        Check(
            LWBridgeControlPipeStartupContract.LaunchEnvelopeCloneSourceRva == 0x1DB119 &&
            LWBridgeControlPipeStartupContract.LaunchEnvelopeCloneCallRva == 0x1DB12B &&
            LWBridgeControlPipeStartupContract.LaunchEnvelopeCloneTargetRva == 0x02A2C0,
            "LaunchEnvelope handoff remains pinned after registration");

        Check(
            LWBridgeControlPipeStartupContract.PendingRefreshClockCallRva == 0x1DC033 &&
            LWBridgeControlPipeStartupContract.PendingRefreshDeadlineAddRva == 0x1DC038 &&
            LWBridgeControlPipeStartupContract.PendingRefreshCallRva == 0x1DC071 &&
            LWBridgeControlPipeStartupContract.PendingRefreshTargetRva == 0x3C2F0C &&
            LWBridgeControlPipeStartupContract.RegistrationLifetimeMilliseconds ==
                LWBridgeControlPipeRegistry.StartupRegistrationLifetimeMilliseconds,
            "pending launch registration refreshes to the same 90-second source-backed deadline");

        Check(
            LWBridgeControlPipeStartupContract.CleanupFutureRva == 0x1D311F &&
            LWBridgeControlPipeStartupContract.CleanupUnregisterCallRva == 0x1D3203 &&
            LWBridgeControlPipeStartupContract.LaterCleanupFutureRva == 0x1D337A &&
            LWBridgeControlPipeStartupContract.LaterCleanupUnregisterCallRva == 0x1D393C &&
            LWBridgeControlPipeStartupContract.ExplicitUnregisterWrapperRva == 0x3CC8B3 &&
            LWBridgeControlPipeStartupContract.ExplicitUnregisterCoreCallRva == 0x3CC8D2 &&
            LWBridgeControlPipeStartupContract.ExplicitUnregisterCoreRva == 0x3C2436,
            "launch cleanup futures explicitly unregister the instance registration");

        Check(
            LWBridgeControlPipeStartupContract.ListenerOwnership == "host-global shared listener" &&
            LWBridgeControlPipeStartupContract.LaunchFailureCleanup == "explicit unregister through cleanup future",
            "ownership and cleanup semantics remain fail-closed source-backed constants");

        return JsonSerializer.SerializeToElement(new
        {
            ok = true,
            finding = "LWB-R7-109",
            recovered = new
            {
                listenerOwnership = LWBridgeControlPipeStartupContract.ListenerOwnership,
                registrationBeforeLaunchEnvelopeHandoff = true,
                pendingRefreshMilliseconds = LWBridgeControlPipeStartupContract.RegistrationLifetimeMilliseconds,
                launchFailureCleanup = LWBridgeControlPipeStartupContract.LaunchFailureCleanup,
                explicitUnregister = "0x1D3203/0x1D393C -> 0x3CC8B3 -> 0x3C2436",
            },
            boundary = new
            {
                productionNamedPipeListenerImplemented = false,
                productionCallLuaEnabled = false,
                pendingCollectionImplemented = false,
            },
        }, JsonOptions.Default);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("Overview bridge startup contract check failed: " + message);
    }
}
