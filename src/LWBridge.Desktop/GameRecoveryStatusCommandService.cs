using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed class GameRecoveryStatusCommandService
{
    private readonly string profileId;
    private readonly string? runtimeDirectory;
    private readonly Func<OverviewRecoveryStatus?> statusProvider;

    internal GameRecoveryStatusCommandService(
        string profileId,
        string? runtimeDirectory,
        Func<OverviewRecoveryStatus?> statusProvider)
    {
        this.profileId = profileId;
        this.runtimeDirectory = runtimeDirectory;
        this.statusProvider = statusProvider;
    }

    internal object Invoke(JsonElement payload)
    {
        RequireRuntime(payload);
        OverviewRecoveryStatus? status = statusProvider();
        if (status is null)
        {
            throw new BridgeCommandException(
                "STATE_UNAVAILABLE",
                "game recovery state is unavailable");
        }

        return status;
    }

    private void RequireRuntime(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty("profileId", out JsonElement property) ||
            property.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new BridgeCommandException(
                "PROFILE_ID_REQUIRED",
                "PROFILE_ID_REQUIRED");
        }

        if (!string.Equals(
                property.GetString(),
                profileId,
                StringComparison.Ordinal) ||
            runtimeDirectory is null)
        {
            throw new BridgeCommandException(
                "PROFILE_RUNTIME_UNAVAILABLE",
                "PROFILE_RUNTIME_UNAVAILABLE");
        }
    }
}
