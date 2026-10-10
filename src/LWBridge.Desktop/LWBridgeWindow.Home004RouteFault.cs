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
        home004RouteFaultTimer.Tick += (_, _) => ProcessHome004IsolatedRouteFault();
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

    private void ProcessHome004IsolatedRouteFault()
    {
        string? requestPath = home004RouteFaultRequestPath;
        if (requestPath is null || !File.Exists(requestPath) || sessionClosed)
            return;

        // Rename first: a request is consumed at most once even if validation
        // fails or an external controller retries the same file.
        string consumedPath = requestPath + ".consumed";
        if (File.Exists(consumedPath)) return;
        try
        {
            File.Move(requestPath, consumedPath);
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllBytes(consumedPath));
            JsonElement request = doc.RootElement;
            string? operation = request.GetProperty("operation").GetString();
            string? profile = request.GetProperty("profileId").GetString();
            string? instance = request.GetProperty("instanceId").GetString();
            int pid = request.GetProperty("gamePid").GetInt32();
            OverviewLifecycleService? lifecycle = overviewLifecycleService;
            JsonElement status = JsonSerializer.SerializeToElement(lifecycle?.CreateProfileInstanceStatus());
            if (operation != "drop-exact-authenticated-route" ||
                lifecycle is null || profile != backend.ProfileId ||
                status.ValueKind != JsonValueKind.Object ||
                status.GetProperty("instanceId").GetString() != instance ||
                status.GetProperty("pid").GetInt32() != pid ||
                status.GetProperty("phase").GetString() != "running" ||
                status.GetProperty("connectionState").GetString() != "connected" ||
                !lifecycle.OwnsExactProcess(pid) || !lifecycle.IsReady ||
                string.IsNullOrWhiteSpace(instance) || bridgeHostState is null)
                throw new InvalidOperationException("Request did not match a fresh, exact, authenticated task-owned game session.");

            bool closed = bridgeHostState.DisconnectExactAuthenticatedRouteForIsolatedTest(instance);
            if (!closed || bridgeHostState.IsRouteConnected(instance))
                throw new InvalidOperationException("Requested authenticated session was not retired.");
            WriteHome004RouteFaultReceipt(consumedPath, true, "exact authenticated pipe closed", instance, pid);
        }
        catch (Exception error)
        {
            // Instrumentation failure never invokes an actual game lifecycle
            // action and must remain visible to the isolated controller.
            WriteHome004RouteFaultReceipt(consumedPath, false, error.GetType().Name + ": " + error.Message, null, null);
        }
    }

    private static void WriteHome004RouteFaultReceipt(
        string consumedPath, bool succeeded, string message, string? instance, int? pid)
    {
        try
        {
            File.WriteAllText(consumedPath + ".result.json", JsonSerializer.Serialize(new
            {
                proof = "HOME004_F04_ISOLATED_AUTHENTICATED_ROUTE_DROP",
                utc = DateTimeOffset.UtcNow.ToString("O"),
                succeeded,
                message,
                instanceId = instance,
                gamePid = pid,
                // No challenge, pipe token, player information or credentials.
            }, JsonOptions.Default));
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
