using System.Text;
using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed class ProfileRegistryCommandService :
    INativeAsyncCommandService,
    IDisposable
{
    private readonly ProfileRegistryStore store;
    private readonly int maxProfiles;

    internal ProfileRegistryCommandService(
        string currentProfileId,
        string databasePath,
        string displayName,
        int maxProfiles = 1)
    {
        store = new ProfileRegistryStore(databasePath);
        this.maxProfiles = maxProfiles;
        store.EnsureLocalProfile(
            currentProfileId,
            displayName,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }
    internal ProfileRegistryCommandService(
        ProfileRegistryStore store,
        int maxProfiles = 1)
    {
        this.store = store ??
            throw new ArgumentNullException(nameof(store));
        if (maxProfiles < 1)
            throw new ArgumentOutOfRangeException(nameof(maxProfiles));
        this.maxProfiles = maxProfiles;
    }

    public bool CanHandle(string command) =>
        command is "profile_list" or "profile_note_set";

    public Task<object?> InvokeAsync(
        string command,
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!CanHandle(command))
            throw new BridgeCommandException(
                "COMMAND_NOT_IMPLEMENTED",
                "Profile registry command is not implemented.");

        if (command == "profile_note_set")
        {
            string profileId = RequiredString(payload, "profileId");
            string note = RequiredString(payload, "note");
            ValidateNote(note);
            store.UpdateNote(
                profileId,
                note,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        object result = store.Read(maxProfiles);
        return Task.FromResult<object?>(result);
    }

    private static string RequiredString(
        JsonElement payload,
        string name)
    {
        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty(name, out JsonElement value) ||
            value.ValueKind != JsonValueKind.String)
        {
            throw new BridgeCommandException(
                "INVALID_REQUEST",
                "INVALID_REQUEST");
        }

        return value.GetString() ?? string.Empty;
    }

    private static void ValidateNote(string note)
    {
        int count = 0;
        foreach (Rune rune in note.EnumerateRunes())
        {
            count++;
            int value = rune.Value;
            if (count > 80 ||
                value < 0x20 ||
                (value >= 0x7f && value <= 0x9f))
            {
                throw new BridgeCommandException(
                    "INVALID_PROFILE_NOTE",
                    "INVALID_PROFILE_NOTE");
            }
        }
    }

    public void Dispose() => store.Dispose();
}
