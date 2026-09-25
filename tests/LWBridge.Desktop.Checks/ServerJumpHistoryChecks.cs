using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class ServerJumpHistoryChecks
{
    internal static async Task RunAsync()
    {
        const string profileId = "history-profile";

        using (MapDataStore store = MapDataStore.CreateInMemory())
        {
            var service = new ServerJumpHistoryCommandService(profileId, store);

            IReadOnlyList<int> missing = service.Invoke(
                "server_jump_history_get",
                Payload(new { profileId }));
            Require(missing.Count == 0, "missing history setting returns empty array");
            Require(
                store.ReadAppSettingJson("serverJumpHistory") is null,
                "get does not create a missing history setting");

            using JsonDocument mixed = JsonDocument.Parse(
                """
                {
                  "profileId": "history-profile",
                  "history": [9, 9, 0, 100000, 8, "7", 7, 6, 5, 4]
                }
                """);
            IReadOnlyList<int> normalized = service.Invoke(
                "server_jump_history_set",
                mixed.RootElement.Clone());
            Require(
                normalized.SequenceEqual([9, 8, 7, 6, 5]),
                "set keeps first-seen valid unique IDs with five-item limit");
            Require(
                store.ReadAppSettingJson("serverJumpHistory") ==
                    "[9,8,7,6,5]",
                "set persists normalized compact JSON in app_settings");

            IReadOnlyList<int> reloaded = service.Invoke(
                "server_jump_history_get",
                Payload(new { profileId }));
            Require(
                reloaded.SequenceEqual([9, 8, 7, 6, 5]),
                "get reads normalized history from app_settings");

            IReadOnlyList<int> nonArray = service.Invoke(
                "server_jump_history_set",
                Payload(new { profileId, history = "not-an-array" }));
            Require(
                nonArray.Count == 0 &&
                store.ReadAppSettingJson("serverJumpHistory") == "[]",
                "non-array history normalizes to an empty persisted array");

            IReadOnlyList<int> missingHistory = service.Invoke(
                "server_jump_history_set",
                Payload(new { profileId }));
            Require(
                missingHistory.Count == 0 &&
                store.ReadAppSettingJson("serverJumpHistory") == "[]",
                "missing history normalizes to an empty persisted array");
        }

        using (MapDataStore importStore = MapDataStore.CreateInMemory())
        {
            var service = new ServerJumpHistoryCommandService(
                profileId,
                importStore);

            IReadOnlyList<int> imported = service.Invoke(
                "server_jump_history_import",
                Payload(new
                {
                    profileId,
                    history = new[] { 501, 501, 0, 502, 100000, 503 },
                }));
            Require(
                imported.SequenceEqual([501, 502, 503]),
                "import normalizes legacy history when no app setting exists");
            Require(
                importStore.ReadAppSettingJson("serverJumpHistory") ==
                    "[501,502,503]",
                "import persists migrated legacy history");

            IReadOnlyList<int> existingWins = service.Invoke(
                "server_jump_history_import",
                Payload(new
                {
                    profileId,
                    history = new[] { 700, 701 },
                }));
            Require(
                existingWins.SequenceEqual([501, 502, 503]),
                "import returns existing app setting instead of overwriting it");
            Require(
                importStore.ReadAppSettingJson("serverJumpHistory") ==
                    "[501,502,503]",
                "existing app setting wins over later legacy import");
        }

        using (MapDataStore errorStore = MapDataStore.CreateInMemory())
        {
            var service = new ServerJumpHistoryCommandService(
                profileId,
                errorStore);

            ExpectCode(
                () => service.Invoke(
                    "server_jump_history_get",
                    Payload(new { })),
                "PROFILE_ID_REQUIRED",
                "missing profileId");
            ExpectCode(
                () => service.Invoke(
                    "server_jump_history_get",
                    Payload(new { profileId = 7 })),
                "PROFILE_ID_REQUIRED",
                "non-string profileId");
            ExpectCode(
                () => service.Invoke(
                    "server_jump_history_get",
                    Payload(new { profileId = " " })),
                "PROFILE_ID_REQUIRED",
                "blank profileId");
            ExpectCode(
                () => service.Invoke(
                    "server_jump_history_get",
                    Payload(new { profileId = "other-profile" })),
                "PROFILE_RUNTIME_UNAVAILABLE",
                "unknown profile runtime");

            errorStore.UpsertAppSettingJson(
                "serverJumpHistory",
                "{ definitely not json",
                1);
            ExpectCode(
                () => service.Invoke(
                    "server_jump_history_get",
                    Payload(new { profileId })),
                "INVALID_SETTING",
                "corrupt stored app setting");
        }

        var unavailable = new ServerJumpHistoryCommandService(
            profileId,
            store: null);
        ExpectCode(
            () => unavailable.Invoke(
                "server_jump_history_get",
                Payload(new { profileId })),
            "PROFILE_RUNTIME_UNAVAILABLE",
            "missing owned map-data runtime");

        string configRoot = Path.Combine(
            Path.GetTempPath(),
            "lwbridge-history-backend-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(configRoot);
        try
        {
            var config = new LocalConfigStore(configRoot);
            using MapDataStore store = MapDataStore.CreateInMemory();
            var backend = new LWBridgeBackend(
                config,
                mapData: store);

            object? result = await backend.InvokeAsync(
                "server_jump_history_set",
                Payload(new
                {
                    profileId = backend.ProfileId,
                    history = new[] { 42, 42, 41 },
                }),
                CancellationToken.None);
            Require(
                result is IReadOnlyList<int> history &&
                history.SequenceEqual([42, 41]),
                "backend routes server_jump_history_set to map app settings");
            Require(
                store.ReadAppSettingJson("serverJumpHistory") == "[42,41]",
                "backend history route persists to map-data app_settings");

            object? getResult = await backend.InvokeAsync(
                "server_jump_history_get",
                Payload(new { profileId = backend.ProfileId }),
                CancellationToken.None);
            Require(
                getResult is IReadOnlyList<int> getHistory &&
                getHistory.SequenceEqual([42, 41]),
                "backend routes server_jump_history_get");
        }
        finally
        {
            try { Directory.Delete(configRoot, recursive: true); }
            catch { }
        }
    }

    private static JsonElement Payload(object value) =>
        JsonSerializer.SerializeToElement(value, JsonOptions.Default);

    private static void ExpectCode(
        Action action,
        string expectedCode,
        string label)
    {
        try
        {
            action();
            throw new InvalidOperationException(
                $"{label}: expected {expectedCode}.");
        }
        catch (BridgeCommandException error)
        {
            Require(
                error.Code == expectedCode,
                $"{label}: expected {expectedCode}, got {error.Code}");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
