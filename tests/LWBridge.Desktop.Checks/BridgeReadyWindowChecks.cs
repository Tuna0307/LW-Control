using System.Text;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

// HOME 009 R2 D. Host side of the recovered bridge-connect window (original 0x1DD114-0x1DD152): the helper reports the
// launcher's game PID and the wall-clock deadline (report + 90 000 ms) in runtime/game-reported.txt; the host performs
// refresh_pending(key, deadline) on the pending pipe registration, so the registration lives exactly as long as the
// readiness wait. Also the separation of the three clocks: launcher->game acquisition budget (--timeout-seconds), the 90 s
// window (helper) and the outer supervision (CreateBoundedStartInvocation).
// Actual OverviewLifecycleService + actual LWBridgeControlPipeRegistry; no process, helper, pipe or game.
internal static class BridgeReadyWindowChecks
{
    private const string Profile = "profile-r2-ready-window";
    private static readonly string Token = LWBridgeProxyLaunchEnvironmentContract.EncodePipeToken(Enumerable.Range(0, 32).Select(v => (byte)v).ToArray());

    private sealed class Run
    {
        public bool Admit150 { get; init; }
        public bool Admit199999 { get; init; }
        public bool Admit200000 { get; init; }
        public string? ErrorCode { get; init; }
    }

    // reportFor: session of the report ("same" | "other" | null for no report); claimBeforeReport simulates a game that already connected.
    private static async Task<Run> ExecuteAsync(string? reportFor, long deadlineMilliseconds, bool claimBeforeReport = false)
    {
        string root = Path.Combine(Path.GetTempPath(), "lwbridge-r2-ready-" + Guid.NewGuid().ToString("N"));
        byte[] entropy = Enumerable.Range(0, 32).Select(v => (byte)v).ToArray();
        var registry = new LWBridgeControlPipeRegistry();
        using var host = new LWBridgeControlPipeHostState(@"\\.\pipe\lwbridge-control-v1-r2-ready", registry, pipeTokenEntropyFactory: () => entropy.ToArray());
        string? session = null, challenge = null;
        bool reportVisible = false;
        bool a150 = false, a199 = false, a200 = false;
        string? errorCode = null;
        var hooks = new OverviewLifecycleTestHooks
        {
            RunOfficialRecoverAsync = (_, _) => Task.CompletedTask,
            RunOfficialSettleAsync = (_, _) => Task.CompletedTask,
            MonotonicMilliseconds = () => 1_000,
            DelayAsync = (_, token) => Task.Delay(2, token),
            ReadAllBytes = path =>
            {
                if (path.EndsWith("game-reported.txt", StringComparison.Ordinal) && reportVisible && reportFor is not null)
                {
                    string reported = reportFor == "same" ? session! : "someone-else";
                    return Encoding.UTF8.GetBytes(
                        $"schema=1\nbridgeVersion={OverviewLifecycleService.BridgeVersion}\nsessionId={reported}\nchallenge={challenge}\ngamePid=4242\ndeadlineMilliseconds={deadlineMilliseconds}\n");
                }
                throw new FileNotFoundException(path);
            },
            WriteLease = (_, _, _) => { },
            DeleteFile = _ => { },
            RunHelperAsync = async (invocation, token) =>
            {
                if (invocation.Operation != "start") throw new InvalidOperationException("only start is scripted");
                session = invocation.SessionId; challenge = invocation.Challenge;
                var binding = invocation.ControlPipeLaunchBinding!;
                if (claimBeforeReport)
                    Check(registry.TryAdmit(Profile, binding.InstanceId, binding.PipeToken, 2_000, new object(), out _), "pre-claim");
                reportVisible = true;                     // the helper writes the marker when the launcher reports the game
                await Task.Delay(250, token);             // the host observer polls on its own cadence
                if (!claimBeforeReport)
                {
                    // probe the pending registration's expiry without consuming it more than once per scenario
                    a150 = registry.IsPending(binding.InstanceId) && ProbeAdmit(registry, binding, 150_000);
                }
                throw new InvalidOperationException("scripted helper ends after the probe");
            },
        };
        try
        {
            Directory.CreateDirectory(root);
            using var lifecycle = new OverviewLifecycleService(
                Profile, root, helperPath: Path.Combine(root, "fake-helper.py"), requireCurrentClientEvidence: false,
                testHooks: hooks, startRecoveryMonitor: false, bridgeHostState: host, enableBridgeControlPipeLaunchBinding: true,
                runtimeRoot: Path.Combine(root, "rt"), evidenceRoot: Path.Combine(root, "ev"), backupRoot: Path.Combine(root, "bk"));
            try { await lifecycle.InvokeAsync("profile_instance_start", JsonSerializer.SerializeToElement(new { }), CancellationToken.None); }
            catch (BridgeCommandException ex) { errorCode = ex.Code; }
            _ = a199; _ = a200;
        }
        finally { try { Directory.Delete(root, recursive: true); } catch { } }
        return new Run { Admit150 = a150, Admit199999 = a199, Admit200000 = a200, ErrorCode = errorCode };
    }

