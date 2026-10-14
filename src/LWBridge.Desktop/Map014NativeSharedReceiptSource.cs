using System.Runtime.CompilerServices;
using System.Text.Json;

namespace LWBridge.Desktop;

/// <summary>
/// Future inbound-only boundary, not an existing native provider.
/// An implementation must independently authenticate *received* native replies
/// against the owned Overview route, PID, challenge and live session, and must
/// never issue camera shifts, game commands, HTTP requests or polling here.
/// There is currently NO production implementation of this interface.
/// </summary>
internal interface IMap014ReceivedNativeSharedGroupIngress
{
    Task<Map014ReceivedNativeSharedGroup?> ReceiveAlreadyCompletedAsync(
        MapScanExecutionRequest request,
        OverviewMapScanSession expectedSession,
        CancellationToken cancellationToken);

    // Return the same independently received final-home packet once only;
    // never send an extra camera command or substitute a synthetic ACK.
    Task<Map014ReceivedNativeHomeAcknowledgement?> AcknowledgeAlreadyReceivedHomeAsync(
        MapScanExecutionRequest request,
        OverviewMapScanSession expectedSession,
        MapScanAcquisitionExitReason reason,
        CancellationToken cancellationToken);
}

/// <summary>
/// Native home packet's independent response identity and timestamp as
/// delivered by the inbound transport, not inferred from boolean cleanup flags.
/// </summary>
internal sealed record Map014ReceivedNativeHomeAcknowledgement(
    string HomeRequestId,
    string NativeReplyRequestId,
    long NativeReplyAtUnixMs,
    MapScanAcquisitionCleanupReceipt Receipt);

/// <summary>
/// Native AOI proof supplied by the ingress, not calculated from an expected
/// block or native probe command. The 10,000 native AOI indices must each be
/// represented exactly once by owned, correlated, timestamped replies.
/// A source-issued boolean/JSON label is never independent authentication.
/// </summary>
internal sealed record Map014ReceivedNativeAoiProof(
    int NativeAoiIndex,
    string NativeReplyRequestId,
    long NativeReplyAtUnixMs,
    string NativeSource,
    string RunId,
    int ServerId,
    long WorldId,
    string LaunchSessionId,
    string ProfileId,
    int GamePid,
    string GroupGeneration);

/// <summary>
/// Explicit type-specific outcome for one logical 20x20 block. Empty records
/// are valid ONLY when the authenticated ingress has supplied the completed
/// kind's own source/reply marker: absence of a kind is not an empty census.
/// </summary>
internal sealed record Map014ReceivedKindBlock(
    int BlockIndex,
    string Kind,
    string Source,
    string NativeReplyRequestId,
    long NativeReplyAtUnixMs,
    IReadOnlyList<MapStoredRecord> Records);

/// <summary>
/// A fully received native world acquisition with one A/B/C camera-retention
/// diagnostic, every native AOI, all selected-kind block enrichments, and the
/// ORIGINAL native-owned final home receipt. This model does not attest packet
/// authenticity; that missing transport is the integration blocker.
/// </summary>
internal sealed record Map014ReceivedNativeSharedGroup(
    OverviewMapScanSession OwnedSession,
    MapScanExecutionRequest Request,
    IReadOnlyList<JsonElement> NativeConsecutiveRegions,
    IReadOnlyList<Map014ReceivedNativeAoiProof> NativeAoiProofs,
    IReadOnlyList<Map014ReceivedKindBlock> KindBlocks,
    MapScanAcquisitionCleanupReceipt NativeHomeReceipt);

/// <summary>
/// OPT-IN production-shaped source envelope processor. It is deliberately not
/// constructed by Map317's production source selector. The existing FastCity
/// file-command/result transport is per-probe, not an authenticated inbound
/// all-world A/B/C group transport, so this cannot be wired truthfully yet.
///
/// No network, game, file, camera or Lua access occurs here. This source only
/// consumes the injected already-received ingress result. In particular it
/// NEVER invents native AOIs, native replies, kind responses or home success.
/// All positive tests are synthetic schema exercises, NOT live native proof.
/// </summary>
internal sealed class Map014NativeSharedReceiptSource : IMapScanSharedAcquisitionSource
{
    private readonly IMap014ReceivedNativeSharedGroupIngress ingress;
    private readonly MapScanExecutionRequest pinnedRequest;
    private readonly OverviewMapScanSession pinnedSession;
    private readonly MapScanSharedAcquisitionOwner pinnedOwner;
    private MapScanAcquisitionCleanupReceipt? verifiedNativeHome;
    private string? verifiedHomeRequestId;
    private long verifiedHomeReplyAt;
    private int captureInvocations;
    private int cleanupInvocations;

