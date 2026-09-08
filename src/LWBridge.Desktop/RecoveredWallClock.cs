using System.Runtime.InteropServices;

namespace LWBridge.Desktop;

internal static class RecoveredWallClock
{
    private const ulong FileTimeUnixEpoch100ns = 116_444_736_000_000_000UL;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeFileTime
    {
        public uint Low;
        public uint High;
    }

    [DllImport("kernel32.dll", EntryPoint = "GetSystemTimePreciseAsFileTime", ExactSpelling = true)]
    private static extern void GetSystemTimePreciseAsFileTime(out NativeFileTime fileTime);

    // LWB-R6-014 RECOVERED: LWBridge samples GetSystemTimePreciseAsFileTime,
    // subtracts the FILETIME Unix epoch and converts 100-ns ticks to Unix ms.
    public static long UnixTimeMilliseconds()
    {
        GetSystemTimePreciseAsFileTime(out NativeFileTime value);
        ulong fileTime100ns = ((ulong)value.High << 32) | value.Low;
        if (fileTime100ns < FileTimeUnixEpoch100ns) return 0;
        return checked((long)((fileTime100ns - FileTimeUnixEpoch100ns) / 10_000UL));
    }
}
