using System.Text.Json;
using LWBridge.Desktop;
using LWBridge.Map317;

namespace LWBridge.Desktop.Checks;

// Inert production-command/status/provider/SQLite inverse. No game, launcher or desktop.
internal static class Completion010StatusGuardChecks
{
    private static JsonElement J(object x) => JsonSerializer.SerializeToElement(x, JsonOptions.Default);
    private static void Check(bool ok, string reason)
    {
        if (!ok) throw new InvalidOperationException("COMPLETION010_STATUS: " + reason);
    }

    internal static async Task RunAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "lwb317-completion010-status-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await SuccessorAsync(root, false);
            await SuccessorAsync(root, true);
            await OverlappingPollsAsync(root);
            await SameRunServerChangeAsync(root);
            await FailedAndCancelledRepliesAsync(root);
            Console.WriteLine("COMPLETION010 status ownership: 5/5 production-command inverse groups passed; real launches=0");
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task SuccessorAsync(string root, bool completeOld)
    {
        var provider = new HeldProvider();
        var late = new TaskCompletionSource<CurrentClientMapStatusContext>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var service = NewService(root, completeOld ? "completed-old" : "stopped-old", provider, _ => late.Task);
        var old = await Start(service);
        Task<object?> pending = service.InvokeAsync("map_scan_status", J(new { }), CancellationToken.None);
        if (completeOld) provider.Complete();
        else await service.InvokeAsync("map_scan_stop", J(new { }), CancellationToken.None);
        provider.Server = completeOld ? 317 : 318;
        var successor = await Start(service);
        int stops = provider.StopCalls;
        late.SetResult(Context(318));
        var answer = (MapScanState)(await pending)!;
        Check(answer.ScanRunId == successor.ScanRunId && answer.IsReading,
            "old observation cannot replace successor state (completed/cancelled)");
        Check(provider.StopCalls == stops && provider.Active == successor.ScanRunId,
            "old observation must not Stop successor provider");
        using var read = new MapStore(Path.Combine(root, completeOld ? "completed-old.db" : "stopped-old.db"));
        Check(read.ReadScanRun(successor.ScanRunId)?.Status == "running",
            "successor SQLite run remains running");
        await service.InvokeAsync("map_scan_stop", J(new { }), CancellationToken.None);
    }

    private static async Task OverlappingPollsAsync(string root)
    {
        var provider = new HeldProvider();
        var first = new TaskCompletionSource<CurrentClientMapStatusContext>(TaskCreationOptions.RunContinuationsAsynchronously);
        int n = 0;
        using var service = NewService(root, "overlap", provider, _ =>
            Interlocked.Increment(ref n) == 1 ? first.Task : Task.FromResult(Context(317)));
        var started = await Start(service);
        Task<object?> stale = service.InvokeAsync("map_scan_status", J(new { }), CancellationToken.None);
        var fresh = (MapScanState)(await service.InvokeAsync("map_scan_status", J(new { }), CancellationToken.None))!;
        first.SetResult(Context(318));
        var old = (MapScanState)(await stale)!;
        Check(fresh.IsReading && old.IsReading && old.ScanRunId == started.ScanRunId &&
            provider.StopCalls == 0, "older overlapping poll must not trigger server failure");
        await service.InvokeAsync("map_scan_stop", J(new { }), CancellationToken.None);
    }

    private static async Task SameRunServerChangeAsync(string root)
    {
        var provider = new HeldProvider();
        using var service = NewService(root, "same-run", provider, _ => Task.FromResult(Context(318)));
        var started = await Start(service);
        var changed = (MapScanState)(await service.InvokeAsync("map_scan_status", J(new { }), CancellationToken.None))!;
        using var read = new MapStore(Path.Combine(root, "same-run.db"));
        Check(!changed.IsReading && changed.Error == Map317CommandService.ServerChangedDuringScanText &&
            read.ReadScanRun(started.ScanRunId)?.Status == "failed" && provider.StopCalls == 1,
            "same-run server change must still fail/discard and stop original provider");
    }

    private static async Task FailedAndCancelledRepliesAsync(string root)
    {
        foreach (string mode in new[] { "rejected", "cancelled" })
        {
            var provider = new HeldProvider();
            var deferred = new TaskCompletionSource<CurrentClientMapStatusContext>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var service = NewService(root, mode, provider, _ => deferred.Task);
            var started = await Start(service);
            Task<object?> pending = service.InvokeAsync("map_scan_status", J(new { }), CancellationToken.None);
            if (mode == "cancelled") deferred.SetCanceled();
            else deferred.SetException(new BridgeCommandException("GAME_CONNECTION_UNAVAILABLE", "observation failed"));
            try
            {
                var answer = await pending;
                Check(mode == "rejected" && ((MapScanState)answer!).ScanRunId == started.ScanRunId,
                    "rejection returns current state without error mutation");
            }
            catch (OperationCanceledException) when (mode == "cancelled") { }
            Check(((MapScanState)service.CreateStatus()).IsReading && provider.StopCalls == 0,
                "rejected/cancelled observation cannot stop run");
            await service.InvokeAsync("map_scan_stop", J(new { }), CancellationToken.None);
        }
    }

    private static Map317CommandService NewService(string root, string name, HeldProvider provider,
        Func<CancellationToken, Task<CurrentClientMapStatusContext>> statusReader) =>
        new(Path.Combine(root, name + ".db"), provider,
            UnavailableMapActionProvider.Instance, startPlunderWorkers: false, statusReader: statusReader);

    private static async Task<MapScanState> Start(Map317CommandService service) =>
        (MapScanState)(await service.InvokeAsync("map_scan_start",
            J(new { selectedTypes = new[] { "city" }, scanMode = "normal" }), CancellationToken.None))!;

    private static CurrentClientMapStatusContext Context(int server) =>
        new(true, server, server, [], [], 1, 100, 100, 0, 0);

    private sealed class HeldProvider : IMap317RunScopedProvider
    {
        public int Server { get; set; } = 317;
        public int StopCalls { get; private set; }
        public string? Active { get; private set; }
        private MapControlPlane? control;
        public event Action<string>? RunTerminated;
        public ValueTask<MapProviderContext> GetContextAsync(CancellationToken token = default) =>
            ValueTask.FromResult(new MapProviderContext(true, true, Server, "live", 1, 100, 100, 1));
        public ValueTask<MapProviderContext> EnterWorldMapAsync(CancellationToken token = default) =>
            GetContextAsync(token);
        public ValueTask<MapProviderStartResult> StartMapScanAsync(MapProviderStartRequest request,
            CancellationToken token = default) =>
            ValueTask.FromResult(new MapProviderStartResult(true, 1, true));
        public void ActivateAcceptedRun(MapControlPlane control, string scanRunId)
        {
            this.control = control; Active = scanRunId;
        }
        public void Complete()
        {
            control!.CompleteScan();
            Retire();
        }
        public ValueTask StopMapScanAsync(CancellationToken token = default)
        {
            StopCalls++; Retire(); return ValueTask.CompletedTask;
        }
        private void Retire()
        {
            if (Active is { } old) { Active = null; control = null; RunTerminated?.Invoke(old); }
        }
        public void Dispose() => Retire();
    }
}
