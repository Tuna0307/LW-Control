using System.Text.Json;
using System.Text.Json.Nodes;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

/// <summary>
/// In-memory FUTURE protocol schema tests, based on names observed in the
/// hash-gated private FOV20 receipt. Never a native witness, never product
/// selection, never camera/game/installed-script traffic.
/// </summary>
internal static class Map014SharedSourceAdmissionChecks
{
    private static readonly OverviewMapScanSession Session =
        new("overview-owned-profile", "overview-owned-session", "challenge-hash",
            74123, @"C:\test-only\game.exe", "2026-10-14T01:00:00Z");

    private static readonly MapScanExecutionRequest Request =
        new("owned-shared-run", 2212, 0, 1000, 1000, ["city", "resource"], 8,
            PlayerTileX: 498, PlayerTileY: 63,
            LaunchSessionId: Session.SessionId, LiveServerId: 2212);

    private static void Require(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException("MAP014_SHARED_SOURCE: " + message);
    }

    private static JsonArray Integers(IEnumerable<int> indices) =>
        JsonSerializer.SerializeToNode(indices.ToArray())!.AsArray();

    private static JsonObject Stage(string stage, int x, int y, int[] indices, bool recv = false) =>
        new()
        {
            ["stage"] = stage,
            ["tileX"] = x,
            ["tileY"] = y,
            ["currentIndices"] = Integers(indices),
            ["geometry"] = new JsonObject { ["fov"] = 20.0 },
            ["flags"] = new JsonObject { ["isRecvViewPoints"] = recv },
        };

    private static int[] Footprint(int x, int y)
    {
        int col = x / 10, row = y / 10;
        int minX = Math.Clamp(col - 2, 0, 94);
        int minY = Math.Clamp(row - 2, 0, 95);
        return Enumerable.Range(minY, 5)
            .SelectMany(r => Enumerable.Range(minX, 6).Select(c => r * 100 + c))
            .ToArray();
    }

    private static JsonObject OriginalGeometry() => new()
    {
        ["fov"] = 10.0,
        ["aspect"] = 1.9075,
        ["zoom"] = 105.0,
        ["position"] = new JsonObject { ["x"] = 498.0, ["y"] = 63.0, ["z"] = 100.0 },
        ["rotation"] = new JsonObject { ["x"] = 0.0, ["y"] = 0.0, ["z"] = 0.0, ["w"] = 1.0 },
        ["projection"] = new JsonArray(5.9, 0, 0, 5.9),
        ["anchors"] = new JsonArray(498, 63),
    };

    private static JsonObject[] Group()
    {
        (int X, int Y)[] targets = [(455, 425), (545, 495), (655, 565)];
        var group = new JsonObject[3];
        for (int i = 0; i < 3; i++)
        {
            int x = targets[i].X, y = targets[i].Y;
            int beforeX = i == 0 ? 498 : targets[i - 1].X;
            int beforeY = i == 0 ? 63 : targets[i - 1].Y;
            int[] nativeIndices = Footprint(x, y);
            string id = "native-request-" + i;
            long requestedAt = 1_800_000_000_000L + i * 4_000L;
            group[i] = new JsonObject
            {
                ["state"] = "proven",
                ["requestMode"] = "coverage",
                ["scanRunId"] = Request.RunId,
                ["profileId"] = Session.ProfileId,
                ["launchSessionId"] = Session.SessionId,
                ["challenge"] = Session.Challenge,
                ["gamePid"] = Session.GamePid,
                ["serverId"] = Request.ServerId,
                ["worldId"] = Request.WorldId,
                ["selectedTypes"] = JsonSerializer.SerializeToNode(Request.SelectedTypes),
                ["regionOrdinal"] = i,
                ["groupGeneration"] = "owned-generation-1",
                ["requestId"] = id,
                ["nativeReplyRequestId"] = id,
                ["requestStartedAtUnixMs"] = requestedAt,
                ["nativeReplyAtUnixMs"] = requestedAt + 500L,
                ["map013NativeReplyState"] = "matched",
                ["responseFlagsTransitioned"] = true,
                ["positionRestoredBeforeResponse"] = false,
                ["positionRestoredAfterResponse"] = i == 2,
                ["map013PositionRetained"] = i != 2,
                ["targetTileX"] = x,
                ["targetTileY"] = y,
                ["targetAoiIndex"] = y / 10 * 100 + x / 10,
                ["homeTileX"] = 498,
                ["homeTileY"] = 63,
                ["postServerLod"] = 0,
                ["postBlockSize"] = 10,
                ["postBlockCount"] = 100,
                ["nativeCurrentSetCount"] = nativeIndices.Length,
                ["requestedIndices"] = Integers(nativeIndices),
                ["map014SubscriptionStages"] = new JsonArray(
                    Stage("before-shift", beforeX, beforeY, nativeIndices, recv: true),
                    Stage("post-lens", x, y, nativeIndices),
                    Stage("post-update", x, y, nativeIndices)),
            };
        }
        group[2]["cameraGeometryRestored"] = true;
        group[2]["cameraGeometryConfirmed"] = true;
        group[2]["cameraProjectionRestored"] = true;
        group[2]["restoredTileX"] = 498;
        group[2]["restoredTileY"] = 63;
        group[2]["originalCameraGeometry"] = OriginalGeometry();
        group[2]["restoredCameraGeometry"] = OriginalGeometry();
        group[2]["homeRestoreRequestId"] = "owned-home-restore-request";
        group[2]["homeNativeReplyRequestId"] = "owned-home-restore-request";
        group[2]["homeNativeReplyState"] = "matched";
        group[2]["homeNativeReplyAtUnixMs"] = 1_800_000_008_000L + 1_000L;
        return group;
    }

