using System.Diagnostics;
using System.Reflection;
using System.Text;
using LWBridge.GamePipe;

namespace LWBridge.Desktop.Checks;

internal static class GamePipeAdapterLeaseChecks
{
    private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;

    public static void Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "lwbridge-adapter-lease-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string lease = Path.Combine(root, "lease.txt");
        const string instance = "lease-owner-A";
        try
        {
            WriteLease(lease, instance, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            Set("runtimeDirectory", root);
            Set("instanceId", instance);
            ResetThreadCache();

            Require(LeaseIsFresh(), "first complete exact-owner lease snapshot was rejected");

            // A cache hit must not reopen the lease. Holding an exclusive handle
            // distinguishes the new bounded-success cache from the previous 10 ms
            // read/parse loop without needing a live game or host.
            using (var held = new FileStream(lease, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var quick = Stopwatch.StartNew();
                Require(LeaseIsFresh(), "fresh exact-owner cached lease was not reused");
                quick.Stop();
                Require(quick.Elapsed < TimeSpan.FromMilliseconds(100),
                    "cached lease path attempted contended file I/O");
            }

            // Cache success cannot mask revocation of the same session. The old
            // implementation reread every loop; preserve that observation boundary
            // by invalidating the cache when the lease file disappears or is
            // replaced, even inside the 250 ms parse-cache window.
            File.Delete(lease);
            Require(!LeaseIsFresh(), "same-instance lease deletion remained fresh inside cache window");
            WriteLease(lease, instance, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            Require(LeaseIsFresh(), "rewritten fresh lease was not admitted after deletion");
            File.WriteAllText(lease,
                "sessionId=" + instance + "\nchallenge=test-only\nupdatedAt=" +
                (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 6L) + "\npadding=changed\n",
                new UTF8Encoding(false));
            Require(!LeaseIsFresh(), "same-instance stale lease replacement remained fresh inside cache window");

            WriteLease(lease, instance, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            Require(LeaseIsFresh(), "fresh lease was not restored after stale replacement test");

            // An instance change must retire the old cache immediately.
            Set("instanceId", "lease-owner-B");
            Require(!LeaseIsFresh(), "old instance cache crossed into successor identity");

            // Restore owner A, establish a fresh cache, then age the validation
            // window and make the prior published timestamp stale. A due read under
            // contention must fail closed; it must never return the cached value.
            Set("instanceId", instance);
            ResetThreadCache();
            WriteLease(lease, instance, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            Require(LeaseIsFresh(), "second exact-owner lease snapshot was rejected");
            Thread.Sleep(300);
            Set("lastValidatedLeaseUpdatedAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 6L);
            using (var held = new FileStream(lease, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var due = Stopwatch.StartNew();
                Require(!LeaseIsFresh(), "due contended revalidation reused stale cached lease");
                due.Stop();
                Require(due.Elapsed < TimeSpan.FromSeconds(1),
                    "known-stale contended lease did not fail closed promptly");
            }

            Console.WriteLine("GAME_PIPE_ADAPTER_LEASE_CACHE_OK 250ms exact-owner cache, successor fence, stale contention fail-closed");
        }
        finally
        {
            ResetThreadCache();
            Set("runtimeDirectory", string.Empty);
            Set("instanceId", string.Empty);
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    private static void WriteLease(string path, string instance, long updatedAt) =>
        File.WriteAllText(path,
            "sessionId=" + instance + "\nchallenge=test-only\nupdatedAt=" + updatedAt + "\n",
            new UTF8Encoding(false));

    private static bool LeaseIsFresh() =>
        (bool)(typeof(PipeClientAdapter).GetMethod("LeaseIsFresh", StaticPrivate)!
            .Invoke(null, null) ?? false);

    private static void ResetThreadCache()
    {
        Set("lastValidatedLeaseUpdatedAt", null);
        Set("lastValidatedLeaseInstance", null);
        Set("lastLeaseValidationTimestamp", 0L);
        Set("lastValidatedLeaseLength", 0L);
        Set("lastValidatedLeaseWriteTicks", 0L);
    }

    private static void Set(string field, object? value) =>
        typeof(PipeClientAdapter).GetField(field, StaticPrivate)!.SetValue(null, value);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
