using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Web.WebView2.Core;

namespace LWBridge.Desktop;

internal sealed partial class LWBridgeWindow
{
    // R28 packaged proof: exercise the production Equipment Schemes tab through
    // WebView DOM clicks only. No desktop input is used. The independent native
    // snapshots before/after make the UI action result reviewable without
    // treating a rendered success notice as game-state confirmation.
    private async Task RunFeature018R28EquipmentTabProofAsync(
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
                    "R28 Equipment proof lost the exact authenticated owned game session.");
        }
        async Task WaitDom(string expression, string stage, int polls = 300)
        {
            for (int i = 0; i < polls; i++)
            {
                Owned();
                if (await core.ExecuteScriptAsync(expression) == "true") return;
                await Task.Delay(100, token);
            }
            string alert = await core.ExecuteScriptAsync("document.querySelector('[data-native-equipment] [role=alert]')?.textContent||''");
            throw new InvalidDataException("R28 Equipment WebView did not settle: " + stage + "; tab error=" + alert);
        }
        async Task<JsonElement> Snapshot()
        {
            // This independent observer is read-only. A heartbeat write can
            // interrupt admission between two reads; wait for fresh authority
            // instead of aborting the proof or retrying a gameplay mutation.
            for (int attempt = 0; attempt < 20; attempt++)
            {
                token.ThrowIfCancellationRequested();
                if (!Feature018IsApprovedAccount(uid, serverId) ||
                    !lifecycle.OwnsExactProcess(owner.GamePid) || !lifecycle.MatchesOwnedMapScanSession(owner))
                    throw new InvalidDataException("R28 snapshot owner session changed.");
                var account = ReadFeature018AuthenticatedAccount(lifecycle, owner);
                if (account is not null && account != approved)
                    throw new InvalidDataException("R28 snapshot authenticated account changed.");
                if (account == approved)
                {
                    try
                    {
                        object? result = await game.InvokeAsync("feature018_equipment_snapshot",
                            JsonSerializer.SerializeToElement(new { profileId = owner.ProfileId }, JsonOptions.Default), token);
                        if (result is not JsonElement json)
                            throw new InvalidDataException("R28 Equipment native snapshot was unavailable.");
                        if (IsOwner()) return json.Clone();
                    }
                    catch (BridgeCommandException error) when (error.Code == "GAME_ACCOUNT_NOT_AUTHORIZED") { }
                }
                await Task.Delay(100, token);
            }
            throw new InvalidDataException("R28 snapshot fresh account admission stayed unavailable.");
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
            throw new InvalidDataException("R28 Equipment WebView returned an invalid string-array snapshot.");
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
        var checkpoints = new List<object>();
        bool exactRestore = false;
        if (equipmentConfigService is null) throw new InvalidDataException("Equipment config missing");
        using var empty = JsonDocument.Parse("{}");
        JsonObject config = JsonNode.Parse(JsonSerializer.Serialize(await equipmentConfigService.InvokeAsync("equipment_config_get", empty.RootElement, token), JsonOptions.Default))!.AsObject();
        config["initialEquipmentConfig"] = new JsonObject { ["ownerUid"] = uid, ["serverId"] = serverId,
            ["squads"] = JsonNode.Parse(original.GetProperty("squads").GetRawText()) };
        await equipmentConfigService.InvokeAsync("equipment_config_save", JsonSerializer.SerializeToElement(config, JsonOptions.Default), token);
        async Task Ready(string stage) => await WaitDom("(()=>{const p=document.querySelector('[data-native-equipment=true]');return !!p&&!p.hasAttribute('data-equipment-busy')&&!p.querySelector('[role=alert]')&&p.querySelector('[data-equipment-sync=synced]')!==null&&p.querySelectorAll('[data-equipment-preset]').length===4;})()", stage, 600);
        async Task Set(string label, string value)
        {
            Owned();
            if (await core.ExecuteScriptAsync("(()=>{const s=document.querySelector('select[aria-label=" + JsonSerializer.Serialize(label) + "]');if(!s||s.disabled)return false;Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype,'value').set.call(s," + JsonSerializer.Serialize(value) + ");s.dispatchEvent(new Event('change',{bubbles:true}));return true;})()") != "true")
                throw new InvalidDataException("R28 selector unavailable");
            await Task.Delay(200, token); await Ready("live gear edit");
        }
        async Task Click(string action)
        {
            if (await core.ExecuteScriptAsync("(()=>{const b=document.querySelector('[data-equipment-action=" + action + "]');if(!b||b.disabled)return false;b.click();return true;})()") != "true")
                throw new InvalidDataException("R28 preset action unavailable: " + action);
            await Task.Delay(200, token); await Ready(action);
        }
        async Task CaptureState(string stage)
        {
            JsonElement native = await Snapshot();
            string[] values = ScriptStringArray(await core.ExecuteScriptAsync("JSON.stringify([...document.querySelectorAll('[data-native-equipment] select')].filter(s=>s.value).map(s=>s.getAttribute('aria-label')+'='+s.value).sort())"));
            if (!values.SequenceEqual(SelectorValues(native))) throw new InvalidDataException("R28 UI/native disagreement: " + stage);
            checkpoints.Add(new { stage, slots = Slots(native), wearers = Wearers(native) });
            await Capture("r28-equipment-" + stage + ".png");
        }
        try
        {
            await core.ExecuteScriptAsync("document.querySelectorAll('.side-nav button')[3].click()");
            await WaitDom("!!document.querySelector('.squad-tabs button:nth-child(2)')", "Squads");
            await core.ExecuteScriptAsync("document.querySelector('.squad-tabs button:nth-child(2)').click()");
            await Ready("presets and live gear");
            await Task.Delay(800, token);
            if (await core.ExecuteScriptAsync("(()=>{const h=document.querySelector('.squad-header button');return !!h&&[...document.querySelectorAll('.squad-panel button')].filter(b=>b.textContent.trim()===h.textContent.trim()).length===1;})()") != "true")
                throw new InvalidDataException("R28 duplicate Refresh remains");
            var available = original.GetProperty("items").EnumerateArray().First(item => item.GetProperty("slot").GetInt32() == 2 && item.GetProperty("heroUuid").GetString() == "" && item.TryGetProperty("level", out var level) && level.ValueKind == JsonValueKind.Number);
            string gear = available.GetProperty("equipUuid").GetString()!;
            int gearLevel = available.GetProperty("level").GetInt32();
            if (await core.ExecuteScriptAsync("(()=>{const e=document.querySelector('[data-inventory-equip=\"" + gear + "\"] [data-equipment-level]');return !!e&&e.textContent.includes(" + JsonSerializer.Serialize(gearLevel.ToString()) + ");})()") != "true")
                throw new InvalidDataException("R28 inventory is missing actual native gear level");
            await CaptureState("presets-and-one-refresh");
            await core.ExecuteScriptAsync("document.querySelector('[data-inventory-equip=\"" + gear + "\"]').scrollIntoView({block:'center'})");
            await Capture("r28-equipment-inventory-levels.png");
            await core.ExecuteScriptAsync("document.querySelector('[data-equipment-presets]').scrollIntoView({block:'start'})");
            JsonElement squad = original.GetProperty("squads")[0], hero = squad.GetProperty("positions")[0];
            string label = squad.GetProperty("squadIndex").GetInt32() + ":" + hero.GetProperty("position").GetInt32() + ":2";
            string previous = hero.GetProperty("equips").EnumerateArray().Where(item => item.GetProperty("slot").GetInt32() == 2).Select(item => item.GetProperty("equipUuid").GetString()!).FirstOrDefault() ?? "";
            // Lock only this task-owned mailbox, then release it during the
            // actual tab gesture. No installed game file or owner session is touched.
            using (var mailboxLock = new FileStream(Path.Combine(lifecycle.RuntimeRoot, "feature018-request.txt"),
                FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Task edit = Set(label, gear);
                await Task.Delay(220, token);
                mailboxLock.Dispose();
                await edit;
            }
            await CaptureState("equipped-after-mailbox-lock");
            JsonElement second = squad.GetProperty("positions")[1];
            string secondLabel = squad.GetProperty("squadIndex").GetInt32() + ":" + second.GetProperty("position").GetInt32() + ":2";
            string secondPrevious = second.GetProperty("equips").EnumerateArray().Where(item => item.GetProperty("slot").GetInt32() == 2).Select(item => item.GetProperty("equipUuid").GetString()!).FirstOrDefault() ?? "";
            async Task Drag(string from, string to, string stage)
            {
                Owned();
                await core.ExecuteScriptAsync("document.querySelector('[data-equipment-slot=\"" + from + "\"]').dispatchEvent(new DragEvent('dragstart',{bubbles:true,cancelable:true,dataTransfer:new DataTransfer()}))");
                await Task.Delay(120, token);
                await core.ExecuteScriptAsync("document.querySelector('[data-equipment-slot=\"" + to + "\"]').dispatchEvent(new DragEvent('dragover',{bubbles:true,cancelable:true}))");
                await Task.Delay(120, token);
                if (await core.ExecuteScriptAsync("document.querySelector('[data-equipment-slot=\"" + to + "\"]').classList.contains('drop-target')") != "true")
                    throw new InvalidDataException("R28 drag highlight missing");
                await Capture("r28-equipment-" + stage + "-highlight.png");
                await core.ExecuteScriptAsync("document.querySelector('[data-equipment-slot=\"" + to + "\"]').dispatchEvent(new DragEvent('drop',{bubbles:true,cancelable:true}))");
                await Task.Delay(200, token); await Ready(stage); await CaptureState(stage);
            }
            await Drag(label, secondLabel, "drag-to-other-hero");
            JsonElement moved = await Snapshot();
            string firstHero = hero.GetProperty("heroUuid").GetString()!, secondHero = second.GetProperty("heroUuid").GetString()!;
            if (Slots(moved).Any(value => value.EndsWith(firstHero + ":2:" + gear)) ||
                !Slots(moved).Any(value => value.EndsWith(secondHero + ":2:" + gear)))
                throw new InvalidDataException("R28 drag did not move native gear between heroes");
            await Drag(secondLabel, label, "drag-back");
            if (secondPrevious.Length > 0) await Set(secondLabel, secondPrevious);

            string[] equippedSlots = Slots(await Snapshot()), equippedWearers = Wearers(await Snapshot());
            for (int ordinal = 1; ordinal <= 4; ordinal++)
            {
                await core.ExecuteScriptAsync("document.querySelector('[data-equipment-preset=\"" + ordinal + "\"]').click()");
                await Task.Delay(100, token); await Click("save-preset");
                JsonElement current = await Snapshot();
                if (!Slots(current).SequenceEqual(equippedSlots) || !Wearers(current).SequenceEqual(equippedWearers))
                    throw new InvalidDataException("R28 Save preset changed game gear");
                await CaptureState("preset-" + ordinal + "-saved");
            }
            JsonObject beforeRename = JsonNode.Parse(JsonSerializer.Serialize(
                await equipmentConfigService.InvokeAsync("equipment_config_get", empty.RootElement, token),
                JsonOptions.Default))!.AsObject();
            if (await core.ExecuteScriptAsync("""
                (()=>{const input=document.querySelector('[data-equipment-preset-name]');
                if(!input||input.disabled)return false;
                Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(input,'Renamed loadout');
                input.dispatchEvent(new Event('input',{bubbles:true}));return true;})()
                """) != "true")
                throw new InvalidDataException("Equipment rename input unavailable");
            await WaitDom("document.querySelector('[data-equipment-action=rename-preset]')?.disabled===false", "rename enabled");
            await Click("rename-preset");
            JsonObject afterRename = JsonNode.Parse(JsonSerializer.Serialize(
                await equipmentConfigService.InvokeAsync("equipment_config_get", empty.RootElement, token),
                JsonOptions.Default))!.AsObject();
            beforeRename["equipmentPresets"]![3]!["name"] = "Renamed loadout";
            JsonElement renamedGear = await Snapshot();
            if (!JsonNode.DeepEquals(beforeRename, afterRename) ||
                !Slots(renamedGear).SequenceEqual(equippedSlots) || !Wearers(renamedGear).SequenceEqual(equippedWearers))
                throw new InvalidDataException("Preset rename changed saved/native gear or another config field");
            await CaptureState("preset-renamed-without-capture");
            for (int ordinal = 1; ordinal <= 4; ordinal++)
            {
                await Set(label, previous);
                await core.ExecuteScriptAsync("document.querySelector('[data-equipment-preset=\"" + ordinal + "\"]').click()");
                await Task.Delay(100, token); await Click("equip-preset");
                JsonElement current = await Snapshot();
                if (!Slots(current).SequenceEqual(equippedSlots) || !Wearers(current).SequenceEqual(equippedWearers))
                    throw new InvalidDataException("R28 preset ordinal did not equip exact saved gear: " + ordinal);
                await CaptureState("preset-" + ordinal + "-equipped");
            }
            await Set(label, previous);
            JsonElement final = await Snapshot();
            exactRestore = Slots(final).SequenceEqual(originalSlots) && Wearers(final).SequenceEqual(originalWearers);
            if (!exactRestore) throw new InvalidDataException("R28 original slots/wearers not restored");
            await CaptureState("initial-state-restored");
            await Save(new { state = "proven", proof = "r28_mailbox_lock_live_drag_levels_presets_single_refresh", mailboxLockRecovered = true, dragBetweenHeroes = true,
                game = new { owner.ProfileId, owner.SessionId, owner.GamePid, uid, serverId },
                actualNativeGearLevel = gearLevel, presetSaveLeftGameUnchanged = true, allFourHotkeyOrdinalsEquippedExactSavedGear = true,
                presetRenameNameOnly = true, physicalKeyboardNotUsed = true, oneRefresh = true, exactRestore, checkpoints, finishedAtUtc = DateTimeOffset.UtcNow });
        }
        catch (Exception error)
        {
            await Save(new { state = "partial", error = error.Message, exactRestore, originalSlots, originalWearers, checkpoints });
            throw;
        }
    }
}
