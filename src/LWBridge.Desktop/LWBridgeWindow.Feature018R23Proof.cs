using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Web.WebView2.Core;

namespace LWBridge.Desktop;

internal sealed partial class LWBridgeWindow
{

    // R23 packaged City proof: the production page must receive game-owned
    // building icon paths and resolve them through GameAssetImage. This proof is
    // read-only: it navigates, selects one building and captures the WebView.
    // It never drags a building, applies a layout or invokes any land action.
    private async Task RunFeature018R23CityImageProofAsync(
        CoreWebView2 core, string outputPath, string directory,
        OverviewMapScanSession owner, string uid, int serverId,
        CancellationToken token)
    {
        OverviewLifecycleService lifecycle = overviewLifecycleService
            ?? throw new InvalidOperationException("FEATURE018 lifecycle missing");
        var approved = new Feature018HotkeySession(owner.ProfileId, owner.SessionId, uid, serverId);
        bool IsOwner() => Feature018IsApprovedAccount(uid, serverId) &&
            lifecycle.OwnsExactProcess(owner.GamePid) &&
            lifecycle.MatchesOwnedMapScanSession(owner) &&
            ReadFeature018AuthenticatedAccount(lifecycle, owner) == approved;
        void Owned()
        {
            token.ThrowIfCancellationRequested();
            if (!IsOwner())
                throw new InvalidDataException(
                    "R23 City image proof lost the exact authenticated owned game session.");
        }
        async Task WaitDom(string expression, string stage, int polls = 400)
        {
            for (int i = 0; i < polls; i++)
            {
                Owned();
                if (await core.ExecuteScriptAsync(expression) == "true") return;
                await Task.Delay(100, token);
            }
            throw new InvalidDataException("R23 City image WebView did not settle: " + stage);
        }
        async Task Save(object receipt) => await File.WriteAllTextAsync(outputPath,
            JsonSerializer.Serialize(receipt,
                new JsonSerializerOptions(JsonOptions.Default) { WriteIndented = true }), token);

        await Save(new
        {
            schemaVersion = 1,
            state = "running",
            proof = "actual_packaged_city_building_asset_images",
            game = new { owner.ProfileId, owner.SessionId, owner.GamePid, uid, serverId },
            startedAtUtc = DateTimeOffset.UtcNow,
        });
        try
        {
            Owned();
            if (await core.ExecuteScriptAsync(
                    "(()=>{const b=document.querySelectorAll('.side-nav button')[4];if(!b)return false;b.click();return true;})()") != "true")
                throw new InvalidDataException("R23 City image proof could not open City Layout.");
            await WaitDom(
                "(()=>{const g=document.querySelector('.city-layout-grid');const b=[...document.querySelectorAll('.city-layout-building')];return !!g&&b.length>0&&!document.querySelector('.city-layout-error[role=alert]');})()",
                "native City grid");
            await WaitDom(
                "(()=>[...document.querySelectorAll('img.city-layout-grid-icon')].some(i=>i.complete&&i.naturalWidth>0&&i.src.startsWith('data:image/')))()",
                "grid building image asset");
            if (await core.ExecuteScriptAsync(
                    "(()=>{const i=[...document.querySelectorAll('img.city-layout-grid-icon')].find(i=>i.complete&&i.naturalWidth>0&&i.src.startsWith('data:image/'));const b=i?.closest('.city-layout-building');if(!b)return false;const v=document.querySelector('.city-layout-grid-viewport');if(v){v.scrollLeft=Math.max(0,b.offsetLeft-v.clientWidth/2);v.scrollTop=Math.max(0,b.offsetTop-v.clientHeight/2)}b.click();return true;})()") != "true")
                throw new InvalidDataException("R23 City image proof could not select a building.");
            await WaitDom(
                "(()=>{const i=document.querySelector('img.city-layout-selected-icon');return !!i&&i.complete&&i.naturalWidth>0&&i.src.startsWith('data:image/');})()",
                "Building properties image asset");
            await WaitDom(
                "(()=>{const v=document.querySelector('.city-layout-grid-viewport');const i=document.querySelector('.city-layout-building[aria-pressed=true] img.city-layout-grid-icon');if(!v||!i||!i.complete||i.naturalWidth<1||!i.src.startsWith('data:image/'))return false;const a=i.getBoundingClientRect(),b=v.getBoundingClientRect();return a.right>b.left&&a.left<b.right&&a.bottom>b.top&&a.top<b.bottom;})()",
                "selected grid building visible for screenshot");

            await WaitDom("(()=>{const b=[...document.querySelectorAll('.city-layout-building')];return b.length>0&&b.every(e=>{const i=e.querySelector('img.city-layout-grid-icon');return i&&i.complete&&i.naturalWidth>0&&i.src.startsWith('data:image/')})})()", "every City building picture", 600);
            await using (var file = File.Create(Path.Combine(directory, "r23-city-100-percent.png")))
                await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, file);
            await core.ExecuteScriptAsync("(()=>{const b=document.querySelector('.city-layout-zoom button:last-child');if(b&&!b.disabled)b.click();document.querySelector('.city-layout-grid-viewport')?.scrollTo(0,0)})()");
            await Task.Delay(500, token);
            await using (var file = File.Create(Path.Combine(directory, "r23-city-zoomed-out.png")))
                await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, file);

