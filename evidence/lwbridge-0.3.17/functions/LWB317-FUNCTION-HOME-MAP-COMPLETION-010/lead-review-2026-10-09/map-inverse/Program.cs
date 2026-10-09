using System.Text.Json;
using LWBridge.Desktop;
using LWBridge.Map317;

static JsonElement J(object? value) => JsonSerializer.SerializeToElement(value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
static string? String(JsonElement v, string p) => v.TryGetProperty(p, out var x) && x.ValueKind == JsonValueKind.String ? x.GetString() : null;
static bool Bool(JsonElement v, string p) => v.TryGetProperty(p, out var x) && x.ValueKind == JsonValueKind.True;
var root = Path.Combine(Path.GetTempPath(), "lwb317-lead010-map-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var results = new List<object>();
try
{
    string db = Path.Combine(root, "map.db");
    var provider = new HeldProvider();
    using var service = new Map317CommandService(db, provider, UnavailableMapActionProvider.Instance, startPlunderWorkers: false);
    var startPayload = J(new { selectedTypes = new[] { "city" }, scanMode = "normal" });
    var empty = J(new { });
    await service.InvokeAsync("map_scan_start", startPayload, CancellationToken.None);
    var oldReading = (MapScanState)service.CreateStatus();
    await service.InvokeAsync("map_scan_stop", empty, CancellationToken.None);
    var cancelled = J(service.CreateStatus());
    using (var read = new MapStore(db))
    {
        // The exact completion expression currently used in Completion010LivePilot.
        bool runnerCompleted = String(cancelled, "status") == "completed" || !Bool(cancelled, "isReading");
        results.Add(new { id = "LEAD010-02-terminal", proof = "actual stopped scan state plus current runner expression; runner itself not launched", durableStatus = read.ReadScanRun(oldReading.ScanRunId)?.Status, runnerCompleted, expectedRunnerCompleted = false, mismatch = runnerCompleted });
    }
    provider.Server = 318;
    await service.InvokeAsync("map_scan_start", startPayload, CancellationToken.None);
    var successor = (MapScanState)service.CreateStatus();
    int stopsBefore = provider.StopCalls;
    // Simulates release of an old async getMapStatus reply after Stop/new Start.
    var result = await service.ApplyLiveServerGuardAsync(oldReading, true, 318, CancellationToken.None);
    using (var read = new MapStore(db))
        results.Add(new { id = "LEAD010-01", proof = "actual production guard/control/provider/SQLite; controlled late status observation", oldRun = oldReading.ScanRunId, successorRun = successor.ScanRunId, successorServer = successor.ServerId, liveServer = 318, successorStillReading = result.IsReading, successorDurableStatus = read.ReadScanRun(successor.ScanRunId)?.Status, additionalProviderStops = provider.StopCalls - stopsBefore, expectedSuccessorStillReading = true, mismatch = !result.IsReading });
    try
    {
        service.PrepareCityExport(J(new { query = new { serverId = 318, page = 1, pageSize = 50 }, headers = new[] { "Name", "Alliance", "Level", "Power", "X", "Y", "Shield", "Updated" } }));
        results.Add(new { id = "LEAD010-02-export", unexpectedSuccess = true });
    }
    catch (LWBridge.Desktop.BridgeCommandException ex)
    {
        results.Add(new { id = "LEAD010-02-export", proof = "exact runner payload through actual production PrepareCityExport; no game/export dialog", code = ex.Code, message = ex.Message, runnerHeaderCount = 8, requiredCount = 12, mismatch = true });
    }
}
finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); Directory.Delete(root, recursive: true); }
var record = new { checkpoint = "76018780d017321cd7fa2a819392c97c1e2888f0", realGameLaunches = 0, desktopInputOrCapture = false, isolatedRootRemoved = !Directory.Exists(root), cases = results };
File.WriteAllText(args[0], JsonSerializer.Serialize(record));
Console.WriteLine(JsonSerializer.Serialize(record));

sealed class HeldProvider : IMap317RunScopedProvider
{
    public int Server { get; set; } = 317;
    public int StopCalls { get; private set; }
    private string? active;
    public event Action<string>? RunTerminated;
    public ValueTask<MapProviderContext> GetContextAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(new MapProviderContext(true, true, Server, "live", 1, 100, 100, 1));
    public ValueTask<MapProviderContext> EnterWorldMapAsync(CancellationToken cancellationToken = default) => GetContextAsync(cancellationToken);
    public ValueTask<MapProviderStartResult> StartMapScanAsync(MapProviderStartRequest request, CancellationToken cancellationToken = default) => ValueTask.FromResult(new MapProviderStartResult(true, 1, true));
    public void ActivateAcceptedRun(MapControlPlane control, string scanRunId) => active = scanRunId;
    public ValueTask StopMapScanAsync(CancellationToken cancellationToken = default)
    {
        StopCalls++;
        if (active is { } run) { active = null; RunTerminated?.Invoke(run); }
        return ValueTask.CompletedTask;
    }
    public void Dispose() { if (active is { } run) { active = null; RunTerminated?.Invoke(run); } }
}
