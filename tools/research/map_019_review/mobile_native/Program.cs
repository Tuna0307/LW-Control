using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using LWBridge.Desktop;
using Microsoft.Data.Sqlite;

// Private opt-in read-only experiment. Production rate/admission is unchanged.
// The external owned runner alone starts/stops the exact game and restores scripts.
if (args.Length != 2 || args[1] != "mobile-review") throw new ArgumentException("owner.json mobile-review");
string ownerPath = Path.GetFullPath(args[0]);
string root = Path.GetDirectoryName(ownerPath)!;
string runtime = Path.Combine(root,"overview-bridge"), probe = Path.Combine(root,"live-resource");
using var ownerDoc = JsonDocument.Parse(File.ReadAllText(ownerPath));
JsonElement owner = ownerDoc.RootElement;
string Text(string name) => owner.GetProperty(name).GetString()!;
var session = new OverviewMapScanSession(Text("profileId"),Text("sessionId"),Text("challenge"),
    owner.GetProperty("gamePid").GetInt32(),Text("gamePath"),Text("gameStartedAtUtc"));
using var lifetime = new CancellationTokenSource(TimeSpan.FromMinutes(29));
var json = new JsonSerializerOptions(JsonOptions.Default) { WriteIndented=true };
void Save(string name, object value) => File.WriteAllText(Path.Combine(root,name),JsonSerializer.Serialize(value,json));
bool ProcessMatches()
{
    try
    {
        using var game=Process.GetProcessById(session.GamePid);
        return !game.HasExited && string.Equals(game.MainModule?.FileName,session.GamePath,StringComparison.OrdinalIgnoreCase) &&
            Math.Abs((game.StartTime.ToUniversalTime()-DateTime.Parse(session.GameStartedAtUtc,CultureInfo.InvariantCulture).ToUniversalTime()).TotalMilliseconds)<=5;
    }
    catch (Exception e) when(e is ArgumentException or System.ComponentModel.Win32Exception or InvalidOperationException) {return false;}
}
long lastVerifiedHeartbeat=0;
OverviewMapScanSession? ReadSession()
{
    if(!ProcessMatches()) return null;
    try
    {
        using var h=JsonDocument.Parse(File.ReadAllText(Path.Combine(runtime,"heartbeat.json")));
        if(!OverviewLifecycleService.HeartbeatMatches(h.RootElement,session.ProfileId,session.SessionId,
            session.Challenge,session.GamePid,DateTimeOffset.UtcNow.ToUnixTimeSeconds())) return null;
        lastVerifiedHeartbeat=Stopwatch.GetTimestamp(); return session;
    }
    catch(Exception e) when(e is IOException or JsonException)
    {
        return lastVerifiedHeartbeat!=0 && Stopwatch.GetElapsedTime(lastVerifiedHeartbeat)<TimeSpan.FromSeconds(5) ? session:null;
    }
}
void RequireOwner() {if(ReadSession()!=session) throw new InvalidOperationException("Exact native owner/session retired");}
async Task Healthy(OverviewMapScanSession expected,CancellationToken token)
{
    var timer=Stopwatch.StartNew();
    while(timer.Elapsed<TimeSpan.FromSeconds(180))
    {
        token.ThrowIfCancellationRequested(); RequireOwner();
        try
        {
            using var d=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(runtime,"heartbeat.json"),token));
            bool True(string key)=>d.RootElement.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.True;
            if(True("gameStateObserved")&&True("gameReady")&&True("loggedIn")&&True("connected")) return;
        }
        catch(IOException) {} catch(JsonException) {}
        await Task.Delay(250,token);
    }
    throw new TimeoutException("Owned game did not become healthy");
}
CurrentClientMapBlockSource Source(bool camera=false)=>new(ReadSession,runtime,probe,
    hooks:camera?new CurrentClientMapBlockSourceHooks{ForceLegacyFullWorldLens=true}:null,
    waitForHealthySession:Healthy,matchesOwnedSession:s=>s==session&&ProcessMatches(),
    assetCacheRoot:Path.Combine(root,"asset-cache"));
