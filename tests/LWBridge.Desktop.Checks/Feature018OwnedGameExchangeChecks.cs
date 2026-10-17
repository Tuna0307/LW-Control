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

    internal static async Task RunAsync()
    {
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
        var shield = Feature018GameCommandService.ReadFields("feature018_hotkey_action",
            JsonSerializer.SerializeToElement(new { action = "shieldUse", slot = 24 }));
        Require(shield["slot"] == "24", "original F8 24-hour binding rejected");
        var frontline = Feature018GameCommandService.ReadFields("feature018_hotkey_action",
            JsonSerializer.SerializeToElement(new { action = "frontlineReinforce", slot = 5 }));
        Require(frontline["slot"] == "5", "original G five-soldier binding rejected");
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

            JsonElement result = await exchange.ExchangeAsync("monopoly_cell_open",
                new Dictionary<string, string> { ["cellKind"] = "next" },
                TimeSpan.FromSeconds(2), CancellationToken.None);
            Require(result.GetProperty("cellId").GetInt32() == 12345,
                "correlated confirmed response not delivered");
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
}
