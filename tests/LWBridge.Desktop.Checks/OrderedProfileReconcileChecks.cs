using System.Text.Json;
using LWBridge.Desktop;
using Microsoft.Data.Sqlite;

namespace LWBridge.Desktop.Checks;

// Fully inert native-service boundary. Multiple local profiles are real SQLite
// entries; per-owner delegates replace any game/process/lease acquisition.
internal static class OrderedProfileReconcileChecks
{
    private static void Require(bool valid, string reason)
    {
        if (!valid) throw new InvalidOperationException("ORDERED_RECONCILE: " + reason);
    }
    internal static async Task RunAsync()
    {
        string folder = Path.Combine(Path.GetTempPath(), "lwb317-ordered-reconcile-" +
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string db = Path.Combine(folder, "controller.db");
        try
        {
            using var store = new ProfileRegistryStore(db);
            store.EnsureLocalProfile("profile-A", "A", 20);
            store.EnsureSecondaryProfile("profile-B", "B", 1, 30);
            store.EnsureSecondaryProfile("profile-C", "C", 1, 25);
            store.EnsureSecondaryProfile("profile-D", "D", 2, 40);
            store.EnsureSecondaryProfile("profile-E", "E", 3, 45);
            // C precedes B on created_at tie-break. D is disabled, E locked.
            using (var dbconn = new SqliteConnection(new SqliteConnectionStringBuilder
            { DataSource = db, Pooling = false }.ToString()))
            {
                dbconn.Open();
                using var command = dbconn.CreateCommand();
                command.CommandText = """
                    UPDATE profiles SET display_order=5 WHERE id='profile-A';
                    UPDATE profiles SET enabled=0 WHERE id='profile-D';
                    UPDATE profiles SET locked_reason='owner-locked' WHERE id='profile-E';
                    """;
                command.ExecuteNonQuery();
            }
            using var registry = new ProfileRegistryCommandService(store, maxProfiles: 1);
            // 010 c-handlers 0x1a4d26: validate ID before note payload.
            // A bad ID and missing note must produce INVALID_PROFILE_ID,
            // rather than the clone's former INVALID_REQUEST.
            try
            {
                _ = await registry.InvokeAsync("profile_note_set",
                    JsonSerializer.SerializeToElement(new { profileId = "bad/id" }),
                    CancellationToken.None);
                throw new InvalidOperationException("invalid note owner unexpectedly accepted");
            }
            catch (BridgeCommandException error) when (error.Code == "INVALID_PROFILE_ID") { }
            var called = new List<string>();
            var service = new OrderedProfileReconcileCommandService(registry,
                (owner, _, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    called.Add(owner);
                    if (owner == "profile-B")
                        throw new InvalidDataException("B cannot create an independent runtime");
                    if (owner == "profile-A")
                        return Task.FromResult<object?>(new
                        {
                            errors = new[] { new { profileId = owner, error = "LAUNCH_FAILED",
                                message = "A failure" } }
                        });
                    return Task.FromResult<object?>(new { errors = Array.Empty<object>() });
                });
            JsonElement empty = JsonSerializer.SerializeToElement(new { autoLaunchAll = true });
            JsonElement observed = JsonSerializer.SerializeToElement(
                await service.InvokeAsync("profile_instances_reconcile", empty,
                    CancellationToken.None), JsonOptions.Default);
            Require(called.SequenceEqual(new[] { "profile-C", "profile-B", "profile-A" }),
                "enabled/unlocked display_order,created_at,id visits all local owners in order");
            string[] ids = observed.GetProperty("errors").EnumerateArray()
                .Select(item => item.GetProperty("profileId").GetString()!).ToArray();
            Require(ids.SequenceEqual(new[] { "profile-B", "profile-A" }),
                "B failure cannot halt later A and errors retain their own profile IDs");
            string[] codes = observed.GetProperty("errors").EnumerateArray()
                .Select(item => item.GetProperty("error").GetString()!).ToArray();
            Require(codes.SequenceEqual(new[] { "PROFILE_RUNTIME_UNAVAILABLE", "LAUNCH_FAILED" }),
                "no substitution of fake entitlement or cross-profile errors");
            _ = await service.InvokeAsync("profile_instances_reconcile", empty, CancellationToken.None);
            Require(called.Count == 3, "same host must not double launch after reconciliation");

            // A reply attributed to a different owner is rejected, and the
            // controller must not silently alias it onto the selected profile.
            var mismatch = new OrderedProfileReconcileCommandService(registry,
                (owner, _, _) => Task.FromResult<object?>(new
                { errors = new[] { new { profileId = "unrelated", error = "LAUNCH_FAILED" } } }));
            JsonElement wrong = JsonSerializer.SerializeToElement(
                await mismatch.InvokeAsync("profile_instances_reconcile", empty, CancellationToken.None),
                JsonOptions.Default);
            Require(wrong.GetProperty("errors")[0].GetProperty("profileId").GetString() == "profile-C" &&
                wrong.GetProperty("errors")[0].GetProperty("error").GetString() == "PROFILE_SCOPE_MISMATCH",
                "foreign-owner failure cannot leak into another profile");

            var cancel = new OrderedProfileReconcileCommandService(registry,
                (_, _, token) => { token.ThrowIfCancellationRequested();
                    return Task.FromResult<object?>(new { errors = Array.Empty<object>() }); });
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            try
            {
                await cancel.InvokeAsync("profile_instances_reconcile", empty, cancelled.Token);
                throw new InvalidOperationException("cancelled gate unexpectedly succeeded");
            }
            catch (OperationCanceledException) when (cancelled.IsCancellationRequested) { }

            // Exercise the actual backend dispatcher, not just the ordered
            // service: a lifecycle is also registered and used to shadow it.
            var config = new LocalConfigStore(persistent: false,
                initialValue: LWBridgeLocalConfig.CreateDefault() with { ProfileId = "profile-D" });
            using var lifecycle = new OverviewLifecycleService(
                "profile-D", gameRoot: null, config: config,
                testHooks: new OverviewLifecycleTestHooks(), startRecoveryMonitor: false);
            int dispatchedOwners = 0;
            var dispatcherService = new OrderedProfileReconcileCommandService(registry,
                (_, _, _) =>
                {
                    dispatchedOwners++;
                    return Task.FromResult<object?>(new { errors = Array.Empty<object>() });
                });
            var backend = new LWBridgeBackend(config,
                asyncCommands: new CompositeAsyncCommandService(dispatcherService, lifecycle),
                overviewLifecycle: lifecycle);
            JsonElement dispatched = JsonSerializer.SerializeToElement(
                await backend.InvokeAsync("profile_instances_reconcile", empty, CancellationToken.None),
                JsonOptions.Default);
            Require(dispatched.GetProperty("errors").GetArrayLength() == 0 && dispatchedOwners == 3,
                "actual backend uses registry admission/order rather than its disabled selected runtime");
            _ = await backend.InvokeAsync("profile_instances_reconcile", empty, CancellationToken.None);
            Require(dispatchedOwners == 3, "actual backend preserves consumed-once startup reconciliation");

            var restartedOwners = new List<string>();
            var multiRestart = new OrderedProfileReconcileCommandService(
                registry,
                (_, _, _) => Task.FromResult<object?>(new { errors = Array.Empty<object>() }),
                (owner, _, _) =>
                {
                    restartedOwners.Add(owner);
                    if (owner == "profile-B")
                        throw new BridgeCommandException("GAME_OPERATION_IN_PROGRESS", "B is busy");
                    if (owner == "profile-A")
                        return Task.FromResult<object?>(new { restarted = Array.Empty<string>(),
                            errors = new[] { new { profileId = owner, error = "GAME_CLOSE_FAILED", message = "A restore" } } });
                    return Task.FromResult<object?>(new { restarted = new[] { owner },
                        errors = Array.Empty<object>() });
                });
            var multiBackend = new LWBridgeBackend(config,
                asyncCommands: new CompositeAsyncCommandService(multiRestart, lifecycle),
                overviewLifecycle: lifecycle);
            JsonElement restartResult = JsonSerializer.SerializeToElement(
                await multiBackend.InvokeAsync("profile_instances_update_and_restart", empty, CancellationToken.None),
                JsonOptions.Default);
            Require(restartedOwners.SequenceEqual(new[] { "profile-C", "profile-B", "profile-A" }),
                "actual backend visits enabled/unlocked owners for Update-and-Restart in registry order");
            Require(restartResult.GetProperty("restarted").EnumerateArray().Select(x => x.GetString())
                    .SequenceEqual(new[] { "profile-C" }) &&
                restartResult.GetProperty("errors").EnumerateArray().Select(x => x.GetProperty("profileId").GetString())
                    .SequenceEqual(new[] { "profile-B", "profile-A" }),
                "one owner restart failure cannot hide a successful later owner or reattribute its errors");
            var wrongRestart = new OrderedProfileReconcileCommandService(registry,
                (_, _, _) => Task.FromResult<object?>(new { errors = Array.Empty<object>() }),
                (_, _, _) => Task.FromResult<object?>(new
                { restarted = new[] { "other-owner" }, errors = Array.Empty<object>() }));
            JsonElement wrongRestartResult = JsonSerializer.SerializeToElement(
                await wrongRestart.InvokeAsync("profile_instances_update_and_restart", empty, CancellationToken.None),
                JsonOptions.Default);
            Require(wrongRestartResult.GetProperty("restarted").GetArrayLength() == 0 &&
                wrongRestartResult.GetProperty("errors").EnumerateArray().All(x =>
                    x.GetProperty("error").GetString() == "PROFILE_SCOPE_MISMATCH"),
                "foreign restart result cannot be attributed to an unrelated registered owner");

            Console.WriteLine("ORDERED profile reconcile: service and actual backend routing PASS; game launches=0");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(folder, true);
        }
    }
}
