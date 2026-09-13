namespace LWBridge.Desktop;

internal sealed class MapScanEngine
{
    private readonly IMapScanBlockSource source;
    private readonly IMapScanRunSink sink;
    private readonly Action<MapScanEngineProgress>? progress;

    public MapScanEngine(
        IMapScanBlockSource source,
        IMapScanRunSink sink,
        Action<MapScanEngineProgress>? progress = null)
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        this.sink = sink ?? throw new ArgumentNullException(nameof(sink));
        this.progress = progress;
    }

    public async Task ExecuteAsync(
        MapScanExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        IReadOnlyList<MapScanTargetBlock> blocks = MapScanTraversal.Build(request.TileWidth, request.TileHeight);
        long startedAt = Environment.TickCount64;
        int completed = 0;
        int failed = 0;
        bool terminalStateWritten = false;
        sink.Begin(request, blocks.Count, UtcNowMilliseconds());

        try
        {
            Report(request.RequestedConcurrency, "scanning", blocks.Count, completed, failed, 0, startedAt);
            foreach (MapScanTargetBlock block in blocks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Exception? lastError = null;
                bool succeeded = false;

                for (int attempt = 1; attempt <= request.MaxAttemptsPerBlock; attempt++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Report(request.RequestedConcurrency, "scanning", blocks.Count, completed, failed, 1, startedAt);
                    MapScanBlockCapture capture;
                    try
                    {
                        capture = await source.CaptureAsync(
                            request,
                            block,
                            cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception error)
                    {
                        lastError = error;
                        continue;
                    }

                    ValidateCapture(request, block, capture);
                    sink.CheckpointSuccess(request, block, capture, attempt, UtcNowMilliseconds());
                    completed++;
                    succeeded = true;
                    break;
                }

                if (!succeeded)
                {
                    failed++;
                    string message = lastError?.Message ?? "map block capture failed";
                    sink.CheckpointFailure(
                        request,
                        block,
                        request.MaxAttemptsPerBlock,
                        message,
                        UtcNowMilliseconds());
                }

                Report(request.RequestedConcurrency, "scanning", blocks.Count, completed, failed, 0, startedAt);
            }

            if (failed > 0)
            {
                const string terminalError = "direct map scan contains failed batches";
                sink.Fail(request, terminalError, UtcNowMilliseconds());
                terminalStateWritten = true;
                throw new BridgeCommandException("INCOMPLETE_SCAN", terminalError);
            }

            MapScanCompletionSafety.ValidateDirectCompletion(blocks.Count, completed, failed);
            Report(request.RequestedConcurrency, "publishing", blocks.Count, completed, failed, 0, startedAt);
            sink.Publish(request, UtcNowMilliseconds());
            Report(request.RequestedConcurrency, "completed", blocks.Count, completed, failed, 0, startedAt);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            sink.Stop(request, UtcNowMilliseconds());
            Report(request.RequestedConcurrency, "idle", blocks.Count, completed, failed, 0, startedAt);
            throw;
        }
        catch (Exception error)
        {
            if (!terminalStateWritten)
            {
                try
                {
                    sink.Fail(request, error.Message, UtcNowMilliseconds());
                }
                catch
                {
                    // Preserve the original fatal error if persistence ownership is already lost.
                }
            }
            throw;
        }
    }

    private void Report(
        int concurrency,
        string phase,
        int total,
        int completed,
        int failed,
        int inflight,
        long startedAt)
    {
        MapScanSchedulerCounters counters = MapScanSchedulerProgress.Normalize(
            total,
            concurrency,
            completed,
            failed,
            inflight);
        long elapsed = Math.Max(Environment.TickCount64 - startedAt, 1);
        progress?.Invoke(new MapScanEngineProgress(
            phase,
            total,
            checked((int)counters.CompletedBlocks),
            checked((int)counters.FailedBlocks),
            checked((int)counters.InflightBlocks),
            checked((int)counters.UnreadBlocks),
            MapScanSchedulerProgress.ComputeScanRate(counters.CompletedBlocks, elapsed)));
    }

    private static void ValidateRequest(MapScanExecutionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RunId))
            throw new BridgeCommandException("INVALID_SCAN_RUN", "scan run id is required.");
        if (request.ServerId <= 0)
            throw new BridgeCommandException("SERVER_UNAVAILABLE", "current server id unavailable");
        if (request.RequestedConcurrency <= 0 || request.MaxAttemptsPerBlock <= 0)
            throw new ArgumentOutOfRangeException(nameof(request));
        if (request.SelectedTypes.Count == 0 || request.SelectedTypes.Any(type =>
                !MapScanContract.AllTypes.Contains(type, StringComparer.Ordinal)))
            throw new BridgeCommandException("INVALID_SCAN_TYPES", "no valid map scan types selected");
    }

    private static void ValidateCapture(
        MapScanExecutionRequest request,
        MapScanTargetBlock block,
        MapScanBlockCapture capture)
    {
        if (capture.BlockIndex != block.BlockIndex)
            throw new InvalidDataException("map block capture did not match the requested block");
        if (capture.ServerId != request.ServerId)
            throw new InvalidDataException("map block capture did not match the active server");
        if (capture.WorldId != 0 && request.WorldId != 0 && capture.WorldId != request.WorldId)
            throw new InvalidDataException("map block capture did not match the active world");
        if (string.IsNullOrWhiteSpace(capture.PayloadJson))
            throw new InvalidDataException("map block capture payload is missing");
        foreach (MapStoredRecord record in capture.Records)
        {
            if (record.ServerId != request.ServerId)
                throw new InvalidDataException("map record did not match the active server");
            if (!request.SelectedTypes.Contains(record.Kind, StringComparer.Ordinal))
                throw new InvalidDataException("map record kind was not selected for this scan");
        }
    }

    private static long UtcNowMilliseconds() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
