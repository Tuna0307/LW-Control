using System.Diagnostics;
using System.Reflection.PortableExecutable;
using Microsoft.Win32;

namespace LWBridge.Desktop;

internal sealed record GameRootStatus(
    bool Valid,
    string Path,
    string Source,
    string? Error,
    string? LauncherPath,
    string? GamePath,
    string? XluaPath,
    bool? Is64Bit,
    string? GameMachine = null,
    string? XluaMachine = null);

internal sealed record GameProcessStatus(
    bool GameRunning,
    bool LauncherRunning,
    int? GamePid,
    int? LauncherPid,
    string? GamePath,
    string? LauncherPath);

internal sealed record GameRootCandidate(
    string Path,
    string Source);

internal sealed record NativeGameRootStatus(
    string Root,
    string Source,
    bool Valid,
    IReadOnlyList<GameRootCandidate> Candidates);

internal sealed class GameInstallationTestHooks
{
    public string? DefaultRoot { get; init; }
    public Func<string, Stream>? OpenRead { get; init; }
    public Func<string, string?>? GetEnvironmentVariable { get; init; }
    public string? NearbyRoot { get; init; }
    public string? LocalAppData { get; init; }
    public string? BridgeRootFileValue { get; init; }
    public IReadOnlyList<GameRootCandidate>? DiscoveredCandidates { get; init; }
    public bool StateAvailable { get; init; } = true;
}

internal sealed class GameInstallationService
{
    private readonly LocalConfigStore config;
    private readonly GameInstallationTestHooks? testHooks;

    public GameInstallationService(LocalConfigStore config, GameInstallationTestHooks? testHooks = null)
    {
        this.config = config;
        this.testHooks = testHooks;
    }

    public GameRootStatus GetStatus()
    {
        string? configured = Normalize(config.Snapshot.GameRoot);
        if (configured is not null)
        {
            var saved = Validate(configured, "configured");
            if (saved.Valid) return saved;
        }

        string defaultRoot = testHooks?.DefaultRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FunFly", "Last War-Survival Game");
        var detected = Validate(defaultRoot, "detected");
        if (detected.Valid) return detected;

        if (configured is not null) return Validate(configured, "configured");
        return detected with { Path = string.Empty, Source = "none" };
    }

    public GameRootStatus SaveSelectedRoot(string path)
    {
        var status = Validate(path, "selected");
        if (!status.Valid) return status;
        config.Update(c => c with { GameRoot = status.Path });
        return status with { Source = "configured" };
    }

    public GameRootStatus Validate(string path, string source)
    {
        string? root = Normalize(path);
        if (root is null)
            return new(false, string.Empty, source, "GAME_ROOT_INVALID", null, null, null, null);

        string launcher = Path.Combine(root, "LastWarLauncher.exe");
        string game = Path.Combine(root, "Game", "LastWar.exe");
        string xlua = Path.Combine(root, "Game", "LastWar_Data", "Plugins", "x86_64", "xlua.dll");

        if (!Directory.Exists(root) || !File.Exists(launcher) || !File.Exists(game) || !File.Exists(xlua))
            return new(false, root, source, "GAME_ROOT_REQUIRED_FILES_MISSING", launcher, game, xlua, null);

        try
        {
            PeArchitecture gameArchitecture = ReadArchitecture(game);
            PeArchitecture xluaArchitecture = ReadArchitecture(xlua);
            bool is64 = gameArchitecture.IsAmd64 && xluaArchitecture.IsAmd64;
            if (!is64)
                return new(
                    false,
                    root,
                    source,
                    "GAME_ROOT_ARCH_UNSUPPORTED",
                    launcher,
                    game,
                    xlua,
                    false,
                    gameArchitecture.Display,
                    xluaArchitecture.Display);
            using var _ = OpenRead(game);
            return new(
                true,
                root,
                source,
                null,
                launcher,
                game,
                xlua,
                true,
                gameArchitecture.Display,
                xluaArchitecture.Display);
        }
        catch (UnauthorizedAccessException)
        {
            return new(false, root, source, "GAME_ROOT_PERMISSION_DENIED", launcher, game, xlua, null);
        }
        catch (IOException)
        {
            return new(false, root, source, "GAME_ROOT_UNREADABLE", launcher, game, xlua, null);
        }
        catch (BadImageFormatException)
        {
            return new(false, root, source, "GAME_ROOT_PE_INVALID", launcher, game, xlua, null);
        }
    }

