using System.Text.Json;

namespace LWBridge.Desktop;

/// <summary>
/// Read-only source-schema diagnostic, NOT admission to a live native group.
/// The saved private A/B/C FOV20 experiment lacks mandatory production
/// run/server/world/selection/group and fresh home-reply identity fields.
/// Passing a synthetic future-protocol receipt never authorizes camera shifts,
/// publication, or the IMapScanSharedAcquisitionSource production selector.
/// </summary>
internal sealed record CurrentClientSharedSourceAdmissionDiagnostic(
    MapScanSharedAcquisitionOwner PinnedOwner,
    int VerifiedRegions,
    int DistinctNativeAoiIndices,
    bool DiagnosticOnly = true);

internal sealed partial class CurrentClientMapBlockSource
{
    /// <summary>
    /// Pure read-only, diagnostic-only preflight against the *current* owned
    /// Overview session. Does not write commands or touch a game process.
    /// A native source must independently match live PID/profile/session and
    /// group generation again immediately before any future camera operation.
    /// </summary>
    internal CurrentClientSharedSourceAdmissionDiagnostic DiagnoseSharedSourceAdmission(
        MapScanExecutionRequest request,
        IReadOnlyList<JsonElement> consecutiveReceipts)
    {
        ArgumentNullException.ThrowIfNull(request);
        OverviewMapScanSession session = RequireReadySession();
        RequireSameSession(session);
        CurrentClientSharedSourceAdmissionDiagnostic result =
            ValidateSharedSourceAdmissionSchema(session, request, consecutiveReceipts);
        RequireSameSession(session);
        return result;
    }

