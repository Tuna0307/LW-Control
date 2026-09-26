using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class GameRootSelectChecks
{
    internal static async Task RunAsync()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "lwbridge-game-root-select-" +
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var config = new LocalConfigStore(
                Path.Combine(root, "config"));
            string previous = CreateNativeRoot(
                Path.Combine(root, "previous"));
            config.Update(c => c with { GameRoot = previous });

            var service = new GameInstallationService(
                config,
                new GameInstallationTestHooks
                {
                    GetEnvironmentVariable = _ => null,
                    NearbyRoot = Path.Combine(root, "missing-nearby"),
                    LocalAppData = Path.Combine(root, "missing-local"),
                    DiscoveredCandidates = Array.Empty<GameRootCandidate>(),
                });

            NativeGameRootSelectionResult canceled =
                service.CreateNativeCanceledSelection();
            JsonElement canceledJson =
                JsonSerializer.SerializeToElement(
                    canceled,
                    JsonOptions.Default);
            Require(
                canceledJson.EnumerateObject()
                    .Select(property => property.Name)
                    .SequenceEqual(
                        new[] { "canceled", "path", "valid" }),
                "game_root_select result has exactly canceled/path/valid");
            Require(
                canceledJson.GetProperty("canceled").GetBoolean() &&
                canceledJson.GetProperty("path").ValueKind ==
                    JsonValueKind.Null &&
                !canceledJson.GetProperty("valid").GetBoolean(),
                "cancel returns true/null/false");
            string invalidPath =
                Path.Combine(root, "invalid-selected");
            Directory.CreateDirectory(invalidPath);
            NativeGameRootSelectionResult invalid =
                service.SaveNativeSelection(invalidPath);
            Require(
                !invalid.Canceled &&
                !invalid.Valid &&
                SamePath(invalid.Path!, invalidPath),
                "ordinary invalid selection returns false/path/false");
            Require(
                SamePath(config.Snapshot.GameRoot!, previous),
                "ordinary invalid selection does not replace saved root");

            string selected = CreateNativeRoot(
                Path.Combine(root, "selected"));
            NativeGameRootSelectionResult valid =
                service.SaveNativeSelection(selected);
            Require(
                !valid.Canceled &&
                valid.Valid &&
                SamePath(valid.Path!, selected),
                "native-minimal selected root returns valid result");
            Require(
                SamePath(config.Snapshot.GameRoot!, selected),
                "valid native selection persists the normalized root");

            GameRootStatus internalValidation =
                service.Validate(selected, "internal");
            Require(
                !internalValidation.Valid &&
                internalValidation.Error ==
                    "GAME_ROOT_REQUIRED_FILES_MISSING",
                "public selection predicate stays separate from launch admission");

            NativeGameRootSelectionResult fromGame =
                service.SaveNativeSelection(
                    Path.Combine(selected, "Game"));
            Require(
                fromGame.Valid &&
                SamePath(fromGame.Path!, selected),
                "selected Game directory normalizes to installation root");

            NativeGameRootSelectionResult fromExe =
                service.SaveNativeSelection(
                    Path.Combine(
                        selected,
                        "Game",
                        "LastWar.exe"));
            Require(
                fromExe.Valid &&
                SamePath(fromExe.Path!, selected),
                "selected LastWar.exe path normalizes to installation root");
            ExpectError(
                () => service.SaveNativeSelection("bad\0path"),
                "INVALID_GAME_ROOT",
                "select the folder containing Game\\LastWar.exe",
                "un-normalizable selected path");

            string unavailableRoot = CreateNativeRoot(
                Path.Combine(root, "unavailable"));
            var unavailableConfig = new LocalConfigStore(
                Path.Combine(root, "unavailable-config"));
            unavailableConfig.Update(
                c => c with { GameRoot = previous });
            var unavailable = new GameInstallationService(
                unavailableConfig,
                new GameInstallationTestHooks
                {
                    StateAvailable = false,
                    GetEnvironmentVariable = _ => null,
                });
            ExpectError(
                () => unavailable.SaveNativeSelection(
                    unavailableRoot),
                "STATE_UNAVAILABLE",
                "path state is unavailable",
                "unavailable path state");
            Require(
                SamePath(
                    unavailableConfig.Snapshot.GameRoot!,
                    previous),
                "state-unavailable selection does not persist");

            var backendConfig = new LocalConfigStore(
                Path.Combine(root, "backend-config"));
            backendConfig.Update(c => c with { GameRoot = previous });
            var backend = new LWBridgeBackend(backendConfig);

            NativeGameRootSelectionResult backendCanceled =
                backend.CreateGameRootSelectionCanceled();
            Require(
                backendCanceled.Canceled &&
                backendCanceled.Path is null &&
                !backendCanceled.Valid,
                "backend cancel helper preserves native envelope");

            NativeGameRootSelectionResult backendInvalid =
                backend.SaveNativeGameRootSelection(invalidPath);
            Require(
                !backendInvalid.Canceled &&
                !backendInvalid.Valid &&
                SamePath(backendInvalid.Path!, invalidPath) &&
                SamePath(
                    backendConfig.Snapshot.GameRoot!,
                    previous),
                "backend invalid selection is a normal no-save result");
            NativeGameRootSelectionResult backendValid =
                backend.SaveNativeGameRootSelection(selected);
            Require(
                backendValid.Valid &&
                SamePath(backendValid.Path!, selected) &&
                SamePath(
                    backendConfig.Snapshot.GameRoot!,
                    selected),
                "backend valid selection persists selected root");

            await Task.CompletedTask;
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
            "native-select-fixture");
        return Path.GetFullPath(root);
    }

    private static bool SamePath(string left, string right) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            StringComparison.OrdinalIgnoreCase);
    private static void ExpectError(
        Action action,
        string expectedCode,
        string expectedMessage,
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
            Require(
                error.Message == expectedMessage,
                $"{label}: expected message '{expectedMessage}', got '{error.Message}'");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
