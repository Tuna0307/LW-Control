using System.Text;

namespace LWBridge.Desktop;

/// <summary>
/// Incremental reader for the Player/Launcher/Updater logs watched by the original game recovery
/// (0x41a18d builds the three paths, 0x41e950 starts a reader at the current file length,
/// 0x41e9bb returns the text appended since the previous read).
/// </summary>
internal interface IRecoveryLogReader
{
    /// <summary>Start observing at the current end of the file (offset = length, or 0 if unreadable).</summary>
    void Begin();

    /// <summary>Text appended since the previous read (empty when unreadable or unchanged).</summary>
    string ReadNew();
}

internal sealed class FileRecoveryLogReader : IRecoveryLogReader
{
    // 0x41eb19: at most 0x400000 bytes per read.
    private const long MaximumReadBytes = 0x400000;
    private readonly string path;
    private long offset;

    internal FileRecoveryLogReader(string path) => this.path = path;

    public void Begin()
    {
        try { offset = new FileInfo(path).Length; }
        catch { offset = 0; }
    }

    public string ReadNew()
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            long length = stream.Length;
            if (length < offset) offset = 0;                      // 0x41ead3-0x41eadd: truncated/replaced log
            long toRead = Math.Min(length - offset, MaximumReadBytes);
            if (toRead <= 0) return string.Empty;
            stream.Seek(offset, SeekOrigin.Begin);
            var buffer = new byte[toRead];
            int read = 0;
            while (read < buffer.Length)
            {
                int n = stream.Read(buffer, read, buffer.Length - read);
                if (n <= 0) break;
                read += n;
            }
            offset += read;
            return Encoding.UTF8.GetString(buffer, 0, read);
        }
        catch
        {
            return string.Empty;
        }
    }
}
