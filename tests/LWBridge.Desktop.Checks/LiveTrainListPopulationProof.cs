using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class LiveTrainListPopulationProof
{
    internal static async Task RunAsync()
    {
        string gameRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FunFly", "Last War-Survival Game");
        int[] servers = ParseServers(
            Environment.GetEnvironmentVariable("LWBRIDGE_TRAIN_LIST_SERVERS")
            ?? "2175,2180,2185,2190,2195,2196");

        using var lifecycle = new OverviewLifecycleService("train-list-population-proof", gameRoot);
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        string? instanceId = null;
        int originalServerId = 0;
        Exception? operationError = null;
        var observations = new List<object>();
        try
        {
            using JsonDocument empty = JsonDocument.Parse("{}");
            JsonElement started = JsonSerializer.SerializeToElement(
                await lifecycle.InvokeAsync(
                    "profile_instance_start", empty.RootElement, cts.Token).ConfigureAwait(false),
                JsonOptions.Default);
            instanceId = started.GetProperty("instanceId").GetString();

            using MapDataStore store = MapDataStore.CreateInMemory();
            var service = new ManualMapScanCommandService(lifecycle, store);
            var source = new CurrentClientMapBlockSource(lifecycle);
            try
            {
                CurrentClientMapContext initial =
                    await source.GetCurrentContextAsync(cts.Token).ConfigureAwait(false);
                originalServerId = initial.ServerId;

                foreach (int serverId in servers)
                {
                    try
                    {
                        int? live = lifecycle.GetLiveServerId();
                        if (live != serverId)
                        {
                            JsonElement jumpPayload = JsonSerializer.SerializeToElement(
                                new { serverId }, JsonOptions.Default);
                            _ = await service.InvokeAsync(
                                "server_jump", jumpPayload, cts.Token).ConfigureAwait(false);
                        }

                        IReadOnlyList<MapStoredRecord> rows =
                            await source.CaptureRailwayListSnapshotForTestAsync(cts.Token)
                                .ConfigureAwait(false);

                        bool followProven = false;
                        string? marchUuid = null;
                        string? ownerName = null;
                        int? quality = null;
                        long? power = null;
                        if (rows.Count > 0)
                        {
                            MapStoredRecord row = rows[0];
                            if (row.ServerId != serverId)
                                throw new InvalidDataException(
                                    $"Train-list row server {row.ServerId} did not match target {serverId}.");
                            ownerName = row.Name;
                            quality = row.Quality;
                            power = row.Power;
                            using JsonDocument data = JsonDocument.Parse(row.DataJson);
                            if (data.RootElement.TryGetProperty("marchUuid", out JsonElement marchValue) &&
                                marchValue.ValueKind == JsonValueKind.String)
                                marchUuid = marchValue.GetString();
                            if (!string.IsNullOrWhiteSpace(marchUuid))
                            {
                                JsonElement followPayload = JsonSerializer.SerializeToElement(
                                    new { serverId, marchUuid }, JsonOptions.Default);
                                JsonElement follow = JsonSerializer.SerializeToElement(
                                    await service.InvokeAsync(
                                        "map_march_follow",
                                        followPayload,
                                        cts.Token).ConfigureAwait(false),
                                    JsonOptions.Default);
                                followProven =
                                    follow.GetProperty("serverId").GetInt32() == serverId &&
                                    string.Equals(
                                        follow.GetProperty("marchUuid").GetString(),
                                        marchUuid,
                                        StringComparison.Ordinal);
                            }
                        }

                        observations.Add(new
                        {
                            serverId,
                            state = "proven",
                            rows = rows.Count,
                            sampleMarchUuid = marchUuid,
                            sampleOwnerName = ownerName,
                            sampleQuality = quality,
                            samplePower = power,
                            followProven,
                        });
                        Console.Error.WriteLine(
                            $"TRAIN_LIST_POPULATION server={serverId} rows={rows.Count} follow={followProven}");
                    }
                    catch (Exception error)
                    {
                        observations.Add(new
                        {
                            serverId,
                            state = "failed",
                            error = error.Message,
                        });
                        Console.Error.WriteLine(
                            $"TRAIN_LIST_POPULATION server={serverId} failed={error.Message}");
                    }
                }
            }
            finally
            {
                if (originalServerId > 0 &&
                    lifecycle.GetReadyMapScanSession() is not null &&
                    lifecycle.GetLiveServerId() is int liveServer &&
                    liveServer != originalServerId)
                {
                    try
                    {
                        JsonElement returnPayload = JsonSerializer.SerializeToElement(
                            new { serverId = originalServerId }, JsonOptions.Default);
                        _ = await service.InvokeAsync(
                            "server_jump", returnPayload, CancellationToken.None).ConfigureAwait(false);
                    }
                    catch (Exception error)
                    {
                        Console.Error.WriteLine("TRAIN_LIST_RETURN_FAILED: " + error.Message);
                    }
                }
                service.Close();
            }

            Console.WriteLine(JsonSerializer.Serialize(new
            {
                ok = true,
                proof = "current_client_official_train_list_population",
                originalServerId,
                servers,
                observations,
            }, JsonOptions.Default));
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
                catch (Exception error)
                {
                    Console.Error.WriteLine("TRAIN_LIST_STOP_FAILED: " + error.Message);
                    if (operationError is null) throw;
                }
            }
        }
    }

    private static int[] ParseServers(string text)
    {
        int[] servers = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => int.TryParse(value, out int parsed) ? parsed : 0)
            .Where(value => value is >= 1 and <= 99_999)
            .Distinct()
            .ToArray();
        if (servers.Length == 0)
            throw new InvalidDataException("LWBRIDGE_TRAIN_LIST_SERVERS did not contain a valid server.");
        return servers;
    }
}
