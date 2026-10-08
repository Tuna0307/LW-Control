using System.Reflection;
using System.Text;
using System.Text.Json;
using LWBridge.Desktop;

var cases = new[] {
    new Case("pending-helper-fulfilled-first", "valid", false, false, false, false, 4, "SUCCESS"),
    new Case("pending-fast-report", "valid", false, false, false, false, 0, "SUCCESS"),
    new Case("pending-helper-rejected", "valid", false, true, false, false, 0, "INERT_HELPER_REJECTED"),
    new Case("claimed-helper-fulfilled", "valid", true, false, false, false, 0, "PIPE_REGISTRATION_INVALID"),
    new Case("claimed-helper-rejected", "valid", true, true, false, false, 0, "PIPE_REGISTRATION_INVALID"),
    new Case("host-closed-helper-fulfilled", "valid", false, false, true, false, 0, "BRIDGE_STOPPED"),
    new Case("obsolete-session-report", "wrong-session", false, false, false, false, 0, "LAUNCH_REPORT_FAILED"),
    new Case("obsolete-challenge-report", "wrong-challenge", false, false, false, false, 0, "LAUNCH_REPORT_FAILED"),
    new Case("report-read-errors", "read-error", false, false, false, false, 0, "LAUNCH_REPORT_FAILED"),
    new Case("malformed-pid-report", "bad-pid", false, false, false, false, 0, "LAUNCH_REPORT_FAILED"),
    new Case("foreign-owner-ack", "valid", false, false, false, false, 0, "LAUNCH_REPORT_FAILED"),
    new Case("missing-report", "missing", false, false, false, false, 0, "LAUNCH_REPORT_FAILED"),
    new Case("cancel-before-helper-completion", "missing", false, false, false, true, 0, "CANCELLED")
};
if (args.Length != 1 || File.Exists(args[0]))
    throw new ArgumentException("Supply a fresh output path.");
string root = Path.Combine(Path.GetTempPath(), "home009-r3-handoff-" + Guid.NewGuid().ToString("N"));
var rows = new List<object>();
try {
    Directory.CreateDirectory(root);
    foreach (var plan in cases) {
        string dir = Path.Combine(root, plan.Name);
        Directory.CreateDirectory(dir);
        var registry = new LWBridgeControlPipeRegistry();
        using var host = new LWBridgeControlPipeHostState(@"\\.\pipe\lwbridge-control-v1-r3",
            registry, pipeTokenEntropyFactory: () => Enumerable.Range(0, 32).Select(v => (byte)v).ToArray());
        var binding = host.PrepareLaunchBinding("primary", "session-r3", OverviewLifecycleService.BridgeVersion, 1000);
        if (plan.Claim && !registry.TryAdmit("primary", binding.InstanceId, binding.PipeToken,
            2000, new object(), out _))
            throw new InvalidOperationException("Cannot claim report registration");
        if (plan.CloseHost) host.Close();
        string ackPath = Path.Combine(dir, "runtime", "registration-confirmed.txt");
        if (plan.Name == "foreign-owner-ack")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ackPath)!);
            File.WriteAllText(ackPath, "schema=1\nsessionId=foreign\nchallenge=foreign\ninstanceId=foreign\ngamePid=4321\n");
        }
        int readCalls = 0, markerWrites = 0;
        var hooks = new OverviewLifecycleTestHooks {
            DelayAsync = (_, ct) => Task.Delay(3, ct),
            ReadAllBytes = _ => {
                int reads = Interlocked.Increment(ref readCalls);
                if (plan.Report == "missing" || reads <= plan.DelayReads)
                    throw new FileNotFoundException("Controlled report not yet published");
                if (plan.Report == "read-error")
                    throw new IOException("Controlled inaccessible report");
                string sid = plan.Report == "wrong-session" ? "old-session" : "session-r3";
                string challenge = plan.Report == "wrong-challenge" ? "old-challenge" : "challenge-r3";
                return Encoding.UTF8.GetBytes(
                    $"schema=1\nbridgeVersion={OverviewLifecycleService.BridgeVersion}\nsessionId={sid}\nchallenge={challenge}\ngamePid={(plan.Report == "bad-pid" ? 0 : 4242)}\ndeadlineMilliseconds=200000\n");
            },
            RunHelperAsync = async (_, ct) => {
                if (plan.Cancel) await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                else await Task.Delay(12, ct);
                if (plan.Reject)
                    throw new BridgeCommandException("INERT_HELPER_REJECTED", "Controlled helper error");
                return JsonSerializer.SerializeToElement(new { ok = true });
            },
            WriteStartCancellation = (_, _, _) => Interlocked.Increment(ref markerWrites),
            DeleteFile = _ => { }
        };
        using var lifecycle = new OverviewLifecycleService("primary", dir,
            helperPath: Path.Combine(dir, "nonexistent.py"), requireCurrentClientEvidence: false,
            testHooks: hooks, startRecoveryMonitor: false, bridgeHostState: host,
            runtimeRoot: Path.Combine(dir, "runtime"), evidenceRoot: Path.Combine(dir, "evidence"),
            backupRoot: Path.Combine(dir, "backups"));
        using var cts = new CancellationTokenSource();
        if (plan.Cancel) cts.CancelAfter(35);
        var inv = new OverviewHelperInvocation("start", "primary", "session-r3",
            "challenge-r3", null, null, null, ControlPipeLaunchBinding: binding);
        var method = typeof(OverviewLifecycleService).GetMethod("RunHelperAsync",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        string actual;
        try {
            await (Task<JsonElement>)method.Invoke(lifecycle, new object[] { inv, cts.Token })!;
            actual = "SUCCESS";
        }
        catch (BridgeCommandException ex) { actual = ex.Code; }
        catch (OperationCanceledException) { actual = "CANCELLED"; }
        string? ackText = File.Exists(ackPath) ? File.ReadAllText(ackPath) : null;
        bool ackVerified = plan.Name switch
        {
            "pending-helper-fulfilled-first" or "pending-fast-report" =>
                ackText is not null && ackText.Contains("sessionId=session-r3\n") &&
                ackText.Contains("challenge=challenge-r3\n") &&
                ackText.Contains("gamePid=4242\n"),
            "foreign-owner-ack" => ackText is not null &&
                ackText.Contains("sessionId=foreign\n"),
            _ => true
        };
        rows.Add(new { plan.Name, plan.Expected, actual,
            match = plan.Expected == actual && ackVerified, ackVerified,
            readCalls, markerWrites, pending = registry.PendingCount,
            connected = registry.ConnectedCount });
    }
}
finally {
    if (Directory.Exists(root)) Directory.Delete(root, true);
}
var report = new { scope = "R3 actual production RunHelperAsync/host/registry with inert helper and held reports",
    results = rows, passed = rows.Count(x => (bool)x.GetType().GetProperty("match")!.GetValue(x)!),
    total = rows.Count, tempRootRemoved = !Directory.Exists(root), launchedGames = 0 };
File.WriteAllText(args[0], JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(JsonSerializer.Serialize(report));
if (report.passed != report.total) Environment.ExitCode = 1;

record Case(string Name, string Report, bool Claim, bool Reject, bool CloseHost, bool Cancel,
    int DelayReads, string Expected);
