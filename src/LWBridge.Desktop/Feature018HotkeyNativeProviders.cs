using System.Text.Json;

namespace LWBridge.Desktop;

/// <summary>
/// Maps a verified game-window key edge to the exact FEATURE018 native game
/// service. The service owns authenticated game-instance correlation; the
/// Lua module decides whether a current-build native method is supported.
/// </summary>
internal sealed class Feature018HotkeyNativeProviders
{
    private const string CommandName = "feature018_hotkey_action";
    private readonly INativeAsyncCommandService gameCommands;

    internal Feature018HotkeyNativeProviders(INativeAsyncCommandService gameCommands)
    {
        this.gameCommands = gameCommands ?? throw new ArgumentNullException(nameof(gameCommands));
    }

    internal IReadOnlyDictionary<Feature018HotkeyKind,
        Func<Feature018HotkeyCommand, CancellationToken,
            Task<Feature018HotkeyActionOutcome>>> Create()
    {
        return Enum.GetValues<Feature018HotkeyKind>().ToDictionary(
            kind => kind,
            kind => (Func<Feature018HotkeyCommand, CancellationToken,
                Task<Feature018HotkeyActionOutcome>>)ExecuteAsync);
    }

    internal static string ActionName(Feature018HotkeyKind kind) => kind switch
    {
        Feature018HotkeyKind.AttackSquad => "attack",
        Feature018HotkeyKind.RecallSquad => "recall",
        Feature018HotkeyKind.ShieldRemaining => "shieldOverlay",
        Feature018HotkeyKind.UseShield => "shieldUse",
        Feature018HotkeyKind.ApplyEquipmentPreset => "equipment",
        Feature018HotkeyKind.RandomRelocate => "randomRelocate",
        Feature018HotkeyKind.AllianceRelocate => "allianceRelocate",
        Feature018HotkeyKind.ReinforceFrontline => "frontlineReinforce",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private async Task<Feature018HotkeyActionOutcome> ExecuteAsync(
        Feature018HotkeyCommand command, CancellationToken cancellationToken)
    {
        if (!gameCommands.CanHandle(CommandName))
        {
            return new(Feature018HotkeyActionStatus.Unsupported,
                "authenticated_game_hotkey_transport_unavailable");
        }

        // The original opt-in speedup toggles are separate settings; until the
        // current game request supports them explicitly, fail closed rather
        // than silently ignoring the player's spend preferences.
        if (command.Intent.Kind == Feature018HotkeyKind.AttackSquad &&
            (command.AllowMarchSpeedupItems || command.AllowMarchSpeedupDiamonds))
        {
            return new(Feature018HotkeyActionStatus.Unsupported,
                "attack_march_speedup_options_not_source_verified");
        }

        JsonElement request = JsonSerializer.SerializeToElement(new
        {
            action = ActionName(command.Intent.Kind),
            slot = command.Intent.Slot,
        }, JsonOptions.Default);
        try
        {
            object? raw = await gameCommands.InvokeAsync(
                CommandName, request, cancellationToken).ConfigureAwait(false);
            // A source-verified game sender may return a correlated
            // state=accepted receipt without a game-state observation.
            // Preserve that distinction instead of falsely marking it failed.
            if (raw is not JsonElement evidence ||
                evidence.ValueKind != JsonValueKind.Object)
            {
                return new(Feature018HotkeyActionStatus.Failed,
                    "native_game_state_receipt_missing");
            }

            if ((evidence.TryGetProperty("requestDispatched", out var dispatched) &&
                 dispatched.ValueKind == JsonValueKind.True) ||
                (evidence.TryGetProperty("accepted", out var accepted) &&
                 accepted.ValueKind == JsonValueKind.True))
            {
                return new(Feature018HotkeyActionStatus.Submitted,
                    "game_message_sent_without_independent_state_confirmation");
            }

            // A nonempty `observation` string alone does not establish which
            // game state was checked. Match the exact source-backed receipt
            // for the requested kind before calling it Confirmed; new action
            // kinds need their own independently verified poststate schema.
            if (!ConfirmedReceiptMatches(command.Intent, evidence))
                return new(Feature018HotkeyActionStatus.Failed,
                    "native_game_action_observation_unverified");

            return new(Feature018HotkeyActionStatus.Confirmed,
                "verified_native_game_state_observation",
                evidence.Clone());
        }
        catch (BridgeCommandException error) when (error.Code == "GAME_METHOD_UNAVAILABLE")
        {
            return new(Feature018HotkeyActionStatus.Unsupported,
                "current_client_action_method_unavailable");
        }
        catch (BridgeCommandException error) when (error.Code == "GAME_ACTION_UNCONFIRMED")
        {
            return new(Feature018HotkeyActionStatus.Submitted,
                "game_request_not_independently_confirmed");
        }
        catch (BridgeCommandException error) when (error.Code == "GAME_CONNECTION_UNAVAILABLE")
        {
            return new(Feature018HotkeyActionStatus.SessionChanged,
                "authenticated_game_route_unavailable");
        }
        catch (BridgeCommandException)
        {
            return new(Feature018HotkeyActionStatus.Failed,
                "current_client_game_action_rejected");
        }
    }

    private static bool ConfirmedReceiptMatches(
        Feature018HotkeyIntent intent, JsonElement evidence)
    {
        if (!evidence.TryGetProperty("observation", out JsonElement observed) ||
            observed.ValueKind != JsonValueKind.String)
            return false;

        switch (intent.Kind)
        {
            case Feature018HotkeyKind.ShieldRemaining:
                if (observed.GetString() != "native_MAIN_SHIELD_effect_status_map" ||
                    !evidence.TryGetProperty("active", out JsonElement active) ||
                    active.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                    !evidence.TryGetProperty("serverTimeMilliseconds", out JsonElement clock) ||
                    !clock.TryGetDouble(out double time) || !double.IsFinite(time) || time < 0 ||
                    !evidence.TryGetProperty("shieldStatusCount", out JsonElement count) ||
                    !count.TryGetInt32(out int shields) || shields < 0)
                    return false;
                return active.GetBoolean() == (shields > 0);

            case Feature018HotkeyKind.RecallSquad:
                return observed.GetString() == "native_world_march_return_target" &&
                       evidence.TryGetProperty("slot", out JsonElement slot) &&
                       slot.TryGetInt32(out int actualSlot) && actualSlot == intent.Slot &&
                       evidence.TryGetProperty("target", out JsonElement target) &&
                       target.ValueKind == JsonValueKind.String &&
                       target.GetString() == "BACK_HOME" &&
                       evidence.TryGetProperty("returning", out JsonElement returning) &&
                       returning.ValueKind == JsonValueKind.True &&
                       IsNativeUuid(evidence, "marchUuid") &&
                       IsNativeUuid(evidence, "formationUuid");

            default:
                // No other kind has an audited current-client state receipt.
                return false;
        }
    }

    private static bool IsNativeUuid(JsonElement evidence, string name) =>
        evidence.TryGetProperty(name, out JsonElement field) &&
        field.ValueKind == JsonValueKind.String &&
        field.GetString() is { Length: > 0 and <= 20 } value &&
        value[0] != '0' && value.All(char.IsAsciiDigit);
}
