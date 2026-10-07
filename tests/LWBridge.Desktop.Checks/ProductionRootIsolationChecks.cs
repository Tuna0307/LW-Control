using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class ProductionRootIsolationChecks
{
    internal static async Task<JsonElement> RunAsync()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "lwb317-production-root-" + Guid.NewGuid().ToString("N"));
        string ownerRoot = Path.GetFullPath(DesktopApplicationPaths.DefaultRoot);
        DesktopApplicationPaths defaults = DesktopApplicationPaths.Create();
        DesktopApplicationPaths isolated = DesktopApplicationPaths.Create(root);

        Check(
            string.Equals(defaults.Root, ownerRoot, StringComparison.OrdinalIgnoreCase) &&
            !defaults.ExplicitIsolation,
            "no-option paths preserve the normal LocalAppData root");
        Check(
            isolated.ExplicitIsolation &&
            !string.Equals(isolated.Root, ownerRoot, StringComparison.OrdinalIgnoreCase),
            "explicit isolation uses a distinct root");
        CheckAllWithin(isolated);
        ExpectRejected(ownerRoot, "normal root");
        ExpectRejected(Path.Combine(ownerRoot, "nested"), "normal-root child");
        string? ownerParent = Directory.GetParent(ownerRoot)?.FullName;
        if (!string.IsNullOrWhiteSpace(ownerParent))
            ExpectRejected(ownerParent, "normal-root parent");

        const string profileA = "live-pilot-isolation-A";
        const string profileB = "live-pilot-isolation-B";
        var hooks = new GameInstallationTestHooks
        {
            DefaultRoot = Path.Combine(root, "missing-game"),
            GetEnvironmentVariable = _ => null,
            NearbyRoot = Path.Combine(root, "missing-nearby"),
            LocalAppData = Path.Combine(root, "missing-local"),
            DiscoveredCandidates = Array.Empty<GameRootCandidate>(),
        };

        try
        {
            LWBridgeLocalConfig seed = LWBridgeLocalConfig.CreateDefault() with
            {
                ProfileId = profileA,
                AutoLaunchGame = false,
                AutoReconnect = false,
                GameDesiredRunning = false,
            };
            var configA = new LocalConfigStore(isolated.Root, initialValue: seed);
            using var registryStore = new ProfileRegistryStore(isolated.ControllerDatabasePath);
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            registryStore.EnsureLocalProfile(profileA, "Isolation A", now);
            registryStore.EnsureSecondaryProfile(profileB, "Isolation B", 1, now + 1);
            registryStore.SelectProfile(profileA);
            using var registry = new ProfileRegistryCommandService(registryStore, maxProfiles: 2);

            await ExerciseOwnerAsync(profileA, configA, isolated, registry, hooks);

            _ = await registry.InvokeAsync(
                "profile_select",
                JsonSerializer.SerializeToElement(new { profileId = profileB, focusGame = false }),
                CancellationToken.None);
            var configB = new LocalConfigStore(
                Path.Combine(isolated.ProfileRoot(profileB), "local-config"),
                initialValue: seed with { ProfileId = profileB });
            await ExerciseOwnerAsync(profileB, configB, isolated, registry, hooks);

            _ = await registry.InvokeAsync(
                "profile_select",
                JsonSerializer.SerializeToElement(new { profileId = profileA, focusGame = false }),
                CancellationToken.None);
            var reopenedA = new LocalConfigStore(isolated.Root);
            await ExerciseOwnerAsync(profileA, reopenedA, isolated, registry, hooks);

            Check(File.Exists(Path.Combine(isolated.Root, "config.json")),
                "global config persisted under isolated root");
            Check(File.Exists(isolated.ControllerDatabasePath),
                "profile registry persisted under isolated root");
            Check(File.Exists(isolated.ProfileDatabasePath(profileA)),
                "profile A database persisted under isolated root");
            Check(File.Exists(isolated.ProfileDatabasePath(profileB)),
                "profile B database persisted under isolated root");
            Check(File.Exists(isolated.MapDatabasePath(profileA)),
                "profile A Map database persisted under isolated root");
            Check(File.Exists(isolated.MapDatabasePath(profileB)),
                "profile B Map database persisted under isolated root");

            return JsonSerializer.SerializeToElement(new
            {
                ok = true,
                defaultRoot = ownerRoot,
                isolatedRoot = isolated.Root,
                profileSequence = new[] { profileA, profileB, profileA },
                paths = new
                {
                    isolated.ControllerDatabasePath,
                    overviewRuntime = isolated.OverviewRuntimeRoot,
                    overviewEvidence = isolated.OverviewEvidenceRoot,
                    overviewBackups = isolated.OverviewBackupRoot,
                    liveResourceRuntime = isolated.LiveResourceRuntimeRoot,
                    webView = isolated.WebViewUserDataRoot("Presentation"),
                    profileA = isolated.ProfileRoot(profileA),
                    profileB = isolated.ProfileRoot(profileB),
                },
                realCurrentClientMapProvider = true,
                autoSchedulerStarted = false,
                bridgeTransportStarted = false,
            });
        }
        finally
        {
            TryDeleteRoot(root);
            Check(!Directory.Exists(root), "isolated test root cleanup completed");
        }
    }

    private static async Task ExerciseOwnerAsync(
        string profileId,
        LocalConfigStore config,
        DesktopApplicationPaths paths,
        ProfileRegistryCommandService registry,
        GameInstallationTestHooks hooks)
    {
        using ProfileRuntimeOwner owner = ProfileRuntimeOwner.Create(
            profileId,
            config,
            paths.ProfileRoot(profileId),
            registry,
            startAutoScheduler: false,
            startRecoveryMonitor: false,
            startBridgeTransport: false,
            installationTestHooks: hooks,
            applicationDataRoot: paths.Root);

        Check(
            string.Equals(owner.OverviewLifecycle.ApplicationDataRoot, paths.Root, StringComparison.OrdinalIgnoreCase),
            "lifecycle application root is isolated");
        Check(
            string.Equals(owner.OverviewLifecycle.RuntimeRoot, paths.OverviewRuntimeRoot, StringComparison.OrdinalIgnoreCase),
            "lifecycle runtime root is isolated");
        Check(
            string.Equals(owner.OverviewLifecycle.EvidenceRoot, paths.OverviewEvidenceRoot, StringComparison.OrdinalIgnoreCase),
            "lifecycle evidence root is isolated");
        Check(
            string.Equals(owner.OverviewLifecycle.BackupRoot, paths.OverviewBackupRoot, StringComparison.OrdinalIgnoreCase),
            "lifecycle backup root is isolated");
        Check(
            string.Equals(owner.OverviewLifecycle.ProfileRuntimeRoot, paths.ProfileRoot(profileId), StringComparison.OrdinalIgnoreCase),
            "profile runtime root is isolated");

        var source = new CurrentClientMapBlockSource(owner.OverviewLifecycle);
        Check(
            string.Equals(source.OverviewRuntimeRoot, paths.OverviewRuntimeRoot, StringComparison.OrdinalIgnoreCase),
            "real Map provider reads isolated overview runtime");
        Check(
            string.Equals(source.ProbeRuntimeRoot, paths.LiveResourceRuntimeRoot, StringComparison.OrdinalIgnoreCase),
            "real Map provider writes probe runtime only under isolated root");
        Check(
            string.Equals(
                source.AssetCacheRoot,
                Path.Combine(paths.ProfileRoot(profileId), "asset-cache"),
                StringComparison.OrdinalIgnoreCase),
            "real Map provider asset cache is profile-isolated");

        _ = owner.Backend.GetBootstrap(fixture: false, sessionId: "production-root-check", suppressAutoLaunch: true);
        await Task.Yield();
    }

    private static void CheckAllWithin(DesktopApplicationPaths paths)
    {
        string[] routed =
        [
            paths.ControllerDatabasePath,
            paths.OverviewRuntimeRoot,
            paths.OverviewEvidenceRoot,
            paths.OverviewBackupRoot,
            paths.LiveResourceRuntimeRoot,
            paths.LocaleCacheRoot,
            paths.WebViewUserDataRoot("Presentation"),
            paths.ProfileDatabasePath("A"),
            paths.RuntimeConfigPath("A"),
            paths.MapDatabasePath("A"),
        ];
        foreach (string path in routed)
            Check(paths.Contains(path), "routed path escaped isolated root: " + path);
    }

    private static void ExpectRejected(string candidate, string description)
    {
        try
        {
            _ = DesktopApplicationPaths.Create(candidate);
            throw new InvalidOperationException(
                "Production root isolation check failed: accepted " + description);
        }
        catch (ArgumentException)
        {
        }
    }

    private static void TryDeleteRoot(string root)
    {
        for (int attempt = 0; Directory.Exists(root) && attempt < 20; attempt++)
        {
            try
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
                Thread.Sleep(25);
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(25);
            }
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(
                "Production root isolation check failed: " + message);
    }
}
