using System.Text.Json;

namespace LWBridge.Map317;

/// <summary>
/// Profile-owned 0.3.17 Map control/data plane. This coordinates the exact
/// local SQLite contract with the high-level game provider boundary without
/// inventing the provider's traversal or transport grammar.
/// </summary>
public sealed class MapControlPlane : IDisposable
{
    private readonly MapStore store;
    private readonly MapScanStateMachine scan;
    private readonly MapExporter exporter;
    private readonly Func<long> nowMilliseconds;

    public MapControlPlane(
        MapStore store,
        IMapProvider provider,
        Func<long>? nowMilliseconds = null)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        ArgumentNullException.ThrowIfNull(provider);
        this.nowMilliseconds = nowMilliseconds ?? (() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        scan = new MapScanStateMachine(
            provider,
            new MapStoreScanLocalSink(store, this.nowMilliseconds),
            () => this.nowMilliseconds() / 1000L);
        exporter = new MapExporter(store);
        scan.StateChanged += (_, args) => ScanStateChanged?.Invoke(this, args);
    }

    public event EventHandler<MapScanStateChangedEventArgs>? ScanStateChanged;
    public event EventHandler? PlayerMarkChanged;

    public MapScanState ScanState => scan.State;

    public ValueTask<MapScanState> RefreshContextAsync(CancellationToken cancellationToken = default) =>
        scan.RefreshContextAsync(cancellationToken);

    public async ValueTask<MapScanState> StartScanAsync(
        MapScanStartRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        MapScanState state = await scan.StartAsync(request, cancellationToken).ConfigureAwait(false);
        long now = nowMilliseconds();
        try
        {
            store.InsertScanRun(new MapScanRun(
                state.ScanRunId,
                state.ServerId,
                state.SelectedTypes,
                "running",
                state.TotalBlocks,
                0,
                0,
                now,
                now,
                null));
            return state;
        }
        catch
        {
            try
            {
                await scan.StopAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Preserve the original persistence failure. Provider stop is already
                // best-effort inside the state machine and no data is fabricated here.
            }
            throw;
        }
    }

    public MapScanState ReportProgress(MapScanProgressUpdate update)
    {
        MapScanState next = scan.PreviewProgress(update);
        if (!string.IsNullOrEmpty(next.ScanRunId) && next.IsReading)
        {
            store.UpdateScanProgress(
                next.ScanRunId,
                next.CompletedBlocks,
                next.FailedBlocks,
                next.Error,
                nowMilliseconds());
        }
        return scan.ApplyProgress(next);
    }

    public void StageRecord(MapRecord record)
    {
        MapScanState state = scan.State;
        if (!state.IsReading || string.IsNullOrEmpty(state.ScanRunId))
            throw new BridgeCommandException("INVALID_SCAN", "map scan is not running");
        if (record.ServerId != state.ServerId || !state.SelectedTypes.Contains(record.Kind, StringComparer.Ordinal))
            throw new BridgeCommandException("INVALID_MAP_RECORD", "map record does not belong to the active scan");
        store.StageRecord(state.ScanRunId, record);
    }

    public MapScanState CompleteScan()
    {
        MapScanState state = scan.State;
        if (!state.IsReading || string.IsNullOrEmpty(state.ScanRunId)) return state;
        long now = nowMilliseconds();
        store.UpdateScanProgress(state.ScanRunId, state.TotalBlocks, 0, null, now);
        store.CompleteScan(state.ScanRunId, now);
        return scan.Complete();
    }

    public MapScanState FailScan(string error)
    {
        MapScanState state = scan.State;
        if (!state.IsReading || string.IsNullOrEmpty(state.ScanRunId)) return state;
        // An incomplete/failed current-client capture must not overwrite the
        // last successfully published result. Only a complete run publishes.
        store.FailScan(state.ScanRunId, error, nowMilliseconds(), preserveRecords: false);
        return scan.Fail(error);
    }

