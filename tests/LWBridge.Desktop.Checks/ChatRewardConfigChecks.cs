using System.Text.Json;
using System.Text.Json.Nodes;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class ChatRewardConfigChecks
{
    private static void Require(bool value, string reason)
    { if (!value) throw new InvalidOperationException("CHAT_REWARD_CONFIG: " + reason); }
    private static JsonElement Element(object value) => JsonSerializer.SerializeToElement(value);
    private static void Reject(Action action)
    {
        try { action(); throw new Exception("Invalid reward configuration accepted"); }
        catch (BridgeCommandException error) when (error.Code == "INVALID_REQUEST") { }
    }
    internal static async Task RunAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), "lw-chat-config-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var tasks = new JsonObject();
            var fresh = ChatAutomationConfigCommandService.ReadEffective(tasks, "goldEgg");
            Require(fresh["enabled"]!.GetValue<bool>() == false && fresh["legacyCombined"] is null &&
                fresh["crossServerEnabled"]!.GetValue<bool>() == false, "fresh profile is independent and off");
            tasks["redPacket"] = new JsonObject { ["enabled"] = true, ["claimDelaySeconds"] = new JsonArray(2d, 3d) };
            tasks["fireworks"] = new JsonObject { ["enabled"] = false, ["claimDelaySeconds"] = new JsonArray(5d, 6d) };
            string before = tasks.ToJsonString();
            var legacy = ChatAutomationConfigCommandService.ReadEffective(tasks, "goldEgg");
            var wire = ChatAutomationConfigCommandService.ReadEffective(tasks, "goldEgg", nativeWire: true);
            Require(legacy["legacyCombined"]!.GetValue<bool>() && legacy["enabled"]!.GetValue<bool>() &&
                !legacy["crossServerEnabled"]!.GetValue<bool>(), "migration retains old controls without new discovery");
            Require(wire.Count == 2 && wire["legacyCombined"]!.GetValue<bool>() &&
                tasks.ToJsonString() == before, "wire migration is sparse and nonmutating");
            foreach (object input in new object[] { new { enabled = true, legacyCombined = true },
                new { enabled = true, ignoreBirthday = "yes" }, new { enabled = true, crossServerEnabled = 1 },
                new { enabled = true, authorizedUid = "123" } })
                Reject(() => ChatAutomationConfigCommandService.Validate("goldEgg", Element(input)));
            string file = Path.Combine(directory, "profile.json");
            var store = new ProfileRuntimeConfigStore(file);
            var service = new ChatAutomationConfigCommandService(store);
            await service.InvokeAsync("feature018_chat_config_save", Element(new { kind = "goldEgg", config = new {
                enabled = true, ignoreBirthday = true, crossServerEnabled = true, claimDelaySeconds = new[] { 0.2, 0.8 }
            } }), CancellationToken.None);
            var saved = ChatAutomationConfigCommandService.ReadEffective(new ProfileRuntimeConfigStore(file).ReadTasksSnapshot(), "goldEgg");
            Require(saved["enabled"]!.GetValue<bool>() && saved["ignoreBirthday"]!.GetValue<bool>() &&
                saved["crossServerEnabled"]!.GetValue<bool>() && saved["legacyCombined"] is null,
                "explicit separation persists independently across reopen");
            Require(!ChatAutomationConfigCommandService.ReadEffective(new JsonObject(), "redPacket")["enabled"]!.GetValue<bool>(),
                "new Gold settings do not expand other services");
            Require(Feature018GameCommandService.ReadFields("feature018_chat_catalog", Element(new { profileId = "profile" })).Count == 0,
                "catalog carries no arbitrary command/configuration");
            foreach (object forged in new object[] { new { authorizedUid = "123" }, new { kind = "goldEgg" },
                new { command = "feature018_chat_run" }, new { confirmedChatConfig = "{}" } })
                Reject(() => Feature018GameCommandService.ReadFields("feature018_chat_catalog", Element(forged)));
            Console.WriteLine("CHAT_REWARD_CONFIG_CHECKS_PASS migration sparse-wire explicit-separation persistence typed-options catalog-schema");
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
