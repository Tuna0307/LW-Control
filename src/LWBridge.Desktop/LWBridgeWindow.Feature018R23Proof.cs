using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace LWBridge.Desktop;

internal sealed partial class LWBridgeWindow
{
    // R23 packaged proof: exercise the production Equipment Schemes tab through
    // WebView DOM clicks only. No desktop input is used. The independent native
    // snapshots before/after make the UI action result reviewable without
    // treating a rendered success notice as game-state confirmation.
    private async Task RunFeature018R23EquipmentTabProofAsync(
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
                    "R23 Equipment proof lost the exact authenticated owned game session.");
        }
        async Task WaitDom(string expression, string stage, int polls = 300)
        {
            for (int i = 0; i < polls; i++)
            {
                Owned();
                if (await core.ExecuteScriptAsync(expression) == "true") return;
                await Task.Delay(100, token);
            }
            throw new InvalidDataException("R23 Equipment WebView did not settle: " + stage);
        }
        async Task<JsonElement> Snapshot()
        {
            Owned();
            object? result = await game.InvokeAsync("feature018_equipment_snapshot",
                JsonSerializer.SerializeToElement(new { profileId = owner.ProfileId }, JsonOptions.Default), token);
            Owned();
            return result is JsonElement json
                ? json.Clone()
                : throw new InvalidDataException("R23 Equipment native snapshot was unavailable.");
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

        bool applyIssued = false, restoredExactly = false;
        try
        {
            Owned();
            if (await core.ExecuteScriptAsync(
                    "(()=>{const b=document.querySelectorAll('.side-nav button')[3];if(!b)return false;b.click();return true;})()") != "true")
                throw new InvalidDataException("R23 Equipment proof could not open Squads.");
            await WaitDom("!!document.querySelector('.squad-tabs button:nth-child(2)')", "Squads tabs");
            if (await core.ExecuteScriptAsync(
                    "(()=>{const b=document.querySelector('.squad-tabs button:nth-child(2)');if(!b)return false;b.click();return true;})()") != "true")
                throw new InvalidDataException("R23 Equipment proof could not open Equipment Schemes.");
            await WaitDom("(()=>{const p=document.querySelector('[data-native-equipment=true]');return !!p&&!p.querySelector('[role=alert]')&&p.querySelectorAll('[data-equipment-position]').length>0&&p.querySelectorAll('.equipment-preset-list button').length===4;})()",
                "native squads and gear");
            int loadedPositions = JsonDocument.Parse(await core.ExecuteScriptAsync(
                "document.querySelectorAll('[data-native-equipment=true] [data-equipment-position]').length"))
                .RootElement.GetInt32();
            int loadedSelects = JsonDocument.Parse(await core.ExecuteScriptAsync(
                "document.querySelectorAll('[data-native-equipment=true] select').length"))
                .RootElement.GetInt32();
            await Capture("r23-equipment-tab-loaded.png");

            // The page-level Refresh uses the same production snapshot path that
            // was broken in R21. Require the remount to return with live rows.
            if (await core.ExecuteScriptAsync(
                    "(()=>{const b=document.querySelector('.squad-header button');if(!b||b.disabled)return false;b.click();return true;})()") != "true")
                throw new InvalidDataException("R23 Equipment Refresh was unavailable.");
            await WaitDom("(()=>{const p=document.querySelector('[data-native-equipment=true]');return !!p&&!p.hasAttribute('data-equipment-busy')&&!p.querySelector('[role=alert]')&&p.querySelectorAll('[data-equipment-position]').length>0;})()",
                "Equipment Refresh");

            string editRaw = await core.ExecuteScriptAsync(
                "(()=>{const all=[...document.querySelectorAll('[data-native-equipment=true] select')];if(all.some(s=>s.value))return null;const s=all.find(s=>!s.disabled&&[...s.options].some(o=>o.value));const option=s&&[...s.options].find(o=>o.value);if(!s||!option)return null;const label=s.getAttribute('aria-label');s.value=option.value;s.dispatchEvent(new Event('change',{bubbles:true}));return {picked:option.value,label};})()");
            using JsonDocument editDoc = JsonDocument.Parse(editRaw);
            if (editDoc.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("R23 fresh preset was not empty/editable with owned gear.");
            string selectedEquipUuid = editDoc.RootElement.GetProperty("picked").GetString()!;
            string editedSlot = editDoc.RootElement.GetProperty("label").GetString()!;
            // Keep the native mutation bounded: use the dropdowns to restore
            // all other slots in the fresh draft, then leave just one slot empty.
            foreach (JsonElement squad in original.GetProperty("squads").EnumerateArray())
                foreach (JsonElement position in squad.GetProperty("positions").EnumerateArray())
                    foreach (JsonElement equip in position.GetProperty("equips").EnumerateArray())
                    {
                        string label = $"{squad.GetProperty("squadIndex").GetInt32()}:{position.GetProperty("position").GetInt32()}:{equip.GetProperty("slot").GetInt32()}";
                        string code = "(()=>{const s=[...document.querySelectorAll('[data-native-equipment=true] select')].find(s=>s.getAttribute('aria-label')===" + JsonSerializer.Serialize(label) + ");if(!s||s.disabled)return false;s.value=" + JsonSerializer.Serialize(equip.GetProperty("equipUuid").GetString()) + ";s.dispatchEvent(new Event('change',{bubbles:true}));return true;})()";
                        if (await core.ExecuteScriptAsync(code) != "true") throw new InvalidDataException("R23 native dropdown missing while filling fresh preset.");
                        await Task.Delay(35, token);
                    }
            await core.ExecuteScriptAsync("(()=>{const s=[...document.querySelectorAll('[data-native-equipment=true] select')].find(s=>s.value);if(!s)return false;s.value='';s.dispatchEvent(new Event('change',{bubbles:true}));return true})()");
            await Task.Delay(100, token);
            if (await core.ExecuteScriptAsync(
                    "(()=>{const b=document.querySelector('[data-equipment-action=save]');if(!b||b.disabled)return false;b.click();return true;})()") != "true")
                throw new InvalidDataException("R23 Equipment Save was unavailable.");
            await WaitDom("(()=>{const p=document.querySelector('[data-native-equipment=true]');return !!p&&!p.hasAttribute('data-equipment-busy')&&!p.querySelector('[role=alert]')&&!!p.querySelector('.equipment-result-success');})()", "save fresh edited preset");
            await Capture("r23-equipment-fresh-preset-saved.png");

            if (await core.ExecuteScriptAsync(
                    "(()=>{const b=document.querySelector('[data-equipment-action=apply]');if(!b||b.disabled)return false;b.click();return true;})()") != "true")
                throw new InvalidDataException("R23 Equipment Apply was unavailable.");
            applyIssued = true;
            await WaitDom("(()=>{const p=document.querySelector('[data-native-equipment=true]');return !!p&&!p.hasAttribute('data-equipment-busy')&&!p.querySelector('[role=alert]')&&!!p.querySelector('.equipment-result-success');})()",
                "apply preset 1", 500);
            JsonElement afterApply = await Snapshot();
            string[] appliedSlots = Slots(afterApply), appliedWearers = Wearers(afterApply);
            if (appliedSlots.SequenceEqual(originalSlots, StringComparer.Ordinal) &&
                appliedWearers.SequenceEqual(originalWearers, StringComparer.Ordinal))
                throw new InvalidDataException("R23 Equipment Apply reported success but native gear did not change.");

            if (await core.ExecuteScriptAsync(
                    "(()=>{const b=document.querySelector('[data-equipment-action=restore]');if(!b||b.disabled)return false;b.click();return true;})()") != "true")
                throw new InvalidDataException("R23 Equipment Restore was unavailable.");
            await WaitDom("(()=>{const p=document.querySelector('[data-native-equipment=true]');return !!p&&!p.hasAttribute('data-equipment-busy')&&!p.querySelector('[role=alert]')&&!!p.querySelector('.equipment-result-success');})()",
                "restore initial equipment", 500);
            JsonElement final = await Snapshot();
            string[] finalSlots = Slots(final), finalWearers = Wearers(final);
            bool slotsRestored = finalSlots.SequenceEqual(originalSlots, StringComparer.Ordinal);
            bool wearersRestored = finalWearers.SequenceEqual(originalWearers, StringComparer.Ordinal);
            if (!slotsRestored || !wearersRestored)
                throw new InvalidDataException("R23 Equipment Restore did not return the exact starting loadout.");
            restoredExactly = true;
            await Capture("r23-equipment-tab-restored.png");
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
                selectedEquipUuid,
                freshPresetEditedBeforeCapture = true,
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
            if (applyIssued && !restoredExactly && IsOwner())
            {
                try
                {
                    object? restore = await game.InvokeAsync("feature018_equipment_restore", JsonSerializer.SerializeToElement(new { }, JsonOptions.Default), token);
                    for (int i = 0; i < 200; i++)
                    {
                        JsonElement state = JsonSerializer.SerializeToElement(restore, JsonOptions.Default);
                        if (state.TryGetProperty("equipmentApplied", out JsonElement done) && done.ValueKind == JsonValueKind.True) break;
                        await Task.Delay(100, token); Owned();
                        restore = await game.InvokeAsync("feature018_equipment_restore", JsonSerializer.SerializeToElement(new { queryOnly = true }, JsonOptions.Default), token);
                    }
                    JsonElement restored = await Snapshot();
                    restoredExactly = Slots(restored).SequenceEqual(originalSlots) && Wearers(restored).SequenceEqual(originalWearers);
                }
                catch (Exception recoveryError) { await File.WriteAllTextAsync(Path.Combine(directory, "r23-equipment-restore-error.txt"), recoveryError.ToString(), CancellationToken.None); }
            }
            await Save(new
            {
                schemaVersion = 1,
                state = "partial",
                proof = "actual_packaged_equipment_tab_webview_buttons",
                game = new { owner.ProfileId, owner.SessionId, owner.GamePid, uid, serverId },
                error = error.Message,
                originalSlots,
                originalWearers,
                restoredExactly,
                finishedAtUtc = DateTimeOffset.UtcNow,
            });
            throw;
        }
    }

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
}
