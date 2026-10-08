using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

/// <summary>
/// Distinguishing original 0.3.17 command-default oracle vs ACTUAL
/// OverviewLifecycleService.ReconcileStartupAsync. Controlled fake helper.
/// Original oracle is SHA-gated machine instruction recovery, not original runtime.
/// </summary>
internal static class Home008NativeSemanticChecks
{
    internal static async Task RunAsync(string evidencePath)
    {
        var original = JsonDocument.Parse(File.ReadAllText(evidencePath));
        JsonElement native = original.RootElement.GetProperty("reconcile");
        if (native.GetProperty("defaultWhenAbsent").GetBoolean() != true ||
            native.GetProperty("defaultWhenNonBoolean").GetBoolean() != true ||
            native.GetProperty("explicitFalse").GetBoolean() != false)
            throw new InvalidDataException("Original SHA-gated reconcile contract did not match expected branch");
        var cases = new (string Name, string Json, bool Expected)[]
        {
            ("absent", "{}", true),
            ("true", "{\"autoLaunchAll\":true}", true),
            ("false", "{\"autoLaunchAll\":false}", false),
            ("null", "{\"autoLaunchAll\":null}", true),
            ("integer", "{\"autoLaunchAll\":4}", true),
            ("string", "{\"autoLaunchAll\":\"false\"}", true),
            ("object", "{\"autoLaunchAll\":{}}", true),
            ("array", "{\"autoLaunchAll\":[]}", true),
        };
        var observations = new List<object>();
        string baseDir = Path.Combine(Path.GetTempPath(), "LWB317-HOME008-" + Guid.NewGuid().ToString("N"));
        try
        {
            foreach ((string name, string input, bool expectedStart) in cases)
            {
                string gameRoot = Path.Combine(baseDir, name);
                Directory.CreateDirectory(Path.Combine(gameRoot, "Game"));
                File.WriteAllBytes(Path.Combine(gameRoot, "Game", "LastWar.exe"), [0x4d,0x5a]);
                int starts = 0;
                var hooks = new OverviewLifecycleTestHooks
                {
                    RunOfficialRecoverAsync = (_, _) => Task.CompletedTask,
                    RunOfficialSettleAsync = (_, _) => Task.CompletedTask,
                    RunHelperAsync = (_, _) =>
                    {
                        Interlocked.Increment(ref starts);
                        throw new BridgeCommandException("EXPECTED_INERT_START_REJECTION", "deliberate inert helper rejection");
                    },
                    ProcessMatches = (_, _, _) => false,
                    DeleteFile = _ => { },
                };
                using var lifecycle = new OverviewLifecycleService(
                    "profile-008-" + name, gameRoot,
                    helperPath: Path.Combine(gameRoot, "no-live-helper.py"),
                    requireCurrentClientEvidence: false,
                    testHooks: hooks,
                    startRecoveryMonitor: false,
                    runtimeRoot: Path.Combine(gameRoot,"runtime"),
                    evidenceRoot: Path.Combine(gameRoot,"evidence"),
                    backupRoot: Path.Combine(gameRoot,"backups"),
                    applicationDataRoot: Path.Combine(gameRoot,"app"));
                using JsonDocument request = JsonDocument.Parse(input);
                JsonElement result = JsonSerializer.SerializeToElement(
                    await lifecycle.InvokeAsync("profile_instances_reconcile",
                        request.RootElement, CancellationToken.None));
                if ((starts == 1) != expectedStart)
                    throw new InvalidDataException($"Home008 original bool contract mismatch {name}: starts={starts}; expected={expectedStart}");
                int firstStarts = starts;
                _ = await lifecycle.InvokeAsync("profile_instances_reconcile",
                    request.RootElement, CancellationToken.None);
                if (starts != firstStarts)
                    throw new InvalidDataException("Startup reconcile consumed-once invariant violated");
                observations.Add(new { name, expectedStart, actualStarts=starts,
                    errors=result.GetProperty("errors").GetArrayLength(),
                    onceOnly=true });
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(evidencePath))!);
            string dest = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(evidencePath))!,"production-reconcile-inert.json");
            File.WriteAllText(dest,JsonSerializer.Serialize(new {
                status="PASS",scope="original byte-recovered boolean decision vs actual production OverviewLifecycleService controlled helper; NOT original runtime",
                originalSha256=original.RootElement.GetProperty("sha256").GetString(),
                cases=observations,gameActions=0
            },new JsonSerializerOptions { WriteIndented=true }));
            Console.WriteLine($"HOME008_PRODUCTION_RECONCILE_ORIGINAL_BOOL_ORACLE_PASS {observations.Count} cases");
        }
        finally
        {
            try { Directory.Delete(baseDir,true); } catch { }
        }
    }
}
