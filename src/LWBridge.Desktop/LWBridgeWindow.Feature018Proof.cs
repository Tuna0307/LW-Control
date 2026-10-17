using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace LWBridge.Desktop;

internal sealed partial class LWBridgeWindow
{
    // Explicit opt-in. The current game bridge supports Hotkey settings, but
    // not the original game-action operations. Capture actual packed WebView
    // behavior and genuine authenticated sub-account identity without inventing
    // gameplay success or dispatching unsupported game actions.
    private async Task RunFeature018PackagedUiProofAsync(
        CoreWebView2 core, string outputPath, string directory,
        OverviewMapScanSession session, CancellationToken token)
    {
        if (productionPaths?.ExplicitIsolation != true || overviewLifecycleService is null ||
            hotkeyConfigService is null || !overviewLifecycleService.MatchesOwnedMapScanSession(session))
            throw new InvalidDataException("FEATURE018 packaged UI lost the authenticated isolated game owner.");

        void Owner()
        {
            token.ThrowIfCancellationRequested();
            if (!overviewLifecycleService.MatchesOwnedMapScanSession(session))
                throw new InvalidDataException("FEATURE018 owned game session changed.");
        }
        async Task<bool> Dom(string expression) =>
            await core.ExecuteScriptAsync(expression) == "true";
        async Task Wait(Func<Task<bool>> condition, string stage, int polls = 120)
        {
            for (int i = 0; i < polls; i++)
            {
                Owner();
                if (await condition()) return;
                await Task.Delay(100, token);
            }
            throw new InvalidDataException("FEATURE018 packaged UI did not settle: " + stage);
        }
        async Task Screenshot(string name)
        {
            Owner();
            await using var output = File.Create(Path.Combine(directory, name));
            await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, output);
        }
        async Task Navigate(int index, string selector, string stage)
        {
            Owner();
            if (!await Dom($"(()=>{{const b=document.querySelectorAll('.side-nav button')[{index}];if(!b)return false;b.click();return true;}})()"))
                throw new InvalidDataException("FEATURE018 missing " + stage + " side-nav button");
            await Wait(() => Dom($"!!document.querySelector('{selector}')"), stage);
        }

        // Record account identity before any live tab action. The heartbeat is
        // bound to the exact authenticated game PID, profile, session, challenge.
        // Game-side session heartbeat is written by Overview, not Map's
        // independent live-resource observer. This must be read while the
        // authenticated owner is running, before its Stop removes lease files.
        string heartbeatPath = Path.Combine(overviewLifecycleService.RuntimeRoot, "heartbeat.json");
        // The Overview bridge becomes ready before the account's player
        // manager has finished logging in. Never treat an early null UID as
        // authorization, or abort before the genuine account state can settle.
        JsonElement heart = default;
        for (int attempt = 0; attempt < 240; attempt++)
        {
            Owner();
            if (File.Exists(heartbeatPath))
            {
                try
                {
                    using JsonDocument heartbeat = JsonDocument.Parse(
                        await File.ReadAllTextAsync(heartbeatPath, token));
                    JsonElement candidate = heartbeat.RootElement;
                    bool loggedIn = candidate.TryGetProperty("loggedIn", out var login) &&
                        login.ValueKind == JsonValueKind.True;
                    bool connected = candidate.TryGetProperty("connected", out var connection) &&
                        connection.ValueKind == JsonValueKind.True;
                    if (OverviewLifecycleService.HeartbeatMatches(candidate, session.ProfileId,
                            session.SessionId, session.Challenge, session.GamePid,
                            DateTimeOffset.UtcNow.ToUnixTimeSeconds()) &&
                        loggedIn && connected &&
                        !string.IsNullOrWhiteSpace(ReadStatusString(candidate, "gameUid")) &&
                        ReadStatusInt32(candidate, "serverId") is int identifiedServer)
                    {
                        await File.WriteAllTextAsync(Path.Combine(directory,
                                "feature018-account-admission.json"),
                            JsonSerializer.Serialize(new
                            {
                                session.ProfileId, session.SessionId, session.GamePid,
                                uid = ReadStatusString(candidate, "gameUid"),
                                serverId = identifiedServer, loggedIn, connected,
                                verifiedAtUtc = DateTimeOffset.UtcNow,
                            }, new JsonSerializerOptions(JsonOptions.Default) { WriteIndented = true }), token);
                        if (identifiedServer != 2212)
                            throw new InvalidDataException(
                                "FEATURE018 logged-in owner is on server " + identifiedServer +
                                " instead of 2212; gameplay is denied.");
                        heart = candidate.Clone();
                        break;
                    }
                }
                catch (IOException) { }
                catch (JsonException) { }
            }
            await Task.Delay(500, token);
        }
        if (heart.ValueKind == JsonValueKind.Undefined)
            throw new InvalidDataException(
                "FEATURE018 failed to observe authenticated UID and server 2212 within 120 seconds; no game actions were issued.");
        string? uid = ReadStatusString(heart, "gameUid");
        int? serverId = ReadStatusInt32(heart, "serverId");
        if (string.IsNullOrWhiteSpace(uid) || serverId != 2212)
            throw new InvalidDataException(
                "FEATURE018 must verify the approved sub-account UID on server 2212 before interacting with live tabs.");

