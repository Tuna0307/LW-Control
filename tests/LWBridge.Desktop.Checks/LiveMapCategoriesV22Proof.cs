using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class LiveMapCategoriesV22Proof
{
    private static readonly string[] Kinds = ["city", "monster", "truck", "railway", "dispatch", "ghost", "treasure"];

    internal static async Task RunAsync()
    {
        string gameRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FunFly", "Last War-Survival Game");
        string proofRoot = Path.Combine(Path.GetTempPath(), "lwb317-map-categories-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(proofRoot);
        using var lifecycle = new OverviewLifecycleService("map-categories-v22-proof", gameRoot);
        using var operationCts = new CancellationTokenSource(TimeSpan.FromMinutes(12));
        string? instanceId = null;
        Exception? operationError = null;
        try
        {
            using JsonDocument empty = JsonDocument.Parse("{}");
            JsonElement started = JsonSerializer.SerializeToElement(await lifecycle.InvokeAsync("profile_instance_start", empty.RootElement, operationCts.Token), JsonOptions.Default);
            instanceId = started.GetProperty("instanceId").GetString();
            using var service = new Map317CommandService(Path.Combine(proofRoot, "map-data.db"), lifecycle);
            JsonElement startPayload = JsonSerializer.SerializeToElement(new { selectedTypes = Kinds, scanMode = "fast", resume = false }, JsonOptions.Default);
            JsonElement accepted = JsonSerializer.SerializeToElement(await service.InvokeAsync("map_scan_start", startPayload, operationCts.Token), JsonOptions.Default);
            int serverId = accepted.GetProperty("serverId").GetInt32();
            JsonElement terminal = accepted;
            while (terminal.GetProperty("isReading").GetBoolean())
            {
                await Task.Delay(500, operationCts.Token).ConfigureAwait(false);
                terminal = JsonSerializer.SerializeToElement(await service.InvokeAsync("map_scan_status", empty.RootElement, operationCts.Token), JsonOptions.Default);
            }
            if (terminal.GetProperty("phase").GetString() != "completed")
                throw new InvalidDataException("Combined non-Resource scan did not complete: " + terminal.GetRawText());

            JsonElement summary = JsonSerializer.SerializeToElement(await service.InvokeAsync("map_summary", empty.RootElement, operationCts.Token), JsonOptions.Default);
            JsonElement optionsPayload = JsonSerializer.SerializeToElement(new { serverId }, JsonOptions.Default);
            JsonElement options = JsonSerializer.SerializeToElement(await service.InvokeAsync("map_data_options", optionsPayload, operationCts.Token), JsonOptions.Default);
            var searches = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (string kind in Kinds)
            {
                JsonElement searchPayload = JsonSerializer.SerializeToElement(new
                {
                    kind,
                    query = new { serverId, page = 1, pageSize = 50, sorts = new[] { new { sortBy = "updatedAt", sortOrder = "desc" } } },
                }, JsonOptions.Default);
                JsonElement page1 = JsonSerializer.SerializeToElement(await service.InvokeAsync("map_search", searchPayload, operationCts.Token), JsonOptions.Default);
                int total = page1.GetProperty("total").GetInt32();
                object? page2 = null;
                if (total > 50)
                {
                    JsonElement page2Payload = JsonSerializer.SerializeToElement(new
                    {
                        kind,
                        query = new { serverId, page = 2, pageSize = 50, sorts = new[] { new { sortBy = "updatedAt", sortOrder = "desc" } } },
                    }, JsonOptions.Default);
                    page2 = await service.InvokeAsync("map_search", page2Payload, operationCts.Token);
                }
                searches[kind] = new { total, page1, page2 };
            }

            JsonElement clearPayload = JsonSerializer.SerializeToElement(new { serverId }, JsonOptions.Default);
            JsonElement cleared = JsonSerializer.SerializeToElement(await service.InvokeAsync("map_scan_clear", clearPayload, operationCts.Token), JsonOptions.Default);
            var source = new CurrentClientMapBlockSource(lifecycle);
            CurrentClientMapContext healthy = await source.GetCurrentContextAsync(operationCts.Token).ConfigureAwait(false);
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                ok = true,
                proof = "combined_non_resource_v22_categories",
                serverId,
                selectedTypes = Kinds,
                terminal,
                summary,
                options,
                searches,
                clear = cleared,
                sameSessionHealthy = healthy.ServerId == serverId,
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
                JsonElement stopPayload = JsonSerializer.SerializeToElement(new { instanceId }, JsonOptions.Default);
                try { await lifecycle.InvokeAsync("profile_instance_stop", stopPayload, stopCts.Token).ConfigureAwait(false); }
                catch (Exception stopError)
                {
                    Console.Error.WriteLine("LIVE_MAP_CATEGORIES_STOP_FAILED: " + stopError.Message);
                    if (operationError is null) throw;
                }
            }
            try { Directory.Delete(proofRoot, recursive: true); } catch { }
        }
    }
}
