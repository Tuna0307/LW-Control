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
        command is "profile_list" or
            "profile_note_set" or
            "profile_reorder" or
            "profile_primary_set";

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
        else if (command == "profile_reorder")
        {
            IReadOnlyList<string> profileIds =
                RequiredProfileIds(payload);
            foreach (string profileId in profileIds)
                ValidateProfileId(profileId);
            store.Reorder(
                profileIds,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }
        else if (command == "profile_primary_set")
        {
            string profileId = RequiredString(payload, "profileId");
            ValidateProfileId(profileId);
            store.AssertPrimary(profileId);
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

    private static IReadOnlyList<string> RequiredProfileIds(
        JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty(
                "profileIds",
                out JsonElement value) ||
            value.ValueKind != JsonValueKind.Array)
        {
            throw new BridgeCommandException(
                "INVALID_REQUEST",
                "INVALID_REQUEST");
        }

        var profileIds = new List<string>();
        foreach (JsonElement item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                throw new BridgeCommandException(
                    "INVALID_REQUEST",
                    "INVALID_REQUEST");
            }
            profileIds.Add(item.GetString() ?? string.Empty);
        }
        return profileIds;
    }

    private static void ValidateProfileId(string profileId)
    {
        int byteCount = Encoding.UTF8.GetByteCount(profileId);
        if (byteCount is < 1 or > 64 ||
            profileId.Any(ch =>
                !((ch >= '0' && ch <= '9') ||
                  (ch >= 'A' && ch <= 'Z') ||
                  (ch >= 'a' && ch <= 'z') ||
                  ch is '_' or '-')))
        {
            throw new BridgeCommandException(
                "INVALID_PROFILE_ID",
                "INVALID_PROFILE_ID");
        }
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