    internal Map014NativeSharedReceiptSource(
        IMap014ReceivedNativeSharedGroupIngress ingress,
        OverviewMapScanSession session,
        MapScanExecutionRequest request,
        string expectedNativeGroupGeneration)
    {
        this.ingress = ingress ?? throw new ArgumentNullException(nameof(ingress));
        pinnedSession = session ?? throw new ArgumentNullException(nameof(session));
        ArgumentNullException.ThrowIfNull(request);
        pinnedRequest = request with { SelectedTypes = request.SelectedTypes?.ToArray() ?? [] };
        if (string.IsNullOrWhiteSpace(expectedNativeGroupGeneration) ||
            string.IsNullOrWhiteSpace(session.ProfileId) ||
            string.IsNullOrWhiteSpace(session.SessionId) ||
            string.IsNullOrWhiteSpace(session.Challenge) ||
            string.IsNullOrWhiteSpace(session.GamePath) ||
            string.IsNullOrWhiteSpace(session.GameStartedAtUtc) ||
            session.GamePid <= 0 ||
            string.IsNullOrWhiteSpace(request.RunId) ||
            request.LiveServerId != request.ServerId ||
            request.ServerId <= 0 || request.WorldId != 0 ||
            request.TileWidth != 1000 || request.TileHeight != 1000 ||
            request.PlayerTileX is not >= 0 or >= 1000 ||
            request.PlayerTileY is not >= 0 or >= 1000 ||
            request.LaunchSessionId != session.SessionId ||
            pinnedRequest.SelectedTypes.Count == 0 ||
            pinnedRequest.SelectedTypes.Distinct(StringComparer.Ordinal).Count() != pinnedRequest.SelectedTypes.Count ||
            pinnedRequest.SelectedTypes.Any(kind => !MapScanContract.AllTypes.Contains(kind, StringComparer.Ordinal)))
            throw new InvalidDataException("Native shared receipt adapter requires exact current Overview/run/0-999 ownership.");
        // No authenticated native Resource freshness cohort or typed
        // WorldMarch queue/response/arrival proof exists for mixed acquisition.
        // Never promote a source-name label into production-ready enrichment.
        if (pinnedRequest.SelectedTypes.Count != 1 ||
            pinnedRequest.SelectedTypes[0] != "city")
            throw new InvalidDataException(
                "Native shared receipt ingress is City-only until independent Resource/March/type-specific source readiness is proven.");
        pinnedOwner = new MapScanSharedAcquisitionOwner(
            pinnedRequest.RunId, pinnedRequest.ServerId, pinnedRequest.WorldId,
            session.SessionId, pinnedRequest.SelectedTypes,
            session.ProfileId, session.GamePid, expectedNativeGroupGeneration);
    }

    public MapScanSharedAcquisitionOwner AcquisitionOwner => pinnedOwner;

