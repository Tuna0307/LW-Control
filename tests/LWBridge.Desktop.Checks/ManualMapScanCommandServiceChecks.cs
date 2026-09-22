using System.Runtime.CompilerServices;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class ManualMapScanCommandServiceChecks
{
    [ModuleInitializer]
    internal static void Run()
    {
        RunAsync().GetAwaiter().GetResult();
    }

    private static async Task RunAsync()
    {
        await NormalStartOwnsOneRunAndStopCancels();
        await StopTimingMatrixPreservesCheckpointBoundary();
        await RestartReconcilesOrphanedRunAndRejectsConcurrentOwner();
        await BackendSummaryTracksActiveManualScan();
        await FreshClearResolvesAuthoritativeLiveServer();
        await ClearOwnsRecoveredGateAndResetsStatus();
        await BackendClearWithoutLiveOwnershipSucceedsLocally();
        await FastUsesRecoveredConcurrencyAndPublishes();
        await ContextFailureLeavesTruthfulError();
        await ZombieBossTypeIsAccepted();
        await RailwayTypeIsAccepted();
        await DispatchTypeIsAccepted();
        await GhostTypeIsAccepted();
        await TreasureTypeIsAccepted();
        await MixedTypesAreAccepted();
        await AllEightTypesAreAccepted();
        await MarchFollowPublicContractIsRecoveredAndUsesLiveSource();
        await ServerJumpPublicContractIsRecoveredAndBusyGated();
        ScheduledPlunderCommandsAreRetired();
        await TreasureStateRefreshPublicContractIsReadOnlyAndCached();
        await ZombieBossMixedTypesFailClosed();
        CurrentPlayerCityHealthContractIsRecovered();
    }

    private static void CurrentPlayerCityHealthContractIsRecovered()
    {
        string repoRoot = FindRepoRootForChecks();
        string probeSource = File.ReadAllText(Path.Combine(repoRoot, "tools", "current_live_resource_probe.lua"));
        int start = probeSource.IndexOf(
            "local function player_city_health_snapshot(info)",
            StringComparison.Ordinal);
        int end = probeSource.IndexOf(
            "local function read_command()",
            start,
            StringComparison.Ordinal);
        Check(start >= 0 && end > start,
            "Player City effective-health lane should remain identifiable in the production probe");
        string lane = probeSource[start..end];
        Check(lane.Contains("call(info, \"IsNormalType\")", StringComparison.Ordinal) &&
              lane.Contains("FUN_BUILD_MAIN", StringComparison.Ordinal) &&
              lane.Contains("GetDefenceWallCoverSpeed", StringComparison.Ordinal) &&
              lane.Contains("unavailable_time / 1000", StringComparison.Ordinal) &&
              lane.Contains("500300", StringComparison.Ordinal) &&
              lane.Contains("local max_hp = 10000", StringComparison.Ordinal) &&
              lane.Contains("math.floor(math.max(0, math.min(hp, max_hp)))", StringComparison.Ordinal),
            "Player City health must preserve the recovered current-v19 normal-city recovery/fire/cap contract");
        int effectiveAssignments = probeSource.Split(
            "health = health.effectiveHp",
            StringSplitOptions.None).Length - 1;
        Check(effectiveAssignments == 2 &&
              probeSource.Contains("healthRawCurHp = health.rawCurHp", StringComparison.Ordinal) &&
              probeSource.Contains("healthCalculationMode = health.calculationMode", StringComparison.Ordinal),
            "both Player City acquisition paths must publish effective HP while retaining raw source diagnostics");

        double screenshotRecoveredHp = Math.Floor(Math.Min(
            1_222d + ((1_790_001_233d - 1_789_856_054d) * 0.38100001215935d),
            10_000d));
        Check(screenshotRecoveredHp == 10_000d,
            "the recovered normal-city formula must resolve the reproduced (67,858) stale 1,222 baseline to full 10,000 HP");
    }

    private static JsonElement Payload(string mode, params string[] types) =>
        JsonSerializer.SerializeToElement(new
        {
            profileId = "default",
            scanMode = mode,
            selectedTypes = types,
        }, JsonOptions.Default);

    private static CurrentClientMapContext Context(int width = 20, int height = 20) =>
        new(2212, 0, width, height);

    private static CurrentClientMapContext StandardContext() =>
        new(2212, 0, 1000, 1000);

    private static async Task NormalStartOwnsOneRunAndStopCancels()
    {
        using MapDataStore store = MapDataStore.CreateInMemory();
        var source = new BlockingSource();
        int contextCalls = 0;
        var service = new ManualMapScanCommandService(
            store,
            _ =>
            {
                contextCalls++;
                return Task.FromResult(Context());
            },
            source);
        var observedPhases = new List<string>();
        service.StatusChanged += status =>
        {
            JsonElement json = JsonSerializer.SerializeToElement(status, JsonOptions.Default);
            observedPhases.Add(String(json, "phase"));
        };

        _ = await service.InvokeAsync(
            "map_scan_start",
            Payload("normal", "resource"),
            CancellationToken.None);
        await source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        JsonElement status = Status(service);
        Check(Bool(status, "isReading"), "normal Start should own an active scan");
        Check(String(status, "phase") == "scanning", "normal Start should enter scanning phase");
        Check(Int(status, "concurrency") == 8, "normal Start must preserve recovered concurrency 8");
        Check(Int(status, "totalBlocks") == 1 && Int(status, "unreadBlocks") == 0,
            "one inflight 20x20 block should use truthful scheduler counters");
        string activeRunId = String(status, "scanRunId");
        string activeScanMode = String(status, "scanMode");
        string activeStrategy = String(status, "scanStrategy");
        string[] activeTypes = status.GetProperty("selectedTypes").EnumerateArray()
            .Select(value => value.GetString() ?? string.Empty)
            .ToArray();

        try
        {
            _ = await service.InvokeAsync(
                "map_scan_start",
                Payload("fast", "city"),
                CancellationToken.None);
            throw new InvalidOperationException("expected changed-type duplicate Start rejection");
        }
        catch (BridgeCommandException error) when (
            error.Code == MapScanStartOwnership.ActiveScanErrorCode &&
            error.Message == MapScanStartOwnership.ActiveScanErrorMessage)
        {
        }

        JsonElement afterChangedTypeStart = Status(service);
        string[] afterTypes = afterChangedTypeStart.GetProperty("selectedTypes").EnumerateArray()
            .Select(value => value.GetString() ?? string.Empty)
            .ToArray();
        Check(String(afterChangedTypeStart, "scanRunId") == activeRunId &&
              String(afterChangedTypeStart, "scanMode") == activeScanMode &&
              String(afterChangedTypeStart, "scanStrategy") == activeStrategy &&
              Int(afterChangedTypeStart, "concurrency") == 8 &&
              afterTypes.SequenceEqual(activeTypes) &&
              activeTypes.SequenceEqual(new[] { "resource" }),
            "changed-type duplicate Start must leave the active run identity, selected types and backend strategy/config untouched");

        object? stop = await service.InvokeAsync(
            "map_scan_stop",
            Payload("normal", "resource"),
            CancellationToken.None);
        status = JsonSerializer.SerializeToElement(stop, JsonOptions.Default);
        Check(!Bool(status, "isReading") && String(status, "phase") == "idle" && Int(status, "inflightBlocks") == 0 &&
              !Bool(status, "resumeAvailable"),
            "one Stop response should be terminal, release scan ownership and explicitly keep unsupported resume unavailable");
        Check(observedPhases.Contains("cancelling") && observedPhases.Contains("idle"),
            "scan status notifications should expose cancelling and terminal idle states");
        Check(contextCalls == 1, "duplicate Start must not reacquire live context");
        service.Close();
    }

    private static async Task StopTimingMatrixPreservesCheckpointBoundary()
    {
        foreach ((string label, int completedBeforeStop) in new[]
                 {
                     ("early", 0),
                     ("mid", 2),
                     ("near-completion", 4),
                 })
        {
            using MapDataStore store = MapDataStore.CreateInMemory();
            string baselineKey = "stop-" + label + "-published";
            store.UpsertRecord(new MapStoredRecord(
                "city", 2212, baselineKey, 1, "stop-" + label + "-uuid", "Published", null,
                30, null, null, null, null, 1000,
                "{\"serverId\":2212,\"ownerUid\":\"stop-owner\"}"));

            var source = new SequencedGateSource();
            var service = new ManualMapScanCommandService(
                store,
                _ => Task.FromResult(Context(100, 20)),
                source);
            try
            {
                JsonElement start = JsonSerializer.SerializeToElement(
                    await service.InvokeAsync(
                        "map_scan_start",
                        Payload("normal", "city"),
                        CancellationToken.None),
                    JsonOptions.Default);
                string runId = String(start, "scanRunId");
                Check(!string.IsNullOrWhiteSpace(runId) &&
                      Int(start, "totalBlocks") == 5 &&
                      Bool(start, "isReading"),
                    $"B06 {label}: Start must expose one owned five-block run");

                Check(SpinWait.SpinUntil(() => source.Calls >= 1, TimeSpan.FromSeconds(2)),
                    $"B06 {label}: first capture did not enter");
                for (int completed = 0; completed < completedBeforeStop; completed++)
                {
                    int callNumber = completed + 1;
                    source.Release(callNumber);
                    Check(SpinWait.SpinUntil(() => source.Calls >= callNumber + 1, TimeSpan.FromSeconds(2)),
                        $"B06 {label}: capture {callNumber + 1} did not enter after releasing {callNumber}");
                }

                Check(source.Calls == completedBeforeStop + 1,
                    $"B06 {label}: expected exactly one blocked in-flight capture at Stop boundary");
                Check(store.ReadScanBlockCheckpointsForTest(runId).Count == completedBeforeStop,
                    $"B06 {label}: durable checkpoint count before Stop must match completed captures");

                JsonElement stopped = JsonSerializer.SerializeToElement(
                    await service.InvokeAsync(
                        "map_scan_stop",
                        Payload("normal", "city"),
                        CancellationToken.None),
                    JsonOptions.Default);

                Check(String(stopped, "scanRunId") == runId &&
                      !Bool(stopped, "isReading") &&
                      String(stopped, "phase") == "idle" &&
                      Int(stopped, "totalBlocks") == 5 &&
                      Int(stopped, "readBlocks") == completedBeforeStop &&
                      Int(stopped, "failedBlocks") == 0 &&
                      Int(stopped, "inflightBlocks") == 0 &&
                      Int(stopped, "unreadBlocks") == 5 - completedBeforeStop,
                    $"B06 {label}: Stop must return terminal idle with the exact partial scheduler boundary");

                IReadOnlyList<MapScanBlockCheckpoint> checkpoints =
                    store.ReadScanBlockCheckpointsForTest(runId);
                Check(checkpoints.Count == completedBeforeStop &&
                      checkpoints.All(row => row.Status == "completed"),
                    $"B06 {label}: canceled in-flight work must not become a checkpoint");
                int callsAfterStop = source.Calls;
                Thread.Sleep(50);
                Check(source.Calls == callsAfterStop &&
                      store.ReadScanBlockCheckpointsForTest(runId).Count == completedBeforeStop,
                    $"B06 {label}: no new scheduling/checkpoint may occur after Stop returns");
                Check(store.GetRecord("city", 2212, baselineKey) is not null,
                    $"B06 {label}: stopped partial scan must not replace previously published data");
            }
            finally
            {
                service.Close();
            }
        }
    }

    private static async Task RestartReconcilesOrphanedRunAndRejectsConcurrentOwner()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "lwbridge-map-restart-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            var orphanRequest = new MapScanExecutionRequest(
                "restart-orphan", 2212, 7, 20, 20, ["city"], 8, 2,
                PlayerTileX: 3, PlayerTileY: 4,
                ScanMode: "normal", LaunchSessionId: "launch-before-crash");
            MapScanTargetBlock block = MapScanTraversal.Build(20, 20)[0];
            var oldPublished = new MapStoredRecord(
                "city", 2212, "published-before-crash", 1, "published-uuid",
                "Published", null, 30, null, null, null, null, 10,
                "{\"serverId\":2212,\"ownerUid\":\"published-owner\"}");
            var stagedOnly = new MapStoredRecord(
                "city", 2212, "staged-before-crash", 2, "staged-uuid",
                "Staged", null, 31, null, null, null, null, 11,
                "{\"serverId\":2212,\"ownerUid\":\"staged-owner\"}");

            using (var beforeCrash = new MapDataStore(path))
            {
                beforeCrash.UpsertRecord(oldPublished);
                var sink = new MapDataStoreScanSink(beforeCrash);
                sink.Begin(orphanRequest, 1, 100);
                sink.CheckpointSuccess(
                    orphanRequest,
                    block,
                    new MapScanBlockCapture(
                        2212, 7, block.BlockIndex, "{}", [stagedOnly]),
                    1,
                    101);
                Check(beforeCrash.ReadScanBlockCheckpointsForTest(orphanRequest.RunId).Count == 1,
                    "interrupted scan fixture must persist its completed checkpoint before simulated process death");
                // Deliberately dispose the database without Stop/Fail/Publish. This models
                // an abrupt process loss after durable checkpointing.
            }

            using var reopened = new MapDataStore(path);
            MapOptionAggregates beforeReconcile = reopened.ReadOptionAggregatesAt(
                MapDataStore.SelectOptionSource(2212, false, 2212, null),
                1_000_000);
            Check(beforeReconcile.ScanProgress is { Status: "running" },
                "abrupt process loss should leave the durable run in running state before a new owner reconciles it");

            var firstSource = new BlockingSource();
            var firstService = new ManualMapScanCommandService(
                reopened,
                _ => Task.FromResult(new CurrentClientMapContext(
                    2212, 7, 20, 20, 3, 4, "launch-after-restart")),
                firstSource,
                getLiveServerId: () => 2212);

            MapOptionAggregates afterReconcile = reopened.ReadOptionAggregatesAt(
                MapDataStore.SelectOptionSource(2212, false, 2212, null),
                1_000_001);
            Check(afterReconcile.ScanProgress is { Status: "failed" } &&
                  afterReconcile.ScanProgress.Error == MapScanRestartPolicy.InterruptedError,
                "the next exclusive scan owner must mark an orphaned running row failed with the restart reason");
            Check(reopened.ReadScanBlockCheckpointsForTest(orphanRequest.RunId).Count == 1,
                "restart reconciliation must preserve durable partial checkpoints as interruption evidence");
            Check(reopened.GetRecord("city", 2212, oldPublished.RecordKey) is not null &&
                  reopened.GetRecord("city", 2212, stagedOnly.RecordKey) is null,
                "restart reconciliation must preserve the previous published dataset and never expose staged rows");

            try
            {
                new MapDataStoreScanSink(reopened).Publish(orphanRequest, 102);
                throw new InvalidOperationException("expected reconciled orphan publication rejection");
            }
            catch (BridgeCommandException error) when (
                error.Code == "INVALID_SCAN" &&
                error.Message == "map scan is not running")
            {
            }

            _ = await firstService.InvokeAsync(
                "map_scan_start",
                Payload("normal", "city"),
                CancellationToken.None);
            await firstSource.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));

            using var concurrentStore = new MapDataStore(path);
            var secondSource = new BlockingSource();
            var secondService = new ManualMapScanCommandService(
                concurrentStore,
                _ => Task.FromResult(new CurrentClientMapContext(
                    2212, 7, 20, 20, 3, 4, "launch-after-restart")),
                secondSource,
                getLiveServerId: () => 2212);

            MapOptionAggregates whileFirstOwns = concurrentStore.ReadOptionAggregatesAt(
                MapDataStore.SelectOptionSource(2212, false, 2212, null),
                1_000_002);
            Check(whileFirstOwns.ScanProgress is { Status: "running" } &&
                  whileFirstOwns.ScanProgress.Error is null,
                "a second app instance must not reconcile a running row while another process owns the scan lease");

            try
            {
                _ = await secondService.InvokeAsync(
                    "map_scan_start",
                    Payload("normal", "city"),
                    CancellationToken.None);
                throw new InvalidOperationException("expected cross-process scan owner rejection");
            }
            catch (BridgeCommandException error) when (
                error.Code == MapScanStartOwnership.ActiveScanErrorCode &&
                error.Message == MapScanStartOwnership.ActiveScanErrorMessage)
            {
            }

            _ = await firstService.InvokeAsync(
                "map_scan_stop",
                Payload("normal", "city"),
                CancellationToken.None);
            firstService.Close();

            _ = await secondService.InvokeAsync(
                "map_scan_start",
                Payload("normal", "city"),
                CancellationToken.None);
            await secondSource.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            JsonElement secondStatus = Status(secondService);
            Check(Bool(secondStatus, "isReading") &&
                  !Bool(secondStatus, "resumeAvailable"),
                "after the prior owner stops, a fresh scan may acquire the lease but unsupported resume must remain false");
            _ = await secondService.InvokeAsync(
                "map_scan_stop",
                Payload("normal", "city"),
                CancellationToken.None);
            secondService.Close();

            Check(reopened.GetRecord("city", 2212, oldPublished.RecordKey) is not null &&
                  reopened.GetRecord("city", 2212, stagedOnly.RecordKey) is null,
                "interrupted and stopped replacement scans must never overwrite the last trustworthy published data");
        }
        finally
        {
            foreach (string candidate in new[]
            {
                path, path + "-wal", path + "-shm", path + ".scan-owner.lock"
            })
            {
                try { File.Delete(candidate); } catch { }
            }
        }
    }

    private static async Task BackendSummaryTracksActiveManualScan()
    {
        using MapDataStore store = MapDataStore.CreateInMemory();
        var source = new BlockingSource();
        var service = new ManualMapScanCommandService(
            store,
            _ => Task.FromResult(StandardContext()),
            source);
        var backend = new LWBridgeBackend(
            new LocalConfigStore(persistent: false),
            asyncCommands: service,
            mapData: store,
            mapScanStatusProvider: service.CreateStatus);

        _ = await service.InvokeAsync(
            "map_scan_start",
            Payload("fast", "monster"),
            CancellationToken.None);
        await source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        JsonElement profile = JsonSerializer.SerializeToElement(new { profileId = backend.ProfileId }, JsonOptions.Default);
        JsonElement summary = JsonSerializer.SerializeToElement(
            await backend.InvokeAsync("map_summary", profile, CancellationToken.None),
            JsonOptions.Default);
        JsonElement scanState = summary.GetProperty("scanState");
        Check(summary.GetProperty("serverId").GetInt32() == 2212 &&
              Bool(scanState, "isReading") && String(scanState, "phase") == "scanning" &&
              scanState.GetProperty("serverId").GetInt32() == 2212,
            "periodic map_summary should preserve the active Manual Scan state");

        _ = await service.InvokeAsync(
            "map_scan_stop",
            Payload("fast", "monster"),
            CancellationToken.None);
        summary = JsonSerializer.SerializeToElement(
            await backend.InvokeAsync("map_summary", profile, CancellationToken.None),
            JsonOptions.Default);
        scanState = summary.GetProperty("scanState");
        Check(!Bool(scanState, "isReading") && String(scanState, "phase") == "idle",
            "map_summary should expose terminal idle after one Stop");
        service.Close();
    }

    private static async Task FreshClearResolvesAuthoritativeLiveServer()
    {
        using MapDataStore store = MapDataStore.CreateInMemory();
        var source = new ImmediateSource();
        var service = new ManualMapScanCommandService(
            store,
            _ => Task.FromResult(Context()),
            source,
            getLiveServerId: () => 2212);
        store.UpsertRecord(new MapStoredRecord(
            "city", 2212, "fresh-clear-city", 1, "fresh-clear-uuid", "Fresh Clear", null,
            30, null, null, null, null, 1000,
            "{\"serverId\":2212,\"ownerUid\":\"fresh-clear-owner\"}"));

        JsonElement payload = JsonSerializer.SerializeToElement(
            new { profileId = "default", serverId = 2212 },
            JsonOptions.Default);
        JsonElement cleared = JsonSerializer.SerializeToElement(
            await service.InvokeAsync("map_scan_clear", payload, CancellationToken.None),
            JsonOptions.Default);
        Check(store.CountRecords("city", 2212) == 0 &&
              Int(cleared, "serverId") == 2212 &&
              String(cleared, "serverIdSource") == MapScanClearOwnership.LiveServerSource &&
              String(cleared, "phase") == "idle",
            "Clear before any scan should resolve and retain the authoritative live server identity");
        Check(source.Calls == 0,
            "Clear should not start or probe a map scan merely to resolve the current live server");
        service.Close();
    }

    private static async Task ClearOwnsRecoveredGateAndResetsStatus()
    {
        using MapDataStore store = MapDataStore.CreateInMemory();
        var source = new BlockingSource();
        var service = new ManualMapScanCommandService(
            store,
            _ => Task.FromResult(Context()),
            source,
            getLiveServerId: () => 2212);

        store.UpsertRecord(new MapStoredRecord(
            "city", 2212, "clear-city", 1, "clear-city-uuid", "Clear City", null,
            30, null, null, null, null, 1000,
            "{\"serverId\":2212,\"ownerUid\":\"clear-owner\"}"));
        store.UpsertRecord(new MapStoredRecord(
            "city", 2213, "other-city", 2, "other-city-uuid", "Other City", null,
            29, null, null, null, null, 1001,
            "{\"serverId\":2213,\"ownerUid\":\"other-owner\"}"));
        store.UpsertPlayerMark(new MapPlayerMark(
            2212,
            "clear-owner",
            "active",
            2000,
            null,
            "{\"serverId\":2212,\"ownerUid\":\"clear-owner\"}"));

        int statusEvents = 0;
        service.StatusChanged += _ => statusEvents++;

        _ = await service.InvokeAsync(
            "map_scan_start",
            Payload("normal", "city"),
            CancellationToken.None);
        await source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        JsonElement clearAll = JsonSerializer.SerializeToElement(
            new { profileId = "default", serverId = 0 },
            JsonOptions.Default);
        try
        {
            _ = await service.InvokeAsync("map_scan_clear", clearAll, CancellationToken.None);
            throw new InvalidOperationException("expected active Clear rejection");
        }
        catch (BridgeCommandException error) when (
            error.Code == MapScanClearOwnership.ActiveScanErrorCode &&
            error.Message == MapScanClearOwnership.ActiveScanErrorMessage)
        {
        }
        Check(store.CountRecords("city", 2212) == 1 && store.CountRecords("city", 2213) == 1,
            "active Clear rejection must preserve every published server");

        _ = await service.InvokeAsync(
            "map_scan_stop",
            Payload("normal", "city"),
            CancellationToken.None);

        // After Auto Scan returns to 2212, clearing a browsed 2213 dataset is a
        // local data operation and must not require live-server equality.
        JsonElement otherServer = JsonSerializer.SerializeToElement(
            new { profileId = "default", serverId = 2213 },
            JsonOptions.Default);
        _ = await service.InvokeAsync("map_scan_clear", otherServer, CancellationToken.None);
        Check(store.CountRecords("city", 2212) == 1 && store.CountRecords("city", 2213) == 0,
            "stopped Clear should permit deleting a browsed non-live server dataset");

        // Re-add the second server and prove the UI's serverId=0 Clear removes all
        // scan datasets while preserving durable player marks.
        store.UpsertRecord(new MapStoredRecord(
            "city", 2213, "other-city", 2, "other-city-uuid", "Other City", null,
            29, null, null, null, null, 1001,
            "{\"serverId\":2213,\"ownerUid\":\"other-owner\"}"));
        int eventsBeforeClear = statusEvents;
        object? cleared = await service.InvokeAsync(
            "map_scan_clear",
            clearAll,
            CancellationToken.None);
        JsonElement status = JsonSerializer.SerializeToElement(cleared, JsonOptions.Default);
        Check(Int(status, "serverId") == 2212 &&
              String(status, "serverIdSource") == MapScanClearOwnership.LiveServerSource &&
              !Bool(status, "isReading") &&
              String(status, "phase") == "idle" &&
              String(status, "scanRunId") == string.Empty &&
              Int(status, "totalBlocks") == 0 &&
              Int(status, "readBlocks") == 0 &&
              Int(status, "failedBlocks") == 0 &&
              Int(status, "unreadBlocks") == 0 &&
              Int(status, "inflightBlocks") == 0 &&
              Int(status, "concurrency") == 0 &&
              Double(status, "scanRate") == 0 &&
              Double(status, "progressPercent") == 0 &&
              !Bool(status, "resumeAvailable"),
            "successful Clear should reset progress while retaining the authoritative live server");
        Check(statusEvents > eventsBeforeClear,
            "successful Clear should publish the reset scan status");
        Check(store.CountRecords("city", 2212) == 0 &&
              store.CountRecords("city", 2213) == 0 &&
              store.CountScanRuns(2212) == 0,
            "serverId=0 Clear should remove all current-session scan/index scopes");
        Check(store.GetPlayerMark(2212, "clear-owner") is not null,
            "session scan Clear should preserve player marks");

        _ = await service.InvokeAsync("map_scan_clear", clearAll, CancellationToken.None);
        Check(store.CountRecords("city", 2212) == 0 && store.CountRecords("city", 2213) == 0,
            "empty repeated all-server Clear should remain idempotent");
        service.Close();
    }

    private static async Task BackendClearWithoutLiveOwnershipSucceedsLocally()
    {
        using MapDataStore store = MapDataStore.CreateInMemory();
        var backend = new LWBridgeBackend(
            new LocalConfigStore(persistent: false),
            mapData: store);
        store.UpsertRecord(new MapStoredRecord(
            "city", 2212, "saved-only-city", 1, "saved-only-uuid", "Saved Only", null,
            30, null, null, null, null, 1000,
            "{\"serverId\":2212,\"ownerUid\":\"saved-only-owner\"}"));

        JsonElement payload = JsonSerializer.SerializeToElement(
            new { profileId = backend.ProfileId, serverId = 0 },
            JsonOptions.Default);
        _ = await backend.InvokeAsync("map_scan_clear", payload, CancellationToken.None);
        Check(store.CountRecords("city", 2212) == 0,
            "backend scan-data Clear must remain available without a live server because it is a local data operation");
    }

    private static async Task FastUsesRecoveredConcurrencyAndPublishes()
    {
        using MapDataStore store = MapDataStore.CreateInMemory();
        var source = new ImmediateSource();
        var service = new ManualMapScanCommandService(
            store,
            _ => Task.FromResult(Context(width: 40, height: 20)),
            source);

        object? start = await service.InvokeAsync(
            "map_scan_start",
            Payload("fast", "resource"),
            CancellationToken.None);
        JsonElement status = JsonSerializer.SerializeToElement(start, JsonOptions.Default);
        if (Bool(status, "isReading"))
            WaitForPhase(service, "completed");
        status = Status(service);

        Check(String(status, "phase") == "completed" && !Bool(status, "isReading") && !Bool(status, "resumeAvailable"),
            "ordinary fallback should publish after all blocks succeed without advertising unsupported resume");
        Check(Int(status, "concurrency") == 8 && String(status, "scanMode") == "normal" &&
              String(status, "scanStrategy") == MapScanStrategyPlanner.NormalBlockStrategy,
            "caller fast preference must not override the backend ordinary fallback plan");
        Check(Int(status, "totalBlocks") == 2 && Int(status, "readBlocks") == 2,
            "40x20 map should traverse two proven ordinary 20-tile blocks");
        Check(Double(status, "progressPercent") == 100.0,
            "completed ordinary fallback should expose 100 percent state");
        Check(source.Calls == 2, "ordinary fallback must use the block source for each traversal block");
        string runId = String(status, "scanRunId");
        Check(store.ReadScanBlockCheckpointsForTest(runId).Count == 0,
            "successful publication should clean block staging");
        service.Close();
    }

    private static async Task ContextFailureLeavesTruthfulError()
    {
        using MapDataStore store = MapDataStore.CreateInMemory();
        var service = new ManualMapScanCommandService(
            store,
            _ => throw new BridgeCommandException(
                MapScanStartOwnership.ServerUnavailableCode,
                MapScanStartOwnership.ServerUnavailableMessage),
            new ImmediateSource());
        try
        {
            _ = await service.InvokeAsync(
                "map_scan_start",
                Payload("normal", "city"),
                CancellationToken.None);
            throw new InvalidOperationException("expected live-context failure");
        }
        catch (BridgeCommandException error) when (
            error.Code == MapScanStartOwnership.ServerUnavailableCode)
        {
        }
        JsonElement status = Status(service);
        Check(!Bool(status, "isReading") && String(status, "phase") == "error",
            "failed Start should release ownership and expose an error phase");
        Check(String(status, "lastError") == MapScanStartOwnership.ServerUnavailableMessage,
            "failed Start should preserve the live-context error message");
        service.Close();
    }

    private static async Task ZombieBossTypeIsAccepted()
    {
        using MapDataStore store = MapDataStore.CreateInMemory();
        var source = new ImmediateSource();
        var service = new ManualMapScanCommandService(store, _ => Task.FromResult(StandardContext()), source);
        _ = await service.InvokeAsync("map_scan_start", Payload("normal", "zombie_boss"), CancellationToken.None);
        WaitForPhase(service, "completed");
        JsonElement status = Status(service);
        Check(Int(status, "readBlocks") == 2500 && Int(status, "failedBlocks") == 0 &&
              Int(status, "concurrency") == 20 && String(status, "scanMode") == "fast",
            "Zombie Boss should use the same ordinary Manual Scan worker as supported kinds");
        service.Close();
    }

    private static async Task RailwayTypeIsAccepted()
    {
        using MapDataStore store = MapDataStore.CreateInMemory();
        var source = new ImmediateSource();
        var service = new ManualMapScanCommandService(store, _ => Task.FromResult(StandardContext()), source);
        _ = await service.InvokeAsync("map_scan_start", Payload("normal", "railway"), CancellationToken.None);
        WaitForPhase(service, "completed");
        JsonElement status = Status(service);
        Check(Int(status, "readBlocks") == 2500 && Int(status, "failedBlocks") == 0 &&
              Int(status, "concurrency") == 20 && String(status, "scanMode") == "fast",
            "Railway/Train should use the same ordinary Manual Scan worker as supported kinds");
        service.Close();
    }

    private static async Task DispatchTypeIsAccepted()
    {
        using MapDataStore store = MapDataStore.CreateInMemory();
        var source = new ImmediateSource();
        var service = new ManualMapScanCommandService(store, _ => Task.FromResult(StandardContext()), source);
        _ = await service.InvokeAsync("map_scan_start", Payload("normal", "dispatch"), CancellationToken.None);
        WaitForPhase(service, "completed");
        JsonElement status = Status(service);
        Check(Int(status, "readBlocks") == 2500 && Int(status, "failedBlocks") == 0 &&
              Int(status, "concurrency") == 20 && String(status, "scanMode") == "fast",
            "Dispatch should use the same ordinary Manual Scan worker as supported kinds");
        service.Close();
    }

    private static async Task GhostTypeIsAccepted()
    {
        using MapDataStore store = MapDataStore.CreateInMemory();
        var source = new ImmediateSource();
        var service = new ManualMapScanCommandService(store, _ => Task.FromResult(StandardContext()), source);
        _ = await service.InvokeAsync("map_scan_start", Payload("normal", "ghost"), CancellationToken.None);
        WaitForPhase(service, "completed");
        JsonElement status = Status(service);
        Check(Int(status, "readBlocks") == 2500 && Int(status, "failedBlocks") == 0 &&
              Int(status, "concurrency") == 20 && String(status, "scanMode") == "fast",
            "Ghost Ops should use the same ordinary Manual Scan worker as supported kinds");
        service.Close();
    }

    private static async Task TreasureTypeIsAccepted()
    {
        using MapDataStore store = MapDataStore.CreateInMemory();
        var source = new ImmediateSource();
        var service = new ManualMapScanCommandService(store, _ => Task.FromResult(StandardContext()), source);
        _ = await service.InvokeAsync("map_scan_start", Payload("normal", "treasure"), CancellationToken.None);
        WaitForPhase(service, "completed");
        JsonElement status = Status(service);
        Check(Int(status, "readBlocks") == 2500 && Int(status, "failedBlocks") == 0 &&
              Int(status, "concurrency") == 20 && String(status, "scanMode") == "fast",
            "Treasure should use the same ordinary Manual Scan worker as supported kinds");
        service.Close();
    }

    private static async Task MixedTypesAreAccepted()
    {
        using MapDataStore store = MapDataStore.CreateInMemory();
        var source = new ImmediateSource();
        var service = new ManualMapScanCommandService(store, _ => Task.FromResult(StandardContext()), source);
        _ = await service.InvokeAsync(
            "map_scan_start", Payload("normal", "city", "resource"), CancellationToken.None);
        WaitForPhase(service, "completed");
        JsonElement status = Status(service);
        Check(Int(status, "readBlocks") == 2500 && Int(status, "failedBlocks") == 0 &&
              Int(status, "concurrency") == 20 && String(status, "scanMode") == "fast" &&
              status.GetProperty("selectedTypes").EnumerateArray().Select(value => value.GetString()).SequenceEqual(new[] { "city", "resource" }) &&
              source.LastSelectedTypes.SequenceEqual(new[] { "city", "resource" }),
            "mixed original Map Data kinds should flow unchanged through the shared Manual Scan worker");
        service.Close();
    }

    private static async Task AllEightTypesAreAccepted()
    {
        using MapDataStore store = MapDataStore.CreateInMemory();
        var source = new ImmediateSource();
        var service = new ManualMapScanCommandService(store, _ => Task.FromResult(StandardContext()), source);
        _ = await service.InvokeAsync(
            "map_scan_start", Payload("normal", MapScanContract.RecoveredDefaultTypes), CancellationToken.None);
        WaitForPhase(service, "completed");
        JsonElement status = Status(service);
        Check(Int(status, "readBlocks") == 2500 && Int(status, "failedBlocks") == 0 &&
              Int(status, "concurrency") == 20 && String(status, "scanMode") == "fast" &&
              source.LastSelectedTypes.SequenceEqual(MapScanContract.RecoveredDefaultTypes),
            "all eight recovered Map Data kinds should use one shared Manual Scan run");
        service.Close();
    }

    private static async Task MarchFollowPublicContractIsRecoveredAndUsesLiveSource()
    {
        using MapDataStore store = MapDataStore.CreateInMemory();
        var service = new ManualMapScanCommandService(
            store,
            _ => Task.FromResult(Context()),
            new ImmediateSource());

        Check(service.CanHandle("map_march_follow"),
            "Manual Map Scan service should expose the recovered map_march_follow command");

        foreach (JsonElement invalid in new[]
        {
            JsonSerializer.SerializeToElement(new { serverId = 0, marchUuid = "123" }, JsonOptions.Default),
            JsonSerializer.SerializeToElement(new { serverId = 2212, marchUuid = "" }, JsonOptions.Default),
            JsonSerializer.SerializeToElement(new { serverId = 2212, marchUuid = "not-a-number" }, JsonOptions.Default),
        })
        {
            try
            {
                _ = await service.InvokeAsync("map_march_follow", invalid, CancellationToken.None);
                throw new InvalidOperationException("invalid march Follow payload should fail before touching the live source");
            }
            catch (BridgeCommandException error) when (
                error.Code == "INVALID_MARCH" &&
                error.Message == "server ID and march UUID are required")
            {
            }
        }

        try
        {
            _ = await service.InvokeAsync(
                "map_march_follow",
                JsonSerializer.SerializeToElement(
                    new { serverId = 2212, marchUuid = "7654321090123" },
                    JsonOptions.Default),
                CancellationToken.None);
            throw new InvalidOperationException("synthetic service should not fabricate a live march Follow");
        }
        catch (BridgeCommandException error) when (error.Code == "COMMAND_NOT_IMPLEMENTED")
        {
        }
        service.Close();

        const int SyntheticServerId = 2212;
        const long SyntheticMarchUuid = 7654321090123;
        int requestedServerId = 0;
        long requestedMarchUuid = 0;
        var liveService = new ManualMapScanCommandService(
            store,
            _ => Task.FromResult(Context()),
            new ImmediateSource(),
            jumpToServer: null,
            getLiveServerId: null,
            followMarch: (serverId, marchUuid, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                requestedServerId = serverId;
                requestedMarchUuid = marchUuid;
                return Task.FromResult(new CurrentClientMarchFollowResult(serverId, marchUuid));
            });

        object? result = await liveService.InvokeAsync(
            "map_march_follow",
            JsonSerializer.SerializeToElement(
                new { serverId = SyntheticServerId, marchUuid = SyntheticMarchUuid.ToString() },
                JsonOptions.Default),
            CancellationToken.None);
        JsonElement json = JsonSerializer.SerializeToElement(result, JsonOptions.Default);
        Check(
            requestedServerId == SyntheticServerId &&
            requestedMarchUuid == SyntheticMarchUuid &&
            json.GetProperty("serverId").GetInt32() == SyntheticServerId &&
            json.GetProperty("marchUuid").GetString() == SyntheticMarchUuid.ToString(),
            "march Follow must preserve exact server and 64-bit march identity through the live source and result");
        liveService.Close();

        int recoveryCalls = 0;
        var recoveringService = new ManualMapScanCommandService(
            store,
            _ => Task.FromResult(Context()),
            new ImmediateSource(),
            followMarch: (serverId, marchUuid, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                recoveryCalls++;
                if (recoveryCalls == 1)
                    throw new BridgeCommandException("MARCH_FOLLOW_FAILED", "march_follow_completion_timeout");
                return Task.FromResult(new CurrentClientMarchFollowResult(serverId, marchUuid));
            });
        JsonElement followPayload = JsonSerializer.SerializeToElement(
            new { serverId = SyntheticServerId, marchUuid = SyntheticMarchUuid.ToString() },
            JsonOptions.Default);
        try
        {
            _ = await recoveringService.InvokeAsync("map_march_follow", followPayload, CancellationToken.None);
            throw new InvalidOperationException("vanished march should fail explicitly through the public service");
        }
        catch (BridgeCommandException error) when (
            error.Code == "MARCH_FOLLOW_FAILED" && error.Message == "march_follow_completion_timeout")
        {
        }
        JsonElement recovered = JsonSerializer.SerializeToElement(
            await recoveringService.InvokeAsync("map_march_follow", followPayload, CancellationToken.None),
            JsonOptions.Default);
        Check(recoveryCalls == 2 &&
              recovered.GetProperty("serverId").GetInt32() == SyntheticServerId &&
              recovered.GetProperty("marchUuid").GetString() == SyntheticMarchUuid.ToString(),
            "explicit stale-target Follow failure must release navigation ownership so a later valid target can succeed");
        recoveringService.Close();
    }

    private static async Task ServerJumpPublicContractIsRecoveredAndBusyGated()
    {
        using MapDataStore store = MapDataStore.CreateInMemory();
        var service = new ManualMapScanCommandService(
            store,
            _ => Task.FromResult(Context()),
            new ImmediateSource());

        Check(service.CanHandle("server_jump"),
            "Manual Map Scan service should expose the recovered server_jump command");

        try
        {
            _ = await service.InvokeAsync(
                "server_jump",
                JsonSerializer.SerializeToElement(new { serverId = 0 }, JsonOptions.Default),
                CancellationToken.None);
            throw new InvalidOperationException("invalid server Jump should fail before touching the live source");
        }
        catch (BridgeCommandException error) when (
            error.Code == "INVALID_SERVER_ID" &&
            error.Message == "server ID must be an integer from 1 to 99999")
        {
        }

        var busyField = typeof(ManualMapScanCommandService).GetField(
            "serverJumping",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new MissingFieldException("ManualMapScanCommandService.serverJumping");
        busyField.SetValue(service, true);
        try
        {
            _ = await service.InvokeAsync(
                "server_jump",
                JsonSerializer.SerializeToElement(new { serverId = 2212 }, JsonOptions.Default),
                CancellationToken.None);
            throw new InvalidOperationException("overlapping server Jump should fail with the recovered busy contract");
        }
        catch (BridgeCommandException error) when (
            error.Code == "GAME_OPERATION_IN_PROGRESS" &&
            error.Message == "another game operation is already in progress")
        {
        }
        finally
        {
            busyField.SetValue(service, false);
        }

        try
        {
            _ = await service.InvokeAsync(
                "server_jump",
                JsonSerializer.SerializeToElement(new { serverId = 2212 }, JsonOptions.Default),
                CancellationToken.None);
            throw new InvalidOperationException("synthetic service should not fabricate a live server Jump");
        }
        catch (BridgeCommandException error) when (error.Code == "COMMAND_NOT_IMPLEMENTED")
        {
        }

        service.Close();

        const int SyntheticOriginalServerId = 101;
        const int SyntheticTargetServerId = 202;
        int requestedTarget = 0;
        int statusEvents = 0;
        var liveService = new ManualMapScanCommandService(
            store,
            _ => Task.FromResult(Context()),
            new ImmediateSource(),
            (targetServerId, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                requestedTarget = targetServerId;
                return Task.FromResult(new CurrentClientServerJumpResult(
                    SyntheticOriginalServerId,
                    targetServerId != SyntheticOriginalServerId));
            });
        liveService.StatusChanged += _ => statusEvents++;
        object? jumpResult = await liveService.InvokeAsync(
            "server_jump",
            JsonSerializer.SerializeToElement(new { serverId = SyntheticTargetServerId }, JsonOptions.Default),
            CancellationToken.None);
        JsonElement jumpJson = JsonSerializer.SerializeToElement(jumpResult, JsonOptions.Default);
        JsonElement liveStatus = Status(liveService);
        Check(requestedTarget == SyntheticTargetServerId &&
              jumpJson.GetProperty("previousServerId").GetInt32() == SyntheticOriginalServerId &&
              jumpJson.GetProperty("changed").GetBoolean() &&
              Int(liveStatus, "serverId") == SyntheticTargetServerId &&
              String(liveStatus, "serverIdSource") == "live" &&
              statusEvents > 0,
            "successful server Jump must publish the authoritative target as the shared live server status");
        liveService.Close();

        int heartbeatReads = 0;
        var freshStatusService = new ManualMapScanCommandService(
            store,
            _ => Task.FromResult(Context()),
            new ImmediateSource(),
            jumpToServer: null,
            getLiveServerId: () =>
            {
                heartbeatReads++;
                return SyntheticOriginalServerId;
            });
        JsonElement freshStatus = Status(freshStatusService);
        Check(Int(freshStatus, "serverId") == SyntheticOriginalServerId &&
              String(freshStatus, "serverIdSource") == "live" &&
              heartbeatReads == 1,
            "fresh idle Map Scan status must initialize its unknown server from the owned live heartbeat");
        JsonElement secondFreshStatus = Status(freshStatusService);
        Check(heartbeatReads == 2 &&
              Int(secondFreshStatus, "serverId") == SyntheticOriginalServerId &&
              Int(secondFreshStatus, "liveServerId") == SyntheticOriginalServerId &&
              String(secondFreshStatus, "serverIdSource") == "live",
            "idle Map Scan status may refresh physical liveServerId without changing the current dataset server identity");
        freshStatusService.Close();
    }

    private static async Task TreasureStateRefreshPublicContractIsReadOnlyAndCached()
    {
        using MapDataStore store = MapDataStore.CreateInMemory();
        const int ServerId = 2212;
        const string PlayerUid = "viewer-uid";
        const string AllianceId = "viewer-alliance";

        void SeedTreasure(
            int pointIndex,
            string uuid,
            int treasureType,
            int suppliesType,
            string allianceId,
            long expireTime)
        {
            string json = JsonSerializer.Serialize(new
            {
                kind = "treasure",
                serverId = ServerId,
                pointId = pointIndex,
                uuid,
                treasureType,
                suppliesType,
                allianceId,
                viewerUid = PlayerUid,
                viewerAllianceId = AllianceId,
                viewerHasReward = false,
                viewerIsWorking = false,
                complete = false,
                expireTime,
                startTime = 1000,
                completionTime = 2000,
                rewardedCount = 1,
                diggingCount = 2,
                rewardMax = 5,
                remainingBoxes = 4,
                createTime = 900,
                discovererAllianceId = allianceId,
                discovererUid = "discoverer",
                workState = 1,
                userCount = 2,
            }, JsonOptions.Default);
            store.UpsertRecord(new MapStoredRecord(
                "treasure",
                ServerId,
                pointIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
                pointIndex,
                uuid,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                1_000,
                json));
        }

        SeedTreasure(101, "ordinary-101", 7, 0, AllianceId, 10_000);
        SeedTreasure(102, "supplies-102", 0, 9, AllianceId, 20_000);

        var calls = new List<(int ServerId, bool RefreshDetails, string[] Uuids)>();
        Task<CurrentClientTreasureInspectionResult> Inspector(
            int serverId,
            IReadOnlyList<CurrentClientTreasureInspectionRecord> records,
            bool refreshDetails,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (records.Any(record =>
                    record.ViewerUid != PlayerUid ||
                    record.ViewerAllianceId != AllianceId ||
                    record.ViewerHasReward != false ||
                    record.ViewerIsWorking != false))
                throw new InvalidOperationException(
                    "Treasure inspector projection lost persisted viewer-relative identity/state.");
            calls.Add((serverId, refreshDetails, records.Select(record => record.Uuid).ToArray()));
            JsonElement[] states = records.Select(record =>
                record.TreasureType > 0
                    ? JsonSerializer.SerializeToElement(new
                    {
                        uuid = record.Uuid,
                        worldClaimState = "claimable",
                        playerClaimState = "unclaimed",
                        claimBlockReason = (string?)null,
                        rewardedCount = 2,
                        diggingCount = 1,
                        remainingBoxes = 3,
                        expireTime = record.ExpireTime,
                    }, JsonOptions.Default)
                    : JsonSerializer.SerializeToElement(new
                    {
                        uuid = record.Uuid,
                        worldClaimState = "charging",
                        playerClaimState = "digging",
                        claimBlockReason = (string?)null,
                        rewardedCount = 1,
                        diggingCount = 2,
                        remainingBoxes = 4,
                        expireTime = record.ExpireTime,
                        chargePercent = 0.5,
                    }, JsonOptions.Default))
                .ToArray();
            return Task.FromResult(new CurrentClientTreasureInspectionResult(
                PlayerUid,
                AllianceId,
                states));
        }

        var service = new ManualMapScanCommandService(
            store,
            _ => Task.FromResult(StandardContext()),
            new ImmediateSource(),
            getLiveServerId: () => ServerId,
            inspectTreasureStates: Inspector);

        Check(service.CanHandle("map_treasure_state_refresh") &&
              service.CanHandle("map_treasure_state_refresh_all") &&
              service.CanHandle("map_treasure_claim_status"),
            "production async service should expose all three recovered read-only Treasure state commands");

        JsonElement pagePayload = JsonSerializer.SerializeToElement(new
        {
            profileId = "default",
            serverId = ServerId,
            records = new object[] { new { uuid = "ordinary-101" } },
        }, JsonOptions.Default);
        JsonElement pageResult = JsonSerializer.SerializeToElement(
            await service.InvokeAsync(
                "map_treasure_state_refresh",
                pagePayload,
                CancellationToken.None),
            JsonOptions.Default);
        Check(pageResult.GetProperty("playerUid").GetString() == PlayerUid &&
              pageResult.GetProperty("allianceId").GetString() == AllianceId &&
              pageResult.GetProperty("states").GetArrayLength() == 1 &&
              pageResult.GetProperty("states")[0].GetProperty("uuid").GetString() == "ordinary-101",
            "page Treasure refresh should return authoritative viewer identity and exactly the requested persisted row state");
        Check(calls.Count == 1 && calls[0].ServerId == ServerId &&
              calls[0].RefreshDetails &&
              calls[0].Uuids.SequenceEqual(new[] { "ordinary-101" }),
            "page Treasure refresh should project only bounded published-row identity into the live inspector");

        MapTreasureClaimState? cached =
            store.ReadTreasureClaimStateForTest(ServerId, PlayerUid, "ordinary-101");
        Check(cached is not null &&
              cached.ExpireTime == 10_000 &&
              cached.StateJson.Contains("\"worldClaimState\":\"claimable\"", StringComparison.Ordinal) &&
              !cached.StateJson.Contains("claimPriority", StringComparison.Ordinal),
            "page Treasure refresh should persist the proven state without synthesizing lucky priority");

        calls.Clear();
        JsonElement allPayload = JsonSerializer.SerializeToElement(
            new { profileId = "default", serverId = ServerId },
            JsonOptions.Default);
        JsonElement allResult = JsonSerializer.SerializeToElement(
            await service.InvokeAsync(
                "map_treasure_state_refresh_all",
                allPayload,
                CancellationToken.None),
            JsonOptions.Default);
        Check(allResult.GetProperty("states").GetArrayLength() == 2 &&
              calls.Count == 1 &&
              calls[0].RefreshDetails &&
              calls[0].Uuids.SequenceEqual(new[] { "ordinary-101", "supplies-102" }),
            "refresh-all should inspect the deterministic published Treasure set in record-key order");
        MapTreasureClaimState? suppliesCached =
            store.ReadTreasureClaimStateForTest(ServerId, PlayerUid, "supplies-102");
        Check(suppliesCached is not null &&
              suppliesCached.StateJson.Contains("\"chargePercent\":0.5", StringComparison.Ordinal),
            "refresh-all should persist Supplies-specific read-only state fields");

        calls.Clear();
        JsonElement statusPayload = JsonSerializer.SerializeToElement(
            new { profileId = "default" },
            JsonOptions.Default);
        JsonElement statusResult = JsonSerializer.SerializeToElement(
            await service.InvokeAsync(
                "map_treasure_claim_status",
                statusPayload,
                CancellationToken.None),
            JsonOptions.Default);
        Check(statusResult.GetProperty("playerUid").GetString() == PlayerUid &&
              statusResult.GetProperty("allianceId").GetString() == AllianceId &&
              statusResult.GetProperty("states").GetArrayLength() == 0 &&
              calls.Count == 1 &&
              !calls[0].RefreshDetails &&
              calls[0].Uuids.Length == 0,
            "claim-status should be identity-only and must not inspect or mutate any Treasure row");

        try
        {
            _ = await service.InvokeAsync(
                "map_treasure_state_refresh",
                JsonSerializer.SerializeToElement(new
                {
                    profileId = "default",
                    serverId = ServerId,
                    records = new object[] { new { uuid = "not-published" } },
                }, JsonOptions.Default),
                CancellationToken.None);
            throw new InvalidOperationException("expected unpublished Treasure UUID rejection");
        }
        catch (BridgeCommandException error) when (
            error.Code == "INVALID_REQUEST" &&
            error.Message == "Treasure state records must uniquely match published rows.")
        {
        }

        try
        {
            _ = await service.InvokeAsync(
                "map_treasure_state_refresh_all",
                JsonSerializer.SerializeToElement(
                    new { profileId = "default", serverId = 2213 },
                    JsonOptions.Default),
                CancellationToken.None);
            throw new InvalidOperationException("expected Treasure server mismatch");
        }
        catch (BridgeCommandException error) when (error.Code == "SERVER_MISMATCH")
        {
        }

        var readingField = typeof(ManualMapScanCommandService).GetField(
            "isReading",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new MissingFieldException("ManualMapScanCommandService.isReading");
        readingField.SetValue(service, true);
        try
        {
            _ = await service.InvokeAsync(
                "map_treasure_state_refresh_all",
                allPayload,
                CancellationToken.None);
            throw new InvalidOperationException("expected active-scan Treasure refresh rejection");
        }
        catch (BridgeCommandException error) when (
            error.Code == "SCAN_RUNNING" &&
            error.Message == "stop the map scan first")
        {
        }
        finally
        {
            readingField.SetValue(service, false);
        }

        Check(calls.Count == 1,
            "invalid UUID, server mismatch and active-scan gates must fail before invoking the live Treasure inspector");

        // Deterministically prove the production source remains read-only: the
        // Treasure lane may issue only the official detail request. It must not
        // contain claim, march, scout or reward-fetch action calls.
        string repoRoot = FindRepoRootForChecks();
        string probeSource = File.ReadAllText(Path.Combine(repoRoot, "tools", "current_live_resource_probe.lua"));
        int laneStart = probeSource.IndexOf(
            "function M._treasureStateRuntime.read_request",
            StringComparison.Ordinal);
        int laneEnd = probeSource.IndexOf(
            "local function read_aoi_diagnostic",
            laneStart,
            StringComparison.Ordinal);
        Check(laneStart >= 0 && laneEnd > laneStart,
            "Treasure state Lua lane should remain identifiable for deterministic safety inspection");
        string lane = probeSource[laneStart..laneEnd];
        Check(lane.Contains("WorldGetSuppliesPointDetail", StringComparison.Ordinal) &&
              !lane.Contains("DetectEventClaimTreasure", StringComparison.Ordinal) &&
              !lane.Contains("LaunchScout", StringComparison.Ordinal) &&
              !lane.Contains("OnClickStartMarch", StringComparison.Ordinal) &&
              !lane.Contains("Fetch", StringComparison.Ordinal) &&
              !lane.Contains("ClaimTreasure", StringComparison.Ordinal),
            "Treasure state Lua lane must stay read-only except for the official Supplies detail request");

        service.Close();
    }

    private static async Task ZombieBossMixedTypesFailClosed()
    {
        using MapDataStore store = MapDataStore.CreateInMemory();
        int contextCalls = 0;
        var service = new ManualMapScanCommandService(
            store,
            _ =>
            {
                contextCalls++;
                return Task.FromResult(Context());
            },
            new ImmediateSource());
        try
        {
            _ = await service.InvokeAsync(
                "map_scan_start", Payload("normal", "monster", "zombie_boss"), CancellationToken.None);
            throw new InvalidOperationException("expected dedicated Zombie Boss mixed-type rejection");
        }
        catch (BridgeCommandException error) when (error.Code == "LIVE_BLOCK_TYPES_UNSUPPORTED")
        {
        }
        Check(contextCalls == 0, "mixed dedicated Zombie Boss scan must fail before live-context acquisition");
        service.Close();
    }

    private static void ScheduledPlunderCommandsAreRetired()
    {
        using MapDataStore store = MapDataStore.CreateInMemory();
        var service = new ManualMapScanCommandService(
            store,
            _ => Task.FromResult(Context()),
            new ImmediateSource());
        Check(!service.CanHandle("map_dispatch_plunder_schedule") &&
              !service.CanHandle("map_dispatch_plunder_cancel") &&
              !service.CanHandle("map_truck_plunder_schedule"),
            "owner-retired Scheduled Plunder commands must not be handled by the production Map service");
        IReadOnlyDictionary<string, string> schema = store.ReadSchemaDefinitions();
        Check(!schema.ContainsKey("dispatch_plunder_jobs") &&
              !schema.ContainsKey("truck_plunder_jobs") &&
              !schema.ContainsKey("truck_plunder_history"),
            "owner-retired Scheduled Plunder tables must not exist in a newly initialized Map store");
        service.Close();
    }

    private static string FindRepoRootForChecks()
    {
        DirectoryInfo? current = new(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "task.md")) &&
                Directory.Exists(Path.Combine(current.FullName, "src", "LWBridge.Desktop")))
                return current.FullName;
            current = current.Parent;
        }
        throw new InvalidOperationException(
            "Could not locate repository root for Manual Map Scan deterministic checks.");
    }

    private static void WaitForPhase(
        ManualMapScanCommandService service,
        string expected)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            if (String(Status(service), "phase") == expected) return;
            Thread.Sleep(10);
        }
        throw new TimeoutException($"manual scan status did not reach '{expected}'");
    }

    private static JsonElement Status(ManualMapScanCommandService service) =>
        JsonSerializer.SerializeToElement(service.CreateStatus(), JsonOptions.Default);

    private static string String(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static int Int(JsonElement root, string name) =>
        root.GetProperty(name).GetInt32();

    private static double Double(JsonElement root, string name) =>
        root.GetProperty(name).GetDouble();

    private static bool Bool(JsonElement root, string name) =>
        root.GetProperty(name).GetBoolean();

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class ImmediateSource : IMapScanBatchSource
    {
        public int Calls { get; private set; }
        public IReadOnlyList<string> LastSelectedTypes { get; private set; } = Array.Empty<string>();

        public Task<MapScanBlockCapture> CaptureAsync(
            MapScanExecutionRequest request,
            MapScanTargetBlock block,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            LastSelectedTypes = request.SelectedTypes.ToArray();
            return Task.FromResult(new MapScanBlockCapture(
                request.ServerId,
                request.WorldId,
                block.BlockIndex,
                "{}",
                []));
        }

        public Task<IReadOnlyList<MapScanBlockCapture>> CaptureBatchAsync(
            MapScanExecutionRequest request,
            MapScanTargetBlock seedBlock,
            IReadOnlySet<int> pendingBlockIndices,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            LastSelectedTypes = request.SelectedTypes.ToArray();
            if (!string.Equals(request.ScanMode, "fast", StringComparison.Ordinal))
            {
                return Task.FromResult<IReadOnlyList<MapScanBlockCapture>>(
                [
                    new MapScanBlockCapture(
                        request.ServerId,
                        request.WorldId,
                        seedBlock.BlockIndex,
                        "{}",
                        [])
                ]);
            }

            MapScanBlockCapture[] captures = MapScanTraversal.Build(
                    request.TileWidth,
                    request.TileHeight)
                .Where(block => pendingBlockIndices.Contains(block.BlockIndex))
                .Select(block => new MapScanBlockCapture(
                    request.ServerId,
                    request.WorldId,
                    block.BlockIndex,
                    "{}",
                    []))
                .ToArray();
            return Task.FromResult<IReadOnlyList<MapScanBlockCapture>>(captures);
        }
    }

    private sealed class SequencedGateSource : IMapScanBlockSource
    {
        private readonly object sync = new();
        private readonly Dictionary<int, TaskCompletionSource<bool>> releases = new();
        private int calls;

        public int Calls
        {
            get
            {
                lock (sync) return calls;
            }
        }

        public void Release(int callNumber)
        {
            TaskCompletionSource<bool> release;
            lock (sync)
            {
                if (!releases.TryGetValue(callNumber, out release!))
                    throw new InvalidOperationException(
                        $"capture call {callNumber} has not entered");
            }
            release.TrySetResult(true);
        }

        public async Task<MapScanBlockCapture> CaptureAsync(
            MapScanExecutionRequest request,
            MapScanTargetBlock block,
            CancellationToken cancellationToken)
        {
            var release = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            int callNumber;
            lock (sync)
            {
                callNumber = ++calls;
                releases.Add(callNumber, release);
            }

            await release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new MapScanBlockCapture(
                request.ServerId,
                request.WorldId,
                block.BlockIndex,
                $"{{\"call\":{callNumber}}}",
                []);
        }
    }

    private sealed class BlockingSource : IMapScanBlockSource
    {
        private readonly TaskCompletionSource<MapScanBlockCapture> completion = new();
        public TaskCompletionSource<bool> Entered { get; } = new();

        public async Task<MapScanBlockCapture> CaptureAsync(
            MapScanExecutionRequest request,
            MapScanTargetBlock block,
            CancellationToken cancellationToken)
        {
            Entered.TrySetResult(true);
            using CancellationTokenRegistration registration = cancellationToken.Register(
                () => completion.TrySetCanceled(cancellationToken));
            return await completion.Task.ConfigureAwait(false);
        }
    }
}
