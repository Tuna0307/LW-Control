using System.Text.Json;

namespace LWBridge.Desktop;

// Explicit one-shot engineering instrument for a genuine F-04 native fault.
// The switch requires BOTH a task-owned --isolated-root and an opt-in env var.
// The request lives only inside that root; no network settings, game files,
// bridge credentials, or other profile connections are changed.
internal sealed partial class LWBridgeWindow
{
    private System.Windows.Forms.Timer? home004RouteFaultTimer;
    private string? home004RouteFaultRequestPath;
    private string? home004F06DispatchPath;
    private bool home004RouteFaultBusy;

    private void InitializeHome004IsolatedRouteFault(string? isolatedRootPath)
    {
        if (isolatedRootPath is not null && productionPaths is not null &&
            Environment.GetEnvironmentVariable("LWBRIDGE_HOME004_F06_DISPATCH_TRACE") == "1")
            home004F06DispatchPath = Path.Combine(productionPaths.Root, "home004-f06-dispatch.jsonl");
        if (isolatedRootPath is null || productionPaths is null ||
            Environment.GetEnvironmentVariable("LWBRIDGE_HOME004_ISOLATED_ROUTE_FAULT") != "1")
            return;

        string root = Path.GetFullPath(isolatedRootPath);
        if (!Path.GetFullPath(productionPaths.Root).Equals(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Home004 fault root and current isolated application root differ.");
        home004RouteFaultRequestPath = Path.Combine(root, "home004-drop-route.request.json");
        home004RouteFaultTimer = new System.Windows.Forms.Timer { Interval = 500 };
        home004RouteFaultTimer.Tick += async (_, _) => await ProcessHome004IsolatedRouteFaultAsync();
        home004RouteFaultTimer.Start();
    }

    private void RecordHome004F06Dispatch(string phase, string command)
    {
        if (home004F06DispatchPath is null ||
            command is not ("profile_instances_update_and_restart" or "profile_instance_start" or "profile_instance_stop"))
            return;
        try
        {
            File.AppendAllText(home004F06DispatchPath,
                JsonSerializer.Serialize(new { utc = DateTimeOffset.UtcNow.ToString("O"), phase, command }) +
                Environment.NewLine);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private async Task ProcessHome004IsolatedRouteFaultAsync()
    {
        string? requestPath = home004RouteFaultRequestPath;
        if (requestPath is null || !File.Exists(requestPath) || sessionClosed || home004RouteFaultBusy)
            return;

        home004RouteFaultBusy = true;
        // Rename first: a request is consumed at most once even if validation
        // fails or an external controller retries the same file.
        string consumedPath = requestPath + ".consumed";
        try
        {
            if (File.Exists(consumedPath)) return;
            File.Move(requestPath, consumedPath);
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllBytes(consumedPath));
            JsonElement request = doc.RootElement;
            string? operation = request.GetProperty("operation").GetString();
            string? profile = request.GetProperty("profileId").GetString();
            OverviewLifecycleService? lifecycle = overviewLifecycleService;

            if (operation == "start-exact-isolated-profile")
            {
                if (lifecycle is null || profile != backend.ProfileId ||
                    lifecycle.CreateProfileInstanceStatus() is not null)
                    throw new InvalidOperationException(
                        "Start request did not match an idle isolated profile with no active/recovering owner.");
                using JsonDocument empty = JsonDocument.Parse("{}");
                await lifecycle.InvokeAsync(
                    "profile_instance_start", empty.RootElement.Clone(), CancellationToken.None);
                JsonElement started = JsonSerializer.SerializeToElement(
                    lifecycle.CreateProfileInstanceStatus());
                string? startedInstance = started.ValueKind == JsonValueKind.Object &&
                    started.TryGetProperty("instanceId", out JsonElement instanceValue) &&
                    instanceValue.ValueKind == JsonValueKind.String
                        ? instanceValue.GetString()
                        : null;
                int? startedPid = started.ValueKind == JsonValueKind.Object &&
                    started.TryGetProperty("pid", out JsonElement pidValue) &&
                    pidValue.TryGetInt32(out int parsedPid)
                        ? parsedPid
                        : null;
                if (started.ValueKind != JsonValueKind.Object ||
                    started.GetProperty("phase").GetString() != "running" ||
                    started.GetProperty("connectionState").GetString() != "connected" ||
                    string.IsNullOrWhiteSpace(startedInstance) ||
                    startedPid is null || startedPid <= 0 ||
                    !lifecycle.OwnsExactProcess(startedPid.Value) || !lifecycle.IsReady)
                    throw new InvalidOperationException(
                        "Production lifecycle Start returned without a fresh exact connected owner.");
                WriteHome004RouteFaultReceipt(
                    consumedPath, true, "isolated production lifecycle Start completed",
                    startedInstance, startedPid, operation: operation,
                    lifecycleStartCompleted: true);
                return;
            }

            string? instance = request.GetProperty("instanceId").GetString();
            int pid = request.GetProperty("gamePid").GetInt32();
            JsonElement status = JsonSerializer.SerializeToElement(lifecycle?.CreateProfileInstanceStatus());
            bool forceReplacement = operation == "drop-exact-authenticated-route";
            bool retainRegistration =
                operation == "drop-exact-authenticated-route-retain-registration";
            bool retainThenStop =
                operation == "drop-exact-authenticated-route-retain-registration-and-stop";
            bool exactStop = operation == "stop-exact-owned-instance";
            bool requiresFreshConnectedRoute = forceReplacement || retainRegistration || retainThenStop;
            if ((!requiresFreshConnectedRoute && !exactStop) ||
                lifecycle is null || profile != backend.ProfileId ||
                status.ValueKind != JsonValueKind.Object ||
                status.GetProperty("instanceId").GetString() != instance ||
                status.GetProperty("pid").GetInt32() != pid ||
                status.GetProperty("phase").GetString() != "running" ||
                !lifecycle.OwnsExactProcess(pid) || string.IsNullOrWhiteSpace(instance))
                throw new InvalidOperationException(
                    "Request did not match the exact task-owned game session.");

            if (exactStop)
            {
                await lifecycle.InvokeAsync(
                    "profile_instance_stop",
                    JsonSerializer.SerializeToElement(new { instanceId = instance }),
                    CancellationToken.None);
                WriteHome004RouteFaultReceipt(
                    consumedPath, true, "exact production lifecycle Stop completed",
                    instance, pid, operation: operation, lifecycleStopCompleted: true);
                return;
            }

            if (status.GetProperty("connectionState").GetString() != "connected" ||
                !lifecycle.IsReady || bridgeHostState is null)
                throw new InvalidOperationException(
                    "Route fault request requires a fresh exact authenticated connected session.");

            bool closed = forceReplacement
                ? bridgeHostState.DisconnectExactAuthenticatedRouteForIsolatedTest(instance)
                : bridgeHostState.DisconnectExactAuthenticatedRouteRetainingRegistrationForIsolatedTest(instance);
            if (!closed || (forceReplacement && bridgeHostState.IsRouteConnected(instance)))
                throw new InvalidOperationException("Requested authenticated session was not closed.");
            string routeDroppedUtc = DateTimeOffset.UtcNow.ToString("O");
            if (retainThenStop)
            {
                await lifecycle.InvokeAsync(
                    "profile_instance_stop",
                    JsonSerializer.SerializeToElement(new { instanceId = instance }),
                    CancellationToken.None);
                WriteHome004RouteFaultReceipt(
                    consumedPath, true,
                    "exact authenticated pipe closed with registration retained; exact production lifecycle Stop completed",
                    instance, pid, retainedRegistration: true, operation: operation,
                    routeDroppedUtc: routeDroppedUtc, lifecycleStopCompleted: true);
                return;
            }
            WriteHome004RouteFaultReceipt(
                consumedPath,
                true,
                forceReplacement
                    ? "exact authenticated pipe closed; registration retired"
                    : "exact authenticated pipe closed; claimed registration retained",
                instance,
                pid,
                retainedRegistration: retainRegistration,
                operation: operation,
                routeDroppedUtc: routeDroppedUtc);
        }
        catch (Exception error)
        {
            // Instrumentation failure never invokes an actual game lifecycle
            // action and must remain visible to the isolated controller.
            WriteHome004RouteFaultReceipt(consumedPath, false, error.GetType().Name + ": " + error.Message, null, null);
        }
        finally
        {
            home004RouteFaultBusy = false;
        }
    }

    private static void WriteHome004RouteFaultReceipt(
        string consumedPath, bool succeeded, string message, string? instance, int? pid,
        bool retainedRegistration = false, string? operation = null,
        string? routeDroppedUtc = null, bool lifecycleStartCompleted = false,
        bool lifecycleStopCompleted = false)
    {
        try
        {
            File.WriteAllText(consumedPath + ".result.json", JsonSerializer.Serialize(new
            {
                proof = "HOME004_F04_ISOLATED_AUTHENTICATED_ROUTE_DROP",
                utc = DateTimeOffset.UtcNow.ToString("O"),
                succeeded,
                message,
                operation,
                instanceId = instance,
                gamePid = pid,
                retainedRegistration,
                routeDroppedUtc,
                lifecycleStartCompleted,
                lifecycleStopCompleted,
                // No challenge, pipe token, player information or credentials.
            }, JsonOptions.Default));
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
