using System.Diagnostics;
using System.Text.Json;
using LWBridge.Desktop;
using Map317 = LWBridge.Map317;

// Explicit diagnostic host: actual production native source, command plane,
// engine and SQLite, no fake rows/providers. Own session is independently
// supplied by the admitted helper; this is not a packaged WebView/pipe proof.
if (args.Length != 2) throw new ArgumentException("owner.json scenario (city|mixed|other|treasure-other|stop|march-source)");
var ownerPath = Path.GetFullPath(args[0]);
using var document = JsonDocument.Parse(File.ReadAllText(ownerPath));
var owner = document.RootElement;
var root = Path.GetDirectoryName(ownerPath)!;
var runtime = Path.Combine(root, "overview-bridge");
var probe = Path.Combine(root, "live-resource");
var scenario = args[1];
bool benchmarking = scenario.StartsWith("benchmark-", StringComparison.Ordinal);
if (!benchmarking && scenario is not ("city" or "mixed" or "other" or "treasure-other" or "stop" or "march-source" or "lens-matrix" or "terrain-probe" or "lod-snapshot" or "direct-aoi")) throw new ArgumentException("Unknown scenario");
string Text(string field) => owner.GetProperty(field).GetString()!;
var session = new OverviewMapScanSession(Text("profileId"), Text("sessionId"), Text("challenge"),
    owner.GetProperty("gamePid").GetInt32(), Text("gamePath"), Text("gameStartedAtUtc"));
bool OwnProcessMatches()
{
    try
    {
        using var process = Process.GetProcessById(session.GamePid);
        if (process.HasExited || !string.Equals(process.MainModule?.FileName, session.GamePath, StringComparison.OrdinalIgnoreCase) ||
            Math.Abs((process.StartTime.ToUniversalTime() - DateTime.Parse(session.GameStartedAtUtc).ToUniversalTime()).TotalMilliseconds) > 5)
            return false;
        return true;
    }
    catch (Exception e) when (e is ArgumentException or System.ComponentModel.Win32Exception or InvalidOperationException) { return false; }
}
long lastVerifiedHeartbeat=0;
OverviewMapScanSession? ReadSession()
{
    if(!OwnProcessMatches()) return null;
    try
    {
        using var h = JsonDocument.Parse(File.ReadAllText(Path.Combine(runtime,"heartbeat.json")));
        if(!OverviewLifecycleService.HeartbeatMatches(h.RootElement,session.ProfileId,session.SessionId,
            session.Challenge,session.GamePid,DateTimeOffset.UtcNow.ToUnixTimeSeconds())) return null;
        lastVerifiedHeartbeat=Stopwatch.GetTimestamp();
        return session;
    }
    catch (Exception e) when (e is IOException or JsonException)
    {
        // Native JSON writes can briefly expose an incomplete heartbeat. The
        // exact live process and last verified fresh heartbeat retain host
        // admission for at most five seconds; game-side ownership is unchanged.
        return lastVerifiedHeartbeat!=0 && Stopwatch.GetElapsedTime(lastVerifiedHeartbeat)<TimeSpan.FromSeconds(5) ? session : null;
    }
}
// Diagnostic A/B only: LWBRIDGE_MAP014_COVERAGE_ZOOM=off sends no canonical lens height; a number simulates that lens.
string? zoomSwitch = Environment.GetEnvironmentVariable("LWBRIDGE_MAP014_COVERAGE_ZOOM");
bool zoomOff = string.Equals(zoomSwitch, "off", StringComparison.OrdinalIgnoreCase);
double? zoomOverride = double.TryParse(zoomSwitch, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double zo) ? zo : null;
CurrentClientMapBlockSource MakeSource(bool bounded, bool forceLegacy=false, bool marchProbe=false, double? zoomArg=null, int? fovArg=null) => new(ReadSession,runtime,probe,
    hooks: marchProbe ? new CurrentClientMapBlockSourceHooks { ObserveMarchFreshness = true, DisableCoverageZoomNormalization = zoomOff, CoverageZoomOverride = zoomOverride }
        : bounded ? new CurrentClientMapBlockSourceHooks { ExperimentalBoundedCameraLens = true, DisableCoverageZoomNormalization = zoomOff, CoverageZoomOverride = zoomOverride }
        : forceLegacy ? new CurrentClientMapBlockSourceHooks { ForceLegacyFullWorldLens = true, DisableCoverageZoomNormalization = zoomOff, CoverageZoomOverride = zoomArg ?? zoomOverride, CoverageFovOverride = fovArg } : null,
    waitForHealthySession: async (expected, token) =>
    {
        var deadline=Stopwatch.StartNew();
        while(deadline.Elapsed<TimeSpan.FromSeconds(180))
        {
            token.ThrowIfCancellationRequested();
            if(ReadSession()!=expected) throw new InvalidOperationException("Own native session retired during healthy admission");
            try
            {
                using var heartbeat=JsonDocument.Parse(File.ReadAllText(Path.Combine(runtime,"heartbeat.json")));
                var h=heartbeat.RootElement;
                bool IsTrue(string key)=>h.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.True;
                if(IsTrue("gameStateObserved")&&IsTrue("gameReady")&&IsTrue("loggedIn")&&IsTrue("connected")) return;
            }
            catch(IOException) {} catch(JsonException) {}
            await Task.Delay(250,token);
        }
        throw new TimeoutException("Genuine native game did not become healthy");
    }, matchesOwnedSession: expected => expected==session && OwnProcessMatches(), assetCacheRoot: Path.Combine(root,"asset-cache"));
