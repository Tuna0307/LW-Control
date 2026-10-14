using LWBridge.Desktop;
using Microsoft.Data.Sqlite;

namespace LWBridge.Desktop.Checks;

/// <summary>
/// Controlled actual-engine tests: no game, native scripts, real source or
/// production database. All camera receipts are deliberately simulated.
/// </summary>
internal static class Map014ConsecutiveEngineCleanupChecks
{
    private static readonly MapScanExecutionRequest Request =
        new("map014-owned-run", 2213, 0, 20, 20, ["city"], 1,
            MaxAttemptsPerBlock: 1, ScanMode: "fast", LaunchSessionId: "owned-native-session");

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("MAP014_ENGINE_CLEANUP: " + message);
    }

    private static async Task<TException> Throws<TException>(Func<Task> action, string why)
        where TException : Exception
    {
        try { await action().ConfigureAwait(false); }
        catch (TException error) { return error; }
        throw new InvalidOperationException("MAP014_ENGINE_CLEANUP expected " + typeof(TException).Name + ": " + why);
    }

    private static MapScanBlockCapture Capture(MapScanExecutionRequest request, MapScanTargetBlock block) =>
        new(request.ServerId, request.WorldId, block.BlockIndex, "{}", []);

    private static MapScanAcquisitionCleanupReceipt Home(MapScanExecutionRequest request) =>
        new(request, OriginalCameraRestored: true, FreshHomeViewReplyMatched: true);

    private sealed class Source : IMapScanBlockSource, IMapScanAcquisitionCleanup
    {
        internal Func<MapScanExecutionRequest, MapScanTargetBlock, CancellationToken, Task<MapScanBlockCapture>> CaptureAction =
            (request, block, _) => Task.FromResult(Capture(request, block));
        internal Func<MapScanExecutionRequest, MapScanAcquisitionExitReason, CancellationToken,
            Task<MapScanAcquisitionCleanupReceipt>> CleanupAction =
            (request, _, _) => Task.FromResult(Home(request));
        internal int CleanupCount;
        public Task<MapScanBlockCapture> CaptureAsync(
            MapScanExecutionRequest request, MapScanTargetBlock block, CancellationToken token) =>
            CaptureAction(request, block, token);

        public Task<MapScanAcquisitionCleanupReceipt> CleanupAsync(
            MapScanExecutionRequest request, MapScanAcquisitionExitReason reason, CancellationToken token)
        {
            Interlocked.Increment(ref CleanupCount);
            return CleanupAction(request, reason, token);
        }
    }

    private sealed class LegacySource : IMapScanBlockSource
    {
        public Task<MapScanBlockCapture> CaptureAsync(
            MapScanExecutionRequest request, MapScanTargetBlock block, CancellationToken _) =>
            Task.FromResult(Capture(request, block));
    }

    private sealed class Sink : IMapScanRunSink
    {
        internal readonly List<string> Events = [];
        internal Action? OnStage;
        internal int Published;
        internal int Staged;
        internal string? TerminalError;
        public void Begin(MapScanExecutionRequest request, int totalBlocks, long _) =>
            Events.Add("begin:" + request.RunId + ":" + totalBlocks);
        public void CheckpointSuccess(
            MapScanExecutionRequest request, MapScanTargetBlock block,
            MapScanBlockCapture capture, int attempts, long _)
        {
            Events.Add("stage:" + request.RunId);
            Staged++;
            OnStage?.Invoke();
        }
        public void CheckpointFailure(
            MapScanExecutionRequest request, MapScanTargetBlock block,
            int attempts, string error, long _) => Events.Add("failed-block:" + request.RunId);
        public void Publish(MapScanExecutionRequest request, long _)
        {
            Events.Add("publish:" + request.RunId);
            Published++;
        }
        public void Fail(MapScanExecutionRequest request, string error, long _)
        {
            TerminalError = error;
            Events.Add("fail:" + request.RunId);
        }
        public void Stop(MapScanExecutionRequest request, long _) =>
            Events.Add("stop:" + request.RunId);
    }

    private static MapScanEngine Engine(IMapScanBlockSource source, Sink sink, int deadlineMs = 400) =>
        new(source, sink, acquisitionCleanupTimeout: TimeSpan.FromMilliseconds(deadlineMs));

    private static async Task TestPositiveAndLegacyAsync()
    {
        var sink = new Sink();
        var source = new Source
        {
            CleanupAction = (request, reason, token) =>
            {
                Require(reason == MapScanAcquisitionExitReason.Completed &&
                        request.RunId == Request.RunId && request.ServerId == 2213 &&
                        request.LaunchSessionId == "owned-native-session" &&
                        token.CanBeCanceled && !token.IsCancellationRequested,
                    "successful cleanup did not own this session or bounded token");
                sink.Events.Add("cleanup:" + request.RunId);
                return Task.FromResult(Home(request));
            }
        };
        await Engine(source, sink).ExecuteAsync(Request);
        Require(source.CleanupCount == 1 && sink.Staged == 1 && sink.Published == 1 &&
                sink.Events.SequenceEqual([
                    "begin:map014-owned-run:1", "stage:map014-owned-run",
                    "cleanup:map014-owned-run", "publish:map014-owned-run"
                ]), "publication was not strictly after one verified native cleanup");

        sink = new Sink();
        await Engine(new LegacySource(), sink).ExecuteAsync(Request with { LaunchSessionId = null });
        Require(sink.Events.SequenceEqual([
            "begin:map014-owned-run:1", "stage:map014-owned-run", "publish:map014-owned-run"
        ]), "non-opted legacy engine semantics changed");

        sink = new Sink();
        source = new Source();
        await Throws<InvalidDataException>(
            () => Engine(source, sink).ExecuteAsync(Request with { LaunchSessionId = null }),
            "consecutive source without exact native session was admitted");
        Require(sink.Events.Count == 0 && source.CleanupCount == 0, "rejected source opened a persistent run");
    }

    private static async Task TestCancellationAfterPositiveStageAsync()
    {
        using var cancel = new CancellationTokenSource();
        var sink = new Sink();
        var source = new Source
        {
            CleanupAction = (request, reason, token) =>
            {
                Require(reason == MapScanAcquisitionExitReason.Stopped &&
                        token.CanBeCanceled && !token.IsCancellationRequested &&
                        cancel.IsCancellationRequested,
                    "Stop passed cancelled scan token into native cleanup");
                sink.Events.Add("cleanup");
                return Task.FromResult(Home(request));
            }
        };
        sink.OnStage = cancel.Cancel;
        await Throws<OperationCanceledException>(
            () => Engine(source, sink).ExecuteAsync(Request, cancel.Token),
            "positive staging must not publish after Stop");
        Require(source.CleanupCount == 1 && sink.Staged == 1 && sink.Published == 0 &&
                sink.Events.SequenceEqual([
                    "begin:map014-owned-run:1", "stage:map014-owned-run",
                    "cleanup", "stop:map014-owned-run"
                ]), "cancelled positive staging escaped cleanup-before-Stop order");
    }

    private static async Task TestLateCaptureAndOwnerFailureAsync()
    {
        using var cancel = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sink = new Sink();
        var source = new Source
        {
            CaptureAction = async (request, block, _) =>
            {
                entered.TrySetResult();
                await release.Task;
                return Capture(request, block); // ignores cancellation; must still be rejected
            },
            CleanupAction = (request, reason, token) =>
            {
                Require(reason == MapScanAcquisitionExitReason.Stopped, "late capture did not Stop");
                sink.Events.Add("cleanup");
                return Task.FromResult(Home(request));
            }
        };
        Task run = Engine(source, sink).ExecuteAsync(Request, cancel.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        cancel.Cancel();
        release.TrySetResult();
        await Throws<OperationCanceledException>(() => run, "late result after cancelled capture was staged");
        Require(sink.Staged == 0 && sink.Published == 0 && source.CleanupCount == 1 &&
                sink.Events.SequenceEqual(["begin:map014-owned-run:1", "cleanup", "stop:map014-owned-run"]),
                "late result bypassed run retirement guard");

        sink = new Sink();
        source = new Source
        {
            CaptureAction = (request, block, _) =>
                Task.FromResult(Capture(request, block) with { ServerId = request.ServerId + 1 })
        };
        await Throws<InvalidDataException>(
            () => Engine(source, sink).ExecuteAsync(Request),
            "foreign server capture acquired under exact run");
        Require(sink.Staged == 0 && sink.Published == 0 && source.CleanupCount == 1 &&
                sink.Events.Last() == "fail:map014-owned-run",
            "late/mismatched source data staged or suppressed its run failure");
    }

    private static async Task TestHeldCleanupCancellationAsync()
    {
        using var cancel = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sink = new Sink();
        var source = new Source
        {
            CleanupAction = async (request, reason, token) =>
            {
                Require(reason == MapScanAcquisitionExitReason.Completed,
                    "positive native cleanup unexpectedly restarted after Stop");
                sink.Events.Add("cleanup-start");
                entered.TrySetResult();
                await release.Task.WaitAsync(token);
                sink.Events.Add("cleanup-finished");
                return Home(request);
            }
        };
        Task scan = Engine(source, sink).ExecuteAsync(Request, cancel.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        cancel.Cancel();
        Require(!scan.IsCompleted && sink.Staged == 1 && sink.Published == 0 &&
                !sink.Events.Any(e => e.StartsWith("stop:")),
            "Stop wrote terminal state before awaited native cleanup");
        release.TrySetResult();
        await Throws<OperationCanceledException>(() => scan,
            "Stop during successful cleanup must still suppress Publish");
        Require(source.CleanupCount == 1 && sink.Published == 0 &&
                sink.Events.TakeLast(2).SequenceEqual(["cleanup-finished", "stop:map014-owned-run"]),
            "held cleanup was not awaited exactly once before Stop");
    }

    private static async Task TestFailureAndNegativeReceiptsAsync()
    {
        async Task CheckInvalid(string name, Func<MapScanExecutionRequest, Task<MapScanAcquisitionCleanupReceipt>> response)
        {
            var sink = new Sink();
            var source = new Source
            {
                CleanupAction = (request, reason, token) =>
                {
                    Require(reason == MapScanAcquisitionExitReason.Completed, name + ": wrong reason");
                    sink.Events.Add("cleanup");
                    return response(request);
                }
            };
            await Throws<Exception>(() => Engine(source, sink).ExecuteAsync(Request), name);
            Require(source.CleanupCount == 1 && sink.Staged == 1 && sink.Published == 0 &&
                    sink.Events.TakeLast(2).SequenceEqual(["cleanup", "fail:map014-owned-run"]),
                name + ": false home or wrong owner published or retried cleanup");
        }
        await CheckInvalid("wrong run", req => Task.FromResult(Home(req with { RunId = "superseding-run" })));
        await CheckInvalid("wrong session", req => Task.FromResult(Home(req with { LaunchSessionId = "new-session" })));
        await CheckInvalid("wrong selected kind", req => Task.FromResult(Home(req with { SelectedTypes = ["truck"] })));
        await CheckInvalid("false camera", req => Task.FromResult(Home(req) with { OriginalCameraRestored = false }));
        await CheckInvalid("stale home reply", req => Task.FromResult(Home(req) with { FreshHomeViewReplyMatched = false }));
        await CheckInvalid("native throws", _ =>
            Task.FromException<MapScanAcquisitionCleanupReceipt>(new IOException("owner unavailable")));

        var held = new TaskCompletionSource<MapScanAcquisitionCleanupReceipt>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        await CheckInvalid("bounded timeout", _ => held.Task);

        var failureSink = new Sink();
        var failureSource = new Source
        {
            CaptureAction = (_, _, _) => Task.FromException<MapScanBlockCapture>(new IOException("producer failed")),
            CleanupAction = (request, reason, _) =>
            {
                Require(reason == MapScanAcquisitionExitReason.Failed, "source failure cleanup not marked Failed");
                failureSink.Events.Add("cleanup");
                return Task.FromResult(Home(request));
            }
        };
        await Throws<BridgeCommandException>(
            () => Engine(failureSource, failureSink).ExecuteAsync(Request),
            "exhausted block source failure must reject entire run");
        Require(failureSource.CleanupCount == 1 && failureSink.Published == 0 &&
                failureSink.Events.TakeLast(2).SequenceEqual(["cleanup", "fail:map014-owned-run"]),
            "failed source did not await cleanup before sink.Fail");
    }

    private static async Task TestFailureAfterPartialPositiveStagingAsync()
    {
        MapScanExecutionRequest twoBlocks = Request with { TileWidth = 40 };
        var sink = new Sink();
        var source = new Source
        {
            CaptureAction = (request, block, _) => block.BlockIndex == 0
                ? Task.FromResult(Capture(request, block))
                : Task.FromException<MapScanBlockCapture>(new IOException("second block native source failed")),
            CleanupAction = (request, reason, token) =>
            {
                Require(reason == MapScanAcquisitionExitReason.Failed &&
                        sink.Staged == 1 && sink.Published == 0 &&
                        sink.Events.Any(e => e.StartsWith("failed-block:")),
                    "positive staging + later failed source did not cleanup before terminal fail");
                sink.Events.Add("cleanup");
                return Task.FromResult(Home(request));
            }
        };
        await Throws<BridgeCommandException>(
            () => Engine(source, sink).ExecuteAsync(twoBlocks),
            "partial positive staging incorrectly published despite later source failure");
        Require(source.CleanupCount == 1 && sink.Staged == 1 && sink.Published == 0 &&
                sink.Events.TakeLast(2).SequenceEqual(["cleanup", "fail:map014-owned-run"]),
            "source failed after staged block but native cleanup was skipped or duplicated");
    }

    private static async Task TestCancellationCleanupFailurePreservedAsync()
    {
        using var cancel = new CancellationTokenSource();
        var sink = new Sink { OnStage = cancel.Cancel };
        var source = new Source
        {
            CleanupAction = (_, reason, token) =>
            {
                Require(reason == MapScanAcquisitionExitReason.Stopped &&
                        token.CanBeCanceled && !token.IsCancellationRequested,
                    "failure-path cleanup reused cancelled scan token");
                return Task.FromException<MapScanAcquisitionCleanupReceipt>(
                    new IOException("native owner departed"));
            }
        };
        OperationCanceledException error = await Throws<OperationCanceledException>(
            () => Engine(source, sink).ExecuteAsync(Request, cancel.Token),
            "cleanup failure masked Stop cancellation");
        Require(error.Data.Contains("MAP014_NATIVE_CLEANUP_UNVERIFIED") &&
                source.CleanupCount == 1 && sink.Staged == 1 && sink.Published == 0 &&
                sink.Events.Last() == "stop:map014-owned-run",
            "cleanup failure lost visible unverified status or bypassed Stop");
    }

    private static int SqlCount(string db, string table, string filter = "1=1")
    {
        using var connection = new SqliteConnection("Data Source=" + db);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table} WHERE {filter}";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static async Task TestRealSQLiteStagingAndPublicationAsync()
    {
        // Each scenario uses the real engine + SQLite production staging sink.
        // No native source, game or installed script is exercised.
        foreach (string scenario in new[] { "complete", "invalid-cleanup", "stop-after-stage" })
        {
            string root = Path.Combine(Path.GetTempPath(), "map014-cleanup-sqlite-" + Guid.NewGuid().ToString("N"));
            string database = Path.Combine(root, "controlled.db");
            Directory.CreateDirectory(root);
            try
            {
                using var store = new MapDataStore(database);
                store.UpsertRecord(new MapStoredRecord(
                    "city", Request.ServerId, "baseline", 110, null, "Published Before Run",
                    null, 15, null, null, null, null, 1800000000000L, "{}"));
                Require(store.GetRecord("city", Request.ServerId, "baseline") is not null,
                    scenario + ": published baseline not seeded");

                using var cancel = new CancellationTokenSource();
                var source = new Source
                {
                    CaptureAction = (request, block, _) => Task.FromResult(
                        Capture(request, block) with
                        {
                            Records = [
                                new MapStoredRecord("city", request.ServerId, "candidate", 220,
                                    null, "Unpublished Candidate", null, 16, null, null, null,
                                    null, 1800000000001L, "{}")
                            ]
                        }),
                    CleanupAction = (request, reason, token) =>
                    {
                        Require(SqlCount(database, "scan_records", "kind='city'") == 1 &&
                                store.GetRecord("city", Request.ServerId, "baseline") is not null &&
                                store.GetRecord("city", Request.ServerId, "candidate") is null,
                            scenario + ": real SQLite staging did not precede cleanup");
                        Require(token.CanBeCanceled && !token.IsCancellationRequested &&
                                reason == (scenario == "stop-after-stage"
                                    ? MapScanAcquisitionExitReason.Stopped
                                    : MapScanAcquisitionExitReason.Completed),
                            scenario + ": cleanup token or exit classification was incorrect");
                        return Task.FromResult(scenario == "invalid-cleanup"
                            ? Home(request) with { FreshHomeViewReplyMatched = false }
                            : Home(request));
                    }
                };
                var engine = new MapScanEngine(source, new MapDataStoreScanSink(store),
                    progress =>
                    {
                        if (scenario == "stop-after-stage" && progress.Phase == "publishing")
                            cancel.Cancel();
                    }, acquisitionCleanupTimeout: TimeSpan.FromMilliseconds(400));
                MapScanExecutionRequest request = Request with { RunId = "sql-" + scenario };
                if (scenario == "complete")
                    await engine.ExecuteAsync(request);
                else if (scenario == "invalid-cleanup")
                    await Throws<InvalidDataException>(() => engine.ExecuteAsync(request),
                        "invalid native home receipt published SQLite candidate");
                else
                    await Throws<OperationCanceledException>(
                        () => engine.ExecuteAsync(request, cancel.Token),
                        "Stop after native-backed staging published SQLite candidate");

                string terminal = scenario == "complete" ? "completed"
                    : scenario == "invalid-cleanup" ? "failed" : "discarded";
                Require(source.CleanupCount == 1 &&
                        SqlCount(database, "scan_runs", $"id='{request.RunId}' AND status='{terminal}'") == 1 &&
                        SqlCount(database, "scan_records", $"run_id='{request.RunId}'") == 0 &&
                        SqlCount(database, "scan_blocks", $"run_id='{request.RunId}'") == 0 &&
                        SqlCount(database, "map_records", "kind='city' AND server_id=2213") == 1 &&
                        (store.GetRecord("city", request.ServerId, "baseline") is not null) ==
                            (scenario != "complete") &&
                        (store.GetRecord("city", request.ServerId, "candidate") is not null) ==
                            (scenario == "complete"),
                    scenario + ": real SQLite publish/retire transaction violated last published data");
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                try { Directory.Delete(root, recursive: true); } catch { }
            }
        }
    }

    internal static async Task RunAsync()
    {
        await TestPositiveAndLegacyAsync();
        await TestCancellationAfterPositiveStageAsync();
        await TestLateCaptureAndOwnerFailureAsync();
        await TestHeldCleanupCancellationAsync();
        await TestFailureAndNegativeReceiptsAsync();
        await TestFailureAfterPartialPositiveStagingAsync();
        await TestCancellationCleanupFailurePreservedAsync();
        await TestRealSQLiteStagingAndPublicationAsync();
        Console.WriteLine("MAP014_CONSECUTIVE_ENGINE_CLEANUP_CONTROLLED_OK native=false publishFailClosed=true sqliteStaging=true");
    }
}
