using System.Collections.Concurrent;
using System.Text.Json;
using LWBridge.MapTrace;

// Inert API-shaped objects. Executes the real reader, no game/native positives.
var map = new Sfs(new() { ["c"] = "world.get.view", ["p"] = new Sfs(new() {
    ["indices"] = new[] { 4245 }, ["token"] = "PRIVATE_VALUE_MUST_NOT_ESCAPE", ["odd\tkey"] = 1 }) });
var login = new Sfs(new() { ["c"] = "login", ["p"] = new Sfs(new() { ["token"] = "PRIVATE_LOGIN" }) });
var malformed = new Sfs(new() { ["c"] = "world.secret:PRIVATE_COMMAND", ["p"] = new Sfs(new()) });
var manager = new Manager();
manager.m_proxy.profiler._sendData.TryAdd(new Packet { info = map }, new object());
manager.m_proxy.profiler._sendData.TryAdd(new Packet { info = login }, new object());
manager.m_proxy.profiler._sendData.TryAdd(new Packet { info = malformed }, new object());
string output = Sampler.Read(manager);
using var parsed = JsonDocument.Parse(output);
if (parsed.RootElement.GetProperty("sendScanned").GetInt32() != 3 ||
    parsed.RootElement.GetProperty("packets").GetArrayLength() != 1 ||
    output.Contains("PRIVATE_VALUE") || output.Contains("PRIVATE_LOGIN") ||
    output.Contains("PRIVATE_COMMAND")) throw new Exception("sampler admission/redaction failure");
var runtimeSource = parsed.RootElement.GetProperty("runtimeSource");
var start = runtimeSource.EnumerateArray().Single(v => v.GetProperty("method").GetString() == "StartViewRequest");
var raw = Convert.FromBase64String(start.GetProperty("ilBase64").GetString()!);
if (raw.Length != start.GetProperty("byteLength").GetInt32() ||
    !start.GetProperty("references").EnumerateArray().Any(v => v.GetProperty("name").GetString() == "WorldPointManager::_started"))
    throw new Exception("runtime source reference/body retention failed");
using var missing = JsonDocument.Parse(Sampler.Read(new object()));
if (missing.RootElement.GetProperty("packets").GetArrayLength() != 0)
    throw new Exception("missing provider produced packets");
var block = new Sfs(new() { ["c"] = "world.get.block", ["p"] = new Sfs(new() {
    ["serverPointArr"] = new SfsArray([new Sfs(new() { ["server"] = 2212, ["index"] = 4245,
        ["pointArr"] = new SfsArray([new Sfs(new() { ["token"] = "PRIVATE_SEGMENT" })]) })]) }) });
manager.m_proxy.profiler._receiveData.TryAdd(new Packet { info = block }, new object());
using var segments = JsonDocument.Parse(Sampler.Read(manager));
var row = segments.RootElement.GetProperty("packets").EnumerateArray().Single(x => x.GetProperty("command").GetString() == "world.get.block");
var segment = row.GetProperty("segments").GetProperty("entries")[0];
if (segment.GetProperty("numbers").GetProperty("server").GetInt32() != 2212 ||
    segment.GetProperty("arrayCounts").GetProperty("pointArr").GetInt32() != 1 ||
    segments.RootElement.GetProperty("receiveEnumerationTruncated").GetBoolean() ||
    segments.RootElement.GetRawText().Contains("PRIVATE_SEGMENT")) throw new Exception("segment metadata failure");
for (int i = 0; i < 513; i++) manager.m_proxy.profiler._sendData.TryAdd(new Packet(), new object());
using var bounded = JsonDocument.Parse(Sampler.Read(manager));
if (!bounded.RootElement.GetProperty("sendEnumerationTruncated").GetBoolean() ||
    bounded.RootElement.GetProperty("sendScanned").GetInt32() != 512) throw new Exception("missing enumeration truncation");
var dense = new Manager();
for (int i = 0; i < 121; i++) dense.m_proxy.profiler._receiveData.TryAdd(new Packet { info = map }, new object());
using var capped = JsonDocument.Parse(Sampler.Read(dense));
if (!capped.RootElement.GetProperty("packetRowsTruncated").GetBoolean() ||
    capped.RootElement.GetProperty("packets").GetArrayLength() != 120) throw new Exception("missing row truncation");
Console.WriteLine("MAP013_MANAGED_SAMPLER_INERT_OK enum=3 map=1 segments=1 secrets=0 missing=0 truncation=1");

