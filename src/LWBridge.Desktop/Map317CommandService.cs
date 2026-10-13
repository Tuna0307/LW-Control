using System.Text.Json;
using Map317 = LWBridge.Map317;

namespace LWBridge.Desktop;

internal sealed record Map317CityExportRequest(
    Map317.MapQuery Query,
    Map317.CityExportOptions Options,
    string DefaultFileName);

internal sealed class Map317CommandService : INativeAsyncCommandService, IDisposable
{
    private static readonly HashSet<string> Commands = new(StringComparer.Ordinal)
    {
        "map_scan_start", "map_scan_status", "map_scan_stop", "map_scan_clear",
        "map_summary", "map_data_options", "map_search", "map_city_export",
        "map_saved_servers",
        "map_coordinate_jump", "map_march_follow", "map_player_mark_set", "server_jump",
        "server_jump_history_get", "server_jump_history_set", "server_jump_history_import",
        "map_treasure_state_refresh", "map_treasure_state_refresh_all",
        "map_treasure_claim", "map_treasure_claim_status", "map_treasure_claim_preview",
        "map_ghost_plunder_prepare",
        "map_dispatch_share_alliance", "map_plunder_jobs_list",
        "map_dispatch_plunder_schedule", "map_dispatch_plunder_cancel", "map_dispatch_plunder_clear",
        "map_dispatch_plunder_list", "map_dispatch_plunder_retry",
        "map_truck_plunder_schedule", "map_truck_plunder_cancel", "map_truck_plunder_clear",
        "map_truck_plunder_list", "map_truck_plunder_retry",
        "game_asset_image", "map_train_list_coverage",
    };

    private readonly Map317.MapStore store;
    private readonly CurrentClientMapBlockSource? currentSource;
    // Inert actual-handler tests may defer the external world-state request without a game.
    private readonly Func<CancellationToken, Task<CurrentClientMapStatusContext>>? statusReader;
    private long statusReadSerial;
    private readonly IMap317RunScopedProvider? scanRunProvider;
    private readonly Map317.MapControlPlane control;
    private readonly Map317.MapActionControlPlane actions;
    private readonly Map317.MapPlunderWorker plunderWorker;
    private readonly CancellationTokenSource workerCancellation = new();
    private readonly Task dispatchWorkerTask;
    private readonly Task truckWorkerTask;
    private readonly object scanLeaseGate = new();
    private readonly SemaphoreSlim scanTransitionGate = new(1, 1);
    private Func<bool>? isAutoScanCycleBusy;
    private Func<IDisposable>? acquireAutoClearLease;
    private Map317ScanProcessLease? activeScanLease;
    private string? activeScanRunId;
    private int disposed;

