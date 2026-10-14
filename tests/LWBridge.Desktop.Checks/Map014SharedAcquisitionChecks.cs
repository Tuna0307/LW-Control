using System.Runtime.CompilerServices;
using LWBridge.Desktop;
using Microsoft.Data.Sqlite;

namespace LWBridge.Desktop.Checks;

/// <summary>
/// Shared native acquisition is simulated. These exercises use the real
/// production MapScanEngine and SQLite scan sink, never a native/game witness.
/// </summary>
internal static class Map014SharedAcquisitionChecks
{
    private static void Require(bool condition, string why)
    {
        if (!condition) throw new InvalidOperationException("MAP014_SHARED: " + why);
    }

    private static async Task<T> Reject<T>(Func<Task> action, string why) where T : Exception
    {
        try { await action(); }
        catch (T error) { return error; }
        throw new InvalidOperationException("MAP014_SHARED expected " + typeof(T).Name + ": " + why);
    }

    private static MapScanExecutionRequest Request(string run, int server, params string[] kinds) =>
        new(run, server, 0, 1000, 1000, kinds, 1, MaxAttemptsPerBlock: 1,
            ScanMode: "fast", LaunchSessionId: "session-for-" + run);

    private static MapScanSharedAcquisitionOwner Owner(
        MapScanExecutionRequest request, string profile = "profile-one") =>
        new(request.RunId, request.ServerId, request.WorldId, request.LaunchSessionId!,
            request.SelectedTypes.ToArray(), profile, 12123, "generation-for-" + request.RunId);

    private static MapStoredRecord Record(
        MapScanExecutionRequest request, MapScanTargetBlock block, string kind) =>
        new(kind, request.ServerId, kind + "-" + block.BlockIndex,
            block.TargetY * 1000 + block.TargetX + 1,
            kind == "truck" ? "truck-" + block.BlockIndex : null,
            "controlled-" + kind,
            null, 10, null, null, null, null, 1800000000000L,
            kind == "city"
                ? System.Text.Json.JsonSerializer.Serialize(new { ownerUid = "native-owner-" + block.BlockIndex })
                : "{}");

    private sealed class SharedSource : IMapScanSharedAcquisitionSource
    {
        internal readonly List<(MapScanAcquisitionExitReason Reason, bool TokenCancelled)> Cleanups = [];
        internal Func<int, MapScanSharedAcquisitionBatch, MapScanSharedAcquisitionBatch>? AlterBatch;
        internal Func<int, CancellationToken, Task>? BeforeYieldAsync;
        internal Func<MapScanExecutionRequest, MapScanAcquisitionExitReason, CancellationToken,
            Task<MapScanAcquisitionCleanupReceipt>>? OnCleanupAsync;
        internal int? EndAfterBatch;
        internal int Calls;
        internal readonly List<int> Submitted = [];
        internal MapScanSharedAcquisitionOwner AcquisitionOwner { get; set; }
        internal int ChunkSize = 250;

        public SharedSource(MapScanSharedAcquisitionOwner owner) => AcquisitionOwner = owner;

        MapScanSharedAcquisitionOwner IMapScanSharedAcquisitionSource.AcquisitionOwner => AcquisitionOwner;

        public Task<MapScanBlockCapture> CaptureAsync(
            MapScanExecutionRequest _, MapScanTargetBlock __, CancellationToken ___) =>
            throw new InvalidOperationException("Shared mode entered legacy per-block source");

        public async IAsyncEnumerable<MapScanSharedAcquisitionBatch> CaptureSharedAsync(
            MapScanExecutionRequest request,
            IReadOnlyList<MapScanTargetBlock> orderedBlocks,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Calls++;
            ValidateFullWorld(orderedBlocks);
            Require(request.SelectedTypes.SequenceEqual(AcquisitionOwner.SelectedTypes),
                "source selection no longer matches captured shared owner");
            for (int offset = 0, batch = 0; offset < orderedBlocks.Count; offset += ChunkSize, batch++)
            {
                await Task.Yield();
                var captures = orderedBlocks.Skip(offset).Take(ChunkSize)
                    .Select(block => new MapScanBlockCapture(
                        request.ServerId, request.WorldId, block.BlockIndex, "{}",
                        request.SelectedTypes.Select(kind => Record(request, block, kind)).ToArray()))
                    .ToArray();
                MapScanSharedAcquisitionBatch result = new(AcquisitionOwner, captures);
                if (BeforeYieldAsync is not null)
                    await BeforeYieldAsync(batch, cancellationToken);
                // Deliberately record all source attempts, including results
                // that arrive late and MUST be rejected by the engine.
                Submitted.AddRange(captures.Select(capture => capture.BlockIndex));
                yield return AlterBatch?.Invoke(batch, result) ?? result;
                if (EndAfterBatch == batch)
                    yield break;
            }
        }

