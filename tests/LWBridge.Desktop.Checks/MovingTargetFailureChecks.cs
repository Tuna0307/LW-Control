using System.Runtime.CompilerServices;

namespace LWBridge.Desktop.Checks;

internal static class MovingTargetFailureChecks
{
    [ModuleInitializer]
    internal static void Run()
    {
        string root = FindRepoRoot();
        string source = File.ReadAllText(Path.Combine(root, "tools", "current_overview_bridge.lua"));
        const string startMarker = "local function pump_march_follow(control)";
        const string endMarker = "local function write_navigation_result(";
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        int end = source.IndexOf(endMarker, start >= 0 ? start : 0, StringComparison.Ordinal);
        Check(start >= 0 && end > start,
            "shipped Overview bridge must retain the bounded march Follow pump");
        string pump = source[start..end];

        string managerLookup = "call(manager, \"GetMarch\", request.marchUuid)";
        string worldLookup = "call(world, \"GetMarch\", request.marchUuid)";
        string identityFailure = "write_march_follow_result(request, \"failed\", \"march_identity_mismatch\")";
        string serverFailure = "write_march_follow_result(request, \"failed\", \"march_server_mismatch\")";
        string proven = "write_march_follow_result(request, \"proven\", nil)";
        string vanished = "write_march_follow_result(request, \"failed\", \"march_follow_completion_timeout\")";

        int managerLookupIndex = pump.IndexOf(managerLookup, StringComparison.Ordinal);
        int worldLookupIndex = pump.IndexOf(worldLookup, StringComparison.Ordinal);
        int identityFailureIndex = pump.IndexOf(identityFailure, StringComparison.Ordinal);
        int serverFailureIndex = pump.IndexOf(serverFailure, StringComparison.Ordinal);
        int observedIndex = pump.IndexOf("if observed then", StringComparison.Ordinal);
        int provenIndex = pump.IndexOf(proven, StringComparison.Ordinal);
        int vanishedIndex = pump.IndexOf(vanished, StringComparison.Ordinal);

        Check(managerLookupIndex >= 0 && worldLookupIndex >= 0,
            "moving Follow must re-resolve the exact requested march UUID from both authoritative live stores");
        Check(identityFailureIndex >= 0 && serverFailureIndex >= 0,
            "moving Follow must reject replaced identity and wrong-server observations explicitly");
        Check(observedIndex >= 0 && provenIndex > observedIndex,
            "moving Follow may report proven only after the exact requested march has been observed");
        Check(vanishedIndex > provenIndex,
            "a march that never becomes observable must end in explicit completion-timeout failure, never stale success");
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "task.md")) &&
                File.Exists(Path.Combine(current.FullName, "tools", "current_overview_bridge.lua")))
                return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("could not locate LW-Control repository root");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(
            "C07 moving-target failure check failed: " + message);
    }
}
