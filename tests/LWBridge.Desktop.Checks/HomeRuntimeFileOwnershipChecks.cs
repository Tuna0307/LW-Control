using System.Reflection;
using System.Text;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class HomeRuntimeFileOwnershipChecks
{
    internal static async Task RunAsync(string root)
    {
        Directory.CreateDirectory(root);
        byte[] owned = Encoding.UTF8.GetBytes("schema=1\nsessionId=A\nchallenge=nonce-A\nupdatedAt=" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() + "\n");
        byte[] foreign = Encoding.UTF8.GetBytes("schema=1\nsessionId=B\nchallenge=nonce-B\nupdatedAt=1800000000\n");
        bool Matches(byte[] bytes) => bytes.SequenceEqual(owned);
        string path = Path.Combine(root, "lease.txt");
        File.WriteAllBytes(path, foreign);
        Require(!OverviewRuntimeFileOwnership.TryWrite(path, owned, Matches), "foreign write is refused");
        Require(!OverviewRuntimeFileOwnership.TryDelete(path, Matches), "foreign cleanup is refused");
        Require(File.ReadAllBytes(path).SequenceEqual(foreign), "foreign bytes survive both operations");
        File.WriteAllBytes(path, new byte[] { 0xff, 0, 0xfe });
        Require(!OverviewRuntimeFileOwnership.TryWrite(path, owned, Matches), "malformed destination is refused");
        Require(!OverviewRuntimeFileOwnership.TryDelete(path, Matches), "malformed cleanup is refused");
        File.Delete(path);
        Require(OverviewRuntimeFileOwnership.TryWrite(path, owned, Matches), "absent destination is admitted");

        bool replacementDenied = false;
        bool partialReadDenied = false;
        string replacement = Path.Combine(root, "foreign-replacement.txt");
        File.WriteAllBytes(replacement, foreign);
        void Compete(string guardedPath, OverviewRuntimeFileMutation mutation)
        {
            try { File.Move(replacement, guardedPath, overwrite: true); }
            catch (IOException) { replacementDenied = true; }
            catch (UnauthorizedAccessException) { replacementDenied = true; }
            try { _ = File.ReadAllBytes(guardedPath); }
            catch (IOException) { partialReadDenied = true; }
        }
        Require(OverviewRuntimeFileOwnership.TryWrite(path, owned, Matches, Compete), "owned refresh succeeds under identity guard");
        Require(replacementDenied && partialReadDenied, "atomic replacement and partial readers are excluded during publication");
        Require(File.ReadAllBytes(path).SequenceEqual(owned), "successful reader gets the complete owned publication");
        replacementDenied = false;
        Require(OverviewRuntimeFileOwnership.TryDelete(path, Matches, Compete), "owned deletion succeeds through verified handle");
        Require(replacementDenied && !File.Exists(path), "replacement cannot enter the validation-to-deletion interval");
        File.Move(replacement, path);
        Require(!OverviewRuntimeFileOwnership.TryDelete(path, Matches) && File.ReadAllBytes(path).SequenceEqual(foreign),
            "foreign identity published after guard release survives later cleanup");

        await VerifyAdapterContentionAsync(root, owned, foreign).ConfigureAwait(false);
        await VerifyPendingCloseAsync(Path.Combine(root, "pending-close"), foreign).ConfigureAwait(false);
    }

    private static async Task VerifyAdapterContentionAsync(string root, byte[] owned, byte[] foreign)
    {
        // Call only the file predicate: never Connect, a pipe listener, or a game.
        Assembly adapter = Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, "OverviewBridge", "LWBridge.GamePipeAdapter.dll"));
        Type type = adapter.GetType("LWBridge.GamePipe.PipeClientAdapter", throwOnError: true)!;
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        type.GetField("runtimeDirectory", flags)!.SetValue(null, root);
        type.GetField("instanceId", flags)!.SetValue(null, "A");
        MethodInfo predicate = type.GetMethod("LeaseIsFresh", flags)!;
        var snapshot = (Func<string, string>)type.GetField("ReadRuntimeSnapshot", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
        bool ReadFresh() => (bool)predicate.Invoke(null, null)!;
        string path = Path.Combine(root, "lease.txt");
        File.WriteAllBytes(path, owned);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        Task<bool> writer = Task.Run(() => OverviewRuntimeFileOwnership.TryWrite(path, owned,
            bytes => bytes.SequenceEqual(owned), (_, _) =>
            {
                entered.Set();
                if (!release.Wait(TimeSpan.FromSeconds(3))) throw new TimeoutException("guard test release");
            }));
        Require(entered.Wait(TimeSpan.FromSeconds(3)), "writer reached guarded publication barrier");
        Task<bool> reader = Task.Run(ReadFresh);
        try
        {
            await Task.Delay(100).ConfigureAwait(false);
            Require(!reader.IsCompleted, "valid lease contention waits instead of reporting stale");
        }
        finally { release.Set(); }
        Require(await writer.ConfigureAwait(false) && await reader.ConfigureAwait(false), "valid complete publication survives consumer contention");
        File.WriteAllBytes(path, foreign);
        Require(!ReadFresh(), "foreign lease is rejected");
        File.WriteAllText(path, "sessionId=A\nupdatedAt=1\n");
        Require(!ReadFresh(), "stale lease is rejected");
        File.WriteAllText(path, "malformed\n");
        Require(!ReadFresh(), "malformed lease is rejected");
        File.WriteAllText(path, "sessionId=A\nupdatedAt=" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() + "\nmalformed\n");
        Require(!ReadFresh(), "valid-looking fields do not admit malformed bytes");
        File.WriteAllBytes(path, owned);
        using (var held = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Require(snapshot(path) == "busy\n", "Lua seam classifies the actual Windows sharing exception");
            bool expired = await Task.Run(() =>
            {
                type.GetField("lastValidatedLeaseInstance", flags)!.SetValue(null, "A");
                type.GetField("lastValidatedLeaseUpdatedAt", flags)!.SetValue(null, DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 6);
                return ReadFresh();
            }).WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            Require(!expired, "contention never extends a previously validated lease beyond its existing freshness horizon");
        }
        File.Delete(path);
        Require(!ReadFresh(), "missing lease is rejected");
        Require(snapshot(path) == "unavailable\n", "missing metadata is never classified as busy");
    }

    private static async Task VerifyPendingCloseAsync(string root, byte[] foreign)
    {
        Directory.CreateDirectory(Path.Combine(root, "Game"));
        string runtime = Path.Combine(root, "runtime");
        Directory.CreateDirectory(runtime);
        string cancellation = Path.Combine(runtime, "cancel-start.txt");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hooks = new OverviewLifecycleTestHooks
        {
            RunOfficialRecoverAsync = (_, _) => Task.CompletedTask,
            RunOfficialSettleAsync = (_, _) => Task.CompletedTask,
            ReadAllBytes = path => throw new FileNotFoundException(path),
            ProcessMatches = (_, _, _) => false,
            RunHelperAsync = async (_, token) =>
            {
                entered.SetResult();
                await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false);
                return default;
            },
        };
        using var lifecycle = new OverviewLifecycleService("pending-A", root,
            helperPath: Path.Combine(root, "never-run.py"), requireCurrentClientEvidence: false,
            testHooks: hooks, startRecoveryMonitor: false, runtimeRoot: runtime,
            evidenceRoot: Path.Combine(root, "evidence"), backupRoot: Path.Combine(root, "backups"));
        using var request = new CancellationTokenSource();
        Task<object?> start = lifecycle.InvokeAsync("profile_instance_start",
            JsonSerializer.SerializeToElement(new { profileId = "pending-A" }, JsonOptions.Default), request.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        File.WriteAllBytes(cancellation, foreign);
        lifecycle.Close();
        Require(File.ReadAllBytes(cancellation).SequenceEqual(foreign), "pending Close preserves foreign cancellation metadata");
        request.Cancel();
        try { _ = await start.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false); }
        catch (BridgeCommandException) { }
        catch (OperationCanceledException) { }
        Require(File.ReadAllBytes(cancellation).SequenceEqual(foreign), "pending Close and cancellation cleanup preserve foreign cancellation metadata");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Home runtime ownership: " + message);
    }
}
