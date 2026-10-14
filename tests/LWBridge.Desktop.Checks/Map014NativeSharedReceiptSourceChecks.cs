using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using LWBridge.Desktop;
using Microsoft.Data.Sqlite;
using Map317 = LWBridge.Map317;

namespace LWBridge.Desktop.Checks;

/// <summary>
/// Isolated inbound-schema tests. The injected ingress is a controlled
/// SYNTHETIC producer, not an authenticated native transport or live witness.
/// Prime wires RunAsync; this file never modifies the shared runner.
/// </summary>
internal static class Map014NativeSharedReceiptSourceChecks
{
    private const string Generation = "controlled-received-group";
    private const long Start = 1_800_000_000_000L;
    private static readonly OverviewMapScanSession Session = new(
        "received-profile", "received-launch", "controlled-challenge", 22446,
        @"C:\nonexistent-controlled-game.exe", "2026-10-14T00:00:00Z");
    private static readonly MapScanExecutionRequest Request = new(
        "received-run", 2213, 0, 1000, 1000, ["city"], 4,
        PlayerTileX: 498, PlayerTileY: 63, ScanMode: "fast",
        LaunchSessionId: Session.SessionId, LiveServerId: 2213);

    private static void Require(bool yes, string reason)
    {
        if (!yes) throw new InvalidOperationException("MAP014_NATIVE_RECEIVED: " + reason);
    }

    private static async Task<T> Reject<T>(Func<Task> action, string why) where T : Exception
    {
        try { await action().ConfigureAwait(false); }
        catch (T error) { return error; }
        throw new InvalidOperationException("MAP014_NATIVE_RECEIVED expected " + typeof(T).Name + ": " + why);
    }

    private static JsonArray Indices(int[] values) => JsonSerializer.SerializeToNode(values)!.AsArray();

    private static int[] Footprint(int x, int y)
    {
        int col = x / 10, row = y / 10;
        int firstX = Math.Clamp(col - 2, 0, 94);
        int firstY = Math.Clamp(row - 2, 0, 95);
        return Enumerable.Range(firstY, 5)
            .SelectMany(r => Enumerable.Range(firstX, 6).Select(c => r * 100 + c)).ToArray();
    }

    private static JsonObject Stage(
        string name, int x, int y, int[] native, long time, bool receiving) => new()
    {
        ["stage"] = name,
        ["tileX"] = x,
        ["tileY"] = y,
        ["observedAtUnixMs"] = time,
        ["flags"] = new JsonObject { ["isRecvViewPoints"] = receiving },
        ["geometry"] = new JsonObject { ["fov"] = 20.0 },
        ["currentIndices"] = Indices(native)
    };

    private static JsonObject NativeReply(
        string id, int x, int y, int[] native, long at, int counterBefore) => new()
    {
        ["source"] = "WorldPointManager.ReceiveViewPoints",
        ["scanRunId"] = Request.RunId,
        ["groupGeneration"] = Generation,
        ["requestId"] = id,
        ["profileId"] = Session.ProfileId,
        ["launchSessionId"] = Session.SessionId,
        ["gamePid"] = Session.GamePid,
        ["serverId"] = Request.ServerId,
        ["worldId"] = Request.WorldId,
        ["targetTileX"] = x,
        ["targetTileY"] = y,
        ["targetAoiIndex"] = y / 10 * 100 + x / 10,
        ["observedAtUnixMs"] = at,
        ["nativeReceiveCountBefore"] = counterBefore,
        ["nativeReceiveCountAfter"] = counterBefore + 1,
        ["currentIndicesAtReply"] = Indices(native)
    };

    private static JsonObject Camera() => new()
    {
        ["fov"] = 10.0,
        ["aspect"] = 1.9,
        ["zoom"] = 105.0,
        ["position"] = new JsonObject { ["x"] = 498, ["y"] = 63, ["z"] = 100 },
        ["rotation"] = new JsonObject { ["x"] = 0, ["y"] = 0, ["z"] = 0, ["w"] = 1 },
        ["projection"] = new JsonArray(1, 0, 0, 1),
        ["anchors"] = new JsonArray(498, 63)
    };