    public Task<MapScanBlockCapture> CaptureAsync(
        MapScanExecutionRequest request, MapScanTargetBlock block, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Native shared receipt source cannot use legacy per-block capture.");

    public async IAsyncEnumerable<MapScanSharedAcquisitionBatch> CaptureSharedAsync(
        MapScanExecutionRequest request,
        IReadOnlyList<MapScanTargetBlock> orderedBlocks,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        CheckRequest(request);
        if (Interlocked.Increment(ref captureInvocations) != 1)
            throw new InvalidDataException("Native shared group receipt cannot be acquired twice for the same run.");
        VerifyBlocks(orderedBlocks);
        cancellationToken.ThrowIfCancellationRequested();
        Map014ReceivedNativeSharedGroup envelope =
            await ingress.ReceiveAlreadyCompletedAsync(pinnedRequest, pinnedSession, cancellationToken)
                .ConfigureAwait(false) ??
            throw new InvalidDataException("No independently received native shared group exists for this run.");
        cancellationToken.ThrowIfCancellationRequested();

        // Preflight the ENTIRE receipt envelope BEFORE emitting even one
        // staged logical block. Absence/collision of one native AOI, one
        // selected kind, the final owned home reply, or a stale session fails.
        (MapScanBlockCapture[] captures,
         MapScanSharedBlockSourceProof[] blockProofs,
         MapScanAcquisitionCleanupReceipt finalHome) =
            ValidateReceivedEnvelope(envelope, orderedBlocks);
        verifiedNativeHome = finalHome;
        verifiedHomeRequestId = envelope.NativeConsecutiveRegions[2]
            .GetProperty("homeRestoreRequestId").GetString();
        verifiedHomeReplyAt = envelope.NativeConsecutiveRegions[2]
            .GetProperty("homeNativeReplyAtUnixMs").GetInt64();

        const int blocksPerChunk = 100;
        for (int start = 0; start < captures.Length; start += blocksPerChunk)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new MapScanSharedAcquisitionBatch(
                pinnedOwner,
                captures.Skip(start).Take(blocksPerChunk).ToArray(),
                blockProofs.Skip(start).Take(blocksPerChunk).ToArray());
        }
    }

    public async Task<MapScanAcquisitionCleanupReceipt> CleanupAsync(
        MapScanExecutionRequest request,
        MapScanAcquisitionExitReason reason,
        CancellationToken cleanupToken)
    {
        CheckRequest(request);
        cleanupToken.ThrowIfCancellationRequested();
        if (Interlocked.Increment(ref cleanupInvocations) != 1)
            throw new InvalidDataException("Shared group native-owned cleanup acknowledgement was already consumed.");
        // A stop before receipt arrival cannot honestly claim restoration.
        // The eventual transport must implement a separate owned cancellation
        // cleanup path; fail closed until that mechanism has genuine proof.
        MapScanAcquisitionCleanupReceipt expected = verifiedNativeHome ??
            throw new InvalidDataException(
                "Native final-home receipt is absent; no restoration or fresh reply can be acknowledged.");
        Map014ReceivedNativeHomeAcknowledgement acknowledged =
            await ingress.AcknowledgeAlreadyReceivedHomeAsync(
                pinnedRequest, pinnedSession, reason, cleanupToken).ConfigureAwait(false) ??
            throw new InvalidDataException("Already-received final native home packet was not acknowledged.");
        MapScanAcquisitionCleanupReceipt received = acknowledged.Receipt ??
            throw new InvalidDataException("Final native home packet did not contain its cleanup receipt.");
        if (received.Request is null || !SameRequest(received.Request, pinnedRequest) ||
            !MatchesOwner(received.SharedOwner) ||
            !received.OriginalCameraRestored || !received.FreshHomeViewReplyMatched ||
            !received.Equals(expected) ||
            string.IsNullOrWhiteSpace(verifiedHomeRequestId) ||
            !string.Equals(acknowledged.HomeRequestId, verifiedHomeRequestId, StringComparison.Ordinal) ||
            !string.Equals(acknowledged.NativeReplyRequestId, verifiedHomeRequestId, StringComparison.Ordinal) ||
            acknowledged.NativeReplyAtUnixMs != verifiedHomeReplyAt)
            throw new InvalidDataException(
                "Final native home acknowledgement did not match the pinned original received packet.");
        return received;
    }

