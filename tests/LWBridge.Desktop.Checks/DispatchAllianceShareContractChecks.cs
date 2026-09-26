using System.Runtime.CompilerServices;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class DispatchAllianceShareContractChecks
{
    [ModuleInitializer]
    internal static void Run()
    {
        MissingAndCountValidationMatchesOriginal();
        RowValidationMatchesOriginal();
        CurrentV19PayloadIsSourceBacked();
        CurrentV19TransportBoundsFailClosed();
    }

    private static void MissingAndCountValidationMatchesOriginal()
    {
        ExpectError(
            JsonSerializer.SerializeToElement(new { }, JsonOptions.Default),
            "select between 1 and 200 dispatch tasks");

        ExpectError(
            JsonSerializer.SerializeToElement(
                new { rows = Array.Empty<object>() },
                JsonOptions.Default),
            "select between 1 and 200 dispatch tasks");

        ExpectError(
            JsonSerializer.SerializeToElement(
                new
                {
                    rows = Enumerable.Range(0, 201)
                        .Select(index => (object)new
                        {
                            uuid = (10_000 + index).ToString(),
                            serverId = 88,
                            x = 1,
                            y = 1,
                            cfgId = 1,
                        })
                        .ToArray(),
                },
                JsonOptions.Default),
            "select between 1 and 200 dispatch tasks");
    }

    private static void RowValidationMatchesOriginal()
    {
        JsonElement valid = JsonSerializer.SerializeToElement(
            new
            {
                rows = new object[]
                {
                    new
                    {
                        uuid = "0",
                        serverId = "100000",
                        x = "12",
                        y = 34.9,
                        cfgId = "+56",
                        ownerName = "Owner",
                        allianceAbbr = "ALLY",
                    },
                    new
                    {
                        uuid = "1417409824803038247",
                        serverId = 88,
                        x = 100,
                        y = 200,
                        cfgId = 300,
                    },
                },
            },
            JsonOptions.Default);

        IReadOnlyList<DispatchAllianceShareRow> rows =
            DispatchAllianceShareContract.NormalizeRows(valid);
        Check(
            rows.Count == 2 &&
            rows[0].Uuid == "0" &&
            rows[0].ServerId == 100_000 &&
            rows[0].X == 12 &&
            rows[0].Y == 34 &&
            rows[0].CfgId == 56 &&
            rows[0].OwnerName == "Owner" &&
            rows[0].AllianceAbbr == "ALLY",
            "recovered Dispatch share row accepts decimal UUID text and positive integer-like row fields");

        object[] invalidRows =
        [
            new { uuid = "", serverId = 88, x = 1, y = 1, cfgId = 1 },
            new { uuid = "1a", serverId = 88, x = 1, y = 1, cfgId = 1 },
            new { uuid = "1", serverId = 0, x = 1, y = 1, cfgId = 1 },
            new { uuid = "1", serverId = 88, x = 0, y = 1, cfgId = 1 },
            new { uuid = "1", serverId = 88, x = 1, y = 0, cfgId = 1 },
            new { uuid = "1", serverId = 88, x = 1, y = 1, cfgId = 0 },
        ];
        foreach (object invalid in invalidRows)
        {
            ExpectError(
                JsonSerializer.SerializeToElement(
                    new { rows = new[] { invalid } },
                    JsonOptions.Default),
                "selected dispatch task cannot be shared");
        }
    }

    private static void CurrentV19PayloadIsSourceBacked()
    {
        DispatchAllianceShareRow row =
            DispatchAllianceShareContract.NormalizeRows(
                JsonSerializer.SerializeToElement(
                    new
                    {
                        rows = new[]
                        {
                            new
                            {
                                uuid = "1417409824803038247",
                                serverId = 2212,
                                x = 333,
                                y = 444,
                                cfgId = 555,
                                ownerName = "Player",
                                allianceAbbr = "TAG",
                            },
                        },
                    },
                    JsonOptions.Default))[0];

        CurrentV19DispatchAllianceSharePlan plan =
            DispatchAllianceShareContract.BuildCurrentV19Plan(row);
        JsonElement point = plan.PointShareParam;
        Check(
            plan.TargetServer == 2212 &&
            plan.TaskUuid == 1_417_409_824_803_038_247L &&
            plan.PostType == "Text_PointShare" &&
            plan.ShareChannel == "TO_ALLIANCE" &&
            plan.Command == "hero.dispatch.share.chat",
            "current-v19 Dispatch share plan pins the official post/channel/command and exact target identity");
        Check(
            point.GetProperty("x").GetInt64() == 333 &&
            point.GetProperty("y").GetInt64() == 444 &&
            point.GetProperty("sid").GetInt64() == 2212 &&
            point.GetProperty("dispatch").GetInt32() == 1 &&
            point.GetProperty("cfgId").GetInt64() == 555 &&
            point.GetProperty("uuid").GetString() == "1417409824803038247" &&
            point.GetProperty("uname").GetString() == "Player" &&
            point.GetProperty("abbr").GetString() == "TAG" &&
            !point.TryGetProperty("oname", out _),
            "current-v19 point-share payload is reconstructable without fabricating oname because Dispatch ShareDecode uses localization key 456288");

        DispatchAllianceShareRow noOwner = row with
        {
            OwnerName = null,
            AllianceAbbr = "IGNORED",
        };
        JsonElement noOwnerPoint =
            DispatchAllianceShareContract.BuildCurrentV19Plan(noOwner)
                .PointShareParam;
        Check(
            !noOwnerPoint.TryGetProperty("uname", out _) &&
            !noOwnerPoint.TryGetProperty("abbr", out _),
            "current-v19 ShareEncode only carries abbr through the uname branch");
    }

    private static void CurrentV19TransportBoundsFailClosed()
    {
        DispatchAllianceShareRow baseRow = new(
            88,
            "1",
            1,
            1,
            1,
            null,
            null,
            "{}");

        ExpectInvalidOperation(
            baseRow with { ServerId = (long)int.MaxValue + 1 },
            "targetServer uses PutInt");
        ExpectInvalidOperation(
            baseRow with { Uuid = "9223372036854775808" },
            "uuid uses PutLong");
    }

    private static void ExpectError(
        JsonElement payload,
        string message)
    {
        try
        {
            _ = DispatchAllianceShareContract.NormalizeRows(payload);
            throw new InvalidOperationException(
                "expected Dispatch alliance-share validation failure");
        }
        catch (BridgeCommandException error)
            when (error.Code == "INVALID_REQUEST" &&
                  error.Message == message)
        {
        }
    }

    private static void ExpectInvalidOperation(
        DispatchAllianceShareRow row,
        string fragment)
    {
        try
        {
            _ = DispatchAllianceShareContract.BuildCurrentV19Plan(row);
            throw new InvalidOperationException(
                "expected current-v19 Dispatch share transport failure");
        }
        catch (InvalidOperationException error)
            when (error.Message.Contains(fragment, StringComparison.Ordinal))
        {
        }
    }

    private static void Check(bool condition, string name)
    {
        if (!condition)
            throw new InvalidOperationException(name);
    }
}
