using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class OverviewBridgeTransportLimitsChecks
{
    internal static JsonElement Run()
    {
        Check(LWBridgeControlPipeTransportLimits.OutboundQueuePermitUnits == 0x2000,
            "outbound channel starts with 0x2000 permit units");
        Check(LWBridgeControlPipeTransportLimits.OutboundPermitUnitsPerItem == 2,
            "outbound channel consumes two permit units per item");
        Check(LWBridgeControlPipeTransportLimits.OutboundQueueItemCapacity == 4096,
            "outbound queue capacity is 4096 items");
        Check(LWBridgeControlPipeTransportLimits.OutboundByteBudget == 64L * 1024 * 1024,
            "outbound byte budget is 64 MiB");
        Check(LWBridgeControlPipeTransportLimits.InboundQueueItemCapacity == 4096,
            "inbound queue capacity is 4096 items");
        Check(LWBridgeControlPipeTransportLimits.InboundQueueByteBudget == 32L * 1024 * 1024,
            "inbound byte budget is 32 MiB");

        Check(LWBridgeControlPipeTransportLimits.CanEnqueueOutboundItem(4095),
            "outbound item 4096 is admitted");
        Check(!LWBridgeControlPipeTransportLimits.CanEnqueueOutboundItem(4096),
            "outbound item 4097 is rejected");
        Check(LWBridgeControlPipeTransportLimits.CanReserveOutboundBytes(
                LWBridgeControlPipeTransportLimits.OutboundByteBudget - 1, 1),
            "outbound byte budget admits an exact-boundary reservation");
        Check(!LWBridgeControlPipeTransportLimits.CanReserveOutboundBytes(
                LWBridgeControlPipeTransportLimits.OutboundByteBudget, 1),
            "outbound byte budget rejects one byte past the boundary");

        Check(LWBridgeControlPipeTransportLimits.CanEnqueueInbound(
                4095, LWBridgeControlPipeTransportLimits.InboundQueueByteBudget - 1, 1),
            "inbound admission permits exact item and byte boundaries");
        Check(!LWBridgeControlPipeTransportLimits.CanEnqueueInbound(4096, 0, 1),
            "inbound admission rejects item 4097");
        Check(!LWBridgeControlPipeTransportLimits.CanEnqueueInbound(
                0, LWBridgeControlPipeTransportLimits.InboundQueueByteBudget, 1),
            "inbound admission rejects one byte past the aggregate budget");

        Check(LWBridgeControlPipeTransportLimits.InboundBackpressureTimeout == TimeSpan.FromSeconds(30),
            "inbound backpressure timeout is 30 seconds");
        Check(LWBridgeControlPipeTransportLimits.WriteTimeout == TimeSpan.FromSeconds(10),
            "write timeout is 10 seconds");
        Check(LWBridgeControlPipeTransportLimits.IdleActivityTimeout == TimeSpan.FromSeconds(30),
            "idle activity timeout is 30 seconds");

        return JsonSerializer.SerializeToElement(new
        {
            ok = true,
            finding = "LWB-R7-099",
            recovered = new
            {
                outboundQueueItems = LWBridgeControlPipeTransportLimits.OutboundQueueItemCapacity,
                outboundBytes = LWBridgeControlPipeTransportLimits.OutboundByteBudget,
                inboundQueueItems = LWBridgeControlPipeTransportLimits.InboundQueueItemCapacity,
                inboundBytes = LWBridgeControlPipeTransportLimits.InboundQueueByteBudget,
                inboundBackpressureSeconds = LWBridgeControlPipeTransportLimits.InboundBackpressureTimeout.TotalSeconds,
                writeTimeoutSeconds = LWBridgeControlPipeTransportLimits.WriteTimeout.TotalSeconds,
                idleActivitySeconds = LWBridgeControlPipeTransportLimits.IdleActivityTimeout.TotalSeconds,
            },
            boundary = new
            {
                persistentNamedPipeListenerImplemented = false,
                pipeServerOptionsRecovered = false,
                proxyLaunchEnvironmentBound = false,
                productionCallLuaEnabled = false,
                authenticPendingCallCollectionOwned = false,
            },
        }, JsonOptions.Default);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("Overview bridge transport limits check failed: " + message);
    }
}
