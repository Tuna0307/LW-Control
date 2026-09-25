using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class ProxyStatusChecks
{
    internal static async Task RunAsync()
    {
        string temp = Path.Combine(
            Path.GetTempPath(),
            "lwbridge-proxy-status-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            string root = CreateNativeRoot(Path.Combine(temp, "install-root"));
            string plugins = Path.Combine(
                root, "Game", "LastWar_Data", "Plugins", "x86_64");
            string target = Path.Combine(plugins, "xlua.dll");
            string original = Path.Combine(plugins, "xlua_.dll");
            File.WriteAllText(target, "proxy-target");
            File.WriteAllText(original, "original-target");

            string resources = Path.Combine(temp, "resources");
            Directory.CreateDirectory(resources);
            File.WriteAllText(
                Path.Combine(resources, "xlua-proxy-bundle.json"), "{}");
            File.WriteAllText(
                Path.Combine(resources, "xlua-proxy-secure.dll"), "secure");
            File.WriteAllText(
                Path.Combine(resources, "xlua-proxy-plain.dll"), "plain");
            string targetHash = "SECURE";
            bool gameRunning = false;
            bool runtimeManaged = false;
            var config = new LocalConfigStore(Path.Combine(temp, "config"));
            config.Update(c => c with { GameRoot = root });
            GameInstallationTestHooks installationHooks =
                CreateInstallationHooks(temp);
            NativeGameRootStatus nativeRoot =
                new GameInstallationService(config, installationHooks)
                    .GetNativeStatus();
            Require(
                nativeRoot.Valid,
                $"proxy fixture root was not selected: saved={config.Snapshot.GameRoot}, root={nativeRoot.Root}");
            var proxyHooks = new ProxyStatusTestHooks
            {
                ResourceDirectory = resources,
                ComputeSha256 = path => Path.GetFileName(path) switch
                {
                    "xlua-proxy-secure.dll" => "SECURE",
                    "xlua-proxy-plain.dll" => "PLAIN",
                    "xlua.dll" => targetHash,
                    _ => null,
                },
                GameRunning = () => gameRunning,
                RuntimeManaged = () => runtimeManaged,
            };
            string runtime = Path.Combine(temp, "runtime");
            var backend = new LWBridgeBackend(
                config,
                profileRuntimeDirectory: runtime,
                installationTestHooks: installationHooks,
                proxyStatusTestHooks: proxyHooks);
            using JsonDocument payload = JsonDocument.Parse(
                JsonSerializer.Serialize(new { profileId = config.Snapshot.ProfileId }));

            JsonElement installed = await InvokeJsonAsync(
                backend, payload.RootElement.Clone());
            RequireFields(
                installed,
                "state", "installed", "resourceAvailable",
                "targetExists", "originalExists", "installedMode",
                "gameRunning", "targetPath", "runtimeManaged",
                "repairRequired");
            Require(installed.GetProperty("state").GetString() == "installed",
                "secure target reports installed state");
            Require(installed.GetProperty("installed").GetBoolean(),
                "secure target reports installed=true");
            Require(installed.GetProperty("resourceAvailable").GetBoolean(),
                "verified resource set is available");
            Require(installed.GetProperty("installedMode").GetString() == "secure",
                "secure target reports secure mode");
            Require(
                SamePath(installed.GetProperty("targetPath").GetString()!, target),
                "targetPath is the recovered x86_64/xlua.dll path");
            Require(!installed.GetProperty("repairRequired").GetBoolean(),
                "healthy stopped proxy does not require repair");

            File.Delete(original);
            gameRunning = true;
            JsonElement missingOriginal = await InvokeJsonAsync(
                backend, payload.RootElement.Clone());
            Require(
                missingOriginal.GetProperty("state").GetString() == "needsRepair" &&
                !missingOriginal.GetProperty("installed").GetBoolean() &&
                missingOriginal.GetProperty("installedMode").GetString() == "secure",
                "missing xlua_.dll reports needsRepair without losing mode");
            Require(missingOriginal.GetProperty("repairRequired").GetBoolean(),
                "unmanaged running game plus needsRepair requires repair");

            runtimeManaged = true;
            JsonElement managedRepair = await InvokeJsonAsync(
                backend, payload.RootElement.Clone());
            Require(!managedRepair.GetProperty("repairRequired").GetBoolean(),
                "managed runtime suppresses repairRequired projection");

            runtimeManaged = false;
            File.WriteAllText(original, "original-target");
            targetHash = "UNKNOWN";
            JsonElement unknownTarget = await InvokeJsonAsync(
                backend, payload.RootElement.Clone());
            Require(
                unknownTarget.GetProperty("state").GetString() == "needsRepair" &&
                unknownTarget.GetProperty("installedMode").ValueKind ==
                    JsonValueKind.Null,
                "unrecognized target hash reports needsRepair and null mode");

            targetHash = "PLAIN";
            gameRunning = false;
            JsonElement plain = await InvokeJsonAsync(
                backend, payload.RootElement.Clone());
            Require(
                plain.GetProperty("state").GetString() == "installed" &&
                plain.GetProperty("installedMode").GetString() == "plain",
                "plain target hash reports installed plain mode");

            File.Delete(target);
            JsonElement targetMissing = await InvokeJsonAsync(
                backend, payload.RootElement.Clone());
            Require(
                targetMissing.GetProperty("state").GetString() == "targetMissing" &&
                !targetMissing.GetProperty("targetExists").GetBoolean() &&
                targetMissing.GetProperty("installedMode").ValueKind ==
                    JsonValueKind.Null,
                "missing xlua.dll reports targetMissing");
            File.WriteAllText(target, "proxy-target");
            var noResourceHooks = new ProxyStatusTestHooks
            {
                ResourceDirectory = Path.Combine(temp, "missing-resources"),
                ComputeSha256 = proxyHooks.ComputeSha256,
                GameRunning = () => false,
                RuntimeManaged = () => false,
            };
            var noResourceBackend = new LWBridgeBackend(
                config,
                profileRuntimeDirectory: runtime,
                installationTestHooks: installationHooks,
                proxyStatusTestHooks: noResourceHooks);
            JsonElement resourceMissing = await InvokeJsonAsync(
                noResourceBackend, payload.RootElement.Clone());
            Require(
                resourceMissing.GetProperty("state").GetString() ==
                    "resourceMissing" &&
                !resourceMissing.GetProperty("resourceAvailable").GetBoolean(),
                "missing proxy resource set reports resourceMissing");

            var noRootConfig = new LocalConfigStore(
                Path.Combine(temp, "no-root-config"));
            var reducedBackend = new LWBridgeBackend(
                noRootConfig,
                profileRuntimeDirectory: runtime,
                installationTestHooks: CreateInstallationHooks(temp),
                proxyStatusTestHooks: proxyHooks);
            using JsonDocument reducedPayload = JsonDocument.Parse(
                JsonSerializer.Serialize(
                    new { profileId = noRootConfig.Snapshot.ProfileId }));
            JsonElement reduced = await InvokeJsonAsync(
                reducedBackend, reducedPayload.RootElement.Clone());
            RequireFields(
                reduced,
                "state", "installed", "resourceAvailable",
                "targetExists", "originalExists", "gameRunning",
                "targetPath", "runtimeManaged", "repairRequired");
            Require(!reduced.TryGetProperty("installedMode", out _),
                "no-launch-component result omits installedMode");
            Require(
                reduced.GetProperty("state").GetString() == "targetMissing" &&
                reduced.GetProperty("targetPath").GetString() == string.Empty &&
                !reduced.GetProperty("gameRunning").GetBoolean(),
                "no-launch-component result is targetMissing/empty/false");

            using JsonDocument empty = JsonDocument.Parse("{}");
            await ExpectErrorAsync(
                () => backend.InvokeAsync(
                    "proxy_status", empty.RootElement.Clone(),
                    CancellationToken.None),
                "PROFILE_ID_REQUIRED",
                "missing profileId");
            using JsonDocument wrongType = JsonDocument.Parse(
                """{"profileId":123}""");
            await ExpectErrorAsync(
                () => backend.InvokeAsync(
                    "proxy_status", wrongType.RootElement.Clone(),
                    CancellationToken.None),
                "PROFILE_ID_REQUIRED",
                "non-string profileId");
            using JsonDocument foreign = JsonDocument.Parse(
                """{"profileId":"foreign"}""");
            await ExpectErrorAsync(
                () => backend.InvokeAsync(
                    "proxy_status", foreign.RootElement.Clone(),
                    CancellationToken.None),
                "PROFILE_RUNTIME_UNAVAILABLE",
                "foreign profile runtime");
            var unavailableRuntimeBackend = new LWBridgeBackend(
                config,
                profileRuntimeDirectory: null,
                installationTestHooks: installationHooks,
                proxyStatusTestHooks: proxyHooks);
            await ExpectErrorAsync(
                () => unavailableRuntimeBackend.InvokeAsync(
                    "proxy_status", payload.RootElement.Clone(),
                    CancellationToken.None),
                "PROFILE_RUNTIME_UNAVAILABLE",
                "missing profile runtime");

            var unavailableStateBackend = new LWBridgeBackend(
                config,
                profileRuntimeDirectory: runtime,
                installationTestHooks: new GameInstallationTestHooks
                {
                    StateAvailable = false,
                    GetEnvironmentVariable = _ => null,
                },
                proxyStatusTestHooks: proxyHooks);
            await ExpectErrorAsync(
                () => unavailableStateBackend.InvokeAsync(
                    "proxy_status", payload.RootElement.Clone(),
                    CancellationToken.None),
                "STATE_UNAVAILABLE",
                "unavailable path state");
        }
        finally
        {
            try { Directory.Delete(temp, recursive: true); }
            catch { }
        }
    }
    private static GameInstallationTestHooks CreateInstallationHooks(
        string temp) => new()
    {
        GetEnvironmentVariable = _ => null,
        NearbyRoot = Path.Combine(temp, "missing-nearby"),
        LocalAppData = Path.Combine(temp, "missing-local"),
        DiscoveredCandidates = Array.Empty<GameRootCandidate>(),
    };

    private static string CreateNativeRoot(string root)
    {
        string plugins = Path.Combine(
            root, "Game", "LastWar_Data", "Plugins", "x86_64");
        Directory.CreateDirectory(plugins);
        Directory.CreateDirectory(Path.Combine(root, "Game"));
        File.WriteAllText(
            Path.Combine(root, "Game", "LastWar.exe"),
            "proxy-status-fixture");
        return Path.GetFullPath(root);
    }

    private static async Task<JsonElement> InvokeJsonAsync(
        LWBridgeBackend backend,
        JsonElement payload)
    {
        object? value = await backend.InvokeAsync(
            "proxy_status", payload, CancellationToken.None);
        return JsonSerializer.SerializeToElement(value, JsonOptions.Default);
    }
    private static async Task ExpectErrorAsync(
        Func<Task<object?>> action,
        string expectedCode,
        string label)
    {
        try
        {
            await action();
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

    private static void RequireFields(
        JsonElement value,
        params string[] expected)
    {
        string[] actual = value.EnumerateObject()
            .Select(property => property.Name)
            .ToArray();
        Require(
            actual.SequenceEqual(expected),
            $"proxy_status fields mismatch: {string.Join(",", actual)}");
    }

    private static bool SamePath(string left, string right) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            StringComparison.OrdinalIgnoreCase);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
