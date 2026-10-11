using LWBridge.Desktop;
using Map317 = LWBridge.Map317;

namespace LWBridge.Desktop.Checks;

internal static class Map006ActionSafetyChecks
{
    internal static async Task RunAsync()
    {
        // No live game and no command transport: the exact production bridge
        // must reject a Ghost job *before* opening a session or writing a
        // Dispatch plunder request. This also covers persisted legacy Ghost jobs.
        var source = new CurrentClientMapBlockSource(
            () => null, null, null);
        try
        {
            await source.ArmDispatchPlunderBatchAsync(
                [new Map317.DispatchPlunderArmJob("ghost", 317, 317, "12345", 1_800_000_000_000)],
                CancellationToken.None);
            throw new InvalidOperationException("MAP_006_ACTION: Ghost incorrectly entered Dispatch transport");
        }
        catch (BridgeCommandException error) when (error.Code == "GHOST_PLUNDER_PROVIDER_UNAVAILABLE")
        {
            // Expected explicit unsupported current-client Ghost route.
        }

        Console.WriteLine("MAP_006_GHOST_DISPATCH_TRANSPORT_FENCE_OK");
    }
}
