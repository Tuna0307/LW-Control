using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed class ProfileSettingsCommandService :
    INativeAsyncCommandService,
    IDisposable
{
    private readonly string profileId;
    private readonly ProfileSettingsStore store;

    internal ProfileSettingsCommandService(
        string profileId,
        string databasePath)
    {
        if (string.IsNullOrWhiteSpace(profileId))
            throw new ArgumentException(
                "Profile identity is required.",
                nameof(profileId));

        this.profileId = profileId;
        store = new ProfileSettingsStore(databasePath);
    }

    internal ProfileSettingsCommandService(
        string profileId,
        ProfileSettingsStore store)
    {
        if (string.IsNullOrWhiteSpace(profileId))
            throw new ArgumentException(
                "Profile identity is required.",
                nameof(profileId));
        this.profileId = profileId;
        this.store = store ??
            throw new ArgumentNullException(nameof(store));
    }

    public bool CanHandle(string command) =>
        command == "profile_settings_save";

    public Task<object?> InvokeAsync(
        string command,
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!CanHandle(command))
            throw new BridgeCommandException(
                "COMMAND_NOT_IMPLEMENTED",
                $"Profile settings service does not handle '{command}'.");

        string requestedProfileId =
            RequiredProfileId(payload);
        if (!string.Equals(
            requestedProfileId,
            profileId,
            StringComparison.Ordinal))
        {
            throw new BridgeCommandException(
                "PROFILE_NOT_FOUND",
                "PROFILE_NOT_FOUND");
        }
        long revision = RequiredRevision(payload);
        JsonElement value = RequiredValue(payload);
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new BridgeCommandException(
                "INVALID_PROFILE_SETTINGS",
                "INVALID_PROFILE_SETTINGS");
        }

        object result = store.Save(revision, value);
        return Task.FromResult<object?>(result);
    }

    private static string RequiredProfileId(
        JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty(
                "profileId",
                out JsonElement property) ||
            property.ValueKind != JsonValueKind.String)
        {
            throw InvalidRequest();
        }

        return property.GetString() ?? string.Empty;
    }

    private static long RequiredRevision(
        JsonElement payload)
    {
        if (!payload.TryGetProperty(
                "revision",
                out JsonElement property) ||
            property.ValueKind != JsonValueKind.Number ||
            !property.TryGetInt64(out long revision) ||
            revision < 0)
        {
            throw InvalidRequest();
        }

        return revision;
    }

    private static JsonElement RequiredValue(
        JsonElement payload)
    {
        if (!payload.TryGetProperty(
                "value",
                out JsonElement property))
        {
            throw InvalidRequest();
        }

        return property.Clone();
    }

    private static BridgeCommandException InvalidRequest() =>
        new("INVALID_REQUEST", "INVALID_REQUEST");

    public void Dispose() => store.Dispose();
}
