using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class HomeAutomationParityChecks
{
    internal static async Task RunAsync()
    {
        // Recovered 0.3.17 handler 0x12c98c-0x12d3a7 writes the effective
        // autoClosePopup=false to profile config before reporting success,
        // even if the caller requests true and there is no connected game.
        string root = Path.Combine(Path.GetTempPath(),
            "home004-local-automation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string configRoot = Path.Combine(root, "config");
            var config = new LocalConfigStore(configRoot, initialValue:
                new LWBridgeLocalConfig { ProfileId = "automation-owner" });
            var backend = new LWBridgeBackend(config: config, bridgeReadyProvider: () => false,
                applicationDataRoot: Path.Combine(root, "data"));

            JsonElement popup = JsonSerializer.SerializeToElement(
                await backend.InvokeAsync("set_automation", JsonSerializer.SerializeToElement(
                    new { profileId = "automation-owner", name = "autoClosePopup", enabled = true }),
                    CancellationToken.None), JsonOptions.Default);
            Require(popup.GetProperty("ok").GetBoolean() &&
                    popup.GetProperty("name").GetString() == "autoClosePopup" &&
                    !popup.GetProperty("enabled").GetBoolean(),
                "original effective autoClosePopup=false reply");

            using (JsonDocument saved = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(configRoot, "config.json"))))
            {
                Require(saved.RootElement.TryGetProperty("autoClosePopup", out JsonElement value) &&
                        value.ValueKind == JsonValueKind.False,
                    "original autoClosePopup effective OFF is durably persisted before acknowledgement");
            }

            JsonElement toggle = JsonSerializer.SerializeToElement(
                await backend.InvokeAsync("set_automation", JsonSerializer.SerializeToElement(
                    new { profileId = "automation-owner", name = "autoForceUpdateReload", enabled = true }),
                    CancellationToken.None), JsonOptions.Default);
            Require(toggle.GetProperty("enabled").GetBoolean() && config.Snapshot.AutoReconnect,
                "original local reconnect ON remains persisted");
            await backend.InvokeAsync("set_automation", JsonSerializer.SerializeToElement(
                new { profileId = "automation-owner", name = "autoForceUpdateReload", enabled = false }),
                CancellationToken.None);
            Require(!config.Snapshot.AutoReconnect,
                "original local reconnect OFF remains persisted");

            try
            {
                await backend.InvokeAsync("set_automation", JsonSerializer.SerializeToElement(
                    new { profileId = "automation-owner", name = "unknown", enabled = true }),
                    CancellationToken.None);
                throw new InvalidDataException("Unknown game-side automation bypassed offline gate.");
            }
            catch (BridgeCommandException error) when (error.Code == "GAME_DISCONNECTED")
            {
                // No game-side contract or protected result is synthesized.
            }
            Console.WriteLine("HOME004_H33_LOCAL_AUTOMATION_PERSISTENCE_OK no game launches");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
