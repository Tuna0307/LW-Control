using System.Text.Json;
using System.Text.Json.Nodes;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class Feature018AutomationChecks
{
    internal static async Task RunAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "lwbridge-r30-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new ProfileRuntimeConfigStore(Path.Combine(root, "a.json"));
            var config = new AutomationTaskConfigCommandService(store);
            var other = new AutomationTaskConfigCommandService(new ProfileRuntimeConfigStore(Path.Combine(root, "b.json")));
            var token = CancellationToken.None;
            if (AutomationTaskConfigCommandService.Default("allianceGift")["intervalMinutes"]!.GetValue<int>() != 120)
                throw new Exception("R30 recovered gift interval changed");
            foreach (object bad in new object[] { new { enabled = true, intervalMinutes = 0 }, new { enabled = true, intervalMinutes = 1441 }, new { enabled = true, intervalMinutes = "120" } })
            {
                try { AutomationTaskConfigCommandService.Validate("allianceGift", JsonSerializer.SerializeToElement(bad));throw new Exception("R30 bad Gift interval accepted"); }
                catch (BridgeCommandException e) when (e.Code == "INVALID_REQUEST") { }
            }
            await config.InvokeAsync("feature018_automation_config_save", JsonSerializer.SerializeToElement(new { kind = "allianceHelp", config = new { enabled = true } }), token);
            var isolated = (JsonObject)(await other.InvokeAsync("feature018_automation_config_get", JsonSerializer.SerializeToElement(new { kind = "allianceHelp" }), token))!;
            if (isolated["enabled"]!.GetValue<bool>()) throw new Exception("R30 settings crossed profiles");
            foreach (object forged in new object[] { new { enabled = "true" }, new { enabled = true, authorizedUid = "999" }, new { enabled = true, intervalMinutes = 0 } })
            {
                try { AutomationTaskConfigCommandService.Validate("allianceHelp", JsonSerializer.SerializeToElement(forged));throw new Exception("R30 invalid config accepted"); }
                catch (BridgeCommandException e) when (e.Code == "INVALID_REQUEST") { }
            }
            var session = new OverviewMapScanSession("profile", "session", "challenge", 123, @"C:\inert\LastWar.exe", "2026-10-21T00:00:00Z");
            var account = new Feature018AuthenticatedGameAccount("1001", 123);
            string request = Path.Combine(root, "request.txt"), response = Path.Combine(root, "response.json");
            Dictionary<string, string>? fields = null;
            var exchange = new Feature018OwnedGameExchange(() => session, expected => expected == session, request, response,
                currentAccount: _ => account, delay: (_, _) =>
                {
                    fields = File.ReadAllLines(request).Select(l => l.Split('=', 2)).ToDictionary(a => a[0], a => a[1]);
                    File.WriteAllText(response, JsonSerializer.Serialize(new { schemaVersion = 1, requestId = fields["requestId"], command = fields["command"],
                        profileId = "profile", launchSessionId = "session", challenge = "challenge", gamePid = 123,
                        observedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), state = "confirmed", data = new { services = new { } } }));
                    return Task.CompletedTask;
                });
            var game = new Feature018GameCommandService(exchange, null, account, store);
            await game.InvokeAsync("feature018_automation_configure", JsonSerializer.SerializeToElement(new { kind = "allianceHelp" }), token);
            if (fields?["authorizedUid"] != "1001" || JsonNode.Parse(fields["confirmedAutomationConfig"])!["allianceHelp"]!["enabled"]!.GetValue<bool>() != true)
                throw new Exception("R30 game config did not come from the owned profile store");
            foreach (object forged in new object[] { new { kind = "allianceHelp", authorizedUid = "999" }, new { kind = "allianceHelp", confirmedAutomationConfig = "{}" } })
            {
                try { await game.InvokeAsync("feature018_automation_configure", JsonSerializer.SerializeToElement(forged), token);throw new Exception("R30 forged game authority accepted"); }
                catch (BridgeCommandException e) when (e.Code == "INVALID_REQUEST") { }
            }
            Console.WriteLine("FEATURE018_R30_MANAGED_CHECKS_PASS profile-isolation validated-settings trusted-wire no-browser-authority");
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
