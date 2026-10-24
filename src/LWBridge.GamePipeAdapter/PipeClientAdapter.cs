using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace LWBridge.GamePipe
{
    public static class PipeClientAdapter
    {
        public static readonly Action<string, string, string> Connect = BeginConnect;
        // Read-only, nonblocking Lua publication seam. Classify the actual .NET
        // exception before xLua converts exceptions to unstructured error text.
        public static readonly Func<string, string> ReadRuntimeSnapshot = ReadRuntimeSnapshotCore;
        // xLua already proves direct System Func<string,string> invocation for
        // ReadRuntimeSnapshot. Keep every direct transport seam on that same
        // shape so the managed worker never invokes Lua off-thread.
        public static readonly Func<string, string> EnableDirectTransport = EnableDirectTransportCore;
        public static readonly Func<string, string> ReadTransportState = ReadTransportStateCore;
        public static readonly Func<string, string> TryDequeueInbound = TryDequeueInboundCore;
        public static readonly Func<string, string> PublishOutbound = PublishOutboundCore;
        public static readonly Func<string, string> Retire = RetireCore;

        private const int MaxFrameBytes = 0x800000;
        private const int FramePrefixBytes = 4;
        private const int DirectQueueItemCapacity = 0x1000;
        private const long DirectInboundByteBudget = 0x04000000;
        private const long DirectOutboundByteBudget = 0x02000000;
        private const int LeaseValidationCacheMilliseconds = 250;
        private const uint GenericRead = 0x80000000;
        private const uint GenericWrite = 0x40000000;
        private const uint OpenExisting = 3;
        private static readonly object Gate = new object();
        private static readonly object TraceGate = new object();
        private static Stream? stream;
        private static Thread? workerThread;
        private static volatile bool running;
        // Every Connect supersedes the prior worker. A retired worker must not
        // close/relabel the replacement stream or make its worker stop.
        private static volatile int connectionGeneration;
        private static volatile string transportState = "idle";
        private static string directTransportRequestedInstance = string.Empty;
        private static bool directTransportEnabled;
        private static int failedGeneration;
        private static readonly Queue<TransportFrame> inboundFrames = new Queue<TransportFrame>();
        private static readonly Queue<TransportFrame> outboundFrames = new Queue<TransportFrame>();
        private static long inboundQueuedBytes;
        private static long outboundQueuedBytes;
        private static readonly AutoResetEvent outboundSignal = new AutoResetEvent(false);
        private static string runtimeDirectory = string.Empty;
        private static string instanceId = string.Empty;
        private static int inboundSequence;
        private static int outboundSequence;
        [ThreadStatic] private static long? lastValidatedLeaseUpdatedAt;
        [ThreadStatic] private static string? lastValidatedLeaseInstance;
        [ThreadStatic] private static long lastLeaseValidationTimestamp;
        [ThreadStatic] private static long lastValidatedLeaseLength;
        [ThreadStatic] private static long lastValidatedLeaseWriteTicks;

        private sealed class TransportFrame
        {
            internal TransportFrame(int generation, string json, int frameBytes)
            {
                Generation = generation;
                Json = json;
                FrameBytes = frameBytes;
            }

            internal int Generation { get; }
            internal string Json { get; }
            internal int FrameBytes { get; }
        }

        private static string EnableDirectTransportCore(string expectedInstance)
        {
            string environmentInstance =
                Environment.GetEnvironmentVariable("LWBRIDGE_INSTANCE_ID") ??
                string.Empty;
            if (string.IsNullOrWhiteSpace(expectedInstance) ||
                !string.Equals(expectedInstance, environmentInstance, StringComparison.Ordinal))
                return "stale";
            lock (Gate)
            {
                directTransportRequestedInstance = expectedInstance;
                return "ok";
            }
        }

        private static string ReadTransportStateCore(string expectedInstance)
        {
            lock (Gate)
            {
                if (!DirectOwnerMatchesLocked(expectedInstance)) return "unavailable";
                return "ok\n" + connectionGeneration.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                       "\n" + transportState;
            }
        }

        private static string TryDequeueInboundCore(string expectedInstance)
        {
            lock (Gate)
            {
                if (!DirectOwnerMatchesLocked(expectedInstance)) return "unavailable";
                int generation = connectionGeneration;
                if (inboundFrames.Count == 0)
                    return "empty\n" + generation.ToString(System.Globalization.CultureInfo.InvariantCulture);

                TransportFrame frame = inboundFrames.Dequeue();
                inboundQueuedBytes -= frame.FrameBytes;
                if (frame.Generation != generation)
                    return "stale";
                return "frame\n" + generation.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                       "\n" + frame.Json;
            }
        }

        private static string PublishOutboundCore(string value)
        {
            if (!TryParseGenerationPayload(value, requireJson: true,
                    out int generation, out string expectedInstance, out string json))
                return "invalid";

            int payloadBytes;
            try { payloadBytes = Encoding.UTF8.GetByteCount(json); }
            catch { return "invalid"; }
            if (payloadBytes <= 0 || payloadBytes > MaxFrameBytes)
            {
                lock (Gate)
                {
                    if (!DirectOwnerMatchesLocked(expectedInstance) ||
                        generation != connectionGeneration || !running)
                        return "stale";
                    FailCurrentGenerationLocked(generation, "error:frame_length_invalid");
                    return "frame_length_invalid";
                }
            }
            int frameBytes = checked(FramePrefixBytes + payloadBytes);

            lock (Gate)
            {
                if (!DirectOwnerMatchesLocked(expectedInstance) ||
                    generation != connectionGeneration || !running)
                    return "stale";
                if (outboundFrames.Count >= DirectQueueItemCapacity ||
                    outboundQueuedBytes > DirectOutboundByteBudget - frameBytes)
                {
                    FailCurrentGenerationLocked(generation, "error:direct_outbound_overflow");
                    return "overflow";
                }

                outboundFrames.Enqueue(new TransportFrame(generation, json, frameBytes));
                outboundQueuedBytes += frameBytes;
                outboundSignal.Set();
                return "ok";
            }
        }

        private static string RetireCore(string value)
        {
            if (!TryParseGenerationPayload(value, requireJson: false,
                    out int generation, out string expectedInstance, out _))
                return "invalid";
            lock (Gate)
            {
                if (!string.Equals(instanceId, expectedInstance, StringComparison.Ordinal) ||
                    generation != connectionGeneration)
                    return "stale";

                running = false;
                directTransportEnabled = false;
                ClearDirectQueuesLocked();
                RetireStreamLocked();
                WriteState("closed");
                outboundSignal.Set();
                return "ok";
            }
        }

        private static bool TryParseGenerationPayload(
            string value,
            bool requireJson,
            out int generation,
            out string expectedInstance,
            out string json)
        {
            generation = 0;
            expectedInstance = string.Empty;
            json = string.Empty;
            if (string.IsNullOrEmpty(value)) return false;
            int first = value.IndexOf('\n');
            if (first <= 0) return false;
            if (!int.TryParse(value.Substring(0, first),
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out generation) || generation <= 0)
                return false;
            if (requireJson)
            {
                int second = value.IndexOf('\n', first + 1);
                if (second <= first + 1 || second == value.Length - 1) return false;
                expectedInstance = value.Substring(first + 1, second - first - 1);
                json = value.Substring(second + 1);
            }
            else
            {
                if (first == value.Length - 1 || value.IndexOf('\n', first + 1) >= 0) return false;
                expectedInstance = value.Substring(first + 1);
            }
            return !string.IsNullOrWhiteSpace(expectedInstance);
        }

        private static bool DirectOwnerMatchesLocked(string expectedInstance) =>
            directTransportEnabled &&
            !string.IsNullOrWhiteSpace(expectedInstance) &&
            string.Equals(instanceId, expectedInstance, StringComparison.Ordinal);

        private static void ClearDirectQueuesLocked()
        {
            inboundFrames.Clear();
            outboundFrames.Clear();
            inboundQueuedBytes = 0;
            outboundQueuedBytes = 0;
        }

        private static void FailCurrentGenerationLocked(int generation, string state)
        {
            if (generation != connectionGeneration) return;
            failedGeneration = generation;
            running = false;
            ClearDirectQueuesLocked();
            WriteState(state);
            RetireStreamLocked();
            outboundSignal.Set();
        }

        private static bool EnqueueInboundFrameLocked(int generation, string json)
        {
            int payloadBytes = Encoding.UTF8.GetByteCount(json);
            if (payloadBytes <= 0 || payloadBytes > MaxFrameBytes)
            {
                FailCurrentGenerationLocked(generation, "error:frame_length_invalid");
                return false;
            }
            int frameBytes = checked(FramePrefixBytes + payloadBytes);
            if (inboundFrames.Count >= DirectQueueItemCapacity ||
                inboundQueuedBytes > DirectInboundByteBudget - frameBytes)
            {
                FailCurrentGenerationLocked(generation, "error:direct_inbound_overflow");
                return false;
            }
            inboundFrames.Enqueue(new TransportFrame(generation, json, frameBytes));
            inboundQueuedBytes += frameBytes;
            return true;
        }

        private static string? DequeueOutboundFrameLocked(int generation)
        {
            if (outboundFrames.Count == 0) return null;
            TransportFrame frame = outboundFrames.Dequeue();
            outboundQueuedBytes -= frame.FrameBytes;
            return frame.Generation == generation ? frame.Json : null;
        }

        private static string ReadRuntimeSnapshotCore(string path)
        {
            try
            {
                string name = Path.GetFileName(path);
                if (name != "lease.txt" && name != "control.txt") return "unavailable\n";
                string text = File.ReadAllText(path, new UTF8Encoding(false, true));
                return text.Length <= 4096 ? "ok\n" + text : "unavailable\n";
            }
            catch (IOException error) when (
                (error.HResult & 0xffff) == 32 || (error.HResult & 0xffff) == 33)
            {
                return "busy\n";
            }
            catch { return "unavailable\n"; }
        }

        // Test-owned, opt-in receipts. Never include token, hello payload or secrets.
        // The real game-side adapter writes these under the explicitly isolated
        // LWBRIDGE_REBUILD_DATA_ROOT. Fail closed on unavailable diagnostics.
        private static void TraceStage(string stage, string? detail = null)
        {
            if (Environment.GetEnvironmentVariable("LWBRIDGE_PIPE_DIAGNOSTIC") != "1")
                return;
            try
            {
                string? configured = Environment.GetEnvironmentVariable("LWBRIDGE_REBUILD_DATA_ROOT");
                string? session = Environment.GetEnvironmentVariable("LWBRIDGE_INSTANCE_ID");
                if (string.IsNullOrWhiteSpace(configured) || string.IsNullOrWhiteSpace(session) ||
                    !Path.GetFullPath(configured).StartsWith(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                        Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    return;
                string directory = Path.Combine(configured, "overview-bridge");
                Directory.CreateDirectory(directory);
                lock (TraceGate)
                {
                    File.AppendAllText(Path.Combine(directory, "pipe-native-receipts.txt"),
                        DateTime.UtcNow.ToString("O") + "|pid=" + Process.GetCurrentProcess().Id +
                        "|session=" + session + "|stage=" + stage +
                        (string.IsNullOrEmpty(detail) ? "" : "|detail=" + detail) +
                        Environment.NewLine, new UTF8Encoding(false));
                }
            }
            catch { /* diagnostics must not perturb production connectivity */ }
        }
        private static void BeginConnect(
            string pipePath,
            string helloJson,
            string runtimeDir)
        {
            TraceStage("begin_connect_entry", "runtimeDirMatches=" + string.Equals(runtimeDir, Path.Combine(Environment.GetEnvironmentVariable("LWBRIDGE_REBUILD_DATA_ROOT") ?? "", "overview-bridge"), StringComparison.OrdinalIgnoreCase));
            lock (Gate)
            {
                CloseLocked();
                int workerGeneration = ++connectionGeneration;
                failedGeneration = 0;
                ClearDirectQueuesLocked();
                runtimeDirectory = runtimeDir ?? string.Empty;
                if (string.IsNullOrWhiteSpace(pipePath) ||
                    string.IsNullOrEmpty(helloJson) ||
                    string.IsNullOrWhiteSpace(runtimeDirectory))
                {
                    WriteState("error:invalid_connect_arguments");
                    return;
                }

                Directory.CreateDirectory(runtimeDirectory);
                instanceId =
                    Environment.GetEnvironmentVariable("LWBRIDGE_INSTANCE_ID") ??
                    string.Empty;
                if (string.IsNullOrWhiteSpace(instanceId))
                {
                    WriteState("error:instance_id_missing");
                    return;
                }
                directTransportEnabled = string.Equals(
                    directTransportRequestedInstance,
                    instanceId,
                    StringComparison.Ordinal);
                directTransportRequestedInstance = string.Empty;
                CleanupMailboxes();
                inboundSequence = 0;
                outboundSequence = 0;
                running = true;
                WriteState("connecting");
                TraceStage("native_startup", "stateFileExists=" + File.Exists(Path.Combine(runtimeDirectory, "pipe-adapter-state.txt")));
                var worker = new Thread(
                    () => ConnectAndRun(pipePath, helloJson, workerGeneration))
                {
                    IsBackground = true,
                    Name = "LWBridgeGamePipeWorker",
                };
                workerThread = worker;
                TraceStage("worker_created");
                worker.Start();
                TraceStage("worker_start_called");
            }
        }

        private static bool CurrentWorker(int generation) =>
            running && connectionGeneration == generation;

        private static void WriteStateForWorker(int generation, string value)
        {
            lock (Gate)
            {
                if (CurrentWorker(generation)) WriteState(value);
            }
        }

        private static void ConnectAndRun(
            string pipePath,
            string helloJson,
            int generation)
        {
            SafeFileHandle? handle = null;
            FileStream? opened = null;
            FileStream? ownedStream = null;
            bool hadError = false;
            TraceStage("worker_entry");
            try
            {
                if (!CurrentWorker(generation)) return;
                if (!WaitNamedPipeW(pipePath, 3000))
                    throw NativeError("WaitNamedPipeW");
                TraceStage("wait_named_pipe_success");
                handle = CreateFileW(
                    pipePath,
                    GenericRead | GenericWrite,
                    0,
                    IntPtr.Zero,
                    OpenExisting,
                    0,
                    IntPtr.Zero);
                if (handle.IsInvalid)
                    throw NativeError("CreateFileW");

                TraceStage("native_pipe_opened");
                opened = new FileStream(
                    handle,
                    FileAccess.ReadWrite,
                    // A pipe frame is already explicitly flushed. Buffered
                    // Dispose can flush concurrently with the worker and block
                    // the game thread during Retire/Connect.
                    1,
                    false);
                ownedStream = opened;
                handle = null;
                if (!CurrentWorker(generation)) return;
                WriteFrame(opened, helloJson);
                TraceStage("hello_frame_written");
                FileStream own;
                lock (Gate)
                {
                    if (!CurrentWorker(generation))
                        return;
                    stream = opened;
                    own = opened;
                    opened = null;
                    WriteState("connected");
                }
                TraceStage("worker_connected");

                while (CurrentWorker(generation))
                {
                    if (!LeaseIsFresh())
                    {
                        hadError = true;
                        WriteStateForWorker(generation, "error:host_lease_stale");
                        return;
                    }

                    // Audit 2026-10-23 W01: the lease read above can wait on file contention while a
                    // successor connects. Mailbox files, sequence counters and the runtime directory are
                    // shared, so they are only touched under the gate after re-checking that this worker
                    // and its own stream are still current. Pipe I/O uses this worker's own stream only.
                    string? json = null;
                    lock (Gate)
                    {
                        if (!CurrentWorker(generation) || !ReferenceEquals(stream, own))
                            return;
                        if (directTransportEnabled)
                        {
                            json = DequeueOutboundFrameLocked(generation);
                        }
                        else
                        {
                            int next = outboundSequence + 1;
                            string outboundPath = OutboundPath(next);
                            if (File.Exists(outboundPath))
                            {
                                json = File.ReadAllText(outboundPath, Encoding.UTF8);
                                outboundSequence = next;
                                try { File.Delete(outboundPath); } catch { }
                            }
                        }
                    }
                    if (json != null)
                    {
                        WriteFrame(own, json);
                        continue;
                    }

                    if (TryGetAvailableFrameBytes(
                            own.SafeFileHandle,
                            out int frameBytes) &&
                        frameBytes > 0)
                    {
                        string frame = ReadFrame(own);
                        lock (Gate)
                        {
                            if (!CurrentWorker(generation) || !ReferenceEquals(stream, own))
                                return;
                            if (directTransportEnabled)
                            {
                                if (!EnqueueInboundFrameLocked(generation, frame))
                                {
                                    hadError = true;
                                    return;
                                }
                            }
                            else
                            {
                                WriteInboundFrame(frame);
                            }
                        }
                        continue;
                    }

                    // Preserve the current 10 ms PeekNamedPipe inbound cadence,
                    // but let Lua publication wake outbound delivery immediately.
                    outboundSignal.WaitOne(10);
                }
            }
            catch (Exception error)
            {
                TraceStage("worker_exception", error.GetType().Name + ":" + (error is Win32Exception win ? win.NativeErrorCode.ToString() : "non-win32"));
                hadError = true;
                WriteStateForWorker(generation, "error:" + FormatError(error));
            }
            finally
            {
                lock (Gate)
                {
                    if (connectionGeneration == generation)
                    {
                        running = false;
                        stream = null;
                        if (!hadError && failedGeneration != generation) WriteState("closed");
                    }
                }
                // Only this worker disposes its unbuffered stream, after pipe
                // I/O has returned and after releasing the shared state gate.
                try { ownedStream?.Dispose(); } catch { }
                try { handle?.Dispose(); } catch { }
            }
        }

        private static bool LeaseIsFresh()
        {
            string path = Path.Combine(runtimeDirectory, "lease.txt");
            string expectedInstance = instanceId;
            if (!string.Equals(lastValidatedLeaseInstance, expectedInstance, StringComparison.Ordinal))
            {
                lastValidatedLeaseUpdatedAt = null;
                lastValidatedLeaseInstance = expectedInstance;
                lastLeaseValidationTimestamp = 0;
                lastValidatedLeaseLength = 0;
                lastValidatedLeaseWriteTicks = 0;
            }
            // The host renews once per second and the existing freshness contract
            // already permits a five-second lease horizon. Re-reading and parsing
            // the same file on every 10 ms idle adapter loop adds disk/decoder work
            // without adding identity information. Reuse a *successful exact-owner*
            // validation for at most 250 ms, but re-evaluate the published timestamp
            // against the original five-second rule on every cache hit. A due
            // revalidation still reads a complete snapshot and remains fail-closed
            // under sharing contention; the cache is never substituted for a failed
            // current read.
            long cachedNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (lastValidatedLeaseUpdatedAt.HasValue && lastLeaseValidationTimestamp != 0)
            {
                long elapsedTicks = Stopwatch.GetTimestamp() - lastLeaseValidationTimestamp;
                double elapsedMilliseconds = elapsedTicks * 1000.0 / Stopwatch.Frequency;
                long cachedUpdatedAt = lastValidatedLeaseUpdatedAt.Value;
                bool sameSnapshot = false;
                try
                {
                    var current = new FileInfo(path);
                    current.Refresh();
                    sameSnapshot = current.Exists &&
                                   current.Length == lastValidatedLeaseLength &&
                                   current.LastWriteTimeUtc.Ticks == lastValidatedLeaseWriteTicks;
                }
                catch { }
                if (sameSnapshot &&
                    elapsedMilliseconds >= 0 &&
                    elapsedMilliseconds < LeaseValidationCacheMilliseconds &&
                    cachedUpdatedAt <= cachedNow + 5 &&
                    cachedNow - cachedUpdatedAt <= 5)
                    return true;
                if (!sameSnapshot)
                {
                    lastValidatedLeaseUpdatedAt = null;
                    lastLeaseValidationTimestamp = 0;
                    lastValidatedLeaseLength = 0;
                    lastValidatedLeaseWriteTicks = 0;
                }
            }
            // Clone-internal adaptation for the host's exact-identity file guard.
            // Retry only Windows sharing/lock contention, at the existing 10ms
            // worker cadence, and never beyond the existing five-second lease
            // horizon (or five seconds for the first read). No cached value is
            // returned as fresh; a complete current snapshot must be validated.
            var retryWindow = Stopwatch.StartNew();
            while (true)
            {
                try
                {
                    var before = new FileInfo(path);
                    before.Refresh();
                    if (!before.Exists) return false;
                    long beforeLength = before.Length;
                    long beforeWriteTicks = before.LastWriteTimeUtc.Ticks;
                    string? session = null;
                    long? updatedAt = null;
                    var keys = new HashSet<string>(StringComparer.Ordinal);
                    foreach (string line in File.ReadAllLines(path, new UTF8Encoding(false, true)))
                    {
                        int separator = line.IndexOf('=');
                        if (separator <= 0)
                            return false;
                        string key = line.Substring(0, separator);
                        if (!keys.Add(key)) return false;
                        string value = line.Substring(separator + 1);
                        if (key == "sessionId")
                            session = value;
                        else if (key == "updatedAt" &&
                                 long.TryParse(value, out long parsed))
                            updatedAt = parsed;
                    }
                    if (!string.Equals(
                            session,
                            expectedInstance,
                            StringComparison.Ordinal) ||
                        updatedAt == null)
                    {
                        return false;
                    }
                    var after = new FileInfo(path);
                    after.Refresh();
                    if (!after.Exists ||
                        after.Length != beforeLength ||
                        after.LastWriteTimeUtc.Ticks != beforeWriteTicks)
                    {
                        if (retryWindow.ElapsedMilliseconds >= 5000) return false;
                        Thread.Sleep(10);
                        continue;
                    }
                    long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    bool fresh = updatedAt.Value <= now + 5 &&
                                 now - updatedAt.Value <= 5;
                    if (fresh)
                    {
                        lastValidatedLeaseUpdatedAt = updatedAt;
                        lastLeaseValidationTimestamp = Stopwatch.GetTimestamp();
                        lastValidatedLeaseLength = after.Length;
                        lastValidatedLeaseWriteTicks = after.LastWriteTimeUtc.Ticks;
                    }
                    else
                    {
                        lastValidatedLeaseUpdatedAt = null;
                        lastLeaseValidationTimestamp = 0;
                        lastValidatedLeaseLength = 0;
                        lastValidatedLeaseWriteTicks = 0;
                    }
                    return fresh;
                }
                catch (IOException error) when (
                    (error.HResult & 0xffff) == 32 || (error.HResult & 0xffff) == 33)
                {
                    long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    if (retryWindow.ElapsedMilliseconds >= 5000 ||
                        (lastValidatedLeaseUpdatedAt.HasValue &&
                         now - lastValidatedLeaseUpdatedAt.Value > 5))
                        return false;
                    Thread.Sleep(10);
                }
                catch
                {
                    return false;
                }
            }
        }

        private static void CloseLocked()
        {
            running = false;
            directTransportEnabled = false;
            ClearDirectQueuesLocked();
            RetireStreamLocked();
            outboundSignal.Set();
        }

        private static void RetireStreamLocked()
        {
            var retired = stream as FileStream;
            stream = null;
            if (retired == null) return;

            // Cancel only this detached worker's exact native handle. Never
            // flush/dispose a FileStream from the Lua caller or under Gate.
            // Retry until the worker's own finally closes the handle: a single
            // cancellation can race with an I/O call just about to be issued.
            ThreadPool.QueueUserWorkItem(state =>
            {
                try
                {
                    SafeFileHandle retiredHandle = retired.SafeFileHandle;
                    while (!retiredHandle.IsClosed)
                    {
                        if (CancelIoEx(retiredHandle, IntPtr.Zero))
                            TraceStage("retired_io_cancelled");
                        Thread.Sleep(10);
                    }
                }
                catch (ObjectDisposedException) { }
            });
        }

        private static string InboundPath(int sequence) =>
            Path.Combine(
                runtimeDirectory,
                "pipe-inbound-" + sequence.ToString("D8") + ".json");

        private static string OutboundPath(int sequence) =>
            Path.Combine(
                runtimeDirectory,
                "pipe-outbound-" + sequence.ToString("D8") + ".json");
        private static void WriteInboundFrame(string json)
        {
            int sequence = Interlocked.Increment(ref inboundSequence);
            string finalPath = InboundPath(sequence);
            string tempPath = finalPath + ".tmp";
            File.WriteAllText(tempPath, json, new UTF8Encoding(false));
            if (File.Exists(finalPath))
                File.Delete(finalPath);
            File.Move(tempPath, finalPath);
        }

        private static void CleanupMailboxes()
        {
            foreach (string pattern in new[]
                     {
                         "pipe-inbound-*.json",
                         "pipe-inbound-*.json.tmp",
                         "pipe-outbound-*.json",
                         "pipe-outbound-*.json.tmp",
                     })
            {
                foreach (string path in Directory.GetFiles(
                             runtimeDirectory,
                             pattern))
                {
                    try { File.Delete(path); } catch { }
                }
            }
        }

        private static void WriteState(string value)
        {
            transportState = value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(runtimeDirectory))
                return;
            try
            {
                File.WriteAllText(
                    Path.Combine(
                        runtimeDirectory,
                        "pipe-adapter-state.txt"),
                    value ?? string.Empty,
                    new UTF8Encoding(false));
            }
            catch { }
        }

        private static void WriteFrame(Stream target, string json)
        {
            byte[] payload = Encoding.UTF8.GetBytes(json);
            if (payload.Length <= 0 || payload.Length > MaxFrameBytes)
                throw new InvalidDataException("frame_length_invalid");
            byte[] prefix = new byte[4];
            prefix[0] = (byte)payload.Length;
            prefix[1] = (byte)(payload.Length >> 8);
            prefix[2] = (byte)(payload.Length >> 16);
            prefix[3] = (byte)(payload.Length >> 24);
            target.Write(prefix, 0, prefix.Length);
            target.Write(payload, 0, payload.Length);
            target.Flush();
        }

        private static bool TryGetAvailableFrameBytes(
            SafeFileHandle handle,
            out int frameBytes)
        {
            frameBytes = 0;
            byte[] prefix = new byte[4];
            if (!PeekNamedPipe(
                    handle,
                    prefix,
                    (uint)prefix.Length,
                    out uint bytesRead,
                    out uint totalBytesAvailable,
                    out _))
            {
                throw NativeError("PeekNamedPipe");
            }

            if (totalBytesAvailable < 4 || bytesRead < 4)
                return true;

            int payloadLength = prefix[0] |
                                (prefix[1] << 8) |
                                (prefix[2] << 16) |
                                (prefix[3] << 24);
            if (payloadLength <= 0 || payloadLength > MaxFrameBytes)
                throw new InvalidDataException("frame_length_invalid");

            long required = 4L + payloadLength;
            if (totalBytesAvailable >= required)
                frameBytes = checked((int)required);
            return true;
        }

        private static string ReadFrame(Stream source)
        {
            byte[] prefix = ReadExact(source, 4);
            int length = prefix[0] |
                         (prefix[1] << 8) |
                         (prefix[2] << 16) |
                         (prefix[3] << 24);
            if (length <= 0 || length > MaxFrameBytes)
                throw new InvalidDataException("frame_length_invalid");
            return Encoding.UTF8.GetString(ReadExact(source, length));
        }

        private static byte[] ReadExact(Stream source, int count)
        {
            byte[] buffer = new byte[count];
            int offset = 0;
            while (offset < count)
            {
                int read = source.Read(buffer, offset, count - offset);
                if (read <= 0)
                    throw new EndOfStreamException("pipe_closed");
                offset += read;
            }
            return buffer;
        }

        private static Win32Exception NativeError(string operation)
        {
            int code = Marshal.GetLastWin32Error();
            return new Win32Exception(
                code,
                operation + " failed (Win32 " + code + ")");
        }

        private static string FormatError(Exception error) =>
            error.GetType().Name + ":" + error.Message;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool WaitNamedPipeW(
            string lpNamedPipeName,
            uint nTimeOut);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CancelIoEx(SafeFileHandle handle, IntPtr overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PeekNamedPipe(
            SafeFileHandle hNamedPipe,
            byte[] lpBuffer,
            uint nBufferSize,
            out uint lpBytesRead,
            out uint lpTotalBytesAvail,
            out uint lpBytesLeftThisMessage);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFileW(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);
    }
}
