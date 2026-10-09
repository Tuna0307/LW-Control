using System.Text.Json;
using LWBridge.Desktop;
using LWBridge.Map317;

static JsonElement J(object? value) => JsonSerializer.SerializeToElement(value, JsonOptions.Default);
var root = Path.Combine(Path.GetTempPath(), "lwb317-lead010r1-probe-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var cases = new List<object>();
try
{
    string db = Path.Combine(root, "map.db");
    using (var records = new MapStore(db))
    {
        foreach (int? level in new int?[] { null, 0, 1, 3 })
        {
            string key = level?.ToString() ?? "null";
            records.UpsertRecord(new MapRecord("city", 317, key, null, key, key, null,
                level, null, null, null, null, 1, J(new { uuid = key, level }).GetRawText()));
        }
    }
    var provider = new HeldProvider();
    using var service = new Map317CommandService(db, provider,
        UnavailableMapActionProvider.Instance, false,
        _ => Task.FromResult(new CurrentClientMapStatusContext(false, 318, 317, [], [], 1, 100, 100, 0, 0)));
    var empty = J(new {});
    foreach (var (id, query, expected) in new (string, object, int)[]
    {
        ("negative-upper-level", new { serverId = 317, maxLevel = -1 }, 0),
        ("zero-lower-level-excludes-null", new { serverId = 317, minLevel = 0 }, 3),
        ("fractional-level-range", new { serverId = 317, minLevel = 0.5, maxLevel = 1.5 }, 1),
        ("inverted-level-range", new { serverId = 317, minLevel = 3, maxLevel = 1 }, 2),
    })
    {
        var reply = J(await service.InvokeAsync("map_search", J(new { kind = "city", query }), CancellationToken.None));
        int total = reply.GetProperty("total").GetInt32();
        cases.Add(new { id, expected, actual = total, mismatch = total != expected,
            basis = "recovered native loose f64 comparisons on indexed integer level, with swap; original runtime not executed" });
    }
    var huge = J(await service.InvokeAsync("map_search", J(new { kind = "city", query = new { serverId = 317, page = 3000000000L }}), CancellationToken.None));
    cases.Add(new { id = "i64-page-envelope", expected = 3000000000L,
        actual = huge.GetProperty("page").GetInt64(), mismatch = huge.GetProperty("page").GetInt64() != 3000000000L,
        basis = "original 0x3E1144 clamps minimum only; recovered i64 page" });
    var start = (MapScanState)(await service.InvokeAsync("map_scan_start",
        J(new { selectedTypes = new[] { "city" }, scanMode = "normal" }), CancellationToken.None))!;
    var outsideWorld = (MapScanState)(await service.InvokeAsync("map_scan_status", empty, CancellationToken.None))!;
    cases.Add(new { id = "reading-outside-world-server", expected = 317, actual = outsideWorld.ServerId,
        mismatch = outsideWorld.ServerId != 317, outsideWorld.IsReading,
        basis = "original status 0xF9567 keeps captured server while reading outside world" });
    await service.InvokeAsync("map_scan_stop", empty, CancellationToken.None);
    provider.Server = 318;
    var successor = (MapScanState)(await service.InvokeAsync("map_scan_start",
        J(new { selectedTypes = new[] { "city" }, scanMode = "normal" }), CancellationToken.None))!;
    int stops = provider.Stops;
    var guarded = await service.ApplyLiveServerGuardAsync(start, true, 318, CancellationToken.None);
    using (var records = new MapStore(db))
        cases.Add(new { id = "LEAD010-01-successor", mismatch = !guarded.IsReading || provider.Stops != stops,
            guarded.IsReading, stopsAdded = provider.Stops - stops,
            durableStatus = records.ReadScanRun(successor.ScanRunId)?.Status });
    await service.InvokeAsync("map_scan_stop", empty, CancellationToken.None);
}
finally
{
    Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
    Directory.Delete(root, recursive: true);
}
var report = new { checkpoint = "faec6f61c3fe7ddd529d5dca0155512d2c439899", gameLaunches = 0,
    desktopInputOrCapture = false, isolatedRootRemoved = !Directory.Exists(root), cases };
File.WriteAllText(args[0], JsonSerializer.Serialize(report));
Console.WriteLine(JsonSerializer.Serialize(report));

sealed class HeldProvider : IMap317RunScopedProvider
{
    public int Server { get; set; } = 317;
    public int Stops { get; private set; }
    private string? active;
    public event Action<string>? RunTerminated;
    public ValueTask<MapProviderContext> GetContextAsync(CancellationToken token = default) =>
        ValueTask.FromResult(new MapProviderContext(true, true, Server, "live", 1, 100, 100, 1));
    public ValueTask<MapProviderContext> EnterWorldMapAsync(CancellationToken token = default) => GetContextAsync(token);
    public ValueTask<MapProviderStartResult> StartMapScanAsync(MapProviderStartRequest request, CancellationToken token = default) =>
        ValueTask.FromResult(new MapProviderStartResult(true, 1, true));
    public void ActivateAcceptedRun(MapControlPlane control, string scanRunId) => active = scanRunId;
    public ValueTask StopMapScanAsync(CancellationToken token = default)
    { Stops++; Dispose(); return ValueTask.CompletedTask; }
    public void Dispose() { if (active is { } run) { active = null; RunTerminated?.Invoke(run); } }
}
