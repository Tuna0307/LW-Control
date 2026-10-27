using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed record TreasureClaimRequest(
    int ServerId,
    string ClaimScope,
    string TargetUuid,
    bool PrioritizeLuckySlots);

internal static class TreasureClaimContract
{
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
}
