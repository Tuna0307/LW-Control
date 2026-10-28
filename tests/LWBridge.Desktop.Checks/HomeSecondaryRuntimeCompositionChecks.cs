using System.Reflection;
using System.Windows.Forms;
using Microsoft.Data.Sqlite;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

// Actual normal-production window constructor, registry selection and retained
// profile factory. No Show/message loop/WebView initialization/game helper.
internal static class HomeSecondaryRuntimeCompositionChecks
{
    internal static async Task RunAsync()
    {
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { RunOnSta(); completed.SetResult(); }
            catch (Exception error) { completed.SetException(error); }
        }) { IsBackground = true, Name = "Home secondary composition regression" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(30));
        Require(thread.Join(TimeSpan.FromSeconds(5)), "composition thread finished after resource cleanup");
        Console.WriteLine("HOME_SECONDARY_RUNTIME_COMPOSITION_OK actual selected-secondary factory and helper/Lua root agreement; game launches=0");
    }

    private static void RunOnSta()
    {
        string root = Path.Combine(Path.GetTempPath(), "home-secondary-composition-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        LWBridgeWindow? window = null;
        OverviewLifecycleService? primary = null, secondary = null;
        const string a = "composition-primary", b = "composition-secondary";
        string oldRuntime = Path.Combine(root, "profiles", b, "overview-runtime");
        string oldBackup = Path.Combine(root, "profiles", b, "overview-backups");
        try
        {
            // A weak-valid native picker root wins selection, but missing real
            // launcher/xLua bytes rejects strict launch admission. An entirely
            // absent root would permit normal discovery to find the real game.
            string inertInstall = Path.Combine(root, "inert-install");
            Directory.CreateDirectory(Path.Combine(inertInstall, "Game", "LastWar_Data", "Plugins", "x86_64"));
            File.WriteAllBytes(Path.Combine(inertInstall, "Game", "LastWar.exe"), []);
            _ = new LocalConfigStore(root, initialValue: LWBridgeLocalConfig.CreateDefault() with
            {
                ProfileId = a, GameRoot = inertInstall,
                AutoLaunchGame = false, AutoReconnect = false, GameDesiredRunning = false
            });
            using (var registry = new ProfileRegistryStore(Path.Combine(root, "controller.db")))
            {
                registry.EnsureLocalProfile(a, "Primary", 1_790_000_000_000);
                registry.EnsureSecondaryProfile(b, "Secondary", 1, 1_790_000_000_001);
                registry.SelectProfile(b);
            }
            Directory.CreateDirectory(oldRuntime);
            Directory.CreateDirectory(oldBackup);
            string oldRecord = Path.Combine(oldRuntime, "adoption.json");
            string oldManifest = Path.Combine(oldBackup, "historical-manifest.json");
            byte[] sentinel = System.Text.Encoding.UTF8.GetBytes("historical files must remain untouched");
            File.WriteAllBytes(oldRecord, sentinel);
            File.WriteAllBytes(oldManifest, sentinel);
            // UI content is never displayed/executed. The check-only UI root seam
            // lets this backend-composition regression run from the Checks
            // directory without copying a canonical browser build into its output.
            string unusedUi = Path.Combine(root, "unused-ui");
            Directory.CreateDirectory(unusedUi);
            File.WriteAllText(Path.Combine(unusedUi, "index.html"), "<!doctype html><title>never shown</title>");
            window = new LWBridgeWindow(null, "overview", "en", "light",
                isolatedRootPath: root, testUiRootPath: unusedUi);
            Require(!window.Visible && !window.IsHandleCreated,
                "no native window shown or handle created");
            Require(Field<LWBridgeBackend>(window, "backend").ProfileId == b,
                "constructor actually selected controller's secondary profile over root config");
            secondary = Field<OverviewLifecycleService>(window, "overviewLifecycleService");
            string clientRoot = Path.Combine(root, "profiles", b, "current-client");
            RequirePath(secondary.ApplicationDataRoot, clientRoot, "secondary application root");
            // These suffixes are the actual native helper/Lua protocol: Python
            // overview_paths and current_overview_bridge.lua both derive from the
            // one LWBRIDGE_REBUILD_DATA_ROOT process environment, not sibling roots.
            RequirePath(secondary.RuntimeRoot, Path.Combine(clientRoot, "overview-bridge"), "secondary helper/game runtime");
            RequirePath(secondary.EvidenceRoot, Path.Combine(clientRoot, "overview-evidence"), "secondary helper evidence");
            RequirePath(secondary.BackupRoot, Path.Combine(clientRoot, "overview-bridge-backups"), "secondary helper backups");
            RequirePath(secondary.LiveResourceRuntimeRoot, Path.Combine(clientRoot, "live-resource"), "secondary native resource runtime");
            var retained = Field<object>(window, "retainedProfileRuntimes");
            object primaryRuntime = retained.GetType().GetProperty("Item")!.GetValue(retained, [a])!;
            primary = (OverviewLifecycleService)primaryRuntime.GetType().GetProperty("Lifecycle")!.GetValue(primaryRuntime)!;
            RequirePath(primary.ApplicationDataRoot, root, "primary application root preserved");
            RequirePath(primary.RuntimeRoot, Path.Combine(root, "overview-bridge"), "primary runtime preserved");
            Require(primary.RuntimeRoot != secondary.RuntimeRoot && !primary.IsReady && !secondary.IsReady,
                "independent stopped owners, no fake native readiness");
            Require(!primary.RuntimeManaged && !secondary.RuntimeManaged,
                "neither stopped inert owner has acquired a game session");
            Require(File.ReadAllBytes(oldRecord).SequenceEqual(sentinel) && File.ReadAllBytes(oldManifest).SequenceEqual(sentinel),
                "old sibling runtime/backup retained without migration or deletion");
            Require(!Directory.EnumerateFiles(root, "game-reported.txt", SearchOption.AllDirectories).Any() &&
                    !Directory.EnumerateFiles(root, "heartbeat.json", SearchOption.AllDirectories).Any(),
                "no actual game/helper producer or synthetic native response ran");
        }
        finally
        {
            if (window is not null)
            {
                // Never shown Forms do not raise FormClosed on Dispose. Invoke
                // the actual shutdown handler once to drain retained timers,
                // SQLite services and shared pipe state before disposing controls.
                typeof(LWBridgeWindow).GetMethod("OnFormClosed", BindingFlags.Instance | BindingFlags.NonPublic,
                        null, [typeof(object), typeof(FormClosedEventArgs)], null)!
                    .Invoke(window, [window, new FormClosedEventArgs(CloseReason.ApplicationExitCall)]);
                window.Dispose();
            }
            if (primary is not null) Require(Field<bool>(primary, "closed"), "primary lifecycle timer owner closed");
            if (secondary is not null) Require(Field<bool>(secondary, "closed"), "secondary lifecycle timer owner closed");
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }

    private static T Field<T>(object owner, string name) =>
        (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;

    private static void RequirePath(string actual, string expected, string message) => Require(
        string.Equals(Path.GetFullPath(actual), Path.GetFullPath(expected), StringComparison.OrdinalIgnoreCase),
        message + ": " + actual);

    private static void Require(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException("HOME_SECONDARY_RUNTIME_COMPOSITION: " + message);
    }
}
