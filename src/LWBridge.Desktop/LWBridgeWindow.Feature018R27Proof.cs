using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Web.WebView2.Core;

namespace LWBridge.Desktop;

internal sealed partial class LWBridgeWindow
{
    // R27 packaged proof: exercise the production Equipment Schemes tab through
    // WebView DOM clicks only. No desktop input is used. The independent native
    // snapshots before/after make the UI action result reviewable without
    // treating a rendered success notice as game-state confirmation.
    private async Task RunFeature018R27EquipmentTabProofAsync(
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
                    "R27 Equipment proof lost the exact authenticated owned game session.");
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
            throw new InvalidDataException("R27 Equipment WebView did not settle: " + stage + "; tab error=" + alert);
        }
        async Task<JsonElement> Snapshot()
        {
            Owned();
            object? result = await game.InvokeAsync("feature018_equipment_snapshot",
                JsonSerializer.SerializeToElement(new { profileId = owner.ProfileId }, JsonOptions.Default), token);
            Owned();
            return result is JsonElement json
                ? json.Clone()
                : throw new InvalidDataException("R27 Equipment native snapshot was unavailable.");
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
            throw new InvalidDataException("R27 Equipment WebView returned an invalid string-array snapshot.");
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
                throw new InvalidDataException("R27 selector unavailable");
            await Task.Delay(200, token); await Ready("live gear edit");
        }
        async Task Click(string action)
        {
            if (await core.ExecuteScriptAsync("(()=>{const b=document.querySelector('[data-equipment-action=" + action + "]');if(!b||b.disabled)return false;b.click();return true;})()") != "true")
                throw new InvalidDataException("R27 preset action unavailable: " + action);
            await Task.Delay(200, token); await Ready(action);
        }
        async Task CaptureState(string stage)
        {
            JsonElement native = await Snapshot();
            string[] values = ScriptStringArray(await core.ExecuteScriptAsync("JSON.stringify([...document.querySelectorAll('[data-native-equipment] select')].filter(s=>s.value).map(s=>s.getAttribute('aria-label')+'='+s.value).sort())"));
            if (!values.SequenceEqual(SelectorValues(native))) throw new InvalidDataException("R27 UI/native disagreement: " + stage);
            checkpoints.Add(new { stage, slots = Slots(native), wearers = Wearers(native) });
            await Capture("r27-equipment-" + stage + ".png");
        }
        try
        {
            await core.ExecuteScriptAsync("document.querySelectorAll('.side-nav button')[3].click()");
            await WaitDom("!!document.querySelector('.squad-tabs button:nth-child(2)')", "Squads");
            await core.ExecuteScriptAsync("document.querySelector('.squad-tabs button:nth-child(2)').click()");
            await Ready("presets and live gear");
            await Task.Delay(800, token);
            if (await core.ExecuteScriptAsync("(()=>{const h=document.querySelector('.squad-header button');return !!h&&[...document.querySelectorAll('.squad-panel button')].filter(b=>b.textContent.trim()===h.textContent.trim()).length===1;})()") != "true")
                throw new InvalidDataException("R27 duplicate Refresh remains");
            var available = original.GetProperty("items").EnumerateArray().First(item => item.GetProperty("slot").GetInt32() == 2 && item.GetProperty("heroUuid").GetString() == "" && item.TryGetProperty("level", out var level) && level.ValueKind == JsonValueKind.Number);
            string gear = available.GetProperty("equipUuid").GetString()!;
            int gearLevel = available.GetProperty("level").GetInt32();
            if (await core.ExecuteScriptAsync("(()=>{const e=document.querySelector('[data-inventory-equip=\"" + gear + "\"] [data-equipment-level]');return !!e&&e.textContent.includes(" + JsonSerializer.Serialize(gearLevel.ToString()) + ");})()") != "true")
                throw new InvalidDataException("R27 inventory is missing actual native gear level");
            await CaptureState("presets-and-one-refresh");
            await core.ExecuteScriptAsync("document.querySelector('[data-inventory-equip=\"" + gear + "\"]').scrollIntoView({block:'center'})");
            await Capture("r27-equipment-inventory-levels.png");
            await core.ExecuteScriptAsync("document.querySelector('[data-equipment-presets]').scrollIntoView({block:'start'})");
            JsonElement squad = original.GetProperty("squads")[0], hero = squad.GetProperty("positions")[0];
            string label = squad.GetProperty("squadIndex").GetInt32() + ":" + hero.GetProperty("position").GetInt32() + ":2";
            string previous = hero.GetProperty("equips").EnumerateArray().Where(item => item.GetProperty("slot").GetInt32() == 2).Select(item => item.GetProperty("equipUuid").GetString()!).FirstOrDefault() ?? "";
            await Set(label, gear);
            await CaptureState("equipped-with-level");
            string[] equippedSlots = Slots(await Snapshot()), equippedWearers = Wearers(await Snapshot());
            for (int ordinal = 1; ordinal <= 4; ordinal++)
            {
                await core.ExecuteScriptAsync("document.querySelector('[data-equipment-preset=\"" + ordinal + "\"]').click()");
                await Task.Delay(100, token); await Click("save-preset");
                JsonElement current = await Snapshot();
                if (!Slots(current).SequenceEqual(equippedSlots) || !Wearers(current).SequenceEqual(equippedWearers))
                    throw new InvalidDataException("R27 Save preset changed game gear");
                await CaptureState("preset-" + ordinal + "-saved");
            }
            for (int ordinal = 1; ordinal <= 4; ordinal++)
            {
                await Set(label, previous);
                await core.ExecuteScriptAsync("document.querySelector('[data-equipment-preset=\"" + ordinal + "\"]').click()");
                await Task.Delay(100, token); await Click("equip-preset");
                JsonElement current = await Snapshot();
                if (!Slots(current).SequenceEqual(equippedSlots) || !Wearers(current).SequenceEqual(equippedWearers))
                    throw new InvalidDataException("R27 preset ordinal did not equip exact saved gear: " + ordinal);
                await CaptureState("preset-" + ordinal + "-equipped");
            }
            await Set(label, previous);
            JsonElement final = await Snapshot();
            exactRestore = Slots(final).SequenceEqual(originalSlots) && Wearers(final).SequenceEqual(originalWearers);
            if (!exactRestore) throw new InvalidDataException("R27 original slots/wearers not restored");
            await CaptureState("initial-state-restored");
            await Save(new { state = "proven", proof = "r27_live_gear_levels_presets_alt_ordinals_single_refresh",
                game = new { owner.ProfileId, owner.SessionId, owner.GamePid, uid, serverId },
                actualNativeGearLevel = gearLevel, presetSaveLeftGameUnchanged = true, allFourHotkeyOrdinalsEquippedExactSavedGear = true,
                physicalKeyboardNotUsed = true, oneRefresh = true, exactRestore, checkpoints, finishedAtUtc = DateTimeOffset.UtcNow });
        }
        catch (Exception error)
        {
            await Save(new { state = "partial", error = error.Message, exactRestore, originalSlots, originalWearers, checkpoints });
            throw;
        }
    }
}
