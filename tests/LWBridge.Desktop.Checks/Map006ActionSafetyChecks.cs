using System.Text.Json;
using LWBridge.Desktop;
using Map317 = LWBridge.Map317;

namespace LWBridge.Desktop.Checks;

internal static class Map006ActionSafetyChecks
{
    internal static async Task RunAsync()
    {
        // The source-backed plunder transport has been deleted. Legacy provider
        // signatures fail before requesting a native session or sending a command.
        var isolatedSource = new CurrentClientMapBlockSource(
            () => throw new InvalidOperationException("retired action requested game session"),
            null, null,
            new CurrentClientMapBlockSourceHooks
            {
                WriteTextAtomic = (_, _) => throw new InvalidOperationException("retired action sent game command"),
            });
        var provider = new CurrentClientMap317ActionProvider(isolatedSource);
        await RejectProviderAsync(
            () => provider.GetTreasureClaimStatusAsync().AsTask(), "GAME_PROVIDER_UNAVAILABLE");
        await RejectProviderAsync(
            () => provider.ClaimTreasuresAsync(new Map317.TreasureClaimProviderRequest(
                317, [], "single", "12345", true)).AsTask(), "GAME_PROVIDER_UNAVAILABLE");

        await RejectProviderAsync(
            () => provider.ShareDispatchTaskToAllianceAsync(JsonSerializer.SerializeToElement(new { uuid = "1" })).AsTask(),
            "FEATURE_REMOVED");
        await RejectProviderAsync(
            () => provider.PrepareGhostPlunderTasksAsync([]).AsTask(), "FEATURE_REMOVED");
        await RejectProviderAsync(
            () => provider.ArmDispatchPlunderAsync([new Map317.DispatchPlunderArmJob("dispatch",317,317,"1",1)]).AsTask(),
            "FEATURE_REMOVED");
        await RejectProviderAsync(
            () => provider.ArmGhostPlunderAsync([new Map317.DispatchPlunderArmJob("ghost",317,317,"1",1)]).AsTask(),
            "FEATURE_REMOVED");
        await RejectProviderAsync(
            () => provider.ArmTruckPlunderAsync(new Map317.TruckPlunderArmJob(317,"1","job",1,null,null)).AsTask(),
            "FEATURE_REMOVED");
        await RejectProviderAsync(
            () => provider.DrainDispatchPlunderResultsAsync().AsTask(), "FEATURE_REMOVED");
        await RejectProviderAsync(
            () => provider.DrainGhostPlunderResultsAsync().AsTask(), "FEATURE_REMOVED");
        await RejectProviderAsync(
            () => provider.DrainTruckPlunderResultsAsync().AsTask(), "FEATURE_REMOVED");
        await RejectProviderAsync(
            () => provider.ClearTruckPlunderPendingAsync(317,"1","job").AsTask(), "FEATURE_REMOVED");
        Console.WriteLine("MAP_006_RETIRED_ACTION_PROVIDER_REJECTS_OK");
    }

    private static async Task RejectProviderAsync(Func<Task> action, string code)
    {
        try { await action(); }
        catch (Map317.BridgeCommandException error) when (error.Code == code) { return; }
        throw new InvalidOperationException("MAP_006_ACTION: expected production provider error " + code);
    }
}
