using System.Diagnostics;
using System.Text.Json;
using LWBridge.Desktop;

// Diagnostic-only raw acquisition experiment. No production admission changes,
// engine publication, action provider or alternate default implementation.
internal static class Map019ShapeReview
{
    internal static async Task RunAsync(CurrentClientMapBlockSource source,
        OverviewMapScanSession session, string root,
        Func<OverviewMapScanSession?> readSession,
        Func<string, Task<JsonElement>> camera, CancellationToken token)
    {
        var context = await source.GetCurrentContextAsync(token);
        await Task.Delay(3000, token);
        string probe = Path.Combine(root, "live-resource");
        string resultPath = Path.Combine(probe, "camera-free-scan-result.json");
        var rounds = new List<object>();
        int ordinal = 0;
        foreach (int cap in new[] {160, 80, 80, 160})
        {
            if (readSession() != session) throw new InvalidOperationException("Shape owner retired");
            string id = "m19shape" + Guid.NewGuid().ToString("N");
            string run = "m19raw" + Guid.NewGuid().ToString("N");
            JsonElement before = await camera($"shape-{ordinal}-before");
            var lines = new[] {"schema=1", "probeVersion=lwbridge-live-resource-probe-2",
                $"requestId={id}", $"profileId={session.ProfileId}", $"launchSessionId={session.SessionId}",
                $"challenge={session.Challenge}", $"gamePid={session.GamePid}", $"scanRunId={run}",
                $"serverId={context.ServerId}", $"worldId={context.WorldId}",
                $"homeTileX={context.PlayerTileX ?? -1}", $"homeTileY={context.PlayerTileY ?? -1}",
                "includeCity=true", "includeResource=true", "includeMonster=false",
                "includeMonsterProtection=false", "includeTrain=false", "includeDispatch=false",
                "includeGhost=false", "includeTreasure=false", "includeResourceDetails=false",
                $"map019AreaCap={cap}", ""};
            string command = Path.Combine(probe, "camera-free-scan.txt");
            if (File.Exists(command)) throw new InvalidOperationException("A native command is pending");
            string temporary = command + "." + id;
            DateTimeOffset dispatched = DateTimeOffset.UtcNow;
            var timer = Stopwatch.StartNew();
            JsonElement? receipt = null;
            bool restored = false;
            try
            {
                await File.WriteAllLinesAsync(temporary, lines, token);
                File.Move(temporary, command);
                while (timer.Elapsed < TimeSpan.FromSeconds(90))
                {
                    token.ThrowIfCancellationRequested();
                    if (readSession() != session) throw new InvalidOperationException("Shape owner retired");
                    try
                    {
                        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(resultPath, token));
                        JsonElement candidate = document.RootElement;
                        if (candidate.TryGetProperty("requestId", out var rid) && rid.GetString() == id)
                        {
                            ValidateOwner(candidate, session, run, context.ServerId, context.WorldId, dispatched);
                            receipt = candidate.Clone();
                            restored = candidate.GetProperty("cameraStable").ValueKind == JsonValueKind.True &&
                                candidate.GetProperty("curViewRestored").ValueKind == JsonValueKind.True;
                            break;
                        }
                    }
                    catch (IOException) { } catch (JsonException) { }
                    await Task.Delay(100, token);
                }
                timer.Stop();
                if (receipt is not JsonElement value || !restored)
                    throw new InvalidDataException("Shape native terminal/cleanup unverified");
                await File.WriteAllTextAsync(Path.Combine(root,$"shape-{ordinal}-receipt.json"),value.GetRawText(),token);
                if (value.GetProperty("state").GetString() != "proven")
                    throw new InvalidDataException("Shape native negative: " + value.GetProperty("error").GetString());
                int expected = (10000 + cap - 1) / cap;
                if (value.GetProperty("map019AreaCap").GetInt32() != cap ||
                    value.GetProperty("completedAois").GetInt32() != 10000 ||
                    value.GetProperty("repliesCaptured").GetInt32() != expected ||
                    value.GetProperty("batches").GetArrayLength() != expected)
                    throw new InvalidDataException("Shape coverage header incomplete");
                var seen = new HashSet<int>();
                long priorTime = 0;
                foreach (JsonElement batch in value.GetProperty("batches").EnumerateArray())
                {
                    long timestamp = batch.GetProperty("capturedAtUnixMs").GetInt64();
                    if (timestamp <= priorTime) throw new InvalidDataException("Nonmonotonic native capture time");
                    priorTime = timestamp;
                    int n = batch.GetProperty("indices").GetArrayLength();
                    if (n < 1 || n > cap) throw new InvalidDataException("Invalid shape batch size");
                    foreach (var index in batch.GetProperty("indices").EnumerateArray())
                    {
                        int aoi = index.GetInt32();
                        if (aoi < 0 || aoi >= 10000 || !seen.Add(aoi))
                            throw new InvalidDataException("Missing/duplicate/out-of-bounds shape AOI");
                    }
                }
                if (seen.Count != 10000) throw new InvalidDataException("Shape missed AOIs");
                JsonElement after = await camera($"shape-{ordinal}-after");
                if (!JsonElement.DeepEquals(before.GetProperty("cameraSnapshot"),after.GetProperty("cameraSnapshot")))
                    throw new InvalidDataException("Shape changed camera geometry");
                rounds.Add(new {ordinal, cap, seconds=timer.Elapsed.TotalSeconds,
                    nativeSeconds=value.GetProperty("elapsedSeconds").GetDouble(),
                    requests=value.GetProperty("requestsSent").GetInt32(), batches=expected,
                    aoiCoverage=seen.Count, cameraRestored=true, rawAcquisitionOnly=true,
                    resourceDetailHydration=false, productionPublication=false});
                await File.WriteAllTextAsync(Path.Combine(root,"shape-review.json"),
                    JsonSerializer.Serialize(rounds,new JsonSerializerOptions{WriteIndented=true}),token);
                Console.WriteLine($"MAP019_SHAPE cap={cap} seconds={timer.Elapsed.TotalSeconds:F3} aoi={seen.Count}");
            }
            finally
            {
                if (!restored)
                {
                    // Request-bound cancel; the helper also stops this exact game if
                    // acknowledgement is absent. No subsequent experiment is started.
                    await File.WriteAllTextAsync(Path.Combine(probe,"camera-free-scan-cancel.txt"),
                        "requestId=" + id + "\n",CancellationToken.None);
                    await Task.Delay(1500,CancellationToken.None);
                }
            }
            ordinal++;
        }
    }

    private static void ValidateOwner(JsonElement value, OverviewMapScanSession session,
        string run, int server, long world, DateTimeOffset dispatched)
    {
        if (value.GetProperty("schemaVersion").GetInt32()!=1 ||
            value.GetProperty("probeVersion").GetString()!="lwbridge-live-resource-probe-2" ||
            value.GetProperty("profileId").GetString()!=session.ProfileId ||
            value.GetProperty("launchSessionId").GetString()!=session.SessionId ||
            value.GetProperty("challenge").GetString()!=session.Challenge ||
            value.GetProperty("gamePid").GetInt32()!=session.GamePid ||
            value.GetProperty("scanRunId").GetString()!=run ||
            value.GetProperty("serverId").GetInt32()!=server || value.GetProperty("worldId").GetInt32()!=world ||
            !DateTimeOffset.TryParse(value.GetProperty("capturedAt").GetString(),out var time) ||
            time < dispatched.AddSeconds(-2) || time > DateTimeOffset.UtcNow.AddSeconds(2))
            throw new InvalidDataException("Shape receipt owner/context/freshness mismatch");
    }
}
