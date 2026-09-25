using System.Text.Json;
using Microsoft.Data.Sqlite;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class ProfileRegistryChecks
{
    internal static async Task RunAsync()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "lwbridge-profile-registry-" + Guid.NewGuid().ToString("N"));
        string databasePath = Path.Combine(root, "controller.db");
        const string profileId = "local-profile-test";

        try
        {
            Directory.CreateDirectory(root);
            using var store = new ProfileRegistryStore(databasePath);
            store.EnsureLocalProfile(
                profileId,
                "Local Game",
                1_700_000_000_000);

            ProfileRegistrySnapshot snapshot = store.Read();
            Require(
                snapshot.SelectedProfileId == profileId,
                "selected profile is seeded");
            Require(snapshot.MaxProfiles == 1, "single-profile quota is one");
            Require(snapshot.Profiles.Count == 1, "one local profile is seeded");

            ProfileRegistryEntry profile = snapshot.Profiles[0];
            Require(profile.Id == profileId, "profile id is stable");
            Require(profile.DisplayName == "Local Game", "display name is preserved");
            Require(profile.RoleName is null, "role name starts unbound");
            Require(profile.ServerId is null, "server id starts unbound");
            Require(profile.GameUid is null, "game uid starts unbound");
            Require(profile.Note == "", "note defaults empty");
            Require(profile.DisplayOrder == 0, "display order starts at zero");
            Require(profile.Enabled, "local profile starts enabled");
            Require(profile.LockedReason is null, "local profile starts unlocked");
            Require(profile.IsPrimary, "local profile starts primary");
            Require(profile.LastLaunchedAt is null, "last launch starts null");
            // EnsureLocalProfile is idempotent and does not overwrite
            // registry metadata once the profile exists.
            using (var connection =
                new SqliteConnection($"Data Source={databasePath}"))
            {
                connection.Open();
                using SqliteCommand update = connection.CreateCommand();
                update.CommandText = """
                    UPDATE profiles
                    SET display_name = 'Renamed',
                        note = 'keep',
                        server_id = '1234',
                        role_name = 'Commander',
                        updated_at = updated_at + 1
                    WHERE id = $id
                    """;
                update.Parameters.AddWithValue("$id", profileId);
                Require(
                    update.ExecuteNonQuery() == 1,
                    "profile metadata update fixture succeeds");
            }

            store.EnsureLocalProfile(
                profileId,
                "Local Game",
                1_800_000_000_000);
            ProfileRegistryEntry preserved = store.Read().Profiles.Single();
            Require(
                preserved.DisplayName == "Renamed" &&
                preserved.Note == "keep" &&
                preserved.ServerId == "1234" &&
                preserved.RoleName == "Commander",
                "registry seeding preserves existing profile metadata");

            using var service =
                new ProfileRegistryCommandService(store);
            object? serviceResult = await service.InvokeAsync(
                "profile_list",
                JsonSerializer.SerializeToElement(
                    new { },
                    JsonOptions.Default),
                CancellationToken.None);
            JsonElement json = JsonSerializer.SerializeToElement(
                serviceResult,
                JsonOptions.Default);
            Require(
                json.EnumerateObject().Select(x => x.Name).ToHashSet()
                    .SetEquals(
                        ["selectedProfileId", "maxProfiles", "profiles"]),
                "profile_list result fields match recovered public shape");
            Require(
                json.GetProperty("selectedProfileId").GetString() == profileId,
                "profile_list returns selected profile");
            Require(
                json.GetProperty("maxProfiles").GetInt32() == 1,
                "profile_list returns single-profile quota");

            JsonElement publicProfile =
                json.GetProperty("profiles")[0];
            string[] publicFields =
                publicProfile.EnumerateObject()
                    .Select(x => x.Name)
                    .ToArray();
            Require(
                publicFields.ToHashSet().SetEquals(
                    [
                        "id",
                        "displayName",
                        "roleName",
                        "serverId",
                        "gameUid",
                        "note",
                        "displayOrder",
                        "enabled",
                        "lockedReason",
                        "isPrimary",
                        "lastLaunchedAt",
                    ]),
                "profile serializer exposes recovered public fields");
            Require(
                !publicProfile.TryGetProperty(
                    "connectionState",
                    out _),
                "profile list excludes rebuild-only connectionState");
            Require(
                !publicProfile.TryGetProperty("createdAt", out _) &&
                !publicProfile.TryGetProperty("updatedAt", out _),
                "profile list excludes database-only timestamps");
            var backend = new LWBridgeBackend(
                new LocalConfigStore(persistent: false),
                asyncCommands: service);
            JsonElement backendResult =
                JsonSerializer.SerializeToElement(
                    await backend.InvokeAsync(
                        "profile_list",
                        JsonSerializer.SerializeToElement(
                            new { },
                            JsonOptions.Default),
                        CancellationToken.None),
                    JsonOptions.Default);

            Require(
                backendResult.GetProperty("profiles")
                    .GetArrayLength() == 1,
                "backend routes profile_list to registry service");
            Require(
                backendResult.GetProperty("profiles")[0]
                    .GetProperty("displayName").GetString() == "Renamed",
                "backend returns persisted registry profile");
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); }
            catch { }
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
