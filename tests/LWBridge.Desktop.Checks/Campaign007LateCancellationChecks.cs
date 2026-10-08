using System.Text.Json;
using Map317 = LWBridge.Map317;

namespace LWBridge.Desktop.Checks;

/// <summary>
/// Production MapControlPlane + actual MapStore transaction inverse checks, with
/// only the OUTER GAME PROVIDER inert. This is not a current game or original
/// protected provider late-response witness.
/// </summary>
internal static class Campaign007LateCancellationChecks
{
    private const int Server = 2212;
    private static long now = 1791429900000;
    private static void Require(bool value, string description)
    {
        if (!value) throw new InvalidDataException("CAMPAIGN007 cancel/inverse: " + description);
    }

    private static Map317.MapRecord Resource(string key) => new(
        "resource", Server, key, 321, null, "controlled-inert-node-" + key,
        null, 1, 1, null, null, null, ++now,
        JsonSerializer.Serialize(new {
            kind = "resource", serverId = Server, recordKey = key,
            resourceNameKey = "controlled-inert-resource", pointIndex = 321,
            name = "controlled-inert-node-" + key, updatedAt = now
        }));

    private static int Published(Map317.MapStore store) =>
        store.Search(new Map317.MapQuery("resource", Server)).Total;

    private static int Staged(Map317.MapStore store, string runId) =>
        store.Search(new Map317.MapQuery("resource", Server, ScanRunId: runId)).Total;

    private static void ExpectCode(Action action, string expected, string description)
    {
        try { action(); }
        catch (Map317.BridgeCommandException error) when (error.Code == expected) { return; }
        throw new InvalidDataException("CAMPAIGN007 missing " + expected + ": " + description);
    }

    internal static async Task RunAsync(string outputPath)
    {
        int stopCalls = 0;
        var liveContext = new Map317.MapProviderContext(
            IsAvailable: true, IsInWorld: true, ServerId: Server,
            ServerIdSource: "live", WorldId: 0, TileWidth: 40, TileHeight: 20,
            ExpectedTotalBlocks: 2);
        var provider = new Map317.MapProviderAdapter(
            _ => ValueTask.FromResult(liveContext),
            _ => throw new InvalidDataException("already-world must never call world entry"),
            (request, _) => {
                Require(request.SelectedTypes.SequenceEqual(["resource"]) &&
                    request.ScanMode == "normal" && request.Concurrency == 8,
                    "real production Start request default");
                return ValueTask.FromResult(new Map317.MapProviderStartResult(
                    Accepted: true, TotalBlocks: 2));
            },
            _ => { stopCalls++; return ValueTask.CompletedTask; }
        );
        using var store = Map317.MapStore.CreateInMemory();
        using var plane = new Map317.MapControlPlane(store, provider, () => ++now);

        store.UpsertRecord(Resource("original-published"));
        Require(Published(store) == 1, "control baseline record missing");

        Map317.MapScanState active = await plane.StartScanAsync(
            new Map317.MapScanStartRequest(["resource"], "normal"));
        string cancelledId = active.ScanRunId;
        Require(active.IsReading && active.TotalBlocks == 2 &&
            store.ReadScanRun(cancelledId)?.Status == "running",
            "started actual control plane run missing");
        plane.StageRecord(Resource("positive-staged-before-stop"));
        Require(Staged(store, cancelledId) == 1 && Published(store) == 1,
            "active staged records must not replace published records");
        Map317.MapScanState stopped = await plane.StopScanAsync();
        Require(stopped.Phase == "idle" && !stopped.IsReading && stopCalls == 1,
            "actual Map control Stop must terminalize exactly once");
        Require(store.ReadScanRun(cancelledId)?.Status == "cancelled" &&
            Staged(store, cancelledId) == 0 && Published(store) == 1,
            "Stop after POSITIVE staging must delete only staging and retain previous publication");
        ExpectCode(() => plane.StageRecord(Resource("late-after-stop")), "INVALID_SCAN",
            "late captured record cannot stage after Stop");
        ExpectCode(() => store.UpdateScanProgress(cancelledId, 1, 0, null, ++now), "INVALID_SCAN",
            "late progress after cancelled");
        ExpectCode(() => store.CompleteScan(cancelledId, ++now), "INVALID_SCAN",
            "late publish after cancelled");
        Require(Staged(store, cancelledId) == 0 && Published(store) == 1,
            "late record/publish must leave cancelled staging empty");

        Map317.MapScanState failedStart = await plane.StartScanAsync(
            new Map317.MapScanStartRequest(["resource"], "normal"));
        plane.StageRecord(Resource("failed-captured"));
        Map317.MapScanState failed = plane.FailScan("controlled-inert-source-error");
        Require(!failed.IsReading &&
            store.ReadScanRun(failedStart.ScanRunId)?.Status == "failed" &&
            Staged(store, failedStart.ScanRunId) == 0 && Published(store) == 2,
            "0.3.17 recovered failed-run preservation must upsert without replacing existing");

        Map317.MapScanState completedStart = await plane.StartScanAsync(
            new Map317.MapScanStartRequest(["resource"], "normal"));
        plane.StageRecord(Resource("complete-selected"));
        ExpectCode(() => store.CompleteScan(completedStart.ScanRunId, ++now),
            "INCOMPLETE_SCAN", "incomplete direct run cannot publish");
        Require(Published(store) == 2 && Staged(store, completedStart.ScanRunId) == 1,
            "incomplete run cannot wipe old publication or staging");
        // Manually committed full progress is a STORE transaction test, not
        // evidence of live game completeness or MapScanEngine capture behavior.
        store.UpdateScanProgress(completedStart.ScanRunId, 2, 0, null, ++now);
        store.CompleteScan(completedStart.ScanRunId, ++now);
        Require(Published(store) == 1 && Staged(store, completedStart.ScanRunId) == 0 &&
            store.ReadScanRun(completedStart.ScanRunId)?.Status == "completed" &&
            store.ReadScanRun(cancelledId) is null &&
            store.ReadScanRun(failedStart.ScanRunId) is null,
            "selected-kind successful completion replaces publication atomically and prunes old runs");

        var result = new {
            workItem = "LWB317-FUNCTION-HOME-MAP-A-TO-A-CAMPAIGN-007",
            evidenceClass = "INERT provider, ACTUAL production MapControlPlane/MapStore SQLite in-memory",
            originalReferenceContract = "0.3.17 RE-MAP-002 recovered cancel/failed/successful store transactions",
            stoppedWithPositiveStaging = true,
            stagingBeforeStop = 1, publishedBeforeStop = 1,
            cancelledStatus = "cancelled", stagedAfterStop = 0,
            publishedAfterStop = 1, providerStopCalls = stopCalls,
            lateCapturedRejected = true, lateProgressRejected = true,
            latePublishRejected = true,
            failedStagedPreservedCount = 2,
            incompletePublishRejected = true,
            completedReplacingCount = 1,
            originalProtectedProviderLateResponseEquivalence = "UNKNOWN_NOT_PROVEN",
            currentGameActions = 0
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        File.WriteAllText(outputPath,
            JsonSerializer.Serialize(result,new JsonSerializerOptions {WriteIndented = true}));
        Console.WriteLine("CAMPAIGN007_PRODUCTION_STORE_CANCEL_AFTER_POSITIVE_STAGING_PASS");
    }
}