        public Task<MapScanAcquisitionCleanupReceipt> CleanupAsync(
            MapScanExecutionRequest request, MapScanAcquisitionExitReason reason, CancellationToken token)
        {
            Cleanups.Add((reason, token.IsCancellationRequested));
            return OnCleanupAsync?.Invoke(request, reason, token) ??
                Task.FromResult(new MapScanAcquisitionCleanupReceipt(request, true, true, AcquisitionOwner));
        }
    }

    private static void ValidateFullWorld(IReadOnlyList<MapScanTargetBlock> blocks)
    {
        Require(blocks.Count == 2500, "full 1000x1000 world does not have 2500 logical blocks");
        var seen = new HashSet<int>();
        for (int index = 0; index < blocks.Count; index++)
        {
            MapScanTargetBlock block = blocks[index];
            int col = index % 50, row = index / 50;
            Require(block.BlockIndex == index && seen.Add(index) &&
                    block.MinX == col * 20 && block.MaxX == col * 20 + 19 &&
                    block.MinY == row * 20 && block.MaxY == row * 20 + 19 &&
                    block.TargetX >= block.MinX && block.TargetX <= block.MaxX &&
                    block.TargetY >= block.MinY && block.TargetY <= block.MaxY,
                "lost full-world 0/19/20/979/980/999 row or column seam at " + index);
        }
        Require(blocks[0].MinX == 0 && blocks[0].MinY == 0 &&
                blocks[^1].MaxX == 999 && blocks[^1].MaxY == 999 &&
                blocks[49].MaxX == 999 && blocks[50].MinX == 0 &&
                blocks[2450].MinY == 980, "full-world boundary geometry invalid");
    }

