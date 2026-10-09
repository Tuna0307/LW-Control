using System.Text.Json;

namespace LWBridge.Map317;

public sealed record ServerJumpProviderResult(bool Changed, int PreviousServerId, int ServerId);

public sealed record TreasureInspectionResult(
    string PlayerUid,
    string AllianceId,
    IReadOnlyList<JsonElement> States);

public sealed record TreasureClaimProviderRequest(
    int ServerId,
    IReadOnlyList<JsonElement> Records,
    string ClaimScope,
    string TargetUuid,
    bool PrioritizeLuckySlots);

public sealed record DispatchShareProviderResult(bool Shared);

public sealed record DispatchPlunderArmJob(
    string Kind,
    int ServerId,
    int OwnerServer,
    string TaskUuid,
    long ExecuteAt);

public sealed record DispatchPlunderArmResult(
    string Kind,
    int ServerId,
    string TaskUuid,
    bool Armed,
    string? ErrorCode = null);

public sealed record DispatchPlunderResultEvent(
    string Kind,
    int ServerId,
    string TaskUuid,
    bool Success,
    string? ErrorCode = null,
    long? ServerDayStartAt = null,
    int? SendOffsetMs = null,
    int? LeadMs = null,
    int? OwnerServer = null);

public sealed record TruckPlunderArmJob(
    int ServerId,
    string TrainUuid,
    string JobId,
    long ExecuteAt,
    int? RobTimes,
    int? MaxLootCount);

public sealed record TruckPlunderArmResult(bool Armed, string? ErrorCode = null);

public sealed record TruckPlunderResultEvent(
    int ServerId,
    string TrainUuid,
    string JobId,
    bool Success,
    string? ErrorCode = null,
    bool? BattleWon = null,
    JsonElement? PlunderRewards = null,
    int? RobTimes = null,
    int? RemainingLootCount = null,
    int? DailyRobCount = null,
    long? ServerDayStartAt = null);

public sealed record MapPlunderServerDayProviderResult(long ServerTime, long ServerDayStartAt);

