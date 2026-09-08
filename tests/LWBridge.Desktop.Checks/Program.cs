using System.Text.Json;
using LWBridge.Desktop;

var failures = new List<string>();

void Check(bool condition, string name)
{
    if (!condition) failures.Add(name);
}

async Task ExpectBridgeError(string expectedCode, string name, Func<Task> action)
{
    try
    {
        await action();
        failures.Add(name);
    }
    catch (BridgeCommandException error)
    {
        Check(error.Code == expectedCode, name + $" (expected {expectedCode}, got {error.Code})");
    }
}

void ExpectConfigError(string expectedCode, string name, Action action)
{
    try
    {
        action();
        failures.Add(name);
    }
    catch (LocalConfigStoreException error)
    {
        Check(error.Code == expectedCode, name + $" (expected {expectedCode}, got {error.Code})");
    }
}

string FindRepoRoot()
{
    DirectoryInfo? current = new(Directory.GetCurrentDirectory());
    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "task.md")) &&
            Directory.Exists(Path.Combine(current.FullName, "src", "LWBridge.Desktop")))
            return current.FullName;
        current = current.Parent;
    }
    throw new InvalidOperationException("Could not locate repository root for deterministic source checks.");
}

string repoRoot = FindRepoRoot();
bool verifyRealConfigUnchanged = args.Contains("--verify-real-config-unchanged", StringComparer.OrdinalIgnoreCase);
string realConfigPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "LWBridgeRebuild", "config.json");
byte[]? realConfigBefore = verifyRealConfigUnchanged && File.Exists(realConfigPath)
    ? File.ReadAllBytes(realConfigPath)
    : null;
bool realConfigExistedBefore = verifyRealConfigUnchanged && File.Exists(realConfigPath);

