using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace LWBridge.Desktop;

internal sealed partial class LWBridgeWindow
{
    // R22 packaged proof: exercise the production Equipment Schemes tab through
    // WebView DOM clicks only. No desktop input is used. The independent native
    // snapshots before/after make the UI action result reviewable without
    // treating a rendered success notice as game-state confirmation.
    private async Task RunFeature018R22EquipmentTabProofAsync(
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

        bool IsOwner() => Feature018IsApprovedAccount(uid, serverId) &&
            lifecycle.OwnsExactProcess(owner.GamePid) &&
            lifecycle.MatchesOwnedMapScanSession(owner) &&
            ReadFeature018AuthenticatedAccount(lifecycle, owner) == approved;
        void Owned()
        {
            token.ThrowIfCancellationRequested();
            if (!IsOwner())
                throw new InvalidDataException(
                    "R22 Equipment proof lost the exact authenticated owned game session.");
        }
        async Task WaitDom(string expression, string stage, int polls = 300)
        {
            for (int i = 0; i < polls; i++)
            {
                Owned();
                if (await core.ExecuteScriptAsync(expression) == "true") return;
                await Task.Delay(100, token);
            }
            throw new InvalidDataException("R22 Equipment WebView did not settle: " + stage);
        }
        async Task<JsonElement> Snapshot()
        {
            Owned();
            object? result = await game.InvokeAsync("feature018_equipment_snapshot",
                JsonSerializer.SerializeToElement(new { profileId = owner.ProfileId }, JsonOptions.Default), token);
            Owned();
            return result is JsonElement json
                ? json.Clone()
                : throw new InvalidDataException("R22 Equipment native snapshot was unavailable.");
        }
        static string[] Slots(JsonElement snapshot) => snapshot.GetProperty("squads").EnumerateArray()
            .SelectMany(squad => squad.GetProperty("positions").EnumerateArray().SelectMany(position =>
                position.GetProperty("equips").EnumerateArray().Select(equip =>
                    $"{squad.GetProperty("squadIndex").GetInt32()}:{position.GetProperty("position").GetInt32()}:" +
                    $"{position.GetProperty("heroUuid").GetString()}:{equip.GetProperty("slot").GetInt32()}:" +
                    equip.GetProperty("equipUuid").GetString())))
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        static string[] Wearers(JsonElement snapshot) => snapshot.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("equipUuid").GetString() + ":" +
                (item.TryGetProperty("heroUuid", out JsonElement hero) && hero.ValueKind == JsonValueKind.String
                    ? hero.GetString() : ""))
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        async Task Capture(string name)
        {
            Owned();
            await using var file = File.Create(Path.Combine(directory, name));
            await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, file);
        }
        async Task Save(object receipt) => await File.WriteAllTextAsync(outputPath,
            JsonSerializer.Serialize(receipt,
                new JsonSerializerOptions(JsonOptions.Default) { WriteIndented = true }), token);

        JsonElement original = await Snapshot();
        string[] originalSlots = Slots(original), originalWearers = Wearers(original);
        object receipt = new
        {
            schemaVersion = 1,
            state = "running",
            proof = "actual_packaged_equipment_tab_webview_buttons",
            game = new { owner.ProfileId, owner.SessionId, owner.GamePid, uid, serverId },
            startedAtUtc = DateTimeOffset.UtcNow,
            originalSlots,
            originalWearers,
        };
        await Save(receipt);

        try
        {
            Owned();
            if (await core.ExecuteScriptAsync(
                    "(()=>{const b=document.querySelectorAll('.side-nav button')[3];if(!b)return false;b.click();return true;})()") != "true")
                throw new InvalidDataException("R22 Equipment proof could not open Squads.");
            await WaitDom("!!document.querySelector('.squad-tabs button:nth-child(2)')", "Squads tabs");
            if (await core.ExecuteScriptAsync(
                    "(()=>{const b=document.querySelector('.squad-tabs button:nth-child(2)');if(!b)return false;b.click();return true;})()") != "true")
                throw new InvalidDataException("R22 Equipment proof could not open Equipment Schemes.");
            await WaitDom("(()=>{const p=document.querySelector('[data-native-equipment=true]');return !!p&&!p.querySelector('[role=alert]')&&p.querySelectorAll('[data-equipment-position]').length>0&&p.querySelectorAll('.equipment-preset-list button').length===4;})()",
                "native squads and gear");
            int loadedPositions = JsonDocument.Parse(await core.ExecuteScriptAsync(
                "document.querySelectorAll('[data-native-equipment=true] [data-equipment-position]').length"))
                .RootElement.GetInt32();
            int loadedSelects = JsonDocument.Parse(await core.ExecuteScriptAsync(
                "document.querySelectorAll('[data-native-equipment=true] select').length"))
                .RootElement.GetInt32();
            await Capture("r22-equipment-tab-loaded.png");

            // The page-level Refresh uses the same production snapshot path that
            // was broken in R21. Require the remount to return with live rows.
            if (await core.ExecuteScriptAsync(
                    "(()=>{const b=document.querySelector('.squad-header button');if(!b||b.disabled)return false;b.click();return true;})()") != "true")
                throw new InvalidDataException("R22 Equipment Refresh was unavailable.");
            await WaitDom("(()=>{const p=document.querySelector('[data-native-equipment=true]');return !!p&&!p.hasAttribute('data-equipment-busy')&&!p.querySelector('[role=alert]')&&p.querySelectorAll('[data-equipment-position]').length>0;})()",
                "Equipment Refresh");

            if (await core.ExecuteScriptAsync(
                    "(()=>{const b=document.querySelector('[data-equipment-action=capture]');if(!b||b.disabled)return false;b.click();return true;})()") != "true")
                throw new InvalidDataException("R22 Equipment preset 1 capture was unavailable.");
            await WaitDom("(()=>{const p=document.querySelector('[data-native-equipment=true]');return !!p&&!p.hasAttribute('data-equipment-busy')&&!p.querySelector('[role=alert]')&&!!p.querySelector('.equipment-result-success');})()",
                "save preset 1");

            string editRaw = await core.ExecuteScriptAsync(
                "(()=>{const s=[...document.querySelectorAll('[data-native-equipment=true] select')].find(e=>e.value);if(!s)return null;const prior=s.value,label=s.getAttribute('aria-label');s.value='';s.dispatchEvent(new Event('change',{bubbles:true}));return {prior,label};})()");
            using JsonDocument editDoc = JsonDocument.Parse(editRaw);
            if (editDoc.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("R22 Equipment proof found no equipped gear to change through the tab.");
            string removedEquipUuid = editDoc.RootElement.GetProperty("prior").GetString()!;
            string editedSlot = editDoc.RootElement.GetProperty("label").GetString()!;

            if (await core.ExecuteScriptAsync(
                    "(()=>{const b=document.querySelector('[data-equipment-action=apply]');if(!b||b.disabled)return false;b.click();return true;})()") != "true")
                throw new InvalidDataException("R22 Equipment Apply was unavailable.");
            await WaitDom("(()=>{const p=document.querySelector('[data-native-equipment=true]');return !!p&&!p.hasAttribute('data-equipment-busy')&&!p.querySelector('[role=alert]')&&!!p.querySelector('.equipment-result-success');})()",
                "apply preset 1", 500);
            JsonElement afterApply = await Snapshot();
            string[] appliedSlots = Slots(afterApply), appliedWearers = Wearers(afterApply);
            if (appliedSlots.SequenceEqual(originalSlots, StringComparer.Ordinal) &&
                appliedWearers.SequenceEqual(originalWearers, StringComparer.Ordinal))
                throw new InvalidDataException("R22 Equipment Apply reported success but native gear did not change.");

            if (await core.ExecuteScriptAsync(
                    "(()=>{const b=document.querySelector('[data-equipment-action=restore]');if(!b||b.disabled)return false;b.click();return true;})()") != "true")
                throw new InvalidDataException("R22 Equipment Restore was unavailable.");
            await WaitDom("(()=>{const p=document.querySelector('[data-native-equipment=true]');return !!p&&!p.hasAttribute('data-equipment-busy')&&!p.querySelector('[role=alert]')&&!!p.querySelector('.equipment-result-success');})()",
                "restore initial equipment", 500);
            JsonElement final = await Snapshot();
            string[] finalSlots = Slots(final), finalWearers = Wearers(final);
            bool slotsRestored = finalSlots.SequenceEqual(originalSlots, StringComparer.Ordinal);
            bool wearersRestored = finalWearers.SequenceEqual(originalWearers, StringComparer.Ordinal);
            if (!slotsRestored || !wearersRestored)
                throw new InvalidDataException("R22 Equipment Restore did not return the exact starting loadout.");
            await Capture("r22-equipment-tab-restored.png");
            await Save(new
            {
                schemaVersion = 1,
                state = "proven",
                proof = "actual_packaged_equipment_tab_webview_buttons",
                game = new { owner.ProfileId, owner.SessionId, owner.GamePid, uid, serverId },
                loadedPositions,
                loadedGearSelectors = loadedSelects,
                refreshSucceeded = true,
                preset1SavedFromTab = true,
                editedSlot,
                removedEquipUuid,
                appliedNativeChange = true,
                restoredFromTab = true,
                slotsRestoredExactly = slotsRestored,
                inventoryWearersRestoredExactly = wearersRestored,
                originalSlots,
                appliedSlots,
                finalSlots,
                finishedAtUtc = DateTimeOffset.UtcNow,
            });
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            await Save(new
            {
                schemaVersion = 1,
                state = "partial",
                proof = "actual_packaged_equipment_tab_webview_buttons",
                game = new { owner.ProfileId, owner.SessionId, owner.GamePid, uid, serverId },
                error = error.Message,
                originalSlots,
                originalWearers,
                finishedAtUtc = DateTimeOffset.UtcNow,
            });
            throw;
        }
    }

    // R22 packaged City proof: the production page must receive game-owned
    // building icon paths and resolve them through GameAssetImage. This proof is
    // read-only: it navigates, selects one building and captures the WebView.
    // It never drags a building, applies a layout or invokes any land action.
    private async Task RunFeature018R22CityImageProofAsync(
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
                    "R22 City image proof lost the exact authenticated owned game session.");
        }
        async Task WaitDom(string expression, string stage, int polls = 400)
        {
            for (int i = 0; i < polls; i++)
            {
                Owned();
                if (await core.ExecuteScriptAsync(expression) == "true") return;
                await Task.Delay(100, token);
            }
            throw new InvalidDataException("R22 City image WebView did not settle: " + stage);
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
                throw new InvalidDataException("R22 City image proof could not open City Layout.");
            await WaitDom(
                "(()=>{const g=document.querySelector('.city-layout-grid');const b=[...document.querySelectorAll('.city-layout-building')];return !!g&&b.length>0&&!document.querySelector('.city-layout-error[role=alert]');})()",
                "native City grid");
            await WaitDom(
                "(()=>[...document.querySelectorAll('img.city-layout-grid-icon')].some(i=>i.complete&&i.naturalWidth>0&&i.src.startsWith('data:image/')))()",
                "grid building image asset");
            if (await core.ExecuteScriptAsync(
                    "(()=>{const b=document.querySelector('.city-layout-building');if(!b)return false;b.click();return true;})()") != "true")
                throw new InvalidDataException("R22 City image proof could not select a building.");
            await WaitDom(
                "(()=>{const i=document.querySelector('img.city-layout-selected-icon');return !!i&&i.complete&&i.naturalWidth>0&&i.src.startsWith('data:image/');})()",
                "Building properties image asset");

            using JsonDocument counts = JsonDocument.Parse(await core.ExecuteScriptAsync(
                "(()=>({buildings:document.querySelectorAll('.city-layout-building').length,gridImages:[...document.querySelectorAll('img.city-layout-grid-icon')].filter(i=>i.complete&&i.naturalWidth>0&&i.src.startsWith('data:image/')).length,selectedImages:[...document.querySelectorAll('img.city-layout-selected-icon')].filter(i=>i.complete&&i.naturalWidth>0&&i.src.startsWith('data:image/')).length,selectedName:document.querySelector('.city-layout-selected strong')?.textContent||''}))()"));
            JsonElement data = counts.RootElement;
            int buildings = data.GetProperty("buildings").GetInt32();
            int gridImages = data.GetProperty("gridImages").GetInt32();
            int selectedImages = data.GetProperty("selectedImages").GetInt32();
            string selectedName = data.GetProperty("selectedName").GetString() ?? "";
            if (buildings < 1 || gridImages < 1 || selectedImages != 1)
                throw new InvalidDataException("R22 City image proof did not render both building image surfaces.");

            await using (var file = File.Create(Path.Combine(directory, "r22-city-layout-building-images.png")))
                await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, file);
            Owned();
            await Save(new
            {
                schemaVersion = 1,
                state = "proven",
                proof = "actual_packaged_city_building_asset_images",
                game = new { owner.ProfileId, owner.SessionId, owner.GamePid, uid, serverId },
                buildings,
                loadedGridImages = gridImages,
                loadedPropertiesImages = selectedImages,
                selectedName,
                screenshot = "r22-city-layout-building-images.png",
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
}
