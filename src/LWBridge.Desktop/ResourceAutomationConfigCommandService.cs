using System.Text.Json;
using System.Text.Json.Nodes;

namespace LWBridge.Desktop;

internal sealed class ResourceAutomationConfigCommandService : INativeAsyncCommandService
{
    internal static readonly string[] TaskNames =
    [
        "buildingResources",
        "armedTruckReward",
    ];

    private readonly ProfileRuntimeConfigStore store;
    private readonly string runtimeStatusPath;
    private readonly Func<long> nowMilliseconds;

    internal ResourceAutomationConfigCommandService(
        ProfileRuntimeConfigStore store,
        string runtimeStatusPath,
        Func<long>? nowMilliseconds = null)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        if (string.IsNullOrWhiteSpace(runtimeStatusPath))
            throw new ArgumentException(
                "Automation status path is required.",
                nameof(runtimeStatusPath));

        this.runtimeStatusPath = Path.GetFullPath(runtimeStatusPath);
        this.nowMilliseconds =
            nowMilliseconds ?? RecoveredWallClock.UnixTimeMilliseconds;
    }

    internal event Action<object>? StatusChanged;

    public bool CanHandle(string command) =>
        command is "resource_automation_status" or
            "resource_automation_configure";

    public Task<object?> InvokeAsync(
        string command,
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        object result = command switch
        {
            "resource_automation_status" => CreateStatus(),
            "resource_automation_configure" => Configure(payload),
            _ => throw new BridgeCommandException(
                "COMMAND_NOT_IMPLEMENTED",
                $"Resource automation config service does not handle '{command}'."),
        };

        return Task.FromResult<object?>(result);
    }

    internal JsonObject CreateStatus()
    {
        JsonObject tasksConfig = store.ReadTasksSnapshot();
        JsonObject? runtime = ReadRuntimeStatusIfPresent();
        long now = nowMilliseconds();

        string? runningTask = ReadRunningTask(runtime);
        JsonObject runtimeTasks = runtime?["tasks"] as JsonObject
            ?? new JsonObject();

        var tasks = new JsonObject();
        foreach (string taskName in TaskNames)
        {
            (bool enabled, long intervalMinutes) =
                ReadConfig(tasksConfig, taskName);

            JsonObject? runtimeTask =
                runtimeTasks[taskName] as JsonObject;
            JsonObject projected = runtimeTask is null
                ? CreateMissingRuntimeTask(taskName)
                : (JsonObject)runtimeTask.DeepClone();

            bool running =
                ReadBoolean(runtimeTask, "running") ||
                string.Equals(
                    runningTask,
                    taskName,
                    StringComparison.Ordinal);

            projected["name"] = taskName;
            projected["state"] = !enabled
                ? "disabled"
                : running
                    ? "running"
                    : "waiting";
            projected["step"] =
                ReadString(runtimeTask, "step") ?? "not_loaded";
            projected["running"] = running;
            projected["enabled"] = enabled;
            projected["intervalMinutes"] = intervalMinutes;

            long? lastRunAt =
                ReadNonNegativeInteger(runtimeTask, "lastRunAt");
            if (lastRunAt.HasValue)
                projected["lastRunAt"] = lastRunAt.Value;
            else
                projected.Remove("lastRunAt");

            if (!enabled)
            {
                projected["nextRunAt"] = null;
            }
            else
            {
                long next = lastRunAt.HasValue
                    ? SaturatingAdd(
                        lastRunAt.Value,
                        intervalMinutes * 60_000L)
                    : now;
                projected["nextRunAt"] = Math.Max(next, now);
            }

            tasks[taskName] = projected;
        }

        return new JsonObject
        {
            ["updatedAt"] = now,
            ["runningTask"] = runningTask,
            ["tasks"] = tasks,
        };
    }

    private JsonObject Configure(JsonElement payload)
    {
        string taskName = ReadTaskName(payload);
        JsonElement config =
            payload.TryGetProperty("config", out JsonElement value)
                ? value
                : default;

        if (config.ValueKind != JsonValueKind.Object ||
            !config.TryGetProperty(
                "intervalMinutes",
                out JsonElement intervalValue) ||
            intervalValue.ValueKind != JsonValueKind.Number ||
            !intervalValue.TryGetInt64(out long intervalMinutes) ||
            intervalMinutes is < 1 or > 1440)
        {
            throw new BridgeCommandException(
                "INVALID_REQUEST",
                "interval must be an integer from 1 to 1440 minutes");
        }

        bool enabled =
            config.TryGetProperty("enabled", out JsonElement enabledValue) &&
            enabledValue.ValueKind == JsonValueKind.True;

        var savedConfig = new JsonObject
        {
            ["enabled"] = enabled,
            ["intervalMinutes"] = intervalMinutes,
        };
        store.SaveResourceAutomationConfig(taskName, savedConfig);

        JsonObject status = CreateStatus();
        StatusChanged?.Invoke(status);

        JsonObject tasks = (JsonObject)status["tasks"]!;
        return (JsonObject)tasks[taskName]!.DeepClone();
    }

    private static string ReadTaskName(JsonElement payload)
    {
        string taskName =
            payload.TryGetProperty("task", out JsonElement taskValue) &&
            taskValue.ValueKind == JsonValueKind.String
                ? taskValue.GetString() ?? string.Empty
                : string.Empty;

        if (!TaskNames.Contains(taskName, StringComparer.Ordinal))
        {
            throw new BridgeCommandException(
                "INVALID_REQUEST",
                $"unknown resource automation task: {taskName}");
        }

        return taskName;
    }

    private JsonObject? ReadRuntimeStatusIfPresent()
    {
        if (!File.Exists(runtimeStatusPath))
            return null;

        try
        {
            JsonNode? node =
                JsonNode.Parse(File.ReadAllText(runtimeStatusPath));
            return node as JsonObject
                ?? throw InvalidAutomationStatus();
        }
        catch (BridgeCommandException)
        {
            throw;
        }
        catch (Exception)
        {
            throw InvalidAutomationStatus();
        }
    }

    private static JsonObject CreateMissingRuntimeTask(string taskName) =>
        new()
        {
            ["name"] = taskName,
            ["state"] = "idle",
            ["step"] = "not_loaded",
            ["running"] = false,
            ["enabled"] = false,
        };

    private static (bool Enabled, long IntervalMinutes) ReadConfig(
        JsonObject tasks,
        string taskName)
    {
        if (tasks[taskName] is not JsonObject task)
            return (false, 60);

        bool enabled = ReadBoolean(task, "enabled");
        long intervalMinutes =
            ReadPositiveInteger(task, "intervalMinutes") is long value &&
            value is >= 1 and <= 1440
                ? value
                : 60;
        return (enabled, intervalMinutes);
    }

    private static bool ReadBoolean(JsonObject? value, string name) =>
        value is not null &&
        value[name] is JsonValue node &&
        node.TryGetValue(out bool parsed) &&
        parsed;

    private static string? ReadString(JsonObject? value, string name) =>
        value is not null &&
        value[name] is JsonValue node &&
        node.TryGetValue(out string? parsed)
            ? parsed
            : null;

    private static long? ReadPositiveInteger(
        JsonObject? value,
        string name) =>
        ReadInteger(value, name) is long parsed && parsed > 0
            ? parsed
            : null;

    private static long? ReadNonNegativeInteger(
        JsonObject? value,
        string name) =>
        ReadInteger(value, name) is long parsed && parsed >= 0
            ? parsed
            : null;

    private static long? ReadInteger(JsonObject? value, string name)
    {
        if (value is null || value[name] is not JsonValue node)
            return null;

        return node.TryGetValue(out long parsed)
            ? parsed
            : null;
    }

    private static string? ReadRunningTask(JsonObject? runtime)
    {
        if (runtime is null)
            return null;

        if (ReadString(runtime, "runningTask") is string direct &&
            !string.IsNullOrEmpty(direct))
        {
            return direct;
        }

        if (runtime["lock"] is JsonObject lockObject)
        {
            string? task =
                ReadString(lockObject, "task") ??
                ReadString(lockObject, "name");
            if (!string.IsNullOrEmpty(task))
                return task;
        }

        return null;
    }

    private static long SaturatingAdd(long left, long right)
    {
        if (right > 0 && left > long.MaxValue - right)
            return long.MaxValue;
        if (right < 0 && left < long.MinValue - right)
            return long.MinValue;
        return left + right;
    }

    private static BridgeCommandException InvalidAutomationStatus() =>
        new(
            "INVALID_AUTOMATION_STATUS",
            "INVALID_AUTOMATION_STATUS");
}