// Deterministic config/persistence checks use isolated temporary storage.
string configRoot = Path.Combine(Path.GetTempPath(), "lwbridge-checks-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(configRoot);
try
{
    var store = new LocalConfigStore(configRoot);
    string originalProfileId = store.Snapshot.ProfileId;
    string configPath = Path.Combine(configRoot, "config.json");
    Check(File.Exists(configPath), "new isolated config is persisted");

    using (JsonDocument saved = JsonDocument.Parse(File.ReadAllText(configPath)))
    {
        Check(saved.RootElement.GetProperty("schemaVersion").GetInt32() == LWBridgeLocalConfig.CurrentSchemaVersion,
            "config writes schema version");
        Check(saved.RootElement.GetProperty("owner").GetString() == LWBridgeLocalConfig.CurrentOwner,
            "config writes ownership marker");
    }

    store.Update(c => c with { AutoReconnect = true });
    var reloaded = new LocalConfigStore(configRoot);
    Check(reloaded.Snapshot.ProfileId == originalProfileId, "profile identity survives restart");
    Check(reloaded.Snapshot.AutoReconnect, "saved preference survives restart");

    // Two independently loaded owners must refresh the committed baseline under
    // the storage lock instead of losing the first writer's successful change.
    var writerA = new LocalConfigStore(configRoot);
    var writerB = new LocalConfigStore(configRoot);
    writerA.Update(c => c with { AutoLaunchGame = false });
    writerB.Update(c => c with { AutoReconnect = false });
    var afterCompetingWriters = new LocalConfigStore(configRoot);
    Check(!afterCompetingWriters.Snapshot.AutoLaunchGame && !afterCompetingWriters.Snapshot.AutoReconnect,
        "competing config owners serialize and preserve both committed changes");

    // A crash can leave an uncommitted uniquely-named temp file. It must not be
    // treated as authoritative on restart or replace the last committed bytes.
    byte[] committedBeforeAbandonedTemp = File.ReadAllBytes(configPath);
    string abandonedTemp = configPath + ".tmp.abandoned";
    File.WriteAllText(abandonedTemp, "{\"schemaVersion\":1,\"owner\":\"LWBridgeRebuild\",\"profileId\":\"uncommitted\"}");
    var afterAbandonedTemp = new LocalConfigStore(configRoot);
    Check(afterAbandonedTemp.Snapshot.ProfileId == originalProfileId,
        "abandoned replacement temp cannot replace committed profile identity");
    Check(File.ReadAllBytes(configPath).SequenceEqual(committedBeforeAbandonedTemp),
        "abandoned replacement temp leaves committed config bytes unchanged");

    store.Update(c => c with { ServerJumpHistory = new[] { 9, 9, 0, 100000, 8, 7, 6, 5, 4 } });
    var historyReloaded = new LocalConfigStore(configRoot);
    Check(historyReloaded.Snapshot.ServerJumpHistory.SequenceEqual(new[] { 9, 8, 7, 6, 5 }),
        "server-jump history persists normalized unique IDs with recovered five-item limit");

    // A valid backup must recover identity without silently inventing a new profile.
    File.WriteAllText(configPath, "{ definitely not valid json");
    var recovered = new LocalConfigStore(configRoot);
    Check(recovered.Snapshot.ProfileId == originalProfileId, "corrupt config recovers stable profile identity from backup");
    Check(Directory.GetFiles(configRoot, "config.json.corrupt.*").Length == 1,
        "corrupt primary is preserved during recovery");
}
finally
{
    try { Directory.Delete(configRoot, recursive: true); }
    catch { }
}

string backendPartialRoot = Path.Combine(Path.GetTempPath(), "lwbridge-backend-partial-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(backendPartialRoot);
try
{
    var ownerAStore = new LocalConfigStore(backendPartialRoot);
    var ownerBStore = new LocalConfigStore(backendPartialRoot);
    var ownerABackend = new LWBridgeBackend(ownerAStore);
    var ownerBBackend = new LWBridgeBackend(ownerBStore);
    string originalProfileId = ownerABackend.ProfileId;

    using (JsonDocument reconnectPayload = JsonDocument.Parse(JsonSerializer.Serialize(new
    {
        profileId = ownerBBackend.ProfileId,
        name = "autoForceUpdateReload",
        enabled = true,
    })))
    {
        await ownerBBackend.InvokeAsync("set_automation", reconnectPayload.RootElement.Clone(), CancellationToken.None);
    }
    ownerBStore.Update(c => c with { GameRoot = @"C:\LastWar\RecoveredRoot" });
    using (JsonDocument ownerBHistoryPayload = JsonDocument.Parse(JsonSerializer.Serialize(new
    {
        profileId = ownerBBackend.ProfileId,
        history = new[] { 501, 502, 503 },
    })))
    {
        await ownerBBackend.InvokeAsync("server_jump_history_set", ownerBHistoryPayload.RootElement.Clone(), CancellationToken.None);
    }

    using (JsonDocument partialSave = JsonDocument.Parse("{\"autoLaunchGame\":false}"))
    {
        await ownerABackend.InvokeAsync("local_config_set", partialSave.RootElement.Clone(), CancellationToken.None);
    }

    LWBridgeLocalConfig persisted = new LocalConfigStore(backendPartialRoot).Snapshot;
    Check(persisted.ProfileId == originalProfileId && !persisted.AutoLaunchGame && persisted.AutoReconnect,
        "backend partial config save preserves profile identity and another owner's reconnect update");
    Check(persisted.GameRoot == @"C:\LastWar\RecoveredRoot" &&
          persisted.ServerJumpHistory.SequenceEqual(new[] { 501, 502, 503 }),
        "backend partial config save preserves another owner's root and server history updates");
}
finally
{
    try { Directory.Delete(backendPartialRoot, recursive: true); }
    catch { }
}

string missingPrimaryRoot = Path.Combine(Path.GetTempPath(), "lwbridge-missing-primary-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(missingPrimaryRoot);
try
{
    var store = new LocalConfigStore(missingPrimaryRoot);
    string originalProfileId = store.Snapshot.ProfileId;
    store.Update(c => c with { AutoReconnect = true });
    File.Delete(Path.Combine(missingPrimaryRoot, "config.json"));

    var recovered = new LocalConfigStore(missingPrimaryRoot);
    Check(recovered.Snapshot.ProfileId == originalProfileId,
        "missing primary recovers stable profile identity from valid owned backup");
    Check(File.Exists(Path.Combine(missingPrimaryRoot, "config.json")),
        "missing-primary recovery restores a primary config from the owned backup");
}
finally
{
    try { Directory.Delete(missingPrimaryRoot, recursive: true); }
    catch { }
}

string missingPrimaryIncompatibleBackupRoot = Path.Combine(Path.GetTempPath(), "lwbridge-missing-primary-incompatible-backup-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(missingPrimaryIncompatibleBackupRoot);
try
{
    string backupPath = Path.Combine(missingPrimaryIncompatibleBackupRoot, "config.backup.json");
    byte[] backupBytes = System.Text.Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"owner\":\"OtherApplication\",\"profileId\":\"foreign-backup\"}");
    File.WriteAllBytes(backupPath, backupBytes);

    ExpectConfigError("CONFIG_OWNER_MISMATCH", "missing primary does not convert an incompatible backup into a new install", () =>
        new LocalConfigStore(missingPrimaryIncompatibleBackupRoot));
    Check(!File.Exists(Path.Combine(missingPrimaryIncompatibleBackupRoot, "config.json")) &&
          File.ReadAllBytes(backupPath).SequenceEqual(backupBytes),
        "incompatible backup remains byte-for-byte unchanged when the primary is absent");
}
finally
{
    try { Directory.Delete(missingPrimaryIncompatibleBackupRoot, recursive: true); }
    catch { }
}

string writeFailureRoot = Path.Combine(Path.GetTempPath(), "lwbridge-write-failure-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(writeFailureRoot);
try
{
    var store = new LocalConfigStore(writeFailureRoot);
    bool original = store.Snapshot.AutoLaunchGame;
    Directory.CreateDirectory(Path.Combine(writeFailureRoot, "config.backup.json"));
    ExpectConfigError("CONFIG_WRITE_FAILED", "failed durable write rejects update", () =>
        store.Update(c => c with { AutoLaunchGame = !original }));
    Check(store.Snapshot.AutoLaunchGame == original, "failed durable write leaves in-memory config unchanged");
}
finally
{
    try { Directory.Delete(writeFailureRoot, recursive: true); }
    catch { }
}

string invalidOwnerRoot = Path.Combine(Path.GetTempPath(), "lwbridge-invalid-owner-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(invalidOwnerRoot);
try
{
    string configPath = Path.Combine(invalidOwnerRoot, "config.json");
    byte[] foreignBytes = System.Text.Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"owner\":\"OtherApp\",\"profileId\":\"keep-me\"}");
    File.WriteAllBytes(configPath, foreignBytes);
    ExpectConfigError("CONFIG_OWNER_MISMATCH", "foreign config ownership fails closed", () => new LocalConfigStore(invalidOwnerRoot));
    Check(File.ReadAllBytes(configPath).SequenceEqual(foreignBytes),
        "foreign config without backup is left byte-for-byte untouched after rejection");
}
finally
{
    try { Directory.Delete(invalidOwnerRoot, recursive: true); }
    catch { }
}

string futureSchemaRoot = Path.Combine(Path.GetTempPath(), "lwbridge-future-schema-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(futureSchemaRoot);
try
{
    string configPath = Path.Combine(futureSchemaRoot, "config.json");
    byte[] futureBytes = System.Text.Encoding.UTF8.GetBytes("{\"schemaVersion\":999,\"owner\":\"LWBridgeRebuild\",\"profileId\":\"future\"}");
    File.WriteAllBytes(configPath, futureBytes);
    ExpectConfigError("CONFIG_SCHEMA_UNSUPPORTED", "future config schema fails closed", () => new LocalConfigStore(futureSchemaRoot));
    Check(File.ReadAllBytes(configPath).SequenceEqual(futureBytes),
        "future-schema config without backup is left byte-for-byte untouched after rejection");
}
finally
{
    try { Directory.Delete(futureSchemaRoot, recursive: true); }
    catch { }
}

string incompatibleWithBackupRoot = Path.Combine(Path.GetTempPath(), "lwbridge-incompatible-backup-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(incompatibleWithBackupRoot);
try
{
    var store = new LocalConfigStore(incompatibleWithBackupRoot);
    store.Update(c => c with { AutoReconnect = true });
    string configPath = Path.Combine(incompatibleWithBackupRoot, "config.json");
    byte[] foreignBytes = System.Text.Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"owner\":\"OtherApplication\",\"profileId\":\"foreign-with-backup\"}");
    File.WriteAllBytes(configPath, foreignBytes);
    ExpectConfigError("CONFIG_OWNER_MISMATCH", "foreign primary is not replaced by valid backup", () => new LocalConfigStore(incompatibleWithBackupRoot));
    Check(File.ReadAllBytes(configPath).SequenceEqual(foreignBytes),
        "foreign primary remains byte-for-byte unchanged when a valid owned backup exists");

    byte[] futureBytes = System.Text.Encoding.UTF8.GetBytes("{\"schemaVersion\":999,\"owner\":\"LWBridgeRebuild\",\"profileId\":\"future-with-backup\"}");
    File.WriteAllBytes(configPath, futureBytes);
    ExpectConfigError("CONFIG_SCHEMA_UNSUPPORTED", "future-schema primary is not replaced by valid backup", () => new LocalConfigStore(incompatibleWithBackupRoot));
    Check(File.ReadAllBytes(configPath).SequenceEqual(futureBytes),
        "future-schema primary remains byte-for-byte unchanged when a valid owned backup exists");
}
finally
{
    try { Directory.Delete(incompatibleWithBackupRoot, recursive: true); }
    catch { }
}

string unreadablePrimaryRoot = Path.Combine(Path.GetTempPath(), "lwbridge-unreadable-primary-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(unreadablePrimaryRoot);
try
{
    string configPath = Path.Combine(unreadablePrimaryRoot, "config.json");
    Directory.CreateDirectory(configPath);
    ExpectConfigError("CONFIG_READ_FAILED", "unreadable primary storage is not treated as a new install", () => new LocalConfigStore(unreadablePrimaryRoot));
    Check(Directory.Exists(configPath), "unreadable primary storage is preserved after read failure");
}
finally
{
    try { Directory.Delete(unreadablePrimaryRoot, recursive: true); }
    catch { }
}

// Profile routing and command-boundary checks use an in-memory config.
var backend = new LWBridgeBackend(new LocalConfigStore(persistent: false));
using (JsonDocument readOnlyBootstrap = JsonDocument.Parse(JsonSerializer.Serialize(
    backend.GetBootstrap(fixture: false, sessionId: "test-session", suppressAutoLaunch: true), JsonOptions.Default)))
{
    Check(readOnlyBootstrap.RootElement.GetProperty("autoLaunchGame").ValueKind == JsonValueKind.False,
        "read-only production probe explicitly suppresses startup launch reconciliation");
}
using JsonDocument emptyPayload = JsonDocument.Parse("{}");
object? profileList = await backend.InvokeAsync("profile_list", emptyPayload.RootElement.Clone(), CancellationToken.None);
Check(profileList is not null, "global profile_list works without active profile payload");

await ExpectBridgeError("PROFILE_REQUIRED", "profile-scoped command rejects missing profile", async () =>
    await backend.InvokeAsync("get_status", emptyPayload.RootElement.Clone(), CancellationToken.None));

using JsonDocument foreignProfile = JsonDocument.Parse("{\"profileId\":\"foreign-profile\"}");
await ExpectBridgeError("PROFILE_SCOPE_MISMATCH", "foreign profile is rejected", async () =>
    await backend.InvokeAsync("profile_instance_status", foreignProfile.RootElement.Clone(), CancellationToken.None));

using JsonDocument profilePayload = JsonDocument.Parse(JsonSerializer.Serialize(new { profileId = backend.ProfileId }));
await ExpectBridgeError("OVERVIEW_LAUNCH_BOOTSTRAP_UNRECOVERED", "launch remains fail-closed until bootstrap is recovered", async () =>
    await backend.InvokeAsync("profile_instance_start", profilePayload.RootElement.Clone(), CancellationToken.None));

using JsonDocument scalarPayload = JsonDocument.Parse("\"bad\"");
await ExpectBridgeError("INVALID_PAYLOAD", "native boundary rejects non-object payloads", async () =>
    await backend.InvokeAsync("profile_list", scalarPayload.RootElement.Clone(), CancellationToken.None));

using JsonDocument historyPayload = JsonDocument.Parse(JsonSerializer.Serialize(new
{
    profileId = backend.ProfileId,
    history = new object[] { 101, 101, 0, 100000, "bad", 202, 303, 404, 505, 606 },
}));
object? normalizedHistory = await backend.InvokeAsync("server_jump_history_set", historyPayload.RootElement.Clone(), CancellationToken.None);
Check(JsonSerializer.Serialize(normalizedHistory, JsonOptions.Default) == "[101,202,303,404,505]",
    "server-jump history validates, deduplicates and caps persisted IDs");

using JsonDocument invalidHistoryPayload = JsonDocument.Parse(JsonSerializer.Serialize(new
{
    profileId = backend.ProfileId,
    history = "not-an-array",
}));
await ExpectBridgeError("INVALID_PAYLOAD", "server-jump history rejects non-array input", async () =>
    await backend.InvokeAsync("server_jump_history_set", invalidHistoryPayload.RootElement.Clone(), CancellationToken.None));

// Native request lifetime: duplicate IDs, explicit cancellation and teardown are deterministic.
using (var requests = new NativeRequestRegistry())
{
    Check(requests.TryStart("delayed", out CancellationTokenSource? delayed) && delayed is not null,
        "request registry accepts first request ID");
    Check(!requests.TryStart("delayed", out _), "request registry rejects duplicate active request ID");
    Task delayedWork = Task.Delay(TimeSpan.FromSeconds(30), delayed!.Token);
    Check(requests.Cancel("delayed"), "request registry accepts explicit cancellation");
    try
    {
        await delayedWork;
        failures.Add("explicit request cancellation reaches delayed native work");
    }
    catch (OperationCanceledException)
    {
        Check(true, "explicit request cancellation reaches delayed native work");
    }
    requests.Complete("delayed", delayed);

    Check(requests.TryStart("close-owned", out CancellationTokenSource? closeOwned) && closeOwned is not null,
        "request registry owns active request before teardown");
    requests.Close();
    Check(closeOwned!.Token.IsCancellationRequested, "session teardown cancels owned native work without disposing its token early");
    Check(!requests.TryStart("late", out _), "closed native session rejects late requests");
    requests.Complete("close-owned", closeOwned);
}

// Host-integrated async lifetime: the same executor used by LWBridgeWindow owns
// backend work, duplicate IDs, cancellation, teardown and publication eligibility.
using JsonDocument delayedPayload = JsonDocument.Parse(JsonSerializer.Serialize(new { profileId = backend.ProfileId }));

var explicitCancelService = new ControlledAsyncCommandService(ignoreCancellationWhileWaiting: false);
var explicitCancelBackend = new LWBridgeBackend(new LocalConfigStore(persistent: false), explicitCancelService);
using (var executor = new NativeRequestExecutor())
{
    using JsonDocument payload = JsonDocument.Parse(JsonSerializer.Serialize(new { profileId = explicitCancelBackend.ProfileId }));
    Task<NativeRequestExecution> pending = executor.ExecuteAsync("explicit-cancel", token =>
        explicitCancelBackend.InvokeAsync(ControlledAsyncCommandService.Command, payload.RootElement.Clone(), token));
    await explicitCancelService.Entered;
    Check(executor.ActiveCount == 1 && explicitCancelService.ActiveOwners == 1,
        "host executor has one owner while delayed backend work is active");

    NativeRequestExecution duplicate = await executor.ExecuteAsync("explicit-cancel", _ => Task.FromResult<object?>(new { unexpected = true }));
    Check(duplicate.Status == NativeRequestExecutionStatus.Rejected,
        "host executor rejects duplicate active request IDs before starting another operation");
    Check(executor.Cancel("explicit-cancel"), "host executor propagates explicit cancellation");

    NativeRequestExecution cancelled = await pending;
    Check(cancelled.Status == NativeRequestExecutionStatus.Cancelled,
        "cancelled backend work cannot publish a success result");
    Check(explicitCancelService.CommitCount == 0 && explicitCancelService.ActiveOwners == 0 && executor.ActiveCount == 0,
        "explicit cancellation drains service/request ownership without committing state");
}

var lateCompletionService = new ControlledAsyncCommandService(ignoreCancellationWhileWaiting: true);
var lateCompletionBackend = new LWBridgeBackend(new LocalConfigStore(persistent: false), lateCompletionService);
using (var executor = new NativeRequestExecutor())
{
    using JsonDocument payload = JsonDocument.Parse(JsonSerializer.Serialize(new { profileId = lateCompletionBackend.ProfileId }));
    Task<NativeRequestExecution> pending = executor.ExecuteAsync("late-completion", token =>
        lateCompletionBackend.InvokeAsync(ControlledAsyncCommandService.Command, payload.RootElement.Clone(), token));
    await lateCompletionService.Entered;
    Check(executor.Cancel("late-completion"), "timeout-style cancellation reaches a delayed backend owner");
    lateCompletionService.Release();

    NativeRequestExecution cancelled = await pending;
    Check(cancelled.Status == NativeRequestExecutionStatus.Cancelled && lateCompletionService.CommitCount == 0,
        "late completion after cancellation is rejected before service state mutation");
}

var closeService = new ControlledAsyncCommandService(ignoreCancellationWhileWaiting: true);
var closeBackend = new LWBridgeBackend(new LocalConfigStore(persistent: false), closeService);
using (var executor = new NativeRequestExecutor())
{
    using JsonDocument payload = JsonDocument.Parse(JsonSerializer.Serialize(new { profileId = closeBackend.ProfileId }));
    Task<NativeRequestExecution> pending = executor.ExecuteAsync("close-owned", token =>
        closeBackend.InvokeAsync(ControlledAsyncCommandService.Command, payload.RootElement.Clone(), token));
    await closeService.Entered;
    executor.Close();
    closeService.Release();

    NativeRequestExecution cancelled = await pending;
    Check(cancelled.Status == NativeRequestExecutionStatus.Cancelled && closeService.CommitCount == 0,
        "session close cancels delayed backend work and suppresses late success");
    NativeRequestExecution afterClose = await executor.ExecuteAsync("after-close", _ => Task.FromResult<object?>(null));
    Check(afterClose.Status == NativeRequestExecutionStatus.Rejected,
        "closed host executor rejects requests until a new session owner is created");
}

using (var reloadedExecutor = new NativeRequestExecutor())
{
    NativeRequestExecution reloaded = await reloadedExecutor.ExecuteAsync("after-close", _ => Task.FromResult<object?>(new { ok = true }));
    Check(reloaded.Status == NativeRequestExecutionStatus.Success,
        "new session owner accepts fresh work after prior session teardown");
}

using (var executor = new NativeRequestExecutor())
{
    var release = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
    Task<NativeRequestExecution> pending = executor.ExecuteAsync("noncooperative-return", _ => release.Task);
    executor.Close();
    release.SetResult(new { late = true });
    NativeRequestExecution completion = await pending;
    Check(completion.Status == NativeRequestExecutionStatus.Cancelled && executor.ActiveCount == 0,
        "noncooperative normal return after close resolves as cancelled and drains ownership");
}

using (var executor = new NativeRequestExecutor())
{
    var release = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
    Task<NativeRequestExecution> pending = executor.ExecuteAsync("noncooperative-fault", _ => release.Task);
    executor.Close();
    release.SetException(new InvalidOperationException("late closed-session fault"));
    NativeRequestExecution completion = await pending;
    Check(completion.Status == NativeRequestExecutionStatus.Cancelled && executor.ActiveCount == 0,
        "noncooperative late fault after close follows cancelled closed-session policy");
}

using (var executor = new NativeRequestExecutor())
{
    var release = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
    Task<NativeRequestExecution> pending = executor.ExecuteAsync("noncooperative-explicit-cancel", _ => release.Task);
    Check(executor.Cancel("noncooperative-explicit-cancel"), "explicit cancel owns a noncooperative request before late return");
    release.SetResult(new { late = true });
    NativeRequestExecution completion = await pending;
    Check(completion.Status == NativeRequestExecutionStatus.Cancelled && executor.ActiveCount == 0,
        "noncooperative normal return after explicit cancel cannot publish success");
}

for (int iteration = 0; iteration < 32; iteration++)
{
    using var executor = new NativeRequestExecutor();
    string requestId = "cancel-complete-race-" + iteration;
    var release = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
    Task<NativeRequestExecution> pending = executor.ExecuteAsync(requestId, _ => release.Task);
    using var startRace = new ManualResetEventSlim(false);
    Task cancel = Task.Run(() =>
    {
        startRace.Wait();
        executor.Cancel(requestId);
    });
    Task complete = Task.Run(() =>
    {
        startRace.Wait();
        release.TrySetResult(new { iteration });
    });
    startRace.Set();
    await Task.WhenAll(cancel, complete);
    NativeRequestExecution completion = await pending;
    Check(completion.Status is NativeRequestExecutionStatus.Success or NativeRequestExecutionStatus.Cancelled,
        "cancel/completion race resolves to one valid terminal status");
    Check(executor.ActiveCount == 0, "cancel/completion race drains request ownership");
}

var listenerOwners = new NativeSubscriptionRegistry(new[] { "bridge://status", "bridge://map-scan-status" });
Check(listenerOwners.Count == 0, "native listener ownership starts at baseline");
Check(listenerOwners.Listen("bridge://status") && !listenerOwners.Listen("bridge://status") && listenerOwners.Count == 1,
    "duplicate listen does not create duplicate native listener owners");
Check(listenerOwners.Unlisten("bridge://status") && listenerOwners.Count == 0,
    "navigation-style unlisten returns native listener ownership to baseline");
Check(!listenerOwners.Listen("bridge://not-allowed"), "native listener allowlist rejects unknown events");
listenerOwners.Listen("bridge://map-scan-status");
listenerOwners.Close();
Check(listenerOwners.Count == 0 && !listenerOwners.Listen("bridge://status"),
    "session teardown clears listeners and rejects late subscriptions");

// Recovered Map Data persistence schema and stable keys. The test supplies an
// explicit recordKey because per-kind record-key derivation remains unrecovered.
string mapStoreRoot = Path.Combine(Path.GetTempPath(), "lwbridge-map-store-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(mapStoreRoot);
string mapDatabasePath = Path.Combine(mapStoreRoot, "map-data.db");
const string largeOwnerUid = "900719925474099312345";
try
{
    using (var mapStore = new MapDataStore(mapDatabasePath))
    {
        IReadOnlyDictionary<string, string> schema = mapStore.ReadSchemaDefinitions();
        string[] recoveredTables =
        [
            "metadata", "map_records", "scan_runs", "scan_blocks", "scan_records", "player_marks",
            "app_settings", "treasure_claim_states", "dispatch_plunder_jobs", "truck_plunder_jobs",
            "truck_plunder_history", "dispatch_assist_jobs",
        ];
        Check(recoveredTables.All(schema.ContainsKey), "map store creates every recovered 0.3.1 table");
        Check(schema["map_records"].Contains("PRIMARY KEY (kind, server_id, record_key)", StringComparison.Ordinal),
            "map record identity is recovered as kind/server/record_key");
        Check(schema["scan_records"].Contains("PRIMARY KEY (run_id, kind, server_id, record_key)", StringComparison.Ordinal),
            "scan staging identity is recovered as run/kind/server/record_key");
        Check(schema["player_marks"].Contains("PRIMARY KEY (server_id, owner_uid)", StringComparison.Ordinal),
            "player mark identity is recovered as server/owner_uid");
        string[] recoveredIndexes =
        [
            "idx_map_kind_server", "idx_map_kind_server_quality_power", "idx_map_kind_server_level",
            "idx_map_kind_server_updated", "idx_map_kind_server_point", "idx_scan_records_run_kind",
            "idx_dispatch_plunder_due", "idx_truck_plunder_due", "idx_truck_plunder_history_updated",
            "idx_dispatch_assist_due", "idx_treasure_claim_states_expire",
        ];
        Check(recoveredIndexes.All(schema.ContainsKey), "map store creates every recovered 0.3.1 index");

        string firstCityJson = JsonSerializer.Serialize(new
        {
            serverId = 77,
            ownerUid = largeOwnerUid,
            ownerName = "First",
            allianceName = "ABC",
            level = 30,
            x = 10,
            y = 20,
            updatedAt = 1000L,
        });
        mapStore.UpsertRecord(new MapStoredRecord(
            "city", 77, "explicit-city-key", 15, "city-uuid", "First", "ABC",
            30, null, 9_000_000_000_000_000_000L, null, null, 1000, firstCityJson));

        string updatedCityJson = JsonSerializer.Serialize(new
        {
            serverId = 77,
            ownerUid = largeOwnerUid,
            ownerName = "Updated",
            allianceName = "ABC",
            level = 31,
            x = 11,
            y = 21,
            updatedAt = 2000L,
        });
        mapStore.UpsertRecord(new MapStoredRecord(
            "city", 77, "explicit-city-key", 16, "city-uuid-2", "Updated", "ABC",
            31, null, 9_000_000_000_000_000_001L, null, null, 2000, updatedCityJson));
        Check(mapStore.CountRecords("city", 77) == 1,
            "same recovered map identity updates instead of duplicating a row");
        MapStoredRecord? updatedRecord = mapStore.GetRecord("city", 77, "explicit-city-key");
        Check(updatedRecord?.UpdatedAt == 2000 && updatedRecord.Name == "Updated" &&
              updatedRecord.Power == 9_000_000_000_000_000_001L &&
              updatedRecord.DataJson.Contains(largeOwnerUid, StringComparison.Ordinal),
            "map upsert preserves updated fields, Int64 power and large string UIDs");

        mapStore.UpsertRecord(new MapStoredRecord(
            "city", 78, "explicit-city-key", 1, null, "Other server", null,
            1, null, null, null, null, 1000, "{\"ownerUid\":\"other\"}"));
        mapStore.UpsertRecord(new MapStoredRecord(
            "monster", 77, "explicit-city-key", 2, null, "Other kind", null,
            2, null, null, null, null, 1000, "{\"monsterNameKey\":\"m1\"}"));
        Check(mapStore.CountRecords("city", 78) == 1 && mapStore.CountRecords("monster", 77) == 1,
            "same record_key remains distinct across recovered kind/server identity dimensions");

        await ExpectBridgeError("INVALID_MAP_RECORD", "map store refuses guessed/missing record identity", () =>
            Task.Run(() => mapStore.UpsertRecord(new MapStoredRecord(
                "city", 77, "", null, null, null, null, null, null, null, null, null, 1, "{}"))));

        mapStore.UpsertPlayerMark(new MapPlayerMark(
            77, largeOwnerUid, "active", 3000, null,
            JsonSerializer.Serialize(new { serverId = 77, ownerUid = largeOwnerUid, ownerName = "Updated", allianceName = "ABC", level = 31 })));
        MapPlayerMark? mark = mapStore.GetPlayerMark(77, largeOwnerUid);
        Check(mark?.OwnerUid == largeOwnerUid && mark.State == "active" && mark.PlayerJson.Contains(largeOwnerUid, StringComparison.Ordinal),
            "player mark is keyed by server/owner UID and preserves large UID text");

        mapStore.InsertScanRun(new MapScanRunSeed(
            "run-77", 77, "[\"city\"]", "running", 100, 10, 0, 1000, 2000, null));
        Check(mapStore.CountScanRuns(77) == 1, "recovered scan run is stored before scoped clear");

        MapClearResult cleared = mapStore.ClearServer(77);
        Check(cleared.DeletedRuns == 1 && cleared.DeletedRecords == 2,
            "map clear deletes scan runs and all map records for the selected server");
        Check(mapStore.CountRecords("city", 77) == 0 && mapStore.CountRecords("monster", 77) == 0 &&
              mapStore.CountRecords("city", 78) == 1,
            "map clear is server-scoped and leaves other server records intact");
        Check(mapStore.GetPlayerMark(77, largeOwnerUid) is not null,
            "recovered clear semantics preserve player marks outside map_records/scan_runs");
    }

    using (var reopenedMapStore = new MapDataStore(mapDatabasePath))
    {
        MapPlayerMark? persistedMark = reopenedMapStore.GetPlayerMark(77, largeOwnerUid);
        Check(persistedMark?.OwnerUid == largeOwnerUid && persistedMark.State == "active",
            "player mark identity/state survives database restart after map-data clear");
        Check(reopenedMapStore.CountRecords("city", 78) == 1,
            "unrelated server map records survive database restart");
        Check(reopenedMapStore.DeletePlayerMark(77, largeOwnerUid) && reopenedMapStore.GetPlayerMark(77, largeOwnerUid) is null,
            "player mark delete uses recovered server/owner UID identity");
    }
}
finally
{
    try { Directory.Delete(mapStoreRoot, recursive: true); }
    catch { }
}

using (var backendMapStore = MapDataStore.CreateInMemory())
{
    var mapBackend = new LWBridgeBackend(new LocalConfigStore(persistent: false), mapData: backendMapStore);
    backendMapStore.UpsertRecord(new MapStoredRecord(
        "city", 91, "backend-city-key", 7, "backend-city-uuid", "Backend City", "XYZ",
        28, null, 1234567890123456789L, null, null, 1000,
        "{\"serverId\":91,\"ownerUid\":\"12345678901234567890\",\"ownerName\":\"Backend City\"}"));
    backendMapStore.InsertScanRun(new MapScanRunSeed(
        "backend-run", 91, "[\"city\"]", "running", 100, 0, 0, 1000, 1000, null));

    using JsonDocument markPayload = JsonDocument.Parse(JsonSerializer.Serialize(new
    {
        profileId = mapBackend.ProfileId,
        row = new
        {
            serverId = 91,
            ownerUid = "12345678901234567890",
            ownerName = "Backend City",
            allianceName = "XYZ",
            level = 28,
        },
        marked = true,
    }));
    object? markResult = await mapBackend.InvokeAsync("map_player_mark_set", markPayload.RootElement.Clone(), CancellationToken.None);
    Check(backendMapStore.GetPlayerMark(91, "12345678901234567890")?.State == "active" &&
          JsonSerializer.Serialize(markResult, JsonOptions.Default).Contains("\"marked\":true", StringComparison.Ordinal),
        "backend map_player_mark_set persists recovered server/owner identity and returns marked state");

    using JsonDocument clearPayload = JsonDocument.Parse(JsonSerializer.Serialize(new
    {
        profileId = mapBackend.ProfileId,
        serverId = 91,
    }));
    object? clearResult = await mapBackend.InvokeAsync("map_scan_clear", clearPayload.RootElement.Clone(), CancellationToken.None);
    using (JsonDocument clearJson = JsonDocument.Parse(JsonSerializer.Serialize(clearResult, JsonOptions.Default)))
    {
        Check(clearJson.RootElement.GetProperty("serverId").GetInt32() == 91 &&
              clearJson.RootElement.GetProperty("phase").GetString() == "idle" &&
              clearJson.RootElement.GetProperty("lastError").ValueKind == JsonValueKind.Null,
            "backend map_scan_clear returns the selected server in an idle post-clear scan status");
    }
    Check(backendMapStore.CountRecords("city", 91) == 0 && backendMapStore.CountScanRuns(91) == 0 &&
          backendMapStore.GetPlayerMark(91, "12345678901234567890") is not null,
        "backend map_scan_clear removes recovered map/scan scope while preserving player marks");

    using JsonDocument unmarkPayload = JsonDocument.Parse(JsonSerializer.Serialize(new
    {
        profileId = mapBackend.ProfileId,
        row = new { serverId = 91, ownerUid = "12345678901234567890" },
        marked = false,
    }));
    await mapBackend.InvokeAsync("map_player_mark_set", unmarkPayload.RootElement.Clone(), CancellationToken.None);
    Check(backendMapStore.GetPlayerMark(91, "12345678901234567890") is null,
        "backend unmark deletes the recovered server/owner player-mark identity");

    using JsonDocument invalidMarkPayload = JsonDocument.Parse(JsonSerializer.Serialize(new
    {
        profileId = mapBackend.ProfileId,
        row = new { serverId = 91 },
        marked = true,
    }));
    await ExpectBridgeError("INVALID_PAYLOAD", "backend mark rejects rows without ownerUid", async () =>
        await mapBackend.InvokeAsync("map_player_mark_set", invalidMarkPayload.RootElement.Clone(), CancellationToken.None));
}

// Recovered Map Scan input contract.
using JsonDocument normalScan = JsonDocument.Parse("{}");
MapScanStartOptions normalOptions = MapScanContract.NormalizeStart(normalScan.RootElement);
Check(normalOptions.ScanMode == "normal" && normalOptions.Concurrency == 8, "normal scan concurrency is recovered as 8");
Check(normalOptions.SelectedTypes.SequenceEqual(MapScanContract.AllTypes), "missing selectedTypes defaults to all eight recovered kinds");

using JsonDocument fastScan = JsonDocument.Parse(
    "{\"scanMode\":\"fast\",\"selectedTypes\":[\"truck\",\"bogus\",\"city\",\"truck\",7,\"treasure\"]}");
MapScanStartOptions fastOptions = MapScanContract.NormalizeStart(fastScan.RootElement);
Check(fastOptions.ScanMode == "fast" && fastOptions.Concurrency == 20, "fast scan concurrency is recovered as 20");
Check(fastOptions.SelectedTypes.SequenceEqual(new[] { "truck", "city", "treasure" }), "scan types filter unknown/non-string values and deduplicate in first-seen order");

using JsonDocument invalidTypes = JsonDocument.Parse("{\"selectedTypes\":[\"unknown\",5]}");
try
{
    MapScanContract.NormalizeStart(invalidTypes.RootElement);
    failures.Add("empty normalized scan type list is rejected");
}
catch (BridgeCommandException error)
{
    Check(error.Code == "INVALID_SCAN_TYPES", "empty normalized scan type list is rejected");
}

using JsonDocument invalidMode = JsonDocument.Parse("{\"scanMode\":\"turbo\"}");
try
{
    MapScanContract.NormalizeStart(invalidMode.RootElement);
    failures.Add("unknown scan mode is rejected");
}
catch (BridgeCommandException error)
{
    Check(error.Code == "INVALID_SCAN_MODE", "unknown scan mode is rejected");
}

// Recovered Map Data query envelope: eight kinds, page size 50 and ordered asc/desc sorts.
using JsonDocument mapQuery = JsonDocument.Parse("{\"kind\":\"city\",\"query\":{\"serverId\":7,\"page\":2,\"pageSize\":50,\"sorts\":[{\"sortBy\":\"level\",\"sortOrder\":\"asc\"},{\"sortBy\":\"updatedAt\",\"sortOrder\":\"desc\"}]}}");
MapDataQueryOptions mapOptions = MapDataQueryContract.NormalizeSearch(mapQuery.RootElement);
Check(mapOptions.Kind == "city" && mapOptions.ServerId == 7 && mapOptions.Page == 2 && mapOptions.PageSize == 50,
    "map query normalizes recovered kind/server/page contract");
Check(mapOptions.Sorts.SequenceEqual(new[] { new MapDataSort("level", "asc"), new MapDataSort("updatedAt", "desc") }),
    "map query preserves ordered recovered sort contract");
Check(mapOptions.UnsupportedFeatures.Contains("sorts", StringComparer.Ordinal),
    "non-updatedAt sort remains fail-closed until its original SQL expression is recovered");

using JsonDocument minimalMapQuery = JsonDocument.Parse("{\"kind\":\"monster\",\"query\":{\"serverId\":1}}");
MapDataQueryOptions minimalMapOptions = MapDataQueryContract.NormalizeSearch(minimalMapQuery.RootElement);
Check(minimalMapOptions.Page == 1 && minimalMapOptions.PageSize == MapDataQueryContract.RecoveredPageSize &&
      minimalMapOptions.Sorts.SequenceEqual(new[] { new MapDataSort("updatedAt", "desc") }),
    "map query defaults to page 1, recovered page size 50 and updatedAt desc");
Check(!minimalMapOptions.MarkedOnly && minimalMapOptions.UnsupportedFeatures.Count == 0,
    "default indexed map query contains no unrecovered filter/sort features");

using JsonDocument badMapKind = JsonDocument.Parse("{\"kind\":\"bogus\",\"query\":{\"serverId\":1}}");
await ExpectBridgeError("INVALID_MAP_KIND", "unknown map result kind is rejected", () =>
    Task.Run(() => { MapDataQueryContract.NormalizeSearch(badMapKind.RootElement); }));

using JsonDocument badMapSort = JsonDocument.Parse("{\"kind\":\"city\",\"query\":{\"serverId\":1,\"sorts\":[{\"sortBy\":\"level\",\"sortOrder\":\"sideways\"}]}}");
await ExpectBridgeError("INVALID_MAP_QUERY", "invalid map sort order is rejected", () =>
    Task.Run(() => { MapDataQueryContract.NormalizeSearch(badMapSort.RootElement); }));

// LWB-R6-004: representative serialized envelopes produced by each real Map Data tab.
var frontendMapQueryCases = new (string Name, string Json, string[] Unsupported)[]
{
    ("city", "{\"kind\":\"city\",\"query\":{\"serverId\":7,\"keyword\":\"\",\"alliance\":\"ONE\",\"markedOnly\":true,\"page\":1,\"pageSize\":50,\"sorts\":[{\"sortBy\":\"updatedAt\",\"sortOrder\":\"desc\"}]}}", ["alliance"]),
    ("resource", "{\"kind\":\"resource\",\"query\":{\"serverId\":7,\"keyword\":\"\",\"resourceNameKey\":\"iron\",\"page\":1,\"pageSize\":50,\"sorts\":[{\"sortBy\":\"updatedAt\",\"sortOrder\":\"desc\"}]}}", ["resourceNameKey"]),
    ("monster", "{\"kind\":\"monster\",\"query\":{\"serverId\":7,\"keyword\":\"\",\"monsterNameKey\":\"doom\",\"page\":1,\"pageSize\":50,\"sorts\":[{\"sortBy\":\"updatedAt\",\"sortOrder\":\"desc\"}]}}", ["monsterNameKey"]),
    ("truck", "{\"kind\":\"truck\",\"query\":{\"serverId\":7,\"keyword\":\"\",\"quality\":\"ur\",\"itemKey\":\"item:1\",\"plunderableOnly\":true,\"page\":1,\"pageSize\":50,\"sorts\":[{\"sortBy\":\"updatedAt\",\"sortOrder\":\"desc\"}]}}", ["quality", "itemKey", "plunderableOnly"]),
    ("railway", "{\"kind\":\"railway\",\"query\":{\"serverId\":7,\"keyword\":\"\",\"quality\":\"ssr\",\"itemKey\":\"item:2\",\"plunderableOnly\":true,\"page\":1,\"pageSize\":50,\"sorts\":[{\"sortBy\":\"updatedAt\",\"sortOrder\":\"desc\"}]}}", ["quality", "itemKey", "plunderableOnly"]),
    ("dispatch", "{\"kind\":\"dispatch\",\"query\":{\"serverId\":7,\"keyword\":\"\",\"specialOnly\":true,\"completionStatus\":\"pending\",\"plunderableOnly\":true,\"minLevel\":5,\"maxLevel\":5,\"page\":1,\"pageSize\":50,\"sorts\":[{\"sortBy\":\"updatedAt\",\"sortOrder\":\"desc\"}]}}", ["specialOnly", "completionStatus", "plunderableOnly", "minLevel", "maxLevel"]),
    ("ghost", "{\"kind\":\"ghost\",\"query\":{\"serverId\":7,\"keyword\":\"\",\"quality\":\"ssr\",\"completionStatus\":\"completed\",\"page\":1,\"pageSize\":50,\"sorts\":[{\"sortBy\":\"updatedAt\",\"sortOrder\":\"desc\"}]}}", ["quality", "completionStatus"]),
    ("treasure", "{\"kind\":\"treasure\",\"query\":{\"serverId\":7,\"keyword\":\"\",\"treasureType\":1,\"includeForeignRadarTreasures\":false,\"luckyFirst\":true,\"viewerUid\":\"10001\",\"viewerAllianceId\":\"20002\",\"page\":1,\"pageSize\":50,\"sorts\":[{\"sortBy\":\"updatedAt\",\"sortOrder\":\"desc\"}]}}", ["treasureType", "includeForeignRadarTreasures", "luckyFirst", "viewerUid", "viewerAllianceId"]),
};
foreach ((string name, string json, string[] unsupported) in frontendMapQueryCases)
{
    using JsonDocument frontendQuery = JsonDocument.Parse(json);
    MapDataQueryOptions normalized = MapDataQueryContract.NormalizeSearch(frontendQuery.RootElement);
    Check(normalized.Kind == name && normalized.UnsupportedFeatures.SequenceEqual(unsupported),
        $"real frontend {name} query envelope preserves recovered fields and fail-closed unsupported set");
}

using JsonDocument zeroMapFilter = JsonDocument.Parse(
    "{\"kind\":\"dispatch\",\"query\":{\"serverId\":7,\"minLevel\":0,\"maxLevel\":0}}");
MapDataQueryOptions zeroMapOptions = MapDataQueryContract.NormalizeSearch(zeroMapFilter.RootElement);
Check(zeroMapOptions.UnsupportedFeatures.SequenceEqual(new[] { "minLevel", "maxLevel" }),
    "explicit numeric zero is not collapsed into an omitted/empty Map Data filter");

using (var indexedSearchStore = MapDataStore.CreateInMemory())
{
    var indexedSearchBackend = new LWBridgeBackend(new LocalConfigStore(persistent: false), mapData: indexedSearchStore);
    indexedSearchStore.UpsertRecord(new MapStoredRecord(
        "city", 7, "city-a", 11, "uuid-a", "Alpha", "ONE",
        30, null, null, null, null, 3000,
        "{\"serverId\":7,\"ownerUid\":\"10000000000000000001\",\"ownerName\":\"Alpha\",\"updatedAt\":3000}"));
    indexedSearchStore.UpsertRecord(new MapStoredRecord(
        "city", 7, "city-b", 12, "uuid-b", "Bravo", "TWO",
        29, null, null, null, null, 3000,
        "{\"serverId\":7,\"ownerUid\":\"10000000000000000002\",\"ownerName\":\"Bravo\",\"updatedAt\":3000}"));
    indexedSearchStore.UpsertRecord(new MapStoredRecord(
        "city", 7, "city-c", 13, "uuid-c", "Charlie", null,
        28, null, null, null, null, 1000,
        "{\"serverId\":7,\"ownerUid\":\"10000000000000000003\",\"ownerName\":\"Charlie\",\"updatedAt\":1000}"));
    indexedSearchStore.UpsertPlayerMark(new MapPlayerMark(
        7, "10000000000000000001", "active", 4000, null,
        "{\"serverId\":7,\"ownerUid\":\"10000000000000000001\",\"ownerName\":\"Alpha\"}"));

    using JsonDocument firstPageSearch = JsonDocument.Parse(JsonSerializer.Serialize(new
    {
        profileId = indexedSearchBackend.ProfileId,
        kind = "city",
        query = new
        {
            serverId = 7,
            keyword = "",
            page = 1,
            pageSize = 2,
            sorts = new[] { new { sortBy = "updatedAt", sortOrder = "desc" } },
        },
    }));
    object? firstPageResult = await indexedSearchBackend.InvokeAsync(
        "map_search", firstPageSearch.RootElement.Clone(), CancellationToken.None);
    using (JsonDocument resultJson = JsonDocument.Parse(JsonSerializer.Serialize(firstPageResult, JsonOptions.Default)))
    {
        JsonElement rows = resultJson.RootElement.GetProperty("rows");
        Check(resultJson.RootElement.GetProperty("total").GetInt32() == 3 && rows.GetArrayLength() == 2,
            "persisted default map_search returns recovered rows/total pagination envelope");
        Check(rows[0].GetProperty("ownerName").GetString() == "Alpha" &&
              rows[1].GetProperty("ownerName").GetString() == "Bravo",
            "default map_search orders by updatedAt desc with record_key asc tie-breaker");
        Check(rows[0].GetProperty("marked").GetBoolean() && !rows[1].GetProperty("marked").GetBoolean(),
            "city map_search joins persisted player marks into visible marked state");
    }

    using JsonDocument secondPageSearch = JsonDocument.Parse(JsonSerializer.Serialize(new
    {
        profileId = indexedSearchBackend.ProfileId,
        kind = "city",
        query = new { serverId = 7, page = 2, pageSize = 2 },
    }));
    object? secondPageResult = await indexedSearchBackend.InvokeAsync(
        "map_search", secondPageSearch.RootElement.Clone(), CancellationToken.None);
    using (JsonDocument resultJson = JsonDocument.Parse(JsonSerializer.Serialize(secondPageResult, JsonOptions.Default)))
    {
        JsonElement rows = resultJson.RootElement.GetProperty("rows");
        Check(rows.GetArrayLength() == 1 && rows[0].GetProperty("ownerName").GetString() == "Charlie",
            "persisted map_search uses recovered LIMIT/OFFSET page semantics");
    }

    using JsonDocument ascendingSearch = JsonDocument.Parse(JsonSerializer.Serialize(new
    {
        profileId = indexedSearchBackend.ProfileId,
        kind = "city",
        query = new
        {
            serverId = 7,
            page = 1,
            pageSize = 3,
            sorts = new[] { new { sortBy = "updatedAt", sortOrder = "asc" } },
        },
    }));
    object? ascendingResult = await indexedSearchBackend.InvokeAsync(
        "map_search", ascendingSearch.RootElement.Clone(), CancellationToken.None);
    using (JsonDocument resultJson = JsonDocument.Parse(JsonSerializer.Serialize(ascendingResult, JsonOptions.Default)))
    {
        JsonElement rows = resultJson.RootElement.GetProperty("rows");
        Check(rows.GetArrayLength() == 3 &&
              rows[0].GetProperty("ownerName").GetString() == "Charlie" &&
              rows[1].GetProperty("ownerName").GetString() == "Alpha" &&
              rows[2].GetProperty("ownerName").GetString() == "Bravo",
            "persisted map_search supports recovered updatedAt asc with record_key asc tie-breaker");
    }

    using JsonDocument markedOnlySearch = JsonDocument.Parse(JsonSerializer.Serialize(new
    {
        profileId = indexedSearchBackend.ProfileId,
        kind = "city",
        query = new { serverId = 7, markedOnly = true },
    }));
    object? markedOnlyResult = await indexedSearchBackend.InvokeAsync(
        "map_search", markedOnlySearch.RootElement.Clone(), CancellationToken.None);
    using (JsonDocument resultJson = JsonDocument.Parse(JsonSerializer.Serialize(markedOnlyResult, JsonOptions.Default)))
    {
        JsonElement rows = resultJson.RootElement.GetProperty("rows");
        Check(resultJson.RootElement.GetProperty("total").GetInt32() == 1 &&
              rows.GetArrayLength() == 1 && rows[0].GetProperty("marked").GetBoolean(),
            "city markedOnly search uses recovered server/owner mark join");
    }

    using JsonDocument unrecoveredKeywordSearch = JsonDocument.Parse(JsonSerializer.Serialize(new
    {
        profileId = indexedSearchBackend.ProfileId,
        kind = "city",
        query = new { serverId = 7, keyword = "Alpha" },
    }));
    await ExpectBridgeError("MAP_QUERY_UNRECOVERED", "nonempty keyword stays fail-closed until exact predicate is recovered", async () =>
        await indexedSearchBackend.InvokeAsync("map_search", unrecoveredKeywordSearch.RootElement.Clone(), CancellationToken.None));

    using JsonDocument unrecoveredFalseFilter = JsonDocument.Parse(JsonSerializer.Serialize(new
    {
        profileId = indexedSearchBackend.ProfileId,
        kind = "treasure",
        query = new { serverId = 7, includeForeignRadarTreasures = false },
    }));
    await ExpectBridgeError("MAP_QUERY_UNRECOVERED", "explicit false treasure filter stays fail-closed until exact predicate is recovered", async () =>
        await indexedSearchBackend.InvokeAsync("map_search", unrecoveredFalseFilter.RootElement.Clone(), CancellationToken.None));

    using JsonDocument unrecoveredLevelSort = JsonDocument.Parse(JsonSerializer.Serialize(new
    {
        profileId = indexedSearchBackend.ProfileId,
        kind = "city",
        query = new { serverId = 7, sorts = new[] { new { sortBy = "level", sortOrder = "asc" } } },
    }));
    await ExpectBridgeError("MAP_QUERY_UNRECOVERED", "alternate map sort stays fail-closed until exact SQL expression is recovered", async () =>
        await indexedSearchBackend.InvokeAsync("map_search", unrecoveredLevelSort.RootElement.Clone(), CancellationToken.None));
}

using JsonDocument unavailableSearch = JsonDocument.Parse(JsonSerializer.Serialize(new
{
    profileId = backend.ProfileId,
    kind = "city",
    query = new { serverId = 1, page = 1, pageSize = 50, sorts = new[] { new { sortBy = "updatedAt", sortOrder = "desc" } } },
}));
await ExpectBridgeError("MAP_INDEX_UNAVAILABLE", "valid recovered map search reaches explicit offline index gate", async () =>
    await backend.InvokeAsync("map_search", unavailableSearch.RootElement.Clone(), CancellationToken.None));

// Generated adapter checks pin the recovered original implicit-profile/event rules.
string generatedApi = File.ReadAllText(Path.Combine(repoRoot, "src", "LWBridge.Desktop", "WebUi", "assets", "api-ClPPi2JT.js"));
Check(generatedApi.Contains("n&&!(`profileId`in r)&&(r.profileId=n)", StringComparison.Ordinal),
    "generated API injects active profile when wrapper omits profileId");
Check(generatedApi.Contains("n.profileId!==T()", StringComparison.Ordinal),
    "generated API filters foreign profile event envelopes");
string localProviders = File.ReadAllText(Path.Combine(repoRoot, "src", "LWBridge.Desktop", "WebUi", "local-providers.js"));
Check(localProviders.Contains("autoLaunchCommitted.current", StringComparison.Ordinal) &&
      localProviders.Contains("setAutoLaunch(autoLaunchCommitted.current)", StringComparison.Ordinal) &&
      localProviders.Contains("profile-save-error", StringComparison.Ordinal),
    "auto-launch preference rolls back to confirmed storage and surfaces rejected saves");
string previewHost = File.ReadAllText(Path.Combine(repoRoot, "src", "LWBridge.Desktop", "WebUi", "preview-host.js"));
Check(previewHost.Contains("NATIVE_TRANSPORT_MISSING", StringComparison.Ordinal) &&
      previewHost.Contains("liveRequested ? await liveInvoke", StringComparison.Ordinal),
    "requested live mode fails visibly instead of falling through to fixtures when native transport is missing");

// Installed-game checks are diagnostics by default and become a gate only when requested.
bool requireInstalled = args.Contains("--require-installed", StringComparer.OrdinalIgnoreCase);
string detectedRoot = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "FunFly", "Last War-Survival Game");
var installation = new GameInstallationService(new LocalConfigStore(persistent: false));
GameRootStatus installed = installation.Validate(detectedRoot, "diagnostic");
if (requireInstalled)
    Check(installed.Valid, "installed Last War root validates when --require-installed is requested");
if (installed.Valid)
{
    Check(installed.Is64Bit == true, "installed game and xlua are AMD64 PE32+");
    Check(installed.GameMachine?.StartsWith("0x8664/", StringComparison.Ordinal) == true,
        "installed LastWar.exe machine type is AMD64");
    Check(installed.XluaMachine?.StartsWith("0x8664/", StringComparison.Ordinal) == true,
        "installed xlua.dll machine type is AMD64");
    Check(File.Exists(installed.LauncherPath), "official launcher exists");
    Check(File.Exists(installed.GamePath), "game executable exists");
    Check(File.Exists(installed.XluaPath), "original xlua exists");
}

GameRootStatus invalid = installation.Validate(
    Path.Combine(Path.GetTempPath(), "lwbridge-missing-root-" + Guid.NewGuid().ToString("N")),
    "self-check");
Check(!invalid.Valid && invalid.Error == "GAME_ROOT_REQUIRED_FILES_MISSING", "missing root fails closed");

GameProcessStatus process = installation.GetProcessStatus();
if (verifyRealConfigUnchanged)
{
    bool existsAfter = File.Exists(realConfigPath);
    Check(existsAfter == realConfigExistedBefore,
        "deterministic checks do not create or remove the real user config");
    if (realConfigExistedBefore && existsAfter && realConfigBefore is not null)
        Check(File.ReadAllBytes(realConfigPath).SequenceEqual(realConfigBefore),
            "deterministic checks leave real user config bytes unchanged");
}

var report = new
{
    ok = failures.Count == 0,
    deterministic = new
    {
        profileRouting = true,
        persistence = true,
        requestLifetime = true,
        mapPersistence = true,
        mapContract = true,
    },
    installedDiagnostic = new
    {
        required = requireInstalled,
        installed.Valid,
        installed.Source,
        installed.Path,
        installed.Error,
        installed.Is64Bit,
        installed.GameMachine,
        installed.XluaMachine,
    },
    process = new
    {
        process.GameRunning,
        process.LauncherRunning,
        process.GamePid,
        process.LauncherPid,
    },
    failures,
};

Console.WriteLine(JsonSerializer.Serialize(report, JsonOptions.Indented));
return failures.Count == 0 ? 0 : 1;

internal sealed class ControlledAsyncCommandService : INativeAsyncCommandService
{
    public const string Command = "diagnostic_delayed_operation";

    private readonly bool ignoreCancellationWhileWaiting;
    private readonly TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int activeOwners;
    private int commitCount;

    public ControlledAsyncCommandService(bool ignoreCancellationWhileWaiting)
    {
        this.ignoreCancellationWhileWaiting = ignoreCancellationWhileWaiting;
    }

    public Task Entered => entered.Task;
    public int ActiveOwners => Volatile.Read(ref activeOwners);
    public int CommitCount => Volatile.Read(ref commitCount);

    public bool CanHandle(string command) => string.Equals(command, Command, StringComparison.Ordinal);

    public void Release() => release.TrySetResult();

    public async Task<object?> InvokeAsync(string command, JsonElement payload, CancellationToken cancellationToken)
    {
        if (!CanHandle(command))
            throw new InvalidOperationException("Unexpected controlled async command: " + command);

        Interlocked.Increment(ref activeOwners);
        entered.TrySetResult();
        try
        {
            if (ignoreCancellationWhileWaiting)
                await release.Task.ConfigureAwait(false);
            else
                await release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            int committed = Interlocked.Increment(ref commitCount);
            return new { committed = true, commitCount = committed };
        }
        finally
        {
            Interlocked.Decrement(ref activeOwners);
        }
    }
}
