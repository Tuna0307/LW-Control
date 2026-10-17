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
    // The UI broker locks only its own session. A replacement broker/provider
    // created during a slow game RPC can otherwise enter a second mutating
    // call while the first native request/confirmation is unresolved.
    // Never queue/replay that key: fail closed and leave read-only status free.
    private static int mutatingNativeActionInFlight;
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
        // The broker normally authenticates the selected session. Validate the
        // exact native mutation owner *again* at the provider boundary, before
        // even probing the native mailbox. A synthetic key or stale coordinator
        // cannot make a main-account (2212) item/gear/march/relocation action.
        if (command.Intent.Kind != Feature018HotkeyKind.ShieldRemaining &&
            (command.Session.GameUid != LWBridgeWindow.Feature018ApprovedGameUid ||
             command.Session.ServerId != LWBridgeWindow.Feature018ApprovedServerId ||
             string.IsNullOrWhiteSpace(command.Session.ProfileId) ||
             string.IsNullOrWhiteSpace(command.Session.InstanceId)))
        {
            return new(Feature018HotkeyActionStatus.SessionChanged,
                "native_hotkey_mutation_owner_not_approved");
        }
        if (command.Intent.Kind == Feature018HotkeyKind.ShieldRemaining)
            return await ExecuteAdmittedAsync(command, cancellationToken).ConfigureAwait(false);
        if (Interlocked.CompareExchange(ref mutatingNativeActionInFlight, 1, 0) != 0)
            return new(Feature018HotkeyActionStatus.Busy,
                "another_native_hotkey_mutation_pending");
        try
        {
            return await ExecuteAdmittedAsync(command, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Volatile.Write(ref mutatingNativeActionInFlight, 0);
        }
    }

    private async Task<Feature018HotkeyActionOutcome> ExecuteAdmittedAsync(
        Feature018HotkeyCommand command, CancellationToken cancellationToken)
    {
        if (!gameCommands.CanHandle(CommandName))
        {
            return new(Feature018HotkeyActionStatus.Unsupported,
                "authenticated_game_hotkey_transport_unavailable");
        }

        // The game-side current-player UID comparison is a second identity
        // fence for an attack. Other game actions keep their native two-field
        // command contract; no unproven speedup is implied.
        JsonElement request = command.Intent.Kind == Feature018HotkeyKind.AttackSquad
            ? JsonSerializer.SerializeToElement(new
            {
                action = ActionName(command.Intent.Kind),
                slot = command.Intent.Slot,
                authorizedUid = command.Session.GameUid,
                allowRapidItem = command.AllowMarchSpeedupItems,
                allowRapidDiamond = command.AllowMarchSpeedupDiamonds,
            }, JsonOptions.Default)
            : JsonSerializer.SerializeToElement(new
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

            bool dispatchedNativeRequest =
                evidence.TryGetProperty("requestDispatched", out var dispatched) &&
                dispatched.ValueKind == JsonValueKind.True;
            bool nativeAcceptedOnly =
                evidence.TryGetProperty("accepted", out var accepted) &&
                accepted.ValueKind == JsonValueKind.True;
            // The original native action receipt preserves requestDispatched
            // after its reply and independent after-state have settled. Do
            // not demote a *completed* action to pending merely because it
            // truthfully records that the original game sender fired once.
            // A send-only or ambiguous receipt still cannot reach Confirmed:
            // it must explicitly be no longer pending AND pass the exact
            // source-specific poststate/reply contract below.
            if (dispatchedNativeRequest &&
                evidence.TryGetProperty("awaitingGameConfirmation", out var completed) &&
                completed.ValueKind == JsonValueKind.False &&
                ConfirmedReceiptMatches(command.Intent, evidence))
                return new(Feature018HotkeyActionStatus.Confirmed,
                    "verified_native_game_state_observation", evidence.Clone());
            if (dispatchedNativeRequest || nativeAcceptedOnly)
            {
                // A source-backed accepted UI chooser/preflight need not have
                // sent a game RPC. Nor can an accepted-but-missing send field
                // prove which original key caused a native action. Reserve
                // 'message sent' and queryOnly polling for explicit TRUE send.
                if (evidence.TryGetProperty("slot", out JsonElement echoedSlot) &&
                    (!echoedSlot.TryGetInt32(out int actualSlot) ||
                     actualSlot != command.Intent.Slot) ||
                    evidence.TryGetProperty("action", out JsonElement echoedAction) &&
                    (echoedAction.ValueKind != JsonValueKind.String ||
                     echoedAction.GetString() != ActionName(command.Intent.Kind)))
                    return new(Feature018HotkeyActionStatus.Failed,
                        "native_action_accepted_for_different_intent");
                if (!dispatchedNativeRequest)
                    return new(Feature018HotkeyActionStatus.Submitted,
                        "native_action_accepted_without_verified_game_rpc_dispatch");

                // Only a single verified outbound send is allowed. Once sent,
                // queryOnly follow-ups must be read-only and preserve the
                // original native object UUID throughout confirmation.
                if (evidence.TryGetProperty("awaitingGameConfirmation", out JsonElement waiting) &&
                    waiting.ValueKind == JsonValueKind.True &&
                    evidence.TryGetProperty("slot", out JsonElement sentSlot) &&
                    sentSlot.TryGetInt32(out int slot) && slot == command.Intent.Slot)
                {
                    string? uuidProperty = command.Intent.Kind switch
                    {
                        Feature018HotkeyKind.RecallSquad => "marchUuid",
                        Feature018HotkeyKind.AttackSquad => "formationUuid",
                        Feature018HotkeyKind.UseShield => "itemUuid",
                        Feature018HotkeyKind.ApplyEquipmentPreset => "presetId",
                        Feature018HotkeyKind.RandomRelocate or Feature018HotkeyKind.AllianceRelocate => "itemUuid",
                        _ => null,
                    };
                    if (uuidProperty is not null &&
                        ((command.Intent.Kind is Feature018HotkeyKind.UseShield or
                            Feature018HotkeyKind.RandomRelocate or Feature018HotkeyKind.AllianceRelocate
                            ? IsNativeItemUseUuid(evidence, uuidProperty)
                            : IsNativeUuid(evidence, uuidProperty)) ||
                         (uuidProperty == "presetId" && evidence.TryGetProperty("presetId", out var preset) &&
                          preset.ValueKind == JsonValueKind.String && preset.GetString() is { Length: > 0 and <= 128 })))
                        return await PollAfterSendAsync(command, uuidProperty,
                            evidence.GetProperty(uuidProperty).GetString()!, cancellationToken)
                            .ConfigureAwait(false);
                }
                return new(Feature018HotkeyActionStatus.Submitted,
                    "game_message_sent_without_independent_state_confirmation");
            }

            // A nonempty `observation` string alone does not establish which
            // game state was checked. Match the exact source-backed receipt
            // for the requested kind before calling it Confirmed; new action
            // kinds need their own independently verified poststate schema.
            if (command.Intent.Kind == Feature018HotkeyKind.RecallSquad &&
                evidence.TryGetProperty("observation", out JsonElement recallObservation) &&
                recallObservation.ValueKind == JsonValueKind.String &&
                recallObservation.GetString() == "native_world_march_return_target" &&
                !RecallCausalityMatches(evidence))
            {
                // The previous native client returned this same target when
                // already BACK_HOME, without dispatching anything. A cached
                // state alone is never a successful key-to-RPC/ACK receipt.
                return new(Feature018HotkeyActionStatus.Failed,
                    "native_recall_back_home_state_lacks_attributed_send_and_reply");
            }
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
            // The authenticated native exchange exposes data even when an
            // accepted response is NOT eligible as a game send. For example,
            // source v26 F10 can request the original chooser while explicitly
            // reporting originalPacketSend=false. Never label this a delivered
            // relocation packet, nor mistake absence of evidence for success.
            if (error.Details is JsonElement data && data.ValueKind == JsonValueKind.Object)
            {
                // SendMessage returning false or throwing may occur AFTER the
                // packet escaped to the game network. Native F9 now reports
                // attempted-but-uncertain explicitly. The false confirmation
                // flag is not evidence of no outbound packet; never invite a
                // retransmission from the keyboard or report a definite miss.
                bool uncertainAttempt =
                    data.TryGetProperty("nativeSendAttempted", out var attempted) &&
                    attempted.ValueKind == JsonValueKind.True &&
                    data.TryGetProperty("nativeSendOutcomeUncertain", out var unsure) &&
                    unsure.ValueKind == JsonValueKind.True;
                if (uncertainAttempt)
                    return new(Feature018HotkeyActionStatus.Submitted,
                        "native_action_outcome_unconfirmed_dispatch_unknown");
                bool noPacket =
                    data.TryGetProperty("requestDispatched", out var notSent) &&
                    notSent.ValueKind == JsonValueKind.False ||
                    data.TryGetProperty("originalPacketSend", out var packet) &&
                    packet.ValueKind == JsonValueKind.False;
                if (noPacket)
                {
                    if (command.Intent.Kind == Feature018HotkeyKind.AllianceRelocate &&
                        data.TryGetProperty("chooserRequestMade", out var chooser) &&
                        chooser.ValueKind == JsonValueKind.True)
                        return new(Feature018HotkeyActionStatus.Submitted,
                            "native_f10_chooser_requested_no_relocation_packet");
                    return new(Feature018HotkeyActionStatus.Failed,
                        "native_game_request_not_dispatched");
                }
            }
            return new(Feature018HotkeyActionStatus.Submitted,
                "native_action_outcome_unconfirmed_dispatch_unknown");
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

    private async Task<Feature018HotkeyActionOutcome> PollAfterSendAsync(
        Feature018HotkeyCommand command, string uuidProperty, string sentUuid,
        CancellationToken cancellationToken)
    {
        // A short one-way confirmation window; never send a second return-home
        // request or indefinitely hold the hotkey action dispatch gate.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(command.Intent.Kind == Feature018HotkeyKind.ApplyEquipmentPreset
            ? TimeSpan.FromMinutes(3) : TimeSpan.FromSeconds(15));
        JsonElement request = command.Intent.Kind == Feature018HotkeyKind.AttackSquad
            ? JsonSerializer.SerializeToElement(new { action = "attack", slot = command.Intent.Slot,
                queryOnly = true, authorizedUid = command.Session.GameUid }, JsonOptions.Default)
            : JsonSerializer.SerializeToElement(new
            { action = ActionName(command.Intent.Kind), slot = command.Intent.Slot, queryOnly = true }, JsonOptions.Default);
        try
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), deadline.Token).ConfigureAwait(false);
                try
                {
                    object? response = await gameCommands.InvokeAsync(
                        CommandName, request, deadline.Token).ConfigureAwait(false);
                    if (response is JsonElement gameState &&
                        gameState.ValueKind == JsonValueKind.Object &&
                        !(gameState.TryGetProperty("awaitingGameConfirmation", out var stillPending) &&
                          stillPending.ValueKind == JsonValueKind.True) &&
                        ConfirmedReceiptMatches(command.Intent, gameState))
                    {
                        if (gameState.GetProperty(uuidProperty).GetString() != sentUuid)
                            return new(Feature018HotkeyActionStatus.SessionChanged,
                                "native_action_target_changed_during_confirmation");
                        return new(Feature018HotkeyActionStatus.Confirmed,
                            "verified_native_game_state_observation", gameState.Clone());
                    }
                }
                catch (BridgeCommandException error) when (error.Code == "GAME_CONNECTION_UNAVAILABLE")
                {
                    return new(Feature018HotkeyActionStatus.SessionChanged,
                        "authenticated_game_route_unavailable");
                }
                catch (BridgeCommandException error) when (error.Code == "GAME_ACTION_UNCONFIRMED")
                {
                    // The query may legitimately observe the pre-ACK march.
                    // An unconfirmed query is never a reason to send again.
                }
                catch (BridgeCommandException)
                {
                    return new(Feature018HotkeyActionStatus.Submitted,
                        "native_action_dispatched_followup_unavailable");
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(Feature018HotkeyActionStatus.Submitted,
                "native_action_dispatched_confirmation_pending");
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
            case Feature018HotkeyKind.ApplyEquipmentPreset:
                return observed.GetString() == "native_HeroEquip_preset_slots_applied" &&
                    evidence.TryGetProperty("equipmentApplied", out var applied) && applied.ValueKind == JsonValueKind.True &&
                    evidence.TryGetProperty("slot", out var equipmentSlot) && equipmentSlot.TryGetInt32(out int ordinal) &&
                    ordinal == intent.Slot &&
                    evidence.TryGetProperty("presetId", out var presetId) && presetId.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(presetId.GetString()) &&
                    evidence.TryGetProperty("completedSteps", out var done) && done.TryGetInt32(out int completed) &&
                    evidence.TryGetProperty("plannedSteps", out var planned) && planned.TryGetInt32(out int total) &&
                    completed >= 0 && completed == total &&
                    evidence.TryGetProperty("slots", out var slots) && slots.ValueKind == JsonValueKind.Array &&
                    slots.GetArrayLength() > 0 && slots.EnumerateArray().All(item =>
                        item.TryGetProperty("currentEquipUuid", out var current) &&
                        item.TryGetProperty("targetEquipUuid", out var target) &&
                        current.ValueKind == JsonValueKind.String && target.ValueKind == JsonValueKind.String &&
                        current.GetString() == target.GetString());

            case Feature018HotkeyKind.RandomRelocate:
            case Feature018HotkeyKind.AllianceRelocate:
                return observed.GetString() == "native_relocation_reply_item_and_world_position" &&
                    evidence.TryGetProperty("awaitingGameConfirmation", out var movePending) && movePending.ValueKind == JsonValueKind.False &&
                    evidence.TryGetProperty("action", out var movedAction) &&
                    movedAction.ValueKind == JsonValueKind.String && movedAction.GetString() == ActionName(intent.Kind) &&
                    evidence.TryGetProperty("requestDispatched", out var relocationSent) && relocationSent.ValueKind == JsonValueKind.True &&
                    evidence.TryGetProperty("relocationConfirmed", out var relocated) && relocated.ValueKind == JsonValueKind.True &&
                    evidence.TryGetProperty("replyObserved", out var reply) && reply.ValueKind == JsonValueKind.True &&
                    // v26 ItemUse carries an exact opaque UTF stack UUID.
                    // The itemId fallback or opType=1 must never confirm F9.
                    evidence.TryGetProperty("replyExactItemUuidObserved", out var exactStack) && exactStack.ValueKind == JsonValueKind.True &&
                    evidence.TryGetProperty("replyUseOperationCompatible", out var normalUse) && normalUse.ValueKind == JsonValueKind.True &&
                    evidence.TryGetProperty("independentWorldAndInventoryPoststateVerified", out var independentAfter) && independentAfter.ValueKind == JsonValueKind.True &&
                    evidence.TryGetProperty("worldPointChanged", out var moved) && moved.ValueKind == JsonValueKind.True &&
                    evidence.TryGetProperty("ownerUid", out var moveOwner) && moveOwner.GetString() == LWBridgeWindow.Feature018ApprovedGameUid &&
                    evidence.TryGetProperty("sourceServerId", out var moveServer) && moveServer.TryGetInt32(out int moveServerId) &&
                    moveServerId == LWBridgeWindow.Feature018ApprovedServerId &&
                    evidence.TryGetProperty("beforeWorldPointId", out var beforePoint) && beforePoint.TryGetInt64(out long oldPoint) &&
                    evidence.TryGetProperty("afterWorldPointId", out var afterPoint) && afterPoint.TryGetInt64(out long newPoint) &&
                    oldPoint is >= 0 and < 10000000 && newPoint is >= 0 and < 10000000 && oldPoint != newPoint &&
                    IsNativeItemUseUuid(evidence, "itemUuid") && IsNativeUuid(evidence, "itemId") &&
                    evidence.TryGetProperty("beforeCount", out var beforeItem) && beforeItem.TryGetInt32(out int beforeMoveCount) &&
                    evidence.TryGetProperty("afterCount", out var afterItem) && afterItem.TryGetInt32(out int afterMoveCount) &&
                    beforeMoveCount > 0 && afterMoveCount == beforeMoveCount - 1;

            case Feature018HotkeyKind.AttackSquad:
                return observed.GetString() == "native_owned_formation_attack_target_transition" &&
                       evidence.TryGetProperty("slot", out JsonElement attackSlot) &&
                       attackSlot.TryGetInt32(out int actualAttackSlot) && actualAttackSlot == intent.Slot &&
                       IsNativeUuid(evidence, "marchUuid") && IsNativeUuid(evidence, "formationUuid") &&
                       IsNativeUuid(evidence, "targetUuid") &&
                       evidence.TryGetProperty("targetType", out JsonElement attackKind) &&
                       attackKind.TryGetInt32(out int nativeAttackKind) && nativeAttackKind > 0 &&
                       evidence.TryGetProperty("targetSource", out JsonElement targetSource) &&
                       targetSource.GetString() == "open_native_formation_selector";
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
                       RecallCausalityMatches(evidence) &&
                       evidence.TryGetProperty("slot", out JsonElement slot) &&
                       slot.TryGetInt32(out int actualSlot) && actualSlot == intent.Slot &&
                       evidence.TryGetProperty("target", out JsonElement target) &&
                       target.ValueKind == JsonValueKind.String &&
                       target.GetString() == "BACK_HOME" &&
                       evidence.TryGetProperty("returning", out JsonElement returning) &&
                       returning.ValueKind == JsonValueKind.True &&
                       IsNativeUuid(evidence, "marchUuid") &&
                       IsNativeUuid(evidence, "formationUuid");

            case Feature018HotkeyKind.UseShield:
                return observed.GetString() == "native_owned_shield_consumption_and_MAIN_SHIELD" &&
                       evidence.TryGetProperty("slot", out JsonElement shieldSlot) &&
                       shieldSlot.TryGetInt32(out int actualHours) && actualHours == intent.Slot &&
                       IsNativeItemUseUuid(evidence, "itemUuid") &&
                       IsNativeUuid(evidence, "itemId") &&
                       evidence.TryGetProperty("statusId", out JsonElement statusId) &&
                       statusId.TryGetInt32(out int shieldStatusId) && shieldStatusId > 0 &&
                       evidence.TryGetProperty("durationMilliseconds", out JsonElement duration) &&
                       duration.TryGetInt64(out long durationMs) &&
                       durationMs == (long)intent.Slot * 60 * 60 * 1000 &&
                       evidence.TryGetProperty("consumedCount", out JsonElement consumed) &&
                       consumed.TryGetInt32(out int used) && used == 1 &&
                       evidence.TryGetProperty("remainingMilliseconds", out JsonElement remaining) &&
                       remaining.TryGetDouble(out double remainingMs) &&
                       double.IsFinite(remainingMs) &&
                       remainingMs > 0 && remainingMs <= durationMs + 15000 &&
                       evidence.TryGetProperty("serverTimeMilliseconds", out JsonElement serverTime) &&
                       serverTime.TryGetDouble(out double shieldClock) &&
                       double.IsFinite(shieldClock) && shieldClock >= 0;

            case Feature018HotkeyKind.ReinforceFrontline:
                return observed.GetString() == "native_ParkourBattleLogic_GetMemberCount" &&
                       evidence.TryGetProperty("action", out JsonElement action) &&
                       action.ValueKind == JsonValueKind.String &&
                       action.GetString() == "frontlineReinforce" &&
                       evidence.TryGetProperty("slot", out JsonElement reinforceSlot) &&
                       reinforceSlot.TryGetInt32(out int reinforcementSlot) && reinforcementSlot == 5 &&
                       evidence.TryGetProperty("heroId", out JsonElement hero) &&
                       hero.TryGetInt32(out int heroId) && heroId > 0 &&
                       evidence.TryGetProperty("beforeCount", out JsonElement before) &&
                       before.TryGetInt32(out int beforeCount) && beforeCount >= 0 &&
                       evidence.TryGetProperty("afterCount", out JsonElement after) &&
                       after.TryGetInt32(out int afterCount) &&
                       afterCount == beforeCount + 5 &&
                       evidence.TryGetProperty("addedMembers", out JsonElement added) &&
                       added.TryGetInt32(out int addedCount) && addedCount == 5 &&
                       evidence.TryGetProperty("unitIdentityVerified", out JsonElement verified) &&
                       verified.ValueKind == JsonValueKind.True &&
                       evidence.TryGetProperty("observedCreatedUnits", out JsonElement created) &&
                       created.TryGetInt32(out int createdCount) && createdCount == 5 &&
                       evidence.TryGetProperty("unitIdentitySource", out JsonElement source) &&
                       source.ValueKind == JsonValueKind.String &&
                       source.GetString() == "native_ParkourTeam_teamUnits_guid";

            default:
                // No other kind has an audited current-client state receipt.
                return false;
        }
    }

    private static bool RecallCausalityMatches(JsonElement evidence) =>
        // Only a genuine original reply observer can establish these fields.
        // A matching cached GetMarch target, or an accepted network send
        // without matching its reply, proves neither origin nor ACK.
        evidence.TryGetProperty("requestPreviouslyDispatched", out JsonElement sent) &&
        sent.ValueKind == JsonValueKind.True &&
        evidence.TryGetProperty("nativeReturnReplyMatched", out JsonElement ack) &&
        ack.ValueKind == JsonValueKind.True;

    private static bool IsNativeUuid(JsonElement evidence, string name) =>
        evidence.TryGetProperty(name, out JsonElement field) &&
        field.ValueKind == JsonValueKind.String &&
        field.GetString() is { Length: > 0 and <= 20 } value &&
        value[0] != '0' && value.All(char.IsAsciiDigit);

    private static bool IsNativeItemUseUuid(JsonElement evidence, string name) =>
        evidence.TryGetProperty(name, out JsonElement field) &&
        field.ValueKind == JsonValueKind.String &&
        field.GetString() is { Length: > 0 and <= 128 } token &&
        token != "0" && token.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-');
}
