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
if (!benchmarking && scenario is not ("city" or "mixed" or "other" or "treasure-other" or "stop" or "march-source" or "lens-matrix" or "terrain-probe" or "lod-snapshot" or "direct-aoi" or "hold-lens" or "direct-full" or "shape-review")) throw new ArgumentException("Unknown scenario");
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
CurrentClientMapBlockSource MakeSource(bool bounded, bool forceLegacy=false, bool marchProbe=false, double? zoomArg=null, int? fovArg=null, bool coarseBoss=false) => new(ReadSession,runtime,probe,
    hooks: coarseBoss ? new CurrentClientMapBlockSourceHooks { UseCoarseZombieBossMap = true }
        : marchProbe ? new CurrentClientMapBlockSourceHooks { ObserveMarchFreshness = true, DisableCoverageZoomNormalization = zoomOff, CoverageZoomOverride = zoomOverride }
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
bool pairedTruck = scenario == "direct-full" && Environment.GetEnvironmentVariable("LWBRIDGE_MAP015_PAIRED_TRUCK") == "1";
// Only this explicit review matrix may extend the historical 30-minute benchmark bound.
bool reviewMatrix = scenario == "benchmark-review";
int reviewMinutes = int.TryParse(Environment.GetEnvironmentVariable("LWBRIDGE_MAP019_MAX_MINUTES"), out int configuredMinutes)
    && configuredMinutes is >= 1 and <= 180 ? configuredMinutes : 180;
if (reviewMatrix && Environment.GetEnvironmentVariable("LWBRIDGE_MAP019_MAX_MINUTES") is not null &&
    (!int.TryParse(Environment.GetEnvironmentVariable("LWBRIDGE_MAP019_MAX_MINUTES"), out int checkedMinutes) || checkedMinutes is <1 or >180))
    throw new ArgumentException("MAP019 max minutes must be 1..180");
using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(reviewMatrix ? reviewMinutes : benchmarking ? 30 : pairedTruck ? 35 : 12));
async Task<JsonElement> Invoke(string command, object payload)
{
    var value = await commands.InvokeAsync(command, JsonSerializer.SerializeToElement(payload,JsonOptions.Default),cts.Token);
    return JsonSerializer.SerializeToElement(value,JsonOptions.Default);
}
async Task<JsonElement> CameraWithToken(string label, CancellationToken cameraToken)
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
        cameraToken.ThrowIfCancellationRequested();
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
        await Task.Delay(100,cameraToken);
    }
    throw new TimeoutException("Independent native camera checkpoint timed out");
}
Task<JsonElement> Camera(string label) => CameraWithToken(label,cts.Token);
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
if (scenario == "direct-full")
{
    // Camera-free whole world: ~70 world.get.block chunks of <=160 areas, sent N per frame (decoded client path).
    var ctx = await source.GetCurrentContextAsync(cts.Token);
    int hx = ctx.PlayerTileX ?? 500, hy = ctx.PlayerTileY ?? 500;
    await source.JumpToCoordinateAsync(ctx.ServerId, hx, hy, cts.Token);
    await Task.Delay(3000, cts.Token);
    var results = new List<object>();
    var cameraRounds = new List<object>();
    // LWBRIDGE_MAP014_FULL_CONFIGS: ';'-separated "linear,rows,cols,first,mEvery[,dwell,gate,interval,one]" (harness experiments only).
    var fullConfigs = (Environment.GetEnvironmentVariable("LWBRIDGE_MAP014_FULL_CONFIGS") ?? "160,10,16,false,0.2;160,10,16,false,0.2")
        .Split(';', StringSplitOptions.RemoveEmptyEntries).Select(c => c.Split(',')).ToArray();
    foreach (var fc in fullConfigs)
    {
        var (perTick, settle, gate, interval, linear) = (1, 6, fc.Length > 6 ? fc[6] : "reply", fc.Length > 7 ? fc[7] : "0", int.Parse(fc[0]));
        var id = "full" + Guid.NewGuid().ToString("N");
        var cmdPath = Path.Combine(probe, "map014-fullscan-fixture.txt");
        var resPath = Path.Combine(probe, "map014-fullscan-fixture-result.json");
        var lines = new[] { "requestId=" + id, "profileId=" + session.ProfileId, "launchSessionId=" + session.SessionId, "challenge=" + session.Challenge,
            "gamePid=" + session.GamePid, "server=" + ctx.ServerId, "rows=" + fc[1], "cols=" + fc[2], "perTick=" + perTick, "settle=" + settle, "gate=" + gate, "interval=" + interval, "linear=" + linear, "first=" + fc[3], "mEvery=" + fc[4], "dwell=" + (fc.Length > 5 ? fc[5] : "0"), "one=" + (fc.Length > 8 ? fc[8] : "0"), "" };
        var tmp = cmdPath + "." + id; File.WriteAllLines(tmp, lines); File.Move(tmp, cmdPath);
        var wait = Stopwatch.StartNew(); JsonElement? answer = null;
        while (wait.Elapsed < TimeSpan.FromSeconds(300) && answer is null)
        {
            try { using var doc = JsonDocument.Parse(File.ReadAllText(resPath)); if (doc.RootElement.GetProperty("requestId").GetString() == id) answer = doc.RootElement.Clone(); }
            catch (IOException) { } catch (JsonException) { }
            if (answer is null) await Task.Delay(100, cts.Token);
        }
        bool completePushObservation = answer is JsonElement tested &&
            tested.TryGetProperty("chunks", out var chunkCount) && chunkCount.GetInt32() == 625 &&
            tested.TryGetProperty("marchPushes", out var pushCount) && pushCount.GetInt32() >= 625 &&
            !tested.TryGetProperty("marchSampleError", out _) &&
            !tested.TryGetProperty("error", out _);
        results.Add(new { config = string.Join(",", fc), perTick, settle, gate, interval, linear,
            wallSeconds = wait.Elapsed.TotalSeconds, completePushObservation, result = answer });
        File.WriteAllText(Path.Combine(root, "direct-full.json"), JsonSerializer.Serialize(results,
            new JsonSerializerOptions(JsonOptions.Default) { WriteIndented = true }));
        Console.WriteLine($"MAP014_DIRECT_FULL perTick={perTick} wall={wait.Elapsed.TotalSeconds:F1}");
        // MAP-015 R2: an independent full production camera Truck round after
        // EACH fast rectangle trial, within the same genuine owned game. Persist
        // both records before advancing, so a later owner timeout cannot make
        // an unpaired trial look accepted.
        if (pairedTruck)
        {
            if (answer is not JsonElement direct ||
                !direct.TryGetProperty("doneAt", out var finished) || !finished.TryGetDouble(out _) ||
                direct.TryGetProperty("error", out _))
                throw new InvalidDataException("Fast Truck fixture did not finish its native-owned cleanup; no camera comparison is safe.");
            int round = cameraRounds.Count;
            var cameraBefore = await Camera($"truck-paired-{round}-before");
            var cameraSource = MakeSource(false, forceLegacy: true);
            var cameraDb = Path.Combine(root, $"truck-paired-{round}.db");
            using var cameraCommands = new Map317CommandService(cameraDb,
                new CurrentClientMap317ScanProvider(cameraSource),
                new CurrentClientMap317ActionProvider(cameraSource), false,
                cameraSource.GetMapStatusContextAsync);
            async Task<JsonElement> CameraCall(string name, object payload) => JsonSerializer.SerializeToElement(
                await cameraCommands.InvokeAsync(name, JsonSerializer.SerializeToElement(payload, JsonOptions.Default), cts.Token),
                JsonOptions.Default);
            var cameraTimer = Stopwatch.StartNew();
            await CameraCall("map_scan_start", new { selectedTypes = new[] { "truck" }, scanMode = "fast" });
            JsonElement cameraTerminal;
            while (true)
            {
                cameraTerminal = await CameraCall("map_scan_status", new { });
                if (!cameraTerminal.GetProperty("isReading").GetBoolean() &&
                    cameraTerminal.GetProperty("phase").GetString() is "completed" or "failed" or "cancelled") break;
                await Task.Delay(100, cts.Token);
            }
            cameraTimer.Stop();
            if (cameraTerminal.GetProperty("phase").GetString() != "completed")
                throw new InvalidOperationException("Paired camera Truck scan failed: " + cameraTerminal.GetRawText());
            var cameraAfter = await Camera($"truck-paired-{round}-after");
            if (!JsonElement.DeepEquals(cameraBefore.GetProperty("cameraSnapshot"), cameraAfter.GetProperty("cameraSnapshot")))
                throw new InvalidDataException("Paired camera Truck scan did not restore original camera.");
            var cameraSummary = await CameraCall("map_summary", new { });
            cameraRounds.Add(new { round, rate = fc.Length > 7 ? fc[7] : "0", fastPushComplete = completePushObservation,
                seconds = cameraTimer.Elapsed.TotalSeconds,
                database = cameraDb, terminal = cameraTerminal, summary = cameraSummary,
                cameraBefore, cameraAfter });
            File.WriteAllText(Path.Combine(root, "direct-full-cameras.json"), JsonSerializer.Serialize(cameraRounds,
                new JsonSerializerOptions(JsonOptions.Default) { WriteIndented = true }));
            Console.WriteLine($"MAP015_PAIRED_CAMERA round={round} seconds={cameraTimer.Elapsed.TotalSeconds:F3}");
        }
        File.WriteAllText(Path.Combine(root, "direct-full.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions(JsonOptions.Default) { WriteIndented = true }));
        await Task.Delay(8000, cts.Token);
    }
    File.WriteAllText(Path.Combine(root, "direct-full.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions(JsonOptions.Default) { WriteIndented = true }));
    return;
}
if (scenario == "hold-lens")
{
    // Hold a big lens for seconds and let the client's own 160-area chunking fetch the whole view.
    var ctx = await source.GetCurrentContextAsync(cts.Token);
    int hx = ctx.PlayerTileX ?? 500, hy = ctx.PlayerTileY ?? 500;
    await source.JumpToCoordinateAsync(ctx.ServerId, hx, hy, cts.Token);
    await Task.Delay(3000, cts.Token);
    var holdCases = new (int x, int y, double zoom, int fov, double hold)[] { (500, 500, 130.0, 120, 2.0), (500, 500, 600.0, 150, 4.0),
        (500, 500, 1000.0, 150, 5.0), (250, 750, 2000.0, 150, 6.0), (750, 250, 3000.0, 170, 6.0) };
    var results = new List<object>();
    foreach (var c in holdCases)
    {
        var id = "hold" + Guid.NewGuid().ToString("N");
        var cmdPath = Path.Combine(probe, "map014-hold-fixture.txt");
        var resPath = Path.Combine(probe, "map014-hold-fixture-result.json");
        var lines = new[] { "requestId=" + id, "profileId=" + session.ProfileId, "launchSessionId=" + session.SessionId, "challenge=" + session.Challenge,
            "gamePid=" + session.GamePid, "x=" + c.x, "y=" + c.y, "zoom=" + c.zoom.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "fov=" + c.fov, "hold=" + c.hold.ToString(System.Globalization.CultureInfo.InvariantCulture), "" };
        var tmp = cmdPath + "." + id; File.WriteAllLines(tmp, lines); File.Move(tmp, cmdPath);
        var wait = Stopwatch.StartNew(); JsonElement? answer = null;
        while (wait.Elapsed < TimeSpan.FromSeconds(c.hold + 10) && answer is null)
        {
            try { using var doc = JsonDocument.Parse(File.ReadAllText(resPath)); if (doc.RootElement.GetProperty("requestId").GetString() == id) answer = doc.RootElement.Clone(); }
            catch (IOException) { } catch (JsonException) { }
            if (answer is null) await Task.Delay(50, cts.Token);
        }
        results.Add(new { c.x, c.y, c.zoom, c.fov, c.hold, result = answer });
        await Task.Delay(3000, cts.Token);
    }
    File.WriteAllText(Path.Combine(root, "hold-lens.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions(JsonOptions.Default) { WriteIndented = true }));
    return;
}
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
    var cases = new List<(string Name, int[] Areas, bool WholeWorldRect, int Type)>
    {
        ("send-far-250-750-3x3", Areas(24, 74, 3, 3), false, 0),
        ("send-far-750-750-3x3", Areas(74, 74, 3, 3), false, 0),
        ("send-far-750-250-160", Areas(67, 20, 16, 10), false, 0),
        ("send-far-250-250-160", Areas(17, 20, 16, 10), false, 0),
    };
    // Arm the read-only 18 s incoming-packet trace (map013 wrapper) so world.get.block replies are recorded.
    string traceCmd = Path.Combine(probe, "map013-native-trace.txt"), traceRes = Path.Combine(probe, "map013-native-trace-result.json");
    if (File.Exists(traceRes)) File.Delete(traceRes);
    File.WriteAllLines(traceCmd + ".tmp", new[] { "profileId=" + session.ProfileId, "launchSessionId=" + session.SessionId,
        "challenge=" + session.Challenge, "gamePid=" + session.GamePid, "sceneOnly=false", "" });
    File.Move(traceCmd + ".tmp", traceCmd);
    await Task.Delay(1500, cts.Token);
    var results = new List<object>();
    foreach (var (name, areas, wholeWorld, msgType) in cases)
    {
        int minC = areas.Min(a => a % 100), maxC = areas.Max(a => a % 100), minR = areas.Min(a => a / 100), maxR = areas.Max(a => a / 100);
        int lb = wholeWorld ? 1 : TileIndex(minC * 10, minR * 10), rt = wholeWorld ? 1000000 : TileIndex(Math.Min(999, (maxC + 1) * 10), Math.Min(999, (maxR + 1) * 10));
        int cx = (minC + maxC + 1) * 5, cy = (minR + maxR + 1) * 5;
        var id = "direct" + Guid.NewGuid().ToString("N");
        var cmdPath = Path.Combine(probe, "map014-direct-fixture.txt");
        var resPath = Path.Combine(probe, "map014-direct-fixture-result.json");
        var lines = new[] { "requestId=" + id, "profileId=" + session.ProfileId, "launchSessionId=" + session.SessionId, "challenge=" + session.Challenge,
            "gamePid=" + session.GamePid, "indices=" + string.Join(",", areas), "lb=" + lb, "rt=" + rt, "x=" + cx, "y=" + cy,
            "mode=msg", "type=" + msgType, "server=" + ctx.ServerId, "first=false", "layout=" + name[..1], "" };
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
    var traceWait = Stopwatch.StartNew();
    while (traceWait.Elapsed < TimeSpan.FromSeconds(25) && !File.Exists(traceRes)) await Task.Delay(250, cts.Token);
    if (File.Exists(traceRes)) File.Copy(traceRes, Path.Combine(root, "direct-aoi-packets.json"), true);
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
    async Task<JsonElement> LensProbe(int tx, int ty, double? lensZoom, int aspect = 4, int fov = 120, string edge = "")
    {
        var id = "lensmatrix" + Guid.NewGuid().ToString("N");
        var cmdPath = Path.Combine(probe, "bulk-aoi-diagnostic.txt");
        var resPath = Path.Combine(probe, "bulk-aoi-diagnostic-result.json");
        var cmd = new List<string> { "schema=1", "probeVersion=lwbridge-live-resource-probe-2", "requestId=" + id,
            "profileId=" + session.ProfileId, "launchSessionId=" + session.SessionId, "challenge=" + session.Challenge,
            "gamePid=" + session.GamePid, "serverId=" + ctx.ServerId, "scanRunId=" + id, "viewLevel=-1", "requestMode=coverage",
            "coverageFov=" + fov, "coverageAspect=" + aspect, "targetTileX=" + tx, "targetTileY=" + ty, "requestedCount=8", "holdMilliseconds=" + (Environment.GetEnvironmentVariable("LWBRIDGE_MAP014_LENS_HOLD_MS") ?? "0"),
            "homeTileX=" + hx, "homeTileY=" + hy, "includeCity=false", "includeResource=false", "includeMonster=false",
            "includeMonsterProtection=false", "includeTrain=true", "observeMarchFreshness=false", "includeDispatch=false",
            "includeGhost=false", "includeTreasure=false", "includeResourceDetails=false", "deferRestoreUntilResponse=" + (Environment.GetEnvironmentVariable("LWBRIDGE_MAP014_LENS_HOLD_MS") is null ? "true" : "false"),
            "drainSplit=" + (Environment.GetEnvironmentVariable("LWBRIDGE_MAP014_LENS_DRAIN") == "1" ? "true" : "false"),
            "coverageEdge=" + edge };
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
            foreach (string edge in (Environment.GetEnvironmentVariable("LWBRIDGE_MAP014_LENS_EDGES") ?? "").Split(',').Select(e => e.Trim() == "none" ? "" : e.Trim()))
            {
                var r = await LensProbe(tx, ty, lens, aspect, fov, edge);
                string state = r.GetProperty("state").GetString() ?? "";
                int[] idx = r.TryGetProperty("requestedIndices", out var ri) && ri.ValueKind == JsonValueKind.Array
                    ? ri.EnumerateArray().Select(v => v.GetInt32()).ToArray() : [];
                matrixRows.Add(new { playerZoom, lensZoom = lens, aspect, fov, edge, matched = r.TryGetProperty("matchedCount", out var mc) ? mc.ToString() : null, target = new[] { tx, ty }, state,
                    error = r.TryGetProperty("error", out var er) ? er.ToString() : null,
                    aois = idx.Length, rows = idx.Select(i => i / 100).Distinct().Count(), cols = idx.Select(i => i % 100).Distinct().Count(),
                    firstRow = idx.Length > 0 ? idx.Min() / 100 : -1, firstCol = idx.Length > 0 ? idx.Min(i => i % 100) : -1,
                    applied = r.TryGetProperty("coverageZoomApplied", out var ap) ? ap.ToString() : null,
                    geometryRestored = r.TryGetProperty("cameraGeometryRestored", out var gr) ? gr.ToString() : null,
                    serverLod = r.TryGetProperty("serverLod", out var sl) ? sl.ToString() : null,
                    elapsed = r.TryGetProperty("totalProbeElapsedSeconds", out var el) ? el.ToString() : null,
                    edgeOriginal = r.TryGetProperty("coverageEdgeOriginal", out var eo) ? eo.ToString() : null,
                    held = r.TryGetProperty("heldViewCount", out var hc) ? hc.ToString() : null,
                    heldRows = r.TryGetProperty("heldViewIndices", out var hi) && hi.ValueKind == JsonValueKind.Array ? hi.EnumerateArray().Select(v => v.GetInt32() / 100).Distinct().Count() : 0,
                    heldCols = r.TryGetProperty("heldViewIndices", out var hj) && hj.ValueKind == JsonValueKind.Array ? hj.EnumerateArray().Select(v => v.GetInt32() % 100).Distinct().Count() : 0,
                    heldSplit = r.TryGetProperty("heldSplitPending", out var hs) ? hs.ToString() : null });
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
if (scenario == "shape-review")
{
    await Map019ShapeReview.RunAsync(source, session, root, ReadSession, Camera, cts.Token);
    return;
}
if (benchmarking)
{
    string[] reviewDefaults = ["city","resource","monster","zombie_boss","dispatch","ghost","treasure","railway","truck","city-resource","all-nine"];
    string[] workloads = reviewMatrix ? (Environment.GetEnvironmentVariable("LWBRIDGE_MAP019_WORKLOADS") ?? string.Join(',', reviewDefaults))
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) : [scenario["benchmark-".Length..]];
    if (workloads.Length == 0 || workloads.Distinct(StringComparer.Ordinal).Count() != workloads.Length)
        throw new ArgumentException("Review workloads must be nonempty and unique");
    string[] Kinds(string selectedWorkload) => selectedWorkload switch {
        "mixed" => ["city","resource","monster","zombie_boss","truck","railway","dispatch","ghost"],
        "all-nine" => ["city","resource","monster","zombie_boss","truck","railway","dispatch","ghost","treasure"],
        "all-nonboss" => ["city","resource","monster","truck","railway","dispatch","ghost","treasure"],
        "city-resource" => ["city","resource"],
        "all-nontruck" => ["city","resource","monster","zombie_boss","railway","dispatch","ghost","treasure"],
        "truck-dispatch" => ["truck","dispatch"],
        _ => [selectedWorkload]
    };
    if (workloads.Any(workload => Kinds(workload).Any(kind => !MapScanContract.AllTypes.Contains(kind))))
        throw new ArgumentException("Unsupported benchmark kind");
    var measurements = new List<object>();
    JsonElement ProcessSample()
    {
        try
        {
            if (!OwnProcessMatches()) throw new InvalidOperationException("Exact game process identity changed");
            using var game = Process.GetProcessById(session.GamePid);
            game.Refresh();
            return JsonSerializer.SerializeToElement(new { available = true, capturedAtUtc = DateTimeOffset.UtcNow,
                totalCpuSeconds = game.TotalProcessorTime.TotalSeconds, workingSetBytes = game.WorkingSet64,
                privateBytes = game.PrivateMemorySize64, peakWorkingSetBytesProcessLifetime = game.PeakWorkingSet64,
                logicalProcessors = Environment.ProcessorCount, fpsMeasured = false });
        }
        catch (Exception error) when (error is ArgumentException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return JsonSerializer.SerializeToElement(new { available = false, error = error.Message, fpsMeasured = false });
        }
    }
    bool CompleteTrainReceipt(JsonElement receipt)
    {
        bool IntIs(string name, int expected) => receipt.TryGetProperty(name, out var value) && value.TryGetInt32(out int actual) && actual == expected;
        return receipt.TryGetProperty("state", out var state) && state.GetString() == "proven" &&
            IntIs("initialRequestsSent",625) && IntIs("rectanglesCompleted",625) && IntIs("unprovenRectangles",0) &&
            IntIs("responseFenceSize",25) && IntIs("requestsPerSecond",20) &&
            receipt.TryGetProperty("responseAttribution",out var attribution) && attribution.GetString()=="bounded_sequential_fences" &&
            receipt.TryGetProperty("requestsSent",out var sentValue) && sentValue.TryGetInt32(out int sent) && sent is >=625 and <=1875 &&
            receipt.TryGetProperty("repairPasses",out var passValue) && passValue.TryGetInt32(out int passes) && passes is >=0 and <=2 &&
            IntIs("repairRequestsSent",sent-625) && (passes > 0 || sent == 625) && sent <= 625*(passes+1) &&
            receipt.TryGetProperty("pushesObserved",out var pushValue) && pushValue.TryGetInt32(out int pushes) && pushes >=625 && pushes <=sent &&
            receipt.TryGetProperty("cameraStable",out var stable) && stable.ValueKind==JsonValueKind.True &&
            receipt.TryGetProperty("curViewRestored",out var restored) && restored.ValueKind==JsonValueKind.True;
    }
    // Existing City A/B has an independently qualified bounded-lens run.
    // For the user's Truck/Dispatch/mixed reproduction, FOV20 is NOT admitted
    // (it previously missed real moving Trucks). Repeat only the current
    // supported serial LOD0 full-world route instead of silently crossing
    // experimental camera modes in a production performance comparison.
    string? roundsRaw = Environment.GetEnvironmentVariable(reviewMatrix ? "LWBRIDGE_MAP019_ROUNDS" : "LWBRIDGE_MAP014_BENCH_ROUNDS");
    int? roundsOverride = int.TryParse(roundsRaw, out int configuredRounds) && configuredRounds is >= 1 and <= 6 ? configuredRounds : null;
    if (roundsRaw is not null && roundsOverride is null) throw new ArgumentException("Benchmark rounds must be 1..6");
    // The owner reported the slowdown in Fast mode; default stays Normal for old receipts.
    string benchScanMode = Environment.GetEnvironmentVariable("LWBRIDGE_MAP014_BENCH_MODE") == "fast" ? "fast" : "normal";
    // MAP-015 owner A/B: a single exact-owned game session may alternate
    // direct camera-free and the existing camera route. Empty retains MAP-014
    // lens qualification behavior so prior benchmark scripts remain valid.
    string[] methodConfigs = (Environment.GetEnvironmentVariable(reviewMatrix ? "LWBRIDGE_MAP019_METHODS" : "LWBRIDGE_MAP015_BENCH_METHODS") ?? (reviewMatrix ? "direct,camera" : ""))
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (reviewMatrix && methodConfigs.Length == 0) throw new ArgumentException("MAP019 methods cannot be empty");
    if (methodConfigs.Any(mode => mode is not ("direct" or "camera") && !(reviewMatrix && mode == "coarse-boss")))
        throw new ArgumentException("Benchmark methods support direct,camera; review alone also supports coarse-boss");
    if (methodConfigs.Contains("coarse-boss") && workloads.Any(workload => workload != "zombie_boss"))
        throw new ArgumentException("coarse-boss requires only the zombie_boss workload");
    await using var remoteScope = reviewMatrix ? await Map019ServerScope.EnterAsync(source,CameraWithToken,root,cts.Token) : null;
    int ordinal = 0;
    foreach (string workload in workloads)
    {
    string[] kinds = Kinds(workload);
    int rounds = roundsOverride ?? (reviewMatrix ? 4 : workload == "city" ? 6 : 2);
    for (int round=0; round<rounds; round++)
    {
        int measurementOrdinal = ordinal++;
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
        string sourceMethod = methodConfigs.Length > 0 ? methodConfigs[round % methodConfigs.Length] : "legacy-lens";
        var measuredSource = sourceMethod switch {
            "direct" => MakeSource(false),
            "camera" => MakeSource(false, forceLegacy: true),
            "coarse-boss" => MakeSource(false, coarseBoss: true),
            _ => MakeSource(bounded, forceLegacy: !bounded, zoomArg: roundZoom, fovArg: roundFov),
        };
        var context = await measuredSource.GetCurrentContextAsync(cts.Token);
        if(context.PlayerTileX is int hx && context.PlayerTileY is int hy)
            await measuredSource.JumpToCoordinateAsync(context.ServerId,hx,hy,cts.Token);
        await Task.Delay(750,cts.Token);
        double? fixtureZoom = double.TryParse(Environment.GetEnvironmentVariable("LWBRIDGE_MAP014_FIXTURE_ZOOM"),
            System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double fz) ? fz : null;
        if (fixtureZoom is double requestedZoom) { await ZoomFixture("set", requestedZoom); await Task.Delay(1500,cts.Token); }
        var cameraBefore = await Camera($"benchmark-{measurementOrdinal}-before");
        using var measuredCommands = new Map317CommandService(Path.Combine(root,$"benchmark-{measurementOrdinal}.db"),
            new CurrentClientMap317ScanProvider(measuredSource), new CurrentClientMap317ActionProvider(measuredSource), false,
            measuredSource.GetMapStatusContextAsync);
        async Task<JsonElement> Call(string name,object payload) => JsonSerializer.SerializeToElement(
            await measuredCommands.InvokeAsync(name,JsonSerializer.SerializeToElement(payload,JsonOptions.Default),cts.Token),JsonOptions.Default);
        // Each round must generate new native acquisition evidence; a metric
        // file left over from an earlier scan is never a qualifying receipt.
        string metricsPath = Path.Combine(probe,"map-fullworld-probe-metrics.json");
        if(File.Exists(metricsPath)) File.Delete(metricsPath);
        string frameTelemetryPath = Path.Combine(probe,"map019-frame-telemetry.json");
        void CopyFrameTelemetry(string edge)
        {
            if (Environment.GetEnvironmentVariable("LWBRIDGE_MAP019_TELEMETRY") != "1" || !File.Exists(frameTelemetryPath)) return;
            // Raw owner-bound telemetry stays private under this exact attempt.
            // Repeated frameCount windows must be differenced, never read as a per-round FPS sample.
            try
            {
                using var frameDoc = JsonDocument.Parse(File.ReadAllText(frameTelemetryPath));
                var value = frameDoc.RootElement;
                if (value.GetProperty("profileId").GetString()!=session.ProfileId || value.GetProperty("sessionId").GetString()!=session.SessionId ||
                    value.GetProperty("challenge").GetString()!=session.Challenge || value.GetProperty("gamePid").GetString()!=session.GamePid.ToString()) return;
                File.WriteAllText(Path.Combine(root,$"benchmark-{measurementOrdinal}-frames-{edge}.json"),value.GetRawText());
            }
            catch (IOException) { } catch (JsonException) { }
        }
        JsonElement? processIdleBefore = null;
        if (Environment.GetEnvironmentVariable("LWBRIDGE_MAP019_TELEMETRY") == "1")
        {
            CopyFrameTelemetry("idle-before");
            processIdleBefore = ProcessSample();
            await Task.Delay(3000,cts.Token);
        }
        JsonElement processBefore = ProcessSample();
        CopyFrameTelemetry("before");
        long sampledPeakWorkingSetBytes = 0, sampledPeakPrivateBytes = 0;
        int memorySamples = 0;
        var nextMemorySample = TimeSpan.Zero;
        DateTimeOffset roundStartedAt = DateTimeOffset.UtcNow;
        JsonElement? coarseEvidence = null;
        var coarseRequestIds = new HashSet<string>(StringComparer.Ordinal);
        void CaptureCoarseEvidence()
        {
            if (sourceMethod != "coarse-boss") return;
            try
            {
                string path = Path.Combine(probe,"bulk-aoi-diagnostic-result.json");
                using var coarseDoc = JsonDocument.Parse(File.ReadAllText(path));
                var value = coarseDoc.RootElement;
                bool StringIs(string name,string expected) => value.TryGetProperty(name,out var field) && field.GetString()==expected;
                if (!StringIs("profileId",session.ProfileId) || !StringIs("launchSessionId",session.SessionId) ||
                    !StringIs("challenge",session.Challenge) || !StringIs("requestMode","zoom") ||
                    !value.TryGetProperty("gamePid",out var pid) || pid.GetInt32()!=session.GamePid ||
                    !value.TryGetProperty("capturedAt",out var time) || !DateTimeOffset.TryParse(time.GetString(),out var capturedAt) ||
                    capturedAt < roundStartedAt.AddSeconds(-1) || capturedAt > DateTimeOffset.UtcNow.AddSeconds(2) ||
                    !value.TryGetProperty("requestId",out var requestIdValue)) return;
                string requestId = requestIdValue.GetString() ?? "";
                if (!requestId.StartsWith("monsterlod2",StringComparison.Ordinal) || !requestId.All(char.IsLetterOrDigit)) return;
                coarseEvidence = value.Clone();
                if (coarseRequestIds.Add(requestId))
                    File.Copy(path,Path.Combine(root,$"benchmark-{measurementOrdinal}-coarse-{requestId}.json"),true);
            }
            catch (IOException) { } catch (JsonException) { }
        }
        var timer=Stopwatch.StartNew();
        try
        {
            await Call("map_scan_start",new{selectedTypes=kinds,scanMode=benchScanMode});
            while(true)
            {
                terminal=await Call("map_scan_status",new{});
                CaptureCoarseEvidence();
                if (Environment.GetEnvironmentVariable("LWBRIDGE_MAP019_TELEMETRY") == "1" && timer.Elapsed >= nextMemorySample)
                {
                    JsonElement sample = ProcessSample();
                    if (sample.TryGetProperty("workingSetBytes",out var working) && sample.TryGetProperty("privateBytes",out var privateValue))
                    {
                        sampledPeakWorkingSetBytes = Math.Max(sampledPeakWorkingSetBytes,working.GetInt64());
                        sampledPeakPrivateBytes = Math.Max(sampledPeakPrivateBytes,privateValue.GetInt64());
                        memorySamples++;
                    }
                    nextMemorySample = timer.Elapsed + TimeSpan.FromSeconds(1);
                }
                if (!terminal.GetProperty("isReading").GetBoolean() && terminal.GetProperty("phase").GetString() is "completed" or "failed" or "cancelled") break;
                await Task.Delay(100,cts.Token);
            }
            timer.Stop();
            JsonElement processAfter = ProcessSample();
            CopyFrameTelemetry("after");
            double? processCpuSeconds = processBefore.TryGetProperty("totalCpuSeconds",out var beforeCpu) &&
                processAfter.TryGetProperty("totalCpuSeconds",out var afterCpu) ? afterCpu.GetDouble()-beforeCpu.GetDouble() : null;
            if (terminal.GetProperty("phase").GetString()!="completed") throw new InvalidOperationException(terminal.GetRawText());
            var cameraAfter=await Camera($"benchmark-{measurementOrdinal}-after");
            if (!JsonElement.DeepEquals(cameraBefore.GetProperty("cameraSnapshot"),cameraAfter.GetProperty("cameraSnapshot")))
                throw new InvalidDataException("Independent benchmark camera checkpoint differs after full scan.");
            var summary=await Call("map_summary",new{});
            bool nativeFastMobile = sourceMethod == "direct" && kinds.All(kind => kind is "truck" or "railway");
            if (!nativeFastMobile && sourceMethod != "coarse-boss")
            {
                if (!File.Exists(metricsPath)) throw new InvalidDataException("Native benchmark omitted acquisition metrics");
                using var nativeMetrics = JsonDocument.Parse(File.ReadAllText(metricsPath));
                JsonElement provenance = nativeMetrics.RootElement.GetProperty("identity");
                if (nativeMetrics.RootElement.GetProperty("phase").GetString() != "prepared" ||
                    nativeMetrics.RootElement.GetProperty("uniqueCoveredAois").GetInt32()!=10000 ||
                    provenance.GetProperty("RunId").GetString()!=terminal.GetProperty("scanRunId").GetString() ||
                    provenance.GetProperty("ServerId").GetInt32()!=terminal.GetProperty("serverId").GetInt32() ||
                    provenance.GetProperty("SelectedTypesKey").GetString()!=string.Join("\u001f",kinds.OrderBy(kind=>kind,StringComparer.Ordinal)))
                    throw new InvalidDataException("Native benchmark metrics lacked this exact completed run/selection/coverage identity");
                File.Copy(metricsPath,Path.Combine(root,$"benchmark-{measurementOrdinal}-probe-metrics.json"));
            }
            JsonElement? directEvidence = null;
            bool ReceiptBelongs(JsonElement receipt) =>
                receipt.TryGetProperty("scanRunId",out var run) && run.GetString()==terminal.GetProperty("scanRunId").GetString() &&
                receipt.TryGetProperty("profileId",out var profile) && profile.GetString()==session.ProfileId &&
                receipt.TryGetProperty("launchSessionId",out var launch) && launch.GetString()==session.SessionId &&
                receipt.TryGetProperty("challenge",out var challenge) && challenge.GetString()==session.Challenge &&
                receipt.TryGetProperty("gamePid",out var pid) && pid.TryGetInt32(out int gamePid) && gamePid==session.GamePid &&
                receipt.TryGetProperty("serverId",out var server) && server.GetInt32()==terminal.GetProperty("serverId").GetInt32();
            string directEvidencePath = Path.Combine(probe,nativeFastMobile ? "fast-truck-scan-result.json" : "camera-free-scan-result.json");
            if (sourceMethod == "direct" && File.Exists(directEvidencePath))
            {
                using var directDocument = JsonDocument.Parse(File.ReadAllText(directEvidencePath));
                if (ReceiptBelongs(directDocument.RootElement))
                {
                    directEvidence = directDocument.RootElement.Clone();
                    File.Copy(directEvidencePath,Path.Combine(root,$"benchmark-{measurementOrdinal}-direct-evidence.json"),true);
                }
            }
            string effectiveMethod = sourceMethod != "direct" ? sourceMethod :
                directEvidence is JsonElement evidence && evidence.TryGetProperty("state",out var stateValue) &&
                stateValue.GetString() == "proven" ? "direct" : "camera-fallback";
            // A valid native discovery receipt can precede a managed hydration
            // rejection. In that case the published scan used camera fallback.
            // Never infer effective acquisition solely from state=proven.
            JsonElement? fallbackEvidence = null;
            if (sourceMethod == "direct")
            {
                string fallbackPath = Path.Combine(probe,"map015-direct-fallback.json");
                try
                {
                    using var fallbackDoc = JsonDocument.Parse(File.ReadAllText(fallbackPath));
                    var fallback = fallbackDoc.RootElement;
                    if (fallback.TryGetProperty("runId",out var fallbackRun) &&
                        fallbackRun.GetString()==terminal.GetProperty("scanRunId").GetString())
                    {
                        fallbackEvidence=fallback.Clone();
                        effectiveMethod="camera-fallback";
                        File.Copy(fallbackPath,Path.Combine(root,$"benchmark-{measurementOrdinal}-fallback.json"),true);
                    }
                }
                catch (IOException) { } catch (JsonException) { }
            }
            if (sourceMethod == "coarse-boss")
            {
                CaptureCoarseEvidence();
                effectiveMethod = coarseEvidence is JsonElement coarse && coarse.TryGetProperty("state",out var coarseState) &&
                    coarseState.GetString()=="proven" && coarse.TryGetProperty("zoomWholeWorldCoarse",out var wholeWorld) &&
                    wholeWorld.ValueKind==JsonValueKind.True ? "coarse-boss" : "camera-fallback";
            }
            if (nativeFastMobile && (effectiveMethod != "direct" ||
                directEvidence is not JsonElement truckReceipt ||
                !CompleteTrainReceipt(truckReceipt)))
                throw new InvalidDataException("Benchmark did not qualify genuine complete native fast mobile acquisition with bounded repairs.");
            if (effectiveMethod == "direct" && nativeFastMobile)
                effectiveMethod = kinds.Contains("truck") ? "direct-fast-truck" : "direct-fast-railway";
            if (sourceMethod == "direct" && !nativeFastMobile && kinds.Any(kind => kind is "truck" or "railway"))
            {
                string mobilePath = Path.Combine(probe,"fast-truck-scan-result.json");
                if (File.Exists(mobilePath))
                {
                    using var mobileDoc = JsonDocument.Parse(File.ReadAllText(mobilePath));
                    if (ReceiptBelongs(mobileDoc.RootElement))
                        File.Copy(mobilePath,Path.Combine(root,$"benchmark-{measurementOrdinal}-mobile-evidence.json"),true);
                }
            }
            measurements.Add(new{round,ordinal=measurementOrdinal,workload,selectedTypes=kinds,method=sourceMethod,effectiveMethod,
                directEvidenceState=directEvidence is JsonElement sourceState && sourceState.TryGetProperty("state",out var stateProperty) ? stateProperty.GetString() : null,
                directEvidenceError=directEvidence is JsonElement sourceError && sourceError.TryGetProperty("error",out var errorProperty) && errorProperty.ValueKind==JsonValueKind.String ? errorProperty.GetString() : null,
                scanMode=benchScanMode,lensZoom=roundZoom,lensFov=roundFov,boundedLens=bounded,seconds=timer.Elapsed.TotalSeconds,
                processIdleBefore,processBefore,processAfter,processCpuSeconds,
                sampledPeakWorkingSetBytes,sampledPeakPrivateBytes,memorySamples,memorySamplingIntervalSeconds=1,
                fallbackEvidence,coarseEvidence,coarseEvidenceBinding=sourceMethod=="coarse-boss" ? "owner+capture-window; native validator correlates requestId; bulk schema omits scanRunId/serverId" : null,
                averageMachineCpuPercent=processCpuSeconds is double cpu ? (double?)(100*cpu/timer.Elapsed.TotalSeconds/Environment.ProcessorCount) : null,
                fpsMeasured=false,terminal,summary,cameraBefore,cameraAfter});
            File.WriteAllText(Path.Combine(root,"benchmark.json"),JsonSerializer.Serialize(measurements,new JsonSerializerOptions(JsonOptions.Default){WriteIndented=true}));
            Console.WriteLine($"MAP014_BENCHMARK {workload} round={round} lens={(bounded?20:120)} seconds={timer.Elapsed.TotalSeconds:F3}");
        }
        catch (Exception error) when (reviewMatrix && !cts.IsCancellationRequested)
        {
            // Preserve the negative before deciding if another round is safe.
            timer.Stop();
            CopyFrameTelemetry("failed-after");
            CaptureCoarseEvidence();
            bool sawOwnedNativeReceipt = false, nativeRestorationVerified = true;
            foreach (string lane in new[] { "camera-free", "fast-truck" })
            {
                try
                {
                    using var failedNative = JsonDocument.Parse(File.ReadAllText(Path.Combine(probe,lane+"-scan-result.json")));
                    JsonElement value = failedNative.RootElement;
                    if (terminal.ValueKind==JsonValueKind.Object && terminal.TryGetProperty("scanRunId",out var terminalRun) &&
                        value.TryGetProperty("scanRunId",out var failedRun) && failedRun.GetString()==terminalRun.GetString() &&
                        value.TryGetProperty("profileId",out var failedProfile) && failedProfile.GetString()==session.ProfileId &&
                        value.TryGetProperty("launchSessionId",out var failedSession) && failedSession.GetString()==session.SessionId &&
                        value.TryGetProperty("challenge",out var failedChallenge) && failedChallenge.GetString()==session.Challenge &&
                        value.TryGetProperty("gamePid",out var failedPid) && failedPid.GetInt32()==session.GamePid)
                    {
                        sawOwnedNativeReceipt = true;
                        nativeRestorationVerified &= value.TryGetProperty("cameraStable",out var stable) && stable.ValueKind==JsonValueKind.True &&
                            value.TryGetProperty("curViewRestored",out var restored) && restored.ValueKind==JsonValueKind.True;
                        File.WriteAllText(Path.Combine(root,$"benchmark-{measurementOrdinal}-{lane}-failed-evidence.json"),value.GetRawText());
                    }
                }
                catch (IOException) { } catch (JsonException) { }
            }
            measurements.Add(new { round,ordinal=measurementOrdinal,workload,selectedTypes=kinds,method=sourceMethod,
                effectiveMethod="unqualified",seconds=timer.Elapsed.TotalSeconds,errorType=error.GetType().Name,
                error=error.Message,terminal,processIdleBefore,processBefore,processAfter=ProcessSample(),coarseEvidence,fpsMeasured=false });
            File.WriteAllText(Path.Combine(root,"benchmark.json"),JsonSerializer.Serialize(measurements,new JsonSerializerOptions(JsonOptions.Default){WriteIndented=true}));
            Console.Error.WriteLine($"MAP019_BENCHMARK_FAILED {workload} round={round} {error.GetType().Name}: {error.Message}");
            using var failedStopCts = new CancellationTokenSource(TimeSpan.FromSeconds(40));
            await measuredCommands.InvokeAsync("map_scan_stop",JsonSerializer.SerializeToElement(new{}),failedStopCts.Token);
            if (ReadSession()!=session) throw new InvalidOperationException("Review aborted after failure: exact owner/session is no longer admitted",error);
            var failedCamera = await Camera($"benchmark-{measurementOrdinal}-failed-after");
            bool cameraRestored = JsonElement.DeepEquals(cameraBefore.GetProperty("cameraSnapshot"),failedCamera.GetProperty("cameraSnapshot"));
            string? failedPhase = terminal.ValueKind==JsonValueKind.Object && terminal.TryGetProperty("phase",out var phase) ? phase.GetString() : null;
            // Continue only a settled failed/cancelled direct scan with independently
            // matching camera and positive restoration flags on every same-run receipt.
            // A validation error after completion, missing receipt, candidate failure,
            // ownership drift or uncertain native cleanup retires the whole session.
            if (sourceMethod!="direct" || failedPhase is not ("failed" or "cancelled") ||
                !cameraRestored || !sawOwnedNativeReceipt || !nativeRestorationVerified)
                throw new InvalidOperationException("Review aborted after failure: safe exact native restoration was not independently verified",error);
            File.WriteAllText(Path.Combine(root,$"benchmark-{measurementOrdinal}-failure-cleanup.json"),JsonSerializer.Serialize(new {
                safeToContinue=true,failedPhase,exactOwnerAdmitted=true,cameraRestored,nativeRestorationVerified,failedCamera }));
        }
        finally
        {
            if (fixtureZoom is not null) { try { await ZoomFixture("restore", 0); } catch (Exception restoreError) { Console.Error.WriteLine("MAP014_ZOOM_FIXTURE_RESTORE_FAILED "+restoreError.Message); } }
            using var stopCts = new CancellationTokenSource(TimeSpan.FromSeconds(40));
            await measuredCommands.InvokeAsync("map_scan_stop",JsonSerializer.SerializeToElement(new{}),stopCts.Token);
        }
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