    private static JsonElement[] Regions()
    {
        (int X, int Y)[] targets = [(455, 425), (545, 495), (655, 565)];
        var regions = new JsonObject[3];
        for (int i = 0; i < 3; i++)
        {
            int x = targets[i].X, y = targets[i].Y;
            int prevX = i == 0 ? 498 : targets[i - 1].X;
            int prevY = i == 0 ? 63 : targets[i - 1].Y;
            int[] native = Footprint(x, y), prior = Footprint(prevX, prevY);
            long at = Start + i * 4000;
            string id = "controlled-target-" + i;
            regions[i] = new JsonObject
            {
                ["requestId"] = id,
                ["groupGeneration"] = Generation,
                ["profileId"] = Session.ProfileId,
                ["launchSessionId"] = Session.SessionId,
                ["challenge"] = Session.Challenge,
                ["gamePid"] = Session.GamePid,
                ["scanRunId"] = Request.RunId,
                ["serverId"] = Request.ServerId,
                ["worldId"] = Request.WorldId,
                ["selectedTypes"] = new JsonArray("city"),
                ["regionOrdinal"] = i,
                ["requestMode"] = "coverage",
                ["state"] = "proven",
                ["map013NativeReplyState"] = "matched",
                ["nativeReplyRequestId"] = id,
                ["responseFlagsTransitioned"] = true,
                ["positionRestoredBeforeResponse"] = false,
                ["homeTileX"] = 498,
                ["homeTileY"] = 63,
                ["requestStartedAtUnixMs"] = at,
                ["nativeReplyAtUnixMs"] = at + 500,
                ["nativeReplyEvidence"] = NativeReply(id, x, y, native, at + 500, 100 + i * 2),
                ["targetTileX"] = x,
                ["targetTileY"] = y,
                ["targetAoiIndex"] = y / 10 * 100 + x / 10,
                ["postServerLod"] = 0,
                ["postBlockSize"] = 10,
                ["postBlockCount"] = 100,
                ["nativeCurrentSetCount"] = native.Length,
                ["requestedIndices"] = Indices(native),
                ["map014SubscriptionStages"] = new JsonArray(
                    Stage("before-shift", prevX, prevY, prior, at - 200, true),
                    Stage("after-shift", x, y, prior, at - 170, true),
                    Stage("post-lens", x, y, prior, at - 150, true),
                    Stage("post-update", x, y, native, at + 50, false),
                    Stage("first-poll", x, y, native, at + 100, false)),
                ["map013PositionRetained"] = i != 2,
                ["positionRestoredAfterResponse"] = i == 2
            };
        }
        JsonObject final = regions[2];
        final["cameraGeometryRestored"] = true;
        final["cameraGeometryConfirmed"] = true;
        final["cameraProjectionRestored"] = true;
        final["restoredTileX"] = 498;
        final["restoredTileY"] = 63;
        final["originalCameraGeometry"] = Camera();
        final["restoredCameraGeometry"] = Camera();
        final["homeRestoreRequestId"] = "controlled-home-reply";
        final["homeRestoreStartedAtUnixMs"] = Start + 8700;
        final["homeNativeReplyRequestId"] = "controlled-home-reply";
        final["homeNativeReplyState"] = "matched";
        final["homeNativeReplyAtUnixMs"] = Start + 9000;
        final["restoredCameraObservedAtUnixMs"] = Start + 9200;
        final["homeNativeReplyEvidence"] =
            NativeReply("controlled-home-reply", 498, 63, Footprint(498, 63),
                Start + 9000, 106);
        return regions.Select(value => JsonSerializer.SerializeToElement(value)).ToArray();
    }

    private static MapScanSharedAcquisitionOwner Owner() => new(
        Request.RunId, Request.ServerId, Request.WorldId, Session.SessionId,
        Request.SelectedTypes, Session.ProfileId, Session.GamePid, Generation);

    private static MapScanAcquisitionCleanupReceipt Home() =>
        new(Request, true, true, Owner());

    private static MapStoredRecord City(MapScanTargetBlock block) => new(
        "city", Request.ServerId, "native-city-" + block.BlockIndex,
        block.TargetY * 1000 + block.TargetX + 1,
        null, "controlled-city", null, 11, null, null, null, null,
        Start, JsonSerializer.Serialize(new { ownerUid = "unique-owner-" + block.BlockIndex }));

    private static Map014ReceivedNativeSharedGroup Envelope()
    {
        var aoi = new List<Map014ReceivedNativeAoiProof>(10000);
        var typed = new List<Map014ReceivedKindBlock>(2500);
        IReadOnlyList<MapScanTargetBlock> blocks = MapScanTraversal.Build(1000, 1000);
        foreach (MapScanTargetBlock block in blocks)
        {
            int x = block.MinX / 10, y = block.MinY / 10;
            int[] indices = [y * 100 + x, y * 100 + x + 1,
                (y + 1) * 100 + x, (y + 1) * 100 + x + 1];
            string id = "received-world-block-" + block.BlockIndex;
            long time = Start + 2000;
            foreach (int index in indices)
                aoi.Add(new Map014ReceivedNativeAoiProof(
                    index, id, time, "WorldPointManager._curViewIndex",
                    Request.RunId, Request.ServerId, Request.WorldId, Session.SessionId,
                    Session.ProfileId, Session.GamePid, Generation));
            typed.Add(new Map014ReceivedKindBlock(block.BlockIndex, "city",
                "WorldPointManager._pointInfos", id, time, [City(block)]));
        }
        return new Map014ReceivedNativeSharedGroup(
            Session, Request, Regions(), aoi, typed, Home());
    }

