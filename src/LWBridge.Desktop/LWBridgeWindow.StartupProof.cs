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
        object? startup = null;
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
                throw new InvalidDataException("Startup proof account was not admitted.");
            var expected = owner;
            var admitted = account;
            bool Current() => lifecycle.MatchesOwnedMapScanSession(expected) &&
                Feature018OwnedGameExchange.ReadFreshAuthenticatedAccount(
                    Path.Combine(lifecycle.RuntimeRoot, "heartbeat.json"), expected) == admitted;
            for (int i = 0; i < 120; i++)
            {
                if (!Current()) throw new InvalidDataException("Startup proof owner changed.");
                startup = await automationStatusService.InvokeAsync("automation_status", empty, token);
                var status = JsonSerializer.SerializeToElement(startup, JsonOptions.Default);
                if (status.TryGetProperty("startup", out var init) && init.TryGetProperty("ready", out var ready) && ready.ValueKind == JsonValueKind.True) break;
                if (i == 119) throw new InvalidDataException("Headless initialization did not complete: " + status.GetRawText());
                await Task.Delay(500, token);
            }
            bool homeOnly = await core.ExecuteScriptAsync("!!document.querySelector('.side-nav button') && document.querySelector('.side-nav button[aria-current=page]')===document.querySelector('.side-nav button') && !document.querySelector('[data-native-automation]')") == "true";
            if (!homeOnly) throw new InvalidDataException("Startup proof left Home or mounted an Automation page.");
            await using (var image = File.Create(Path.Combine(directory, "startup-home.png")))
                await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, image);
            await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(new { success = true, homeOnly,
                startup, native = true, fixture = false }, JsonOptions.Default), token);
        }
        finally
        {
            if (owner is not null)
            {
                using var stop = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                await lifecycle.InvokeAsync("profile_instance_stop", JsonSerializer.SerializeToElement(new { instanceId = owner.SessionId }), stop.Token);
            }
        }
    }
}
