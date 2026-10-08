using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

// HOME 009 R2 C. Actual OverviewLifecycleService.ReconcileStartupAsync against the recovered original admission rule
// (0x203519-0x203551 payload `autoLaunchAll`, default true for missing/null/non-boolean; 0x39E153/0x2EA391 no other
// preference; launch call 0x2053FA with closeUnmanaged=false). The inert helper hook records admissions; no process,
// game or real helper runs. The native AutoLaunchGame mirror is varied to prove it no longer participates.
internal static class ReconcileAdmissionChecks
{
    private static (string Name, string Payload, bool Admit)[] Payloads() =>
    [
        ("true", "{\"autoLaunchAll\":true}", true),
        ("false", "{\"autoLaunchAll\":false}", false),
        ("missing", "{}", true),
        ("null", "{\"autoLaunchAll\":null}", true),
        ("integer", "{\"autoLaunchAll\":0}", true),
        ("string", "{\"autoLaunchAll\":\"false\"}", true),
        ("object", "{\"autoLaunchAll\":{}}", true),
        ("array", "{\"autoLaunchAll\":[]}", true),
    ];

    internal static async Task<JsonElement> RunAsync()
    {
        var rows = new List<object>();
        foreach (bool nativeMirror in new[] { true, false })
            foreach ((string name, string payloadText, bool admit) in Payloads())
            {
                using var env = new RecoveryAsyncOwnershipChecks.Env();
                env.Config.Update(c => c with { AutoLaunchGame = nativeMirror });
                using JsonDocument payload = JsonDocument.Parse(payloadText);
                object? result = await env.Lifecycle.InvokeAsync("profile_instances_reconcile", payload.RootElement.Clone(), CancellationToken.None);
                int starts = env.StartCalls;
                Check(starts == (admit ? 1 : 0), $"payload={name} native={nativeMirror}: expected admission {(admit ? 1 : 0)}, actual {starts}");
                Check(JsonSerializer.SerializeToElement(result, JsonOptions.Default).GetProperty("errors").GetArrayLength() == 0, "no errors");
                // consumed-once: a repeated reconcile cannot admit again (0x203021 runs once per app start)
                await env.Lifecycle.InvokeAsync("profile_instances_reconcile", payload.RootElement.Clone(), CancellationToken.None);
                Check(env.StartCalls == starts, "repeated reconcile must not admit again");
                rows.Add(new { payload = name, nativeAutoLaunchGame = nativeMirror, expectedAdmission = admit ? 1 : 0, actualAdmission = starts });
            }

        // failure: the launch error is reported as {profileId, error...} and later calls stay consumed
        using (var env = new RecoveryAsyncOwnershipChecks.Env())
        {
            env.Config.Update(c => c with { AutoLaunchGame = false });
            env.PreFailStarts = 1;
            using JsonDocument payload = JsonDocument.Parse("{\"autoLaunchAll\":true}");
            object? result = await env.Lifecycle.InvokeAsync("profile_instances_reconcile", payload.RootElement.Clone(), CancellationToken.None);
            JsonElement errors = JsonSerializer.SerializeToElement(result, JsonOptions.Default).GetProperty("errors");
            Check(errors.GetArrayLength() == 1 && errors[0].GetProperty("profileId").GetString() == "r1-async-ownership",
                "launch failure is reported per profile: " + errors);
            rows.Add(new { case_ = "launch-failure-reported", errors = errors.GetArrayLength() });
        }

        // supported states: already running owned game is never double-started
        using (var env = new RecoveryAsyncOwnershipChecks.Env())
        {
            env.Config.Update(c => c with { AutoLaunchGame = false });
            await env.StartAsync();
            using JsonDocument payload = JsonDocument.Parse("{\"autoLaunchAll\":true}");
            await env.Lifecycle.InvokeAsync("profile_instances_reconcile", payload.RootElement.Clone(), CancellationToken.None);
            Check(env.StartCalls == 1, "reconcile with an owned running game must not start another");
            rows.Add(new { case_ = "owned-running-not-double-started" });
        }
        return JsonSerializer.SerializeToElement(new { ok = true, rows });
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("reconcile admission check failed: " + message);
    }
}
