using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class Map016LaunchScheduleChecks
{
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("MAP016_LAUNCH_SCHEDULE: " + message);
    }

    internal static async Task RunAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "lwbridge-map016-start-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            CheckSettlementIdentity(root);
            CheckNoGameUiDispatchBarrier();
            await CheckFirstIntervalAndReconnectAsync(root);
            await Map016DataChecks.RunAsync();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
        Console.WriteLine("MAP016_LAUNCH_SCHEDULE_OK");
    }

    private static void CheckNoGameUiDispatchBarrier()
    {
        JsonElement disabled = JsonSerializer.SerializeToElement(new { autoLaunchAll = false });
        JsonElement enabled = JsonSerializer.SerializeToElement(new { autoLaunchAll = true });
        JsonElement missing = JsonSerializer.SerializeToElement(new { });
        Require(LWBridgeWindow.Map016NoGameUiCommandAllowed("profile_instances_reconcile", disabled),
            "a real startup reconcile with native Auto Launch disabled was blocked");
        Require(!LWBridgeWindow.Map016NoGameUiCommandAllowed("profile_instances_reconcile", enabled),
            "an unexpected Auto Launch true reconcile can reach the game launcher");
        Require(!LWBridgeWindow.Map016NoGameUiCommandAllowed("profile_instances_reconcile", missing),
            "missing reconcile flag re-enables the original implicit Auto Launch default");
        Require(LWBridgeWindow.Map016NoGameUiCommandAllowed("server_jump_history_import",
                JsonSerializer.SerializeToElement(new { profileId = "map014-native-ui", history = new[] { 2212 } })),
            "startup local-only saved history migration unexpectedly blocked");
        Require(!LWBridgeWindow.Map016NoGameUiCommandAllowed("server_jump_history_import",
                JsonSerializer.SerializeToElement(new { profileId = "map014-native-ui", history = new[] { -1 } })),
            "invalid saved history migration escaped the read-only proof guard");
        Require(!LWBridgeWindow.Map016NoGameUiCommandAllowed("server_jump_history_import",
                JsonSerializer.SerializeToElement(new { profileId = "other", history = new[] { 2212 } })),
            "foreign profile migration escaped the proof guard");
        foreach (string command in new[]
        {
            "profile_instance_start", "profile_instances_update_and_restart",
            "profile_instance_stop", "map_scan_start", "map_scan_clear",
            "local_map_auto_scan_run_now", "local_map_auto_scan_config_set",
            "server_jump", "map_coordinate_jump", "map_treasure_claim", "game_root_select"
        })
            Require(!LWBridgeWindow.Map016NoGameUiCommandAllowed(command, disabled),
                "no-game UI dispatch admitted a mutation: " + command);
        foreach (string command in new[] { "map_search", "map_saved_servers", "map_data_options",
            "map_scan_status", "local_config_get", "profile_instance_status" })
            Require(LWBridgeWindow.Map016NoGameUiCommandAllowed(command, disabled),
                "saved read-only Map/UI proof command was blocked: " + command);
    }

    private static void CheckSettlementIdentity(string root)
    {
        string marker = Path.Combine(root, "official-settled-client.json");
        string game = Path.Combine(root, "GameInstallation");
        string hash = new('a', 64);
        Require(!OverviewLifecycleService.OfficialSettleMarkerMatches(marker, game, hash),
            "first-use installation skipped official settlement");
        Require(!OverviewLifecycleService.OfficialSettleNoUpdaterProofAllowed(marker, game, hash, false),
            "no-updater packaged proof admitted first use with no genuine settlement");
        void Write(object value) => File.WriteAllText(marker, JsonSerializer.Serialize(value));
        Write(new { schemaVersion = 1, gameRoot = Path.GetFullPath(game), packageSha256 = hash });
        Require(OverviewLifecycleService.OfficialSettleMarkerMatches(marker, game, hash),
            "unchanged admitted installation could not reuse official settlement");
        Require(OverviewLifecycleService.OfficialSettleMarkerMatches(marker, game.ToUpperInvariant(), hash),
            "Windows path-casing difference incorrectly forced a second official launcher cycle");
        Require(OverviewLifecycleService.OfficialSettleNoUpdaterProofAllowed(
                marker, game.ToUpperInvariant(), hash, false),
            "Windows path-casing difference rejected an unchanged settled client");
        Require(OverviewLifecycleService.OfficialSettleNoUpdaterProofAllowed(marker, game, hash, false),
            "matching official-settled package could not safely reuse its existing settlement");
        Require(!OverviewLifecycleService.OfficialSettleNoUpdaterProofAllowed(marker, game, hash, true),
            "forced official launcher restart bypassed no-updater acceptance");
        Require(!OverviewLifecycleService.OfficialSettleMarkerMatches(marker, game, new string('b', 64)),
            "changed original package reused a stale settlement");
        Require(!OverviewLifecycleService.OfficialSettleNoUpdaterProofAllowed(
                marker, game, new string('b', 64), false),
            "changed package started without required official settlement");
        Require(!OverviewLifecycleService.OfficialSettleMarkerMatches(marker, game + "-other", hash),
            "another installation reused the marker");
        Require(!OverviewLifecycleService.OfficialSettleNoUpdaterProofAllowed(
                marker, game + "-other", hash, false),
            "foreign installation was accepted as settled");
        Write(new { schemaVersion = 2, gameRoot = Path.GetFullPath(game), packageSha256 = hash });
        Require(!OverviewLifecycleService.OfficialSettleMarkerMatches(marker, game, hash),
            "unknown marker schema skipped settlement");
        Require(!OverviewLifecycleService.OfficialSettleNoUpdaterProofAllowed(marker, game, hash, false),
            "unknown marker schema bypassed official preflight");
        File.WriteAllText(marker, "{broken");
        Require(!OverviewLifecycleService.OfficialSettleMarkerMatches(marker, game, hash),
            "malformed marker skipped settlement");
        Require(!OverviewLifecycleService.OfficialSettleNoUpdaterProofAllowed(marker, game, hash, false),
            "malformed settlement marker admitted no-updater Launch");
    }

    private static async Task CheckFirstIntervalAndReconnectAsync(string root)
    {
        string statePath = Path.Combine(root, "auto.json");
        Require(MapAutoScanCommandService.NormalizeConfig(MapAutoScanConfig.Default with { ScanMode = "normal" }).ScanMode == "fast",
            "legacy saved Auto normal mode was not migrated to the best automatic method");
        long now = 1_800_000_000_000;
        bool online = false, independentScan = false;
        int starts = 0;
        MapAutoScanCommandService Create() => new(statePath,
            new MapAutoScanExecutionBoundary
            {
                IsOnline = () => online,
                IsMapScanActive = () => independentScan,
                ReadStatusAsync = (run, _) => Task.FromResult(
                    new MapAutoScanRuntimeStatus(2212, false, null, run, run is null ? "idle" : "completed")),
                StartTargetScanAsync = (server, kinds, _, _) =>
                {
                    Require(server == 2212 && kinds.SequenceEqual(["dispatch"]), "Auto changed its saved target/types");
                    return Task.FromResult("map016-run-" + Interlocked.Increment(ref starts));
                },
                ReturnServerAsync = (_, _) => throw new InvalidOperationException("same-server schedule must not navigate"),
                StopScanIfOwnedAsync = (_, _) => throw new InvalidOperationException("completed scan must not Stop"),
            }, new MapAutoScanSchedulerHooks
            {
                UtcNowMilliseconds = () => now,
                DelayAsync = (_, _) => Task.CompletedTask,
            }, startScheduler: false);

        await using (var auto = Create())
        {
            Require(!await auto.CheckDueAsync(), "disabled offline owner scanned");
            MapAutoScanSnapshot enabled = await auto.UpdateConfigAsync(new(true, 20, [2212], ["dispatch"], "fast", false, 0));
            Require(starts == 0 && enabled.Config.NextRunAt == now + 20 * 60_000L,
                "enabling scheduled an immediate first scan");
            now += 25 * 60_000L;
            Require(!await auto.CheckDueAsync(), "offline overdue schedule scanned");
            online = true;
            Require(!await auto.CheckDueAsync(), "connection fired an overdue saved schedule immediately");
            long firstDeadline = (await auto.GetSnapshotAsync()).Config.NextRunAt;
            Require(firstDeadline == now + 20 * 60_000L, "first connected run did not wait a full interval");
            now = firstDeadline - 1;
            Require(!await auto.CheckDueAsync() && starts == 0, "first scheduled run began early");
            now++;
            Require(await auto.CheckDueAsync(), "first scheduled run did not begin at its interval");
            await auto.WaitForIdleAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Require(starts == 1 && (await auto.GetSnapshotAsync()).LastCycle?.Outcome == "completed",
                "interval run did not publish successfully");

            independentScan = true;
            online = false;
            now += 25 * 60_000L;
            Require(!await auto.CheckDueAsync(), "disconnected independent scan admitted Auto");
            online = true;
            Require(!await auto.CheckDueAsync(), "reconnect overrode an independent scan");
            Require((await auto.GetSnapshotAsync()).Config.NextRunAt == now + 20 * 60_000L,
                "reconnect while another scan was active failed to defer the overdue schedule");
            independentScan = false;
            await auto.RunNowAsync();
            await auto.WaitForIdleAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Require(starts == 2, "explicit Run now was delayed by the scheduled interval");

            MapAutoScanSnapshot previous = await auto.GetSnapshotAsync();
            await auto.UpdateConfigAsync(previous.Config with { Enabled = false });
            now += 25 * 60_000L;
            Require(!await auto.CheckDueAsync() && starts == 2, "disabled schedule ran later");
            await auto.UpdateConfigAsync(previous.Config with { Enabled = true });
            Require(starts == 2, "re-enabling started immediately");
        }

        // Reopen a saved enabled schedule after its old deadline, as on owner launch.
        now += 25 * 60_000L;
        await using (var reopened = Create())
        {
            Require(!await reopened.CheckDueAsync() && starts == 2,
                "reopening an overdue enabled schedule scanned on launch");
            Require((await reopened.GetSnapshotAsync()).Config.NextRunAt == now + 20 * 60_000L,
                "reopened schedule did not persist the new connected interval");
        }

        // Run now must also work as the very first online operation of a new owner.
        await using (var explicitOwner = Create())
        {
            await explicitOwner.RunNowAsync();
            await explicitOwner.WaitForIdleAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Require(starts == 3, "first-online explicit Run now was incorrectly deferred");
        }
    }
}
