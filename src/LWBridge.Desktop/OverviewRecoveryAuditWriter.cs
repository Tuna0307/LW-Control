using System.Text;
using System.Text.Json;

namespace LWBridge.Desktop;

// Recovery evidence survives runtime cleanup and log rotation. Only callers'
// explicitly selected diagnostic fields belong here, never payloads or tokens.
internal static class OverviewRecoveryAuditWriter
{
    internal const int MaximumFileBytes = 128 * 1024;
    private static readonly object Gate = new();

    internal static void Append(string directory, object row)
    {
        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(row) + "\n");
            if (bytes.Length > 4096) return;
            lock (Gate)
            {
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "recovery-events.jsonl");
                if (File.Exists(path) && new FileInfo(path).Length + bytes.Length > MaximumFileBytes)
                    File.Move(path, Path.Combine(directory, "recovery-events.previous.jsonl"), overwrite: true);
                using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
                stream.Write(bytes);
            }
        }
        catch (IOException) { } // Diagnostics never change recovery behavior.
        catch (UnauthorizedAccessException) { }
    }
}
