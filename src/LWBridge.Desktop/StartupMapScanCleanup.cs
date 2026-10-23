namespace LWBridge.Desktop;

internal static class StartupMapScanCleanup
{
    internal static IReadOnlyList<string> Clear(string applicationRoot)
    {
        string profiles = Path.Combine(applicationRoot, "profiles");
        if (!Directory.Exists(profiles)) return [];
        var databases = Directory.EnumerateDirectories(profiles)
            .SelectMany(profile => new[] {
                (Path: Path.Combine(profile, "map-data", "map-data.db"), Legacy: false),
                (Path: Path.Combine(profile, "map-data.db"), Legacy: true),
            }).Where(database => File.Exists(database.Path)).ToArray();
        var leases = new List<Map317ScanProcessLease>();
        try
        {
            // Do not erase rows underneath another process's live scan.
            foreach (var database in databases)
                leases.Add(Map317ScanProcessLease.TryAcquire(database.Path) ??
                    throw new BridgeCommandException("MAP_SCAN_ACTIVE", "Another app is scanning this profile; close it before restarting."));
            foreach (var database in databases)
            {
                if (database.Legacy)
                {
                    using var store = new MapDataStore(database.Path);
                    store.ClearAllScanData();
                }
                else
                {
                    using var store = new LWBridge.Map317.MapStore(database.Path);
                    store.ClearAllScanData();
                }
            }
            return databases.Select(database => database.Path).ToArray();
        }
        finally { foreach (var lease in leases) lease.Dispose(); }
    }
}
