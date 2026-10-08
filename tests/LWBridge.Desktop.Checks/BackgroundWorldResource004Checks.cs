using LWBridge.Map317;

namespace LWBridge.Desktop.Checks;

/// <summary>
/// Inert inverse tests against the actual production MapScanStateMachine.StartAsync
/// entry/EnterWorldMapAsync/provider boundary. The real current-client world
/// request/session matcher is additionally exercised by CurrentClientMapBlockSourceChecks.
/// These do NOT claim any simulated provider result as live game evidence.
/// </summary>
internal static class BackgroundWorldResource004Checks
{
    private const int Server = 2212;
    private static MapProviderContext Context(bool inWorld, int server = Server) =>
        new(true, inWorld, server, "live", 0, 1000, 1000, 2500);

    internal static async Task RunAsync()
    {
        await CityEntersWorldAsync();
        await AlreadyWorldSkipsEntryAsync();
        await WorldEntryFailurePropagatesAsync();
        await StaleSessionFailsClosedAsync();
        await CancellationPropagatesAsync();
        await ReplacedSessionCannotCreateRunAsync();
        Console.WriteLine("WORLD004_START_PROVIDER_INVERSES_PASS 6/6 (inert; production state machine and real provider adapter)");
    }

    private static async Task CityEntersWorldAsync()
    {
        int entry = 0, starts = 0;
        var machine = NewMachine(
            ct => ValueTask.FromResult(Context(false)),
            ct => { entry++; return ValueTask.FromResult(Context(true)); },
            (request, ct) => { starts++; Require(request.ServerId == Server &&
                request.SelectedTypes.SequenceEqual(["resource"]), "city correct server and type"); return ValueTask.FromResult(new MapProviderStartResult(true, 2500)); });
        MapScanState state = await machine.StartAsync(new MapScanStartRequest(["resource"], "normal"));
        Require(state.IsReading && state.ScanRunId.Length == 32 && entry == 1 && starts == 1, "city must enter world then start exactly one run");
        await machine.StopAsync();
    }

    private static async Task AlreadyWorldSkipsEntryAsync()
    {
        int entry = 0, starts = 0;
        var machine = NewMachine(
            ct => ValueTask.FromResult(Context(true)),
            ct => { entry++; return ValueTask.FromResult(Context(true)); },
            (request, ct) => { starts++; return ValueTask.FromResult(new MapProviderStartResult(true, 2500)); });
        MapScanState state = await machine.StartAsync(new MapScanStartRequest(["resource"], "normal"));
        Require(state.IsReading && entry == 0 && starts == 1, "already-world must not transition again");
        await machine.StopAsync();
    }

    private static async Task WorldEntryFailurePropagatesAsync()
    {
        int starts = 0;
        var machine = NewMachine(
            ct => ValueTask.FromResult(Context(false)),
            ct => throw new LWBridge.Map317.BridgeCommandException("WORLD_MAP_FAILED", "failed to enter world map"),
            (request, ct) => { starts++; return ValueTask.FromResult(new MapProviderStartResult(true, 2500)); });
        await ExpectAsync(machine, "WORLD_MAP_FAILED");
        Require(starts == 0 && !machine.State.IsReading, "entry failure cannot accept Resource run");
    }

    private static async Task StaleSessionFailsClosedAsync()
    {
        int starts = 0;
        var machine = NewMachine(
            ct => ValueTask.FromResult(Context(false)),
            ct => throw new LWBridge.Map317.BridgeCommandException("GAME_CONNECTION_UNAVAILABLE", "game connection unavailable"),
            (request, ct) => { starts++; return ValueTask.FromResult(new MapProviderStartResult(true, 2500)); });
        await ExpectAsync(machine, "GAME_CONNECTION_UNAVAILABLE");
        Require(starts == 0 && machine.State.ScanRunId.Length == 0, "stale session cannot create run");
    }

    private static async Task CancellationPropagatesAsync()
    {
        using var source = new CancellationTokenSource();
        int starts = 0;
        var machine = NewMachine(
            ct => ValueTask.FromResult(Context(false)),
            ct => { source.Cancel(); ct.ThrowIfCancellationRequested(); return ValueTask.FromResult(Context(true)); },
            (request, ct) => { starts++; return ValueTask.FromResult(new MapProviderStartResult(true, 2500)); });
        try
        {
            await machine.StartAsync(new MapScanStartRequest(["resource"], "normal"), source.Token);
            throw new InvalidDataException("cancelled world transition accepted a Resource run");
        }
        catch (OperationCanceledException) when (source.IsCancellationRequested) { }
        Require(starts == 0 && !machine.State.IsReading, "cancellation must not start provider");
    }

