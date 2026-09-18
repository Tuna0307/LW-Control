using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class LiveServerJumpProof
{
    internal static async Task RunAsync()
    {
        string gameRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FunFly", "Last War-Survival Game");
        using var lifecycle = new OverviewLifecycleService("server-jump-proof", gameRoot);
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

            using MapDataStore store = MapDataStore.CreateInMemory();
            var service = new ManualMapScanCommandService(lifecycle, store);
            var source = new CurrentClientMapBlockSource(lifecycle);
            try
            {
                CurrentClientMapContext initial =
                    await source.GetCurrentContextAsync(operationCts.Token).ConfigureAwait(false);
                int homeServerId = initial.ServerId;
                int targetServerId = ResolveTarget(homeServerId);

                JsonElement samePayload = JsonSerializer.SerializeToElement(
                    new { serverId = homeServerId }, JsonOptions.Default);
                object? sameResult = await service.InvokeAsync(
                    "server_jump", samePayload, operationCts.Token).ConfigureAwait(false);
                JsonElement same = JsonSerializer.SerializeToElement(sameResult, JsonOptions.Default);
                if (same.GetProperty("previousServerId").GetInt32() != homeServerId ||
                    same.GetProperty("changed").GetBoolean())
                    throw new InvalidDataException("Same-server server_jump was not proven as a no-op.");

                bool crossServerAccepted = false;
                string? crossServerBlockedReason = null;
                int? visitedServerId = null;
                try
                {
                    using var jumpCts = CancellationTokenSource.CreateLinkedTokenSource(operationCts.Token);
                    jumpCts.CancelAfter(TimeSpan.FromSeconds(45));
                    JsonElement targetPayload = JsonSerializer.SerializeToElement(
                        new { serverId = targetServerId }, JsonOptions.Default);
                    object? jumpResult = await service.InvokeAsync(
                        "server_jump", targetPayload, jumpCts.Token).ConfigureAwait(false);
                    JsonElement jumped = JsonSerializer.SerializeToElement(jumpResult, JsonOptions.Default);
                    if (jumped.GetProperty("previousServerId").GetInt32() != homeServerId ||
                        !jumped.GetProperty("changed").GetBoolean())
                        throw new InvalidDataException("Cross-server server_jump did not report the expected transition.");

                    CurrentClientMapContext away =
                        await source.GetCurrentContextAsync(jumpCts.Token).ConfigureAwait(false);
                    if (away.ServerId != targetServerId)
                        throw new InvalidDataException("Live context did not settle on the requested target server.");
                    visitedServerId = away.ServerId;
                    JsonElement returnPayload = JsonSerializer.SerializeToElement(
                        new { serverId = homeServerId }, JsonOptions.Default);
                    object? returnResult = await service.InvokeAsync(
                        "server_jump", returnPayload, jumpCts.Token).ConfigureAwait(false);
                    JsonElement returned = JsonSerializer.SerializeToElement(returnResult, JsonOptions.Default);
                    if (!returned.GetProperty("changed").GetBoolean())
                        throw new InvalidDataException("Return server_jump did not report a changed transition.");

                    CurrentClientMapContext home =
                        await source.GetCurrentContextAsync(jumpCts.Token).ConfigureAwait(false);
                    if (home.ServerId != homeServerId)
                        throw new InvalidDataException("Live context did not return to the original server.");
                    crossServerAccepted = true;
                }
                catch (BridgeCommandException error) when (
                    error.Code == "SERVER_JUMP_FAILED" &&
                    error.Message == "server_jump_precheck_failed")
                {
                    crossServerBlockedReason = error.Message;
                }

                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    ok = true,
                    proof = "server_jump_owned_session_roundtrip",
                    homeServerId,
                    targetServerId,
                    sameServerProven = true,
                    crossServerAccepted,
                    crossServerBlockedReason,
                    visitedServerId,
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
                    Console.Error.WriteLine("LIVE_SERVER_JUMP_STOP_FAILED: " + stopError.Message);
                    if (operationError is null) throw;
                }
            }
        }
    }

    private static int ResolveTarget(int homeServerId)
    {
        string? configured = Environment.GetEnvironmentVariable("LWBRIDGE_SERVER_JUMP_TARGET");
        if (int.TryParse(configured, out int parsed) && parsed is >= 1 and <= 99999 && parsed != homeServerId)
            return parsed;
        return homeServerId < 99999 ? homeServerId + 1 : homeServerId - 1;
    }
}
