using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace LWBridge.Desktop;

// LWB-R7-111: exact Win32 server-instance construction recovered in R7-100.
// This primitive creates only a server pipe handle. It does not call
// ConnectNamedPipe, accept proxy traffic, register a profile, or route RPC.
internal static class LWBridgeControlPipeNativeServer
{
    private const uint SecurityDescriptorRevision = 1;

    internal static SafeFileHandle CreateServerInstance(
        string fullPipePath,
        string currentUserSid,
        bool firstServerInstance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPipePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentUserSid);

        string sddl = LWBridgeControlPipeListenerContract
            .GetSecurityDescriptorSddlForSid(currentUserSid);

        if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(
                sddl,
                SecurityDescriptorRevision,
                out IntPtr securityDescriptor,
                IntPtr.Zero))
        {
            throw CreateWin32Exception("ConvertStringSecurityDescriptorToSecurityDescriptorW");
        }

        try
        {
            var attributes = new SecurityAttributes
            {
                Length = LWBridgeControlPipeListenerContract.SecurityAttributesLength,
                SecurityDescriptor = securityDescriptor,
                InheritHandle = 0,
            };

            SafeFileHandle handle = CreateNamedPipeW(
                fullPipePath,
                LWBridgeControlPipeListenerContract.GetOpenMode(firstServerInstance),
                LWBridgeControlPipeListenerContract.PipeRejectRemoteClients,
                LWBridgeControlPipeListenerContract.MaxInstances,
                LWBridgeControlPipeListenerContract.OutboundBufferBytes,
                LWBridgeControlPipeListenerContract.InboundBufferBytes,
                LWBridgeControlPipeListenerContract.DefaultTimeoutMilliseconds,
                ref attributes);

            if (handle.IsInvalid)
            {
                int error = Marshal.GetLastWin32Error();
                handle.Dispose();
                throw new Win32Exception(
                    error,
                    $"CreateNamedPipeW failed for '{fullPipePath}' (Win32 {error}).");
            }

            return handle;
        }
        finally
        {
            _ = LocalFree(securityDescriptor);
        }
    }

    internal static LWBridgeControlPipePendingConnect BeginConnect(SafeFileHandle pipeHandle)
    {
        ArgumentNullException.ThrowIfNull(pipeHandle);
        if (pipeHandle.IsInvalid || pipeHandle.IsClosed)
            throw new ArgumentException("A live server pipe handle is required.", nameof(pipeHandle));

        return new LWBridgeControlPipePendingConnect(pipeHandle);
    }

    private static Win32Exception CreateWin32Exception(string operation)
    {
        int error = Marshal.GetLastWin32Error();
        return new Win32Exception(error, $"{operation} failed (Win32 {error}).");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes
    {
        internal uint Length;
        internal IntPtr SecurityDescriptor;
        internal int InheritHandle;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptorW(
        string stringSecurityDescriptor,
        uint stringSdRevision,
        out IntPtr securityDescriptor,
        IntPtr securityDescriptorSize);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateNamedPipeW(
        string lpName,
        uint dwOpenMode,
        uint dwPipeMode,
        uint nMaxInstances,
        uint nOutBufferSize,
        uint nInBufferSize,
        uint nDefaultTimeOut,
        ref SecurityAttributes lpSecurityAttributes);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr hMem);
}


internal sealed class LWBridgeControlPipePendingConnect : IDisposable
{
    private const uint WaitObject0 = 0x00000000;
    private const uint WaitTimeout = 0x00000102;
    private const uint WaitFailed = 0xFFFFFFFF;

    private readonly SafeFileHandle pipeHandle;
    private readonly SafeWaitHandle eventHandle;
    private readonly IntPtr overlappedPointer;
    private bool pipeDangerousRefAdded;
    private bool disposed;
    private bool completed;

