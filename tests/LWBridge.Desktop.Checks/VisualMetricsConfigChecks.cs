using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class VisualMetricsConfigChecks
{
    internal static async Task RunAsync()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "lwbridge-visual-metrics-" + Guid.NewGuid().ToString("N"));
        string runtimeConfigPath = Path.Combine(root, "runtime", "config.json");

        try
        {
            var visual = new VisualMetricsConfigCommandService(runtimeConfigPath);
            var hotkeys = new HotkeyConfigCommandService(runtimeConfigPath);

            JsonElement defaults = await Invoke(
                visual,
                "visual_metrics_config_get",
                new { profileId = "profile-visual-test" });
            Require(!defaults.GetProperty("showFps").GetBoolean(),
                "default showFps disabled");
            Require(!defaults.GetProperty("showPing").GetBoolean(),
                "default showPing disabled");

            _ = await Invoke(
                hotkeys,
                "hotkey_config_save",
                new
                {
                    profileId = "profile-visual-test",
                    attack = true,
                    attackMarchSpeedupItem = false,
                    attackMarchSpeedupDiamond = false,
                    recall = true,
                    shieldOverlay = true,
                    shieldUse = true,
                    equipment = true,
                    randomRelocate = false,
                    allianceRelocate = false,
                    frontlineReinforce = true,
                });

            JsonElement saved = await Invoke(
                visual,
                "visual_metrics_config_save",
                new
                {
                    profileId = "profile-visual-test",
                    showFps = true,
                    showPing = false,
                });
            Require(saved.GetProperty("showFps").GetBoolean(),
                "save returns showFps");
            Require(!saved.GetProperty("showPing").GetBoolean(),
                "save returns showPing");

            using (JsonDocument document =
                JsonDocument.Parse(File.ReadAllText(runtimeConfigPath)))
            {
                JsonElement rootElement = document.RootElement;
                Require(rootElement.GetProperty("hotkeys")
                    .GetProperty("frontlineReinforce").GetBoolean(),
                    "visual save preserves Hotkey sibling");
                Require(rootElement.GetProperty("visualMetrics")
                    .GetProperty("showFps").GetBoolean(),
                    "visual save persists showFps");
                Require(!rootElement.GetProperty("visualMetrics")
                    .GetProperty("showPing").GetBoolean(),
                    "visual save persists showPing");
            }

            _ = await Invoke(
                hotkeys,
                "hotkey_config_save",
                new
                {
                    profileId = "profile-visual-test",
                    attack = false,
                    attackMarchSpeedupItem = true,
                    attackMarchSpeedupDiamond = false,
                    recall = true,
                    shieldOverlay = true,
                    shieldUse = true,
                    equipment = true,
                    randomRelocate = false,
                    allianceRelocate = false,
                    frontlineReinforce = true,
                });

            var reopened = new VisualMetricsConfigCommandService(runtimeConfigPath);
            JsonElement persisted = await Invoke(
                reopened,
                "visual_metrics_config_get",
                new { profileId = "profile-visual-test" });
            Require(persisted.GetProperty("showFps").GetBoolean(),
                "Hotkey save preserves visual metrics sibling");
            Require(!persisted.GetProperty("showPing").GetBoolean(),
                "reopened visual metrics returns latest showPing");

            await ExpectCode(
                "INVALID_REQUEST",
                async () => await Invoke(
                    visual,
                    "visual_metrics_config_save",
                    new
                    {
                        profileId = "profile-visual-test",
                        showFps = true,
                    }));
            await ExpectCode(
                "INVALID_REQUEST",
                async () => await Invoke(
                    visual,
                    "visual_metrics_config_save",
                    new
                    {
                        profileId = "profile-visual-test",
                        showFps = "yes",
                        showPing = false,
                    }));

            File.WriteAllText(runtimeConfigPath, "{");
            await ExpectCode(
                "STATE_UNAVAILABLE",
                async () => await Invoke(
                    visual,
                    "visual_metrics_config_get",
                    new { profileId = "profile-visual-test" }));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); }
            catch { }
        }
    }

    private static async Task<JsonElement> Invoke(
        VisualMetricsConfigCommandService service,
        string command,
        object payload)
    {
        JsonElement request =
            JsonSerializer.SerializeToElement(payload, JsonOptions.Default);
        object? result =
            await service.InvokeAsync(command, request, CancellationToken.None);
        return JsonSerializer.SerializeToElement(result, JsonOptions.Default);
    }

    private static async Task<JsonElement> Invoke(
        HotkeyConfigCommandService service,
        string command,
        object payload)
    {
        JsonElement request =
            JsonSerializer.SerializeToElement(payload, JsonOptions.Default);
        object? result =
            await service.InvokeAsync(command, request, CancellationToken.None);
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
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
