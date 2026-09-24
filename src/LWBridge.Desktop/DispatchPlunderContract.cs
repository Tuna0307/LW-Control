using System.Globalization;
using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed record DispatchPlunderScheduleRow(
    long ServerId,
    string Uuid,
    string Json,
    long CompletionTime,
    long PlunderAt,
    long? ExpireAt);

internal sealed record DispatchPlunderTarget(
    long ServerId,
    string TaskUuid);

internal static class DispatchPlunderContract
{
    internal static IReadOnlyList<DispatchPlunderScheduleRow> NormalizeScheduleRows(
        JsonElement payload)
    {
        if (!payload.TryGetProperty("rows", out JsonElement rows) ||
            rows.ValueKind != JsonValueKind.Array)
        {
            throw new BridgeCommandException(
                "INVALID_REQUEST",
                "secret task rows are required");
        }

        int count = rows.GetArrayLength();
        if (count is < 1 or > 200)
        {
            throw new BridgeCommandException(
                "INVALID_REQUEST",
                "select between 1 and 200 secret tasks");
        }

        var normalized = new List<DispatchPlunderScheduleRow>(count);
        foreach (JsonElement row in rows.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object)
                throw InvalidSchedule();

            long serverId = ReadRecoveredIntegerLike(row, "serverId");
            string? uuid =
                row.TryGetProperty("uuid", out JsonElement uuidValue) &&
                uuidValue.ValueKind == JsonValueKind.String
                    ? uuidValue.GetString()
                    : null;
            long completionTime =
                ReadRecoveredIntegerLike(row, "completionTime");
            long plunderAt =
                ReadRecoveredIntegerLike(row, "plunderAt");
            long taskExpireTime =
                ReadRecoveredIntegerLike(row, "taskExpireTime");
            long stolenCount =
                ReadRecoveredIntegerLike(row, "stolenCount");
            long maxStealCount =
                ReadRecoveredIntegerLike(row, "maxStealCount");

            bool decimalUuid =
                !string.IsNullOrEmpty(uuid) &&
                uuid.All(ch => ch is >= '0' and <= '9');
            if (serverId <= 0 ||
                !decimalUuid ||
                completionTime <= 0 ||
                plunderAt < completionTime ||
                (taskExpireTime > 0 && taskExpireTime <= plunderAt) ||
                (maxStealCount > 0 && stolenCount >= maxStealCount))
            {
                throw InvalidSchedule();
            }

            normalized.Add(new DispatchPlunderScheduleRow(
                serverId,
                uuid!,
                row.GetRawText(),
                completionTime,
                plunderAt,
                taskExpireTime > 0 ? taskExpireTime : null));
        }

        return normalized;
    }

    internal static DispatchPlunderTarget NormalizeCancel(JsonElement payload)
    {
        if (!payload.TryGetProperty("serverId", out JsonElement serverValue) ||
            !serverValue.TryGetInt64(out long serverId) ||
            serverId <= 0 ||
            !payload.TryGetProperty("taskUuid", out JsonElement uuidValue) ||
            uuidValue.ValueKind != JsonValueKind.String)
        {
            throw InvalidTarget();
        }

        string? taskUuid = uuidValue.GetString();
        if (string.IsNullOrEmpty(taskUuid) ||
            taskUuid.Any(ch => ch is < '0' or > '9'))
        {
            throw InvalidTarget();
        }

        return new DispatchPlunderTarget(serverId, taskUuid);
    }

    private static BridgeCommandException InvalidSchedule() =>
        new("INVALID_REQUEST", "secret task scheduling data is invalid");

    private static BridgeCommandException InvalidTarget() =>
        new("INVALID_REQUEST", "server ID and secret task UUID are required");

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