    private static Map014ReceivedNativeSharedGroup ReceivedForAcceptedRun(
        MapScanExecutionRequest accepted)
    {
        Map014ReceivedNativeSharedGroup original = Envelope();
        JsonElement[] regions = original.NativeConsecutiveRegions.Select(region =>
        {
            JsonNode json = JsonNode.Parse(region.GetRawText())!;
            json["scanRunId"] = accepted.RunId;
            json["nativeReplyEvidence"]!["scanRunId"] = accepted.RunId;
            if (json["homeNativeReplyEvidence"] is JsonNode homeReply)
                homeReply["scanRunId"] = accepted.RunId;
            return JsonSerializer.SerializeToElement(json);
        }).ToArray();
        MapScanSharedAcquisitionOwner boundOwner = Owner() with
        {
            RunId = accepted.RunId,
            SelectedTypes = accepted.SelectedTypes.ToArray()
        };
        return original with
        {
            Request = accepted,
            NativeConsecutiveRegions = regions,
            NativeAoiProofs = original.NativeAoiProofs.Select(evidence =>
                evidence with { RunId = accepted.RunId }).ToArray(),
            NativeHomeReceipt = new MapScanAcquisitionCleanupReceipt(
                accepted, true, true, boundOwner)
        };
    }

    private sealed class Received : IMap014ReceivedNativeSharedGroupIngress
    {
        internal Map014ReceivedNativeSharedGroup? Group;
        internal Func<CancellationToken, Task>? BeforeGroup;
        internal Func<CancellationToken, Task>? BeforeHome;
        internal Map014ReceivedNativeHomeAcknowledgement? HomeReply;
        internal MapScanExecutionRequest ExpectedRequest;
        internal int Reads, HomeReads;
        internal MapScanAcquisitionExitReason? CleanupReason;

        internal Received(
            Map014ReceivedNativeSharedGroup? group,
            MapScanExecutionRequest? acceptedRequest = null)
        {
            Group = group;
            // The trusted inbound transport is pinned to the accepted request,
            // NOT to untrusted or stale values inside the returned envelope.
            ExpectedRequest = acceptedRequest ?? Request;
            if (group?.NativeHomeReceipt is { } original)
                HomeReply = new Map014ReceivedNativeHomeAcknowledgement(
                    "controlled-home-reply", "controlled-home-reply", Start + 9000, original);
        }

        public async Task<Map014ReceivedNativeSharedGroup?> ReceiveAlreadyCompletedAsync(
            MapScanExecutionRequest request, OverviewMapScanSession session, CancellationToken token)
        {
            Reads++;
            Require(request.RunId == ExpectedRequest.RunId && session == Session,
                "received ingress was not pinned to request and Overview session");
            if (BeforeGroup is not null) await BeforeGroup(token);
            return Group;
        }

        public async Task<Map014ReceivedNativeHomeAcknowledgement?> AcknowledgeAlreadyReceivedHomeAsync(
            MapScanExecutionRequest request, OverviewMapScanSession session,
            MapScanAcquisitionExitReason reason, CancellationToken token)
        {
            HomeReads++;
            CleanupReason = reason;
            Require(request.RunId == ExpectedRequest.RunId && session == Session,
                "home acknowledgement changed run or Overview session");
            if (BeforeHome is not null) await BeforeHome(token);
            return HomeReply;
        }
    }

    private static Map014NativeSharedReceiptSource Source(Received ingress) =>
        new(ingress, Session, Request, Generation);

    private static async Task RejectGroup(
        string reason, Func<Map014ReceivedNativeSharedGroup, Map014ReceivedNativeSharedGroup> mutate)
    {
        Received ingress = new(mutate(Envelope()));
        Map014NativeSharedReceiptSource source = Source(ingress);
        await Reject<InvalidDataException>(async () =>
        {
            await foreach (MapScanSharedAcquisitionBatch _ in
                source.CaptureSharedAsync(Request, MapScanTraversal.Build(1000, 1000), CancellationToken.None))
            {
                throw new InvalidOperationException("Invalid native group staged a positive block: " + reason);
            }
        }, reason);
        Require(ingress.Reads == 1 && ingress.HomeReads == 0,
            reason + " attempted native cleanup acknowledgement after rejected source envelope");
    }

