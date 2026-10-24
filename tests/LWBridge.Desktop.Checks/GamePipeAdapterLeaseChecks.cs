using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Text;
using LWBridge.GamePipe;

namespace LWBridge.Desktop.Checks;

internal static class GamePipeAdapterLeaseChecks
{
    private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;

    public static void Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "lwbridge-adapter-lease-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string lease = Path.Combine(root, "lease.txt");
        const string instance = "lease-owner-A";
        try
        {
            WriteLease(lease, instance, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            Set("runtimeDirectory", root);
            Set("instanceId", instance);
            ResetThreadCache();

            Require(LeaseIsFresh(), "first complete exact-owner lease snapshot was rejected");

            // A cache hit must not reopen the lease. Holding an exclusive handle
            // distinguishes the new bounded-success cache from the previous 10 ms
            // read/parse loop without needing a live game or host.
            using (var held = new FileStream(lease, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var quick = Stopwatch.StartNew();
                Require(LeaseIsFresh(), "fresh exact-owner cached lease was not reused");
                quick.Stop();
                Require(quick.Elapsed < TimeSpan.FromMilliseconds(100),
                    "cached lease path attempted contended file I/O");
            }

            // Cache success cannot mask revocation of the same session. The old
            // implementation reread every loop; preserve that observation boundary
            // by invalidating the cache when the lease file disappears or is
            // replaced, even inside the 250 ms parse-cache window.
            File.Delete(lease);
            Require(!LeaseIsFresh(), "same-instance lease deletion remained fresh inside cache window");
            WriteLease(lease, instance, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            Require(LeaseIsFresh(), "rewritten fresh lease was not admitted after deletion");
            File.WriteAllText(lease,
                "sessionId=" + instance + "\nchallenge=test-only\nupdatedAt=" +
                (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 6L) + "\npadding=changed\n",
                new UTF8Encoding(false));
            Require(!LeaseIsFresh(), "same-instance stale lease replacement remained fresh inside cache window");

            WriteLease(lease, instance, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            Require(LeaseIsFresh(), "fresh lease was not restored after stale replacement test");

            // An instance change must retire the old cache immediately.
            Set("instanceId", "lease-owner-B");
            Require(!LeaseIsFresh(), "old instance cache crossed into successor identity");

            // Restore owner A, establish a fresh cache, then age the validation
            // window and make the prior published timestamp stale. A due read under
            // contention must fail closed; it must never return the cached value.
            Set("instanceId", instance);
            ResetThreadCache();
            WriteLease(lease, instance, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            Require(LeaseIsFresh(), "second exact-owner lease snapshot was rejected");
            Thread.Sleep(300);
            Set("lastValidatedLeaseUpdatedAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 6L);
            using (var held = new FileStream(lease, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var due = Stopwatch.StartNew();
                Require(!LeaseIsFresh(), "due contended revalidation reused stale cached lease");
                due.Stop();
                Require(due.Elapsed < TimeSpan.FromSeconds(1),
                    "known-stale contended lease did not fail closed promptly");
            }

            Console.WriteLine("GAME_PIPE_ADAPTER_LEASE_CACHE_OK 250ms exact-owner cache, successor fence, stale contention fail-closed");
        }
        finally
        {
            ResetThreadCache();
            Set("runtimeDirectory", string.Empty);
            Set("instanceId", string.Empty);
            try { Directory.Delete(root, recursive: true); } catch { }
        }

        RunDirectQueueChecks();
        RunDirectPipeIntegrationChecks();
        RunBlockedWriteRetirementChecks();
    }

    private static void RunBlockedWriteRetirementChecks()
    {
        const string owner = "blocked-write-owner";
        string root = Path.Combine(Path.GetTempPath(),
            "lwbridge-adapter-blocked-write-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string? previousInstance = Environment.GetEnvironmentVariable("LWBRIDGE_INSTANCE_ID");
        string? previousDiagnostic = Environment.GetEnvironmentVariable("LWBRIDGE_PIPE_DIAGNOSTIC");
        string? previousDiagnosticRoot = Environment.GetEnvironmentVariable("LWBRIDGE_REBUILD_DATA_ROOT");
        NamedPipeServerStream? blockedServer = null;
        Thread? blockedWorker = null;
        Task<string>? retirement = null;
        try
        {
            Environment.SetEnvironmentVariable("LWBRIDGE_INSTANCE_ID", owner);
            Environment.SetEnvironmentVariable("LWBRIDGE_PIPE_DIAGNOSTIC", "1");
            Environment.SetEnvironmentVariable("LWBRIDGE_REBUILD_DATA_ROOT", root);
            WriteLease(Path.Combine(root, "lease.txt"), owner,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            string pipeName = "lwbridge-blocked-write-" + Guid.NewGuid().ToString("N");
            blockedServer = new NamedPipeServerStream(pipeName, PipeDirection.InOut,
                1, PipeTransmissionMode.Byte, PipeOptions.None, 1024, 1024);
            Task waiting = Task.Run(blockedServer.WaitForConnection);
            Require(PipeClientAdapter.EnableDirectTransport(owner) == "ok", "blocked-write opt-in failed");
            PipeClientAdapter.Connect("\\\\.\\pipe\\" + pipeName, "{}", root);
            Require(waiting.Wait(TimeSpan.FromSeconds(4)), "blocked-write pipe did not connect");
            Require(ReadPipeFrame(blockedServer) == "{}", "blocked-write hello changed");
            int generation = ParseDirectGeneration(WaitForDirectState(owner, "connected"));
            blockedWorker = (Thread)Get("workerThread");

            // The peer stays connected but deliberately stops reading. A frame
            // much larger than its pipe quota must block the exact worker write.
            string payload = new string('x', 0x100000);
            Require(PipeClientAdapter.PublishOutbound(generation + "\n" + owner + "\n" + payload) == "ok",
                "blocked-write payload was rejected");
            var dequeueDeadline = Stopwatch.StartNew();
            while (QueueCount("outboundFrames") != 0 && dequeueDeadline.Elapsed < TimeSpan.FromSeconds(2))
                Thread.Sleep(5);
            Require(QueueCount("outboundFrames") == 0 && !blockedWorker.Join(100),
                "blocked-write worker never reached the held pipe write");

            // Run retirement off the check runner so the old implementation
            // fails within a bound; finally closes the peer to release it.
            retirement = Task.Run(() => PipeClientAdapter.Retire(generation + "\n" + owner));
            Require(retirement.Wait(TimeSpan.FromSeconds(2)) && retirement.Result == "ok",
                "Retire blocked on an unresponsive peer/stream flush");
            Require(blockedWorker.Join(TimeSpan.FromSeconds(2)),
                "Retire returned but left the exact old worker blocked in pipe I/O");
            string receipt = Path.Combine(root, "overview-bridge", "pipe-native-receipts.txt");
            var receiptDeadline = Stopwatch.StartNew();
            bool cancellationObserved = false;
            while (!cancellationObserved && receiptDeadline.Elapsed < TimeSpan.FromSeconds(2))
            {
                try
                {
                    cancellationObserved = File.ReadAllText(receipt)
                        .Contains("|stage=retired_io_cancelled", StringComparison.Ordinal);
                }
                catch (IOException) { }
                if (!cancellationObserved) Thread.Sleep(5);
            }
            Require(cancellationObserved,
                "exact blocked write did not report a successful CancelIoEx request");
            Require(!blockedServer.SafePipeHandle.IsClosed,
                "blocked-write cancellation required closing the peer");

            // Keep the old server alive while proving successor traffic and
            // stale retirement fencing; old cancellation must use its own handle.
            WriteLease(Path.Combine(root, "lease.txt"), owner,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            string successorName = "lwbridge-blocked-successor-" + Guid.NewGuid().ToString("N");
            using var successor = new NamedPipeServerStream(successorName, PipeDirection.InOut,
                1, PipeTransmissionMode.Byte, PipeOptions.None);
            Task successorWaiting = Task.Run(successor.WaitForConnection);
            Require(PipeClientAdapter.EnableDirectTransport(owner) == "ok", "successor opt-in failed");
            PipeClientAdapter.Connect("\\\\.\\pipe\\" + successorName, "{}", root);
            Require(successorWaiting.Wait(TimeSpan.FromSeconds(4)), "successor pipe did not connect");
            Require(ReadPipeFrame(successor) == "{}", "successor hello changed");
            int successorGeneration = ParseDirectGeneration(WaitForDirectState(owner, "connected"));
            Thread successorWorker = (Thread)Get("workerThread");
            Require(PipeClientAdapter.Retire(generation + "\n" + owner) == "stale",
                "old retirement affected the successor");
            Require(PipeClientAdapter.PublishOutbound(successorGeneration + "\n" + owner + "\n{\"ok\":true}") == "ok" &&
                    ReadPipeFrame(successor) == "{\"ok\":true}",
                "old cancellation corrupted successor delivery");
            Require(PipeClientAdapter.Retire(successorGeneration + "\n" + owner) == "ok" &&
                    successorWorker.Join(TimeSpan.FromSeconds(2)), "successor did not retire cleanly");
            Console.WriteLine("GAME_PIPE_ADAPTER_BLOCKED_WRITE_RETIRE_OK bounded retire, CancelIoEx=true, exact worker exit, peer alive, successor fence");
        }
        finally
        {
            // This release is essential for bounded failure on the old code.
            blockedServer?.Dispose();
            retirement?.Wait(TimeSpan.FromSeconds(2));
            blockedWorker?.Join(TimeSpan.FromSeconds(2));
            try
            {
                int generation = (int)Get("connectionGeneration");
                if ((string)Get("instanceId") == owner)
                    _ = PipeClientAdapter.Retire(generation + "\n" + owner);
            }
            finally
            {
                Environment.SetEnvironmentVariable("LWBRIDGE_INSTANCE_ID", previousInstance);
                Environment.SetEnvironmentVariable("LWBRIDGE_PIPE_DIAGNOSTIC", previousDiagnostic);
                Environment.SetEnvironmentVariable("LWBRIDGE_REBUILD_DATA_ROOT", previousDiagnosticRoot);
                ResetDirect(string.Empty, 0, enabled: false, running: false);
                Set("directTransportRequestedInstance", string.Empty);
                Set("runtimeDirectory", string.Empty);
                Set("instanceId", string.Empty);
                Set("transportState", "idle");
                Set("failedGeneration", 0);
                try { Directory.Delete(root, recursive: true); } catch { }
            }
        }
    }

    private static void RunDirectQueueChecks()
    {
        const string owner = "queue-owner-A";
        const int generation = 41;
        string? previousInstance = Environment.GetEnvironmentVariable("LWBRIDGE_INSTANCE_ID");
        try
        {
            Environment.SetEnvironmentVariable("LWBRIDGE_INSTANCE_ID", owner);
            Require(PipeClientAdapter.EnableDirectTransport("foreign-owner") == "stale",
                "direct transport opt-in accepted a foreign environment owner");
            Require(PipeClientAdapter.EnableDirectTransport(owner) == "ok",
                "direct transport opt-in rejected the exact environment owner");

            ResetDirect(owner, generation);
            Require(PipeClientAdapter.ReadTransportState(owner) == "ok\n41\nconnected",
                "direct state did not publish exact generation/state");
            Require(PipeClientAdapter.ReadTransportState("foreign-owner") == "unavailable",
                "direct state crossed owner identity");

            Require(EnqueueInbound(generation, "{\"n\":1}"), "first direct inbound frame was rejected");
            Require(EnqueueInbound(generation, "{\"n\":2}"), "second direct inbound frame was rejected");
            Require(PipeClientAdapter.TryDequeueInbound(owner) == "frame\n41\n{\"n\":1}",
                "direct inbound FIFO order changed");
            Require(PipeClientAdapter.TryDequeueInbound(owner) == "frame\n41\n{\"n\":2}",
                "direct inbound second frame changed");
            Require(PipeClientAdapter.TryDequeueInbound(owner) == "empty\n41",
                "direct inbound dequeue was not at-most-once");

            AutoResetEvent outboundWake =
                (AutoResetEvent)(typeof(PipeClientAdapter).GetField("outboundSignal", StaticPrivate)!
                    .GetValue(null) ?? throw new InvalidOperationException("outbound signal missing"));
            outboundWake.Reset();
            Require(PipeClientAdapter.PublishOutbound("41\n" + owner + "\n{\"reply\":1}") == "ok",
                "exact generation outbound publication failed");
            Require(outboundWake.WaitOne(0), "outbound publication did not signal the adapter worker");
            Require(QueueCount("outboundFrames") == 1,
                "outbound publication did not enqueue exactly once");
            Require(PipeClientAdapter.PublishOutbound("40\n" + owner + "\n{\"stale\":1}") == "stale" &&
                    QueueCount("outboundFrames") == 1,
                "stale generation publication mutated the current queue");

            ResetDirect(owner, generation);
            for (int index = 0; index < 0x1000; index++)
                Require(PipeClientAdapter.PublishOutbound("41\n" + owner + "\n{}") == "ok",
                    "outbound item capacity rejected item " + index);
            Require(QueueCount("outboundFrames") == 0x1000,
                "outbound queue did not reach the 4096-item bound");
            Require(PipeClientAdapter.PublishOutbound("41\n" + owner + "\n{}") == "overflow",
                "outbound +1 item did not fail the current generation");
            Require(!(bool)Get("running") && QueueCount("outboundFrames") == 0 &&
                    PipeClientAdapter.ReadTransportState(owner) ==
                    "ok\n41\nerror:direct_outbound_overflow",
                "outbound overflow did not fail/clear only the current generation");

            ResetDirect(owner, generation);
            for (int index = 0; index < 0x1000; index++)
                Require(EnqueueInbound(generation, "{}"),
                    "inbound item capacity rejected item " + index);
            Require(QueueCount("inboundFrames") == 0x1000,
                "inbound queue did not reach the 4096-item bound");
            Require(!EnqueueInbound(generation, "{}"),
                "inbound +1 item did not fail the current generation");
            Require(!(bool)Get("running") && QueueCount("inboundFrames") == 0 &&
                    PipeClientAdapter.ReadTransportState(owner) ==
                    "ok\n41\nerror:direct_inbound_overflow",
                "inbound overflow did not fail/clear only the current generation");

            ResetDirect(owner, generation);
            Set("outboundQueuedBytes", 0x02000000L - 5L);
            Require(PipeClientAdapter.PublishOutbound("41\n" + owner + "\n{}") == "overflow" &&
                    !(bool)Get("running"),
                "32 MiB outbound byte budget did not fail closed");

            ResetDirect(owner, generation);
            Set("inboundQueuedBytes", 0x04000000L - 5L);
            Require(!EnqueueInbound(generation, "{}") && !(bool)Get("running"),
                "64 MiB inbound byte budget did not fail closed");

            ResetDirect(owner, generation);
            string maximum = new string('x', 0x800000);
            Require(PipeClientAdapter.PublishOutbound("41\n" + owner + "\n" + maximum) == "ok",
                "exact 8 MiB transport payload was rejected");
            ResetDirect(owner, generation);
            string oversized = new string('x', 0x800001);
            Require(PipeClientAdapter.PublishOutbound("41\n" + owner + "\n" + oversized) ==
                    "frame_length_invalid" && !(bool)Get("running") &&
                    PipeClientAdapter.ReadTransportState(owner) ==
                    "ok\n41\nerror:frame_length_invalid",
                ">8 MiB transport payload did not fail the current generation");

            ResetDirect(owner, generation);
            Require(EnqueueInbound(generation, "{\"held\":true}"),
                "retire setup inbound enqueue failed");
            Require(PipeClientAdapter.PublishOutbound("41\n" + owner + "\n{\"held\":true}") == "ok",
                "retire setup outbound enqueue failed");
            Require(PipeClientAdapter.Retire("41\n" + owner) == "ok" &&
                    !(bool)Get("running") && QueueCount("inboundFrames") == 0 &&
                    QueueCount("outboundFrames") == 0,
                "exact generation retire did not stop and clear both queues");

            ResetDirect(owner, generation + 1);
            Require(PipeClientAdapter.Retire("41\n" + owner) == "stale" &&
                    (bool)Get("running") &&
                    PipeClientAdapter.ReadTransportState(owner) == "ok\n42\nconnected",
                "stale retire affected the successor generation");
            Require(PipeClientAdapter.PublishOutbound("41\n" + owner + "\n{}") == "stale" &&
                    PipeClientAdapter.PublishOutbound("42\n" + owner + "\n{}") == "ok",
                "successor generation publication fence failed");

            Console.WriteLine(
                "GAME_PIPE_ADAPTER_DIRECT_QUEUE_OK fifo, at-most-once, 8MiB, 4096 items, 64/32MiB, wake, overflow, retire/successor fence");
        }
        finally
        {
            Environment.SetEnvironmentVariable("LWBRIDGE_INSTANCE_ID", previousInstance);
            ResetDirect(string.Empty, 0, enabled: false, running: false);
            Set("directTransportRequestedInstance", string.Empty);
            Set("transportState", "idle");
            Set("failedGeneration", 0);
        }
    }

    private static void RunDirectPipeIntegrationChecks()
    {
        const string owner = "queue-pipe-owner";
        string root = Path.Combine(Path.GetTempPath(),
            "lwbridge-adapter-direct-pipe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string? previousInstance = Environment.GetEnvironmentVariable("LWBRIDGE_INSTANCE_ID");
        try
        {
            Environment.SetEnvironmentVariable("LWBRIDGE_INSTANCE_ID", owner);
            WriteLease(Path.Combine(root, "lease.txt"), owner,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            string directPipeName = "lwbridge-adapter-direct-" + Guid.NewGuid().ToString("N");
            using (var server = new NamedPipeServerStream(
                       directPipeName,
                       PipeDirection.InOut,
                       1,
                       PipeTransmissionMode.Byte,
                       PipeOptions.None))
            {
                Task waiting = Task.Run(server.WaitForConnection);
                Require(PipeClientAdapter.EnableDirectTransport(owner) == "ok",
                    "direct integration opt-in failed");
                const string hello = "{\"type\":\"test.hello\"}";
                PipeClientAdapter.Connect("\\\\.\\pipe\\" + directPipeName, hello, root);
                Require(waiting.Wait(TimeSpan.FromSeconds(4)),
                    "direct integration pipe did not connect");
                Require(ReadPipeFrame(server) == hello,
                    "direct integration hello frame changed");
                string directState = WaitForDirectState(owner, "connected");
                int directGeneration = ParseDirectGeneration(directState);

                const string inbound =
                    "{\"version\":1,\"type\":\"hello.ack\",\"profileId\":\"test\",\"instanceId\":\"queue-pipe-owner\",\"requestId\":\"\",\"timestamp\":1,\"payload\":{}}";
                try
                {
                    WritePipeFrame(server, inbound);
                }
                catch (IOException error)
                {
                    throw new InvalidOperationException(
                        "direct integration server write failed; state=" +
                        PipeClientAdapter.ReadTransportState(owner),
                        error);
                }
                string dequeued = WaitForDirectInbound(owner);
                Require(dequeued == "frame\n" + directGeneration + "\n" + inbound,
                    "direct integration inbound raw frame changed");
                Require(!Directory.GetFiles(root, "pipe-inbound-*.json").Any(),
                    "direct integration unexpectedly published an inbound mailbox file");

                const string outbound =
                    "{\"version\":1,\"type\":\"result\",\"profileId\":\"test\",\"instanceId\":\"queue-pipe-owner\",\"requestId\":\"cmd_1\",\"timestamp\":2,\"payload\":{\"id\":\"cmd_1\",\"ok\":true,\"result\":{}}}";
                Require(PipeClientAdapter.PublishOutbound(
                            directGeneration + "\n" + owner + "\n" + outbound) == "ok",
                    "direct integration outbound publication failed");
                Require(ReadPipeFrame(server) == outbound,
                    "direct integration outbound raw frame changed");
                Require(!Directory.GetFiles(root, "pipe-outbound-*.json").Any(),
                    "direct integration unexpectedly published an outbound mailbox file");
                Require(PipeClientAdapter.Retire(directGeneration + "\n" + owner) == "ok",
                    "direct integration exact-generation retire failed");
            }

            // A caller that cannot resolve/enable the direct delegate family must
            // retain the old numbered-file path rather than receiving duplicate
            // direct+file delivery.
            WriteLease(Path.Combine(root, "lease.txt"), owner,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            string fallbackPipeName = "lwbridge-adapter-fallback-" + Guid.NewGuid().ToString("N");
            using (var server = new NamedPipeServerStream(
                       fallbackPipeName,
                       PipeDirection.InOut,
                       1,
                       PipeTransmissionMode.Byte,
                       PipeOptions.None))
            {
                Task waiting = Task.Run(server.WaitForConnection);
                const string hello = "{\"type\":\"test.fallback.hello\"}";
                PipeClientAdapter.Connect("\\\\.\\pipe\\" + fallbackPipeName, hello, root);
                Require(waiting.Wait(TimeSpan.FromSeconds(4)),
                    "fallback integration pipe did not connect");
                Require(ReadPipeFrame(server) == hello,
                    "fallback integration hello frame changed");

                const string inbound = "{\"type\":\"fallback.inbound\"}";
                WritePipeFrame(server, inbound);
                string inboundPath = Path.Combine(root, "pipe-inbound-00000001.json");
                Require(WaitForFile(inboundPath) &&
                        File.ReadAllText(inboundPath, Encoding.UTF8) == inbound,
                    "fallback integration inbound mailbox was not published exactly once");

                const string outbound = "{\"type\":\"fallback.outbound\"}";
                File.WriteAllText(Path.Combine(root, "pipe-outbound-00000001.json"),
                    outbound, new UTF8Encoding(false));
                Require(ReadPipeFrame(server) == outbound,
                    "fallback integration outbound mailbox was not delivered");

                int fallbackGeneration = (int)Get("connectionGeneration");
                Require(PipeClientAdapter.Retire(fallbackGeneration + "\n" + owner) == "ok",
                    "fallback integration exact-generation retire failed");
            }

            Console.WriteLine(
                "GAME_PIPE_ADAPTER_DIRECT_PIPE_OK real inert pipe direct raw FIFO/no-mailbox + legacy mailbox fallback");
        }
        finally
        {
            try
            {
                int generation = (int)Get("connectionGeneration");
                string currentOwner = (string)Get("instanceId");
                if (generation > 0 && currentOwner == owner)
                    _ = PipeClientAdapter.Retire(generation + "\n" + owner);
            }
            catch { }
            Environment.SetEnvironmentVariable("LWBRIDGE_INSTANCE_ID", previousInstance);
            ResetDirect(string.Empty, 0, enabled: false, running: false);
            Set("directTransportRequestedInstance", string.Empty);
            Set("runtimeDirectory", string.Empty);
            Set("instanceId", string.Empty);
            Set("transportState", "idle");
            Set("failedGeneration", 0);
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    private static string WaitForDirectInbound(string owner)
    {
        var timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(4))
        {
            string value = PipeClientAdapter.TryDequeueInbound(owner);
            if (value.StartsWith("frame\n", StringComparison.Ordinal)) return value;
            Require(value.StartsWith("empty\n", StringComparison.Ordinal),
                "direct integration dequeue returned " + value);
            Thread.Sleep(5);
        }
        throw new TimeoutException("direct integration inbound frame was not queued");
    }

    private static string WaitForDirectState(string owner, string state)
    {
        var timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(4))
        {
            string value = PipeClientAdapter.ReadTransportState(owner);
            if (value.EndsWith("\n" + state, StringComparison.Ordinal)) return value;
            Thread.Sleep(5);
        }
        throw new TimeoutException("direct integration state did not reach " + state);
    }

    private static int ParseDirectGeneration(string value)
    {
        int first = value.IndexOf('\n');
        int second = first < 0 ? -1 : value.IndexOf('\n', first + 1);
        int generation = 0;
        Require(first > 0 && second > first + 1 &&
                int.TryParse(value.Substring(first + 1, second - first - 1), out generation) &&
                generation > 0,
            "direct integration generation was invalid");
        return generation;
    }

    private static bool WaitForFile(string path)
    {
        var timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(4))
        {
            if (File.Exists(path)) return true;
            Thread.Sleep(5);
        }
        return false;
    }

    private static string ReadPipeFrame(Stream stream)
    {
        byte[] prefix = ReadPipeExact(stream, 4);
        int length = prefix[0] | (prefix[1] << 8) | (prefix[2] << 16) | (prefix[3] << 24);
        Require(length > 0 && length <= 0x800000,
            "inert pipe frame length was invalid: " + length);
        return Encoding.UTF8.GetString(ReadPipeExact(stream, length));
    }

    private static byte[] ReadPipeExact(Stream stream, int count)
    {
        byte[] buffer = new byte[count];
        int offset = 0;
        while (offset < count)
        {
            Task<int> read = stream.ReadAsync(buffer, offset, count - offset);
            Require(read.Wait(TimeSpan.FromSeconds(4)),
                "inert pipe read timed out");
            int received = read.GetAwaiter().GetResult();
            Require(received > 0, "inert pipe closed before a complete frame");
            offset += received;
        }
        return buffer;
    }

    private static void WritePipeFrame(Stream stream, string json)
    {
        byte[] payload = Encoding.UTF8.GetBytes(json);
        Require(payload.Length > 0 && payload.Length <= 0x800000,
            "inert pipe write frame length was invalid");
        byte[] frame = new byte[4 + payload.Length];
        frame[0] = (byte)payload.Length;
        frame[1] = (byte)(payload.Length >> 8);
        frame[2] = (byte)(payload.Length >> 16);
        frame[3] = (byte)(payload.Length >> 24);
        Buffer.BlockCopy(payload, 0, frame, 4, payload.Length);
        // The production host publishes one complete framed buffer. Match that
        // contract: the adapter intentionally waits for the complete frame in
        // PeekNamedPipe before it performs a blocking read.
        stream.Write(frame, 0, frame.Length);
        stream.Flush();
    }

    private static void WriteLease(string path, string instance, long updatedAt) =>
        File.WriteAllText(path,
            "sessionId=" + instance + "\nchallenge=test-only\nupdatedAt=" + updatedAt + "\n",
            new UTF8Encoding(false));

    private static bool LeaseIsFresh() =>
        (bool)(typeof(PipeClientAdapter).GetMethod("LeaseIsFresh", StaticPrivate)!
            .Invoke(null, null) ?? false);

    private static bool EnqueueInbound(int generation, string json) =>
        (bool)(typeof(PipeClientAdapter).GetMethod("EnqueueInboundFrameLocked", StaticPrivate)!
            .Invoke(null, new object[] { generation, json }) ?? false);

    private static void ResetDirect(
        string owner,
        int generation,
        bool enabled = true,
        bool running = true)
    {
        typeof(PipeClientAdapter).GetMethod("ClearDirectQueuesLocked", StaticPrivate)!
            .Invoke(null, null);
        Set("instanceId", owner);
        Set("connectionGeneration", generation);
        Set("directTransportEnabled", enabled);
        Set("running", running);
        Set("failedGeneration", 0);
        Set("transportState", running ? "connected" : "idle");
        Set("inboundQueuedBytes", 0L);
        Set("outboundQueuedBytes", 0L);
        ((AutoResetEvent)(typeof(PipeClientAdapter).GetField("outboundSignal", StaticPrivate)!
            .GetValue(null) ?? throw new InvalidOperationException("outbound signal missing"))).Reset();
    }

    private static int QueueCount(string field)
    {
        object queue = typeof(PipeClientAdapter).GetField(field, StaticPrivate)!.GetValue(null)
            ?? throw new InvalidOperationException(field + " missing");
        return (int)(queue.GetType().GetProperty("Count")!.GetValue(queue)
            ?? throw new InvalidOperationException(field + " count missing"));
    }

    private static object Get(string field) =>
        typeof(PipeClientAdapter).GetField(field, StaticPrivate)!.GetValue(null)
        ?? throw new InvalidOperationException(field + " missing");

    private static void ResetThreadCache()
    {
        Set("lastValidatedLeaseUpdatedAt", null);
        Set("lastValidatedLeaseInstance", null);
        Set("lastLeaseValidationTimestamp", 0L);
        Set("lastValidatedLeaseLength", 0L);
        Set("lastValidatedLeaseWriteTicks", 0L);
    }

    private static void Set(string field, object? value) =>
        typeof(PipeClientAdapter).GetField(field, StaticPrivate)!.SetValue(null, value);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