    public NativeGameRootStatus GetNativeStatus()
    {
        if (testHooks?.StateAvailable == false)
        {
            throw new BridgeCommandException(
                "STATE_UNAVAILABLE",
                "path state is unavailable");
        }

        var candidates = new List<GameRootCandidate>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddNativeCandidate(
            candidates,
            seen,
            config.Snapshot.GameRoot,
            "saved");

        string? environmentRoot =
            testHooks?.GetEnvironmentVariable is { } getEnvironmentVariable
                ? getEnvironmentVariable("LASTWAR_BRIDGE_ROOT")
                : Environment.GetEnvironmentVariable("LASTWAR_BRIDGE_ROOT");
        AddNativeCandidate(
            candidates,
            seen,
            environmentRoot,
            "environment");

        string nearbyRoot =
            testHooks?.NearbyRoot ??
            AppContext.BaseDirectory;
        AddNativeCandidate(
            candidates,
            seen,
            nearbyRoot,
            "nearby");

        string? bridgeRootFileValue = testHooks?.BridgeRootFileValue;
        if (bridgeRootFileValue is null)
        {
            bridgeRootFileValue = ReadRootFile(
                Path.Combine(nearbyRoot, "bridge-root.txt"));
        }
        AddNativeCandidate(
            candidates,
            seen,
            bridgeRootFileValue,
            "bridge-root-file");

        string localAppData =
            testHooks?.LocalAppData ??
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            AddNativeCandidate(
                candidates,
                seen,
                Path.Combine(
                    localAppData,
                    "FunFly",
                    "Last War-Survival Game"),
                "default");
        }

        IEnumerable<GameRootCandidate> discovered =
            testHooks?.DiscoveredCandidates ??
            DiscoverNativeCandidates();
        foreach (GameRootCandidate candidate in discovered)
        {
            AddNativeCandidate(
                candidates,
                seen,
                candidate.Path,
                candidate.Source);
        }

