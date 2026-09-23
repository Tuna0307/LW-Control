using System.Diagnostics;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class LiveDispatchSameSessionStressProof
{
    internal static async Task RunAsync()
    {
        string gameRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FunFly", "Last War-Survival Game");
        string proofRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LWBridgeRebuild", "live-dispatch-stress");
        Directory.CreateDirectory(proofRoot);
        string databasePath = Path.Combine(
            proofRoot, "dispatch-stress-" + Guid.NewGuid().ToString("N") + ".db");
        int repeatCount = int.TryParse(Environment.GetEnvironmentVariable("LWBRIDGE_DISPATCH_STRESS_COUNT"), out int parsed)
            ? Math.Clamp(parsed, 2, 10) : 3;
        int targetServerId = int.TryParse(Environment.GetEnvironmentVariable("LWBRIDGE_MANUAL_SCAN_SERVER"), out int server)
            ? server : 2207;
        int[] serverSequence = (Environment.GetEnvironmentVariable("LWBRIDGE_DISPATCH_STRESS_SERVERS") ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => int.TryParse(value, out int id) && id is >= 1 and <= 99999 ? id : -1)
            .Where(id => id > 0)
            .ToArray();
        if (serverSequence.Length == 0)
            serverSequence = Enumerable.Repeat(targetServerId, repeatCount).ToArray();
        else
            repeatCount = serverSequence.Length;
        using var lifecycle = new OverviewLifecycleService("dispatch-stress-proof", gameRoot);
        using var operationCts = new CancellationTokenSource(TimeSpan.FromMinutes(15));
        string? instanceId = null;
        Exception? operationError = null;
        try
        {
            using JsonDocument empty = JsonDocument.Parse("{}");
            object? startInstance = await lifecycle.InvokeAsync(
                "profile_instance_start", empty.RootElement, operationCts.Token).ConfigureAwait(false);
            JsonElement instanceJson = JsonSerializer.SerializeToElement(startInstance, JsonOptions.Default);
            instanceId = instanceJson.GetProperty("instanceId").GetString();

            using var store = new MapDataStore(databasePath);
            var service = new ManualMapScanCommandService(lifecycle, store);
            var source = new CurrentClientMapBlockSource(lifecycle);
            try
            {
                var observations = new List<object>();
                for (int iteration = 1; iteration <= repeatCount; iteration++)
                {
                    int requestedServerId = serverSequence[iteration - 1];
                    Stopwatch jumpWatch = Stopwatch.StartNew();
                    JsonElement jumpPayload = JsonSerializer.SerializeToElement(
                        new { serverId = requestedServerId }, JsonOptions.Default);
                    await service.InvokeAsync("server_jump", jumpPayload, operationCts.Token).ConfigureAwait(false);
                    CurrentClientMapContext context =
                        await source.GetCurrentContextAsync(operationCts.Token).ConfigureAwait(false);
                    jumpWatch.Stop();
                    if (context.ServerId != requestedServerId)
                        throw new InvalidDataException(
                            $"dispatch stress jump expected server {requestedServerId}, found {context.ServerId}.");

                    Stopwatch stopwatch = Stopwatch.StartNew();
                    JsonElement payload = JsonSerializer.SerializeToElement(new
                    {
                        profileId = "dispatch-stress-proof",
                        scanMode = "fast",
                        selectedTypes = new[] { "dispatch" },
                    }, JsonOptions.Default);
                    object? start = await service.InvokeAsync(
                        "map_scan_start", payload, operationCts.Token).ConfigureAwait(false);
                    JsonElement status = JsonSerializer.SerializeToElement(start, JsonOptions.Default);
                    string runId = status.GetProperty("scanRunId").GetString() ?? string.Empty;
                    DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMinutes(4);
                    while (DateTimeOffset.UtcNow < deadline)
                    {
                        operationCts.Token.ThrowIfCancellationRequested();
                        status = JsonSerializer.SerializeToElement(service.CreateStatus(), JsonOptions.Default);
                        string phase = status.GetProperty("phase").GetString() ?? string.Empty;
                        if (phase == "completed") break;
                        if (phase == "error")
                        {
                            string error = status.TryGetProperty("lastError", out JsonElement last)
                                ? last.GetString() ?? "unknown" : "unknown";
                            throw new InvalidDataException(
                                $"same-session dispatch iteration {iteration} failed run={runId}: {error}");
                        }
                        await Task.Delay(100, operationCts.Token).ConfigureAwait(false);
                    }
                    stopwatch.Stop();
                    status = JsonSerializer.SerializeToElement(service.CreateStatus(), JsonOptions.Default);
                    int read = status.GetProperty("readBlocks").GetInt32();
                    int failed = status.GetProperty("failedBlocks").GetInt32();
                    string finalPhase = status.GetProperty("phase").GetString() ?? string.Empty;
                    if (finalPhase != "completed" || read != 2500 || failed != 0)
                        throw new InvalidDataException(
                            $"same-session dispatch iteration {iteration} incomplete: phase={finalPhase}, read={read}, failed={failed}");
                    int serverId = status.GetProperty("serverId").GetInt32();
                    int rows = store.CountRecords("dispatch", serverId);
                    observations.Add(new
                    {
                        iteration,
                        runId,
                        serverId,
                        rows,
                        jumpSeconds = jumpWatch.Elapsed.TotalSeconds,
                        scanSeconds = stopwatch.Elapsed.TotalSeconds,
                    });
                    Console.Error.WriteLine(
                        $"DISPATCH_STRESS iteration={iteration}/{repeatCount} server={serverId} rows={rows} " +
                        $"jump={jumpWatch.Elapsed.TotalSeconds:F3}s scan={stopwatch.Elapsed.TotalSeconds:F3}s");
                }
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    ok = true,
                    proof = "same_owned_session_repeated_fast_dispatch",
                    targetServerId,
                    serverSequence,
                    repeatCount,
                    observations,
                }, JsonOptions.Default));
            }
            finally
            {
                service.Close();
            }
        }
        catch (Exception error)
        {
            operationError = error;
            throw;
        }
        finally
        {
            instanceId ??= lifecycle.GetReadyMapScanSession()?.SessionId;
            if (!string.IsNullOrWhiteSpace(instanceId))
            {
                using var stopCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                using JsonDocument stopPayload = JsonDocument.Parse(
                    JsonSerializer.Serialize(new { instanceId }, JsonOptions.Default));
                try
                {
                    await lifecycle.InvokeAsync(
                        "profile_instance_stop", stopPayload.RootElement, stopCts.Token).ConfigureAwait(false);
                }
                catch (Exception stopError)
                {
                    Console.Error.WriteLine("DISPATCH_STRESS_STOP_FAILED: " + stopError.Message);
                    if (operationError is null) throw;
                }
            }
            foreach (string candidate in new[] { databasePath, databasePath + "-wal", databasePath + "-shm" })
            {
                try { if (File.Exists(candidate)) File.Delete(candidate); } catch { }
            }
        }
    }
}
