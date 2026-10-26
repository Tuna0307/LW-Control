using System.Text.Json;
using System.Text.Json.Nodes;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class Feature018RuntimeStartupChecks
{
    internal static async Task RunAsync()
    {
        var a = new Feature018StartupOwner(new("a", "s", "nonce", 42, "game.exe", "start"), new("123", 2212));
        var b = a with { Session = a.Session with { SessionId = "next", GameStartedAtUtc = "next-start" } };
        Feature018StartupOwner? current = null;
        var tasks = JsonNode.Parse("""
            {"monsterSweep":{"enabled":true,"allianceDrill":{"enabled":true}},"staminaPotion":{"enabled":true}}
            """)!.AsObject();
        var calls = new List<(Feature018StartupOwner Owner, string Command, string Payload)>();
        bool failAutomation = true;
        string afkState = "idle";
        var runtime = new Feature018RuntimeStartup(() => current, () => tasks,
            () => JsonSerializer.SerializeToElement(new { showFps = true, showPing = false }),
            (owner, command, payload, _) =>
            {
                calls.Add((owner, command, payload.GetRawText()));
                if (command == "feature018_automation_status" && failAutomation)
                    throw new BridgeCommandException("GAME_CONNECTION_UNAVAILABLE", "cold startup");
                return Task.FromResult<object?>(JsonSerializer.SerializeToElement(new { state = afkState }));
            });
        await runtime.SynchronizeAsync(default);
        Require(calls.Count == 0, "No ready owner must mean zero native requests.");
        current = a;
        await runtime.SynchronizeAsync(default);
        Require(calls.Any(x => x.Command == "feature018_afk_configure") &&
            calls.Count(x => x.Command == "automation_start") == 2,
            "Saved engines must initialize without any page mounts; one failed subsystem cannot block others.");
        Require(runtime.Snapshot["errors"]?["automation"] is not null, "Startup errors must remain inspectable.");
        Require(calls.All(x => x.Owner == a), "Every command must carry the captured full owner.");
        Require(!calls.Any(x => x.Command.Contains("scan_start") || x.Command.Contains("run") || x.Command.Contains("equipment")),
            "Startup must not run manual features.");
        failAutomation = false;
        int first = calls.Count;
        await runtime.SynchronizeAsync(default);
        Require(calls.Count == first + 2 && calls.TakeLast(2).All(x => x.Command is "feature018_automation_status" or "feature018_chat_status"),
            "Retry only the failed/idempotent sync; do not restart already initialized controllers.");
        Require(runtime.Snapshot["errors"]?["automation"] is null, "A successful retry clears its own error.");
        current = null;
        await runtime.SynchronizeAsync(default);
        Require(runtime.Snapshot["ready"]!.GetValue<bool>() == false && calls.Count == first + 2,
            "An authority gap sends nothing.");
        current = a;
        await runtime.SynchronizeAsync(default);
        Require(calls.Count(x => x.Command == "automation_start") == 2,
            "A same-session reconnect must not reset pending stamina/Drill work.");
        current = b; afkState = "completed";
        Require(runtime.ChatSnapshot is null && runtime.Snapshot["ready"]!.GetValue<bool>() == false,
            "A new owner must not observe the previous owner's cached initialization or Chat result.");
        await runtime.SynchronizeAsync(default);
        Require(calls.Count(x => x.Command == "feature018_afk_configure") == 1,
            "Completed AFK work must not restart, including a lost-acknowledgement result.");

        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        current = a;
        var delayedCalls = new List<string>();
        var delayed = new Feature018RuntimeStartup(() => current, () => tasks,
            () => JsonSerializer.SerializeToElement(new { showFps = false, showPing = false }),
            async (_, command, _, _) => { delayedCalls.Add(command);entered.TrySetResult();await released.Task;return null; });
        var pending = delayed.SynchronizeAsync(default);
        await entered.Task; current = b; released.SetResult();await pending;
        Require(delayedCalls.Count == 1 && delayed.Snapshot["ready"]!.GetValue<bool>() == false,
            "A retired completion must not apply or send the remaining startup commands into a successor.");

        calls.Clear();current = a;
        var initiallyOff = new JsonObject();
        var allOff = new Feature018RuntimeStartup(() => current, () => initiallyOff,
            () => JsonSerializer.SerializeToElement(new { showFps = false, showPing = false }),
            (_, command, _, _) => { calls.Add((a, command, ""));return Task.FromResult<object?>(null); });
        await allOff.SynchronizeAsync(default);
        Require(calls.Count == 3 && calls.All(x => x.Command is "feature018_automation_status" or "feature018_chat_status" or "feature018_visual_metrics_configure"),
            "Disabled saved features must never issue a controller Start.");
        initiallyOff["staminaPotion"] = new JsonObject { ["enabled"] = true };
        await allOff.SynchronizeAsync(default);
        Require(calls.Count(x => x.Command == "automation_start") == 1,
            "A delayed saved-enabled snapshot must initialize without a page visit.");
        Console.WriteLine("FEATURE018_STARTUP_CHECKS_PASS headless enabled/off retry no-replay session-retirement saved-settings");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