    internal static CurrentClientSharedSourceAdmissionDiagnostic ValidateSharedSourceAdmissionSchema(
        OverviewMapScanSession session,
        MapScanExecutionRequest request,
        IReadOnlyList<JsonElement> consecutiveReceipts)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(session.ProfileId) ||
            string.IsNullOrWhiteSpace(session.SessionId) ||
            string.IsNullOrWhiteSpace(session.Challenge) ||
            session.GamePid <= 0 ||
            string.IsNullOrWhiteSpace(request.RunId) ||
            string.IsNullOrWhiteSpace(request.LaunchSessionId) ||
            !string.Equals(request.LaunchSessionId, session.SessionId, StringComparison.Ordinal) ||
            request.ServerId <= 0 || request.LiveServerId != request.ServerId ||
            request.WorldId != 0 || request.TileWidth != 1000 || request.TileHeight != 1000 ||
            request.PlayerTileX is not >= 0 or >= 1000 ||
            request.PlayerTileY is not >= 0 or >= 1000 ||
            request.SelectedTypes is null || request.SelectedTypes.Count == 0 ||
            request.SelectedTypes.Distinct(StringComparer.Ordinal).Count() != request.SelectedTypes.Count ||
            request.SelectedTypes.Any(kind => !MapScanContract.AllTypes.Contains(kind, StringComparer.Ordinal)))
            throw new InvalidDataException("Shared native diagnostic lacks a valid exact Overview scan owner/world/selection.");
        if (consecutiveReceipts is null || consecutiveReceipts.Count != 3)
            throw new InvalidDataException("Shared native diagnostic must contain exactly ordered A/B/C receipts.");

        const int MaxNativeResponseMilliseconds = 20_000;
        string? group = null;
        long lastResponseAt = 0;
        int lastNativeReceiveCount = -1;
        int[]? lastFootprint = null;
        int expectedBeforeX = request.PlayerTileX.Value;
        int expectedBeforeY = request.PlayerTileY.Value;
        var uniqueRequests = new HashSet<string>(StringComparer.Ordinal);
        var aoiUnion = new HashSet<int>();
        for (int ordinal = 0; ordinal < 3; ordinal++)
        {
            JsonElement receipt = consecutiveReceipts[ordinal];
            string requestId = SharedString(receipt, "requestId");
            string generation = SharedString(receipt, "groupGeneration");
            if (ordinal == 0) group = generation;
            if (!string.Equals(group, generation, StringComparison.Ordinal) ||
                !uniqueRequests.Add(requestId) ||
                !SharedEquals(receipt, "profileId", session.ProfileId) ||
                !SharedEquals(receipt, "launchSessionId", session.SessionId) ||
                !SharedEquals(receipt, "challenge", session.Challenge) ||
                SharedInt(receipt, "gamePid") != session.GamePid ||
                !SharedEquals(receipt, "scanRunId", request.RunId) ||
                SharedInt(receipt, "serverId") != request.ServerId ||
                SharedLong(receipt, "worldId") != request.WorldId ||
                SharedInt(receipt, "regionOrdinal") != ordinal ||
                !SharedEquals(receipt, "requestMode", "coverage") ||
                !SharedEquals(receipt, "state", "proven") ||
                !SharedEquals(receipt, "map013NativeReplyState", "matched") ||
                !SharedEquals(receipt, "nativeReplyRequestId", requestId) ||
                !SharedBool(receipt, "responseFlagsTransitioned", true) ||
                !SharedBool(receipt, "positionRestoredBeforeResponse", false) ||
                SharedInt(receipt, "homeTileX") != request.PlayerTileX ||
                SharedInt(receipt, "homeTileY") != request.PlayerTileY ||
                !SharedKindsEqual(receipt, request.SelectedTypes))
                throw new InvalidDataException("Shared native diagnostic changed group/session/run/selection or lacked a fresh matched target reply.");
            long requestedAt = SharedLong(receipt, "requestStartedAtUnixMs");
            long respondedAt = SharedLong(receipt, "nativeReplyAtUnixMs");
            if (requestedAt <= lastResponseAt ||
                respondedAt <= requestedAt ||
                respondedAt - requestedAt > MaxNativeResponseMilliseconds)
                throw new InvalidDataException("Shared native diagnostic response is stale, overlapping or outside its request window.");

            int x = SharedInt(receipt, "targetTileX"), y = SharedInt(receipt, "targetTileY");
            if (x is < 0 or >= 1000 || y is < 0 or >= 1000 ||
                (x == expectedBeforeX && y == expectedBeforeY) ||
                SharedInt(receipt, "targetAoiIndex") != y / 10 * 100 + x / 10 ||
                SharedInt(receipt, "postServerLod") != 0 ||
                SharedInt(receipt, "postBlockSize") != 10 ||
                SharedInt(receipt, "postBlockCount") != 100)
                throw new InvalidDataException("Shared native diagnostic target, LOD or AOI geometry invalid.");

            JsonElement before = SharedStage(receipt, "before-shift");
            JsonElement shifted = SharedStage(receipt, "after-shift");
            JsonElement lens = SharedStage(receipt, "post-lens");
            JsonElement updated = SharedStage(receipt, "post-update");
            JsonElement firstPoll = SharedStage(receipt, "first-poll");
            long beforeAt = SharedLong(before, "observedAtUnixMs");
            long shiftedAt = SharedLong(shifted, "observedAtUnixMs");
            long lensAt = SharedLong(lens, "observedAtUnixMs");
            long updateAt = SharedLong(updated, "observedAtUnixMs");
            long pollAt = SharedLong(firstPoll, "observedAtUnixMs");
            // A request-id echoed beside booleans alone cannot prove an
            // order-correct native response. The future producer must
            // timestamp each actual stage in the same clock domain.
            if (beforeAt <= lastResponseAt || beforeAt >= requestedAt ||
                shiftedAt <= beforeAt || lensAt <= shiftedAt ||
                lensAt >= requestedAt || updateAt <= requestedAt ||
                pollAt <= updateAt || pollAt >= respondedAt)
                throw new InvalidDataException(
                    "Shared native diagnostic lacks a monotonic native stage/request/reply chronology.");
            if (SharedInt(before, "tileX") != expectedBeforeX ||
                SharedInt(before, "tileY") != expectedBeforeY ||
                SharedInt(shifted, "tileX") != x || SharedInt(shifted, "tileY") != y ||
                SharedInt(lens, "tileX") != x || SharedInt(lens, "tileY") != y ||
                SharedInt(firstPoll, "tileX") != x || SharedInt(firstPoll, "tileY") != y ||
                !SharedStageFlag(before, true) ||
                !SharedStageFlag(shifted, true) ||
                !SharedStageFlag(lens, true) ||
                !SharedStageFlag(updated, false) ||
                !SharedStageFlag(firstPoll, false))
                throw new InvalidDataException("Shared native diagnostic camera continuity lost between retained regions.");
            if (!lens.TryGetProperty("geometry", out JsonElement geometry) ||
                !geometry.TryGetProperty("fov", out JsonElement fov) ||
                fov.ValueKind != JsonValueKind.Number ||
                !fov.TryGetDouble(out double applied) ||
                !double.IsFinite(applied) || Math.Abs(applied - 20.0) > 0.0001)
                throw new InvalidDataException("Shared native diagnostic did not apply FOV20 at the native source.");
            if (SharedInt(updated, "tileX") != x || SharedInt(updated, "tileY") != y)
                throw new InvalidDataException("Shared native diagnostic did not arm a fresh target-view reply.");

            int[] footprint = SharedAoiIndices(receipt, "requestedIndices");
            int[] current = SharedAoiIndices(updated, "currentIndices");
            int[] beforeCurrent = SharedAoiIndices(before, "currentIndices");
            int[] shiftedCurrent = SharedAoiIndices(shifted, "currentIndices");
            int[] lensCurrent = SharedAoiIndices(lens, "currentIndices");
            int[] pollCurrent = SharedAoiIndices(firstPoll, "currentIndices");
            if (SharedInt(receipt, "nativeCurrentSetCount") != footprint.Length ||
                !footprint.ToHashSet().SetEquals(current) ||
                !footprint.ToHashSet().SetEquals(pollCurrent) ||
                !beforeCurrent.ToHashSet().SetEquals(shiftedCurrent) ||
                !beforeCurrent.ToHashSet().SetEquals(lensCurrent) ||
                !SharedIsAoiRectangle(beforeCurrent) ||
                (lastFootprint is not null &&
                 !beforeCurrent.ToHashSet().SetEquals(lastFootprint)) ||
                beforeCurrent.ToHashSet().SetEquals(footprint) ||
                !SharedIsAoiRectangle(footprint) ||
                !footprint.Contains(y / 10 * 100 + x / 10))
                throw new InvalidDataException("Shared native diagnostic retained a stale, holed or target-missing _curViewIndex footprint.");
            lastNativeReceiveCount = SharedNativeReplyEvidence(
                receipt, "nativeReplyEvidence", requestId, group!, session, request,
                x, y, respondedAt, footprint, lastNativeReceiveCount);
            lastResponseAt = respondedAt;
            lastFootprint = footprint;
            aoiUnion.UnionWith(footprint);

            if (ordinal < 2)
            {
                if (!SharedBool(receipt, "map013PositionRetained", true) ||
                    !SharedBool(receipt, "positionRestoredAfterResponse", false))
                    throw new InvalidDataException("Shared native diagnostic prematurely restored an intermediate camera.");
            }
            else
            {
                if (!SharedBool(receipt, "positionRestoredAfterResponse", true) ||
                    !SharedBool(receipt, "cameraGeometryRestored", true) ||
                    !SharedBool(receipt, "cameraGeometryConfirmed", true) ||
                    !SharedBool(receipt, "cameraProjectionRestored", true) ||
                    SharedInt(receipt, "restoredTileX") != request.PlayerTileX ||
                    SharedInt(receipt, "restoredTileY") != request.PlayerTileY ||
                    !receipt.TryGetProperty("originalCameraGeometry", out JsonElement original) ||
                    !receipt.TryGetProperty("restoredCameraGeometry", out JsonElement restored) ||
                    original.ValueKind != JsonValueKind.Object || restored.ValueKind != JsonValueKind.Object ||
                    !SharedHasCameraCheckpoint(original) ||
                    !JsonElement.DeepEquals(original, restored))
                    throw new InvalidDataException("Shared native diagnostic failed the one original-home camera/projection restoration.");
                string restoreId = SharedString(receipt, "homeRestoreRequestId");
                long homeRequestAt = SharedLong(receipt, "homeRestoreStartedAtUnixMs");
                long homeReplyAt = SharedLong(receipt, "homeNativeReplyAtUnixMs");
                long restoredAt = SharedLong(receipt, "restoredCameraObservedAtUnixMs");
                if (homeRequestAt <= respondedAt ||
                    homeReplyAt <= homeRequestAt ||
                    homeReplyAt - homeRequestAt > MaxNativeResponseMilliseconds ||
                    restoredAt < homeReplyAt ||
                    restoredAt - homeReplyAt > MaxNativeResponseMilliseconds)
                    throw new InvalidDataException(
                        "Shared native diagnostic has stale home request/reply or an unverified post-reply camera.");
                if (uniqueRequests.Contains(restoreId) ||
                    !SharedEquals(receipt, "homeNativeReplyRequestId", restoreId) ||
                    !SharedEquals(receipt, "homeNativeReplyState", "matched") ||
                    homeReplyAt <= respondedAt)
                    throw new InvalidDataException("Shared native diagnostic lacks a separate correlated fresh home-view reply.");
                if (!receipt.TryGetProperty("homeNativeReplyEvidence", out JsonElement homeEvidence))
                    throw new InvalidDataException("Shared native diagnostic lacks a separate native home reply event.");
                int[] homeIndices = SharedAoiIndices(homeEvidence, "currentIndicesAtReply");
                int homeAoi = request.PlayerTileY.Value / 10 * 100 + request.PlayerTileX.Value / 10;
                if (!SharedIsAoiRectangle(homeIndices) || !homeIndices.Contains(homeAoi) ||
                    (lastFootprint is not null && homeIndices.ToHashSet().SetEquals(lastFootprint)))
                    throw new InvalidDataException("Shared native diagnostic home AOI still reflects stale C footprint.");
                _ = SharedNativeReplyEvidence(
                    receipt, "homeNativeReplyEvidence", restoreId, group!, session, request,
                    request.PlayerTileX.Value, request.PlayerTileY.Value,
                    homeReplyAt, homeIndices, lastNativeReceiveCount);
            }
            expectedBeforeX = x;
            expectedBeforeY = y;
        }
        return new CurrentClientSharedSourceAdmissionDiagnostic(
            new MapScanSharedAcquisitionOwner(
                request.RunId, request.ServerId, request.WorldId, session.SessionId,
                request.SelectedTypes.ToArray(), session.ProfileId, session.GamePid, group!),
            3, aoiUnion.Count);
    }

    private static bool SharedEquals(JsonElement obj, string name, string expected) =>
        obj.ValueKind == JsonValueKind.Object &&
        obj.TryGetProperty(name, out JsonElement value) &&
        value.ValueKind == JsonValueKind.String &&
        string.Equals(value.GetString(), expected, StringComparison.Ordinal);

    private static bool SharedBool(JsonElement obj, string name, bool expected) =>
        obj.ValueKind == JsonValueKind.Object &&
        obj.TryGetProperty(name, out JsonElement value) &&
        (expected ? value.ValueKind == JsonValueKind.True : value.ValueKind == JsonValueKind.False);

    private static bool SharedStageFlag(JsonElement stage, bool expected) =>
        stage.TryGetProperty("flags", out JsonElement flags) &&
        SharedBool(flags, "isRecvViewPoints", expected);

    private static int SharedNativeReplyEvidence(
        JsonElement receipt,
        string field,
        string requestId,
        string generation,
        OverviewMapScanSession session,
        MapScanExecutionRequest request,
        int targetX,
        int targetY,
        long replyAt,
        int[] expectedIndices,
        int previousReceiveCount)
    {
        if (!receipt.TryGetProperty(field, out JsonElement native) ||
            native.ValueKind != JsonValueKind.Object ||
            !SharedEquals(native, "source", "WorldPointManager.ReceiveViewPoints") ||
            !SharedEquals(native, "requestId", requestId) ||
            !SharedEquals(native, "scanRunId", request.RunId) ||
            !SharedEquals(native, "groupGeneration", generation) ||
            !SharedEquals(native, "profileId", session.ProfileId) ||
            !SharedEquals(native, "launchSessionId", session.SessionId) ||
            SharedInt(native, "gamePid") != session.GamePid ||
            SharedInt(native, "serverId") != request.ServerId ||
            SharedLong(native, "worldId") != request.WorldId ||
            SharedInt(native, "targetTileX") != targetX ||
            SharedInt(native, "targetTileY") != targetY ||
            SharedInt(native, "targetAoiIndex") !=
                targetY / 10 * 100 + targetX / 10 ||
            SharedLong(native, "observedAtUnixMs") != replyAt)
            throw new InvalidDataException(
                "Shared native diagnostic has scalar response flags but no exact owned native reply event.");
        int countBefore = SharedInt(native, "nativeReceiveCountBefore");
        int countAfter = SharedInt(native, "nativeReceiveCountAfter");
        if (countBefore < 0 || countAfter <= countBefore ||
            (previousReceiveCount >= 0 && countBefore < previousReceiveCount) ||
            !SharedAoiIndices(native, "currentIndicesAtReply")
                .ToHashSet().SetEquals(expectedIndices))
            throw new InvalidDataException(
                "Shared native diagnostic has no fresh native receive counter/footprint advancement.");
        // Typed sampler fields remain independently unverified without a native
        // packet provider. Do not promote this diagnostic to product admission.
        return countAfter;
    }

    private static string SharedString(JsonElement obj, string name) =>
        obj.ValueKind == JsonValueKind.Object &&
        obj.TryGetProperty(name, out JsonElement value) &&
        value.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new InvalidDataException("Shared native diagnostic missing nonblank " + name + ".");

    private static int SharedInt(JsonElement obj, string name) =>
        obj.ValueKind == JsonValueKind.Object &&
        obj.TryGetProperty(name, out JsonElement value) &&
        value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number)
            ? number
            : throw new InvalidDataException("Shared native diagnostic invalid " + name + ".");

    private static long SharedLong(JsonElement obj, string name) =>
        obj.ValueKind == JsonValueKind.Object &&
        obj.TryGetProperty(name, out JsonElement value) &&
        value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long number)
            ? number
            : throw new InvalidDataException("Shared native diagnostic invalid " + name + ".");

    private static bool SharedKindsEqual(JsonElement receipt, IReadOnlyList<string> selected) =>
        receipt.TryGetProperty("selectedTypes", out JsonElement kinds) &&
        kinds.ValueKind == JsonValueKind.Array &&
        kinds.GetArrayLength() == selected.Count &&
        kinds.EnumerateArray().Select(value =>
            value.ValueKind == JsonValueKind.String ? value.GetString() : null)
            .SequenceEqual(selected, StringComparer.Ordinal);

    private static bool SharedHasCameraCheckpoint(JsonElement camera)
    {
        foreach (string name in new[] { "fov", "aspect", "zoom" })
        {
            if (!camera.TryGetProperty(name, out JsonElement number) ||
                number.ValueKind != JsonValueKind.Number ||
                !number.TryGetDouble(out double value) ||
                !double.IsFinite(value) || value <= 0)
                return false;
        }
        foreach (string name in new[] { "position", "rotation", "projection", "anchors" })
        {
            if (!camera.TryGetProperty(name, out JsonElement part) ||
                part.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ||
                part.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
                return false;
        }
        return true;
    }

    private static JsonElement SharedStage(JsonElement receipt, string stage)
    {
        if (!receipt.TryGetProperty("map014SubscriptionStages", out JsonElement stages) ||
            stages.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Shared native diagnostic has no subscription stages.");
        JsonElement[] matches = stages.EnumerateArray()
            .Where(row => SharedEquals(row, "stage", stage)).ToArray();
        if (matches.Length != 1)
            throw new InvalidDataException("Shared native diagnostic requires one native " + stage + " stage.");
        return matches[0];
    }

    private static int[] SharedAoiIndices(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out JsonElement indices) ||
            indices.ValueKind != JsonValueKind.Array || indices.GetArrayLength() == 0)
            throw new InvalidDataException("Shared native diagnostic missing AOI " + name + ".");
        var seen = new HashSet<int>();
        foreach (JsonElement item in indices.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Number || !item.TryGetInt32(out int index) ||
                index is < 0 or >= 10_000 || !seen.Add(index))
                throw new InvalidDataException("Shared native diagnostic duplicated or invalid AOI " + name + ".");
        }
        return seen.ToArray();
    }

    private static bool SharedIsAoiRectangle(int[] indices)
    {
        int minX = indices.Min(index => index % 100);
        int maxX = indices.Max(index => index % 100);
        int minY = indices.Min(index => index / 100);
        int maxY = indices.Max(index => index / 100);
        if (indices.Length != (maxX - minX + 1) * (maxY - minY + 1))
            return false;
        var set = indices.ToHashSet();
        for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
                if (!set.Contains(y * 100 + x)) return false;
        return true;
    }
}
