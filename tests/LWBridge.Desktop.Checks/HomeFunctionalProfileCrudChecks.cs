using System.Text.Json;
using LWBridge.Desktop;
using Microsoft.Data.Sqlite;

namespace LWBridge.Desktop.Checks;

// F-07 native SQLite registry, no fake game processes or commercial entitlement.
internal static class HomeFunctionalProfileCrudChecks
{
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("HOME_F07: " + message);
    }

    private static async Task ExpectCodeAsync(Func<Task> action, string expected)
    {
        try { await action(); }
        catch (BridgeCommandException ex) when (ex.Code == expected) { return; }
        throw new InvalidOperationException("HOME_F07: expected " + expected);
    }

    internal static async Task RunAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "lwbridge-f07-local-profiles-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string database = Path.Combine(root, "controller.db");
        JsonElement empty = JsonSerializer.SerializeToElement(new { });
        try
        {
            bool deleteSafe = false;
            string[] created;
            using (var store = new ProfileRegistryStore(database))
            {
                store.EnsureLocalProfile("primary", "Primary", 1);
                using var registry = new ProfileRegistryCommandService(store, maxProfiles: 4,
                    deleteProfileUnderOwnerGate: (_, delete, _) =>
                    {
                        if (!deleteSafe)
                            throw new BridgeCommandException("PROFILE_STILL_ACTIVE", "Cannot delete an active profile.");
                        delete();
                        return Task.CompletedTask;
                    });
                for (int i = 0; i < 3; i++)
                    await registry.InvokeAsync("profile_create", empty, CancellationToken.None);
                var roster = registry.Snapshot;
                created = roster.Profiles.Skip(1).Select(p => p.Id).ToArray();
                Require(created.Length == 3 && created.Distinct(StringComparer.Ordinal).Count() == 3 &&
                        created.All(id => id.StartsWith("local-", StringComparison.Ordinal)) &&
                        roster.MaxProfiles == 4, "creates three distinct durable local secondary profiles");
                await ExpectCodeAsync(async () => await registry.InvokeAsync("profile_create", empty,
                    CancellationToken.None), "PROFILE_LIMIT_REACHED");

                deleteSafe = true;
                await ExpectCodeAsync(async () => await registry.InvokeAsync("profile_delete",
                    JsonSerializer.SerializeToElement(new { profileId = "primary" }), CancellationToken.None),
                    "PROFILE_PRIMARY_FIXED");
                _ = await registry.InvokeAsync("profile_select",
                    JsonSerializer.SerializeToElement(new { profileId = created[0], focusGame = false }),
                    CancellationToken.None);
                await ExpectCodeAsync(async () => await registry.InvokeAsync("profile_delete",
                    JsonSerializer.SerializeToElement(new { profileId = created[0] }), CancellationToken.None),
                    "PROFILE_SELECTED");
                _ = await registry.InvokeAsync("profile_select",
                    JsonSerializer.SerializeToElement(new { profileId = "primary", focusGame = false }),
                    CancellationToken.None);

                deleteSafe = false;
                await ExpectCodeAsync(async () => await registry.InvokeAsync("profile_delete",
                    JsonSerializer.SerializeToElement(new { profileId = created[0] }), CancellationToken.None),
                    "PROFILE_STILL_ACTIVE");
                Require(registry.Snapshot.Profiles.Count == 4,
                    "rejected deletion retains the exact registry owner");
                deleteSafe = true;
                _ = await registry.InvokeAsync("profile_delete",
                    JsonSerializer.SerializeToElement(new { profileId = created[0] }), CancellationToken.None);
                Require(registry.Snapshot.Profiles.Count == 3 &&
                        registry.Snapshot.Profiles.All(p => p.Id != created[0]),
                    "stopped secondary deletion removes only its registry row");
                _ = await registry.InvokeAsync("profile_create", empty, CancellationToken.None);
                Require(registry.Snapshot.Profiles.Count == 4,
                    "deletion permits a later capacity-safe local creation");
            }

            using (var reopened = new ProfileRegistryStore(database))
            {
                Require(reopened.Read(4).Profiles.Count == 4 &&
                        reopened.Read(4).Profiles.Any(p => p.Id == created[1]) &&
                        reopened.Read(4).Profiles.All(p => p.Id != created[0]),
                    "profile create/delete and independent metadata survive SQLite reopen");
            }
            // The production Delete gate must not turn an unknown/pending
            // restoration journal into deleted profile ownership. Neither
            // game processes nor installed files are needed for this inverse.
            string runtimeRoot = Path.Combine(root, "overview-bridge");
            Directory.CreateDirectory(runtimeRoot);
            var config = new LocalConfigStore(Path.Combine(root, "profile-config"),
                initialValue: LWBridgeLocalConfig.CreateDefault() with
                { ProfileId = "local-safety", GameDesiredRunning = false });
            using (var lifecycle = new OverviewLifecycleService("local-safety",
                       Path.Combine(root, "missing-game"), config: config,
                       startRecoveryMonitor: false, runtimeRoot: runtimeRoot,
                       applicationDataRoot: root, requireCurrentClientEvidence: false))
            {
                Require(lifecycle.CanRetireStoppedLocalProfile(),
                    "stopped owner with no recovery journal can be removed safely");
                config.Update(c => c with { GameDesiredRunning = true });
                Require(!lifecycle.CanRetireStoppedLocalProfile(),
                    "desired-running still fences deletion without a process PID");
                config.Update(c => c with { GameDesiredRunning = false });
                string journal = Path.Combine(runtimeRoot, "recovery.json");
                File.WriteAllText(journal, "not valid JSON");
                Require(!lifecycle.CanRetireStoppedLocalProfile(),
                    "unreadable recovery journal fails closed instead of dropping an owner");
                File.WriteAllText(journal, "{\"schemaVersion\":1,\"stage\":\"closing_owned_game_for_restore\"}");
                Require(!lifecycle.CanRetireStoppedLocalProfile(),
                    "pending exact restoration blocks profile deletion");
                File.Delete(journal);
                Require(lifecycle.CanRetireStoppedLocalProfile(),
                    "stopped absent journal restores safe local delete eligibility");
            }
            Console.WriteLine("HOME_F07_NATIVE_PROFILE_CRUD_OK no game processes");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