    // TryAdmit claims the registration on success, so each probe time is a separate scenario run.
    private static bool ProbeAdmit(LWBridgeControlPipeRegistry registry, LWBridgeControlPipeLaunchBinding binding, long now) =>
        registry.TryAdmit(Profile, binding.InstanceId, binding.PipeToken, now, new object(), out _);

    internal static async Task<JsonElement> RunAsync()
    {
        var done = new List<string>();

        Run reported = await ExecuteAsync("same", 200_000);
        Check(reported.Admit150, "a pending registration is refreshed to the reported deadline (valid at 150 000, beyond the initial 91 000)");
        done.Add("report-refreshes-pending-registration");

        Run none = await ExecuteAsync(null, 200_000);
        Check(!none.Admit150, "without a game report the initial 90 s registration has expired at 150 000");
        done.Add("no-report-keeps-initial-expiry");

        Run foreign = await ExecuteAsync("other", 200_000);
        Check(!foreign.Admit150, "a report for another session is ignored");
        done.Add("foreign-session-report-ignored");

        Run claimed = await ExecuteAsync("same", 200_000, claimBeforeReport: true);
        Check(claimed.ErrorCode == "PIPE_REGISTRATION_INVALID",
            "refreshing an already-claimed registration is the original PIPE_REGISTRATION_INVALID launch error: " + claimed.ErrorCode);
        done.Add("refresh-of-claimed-registration-is-the-launch-error");

        // expiry arithmetic at the registry: admission is valid strictly before the refreshed deadline (expires <= now rejects)
        {
            var registry = new LWBridgeControlPipeRegistry();
            registry.Register(Profile, "inst", Token, 91_000);
            registry.RefreshPending("inst", 200_000);
            Check(registry.TryAdmit(Profile, "inst", Token, 199_999, new object(), out _), "admit at deadline-1");
            var r2 = new LWBridgeControlPipeRegistry();
            r2.Register(Profile, "inst", Token, 91_000);
            r2.RefreshPending("inst", 200_000);
            Check(!r2.TryAdmit(Profile, "inst", Token, 200_000, new object(), out _), "no admission at the deadline itself");
            done.Add("registry-expiry-boundary");
        }

        // the three budgets: acquisition (--timeout-seconds), 90 s window, outer supervision
        {
            string root = Path.Combine(Path.GetTempPath(), "lwbridge-r2-budget-" + Guid.NewGuid().ToString("N"));
            long clock = 10_000;
            var hooks = new OverviewLifecycleTestHooks { MonotonicMilliseconds = () => clock };
            using var lifecycle = new OverviewLifecycleService(
                Profile, root, helperPath: Path.Combine(root, "x.py"), requireCurrentClientEvidence: false,
                testHooks: hooks, startRecoveryMonitor: false);
            OverviewHelperInvocation full = lifecycle.CreateBoundedStartInvocation("s", "c", clock + 225_000, null);
            Check(full.TimeoutSeconds == 120 && full.SupervisionMilliseconds == 225_000, $"default window: {full.TimeoutSeconds}/{full.SupervisionMilliseconds}");
            OverviewHelperInvocation reduced = lifecycle.CreateBoundedStartInvocation("s", "c", clock + 130_000, null);
            Check(reduced.TimeoutSeconds == 25, "acquisition shrinks first, the 90 s window is reserved: " + reduced.TimeoutSeconds);
            OverviewHelperInvocation minimum = lifecycle.CreateBoundedStartInvocation("s", "c", clock + 115_000, null);
            Check(minimum.TimeoutSeconds == 10, "minimum window leaves 10 s acquisition");
            bool refused = false;
            try { lifecycle.CreateBoundedStartInvocation("s", "c", clock + 114_999, null); }
            catch (BridgeCommandException ex) { refused = ex.Code == "BRIDGE_START_TIMEOUT"; }
            Check(refused, "less than acquisition-minimum + 90 s + cleanup margin is refused as BRIDGE_START_TIMEOUT");
            done.Add("acquisition-window-supervision-budgets");
        }
        return JsonSerializer.SerializeToElement(new { ok = true, cases = done });
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("bridge ready window check failed: " + message);
    }
}