            using JsonDocument counts = JsonDocument.Parse(await core.ExecuteScriptAsync(
                "(()=>{const v=document.querySelector('.city-layout-grid-viewport');const visible=i=>{if(!v)return false;const a=i.getBoundingClientRect(),b=v.getBoundingClientRect();return a.right>b.left&&a.left<b.right&&a.bottom>b.top&&a.top<b.bottom};return {buildings:document.querySelectorAll('.city-layout-building').length,gridImages:[...document.querySelectorAll('img.city-layout-grid-icon')].filter(i=>i.complete&&i.naturalWidth>0&&i.src.startsWith('data:image/')).length,visibleSelectedGridImages:[...document.querySelectorAll('.city-layout-building[aria-pressed=true] img.city-layout-grid-icon')].filter(i=>i.complete&&i.naturalWidth>0&&i.src.startsWith('data:image/')&&visible(i)).length,selectedImages:[...document.querySelectorAll('img.city-layout-selected-icon')].filter(i=>i.complete&&i.naturalWidth>0&&i.src.startsWith('data:image/')).length,selectedName:document.querySelector('.city-layout-selected strong')?.textContent||''}})()"));
            JsonElement data = counts.RootElement;
            int buildings = data.GetProperty("buildings").GetInt32();
            int gridImages = data.GetProperty("gridImages").GetInt32();
            int visibleSelectedGridImages = data.GetProperty("visibleSelectedGridImages").GetInt32();
            int selectedImages = data.GetProperty("selectedImages").GetInt32();
            string selectedName = data.GetProperty("selectedName").GetString() ?? "";
            if (buildings < 1 || gridImages != buildings || selectedImages != 1)
                throw new InvalidDataException("R23 City image proof did not render both building image surfaces.");

