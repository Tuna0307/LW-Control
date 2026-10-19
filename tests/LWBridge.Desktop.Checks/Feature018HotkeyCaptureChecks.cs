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
        internal Func<CancellationToken, Task<object?>>? AsyncReceipt;
        public bool CanHandle(string command) =>
            Available && command == "feature018_hotkey_action";

        public Task<object?> InvokeAsync(string command, JsonElement payload, CancellationToken cancellationToken)
        {
            LastName = command;
            LastPayload = payload.Clone();
            Calls++;
            return AsyncReceipt?.Invoke(cancellationToken) ?? Task.FromResult(Receipt());
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

    private sealed class FakeCapturePlatform : Feature018HotkeyCapture.IPlatform
    {
        internal nint Foreground = (nint)101;
        internal readonly HashSet<nint> OwnedRoots = [(nint)101, (nint)202];
        internal bool ExactOwner = true;
        internal bool KeyboardFocusOwned = true;
        internal readonly HashSet<Keys> DownKeys = [];
        internal Action? OnKeyRead;
        internal int KeyReads;
        public nint ForegroundWindow() => Foreground;
        public bool IsExactOwnedRoot(nint hwnd, int pid, string path, DateTime startedUtc) =>
            ExactOwner && pid == 43210 && hwnd != 0 && OwnedRoots.Contains(hwnd) &&
            path.EndsWith("LastWar.exe", StringComparison.OrdinalIgnoreCase) &&
            startedUtc.Kind == DateTimeKind.Utc;
        public bool HasOwnedKeyboardFocus(nint hwnd, int pid) =>
            KeyboardFocusOwned && pid == 43210 && hwnd == Foreground;
        public IReadOnlySet<Keys> WatchedKeysDown()
        {
            KeyReads++;
            IReadOnlySet<Keys> snapshot = DownKeys.ToHashSet();
            OnKeyRead?.Invoke();
            return snapshot;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("FEATURE018_HOTKEY_CAPTURE: " + message);
    }

    private static HashSet<Keys> Down(params Keys[] keys) => [.. keys];

    internal static async Task RunAsync()
    {
        Environment.SetEnvironmentVariable("LWBRIDGE_FEATURE018_PROOF_UID", "4799432499002212");
        Environment.SetEnvironmentVariable("LWBRIDGE_FEATURE018_PROOF_SERVER", "2212");
        if (!LWBridgeWindow.Feature018IsApprovedAccount("4799432499002212", 2212) ||
            LWBridgeWindow.Feature018IsApprovedAccount("1183884595002296", 2296) ||
            LWBridgeWindow.Feature018IsApprovedAccount("4799432499002213", 2212) ||
            LWBridgeWindow.Feature018IsApprovedAccount(null, 2212) ||
            LWBridgeWindow.Feature018IsApprovedAccount("4799432499002212", 2296))
            throw new InvalidDataException("R15 harness must admit only exact sub-account UID, including on shared server 2212");

        await NativeAttackRejectionDiagnosticsChecksAsync();
        await AttackNativeChooserContinuationChecksAsync();
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
        Require(recognizer.Observe(Down(Keys.Q, Keys.D1), true, true) is null,
            "Q plus unmodified equipment digit falsely dispatched an attack");
        Require(recognizer.Observe(Down(Keys.Q), true, true) is null,
            "a Q held through an ambiguous chord acquired a fabricated new key edge");
        _ = recognizer.Observe(Down(), true, true);
        Require(recognizer.Observe(Down(Keys.F9, Keys.D4), true, true) is null,
            "relocation F9 plus another watched digit falsely dispatched");
        _ = recognizer.Observe(Down(), true, true);
        Require(recognizer.Observe(Down(Keys.Menu, Keys.D2, Keys.Q), true, true) is null,
            "equipment Alt+2 plus unrelated Q falsely dispatched");
        _ = recognizer.Observe(Down(), true, true);
        Require(recognizer.Observe(Down(Keys.Menu, Keys.LMenu, Keys.D2), true, true) ==
                new Feature018HotkeyIntent(Feature018HotkeyKind.ApplyEquipmentPreset, 2),
            "legitimate overlapping Windows generic/left Alt aliases were rejected");
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
        CaptureExactRootRaceChecks();
        await OwnedWindowAdmissionChecksAsync();

        var session = new Feature018HotkeySession("p", "i",
            LWBridgeWindow.Feature018ApprovedGameUid, LWBridgeWindow.Feature018ApprovedServerId);
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
        Require(seen.Count == 1 && seen[0].Intent == attack,
            "captured attack intent changed before native dispatch");

        bool nativeReturned = false;
        int unavailableAfterReturn = 0;
        var transientProviders = new Dictionary<Feature018HotkeyKind,
            Func<Feature018HotkeyCommand, CancellationToken, Task<Feature018HotkeyActionOutcome>>>
        {
            [Feature018HotkeyKind.AttackSquad] = (command, _) =>
            {
                nativeReturned = true;
                return Task.FromResult(new Feature018HotkeyActionOutcome(
                    Feature018HotkeyActionStatus.Submitted, "native_send_observed"));
            },
        };
        var transientBroker = new Feature018HotkeyActions(session,
            () =>
            {
                if (nativeReturned && unavailableAfterReturn++ < 2) return null;
                return session;
            },
            () => true, () => new HotkeyConfig(), transientProviders);
        Feature018HotkeyActionOutcome transientResult =
            await transientBroker.TryDispatchAsync(attack);
        Require(transientResult.Status == Feature018HotkeyActionStatus.Submitted &&
                transientResult.Reason == "native_send_observed" &&
                unavailableAfterReturn >= 3,
            "transient post-native heartbeat gap erased an admitted native result");

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
        Require(seen.Count == 1, "blocked shortcut reached action producer");

        var stub = new StubNativeCommandService();
        var wired = new Feature018HotkeyNativeProviders(stub).Create();
        // The recognizer/broker and mailbox retain the authenticated account.
        // Direct native provider calls reject missing/malformed account snapshots
        // on every account, independent of the old proof-only 2288 identity.
        foreach (Feature018HotkeyKind mutation in new[]
        {
            Feature018HotkeyKind.AttackSquad, Feature018HotkeyKind.RecallSquad,
            Feature018HotkeyKind.UseShield, Feature018HotkeyKind.ApplyEquipmentPreset,
            Feature018HotkeyKind.RandomRelocate, Feature018HotkeyKind.AllianceRelocate,
            Feature018HotkeyKind.ReinforceFrontline,
        })
        {
            var forbidden = new Feature018HotkeyCommand(
                new Feature018HotkeySession("main", "i", "", 2212),
                new Feature018HotkeyIntent(mutation, mutation switch
                {
                    Feature018HotkeyKind.UseShield => 8,
                    Feature018HotkeyKind.ReinforceFrontline => 5,
                    Feature018HotkeyKind.RandomRelocate or Feature018HotkeyKind.AllianceRelocate => 0,
                    _ => 1,
                }));
            int beforeForbidden = stub.Calls;
            Feature018HotkeyActionOutcome denied =
                await wired[mutation](forbidden, CancellationToken.None);
            Require(denied.Status == Feature018HotkeyActionStatus.SessionChanged &&
                    denied.Reason == "native_hotkey_account_snapshot_unavailable" &&
                    stub.Calls == beforeForbidden,
                "missing game UID reached mutating native action provider through " + mutation);
        }
        var missingServer = new Feature018HotkeyCommand(
            session with { ServerId = 0 },
            new Feature018HotkeyIntent(Feature018HotkeyKind.UseShield, 8));
        Require((await wired[Feature018HotkeyKind.UseShield](
                    missingServer, CancellationToken.None)).Status ==
                Feature018HotkeyActionStatus.SessionChanged && stub.Calls == 0,
            "missing game server reached native shield ItemUse");
        var anyAccountProvider = new StubNativeCommandService();
        var anyAccount = new Feature018HotkeyNativeProviders(anyAccountProvider).Create();
        Feature018HotkeyActionOutcome admittedOtherAccount = await anyAccount[Feature018HotkeyKind.UseShield](
            new Feature018HotkeyCommand(
                new Feature018HotkeySession("other-profile", "other-session", "3133546747002212", 2212),
                new Feature018HotkeyIntent(Feature018HotkeyKind.UseShield, 8)), CancellationToken.None);
        Require(anyAccountProvider.Calls == 1 && admittedOtherAccount.Status != Feature018HotkeyActionStatus.SessionChanged,
            "any-account native provider rejected valid UID/server before the authenticated exchange");

        // The real exchange returns accepted game data ONLY when there was a
        // source-classified native request/accepted response; an F10 chooser
        // may have been requested while NO relocation packet was sent. Provider
        // must not fabricate a send, poll or successful relocation from it.
        stub.Receipt = () => JsonSerializer.SerializeToElement(new
        {
            accepted = true, requestDispatched = false,
            awaitingGameConfirmation = true, slot = 0,
            action = "allianceRelocate", itemUuid = "991001",
            originalPacketSend = false,
        });
        Feature018HotkeyActionOutcome chooserOnly = await wired[
            Feature018HotkeyKind.AllianceRelocate](
                new Feature018HotkeyCommand(session, new(Feature018HotkeyKind.AllianceRelocate)),
                CancellationToken.None);
        Require(chooserOnly.Status == Feature018HotkeyActionStatus.Submitted &&
                !chooserOnly.Working &&
                chooserOnly.Reason == "native_action_accepted_without_verified_game_rpc_dispatch" &&
                stub.Calls == 1 && !stub.LastPayload.TryGetProperty("queryOnly", out _),
            "F10 accepted chooser falsely counted as packet send or triggered query retry");

        var f10PollStub = new StubNativeCommandService();
        f10PollStub.Receipt = () => f10PollStub.Calls == 1
            ? JsonSerializer.SerializeToElement(new
            {
                accepted = true, requestDispatched = false, slot = 0,
                action = "allianceRelocate", ownerUid = session.GameUid,
                sourceServerId = session.ServerId, itemUuid = "991001",
                chooserRequestMade = true, ownerSelectionRequired = true,
                originalPacketSend = false,
            })
            : JsonSerializer.SerializeToElement(new
            {
                observation = "native_alliance_relocation_reply_world_position",
                action = "allianceRelocate", slot = 0,
                ownerUid = session.GameUid, sourceServerId = session.ServerId,
                itemUuid = "991001", itemId = "5005",
                chooserRequestMade = true, ownerSelectionRequired = true,
                ownerSelectedRemoteRallyObserved = true,
                allianceMoveRequestObserved = true, allianceMoveSendCount = 1,
                allianceMoveReplyObserved = true, worldMvReason240Observed = true,
                replyResultOneObserved = true, serverAckAttributableByOneSendFence = true,
                requestDispatched = true, relocationConfirmed = true,
                awaitingGameConfirmation = false,
                independentWorldAndInventoryPoststateVerified = true,
                worldPointChanged = true, selectedRallyServerId = 2213, replyServerId = 2213,
                beforeWorldPointId = 440444, afterWorldPointId = 540544,
                acknowledgedWorldPointId = 540544, beforeCount = 2, afterCount = 1,
            });
        var f10Wired = new Feature018HotkeyNativeProviders(f10PollStub).Create();
        Feature018HotkeyActionOutcome f10Confirmed = await f10Wired[
            Feature018HotkeyKind.AllianceRelocate](
                new Feature018HotkeyCommand(session, new(Feature018HotkeyKind.AllianceRelocate)),
                CancellationToken.None);
        Require(f10Confirmed.Status == Feature018HotkeyActionStatus.Confirmed &&
                f10Confirmed.Working && f10PollStub.Calls == 2 &&
                f10PollStub.LastPayload.TryGetProperty("queryOnly", out var f10QueryOnly) &&
                f10QueryOnly.ValueKind == JsonValueKind.True,
            "F10 chooser owner-selection/reason240 continuation did not confirm through read-only polling");

        stub.Receipt = () => JsonSerializer.SerializeToElement(new
        {
            accepted = true, requestDispatched = true,
            action = "randomRelocate", slot = 0,
        });
        Feature018HotkeyActionOutcome wrongAction = await wired[
            Feature018HotkeyKind.AllianceRelocate](
                new Feature018HotkeyCommand(session, new(Feature018HotkeyKind.AllianceRelocate)),
                CancellationToken.None);
        Require(wrongAction.Status == Feature018HotkeyActionStatus.Failed &&
                wrongAction.Reason == "native_action_accepted_for_different_intent" &&
                stub.Calls == 2,
            "one native action's accepted send was attributed to a different shortcut");
        stub.Receipt = () => JsonSerializer.SerializeToElement(new
        {
            accepted = true, requestDispatched = true,
            action = "shieldUse", slot = 12,
        });
        Feature018HotkeyActionOutcome wrongSlot = await wired[
            Feature018HotkeyKind.UseShield](
                new Feature018HotkeyCommand(session, new(Feature018HotkeyKind.UseShield, 8)),
                CancellationToken.None);
        Require(wrongSlot.Status == Feature018HotkeyActionStatus.Failed &&
                wrongSlot.Reason == "native_action_accepted_for_different_intent" &&
                stub.Calls == 3,
            "one shield slot's accepted send was attributed to a different F6/F7 key");

        stub.Receipt = () => throw new BridgeCommandException(
            "GAME_ACTION_UNCONFIRMED", "Native F10 chooser open is not a move",
            JsonSerializer.SerializeToElement(new
            {
                action = "allianceRelocate", chooserRequestMade = true,
                originalPacketSend = false, requestDispatched = false,
            }));
        Feature018HotkeyActionOutcome nativeChooser = await wired[
            Feature018HotkeyKind.AllianceRelocate](
                new Feature018HotkeyCommand(session, new(Feature018HotkeyKind.AllianceRelocate)),
                CancellationToken.None);
        Require(nativeChooser.Status == Feature018HotkeyActionStatus.Submitted &&
                !nativeChooser.Working &&
                nativeChooser.Reason == "native_f10_chooser_requested_no_relocation_packet" &&
                stub.Calls == 4,
            "accepted original F10 native chooser without MoveCity was reported as a game request");
        stub.Receipt = () => throw new BridgeCommandException(
            "GAME_ACTION_UNCONFIRMED", "No native F9 item-use packet sent",
            JsonSerializer.SerializeToElement(new
            {
                action = "randomRelocate", originalPacketSend = false,
                requestDispatched = false,
            }));
        Feature018HotkeyActionOutcome noNativePacket = await wired[
            Feature018HotkeyKind.RandomRelocate](
                new Feature018HotkeyCommand(session, new(Feature018HotkeyKind.RandomRelocate)),
                CancellationToken.None);
        Require(noNativePacket.Status == Feature018HotkeyActionStatus.Failed &&
                noNativePacket.Reason == "native_game_request_not_dispatched" &&
                stub.Calls == 5,
            "explicitly absent original F9 game send was misreported as Submitted");
        stub.Receipt = () => throw new BridgeCommandException(
            "GAME_ACTION_UNCONFIRMED", "Original ItemUse SendMessage returned false after an attempted send",
            JsonSerializer.SerializeToElement(new
            {
                action = "randomRelocate", requestDispatched = false,
                nativeSendAttempted = true, nativeSendOutcomeUncertain = true,
            }));
        int beforeUncertain = stub.Calls;
        Feature018HotkeyActionOutcome ambiguousF9 = await wired[
            Feature018HotkeyKind.RandomRelocate](
                new Feature018HotkeyCommand(session, new(Feature018HotkeyKind.RandomRelocate)),
                CancellationToken.None);
        Require(ambiguousF9.Status == Feature018HotkeyActionStatus.Submitted &&
                !ambiguousF9.Working &&
                ambiguousF9.Reason == "native_action_outcome_unconfirmed_dispatch_unknown" &&
                stub.Calls == beforeUncertain + 1,
            "original F9 SendMessage false/throw was falsely classified as definitely not sent or automatically retried");

        // Even two distinct provider instances (e.g., a retired capture and
        // its newly created replacement) share the same native mutating route.
        // The second must not overlap, queue or retry while the first is
        // unresolved; a read-only shield observation remains eligible.
        var nativeHeld = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var firstStub = new StubNativeCommandService
        {
            AsyncReceipt = token => nativeHeld.Task.WaitAsync(token),
        };
        var secondStub = new StubNativeCommandService
        {
            Receipt = () => JsonSerializer.SerializeToElement(new
            {
                observation = "native_MAIN_SHIELD_effect_status_map",
                active = false, serverTimeMilliseconds = 1000, shieldStatusCount = 0,
            }),
        };
        var firstNative = new Feature018HotkeyNativeProviders(firstStub).Create();
        var secondNative = new Feature018HotkeyNativeProviders(secondStub).Create();
        using (var abortOld = new CancellationTokenSource())
        {
            Task<Feature018HotkeyActionOutcome> oldPending =
                firstNative[Feature018HotkeyKind.UseShield](
                    new Feature018HotkeyCommand(session, new(Feature018HotkeyKind.UseShield, 8)),
                    abortOld.Token);
            Require(!oldPending.IsCompleted && firstStub.Calls == 1,
                "first native shield use did not hold its pending-RPC lease");
            Feature018HotkeyActionOutcome simultaneous = await secondNative[
                Feature018HotkeyKind.RandomRelocate](
                    new Feature018HotkeyCommand(session, new(Feature018HotkeyKind.RandomRelocate)),
                    CancellationToken.None);
            Require(simultaneous.Status == Feature018HotkeyActionStatus.Busy &&
                    simultaneous.Reason == "another_native_hotkey_mutation_pending" &&
                    secondStub.Calls == 0,
                "replacement provider overlapped pending shield ItemUse with relocation RPC");
            Feature018HotkeyActionOutcome overlayDuringPending = await secondNative[
                Feature018HotkeyKind.ShieldRemaining](
                    new Feature018HotkeyCommand(session, new(Feature018HotkeyKind.ShieldRemaining)),
                    CancellationToken.None);
            Require(overlayDuringPending.Working && secondStub.Calls == 1,
                "independent read-only shield status was blocked by mutating native lease");
            abortOld.Cancel();
            try
            {
                await oldPending;
                throw new InvalidOperationException("aborted native send fixture did not cancel");
            }
            catch (OperationCanceledException) { }
        }
        secondStub.Receipt = () => JsonSerializer.SerializeToElement(new
        {
            requestDispatched = true, awaitingGameConfirmation = true, slot = 0,
        });
        Feature018HotkeyActionOutcome afterRetirement = await secondNative[
            Feature018HotkeyKind.RandomRelocate](
                new Feature018HotkeyCommand(session, new(Feature018HotkeyKind.RandomRelocate)),
                CancellationToken.None);
        Require(afterRetirement.Status == Feature018HotkeyActionStatus.Submitted &&
                !afterRetirement.Working && secondStub.Calls == 2,
            "cancelled old-provider native lease either leaked or fabricated relocation ACK");

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
        int beforeReadOnly = stub.Calls;
        Feature018HotkeyActionOutcome observed = await nativeCoordinator.TryDispatchAsync(
            new(Feature018HotkeyKind.ShieldRemaining));
        Require(observed.Working && observed.Status == Feature018HotkeyActionStatus.Confirmed,
            "native confirmed shield status not admitted");
        Require(stub.LastName == "feature018_hotkey_action" && stub.Calls == beforeReadOnly + 1 &&
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
            observation = "native_world_march_return_reply_and_target",
            slot = 2,
            marchUuid = "803001",
            formationUuid = "1001",
            target = "BACK_HOME",
            returning = true,
        });
        Feature018HotkeyActionOutcome alreadyHome = await nativeCoordinator.TryDispatchAsync(
            new(Feature018HotkeyKind.RecallSquad, 2));
        Require(alreadyHome.Status == Feature018HotkeyActionStatus.Failed &&
                !alreadyHome.Working &&
                alreadyHome.Reason == "native_recall_back_home_state_lacks_attributed_send_and_reply",
            "cached BACK_HOME before native send falsely confirmed keyed recall");
        stub.Receipt = () => JsonSerializer.SerializeToElement(new
        {
            observation = "native_world_march_return_reply_and_target",
            slot = 2, marchUuid = "803001", formationUuid = "1001",
            target = "BACK_HOME", returning = true,
            requestPreviouslyDispatched = true, nativeReturnReplyMatched = false,
        });
        Require(!(await nativeCoordinator.TryDispatchAsync(
                    new(Feature018HotkeyKind.RecallSquad, 2))).Working,
            "native send without original matching reply falsely confirmed keyed recall");
        stub.Receipt = () => JsonSerializer.SerializeToElement(new
        {
            observation = "native_world_march_return_reply_and_target",
            slot = 2, marchUuid = "803001", formationUuid = "1001",
            target = "BACK_HOME", returning = true,
            requestPreviouslyDispatched = true, nativeReturnReplyMatched = true,
        });
        Require((await nativeCoordinator.TryDispatchAsync(
                    new(Feature018HotkeyKind.RecallSquad, 2))).Working,
            "explicit independently matched native return reply rejected");
        stub.Receipt = () => JsonSerializer.SerializeToElement(new
        {
            observation = "native_world_march_return_server_applied_poststate",
            slot = 2, marchUuid = "803001", formationUuid = "1001",
            target = "BACK_HOME", returning = true,
            requestPreviouslyDispatched = true,
            nativePostStateObserved = true,
            serverAppliedPostStateObserved = true,
            priorTargetWasBackHome = false,
            authoritativePostStateSource = "WorldMarch.GetMarchTargetType_after_dispatch",
            serverAcknowledgementCorrelated = false,
        });
        Require((await nativeCoordinator.TryDispatchAsync(
                    new(Feature018HotkeyKind.RecallSquad, 2))).Working,
            "authoritative same-real-march BACK_HOME transition rejected without diagnostic reply");
        stub.Receipt = () => JsonSerializer.SerializeToElement(new
        {
            observation = "native_world_march_return_server_applied_poststate",
            slot = 2, marchUuid = "803001", formationUuid = "1001",
            target = "BACK_HOME", returning = true,
            requestPreviouslyDispatched = true,
            nativePostStateObserved = true,
            serverAppliedPostStateObserved = true,
            priorTargetWasBackHome = true,
            authoritativePostStateSource = "WorldMarch.GetMarchTargetType_after_dispatch",
            serverAcknowledgementCorrelated = false,
        });
        Require(!(await nativeCoordinator.TryDispatchAsync(
                    new(Feature018HotkeyKind.RecallSquad, 2))).Working,
            "preexisting BACK_HOME state falsely admitted as server-applied recall transition");
        Require((await nativeCoordinator.TryDispatchAsync(
            new(Feature018HotkeyKind.RecallSquad, 3))).Status == Feature018HotkeyActionStatus.Failed,
            "another squad slot's return-home receipt falsely confirmed shortcut");

        stub.Receipt = () => JsonSerializer.SerializeToElement(new
        {
            observation = "native_world_march_return_reply_and_target",
            slot = 2,
            marchUuid = "803001",
            formationUuid = "1001",
            target = "BACK_HOME",
            returning = true,
        });
        Require((await nativeCoordinator.TryDispatchAsync(
            new(Feature018HotkeyKind.AttackSquad, 2))).Status == Feature018HotkeyActionStatus.Failed,
            "recall observation falsely confirmed a current-client attack");

        JsonObject ReinforcementReceipt(int created, int reserved) => new()
        {
            ["observation"] = "native_ParkourTeam_GetRemainUnitCount",
            ["action"] = "frontlineReinforce", ["slot"] = 5, ["heroId"] = 1301,
            ["stageRunId"] = "101", ["beforeCount"] = 50, ["afterCount"] = 55,
            ["activeMemberCountBefore"] = 50, ["activeMemberCountAfter"] = 50 + created,
            ["reserveMemberCountBefore"] = 0, ["reserveMemberCountAfter"] = reserved,
            ["addedMembers"] = 5, ["observedCreatedUnits"] = created, ["reservedMembers"] = reserved,
            ["unitIdentityVerified"] = true, ["nativeReserveVerified"] = true,
            ["unitIdentitySource"] = "native_ParkourTeam_teamUnits_guid",
            ["reserveIdentitySource"] = "native_ParkourTeam_overflowUnitCount",
            ["guidAllocatorBefore"] = 100, ["guidAllocatorAfter"] = 100 + created,
        };
        foreach ((int created, int reserved) in new[] { (5, 0), (2, 3), (0, 5) })
        {
            stub.Receipt = () => JsonSerializer.SerializeToElement(ReinforcementReceipt(created, reserved));
            Require((await nativeCoordinator.TryDispatchAsync(
                new(Feature018HotkeyKind.ReinforceFrontline, 5))).Working,
                "native active/reserve five-soldier split was not admitted: " + created + "/" + reserved);
        }
        foreach (Action<JsonObject> corrupt in new Action<JsonObject>[]
        {
            row => row["afterCount"] = 54,
            row => row["observedCreatedUnits"] = 5, // All-reserve cannot claim five created GUIDs.
            row => row["reservedMembers"] = 4,
            row => row["nativeReserveVerified"] = false,
            row => row["unitIdentityVerified"] = false,
            row => row["guidAllocatorAfter"] = 105,
            row => row["reserveMemberCountAfter"] = 4,
            row => row["activeMemberCountAfter"] = 51,
            row => row["reserveIdentitySource"] = "unverified_counter",
            row => row["observation"] = "native_ParkourBattleLogic_GetMemberCount",
        })
        {
            JsonObject invalid = ReinforcementReceipt(0, 5);
            corrupt(invalid);
            stub.Receipt = () => JsonSerializer.SerializeToElement(invalid);
            Require((await nativeCoordinator.TryDispatchAsync(
                new(Feature018HotkeyKind.ReinforceFrontline, 5))).Status == Feature018HotkeyActionStatus.Failed,
                "contradictory native reserve/GUID/count receipt falsely confirmed five soldiers");
        }
        Console.WriteLine("FEATURE018_G_NATIVE_RESERVE_RECEIPT_PASS");

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
                observation = "native_world_march_return_server_applied_poststate",
                slot = 2,
                marchUuid = "803001",
                formationUuid = "1001",
                target = "BACK_HOME",
                returning = true,
                requestPreviouslyDispatched = true,
                nativePostStateObserved = true,
                serverAppliedPostStateObserved = true,
                priorTargetWasBackHome = false,
                authoritativePostStateSource = "WorldMarch.GetMarchTargetType_after_dispatch",
                serverAcknowledgementCorrelated = false,
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
            sentUid.GetString() == session.GameUid &&
            stub.LastPayload.GetProperty("expectedGameUid").GetString() == session.GameUid &&
            stub.LastPayload.GetProperty("expectedServerId").GetInt32() == session.ServerId &&
            !stub.LastPayload.TryGetProperty("allowRapidItem", out _) &&
            !stub.LastPayload.TryGetProperty("allowRapidDiamond", out _) &&
            !stub.LastPayload.TryGetProperty("itemOptIn", out _) &&
            !stub.LastPayload.TryGetProperty("diamondOptIn", out _) &&
            !stub.LastPayload.TryGetProperty("itemId", out _),
            "attack game intent lost its original squad or authenticated owner UID: " +
            stub.LastPayload.GetRawText());

        var validAttackFields = Feature018GameCommandService.ReadFields("feature018_hotkey_action",
            JsonSerializer.SerializeToElement(new { action = "attack", slot = 2,
                authorizedUid = session.GameUid }));
        Require(validAttackFields["authorizedUid"] == session.GameUid &&
                validAttackFields["slot"] == "2" &&
                !validAttackFields.ContainsKey("allowRapidItem") &&
                !validAttackFields.ContainsKey("allowRapidDiamond") &&
                !validAttackFields.ContainsKey("itemOptIn") &&
                !validAttackFields.ContainsKey("diamondOptIn") &&
                !validAttackFields.ContainsKey("itemId"),
            "native attack lost authenticated numeric owner UID");
        var attackStatus = Feature018GameCommandService.ReadFields("feature018_hotkey_action",
            JsonSerializer.SerializeToElement(new { action = "attack", slot = 2,
                authorizedUid = session.GameUid, queryOnly = true }));
        Require(attackStatus["queryOnly"] == "1" && !attackStatus.ContainsKey("allowRapidItem"),
            "attack confirmation query added new spending consent");
        var equipmentStatus = Feature018GameCommandService.ReadFields("feature018_hotkey_action",
            JsonSerializer.SerializeToElement(new { action = "equipment", slot = 2, queryOnly = true }));
        Require(equipmentStatus["queryOnly"] == "1", "equipment native poststate polling was rejected");
        foreach (string retiredAction in new[] { "speedupItem", "speedupDiamond" })
        {
            try
            {
                _ = Feature018GameCommandService.ReadFields("feature018_hotkey_action",
                    JsonSerializer.SerializeToElement(new
                    {
                        action = retiredAction, slot = 1, authorizedUid = session.GameUid,
                    }));
                throw new InvalidOperationException("retired rapid hotkey action admitted: " + retiredAction);
            }
            catch (BridgeCommandException error) when (error.Code == "INVALID_REQUEST") { }
        }
        foreach (var invalid in new[]
        {
            new { action = "attack", slot = 1, authorizedUid = "", itemOptIn = false, itemId = "", queryOnly = false },
            new { action = "attack", slot = 1, authorizedUid = "bad-uid", itemOptIn = false, itemId = "", queryOnly = false },
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

        int beforeSecondAttack = stub.Calls;
        stub.Receipt = () => JsonSerializer.SerializeToElement(new
        {
            requestDispatched = true,
            awaitingGameConfirmation = true,
            slot = 3,
            action = "attack",
        });
        Require((await nativeCoordinator.TryDispatchAsync(
            new(Feature018HotkeyKind.AttackSquad, 3))).Status == Feature018HotkeyActionStatus.Submitted &&
            stub.Calls == beforeSecondAttack + 1 &&
            !stub.LastPayload.TryGetProperty("allowRapidItem", out _) &&
            !stub.LastPayload.TryGetProperty("allowRapidDiamond", out _) &&
            !stub.LastPayload.TryGetProperty("itemOptIn", out _) &&
            !stub.LastPayload.TryGetProperty("diamondOptIn", out _) &&
            !stub.LastPayload.TryGetProperty("itemId", out _),
            "attack payload reintroduced retired march speed-up consent");
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

        // Owned shield status remains useful for the item-use path; R18's
        // visible-city overlay must explicitly reject this older single timer.
        var shieldPlatform = new FakeCapturePlatform();
        bool shieldAdmitted = true;
        using (var shieldCapture = new Feature018HotkeyCapture(43210, @"C:\game\LastWar.exe",
            DateTime.UtcNow, () => shieldAdmitted, _ => { }, shieldPlatform))
        {
            shieldPlatform.DownKeys.Add(Keys.Space);
            Require(shieldCapture.IsSpaceHeld(), "Space hold not observed");
            shieldPlatform.DownKeys.Clear();
            Require(!shieldCapture.IsSpaceHeld(), "Space release left overlay visible");
            shieldPlatform.DownKeys.Add(Keys.Space);
            shieldPlatform.OnKeyRead = () => shieldAdmitted = false;
            Require(!shieldCapture.IsSpaceHeld(), "owner loss during Space sample left overlay visible");
        }
        Require(Feature018ShieldStatusOverlay.TryReadCities(JsonSerializer.SerializeToElement(new
            { observation = "native_visible_city_shields", readOnly = true, cities = new[] { new { uuid = "A" }, new { uuid = "B" } } })),
            "multiple native city shield rows rejected");
        Require(!Feature018ShieldStatusOverlay.TryReadCities(JsonSerializer.SerializeToElement(new
            { observation = "native_MAIN_SHIELD_effect_status_map", readOnly = true, cities = Array.Empty<object>() })),
            "old own-shield observation admitted to visible-city overlay");
        // The owned shield observer requires the *actual* returned MAIN_SHIELD
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
        await QueuedStageReinforcementChecksAsync();
        Console.WriteLine("FEATURE018_HOTKEY_CAPTURE_PASS");
    }

    private static async Task NativeAttackRejectionDiagnosticsChecksAsync()
    {
        // This is the captured account shape observed in the 2212 Q failure;
        // no game or keyboard is contacted. Native failed receipts must retain
        // the distinguishing rejection, never become a positive observation.
        var session = new Feature018HotkeySession("2212-test-profile", "2212-session",
            "4799432499002212", 2212);
        var command = new Feature018HotkeyCommand(session,
            new Feature018HotkeyIntent(Feature018HotkeyKind.AttackSquad, 1));
        foreach ((string code, string nativeReason) in new[]
        {
            ("GAME_ACTION_FAILED", "No hovered native world troop was source-verified"),
            ("GAME_RESPONSE_TIMEOUT", "No correlated game action result arrived."),
            ("INVALID_REQUEST", "Native attack request did not match current formation."),
        })
        {
            var stub = new StubNativeCommandService
            {
                Receipt = () => throw new BridgeCommandException(code, nativeReason),
            };
            var provider = new Feature018HotkeyNativeProviders(stub).Create()[
                Feature018HotkeyKind.AttackSquad];
            Feature018HotkeyActionOutcome outcome = await provider(command, CancellationToken.None);
            Require(outcome.Status == Feature018HotkeyActionStatus.Failed && !outcome.Working &&
                    outcome.Reason.StartsWith("current_client_game_action_rejected", StringComparison.Ordinal) &&
                    outcome.Reason.Contains(code, StringComparison.Ordinal) &&
                    outcome.Reason.Contains(nativeReason, StringComparison.Ordinal) &&
                    outcome.AuthoritativeObservation is null && stub.Calls == 1 &&
                    !stub.LastPayload.TryGetProperty("queryOnly", out _),
                "2212 captured Q rejection lost the original native code/message or falsely claimed a send");
        }

        var diagnostic = JsonSerializer.SerializeToElement(new
        {
            requestDispatched = false, nativePrecondition = "hover_target_absent",
            ownerUid = session.GameUid, sourceServerId = session.ServerId,
        });
        var withDetails = new StubNativeCommandService
        {
            Receipt = () => throw new BridgeCommandException("GAME_ACTION_FAILED",
                "Native hover unavailable", diagnostic),
        };
        Feature018HotkeyActionOutcome detailed = await new Feature018HotkeyNativeProviders(
            withDetails).Create()[Feature018HotkeyKind.AttackSquad](command, CancellationToken.None);
        Require(detailed.Status == Feature018HotkeyActionStatus.Failed && !detailed.Working &&
                detailed.AuthoritativeObservation is { ValueKind: JsonValueKind.Object } observed &&
                observed.GetProperty("nativePrecondition").GetString() == "hover_target_absent" &&
                !observed.GetProperty("requestDispatched").GetBoolean() &&
                withDetails.Calls == 1,
            "native diagnostic object disappeared or was credited as authoritative gameplay");

        // Reproduce the actual ExchangeAsync -> GameCommandService -> provider
        // failed-response route, rather than only throwing a stub exception.
        string root = Path.Combine(Path.GetTempPath(), "feature018-2212-rejection-" +
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string requestPath = Path.Combine(root, "feature018-request.txt");
            string responsePath = Path.Combine(root, "feature018-response.json");
            var owner = new OverviewMapScanSession("2212-test-profile", "2212-session",
                "test-challenge", 43210, @"C:\test\LastWar.exe", "2026-10-17T00:00:00Z");
            int nativeCalls = 0;
            var exchange = new Feature018OwnedGameExchange(
                () => owner, candidate => candidate == owner,
                requestPath, responsePath,
                delay: (_, _) =>
                {
                    var values = File.ReadAllLines(requestPath)
                        .Where(line => line.Contains('='))
                        .Select(line => line.Split('=', 2))
                        .ToDictionary(parts => parts[0], parts => parts[1]);
                    Require(values["command"] == "feature018_hotkey_action" &&
                            values["action"] == "attack" && values["slot"] == "1" &&
                            values["authorizedUid"] == session.GameUid &&
                            !values.ContainsKey("queryOnly"),
                        "2212 native failure test issued a different command or duplicate keyboard intent");
                    nativeCalls++;
                    File.WriteAllText(responsePath, JsonSerializer.Serialize(new
                    {
                        schemaVersion = 1, requestId = values["requestId"],
                        profileId = values["profileId"],
                        launchSessionId = values["launchSessionId"],
                        challenge = values["challenge"],
                        gamePid = int.Parse(values["gamePid"]),
                        command = values["command"],
                        observedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        state = "failed", errorCode = "GAME_ACTION_FAILED",
                        error = "Original native hover target could not be resolved",
                        data = new { requestDispatched = false, nativePrecondition = "hover_absent" },
                    }));
                    return Task.CompletedTask;
                },
                currentAccount: _ => new Feature018AuthenticatedGameAccount(
                    session.GameUid, session.ServerId));
            var nativeService = new Feature018GameCommandService(exchange);
            Feature018HotkeyActionOutcome routed = await new Feature018HotkeyNativeProviders(
                nativeService).Create()[Feature018HotkeyKind.AttackSquad](command, CancellationToken.None);
            Require(routed.Status == Feature018HotkeyActionStatus.Failed && !routed.Working &&
                    routed.Reason.Contains("GAME_ACTION_FAILED", StringComparison.Ordinal) &&
                    routed.Reason.Contains("Original native hover target could not be resolved",
                        StringComparison.Ordinal) &&
                    routed.AuthoritativeObservation is { ValueKind: JsonValueKind.Object } native &&
                    native.GetProperty("nativePrecondition").GetString() == "hover_absent" &&
                    nativeCalls == 1,
                "real 2212 mailbox failed envelope was collapsed into generic rejection");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task AttackNativeChooserContinuationChecksAsync()
    {
        var session = new Feature018HotkeySession("other-profile", "other-session",
            "3133546747002212", 2212);
        var captured = new Feature018HotkeyCommand(session,
            new(Feature018HotkeyKind.AttackSquad, 3));
        JsonElement chooser = JsonSerializer.SerializeToElement(new
        {
            accepted = true, requestDispatched = false, awaitingNativeChooser = true,
            chooserRequestMade = true, action = "attack", slot = 3,
            formationUuid = "9007199254740993",
            ownerUid = session.GameUid, sourceServerId = session.ServerId,
        });
        JsonElement waiting = JsonSerializer.SerializeToElement(new
        {
            accepted = true, requestDispatched = false, awaitingNativeChooser = true,
            chooserRequestMade = true, action = "attack", slot = 3,
            formationUuid = "9007199254740993",
            ownerUid = session.GameUid, sourceServerId = session.ServerId,
        });
        JsonElement sent = JsonSerializer.SerializeToElement(new
        {
            accepted = true, requestDispatched = true, awaitingGameConfirmation = true,
            action = "attack", slot = 3, formationUuid = "9007199254740993",
            ownerUid = session.GameUid, sourceServerId = session.ServerId,
            nativeTransaction = new
            {
                formation = "9007199254740993", sendCount = 1,
                outboundRoute = "WorldMarchFormationNew", senderReturned = true,
                senderReturnedFalse = false,
                outboundFormationOrMarch = "9007199254740993",
                unattributedMatchingSendObserved = false, unattributedSendCount = 0,
            },
        });
        JsonElement verified = JsonSerializer.SerializeToElement(new
        {
            action = "attack", slot = 3, formationUuid = "9007199254740993",
            ownerUid = session.GameUid, sourceServerId = session.ServerId,
            observation = "native_owned_formation_attack_target_transition",
            marchUuid = "9007199254741999", targetUuid = "22334455",
            targetType = 9, targetSource = "native_physical_hover_original_selector",
            currentCursorVerified = true, nativeMarchPostStateVerified = true,
            nativeMarchGuidMatchesRequest = true,
            serverAcknowledgementCorrelated = true,
            nativeTransaction = new
            {
                formation = "9007199254740993", sendCount = 1,
                outboundRoute = "WorldMarchFormationNew", senderReturned = true,
                senderReturnedFalse = false,
                outboundFormationOrMarch = "9007199254740993",
                unattributedMatchingSendObserved = false, unattributedSendCount = 0,
            },
        });
        var happy = new StubNativeCommandService();
        happy.Receipt = () => happy.Calls switch
        {
            1 => chooser, 2 => waiting, 3 => sent, _ => verified,
        };
        var provider = new Feature018HotkeyNativeProviders(happy).Create()[
            Feature018HotkeyKind.AttackSquad];
        Feature018HotkeyActionOutcome success = await provider(captured, CancellationToken.None);
        Require(success.Working && happy.Calls == 4 &&
                happy.LastPayload.GetProperty("queryOnly").GetBoolean() &&
                happy.LastPayload.GetProperty("expectedGameUid").GetString() == session.GameUid &&
                happy.LastPayload.GetProperty("expectedServerId").GetInt32() == session.ServerId,
            "native attack chooser was not observed through a single initial request and three read-only polls");

        // Query-only continuation never inherits speedup item/diamond consent
        // or treats the next poll as a second captured keyboard press.
        var calls = new List<JsonElement>();
        var single = new StubNativeCommandService();
        single.Receipt = () =>
        {
            calls.Add(single.LastPayload.Clone());
            // A chooser-only native sequence must never fabricate the one
            // WorldMarchFormationNew transaction required for confirmation.
            return single.Calls == 1 ? chooser : waiting;
        };
        var noRpc = await new Feature018HotkeyNativeProviders(single).Create()[
            Feature018HotkeyKind.AttackSquad](captured, CancellationToken.None);
        Require(noRpc.Status == Feature018HotkeyActionStatus.Submitted &&
                !noRpc.Working && single.Calls == 11 &&
                !calls[0].TryGetProperty("queryOnly", out _) &&
                !calls[0].TryGetProperty("allowRapidItem", out _) &&
                !calls[0].TryGetProperty("allowRapidDiamond", out _) &&
                calls.Skip(1).All(item =>
                    item.GetProperty("queryOnly").GetBoolean() &&
                    item.GetProperty("authorizedUid").GetString() == session.GameUid &&
                    item.GetProperty("expectedGameUid").GetString() == session.GameUid &&
                    item.GetProperty("expectedServerId").GetInt32() == session.ServerId &&
                    !item.TryGetProperty("allowRapidItem", out _) &&
                    !item.TryGetProperty("allowRapidDiamond", out _)),
            "chooser-only state was credited as sent, looped beyond ten polls or repeated initial attack");

        // Malformed chooser evidence cannot arm a continuation at all.
        foreach (var malformed in new[]
        {
            new { accepted = true, requestDispatched = false, awaitingNativeChooser = true,
                chooserRequestMade = false, slot = 3, formationUuid = "9007199254740993",
                ownerUid = session.GameUid, sourceServerId = session.ServerId },
            new { accepted = true, requestDispatched = false, awaitingNativeChooser = true,
                chooserRequestMade = true, slot = 2, formationUuid = "9007199254740993",
                ownerUid = session.GameUid, sourceServerId = session.ServerId },
            new { accepted = true, requestDispatched = false, awaitingNativeChooser = true,
                chooserRequestMade = true, slot = 3, formationUuid = "invalid-uuid",
                ownerUid = session.GameUid, sourceServerId = session.ServerId },
            new { accepted = true, requestDispatched = false, awaitingNativeChooser = true,
                chooserRequestMade = true, slot = 3, formationUuid = "9007199254740993",
                ownerUid = "3133546747002289", sourceServerId = session.ServerId },
            new { accepted = true, requestDispatched = false, awaitingNativeChooser = true,
                chooserRequestMade = true, slot = 3, formationUuid = "9007199254740993",
                ownerUid = session.GameUid, sourceServerId = 2289 },
        })
        {
            var invalid = new StubNativeCommandService
            {
                Receipt = () => JsonSerializer.SerializeToElement(malformed),
            };
            Feature018HotkeyActionOutcome refused =
                await new Feature018HotkeyNativeProviders(invalid).Create()[
                    Feature018HotkeyKind.AttackSquad](captured, CancellationToken.None);
            Require(refused.Status == Feature018HotkeyActionStatus.Failed && invalid.Calls == 1,
                "malformed native formation chooser advanced to status query");
        }

        // A query response cannot silently substitute another slot, march
        // formation, account, or server, even if it claims a valid send.
        foreach (var malicious in new[]
        {
            JsonSerializer.SerializeToElement(new
            { accepted = true, requestDispatched = false, slot = 2,
              formationUuid = "9007199254740993" }),
            JsonSerializer.SerializeToElement(new
            { accepted = true, requestDispatched = false, slot = 3,
              formationUuid = "9007199254740994" }),
            JsonSerializer.SerializeToElement(new
            { accepted = true, requestDispatched = false, slot = 3,
              formationUuid = "9007199254740993", ownerUid = "3133546747002289" }),
            JsonSerializer.SerializeToElement(new
            { accepted = true, requestDispatched = false, slot = 3,
              formationUuid = "9007199254740993", sourceServerId = 2289 }),
        })
        {
            var wrong = new StubNativeCommandService();
            wrong.Receipt = () => wrong.Calls == 1 ? chooser : malicious;
            Feature018HotkeyActionOutcome refused =
                await new Feature018HotkeyNativeProviders(wrong).Create()[
                    Feature018HotkeyKind.AttackSquad](captured, CancellationToken.None);
            Require(refused.Status == Feature018HotkeyActionStatus.SessionChanged &&
                    wrong.Calls == 2 && wrong.LastPayload.GetProperty("queryOnly").GetBoolean(),
                "chooser poll accepted another owner/slot/formation or retried original hover/click");
        }
        // Claims of a network send must prove exactly one native original
        // create-march route; missing, duplicate, or wrong-route evidence
        // cannot authorize poststate confirmation.
        foreach (var invalidRpc in new[]
        {
            JsonSerializer.SerializeToElement(new { accepted = true, requestDispatched = true,
                slot = 3, formationUuid = "9007199254740993",
                ownerUid = session.GameUid, sourceServerId = session.ServerId }),
            JsonSerializer.SerializeToElement(new { accepted = true, requestDispatched = true,
                slot = 3, formationUuid = "9007199254740993",
                ownerUid = session.GameUid, sourceServerId = session.ServerId,
                nativeTransaction = new { formation = "9007199254740993",
                    sendCount = 2, outboundRoute = "WorldMarchFormationNew",
                    senderReturned = true } }),
            JsonSerializer.SerializeToElement(new { accepted = true, requestDispatched = true,
                slot = 3, formationUuid = "9007199254740993",
                ownerUid = session.GameUid, sourceServerId = session.ServerId,
                nativeTransaction = new { formation = "9007199254740993",
                    sendCount = 1, outboundRoute = "WorldMarchChange",
                    senderReturned = true } }),
        })
        {
            var wrong = new StubNativeCommandService();
            wrong.Receipt = () => wrong.Calls == 1 ? chooser : invalidRpc;
            Feature018HotkeyActionOutcome refused =
                await new Feature018HotkeyNativeProviders(wrong).Create()[
                    Feature018HotkeyKind.AttackSquad](captured, CancellationToken.None);
            Require(refused.Status == Feature018HotkeyActionStatus.Failed && wrong.Calls == 2,
                "unproven or duplicate native attack RPC reached post-send confirmation");
        }
        var rejectedAccount = new StubNativeCommandService();
        rejectedAccount.Receipt = () => rejectedAccount.Calls == 1 ? chooser :
            throw new BridgeCommandException("GAME_ACCOUNT_NOT_AUTHORIZED", "native identity switched");
        Feature018HotkeyActionOutcome switched = await new Feature018HotkeyNativeProviders(
            rejectedAccount).Create()[Feature018HotkeyKind.AttackSquad](captured, CancellationToken.None);
        Require(switched.Status == Feature018HotkeyActionStatus.SessionChanged &&
                rejectedAccount.Calls == 2,
            "chooser identity switch was not detected at the authenticated game exchange");

        // Admission is from a captured key handled by the broker, not a new
        // synthetic press on each chooser query. The broker must also refuse
        // any success if its live native session changes during the await.
        Feature018HotkeySession? liveSession = session;
        var race = new StubNativeCommandService();
        race.Receipt = () =>
        {
            if (race.Calls == 1) return chooser;
            liveSession = session with { GameUid = "3133546747002289", ServerId = 2289 };
            return verified;
        };
        var raceBroker = new Feature018HotkeyActions(session,
            () => liveSession, () => true, () => new HotkeyConfig(),
            new Feature018HotkeyNativeProviders(race).Create());
        Feature018HotkeyActionOutcome raceResult = await raceBroker.TryDispatchAsync(
            new Feature018HotkeyIntent(Feature018HotkeyKind.AttackSquad, 3));
        Require(raceResult.Status == Feature018HotkeyActionStatus.SessionChanged &&
                !raceResult.Working && race.Calls >= 2 &&
                race.LastPayload.GetProperty("queryOnly").GetBoolean(),
            "captured attack continuation credited a different session after chooser await");
    }

    private static async Task QueuedStageReinforcementChecksAsync()
    {
        var session = new Feature018HotkeySession("queue-profile", "queue-instance",
            LWBridgeWindow.Feature018ApprovedGameUid, LWBridgeWindow.Feature018ApprovedServerId);
        var g = new Feature018HotkeyIntent(Feature018HotkeyKind.ReinforceFrontline, 5);
        foreach (string scenario in new[]
        {
            "two_presses", "disabled_while_waiting", "owner_changed_while_waiting",
            "focus_lost_while_waiting", "prior_submitted", "prior_failed",
            "prior_unsupported", "prior_missing_run", "other_key_busy", "cancel_waiter",
        })
        {
            Feature018HotkeySession? current = session;
            bool focused = true;
            HotkeyConfig config = new();
            var seen = new List<Feature018HotkeyCommand>();
            var firstDone = new TaskCompletionSource<Feature018HotkeyActionOutcome>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var confirmed = new Feature018HotkeyActionOutcome(
                Feature018HotkeyActionStatus.Confirmed, "native_five_members",
                JsonSerializer.SerializeToElement(new { stageRunId = "native-run-A" }));
            var providers = new Dictionary<Feature018HotkeyKind,
                Func<Feature018HotkeyCommand, CancellationToken, Task<Feature018HotkeyActionOutcome>>>
            {
                [Feature018HotkeyKind.ReinforceFrontline] = (command, _) =>
                {
                    seen.Add(command);
                    return seen.Count == 1 ? firstDone.Task : Task.FromResult(confirmed);
                },
                [Feature018HotkeyKind.AttackSquad] = (_, _) =>
                    throw new InvalidOperationException("busy non-G action reached producer"),
            };
            var broker = new Feature018HotkeyActions(session, () => current,
                () => focused, () => config, providers);
            Task<Feature018HotkeyActionOutcome> first = broker.TryDispatchAsync(g);
            Require(seen.Count == 1 && seen[0].ExpectedStageRunId is null,
                "first G was incorrectly bound to a previous run: " + scenario);
            using var cancellation = new CancellationTokenSource();
            Task<Feature018HotkeyActionOutcome> waiting = broker.TryDispatchAsync(g, cancellation.Token);
            Require(!waiting.IsCompleted && seen.Count == 1,
                "second physical G was dropped or dispatched concurrently: " + scenario);
            Feature018HotkeyActionOutcome firstResult = confirmed;
            Feature018HotkeyActionStatus expected = Feature018HotkeyActionStatus.Failed;
            switch (scenario)
            {
                case "two_presses": expected = Feature018HotkeyActionStatus.Confirmed; break;
                case "disabled_while_waiting":
                    config = config with { FrontlineReinforce = false };
                    expected = Feature018HotkeyActionStatus.Disabled;
                    break;
                case "owner_changed_while_waiting":
                    current = session with { GameUid = "different-owner" };
                    expected = Feature018HotkeyActionStatus.SessionChanged;
                    break;
                case "focus_lost_while_waiting":
                    focused = false;
                    expected = Feature018HotkeyActionStatus.NotFocused;
                    break;
                case "prior_submitted":
                    firstResult = new(Feature018HotkeyActionStatus.Submitted, "unknown_native_outcome");
                    break;
                case "prior_failed":
                    firstResult = new(Feature018HotkeyActionStatus.Failed, "native_failure");
                    break;
                case "prior_unsupported":
                    firstResult = new(Feature018HotkeyActionStatus.Unsupported, "outside_stage");
                    break;
                case "prior_missing_run":
                    firstResult = new(Feature018HotkeyActionStatus.Confirmed, "missing_stage_identity");
                    break;
                case "other_key_busy":
                    Require((await broker.TryDispatchAsync(new(Feature018HotkeyKind.AttackSquad, 1))).Status ==
                        Feature018HotkeyActionStatus.Busy, "non-G action no longer returns Busy");
                    break;
                case "cancel_waiter": cancellation.Cancel(); break;
            }
            firstDone.SetResult(firstResult);
            _ = await first;
            if (scenario == "cancel_waiter")
            {
                try
                {
                    _ = await waiting;
                    throw new InvalidOperationException("cancelled waiting G was dispatched");
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            }
            else
            {
                Feature018HotkeyActionOutcome outcome = await waiting;
                Require(outcome.Status == expected, "wrong waiting G outcome: " + scenario + ": " + outcome.Status);
            }
            if (scenario == "two_presses")
            {
                Require(seen.Count == 2 && seen[1].ExpectedStageRunId == "native-run-A",
                    "second captured G did not dispatch once against the preceding native run");
                _ = await broker.TryDispatchAsync(g);
                Require(seen.Count == 3 && seen[2].ExpectedStageRunId is null,
                    "a fresh nonqueued physical press inherited an old run binding");
            }
            else
            {
                Require(seen.Count == 1, "invalid queued G replayed or automatically resent: " + scenario);
            }
        }
        Console.WriteLine("FEATURE018_G_QUEUED_STAGE_PRESS_PASS");
    }

    private static void CaptureExactRootRaceChecks()
    {
        var os = new FakeCapturePlatform();
        var dispatched = new List<Feature018HotkeyIntent>();
        bool inputClear = true;
        int nativeFocusChecks = 0;
        using var capture = new Feature018HotkeyCapture(
            43210, @"C:\fake\LastWar.exe", new DateTime(2026, 10, 17, 0, 0, 0, DateTimeKind.Utc),
            () => { nativeFocusChecks++; return inputClear; }, dispatched.Add, os);

        Require(capture.IsExactForegroundOwner(),
            "approved root and owned GUI keyboard focus were incorrectly rejected");
        os.KeyboardFocusOwned = false;
        Require(!capture.IsExactForegroundOwner(),
            "async native broker focus gate admitted root while keyboard focused elsewhere");
        os.KeyboardFocusOwned = true;
        os.ExactOwner = false;
        Require(!capture.IsExactForegroundOwner(),
            "async native broker focus gate admitted replaced owner");
        os.ExactOwner = true;
        capture.PollOnceForChecks(); // Exact owned root, no pressed keys: arm.
        os.DownKeys.Add(Keys.Q);
        capture.PollOnceForChecks();
        Require(dispatched.SequenceEqual(new[] {
                    new Feature018HotkeyIntent(Feature018HotkeyKind.AttackSquad, 1) }) &&
                capture.LastObservedSequence == 1 &&
                capture.SnapshotCapturedIntents() is { Length: 1 } first &&
                first[0].CaptureId == capture.CaptureId &&
                first[0].ForegroundHwnd == 101 &&
                first[0].GamePid == 43210 && nativeFocusChecks == 4,
            "real capture timer seam did not retain one authenticated native intent observation");
        capture.PollOnceForChecks(); // Still held; no repeated key edge.
        Require(dispatched.Count == 1 && capture.LastObservedSequence == 1,
            "held Q generated a second recognizer callback");

        // Two different foreground roots can belong to the *same* game PID.
        // Never label an edge read from root 101 as if it came from root 202.
        os.DownKeys.Clear();
        capture.PollOnceForChecks();
        os.DownKeys.Add(Keys.F6);
        os.OnKeyRead = () => os.Foreground = (nint)202;
        capture.PollOnceForChecks();
        Require(dispatched.Count == 1 && capture.LastObservedSequence == 1 &&
                capture.SnapshotCapturedIntents().Length == 1,
            "foreground sibling swap during GetAsyncKeyState fabricated a shield key intent");
        os.OnKeyRead = null;
        capture.PollOnceForChecks(); // Held F6 after unsafe sample: must remain disarmed.
        Require(dispatched.Count == 1, "held F6 on new owned HWND rearmed without release");
        os.DownKeys.Clear();
        capture.PollOnceForChecks();
        os.DownKeys.Add(Keys.F6);
        capture.PollOnceForChecks();
        Require(dispatched.Count == 2 &&
                capture.SnapshotCapturedIntents() is { Length: 2 } afterSibling &&
                afterSibling[1].ForegroundHwnd == 202 &&
                afterSibling[1].Sequence == 2 &&
                afterSibling[1].Kind == Feature018HotkeyKind.UseShield &&
                afterSibling[1].Slot == 8,
            "new exact root did not require a release and then capture the next independent F6");

        os.DownKeys.Clear();
        capture.PollOnceForChecks();
        os.DownKeys.Add(Keys.Q);
        os.OnKeyRead = () => inputClear = false;
        capture.PollOnceForChecks();
        Require(dispatched.Count == 2, "game-native text-input focus changed after sample and still dispatched Q");
        os.OnKeyRead = null;
        inputClear = true;
        capture.PollOnceForChecks();
        Require(dispatched.Count == 2, "Q held through chat focus transition unexpectedly triggered");
        os.DownKeys.Clear();
        capture.PollOnceForChecks();
        os.DownKeys.Add(Keys.Q);
        os.OnKeyRead = () => os.KeyboardFocusOwned = false;
        capture.PollOnceForChecks();
        Require(dispatched.Count == 2, "keyboard focus left owned root while keys were read");
        os.OnKeyRead = null;
        os.KeyboardFocusOwned = true;
        capture.PollOnceForChecks();
        Require(dispatched.Count == 2, "held key after keyboard-focus return bypassed rearming");

        os.DownKeys.Clear();
        capture.PollOnceForChecks();
        os.DownKeys.Add(Keys.Q);
        os.OnKeyRead = () => os.ExactOwner = false;
        capture.PollOnceForChecks();
        Require(dispatched.Count == 2, "replaced owner/PID during key read dispatched an action");
        os.OnKeyRead = null;
        os.ExactOwner = true;
        os.DownKeys.Clear();
        capture.PollOnceForChecks();
        os.DownKeys.Add(Keys.D2);
        os.DownKeys.Add(Keys.Menu);
        capture.PollOnceForChecks();
        Require(dispatched.Count == 3 &&
                dispatched[^1] == new Feature018HotkeyIntent(
                    Feature018HotkeyKind.ApplyEquipmentPreset, 2) &&
                capture.SnapshotCapturedIntents()[^1].ForegroundHwnd == 202,
            "verified Alt+2 callback failed after an untrusted owner transition");
        Console.WriteLine("FEATURE018_CAPTURE_EXACT_ROOT_RACE_PASS nativeGameAck=false");
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
                    !game.CanHandle("feature018_equipment_apply") &&
                    !game.CanHandle("feature018_rapid_action"),
                "retired or mutating native command remained registered");
            await ExpectEquipmentErrorAsync("COMMAND_NOT_IMPLEMENTED", () =>
                game.InvokeAsync("feature018_rapid_action",
                    JsonSerializer.SerializeToElement(new { mode = "status", slot = 1 }),
                    CancellationToken.None));
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
