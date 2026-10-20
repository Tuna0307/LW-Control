using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed record Feature018HotkeySession(
    string ProfileId,
    string InstanceId,
    string GameUid,
    int ServerId);

internal sealed record Feature018HotkeyCommand(
    Feature018HotkeySession Session,
    Feature018HotkeyIntent Intent,
    string? ExpectedStageRunId = null,
    int ReinforceCount = 5);

internal enum Feature018HotkeyActionStatus
{
    Disabled,
    NotFocused,
    SessionChanged,
    Unsupported,
    Busy,
    Failed,
    Submitted,
    Confirmed,
}

internal sealed record Feature018HotkeyActionOutcome(
    Feature018HotkeyActionStatus Status,
    string Reason,
    JsonElement? AuthoritativeObservation = null)
{
    // Only a source-verified game-side result may claim an actual state change.
    internal bool Working => Status == Feature018HotkeyActionStatus.Confirmed;
}

/// <summary>
/// The input recognizer reports user intentions. This broker may execute only
/// explicitly registered, source-verified native action providers. Missing
/// current-client methods remain UNSUPPORTED instead of simulated success.
/// The same session and game focus must be checked again at dispatch time.
/// </summary>
internal sealed class Feature018HotkeyActions
{
    private readonly Feature018HotkeySession expected;
    private readonly Func<Feature018HotkeySession?> currentSession;
    private readonly Func<bool> safeGameForeground;
    private readonly Func<HotkeyConfig> currentPreferences;
    private readonly IReadOnlyDictionary<Feature018HotkeyKind,
        Func<Feature018HotkeyCommand, CancellationToken, Task<Feature018HotkeyActionOutcome>>> providers;
    private readonly SemaphoreSlim dispatchGate = new(1, 1);
    // Only consecutive confirmed G actions can admit waiting G edges. The
    // native run identity prevents a pending key migrating to a later stage.
    private string? lastConfirmedStageRunId;
    private long lastConfirmedStageEpoch;
    private long reinforcementEpoch;

    internal Feature018HotkeyActions(
        Feature018HotkeySession expected,
        Func<Feature018HotkeySession?> currentSession,
        Func<bool> safeGameForeground,
        Func<HotkeyConfig> currentPreferences,
        IReadOnlyDictionary<Feature018HotkeyKind,
            Func<Feature018HotkeyCommand, CancellationToken,
                Task<Feature018HotkeyActionOutcome>>>? providers = null)
    {
        if (string.IsNullOrWhiteSpace(expected.ProfileId) ||
            string.IsNullOrWhiteSpace(expected.InstanceId) ||
            string.IsNullOrWhiteSpace(expected.GameUid) || expected.ServerId <= 0)
            throw new ArgumentException("Complete authenticated game identity is required", nameof(expected));
        this.expected = expected;
        this.currentSession = currentSession ?? throw new ArgumentNullException(nameof(currentSession));
        this.safeGameForeground = safeGameForeground ?? throw new ArgumentNullException(nameof(safeGameForeground));
        this.currentPreferences = currentPreferences ?? throw new ArgumentNullException(nameof(currentPreferences));
        this.providers = providers ??
            new Dictionary<Feature018HotkeyKind,
                Func<Feature018HotkeyCommand, CancellationToken, Task<Feature018HotkeyActionOutcome>>>();
    }

