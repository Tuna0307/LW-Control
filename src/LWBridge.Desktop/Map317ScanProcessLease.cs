namespace LWBridge.Desktop;

internal sealed class Map317ScanProcessLeaseTestHooks
{
    internal Action<FileStream, string>? WriteMetadata { get; init; }
    internal Action<FileStream>? FlushToDisk { get; init; }
}

internal sealed class Map317ScanProcessLease : IDisposable
{
    internal const string InterruptedError = "map scan interrupted by application restart";

    private FileStream? stream;

    private Map317ScanProcessLease(FileStream stream) => this.stream = stream;

    internal static Map317ScanProcessLease? TryAcquire(
        string databasePath,
        Map317ScanProcessLeaseTestHooks? testHooks = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        string lockPath = databasePath + ".scan-owner.lock";
        FileStream owned;
        try
        {
            owned = new FileStream(
                lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None,
                bufferSize: 1, FileOptions.None);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException error)
        {
            throw LockFailure(error);
        }

        try
        {
            owned.SetLength(0);
            string metadata = $"pid={Environment.ProcessId};acquiredAt={DateTimeOffset.UtcNow:O}";
            if (testHooks?.WriteMetadata is { } writeMetadata)
            {
                writeMetadata(owned, metadata);
            }
            else
            {
                using var writer = new StreamWriter(owned, System.Text.Encoding.UTF8, leaveOpen: true);
                writer.Write(metadata);
                writer.Flush();
            }
            if (testHooks?.FlushToDisk is { } flushToDisk)
                flushToDisk(owned);
            else
                owned.Flush(flushToDisk: true);
            owned.Position = 0;
            return new Map317ScanProcessLease(owned);
        }
        catch (Exception error)
        {
            owned.Dispose();
            if (error is IOException or UnauthorizedAccessException)
                throw LockFailure(error);
            throw;
        }
    }

    private static BridgeCommandException LockFailure(Exception error) =>
        new(
            "MAP_SCAN_LOCK_FAILED",
            "LWBridge could not acquire the profile map scan owner lock.",
            error.Message);

    public void Dispose() => Interlocked.Exchange(ref stream, null)?.Dispose();
}
