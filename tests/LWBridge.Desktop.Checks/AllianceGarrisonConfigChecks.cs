using System.Text.Json;
using System.Text.Json.Nodes;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class AllianceGarrisonConfigChecks
{
    internal static async Task RunAsync()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "lwbridge-alliance-garrison-" + Guid.NewGuid().ToString("N"));
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
                    "monsterSweep": { "enabled": false, "strategies": [] },
                    "allianceGift": { "enabled": false, "intervalMinutes": 120 }
                  },
                  "unknownSibling": { "keep": 7 }
                }
                """);

            var store = new ProfileRuntimeConfigStore(runtimeConfigPath);
            var service = new AllianceGarrisonConfigCommandService(store);

            JsonObject payload = ValidPayload();
            payload["profileId"] = "profile-garrison-test";
            payload["futureTopLevel"] = new JsonObject { ["keep"] = true };
            ((JsonObject)((JsonArray)payload["targets"]!)[0]!)["futureTargetField"] =
                new JsonObject { ["keep"] = true };

            JsonElement saved = await Invoke(service, payload);
            Require(saved.GetProperty("enabled").GetBoolean(),
                "save returns enabled");
            Require(saved.GetProperty("recallOnDisable").GetBoolean(),
                "save returns recallOnDisable");
            Require(saved.GetProperty("squadPriority").GetArrayLength() == 2,
                "save returns squad priority");
            Require(saved.GetProperty("targets").GetArrayLength() == 2,
                "save returns targets");
            Require(!saved.TryGetProperty("profileId", out _),
                "save strips routing-only profileId");
            Require(saved.GetProperty("futureTopLevel")
                .GetProperty("keep").GetBoolean(),
                "save preserves unknown top-level task field");

            using (JsonDocument document =
                JsonDocument.Parse(File.ReadAllText(runtimeConfigPath)))
            {
                JsonElement config = document.RootElement;
                Require(config.TryGetProperty("hotkeys", out _),
                    "save preserves hotkeys");
                Require(config.TryGetProperty("visualMetrics", out _),
                    "save preserves visual metrics");
                Require(config.GetProperty("unknownSibling")
                    .GetProperty("keep").GetInt32() == 7,
                    "save preserves unknown root sibling");

                JsonElement tasks = config.GetProperty("tasks");
                Require(tasks.TryGetProperty("monsterSweep", out _),
                    "save preserves monsterSweep sibling");
                Require(tasks.GetProperty("allianceGift")
                    .GetProperty("intervalMinutes").GetInt32() == 120,
                    "save preserves sibling task");
                JsonElement garrison = tasks.GetProperty("allianceGarrison");
                Require(!garrison.TryGetProperty("profileId", out _),
                    "persisted task strips profileId");
                Require(garrison.GetProperty("futureTopLevel")
                    .GetProperty("keep").GetBoolean(),
                    "persisted task preserves unknown top-level field");
                Require(garrison.GetProperty("targets")[0]
                    .GetProperty("futureTargetField")
                    .GetProperty("keep").GetBoolean(),
                    "persisted target preserves unknown nested field");
            }

            JsonObject disabledEmpty = new()
            {
                ["enabled"] = false,
                ["recallOnDisable"] = true,
                ["squadPriority"] = new JsonArray(),
                ["targets"] = new JsonArray(),
            };
            _ = await Invoke(service, disabledEmpty);

            JsonObject missingOptionalBooleans = new()
            {
                ["squadPriority"] = new JsonArray(),
                ["targets"] = new JsonArray(),
            };
            _ = await Invoke(service, missingOptionalBooleans);

            JsonObject badEnabled = ValidPayload();
            badEnabled["enabled"] = "yes";
            await ExpectError(
                service,
                badEnabled,
                "INVALID_REQUEST",
                "alliance garrison enabled must be boolean");

            JsonObject badRecall = ValidPayload();
            badRecall["recallOnDisable"] = 1;
            await ExpectError(
                service,
                badRecall,
                "INVALID_REQUEST",
                "alliance garrison recall on disable must be boolean");

            JsonObject missingSquads = ValidPayload();
            missingSquads.Remove("squadPriority");
            await ExpectError(
                service,
                missingSquads,
                "INVALID_REQUEST",
                "alliance garrison squad priority must be an array");

            JsonObject nonIntegerSquad = ValidPayload();
            nonIntegerSquad["squadPriority"] = new JsonArray(1.5);
            await ExpectError(
                service,
                nonIntegerSquad,
                "INVALID_REQUEST",
                "alliance garrison squad priority must contain integers");

            JsonObject invalidSquad = ValidPayload();
            invalidSquad["squadPriority"] = new JsonArray(0);
            await ExpectError(
                service,
                invalidSquad,
                "INVALID_REQUEST",
                "invalid squad index");

            JsonObject duplicateSquad = ValidPayload();
            duplicateSquad["squadPriority"] = new JsonArray(1, 1);
            await ExpectError(
                service,
                duplicateSquad,
                "INVALID_REQUEST",
                "alliance garrison squad priority contains duplicates");

            JsonObject missingTargets = ValidPayload();
            missingTargets.Remove("targets");
            await ExpectError(
                service,
                missingTargets,
                "INVALID_REQUEST",
                "alliance garrison targets must be an array");

            JsonObject targetNotObject = ValidPayload();
            targetNotObject["targets"] = new JsonArray("bad");
            await ExpectError(
                service,
                targetNotObject,
                "INVALID_REQUEST",
                "alliance garrison target must be an object");

            JsonObject invalidKind = ValidPayload();
            ((JsonObject)((JsonArray)invalidKind["targets"]!)[0]!)["kind"] = "other";
            await ExpectError(
                service,
                invalidKind,
                "INVALID_REQUEST",
                "invalid alliance garrison target kind");

            JsonObject badBuildingId = ValidPayload();
            ((JsonObject)((JsonArray)badBuildingId["targets"]!)[0]!)["buildId"] = 0;
            await ExpectError(
                service,
                badBuildingId,
                "INVALID_REQUEST",
                "alliance garrison building id must be positive");

            JsonObject blankUid = ValidPayload();
            JsonObject ally = (JsonObject)((JsonArray)blankUid["targets"]!)[1]!;
            ally["uid"] = " \t\r\n ";
            await ExpectError(
                service,
                blankUid,
                "INVALID_REQUEST",
                "alliance garrison ally uid is required");

            JsonObject badSnapshot = ValidPayload();
            JsonObject badSnapshotTarget =
                (JsonObject)((JsonArray)badSnapshot["targets"]!)[1]!;
            badSnapshotTarget["uuidSnapshot"] = "12x34";
            await ExpectError(
                service,
                badSnapshot,
                "INVALID_REQUEST",
                "alliance garrison city snapshot is invalid");

            JsonObject missingSnapshotPoint = ValidPayload();
            JsonObject missingPointTarget =
                (JsonObject)((JsonArray)missingSnapshotPoint["targets"]!)[1]!;
            missingPointTarget.Remove("pointIdSnapshot");
            await ExpectError(
                service,
                missingSnapshotPoint,
                "INVALID_REQUEST",
                "alliance garrison city snapshot is invalid");

            JsonObject blankSnapshotAllowed = ValidPayload();
            JsonObject blankSnapshotTarget =
                (JsonObject)((JsonArray)blankSnapshotAllowed["targets"]!)[1]!;
            blankSnapshotTarget["uuidSnapshot"] = "   ";
            blankSnapshotTarget.Remove("serverIdSnapshot");
            blankSnapshotTarget.Remove("pointIdSnapshot");
            _ = await Invoke(service, blankSnapshotAllowed);

            JsonObject longName = ValidPayload();
            ((JsonObject)((JsonArray)longName["targets"]!)[0]!)["nameSnapshot"] =
                new string('x', 101);
            await ExpectError(
                service,
                longName,
                "INVALID_REQUEST",
                "alliance garrison target name is too long");

            JsonObject duplicateBuildings = ValidPayload();
            duplicateBuildings["targets"] = new JsonArray(
                BuildingTarget(77),
                BuildingTarget(77));
            await ExpectError(
                service,
                duplicateBuildings,
                "INVALID_REQUEST",
                "alliance garrison targets contain duplicates");

            JsonObject duplicateAllies = ValidPayload();
            duplicateAllies["targets"] = new JsonArray(
                AllyTarget(" ally-1 "),
                AllyTarget("ally-1"));
            await ExpectError(
                service,
                duplicateAllies,
                "INVALID_REQUEST",
                "alliance garrison targets contain duplicates");

            JsonObject enabledNoTarget = ValidPayload();
            enabledNoTarget["targets"] = new JsonArray();
            await ExpectError(
                service,
                enabledNoTarget,
                "INVALID_REQUEST",
                "alliance garrison requires at least one target and squad");

            JsonObject enabledNoSquad = ValidPayload();
            enabledNoSquad["squadPriority"] = new JsonArray();
            await ExpectError(
                service,
                enabledNoSquad,
                "INVALID_REQUEST",
                "alliance garrison requires at least one target and squad");

            var backend = new LWBridgeBackend(
                new LocalConfigStore(persistent: false),
                asyncCommands: service,
                runtimeTasksProvider: store.ReadTasksSnapshot);
            JsonObject backendPayload = ValidPayload();
            backendPayload["profileId"] = backend.ProfileId;
            JsonElement backendResult = JsonSerializer.SerializeToElement(
                await backend.InvokeAsync(
                    "alliance_garrison_config_save",
                    JsonSerializer.SerializeToElement(
                        backendPayload,
                        JsonOptions.Default),
                    CancellationToken.None),
                JsonOptions.Default);
            Require(backendResult.GetProperty("enabled").GetBoolean(),
                "production backend routes alliance_garrison_config_save");

            JsonElement status = JsonSerializer.SerializeToElement(
                await backend.InvokeAsync(
                    "get_status",
                    JsonSerializer.SerializeToElement(
                        new { profileId = backend.ProfileId },
                        JsonOptions.Default),
                    CancellationToken.None),
                JsonOptions.Default);
            JsonElement statusConfig = status.GetProperty("config")
                .GetProperty("tasks")
                .GetProperty("allianceGarrison");
            Require(statusConfig.GetProperty("enabled").GetBoolean(),
                "get_status reloads persisted allianceGarrison config");
            Require(statusConfig.GetProperty("recallOnDisable").GetBoolean(),
                "status preserves recallOnDisable");

            File.WriteAllText(runtimeConfigPath, "{");
            await ExpectStateError(service);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); }
            catch { }
        }
    }

    private static JsonObject ValidPayload() => new()
    {
        ["enabled"] = true,
        ["recallOnDisable"] = true,
        ["squadPriority"] = new JsonArray(1, 3),
        ["targets"] = new JsonArray(
            BuildingTarget(77),
            new JsonObject
            {
                ["kind"] = "allyCity",
                ["uid"] = "ally-9",
                ["nameSnapshot"] = "Friendly City",
                ["uuidSnapshot"] = "1234567890123",
                ["uuidUpdatedAt"] = 123456,
                ["serverIdSnapshot"] = 18,
                ["pointIdSnapshot"] = 456,
            }),
    };

    private static JsonObject BuildingTarget(long buildId) => new()
    {
        ["kind"] = "allianceBuilding",
        ["buildId"] = buildId,
        ["nameSnapshot"] = "Alliance Center",
    };

    private static JsonObject AllyTarget(string uid) => new()
    {
        ["kind"] = "allyCity",
        ["uid"] = uid,
        ["nameSnapshot"] = "Ally",
    };

    private static async Task<JsonElement> Invoke(
        AllianceGarrisonConfigCommandService service,
        JsonObject payload)
    {
        object? result = await service.InvokeAsync(
            "alliance_garrison_config_save",
            JsonSerializer.SerializeToElement(payload, JsonOptions.Default),
            CancellationToken.None);
        return JsonSerializer.SerializeToElement(result, JsonOptions.Default);
    }

    private static async Task ExpectError(
        AllianceGarrisonConfigCommandService service,
        JsonObject payload,
        string expectedCode,
        string expectedMessage)
    {
        try
        {
            _ = await Invoke(service, payload);
            throw new InvalidOperationException(
                $"Expected alliance_garrison_config_save to fail with {expectedCode}.");
        }
        catch (BridgeCommandException error)
        {
            Require(error.Code == expectedCode,
                $"expected {expectedCode}, got {error.Code}");
            Require(error.Message == expectedMessage,
                $"expected '{expectedMessage}', got '{error.Message}'");
        }
    }

    private static async Task ExpectStateError(
        AllianceGarrisonConfigCommandService service)
    {
        try
        {
            _ = await Invoke(service, ValidPayload());
            throw new InvalidOperationException(
                "Expected malformed runtime config to fail.");
        }
        catch (BridgeCommandException error)
        {
            Require(error.Code == "STATE_UNAVAILABLE",
                $"expected STATE_UNAVAILABLE, got {error.Code}");
            Require(error.Message == "config state is unavailable",
                "malformed runtime config uses native state-unavailable message");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
