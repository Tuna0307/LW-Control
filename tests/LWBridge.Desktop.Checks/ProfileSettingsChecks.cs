using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class ProfileSettingsChecks
{
    internal static async Task RunAsync()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "lwbridge-profile-settings-" + Guid.NewGuid().ToString("N"));
        string databasePath = Path.Combine(root, "profile.db");
        const string profileId = "profile-settings-test";

        try
        {
            Directory.CreateDirectory(root);

            using (var stateStore = new ProfileStateStore(databasePath))
            {
                _ = stateStore.Save(
                    profileId,
                    "future_key",
                    0,
                    JsonSerializer.SerializeToElement(
                        new { keep = 7 },
                        JsonOptions.Default));
            }

            using var store = new ProfileSettingsStore(databasePath);
            using var service =
                new ProfileSettingsCommandService(profileId, store);

            ProfileSettingsRecord initial = store.Read();
            Require(initial.Revision == 0, "initial revision is zero");
            Require(
                initial.Value.ValueKind == JsonValueKind.Object &&
                !initial.Value.EnumerateObject().Any(),
                "initial settings value is empty object");
            JsonElement first = await Invoke(
                service,
                new
                {
                    profileId,
                    revision = 0,
                    value = new
                    {
                        locale = "en",
                        nested = new { keep = true },
                    },
                });

            Require(
                first.EnumerateObject().Select(x => x.Name)
                    .SequenceEqual(["revision", "value"]),
                "save result has revision/value fields");
            Require(
                first.GetProperty("revision").GetInt64() == 1,
                "first save increments revision");
            Require(
                first.GetProperty("value")
                    .GetProperty("locale").GetString() == "en",
                "first save returns persisted value");

            ProfileSettingsRecord persisted = store.Read();
            Require(
                persisted.Revision == 1 &&
                persisted.Value.GetProperty("nested")
                    .GetProperty("keep").GetBoolean(),
                "settings persist in profile.db");

            using (var stateStore = new ProfileStateStore(databasePath))
            {
                ProfileStateRecord future =
                    stateStore.Read(profileId, "future_key");
                Require(
                    future.Revision == 1 &&
                    future.Value is JsonElement value &&
                    value.GetProperty("keep").GetInt32() == 7,
                    "profile settings save preserves profile_state data");
            }
            await ExpectCode(
                service,
                new
                {
                    profileId,
                    revision = 0,
                    value = new { stale = true },
                },
                "PROFILE_REVISION_CONFLICT");

            await ExpectCode(
                service,
                new
                {
                    profileId = "missing-profile",
                    revision = 1,
                    value = new { ok = true },
                },
                "PROFILE_NOT_FOUND");

            await ExpectCode(
                service,
                new
                {
                    profileId,
                    revision = 1,
                    value = 5,
                },
                "INVALID_PROFILE_SETTINGS");

            await ExpectCode(
                service,
                new
                {
                    profileId,
                    revision = -1,
                    value = new { ok = true },
                },
                "INVALID_REQUEST");

            await ExpectCode(
                service,
                JsonSerializer.SerializeToElement(
                    new { profileId, value = new { ok = true } },
                    JsonOptions.Default),
                "INVALID_REQUEST");

            var backend = new LWBridgeBackend(
                new LocalConfigStore(persistent: false),
                asyncCommands: service);
            JsonElement backendResult =
                JsonSerializer.SerializeToElement(
                    await backend.InvokeAsync(
                        "profile_settings_save",
                        JsonSerializer.SerializeToElement(
                            new
                            {
                                profileId,
                                revision = 1,
                                value = new { mode = "compact" },
                            },
                            JsonOptions.Default),
                        CancellationToken.None),
                    JsonOptions.Default);

            Require(
                backendResult.GetProperty("revision").GetInt64() == 2,
                "backend routes profile_settings_save");

            await ExpectBackendCode(
                backend,
                new
                {
                    profileId = "another-profile",
                    revision = 2,
                    value = new { mode = "wide" },
                },
                "PROFILE_NOT_FOUND");
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); }
            catch { }
        }
    }

    private static Task<JsonElement> Invoke(
        ProfileSettingsCommandService service,
        object payload) =>
        Invoke(
            service,
            JsonSerializer.SerializeToElement(
                payload,
                JsonOptions.Default));
    private static async Task<JsonElement> Invoke(
        ProfileSettingsCommandService service,
        JsonElement payload)
    {
        object? result = await service.InvokeAsync(
            "profile_settings_save",
            payload,
            CancellationToken.None);
        return JsonSerializer.SerializeToElement(
            result,
            JsonOptions.Default);
    }

    private static async Task ExpectCode(
        ProfileSettingsCommandService service,
        object payload,
        string expectedCode) =>
        await ExpectCode(
            service,
            JsonSerializer.SerializeToElement(
                payload,
                JsonOptions.Default),
            expectedCode);

    private static async Task ExpectCode(
        ProfileSettingsCommandService service,
        JsonElement payload,
        string expectedCode)
    {
        try
        {
            _ = await Invoke(service, payload);
            throw new InvalidOperationException(
                $"Expected profile_settings_save to fail with {expectedCode}.");
        }
        catch (BridgeCommandException error)
        {
            Require(
                error.Code == expectedCode,
                $"expected {expectedCode}, got {error.Code}");
        }
    }
    private static async Task ExpectBackendCode(
        LWBridgeBackend backend,
        object payload,
        string expectedCode)
    {
        try
        {
            _ = await backend.InvokeAsync(
                "profile_settings_save",
                JsonSerializer.SerializeToElement(
                    payload,
                    JsonOptions.Default),
                CancellationToken.None);
            throw new InvalidOperationException(
                $"Expected backend profile_settings_save to fail with {expectedCode}.");
        }
        catch (BridgeCommandException error)
        {
            Require(
                error.Code == expectedCode,
                $"backend expected {expectedCode}, got {error.Code}");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
