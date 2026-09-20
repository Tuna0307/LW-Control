using System.Security.Cryptography;

namespace LWBridge.Desktop;

// LWB-R7-110 + R7-117: application-global ownership shell for the recovered original
// bridge host. The original application constructs one bridge host during app
// startup (RVA 0x4100B5 -> 0x3C30BE), while profile start consumes that shared
// host. This shell owns the single registry and pipe identity for the desktop
// process, but deliberately does not open the native pipe or start transport.
internal sealed class LWBridgeControlPipeHostState : IDisposable
{
    private readonly object gate = new();
    private readonly LWBridgeControlPipeRegistry registry;
    private readonly Func<byte[]> pipeTokenEntropyFactory;
    private bool stopped;

    internal LWBridgeControlPipeHostState(
        string? pipePath = null,
        LWBridgeControlPipeRegistry? registry = null,
        Func<byte[]>? pipeTokenEntropyFactory = null)
    {
        PipePath = string.IsNullOrWhiteSpace(pipePath)
            ? LWBridgeControlPipeContract.GetCurrentUserFullPath()
            : pipePath;
        this.registry = registry ?? new LWBridgeControlPipeRegistry();
        this.pipeTokenEntropyFactory = pipeTokenEntropyFactory ??
            (() => RandomNumberGenerator.GetBytes(
                LWBridgeProxyLaunchEnvironmentContract.PipeTokenEntropyBytes));
    }

    public string PipePath { get; }

    public bool IsStopped
    {
        get { lock (gate) return stopped; }
    }

    public int PendingRegistrationCount => registry.PendingCount;

    public int ConnectedRouteCount => registry.ConnectedCount;

    internal LWBridgeControlPipeLaunchBinding PrepareLaunchBinding(
        string profileId,
        string instanceId,
        string buildId,
        long nowMilliseconds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(buildId);
        if (nowMilliseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(nowMilliseconds));

        lock (gate)
        {
            ThrowIfStopped();

            byte[] entropy = pipeTokenEntropyFactory();
            if (entropy.Length !=
                LWBridgeProxyLaunchEnvironmentContract.PipeTokenEntropyBytes)
            {
                throw new InvalidOperationException(
                    "LWBridge pipe-token entropy source returned an invalid byte count.");
            }

            string token =
                LWBridgeProxyLaunchEnvironmentContract.EncodePipeToken(entropy);
            long expiresAt = checked(
                nowMilliseconds +
                LWBridgeControlPipeRegistry
                    .StartupRegistrationLifetimeMilliseconds);
            registry.Register(profileId, instanceId, token, expiresAt);

            IReadOnlyDictionary<string, string> environment =
                LWBridgeProxyLaunchEnvironmentContract.CreateBindings(
                    profileId,
                    instanceId,
                    token,
                    buildId);

            return new LWBridgeControlPipeLaunchBinding(
                profileId,
                instanceId,
                token,
                buildId,
                expiresAt,
                environment);
        }
    }

    internal long RefreshLaunchBinding(
        string instanceId,
        long nowMilliseconds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        if (nowMilliseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(nowMilliseconds));

        lock (gate)
        {
            ThrowIfStopped();
            long expiresAt = checked(
                nowMilliseconds +
                LWBridgeControlPipeRegistry
                    .StartupRegistrationLifetimeMilliseconds);
            registry.RefreshPending(instanceId, expiresAt);
            return expiresAt;
        }
    }

    internal void CancelLaunchBinding(string instanceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        // Explicit launch-failure cleanup remains legal while the host is
        // stopping, matching the recovered unregister cleanup futures.
        registry.Unregister(instanceId);
    }

    internal LWBridgeControlPipeRegistry RequireActiveRegistry()
    {
        lock (gate)
        {
            ThrowIfStopped();
            return registry;
        }
    }

    private void ThrowIfStopped()
    {
        if (stopped)
        {
            throw new BridgeCommandException(
                "BRIDGE_STOPPED",
                "The shared bridge host is stopped.");
        }
    }

    public void Close()
    {
        lock (gate)
            stopped = true;
    }

    public void Dispose() => Close();
}