    private static JsonElement[] Pack(JsonObject[] group) =>
        group.Select(node => JsonSerializer.SerializeToElement(node)).ToArray();

    private static CurrentClientMapBlockSource Controlled(
        Func<OverviewMapScanSession?> getReady,
        Func<OverviewMapScanSession, bool> sameOwner) =>
        new(getReady, "controlled-no-access-overview", "controlled-no-access-probe",
            new CurrentClientMapBlockSourceHooks
            {
                ReadAllBytes = _ => throw new InvalidOperationException("Admission must not read native files"),
                WriteTextAtomic = (_, _) => throw new InvalidOperationException("Admission must not write native commands"),
                DelayAsync = (_, _) => throw new InvalidOperationException("Admission must not wait on native responses"),
            },
            matchesOwnedSession: sameOwner);

    private static void Reject(string reason, Action<JsonObject[]> change)
    {
        JsonObject[] group = Group();
        change(group);
        try
        {
            CurrentClientMapBlockSource.ValidateSharedSourceAdmissionSchema(Session, Request, Pack(group));
        }
        catch (InvalidDataException)
        {
            return;
        }
        throw new InvalidOperationException("MAP014_SHARED_SOURCE incorrectly admitted " + reason);
    }

    private static void RejectRequest(string reason, Func<MapScanExecutionRequest, MapScanExecutionRequest> change)
    {
        try
        {
            CurrentClientMapBlockSource.ValidateSharedSourceAdmissionSchema(Session, change(Request), Pack(Group()));
        }
        catch (InvalidDataException)
        {
            return;
        }
        throw new InvalidOperationException("MAP014_SHARED_SOURCE incorrectly admitted " + reason);
    }

