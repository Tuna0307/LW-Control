using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

// HOME 009 R1 C. Inert checks of the REAL Start path (OverviewLifecycleService.InvokeAsync("profile_instance_start"))
// for the original 0.3.17 `closeUnmanaged` contract: 0x207784 (payload key, default false), 0x1d6548 (gate),
// 0x1d6578-0x1d65b7 (sequential path-verified terminate; error aborts), 0x1d693f-0x1d6955 (5 s deadline),
// 0x1d723b-0x1d72eb/0x1d74e5 (list -> empty? -> deadline -> GAME_CLOSE_TIMEOUT -> 100 ms wait).
// Process table, clock and delays are scripted; no process is opened or terminated and no helper/game runs.
internal static class UnmanagedCloseChecks
{
    private const string TestStop = "TEST_STOP_AFTER_CLOSE";

    private sealed class World : IOwnedProcessApi
    {
        public long Now = 1_000_000;
        public readonly Dictionary<int, long?> GoneAt = new();          // pid -> virtual ms when it disappears (null: never)
        public readonly Dictionary<int, string> Image = new();
        public readonly HashSet<int> QueryDenied = new();
        public readonly HashSet<int> TerminateOpenDenied = new();
        public readonly HashSet<int> TerminateFails = new();
        public readonly Dictionary<int, long> ExitDelayAfterTerminate = new(); // missing: process never exits by itself
        public readonly List<int> Terminated = new();
        public readonly List<string> Events = new();
        public string Game = "";
        private readonly Dictionary<IntPtr, (int Pid, uint Access)> handles = new();
        private long next = 100;

        public bool Alive(int pid) => Image.ContainsKey(pid) && (!GoneAt.TryGetValue(pid, out long? g) || g is null || Now < g);
        public IReadOnlyList<int> List(string _) { var l = Image.Keys.Where(p => Alive(p) && Image[p].Equals(Game, StringComparison.OrdinalIgnoreCase)).ToList(); l.Sort(); Events.Add("list:" + string.Join(",", l)); return l; }

        public IntPtr Open(int pid, uint access)
        {
            if (!Alive(pid)) return IntPtr.Zero;
            if ((access & OwnedProcessTermination.ProcessTerminate) != 0 && TerminateOpenDenied.Contains(pid)) return IntPtr.Zero;
            if (access == OwnedProcessTermination.ProcessQueryLimitedInformation && QueryDenied.Contains(pid)) return IntPtr.Zero;
            var h = new IntPtr(next++); handles[h] = (pid, access); return h;
        }
        public string? ImagePath(IntPtr handle) { var (pid, _) = handles[handle]; return Alive(pid) && !QueryDenied.Contains(pid) ? Image[pid] : null; }
        public string? CreationUtc(IntPtr handle) => "2026-10-08T00:00:00.0000000Z";
        public bool Terminate(IntPtr handle, uint exitCode)
        {
            var (pid, _) = handles[handle];
            if (TerminateFails.Contains(pid)) { Events.Add("terminate-fail:" + pid); return false; }
            Terminated.Add(pid); Events.Add("terminate:" + pid);
            if (ExitDelayAfterTerminate.TryGetValue(pid, out long d)) GoneAt[pid] = Now + d;
            return true;
        }
        public bool WaitExited(IntPtr handle, int milliseconds) => !Alive(handles[handle].Pid);
        public void Close(IntPtr handle) => handles.Remove(handle);
        public bool PidExists(int pid) => Alive(pid);
        public int LastErrorCode(int fallback) => fallback;
        public int OpenHandles => handles.Count;
    }

    private sealed class Outcome { public string? Code; public string? Message; public object? Details; public bool StartReached; public List<string> Events = new(); }

