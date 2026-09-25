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

            JsonElement noteResult = await InvokeNote(
                service,
                profileId,
                "hello 😀");
            Require(
                noteResult.GetProperty("selectedProfileId").GetString() ==
                    profileId,
                "note save preserves selected profile");
            Require(
                noteResult.GetProperty("profiles")[0]
                    .GetProperty("note").GetString() == "hello 😀",
                "note save returns refreshed profile list");

            long updatedAt = ReadUpdatedAt(databasePath, profileId);
            Require(
                updatedAt > 1_700_000_000_001,
                "note save updates updated_at");

            string eightyEmoji = string.Concat(
                Enumerable.Repeat("😀", 80));
            JsonElement eightyResult = await InvokeNote(
                service,
                profileId,
                eightyEmoji);
            Require(
                eightyResult.GetProperty("profiles")[0]
                    .GetProperty("note").GetString() == eightyEmoji,
                "80 non-BMP Unicode scalars are accepted");

            await ExpectNoteCode(
                service,
                profileId,
                string.Concat(Enumerable.Repeat("😀", 81)),
                "INVALID_PROFILE_NOTE");
            await ExpectNoteCode(
                service,
                profileId,
                "bad\u001fcontrol",
                "INVALID_PROFILE_NOTE");
            await ExpectNoteCode(
                service,
                profileId,
                "bad\u007fcontrol",
                "INVALID_PROFILE_NOTE");
            await ExpectNoteCode(
                service,
                profileId,
                "bad\u009fcontrol",
                "INVALID_PROFILE_NOTE");

            JsonElement nbspResult = await InvokeNote(
                service,
                profileId,
                "A\u00a0B");
            Require(
                nbspResult.GetProperty("profiles")[0]
                    .GetProperty("note").GetString() == "A\u00a0B",
                "U+00A0 is accepted");

            JsonElement emptyResult = await InvokeNote(
                service,
                profileId,
                "");
            Require(
                emptyResult.GetProperty("profiles")[0]
                    .GetProperty("note").GetString() == "",
                "empty note is accepted");

            await ExpectNoteCode(
                service,
                "missing-profile",
                "valid",
                "PROFILE_NOT_FOUND");
            await ExpectPayloadCode(
                service,
                new { note = "missing id" },
                "INVALID_REQUEST");
            await ExpectPayloadCode(
                service,
                new { profileId, note = 7 },
                "INVALID_REQUEST");

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

            JsonElement backendNoteResult =
                JsonSerializer.SerializeToElement(
                    await backend.InvokeAsync(
                        "profile_note_set",
                        JsonSerializer.SerializeToElement(
                            new
                            {
                                profileId,
                                note = "backend note",
                            },
                            JsonOptions.Default),
                        CancellationToken.None),
                    JsonOptions.Default);
            Require(
                backendNoteResult.GetProperty("selectedProfileId")
                    .GetString() == profileId &&
                backendNoteResult.GetProperty("profiles")[0]
                    .GetProperty("note").GetString() == "backend note",
                "backend routes profile_note_set and returns refreshed list");
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); }
            catch { }
        }
    }

    private static async Task<JsonElement> InvokeNote(
        ProfileRegistryCommandService service,
        string profileId,
        string note)
    {
        object? result = await service.InvokeAsync(
            "profile_note_set",
            JsonSerializer.SerializeToElement(
                new { profileId, note },
                JsonOptions.Default),
            CancellationToken.None);
        return JsonSerializer.SerializeToElement(
            result,
            JsonOptions.Default);
    }

    private static async Task ExpectNoteCode(
        ProfileRegistryCommandService service,
        string profileId,
        string note,
        string expectedCode) =>
        await ExpectPayloadCode(
            service,
            new { profileId, note },
            expectedCode);

    private static async Task ExpectPayloadCode(
        ProfileRegistryCommandService service,
        object payload,
        string expectedCode)
    {
        try
        {
            _ = await service.InvokeAsync(
                "profile_note_set",
                JsonSerializer.SerializeToElement(
                    payload,
                    JsonOptions.Default),
                CancellationToken.None);
            throw new InvalidOperationException(
                $"Expected profile_note_set to fail with {expectedCode}.");
        }
        catch (BridgeCommandException error)
        {
            Require(
                error.Code == expectedCode,
                $"expected {expectedCode}, got {error.Code}");
        }
    }

    private static long ReadUpdatedAt(
        string databasePath,
        string profileId)
    {
        using var connection =
            new SqliteConnection($"Data Source={databasePath}");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT updated_at FROM profiles WHERE id = $id";
        command.Parameters.AddWithValue("$id", profileId);
        return Convert.ToInt64(command.ExecuteScalar());
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
