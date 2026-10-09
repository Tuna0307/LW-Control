using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed class CityLayoutDraftCommandService : INativeAsyncCommandService, IDisposable
{
    internal const string DraftKey = "city_layout_draft_v1";

    private readonly string profileId;
    private readonly ProfileStateStore store;

    public CityLayoutDraftCommandService(string profileId, string databasePath)
    {
        if (string.IsNullOrWhiteSpace(profileId))
            throw new ArgumentException("Profile identity is required.", nameof(profileId));
        this.profileId = profileId;
        store = new ProfileStateStore(databasePath);
    }

    internal CityLayoutDraftCommandService(string profileId, ProfileStateStore store)
    {
        if (string.IsNullOrWhiteSpace(profileId))
            throw new ArgumentException("Profile identity is required.", nameof(profileId));
        this.profileId = profileId;
        this.store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public bool CanHandle(string command) => command is
        "city_layout_draft_get" or
        "city_layout_draft_save" or
        "city_layout_draft_clear";

    public Task<object?> InvokeAsync(
        string command,
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!CanHandle(command))
            throw new BridgeCommandException(
                "COMMAND_NOT_IMPLEMENTED",
                $"City Layout draft service does not handle '{command}'.");

        RequireSelectedProfile(payload);

        object result = command switch
        {
            "city_layout_draft_get" =>
                store.Read(profileId, DraftKey),
            "city_layout_draft_save" =>
                store.Save(
                    profileId,
                    DraftKey,
                    RequiredRevision(payload),
                    RequiredValue(payload)),
            "city_layout_draft_clear" =>
                store.Clear(
                    profileId,
                    DraftKey,
                    RequiredRevision(payload)),
            _ => throw new InvalidOperationException(),
        };
        return Task.FromResult<object?>(result);
    }

    private void RequireSelectedProfile(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty("profileId", out JsonElement value) ||
            value.ValueKind != JsonValueKind.String ||
            !string.Equals(value.GetString(), profileId, StringComparison.Ordinal))
        {
            throw new BridgeCommandException(
                "INVALID_PROFILE_STATE",
                "invalid profile state");
        }
    }

    private static long RequiredRevision(JsonElement payload)
    {
        if (!payload.TryGetProperty("revision", out JsonElement value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt64(out long revision) ||
            revision < 0)
        {
            throw new BridgeCommandException(
                "INVALID_PROFILE_STATE",
                "invalid profile state");
        }
        return revision;
    }

    private static JsonElement RequiredValue(JsonElement payload)
    {
        if (!payload.TryGetProperty("value", out JsonElement value) ||
            value.ValueKind == JsonValueKind.Undefined)
        {
            throw new BridgeCommandException(
                "INVALID_PROFILE_STATE",
                "invalid profile state");
        }
        return value.Clone();
    }

    public void Dispose() => store.Dispose();
}
