using System.Diagnostics;
using System.Reflection.PortableExecutable;

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

internal sealed class GameInstallationTestHooks
{
    public string? DefaultRoot { get; init; }
    public Func<string, Stream>? OpenRead { get; init; }
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
