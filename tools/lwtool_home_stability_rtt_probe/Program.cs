using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using LWBridge.Desktop;

const int warmup = 20;
const int measured = 300;
const string build = "home-stability-rtt-v1";
const string profile = "home-stability-rtt";
const string route = "rtt";
string executable = Environment.ProcessPath ?? throw new InvalidOperationException("process image missing");
string pipeName = "lwbridge-home-stability-rtt-" + Guid.NewGuid().ToString("N");
using var host = new LWBridgeControlPipeHostState(@"\\.\pipe\" + pipeName);
Task listener = host.StartRpcTransport(build, executable);
long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
var binding = host.PrepareLaunchBinding(profile, route, build, Now(), executable);
using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
await client.ConnectAsync(5000);
await client.WriteAsync(LWBridgeControlPipeProtocol.EncodeFrame(JsonSerializer.SerializeToUtf8Bytes(new
{
    version = 1,
    type = "hello",
    profileId = profile,
    instanceId = route,
    requestId = "",
    timestamp = Now(),
    payload = new { token = binding.PipeToken, pid = Environment.ProcessId, buildId = build }
})));
_ = await ReadFrameAsync(client, TimeSpan.FromSeconds(3));
await UntilAsync(() => host.IsRouteConnected(route), "authenticated RTT route missing");

using var empty = JsonDocument.Parse("{}");
JsonElement emptyArgs = empty.RootElement.Clone();
var values = new List<double>(measured);
for (int index = 0; index < warmup + measured; index++)
{
    long now = Now();
    var stopwatch = Stopwatch.StartNew();
    Task<JsonElement?> pending = host.CallLuaAsync(
        route, "getStatus", emptyArgs, now, now,
        resultTimeout: TimeSpan.FromSeconds(2));
    byte[] frame = await ReadFrameAsync(client, TimeSpan.FromSeconds(2));
    using JsonDocument command = JsonDocument.Parse(frame);
    string requestId = command.RootElement.GetProperty("requestId").GetString()
        ?? throw new InvalidDataException("requestId missing");
    if (command.RootElement.GetProperty("payload").GetProperty("fn").GetString() != "getStatus")
        throw new InvalidDataException("unexpected RTT function");
    await client.WriteAsync(LWBridgeControlPipeProtocol.EncodeFrame(JsonSerializer.SerializeToUtf8Bytes(new
    {
        version = 1,
        type = "result",
        profileId = profile,
        instanceId = route,
        requestId,
        timestamp = Now(),
        payload = new { id = requestId, ok = true, result = new { ready = true } }
    })));
    JsonElement? result = await pending.WaitAsync(TimeSpan.FromSeconds(2));
    stopwatch.Stop();
    if (result?.GetProperty("ready").GetBoolean() != true)
        throw new InvalidDataException("RTT response correlation failed");
    if (index >= warmup) values.Add(stopwatch.Elapsed.TotalMilliseconds);
}

values.Sort();
double Percentile(double fraction)
{
    int index = (int)Math.Ceiling(fraction * values.Count) - 1;
    return values[Math.Clamp(index, 0, values.Count - 1)];
}
Console.WriteLine(JsonSerializer.Serialize(new
{
    proof = "LWTOOL_HOME_STABILITY_AUTHENTICATED_HOST_RTT_OK",
    realWindowsNamedPipe = true,
    authenticated = true,
    correlatedRequestReply = true,
    function = "getStatus",
    gameplayMutation = false,
    warmup,
    samples = values.Count,
    milliseconds = new
    {
        min = values[0],
        p50 = Percentile(0.50),
        p95 = Percentile(0.95),
        p99 = Percentile(0.99),
        max = values[^1],
        mean = values.Average(),
    }
}, new JsonSerializerOptions { WriteIndented = true }));

host.Close();
await listener.WaitAsync(TimeSpan.FromSeconds(3));

static async Task<byte[]> ReadFrameAsync(NamedPipeClientStream client, TimeSpan timeout)
{
    using var cancel = new CancellationTokenSource(timeout);
    byte[] prefix = new byte[4];
    await client.ReadExactlyAsync(prefix, cancel.Token);
    int length = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(prefix));
    if (length <= 0 || length > LWBridgeControlPipeProtocol.MaxFramePayloadLength)
        throw new InvalidDataException("unexpected pipe frame length");
    byte[] payload = new byte[length];
    await client.ReadExactlyAsync(payload, cancel.Token);
    return payload;
}

static async Task UntilAsync(Func<bool> predicate, string message)
{
    DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
    while (!predicate())
    {
        if (DateTime.UtcNow >= deadline) throw new TimeoutException(message);
        await Task.Delay(10);
    }
}
