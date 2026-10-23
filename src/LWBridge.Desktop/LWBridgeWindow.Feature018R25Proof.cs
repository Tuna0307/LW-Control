using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Web.WebView2.Core;

namespace LWBridge.Desktop;

internal sealed partial class LWBridgeWindow
{
    // R25 packaged proof: exercise the production Equipment Schemes tab through
    // WebView DOM clicks only. No desktop input is used. The independent native
    // snapshots before/after make the UI action result reviewable without
    // treating a rendered success notice as game-state confirmation.
    private async Task RunFeature018R25EquipmentTabProofAsync(
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
                    "R25 Equipment proof lost the exact authenticated owned game session.");
        }
        async Task WaitDom(string expression, string stage, int polls = 300)
        {
            for (int i = 0; i < polls; i++)
            {
                Owned();
                if (await core.ExecuteScriptAsync(expression) == "true") return;
                await Task.Delay(100, token);
            }
            throw new InvalidDataException("R25 Equipment WebView did not settle: " + stage);
        }
        async Task<JsonElement> Snapshot()
        {
            Owned();
            object? result = await game.InvokeAsync("feature018_equipment_snapshot",
                JsonSerializer.SerializeToElement(new { profileId = owner.ProfileId }, JsonOptions.Default), token);
            Owned();
            return result is JsonElement json
                ? json.Clone()
                : throw new InvalidDataException("R25 Equipment native snapshot was unavailable.");
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
            throw new InvalidDataException("R25 Equipment WebView returned an invalid string-array snapshot.");
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
            throw new InvalidDataException("R25 Equipment config service is unavailable.");
        using var noPayload = JsonDocument.Parse("{}");
        object? savedConfigObject = await equipmentConfigService.InvokeAsync(
            "equipment_config_get", noPayload.RootElement, token);
        JsonObject proofConfig = JsonNode.Parse(
            JsonSerializer.Serialize(savedConfigObject, JsonOptions.Default)) as JsonObject
            ?? throw new InvalidDataException("R25 Equipment saved config is unavailable.");
        proofConfig["equipmentPresets"] = new JsonArray();
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

        const string selectors = "JSON.stringify([...document.querySelectorAll('[data-native-equipment=true] select')].map(s=>s.getAttribute('aria-label')+'='+s.value).sort())";
        const string heroes = "JSON.stringify([...document.querySelectorAll('[data-equipment-position]')].map(p=>p.dataset.equipmentPosition+'='+p.querySelector('.equipment-position-hero strong').textContent).sort())";
        async Task<string[]> Values() => ScriptStringArray(await core.ExecuteScriptAsync(selectors));
        async Task Click(string action)
        {
            Owned();
            if (await core.ExecuteScriptAsync("(()=>{const b=document.querySelector('[data-equipment-action=" + action + "]');if(!b||b.disabled)return false;b.click();return true;})()") != "true")
                throw new InvalidDataException("R25 Equipment button unavailable: " + action);
            await WaitDom("(()=>{const p=document.querySelector('[data-native-equipment=true]');return !!p&&!p.hasAttribute('data-equipment-busy')&&!p.querySelector('[role=alert]')&&!!p.querySelector('.equipment-result-success');})()", action, 600);
        }
        async Task RequireDom(string expression, string reason)
        {
            Owned();
            if (await core.ExecuteScriptAsync(expression) != "true") throw new InvalidDataException(reason);
        }
        async Task Drag(string sourceSelector, string targetSelector, string captureName)
        {
            string source = JsonSerializer.Serialize(sourceSelector), target = JsonSerializer.Serialize(targetSelector);
            await RequireDom("(()=>{const s=document.querySelector(" + source + ");if(!s||!s.draggable)return false;s.dispatchEvent(new DragEvent('dragstart',{bubbles:true,cancelable:true,dataTransfer:new DataTransfer()}));return true;})()", "R25 source is not draggable");
            await Task.Delay(120, token);
            await RequireDom("(()=>{const t=document.querySelector(" + target + ");if(!t)return false;t.dispatchEvent(new DragEvent('dragover',{bubbles:true,cancelable:true}));return true;})()", "R25 target missing");
            await Task.Delay(120, token);
            await RequireDom("document.querySelector(" + target + ").classList.contains('drop-target')", "R25 valid drop is not highlighted");
            await Capture(captureName + "-highlight.png");
            await core.ExecuteScriptAsync("(()=>{document.querySelector(" + target + ").dispatchEvent(new DragEvent('drop',{bubbles:true,cancelable:true}));document.querySelector(" + source + ").dispatchEvent(new DragEvent('dragend',{bubbles:true,cancelable:true}));})()");
            await Task.Delay(120, token);
            await RequireDom("!!document.querySelector('[data-equipment-plan-banner]')&&Number(document.querySelector('[data-equipment-change-count]')?.dataset.equipmentChangeCount)>0", "R25 changed plan has no banner/count");
            await Capture(captureName + ".png");
        }
        bool applyIssued = false, restoredExactly = false;
        try
        {
            await RequireDom("(()=>{const b=document.querySelectorAll('.side-nav button')[3];if(!b)return false;b.click();return true;})()", "R25 Squads unavailable");
            await WaitDom("!!document.querySelector('.squad-tabs button:nth-child(2)')", "Squads tabs");
            await RequireDom("(()=>{document.querySelector('.squad-tabs button:nth-child(2)').click();return true;})()", "Equipment tab");
            await WaitDom("(()=>{const p=document.querySelector('[data-native-equipment=true]');return !!p&&!p.hasAttribute('data-equipment-busy')&&!p.querySelector('[role=alert]')&&p.querySelectorAll('select').length>0;})()", "auto-filled gear");
            await Task.Delay(1200, token);
            int loadedSelects = JsonDocument.Parse(await core.ExecuteScriptAsync("document.querySelectorAll('[data-native-equipment=true] select').length")).RootElement.GetInt32();
            string[] autoFilled = (await Values()).Where(value => !value.EndsWith('=')).ToArray();
            if (!autoFilled.SequenceEqual(originalSelectorValues, StringComparer.Ordinal))
                throw new InvalidDataException("R25 fresh preset did not auto-fill every native equipped slot");
            await RequireDom("!document.querySelector('[data-equipment-plan-banner]')", "Auto-filled plan should match wearing now");
            await Capture("r25-equipment-auto-filled.png");
            // All four fresh preset ordinals must copy the complete live gear.
            for (int preset = 1; preset < 4; preset++)
            {
                await core.ExecuteScriptAsync("document.querySelectorAll('.equipment-preset-list button')[" + preset + "].click()");
                await Task.Delay(100, token);
                if (!(await Values()).Where(value => !value.EndsWith('=')).SequenceEqual(originalSelectorValues, StringComparer.Ordinal))
                    throw new InvalidDataException("R25 auto-fill failed for preset " + (preset + 1));
            }
            await core.ExecuteScriptAsync("document.querySelectorAll('.equipment-preset-list button')[0].click()");
            await Task.Delay(100, token);
            string[] initialPlan = await Values(), initialHeroes = ScriptStringArray(await core.ExecuteScriptAsync(heroes));
            string itemPair = await core.ExecuteScriptAsync("(()=>{const a=[...document.querySelectorAll('[data-equipment-slot]')];for(const s of a){const v=s.querySelector('select').value;if(!v)continue;const t=a.find(t=>t!==s&&t.dataset.equipmentSlot.split(':')[2]===s.dataset.equipmentSlot.split(':')[2]&&t.querySelector('select').value&&t.querySelector('select').value!==v);if(t)return {source:s.dataset.equipmentSlot,target:t.dataset.equipmentSlot};}return null;})()");
            using JsonDocument itemDoc = JsonDocument.Parse(itemPair);
            string itemSource = itemDoc.RootElement.GetProperty("source").GetString()!, itemTarget = itemDoc.RootElement.GetProperty("target").GetString()!;
            await Drag("[data-equipment-slot='" + itemSource + "']", "[data-equipment-slot='" + itemTarget + "']", "r25-equipment-single-swap");
            string[] afterItem = await Values();
            var beforeItemMap = initialPlan.Select(value => value.Split('=')).ToDictionary(parts => parts[0], parts => parts[1]);
            var afterItemMap = afterItem.Select(value => value.Split('=')).ToDictionary(parts => parts[0], parts => parts[1]);
            if (afterItemMap[itemSource] != beforeItemMap[itemTarget] || afterItemMap[itemTarget] != beforeItemMap[itemSource] ||
                initialPlan.Where(value => !value.StartsWith(itemSource + "=") && !value.StartsWith(itemTarget + "=")).Except(afterItem).Any())
                throw new InvalidDataException("R25 single-item drag did not swap exactly the two selected UUIDs");

            string setPair = await core.ExecuteScriptAsync("(()=>{const a=[...document.querySelectorAll('[data-equipment-position]')];for(const s of a){const sv=[...s.querySelectorAll('select')].map(e=>e.value).join('|');const t=a.find(t=>t!==s&&t.dataset.equipmentPosition.split(':')[0]!==s.dataset.equipmentPosition.split(':')[0]&&[...t.querySelectorAll('select')].map(e=>e.value).join('|')!==sv);if(t)return {source:s.dataset.equipmentPosition,target:t.dataset.equipmentPosition};}return null;})()");
            using JsonDocument setDoc = JsonDocument.Parse(setPair);
            string setSource = setDoc.RootElement.GetProperty("source").GetString()!, setTarget = setDoc.RootElement.GetProperty("target").GetString()!;
            await Drag("[data-equipment-drag='loadout:" + setSource + "']", "[data-equipment-position='" + setTarget + "']", "r25-equipment-full-set-swap");
            string[] finalPlan = await Values();
            var setMap = finalPlan.Select(value => value.Split('=')).ToDictionary(parts => parts[0], parts => parts[1]);
            foreach (int slot in new[] { 1, 2, 3, 4 })
                if (setMap[setSource + ":" + slot] != afterItemMap[setTarget + ":" + slot] ||
                    setMap[setTarget + ":" + slot] != afterItemMap[setSource + ":" + slot])
                    throw new InvalidDataException("R25 full set did not exchange all four slots");
            string[] afterHeroes = ScriptStringArray(await core.ExecuteScriptAsync(heroes));
            if (!initialHeroes.SequenceEqual(afterHeroes, StringComparer.Ordinal))
                throw new InvalidDataException("R25 drag moved hero identities");
            int changes = finalPlan.Except(initialPlan).Count();
            await RequireDom("Number(document.querySelector('[data-equipment-change-count]')?.dataset.equipmentChangeCount)===" + changes + "&&document.querySelectorAll('[data-equipment-changed=true]').length===" + changes, "R25 count/highlight mismatch");
            await Capture("r25-equipment-plan-banner.png");

            await Click("save");
            JsonElement afterSave = await Snapshot();
            if (!Slots(afterSave).SequenceEqual(originalSlots) || !Wearers(afterSave).SequenceEqual(originalWearers))
                throw new InvalidDataException("R25 Save configuration changed native equipment");
            await RequireDom("!!document.querySelector('[data-equipment-plan-banner]')", "R25 save cleared unapplied state");
            await Capture("r25-equipment-plan-saved.png");
            applyIssued = true;
            await Click("apply");
            JsonElement afterApply = await Snapshot();
            string[] appliedSlots = Slots(afterApply);
            if (appliedSlots.SequenceEqual(originalSlots) ||
                !SelectorValues(afterApply).SequenceEqual(finalPlan.Where(value => !value.EndsWith('=')), StringComparer.Ordinal))
                throw new InvalidDataException("R25 native equipment differs from applied plan");
            await RequireDom("!document.querySelector('[data-equipment-plan-banner]')&&document.querySelectorAll('[data-equipment-changed=true]').length===0&&document.querySelector('[data-equipment-applied=true]')!==null", "R25 applied state not confirmed in tab");
            await Capture("r25-equipment-applied.png");
            await Click("restore");
            JsonElement final = await Snapshot();
            bool slotsRestored = Slots(final).SequenceEqual(originalSlots), wearersRestored = Wearers(final).SequenceEqual(originalWearers);
            if (!slotsRestored || !wearersRestored) throw new InvalidDataException("R25 restore did not return exact starting state");
            restoredExactly = true;
            await Capture("r25-equipment-restored.png");
            await Save(new { schemaVersion = 1, state = "proven", proof = "r25_actual_packaged_equipment_tab_plan_drag_save_apply_restore",
                game = new { owner.ProfileId, owner.SessionId, owner.GamePid, uid, serverId }, loadedGearSelectors = loadedSelects,
                allFourPresetsAutoFilled = true, singleItem = new { source = itemSource, target = itemTarget, exactSwap = true },
                fullSet = new { source = setSource, target = setTarget, allFourSlotsSwapped = true }, heroesFixed = true,
                changes, countAndHighlightsMatched = true, saveLeftAllSlotsAndWearersUnchanged = true,
                nativeApplyMatchedEveryPlannedSlot = true, bannerClearedAfterNativeRead = true, appliedNotice = true,
                slotsRestoredExactly = slotsRestored, inventoryWearersRestoredExactly = wearersRestored,
                originalSlots, originalWearers, initialPlan, afterItem, finalPlan, appliedSlots, finalSlots = Slots(final), finalWearers = Wearers(final),
                appLanguage = JsonDocument.Parse(await core.ExecuteScriptAsync("document.documentElement.lang")).RootElement.GetString(),
                finishedAtUtc = DateTimeOffset.UtcNow });
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
                catch (Exception recoveryError) { await File.WriteAllTextAsync(Path.Combine(directory, "r25-equipment-restore-error.txt"), recoveryError.ToString(), CancellationToken.None); }
            }
            await Save(new
            {
                schemaVersion = 1,
                state = "partial",
                proof = "r25_actual_packaged_equipment_tab_plan_drag_save_apply_restore",
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

}
