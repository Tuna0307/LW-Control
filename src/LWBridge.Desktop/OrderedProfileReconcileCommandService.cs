using System.Text.Json;

namespace LWBridge.Desktop;

/// <summary>
/// Locally available native startup ordering, separate from the protected
/// entitlement/capacity and per-profile lease acquisition protocols.
/// The registry's SQL order is display_order,created_at,id. Each enabled,
/// unlocked profile is visited exactly once in that order and a failure in
/// one must not prevent an independent owner from reconciling next.
/// </summary>
internal sealed class OrderedProfileReconcileCommandService : INativeAsyncCommandService
{
    private readonly ProfileRegistryCommandService profiles;
    private readonly Func<string, JsonElement, CancellationToken, Task<object?>> reconcileOwner;
    private readonly SemaphoreSlim gate = new(1, 1);
    private bool consumed;
    private OverviewStartupError[] priorErrors = [];

    internal OrderedProfileReconcileCommandService(
        ProfileRegistryCommandService profiles,
        Func<string, JsonElement, CancellationToken, Task<object?>> reconcileOwner)
    {
        this.profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        this.reconcileOwner = reconcileOwner ?? throw new ArgumentNullException(nameof(reconcileOwner));
    }

    public bool CanHandle(string command) =>
        command == "profile_instances_reconcile";

    public async Task<object?> InvokeAsync(
        string command, JsonElement payload, CancellationToken cancellationToken)
    {
        if (!CanHandle(command))
            throw new BridgeCommandException("COMMAND_NOT_IMPLEMENTED",
                "Profile startup reconciliation is not implemented for this command.");
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (consumed) return new { errors = priorErrors };
            var errors = new List<OverviewStartupError>();
            // Do not silently truncate to maxProfiles: that number is an
            // unverified protected entitlement response in the original.
            // The current single-owner product exposes only one local owner;
            // unavailable others receive an explicit error, never an aliased
            // selected-profile launch.
            foreach (ProfileRegistryEntry profile in profiles.Snapshot.Profiles)
            {
                if (!profile.Enabled || profile.LockedReason is not null) continue;
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    object? result = await reconcileOwner(profile.Id, payload, cancellationToken)
                        .ConfigureAwait(false);
                    JsonElement obj = JsonSerializer.SerializeToElement(result, JsonOptions.Default);
                    if (obj.ValueKind != JsonValueKind.Object ||
                        !obj.TryGetProperty("errors", out JsonElement items) ||
                        items.ValueKind != JsonValueKind.Array)
                        throw new BridgeCommandException("PROFILE_RUNTIME_UNAVAILABLE",
                            "The profile owner did not return reconciliation errors.");
                    foreach (JsonElement item in items.EnumerateArray())
                    {
                        if (item.ValueKind != JsonValueKind.Object) continue;
                        string? ownerId = item.TryGetProperty("profileId", out var p) &&
                            p.ValueKind == JsonValueKind.String ? p.GetString() : null;
                        string? code = item.TryGetProperty("error", out var e) &&
                            e.ValueKind == JsonValueKind.String ? e.GetString() : null;
                        if (string.IsNullOrWhiteSpace(code)) continue;
                        if (!string.Equals(ownerId, profile.Id, StringComparison.Ordinal))
                            throw new BridgeCommandException("PROFILE_SCOPE_MISMATCH",
                                "A profile reconciliation response belonged to a different owner.");
                        string message = item.TryGetProperty("message", out var m) &&
                            m.ValueKind == JsonValueKind.String ? m.GetString() ?? code : code;
                        errors.Add(new OverviewStartupError(profile.Id, code, message));
                    }
                }
                catch (BridgeCommandException error)
                {
                    errors.Add(new OverviewStartupError(profile.Id, error.Code, error.Message));
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    // A failed local owner is an error for THAT profile, not a
                    // reason to skip later owners or impersonate their launcher.
                    // Unknown local exceptions do not acquire an invented
                    // original 0.3.17 error code.
                    errors.Add(new OverviewStartupError(profile.Id,
                        "PROFILE_RUNTIME_UNAVAILABLE", "Local profile reconciliation failed."));
                }
            }
            priorErrors = [.. errors];
            consumed = true;
            return new { errors = priorErrors };
        }
        finally { gate.Release(); }
    }
}
