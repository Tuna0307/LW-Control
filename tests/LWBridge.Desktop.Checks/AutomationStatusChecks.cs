using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class AutomationStatusChecks
{
    internal static async Task RunAsync()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "lwbridge-automation-status-" + Guid.NewGuid().ToString("N"));
        string runtimeDirectory = Path.Combine(root, "runtime");
        string configPath = Path.Combine(runtimeDirectory, "config.json");
        string statusPath =
            Path.Combine(runtimeDirectory, "automation-status.json");

        try
        {
            Directory.CreateDirectory(runtimeDirectory);
            File.WriteAllText(
                configPath,
                """
                {
                  "tasks": {
                    "construction": { "enabled": true, "maxBuilders": 1 },
                    "allianceTrainRide": { "enabled": false },
                    "officialPosition": { "enabled": "yes" },
                    "stamina": { "enabled": true },
                    "monsterSweep": {
                      "enabled": true,
                      "strategies": []
                    }
                  },
                  "futureRoot": { "keep": 7 }
                }
                """);

            var store = new ProfileRuntimeConfigStore(configPath);
            var service = new AutomationStatusCommandService(
                store,
                statusPath);

            JsonElement idle = await Invoke(service);
            Require(
                idle.EnumerateObject().Select(x => x.Name)
                    .SequenceEqual(["updatedAt", "lock", "tasks"]),
                "idle automation status preserves exact top-level field order");
            Require(
                idle.GetProperty("updatedAt").ValueKind ==
                    JsonValueKind.Null,
                "missing status has null updatedAt");
            Require(
                idle.GetProperty("lock").ValueKind ==
                    JsonValueKind.Null,
                "missing status has null lock");

            JsonElement tasks = idle.GetProperty("tasks");
            Require(
                tasks.EnumerateObject().Select(x => x.Name)
                    .SequenceEqual(AutomationStatusCommandService.TaskNames),
                "idle status exposes exact original 18-task sequence");

            foreach (string taskName in
                AutomationStatusCommandService.TaskNames)
            {
                bool expectedEnabled =
                    taskName is "construction" or "stamina" or "monsterSweep";
                AssertIdleTask(
                    tasks.GetProperty(taskName),
                    taskName,
                    expectedEnabled);
            }

            Require(
                !tasks.GetProperty("officialPosition")
                    .GetProperty("enabled").GetBoolean(),
                "non-boolean enabled value normalizes false");

            File.WriteAllText(
                statusPath,
                """
                {
                  "updatedAt": 123,
                  "lock": { "task": "construction" },
                  "tasks": {
                    "construction": {
                      "name": "construction",
                      "state": "running"
                    }
                  },
                  "futureStatusField": 9
                }
                """);

            JsonElement persisted = await Invoke(service);
            Require(
                persisted.GetProperty("updatedAt").GetInt64() == 123,
                "persisted automation status passes through");
            Require(
                persisted.GetProperty("lock")
                    .GetProperty("task").GetString() == "construction",
                "persisted automation lock passes through");
            Require(
                persisted.GetProperty("futureStatusField").GetInt32() == 9,
                "persisted unknown status fields pass through");

            File.WriteAllText(statusPath, "{");
            await ExpectError(
                service,
                "INVALID_AUTOMATION_STATUS");
            File.Delete(statusPath);

            var backend = new LWBridgeBackend(
                new LocalConfigStore(persistent: false),
                asyncCommands: service,
                runtimeTasksProvider: store.ReadTasksSnapshot);
            JsonElement backendStatus =
                JsonSerializer.SerializeToElement(
                    await backend.InvokeAsync(
                        "automation_status",
                        JsonSerializer.SerializeToElement(
                            new { profileId = backend.ProfileId },
                            JsonOptions.Default),
                        CancellationToken.None),
                    JsonOptions.Default);

            Require(
                backendStatus.GetProperty("tasks")
                    .EnumerateObject().Count() == 18,
                "production backend routes automation_status service");

            await ExpectBackendCode(
                backend,
                "automation_configure",
                new
                {
                    profileId = backend.ProfileId,
                    task = "construction",
                    config = new { enabled = true, maxBuilders = 1 },
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
        bool expectedEnabled)
    {
        Require(
            task.EnumerateObject().Select(x => x.Name).SequenceEqual(
                ["name", "state", "step", "running", "enabled"]),
            $"{expectedName} has exact idle field sequence");
        Require(
            task.GetProperty("name").GetString() == expectedName,
            $"{expectedName} name");
        Require(
            task.GetProperty("state").GetString() == "idle",
            $"{expectedName} idle state");
        Require(
            task.GetProperty("step").GetString() == "not_loaded",
            $"{expectedName} not-loaded step");
        Require(
            !task.GetProperty("running").GetBoolean(),
            $"{expectedName} is not running");
        Require(
            task.GetProperty("enabled").GetBoolean() == expectedEnabled,
            $"{expectedName} enabled projection");
    }

    private static async Task<JsonElement> Invoke(
        AutomationStatusCommandService service)
    {
        object? result = await service.InvokeAsync(
            "automation_status",
            JsonSerializer.SerializeToElement(
                new { profileId = "profile-automation-test" },
                JsonOptions.Default),
            CancellationToken.None);
        return JsonSerializer.SerializeToElement(
            result,
            JsonOptions.Default);
    }

    private static async Task ExpectError(
        AutomationStatusCommandService service,
        string expectedCode)
    {
        try
        {
            _ = await Invoke(service);
            throw new InvalidOperationException(
                $"Expected automation_status to fail with {expectedCode}.");
        }
        catch (BridgeCommandException error)
        {
            Require(
                error.Code == expectedCode,
                $"expected {expectedCode}, got {error.Code}");
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
