using System.Text.Json;
using System.Text.Json.Nodes;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class Feature018EquipmentProviderChecks
{
    private sealed class FakeCommands : INativeAsyncCommandService
    {
        internal readonly List<JsonElement> Requests = [];
        internal Func<int, JsonElement, object?> Respond = (_, _) => null;
        public bool CanHandle(string command) => command == "feature018_hotkey_action";
        public Task<object?> InvokeAsync(string command, JsonElement payload, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Require(command == "feature018_hotkey_action", "unexpected native command");
            Requests.Add(payload.Clone());
            return Task.FromResult(Respond(Requests.Count, payload));
        }
    }

    private static readonly Feature018HotkeySession Owner =
        new("owner", "launch", "3133546747002289", 2289);
    private static void Require(bool condition, string detail)
    {
        if (!condition) throw new InvalidOperationException("FEATURE018_EQUIPMENT_PROVIDER: " + detail);
    }
    private static Feature018HotkeyActions Coordinator(INativeAsyncCommandService service) =>
        new(Owner, () => Owner, () => true,
            () => new HotkeyConfig(Equipment: true, RandomRelocate: true, AllianceRelocate: true),
            new Feature018HotkeyNativeProviders(service).Create());
    private static JsonElement Element(JsonObject value) => JsonSerializer.SerializeToElement(value);
    private static JsonObject Equipment(string preset = "owner-preset", int slot = 1) => new()
    {
        ["observation"] = "native_HeroEquip_preset_slots_applied",
        ["equipmentApplied"] = true, ["slot"] = slot, ["presetId"] = preset,
        ["completedSteps"] = 2, ["plannedSteps"] = 2,
        ["slots"] = new JsonArray(new JsonObject
        {
            ["heroUuid"] = "9007199254740993", ["slot"] = 1,
            ["currentEquipUuid"] = "9007199254740995",
            ["targetEquipUuid"] = "9007199254740995",
        }),
    };
    private static JsonObject Relocation(string action = "randomRelocate") => new()
    {
        ["observation"] = "native_relocation_reply_item_and_world_position",
        ["action"] = action, ["slot"] = 0, ["relocationConfirmed"] = true,
        ["requestDispatched"] = true, ["awaitingGameConfirmation"] = false,
        ["replyObserved"] = true,
        ["replyExactItemUuidObserved"] = true,
        ["replyItemIdFallbackObserved"] = false,
        ["replyItemIdentityCorrelated"] = true,
        ["replyUseOperationCompatible"] = true,
        ["independentWorldAndInventoryPoststateVerified"] = true,
        ["clientRequestNonceAvailable"] = false, ["serverAckAttributable"] = false,
        ["worldPointChanged"] = true,
        ["ownerUid"] = Owner.GameUid, ["sourceServerId"] = Owner.ServerId,
        ["itemUuid"] = "e43623c3ec5e4cd78ae1b3f904094fd7", ["itemId"] = "10004",
        ["beforeCount"] = 2, ["afterCount"] = 1,
        ["beforeWorldPointId"] = 440444, ["afterWorldPointId"] = 540544,
    };
    private static JsonObject AllianceRelocation() => new()
    {
        ["observation"] = "native_alliance_relocation_reply_world_position",
        ["action"] = "allianceRelocate", ["slot"] = 0,
        ["relocationConfirmed"] = true, ["requestDispatched"] = true,
        ["awaitingGameConfirmation"] = false,
        ["ownerSelectedRemoteRallyObserved"] = true,
        ["allianceMoveRequestObserved"] = true, ["allianceMoveSendCount"] = 1,
        ["allianceMoveReplyObserved"] = true, ["worldMvReason240Observed"] = true,
        ["replyResultOneObserved"] = true,
        ["serverAckAttributableByOneSendFence"] = true,
        ["independentWorldAndInventoryPoststateVerified"] = true,
        ["worldPointChanged"] = true,
        ["ownerUid"] = Owner.GameUid, ["sourceServerId"] = Owner.ServerId,
        ["selectedRallyServerId"] = 2213, ["replyServerId"] = 2213,
        ["itemUuid"] = "e43623c3ec5e4cd78ae1b3f904094fd7", ["itemId"] = "10005",
        ["beforeCount"] = 2, ["afterCount"] = 1,
        ["beforeWorldPointId"] = 440444, ["afterWorldPointId"] = 540544,
        ["acknowledgedWorldPointId"] = 540544,
    };
    private static JsonElement Pending(string identity = "owner-preset", bool equipment = true) =>
        Element(new JsonObject
        {
            ["requestDispatched"] = true, ["awaitingGameConfirmation"] = true,
            ["slot"] = equipment ? 1 : 0,
            [equipment ? "presetId" : "itemUuid"] = identity,
        });

    internal static async Task RunAsync()
    {
        await TrustedEquipmentConfigSurvivesPendingAndConfirmationAsync();
        await ChangedPresetDuringConfirmationIsRejectedAsync();
        await ChangedRelocationItemDuringConfirmationIsRejectedAsync();
        await OriginalOpaqueRelocationItemUsesSingleSendAndReadOnlyPollAsync();
        await InvalidFinalObservationsAreRejectedAsync();
        Console.WriteLine("FEATURE018_EQUIPMENT_RELOCATION_PROVIDER_PASS");
    }

    private static async Task TrustedEquipmentConfigSurvivesPendingAndConfirmationAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "feature018-equipment-provider-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new ProfileRuntimeConfigStore(Path.Combine(root, "config.json"));
            store.SaveEquipmentConfig(JsonNode.Parse("""
                {"equipmentPresets":[{"id":"owner-preset","name":"Native owner gear",
                "squads":[{"squadIndex":1,"positions":[{"position":1,
                "equips":[{"slot":1,"equipUuid":"9007199254740995"}]}]}]}]}
                """)!.AsObject());
            var equipment = new EquipmentConfigCommandService(store);
            string requestPath = Path.Combine(root, "request.txt");
            string replyPath = Path.Combine(root, "reply.json");
            var session = new OverviewMapScanSession("owner", "launch", "challenge", 12345,
                @"C:\fake\LastWar.exe", "2026-10-17T00:00:00Z");
            var transmitted = new List<Dictionary<string, string>>();
            var exchange = new Feature018OwnedGameExchange(() => session, actual => actual == session,
                requestPath, replyPath, delay: (_, _) =>
                {
                    var fields = File.ReadAllLines(requestPath).Where(line => line.Contains('='))
                        .Select(line => line.Split('=', 2)).ToDictionary(pair => pair[0], pair => pair[1]);
                    transmitted.Add(fields);
                    JsonObject data = transmitted.Count < 3
                        ? JsonSerializer.Deserialize<JsonObject>(Pending().GetRawText())!
                        : Equipment();
                    File.WriteAllText(replyPath, JsonSerializer.Serialize(new
                    {
                        schemaVersion = 1, requestId = fields["requestId"], profileId = fields["profileId"],
                        launchSessionId = fields["launchSessionId"], challenge = fields["challenge"],
                        gamePid = int.Parse(fields["gamePid"]), command = fields["command"],
                        observedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        state = transmitted.Count < 3 ? "accepted" : "confirmed", data,
                    }));
                    return Task.CompletedTask;
                }, currentAccount: _ => new Feature018AuthenticatedGameAccount(Owner.GameUid, Owner.ServerId));
            var game = new Feature018GameCommandService(exchange, equipment);
            var coordinator = Coordinator(game);
            Feature018HotkeyActionOutcome outcome = await coordinator.TryDispatchAsync(
                new(Feature018HotkeyKind.ApplyEquipmentPreset, 1));
            Require(outcome.Working && transmitted.Count == 3,
                "accepted and intermediate pending preset did not wait for authoritative final slots");
            Require(!transmitted[0].ContainsKey("queryOnly") &&
                    transmitted.Skip(1).All(fields => fields["queryOnly"] == "1"),
                "confirmation sent another initial equipment action");
            foreach (var fields in transmitted)
            {
                Require(fields["action"] == "equipment" && fields["slot"] == "1" &&
                        fields["presetId"] == "owner-preset", "preset identity changed across native requests");
                using JsonDocument presets = JsonDocument.Parse(fields["confirmedEquipmentPresets"]);
                Require(presets.RootElement[0].GetProperty("squads")[0].GetProperty("positions")[0]
                            .GetProperty("equips")[0].GetProperty("equipUuid").GetString() == "9007199254740995",
                    "trusted saved equipment UUID lost precision during action or pending poll");
            }
            // Extra caller fields cannot replace the confirmed native profile config.
            foreach (var injected in new[] { "presetId", "confirmedEquipmentPresets" })
            {
                var payload = new JsonObject { ["action"] = "equipment", ["slot"] = 1, [injected] = "forged" };
                await game.InvokeAsync("feature018_hotkey_action", Element(payload), CancellationToken.None);
                var last = transmitted[^1];
                Require(last["presetId"] == "owner-preset" &&
                        !last["confirmedEquipmentPresets"].Contains("forged", StringComparison.Ordinal),
                    "caller-supplied equipment config overrode persisted profile authority");
            }
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static async Task ChangedPresetDuringConfirmationIsRejectedAsync()
    {
        var service = new FakeCommands
        {
            Respond = (count, _) => count == 1 ? Pending() : Element(Equipment("other-preset")),
        };
        Feature018HotkeyActionOutcome outcome = await Coordinator(service).TryDispatchAsync(
            new(Feature018HotkeyKind.ApplyEquipmentPreset, 1));
        Require(outcome.Status == Feature018HotkeyActionStatus.SessionChanged && !outcome.Working,
            "another preset's complete native receipt confirmed the pending shortcut");
        Require(service.Requests.Count == 2 && service.Requests[1].GetProperty("queryOnly").GetBoolean(),
            "preset mismatch was retried or dispatched as a fresh action");
    }

    private static async Task ChangedRelocationItemDuringConfirmationIsRejectedAsync()
    {
        var service = new FakeCommands
        {
            Respond = (count, _) => count == 1 ? Pending("111001", false) : Element(Relocation()),
        };
        Feature018HotkeyActionOutcome outcome = await Coordinator(service).TryDispatchAsync(
            new(Feature018HotkeyKind.RandomRelocate, 0));
        Require(outcome.Status == Feature018HotkeyActionStatus.SessionChanged && !outcome.Working,
            "another item's relocation confirmed the initially dispatched item");
        Require(service.Requests.Count == 2 && service.Requests[1].GetProperty("queryOnly").GetBoolean(),
            "relocation identity mismatch caused another action send");
    }

    private static async Task OriginalOpaqueRelocationItemUsesSingleSendAndReadOnlyPollAsync()
    {
        const string ownedStack = "e43623c3ec5e4cd78ae1b3f904094fd7";
        var service = new FakeCommands
        {
            Respond = (count, _) => count == 1 ? Pending(ownedStack, false) : Element(Relocation()),
        };
        Feature018HotkeyActionOutcome outcome = await Coordinator(service).TryDispatchAsync(
            new(Feature018HotkeyKind.RandomRelocate, 0));
        Require(outcome.Working && service.Requests.Count == 2 &&
                !service.Requests[0].TryGetProperty("queryOnly", out _) &&
                service.Requests[1].TryGetProperty("queryOnly", out JsonElement readOnly) &&
                readOnly.ValueKind == JsonValueKind.True,
            "opaque original ItemUse stack UUID did not confirm via exactly one initial send and one read-only query");
        var bad = new FakeCommands
        {
            Respond = (count, _) =>
            {
                if (count == 1) return Pending(ownedStack, false);
                JsonObject wrongItem = Relocation();
                wrongItem["itemUuid"] = "different-original-stack";
                return Element(wrongItem);
            },
        };
        Feature018HotkeyActionOutcome mismatch = await Coordinator(bad).TryDispatchAsync(
            new(Feature018HotkeyKind.RandomRelocate, 0));
        Require(mismatch.Status == Feature018HotkeyActionStatus.SessionChanged && !mismatch.Working &&
                bad.Requests.Count == 2 && bad.Requests[1].GetProperty("queryOnly").GetBoolean(),
            "opaque original ItemUse stack mismatch was credited or led to a second send");
    }

    private static async Task InvalidFinalObservationsAreRejectedAsync()
    {
        var equipmentCases = new Action<JsonObject>[]
        {
            data => data["slot"] = 2,
            data => data["equipmentApplied"] = false,
            data => data["completedSteps"] = 1,
            data => data["plannedSteps"] = -1,
            data => data["slots"] = new JsonArray(),
            data => data["slots"]![0]!["currentEquipUuid"] = "999",
        };
        foreach (var corrupt in equipmentCases)
        {
            JsonObject data = Equipment(); corrupt(data);
            var service = new FakeCommands { Respond = (_, _) => Element(data) };
            Require((await Coordinator(service).TryDispatchAsync(new(Feature018HotkeyKind.ApplyEquipmentPreset, 1))).Status ==
                    Feature018HotkeyActionStatus.Failed, "incomplete or mismatched equipment observation falsely confirmed");
            Require(service.Requests.Count == 1, "invalid equipment receipt initiated polling/retransmit");
        }
        var randomService = new FakeCommands { Respond = (_, _) => Element(Relocation()) };
        Require((await Coordinator(randomService).TryDispatchAsync(
                    new(Feature018HotkeyKind.RandomRelocate, 0))).Working,
            "valid native F9 relocation reply plus consumed item and changed city was rejected");
        JsonObject fallback = Relocation();
        fallback["replyExactItemUuidObserved"] = false;
        fallback["replyItemIdFallbackObserved"] = true;
        var fallbackService = new FakeCommands { Respond = (_, _) => Element(fallback) };
        Require((await Coordinator(fallbackService).TryDispatchAsync(
                    new(Feature018HotkeyKind.RandomRelocate, 0))).Working,
            "original ItemUse itemId fallback with one-send poststate was rejected");
        int randomNegativeIndex = 0;
        foreach (var corrupt in new Action<JsonObject>[]
        {
            data => data["action"] = "allianceRelocate",
            data => data["requestDispatched"] = false,
            data => data["awaitingGameConfirmation"] = null,
            data => data["replyObserved"] = false,
            data => { data["replyExactItemUuidObserved"] = false; data["replyItemIdFallbackObserved"] = false; },
            data => { data["replyExactItemUuidObserved"] = true; data["replyItemIdFallbackObserved"] = true; },
            data => data["replyItemIdentityCorrelated"] = false,
            data => data["replyUseOperationCompatible"] = false,
            data => data["independentWorldAndInventoryPoststateVerified"] = false,
            data => data["worldPointChanged"] = false,
            data => data["ownerUid"] = "2553945035002288",
            data => data["sourceServerId"] = 2288,
            data => data["afterCount"] = 2,
            data => data["itemUuid"] = "not:a:native:stack:uuid",
        })
        {
            JsonObject data = Relocation(); corrupt(data);
            randomService.Respond = (_, _) => Element(data);
            Feature018HotkeyActionOutcome outcome = await Coordinator(randomService)
                .TryDispatchAsync(new(Feature018HotkeyKind.RandomRelocate, 0));
            Require(!outcome.Working,
                $"unmatched F9 relocation receipt falsely confirmed (case {randomNegativeIndex}: {outcome.Status}/{outcome.Reason})");
            randomNegativeIndex++;
        }

        var allianceService = new FakeCommands { Respond = (_, _) => Element(AllianceRelocation()) };
        Require((await Coordinator(allianceService).TryDispatchAsync(
                    new(Feature018HotkeyKind.AllianceRelocate, 0))).Working,
            "valid F10 owner-selection/reason240 relocation receipt was rejected");
        int allianceNegativeIndex = 0;
        foreach (var corrupt in new Action<JsonObject>[]
        {
            data => data["action"] = "randomRelocate",
            data => data["requestDispatched"] = false,
            data => data["ownerSelectedRemoteRallyObserved"] = false,
            data => data["allianceMoveRequestObserved"] = false,
            data => data["allianceMoveSendCount"] = 2,
            data => data["allianceMoveReplyObserved"] = false,
            data => data["worldMvReason240Observed"] = false,
            data => data["replyResultOneObserved"] = false,
            data => data["serverAckAttributableByOneSendFence"] = false,
            data => data["replyServerId"] = 2214,
            data => data["acknowledgedWorldPointId"] = 540545,
            data => data["independentWorldAndInventoryPoststateVerified"] = false,
            data => data["afterCount"] = 2,
        })
        {
            JsonObject data = AllianceRelocation(); corrupt(data);
            allianceService.Respond = (_, _) => Element(data);
            Feature018HotkeyActionOutcome outcome = await Coordinator(allianceService)
                .TryDispatchAsync(new(Feature018HotkeyKind.AllianceRelocate, 0));
            Require(!outcome.Working,
                $"unmatched F10 relocation receipt falsely confirmed (case {allianceNegativeIndex}: {outcome.Status}/{outcome.Reason})");
            allianceNegativeIndex++;
        }
    }
}
