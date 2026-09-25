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
        command == "profile_list";

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

        object result = store.Read(maxProfiles);
        return Task.FromResult<object?>(result);
    }

    public void Dispose() => store.Dispose();
}
