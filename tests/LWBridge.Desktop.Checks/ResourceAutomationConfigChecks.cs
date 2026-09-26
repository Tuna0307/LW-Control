using System.Text.Json;
using System.Text.Json.Nodes;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class ResourceAutomationConfigChecks
{
    private const long Now = 1_700_000_000_000L;

    internal static async Task RunAsync()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "lwbridge-resource-automation-" + Guid.NewGuid().ToString("N"));
        string runtimeDirectory = Path.Combine(root, "runtime");
        string runtimeConfigPath = Path.Combine(runtimeDirectory, "config.json");
        string automationStatusPath =
            Path.Combine(runtimeDirectory, "automation-status.json");

        try
        {
            Directory.CreateDirectory(runtimeDirectory);
            File.WriteAllText(
                runtimeConfigPath,
                """
                {
                  "hotkeys": { "attack": true },
                  "visualMetrics": { "showFps": false, "showPing": false },
                  "tasks": {
                    "monsterSweep": { "enabled": false, "strategies": [] },
                    "buildingResources": {
                      "enabled": false,
                      "intervalMinutes": 60
                    },
                    "armedTruckReward": {
                      "enabled": false,
                      "intervalMinutes": 60
                    }
                  },
                  "futureRoot": { "keep": 9 }
                }
                """);

            var store = new ProfileRuntimeConfigStore(runtimeConfigPath);
            var service = new ResourceAutomationConfigCommandService(
                store,
                automationStatusPath,
                () => Now);

            JsonElement initial = await Invoke(
                service,
                "resource_automation_status",
                new { profileId = "profile-resource-test" });

            Require(initial.GetProperty("updatedAt").GetInt64() == Now,
                "status updatedAt uses native wall-clock projection");
            Require(initial.GetProperty("runningTask").ValueKind ==
                    JsonValueKind.Null,
                "missing native status has null runningTask");

            JsonElement initialTasks = initial.GetProperty("tasks");
            Require(initialTasks.EnumerateObject()
                    .Select(entry => entry.Name)
                    .SequenceEqual(
                    ResourceAutomationConfigCommandService.TaskNames),
                "status exposes exactly the two native resource tasks");

            AssertIdleTask(
                initialTasks.GetProperty("buildingResources"),
                "buildingResources",
                enabled: false,
                intervalMinutes: 60,
                nextRunAt: null);
            AssertIdleTask(
                initialTasks.GetProperty("armedTruckReward"),
                "armedTruckReward",
                enabled: false,
                intervalMinutes: 60,
                nextRunAt: null);

            object? published = null;
            service.StatusChanged += value => published = value;

            JsonElement configured = await Invoke(
                service,
                "resource_automation_configure",
                new
                {
                    profileId = "profile-resource-test",
                    task = "buildingResources",
                    config = new
                    {
                        enabled = true,
                        intervalMinutes = 30,
                        ignoredFutureField = "not persisted",
                    },
                });

            AssertIdleTask(
                configured,
                "buildingResources",
                enabled: true,
                intervalMinutes: 30,
                nextRunAt: Now);
            Require(published is not null,
                "configure emits resource-automation status event");

            JsonElement eventStatus =
                JsonSerializer.SerializeToElement(
                    published,
                    JsonOptions.Default);
            AssertIdleTask(
                eventStatus.GetProperty("tasks")
                    .GetProperty("buildingResources"),
                "buildingResources",
                enabled: true,
                intervalMinutes: 30,
                nextRunAt: Now);

            using (JsonDocument document =
                JsonDocument.Parse(File.ReadAllText(runtimeConfigPath)))
            {
                JsonElement config = document.RootElement;
                Require(config.GetProperty("hotkeys")
                    .GetProperty("attack").GetBoolean(),
                    "configure preserves hotkeys sibling");
                Require(!config.GetProperty("visualMetrics")
                    .GetProperty("showFps").GetBoolean(),
                    "configure preserves visual metrics sibling");
                Require(config.GetProperty("futureRoot")
                    .GetProperty("keep").GetInt32() == 9,
                    "configure preserves unknown root sibling");

                JsonElement tasks = config.GetProperty("tasks");
                Require(tasks.TryGetProperty("monsterSweep", out _),
                    "configure preserves sibling task");
                JsonElement saved =
                    tasks.GetProperty("buildingResources");
                Require(saved.GetProperty("enabled").GetBoolean(),
                    "configure persists enabled");
                Require(saved.GetProperty("intervalMinutes").GetInt64() == 30,
                    "configure persists interval");
                Require(saved.EnumerateObject().Count() == 2,
                    "configure persists only native resource config fields");
            }

            JsonElement wrongEnabled = await Invoke(
                service,
                "resource_automation_configure",
                new
                {
                    task = "armedTruckReward",
                    config = new
                    {
                        enabled = "yes",
                        intervalMinutes = 45,
                    },
                });
            AssertIdleTask(
                wrongEnabled,
                "armedTruckReward",
                enabled: false,
                intervalMinutes: 45,
                nextRunAt: null);

            JsonElement missingEnabled = await Invoke(
                service,
                "resource_automation_configure",
                new
                {
                    task = "armedTruckReward",
                    config = new
                    {
                        intervalMinutes = 15,
                    },
                });
            AssertIdleTask(
                missingEnabled,
                "armedTruckReward",
                enabled: false,
                intervalMinutes: 15,
                nextRunAt: null);

            foreach (object badInterval in new object[]
            {
                0,
                1441,
                -1,
                1.5,
                "60",
            })
            {
                await ExpectError(
                    service,
                    new
                    {
                        task = "buildingResources",
                        config = new
                        {
                            enabled = true,
                            intervalMinutes = badInterval,
                        },
                    },
                    "INVALID_REQUEST",
                    "interval must be an integer from 1 to 1440 minutes");
            }

            await ExpectError(
                service,
                new
                {
                    task = "buildingResources",
                    config = new
                    {
                        enabled = true,
                    },
                },
                "INVALID_REQUEST",
                "interval must be an integer from 1 to 1440 minutes");

            await ExpectError(
                service,
                new
                {
                    task = "unknownTask",
                    config = new
                    {
                        enabled = true,
                        intervalMinutes = 60,
                    },
                },
                "INVALID_REQUEST",
                "unknown resource automation task: unknownTask");

            await ExpectError(
                service,
                new
                {
                    config = new
                    {
                        enabled = true,
                        intervalMinutes = 60,
                    },
                },
                "INVALID_REQUEST",
                "unknown resource automation task: ");

            File.WriteAllText(automationStatusPath, "{");
            await ExpectError(
                service,
                "resource_automation_status",
                new { },
                "INVALID_AUTOMATION_STATUS",
                "INVALID_AUTOMATION_STATUS");
            File.Delete(automationStatusPath);

            var backend = new LWBridgeBackend(
                new LocalConfigStore(persistent: false),
                asyncCommands: service,
                runtimeTasksProvider: store.ReadTasksSnapshot);

            JsonElement backendStatus = JsonSerializer.SerializeToElement(
                await backend.InvokeAsync(
                    "resource_automation_status",
                    JsonSerializer.SerializeToElement(
                        new { profileId = backend.ProfileId },
                        JsonOptions.Default),
                    CancellationToken.None),
                JsonOptions.Default);
            Require(backendStatus.GetProperty("tasks")
                    .GetProperty("buildingResources")
                    .GetProperty("intervalMinutes").GetInt64() == 30,
                "production backend routes resource_automation_status");

            await ExpectBackendCode(
                backend,
                "resource_automation_run",
                new
                {
                    profileId = backend.ProfileId,
                    task = "buildingResources",
                },
                "COMMAND_NOT_IMPLEMENTED");
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); }
            catch { }
        }
    }

    private static void AssertIdleTask(
        JsonElement task,
        string expectedName,
        bool enabled,
        long intervalMinutes,
        long? nextRunAt)
    {
        Require(task.GetProperty("name").GetString() == expectedName,
            $"{expectedName} name");
        Require(task.GetProperty("state").GetString() ==
                (enabled ? "waiting" : "disabled"),
            $"{expectedName} state");
        Require(task.GetProperty("step").GetString() == "not_loaded",
            $"{expectedName} default step");
        Require(!task.GetProperty("running").GetBoolean(),
            $"{expectedName} is not running");
        Require(task.GetProperty("enabled").GetBoolean() == enabled,
            $"{expectedName} enabled");
        Require(task.GetProperty("intervalMinutes").GetInt64() ==
                intervalMinutes,
            $"{expectedName} interval");
        Require(!task.TryGetProperty("lastRunAt", out _),
            $"{expectedName} omits missing lastRunAt");

        JsonElement next = task.GetProperty("nextRunAt");
        if (nextRunAt.HasValue)
        {
            Require(next.GetInt64() == nextRunAt.Value,
                $"{expectedName} nextRunAt");
        }
        else
        {
            Require(next.ValueKind == JsonValueKind.Null,
                $"{expectedName} nextRunAt null when disabled");
        }
    }

    private static async Task<JsonElement> Invoke(
        ResourceAutomationConfigCommandService service,
        string command,
        object payload)
    {
        object? result = await service.InvokeAsync(
            command,
            JsonSerializer.SerializeToElement(
                payload,
                JsonOptions.Default),
            CancellationToken.None);
        return JsonSerializer.SerializeToElement(
            result,
            JsonOptions.Default);
    }

    private static Task<JsonElement> Invoke(
        ResourceAutomationConfigCommandService service,
        object payload) =>
        Invoke(service, "resource_automation_configure", payload);

    private static async Task ExpectError(
        ResourceAutomationConfigCommandService service,
        object payload,
        string expectedCode,
        string expectedMessage) =>
        await ExpectError(
            service,
            "resource_automation_configure",
            payload,
            expectedCode,
            expectedMessage);

    private static async Task ExpectError(
        ResourceAutomationConfigCommandService service,
        string command,
        object payload,
        string expectedCode,
        string expectedMessage)
    {
        try
        {
            _ = await Invoke(service, command, payload);
            throw new InvalidOperationException(
                $"Expected {command} to fail with {expectedCode}.");
        }
        catch (BridgeCommandException error)
        {
            Require(error.Code == expectedCode,
                $"expected {expectedCode}, got {error.Code}");
            Require(error.Message == expectedMessage,
                $"expected '{expectedMessage}', got '{error.Message}'");
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
                JsonSerializer.SerializeToElement(
                    payload,
                    JsonOptions.Default),
                CancellationToken.None);
            throw new InvalidOperationException(
                $"Expected {command} to fail with {expectedCode}.");
        }
        catch (BridgeCommandException error)
        {
            Require(error.Code == expectedCode,
                $"{command} expected {expectedCode}, got {error.Code}");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