            await using (var file = File.Create(Path.Combine(directory, "r23-city-layout-building-images.png")))
                await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, file);
            Owned();
            await Save(new
            {
                schemaVersion = 1,
                state = "proven",
                proof = "actual_packaged_city_building_asset_images",
                game = new { owner.ProfileId, owner.SessionId, owner.GamePid, uid, serverId },
                buildings,
                allBuildingImagesDecoded = gridImages == buildings,
                screenshot100 = "r23-city-100-percent.png",
                screenshotZoomedOut = "r23-city-zoomed-out.png",
                loadedGridImages = gridImages,
                visibleSelectedGridImages,
                loadedPropertiesImages = selectedImages,
                selectedName,
                screenshot = "r23-city-layout-building-images.png",
                readOnly = true,
                finishedAtUtc = DateTimeOffset.UtcNow,
            });
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            await Save(new
            {
                schemaVersion = 1,
                state = "partial",
                proof = "actual_packaged_city_building_asset_images",
                game = new { owner.ProfileId, owner.SessionId, owner.GamePid, uid, serverId },
                error = error.Message,
                finishedAtUtc = DateTimeOffset.UtcNow,
            });
            throw;
        }
    }

    // R23 packaged Chat proof: exercise the three production Chat automation
    // cards through their actual WebView controls while the exact authenticated
    // game session is owned. A Red Packet enabled-setting round trip proves the
    // persisted UI adapter reaches the trusted native config. The proof restores
    // the switch to its starting value and never enables replies, Treasure search
    // or Treasure dispatch. It intentionally does not press Run now, because an
    // earlier one-shot native Treasure submission may still be awaiting a terminal
    // claimed-state and must never be replayed merely to prove the UI.
    private async Task RunFeature018R23ChatTabProofAsync(
        CoreWebView2 core, string outputPath, string directory,
        OverviewMapScanSession owner, string uid, int serverId,
        CancellationToken token)
    {
        OverviewLifecycleService lifecycle = overviewLifecycleService
            ?? throw new InvalidOperationException("FEATURE018 lifecycle missing");
        var approved = new Feature018HotkeySession(owner.ProfileId, owner.SessionId, uid, serverId);
        var game = new Feature018GameCommandService(new Feature018OwnedGameExchange(lifecycle),
            equipmentConfigService, new Feature018AuthenticatedGameAccount(uid, serverId),
            profileRuntimeConfigStore);
        ProfileRuntimeConfigStore runtimeStore = profileRuntimeConfigStore
            ?? throw new InvalidOperationException("FEATURE018 runtime config store missing");
        JsonObject initialRedPacketConfig = runtimeStore.ReadTasksSnapshot()["redPacket"] is JsonObject savedRedPacket
            ? (JsonObject)savedRedPacket.DeepClone()
            : ChatAutomationConfigCommandService.Default("redPacket");

        bool IsOwner() => Feature018IsApprovedAccount(uid, serverId) &&
            lifecycle.OwnsExactProcess(owner.GamePid) &&
            lifecycle.MatchesOwnedMapScanSession(owner) &&
            ReadFeature018AuthenticatedAccount(lifecycle, owner) == approved;
        void Owned()
        {
            token.ThrowIfCancellationRequested();
            if (!IsOwner())
                throw new InvalidDataException(
                    "R23 Chat proof lost the exact authenticated owned game session.");
        }
        async Task WaitDom(string expression, string stage, int polls = 300)
        {
            for (int i = 0; i < polls; i++)
            {
                Owned();
                if (await core.ExecuteScriptAsync(expression) == "true") return;
                await Task.Delay(100, token);
            }
            throw new InvalidDataException("R23 Chat WebView did not settle: " + stage);
        }
        async Task<JsonElement> Status()
        {
            Owned();
            object? result = await game.InvokeAsync("feature018_chat_status",
                JsonSerializer.SerializeToElement(new { profileId = owner.ProfileId }, JsonOptions.Default), token);
            Owned();
            return result is JsonElement json
                ? json.Clone()
                : throw new InvalidDataException("R23 Chat native status was unavailable.");
        }
        static bool Enabled(JsonElement status, string kind) =>
            status.GetProperty("services").GetProperty(kind).GetProperty("enabled").GetBoolean();
        async Task WaitNativeEnabled(string kind, bool expected, string stage, int polls = 100)
        {
            for (int i = 0; i < polls; i++)
            {
                JsonElement status = await Status();
                if (Enabled(status, kind) == expected) return;
                await Task.Delay(100, token);
            }
            throw new InvalidDataException("R23 Chat native config did not settle: " + stage);
        }
        async Task Capture(string name)
        {
            Owned();
            await using var file = File.Create(Path.Combine(directory, name));
            await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, file);
        }
        async Task Save(object receipt) => await File.WriteAllTextAsync(outputPath,
            JsonSerializer.Serialize(receipt,
                new JsonSerializerOptions(JsonOptions.Default) { WriteIndented = true }), token);

        await Save(new
        {
            schemaVersion = 1,
            state = "running",
            proof = "actual_packaged_chat_automation_tab",
            game = new { owner.ProfileId, owner.SessionId, owner.GamePid, uid, serverId },
            startedAtUtc = DateTimeOffset.UtcNow,
        });

        bool initialRedPacket = false;
        bool initialRead = false;
        try
        {
            Owned();
            if (await core.ExecuteScriptAsync(
                    "(()=>{const b=document.querySelectorAll('.side-nav button')[1];if(!b)return false;b.click();return true;})()") != "true")
                throw new InvalidDataException("R23 Chat proof could not open Automation.");
            await WaitDom("document.querySelectorAll('.automation-categories button').length>=5", "Automation categories");
            if (await core.ExecuteScriptAsync(
                    "(()=>{const b=document.querySelector('[data-automation-category=chat]');if(!b)return false;b.click();return true;})()") != "true")
                throw new InvalidDataException("R23 Chat proof could not open Chat category.");
            await WaitDom(
                "(()=>{const cards=[...document.querySelectorAll('[data-native-chat]')];if(cards.length!==4||!cards.every(c=>c.querySelector('.automation-card-meta-row')&&!c.querySelector('[role=alert]')))return false;return ['redPacket','fireworks'].every(k=>{const s=document.querySelector(`[data-native-chat=${k}] button[role=switch]`);return s&&!s.disabled})})()",
                "native Chat cards");

            using JsonDocument dom = JsonDocument.Parse(await core.ExecuteScriptAsync(
                "(()=>({cards:[...document.querySelectorAll('[data-native-chat]')].map(c=>({kind:c.dataset.nativeChat,state:c.querySelector('.automation-card-meta-row')?.textContent||'',enabled:c.querySelector('button[role=switch]')?.getAttribute('aria-checked')==='true'})),panel:document.querySelector('.panel-title p')?.textContent||''}))()"));
            JsonElement domRoot = dom.RootElement;
            int cardCount = domRoot.GetProperty("cards").GetArrayLength();
            if (cardCount != 3)
                throw new InvalidDataException("R23 Chat proof did not render all three native Chat cards.");
            JsonElement red = domRoot.GetProperty("cards").EnumerateArray()
                .First(card => card.GetProperty("kind").GetString() == "redPacket");
            initialRedPacket = red.GetProperty("enabled").GetBoolean();
            initialRead = true;

            JsonElement before = await Status();
            if (Enabled(before, "redPacket") != initialRedPacket)
                throw new InvalidDataException("R23 Chat WebView and native Red Packet config disagree before edit.");
            JsonElement beforeListener = before.GetProperty("listener");
            if (!beforeListener.GetProperty("ready").GetBoolean())
                throw new InvalidDataException("R23 Chat native listener is not ready.");

            await Capture("r23-chat-tab-connected.png");

            bool changed = !initialRedPacket;
            if (await core.ExecuteScriptAsync(
                    "(()=>{const c=document.querySelector('[data-native-chat=redPacket]');const b=c?.querySelector('button[role=switch]');if(!b||b.disabled)return false;b.click();return true;})()") != "true")
                throw new InvalidDataException("R23 Chat Red Packet settings switch was unavailable.");
            await WaitDom(
                $"document.querySelector('[data-native-chat=redPacket] button[role=switch]')?.getAttribute('aria-checked')==='{changed.ToString().ToLowerInvariant()}'",
                "Red Packet changed setting");
            await WaitNativeEnabled("redPacket", changed, "Red Packet changed setting");
            await WaitDom(
                "(()=>{const b=document.querySelector('[data-native-chat=redPacket] button[role=switch]');return !!b&&!b.disabled})()",
                "Red Packet changed save completed");

            if (await core.ExecuteScriptAsync(
                    "(()=>{const c=document.querySelector('[data-native-chat=redPacket]');const b=c?.querySelector('button[role=switch]');if(!b||b.disabled)return false;b.click();return true;})()") != "true")
                throw new InvalidDataException("R23 Chat Red Packet settings restore was unavailable.");
            await WaitDom(
                $"document.querySelector('[data-native-chat=redPacket] button[role=switch]')?.getAttribute('aria-checked')==='{initialRedPacket.ToString().ToLowerInvariant()}'",
                "Red Packet restored setting");
            await WaitNativeEnabled("redPacket", initialRedPacket, "Red Packet restored setting");
            await WaitDom(
                "(()=>{const b=document.querySelector('[data-native-chat=redPacket] button[role=switch]');return !!b&&!b.disabled})()",
                "Red Packet restore save completed");

            await Task.Delay(2200, token);
            JsonElement after = await Status();
            JsonElement listener = after.GetProperty("listener");
            int observedMessages = listener.GetProperty("observedMessages").GetInt32();
            int eventMessages = listener.GetProperty("eventMessages").GetInt32();
            int retainedMessages = listener.GetProperty("retainedMessages").GetInt32();
            int roomCount = listener.GetProperty("roomCount").GetInt32();
            int eventListenerCount = listener.GetProperty("eventListenerCount").GetInt32();
            if (!listener.GetProperty("ready").GetBoolean() || observedMessages < 1 || roomCount < 1 || eventListenerCount < 1)
                throw new InvalidDataException("R23 Chat listener did not observe native chat data.");

            using JsonDocument finalDom = JsonDocument.Parse(await core.ExecuteScriptAsync(
                "(()=>({cards:[...document.querySelectorAll('[data-native-chat]')].map(c=>({kind:c.dataset.nativeChat,state:c.querySelector('.automation-card-meta-row')?.textContent||'',enabled:c.querySelector('button[role=switch]')?.getAttribute('aria-checked')==='true',observed:c.querySelector('p.muted')?.textContent||''})),panel:document.querySelector('.panel-title p')?.textContent||''}))()"));
            await Capture("r23-chat-tab-listening.png");
            Owned();
            await Save(new
            {
                schemaVersion = 1,
                state = "proven",
                proof = "actual_packaged_chat_automation_tab",
                game = new { owner.ProfileId, owner.SessionId, owner.GamePid, uid, serverId },
                cards = finalDom.RootElement.GetProperty("cards").Clone(),
                panelStatus = finalDom.RootElement.GetProperty("panel").GetString(),
                realConnected = true,
                settingsSavedThroughRealSwitch = true,
                redPacketInitialEnabled = initialRedPacket,
                redPacketChangedEnabled = changed,
                redPacketRestoredEnabled = Enabled(after, "redPacket"),
                settingsRestoredExactly = Enabled(after, "redPacket") == initialRedPacket,
                listener = new { ready = true, observedMessages, eventMessages, retainedMessages, roomCount, eventListenerCount },
                noSpending = new
                {
                    redPacket = after.GetProperty("services").GetProperty("redPacket").GetProperty("capabilities").GetProperty("noSpending").GetBoolean(),
                    fireworks = after.GetProperty("services").GetProperty("fireworks").GetProperty("capabilities").GetProperty("noSpending").GetBoolean(),
                    treasure = after.GetProperty("services").GetProperty("treasure").GetProperty("capabilities").GetProperty("noSpending").GetBoolean(),
                },
                claimAction = "not_run_in_ui_harness_to_avoid_replaying_prior_unconfirmed_treasure",
                screenshotConnected = "r23-chat-tab-connected.png",
                screenshotListening = "r23-chat-tab-listening.png",
                finishedAtUtc = DateTimeOffset.UtcNow,
            });
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            if (initialRead && IsOwner())
            {
                try
                {
                    runtimeStore.SaveResourceAutomationConfig("redPacket",
                        (JsonObject)initialRedPacketConfig.DeepClone());
                    await Status();
                    await WaitNativeEnabled("redPacket", initialRedPacket,
                        "Red Packet recovery restore", 60);
                }
                catch { /* The partial receipt below records the primary proof failure. */ }
            }
            await Save(new
            {
                schemaVersion = 1,
                state = "partial",
                proof = "actual_packaged_chat_automation_tab",
                game = new { owner.ProfileId, owner.SessionId, owner.GamePid, uid, serverId },
                error = error.Message,
                finishedAtUtc = DateTimeOffset.UtcNow,
            });
            throw;
        }
    }
}