    private static int SqlCount(string db, string table, string where = "1=1")
    {
        using var connection = new SqliteConnection("Data Source=" + db);
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM {table} WHERE {where}";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static async Task TestReceivedCitySQLiteAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(),
            "map014-inbound-only-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "data.db");
        try
        {
            using var store = new MapDataStore(path);
            var ingress = new Received(Envelope());
            await new MapScanEngine(Source(ingress), new MapDataStoreScanSink(store))
                .ExecuteAsync(Request);
            Require(ingress.Reads == 1 && ingress.HomeReads == 1 &&
                    ingress.CleanupReason == MapScanAcquisitionExitReason.Completed &&
                    SqlCount(path, "map_records", "server_id=2213 AND kind='city'") == 2500 &&
                    SqlCount(path, "scan_records") == 0 &&
                    SqlCount(path, "scan_blocks") == 0 &&
                    SqlCount(path, "scan_runs", "id='received-run' AND status='completed'") == 1 &&
                    store.GetRecord("city", 2213, "native-city-0") is not null &&
                    store.GetRecord("city", 2213, "native-city-2499") is not null,
                "inbound full-world native-shaped receipt did not stage/publish exactly once");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(directory, true); } catch { }
        }
    }

    private static async Task TestRetiredReceivedCitySQLiteAsync(bool cancelledAfterStaging)
    {
        string directory = Path.Combine(Path.GetTempPath(),
            "map014-inbound-retired-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "data.db");
        try
        {
            using var store = new MapDataStore(path);
            var previousHome = new MapStoredRecord("city", 2213, "previous-home", 1,
                null, "previous source", null, 1, null, null, null, null,
                Start - 5000, "{\"ownerUid\":\"previous-home-owner\"}");
            store.UpsertRecord(previousHome);
            store.UpsertRecord(previousHome with {
                ServerId = 2214, RecordKey = "unrelated-remote"
            });
            var ingress = new Received(Envelope());
            using var stop = new CancellationTokenSource();
            if (!cancelledAfterStaging)
                ingress.HomeReply = ingress.HomeReply! with {
                    NativeReplyRequestId = "wrong-original-home-reply"
                };
            Task acquisition = new MapScanEngine(
                Source(ingress), new MapDataStoreScanSink(store),
                progress: progress => {
                    if (cancelledAfterStaging && progress.CompletedBlocks >= 100)
                        stop.Cancel();
                }).ExecuteAsync(Request, stop.Token);
            if (cancelledAfterStaging)
                await Reject<OperationCanceledException>(() => acquisition,
                    "positive staged native group was published after exact Stop");
            else
                await Reject<InvalidDataException>(() => acquisition,
                    "wrong independently received final native home ACK published grouped City");

            string terminal = cancelledAfterStaging ? "discarded" : "failed";
            Require(SqlCount(path, "scan_runs", "id='received-run' AND status='" + terminal + "'") == 1 &&
                    SqlCount(path, "map_records", "server_id=2213 AND kind='city'") == 1 &&
                    SqlCount(path, "map_records", "server_id=2214 AND kind='city'") == 1 &&
                    SqlCount(path, "scan_records") == 0 &&
                    SqlCount(path, "scan_blocks") == 0 &&
                    store.GetRecord("city", 2213, "previous-home") is not null &&
                    store.GetRecord("city", 2214, "unrelated-remote") is not null &&
                    store.GetRecord("city", 2213, "native-city-0") is null &&
                    ingress.Reads == 1 && ingress.HomeReads == 1 &&
                    ingress.CleanupReason == (cancelledAfterStaging
                        ? MapScanAcquisitionExitReason.Stopped
                        : MapScanAcquisitionExitReason.Completed),
                "retired/wrong-home native group lost historical SQLite rows or left positive staging");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(directory, true); } catch { }
        }
    }

    private static async Task TestSourceAdmissionAndInversesAsync()
    {
        Received absent = new(null);
        await Reject<InvalidDataException>(async () =>
        {
            await foreach (var _ in Source(absent).CaptureSharedAsync(
                Request, MapScanTraversal.Build(1000, 1000), CancellationToken.None)) { }
        }, "source absent");
        Require(absent.Reads == 1 && absent.HomeReads == 0, "absent ingress was treated as a native success");

        foreach (string kind in new[] { "resource", "truck", "railway", "other" })
            await Reject<InvalidDataException>(() => Task.Run(() =>
            {
                var selected = Request with { SelectedTypes = ["city", kind] };
                _ = new Map014NativeSharedReceiptSource(
                    new Received(null), Session, selected, Generation);
            }), kind + " source has no authenticated type-specific readiness");

        await RejectGroup("different run", envelope => envelope with
        {
            Request = envelope.Request with { RunId = "previous-run" }
        });
        await RejectGroup("different session", envelope => envelope with
        {
            OwnedSession = envelope.OwnedSession with { GamePid = Session.GamePid + 1 }
        });
        await RejectGroup("different server", envelope => envelope with
        {
            NativeAoiProofs = [
                envelope.NativeAoiProofs[0] with { ServerId = 2214 },
                ..envelope.NativeAoiProofs.Skip(1)
            ]
        });
        await RejectGroup("stale AOI", envelope => envelope with
        {
            NativeAoiProofs = [
                envelope.NativeAoiProofs[0] with { NativeReplyAtUnixMs = Start - 1 },
                ..envelope.NativeAoiProofs.Skip(1)
            ]
        });
        await RejectGroup("missing 0..999 AOI", envelope => envelope with
        {
            NativeAoiProofs = envelope.NativeAoiProofs.Skip(1).ToArray()
        });
        await RejectGroup("duplicate 0..999 AOI", envelope => envelope with
        {
            NativeAoiProofs = [
                envelope.NativeAoiProofs[0],
                envelope.NativeAoiProofs[0],
                ..envelope.NativeAoiProofs.Skip(2)
            ]
        });
        await RejectGroup("one reply reused to cover all 10000 AOIs", envelope => envelope with
        {
            NativeAoiProofs = envelope.NativeAoiProofs.Select(proof => proof with {
                NativeReplyRequestId = "forged-one-world-reply"
            }).ToArray(),
            KindBlocks = envelope.KindBlocks.Select(block => block with {
                NativeReplyRequestId = "forged-one-world-reply"
            }).ToArray()
        });
        await RejectGroup("one reply id with inconsistent native observation clocks", envelope => envelope with
        {
            NativeAoiProofs = [envelope.NativeAoiProofs[0] with {
                NativeReplyAtUnixMs = envelope.NativeAoiProofs[0].NativeReplyAtUnixMs + 1
            }, ..envelope.NativeAoiProofs.Skip(1)]
        });
        await RejectGroup("region A native reply reused to certify distant non-member AOIs", envelope =>
        {
            JsonElement a = envelope.NativeConsecutiveRegions[0];
            string nativeId = a.GetProperty("requestId").GetString()!;
            long nativeTime = a.GetProperty("nativeReplyAtUnixMs").GetInt64();
            int[] wrongAois = [9898, 9899, 9998, 9999];
            return envelope with
            {
                NativeAoiProofs = envelope.NativeAoiProofs.Select(proof =>
                    wrongAois.Contains(proof.NativeAoiIndex)
                        ? proof with { NativeReplyRequestId = nativeId, NativeReplyAtUnixMs = nativeTime }
                        : proof).ToArray(),
                KindBlocks = envelope.KindBlocks.Select(block => block.BlockIndex == 2499
                    ? block with { NativeReplyRequestId = nativeId, NativeReplyAtUnixMs = nativeTime }
                    : block).ToArray()
            };
        });
        await RejectGroup("region A native reply reused with a fabricated observation time", envelope =>
        {
            JsonElement a = envelope.NativeConsecutiveRegions[0];
            string nativeId = a.GetProperty("requestId").GetString()!;
            int[] wrongAois = [9898, 9899, 9998, 9999];
            return envelope with
            {
                NativeAoiProofs = envelope.NativeAoiProofs.Select(proof =>
                    wrongAois.Contains(proof.NativeAoiIndex)
                        ? proof with { NativeReplyRequestId = nativeId }
                        : proof).ToArray(),
                KindBlocks = envelope.KindBlocks.Select(block => block.BlockIndex == 2499
                    ? block with { NativeReplyRequestId = nativeId }
                    : block).ToArray()
            };
        });
        await RejectGroup("late world AOI reply after native final-home request began", envelope => envelope with
        {
            NativeAoiProofs = envelope.NativeAoiProofs.Select(proof =>
                proof.NativeReplyRequestId == "received-world-block-2499"
                    ? proof with { NativeReplyAtUnixMs = Start + 8800 }
                    : proof).ToArray(),
            KindBlocks = envelope.KindBlocks.Select(block => block.BlockIndex == 2499
                ? block with { NativeReplyAtUnixMs = Start + 8800 }
                : block).ToArray()
        });
        await RejectGroup("late City kind result after native final-home began", envelope => envelope with
        {
            KindBlocks = [
                envelope.KindBlocks[0] with { NativeReplyAtUnixMs = Start + 8800 },
                ..envelope.KindBlocks.Skip(1)
            ],
            NativeAoiProofs = envelope.NativeAoiProofs.Select(proof =>
                proof.NativeReplyRequestId == "received-world-block-0"
                    ? proof with { NativeReplyAtUnixMs = Start + 8800 }
                    : proof).ToArray()
        });
        await RejectGroup("future-dated City observation after its native source reply", envelope => envelope with
        {
            KindBlocks = [
                envelope.KindBlocks[0] with {
                    Records = [envelope.KindBlocks[0].Records[0] with { UpdatedAt = Start + 9100 }]
                },
                ..envelope.KindBlocks.Skip(1)
            ]
        });
        await RejectGroup("stale City observation preceding current owned source run", envelope => envelope with
        {
            KindBlocks = [
                envelope.KindBlocks[0] with {
                    Records = [envelope.KindBlocks[0].Records[0] with { UpdatedAt = Start - 1 }]
                },
                ..envelope.KindBlocks.Skip(1)
            ]
        });
        await RejectGroup("false final home receipt", envelope => envelope with
        {
            NativeHomeReceipt = envelope.NativeHomeReceipt with
            {
                FreshHomeViewReplyMatched = false
            }
        });
        await RejectGroup("type-specific enrichment missing", envelope => envelope with
        {
            KindBlocks = envelope.KindBlocks.Skip(1).ToArray()
        });
        await RejectGroup("far-block source cohort replay", envelope => envelope with
        {
            KindBlocks = [
                envelope.KindBlocks[0] with {
                    NativeReplyRequestId = envelope.KindBlocks[2499].NativeReplyRequestId
                },
                ..envelope.KindBlocks.Skip(1)
            ]
        });
        await RejectGroup("foreign record server", envelope => envelope with
        {
            KindBlocks = [
                envelope.KindBlocks[0] with {
                    Records = [envelope.KindBlocks[0].Records[0] with { ServerId = 2214 }]
                },
                ..envelope.KindBlocks.Skip(1)
            ]
        });
        await RejectGroup("null City position", envelope => envelope with
        {
            KindBlocks = [
                envelope.KindBlocks[0] with {
                    Records = [envelope.KindBlocks[0].Records[0] with { PointIndex = null }]
                },
                ..envelope.KindBlocks.Skip(1)
            ]
        });
        await RejectGroup("missing native City owner", envelope => envelope with
        {
            KindBlocks = [
                envelope.KindBlocks[0] with {
                    Records = [envelope.KindBlocks[0].Records[0] with { DataJson = "{}" }]
                },
                ..envelope.KindBlocks.Skip(1)
            ]
        });
        await RejectGroup("duplicate canonical City owner on far block", envelope => envelope with
        {
            KindBlocks = [
                ..envelope.KindBlocks.Take(2499),
                envelope.KindBlocks[2499] with {
                    Records = [envelope.KindBlocks[2499].Records[0] with {
                        DataJson = envelope.KindBlocks[0].Records[0].DataJson
                    }]
                }
            ]
        });
        await RejectGroup("forged A/B/C native reply", envelope =>
        {
            JsonNode[] regions = envelope.NativeConsecutiveRegions
                .Select(receipt => JsonNode.Parse(receipt.GetRawText())!).ToArray();
            regions[1]["nativeReplyEvidence"]!["gamePid"] = 112;
            return envelope with { NativeConsecutiveRegions =
                regions.Select(value => JsonSerializer.SerializeToElement(value)).ToArray() };
        });
    }

    private static async Task TestLateCancellationAndCleanupAsync()
    {
        using var cancel = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ingress = new Received(Envelope())
        {
            BeforeGroup = async _ =>
            {
                entered.TrySetResult();
                await release.Task; // ignores cancellation deliberately
            }
        };
        Map014NativeSharedReceiptSource source = Source(ingress);
        Task reader = Task.Run(async () =>
        {
            await foreach (var _ in source.CaptureSharedAsync(
                Request, MapScanTraversal.Build(1000, 1000), cancel.Token)) { }
        });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        cancel.Cancel();
        release.TrySetResult();
        await Reject<OperationCanceledException>(() => reader, "late received envelope after Stop");
        await Reject<InvalidDataException>(
            () => source.CleanupAsync(Request, MapScanAcquisitionExitReason.Stopped, CancellationToken.None),
            "cancel before native home evidence cannot return a positive receipt");
        Require(ingress.Reads == 1 && ingress.HomeReads == 0,
            "late native producer / cleanup fabricated fresh home success");

        using var stopped = new CancellationTokenSource();
        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var free = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var owned = new Received(Envelope())
        {
            BeforeHome = async token =>
            {
                Require(token.CanBeCanceled && !token.IsCancellationRequested &&
                        stopped.IsCancellationRequested,
                    "cleanup must use independent token after cancelled positive staging");
                held.TrySetResult();
                await free.Task.WaitAsync(token);
            }
        };
        MapScanExecutionRequest request = Request;
        Task run = new MapScanEngine(Source(owned), new MinimalSink(),
            progress: p =>
            {
                if (p.CompletedBlocks > 0) stopped.Cancel();
            }).ExecuteAsync(request, stopped.Token);
        await held.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Require(!run.IsCompleted && owned.HomeReads == 1,
            "Stop did not await the real received home acknowledgement");
        free.TrySetResult();
        await Reject<OperationCanceledException>(() => run, "Stop after native staged positive");
        Require(owned.CleanupReason == MapScanAcquisitionExitReason.Stopped && owned.HomeReads == 1,
            "cancelled native receipt source double-acked home");
    }

    private static async Task TestMatchingRegionReplyMembershipAsync()
    {
        // Controlled positive for the narrow reply-ID binding rule: a region A
        // ID may legitimately certify AOIs present in A's actual source
        // footprint, but never a distant tile or a different reply clock.
        Map014ReceivedNativeSharedGroup original = Envelope();
        JsonElement regionA = original.NativeConsecutiveRegions[0];
        string regionReplyId = regionA.GetProperty("nativeReplyRequestId").GetString()!;
        long regionReplyTime = regionA.GetProperty("nativeReplyAtUnixMs").GetInt64();
        var nativeFootprint = regionA.GetProperty("requestedIndices").EnumerateArray()
            .Select(index => index.GetInt32()).ToHashSet();
        MapScanTargetBlock legitimate = MapScanTraversal.Build(1000, 1000).First(block =>
        {
            int topLeft = block.MinY / 10 * 100 + block.MinX / 10;
            return nativeFootprint.Contains(topLeft) &&
                   nativeFootprint.Contains(topLeft + 1) &&
                   nativeFootprint.Contains(topLeft + 100) &&
                   nativeFootprint.Contains(topLeft + 101);
        });
        int[] ownedAois = [
            legitimate.MinY / 10 * 100 + legitimate.MinX / 10,
            legitimate.MinY / 10 * 100 + legitimate.MinX / 10 + 1,
            (legitimate.MinY / 10 + 1) * 100 + legitimate.MinX / 10,
            (legitimate.MinY / 10 + 1) * 100 + legitimate.MinX / 10 + 1
        ];
        Map014ReceivedNativeSharedGroup valid = original with
        {
            NativeAoiProofs = original.NativeAoiProofs.Select(proof =>
                ownedAois.Contains(proof.NativeAoiIndex)
                    ? proof with {
                        NativeReplyRequestId = regionReplyId,
                        NativeReplyAtUnixMs = regionReplyTime
                    }
                    : proof).ToArray(),
            KindBlocks = original.KindBlocks.Select(block =>
                block.BlockIndex == legitimate.BlockIndex
                    ? block with {
                        NativeReplyRequestId = regionReplyId,
                        NativeReplyAtUnixMs = regionReplyTime
                    }
                    : block).ToArray()
        };
        var ingress = new Received(valid);
        Map014NativeSharedReceiptSource source = Source(ingress);
        int acceptedBlocks = 0;
        await foreach (MapScanSharedAcquisitionBatch batch in source.CaptureSharedAsync(
            Request, MapScanTraversal.Build(1000, 1000), CancellationToken.None))
            acceptedBlocks += batch.Captures.Count;
        MapScanAcquisitionCleanupReceipt home = await source.CleanupAsync(
            Request, MapScanAcquisitionExitReason.Completed, CancellationToken.None);
        Require(acceptedBlocks == 2500 && home.FreshHomeViewReplyMatched &&
                ingress.Reads == 1 && ingress.HomeReads == 1,
            "matching region source reply/AOI cohort failed received-only controlled admission");
    }

    private sealed class FactoryFallback : IMapScanBlockSource
    {
        public Task<MapScanBlockCapture> CaptureAsync(
            MapScanExecutionRequest request, MapScanTargetBlock block, CancellationToken token) =>
            throw new InvalidOperationException(
                "Production fallback must not be used by accepted-run received ingress factory");
    }

    private static async Task TestActualMap317ReceivedIngressAsync(string scenario)
    {
        using Map317.MapStore store = Map317.MapStore.CreateInMemory();
        Received? ingress = null;
        int factoryCalls = 0;
        var provider = new CurrentClientMap317ScanProvider(
            new FactoryFallback(),
            _ => Task.FromResult(new CurrentClientMapStatusContext(
                true, 2213, 2213, [], [], 0, 1000, 1000, 498, 63)),
            _ => Task.FromResult(new CurrentClientMapContext(
                2213, 0, 1000, 1000, 498, 63, Session.SessionId)),
            createSourceForAcceptedRun: (acceptedRequest, acceptedContext) =>
            {
                factoryCalls++;
                Require(!string.IsNullOrWhiteSpace(acceptedRequest.RunId) &&
                        acceptedContext.LaunchSessionId == Session.SessionId,
                    "Map317 source factory did not wait for the accepted durable run");
                Map014ReceivedNativeSharedGroup envelope = ReceivedForAcceptedRun(acceptedRequest);
                if (scenario == "missing-aoi")
                    envelope = envelope with
                    {
                        NativeAoiProofs = envelope.NativeAoiProofs.Skip(1).ToArray()
                    };
                if (scenario == "wrong-profile")
                    envelope = envelope with
                    {
                        NativeAoiProofs = [
                            envelope.NativeAoiProofs[0] with { ProfileId = "retired-profile" },
                            ..envelope.NativeAoiProofs.Skip(1)
                        ]
                    };
                if (scenario == "late-after-home-start")
                    envelope = envelope with
                    {
                        NativeAoiProofs = envelope.NativeAoiProofs.Select(proof =>
                            proof.NativeReplyRequestId == "received-world-block-2499"
                                ? proof with { NativeReplyAtUnixMs = Start + 8800 }
                                : proof).ToArray(),
                        KindBlocks = envelope.KindBlocks.Select(block => block.BlockIndex == 2499
                            ? block with { NativeReplyAtUnixMs = Start + 8800 }
                            : block).ToArray()
                    };
                if (scenario == "future-city-observation")
                    envelope = envelope with
                    {
                        KindBlocks = [
                            envelope.KindBlocks[0] with {
                                Records = [envelope.KindBlocks[0].Records[0] with {
                                    UpdatedAt = Start + 9100
                                }]
                            },
                            ..envelope.KindBlocks.Skip(1)
                        ]
                    };
                ingress = new Received(envelope, acceptedRequest);
                if (scenario == "wrong-home-reply")
                    ingress.HomeReply = ingress.HomeReply! with {
                        NativeReplyRequestId = "stale-final-home-response"
                    };
                return new Map014NativeSharedReceiptSource(
                    ingress, Session, acceptedRequest, Generation);
            });
        using var control = new Map317.MapControlPlane(store, provider);
        var terminal = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        provider.RunTerminated += runId => terminal.TrySetResult(runId);
        Map317.MapScanState accepted = await control.StartScanAsync(
            new Map317.MapScanStartRequest(["city"], "fast"));
        Require(factoryCalls == 0, "Map317 instantiated native receipt source before the accepted run");
        provider.ActivateAcceptedRun(control, accepted.ScanRunId);
        Require(await terminal.Task.WaitAsync(TimeSpan.FromSeconds(25)) == accepted.ScanRunId,
            "received ingress Map317 controlled run never terminated");
        bool positive = scenario == "valid";
        Require(factoryCalls == 1 && ingress is not null && ingress.Reads == 1 &&
                control.ScanState.Phase == (positive ? "completed" : "failed") &&
                store.SummaryCounts(2213)["city"] == (positive ? 2500 : 0) &&
                ingress.HomeReads == (positive || scenario == "wrong-home-reply" ? 1 : 0) &&
                (positive ? ingress.CleanupReason == MapScanAcquisitionExitReason.Completed : true),
            "Map317 native received group was incorrectly published or skipped: " + scenario);
    }

    private sealed class MinimalSink : IMapScanRunSink
    {
        public void Begin(MapScanExecutionRequest _, int __, long ___) { }
        public void CheckpointSuccess(MapScanExecutionRequest _, MapScanTargetBlock __,
            MapScanBlockCapture ___, int ____, long _____) { }
        public void CheckpointFailure(MapScanExecutionRequest _, MapScanTargetBlock __,
            int ___, string ____, long _____) { }
        public void Publish(MapScanExecutionRequest _, long __) =>
            throw new InvalidOperationException("Cancelled native receipt scan published");
        public void Fail(MapScanExecutionRequest _, string __, long ___) { }
        public void Stop(MapScanExecutionRequest _, long __) { }
    }

    internal static async Task RunAsync()
    {
        await TestReceivedCitySQLiteAsync();
        await TestRetiredReceivedCitySQLiteAsync(cancelledAfterStaging: true);
        await TestRetiredReceivedCitySQLiteAsync(cancelledAfterStaging: false);
        await TestSourceAdmissionAndInversesAsync();
        await TestMatchingRegionReplyMembershipAsync();
        await TestLateCancellationAndCleanupAsync();
        await TestActualMap317ReceivedIngressAsync("valid");
        await TestActualMap317ReceivedIngressAsync("missing-aoi");
        await TestActualMap317ReceivedIngressAsync("wrong-profile");
        await TestActualMap317ReceivedIngressAsync("wrong-home-reply");
        await TestActualMap317ReceivedIngressAsync("late-after-home-start");
        await TestActualMap317ReceivedIngressAsync("future-city-observation");
        Console.WriteLine(
            "MAP014_NATIVE_SHARED_RECEIVED_ONLY_OK synthetic=true native=false selector=false ingressOnly=true aoi=10000 blocks=2500 kind=city mixed=blocked map317Sqlite=true");
    }
}
