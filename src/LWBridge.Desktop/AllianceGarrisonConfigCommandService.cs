using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LWBridge.Desktop;

internal sealed class AllianceGarrisonConfigCommandService : INativeAsyncCommandService
{
    private readonly ProfileRuntimeConfigStore store;

    internal AllianceGarrisonConfigCommandService(string runtimeConfigPath) =>
        store = new ProfileRuntimeConfigStore(runtimeConfigPath);

    internal AllianceGarrisonConfigCommandService(ProfileRuntimeConfigStore store) =>
        this.store = store ?? throw new ArgumentNullException(nameof(store));

    public bool CanHandle(string command) =>
        command is "alliance_garrison_config_save";

    public Task<object?> InvokeAsync(
        string command,
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (command != "alliance_garrison_config_save")
        {
            throw new BridgeCommandException(
                "COMMAND_NOT_IMPLEMENTED",
                $"Alliance garrison config service does not handle '{command}'.");
        }

        JsonObject config = ParseAndValidate(payload);
        return Task.FromResult<object?>(
            store.SaveAllianceGarrisonConfig(config));
    }

    private static JsonObject ParseAndValidate(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object)
            throw InvalidConfigObject();

        bool enabled = false;
        if (payload.TryGetProperty("enabled", out JsonElement enabledElement))
        {
            if (!TryReadBoolean(enabledElement, out enabled))
                throw Invalid("alliance garrison enabled must be boolean");
        }

        if (payload.TryGetProperty(
                "recallOnDisable",
                out JsonElement recallOnDisable) &&
            !TryReadBoolean(recallOnDisable, out _))
        {
            throw Invalid(
                "alliance garrison recall on disable must be boolean");
        }

        if (!payload.TryGetProperty(
                "squadPriority",
                out JsonElement squadPriority) ||
            squadPriority.ValueKind != JsonValueKind.Array)
        {
            throw Invalid(
                "alliance garrison squad priority must be an array");
        }

        int squadCount = ValidateSquadPriority(squadPriority);

        if (!payload.TryGetProperty("targets", out JsonElement targets) ||
            targets.ValueKind != JsonValueKind.Array)
        {
            throw Invalid("alliance garrison targets must be an array");
        }

        int targetCount = ValidateTargets(targets);

        if (enabled && (targetCount == 0 || squadCount == 0))
        {
            throw Invalid(
                "alliance garrison requires at least one target and squad");
        }

        JsonNode? node = JsonNode.Parse(payload.GetRawText());
        if (node is not JsonObject config)
            throw InvalidConfigObject();

        config.Remove("profileId");
        return config;
    }

    private static int ValidateSquadPriority(JsonElement squadPriority)
    {
        var seen = new HashSet<long>();
        int count = 0;

        foreach (JsonElement item in squadPriority.EnumerateArray())
        {
            if (!item.TryGetInt64(out long squadIndex))
            {
                throw Invalid(
                    "alliance garrison squad priority must contain integers");
            }

            if (squadIndex < 1 || squadIndex > 4)
                throw Invalid("invalid squad index");

            if (!seen.Add(squadIndex))
            {
                throw Invalid(
                    "alliance garrison squad priority contains duplicates");
            }

            count++;
        }

        return count;
    }

    private static int ValidateTargets(JsonElement targets)
    {
        var identities = new HashSet<string>(StringComparer.Ordinal);
        int count = 0;

        foreach (JsonElement target in targets.EnumerateArray())
        {
            if (target.ValueKind != JsonValueKind.Object)
                throw Invalid("alliance garrison target must be an object");

            string identity = ValidateTarget(target);
            if (!identities.Add(identity))
            {
                throw Invalid(
                    "alliance garrison targets contain duplicates");
            }

            if (target.TryGetProperty(
                    "nameSnapshot",
                    out JsonElement nameSnapshot) &&
                nameSnapshot.ValueKind == JsonValueKind.String)
            {
                string name = nameSnapshot.GetString() ?? string.Empty;
                if (name.EnumerateRunes().Count() > 100)
                {
                    throw Invalid(
                        "alliance garrison target name is too long");
                }
            }

            count++;
        }

        return count;
    }

    private static string ValidateTarget(JsonElement target)
    {
        if (!target.TryGetProperty("kind", out JsonElement kindElement) ||
            kindElement.ValueKind != JsonValueKind.String)
        {
            throw Invalid("invalid alliance garrison target kind");
        }

        string? kind = kindElement.GetString();
        if (string.Equals(kind, "allianceBuilding", StringComparison.Ordinal))
        {
            if (!target.TryGetProperty("buildId", out JsonElement buildId) ||
                !buildId.TryGetInt64(out long id) ||
                id <= 0)
            {
                throw Invalid(
                    "alliance garrison building id must be positive");
            }

            return $"building:{id}";
        }

        if (!string.Equals(kind, "allyCity", StringComparison.Ordinal))
            throw Invalid("invalid alliance garrison target kind");

        string uid = ReadTrimmedString(target, "uid");
        if (uid.Length == 0)
            throw Invalid("alliance garrison ally uid is required");

        string uuidSnapshot = ReadTrimmedString(target, "uuidSnapshot");
        if (uuidSnapshot.Length > 0)
            ValidateCitySnapshot(target, uuidSnapshot);

        return "ally:" + uid;
    }

    private static void ValidateCitySnapshot(
        JsonElement target,
        string uuidSnapshot)
    {
        if (!uuidSnapshot.All(static c => c is >= '0' and <= '9') ||
            !TryReadPositiveInteger(target, "serverIdSnapshot") ||
            !TryReadPositiveInteger(target, "pointIdSnapshot"))
        {
            throw Invalid("alliance garrison city snapshot is invalid");
        }
    }

    private static bool TryReadPositiveInteger(
        JsonElement value,
        string property)
    {
        return value.TryGetProperty(property, out JsonElement element) &&
            element.TryGetInt64(out long integer) &&
            integer > 0;
    }

    private static string ReadTrimmedString(
        JsonElement value,
        string property)
    {
        if (!value.TryGetProperty(property, out JsonElement element) ||
            element.ValueKind != JsonValueKind.String)
        {
            return string.Empty;
        }

        return (element.GetString() ?? string.Empty).Trim();
    }

    private static bool TryReadBoolean(
        JsonElement element,
        out bool result)
    {
        result = false;
        if (element.ValueKind == JsonValueKind.True)
        {
            result = true;
            return true;
        }

        return element.ValueKind == JsonValueKind.False;
    }

    private static BridgeCommandException InvalidConfigObject() =>
        new("INVALID_CONFIG", "object required");

    private static BridgeCommandException Invalid(string message) =>
        new("INVALID_REQUEST", message);
}