    internal Map317CommandService(
        string databasePath,
        OverviewLifecycleService lifecycle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        ArgumentNullException.ThrowIfNull(lifecycle);
        store = new Map317.MapStore(databasePath);
        currentSource = new CurrentClientMapBlockSource(lifecycle);
        statusReader = currentSource.GetMapStatusContextAsync;
        scanRunProvider = new CurrentClientMap317ScanProvider(currentSource);
        control = new Map317.MapControlPlane(store, scanRunProvider);
        using (Map317ScanProcessLease? startupLease = Map317ScanProcessLease.TryAcquire(store.DatabasePath))
        {
            if (startupLease is not null)
                store.ReconcileInterruptedScans(
                    Map317ScanProcessLease.InterruptedError,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }
        scanRunProvider.RunTerminated += ReleaseScanLease;
        var actionProvider = new CurrentClientMap317ActionProvider(currentSource);
        actions = new Map317.MapActionControlPlane(store, actionProvider);
        plunderWorker = new Map317.MapPlunderWorker(store, actionProvider);

        control.ScanStateChanged += (_, args) => ScanStatusChanged?.Invoke(ApplyStatusOverlay(args.State));
        control.PlayerMarkChanged += (_, _) => PlayerMarkChanged?.Invoke();
        actions.DispatchPlunderChanged += (_, _) => DispatchPlunderChanged?.Invoke();
        actions.TruckPlunderChanged += (_, _) => TruckPlunderChanged?.Invoke();
        plunderWorker.DispatchPlunderChanged += (_, _) => DispatchPlunderChanged?.Invoke();
        plunderWorker.TruckPlunderChanged += (_, _) => TruckPlunderChanged?.Invoke();

        plunderWorker.RecoverAfterRestart();
        dispatchWorkerTask = Task.Run(() => RunPlunderWorkerAsync(
            plunderWorker.RunDispatchOnceAsync, workerCancellation.Token));
        truckWorkerTask = Task.Run(() => RunPlunderWorkerAsync(
            plunderWorker.RunTruckOnceAsync, workerCancellation.Token));
    }

    /// <summary>
    /// Explicit isolated composition used by deterministic desktop integration proof.
    /// It keeps the real Map317 command/control/store code while substituting only the
    /// external game provider boundaries. No product path calls this constructor.
    /// </summary>
    internal Map317CommandService(
        string databasePath,
        Map317.IMapProvider mapProvider,
        Map317.IMapActionProvider actionProvider,
        bool startPlunderWorkers,
        Func<CancellationToken, Task<CurrentClientMapStatusContext>>? statusReader = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        ArgumentNullException.ThrowIfNull(mapProvider);
        ArgumentNullException.ThrowIfNull(actionProvider);
        store = new Map317.MapStore(databasePath);
        currentSource = null;
        this.statusReader = statusReader;
        scanRunProvider = mapProvider as IMap317RunScopedProvider;
        control = new Map317.MapControlPlane(store, mapProvider);
        using (Map317ScanProcessLease? startupLease = Map317ScanProcessLease.TryAcquire(store.DatabasePath))
        {
            if (startupLease is not null)
                store.ReconcileInterruptedScans(
                    Map317ScanProcessLease.InterruptedError,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }
        actions = new Map317.MapActionControlPlane(store, actionProvider);
        plunderWorker = new Map317.MapPlunderWorker(store, actionProvider);
        if (scanRunProvider is not null)
            scanRunProvider.RunTerminated += ReleaseScanLease;

        control.ScanStateChanged += (_, args) => ScanStatusChanged?.Invoke(ApplyStatusOverlay(args.State));
        control.PlayerMarkChanged += (_, _) => PlayerMarkChanged?.Invoke();
        actions.DispatchPlunderChanged += (_, _) => DispatchPlunderChanged?.Invoke();
        actions.TruckPlunderChanged += (_, _) => TruckPlunderChanged?.Invoke();
        plunderWorker.DispatchPlunderChanged += (_, _) => DispatchPlunderChanged?.Invoke();
        plunderWorker.TruckPlunderChanged += (_, _) => TruckPlunderChanged?.Invoke();

        plunderWorker.RecoverAfterRestart();
        dispatchWorkerTask = startPlunderWorkers
            ? Task.Run(() => RunPlunderWorkerAsync(plunderWorker.RunDispatchOnceAsync, workerCancellation.Token))
            : Task.CompletedTask;
        truckWorkerTask = startPlunderWorkers
            ? Task.Run(() => RunPlunderWorkerAsync(plunderWorker.RunTruckOnceAsync, workerCancellation.Token))
            : Task.CompletedTask;
    }

    public event Action<object>? ScanStatusChanged;
    public event Action? PlayerMarkChanged;
    public event Action? DispatchPlunderChanged;
    public event Action? TruckPlunderChanged;

    internal ResourceCompletenessReport? LastResourceCompletenessReport =>
        currentSource?.LastResourceCompletenessReport;

    // Bound after the Auto service is constructed for the same profile owner.
    // No cross-profile busy signals are consulted or mutated here.
    internal void BindAutoScanCycleBusy(Func<bool> isBusy) =>
        isAutoScanCycleBusy = isBusy ?? throw new ArgumentNullException(nameof(isBusy));

    internal void BindAutoScanClearLease(Func<IDisposable> acquireLease) =>
        acquireAutoClearLease = acquireLease ?? throw new ArgumentNullException(nameof(acquireLease));

    public bool CanHandle(string command) => Commands.Contains(command);

    internal object CreateStatus() => ApplyStatusOverlay(control.ScanState);

    internal bool IsScanActive => control.ScanState.IsReading;

    internal async Task<MapAutoScanRuntimeStatus> ReadAutoScanStatusAsync(
        string? scanRunId,
        CancellationToken cancellationToken)
    {
        Map317.MapScanState state = await ReadScanStatusAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(scanRunId) ||
            string.Equals(state.ScanRunId, scanRunId, StringComparison.Ordinal))
        {
            return new MapAutoScanRuntimeStatus(
                state.ServerId,
                state.IsReading,
                state.Error,
                string.IsNullOrWhiteSpace(state.ScanRunId) ? null : state.ScanRunId,
                state.Phase);
        }

        Map317.MapScanRun? ownedRun = store.ReadScanRun(scanRunId);
        if (ownedRun is null)
            return new MapAutoScanRuntimeStatus(0, false, "Auto scan owner run is unavailable", scanRunId, "missing");
        return new MapAutoScanRuntimeStatus(
            ownedRun.ServerId,
            string.Equals(ownedRun.Status, "running", StringComparison.Ordinal),
            ownedRun.Error,
            ownedRun.Id,
            ownedRun.Status);
    }

    internal async Task<string> StartAutoScanTargetAsync(
        int serverId,
        IReadOnlyList<string> selectedTypes,
        string scanMode,
        CancellationToken cancellationToken)
    {
        await scanTransitionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            AcquireScanLease();
            try
            {
                _ = await actions.ServerJumpAsync(serverId, cancellationToken).ConfigureAwait(false);
                JsonElement payload = JsonSerializer.SerializeToElement(new
                {
                    selectedTypes,
                    scanMode,
                    resume = false,
                }, JsonOptions.Default);
                Map317.MapScanState started =
                    await StartScanWithRegisteredLeaseAsync(payload, cancellationToken).ConfigureAwait(false);
                return started.ScanRunId;
            }
            catch
            {
                ReleaseScanLease();
                throw;
            }
        }
        finally
        {
            scanTransitionGate.Release();
        }
    }

    internal async Task ReturnAutoScanToServerAsync(int serverId, CancellationToken cancellationToken)
    {
        await scanTransitionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using Map317ScanProcessLease? lease = Map317ScanProcessLease.TryAcquire(store.DatabasePath);
            if (lease is null || control.ScanState.IsReading)
                throw new BridgeCommandException("SCAN_RUNNING", "map scan already running");
            lock (scanLeaseGate)
            {
                if (activeScanLease is not null)
                    throw new BridgeCommandException("SCAN_RUNNING", "map scan already running");
            }
            _ = await actions.ServerJumpAsync(serverId, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            scanTransitionGate.Release();
        }
    }

