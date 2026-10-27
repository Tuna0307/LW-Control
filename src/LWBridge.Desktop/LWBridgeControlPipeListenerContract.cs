namespace LWBridge.Desktop;

// LWB-R7-100: listener construction and accept semantics recovered from
// lwbridge-0.3.1.exe. Consumed by LWBridgeControlPipeNativeServer and the
// live handshake; this class itself opens no pipe.
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
}