/// <summary>
/// Game-side Map action boundary. Exact 0.3.17 public validation/persistence is
/// local; the protected/current-client calls stay behind these high-level names.
/// </summary>
public interface IMapActionProvider
{
    ValueTask GotoWorldCoordinateAsync(int serverId, int x, int y, CancellationToken cancellationToken = default);
    ValueTask GotoWorldMarchAsync(int serverId, string marchUuid, CancellationToken cancellationToken = default);
    ValueTask<int> GetCurrentServerIdAsync(CancellationToken cancellationToken = default);
    ValueTask GotoServerAsync(int serverId, CancellationToken cancellationToken = default);
    ValueTask<TreasureInspectionResult> InspectTreasureStatesAsync(
        IReadOnlyList<JsonElement> records, bool refresh, CancellationToken cancellationToken = default);
    ValueTask<JsonElement> GetTreasureClaimStatusAsync(CancellationToken cancellationToken = default);
    ValueTask<JsonElement> ClaimTreasuresAsync(TreasureClaimProviderRequest request, CancellationToken cancellationToken = default);
    ValueTask<DispatchShareProviderResult> ShareDispatchTaskToAllianceAsync(JsonElement row, CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<JsonElement>> PrepareGhostPlunderTasksAsync(
        IReadOnlyList<JsonElement> rows, CancellationToken cancellationToken = default);
    ValueTask<MapPlunderServerDayProviderResult?> GetMapPlunderServerDayStartAsync(CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<DispatchPlunderArmResult>> ArmDispatchPlunderAsync(
        IReadOnlyList<DispatchPlunderArmJob> jobs, CancellationToken cancellationToken = default);
    ValueTask<TruckPlunderArmResult> ArmTruckPlunderAsync(
        TruckPlunderArmJob job, CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<DispatchPlunderResultEvent>> DrainDispatchPlunderResultsAsync(
        CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<TruckPlunderResultEvent>> DrainTruckPlunderResultsAsync(
        CancellationToken cancellationToken = default);
    ValueTask ClearTruckPlunderPendingAsync(
        int serverId, string trainUuid, string jobId, CancellationToken cancellationToken = default);
}

public sealed class MapActionProviderAdapter : IMapActionProvider
{
    public required Func<int, int, int, CancellationToken, ValueTask> GotoWorldCoordinate { get; init; }
    public required Func<int, string, CancellationToken, ValueTask> GotoWorldMarch { get; init; }
    public required Func<CancellationToken, ValueTask<int>> GetCurrentServerId { get; init; }
    public required Func<int, CancellationToken, ValueTask> GotoServer { get; init; }
    public required Func<IReadOnlyList<JsonElement>, bool, CancellationToken, ValueTask<TreasureInspectionResult>> InspectTreasureStates { get; init; }
    public required Func<CancellationToken, ValueTask<JsonElement>> GetTreasureClaimStatus { get; init; }
    public required Func<TreasureClaimProviderRequest, CancellationToken, ValueTask<JsonElement>> ClaimTreasures { get; init; }
    public required Func<JsonElement, CancellationToken, ValueTask<DispatchShareProviderResult>> ShareDispatchTaskToAlliance { get; init; }
    public required Func<IReadOnlyList<JsonElement>, CancellationToken, ValueTask<IReadOnlyList<JsonElement>>> PrepareGhostPlunderTasks { get; init; }
    public required Func<CancellationToken, ValueTask<MapPlunderServerDayProviderResult?>> GetMapPlunderServerDayStart { get; init; }
    public required Func<IReadOnlyList<DispatchPlunderArmJob>, CancellationToken, ValueTask<IReadOnlyList<DispatchPlunderArmResult>>> ArmDispatchPlunder { get; init; }
    public required Func<TruckPlunderArmJob, CancellationToken, ValueTask<TruckPlunderArmResult>> ArmTruckPlunder { get; init; }
    public required Func<CancellationToken, ValueTask<IReadOnlyList<DispatchPlunderResultEvent>>> DrainDispatchPlunderResults { get; init; }
    public required Func<CancellationToken, ValueTask<IReadOnlyList<TruckPlunderResultEvent>>> DrainTruckPlunderResults { get; init; }
    public required Func<int, string, string, CancellationToken, ValueTask> ClearTruckPlunderPending { get; init; }

    public ValueTask GotoWorldCoordinateAsync(int serverId, int x, int y, CancellationToken cancellationToken = default) => GotoWorldCoordinate(serverId, x, y, cancellationToken);
    public ValueTask GotoWorldMarchAsync(int serverId, string marchUuid, CancellationToken cancellationToken = default) => GotoWorldMarch(serverId, marchUuid, cancellationToken);
    public ValueTask<int> GetCurrentServerIdAsync(CancellationToken cancellationToken = default) => GetCurrentServerId(cancellationToken);
    public ValueTask GotoServerAsync(int serverId, CancellationToken cancellationToken = default) => GotoServer(serverId, cancellationToken);
    public ValueTask<TreasureInspectionResult> InspectTreasureStatesAsync(IReadOnlyList<JsonElement> records, bool refresh, CancellationToken cancellationToken = default) => InspectTreasureStates(records, refresh, cancellationToken);
    public ValueTask<JsonElement> GetTreasureClaimStatusAsync(CancellationToken cancellationToken = default) => GetTreasureClaimStatus(cancellationToken);
    public ValueTask<JsonElement> ClaimTreasuresAsync(TreasureClaimProviderRequest request, CancellationToken cancellationToken = default) => ClaimTreasures(request, cancellationToken);
    public ValueTask<DispatchShareProviderResult> ShareDispatchTaskToAllianceAsync(JsonElement row, CancellationToken cancellationToken = default) => ShareDispatchTaskToAlliance(row, cancellationToken);
    public ValueTask<IReadOnlyList<JsonElement>> PrepareGhostPlunderTasksAsync(IReadOnlyList<JsonElement> rows, CancellationToken cancellationToken = default) => PrepareGhostPlunderTasks(rows, cancellationToken);
    public ValueTask<MapPlunderServerDayProviderResult?> GetMapPlunderServerDayStartAsync(CancellationToken cancellationToken = default) => GetMapPlunderServerDayStart(cancellationToken);
    public ValueTask<IReadOnlyList<DispatchPlunderArmResult>> ArmDispatchPlunderAsync(IReadOnlyList<DispatchPlunderArmJob> jobs, CancellationToken cancellationToken = default) => ArmDispatchPlunder(jobs, cancellationToken);
    public ValueTask<TruckPlunderArmResult> ArmTruckPlunderAsync(TruckPlunderArmJob job, CancellationToken cancellationToken = default) => ArmTruckPlunder(job, cancellationToken);
    public ValueTask<IReadOnlyList<DispatchPlunderResultEvent>> DrainDispatchPlunderResultsAsync(CancellationToken cancellationToken = default) => DrainDispatchPlunderResults(cancellationToken);
    public ValueTask<IReadOnlyList<TruckPlunderResultEvent>> DrainTruckPlunderResultsAsync(CancellationToken cancellationToken = default) => DrainTruckPlunderResults(cancellationToken);
    public ValueTask ClearTruckPlunderPendingAsync(int serverId, string trainUuid, string jobId, CancellationToken cancellationToken = default) => ClearTruckPlunderPending(serverId, trainUuid, jobId, cancellationToken);
}

public sealed class UnavailableMapActionProvider : IMapActionProvider
{
    public static UnavailableMapActionProvider Instance { get; } = new();
    private UnavailableMapActionProvider() { }
    private static BridgeCommandException Error() => new("GAME_CONNECTION_UNAVAILABLE", "game connection unavailable");
    public ValueTask GotoWorldCoordinateAsync(int serverId, int x, int y, CancellationToken cancellationToken = default) => ValueTask.FromException(Error());
    public ValueTask GotoWorldMarchAsync(int serverId, string marchUuid, CancellationToken cancellationToken = default) => ValueTask.FromException(Error());
    public ValueTask<int> GetCurrentServerIdAsync(CancellationToken cancellationToken = default) => ValueTask.FromException<int>(Error());
    public ValueTask GotoServerAsync(int serverId, CancellationToken cancellationToken = default) => ValueTask.FromException(Error());
    public ValueTask<TreasureInspectionResult> InspectTreasureStatesAsync(IReadOnlyList<JsonElement> records, bool refresh, CancellationToken cancellationToken = default) => ValueTask.FromException<TreasureInspectionResult>(Error());
    public ValueTask<JsonElement> GetTreasureClaimStatusAsync(CancellationToken cancellationToken = default) => ValueTask.FromException<JsonElement>(Error());
    public ValueTask<JsonElement> ClaimTreasuresAsync(TreasureClaimProviderRequest request, CancellationToken cancellationToken = default) => ValueTask.FromException<JsonElement>(Error());
    public ValueTask<DispatchShareProviderResult> ShareDispatchTaskToAllianceAsync(JsonElement row, CancellationToken cancellationToken = default) => ValueTask.FromException<DispatchShareProviderResult>(Error());
    public ValueTask<IReadOnlyList<JsonElement>> PrepareGhostPlunderTasksAsync(IReadOnlyList<JsonElement> rows, CancellationToken cancellationToken = default) => ValueTask.FromException<IReadOnlyList<JsonElement>>(Error());
    public ValueTask<MapPlunderServerDayProviderResult?> GetMapPlunderServerDayStartAsync(CancellationToken cancellationToken = default) => ValueTask.FromException<MapPlunderServerDayProviderResult?>(Error());
    public ValueTask<IReadOnlyList<DispatchPlunderArmResult>> ArmDispatchPlunderAsync(IReadOnlyList<DispatchPlunderArmJob> jobs, CancellationToken cancellationToken = default) => ValueTask.FromException<IReadOnlyList<DispatchPlunderArmResult>>(Error());
    public ValueTask<TruckPlunderArmResult> ArmTruckPlunderAsync(TruckPlunderArmJob job, CancellationToken cancellationToken = default) => ValueTask.FromException<TruckPlunderArmResult>(Error());
    public ValueTask<IReadOnlyList<DispatchPlunderResultEvent>> DrainDispatchPlunderResultsAsync(CancellationToken cancellationToken = default) => ValueTask.FromException<IReadOnlyList<DispatchPlunderResultEvent>>(Error());
    public ValueTask<IReadOnlyList<TruckPlunderResultEvent>> DrainTruckPlunderResultsAsync(CancellationToken cancellationToken = default) => ValueTask.FromException<IReadOnlyList<TruckPlunderResultEvent>>(Error());
    public ValueTask ClearTruckPlunderPendingAsync(int serverId, string trainUuid, string jobId, CancellationToken cancellationToken = default) => ValueTask.FromException(Error());
}