    public ValueTask<MapScanState> StopScanAsync(CancellationToken cancellationToken = default) =>
        scan.StopAsync(cancellationToken);

    // Original status service (0xF9770-0xF97AA): a live server change while reading marks
    // the run failed (staging DISCARDED, not preserved), stops the provider scan and leaves
    // the shared state idle with the error text; the status call itself still succeeds.
    public ValueTask<MapScanState> FailForServerChangeAsync(
        MapScanState capturedRun, string error, CancellationToken cancellationToken = default) =>
        scan.FailAndStopAsync(error, capturedRun.ScanRunId, capturedRun.ServerId,
            store, nowMilliseconds, cancellationToken);

    public ValueTask<MapScanState> ClearScanAsync(int serverId, CancellationToken cancellationToken = default) =>
        scan.ClearAsync(serverId, cancellationToken);

    public MapSearchResult Search(MapQuery query, long? nowUnixMilliseconds = null)
    {
        MapScanState state = scan.State;
        string? runId = ActiveRunForServer(query.ServerId, state);
        return store.Search(query with { ScanRunId = runId }, nowUnixMilliseconds);
    }

    public MapOptionSet ReadOptions(int requestedServerId)
    {
        MapScanState state = scan.State;
        int serverId = requestedServerId > 0 ? requestedServerId : state.ServerId;
        MapStore.ValidateServerId(serverId);
        return store.ReadOptions(serverId, ActiveRunForServer(serverId, state), nowMilliseconds());
    }

    public (int ServerId, IReadOnlyDictionary<string, int> Counts, MapScanState ScanState) ReadSummary()
    {
        MapScanState state = scan.State;
        if (state.ServerId <= 0)
            return (0, MapKinds.All.ToDictionary(kind => kind, _ => 0, StringComparer.Ordinal), state);
        string? runId = ActiveRunForServer(state.ServerId, state);
        return (state.ServerId, store.SummaryCounts(state.ServerId, runId), state);
    }

    public void SetPlayerMark(MapPlayerMark mark, bool marked)
    {
        store.SetPlayerMark(mark, marked);
        PlayerMarkChanged?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyList<int> GetServerJumpHistory() => store.GetServerJumpHistory();

    public IReadOnlyList<int> SetServerJumpHistory(IEnumerable<int> history) =>
        store.SetServerJumpHistory(history, nowMilliseconds());

    public IReadOnlyList<int> ImportServerJumpHistory(IEnumerable<int> history) =>
        store.ImportServerJumpHistory(history, nowMilliseconds());

    public CityExportResult ExportCitiesToPath(MapQuery query, CityExportOptions options, string path) =>
        exporter.ExportCitiesToPath(query with { ScanRunId = ActiveRunForServer(query.ServerId, scan.State) }, options, path);

    public void UpsertTreasureClaimState(
        int serverId,
        string playerUid,
        string treasureUuid,
        long? expireTime,
        long updatedAt,
        string stateJson) =>
        store.UpsertTreasureClaimState(serverId, playerUid, treasureUuid, expireTime, updatedAt, stateJson, nowMilliseconds());

    public void Dispose() => store.Dispose();

    private static string? ActiveRunForServer(int serverId, MapScanState state) =>
        serverId > 0 && state.IsReading && state.ServerId == serverId && !string.IsNullOrEmpty(state.ScanRunId)
            ? state.ScanRunId
            : null;
}

internal sealed class MapStoreScanLocalSink : IMapScanLocalSink
{
    private readonly MapStore store;
    private readonly Func<long> nowMilliseconds;

    internal MapStoreScanLocalSink(MapStore store, Func<long> nowMilliseconds)
    {
        this.store = store;
        this.nowMilliseconds = nowMilliseconds;
    }

    public ValueTask CancelScanAsync(string scanRunId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        store.CancelScan(scanRunId, null, nowMilliseconds());
        return ValueTask.CompletedTask;
    }

    public ValueTask ClearServerAsync(int serverId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        store.ClearServer(serverId);
        return ValueTask.CompletedTask;
    }
}
