using System.Globalization;
using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed record TreasureClaimRequest(
    int ServerId,
    string ClaimScope,
    string TargetUuid,
    bool PrioritizeLuckySlots);

internal sealed record CurrentV19DirectTreasureClaimPlan(
    long TreasureUuid,
    int TargetServer,
    string Command);

internal sealed record CurrentV19DirectTreasureClaimOutcome(
    bool Succeeded,
    string? ErrorCode,
    bool HasReward);

internal static class TreasureClaimContract
{
    internal const string CurrentV19Command = "detect.event.claim.treasure";

    internal static TreasureClaimRequest NormalizeRequest(JsonElement payload)
    {
        if (!payload.TryGetProperty("serverId", out JsonElement serverValue) ||
            serverValue.ValueKind != JsonValueKind.Number ||
            !serverValue.TryGetInt32(out int serverId) ||
            serverId is < 1 or > 99_999)
        {
            throw new BridgeCommandException(
                "INVALID_SERVER_ID",
                "server ID must be an integer from 1 to 99999");
        }

        string claimScope =
            payload.TryGetProperty("claimScope", out JsonElement scopeValue) &&
            scopeValue.ValueKind == JsonValueKind.String
                ? scopeValue.GetString() ?? string.Empty
                : string.Empty;
        string targetUuid =
            payload.TryGetProperty("targetUuid", out JsonElement uuidValue) &&
            uuidValue.ValueKind == JsonValueKind.String
                ? uuidValue.GetString() ?? string.Empty
                : string.Empty;

        if (claimScope is not ("boxes" or "season" or "single") ||
            (claimScope == "single" && string.IsNullOrEmpty(targetUuid)))
        {
            throw new BridgeCommandException(
                "INVALID_TREASURE_CLAIM_SCOPE",
                "treasure claim scope is invalid");
        }

        bool prioritizeLuckySlots =
            !payload.TryGetProperty(
                "prioritizeLuckySlots",
                out JsonElement priorityValue) ||
            priorityValue.ValueKind != JsonValueKind.False;

        return new TreasureClaimRequest(
            serverId,
            claimScope,
            targetUuid,
            prioritizeLuckySlots);
    }

    internal static CurrentV19DirectTreasureClaimPlan BuildCurrentV19DirectPlan(
        int targetServer,
        string treasureUuid)
    {
        if (targetServer is < 1 or > 99_999)
        {
            throw new InvalidOperationException(
                "current-v19 Treasure targetServer is outside the proven PutInt server domain.");
        }

        if (!long.TryParse(
                treasureUuid,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out long uuid) ||
            uuid <= 0)
        {
            throw new InvalidOperationException(
                "current-v19 Treasure uuid must be a positive signed Int64 for PutLong.");
        }

        return new CurrentV19DirectTreasureClaimPlan(
            uuid,
            targetServer,
            CurrentV19Command);
    }

    internal static CurrentV19DirectTreasureClaimOutcome ClassifyCurrentV19DirectResponse(
        JsonElement message)
    {
        if (message.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Treasure claim response must be an object.");

        if (message.TryGetProperty("errorCode", out JsonElement errorCode) &&
            errorCode.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
        {
            string value = errorCode.ValueKind == JsonValueKind.String
                ? errorCode.GetString() ?? string.Empty
                : errorCode.GetRawText();
            return new CurrentV19DirectTreasureClaimOutcome(
                false,
                value,
                false);
        }

        bool hasReward =
            message.TryGetProperty("reward", out JsonElement reward) &&
            reward.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined;
        return new CurrentV19DirectTreasureClaimOutcome(
            true,
            null,
            hasReward);
    }
}
