using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

// HOME 009 R1 B. Inert checks of the REAL handle-bound termination code (OwnedProcessTermination and the
// service's TerminateOwnedProcessAsync mapping) against a scripted process-incarnation table. No process is
// opened for termination; the only real-OS use is a read-only query of this check's own process to prove the
// production Win32 format of the image path / creation identity.
internal static class OwnedProcessHandleBindingChecks
{
    private const string Game = @"C:\Games\LastWar\Game\LastWar.exe";
    private const string Old = "2026-09-12T01:00:00.0000000Z";
    private const string New = "2026-09-12T02:00:00.0000000Z";
    private const int Pid = 41000;

    private sealed class Inc
    {
        public int Pid; public string Image = Game; public string Created = Old; public string Label = "captured";
        public bool Alive = true; public int ExitAfterWaits = 0; public bool TerminateFails;
        public bool TerminateOpenDenied; public bool QueryDenied; public bool CreationUnreadable;
        public int TerminateCalls; public int WaitsAfterTerminate = -1;
    }

    private sealed class Table : IOwnedProcessApi
    {
        public readonly List<Inc> All = new();
        public readonly Dictionary<IntPtr, (Inc Inc, uint Access, bool Closed)> Handles = new();
        public Action<string>? Hook;
        private long next = 100;

        public Inc Add(Inc inc) { All.Add(inc); return inc; }
        public Inc? ByPid(int pid) => All.LastOrDefault(i => i.Pid == pid && i.Alive);
        public Inc Replace(int pid, string image, string created, string label)
        {
            Inc? old = ByPid(pid);
            if (old is not null) old.Alive = false;
            return Add(new Inc { Pid = pid, Image = image, Created = created, Label = label });
        }
        public int OpenHandles => Handles.Values.Count(h => !h.Closed);

        public IntPtr Open(int pid, uint access)
        {
            Hook?.Invoke("open");
            Inc? inc = ByPid(pid);
            if (inc is null) return IntPtr.Zero;
            if ((access & OwnedProcessTermination.ProcessTerminate) != 0 && inc.TerminateOpenDenied) return IntPtr.Zero;
            if (access == OwnedProcessTermination.ProcessQueryLimitedInformation && inc.QueryDenied) return IntPtr.Zero;
            var handle = new IntPtr(next++);
            Handles[handle] = (inc, access, false);
            return handle;
        }
        public string? ImagePath(IntPtr handle) { var h = Handles[handle]; return h.Inc.QueryDenied || !h.Inc.Alive ? null : h.Inc.Image; }
        public string? CreationUtc(IntPtr handle) { var h = Handles[handle]; return h.Inc.CreationUnreadable ? null : h.Inc.Created; }
        public bool Terminate(IntPtr handle, uint exitCode)
        {
            Hook?.Invoke("terminate");
            var h = Handles[handle];
            Check((h.Access & OwnedProcessTermination.ProcessTerminate) != 0, "terminate needs the terminate right");
            Check(exitCode == 1, "exit code 1");
            h.Inc.TerminateCalls++;
            if (h.Inc.TerminateFails || !h.Inc.Alive) return false;
            h.Inc.WaitsAfterTerminate = 0;
            return true;
        }
        public bool WaitExited(IntPtr handle, int milliseconds)
        {
            var h = Handles[handle];
            Check((h.Access & OwnedProcessTermination.Synchronize) != 0, "wait needs synchronize");
            if (!h.Inc.Alive) return true;
            if (h.Inc.WaitsAfterTerminate >= 0)
            {
                if (h.Inc.WaitsAfterTerminate >= h.Inc.ExitAfterWaits) { h.Inc.Alive = false; return true; }
                h.Inc.WaitsAfterTerminate++;
            }
            return false;
        }
        public void Close(IntPtr handle) { var h = Handles[handle]; Handles[handle] = (h.Inc, h.Access, true); }
    }

    private static Task NoDelay(TimeSpan _, CancellationToken token) { token.ThrowIfCancellationRequested(); return Task.CompletedTask; }

