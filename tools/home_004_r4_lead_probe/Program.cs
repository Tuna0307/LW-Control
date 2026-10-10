using System.Reflection;
using System.Text.Json;

// Independent inverse through the existing inert fixture and actual production
// lifecycle, including its protected adoption/registration seam. No game/GUI.
Assembly tests = Assembly.Load("LWBridge.Desktop.Checks");
Type caseType = tests.GetType(
    "LWBridge.Desktop.Checks.HomeR2RecoveryChecks+Case", throwOnError: true)!;
const BindingFlags instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
using var test = (IDisposable)Activator.CreateInstance(caseType, instance, null,
    ["lead-r4-stale-pending-stop", true, true], null)!;
object Read(string name) => caseType.GetField(name, instance)!.GetValue(test)!;
void Write(string name, object value) => caseType.GetField(name, instance)!.SetValue(test, value);
Task Run(string name) => (Task)caseType.GetMethod(name, instance)!.Invoke(test, null)!;
void Advance(long value) => caseType.GetMethod("Advance", instance)!.Invoke(test, [value]);
await Run("Start");
string oldInstanceId = (string)Read("Session");
Write("Alive", false);
await Run("Observe");
Advance(2_000);
await Run("Observe");
var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
Write("HeldRecoveryStart", held);
Write("RecoveryStartReached", reached);
Task pending = Run("Tick");
await reached.Task.WaitAsync(TimeSpan.FromSeconds(5));
object service = Read("Service");
object config = Read("Config");
bool Desired() => (bool)config.GetType().GetProperty("Snapshot")!.GetValue(config)!
    .GetType().GetProperty("GameDesiredRunning")!.GetValue(
        config.GetType().GetProperty("Snapshot")!.GetValue(config)!)!;
string CurrentInstance() => JsonSerializer.SerializeToElement(
    service.GetType().GetMethod("CreateProfileInstanceStatus", instance)!.Invoke(service, null))
    .GetProperty("instanceId").GetString()!;
string pendingInstanceId = CurrentInstance();
if (oldInstanceId == pendingInstanceId || !Desired())
    throw new InvalidOperationException("Inverse precondition: new pending owner must differ from old owner.");
string? rejectedCode = null;
try
{
    Task<object?> stop = (Task<object?>)service.GetType().GetMethod("InvokeAsync")!
        .Invoke(service,
            ["profile_instance_stop", JsonSerializer.SerializeToElement(new { instanceId = oldInstanceId }),
                CancellationToken.None])!;
    await stop;
}
catch (Exception error)
{
    Exception actual = error is TargetInvocationException reflection ? reflection.InnerException! : error;
    rejectedCode = actual.GetType().GetProperty("Code")?.GetValue(actual)?.ToString()
        ?? actual.GetType().Name;
}
bool desiredAfterStaleStop = Desired();
held.TrySetResult();
await pending.WaitAsync(TimeSpan.FromSeconds(5));
var result = new
{
    proof = "HOME004_R4_LEAD_STALE_PENDING_STOP_INVERSE",
    gameLaunches = 0,
    desktopActions = 0,
    usesActualProductionLifecycle = true,
    oldAndPendingOwnerDiffer = oldInstanceId != pendingInstanceId,
    rejectedCode,
    desiredAfterStaleStop,
    recoveryState = service.GetType().GetProperty("CurrentRecoveryStatus", instance)!
        .GetValue(service)!.GetType().GetProperty("State")!.GetValue(
            service.GetType().GetProperty("CurrentRecoveryStatus", instance)!.GetValue(service)!),
    defectReproduced = rejectedCode is null && !desiredAfterStaleStop,
};
Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
if (!result.defectReproduced)
    throw new InvalidOperationException("Recorded stale pending Stop inverse did not reproduce.");

// The WebView dispatch currently captures only the selected backend. Exercise
// that backend's actual explicit-profile validation versus B's actual owner.
using var sibling = (IDisposable)Activator.CreateInstance(caseType, instance, null,
    ["lead-r4-sibling", true, false], null)!;
await (Task)caseType.GetMethod("Start", instance)!.Invoke(sibling, null)!;
object bConfig = caseType.GetField("Config", instance)!.GetValue(sibling)!;
object bService = caseType.GetField("Service", instance)!.GetValue(sibling)!;
string bProfile = (string)caseType.GetField("Profile", instance)!.GetValue(sibling)!;
string bSession = (string)caseType.GetField("Session", instance)!.GetValue(sibling)!;
Type backendType = service.GetType().Assembly.GetType("LWBridge.Desktop.LWBridgeBackend", true)!;
object Backend(object ownerConfig, object ownerService, string ownRoot)
{
    ConstructorInfo ctor = backendType.GetConstructors().Single();
    object?[] args = ctor.GetParameters().Select(parameter => parameter.DefaultValue).ToArray();
    foreach ((ParameterInfo parameter, int index) in ctor.GetParameters().Select((p, i) => (p, i)))
        args[index] = parameter.Name switch
        {
            "config" => ownerConfig,
            "overviewLifecycle" => ownerService,
            "profileRuntimeDirectory" => Path.Combine(ownRoot, "backend-runtime"),
            "applicationDataRoot" => ownRoot,
            _ => args[index],
        };
    return ctor.Invoke(args);
}
object selectedBackend = Backend(config, service, (string)Read("Root"));
object bBackend = Backend(bConfig, bService, (string)caseType.GetField("Root", instance)!.GetValue(sibling)!);
async Task<object?> Status(object backend) => await (Task<object?>)backendType.GetMethod("InvokeAsync")!
    .Invoke(backend, ["profile_instance_status", JsonSerializer.SerializeToElement(new { profileId = bProfile }),
        CancellationToken.None])!;
string? selectedBackendError = null;
try { _ = await Status(selectedBackend); }
catch (Exception error)
{
    Exception actual = error is TargetInvocationException reflection ? reflection.InnerException! : error;
    selectedBackendError = actual.GetType().GetProperty("Code")?.GetValue(actual)?.ToString();
}
JsonElement bStatus = JsonSerializer.SerializeToElement(await Status(bBackend));
bool explicitOwnerWorks = bStatus.GetProperty("instanceId").GetString() == bSession;
Console.WriteLine(JsonSerializer.Serialize(new
{
    proof = "HOME004_R4_LEAD_SELECTED_BACKEND_SCOPE_INVERSE",
    gameLaunches = 0,
    desktopActions = 0,
    actualSelectedBackendError = selectedBackendError,
    actualTargetBackendReturnsOwnInstance = explicitOwnerWorks,
    defectReproduced = selectedBackendError == "PROFILE_RUNTIME_UNAVAILABLE" && explicitOwnerWorks,
}, new JsonSerializerOptions { WriteIndented = true }));
if (selectedBackendError != "PROFILE_RUNTIME_UNAVAILABLE" || !explicitOwnerWorks)
    throw new InvalidOperationException("Recorded selected-backend routing inverse did not reproduce.");
