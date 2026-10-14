using System.Text.Json;
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

        // Exercise the actual production provider, without an owned game session
        // or runtime transport. Unsupported claims/Ghost execution must reject
        // before a session is read, even for source-valid request identities.
        var isolatedSource = new CurrentClientMapBlockSource(
            () => throw new InvalidOperationException("unsupported action accessed game session"),
            null, null,
            new CurrentClientMapBlockSourceHooks
            {
                WriteTextAtomic = (_, _) => throw new InvalidOperationException("unsupported action sent game command"),
            });
        var provider = new CurrentClientMap317ActionProvider(isolatedSource);
        await RejectProviderAsync(
            () => provider.GetTreasureClaimStatusAsync().AsTask(),
            "GAME_PROVIDER_UNAVAILABLE");
        await RejectProviderAsync(
            () => provider.ClaimTreasuresAsync(new Map317.TreasureClaimProviderRequest(
                317, [], "single", "12345", true)).AsTask(),
            "GAME_PROVIDER_UNAVAILABLE");
        var validGhost = new Map317.DispatchPlunderArmJob(
            "ghost", 317, 318, "12345", 1_800_000_000_000);
        await RejectProviderAsync(
            () => provider.ArmGhostPlunderAsync([validGhost]).AsTask(),
            "GHOST_PLUNDER_PROVIDER_UNAVAILABLE");
        await RejectProviderAsync(
            () => provider.ArmGhostPlunderAsync([
                validGhost with { TaskUuid = "9223372036854775808" }]).AsTask(),
            "INVALID_REQUEST");
        await RejectProviderAsync(
            () => provider.ArmGhostPlunderAsync([validGhost, validGhost]).AsTask(),
            "INVALID_REQUEST");
        if ((await provider.DrainGhostPlunderResultsAsync()).Count != 0)
            throw new InvalidOperationException("MAP_006_ACTION: unsupported Ghost fabricated a terminal result");

        JsonElement unsupportedUuid = JsonSerializer.SerializeToElement(new
        {
            kind = "ghost", taskKind = "ghost", pointType = 29,
            runtimeClass = "GhostreconPointInfo", serverId = 317,
            uuid = "9223372036854775808", ownerServer = 318,
            completionTime = 1_800_000_000_000L, protectTime = 30,
            plunderAt = 1_800_000_030_000L, taskExpireTime = 1_800_000_060_000L,
            stealListCount = 0, stolenCount = 0, stealMaxTimes = 2, maxStealCount = 2,
        });
        await RejectProviderAsync(
            () => provider.PrepareGhostPlunderTasksAsync([unsupportedUuid]).AsTask(),
            "INVALID_REQUEST");
        JsonElement overflowedProtection = JsonSerializer.SerializeToElement(new
        {
            kind = "ghost", taskKind = "ghost", pointType = 29,
            runtimeClass = "GhostreconPointInfo", serverId = 317,
            uuid = "12345", ownerServer = 318,
            completionTime = 1_800_000_000_000L, protectTime = long.MaxValue,
            plunderAt = long.MaxValue, taskExpireTime = 0L,
            stealListCount = 0, stolenCount = 0, stealMaxTimes = 2, maxStealCount = 2,
        });
        await RejectProviderAsync(
            () => provider.PrepareGhostPlunderTasksAsync([overflowedProtection]).AsTask(),
            "INVALID_REQUEST");

        Console.WriteLine("MAP_006_GHOST_DISPATCH_TRANSPORT_FENCE_OK");
    }

    private static async Task RejectProviderAsync(Func<Task> action, string code)
    {
        try { await action(); }
        catch (Map317.BridgeCommandException error) when (error.Code == code) { return; }
        throw new InvalidOperationException("MAP_006_ACTION: expected production provider error " + code);
    }
}
