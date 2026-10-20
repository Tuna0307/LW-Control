using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class Feature018OwnedGameExchangeChecks
{
    private static void Require(bool condition, string detail)
    {
        if (!condition) throw new InvalidDataException("FEATURE018_EXCHANGE: " + detail);
    }

    private static async Task RequireErrorAsync(string expected, Func<Task> action)
    {
        try { await action(); }
        catch (BridgeCommandException error) when (error.Code == expected) { return; }
        throw new InvalidDataException("FEATURE018_EXCHANGE: expected native error " + expected);
    }

    private static void FreshHeartbeatReadRaceChecks()
    {
        var owner = new OverviewMapScanSession("heartbeat-profile", "heartbeat-session",
            "heartbeat-challenge", 43210, @"C:\test\LastWar.exe", "2026-10-17T00:00:00Z");
        byte[] Heartbeat(string uid = "2553945035002288", int server = 2288,
            long? updatedAt = null, string? challenge = null) => JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 1, bridgeVersion = OverviewLifecycleService.BridgeVersion,
            profileId = owner.ProfileId, sessionId = owner.SessionId,
            challenge = challenge ?? owner.Challenge, gamePid = owner.GamePid,
            ready = true, messageVisible = true, messageText = OverviewLifecycleService.ReadyMessage,
            updatedAt = updatedAt ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            gameStateObserved = true, loggedIn = true, connected = true,
            gameUid = uid, serverId = server,
        });
        int reads = 0;
        Require(Feature018OwnedGameExchange.ReadFreshAuthenticatedAccount("unused", owner,
            _ => ++reads == 1 ? "{"u8.ToArray() : Heartbeat()) is
                { GameUid: "2553945035002288", ServerId: 2288 } && reads == 2,
            "truncated first heartbeat did not reread the fresh authenticated heartbeat");
        reads = 0;
        Require(Feature018OwnedGameExchange.ReadFreshAuthenticatedAccount("unused", owner,
            _ => ++reads == 1 ? throw new IOException("concurrent writer") : Heartbeat()) is
                { GameUid: "2553945035002288", ServerId: 2288 } && reads == 2,
            "transient heartbeat IO read did not reread successfully");
        reads = 0;
        Require(Feature018OwnedGameExchange.ReadFreshAuthenticatedAccount("unused", owner,
            _ => { reads++; return "{"u8.ToArray(); }) is null && reads == 3,
            "persistently truncated heartbeat escaped the three-read bound");
        reads = 0;
        Require(Feature018OwnedGameExchange.ReadFreshAuthenticatedAccount("unused", owner,
            _ => { reads++; throw new IOException("persistent IO"); }) is null && reads == 3,
            "persistent heartbeat IO escaped the three-read bound");
        foreach (var (uid, server) in new[]
        {
            ("3133546747002289", 2289), ("3133546747002212", 2212),
            ("2553945035002288", 2289), ("2553945035002288", 2212),
        })
        {
            Feature018AuthenticatedGameAccount? admitted =
                Feature018OwnedGameExchange.ReadFreshAuthenticatedAccount("unused", owner,
                    _ => Heartbeat(uid: uid, server: server));
            Require(admitted == new Feature018AuthenticatedGameAccount(uid, server),
                "product account admission rejected valid arbitrary UID/server pair");
        }
        foreach (byte[] invalid in new[]
        {
            Heartbeat(uid: ""), Heartbeat(uid: "not-a-uid"),
            Heartbeat(uid: "0001234"), Heartbeat(server: 0), Heartbeat(server: -1),
            JsonSerializer.SerializeToUtf8Bytes(new
            {
                schemaVersion = 1, bridgeVersion = OverviewLifecycleService.BridgeVersion,
                profileId = owner.ProfileId, sessionId = owner.SessionId,
                challenge = owner.Challenge, gamePid = owner.GamePid,
                ready = true, messageVisible = true, messageText = OverviewLifecycleService.ReadyMessage,
                updatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                gameStateObserved = true, loggedIn = true, connected = true,
                gameUid = "3133546747002289", serverId = "2289",
            }),
            Heartbeat(updatedAt: DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds()),
            Heartbeat(challenge: "different-challenge"),
        })
        {
            reads = 0;
            Require(Feature018OwnedGameExchange.ReadFreshAuthenticatedAccount("unused", owner,
                _ => ++reads == 1 ? invalid : Heartbeat()) is null && reads == 1,
                "parsed wrong/stale heartbeat retried into a later acceptable account");
        }
    }

    internal static async Task RunAsync()
    {
        await LockedMailboxAsync();
        await CityTransientIdentityAsync();
        FreshHeartbeatReadRaceChecks();
        var city = Feature018GameCommandService.ReadFields("city_layout_apply_start",
            JsonSerializer.SerializeToElement(new
            {
                baseRevision = "city-v26-42",
                placements = new[]
                {
                    new { uuid = "9007199254740993", targetPointId = 456 },
                    new { uuid = "9007199254740994", targetPointId = 457 },
                },
            }));
        Require(city["baseRevision"] == "city-v26-42" &&
                city["placements"] == "9007199254740993:456;9007199254740994:457",
            "city long identity lost precision across native transport");
        var restore = Feature018GameCommandService.ReadFields("city_layout_restore_preflight",
            JsonSerializer.SerializeToElement(new { jobId = "own-city-job-42" }));
        Require(restore.Count == 1 && restore["jobId"] == "own-city-job-42",
            "read-only original City restoration did not require exact owner job ID");
        var liveRestore = Feature018GameCommandService.ReadFields("city_layout_restore_start",
            JsonSerializer.SerializeToElement(new {
                jobId = "own-city-job-42", restoreOptIn = true, profileId = "approved-runtime" }));
        Require(liveRestore.Count == 2 && liveRestore["jobId"] == "own-city-job-42" &&
            liveRestore["restoreOptIn"] == "1" && !liveRestore.ContainsKey("requestId"),
            "original City restore must use game-owned destinations and exchange-owned unique request ID");
        foreach (object invalidRestore in new object[]
        {
            new { jobId = "own-city-job-42", restoreOptIn = false },
            new { jobId = "own-city-job-42" },
            new { jobId = "own-city-job-42", restoreOptIn = true, pointId = 4411 },
            new { jobId = "own-city-job-42", restoreOptIn = true,
                placements = new[] { new { uuid = "1392040865799139725", pointId = 4442 } } },
            new { jobId = "own-city-job-42", restoreOptIn = true, requestId = "forged" },
            new { jobId = "bad job", restoreOptIn = true },
            new { jobId = "own-city-job-42", restoreOptIn = true, profileId = "" },
        })
            await RequireErrorAsync("INVALID_REQUEST", () => Task.Run(() =>
                Feature018GameCommandService.ReadFields("city_layout_restore_start",
                    JsonSerializer.SerializeToElement(invalidRestore))));
        using (JsonDocument duplicateRestore = JsonDocument.Parse(
            "{\"jobId\":\"one\",\"jobId\":\"two\",\"restoreOptIn\":true}"))
            await RequireErrorAsync("INVALID_REQUEST", () => Task.Run(() =>
                Feature018GameCommandService.ReadFields("city_layout_restore_start",
                    duplicateRestore.RootElement)));
        await RequireErrorAsync("INVALID_REQUEST", () => Task.Run(() =>
            Feature018GameCommandService.ReadFields("city_layout_restore_preflight",
                JsonSerializer.SerializeToElement(new { jobId = "wrong job" }))));
        var shield = Feature018GameCommandService.ReadFields("feature018_hotkey_action",
            JsonSerializer.SerializeToElement(new { action = "shieldUse", slot = 24 }));
        Require(shield["slot"] == "24", "original F8 24-hour binding rejected");
        var frontline = Feature018GameCommandService.ReadFields("feature018_hotkey_action",
            JsonSerializer.SerializeToElement(new { action = "frontlineReinforce", slot = 5 }));
        Require(frontline["slot"] == "5", "original G five-soldier binding rejected");
        var confirmedFrontlineTap = Feature018GameCommandService.ReadFields(
            "feature018_frontline_action",
            JsonSerializer.SerializeToElement(new { frontlineOptIn = true, profileId = "approved-runtime" }));
        Require(confirmedFrontlineTap.Count == 3 &&
            confirmedFrontlineTap["action"] == "frontlineReinforce" &&
            confirmedFrontlineTap["slot"] == "5" &&
            confirmedFrontlineTap["frontlineOptIn"] == "1",
            "source-native five-soldier tap lost its fresh owner confirmation");
        foreach (object forgedFrontline in new object[]
        {
            new { frontlineOptIn = false },
            new { action = "frontlineReinforce", frontlineOptIn = true },
            new { slot = 5, frontlineOptIn = true },
            new { frontlineOptIn = true, authorizedUid = "3133546747002212" },
            new { frontlineOptIn = true, heroId = 22 },
            new { frontlineOptIn = true, profileId = "" },
            new { frontlineOptIn = true, profileId = 5 },
        })
            await RequireErrorAsync("INVALID_REQUEST", () => Task.Run(() =>
                Feature018GameCommandService.ReadFields("feature018_frontline_action",
                    JsonSerializer.SerializeToElement(forgedFrontline))));
        await RequireErrorAsync("INVALID_REQUEST", () => Task.Run(() =>
            Feature018GameCommandService.ReadFields("feature018_frontline_action",
                JsonSerializer.SerializeToElement(new { }))));
        using (JsonDocument repeatedFrontline = JsonDocument.Parse(
            "{\"frontlineOptIn\":true,\"frontlineOptIn\":true}"))
            await RequireErrorAsync("INVALID_REQUEST", () => Task.Run(() =>
                Feature018GameCommandService.ReadFields("feature018_frontline_action",
                    repeatedFrontline.RootElement)));
        await RequireErrorAsync("INVALID_REQUEST", () => Task.Run(() =>
            Feature018GameCommandService.ReadFields("feature018_hotkey_action",
                JsonSerializer.SerializeToElement(new { action = "shieldUse", slot = 5 }))));
        await RequireErrorAsync("INVALID_REQUEST", () => Task.Run(() =>
            Feature018GameCommandService.ReadFields("automation_start",
                JsonSerializer.SerializeToElement(new { taskName = "otherTask" }))));
        await RequireErrorAsync("INVALID_REQUEST", () => Task.Run(() =>
            Feature018GameCommandService.ReadFields("city_layout_apply_start",
                JsonSerializer.SerializeToElement(new { baseRevision = "a", placements = new[]
                {
                    new { uuid = "15", targetPointId = 1 },
                    new { uuid = "15", targetPointId = 2 },
                } }))));
        var targetedLand = Feature018GameCommandService.ReadFields("mini_land_status",
            JsonSerializer.SerializeToElement(new { expectedCellId = 6 }));
        Require(targetedLand.Count == 1 && targetedLand["expectedCellId"] == "6",
            "read-only exact previously dispatched land-cell status field lost");
        var targetedUnlock = Feature018GameCommandService.ReadFields("monopoly_cell_open",
            JsonSerializer.SerializeToElement(new { expectedCellId = 6 }));
        Require(targetedUnlock.Count == 1 && targetedUnlock["expectedCellId"] == "6",
            "one-shot native land unlock must preserve its expected locked cell ID");
        var scopedLand = Feature018GameCommandService.ReadFields("mini_land_status",
            JsonSerializer.SerializeToElement(new { profileId = "approved-runtime", expectedCellId = 6 }));
        Require(scopedLand.Count == 1 && scopedLand["expectedCellId"] == "6",
            "profile-scoped exact land read rejected or forwarded untrusted routing metadata");
        Require(Feature018GameCommandService.ReadFields("mini_land_status",
            JsonSerializer.SerializeToElement(new { })).Count == 0,
            "ordinary next unopened cell status changed schema");
        Require(Feature018GameCommandService.ReadFields("feature018_shield_inventory_get",
            JsonSerializer.SerializeToElement(new { })).Count == 0,
            "native shield read-only query changed transport schema");
        Require(Feature018GameCommandService.ReadFields("feature018_shield_inventory_get",
            JsonSerializer.SerializeToElement(new { profileId = "approved-runtime" })).Count == 0,
            "read-only shield must discard profile-scoped UI routing metadata");
        Require(Feature018GameCommandService.ReadFields("feature018_equipment_snapshot",
            JsonSerializer.SerializeToElement(new { profileId = "approved-runtime" })).Count == 0,
            "read-only equipment snapshot rejected or forwarded profile-scoped UI routing metadata");
        Require(Feature018GameCommandService.ReadFields("feature018_equipment_snapshot",
            JsonSerializer.SerializeToElement(new { })).Count == 0,
            "read-only equipment snapshot stopped accepting the legacy empty payload");
        foreach (object forbiddenEquipmentSnapshot in new object[]
        {
            new { profileId = "" },
            new { profileId = new string('x', 129) },
            new { profileId = "approved-runtime", ownerUid = "3133546747002212" },
            new { profileId = "approved-runtime", slot = 1 },
        })
            await RequireErrorAsync("INVALID_REQUEST", () => Task.Run(() =>
                Feature018GameCommandService.ReadFields("feature018_equipment_snapshot",
                    JsonSerializer.SerializeToElement(forbiddenEquipmentSnapshot))));
        Require(Feature018GameCommandService.ReadFields("mini_frontline_status",
            JsonSerializer.SerializeToElement(new { profileId = "approved-runtime" })).Count == 0,
            "original Frontline status forwarded untrusted client owner identity");
        Require(Feature018GameCommandService.ReadFields("mini_frontline_status",
            JsonSerializer.SerializeToElement(new { })).Count == 0,
            "original Frontline status stopped accepting empty read-only payload");
        foreach (object forbiddenFrontlineStatus in new object[]
        {
            new { slot = 5 },
            new { heroId = 203 },
            new { action = "frontlineReinforce" },
            new { frontlineOptIn = true },
            new { profileId = "" },
            new { profileId = "approved-runtime", ownerUid = "3133546747002212" },
        })
            await RequireErrorAsync("INVALID_REQUEST", () => Task.Run(() =>
                Feature018GameCommandService.ReadFields("mini_frontline_status",
                    JsonSerializer.SerializeToElement(forbiddenFrontlineStatus))));
        var shopRead = Feature018GameCommandService.ReadFields("feature018_shield_shop",
            JsonSerializer.SerializeToElement(new { mode = "inspect", slot = 12, profileId = "approved-runtime" }));
        Require(shopRead.Count == 2 && shopRead["mode"] == "inspect" &&
                shopRead["slot"] == "12", "shop read-only inspection forwarded untrusted profile identity");
        var shopBuy = Feature018GameCommandService.ReadFields("feature018_shield_shop",
            JsonSerializer.SerializeToElement(new { mode = "buy", slot = 24, gemOptIn = true }));
        Require(shopBuy.Count == 3 && shopBuy["slot"] == "24" && shopBuy["gemOptIn"] == "1",
            "exact user-approved native 24-hour shop operation rejected");
        var armReplacement = Feature018GameCommandService.ReadFields("feature018_shield_shop",
            JsonSerializer.SerializeToElement(new { mode = "armUse", slot = 8,
                shieldReplaceOptIn = true }));
        Require(armReplacement["shieldReplaceOptIn"] == "1", "source-native shield replacement explicit consent lost");
        var nativeFormationEditor = Feature018GameCommandService.ReadFields("feature018_attack_prepare",
            JsonSerializer.SerializeToElement(new { mode = "staffingEditor", slot = 2, editorOptIn = true }));
        Require(nativeFormationEditor.Count == 3 && nativeFormationEditor["slot"] == "2" &&
            nativeFormationEditor["editorOptIn"] == "1",
            "original manual formation staffing editor consent/slot not marshalled");
        var selectedNativeTarget = Feature018GameCommandService.ReadFields("feature018_attack_prepare",
            JsonSerializer.SerializeToElement(new { mode = "selectorTarget" }));
        Require(selectedNativeTarget.Count == 1 && selectedNativeTarget["mode"] == "selectorTarget",
            "original selected-target observation forwarded client-supplied targeting fields");
        var verifiedAttackReadiness = Feature018GameCommandService.ReadFields("feature018_attack_prepare",
            JsonSerializer.SerializeToElement(new { mode = "attackReadiness", slot = 2 }));
        Require(verifiedAttackReadiness.Count == 2 &&
            verifiedAttackReadiness["mode"] == "attackReadiness" &&
            verifiedAttackReadiness["slot"] == "2",
            "read-only original attack prerequisites lost native squad slot");
        var cursorNative = Feature018GameCommandService.ReadFields("feature018_attack_prepare",
            JsonSerializer.SerializeToElement(new { mode = "cursorPoint" }));
        Require(cursorNative.Count == 1 && cursorNative["mode"] == "cursorPoint",
            "read-only authenticated cursor point diagnostic rejected");
        var pixelNative = Feature018GameCommandService.ReadFields("feature018_attack_prepare",
            JsonSerializer.SerializeToElement(new { mode = "cursorPoint", samplePixelX = 1432,
                samplePixelY = 706 }));
        Require(pixelNative.Count == 3 && pixelNative["samplePixelX"] == "1432" &&
                pixelNative["samplePixelY"] == "706",
            "read-only cursor pixel coordinate validation lost both sampled inputs");
        var hoverOpen = Feature018GameCommandService.ReadFields("feature018_attack_prepare",
            JsonSerializer.SerializeToElement(new { mode = "cursorOpen", targetKind = "monster" }));
        Require(hoverOpen.Count == 2 && hoverOpen["mode"] == "cursorOpen" &&
            hoverOpen["targetKind"] == "monster",
            "native cursor UI selector admission lost verified hover-target kind");
        foreach (object invalidHover in new object[]
        {
            new { mode = "cursorOpen", targetKind = "worldBoss" },
            new { mode = "cursorOpen", targetKind = "monster", targetUuid = "9922" },
            new { mode = "cursorOpen", targetKind = "resource", samplePixelX = 400 },
            new { mode = "cursorOpen", targetKind = "city", samplePixelY = 100 },
        })
            await RequireErrorAsync("INVALID_REQUEST", () => Task.Run(() =>
                Feature018GameCommandService.ReadFields("feature018_attack_prepare",
                    JsonSerializer.SerializeToElement(invalidHover))));
        foreach (string relocation in new[] { "randomRelocate", "allianceRelocate" })
        {
            Dictionary<string, string> readiness = Feature018GameCommandService.ReadFields(
                "feature018_relocation_preflight",
                JsonSerializer.SerializeToElement(new { action = relocation, profileId = "approved-runtime" }));
            Require(readiness.Count == 1 && readiness["action"] == relocation,
                "authenticated native F9/F10 readiness forwarded untrusted UI fields");
        }
        foreach (object invalidRelocation in new object[]
        {
            new { action = "randomRelocate", itemUuid = "forged-stack" },
            new { action = "allianceRelocate", destinationPoint = 501 },
            new { action = "randomRelocate", authorizedUid = "3133546747002212" },
            new { action = "allianceRelocate", optIn = true },
            new { action = "MoveCrossServer" },
            new { action = "randomRelocate", profileId = "" },
            new { action = "randomRelocate", profileId = 2 },
        })
            await RequireErrorAsync("INVALID_REQUEST", () => Task.Run(() =>
                Feature018GameCommandService.ReadFields("feature018_relocation_preflight",
                    JsonSerializer.SerializeToElement(invalidRelocation))));
        using (JsonDocument repeatedRelocation = JsonDocument.Parse(
            "{\"action\":\"randomRelocate\",\"action\":\"allianceRelocate\"}"))
            await RequireErrorAsync("INVALID_REQUEST", () => Task.Run(() =>
                Feature018GameCommandService.ReadFields("feature018_relocation_preflight",
                    repeatedRelocation.RootElement)));
        foreach (object invalidEditor in new object[]
        {
            new { mode = "inventory", slot = 1 },
            new { mode = "selectorTarget", targetUuid = "123" },
            new { mode = "selectorTarget", pointIndex = 1723 },
            new { mode = "cursorPoint", samplePixelX = 14 },
            new { mode = "cursorPoint", samplePixelY = 14 },
            new { mode = "cursorPoint", samplePixelX = -1, samplePixelY = 12 },
            new { mode = "cursorPoint", samplePixelX = 12, samplePixelY = 32768 },
            new { mode = "cursorPoint", samplePixelX = 4.5, samplePixelY = 5 },
            new { mode = "cursorPoint", samplePixelX = 12, samplePixelY = 15, targetUuid = "123" },
            new { mode = "attackReadiness" },
            new { mode = "attackReadiness", slot = 0 },
            new { mode = "attackReadiness", slot = 5 },
            new { mode = "attackReadiness", slot = 1, targetUuid = "forged" },
            new { mode = "attackReadiness", slot = 1, authorizedUid = "3133546747002212" },
            new { mode = "staffingEditor", slot = 2 },
            new { mode = "staffingEditor", slot = 2, editorOptIn = false },
            new { mode = "staffingEditor", slot = 0, editorOptIn = true },
            new { mode = "staffingEditor", slot = 4, editorOptIn = true, targetUuid = "12" },
            new { mode = "staffingEditor", slot = 1, editorOptIn = true,
                authorizedUid = "3133546747002212" },
        })
            await RequireErrorAsync("INVALID_REQUEST", () => Task.Run(() =>
                Feature018GameCommandService.ReadFields("feature018_attack_prepare",
                    JsonSerializer.SerializeToElement(invalidEditor))));
        foreach (object forged in new object[]
        {
            new { mode = "inspect", slot = 12, gemOptIn = true },
            new { mode = "status", slot = 12, shieldReplaceOptIn = true },
            new { mode = "buy", slot = 12, gemOptIn = true, itemUuid = "forged" },
            new { mode = "buy", slot = 12, gemOptIn = true, authorizedUid = "3133546747002212" },
            new { mode = "buy", slot = 12, gemOptIn = false },
            new { mode = "armUse", slot = 12, shieldReplaceOptIn = false },
            new { mode = "inspect", slot = "12" },
            new { mode = "inspect", slot = 8, profileId = "" },
        })
            await RequireErrorAsync("INVALID_REQUEST", () => Task.Run(() =>
                Feature018GameCommandService.ReadFields("feature018_shield_shop",
                    JsonSerializer.SerializeToElement(forged))));
        foreach (string duplicate in new[]
        {
            "{\"mode\":\"inspect\",\"mode\":\"buy\",\"slot\":8,\"gemOptIn\":true}",
            "{\"mode\":\"buy\",\"slot\":8,\"slot\":24,\"gemOptIn\":true}",
            "{\"mode\":\"buy\",\"slot\":8,\"gemOptIn\":true,\"gemOptIn\":true}",
            "{\"mode\":\"inspect\",\"slot\":8,\"profileId\":\"a\",\"profileId\":\"b\"}",
        })
        {
            using JsonDocument duplicatePayload = JsonDocument.Parse(duplicate);
            await RequireErrorAsync("INVALID_REQUEST", () => Task.Run(() =>
                Feature018GameCommandService.ReadFields("feature018_shield_shop",
                    duplicatePayload.RootElement)));
        }
        await RequireErrorAsync("INVALID_REQUEST", () => Task.Run(() =>
            Feature018GameCommandService.ReadFields("feature018_shield_inventory_get",
                JsonSerializer.SerializeToElement(new { itemId = "10001", itemOptIn = true }))));
        foreach (var invalid in new object[]
                 { new { expectedCellId = -1 }, new { expectedCellId = 0 },
                   new { expectedCellId = 10001 }, new { expectedCellId = "6" },
                   new { expectedCellId = 6, itemOptIn = true } })
            await RequireErrorAsync("INVALID_REQUEST", () => Task.Run(() =>
                Feature018GameCommandService.ReadFields("mini_land_status",
                    JsonSerializer.SerializeToElement(invalid))));
        await RequireErrorAsync("INVALID_REQUEST", () => Task.Run(() =>
            Feature018GameCommandService.ReadFields("monopoly_cell_open",
                JsonSerializer.SerializeToElement(new { expectedCellId = 6, gemOptIn = true }))));
        string root = Path.Combine(Path.GetTempPath(), "feature018-exchange-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string request = Path.Combine(root, "request.txt");
            string reply = Path.Combine(root, "response.json");
            var owner = new OverviewMapScanSession("test-profile", "test-session", "test-challenge",
                43210, "C:\\test\\LastWar.exe", "2026-10-17T00:00:00Z");
            OverviewMapScanSession? current = null;
            var withoutOwner = new Feature018OwnedGameExchange(
                () => current, expected => current == expected, request, reply);
            await RequireErrorAsync("GAME_CONNECTION_UNAVAILABLE", () =>
                withoutOwner.ExchangeAsync("city_layout_snapshot_get", null,
                    TimeSpan.FromSeconds(1), CancellationToken.None));
            Require(!File.Exists(request), "request written without authenticated owner");

            current = owner;
            bool approved = false;
            var accountGated = new Feature018OwnedGameExchange(
                () => current, expected => current == expected, request, reply,
                approvedAccount: _ => approved);
            await RequireErrorAsync("GAME_ACCOUNT_NOT_AUTHORIZED", () =>
                accountGated.ExchangeAsync("monopoly_cell_open", null,
                    TimeSpan.FromSeconds(1), CancellationToken.None));
            Require(!File.Exists(request), "rejected account wrote a live action request");
            var boundAccount = new Feature018AuthenticatedGameAccount("3133546747002289", 2289);
            var accountAtExchange = new Feature018AuthenticatedGameAccount("3133546747002212", 2212);
            var expectedAccountExchange = new Feature018OwnedGameExchange(
                () => current, expected => current == expected, request, reply,
                currentAccount: _ => accountAtExchange);
            await RequireErrorAsync("GAME_CONNECTION_UNAVAILABLE", () =>
                expectedAccountExchange.ExchangeAsync("feature018_hotkey_action", null,
                    TimeSpan.FromSeconds(1), CancellationToken.None,
                    expectedAccount: boundAccount));
            Require(!File.Exists(request), "captured hotkey account changed before dispatch yet wrote request");
            var hotkeyService = new Feature018GameCommandService(expectedAccountExchange);
            await RequireErrorAsync("GAME_CONNECTION_UNAVAILABLE", () =>
                hotkeyService.InvokeAsync("feature018_hotkey_action",
                    JsonSerializer.SerializeToElement(new
                    {
                        action = "recall", slot = 1,
                        expectedGameUid = boundAccount.GameUid,
                        expectedServerId = boundAccount.ServerId,
                    }), CancellationToken.None));
            Require(!File.Exists(request), "hotkey identity fence was not carried through managed command service");
            await RequireErrorAsync("INVALID_REQUEST", () =>
                hotkeyService.InvokeAsync("feature018_hotkey_action",
                    JsonSerializer.SerializeToElement(new
                    { action = "recall", slot = 1, expectedGameUid = boundAccount.GameUid }),
                    CancellationToken.None));
            await RequireErrorAsync("INVALID_REQUEST", () =>
                hotkeyService.InvokeAsync("feature018_hotkey_action",
                    JsonSerializer.SerializeToElement(new
                    { action = "recall", slot = 1, expectedGameUid = "forged", expectedServerId = 2289 }),
                    CancellationToken.None));
            string responseState = "confirmed";
            bool badChallenge = false;
            bool retireOwner = false;
            var exchange = new Feature018OwnedGameExchange(
                () => current, expected => current == expected, request, reply,
                delay: (_, _) =>
                {
                    Dictionary<string, string> command = File.ReadAllLines(request)
                        .Where(line => line.Contains('='))
                        .Select(line => line.Split('=', 2))
                        .ToDictionary(pair => pair[0], pair => pair[1]);
                    if (retireOwner) current = null;
                    File.WriteAllText(reply, JsonSerializer.Serialize(new
                    {
                        schemaVersion = 1,
                        requestId = command["requestId"],
                        profileId = command["profileId"],
                        launchSessionId = command["launchSessionId"],
                        challenge = badChallenge ? "other-challenge" : command["challenge"],
                        gamePid = int.Parse(command["gamePid"]),
                        command = command["command"],
                        observedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        state = responseState,
                        data = new { cellId = 12345, requestDispatched = true },
                    }));
                    return Task.CompletedTask;
                });

            // The shipping bridge requires this field on every command. Verify
            // it is stamped by the native-authenticated exchange, even when
            // a command has no payload and no Hotkey-specific UID reader.
            var observedStamps = new List<(string Command, string Uid)>();
            var stampedExchange = new Feature018OwnedGameExchange(
                () => current, expected => current == expected, request, reply,
                currentAccount: _ => accountAtExchange,
                delay: (_, _) =>
                {
                    var source = File.ReadAllLines(request).Select(line => line.Split('=', 2))
                        .Where(parts => parts.Length == 2)
                        .ToDictionary(parts => parts[0], parts => parts[1]);
                    Require(source.ContainsKey("authorizedUid") &&
                            File.ReadAllLines(request).Count(line =>
                                line.StartsWith("authorizedUid=", StringComparison.Ordinal)) == 1,
                        "outbound mailbox missing or duplicated native authenticated UID");
                    observedStamps.Add((source["command"], source["authorizedUid"]));
                    File.WriteAllText(reply, JsonSerializer.Serialize(new
                    {
                        schemaVersion = 1, requestId = source["requestId"],
                        profileId = source["profileId"], launchSessionId = source["launchSessionId"],
                        challenge = source["challenge"], gamePid = owner.GamePid,
                        command = source["command"],
                        observedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        state = "confirmed", data = new { observed = true },
                    }));
                    return Task.CompletedTask;
                });
            foreach (string command in new[] { "city_layout_snapshot_get", "mini_land_status",
                "feature018_shield_inventory_get", "feature018_hotkey_action" })
            {
                await stampedExchange.ExchangeAsync(command, null,
                    TimeSpan.FromSeconds(2), CancellationToken.None);
            }
            Require(observedStamps.Count == 4 && observedStamps.All(row =>
                    row.Uid == accountAtExchange.GameUid),
                "ordinary FEATURE018 requests did not bind authenticated arbitrary-account UID");
            await RequireErrorAsync("INVALID_REQUEST", () =>
                stampedExchange.ExchangeAsync("mini_land_status",
                    new Dictionary<string, string> { ["authorizedUid"] = "2553945035002288" },
                    TimeSpan.FromSeconds(2), CancellationToken.None));
            Require(observedStamps.Count == 4,
                "forged command UID reached the game mailbox");

            JsonElement result = await exchange.ExchangeAsync("monopoly_cell_open",
                new Dictionary<string, string> { ["cellKind"] = "next" },
                TimeSpan.FromSeconds(2), CancellationToken.None);
            Require(result.GetProperty("cellId").GetInt32() == 12345,
                "correlated confirmed response not delivered");
            foreach (var (first, second) in new[]
            {
                (new Feature018AuthenticatedGameAccount("3133546747002289", 2289),
                 new Feature018AuthenticatedGameAccount("2553945035002288", 2289)),
                (new Feature018AuthenticatedGameAccount("2553945035002288", 2288),
                 new Feature018AuthenticatedGameAccount("2553945035002288", 2212)),
            })
            {
                Feature018AuthenticatedGameAccount? active = first;
                var changingAccount = new Feature018OwnedGameExchange(
                    () => current, expected => current == expected, request, reply,
                    delay: (_, _) => { active = second; return Task.CompletedTask; },
                    currentAccount: _ => active);
                await RequireErrorAsync("GAME_CONNECTION_UNAVAILABLE", () =>
                    changingAccount.ExchangeAsync("monopoly_cell_open", null,
                        TimeSpan.FromSeconds(1), CancellationToken.None));
            }
            var missingAccount = new Feature018OwnedGameExchange(
                () => current, expected => current == expected, request, reply,
                currentAccount: _ => null);
            await RequireErrorAsync("GAME_ACCOUNT_NOT_AUTHORIZED", () =>
                missingAccount.ExchangeAsync("monopoly_cell_open", null,
                    TimeSpan.FromSeconds(1), CancellationToken.None));
            approved = true;
            var changedDuringResponse = new Feature018OwnedGameExchange(
                () => current, expected => current == expected, request, reply,
                delay: (_, _) =>
                {
                    approved = false;
                    return Task.CompletedTask;
                }, approvedAccount: _ => approved);
            await RequireErrorAsync("GAME_ACCOUNT_NOT_AUTHORIZED", () =>
                changedDuringResponse.ExchangeAsync("monopoly_cell_open", null,
                    TimeSpan.FromSeconds(1), CancellationToken.None,
                    allowAcceptedDispatch: true));

            responseState = "accepted";
            await RequireErrorAsync("GAME_ACTION_UNCONFIRMED", () =>
                exchange.ExchangeAsync("monopoly_cell_open", null,
                    TimeSpan.FromSeconds(2), CancellationToken.None));
            JsonElement submitted = await exchange.ExchangeAsync("monopoly_cell_open", null,
                TimeSpan.FromSeconds(2), CancellationToken.None, allowAcceptedDispatch: true);
            Require(submitted.GetProperty("requestDispatched").GetBoolean() &&
                    submitted.GetProperty("cellId").GetInt32() == 12345,
                "known accepted request not distinguished from actual completion");
            responseState = "confirmed";
            badChallenge = true;
            await RequireErrorAsync("GAME_RESPONSE_UNTRUSTED", () =>
                exchange.ExchangeAsync("monopoly_cell_open", null,
                    TimeSpan.FromSeconds(2), CancellationToken.None));
            badChallenge = false;
            retireOwner = true;
            await RequireErrorAsync("GAME_CONNECTION_UNAVAILABLE", () =>
                exchange.ExchangeAsync("monopoly_cell_open", null,
                    TimeSpan.FromSeconds(2), CancellationToken.None));
            current = owner;
            var requestIds = new HashSet<string>(StringComparer.Ordinal);
            Func<TimeSpan, CancellationToken, Task> twoClientGame = (_, _) =>
            {
                Dictionary<string, string> currentRequest = File.ReadAllLines(request)
                    .Where(line => line.Contains('='))
                    .Select(line => line.Split('=', 2))
                    .ToDictionary(parts => parts[0], parts => parts[1]);
                requestIds.Add(currentRequest["requestId"]);
                File.WriteAllText(reply, JsonSerializer.Serialize(new
                {
                    schemaVersion = 1,
                    requestId = currentRequest["requestId"],
                    profileId = owner.ProfileId,
                    launchSessionId = owner.SessionId,
                    challenge = owner.Challenge,
                    gamePid = owner.GamePid,
                    command = currentRequest["command"],
                    observedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    state = "confirmed",
                    data = new { observedCommand = currentRequest["command"] },
                }));
                return Task.CompletedTask;
            };
            var clientOne = new Feature018OwnedGameExchange(
                () => current, session => current == session,
                request, reply, delay: twoClientGame);
            var clientTwo = new Feature018OwnedGameExchange(
                () => current, session => current == session,
                request, reply, delay: twoClientGame);
            Task<JsonElement> cityRequest = clientOne.ExchangeAsync("city_layout_snapshot_get", null,
                TimeSpan.FromSeconds(2), CancellationToken.None);
            Task<JsonElement> landRequest = clientTwo.ExchangeAsync("mini_land_status", null,
                TimeSpan.FromSeconds(2), CancellationToken.None);
            await Task.WhenAll(cityRequest, landRequest);
            Require(cityRequest.Result.GetProperty("observedCommand").GetString() == "city_layout_snapshot_get" &&
                    landRequest.Result.GetProperty("observedCommand").GetString() == "mini_land_status" &&
                    requestIds.Count == 2,
                "two independent game command services collided in one owner mailbox");
            Console.WriteLine("FEATURE018_EXCHANGE_OWNER_CORRELATION_PASS");
        }
        finally { Directory.Delete(root, recursive: true); }
    }
    private static async Task LockedMailboxAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "feature018-lock-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string request = Path.Combine(root, "request.txt"), reply = Path.Combine(root, "response.json");
        var owner = new OverviewMapScanSession("lock-profile", "lock-session", "lock-challenge",
            43210, @"C:\test\LastWar.exe", "2026-10-17T00:00:00Z");
        OverviewMapScanSession? current = owner;
        FileStream? requestLock = null, responseLock = null;
        try
        {
            File.WriteAllText(request, "previous");
            Directory.CreateDirectory(reply); // Opening a directory for read gives access denied on Windows.
            requestLock = new FileStream(request, FileMode.Open, FileAccess.Read, FileShare.Read);
            int waits = 0;
            var ids = new HashSet<string>();
            var exchange = new Feature018OwnedGameExchange(() => current, expected => current == expected,
                request, reply, delay: (_, _) =>
                {
                    waits++;
                    if (requestLock is not null)
                    {
                        Require(File.ReadAllText(request) == "previous", "locked request was changed");
                        Require(Directory.GetFiles(root, "*.tmp-*").Length == 1, "retry must keep one owned envelope");
                        requestLock.Dispose(); requestLock = null;
                    }
                    else
                    {
                        var fields = File.ReadAllLines(request).Select(line => line.Split('=', 2))
                            .Where(parts => parts.Length == 2).ToDictionary(parts => parts[0], parts => parts[1]);
                        ids.Add(fields["requestId"]);
                        responseLock?.Dispose(); responseLock = null;
                        if (Directory.Exists(reply)) Directory.Delete(reply);
                        File.WriteAllText(reply, JsonSerializer.Serialize(new
                        {
                            schemaVersion = 1, requestId = fields["requestId"], profileId = owner.ProfileId,
                            launchSessionId = owner.SessionId, challenge = owner.Challenge,
                            gamePid = owner.GamePid, command = fields["command"],
                            observedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), state = "confirmed",
                            data = new { equipmentApplied = true },
                        }));
                        if (waits == 2) responseLock = new FileStream(reply, FileMode.Open, FileAccess.Read, FileShare.None);
                    }
                    return Task.CompletedTask;
                });
            var result = await exchange.ExchangeAsync("feature018_equipment_edit", null,
                TimeSpan.FromSeconds(2), CancellationToken.None);
            Require(result.GetProperty("equipmentApplied").GetBoolean() && waits == 3 && ids.Count == 1,
                "request/response lock recovery did not retain exactly one publication");
            Require(Directory.GetFiles(root, "*.tmp-*").Length == 0, "successful retry leaked temporary envelope");

            // A permanently locked old request must never reach the game.
            string previous = File.ReadAllText(request);
            requestLock = new FileStream(request, FileMode.Open, FileAccess.Read, FileShare.Read);
            int retries = 0;
            var locked = new Feature018OwnedGameExchange(() => current, expected => current == expected,
                request, reply, delay: (_, _) => { retries++; return Task.CompletedTask; });
            await RequireErrorAsync("GAME_COMMAND_NOT_PUBLISHED", () => locked.ExchangeAsync(
                "feature018_equipment_edit", null, TimeSpan.FromSeconds(2), CancellationToken.None));
            Require(retries == 5 && File.ReadAllText(request) == previous && Directory.GetFiles(root, "*.tmp-*").Length == 0,
                "permanent request lock escaped bound, changed mailbox or leaked temporary file");
            requestLock.Dispose(); requestLock = null;

            // A real Windows access-denied replacement (destination directory)
            // follows the same bounded no-publication path.
            File.Delete(request); Directory.CreateDirectory(request); retries = 0;
            await RequireErrorAsync("GAME_COMMAND_NOT_PUBLISHED", () => locked.ExchangeAsync(
                "feature018_equipment_edit", null, TimeSpan.FromSeconds(2), CancellationToken.None));
            Require(retries == 5 && Directory.Exists(request), "access-denied replacement was not bounded");
            Directory.Delete(request); File.WriteAllText(request, previous);
            requestLock = new FileStream(request, FileMode.Open, FileAccess.Read, FileShare.Read);
            var retired = new Feature018OwnedGameExchange(() => current, expected => current == expected,
                request, reply, delay: (_, _) => { current = null; return Task.CompletedTask; });
            await RequireErrorAsync("GAME_CONNECTION_UNAVAILABLE", () => retired.ExchangeAsync(
                "feature018_equipment_edit", null, TimeSpan.FromSeconds(2), CancellationToken.None));
            Require(File.ReadAllText(request) == previous, "retired owner published a retried command");
            Console.WriteLine("FEATURE018_MAILBOX_LOCK_RECOVERY_PASS");
        }
        finally
        {
            requestLock?.Dispose(); responseLock?.Dispose();
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task CityTransientIdentityAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "r18-city-identity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string request = Path.Combine(root, "request.txt"), reply = Path.Combine(root, "reply.json");
            var owner = new OverviewMapScanSession("city-profile", "city-session", "city-challenge",
                12345, @"C:\test\LastWar.exe", "2026-10-19T00:00:00Z");
            var account = new Feature018AuthenticatedGameAccount("90001", 999);
            bool available = false;
            DateTimeOffset now = DateTimeOffset.UtcNow;
            var sent = new HashSet<string>();
            var exchange = new Feature018OwnedGameExchange(
                () => available ? owner : null, value => value == owner, request, reply,
                utcNow: () => now, currentAccount: _ => available ? account : null,
                retainedSession: () => owner,
                delay: (_, _) =>
                {
                    if (!available)
                    {
                        Require(!File.Exists(request), "City sent during unavailable identity");
                        now = now.AddSeconds(16);
                        available = true;
                    }
                    else
                    {
                        var wire = File.ReadAllLines(request).Select(line => line.Split('=', 2))
                            .Where(pair => pair.Length == 2).ToDictionary(pair => pair[0], pair => pair[1]);
                        sent.Add(wire["requestId"]);
                        File.WriteAllText(reply, JsonSerializer.Serialize(new
                        {
                            schemaVersion = 1, requestId = wire["requestId"], profileId = owner.ProfileId,
                            launchSessionId = owner.SessionId, challenge = owner.Challenge, gamePid = owner.GamePid,
                            command = wire["command"], observedAt = now.ToUnixTimeMilliseconds(),
                            state = "confirmed", data = new { pausedAndResumed = true },
                        }));
                    }
                    return Task.CompletedTask;
                });
            JsonElement value = await exchange.ExchangeAsync("city_layout_apply_status", null,
                TimeSpan.FromSeconds(30), CancellationToken.None, expectedAccount: account);
            Require(value.GetProperty("pausedAndResumed").GetBoolean() && sent.Count == 1,
                "City transient 16s identity gap aborted or replayed request");
        }
        finally { Directory.Delete(root, true); }
        Console.WriteLine("FEATURE018_CITY_TRANSIENT_IDENTITY_PASS");
    }
}
