using System;
using System.ComponentModel;
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

        private const int MaxFrameBytes = 0x800000;
        private const uint GenericRead = 0x80000000;
        private const uint GenericWrite = 0x40000000;
        private const uint OpenExisting = 3;
        private static readonly object Gate = new object();
        private static Stream? stream;
        private static volatile bool running;
        private static string runtimeDirectory = string.Empty;
        private static string instanceId = string.Empty;
        private static int inboundSequence;
        private static int outboundSequence;

        private static void BeginConnect(
            string pipePath,
            string helloJson,
            string runtimeDir)
        {
            lock (Gate)
            {
                CloseLocked();
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
                CleanupMailboxes();
                inboundSequence = 0;
                outboundSequence = 0;
                running = true;
                WriteState("connecting");
                var worker = new Thread(
                    () => ConnectAndRun(pipePath, helloJson))
                {
                    IsBackground = true,
                    Name = "LWBridgeGamePipeWorker",
                };
                worker.Start();
            }
        }

        private static void ConnectAndRun(
            string pipePath,
            string helloJson)
        {
            SafeFileHandle? handle = null;
            FileStream? opened = null;
            try
            {
                if (!WaitNamedPipeW(pipePath, 3000))
                    throw NativeError("WaitNamedPipeW");
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

                opened = new FileStream(
                    handle,
                    FileAccess.ReadWrite,
                    4096,
                    false);
                handle = null;
                WriteFrame(opened, helloJson);
                lock (Gate)
                {
                    if (!running)
                        return;
                    stream = opened;
                    opened = null;
                }
                WriteState("connected");

                while (running)
                {
                    if (!LeaseIsFresh())
                    {
                        WriteState("error:host_lease_stale");
                        return;
                    }

                    Stream? current;
                    lock (Gate) current = stream;
                    if (current is not FileStream fileStream)
                        return;

                    int next = outboundSequence + 1;
                    string outboundPath = OutboundPath(next);
                    if (File.Exists(outboundPath))
                    {
                        string json =
                            File.ReadAllText(outboundPath, Encoding.UTF8);
                        WriteFrame(current, json);
                        outboundSequence = next;
                        try { File.Delete(outboundPath); } catch { }
                        continue;
                    }

                    if (TryGetAvailableFrameBytes(
                            fileStream.SafeFileHandle,
                            out int frameBytes) &&
                        frameBytes > 0)
                    {
                        string frame = ReadFrame(current);
                        WriteInboundFrame(frame);
                        continue;
                    }

                    Thread.Sleep(10);
                }
            }
            catch (Exception error)
            {
                if (running)
                    WriteState("error:" + FormatError(error));
            }
            finally
            {
                running = false;
                lock (Gate)
                {
                    try { stream?.Dispose(); } catch { }
                    stream = null;
                }
                try { opened?.Dispose(); } catch { }
                try { handle?.Dispose(); } catch { }
            }
        }

        private static bool LeaseIsFresh()
        {
            string path = Path.Combine(runtimeDirectory, "lease.txt");
            try
            {
                string? session = null;
                long? updatedAt = null;
                foreach (string line in File.ReadAllLines(path))
                {
                    int separator = line.IndexOf('=');
                    if (separator <= 0)
                        continue;
                    string key = line.Substring(0, separator);
                    string value = line.Substring(separator + 1);
                    if (key == "sessionId")
                        session = value;
                    else if (key == "updatedAt" &&
                             long.TryParse(value, out long parsed))
                        updatedAt = parsed;
                }
                if (!string.Equals(
                        session,
                        instanceId,
                        StringComparison.Ordinal) ||
                    updatedAt == null)
                {
                    return false;
                }
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                return updatedAt.Value <= now + 5 &&
                       now - updatedAt.Value <= 5;
            }
            catch
            {
                return false;
            }
        }

        private static void CloseLocked()
        {
            running = false;
            try { stream?.Dispose(); } catch { }
            stream = null;
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
