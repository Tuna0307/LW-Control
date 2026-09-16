using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed partial class OverviewLifecycleService
{
    private static readonly TimeSpan OfficialClientSettleTimeout = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan OfficialNormalCloseTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan OfficialProcessPoll = TimeSpan.FromMilliseconds(250);

    // IMPLEMENTATION POLICY LWB-OVR-016: perform the destructive launcher warm-up only
    // once per validated current-client package/root. Repeated launches first run the
    // read-only recovery/compatibility preflight and reuse the persisted settled marker.
    private async Task EnsureOfficialClientSettledAsync(
        string selectedRoot,
        CancellationToken cancellationToken,
        long? overallDeadline = null)
    {
        if (testHooks is not null)
        {
            if (testHooks.RunOfficialRecoverAsync is { } testRecover)
                await testRecover(selectedRoot, cancellationToken).ConfigureAwait(false);
            if (testHooks.RunOfficialSettleAsync is { } testSettle)
                await testSettle(selectedRoot, cancellationToken).ConfigureAwait(false);
            return;
        }
        // Existing isolated lifecycle tests predate the production preflight. They remain
        // deterministic unless they explicitly provide the recovery/settle hooks.
        if (overallDeadline is null)
            throw new InvalidOperationException("Production official settlement requires one overall start deadline.");
        long deadline = overallDeadline.Value;

        string packageSha256 = await RecoverPendingBeforeOfficialSettleAsync(
            selectedRoot, deadline, cancellationToken).ConfigureAwait(false);
        if (OfficialSettleMarkerMatches(selectedRoot, packageSha256))
            return;
        if (RecoveryClockMilliseconds() >= deadline)
            throw new BridgeCommandException("OFFICIAL_SETTLE_TIMEOUT",
                "Pending recovery consumed the bounded Overview start window.");

        string launcherPath = Path.GetFullPath(Path.Combine(selectedRoot, "LastWarLauncher.exe"));
        if (!File.Exists(launcherPath))
            throw new BridgeCommandException("LAUNCHER_NOT_FOUND", "The selected Last War launcher is missing.");
        if (FindSelectedGameProcess(selectedRoot) is not null)
            throw new BridgeCommandException("UNMANAGED_GAME_RUNNING", "Close the game started outside this application first.");
        if (IsUpdateProcessRunning())
            throw new BridgeCommandException("UNMANAGED_LAUNCHER_RUNNING", "Close the existing Last War launcher/updater before starting Overview.");

        var start = new ProcessStartInfo
        {
            FileName = launcherPath,
            WorkingDirectory = selectedRoot,
            UseShellExecute = true,
        };
        using Process launcher = Process.Start(start)
            ?? throw new BridgeCommandException("LAUNCH_FAILED", "The official Last War launcher could not be started.");
        string launcherStartedAtUtc = launcher.StartTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

        OfficialProcessIdentity? game = null;
        while (RecoveryClockMilliseconds() < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            game = ReadSingleSelectedGameIdentity(selectedRoot);
            if (game is not null) break;
            if (launcher.HasExited && !IsUpdateProcessRunning())
                throw new BridgeCommandException("LAUNCH_FAILED", "The official launcher exited before starting the selected game.");
            await RecoveryDelayAsync(OfficialProcessPoll, cancellationToken).ConfigureAwait(false);
        }
        if (game is null)
            throw new BridgeCommandException("OFFICIAL_SETTLE_TIMEOUT", "The official launcher did not finish updating and start the selected game before timeout.");

        await CloseOfficialGameNormallyAsync(game, deadline, cancellationToken).ConfigureAwait(false);
        await FinishOwnedLauncherAsync(
            launcher,
            launcherPath,
            launcherStartedAtUtc,
            deadline,
            cancellationToken).ConfigureAwait(false);
        while (IsUpdateProcessRunning() && RecoveryClockMilliseconds() < deadline)
            await RecoveryDelayAsync(OfficialProcessPoll, cancellationToken).ConfigureAwait(false);
        if (IsUpdateProcessRunning())
            throw new BridgeCommandException("OFFICIAL_SETTLE_TIMEOUT", "The official launcher/updater did not settle before the Overview install phase.");
        if (ReadSingleSelectedGameIdentity(selectedRoot) is not null)
            throw new BridgeCommandException("OFFICIAL_SETTLE_GAME_CLOSE_FAILED", "The official preflight game is still running after normal close.");

        string settledPackageSha256 = await RecoverPendingBeforeOfficialSettleAsync(
            selectedRoot, deadline, cancellationToken).ConfigureAwait(false);
        WriteOfficialSettleMarker(selectedRoot, settledPackageSha256);
    }

    private async Task<string> RecoverPendingBeforeOfficialSettleAsync(
        string selectedRoot,
        long deadline,
        CancellationToken cancellationToken)
    {
        if (FindSelectedGameProcess(selectedRoot) is not null)
            throw new BridgeCommandException("UNMANAGED_GAME_RUNNING", "Close the game started outside this application first.");
        long remainingMilliseconds = deadline - RecoveryClockMilliseconds();
        if (remainingMilliseconds <= 0)
            throw new BridgeCommandException("OFFICIAL_SETTLE_TIMEOUT", "No bounded start window remained for pending recovery.");

        JsonElement result = await RunHelperAsync(
            new OverviewHelperInvocation(
                "preflight-recover", profileId, null, null, null, null, null,
                SupervisionMilliseconds: (int)Math.Min(int.MaxValue, remainingMilliseconds)),
            cancellationToken).ConfigureAwait(false);
        RequireString(result, "mode", "overview_preflight_recover");
        if (!result.TryGetProperty("installedFilesChanged", out JsonElement changed) || changed.ValueKind != JsonValueKind.False)
            throw new InvalidDataException("Overview preflight recovery reported changed installed files.");
        if (!result.TryGetProperty("currentClient", out JsonElement current) || current.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Overview preflight recovery did not return current-client compatibility evidence.");
        return CurrentClientCompatibility.ValidateCurrentClient(current);
    }

    private string OfficialSettleMarkerPath => Path.Combine(evidenceRoot, "official-settled-client.json");

    private bool OfficialSettleMarkerMatches(string selectedRoot, string packageSha256)
    {
        try
        {
            using JsonDocument marker = JsonDocument.Parse(File.ReadAllBytes(OfficialSettleMarkerPath));
            JsonElement root = marker.RootElement;
            return MatchesInt(root, "schemaVersion", 1) &&
                   MatchesString(root, "gameRoot", Path.GetFullPath(selectedRoot)) &&
                   MatchesString(root, "packageSha256", packageSha256);
        }
        catch { return false; }
    }

    private void WriteOfficialSettleMarker(string selectedRoot, string packageSha256)
    {
        Directory.CreateDirectory(evidenceRoot);
        File.WriteAllText(OfficialSettleMarkerPath, JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            gameRoot = Path.GetFullPath(selectedRoot),
            packageSha256,
            settledAtUtc = RecoveryNow().ToString("O", CultureInfo.InvariantCulture),
        }, JsonOptions.Default));
    }

    private static OfficialProcessIdentity? ReadSingleSelectedGameIdentity(string selectedRoot)
    {
        string expectedPath = Path.GetFullPath(Path.Combine(selectedRoot, "Game", "LastWar.exe"));
        var matches = new List<OfficialProcessIdentity>();
        foreach (Process process in Process.GetProcessesByName("LastWar"))
        {
            using (process)
            {
                try
                {
                    if (process.HasExited) continue;
                    string? actualPath = process.MainModule?.FileName;
                    if (actualPath is null || !PathEquals(actualPath, expectedPath)) continue;
                    matches.Add(new OfficialProcessIdentity(
                        process.Id,
                        Path.GetFullPath(actualPath),
                        process.StartTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)));
                }
                catch { }
            }
        }
        if (matches.Count > 1)
            throw new BridgeCommandException("MULTIPLE_GAME_PROCESSES", "Multiple Last War processes match the selected installation.");
        return matches.Count == 1 ? matches[0] : null;
    }

    private async Task CloseOfficialGameNormallyAsync(
        OfficialProcessIdentity game,
        long deadline,
        CancellationToken cancellationToken)
    {
        if (!ProcessMatches(game.Pid, game.Path, game.StartedAtUtc))
            throw new BridgeCommandException("PROCESS_IDENTITY_CHANGED", "The official preflight game identity changed before normal close.");
        using Process process = Process.GetProcessById(game.Pid);
        while (!process.HasExited && process.MainWindowHandle == IntPtr.Zero && RecoveryClockMilliseconds() < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            process.Refresh();
            await RecoveryDelayAsync(OfficialProcessPoll, cancellationToken).ConfigureAwait(false);
        }
        if (process.HasExited)
            throw new BridgeCommandException("OFFICIAL_SETTLE_GAME_EXITED", "The official preflight game exited before it could be normally closed.");
        if (!ProcessMatches(game.Pid, game.Path, game.StartedAtUtc))
            throw new BridgeCommandException("PROCESS_IDENTITY_CHANGED", "The official preflight game identity changed before normal close.");
        if (!process.CloseMainWindow())
            throw new BridgeCommandException("OFFICIAL_SETTLE_GAME_CLOSE_FAILED", "The official preflight game did not accept a normal main-window close.");

        TimeSpan wait = RemainingOfficialSettle(deadline, OfficialNormalCloseTimeout);
        if (wait <= TimeSpan.Zero)
            throw new BridgeCommandException("OFFICIAL_SETTLE_TIMEOUT", "No time remained for the official preflight game to close.");
        Task exit = process.WaitForExitAsync(CancellationToken.None);
        Task timeout = RecoveryDelayAsync(wait, cancellationToken);
        if (!ReferenceEquals(await Task.WhenAny(exit, timeout).ConfigureAwait(false), exit))
            throw new BridgeCommandException("OFFICIAL_SETTLE_GAME_CLOSE_FAILED", "The official preflight game did not exit after normal close.");
        await exit.ConfigureAwait(false);
    }

    private async Task FinishOwnedLauncherAsync(
        Process launcher,
        string launcherPath,
        string launcherStartedAtUtc,
        long deadline,
        CancellationToken cancellationToken)
    {
        if (launcher.HasExited) return;
        TimeSpan grace = RemainingOfficialSettle(deadline, TimeSpan.FromSeconds(3));
        if (grace > TimeSpan.Zero)
        {
            Task naturalExit = launcher.WaitForExitAsync(CancellationToken.None);
            Task graceDelay = RecoveryDelayAsync(grace, cancellationToken);
            if (ReferenceEquals(await Task.WhenAny(naturalExit, graceDelay).ConfigureAwait(false), naturalExit))
            {
                await naturalExit.ConfigureAwait(false);
                return;
            }
            cancellationToken.ThrowIfCancellationRequested();
        }

        if (!ProcessMatches(launcher.Id, launcherPath, launcherStartedAtUtc))
            throw new BridgeCommandException("PROCESS_IDENTITY_CHANGED", "The official launcher identity changed before normal close.");
        launcher.Refresh();
        if (!launcher.CloseMainWindow())
            throw new BridgeCommandException("OFFICIAL_SETTLE_LAUNCHER_CLOSE_FAILED", "The helper-owned official launcher did not accept a normal close.");

        TimeSpan wait = RemainingOfficialSettle(deadline, OfficialNormalCloseTimeout);
        if (wait <= TimeSpan.Zero)
            throw new BridgeCommandException("OFFICIAL_SETTLE_TIMEOUT", "No time remained for the official launcher to close.");
        Task exit = launcher.WaitForExitAsync(CancellationToken.None);
        Task timeout = RecoveryDelayAsync(wait, cancellationToken);
        if (!ReferenceEquals(await Task.WhenAny(exit, timeout).ConfigureAwait(false), exit))
            throw new BridgeCommandException("OFFICIAL_SETTLE_LAUNCHER_CLOSE_FAILED", "The helper-owned official launcher did not exit after normal close.");
        await exit.ConfigureAwait(false);
    }

    private TimeSpan RemainingOfficialSettle(long deadline, TimeSpan cap)
    {
        long remainingMilliseconds = deadline - RecoveryClockMilliseconds();
        if (remainingMilliseconds <= 0) return TimeSpan.Zero;
        double capped = Math.Min(remainingMilliseconds, cap.TotalMilliseconds);
        return TimeSpan.FromMilliseconds(Math.Max(1, capped));
    }

    private sealed record OfficialProcessIdentity(
        int Pid,
        string Path,
        string StartedAtUtc);
}
