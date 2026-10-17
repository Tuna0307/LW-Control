using System.Text.Json;
using System.Text.Json.Nodes;
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

    // Exercises the real key admission path with inert HWNDs and a fake Win32
    // stream. No foreground window or physical keyboard state is ever changed.
    private sealed class FakeOwnedKeyPlatform : Feature018OwnedWindowKeys.IPlatform
    {
        internal bool ProcessMatches = true;
        internal nint Foreground = (nint)101;
        internal nint DiscoveredWindow = (nint)101;
        internal readonly HashSet<nint> OwnedRoots = [(nint)101];
        internal bool CanActivate;
        internal bool KeyboardFocusOwned = true;
        internal byte? RejectDownKey;
        internal byte? RejectUpKey;
        internal bool SuppressKeyDownState;
        internal bool SuppressKeyUpState;
        internal int ActivationCalls;
        internal int DiscoveryCalls;
        internal readonly List<(byte Key, bool Release)> SendAttempts = [];
        internal readonly List<int> Delays = [];
        internal readonly HashSet<Keys> DownKeys = [];
        internal Action<int, CancellationToken>? OnDelay;
        internal Func<int, CancellationToken, Task>? OnAsyncDelay;

        public bool SameProcess(OverviewMapScanSession _) => ProcessMatches;
        public nint ForegroundWindow() => Foreground;
        public nint FindOwnedWindow(int _) { DiscoveryCalls++; return DiscoveredWindow; }
        public bool IsOwnedRootWindow(nint hwnd, int _) => OwnedRoots.Contains(hwnd);
        public bool IsKeyboardFocusInOwnedWindow(nint _, int __) => KeyboardFocusOwned;
        public bool ActivateWindow(nint hwnd)
        {
            ActivationCalls++;
            if (CanActivate) Foreground = hwnd;
            return CanActivate;
        }
        public IReadOnlySet<Keys> WatchedKeysDown() => DownKeys.ToHashSet();
        public bool SendKey(byte key, bool release)
        {
            SendAttempts.Add((key, release));
            bool accepted = release ? RejectUpKey != key : RejectDownKey != key;
            if (accepted && !(release ? SuppressKeyUpState : SuppressKeyDownState))
            {
                if (release) DownKeys.Remove((Keys)key);
                else DownKeys.Add((Keys)key);
            }
            return accepted;
        }
        public Task Delay(int milliseconds, CancellationToken token)
        {
            Delays.Add(milliseconds);
            OnDelay?.Invoke(milliseconds, token);
            token.ThrowIfCancellationRequested();
            return OnAsyncDelay?.Invoke(milliseconds, token) ?? Task.CompletedTask;
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

        // R2 uses SendInput-style key edges through an exact task-owned game
        // window. These key plans must agree with the actual native recognizer
        // for every original binding before any live game focus is considered.
        var ownedKeyPlans = new (string Binding, byte[] Expected)[] {
            ("Q", [0x51]), ("W", [0x57]), ("E", [0x45]), ("R", [0x52]),
            ("A", [0x41]), ("S", [0x53]), ("D", [0x44]), ("F", [0x46]),
            ("Space", [0x20]), ("F6", [0x75]), ("F7", [0x76]), ("F8", [0x77]),
            ("Alt+1", [0x12, 0x31]), ("Alt+2", [0x12, 0x32]),
            ("Alt+3", [0x12, 0x33]), ("Alt+4", [0x12, 0x34]),
            ("F9", [0x78]), ("F10", [0x79]), ("G", [0x47]),
        };
        Require(ownedKeyPlans.Length == 19, "original key plan catalogue lost an expected binding");
        foreach ((string binding, byte[] expected) in ownedKeyPlans)
        {
            Require(Feature018OwnedWindowKeys.TryKeyPlan(binding, out byte[] actual) &&
                    actual.SequenceEqual(expected), "task-owned game key plan differs from original mapping: " + binding);
        }
        foreach (string unsupported in new[] { "", "Ctrl+F", "Shift+Q", "Alt+F6", "Alt+5", "G+F", "Space+Q" })
        {
            Require(!Feature018OwnedWindowKeys.TryKeyPlan(unsupported, out byte[] actual) &&
                    actual.Length == 0, "unknown chord was eligible for task-owned injection: " + unsupported);
        }
        await OwnedWindowAdmissionChecksAsync();

        var session = new Feature018HotkeySession("p", "i", "123456789", 2212);
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
            observation = "unrelated_native_observation",
            active = true,
            serverTimeMilliseconds = 1000,
            shieldStatusCount = 1,
        });
        Require((await nativeCoordinator.TryDispatchAsync(
            new(Feature018HotkeyKind.ShieldRemaining))).Status == Feature018HotkeyActionStatus.Failed,
            "unrelated observation was accepted as an authenticated shield status");
        stub.Receipt = () => JsonSerializer.SerializeToElement(new
        {
            observation = "native_MAIN_SHIELD_effect_status_map",
            active = false,
            serverTimeMilliseconds = 1000,
            shieldStatusCount = 1,
        });
        Require((await nativeCoordinator.TryDispatchAsync(
            new(Feature018HotkeyKind.ShieldRemaining))).Status == Feature018HotkeyActionStatus.Failed,
            "contradictory shield count and active flag accepted");

        stub.Receipt = () => JsonSerializer.SerializeToElement(new
        {
            observation = "native_world_march_return_target",
            slot = 2,
            marchUuid = "803001",
            formationUuid = "1001",
            target = "BACK_HOME",
            returning = true,
        });
        Require((await nativeCoordinator.TryDispatchAsync(
            new(Feature018HotkeyKind.RecallSquad, 2))).Working,
            "source-observed owner squad return-home state rejected");
        Require((await nativeCoordinator.TryDispatchAsync(
            new(Feature018HotkeyKind.RecallSquad, 3))).Status == Feature018HotkeyActionStatus.Failed,
            "another squad slot's return-home receipt falsely confirmed shortcut");

        stub.Receipt = () => JsonSerializer.SerializeToElement(new
        {
            observation = "native_world_march_return_target",
            slot = 2,
            marchUuid = "803001",
            formationUuid = "1001",
            target = "BACK_HOME",
            returning = true,
        });
        Require((await nativeCoordinator.TryDispatchAsync(
            new(Feature018HotkeyKind.AttackSquad, 2))).Status == Feature018HotkeyActionStatus.Failed,
            "recall observation falsely confirmed a current-client attack");

        stub.Receipt = () => JsonSerializer.SerializeToElement(new
        {
            observation = "native_ParkourBattleLogic_GetMemberCount",
            action = "frontlineReinforce",
            slot = 5,
            heroId = 1301,
            beforeCount = 7,
            afterCount = 12,
            addedMembers = 5,
            unitIdentityVerified = true,
            observedCreatedUnits = 5,
            unitIdentitySource = "native_ParkourTeam_teamUnits_guid",
        });
        Require((await nativeCoordinator.TryDispatchAsync(
            new(Feature018HotkeyKind.ReinforceFrontline, 5))).Working,
            "native five-member Frontline source observation was not admitted");
        stub.Receipt = () => JsonSerializer.SerializeToElement(new
        {
            observation = "native_ParkourBattleLogic_GetMemberCount",
            action = "frontlineReinforce",
            slot = 5,
            heroId = 1301,
            beforeCount = 7,
            afterCount = 11,
            addedMembers = 5,
            unitIdentityVerified = true,
            observedCreatedUnits = 5,
            unitIdentitySource = "native_ParkourTeam_teamUnits_guid",
        });
        Require((await nativeCoordinator.TryDispatchAsync(
            new(Feature018HotkeyKind.ReinforceFrontline, 5))).Status == Feature018HotkeyActionStatus.Failed,
            "four soldier additions falsely confirmed five-member Frontline action");
        stub.Receipt = () => JsonSerializer.SerializeToElement(new
        {
            observation = "native_ParkourBattleLogic_GetMemberCount",
            action = "frontlineReinforce", slot = 5, heroId = 1301,
            beforeCount = 7, afterCount = 12, addedMembers = 5,
            unitIdentityVerified = false, observedCreatedUnits = 5,
            unitIdentitySource = "native_ParkourTeam_teamUnits_guid",
        });
        Require((await nativeCoordinator.TryDispatchAsync(
            new(Feature018HotkeyKind.ReinforceFrontline, 5))).Status == Feature018HotkeyActionStatus.Failed,
            "count-only Frontline afterstate falsely admitted without independently verified unit GUIDs");

        stub.Receipt = () => JsonSerializer.SerializeToElement(new
        {
            observation = "native_owned_shield_consumption_and_MAIN_SHIELD",
            slot = 8,
            itemUuid = "101001",
            itemId = "9",
            statusId = 501,
            durationMilliseconds = 28800000L,
            consumedCount = 1,
            remainingMilliseconds = 28799000,
            serverTimeMilliseconds = 1000000,
        });
        Require((await nativeCoordinator.TryDispatchAsync(
            new(Feature018HotkeyKind.UseShield, 8))).Working,
            "owned eight-hour inventory and shield status observation was not admitted");
        Require((await nativeCoordinator.TryDispatchAsync(
            new(Feature018HotkeyKind.UseShield, 12))).Status == Feature018HotkeyActionStatus.Failed,
            "eight-hour shield observation falsely confirmed a twelve-hour action");

        int beforePoll = stub.Calls;
        stub.Receipt = () => stub.LastPayload.TryGetProperty("queryOnly", out JsonElement only) &&
                              only.ValueKind == JsonValueKind.True
            ? JsonSerializer.SerializeToElement(new
            {
                observation = "native_world_march_return_target",
                slot = 2,
                marchUuid = "803001",
                formationUuid = "1001",
                target = "BACK_HOME",
                returning = true,
            })
            : JsonSerializer.SerializeToElement(new
            {
                requestDispatched = true,
                awaitingGameConfirmation = true,
                slot = 2,
                marchUuid = "803001",
            });
        Feature018HotkeyActionOutcome polledRecall = await nativeCoordinator.TryDispatchAsync(
            new(Feature018HotkeyKind.RecallSquad, 2));
        Require(polledRecall.Working && stub.Calls == beforePoll + 2 &&
                stub.LastPayload.GetProperty("queryOnly").GetBoolean(),
            "recall did not query exactly once after a single native return-home send");

        beforePoll = stub.Calls;
        stub.Receipt = () => stub.LastPayload.TryGetProperty("queryOnly", out JsonElement only) &&
                              only.ValueKind == JsonValueKind.True
            ? JsonSerializer.SerializeToElement(new
            {
                observation = "native_owned_shield_consumption_and_MAIN_SHIELD",
                slot = 24,
                itemUuid = "101003",
                itemId = "11",
                statusId = 503,
                durationMilliseconds = 86400000L,
                consumedCount = 1,
                remainingMilliseconds = 86399000,
                serverTimeMilliseconds = 1000000,
            })
            : JsonSerializer.SerializeToElement(new
            {
                requestDispatched = true,
                awaitingGameConfirmation = true,
                slot = 24,
                itemUuid = "101003",
                itemId = "11",
            });
        Feature018HotkeyActionOutcome polledShield = await nativeCoordinator.TryDispatchAsync(
            new(Feature018HotkeyKind.UseShield, 24));
        Require(polledShield.Working && stub.Calls == beforePoll + 2 &&
                stub.LastPayload.GetProperty("queryOnly").GetBoolean(),
            "shield did not query exactly once after a single native ItemUse send");

        foreach (string action in new[] { "recall", "shieldUse" })
        {
            var validQuery = JsonSerializer.SerializeToElement(new
            {
                action, slot = action == "recall" ? 2 : 24, queryOnly = true,
            });
            Require(Feature018GameCommandService.ReadFields("feature018_hotkey_action",
                validQuery).TryGetValue("queryOnly", out string? readOnly) &&
                readOnly == "1", "native action queryOnly flag was dropped: " + action);
        }
        foreach (string action in new[] { "shieldOverlay", "frontlineReinforce" })
        {
            try
            {
                _ = Feature018GameCommandService.ReadFields("feature018_hotkey_action",
                    JsonSerializer.SerializeToElement(new
                    {
                        action, slot = 1, queryOnly = true,
                    }));
                throw new InvalidOperationException("unverified queryOnly action accepted: " + action);
            }
            catch (BridgeCommandException error) when (error.Code == "INVALID_REQUEST") { }
        }

        stub.Receipt = () => JsonSerializer.SerializeToElement(new
        {
            accepted = true,
            awaitingGameConfirmation = true,
        });
        Feature018HotkeyActionOutcome accepted = await nativeCoordinator.TryDispatchAsync(
            new(Feature018HotkeyKind.RecallSquad, 2));
        Require(accepted.Status == Feature018HotkeyActionStatus.Submitted && !accepted.Working,
            "game-native accepted receipt was falsely promoted to confirmed gameplay");

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
            stub.LastPayload.GetProperty("slot").GetInt32() == 2 &&
            stub.LastPayload.TryGetProperty("authorizedUid", out JsonElement sentUid) &&
            sentUid.GetString() == session.GameUid,
            "attack game intent lost its original squad or authenticated owner UID: " +
            stub.LastPayload.GetRawText());

        var validAttackFields = Feature018GameCommandService.ReadFields("feature018_hotkey_action",
            JsonSerializer.SerializeToElement(new { action = "attack", slot = 2,
                authorizedUid = session.GameUid, allowRapidItem = true,
                allowRapidDiamond = false }));
        Require(validAttackFields["authorizedUid"] == session.GameUid &&
                validAttackFields["slot"] == "2",
            "native attack lost authenticated numeric owner UID");
        Require(validAttackFields["allowRapidItem"] == "1" &&
            !validAttackFields.ContainsKey("allowRapidDiamond"),
            "attack speedup preference consent was lost or false preference became a spend");
        var attackStatus = Feature018GameCommandService.ReadFields("feature018_hotkey_action",
            JsonSerializer.SerializeToElement(new { action = "attack", slot = 2,
                authorizedUid = session.GameUid, queryOnly = true }));
        Require(attackStatus["queryOnly"] == "1" && !attackStatus.ContainsKey("allowRapidItem"),
            "attack confirmation query added new spending consent");
        var equipmentStatus = Feature018GameCommandService.ReadFields("feature018_hotkey_action",
            JsonSerializer.SerializeToElement(new { action = "equipment", slot = 2, queryOnly = true }));
        Require(equipmentStatus["queryOnly"] == "1", "equipment native poststate polling was rejected");
        var diamondUse = Feature018GameCommandService.ReadFields("feature018_hotkey_action",
            JsonSerializer.SerializeToElement(new { action = "speedupDiamond", slot = 2,
                authorizedUid = session.GameUid, itemId = "9999", diamondOptIn = true }));
        Require(diamondUse["diamondOptIn"] == "1" && diamondUse["itemId"] == "9999",
            "gem speedup lost explicit native template or consent");
        var validRapid = Feature018GameCommandService.ReadFields("feature018_hotkey_action",
            JsonSerializer.SerializeToElement(new { action = "speedupItem", slot = 2,
                authorizedUid = session.GameUid, itemOptIn = true, itemId = "9001" }));
        Require(validRapid["itemOptIn"] == "1" && validRapid["itemId"] == "9001" &&
                validRapid["authorizedUid"] == session.GameUid,
            "owned speedup item lost explicit opt-in and source ID");
        var readOnlyRapid = Feature018GameCommandService.ReadFields("feature018_hotkey_action",
            JsonSerializer.SerializeToElement(new { action = "speedupItem", slot = 2,
                authorizedUid = session.GameUid, queryOnly = true }));
        Require(readOnlyRapid["queryOnly"] == "1" && !readOnlyRapid.ContainsKey("itemOptIn"),
            "read-only item-speedup status silently requested a new consumable");
        var readInventory = Feature018GameCommandService.ReadFields("feature018_rapid_action",
            JsonSerializer.SerializeToElement(new { mode = "inventory", slot = 1 }));
        Require(readInventory.Count == 2 && readInventory["mode"] == "inventory" &&
                readInventory["slot"] == "1",
            "explicit owned-march rapid inventory added spending or an untrusted UID");
        var itemUse = Feature018GameCommandService.ReadFields("feature018_rapid_action",
            JsonSerializer.SerializeToElement(new
            {
                mode = "itemUse", slot = 4, itemOptIn = true, itemId = "331008",
            }));
        Require(itemUse["mode"] == "itemUse" && itemUse["slot"] == "4" &&
                itemUse["itemOptIn"] == "1" && itemUse["itemId"] == "331008" &&
                !itemUse.ContainsKey("authorizedUid"),
            "new explicit rapid request must select one item and resolve game UID natively");
        var rapidStatus = Feature018GameCommandService.ReadFields("feature018_rapid_action",
            JsonSerializer.SerializeToElement(new { mode = "status", slot = 4 }));
        Require(rapidStatus.Count == 2 && !rapidStatus.ContainsKey("itemOptIn") &&
                !rapidStatus.ContainsKey("itemId"),
            "rapid status polling must be read-only");
        foreach (string raw in new[]
        {
            """{"mode":"itemUse","slot":1,"itemId":"331008","itemOptIn":false}""",
            """{"mode":"itemUse","slot":1,"itemId":"331008"}""",
            """{"mode":"itemUse","slot":1,"itemId":"-1","itemOptIn":true}""",
            """{"mode":"inventory","slot":1,"itemOptIn":true}""",
            """{"mode":"status","slot":2,"itemId":"331008"}""",
            """{"mode":"itemUse","slot":0,"itemId":"331008","itemOptIn":true}""",
            """{"mode":"itemUse","slot":5,"itemId":"331008","itemOptIn":true}""",
            """{"mode":"buyDiamond","slot":1}""",
        })
        {
            try
            {
                using JsonDocument invalidRapid = JsonDocument.Parse(raw);
                _ = Feature018GameCommandService.ReadFields("feature018_rapid_action",
                    invalidRapid.RootElement);
                throw new InvalidOperationException("Unsafe item rapid action admitted: " + raw);
            }
            catch (BridgeCommandException error) when (error.Code == "INVALID_REQUEST") { }
        }
        foreach (var invalid in new[]
        {
            new { action = "attack", slot = 1, authorizedUid = "", itemOptIn = false, itemId = "", queryOnly = false },
            new { action = "attack", slot = 1, authorizedUid = "bad-uid", itemOptIn = false, itemId = "", queryOnly = false },
            new { action = "speedupItem", slot = 1, authorizedUid = session.GameUid, itemOptIn = false, itemId = "9001", queryOnly = false },
            new { action = "speedupItem", slot = 1, authorizedUid = session.GameUid, itemOptIn = true, itemId = "bad-item", queryOnly = false },
            new { action = "speedupDiamond", slot = 1, authorizedUid = session.GameUid, itemOptIn = false, itemId = "", queryOnly = false },
        })
        {
            try
            {
                _ = Feature018GameCommandService.ReadFields("feature018_hotkey_action",
                    JsonSerializer.SerializeToElement(invalid));
                throw new InvalidOperationException("invalid native attack/rapid request admitted: " + invalid.action);
            }
            catch (BridgeCommandException error) when (error.Code == "INVALID_REQUEST") { }
        }

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
            new(Feature018HotkeyKind.AttackSquad, 3))).Status == Feature018HotkeyActionStatus.Submitted &&
            stub.Calls == beforeSpend + 1 &&
            stub.LastPayload.GetProperty("allowRapidDiamond").GetBoolean(),
            "speedup gem consent was dropped or native send falsely confirmed");
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

        // The visible Space overlay requires the *actual* returned MAIN_SHIELD
        // timing fields, including absence of a shield. An arbitrary confirmed
        // observation or multiple contradictory shield timers is insufficient.
        JsonElement timed = JsonSerializer.SerializeToElement(new
        {
            observation = "native_MAIN_SHIELD_effect_status_map",
            active = true, shieldStatusCount = 1, remainingMilliseconds = 3500000
        });
        Require(Feature018ShieldStatusOverlay.TryRead(timed, out bool active, out double remaining) &&
            active && remaining == 3500000, "native shield timer was not renderable");
        JsonElement absent = JsonSerializer.SerializeToElement(new
        {
            observation = "native_MAIN_SHIELD_effect_status_map",
            active = false, shieldStatusCount = 0, remainingMilliseconds = 0
        });
        Require(Feature018ShieldStatusOverlay.TryRead(absent, out active, out remaining) &&
            !active && remaining == 0, "shield-free native status misrepresented");
        foreach (var negative in new[]
        {
            new { observation = "unverified", active = true, shieldStatusCount = 1, remainingMilliseconds = 3500000.0 },
            new { observation = "native_MAIN_SHIELD_effect_status_map", active = true, shieldStatusCount = 2, remainingMilliseconds = 3500000.0 },
            new { observation = "native_MAIN_SHIELD_effect_status_map", active = true, shieldStatusCount = 1, remainingMilliseconds = -1.0 },
        })
        {
            Require(!Feature018ShieldStatusOverlay.TryRead(
                JsonSerializer.SerializeToElement(negative), out _, out _),
                "unknown or contradictory shield timer displayed");
        }

        await EquipmentReadOnlyPreflightChecksAsync();
        Console.WriteLine("FEATURE018_HOTKEY_CAPTURE_PASS");
    }

    private static async Task OwnedWindowAdmissionChecksAsync()
    {
        Require(Feature018OwnedWindowKeys.NativeInputSize == (IntPtr.Size == 8 ? 40 : 28),
            "Win32 INPUT union has the wrong native SendInput marshaling size");
        Require(Feature018OwnedWindowKeys.NativeGuiThreadInfoSize == (IntPtr.Size == 8 ? 72 : 48),
            "Win32 GUITHREADINFO focus guard has the wrong native size");
        static bool KeysMatch(string binding, int phase, params Keys[] held)
        {
            Require(Feature018OwnedWindowKeys.TryKeyPlan(binding, out byte[] keys),
                "test phase uses invalid hotkey binding");
            return Feature018OwnedWindowKeys.MatchesWatchedKeyState(Down(held), keys, phase);
        }
        Require(KeysMatch("Q", 0) && KeysMatch("Q", 1, Keys.Q) &&
                KeysMatch("F6", 1, Keys.F6) &&
                !KeysMatch("Q", 1, Keys.Q, Keys.F) &&
                !KeysMatch("F6", 1, Keys.F7) &&
                !KeysMatch("Q", 1, Keys.Q, Keys.ShiftKey) &&
                !KeysMatch("Q", 1) &&
                KeysMatch("Alt+1", 1, Keys.LMenu, Keys.Menu) &&
                KeysMatch("Alt+1", 2, Keys.Menu, Keys.D1) &&
                KeysMatch("Alt+1", 2, Keys.LMenu, Keys.Menu, Keys.D1) &&
                !KeysMatch("Alt+1", 1, Keys.D1) &&
                !KeysMatch("Alt+1", 2, Keys.Menu) &&
                !KeysMatch("Alt+1", 2, Keys.Menu, Keys.D2) &&
                !KeysMatch("Alt+1", 2, Keys.Menu, Keys.D1, Keys.ControlKey),
            "native GetAsyncKeyState plan matcher admitted a false shortcut chord");
        var owner = new OverviewMapScanSession("owner-profile", "launch-session",
            "owner-challenge", 43210, @"C:\fake\LastWar.exe", "2026-10-17T00:00:00Z");
        static async Task<Feature018OwnedKeyReceipt> Press(
            OverviewMapScanSession session, FakeOwnedKeyPlatform platform,
            string binding = "Q", Func<bool>? approved = null,
            Func<bool>? textInputClear = null, CancellationToken token = default) =>
            await Feature018OwnedWindowKeys.PressWithPlatformAsync(
                session, binding, approved ?? (() => true),
                textInputClear ?? (() => true), platform, token);

        var api = new FakeOwnedKeyPlatform();
        Feature018OwnedKeyReceipt result = await Press(owner, api);
        Require(result.Pressed && result.Hwnd == 101 &&
                result.Observation == "win32_key_state_observed_for_owned_game_keyboard_focus" &&
                result.InputBaselineObservedAtUtc <= result.InputInjectionStartedAtUtc &&
                result.InputInjectionStartedAtUtc <= result.KeyDownObservedAtUtc &&
                result.KeyDownObservedAtUtc is not null &&
                result.KeyDownLastObservedAtUtc >= result.KeyDownObservedAtUtc &&
                result.KeyDownStateSamples == 3 &&
                result.KeyReleaseObservedAtUtc >= result.KeyDownLastObservedAtUtc &&
                result.KeyReleaseObservedAtUtc >= result.KeyDownObservedAtUtc &&
                api.ActivationCalls == 0 && api.DiscoveryCalls == 0 &&
                api.Delays.SequenceEqual(new[] { 420, 35, 35, 45, 45, 95, 70 }) &&
                api.SendAttempts.SequenceEqual(new[] { ((byte)0x51, false), ((byte)0x51, true) }),
            "already-foreground exact HWND lacked temporally distinct Q down samples");

        // Concurrent proof callers share one process-level Win32 input stream.
        // Do not let Alt+number and Q interleave into another chord or silently
        // queue a second action whose native result cannot be attributed.
        api = new FakeOwnedKeyPlatform();
        var held = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        api.OnAsyncDelay = (ms, token) => ms == 420
            ? held.Task.WaitAsync(token) : Task.CompletedTask;
        Task<Feature018OwnedKeyReceipt> first = Press(owner, api, "Alt+2");
        Require(!first.IsCompleted && api.Delays.SequenceEqual(new[] { 420 }) &&
                api.SendAttempts.Count == 0,
            "first owned game key request failed to hold its input admission lease");
        var concurrent = new FakeOwnedKeyPlatform();
        Feature018OwnedKeyReceipt overlap = await Press(owner, concurrent, "Q");
        Require(!overlap.Pressed && overlap.Hwnd == 0 &&
                overlap.Observation == "owned_game_key_press_already_in_progress" &&
                concurrent.ActivationCalls == 0 && concurrent.DiscoveryCalls == 0 &&
                concurrent.Delays.Count == 0 && concurrent.SendAttempts.Count == 0,
            "overlapping independent Q sent OS input while an Alt chord was in flight");
        held.SetResult(true);
        result = await first;
        Require(result.Pressed && api.SendAttempts.SequenceEqual(new[] {
                    ((byte)0x12, false), ((byte)0x32, false),
                    ((byte)0x32, true), ((byte)0x12, true) }),
            "active Alt chord was corrupted by denied overlapping key attempt");
        result = await Press(owner, concurrent, "F6");
        Require(result.Pressed && concurrent.SendAttempts.SequenceEqual(new[] {
                    ((byte)0x75, false), ((byte)0x75, true) }),
            "finished key attempt leaked its one-at-a-time admission lease");

        api = new FakeOwnedKeyPlatform();
        using (var cancelledWaiting = new CancellationTokenSource())
        {
            var blocked = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            api.OnAsyncDelay = (ms, token) => ms == 420
                ? blocked.Task.WaitAsync(token) : Task.CompletedTask;
            Task<Feature018OwnedKeyReceipt> interrupted =
                Press(owner, api, "Alt+1", token: cancelledWaiting.Token);
            Require(!interrupted.IsCompleted, "cancellation fixture did not suspend admission");
            cancelledWaiting.Cancel();
            try
            {
                await interrupted;
                throw new InvalidOperationException("cancelled admission unexpectedly returned");
            }
            catch (OperationCanceledException) { }
            Require(api.SendAttempts.Count == 0, "cancelled admission injected an action");
        }
        result = await Press(owner, new FakeOwnedKeyPlatform(), "Q");
        Require(result.Pressed, "cancelled admission retained a stale global key lease");

        // A second visible top-level window of the *same* verified game process
        // can legitimately have focus. Never seize an arbitrary first sibling.
        api = new FakeOwnedKeyPlatform { Foreground = (nint)202 };
        api.OwnedRoots.Add((nint)202);
        result = await Press(owner, api, "Alt+2");
        Require(result.Pressed && result.Hwnd == 202 &&
                result.InputInjectionStartedAtUtc <= result.KeyDownObservedAtUtc &&
                result.KeyDownStateSamples == 3 &&
                api.Delays.SequenceEqual(new[] { 420, 35, 35, 55, 45, 45, 95, 70 }) &&
                result.KeyReleaseObservedAtUtc is not null &&
                api.ActivationCalls == 0 && api.DiscoveryCalls == 0 &&
                api.SendAttempts.SequenceEqual(new[] {
                    ((byte)0x12, false), ((byte)0x32, false),
                    ((byte)0x32, true), ((byte)0x12, true) }),
            "focused owned sibling HWND must be preferred with bounded Alt chord cleanup");

        api = new FakeOwnedKeyPlatform { Foreground = (nint)999 };
        result = await Press(owner, api);
        Require(!result.Pressed && result.Observation == "game_foreground_activation_denied" &&
                api.ActivationCalls == 1 && api.SendAttempts.Count == 0,
            "Windows foreground denial was bypassed by keyboard injection");

        api = new FakeOwnedKeyPlatform { Foreground = (nint)999, CanActivate = true };
        result = await Press(owner, api);
        Require(result.Pressed && api.ActivationCalls == 1 && api.Foreground == (nint)101,
            "legitimately activated owned game HWND was rejected");

        api = new FakeOwnedKeyPlatform();
        result = await Press(owner, api, approved: () => false);
        Require(!result.Pressed && result.Observation == "approved_game_identity_unavailable" &&
                api.DiscoveryCalls == 0 && api.SendAttempts.Count == 0,
            "unauthenticated account reached any owned game input");
        api = new FakeOwnedKeyPlatform { ProcessMatches = false };
        result = await Press(owner, api);
        Require(!result.Pressed && result.Observation == "approved_game_identity_unavailable" &&
                api.DiscoveryCalls == 0 && api.SendAttempts.Count == 0,
            "replaced or unverified game PID reached input");

        api = new FakeOwnedKeyPlatform { Foreground = (nint)999, DiscoveredWindow = (nint)222 };
        result = await Press(owner, api);
        Require(!result.Pressed && result.Observation == "owned_game_window_unavailable" &&
                api.ActivationCalls == 0 && api.SendAttempts.Count == 0,
            "unowned/hidden/non-root HWND was eligible for activation");

        api = new FakeOwnedKeyPlatform();
        api.OnDelay = (ms, _) => { if (ms == 420) api.Foreground = (nint)999; };
        result = await Press(owner, api);
        Require(!result.Pressed && result.Observation == "owned_game_window_or_account_changed" &&
                api.SendAttempts.Count == 0,
            "focus loss before capture arming reached a key send");

        api = new FakeOwnedKeyPlatform();
        result = await Press(owner, api, textInputClear: () => false);
        Require(!result.Pressed && result.Observation == "game_text_input_focus_could_not_be_qualified" &&
                api.SendAttempts.Count == 0,
            "stale native game-input focus was treated as capture readiness");

        api = new FakeOwnedKeyPlatform();
        int alternatingInputSamples = 0;
        result = await Press(owner, api,
            textInputClear: () => ++alternatingInputSamples % 2 == 1);
        Require(!result.Pressed && result.Observation == "game_input_admission_not_stable" &&
                api.SendAttempts.Count == 0 && alternatingInputSamples == 75 &&
                api.Delays.Count(delay => delay == 35) == 74,
            "one timely native EventSystem input-clear sample falsely armed capture");

        api = new FakeOwnedKeyPlatform();
        int delayedNativeAdmission = 0;
        result = await Press(owner, api,
            textInputClear: () => ++delayedNativeAdmission >= 3);
        Require(result.Pressed && delayedNativeAdmission >= 5 &&
                api.Delays.Take(5).SequenceEqual(new[] { 420, 35, 35, 35, 35 }) &&
                result.InputBaselineObservedAtUtc is not null,
            "late native EventSystem readiness skipped full recognizer arming interval");
        api = new FakeOwnedKeyPlatform();
        api.DownKeys.Add(Keys.Q);
        result = await Press(owner, api);
        Require(!result.Pressed && result.Observation == "game_keyboard_baseline_could_not_be_qualified" &&
                api.SendAttempts.Count == 0,
            "held pre-existing hotkey could be injected over without recognizer arming");

        api = new FakeOwnedKeyPlatform { KeyboardFocusOwned = false };
        result = await Press(owner, api);
        Require(!result.Pressed &&
                result.Observation == "game_keyboard_focus_could_not_be_qualified" &&
                api.SendAttempts.Count == 0,
            "game HWND foreground while keyboard focus belongs elsewhere was admitted");
        api = new FakeOwnedKeyPlatform();
        api.OnDelay = (ms, _) => { if (ms == 55) api.KeyboardFocusOwned = false; };
        result = await Press(owner, api, "Alt+1");
        Require(!result.Pressed && result.Observation == "owned_window_changed_before_chord" &&
                api.SendAttempts.SequenceEqual(new[] {
                    ((byte)0x12, false), ((byte)0x12, true) }),
            "owned game foreground with focus switched away during Alt injected a number");
        api = new FakeOwnedKeyPlatform();
        api.OnDelay = (ms, _) => { if (ms == 95) api.KeyboardFocusOwned = false; };
        result = await Press(owner, api, "Q");
        Require(!result.Pressed && result.Observation == "owned_window_changed_during_keypress" &&
                api.SendAttempts.SequenceEqual(new[] {
                    ((byte)0x51, false), ((byte)0x51, true) }),
            "owned game foreground with focus switched away during Q claimed key intent: " +
            result.Observation + " (" + string.Join(",", api.Delays) + ")");

        api = new FakeOwnedKeyPlatform();
        api.OnDelay = (ms, _) => { if (ms == 70) api.Foreground = (nint)999; };
        result = await Press(owner, api, "Q");
        Require(!result.Pressed &&
                result.Observation == "owned_window_changed_during_key_release" &&
                result.KeyDownStateSamples == 3 &&
                result.KeyReleaseObservedAtUtc is null &&
                api.SendAttempts.SequenceEqual(new[] {
                    ((byte)0x51, false), ((byte)0x51, true) }),
            "foreground loss after three down samples falsely claimed a completed key");

        api = new FakeOwnedKeyPlatform { SuppressKeyDownState = true };
        result = await Press(owner, api, "F6");
        Require(!result.Pressed && result.Observation == "win32_keydown_state_not_observed" &&
                api.SendAttempts.SequenceEqual(new[] {
                    ((byte)0x75, false), ((byte)0x75, true) }),
            "accepted Win32 F6 insertion without observed pressed state claimed a shortcut");

        api = new FakeOwnedKeyPlatform();
        int downObservations = 0;
        api.OnDelay = (ms, _) =>
        {
            if (ms == 45 && ++downObservations == 2) api.DownKeys.Clear();
        };
        result = await Press(owner, api, "F6");
        Require(!result.Pressed && result.Observation == "win32_keydown_state_not_observed" &&
                downObservations == 2 &&
                api.SendAttempts.SequenceEqual(new[] {
                    ((byte)0x75, false), ((byte)0x75, true) }),
            "missing second independent F6 down state incorrectly claimed an intent witness");
        api = new FakeOwnedKeyPlatform { SuppressKeyDownState = true };
        result = await Press(owner, api, "Alt+2");
        Require(!result.Pressed && result.Observation == "win32_modifier_state_not_observed" &&
                api.SendAttempts.SequenceEqual(new[] {
                    ((byte)0x12, false), ((byte)0x12, true) }),
            "accepted Win32 Alt insertion without modifier state claimed a chord");

        api = new FakeOwnedKeyPlatform { SuppressKeyUpState = true };
        result = await Press(owner, api, "Q");
        Require(!result.Pressed && result.Observation == "win32_keyup_state_not_observed" &&
                result.KeyDownObservedAtUtc is not null &&
                result.KeyReleaseObservedAtUtc is null,
            "completed Win32 keyup without observed release falsely claimed completed press");

        api = new FakeOwnedKeyPlatform { RejectDownKey = 0x51 };
        result = await Press(owner, api);
        Require(!result.Pressed && result.Observation == "win32_sendinput_keydown_rejected" &&
                api.SendAttempts.SequenceEqual(new[] { ((byte)0x51, false) }),
            "SendInput rejection was falsely reported as a physical press");
        api = new FakeOwnedKeyPlatform { RejectDownKey = 0x12 };
        result = await Press(owner, api, "Alt+1");
        Require(!result.Pressed && result.Observation == "win32_sendinput_modifier_rejected" &&
                api.SendAttempts.SequenceEqual(new[] { ((byte)0x12, false) }),
            "modifier SendInput rejection still injected the chord");

        api = new FakeOwnedKeyPlatform();
        api.OnDelay = (ms, _) => { if (ms == 55) api.Foreground = (nint)999; };
        result = await Press(owner, api, "Alt+1");
        Require(!result.Pressed && result.Observation == "owned_window_changed_before_chord" &&
                api.SendAttempts.SequenceEqual(new[] {
                    ((byte)0x12, false), ((byte)0x12, true) }),
            "focus lost after Alt down still injected the primary action key");

        api = new FakeOwnedKeyPlatform();
        api.OnDelay = (ms, _) => { if (ms == 95) api.Foreground = (nint)999; };
        result = await Press(owner, api);
        Require(!result.Pressed && result.Observation == "owned_window_changed_during_keypress" &&
                api.SendAttempts.SequenceEqual(new[] {
                    ((byte)0x51, false), ((byte)0x51, true) }),
            "lost focus during keydown was reported as eligible completed input");

        api = new FakeOwnedKeyPlatform();
        using var cancelled = new CancellationTokenSource();
        api.OnDelay = (ms, _) => { if (ms == 55) cancelled.Cancel(); };
        try
        {
            await Press(owner, api, "Alt+1", token: cancelled.Token);
            throw new InvalidOperationException("cancelled chord input did not throw");
        }
        catch (OperationCanceledException) { }
        Require(api.SendAttempts.SequenceEqual(new[] {
                    ((byte)0x12, false), ((byte)0x12, true) }),
            "cancellation after Alt down left an injected modifier held");

        api = new FakeOwnedKeyPlatform { RejectUpKey = 0x12 };
        try
        {
            await Press(owner, api, "Alt+1");
            throw new InvalidOperationException("rejected Win32 Alt release falsely reported success");
        }
        catch (InvalidOperationException error) when (
            error.Message == "win32_sendinput_keyup_rejected") { }
        Require(api.SendAttempts.SequenceEqual(new[] {
                    ((byte)0x12, false), ((byte)0x31, false),
                    ((byte)0x31, true), ((byte)0x12, true) }),
            "failed release hid or skipped the primary/Alt cleanup attempts");
        Console.WriteLine("FEATURE018_OWNED_WINDOW_ADMISSION_PASS");
    }

    private static async Task EquipmentReadOnlyPreflightChecksAsync()
    {
        const string savedPreset = """
            {"equipmentPresets":[
              {"id":"gear-owner-1","name":"Owned loadout",
               "squads":[{"squadIndex":1,"positions":[
                 {"position":1,"equips":[{"slot":1,"equipUuid":"9007199254740993"}]}
               ]}]},
              {"id":"gear-owner-2","name":"Alternate",
               "squads":[{"squadIndex":2,"positions":[
                 {"position":1,"equips":[]}
               ]}]}
            ]}
            """;
        string root = Path.Combine(Path.GetTempPath(),
            "feature018-equipment-owner-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new ProfileRuntimeConfigStore(Path.Combine(root, "config.json"));
            var equipment = new EquipmentConfigCommandService(store);
            string request = Path.Combine(root, "native-request.txt");
            string response = Path.Combine(root, "native-response.json");
            var owner = new OverviewMapScanSession("owner-profile", "launch-session",
                "owner-challenge", 43210, @"C:\\fake\\LastWar.exe", "2026-10-17T00:00:00Z");
            OverviewMapScanSession? current = owner;
            string responseState = "confirmed";
            bool staleConfigDuringReply = false;
            bool forgedApplied = false;
            Dictionary<string, string>? transmitted = null;
            var fakeGame = new Feature018OwnedGameExchange(
                () => current, session => current == session, request, response,
                delay: (_, _) =>
                {
                    transmitted = File.ReadAllLines(request)
                        .Where(line => line.Contains('='))
                        .Select(line => line.Split('=', 2))
                        .ToDictionary(parts => parts[0], parts => parts[1]);
                    if (staleConfigDuringReply)
                    {
                        JsonObject newer = JsonNode.Parse(savedPreset)!.AsObject();
                        newer["equipmentPresets"]![0]!["id"] = "gear-newer";
                        store.SaveEquipmentConfig(newer);
                    }
                    object receipt = forgedApplied
                        ? new
                        {
                            observation = "native_HeroEquip_slot_inventory_preflight",
                            readOnly = true, readyToApply = false,
                            requestDispatched = true, action = "equipment",
                            slot = int.Parse(transmitted["slot"]),
                            presetId = transmitted["presetId"],
                            applied = 1, slots = new[] { new { slot = 1 } },
                        }
                        : new
                        {
                            observation = "native_HeroEquip_slot_inventory_preflight",
                            readOnly = true, readyToApply = false,
                            requestDispatched = false, action = "equipment",
                            slot = int.Parse(transmitted["slot"]),
                            presetId = transmitted["presetId"],
                            slots = new[] { new
                            {
                                squadIndex = 1, position = 1, heroUuid = "5001", slot = 1,
                                currentEquipUuid = "9007199254740993",
                                targetEquipUuid = "9007199254740993", state = "alreadyEquipped",
                            }},
                        };
                    File.WriteAllText(response, JsonSerializer.Serialize(new
                    {
                        schemaVersion = 1,
                        requestId = transmitted["requestId"],
                        profileId = transmitted["profileId"],
                        launchSessionId = transmitted["launchSessionId"],
                        challenge = transmitted["challenge"],
                        gamePid = int.Parse(transmitted["gamePid"]),
                        command = transmitted["command"],
                        observedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        state = responseState,
                        data = receipt,
                    }));
                    return Task.CompletedTask;
                });
            var game = new Feature018GameCommandService(fakeGame, equipment);
            var absent = new Feature018GameCommandService(fakeGame,
                new EquipmentConfigCommandService(new ProfileRuntimeConfigStore(
                    Path.Combine(root, "missing.json"))));
            JsonElement first = JsonSerializer.SerializeToElement(new { slot = 1 });
            JsonElement second = JsonSerializer.SerializeToElement(new { slot = 2 });

            Require(game.CanHandle("feature018_equipment_preflight") &&
                    !game.CanHandle("feature018_equipment_apply"),
                "only read-only native equipment command must be registered");
            await ExpectEquipmentErrorAsync("GAME_METHOD_UNAVAILABLE", () =>
                new Feature018GameCommandService(fakeGame).InvokeAsync(
                    "feature018_equipment_preflight", first, CancellationToken.None));
            await ExpectEquipmentErrorAsync("GAME_PRECONDITION_FAILED", () =>
                absent.InvokeAsync("feature018_equipment_preflight", first,
                    CancellationToken.None));
            Require(!File.Exists(request),
                "missing owner storage reached the native game exchange");

            foreach (string invalid in new[]
            {
                """{"slot":0}""", """{"slot":5}""",
                """{"slot":1.5}""", """{"slot":"1"}""",
                """{"slot":true}""", """{}""",
                """{"slot":1,"equipmentPresets":[]}""",
                """{"slot":1,"confirmedEquipmentPresets":"fake"}""",
                """{"slot":1,"authorizedUid":"forged"}""",
                """{"slot":1,"presetId":"other"}""",
                """{"slot":1,"itemOptIn":true}""",
                """{"slot":1,"itemId":"5001"}""",
                """{"slot":1,"queryOnly":false}""",
                """{"slot":1,"slot":2}""",
            })
            {
                using JsonDocument parsed = JsonDocument.Parse(invalid);
                await ExpectEquipmentErrorAsync("INVALID_REQUEST", () =>
                    game.InvokeAsync("feature018_equipment_preflight",
                        parsed.RootElement, CancellationToken.None));
            }
            Require(!File.Exists(request),
                "forged or invalid equipment parameters reached the game mailbox");

            store.SaveEquipmentConfig(JsonNode.Parse(savedPreset)!.AsObject());
            Dictionary<string, string> fields =
                await game.ReadConfirmedEquipmentPreflightFieldsAsync(first,
                    CancellationToken.None);
            Require(fields.Count == 4 && fields["slot"] == "1" &&
                    fields["action"] == "equipment" &&
                    fields["presetId"] == "gear-owner-1" &&
                    fields["confirmedEquipmentPresets"].Length <= 14000 &&
                    !fields.ContainsKey("authorizedUid") &&
                    !fields.ContainsKey("equipUuid") && !fields.ContainsKey("itemId") &&
                    !fields.ContainsKey("itemOptIn"),
                "owner-confirmed source was lost or an untrusted UID/spend leaked");
            using (JsonDocument parsed = JsonDocument.Parse(fields["confirmedEquipmentPresets"]))
            {
                Require(parsed.RootElement.GetArrayLength() == 2 &&
                        parsed.RootElement[0].GetProperty("id").GetString() == "gear-owner-1" &&
                        parsed.RootElement[0].GetProperty("squads")[0]
                            .GetProperty("positions")[0].GetProperty("equips")[0]
                            .GetProperty("equipUuid").GetString() == "9007199254740993",
                    "saved gear UUID or ordered confirmed profile preset lost in native JSON");
            }

            JsonElement observation = (JsonElement)(await game.InvokeAsync(
                "feature018_equipment_preflight", first, CancellationToken.None))!;
            Require(observation.GetProperty("readOnly").GetBoolean() &&
                    !observation.GetProperty("requestDispatched").GetBoolean() &&
                    transmitted is not null &&
                    transmitted["confirmedEquipmentPresets"] == fields["confirmedEquipmentPresets"] &&
                    transmitted["presetId"] == "gear-owner-1" &&
                    transmitted["slot"] == "1" &&
                    transmitted["command"] == "feature018_equipment_preflight" &&
                    transmitted["profileId"] == owner.ProfileId &&
                    !transmitted.ContainsKey("authorizedUid") &&
                    !transmitted.ContainsKey("equipmentApplied") &&
                    !transmitted.ContainsKey("itemOptIn"),
                "read-only owner-confirmed preflight was not emitted verbatim");

            fields = await game.ReadConfirmedEquipmentPreflightFieldsAsync(second,
                CancellationToken.None);
            Require(fields["presetId"] == "gear-owner-2",
                "Alt+2 did not map to the second owner-confirmed preset");
            await game.InvokeAsync("feature018_equipment_preflight", second,
                CancellationToken.None);
            await ExpectEquipmentErrorAsync("GAME_PRECONDITION_FAILED", () =>
                game.InvokeAsync("feature018_equipment_preflight",
                    JsonSerializer.SerializeToElement(new { slot = 3 }),
                    CancellationToken.None));

            forgedApplied = true;
            await ExpectEquipmentErrorAsync("GAME_ACTION_UNCONFIRMED", () =>
                game.InvokeAsync("feature018_equipment_preflight", first,
                    CancellationToken.None));
            forgedApplied = false;
            responseState = "accepted";
            await ExpectEquipmentErrorAsync("GAME_ACTION_UNCONFIRMED", () =>
                game.InvokeAsync("feature018_equipment_preflight", first,
                    CancellationToken.None));
            responseState = "confirmed";

            staleConfigDuringReply = true;
            await ExpectEquipmentErrorAsync("GAME_ACTION_UNCONFIRMED", () =>
                game.InvokeAsync("feature018_equipment_preflight", first,
                    CancellationToken.None));
            staleConfigDuringReply = false;
            fields = await game.ReadConfirmedEquipmentPreflightFieldsAsync(
                first, CancellationToken.None);
            Require(fields["presetId"] == "gear-newer",
                "native preflight reused stale cached saved preset identity");

            JsonObject oversized = JsonNode.Parse(savedPreset)!.AsObject();
            oversized["equipmentPresets"]![0]!["name"] = new string('X', 14050);
            store.SaveEquipmentConfig(oversized);
            await ExpectEquipmentErrorAsync("GAME_PRECONDITION_FAILED", () =>
                game.InvokeAsync("feature018_equipment_preflight", first,
                    CancellationToken.None));
            Console.WriteLine("FEATURE018_EQUIPMENT_OWNER_PREFLIGHT_PASS");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static async Task ExpectEquipmentErrorAsync(string expected,
        Func<Task<object?>> action)
    {
        try { _ = await action(); }
        catch (BridgeCommandException error) when (error.Code == expected) { return; }
        throw new InvalidOperationException(
            "FEATURE018_EQUIPMENT_PREFLIGHT: expected guarded " + expected);
    }
}
