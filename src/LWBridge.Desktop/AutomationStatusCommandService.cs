using System.Text.Json;
using System.Text.Json.Nodes;

namespace LWBridge.Desktop;

internal sealed class AutomationStatusCommandService : INativeAsyncCommandService
{
    internal static readonly string[] TaskNames =
    [
        "construction",
        "allianceTrainRide",
        "officialPosition",
        "stamina",
        "staminaPotion",
        "treatment",
        "allianceDonate",
        "allianceHelp",
        "allianceGift",
        "strongholdResource",
        "allianceCenterResource",
        "allianceGather",
        "railway",
        "dispatch",
        "dispatchAssist",
        "ghostRecon",
        "monsterSweep",
    ];
    private readonly ProfileRuntimeConfigStore store;
    private readonly string runtimeStatusPath;

    internal AutomationStatusCommandService(
        ProfileRuntimeConfigStore store,
        string runtimeStatusPath)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        if (string.IsNullOrWhiteSpace(runtimeStatusPath))
        {
            throw new ArgumentException(
                "Automation status path is required.",
                nameof(runtimeStatusPath));
        }

        this.runtimeStatusPath = Path.GetFullPath(runtimeStatusPath);
    }

    public bool CanHandle(string command) =>
        command == "automation_status";

    private Func<CancellationToken, Task<JsonNode?>>? nativeStatusReader;

    internal void SetNativeStatusReader(Func<CancellationToken, Task<JsonNode?>> reader) =>
        nativeStatusReader = reader ?? throw new ArgumentNullException(nameof(reader));

    public async Task<object?> InvokeAsync(
        string command,
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!CanHandle(command))
        {
            throw new BridgeCommandException(
                "COMMAND_NOT_IMPLEMENTED",
                $"Automation status service does not handle '{command}'.");
        }

        JsonNode status = CreateStatus();
        if (nativeStatusReader is null) return status;
        JsonNode? native;
        try { native = await nativeStatusReader(cancellationToken).ConfigureAwait(false); }
        catch (BridgeCommandException error) when (error.Code is
            "GAME_CONNECTION_UNAVAILABLE" or "GAME_DISCONNECTED" or "GAME_ACCOUNT_CHANGED" or "GAME_COMMAND_TIMEOUT")
        {
            native = null;
        }
        JsonObject config = store.ReadTasksSnapshot();
        if (status["tasks"] is not JsonObject tasks) return status;
        foreach (string name in new[] { "monsterSweep", "staminaPotion", "allianceDrill" })
        {
            JsonNode? runtime = name == "monsterSweep" ? native : native?[name];
            JsonNode? taskConfig = name == "allianceDrill" ? config["monsterSweep"]?[name] : config[name];
            JsonObject row = runtime?.DeepClone() as JsonObject ?? new JsonObject
            {
                ["running"] = false, ["state"] = "disconnected", ["step"] = "disconnected",
            };
            // Engine status is source-native; enabled remains the profile's
            // persisted switch. Never let a stale status file claim a run.
            row["name"] = name;
            row["enabled"] = taskConfig?["enabled"]?.DeepClone() ?? JsonValue.Create(false);
            tasks[name] = row;
        }
        status["updatedAt"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return status;
    }

    internal JsonNode CreateStatus()
    {
        JsonNode? persisted = ReadRuntimeStatusIfPresent();
        if (persisted is not null)
            return persisted;

        JsonObject configTasks = store.ReadTasksSnapshot();
        var tasks = new JsonObject();
        foreach (string taskName in TaskNames)
        {
            bool enabled =
                configTasks[taskName] is JsonObject config &&
                config["enabled"] is JsonValue value &&
                value.TryGetValue(out bool parsed) &&
                parsed;

            tasks[taskName] = new JsonObject
            {
                ["name"] = taskName,
                ["state"] = "idle",
                ["step"] = "not_loaded",
                ["running"] = false,
                ["enabled"] = enabled,
            };
        }

        return new JsonObject
        {
            ["updatedAt"] = null,
            ["lock"] = null,
            ["tasks"] = tasks,
        };
    }

    private JsonNode? ReadRuntimeStatusIfPresent()
    {
        if (!File.Exists(runtimeStatusPath))
            return null;

        try
        {
            JsonNode? node = JsonNode.Parse(File.ReadAllText(runtimeStatusPath));
            if (node is null)
                throw new JsonException("Automation status JSON is null.");
            if (node["tasks"] is JsonObject tasks)
                ProfileRuntimeConfigStore.WithoutRetiredTasks(tasks);
            return node;
        }
        catch (BridgeCommandException)
        {
            throw;
        }
        catch (Exception error)
        {
            // ORIGINAL 0.3.1 returns INVALID_AUTOMATION_STATUS when the
            // persisted automation-status.json cannot be decoded. The exact
            // Rust serde parser-detail wording remains implementation-specific.
            throw new BridgeCommandException(
                "INVALID_AUTOMATION_STATUS",
                error.Message);
        }
    }
}