    private (MapScanBlockCapture[], MapScanSharedBlockSourceProof[], MapScanAcquisitionCleanupReceipt) ValidateReceivedEnvelope(
        Map014ReceivedNativeSharedGroup envelope, IReadOnlyList<MapScanTargetBlock> blocks)
    {
        if (envelope.OwnedSession != pinnedSession ||
            envelope.Request is null || !SameRequest(envelope.Request, pinnedRequest))
            throw new InvalidDataException("Received native group came from another Overview owner or run.");

        CurrentClientSharedSourceAdmissionDiagnostic diagnostic =
            CurrentClientMapBlockSource.ValidateSharedSourceAdmissionSchema(
                pinnedSession, pinnedRequest, envelope.NativeConsecutiveRegions);
        if (!MatchesOwner(diagnostic.PinnedOwner) || !diagnostic.DiagnosticOnly)
            throw new InvalidDataException("Received native A/B/C admission does not match the pinned group.");
        JsonElement final = envelope.NativeConsecutiveRegions[2];
        long firstStarted = envelope.NativeConsecutiveRegions[0]
            .GetProperty("requestStartedAtUnixMs").GetInt64();
        long finalHomeAt = final.GetProperty("homeNativeReplyAtUnixMs").GetInt64();
        string finalHomeReplyId = final.GetProperty("homeRestoreRequestId").GetString()!;
        if (finalHomeAt <= firstStarted)
            throw new InvalidDataException("Native shared group has no positive owned home reply interval.");

        if (envelope.NativeHomeReceipt is not { } home ||
            home.Request is null || !SameRequest(home.Request, pinnedRequest) ||
            !MatchesOwner(home.SharedOwner) ||
            !home.OriginalCameraRestored || !home.FreshHomeViewReplyMatched)
            throw new InvalidDataException(
                "Native final-home response/acknowledgement is absent or belongs to another group.");

        var aoiProofs = envelope.NativeAoiProofs;
        if (aoiProofs is null || aoiProofs.Count != 10000)
            throw new InvalidDataException("Native group must explicitly prove all 10,000 native AOIs.");
        var allNativeAois = new HashSet<int>();
        var aoiByIndex = new Map014ReceivedNativeAoiProof[10000];
        foreach (Map014ReceivedNativeAoiProof evidence in aoiProofs)
        {
            if (evidence is null || evidence.NativeAoiIndex is < 0 or >= 10000 ||
                !allNativeAois.Add(evidence.NativeAoiIndex) ||
                !MatchesEvidenceOwner(evidence.RunId, evidence.ServerId, evidence.WorldId,
                    evidence.LaunchSessionId, evidence.ProfileId, evidence.GamePid, evidence.GroupGeneration) ||
                string.IsNullOrWhiteSpace(evidence.NativeReplyRequestId) ||
                string.Equals(evidence.NativeReplyRequestId, finalHomeReplyId, StringComparison.Ordinal) ||
                !string.Equals(evidence.NativeSource, "WorldPointManager._curViewIndex", StringComparison.Ordinal) ||
                evidence.NativeReplyAtUnixMs < firstStarted ||
                evidence.NativeReplyAtUnixMs >= finalHomeAt)
                throw new InvalidDataException("Native AOI receipt is stale, duplicate, wrong source or wrong owner.");
            aoiByIndex[evidence.NativeAoiIndex] = evidence;
        }
        if (allNativeAois.Count != 10000)
            throw new InvalidDataException("Received native AOI proof has holes at the 0..999 seams.");

        var enrichments = envelope.KindBlocks;
        int expectedCount = checked(blocks.Count * pinnedRequest.SelectedTypes.Count);
        if (enrichments is null || enrichments.Count != expectedCount)
            throw new InvalidDataException("Native source omitted one or more type-specific logical block results.");
        var byBlock = new List<MapStoredRecord>[blocks.Count];
        for (int index = 0; index < blocks.Count; index++)
            byBlock[index] = [];
        var observed = new HashSet<(int Block, string Kind)>();
        var nativeRecordKeys = new HashSet<(string Kind, int ServerId, string RecordKey)>();
        var nativeCityOwners = new HashSet<(int ServerId, string OwnerUid)>();
        foreach (Map014ReceivedKindBlock typed in enrichments)
        {
            if (typed is null || typed.BlockIndex is < 0 or >= 2500 ||
                !observed.Add((typed.BlockIndex, typed.Kind)) ||
                !pinnedRequest.SelectedTypes.Contains(typed.Kind, StringComparer.Ordinal) ||
                !ExpectedKindSource(typed.Kind, typed.Source) ||
                typed.NativeReplyAtUnixMs < firstStarted ||
                typed.NativeReplyAtUnixMs >= finalHomeAt ||
                typed.Records is null)
                throw new InvalidDataException("Type-specific native enrichment is missing, stale, foreign or duplicate.");
            MapScanTargetBlock block = blocks[typed.BlockIndex];
            int blockAoi = block.MinY / 10 * 100 + block.MinX / 10;
            Map014ReceivedNativeAoiProof direct = aoiByIndex[blockAoi];
            if (direct is null ||
                !string.Equals(typed.NativeReplyRequestId,
                    direct.NativeReplyRequestId, StringComparison.Ordinal) ||
                typed.NativeReplyAtUnixMs != direct.NativeReplyAtUnixMs)
                throw new InvalidDataException(
                    "Type-specific enrichment refers to another logical block's native AOI response.");
            foreach (MapStoredRecord record in typed.Records)
            {
                if (record is null || record.Kind != typed.Kind ||
                    record.ServerId != pinnedRequest.ServerId ||
                    string.IsNullOrWhiteSpace(record.RecordKey) ||
                    !nativeRecordKeys.Add((record.Kind, record.ServerId, record.RecordKey)) ||
                    record.PointIndex is not int point ||
                    point is < 1 or > 1000000 ||
                    (point - 1) % 1000 < block.MinX ||
                    (point - 1) % 1000 > block.MaxX ||
                    (point - 1) / 1000 < block.MinY ||
                    (point - 1) / 1000 > block.MaxY)
                    throw new InvalidDataException("Type-enriched record belongs to another kind/server/AOI block.");
                // City-only remains the only admitted native kind. Validate
                // canonical owner identity now (before ANY positive batch)
                // rather than relying on the engine to reject a late block
                // after earlier SQLite staging has begun.
                if (string.IsNullOrWhiteSpace(record.DataJson))
                    throw new InvalidDataException("Received native City owner JSON is missing.");
                try
                {
                    using JsonDocument city = JsonDocument.Parse(record.DataJson);
                    if (city.RootElement.ValueKind != JsonValueKind.Object ||
                        !city.RootElement.TryGetProperty("ownerUid", out JsonElement ownerUid) ||
                        ownerUid.ValueKind != JsonValueKind.String ||
                        string.IsNullOrWhiteSpace(ownerUid.GetString()) ||
                        !nativeCityOwners.Add((record.ServerId, ownerUid.GetString()!)))
                        throw new InvalidDataException(
                            "Received native City owner identity is missing, stale or duplicated.");
                }
                catch (JsonException error)
                {
                    throw new InvalidDataException("Received native City owner JSON is invalid.", error);
                }
                byBlock[typed.BlockIndex].Add(record);
            }
        }
        if (observed.Count != expectedCount)
            throw new InvalidDataException("Native source did not enrich every selected kind on every logical block.");

        var captures = new MapScanBlockCapture[blocks.Count];
        var blockProofs = new MapScanSharedBlockSourceProof[blocks.Count];
        for (int i = 0; i < blocks.Count; i++)
        {
            MapScanTargetBlock block = blocks[i];
            // Each 20x20 block must have exactly four *received*, owned
            // 10x10 native AOI receipts. Geometry is used to VERIFY expected
            // membership, never to synthesize a missing proof.
            var expectedAois = new[]
            {
                block.MinY / 10 * 100 + block.MinX / 10,
                block.MinY / 10 * 100 + block.MinX / 10 + 1,
                (block.MinY / 10 + 1) * 100 + block.MinX / 10,
                (block.MinY / 10 + 1) * 100 + block.MinX / 10 + 1
            };
            if (expectedAois.Any(index => aoiByIndex[index] is null))
                throw new InvalidDataException("Logical block lacks one of four actual native AOI proofs.");
            Map014ReceivedNativeAoiProof first = aoiByIndex[expectedAois[0]];
            if (expectedAois.Any(index =>
                    !string.Equals(aoiByIndex[index].NativeReplyRequestId,
                        first.NativeReplyRequestId, StringComparison.Ordinal) ||
                    aoiByIndex[index].NativeReplyAtUnixMs != first.NativeReplyAtUnixMs))
                throw new InvalidDataException(
                    "Native 20x20 logical block has no one correlated received response for all four AOIs.");
            // The proof indices are copied from the received owner-authenticated
            // ingress observations, never generated by the expected block grid.
            blockProofs[i] = new MapScanSharedBlockSourceProof(
                i, expectedAois.Select(index => aoiByIndex[index].NativeAoiIndex).ToArray(),
                first.NativeReplyRequestId, first.NativeReplyAtUnixMs);
            captures[i] = new MapScanBlockCapture(
                pinnedRequest.ServerId, pinnedRequest.WorldId, i,
                JsonSerializer.Serialize(new
                {
                    protocol = "map014_native_shared_received_v0_not_live",
                    blockIndex = i,
                    groupGeneration = pinnedOwner.GroupGeneration,
                    nativeAoiIndices = blockProofs[i].NativePointAoiIndices,
                    nativeSource = "WorldPointManager._curViewIndex",
                    diagnosticOnly = true
                }),
                byBlock[i].ToArray());
        }
        return (captures, blockProofs, home);
    }

