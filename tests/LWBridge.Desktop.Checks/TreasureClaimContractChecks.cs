using System.Runtime.CompilerServices;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class TreasureClaimContractChecks
{
    [ModuleInitializer]
    internal static void Run()
    {
        RequestValidationMatchesRecoveredHost();
        CandidateQueryMatchesRecoveredStore();
        CurrentV19DirectTransportIsSourceBacked();
        OriginalFrontendClaimStatusContractIsPinned();
        ProductionClaimRouteRemainsBlocked();
    }

    private static void RequestValidationMatchesRecoveredHost()
    {
        TreasureClaimRequest boxes = TreasureClaimContract.NormalizeRequest(
            JsonSerializer.SerializeToElement(
                new { serverId = 2212, claimScope = "boxes" },
                JsonOptions.Default));
        Check(
            boxes.ServerId == 2212 &&
            boxes.ClaimScope == "boxes" &&
            boxes.TargetUuid == string.Empty &&
            boxes.PrioritizeLuckySlots,
            "Treasure boxes request preserves recovered scope and default lucky priority");

        TreasureClaimRequest season = TreasureClaimContract.NormalizeRequest(
            JsonSerializer.SerializeToElement(
                new
                {
                    serverId = 2212,
                    claimScope = "season",
                    prioritizeLuckySlots = false,
                },
                JsonOptions.Default));
        Check(
            !season.PrioritizeLuckySlots,
            "Treasure season request preserves explicit false lucky priority");

        TreasureClaimRequest single = TreasureClaimContract.NormalizeRequest(
            JsonSerializer.SerializeToElement(
                new
                {
                    serverId = 2212,
                    claimScope = "single",
                    targetUuid = "1417409824803038247",
                },
                JsonOptions.Default));
        Check(
            single.TargetUuid == "1417409824803038247",
            "Treasure single request requires and preserves targetUuid");

        ExpectError(
            new { serverId = 0, claimScope = "boxes" },
            "INVALID_SERVER_ID",
            "server ID must be an integer from 1 to 99999");
        ExpectError(
            new { serverId = 100_000, claimScope = "boxes" },
            "INVALID_SERVER_ID",
            "server ID must be an integer from 1 to 99999");
        ExpectError(
            new { serverId = 2212, claimScope = "other" },
            "INVALID_TREASURE_CLAIM_SCOPE",
            "treasure claim scope is invalid");
        ExpectError(
            new { serverId = 2212, claimScope = "single", targetUuid = "" },
            "INVALID_TREASURE_CLAIM_SCOPE",
            "treasure claim scope is invalid");
    }

    private static void CandidateQueryMatchesRecoveredStore()
    {
        using MapDataStore store = MapDataStore.CreateInMemory();
        Add(store, 9, "s1", 1, 0, 0);
        Add(store, 3, "s3", 3, 0, 2_000);
        Add(store, 6, "s4", 4, 0, 0);
        Add(store, 1, "box", 0, 1, 0);
        Add(store, 2, "incomplete", 0, 0, 0);
        Add(store, 4, "wrong-supplies", 2, 1, 0);
        Add(store, 5, "expired", 0, 1, 1_000);
        Add(store, 7, "0", 0, 1, 0);

        IReadOnlyList<string> rows =
            store.ReadTreasureClaimCandidateJson(2212, 1_500);
        string[] uuids = rows
            .Select(json => JsonDocument.Parse(json).RootElement
                .GetProperty("uuid").GetString() ?? string.Empty)
            .ToArray();

        Check(
            uuids.SequenceEqual(new[] { "box", "s3", "s4", "s1" }),
            "Treasure candidate SQL preserves supplies 1/3/4 or completed ordinary, nonexpired/nonzero UUID, point-index ordering");
    }

    private static void CurrentV19DirectTransportIsSourceBacked()
    {
        CurrentV19DirectTreasureClaimPlan plan =
            TreasureClaimContract.BuildCurrentV19DirectPlan(
                2212,
                "1417409824803038247");
        Check(
            plan.TargetServer == 2212 &&
            plan.TreasureUuid == 1_417_409_824_803_038_247L &&
            plan.Command == "detect.event.claim.treasure",
            "current-v19 direct Treasure plan pins PutLong uuid, PutInt targetServer and official command");

        CurrentV19DirectTreasureClaimOutcome success =
            TreasureClaimContract.ClassifyCurrentV19DirectResponse(
                JsonSerializer.SerializeToElement(
                    new { reward = new[] { new { id = 1 } } },
                    JsonOptions.Default));
        Check(
            success.Succeeded && success.HasReward && success.ErrorCode is null,
            "Treasure direct response without errorCode is authoritative success even before local cache refresh");

        CurrentV19DirectTreasureClaimOutcome noRewardSuccess =
            TreasureClaimContract.ClassifyCurrentV19DirectResponse(
                JsonSerializer.SerializeToElement(new { ok = true }, JsonOptions.Default));
        Check(
            noRewardSuccess.Succeeded && !noRewardSuccess.HasReward,
            "Treasure direct success does not require a reward field");

        CurrentV19DirectTreasureClaimOutcome rejected =
            TreasureClaimContract.ClassifyCurrentV19DirectResponse(
                JsonSerializer.SerializeToElement(
                    new { errorCode = 12345 },
                    JsonOptions.Default));
        Check(
            !rejected.Succeeded &&
            rejected.ErrorCode == "12345" &&
            !rejected.HasReward,
            "Treasure direct response with errorCode is server rejection");

        try
        {
            _ = TreasureClaimContract.BuildCurrentV19DirectPlan(
                2212,
                "9223372036854775808");
            throw new InvalidOperationException(
                "expected Treasure PutLong transport bound failure");
        }
        catch (InvalidOperationException error)
            when (error.Message.Contains("PutLong", StringComparison.Ordinal))
        {
        }
    }

    private static void OriginalFrontendClaimStatusContractIsPinned()
    {
        Check(
            TreasureClaimContract.OriginalFrontendStatusPollIntervalMilliseconds == 1_000 &&
            TreasureClaimContract.OriginalFrontendStatusPollLimit == 1_800,
            "original Treasure frontend polls claim status once per second for at most 1800 polls");
        Check(
            TreasureClaimContract.OriginalFrontendLuckyPriorityEnabled(null) &&
            TreasureClaimContract.OriginalFrontendLuckyPriorityEnabled("true") &&
            !TreasureClaimContract.OriginalFrontendLuckyPriorityEnabled("false"),
            "original Treasure frontend lucky priority defaults on unless local storage is exactly false");
        Check(
            !TreasureClaimContract.OriginalFrontendShouldPollStatus(0) &&
            TreasureClaimContract.OriginalFrontendShouldPollStatus(1),
            "original Treasure frontend polls only when the immediate claim result queued at least one row");
        Check(
            !TreasureClaimContract.OriginalFrontendBatchIsTerminal(false, null) &&
            !TreasureClaimContract.OriginalFrontendBatchIsTerminal(true, "running") &&
            TreasureClaimContract.OriginalFrontendBatchIsTerminal(true, "completed") &&
            TreasureClaimContract.OriginalFrontendBatchIsTerminal(true, null),
            "original Treasure frontend treats only an existing running batch as nonterminal");

        var ordinary = new TreasureClaimFrontendRow(
            "1417409824803038247", 0, true, "unclaimed", "claimable", string.Empty);
        Check(
            TreasureClaimContract.OriginalFrontendCanClaimRow(ordinary, false),
            "completed ordinary Treasure row is frontend-claimable");
        Check(
            TreasureClaimContract.OriginalFrontendCanClaimRow(
                ordinary with { PlayerClaimState = "failed" }, false) &&
            TreasureClaimContract.OriginalFrontendCanClaimRow(
                ordinary with { PlayerClaimState = "verifying" }, false) &&
            TreasureClaimContract.OriginalFrontendCanClaimRow(
                ordinary with { ClaimBlockReason = "no_scout" }, false),
            "original row button does not itself disable failed/verifying or no-scout rows; protected executor eligibility remains separate");
        Check(
            TreasureClaimContract.OriginalFrontendCanClaimRow(
                ordinary with { SuppliesType = 3, Complete = false }, false),
            "supported Supplies types bypass the ordinary complete requirement in the frontend");
        Check(
            !TreasureClaimContract.OriginalFrontendCanClaimRow(
                ordinary with { Complete = false }, false) &&
            !TreasureClaimContract.OriginalFrontendCanClaimRow(
                ordinary with { Uuid = "   " }, false) &&
            !TreasureClaimContract.OriginalFrontendCanClaimRow(
                ordinary with { Uuid = " 0 " }, false) &&
            !TreasureClaimContract.OriginalFrontendCanClaimRow(
                ordinary with { PlayerClaimState = "scouting" }, false) &&
            !TreasureClaimContract.OriginalFrontendCanClaimRow(
                ordinary with { WorldClaimState = "expired" }, false) &&
            !TreasureClaimContract.OriginalFrontendCanClaimRow(
                ordinary with { ClaimBlockReason = "other_alliance" }, false) &&
            !TreasureClaimContract.OriginalFrontendCanClaimRow(ordinary, true),
            "original frontend single-claim button trims UUID and blocks incomplete ordinary, active, expired, foreign-alliance and global-disabled rows");
    }

    private static void ProductionClaimRouteRemainsBlocked()
    {
        string source = File.ReadAllText(
            Path.Combine(
                FindRepoRoot(),
                "src",
                "LWBridge.Desktop",
                "ManualMapScanCommandService.cs"));
        Check(
            !source.Contains(
                "\"map_treasure_claim\" or",
                StringComparison.Ordinal) &&
            !source.Contains(
                "command == \"map_treasure_claim\"",
                StringComparison.Ordinal),
            "state-changing Treasure claim route remains absent while hidden claimTreasures orchestration is unrecovered");
    }

    private static void Add(
        MapDataStore store,
        int pointIndex,
        string uuid,
        int suppliesType,
        int complete,
        long expireTime)
    {
        string json = JsonSerializer.Serialize(
            new
            {
                uuid,
                suppliesType,
                complete,
                expireTime,
            },
            JsonOptions.Default);
        store.UpsertRecord(new MapStoredRecord(
            "treasure",
            2212,
            "treasure:" + pointIndex,
            pointIndex,
            uuid,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            1,
            json));
    }

    private static void ExpectError(
        object payload,
        string code,
        string message)
    {
        try
        {
            _ = TreasureClaimContract.NormalizeRequest(
                JsonSerializer.SerializeToElement(payload, JsonOptions.Default));
            throw new InvalidOperationException(
                "expected Treasure claim validation failure");
        }
        catch (BridgeCommandException error)
            when (error.Code == code && error.Message == message)
        {
        }
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
                return current.FullName;
            current = current.Parent;
        }
        throw new InvalidOperationException("repository root not found");
    }

    private static void Check(bool condition, string name)
    {
        if (!condition)
            throw new InvalidOperationException(name);
    }
}
