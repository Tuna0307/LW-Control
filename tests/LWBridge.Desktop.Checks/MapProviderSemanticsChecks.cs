using System.Runtime.CompilerServices;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class MapProviderSemanticsChecks
{
    [ModuleInitializer]
    internal static void Run()
    {
        GhostPreparationPreservesSourceBackedRow();
        GhostPreparationAllowsFrontendDelay();
        GhostPreparationMatchesExpiryBoundary();
        GhostPreparationPreservesExpiryReaderContract();
        GhostPreparationRejectsMutatedCounts();
        GhostPreparationRejectsEarlyOrExpiredTiming();
        GhostPreparationRejectsInvalidIdentity();
    }

    private static void GhostPreparationPreservesSourceBackedRow()
    {
        JsonElement row = Row();
        IReadOnlyList<JsonElement> prepared =
            CurrentClientMap317ActionProvider.PrepareGhostPlunderRows([row]);
        Check(prepared.Count == 1, "Ghost preparation changed row count");
        Check(prepared[0].GetRawText() == row.GetRawText(),
            "Ghost preparation must not invent or discard current-v22 row fields");
    }

    private static void GhostPreparationAllowsFrontendDelay()
    {
        JsonElement row = Row(plunderAt: 1_789_616_345_000L);
        JsonElement prepared =
            CurrentClientMap317ActionProvider.PrepareGhostPlunderRows([row]).Single();
        Check(prepared.GetProperty("plunderAt").GetInt64() == 1_789_616_345_000L,
            "Ghost preparation rewrote frontend-owned random delay");
    }

    private static void GhostPreparationMatchesExpiryBoundary()
    {
        ExpectAccepted(Row(taskExpireTimeJson: "1789623200000"), "positive expiry after plunder");
        ExpectAccepted(Row(taskExpireTimeJson: "0"), "zero expiry");
        ExpectAccepted(Row(taskExpireTimeJson: "-1"), "negative expiry");
        ExpectAccepted(Row(taskExpireTimeJson: null), "missing expiry");
        ExpectInvalid(Row(taskExpireTimeJson: "1789616300000"),
            "Ghost preparation accepted positive expiry equal to plunder");
        ExpectInvalid(Row(taskExpireTimeJson: "1789616299999"),
            "Ghost preparation accepted positive expiry before plunder");
    }

    private static void GhostPreparationPreservesExpiryReaderContract()
    {
        ExpectInvalidOperation(Row(taskExpireTimeJson: "\"1789623200000\""),
            "Ghost preparation broadened numeric-string expiry parsing");
        ExpectInvalidOperation(Row(taskExpireTimeJson: "\"not-a-number\""),
            "Ghost preparation broadened malformed-string expiry parsing");
    }

    private static void GhostPreparationRejectsMutatedCounts()
    {
        ExpectInvalid(Row(stolenCount: 2), "Ghost preparation accepted a stale stealList count");
        ExpectInvalid(Row(maxStealCount: 4), "Ghost preparation accepted a stale steal-max count");
    }

    private static void GhostPreparationRejectsEarlyOrExpiredTiming()
    {
        ExpectInvalid(Row(plunderAt: 1_789_616_299_999L),
            "Ghost preparation accepted plunder before v22 protection elapsed");
        ExpectInvalid(Row(plunderAt: 1_789_623_200_000L),
            "Ghost preparation accepted plunder at task expiry");
    }

    private static void GhostPreparationRejectsInvalidIdentity()
    {
        ExpectInvalid(Row(uuid: "ghost-123"),
            "Ghost preparation accepted non-decimal task UUID");
        ExpectInvalid(Row(ownerServer: 0),
            "Ghost preparation accepted invalid owner server");
    }

    private static JsonElement Row(
        string uuid = "123",
        int ownerServer = 33,
        long plunderAt = 1_789_616_300_000L,
        int stolenCount = 1,
        int maxStealCount = 3,
        string? taskExpireTimeJson = "1789623200000")
    {
        string expiry = taskExpireTimeJson is null ? string.Empty : $",\"taskExpireTime\":{taskExpireTimeJson}";
        string json = $$"""
        {
          "serverId":91,
          "taskKind":"ghost",
          "uuid":"{{uuid}}",
          "ownerServer":{{ownerServer}},
          "completionTime":1789616000000,
          "protectTime":300,
          "plunderAt":{{plunderAt}}{{expiry}},
          "stealListCount":1,
          "stealMaxTimes":3,
          "stolenCount":{{stolenCount}},
          "maxStealCount":{{maxStealCount}}
        }
        """;
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static void ExpectAccepted(JsonElement row, string caseName)
    {
        JsonElement prepared =
            CurrentClientMap317ActionProvider.PrepareGhostPlunderRows([row]).Single();
        Check(prepared.GetRawText() == row.GetRawText(),
            $"Ghost preparation did not preserve {caseName} row values");
    }

    private static void ExpectInvalidOperation(JsonElement row, string message)
    {
        try
        {
            _ = CurrentClientMap317ActionProvider.PrepareGhostPlunderRows([row]);
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private static void ExpectInvalid(JsonElement row, string message)
    {
        try
        {
            _ = CurrentClientMap317ActionProvider.PrepareGhostPlunderRows([row]);
        }
        catch (InvalidDataException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
