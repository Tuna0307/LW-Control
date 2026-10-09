namespace LWBridge.Desktop;

// LWB-R7-109: source-backed ownership/order/cleanup anchors for the original
// bridge startup path. This is an offline contract model only. It does not
// construct a listener, register a live instance, mutate environment state,
// launch a child, or enable call_lua.
internal static class LWBridgeControlPipeStartupContract
{
    public const int ApplicationStartupFunctionRva = 0x40F546;
    public const int HostConstructorCallRva = 0x4100B5;
    public const int HostConstructorRva = 0x3C30BE;

    public const int ProfileInstanceStartWrapperRva = 0x1FF192;
    public const int ProfileInstanceStartLaunchPollCallRva = 0x1FF83B;
    public const int ProfileLaunchPollRva = 0x1D3F1B;

    public const int StartupRegistrationCallRva = 0x1D7C94;
    public const int StartupRegistrationWrapperRva = 0x3CCA38;

    public const int LaunchEnvelopeCloneSourceRva = 0x1DB119;
    public const int LaunchEnvelopeCloneCallRva = 0x1DB12B;
    public const int LaunchEnvelopeCloneTargetRva = 0x02A2C0;

    public const int PendingRefreshClockCallRva = 0x1DC033;
    public const int PendingRefreshDeadlineAddRva = 0x1DC038;
    public const int PendingRefreshCallRva = 0x1DC071;
    public const int PendingRefreshTargetRva = 0x3C2F0C;

    public const int CleanupFutureRva = 0x1D311F;
    public const int CleanupUnregisterCallRva = 0x1D3203;
    public const int LaterCleanupFutureRva = 0x1D337A;
    public const int LaterCleanupUnregisterCallRva = 0x1D393C;
    public const int ExplicitUnregisterWrapperRva = 0x3CC8B3;
    public const int ExplicitUnregisterCoreCallRva = 0x3CC8D2;
    public const int ExplicitUnregisterCoreRva = 0x3C2436;

    public const int RegistrationLifetimeMilliseconds = 90_000;

    public const string ListenerOwnership = "host-global shared listener";
    public const string LaunchFailureCleanup = "explicit unregister through cleanup future";
}