async Task<JsonElement> Camera(string label,CancellationToken token)
{
    RequireOwner(); string id="m19mobilecamera"+Guid.NewGuid().ToString("N");
    string command=Path.Combine(probe,"runtime-diagnostic.txt");
    if(File.Exists(command)) throw new InvalidOperationException("Another runtime diagnostic is pending");
    string tmp=command+"."+id;
    await File.WriteAllLinesAsync(tmp,new[]{"schema=1","probeVersion=lwbridge-live-resource-probe-2",
        $"requestId={id}",$"profileId={session.ProfileId}",$"launchSessionId={session.SessionId}",
        $"challenge={session.Challenge}",$"gamePid={session.GamePid}","mode=camera_checkpoint",""},token);
    File.Move(tmp,command);
    var timer=Stopwatch.StartNew();
    while(timer.Elapsed<TimeSpan.FromSeconds(15))
    {
        token.ThrowIfCancellationRequested(); RequireOwner();
        try
        {
            using var d=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(probe,"runtime-diagnostic-result.json"),token));
            JsonElement r=d.RootElement;
            if(r.GetProperty("requestId").GetString()==id)
            {
                if(r.GetProperty("profileId").GetString()!=session.ProfileId || r.GetProperty("launchSessionId").GetString()!=session.SessionId ||
                    r.GetProperty("challenge").GetString()!=session.Challenge || r.GetProperty("gamePid").GetInt32()!=session.GamePid ||
                    r.GetProperty("state").GetString()!="proven") throw new InvalidDataException("Foreign/failed native camera checkpoint");
                Save(label+"-camera.json",r); return r.Clone();
            }
        }
        catch(IOException) {} catch(JsonException) {}
        await Task.Delay(100,token);
    }
    throw new TimeoutException("Native camera checkpoint timed out");
}
MethodInfo Private(string name,bool isStatic)=>typeof(CurrentClientMapBlockSource).GetMethod(name,
    BindingFlags.NonPublic|(isStatic?BindingFlags.Static:BindingFlags.Instance)) ?? throw new MissingMethodException(name);
object? Invoke(MethodInfo method,object? target,params object?[] arguments)
{
    try{return method.Invoke(target,arguments);}
    catch(TargetInvocationException e) when(e.InnerException is not null){ExceptionDispatchInfo.Capture(e.InnerException).Throw();throw;}
}
void Validate(JsonElement r,int rate,int fenceSize)
{
    bool Int(string k,int expected)=>r.GetProperty(k).GetInt32()==expected;
    int sent=r.GetProperty("requestsSent").GetInt32(),passes=r.GetProperty("repairPasses").GetInt32(),pushes=r.GetProperty("pushesObserved").GetInt32();
    if(r.GetProperty("state").GetString()!="proven" || !Int("initialRequestsSent",625) || !Int("rectanglesCompleted",625) ||
        !Int("unprovenRectangles",0) || !Int("requestsPerSecond",rate) || !Int("responseFenceSize",fenceSize) ||
        r.GetProperty("responseAttribution").GetString()!="bounded_sequential_fences" ||
        passes is <0 or >2 || sent<625 || sent>625*(passes+1) || !Int("repairRequestsSent",sent-625) ||
        pushes<625 || pushes>sent || !r.GetProperty("cameraStable").GetBoolean() || !r.GetProperty("curViewRestored").GetBoolean())
        throw new InvalidDataException("Mobile receipt did not prove625 rectangles, bounded repairs, rate and restoration");
    JsonElement rows=r.GetProperty("trainRecords");
    if(rows.ValueKind!=JsonValueKind.Array || rows.GetArrayLength()==0 || !Int("trainRecordCount",rows.GetArrayLength()))
        throw new InvalidDataException("Mobile receipt lacks positive typed source records");
}
List<MapStoredRecord> Normalize(JsonElement receipt,MapScanExecutionRequest request,long admission)
{
    var rows=new List<JsonElement>();
    foreach(JsonElement row in receipt.GetProperty("trainRecords").EnumerateArray())
    {
        if(!CurrentClientMapBlockSource.FastTruckArrivalIsAfter(row,admission)) continue;
        if(row.GetProperty("trainType").GetInt32()!=1 || !row.GetProperty("truckMetadataKnown").GetBoolean() ||
            row.GetProperty("trainQuality").GetInt32()<1 || row.GetProperty("carriageNum").GetInt32()<0 ||
            string.IsNullOrWhiteSpace(row.GetProperty("ownerName").GetString()) || row.GetProperty("arriveTs").GetInt64()<=0 ||
            row.GetProperty("power").GetInt64()<0 || row.GetProperty("truckCurrentGoodsRaw").ValueKind!=JsonValueKind.Array)
            throw new InvalidDataException("Mobile row lacks production metadata");
        rows.Add(row.Clone());
    }
    JsonElement wrapped=JsonSerializer.SerializeToElement(new{train_march_records=rows},json);
    DateTimeOffset captured=DateTimeOffset.Parse(receipt.GetProperty("capturedAt").GetString()!,CultureInfo.InvariantCulture);
    var prepared=(IEnumerable)Invoke(Private("PrepareTrainRecords",true),null,wrapped,request,
        Enumerable.Range(0,10000).ToHashSet(),captured,(long?)admission)!;
    var normalized=new List<MapStoredRecord>();
    MethodInfo enrich=Private("ApplyFinalTruckMetadataEnrichment",true);
    foreach(object item in prepared)
    {
        object enriched=Invoke(enrich,null,item)!;
        normalized.Add((MapStoredRecord)enriched.GetType().GetProperty("Record")!.GetValue(enriched)!);
    }
    if(normalized.Count==0 || normalized.Count!=rows.Count || normalized.Select(r=>r.RecordKey).Distinct().Count()!=normalized.Count)
        throw new InvalidDataException("Mobile production normalization rejected/duplicated rows");
    return normalized;
}
void SaveRows(int ordinal,IReadOnlyList<MapStoredRecord> rows)=>Save($"mobile-{ordinal}-normalized.json",
    rows.Select(r=>new{r.Kind,r.ServerId,r.RecordKey,r.Uuid,r.Name,r.AllianceName,r.Quality,r.Power,r.UpdatedAt,
        data=JsonSerializer.Deserialize<JsonElement>(r.DataJson)}));
