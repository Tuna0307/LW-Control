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
            MapStoredRecord city = new("city", request.ServerId, "native-owner-1", 10011,
                null, "current", null, 1, null, null, null, null, 1800000000000L,
                "{\"ownerUid\":\"native-owner-1\",\"x\":10,\"y\":10}");
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

    private static async Task CheckAcceptedRunSourceFactoryAsync()
    {
        using Map317.MapStore store = Map317.MapStore.CreateInMemory();
        const int server = 2213;
        const string session = "factory-owned-session";
        int calls = 0;
        bool rejectFactory = true;
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
                        server, 0, session, ["city", "treasure"],
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

        rejectFactory = false;
        terminal = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        Map317.MapScanState accepted = await control.StartScanAsync(
            new Map317.MapScanStartRequest(["city", "other"], "fast"));
        provider.ActivateAcceptedRun(control, accepted.ScanRunId);
        Require(await terminal.Task.WaitAsync(TimeSpan.FromSeconds(20)) == accepted.ScanRunId &&
                control.ScanState.Phase == "completed" && calls == 2 &&
                admittedSource?.Calls == 1 && admittedSource.Cleanups == 1 &&
                store.SummaryCounts(server)["city"] == 1 &&
                store.SummaryCounts(server)["other"] == 1,
            "per-accepted-run factory failed to pin new owner/source and publish through Map317");
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
        await CheckAcceptedRunSourceFactoryAsync();
        Console.WriteLine("MAP014_MAP317_SHARED_OTHER_SQLITE_OK native=false blocks=2500 oneHomeRestore=true types=city/treasure/other");
    }
}
