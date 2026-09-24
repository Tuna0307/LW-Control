using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class HotkeyConfigChecks
{
    internal static async Task RunAsync()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "lwbridge-hotkeys-" + Guid.NewGuid().ToString("N"));
        string runtimeConfigPath = Path.Combine(root, "runtime", "config.json");

        try
        {
            var service = new HotkeyConfigCommandService(runtimeConfigPath);

            JsonElement defaults = await Invoke(
                service,
                "hotkey_config_get",
                new { profileId = "profile-hotkeys-test" });
            Require(defaults.GetProperty("attack").GetBoolean(), "default attack enabled");
            Require(!defaults.GetProperty("attackMarchSpeedupItem").GetBoolean(), "default item speedup disabled");
            Require(!defaults.GetProperty("attackMarchSpeedupDiamond").GetBoolean(), "default diamond speedup disabled");
            Require(defaults.GetProperty("recall").GetBoolean(), "default recall enabled");
            Require(defaults.GetProperty("shieldOverlay").GetBoolean(), "default shield overlay enabled");
            Require(defaults.GetProperty("shieldUse").GetBoolean(), "default shield use enabled");
            Require(defaults.GetProperty("equipment").GetBoolean(), "default equipment enabled");
            Require(!defaults.GetProperty("randomRelocate").GetBoolean(), "default random relocation disabled");
            Require(!defaults.GetProperty("allianceRelocate").GetBoolean(), "default alliance relocation disabled");
            Require(defaults.GetProperty("frontlineReinforce").GetBoolean(), "default frontline reinforce enabled");

            Directory.CreateDirectory(Path.GetDirectoryName(runtimeConfigPath)!);
            File.WriteAllText(
                runtimeConfigPath,
                """
                {
                  "visualMetrics": {"showFps": true, "showPing": false},
                  "futureConfig": {"keep": 7}
                }
                """);

            JsonElement saved = await Invoke(
                service,
                "hotkey_config_save",
                new
                {
                    profileId = "profile-hotkeys-test",
                    attack = false,
                    attackMarchSpeedupItem = true,
                    attackMarchSpeedupDiamond = true,
                    recall = false,
                    shieldOverlay = false,
                    shieldUse = false,
                    equipment = false,
                    randomRelocate = true,
                    allianceRelocate = true,
                    frontlineReinforce = false,
                });

            Require(!saved.GetProperty("attack").GetBoolean(), "saved attack value returned");
            Require(saved.GetProperty("randomRelocate").GetBoolean(), "saved relocation value returned");

            using (JsonDocument document = JsonDocument.Parse(File.ReadAllText(runtimeConfigPath)))
            {
                JsonElement rootElement = document.RootElement;
                Require(rootElement.GetProperty("visualMetrics").GetProperty("showFps").GetBoolean(),
                    "save preserves visual metrics sibling");
                Require(rootElement.GetProperty("futureConfig").GetProperty("keep").GetInt32() == 7,
                    "save preserves unknown top-level sibling");

                JsonElement hotkeys = rootElement.GetProperty("hotkeys");
                Require(!hotkeys.GetProperty("attack").GetBoolean(), "runtime config stores attack");
                Require(hotkeys.GetProperty("attackMarchSpeedupItem").GetBoolean(), "runtime config stores item speedup");
                Require(hotkeys.GetProperty("attackMarchSpeedupDiamond").GetBoolean(), "runtime config stores diamond speedup");
                Require(!hotkeys.GetProperty("recall").GetBoolean(), "runtime config stores recall");
                Require(!hotkeys.GetProperty("shieldOverlay").GetBoolean(), "runtime config stores shield overlay");
                Require(!hotkeys.GetProperty("shieldUse").GetBoolean(), "runtime config stores shield use");
                Require(!hotkeys.GetProperty("equipment").GetBoolean(), "runtime config stores equipment");
                Require(hotkeys.GetProperty("randomRelocate").GetBoolean(), "runtime config stores random relocation");
                Require(hotkeys.GetProperty("allianceRelocate").GetBoolean(), "runtime config stores alliance relocation");
                Require(!hotkeys.GetProperty("frontlineReinforce").GetBoolean(), "runtime config stores frontline reinforce");
            }

            var reopened = new HotkeyConfigCommandService(runtimeConfigPath);
            JsonElement persisted = await Invoke(
                reopened,
                "hotkey_config_get",
                new { profileId = "profile-hotkeys-test" });
            Require(persisted.GetProperty("attackMarchSpeedupDiamond").GetBoolean(),
                "hotkey config persists after reopen");
            Require(!persisted.GetProperty("frontlineReinforce").GetBoolean(),
                "reopened config returns latest frontline value");

            await ExpectCode(
                "INVALID_REQUEST",
                async () => await Invoke(
                    service,
                    "hotkey_config_save",
                    new
                    {
                        profileId = "profile-hotkeys-test",
                        attack = true,
                    }));

            await ExpectCode(
                "INVALID_REQUEST",
                async () => await Invoke(
                    service,
                    "hotkey_config_save",
                    new
                    {
                        profileId = "profile-hotkeys-test",
                        attack = "yes",
                        attackMarchSpeedupItem = false,
                        attackMarchSpeedupDiamond = false,
                        recall = true,
                        shieldOverlay = true,
                        shieldUse = true,
                        equipment = true,
                        randomRelocate = false,
                        allianceRelocate = false,
                        frontlineReinforce = true,
                    }));

            File.WriteAllText(runtimeConfigPath, "{");
            await ExpectCode(
                "STATE_UNAVAILABLE",
                async () => await Invoke(
                    service,
                    "hotkey_config_get",
                    new { profileId = "profile-hotkeys-test" }));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); }
            catch { }
        }
    }

    private static async Task<JsonElement> Invoke(
        HotkeyConfigCommandService service,
        string command,
        object payload)
    {
        JsonElement request = JsonSerializer.SerializeToElement(payload, JsonOptions.Default);
        object? result = await service.InvokeAsync(command, request, CancellationToken.None);
        return JsonSerializer.SerializeToElement(result, JsonOptions.Default);
    }

    private static async Task ExpectCode(string expectedCode, Func<Task> action)
    {
        try
        {
            await action();
            throw new InvalidOperationException($"Expected {expectedCode}.");
        }
        catch (BridgeCommandException error)
        {
            Require(error.Code == expectedCode,
                $"expected {expectedCode}, got {error.Code}");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
