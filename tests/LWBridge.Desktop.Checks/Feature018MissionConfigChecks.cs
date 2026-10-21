using System.Text.Json.Nodes;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class Feature018MissionConfigChecks
{
    internal static void Run()
    {
        if (AutomationMissionConfig.Default("other") is not null) throw new Exception("Unknown mission default accepted");
        foreach (string kind in new[] { "dispatch", "dispatchAssist", "ghostRecon" })
        {
            var first = AutomationMissionConfig.Default(kind)!;
            var second = AutomationMissionConfig.Default(kind)!;
            first["enabled"] = true;
            if (second["enabled"]!.GetValue<bool>()) throw new Exception("Mission defaults share mutable state");
            var valid = AutomationMissionConfig.Validate(kind, second);
            if (valid["enabled"]!.GetValue<bool>()) throw new Exception("Recovered mission defaults must be off");
        }
        if (AutomationMissionConfig.Default("dispatch")!["delayMinutes"]!.GetValue<int>() != 3 ||
            AutomationMissionConfig.Default("dispatch")!["weeklyQualities"]!.ToJsonString() != "[\"none\",\"ur\",\"none\",\"none\",\"none\",\"ur\",\"none\"]" ||
            AutomationMissionConfig.Default("dispatchAssist")!["intervalSeconds"]!.GetValue<int>() != 30)
            throw new Exception("Recovered mission schedule changed");
        foreach (var (kind, input) in new (string, string)[]
        {
            ("dispatch", "{\"collectRewards\":true}"),
            ("ghostRecon", "{\"autoJoinAlliance\":true}"),
            ("ghostRecon", "{\"autoClaimRewards\":true}"),
            ("dispatchAssist", "{\"autoHelp\":true,\"qualities\":[\"special\"]}"),
        })
            if (!AutomationMissionConfig.Validate(kind, JsonNode.Parse(input)!.AsObject())["enabled"]!.GetValue<bool>()) throw new Exception("Independent mission switch did not activate engine");
        if (AutomationMissionConfig.Validate("ghostRecon", new() { ["enabled"] = true })["enabled"]!.GetValue<bool>())
            throw new Exception("Master enabled bypassed independent mission switches");
        foreach (var (kind, input) in new (string, string)[]
        {
            ("dispatch", "{\"delayMinutes\":\"3\"}"), ("dispatch", "{\"delayMinutes\":-1}"),
            ("dispatch", "{\"delayMinutes\":1441}"), ("dispatch", "{\"autoExecute\":1}"),
            ("dispatch", "{\"weeklyQualities\":[\"ur\"]}"),
            ("dispatch", "{\"weeklyQualities\":[\"none\",\"ur\",\"special\",\"none\",\"none\",\"ur\",\"none\"]}"),
            ("dispatchAssist", "{\"autoHelp\":true,\"qualities\":[]}"),
            ("dispatchAssist", "{\"qualities\":[\"ur\",\"ur\"]}"), ("dispatchAssist", "{\"qualities\":[\"other\"]}"),
            ("dispatchAssist", "{\"delaySeconds\":[3,2]}"), ("dispatchAssist", "{\"delaySeconds\":[0,86401]}"),
            ("dispatchAssist", "{\"delaySeconds\":[0,\"2\"]}"), ("dispatchAssist", "{\"delaySeconds\":[0,2,3]}"),
            ("dispatchAssist", "{\"intervalSeconds\":4}"), ("dispatchAssist", "{\"intervalSeconds\":301}"),
            ("ghostRecon", "{\"allianceFilter\":\"all\"}"), ("ghostRecon", "{\"autoStartOwn\":null}"),
            ("ghostRecon", "{\"authorizedUid\":\"999\"}"), ("dispatch", "{\"runNow\":true}"),
        })
        {
            try { AutomationMissionConfig.Validate(kind, JsonNode.Parse(input)!.AsObject());throw new Exception("Invalid mission configuration accepted: " + input); }
            catch (BridgeCommandException e) when (e.Code == "INVALID_REQUEST") { }
        }
        Console.WriteLine("FEATURE018_R30_MISSION_CONFIG_CHECKS_PASS defaults independent-switches types ranges unknown-fields");
    }
}