    private static int Count(string db, string table, string where = "1=1")
    {
        using var connection = new SqliteConnection("Data Source=" + db);
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM {table} WHERE {where}";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static string TempDb()
    {
        string path = Path.Combine(Path.GetTempPath(), "map014-shared-" +
            Guid.NewGuid().ToString("N"), "data.db");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return path;
    }

    private static void CleanDb(string path)
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(Path.GetDirectoryName(path)!, true); } catch { }
    }

    private static void Seed(MapDataStore store, int server, string kind = "city")
    {
        store.UpsertRecord(new MapStoredRecord(
            kind, server, "baseline", 1, null, "existing", null,
            1, null, null, null, null, 1800000000000L, "{}"));
    }

    private static void VerifyTerminal(
        string db, MapScanExecutionRequest request, string status, bool published)
    {
        Require(Count(db, "scan_runs", $"id='{request.RunId}' AND status='{status}'") == 1 &&
                Count(db, "scan_records", $"run_id='{request.RunId}'") == 0 &&
                Count(db, "scan_blocks", $"run_id='{request.RunId}'") == 0,
            "real SQLite retained wrong terminal or run-owned staging: " + status);
        foreach (string kind in request.SelectedTypes)
        {
            int rows = Count(db, "map_records",
                $"server_id={request.ServerId} AND kind='{kind}'");
            Require(rows == (published ? 2500 : kind == "city" ? 1 : 0),
                $"published {rows} {kind} rows when published={published}");
        }
    }

    private static async Task CheckSuccessfulSharedWorldAsync(string[] kinds)
    {
        string db = TempDb();
        try
        {
            using var store = new MapDataStore(db);
            MapScanExecutionRequest request = Request("all-" + kinds[1], 2213, kinds);
            Seed(store, request.ServerId);
            // Separate server is a published baseline that must never be deleted.
            Seed(store, 2214);
            var source = new SharedSource(Owner(request));
            await new MapScanEngine(source, new MapDataStoreScanSink(store))
                .ExecuteAsync(request);
            Require(source.Calls == 1 && source.Cleanups.SequenceEqual([
                    (MapScanAcquisitionExitReason.Completed, false)
                ]) && source.Submitted.SequenceEqual(Enumerable.Range(0, 2500)),
                "did not use one ordered full-world discovery followed by one home cleanup");
            VerifyTerminal(db, request, "completed", true);
            Require(Count(db, "map_records", "server_id=2214 AND kind='city'") == 1 &&
                    Count(db, "map_records", "server_id=2213 AND kind='city' AND record_key='baseline'") == 0 &&
                    store.GetRecord("city", 2213, "city-0")?.PointIndex == 9010 &&
                    store.GetRecord("city", 2213, "city-2499")?.PointIndex == 989990,
                "new City identity/seams or foreign server published data changed");
        }
        finally { CleanDb(db); }
    }

    private static async Task CheckRejectedStreamAsync(
        string label, Action<SharedSource, MapScanExecutionRequest> alter, bool useRemoteServer = false)
    {
        string db = TempDb();
        try
        {
            using var store = new MapDataStore(db);
            MapScanExecutionRequest request = Request("reject-" + label, useRemoteServer ? 2214 : 2213,
                "city", "resource");
            Seed(store, request.ServerId);
            Seed(store, request.ServerId == 2214 ? 2213 : 2214);
            var source = new SharedSource(Owner(request, "profile-two"));
            alter(source, request);
            await Reject<InvalidDataException>(() =>
                new MapScanEngine(source, new MapDataStoreScanSink(store)).ExecuteAsync(request),
                "invalid shared stream was published: " + label);
            Require(source.Calls == 1 && source.Cleanups.Count == 1 &&
                    source.Cleanups[0].Reason == MapScanAcquisitionExitReason.Failed,
                "failed shared stream not cleaned once: " + label);
            VerifyTerminal(db, request, "failed", false);
            Require(Count(db, "map_records", "kind='city'") == 2,
                "wrong profile or remote server staging changed independent City baselines");
        }
        finally { CleanDb(db); }
    }

    private static Task CheckRejectedNativeIdentityAsync(
        string label, int recordIndex, Func<MapStoredRecord, MapStoredRecord> mutate) =>
        CheckRejectedStreamAsync(label, (source, _) =>
            source.AlterBatch = (batch, value) => batch == 1
                ? value with
                {
                    Captures = [
                        value.Captures[0] with
                        {
                            Records = value.Captures[0].Records.Select((record, index) =>
                                index == recordIndex ? mutate(record) : record).ToArray()
                        },
                        ..value.Captures.Skip(1)
                    ]
                }
                : value);

    private static async Task CheckEarlyAdmissionRejectAsync()
    {
        foreach (string dimension in new[] {
            "run", "server", "world", "session", "selection", "profile", "pid", "generation", "bounds"
        })
        {
            string db = TempDb();
            try
            {
                using var store = new MapDataStore(db);
                MapScanExecutionRequest req = Request("admit-" + dimension, 2213, "city", "resource");
                MapScanSharedAcquisitionOwner owner = Owner(req);
                owner = dimension switch
                {
                    "run" => owner with { RunId = "previous-run" },
                    "server" => owner with { ServerId = 2214 },
                    "world" => owner with { WorldId = 1 },
                    "session" => owner with { LaunchSessionId = "superseding-session" },
                    "selection" => owner with { SelectedTypes = ["city", "truck"] },
                    "profile" => owner with { ProfileId = "" },
                    "pid" => owner with { GameProcessId = 0 },
                    "generation" => owner with { GroupGeneration = "" },
                    _ => owner
                };
                var source = new SharedSource(owner);
                if (dimension == "bounds") req = req with { TileWidth = 980 };
                await Reject<InvalidDataException>(
                    () => new MapScanEngine(source, new MapDataStoreScanSink(store)).ExecuteAsync(req),
                    "invalid owner admitted: " + dimension);
                Require(source.Calls == 0 && source.Cleanups.Count == 0 &&
                        Count(db, "scan_runs") == 0,
                    "invalid native run entered source or SQLite staging: " + dimension);
            }
            finally { CleanDb(db); }
        }
    }

    private static async Task CheckCancellationAsync(bool duringCleanup)
    {
        string db = TempDb();
        try
        {
            using var store = new MapDataStore(db);
            MapScanExecutionRequest request = Request(duringCleanup ? "cancel-final" : "cancel-batch",
                2213, "city", "truck");
            Seed(store, 2213);
            Seed(store, 2214);
            using var cancellation = new CancellationTokenSource();
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            MapScanSharedAcquisitionOwner owned = Owner(request);
            var source = new SharedSource(owned)
            {
                BeforeYieldAsync = async (batch, _) =>
                {
                    if (!duringCleanup && batch == 1)
                    {
                        entered.TrySetResult();
                        await release.Task; // intentionally ignores cancellation
                    }
                },
                OnCleanupAsync = async (req, reason, cleanupToken) =>
                {
                    Require(cleanupToken.CanBeCanceled && !cleanupToken.IsCancellationRequested,
                        "native cleanup reused cancelled scan token");
                    if (duringCleanup)
                    {
                        Require(reason == MapScanAcquisitionExitReason.Completed &&
                                Count(db, "scan_records") == 5000, "cleanup before full positive staging");
                        entered.TrySetResult();
                        await release.Task.WaitAsync(cleanupToken);
                    }
                    else
                    {
                        Require(reason == MapScanAcquisitionExitReason.Stopped,
                            "cancelled late batch acquired after source token was cancelled");
                    }
                    return new(req, true, true, owned);
                }
            };
            Task run = new MapScanEngine(source, new MapDataStoreScanSink(store),
                acquisitionCleanupTimeout: TimeSpan.FromSeconds(4))
                .ExecuteAsync(request, cancellation.Token);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(6));
            Require(Count(db, "scan_records") == (duringCleanup ? 5000 : 500) &&
                    Count(db, "map_records", "server_id=2213 AND kind='city'") == 1,
                "held acquisition did not stage the exact expected positive batch");
            cancellation.Cancel();
            Require(!run.IsCompleted && Count(db, "scan_runs", "status='running'") == 1,
                "Stop retired run or published before pending source/home cleanup");
            release.TrySetResult();
            await Reject<OperationCanceledException>(() => run,
                "late source/home reply published after cancellation");
            Require(source.Calls == 1 && source.Cleanups.Count == 1 &&
                    !source.Cleanups[0].TokenCancelled, "cancel cleanup skipped or duplicated");
            VerifyTerminal(db, request, "discarded", false);
            Require(Count(db, "map_records", "server_id=2214 AND kind='city'") == 1,
                "cancelled remote acquisition overwrote foreign published City");
        }
        finally { CleanDb(db); }
    }

    private static async Task CheckCleanupFailureAsync()
    {
        string db = TempDb();
        try
        {
            using var store = new MapDataStore(db);
            MapScanExecutionRequest request = Request("cleanup-false", 2213, "city", "resource");
            Seed(store, 2213);
            Seed(store, 2214);
            var source = new SharedSource(Owner(request))
            {
                OnCleanupAsync = (req, reason, _) =>
                {
                    Require(reason == MapScanAcquisitionExitReason.Completed &&
                            Count(db, "scan_records") == 5000,
                        "false native cleanup was not after full successful staging");
                    return Task.FromResult(new MapScanAcquisitionCleanupReceipt(
                        req, true, true,
                        Owner(req) with { GroupGeneration = "stale-earlier-group" }));
                }
            };
            await Reject<InvalidDataException>(() =>
                new MapScanEngine(source, new MapDataStoreScanSink(store)).ExecuteAsync(request),
                "mismatched final native group receipt allowed SQLite Publish");
            Require(source.Calls == 1 && source.Cleanups.Count == 1,
                "cleanup failure was duplicated or skipped");
            VerifyTerminal(db, request, "failed", false);
            Require(Count(db, "map_records", "server_id=2214 AND kind='city'") == 1,
                "failed cleanup mutated foreign published rows");
        }
        finally { CleanDb(db); }
    }

    internal static async Task RunAsync()
    {
        await CheckSuccessfulSharedWorldAsync(["city", "resource"]);
        await CheckSuccessfulSharedWorldAsync(["city", "truck"]);
        await CheckEarlyAdmissionRejectAsync();
        await CheckRejectedStreamAsync("duplicate", (source, _) =>
            source.AlterBatch = (batch, value) => batch == 1
                ? value with { Captures = [value.Captures[0] with { BlockIndex = 0 }, ..value.Captures.Skip(1)] }
                : value);
        await CheckRejectedStreamAsync("missing-blocks", (source, _) => source.EndAfterBatch = 0);
        await CheckRejectedStreamAsync("stale-profile", (source, _) =>
            source.AlterBatch = (batch, value) => batch == 1
                ? value with { Owner = value.Owner with { ProfileId = "profile-old" } } : value);
        await CheckRejectedStreamAsync("stale-generation", (source, _) =>
            source.AlterBatch = (batch, value) => batch == 1
                ? value with { Owner = value.Owner with { GroupGeneration = "last-run" } } : value);
        await CheckRejectedStreamAsync("remote-server", (source, _) =>
            source.AlterBatch = (batch, value) => batch == 1
                ? value with { Owner = value.Owner with { ServerId = 2213 } } : value, useRemoteServer: true);
        await CheckRejectedStreamAsync("wrong-kind", (source, _) =>
            source.AlterBatch = (batch, value) => batch == 1
                ? value with
                {
                    Captures = [
                        value.Captures[0] with
                        {
                            Records = [value.Captures[0].Records[0] with { Kind = "truck" }]
                        },
                        ..value.Captures.Skip(1)
                    ]
                } : value);
        await CheckRejectedStreamAsync("foreign-point", (source, _) =>
            source.AlterBatch = (batch, value) => batch == 1
                ? value with
                {
                    Captures = [
                        value.Captures[0] with
                        {
                            Records = [
                                value.Captures[0].Records[0],
                                value.Captures[0].Records[1] with { PointIndex = 1 }
                            ]
                        },
                        ..value.Captures.Skip(1)
                    ]
                } : value);
        await CheckRejectedStreamAsync("zero-point", (source, _) =>
            source.AlterBatch = (batch, value) => batch == 1
                ? value with
                {
                    Captures = [
                        value.Captures[0] with
                        {
                            Records = [
                                value.Captures[0].Records[0] with { PointIndex = 0 },
                                value.Captures[0].Records[1]
                            ]
                        },
                        ..value.Captures.Skip(1)
                    ]
                } : value);
        // These were formerly silently accepted by the shared source gate.
        // The first batch is already staged: each inverse must also prove the
        // real SQLite sink discards that run without touching published rows.
        await CheckRejectedNativeIdentityAsync("null-city-point", 0,
            record => record with { PointIndex = null });
        await CheckRejectedNativeIdentityAsync("null-resource-point", 1,
            record => record with { PointIndex = null });
        await CheckRejectedNativeIdentityAsync("missing-city-ownerUid", 0,
            record => record with { DataJson = "{}" });
        await CheckRejectedNativeIdentityAsync("blank-city-ownerUid", 0,
            record => record with { DataJson = "{\"ownerUid\":\"  \"}" });
        await CheckRejectedNativeIdentityAsync("null-city-ownerUid", 0,
            record => record with { DataJson = "{\"ownerUid\":null}" });
        await CheckRejectedNativeIdentityAsync("numeric-city-ownerUid", 0,
            record => record with { DataJson = "{\"ownerUid\":1234}" });
        await CheckRejectedNativeIdentityAsync("malformed-city-ownerJson", 0,
            record => record with { DataJson = "{invalid" });
        await CheckRejectedStreamAsync("repeat-resource", (source, _) =>
            source.AlterBatch = (batch, value) => batch == 1
                ? value with
                {
                    Captures = [
                        value.Captures[0] with
                        {
                            Records = [
                                value.Captures[0].Records[0],
                                value.Captures[0].Records[1] with { RecordKey = "resource-0" }
                            ]
                        },
                        ..value.Captures.Skip(1)
                    ]
                } : value);
        await CheckRejectedStreamAsync("repeat-city-owner", (source, _) =>
            source.AlterBatch = (batch, value) =>
            {
                if (batch is not (0 or 1)) return value;
                return value with
                {
                    Captures = [
                        value.Captures[0] with
                        {
                            Records = [
                                value.Captures[0].Records[0] with
                                {
                                    DataJson = "{\"ownerUid\":\"controlled-same-owner\"}"
                                },
                                value.Captures[0].Records[1]
                            ]
                        },
                        ..value.Captures.Skip(1)
                    ]
                };
            });
        await CheckCancellationAsync(duringCleanup: false);
        await CheckCancellationAsync(duringCleanup: true);
        await CheckCleanupFailureAsync();
        Console.WriteLine("MAP014_SHARED_ACQUISITION_ENGINE_SQLITE_OK blocks=2500 tiles=0..999 kinds=city/resource,city/truck oneSource=true native=false");
    }
}
