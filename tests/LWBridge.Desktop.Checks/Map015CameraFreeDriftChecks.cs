using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

// Inert receipts only. Never launches, connects to, or touches a game.
internal static class Map015CameraFreeDriftChecks
{
    private static readonly OverviewMapScanSession Session = new(
        "drift-profile", "drift-session", "drift-challenge", 34123,
        @"C:\nonexistent-inert-game.exe", "2026-10-16T00:00:00Z");
    private static readonly MapScanExecutionRequest Request = new(
        "drift-run", 2212, 0, 1000, 1000, ["city"], 1,
        LaunchSessionId: Session.SessionId, LiveServerId: 2212);

    private static JsonObject Drift(string requestId) => new()
    {
        ["schemaVersion"] = 1,
        ["probeVersion"] = "lwbridge-live-resource-probe-2",
        ["requestId"] = requestId,
        ["profileId"] = Session.ProfileId,
        ["launchSessionId"] = Session.SessionId,
        ["challenge"] = Session.Challenge,
        ["gamePid"] = Session.GamePid,
        ["scanRunId"] = Request.RunId,
        ["serverId"] = Request.ServerId,
        ["worldId"] = Request.WorldId,
        ["state"] = "failed",
        ["error"] = "camera_tile_changed",
        ["curViewRestored"] = true,
        ["cameraStable"] = false,
        ["completedAois"] = 10000,
        ["requestsSent"] = 63,
        ["repliesCaptured"] = 63,
        ["elapsedSeconds"] = 9.0095,
        ["batches"] = new JsonArray(),
        ["capturedAt"] = DateTimeOffset.UtcNow.ToString("O"),
    };

    internal static async Task RunAsync()
    {
        var calls = new List<string>();
        var delays = new List<TimeSpan>();
        int cancels = 0;
        string? receipt = null;
        var source = new CurrentClientMapBlockSource(
            () => Session, "inert-overview", "inert-live-resource",
            new CurrentClientMapBlockSourceHooks
            {
                WriteTextAtomic = (path, command) =>
                {
                    string file = Path.GetFileName(path);
                    if (file == "camera-free-scan-cancel.txt") { cancels++; return; }
                    if (file != "camera-free-scan.txt")
                        throw new Exception("Drift retry wrote unexpected native command: " + file);
                    string id = command.Split('\n').Single(line => line.StartsWith("requestId=", StringComparison.Ordinal))["requestId=".Length..];
                    calls.Add(id);
                    receipt = Drift(id).ToJsonString();
                },
                ReadAllBytes = path => Path.GetFileName(path) == "camera-free-scan-result.json" && receipt is not null
                    ? Encoding.UTF8.GetBytes(receipt)
                    : throw new FileNotFoundException("Inert receipt not available", path),
                DelayAsync = (delay, _) => { delays.Add(delay); return Task.CompletedTask; },
            });

        DateTimeOffset now = DateTimeOffset.UtcNow;
        string testId = "owned-drift";
        if (!source.IsCameraFreeTerminalDriftReceipt(
                JsonSerializer.SerializeToElement(Drift(testId)), Session, Request, testId, now))
            throw new Exception("Exact-owner fully restored camera drift was not recognized.");
        foreach ((string field, JsonNode? rejected) in new (string, JsonNode?)[]
        {
            ("curViewRestored", JsonValue.Create(false)),
            ("cameraStable", JsonValue.Create(true)),
            ("state", JsonValue.Create("proven")),
            ("error", JsonValue.Create("other_failure")),
            ("completedAois", JsonValue.Create(9999)),
            ("requestsSent", JsonValue.Create(66)),
            ("repliesCaptured", JsonValue.Create(62)),
            ("batches", new JsonArray(new JsonObject())),
        })
        {
            JsonObject bad = Drift(testId);
            bad[field] = rejected?.DeepClone();
            if (source.IsCameraFreeTerminalDriftReceipt(
                    JsonSerializer.SerializeToElement(bad), Session, Request, testId, now))
                throw new Exception("Unsafe native drift receipt accepted: " + field);
        }
        JsonObject wrongOwner = Drift(testId);
        wrongOwner["challenge"] = "different-owner";
        try
        {
            source.IsCameraFreeTerminalDriftReceipt(
                JsonSerializer.SerializeToElement(wrongOwner), Session, Request, testId, now);
            throw new Exception("Wrong native owner was allowed to authorize a direct retry.");
        }
        catch (InvalidDataException) { }

        IReadOnlyList<MapScanTargetBlock> blocks = MapScanTraversal.Build(1000, 1000);
        try
        {
            await source.CaptureBatchAsync(Request, blocks[0],
                blocks.Select(block => block.BlockIndex).ToHashSet(), CancellationToken.None);
            throw new Exception("Repeated camera drift returned normally instead of terminating.");
        }
        catch (MapScanSourceValidationException ex)
        {
            if (!ex.Message.Contains("after its one bounded direct retry", StringComparison.Ordinal)) throw;
        }
        if (calls.Count != 2 || calls[0] == calls[1] || cancels != 0 ||
            delays.Count(delay => delay == TimeSpan.FromSeconds(3)) != 1)
            throw new Exception("Camera drift retry had duplicate request IDs, cancellation, or incorrect settling.");
        Console.WriteLine("MAP015_CAMERA_DRIFT_CONTROLLED_OK native=false freshRequests=2 settle=3s cancel=0 secondDrift=terminal inverses=9");
    }
}
