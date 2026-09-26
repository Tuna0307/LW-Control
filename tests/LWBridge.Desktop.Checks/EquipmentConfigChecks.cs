using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class EquipmentConfigChecks
{
    internal static async Task RunAsync()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "lwbridge-equipment-" + Guid.NewGuid().ToString("N"));
        string runtimeConfigPath = Path.Combine(root, "runtime", "config.json");

        try
        {
            var equipment = new EquipmentConfigCommandService(runtimeConfigPath);

            JsonElement defaults = await Invoke(
                equipment,
                "equipment_config_get",
                new { profileId = "profile-equipment-test" });
            string[] defaultFields = defaults.EnumerateObject()
                .Select(property => property.Name)
                .ToArray();
            Require(
                defaultFields.SequenceEqual(["equipmentPresets"], StringComparer.Ordinal),
                "missing config returns only equipmentPresets");
            Require(
                defaults.GetProperty("equipmentPresets").ValueKind ==
                    JsonValueKind.Array &&
                defaults.GetProperty("equipmentPresets").GetArrayLength() == 0,
                "missing config returns empty equipmentPresets");

            Directory.CreateDirectory(Path.GetDirectoryName(runtimeConfigPath)!);
            File.WriteAllText(
                runtimeConfigPath,
                """
                {
                  "hotkeys": { "attack": true },
                  "visualMetrics": { "showFps": false, "showPing": false },
                  "unknownSibling": { "keep": 7 },
                  "equipmentSchemes": [{ "legacy": true }],
                  "squadEquipmentBindings": { "legacy": true }
                }
                """);

            JsonElement saved = await Invoke(
                equipment,
                "equipment_config_save",
                new
                {
                    profileId = "profile-equipment-test",
                    equipmentPresets = new object[]
                    {
                        new
                        {
                            id = " preset-1 ",
                            name = " First ",
                            squads = new object[]
                            {
                                new
                                {
                                    squadIndex = 1,
                                    positions = Array.Empty<object>(),
                                },
                            },
                        },
                        new
                        {
                            id = "preset-2",
                            name = "Second",
                            arbitraryFutureField = new { keep = true },
                        },
                    },
                    initialEquipmentConfig = new
                    {
                        capturedAt = 123456789L,
                        squads = Array.Empty<object>(),
                    },
                });

            Require(
                saved.GetProperty("equipmentPresets").GetArrayLength() == 2,
                "save returns equipment presets");
            Require(
                saved.TryGetProperty("initialEquipmentConfig", out JsonElement initial) &&
                initial.GetProperty("capturedAt").GetInt64() == 123456789L,
                "save returns initialEquipmentConfig");

            using (JsonDocument document =
                JsonDocument.Parse(File.ReadAllText(runtimeConfigPath)))
            {
                JsonElement rootElement = document.RootElement;
                Require(
                    rootElement.GetProperty("unknownSibling")
                        .GetProperty("keep").GetInt32() == 7,
                    "equipment save preserves unknown sibling");
                Require(
                    rootElement.TryGetProperty("hotkeys", out _),
                    "equipment save preserves Hotkey sibling");
                Require(
                    rootElement.TryGetProperty("visualMetrics", out _),
                    "equipment save preserves visual metrics sibling");
                Require(
                    !rootElement.TryGetProperty("equipmentSchemes", out _),
                    "equipment save removes legacy equipmentSchemes");
                Require(
                    !rootElement.TryGetProperty("squadEquipmentBindings", out _),
                    "equipment save removes legacy squadEquipmentBindings");
                Require(
                    rootElement.GetProperty("equipmentPresets")
                        .GetArrayLength() == 2,
                    "equipment presets persist");
                Require(
                    rootElement.GetProperty("equipmentPresets")[1]
                        .GetProperty("arbitraryFutureField")
                        .GetProperty("keep").GetBoolean(),
                    "nested preset JSON is preserved");
            }

            var reopened = new EquipmentConfigCommandService(runtimeConfigPath);
            JsonElement persisted = await Invoke(
                reopened,
                "equipment_config_get",
                new { profileId = "profile-equipment-test" });
            Require(
                persisted.GetProperty("equipmentPresets").GetArrayLength() == 2,
                "reopened equipment presets persist");
            Require(
                persisted.TryGetProperty("initialEquipmentConfig", out _),
                "reopened initialEquipmentConfig persists");

            JsonElement withoutInitial = await Invoke(
                equipment,
                "equipment_config_save",
                new
                {
                    profileId = "profile-equipment-test",
                    equipmentPresets = new object[]
                    {
                        new { id = "preset-1", name = "First" },
                    },
                });
            Require(
                !withoutInitial.TryGetProperty("initialEquipmentConfig", out _),
                "omitting initialEquipmentConfig removes it from result");

            using (JsonDocument document =
                JsonDocument.Parse(File.ReadAllText(runtimeConfigPath)))
            {
                Require(
                    !document.RootElement.TryGetProperty(
                        "initialEquipmentConfig",
                        out _),
                    "omitting initialEquipmentConfig removes stored member");
            }

            await ExpectError(
                equipment,
                new { profileId = "profile-equipment-test" },
                "INVALID_REQUEST",
                "invalid equipment presets");
            await ExpectError(
                equipment,
                new
                {
                    profileId = "profile-equipment-test",
                    equipmentPresets = new { },
                },
                "INVALID_REQUEST",
                "invalid equipment presets");
            await ExpectError(
                equipment,
                new
                {
                    profileId = "profile-equipment-test",
                    equipmentPresets = new object[] { new { id = "x" } },
                },
                "INVALID_REQUEST",
                "invalid equipment preset");
            await ExpectError(
                equipment,
                new
                {
                    profileId = "profile-equipment-test",
                    equipmentPresets = new object[]
                    {
                        new { id = "  ", name = "Name" },
                    },
                },
                "INVALID_REQUEST",
                "invalid equipment preset");
            await ExpectError(
                equipment,
                new
                {
                    profileId = "profile-equipment-test",
                    equipmentPresets = new object[]
                    {
                        new { id = "same", name = "One" },
                        new { id = "　same　", name = "Two" },
                    },
                },
                "INVALID_REQUEST",
                "invalid equipment preset");

            var backend = new LWBridgeBackend(
                new LocalConfigStore(persistent: false),
                asyncCommands: equipment);
            JsonElement backendGet = JsonSerializer.SerializeToElement(
                await backend.InvokeAsync(
                    "equipment_config_get",
                    JsonSerializer.SerializeToElement(new
                    {
                        profileId = backend.ProfileId,
                    }),
                    CancellationToken.None),
                JsonOptions.Default);
            Require(
                backendGet.GetProperty("equipmentPresets").GetArrayLength() == 1,
                "production backend routes equipment_config_get");

            await ExpectBackendCode(
                backend,
                "equipment_preset_apply",
                new
                {
                    profileId = backend.ProfileId,
                    presetId = "preset-1",
                    squadIndex = 1,
                    operationId = "operation-1",
                },
                "COMMAND_NOT_IMPLEMENTED");
            await ExpectBackendCode(
                backend,
                "equipment_initial_apply",
                new
                {
                    profileId = backend.ProfileId,
                    operationId = "operation-2",
                },
                "COMMAND_NOT_IMPLEMENTED");

            File.WriteAllText(runtimeConfigPath, "{");
            await ExpectGetError(
                equipment,
                "STATE_UNAVAILABLE",
                "config state is unavailable");
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); }
            catch { }
        }
    }

    private static async Task<JsonElement> Invoke(
        EquipmentConfigCommandService service,
        string command,
        object payload)
    {
        JsonElement request =
            JsonSerializer.SerializeToElement(payload, JsonOptions.Default);
        object? result =
            await service.InvokeAsync(command, request, CancellationToken.None);
        return JsonSerializer.SerializeToElement(result, JsonOptions.Default);
    }

    private static async Task ExpectError(
        EquipmentConfigCommandService service,
        object payload,
        string expectedCode,
        string expectedMessage)
    {
        try
        {
            _ = await Invoke(service, "equipment_config_save", payload);
            throw new InvalidOperationException($"Expected {expectedCode}.");
        }
        catch (BridgeCommandException error)
        {
            Require(
                error.Code == expectedCode,
                $"expected {expectedCode}, got {error.Code}");
            Require(
                error.Message == expectedMessage,
                $"expected '{expectedMessage}', got '{error.Message}'");
        }
    }

    private static async Task ExpectGetError(
        EquipmentConfigCommandService service,
        string expectedCode,
        string expectedMessage)
    {
        try
        {
            _ = await Invoke(
                service,
                "equipment_config_get",
                new { profileId = "profile-equipment-test" });
            throw new InvalidOperationException($"Expected {expectedCode}.");
        }
        catch (BridgeCommandException error)
        {
            Require(
                error.Code == expectedCode,
                $"expected {expectedCode}, got {error.Code}");
            Require(
                error.Message == expectedMessage,
                $"expected '{expectedMessage}', got '{error.Message}'");
        }
    }

    private static async Task ExpectBackendCode(
        LWBridgeBackend backend,
        string command,
        object payload,
        string expectedCode)
    {
        try
        {
            _ = await backend.InvokeAsync(
                command,
                JsonSerializer.SerializeToElement(payload, JsonOptions.Default),
                CancellationToken.None);
            throw new InvalidOperationException(
                $"Expected {command} to fail with {expectedCode}.");
        }
        catch (BridgeCommandException error)
        {
            Require(
                error.Code == expectedCode,
                $"{command} expected {expectedCode}, got {error.Code}");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