    internal static async Task<JsonElement> RunAsync()
    {
        var done = new List<string>();

        // legitimate termination: identity matches on the handle, exit observed on the same handle after delays
        {
            var t = new Table(); t.Add(new Inc { Pid = Pid, ExitAfterWaits = 3 });
            var r = await OwnedProcessTermination.TerminateAsync(t, Pid, Game, Old, NoDelay, CancellationToken.None);
            Check(r == OwnedProcessTerminationResult.Terminated && t.All[0].TerminateCalls == 1 && !t.All[0].Alive && t.OpenHandles == 0, "legitimate termination");
            done.Add("legitimate-termination-awaited-on-handle");
        }
        // same-path replacement before the handle is opened (different incarnation) is not terminated
        {
            var t = new Table(); t.Add(new Inc { Pid = Pid });
            t.Hook = name => { if (name == "open") { t.Hook = null; t.Replace(Pid, Game, New, "replacement"); } };
            var r = await OwnedProcessTermination.TerminateAsync(t, Pid, Game, Old, NoDelay, CancellationToken.None);
            Check(r == OwnedProcessTerminationResult.NotVerified && t.All.All(i => i.TerminateCalls == 0) && t.OpenHandles == 0, "same-path replacement must be refused");
            done.Add("same-path-replacement-refused");
        }
        // different-path replacement
        {
            var t = new Table(); t.Add(new Inc { Pid = Pid });
            t.Hook = name => { if (name == "open") { t.Hook = null; t.Replace(Pid, @"C:\Other\Tool.exe", New, "other"); } };
            var r = await OwnedProcessTermination.TerminateAsync(t, Pid, Game, Old, NoDelay, CancellationToken.None);
            Check(r == OwnedProcessTerminationResult.NotVerified && t.All.All(i => i.TerminateCalls == 0), "different-path replacement must be refused");
            done.Add("different-path-replacement-refused");
        }
        // replacement after validation, before TerminateProcess: the call can only reach the captured handle's process
        {
            var t = new Table(); t.Add(new Inc { Pid = Pid });
            Inc? replacement = null;
            t.Hook = name => { if (name == "terminate") { t.Hook = null; replacement = t.Replace(Pid, Game, New, "replacement"); } };
            var r = await OwnedProcessTermination.TerminateAsync(t, Pid, Game, Old, NoDelay, CancellationToken.None);
            Check(replacement is not null && replacement.TerminateCalls == 0 && replacement.Alive, "replacement must survive");
            Check(r == OwnedProcessTerminationResult.NotVerified && t.OpenHandles == 0, "old process already gone => nothing terminated, no leak");
            done.Add("replacement-after-validation-only-reaches-opened-handle");
        }
        // unreadable creation identity is refused
        {
            var t = new Table(); t.Add(new Inc { Pid = Pid, CreationUnreadable = true });
            var r = await OwnedProcessTermination.TerminateAsync(t, Pid, Game, Old, NoDelay, CancellationToken.None);
            Check(r == OwnedProcessTerminationResult.NotVerified && t.All[0].TerminateCalls == 0, "unreadable creation");
            done.Add("unreadable-creation-refused");
        }
        // access failures
        {
            var t = new Table(); t.Add(new Inc { Pid = Pid, TerminateOpenDenied = true });
            var denied = await OwnedProcessTermination.TerminateAsync(t, Pid, Game, Old, NoDelay, CancellationToken.None);
            Check(denied == OwnedProcessTerminationResult.OpenDenied && t.OpenHandles == 0, "terminate right denied => OpenDenied");
            var t2 = new Table(); t2.Add(new Inc { Pid = Pid, TerminateOpenDenied = true, QueryDenied = true });
            var unverified = await OwnedProcessTermination.TerminateAsync(t2, Pid, Game, Old, NoDelay, CancellationToken.None);
            Check(unverified == OwnedProcessTerminationResult.NotVerified, "nothing verifiable => NotVerified");
            var t3 = new Table(); t3.Add(new Inc { Pid = Pid, TerminateFails = true });
            var failed = await OwnedProcessTermination.TerminateAsync(t3, Pid, Game, Old, NoDelay, CancellationToken.None);
            Check(failed == OwnedProcessTerminationResult.TerminateFailed && t3.OpenHandles == 0, "terminate failure while alive");
            var t4 = new Table(); var gone = t4.Add(new Inc { Pid = Pid, TerminateFails = true });
            t4.Hook = name => { if (name == "terminate") gone.Alive = false; };
            var vanished = await OwnedProcessTermination.TerminateAsync(t4, Pid, Game, Old, NoDelay, CancellationToken.None);
            Check(vanished == OwnedProcessTerminationResult.NotVerified, "terminate failed because the process just exited");
            done.Add("access-and-terminate-failures-classified");
        }
        // absent PID
        {
            var t = new Table();
            var r = await OwnedProcessTermination.TerminateAsync(t, Pid, Game, Old, NoDelay, CancellationToken.None);
            Check(r == OwnedProcessTerminationResult.NotVerified, "absent PID");
            done.Add("absent-pid-not-verified");
        }
        // cancellation while waiting for the exit closes the handle
        {
            var t = new Table(); t.Add(new Inc { Pid = Pid, ExitAfterWaits = int.MaxValue });
            using var cts = new CancellationTokenSource();
            int delays = 0;
            Task Delay(TimeSpan _, CancellationToken token) { if (++delays == 3) cts.Cancel(); token.ThrowIfCancellationRequested(); return Task.CompletedTask; }
            bool cancelled = false;
            try { await OwnedProcessTermination.TerminateAsync(t, Pid, Game, Old, Delay, cts.Token); }
            catch (OperationCanceledException) { cancelled = true; }
            Check(cancelled && t.OpenHandles == 0 && t.All[0].TerminateCalls == 1, "cancellation during exit wait");
            done.Add("cancel-during-exit-wait-closes-handle");
        }
        // path-only identity (updater helper) and mismatch
        {
            var t = new Table(); t.Add(new Inc { Pid = Pid, Image = @"C:\Games\LastWar\LastWarUpdater.exe", Created = New });
            var ok = await OwnedProcessTermination.TerminateAsync(t, Pid, @"C:\Games\LastWar\LastWarUpdater.exe", null, NoDelay, CancellationToken.None);
            var t2 = new Table(); t2.Add(new Inc { Pid = Pid, Image = @"C:\Elsewhere\LastWarUpdater.exe" });
            var skip = await OwnedProcessTermination.TerminateAsync(t2, Pid, @"C:\Games\LastWar\LastWarUpdater.exe", null, NoDelay, CancellationToken.None);
            Check(ok == OwnedProcessTerminationResult.Terminated && skip == OwnedProcessTerminationResult.NotVerified && t2.All[0].TerminateCalls == 0, "path-only identity");
            done.Add("updater-path-only-identity");
        }
        // service mapping of results to the existing error codes
        {
            await ServiceMappingAsync(done);
        }
        // production Win32 format, read-only on this process
        {
            using Process self = Process.GetCurrentProcess();
            IntPtr handle = Win32OwnedProcessApi.Instance.Open(self.Id, OwnedProcessTermination.ProcessQueryLimitedInformation);
            Check(handle != IntPtr.Zero, "open self for query");
            try
            {
                string? image = Win32OwnedProcessApi.Instance.ImagePath(handle);
                string? created = Win32OwnedProcessApi.Instance.CreationUtc(handle);
                Check(image is not null && string.Equals(Path.GetFullPath(image), Path.GetFullPath(self.MainModule!.FileName), StringComparison.OrdinalIgnoreCase), "self image path");
                Check(created == self.StartTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture), "self creation identity format equals Process.StartTime 'O'");
                Check(!Win32OwnedProcessApi.Instance.WaitExited(handle, 0), "self is not exited");
            }
            finally { Win32OwnedProcessApi.Instance.Close(handle); }
            done.Add("win32-format-matches-process-starttime-readonly");
        }
        return JsonSerializer.SerializeToElement(new { ok = true, cases = done });
    }

    private static async Task ServiceMappingAsync(List<string> done)
    {
        string root = Path.Combine(Path.GetTempPath(), "lwbridge-r1-owned-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            async Task<string?> Run(Table table)
            {
                var hooks = new OverviewLifecycleTestHooks
                {
                    RunOfficialRecoverAsync = (_, _) => Task.CompletedTask,
                    RunOfficialSettleAsync = (_, _) => Task.CompletedTask,
                    OwnedProcessApi = table,
                    DelayAsync = NoDelay,
                };
                using var lifecycle = new OverviewLifecycleService(
                    "r1-owned-process", root, helperPath: Path.Combine(root, "fake.py"),
                    requireCurrentClientEvidence: false, config: new LocalConfigStore(Path.Combine(root, "config")),
                    testHooks: hooks, startRecoveryMonitor: false,
                    runtimeRoot: Path.Combine(root, "rt"), evidenceRoot: Path.Combine(root, "ev"), backupRoot: Path.Combine(root, "bk"));
                try { await lifecycle.TerminateOwnedProcessAsync(Pid, Game, Old, CancellationToken.None); return null; }
                catch (BridgeCommandException ex) { return ex.Code; }
            }

            var ok = new Table(); ok.Add(new Inc { Pid = Pid, ExitAfterWaits = 2 });
            Check(await Run(ok) is null && ok.OpenHandles == 0, "service: verified termination returns");
            var replaced = new Table(); replaced.Add(new Inc { Pid = Pid, Created = New });
            Check(await Run(replaced) == "PROCESS_IDENTITY_CHANGED" && replaced.All[0].TerminateCalls == 0, "service: replacement => PROCESS_IDENTITY_CHANGED");
            var denied = new Table(); denied.Add(new Inc { Pid = Pid, TerminateOpenDenied = true });
            Check(await Run(denied) == "GAME_RECOVERY_TERMINATE_FAILED", "service: open denied");
            var fails = new Table(); fails.Add(new Inc { Pid = Pid, TerminateFails = true });
            Check(await Run(fails) == "GAME_RECOVERY_TERMINATE_FAILED", "service: terminate failure");
            var absent = new Table();
            Check(await Run(absent) == "PROCESS_IDENTITY_CHANGED", "service: absent process");
            done.Add("service-error-code-mapping-preserved");
        }
        finally { try { Directory.Delete(root, recursive: true); } catch { } }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("owned-process handle binding check failed: " + message);
    }
}
