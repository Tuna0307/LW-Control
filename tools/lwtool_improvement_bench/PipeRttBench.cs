using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using LWBridge.Desktop;

const string build = "lwtool-improvement-rtt-inert";
const string profile = "lwtool-improvement-inert";
const string instance = "rtt-session";
const int samples = 500;
string executable = Environment.ProcessPath ?? throw new InvalidOperationException("Process image missing");
string pipe = "lwtool-improvement-rtt-" + Guid.NewGuid().ToString("N");
var registry = new LWBridgeControlPipeRegistry();
using var host = new LWBridgeControlPipeHostState(@"\\.\pipe\" + pipe, registry);
Task listener = host.StartRpcTransport(build, executable);
long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
var binding = host.PrepareLaunchBinding(profile, instance, build, Now());
using NamedPipeClientStream client = await ConnectHelloAsync(pipe, binding.PipeToken);
using var responderCancellation = new CancellationTokenSource();
Task responder = RespondAsync(client, responderCancellation.Token);

var rtts = new double[samples];
using JsonDocument argumentsDocument = JsonDocument.Parse("{\"readOnly\":true}");
JsonElement arguments = argumentsDocument.RootElement.Clone();
for (int index = 0; index < samples; index++)
{
    var watch = Stopwatch.StartNew();
    JsonElement? result = await host.CallLuaAsync(
        instance, "isolated_echo", arguments, Now(), Now(), resultTimeout: TimeSpan.FromSeconds(2));
    watch.Stop();
    if (result?.GetProperty("readOnly").GetBoolean() != true)
        throw new InvalidOperationException("Inert echo returned the wrong result");
    rtts[index] = watch.Elapsed.TotalMilliseconds;
}

Array.Sort(rtts);
double Percentile(double p)
{
    int index = (int)Math.Ceiling((rtts.Length - 1) * p);
    return rtts[Math.Clamp(index, 0, rtts.Length - 1)];
}

ProcessSnapshot Snapshot()
{
    using Process process = Process.GetCurrentProcess();
    process.Refresh();
    return new ProcessSnapshot(
        process.TotalProcessorTime.TotalMilliseconds,
        process.PrivateMemorySize64,
        process.WorkingSet64,
        process.HandleCount,
        process.Threads.Count);
}

ProcessSnapshot before = Snapshot();
const int soakSeconds = 60;
var soakWatch = Stopwatch.StartNew();
int heartbeatsSent = 0;
while (soakWatch.Elapsed < TimeSpan.FromSeconds(soakSeconds))
{
    byte[] heartbeat = JsonSerializer.SerializeToUtf8Bytes(new
    {
        version = 1,
        type = "heartbeat",
        profileId = profile,
        instanceId = instance,
        requestId = "",
        timestamp = Now(),
        payload = new { readOnly = true }
    });
    await client.WriteAsync(LWBridgeControlPipeProtocol.EncodeFrame(heartbeat));
    heartbeatsSent++;
    await Task.Delay(TimeSpan.FromSeconds(5));
}
ProcessSnapshot after = Snapshot();
if (!host.IsRouteConnected(instance) || host.ConnectedRouteCount != 1)
    throw new InvalidOperationException("Authenticated route did not survive heartbeat-only soak");

responderCancellation.Cancel();
client.Dispose();
try { await responder.WaitAsync(TimeSpan.FromSeconds(3)); } catch (OperationCanceledException) { }
catch (IOException) { }
catch (ObjectDisposedException) { }
await UntilAsync(() => !host.IsRouteConnected(instance), "EOF did not retire inert RTT route");
host.Close();
await listener.WaitAsync(TimeSpan.FromSeconds(3));

Console.WriteLine(JsonSerializer.Serialize(new
{
    schemaVersion = 1,
    proof = "LWTOOL_IMPROVEMENT_INERT_PIPE_RTT_SOAK_OK",
    realWindowsNamedPipes = true,
    gameLaunches = 0,
    automationStarted = false,
    rtt = new
    {
        samples,
        p50Ms = Percentile(0.50),
        p95Ms = Percentile(0.95),
        p99Ms = Percentile(0.99),
        minMs = rtts[0],
        maxMs = rtts[^1],
    },
    authenticatedTransportSoak = new
    {
        seconds = soakSeconds,
        heartbeatsSent,
        cpuMs = after.CpuMs - before.CpuMs,
        privateBytesBefore = before.PrivateBytes,
        privateBytesAfter = after.PrivateBytes,
        privateBytesDelta = after.PrivateBytes - before.PrivateBytes,
        workingSetBefore = before.WorkingSet,
        workingSetAfter = after.WorkingSet,
        workingSetDelta = after.WorkingSet - before.WorkingSet,
        handlesBefore = before.Handles,
        handlesAfter = after.Handles,
        handlesDelta = after.Handles - before.Handles,
        threadsBefore = before.Threads,
        threadsAfter = after.Threads,
        threadsDelta = after.Threads - before.Threads,
        failedRpcSessions = host.FailedRpcSessionCount,
        note = "Production host + real named pipe + authenticated heartbeat only; no game/Lua/adapter and not a substitute for connected-game idle measurement."
    }
}, new JsonSerializerOptions { WriteIndented = true }));

byte[] Hello(string token) => JsonSerializer.SerializeToUtf8Bytes(new
{
    version = 1,
    type = "hello",
    profileId = profile,
    instanceId = instance,
    requestId = "",
    timestamp = Now(),
    payload = new { token, pid = Environment.ProcessId, buildId = build }
});

async Task<NamedPipeClientStream> ConnectHelloAsync(string pipeName, string token)
{
    var stream = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
    try
    {
        await stream.ConnectAsync(5000);
        await stream.WriteAsync(LWBridgeControlPipeProtocol.EncodeFrame(Hello(token)));
        byte[] ack = await ReadFrameAsync(stream, TimeSpan.FromSeconds(4), CancellationToken.None);
        using JsonDocument json = JsonDocument.Parse(ack);
        if (json.RootElement.GetProperty("type").GetString() != "hello.ack")
            throw new InvalidOperationException("Missing authenticated hello ACK");
        await UntilAsync(() => host.IsRouteConnected(instance), "ACK had no authenticated route");
        return stream;
    }
    catch
    {
        stream.Dispose();
        throw;
    }
}

async Task RespondAsync(NamedPipeClientStream stream, CancellationToken token)
{
    while (!token.IsCancellationRequested)
    {
        byte[] payload = await ReadFrameAsync(stream, TimeSpan.FromSeconds(30), token);
        using JsonDocument json = JsonDocument.Parse(payload);
        JsonElement root = json.RootElement;
        if (root.GetProperty("type").GetString() != "command")
            throw new InvalidDataException("Unexpected server frame in RTT responder");
        string requestId = root.GetProperty("payload").GetProperty("id").GetString()!;
        byte[] result = JsonSerializer.SerializeToUtf8Bytes(new
        {
            version = 1,
            type = "result",
            profileId = profile,
            instanceId = instance,
            requestId,
            timestamp = Now(),
            payload = new { id = requestId, ok = true, result = new { readOnly = true } }
        });
        await stream.WriteAsync(LWBridgeControlPipeProtocol.EncodeFrame(result), token);
    }
}

static async Task<byte[]> ReadFrameAsync(
    NamedPipeClientStream stream,
    TimeSpan timeout,
    CancellationToken outer)
{
    using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(outer);
    timeoutCancellation.CancelAfter(timeout);
    byte[] prefix = new byte[4];
    await stream.ReadExactlyAsync(prefix, timeoutCancellation.Token);
    int length = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(prefix));
    if (length <= 0 || length > LWBridgeControlPipeProtocol.MaxFramePayloadLength)
        throw new InvalidDataException("Unexpected frame length");
    byte[] payload = new byte[length];
    await stream.ReadExactlyAsync(payload, timeoutCancellation.Token);
    return payload;
}

static async Task UntilAsync(Func<bool> predicate, string error, TimeSpan? timeout = null)
{
    DateTime deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
    while (!predicate())
    {
        if (DateTime.UtcNow >= deadline) throw new TimeoutException(error);
        await Task.Delay(20);
    }
}

internal readonly record struct ProcessSnapshot(
    double CpuMs,
    long PrivateBytes,
    long WorkingSet,
    int Handles,
    int Threads);
