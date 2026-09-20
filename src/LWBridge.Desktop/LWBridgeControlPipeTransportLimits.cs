namespace LWBridge.Desktop;

// LWB-R7-099: numeric transport limits recovered from the original 0.3.1
// bridge host. This is intentionally an offline boundary model only: it does
// not open a pipe, start an accept loop, enqueue production traffic, or own
// pending Lua calls.
internal static class LWBridgeControlPipeTransportLimits
{
    // The original bounded sender stores 0x2000 permit units and consumes two
    // units per queued item; the adjacent configured maximum is 0x1000.
    public const int OutboundQueuePermitUnits = 0x2000;
    public const int OutboundPermitUnitsPerItem = 2;
    public const int OutboundQueueItemCapacity = 0x1000;

    // A separate semaphore is constructed with 0x04000000 permits and the
    // routing path acquires permits equal to the encoded outbound frame bytes.
    public const long OutboundByteBudget = 0x04000000;

    // Inbound accounting checks the existing item count against 0x0FFF and the
    // post-add byte count against 0x02000000 before queue admission.
    public const int InboundQueueItemCapacity = 0x1000;
    public const long InboundQueueByteBudget = 0x02000000;

    public static readonly TimeSpan InboundBackpressureTimeout = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan WriteTimeout = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan IdleActivityTimeout = TimeSpan.FromSeconds(30);

    public static bool CanEnqueueOutboundItem(int queuedItems) =>
        queuedItems >= 0 && queuedItems < OutboundQueueItemCapacity;

    public static bool CanReserveOutboundBytes(long reservedBytes, long frameBytes) =>
        reservedBytes >= 0 &&
        frameBytes > 0 &&
        frameBytes <= OutboundByteBudget &&
        reservedBytes <= OutboundByteBudget - frameBytes;

    public static bool CanEnqueueInbound(
        int queuedItems,
        long queuedBytes,
        long messageBytes) =>
        queuedItems >= 0 &&
        queuedItems < InboundQueueItemCapacity &&
        queuedBytes >= 0 &&
        messageBytes > 0 &&
        messageBytes <= InboundQueueByteBudget &&
        queuedBytes <= InboundQueueByteBudget - messageBytes;
}