        GameRootCandidate? selected =
            candidates.Count == 0 ? null : candidates[0];
        return new NativeGameRootStatus(
            selected?.Path ?? string.Empty,
            selected?.Source ?? string.Empty,
            selected is not null,
            candidates);
    }

    private static void AddNativeCandidate(
        List<GameRootCandidate> candidates,
        HashSet<string> seen,
        string? rawPath,
        string source)
    {
        string? current = NormalizeNativeCandidate(rawPath);
        while (current is not null)
        {
            if (IsNativeRootValid(current))
            {
                if (seen.Add(current))
                    candidates.Add(new GameRootCandidate(current, source));
                return;
            }

            string? parent;
            try
            {
                parent = Directory.GetParent(current)?.FullName;
            }
            catch
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(parent) ||
                string.Equals(
                    parent,
                    current,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            current = Path.TrimEndingDirectorySeparator(parent);
        }
    }

    private static bool IsNativeRootValid(string root)
    {
        try
        {
            return File.Exists(
                       Path.Combine(root, "Game", "LastWar.exe")) &&
                   Directory.Exists(
                       Path.Combine(
                           root,
                           "Game",
                           "LastWar_Data",
                           "Plugins",
                           "x86_64"));
        }
        catch
        {
            return false;
        }
    }

    private static string? NormalizeNativeCandidate(string? rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
            return null;

        string value = rawPath.Trim().Trim('"');

        int comma = value.LastIndexOf(',');
        if (comma > 0 &&
            int.TryParse(
                value[(comma + 1)..].Trim(),
                out _))
        {
            value = value[..comma].Trim().Trim('"');
        }

        try
        {
            string full =
                Path.TrimEndingDirectorySeparator(
                    Path.GetFullPath(value));
            string leaf = Path.GetFileName(full);

            if (string.Equals(
                    leaf,
                    "LastWar.exe",
                    StringComparison.OrdinalIgnoreCase))
            {
                DirectoryInfo? gameDirectory =
                    Directory.GetParent(full);
                DirectoryInfo? rootDirectory =
                    gameDirectory?.Parent;
                if (rootDirectory is not null)
                    full = rootDirectory.FullName;
            }
            else if (string.Equals(
                         leaf,
                         "Game",
                         StringComparison.OrdinalIgnoreCase))
            {
                DirectoryInfo? rootDirectory =
                    Directory.GetParent(full);
                if (rootDirectory is not null)
                    full = rootDirectory.FullName;
            }

            return Path.TrimEndingDirectorySeparator(full);
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadRootFile(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;
            string value = File.ReadAllText(path).Trim();
            return value.Length == 0 ? null : value;
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyList<GameRootCandidate>
        DiscoverNativeCandidates()
    {
        var result = new List<GameRootCandidate>();

        foreach (Process process in Process.GetProcessesByName("LastWar"))
        {
            try
            {
                string? path = SafeProcessPath(process);
                if (!string.IsNullOrWhiteSpace(path))
                    result.Add(new GameRootCandidate(path, "process"));
            }
            finally
            {
                process.Dispose();
            }
        }

        if (!OperatingSystem.IsWindows())
            return result;

        foreach (RegistryHive hive in new[]
                 {
                     RegistryHive.CurrentUser,
                     RegistryHive.LocalMachine,
                 })
        {
            foreach (RegistryView view in new[]
                     {
                         RegistryView.Registry64,
                         RegistryView.Registry32,
                     })
            {
                try
                {
                    using RegistryKey baseKey =
                        RegistryKey.OpenBaseKey(hive, view);
                    using RegistryKey? uninstall =
                        baseKey.OpenSubKey(
                            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                    if (uninstall is null)
                        continue;

                    foreach (string name in uninstall.GetSubKeyNames())
                    {
                        try
                        {
                            using RegistryKey? app =
                                uninstall.OpenSubKey(name);
                            if (app is null)
                                continue;

                            string displayName =
                                app.GetValue("DisplayName") as string ??
                                string.Empty;
                            string installLocation =
                                app.GetValue("InstallLocation") as string ??
                                string.Empty;
                            string displayIcon =
                                app.GetValue("DisplayIcon") as string ??
                                string.Empty;

                            bool relevant =
                                displayName.Contains(
                                    "Last War",
                                    StringComparison.OrdinalIgnoreCase) ||
                                displayName.Contains(
                                    "LastWar",
                                    StringComparison.OrdinalIgnoreCase) ||
                                displayName.Contains(
                                    "FunFly",
                                    StringComparison.OrdinalIgnoreCase) ||
                                installLocation.Contains(
                                    "Last War",
                                    StringComparison.OrdinalIgnoreCase) ||
                                installLocation.Contains(
                                    "LastWar",
                                    StringComparison.OrdinalIgnoreCase) ||
                                installLocation.Contains(
                                    "FunFly",
                                    StringComparison.OrdinalIgnoreCase);

                            if (!relevant)
                                continue;

                            if (!string.IsNullOrWhiteSpace(installLocation))
                            {
                                result.Add(
                                    new GameRootCandidate(
                                        installLocation,
                                        "registry"));
                            }

                            if (!string.IsNullOrWhiteSpace(displayIcon))
                            {
                                result.Add(
                                    new GameRootCandidate(
                                        displayIcon,
                                        "registry"));
                            }
                        }
                        catch
                        {
                        }
                    }
                }
                catch
                {
                }
            }
        }

        return result;
    }

    public GameProcessStatus GetProcessStatus()
    {
        GameRootStatus root = GetStatus();
        string? expectedGame = root.Valid ? root.GamePath : null;
        string? expectedLauncher = root.Valid ? root.LauncherPath : null;
        Process? game = FindMatchingProcess("LastWar", expectedGame);
        Process? launcher = FindMatchingProcess("LastWarLauncher", expectedLauncher);
        try
        {
            return new(
                game is not null,
                launcher is not null,
                game?.Id,
                launcher?.Id,
                SafeProcessPath(game),
                SafeProcessPath(launcher));
        }
        finally
        {
            game?.Dispose();
            launcher?.Dispose();
        }
    }

    private static Process? FindMatchingProcess(string name, string? expectedPath)
    {
        if (expectedPath is null) return null;
        foreach (Process process in Process.GetProcessesByName(name))
        {
            string? actual = SafeProcessPath(process);
            if (actual is not null && PathEquals(actual, expectedPath)) return process;
            process.Dispose();
        }
        return null;
    }

    private static string? SafeProcessPath(Process? process)
    {
        if (process is null) return null;
        try { return process.MainModule?.FileName; }
        catch { return null; }
    }

    private PeArchitecture ReadArchitecture(string path)
    {
        using var stream = OpenRead(path);
        using var reader = new PEReader(stream, PEStreamOptions.LeaveOpen);
        ushort machine = (ushort)reader.PEHeaders.CoffHeader.Machine;
        bool pe32Plus = reader.PEHeaders.PEHeader?.Magic == PEMagic.PE32Plus;
        return new PeArchitecture(
            pe32Plus && machine == 0x8664,
            $"0x{machine:X4}/{reader.PEHeaders.CoffHeader.Machine}/{(pe32Plus ? "PE32+" : "PE32")}");
    }

    private sealed record PeArchitecture(bool IsAmd64, string Display);

    private Stream OpenRead(string path) => testHooks?.OpenRead?.Invoke(path) ??
        File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

    private static string? Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try { return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path.Trim())); }
        catch { return null; }
    }

    private static bool PathEquals(string left, string right) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            StringComparison.OrdinalIgnoreCase);
}
