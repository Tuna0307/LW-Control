using System.Text.Json;
using LWBridge.Desktop;
using Microsoft.Data.Sqlite;

namespace LWBridge.Desktop.Checks;

internal static class CityLayoutDraftChecks
{
    internal static async Task RunAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "lwbridge-city-layout-draft-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string databasePath = Path.Combine(root, "profile.db");
        const string profileId = "profile-city-layout-test";

        try
        {
            using (var service = new CityLayoutDraftCommandService(profileId, databasePath))
            {
                JsonElement empty = await Invoke(service, "city_layout_draft_get", new { profileId });
                Require(empty.GetProperty("profileId").GetString() == profileId, "draft get profile identity");
                Require(empty.GetProperty("key").GetString() == CityLayoutDraftCommandService.DraftKey, "draft get key");
                Require(empty.GetProperty("revision").GetInt64() == 0, "missing draft revision is frontend-compatible zero");
                Require(empty.GetProperty("value").ValueKind == JsonValueKind.Null, "missing draft value is null");

                JsonElement first = await Invoke(service, "city_layout_draft_save", new
                {
                    profileId,
                    revision = 0,
                    value = new
                    {
                        version = 1,
                        baseRevision = "layout-a",
                        placements = new[] { new { uuid = "100", targetPointId = 7 } },
                        updatedAt = 1000,
                    },
                });
                Require(first.GetProperty("revision").GetInt64() == 1, "first draft save creates revision 1");
                Require(first.GetProperty("value").GetProperty("baseRevision").GetString() == "layout-a",
                    "first draft value round-trips");

                JsonElement second = await Invoke(service, "city_layout_draft_save", new
                {
                    profileId,
                    revision = 1,
                    value = new
                    {
                        version = 1,
                        baseRevision = "layout-a",
                        placements = new[] { new { uuid = "100", targetPointId = 8 } },
                        updatedAt = 2000,
                    },
                });
                Require(second.GetProperty("revision").GetInt64() == 2, "matching draft update increments revision exactly once");

                await ExpectCode("PROFILE_REVISION_CONFLICT", async () =>
                    await Invoke(service, "city_layout_draft_save", new
                    {
                        profileId,
                        revision = 1,
                        value = new { version = 1, baseRevision = "stale", placements = Array.Empty<object>(), updatedAt = 3000 },
                    }));
                await ExpectCode("PROFILE_REVISION_CONFLICT", async () =>
                    await Invoke(service, "city_layout_draft_clear", new { profileId, revision = 1 }));
                await ExpectCode("INVALID_PROFILE_STATE", async () =>
                    await Invoke(service, "city_layout_draft_get", new { profileId = "other-profile" }));
            }

            using (var reopened = new CityLayoutDraftCommandService(profileId, databasePath))
            {
                JsonElement persisted = await Invoke(reopened, "city_layout_draft_get", new { profileId });
                Require(persisted.GetProperty("revision").GetInt64() == 2, "draft revision persists after reopen");
                Require(persisted.GetProperty("value").GetProperty("placements")[0].GetProperty("targetPointId").GetInt32() == 8,
                    "latest draft value persists after reopen");

                JsonElement cleared = await Invoke(reopened, "city_layout_draft_clear", new { profileId, revision = 2 });
                Require(cleared.GetProperty("revision").GetInt64() == 0 &&
                        cleared.GetProperty("value").ValueKind == JsonValueKind.Null,
                    "successful clear returns empty frontend-compatible state");

                JsonElement emptyAgain = await Invoke(reopened, "city_layout_draft_get", new { profileId });
                Require(emptyAgain.GetProperty("revision").GetInt64() == 0, "cleared draft is absent");
            }

            SeedMalformedJson(databasePath);
            using (var malformed = new CityLayoutDraftCommandService(profileId, databasePath))
                await ExpectCode("PROFILE_DATA_INVALID", async () =>
                    await Invoke(malformed, "city_layout_draft_get", new { profileId }));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); }
            catch { }
        }
    }

    private static async Task<JsonElement> Invoke(
        CityLayoutDraftCommandService service,
        string command,
        object payload)
    {
        JsonElement request = JsonSerializer.SerializeToElement(payload, JsonOptions.Default);
        object? result = await service.InvokeAsync(command, request, CancellationToken.None);
        return JsonSerializer.SerializeToElement(result, JsonOptions.Default);
    }

    private static async Task ExpectCode(string expectedCode, Func<Task> action)
    {
        try
        {
            await action();
            throw new InvalidOperationException($"Expected {expectedCode}.");
        }
        catch (BridgeCommandException error)
        {
            Require(error.Code == expectedCode,
                $"expected {expectedCode}, got {error.Code}");
        }
    }

    private static void SeedMalformedJson(string databasePath)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR REPLACE INTO profile_state(key, value_json, revision)
            VALUES ($key, $value, 1)
            """;
        command.Parameters.AddWithValue("$key", CityLayoutDraftCommandService.DraftKey);
        command.Parameters.AddWithValue("$value", "{");
        command.ExecuteNonQuery();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
