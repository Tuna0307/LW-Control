using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Web.WebView2.Core;

namespace LWBridge.Desktop;

internal sealed partial class LWBridgeWindow
{
    // R26 packaged proof: exercise the production Equipment Schemes tab through
    // WebView DOM clicks only. No desktop input is used. The independent native
    // snapshots before/after make the UI action result reviewable without
    // treating a rendered success notice as game-state confirmation.
    private async Task RunFeature018R26EquipmentTabProofAsync(
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
                    "R26 Equipment proof lost the exact authenticated owned game session.");
        }
        async Task WaitDom(string expression, string stage, int polls = 300)
        {
            for (int i = 0; i < polls; i++)
            {
                Owned();
                if (await core.ExecuteScriptAsync(expression) == "true") return;
                await Task.Delay(100, token);
            }
            throw new InvalidDataException("R26 Equipment WebView did not settle: " + stage);
        }
        async Task<JsonElement> Snapshot()
        {
            Owned();
            object? result = await game.InvokeAsync("feature018_equipment_snapshot",
                JsonSerializer.SerializeToElement(new { profileId = owner.ProfileId }, JsonOptions.Default), token);
            Owned();
            return result is JsonElement json
                ? json.Clone()
                : throw new InvalidDataException("R26 Equipment native snapshot was unavailable.");
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
            throw new InvalidDataException("R26 Equipment WebView returned an invalid string-array snapshot.");
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
        if (originalSlots.Length != 0 || original.GetProperty("items").EnumerateArray().Any(item => item.GetProperty("heroUuid").GetString() != ""))
            throw new InvalidDataException("R26 expects the owner-reported fully unequipped start; no action issued.");
        var checkpoints = new List<object>();
        const string readUi = "JSON.stringify([...document.querySelectorAll('[data-native-equipment=true] select')].filter(s=>s.value).map(s=>s.getAttribute('aria-label')+'='+s.value).sort())";
        async Task Ready(string stage) => await WaitDom("(()=>{const p=document.querySelector('[data-native-equipment=true]');return !!p&&!p.hasAttribute('data-equipment-busy')&&!p.querySelector('[role=alert]')&&p.querySelector('[data-equipment-sync=synced]')!==null;})()", stage, 600);
        async Task<JsonElement> Checkpoint(string stage)
        {
            await Ready(stage);
            JsonElement native = await Snapshot();
            string[] ui = ScriptStringArray(await core.ExecuteScriptAsync(readUi));
            if (!ui.SequenceEqual(SelectorValues(native), StringComparer.Ordinal))
                throw new InvalidDataException("R26 rendered equipment differs from native state: " + stage);
            checkpoints.Add(new { stage, slots = Slots(native), wearers = Wearers(native), ui });
            await Capture("r26-equipment-" + stage + ".png");
            return native;
        }
        async Task Set(string label, string value)
        {
            Owned();
            string js = "(()=>{const s=document.querySelector('select[aria-label=" + JsonSerializer.Serialize(label) + "]');if(!s||s.disabled)return false;Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype,'value').set.call(s," + JsonSerializer.Serialize(value) + ");s.dispatchEvent(new Event('change',{bubbles:true}));return true;})()";
            if (await core.ExecuteScriptAsync(js) != "true") throw new InvalidDataException("R26 gear selector unavailable");
            await Task.Delay(200, token); await Ready("immediate edit");
        }
        async Task NativeEdit(JsonElement state, string hero, int slot, string gear)
        {
            var position = state.GetProperty("squads").EnumerateArray().SelectMany(s => s.GetProperty("positions").EnumerateArray()).First(p => p.GetProperty("heroUuid").GetString() == hero);
            string worn = position.GetProperty("equips").EnumerateArray().Where(e => e.GetProperty("slot").GetInt32() == slot).Select(e => e.GetProperty("equipUuid").GetString()!).FirstOrDefault() ?? "";
            string wearer = state.GetProperty("items").EnumerateArray().Where(e => e.GetProperty("equipUuid").GetString() == gear).Select(e => e.GetProperty("heroUuid").GetString()!).FirstOrDefault() ?? "";
            string operationId = Guid.NewGuid().ToString("N");
            object? response = await game.InvokeAsync("feature018_equipment_edit", JsonSerializer.SerializeToElement(new { operationId, heroUuid = hero, equipSlot = slot, equipUuid = gear, expectedEquipUuid = worn, expectedWearerUuid = wearer, expectedGameUid = uid, expectedServerId = serverId }), token);
            for (int attempt = 0; attempt < 200; attempt++)
            {
                if (JsonSerializer.SerializeToElement(response).GetProperty("equipmentApplied").GetBoolean()) return;
                await Task.Delay(150, token); Owned();
                response = await game.InvokeAsync("feature018_equipment_edit", JsonSerializer.SerializeToElement(new { operationId, queryOnly = true }), token);
            }
            throw new InvalidDataException("R26 external native transaction unresolved; never replay");
        }
        bool restored = false;
        try
        {
            await core.ExecuteScriptAsync("document.querySelectorAll('.side-nav button')[3].click()");
            await WaitDom("!!document.querySelector('.squad-tabs button:nth-child(2)')", "Squads");
            await core.ExecuteScriptAsync("document.querySelector('.squad-tabs button:nth-child(2)').click()");
            await Ready("live tab");
            await Task.Delay(800, token);
            if (await core.ExecuteScriptAsync("document.querySelector('.equipment-preset-rail')===null&&document.querySelector('[data-equipment-action=save]')===null&&document.querySelector('[data-equipment-action=apply]')===null&&document.querySelector('[data-equipment-action=restore]')===null") != "true")
                throw new InvalidDataException("R26 live tab still exposes plan controls");
            await Checkpoint("empty-start");
            JsonElement squad = original.GetProperty("squads")[0];
            JsonElement first = squad.GetProperty("positions")[0], second = squad.GetProperty("positions")[1];
            int index = squad.GetProperty("squadIndex").GetInt32();
            string firstKey = index + ":" + first.GetProperty("position").GetInt32(), secondKey = index + ":" + second.GetProperty("position").GetInt32();
            string firstHero = first.GetProperty("heroUuid").GetString()!, secondHero = second.GetProperty("heroUuid").GetString()!;
            string armor = original.GetProperty("items").EnumerateArray().First(item => item.GetProperty("slot").GetInt32() == 2).GetProperty("equipUuid").GetString()!;
            await Set(firstKey + ":2", armor);
            JsonElement equipped = await Checkpoint("equipped-immediately");
            if (!Slots(equipped).Any(value => value.EndsWith(firstHero + ":2:" + armor))) throw new InvalidDataException("R26 equip did not reach game");
            await Set(firstKey + ":2", "");
            if (Slots(await Checkpoint("removed-immediately")).Length != 0) throw new InvalidDataException("R26 remove did not reach game");
            await Set(firstKey + ":2", armor);
            await core.ExecuteScriptAsync("document.querySelector('[data-equipment-slot=\"" + firstKey + ":2\"]').dispatchEvent(new DragEvent('dragstart',{bubbles:true,cancelable:true,dataTransfer:new DataTransfer()}))");
            await Task.Delay(120, token);
            await core.ExecuteScriptAsync("document.querySelector('[data-equipment-slot=\"" + secondKey + ":2\"]').dispatchEvent(new DragEvent('dragover',{bubbles:true,cancelable:true}))");
            await Task.Delay(120, token);
            await Capture("r26-equipment-move-highlight.png");
            await core.ExecuteScriptAsync("document.querySelector('[data-equipment-slot=\"" + secondKey + ":2\"]').dispatchEvent(new DragEvent('drop',{bubbles:true,cancelable:true}))");
            await Task.Delay(200, token);
            JsonElement moved = await Checkpoint("moved-between-heroes");
            if (Slots(moved).Length != 1 || !Slots(moved)[0].EndsWith(secondHero + ":2:" + armor)) throw new InvalidDataException("R26 drag did not unequip source and equip destination");
            await Set(secondKey + ":2", "");
            await Checkpoint("returned-to-empty");
            // Use the same supported native edit outside the tab to prove that
            // polling reflects game-side changes without a Refresh click.
            await NativeEdit(await Snapshot(), firstHero, 2, armor);
            await WaitDom("document.querySelector('select[aria-label=\"" + firstKey + ":2\"]').value===" + JsonSerializer.Serialize(armor), "automatic game-side sync");
            await Checkpoint("external-change-auto-synced");
            await Set(firstKey + ":2", "");
            JsonElement final = await Checkpoint("initial-state-restored");
            restored = Slots(final).SequenceEqual(originalSlots) && Wearers(final).SequenceEqual(originalWearers);
            if (!restored) throw new InvalidDataException("R26 did not restore every initial slot and inventory wearer");
            await Save(new { state = "proven", proof = "r26_live_equipment_no_plan", game = new { owner.ProfileId, owner.SessionId, owner.GamePid, uid, serverId },
                startFullyUnequipped = true, equipImmediate = true, removeImmediate = true, dragMovesBetweenHeroes = true,
                automaticGameSideRefresh = true, noPlanControls = true, everyInitialSlotAndInventoryWearerRestored = restored,
                checkpoints, finishedAtUtc = DateTimeOffset.UtcNow });
        }
        catch (Exception error)
        {
            // Keep any unresolved transaction fenced for exact-owner recovery;
            // never issue a fresh edit or overwrite its intent here.
            await Save(new { state = "partial", error = error.Message, restored, originalSlots, originalWearers, checkpoints });
            throw;
        }
    }
}
