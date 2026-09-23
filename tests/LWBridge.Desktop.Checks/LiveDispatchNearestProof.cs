using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class LiveDispatchNearestProof
{
    internal static async Task RunAsync()
    {
        string gameRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FunFly", "Last War-Survival Game");
        string proofRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LWBridgeRebuild", "live-dispatch-nearest");
        Directory.CreateDirectory(proofRoot);
        string databasePath = Path.Combine(
            proofRoot, "dispatch-nearest-" + Guid.NewGuid().ToString("N") + ".db");
        using var lifecycle = new OverviewLifecycleService("dispatch-nearest-proof", gameRoot);
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        string? instanceId = null;
        Exception? operationError = null;
        try
        {
            using JsonDocument empty = JsonDocument.Parse("{}");
            object? started = await lifecycle.InvokeAsync(
                "profile_instance_start", empty.RootElement, cts.Token).ConfigureAwait(false);
            JsonElement startJson = JsonSerializer.SerializeToElement(started, JsonOptions.Default);
            instanceId = startJson.GetProperty("instanceId").GetString();
            var source = new CurrentClientMapBlockSource(lifecycle);
            CurrentClientMapContext context =
                await source.GetCurrentContextAsync(cts.Token).ConfigureAwait(false);
            int originalServerId = context.ServerId;
            if (originalServerId <= 0)
                throw new InvalidDataException("Dispatch Quick Find proof has no live server.");

            using var store = new MapDataStore(databasePath);
            var service = new ManualMapScanCommandService(lifecycle, store);
            try
            {
                Stopwatch watch = Stopwatch.StartNew();
                object? response = await service.InvokeAsync(
                    "map_dispatch_find_nearest",
                    empty.RootElement.Clone(),
                    cts.Token).ConfigureAwait(false);
                watch.Stop();
                JsonElement result =
                    JsonSerializer.SerializeToElement(response, JsonOptions.Default);

                int serverId = result.GetProperty("serverId").GetInt32();
                int liveServerId = result.GetProperty("liveServerId").GetInt32();
                int x = result.GetProperty("x").GetInt32();
                int y = result.GetProperty("y").GetInt32();
                string pointIdText = result.GetProperty("pointId").GetString() ?? string.Empty;
                if (!long.TryParse(
                        pointIdText, NumberStyles.Integer, CultureInfo.InvariantCulture,
                        out long pointId) ||
                    pointId <= 0 ||
                    serverId <= 0 ||
                    liveServerId <= 0 ||
                    x is < 0 or >= 1000 ||
                    y is < 0 or >= 1000)
                {
                    throw new InvalidDataException(
                        "Dispatch Quick Find public command returned invalid target identity.");
                }
                bool pointDataResolved =
                    result.GetProperty("pointDataResolved").GetBoolean();
                int? pointType =
                    result.TryGetProperty("pointType", out JsonElement typeValue) &&
                    typeValue.ValueKind == JsonValueKind.Number &&
                    typeValue.TryGetInt32(out int parsedType)
                    ? parsedType : null;
                string? runtimeClass =
                    result.TryGetProperty("runtimeClass", out JsonElement classValue) &&
                    classValue.ValueKind == JsonValueKind.String
                    ? classValue.GetString() : null;
                if (pointDataResolved &&
                    (pointType != 17 ||
                     runtimeClass?.Contains(
                         "HeroDispatchMissionPointInfo",
                         StringComparison.Ordinal) != true))
                {
                    throw new InvalidDataException(
                        "Dispatch Quick Find detailed point metadata is not a Secret Task.");
                }

                int physicalAfter = lifecycle.GetLiveServerId() ?? 0;
                JsonElement status =
                    JsonSerializer.SerializeToElement(service.CreateStatus(), JsonOptions.Default);
                if (status.GetProperty("dispatchQuickFinding").GetBoolean())
                    throw new InvalidDataException(
                        "Dispatch Quick Find ownership flag remained active after completion.");

                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    ok = true,
                    proof = "public_map_dispatch_find_nearest_current_v20",
                    originalServerId,
                    wallSeconds = watch.Elapsed.TotalSeconds,
                    serverId,
                    liveServerId,
                    pointId,
                    x,
                    y,
                    pointDataResolved,
                    runtimeClass,
                    pointType,
                    cfgId = result.TryGetProperty("cfgId", out JsonElement cfgValue) &&
                        cfgValue.ValueKind == JsonValueKind.Number &&
                        cfgValue.TryGetInt32(out int cfgId) ? cfgId : (int?)null,
                    sourceElapsedSeconds =
                        result.TryGetProperty("elapsedSeconds", out JsonElement elapsedValue) &&
                        elapsedValue.TryGetDouble(out double sourceElapsed)
                        ? sourceElapsed : (double?)null,
                    identitySource =
                        result.TryGetProperty("identitySource", out JsonElement identityValue) &&
                        identityValue.ValueKind == JsonValueKind.String
                        ? identityValue.GetString() : null,
                    physicalServerAfter = physicalAfter,
                    physicalServerUnchanged = physicalAfter == originalServerId,
                    statusDispatchQuickFinding =
                        status.GetProperty("dispatchQuickFinding").GetBoolean(),
                    safety = new
                    {
                        serverJumpCommandInvoked = false,
                        stateChangingGameplayAction = false,
                    },
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
                        "profile_instance_stop", stopPayload.RootElement, stopCts.Token)
                        .ConfigureAwait(false);
                }
                catch (Exception stopError)
                {
                    Console.Error.WriteLine(
                        "DISPATCH_NEAREST_STOP_FAILED: " + stopError.Message);
                    if (operationError is null) throw;
                }
            }
            foreach (string candidate in new[]
            {
                databasePath,
                databasePath + "-wal",
                databasePath + "-shm",
            })
            {
                try { if (File.Exists(candidate)) File.Delete(candidate); } catch { }
            }
        }
    }
}