var source = MakeSource(Environment.GetEnvironmentVariable("LWBRIDGE_MAP014_BOUNDED_LENS") == "1",
    marchProbe: scenario == "march-source");
using var commands = new Map317CommandService(Path.Combine(root,"map-014.db"),
    new CurrentClientMap317ScanProvider(source), new CurrentClientMap317ActionProvider(source), false,
    source.GetMapStatusContextAsync);
using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(benchmarking ? 30 : 12));
async Task<JsonElement> Invoke(string command, object payload)
{
    var value = await commands.InvokeAsync(command, JsonSerializer.SerializeToElement(payload,JsonOptions.Default),cts.Token);
    return JsonSerializer.SerializeToElement(value,JsonOptions.Default);
}
async Task<JsonElement> Camera(string label)
{
    var id = "map014camera" + Guid.NewGuid().ToString("N");
    var path = Path.Combine(probe,"runtime-diagnostic.txt");
    if (File.Exists(path)) throw new InvalidOperationException("Another native diagnostic is pending");
    var lines = new[] { "schema=1","probeVersion=lwbridge-live-resource-probe-2",$"requestId={id}",
        $"profileId={session.ProfileId}",$"launchSessionId={session.SessionId}",$"challenge={session.Challenge}",
        $"gamePid={session.GamePid}","mode=camera_checkpoint","" };
    var temporary=path+"."+id;
    File.WriteAllLines(temporary,lines); File.Move(temporary,path);
    var deadline=Stopwatch.StartNew();
    while (deadline.Elapsed<TimeSpan.FromSeconds(15))
    {
        cts.Token.ThrowIfCancellationRequested();
        if (ReadSession() is null) throw new InvalidOperationException("Own native session retired");
        try
        {
            using var result=JsonDocument.Parse(File.ReadAllText(Path.Combine(probe,"runtime-diagnostic-result.json")));
            var r=result.RootElement;
            if (r.GetProperty("requestId").GetString()==id)
            {
                if (r.GetProperty("profileId").GetString()!=session.ProfileId ||
                    r.GetProperty("launchSessionId").GetString()!=session.SessionId ||
                    r.GetProperty("challenge").GetString()!=session.Challenge ||
                    r.GetProperty("gamePid").GetInt32()!=session.GamePid)
                    throw new InvalidDataException("Foreign camera reply");
                File.WriteAllText(Path.Combine(root,label+"-camera.json"),r.GetRawText());
                if (r.GetProperty("state").GetString()!="proven") throw new InvalidDataException("Camera checkpoint failed: "+r.GetRawText());
                return r.Clone();
            }
        }
        catch(IOException) {} catch(JsonException) {}
        await Task.Delay(100,cts.Token);
    }
    throw new TimeoutException("Independent native camera checkpoint timed out");
}
async Task<JsonElement> ZoomFixture(string mode, double zoom)
{
    // Harness-only (see map014_zoom_fixture.lua): reproduce a zoomed-in owner camera and restore it.
    var id = "map014zoom" + Guid.NewGuid().ToString("N");
    var path = Path.Combine(probe,"map014-zoom-fixture.txt");
    var resultPath = Path.Combine(probe,"map014-zoom-fixture-result.json");
    var lines = new[] { $"requestId={id}",$"profileId={session.ProfileId}",$"launchSessionId={session.SessionId}",
        $"challenge={session.Challenge}",$"gamePid={session.GamePid}",$"mode={mode}",$"zoom={zoom.ToString(System.Globalization.CultureInfo.InvariantCulture)}","" };
    var temporary=path+"."+id;
    File.WriteAllLines(temporary,lines); File.Move(temporary,path);
    var deadline=Stopwatch.StartNew();
    while (deadline.Elapsed<TimeSpan.FromSeconds(15))
    {
        cts.Token.ThrowIfCancellationRequested();
        if (ReadSession() is null) throw new InvalidOperationException("Own native session retired");
        try
        {
            using var result=JsonDocument.Parse(File.ReadAllText(resultPath));
            if (result.RootElement.GetProperty("requestId").GetString()==id)
            {
                File.WriteAllText(Path.Combine(root,$"zoom-fixture-{mode}.json"),result.RootElement.GetRawText());
                if (!result.RootElement.GetProperty("ok").GetBoolean()) throw new InvalidDataException("Zoom fixture failed: "+result.RootElement.GetRawText());
                return result.RootElement.Clone();
            }
        }
        catch(IOException) {} catch(JsonException) {}
        await Task.Delay(100,cts.Token);
    }
    throw new TimeoutException("Zoom fixture did not answer");
}
var history=new List<object>();
JsonElement before=default, after=default, terminal=default;
if (scenario == "direct-aoi")
{
    // Camera-free SendAoiRequest experiment (harness-only fixture map014_direct_fixture.lua).
    var ctx = await source.GetCurrentContextAsync(cts.Token);
    int hx = ctx.PlayerTileX ?? 500, hy = ctx.PlayerTileY ?? 500;
    // Enter the World map at home first (same as the benchmarks), then let the scene settle.
    await source.JumpToCoordinateAsync(ctx.ServerId, hx, hy, cts.Token);
    await Task.Delay(3000, cts.Token);
    static int[] Areas(int col0, int row0, int cols, int rows) =>
        Enumerable.Range(row0, rows).SelectMany(r => Enumerable.Range(col0, cols).Select(c => r * 100 + c)).ToArray();
    static int TileIndex(int x, int y) => y * 1000 + x + 1;
    var cases = new List<(string Name, int[] Areas, bool WholeWorldRect)>
    {
        ("home-3x3-world-rect", Areas(Math.Clamp(hx / 10 - 1, 0, 97), Math.Clamp(hy / 10 - 1, 0, 97), 3, 3), true),
        ("far-250-750-3x3-exact-rect", Areas(24, 74, 3, 3), false),
        ("far-750-750-3x3-world-rect", Areas(74, 74, 3, 3), true),
        ("far-250-250-160-exact-rect", Areas(17, 20, 16, 10), false),
        ("far-750-250-160-world-rect", Areas(67, 20, 16, 10), true),
    };
    var results = new List<object>();
    foreach (var (name, areas, wholeWorld) in cases)
    {
        int minC = areas.Min(a => a % 100), maxC = areas.Max(a => a % 100), minR = areas.Min(a => a / 100), maxR = areas.Max(a => a / 100);
        int lb = wholeWorld ? 1 : TileIndex(minC * 10, minR * 10), rt = wholeWorld ? 1000000 : TileIndex(maxC * 10 + 9, maxR * 10 + 9);
        int cx = (minC + maxC + 1) * 5, cy = (minR + maxR + 1) * 5;
        var id = "direct" + Guid.NewGuid().ToString("N");
        var cmdPath = Path.Combine(probe, "map014-direct-fixture.txt");
        var resPath = Path.Combine(probe, "map014-direct-fixture-result.json");
        var lines = new[] { "requestId=" + id, "profileId=" + session.ProfileId, "launchSessionId=" + session.SessionId, "challenge=" + session.Challenge,
            "gamePid=" + session.GamePid, "indices=" + string.Join(",", areas), "lb=" + lb, "rt=" + rt, "x=" + cx, "y=" + cy, "" };
        var tmp = cmdPath + "." + id; File.WriteAllLines(tmp, lines); File.Move(tmp, cmdPath);
        var wait = Stopwatch.StartNew(); JsonElement? answer = null;
        while (wait.Elapsed < TimeSpan.FromSeconds(15) && answer is null)
        {
            try { using var doc = JsonDocument.Parse(File.ReadAllText(resPath)); if (doc.RootElement.GetProperty("requestId").GetString() == id) answer = doc.RootElement.Clone(); }
            catch (IOException) { } catch (JsonException) { }
            if (answer is null) await Task.Delay(50, cts.Token);
        }
        results.Add(new { name, areaCount = areas.Length, lb, rt, x = cx, y = cy, result = answer });
        Console.WriteLine($"MAP014_DIRECT_AOI {name} {(answer?.GetRawText() ?? "timeout").Substring(0, Math.Min(400, (answer?.GetRawText() ?? "timeout").Length))}");
        await Task.Delay(2000, cts.Token);
    }
    File.WriteAllText(Path.Combine(root, "direct-aoi.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions(JsonOptions.Default) { WriteIndented = true }));
    return;
}
if (scenario == "lod-snapshot")
{
    // Diagnostic: one coarse whole-world (zoom/LOD2) request with every kind enabled; report what came back.
    var ctx = await source.GetCurrentContextAsync(cts.Token);
    int hx = ctx.PlayerTileX ?? 500, hy = ctx.PlayerTileY ?? 500;
    var results = new List<object>();
    foreach (var (tx, ty) in new[] { (hx, hy), (505, 505), (250, 750) })
    {
        var id = "lodsnap" + Guid.NewGuid().ToString("N");
        var cmdPath = Path.Combine(probe, "bulk-aoi-diagnostic.txt");
        var resPath = Path.Combine(probe, "bulk-aoi-diagnostic-result.json");
        var cmd = new[] { "schema=1", "probeVersion=lwbridge-live-resource-probe-2", "requestId=" + id,
            "profileId=" + session.ProfileId, "launchSessionId=" + session.SessionId, "challenge=" + session.Challenge,
            "gamePid=" + session.GamePid, "serverId=" + ctx.ServerId, "scanRunId=" + id, "viewLevel=-1", "requestMode=zoom",
            "targetTileX=" + tx, "targetTileY=" + ty, "requestedCount=160", "holdMilliseconds=0",
            "homeTileX=" + hx, "homeTileY=" + hy, "includeCity=true", "includeResource=true", "includeMonster=true",
            "includeMonsterProtection=false", "includeTrain=true", "includeDispatch=true", "includeGhost=true",
            "includeTreasure=false", "includeResourceDetails=false", "" };
        var tmp = cmdPath + "." + id; File.WriteAllLines(tmp, cmd); File.Move(tmp, cmdPath);
        var wait = Stopwatch.StartNew(); JsonElement? answer = null;
        while (wait.Elapsed < TimeSpan.FromSeconds(30) && answer is null)
        {
            try { using var doc = JsonDocument.Parse(File.ReadAllText(resPath)); if (doc.RootElement.GetProperty("requestId").GetString() == id) answer = doc.RootElement.Clone(); }
            catch (IOException) { } catch (JsonException) { }
            if (answer is null) await Task.Delay(50, cts.Token);
        }
        double seconds = wait.Elapsed.TotalSeconds;
        var summary = new Dictionary<string, object?> { ["target"] = new[] { tx, ty }, ["wallSeconds"] = seconds };
        if (answer is JsonElement r)
            foreach (var prop in r.EnumerateObject())
                summary[prop.Name] = prop.Value.ValueKind == JsonValueKind.Array ? $"array[{prop.Value.GetArrayLength()}]"
                    : prop.Value.ValueKind == JsonValueKind.Object ? "object" : prop.Value.ToString();
        else summary["timeout"] = true;
        results.Add(summary);
        File.WriteAllText(Path.Combine(root, "lod-snapshot-" + results.Count + ".json"), answer?.GetRawText() ?? "null");
        await Task.Delay(4000, cts.Token);
    }
    File.WriteAllText(Path.Combine(root, "lod-snapshot.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions(JsonOptions.Default) { WriteIndented = true }));
    Console.WriteLine("MAP014_LOD_SNAPSHOT_DONE");
    return;
}
if (scenario == "terrain-probe")
{
    // Read-only: evaluates every candidate terrain predicate on the same tiles (harness-only Lua fixture).
    var ctx = await source.GetCurrentContextAsync(cts.Token);
    var tiles = new List<(int X, int Y, string Group, int Level)>();
    void Add(int x, int y, string group, int level = 0) { if (x is >= 0 and < 1000 && y is >= 0 and < 1000 && !tiles.Any(t => t.X == x && t.Y == y)) tiles.Add((x, y, group, level)); }
    string dbPath = Environment.GetEnvironmentVariable("LWBRIDGE_MAP014_TERRAIN_DB") ?? "";
    if (File.Exists(dbPath))
    {
        using var sqlite = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=" + dbPath + ";Mode=ReadOnly");
        sqlite.Open();
        using var query = sqlite.CreateCommand();
        query.CommandText = "select json_extract(data_json,'$.x'), json_extract(data_json,'$.y'), json_extract(data_json,'$.level') from map_records where kind='resource' and server_id=$s";
        query.Parameters.AddWithValue("$s", ctx.ServerId);
        var rows = new List<(int, int, int)>();
        using (var reader = query.ExecuteReader()) while (reader.Read()) rows.Add((reader.GetInt32(0), reader.GetInt32(1), reader.IsDBNull(2) ? 0 : reader.GetInt32(2)));
        foreach (var (x, y, level) in rows.Where(r => Math.Abs(r.Item1 - 398) <= 25 && Math.Abs(r.Item2 - 483) <= 25)) Add(x, y, "owner-area-resource", level);
        int stride = Math.Max(1, rows.Count / 60);
        for (int i = 0; i < rows.Count; i += stride) Add(rows[i].Item1, rows[i].Item2, "sample-resource", rows[i].Item3);
    }
    Add(398, 483, "owner-point");
    for (int x = 374; x <= 422; x += 8) for (int y = 459; y <= 507; y += 8) Add(x, y, "owner-lattice");
    foreach (int x in new[] { 0, 50, 100, 150, 200, 250, 300, 350, 398, 450, 500, 550, 600, 650, 700, 750, 800, 850, 900, 950, 999 }) Add(x, 483, "transect-y483");
    foreach (int y in new[] { 0, 50, 100, 150, 200, 250, 300, 350, 400, 483, 550, 600, 650, 700, 750, 800, 850, 900, 950, 999 }) Add(398, y, "transect-x398");
    foreach (var (x, y) in new[] { (50, 50), (950, 950), (50, 950), (950, 50), (250, 250), (750, 750) }) Add(x, y, "far-control");
    foreach (var (x, y) in new[] { (450, 450), (449, 450), (450, 449), (549, 549), (550, 549), (549, 550), (500, 500), (449, 500), (550, 500) }) Add(x, y, "black-land-boundary");
    if (ctx.PlayerTileX is int hx2 && ctx.PlayerTileY is int hy2) foreach (int d in new[] { 0, 1, 5, 15, 30 }) { Add(hx2 + d, hy2, "home-heat"); Add(hx2 - d, hy2, "home-heat"); }
    var passes = new List<object>();
    for (int pass = 0; pass < 2; pass++)
    {
        if (pass == 1) await Task.Delay(TimeSpan.FromSeconds(60), cts.Token);
        var id = "terrain" + Guid.NewGuid().ToString("N");
        var cmdPath = Path.Combine(probe, "map014-terrain-fixture.txt");
        var resPath = Path.Combine(probe, "map014-terrain-fixture-result.json");
        var lines = new[] { "requestId=" + id, "profileId=" + session.ProfileId, "launchSessionId=" + session.SessionId, "challenge=" + session.Challenge,
            "gamePid=" + session.GamePid, "serverId=" + ctx.ServerId, "tiles=" + string.Join(",", tiles.Take(400).Select(t => t.X + ":" + t.Y)), "" };
        var tmp = cmdPath + "." + id; File.WriteAllLines(tmp, lines); File.Move(tmp, cmdPath);
        var wait = Stopwatch.StartNew(); JsonElement? answer = null;
        while (wait.Elapsed < TimeSpan.FromSeconds(30) && answer is null)
        {
            cts.Token.ThrowIfCancellationRequested();
            try { using var doc = JsonDocument.Parse(File.ReadAllText(resPath)); if (doc.RootElement.GetProperty("requestId").GetString() == id) answer = doc.RootElement.Clone(); }
            catch (IOException) { } catch (JsonException) { }
            if (answer is null) await Task.Delay(100, cts.Token);
        }
        if (answer is null) throw new TimeoutException("terrain fixture did not answer");
        passes.Add(new { pass, capturedAt = DateTimeOffset.UtcNow, result = answer.Value });
    }
    File.WriteAllText(Path.Combine(root, "terrain-probe.json"), JsonSerializer.Serialize(new { serverId = ctx.ServerId, home = new[] { ctx.PlayerTileX, ctx.PlayerTileY },
        tiles = tiles.Take(400).Select(t => new { t.X, t.Y, t.Group, t.Level }), passes }, new JsonSerializerOptions(JsonOptions.Default) { WriteIndented = true }));
    Console.WriteLine("MAP014_TERRAIN_PROBE_DONE tiles=" + Math.Min(400, tiles.Count));
    return;
}
if (scenario == "lens-matrix")
{
    // Diagnostic: single native coverage probes at fixed mid-map targets under different player zooms and
    // lens heights. Reports each native AOI window (rows x columns); verifies exact camera restoration.
    var ctx = await source.GetCurrentContextAsync(cts.Token);
    int hx = ctx.PlayerTileX ?? throw new InvalidOperationException("home tile unknown"), hy = ctx.PlayerTileY!.Value;
    async Task<JsonElement> LensProbe(int tx, int ty, double? lensZoom, int aspect = 4, int fov = 120)
    {
        var id = "lensmatrix" + Guid.NewGuid().ToString("N");
        var cmdPath = Path.Combine(probe, "bulk-aoi-diagnostic.txt");
        var resPath = Path.Combine(probe, "bulk-aoi-diagnostic-result.json");
        var cmd = new List<string> { "schema=1", "probeVersion=lwbridge-live-resource-probe-2", "requestId=" + id,
            "profileId=" + session.ProfileId, "launchSessionId=" + session.SessionId, "challenge=" + session.Challenge,
            "gamePid=" + session.GamePid, "serverId=" + ctx.ServerId, "scanRunId=" + id, "viewLevel=-1", "requestMode=coverage",
            "coverageFov=" + fov, "coverageAspect=" + aspect, "targetTileX=" + tx, "targetTileY=" + ty, "requestedCount=8", "holdMilliseconds=0",
            "homeTileX=" + hx, "homeTileY=" + hy, "includeCity=false", "includeResource=false", "includeMonster=false",
            "includeMonsterProtection=false", "includeTrain=true", "observeMarchFreshness=false", "includeDispatch=false",
            "includeGhost=false", "includeTreasure=false", "includeResourceDetails=false", "deferRestoreUntilResponse=true" };
        if (lensZoom is double z) cmd.Add("coverageZoom=" + z.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
        cmd.Add("");
        var tmp = cmdPath + "." + id;
        File.WriteAllLines(tmp, cmd); File.Move(tmp, cmdPath);
        var wait = Stopwatch.StartNew();
        while (wait.Elapsed < TimeSpan.FromSeconds(20))
        {
            cts.Token.ThrowIfCancellationRequested();
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(resPath));
                if (doc.RootElement.GetProperty("requestId").GetString() == id) return doc.RootElement.Clone();
            }
            catch (IOException) { } catch (JsonException) { }
            await Task.Delay(50, cts.Token);
        }
        throw new TimeoutException("lens probe timed out");
    }
    var matrixRows = new List<object>();
    // Env overrides for the speed study: LWBRIDGE_MAP014_LENS_LIST="130,200,300", LWBRIDGE_MAP014_LENS_ASPECTS="4,6",
    // LWBRIDGE_MAP014_LENS_PLAYER_ZOOMS="" (default only).
    double?[] ParseList(string name, double?[] fallback) => Environment.GetEnvironmentVariable(name) is string raw
        ? raw.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(v => v.Trim() == "none" ? (double?)null : double.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray()
        : fallback;
    double?[] lensList = ParseList("LWBRIDGE_MAP014_LENS_LIST", new double?[] { null, 105.0, 130.0, 81.31 });
    int[] aspects = (Environment.GetEnvironmentVariable("LWBRIDGE_MAP014_LENS_ASPECTS") ?? "4").Split(',').Select(int.Parse).ToArray();
    int[] fovs = (Environment.GetEnvironmentVariable("LWBRIDGE_MAP014_LENS_FOVS") ?? "120").Split(',').Select(int.Parse).ToArray();
    double?[] playerZooms = Environment.GetEnvironmentVariable("LWBRIDGE_MAP014_LENS_PLAYER_ZOOMS") is not null
        ? ParseList("LWBRIDGE_MAP014_LENS_PLAYER_ZOOMS", new double?[] { null }).Prepend(null).Distinct().ToArray()
        : new double?[] { null, 81.31, 60.0 };
    foreach (double? playerZoom in playerZooms)
    {
        if (playerZoom is double pz) { await ZoomFixture("set", pz); await Task.Delay(1500, cts.Token); }
        var camBefore = await Camera("lens-matrix-before-" + (playerZoom?.ToString("0.##") ?? "default"));
        foreach (var (tx, ty) in new[] { (505, 505), (275, 675) })
            foreach (double? lens in lensList)
            foreach (int aspect in aspects)
            foreach (int fov in fovs)
            {
                var r = await LensProbe(tx, ty, lens, aspect, fov);
                string state = r.GetProperty("state").GetString() ?? "";
                int[] idx = r.TryGetProperty("requestedIndices", out var ri) && ri.ValueKind == JsonValueKind.Array
                    ? ri.EnumerateArray().Select(v => v.GetInt32()).ToArray() : [];
                matrixRows.Add(new { playerZoom, lensZoom = lens, aspect, fov, matched = r.TryGetProperty("matchedCount", out var mc) ? mc.ToString() : null, target = new[] { tx, ty }, state,
                    error = r.TryGetProperty("error", out var er) ? er.ToString() : null,
                    aois = idx.Length, rows = idx.Select(i => i / 100).Distinct().Count(), cols = idx.Select(i => i % 100).Distinct().Count(),
                    firstRow = idx.Length > 0 ? idx.Min() / 100 : -1, firstCol = idx.Length > 0 ? idx.Min(i => i % 100) : -1,
                    applied = r.TryGetProperty("coverageZoomApplied", out var ap) ? ap.ToString() : null,
                    geometryRestored = r.TryGetProperty("cameraGeometryRestored", out var gr) ? gr.ToString() : null,
                    serverLod = r.TryGetProperty("serverLod", out var sl) ? sl.ToString() : null,
                    elapsed = r.TryGetProperty("totalProbeElapsedSeconds", out var el) ? el.ToString() : null });
                await Task.Delay(700, cts.Token);
            }
        var camAfter = await Camera("lens-matrix-after-" + (playerZoom?.ToString("0.##") ?? "default"));
        matrixRows.Add(new { playerZoom, cameraRestoredExactly = JsonElement.DeepEquals(camBefore.GetProperty("cameraSnapshot"), camAfter.GetProperty("cameraSnapshot")) });
        if (playerZoom is not null) await ZoomFixture("restore", 0);
        await Task.Delay(1000, cts.Token);
    }
    File.WriteAllText(Path.Combine(root, "lens-matrix.json"), JsonSerializer.Serialize(matrixRows, new JsonSerializerOptions(JsonOptions.Default) { WriteIndented = true }));
    Console.WriteLine("MAP014_LENS_MATRIX_DONE rows=" + matrixRows.Count);
    return;
}
if (benchmarking)
{
    string workload = scenario["benchmark-".Length..];
    string[] kinds = workload switch {
        "mixed" => ["city","resource","monster","zombie_boss","truck","railway","dispatch","ghost"],
        "truck-dispatch" => ["truck","dispatch"],
        _ => [workload]
    };
    if (kinds.Any(kind => !MapScanContract.AllTypes.Contains(kind))) throw new ArgumentException("Unsupported benchmark kind");
    var measurements = new List<object>();
    // Existing City A/B has an independently qualified bounded-lens run.
    // For the user's Truck/Dispatch/mixed reproduction, FOV20 is NOT admitted
    // (it previously missed real moving Trucks). Repeat only the current
    // supported serial LOD0 full-world route instead of silently crossing
    // experimental camera modes in a production performance comparison.
    int rounds = int.TryParse(Environment.GetEnvironmentVariable("LWBRIDGE_MAP014_BENCH_ROUNDS"), out int configuredRounds) && configuredRounds is >= 1 and <= 6
        ? configuredRounds : workload == "city" ? 6 : 2;
    // The owner reported the slowdown in Fast mode; default stays Normal for old receipts.
    string benchScanMode = Environment.GetEnvironmentVariable("LWBRIDGE_MAP014_BENCH_MODE") == "fast" ? "fast" : "normal";
    for (int round=0; round<rounds; round++)
    {
        bool bounded = workload == "city" && round%2 == 1;
        // LWBRIDGE_MAP014_BENCH_CONFIGS="130:120,600:150" alternates lens:fov per round (A/B accuracy + speed).
        string[] benchConfigs = (Environment.GetEnvironmentVariable("LWBRIDGE_MAP014_BENCH_CONFIGS") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
        double? roundZoom = null; int? roundFov = null;
        if (benchConfigs.Length > 0)
        {
            string[] parts = benchConfigs[round % benchConfigs.Length].Split(':');
            roundZoom = double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
            roundFov = parts.Length > 1 ? int.Parse(parts[1]) : null;
        }
        var measuredSource = MakeSource(bounded, forceLegacy: !bounded, zoomArg: roundZoom, fovArg: roundFov);
        var context = await measuredSource.GetCurrentContextAsync(cts.Token);
        if(context.PlayerTileX is int hx && context.PlayerTileY is int hy)
            await measuredSource.JumpToCoordinateAsync(context.ServerId,hx,hy,cts.Token);
        await Task.Delay(750,cts.Token);
        double? fixtureZoom = double.TryParse(Environment.GetEnvironmentVariable("LWBRIDGE_MAP014_FIXTURE_ZOOM"),
            System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double fz) ? fz : null;
        if (fixtureZoom is double requestedZoom) { await ZoomFixture("set", requestedZoom); await Task.Delay(1500,cts.Token); }
        var cameraBefore = await Camera($"benchmark-{round}-before");
        using var measuredCommands = new Map317CommandService(Path.Combine(root,$"benchmark-{round}.db"),
            new CurrentClientMap317ScanProvider(measuredSource), new CurrentClientMap317ActionProvider(measuredSource), false,
            measuredSource.GetMapStatusContextAsync);
        async Task<JsonElement> Call(string name,object payload) => JsonSerializer.SerializeToElement(
            await measuredCommands.InvokeAsync(name,JsonSerializer.SerializeToElement(payload,JsonOptions.Default),cts.Token),JsonOptions.Default);
        // Each round must generate new native acquisition evidence; a metric
        // file left over from an earlier scan is never a qualifying receipt.
        string metricsPath = Path.Combine(probe,"map-fullworld-probe-metrics.json");
        if(File.Exists(metricsPath)) File.Delete(metricsPath);
        var timer=Stopwatch.StartNew();
        try
        {
            await Call("map_scan_start",new{selectedTypes=kinds,scanMode=benchScanMode});
            while(true)
            {
                terminal=await Call("map_scan_status",new{});
                if (!terminal.GetProperty("isReading").GetBoolean() && terminal.GetProperty("phase").GetString() is "completed" or "failed" or "cancelled") break;
                await Task.Delay(100,cts.Token);
            }
            timer.Stop();
            if (terminal.GetProperty("phase").GetString()!="completed") throw new InvalidOperationException(terminal.GetRawText());
            var cameraAfter=await Camera($"benchmark-{round}-after");
            if (!JsonElement.DeepEquals(cameraBefore.GetProperty("cameraSnapshot"),cameraAfter.GetProperty("cameraSnapshot")))
                throw new InvalidDataException("Independent benchmark camera checkpoint differs after full scan.");
            var summary=await Call("map_summary",new{});
            if (!File.Exists(metricsPath)) throw new InvalidDataException("Native benchmark omitted acquisition metrics");
            using var nativeMetrics = JsonDocument.Parse(File.ReadAllText(metricsPath));
            JsonElement provenance = nativeMetrics.RootElement.GetProperty("identity");
            if (nativeMetrics.RootElement.GetProperty("phase").GetString() != "prepared" ||
                nativeMetrics.RootElement.GetProperty("uniqueCoveredAois").GetInt32()!=10000 ||
                provenance.GetProperty("RunId").GetString()!=terminal.GetProperty("scanRunId").GetString() ||
                provenance.GetProperty("ServerId").GetInt32()!=terminal.GetProperty("serverId").GetInt32() ||
                provenance.GetProperty("SelectedTypesKey").GetString()!=string.Join("\u001f",kinds.OrderBy(kind=>kind,StringComparer.Ordinal)))
                throw new InvalidDataException("Native benchmark metrics lacked this exact completed run/selection/coverage identity");
            File.Copy(metricsPath,Path.Combine(root,$"benchmark-{round}-probe-metrics.json"));
            measurements.Add(new{round,scanMode=benchScanMode,lensZoom=roundZoom,lensFov=roundFov,boundedLens=bounded,seconds=timer.Elapsed.TotalSeconds,terminal,summary,cameraBefore,cameraAfter});
            File.WriteAllText(Path.Combine(root,"benchmark.json"),JsonSerializer.Serialize(measurements,new JsonSerializerOptions(JsonOptions.Default){WriteIndented=true}));
            Console.WriteLine($"MAP014_BENCHMARK {workload} round={round} lens={(bounded?20:120)} seconds={timer.Elapsed.TotalSeconds:F3}");
        }
        finally
        {
            if (fixtureZoom is not null) { try { await ZoomFixture("restore", 0); } catch (Exception restoreError) { Console.Error.WriteLine("MAP014_ZOOM_FIXTURE_RESTORE_FAILED "+restoreError.Message); } }
            using var stopCts = new CancellationTokenSource(TimeSpan.FromSeconds(40));
            await measuredCommands.InvokeAsync("map_scan_stop",JsonSerializer.SerializeToElement(new{}),stopCts.Token);
        }
    }
    return;
}
try
{
    var world=await source.GetCurrentContextAsync(cts.Token);
    Console.WriteLine($"NATIVE_MAP014_WORLD server={world.ServerId} world={world.WorldId}");
    if(world.PlayerTileX is int homeX && world.PlayerTileY is int homeY)
        await source.JumpToCoordinateAsync(world.ServerId,homeX,homeY,cts.Token);
    await Task.Delay(1500,cts.Token);
    before=await Camera(scenario+"-before");
    string[] selected = scenario switch
    {
        "mixed" => ["city", "resource", "monster", "truck", "railway", "dispatch"],
        "march-source" => ["truck", "railway"],
        "other" => ["other"],
        "treasure-other" => ["treasure", "other"],
        _ => ["city"],
    };
    var started=await Invoke("map_scan_start",new{selectedTypes=selected,scanMode="normal"});
    history.Add(new{command="map_scan_start",result=started});
    var timer=Stopwatch.StartNew(); var didStop=false;
    while(true)
    {
        var status=await Invoke("map_scan_status",new{});
        var phase=status.GetProperty("phase").GetString();
        Console.WriteLine($"NATIVE_MAP014 {scenario} {phase} completed={status.GetProperty("completedBlocks")} acquisition={(status.TryGetProperty("acquisitionProgressPercent",out var acq)?acq.ToString():"unknown")} elapsed={timer.Elapsed.TotalSeconds:F1}");
        if (scenario=="stop" && !didStop && timer.Elapsed>TimeSpan.FromSeconds(15))
        { history.Add(new{command="map_scan_stop",result=await Invoke("map_scan_stop",new{})}); didStop=true; }
        if (!status.GetProperty("isReading").GetBoolean() && (phase is "completed" or "failed" or "cancelled" or "stopped" || didStop && phase == "idle"))
        { terminal=status; break; }
        await Task.Delay(5000,cts.Token);
    }
    after=await Camera(scenario+"-after");
    history.Add(new{command="map_summary",result=await Invoke("map_summary",new{})});
    foreach(var kind in selected)
        history.Add(new{command="map_search",kind,result=await Invoke("map_search",new{kind,query=new{serverId=world.ServerId,page=1,pageSize=200}})});
    if((scenario is "mixed" or "other" or "treasure-other") && terminal.GetProperty("phase").GetString()=="completed")
    {
        var options=await Invoke("map_data_options",new{serverId=world.ServerId});
        history.Add(new{command="map_data_options",result=options});
        if(scenario=="mixed" && options.TryGetProperty("rewardItems",out var rewards)&&rewards.TryGetProperty("truck",out var items))
        {
            int index=0;
            foreach(var item in items.EnumerateArray().Where(item=>item.TryGetProperty("iconPath",out var path)&&path.ValueKind==JsonValueKind.String&& !string.IsNullOrWhiteSpace(path.GetString())).Take(3))
            {
                try
                {
                    var png=await source.GetAssetImageAsync(item.GetProperty("iconPath").GetString(),null,cts.Token);
                    var file=$"native-truck-item-{++index}.png";
                    File.WriteAllBytes(Path.Combine(root,file),Convert.FromBase64String(png.DataUrl.Split(',')[1]));
                    history.Add(new{command="actual_native_asset_reader",itemKey=item.GetProperty("key").GetString(),file,png.Width,png.Height});
                }
                catch(Exception e) { history.Add(new{command="actual_native_asset_reader",error=e.Message,item}); }
            }
        }
    }
    var report=new{scenario,diagnosticHost=true,productionSource=true,controlledProvider=false,packagedUi=false,
        terminal,history,before,after};
    File.WriteAllText(Path.Combine(root,scenario+"-native-report.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions(JsonOptions.Default){WriteIndented=true}));
    if (scenario!="stop" && terminal.GetProperty("phase").GetString()!="completed")
        throw new InvalidOperationException("Native scan failed: "+terminal.GetRawText());
}
finally
{
    using var stopCts=new CancellationTokenSource(TimeSpan.FromSeconds(40));
    try { await commands.InvokeAsync("map_scan_stop",JsonSerializer.SerializeToElement(new{}),stopCts.Token); }
    catch(Exception e) { Console.Error.WriteLine("Native scan cleanup: "+e.Message); }
}