    private static bool ExpectedKindSource(string kind, string source) => kind switch
    {
        "city" => source == "WorldPointManager._pointInfos",
        // Name-only source flags never prove Resource or March freshness, or
        // per-kind completion for the remaining mixed native categories.
        _ => false
    };

    private bool MatchesEvidenceOwner(
        string run, int server, long world, string launch, string profile, int pid, string generation) =>
        string.Equals(run, pinnedOwner.RunId, StringComparison.Ordinal) &&
        server == pinnedOwner.ServerId && world == pinnedOwner.WorldId &&
        string.Equals(launch, pinnedOwner.LaunchSessionId, StringComparison.Ordinal) &&
        string.Equals(profile, pinnedOwner.ProfileId, StringComparison.Ordinal) &&
        pid == pinnedOwner.GameProcessId &&
        string.Equals(generation, pinnedOwner.GroupGeneration, StringComparison.Ordinal);

    private bool MatchesOwner(MapScanSharedAcquisitionOwner? owner) =>
        owner is not null && MatchesEvidenceOwner(
            owner.RunId, owner.ServerId, owner.WorldId, owner.LaunchSessionId,
            owner.ProfileId, owner.GameProcessId, owner.GroupGeneration) &&
        owner.SelectedTypes is not null &&
        owner.SelectedTypes.SequenceEqual(pinnedRequest.SelectedTypes, StringComparer.Ordinal);

