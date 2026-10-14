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

    private static JsonObject Stage(
        string stage, int x, int y, int[] indices, long observedAtUnixMs, bool recv = false) =>
        new()
        {
            ["stage"] = stage,
            ["tileX"] = x,
            ["tileY"] = y,
            ["observedAtUnixMs"] = observedAtUnixMs,
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

    private static JsonObject ReplyEvent(
        string id, long observedAt, int[] nativeIndices, int before, int after,
        int targetX, int targetY) => new()
    {
        ["source"] = "WorldPointManager.ReceiveViewPoints",
        ["requestId"] = id,
        ["scanRunId"] = Request.RunId,
        ["groupGeneration"] = "owned-generation-1",
        ["profileId"] = Session.ProfileId,
        ["launchSessionId"] = Session.SessionId,
        ["gamePid"] = Session.GamePid,
        ["serverId"] = Request.ServerId,
        ["worldId"] = Request.WorldId,
        ["targetTileX"] = targetX,
        ["targetTileY"] = targetY,
        ["targetAoiIndex"] = targetY / 10 * 100 + targetX / 10,
        ["observedAtUnixMs"] = observedAt,
        ["nativeReceiveCountBefore"] = before,
        ["nativeReceiveCountAfter"] = after,
        ["currentIndicesAtReply"] = Integers(nativeIndices),
    };

    private static JsonObject[] Group((int X, int Y)[]? requestedTargets = null)
    {
        (int X, int Y)[] targets = requestedTargets ?? [(455, 425), (545, 495), (655, 565)];
        var group = new JsonObject[3];
        for (int i = 0; i < 3; i++)
        {
            int x = targets[i].X, y = targets[i].Y;
            int beforeX = i == 0 ? 498 : targets[i - 1].X;
            int beforeY = i == 0 ? 63 : targets[i - 1].Y;
            int[] nativeIndices = Footprint(x, y);
            int[] previousIndices = Footprint(beforeX, beforeY);
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
                ["nativeReplyEvidence"] = ReplyEvent(id, requestedAt + 500L, nativeIndices,
                    100 + i * 2, 101 + i * 2, x, y),
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
                    Stage("before-shift", beforeX, beforeY, previousIndices, requestedAt - 200, recv: true),
                    Stage("after-shift", x, y, previousIndices, requestedAt - 170, recv: true),
                    Stage("post-lens", x, y, previousIndices, requestedAt - 150, recv: true),
                    Stage("post-update", x, y, nativeIndices, requestedAt + 50),
                    Stage("first-poll", x, y, nativeIndices, requestedAt + 100)),
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
        group[2]["homeRestoreStartedAtUnixMs"] = 1_800_000_008_700L;
        group[2]["homeNativeReplyRequestId"] = "owned-home-restore-request";
        group[2]["homeNativeReplyState"] = "matched";
        group[2]["homeNativeReplyAtUnixMs"] = 1_800_000_008_000L + 1_000L;
        group[2]["homeNativeReplyEvidence"] = ReplyEvent(
            "owned-home-restore-request", 1_800_000_009_000L, Footprint(498, 63),
            106, 107, 498, 63);
        group[2]["restoredCameraObservedAtUnixMs"] = 1_800_000_009_200L;
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

    private static int RejectArchivedPrivateGroupsWhenPresent()
    {
        DirectoryInfo? workspace = new(AppContext.BaseDirectory);
        for (int parent = 0; parent < 12 && workspace is not null; parent++, workspace = workspace.Parent)
        {
            string archives = Path.Combine(workspace.FullName, "artifacts", "map-013-live");
            if (!Directory.Exists(archives)) continue;
            int checkedGroups = 0;
            foreach (string name in new[] {
                "native-causal-20261014-004239", "native-causal-20261014-004916"
            })
            {
                string[] paths = Enumerable.Range(1, 3)
                    .Select(index => Path.Combine(archives, name, $"sequence-region-{index}.json"))
                    .ToArray();
                if (!paths.All(File.Exists)) continue;
                // Read only: genuine private FOV20 receipts do NOT have the
                // new exact scan run/selection/typed native response fields.
                using JsonDocument a = JsonDocument.Parse(File.ReadAllText(paths[0]));
                JsonElement first = a.RootElement;
                Require(!first.TryGetProperty("scanRunId", out _) &&
                        !first.TryGetProperty("nativeReplyEvidence", out _),
                    "private native receipt unexpectedly claims future production schema");
                using JsonDocument b = JsonDocument.Parse(File.ReadAllText(paths[1]));
                using JsonDocument c = JsonDocument.Parse(File.ReadAllText(paths[2]));
                JsonElement[] receipts = [
                    first.Clone(), b.RootElement.Clone(), c.RootElement.Clone()
                ];
                var owner = new OverviewMapScanSession(
                    first.GetProperty("profileId").GetString()!,
                    first.GetProperty("launchSessionId").GetString()!,
                    first.GetProperty("challenge").GetString()!,
                    first.GetProperty("gamePid").GetInt32(),
                    "archive-read-only", "2026-10-14T00:00:00Z");
                var request = Request with {
                    RunId = "not-present-in-private-receipt",
                    LaunchSessionId = owner.SessionId
                };
                try
                {
                    CurrentClientMapBlockSource.ValidateSharedSourceAdmissionSchema(owner, request, receipts);
                }
                catch (InvalidDataException)
                {
                    checkedGroups++;
                    continue;
                }
                throw new InvalidOperationException(
                    "MAP014_SHARED_SOURCE promoted saved private native receipt to product admission: " + name);
            }
            return checkedGroups;
        }
        // Portable check runners without the optional private archive rely on
        // the synthetic missing-schema inverses below; never invent a witness.
        return 0;
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
        CurrentClientSharedSourceAdmissionDiagnostic seams =
            CurrentClientMapBlockSource.ValidateSharedSourceAdmissionSchema(
                Session, Request, Pack(Group([(0, 0), (999, 999), (10, 990)])));
        Require(seams.VerifiedRegions == 3 && seams.DistinctNativeAoiIndices > 0,
            "full-world native AOI seam tiles 0/999 were incorrectly rejected");
        int verifiedPrivateArchiveGroups = RejectArchivedPrivateGroupsWhenPresent();

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
        // RED: an attacker can preserve the scalar "matched"/transition booleans
        // while omitting any independently correlated native response event.
        Reject("forged scalar reply without typed native event", regions =>
            regions[1].Remove("nativeReplyEvidence"));
        Reject("native reply event attached to previous run request", regions =>
            regions[1]["nativeReplyEvidence"]!["requestId"] = "native-request-0");
        Reject("native reply event from foreign profile", regions =>
            regions[1]["nativeReplyEvidence"]!["profileId"] = "old-overview-profile");
        Reject("native reply event from previous group generation", regions =>
            regions[1]["nativeReplyEvidence"]!["groupGeneration"] = "stale-group");
        Reject("native reply event from old run", regions =>
            regions[1]["nativeReplyEvidence"]!["scanRunId"] = "stale-run");
        Reject("native reply event points at prior native tile", regions =>
            regions[1]["nativeReplyEvidence"]!["targetTileX"] = 455);
        Reject("native reply event from wrong world", regions =>
            regions[1]["nativeReplyEvidence"]!["worldId"] = 1);
        Reject("native reply event lacks incremented receive counter", regions =>
            regions[1]["nativeReplyEvidence"]!["nativeReceiveCountAfter"] =
                regions[1]["nativeReplyEvidence"]!["nativeReceiveCountBefore"]!.DeepClone());
        Reject("native reply event reused prior receive counter", regions =>
            regions[1]["nativeReplyEvidence"]!["nativeReceiveCountBefore"] = 0);
        Reject("missing stage native observation time", regions =>
            regions[1]["map014SubscriptionStages"]![2]!.AsObject().Remove("observedAtUnixMs"));
        Reject("post-update native stage before request", regions =>
            regions[1]["map014SubscriptionStages"]![3]!["observedAtUnixMs"] = 1_800_000_003_900L);
        Reject("first-poll native stage timestamp precedes post-update", regions =>
            regions[1]["map014SubscriptionStages"]![4]!["observedAtUnixMs"] = 1_800_000_004_010L);
        Reject("false pre-update native flag sequence", regions =>
            regions[1]["map014SubscriptionStages"]![1]!["flags"]!["isRecvViewPoints"] = false);
        Reject("stale first-poll AOI after update", regions =>
            regions[1]["map014SubscriptionStages"]![4]!["currentIndices"] =
                regions[0]["requestedIndices"]!.DeepClone());
        Reject("missing first-poll stage", regions =>
            regions[1]["map014SubscriptionStages"]!.AsArray().RemoveAt(4));
        Reject("replayed native source AOIs on shifted B target", regions =>
        {
            // The old footprint contains both old and nearby new targets.
            // A target-membership check by itself accepts no new discovery.
            regions[1]["targetTileX"] = 465;
            regions[1]["targetTileY"] = 435;
            regions[1]["targetAoiIndex"] = 4346;
            regions[1]["requestedIndices"] = regions[0]["requestedIndices"]!.DeepClone();
            regions[1]["nativeCurrentSetCount"] = regions[0]["nativeCurrentSetCount"]!.DeepClone();
            for (int stage = 1; stage <= 4; stage++)
            {
                regions[1]["map014SubscriptionStages"]![stage]!["tileX"] = 465;
                regions[1]["map014SubscriptionStages"]![stage]!["tileY"] = 435;
            }
            regions[1]["map014SubscriptionStages"]![3]!["currentIndices"] =
                regions[0]["requestedIndices"]!.DeepClone();
            regions[1]["map014SubscriptionStages"]![4]!["currentIndices"] =
                regions[0]["requestedIndices"]!.DeepClone();
            regions[1]["nativeReplyEvidence"]!["currentIndicesAtReply"] =
                regions[0]["requestedIndices"]!.DeepClone();
            regions[1]["nativeReplyEvidence"]!["targetTileX"] = 465;
            regions[1]["nativeReplyEvidence"]!["targetTileY"] = 435;
            regions[1]["nativeReplyEvidence"]!["targetAoiIndex"] = 4346;
            regions[2]["map014SubscriptionStages"]![0]!["tileX"] = 465;
            regions[2]["map014SubscriptionStages"]![0]!["tileY"] = 435;
        });
        Reject("missing home request start time", regions =>
            regions[2].Remove("homeRestoreStartedAtUnixMs"));
        Reject("home reply before its new request", regions =>
            regions[2]["homeRestoreStartedAtUnixMs"] = 1_800_000_009_100L);
        Reject("home reply without typed native response event", regions =>
            regions[2].Remove("homeNativeReplyEvidence"));
        Reject("home native reply event from wrong request", regions =>
            regions[2]["homeNativeReplyEvidence"]!["requestId"] = "native-request-2");
        Reject("home native reply event retained C footprint", regions =>
            regions[2]["homeNativeReplyEvidence"]!["currentIndicesAtReply"] =
                regions[2]["requestedIndices"]!.DeepClone());
        Reject("camera checkpoint sampled before home response", regions =>
            regions[2]["restoredCameraObservedAtUnixMs"] = 1_800_000_008_990L);
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
            regions[1]["map014SubscriptionStages"]![3]!["currentIndices"] =
                regions[0]["requestedIndices"]!.DeepClone());
        Reject("holed footprint", regions =>
            regions[1]["requestedIndices"]!.AsArray().RemoveAt(1));
        Reject("incorrect FOV", regions =>
            regions[1]["map014SubscriptionStages"]![2]!["geometry"]!["fov"] = 120);
        Reject("post-update wrong physical tile", regions =>
            regions[1]["map014SubscriptionStages"]![3]!["tileX"] = 455);
        Reject("fresh reply flag not rearmed", regions =>
            regions[1]["map014SubscriptionStages"]![3]!["flags"]!["isRecvViewPoints"] = true);
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
            "MAP014_SHARED_SOURCE_ADMISSION_DIAGNOSTIC_OK A-B-C=3 owner/session/group AOI+typedNativeReply+oneHomeRestore seam0-999=true negatives=45+ archiveGroupsRejected=" +
            verifiedPrivateArchiveGroups + " native=false selector=false");
    }
}