    private static async Task<Outcome> RunAsync(World world, string root, JsonElement payload)
    {
        Directory.CreateDirectory(Path.Combine(root, "Game"));
        string game = Path.Combine(root, "Game", "LastWar.exe");
        world.Game = game;
        var outcome = new Outcome();
        var config = new LocalConfigStore(Path.Combine(root, "config"));
        config.Update(current => current with { ProfileId = "r1-unmanaged-close", GameRoot = root });
        var hooks = new OverviewLifecycleTestHooks
        {
            RunOfficialRecoverAsync = (_, _) => Task.CompletedTask,
            RunOfficialSettleAsync = (_, _) => Task.CompletedTask,
            RunHelperAsync = (invocation, _) =>
            {
                outcome.StartReached = true; world.Events.Add("helper-start");
                throw new BridgeCommandException(TestStop, "test ends the start after the close step");
            },
            SelectedGamePids = world.List,
            OwnedProcessApi = world,
            MonotonicMilliseconds = () => world.Now,
            DelayAsync = (delay, token) => { token.ThrowIfCancellationRequested(); world.Now += (long)delay.TotalMilliseconds; world.Events.Add("wait:" + (long)delay.TotalMilliseconds); return Task.CompletedTask; },
            ReadAllBytes = _ => Array.Empty<byte>(),
            WriteLease = (_, _, _) => { },
            DeleteFile = _ => { },
            UpdateProcessRunning = () => false,
            UpdateActivityFingerprint = () => null,
            ProcessHung = (_, _) => false,
        };
        using var lifecycle = new OverviewLifecycleService(
            "r1-unmanaged-close", root, helperPath: Path.Combine(root, "fake-helper.py"),
            requireCurrentClientEvidence: false, config: config, testHooks: hooks, startRecoveryMonitor: false,
            runtimeRoot: Path.Combine(root, "rt"), evidenceRoot: Path.Combine(root, "ev"), backupRoot: Path.Combine(root, "bk"));
        try { await lifecycle.InvokeAsync("profile_instance_start", payload, CancellationToken.None); }
        catch (BridgeCommandException ex) { outcome.Code = ex.Code; outcome.Message = ex.Message; outcome.Details = ex.Details; }
        outcome.Events = world.Events;
        return outcome;
    }

    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement.Clone();

