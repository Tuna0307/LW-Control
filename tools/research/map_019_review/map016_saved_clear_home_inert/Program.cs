// Inert checks against the ACTUAL acceptance gate compiled in Desktop.
// No launch, WebView, bridge pipe, game, SQLite write or source DB access.
using System.Text.Json;
using System.Text.Json.Nodes;
using LWBridge.Desktop;
using LWBridge.Map317;

const int Home = 2212;
const string RunId = "inert-current-session-home-resource-1";
var owner = new OverviewMapScanSession("map014-native-ui", "inert-session",
    "synthetic-test-challenge", 98765, "not-a-game-path", "2026-10-24T08:00:00Z");
var passing = Started();
var complete = passing with {
    IsReading = false, Phase = "completed", CompletedBlocks = 2500,
    ReadBlocks = 2500, UnreadBlocks = 0, InflightBlocks = 0,
    NativePendingRecords = 0, NativeDroppedRecords = 0,
};
var durable = new MapScanRun(RunId, Home, ["resource"], "completed",
    2500, 2500, 0, 1000, 2000, null);

Map016SavedClearHomeRunProof.RequireStarted(passing, Home);
Map016SavedClearHomeRunProof.RequireCompleted(complete, passing, Home);
Map016SavedClearHomeRunProof.RequireDurable(durable, RunId, Home);
var (native, metrics) = Native();
Map016SavedClearHomeRunProof.RequireNative(native, metrics, RunId, Home, owner);
int cases = 4;

Reject("old wrong server start", () => Map016SavedClearHomeRunProof.RequireStarted(
    passing with { ServerId = 2213 }, Home));
Reject("start missing native capability", () => Map016SavedClearHomeRunProof.RequireStarted(
    passing with { NativeCaptureReady = false }, Home));
Reject("start multiple selected types", () => Map016SavedClearHomeRunProof.RequireStarted(
    passing with { SelectedTypes = ["resource", "city"] }, Home));
Reject("start wrong mode", () => Map016SavedClearHomeRunProof.RequireStarted(
    passing with { ScanMode = "normal" }, Home));
Reject("start no run id", () => Map016SavedClearHomeRunProof.RequireStarted(
    passing with { ScanRunId = "" }, Home));
Reject("not completed", () => Map016SavedClearHomeRunProof.RequireCompleted(
    complete with { Phase = "failed", Error = "native_missing" }, passing, Home));
Reject("completion replaced owner", () => Map016SavedClearHomeRunProof.RequireCompleted(
    complete with { ScanRunId = "successor" }, passing, Home));
Reject("partial 2499", () => Map016SavedClearHomeRunProof.RequireCompleted(
    complete with { ReadBlocks = 2499 }, passing, Home));
Reject("native dropped", () => Map016SavedClearHomeRunProof.RequireCompleted(
    complete with { NativeDroppedRecords = 1 }, passing, Home));
Reject("changed home", () => Map016SavedClearHomeRunProof.RequireCompleted(
    complete with { ServerIdSource = "cached" }, passing, Home));
Reject("durable foreign ID", () => Map016SavedClearHomeRunProof.RequireDurable(
    durable with { Id = "other" }, RunId, Home));
Reject("durable cancelled", () => Map016SavedClearHomeRunProof.RequireDurable(
    durable with { Status = "cancelled" }, RunId, Home));
Reject("durable foreign selection", () => Map016SavedClearHomeRunProof.RequireDurable(
    durable with { SelectedTypes = ["city"] }, RunId, Home));
Reject("native foreign run", () => CheckNative(n => n["scanRunId"] = "older-run"));
Reject("native foreign session", () => CheckNative(n => n["launchSessionId"] = "older-session"));
Reject("native foreign challenge", () => CheckNative(n => n["challenge"] = "wrong-challenge"));
Reject("native foreign server", () => CheckNative(n => n["serverId"] = 2213));
Reject("native retry beyond bounded", () => CheckNative(n => n["requestsSent"] = 66));
Reject("camera not restored", () => CheckNative(n => n["curViewRestored"] = false));
Reject("native not proven", () => CheckNative(n => n["state"] = "failed"));
Reject("overlapping native AOIs", () => CheckNative(n =>
    n["batches"]![1]!["indices"]![0] = 0));
Reject("native AOI missing", () => CheckNative(n =>
    n["batches"]![0]!["indices"]!.AsArray().RemoveAt(0)));
Reject("wrong prepared metrics run", () => CheckNative(
    _ => { }, m => m["identity"]!["RunId"] = "old-run"));
Reject("metrics lack prepared phase", () => CheckNative(
    _ => { }, m => m["phase"] = "acquiring"));
Reject("metrics lack direct identity", () => CheckNative(
    _ => { }, m => m["acquisitionPolicy"] = "camera"));

Console.WriteLine($"MAP016 Saved Clear home admission: {cases} positive/negative inert predicates passed.");

void Reject(string label, Action action)
{
    bool rejected = false;
    try { action(); }
    catch (InvalidDataException) { rejected = true; }
    if (!rejected) throw new Exception("Unsafe MAP016 home proof accepted: " + label);
    cases++;
}

void CheckNative(Action<JsonObject> mutateNative, Action<JsonObject>? mutateMetrics = null)
{
    var n = JsonNode.Parse(native.GetRawText())!.AsObject();
    var m = JsonNode.Parse(metrics.GetRawText())!.AsObject();
    mutateNative(n);
    mutateMetrics?.Invoke(m);
    using var nDoc = JsonDocument.Parse(n.ToJsonString());
    using var mDoc = JsonDocument.Parse(m.ToJsonString());
    Map016SavedClearHomeRunProof.RequireNative(nDoc.RootElement, mDoc.RootElement,
        RunId, Home, owner);
}

MapScanState Started() => new(
    ServerId: Home, ServerIdSource: "live", ScanRunId: RunId, IsReading: true,
    Phase: "scanning", SelectedTypes: ["resource"], TotalBlocks: 2500,
    CompletedBlocks: 0, ReadBlocks: 0, FailedBlocks: 0, UnreadBlocks: 2500,
    InflightBlocks: 0, ScanMode: "fast", Concurrency: 20, ScanRate: 0,
    ProgressPercent: 0, NativeCaptureReady: true, NativePendingRecords: 0,
    NativeDroppedRecords: 0, ResumeAvailable: false, StartedAt: 999);

(JsonElement, JsonElement) Native()
{
    var batches = Enumerable.Range(0, 63)
        .Select(i => new { indices = Enumerable.Range(i * 160,
            Math.Min(160, 10000 - i * 160)).ToArray() }).ToArray();
    var response = JsonSerializer.SerializeToElement(new {
        schemaVersion = 1, probeVersion = "lwbridge-live-resource-probe-2",
        state = "proven", scanRunId = RunId, profileId = owner.ProfileId,
        launchSessionId = owner.SessionId, challenge = owner.Challenge,
        gamePid = owner.GamePid, serverId = Home, worldId = 0,
        requestId = "direct-inert-1", cameraStable = true, curViewRestored = true,
        completedAois = 10000, repliesCaptured = 63, requestsSent = 63, batches,
    });
    var prepared = JsonSerializer.SerializeToElement(new {
        source = "CurrentClientMapBlockSource.ProbeCameraFreeMapAsync",
        phase = "prepared", validatedProbeResponses = 63, uniqueCoveredAois = 10000,
        requiredAois = 10000, acquisitionPolicy = "direct",
        identity = new { RunId, ServerId = Home, WorldId = 0,
            SelectedTypesKey = "resource", ScanMode = "fast" },
    });
    return (response, prepared);
}
