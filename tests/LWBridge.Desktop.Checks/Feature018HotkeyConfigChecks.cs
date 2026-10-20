using System.Text.Json;
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
            Console.WriteLine("FEATURE018_HOTKEY_CONFIG_PASS");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
