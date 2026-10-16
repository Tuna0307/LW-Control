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
            // The native exchange yields ONLY data from a fully correlated
            // game-side state=confirmed result. Still require the observed
            // shield fields or an explicitly named native-state observation.
            if (raw is not JsonElement evidence ||
                evidence.ValueKind != JsonValueKind.Object ||
                !evidence.TryGetProperty("observation", out var origin) ||
                origin.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(origin.GetString()))
            {
                return new(Feature018HotkeyActionStatus.Failed,
                    "native_game_state_receipt_missing");
            }

            if (command.Intent.Kind == Feature018HotkeyKind.ShieldRemaining &&
                (!evidence.TryGetProperty("active", out var active) ||
                 active.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                 !evidence.TryGetProperty("serverTimeMilliseconds", out var clock) ||
                 !clock.TryGetDouble(out var serverTime) || serverTime < 0 ||
                 !evidence.TryGetProperty("shieldStatusCount", out var count) ||
                 !count.TryGetInt32(out var numberOfShields) || numberOfShields < 0))
            {
                return new(Feature018HotkeyActionStatus.Failed,
                    "native_game_shield_status_receipt_invalid");
            }

            if (evidence.TryGetProperty("requestDispatched", out var dispatched) &&
                dispatched.ValueKind == JsonValueKind.True)
            {
                // A source-verified network send without independently
                // observed state is not completed gameplay.
                return new(Feature018HotkeyActionStatus.Submitted,
                    "game_message_sent_without_independent_state_confirmation");
            }
            return new(Feature018HotkeyActionStatus.Confirmed,
                "verified_native_game_state_observation");
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
}
