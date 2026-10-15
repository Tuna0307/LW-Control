using LWBridge.Desktop;
using Microsoft.Data.Sqlite;

namespace LWBridge.Desktop.Checks;

// Actual engine and production SQLite sink with an isolated controlled source.
// No game, native response, camera or owner database is exercised by these tests.
internal static class Map014TerminalSourceValidationChecks
{
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("MAP014_TERMINAL_SOURCE: " + message);
    }

    private static object? SqlValue(string database, string sql, string runId)
    {
        using var connection = new SqliteConnection("Data Source=" + database);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$run", runId);
        return command.ExecuteScalar();
    }

    private sealed class Source(string scenario, string database,
        MapScanSourceValidationException terminalError) : IMapScanBatchSource, IMapScanAcquisitionCleanup
    {
        internal int Calls;
        internal int FaultCalls;
        internal int CleanupCalls;

        public Task<MapScanBlockCapture> CaptureAsync(MapScanExecutionRequest request,
            MapScanTargetBlock block, CancellationToken token) =>
            throw new InvalidOperationException("Source validation must not downgrade to per-block capture.");

        public Task<IReadOnlyList<MapScanBlockCapture>> CaptureBatchAsync(MapScanExecutionRequest request,
            MapScanTargetBlock block, IReadOnlySet<int> pending, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Calls++;
            Require(pending.Contains(block.BlockIndex), "source received an already checkpointed block");
            if (block.BlockIndex == 1)
            {
                Require(Convert.ToInt32(SqlValue(database,
                    "SELECT COUNT(*) FROM scan_records WHERE run_id=$run", request.RunId)) == 1,
                    "the distinguishing source failure did not follow positive durable staging");
                if (scenario == "fatal")
                {
                    FaultCalls++;
                    return Task.FromException<IReadOnlyList<MapScanBlockCapture>>(terminalError);
                }
                if (FaultCalls++ == 0)
                    return Task.FromException<IReadOnlyList<MapScanBlockCapture>>(scenario == "timeout"
                        ? new TimeoutException("ordinary transient response timeout")
                        : new IOException("ordinary transient source read"));
            }
            MapStoredRecord record = new("resource", request.ServerId, "candidate-" + block.BlockIndex,
                111 + block.BlockIndex, null, "Controlled Resource", null, 5,
                null, null, null, null, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                $"{{\"x\":{block.MinX + 1},\"y\":{block.MinY + 1}}}");
            return Task.FromResult<IReadOnlyList<MapScanBlockCapture>>([
                new MapScanBlockCapture(request.ServerId, request.WorldId, block.BlockIndex, "{}", [record])
            ]);
        }

        public Task<MapScanAcquisitionCleanupReceipt> CleanupAsync(MapScanExecutionRequest request,
            MapScanAcquisitionExitReason reason, CancellationToken token)
        {
            CleanupCalls++;
            Require(token.CanBeCanceled && !token.IsCancellationRequested,
                "cleanup must receive its independent bounded token");
            MapScanAcquisitionExitReason expected = scenario == "fatal"
                ? MapScanAcquisitionExitReason.Failed : scenario == "cancel"
                    ? MapScanAcquisitionExitReason.Stopped : MapScanAcquisitionExitReason.Completed;
            Require(reason == expected, "terminal classification was changed by source validation");
            Require(Convert.ToInt32(SqlValue(database,
                    "SELECT COUNT(*) FROM scan_records WHERE run_id=$run", request.RunId)) ==
                    (scenario is "fatal" or "cancel" ? 1 : 3),
                "staging did not remain unpublished until cleanup");
            return Task.FromResult(new MapScanAcquisitionCleanupReceipt(request, true, true));
        }
    }

    private sealed class Sink(MapDataStore store) : IMapScanRunSink
    {
        private readonly MapDataStoreScanSink production = new(store);
        internal int PublishCalls;
        internal int FailCalls;
        internal int StopCalls;
        internal int FailedBlockCalls;
        internal string? FailureMessage;
        public void Begin(MapScanExecutionRequest request, int total, long now) => production.Begin(request, total, now);
        public void CheckpointSuccess(MapScanExecutionRequest request, MapScanTargetBlock block,
            MapScanBlockCapture capture, int attempts, long now) =>
            production.CheckpointSuccess(request, block, capture, attempts, now);
        public void CheckpointFailure(MapScanExecutionRequest request, MapScanTargetBlock block,
            int attempts, string error, long now)
        {
            FailedBlockCalls++;
            production.CheckpointFailure(request, block, attempts, error, now);
        }
        public void Publish(MapScanExecutionRequest request, long now)
        {
            PublishCalls++;
            production.Publish(request, now);
        }
        public void Fail(MapScanExecutionRequest request, string error, long now)
        {
            FailCalls++;
            FailureMessage = error;
            production.Fail(request, error, now);
        }
        public void Stop(MapScanExecutionRequest request, long now)
        {
            StopCalls++;
            production.Stop(request, now);
        }
    }

    private static async Task CheckAsync(string scenario)
    {
        string root = Path.Combine(Path.GetTempPath(), "map014-terminal-source-" + Guid.NewGuid().ToString("N"));
        string database = Path.Combine(root, "controlled.db");
        Directory.CreateDirectory(root);
        Exception? testFailure = null;
        try
        {
            using var store = new MapDataStore(database);
            store.UpsertRecord(new MapStoredRecord("resource", 2213, "published-before-run", 111,
                null, "Published Resource", null, 5, null, null, null, null,
                1_800_000_000_000, "{}"));
            var request = new MapScanExecutionRequest("terminal-" + scenario, 2213, 0,
                60, 20, ["resource"], 1, MaxAttemptsPerBlock: 2,
                LaunchSessionId: "controlled-source-session");
            var original = new MapScanSourceValidationException("native Resource hydration identity incomplete",
                new InvalidDataException("controlled native missing source identity"));
            var source = new Source(scenario, database, original);
            var sink = new Sink(store);
            using var cancel = new CancellationTokenSource();
            var engine = new MapScanEngine(source, sink, progress =>
            {
                if (scenario == "cancel" && progress.CompletedBlocks == 1) cancel.Cancel();
            }, acquisitionCleanupTimeout: TimeSpan.FromSeconds(1));
            Exception? observed = null;
            try { await engine.ExecuteAsync(request, cancel.Token); }
            catch (Exception error) { observed = error; }

            string expectedStatus = scenario == "fatal" ? "failed" : scenario == "cancel" ? "discarded" : "completed";
            Require((string?)SqlValue(database, "SELECT status FROM scan_runs WHERE id=$run", request.RunId) == expectedStatus,
                "actual SQLite run did not reach expected terminal status: " + scenario);
            Require(source.CleanupCalls == 1 && sink.FailedBlockCalls == 0 &&
                    Convert.ToInt32(SqlValue(database, "SELECT COUNT(*) FROM scan_records WHERE run_id=$run", request.RunId)) == 0 &&
                    Convert.ToInt32(SqlValue(database, "SELECT COUNT(*) FROM scan_blocks WHERE run_id=$run", request.RunId)) == 0,
                "terminal retirement retained staging, repeated cleanup or downgraded to a failed block");
            if (scenario == "fatal")
            {
                Require(ReferenceEquals(observed, original) && ReferenceEquals(observed!.InnerException, original.InnerException) &&
                        source.Calls == 2 && source.FaultCalls == 1 && sink.FailCalls == 1 &&
                        sink.PublishCalls == 0 && sink.StopCalls == 0 && sink.FailureMessage == original.Message &&
                        (string?)SqlValue(database, "SELECT error FROM scan_runs WHERE id=$run", request.RunId) == original.Message,
                    "terminal source error was replaced, retried or used for per-band fallback");
            }
            else if (scenario == "cancel")
            {
                Require(observed is OperationCanceledException && source.Calls == 1 && source.FaultCalls == 0 &&
                        sink.PublishCalls == 0 && sink.FailCalls == 0 && sink.StopCalls == 1,
                    "cancellation after positive staging became a validation failure or retried source");
            }
            else
            {
                Require(observed is null && source.Calls == 4 && source.FaultCalls == 2 &&
                        sink.PublishCalls == 1 && sink.FailCalls == 0 && sink.StopCalls == 0,
                    "ordinary IO/timeout stopped being a bounded retryable capture: " + observed);
            }
            bool published = scenario is "io" or "timeout";
            Require((store.GetRecord("resource", 2213, "published-before-run") is null) == published &&
                    (store.GetRecord("resource", 2213, "candidate-0") is not null) == published,
                "failed/cancelled source replaced previously published data");
        }
        catch (Exception error) { testFailure = error; throw; }
        finally
        {
            try
            {
                SqliteConnection.ClearAllPools();
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
            catch (Exception cleanupError) when (testFailure is not null)
            {
                Console.Error.WriteLine("MAP014 source test cleanup after failure: " + cleanupError.Message);
            }
        }
    }

    internal static async Task RunAsync()
    {
        foreach (string scenario in new[] { "fatal", "io", "timeout", "cancel" })
            await CheckAsync(scenario);
        Console.WriteLine("MAP014_TERMINAL_SOURCE_VALIDATION_OK native=false sqlite=true fatalNoRetry=true ordinaryRetries=true");
    }
}
