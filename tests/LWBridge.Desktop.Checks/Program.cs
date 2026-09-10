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

void ExpectInvalidData(string expectedMessageFragment, string name, Action action)
{
    try
    {
        action();
        failures.Add(name);
    }
    catch (InvalidDataException error)
    {
        Check(error.Message.Contains(expectedMessageFragment, StringComparison.OrdinalIgnoreCase),
            name + $" (expected message containing {expectedMessageFragment}, got {error.Message})");
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

string WriteFakeLiveHelper(string root, string name, int delayMilliseconds, int pointId)
{
    string helperPath = Path.Combine(root, name + ".py");
    string resultPath = Path.Combine(root, name + "-result.json");
    string resultLiteral = JsonSerializer.Serialize(resultPath);
    File.WriteAllText(helperPath, $$"""
        import argparse
        import json
        import time

        parser = argparse.ArgumentParser(add_help=False)
        parser.add_argument("--request-id", required=True)
        args, _ = parser.parse_known_args()
        time.sleep({{delayMilliseconds}} / 1000.0)
        result = {
            "schemaVersion": 1,
            "probeVersion": "lwbridge-live-resource-probe-1",
            "requestId": args.request_id,
            "state": "proven",
            "requestRoute": "WorldPointManager.StartViewRequest+UpdateViewRequest(true)",
            "source": "WorldPointManager._pointInfos",
            "capturedAt": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
            "acquisitionOrdinal": 1,
            "point_records": [{
                "kind": "resource_point",
                "serverId": 2212,
                "pointId": {{pointId}},
                "x": 481,
                "y": 32,
                "level": 3,
                "source": "WorldPointManager._pointInfos"
            }]
        }
        with open({{resultLiteral}}, "w", encoding="utf-8") as stream:
            json.dump(result, stream)
        print(json.dumps({
            "ok": True,
            "probeVersion": "lwbridge-live-resource-probe-1",
            "requestId": args.request_id,
            "resultPath": {{resultLiteral}}
        }))
        """);
    return helperPath;
}

string WriteCorrelatedLiveResult(
    string root,
    string name,
    string requestId,
    int serverId = 2212,
    string capturedAt = "2026-09-10T05:20:00Z",
    string source = "WorldPointManager._pointInfos",
    string pointSource = "WorldPointManager._pointInfos")
{
    string resultPath = Path.Combine(root, name + ".json");
    File.WriteAllText(resultPath, JsonSerializer.Serialize(new
    {
        schemaVersion = 1,
        probeVersion = "lwbridge-live-resource-probe-1",
        requestId,
        state = "proven",
        requestRoute = "WorldPointManager.StartViewRequest+UpdateViewRequest(true)",
        source,
        capturedAt,
        acquisitionOrdinal = 1,
        point_records = new[]
        {
            new
            {
                kind = "resource_point",
                serverId,
                pointId = 1009,
                x = 481,
                y = 32,
                level = 3,
                source = pointSource,
            },
        },
    }, JsonOptions.Default));
    return resultPath;
}

(bool IsReading, string Phase) ReadLiveStatus(LiveResourceProbeCommandService service)
{
    using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(service.CreateStatus(), JsonOptions.Default));
    JsonElement root = document.RootElement;
    return (
        root.GetProperty("isReading").GetBoolean(),
        root.GetProperty("phase").GetString() ?? string.Empty);
}

async Task<bool> WaitForLiveState(
    LiveResourceProbeCommandService service,
    Func<(bool IsReading, string Phase), bool> predicate,
    int timeoutMilliseconds = 4000)
{
    DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
    while (DateTime.UtcNow < deadline)
    {
        if (predicate(ReadLiveStatus(service))) return true;
        await Task.Delay(20);
    }
    return predicate(ReadLiveStatus(service));
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

Check(
    LWBridgeControlPipeContract.GetFullPathForSid("S-1-5-21-1-2-3-1001") ==
    @"\\.\pipe\lwbridge-control-v1-c169ebe52e9c0ba4",
    "recovered LWBridge control pipe name uses first 16 lowercase SHA-256 hex characters of UTF-8 user SID");
Check(
    LWBridgeControlPipeContract.GetCurrentUserFullPath().StartsWith(
        LWBridgeControlPipeContract.FullPathPrefix,
        StringComparison.Ordinal),
    "current-user LWBridge control pipe name uses recovered prefix");

// LWB-R5-007: protocol framing and proxy hello parsing are recovered from the
// secure proxy/original host. These checks do not construct hello.ack or a
// command request because those host-side contracts remain gated.
byte[] helloPayload = System.Text.Encoding.UTF8.GetBytes(
    "{\"version\":1,\"type\":\"hello\",\"profileId\":\"profile-a\",\"instanceId\":\"instance-b\",\"requestId\":\"\",\"timestamp\":123456789,\"payload\":{\"token\":\"token-c\",\"pid\":4321,\"buildId\":\"build-d\"}}");
byte[] helloFrame = LWBridgeControlPipeProtocol.EncodeFrame(helloPayload);
Check(
    System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(helloFrame) == helloPayload.Length,
    "recovered control-pipe frame uses a four-byte little-endian payload length");
Check(
    LWBridgeControlPipeProtocol.TryDecodeFrame(helloFrame, out byte[] decodedHelloPayload, out int helloBytesConsumed) &&
    helloBytesConsumed == helloFrame.Length && decodedHelloPayload.SequenceEqual(helloPayload),
    "recovered control-pipe frame round-trips one complete payload");
Check(
    !LWBridgeControlPipeProtocol.TryDecodeFrame(helloFrame.AsSpan(0, helloFrame.Length - 1), out _, out _),
    "incomplete recovered control-pipe frame remains pending");
bool rejectedZeroLengthFrame = false;
try
{
    LWBridgeControlPipeProtocol.TryDecodeFrame([0, 0, 0, 0], out _, out _);
}
catch (InvalidDataException)
{
    rejectedZeroLengthFrame = true;
}
Check(rejectedZeroLengthFrame, "zero-length recovered control-pipe frame fails closed");

LWBridgeProxyHello hello = LWBridgeControlPipeProtocol.ParseProxyHello(decodedHelloPayload);
Check(
    hello.Version == 1 && hello.Type == "hello" && hello.ProfileId == "profile-a" &&
    hello.InstanceId == "instance-b" && hello.RequestId.Length == 0 &&
    hello.Timestamp.ValueKind == JsonValueKind.Number && hello.Timestamp.GetRawText() == "123456789" &&
    hello.Token == "token-c" && hello.Pid.ValueKind == JsonValueKind.Number && hello.Pid.GetRawText() == "4321" &&
    hello.BuildId == "build-d",
    "recovered proxy hello schema preserves version, identity, timestamp and nested token/pid/build fields");
Check(
    LWBridgeControlPipeProtocol.MatchesExpectedIdentity(hello, "profile-a", "instance-b", "token-c", "build-d"),
    "recovered proxy hello identity fields match the expected profile/instance/token/build tuple");
Check(
    !LWBridgeControlPipeProtocol.MatchesExpectedIdentity(hello, "profile-a", "foreign-instance", "token-c", "build-d"),
    "identity comparison policy rejects a foreign proxy hello tuple");

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

await ExpectBridgeError("MAP_INDEX_UNAVAILABLE", "map summary remains fail-closed until native summary state is recovered", async () =>
    await backend.InvokeAsync("map_summary", profilePayload.RootElement.Clone(), CancellationToken.None));

// PM10-03: saved-capture replay is a bounded importer with its own in-memory
// store. Invalid or out-of-slice resource identities fail closed instead of
// silently selecting a later record, and normal production gates stay closed.
string firstLiveReplayRoot = Path.Combine(Path.GetTempPath(), "lwbridge-first-live-replay-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(firstLiveReplayRoot);
try
{
    string validReplayPath = Path.Combine(firstLiveReplayRoot, "valid.json");
    File.WriteAllText(validReplayPath, """
        {
          "sourceCaptureSha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
          "capturedAt": "2026-09-09T19:02:27Z",
          "probeVersion": "lwcontrol-world-full-scan-probe-9",
          "point_records": [
            {
              "kind": "resource_point",
              "serverId": 2212,
              "pointId": 1006,
              "x": 5,
              "y": 1,
              "source": "WorldPointManager._pointInfos"
            }
          ]
        }
        """);

    FirstLiveReplay replay = FirstLiveResultImporter.CreateIsolatedReplay(validReplayPath);
    using (replay.Store)
    {
        Check(replay.Import.ServerId == 2212 && replay.Import.PointIndex == 1006 &&
              replay.Import.X == 5 && replay.Import.Y == 1,
            "first-live replay imports the source-backed resource identity and coordinates");
        Check(replay.Import.Level is null && replay.Import.ProbeVersion == "lwcontrol-world-full-scan-probe-9" &&
              replay.Import.DeclaredSourceCaptureSha256 == "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            "first-live replay preserves capture provenance while optional unsupported fields remain unknown");
        using (JsonDocument replayData = JsonDocument.Parse(replay.Import.DataJson))
        {
            Check(replayData.RootElement.GetProperty("kind").GetString() == "resource" &&
                  replayData.RootElement.GetProperty("sourceKind").GetString() == "resource_point" &&
                  !replayData.RootElement.TryGetProperty("resourceNameKey", out _) &&
                  !replayData.RootElement.TryGetProperty("level", out _),
                "first-live replay maps only the bounded public kind and does not fabricate optional resource fields");
        }
        Check(replay.Store.CountRecords("resource", 2212) == 1,
            "first-live replay stores exactly one imported resource in its isolated map index");

        var replayBackend = new LWBridgeBackend(
            new LocalConfigStore(persistent: false),
            mapData: replay.Store,
            firstLiveResultServerId: replay.Import.ServerId);
        using JsonDocument replayProfile = JsonDocument.Parse(JsonSerializer.Serialize(new { profileId = replayBackend.ProfileId }));
        object? replaySummary = await replayBackend.InvokeAsync("map_summary", replayProfile.RootElement.Clone(), CancellationToken.None);
        using (JsonDocument replaySummaryJson = JsonDocument.Parse(JsonSerializer.Serialize(replaySummary, JsonOptions.Default)))
        {
            JsonElement scanState = replaySummaryJson.RootElement.GetProperty("scanState");
            Check(scanState.GetProperty("phase").GetString() == "unavailable" &&
                  scanState.GetProperty("serverIdSource").GetString() == "saved_capture_replay" &&
                  scanState.GetProperty("isReading").ValueKind == JsonValueKind.False,
                "saved-capture summary explicitly reports replay data without an active fresh scan");
        }
        await ExpectBridgeError("BRIDGE_NOT_READY", "saved-capture replay cannot start a production map scan", async () =>
            await replayBackend.InvokeAsync("map_scan_start", JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                profileId = replayBackend.ProfileId,
                serverId = 2212,
                selectedTypes = new[] { "resource" },
            })).RootElement.Clone(), CancellationToken.None));
    }

    using (var productionMapStore = MapDataStore.CreateInMemory())
    {
        var productionMapBackend = new LWBridgeBackend(
            new LocalConfigStore(persistent: false),
            mapData: productionMapStore);
        Check(productionMapStore.CountRecords("resource", 2212) == 0,
            "saved replay import never writes into a separate production map index");
        // Synthetic stale-row fixture: even when the production index already contains
        // older data, a disconnected backend must not promote it to a fresh scan result.
        productionMapStore.UpsertRecord(new MapStoredRecord(
            "resource", 2212, "synthetic-stale-resource", 1005, null, null, null,
            3, null, null, null, null, 1,
            "{\"serverId\":2212,\"pointIndex\":1005,\"x\":4,\"y\":1,\"updatedAt\":1}"));
        Check(productionMapStore.CountRecords("resource", 2212) == 1,
            "synthetic stale production row exists before disconnected freshness-gate checks");
        using JsonDocument productionProfile = JsonDocument.Parse(JsonSerializer.Serialize(new { profileId = productionMapBackend.ProfileId }));
        await ExpectBridgeError("MAP_INDEX_UNAVAILABLE", "production map summary remains closed after a replay import", async () =>
            await productionMapBackend.InvokeAsync("map_summary", productionProfile.RootElement.Clone(), CancellationToken.None));
        await ExpectBridgeError("BRIDGE_NOT_READY", "disconnected production scan never presents a stale indexed row as fresh", async () =>
            await productionMapBackend.InvokeAsync("map_scan_start", JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                profileId = productionMapBackend.ProfileId,
                serverId = 2212,
                selectedTypes = new[] { "resource" },
            })).RootElement.Clone(), CancellationToken.None));
        await ExpectBridgeError("OVERVIEW_LAUNCH_BOOTSTRAP_UNRECOVERED", "production launch gate remains closed after a replay import", async () =>
            await productionMapBackend.InvokeAsync("profile_instance_start", productionProfile.RootElement.Clone(), CancellationToken.None));

        var boundedLiveService = new LiveResourceProbeCommandService(
            productionMapStore,
            Path.Combine(firstLiveReplayRoot, "helper-must-not-run.py"));
        using (JsonDocument boundedStatus = JsonDocument.Parse(JsonSerializer.Serialize(boundedLiveService.CreateStatus(), JsonOptions.Default)))
        {
            JsonElement root = boundedStatus.RootElement;
            Check(root.GetProperty("serverId").ValueKind == JsonValueKind.Null &&
                  root.GetProperty("liveResourceAcquisitionOrdinal").ValueKind == JsonValueKind.Null &&
                  root.GetProperty("totalBlocks").ValueKind == JsonValueKind.Null &&
                  root.GetProperty("progressPercent").ValueKind == JsonValueKind.Null &&
                  root.GetProperty("nativeCaptureReady").ValueKind == JsonValueKind.Null,
                "bounded live status preserves unavailable identity and unmeasured full-scan metrics as unknown");
        }
        var boundedLiveBackend = new LWBridgeBackend(
            new LocalConfigStore(persistent: false),
            asyncCommands: boundedLiveService,
            mapData: productionMapStore);
        using JsonDocument boundedProfile = JsonDocument.Parse(JsonSerializer.Serialize(new { profileId = boundedLiveBackend.ProfileId }));
        object? boundedStatusEnvelope = await boundedLiveBackend.InvokeAsync("get_status", boundedProfile.RootElement.Clone(), CancellationToken.None);
        using (JsonDocument boundedStatusJson = JsonDocument.Parse(JsonSerializer.Serialize(boundedStatusEnvelope, JsonOptions.Default)))
        {
            Check(boundedStatusJson.RootElement.GetProperty("xluaOnline").ValueKind == JsonValueKind.False,
                "bounded direct resource probe never promotes its heartbeat to original bridge online state");
        }
        await ExpectBridgeError("LIVE_RESOURCE_TYPES_UNSUPPORTED", "bounded live route rejects unsupported scan kinds before helper launch", async () =>
            await boundedLiveBackend.InvokeAsync("map_scan_start", JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                profileId = boundedLiveBackend.ProfileId,
                selectedTypes = new[] { "city" },
                scanMode = "normal",
            })).RootElement.Clone(), CancellationToken.None));

        string foreignLiveResultPath = Path.Combine(firstLiveReplayRoot, "foreign-live-resource-result.json");
        File.WriteAllText(foreignLiveResultPath, """
            {
              "schemaVersion": 1,
              "probeVersion": "lwbridge-live-resource-probe-1",
              "requestId": "foreign-request",
              "state": "proven",
              "requestRoute": "WorldPointManager.StartViewRequest+UpdateViewRequest(true)",
              "source": "WorldPointManager._pointInfos",
              "capturedAt": "2026-09-10T05:00:00Z",
              "point_records": [
                {
                  "kind": "resource_point",
                  "serverId": 2212,
                  "pointId": 9999,
                  "x": 10,
                  "y": 20,
                  "source": "WorldPointManager._pointInfos"
                }
              ]
            }
            """);
        int recordsBeforeForeignResult = productionMapStore.CountRecords("resource", 2212);
        ExpectInvalidData("correlation contract", "bounded live route rejects a foreign result before persistence", () =>
            LiveResourceProbeCommandService.ImportCorrelatedResult(
                productionMapStore, foreignLiveResultPath, "expected-request", out _));
        Check(productionMapStore.CountRecords("resource", 2212) == recordsBeforeForeignResult,
            "foreign live result cannot mutate the normal map index before correlation succeeds");

        string immutableResultPath = Path.Combine(firstLiveReplayRoot, "immutable-live-resource-result.json");
        File.WriteAllText(immutableResultPath, """
            {
              "schemaVersion": 1,
              "probeVersion": "lwbridge-live-resource-probe-1",
              "requestId": "immutable-request",
              "state": "proven",
              "requestRoute": "WorldPointManager.StartViewRequest+UpdateViewRequest(true)",
              "source": "WorldPointManager._pointInfos",
              "capturedAt": "2026-09-10T05:20:00Z",
              "acquisitionOrdinal": 7,
              "point_records": [
                {
                  "kind": "resource_point",
                  "serverId": 2212,
                  "pointId": 1008,
                  "x": 481,
                  "y": 32,
                  "level": 3,
                  "source": "WorldPointManager._pointInfos"
                }
              ]
            }
            """);
        int immutableReadCount = 0;
        FirstLiveResultImport immutableImport = LiveResourceProbeCommandService.ImportCorrelatedResult(
            productionMapStore,
            immutableResultPath,
            "immutable-request",
            out int? immutableOrdinal,
            path =>
            {
                immutableReadCount++;
                byte[] bytes = File.ReadAllBytes(path);
                File.WriteAllText(path, File.ReadAllText(foreignLiveResultPath));
                return bytes;
            });
        Check(immutableReadCount == 1 && immutableOrdinal == 7 && immutableImport.PointIndex == 1008,
            "correlated live import validates, hashes and persists one immutable byte snapshot even if the shared path is replaced after the read");
        Check(productionMapStore.CountRecords("resource", 2212) == recordsBeforeForeignResult + 1,
            "immutable correlated import persists only the row from the validated byte snapshot");

        int recordsBeforeScopeRejections = productionMapStore.CountRecords("resource", 2212);
        string wrongServerPath = WriteCorrelatedLiveResult(
            firstLiveReplayRoot, "wrong-server-live-result", "wrong-server-request", serverId: 2213);
        ExpectInvalidData("different server", "bounded live route rejects a result from a different established server", () =>
            LiveResourceProbeCommandService.ImportCorrelatedResult(
                productionMapStore,
                wrongServerPath,
                "wrong-server-request",
                out _,
                expectedServerId: 2212,
                operationStartedAtUtc: DateTimeOffset.Parse("2026-09-10T05:10:00Z"),
                nowUtc: DateTimeOffset.Parse("2026-09-10T05:30:00Z")));

        string staleLiveResultPath = WriteCorrelatedLiveResult(
            firstLiveReplayRoot,
            "stale-live-result",
            "stale-request",
            capturedAt: "2026-09-10T05:00:00Z");
        ExpectInvalidData("stale", "bounded live route rejects a result predating the active acquisition", () =>
            LiveResourceProbeCommandService.ImportCorrelatedResult(
                productionMapStore,
                staleLiveResultPath,
                "stale-request",
                out _,
                expectedServerId: 2212,
                operationStartedAtUtc: DateTimeOffset.Parse("2026-09-10T05:10:00Z"),
                nowUtc: DateTimeOffset.Parse("2026-09-10T05:30:00Z")));

        string wrongSourcePath = WriteCorrelatedLiveResult(
            firstLiveReplayRoot,
            "wrong-source-live-result",
            "wrong-source-request",
            source: "OtherSource");
        ExpectInvalidData("WorldPointManager._pointInfos source", "bounded live route rejects a mismatched top-level source", () =>
            LiveResourceProbeCommandService.ImportCorrelatedResult(
                productionMapStore, wrongSourcePath, "wrong-source-request", out _));

        string wrongPointSourcePath = WriteCorrelatedLiveResult(
            firstLiveReplayRoot,
            "wrong-point-source-live-result",
            "wrong-point-source-request",
            pointSource: "OtherPointSource");
        ExpectInvalidData("WorldPointManager._pointInfos source", "bounded live route rejects a mismatched point source", () =>
            LiveResourceProbeCommandService.ImportCorrelatedResult(
                productionMapStore, wrongPointSourcePath, "wrong-point-source-request", out _));
        Check(productionMapStore.CountRecords("resource", 2212) == recordsBeforeScopeRejections,
            "stale, foreign-server and mismatched-source results are rejected before persistence");
    }

    using JsonDocument lifecycleStartPayload = JsonDocument.Parse("""
        {"selectedTypes":["resource"],"scanMode":"normal"}
        """);
    using JsonDocument lifecycleStopPayload = JsonDocument.Parse("{}");

    using (var lifecycleStore = MapDataStore.CreateInMemory())
    {
        string helper = WriteFakeLiveHelper(firstLiveReplayRoot, "fake-live-cancel", 500, 2001);
        var service = new LiveResourceProbeCommandService(lifecycleStore, helper, TimeSpan.FromSeconds(3));
        Task<object?> firstStart = service.InvokeAsync(
            "map_scan_start", lifecycleStartPayload.RootElement.Clone(), CancellationToken.None);
        Check(await WaitForLiveState(service, state => state.IsReading && state.Phase == "reading"),
            "fake live helper enters reading state before lifecycle cancellation checks");
        await ExpectBridgeError("MAP_SCAN_ALREADY_RUNNING", "duplicate live Start rejects immediately instead of queueing", async () =>
            await service.InvokeAsync("map_scan_start", lifecycleStartPayload.RootElement.Clone(), CancellationToken.None));

        object? stopStatus = await service.InvokeAsync(
            "map_scan_stop", lifecycleStopPayload.RootElement.Clone(), CancellationToken.None);
        using (JsonDocument stopStatusJson = JsonDocument.Parse(JsonSerializer.Serialize(stopStatus, JsonOptions.Default)))
        {
            Check(stopStatusJson.RootElement.GetProperty("isReading").GetBoolean() &&
                  stopStatusJson.RootElement.GetProperty("phase").GetString() == "cancelling",
                "map_scan_stop targets the active acquisition and reports cancelling while helper cleanup continues");
        }
        bool firstCancelled = false;
        try { await firstStart; }
        catch (OperationCanceledException) { firstCancelled = true; }
        Check(firstCancelled && lifecycleStore.CountRecords("resource", 2212) == 0,
            "cancelled live acquisition cannot publish the helper's later successful result");
        Check(await WaitForLiveState(service, state => !state.IsReading && state.Phase == "idle"),
            "cancelled helper completion releases operation ownership back to idle");

        object? retryStatus = await service.InvokeAsync(
            "map_scan_start", lifecycleStartPayload.RootElement.Clone(), CancellationToken.None);
        using (JsonDocument retryStatusJson = JsonDocument.Parse(JsonSerializer.Serialize(retryStatus, JsonOptions.Default)))
        {
            Check(retryStatusJson.RootElement.GetProperty("isReading").ValueKind == JsonValueKind.False &&
                  retryStatusJson.RootElement.GetProperty("phase").GetString() == "idle" &&
                  retryStatusJson.RootElement.GetProperty("serverId").GetInt32() == 2212,
                "a new Start returns a truthful completed status after cancelled helper cleanup has completed");
        }
        Check(lifecycleStore.CountRecords("resource", 2212) == 1,
            "successful retry imports exactly one fake-helper resource row");
    }

    using (var timeoutStore = MapDataStore.CreateInMemory())
    {
        string helper = WriteFakeLiveHelper(firstLiveReplayRoot, "fake-live-timeout", 900, 2002);
        var service = new LiveResourceProbeCommandService(timeoutStore, helper, TimeSpan.FromMilliseconds(120));
        await ExpectBridgeError("LIVE_RESOURCE_ACQUISITION_FAILED", "hung fake helper is bounded by process supervision", async () =>
            await service.InvokeAsync("map_scan_start", lifecycleStartPayload.RootElement.Clone(), CancellationToken.None));
        (bool IsReading, string Phase) stuck = ReadLiveStatus(service);
        Check(stuck.IsReading && stuck.Phase == "helper_stuck",
            "timed-out helper retains operation ownership while its cleanup process is still running");
        await ExpectBridgeError("MAP_SCAN_ALREADY_RUNNING", "duplicate Start remains rejected while timed-out helper is still owned", async () =>
            await service.InvokeAsync("map_scan_start", lifecycleStartPayload.RootElement.Clone(), CancellationToken.None));
        Check(await WaitForLiveState(service, state => !state.IsReading && state.Phase == "error", 4000),
            "late fake-helper exit releases ownership without importing its result");
        Check(timeoutStore.CountRecords("resource", 2212) == 0,
            "supervised timeout never persists a late helper result");
    }

    using (var closeStore = MapDataStore.CreateInMemory())
    {
        string helper = WriteFakeLiveHelper(firstLiveReplayRoot, "fake-live-close", 500, 2003);
        var service = new LiveResourceProbeCommandService(closeStore, helper, TimeSpan.FromSeconds(3));
        Task<object?> start = service.InvokeAsync(
            "map_scan_start", lifecycleStartPayload.RootElement.Clone(), CancellationToken.None);
        Check(await WaitForLiveState(service, state => state.IsReading),
            "fake helper is active before application-close lifecycle check");
        service.Close();
        bool closeCancelled = false;
        try { await start; }
        catch (OperationCanceledException) { closeCancelled = true; }
        Check(closeCancelled && closeStore.CountRecords("resource", 2212) == 0,
            "application close cancels publication and leaves the store untouched after helper completion");
        await ExpectBridgeError("MAP_SCAN_CLOSED", "closed service rejects any later Start", async () =>
            await service.InvokeAsync("map_scan_start", lifecycleStartPayload.RootElement.Clone(), CancellationToken.None));
    }

    string missingTimestampPath = Path.Combine(firstLiveReplayRoot, "missing-timestamp.json");
    File.WriteAllText(missingTimestampPath, "{\"point_records\":[]}");
    ExpectInvalidData("capturedAt", "first-live replay rejects a missing capture timestamp", () =>
        FirstLiveResultImporter.CreateIsolatedReplay(missingTimestampPath));

    string invalidTimestampPath = Path.Combine(firstLiveReplayRoot, "invalid-timestamp.json");
    File.WriteAllText(invalidTimestampPath, "{\"capturedAt\":\"not-a-time\",\"point_records\":[]}");
    ExpectInvalidData("capturedAt", "first-live replay rejects an invalid capture timestamp", () =>
        FirstLiveResultImporter.CreateIsolatedReplay(invalidTimestampPath));

    string missingRecordsPath = Path.Combine(firstLiveReplayRoot, "missing-records.json");
    File.WriteAllText(missingRecordsPath, "{\"capturedAt\":\"2026-09-09T19:02:27Z\"}");
    ExpectInvalidData("point_records", "first-live replay rejects missing point records", () =>
        FirstLiveResultImporter.CreateIsolatedReplay(missingRecordsPath));

    string invalidFirstResourcePath = Path.Combine(firstLiveReplayRoot, "invalid-first-resource.json");
    File.WriteAllText(invalidFirstResourcePath, """
        {
          "capturedAt": "2026-09-09T19:02:27Z",
          "point_records": [
            {"kind":"resource_point","serverId":2212,"pointId":1006,"x":0,"y":1},
            {"kind":"resource_point","serverId":2212,"pointId":1007,"x":6,"y":2}
          ]
        }
        """);
    ExpectInvalidData("x", "first-live replay fails on the first out-of-slice resource instead of silently choosing a later record", () =>
        FirstLiveResultImporter.CreateIsolatedReplay(invalidFirstResourcePath));

    string overflowCoordinatePath = Path.Combine(firstLiveReplayRoot, "overflow-coordinate.json");
    File.WriteAllText(overflowCoordinatePath, """
        {
          "capturedAt": "2026-09-09T19:02:27Z",
          "point_records": [
            {"kind":"resource_point","serverId":2212,"pointId":1006,"x":2147483648,"y":1}
          ]
        }
        """);
    ExpectInvalidData("x", "first-live replay rejects coordinates outside the documented Int32 demo boundary", () =>
        FirstLiveResultImporter.CreateIsolatedReplay(overflowCoordinatePath));

    string invalidServerPath = Path.Combine(firstLiveReplayRoot, "invalid-server.json");
    File.WriteAllText(invalidServerPath, """
        {
          "capturedAt": "2026-09-09T19:02:27Z",
          "point_records": [
            {"kind":"resource_point","serverId":100000,"pointId":1006,"x":5,"y":1}
          ]
        }
        """);
    ExpectInvalidData("1 through 99999", "first-live replay enforces the recovered public Map Data server boundary before storage", () =>
        FirstLiveResultImporter.CreateIsolatedReplay(invalidServerPath));

    string maxBoundaryPath = Path.Combine(firstLiveReplayRoot, "max-boundary.json");
    File.WriteAllText(maxBoundaryPath, """
        {
          "capturedAt": "2026-09-09T19:02:27Z",
          "point_records": [
            {"kind":"resource_point","serverId":99999,"pointId":2147483647,"x":2147483647,"y":2147483647}
          ]
        }
        """);
    FirstLiveReplay maxBoundaryReplay = FirstLiveResultImporter.CreateIsolatedReplay(maxBoundaryPath);
    using (maxBoundaryReplay.Store)
    {
        Check(maxBoundaryReplay.Import.ServerId == 99999 && maxBoundaryReplay.Import.PointIndex == int.MaxValue &&
              maxBoundaryReplay.Import.X == int.MaxValue && maxBoundaryReplay.Import.Y == int.MaxValue,
            "first-live replay accepts the documented positive Int32 upper boundary");
    }
}
finally
{
    try { Directory.Delete(firstLiveReplayRoot, recursive: true); }
    catch { }
}

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

        // IMPLEMENTATION POLICY LWB-R6-008: a search count and page are one read snapshot.
        mapStore.UpsertRecord(new MapStoredRecord(
            "city", 79, "snapshot-a", 1, "snapshot-a", "Snapshot A", null,
            1, null, null, null, null, 1000, "{\"ownerUid\":\"snapshot-a\",\"ownerName\":\"Snapshot A\",\"updatedAt\":1000}"));
        mapStore.UpsertRecord(new MapStoredRecord(
            "city", 79, "snapshot-b", 2, "snapshot-b", "Snapshot B", null,
            1, null, null, null, null, 900, "{\"ownerUid\":\"snapshot-b\",\"ownerName\":\"Snapshot B\",\"updatedAt\":900}"));
        using (var concurrentWriter = new MapDataStore(mapDatabasePath))
        using (JsonDocument snapshotQuery = JsonDocument.Parse(
            "{\"kind\":\"city\",\"query\":{\"serverId\":79,\"page\":1,\"pageSize\":50}}"))
        {
            MapSearchResult snapshotResult = mapStore.SearchIndexedForSnapshotTest(
                MapDataQueryContract.NormalizeSearch(snapshotQuery.RootElement),
                () => concurrentWriter.UpsertRecord(new MapStoredRecord(
                    "city", 79, "snapshot-c", 3, "snapshot-c", "Snapshot C", null,
                    1, null, null, null, null, 1100, "{\"ownerUid\":\"snapshot-c\",\"ownerName\":\"Snapshot C\",\"updatedAt\":1100}")));
            Check(snapshotResult.Total == 2 && snapshotResult.Rows.Count == 2 &&
                  snapshotResult.Rows.All(row => row.GetProperty("ownerName").GetString() != "Snapshot C"),
                "map_search count/page stay on one SQLite snapshot across a concurrent WAL writer");
        }
        Check(mapStore.CountRecords("city", 79) == 3,
            "concurrent writer commits after the search snapshot without losing the new row");

        // PM7-A / LWB-R6-038 IMPLEMENTATION POLICY: all option/count families in
        // one source context observe one read snapshot while a WAL writer commits.
        mapStore.UpsertRecord(new MapStoredRecord(
            "city", 84, "option-snapshot-a", 1, "option-snapshot-a", "Option Snapshot A", "Alpha",
            1, null, null, null, null, 1000,
            "{\"ownerUid\":\"option-snapshot-a\",\"ownerName\":\"Option Snapshot A\"}"));
        MapOptionSourceSelection optionSnapshotSource =
            MapDataStore.SelectOptionSource(84, isReading: false, scanStateServerId: 84, scanRunId: null);
        using (var optionConcurrentWriter = new MapDataStore(mapDatabasePath))
        {
            MapOptionAggregates optionSnapshot = mapStore.ReadOptionAggregatesAtForSnapshotTest(
                optionSnapshotSource,
                nowUnixMilliseconds: 5_000,
                () => optionConcurrentWriter.UpsertRecord(new MapStoredRecord(
                    "city", 84, "option-snapshot-b", 2, "option-snapshot-b", "Option Snapshot B", "Beta",
                    1, null, null, null, null, 1100,
                    "{\"ownerUid\":\"option-snapshot-b\",\"ownerName\":\"Option Snapshot B\"}")));
            Check(optionSnapshot.Alliances.Count == 1 && optionSnapshot.Alliances[0].Name == "Alpha" &&
                  optionSnapshot.Counts["city"] == 1,
                "option alliances and counts stay on one SQLite snapshot across a concurrent WAL writer");
        }
        MapOptionAggregates optionAfterWriter =
            mapStore.ReadOptionAggregatesAt(optionSnapshotSource, nowUnixMilliseconds: 5_000);
        Check(optionAfterWriter.Alliances.Count == 2 && optionAfterWriter.Counts["city"] == 2,
            "option aggregate snapshot releases cleanly and exposes the committed concurrent row on the next read");

        // LWB-R6-019: exercise only the recovered completed-kind delete/copy transaction.
        // The helper remains test-only because the original scan-completeness eligibility
        // gate and native record-key derivation are separate UNKNOWN/BLOCKED contracts.
        mapStore.UpsertRecord(new MapStoredRecord(
            "city", 80, "publish-old-a", 1, "publish-old-a", "Old A", null,
            1, null, null, null, null, 100, "{\"ownerUid\":\"publish-old-a\",\"ownerName\":\"Old A\",\"updatedAt\":100}"));
        mapStore.UpsertRecord(new MapStoredRecord(
            "city", 80, "publish-old-b", 2, "publish-old-b", "Old B", null,
            1, null, null, null, null, 100, "{\"ownerUid\":\"publish-old-b\",\"ownerName\":\"Old B\",\"updatedAt\":100}"));
        mapStore.UpsertRecord(new MapStoredRecord(
            "monster", 80, "publish-monster", 3, null, "Keep Monster", null,
            2, null, null, null, null, 100, "{\"monsterNameKey\":\"keep\",\"updatedAt\":100}"));
        mapStore.UpsertRecord(new MapStoredRecord(
            "city", 81, "publish-other-server", 4, null, "Keep Other Server", null,
            1, null, null, null, null, 100, "{\"ownerUid\":\"other-server\",\"updatedAt\":100}"));
        mapStore.InsertScanRun(new MapScanRunSeed(
            "publish-run-80", 80, "[\"city\"]", "completed", 2, 2, 0, 100, 200, null));
        mapStore.InsertScanRun(new MapScanRunSeed(
            "publish-other-run-80", 80, "[\"city\"]", "completed", 1, 1, 0, 100, 200, null));
        mapStore.StageRecordForPublishTest("publish-run-80", new MapStoredRecord(
            "city", 80, "publish-new-a", 10, "publish-new-a", "New A", "NEW",
            10, null, null, null, null, 300, "{\"ownerUid\":\"publish-new-a\",\"ownerName\":\"New A\",\"updatedAt\":300}"));
        mapStore.StageRecordForPublishTest("publish-run-80", new MapStoredRecord(
            "city", 80, "publish-new-b", 11, "publish-new-b", "New B", "NEW",
            11, null, null, null, null, 301, "{\"ownerUid\":\"publish-new-b\",\"ownerName\":\"New B\",\"updatedAt\":301}"));
        mapStore.StageRecordForPublishTest("publish-other-run-80", new MapStoredRecord(
            "city", 80, "publish-wrong-run", 12, "publish-wrong-run", "Wrong Run", null,
            12, null, null, null, null, 302, "{\"ownerUid\":\"publish-wrong-run\",\"updatedAt\":302}"));

        int publishedRows = mapStore.ReplacePublishedKindFromStagingForTest("publish-run-80", "city", 80);
        Check(publishedRows == 2 && mapStore.CountRecords("city", 80) == 2 &&
              mapStore.GetRecord("city", 80, "publish-new-a")?.Name == "New A" &&
              mapStore.GetRecord("city", 80, "publish-new-b")?.Name == "New B" &&
              mapStore.GetRecord("city", 80, "publish-old-a") is null &&
              mapStore.GetRecord("city", 80, "publish-old-b") is null &&
              mapStore.GetRecord("city", 80, "publish-wrong-run") is null,
            "recovered publish slice atomically replaces one kind/server from only the selected scan run");
        Check(mapStore.CountRecords("monster", 80) == 1 && mapStore.CountRecords("city", 81) == 1,
            "recovered publish slice leaves other kinds and servers unchanged");

        mapStore.UpsertRecord(new MapStoredRecord(
            "city", 82, "rollback-old-a", 1, "rollback-old-a", "Rollback Old A", null,
            1, null, null, null, null, 100, "{\"ownerUid\":\"rollback-old-a\",\"ownerName\":\"Rollback Old A\",\"updatedAt\":100}"));
        mapStore.UpsertRecord(new MapStoredRecord(
            "city", 82, "rollback-old-b", 2, "rollback-old-b", "Rollback Old B", null,
            1, null, null, null, null, 101, "{\"ownerUid\":\"rollback-old-b\",\"ownerName\":\"Rollback Old B\",\"updatedAt\":101}"));
        mapStore.InsertScanRun(new MapScanRunSeed(
            "rollback-run-82", 82, "[\"city\"]", "completed", 1, 1, 0, 100, 200, null));
        mapStore.StageRecordForPublishTest("rollback-run-82", new MapStoredRecord(
            "city", 82, "rollback-new", 10, "rollback-new", "Rollback New", null,
            10, null, null, null, null, 300, "{\"ownerUid\":\"rollback-new\",\"ownerName\":\"Rollback New\",\"updatedAt\":300}"));

        try
        {
            mapStore.ReplacePublishedKindFromStagingForTest(
                "rollback-run-82", "city", 82,
                () => throw new InvalidOperationException("deterministic publish failure"));
            failures.Add("staged publication rollback preserves the prior published kind/server on mid-transaction failure");
        }
        catch (InvalidOperationException error) when (error.Message == "deterministic publish failure")
        {
            Check(mapStore.CountRecords("city", 82) == 2 &&
                  mapStore.GetRecord("city", 82, "rollback-old-a")?.Name == "Rollback Old A" &&
                  mapStore.GetRecord("city", 82, "rollback-old-b")?.Name == "Rollback Old B" &&
                  mapStore.GetRecord("city", 82, "rollback-new") is null,
                "staged publication rollback preserves the prior published kind/server on mid-transaction failure");
        }

        // LWB-R6-028 IMPLEMENTATION POLICY: exercise restart-safe persistence over
        // the recovered scan_blocks schema without claiming original scheduling,
        // acknowledgement, retry or status-transition behavior.
        mapStore.InsertScanRun(new MapScanRunSeed(
            "checkpoint-run-83", 83, "[\"city\"]", "synthetic-running", 3, 0, 0, 100, 100, null));
        mapStore.UpsertScanBlockCheckpointForTest(
            "checkpoint-run-83",
            new MapScanBlockCheckpoint(0, "{\"block\":0}", "synthetic-pending", 0, null, 110));
        mapStore.UpsertScanBlockCheckpointForTest(
            "checkpoint-run-83",
            new MapScanBlockCheckpoint(1, "{\"block\":1}", "synthetic-failed", 2, "synthetic failure", 120));
        mapStore.UpsertScanBlockCheckpointForTest(
            "checkpoint-run-83",
            new MapScanBlockCheckpoint(1, "{\"block\":1,\"retry\":true}", "synthetic-retry", 3, null, 130));

        IReadOnlyList<MapScanBlockCheckpoint> checkpointRows =
            mapStore.ReadScanBlockCheckpointsForTest("checkpoint-run-83");
        Check(checkpointRows.Count == 2 &&
              checkpointRows[0].BlockIndex == 0 && checkpointRows[0].Attempts == 0 &&
              checkpointRows[1].BlockIndex == 1 && checkpointRows[1].Attempts == 3 &&
              checkpointRows[1].Status == "synthetic-retry" && checkpointRows[1].Error is null &&
              checkpointRows[1].PayloadJson.Contains("retry", StringComparison.Ordinal),
              "scan block checkpoint upsert is keyed by recovered run/block identity and preserves the latest durable state");

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
        IReadOnlyList<MapScanBlockCheckpoint> reopenedCheckpoints =
            reopenedMapStore.ReadScanBlockCheckpointsForTest("checkpoint-run-83");
        Check(reopenedCheckpoints.Count == 2 &&
              reopenedCheckpoints[1].BlockIndex == 1 && reopenedCheckpoints[1].Attempts == 3 &&
              reopenedCheckpoints[1].Status == "synthetic-retry" && reopenedCheckpoints[1].Error is null,
            "scan block checkpoints survive database restart for later resume/reconciliation");
        MapClearResult checkpointClear = reopenedMapStore.ClearServer(83);
        Check(checkpointClear.DeletedRuns == 1 &&
              reopenedMapStore.ReadScanBlockCheckpointsForTest("checkpoint-run-83").Count == 0,
            "server-scoped scan clear cascades recovered scan-run deletion to persisted block checkpoints");
        Check(reopenedMapStore.DeletePlayerMark(77, largeOwnerUid) && reopenedMapStore.GetPlayerMark(77, largeOwnerUid) is null,
            "player mark delete uses recovered server/owner UID identity");
    }
}
finally
{
    try { Directory.Delete(mapStoreRoot, recursive: true); }
    catch { }
}

