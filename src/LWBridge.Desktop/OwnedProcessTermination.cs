using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace LWBridge.Desktop;

// HOME 009 R1 B. TECHNICAL ADAPTATION (not an original-contract fact): a process is verified AND terminated
// through the same OS handle. The previous native recovery validated a System.Diagnostics.Process and then
// reopened the PID with OpenProcess(PROCESS_TERMINATE), which let a PID replaced between the two steps be
// terminated. Image path and creation incarnation are now read from the handle that is terminated and awaited.
internal interface IOwnedProcessApi
{
    IntPtr Open(int pid, uint access);                // IntPtr.Zero on failure
    string? ImagePath(IntPtr handle);                 // null when unreadable (including after exit)
    string? CreationUtc(IntPtr handle);               // ISO-8601 "O" UTC, null when unreadable
    bool Terminate(IntPtr handle, uint exitCode);
    bool WaitExited(IntPtr handle, int milliseconds); // true when the process behind THIS handle has exited
    void Close(IntPtr handle);
}

internal enum OwnedProcessTerminationResult
{
    /// <summary>Handle-verified identity matched, TerminateProcess succeeded and the exit was observed on the handle.</summary>
    Terminated,
    /// <summary>No process could be verified (absent, unreadable, different image or different incarnation); nothing was terminated.</summary>
    NotVerified,
    /// <summary>The process was verified as a query-only handle, but the terminate right could not be obtained.</summary>
    OpenDenied,
    /// <summary>The identity was verified on the handle but TerminateProcess failed while the process is still alive.</summary>
    TerminateFailed,
}

internal static class OwnedProcessTermination
{
    internal const uint ProcessTerminate = 0x0001;
    internal const uint ProcessQueryLimitedInformation = 0x1000;
    internal const uint Synchronize = 0x00100000;

    // expectedStartedAtUtc == null: path-only identity (updater helper processes carry no recorded incarnation).
    internal static async Task<OwnedProcessTerminationResult> TerminateAsync(
        IOwnedProcessApi api,
        int pid,
        string expectedPath,
        string? expectedStartedAtUtc,
        Func<TimeSpan, CancellationToken, Task> delayAsync,
        CancellationToken cancellationToken)
    {
        IntPtr handle = api.Open(pid, ProcessQueryLimitedInformation | ProcessTerminate | Synchronize);
        if (handle == IntPtr.Zero)
        {
            // Classify without any terminate right so an access problem is not reported as "not verified".
            IntPtr query = api.Open(pid, ProcessQueryLimitedInformation);
            if (query == IntPtr.Zero) return OwnedProcessTerminationResult.NotVerified;
            try
            {
                string? image = api.ImagePath(query);
                return image is not null && PathEquals(image, expectedPath)
                    ? OwnedProcessTerminationResult.OpenDenied
                    : OwnedProcessTerminationResult.NotVerified;
            }
            finally { api.Close(query); }
        }
        try
        {
            string? actualPath = api.ImagePath(handle);
            if (actualPath is null || !PathEquals(actualPath, expectedPath)) return OwnedProcessTerminationResult.NotVerified;
            if (expectedStartedAtUtc is not null &&
                !string.Equals(api.CreationUtc(handle), expectedStartedAtUtc, StringComparison.Ordinal))
                return OwnedProcessTerminationResult.NotVerified;
            if (!api.Terminate(handle, 1))
                return api.WaitExited(handle, 0) ? OwnedProcessTerminationResult.NotVerified
                                                 : OwnedProcessTerminationResult.TerminateFailed;
            while (!api.WaitExited(handle, 0))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await delayAsync(TimeSpan.FromMilliseconds(25), cancellationToken).ConfigureAwait(false);
            }
            return OwnedProcessTerminationResult.Terminated;
        }
        finally { api.Close(handle); }
    }

    private static bool PathEquals(string left, string right)
    {
        try
        {
            return string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)), StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException) { return false; }
        catch (NotSupportedException) { return false; }
    }
}

internal sealed class Win32OwnedProcessApi : IOwnedProcessApi
{
    public static readonly Win32OwnedProcessApi Instance = new();

    public IntPtr Open(int pid, uint access) => OpenProcess(access, false, pid);

    public string? ImagePath(IntPtr handle)
    {
        var buffer = new StringBuilder(0x8000);
        uint size = (uint)buffer.Capacity;
        return QueryFullProcessImageNameW(handle, 0, buffer, ref size) ? buffer.ToString(0, (int)size) : null;
    }

    public string? CreationUtc(IntPtr handle)
    {
        if (!GetProcessTimes(handle, out long created, out _, out _, out _)) return null;
        return DateTime.FromFileTimeUtc(created).ToString("O", CultureInfo.InvariantCulture);
    }

    public bool Terminate(IntPtr handle, uint exitCode) => TerminateProcess(handle, exitCode);

    public bool WaitExited(IntPtr handle, int milliseconds) => WaitForSingleObject(handle, (uint)milliseconds) == 0;

    public void Close(IntPtr handle) => _ = CloseHandle(handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, int processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageNameW(IntPtr process, uint flags, StringBuilder exeName, ref uint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessTimes(IntPtr process, out long creation, out long exit, out long kernel, out long user);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateProcess(IntPtr process, uint exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
