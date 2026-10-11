using LWBridge.Desktop;
using System.Text.Json;

string root = Path.Combine(Path.GetTempPath(), "lw-map-lead-stop-" + Guid.NewGuid().ToString("N"));
var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var stopEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var releaseStop = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var returned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
bool nativeLeaseHeld = false;
int currentServer = 317;
int returnCalls = 0;
bool returnBeforeStop = false;
try
{
    await using var auto = new MapAutoScanCommandService(Path.Combine(root, "auto.json"),
        new MapAutoScanExecutionBoundary
        {
            IsOnline = () => true,
            IsMapScanActive = () => nativeLeaseHeld,
            ReadStatusAsync = async (run, token) =>
            {
                if (run is null) return new(currentServer, false, null, null, "idle");
                entered.SetResult();
                await Task.Delay(Timeout.Infinite, token);
                throw new Exception("Unreachable");
            },
            StartTargetScanAsync = (server, _, _, _) =>
            {
                currentServer = server;
                nativeLeaseHeld = true;
                return Task.FromResult("lead-off-origin-run");
            },
            StopScanIfOwnedAsync = async (run, _) =>
            {
                if (run != "lead-off-origin-run") throw new Exception("Wrong Stop owner");
                stopEntered.SetResult();
                await releaseStop.Task;
                nativeLeaseHeld = false;
                return true;
            },
            ReturnServerAsync = (server, _) =>
            {
                returnCalls++;
                returnBeforeStop = nativeLeaseHeld;
                returned.TrySetResult();
                if (nativeLeaseHeld) throw new InvalidOperationException("SCAN_RUNNING: native Stop still owns lease");
                currentServer = server;
                return Task.CompletedTask;
            },
        }, new MapAutoScanSchedulerHooks
        {
            UtcNowMilliseconds = () => 1_800_000_000_000,
            DelayAsync = (_, _) => Task.CompletedTask,
        }, startScheduler: false);
    await auto.UpdateConfigAsync(new(true, 20, [318], ["truck"], "fast", true, 0));
    await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
    Task cancel = auto.CancelAsync();
    await stopEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
    try { await returned.Task.WaitAsync(TimeSpan.FromSeconds(2)); }
    catch (TimeoutException) { }
    releaseStop.SetResult();
    await cancel.WaitAsync(TimeSpan.FromSeconds(5));
    var final = await auto.GetSnapshotAsync();
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        evidenceType = "controlled-production-scheduler-exact-source-linked",
        returnBeforeStop, returnCalls, currentServer,
        final.LastError, final.LastCycle, final.OwnsActiveScan,
        failureReproduced = returnBeforeStop && currentServer == 318 && final.LastError != null,
        externalGameActions = 0,
    }, new JsonSerializerOptions { WriteIndented = true }));
    await auto.UpdateConfigAsync(final.Config with { Enabled = false });
}
finally
{
    releaseStop.TrySetResult();
    if (Directory.Exists(root)) Directory.Delete(root, true);
}

namespace LWBridge.Desktop
{
    internal interface INativeAsyncCommandService
    {
        bool CanHandle(string command);
        Task<object?> InvokeAsync(string command, JsonElement payload, CancellationToken cancellationToken);
    }
    internal sealed class BridgeCommandException(string code, string message) : Exception(message)
    {
        public string Code { get; } = code;
    }
}