// LWB-R6-030 RECOVERED / OFFLINE-TESTED: validate the recovered native source/run
// decision without enabling the still-incomplete public map_data_options response.
MapOptionSourceSelection activeOptionSource =
    MapDataStore.SelectOptionSource(120, isReading: true, scanStateServerId: 120, scanRunId: "active-run-120");
Check(activeOptionSource.UsesStagingRecords &&
      activeOptionSource.ServerId == 120 &&
      activeOptionSource.ScanRunId == "active-run-120",
    "map option source selector uses the active scan run only for a matching reading server with nonempty scanRunId");

Check(!MapDataStore.SelectOptionSource(120, isReading: false, scanStateServerId: 120, scanRunId: "active-run-120").UsesStagingRecords,
    "map option source selector falls back to published rows when scan state is not reading");
Check(!MapDataStore.SelectOptionSource(120, isReading: true, scanStateServerId: 121, scanRunId: "active-run-120").UsesStagingRecords,
    "map option source selector falls back to published rows when scan-state server differs from the requested server");
Check(!MapDataStore.SelectOptionSource(120, isReading: true, scanStateServerId: 120, scanRunId: null).UsesStagingRecords &&
      !MapDataStore.SelectOptionSource(120, isReading: true, scanStateServerId: 120, scanRunId: "").UsesStagingRecords,
    "map option source selector requires a present nonempty scanRunId for staging rows");
