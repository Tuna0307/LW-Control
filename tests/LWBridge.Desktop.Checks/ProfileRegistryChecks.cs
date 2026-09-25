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

            InsertProfile(
                databasePath,
                "alpha",
                "Alpha",
                1,
                1_700_000_000_100);
            InsertProfile(
                databasePath,
                "valid_ID-3",
                "Beta",
                2,
                1_700_000_000_200);

            JsonElement reorderResult = await InvokeReorder(
                service,
                ["valid_ID-3", profileId, "alpha"]);
            JsonElement reorderedProfiles =
                reorderResult.GetProperty("profiles");
            Require(
                reorderedProfiles.GetArrayLength() == 3 &&
                reorderedProfiles[0].GetProperty("id").GetString() ==
                    "valid_ID-3" &&
                reorderedProfiles[1].GetProperty("id").GetString() ==
                    profileId &&
                reorderedProfiles[2].GetProperty("id").GetString() ==
                    "alpha",
                "reorder returns profiles in requested order");
            Require(
                reorderedProfiles[0].GetProperty("displayOrder").GetInt64() == 0 &&
                reorderedProfiles[1].GetProperty("displayOrder").GetInt64() == 1 &&
                reorderedProfiles[2].GetProperty("displayOrder").GetInt64() == 2,
                "reorder assigns zero-based display order");
            Require(
                reorderResult.GetProperty("selectedProfileId").GetString() ==
                    profileId,
                "reorder preserves selected profile");
            long[] reorderTimestamps =
                ReadProfileTimestamps(databasePath);
            Require(
                reorderTimestamps.Distinct().Count() == 1 &&
                reorderTimestamps[0] > 1_700_000_000_200,
                "reorder applies one shared updated_at timestamp");

            await ExpectReorderCode(
                service,
                ["valid_ID-3", profileId, profileId],
                "INVALID_PROFILE_ORDER");
            await ExpectReorderCode(
                service,
                [profileId, "alpha"],
                "INVALID_PROFILE_ORDER");
            await ExpectReorderCode(
                service,
                [],
                "INVALID_PROFILE_ORDER");
            await ExpectReorderCode(
                service,
                [profileId, "alpha", "valid_ID-3", "extra"],
                "INVALID_PROFILE_ORDER");
            await ExpectReorderCode(
                service,
                [profileId, "bad id", "alpha"],
                "INVALID_PROFILE_ID");
            await ExpectReorderCode(
                service,
                [profileId, new string('a', 65), "alpha"],
                "INVALID_PROFILE_ID");
            await ExpectReorderCode(
                service,
                ["", profileId, "alpha"],
                "INVALID_PROFILE_ID");
            await ExpectCommandPayloadCode(
                service,
                "profile_reorder",
                new { profileIds = "not-an-array" },
                "INVALID_REQUEST");
            await ExpectCommandPayloadCode(
                service,
                "profile_reorder",
                new { profileIds = new object[] { profileId, 7, "alpha" } },
                "INVALID_REQUEST");

            Require(
                store.Read().Profiles.Select(profile => profile.Id)
                    .SequenceEqual(
                        ["valid_ID-3", profileId, "alpha"]),
                "failed reorder attempts leave order unchanged");

            JsonElement backendReorder =
                JsonSerializer.SerializeToElement(
                    await backend.InvokeAsync(
                        "profile_reorder",
                        JsonSerializer.SerializeToElement(
                            new
                            {
                                profileIds = new[]
                                {
                                    "alpha",
                                    "valid_ID-3",
                                    profileId,
                                },
                            },
                            JsonOptions.Default),
                        CancellationToken.None),
                    JsonOptions.Default);
            Require(
                backendReorder.GetProperty("profiles")[0]
                    .GetProperty("id").GetString() == "alpha" &&
                backendReorder.GetProperty("profiles")[2]
                    .GetProperty("id").GetString() == profileId,
                "backend routes profile_reorder");

            Require(
                HasPrimaryUniqueIndex(databasePath),
                "controller schema has native primary uniqueness index");
            long primaryUpdatedAt =
                ReadUpdatedAt(databasePath, profileId);
            JsonElement primaryResult = await InvokePrimary(
                service,
                profileId);
            Require(
                primaryResult.GetProperty("selectedProfileId").GetString() ==
                    profileId,
                "primary assertion preserves selected profile");
            Require(
                store.Read().Profiles.Single(
                    profile => profile.Id == profileId).IsPrimary,
                "current primary remains primary");
            Require(
                ReadUpdatedAt(databasePath, profileId) == primaryUpdatedAt,
                "primary assertion is a no-op mutation");

            await ExpectCommandPayloadCode(
                service,
                "profile_primary_set",
                new { profileId = "alpha" },
                "PROFILE_PRIMARY_FIXED");
            await ExpectCommandPayloadCode(
                service,
                "profile_primary_set",
                new { profileId = "missing-profile" },
                "PROFILE_NOT_FOUND");
            await ExpectCommandPayloadCode(
                service,
                "profile_primary_set",
                new { profileId = "bad id" },
                "INVALID_PROFILE_ID");
            await ExpectCommandPayloadCode(
                service,
                "profile_primary_set",
                new { },
                "INVALID_REQUEST");

            JsonElement backendPrimary =
                JsonSerializer.SerializeToElement(
                    await backend.InvokeAsync(
                        "profile_primary_set",
                        JsonSerializer.SerializeToElement(
                            new { profileId },
                            JsonOptions.Default),
                        CancellationToken.None),
                    JsonOptions.Default);
            Require(
                backendPrimary.GetProperty("selectedProfileId")
                    .GetString() == profileId,
                "backend routes profile_primary_set");
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

    private static Task ExpectPayloadCode(
        ProfileRegistryCommandService service,
        object payload,
        string expectedCode) =>
        ExpectCommandPayloadCode(
            service,
            "profile_note_set",
            payload,
            expectedCode);

    private static async Task<JsonElement> InvokePrimary(
        ProfileRegistryCommandService service,
        string profileId)
    {
        object? result = await service.InvokeAsync(
            "profile_primary_set",
            JsonSerializer.SerializeToElement(
                new { profileId },
                JsonOptions.Default),
            CancellationToken.None);
        return JsonSerializer.SerializeToElement(
            result,
            JsonOptions.Default);
    }

    private static async Task<JsonElement> InvokeReorder(
        ProfileRegistryCommandService service,
        IReadOnlyList<string> profileIds)
    {
        object? result = await service.InvokeAsync(
            "profile_reorder",
            JsonSerializer.SerializeToElement(
                new { profileIds },
                JsonOptions.Default),
            CancellationToken.None);
        return JsonSerializer.SerializeToElement(
            result,
            JsonOptions.Default);
    }

    private static Task ExpectReorderCode(
        ProfileRegistryCommandService service,
        IReadOnlyList<string> profileIds,
        string expectedCode) =>
        ExpectCommandPayloadCode(
            service,
            "profile_reorder",
            new { profileIds },
            expectedCode);

    private static async Task ExpectCommandPayloadCode(
        ProfileRegistryCommandService service,
        string command,
        object payload,
        string expectedCode)
    {
        try
        {
            _ = await service.InvokeAsync(
                command,
                JsonSerializer.SerializeToElement(
                    payload,
                    JsonOptions.Default),
                CancellationToken.None);
            throw new InvalidOperationException(
                $"Expected {command} to fail with {expectedCode}.");
        }
        catch (BridgeCommandException error)
        {
            Require(
                error.Code == expectedCode,
                $"expected {expectedCode}, got {error.Code}");
        }
    }

    private static void InsertProfile(
        string databasePath,
        string id,
        string displayName,
        long displayOrder,
        long timestamp)
    {
        using var connection =
            new SqliteConnection($"Data Source={databasePath}");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO profiles(
                id, display_name, role_name, server_id, game_uid,
                note, display_order, enabled, locked_reason,
                is_primary, created_at, updated_at, last_launched_at)
            VALUES(
                $id, $displayName, NULL, NULL, NULL,
                '', $displayOrder, 1, NULL,
                0, $timestamp, $timestamp, NULL)
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$displayName", displayName);
        command.Parameters.AddWithValue("$displayOrder", displayOrder);
        command.Parameters.AddWithValue("$timestamp", timestamp);
        Require(command.ExecuteNonQuery() == 1, "profile fixture inserted");
    }

    private static long[] ReadProfileTimestamps(string databasePath)
    {
        using var connection =
            new SqliteConnection($"Data Source={databasePath}");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT updated_at FROM profiles ORDER BY id";
        using SqliteDataReader reader = command.ExecuteReader();
        var values = new List<long>();
        while (reader.Read())
            values.Add(reader.GetInt64(0));
        return values.ToArray();
    }

    private static bool HasPrimaryUniqueIndex(string databasePath)
    {
        using var connection =
            new SqliteConnection($"Data Source={databasePath}");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'index'
              AND name = 'idx_profiles_primary'
              AND sql LIKE '%is_primary = 1%'
            """;
        return Convert.ToInt64(command.ExecuteScalar()) == 1;
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
