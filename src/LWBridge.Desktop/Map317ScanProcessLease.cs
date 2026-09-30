namespace LWBridge.Desktop;

internal sealed class Map317ScanProcessLease : IDisposable
{
    internal const string InterruptedError = "map scan interrupted by application restart";

    private FileStream? stream;

    private Map317ScanProcessLease(FileStream stream) => this.stream = stream;

    internal static Map317ScanProcessLease? TryAcquire(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        string lockPath = databasePath + ".scan-owner.lock";
        try
        {
            var owned = new FileStream(
                lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None,
                bufferSize: 1, FileOptions.None);
            owned.SetLength(0);
            using (var writer = new StreamWriter(owned, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                writer.Write($"pid={Environment.ProcessId};acquiredAt={DateTimeOffset.UtcNow:O}");
                writer.Flush();
            }
            owned.Flush(flushToDisk: true);
            owned.Position = 0;
            return new Map317ScanProcessLease(owned);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException error)
        {
            throw new BridgeCommandException(
                "MAP_SCAN_LOCK_FAILED",
                "LWBridge could not acquire the profile map scan owner lock.",
                error.Message);
        }
    }

    public void Dispose() => Interlocked.Exchange(ref stream, null)?.Dispose();
}
