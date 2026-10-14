using System.Diagnostics;
using System.Runtime.ExceptionServices;

namespace LWBridge.Desktop;

internal sealed record MapScanEngineStageMetrics(
    string RunId,
    int ServerId,
    string ScanMode,
    double SourceCaptureMilliseconds,
    double CheckpointMilliseconds,
    double PublicationMilliseconds,
    double WholeEngineMilliseconds);

internal sealed class MapScanEngine
{
    private readonly IMapScanBlockSource source;
    private readonly IMapScanRunSink sink;
    private readonly Action<MapScanEngineProgress>? progress;
    private readonly Action<MapScanEngineStageMetrics>? completedStageMetrics;
    private readonly TimeSpan acquisitionCleanupTimeout;

    public MapScanEngine(
        IMapScanBlockSource source,
        IMapScanRunSink sink,
        Action<MapScanEngineProgress>? progress = null,
        Action<MapScanEngineStageMetrics>? completedStageMetrics = null,
        TimeSpan? acquisitionCleanupTimeout = null)
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        this.sink = sink ?? throw new ArgumentNullException(nameof(sink));
        this.progress = progress;
        this.completedStageMetrics = completedStageMetrics;
        this.acquisitionCleanupTimeout = acquisitionCleanupTimeout ?? TimeSpan.FromSeconds(8);
        if (this.acquisitionCleanupTimeout <= TimeSpan.Zero ||
            this.acquisitionCleanupTimeout > TimeSpan.FromSeconds(30))
            throw new ArgumentOutOfRangeException(nameof(acquisitionCleanupTimeout));
    }

    public async Task ExecuteAsync(
        MapScanExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        // This contract is *opt-in by source type*. It never changes the
        // existing per-probe behavior for ordinary sources. A consecutive
        // source cannot restore a run without a stable session owner.
        IMapScanAcquisitionCleanup? acquisitionCleanup = source as IMapScanAcquisitionCleanup;
        if (acquisitionCleanup is not null && string.IsNullOrWhiteSpace(request.LaunchSessionId))
            throw new InvalidDataException(
                "Consecutive native cleanup requires an exact launch session identity.");
        IReadOnlyList<MapScanTargetBlock> blocks = MapScanTraversal.Build(request.TileWidth, request.TileHeight);
        long startedAt = Environment.TickCount64;
        int completed = 0;
        int failed = 0;
        bool terminalStateWritten = false;
        double sourceCaptureMilliseconds = 0;
        double checkpointMilliseconds = 0;
        bool cleanupAttempted = false;
        Exception? cleanupOutcomeError = null;
        sink.Begin(request, blocks.Count, UtcNowMilliseconds());

        async Task CleanupOwnedAcquisitionAsync(MapScanAcquisitionExitReason reason)
        {
            if (acquisitionCleanup is null || cleanupAttempted) return;
            cleanupAttempted = true;
            // Never pass the cancelled scan token: Stop and any cancellation
            // after staging still owe a bounded native home-view cleanup.
            using var deadline = new CancellationTokenSource(acquisitionCleanupTimeout);
            try
            {
                MapScanAcquisitionCleanupReceipt receipt =
                    await acquisitionCleanup.CleanupAsync(request, reason, deadline.Token)
                        .WaitAsync(acquisitionCleanupTimeout).ConfigureAwait(false);
                if (receipt is null || !MatchesCleanupRequest(request, receipt.Request) ||
                    !receipt.OriginalCameraRestored || !receipt.FreshHomeViewReplyMatched)
                    throw new InvalidDataException(
                        "Native cleanup did not verify the exact scan owner, original camera and fresh home reply.");
            }
            catch (Exception error)
            {
                cleanupOutcomeError = error;
                throw;
            }
        }

        try
        {
            Report(request.RequestedConcurrency, "scanning", blocks.Count, completed, failed, 0, startedAt);
            var pending = new HashSet<int>(blocks.Select(block => block.BlockIndex));
            var blocksByIndex = blocks.ToDictionary(block => block.BlockIndex);
            while (pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                MapScanTargetBlock block = blocks.First(candidate => pending.Contains(candidate.BlockIndex));
                Exception? lastError = null;
                bool succeeded = false;

                for (int attempt = 1; attempt <= request.MaxAttemptsPerBlock; attempt++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Report(request.RequestedConcurrency, "scanning", blocks.Count, completed, failed, 1, startedAt);
                    IReadOnlyList<MapScanBlockCapture> captures;
                    long captureStarted = Stopwatch.GetTimestamp();
                    try
                    {
                        captures = source is IMapScanProgressBatchSource progressiveBatchSource
                            ? await progressiveBatchSource.CaptureBatchAsync(
                                request, block, pending, sourceProgress =>
                                    Report(request.RequestedConcurrency, "scanning", blocks.Count, completed, failed, 1, startedAt, sourceProgress.Percent),
                                cancellationToken).ConfigureAwait(false)
                            : source is IMapScanBatchSource batchSource
                                ? await batchSource.CaptureBatchAsync(request, block, pending, cancellationToken).ConfigureAwait(false)
                                : [await source.CaptureAsync(request, block, cancellationToken).ConfigureAwait(false)];
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (BridgeCommandException error) when (
                        error.Code == MapScanStartOwnership.MissingConnectionErrorCode)
                    {
                        // A definitive loss of the exact owned game session is run-terminal.
                        // Retrying this as an ordinary block failure can otherwise fan out across
                        // every remaining world block, bury the recovered connection error under
                        // INCOMPLETE_SCAN and delay the user-visible failure substantially.
                        throw;
                    }
                    catch (Exception error)
                    {
                        lastError = error;
                        continue;
                    }
                    finally
                    {
                        sourceCaptureMilliseconds += Stopwatch.GetElapsedTime(captureStarted).TotalMilliseconds;
                    }

                    // Provider cancellation can race a completed asynchronous batch.
                    // Retire that response before any persistent checkpoint or publication.
                    long checkpointStarted = Stopwatch.GetTimestamp();
                    cancellationToken.ThrowIfCancellationRequested();
                    ValidateBatch(request, block, captures, pending, blocksByIndex);
                    MapScanBlockSuccess[] successes = captures
                        .Select(capture => new MapScanBlockSuccess(blocksByIndex[capture.BlockIndex], capture))
                        .ToArray();
                    long checkpointedAt = UtcNowMilliseconds();
                    if (sink is IMapScanBatchRunSink batchSink && successes.Length > 1)
                        batchSink.CheckpointSuccessBatch(request, successes, attempt, checkpointedAt);
                    else
                        foreach (MapScanBlockSuccess success in successes)
                            sink.CheckpointSuccess(request, success.Block, success.Capture, attempt, checkpointedAt);
                    foreach (MapScanBlockSuccess success in successes)
                    {
                        pending.Remove(success.Block.BlockIndex);
                        completed++;
                    }
                    checkpointMilliseconds += Stopwatch.GetElapsedTime(checkpointStarted).TotalMilliseconds;
                    succeeded = true;
                    break;
                }

                if (!succeeded)
                {
                    failed++;
                    pending.Remove(block.BlockIndex);
                    string message = lastError?.Message ?? "map block capture failed";
                    sink.CheckpointFailure(request, block, request.MaxAttemptsPerBlock, message, UtcNowMilliseconds());
                }

                Report(request.RequestedConcurrency, "scanning", blocks.Count, completed, failed, 0, startedAt);
            }

            if (failed > 0)
            {
                const string terminalError = "direct map scan contains failed batches";
                await CleanupOwnedAcquisitionAsync(MapScanAcquisitionExitReason.Failed).ConfigureAwait(false);
                sink.Fail(request, terminalError, UtcNowMilliseconds());
                terminalStateWritten = true;
                throw new BridgeCommandException("INCOMPLETE_SCAN", terminalError);
            }

            MapScanCompletionSafety.ValidateDirectCompletion(blocks.Count, completed, failed);
            Report(request.RequestedConcurrency, "publishing", blocks.Count, completed, failed, 0, startedAt);
            cancellationToken.ThrowIfCancellationRequested();
            // A positive staged run must restore the native home *and* match a
            // fresh reply before the SQLite publication transaction can start.
            await CleanupOwnedAcquisitionAsync(MapScanAcquisitionExitReason.Completed).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            long publishStarted = Stopwatch.GetTimestamp();
            sink.Publish(request, UtcNowMilliseconds());
            double publicationMilliseconds = Stopwatch.GetElapsedTime(publishStarted).TotalMilliseconds;
            if (completedStageMetrics is not null)
            {
                // Diagnostic failures cannot turn a completed, durable scan into
                // an apparent failed scan or make the engine publish twice.
                try
                {
                    completedStageMetrics(new MapScanEngineStageMetrics(
                        request.RunId, request.ServerId, request.ScanMode,
                        sourceCaptureMilliseconds, checkpointMilliseconds,
                        publicationMilliseconds, Environment.TickCount64 - startedAt));
                }
                catch (Exception error)
                {
                    Console.Error.WriteLine("Map scan stage diagnostic unavailable: " + error.Message);
                }
            }
        }
        catch (OperationCanceledException cancellationError) when (cancellationToken.IsCancellationRequested)
        {
            Exception? cleanupError = null;
            try
            {
                await CleanupOwnedAcquisitionAsync(MapScanAcquisitionExitReason.Stopped).ConfigureAwait(false);
            }
            catch (Exception error)
            {
                cleanupError = error;
            }
            cleanupError ??= cleanupOutcomeError;
            // Even if the native owner is gone and restoration is unverified,
            // retire this exact scan's persistent staging; never publish it.
            sink.Stop(request, UtcNowMilliseconds());
            if (cleanupError is not null)
            {
                // Preserve Stop/cancellation classification while surfacing the
                // restoration failure in the observed exception and diagnostics.
                cancellationError.Data["MAP014_NATIVE_CLEANUP_UNVERIFIED"] = cleanupError;
                Console.Error.WriteLine("MAP014 cancelled scan native cleanup unverified: " + cleanupError.Message);
            }
            throw;
        }
        catch (Exception error)
        {
            Exception propagated = error;
            try
            {
                await CleanupOwnedAcquisitionAsync(cancellationToken.IsCancellationRequested
                    ? MapScanAcquisitionExitReason.Stopped
                    : MapScanAcquisitionExitReason.Failed).ConfigureAwait(false);
            }
            catch (Exception cleanupError)
            {
                propagated = new AggregateException(
                    "Map acquisition and native cleanup both failed; publication is prohibited.",
                    error, cleanupError);
            }
            if (!terminalStateWritten)
            {
                try
                {
                    if (acquisitionCleanup is not null && cancellationToken.IsCancellationRequested)
                        sink.Stop(request, UtcNowMilliseconds());
                    else
                        sink.Fail(request, propagated.Message, UtcNowMilliseconds());
                }
                catch
                {
                    // Preserve the original fatal error if persistence ownership is already lost.
                }
            }
            if (!ReferenceEquals(propagated, error))
                ExceptionDispatchInfo.Capture(propagated).Throw();
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
        long startedAt,
        double? acquisitionProgressPercent = null)
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
            MapScanSchedulerProgress.ComputeScanRate(counters.CompletedBlocks, elapsed),
            acquisitionProgressPercent));
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

    private static bool MatchesCleanupRequest(MapScanExecutionRequest expected, MapScanExecutionRequest? actual) =>
        actual is not null &&
        string.Equals(expected.RunId, actual.RunId, StringComparison.Ordinal) &&
        expected.ServerId == actual.ServerId &&
        expected.WorldId == actual.WorldId &&
        expected.TileWidth == actual.TileWidth &&
        expected.TileHeight == actual.TileHeight &&
        expected.RequestedConcurrency == actual.RequestedConcurrency &&
        expected.MaxAttemptsPerBlock == actual.MaxAttemptsPerBlock &&
        expected.PlayerTileX == actual.PlayerTileX &&
        expected.PlayerTileY == actual.PlayerTileY &&
        string.Equals(expected.ScanMode, actual.ScanMode, StringComparison.Ordinal) &&
        string.Equals(expected.LaunchSessionId, actual.LaunchSessionId, StringComparison.Ordinal) &&
        expected.LiveServerId == actual.LiveServerId &&
        expected.SelectedTypes.SequenceEqual(actual.SelectedTypes, StringComparer.Ordinal);

    private static void ValidateBatch(
        MapScanExecutionRequest request,
        MapScanTargetBlock seedBlock,
        IReadOnlyList<MapScanBlockCapture> captures,
        IReadOnlySet<int> pending,
        IReadOnlyDictionary<int, MapScanTargetBlock> blocksByIndex)
    {
        if (captures.Count == 0)
            throw new InvalidDataException("map batch capture returned no logical blocks");
        if (!captures.Any(capture => capture.BlockIndex == seedBlock.BlockIndex))
            throw new InvalidDataException("map batch capture did not include the requested seed block");
        var seen = new HashSet<int>();
        foreach (MapScanBlockCapture capture in captures)
        {
            if (!seen.Add(capture.BlockIndex))
                throw new InvalidDataException("map batch capture contained a duplicate logical block");
            if (!pending.Contains(capture.BlockIndex) || !blocksByIndex.TryGetValue(capture.BlockIndex, out MapScanTargetBlock block))
                throw new InvalidDataException("map batch capture returned a block outside the pending scan set");
            ValidateCapture(request, block, capture);
        }
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
        // A zero capture world is an unspecified legacy source. A nonzero
        // captured world must always equal the requested world, including when
        // the admitted standard world is id 0; otherwise unrelated world data
        // could be staged under the correct server.
        if (capture.WorldId != 0 && capture.WorldId != request.WorldId)
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
