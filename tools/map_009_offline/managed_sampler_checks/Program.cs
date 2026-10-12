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
