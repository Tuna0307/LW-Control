using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Web.WebView2.Core;

namespace LWBridge.Desktop;

internal sealed partial class LWBridgeWindow
{
    // Isolated normal production runtime, starting on Home. No navigation or
    // emulated feature-page polling is used to activate background controllers.
    private async Task RunStartupHomeProofAsync(CoreWebView2 core, string outputPath)
    {
        if (productionPaths?.ExplicitIsolation != true || overviewLifecycleService is null ||
            profileRuntimeConfigStore is null || automationStatusService is null)
            throw new InvalidDataException("Startup proof requires the isolated production runtime.");
        var lifecycle = overviewLifecycleService;
        string directory = Path.GetDirectoryName(outputPath)!;
        Directory.CreateDirectory(directory);
        var initialTasks = profileRuntimeConfigStore.ReadTasksSnapshot();
        if (initialTasks.Any(pair => pair.Value is JsonObject task && task["enabled"]?.GetValue<bool>() == true) ||
            initialTasks["monsterSweep"]?["allianceDrill"]?["enabled"]?.GetValue<bool>() == true)
            throw new InvalidDataException("Startup proof must begin with all background tasks disabled.");
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(7));
        var token = deadline.Token;
        var empty = JsonSerializer.SerializeToElement(new { });
        OverviewMapScanSession? owner = null;
        Feature018GameCommandService? game = null;
        bool gatherEnabled = false, stopRequested = false;
        object? startup = null;
        JsonElement last = default;
        int gatherCount = 0;
        try
        {
            await lifecycle.InvokeAsync("profile_instance_start", empty, token);
            Feature018AuthenticatedGameAccount? account = null;
            for (int i = 0; i < 240; i++)
            {
                owner = lifecycle.GetReadyMapScanSession();
                if (owner is not null)
                {
                    account = Feature018OwnedGameExchange.ReadFreshAuthenticatedAccount(
                        Path.Combine(lifecycle.RuntimeRoot, "heartbeat.json"), owner);
                    if (account is not null) break;
                }
                await Task.Delay(500, token);
            }
            if (owner is null || account is null || !Feature018IsApprovedAccount(account.GameUid, account.ServerId))
                throw new InvalidDataException("Startup proof account was not admitted; no gathering allowed.");
            var expected = owner;
            var admitted = account;
            bool Current() => lifecycle.MatchesOwnedMapScanSession(expected) &&
                Feature018OwnedGameExchange.ReadFreshAuthenticatedAccount(
                    Path.Combine(lifecycle.RuntimeRoot, "heartbeat.json"), expected) == admitted;
            var exchange = new Feature018OwnedGameExchange(
                () => lifecycle.GetReadyMapScanSession() == expected ? expected : null,
                candidate => candidate == expected && lifecycle.MatchesOwnedMapScanSession(candidate),
                Path.Combine(lifecycle.RuntimeRoot, "feature018-request.txt"),
                Path.Combine(lifecycle.RuntimeRoot, "feature018-response.json"),
                currentAccount: candidate => Feature018OwnedGameExchange.ReadFreshAuthenticatedAccount(
                    Path.Combine(lifecycle.RuntimeRoot, "heartbeat.json"), candidate));
            game = new Feature018GameCommandService(exchange, equipmentConfigService, admitted, profileRuntimeConfigStore);
            async Task<JsonElement> Observe()
            {
                if (!Current()) throw new InvalidDataException("Startup proof owner changed.");
                return JsonSerializer.SerializeToElement(await game.InvokeAsync("feature018_automation_observe",
                    JsonSerializer.SerializeToElement(new { kind = "resourceGather" }), token), JsonOptions.Default);
            }
            for (int i = 0; i < 120; i++)
            {
                if (!Current()) throw new InvalidDataException("Startup proof owner changed.");
                startup = await automationStatusService.InvokeAsync("automation_status", empty, token);
                var status = JsonSerializer.SerializeToElement(startup, JsonOptions.Default);
                if (status.TryGetProperty("startup", out var init) && init.TryGetProperty("ready", out var ready) && ready.ValueKind == JsonValueKind.True) break;
                if (i == 119) throw new InvalidDataException("Headless initialization did not complete: " + status.GetRawText());
                await Task.Delay(500, token);
            }
            if (await core.ExecuteScriptAsync("!!document.querySelector('.side-nav button') && document.querySelector('.side-nav button[aria-current=page]')===document.querySelector('.side-nav button') && !document.querySelector('[data-native-automation]')") != "true")
                throw new InvalidDataException("Startup proof left Home or mounted an Automation page.");

            if (Environment.GetEnvironmentVariable("LWBRIDGE_STARTUP_GATHER_PROOF") == "1")
            {
                last = await Observe();
                if (last.GetProperty("gatherSquads").EnumerateArray().Any(s => s.TryGetProperty("marchUuid", out _)))
                    throw new InvalidDataException("Startup gather proof requires idle squads; no old march adoption.");
                var config = AutomationTaskConfigCommandService.Default("resourceGather");
                config["enabled"] = true;config["recallOnDisable"] = true;config["scanRadius"] = 500;
                config["squads"] = JsonSerializer.SerializeToNode(new[] { new { squadIndex = 1, enabled = true, resource = "gold", level = 10 } });
                var settings = new AutomationTaskConfigCommandService(profileRuntimeConfigStore);
                if (!Current()) throw new InvalidDataException("Startup proof owner changed before saved enable.");
                await settings.InvokeAsync("feature018_automation_config_save", JsonSerializer.SerializeToElement(new { kind = "resourceGather", config }), token);
                gatherEnabled = true;
                // No configure, run or page visit: only the background coordinator
                // may apply this persisted setting. Observe cannot configure it.
                for (int i = 0; i < 180; i++)
                {
                    last = await Observe();
                    if (last.TryGetProperty("trackedGatherSquads", out var tracked) && tracked.ValueKind == JsonValueKind.Array &&
                        tracked.EnumerateArray().Any(s => s.GetProperty("squadIndex").GetInt32() == 1 && s.GetProperty("recordMatches").GetBoolean()))
                    { gatherCount = 1;break; }
                    await Task.Delay(1000, token);
                }
                if (gatherCount != 1) throw new InvalidDataException("Saved gathering did not start while Home remained selected.");
                stopRequested = true;
                await game.InvokeAsync("feature018_automation_stop", JsonSerializer.SerializeToElement(new { kind = "resourceGather" }), token);
                config["enabled"] = false;
                await settings.InvokeAsync("feature018_automation_config_save", JsonSerializer.SerializeToElement(new { kind = "resourceGather", config }), token);
                gatherEnabled = false;
                for (int i = 0; i < 180; i++)
                {
                    last = await Observe();
                    if (!last.GetProperty("gatherSquads").EnumerateArray().Any(s => s.TryGetProperty("marchUuid", out _))) break;
                    if (i == 179) throw new InvalidDataException("Startup proof recall did not reach home.");
                    await Task.Delay(1000, token);
                }
            }
            bool homeOnly = await core.ExecuteScriptAsync("!!document.querySelector('.side-nav button') && document.querySelector('.side-nav button[aria-current=page]')===document.querySelector('.side-nav button') && !document.querySelector('[data-native-automation]')") == "true";
            if (!homeOnly) throw new InvalidDataException("Startup proof navigated away from Home.");
            await using (var image = File.Create(Path.Combine(directory, "startup-home.png")))
                await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, image);
            await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(new { success = true, homeOnly,
                startup, gatherCount, allHome = gatherCount == 0 || !last.GetProperty("gatherSquads").EnumerateArray().Any(s => s.TryGetProperty("marchUuid", out _)),
                native = true, fixture = false }, JsonOptions.Default), token);
        }
        finally
        {
            if (gatherEnabled)
            {
                if (!stopRequested && game is not null)
                {
                    stopRequested = true;
                    try { await game.InvokeAsync("feature018_automation_stop", JsonSerializer.SerializeToElement(new { kind = "resourceGather" }), CancellationToken.None); } catch { }
                }
                var saved = profileRuntimeConfigStore.ReadTasksSnapshot()["resourceGather"]?.DeepClone() as JsonObject;
                if (saved is not null)
                {
                    saved["enabled"] = false;
                    await new AutomationTaskConfigCommandService(profileRuntimeConfigStore).InvokeAsync("feature018_automation_config_save",
                        JsonSerializer.SerializeToElement(new { kind = "resourceGather", config = saved }), CancellationToken.None);
                }
            }
            if (owner is not null)
            {
                using var stop = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                await lifecycle.InvokeAsync("profile_instance_stop", JsonSerializer.SerializeToElement(new { instanceId = owner.SessionId }), stop.Token);
            }
        }
    }
}