    internal LWBridgeControlPipePendingConnect(SafeFileHandle pipeHandle)
    {
        this.pipeHandle = pipeHandle;
        bool added = false;
        pipeHandle.DangerousAddRef(ref added);
        pipeDangerousRefAdded = added;

        try
        {
            eventHandle = CreateEventW(IntPtr.Zero, true, false, null);
            if (eventHandle.IsInvalid)
                throw CreateWin32Exception("CreateEventW");

            var overlapped = new NativeOverlappedLayout
            {
                EventHandle = eventHandle.DangerousGetHandle(),
            };
            overlappedPointer = Marshal.AllocHGlobal(Marshal.SizeOf<NativeOverlappedLayout>());
            Marshal.StructureToPtr(overlapped, overlappedPointer, false);

            bool immediate = ConnectNamedPipe(pipeHandle, overlappedPointer);
            InitialLastError = immediate ? 0 : Marshal.GetLastWin32Error();
            InitialDisposition = LWBridgeControlPipeListenerContract.ClassifyConnectResult(
                immediate,
                InitialLastError);
            completed = InitialDisposition == LWBridgePipeConnectDisposition.Connected;
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    internal LWBridgePipeConnectDisposition InitialDisposition { get; }

    internal int InitialLastError { get; }

    internal LWBridgePipeConnectDisposition WaitForCompletion(TimeSpan timeout)
    {
        ThrowIfDisposed();
        if (completed)
            return LWBridgePipeConnectDisposition.Connected;
        if (InitialDisposition == LWBridgePipeConnectDisposition.Failed)
            return LWBridgePipeConnectDisposition.Failed;
        if (InitialDisposition != LWBridgePipeConnectDisposition.Pending)
            throw new InvalidOperationException("Unexpected native connect state.");

        long timeoutMilliseconds = checked((long)Math.Ceiling(timeout.TotalMilliseconds));
        if (timeoutMilliseconds <= 0 || timeoutMilliseconds > uint.MaxValue - 1)
            throw new ArgumentOutOfRangeException(nameof(timeout));

        uint wait = WaitForSingleObject(eventHandle, (uint)timeoutMilliseconds);
        if (wait == WaitTimeout)
            return LWBridgePipeConnectDisposition.Pending;
        if (wait == WaitFailed)
            throw CreateWin32Exception("WaitForSingleObject");
        if (wait != WaitObject0)
            throw new InvalidOperationException($"Unexpected wait result 0x{wait:X8}.");

        if (GetOverlappedResult(pipeHandle, overlappedPointer, out _, false))
        {
            completed = true;
            return LWBridgePipeConnectDisposition.Connected;
        }

        int error = Marshal.GetLastWin32Error();
        LWBridgePipeConnectDisposition result =
            LWBridgeControlPipeListenerContract.ClassifyConnectResult(false, error);
        completed = result == LWBridgePipeConnectDisposition.Connected;
        return result;
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;

        if (overlappedPointer != IntPtr.Zero)
        {
            if (!completed && InitialDisposition == LWBridgePipeConnectDisposition.Pending)
                _ = CancelIoEx(pipeHandle, overlappedPointer);
            Marshal.FreeHGlobal(overlappedPointer);
        }

        eventHandle?.Dispose();
        if (pipeDangerousRefAdded)
        {
            pipeHandle.DangerousRelease();
            pipeDangerousRefAdded = false;
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    private static Win32Exception CreateWin32Exception(string operation)
    {
        int error = Marshal.GetLastWin32Error();
        return new Win32Exception(error, $"{operation} failed (Win32 {error}).");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeOverlappedLayout
    {
        internal UIntPtr Internal;
        internal UIntPtr InternalHigh;
        internal uint Offset;
        internal uint OffsetHigh;
        internal IntPtr EventHandle;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ConnectNamedPipe(
        SafeFileHandle hNamedPipe,
        IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetOverlappedResult(
        SafeFileHandle hFile,
        IntPtr lpOverlapped,
        out uint lpNumberOfBytesTransferred,
        [MarshalAs(UnmanagedType.Bool)] bool bWait);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CancelIoEx(
        SafeFileHandle hFile,
        IntPtr lpOverlapped);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeWaitHandle CreateEventW(
        IntPtr lpEventAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool bManualReset,
        [MarshalAs(UnmanagedType.Bool)] bool bInitialState,
        string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(
        SafeWaitHandle hHandle,
        uint dwMilliseconds);
}
