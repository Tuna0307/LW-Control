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
        // Original 0x154905: a present runtime always answers (idle is a normal result); STATE_UNAVAILABLE only
        // names a poisoned lock. An absent lifecycle owner is the original's "no runtime for this profile".
        return statusProvider() ??
            throw new BridgeCommandException("PROFILE_RUNTIME_UNAVAILABLE", "PROFILE_RUNTIME_UNAVAILABLE");
    }

    private void RequireRuntime(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty("profileId", out JsonElement property) ||
            property.ValueKind != JsonValueKind.String ||
            string.IsNullOrEmpty(property.GetString()))
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
