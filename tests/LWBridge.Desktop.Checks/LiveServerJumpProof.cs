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

            string proofRoot = Path.Combine(Path.GetTempPath(), "lwb317-server-jump-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(proofRoot);
            var service = new Map317CommandService(Path.Combine(proofRoot, "map-data.db"), lifecycle);
            var source = new CurrentClientMapBlockSource(lifecycle);
            try
            {
                CurrentClientMapContext initial =
                    await source.GetCurrentContextAsync(operationCts.Token).ConfigureAwait(false);
                int homeServerId = initial.ServerId;
                CurrentClientMapStatusContext statusContext =
                    await source.GetMapStatusContextAsync(operationCts.Token).ConfigureAwait(false);
                int? targetServerId = statusContext.SeasonServerIds
                    .Concat(statusContext.TruckMatchServerIds)
                    .FirstOrDefault(serverId => serverId != homeServerId);
                if (targetServerId == 0) targetServerId = null;

                JsonElement samePayload = JsonSerializer.SerializeToElement(
                    new { serverId = homeServerId }, JsonOptions.Default);
                object? sameResult = await service.InvokeAsync(
                    "server_jump", samePayload, operationCts.Token).ConfigureAwait(false);
                JsonElement same = JsonSerializer.SerializeToElement(sameResult, JsonOptions.Default);
                if (same.GetProperty("previousServerId").GetInt32() != homeServerId ||
                    same.GetProperty("serverId").GetInt32() != homeServerId ||
                    same.GetProperty("changed").GetBoolean())
                    throw new InvalidDataException("Same-server server_jump was not proven as a no-op.");

                bool crossServerAccepted = false;
                string? crossServerBlockedReason = targetServerId is null
                    ? "no safe alternate server advertised by live season/truck-match state"
                    : null;
                int? visitedServerId = null;
                if (targetServerId is not null) try
                {
                    using var jumpCts = CancellationTokenSource.CreateLinkedTokenSource(operationCts.Token);
                    jumpCts.CancelAfter(TimeSpan.FromSeconds(45));
                    JsonElement targetPayload = JsonSerializer.SerializeToElement(
                        new { serverId = targetServerId.Value }, JsonOptions.Default);
                    object? jumpResult = await service.InvokeAsync(
                        "server_jump", targetPayload, jumpCts.Token).ConfigureAwait(false);
                    JsonElement jumped = JsonSerializer.SerializeToElement(jumpResult, JsonOptions.Default);
                    if (jumped.GetProperty("previousServerId").GetInt32() != homeServerId ||
                        jumped.GetProperty("serverId").GetInt32() != targetServerId.Value ||
                        !jumped.GetProperty("changed").GetBoolean())
                        throw new InvalidDataException("Cross-server server_jump did not report the expected transition.");

                    CurrentClientMapContext away =
                        await source.GetCurrentContextAsync(jumpCts.Token).ConfigureAwait(false);
                    if (away.ServerId != targetServerId.Value)
                        throw new InvalidDataException("Live context did not settle on the requested target server.");
                    visitedServerId = away.ServerId;
                    JsonElement returnPayload = JsonSerializer.SerializeToElement(
                        new { serverId = homeServerId }, JsonOptions.Default);
                    object? returnResult = await service.InvokeAsync(
                        "server_jump", returnPayload, jumpCts.Token).ConfigureAwait(false);
                    JsonElement returned = JsonSerializer.SerializeToElement(returnResult, JsonOptions.Default);
                    if (returned.GetProperty("serverId").GetInt32() != homeServerId ||
                        !returned.GetProperty("changed").GetBoolean())
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

                JsonElement historyPayload = JsonSerializer.SerializeToElement(new { }, JsonOptions.Default);
                object? historyResult = await service.InvokeAsync(
                    "server_jump_history_get", historyPayload, operationCts.Token).ConfigureAwait(false);

                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    ok = true,
                    proof = "server_jump_owned_session_roundtrip",
                    homeServerId,
                    targetServerId,
                    advertisedSeasonServerIds = statusContext.SeasonServerIds,
                    advertisedTruckMatchServerIds = statusContext.TruckMatchServerIds,
                    sameServerProven = true,
                    crossServerAccepted,
                    crossServerBlockedReason,
                    visitedServerId,
                    history = historyResult,
                }, JsonOptions.Default));
            }
            finally
            {
                service.Dispose();
                try { Directory.Delete(proofRoot, recursive: true); } catch { }
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

}
