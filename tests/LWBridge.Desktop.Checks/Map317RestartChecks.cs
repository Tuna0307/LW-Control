using System.Runtime.CompilerServices;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class Map317RestartChecks
{
    [ModuleInitializer]
    internal static void Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "lwb317-map317-restart-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string database = Path.Combine(root, "map-data.db");
        try
        {
            using Map317ScanProcessLease first = Map317ScanProcessLease.TryAcquire(database)
                ?? throw new InvalidOperationException("first Map317 scan lease acquisition failed");
            if (Map317ScanProcessLease.TryAcquire(database) is not null)
                throw new InvalidOperationException("concurrent Map317 scan lease acquisition was not rejected");
            first.Dispose();
            using Map317ScanProcessLease second = Map317ScanProcessLease.TryAcquire(database)
                ?? throw new InvalidOperationException("Map317 scan lease was not reusable after owner release");
        }
        finally
        {
            try { File.Delete(database + ".scan-owner.lock"); } catch { }
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }
}
