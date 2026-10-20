using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Web.WebView2.Core;

namespace LWBridge.Desktop;

internal sealed partial class LWBridgeWindow
{
    // R29 timing proof. This is reachable only from the existing explicit
    // FEATURE018 proof environment. It uses the production Equipment tab,
    // never OS input. The proof stores the full current loadout in preset 1,
    // removes every currently equipped item through the tab, then times one
    // Equip preset click until the UI/native state is synchronized again.
    // The full original slot + inventory wearer map must be restored before a
    // positive receipt can be written.
    private async Task RunFeature018R29PresetSpeedProofAsync(
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
                    "R29 preset timing lost the exact authenticated owned game session.");
        }
        async Task WaitDom(string expression, string stage, int polls = 600)
        {
            for (int i = 0; i < polls; i++)
            {
                Owned();
                if (await core.ExecuteScriptAsync(expression) == "true") return;
                await Task.Delay(100, token);
            }
            string alert = await core.ExecuteScriptAsync(
                "document.querySelector('[data-native-equipment] [role=alert]')?.textContent||''");
            throw new InvalidDataException(
                "R29 preset timing WebView did not settle: " + stage + "; tab error=" + alert);
        }
        async Task<JsonElement> Snapshot()
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                token.ThrowIfCancellationRequested();
                if (!Feature018IsApprovedAccount(uid, serverId) ||
                    !lifecycle.OwnsExactProcess(owner.GamePid) ||
                    !lifecycle.MatchesOwnedMapScanSession(owner))
                    throw new InvalidDataException("R29 timing snapshot owner session changed.");
                var account = ReadFeature018AuthenticatedAccount(lifecycle, owner);
                if (account is not null && account != approved)
                    throw new InvalidDataException("R29 timing authenticated account changed.");
                if (account == approved)
                {
                    try
                    {
                        object? result = await game.InvokeAsync("feature018_equipment_snapshot",
                            JsonSerializer.SerializeToElement(
                                new { profileId = owner.ProfileId }, JsonOptions.Default), token);
                        if (result is JsonElement json && IsOwner()) return json.Clone();
                    }
                    catch (BridgeCommandException error) when (
                        error.Code == "GAME_ACCOUNT_NOT_AUTHORIZED") { }
                }
                await Task.Delay(100, token);
            }
            throw new InvalidDataException(
                "R29 timing fresh equipment account admission stayed unavailable.");
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
                (item.TryGetProperty("heroUuid", out JsonElement hero) &&
                 hero.ValueKind == JsonValueKind.String ? hero.GetString() : ""))
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        static int PositionCount(JsonElement snapshot) => snapshot.GetProperty("squads").EnumerateArray()
            .Sum(squad => squad.GetProperty("positions").GetArrayLength());
        async Task Ready(string stage) => await WaitDom(
            "(()=>{const p=document.querySelector('[data-native-equipment=true]');" +
            "return !!p&&!p.hasAttribute('data-equipment-busy')&&!p.querySelector('[role=alert]')" +
            "&&p.querySelector('[data-equipment-sync=synced]')!==null" +
            "&&p.querySelectorAll('[data-equipment-preset]').length===4;})()", stage);
        async Task Click(string action)
        {
            Owned();
            if (await core.ExecuteScriptAsync(
                "(()=>{const b=document.querySelector('[data-equipment-action=" + action +
                "]');if(!b||b.disabled)return false;b.click();return true;})()") != "true")
                throw new InvalidDataException("R29 timing action unavailable: " + action);
            await Ready(action);
        }
        async Task Set(string label, string value)
        {
            Owned();
            if (await core.ExecuteScriptAsync(
                "(()=>{const s=document.querySelector('select[aria-label=" +
                JsonSerializer.Serialize(label) +
                "]');if(!s||s.disabled)return false;" +
                "Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype,'value').set.call(s," +
                JsonSerializer.Serialize(value) +
                ");s.dispatchEvent(new Event('change',{bubbles:true}));return true;})()") != "true")
                throw new InvalidDataException("R29 timing selector unavailable: " + label);
            await Ready("remove " + label);
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

        if (equipmentConfigService is null)
            throw new InvalidDataException("R29 timing equipment config missing");
        using var empty = JsonDocument.Parse("{}");
        object? originalConfigObject = await equipmentConfigService.InvokeAsync(
            "equipment_config_get", empty.RootElement, token);
        JsonElement originalConfig = JsonSerializer.SerializeToElement(
            originalConfigObject, JsonOptions.Default).Clone();
        JsonElement original = await Snapshot();
        string[] originalSlots = Slots(original), originalWearers = Wearers(original);
        int squadCount = original.GetProperty("squads").GetArrayLength();
        int positionCount = PositionCount(original);
        const int requestedSquadCount = 4;
        bool exactRestore = false;
        bool configRestored = false;
        var timings = new List<object>();
        Exception? proofError = null;

        async Task BestEffortRestoreGear()
        {
            try
            {
                JsonElement current = await Snapshot();
                if (Slots(current).SequenceEqual(originalSlots) &&
                    Wearers(current).SequenceEqual(originalWearers))
                {
                    exactRestore = true;
                    return;
                }
                for (int attempt = 0; attempt < 180; attempt++)
                {
                    token.ThrowIfCancellationRequested();
                    object? restored = await game.InvokeAsync("feature018_equipment_restore",
                        JsonSerializer.SerializeToElement(new
                        {
                            profileId = owner.ProfileId,
                            queryOnly = attempt > 0,
                        }, JsonOptions.Default), token);
                    JsonElement state = JsonSerializer.SerializeToElement(restored, JsonOptions.Default);
                    if (state.TryGetProperty("equipmentApplied", out JsonElement applied) &&
                        applied.ValueKind == JsonValueKind.True)
                        break;
                    await Task.Delay(250, token);
                }
                JsonElement final = await Snapshot();
                exactRestore = Slots(final).SequenceEqual(originalSlots) &&
                    Wearers(final).SequenceEqual(originalWearers);
            }
            catch { exactRestore = false; }
        }

        try
        {
            JsonObject config = JsonNode.Parse(originalConfig.GetRawText())!.AsObject();
            config["initialEquipmentConfig"] = new JsonObject
            {
                ["ownerUid"] = uid,
                ["serverId"] = serverId,
                ["squads"] = JsonNode.Parse(original.GetProperty("squads").GetRawText()),
            };
            await equipmentConfigService.InvokeAsync("equipment_config_save",
                JsonSerializer.SerializeToElement(config, JsonOptions.Default), token);

            await core.ExecuteScriptAsync("document.querySelectorAll('.side-nav button')[3].click()");
            await WaitDom("!!document.querySelector('.squad-tabs button:nth-child(2)')", "Squads");
            await core.ExecuteScriptAsync("document.querySelector('.squad-tabs button:nth-child(2)').click()");
            await Ready("Equipment Schemes");
            await core.ExecuteScriptAsync(
                "document.querySelector('[data-equipment-preset=\"1\"]')?.click()");
            await Task.Delay(100, token);
            await Click("save-preset");
            JsonElement afterSave = await Snapshot();
            if (!Slots(afterSave).SequenceEqual(originalSlots) ||
                !Wearers(afterSave).SequenceEqual(originalWearers))
                throw new InvalidDataException("R29 timing Save preset changed native gear");

            // Remove every worn item through the same production dropdowns.
            // Each ordinary edit is confirmed before the next. The timed part
            // below is only the complete preset restoration.
            var selectors = original.GetProperty("squads").EnumerateArray()
                .SelectMany(squad => squad.GetProperty("positions").EnumerateArray()
                    .SelectMany(position => position.GetProperty("equips").EnumerateArray()
                        .Select(equip => new
                        {
                            Label = $"{squad.GetProperty("squadIndex").GetInt32()}:" +
                                $"{position.GetProperty("position").GetInt32()}:" +
                                $"{equip.GetProperty("slot").GetInt32()}",
                            Uuid = equip.GetProperty("equipUuid").GetString()!,
                        })))
                .Where(item => !string.IsNullOrWhiteSpace(item.Uuid))
                .ToArray();
            if (selectors.Length < 2)
                throw new InvalidDataException(
                    "R29 timing needs at least two currently equipped items for a meaningful preset switch.");
            foreach (var selector in selectors) await Set(selector.Label, "");
            JsonElement stripped = await Snapshot();
            if (Slots(stripped).Length != 0 ||
                Wearers(stripped).Any(value => !value.EndsWith(":", StringComparison.Ordinal)))
                throw new InvalidDataException(
                    "R29 timing did not reach the expected fully unequipped preparation state.");
            await Capture("r29-preset-speed-before.png");

            var stopwatch = Stopwatch.StartNew();
            await Click("equip-preset");
            stopwatch.Stop();
            JsonElement restored = await Snapshot();
            exactRestore = Slots(restored).SequenceEqual(originalSlots) &&
                Wearers(restored).SequenceEqual(originalWearers);
            if (!exactRestore)
                throw new InvalidDataException(
                    "R29 timed preset did not restore the exact original slot/wearer state.");
            timings.Add(new
            {
                elapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
                changedSlots = selectors.Length,
                squadCount,
                positionCount,
            });
            await Capture("r29-preset-speed-after.png");
        }
        catch (Exception error)
        {
            proofError = error;
            throw;
        }
        finally
        {
            if (!exactRestore) await BestEffortRestoreGear();
            try
            {
                await equipmentConfigService.InvokeAsync("equipment_config_save",
                    originalConfig, CancellationToken.None);
                configRestored = true;
            }
            catch { configRestored = false; }
            await Save(new
            {
                state = proofError is null && exactRestore && configRestored ? "proven" : "partial",
                proof = "r29_complete_preset_speed",
                sourceBehavior = "production-equipment-tab",
                game = new { owner.ProfileId, owner.SessionId, owner.GamePid, uid, serverId },
                squadCount,
                positionCount,
                requestedSquadCount,
                fullFourSquadSwitch = squadCount == requestedSquadCount,
                timingScope = squadCount == requestedSquadCount
                    ? "full-four-squad"
                    : "all-populated-native-squads",
                changedSlots = originalSlots.Length,
                timings,
                exactRestore,
                configRestored,
                error = proofError?.Message,
                screenshots = new[] { "r29-preset-speed-before.png", "r29-preset-speed-after.png" },
                finishedAtUtc = DateTimeOffset.UtcNow,
            });
        }
    }
}
