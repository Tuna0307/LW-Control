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
        "allianceGarrison",
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

    public Task<object?> InvokeAsync(
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

        return Task.FromResult<object?>(CreateStatus());
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
