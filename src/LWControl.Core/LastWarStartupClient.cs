using System.Diagnostics;
using System.Text.Json;

namespace LWControl.Core;

public sealed record LastWarStartupResult(
    string LaunchPath,
    bool GameWasAlreadyRunning,
    bool RuntimeChanged,
    CurrentWorldMapRuntimeInspection WorldScanRuntime,
    CurrentDailyTaskRuntimeInspection DailyTaskRuntime);

public sealed class LastWarStartupClient
{
    private const string InstallerName = "install_daily_task_runtime.py";
    private const string PreparerName = "prepare_daily_task_runtime.py";

    public static string InstallationRoot => Path.Combine(
        RuntimePaths.CanonicalLocalApplicationData, "FunFly", "Last War-Survival Game");

    public static string LauncherPath => Path.Combine(InstallationRoot, "LastWarLauncher.exe");
    public static string GamePath => Path.Combine(InstallationRoot, "Game", "LastWar.exe");

    public async Task<LastWarStartupResult> StartAsync(
        Action<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        bool alreadyRunning = IsGameRunning();
        bool runtimeChanged = false;
        if (!alreadyRunning)
        {
            runtimeChanged = await EnsurePersistentRuntimeAsync(progress, cancellationToken).ConfigureAwait(false);
            progress?.Invoke(runtimeChanged
                ? "Current persistent runtime was installed before launch."
                : "Current persistent runtime is already installed.");
        }
        else
        {
            progress?.Invoke("Last War is already running; checking the loaded persistent runtime.");
        }

        string launchPath = alreadyRunning ? GamePath : StartOfficialGame(progress);
        if (!alreadyRunning)
        {
            var processDeadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(45);
            while (DateTimeOffset.UtcNow < processDeadline && !IsGameRunning())
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(250, cancellationToken).ConfigureAwait(false);
            }
            if (!IsGameRunning())
                throw new InvalidOperationException(
                    "The official Last War launcher opened, but LastWar.exe did not start. Start the game from the launcher, then press Refresh.");
        }

