using System.Text.Json;

namespace LWBridge.Map317;

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

    public ValueTask GotoWorldCoordinateAsync(int serverId, int x, int y, CancellationToken cancellationToken = default) => GotoWorldCoordinate(serverId, x, y, cancellationToken);
    public ValueTask GotoWorldMarchAsync(int serverId, string marchUuid, CancellationToken cancellationToken = default) => GotoWorldMarch(serverId, marchUuid, cancellationToken);
    public ValueTask<int> GetCurrentServerIdAsync(CancellationToken cancellationToken = default) => GetCurrentServerId(cancellationToken);
    public ValueTask GotoServerAsync(int serverId, CancellationToken cancellationToken = default) => GotoServer(serverId, cancellationToken);
    public ValueTask<TreasureInspectionResult> InspectTreasureStatesAsync(IReadOnlyList<JsonElement> records, bool refresh, CancellationToken cancellationToken = default) => InspectTreasureStates(records, refresh, cancellationToken);
    public ValueTask<JsonElement> GetTreasureClaimStatusAsync(CancellationToken cancellationToken = default) => GetTreasureClaimStatus(cancellationToken);
    public ValueTask<JsonElement> ClaimTreasuresAsync(TreasureClaimProviderRequest request, CancellationToken cancellationToken = default) => ClaimTreasures(request, cancellationToken);
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
}