    private void CheckRequest(MapScanExecutionRequest request)
    {
        if (request is null || !SameRequest(request, pinnedRequest))
            throw new InvalidDataException("Native shared receipt source was called under a different run/selection.");
    }

    private static bool SameRequest(MapScanExecutionRequest a, MapScanExecutionRequest b) =>
        a.RunId == b.RunId && a.ServerId == b.ServerId && a.WorldId == b.WorldId &&
        a.TileWidth == b.TileWidth && a.TileHeight == b.TileHeight &&
        a.RequestedConcurrency == b.RequestedConcurrency &&
        a.MaxAttemptsPerBlock == b.MaxAttemptsPerBlock &&
        a.PlayerTileX == b.PlayerTileX && a.PlayerTileY == b.PlayerTileY &&
        a.ScanMode == b.ScanMode && a.LaunchSessionId == b.LaunchSessionId &&
        a.LiveServerId == b.LiveServerId &&
        a.SelectedTypes.SequenceEqual(b.SelectedTypes, StringComparer.Ordinal);

    private static void VerifyBlocks(IReadOnlyList<MapScanTargetBlock> blocks)
    {
        if (blocks is null || blocks.Count != 2500)
            throw new InvalidDataException("Native world group is missing exact 2500 logical blocks.");
        for (int i = 0; i < blocks.Count; i++)
        {
            MapScanTargetBlock block = blocks[i];
            int x = (i % 50) * 20, y = (i / 50) * 20;
            if (block.BlockIndex != i || block.MinX != x || block.MinY != y ||
                block.MaxX != x + 19 || block.MaxY != y + 19)
                throw new InvalidDataException("Native world group lost a 0..999 geometry seam.");
        }
    }
}
