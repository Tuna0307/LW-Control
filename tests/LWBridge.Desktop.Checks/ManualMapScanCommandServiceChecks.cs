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
        await BackendSummaryTracksActiveManualScan();
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
        await ZombieBossMixedTypesFailClosed();
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

        try
        {
            _ = await service.InvokeAsync(
                "map_scan_start",
                Payload("normal", "resource"),
                CancellationToken.None);
            throw new InvalidOperationException("expected duplicate Start rejection");
        }
        catch (BridgeCommandException error) when (
            error.Code == MapScanStartOwnership.ActiveScanErrorCode &&
            error.Message == MapScanStartOwnership.ActiveScanErrorMessage)
        {
        }

        object? stop = await service.InvokeAsync(
            "map_scan_stop",
            Payload("normal", "resource"),
            CancellationToken.None);
        status = JsonSerializer.SerializeToElement(stop, JsonOptions.Default);
        Check(!Bool(status, "isReading") && String(status, "phase") == "idle" && Int(status, "inflightBlocks") == 0,
            "one Stop response should be terminal and release scan ownership");
        Check(observedPhases.Contains("cancelling") && observedPhases.Contains("idle"),
            "scan status notifications should expose cancelling and terminal idle states");
        Check(contextCalls == 1, "duplicate Start must not reacquire live context");
        service.Close();
    }

    private static async Task BackendSummaryTracksActiveManualScan()
    {
        using MapDataStore store = MapDataStore.CreateInMemory();
        var source = new BlockingSource();
        var service = new ManualMapScanCommandService(
            store,
            _ => Task.FromResult(Context()),
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

        Check(String(status, "phase") == "completed" && !Bool(status, "isReading"),
            "fast scan should publish after all blocks succeed");
        Check(Int(status, "concurrency") == 20, "fast Start must preserve recovered concurrency 20");
        Check(Int(status, "totalBlocks") == 2 && Int(status, "readBlocks") == 2,
            "40x20 map should traverse two recovered 20-tile blocks");
        Check(Double(status, "progressPercent") == 100.0,
            "completed fast scan should expose recovered 100 percent state");
        Check(source.Calls == 2, "fast scan must use the same block source for each traversal block");
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
        var service = new ManualMapScanCommandService(store, _ => Task.FromResult(Context()), source);
        _ = await service.InvokeAsync("map_scan_start", Payload("normal", "zombie_boss"), CancellationToken.None);
        WaitForPhase(service, "completed");
        JsonElement status = Status(service);
        Check(Int(status, "readBlocks") == 1 && Int(status, "failedBlocks") == 0,
            "Zombie Boss should use the same ordinary Manual Scan worker as supported kinds");
        service.Close();
    }

    private static async Task RailwayTypeIsAccepted()
    {
        using MapDataStore store = MapDataStore.CreateInMemory();
        var source = new ImmediateSource();
        var service = new ManualMapScanCommandService(store, _ => Task.FromResult(Context()), source);
        _ = await service.InvokeAsync("map_scan_start", Payload("normal", "railway"), CancellationToken.None);
        WaitForPhase(service, "completed");
        JsonElement status = Status(service);
        Check(Int(status, "readBlocks") == 1 && Int(status, "failedBlocks") == 0,
            "Railway/Train should use the same ordinary Manual Scan worker as supported kinds");
        service.Close();
    }

    private static async Task DispatchTypeIsAccepted()
    {
        using MapDataStore store = MapDataStore.CreateInMemory();
        var source = new ImmediateSource();
        var service = new ManualMapScanCommandService(store, _ => Task.FromResult(Context()), source);
        _ = await service.InvokeAsync("map_scan_start", Payload("normal", "dispatch"), CancellationToken.None);
        WaitForPhase(service, "completed");
        JsonElement status = Status(service);
        Check(Int(status, "readBlocks") == 1 && Int(status, "failedBlocks") == 0,
            "Dispatch should use the same ordinary Manual Scan worker as supported kinds");
        service.Close();
    }

    private static async Task GhostTypeIsAccepted()
    {
        using MapDataStore store = MapDataStore.CreateInMemory();
        var source = new ImmediateSource();
        var service = new ManualMapScanCommandService(store, _ => Task.FromResult(Context()), source);
        _ = await service.InvokeAsync("map_scan_start", Payload("normal", "ghost"), CancellationToken.None);
        WaitForPhase(service, "completed");
        JsonElement status = Status(service);
        Check(Int(status, "readBlocks") == 1 && Int(status, "failedBlocks") == 0,
            "Ghost Ops should use the same ordinary Manual Scan worker as supported kinds");
        service.Close();
    }

    private static async Task TreasureTypeIsAccepted()
    {
        using MapDataStore store = MapDataStore.CreateInMemory();
        var source = new ImmediateSource();
        var service = new ManualMapScanCommandService(store, _ => Task.FromResult(Context()), source);
        _ = await service.InvokeAsync("map_scan_start", Payload("normal", "treasure"), CancellationToken.None);
        WaitForPhase(service, "completed");
        JsonElement status = Status(service);
        Check(Int(status, "readBlocks") == 1 && Int(status, "failedBlocks") == 0,
            "Treasure should use the same ordinary Manual Scan worker as supported kinds");
        service.Close();
    }

    private static async Task MixedTypesAreAccepted()
    {
        using MapDataStore store = MapDataStore.CreateInMemory();
        var source = new ImmediateSource();
        var service = new ManualMapScanCommandService(store, _ => Task.FromResult(Context()), source);
        _ = await service.InvokeAsync(
            "map_scan_start", Payload("normal", "city", "resource"), CancellationToken.None);
        WaitForPhase(service, "completed");
        JsonElement status = Status(service);
        Check(Int(status, "readBlocks") == 1 && Int(status, "failedBlocks") == 0 &&
              status.GetProperty("selectedTypes").EnumerateArray().Select(value => value.GetString()).SequenceEqual(new[] { "city", "resource" }) &&
              source.LastSelectedTypes.SequenceEqual(new[] { "city", "resource" }),
            "mixed original Map Data kinds should flow unchanged through the shared Manual Scan worker");
        service.Close();
    }

    private static async Task AllEightTypesAreAccepted()
    {
        using MapDataStore store = MapDataStore.CreateInMemory();
        var source = new ImmediateSource();
        var service = new ManualMapScanCommandService(store, _ => Task.FromResult(Context()), source);
        _ = await service.InvokeAsync(
            "map_scan_start", Payload("normal", MapScanContract.RecoveredDefaultTypes), CancellationToken.None);
        WaitForPhase(service, "completed");
        JsonElement status = Status(service);
        Check(Int(status, "readBlocks") == 1 && Int(status, "failedBlocks") == 0 &&
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
        _ = Status(freshStatusService);
        Check(heartbeatReads == 1,
            "known idle Map Scan status must not repeatedly overwrite server ownership from heartbeat snapshots");
        freshStatusService.Close();
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

    private sealed class ImmediateSource : IMapScanBlockSource
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
