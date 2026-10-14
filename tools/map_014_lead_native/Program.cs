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
if (!benchmarking && scenario is not ("city" or "mixed" or "other" or "treasure-other" or "stop" or "march-source")) throw new ArgumentException("Unknown scenario");
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
CurrentClientMapBlockSource MakeSource(bool bounded, bool forceLegacy=false, bool marchProbe=false) => new(ReadSession,runtime,probe,
    hooks: marchProbe ? new CurrentClientMapBlockSourceHooks { ObserveMarchFreshness = true }
        : bounded ? new CurrentClientMapBlockSourceHooks { ExperimentalBoundedCameraLens = true }
        : forceLegacy ? new CurrentClientMapBlockSourceHooks { ForceLegacyFullWorldLens = true } : null,
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
var history=new List<object>();
JsonElement before=default, after=default, terminal=default;
if (benchmarking)
{
    string workload = scenario["benchmark-".Length..];
    string[] kinds = workload == "mixed" ? ["city","resource","monster","truck","railway","dispatch"] : [workload];
    if (kinds.Any(kind => !MapScanContract.AllTypes.Contains(kind))) throw new ArgumentException("Unsupported benchmark kind");
    var measurements = new List<object>();
    for (int round=0; round<6; round++)
    {
        bool bounded = round%2 == 1;
        var measuredSource = MakeSource(bounded, forceLegacy: !bounded);
        var context = await measuredSource.GetCurrentContextAsync(cts.Token);
        if(context.PlayerTileX is int hx && context.PlayerTileY is int hy)
            await measuredSource.JumpToCoordinateAsync(context.ServerId,hx,hy,cts.Token);
        await Task.Delay(750,cts.Token);
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
            await Call("map_scan_start",new{selectedTypes=kinds,scanMode="normal"});
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
            measurements.Add(new{round,boundedLens=bounded,seconds=timer.Elapsed.TotalSeconds,terminal,summary,cameraBefore,cameraAfter});
            File.WriteAllText(Path.Combine(root,"benchmark.json"),JsonSerializer.Serialize(measurements,new JsonSerializerOptions(JsonOptions.Default){WriteIndented=true}));
            Console.WriteLine($"MAP014_BENCHMARK {workload} round={round} lens={(bounded?20:120)} seconds={timer.Elapsed.TotalSeconds:F3}");
        }
        finally
        {
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