Check(MapDataStore.SelectOptionSource(120, isReading: true, scanStateServerId: 120, scanRunId: " ").UsesStagingRecords,
    "map option source selector preserves the recovered raw nonempty-string test without trimming scanRunId");

// PM7-A / LWB-R6-038 IMPLEMENTED/OFFLINE-TESTED: one source-aware aggregate
// service now evaluates the recovered option/count SQL families against either the
// published map_records scope or the exact active scan_records run scope. Public
// assembly remains fail-closed until exact scanProgress serialization/state is proven.
using (var persistedOptionsStore = MapDataStore.CreateInMemory())
{
    const int optionServerId = 120;
    const long optionNowUnixMilliseconds = 5_000;

    void SeedOptionRecord(
        string kind,
        int serverId,
        string recordKey,
        string dataJson,
        string? allianceName = null,
        int? level = null,
        long updatedAt = 1_000)
    {
        persistedOptionsStore.UpsertRecord(new MapStoredRecord(
            kind, serverId, recordKey, null, null, null, allianceName,
            level, null, null, null, null, updatedAt, dataJson));
    }

    void SeedStagedOptionRecord(
        string runId,
        string kind,
        int serverId,
        string recordKey,
        string dataJson,
        string? allianceName = null,
        int? level = null,
        long updatedAt = 1_000)
    {
        persistedOptionsStore.StageRecordForPublishTest(runId, new MapStoredRecord(
            kind, serverId, recordKey, null, null, null, allianceName,
            level, null, null, null, null, updatedAt, dataJson));
    }

    SeedOptionRecord("city", optionServerId, "city-alpha-1", "{\"ownerUid\":\"a1\"}", "Alpha");
    SeedOptionRecord("city", optionServerId, "city-alpha-2", "{\"ownerUid\":\"a2\"}", "Alpha");
    SeedOptionRecord("city", optionServerId, "city-empty", "{\"ownerUid\":\"empty\"}", "");
    SeedOptionRecord("city", optionServerId, "city-null", "{\"ownerUid\":\"null\"}");
    SeedOptionRecord("city", optionServerId + 1, "city-other-server", "{\"ownerUid\":\"other\"}", "Other");

    SeedOptionRecord("resource", optionServerId, "resource-wood-1", "{\"resourceNameKey\":\"wood\"}");
    SeedOptionRecord("resource", optionServerId, "resource-wood-2", "{\"resourceNameKey\":\"wood\"}");
    SeedOptionRecord("resource", optionServerId, "resource-empty", "{\"resourceNameKey\":\"\"}");
    SeedOptionRecord("monster", optionServerId, "monster-zombie", "{\"monsterNameKey\":\"zombie\"}");

    SeedOptionRecord("dispatch", optionServerId, "dispatch-level-3-a", "{}", level: 3);
    SeedOptionRecord("dispatch", optionServerId, "dispatch-level-1", "{}", level: 1);
    SeedOptionRecord("dispatch", optionServerId, "dispatch-level-3-b", "{}", level: 3);
    SeedOptionRecord("dispatch", optionServerId, "dispatch-level-0", "{}", level: 0);

    SeedOptionRecord("treasure", optionServerId, "treasure-ordinary-1",
        "{\"suppliesType\":0,\"treasureType\":12,\"treasureNameKey\":\"treasure-12\"}");
    SeedOptionRecord("treasure", optionServerId, "treasure-ordinary-2",
        "{\"suppliesType\":0,\"treasureType\":12,\"treasureNameKey\":\"treasure-12-new\"}");
    SeedOptionRecord("treasure", optionServerId, "treasure-supplies",
        "{\"suppliesType\":4,\"treasureType\":99,\"treasureNameKey\":\"supplies-4\"}");
    SeedOptionRecord("treasure", optionServerId, "treasure-zero",
        "{\"suppliesType\":0,\"treasureType\":0,\"treasureNameKey\":\"zero\"}");

    SeedOptionRecord("truck", optionServerId, "truck-future",
        "{\"arriveTs\":6000,\"currentGoods\":[{\"key\":\"iron\",\"name\":\"Iron\",\"iconPath\":\"iron.png\"},{\"key\":\"iron\",\"name\":\"Iron\",\"iconPath\":\"iron.png\"}]}");
    SeedOptionRecord("truck", optionServerId, "truck-past",
        "{\"arriveTs\":4000,\"currentGoods\":[{\"key\":\"past\",\"name\":\"Past\",\"iconPath\":\"past.png\"}]}");
    SeedOptionRecord("railway", optionServerId, "railway-no-arrival",
        "{\"currentGoods\":[{\"key\":\"food\",\"name\":\"Food\"},{\"key\":\"missing-name\"}]}");
    SeedOptionRecord("truck", optionServerId + 1, "truck-other-server",
        "{\"arriveTs\":6000,\"currentGoods\":[{\"key\":\"other\",\"name\":\"Other\"}]}");

    persistedOptionsStore.InsertScanRun(new MapScanRunSeed(
        "options-run-old", optionServerId, "[\"city\"]", "completed", 100, 100, 0, 500, 1_000, null));
    persistedOptionsStore.InsertScanRun(new MapScanRunSeed(
        "options-run-new", optionServerId, "[\"city\",\"truck\"]", "running", 100, 40, 1, 1_500, 3_000, "one failed block"));
    persistedOptionsStore.InsertScanRun(new MapScanRunSeed(
        "options-run-discarded", optionServerId, "[\"city\"]", "discarded", 100, 100, 0, 2_000, 4_000, null));
    persistedOptionsStore.InsertScanRun(new MapScanRunSeed(
        "options-active-run", optionServerId,
        "[\"city\",\"resource\",\"truck\",\"dispatch\",\"treasure\"]",
        "running", 200, 75, 2, 2_100, 2_500, "synthetic active failure"));
    persistedOptionsStore.InsertScanRun(new MapScanRunSeed(
        "options-other-run", optionServerId, "[\"city\"]", "running", 50, 10, 0, 2_200, 2_600, null));
    persistedOptionsStore.InsertScanRun(new MapScanRunSeed(
        "options-other-server-run", optionServerId + 1, "[\"city\"]", "running", 50, 10, 0, 2_300, 2_700, null));

    SeedStagedOptionRecord("options-active-run", "city", optionServerId,
        "staged-city-alpha", "{\"ownerUid\":\"stage-alpha\"}", "StageAlpha");
    SeedStagedOptionRecord("options-active-run", "city", optionServerId,
        "staged-city-no-alliance", "{\"ownerUid\":\"stage-none\"}", "");
    SeedStagedOptionRecord("options-active-run", "resource", optionServerId,
        "staged-resource-stone", "{\"resourceNameKey\":\"stone\"}");
    SeedStagedOptionRecord("options-active-run", "dispatch", optionServerId,
        "staged-dispatch-level-7", "{}", level: 7);
    SeedStagedOptionRecord("options-active-run", "treasure", optionServerId,
        "staged-treasure-21", "{\"suppliesType\":0,\"treasureType\":21,\"treasureNameKey\":\"treasure-21\"}");
    SeedStagedOptionRecord("options-active-run", "truck", optionServerId,
        "staged-truck", "{\"arriveTs\":6000,\"currentGoods\":[{\"key\":\"stage-iron\",\"name\":\"Stage Iron\"}]}");

    SeedStagedOptionRecord("options-other-run", "city", optionServerId,
        "wrong-run-city", "{\"ownerUid\":\"wrong-run\"}", "WrongRun");
    SeedStagedOptionRecord("options-other-run", "resource", optionServerId,
        "wrong-run-resource", "{\"resourceNameKey\":\"wrong-run\"}");
    SeedStagedOptionRecord("options-other-server-run", "city", optionServerId + 1,
        "wrong-server-city", "{\"ownerUid\":\"wrong-server\"}", "WrongServer");

    MapOptionSourceSelection publishedOptionSource =
        MapDataStore.SelectOptionSource(optionServerId, isReading: false, scanStateServerId: optionServerId, scanRunId: null);
    MapOptionAggregates persistedOptions =
        persistedOptionsStore.ReadOptionAggregatesAt(publishedOptionSource, optionNowUnixMilliseconds);

    Check(persistedOptions.Alliances.Count == 1 &&
          persistedOptions.Alliances.Single(item => item.Name == "Alpha").Count == 2 &&
          persistedOptions.Alliances.All(item => item.Name != "Other"),
        "persisted option alliance aggregation emits only nonempty alliance names while keeping recovered ordering and server scope");
    Check(persistedOptions.Names.Count == 2 &&
          persistedOptions.Names.Any(item => item.Kind == "resource" && item.Key == "wood" && item.Count == 2) &&
          persistedOptions.Names.Any(item => item.Kind == "monster" && item.Key == "zombie" && item.Count == 1) &&
          persistedOptions.Names.All(item => item.Key.Length > 0),
        "persisted resource/monster option aggregation excludes empty keys and preserves counts");
    Check(persistedOptions.DispatchLevels.SequenceEqual(new[] { 1, 3 }),
        "persisted dispatch option levels are distinct positive integers ordered ascending");
    Check(persistedOptions.TreasureTypes.Count == 2 &&
          persistedOptions.TreasureTypes[0].Key == "treasure:12" &&
          persistedOptions.TreasureTypes[0].SuppliesType == 0 &&
          persistedOptions.TreasureTypes[0].TreasureType == 12 &&
          persistedOptions.TreasureTypes[0].Count == 2 &&
          persistedOptions.TreasureTypes[0].TreasureNameKey == "treasure-12-new" &&
          persistedOptions.TreasureTypes[1].Key == "supplies:4" &&
          persistedOptions.TreasureTypes[1].SuppliesType == 4 &&
          persistedOptions.TreasureTypes[1].TreasureType == 0 &&
          persistedOptions.TreasureTypes[1].Count == 1,
        "persisted treasure options preserve recovered key formatting, ordinary/supplies normalization, grouping and ordering");
    Check(persistedOptions.RewardItems.Count == 2 &&
          persistedOptions.RewardItems[0].Kind == "railway" && persistedOptions.RewardItems[0].Key == "food" &&
          persistedOptions.RewardItems[1].Kind == "truck" && persistedOptions.RewardItems[1].Key == "iron" &&
          persistedOptions.RewardItems.All(item => item.Key != "past" && item.Key != "other"),
        "persisted reward options deduplicate current goods, apply recovered Unix-ms arrival cutoff and isolate server scope");
    Check(persistedOptions.Counts.Count == 8 &&
          persistedOptions.Counts["city"] == 4 &&
          persistedOptions.Counts["resource"] == 3 &&
          persistedOptions.Counts["monster"] == 1 &&
          persistedOptions.Counts["truck"] == 2 &&
          persistedOptions.Counts["railway"] == 1 &&
          persistedOptions.Counts["dispatch"] == 4 &&
          persistedOptions.Counts["ghost"] == 0 &&
          persistedOptions.Counts["treasure"] == 4,
        "persisted option test kernel returns the exact eight frontend count keys with zero for an absent kind");
    Check(persistedOptions.NoAllianceCount == 2,
        "persisted option test kernel accumulates null and empty alliance groups into native noAllianceCount instead of alliances[]");
    Check(persistedOptions.ScanProgress?.Id == "options-run-new" &&
          persistedOptions.ScanProgress.ServerId == optionServerId &&
          persistedOptions.ScanProgress.Status == "running" &&
          persistedOptions.ScanProgress.CompletedBlocks == 40 &&
          persistedOptions.ScanProgress.FailedBlocks == 1 &&
          persistedOptions.ScanProgress.Error == "one failed block",
        "persisted option scan progress selects newest non-discarded run for the requested server");

    MapOptionAggregates otherServerOptions =
        persistedOptionsStore.ReadOptionAggregatesAt(
            MapDataStore.SelectOptionSource(
                optionServerId + 1, isReading: false, scanStateServerId: optionServerId + 1, scanRunId: null),
            optionNowUnixMilliseconds);
    Check(otherServerOptions.Alliances.Count == 1 &&
          otherServerOptions.Alliances[0].Name == "Other" &&
          otherServerOptions.RewardItems.Count == 1 &&
          otherServerOptions.RewardItems[0].Key == "other" &&
          otherServerOptions.Counts["city"] == 1 &&
          otherServerOptions.Counts["truck"] == 1 &&
          otherServerOptions.Counts.Where(item => item.Key is not "city" and not "truck").All(item => item.Value == 0) &&
          otherServerOptions.NoAllianceCount == 0 &&
          otherServerOptions.ScanProgress?.Id == "options-other-server-run" &&
          otherServerOptions.ScanProgress.ServerId == optionServerId + 1,
        "persisted option aggregation does not mix rows, counts, no-alliance state or scan progress across servers");

    MapOptionAggregates stagedOptions = persistedOptionsStore.ReadOptionAggregatesAt(
        MapDataStore.SelectOptionSource(
            optionServerId, isReading: true, scanStateServerId: optionServerId, scanRunId: "options-active-run"),
        optionNowUnixMilliseconds);
    Check(stagedOptions.Alliances.Count == 1 &&
          stagedOptions.Alliances[0].Name == "StageAlpha" && stagedOptions.Alliances[0].Count == 1 &&
          stagedOptions.NoAllianceCount == 1,
        "active option aggregation reads alliance/no-alliance groups only from the exact staged run");
    Check(stagedOptions.Names.Count == 1 &&
          stagedOptions.Names[0].Kind == "resource" && stagedOptions.Names[0].Key == "stone" &&
          stagedOptions.DispatchLevels.SequenceEqual(new[] { 7 }),
        "active option aggregation reads names and dispatch levels from the staged source without published/run leakage");
    Check(stagedOptions.TreasureTypes.Count == 1 && stagedOptions.TreasureTypes[0].Key == "treasure:21" &&
          stagedOptions.RewardItems.Count == 1 && stagedOptions.RewardItems[0].Key == "stage-iron",
        "active option aggregation applies recovered treasure keys and reward cutoff within the exact staged run");
    Check(stagedOptions.Counts["city"] == 2 && stagedOptions.Counts["resource"] == 1 &&
          stagedOptions.Counts["truck"] == 1 && stagedOptions.Counts["dispatch"] == 1 &&
          stagedOptions.Counts["treasure"] == 1 &&
          stagedOptions.Counts.Where(item => item.Key is not "city" and not "resource" and not "truck" and not "dispatch" and not "treasure")
              .All(item => item.Value == 0),
        "active option counts use the same exact staged run as every option family");
    Check(stagedOptions.ScanProgress?.Id == "options-active-run" &&
          stagedOptions.ScanProgress.ServerId == optionServerId &&
          stagedOptions.ScanProgress.CompletedBlocks == 75 && stagedOptions.ScanProgress.FailedBlocks == 2,
        "active option progress record selection uses the exact staged scanRunId rather than the newest run");
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

    using JsonDocument optionsPayload = JsonDocument.Parse(JsonSerializer.Serialize(new
    {
        profileId = mapBackend.ProfileId,
        serverId = 91,
    }));
    await ExpectBridgeError("MAP_INDEX_UNAVAILABLE", "public map_data_options stays fail-closed until exact progress serialization and production state integration are established", async () =>
        await mapBackend.InvokeAsync("map_data_options", optionsPayload.RootElement.Clone(), CancellationToken.None));

    using JsonDocument exportPayload = JsonDocument.Parse(JsonSerializer.Serialize(new
    {
        profileId = mapBackend.ProfileId,
        query = new
        {
            serverId = 91,
            page = 1,
            pageSize = 200,
            sorts = new[] { new { sortBy = "updatedAt", sortOrder = "desc" } },
        },
        headers = new[] { "Server", "X", "Y", "Player", "UID", "UUID", "Alliance", "Level", "HP", "Shield Ends", "Marked", "Updated At" },
        sheetName = "City",
        yesLabel = "Yes",
        noLabel = "No",
    }));
    await ExpectBridgeError("MAP_INDEX_UNAVAILABLE", "recovered city export envelope reaches the explicit writer gate without enabling guessed export behavior", async () =>
        await mapBackend.InvokeAsync("map_city_export", exportPayload.RootElement.Clone(), CancellationToken.None));

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
    ("city", "{\"kind\":\"city\",\"query\":{\"serverId\":7,\"keyword\":\"\",\"alliance\":\"ONE\",\"markedOnly\":true,\"page\":1,\"pageSize\":50,\"sorts\":[{\"sortBy\":\"updatedAt\",\"sortOrder\":\"desc\"}]}}", []),
    ("resource", "{\"kind\":\"resource\",\"query\":{\"serverId\":7,\"keyword\":\"\",\"resourceNameKey\":\"iron\",\"page\":1,\"pageSize\":50,\"sorts\":[{\"sortBy\":\"updatedAt\",\"sortOrder\":\"desc\"}]}}", []),
    ("monster", "{\"kind\":\"monster\",\"query\":{\"serverId\":7,\"keyword\":\"\",\"monsterNameKey\":\"doom\",\"page\":1,\"pageSize\":50,\"sorts\":[{\"sortBy\":\"updatedAt\",\"sortOrder\":\"desc\"}]}}", []),
    ("truck", "{\"kind\":\"truck\",\"query\":{\"serverId\":7,\"keyword\":\"\",\"quality\":\"ur\",\"itemKey\":\"item:1\",\"plunderableOnly\":true,\"page\":1,\"pageSize\":50,\"sorts\":[{\"sortBy\":\"updatedAt\",\"sortOrder\":\"desc\"}]}}", []),
    ("railway", "{\"kind\":\"railway\",\"query\":{\"serverId\":7,\"keyword\":\"\",\"quality\":\"ssr\",\"itemKey\":\"item:2\",\"plunderableOnly\":true,\"page\":1,\"pageSize\":50,\"sorts\":[{\"sortBy\":\"updatedAt\",\"sortOrder\":\"desc\"}]}}", []),
    ("dispatch", "{\"kind\":\"dispatch\",\"query\":{\"serverId\":7,\"keyword\":\"\",\"specialOnly\":true,\"completionStatus\":\"pending\",\"plunderableOnly\":true,\"minLevel\":5,\"maxLevel\":5,\"page\":1,\"pageSize\":50,\"sorts\":[{\"sortBy\":\"updatedAt\",\"sortOrder\":\"desc\"}]}}", []),
    ("ghost", "{\"kind\":\"ghost\",\"query\":{\"serverId\":7,\"keyword\":\"\",\"quality\":\"ssr\",\"completionStatus\":\"completed\",\"page\":1,\"pageSize\":50,\"sorts\":[{\"sortBy\":\"updatedAt\",\"sortOrder\":\"desc\"}]}}", []),
    ("treasure", "{\"kind\":\"treasure\",\"query\":{\"serverId\":7,\"keyword\":\"\",\"treasureType\":1,\"suppliesType\":0,\"includeForeignRadarTreasures\":false,\"luckyFirst\":true,\"viewerUid\":\"10001\",\"viewerAllianceId\":\"20002\",\"page\":1,\"pageSize\":50,\"sorts\":[{\"sortBy\":\"updatedAt\",\"sortOrder\":\"desc\"}]}}", ["includeForeignRadarTreasures", "luckyFirst", "viewerUid", "viewerAllianceId"]),
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
    indexedSearchStore.UpsertRecord(new MapStoredRecord(
        "resource", 7, "resource-iron", 21, "resource-a", "Iron Mine", null,
        10, null, null, null, null, 2500,
        "{\"serverId\":7,\"resourceNameKey\":\"iron\",\"level\":10,\"updatedAt\":2500}"));
    indexedSearchStore.UpsertRecord(new MapStoredRecord(
        "resource", 7, "resource-food", 22, "resource-b", "Food Field", null,
        10, null, null, null, null, 2400,
        "{\"serverId\":7,\"resourceNameKey\":\"food\",\"level\":10,\"updatedAt\":2400}"));
    indexedSearchStore.UpsertRecord(new MapStoredRecord(
        "monster", 7, "monster-doom", 31, "monster-a", "Doom Elite", null,
        20, null, null, null, null, 2300,
        "{\"serverId\":7,\"monsterNameKey\":\"doom\",\"level\":20,\"updatedAt\":2300}"));
    indexedSearchStore.UpsertRecord(new MapStoredRecord(
        "monster", 7, "monster-zombie", 32, "monster-b", "Zombie", null,
        20, null, null, null, null, 2200,
        "{\"serverId\":7,\"monsterNameKey\":\"zombie\",\"level\":20,\"updatedAt\":2200}"));
    indexedSearchStore.UpsertRecord(new MapStoredRecord(
        "truck", 7, "truck-item-1", 41, "truck-a", "Truck A", null,
        null, 5, null, null, null, 2100,
        "{\"serverId\":7,\"uuid\":\"truck-a\",\"quality\":5,\"isSpecialURQuality\":false,\"currentGoods\":[{\"key\":\"item:1\",\"count\":2}],\"updatedAt\":2100}"));
    indexedSearchStore.UpsertRecord(new MapStoredRecord(
        "truck", 7, "truck-item-2", 42, "truck-b", "Truck B", null,
        null, 4, null, null, null, 2000,
        "{\"serverId\":7,\"uuid\":\"truck-b\",\"quality\":4,\"currentGoods\":[{\"key\":\"item:2\",\"count\":1}],\"updatedAt\":2000}"));
    indexedSearchStore.UpsertRecord(new MapStoredRecord(
        "truck", 7, "truck-reindeer", 43, "truck-c", "Truck C", null,
        null, 6, null, null, null, 1900,
        "{\"serverId\":7,\"uuid\":\"truck-c\",\"quality\":6,\"isSpecialURQuality\":true,\"updatedAt\":1900}"));
    indexedSearchStore.UpsertRecord(new MapStoredRecord(
        "truck", 7, "truck-quality-n", 44, "truck-d", "Truck D", null,
        null, 1, null, null, null, 1890,
        "{\"serverId\":7,\"uuid\":\"truck-d\",\"quality\":1,\"updatedAt\":1890}"));
    indexedSearchStore.UpsertRecord(new MapStoredRecord(
        "truck", 7, "truck-quality-r", 45, "truck-e", "Truck E", null,
        null, 2, null, null, null, 1880,
        "{\"serverId\":7,\"uuid\":\"truck-e\",\"quality\":2,\"updatedAt\":1880}"));
    indexedSearchStore.UpsertRecord(new MapStoredRecord(
        "truck", 7, "truck-quality-sr", 46, "truck-f", "Truck F", null,
        null, 3, null, null, null, 1870,
        "{\"serverId\":7,\"uuid\":\"truck-f\",\"quality\":3,\"updatedAt\":1870}"));
    indexedSearchStore.UpsertRecord(new MapStoredRecord(
        "truck", 7, "truck-quality-ur-high", 47, "truck-g", "Truck G", null,
        null, 7, null, null, null, 1860,
        "{\"serverId\":7,\"uuid\":\"truck-g\",\"quality\":7,\"isSpecialURQuality\":false,\"updatedAt\":1860}"));
    indexedSearchStore.UpsertRecord(new MapStoredRecord(
        "truck", 8, "truck-quality-ur-other-server", 48, "truck-server8", "Truck Server 8", null,
        null, 8, null, null, null, 1855,
        "{\"serverId\":8,\"uuid\":\"truck-server8\",\"quality\":8,\"isSpecialURQuality\":false,\"updatedAt\":1855}"));
    indexedSearchStore.UpsertRecord(new MapStoredRecord(
        "railway", 7, "railway-quality-ur-special", 49, "railway-a", "Railway A", null,
        null, 6, null, null, null, 1850,
        "{\"serverId\":7,\"uuid\":\"railway-a\",\"quality\":6,\"isSpecialURQuality\":true,\"currentGoods\":[{\"key\":\"item:2\",\"count\":1}],\"updatedAt\":1850}"));
    indexedSearchStore.UpsertRecord(new MapStoredRecord(
        "dispatch", 7, "dispatch-regular", 51, "dispatch-a", "Dispatch A", null,
        6, null, null, null, null, 1800,
        "{\"serverId\":7,\"uuid\":\"dispatch-a\",\"level\":6,\"isSpecial\":false,\"updatedAt\":1800}"));
    indexedSearchStore.UpsertRecord(new MapStoredRecord(
        "dispatch", 7, "dispatch-special", 52, "dispatch-b", "Dispatch B", null,
        5, null, null, null, null, 1700,
        "{\"serverId\":7,\"uuid\":\"dispatch-b\",\"level\":5,\"isSpecial\":true,\"updatedAt\":1700}"));
    indexedSearchStore.UpsertRecord(new MapStoredRecord(
        "dispatch", 7, "dispatch-quality-ur-special", 55, "dispatch-c", "Dispatch C", null,
        7, 6, null, null, null, 1690,
        "{\"serverId\":7,\"uuid\":\"dispatch-c\",\"level\":7,\"quality\":6,\"isSpecial\":false,\"isSpecialURQuality\":true,\"updatedAt\":1690}"));
    indexedSearchStore.UpsertRecord(new MapStoredRecord(
        "ghost", 7, "ghost-quality-ur-special", 56, "ghost-a", "Ghost A", null,
        null, 7, null, null, null, 1680,
        "{\"serverId\":7,\"uuid\":\"ghost-a\",\"quality\":7,\"isSpecialURQuality\":true,\"updatedAt\":1680}"));
    indexedSearchStore.UpsertRecord(new MapStoredRecord(
        "treasure", 7, "treasure-standard", 53, "treasure-a", "Treasure A", null,
        null, null, null, null, null, 1650,
        "{\"serverId\":7,\"uuid\":\"treasure-a\",\"treasureType\":5,\"suppliesType\":0,\"treasureNameKey\":\"standard-five\",\"updatedAt\":1650}"));
    indexedSearchStore.UpsertRecord(new MapStoredRecord(
        "treasure", 7, "treasure-supplies", 54, "treasure-b", "Treasure B", null,
        null, null, null, null, null, 1640,
        "{\"serverId\":7,\"uuid\":\"treasure-b\",\"treasureType\":5,\"suppliesType\":3,\"treasureNameKey\":\"supplies-three\",\"updatedAt\":1640}"));
    indexedSearchStore.UpsertRecord(new MapStoredRecord(
        "city", 8, "city-literal-wildcards", 61, "literal-a", "A%_\\B", "LIT",
        null, null, null, null, null, 1600,
        "{\"serverId\":8,\"ownerUid\":\"literal-owner\",\"ownerName\":\"A%_\\\\B\",\"updatedAt\":1600}"));
    indexedSearchStore.UpsertRecord(new MapStoredRecord(
        "city", 8, "city-wildcard-control", 62, "control-a", "AxyzQB", "CONTROL",
        null, null, null, null, null, 1500,
        "{\"serverId\":8,\"ownerUid\":\"control-owner\",\"ownerName\":\"AxyzQB\",\"updatedAt\":1500}"));
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

    using JsonDocument allianceSearch = JsonDocument.Parse(JsonSerializer.Serialize(new
    {
        profileId = indexedSearchBackend.ProfileId,
        kind = "city",
        query = new { serverId = 7, alliance = "ONE" },
    }));
    object? allianceResult = await indexedSearchBackend.InvokeAsync(
        "map_search", allianceSearch.RootElement.Clone(), CancellationToken.None);
    using (JsonDocument resultJson = JsonDocument.Parse(JsonSerializer.Serialize(allianceResult, JsonOptions.Default)))
    {
        JsonElement rows = resultJson.RootElement.GetProperty("rows");
        Check(resultJson.RootElement.GetProperty("total").GetInt32() == 1 &&
              rows[0].GetProperty("ownerName").GetString() == "Alpha",
            "city alliance search uses recovered alliance_name equality predicate");
    }

    using JsonDocument withoutAllianceSearch = JsonDocument.Parse(JsonSerializer.Serialize(new
    {
        profileId = indexedSearchBackend.ProfileId,
        kind = "city",
        query = new { serverId = 7, withoutAlliance = true },
    }));
    object? withoutAllianceResult = await indexedSearchBackend.InvokeAsync(
        "map_search", withoutAllianceSearch.RootElement.Clone(), CancellationToken.None);
    using (JsonDocument resultJson = JsonDocument.Parse(JsonSerializer.Serialize(withoutAllianceResult, JsonOptions.Default)))
    {
        JsonElement rows = resultJson.RootElement.GetProperty("rows");
        Check(resultJson.RootElement.GetProperty("total").GetInt32() == 1 &&
              rows[0].GetProperty("ownerName").GetString() == "Charlie",
            "city no-alliance search uses recovered null/empty alliance predicate");
    }

    foreach ((string kind, string field, string value) in new[]
    {
        ("resource", "resourceNameKey", "iron"),
        ("monster", "monsterNameKey", "doom"),
    })
    {
        string queryJson = $"{{\"profileId\":\"{indexedSearchBackend.ProfileId}\",\"kind\":\"{kind}\",\"query\":{{\"serverId\":7,\"{field}\":\"{value}\"}}}}";
        using JsonDocument filteredSearch = JsonDocument.Parse(queryJson);
        object? filteredResult = await indexedSearchBackend.InvokeAsync(
            "map_search", filteredSearch.RootElement.Clone(), CancellationToken.None);
        using JsonDocument resultJson = JsonDocument.Parse(JsonSerializer.Serialize(filteredResult, JsonOptions.Default));
        JsonElement rows = resultJson.RootElement.GetProperty("rows");
        Check(resultJson.RootElement.GetProperty("total").GetInt32() == 1 &&
              rows[0].GetProperty(field).GetString() == value,
            $"{kind} name-key search uses recovered JSON equality predicate");
        Check(indexedSearchStore.SearchIndexed(MapDataQueryContract.NormalizeSearch(filteredSearch.RootElement)).Total == 1,
            $"{kind} name-key predicate is applied by persisted store");
    }

    using JsonDocument itemSearch = JsonDocument.Parse(JsonSerializer.Serialize(new
    {
        profileId = indexedSearchBackend.ProfileId,
        kind = "truck",
        query = new { serverId = 7, itemKey = "item:1" },
    }));
    object? itemResult = await indexedSearchBackend.InvokeAsync(
        "map_search", itemSearch.RootElement.Clone(), CancellationToken.None);
    using (JsonDocument resultJson = JsonDocument.Parse(JsonSerializer.Serialize(itemResult, JsonOptions.Default)))
    {
        JsonElement rows = resultJson.RootElement.GetProperty("rows");
        Check(resultJson.RootElement.GetProperty("total").GetInt32() == 1 &&
              rows[0].GetProperty("uuid").GetString() == "truck-a",
            "truck itemKey search uses recovered currentGoods membership predicate");
    }

    foreach ((string kind, string field, string expectedUuid) in new[]
    {
        ("dispatch", "specialOnly", "dispatch-b"),
        ("truck", "reindeerOnly", "truck-c"),
    })
    {
        string queryJson = $"{{\"profileId\":\"{indexedSearchBackend.ProfileId}\",\"kind\":\"{kind}\",\"query\":{{\"serverId\":7,\"{field}\":true}}}}";
        using JsonDocument booleanSearch = JsonDocument.Parse(queryJson);
        object? booleanResult = await indexedSearchBackend.InvokeAsync(
            "map_search", booleanSearch.RootElement.Clone(), CancellationToken.None);
        using JsonDocument resultJson = JsonDocument.Parse(JsonSerializer.Serialize(booleanResult, JsonOptions.Default));
        JsonElement rows = resultJson.RootElement.GetProperty("rows");
        Check(resultJson.RootElement.GetProperty("total").GetInt32() == 1 &&
              rows.GetArrayLength() == 1 && rows[0].GetProperty("uuid").GetString() == expectedUuid,
            $"{kind} {field} search uses recovered JSON boolean predicate");
    }

    foreach ((int treasureType, int suppliesType, string expectedUuid) in new[]
    {
        (5, 0, "treasure-a"),
        (0, 3, "treasure-b"),
    })
    {
        using JsonDocument treasureSearch = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            profileId = indexedSearchBackend.ProfileId,
            kind = "treasure",
            query = new { serverId = 7, treasureType, suppliesType },
        }));
        object? treasureResult = await indexedSearchBackend.InvokeAsync(
            "map_search", treasureSearch.RootElement.Clone(), CancellationToken.None);
        using JsonDocument resultJson = JsonDocument.Parse(JsonSerializer.Serialize(treasureResult, JsonOptions.Default));
        JsonElement rows = resultJson.RootElement.GetProperty("rows");
        Check(resultJson.RootElement.GetProperty("total").GetInt32() == 1 &&
              rows.GetArrayLength() == 1 && rows[0].GetProperty("uuid").GetString() == expectedUuid,
            $"treasure option filter preserves recovered treasure/supplies dimension {treasureType}/{suppliesType}");
    }

    using JsonDocument invalidTreasureSelection = JsonDocument.Parse(
        "{\"kind\":\"treasure\",\"query\":{\"serverId\":7,\"treasureType\":5,\"suppliesType\":3}}");
    Check(MapDataQueryContract.NormalizeSearch(invalidTreasureSelection.RootElement).UnsupportedFeatures
            .SequenceEqual(new[] { "treasureType", "suppliesType" }),
        "treasure filter rejects both-positive dimensions outside the recovered option shape");

    using JsonDocument partialTreasureSelection = JsonDocument.Parse(
        "{\"kind\":\"treasure\",\"query\":{\"serverId\":7,\"treasureType\":5}}");
    Check(MapDataQueryContract.NormalizeSearch(partialTreasureSelection.RootElement).UnsupportedFeatures
            .SequenceEqual(new[] { "treasureType", "suppliesType" }),
        "treasure filter rejects a partial selection because the frontend emits both dimensions");

    foreach ((string quality, string expectedUuid) in new[]
    {
        ("n", "truck-d"),
        ("r", "truck-e"),
        ("sr", "truck-f"),
        ("ssr", "truck-b"),
    })
    {
        using JsonDocument qualitySearch = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            profileId = indexedSearchBackend.ProfileId,
            kind = "truck",
            query = new { serverId = 7, quality },
        }));
        object? qualityResult = await indexedSearchBackend.InvokeAsync(
            "map_search", qualitySearch.RootElement.Clone(), CancellationToken.None);
        using JsonDocument resultJson = JsonDocument.Parse(JsonSerializer.Serialize(qualityResult, JsonOptions.Default));
        JsonElement rows = resultJson.RootElement.GetProperty("rows");
        Check(resultJson.RootElement.GetProperty("total").GetInt32() == 1 &&
              rows.GetArrayLength() == 1 && rows[0].GetProperty("uuid").GetString() == expectedUuid,
            $"truck quality {quality} uses recovered ordinary-quality predicate and binding");
    }

    foreach ((int page, string expectedUuid) in new[]
    {
        (1, "truck-a"),
        (2, "truck-g"),
    })
    {
        using JsonDocument truckUrSearch = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            profileId = indexedSearchBackend.ProfileId,
            kind = "truck",
            query = new { serverId = 7, quality = "ur", page, pageSize = 1 },
        }));
        object? truckUrResult = await indexedSearchBackend.InvokeAsync(
            "map_search", truckUrSearch.RootElement.Clone(), CancellationToken.None);
        using JsonDocument resultJson = JsonDocument.Parse(JsonSerializer.Serialize(truckUrResult, JsonOptions.Default));
        JsonElement rows = resultJson.RootElement.GetProperty("rows");
        Check(resultJson.RootElement.GetProperty("total").GetInt32() == 2 &&
              rows.GetArrayLength() == 1 && rows[0].GetProperty("uuid").GetString() == expectedUuid,
            $"truck UR page {page} keeps count/page consistent, includes non-special quality above five and remains server-scoped");
    }

    foreach ((string kind, string? itemKey, string expectedUuid) in new[]
    {
        ("railway", "item:2", "railway-a"),
        ("dispatch", null, "dispatch-c"),
        ("ghost", null, "ghost-a"),
    })
    {
        object query = itemKey is null
            ? new { serverId = 7, quality = "ur" }
            : new { serverId = 7, quality = "ur", itemKey };
        using JsonDocument nonTruckUrSearch = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            profileId = indexedSearchBackend.ProfileId,
            kind,
            query,
        }));
        object? nonTruckUrResult = await indexedSearchBackend.InvokeAsync(
            "map_search", nonTruckUrSearch.RootElement.Clone(), CancellationToken.None);
        using JsonDocument resultJson = JsonDocument.Parse(JsonSerializer.Serialize(nonTruckUrResult, JsonOptions.Default));
        JsonElement rows = resultJson.RootElement.GetProperty("rows");
        Check(resultJson.RootElement.GetProperty("total").GetInt32() == 1 &&
              rows.GetArrayLength() == 1 && rows[0].GetProperty("uuid").GetString() == expectedUuid &&
              rows[0].GetProperty("isSpecialURQuality").GetBoolean(),
            $"{kind} persisted UR search retains special-UR rows because the recovered exclusion is truck-only");
    }

    foreach (string kind in new[] { "truck", "railway", "dispatch", "ghost" })
    {
        using JsonDocument supportedQuality = JsonDocument.Parse(
            $"{{\"kind\":\"{kind}\",\"query\":{{\"serverId\":7,\"quality\":\"ur\"}}}}");
        Check(MapDataQueryContract.NormalizeSearch(supportedQuality.RootElement).UnsupportedFeatures.Count == 0,
            $"ordinary quality accepts recovered frontend kind {kind}");
    }

    using JsonDocument mismatchedQuality = JsonDocument.Parse(
        "{\"kind\":\"city\",\"query\":{\"serverId\":7,\"quality\":\"ur\"}}");
    Check(MapDataQueryContract.NormalizeSearch(mismatchedQuality.RootElement).UnsupportedFeatures
            .SequenceEqual(new[] { "quality" }),
        "ordinary quality remains fail-closed outside the four recovered frontend kinds");

    using JsonDocument unknownQuality = JsonDocument.Parse(
        "{\"kind\":\"truck\",\"query\":{\"serverId\":7,\"quality\":\"legendary\"}}");
    Check(MapDataQueryContract.NormalizeSearch(unknownQuality.RootElement).UnsupportedFeatures
            .SequenceEqual(new[] { "quality" }),
        "unknown quality selector remains fail-closed");

    using JsonDocument numericQuality = JsonDocument.Parse(
        "{\"kind\":\"truck\",\"query\":{\"serverId\":7,\"quality\":5}}");
    Check(MapDataQueryContract.NormalizeSearch(numericQuality.RootElement).UnsupportedFeatures
            .SequenceEqual(new[] { "quality" }),
        "numeric backend quality form remains outside the recovered frontend public contract");

    using JsonDocument dispatchLevelSearch = JsonDocument.Parse(JsonSerializer.Serialize(new
    {
        profileId = indexedSearchBackend.ProfileId,
        kind = "dispatch",
        query = new { serverId = 7, minLevel = 5, maxLevel = 5 },
    }));
    object? dispatchLevelResult = await indexedSearchBackend.InvokeAsync(
        "map_search", dispatchLevelSearch.RootElement.Clone(), CancellationToken.None);
    using (JsonDocument resultJson = JsonDocument.Parse(JsonSerializer.Serialize(dispatchLevelResult, JsonOptions.Default)))
    {
        JsonElement rows = resultJson.RootElement.GetProperty("rows");
        Check(resultJson.RootElement.GetProperty("total").GetInt32() == 1 &&
              rows.GetArrayLength() == 1 && rows[0].GetProperty("uuid").GetString() == "dispatch-b",
            "dispatch exact-level filter uses the recovered level >= / <= predicate pair");
    }

    using JsonDocument mismatchedSpecialSearch = JsonDocument.Parse(
        "{\"kind\":\"city\",\"query\":{\"serverId\":7,\"specialOnly\":true}}");
    Check(MapDataQueryContract.NormalizeSearch(mismatchedSpecialSearch.RootElement).UnsupportedFeatures
            .SequenceEqual(new[] { "specialOnly" }),
        "specialOnly remains fail-closed outside recovered dispatch/ghost kinds");

    foreach ((string kind, string field) in new[]
    {
        ("dispatch", "specialOnly"),
        ("ghost", "specialOnly"),
        ("truck", "reindeerOnly"),
    })
    {
        using JsonDocument supportedBoolean = JsonDocument.Parse(
            $"{{\"kind\":\"{kind}\",\"query\":{{\"serverId\":7,\"{field}\":true}}}}");
        Check(MapDataQueryContract.NormalizeSearch(supportedBoolean.RootElement).UnsupportedFeatures.Count == 0,
            $"{field} accepts recovered frontend kind {kind}");
    }

    using JsonDocument mismatchedReindeerSearch = JsonDocument.Parse(
        "{\"kind\":\"railway\",\"query\":{\"serverId\":7,\"reindeerOnly\":true}}");
    Check(MapDataQueryContract.NormalizeSearch(mismatchedReindeerSearch.RootElement).UnsupportedFeatures
            .SequenceEqual(new[] { "reindeerOnly" }),
        "reindeerOnly remains fail-closed on railway because the recovered visible selector only emits it for truck");

    using JsonDocument explicitFalseSpecial = JsonDocument.Parse(
        "{\"kind\":\"dispatch\",\"query\":{\"serverId\":7,\"specialOnly\":false}}");
    Check(MapDataQueryContract.NormalizeSearch(explicitFalseSpecial.RootElement).UnsupportedFeatures
            .SequenceEqual(new[] { "specialOnly" }),
        "explicit false specialOnly stays fail-closed because the recovered frontend omits that form");

    foreach ((string keyword, string expectedOwner) in new[]
    {
        ("alpha", "Alpha"),
        ("one", "Alpha"),
        ("uuid-b", "Bravo"),
        ("10000000000000000003", "Charlie"),
    })
    {
        using JsonDocument keywordSearch = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            profileId = indexedSearchBackend.ProfileId,
            kind = "city",
            query = new { serverId = 7, keyword },
        }));
        object? keywordResult = await indexedSearchBackend.InvokeAsync(
            "map_search", keywordSearch.RootElement.Clone(), CancellationToken.None);
        using JsonDocument resultJson = JsonDocument.Parse(JsonSerializer.Serialize(keywordResult, JsonOptions.Default));
        JsonElement rows = resultJson.RootElement.GetProperty("rows");
        Check(resultJson.RootElement.GetProperty("total").GetInt32() == 1 &&
              rows.GetArrayLength() == 1 && rows[0].GetProperty("ownerName").GetString() == expectedOwner,
            $"keyword literal substring searches recovered name/alliance/uuid/data_json columns for {keyword}");
    }

    using JsonDocument nameColumnKeywordSearch = JsonDocument.Parse(JsonSerializer.Serialize(new
    {
        profileId = indexedSearchBackend.ProfileId,
        kind = "resource",
        query = new { serverId = 7, keyword = "mine" },
    }));
    object? nameColumnKeywordResult = await indexedSearchBackend.InvokeAsync(
        "map_search", nameColumnKeywordSearch.RootElement.Clone(), CancellationToken.None);
    using (JsonDocument resultJson = JsonDocument.Parse(JsonSerializer.Serialize(nameColumnKeywordResult, JsonOptions.Default)))
    {
        JsonElement rows = resultJson.RootElement.GetProperty("rows");
        Check(resultJson.RootElement.GetProperty("total").GetInt32() == 1 &&
              rows.GetArrayLength() == 1 && rows[0].GetProperty("resourceNameKey").GetString() == "iron",
            "keyword searches the indexed name column when data_json does not contain the display name");
    }

    using JsonDocument escapedKeywordSearch = JsonDocument.Parse(JsonSerializer.Serialize(new
    {
        profileId = indexedSearchBackend.ProfileId,
        kind = "city",
        query = new { serverId = 8, keyword = "%_\\" },
    }));
    object? escapedKeywordResult = await indexedSearchBackend.InvokeAsync(
        "map_search", escapedKeywordSearch.RootElement.Clone(), CancellationToken.None);
    using (JsonDocument resultJson = JsonDocument.Parse(JsonSerializer.Serialize(escapedKeywordResult, JsonOptions.Default)))
    {
        JsonElement rows = resultJson.RootElement.GetProperty("rows");
        Check(resultJson.RootElement.GetProperty("total").GetInt32() == 1 &&
              rows.GetArrayLength() == 1 && rows[0].GetProperty("ownerName").GetString() == "A%_\\B",
            "keyword escapes backslash, percent and underscore before literal-substring matching");
    }

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

