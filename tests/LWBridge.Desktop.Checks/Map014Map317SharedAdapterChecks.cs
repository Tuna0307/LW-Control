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

    private static async Task<string> JoinProviderTerminalAsync(
        CurrentClientMap317ScanProvider provider, Task<string> notification, TimeSpan timeout)
    {
        string runId = await notification.WaitAsync(timeout);
        // RunTerminated is issued inside the task's finally block, before its
        // admission fields are cleared. Observing the event is deliberately
        // not permission to start a successor or dispose its SQLite owner.
        // Join the actual provider task instead of racing that final cleanup.
        await provider.StopMapScanAsync().AsTask().WaitAsync(timeout);
        return runId;
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
        public TaskCompletionSource? CaptureEntered;
        public TaskCompletionSource? ReleaseCapture;

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
            CaptureEntered?.TrySetResult();
            if (ReleaseCapture is not null)
                await ReleaseCapture.Task.WaitAsync(cancellationToken);
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
        Require(await JoinProviderTerminalAsync(provider, terminal.Task, TimeSpan.FromSeconds(10)) == accepted.ScanRunId &&
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
        Require(await JoinProviderTerminalAsync(provider, terminated.Task, TimeSpan.FromSeconds(25)) == accepted.ScanRunId,
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
        Require(await JoinProviderTerminalAsync(provider, done.Task, TimeSpan.FromSeconds(10)) == accepted.ScanRunId &&
                control.ScanState.Phase == "failed" &&
                source.Calls == 0 && source.Cleanups == 0 &&
                store.SummaryCounts(server)["other"] == 0,
            "retired owned session was allowed to begin native shared capture or publish after Map317 Start");
    }

    private static async Task CheckPreBeginNativeOwnerDriftRetiresExactRunAsync()
    {
        // The provider preflight can pass the live game owner, yet the source
        // may retire its advertised native group before the engine pins it.
        // ValidateSharedOwner then throws BEFORE MapScanEngine.sink.Begin and
        // its capture try/catch. The provider still owns an accepted SQLite
        // 'running' run: it must terminalize that exact run, preserve the
        // previous publication, and admit an independent valid successor.
        using Map317.MapStore store = Map317.MapStore.CreateInMemory();
        const int server = 2213;
        const string session = "pre-begin-owned-session";
        store.UpsertRecord(new Map317.MapRecord("city", server,
            "preserved-before-owner-drift", 10011, null, "original", null,
            1, null, null, null, null, 1700000000000L,
            "{\"ownerUid\":\"preserved-before-owner-drift\"}"));
        NativeSource? activeNative = null;
        NativeSource? badNative = null;
        int contextReads = 0;
        var provider = new CurrentClientMap317ScanProvider(
            new NonSharedOwnedSource(),
            _ => Task.FromResult(new CurrentClientMapStatusContext(
                true, server, server, [], [], 0, 1000, 1000, 500, 500)),
            _ =>
            {
                // First read is native-source admission; the second is the
                // passive exact owner check before running the real engine.
                // Only the advertised source group RunId changes, not the
                // client/server context, making this distinct from the
                // already-qualified retired-session-at-activation inverse.
                if (Interlocked.Increment(ref contextReads) == 2)
                    activeNative!.AcquisitionOwner = activeNative.AcquisitionOwner with
                    { RunId = "obsolete-native-group-run" };
                return Task.FromResult(new CurrentClientMapContext(
                    server, 0, 1000, 1000, 500, 500, session));
            },
            createSourceForAcceptedRun: (run, _) =>
            {
                activeNative = new NativeSource
                {
                    CityRecordKey = "source-after-owner-drift",
                    AcquisitionOwner = new MapScanSharedAcquisitionOwner(
                        run.RunId, server, 0, session, ["city"],
                        "pre-begin-profile", 12345, "group-" + run.RunId)
                };
                badNative ??= activeNative;
                return activeNative;
            });
        using var control = new Map317.MapControlPlane(store, provider);
        var terminated = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        provider.RunTerminated += id => terminated.TrySetResult(id);

        Map317.MapScanState bad = await control.StartScanAsync(
            new Map317.MapScanStartRequest(["city"], "fast"));
        provider.ActivateAcceptedRun(control, bad.ScanRunId);
        Require(await JoinProviderTerminalAsync(provider, terminated.Task, TimeSpan.FromSeconds(10)) == bad.ScanRunId &&
                control.ScanState.Phase == "failed" &&
                store.ReadScanRun(bad.ScanRunId)?.Status == "failed" &&
                badNative?.Calls == 0 && badNative?.Cleanups == 0 &&
                store.SummaryCounts(server)["city"] == 1 &&
                store.Search(new Map317.MapQuery("city", server)).Rows[0]
                    .GetRawText().Contains("preserved-before-owner-drift", StringComparison.Ordinal),
            "pre-Begin retired native group stranded accepted run or damaged saved City");

        terminated = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        Map317.MapScanState next = await control.StartScanAsync(
            new Map317.MapScanStartRequest(["city"], "fast"));
        provider.ActivateAcceptedRun(control, next.ScanRunId);
        Require(await JoinProviderTerminalAsync(provider, terminated.Task, TimeSpan.FromSeconds(25)) == next.ScanRunId &&
                control.ScanState.Phase == "completed" &&
                activeNative?.Calls == 1 && activeNative?.Cleanups == 1 &&
                store.SummaryCounts(server)["city"] == 1,
            "valid successor could not publish after pre-Begin native owner rejection");
    }

    private static async Task CheckProviderCancellationWithoutMap317StopRetiresExactRunAsync()
    {
        // Provider shutdown/cancellation can occur without the user or Auto
        // having called Map317.StopScanAsync. Engine cancellation does exactly
        // one native cleanup and calls sink.Stop, which intentionally does not
        // recurse into the provider. Without exact-run fallback terminalization
        // the accepted SQLite run remained 'running' and Map317 Reading.
        using Map317.MapStore store = Map317.MapStore.CreateInMemory();
        const int server = 2213;
        const string session = "provider-cancel-session";
        store.UpsertRecord(new Map317.MapRecord("city", server,
            "before-provider-cancel", 10011, null, "previous", null,
            1, null, null, null, null, 1700000000000L,
            "{\"ownerUid\":\"before-provider-cancel\"}"));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var neverRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        NativeSource? blockedSource = null;
        NativeSource? successorSource = null;
        int acceptedSources = 0;
        var provider = new CurrentClientMap317ScanProvider(
            new NonSharedOwnedSource(),
            _ => Task.FromResult(new CurrentClientMapStatusContext(
                true, server, server, [], [], 0, 1000, 1000, 500, 500)),
            _ => Task.FromResult(new CurrentClientMapContext(
                server, 0, 1000, 1000, 500, 500, session)),
            createSourceForAcceptedRun: (request, _) =>
            {
                NativeSource source = new()
                {
                    CityRecordKey = acceptedSources == 0
                        ? "cancelled-unpublished-city" : "completed-successor-city",
                    AcquisitionOwner = new MapScanSharedAcquisitionOwner(
                        request.RunId, server, 0, session, ["city"],
                        "provider-cancel-profile", 12345, "group-" + request.RunId),
                    CaptureEntered = acceptedSources == 0 ? entered : null,
                    ReleaseCapture = acceptedSources == 0 ? neverRelease : null,
                };
                if (acceptedSources++ == 0) blockedSource = source;
                else successorSource = source;
                return source;
            });
        using var control = new Map317.MapControlPlane(store, provider);
        var terminated = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        provider.RunTerminated += id => terminated.TrySetResult(id);

        Map317.MapScanState first = await control.StartScanAsync(
            new Map317.MapScanStartRequest(["city"], "fast"));
        provider.ActivateAcceptedRun(control, first.ScanRunId);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await provider.StopMapScanAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        Require(await JoinProviderTerminalAsync(provider, terminated.Task, TimeSpan.FromSeconds(10)) == first.ScanRunId &&
                control.ScanState.Phase == "failed" && !control.ScanState.IsReading &&
                store.ReadScanRun(first.ScanRunId)?.Status == "failed" &&
                blockedSource?.Calls == 1 && blockedSource?.Cleanups == 1 &&
                store.SummaryCounts(server)["city"] == 1 &&
                store.Search(new Map317.MapQuery("city", server)).Rows[0]
                    .GetRawText().Contains("before-provider-cancel", StringComparison.Ordinal),
            "unowned provider cancellation left a running scan or changed published City");

        terminated = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        Map317.MapScanState second = await control.StartScanAsync(
            new Map317.MapScanStartRequest(["city"], "fast"));
        provider.ActivateAcceptedRun(control, second.ScanRunId);
        Require(await JoinProviderTerminalAsync(provider, terminated.Task, TimeSpan.FromSeconds(25)) == second.ScanRunId &&
                control.ScanState.Phase == "completed" &&
                successorSource?.Calls == 1 && successorSource?.Cleanups == 1 &&
                store.SummaryCounts(server)["city"] == 1 &&
                store.Search(new Map317.MapQuery("city", server)).Rows[0]
                    .GetRawText().Contains("completed-successor-city", StringComparison.Ordinal),
            "provider cancellation contaminated an independently accepted successor");
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
        Require(await JoinProviderTerminalAsync(provider, terminated.Task, TimeSpan.FromSeconds(25)) == acceptedFirst.ScanRunId &&
                control.ScanState.Phase == "completed" &&
                firstSource?.Calls == 1 && firstSource.Cleanups == 1 &&
                store.SummaryCounts(originalServer)["city"] == 1,
            "first current-owner scan was not published through real Map317/SQLite");

        second = true;
        terminated = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        Map317.MapScanState acceptedSecond = await control.StartScanAsync(
            new Map317.MapScanStartRequest(["city"], "fast"));
        provider.ActivateAcceptedRun(control, acceptedSecond.ScanRunId);
        Require(await JoinProviderTerminalAsync(provider, terminated.Task, TimeSpan.FromSeconds(25)) == acceptedSecond.ScanRunId &&
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
        Require(await JoinProviderTerminalAsync(provider, terminal.Task, TimeSpan.FromSeconds(5)) == rejected.ScanRunId &&
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
        Require(await JoinProviderTerminalAsync(provider, terminal.Task, TimeSpan.FromSeconds(5)) == badNativeOwner.ScanRunId &&
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
        Require(await JoinProviderTerminalAsync(provider, terminal.Task, TimeSpan.FromSeconds(20)) == accepted.ScanRunId &&
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

        bool rejectedBegin = false;
        try { sink.Begin(retiredRequest, successor.TotalBlocks, 1000); }
        catch (InvalidDataException) { rejectedBegin = true; }
        Require(rejectedBegin,
            "retired source acquired a new logical-block ledger for successor run");

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
        catch (InvalidDataException)
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

    private static async Task CheckMap317BatchStagingGuardsAsync()
    {
        // Exercise the production IMapScanBatchRunSink -> MapControlPlane ->
        // MapStore path with a two-block world. A batch must stage all records
        // under the exact accepted run before either block is certified, and
        // rejected/cancelled/stale batches must leave the last publication intact.
        const int server = 2213;
        const int foreignServer = 2214;
        const long updatedAt = 1800000000000L;
        IReadOnlyList<MapScanTargetBlock> blocks = MapScanTraversal.Build(40, 20);
        Require(blocks.Count == 2,
            "controlled batch staging geometry did not produce exactly two logical blocks");

        CurrentClientMap317ScanProvider NewProvider() => new(
            new NonSharedOwnedSource(),
            _ => Task.FromResult(new CurrentClientMapStatusContext(
                true, server, server, [], [], 0, 40, 20, 20, 10)),
            _ => Task.FromResult(new CurrentClientMapContext(
                server, 0, 40, 20, 20, 10, "batch-staging-session")));

        static MapStoredRecord Record(string kind, int recordServer, string key, int pointIndex) =>
            new(kind, recordServer, key, pointIndex, null, key, null, 1, null, null,
                null, null, updatedAt, JsonSerializer.Serialize(new { ownerUid = key }));

        static Map317.MapRecord PublishedRecord(string key) => new(
            "city", server, key, 1, null, key, null, 1, null, null, null, null,
            1700000000000L, JsonSerializer.Serialize(new { ownerUid = key }));

        MapScanExecutionRequest Request(string runId) => new(
            runId, server, 0, 40, 20, ["city"], 2,
            PlayerTileX: 20, PlayerTileY: 10, ScanMode: "fast",
            LaunchSessionId: "batch-staging-session", LiveServerId: server);

        MapScanBlockSuccess Success(MapScanTargetBlock block, params MapStoredRecord[] records) =>
            new(block, new MapScanBlockCapture(
                server, 0, block.BlockIndex, "{}", records));

        MapScanBlockSuccess[] ValidBatch() =>
        [
            Success(blocks[0], Record("city", server, "batch-city-a", 1)),
            Success(blocks[1], Record("city", server, "batch-city-b", 21)),
        ];

        // A valid multi-block batch is visible only through exact-run staging
        // until completion, then replaces the prior publication atomically.
        {
            using Map317.MapStore store = Map317.MapStore.CreateInMemory();
            store.UpsertRecord(PublishedRecord("saved-batch-owner"));
            var provider = NewProvider();
            using var control = new Map317.MapControlPlane(store, provider);
            Map317.MapScanState run = await control.StartScanAsync(
                new Map317.MapScanStartRequest(["city"], "fast"));
            var request = Request(run.ScanRunId);
            var sink = new Map317ScanRunSink(control);
            sink.Begin(request, run.TotalBlocks, updatedAt);
            sink.CheckpointSuccessBatch(request, ValidBatch(), 1, updatedAt);

            Map317.MapSearchResult staged = store.Search(
                new Map317.MapQuery("city", server, ScanRunId: run.ScanRunId));
            Map317.MapSearchResult published = store.Search(new Map317.MapQuery("city", server));
            Require(staged.Total == 2 && published.Total == 1 &&
                    published.Rows[0].GetRawText().Contains("saved-batch-owner", StringComparison.Ordinal),
                "multi-block exact-run batch leaked before publication or failed to stage both records");

            control.ReportProgressForRun(run.ScanRunId,
                new Map317.MapScanProgressUpdate(run.TotalBlocks, run.TotalBlocks,
                    0, 0, 1.0, true, 0, 0));
            sink.Publish(request, updatedAt + 1);
            published = store.Search(new Map317.MapQuery("city", server));
            Require(store.ReadScanRun(run.ScanRunId)?.Status == "completed" &&
                    published.Total == 2 &&
                    published.Rows.All(row => !row.GetRawText().Contains(
                        "saved-batch-owner", StringComparison.Ordinal)),
                "complete exact-run batch did not atomically replace the last good publication");
            await provider.StopMapScanAsync();
        }

        // Foreign kind/server records and duplicate logical blocks are rejected
        // before any part of the batch is staged or its block ledger advances.
        foreach (string invalidCase in new[] { "kind", "server", "duplicate" })
        {
            using Map317.MapStore store = Map317.MapStore.CreateInMemory();
            store.UpsertRecord(PublishedRecord("saved-before-" + invalidCase));
            var provider = NewProvider();
            using var control = new Map317.MapControlPlane(store, provider);
            Map317.MapScanState run = await control.StartScanAsync(
                new Map317.MapScanStartRequest(["city"], "fast"));
            var request = Request(run.ScanRunId);
            var sink = new Map317ScanRunSink(control);
            sink.Begin(request, run.TotalBlocks, updatedAt);

            MapScanBlockSuccess[] invalid = invalidCase switch
            {
                "kind" =>
                [
                    Success(blocks[0], Record("city", server, "valid-before-kind", 1)),
                    Success(blocks[1], Record("resource", server, "foreign-kind", 21)),
                ],
                "server" =>
                [
                    Success(blocks[0], Record("city", server, "valid-before-server", 1)),
                    Success(blocks[1], Record("city", foreignServer, "foreign-server", 21)),
                ],
                _ =>
                [
                    Success(blocks[0], Record("city", server, "duplicate-a", 1)),
                    Success(blocks[0], Record("city", server, "duplicate-b", 2)),
                ],
            };
            bool rejected = false;
            try
            {
                sink.CheckpointSuccessBatch(request, invalid, 1, updatedAt);
            }
            catch (Map317.BridgeCommandException error) when (
                invalidCase is "kind" or "server" && error.Code == "INVALID_MAP_RECORD")
            {
                rejected = true;
            }
            catch (InvalidDataException) when (invalidCase == "duplicate")
            {
                rejected = true;
            }

            Map317.MapSearchResult staged = store.Search(
                new Map317.MapQuery("city", server, ScanRunId: run.ScanRunId));
            Map317.MapSearchResult published = store.Search(new Map317.MapQuery("city", server));
            Require(rejected && staged.Total == 0 && published.Total == 1 &&
                    published.Rows[0].GetRawText().Contains(
                        "saved-before-" + invalidCase, StringComparison.Ordinal),
                "rejected " + invalidCase + " batch partially staged or changed last-good rows");

            // Retrying the same two logical blocks proves a rejected batch did
            // not consume either ledger entry before its SQLite commit.
            sink.CheckpointSuccessBatch(request, ValidBatch(), 1, updatedAt + 1);
            Require(store.Search(new Map317.MapQuery(
                        "city", server, ScanRunId: run.ScanRunId)).Total == 2,
                "rejected " + invalidCase + " batch advanced the block ledger before commit");
            control.FailScanForRun(run.ScanRunId, "controlled rejected-batch cleanup");
            published = store.Search(new Map317.MapQuery("city", server));
            Require(store.ReadScanRun(run.ScanRunId)?.Status == "failed" &&
                    published.Total == 1 && published.Rows[0].GetRawText().Contains(
                        "saved-before-" + invalidCase, StringComparison.Ordinal),
                "failed batch retry replaced the last good publication");
            await provider.StopMapScanAsync();
        }

        // Cancellation clears staging and retires exact ownership. The same
        // old batch must remain rejected both while cancelled and after a new
        // independent run has been admitted.
        {
            using Map317.MapStore store = Map317.MapStore.CreateInMemory();
            store.UpsertRecord(PublishedRecord("saved-before-cancel"));
            var provider = NewProvider();
            using var control = new Map317.MapControlPlane(store, provider);
            Map317.MapScanState cancelled = await control.StartScanAsync(
                new Map317.MapScanStartRequest(["city"], "fast"));
            var oldRequest = Request(cancelled.ScanRunId);
            var oldSink = new Map317ScanRunSink(control);
            oldSink.Begin(oldRequest, cancelled.TotalBlocks, updatedAt);
            await control.StopScanAsync();

            bool cancelledRejected = false;
            try
            {
                oldSink.CheckpointSuccessBatch(oldRequest, ValidBatch(), 1, updatedAt + 1);
            }
            catch (Map317.BridgeCommandException error) when (error.Code == "INVALID_SCAN")
            {
                cancelledRejected = true;
            }
            Require(cancelledRejected &&
                    store.ReadScanRun(cancelled.ScanRunId)?.Status == "cancelled" &&
                    store.Search(new Map317.MapQuery(
                        "city", server, ScanRunId: cancelled.ScanRunId)).Total == 0 &&
                    store.Search(new Map317.MapQuery("city", server)).Rows[0]
                        .GetRawText().Contains("saved-before-cancel", StringComparison.Ordinal),
                "cancelled exact run accepted late batch staging or changed last-good rows");

            Map317.MapScanState successor = await control.StartScanAsync(
                new Map317.MapScanStartRequest(["city"], "fast"));
            bool staleRejected = false;
            try
            {
                oldSink.CheckpointSuccessBatch(oldRequest, ValidBatch(), 1, updatedAt + 2);
            }
            catch (Map317.BridgeCommandException error) when (error.Code == "INVALID_SCAN")
            {
                staleRejected = true;
            }
            Require(staleRejected && control.ScanState.ScanRunId == successor.ScanRunId &&
                    store.Search(new Map317.MapQuery(
                        "city", server, ScanRunId: successor.ScanRunId)).Total == 0 &&
                    store.Search(new Map317.MapQuery("city", server)).Rows[0]
                        .GetRawText().Contains("saved-before-cancel", StringComparison.Ordinal),
                "stale cancelled batch contaminated an independently accepted successor");
            await control.StopScanAsync();
        }
    }

    private static async Task CheckMap317EarlyOwnedPublishCannotReplaceSavedRowsAsync()
    {
        // Direct actual provider -> Map317 -> SQLite inverse. A still-owned
        // native source can stage positive City data and invoke its sink's
        // Publish before all 2,500 logical blocks are complete. RunId is valid
        // but ownership is NOT completion; never replace previous published
        // rows just because CompleteScanForRun can fill in total progress.
        foreach (int reportedBlocks in new[] { 0, 1, 2500 })
        {
            using Map317.MapStore store = Map317.MapStore.CreateInMemory();
            const int server = 2213;
            store.UpsertRecord(new Map317.MapRecord("city", server,
                "saved-previous-owner", 10011, null, "saved", null, 1,
                null, null, null, null, 1700000000000L,
                "{\"ownerUid\":\"saved-previous-owner\"}"));
            var provider = new CurrentClientMap317ScanProvider(
                new NonSharedOwnedSource(),
                _ => Task.FromResult(new CurrentClientMapStatusContext(
                    true, server, server, [], [], 0, 1000, 1000, 500, 500)),
                _ => Task.FromResult(new CurrentClientMapContext(
                    server, 0, 1000, 1000, 500, 500, "incomplete-publish-session")));
            using var control = new Map317.MapControlPlane(store, provider);
            Map317.MapScanState run = await control.StartScanAsync(
                new Map317.MapScanStartRequest(["city"], "fast"));
            var request = new MapScanExecutionRequest(run.ScanRunId, server, 0,
                1000, 1000, ["city"], 4, PlayerTileX: 500, PlayerTileY: 500,
                ScanMode: "fast", LaunchSessionId: "incomplete-publish-session",
                LiveServerId: server);
            var sink = new Map317ScanRunSink(control);
            bool rejectedServer = false, rejectedSelection = false;
            try { sink.Begin(request with { ServerId = 2214 }, run.TotalBlocks, 1800000000000L); }
            catch (InvalidDataException) { rejectedServer = true; }
            try { sink.Begin(request with { SelectedTypes = ["resource"] }, run.TotalBlocks, 1800000000000L); }
            catch (InvalidDataException) { rejectedSelection = true; }
            Require(rejectedServer && rejectedSelection && control.ScanState.IsReading,
                "foreign server or kind list created a native checkpoint ledger");
            sink.Begin(request, run.TotalBlocks, 1800000000000L);
            var block = MapScanTraversal.Build(1000, 1000)[0];
            sink.CheckpointSuccess(request, block,
                new MapScanBlockCapture(server, 0, 0, "{}", [
                    new MapStoredRecord("city", server, "incomplete-new-owner",
                        10012, null, "incomplete", null, 1, null, null,
                        null, null, 1800000000000L,
                        "{\"ownerUid\":\"incomplete-new-owner\"}")
                ]), 1, 1800000000000L);
            bool duplicateRejected = false;
            try
            {
                sink.CheckpointSuccess(request, block,
                    new MapScanBlockCapture(server, 0, 0, "{}", []),
                    1, 1800000000000L);
            }
            catch (InvalidDataException)
            {
                duplicateRejected = true;
            }
            Require(duplicateRejected,
                "duplicate same-run native block checkpoint was counted twice");
            if (reportedBlocks > 0)
                control.ReportProgressForRun(run.ScanRunId,
                    new Map317.MapScanProgressUpdate(reportedBlocks,
                        reportedBlocks, 0, 0, 1.0, true, 0, 0));
            bool rejected = false;
            try { sink.Publish(request, 1800000000001L); }
            catch (Map317.BridgeCommandException e) when (e.Code == "INCOMPLETE_SCAN")
            {
                rejected = true;
            }
            Require(rejected && control.ScanState.IsReading &&
                    control.ScanState.ScanRunId == run.ScanRunId &&
                    store.ReadScanRun(run.ScanRunId)?.Status == "running" &&
                    store.ReadScanRun(run.ScanRunId)?.CompletedBlocks ==
                        reportedBlocks &&
                    store.SummaryCounts(server)["city"] == 1 &&
                    store.Search(new Map317.MapQuery("city", server)).Rows[0]
                        .GetRawText().Contains("saved-previous-owner", StringComparison.Ordinal),
                "premature owned native Publish replaced last good City rows");
            control.FailScanForRun(run.ScanRunId, "controlled incomplete native run");
            Require(store.SummaryCounts(server)["city"] == 1 &&
                    control.ScanState.Phase == "failed",
                "failed incomplete native source did not preserve saved City rows");
        }
    }

    private static async Task CheckRealMap317CancelAndPublishExclusionAsync()
    {
        // Exercise concurrent *real* Map317 Stop/SQLite publication, not a
        // validator surrogate. Either the cancellation transaction wins and
        // must preserve preexisting rows, or the exact owner-atomic publish
        // wins and its new City must be present. A published SQLite run can
        // never silently lose its rows to late cancellation, and a cancelled
        // run must never publish staged rows.
        for (int iteration = 0; iteration < 16; iteration++)
        {
            using Map317.MapStore store = Map317.MapStore.CreateInMemory();
            const int server = 2213;
            const string retainedKey = "previous-owner";
            store.UpsertRecord(new Map317.MapRecord("city", server, retainedKey,
                10011, null, "previous", null, 1, null, null, null, null,
                1700000000000L, "{\"ownerUid\":\"previous-owner\"}"));
            var provider = new CurrentClientMap317ScanProvider(
                new NonSharedOwnedSource(),
                _ => Task.FromResult(new CurrentClientMapStatusContext(
                    true, server, server, [], [], 0, 1000, 1000, 500, 500)),
                _ => Task.FromResult(new CurrentClientMapContext(
                    server, 0, 1000, 1000, 500, 500, "owned-race-session")));
            using var control = new Map317.MapControlPlane(store, provider);
            Map317.MapScanState accepted = await control.StartScanAsync(
                new Map317.MapScanStartRequest(["city"], "fast"));
            control.StageRecordForRun(accepted.ScanRunId,
                new Map317.MapRecord("city", server, "current-owner", 10022,
                    null, "new", null, 1, null, null, null, null,
                    1800000000000L, "{\"ownerUid\":\"current-owner\"}"));
            control.ReportProgressForRun(accepted.ScanRunId,
                new Map317.MapScanProgressUpdate(accepted.TotalBlocks,
                    accepted.TotalBlocks, 0, 0, 1.0, true, 0, 0));

            using var start = new ManualResetEventSlim(false);
            Task stop = Task.Run(async () =>
            {
                start.Wait();
                await control.StopScanAsync();
            });
            Task publish = Task.Run(() =>
            {
                start.Wait();
                try { control.CompleteScanForRun(accepted.ScanRunId); }
                catch (Map317.BridgeCommandException error) when (
                    error.Code is "INVALID_SCAN" or "INCOMPLETE_SCAN")
                {
                    // Stop won before native publication. It must not publish.
                }
            });
            start.Set();
            await Task.WhenAll(stop, publish).WaitAsync(TimeSpan.FromSeconds(10));
            Map317.MapScanRun result = store.ReadScanRun(accepted.ScanRunId)
                ?? throw new InvalidOperationException("MAP014_MAP317_SHARED: race run disappeared");
            int count = store.SummaryCounts(server)["city"];
            if (result.Status == "completed")
                Require(count == 1 &&
                    store.Search(new Map317.MapQuery("city", server)).Rows[0]
                        .GetRawText().Contains("current-owner", StringComparison.Ordinal),
                    "Stop erased a completed native publication or kept old City");
            else if (result.Status == "cancelled")
                Require(count == 1 &&
                    store.Search(new Map317.MapQuery("city", server)).Rows[0]
                        .GetRawText().Contains(retainedKey, StringComparison.Ordinal),
                    "canceled native run published staged City or replaced previous SQLite row");
            else
                throw new InvalidOperationException(
                    "MAP014_MAP317_SHARED: concurrent Stop/Publish left run " + result.Status);
        }
    }

    private static async Task CheckMap317AcceptedStartCannotOutliveStopAsync()
    {
        // Scan.StartAsync announces the accepted owner before the separate
        // MapControlPlane SQLite InsertScanRun. A concurrent Stop triggered
        // from that exact acceptance must not finish before a late INSERT
        // leaves a false active 'running' row. This uses real Map317 Start,
        // Stop and SQLite, not a synthetic transport receipt.
        for (int iteration = 0; iteration < 16; iteration++)
        {
            using Map317.MapStore store = Map317.MapStore.CreateInMemory();
            var provider = new CurrentClientMap317ScanProvider(
                new NonSharedOwnedSource(),
                _ => Task.FromResult(new CurrentClientMapStatusContext(
                    true, 2213, 2213, [], [], 0, 1000, 1000, 500, 500)),
                _ => Task.FromResult(new CurrentClientMapContext(
                    2213, 0, 1000, 1000, 500, 500, "start-stop-session")));
            using var control = new Map317.MapControlPlane(store, provider);
            Task? stop = null;
            string? acceptedRunId = null;
            control.ScanStateChanged += (_, args) =>
            {
                if (args.State.IsReading && acceptedRunId is null)
                {
                    acceptedRunId = args.State.ScanRunId;
                    stop = Task.Run(async () => await control.StopScanAsync());
                }
            };
            try
            {
                await control.StartScanAsync(new Map317.MapScanStartRequest(["city"], "fast"));
            }
            catch (Map317.BridgeCommandException error) when (error.Code == "INVALID_SCAN")
            {
                // Stop beat exact-run persistence. Start correctly refused
                // to return a durable active run and retired only its owner.
            }
            Require(!string.IsNullOrWhiteSpace(acceptedRunId) && stop is not null,
                "no accepted run or owned Stop was observed");
            await stop!.WaitAsync(TimeSpan.FromSeconds(10));
            Map317.MapScanRun? durable = store.ReadScanRun(acceptedRunId!);
            Require(!control.ScanState.IsReading &&
                    (durable is null || durable.Status == "cancelled") &&
                    store.SummaryCounts(2213)["city"] == 0,
                "Stop raced durable Start and left orphan running SQLite scan or published City");
        }
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
        await CheckPreBeginNativeOwnerDriftRetiresExactRunAsync();
        await CheckProviderCancellationWithoutMap317StopRetiresExactRunAsync();
        await CheckRetiredOwnerAfterNativeHomeAsync(serverChanged: true);
        await CheckRetiredOwnerAfterNativeHomeAsync(serverChanged: false);
        await CheckAcceptedRunSourceFactoryAsync();
        await CheckRetiredMap317SinkCannotMutateSuccessorAsync();
        await CheckMap317BatchStagingGuardsAsync();
        await CheckMap317EarlyOwnedPublishCannotReplaceSavedRowsAsync();
        await CheckRealMap317CancelAndPublishExclusionAsync();
        await CheckMap317AcceptedStartCannotOutliveStopAsync();
        Console.WriteLine("MAP014_MAP317_SHARED_OTHER_SQLITE_OK native=false blocks=2500 oneHomeRestore=true types=city/treasure/other");
    }
}