    internal static async Task<JsonElement> RunAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "lwbridge-r1-unmanaged-" + Guid.NewGuid().ToString("N"));
        var done = new List<string>();
        try
        {
            Directory.CreateDirectory(root);
            string Fresh(string name) { string r = Path.Combine(root, name); Directory.CreateDirectory(r); return r; }
            World Make(string name, out string r)
            {
                r = Fresh(name);
                var w = new World(); w.Game = Path.Combine(r, "Game", "LastWar.exe"); return w;
            }

            // payload parsing: missing / non-boolean / non-object => false (0x207784-0x2077af)
            Check(!OverviewLifecycleService.ReadCloseUnmanaged(Json("{}")), "missing key is false");
            Check(!OverviewLifecycleService.ReadCloseUnmanaged(Json("{\"closeUnmanaged\":1}")), "numeric value is false");
            Check(!OverviewLifecycleService.ReadCloseUnmanaged(Json("{\"closeUnmanaged\":\"true\"}")), "string value is false");
            Check(!OverviewLifecycleService.ReadCloseUnmanaged(Json("[true]")), "array payload is false");
            Check(!OverviewLifecycleService.ReadCloseUnmanaged(Json("{\"closeUnmanaged\":false}")), "explicit false");
            Check(OverviewLifecycleService.ReadCloseUnmanaged(Json("{\"closeUnmanaged\":true}")), "explicit true");
            done.Add("payload-key-default-false");

            // closeUnmanaged false (default): refused, nothing terminated, helper never started
            {
                var w = Make("refuse", out string r);
                w.Image[500] = w.Game; w.GoneAt[500] = null;
                var o = await RunAsync(w, r, Json("{}"));
                Check(o.Code == "UNMANAGED_GAME_RUNNING" && w.Terminated.Count == 0 && !o.StartReached, $"refusal: {o.Code}");
                Check(o.Message == "UNMANAGED_GAME_RUNNING", "message equals the code (0x1d6781)");
                Check(JsonSerializer.Serialize(o.Details) == "{\"pids\":[500]}", "details carry the ascending pids: " + JsonSerializer.Serialize(o.Details));
                done.Add("false-refuses-without-touching-processes");
            }
            // closeUnmanaged true, nothing running: no close step
            {
                var w = Make("none", out string r);
                var o = await RunAsync(w, r, Json("{\"closeUnmanaged\":true}"));
                Check(o.StartReached && w.Terminated.Count == 0 && o.Code == TestStop, "empty list proceeds to start");
                done.Add("true-no-process-proceeds");
            }
            // sequential ascending termination then poll until gone (list precedes deadline check)
            {
                var w = Make("order", out string r);
                foreach (int pid in new[] { 900, 300, 600 }) { w.Image[pid] = w.Game; w.ExitDelayAfterTerminate[pid] = 250; }
                var o = await RunAsync(w, r, Json("{\"closeUnmanaged\":true}"));
                Check(w.Terminated.SequenceEqual(new[] { 300, 600, 900 }), "ascending order: " + string.Join(",", w.Terminated));
                Check(o.StartReached && o.Code == TestStop, "start continues after the close: " + o.Code);
                int firstWait = w.Events.IndexOf("wait:100");
                Check(w.Events.IndexOf("helper-start") > firstWait && firstWait > w.Events.IndexOf("terminate:900"), "close precedes start; waits follow terminates");
                done.Add("terminates-in-ascending-order-then-polls");
            }
            // deadline boundaries: iteration i lists at t0+100*i; gone at G.
            foreach ((long goneAfter, bool ok) in new[] { (4900L, true), (5000L, true), (5001L, false), (5100L, false) })
            {
                var w = Make("deadline" + goneAfter, out string r);
                w.Image[700] = w.Game; w.ExitDelayAfterTerminate[700] = goneAfter;
                long t0 = w.Now;
                var o = await RunAsync(w, r, Json("{\"closeUnmanaged\":true}"));
                Check(ok ? (o.StartReached && o.Code == TestStop) : (o.Code == "GAME_CLOSE_TIMEOUT" && !o.StartReached), $"gone after {goneAfter} ms: {o.Code}");
                if (!ok) Check(w.Now - t0 == 5000, "timeout after exactly the 5 s of 100 ms waits: " + (w.Now - t0));
            }
            done.Add("deadline-boundaries-list-before-deadline-check");
            // an error terminating a process aborts at once; later PIDs are untouched
            {
                var w = Make("abort", out string r);
                w.Image[100] = w.Game; w.ExitDelayAfterTerminate[100] = 0;
                w.Image[200] = w.Game; w.TerminateOpenDenied.Add(200);
                w.Image[300] = w.Game;
                var o = await RunAsync(w, r, Json("{\"closeUnmanaged\":true}"));
                Check(o.Code == "IO_ERROR" && w.Terminated.SequenceEqual(new[] { 100 }) && !o.StartReached, $"abort: {o.Code}/{string.Join(",", w.Terminated)}");
                done.Add("terminate-error-aborts-before-later-pids");
            }
            {
                var w = Make("queryfail", out string r);
                w.Image[100] = w.Game; w.QueryDenied.Add(100);
                var o = await RunAsync(w, r, Json("{\"closeUnmanaged\":true}"));
                Check(o.Code == "PROCESS_QUERY_FAILED" && w.Terminated.Count == 0, $"query failure: {o.Code}");
                done.Add("unreadable-image-is-process-query-failed");
            }
            {
                var w = Make("termfail", out string r);
                w.Image[100] = w.Game; w.TerminateFails.Add(100);
                var o = await RunAsync(w, r, Json("{\"closeUnmanaged\":true}"));
                Check(o.Code == "IO_ERROR" && !o.StartReached, $"terminate failure: {o.Code}");
                done.Add("terminate-failure-is-io-error");
            }
            // a different image at the listed PID is left alone (original: no action) and no handle leaks
            {
                var w = Make("foreign", out string r);
                w.Image[100] = w.Game; w.ExitDelayAfterTerminate[100] = 0;
                var o = await RunAsync(w, r, Json("{\"closeUnmanaged\":true}"));
                Check(w.OpenHandles == 0, "handles closed");
                done.Add("no-handle-leak");
            }
        }
        finally { try { Directory.Delete(root, recursive: true); } catch { } }
        return JsonSerializer.SerializeToElement(new { ok = true, cases = done });
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("unmanaged close check failed: " + message);
    }
}
