using System.Reflection;
using System.Text.Json;
using LWBridge.Desktop;
using LWBridge.Map317;

// Diagnostic only: all roots are temporary, all acquisition is inert. Reflection
// installs controlled private lifecycle identities/capture handles, never invokes
// private lease-release helpers or repairs a scenario. No game/helper is launched.
string root = Path.Combine(Path.GetTempPath(), "lwb317-lead-recovery002-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var results = new List<object>();
try
{
    string runtime = Path.Combine(root, "overview-runtime");
    Directory.CreateDirectory(runtime);
    string marker = Path.Combine(runtime, "cancel-start.txt");
    string foreign = "schema=1\nsessionId=foreign-session\nchallenge=foreign-challenge\n";
    File.WriteAllText(marker, foreign);
    using (var lifecycle = new OverviewLifecycleService("controlled-A", null,
        startRecoveryMonitor:false, runtimeRoot:runtime, evidenceRoot:Path.Combine(root,"evidence"),
        backupRoot:Path.Combine(root,"backups")))
    {
        Set(lifecycle, "phase", "starting");
        Set(lifecycle, "instanceId", "controlled-session");
        Set(lifecycle, "challenge", "controlled-challenge");
        lifecycle.Close();
    }
    string actual = File.ReadAllText(marker);
    Require(actual != foreign && actual.Contains("controlled-session"), "Expected current active Close overwrite no longer reproduces");
    results.Add(new { name="active-close-overwrites-foreign-start-cancellation", expected=foreign, actual, defectReproduced=true });

    string lease = Path.Combine(runtime,"lease.txt");
    File.WriteAllText(lease,"schema=1\nsessionId=controlled-session\nchallenge=controlled-challenge\n");
    bool replacedBetweenValidationAndDeletion = false;
    var raceHooks = new OverviewLifecycleTestHooks { DeleteFile = path => {
        // Deterministic foreign atomic replacement after the production predicate
        // has matched, immediately before its path-based delete operation.
        File.WriteAllText(path,foreign);
        replacedBetweenValidationAndDeletion = true;
        File.Delete(path);
    }};
    using (var lifecycle = new OverviewLifecycleService("controlled-A",null,testHooks:raceHooks,
        startRecoveryMonitor:false,runtimeRoot:runtime,evidenceRoot:Path.Combine(root,"race-evidence"),
        backupRoot:Path.Combine(root,"race-backups")))
    {
        Set(lifecycle,"instanceId","controlled-session");
        Set(lifecycle,"challenge","controlled-challenge");
        lifecycle.Close();
    }
    Require(replacedBetweenValidationAndDeletion && !File.Exists(lease),"Conditional delete race no longer reproduces");
    results.Add(new {name="foreign-replacement-between-validation-and-path-deletion",
        expectedForeignPreserved=true, actualForeignPreserved=File.Exists(lease),
        proofBoundary="controlled DeleteFile hook models the exact permitted I/O interleaving",defectReproduced=true});

    using var captureCancellation = new CancellationTokenSource();
    using var callerCancellation = new CancellationTokenSource();
    var source = new CurrentClientMapBlockSource(() => null, Path.Combine(root,"source-overview"), Path.Combine(root,"source-probe"));
    using var nativeProvider = new CurrentClientMap317ScanProvider(source);
    Set(nativeProvider, "activeCancellation", captureCancellation);
    Set(nativeProvider, "activeTask", Task.CompletedTask);
    int stopCalls = 0;
    var context = new MapProviderContext(true,true,317,"live",1,100,100,1);
    var adapter = new MapProviderAdapter(_=>ValueTask.FromResult(context), _=>ValueTask.FromResult(context),
        (_,_)=>ValueTask.FromResult(new MapProviderStartResult(true,1,true,0,0,null)),
        token=> { stopCalls++; return nativeProvider.StopMapScanAsync(token); });
    var stateMachine = new MapScanStateMachine(adapter, new CancelBoundarySink(callerCancellation));
    await stateMachine.StartAsync(new MapScanStartRequest(new[]{"city"},"normal"));
    var stopped = await stateMachine.StopAsync(callerCancellation.Token);
    bool captureCancelled = captureCancellation.IsCancellationRequested;
    await stateMachine.StopAsync();
    Require(!stopped.IsReading && !captureCancelled && stopCalls==1,
        "Expected cancel-before-provider Stop / idle non-retry defect no longer reproduces");
    results.Add(new { name="stop-cancelled-after-local-commit-before-provider-entry", expectedCaptureCancellation=true,
        actualCaptureCancellation=captureCancelled, reportedPhase=stopped.Phase, providerStopCallsAfterSecondStop=stopCalls,
        defectReproduced=true });
    // Explicitly terminate only our controlled handle after recording the defect.
    captureCancellation.Cancel();
}
finally
{
    Directory.Delete(root, recursive:true);
}
string resultJson = JsonSerializer.Serialize(new {
    proofType="actual lifecycle Close/current-client Stop/MapScanStateMachine; controlled private identities and inert adapter; no real capture",
    externalActions=0, temporaryRootRemoved=!Directory.Exists(root), results
}, new JsonSerializerOptions{WriteIndented=true});
Console.WriteLine(resultJson);
if(args.Length==1) File.WriteAllText(args[0],resultJson+Environment.NewLine);
static void Require(bool condition,string message) { if(!condition) throw new InvalidOperationException(message); }
static void Set(object owner,string field,object? value) => owner.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(owner,value);
sealed class CancelBoundarySink(CancellationTokenSource caller) : IMapScanLocalSink
{
    public ValueTask CancelScanAsync(string scanRunId,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Models caller cancellation immediately after the committed local cancel.
        caller.Cancel();
        return ValueTask.CompletedTask;
    }
    public ValueTask ClearServerAsync(int serverId,CancellationToken cancellationToken=default) => ValueTask.CompletedTask;
}
