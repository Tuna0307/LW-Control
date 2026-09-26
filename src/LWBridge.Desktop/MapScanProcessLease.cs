namespace LWBridge.Desktop;

internal static class MapScanRestartPolicy
{
    internal const string InterruptedError = "map scan interrupted by application restart";
}

internal sealed class MapScanProcessLease : IDisposable
{
    private readonly FileStream? fileStream;
    private readonly SemaphoreSlim? inMemorySemaphore;
    private bool disposed;

    private MapScanProcessLease(FileStream fileStream)
    {
        this.fileStream = fileStream;
    }

    private MapScanProcessLease(SemaphoreSlim inMemorySemaphore)
    {
        this.inMemorySemaphore = inMemorySemaphore;
    }

    internal static MapScanProcessLease? TryAcquire(MapDataStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        if (store.DatabasePath == ":memory:")
        {
            if (!store.InMemoryScanLease.Wait(0)) return null;
            return new MapScanProcessLease(store.InMemoryScanLease);
        }

        string lockPath = store.DatabasePath + ".scan-owner.lock";
        try
        {
            var stream = new FileStream(
                lockPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1,
                FileOptions.None);
            stream.SetLength(0);
            using (var writer = new StreamWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                writer.Write($"pid={Environment.ProcessId};acquiredAt={DateTimeOffset.UtcNow:O}");
                writer.Flush();
            }
            stream.Flush(flushToDisk: true);
            stream.Position = 0;
            return new MapScanProcessLease(stream);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new BridgeCommandException(
                "MAP_SCAN_LOCK_FAILED",
                "LWBridge could not acquire the profile map scan owner lock.",
                ex.Message);
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        fileStream?.Dispose();
        inMemorySemaphore?.Release();
    }
}
