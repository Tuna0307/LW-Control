namespace LWBridge.Desktop;

// LWB-R7-100: source-backed listener construction and accept semantics from
// lwbridge-0.3.1.exe. This remains an offline contract model: it never opens a
// named pipe or starts a production accept loop.
internal static class LWBridgeControlPipeListenerContract
{
    public const uint PipeAccessDuplex = 0x00000003;
    public const uint FileFlagFirstPipeInstance = 0x00080000;
    public const uint FileFlagOverlapped = 0x40000000;
    public const uint PipeRejectRemoteClients = 0x00000008;
    public const uint MaxInstances = 0x000000FF;
    public const uint OutboundBufferBytes = 0x00010000;
    public const uint InboundBufferBytes = 0x00010000;
    public const uint DefaultTimeoutMilliseconds = 0;
    public const uint SecurityAttributesLength = 24;

    public const int ErrorNoData = 232;
    public const int ErrorPipeConnected = 535;
    public const int ErrorIoPending = 997;

    public static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(5);

    public static uint GetOpenMode(bool firstServerInstance) =>
        PipeAccessDuplex |
        FileFlagOverlapped |
        (firstServerInstance ? FileFlagFirstPipeInstance : 0u);

    public static string GetSecurityDescriptorSddlForSid(string sid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sid);
        return $"D:P(A;;GA;;;SY)(A;;GA;;;{sid})";
    }

    public static LWBridgePipeConnectDisposition ClassifyConnectResult(
        bool connectNamedPipeReturnedTrue,
        int lastError)
    {
        if (connectNamedPipeReturnedTrue)
            return LWBridgePipeConnectDisposition.Connected;

        return lastError switch
        {
            ErrorNoData => LWBridgePipeConnectDisposition.Connected,
            ErrorPipeConnected => LWBridgePipeConnectDisposition.Connected,
            ErrorIoPending => LWBridgePipeConnectDisposition.Pending,
            _ => LWBridgePipeConnectDisposition.Failed,
        };
    }

    // The original accept state sets the first-instance byte to 1 only during
    // initial construction, then clears it immediately after the first server
    // object is created. Replacement accept instances therefore omit the flag.
    public static bool UseFirstPipeInstanceFlag(int serverInstanceOrdinal)
    {
        if (serverInstanceOrdinal < 0)
            throw new ArgumentOutOfRangeException(nameof(serverInstanceOrdinal));
        return serverInstanceOrdinal == 0;
    }
}

internal enum LWBridgePipeConnectDisposition
{
    Connected,
    Pending,
    Failed,
}
