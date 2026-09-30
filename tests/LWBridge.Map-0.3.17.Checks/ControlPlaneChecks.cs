namespace LWBridge.Map317.Checks;

internal static class ControlPlaneChecks
{
    internal static void Run()
    {
        long now = 10_000;
        using MapStore store = MapStore.CreateInMemory();
        var provider = new Provider();
        using var plane = new MapControlPlane(store, provider, () => now);

        MapScanState started = plane.StartScanAsync(new MapScanStartRequest(["city"], "normal")).AsTask().GetAwaiter().GetResult();
        TestAssert.True(store.ReadScanRun(started.ScanRunId)?.Status == "running", "control plane must persist accepted scan run");

        bool eventSawPersistedProgress = false;
        plane.ScanStateChanged += (_, args) =>
        {
            if (args.State.CompletedBlocks == 2)
            {
                MapScanRun? persisted = store.ReadScanRun(args.State.ScanRunId);
                eventSawPersistedProgress = persisted?.CompletedBlocks == 2;
            }
        };
        now += 2_000;
        MapScanState progress = plane.ReportProgress(new MapScanProgressUpdate(2, 99, 0, 1, 999, true, 3, 0));
        TestAssert.True(eventSawPersistedProgress, "progress must persist before state event publication");
        TestAssert.Equal(2, progress.ReadBlocks, "control plane progress must use exact derived readBlocks");

        plane.StageRecord(StoreChecks.Record("city", provider.Context.ServerId, "live-1", now, "live-1", "Live", null, 1, null, null,
            "{\"ownerUid\":\"live-owner\",\"ownerName\":\"Live\"}"));
        MapSearchResult active = plane.Search(new MapQuery("city", provider.Context.ServerId));
        TestAssert.Equal(1, active.Total, "control-plane search must use matching active staging run");

        now += 1_000;
        plane.ReportProgress(new MapScanProgressUpdate(provider.TotalBlocks, provider.TotalBlocks, 0, 0, 0, true, 0, 0));
        MapScanState completed = plane.CompleteScan();
        TestAssert.True(!completed.IsReading && completed.Phase == "completed" && completed.ProgressPercent == 100.0,
            "completed control-plane state mismatch");
        TestAssert.Equal(1, plane.Search(new MapQuery("city", provider.Context.ServerId)).Total,
            "completed scan must query published rows");

        IReadOnlyList<int> history = plane.SetServerJumpHistory([2212, 2212, 10, 20, 30, 40, 50]);
        TestAssert.True(history.SequenceEqual([2212, 10, 20, 30, 40]), "control-plane history normalization mismatch");
    }

    private sealed class Provider : IMapProvider
    {
        internal int TotalBlocks { get; } = 4;
        internal MapProviderContext Context { get; private set; } = new(true, true, 2212, "live", 0, 1000, 1000);

        public ValueTask<MapProviderContext> GetContextAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(Context);
        public ValueTask<MapProviderContext> EnterWorldMapAsync(CancellationToken cancellationToken = default)
        {
            Context = Context with { IsInWorld = true };
            return ValueTask.FromResult(Context);
        }
        public ValueTask<MapProviderStartResult> StartMapScanAsync(MapProviderStartRequest request, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new MapProviderStartResult(true, TotalBlocks, NativeCaptureReady: true));
        public ValueTask StopMapScanAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
}
