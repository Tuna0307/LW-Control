using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace LWBridge.Desktop;

// LWB-R7-115 + R7-123: authenticated hello transport. R7-115 proved the
// recovered native pipe, frame protocol, client-process identity gates and
// registry admission in isolation; R7-123 starts it through the shared host.
internal static class LWBridgeControlPipeIsolatedHandshake
{
    internal static async Task<LWBridgeAuthenticatedConnection> AuthenticateAsync(
        SafeFileHandle connectedPipe,
        LWBridgeControlPipeRegistry registry,
        string expectedBuildId,
        string expectedCanonicalClientPath,
        object route,
        long nowMilliseconds,
        long ackTimestamp,
        TimeSpan? timeoutOverride = null,
        CancellationToken cancellationToken = default,
        Stream? streamOverride = null)
    {
        ArgumentNullException.ThrowIfNull(connectedPipe);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedBuildId);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedCanonicalClientPath);
        ArgumentNullException.ThrowIfNull(route);

        TimeSpan timeout = timeoutOverride ??
            LWBridgeControlPipeListenerContract.HandshakeTimeout;
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeoutOverride));

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        timeoutSource.CancelAfter(timeout);

        // The server handle was created FILE_FLAG_OVERLAPPED in R7-111.
        // A session may supply its one long-lived async stream so the same OS
        // handle is bound to the .NET thread pool exactly once across hello
        // and RPC transport. Standalone handshake proofs still use a temporary
        // non-owning stream wrapper.
        FileStream? ownedStream = null;
        Stream stream;
        if (streamOverride is null)
        {
            var streamHandle = new SafeFileHandle(
                connectedPipe.DangerousGetHandle(),
                ownsHandle: false);
            ownedStream = new FileStream(
                streamHandle,
                FileAccess.ReadWrite,
                bufferSize: 4096,
                isAsync: true);
            stream = ownedStream;
        }
        else
        {
            stream = streamOverride;
        }

        try
        {
        LWBridgeProxyHello hello;
        try
        {
            byte[] helloPayload = await ReadFrameAsync(
                stream,
                timeoutSource.Token).ConfigureAwait(false);
            hello = LWBridgeControlPipeProtocol.ParseProxyHello(helloPayload);
        }
        catch (OperationCanceledException) when (
            !cancellationToken.IsCancellationRequested &&
            timeoutSource.IsCancellationRequested)
        {
            throw new BridgeCommandException(
                "PIPE_HANDSHAKE_TIMEOUT",
                "named pipe hello did not complete before the recovered handshake deadline");
        }

        if (!hello.Pid.TryGetUInt32(out uint claimedPid) || claimedPid == 0)
            throw Reject("hello pid is outside the recovered UInt32 range");

        uint actualPid = GetPipeClientProcessId(connectedPipe);
        if (!LWBridgeControlPipeHandshakeContract.ClientPidMatches(
                actualPid,
                claimedPid))
        {
            throw Reject("hello pid does not match the named-pipe client process");
        }

        if (!LWBridgeControlPipeHandshakeContract.BuildIdMatches(
                expectedBuildId,
                hello.BuildId))
        {
            throw Reject("hello buildId does not match the expected bridge build");
        }

        string actualCanonicalClientPath = GetCanonicalClientImagePath(actualPid);
        byte[] actualPathBytes = Encoding.UTF8.GetBytes(actualCanonicalClientPath);
        byte[] expectedPathBytes = Encoding.UTF8.GetBytes(expectedCanonicalClientPath);
        if (!LWBridgeControlPipeClientPathContract.AsciiCaseInsensitiveUtf8PathEquals(
                actualPathBytes,
                expectedPathBytes))
        {
            throw Reject("named-pipe client executable path does not match the expected bridge image");
        }

        var connection = new LWBridgeAuthenticatedConnection(
            hello.ProfileId,
            hello.InstanceId,
            actualPid,
            actualCanonicalClientPath,
            Generation: 0);

        if (!registry.TryAdmit(
                hello.ProfileId,
                hello.InstanceId,
                hello.Token,
                nowMilliseconds,
                route,
                out ulong generation))
        {
            throw Reject("hello registration/token admission failed");
        }

        connection = connection with { Generation = generation };

        try
        {
            byte[] ackPayload = LWBridgeControlPipeProtocol.EncodeHelloAck(
                hello,
                ackTimestamp);
            byte[] ackFrame = LWBridgeControlPipeProtocol.EncodeFrame(ackPayload);
            await stream.WriteAsync(ackFrame, timeoutSource.Token)
                .ConfigureAwait(false);
            await stream.FlushAsync(timeoutSource.Token).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            _ = registry.RemoveConnected(hello.InstanceId, generation);
            throw;
        }
        }
        finally
        {
            ownedStream?.Dispose();
        }
    }

    internal static string CanonicalizeExpectedClientPath(string path) =>
        CanonicalizePath(path);

    private static BridgeCommandException Reject(string message) =>
        new("PIPE_HANDSHAKE_REJECTED", message);

    private static async Task<byte[]> ReadFrameAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        byte[] prefix = new byte[LWBridgeControlPipeProtocol.FramePrefixLength];
        await ReadExactlyAsync(stream, prefix, cancellationToken).ConfigureAwait(false);
        uint payloadLength = System.Buffers.Binary.BinaryPrimitives
            .ReadUInt32LittleEndian(prefix);
        if (payloadLength is 0 or > LWBridgeControlPipeProtocol.MaxFramePayloadLength)
            throw new InvalidDataException(
                "LWBridge control-pipe frame length is outside the recovered range.");

        byte[] payload = new byte[checked((int)payloadLength)];
        await ReadExactlyAsync(stream, payload, cancellationToken).ConfigureAwait(false);
        return payload;
    }

    private static async Task ReadExactlyAsync(
        Stream stream,
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int read = await stream.ReadAsync(
                buffer[offset..],
                cancellationToken).ConfigureAwait(false);
            if (read == 0)
                throw new EndOfStreamException(
                    "LWBridge control-pipe closed during framed handshake.");
            offset += read;
        }
    }

    private static uint GetPipeClientProcessId(SafeFileHandle pipe)
    {
        if (!GetNamedPipeClientProcessId(pipe, out uint pid))
            throw NativeFailure("GetNamedPipeClientProcessId");
        return pid;
    }

    private static string GetCanonicalClientImagePath(uint pid)
    {
        using SafeFileHandle process = OpenProcess(
            LWBridgeControlPipeClientPathNative.ProcessQueryLimitedInformation,
            false,
            pid);
        if (process.IsInvalid)
            throw NativeFailure("OpenProcess");

        uint capacity = 0x8000;
        var buffer = new StringBuilder(checked((int)capacity));
        if (!QueryFullProcessImageNameW(process, 0, buffer, ref capacity))
            throw NativeFailure("QueryFullProcessImageNameW");

        return CanonicalizePath(buffer.ToString(0, checked((int)capacity)));
    }

    private static string CanonicalizePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (path.IndexOf('\0') >= 0)
            throw new ArgumentException("Client path contains an embedded NUL.", nameof(path));

        string absolute = GetRecoveredFullPath(path);
        using SafeFileHandle file = CreateFileW(
            absolute,
            LWBridgeControlPipeClientPathContract.DesiredAccess,
            LWBridgeControlPipeClientPathContract.ShareMode,
            IntPtr.Zero,
            LWBridgeControlPipeClientPathContract.OpenExisting,
            LWBridgeControlPipeClientPathContract.FileFlagBackupSemantics,
            IntPtr.Zero);
        if (file.IsInvalid)
            throw NativeFailure("CreateFileW");

        return GetFinalPath(file);
    }

    private static string GetRecoveredFullPath(string path)
    {
        if (path.StartsWith(@"\\?\", StringComparison.Ordinal) ||
            path.StartsWith(@"\??\", StringComparison.Ordinal))
        {
            return path;
        }

        uint required = GetFullPathNameW(path, 0, null, IntPtr.Zero);
        if (required == 0)
            throw NativeFailure("GetFullPathNameW");

        var buffer = new StringBuilder(checked((int)required));
        uint written = GetFullPathNameW(
            path,
            checked((uint)buffer.Capacity),
            buffer,
            IntPtr.Zero);
        if (written == 0 || written >= buffer.Capacity)
            throw NativeFailure("GetFullPathNameW");

        string full = buffer.ToString();
        if (full.StartsWith(@"\\", StringComparison.Ordinal))
            return LWBridgeControlPipeClientPathContract.VerbatimUncPrefix +
                   full[2..];
        if (full.Length >= 3 &&
            full[1] == ':' &&
            full[2] == '\\')
        {
            return LWBridgeControlPipeClientPathContract.VerbatimDosPrefix + full;
        }

        return full;
    }

    private static string GetFinalPath(SafeFileHandle file)
    {
        uint required = GetFinalPathNameByHandleW(
            file,
            null,
            0,
            LWBridgeControlPipeClientPathContract.GetFinalPathNameFlags);
        if (required == 0)
            throw NativeFailure("GetFinalPathNameByHandleW");

        var buffer = new StringBuilder(checked((int)required + 1));
        uint written = GetFinalPathNameByHandleW(
            file,
            buffer,
            checked((uint)buffer.Capacity),
            LWBridgeControlPipeClientPathContract.GetFinalPathNameFlags);
        if (written == 0 || written >= buffer.Capacity)
            throw NativeFailure("GetFinalPathNameByHandleW");
        return buffer.ToString(0, checked((int)written));
    }

    private static Win32Exception NativeFailure(string operation)
    {
        int error = Marshal.GetLastWin32Error();
        return new Win32Exception(error, $"{operation} failed (Win32 {error}).");
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientProcessId(
        SafeFileHandle Pipe,
        out uint ClientProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeFileHandle OpenProcess(
        uint dwDesiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle,
        uint dwProcessId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageNameW(
        SafeFileHandle hProcess,
        uint dwFlags,
        StringBuilder lpExeName,
        ref uint lpdwSize);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFullPathNameW(
        string lpFileName,
        uint nBufferLength,
        StringBuilder? lpBuffer,
        IntPtr lpFilePart);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandleW(
        SafeFileHandle hFile,
        StringBuilder? lpszFilePath,
        uint cchFilePath,
        uint dwFlags);
}

internal static class LWBridgeControlPipeClientPathNative
{
    public const uint ProcessQueryLimitedInformation = 0x1000;
}

internal sealed record LWBridgeAuthenticatedConnection(
    string ProfileId,
    string InstanceId,
    uint ClientPid,
    string CanonicalClientPath,
    ulong Generation);
