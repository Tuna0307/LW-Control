using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text.Json;
using LWBridge.Desktop;

// Isolated real native listener/RPC idle-timeout probe. No LastWar, desktop
// input, installed files, owner's pipe or production profile/config data.
string name = "home004-lead-r3-idle-" + Guid.NewGuid().ToString("N");
using var host = new LWBridgeControlPipeHostState(pipePath: @"\\.\pipe\" + name);
string executable = Environment.ProcessPath!;
const string build = "isolated-lead-r3";
Task listener = host.StartRpcTransport(build, executable);
long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
var binding = host.PrepareLaunchBinding("isolated-probe", "first-session", build, now);
using var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
await client.ConnectAsync(5000);
byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new {
    version = LWBridgeControlPipeProtocol.ProtocolVersion,
    type = "hello", profileId = "isolated-probe", instanceId = "first-session",
    requestId = "", timestamp = now,
    payload = new { token = binding.PipeToken, pid = Environment.ProcessId, buildId = build }
});
await client.WriteAsync(LWBridgeControlPipeProtocol.EncodeFrame(payload));
byte[] prefix = new byte[4];
await client.ReadExactlyAsync(prefix);
byte[] ack = new byte[checked((int)BinaryPrimitives.ReadUInt32LittleEndian(prefix))];
await client.ReadExactlyAsync(ack);
for (int i = 0; i < 100 && host.AuthenticatedSessionCount < 1; i++)
    await Task.Delay(10);
if (host.AuthenticatedSessionCount != 1 || !host.IsRouteConnected("first-session"))
    throw new InvalidOperationException("Real isolated hello/ACK did not establish authenticated route");
Console.WriteLine(JsonSerializer.Serialize(new {
    phase = "authenticated-inert-client", authenticated = host.AuthenticatedSessionCount,
    routeConnected = host.IsRouteConnected("first-session"), gameLaunches = 0
}));
string? listenerError = null;
try { await listener.WaitAsync(TimeSpan.FromSeconds(40)); }
catch (Exception e) { listenerError = e.GetType().Name; }
bool completedBeforeHostStop = listener.IsCompleted;
bool hostReportedStopped = host.IsStopped;
await host.EnsureRpcTransportAsync(build, executable);
host.PrepareLaunchBinding("isolated-probe", "successor-session", build,
    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
bool successorConnected = false;
string? successorError = null;
using (var second = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous)) {
    try { await second.ConnectAsync(2000); successorConnected = true; }
    catch (Exception e) { successorError = e.GetType().Name; }
}
var result = new {
    phase = "idle-listener-survival-inverse", listenerError,
    listenerCompletedWithoutHostStop = completedBeforeHostStop,
    hostReportedStopped, transportStillReportedStarted = host.IsRpcTransportStarted,
    successorConnected, successorError, gameLaunches = 0,
    defectReproduced = completedBeforeHostStop && !hostReportedStopped && !successorConnected
};
string json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
Console.WriteLine(json);
File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "inverse-result.json"), json);
if (!result.defectReproduced) throw new InvalidOperationException("Expected inverse was not reproduced");
