using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class GameRootNativeStatusChecks
{
    internal static async Task RunAsync()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "lwbridge-game-root-native-" +
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string saved = CreateNativeRoot(
                Path.Combine(root, "saved"));
            string environment = CreateNativeRoot(
                Path.Combine(root, "environment"));
            string nearby = CreateNativeRoot(
                Path.Combine(root, "nearby"));
            string bridgeFile = CreateNativeRoot(
                Path.Combine(root, "bridge-file"));
            string localAppData = Path.Combine(root, "local-app-data");
            string defaultRoot = CreateNativeRoot(
                Path.Combine(
                    localAppData,
                    "FunFly",
                    "Last War-Survival Game"));
            string processRoot = CreateNativeRoot(
                Path.Combine(root, "process"));
            string registryRoot = CreateNativeRoot(
                Path.Combine(root, "registry"));

            var config = new LocalConfigStore(
                Path.Combine(root, "config"));
            config.Update(c => c with { GameRoot = saved });
            var service = new GameInstallationService(
                config,
                new GameInstallationTestHooks
                {
                    GetEnvironmentVariable =
                        name => name == "LASTWAR_BRIDGE_ROOT"
                            ? environment
                            : null,
                    NearbyRoot = nearby,
                    LocalAppData = localAppData,
                    BridgeRootFileValue = bridgeFile,
                    DiscoveredCandidates =
                    [
                        new GameRootCandidate(
                            Path.Combine(
                                processRoot,
                                "Game",
                                "LastWar.exe"),
                            "process"),
                        new GameRootCandidate(
                            "\"" +
                            Path.Combine(
                                registryRoot,
                                "LastWarLauncher.exe") +
                            "\",0",
                            "registry"),
                    ],
                });

            NativeGameRootStatus status =
                service.GetNativeStatus();

            Require(status.Valid, "native game root status is valid");
            Require(
                SamePath(status.Root, saved) &&
                status.Source == "saved",
                "saved root wins native source ordering");
            Require(
                status.Candidates.Count == 7,
                "native candidate resolver preserves seven distinct valid roots");
            string[] expectedSources =
            [
                "saved",
                "environment",
                "nearby",
                "bridge-root-file",
                "default",
                "process",
                "registry",
            ];
            for (int i = 0; i < expectedSources.Length; i++)
            {
                Require(
                    status.Candidates[i].Source == expectedSources[i],
                    $"candidate {i} source ordering");
            }

            Require(
                SamePath(status.Candidates[5].Path, processRoot),
                "LastWar.exe candidate normalizes to game root");
            Require(
                SamePath(status.Candidates[6].Path, registryRoot),
                "quoted registry icon with comma index walks to game root");

            JsonElement json =
                JsonSerializer.SerializeToElement(
                    status,
                    JsonOptions.Default);
            string[] topLevelNames =
                json.EnumerateObject()
                    .Select(property => property.Name)
                    .ToArray();
            Require(
                topLevelNames.SequenceEqual(
                    new[] { "root", "source", "valid", "candidates" }),
                "public game_root_status has exactly four native fields");
            JsonElement firstCandidate =
                json.GetProperty("candidates")[0];
            Require(
                firstCandidate.EnumerateObject()
                    .Select(property => property.Name)
                    .SequenceEqual(new[] { "path", "source" }),
                "candidate schema is exactly path/source");
            GameRootStatus internalValidation =
                service.Validate(saved, "internal");
            Require(
                !internalValidation.Valid &&
                internalValidation.Error ==
                    "GAME_ROOT_REQUIRED_FILES_MISSING",
                "public native predicate does not weaken internal launch validation");

            var noRoot = new GameInstallationService(
                new LocalConfigStore(
                    Path.Combine(root, "empty-config")),
                new GameInstallationTestHooks
                {
                    GetEnvironmentVariable = _ => null,
                    NearbyRoot = Path.Combine(root, "missing-nearby"),
                    LocalAppData = Path.Combine(root, "missing-local"),
                    DiscoveredCandidates = Array.Empty<GameRootCandidate>(),
                });
            NativeGameRootStatus empty =
                noRoot.GetNativeStatus();
            Require(
                !empty.Valid &&
                empty.Root == string.Empty &&
                empty.Source == string.Empty &&
                empty.Candidates.Count == 0,
                "no native root serializes as empty root/source and empty candidates");

            var duplicateConfig = new LocalConfigStore(
                Path.Combine(root, "duplicate-config"));
            duplicateConfig.Update(
                c => c with { GameRoot = saved });
            var duplicate = new GameInstallationService(
                duplicateConfig,
                new GameInstallationTestHooks
                {
                    GetEnvironmentVariable =
                        _ => Path.Combine(
                            saved,
                            "Game",
                            "LastWar.exe"),
                    NearbyRoot = saved,
                    LocalAppData = Path.Combine(root, "missing-local-2"),
                    BridgeRootFileValue = saved,
                    DiscoveredCandidates = Array.Empty<GameRootCandidate>(),
                });
            NativeGameRootStatus duplicateStatus =
                duplicate.GetNativeStatus();
            Require(
                duplicateStatus.Candidates.Count == 1 &&
                duplicateStatus.Candidates[0].Source == "saved",
                "native candidates deduplicate normalized roots with first source winning");
            var unavailable = new GameInstallationService(
                new LocalConfigStore(persistent: false),
                new GameInstallationTestHooks
                {
                    StateAvailable = false,
                });
            ExpectCode(
                () => unavailable.GetNativeStatus(),
                "STATE_UNAVAILABLE",
                "unavailable path state");

            var backendConfig = new LocalConfigStore(
                Path.Combine(root, "backend-config"));
            backendConfig.Update(
                c => c with { GameRoot = saved });
            var backend = new LWBridgeBackend(backendConfig);
            object? backendResult =
                await backend.InvokeAsync(
                    "game_root_status",
                    JsonSerializer.SerializeToElement(
                        new { },
                        JsonOptions.Default),
                    CancellationToken.None);
            JsonElement backendJson =
                JsonSerializer.SerializeToElement(
                    backendResult,
                    JsonOptions.Default);
            Require(
                backendJson.EnumerateObject()
                    .Select(property => property.Name)
                    .SequenceEqual(
                        new[] { "root", "source", "valid", "candidates" }),
                "backend routes game_root_status to native projection");
            Require(
                backendJson.GetProperty("valid").GetBoolean() &&
                backendJson.GetProperty("source").GetString() == "saved",
                "backend native projection selects persisted root");
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); }
            catch { }
        }
    }

    private static string CreateNativeRoot(string root)
    {
        Directory.CreateDirectory(
            Path.Combine(
                root,
                "Game",
                "LastWar_Data",
                "Plugins",
                "x86_64"));
        Directory.CreateDirectory(Path.Combine(root, "Game"));
        File.WriteAllText(
            Path.Combine(root, "Game", "LastWar.exe"),
            "native-status-fixture");
        return Path.GetFullPath(root);
    }

    private static bool SamePath(string left, string right) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            StringComparison.OrdinalIgnoreCase);
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
