using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class LiveOverviewLifecycleProof
{
    internal static async Task RunAsync()
    {
        string gameRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FunFly", "Last War-Survival Game");
        var results = new List<object>();

        results.Add(await RunManualLaunchAsync(gameRoot).ConfigureAwait(false));
        results.Add(await RunAutoLaunchAsync(gameRoot).ConfigureAwait(false));

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            diagnosticOk = true,
            proof = "home_overview_v17_manual_and_auto_launch",
            results,
        }, JsonOptions.Default));
    }

    private static async Task<object> RunManualLaunchAsync(string gameRoot)
    {
        using var lifecycle = new OverviewLifecycleService("home-manual-v17-proof", gameRoot);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        string? instanceId = null;
        Exception? operationError = null;
        try
        {
            using JsonDocument empty = JsonDocument.Parse("{}");
            object? start = await lifecycle.InvokeAsync(
                "profile_instance_start", empty.RootElement, timeout.Token).ConfigureAwait(false);
            JsonElement startJson = JsonSerializer.SerializeToElement(start, JsonOptions.Default);
            instanceId = startJson.GetProperty("instanceId").GetString();
            if (string.IsNullOrWhiteSpace(instanceId))
                throw new InvalidDataException("Manual Home launch did not return an owned instance id.");

            JsonElement status = JsonSerializer.SerializeToElement(
                lifecycle.CreateInstanceStatus(), JsonOptions.Default);
            RequireRunning(status, "manual Home launch");
            OverviewMapScanSession? ready = lifecycle.GetReadyMapScanSession();
            if (ready is null || ready.SessionId != instanceId)
                throw new InvalidDataException("Manual Home launch did not expose a ready owned bridge session.");

            return new
            {
                mode = "manual",
                instanceId,
                pid = status.GetProperty("pid").GetInt32(),
                connectionState = status.GetProperty("connectionState").GetString(),
            };
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
                    JsonElement stopped = JsonSerializer.SerializeToElement(
                        lifecycle.CreateInstanceStatus(), JsonOptions.Default);
                    if (stopped.GetProperty("phase").GetString() != "stopped")
                        throw new InvalidDataException("Manual Home Close Game did not return to stopped state.");
                }
                catch when (operationError is not null)
                {
                    Console.Error.WriteLine("LIVE_HOME_MANUAL_STOP_FAILED");
                }
            }
        }
    }

    private static async Task<object> RunAutoLaunchAsync(string gameRoot)
    {
        var config = new LocalConfigStore(persistent: false);
        using var lifecycle = new OverviewLifecycleService(
            "home-auto-v17-proof", gameRoot, config: config);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        string? instanceId = null;
        Exception? operationError = null;
        try
        {
            JsonElement payload = JsonSerializer.SerializeToElement(new { autoLaunchAll = true });
            object? reconcile = await lifecycle.InvokeAsync(
                "profile_instances_reconcile", payload, timeout.Token).ConfigureAwait(false);
            JsonElement reconcileJson = JsonSerializer.SerializeToElement(reconcile, JsonOptions.Default);
            if (reconcileJson.GetProperty("errors").GetArrayLength() != 0)
                throw new InvalidDataException("Open games at startup produced a startup error.");

            JsonElement status = JsonSerializer.SerializeToElement(
                lifecycle.CreateInstanceStatus(), JsonOptions.Default);
            RequireRunning(status, "Open games at startup");
            instanceId = status.GetProperty("instanceId").GetString();
            if (string.IsNullOrWhiteSpace(instanceId))
                throw new InvalidDataException("Open games at startup did not create an owned instance.");

            return new
            {
                mode = "auto-start",
                instanceId,
                pid = status.GetProperty("pid").GetInt32(),
                connectionState = status.GetProperty("connectionState").GetString(),
            };
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
                    JsonElement stopped = JsonSerializer.SerializeToElement(
                        lifecycle.CreateInstanceStatus(), JsonOptions.Default);
                    if (stopped.GetProperty("phase").GetString() != "stopped")
                        throw new InvalidDataException("Auto-start Close Game did not return to stopped state.");
                }
                catch when (operationError is not null)
                {
                    Console.Error.WriteLine("LIVE_HOME_AUTO_STOP_FAILED");
                }
            }
        }
    }

    private static void RequireRunning(JsonElement status, string scope)
    {
        if (status.GetProperty("phase").GetString() != "running" ||
            status.GetProperty("connectionState").GetString() != "connected" ||
            status.GetProperty("pid").GetInt32() <= 0)
        {
            throw new InvalidDataException(scope + " did not reach running/connected state.");
        }
    }
}
