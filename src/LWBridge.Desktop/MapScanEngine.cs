using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Text.Json;

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
    private readonly Func<CancellationToken, Task>? beforePublicationOwnerCheck;

    public MapScanEngine(
        IMapScanBlockSource source,
        IMapScanRunSink sink,
        Action<MapScanEngineProgress>? progress = null,
        Action<MapScanEngineStageMetrics>? completedStageMetrics = null,
        TimeSpan? acquisitionCleanupTimeout = null,
        Func<CancellationToken, Task>? beforePublicationOwnerCheck = null)
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        this.sink = sink ?? throw new ArgumentNullException(nameof(sink));
        this.progress = progress;
        this.completedStageMetrics = completedStageMetrics;
        this.acquisitionCleanupTimeout = acquisitionCleanupTimeout ?? TimeSpan.FromSeconds(8);
        this.beforePublicationOwnerCheck = beforePublicationOwnerCheck;
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
        IMapScanSharedAcquisitionSource? sharedSource = source as IMapScanSharedAcquisitionSource;
        MapScanSharedAcquisitionOwner? pinnedSharedOwner = null;
        if (sharedSource is not null)
        {
            // Freeze the run selection before any asynchronous acquisition.
            // Caller/source mutation cannot retarget staged data mid-run.
            request = request with { SelectedTypes = request.SelectedTypes.ToArray() };
            MapScanSharedAcquisitionOwner claimed = sharedSource.AcquisitionOwner;
            if (claimed is null)
                throw new InvalidDataException("Shared native acquisition owner is missing.");
            pinnedSharedOwner = claimed with { SelectedTypes = claimed.SelectedTypes?.ToArray() ?? [] };
            ValidateSharedOwner(request, pinnedSharedOwner);
            if (request.TileWidth != 1000 || request.TileHeight != 1000)
                throw new InvalidDataException(
                    "Shared acquisition requires the complete 0-999 full-world tile geometry.");
        }
        IReadOnlyList<MapScanTargetBlock> blocks = MapScanTraversal.Build(request.TileWidth, request.TileHeight);
        if (sharedSource is not null &&
            (blocks.Count != 2500 || blocks[0].MinX != 0 || blocks[0].MinY != 0 ||
             blocks[^1].MaxX != 999 || blocks[^1].MaxY != 999 ||
             blocks.Where((block, index) => block.BlockIndex != index).Any()))
            throw new InvalidDataException("Shared acquisition cannot establish all 2,500 full-world logical blocks.");
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
                if (sharedSource is not null &&
                    (pinnedSharedOwner is null ||
                     !MatchesSharedOwner(pinnedSharedOwner, receipt.SharedOwner) ||
                     !MatchesSharedOwner(pinnedSharedOwner, sharedSource.AcquisitionOwner)))
                    throw new InvalidDataException(
                        "Final native cleanup did not acknowledge the original shared profile/process/group owner.");
            }
            catch (Exception error)
            {
                cleanupOutcomeError = error;
                throw;
            }
        }

        async Task CaptureSharedRunAsync()
        {
            if (sharedSource is null || pinnedSharedOwner is null)
                throw new InvalidOperationException("Shared acquisition was not admitted.");
            var pending = new HashSet<int>(blocks.Select(block => block.BlockIndex));
            var blocksByIndex = blocks.ToDictionary(block => block.BlockIndex);
            var discoveredKeys = new HashSet<(string Kind, int Server, string Key)>();
            var discoveredCityOwners = new HashSet<(int Server, string OwnerUid)>();
            var observedNativePointAois = new HashSet<int>();
            int nextIndex = 0;
            // Exactly ONE source invocation for this run. The source may
            // discover world AOIs once and enrich multiple selected kinds
            // before yielding ordered, final logical-block batches.
            long captureStarted = Stopwatch.GetTimestamp();
            try
            {
                await foreach (MapScanSharedAcquisitionBatch batch in
                    sharedSource.CaptureSharedAsync(request, blocks, cancellationToken)
                        .WithCancellation(cancellationToken).ConfigureAwait(false))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (batch is null || !MatchesSharedOwner(pinnedSharedOwner, batch.Owner) ||
                        !MatchesSharedOwner(pinnedSharedOwner, sharedSource.AcquisitionOwner))
                        throw new InvalidDataException("Shared acquisition batch changed native run/profile/session owner.");
                    IReadOnlyList<MapScanBlockCapture> captures = batch.Captures;
                    if (captures is null || captures.Count == 0 || captures.Count > 2500 - nextIndex)
                        throw new InvalidDataException("Shared acquisition returned missing or excess logical blocks.");
                    for (int i = 0; i < captures.Count; i++)
                    {
                        if (captures[i] is null || captures[i].BlockIndex != nextIndex + i)
                            throw new InvalidDataException(
                                "Shared acquisition returned an out-of-order, stale or duplicate logical block.");
                    }
                    ValidateBatch(request, blocks[nextIndex], captures, pending, blocksByIndex);
                    // Legacy probes may report WorldId=0 as "unspecified";
                    // source-owned shared acquisition may not. The selected
                    // world must be explicitly present on EVERY native block,
                    // especially when scanning a nonzero remote world.
                    if (captures.Any(capture => capture.WorldId != request.WorldId))
                        throw new InvalidDataException(
                            "Shared native acquisition omitted or changed the exact world identity.");
                    ValidateSharedSourceCoverage(captures, batch.BlockSourceProofs,
                        blocksByIndex, observedNativePointAois);
                    // The shared native discovery/enrichment stream must not
                    // quietly replace an older City owner or Resource row by
                    // re-emitting a stable identity from another AOI/block.
                    // Native source arbitration must resolve these before
                    // emitting the final logical block records.
                    foreach (MapScanBlockCapture capture in captures)
                    {
                        MapScanTargetBlock owningBlock = blocksByIndex[capture.BlockIndex];
                        foreach (MapStoredRecord record in capture.Records)
                            ValidateSharedRecord(owningBlock, record, discoveredKeys, discoveredCityOwners);
                    }
                    // All kinds for the batch must validate together before
                    // checkpointing; a late cancellation must not publish.
                    cancellationToken.ThrowIfCancellationRequested();
                    long checkpointStarted = Stopwatch.GetTimestamp();
                    MapScanBlockSuccess[] successes = captures
                        .Select(capture => new MapScanBlockSuccess(blocksByIndex[capture.BlockIndex], capture))
                        .ToArray();
                    long now = UtcNowMilliseconds();
                    if (sink is IMapScanBatchRunSink batchSink && successes.Length > 1)
                        batchSink.CheckpointSuccessBatch(request, successes, 1, now);
                    else
                        foreach (MapScanBlockSuccess success in successes)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            sink.CheckpointSuccess(request, success.Block, success.Capture, 1, now);
                        }
                    foreach (MapScanBlockSuccess success in successes)
                    {
                        pending.Remove(success.Block.BlockIndex);
                        completed++;
                    }
                    nextIndex += captures.Count;
                    checkpointMilliseconds += Stopwatch.GetElapsedTime(checkpointStarted).TotalMilliseconds;
                    Report(request.RequestedConcurrency, "scanning", blocks.Count, completed, failed, 0, startedAt);
                }
            }
            finally
            {
                sourceCaptureMilliseconds += Stopwatch.GetElapsedTime(captureStarted).TotalMilliseconds;
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (nextIndex != blocks.Count || pending.Count != 0 ||
                observedNativePointAois.Count != 10_000)
                throw new InvalidDataException(
                    $"Shared acquisition finished with {nextIndex} of {blocks.Count} logical blocks and " +
                    $"{observedNativePointAois.Count} of 10,000 source-observed native point AOIs.");
        }

        try
        {
            Report(request.RequestedConcurrency, "scanning", blocks.Count, completed, failed, 0, startedAt);
            if (sharedSource is not null)
                await CaptureSharedRunAsync().ConfigureAwait(false);
            else
            {
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
            // The native home acknowledgement alone does not prove that
            // the current desktop owner has remained on this exact
            // server/world/launch session during a long shared acquisition.
            // A final passive owner check must finish before *any* durable
            // map publication. On drift, the already-restored run fails and
            // staged records remain unpublished; cleanup is never repeated.
            if (beforePublicationOwnerCheck is not null)
                await beforePublicationOwnerCheck(cancellationToken).ConfigureAwait(false);
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
                !MapScanContract.AllTypes.Contains(type, StringComparer.Ordinal)) ||
            request.SelectedTypes.Distinct(StringComparer.Ordinal).Count() != request.SelectedTypes.Count)
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

    private static void ValidateSharedOwner(
        MapScanExecutionRequest request, MapScanSharedAcquisitionOwner owner)
    {
        if (string.IsNullOrWhiteSpace(owner.ProfileId) ||
            string.IsNullOrWhiteSpace(owner.GroupGeneration) ||
            owner.GameProcessId <= 0 ||
            string.IsNullOrWhiteSpace(owner.LaunchSessionId) ||
            !string.Equals(owner.RunId, request.RunId, StringComparison.Ordinal) ||
            owner.ServerId != request.ServerId || owner.WorldId != request.WorldId ||
            !string.Equals(owner.LaunchSessionId, request.LaunchSessionId, StringComparison.Ordinal) ||
            owner.SelectedTypes is null ||
            !owner.SelectedTypes.SequenceEqual(request.SelectedTypes, StringComparer.Ordinal))
            throw new InvalidDataException("Shared acquisition owner does not match the admitted run and selection.");
    }

    private static bool MatchesSharedOwner(
        MapScanSharedAcquisitionOwner expected, MapScanSharedAcquisitionOwner? actual) =>
        actual is not null &&
        string.Equals(expected.RunId, actual.RunId, StringComparison.Ordinal) &&
        expected.ServerId == actual.ServerId &&
        expected.WorldId == actual.WorldId &&
        string.Equals(expected.LaunchSessionId, actual.LaunchSessionId, StringComparison.Ordinal) &&
        string.Equals(expected.ProfileId, actual.ProfileId, StringComparison.Ordinal) &&
        expected.GameProcessId == actual.GameProcessId &&
        string.Equals(expected.GroupGeneration, actual.GroupGeneration, StringComparison.Ordinal) &&
        actual.SelectedTypes is not null &&
        expected.SelectedTypes.SequenceEqual(actual.SelectedTypes, StringComparer.Ordinal);

    private static void ValidateSharedSourceCoverage(
        IReadOnlyList<MapScanBlockCapture> captures,
        IReadOnlyList<MapScanSharedBlockSourceProof>? proofs,
        IReadOnlyDictionary<int, MapScanTargetBlock> blocks,
        HashSet<int> observedAois)
    {
        // A stream that synthesizes 2,500 empty logical blocks cannot claim
        // discovery completeness merely because every block was checkpointed.
        // Source-reported replies must account for all four 10x10 AOIs inside
        // each physical 20x20 block, 10,000 unique in total. The future
        // native provider is responsible for independent packet correlation.
        if (proofs is null || proofs.Count != captures.Count)
            throw new InvalidDataException("Shared native reply AOI proof count is missing or incomplete.");
        const int NativeAoisAcross = 100;
        for (int i = 0; i < captures.Count; i++)
        {
            MapScanSharedBlockSourceProof proof = proofs[i];
            MapScanBlockCapture capture = captures[i];
            if (proof is null || proof.LogicalBlockIndex != capture.BlockIndex ||
                string.IsNullOrWhiteSpace(proof.NativeReplyId) ||
                proof.NativeObservedAtUnixMilliseconds <= 0 ||
                proof.NativePointAoiIndices is null ||
                proof.NativePointAoiIndices.Count != 4)
                throw new InvalidDataException("Shared native AOI proof is missing source reply or exact block membership.");
            MapScanTargetBlock block = blocks[capture.BlockIndex];
            int firstRow = block.MinY / 10;
            int firstCol = block.MinX / 10;
            var expected = new HashSet<int>
            {
                firstRow * NativeAoisAcross + firstCol,
                firstRow * NativeAoisAcross + firstCol + 1,
                (firstRow + 1) * NativeAoisAcross + firstCol,
                (firstRow + 1) * NativeAoisAcross + firstCol + 1,
            };
            if (!expected.SetEquals(proof.NativePointAoiIndices) ||
                !proof.NativePointAoiIndices.All(observedAois.Add))
                throw new InvalidDataException(
                    "Shared native AOI proof reused a stale source footprint or omitted this logical block's current AOIs.");
        }
    }

    private static void ValidateSharedRecord(
        MapScanTargetBlock block,
        MapStoredRecord record,
        HashSet<(string Kind, int Server, string Key)> keys,
        HashSet<(int Server, string OwnerUid)> cityOwners)
    {
        if (string.IsNullOrWhiteSpace(record.RecordKey) ||
            !keys.Add((record.Kind, record.ServerId, record.RecordKey)))
            throw new InvalidDataException(
                "Shared native acquisition emitted an empty or duplicate stable record identity.");
        if (record.Kind is not ("city" or "resource")) return;
        if (record.PointIndex is not int point)
            throw new InvalidDataException("Shared City/Resource native point index is missing.");
        if (point is < 1 or > 1_000_000)
            throw new InvalidDataException("Shared City/Resource point index is out of world bounds.");
        int tile = point - 1;
        int x = tile % 1000, y = tile / 1000;
        if (x < block.MinX || x > block.MaxX || y < block.MinY || y > block.MaxY)
            throw new InvalidDataException(
                "Shared City/Resource point index is outside its owning logical block.");
        if (record.Kind != "city") return;
        if (string.IsNullOrWhiteSpace(record.DataJson))
            throw new InvalidDataException("Shared City native owner JSON is missing.");
        try
        {
            using JsonDocument json = JsonDocument.Parse(record.DataJson);
            if (json.RootElement.ValueKind != JsonValueKind.Object ||
                !json.RootElement.TryGetProperty("ownerUid", out JsonElement uid) ||
                uid.ValueKind != JsonValueKind.String)
                throw new InvalidDataException("Shared City native ownerUid is missing or invalid.");
            string? ownerUid = uid.GetString();
            if (string.IsNullOrWhiteSpace(ownerUid))
                throw new InvalidDataException("Shared City native ownerUid must be nonempty.");
            if (!cityOwners.Add((record.ServerId, ownerUid)))
                throw new InvalidDataException(
                    "Shared City owner appeared more than once; latest native location has not been resolved.");
        }
        catch (JsonException error)
        {
            throw new InvalidDataException("Shared City owner JSON is not valid.", error);
        }
    }

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
