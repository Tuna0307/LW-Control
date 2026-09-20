namespace LWBridge.Desktop;

// LWB-R7-110: application-global ownership shell for the recovered original
// bridge host. The original application constructs one bridge host during app
// startup (RVA 0x4100B5 -> 0x3C30BE), while profile start consumes that shared
// host. This shell owns the single registry and pipe identity for the desktop
// process, but deliberately does not open the native pipe or start transport.
internal sealed class LWBridgeControlPipeHostState : IDisposable
{
    private readonly object gate = new();
    private readonly LWBridgeControlPipeRegistry registry;
    private bool stopped;

    internal LWBridgeControlPipeHostState(
        string? pipePath = null,
        LWBridgeControlPipeRegistry? registry = null)
    {
        PipePath = string.IsNullOrWhiteSpace(pipePath)
            ? LWBridgeControlPipeContract.GetCurrentUserFullPath()
            : pipePath;
        this.registry = registry ?? new LWBridgeControlPipeRegistry();
    }

    public string PipePath { get; }

    public bool IsStopped
    {
        get { lock (gate) return stopped; }
    }

    public int PendingRegistrationCount => registry.PendingCount;

    public int ConnectedRouteCount => registry.ConnectedCount;

    internal LWBridgeControlPipeRegistry RequireActiveRegistry()
    {
        lock (gate)
        {
            if (stopped)
            {
                throw new BridgeCommandException(
                    "BRIDGE_STOPPED",
                    "The shared bridge host is stopped.");
            }

            return registry;
        }
    }

    public void Close()
    {
        lock (gate)
            stopped = true;
    }

    public void Dispose() => Close();
}
