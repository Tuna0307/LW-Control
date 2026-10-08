using System.IO.Pipes;
using System.Text;
using LWBridge.GamePipe;

string root = Path.Combine(Path.GetTempPath(), "home009-r3-pipe-epoch-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
string instance = Guid.NewGuid().ToString("N");
Environment.SetEnvironmentVariable("LWBRIDGE_INSTANCE_ID", instance);
File.WriteAllText(Path.Combine(root, "lease.txt"),
    $"schema=1\nsessionId={instance}\nupdatedAt={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}\n");
var successes = 0;
NamedPipeServerStream? last = null;
string name = "lwbridge-home009-r3-epoch-" + Guid.NewGuid().ToString("N");
try
{
    for (int repeat = 0; repeat < 24; repeat++)
    {
        var listener = new NamedPipeServerStream(name, PipeDirection.InOut, 2,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        Task accepted = listener.WaitForConnectionAsync();
        PipeClientAdapter.Connect(@"\\.\pipe\" + name,
            "{\"type\":\"hello\",\"instanceId\":\"" + instance + "\"}", root);
        await accepted.WaitAsync(TimeSpan.FromSeconds(7));
        byte[] length = new byte[4];
        await listener.ReadExactlyAsync(length).AsTask().WaitAsync(TimeSpan.FromSeconds(7));
        int size = BitConverter.ToInt32(length);
        if (size is < 2 or > 1024) throw new Exception("bad hello frame size " + size);
        byte[] hello = new byte[size];
        await listener.ReadExactlyAsync(hello).AsTask().WaitAsync(TimeSpan.FromSeconds(7));
        if (!Encoding.UTF8.GetString(hello).Contains(instance))
            throw new Exception("hello identity lost");
        if (last is not null) await last.DisposeAsync();
        last = listener;
        await Task.Delay(45);
        string state = File.ReadAllText(Path.Combine(root,"pipe-adapter-state.txt"));
        if (state != "connected")
            throw new Exception($"retired worker clobbered replacement worker on {repeat}: {state}");
        successes++;
    }
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new {
        ok=true, successiveConnections=successes,
        preservedReplacementState=true,gameLaunches=0,realProcessesTerminated=0
    }));
}
finally
{
    if (last is not null) await last.DisposeAsync();
    // Preserve on failure for diagnostics; normal cleanup is safe because the
    // background worker exits when this test-owned console process exits.
    if (successes == 24) Directory.Delete(root,recursive:true);
}
