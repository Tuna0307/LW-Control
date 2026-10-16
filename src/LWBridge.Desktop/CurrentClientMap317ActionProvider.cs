using System.Globalization;
using System.Text.Json;
using Map317 = LWBridge.Map317;

namespace LWBridge.Desktop;

/// <summary>
/// Production current-client adapter for the exact 0.3.17 Map action plane.
/// Only source-backed current-client actions are implemented here; unrecovered
/// protected actions fail explicitly instead of fabricating game behavior.
/// </summary>
internal sealed class CurrentClientMap317ActionProvider : Map317.IMapActionProvider, Map317.IGhostPlunderProvider
{
    private readonly CurrentClientMapBlockSource source;
    internal CurrentClientMap317ActionProvider(CurrentClientMapBlockSource source)
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
    }

    public async ValueTask GotoWorldCoordinateAsync(
        int serverId, int x, int y, CancellationToken cancellationToken = default)
    {
        await TranslateAsync(() => source.JumpToCoordinateAsync(serverId, x, y, cancellationToken))
            .ConfigureAwait(false);
    }

    public async ValueTask GotoWorldMarchAsync(
        int serverId, string marchUuid, CancellationToken cancellationToken = default)
    {
        if (!long.TryParse(marchUuid, NumberStyles.None, CultureInfo.InvariantCulture, out long parsed) || parsed <= 0)
            throw new Map317.BridgeCommandException("INVALID_MARCH", "server ID and march UUID are required");
        await TranslateAsync(() => source.FollowMarchAsync(serverId, parsed, cancellationToken)).ConfigureAwait(false);
    }

    public async ValueTask<int> GetCurrentServerIdAsync(CancellationToken cancellationToken = default)
    {
        CurrentClientMapStatusContext context = await TranslateAsync(
            () => source.GetMapStatusContextAsync(cancellationToken)).ConfigureAwait(false);
        if (context.ServerId <= 0)
            throw ProviderUnavailable("current server id unavailable");
        return context.ServerId;
    }

    public async ValueTask GotoServerAsync(int serverId, CancellationToken cancellationToken = default)
    {
        await TranslateAsync(() => source.JumpToServerAsync(serverId, cancellationToken)).ConfigureAwait(false);
    }

    public async ValueTask<Map317.TreasureInspectionResult> InspectTreasureStatesAsync(
        IReadOnlyList<JsonElement> records,
        bool refresh,
        CancellationToken cancellationToken = default)
    {
        if (records.Count == 0)
            return new Map317.TreasureInspectionResult(string.Empty, string.Empty, Array.Empty<JsonElement>());

        int serverId = checked((int)ReadInteger(records[0], "serverId"));
        var normalized = records.Select(row => ToTreasureRecord(serverId, row)).ToArray();
        var states = new List<JsonElement>(normalized.Length);
        string playerUid = string.Empty;
        string allianceId = string.Empty;
        foreach (CurrentClientTreasureInspectionRecord[] batch in normalized.Chunk(100))
        {
            CurrentClientTreasureInspectionResult result = await TranslateAsync(
                () => source.InspectTreasureStatesAsync(serverId, batch, refresh, cancellationToken))
                .ConfigureAwait(false);
            if (playerUid.Length == 0)
            {
                playerUid = result.PlayerUid;
                allianceId = result.AllianceId;
            }
            else if (!string.Equals(playerUid, result.PlayerUid, StringComparison.Ordinal) ||
                     !string.Equals(allianceId, result.AllianceId, StringComparison.Ordinal))
            {
                throw new Map317.BridgeCommandException(
                    "GAME_PROVIDER_ERROR", "treasure inspection identity changed during batching");
            }
            states.AddRange(result.States.Select(item => item.Clone()));
        }
        return new Map317.TreasureInspectionResult(playerUid, allianceId, states);
    }

    public ValueTask<JsonElement> GetTreasureClaimStatusAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromException<JsonElement>(ProviderUnavailable("current-client Treasure claim status provider is unavailable"));

    public ValueTask<JsonElement> ClaimTreasuresAsync(
        Map317.TreasureClaimProviderRequest request,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromException<JsonElement>(ProviderUnavailable("current-client Treasure claim provider is unavailable"));

    // Compatibility signatures remain on IMapActionProvider because historic
    // consumers and the upgrade probe compile against this interface. Every
    // retired operation rejects before reading game state or sending a command.
    private static Map317.BridgeCommandException Retired() =>
        new("FEATURE_REMOVED", "Automatic plunder, scheduling and alliance sharing were removed by the owner.");

    public ValueTask<Map317.DispatchShareProviderResult> ShareDispatchTaskToAllianceAsync(
        JsonElement row, CancellationToken cancellationToken = default) =>
        ValueTask.FromException<Map317.DispatchShareProviderResult>(Retired());

    public ValueTask<IReadOnlyList<JsonElement>> PrepareGhostPlunderTasksAsync(
        IReadOnlyList<JsonElement> rows, CancellationToken cancellationToken = default) =>
        ValueTask.FromException<IReadOnlyList<JsonElement>>(Retired());

    public ValueTask<Map317.MapPlunderServerDayProviderResult?> GetMapPlunderServerDayStartAsync(
        CancellationToken cancellationToken = default) =>
        ValueTask.FromException<Map317.MapPlunderServerDayProviderResult?>(Retired());

    public ValueTask<IReadOnlyList<Map317.DispatchPlunderArmResult>> ArmDispatchPlunderAsync(
        IReadOnlyList<Map317.DispatchPlunderArmJob> jobs, CancellationToken cancellationToken = default) =>
        ValueTask.FromException<IReadOnlyList<Map317.DispatchPlunderArmResult>>(Retired());

    public ValueTask<IReadOnlyList<Map317.DispatchPlunderArmResult>> ArmGhostPlunderAsync(
        IReadOnlyList<Map317.DispatchPlunderArmJob> jobs, CancellationToken cancellationToken = default) =>
        ValueTask.FromException<IReadOnlyList<Map317.DispatchPlunderArmResult>>(Retired());

    public ValueTask<Map317.TruckPlunderArmResult> ArmTruckPlunderAsync(
        Map317.TruckPlunderArmJob job, CancellationToken cancellationToken = default) =>
        ValueTask.FromException<Map317.TruckPlunderArmResult>(Retired());

    public ValueTask<IReadOnlyList<Map317.DispatchPlunderResultEvent>> DrainDispatchPlunderResultsAsync(
        CancellationToken cancellationToken = default) =>
        ValueTask.FromException<IReadOnlyList<Map317.DispatchPlunderResultEvent>>(Retired());

    public ValueTask<IReadOnlyList<Map317.DispatchPlunderResultEvent>> DrainGhostPlunderResultsAsync(
        CancellationToken cancellationToken = default) =>
        ValueTask.FromException<IReadOnlyList<Map317.DispatchPlunderResultEvent>>(Retired());

    public ValueTask<IReadOnlyList<Map317.TruckPlunderResultEvent>> DrainTruckPlunderResultsAsync(
        CancellationToken cancellationToken = default) =>
        ValueTask.FromException<IReadOnlyList<Map317.TruckPlunderResultEvent>>(Retired());

    public ValueTask ClearTruckPlunderPendingAsync(
        int serverId, string trainUuid, string jobId,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromException(Retired());


    private static CurrentClientTreasureInspectionRecord ToTreasureRecord(int expectedServerId, JsonElement row)
    {
        int serverId = checked((int)ReadInteger(row, "serverId"));
        if (serverId != expectedServerId)
            throw new Map317.BridgeCommandException("INVALID_REQUEST", "Treasure inspection spans multiple servers");
        return new CurrentClientTreasureInspectionRecord(
            serverId,
            checked((int)ReadInteger(row, "pointIndex")),
            ReadText(row, "uuid"),
            checked((int)ReadInteger(row, "treasureType")),
            checked((int)ReadInteger(row, "suppliesType")),
            ReadText(row, "allianceId"),
            ReadText(row, "viewerUid"),
            ReadText(row, "viewerAllianceId"),
            ReadNullableBoolean(row, "viewerHasReward"),
            ReadNullableBoolean(row, "viewerIsWorking"),
            ReadNullableBoolean(row, "complete"),
            ReadNullableInteger(row, "expireTime"),
            ReadNullableInteger(row, "startTime"),
            ReadNullableInteger(row, "completionTime"),
            ReadNullableInt(row, "rewardedCount"),
            ReadNullableInt(row, "diggingCount"),
            ReadNullableInt(row, "rewardMax"),
            ReadNullableInt(row, "remainingBoxes"),
            ReadNullableInteger(row, "createTime"),
            ReadText(row, "discovererAllianceId"),
            ReadText(row, "discovererUid"),
            ReadNullableInt(row, "workState"),
            ReadNullableInt(row, "userCount"));
    }

    private static async Task<T> TranslateAsync<T>(Func<Task<T>> action)
    {
        try { return await action().ConfigureAwait(false); }
        catch (BridgeCommandException error)
        {
            throw new Map317.BridgeCommandException(error.Code, error.Message, error.Details);
        }
        catch (TimeoutException error)
        {
            throw new Map317.BridgeCommandException("GAME_CONNECTION_UNAVAILABLE", error.Message);
        }
        catch (InvalidDataException error)
        {
            throw new Map317.BridgeCommandException("GAME_PROVIDER_ERROR", error.Message);
        }
    }

    private static async Task TranslateAsync(Func<Task> action)
    {
        try { await action().ConfigureAwait(false); }
        catch (BridgeCommandException error)
        {
            throw new Map317.BridgeCommandException(error.Code, error.Message, error.Details);
        }
        catch (TimeoutException error)
        {
            throw new Map317.BridgeCommandException("GAME_CONNECTION_UNAVAILABLE", error.Message);
        }
        catch (InvalidDataException error)
        {
            throw new Map317.BridgeCommandException("GAME_PROVIDER_ERROR", error.Message);
        }
    }

    private static Map317.BridgeCommandException ProviderUnavailable(string message) =>
        new("GAME_PROVIDER_UNAVAILABLE", message);

    private static string ReadText(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out JsonElement value)) return string.Empty;
        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString();
    }

    private static long ReadInteger(JsonElement row, string name) => ReadNullableInteger(row, name) ?? 0;

    private static long? ReadNullableInteger(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out JsonElement value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        if (value.TryGetInt64(out long integer)) return integer;
        return value.ValueKind == JsonValueKind.String &&
               long.TryParse(value.GetString(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long parsed)
            ? parsed
            : null;
    }

    private static int? ReadNullableInt(JsonElement row, string name)
    {
        long? value = ReadNullableInteger(row, name);
        return value is >= int.MinValue and <= int.MaxValue ? (int)value.Value : null;
    }

    private static bool? ReadNullableBoolean(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out JsonElement value)) return null;
        return value.ValueKind == JsonValueKind.True ? true : value.ValueKind == JsonValueKind.False ? false : null;
    }
}