    internal async Task<Feature018HotkeyActionOutcome> TryDispatchAsync(
        Feature018HotkeyIntent intent,
        CancellationToken cancellationToken = default)
    {
        bool reinforcement = intent.Kind == Feature018HotkeyKind.ReinforceFrontline;
        if (!reinforcement) Interlocked.Increment(ref reinforcementEpoch);
        long capturedEpoch = Volatile.Read(ref reinforcementEpoch);
        if (!SameAdmittedSession())
            return Reject(Feature018HotkeyActionStatus.SessionChanged, "admitted_game_session_unavailable");
        bool focused;
        HotkeyConfig config;
        try
        {
            focused = safeGameForeground();
            config = currentPreferences();
        }
        catch
        {
            return Reject(Feature018HotkeyActionStatus.NotFocused, "game_focus_or_config_unavailable");
        }
        if (!focused)
            return Reject(Feature018HotkeyActionStatus.NotFocused, "game_window_or_text_focus_unverified");
        if (!Enabled(config, intent.Kind))
            return Reject(Feature018HotkeyActionStatus.Disabled, "profile_hotkey_preference_disabled");
        if (!ValidSlot(intent))
            return Reject(Feature018HotkeyActionStatus.Unsupported, "invalid_original_hotkey_slot");
        if (!providers.TryGetValue(intent.Kind, out var producer))
            return Reject(Feature018HotkeyActionStatus.Unsupported, "current_client_action_provider_unverified");
        bool queued;
        try
        {
            queued = !await dispatchGate.WaitAsync(0, cancellationToken).ConfigureAwait(false);
            if (queued)
            {
                if (!reinforcement)
                    return new(Feature018HotkeyActionStatus.Busy, "other_hotkey_action_pending");
                await dispatchGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            Interlocked.Increment(ref reinforcementEpoch);
            throw;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            // A waiting key is a distinct captured press, not a retry of the
            // preceding call. Re-read every mutable admission after waiting.
            if (!SameAdmittedSession())
                return Reject(Feature018HotkeyActionStatus.SessionChanged, "game_session_changed_before_dispatch");
            if (!safeGameForeground())
                return Reject(Feature018HotkeyActionStatus.NotFocused, "game_focus_changed_before_dispatch");
            config = currentPreferences();
            if (!Enabled(config, intent.Kind))
                return Reject(Feature018HotkeyActionStatus.Disabled, "profile_hotkey_preference_disabled");
            string? expectedStageRunId = null;
            if (queued)
            {
                if (Volatile.Read(ref reinforcementEpoch) != capturedEpoch ||
                    lastConfirmedStageEpoch != capturedEpoch ||
                    string.IsNullOrWhiteSpace(lastConfirmedStageRunId))
                    return Reject(Feature018HotkeyActionStatus.Failed, "queued_g_prior_stage_not_confirmed");
                expectedStageRunId = lastConfirmedStageRunId;
            }
            lastConfirmedStageRunId = null;
            var command = new Feature018HotkeyCommand(
                expected,
                intent,
                expectedStageRunId,
                config.Normalized().FrontlineReinforceCount);
            Feature018HotkeyActionOutcome result = await producer(command, cancellationToken)
                .ConfigureAwait(false) ??
                new(Feature018HotkeyActionStatus.Failed, "game_action_returned_no_receipt");
            if (!await SameAdmittedSessionAfterNativeCallAsync(cancellationToken).ConfigureAwait(false))
                return Reject(Feature018HotkeyActionStatus.SessionChanged, "game_session_changed_after_native_call");
            if (reinforcement && result.Status == Feature018HotkeyActionStatus.Confirmed &&
                result.AuthoritativeObservation is { ValueKind: JsonValueKind.Object } receipt &&
                receipt.TryGetProperty("stageRunId", out JsonElement stage) &&
                stage.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(stage.GetString()) &&
                (expectedStageRunId is null || expectedStageRunId == stage.GetString()) &&
                Volatile.Read(ref reinforcementEpoch) == capturedEpoch)
            {
                lastConfirmedStageRunId = stage.GetString();
                lastConfirmedStageEpoch = capturedEpoch;
            }
            else
            {
                Interlocked.Increment(ref reinforcementEpoch);
            }
            // A request accepted by a game message sender is still Submitted;
            // a provider can return Confirmed only after an observed game state.
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Interlocked.Increment(ref reinforcementEpoch);
            throw;
        }
        catch (Exception)
        {
            return Reject(Feature018HotkeyActionStatus.Failed, "native_game_action_failed");
        }
        finally
        {
            dispatchGate.Release();
        }
    }

    private Feature018HotkeyActionOutcome Reject(Feature018HotkeyActionStatus status, string reason)
    {
        Interlocked.Increment(ref reinforcementEpoch);
        return new(status, reason);
    }

    private bool SameAdmittedSession()
    {
        try { return currentSession() is { } actual && actual == expected; }
        catch { return false; }
    }

    private async Task<bool> SameAdmittedSessionAfterNativeCallAsync(
        CancellationToken cancellationToken)
    {
        // The native exchange already rechecks exact process/session plus
        // authenticated UID/server immediately before returning its response.
        // A heartbeat rewrite can nevertheless make the window-level reader
        // temporarily return null at this second, independent broker check.
        // Distinguish that short unavailable sample from an explicit different
        // account/session: any concrete mismatch fails immediately.
        for (int attempt = 0; attempt < 10; attempt++)
        {
            Feature018HotkeySession? actual;
            try { actual = currentSession(); }
            catch { actual = null; }
            if (actual == expected) return true;
            if (actual is not null) return false;
            if (attempt == 9) return false;
            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        }
        return false;
    }

    internal static bool Enabled(HotkeyConfig config, Feature018HotkeyKind kind) => kind switch
    {
        Feature018HotkeyKind.AttackSquad => config.Attack,
        Feature018HotkeyKind.RecallSquad => config.Recall,
        Feature018HotkeyKind.UseShield => config.ShieldUse,
        Feature018HotkeyKind.ApplyEquipmentPreset => config.Equipment,
        Feature018HotkeyKind.RandomRelocate => config.RandomRelocate,
        Feature018HotkeyKind.AllianceRelocate => config.AllianceRelocate,
        Feature018HotkeyKind.ReinforceFrontline => config.FrontlineReinforce,
        _ => false,
    };

    private static bool ValidSlot(Feature018HotkeyIntent intent) => intent.Kind switch
    {
        Feature018HotkeyKind.AttackSquad or
        Feature018HotkeyKind.RecallSquad or
        Feature018HotkeyKind.ApplyEquipmentPreset => intent.Slot is >= 1 and <= 4,
        Feature018HotkeyKind.UseShield => intent.Slot is 8 or 12 or 24,
        Feature018HotkeyKind.ReinforceFrontline => intent.Slot == 5,
        Feature018HotkeyKind.RandomRelocate or
        Feature018HotkeyKind.AllianceRelocate => intent.Slot == 0,
        _ => false,
    };
}