    internal async Task<bool> StopAutoScanIfOwnedAsync(
        string scanRunId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(scanRunId)) return false;
        await scanTransitionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Map317.MapScanState current = control.ScanState;
            if (!current.IsReading ||
                !string.Equals(current.ScanRunId, scanRunId, StringComparison.Ordinal))
                return false;
            Map317.MapScanState stopped = await control.StopScanAsync(cancellationToken).ConfigureAwait(false);
            if (scanRunProvider is null && !stopped.IsReading)
                ReleaseScanLease(scanRunId);
            return true;
        }
        finally
        {
            scanTransitionGate.Release();
        }
    }

    public async Task<object?> InvokeAsync(
        string command,
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        try
        {
            switch (command)
            {
                case "game_asset_image":
                    return await ReadAssetImageAsync(payload, cancellationToken).ConfigureAwait(false);
                case "map_train_list_coverage":
                    return await ReadTrainListCoverageAsync(cancellationToken).ConfigureAwait(false);
                case "map_scan_start":
                    return await StartScanAsync(payload, cancellationToken).ConfigureAwait(false);
                case "map_scan_status":
                    return await ReadScanStatusAsync(cancellationToken).ConfigureAwait(false);
                case "map_scan_stop":
                    return await StopScanAsync(cancellationToken).ConfigureAwait(false);
                case "map_scan_clear":
                    return await ClearScanAsync(
                        RequiredInt(payload, "serverId"),
                        payload.TryGetProperty("clearSavedServer", out var savedClear) &&
                        savedClear.ValueKind == JsonValueKind.True,
                        cancellationToken).ConfigureAwait(false);
                case "map_summary":
                    {
                        Map317.MapScanState observedState = await ReadScanStatusAsync(cancellationToken).ConfigureAwait(false);
                        var summary = control.ReadSummary();
                        return new { serverId = summary.ServerId, counts = summary.Counts, scanState = observedState };
                    }
                case "map_data_options":
                    return control.ReadOptions(OptionsServerId(payload));
                case "map_saved_servers":
                    return new { servers = store.ReadPublishedServers() };
                case "map_search":
                    {
                        // Original 0.3.17 search never rejects a mistyped page/pageSize/server/kind:
                        // it coerces loosely and answers an EMPTY page when nothing is usable.
                        Map317.MapQuery? query = NormalizeOriginalSearch(payload);
                        return query is null
                            ? EmptyOriginalSearch(payload)
                            : control.Search(query);
                    }
                case "map_city_export":
                    throw new BridgeCommandException(
                        "NATIVE_DIALOG_REQUIRED", "City export requires the desktop save dialog host.");
                case "map_coordinate_jump":
                    return await actions.CoordinateJumpAsync(
                        RequiredInt(payload, "serverId"),
                        RequiredInt(payload, "x"),
                        RequiredInt(payload, "y"),
                        cancellationToken).ConfigureAwait(false);
                case "map_march_follow":
                    return await actions.MarchFollowAsync(
                        RequiredInt(payload, "serverId"),
                        RequiredString(payload, "marchUuid"),
                        cancellationToken).ConfigureAwait(false);
                case "server_jump":
                    // Original handler 0x182112: INVALID_SERVER_ID for any missing,
                    // non-integer or out-of-range id, with the original text.
                    return await actions.ServerJumpAsync(
                        Map317.MapActionControlPlane.RequireServerJumpServerId(payload),
                        cancellationToken).ConfigureAwait(false);
                case "map_player_mark_set":
                    return SetPlayerMark(payload);
                case "server_jump_history_get":
                    return actions.GetServerJumpHistory();
                case "server_jump_history_set":
                    return control.SetServerJumpHistory(ReadIntArray(payload, "history"));
                case "server_jump_history_import":
                    return control.ImportServerJumpHistory(ReadIntArray(payload, "history"));
                case "map_treasure_state_refresh":
                    return await actions.RefreshTreasureStatesAsync(
                        RequiredInt(payload, "serverId"), ReadRows(payload, "records"), cancellationToken)
                        .ConfigureAwait(false);
                case "map_treasure_state_refresh_all":
                    return await actions.RefreshAllTreasureStatesAsync(
                        RequiredInt(payload, "serverId"), cancellationToken).ConfigureAwait(false);
                case "map_treasure_claim_status":
                    return await actions.TreasureClaimStatusAsync(cancellationToken).ConfigureAwait(false);
                case "map_treasure_claim_preview":
                    {
                        TreasureClaimRequest preview = TreasureClaimContract.NormalizeRequest(payload);
                        return await actions.PreviewTreasureClaimAsync(
                            preview.ServerId, preview.ClaimScope, preview.PrioritizeLuckySlots,
                            preview.TargetUuid, cancellationToken).ConfigureAwait(false);
                    }
                case "map_ghost_plunder_prepare":
                    return await actions.PrepareGhostPlunderAsync(
                        ReadRows(payload, "rows"), cancellationToken).ConfigureAwait(false);
                case "map_treasure_claim":
                    {
                        // Original handler 0x18EE07: INVALID_SERVER_ID /
                        // INVALID_TREASURE_CLAIM_SCOPE before any game work.
                        TreasureClaimRequest claim = TreasureClaimContract.NormalizeRequest(payload);
                        return await actions.ClaimTreasuresAsync(
                            claim.ServerId, claim.ClaimScope, claim.PrioritizeLuckySlots,
                            claim.TargetUuid, cancellationToken).ConfigureAwait(false);
                    }
                case "map_dispatch_share_alliance":
                    return await actions.ShareDispatchToAllianceAsync(
                        ReadRows(payload, "rows"), cancellationToken).ConfigureAwait(false);
                case "map_plunder_jobs_list":
                    {
                        Map317.MapPlunderJobsSnapshot jobs =
                            await actions.ListPlunderJobsAsync(cancellationToken).ConfigureAwait(false);
                        return new { dispatchJobs = jobs.DispatchJobs, truckJobs = jobs.TruckJobs };
                    }
                case "map_dispatch_plunder_schedule":
                    return await actions.ScheduleDispatchPlunderAsync(
                        ReadRows(payload, "rows"), cancellationToken).ConfigureAwait(false);
                case "map_dispatch_plunder_cancel":
                    actions.CancelDispatchPlunder(
                        RequiredLong(payload, "serverId"), RequiredString(payload, "taskUuid"));
                    return null;
                case "map_dispatch_plunder_clear":
                    return actions.ClearDispatchPlunder(
                        RequiredLong(payload, "before"), OptionalString(payload, "taskKind"));
                case "map_dispatch_plunder_list":
                    return actions.ListDispatchPlunderJobs();
                case "map_dispatch_plunder_retry":
                    actions.RetryDispatchPlunder(
                        RequiredLong(payload, "serverId"), RequiredString(payload, "taskUuid"));
                    return null;
                case "map_truck_plunder_schedule":
                    return actions.ScheduleTruckPlunder(ReadRows(payload, "rows"));
                case "map_truck_plunder_cancel":
                    actions.CancelTruckPlunder(
                        RequiredLong(payload, "serverId"), RequiredString(payload, "trainUuid"));
                    return null;
                case "map_truck_plunder_clear":
                    return actions.ClearTruckPlunder(RequiredLong(payload, "before"));
                case "map_truck_plunder_list":
                    return actions.ListTruckPlunderJobs();
                case "map_truck_plunder_retry":
                    return actions.RetryTruckPlunder(
                        RequiredLong(payload, "serverId"), RequiredString(payload, "trainUuid"));
                default:
                    throw new BridgeCommandException(
                        "COMMAND_NOT_IMPLEMENTED", $"Map317 cannot handle '{command}'.");
            }
        }
        catch (Map317.BridgeCommandException error)
        {
            throw new BridgeCommandException(error.Code, error.Message, error.Details);
        }
    }

    private async Task<object> ReadAssetImageAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        if (currentSource is null)
            throw new BridgeCommandException("GAME_CONNECTION_UNAVAILABLE", "game connection unavailable");
        string? assetPath = OptionalString(payload, "assetPath")?.Trim();
        string? spriteName = OptionalString(payload, "spriteName")?.Trim();
        bool hasAssetPath = !string.IsNullOrEmpty(assetPath);
        bool hasSpriteName = !string.IsNullOrEmpty(spriteName);
        if (hasAssetPath == hasSpriteName)
            throw new BridgeCommandException("INVALID_REQUEST", "exactly one image source is required");

        CurrentClientAssetImageResult result = await currentSource.GetAssetImageAsync(
            hasAssetPath ? assetPath : null,
            hasSpriteName ? spriteName : null,
            cancellationToken).ConfigureAwait(false);
        return new { dataUrl = result.DataUrl };
    }

    private async Task<object> ReadTrainListCoverageAsync(CancellationToken cancellationToken)
    {
        if (currentSource is null)
            throw new BridgeCommandException("GAME_CONNECTION_UNAVAILABLE", "game connection unavailable");
        if (control.ScanState.IsReading)
            throw new BridgeCommandException("SCAN_RUNNING", "stop the map scan first");
        CurrentClientTrainListCoverageResult result =
            await currentSource.GetTrainListCoverageAsync(cancellationToken).ConfigureAwait(false);
        return new
        {
            liveServerId = result.LiveServerId,
            matchServerIds = result.MatchServerIds.Distinct().OrderBy(id => id).ToArray(),
            truckServerIds = result.TruckServerIds.ToArray(),
            railwayServerIds = result.RailwayServerIds.ToArray(),
        };
    }

    internal Map317CityExportRequest PrepareCityExport(
        JsonElement payload,
        DateTimeOffset? utcNow = null)
    {
        // Original 0x129FA7 order: headers (12 strings) -> labels -> query (missing = {}) ->
        // final export server: loose query.serverId, else the shared-state server, else
        // MAP_EXPORT_FAILED "city export server is unavailable".
        string[] headers = ReadHeaders(payload);
        System.Text.Json.Nodes.JsonObject queryObject =
            payload.TryGetProperty("query", out JsonElement query) && query.ValueKind == JsonValueKind.Object
                ? (System.Text.Json.Nodes.JsonObject)System.Text.Json.Nodes.JsonNode.Parse(query.GetRawText())!
                : new System.Text.Json.Nodes.JsonObject();
        long exportServer = LooseInt64(JsonSerializer.SerializeToElement(queryObject), "serverId") ?? 0;
        if (exportServer <= 0) exportServer = control.ScanState.ServerId;
        if (exportServer <= 0)
            throw new BridgeCommandException("MAP_EXPORT_FAILED", "city export server is unavailable");
        queryObject["serverId"] = (int)Math.Min(exportServer, int.MaxValue);
        int serverId = (int)Math.Min(exportServer, int.MaxValue);
        using JsonDocument envelope = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            kind = "city",
            query = queryObject,
        }));
        Map317.MapQuery normalized = NormalizeQuery(envelope.RootElement);
        string sheet = OptionalString(payload, "sheetName") ?? "Cities";
        string yes = OptionalString(payload, "yesLabel") ?? "Yes";
        string no = OptionalString(payload, "noLabel") ?? "No";
        return new Map317CityExportRequest(
            normalized,
            new Map317.CityExportOptions(headers, sheet, yes, no),
            Map317.MapExporter.DefaultCityFileName(serverId, utcNow ?? DateTimeOffset.UtcNow));
    }

    internal object WriteCityExport(Map317CityExportRequest request, string path)
    {
        try
        {
            return control.ExportCitiesToPath(request.Query, request.Options, path);
        }
        catch (Map317.BridgeCommandException error)
        {
            throw new BridgeCommandException(error.Code, error.Message, error.Details);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        workerCancellation.Cancel();
        try { Task.WhenAll(dispatchWorkerTask, truckWorkerTask).GetAwaiter().GetResult(); }
        catch (OperationCanceledException) { }
        if (scanRunProvider is not null)
        {
            scanRunProvider.Dispose();
            scanRunProvider.RunTerminated -= ReleaseScanLease;
        }
        ReleaseScanLease();
        control.Dispose();
        workerCancellation.Dispose();
        scanTransitionGate.Dispose();
    }

    private async Task<object> StartScanAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        await scanTransitionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            AcquireScanLease();
            return await StartScanWithRegisteredLeaseAsync(payload, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            scanTransitionGate.Release();
        }
    }

    private void AcquireScanLease()
    {
        Map317ScanProcessLease? lease = Map317ScanProcessLease.TryAcquire(store.DatabasePath);
        if (lease is null)
            throw new BridgeCommandException("SCAN_RUNNING", "map scan already running");
        lock (scanLeaseGate)
        {
            if (activeScanLease is not null)
            {
                lease.Dispose();
                throw new BridgeCommandException("SCAN_RUNNING", "map scan already running");
            }
            activeScanLease = lease;
        }
    }

    private async Task<Map317.MapScanState> StartScanWithRegisteredLeaseAsync(
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        try
        {
            store.ReconcileInterruptedScans(
                Map317ScanProcessLease.InterruptedError,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            string[]? types = null;
            if (payload.TryGetProperty("selectedTypes", out JsonElement selected) &&
                selected.ValueKind == JsonValueKind.Array)
                types = selected.EnumerateArray()
                    .Where(value => value.ValueKind == JsonValueKind.String)
                    .Select(value => value.GetString() ?? string.Empty).ToArray();
            string? mode = OptionalString(payload, "scanMode");
            bool resume = OptionalBool(payload, "resume", false);
            Map317.MapScanState state =
                await control.StartScanAsync(new Map317.MapScanStartRequest(types, mode, resume), cancellationToken)
                    .ConfigureAwait(false);
            if (scanRunProvider is null)
                throw new InvalidOperationException(
                    "An accepting Map provider must implement the run-scoped terminal lifetime contract.");
            RegisterActiveScanRun(state.ScanRunId);
            scanRunProvider.ActivateAcceptedRun(control, state.ScanRunId);
            return state;
        }
        catch (Exception error)
        {
            if (control.ScanState.IsReading)
                control.FailScan(error.Message);
            ReleaseScanLease();
            throw;
        }
    }

    private async Task<Map317.MapScanState> StopScanAsync(CancellationToken cancellationToken)
    {
        await scanTransitionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await control.StopScanAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            scanTransitionGate.Release();
        }
    }

    private sealed record ObservedStatus(Map317.MapScanState Captured, CurrentClientMapStatusContext Context);
    private ObservedStatus? lastStatusContext;

    private static bool SameStatusOwner(Map317.MapScanState a, Map317.MapScanState b) =>
        string.Equals(a.ScanRunId, b.ScanRunId, StringComparison.Ordinal) &&
        a.ServerId == b.ServerId && a.StartedAt == b.StartedAt && a.IsReading == b.IsReading;

    // Original status service (0xF8CB2; trigger 0xF9559-0xF975C): while the shared state is reading,
    // a live server id L > 0 (game not known to be outside the world) that differs from the
    // scan's positive server fails the run with this exact text. Not an error to the caller.
    internal const string ServerChangedDuringScanText = "current server changed during map scan";

    internal async Task<Map317.MapScanState> ApplyLiveServerGuardAsync(
        Map317.MapScanState state, bool liveInWorld, int liveServerId, CancellationToken cancellationToken)
    {
        if (state.IsReading && liveInWorld && liveServerId > 0 &&
            state.ServerId > 0 && state.ServerId != liveServerId)
            return await control.FailForServerChangeAsync(state, ServerChangedDuringScanText, cancellationToken)
                .ConfigureAwait(false);
        return state;
    }

    // The UI replaces its whole scan state on every bridge://map-scan-status event.
    // Events therefore carry the home/season/truck-match/world overlay last observed
    // by map_scan_status instead of resetting those fields to defaults until the next
    // poll. (The server id/source of a running scan is never overlaid on an event.)
    private Map317.MapScanState ApplyStatusOverlay(Map317.MapScanState state)
    {
        ObservedStatus? observation = Volatile.Read(ref lastStatusContext);
        if (observation is null || !SameStatusOwner(state, observation.Captured)) return state;
        CurrentClientMapStatusContext context = observation.Context;
        return state with
        {
            ServerId = !state.IsReading && context.ServerId > 0 ? context.ServerId : state.ServerId,
            ServerIdSource = !state.IsReading && context.ServerId > 0 ? "live" : state.ServerIdSource,
            IsInWorld = context.IsInWorld,
            HomeServerId = context.HomeServerId,
            SeasonServerIds = context.SeasonServerIds,
            TruckMatchServerIds = context.TruckMatchServerIds,
        };
    }

    private async Task<Map317.MapScanState> ReadScanStatusAsync(CancellationToken cancellationToken)
    {
        long requestSerial = Interlocked.Increment(ref statusReadSerial);
        Map317.MapScanState state = control.ScanState;
        if (!state.IsReading)
        {
            try
            {
                state = await control.RefreshContextAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Map317.BridgeCommandException)
            {
                state = control.ScanState;
            }
        }
        if (statusReader is null)
            return ApplyStatusOverlay(state);
        try
        {
            CurrentClientMapStatusContext context =
                await statusReader(cancellationToken).ConfigureAwait(false);
            // No stale reply (or failed previous poll) may apply a server overlay
            // to the successor's run, profile session or newer status observation.
            if (Volatile.Read(ref disposed) != 0 ||
                requestSerial != Volatile.Read(ref statusReadSerial) ||
                !SameStatusOwner(state, control.ScanState))
                return ApplyStatusOverlay(control.ScanState);

            Map317.MapScanState guarded = await ApplyLiveServerGuardAsync(
                state, context.IsInWorld, context.ServerId, cancellationToken).ConfigureAwait(false);
            Map317.MapScanState current = control.ScanState;
            // The guarded operation can wait behind Stop/Start; never publish
            // an old observation once a new run has acquired the control plane.
            if (Volatile.Read(ref disposed) != 0 ||
                requestSerial != Volatile.Read(ref statusReadSerial) ||
                !string.Equals(guarded.ScanRunId, current.ScanRunId, StringComparison.Ordinal) ||
                guarded.StartedAt != current.StartedAt || guarded.ServerId != current.ServerId)
                return ApplyStatusOverlay(current);
            ObservedStatus? previouslySeen = Volatile.Read(ref lastStatusContext);
            if (!context.TruckMatchesKnown && previouslySeen is not null &&
                SameStatusOwner(current, previouslySeen.Captured))
            {
                context = context with { TruckMatchServerIds = previouslySeen.Context.TruckMatchServerIds };
            }
            Volatile.Write(ref lastStatusContext, new ObservedStatus(current, context));
            return current with
            {
                ServerId = context.ServerId > 0 ? context.ServerId : current.ServerId,
                ServerIdSource = context.ServerId > 0 ? "live" : current.ServerIdSource,
                IsInWorld = context.IsInWorld,
                HomeServerId = context.HomeServerId,
                SeasonServerIds = context.SeasonServerIds,
                TruckMatchServerIds = context.TruckMatchServerIds,
            };
        }
        catch (BridgeCommandException)
        {
            // A failed/overlapped routine read must not momentarily erase valid
            // warzone buttons. The cached overlay is scoped to this very run;
            // a genuinely new successful observation still replaces the list.
            return ApplyStatusOverlay(control.ScanState);
        }
    }

    private async Task<Map317.MapScanState> ClearScanAsync(
        int requestedServerId,
        bool clearSavedServer,
        CancellationToken cancellationToken)
    {
        await scanTransitionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Reserve against Auto's cycleGate for the whole asynchronous Clear.
            // Auto cannot admit a new cycle between the busy check and deletion.
            using IDisposable? clearLease = acquireAutoClearLease?.Invoke();
            if (isAutoScanCycleBusy?.Invoke() == true)
                throw new BridgeCommandException("AUTO_SCAN_RUNNING", "Stop the Auto scan cycle before clearing Map data.");
            Map317.MapScanState current = control.ScanState;
            if (current.IsReading)
                throw new BridgeCommandException(
                    MapScanClearOwnership.ActiveScanErrorCode,
                    MapScanClearOwnership.ActiveScanErrorMessage);

            Map317.MapScanState fresh;
            try
            {
                // Clear is destructive. It must authorize against a fresh provider
                // observation at invocation time; retained browse/status context is
                // never sufficient to delete durable rows.
                fresh = await control.RefreshContextAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                throw new BridgeCommandException(
                    MapScanClearOwnership.ServerUnavailableErrorCode,
                    MapScanClearOwnership.ServerUnavailableErrorMessage);
            }

            // Auto's saved dataset may remain selected after its remote scan
            // returns the player to the home server. This explicitly requested
            // saved-row deletion needs fresh authenticated live readiness,
            // the same profile-owned published store, and an idle scan/Auto
            // lease. Leave the legacy same-live-server clear strict.
            if (clearSavedServer && requestedServerId != fresh.ServerId)
            {
                if (fresh.IsReading || fresh.ServerId <= 0 ||
                    fresh.ServerIdSource != MapScanClearOwnership.LiveServerSource ||
                    !store.ReadPublishedServers().Contains(requestedServerId))
                    throw new BridgeCommandException(
                        MapScanClearOwnership.ServerUnavailableErrorCode,
                        MapScanClearOwnership.ServerUnavailableErrorMessage);
                cancellationToken.ThrowIfCancellationRequested();
                store.ClearServer(requestedServerId);
                return ApplyStatusOverlay(control.ScanState);
            }
            MapScanClearOwnership.Validate(
                requestedServerId, fresh.IsReading, fresh.ServerId, fresh.ServerIdSource);
            return await control.ClearScanAsync(requestedServerId, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            scanTransitionGate.Release();
        }
    }

    private void RegisterActiveScanRun(string scanRunId)
    {
        if (string.IsNullOrWhiteSpace(scanRunId))
            throw new InvalidOperationException("Map317 accepted scan did not return a run id.");
        lock (scanLeaseGate)
        {
            if (activeScanLease is null)
                throw new InvalidOperationException("Map317 accepted scan has no owned process lease.");
            activeScanRunId = scanRunId;
        }
    }

    private void ReleaseScanLease(string? expectedRunId = null)
    {
        Map317ScanProcessLease? lease;
        lock (scanLeaseGate)
        {
            if (expectedRunId is not null &&
                !string.Equals(activeScanRunId, expectedRunId, StringComparison.Ordinal))
                return;
            lease = activeScanLease;
            activeScanLease = null;
            activeScanRunId = null;
        }
        lease?.Dispose();
    }

    private object SetPlayerMark(JsonElement payload)
    {
        if (!payload.TryGetProperty("row", out JsonElement row) ||
            row.ValueKind != JsonValueKind.Object)
            throw new BridgeCommandException("INVALID_PLAYER_MARK", "player mark row must be an object.");
        // Recovered 0.3.17 handler 0x151985 tests marked === true;
        // false, missing, null and other non-true values all remove the mark.
        bool marked = payload.TryGetProperty("marked", out JsonElement markedValue) &&
            markedValue.ValueKind == JsonValueKind.True;
        int serverId = RequiredInt(row, "serverId");
        string ownerUid = RequiredString(row, "ownerUid");
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        control.SetPlayerMark(
            new Map317.MapPlayerMark(serverId, ownerUid, "active", now, null, row.GetRawText()), marked);
        return new
        {
            serverId,
            ownerUid,
            marked,
            state = marked ? "active" : null,
            markedAt = marked ? now : (long?)null,
            checkedAt = (long?)null,
        };
    }

    // Rust i64::from_str: optional +/- then ASCII digits only (no spaces).
    private static long? LooseInt64(JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Number:
                if (value.TryGetInt64(out long exact)) return exact;
                double number = value.GetDouble();
                if (double.IsNaN(number)) return 0;
                if (number >= long.MaxValue) return long.MaxValue;
                if (number <= long.MinValue) return long.MinValue;
                return (long)Math.Truncate(number);
            case JsonValueKind.String:
                string? text = value.GetString();
                return text is { Length: > 0 } &&
                       !text.Any(char.IsWhiteSpace) &&
                       long.TryParse(text, System.Globalization.NumberStyles.AllowLeadingSign,
                           System.Globalization.CultureInfo.InvariantCulture, out long parsed)
                    ? parsed : null;
            default:
                return null;
        }
    }

    private static long? LooseInt64(JsonElement owner, string name) =>
        owner.ValueKind == JsonValueKind.Object && owner.TryGetProperty(name, out JsonElement value)
            ? LooseInt64(value) : null;

    // map_data_options: a non-positive/missing server is replaced by the shared-state server.
    private int OptionsServerId(JsonElement payload)
    {
        long requested = LooseInt64(payload, "serverId") ?? 0;
        if (requested <= 0) requested = control.ScanState.ServerId;
        return requested is >= 0 and <= int.MaxValue ? (int)requested : 0;
    }

    // The original Map handler returns the full native empty-page envelope,
    // even for unknown kinds/unavailable servers; it preserves coerced paging.
    private static Map317.MapSearchResult EmptyOriginalSearch(JsonElement payload)
    {
        JsonElement query = payload.ValueKind == JsonValueKind.Object &&
            payload.TryGetProperty("query", out var value) &&
            value.ValueKind == JsonValueKind.Object ? value : default;
        long? page = LooseInt64(query, "page");
        long? size = LooseInt64(query, "pageSize");
        return new Map317.MapSearchResult([], 0,
            Math.Max(page ?? 1L, 1L),
            (int)Math.Clamp(size ?? 50, 1, 200));
    }

    private static double? LooseFiniteDouble(JsonElement owner, string name)
    {
        if (owner.ValueKind != JsonValueKind.Object ||
            !owner.TryGetProperty(name, out JsonElement value))
            return null;
        double number;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out number) ||
            value.ValueKind == JsonValueKind.String &&
            double.TryParse(value.GetString(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out number))
            return double.IsFinite(number) ? number : null;
        return null;
    }

    private Map317.MapQuery? NormalizeOriginalSearch(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty("kind", out JsonElement kindValue) ||
            kindValue.ValueKind != JsonValueKind.String)
            return null;
        string kind = kindValue.GetString() ?? string.Empty;
        if (!MapScanContract.RecoveredDefaultTypes.Contains(kind, StringComparer.Ordinal))
            return null; // original swallows INVALID_MAP_KIND into an empty page
        System.Text.Json.Nodes.JsonObject query =
            payload.TryGetProperty("query", out JsonElement queryValue) && queryValue.ValueKind == JsonValueKind.Object
                ? (System.Text.Json.Nodes.JsonObject)System.Text.Json.Nodes.JsonNode.Parse(queryValue.GetRawText())!
                : new System.Text.Json.Nodes.JsonObject();
        JsonElement queryElement = JsonSerializer.SerializeToElement(query);

        long? page = LooseInt64(queryElement, "page");
        long? pageSize = LooseInt64(queryElement, "pageSize");
        long server = LooseInt64(queryElement, "serverId") ?? 0;
        if (server <= 0) server = control.ScanState.ServerId;
        if (server is < 1 or > 99_999) return null; // final non-positive server -> empty page; >99999 matches nothing

        long originalPage = Math.Max(page ?? 1L, 1L);
        // The legacy normalization DTO is int32. Give it only a representable
        // parsing value; restore the original i64 for actual query and response.
        query["page"] = (int)Math.Min(originalPage, int.MaxValue);
        query["pageSize"] = pageSize is null ? 50 : (int)Math.Clamp(pageSize.Value, 1, 200);
        query["serverId"] = (int)server;
        foreach (string name in new[] { "treasureType", "suppliesType" })
        {
            if (!query.ContainsKey(name) || query[name] is null) continue;
            long? loose = LooseInt64(queryElement, name);
            if (loose is null) query.Remove(name);
            else query[name] = (int)Math.Clamp(loose.Value, 0, int.MaxValue);
        }
        // Original 0x3E27F6 takes finite f64 bounds and swaps inversions.
        // Do not clamp to a nonnegative integer: level 0, NULL and -1 differ.
        double? minLevel = LooseFiniteDouble(queryElement, "minLevel");
        double? maxLevel = LooseFiniteDouble(queryElement, "maxLevel");
        if (minLevel.HasValue && maxLevel.HasValue && minLevel > maxLevel)
            (minLevel, maxLevel) = (maxLevel, minLevel);
        // The internal older DTO accepts nonnegative int only; preserve f64
        // exactly on MapQuery after the other validation is complete.
        query.Remove("minLevel");
        query.Remove("maxLevel");
        // The original Map search projects optional booleans with as_bool:
        // a string/number/array/object is absent, not INVALID_MAP_QUERY.
        // The strict clone DTO parser still serves other callers; normalize
        // this native boundary before handing the query to that parser.
        foreach (string name in new[] { "markedOnly", "includeForeignRadarTreasures", "luckyFirst" })
        {
            if (queryElement.TryGetProperty(name, out JsonElement flag) &&
                flag.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                query.Remove(name);
        }
        if (queryElement.TryGetProperty("sorts", out JsonElement sorts) &&
            (sorts.ValueKind != JsonValueKind.Array ||
             sorts.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.Object ||
                !item.TryGetProperty("sortBy", out var sortBy) || sortBy.ValueKind != JsonValueKind.String ||
                !item.TryGetProperty("sortOrder", out var sortOrder) ||
                sortOrder.ValueKind != JsonValueKind.String ||
                sortOrder.GetString() is not ("asc" or "desc"))))
            query.Remove("sorts");
        // Native search also accepts finite loose f64 power bounds, swapping
        // inversions before applying them to the indexed numeric power column.
        double? minPower = LooseFiniteDouble(queryElement, "minPower");
        double? maxPower = LooseFiniteDouble(queryElement, "maxPower");
        if (minPower.HasValue && maxPower.HasValue && minPower > maxPower)
            (minPower, maxPower) = (maxPower, minPower);
        // New Map filters are explicitly validated here at the live search
        // boundary. Reject malformed input; do not turn an invalid maximum or
        // absent native metadata into an unfiltered/zero-power match.
        string? amountState = null;
        string? tileState = null;
        string[]? itemKeys = null;
        if (queryElement.TryGetProperty("resourceAmountState", out JsonElement amountValue) &&
            amountValue.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
        {
            amountState = amountValue.ValueKind == JsonValueKind.String ? amountValue.GetString() : null;
            if (kind != "resource" || amountState is not ("full" or "partial" or "unknown"))
                throw new BridgeCommandException("INVALID_MAP_QUERY", "Invalid Resource amount state.");
        }
        if (queryElement.TryGetProperty("resourceTileState", out JsonElement tileValue) &&
            tileValue.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
        {
            tileState = tileValue.ValueKind == JsonValueKind.String ? tileValue.GetString() : null;
            if (kind != "resource" || tileState is not ("black" or "normal" or "unknown"))
                throw new BridgeCommandException("INVALID_MAP_QUERY", "Invalid Resource terrain state.");
        }
        if (queryElement.TryGetProperty("itemKeys", out JsonElement keysValue) &&
            keysValue.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
        {
            if (kind is not ("truck" or "railway") || keysValue.ValueKind != JsonValueKind.Array ||
                keysValue.GetArrayLength() is < 1 or > 24 ||
                keysValue.EnumerateArray().Any(value => value.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(value.GetString()) || value.GetString()!.Length > 160))
                throw new BridgeCommandException("INVALID_MAP_QUERY", "Invalid reward item selection.");
            itemKeys = keysValue.EnumerateArray().Select(value => value.GetString()!)
                .Distinct(StringComparer.Ordinal).ToArray();
        }
        if (queryElement.TryGetProperty("escortPowerMaxMillions", out JsonElement escortValue) &&
            escortValue.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
        {
            if (kind != "truck" || escortValue.ValueKind != JsonValueKind.Number ||
                !escortValue.TryGetDecimal(out decimal millions) || millions < 0 || millions > 1_000_000_000m)
                throw new BridgeCommandException("INVALID_MAP_QUERY", "Escort power maximum must be a non-negative number in millions.");
            maxPower = (double)(millions * 1_000_000m);
        }
        using JsonDocument envelope = JsonDocument.Parse(JsonSerializer.Serialize(new { kind, query }));
        Map317.MapQuery parsed = NormalizeQuery(envelope.RootElement);
        return parsed with
        {
            Page = originalPage,
            MinLevel = minLevel,
            MaxLevel = maxLevel,
            MinPower = minPower,
            MaxPower = maxPower,
            ResourceAmountState = amountState,
            ResourceTileState = tileState,
            ItemKeys = itemKeys,
            // The recovered itemCount sort expression still uses $itemKey.
            // When an ALL selection has no legacy single-key field, use its
            // first selected genuine key as the deterministic sort dimension.
            ItemKey = parsed.ItemKey ?? itemKeys?.FirstOrDefault(),
        };
    }

    private static Map317.MapQuery NormalizeQuery(JsonElement payload)
    {
        MapDataQueryOptions parsed = MapDataQueryContract.NormalizeSearch(payload);
        return new Map317.MapQuery(
            parsed.Kind,
            parsed.ServerId,
            parsed.Page,
            parsed.PageSize,
            parsed.Sorts.Select(sort => new Map317.MapSort(sort.SortBy, sort.SortOrder)).ToArray(),
            parsed.Keyword,
            parsed.ResourceNameKey,
            parsed.MonsterNameKey,
            parsed.TreasureType,
            parsed.SuppliesType,
            parsed.Alliance,
            parsed.WithoutAlliance,
            parsed.MarkedOnly,
            parsed.Quality,
            parsed.SpecialOnly,
            parsed.ReindeerOnly,
            parsed.ItemKey,
            parsed.CompletionStatus,
            parsed.PlunderableOnly,
            parsed.IncludeForeignRadarTreasures,
            parsed.LuckyFirst,
            parsed.ViewerUid,
            parsed.ViewerAllianceId,
            parsed.MinLevel,
            parsed.MaxLevel);
    }

    private static async Task RunPlunderWorkerAsync(
        Func<CancellationToken, ValueTask> runOnce,
        CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(Map317.MapPlunderWorker.TickMilliseconds));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    await runOnce(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Map317.BridgeCommandException)
                {
                    // The durable worker persists recovered failure/waiting states.
                    // A provider-level failure must not terminate future scheduled work.
                }
                catch (Exception)
                {
                    // Keep the scheduler alive across an adapter/runtime fault. Individual
                    // provider calls translate expected malformed-current-client failures
                    // into Map317 errors before they reach this boundary.
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private static IReadOnlyList<JsonElement> ReadRows(JsonElement payload, string name)
    {
        if (!payload.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.Array)
            throw new BridgeCommandException("INVALID_REQUEST", $"{name} must be an array");
        return value.EnumerateArray().Select(row => row.Clone()).ToArray();
    }

    private static int[] ReadIntArray(JsonElement payload, string name)
    {
        if (!payload.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.Array)
            throw new BridgeCommandException("INVALID_SETTING", $"{name} must be an array");
        var result = new List<int>();
        foreach (JsonElement item in value.EnumerateArray())
        {
            if (!item.TryGetInt32(out int parsed))
                throw new BridgeCommandException("INVALID_SETTING", $"{name} must contain integers");
            result.Add(parsed);
        }
        return result.ToArray();
    }

    private static int RequiredInt(JsonElement payload, string name)
    {
        if (!payload.TryGetProperty(name, out JsonElement value) ||
            value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int parsed))
            throw new BridgeCommandException("INVALID_REQUEST", $"{name} must be an integer");
        return parsed;
    }

    private static long RequiredLong(JsonElement payload, string name)
    {
        if (!payload.TryGetProperty(name, out JsonElement value) ||
            value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out long parsed))
            throw new BridgeCommandException("INVALID_REQUEST", $"{name} must be an integer");
        return parsed;
    }

    private static string RequiredString(JsonElement payload, string name)
    {
        string? value = OptionalString(payload, name);
        if (string.IsNullOrWhiteSpace(value))
            throw new BridgeCommandException("INVALID_REQUEST", $"{name} is required");
        return value;
    }

    private static string? OptionalString(JsonElement payload, string name) =>
        payload.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool OptionalBool(JsonElement payload, string name, bool fallback) =>
        payload.TryGetProperty(name, out JsonElement value) &&
        value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : fallback;

    private static string[] ReadHeaders(JsonElement payload)
    {
        if (!payload.TryGetProperty("headers", out JsonElement value) || value.ValueKind != JsonValueKind.Array)
            throw new BridgeCommandException("MAP_EXPORT_FAILED", "city export headers are invalid");
        // Original 0x39D084 collects only string array elements before
        // checking the resulting 12-column count. An ignored non-string
        // element must not invalidate twelve otherwise valid labels.
        string[] headers = value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString() ?? string.Empty)
            .ToArray();
        if (headers.Length != 12 || headers.Any(string.IsNullOrWhiteSpace))
            throw new BridgeCommandException("MAP_EXPORT_FAILED", "city export headers are invalid");
        return headers;
    }
}