// LWB-R6-014: deterministic wall-clock boundaries use an isolated store so the
// recovered time predicates cannot change the older quality/count fixtures.
using (var timeFilterStore = MapDataStore.CreateInMemory())
{
    const long recoveredNow = 1_800_000_000_000L;

    void SeedTimeRecord(string kind, int serverId, string uuid, long updatedAt, string fields)
    {
        string json = $"{{\"serverId\":{serverId},\"uuid\":\"{uuid}\",{fields},\"updatedAt\":{updatedAt}}}";
        timeFilterStore.UpsertRecord(new MapStoredRecord(
            kind, serverId, uuid, null, uuid, uuid, null,
            null, null, null, null, null, updatedAt, json));
    }

    MapSearchResult SearchAt(string kind, int serverId, string extraQuery = "")
    {
        using JsonDocument query = JsonDocument.Parse(
            $"{{\"kind\":\"{kind}\",\"query\":{{\"serverId\":{serverId}{extraQuery}}}}}");
        return timeFilterStore.SearchIndexedAtForTest(
            MapDataQueryContract.NormalizeSearch(query.RootElement), recoveredNow);
    }

    static HashSet<string> ResultUuids(MapSearchResult result) =>
        result.Rows.Select(row => row.GetProperty("uuid").GetString()!).ToHashSet(StringComparer.Ordinal);

    SeedTimeRecord("truck", 77, "truck-null-arrival", 106, "\"arriveTs\":null,\"remainingLootCount\":1");
    SeedTimeRecord("truck", 77, "truck-future", 105, $"\"arriveTs\":{recoveredNow + 1},\"remainingLootCount\":1");
    SeedTimeRecord("truck", 77, "truck-at-now", 104, $"\"arriveTs\":{recoveredNow},\"remainingLootCount\":1");
    SeedTimeRecord("truck", 77, "truck-expired", 103, $"\"arriveTs\":{recoveredNow - 1},\"remainingLootCount\":1");
    SeedTimeRecord("truck", 77, "truck-empty", 102, $"\"arriveTs\":{recoveredNow + 10},\"remainingLootCount\":0");
    SeedTimeRecord("truck", 77, "truck-fallback", 101, $"\"arriveTs\":{recoveredNow + 20},\"maxLootCount\":2,\"robTimes\":1");

    MapSearchResult truckDefault = SearchAt("truck", 77);
    Check(truckDefault.Total == 4 && ResultUuids(truckDefault).SetEquals(
            new[] { "truck-null-arrival", "truck-future", "truck-empty", "truck-fallback" }),
        "truck default search keeps null/future arrivals and excludes arriveTs <= sampled now");

    MapSearchResult truckPlunderable = SearchAt("truck", 77, ",\"plunderableOnly\":true");
    Check(truckPlunderable.Total == 2 && ResultUuids(truckPlunderable).SetEquals(
            new[] { "truck-future", "truck-fallback" }),
        "truck plunderableOnly requires a future non-null arrival and positive direct/fallback remaining loot");

    SeedTimeRecord("railway", 77, "railway-future", 202, $"\"arriveTs\":{recoveredNow + 1},\"remainingLootCount\":1");
    SeedTimeRecord("railway", 77, "railway-expired", 201, $"\"arriveTs\":{recoveredNow - 1},\"remainingLootCount\":1");
    MapSearchResult railwayPlunderable = SearchAt("railway", 77, ",\"plunderableOnly\":true");
    Check(railwayPlunderable.Total == 1 && ResultUuids(railwayPlunderable).SetEquals(new[] { "railway-future" }),
        "railway plunderableOnly shares the recovered active-arrival and remaining-loot predicates");

    SeedTimeRecord("dispatch", 78, "dispatch-null", 305, "\"completionTime\":null");
    SeedTimeRecord("dispatch", 78, "dispatch-zero", 304, "\"completionTime\":0");
    SeedTimeRecord("dispatch", 78, "dispatch-future", 303, $"\"completionTime\":{recoveredNow + 1}");
    SeedTimeRecord("dispatch", 78, "dispatch-now", 302, $"\"completionTime\":{recoveredNow}");
    SeedTimeRecord("dispatch", 78, "dispatch-past", 301, $"\"completionTime\":{recoveredNow - 1}");

    MapSearchResult dispatchPending = SearchAt("dispatch", 78, ",\"completionStatus\":\"pending\"");
    Check(dispatchPending.Total == 3 && ResultUuids(dispatchPending).SetEquals(
            new[] { "dispatch-null", "dispatch-zero", "dispatch-future" }),
        "dispatch pending completion includes null/nonpositive/future and excludes completionTime <= sampled now");
    MapSearchResult dispatchCompleted = SearchAt("dispatch", 78, ",\"completionStatus\":\"completed\"");
    Check(dispatchCompleted.Total == 2 && ResultUuids(dispatchCompleted).SetEquals(
            new[] { "dispatch-now", "dispatch-past" }),
        "dispatch completed completion requires positive completionTime <= sampled now");

    SeedTimeRecord("ghost", 79, "ghost-future", 402, $"\"completionTime\":{recoveredNow + 1}");
    SeedTimeRecord("ghost", 79, "ghost-past", 401, $"\"completionTime\":{recoveredNow - 1}");
    Check(SearchAt("ghost", 79, ",\"completionStatus\":\"pending\"").Total == 1 &&
          SearchAt("ghost", 79, ",\"completionStatus\":\"completed\"").Total == 1,
        "ghost completionStatus uses the same recovered pending/completed wall-clock boundary");

    SeedTimeRecord("dispatch", 80, "dispatch-plunder-valid", 505,
        $"\"completionTime\":{recoveredNow - 100},\"taskExpireTime\":{recoveredNow + 1},\"maxStealCount\":2,\"stolenCount\":1");
    SeedTimeRecord("dispatch", 80, "dispatch-plunder-expired", 504,
        $"\"completionTime\":{recoveredNow - 100},\"taskExpireTime\":{recoveredNow},\"maxStealCount\":2,\"stolenCount\":1");
    SeedTimeRecord("dispatch", 80, "dispatch-plunder-full", 503,
        $"\"completionTime\":{recoveredNow - 100},\"taskExpireTime\":{recoveredNow + 1},\"maxStealCount\":2,\"stolenCount\":2");
    SeedTimeRecord("dispatch", 80, "dispatch-plunder-zero", 502,
        $"\"completionTime\":{recoveredNow - 100},\"plunderAt\":0,\"taskExpireTime\":{recoveredNow + 1},\"maxStealCount\":0");
    SeedTimeRecord("dispatch", 80, "dispatch-completion-zero", 501,
        $"\"completionTime\":0,\"taskExpireTime\":{recoveredNow + 1},\"maxStealCount\":0");
    MapSearchResult dispatchPlunderable = SearchAt("dispatch", 80, ",\"plunderableOnly\":true");
    Check(dispatchPlunderable.Total == 1 && ResultUuids(dispatchPlunderable).SetEquals(new[] { "dispatch-plunder-valid" }),
        "dispatch plunderableOnly enforces completion, plunderAt fallback, strict expiry and steal-capacity predicates");

    foreach ((string json, string expectedFeature) in new[]
    {
        ("{\"kind\":\"truck\",\"query\":{\"serverId\":77,\"completionStatus\":\"pending\"}}", "completionStatus"),
        ("{\"kind\":\"dispatch\",\"query\":{\"serverId\":78,\"completionStatus\":\"later\"}}", "completionStatus"),
        ("{\"kind\":\"ghost\",\"query\":{\"serverId\":79,\"plunderableOnly\":true}}", "plunderableOnly"),
        ("{\"kind\":\"dispatch\",\"query\":{\"serverId\":80,\"plunderableOnly\":false}}", "plunderableOnly"),
    })
    {
        using JsonDocument unsupportedTimeFilter = JsonDocument.Parse(json);
        Check(MapDataQueryContract.NormalizeSearch(unsupportedTimeFilter.RootElement).UnsupportedFeatures
                .SequenceEqual(new[] { expectedFeature }),
            $"unrecovered public time-filter form remains fail-closed for {expectedFeature}");
    }
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
        bridgeControlPipeContract = true,
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
