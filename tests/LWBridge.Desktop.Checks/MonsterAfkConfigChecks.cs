using System.Text.Json;
using System.Text.Json.Nodes;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class MonsterAfkConfigChecks
{
    internal static async Task RunAsync()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "lwbridge-monster-afk-" + Guid.NewGuid().ToString("N"));
        string runtimeConfigPath = Path.Combine(root, "runtime", "config.json");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(runtimeConfigPath)!);
            File.WriteAllText(
                runtimeConfigPath,
                """
                {
                  "hotkeys": { "attack": true },
                  "visualMetrics": { "showFps": false, "showPing": false },
                  "tasks": {
                    "staminaPotion": { "enabled": false, "minStamina": 50 },
                    "allianceGift": { "enabled": false, "intervalMinutes": 120 }
                  },
                  "unknownSibling": { "keep": 7 }
                }
                """);

            var store = new ProfileRuntimeConfigStore(runtimeConfigPath);
            var service = new MonsterAfkConfigCommandService(store);
            JsonObject payload = ValidPayload();
            payload["profileId"] = "profile-monster-afk-test";
            payload["futureTopLevel"] = new JsonObject { ["keep"] = true };

            JsonObject farm = ValidFarmStrategy("farm-1");
            farm["futureStrategyField"] = new JsonObject { ["keep"] = true };
            ((JsonArray)payload["strategies"]!).Add(farm);

            JsonElement saved = await Invoke(service, payload);
            Require(saved.GetProperty("enabled").GetBoolean(), "save returns enabled");
            Require(
                saved.GetProperty("strategies").GetArrayLength() == 1,
                "save returns strategies");
            Require(
                saved.GetProperty("futureTopLevel").GetProperty("keep").GetBoolean(),
                "save result preserves unknown top-level JSON");
            Require(
                !saved.TryGetProperty("profileId", out _),
                "save result strips routing-only profileId");

            using (JsonDocument document =
                JsonDocument.Parse(File.ReadAllText(runtimeConfigPath)))
            {
                JsonElement config = document.RootElement;
                Require(
                    config.GetProperty("unknownSibling").GetProperty("keep").GetInt32() == 7,
                    "save preserves unknown config sibling");
                Require(config.TryGetProperty("hotkeys", out _), "save preserves hotkeys");
                Require(
                    config.TryGetProperty("visualMetrics", out _),
                    "save preserves visual metrics");
                JsonElement tasks = config.GetProperty("tasks");
                Require(
                    tasks.GetProperty("staminaPotion").GetProperty("minStamina").GetInt32() == 50,
                    "save preserves stamina sibling task");
                Require(
                    tasks.GetProperty("allianceGift").GetProperty("intervalMinutes").GetInt32() == 120,
                    "save preserves allianceGift sibling task");
                JsonElement monsterSweep = tasks.GetProperty("monsterSweep");
                Require(
                    !monsterSweep.TryGetProperty("profileId", out _),
                    "persisted task strips profileId");
                Require(
                    monsterSweep.GetProperty("futureTopLevel")
                        .GetProperty("keep").GetBoolean(),
                    "persisted task preserves unknown top-level field");
                Require(
                    monsterSweep.GetProperty("strategies")[0]
                        .GetProperty("futureStrategyField")
                        .GetProperty("keep").GetBoolean(),
                    "persisted strategy preserves unknown nested field");
            }

            JsonObject disabledEmpty = ValidPayload(enabled: false);
            JsonObject disabledStrategy = ValidFarmStrategy("disabled-empty");
            disabledStrategy["enabled"] = false;
            disabledStrategy["squadIndexes"] = new JsonArray();
            ((JsonArray)disabledEmpty["strategies"]!).Add(disabledStrategy);
            _ = await Invoke(service, disabledEmpty);

            JsonObject progressive = ValidPayload();
            JsonObject progressiveStrategy = ValidFarmStrategy("progressive");
            progressiveStrategy["levelFilterEnabled"] = true;
            progressiveStrategy["minLevel"] = 20;
            progressiveStrategy["maxLevel"] = 1;
            progressiveStrategy["progressiveLevels"] = true;
            ((JsonArray)progressive["strategies"]!).Add(progressiveStrategy);
            _ = await Invoke(service, progressive);

            await ExpectError(
                service,
                new JsonObject
                {
                    ["enabled"] = false,
                    ["allianceDrill"] = ValidAllianceDrill(),
                },
                "invalid monster AFK config");

            JsonObject invalidDrill = ValidPayload();
            invalidDrill["allianceDrill"] = new JsonObject();
            await ExpectError(service, invalidDrill, "invalid alliance drill config");

            JsonObject drillRequired = ValidPayload();
            ((JsonObject)drillRequired["allianceDrill"]!)["enabled"] = true;
            await ExpectError(service, drillRequired, "alliance drill squad required");

            JsonObject drillInvalidIndex = ValidPayload();
            ((JsonObject)drillInvalidIndex["allianceDrill"]!)["squadIndexes"] =
                new JsonArray(5);
            await ExpectError(service, drillInvalidIndex, "invalid squad index");

            JsonObject drillDuplicate = ValidPayload();
            ((JsonObject)drillDuplicate["allianceDrill"]!)["squadIndexes"] =
                new JsonArray(1, 1);
            await ExpectError(service, drillDuplicate, "duplicate alliance drill squad");

            JsonObject blankId = PayloadWith(ValidFarmStrategy(" "));
            await ExpectError(service, blankId, "invalid monster AFK strategy id");

            JsonObject duplicateId = ValidPayload();
            ((JsonArray)duplicateId["strategies"]!).Add(ValidFarmStrategy(" same "));
            ((JsonArray)duplicateId["strategies"]!).Add(ValidFarmStrategy("same"));
            await ExpectError(service, duplicateId, "invalid monster AFK strategy id");

            JsonObject invalidKind = PayloadWith(ValidFarmStrategy("bad-kind"));
            ((JsonObject)((JsonArray)invalidKind["strategies"]!)[0]!)["kind"] = "other";
            await ExpectError(service, invalidKind, "invalid monster AFK strategy kind");

            JsonObject invalidAction = PayloadWith(ValidFarmStrategy("bad-action"));
            ((JsonObject)((JsonArray)invalidAction["strategies"]!)[0]!)["attackEnabled"] = false;
            await ExpectError(service, invalidAction, "invalid monster AFK strategy action");

            JsonObject invalidJoin = PayloadWith(ValidJoinStrategy("bad-join"));
            ((JsonObject)((JsonArray)invalidJoin["strategies"]!)[0]!).Remove("rally");
            await ExpectError(service, invalidJoin, "invalid monster AFK strategy action");

            JsonObject invalidLimit = PayloadWith(ValidFarmStrategy("bad-limit"));
            ((JsonObject)((JsonArray)invalidLimit["strategies"]!)[0]!)["executionLimit"] = -1;
            await ExpectError(service, invalidLimit, "invalid monster AFK execution limit");

            JsonObject invalidLevel = PayloadWith(ValidFarmStrategy("bad-level"));
            JsonObject invalidLevelStrategy =
                (JsonObject)((JsonArray)invalidLevel["strategies"]!)[0]!;
            invalidLevelStrategy["levelFilterEnabled"] = true;
            invalidLevelStrategy["minLevel"] = 20;
            invalidLevelStrategy["maxLevel"] = 10;
            invalidLevelStrategy["progressiveLevels"] = false;
            await ExpectError(service, invalidLevel, "invalid monster AFK level range");

            JsonObject invalidState = PayloadWith(ValidFarmStrategy("bad-state"));
            ((JsonObject)((JsonArray)invalidState["strategies"]!)[0]!).Remove("enabled");
            await ExpectError(service, invalidState, "invalid monster AFK strategy state");

            JsonObject squadsRequired = PayloadWith(ValidFarmStrategy("no-squad"));
            ((JsonObject)((JsonArray)squadsRequired["strategies"]!)[0]!)["squadIndexes"] =
                new JsonArray();
            await ExpectError(service, squadsRequired, "monster AFK squad required");

            JsonObject duplicateSquad = PayloadWith(ValidFarmStrategy("dup-squad"));
            ((JsonObject)((JsonArray)duplicateSquad["strategies"]!)[0]!)["squadIndexes"] =
                new JsonArray(1, 1);
            await ExpectError(service, duplicateSquad, "duplicate monster AFK squad");

            JsonObject invalidSquad = PayloadWith(ValidFarmStrategy("bad-squad"));
            ((JsonObject)((JsonArray)invalidSquad["strategies"]!)[0]!)["squadIndexes"] =
                new JsonArray(0);
            await ExpectError(service, invalidSquad, "invalid squad index");

            var backend = new LWBridgeBackend(
                new LocalConfigStore(persistent: false),
                asyncCommands: service,
                runtimeTasksProvider: store.ReadTasksSnapshot);
            JsonObject backendPayload = ValidPayload();
            backendPayload["profileId"] = backend.ProfileId;
            JsonElement backendResult = JsonSerializer.SerializeToElement(
                await backend.InvokeAsync(
                    "monster_afk_config_save",
                    JsonSerializer.SerializeToElement(
                        backendPayload,
                        JsonOptions.Default),
                    CancellationToken.None),
                JsonOptions.Default);
            Require(
                backendResult.TryGetProperty("strategies", out _),
                "production backend routes monster_afk_config_save");

            JsonElement status = JsonSerializer.SerializeToElement(
                await backend.InvokeAsync(
                    "get_status",
                    JsonSerializer.SerializeToElement(
                        new { profileId = backend.ProfileId },
                        JsonOptions.Default),
                    CancellationToken.None),
                JsonOptions.Default);
            Require(
                status.GetProperty("config")
                    .GetProperty("tasks")
                    .GetProperty("monsterSweep")
                    .GetProperty("enabled")
                    .GetBoolean(),
                "get_status reloads persisted monsterSweep config");

            await ExpectBackendCode(
                backend,
                "monster_afk_start",
                new { profileId = backend.ProfileId },
                "COMMAND_NOT_IMPLEMENTED");
            await ExpectBackendCode(
                backend,
                "monster_afk_stop",
                new { profileId = backend.ProfileId },
                "COMMAND_NOT_IMPLEMENTED");

            File.WriteAllText(runtimeConfigPath, "{");
            await ExpectStateError(service);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); }
            catch { }
        }
    }

    private static JsonObject ValidPayload(bool enabled = true) =>
        new()
        {
            ["enabled"] = enabled,
            ["strategies"] = new JsonArray(),
            ["allianceDrill"] = ValidAllianceDrill(),
        };

    private static JsonObject ValidAllianceDrill() =>
        new()
        {
            ["enabled"] = false,
            ["activeRally"] = false,
            ["squadIndexes"] = new JsonArray(),
        };

    private static JsonObject ValidFarmStrategy(string id) =>
        new()
        {
            ["id"] = id,
            ["kind"] = "farm",
            ["attackEnabled"] = true,
            ["joinEnabled"] = false,
            ["rally"] = false,
            ["executionLimit"] = 0,
            ["levelFilterEnabled"] = false,
            ["minLevel"] = 1,
            ["maxLevel"] = 999,
            ["progressiveLevels"] = false,
            ["enabled"] = true,
            ["squadIndexes"] = new JsonArray(1),
        };

    private static JsonObject ValidJoinStrategy(string id) =>
        new()
        {
            ["id"] = id,
            ["kind"] = "join",
            ["attackEnabled"] = false,
            ["joinEnabled"] = true,
            ["rally"] = true,
            ["executionLimit"] = 0,
            ["levelFilterEnabled"] = false,
            ["minLevel"] = 1,
            ["maxLevel"] = 999,
            ["progressiveLevels"] = false,
            ["enabled"] = true,
            ["squadIndexes"] = new JsonArray(1),
        };

    private static JsonObject PayloadWith(JsonObject strategy)
    {
        JsonObject payload = ValidPayload();
        ((JsonArray)payload["strategies"]!).Add(strategy);
        return payload;
    }

    private static async Task<JsonElement> Invoke(
        MonsterAfkConfigCommandService service,
        JsonObject payload)
    {
        object? result = await service.InvokeAsync(
            "monster_afk_config_save",
            JsonSerializer.SerializeToElement(payload, JsonOptions.Default),
            CancellationToken.None);
        return JsonSerializer.SerializeToElement(result, JsonOptions.Default);
    }

    private static async Task ExpectError(
        MonsterAfkConfigCommandService service,
        JsonObject payload,
        string expectedMessage)
    {
        try
        {
            _ = await Invoke(service, payload);
            throw new InvalidOperationException(
                $"Expected INVALID_REQUEST / {expectedMessage}.");
        }
        catch (BridgeCommandException error)
        {
            Require(error.Code == "INVALID_REQUEST", $"unexpected code {error.Code}");
            Require(
                error.Message == expectedMessage,
                $"expected '{expectedMessage}', got '{error.Message}'");
        }
    }

    private static async Task ExpectStateError(
        MonsterAfkConfigCommandService service)
    {
        try
        {
            _ = await Invoke(service, ValidPayload());
            throw new InvalidOperationException("Expected STATE_UNAVAILABLE.");
        }
        catch (BridgeCommandException error)
        {
            Require(error.Code == "STATE_UNAVAILABLE", "state error code");
            Require(
                error.Message == "config state is unavailable",
                "state error message");
        }
    }

    private static async Task ExpectBackendCode(
        LWBridgeBackend backend,
        string command,
        object payload,
        string expectedCode)
    {
        try
        {
            _ = await backend.InvokeAsync(
                command,
                JsonSerializer.SerializeToElement(payload, JsonOptions.Default),
                CancellationToken.None);
            throw new InvalidOperationException(
                $"Expected {command} to fail with {expectedCode}.");
        }
        catch (BridgeCommandException error)
        {
            Require(
                error.Code == expectedCode,
                $"{command} expected {expectedCode}, got {error.Code}");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