        var worldClient = new CurrentWorldMapScanClient();
        var dailyClient = new CurrentDailyTaskRuntimeClient();
        var heartbeatDeadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(45);
        CurrentWorldMapRuntimeInspection world = worldClient.Inspect();
        CurrentDailyTaskRuntimeInspection daily = dailyClient.Inspect();
        while (DateTimeOffset.UtcNow < heartbeatDeadline && world.StatusCode != "ready")
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
            world = worldClient.Inspect();
            daily = dailyClient.Inspect();
        }
        if (world.StatusCode != "ready")
            throw new InvalidOperationException(
                $"Last War is running, but the persistent World Scan runtime did not become ready ({world.StatusCode}).");

        progress?.Invoke($"Persistent runtime ready via {world.RegistrationMethod ?? "unknown registration route"}.");
        return new(launchPath, alreadyRunning, runtimeChanged, world, daily);
    }

    public (bool GameRunning, CurrentWorldMapRuntimeInspection World, CurrentDailyTaskRuntimeInspection Daily) Inspect()
    {
        return (IsGameRunning(), new CurrentWorldMapScanClient().Inspect(), new CurrentDailyTaskRuntimeClient().Inspect());
    }

    private static string StartOfficialGame(Action<string>? progress)
    {
        string path;
        if (File.Exists(LauncherPath))
            path = LauncherPath;
        else if (File.Exists(GamePath))
            path = GamePath;
        else
            throw new FileNotFoundException(
                "Last War was not found at the recovered FunFly installation path.", LauncherPath);

        _ = Process.Start(new ProcessStartInfo(path)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            ErrorDialog = false,
            WorkingDirectory = Path.GetDirectoryName(path)!,
        }) ?? throw new InvalidOperationException("Windows did not start the Last War process.");
        progress?.Invoke(path == LauncherPath
            ? "Official Last War launcher started."
            : "Official LastWar.exe started because the launcher was not present.");
        return path;
    }

    private static async Task<bool> EnsurePersistentRuntimeAsync(
        Action<string>? progress,
        CancellationToken cancellationToken)
    {
        string? repository = FindRepositoryRoot();
        if (repository is null)
        {
            string manifestPath = Path.Combine(RuntimePaths.LWControlRuntimeDirectory, "daily-task-runtime-install.json");
            if (File.Exists(manifestPath))
                return false;
            throw new InvalidOperationException(
                "The persistent runtime is not installed and the LW-Control tools directory could not be found from this desktop build.");
        }

        string installer = Path.Combine(repository, "tools", InstallerName);
        string preparer = Path.Combine(repository, "tools", PreparerName);
        using JsonDocument statusDocument = await RunPythonJsonAsync(
            installer, ["--status", "--json"], cancellationToken).ConfigureAwait(false);
        JsonElement state = statusDocument.RootElement;
        if (ReadBool(state, "installed")) return false;

        bool runtimeMarker = ReadBool(state, "runtime_entry_present");
        bool worldMarker = ReadBool(state, "world_scan_entry_present");
        bool originalMarker = ReadBool(state, "preserved_original_present");
        bool manifest = ReadBool(state, "manifest_present");
        bool manifestHashes = ReadBool(state, "manifest_hash_match");

        if (manifest && manifestHashes && runtimeMarker && worldMarker && originalMarker)
        {
            progress?.Invoke("A previous exact LW-Control runtime install is present but its payload is stale; restoring its recorded original first.");
            using var _ = await RunPythonJsonAsync(
                installer, ["--uninstall", "--json"], cancellationToken).ConfigureAwait(false);
        }
        else if (manifest || runtimeMarker || worldMarker || originalMarker)
        {
            throw new InvalidOperationException(
                "A partial or changed LW-Control runtime installation is present. Automatic repair was refused because the recorded install cannot be restored exactly.");
        }

        string candidate = Path.Combine(RuntimePaths.CanonicalLocalApplicationData, "LWControl", "candidates",
            $"persistent-runtime-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}");
        progress?.Invoke("Preparing the current verified persistent runtime package.");
        using (var _ = await RunPythonJsonAsync(
                   preparer, ["--prepare-dir", candidate, "--json"], cancellationToken).ConfigureAwait(false)) { }
        progress?.Invoke("Installing the verified persistent runtime while Last War is closed.");
        using (var _ = await RunPythonJsonAsync(
                   installer, ["--install", candidate, "--json"], cancellationToken).ConfigureAwait(false)) { }

        using JsonDocument afterDocument = await RunPythonJsonAsync(
            installer, ["--status", "--json"], cancellationToken).ConfigureAwait(false);
        if (!ReadBool(afterDocument.RootElement, "installed"))
            throw new InvalidOperationException("Persistent runtime installation did not pass its exact post-install inspection.");
        return true;
    }

    private static bool IsGameRunning() => Process.GetProcessesByName("LastWar").Any();

    private static bool ReadBool(JsonElement element, string property) =>
        element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.True;

    private static string? FindRepositoryRoot()
    {
        foreach (string start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            DirectoryInfo? directory = new(Path.GetFullPath(start));
            for (int depth = 0; directory is not null && depth < 10; depth++, directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "tools", InstallerName))
                    && File.Exists(Path.Combine(directory.FullName, "tools", PreparerName)))
                    return directory.FullName;
            }
        }
        return null;
    }

    private static async Task<JsonDocument> RunPythonJsonAsync(
        string script,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo("python")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(script)!,
        };
        start.ArgumentList.Add(script);
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("Could not start Python for the runtime verifier.");
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        string output = await outputTask.ConfigureAwait(false);
        string error = await errorTask.ConfigureAwait(false);
        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"Runtime verifier refused the operation: {FirstUsefulLine(error, output)}");
        try
        {
            return JsonDocument.Parse(output);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Runtime verifier returned invalid JSON.", ex);
        }
    }

    private static string FirstUsefulLine(params string[] values)
    {
        foreach (string value in values)
        {
            string? line = value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim()).FirstOrDefault(item => item.Length > 0);
            if (line is not null) return line.Length <= 500 ? line : line[..500];
        }
        return "unknown runtime-tool failure";
    }
}