IReadOnlyList<MapStoredRecord> ReadCameraRows(string database,int server)
{
    using var connection=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=database,Mode=SqliteOpenMode.ReadOnly}.ToString());
    connection.Open();using var command=connection.CreateCommand();
    command.CommandText="SELECT kind,server_id,record_key,point_index,uuid,name,alliance_name,level,quality,power,distance,shield_end_time,updated_at,data_json FROM map_records WHERE kind='truck' AND server_id=$server ORDER BY record_key";
    command.Parameters.AddWithValue("$server",server);using var reader=command.ExecuteReader();
    var rows=new List<MapStoredRecord>();
    while(reader.Read()) rows.Add(new MapStoredRecord(reader.GetString(0),reader.GetInt32(1),reader.GetString(2),
        reader.IsDBNull(3)?null:reader.GetInt32(3),reader.IsDBNull(4)?null:reader.GetString(4),
        reader.IsDBNull(5)?null:reader.GetString(5),reader.IsDBNull(6)?null:reader.GetString(6),
        reader.IsDBNull(7)?null:reader.GetInt32(7),reader.IsDBNull(8)?null:reader.GetInt32(8),
        reader.IsDBNull(9)?null:reader.GetInt64(9),reader.IsDBNull(10)?null:reader.GetDouble(10),
        reader.IsDBNull(11)?null:reader.GetInt64(11),reader.GetInt64(12),reader.GetString(13)));
    return rows;
}
var source=Source();
CurrentClientMapContext context=await source.GetCurrentContextAsync(lifetime.Token);
if(context.WorldId!=0 || context.TileWidth!=1000 || context.TileHeight!=1000) throw new InvalidDataException("Mobile review requires the current1000 normal-world server");
await Task.Delay(3000,lifetime.Token);
var measurements=new List<object>();
var snapshots=new List<(int ordinal,string method,long at,IReadOnlyList<MapStoredRecord> rows)>();
string[] methods=(Environment.GetEnvironmentVariable("LWBRIDGE_MAP019_MOBILE_METHODS") ??
    "20,camera,camera,20,15,15").Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
if(methods.Length is <2 or >8 || methods.Any(method=>method!="camera" &&
    (!int.TryParse(method,CultureInfo.InvariantCulture,out int rate) || rate is not (15 or 20 or 25 or 30))))
    throw new InvalidDataException("Mobile review methods must be camera and/or native rates 15,20,25,30 (2..8 rounds)");
string[] fenceTexts=(Environment.GetEnvironmentVariable("LWBRIDGE_MAP019_MOBILE_FENCES") ??
    string.Join(',',Enumerable.Repeat("25",methods.Length))).Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
