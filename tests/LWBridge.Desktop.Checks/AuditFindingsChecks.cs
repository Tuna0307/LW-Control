using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using LWBridge.Desktop;
using Microsoft.Data.Sqlite;
using M = LWBridge.Map317;

namespace LWBridge.Desktop.Checks;

// Audit 2026-10-23 (artifacts/WHOLE-PROGRAM-AUDIT-2026-10-23.md) W04, W05 and W09, adapted from the
// audit's probes against the production classes with isolated storage. Each fails on the code before
// the fix. W06 (two app processes) is covered by the audit's child-process probe, not here.
internal static class AuditFindingsChecks
{
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("AUDIT_FINDINGS: " + message);
    }

    internal static async Task RunAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "lwbridge-audit-findings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            CityDraftClearNeverReusesRevision(root);
            await ProfileCapacityHoldsAcrossStoresAsync(root);
            await CityExportIsOneSnapshotAsync(root);
            Console.WriteLine("AUDIT_FINDINGS_CHECKS_OK W04 draft-revision W05 profile-capacity W09 export-snapshot");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    // W04: after Clear and a new draft, an editor holding the old revision must conflict.
    private static void CityDraftClearNeverReusesRevision(string root)
    {
        using var store = new ProfileStateStore(Path.Combine(root, "city.db"));
        var old = store.Save("audit", "city_layout_draft_v1", 0, JsonSerializer.SerializeToElement(new { plan = "old" }));
        var cleared = store.Clear("audit", "city_layout_draft_v1", old.Revision);
        Require(cleared.Value is null && cleared.Revision > old.Revision, "Clear keeps a higher cleared revision");
        var fresh = store.Save("audit", "city_layout_draft_v1", 0, JsonSerializer.SerializeToElement(new { plan = "new" }));
        Require(fresh.Revision > cleared.Revision, "A new draft after Clear gets a fresh revision");
        try
        {
            store.Save("audit", "city_layout_draft_v1", old.Revision, JsonSerializer.SerializeToElement(new { plan = "stale" }));
            Require(false, "A stale editor revision must not overwrite the new draft");
        }
        catch (BridgeCommandException) { }
        Require(store.Read("audit", "city_layout_draft_v1").Value?.GetProperty("plan").GetString() == "new", "New draft kept");
    }

    // W05: two registry owners (two app windows) racing for the last slot never exceed capacity.
    private static async Task ProfileCapacityHoldsAcrossStoresAsync(string root)
    {
        for (int trial = 0; trial < 20; trial++)
        {
            string db = Path.Combine(root, $"profiles-{trial}.db");
            using var a = new ProfileRegistryStore(db);
            a.EnsureLocalProfile("audit", "Audit", 1);
            using var b = new ProfileRegistryStore(db);
            using var barrier = new Barrier(2);
            Task Attempt(ProfileRegistryStore store) => Task.Factory.StartNew(() =>
            {
                barrier.SignalAndWait();
                try { store.CreateLocalSecondaryProfile(2, 2); } catch (BridgeCommandException) { }
            }, TaskCreationOptions.LongRunning);
            await Task.WhenAll(Attempt(a), Attempt(b));
            int count = a.Read(2).Profiles.Count;
            Require(count == 2, $"capacity 2 holds across two stores (trial {trial}: {count})");
        }
    }

    // W09: a City export while another connection keeps rewriting every row holds ONE version.
    private static async Task CityExportIsOneSnapshotAsync(string root)
    {
        string mapDb = Path.Combine(root, "map.db");
        using var map = new M.MapStore(mapDb);
        using (var c = new SqliteConnection("Data Source=" + mapDb))
        {
            c.Open();
            using var tx = c.BeginTransaction();
            using var q = c.CreateCommand();
            q.Transaction = tx;
            q.CommandText = "INSERT INTO map_records(kind,server_id,record_key,point_index,updated_at,data_json) VALUES ('city',123,$key,$id,1,$json)";
            var key = q.Parameters.Add("$key", SqliteType.Text);
            var id = q.Parameters.Add("$id", SqliteType.Integer);
            var json = q.Parameters.Add("$json", SqliteType.Text);
            for (int i = 1; i <= 4000; i++)
            {
                key.Value = i.ToString();
                id.Value = i;
                json.Value = JsonSerializer.Serialize(new { ownerName = "SNAPSHOT_A", ownerUid = i.ToString(), serverId = 123, x = i % 1000, y = i / 1000, padding = new string('x', 1500) });
                q.ExecuteNonQuery();
            }
            tx.Commit();
        }
        using var cancellation = new CancellationTokenSource();
        using var writerReady = new ManualResetEventSlim();
        int updates = 0;
        Task writer = Task.Factory.StartNew(() =>
        {
            using var c = new SqliteConnection("Data Source=" + mapDb);
            c.Open();
            writerReady.Set();
            while (!cancellation.IsCancellationRequested)
            {
                using var q = c.CreateCommand();
                q.CommandText = "UPDATE map_records SET data_json=json_set(data_json,'$.ownerName',$name)";
                q.Parameters.AddWithValue("$name", Interlocked.Increment(ref updates) % 2 == 0 ? "SNAPSHOT_A" : "SNAPSHOT_B");
                try { q.ExecuteNonQuery(); } catch (SqliteException) { }
                Thread.Sleep(5);
            }
        }, TaskCreationOptions.LongRunning);
        writerReady.Wait();
        string output = Path.Combine(root, "export.xlsx");
        try
        {
            new M.MapExporter(map).ExportCitiesToPath(
                new M.MapQuery("city", 123, Sorts: new[] { new M.MapSort("updatedAt", "asc") }),
                new M.CityExportOptions(Enumerable.Range(1, 12).Select(i => "H" + i).ToArray(), "Cities", "Yes", "No"), output);
        }
        finally { cancellation.Cancel(); await writer; }
        using var zip = ZipFile.OpenRead(output);
        using var sheet = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        string[] versions = XDocument.Load(sheet).Descendants(ns + "t").Select(x => x.Value)
            .Where(x => x.StartsWith("SNAPSHOT_", StringComparison.Ordinal)).Distinct().ToArray();
        Require(versions.Length == 1, "export rows come from one database version: " + string.Join(",", versions));
    }
}
