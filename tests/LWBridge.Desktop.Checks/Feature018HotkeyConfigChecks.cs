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
            Require(initial.Attack && initial.Recall && initial.ShieldOverlay && initial.ShieldUse &&
                    initial.Equipment && initial.FrontlineReinforce && !initial.TreasureChestHint &&
                    !initial.AttackMarchSpeedupItem && !initial.AttackMarchSpeedupDiamond &&
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

            var oldTen = new
            {
                attack = true, attackMarchSpeedupItem = false, attackMarchSpeedupDiamond = false,
                recall = true, shieldOverlay = true, shieldUse = true, equipment = true,
                randomRelocate = false, allianceRelocate = false, frontlineReinforce = true,
            };
            HotkeyConfig migrated = (HotkeyConfig)(await reopen.InvokeAsync(
                "hotkey_config_save", JsonSerializer.SerializeToElement(oldTen),
                CancellationToken.None))!;
            Require(migrated.TreasureChestHint == false, "legacy ten-key save did not retain safe default");

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