var owned = new Manager();
string context = "r1|p1|s1|123|2212|0|545|495|0";
Packet Reply(int server = 2212, int x = 545, bool empty = false) => new() { info = new Sfs(new() {
    ["c"] = "world.get.block", ["p"] = new Sfs(new() { ["serverPointArr"] = new SfsArray([
        new Sfs(new() { ["serverId"] = server, ["worldId"] = 0, ["viewLvl"] = 0,
            ["x"] = x, ["y"] = 495, ["leftBottom"] = 450500, ["rightTop"] = 550600,
            ["points"] = new SfsArray(empty ? [] : [new Sfs(new())]) })]) }) }) };
owned.m_proxy.profiler._receiveData.TryAdd(Reply(), new object());
var scene=new Scene();
if (!Sampler.Begin(owned, context) || !Sampler.BindWorld(scene,context) || Sampler.Match(owned, context) != "waiting")
    throw new Exception("cached reply admitted");
if (Sampler.Begin(owned, "r2|p2|s2|456|2212|0|545|495|0") || Sampler.Retire("foreign"))
    throw new Exception("foreign rearm/retirement changed window");
owned.m_proxy.profiler._receiveData.TryAdd(Reply(server: 2213), new object());
owned.m_proxy.profiler._receiveData.TryAdd(Reply(x: 455), new object());
if (Sampler.Match(owned, context) != "waiting" || Sampler.Match(new Manager(), context) != "unarmed")
    throw new Exception("foreign reply/manager admitted");
owned.m_proxy.profiler._receiveData.TryAdd(Reply(empty: true), new object());
if (Sampler.Match(owned, context) != "matched" || !Sampler.Retire(context) ||
    Sampler.Match(owned, context) != "unarmed") throw new Exception("empty reply or retirement failed");
if (Sampler.Begin(new object(), context) || Sampler.Begin(owned, "invalid") ||
    Sampler.Begin(owned, "r1|p1|s1|123|2212|0|545|495|1")) throw new Exception("invalid admission");
var tooMany = new Manager();
for (int i = 0; i < 513; i++) tooMany.m_proxy.profiler._receiveData.TryAdd(new Packet(), new object());
if (Sampler.Begin(tooMany, context)) throw new Exception("incomplete baseline admitted");
Console.WriteLine("MAP013_NATIVE_REPLY_WINDOW_INERT_OK cached=reject foreign=reject empty=match retirement=exact bounds=reject");
var pendingCache=new Manager();var pendingScene=new Scene();
if (!Sampler.Begin(pendingCache,context) || !Sampler.BindWorld(pendingScene,context)) throw new Exception("cache check admission");
pendingCache.m_proxy.profiler._receiveData.TryAdd(Reply(),new object());
if (Sampler.Match(pendingCache,context)!="waiting") throw new Exception("unpopulated cache admitted");
pendingScene.PointManager._pointInfos.Add(495545,new CachedPoint {pointIndex=495545});
if (Sampler.Match(pendingCache,context)!="matched" || !Sampler.Retire(context)) throw new Exception("populated cache did not match");

public sealed class Manager { public Proxy m_proxy = new(); }
public sealed class Proxy { public Profiler profiler { get; } = new(); }
public sealed class Profiler
{
    public ConcurrentDictionary<Packet, object> _sendData = new();
    public ConcurrentDictionary<Packet, object> _receiveData = new();
}
public sealed class Packet { public Sfs? info { get; set; } }
public sealed class Sfs(Dictionary<string, object> values)
{
    public string? GetUtfString(string key) => values.GetValueOrDefault(key) as string;
    public Sfs? GetSFSObject(string key) => values.GetValueOrDefault(key) as Sfs;
    public SfsArray? GetSFSArray(string key) => values.GetValueOrDefault(key) as SfsArray;
    public int GetInt(string key) => (int)values[key];
    public IEnumerable<string> GetKeys() => values.Keys;
}
public sealed class SfsArray(Sfs[] values)
{
    public int Size() => values.Length;
    public Sfs GetSFSObject(int index) => values[index];
}
// Inert source-reflection control; not a game fixture or native witness.
public sealed class WorldPointManager
{
    private bool _started;
    public void StartViewRequest() => _started = true;
    public bool UpdateViewRequest(bool force) => force && _started;
}
public sealed class Scene
{
    public CacheManager PointManager {get;}=new();
    public Tile IndexToTilePos(int index)=>new() {x=index%1000,y=index/1000};
}
public sealed class Tile {public int x {get;set;} public int y {get;set;}}
public sealed class CachedPoint {public int pointIndex;}
public sealed class CacheManager {public Dictionary<int,CachedPoint> _pointInfos=new();}
