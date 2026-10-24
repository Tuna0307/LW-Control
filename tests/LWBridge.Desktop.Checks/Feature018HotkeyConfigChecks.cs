using System.Text.Json;
using System.Text.Json.Nodes;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class Feature018HotkeyConfigChecks
{
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("FEATURE018_HOTKEY_CONFIG: " + message);
    }

    internal static async Task RunAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "lwbridge-feature018-hotkey-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "runtime-config.json");
            var commands = new HotkeyConfigCommandService(path);
            JsonElement Empty() => JsonSerializer.SerializeToElement(new { });
            HotkeyConfig initial = (HotkeyConfig)(await commands.InvokeAsync(
                "hotkey_config_get", Empty(), CancellationToken.None))!;
            Require(initial.Attack && initial.Recall && initial.ShieldUse &&
                    initial.Equipment && initial.FrontlineReinforce && !initial.TreasureChestHint &&
                    !initial.RandomRelocate && !initial.AllianceRelocate,
                "original hotkey defaults changed");

            // The 0.3.17 Mini Games chest toggle shares the original full config
            // write, but is absent in the 0.3.1 predecessor. Preserve both.
            HotkeyConfig expected = initial with { Attack = false, TreasureChestHint = true };
            HotkeyConfig written = (HotkeyConfig)(await commands.InvokeAsync(
                "hotkey_config_save", JsonSerializer.SerializeToElement(expected, JsonOptions.Default),
                CancellationToken.None))!;
            Require(written == expected, "save lost Hotkeys or Mini Games field");
            var reopen = new HotkeyConfigCommandService(path);
            HotkeyConfig reread = (HotkeyConfig)(await reopen.InvokeAsync(
                "hotkey_config_get", Empty(), CancellationToken.None))!;
            Require(reread == expected, "native persisted fields did not survive reopen");

            // R16 retired march speed-ups entirely. Existing profile files may
            // still contain both old fields, including enabled values. They
            // must deserialize as ordinary hotkey config without reintroducing
            // either preference into the current model or a subsequent save.
            var legacySaved = new
            {
                hotkeys = new
                {
                    attack = true,
                    attackMarchSpeedupItem = true,
                    attackMarchSpeedupDiamond = true,
                    recall = true,
                    shieldOverlay = true,
                    shieldUse = true,
                    equipment = true,
                    randomRelocate = false,
                    allianceRelocate = false,
                    frontlineReinforce = true,
                },
            };
            File.WriteAllText(path, JsonSerializer.Serialize(legacySaved, JsonOptions.Default));
            var legacyOpen = new HotkeyConfigCommandService(path);
            HotkeyConfig migrated = (HotkeyConfig)(await legacyOpen.InvokeAsync(
                "hotkey_config_get", Empty(), CancellationToken.None))!;
            Require(migrated.Attack && migrated.Recall && migrated.ShieldUse &&
                    migrated.Equipment && migrated.FrontlineReinforce && !migrated.RandomRelocate &&
                    !migrated.AllianceRelocate && !migrated.TreasureChestHint,
                "legacy saved hotkeys with retired speed-up fields did not load safely");
            JsonElement migratedJson = JsonSerializer.SerializeToElement(migrated, JsonOptions.Default);
            Require(!migratedJson.TryGetProperty("attackMarchSpeedupItem", out _) &&
                    !migratedJson.TryGetProperty("attackMarchSpeedupDiamond", out _) &&
                    !migratedJson.TryGetProperty("shieldOverlay", out _),
                "retired hotkey fields survived current hotkey projection");
            _ = await legacyOpen.InvokeAsync("hotkey_config_save", migratedJson, CancellationToken.None);
            using (JsonDocument persisted = JsonDocument.Parse(File.ReadAllText(path)))
            {
                JsonElement hotkeys = persisted.RootElement.GetProperty("hotkeys");
                Require(!hotkeys.TryGetProperty("attackMarchSpeedupItem", out _) &&
                        !hotkeys.TryGetProperty("attackMarchSpeedupDiamond", out _) &&
                        !hotkeys.TryGetProperty("shieldOverlay", out _),
                    "current save preserved retired hotkey fields");
            }

            bool rejected = false;
            try
            {
                await reopen.InvokeAsync("hotkey_config_save",
                    JsonSerializer.SerializeToElement(new { treasureChestHint = "not-a-boolean" }),
                    CancellationToken.None);
            }
            catch (BridgeCommandException error) when (error.Code == "INVALID_REQUEST") { rejected = true; }
            Require(rejected, "malformed whole-config save was accepted");

            var equipmentStore = new ProfileRuntimeConfigStore(path);
            var plan = JsonNode.Parse("{\"equipmentPresets\":[{\"id\":\"one\",\"name\":\"Retained plan\",\"squads\":[]}]}")!.AsObject();
            // Real Windows file handle: reads allowed, replacement denied until
            // the owner releases it. This exercises File.Move, not a fake IO.
            using (var briefLock = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Task release = Task.Run(() => { Thread.Sleep(180); briefLock.Dispose(); });
                JsonObject recovered = equipmentStore.SaveEquipmentConfig(plan);
                await release;
                Require(recovered["equipmentPresets"]!.ToJsonString() == plan["equipmentPresets"]!.ToJsonString(),
                    "brief replacement lock lost the equipment plan");
            }
            string previous = File.ReadAllText(path);
            var changedPlan = (JsonObject)plan.DeepClone();
            changedPlan["equipmentPresets"]![0]!["name"] = "Unsaved edits";
            bool busy = false;
            using (var persistentLock = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                try { equipmentStore.SaveEquipmentConfig(changedPlan); }
                catch (BridgeCommandException error) when (error.Code == "CONFIG_FILE_BUSY")
                { busy = error.Message.Contains("Your edits are still here", StringComparison.Ordinal); }
            }
            Require(busy && File.ReadAllText(path) == previous &&
                    Directory.GetFiles(root, "*.tmp-*").Length == 0,
                "persistent lock must fail plainly, keep old config intact and clean only owned temporary files");
            using (var exclusiveLock = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Task release = Task.Run(() => { Thread.Sleep(180); exclusiveLock.Dispose(); });
                equipmentStore.SaveEquipmentConfig(changedPlan);
                await release;
            }
            Require(equipmentStore.ReadEquipmentConfig()["equipmentPresets"]![0]!["name"]!.GetValue<string>() == "Unsaved edits",
                "exclusive read lock retry or later save lost the retained plan");
            var renameStore = new ProfileRuntimeConfigStore(Path.Combine(root, "rename.json"));
            renameStore.SaveEquipmentConfig(JsonNode.Parse("""
                {"equipmentPresets":[{"id":"saved-one","name":"Original","squads":[{"latestGear":"keep"}]},
                 {"id":"saved-two","name":"Other","squads":[]}],"initialEquipmentConfig":{"restore":"keep"}}
                """)!.AsObject());
            renameStore.SaveHotkeys(HotkeyConfig.Default with { Attack = false });
            string renamePath = Path.Combine(root, "rename.json");
            JsonNode beforeRename = JsonNode.Parse(File.ReadAllText(renamePath))!;
            var renameService = new EquipmentConfigCommandService(renameStore);
            using (JsonDocument renameRequest = JsonDocument.Parse("""
                {"slot":1,"expectedPresetId":"saved-one","name":"  Renamed  "}
                """))
                await renameService.InvokeAsync("equipment_preset_rename", renameRequest.RootElement, CancellationToken.None);
            beforeRename["equipmentPresets"]![0]!["name"] = "Renamed";
            Require(JsonNode.DeepEquals(beforeRename, JsonNode.Parse(File.ReadAllText(renamePath))),
                "Rename changed saved gear, another preset, restoration data or unrelated preferences");
            string afterRename = File.ReadAllText(renamePath);
            foreach (string invalid in new[] {
                "{\"slot\":0,\"expectedPresetId\":\"saved-one\",\"name\":\"Bad\"}",
                "{\"slot\":5,\"expectedPresetId\":\"saved-one\",\"name\":\"Bad\"}",
                "{\"slot\":1,\"expectedPresetId\":\"retired-id\",\"name\":\"Bad\"}",
                "{\"slot\":1,\"expectedPresetId\":\"saved-one\",\"name\":\" \"}",
                "{\"slot\":\"1\",\"expectedPresetId\":\"saved-one\",\"name\":\"Bad\"}",
                "{\"slot\":1,\"name\":\"Bad\"}",
            })
            {
                bool renameRejected = false;
                using JsonDocument invalidRename = JsonDocument.Parse(invalid);
                try { await renameService.InvokeAsync("equipment_preset_rename", invalidRename.RootElement, CancellationToken.None); }
                catch (BridgeCommandException) { renameRejected = true; }
                Require(renameRejected && File.ReadAllText(renamePath) == afterRename,
                    "Invalid or stale-ID rename modified persisted data");
            }
            Console.WriteLine("FEATURE018_EQUIPMENT_RENAME_PASS nameOnly=true staleIdAndMalformed=6");
            Console.WriteLine("FEATURE018_HOTKEY_CONFIG_PASS");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
