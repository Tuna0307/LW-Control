using System.Text.Json;
using System.Text.Json.Nodes;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class Feature018RetiredTasksChecks
{
    internal static async Task RunAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "lwbridge-r18-retired-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string config = Path.Combine(root, "config.json");
            string status = Path.Combine(root, "automation-status.json");
            File.WriteAllText(config, """
                {"tasks":{"allianceGarrison":{"enabled":true},"zombieBus":{"enabled":true},
                "monsterSweep":{"enabled":true,"allianceDrill":{"enabled":true}},
                "staminaPotion":{"enabled":true}},"hotkeys":{"frontlineReinforceCount":17}}
                """);
            var store = new ProfileRuntimeConfigStore(config);
            void Check(JsonObject tasks)
            {
                if (tasks.ContainsKey("allianceGarrison") || tasks.ContainsKey("zombieBus"))
                    throw new Exception("R18 legacy garrison settings were exposed");
            }
            Check(store.ReadTasksSnapshot());
            if (store.ReadTasksSnapshot()["monsterSweep"]?["allianceDrill"]?["enabled"]?.GetValue<bool>() != true ||
                store.ReadTasksSnapshot()["staminaPotion"]?["enabled"]?.GetValue<bool>() != true ||
                store.ReadHotkeys().FrontlineReinforceCount != 17)
                throw new Exception("R18 removed a retained setting");
            var command = new AutomationStatusCommandService(store, status);
            var response = (JsonNode)(await command.InvokeAsync("automation_status",
                JsonSerializer.SerializeToElement(new { }), CancellationToken.None))!;
            Check(response["tasks"]!.AsObject());
            File.WriteAllText(status, """{"tasks":{"allianceGarrison":{"running":true},"zombieBus":{"running":true},"monsterSweep":{"running":false}}}""");
            Check(command.CreateStatus()["tasks"]!.AsObject());
            store.SaveHotkeys(store.ReadHotkeys());
            var saved = JsonNode.Parse(File.ReadAllText(config))!;
            Check(saved["tasks"]!.AsObject());
            if (saved["tasks"]?["monsterSweep"]?["allianceDrill"]?["enabled"]?.GetValue<bool>() != true)
                throw new Exception("R18 migration discarded Alliance Drill");
        }
        finally { Directory.Delete(root, true); }
        Console.WriteLine("FEATURE018_RETIRED_TASKS_PASS");
    }
}
