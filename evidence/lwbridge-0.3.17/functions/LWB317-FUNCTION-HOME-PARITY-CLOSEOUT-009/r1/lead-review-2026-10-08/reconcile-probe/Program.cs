using System.Text.Json;
using LWBridge.Desktop;

// Independent lead probe of the actual production reconcile handler. All process/helper
// boundaries are inert; all configuration and runtime roots are explicit temporary paths.
var output = new List<object>();
string root = Path.Combine(Path.GetTempPath(), "home009-r1-lead-reconcile-" + Guid.NewGuid().ToString("N"));
try
{
    foreach (bool local in new[] { false, true })
    foreach (bool intent in new[] { false, true })
    {
        string gameRoot = Path.Combine(root, $"local-{local}-intent-{intent}");
        Directory.CreateDirectory(Path.Combine(gameRoot, "Game"));
        File.WriteAllBytes(Path.Combine(gameRoot, "Game", "LastWar.exe"), [0x4d, 0x5a]);
        var config = new LocalConfigStore(Path.Combine(gameRoot, "config"));
        config.Update(c => c with { ProfileId = "lead-reconcile", GameRoot = gameRoot, AutoLaunchGame = local });
        int calls = 0;
        var hooks = new OverviewLifecycleTestHooks
        {
            SelectedGamePids = _ => Array.Empty<int>(),
            RunOfficialRecoverAsync = (_, _) => Task.CompletedTask,
            RunOfficialSettleAsync = (_, _) => Task.CompletedTask,
            RunHelperAsync = (_, _) => { calls++; throw new BridgeCommandException("INERT_LEAD_REJECTION", "no process can start"); },
            ProcessMatches = (_, _, _) => false,
            DeleteFile = _ => { },
        };
        using var lifecycle = new OverviewLifecycleService(
            "lead-reconcile", gameRoot, helperPath: Path.Combine(gameRoot, "nonexistent-helper.py"),
            requireCurrentClientEvidence: false, config: config, testHooks: hooks, startRecoveryMonitor: false,
            applicationDataRoot: Path.Combine(gameRoot, "app"), runtimeRoot: Path.Combine(gameRoot, "runtime"),
            evidenceRoot: Path.Combine(gameRoot, "evidence"), backupRoot: Path.Combine(gameRoot, "backups"));
        _ = await lifecycle.InvokeAsync("profile_instances_reconcile", JsonSerializer.SerializeToElement(new { autoLaunchAll = intent }), CancellationToken.None);
        // Bounded original contract: one enabled primary profile with no restart reason,
        // source-recovered autoLaunchAll determines admission; no native persisted gate.
        output.Add(new { autoLaunchAll = intent, nativeAutoLaunchGame = local, expectedHelperCalls = intent ? 1 : 0,
            actualHelperCalls = calls, mismatch = calls != (intent ? 1 : 0) });
    }
    File.WriteAllText(args[0], JsonSerializer.Serialize(new { scope = "actual current reconcile with inert helper versus source-recovered admission; not original/live execution", cases = output, newGameLaunches = 0 }, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine("HOME009_R1_LEAD_RECONCILE_PROBE_COMPLETE cases=4");
}
finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