        if (Environment.GetEnvironmentVariable("LWBRIDGE_FEATURE018_R2_PROOF") == "1")
        {
            feature018R2AccountVerified = true;
            feature018R2ApprovedAccount = new Feature018HotkeySession(
                session.ProfileId, session.SessionId, uid!, serverId.Value);
            StartFeature018HotkeyMonitoring();
            await RunFeature018R2PackagedUiProofAsync(core, outputPath, directory,
                session, uid, serverId.Value, token);
            return;
        }

        using JsonDocument empty = JsonDocument.Parse("{}");
        HotkeyConfig first = (HotkeyConfig)(await hotkeyConfigService.InvokeAsync(
            "hotkey_config_get", empty.RootElement.Clone(), token)
            ?? throw new InvalidDataException("FEATURE018 native Hotkey config is absent."));
        async Task<HotkeyConfig> ReadConfig() =>
            (HotkeyConfig)(await hotkeyConfigService.InvokeAsync("hotkey_config_get", empty.RootElement.Clone(), token)
            ?? throw new InvalidDataException("FEATURE018 native config vanished."));
        bool cityUnavailable = false, attackPersisted = false, chestPersisted = false;
        bool landRejected = false, foodHouseRejected = false, foodHouseUnsupported = false;
        try
        {
            await Navigate(4, ".city-layout-empty[role=status]", "City Layout unsupported live snapshot");
            await Wait(() => Dom("document.querySelector('.city-layout-empty[role=status]')?.textContent?.toLowerCase()?.includes('city_layout_snapshot_get')===true"),
                "native City Layout missing-producer error");
            cityUnavailable = true;
            await Screenshot("feature018-city-layout.png");

            await Navigate(5, ".hotkey-panel[data-hotkey-category=hotkeys] .hotkey-switch",
                "Hotkeys native panel");
            await Wait(() => Dom("document.querySelectorAll('.hotkey-panel[data-hotkey-category=hotkeys] .hotkey-switch').length===7"),
                "all original Hotkeys switches");
            await Screenshot("feature018-hotkeys-before.png");
            if (!await Dom("(()=>{const b=document.querySelector('.hotkey-panel[data-hotkey-category=hotkeys] .hotkey-switch');if(!b||b.disabled)return false;b.click();return true;})()"))
                throw new InvalidDataException("FEATURE018 Hotkey attack toggle was disabled.");
            await Wait(async () => (await ReadConfig()).Attack != first.Attack,
                "Hotkeys toggle persisted in native config");
            attackPersisted = true;
            if (!await Dom("(()=>{const b=document.querySelector('.hotkey-panel[data-hotkey-category=hotkeys] .hotkey-switch');if(!b||b.disabled)return false;b.click();return true;})()"))
                throw new InvalidDataException("FEATURE018 Hotkey attack restore was disabled.");
            await Wait(async () => (await ReadConfig()).Attack == first.Attack, "Hotkeys restoration");
            await Screenshot("feature018-hotkeys-restored.png");

            await Navigate(6, ".hotkey-panel[data-hotkey-category=miniGames] .mini-game-actions",
                "Mini Games original feature cards");
            await Wait(() => Dom("!!document.querySelector('.hotkey-panel[data-hotkey-category=miniGames] .hotkey-attack-settings')===false && document.querySelectorAll('.hotkey-panel[data-hotkey-category=miniGames] .hotkey-card').length===4"),
                "all four Mini Games feature cards");
            await Wait(() => Dom("!!document.querySelector('.hotkey-panel[data-hotkey-category=miniGames] .hotkey-card .hotkey-switch')"),
                "G frontline switch native load");
            if (!await Dom("(()=>{const e=document.querySelector('.hotkey-panel[data-hotkey-category=miniGames] .hotkey-card:nth-child(2) button.toggle-row');if(!e||e.disabled)return false;e.click();return true;})()"))
                throw new InvalidDataException("FEATURE018 Black Market hint switch disabled.");
            await Wait(async () => (await ReadConfig()).TreasureChestHint != first.TreasureChestHint,
                "Black Market chest hint native preference");
            chestPersisted = true;
            if (!await Dom("(()=>{const e=document.querySelector('.hotkey-panel[data-hotkey-category=miniGames] .hotkey-card:nth-child(2) button.toggle-row');if(!e||e.disabled)return false;e.click();return true;})()"))
                throw new InvalidDataException("FEATURE018 chest hint restoration disabled.");
            await Wait(async () => (await ReadConfig()).TreasureChestHint == first.TreasureChestHint,
                "chest hint restore");
            await Wait(() => Dom("document.querySelector('.hotkey-panel[data-hotkey-category=miniGames] .hotkey-card:nth-child(4) .hotkey-state')?.textContent?.length>0"),
                "Food House native unsupported status");
            foodHouseUnsupported = await Dom("(()=>{const e=document.querySelector('.hotkey-panel[data-hotkey-category=miniGames] .hotkey-card:nth-child(4) .hotkey-state');return e?.textContent?.length>0;})()");
            await Screenshot("feature018-mini-games-before.png");
            if (!await Dom("(()=>{const b=document.querySelector('.hotkey-panel[data-hotkey-category=miniGames] .hotkey-card:nth-child(3) .mini-game-actions button');if(!b||b.disabled)return false;b.click();return true;})()"))
                throw new InvalidDataException("FEATURE018 Land Cell action disabled.");
            await Wait(() => Dom("document.querySelector('.hotkey-panel[data-hotkey-category=miniGames] .automation-error[role=alert]')?.textContent?.includes('monopoly_cell_open')===true"),
                "native land unlock unsupported error");
            landRejected = true;
            if (!await Dom("(()=>{const b=document.querySelector('.hotkey-panel[data-hotkey-category=miniGames] .hotkey-card:nth-child(4) .mini-game-actions button');if(!b||b.disabled)return false;b.click();return true;})()"))
                throw new InvalidDataException("FEATURE018 Food House action disabled.");
            await Wait(() => Dom("document.querySelector('.hotkey-panel[data-hotkey-category=miniGames] .hotkey-card:nth-child(4) .automation-error[role=alert]')?.textContent?.includes('automation_start')===true"),
                "native Food House unsupported start error");
            foodHouseRejected = true;
            await Screenshot("feature018-mini-games-errors.png");
        }
        finally
        {
            // Even a partially failed proof never leaves an opt-in hotkey
            // setting changed for the owner.
            HotkeyConfig last = await ReadConfig();
            if (last != first)
                await hotkeyConfigService.InvokeAsync("hotkey_config_save",
                    JsonSerializer.SerializeToElement(first, JsonOptions.Default), token);
        }
        if (await ReadConfig() != first)
            throw new InvalidDataException("FEATURE018 native preferences did not return to their original values.");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            state = "partial",
            proof = "actual_packaged_original_tabs_native_preferences_and_explicit_unsupported_game_commands",
            game = new { session.ProfileId, session.SessionId, session.GamePid, uid, serverId, heartbeatVerified = true },
            cityUnavailable, attackPersisted, chestPersisted, landRejected, foodHouseRejected,
            foodHouseUnsupported, configRestored = true, originalGameLanguageChanged = false,
            capturedAtUtc = DateTimeOffset.UtcNow,
        }, new JsonSerializerOptions(JsonOptions.Default) { WriteIndented = true }), token);
    }
}
