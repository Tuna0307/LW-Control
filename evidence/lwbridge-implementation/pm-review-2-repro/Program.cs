using System.Text.Json;
using LWBridge.Desktop;

string auditRoot = Path.Combine(Directory.GetCurrentDirectory(), ".codex-live", "pm-review-2", "cases", Guid.NewGuid().ToString("N"));
var results = new List<object>();

string staleRoot = Path.Combine(auditRoot, "stale-owner");
var first = new LocalConfigStore(staleRoot);
var second = new LocalConfigStore(staleRoot);
var backend = new LWBridgeBackend(first);
second.Update(c => c with { AutoReconnect = true });
using var save = JsonDocument.Parse("{\"autoLaunchGame\":false}");
await backend.InvokeAsync("local_config_set", save.RootElement.Clone(), CancellationToken.None);
bool retained = new LocalConfigStore(staleRoot).Snapshot.AutoReconnect;
results.Add(new { id = "PM2-01", scenario = "backend partial save preserves another owner's committed AutoReconnect", expected = true, actual = retained, passed = retained });

string missingRoot = Path.Combine(auditRoot, "missing-primary");
var missing = new LocalConfigStore(missingRoot);
string profile = missing.Snapshot.ProfileId;
missing.Update(c => c with { AutoReconnect = true });
File.Delete(Path.Combine(missingRoot, "config.json"));
bool sameProfile = new LocalConfigStore(missingRoot).Snapshot.ProfileId == profile;
results.Add(new { id = "PM2-02", scenario = "missing primary with valid backup preserves profile identity", expected = true, actual = sameProfile, passed = sameProfile });

string foreignRoot = Path.Combine(auditRoot, "foreign-primary");
var foreign = new LocalConfigStore(foreignRoot);
foreign.Update(c => c with { AutoReconnect = true });
string foreignPath = Path.Combine(foreignRoot, "config.json");
string foreignBytes = JsonSerializer.Serialize(new { schemaVersion = 1, owner = "OtherApplication", profileId = "foreign" });
File.WriteAllText(foreignPath, foreignBytes);
string outcome;
try { _ = new LocalConfigStore(foreignRoot); outcome = "loaded-backup-and-replaced-primary"; }
catch (LocalConfigStoreException ex) { outcome = ex.Code; }
bool preserved = File.ReadAllText(foreignPath) == foreignBytes;
results.Add(new { id = "PM2-03", scenario = "foreign-owner primary with valid backup is not automatically replaced", actual = outcome, primaryPreserved = preserved, passed = preserved });

using var executor = new NativeRequestExecutor();
var release = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
Task<NativeRequestExecution> pending = executor.ExecuteAsync("late", _ => release.Task);
executor.Close();
release.SetResult(new { done = true });
try {
    NativeRequestExecution completion = await pending;
    results.Add(new { id = "PM2-04", scenario = "noncooperative operation returns after close", actual = completion.Status.ToString(), passed = completion.Status == NativeRequestExecutionStatus.Cancelled });
} catch (Exception ex) {
    results.Add(new { id = "PM2-04", scenario = "noncooperative operation returns after close", actual = ex.GetType().Name, passed = false });
}
string json = JsonSerializer.Serialize(new { source = "current production classes via project reference", isolation = "unique scratch config directories; no live game commands", results }, new JsonSerializerOptions { WriteIndented = true });
File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), ".codex-live", "pm-review-2", "edge-cases.json"), json);
Console.WriteLine(json);
