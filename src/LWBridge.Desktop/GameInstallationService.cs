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
    bool? Is64Bit);

internal sealed record GameProcessStatus(
    bool GameRunning,
    bool LauncherRunning,
    int? GamePid,
    int? LauncherPid,
    string? GamePath,
    string? LauncherPath);

internal sealed class GameInstallationService
{
    private readonly LocalConfigStore config;

    public GameInstallationService(LocalConfigStore config) => this.config = config;

    public GameRootStatus GetStatus()
    {
        string? configured = Normalize(config.Snapshot.GameRoot);
        if (configured is not null)
        {
            var saved = Validate(configured, "configured");
            if (saved.Valid) return saved;
        }

        string defaultRoot = Path.Combine(
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
            bool is64 = IsPe64(game) && IsPe64(xlua);
            if (!is64)
                return new(false, root, source, "GAME_ROOT_ARCH_UNSUPPORTED", launcher, game, xlua, false);
            using var _ = File.Open(game, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return new(true, root, source, null, launcher, game, xlua, true);
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
        foreach (Process process in Process.GetProcessesByName(name))
        {
            if (expectedPath is null) return process;
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

    private static bool IsPe64(string path)
    {
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new PEReader(stream, PEStreamOptions.LeaveOpen);
        return reader.PEHeaders.PEHeader?.Magic == PEMagic.PE32Plus;
    }

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
