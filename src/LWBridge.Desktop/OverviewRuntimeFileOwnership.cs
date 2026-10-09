using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace LWBridge.Desktop;

internal enum OverviewRuntimeFileMutation { Write, Delete }

/// <summary>
/// Clone-internal Windows ownership transaction for the existing shared runtime
/// filenames. Sharing restrictions protect the opened identity from other owners
/// (including helpers that atomically replace a pathname), not just this instance.
/// Successful readers can observe only complete snapshots; contended I/O fails
/// closed instead of changing a foreign or unverifiable destination.
/// </summary>
internal static class OverviewRuntimeFileOwnership
{
    internal static bool TryWrite(
        string path,
        byte[] contents,
        Func<byte[], bool> matchesOwner,
        Action<string, OverviewRuntimeFileMutation>? beforeMutation = null)
    {
        try
        {
            FileStream stream;
            bool created = false;
            try
            {
                stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            catch (FileNotFoundException)
            {
                // CreateNew admits only an absent pathname. A foreign creator that
                // wins this race makes admission fail; it is never overwritten.
                stream = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
                created = true;
            }
            using (stream)
            {
                if (!created && !matchesOwner(ReadSnapshot(stream))) return false;
                beforeMutation?.Invoke(path, OverviewRuntimeFileMutation.Write);
                stream.Position = 0;
                stream.Write(contents);
                stream.SetLength(contents.Length);
                stream.Flush(flushToDisk: true);
                return true;
            }
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    internal static bool TryDelete(
        string path,
        Func<byte[], bool> matchesOwner,
        Action<string, OverviewRuntimeFileMutation>? beforeMutation = null)
    {
        // DELETE is requested on this exact identity. Denying write/delete sharing
        // blocks both in-place rewrites and atomic pathname replacement until the
        // same handle is marked for deletion and closed.
        using SafeFileHandle handle = CreateFileW(
            path, GenericRead | DeleteAccess, 0, IntPtr.Zero,
            OpenExisting, FileAttributeNormal, IntPtr.Zero);
        if (handle.IsInvalid) return false;
        try
        {
            using var stream = new FileStream(handle, FileAccess.Read);
            if (!matchesOwner(ReadSnapshot(stream))) return false;
            beforeMutation?.Invoke(path, OverviewRuntimeFileMutation.Delete);
            var disposition = new FileDispositionInfo { DeleteFile = 1 };
            return SetFileInformationByHandle(
                handle, FileDispositionInfoClass, ref disposition,
                (uint)Marshal.SizeOf<FileDispositionInfo>());
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    private static byte[] ReadSnapshot(FileStream stream)
    {
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        return bytes.ToArray();
    }

    private const uint GenericRead = 0x80000000;
    private const uint DeleteAccess = 0x00010000;
    private const uint OpenExisting = 3;
    private const uint FileAttributeNormal = 0x80;
    private const int FileDispositionInfoClass = 4;

    [StructLayout(LayoutKind.Sequential)]
    private struct FileDispositionInfo { internal byte DeleteFile; }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string fileName, uint desiredAccess, uint shareMode,
        IntPtr securityAttributes, uint creationDisposition,
        uint flagsAndAttributes, IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(
        SafeFileHandle file, int informationClass,
        ref FileDispositionInfo information, uint bufferSize);
}
