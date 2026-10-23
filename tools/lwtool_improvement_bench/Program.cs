using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using LWBridge.GamePipe;

const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
const int iterations = 40_000;
string root = Path.Combine(Path.GetTempPath(), "lwbridge-lease-bench-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
string lease = Path.Combine(root, "lease.txt");
const string instance = "bench-owner";

try
{
    WriteLease();
    Set("runtimeDirectory", root);
    Set("instanceId", instance);
    ResetCache();

    var method = typeof(PipeClientAdapter).GetMethod("LeaseIsFresh", StaticPrivate)
        ?? throw new InvalidOperationException("LeaseIsFresh missing");
    var candidate = (Func<bool>)method.CreateDelegate(typeof(Func<bool>));

    // Warm JIT and OS file cache equally before measuring CPU/wall differences.
    for (int i = 0; i < 100; i++) BaselineLeaseIsFresh();
    ResetCache();
    for (int i = 0; i < 100; i++)
        if (!candidate()) throw new InvalidOperationException("candidate warmup rejected fresh lease");

    var trials = new List<object>();
    var baselineWalls = new List<double>();
    var baselineCpus = new List<double>();
    var candidateWalls = new List<double>();
    var candidateCpus = new List<double>();
    for (int trial = 1; trial <= 3; trial++)
    {
        WriteLease();
        var baseline = Measure(BaselineLeaseIsFresh, iterations);
        WriteLease();
        ResetCache();
        var cached = Measure(candidate, iterations);
        baselineWalls.Add(baseline.wallMs);
        baselineCpus.Add(baseline.cpuMs);
        candidateWalls.Add(cached.wallMs);
        candidateCpus.Add(cached.cpuMs);
        trials.Add(new
        {
            trial,
            baseline = new { baseline.wallMs, baseline.cpuMs, baseline.accepted },
            candidate = new { cached.wallMs, cached.cpuMs, cached.accepted },
            wallSpeedup = baseline.wallMs / Math.Max(0.001, cached.wallMs),
            cpuSpeedup = cached.cpuMs > 0 ? baseline.cpuMs / cached.cpuMs : (double?)null,
        });
    }
    static double Median(List<double> values)
    {
        double[] ordered = values.OrderBy(value => value).ToArray();
        return ordered[ordered.Length / 2];
    }
    double baselineWallMedian = Median(baselineWalls);
    double baselineCpuMedian = Median(baselineCpus);
    double candidateWallMedian = Median(candidateWalls);
    double candidateCpuMedian = Median(candidateCpus);

    // Model the production idle cadence separately. The pre-change path parsed
    // once every 10 ms loop (100/s); the bounded cache can require at most four
    // successful full validations/s at a 250 ms interval while the lease stays
    // unchanged/fresh. This is a source-derived upper bound, not a file-I/O
    // counter and is kept separate from the measured tight-loop result.
    var result = new
    {
        schemaVersion = 1,
        workload = "exact production lease parser vs bounded exact-owner cache",
        iterations,
        trials,
        median = new
        {
            baselineWallMs = baselineWallMedian,
            baselineCpuMs = baselineCpuMedian,
            candidateWallMs = candidateWallMedian,
            candidateCpuMs = candidateCpuMedian,
            wallSpeedup = baselineWallMedian / Math.Max(0.001, candidateWallMedian),
            cpuSpeedup = candidateCpuMedian > 0 ? baselineCpuMedian / candidateCpuMedian : (double?)null,
            cpuAccounting = "Process.TotalProcessorTime is quantized on this Windows host; wall time and parse-count reduction are the primary metrics when candidate CPU rounds to zero.",
        },
        productionIdleModel = new
        {
            loopSleepMs = 10,
            baselineFullParsesPerSecondUpperBound = 100,
            candidateFullParsesPerSecondUpperBound = 4,
            reductionPercent = 96.0,
            note = "Upper bound from 10 ms adapter loop and 250 ms successful-validation cache; actual I/O depends on traffic and reconnect state."
        },
        gameLaunches = 0,
    };
    Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
}
finally
{
    ResetCache();
    Set("runtimeDirectory", string.Empty);
    Set("instanceId", string.Empty);
    try { Directory.Delete(root, recursive: true); } catch { }
}

(double wallMs, double cpuMs, int accepted) Measure(Func<bool> action, int count)
{
    using Process process = Process.GetCurrentProcess();
    TimeSpan cpuBefore = process.TotalProcessorTime;
    var wall = Stopwatch.StartNew();
    int accepted = 0;
    for (int i = 0; i < count; i++)
        if (action()) accepted++;
    wall.Stop();
    process.Refresh();
    double cpu = (process.TotalProcessorTime - cpuBefore).TotalMilliseconds;
    if (accepted != count) throw new InvalidOperationException($"lease accepted {accepted}/{count}");
    return (wall.Elapsed.TotalMilliseconds, cpu, accepted);
}

bool BaselineLeaseIsFresh()
{
    string? session = null;
    long? updatedAt = null;
    var keys = new HashSet<string>(StringComparer.Ordinal);
    foreach (string line in File.ReadAllLines(lease, new UTF8Encoding(false, true)))
    {
        int separator = line.IndexOf('=');
        if (separator <= 0) return false;
        string key = line.Substring(0, separator);
        if (!keys.Add(key)) return false;
        string value = line.Substring(separator + 1);
        if (key == "sessionId") session = value;
        else if (key == "updatedAt" && long.TryParse(value, out long parsed)) updatedAt = parsed;
    }
    if (!string.Equals(session, instance, StringComparison.Ordinal) || updatedAt == null) return false;
    long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    return updatedAt.Value <= now + 5 && now - updatedAt.Value <= 5;
}

void WriteLease() => File.WriteAllText(lease,
    "sessionId=" + instance + "\nchallenge=bench-only\nupdatedAt=" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() + "\n",
    new UTF8Encoding(false));

void ResetCache()
{
    Set("lastValidatedLeaseUpdatedAt", null);
    Set("lastValidatedLeaseInstance", null);
    Set("lastLeaseValidationTimestamp", 0L);
    Set("lastValidatedLeaseLength", 0L);
    Set("lastValidatedLeaseWriteTicks", 0L);
}

void Set(string field, object? value) =>
    typeof(PipeClientAdapter).GetField(field, StaticPrivate)!.SetValue(null, value);
