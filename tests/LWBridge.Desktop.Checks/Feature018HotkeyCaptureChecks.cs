using System.Text.Json;
using System.Windows.Forms;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class Feature018HotkeyCaptureChecks
{
    private sealed class StubNativeCommandService : INativeAsyncCommandService
    {
        internal bool Available = true;
        internal string? LastName;
        internal JsonElement LastPayload;
        internal int Calls;
        internal Func<object?> Receipt = () => null;
        public bool CanHandle(string command) =>
            Available && command == "feature018_hotkey_action";

        public Task<object?> InvokeAsync(string command, JsonElement payload, CancellationToken cancellationToken)
        {
            LastName = command;
            LastPayload = payload.Clone();
            Calls++;
            return Task.FromResult(Receipt());
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("FEATURE018_HOTKEY_CAPTURE: " + message);
    }

    private static HashSet<Keys> Down(params Keys[] keys) => [.. keys];

    internal static async Task RunAsync()
    {
        var recognizer = new Feature018HotkeyRecognizer();
        Require(recognizer.Observe(Down(Keys.Q), true, true) is null, "held key on first focus triggered");
        Require(recognizer.Observe(Down(Keys.Q), true, true) is null, "repeat after focus triggered");
        Require(recognizer.Observe(Down(), true, true) is null, "arming released key triggered");
        Require(recognizer.Observe(Down(Keys.Q), true, true) ==
            new Feature018HotkeyIntent(Feature018HotkeyKind.AttackSquad, 1), "Q did not produce attack squad 1");
        Require(recognizer.Observe(Down(Keys.Q), true, true) is null, "repeat press dispatched twice");
        Require(recognizer.Observe(Down(Keys.Q), false, true) is null, "blur dispatch");
        Require(recognizer.Observe(Down(Keys.Q), true, true) is null, "refocus-held dispatch");
        _ = recognizer.Observe(Down(), true, true);
        Require(recognizer.Observe(Down(Keys.F, Keys.ControlKey), true, true) is null,
            "Ctrl+F stole a normal game shortcut");
        _ = recognizer.Observe(Down(), true, true);
        Require(recognizer.Observe(Down(Keys.A, Keys.F), true, true) is null,
            "two keys in same sample must be ambiguous");
        _ = recognizer.Observe(Down(), true, true);
        Require(recognizer.Observe(Down(Keys.G), true, false) is null,
            "shortcut typed into game input");
        Require(recognizer.Observe(Down(Keys.G), true, true) is null,
            "key held from input must not dispatch");
        _ = recognizer.Observe(Down(), true, true);

        var cases = new (Keys Key, Keys? Modifier, Feature018HotkeyKind Kind, int Slot)[]
        {
            (Keys.W, null, Feature018HotkeyKind.AttackSquad, 2),
            (Keys.E, null, Feature018HotkeyKind.AttackSquad, 3),
            (Keys.R, null, Feature018HotkeyKind.AttackSquad, 4),
            (Keys.A, null, Feature018HotkeyKind.RecallSquad, 1),
            (Keys.S, null, Feature018HotkeyKind.RecallSquad, 2),
            (Keys.D, null, Feature018HotkeyKind.RecallSquad, 3),
            (Keys.F, null, Feature018HotkeyKind.RecallSquad, 4),
            (Keys.Space, null, Feature018HotkeyKind.ShieldRemaining, 0),
            (Keys.F6, null, Feature018HotkeyKind.UseShield, 8),
            (Keys.F7, null, Feature018HotkeyKind.UseShield, 12),
            (Keys.F8, null, Feature018HotkeyKind.UseShield, 24),
            (Keys.D1, Keys.Menu, Feature018HotkeyKind.ApplyEquipmentPreset, 1),
            (Keys.D2, Keys.Menu, Feature018HotkeyKind.ApplyEquipmentPreset, 2),
            (Keys.D3, Keys.Menu, Feature018HotkeyKind.ApplyEquipmentPreset, 3),
            (Keys.D4, Keys.Menu, Feature018HotkeyKind.ApplyEquipmentPreset, 4),
            (Keys.F9, null, Feature018HotkeyKind.RandomRelocate, 0),
            (Keys.F10, null, Feature018HotkeyKind.AllianceRelocate, 0),
            (Keys.G, null, Feature018HotkeyKind.ReinforceFrontline, 5),
        };
        foreach (var item in cases)
        {
            _ = recognizer.Observe(Down(), true, true);
            HashSet<Keys> chord = item.Modifier is { } modifier
                ? Down(item.Key, modifier)
                : Down(item.Key);
            Require(recognizer.Observe(chord, true, true) ==
                new Feature018HotkeyIntent(item.Kind, item.Slot),
                $"missed mapping {item.Key} / {item.Modifier}");
        }

        var session = new Feature018HotkeySession("p", "i", "uid", 2212);
        Feature018HotkeySession? current = session;
        bool focused = true;
        HotkeyConfig config = new();
        var seen = new List<Feature018HotkeyCommand>();
        var providers = new Dictionary<Feature018HotkeyKind,
            Func<Feature018HotkeyCommand, CancellationToken, Task<Feature018HotkeyActionOutcome>>>
        {
            [Feature018HotkeyKind.AttackSquad] = (command, _) =>
            {
                seen.Add(command);
                return Task.FromResult(new Feature018HotkeyActionOutcome(
                    Feature018HotkeyActionStatus.Submitted, "only_sent_no_server_confirmation"));
            },
        };
        var coordinator = new Feature018HotkeyActions(session, () => current,
            () => focused, () => config, providers);
        var attack = new Feature018HotkeyIntent(Feature018HotkeyKind.AttackSquad, 1);
        Feature018HotkeyActionOutcome sent = await coordinator.TryDispatchAsync(attack);
        Require(sent.Status == Feature018HotkeyActionStatus.Submitted && !sent.Working,
            "sent message was claimed to be confirmed gameplay");
        Require(seen.Count == 1 && !seen[0].AllowMarchSpeedupItems &&
            !seen[0].AllowMarchSpeedupDiamonds, "original spend-off defaults bypassed");

        config = config with { AttackMarchSpeedupItem = true, AttackMarchSpeedupDiamond = true };
        _ = await coordinator.TryDispatchAsync(attack);
        Require(seen.Count == 2 && seen[1].AllowMarchSpeedupItems &&
            seen[1].AllowMarchSpeedupDiamonds, "opt-in march speedup preferences lost");
        Require((await coordinator.TryDispatchAsync(new(Feature018HotkeyKind.UseShield, 24))).Status ==
            Feature018HotkeyActionStatus.Unsupported, "unknown shield game producer falsely available");
        config = config with { Attack = false };
        Require((await coordinator.TryDispatchAsync(attack)).Status == Feature018HotkeyActionStatus.Disabled,
            "disabled attack invoked producer");
        config = config with { Attack = true };
        focused = false;
        Require((await coordinator.TryDispatchAsync(attack)).Status == Feature018HotkeyActionStatus.NotFocused,
            "unfocused game accepted shortcut");
        focused = true;
        current = session with { GameUid = "different-account" };
        Require((await coordinator.TryDispatchAsync(attack)).Status == Feature018HotkeyActionStatus.SessionChanged,
            "different game account accepted shortcut");
        Require(seen.Count == 2, "blocked shortcut reached action producer");

        var stub = new StubNativeCommandService();
        var wired = new Feature018HotkeyNativeProviders(stub).Create();
        current = session;
        focused = true;
        config = new HotkeyConfig();
        var nativeCoordinator = new Feature018HotkeyActions(session, () => current,
            () => focused, () => config, wired);
        stub.Receipt = () => JsonSerializer.SerializeToElement(new
        {
            observation = "native_MAIN_SHIELD_effect_status_map",
            active = true,
            serverTimeMilliseconds = 1000,
            shieldStatusCount = 1,
        });
        Feature018HotkeyActionOutcome observed = await nativeCoordinator.TryDispatchAsync(
            new(Feature018HotkeyKind.ShieldRemaining));
        Require(observed.Working && observed.Status == Feature018HotkeyActionStatus.Confirmed,
            "native confirmed shield status not admitted");
        Require(stub.LastName == "feature018_hotkey_action" && stub.Calls == 1 &&
            stub.LastPayload.GetProperty("action").GetString() == "shieldOverlay" &&
            stub.LastPayload.GetProperty("slot").GetInt32() == 0,
            "native game request did not use exact feature018 action schema");

        stub.Receipt = () => JsonSerializer.SerializeToElement(new
        {
            observation = "native_MAIN_SHIELD_effect_status_map",
        });
        Require((await nativeCoordinator.TryDispatchAsync(
            new(Feature018HotkeyKind.ShieldRemaining))).Status == Feature018HotkeyActionStatus.Failed,
            "incomplete shield state falsely confirmed");
        stub.Receipt = () => throw new BridgeCommandException(
            "GAME_METHOD_UNAVAILABLE", "no current game function");
        Require((await nativeCoordinator.TryDispatchAsync(
            new(Feature018HotkeyKind.UseShield, 24))).Status == Feature018HotkeyActionStatus.Unsupported,
            "native unsupported shield-use was interpreted as working");
        Require(stub.LastPayload.GetProperty("action").GetString() == "shieldUse" &&
            stub.LastPayload.GetProperty("slot").GetInt32() == 24,
            "shield 24-hour game request lost intent");

        stub.Receipt = () => throw new BridgeCommandException(
            "GAME_ACTION_UNCONFIRMED", "accepted, waiting for server state");
        var notConfirmed = await nativeCoordinator.TryDispatchAsync(
            new(Feature018HotkeyKind.AttackSquad, 2));
        Require(notConfirmed.Status == Feature018HotkeyActionStatus.Submitted && !notConfirmed.Working,
            "native accepted-but-unconfirmed network send falsely marked working");
        Require(stub.LastPayload.GetProperty("action").GetString() == "attack" &&
            stub.LastPayload.GetProperty("slot").GetInt32() == 2,
            "attack game intent was not sent with original squad");

        // Native bridge now returns a correlated state=accepted data body
        // for a source-backed send. It intentionally has no observation yet.
        stub.Receipt = () => JsonSerializer.SerializeToElement(new
        {
            requestDispatched = true,
            awaitingGameConfirmation = true,
            slot = 1,
        });
        Feature018HotkeyActionOutcome sentRecall = await nativeCoordinator.TryDispatchAsync(
            new(Feature018HotkeyKind.RecallSquad, 1));
        Require(sentRecall.Status == Feature018HotkeyActionStatus.Submitted &&
                !sentRecall.Working,
            "native send-only recall receipt was not preserved as Submitted");
        Require(stub.LastPayload.GetProperty("action").GetString() == "recall" &&
                stub.LastPayload.GetProperty("slot").GetInt32() == 1,
            "native recall call lost its owner squad slot");

        int beforeSpend = stub.Calls;
        config = config with { AttackMarchSpeedupDiamond = true };
        Require((await nativeCoordinator.TryDispatchAsync(
            new(Feature018HotkeyKind.AttackSquad, 3))).Status == Feature018HotkeyActionStatus.Unsupported &&
            stub.Calls == beforeSpend,
            "speedup diamond opt-in silently ignored during native dispatch");
        stub.Available = false;
        Require((await nativeCoordinator.TryDispatchAsync(
            new(Feature018HotkeyKind.RecallSquad, 1))).Status == Feature018HotkeyActionStatus.Unsupported,
            "missing native game transport was accepted");

        foreach ((Feature018HotkeyKind key, string nativeAction) in new[]
        {
            (Feature018HotkeyKind.RecallSquad, "recall"),
            (Feature018HotkeyKind.ApplyEquipmentPreset, "equipment"),
            (Feature018HotkeyKind.RandomRelocate, "randomRelocate"),
            (Feature018HotkeyKind.AllianceRelocate, "allianceRelocate"),
            (Feature018HotkeyKind.ReinforceFrontline, "frontlineReinforce"),
        })
        {
            Require(Feature018HotkeyNativeProviders.ActionName(key) == nativeAction,
                "native action mapping differs from game bridge schema: " + key);
        }

        Console.WriteLine("FEATURE018_HOTKEY_CAPTURE_PASS");
    }
}