if(fenceTexts.Length!=methods.Length || fenceTexts.Select(text=>int.TryParse(text,CultureInfo.InvariantCulture,out int fence)?fence:0)
    .Any(fence=>fence is not (5 or 10 or 25)))
    throw new InvalidDataException("Mobile review fences must provide one 5,10 or25 value per round");
int[] fences=fenceTexts.Select(text=>int.Parse(text,CultureInfo.InvariantCulture)).ToArray();
Save("mobile-review-plan.json",new{methods,fences,currentRate=20,currentFence=25,candidateRates=methods.Where(method=>method!="camera"&&method!="20").Distinct().ToArray(),candidateFences=fences.Where(fence=>fence!=25).Distinct().ToArray(),productionBehaviorChanged=false,
    hypothesis="Higher bounded initial send rates may reduce the 625-rectangle census floor while the existing fence/repair and restoration proof detects unsafe loss; no speed or accuracy verdict assumed.",
    nativeOnly=true,packagedUi=false,gameActions=0});
for(int ordinal=0;ordinal<methods.Length;ordinal++)
{
    RequireOwner(); string method=methods[ordinal];
    JsonElement before=await Camera($"mobile-{ordinal}-before",lifetime.Token);
    var timer=Stopwatch.StartNew();
    IReadOnlyList<MapStoredRecord> records;
    long admittedAt;
    if(method=="camera")
    {
        var cameraSource=Source(camera:true);
        string database=Path.Combine(root,$"mobile-{ordinal}-camera.db");
        using var commands=new Map317CommandService(database,new CurrentClientMap317ScanProvider(cameraSource),
            new CurrentClientMap317ActionProvider(cameraSource),false,cameraSource.GetMapStatusContextAsync);
        async Task<JsonElement> Call(string command)=>JsonSerializer.SerializeToElement(await commands.InvokeAsync(command,
            JsonSerializer.SerializeToElement(command=="map_scan_start"?(object)new{selectedTypes=new[]{"truck"},scanMode="fast"}:new{},json),lifetime.Token),json);
        JsonElement terminal=default;
        string metricsPath=Path.Combine(probe,"map-fullworld-probe-metrics.json");
        if(File.Exists(metricsPath)) File.Delete(metricsPath);
        try
        {
            await Call("map_scan_start");
            while(true)
            {
                RequireOwner();terminal=await Call("map_scan_status");
                if(!terminal.GetProperty("isReading").GetBoolean() && terminal.GetProperty("phase").GetString() is "completed" or "failed" or "cancelled") break;
                await Task.Delay(100,lifetime.Token);
            }
            timer.Stop(); Save($"mobile-{ordinal}-camera-terminal.json",terminal);
            if(terminal.GetProperty("phase").GetString()!="completed") throw new InvalidDataException("Camera Truck control failed");
            using var nativeMetrics=JsonDocument.Parse(await File.ReadAllTextAsync(metricsPath,lifetime.Token));
            JsonElement metrics=nativeMetrics.RootElement,identity=metrics.GetProperty("identity");
            if(metrics.GetProperty("phase").GetString()!="prepared" || metrics.GetProperty("uniqueCoveredAois").GetInt32()!=10000 ||
                identity.GetProperty("RunId").GetString()!=terminal.GetProperty("scanRunId").GetString() ||
                identity.GetProperty("ServerId").GetInt32()!=context.ServerId || identity.GetProperty("SelectedTypesKey").GetString()!="truck")
                throw new InvalidDataException("Camera control lacks exact-run10000-AOI native coverage metrics");
            Save($"mobile-{ordinal}-camera-metrics.json",metrics);
            records=ReadCameraRows(database,context.ServerId);
            admittedAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
        finally
        {
            using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(40));
            await commands.InvokeAsync("map_scan_stop",JsonSerializer.SerializeToElement(new{},json),stop.Token);
        }
        measurements.Add(new{ordinal,method,seconds=timer.Elapsed.TotalSeconds,records=records.Count,database,
            admittedAtUnixMs=admittedAt,scanRunId=terminal.GetProperty("scanRunId").GetString(),
            publicationScope="isolated diagnostic SQLite only",packagedUi=false});
    }
    else
    {
        int rate=int.Parse(method,CultureInfo.InvariantCulture),fenceSize=fences[ordinal];
        var request=new MapScanExecutionRequest("m19mobile"+Guid.NewGuid().ToString("N"),context.ServerId,0,1000,1000,
            ["truck"],1,PlayerTileX:context.PlayerTileX,PlayerTileY:context.PlayerTileY,LaunchSessionId:session.SessionId,LiveServerId:context.ServerId);
        using var round=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        var task=(Task<JsonElement>)Invoke(Private("ProbeFastTruckAsync",false),source,session,request,rate,round.Token,false,fenceSize)!;
        while(!task.IsCompleted)
        {
            if(ReadSession()!=session) round.Cancel();
            await Task.WhenAny(task,Task.Delay(200,CancellationToken.None));
        }
        JsonElement receipt=await task;timer.Stop();admittedAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Save($"mobile-{ordinal}-native-receipt.json",receipt);
        Validate(receipt,rate,fenceSize);records=Normalize(receipt,request,admittedAt);
        measurements.Add(new{ordinal,method="native-"+rate,rate,seconds=timer.Elapsed.TotalSeconds,
            admittedAtUnixMs=admittedAt,scanRunId=request.RunId,
            nativeRequestId=receipt.GetProperty("requestId").GetString(),
            nativeSeconds=receipt.GetProperty("elapsedSeconds").GetDouble(),records=records.Count,
            rawRecords=receipt.GetProperty("trainRecordCount").GetInt32(),
            fenceSize,requestsSent=receipt.GetProperty("requestsSent").GetInt32(),repairPasses=receipt.GetProperty("repairPasses").GetInt32(),
            repairRequestsSent=receipt.GetProperty("repairRequestsSent").GetInt32(),
            rawAcquisitionOnly=true,productionPublication=false,packagedUi=false});
    }
    RequireOwner();JsonElement after=await Camera($"mobile-{ordinal}-after",lifetime.Token);
    if(!JsonElement.DeepEquals(before.GetProperty("cameraSnapshot"),after.GetProperty("cameraSnapshot")))
        throw new InvalidDataException("Independent camera geometry differs after mobile round");
    SaveRows(ordinal,records); snapshots.Add((ordinal,method,admittedAt,records));
    Save("mobile-review.json",measurements);
    Console.WriteLine($"MAP019_MOBILE ordinal={ordinal} method={method} seconds={timer.Elapsed.TotalSeconds:F3} rows={records.Count}");
}
// Retain a finite identity/field matrix. Differences require natural-drift
// interpretation; this deliberately emits no automatic candidate acceptance.
var comparisons=new List<object>();
string[] fields=["ownerUid","ownerName","allianceUid","allianceName","quality","power","trainCfgId","carriageNum","currentGoods","arriveTs","endTime","robTimes","protectTime"];
for(int i=0;i<snapshots.Count;i++) for(int j=i+1;j<snapshots.Count;j++)
{
    var a=snapshots[i];var b=snapshots[j];var left=a.rows.ToDictionary(r=>r.RecordKey);var right=b.rows.ToDictionary(r=>r.RecordKey);
    string[] common=left.Keys.Intersect(right.Keys).ToArray();
    var differences=new Dictionary<string,int>();
    foreach(string field in fields)
    {
        int changes=0;
        foreach(string key in common)
        {
            using var x=JsonDocument.Parse(left[key].DataJson);using var y=JsonDocument.Parse(right[key].DataJson);
            bool hx=x.RootElement.TryGetProperty(field,out var xv),hy=y.RootElement.TryGetProperty(field,out var yv);
            if(hx!=hy || (hx&&!JsonElement.DeepEquals(xv,yv))) changes++;
        }
        differences[field]=changes;
    }
    string[] missing=left.Keys.Except(right.Keys).ToArray();
    int expired=missing.Count(key=>{using var d=JsonDocument.Parse(left[key].DataJson);return !CurrentClientMapBlockSource.FastTruckArrivalIsAfter(d.RootElement,b.at);});
    comparisons.Add(new{a=a.ordinal,b=b.ordinal,methodA=a.method,methodB=b.method,shared=common.Length,
        missing=missing.Length,missingExpiredByLaterAdmission=expired,missingStillUnexpired=missing.Length-expired,
        added=right.Keys.Except(left.Keys).Count(),fieldDifferencesAmongShared=differences,
        interpretation="Finite time-separated observations; new launches, arrivals, plunder/rewards and movement are natural drift. Compare same-method controls; no automatic recall/speed verdict."});
}
Save("mobile-field-drift-comparisons.json",comparisons);
Console.WriteLine("MAP019_MOBILE_DONE");
