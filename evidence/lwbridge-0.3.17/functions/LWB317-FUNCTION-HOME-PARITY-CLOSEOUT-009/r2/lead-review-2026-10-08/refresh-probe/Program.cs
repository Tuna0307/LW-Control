using System.Reflection;
using System.Text;
using System.Text.Json;
using LWBridge.Desktop;

// Lead inverse: actual production RunHelperAsync + host + registry. No process,
// transport, game, installed scripts or owner root is opened. The claimed-binding
// error matches the worker's own bridge-ready-window comparator; only helper
// fulfillment versus rejection differs here.
if (args.Length != 1 || File.Exists(args[0])) throw new InvalidOperationException("supply a fresh output path; never overwrite historical evidence");
var rows = new List<object>();
string root = Path.Combine(Path.GetTempPath(), "home009-r2-lead-refresh-" + Guid.NewGuid().ToString("N"));
try
{
    Directory.CreateDirectory(root);
    foreach (bool helperFulfills in new[] { false, true })
    {
        var registry = new LWBridgeControlPipeRegistry();
        using var host = new LWBridgeControlPipeHostState(@"\\.\pipe\lwbridge-control-v1-lead-r2", registry,
            pipeTokenEntropyFactory: () => Enumerable.Range(0, 32).Select(v => (byte)v).ToArray());
        var binding = host.PrepareLaunchBinding("lead-profile", "lead-session", OverviewLifecycleService.BridgeVersion, 1000);
        if (!registry.TryAdmit("lead-profile", binding.InstanceId, binding.PipeToken, 2000, new object(), out _))
            throw new InvalidOperationException("inert registry setup failed");
        var cancellationWritten = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int markerWrites = 0;
        var hooks = new OverviewLifecycleTestHooks
        {
            DelayAsync = (_, token) => Task.Delay(1, token),
            ReadAllBytes = path => path.EndsWith("game-reported.txt", StringComparison.Ordinal)
                ? Encoding.UTF8.GetBytes($"schema=1\nbridgeVersion={OverviewLifecycleService.BridgeVersion}\nsessionId=lead-session\nchallenge=lead-challenge\ngamePid=4242\ndeadlineMilliseconds=200000\n")
                : throw new FileNotFoundException(path),
            WriteStartCancellation = (_, _, _) => { markerWrites++; cancellationWritten.TrySetResult(); },
            DeleteFile = _ => { },
            RunHelperAsync = async (_, token) =>
            {
                await cancellationWritten.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
                // The observer has returned its refresh error before either helper outcome.
                await Task.Delay(30, token);
                if (!helperFulfills) throw new BridgeCommandException("INERT_HELPER_CANCELLED", "controlled helper rejection");
                return JsonSerializer.SerializeToElement(new { ok = true, inert = true });
            },
        };
        using var lifecycle = new OverviewLifecycleService("lead-profile", root, helperPath: Path.Combine(root, "missing.py"),
            requireCurrentClientEvidence: false, testHooks: hooks, startRecoveryMonitor: false, bridgeHostState: host,
            runtimeRoot: Path.Combine(root, "runtime"), evidenceRoot: Path.Combine(root, "evidence"), backupRoot: Path.Combine(root, "backups"));
        var invocation = new OverviewHelperInvocation("start", "lead-profile", "lead-session", "lead-challenge", null, null, null,
            ControlPipeLaunchBinding: binding);
        var method = typeof(OverviewLifecycleService).GetMethod("RunHelperAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        string outcome;
        try
        {
            await (Task<JsonElement>)method.Invoke(lifecycle, new object[] { invocation, CancellationToken.None })!;
            outcome = "SUCCESS";
        }
        catch (BridgeCommandException ex) { outcome = ex.Code; }
        rows.Add(new { helperFulfills, markerWrites, expected = "PIPE_REGISTRATION_INVALID", actual = outcome,
            mismatch = outcome != "PIPE_REGISTRATION_INVALID" });
    }
}
finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
var result = new { checkpoint = "79617a18d493fbb722f4cc2179279bff954472be",
    scope = "actual production helper/observer boundary; source-recovered refresh-error precedence, not original/live execution",
    cases = rows, newGameLaunches = 0, tempRootRemoved = !Directory.Exists(root) };
File.WriteAllText(args[0], JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(JsonSerializer.Serialize(result));
