using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class LiveManualStopProof
{
    internal static async Task RunAsync()
    {
        string gameRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FunFly", "Last War-Survival Game");
        using var lifecycle = new OverviewLifecycleService("manual-stop-live-proof", gameRoot);
        using var operationCts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        string? instanceId = null;
        Exception? operationError = null;
        try
        {
            using JsonDocument empty = JsonDocument.Parse("{}");
            object? start = await lifecycle.InvokeAsync(
                "profile_instance_start", empty.RootElement, operationCts.Token).ConfigureAwait(false);
            JsonElement startJson = JsonSerializer.SerializeToElement(start, JsonOptions.Default);
            instanceId = startJson.GetProperty("instanceId").GetString();
            OverviewMapScanSession session = lifecycle.GetReadyMapScanSession() ??
                throw new InvalidOperationException("Production lifecycle did not expose a ready Manual Stop session.");
            var source = new CurrentClientMapBlockSource(lifecycle);
            CurrentClientMapContext context = await source.GetCurrentContextAsync(operationCts.Token)
                .ConfigureAwait(false);
            object proof = await LiveManualMapScanProof.RunAsync(source, context, operationCts.Token)
                .ConfigureAwait(false);
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                ok = true,
                proof = "ordinary_manual_scan_single_stop",
                sessionId = session.SessionId,
                gamePid = session.GamePid,
                result = proof,
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
                catch (Exception stopError)
                {
                    Console.Error.WriteLine("LIVE_MANUAL_STOP_CLEANUP_FAILED: " + stopError.Message);
                    if (operationError is null) throw;
                }
            }
        }
    }
}