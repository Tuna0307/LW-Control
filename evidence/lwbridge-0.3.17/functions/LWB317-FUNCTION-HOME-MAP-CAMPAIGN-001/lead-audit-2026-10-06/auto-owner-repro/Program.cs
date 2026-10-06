using System.Text.Json;
using LWBridge.Desktop;

// Execute the actual scheduler core, linking production source without Desktop,
// native processes, game providers, browser automation, or account storage.
var polling = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
string activeRun = "none";
var stoppedRuns = new List<string>();
string statePath = Path.Combine(Path.GetTempPath(), "lwb317-lead-owner-" + Guid.NewGuid().ToString("N") + ".json");
try
{
    await using var service = new MapAutoScanCommandService(statePath,
        new MapAutoScanExecutionBoundary
        {
            IsOnline = () => true,
            IsMapScanActive = () => activeRun != "none",
            ReadStatusAsync = _ => Task.FromResult(new MapAutoScanRuntimeStatus(321, activeRun != "none", null)),
            StartTargetScanAsync = (_, _, _, _) => { activeRun = "auto-A"; return Task.CompletedTask; },
            ReturnServerAsync = (_, _) => Task.CompletedTask,
            StopScanAsync = _ => { stoppedRuns.Add(activeRun); activeRun = "none"; return Task.CompletedTask; },
        },
        new MapAutoScanSchedulerHooks
        {
            UtcNowMilliseconds = () => 1_800_000_000_000L,
            DelayAsync = async (_, token) =>
            {
                polling.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            },
        }, startScheduler: false);
    await service.UpdateConfigAsync(MapAutoScanConfig.Default with
    {
        Enabled = true, ServerIds = new[] { 321 }, ReturnToOriginalServer = false,
    });
    await polling.Task.WaitAsync(TimeSpan.FromSeconds(5));
    var before = await service.GetSnapshotAsync();
    if (!before.OwnsActiveScan || activeRun != "auto-A") throw new Exception("Auto did not reach owned poll barrier");
    // External provider completion releases native scan admission between polls.
    activeRun = "none";
    // A separate Manual owner acquires the newly released native lease.
    activeRun = "manual-M";
    await service.UpdateConfigAsync(before.Config with { Enabled = false });
    bool reproduced = stoppedRuns.Contains("manual-M");
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        proofType = "actual-auto-service-core-with-inert-execution-boundary",
        externalActions = 0,
        scenario = "Auto completion, Manual replacement before poll, Auto disable",
        stoppedRuns, expectedManualStopped = false, defectReproduced = reproduced,
        scopeLimit = "Native Map317 completion/lease interleaving confirmed separately by source review",
    }, new JsonSerializerOptions { WriteIndented = true }));
    if (!reproduced) throw new Exception("Finding no longer reproduces; reassess current code");
}
finally
{
    if (File.Exists(statePath)) File.Delete(statePath);
}
