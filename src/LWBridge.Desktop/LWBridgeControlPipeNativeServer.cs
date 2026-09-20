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
