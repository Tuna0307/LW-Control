using System.Text.Json;
using System.Text.Json.Nodes;
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
        static string[] SelectorValues(JsonElement snapshot) => snapshot.GetProperty("squads").EnumerateArray()
            .SelectMany(squad => squad.GetProperty("positions").EnumerateArray().SelectMany(position =>
                position.GetProperty("equips").EnumerateArray().Select(equip =>
                    $"{squad.GetProperty("squadIndex").GetInt32()}:{position.GetProperty("position").GetInt32()}:" +
                    $"{equip.GetProperty("slot").GetInt32()}={equip.GetProperty("equipUuid").GetString()}")))
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        static string[] ScriptStringArray(string scriptResult)
        {
            using JsonDocument outer = JsonDocument.Parse(scriptResult);
            if (outer.RootElement.ValueKind == JsonValueKind.Array)
                return outer.RootElement.Deserialize<string[]>(JsonOptions.Default) ?? Array.Empty<string>();
            if (outer.RootElement.ValueKind == JsonValueKind.String)
            {
                string? inner = outer.RootElement.GetString();
                return string.IsNullOrEmpty(inner)
                    ? Array.Empty<string>()
                    : JsonSerializer.Deserialize<string[]>(inner, JsonOptions.Default) ?? Array.Empty<string>();
            }
            throw new InvalidDataException("R24 Equipment WebView returned an invalid string-array snapshot.");
        }
        static JsonArray MinimalSquads(JsonElement snapshot)
        {
            var squads = new JsonArray();
            foreach (JsonElement squad in snapshot.GetProperty("squads").EnumerateArray())
            {
                var positions = new JsonArray();
                foreach (JsonElement position in squad.GetProperty("positions").EnumerateArray())
                {
                    var equips = new JsonArray();
                    foreach (JsonElement equip in position.GetProperty("equips").EnumerateArray())
                        equips.Add(new JsonObject
                        {
                            ["slot"] = equip.GetProperty("slot").GetInt32(),
                            ["equipUuid"] = equip.GetProperty("equipUuid").GetString(),
                        });
                    positions.Add(new JsonObject
                    {
                        ["position"] = position.GetProperty("position").GetInt32(),
                        ["heroUuid"] = position.GetProperty("heroUuid").GetString(),
                        ["equips"] = equips,
                    });
                }
                squads.Add(new JsonObject
                {
                    ["squadIndex"] = squad.GetProperty("squadIndex").GetInt32(),
                    ["positions"] = positions,
                });
            }
            return squads;
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

        JsonElement original = await Snapshot();
        string[] originalSlots = Slots(original), originalWearers = Wearers(original);
        string[] originalSelectorValues = SelectorValues(original);
        if (equipmentConfigService is null)
            throw new InvalidDataException("R24 Equipment config service is unavailable.");
        using var noPayload = JsonDocument.Parse("{}");
        object? savedConfigObject = await equipmentConfigService.InvokeAsync(
            "equipment_config_get", noPayload.RootElement, token);
        JsonObject proofConfig = JsonNode.Parse(
            JsonSerializer.Serialize(savedConfigObject, JsonOptions.Default)) as JsonObject
            ?? throw new InvalidDataException("R24 Equipment saved config is unavailable.");
        proofConfig["initialEquipmentConfig"] = new JsonObject
        {
            ["ownerUid"] = uid,
            ["serverId"] = serverId,
            ["capturedAt"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ["squads"] = MinimalSquads(original),
        };
        await equipmentConfigService.InvokeAsync("equipment_config_save",
            JsonSerializer.SerializeToElement(proofConfig, JsonOptions.Default), token);
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
            await Capture("r24-equipment-tab-loaded.png");

            // The page-level Refresh uses the same production snapshot path that
            // was broken in R21. Require the remount to return with live rows.
            if (await core.ExecuteScriptAsync(
                    "(()=>{const b=document.querySelector('.squad-header button');if(!b||b.disabled)return false;b.click();return true;})()") != "true")
                throw new InvalidDataException("R23 Equipment Refresh was unavailable.");
            await WaitDom("(()=>{const p=document.querySelector('[data-native-equipment=true]');return !!p&&!p.hasAttribute('data-equipment-busy')&&!p.querySelector('[role=alert]')&&p.querySelectorAll('[data-equipment-position]').length>0;})()",
                "Equipment Refresh");

            // Save current squads first. The production component must copy all
            // four native slot types for every live formation position and the
            // instructional hint must disappear after that capture.
            if (await core.ExecuteScriptAsync(
                    "(()=>{const b=document.querySelector('[data-equipment-action=capture]');if(!b||b.disabled)return false;b.click();return true;})()") != "true")
                throw new InvalidDataException("R24 Save current squads was unavailable.");
            await WaitDom("(()=>{const p=document.querySelector('[data-native-equipment=true]');return !!p&&!p.hasAttribute('data-equipment-busy')&&!p.querySelector('[role=alert]')&&!p.querySelector('.equipment-preset-hint')&&!!p.querySelector('.equipment-result-success');})()",
                "capture current squads");
            string capturedSelectorsRaw = await core.ExecuteScriptAsync(
                "JSON.stringify([...document.querySelectorAll('[data-native-equipment=true] select')].filter(s=>s.value).map(s=>s.getAttribute('aria-label')+'='+s.value).sort())");
            string[] capturedSelectors = ScriptStringArray(capturedSelectorsRaw);
            if (!capturedSelectors.SequenceEqual(originalSelectorValues, StringComparer.Ordinal))
                throw new InvalidDataException("R24 Save current squads did not capture every equipped native slot.");
            await Capture("r24-equipment-current-squads-saved.png");

            string beforeDragRaw = await core.ExecuteScriptAsync(
                "JSON.stringify([...document.querySelectorAll('[data-native-equipment=true] select')].map(s=>s.getAttribute('aria-label')+'='+s.value).sort())");
            string[] beforeDrag = ScriptStringArray(beforeDragRaw);
            string heroMapRaw = await core.ExecuteScriptAsync(
                "JSON.stringify([...document.querySelectorAll('[data-native-equipment=true] [data-equipment-position]')].map(p=>p.getAttribute('data-equipment-position')+'='+((p.querySelector('.equipment-position-hero strong')||{}).textContent||'')).sort())");
            string[] heroMap = ScriptStringArray(heroMapRaw);

            // Single-item drag: same native slot type only. Reverse it after a
            // screenshot so the later Apply remains a separately bounded
            // dropdown mutation.
            string itemDragRaw = await core.ExecuteScriptAsync(
                "(()=>{const all=[...document.querySelectorAll('[data-native-equipment=true] [data-equipment-slot]')];const src=all.find(e=>e.querySelector('select')?.value);if(!src)return null;const slot=src.getAttribute('data-equipment-slot').split(':')[2];const dst=all.find(e=>e!==src&&e.getAttribute('data-equipment-slot').split(':')[2]===slot);if(!dst)return null;src.dispatchEvent(new Event('dragstart',{bubbles:true,cancelable:true}));return {source:src.getAttribute('data-equipment-slot'),target:dst.getAttribute('data-equipment-slot')};})()");
            using JsonDocument itemDragDoc = JsonDocument.Parse(itemDragRaw);
            if (itemDragDoc.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("R24 single equipment drag had no same-slot target.");
            string itemSource = itemDragDoc.RootElement.GetProperty("source").GetString()!;
            string itemTarget = itemDragDoc.RootElement.GetProperty("target").GetString()!;
            await Task.Delay(100, token);
            string itemDrop = "(()=>{const all=[...document.querySelectorAll('[data-equipment-slot]')];const src=all.find(e=>e.getAttribute('data-equipment-slot')===" +
                JsonSerializer.Serialize(itemSource) + ");const dst=all.find(e=>e.getAttribute('data-equipment-slot')===" +
                JsonSerializer.Serialize(itemTarget) + ");if(!src||!dst)return false;dst.dispatchEvent(new Event('dragover',{bubbles:true,cancelable:true}));dst.dispatchEvent(new Event('drop',{bubbles:true,cancelable:true}));src.dispatchEvent(new Event('dragend',{bubbles:true,cancelable:true}));return true;})()";
            if (await core.ExecuteScriptAsync(itemDrop) != "true")
                throw new InvalidDataException("R24 single equipment drag could not be dropped.");
            await Task.Delay(100, token);
            string afterItemRaw = await core.ExecuteScriptAsync(
                "JSON.stringify([...document.querySelectorAll('[data-native-equipment=true] select')].map(s=>s.getAttribute('aria-label')+'='+s.value).sort())");
            string[] afterItem = ScriptStringArray(afterItemRaw);
            if (afterItem.SequenceEqual(beforeDrag, StringComparer.Ordinal))
                throw new InvalidDataException("R24 single equipment drag did not change the saved draft.");
            await Capture("r24-equipment-single-item-drag.png");
            string reverseItemStart = "(()=>{const src=[...document.querySelectorAll('[data-equipment-slot]')].find(e=>e.getAttribute('data-equipment-slot')===" +
                JsonSerializer.Serialize(itemTarget) + ");if(!src)return false;src.dispatchEvent(new Event('dragstart',{bubbles:true,cancelable:true}));return true;})()";
            if (await core.ExecuteScriptAsync(reverseItemStart) != "true")
                throw new InvalidDataException("R24 single equipment reverse drag could not start.");
            await Task.Delay(100, token);
            string reverseItemDrop = "(()=>{const all=[...document.querySelectorAll('[data-equipment-slot]')];const src=all.find(e=>e.getAttribute('data-equipment-slot')===" +
                JsonSerializer.Serialize(itemTarget) + ");const dst=all.find(e=>e.getAttribute('data-equipment-slot')===" +
                JsonSerializer.Serialize(itemSource) + ");if(!src||!dst)return false;dst.dispatchEvent(new Event('dragover',{bubbles:true,cancelable:true}));dst.dispatchEvent(new Event('drop',{bubbles:true,cancelable:true}));src.dispatchEvent(new Event('dragend',{bubbles:true,cancelable:true}));return true;})()";
            if (await core.ExecuteScriptAsync(reverseItemDrop) != "true")
                throw new InvalidDataException("R24 single equipment drag could not be reversed.");
            await Task.Delay(100, token);

            // Full-set drag between two live hero positions, again reversed
            // before the real native mutation. Hero cards must stay fixed.
            string loadoutDragRaw = await core.ExecuteScriptAsync(
                "(()=>{const cards=[...document.querySelectorAll('[data-native-equipment=true] [data-equipment-position]')];const src=cards.find(p=>[...p.querySelectorAll('select')].some(s=>s.value));const dst=cards.find(p=>p!==src);const h=src&&src.querySelector('[data-equipment-drag^=\"loadout:\"]');if(!src||!dst||!h)return null;h.dispatchEvent(new Event('dragstart',{bubbles:true,cancelable:true}));return {source:src.getAttribute('data-equipment-position'),target:dst.getAttribute('data-equipment-position')};})()");
            using JsonDocument loadoutDragDoc = JsonDocument.Parse(loadoutDragRaw);
            if (loadoutDragDoc.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("R24 full equipment-set drag had no second equipped hero.");
            string loadoutSource = loadoutDragDoc.RootElement.GetProperty("source").GetString()!;
            string loadoutTarget = loadoutDragDoc.RootElement.GetProperty("target").GetString()!;
            await Task.Delay(100, token);
            string loadoutDrop = "(()=>{const all=[...document.querySelectorAll('[data-equipment-position]')];const src=all.find(e=>e.getAttribute('data-equipment-position')===" +
                JsonSerializer.Serialize(loadoutSource) + ");const dst=all.find(e=>e.getAttribute('data-equipment-position')===" +
                JsonSerializer.Serialize(loadoutTarget) + ");const h=src&&src.querySelector('[data-equipment-drag^=\"loadout:\"]');if(!h||!dst)return false;dst.dispatchEvent(new Event('dragover',{bubbles:true,cancelable:true}));dst.dispatchEvent(new Event('drop',{bubbles:true,cancelable:true}));h.dispatchEvent(new Event('dragend',{bubbles:true,cancelable:true}));return true;})()";
            if (await core.ExecuteScriptAsync(loadoutDrop) != "true")
                throw new InvalidDataException("R24 full equipment-set drag could not be dropped.");
            await Task.Delay(100, token);
            string afterLoadoutRaw = await core.ExecuteScriptAsync(
                "JSON.stringify([...document.querySelectorAll('[data-native-equipment=true] select')].map(s=>s.getAttribute('aria-label')+'='+s.value).sort())");
            string[] afterLoadout = ScriptStringArray(afterLoadoutRaw);
            if (afterLoadout.SequenceEqual(beforeDrag, StringComparer.Ordinal))
                throw new InvalidDataException("R24 full equipment-set drag did not change the saved draft.");
            string heroAfterDragRaw = await core.ExecuteScriptAsync(
                "JSON.stringify([...document.querySelectorAll('[data-native-equipment=true] [data-equipment-position]')].map(p=>p.getAttribute('data-equipment-position')+'='+((p.querySelector('.equipment-position-hero strong')||{}).textContent||'')).sort())");
            string[] heroAfterDrag = ScriptStringArray(heroAfterDragRaw);
            if (!heroAfterDrag.SequenceEqual(heroMap, StringComparer.Ordinal))
                throw new InvalidDataException("R24 equipment drag changed a fixed hero position.");
            await Capture("r24-equipment-full-set-drag.png");
            string reverseLoadoutStart = "(()=>{const src=[...document.querySelectorAll('[data-equipment-position]')].find(e=>e.getAttribute('data-equipment-position')===" +
                JsonSerializer.Serialize(loadoutTarget) + ");const h=src&&src.querySelector('[data-equipment-drag^=\"loadout:\"]');if(!h)return false;h.dispatchEvent(new Event('dragstart',{bubbles:true,cancelable:true}));return true;})()";
            if (await core.ExecuteScriptAsync(reverseLoadoutStart) != "true")
                throw new InvalidDataException("R24 full equipment-set reverse drag could not start.");
            await Task.Delay(100, token);
            string reverseLoadoutDrop = "(()=>{const all=[...document.querySelectorAll('[data-equipment-position]')];const src=all.find(e=>e.getAttribute('data-equipment-position')===" +
                JsonSerializer.Serialize(loadoutTarget) + ");const dst=all.find(e=>e.getAttribute('data-equipment-position')===" +
                JsonSerializer.Serialize(loadoutSource) + ");const h=src&&src.querySelector('[data-equipment-drag^=\"loadout:\"]');if(!h||!dst)return false;dst.dispatchEvent(new Event('dragover',{bubbles:true,cancelable:true}));dst.dispatchEvent(new Event('drop',{bubbles:true,cancelable:true}));h.dispatchEvent(new Event('dragend',{bubbles:true,cancelable:true}));return true;})()";
            if (await core.ExecuteScriptAsync(reverseLoadoutDrop) != "true")
                throw new InvalidDataException("R24 full equipment-set drag could not be reversed.");
            await Task.Delay(100, token);
            string restoredDraftRaw = await core.ExecuteScriptAsync(
                "JSON.stringify([...document.querySelectorAll('[data-native-equipment=true] select')].map(s=>s.getAttribute('aria-label')+'='+s.value).sort())");
            string[] restoredDraft = ScriptStringArray(restoredDraftRaw);
            if (!restoredDraft.SequenceEqual(beforeDrag, StringComparer.Ordinal))
                throw new InvalidDataException("R24 drag reversals did not restore the captured draft exactly.");

            string editRaw = await core.ExecuteScriptAsync(
                "(()=>{const s=[...document.querySelectorAll('[data-native-equipment=true] select')].find(s=>!s.disabled&&s.value);if(!s)return null;const picked=s.value,label=s.getAttribute('aria-label');s.value='';s.dispatchEvent(new Event('change',{bubbles:true}));return {picked,label};})()");
            using JsonDocument editDoc = JsonDocument.Parse(editRaw);
            if (editDoc.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("R24 native dropdown had no equipped slot to change.");
            string selectedEquipUuid = editDoc.RootElement.GetProperty("picked").GetString()!;
            string editedSlot = editDoc.RootElement.GetProperty("label").GetString()!;
            await Task.Delay(100, token);
            if (await core.ExecuteScriptAsync(
                    "(()=>{const b=document.querySelector('[data-equipment-action=save]');if(!b||b.disabled)return false;b.click();return true;})()") != "true")
                throw new InvalidDataException("R24 Equipment Save was unavailable.");
            await WaitDom("(()=>{const p=document.querySelector('[data-native-equipment=true]');return !!p&&!p.hasAttribute('data-equipment-busy')&&!p.querySelector('[role=alert]')&&!!p.querySelector('.equipment-result-success');})()", "save dropdown edit");
            await Capture("r24-equipment-dropdown-saved.png");

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
                throw new InvalidDataException("R24 Equipment Apply reported success but native gear did not change.");
            await Capture("r24-equipment-applied.png");

            if (await core.ExecuteScriptAsync(
                    "(()=>{const b=document.querySelector('[data-equipment-action=restore]');if(!b||b.disabled)return false;b.click();return true;})()") != "true")
                throw new InvalidDataException("R24 Equipment Restore was unavailable.");
            await WaitDom("(()=>{const p=document.querySelector('[data-native-equipment=true]');return !!p&&!p.hasAttribute('data-equipment-busy')&&!p.querySelector('[role=alert]')&&!!p.querySelector('.equipment-result-success');})()",
                "restore initial equipment", 500);
            JsonElement final = await Snapshot();
            string[] finalSlots = Slots(final), finalWearers = Wearers(final);
            bool slotsRestored = finalSlots.SequenceEqual(originalSlots, StringComparer.Ordinal);
            bool wearersRestored = finalWearers.SequenceEqual(originalWearers, StringComparer.Ordinal);
            if (!slotsRestored || !wearersRestored)
                throw new InvalidDataException("R24 Equipment Restore did not return the exact starting loadout.");
            restoredExactly = true;
            await Capture("r24-equipment-tab-restored.png");
            await Save(new
            {
                schemaVersion = 1,
                state = "proven",
                proof = "r24_actual_packaged_equipment_tab_webview_buttons_and_drag",
                game = new { owner.ProfileId, owner.SessionId, owner.GamePid, uid, serverId },
                loadedPositions,
                loadedGearSelectors = loadedSelects,
                refreshSucceeded = true,
                preset1SavedFromTab = true,
                capturedEveryEquippedNativeSlot = true,
                captureHintCleared = true,
                singleItemDrag = new { source = itemSource, target = itemTarget, sameSlotOnly = true, reversedExactly = true },
                fullSetDrag = new { source = loadoutSource, target = loadoutTarget, heroesStayedFixed = true, reversedExactly = true },
                editedSlot,
                selectedEquipUuid,
                dropdownEditedAfterCapture = true,
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
                proof = "r24_actual_packaged_equipment_tab_webview_buttons_and_drag",
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
