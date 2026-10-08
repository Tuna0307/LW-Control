using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

// HOME 009 R1 E. Executed replacement proof for the retired Program.cs recovery assertions whose boundary values are
// original-contract facts (RVAs asserted by home009_recovery_contract_check.py): millisecond-exact threshold inequalities
// of the monitor (0x41abf8/0x41ac0b hang 30 s, 0x41ac34 disconnect > 59999, 0x41ac53 login-unavailable 180 s), the
// two-miss rule (0x41aafe), the 15-minute update stall (0xe6096) and the retry table (0xd67b08, capped index).
// They drive the real OverviewLifecycleService with exact clock values, not 2-second pump ticks, so equality and the
// off-by-one side of every boundary are executed.
internal static class RecoveryBoundaryChecks
{
    private static string State(RecoveryAsyncOwnershipChecks.Env env) => env.Lifecycle.CurrentRecoveryStatus.State;

    internal static async Task<JsonElement> RunAsync()
    {
        var done = new List<string>();

        // two consecutive missing-process observations (0x41aafe)
        using (var env = new RecoveryAsyncOwnershipChecks.Env())
        {
            await env.StartAsync();
            env.Current.Alive = false;
            await env.Lifecycle.RunRecoveryObservationForTestAsync();
            Check(State(env) == "idle" && env.StartCalls == 1, "one missing observation must not start recovery");
            env.Clock += 1000;
            await env.Lifecycle.RunRecoveryObservationForTestAsync();
            Check(State(env) != "idle" && env.Lifecycle.CurrentRecoveryStatus.Reason == "processExit",
                "second miss starts processExit recovery: " + State(env));
            done.Add("two-miss-rule");
        }

        // hang: offline AND hung for >= 30 000 ms
        foreach ((long delta, bool expectActive) in new[] { (29_999L, false), (30_000L, true) })
        {
            using var env = new RecoveryAsyncOwnershipChecks.Env();
            await env.StartAsync();
            env.BridgeOnline = false; env.Hung = true;
            await env.Lifecycle.RunRecoveryObservationForTestAsync();
            env.Clock += delta;
            await env.Lifecycle.RunRecoveryObservationForTestAsync();
            Check((State(env) != "idle") == expectActive, $"hang +{delta} ms active={State(env)}");
            if (expectActive) Check(env.Lifecycle.CurrentRecoveryStatus.Reason == "hang", "hang reason");
        }
        done.Add("hang-30000ms-boundary");

        // disconnect: offline, not hung, > 59 999 ms
        foreach ((long delta, bool expectActive) in new[] { (59_999L, false), (60_000L, true) })
        {
            using var env = new RecoveryAsyncOwnershipChecks.Env();
            await env.StartAsync();
            env.BridgeOnline = false;
            await env.Lifecycle.RunRecoveryObservationForTestAsync();
            env.Clock += delta;
            await env.Lifecycle.RunRecoveryObservationForTestAsync();
            Check((State(env) != "idle") == expectActive, $"disconnect +{delta} ms: {State(env)}");
            if (expectActive) Check(env.Lifecycle.CurrentRecoveryStatus.Reason == "disconnect", "disconnect reason");
        }
        done.Add("disconnect-60000ms-boundary");

        // login unavailable: bridge online but game state unhealthy (also: state never observed => invalid record), 180 000 ms
        foreach (bool observed in new[] { true, false })
            foreach ((long delta, bool expectActive) in new[] { (179_999L, false), (180_000L, true) })
            {
                using var env = new RecoveryAsyncOwnershipChecks.Env();
                await env.StartAsync();
                env.GameHealthy = false; env.GameStateObserved = observed;
                await env.Lifecycle.RunRecoveryObservationForTestAsync();
                env.Clock += delta;
                await env.Lifecycle.RunRecoveryObservationForTestAsync();
                Check((State(env) != "idle") == expectActive, $"login observed={observed} +{delta} ms: {State(env)}");
            }
        done.Add("login-unavailable-180000ms-boundary-and-unobserved-equals-invalid");

        // an updater process suppresses the offline-driven recovery (0x41ab5b either = updating || online)
        using (var env = new RecoveryAsyncOwnershipChecks.Env())
        {
            await env.StartAsync();
            env.BridgeOnline = false; env.UpdateRunning = true;
            await env.Lifecycle.RunRecoveryObservationForTestAsync();
            env.Clock += 600_000;
            await env.Lifecycle.RunRecoveryObservationForTestAsync();
            Check(State(env) == "idle", "updater activity suppresses disconnect recovery: " + State(env));
            done.Add("updater-suppresses-disconnect");
        }

        // update stall: no updater activity for >= 900 000 ms kills the updaters (0xe6096); a fingerprint change resets the clock
        foreach ((long delta, int expectedKills) in new[] { (899_999L, 0), (900_000L, 1) })
        {
            using var env = new RecoveryAsyncOwnershipChecks.Env();
            await env.StartAsync();
            env.Current.Alive = false; env.UpdateRunning = true;
            await env.Lifecycle.RunRecoveryObservationForTestAsync();
            env.Clock += 1000;
            await env.Lifecycle.RunRecoveryObservationForTestAsync();
            Check(State(env) != "idle", "recovery started: " + State(env));
            env.Clock += delta;
            await env.Lifecycle.RunRecoveryRunTickForTestAsync();
            Check(env.KillUpdaterCalls == expectedKills, $"update stall +{delta}: kills={env.KillUpdaterCalls}");
        }
        using (var env = new RecoveryAsyncOwnershipChecks.Env())
        {
            await env.StartAsync();
            env.Current.Alive = false; env.UpdateRunning = true;
            await env.Lifecycle.RunRecoveryObservationForTestAsync();
            env.Clock += 1000;
            await env.Lifecycle.RunRecoveryObservationForTestAsync();
            env.Clock += 899_999;
            env.Fingerprint = "changed";
            await env.Lifecycle.RunRecoveryRunTickForTestAsync();
            env.Clock += 899_999;
            await env.Lifecycle.RunRecoveryRunTickForTestAsync();
            Check(env.KillUpdaterCalls == 0, "fingerprint change resets the inactivity clock");
        }
        done.Add("update-stall-900000ms-and-fingerprint-reset");

        // retry delays follow 15/30/60/120/300 s and stay capped at 300 s (0xd67b08)
        using (var env = new RecoveryAsyncOwnershipChecks.Env())
        {
            await env.StartAsync();
            env.Current.Alive = false; env.FailStarts = 100;
            await env.Lifecycle.RunRecoveryObservationForTestAsync();
            env.Clock += 1000;
            await env.Lifecycle.RunRecoveryObservationForTestAsync();
            var delays = new List<long>();
            for (int i = 0; i < 12 && delays.Count < 7; i++)
            {
                env.Clock += 1;
                await env.Lifecycle.RunRecoveryRunTickForTestAsync();
                OverviewRecoveryStatus status = env.Lifecycle.CurrentRecoveryStatus;
                if (status.State == "waiting" && status.NextRetryAt is long next)
                {
                    delays.Add(next - env.Clock);
                    env.Clock = next;           // the next attempt becomes due exactly now
                }
            }
            long[] expected = [15_000, 30_000, 60_000, 120_000, 300_000, 300_000, 300_000];
            Check(delays.Count == expected.Length && delays.Zip(expected).All(p => Math.Abs(p.First - p.Second) <= 1),
                "retry delays: " + string.Join(",", delays));
            done.Add("retry-table-and-cap");
        }
        return JsonSerializer.SerializeToElement(new { ok = true, cases = done });
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("recovery boundary check failed: " + message);
    }
}
