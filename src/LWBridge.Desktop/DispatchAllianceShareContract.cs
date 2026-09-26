using System.Globalization;
using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed record DispatchAllianceShareRow(
    long ServerId,
    string Uuid,
    long X,
    long Y,
    long CfgId,
    string? OwnerName,
    string? AllianceAbbr,
    string Json);

internal sealed record CurrentV19DispatchAllianceSharePlan(
    DispatchAllianceShareRow Row,
    int TargetServer,
    long TaskUuid,
    string PostType,
    string ShareChannel,
    string Command,
    JsonElement PointShareParam);

internal static class DispatchAllianceShareContract
{
    internal const string CurrentPostType = "Text_PointShare";
    internal const string CurrentShareChannel = "TO_ALLIANCE";
    internal const string CurrentCommand = "hero.dispatch.share.chat";
    internal const string CurrentDispatchLabelKey = "456288";

    internal static IReadOnlyList<DispatchAllianceShareRow> NormalizeRows(
        JsonElement payload)
    {
        if (!payload.TryGetProperty("rows", out JsonElement rows) ||
            rows.ValueKind != JsonValueKind.Array)
        {
            throw InvalidCount();
        }

        int count = rows.GetArrayLength();
        if (count is < 1 or > 200)
            throw InvalidCount();

        var normalized = new List<DispatchAllianceShareRow>(count);
        foreach (JsonElement row in rows.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object)
                throw InvalidRow();

            string? uuid =
                row.TryGetProperty("uuid", out JsonElement uuidValue) &&
                uuidValue.ValueKind == JsonValueKind.String
                    ? uuidValue.GetString()
                    : null;
            long serverId = ReadRecoveredIntegerLike(row, "serverId");
            long x = ReadRecoveredIntegerLike(row, "x");
            long y = ReadRecoveredIntegerLike(row, "y");
            long cfgId = ReadRecoveredIntegerLike(row, "cfgId");

            bool decimalUuid =
                !string.IsNullOrEmpty(uuid) &&
                uuid.All(ch => ch is >= '0' and <= '9');
            if (!decimalUuid ||
                serverId <= 0 ||
                x <= 0 ||
                y <= 0 ||
                cfgId <= 0)
            {
                throw InvalidRow();
            }

            normalized.Add(new DispatchAllianceShareRow(
                serverId,
                uuid!,
                x,
                y,
                cfgId,
                ReadOptionalString(row, "ownerName"),
                ReadOptionalString(row, "allianceAbbr"),
                row.GetRawText()));
        }

        return normalized;
    }

    internal static CurrentV19DispatchAllianceSharePlan BuildCurrentV19Plan(
        DispatchAllianceShareRow row)
    {
        if (row.ServerId > int.MaxValue)
        {
            throw new InvalidOperationException(
                "current-v19 ChatHeroDispatchShareCommand targetServer uses PutInt");
        }

        if (!long.TryParse(
                row.Uuid,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out long taskUuid))
        {
            throw new InvalidOperationException(
                "current-v19 ChatHeroDispatchShareCommand uuid uses PutLong");
        }

        var point = new Dictionary<string, object?>
        {
            ["x"] = row.X,
            ["y"] = row.Y,
            ["sid"] = row.ServerId,
            ["dispatch"] = 1,
            ["cfgId"] = row.CfgId,
            ["uuid"] = row.Uuid,
        };

        if (!string.IsNullOrEmpty(row.OwnerName))
        {
            point["uname"] = row.OwnerName;
            if (row.AllianceAbbr is not null)
                point["abbr"] = row.AllianceAbbr;
        }

        return new CurrentV19DispatchAllianceSharePlan(
            row,
            checked((int)row.ServerId),
            taskUuid,
            CurrentPostType,
            CurrentShareChannel,
            CurrentCommand,
            JsonSerializer.SerializeToElement(point, JsonOptions.Default));
    }

    private static string? ReadOptionalString(
        JsonElement row,
        string name)
    {
        if (!row.TryGetProperty(name, out JsonElement value) ||
            value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return value.GetString();
    }

    private static BridgeCommandException InvalidCount() =>
        new("INVALID_REQUEST", "select between 1 and 200 dispatch tasks");

    private static BridgeCommandException InvalidRow() =>
        new("INVALID_REQUEST", "selected dispatch task cannot be shared");

    private static long ReadRecoveredIntegerLike(
        JsonElement row,
        string name)
    {
        if (!row.TryGetProperty(name, out JsonElement value))
            return 0;

        if (value.ValueKind == JsonValueKind.Number)
        {
            if (value.TryGetInt64(out long integer))
                return integer;
            if (value.TryGetDouble(out double floating) &&
                double.IsFinite(floating))
            {
                if (floating >= long.MaxValue)
                    return long.MaxValue;
                if (floating <= long.MinValue)
                    return long.MinValue;
                return (long)floating;
            }
            return 0;
        }

        if (value.ValueKind == JsonValueKind.String &&
            long.TryParse(
                value.GetString(),
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out long parsed))
        {
            return parsed;
        }

        return 0;
    }
}
