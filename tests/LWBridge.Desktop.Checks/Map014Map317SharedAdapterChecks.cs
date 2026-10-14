using System.Runtime.CompilerServices;
using System.Text.Json;
using LWBridge.Desktop;
using Map317 = LWBridge.Map317;

namespace LWBridge.Desktop.Checks;

// Exercises the actual Map317 accepted-run provider/engine/Map317 SQLite
// publication route. Only game context and native captures are simulated.
internal static class Map014Map317SharedAdapterChecks
{
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("MAP014_MAP317_SHARED: " + message);
    }

    private sealed class NativeSource : IMapScanSharedAcquisitionSource
    {
        public MapScanSharedAcquisitionOwner AcquisitionOwner { get; set; } = null!;
        public int Calls;
        public int Cleanups;
        public bool WrongCaptureOwner;
        public bool WrongCleanupOwner;
        public bool WrongSourceAoi;
        public IReadOnlyList<string>? CapturedNativeTypes;
        public int StagedSampleCount;
        public string CityRecordKey = "native-owner-1";
        public Action? AfterNativeHomeAcknowledged;

        public Task<MapScanBlockCapture> CaptureAsync(MapScanExecutionRequest _,
            MapScanTargetBlock __, CancellationToken ___) =>
            throw new InvalidOperationException("Opt-in Map317 shared source was downgraded to legacy per-block capture.");

        public async IAsyncEnumerable<MapScanSharedAcquisitionBatch> CaptureSharedAsync(
            MapScanExecutionRequest request, IReadOnlyList<MapScanTargetBlock> blocks,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Calls++;
            CapturedNativeTypes = request.SelectedTypes.ToArray();
            Require(request.SelectedTypes.SequenceEqual(AcquisitionOwner.SelectedTypes),
                "native type mapping must be known before capture");
            Require(blocks.Count == 2500 && blocks[0].MinX == 0 &&
                blocks[^1].MaxX == 999 && blocks[^1].MaxY == 999,
                "shared native input omitted a logical world seam");
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            MapStoredRecord treasure = new("treasure", request.ServerId, "real-treasure",
                10011, "real-treasure", null, null, null, null, null, null, null, 1800000000000L,
                JsonSerializer.Serialize(new { pointType = 21, runtimeClass = "Client.TreasurePointInfo",
                    treasureType = 8, suppliesType = 0, eventId = 0, x = 10, y = 10 }));
            MapStoredRecord supplies = treasure with
            {
                RecordKey = "real-supplies", Uuid = "real-supplies",
                DataJson = JsonSerializer.Serialize(new { pointType = 27,
                    runtimeClass = "Client.WorldSuppliesPoint", treasureType = 0,
                    suppliesType = 1, eventId = 0, x = 10, y = 10 })
            };
            MapStoredRecord city = new("city", request.ServerId, CityRecordKey, 10011,
                null, "current", null, 1, null, null, null, null, 1800000000000L,
                JsonSerializer.Serialize(new { ownerUid = CityRecordKey, x = 10, y = 10 }));
            MapStoredRecord[] firstRecords =
                (request.SelectedTypes.Contains("treasure", StringComparer.Ordinal)
                    ? new[] { treasure, supplies } : [])
                .Concat(request.SelectedTypes.Contains("city", StringComparer.Ordinal)
                    ? new[] { city } : []).ToArray();
            StagedSampleCount = firstRecords.Length;
            MapScanBlockCapture[] all = blocks.Select((block, index) =>
                new MapScanBlockCapture(request.ServerId, request.WorldId, block.BlockIndex,
                    "{}", index == 0 ? firstRecords : [])).ToArray();
            MapScanSharedBlockSourceProof[] proofs = blocks.Select(block =>
                new MapScanSharedBlockSourceProof(block.BlockIndex,
                    [
                        (block.MinY / 10) * 100 + block.MinX / 10,
                        (block.MinY / 10) * 100 + block.MinX / 10 + 1,
                        (block.MinY / 10 + 1) * 100 + block.MinX / 10,
                        (block.MinY / 10 + 1) * 100 + block.MinX / 10 + 1,
                    ], "controlled-native-packet", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()))
                .ToArray();
            if (WrongSourceAoi)
                proofs[0] = proofs[0] with { NativePointAoiIndices = [11, 12, 111, 112] };
            yield return new MapScanSharedAcquisitionBatch(WrongCaptureOwner
                ? AcquisitionOwner with { GroupGeneration = "obsolete-group" }
                : AcquisitionOwner, all, proofs);
        }

        public Task<MapScanAcquisitionCleanupReceipt> CleanupAsync(MapScanExecutionRequest request,
            MapScanAcquisitionExitReason reason, CancellationToken token)
        {
            Cleanups++;
            Require(token.CanBeCanceled && !token.IsCancellationRequested &&
                request.SelectedTypes.SequenceEqual(AcquisitionOwner.SelectedTypes),
                "native single final restore was not independently bounded or type-bound");
            // Controlled passive context changes after the native source
            // acknowledges home, before the engine can publish SQLite.
            AfterNativeHomeAcknowledged?.Invoke();
            return Task.FromResult(new MapScanAcquisitionCleanupReceipt(request, true, true,
                WrongCleanupOwner
                    ? AcquisitionOwner with { GroupGeneration = "obsolete-group" }
                    : AcquisitionOwner));
        }
    }

    // Opt-in retained-camera sources that still use the ordinary batch path
    // also need their final native restoration to survive Other partitioning.
    private sealed class NonSharedOwnedSource : IMapScanBlockSource, IMapScanAcquisitionCleanup
    {
        internal int Captures;
        internal int Cleanups;
        internal bool Restored = true;
        internal IReadOnlyList<string>? NativeSelected;

        public Task<MapScanBlockCapture> CaptureAsync(MapScanExecutionRequest request,
            MapScanTargetBlock block, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Captures++;
            NativeSelected = request.SelectedTypes.ToArray();
            Require(request.SelectedTypes.SequenceEqual(["treasure"]),
                "legacy source did not receive exact mapped native Other selection");
            MapStoredRecord source = new("treasure", request.ServerId, "real-supplies",
                10011, "real-supplies", null, null, null, null, null, null, null,
                1800000000000L,
                JsonSerializer.Serialize(new { pointType = 27,
                    runtimeClass = "Client.WorldSuppliesPoint", treasureType = 0,
                    suppliesType = 1, eventId = 0, x = 10, y = 10 }));
            return Task.FromResult(new MapScanBlockCapture(request.ServerId,
                request.WorldId, block.BlockIndex, "{}", [source]));
        }

        public Task<MapScanAcquisitionCleanupReceipt> CleanupAsync(
            MapScanExecutionRequest request, MapScanAcquisitionExitReason reason,
            CancellationToken token)
        {
            Cleanups++;
            Require(request.SelectedTypes.SequenceEqual(["treasure"]) &&
                token.CanBeCanceled && !token.IsCancellationRequested &&
                reason == MapScanAcquisitionExitReason.Completed,
                "ordinary opt-in cleanup did not preserve native kind, run or independent token");
            return Task.FromResult(new MapScanAcquisitionCleanupReceipt(
                request, Restored, true));
        }
    }

    private static async Task CheckLegacyCleanupProviderAsync(bool restored)
    {
        using Map317.MapStore store = Map317.MapStore.CreateInMemory();
        var source = new NonSharedOwnedSource { Restored = restored };
        var provider = new CurrentClientMap317ScanProvider(source,
            _ => Task.FromResult(new CurrentClientMapStatusContext(true, 2213, 2213,
                [], [], 0, 20, 20, 10, 10)),
            _ => Task.FromResult(new CurrentClientMapContext(2213, 0, 20, 20,
                10, 10, "ordinary-owned-session")));
        using var control = new Map317.MapControlPlane(store, provider);
        var terminal = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        provider.RunTerminated += id => terminal.TrySetResult(id);
        Map317.MapScanState accepted = await control.StartScanAsync(
            new Map317.MapScanStartRequest(["other"], "normal"));
        provider.ActivateAcceptedRun(control, accepted.ScanRunId);
        Require(await terminal.Task.WaitAsync(TimeSpan.FromSeconds(10)) == accepted.ScanRunId &&
                control.ScanState.Phase == (restored ? "completed" : "failed") &&
                source.Captures == 1 && source.Cleanups == 1 &&
                source.NativeSelected!.SequenceEqual(["treasure"]) &&
                store.SummaryCounts(2213)["other"] == (restored ? 1 : 0),
            "Map317 lost non-shared opt-in native cleanup or published without restoration");
    }

    private static async Task CheckAsync(string[] selected, string[] expectedNative,
        bool invalidCapture = false, bool invalidCleanup = false,
        bool invalidSourceAoi = false)
    {
        using Map317.MapStore store = Map317.MapStore.CreateInMemory();
        var source = new NativeSource
        {
            WrongCaptureOwner = invalidCapture,
            WrongCleanupOwner = invalidCleanup,
            WrongSourceAoi = invalidSourceAoi,
        };
        const int server = 2213;
        const string session = "map014-controlled-session";
        var provider = new CurrentClientMap317ScanProvider(source,
            _ => Task.FromResult(new CurrentClientMapStatusContext(true, server, server,
                [], [], 0, 1000, 1000, 500, 500)),
            _ => Task.FromResult(new CurrentClientMapContext(server, 0, 1000,
                1000, 500, 500, session)));
        using var control = new Map317.MapControlPlane(store, provider);
        var terminated = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        provider.RunTerminated += id => terminated.TrySetResult(id);
        Map317.MapScanState accepted = await control.StartScanAsync(
            new Map317.MapScanStartRequest(selected, "fast"));
        source.AcquisitionOwner = new MapScanSharedAcquisitionOwner(accepted.ScanRunId,
            server, 0, session, expectedNative, "controlled-profile", 12345, "group-native-1");
        provider.ActivateAcceptedRun(control, accepted.ScanRunId);
        Require(await terminated.Task.WaitAsync(TimeSpan.FromSeconds(25)) == accepted.ScanRunId,
            "real Map317 adapter failed to terminalize accepted native shared run");
        string phase = control.ScanState.Phase;
        Require(phase == (invalidCapture || invalidCleanup || invalidSourceAoi ? "failed" : "completed"),
            "invalid native shared receipt was published or valid run stopped: " + phase);
        Require(source.Calls == 1 && source.Cleanups == 1 &&
            source.CapturedNativeTypes is not null &&
            source.CapturedNativeTypes.SequenceEqual(expectedNative),
            "native source lost the single-run stream or one final owned cleanup");
        int treasureCount = store.SummaryCounts(server)["treasure"];
        int otherCount = store.SummaryCounts(server)["other"];
        int cityCount = store.SummaryCounts(server)["city"];
        if (invalidCapture || invalidCleanup || invalidSourceAoi)
            Require(treasureCount == 0 && otherCount == 0 && cityCount == 0,
                "invalid native owner leaked partial Map317 published records");
        else
        {
            Require(treasureCount == (selected.Contains("treasure") ? 1 : 0) &&
                otherCount == (selected.Contains("other") ? 1 : 0) &&
                cityCount == (selected.Contains("city") ? 1 : 0),
                "one native Treasure census was not split into exact user-selected output categories");
            if (selected.Contains("other"))
                Require(store.Search(new Map317.MapQuery("other", server, OtherSubtype: "supplies:1")).Total == 1,
                    "real Map317 Other subtype not available from shared native acquisition");
        }
    }

    private static async Task CheckRetiredSessionAtActivationAsync()
    {
        using Map317.MapStore store = Map317.MapStore.CreateInMemory();
        const int server = 2213;
        const string session = "original-owned-session";
        bool sessionReplaced = false;
        var source = new NativeSource();
        var provider = new CurrentClientMap317ScanProvider(source,
            _ => Task.FromResult(new CurrentClientMapStatusContext(true, server, server,
                [], [], 0, 1000, 1000, 500, 500)),
            _ => Task.FromResult(new CurrentClientMapContext(
                sessionReplaced ? 2214 : server, 0, 1000, 1000, 500, 500,
                sessionReplaced ? "replacement-session" : session)));
        using var control = new Map317.MapControlPlane(store, provider);
        var done = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        provider.RunTerminated += id => done.TrySetResult(id);
        Map317.MapScanState accepted = await control.StartScanAsync(
            new Map317.MapScanStartRequest(["treasure", "other"], "fast"));
        source.AcquisitionOwner = new(accepted.ScanRunId, server, 0, session,
            ["treasure"], "retired-profile", 12345, "owned-group");
        sessionReplaced = true;
        provider.ActivateAcceptedRun(control, accepted.ScanRunId);
        Require(await done.Task.WaitAsync(TimeSpan.FromSeconds(10)) == accepted.ScanRunId &&
                control.ScanState.Phase == "failed" &&
                source.Calls == 0 && source.Cleanups == 0 &&
                store.SummaryCounts(server)["other"] == 0,
            "retired owned session was allowed to begin native shared capture or publish after Map317 Start");
    }

    private static async Task CheckRetiredOwnerAfterNativeHomeAsync(bool serverChanged)
    {
        // Execute TWO real Map317 runs: an already published owner must remain
        // untouched if a later opt-in run loses live server/session ownership
        // after its single valid native home receipt but before publication.
        using Map317.MapStore store = Map317.MapStore.CreateInMemory();
        const int originalServer = 2213;
        const string originalSession = "post-home-owned-session";
        int liveServer = originalServer;
        string liveSession = originalSession;
        bool second = false;
        NativeSource? firstSource = null;
        NativeSource? secondSource = null;
        var provider = new CurrentClientMap317ScanProvider(
            new NonSharedOwnedSource(),
            _ => Task.FromResult(new CurrentClientMapStatusContext(
                true, liveServer, liveServer, [], [], 0, 1000, 1000, 500, 500)),
            _ => Task.FromResult(new CurrentClientMapContext(
                liveServer, 0, 1000, 1000, 500, 500, liveSession)),
            createSourceForAcceptedRun: (accepted, _) =>
            {
                var native = new NativeSource
                {
                    CityRecordKey = second ? "new-unpublished-owner" : "previous-published-owner",
                    AcquisitionOwner = new MapScanSharedAcquisitionOwner(
                        accepted.RunId, originalServer, 0, originalSession,
                        ["city"], "post-home-profile", 12345, "post-home-" + accepted.RunId),
                    AfterNativeHomeAcknowledged = second ? () =>
                    {
                        if (serverChanged) liveServer = 2214;
                        else liveSession = "manual-new-session";
                    } : null
                };
                if (second) secondSource = native;
                else firstSource = native;
                return native;
            });
        using var control = new Map317.MapControlPlane(store, provider);
        var terminated = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        provider.RunTerminated += id => terminated.TrySetResult(id);
        Map317.MapScanState acceptedFirst = await control.StartScanAsync(
            new Map317.MapScanStartRequest(["city"], "fast"));
        provider.ActivateAcceptedRun(control, acceptedFirst.ScanRunId);
        Require(await terminated.Task.WaitAsync(TimeSpan.FromSeconds(25)) == acceptedFirst.ScanRunId &&
                control.ScanState.Phase == "completed" &&
                firstSource?.Calls == 1 && firstSource.Cleanups == 1 &&
                store.SummaryCounts(originalServer)["city"] == 1,
            "first current-owner scan was not published through real Map317/SQLite");

        second = true;
        terminated = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        Map317.MapScanState acceptedSecond = await control.StartScanAsync(
            new Map317.MapScanStartRequest(["city"], "fast"));
        provider.ActivateAcceptedRun(control, acceptedSecond.ScanRunId);
        Require(await terminated.Task.WaitAsync(TimeSpan.FromSeconds(25)) == acceptedSecond.ScanRunId &&
                control.ScanState.Phase == "failed" &&
                secondSource?.Calls == 1 && secondSource.Cleanups == 1 &&
                store.SummaryCounts(originalServer)["city"] == 1 &&
                store.Search(new Map317.MapQuery("city", originalServer)).Total == 1,
            "retired post-home server/session published new City or lost previous SQLite rows");
    }

    private static async Task CheckAcceptedRunSourceFactoryAsync()
    {
        using Map317.MapStore store = Map317.MapStore.CreateInMemory();
        const int server = 2213;
        const string session = "factory-owned-session";
        int calls = 0;
        bool rejectFactory = true;
        bool rejectSharedOwnerProjection = false;
        NativeSource? admittedSource = null;
        var provider = new CurrentClientMap317ScanProvider(
            new NonSharedOwnedSource(),
            _ => Task.FromResult(new CurrentClientMapStatusContext(true, server, server,
                [], [], 0, 1000, 1000, 500, 500)),
            _ => Task.FromResult(new CurrentClientMapContext(server, 0,
                1000, 1000, 500, 500, session)),
            (accepted, context) =>
            {
                calls++;
                Require(accepted.ServerId == server && accepted.WorldId == 0 &&
                        context.LaunchSessionId == session &&
                        accepted.LaunchSessionId == session &&
                        accepted.SelectedTypes.SequenceEqual(["city", "other"]),
                    "accepted-run factory lacked the exact durable source/session selection");
                if (rejectFactory)
                    throw new InvalidDataException("No genuinely authenticated native group ingress");
                admittedSource = new NativeSource
                {
                    AcquisitionOwner = new MapScanSharedAcquisitionOwner(accepted.RunId,
                        server, 0, session, rejectSharedOwnerProjection
                            ? new[] { "city", "other" } : new[] { "city", "treasure" },
                        "factory-profile", 12345, "factory-group-" + accepted.RunId)
                };
                return admittedSource;
            });
        using var control = new Map317.MapControlPlane(store, provider);
        var terminal = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        provider.RunTerminated += id => terminal.TrySetResult(id);
        Map317.MapScanState rejected = await control.StartScanAsync(
            new Map317.MapScanStartRequest(["city", "other"], "fast"));
        provider.ActivateAcceptedRun(control, rejected.ScanRunId);
        Require(await terminal.Task.WaitAsync(TimeSpan.FromSeconds(5)) == rejected.ScanRunId &&
                control.ScanState.Phase == "failed" && calls == 1 &&
                admittedSource is null &&
                store.SummaryCounts(server)["city"] == 0 &&
                store.SummaryCounts(server)["other"] == 0,
            "missing accepted native ingress silently downgraded to guarded legacy scan");

        // A factory may return normally yet supply the wrong native owner
        // selection for Other->Treasure. The wrapper constructor then throws
        // before native Capture or engine Begin. It must retire only this
        // durable run, not strand Map317 in Reading and block the successor.
        rejectFactory = false;
        rejectSharedOwnerProjection = true;
        terminal = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        Map317.MapScanState badNativeOwner = await control.StartScanAsync(
            new Map317.MapScanStartRequest(["city", "other"], "fast"));
        provider.ActivateAcceptedRun(control, badNativeOwner.ScanRunId);
        Require(await terminal.Task.WaitAsync(TimeSpan.FromSeconds(5)) == badNativeOwner.ScanRunId &&
                control.ScanState.Phase == "failed" && calls == 2 &&
                admittedSource?.Calls == 0 && admittedSource.Cleanups == 0 &&
                store.SummaryCounts(server)["city"] == 0 &&
                store.SummaryCounts(server)["other"] == 0,
            "invalid native shared Other mapping stranded the accepted Map317 run or captured before admission");

        rejectSharedOwnerProjection = false;
        terminal = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        Map317.MapScanState accepted = await control.StartScanAsync(
            new Map317.MapScanStartRequest(["city", "other"], "fast"));
        provider.ActivateAcceptedRun(control, accepted.ScanRunId);
        Require(await terminal.Task.WaitAsync(TimeSpan.FromSeconds(20)) == accepted.ScanRunId &&
                control.ScanState.Phase == "completed" && calls == 3 &&
                admittedSource?.Calls == 1 && admittedSource.Cleanups == 1 &&
                store.SummaryCounts(server)["city"] == 1 &&
                store.SummaryCounts(server)["other"] == 1,
            "per-accepted-run factory failed to pin new owner/source and publish through Map317");
    }

    private static async Task CheckRetiredMap317SinkCannotMutateSuccessorAsync()
    {
        // Exercises the real Map317 control plane and in-memory SQLite with
        // an obsolete engine sink. Before the run-ID fence, a late Fail could
        // fail the independent successor, Publish could finish it, or Stage
        // could contaminate its active scan records.
        using Map317.MapStore store = Map317.MapStore.CreateInMemory();
        const int server = 2213;
        var provider = new CurrentClientMap317ScanProvider(
            new NonSharedOwnedSource(),
            _ => Task.FromResult(new CurrentClientMapStatusContext(
                true, server, server, [], [], 0, 1000, 1000, 500, 500)),
            _ => Task.FromResult(new CurrentClientMapContext(
                server, 0, 1000, 1000, 500, 500, "controlled-sink-session")));
        using var control = new Map317.MapControlPlane(store, provider);
        Map317.MapScanState obsolete = await control.StartScanAsync(
            new Map317.MapScanStartRequest(["city"], "fast"));
        await provider.StopMapScanAsync(); // Clears only the pending source lease.
        control.FailScan("retired old run");
        Map317.MapScanState successor = await control.StartScanAsync(
            new Map317.MapScanStartRequest(["city"], "fast"));
        Require(successor.ScanRunId != obsolete.ScanRunId && successor.IsReading,
            "controlled Map317 successor did not acquire an independent run");
        var retiredRequest = new MapScanExecutionRequest(
            obsolete.ScanRunId, server, 0, 1000, 1000, ["city"], 4,
            PlayerTileX: 500, PlayerTileY: 500, ScanMode: "fast",
            LaunchSessionId: "controlled-sink-session", LiveServerId: server);
        var sink = new Map317ScanRunSink(control);
        sink.Fail(retiredRequest, "obsolete native packet", 1000);
        Require(control.ScanState.IsReading && control.ScanState.ScanRunId == successor.ScanRunId,
            "retired engine failure overwrote current Map317 run");

        var block = MapScanTraversal.Build(1000, 1000)[0];
        var oldCity = new MapStoredRecord("city", server, "obsolete-city", 10011,
            null, "obsolete", null, 1, null, null, null, null, 1800000000000L,
            "{\"ownerUid\":\"obsolete-city\"}");
        bool rejectedStage = false;
        try
        {
            sink.CheckpointSuccess(retiredRequest, block,
                new MapScanBlockCapture(server, 0, 0, "{}", [oldCity]), 1, 1000);
        }
        catch (Map317.BridgeCommandException error) when (error.Code == "INVALID_SCAN")
        {
            rejectedStage = true;
        }
        Require(rejectedStage, "stale source staged into independent successor");
        bool rejectedPublish = false;
        try
        {
            sink.Publish(retiredRequest, 1000);
        }
        catch (Map317.BridgeCommandException error) when (error.Code == "INVALID_SCAN")
        {
            rejectedPublish = true;
        }
        Require(rejectedPublish, "stale source published independent successor");
        bool rejectedProgress = false;
        try
        {
            control.ReportProgressForRun(retiredRequest.RunId,
                new Map317.MapScanProgressUpdate(2500, 2500, 0, 0, 200,
                    true, 0, 0, Phase: "publishing"));
        }
        catch (Map317.BridgeCommandException error) when (error.Code == "INVALID_SCAN")
        {
            rejectedProgress = true;
        }
        Require(rejectedProgress, "stale native progress updated independent successor");
        Require(control.ScanState.IsReading && control.ScanState.ScanRunId == successor.ScanRunId &&
                control.ScanState.CompletedBlocks == 0 &&
                store.SummaryCounts(server)["city"] == 0,
            "retired native sink changed successor scan or persisted stale City");
        // The exact current run is still allowed to stage and advance its
        // *own* SQLite checkpoint, without publishing historical map rows.
        control.StageRecordForRun(successor.ScanRunId,
            new Map317.MapRecord("city", server, "owned-successor-city", 10011,
                null, "owned", null, 1, null, null, null, null,
                1800000000000L, "{\"ownerUid\":\"owned-successor-city\"}"));
        control.ReportProgressForRun(successor.ScanRunId,
            new Map317.MapScanProgressUpdate(1, 1, 0, 0, 1.0, true, 0, 0));
        Require(control.ScanState.CompletedBlocks == 1 &&
                store.ReadScanRun(successor.ScanRunId)?.CompletedBlocks == 1 &&
                store.SummaryCounts(server)["city"] == 0,
            "exact successor could not checkpoint to its own SQLite run");
        await provider.StopMapScanAsync();
        control.FailScanForRun(successor.ScanRunId, "controlled successor cleanup");
        Require(store.SummaryCounts(server)["city"] == 0 &&
                control.ScanState.Phase == "failed",
            "failed current successor leaked staged City into published SQLite rows");
    }

    internal static async Task RunAsync()
    {
        await CheckAsync(["other"], ["treasure"]);
        await CheckAsync(["treasure"], ["treasure"]);
        await CheckAsync(["treasure", "other"], ["treasure"]);
        await CheckAsync(["city", "other"], ["city", "treasure"]);
        await CheckAsync(["treasure", "other"], ["treasure"], invalidCapture: true);
        await CheckAsync(["treasure", "other"], ["treasure"], invalidCleanup: true);
        await CheckAsync(["city", "other"], ["city", "treasure"], invalidSourceAoi: true);
        await CheckLegacyCleanupProviderAsync(restored: true);
        await CheckLegacyCleanupProviderAsync(restored: false);
        await CheckRetiredSessionAtActivationAsync();
        await CheckRetiredOwnerAfterNativeHomeAsync(serverChanged: true);
        await CheckRetiredOwnerAfterNativeHomeAsync(serverChanged: false);
        await CheckAcceptedRunSourceFactoryAsync();
        await CheckRetiredMap317SinkCannotMutateSuccessorAsync();
        Console.WriteLine("MAP014_MAP317_SHARED_OTHER_SQLITE_OK native=false blocks=2500 oneHomeRestore=true types=city/treasure/other");
    }
}
