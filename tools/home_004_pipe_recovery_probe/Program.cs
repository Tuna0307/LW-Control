using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using LWBridge.Desktop;

// Real isolated Windows named pipes and the production host, handshake,
// registry and RPC transport. No game process, installed scripts or user pipe.
const string build = "home004-r3-r1-inert";
const string profile = "home004-isolated";
string executable = Environment.ProcessPath ?? throw new InvalidOperationException("Process image missing");
string name = "home004-r3-r1-" + Guid.NewGuid().ToString("N");
var registry = new LWBridgeControlPipeRegistry();
using var host = new LWBridgeControlPipeHostState(@"\\.\pipe\" + name, registry);
Task listener = host.StartRpcTransport(build, executable);

long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
var malformedBinding = host.PrepareLaunchBinding(profile, "bad-frame", build, Now());
int rejectionCount = 0;

await SendRejectedAsync(name, Hello("bad-frame", "invalid-token", Environment.ProcessId, build), ++rejectionCount);
await SendRejectedAsync(name, Hello("bad-frame", malformedBinding.PipeToken, Environment.ProcessId + 1, build), ++rejectionCount);
await SendRejectedAsync(name, Hello("bad-frame", malformedBinding.PipeToken, Environment.ProcessId, "different-build"), ++rejectionCount);
await SendRejectedAsync(name, Encoding.UTF8.GetBytes("{bad json"), ++rejectionCount);
await SendRejectedAsync(name, new byte[] { 0, 0, 0, 0 }, ++rejectionCount, rawFrame: true);
if (!host.IsRpcTransportStarted || listener.IsCompleted)
    throw new InvalidOperationException("Unauthenticated rejection stopped shared listener");
Console.WriteLine("PROBE_PHASE_REJECTIONS_OK");

using (var malformed = await ConnectHelloAsync(name, "bad-frame", malformedBinding.PipeToken))
{
    int failuresBefore = host.FailedRpcSessionCount;
    await malformed.WriteAsync(new byte[] { 0, 0, 0, 0 });
    await UntilAsync(() => host.FailedRpcSessionCount > failuresBefore &&
                           !host.IsRouteConnected("bad-frame"),
        "authenticated invalid frame did not retire its session");
    if (host.LastRpcSessionError != "InvalidDataException")
        throw new InvalidOperationException("Malformed authenticated frame was misclassified");
}
Console.WriteLine("PROBE_PHASE_MALFORMED_OK");

var original = host.PrepareLaunchBinding(profile, "original", build, Now());
ulong originalGeneration;
using (var idle = await ConnectHelloAsync(name, "original", original.PipeToken))
{
    originalGeneration = registry.Resolve("original")?.Generation ??
        throw new InvalidOperationException("Original route generation missing");
    int failuresBefore = host.FailedRpcSessionCount;
    // Do not alter the 30-second idle timer or close the client to force EOF.
    await UntilAsync(() => host.FailedRpcSessionCount > failuresBefore &&
                           !host.IsRouteConnected("original"),
        "30-second authenticated idle session did not retire", TimeSpan.FromSeconds(40));
    if (host.LastRpcSessionError != "OperationCanceledException")
        throw new InvalidOperationException("Idle reader cancellation was not session-scoped");
    if (listener.IsCompleted || !host.IsRpcTransportStarted || host.IsStopped)
        throw new InvalidOperationException("Idle session ended the shared listener");
}
Console.WriteLine("PROBE_PHASE_IDLE_OK");

await host.EnsureRpcTransportAsync(build, executable);
using (var reconnect = await ConnectHelloAsync(name, "original", original.PipeToken))
{
    ulong successorGeneration = registry.Resolve("original")?.Generation ?? 0;
    if (successorGeneration <= originalGeneration ||
        registry.RemoveConnected("original", originalGeneration) ||
        !host.IsRouteConnected("original"))
        throw new InvalidOperationException("Obsolete route generation retired the new connection");
}
await UntilAsync(() => !host.IsRouteConnected("original"), "EOF did not retire original route");
Console.WriteLine("PROBE_PHASE_RECONNECT_OK");

var successor = host.PrepareLaunchBinding(profile, "successor", build, Now());
using var second = await ConnectHelloAsync(name, "successor", successor.PipeToken);
const string otherProfile = "home004-isolated-B";
var concurrent = host.PrepareLaunchBinding(
    otherProfile, "other-owner", build, Now(), executable);
using var other = await ConnectHelloAsync(
    name, "other-owner", concurrent.PipeToken, otherProfile);
if (!host.IsRouteConnected("successor") || !host.IsRouteConnected("other-owner") ||
    host.ConnectedRouteCount != 2)
    throw new InvalidOperationException(
        "Two independent authenticated profile sessions did not coexist on one listener");
// The two actual OS pipe readers are live at the same time. A command sent to
// B must arrive on B's stream while A's authenticated session remains open.
using JsonDocument otherArgumentDocument = JsonDocument.Parse("{\"text\":\"other\"}");
Task<JsonElement?> otherRpc = host.CallLuaAsync(
    "other-owner", "isolated_echo", otherArgumentDocument.RootElement.Clone(),
    Now(), Now(), resultTimeout: TimeSpan.FromSeconds(5));
byte[] otherCommand = await ReadFrameAsync(other, TimeSpan.FromSeconds(3));
using JsonDocument otherEnvelopeDoc = JsonDocument.Parse(otherCommand);
string otherRequestId = otherEnvelopeDoc.RootElement.GetProperty("payload")
    .GetProperty("id").GetString()!;
await other.WriteAsync(LWBridgeControlPipeProtocol.EncodeFrame(
    JsonSerializer.SerializeToUtf8Bytes(new
    {
        version = 1, type = "result", profileId = otherProfile,
        instanceId = "other-owner", requestId = otherRequestId,
        timestamp = Now(), payload = new
        {
            id = otherRequestId, ok = true, result = new { echoed = "other" }
        }
    })));
JsonElement? otherResult = await otherRpc.WaitAsync(TimeSpan.FromSeconds(3));
if (otherResult?.GetProperty("echoed").GetString() != "other" ||
    !host.IsRouteConnected("successor"))
    throw new InvalidOperationException(
        "B's RPC incorrectly consumed, disconnected or rerouted A's session");
Console.WriteLine("PROBE_PHASE_TWO_PROFILE_CONCURRENT_RPC_OK");
using JsonDocument argumentDocument = JsonDocument.Parse("{\"text\":\"alive\"}");
JsonElement arguments = argumentDocument.RootElement.Clone();
Task<JsonElement?> rpc = host.CallLuaAsync("successor", "isolated_echo", arguments,
    Now(), Now(), resultTimeout: TimeSpan.FromSeconds(5));
byte[] command = await ReadFrameAsync(second, TimeSpan.FromSeconds(3));
using JsonDocument commandDoc = JsonDocument.Parse(command);
JsonElement envelope = commandDoc.RootElement;
if (envelope.GetProperty("type").GetString() != "command" ||
    envelope.GetProperty("payload").GetProperty("fn").GetString() != "isolated_echo")
    throw new InvalidOperationException("Host emitted wrong authenticated RPC command");
string id = envelope.GetProperty("payload").GetProperty("id").GetString()!;
byte[] response = JsonSerializer.SerializeToUtf8Bytes(new
{
    version = 1, type = "result", profileId = profile, instanceId = "successor",
    requestId = id, timestamp = Now(), payload = new { id, ok = true, result = new { echoed = "alive" } }
});
await second.WriteAsync(LWBridgeControlPipeProtocol.EncodeFrame(response));
JsonElement? result = await rpc.WaitAsync(TimeSpan.FromSeconds(3));
if (result?.GetProperty("echoed").GetString() != "alive")
    throw new InvalidOperationException("Authenticated successor RPC round-trip failed");
Console.WriteLine("PROBE_PHASE_RPC_OK");

Task<JsonElement?> unfinished = host.CallLuaAsync("successor", "held_shutdown", arguments,
    Now(), Now(), resultTimeout: TimeSpan.FromSeconds(10));
_ = await ReadFrameAsync(second, TimeSpan.FromSeconds(3));
int firstInstanceFlagUseCount = host.FirstInstanceFlagUseCount;
int failedRpcSessions = host.FailedRpcSessionCount;
host.Close();
await listener.WaitAsync(TimeSpan.FromSeconds(3));
if (!host.IsStopped || host.IsRpcTransportStarted || host.ConnectedRouteCount != 0)
    throw new InvalidOperationException("Explicit host Close left a connected route or listener");
try
{
    _ = await unfinished.WaitAsync(TimeSpan.FromSeconds(3));
    throw new InvalidOperationException("Pending RPC completed successfully across shutdown");
}
catch (BridgeCommandException e) when (e.Code == "APP_SHUTTING_DOWN") { }
Console.WriteLine("PROBE_PHASE_SHUTDOWN_OK");

// Identity/path mismatches are separate from transport errors. A valid token
// cannot override a different actual executable image on a different host.
string mismatchPipe = "home004-r3-r1-path-" + Guid.NewGuid().ToString("N");
using var wrongPath = new LWBridgeControlPipeHostState(@"\\.\pipe\" + mismatchPipe);
string existingNonExecutableImage = typeof(JsonDocument).Assembly.Location;
Task wrongListener = wrongPath.StartRpcTransport(build, existingNonExecutableImage);
var pathBinding = wrongPath.PrepareLaunchBinding(profile, "path-test", build, Now());
using (var wrong = new NamedPipeClientStream(".", mismatchPipe, PipeDirection.InOut, PipeOptions.Asynchronous))
{
    await wrong.ConnectAsync(5000);
    await wrong.WriteAsync(LWBridgeControlPipeProtocol.EncodeFrame(
        Hello("path-test", pathBinding.PipeToken, Environment.ProcessId, build)));
    await UntilAsync(() => wrongPath.RejectedHandshakeCount == 1, "Wrong client image was admitted");
    if (wrongPath.IsRouteConnected("path-test") || !wrongPath.IsRpcTransportStarted)
        throw new InvalidOperationException("Path mismatch compromised listener admission");
}
wrongPath.Close();
await wrongListener.WaitAsync(TimeSpan.FromSeconds(3));
Console.WriteLine("PROBE_PHASE_PATH_OK");

Console.WriteLine(JsonSerializer.Serialize(new
{
    proof = "HOME004_R3_R1_ISOLATED_PIPE_RECOVERY_OK",
    realWindowsNamedPipes = true,
    gameLaunches = 0,
    unauthenticatedRejections = rejectionCount,
    wrongImageRejected = true,
    authenticatedMalformedFrameRetired = true,
    idleTimeoutSeconds = 30,
    sharedListenerSurvivedIdle = true,
    freshHelloAckAndRpc = true,
    obsoleteGenerationPreservedSuccessor = true,
    explicitHostCloseRetiredPendingRpc = true,
    simultaneousAuthenticatedProfilesAndRpc = true,
    firstInstanceFlagUseCount,
    failedRpcSessions
}, new JsonSerializerOptions { WriteIndented = true }));

byte[] Hello(string instanceId, string token, int pid, string idBuild,
    string? alternateProfile = null) =>
    JsonSerializer.SerializeToUtf8Bytes(new
    {
        version = 1, type = "hello", profileId = alternateProfile ?? profile, instanceId,
        requestId = "", timestamp = Now(),
        payload = new { token, pid, buildId = idBuild }
    });

async Task SendRejectedAsync(string pipe, byte[] bytes, int expectedCount, bool rawFrame = false)
{
    using var client = new NamedPipeClientStream(".", pipe, PipeDirection.InOut, PipeOptions.Asynchronous);
    await client.ConnectAsync(5000);
    await client.WriteAsync(rawFrame ? bytes : LWBridgeControlPipeProtocol.EncodeFrame(bytes));
    await UntilAsync(() => host.RejectedHandshakeCount >= expectedCount,
        "Invalid handshake was not rejected");
    if (host.IsRouteConnected("bad-frame"))
        throw new InvalidOperationException("Invalid handshake admitted a route");
}

async Task<NamedPipeClientStream> ConnectHelloAsync(
    string pipe, string instanceId, string token, string? alternateProfile = null)
{
    var client = new NamedPipeClientStream(".", pipe, PipeDirection.InOut, PipeOptions.Asynchronous);
    try
    {
        await client.ConnectAsync(5000);
        await client.WriteAsync(LWBridgeControlPipeProtocol.EncodeFrame(
            Hello(instanceId, token, Environment.ProcessId, build, alternateProfile)));
        byte[] ack = await ReadFrameAsync(client, TimeSpan.FromSeconds(4));
        using JsonDocument json = JsonDocument.Parse(ack);
        if (json.RootElement.GetProperty("type").GetString() != "hello.ack" ||
            json.RootElement.GetProperty("instanceId").GetString() != instanceId)
            throw new InvalidOperationException("Missing fresh authenticated hello/ACK");
        await UntilAsync(() => host.IsRouteConnected(instanceId), "ACK had no authenticated route");
        return client;
    }
    catch { client.Dispose(); throw; }
}

static async Task<byte[]> ReadFrameAsync(NamedPipeClientStream client, TimeSpan timeout)
{
    using var cancel = new CancellationTokenSource(timeout);
    byte[] prefix = new byte[4];
    await client.ReadExactlyAsync(prefix, cancel.Token);
    int length = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(prefix));
    if (length <= 0 || length > LWBridgeControlPipeProtocol.MaxFramePayloadLength)
        throw new InvalidDataException("Unexpected pipe frame length");
    byte[] payload = new byte[length];
    await client.ReadExactlyAsync(payload, cancel.Token);
    return payload;
}

static async Task UntilAsync(Func<bool> predicate, string error, TimeSpan? timeout = null)
{
    DateTime until = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
    while (!predicate())
    {
        if (DateTime.UtcNow >= until)
            throw new TimeoutException(error);
        await Task.Delay(20);
    }
}