    private static async Task ReplacedSessionCannotCreateRunAsync()
    {
        int starts = 0;
        var machine = NewMachine(
            ct => ValueTask.FromResult(Context(false)),
            ct => throw new LWBridge.Map317.BridgeCommandException("GAME_CONNECTION_UNAVAILABLE", "owned profile instance changed during world entry"),
            (request, ct) => { starts++; return ValueTask.FromResult(new MapProviderStartResult(true, 2500)); });
        await ExpectAsync(machine, "GAME_CONNECTION_UNAVAILABLE");
        Require(starts == 0 && machine.State.ScanRunId.Length == 0, "replaced session must not create run");
    }

    private static async Task ExpectAsync(MapScanStateMachine machine, string code)
    {
        try { await machine.StartAsync(new MapScanStartRequest(["resource"], "normal")); }
        catch (LWBridge.Map317.BridgeCommandException ex) when (ex.Code == code) { return; }
        throw new InvalidDataException("expected exact Map start failure " + code);
    }

    private static MapScanStateMachine NewMachine(
        Func<CancellationToken, ValueTask<MapProviderContext>> context,
        Func<CancellationToken, ValueTask<MapProviderContext>> enter,
        Func<MapProviderStartRequest, CancellationToken, ValueTask<MapProviderStartResult>> start) =>
        new(new MapProviderAdapter(context, enter, start, ct => ValueTask.CompletedTask), new NoopSink());

    private sealed class NoopSink : IMapScanLocalSink
    {
        public ValueTask CancelScanAsync(string runId, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask ClearServerAsync(int serverId, CancellationToken ct = default) => ValueTask.CompletedTask;
    }

    internal static void ProveCompletedReopen(string database, string runId, string outputFile)
    {
        using var store = new MapStore(database);
        MapScanRun run = store.ReadScanRun(runId) ?? throw new InvalidDataException("reopened exact durable run missing");
        Require(run.Status == "completed" && run.TotalBlocks == 2500 &&
            run.CompletedBlocks == 2500 && run.FailedBlocks == 0,
            "completed Resource run must have 2500/2500, zero failed");
        Require(run.SelectedTypes.SequenceEqual(["resource"]), "durable type resource only");
        var first = store.Search(new MapQuery("resource", run.ServerId, Page: 1, PageSize: 50));
        var second = store.Search(new MapQuery("resource", run.ServerId, Page: 2, PageSize: 50));
        var filtered = store.Search(new MapQuery("resource", run.ServerId, Page: 1,
            PageSize: 50, Keyword: "__witness004_unlikely_name__"));
        Require(first.Total > 0 && first.Rows.Count == 50 && second.Rows.Count == 50 &&
            second.Total == first.Total && filtered.Total == 0,
            "production storage query/filter/pagination must return real published Resource data");
        string[] keys1 = first.Rows.Select(row => row.GetProperty("recordKey").GetString() ?? "").ToArray();
        string[] keys2 = second.Rows.Select(row => row.GetProperty("recordKey").GetString() ?? "").ToArray();
        Require(keys1.Distinct().Count() == 50 && keys2.Distinct().Count() == 50 &&
            !keys1.Intersect(keys2).Any(), "reopened Resource pages overlap");
        var result = new
        {
            mode = "independent-production-MapStore-readonly-query-of-live-published-run",
            database, runId, serverId = run.ServerId, runStatus = run.Status,
            run.TotalBlocks, run.CompletedBlocks, run.FailedBlocks, firstTotal = first.Total,
            page1Count = first.Rows.Count, page2Count = second.Rows.Count,
            pageKeysNonoverlapping = true, impossibleKeywordTotal = filtered.Total,
            originalReferenceParity = "NOT_PROVEN",
            source = "MapStore.ReadScanRun + MapStore.Search; real isolated game-published records"
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputFile))!);
        File.WriteAllText(outputFile, System.Text.Json.JsonSerializer.Serialize(result,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("WORLD004_COMPLETED_SQLITE_REOPEN_PASS " + first.Total + " positive Resource rows");
    }
    private static void Require(bool value, string text)
    {
        if (!value) throw new InvalidDataException("WORLD004 " + text);
    }
}
