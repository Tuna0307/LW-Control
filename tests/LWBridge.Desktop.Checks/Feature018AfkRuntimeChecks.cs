using System.Text.Json;
using System.Text.Json.Nodes;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class Feature018AfkRuntimeChecks
{
    internal static async Task RunAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "lwbridge-afk-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new ProfileRuntimeConfigStore(Path.Combine(root, "config.json"));
            var config = JsonNode.Parse("""
                {"enabled":true,"strategies":[],"allianceDrill":{"enabled":false,"activeRally":false,"squadIndexes":[]}}
                """)!.AsObject();
            store.SaveMonsterSweepConfig(config);
            var session = new OverviewMapScanSession("profile", "session", "challenge", 123,
                @"C:\inert\LastWar.exe", "2026-10-19T00:00:00Z");
            var account = new Feature018AuthenticatedGameAccount("1001", 123);
            string request = Path.Combine(root, "request.txt"), response = Path.Combine(root, "response.json");
            Dictionary<string, string>? observed = null;
            var exchange = new Feature018OwnedGameExchange(() => session, expected => expected == session,
                request, response, currentAccount: _ => account, delay: (_, _) =>
                {
                    observed = File.ReadAllLines(request).Select(line => line.Split('=', 2))
                        .ToDictionary(parts => parts[0], parts => parts[1]);
                    File.WriteAllText(response, JsonSerializer.Serialize(new
                    {
                        schemaVersion = 1, requestId = observed["requestId"], command = observed["command"],
                        profileId = observed["profileId"], launchSessionId = observed["launchSessionId"],
                        challenge = observed["challenge"], gamePid = 123,
                        observedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), state = "confirmed",
                        data = new { running = true, observation = "inert_native_afk_fixture" },
                    }));
                    return Task.CompletedTask;
                });
            var service = new Feature018GameCommandService(exchange, null, account, store);
            foreach (object forged in new object[]
            {
                new { taskName = "monsterSweep", targetUuid = "501" },
                new { taskName = "monsterSweep", confirmedAfkConfig = "{}" },
                new { taskName = "monsterSweep", authorizedUid = "2002" },
            })
            {
                try
                {
                    await service.InvokeAsync("automation_start", JsonSerializer.SerializeToElement(forged), CancellationToken.None);
                    throw new InvalidDataException("AFK accepted browser-owned action fields");
                }
                catch (BridgeCommandException error) when (error.Code == "INVALID_REQUEST") { }
                if (File.Exists(request)) throw new InvalidDataException("Forged AFK command wrote a mailbox");
            }
            await service.InvokeAsync("automation_start", JsonSerializer.SerializeToElement(new { taskName = "monsterSweep" }), CancellationToken.None);
            if (observed is null || observed["authorizedUid"] != account.GameUid ||
                observed["confirmedAfkConfig"] != config.ToJsonString())
                throw new InvalidDataException("AFK did not use exact authenticated account and persisted config");
            if (!service.CanHandle("feature018_afk_status") || !service.CanHandle("monster_catalog_options"))
                throw new InvalidDataException("AFK native status/catalogue not routed");
            var settings = new MonsterAfkConfigCommandService(store);
            foreach (string invalid in new[]
            {
                """{"enabled":true,"minStamina":"10","preferFifty":false}""",
                """{"enabled":true,"minStamina":-1,"preferFifty":false}""",
                """{"enabled":true,"minStamina":10,"preferFifty":false,"buyGems":true}""",
            })
            {
                try
                {
                    await settings.InvokeAsync("stamina_potion_config_save", JsonDocument.Parse(invalid).RootElement, CancellationToken.None);
                    throw new InvalidDataException("Invalid stamina settings were saved");
                }
                catch (BridgeCommandException error) when (error.Code == "INVALID_REQUEST") { }
            }
            JsonElement stamina = JsonSerializer.SerializeToElement(new { enabled = true, minStamina = 50, preferFifty = false });
            await settings.InvokeAsync("stamina_potion_config_save", stamina, CancellationToken.None);
            await service.InvokeAsync("automation_start", JsonSerializer.SerializeToElement(new { taskName = "staminaPotion" }), CancellationToken.None);
            if (observed!["confirmedStaminaConfig"] != store.ReadTasksSnapshot()["staminaPotion"]!.ToJsonString() ||
                observed.ContainsKey("confirmedAfkConfig"))
                throw new InvalidDataException("Stamina Start did not use its exact trusted task settings");
            await service.InvokeAsync("feature018_afk_configure", JsonSerializer.SerializeToElement(new { }), CancellationToken.None);
            if (!observed!.ContainsKey("confirmedAfkConfig"))
                throw new InvalidDataException("AFK Configure did not carry trusted profile settings");
            Console.WriteLine("FEATURE018_AFK_MANAGED_RUNTIME_CHECKS_OK");
        }
        finally { Directory.Delete(root, true); }
    }
}
