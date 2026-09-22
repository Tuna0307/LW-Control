using System.Runtime.CompilerServices;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class PlayerMarkRelocationChecks
{
    [ModuleInitializer]
    internal static void Run() => MarkSurvivesRescanRestartAndRelocates().GetAwaiter().GetResult();

    private static async Task MarkSurvivesRescanRestartAndRelocates()
    {
        const int serverId = 2212;
        const string ownerUid = "12345678901234567890";
        const int oldX = 111;
        const int oldY = 222;
        const int movedX = 333;
        const int movedY = 444;

        string path = Path.Combine(
            Path.GetTempPath(),
            "lwbridge-mark-relocate-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            using (var store = new MapDataStore(path))
            {
                MapStoredRecord oldRow = City(
                    serverId, "city-old-position", 101, ownerUid, oldX, oldY, 1_000);
                store.UpsertRecord(oldRow);

                var backend = new LWBridgeBackend(
                    new LocalConfigStore(persistent: false),
                    mapData: store);
                JsonElement markPayload = JsonSerializer.SerializeToElement(new
                {
                    profileId = backend.ProfileId,
                    row = new
                    {
                        serverId,
                        ownerUid,
                        ownerName = "Marked Mover",
                        x = oldX,
                        y = oldY,
                    },
                    marked = true,
                }, JsonOptions.Default);
                JsonElement marked = JsonSerializer.SerializeToElement(
                    await backend.InvokeAsync(
                        "map_player_mark_set",
                        markPayload,
                        CancellationToken.None),
                    JsonOptions.Default);
                Check(marked.GetProperty("marked").GetBoolean() &&
                      store.GetPlayerMark(serverId, ownerUid) is not null,
                    "public mark command must persist recovered server/owner identity before rescan");

                var request = new MapScanExecutionRequest(
                    "mark-relocate-rescan",
                    serverId,
                    0,
                    20,
                    20,
                    ["city"],
                    8,
                    2,
                    ScanMode: "normal",
                    LaunchSessionId: "mark-relocate-session");
                MapScanTargetBlock block = MapScanTraversal.Build(20, 20).Single();
                MapStoredRecord movedRow = City(
                    serverId, "city-new-position", 202, ownerUid, movedX, movedY, 2_000);
                var sink = new MapDataStoreScanSink(store);
                sink.Begin(request, 1, 1_100);
                sink.CheckpointSuccess(
                    request,
                    block,
                    new MapScanBlockCapture(
                        serverId, 0, block.BlockIndex, "{}", [movedRow]),
                    1,
                    1_200);
                sink.Publish(request, 1_300);

                Check(store.GetRecord("city", serverId, oldRow.RecordKey) is null &&
                      store.GetRecord("city", serverId, movedRow.RecordKey) is not null,
                    "transactional City rescan must replace the old positional row with the moved row");
                Check(store.GetPlayerMark(serverId, ownerUid) is not null,
                    "transactional City publication must preserve mark identity outside map_records");
            }

            // Reopen the real file-backed store to model application restart.
            using (var reopened = new MapDataStore(path))
            {
                var coordinateJump = new RecordingCoordinateJumpService();
                var backend = new LWBridgeBackend(
                    new LocalConfigStore(persistent: false),
                    asyncCommands: coordinateJump,
                    mapData: reopened);
                JsonElement query = JsonSerializer.SerializeToElement(new
                {
                    profileId = backend.ProfileId,
                    kind = "city",
                    query = new
                    {
                        serverId,
                        markedOnly = true,
                        page = 1,
                        pageSize = 50,
                        sorts = new[]
                        {
                            new { sortBy = "updatedAt", sortOrder = "desc" },
                        },
                    },
                }, JsonOptions.Default);
                JsonElement result = JsonSerializer.SerializeToElement(
                    await backend.InvokeAsync("map_search", query, CancellationToken.None),
                    JsonOptions.Default);
                Check(result.GetProperty("total").GetInt32() == 1,
                    "markedOnly after restart must resolve exactly the moved City by stable owner identity");
                JsonElement row = result.GetProperty("rows")[0];
                Check(row.GetProperty("ownerUid").GetString() == ownerUid &&
                      row.GetProperty("serverId").GetInt32() == serverId &&
                      row.GetProperty("x").GetInt32() == movedX &&
                      row.GetProperty("y").GetInt32() == movedY &&
                      row.GetProperty("marked").GetBoolean(),
                    "reopened marked City must expose the new indexed coordinates and marked overlay");

                JsonElement relocatePayload = JsonSerializer.SerializeToElement(new
                {
                    serverId = row.GetProperty("serverId").GetInt32(),
                    x = row.GetProperty("x").GetInt32(),
                    y = row.GetProperty("y").GetInt32(),
                }, JsonOptions.Default);
                Check(relocatePayload.GetProperty("serverId").GetInt32() == serverId &&
                      relocatePayload.GetProperty("x").GetInt32() == movedX &&
                      relocatePayload.GetProperty("y").GetInt32() == movedY,
                    "relocate payload derived from the reopened marked row must target the moved coordinates, never the marked snapshot coordinates");

                JsonElement relocateResult = JsonSerializer.SerializeToElement(
                    await backend.InvokeAsync(
                        "map_coordinate_jump",
                        relocatePayload,
                        CancellationToken.None),
                    JsonOptions.Default);
                Check(coordinateJump.Calls == 1 &&
                      coordinateJump.ServerId == serverId &&
                      coordinateJump.X == movedX &&
                      coordinateJump.Y == movedY &&
                      relocateResult.GetProperty("serverId").GetInt32() == serverId &&
                      relocateResult.GetProperty("x").GetInt32() == movedX &&
                      relocateResult.GetProperty("y").GetInt32() == movedY,
                    "public map_coordinate_jump routing after restart must receive and return the moved indexed coordinates");

                JsonElement unmarkPayload = JsonSerializer.SerializeToElement(new
                {
                    profileId = backend.ProfileId,
                    row = new { serverId, ownerUid },
                    marked = false,
                }, JsonOptions.Default);
                _ = await backend.InvokeAsync(
                    "map_player_mark_set",
                    unmarkPayload,
                    CancellationToken.None);
                Check(reopened.GetPlayerMark(serverId, ownerUid) is null,
                    "public unmark must delete the stable server/owner mark after relocation");
            }

            using (var reopenedAfterUnmark = new MapDataStore(path))
            {
                var backend = new LWBridgeBackend(
                    new LocalConfigStore(persistent: false),
                    mapData: reopenedAfterUnmark);
                JsonElement query = JsonSerializer.SerializeToElement(new
                {
                    profileId = backend.ProfileId,
                    kind = "city",
                    query = new
                    {
                        serverId,
                        markedOnly = true,
                        page = 1,
                        pageSize = 50,
                        sorts = new[]
                        {
                            new { sortBy = "updatedAt", sortOrder = "desc" },
                        },
                    },
                }, JsonOptions.Default);
                JsonElement result = JsonSerializer.SerializeToElement(
                    await backend.InvokeAsync("map_search", query, CancellationToken.None),
                    JsonOptions.Default);
                Check(result.GetProperty("total").GetInt32() == 0,
                    "unmark must remain durable across restart while the moved City itself stays published");
                Check(reopenedAfterUnmark.GetRecord("city", serverId, "city-new-position") is not null,
                    "unmark must not delete or revert the relocated City row");
            }
        }
        finally
        {
            foreach (string candidate in new[]
            {
                path, path + "-wal", path + "-shm", path + ".scan-owner.lock"
            })
            {
                try { File.Delete(candidate); } catch { }
            }
        }
    }

    private static MapStoredRecord City(
        int serverId,
        string recordKey,
        int pointIndex,
        string ownerUid,
        int x,
        int y,
        long updatedAt) =>
        new(
            "city",
            serverId,
            recordKey,
            pointIndex,
            ownerUid,
            "Marked Mover",
            "MVR",
            30,
            null,
            null,
            null,
            null,
            updatedAt,
            JsonSerializer.Serialize(new
            {
                serverId,
                ownerUid,
                uuid = ownerUid,
                ownerName = "Marked Mover",
                allianceName = "MVR",
                level = 30,
                x,
                y,
                updatedAt,
            }, JsonOptions.Default));

    private sealed class RecordingCoordinateJumpService : INativeAsyncCommandService
    {
        public int Calls { get; private set; }
        public int ServerId { get; private set; }
        public int X { get; private set; }
        public int Y { get; private set; }

        public bool CanHandle(string command) =>
            string.Equals(command, "map_coordinate_jump", StringComparison.Ordinal);

        public Task<object?> InvokeAsync(
            string command,
            JsonElement payload,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!CanHandle(command))
                throw new InvalidOperationException("unexpected relocation command: " + command);

            Calls++;
            ServerId = payload.GetProperty("serverId").GetInt32();
            X = payload.GetProperty("x").GetInt32();
            Y = payload.GetProperty("y").GetInt32();
            return Task.FromResult<object?>(new { serverId = ServerId, x = X, y = Y });
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(
            "C04 mark/rescan/restart/relocate check failed: " + message);
    }
}