    internal static void Run()
    {
        int gets = 0, matched = 0;
        OverviewMapScanSession? current = Session;
        CurrentClientMapBlockSource source = Controlled(
            () => { gets++; return current; },
            expected => { matched++; return current == expected; });
        CurrentClientSharedSourceAdmissionDiagnostic positive =
            source.DiagnoseSharedSourceAdmission(Request, Pack(Group()));
        Require(positive.DiagnosticOnly && positive.VerifiedRegions == 3 &&
                positive.DistinctNativeAoiIndices > 0 && gets == 1 && matched == 2 &&
                positive.PinnedOwner.RunId == Request.RunId &&
                positive.PinnedOwner.ProfileId == Session.ProfileId &&
                positive.PinnedOwner.GameProcessId == Session.GamePid &&
                positive.PinnedOwner.LaunchSessionId == Session.SessionId &&
                positive.PinnedOwner.GroupGeneration == "owned-generation-1",
            "read-only hypothetical A/B/C schema was not pinned to live Overview + request");

        current = Session with { SessionId = "replacement-overview-session" };
        try
        {
            source.DiagnoseSharedSourceAdmission(Request, Pack(Group()));
            throw new InvalidOperationException("replacement Overview session was admitted");
        }
        catch (InvalidDataException) { }
        current = null;
        try
        {
            source.DiagnoseSharedSourceAdmission(Request, Pack(Group()));
            throw new InvalidOperationException("missing Overview owner was admitted");
        }
        catch (BridgeCommandException) { }
        int admissionChecks = 0;
        CurrentClientMapBlockSource changingOwner = Controlled(
            () => Session, _ => ++admissionChecks == 1);
        try
        {
            changingOwner.DiagnoseSharedSourceAdmission(Request, Pack(Group()));
            throw new InvalidOperationException("Overview owner changed after schema check but was admitted");
        }
        catch (BridgeCommandException) { }
        Require(admissionChecks == 2, "source did not revalidate live Overview ownership after schema check");

        RejectRequest("foreign run", request => request with { RunId = "old-run" });
        RejectRequest("foreign server", request => request with { ServerId = 2213 });
        RejectRequest("foreign world", request => request with { WorldId = 1 });
        RejectRequest("missing launch session", request => request with { LaunchSessionId = null });
        RejectRequest("foreign launch session", request => request with { LaunchSessionId = "successor" });
        RejectRequest("foreign selected kinds", request => request with { SelectedTypes = ["city", "truck"] });
        RejectRequest("duplicate selected kinds", request => request with { SelectedTypes = ["city", "city"] });
        RejectRequest("partial width", request => request with { TileWidth = 980 });
        RejectRequest("missing live server", request => request with { LiveServerId = null });
        RejectRequest("missing origin", request => request with { PlayerTileX = null });

        Reject("stale previous profile", regions => regions[1]["profileId"] = "retired-profile");
        Reject("retired game PID", regions => regions[1]["gamePid"] = 123);
        Reject("stale challenge", regions => regions[0]["challenge"] = "old-challenge");
        Reject("wrong run echo", regions => regions[1]["scanRunId"] = "different-run");
        Reject("wrong server echo", regions => regions[2]["serverId"] = 2213);
        Reject("wrong world echo", regions => regions[0]["worldId"] = 1);
        Reject("wrong kind echo", regions => regions[1]["selectedTypes"] = new JsonArray("city", "truck"));
        Reject("group generation replay", regions => regions[2]["groupGeneration"] = "older-group");
        Reject("duplicate request IDs", regions => regions[1]["requestId"] = "native-request-0");
        Reject("unmatched reply", regions => regions[1]["map013NativeReplyState"] = "waiting");
        Reject("wrong native reply request ID", regions => regions[1]["nativeReplyRequestId"] = "old-request");
        Reject("false transitioned flags", regions => regions[1]["responseFlagsTransitioned"] = false);
        Reject("replayed old reply time", regions => regions[1]["nativeReplyAtUnixMs"] = 1_800_000_000_000L);
        Reject("overlapping native requests", regions => regions[1]["requestStartedAtUnixMs"] = 1_800_000_000_001L);
        Reject("missing target AOI", regions =>
        {
            JsonArray indices = regions[1]["requestedIndices"]!.AsArray();
            int offset = Enumerable.Range(0, indices.Count)
                .Single(index => indices[index]!.GetValue<int>() == 4954);
            indices.RemoveAt(offset);
        });
        Reject("stale old AOI native current set", regions =>
            regions[1]["map014SubscriptionStages"]![2]!["currentIndices"] =
                regions[0]["requestedIndices"]!.DeepClone());
        Reject("holed footprint", regions =>
            regions[1]["requestedIndices"]!.AsArray().RemoveAt(1));
        Reject("incorrect FOV", regions =>
            regions[1]["map014SubscriptionStages"]![1]!["geometry"]!["fov"] = 120);
        Reject("post-update wrong physical tile", regions =>
            regions[1]["map014SubscriptionStages"]![2]!["tileX"] = 455);
        Reject("fresh reply flag not rearmed", regions =>
            regions[1]["map014SubscriptionStages"]![2]!["flags"]!["isRecvViewPoints"] = true);
        Reject("lost A->B retained camera continuity", regions =>
            regions[1]["map014SubscriptionStages"]![0]!["tileX"] = 498);
        Reject("premature B home restore", regions => regions[1]["positionRestoredAfterResponse"] = true);
        Reject("final C absent camera restoration", regions => regions[2]["cameraGeometryRestored"] = false);
        Reject("final camera projection differs", regions =>
            regions[2]["restoredCameraGeometry"]!["projection"]![0] = 0.5);
        Reject("fabricated empty camera checkpoints", regions =>
        {
            regions[2]["originalCameraGeometry"] = new JsonObject();
            regions[2]["restoredCameraGeometry"] = new JsonObject();
        });
        Reject("final home tile wrong", regions => regions[2]["restoredTileX"] = 450);
        Reject("stale final home reply", regions => regions[2]["homeNativeReplyState"] = "waiting");
        Reject("old home response request ID", regions =>
            regions[2]["homeNativeReplyRequestId"] = "previous-home");
        Reject("home request reuses an earlier target request", regions =>
        {
            regions[2]["homeRestoreRequestId"] = "native-request-0";
            regions[2]["homeNativeReplyRequestId"] = "native-request-0";
        });
        Reject("missing separate home view reply time", regions =>
            regions[2]["homeNativeReplyAtUnixMs"] = 1_800_000_008_000L);
        Reject("unbounded late home response", regions =>
            regions[2]["homeNativeReplyAtUnixMs"] = 1_800_000_080_000L);
        Reject("private receipt missing new production run protocol", regions =>
            regions[1].Remove("scanRunId"));

        Console.WriteLine(
            "MAP014_SHARED_SOURCE_ADMISSION_DIAGNOSTIC_OK A-B-C=3 owner/session/group AOI+nativeReply+oneHomeRestore negatives=30+ native=false selector=false");
    }
}
