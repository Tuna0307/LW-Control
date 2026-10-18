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
        // The broker pins the authenticated account when a key is captured.
        // Reject malformed snapshots at the provider boundary; the shared
        // game exchange rereads the same UID/server throughout the native RPC.
        if (string.IsNullOrWhiteSpace(command.Session.ProfileId) ||
            string.IsNullOrWhiteSpace(command.Session.InstanceId) ||
            command.Session.GameUid is not { Length: > 0 and <= 20 } uid ||
            uid[0] == '0' || !uid.All(char.IsAsciiDigit) ||
            command.Session.ServerId <= 0)
        {
            return new(Feature018HotkeyActionStatus.SessionChanged,
                "native_hotkey_account_snapshot_unavailable");
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
                expectedGameUid = command.Session.GameUid,
                expectedServerId = command.Session.ServerId,
                allowRapidItem = command.AllowMarchSpeedupItems,
                allowRapidDiamond = command.AllowMarchSpeedupDiamonds,
            }, JsonOptions.Default)
            : command.Intent.Kind == Feature018HotkeyKind.ReinforceFrontline && command.ExpectedStageRunId is not null
            ? JsonSerializer.SerializeToElement(new
            {
                action = ActionName(command.Intent.Kind), slot = command.Intent.Slot,
                expectedStageRunId = command.ExpectedStageRunId,
                count = command.ReinforceCount,
                expectedGameUid = command.Session.GameUid,
                expectedServerId = command.Session.ServerId,
            }, JsonOptions.Default)
            : command.Intent.Kind == Feature018HotkeyKind.ReinforceFrontline
            ? JsonSerializer.SerializeToElement(new
            {
                action = ActionName(command.Intent.Kind), slot = command.Intent.Slot,
                count = command.ReinforceCount,
                expectedGameUid = command.Session.GameUid,
                expectedServerId = command.Session.ServerId,
            }, JsonOptions.Default)
            : JsonSerializer.SerializeToElement(new
            {
                action = ActionName(command.Intent.Kind),
                slot = command.Intent.Slot,
                expectedGameUid = command.Session.GameUid,
                expectedServerId = command.Session.ServerId,
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
            // MarchUtil.OnClickStartMarch can open the game's original
            // formation chooser asynchronously without posting an RPC.
            // Continue ONLY this exact captured Q/W/E/R intent with bounded,
            // read-only status requests; the original hover/click must never
            // be issued again by the provider.
            if (command.Intent.Kind == Feature018HotkeyKind.AttackSquad &&
                nativeAcceptedOnly && !dispatchedNativeRequest &&
                evidence.TryGetProperty("awaitingNativeChooser", out var awaitingChooser) &&
                awaitingChooser.ValueKind == JsonValueKind.True)
            {
                if (!IsAttackChooserReceipt(command, evidence, null) ||
                    !evidence.TryGetProperty("chooserRequestMade", out var chooserMade) ||
                    chooserMade.ValueKind != JsonValueKind.True ||
                    !evidence.TryGetProperty("requestDispatched", out var notSent) ||
                    notSent.ValueKind != JsonValueKind.False)
                    return new(Feature018HotkeyActionStatus.Failed,
                        "native_attack_chooser_identity_unverified");
                return await PollNativeAttackChooserAsync(command,
                    evidence.GetProperty("formationUuid").GetString()!, cancellationToken)
                    .ConfigureAwait(false);
            }
            if (command.Intent.Kind == Feature018HotkeyKind.AllianceRelocate &&
                nativeAcceptedOnly && !dispatchedNativeRequest &&
                IsF10ChooserReceipt(command, evidence, null) &&
                evidence.TryGetProperty("chooserRequestMade", out var f10ChooserMade) &&
                f10ChooserMade.ValueKind == JsonValueKind.True)
            {
                return await PollNativeF10ChooserAsync(command,
                    evidence.GetProperty("itemUuid").GetString()!, cancellationToken)
                    .ConfigureAwait(false);
            }
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
                ConfirmedReceiptMatches(command.Intent, evidence, command.Session, command.ReinforceCount))
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
                    if (command.Intent.Kind == Feature018HotkeyKind.AllianceRelocate &&
                        IsF10ChooserReceipt(command, evidence, null))
                        return await PollNativeF10ChooserAsync(command,
                            evidence.GetProperty("itemUuid").GetString()!, cancellationToken)
                            .ConfigureAwait(false);
                    string? uuidProperty = command.Intent.Kind switch
                    {
                        Feature018HotkeyKind.RecallSquad => "marchUuid",
                        Feature018HotkeyKind.AttackSquad => "formationUuid",
                        Feature018HotkeyKind.UseShield => "itemUuid",
                        Feature018HotkeyKind.ApplyEquipmentPreset => "presetId",
                        Feature018HotkeyKind.RandomRelocate => "itemUuid",
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
                recallObservation.GetString() == "native_world_march_return_reply_and_target" &&
                !RecallCausalityMatches(evidence))
            {
                // The previous native client returned this same target when
                // already BACK_HOME, without dispatching anything. A cached
                // state alone is never a successful key-to-RPC/ACK receipt.
                return new(Feature018HotkeyActionStatus.Failed,
                    "native_recall_back_home_state_lacks_attributed_send_and_reply");
            }
            if (!ConfirmedReceiptMatches(command.Intent, evidence, command.Session, command.ReinforceCount))
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
        catch (BridgeCommandException error) when (
            error.Code is "GAME_CONNECTION_UNAVAILABLE" or "GAME_ACCOUNT_NOT_AUTHORIZED" or
                "GAME_ACCOUNT_CHANGED")
        {
            return new(Feature018HotkeyActionStatus.SessionChanged,
                "authenticated_game_route_unavailable");
        }
        catch (BridgeCommandException error) when (command.Intent.Kind == Feature018HotkeyKind.ReinforceFrontline)
        {
            return new(Feature018HotkeyActionStatus.Failed, error.Message,
                error.Details is JsonElement data ? data.Clone() : null);
        }
        catch (BridgeCommandException error)
        {
            // The game exchange preserves the native failure code/message
            // (including state=failed Lua errors). Dropping both here left
            // captured Q/W/E/R failures with one indistinguishable reason
            // and no way to identify the blocked native method. This is
            // diagnostic evidence only: failure must never become Confirmed.
            return new(Feature018HotkeyActionStatus.Failed,
                NativeRejectionReason("current_client_game_action_rejected", error),
                NativeRejectionDetails(error));
        }
    }

    private static string NativeRejectionReason(string prefix, BridgeCommandException error)
    {
        // Retain the original code and game error for the desktop outcome,
        // but bound and single-line untrusted native text in the UI/log.
        static string OneLine(string? value, string fallback, int limit)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            string clean = new(value.Select(c => char.IsControl(c) ? ' ' : c).ToArray());
            return clean.Length <= limit ? clean : clean[..limit];
        }
        return prefix + " [" + OneLine(error.Code, "UNKNOWN", 128) + "]: " +
            OneLine(error.Message, "No native failure message supplied", 1024);
    }

    private static JsonElement? NativeRejectionDetails(BridgeCommandException error) =>
        error.Details is JsonElement { ValueKind: JsonValueKind.Object } details
            ? details.Clone() : null;

    private static bool IsAttackChooserReceipt(
        Feature018HotkeyCommand command, JsonElement data, string? formationUuid)
    {
        if (command.Intent.Kind != Feature018HotkeyKind.AttackSquad ||
            command.Intent.Slot is < 1 or > 4 ||
            !data.TryGetProperty("slot", out var slot) ||
            !slot.TryGetInt32(out int actualSlot) || actualSlot != command.Intent.Slot ||
            !IsNativeUuid(data, "formationUuid") ||
            (formationUuid is not null &&
             data.GetProperty("formationUuid").GetString() != formationUuid) ||
            (data.TryGetProperty("action", out var action) &&
             (action.ValueKind != JsonValueKind.String || action.GetString() != "attack")) ||
            !data.TryGetProperty("ownerUid", out var owner) ||
            owner.ValueKind != JsonValueKind.String ||
            owner.GetString() != command.Session.GameUid ||
            !data.TryGetProperty("sourceServerId", out var server) ||
            !server.TryGetInt32(out int serverId) ||
            serverId != command.Session.ServerId)
            return false;
        return true;
    }

    private static bool IsF10ChooserReceipt(
        Feature018HotkeyCommand command, JsonElement data, string? itemUuid)
    {
        if (command.Intent.Kind != Feature018HotkeyKind.AllianceRelocate ||
            command.Intent.Slot != 0 ||
            !data.TryGetProperty("slot", out var slot) ||
            !slot.TryGetInt32(out int actualSlot) || actualSlot != 0 ||
            !data.TryGetProperty("action", out var action) ||
            action.ValueKind != JsonValueKind.String || action.GetString() != "allianceRelocate" ||
            !data.TryGetProperty("ownerUid", out var owner) ||
            owner.ValueKind != JsonValueKind.String || owner.GetString() != command.Session.GameUid ||
            !data.TryGetProperty("sourceServerId", out var server) ||
            !server.TryGetInt32(out int serverId) || serverId != command.Session.ServerId ||
            !IsNativeItemUseUuid(data, "itemUuid") ||
            (itemUuid is not null && data.GetProperty("itemUuid").GetString() != itemUuid) ||
            !data.TryGetProperty("chooserRequestMade", out var chooser) ||
            chooser.ValueKind != JsonValueKind.True ||
            !data.TryGetProperty("ownerSelectionRequired", out var selection) ||
            selection.ValueKind != JsonValueKind.True)
            return false;
        return true;
    }

    private static bool AttackHasExactlyOneNativeRpc(JsonElement data, string formationUuid)
    {
        if (!data.TryGetProperty("nativeTransaction", out var transaction) ||
            transaction.ValueKind != JsonValueKind.Object ||
            !transaction.TryGetProperty("sendCount", out var count) ||
            !count.TryGetInt32(out int sends) || sends != 1 ||
            !transaction.TryGetProperty("outboundRoute", out var route) ||
            route.ValueKind != JsonValueKind.String ||
            route.GetString() != "WorldMarchFormationNew" ||
            !transaction.TryGetProperty("senderReturned", out var senderReturned) ||
            senderReturned.ValueKind != JsonValueKind.True ||
            (transaction.TryGetProperty("senderReturnedFalse", out var returnedFalse) &&
             returnedFalse.ValueKind != JsonValueKind.False) ||
            (transaction.TryGetProperty("senderError", out var senderError) &&
             senderError.ValueKind != JsonValueKind.Null) ||
            (transaction.TryGetProperty("unattributedMatchingSendObserved", out var unattributed) &&
             unattributed.ValueKind != JsonValueKind.False) ||
            (transaction.TryGetProperty("unattributedSendCount", out var extra) &&
             (!extra.TryGetInt32(out int extras) || extras != 0)) ||
            !transaction.TryGetProperty("formation", out var formation) ||
            formation.ValueKind != JsonValueKind.String ||
            formation.GetString() != formationUuid ||
            (transaction.TryGetProperty("outboundFormationOrMarch", out var transmittedFormation) &&
             (transmittedFormation.ValueKind != JsonValueKind.String ||
              transmittedFormation.GetString() != formationUuid)))
            return false;
        return true;
    }

    private async Task<Feature018HotkeyActionOutcome> PollNativeAttackChooserAsync(
        Feature018HotkeyCommand command, string formationUuid,
        CancellationToken cancellationToken)
    {
        // This method does not capture or synthesize a physical key press.
        // The parent action broker admitted that original key and pins its
        // session. Each query is separately fenced by the game exchange
        // against the same native UID/server; never include speedup consent.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(3));
        JsonElement query = JsonSerializer.SerializeToElement(new
        {
            action = "attack", slot = command.Intent.Slot, queryOnly = true,
            authorizedUid = command.Session.GameUid,
            expectedGameUid = command.Session.GameUid,
            expectedServerId = command.Session.ServerId,
        }, JsonOptions.Default);
        try
        {
            for (int poll = 0; poll < 10; poll++)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), deadline.Token)
                    .ConfigureAwait(false);
                JsonElement response;
                try
                {
                    object? raw = await gameCommands.InvokeAsync(
                        CommandName, query, deadline.Token).ConfigureAwait(false);
                    if (raw is not JsonElement payload || payload.ValueKind != JsonValueKind.Object)
                        return new(Feature018HotkeyActionStatus.Failed,
                            "native_attack_chooser_followup_receipt_missing");
                    response = payload;
                }
                catch (BridgeCommandException error) when (
                    error.Code is "GAME_CONNECTION_UNAVAILABLE" or "GAME_ACCOUNT_NOT_AUTHORIZED" or
                    "GAME_ACCOUNT_CHANGED")
                {
                    return new(Feature018HotkeyActionStatus.SessionChanged,
                        "native_attack_chooser_authenticated_session_changed");
                }
                catch (BridgeCommandException error) when (error.Code == "GAME_ACTION_UNCONFIRMED")
                {
                    // A status query cannot authorize another click or
                    // retransmit merely because the chooser is still open.
                    continue;
                }
                catch (BridgeCommandException error)
                {
                    return new(Feature018HotkeyActionStatus.Submitted,
                        NativeRejectionReason("native_attack_chooser_followup_unavailable", error),
                        NativeRejectionDetails(error));
                }

                if (!IsAttackChooserReceipt(command, response, formationUuid))
                    return new(Feature018HotkeyActionStatus.SessionChanged,
                        "native_attack_chooser_identity_changed");
                bool sent = response.TryGetProperty("requestDispatched", out var dispatched) &&
                            dispatched.ValueKind == JsonValueKind.True;
                bool exactRpc = AttackHasExactlyOneNativeRpc(response, formationUuid);
                if (sent && !exactRpc)
                    return new(Feature018HotkeyActionStatus.Failed,
                        "native_attack_chooser_rpc_count_unverified");
                if (exactRpc)
                {
                    // Native Lua may report a poststate receipt with
                    // requestDispatched=false because *this poll* is read-only.
                    // Exact original transaction evidence is required before
                    // interpreting either a completed or pending march.
                    bool awaiting = response.TryGetProperty("awaitingGameConfirmation", out var waiting) &&
                                     waiting.ValueKind == JsonValueKind.True;
                    if (!awaiting &&
                        ConfirmedReceiptMatches(command.Intent, response,
                            command.Session, command.ReinforceCount))
                        return new(Feature018HotkeyActionStatus.Confirmed,
                            "verified_native_game_state_observation", response.Clone());
                    return await PollAfterSendAsync(command, "formationUuid",
                        formationUuid, cancellationToken).ConfigureAwait(false);
                }
                if (response.TryGetProperty("chooserRequestMade", out var chooserMade) &&
                    chooserMade.ValueKind == JsonValueKind.False)
                    return new(Feature018HotkeyActionStatus.Failed,
                        "native_attack_chooser_closed_without_verified_send");
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(Feature018HotkeyActionStatus.Submitted,
                "native_attack_chooser_pending_no_verified_game_rpc");
        }
        return new(Feature018HotkeyActionStatus.Submitted,
            "native_attack_chooser_pending_no_verified_game_rpc");
    }

    private async Task<Feature018HotkeyActionOutcome> PollNativeF10ChooserAsync(
        Feature018HotkeyCommand command, string itemUuid,
        CancellationToken cancellationToken)
    {
        // F10 itself only opens the original chooser. The owner performs the
        // native selection; these follow-ups are read-only and may observe the
        // exact selected AllianceMoveCity send plus its reason-240 reply. They
        // never reopen the chooser or synthesize a second relocation request.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(60));
        JsonElement query = JsonSerializer.SerializeToElement(new
        {
            action = "allianceRelocate", slot = 0, queryOnly = true,
            expectedGameUid = command.Session.GameUid,
            expectedServerId = command.Session.ServerId,
        }, JsonOptions.Default);
        try
        {
            for (int poll = 0; poll < 240; poll++)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), deadline.Token)
                    .ConfigureAwait(false);
                JsonElement response;
                try
                {
                    object? raw = await gameCommands.InvokeAsync(
                        CommandName, query, deadline.Token).ConfigureAwait(false);
                    if (raw is not JsonElement payload || payload.ValueKind != JsonValueKind.Object)
                        return new(Feature018HotkeyActionStatus.Failed,
                            "native_f10_chooser_followup_receipt_missing");
                    response = payload;
                }
                catch (BridgeCommandException error) when (
                    error.Code is "GAME_CONNECTION_UNAVAILABLE" or "GAME_ACCOUNT_NOT_AUTHORIZED" or
                        "GAME_ACCOUNT_CHANGED")
                {
                    return new(Feature018HotkeyActionStatus.SessionChanged,
                        "native_f10_authenticated_session_changed");
                }
                catch (BridgeCommandException error) when (error.Code == "GAME_ACTION_UNCONFIRMED")
                {
                    continue;
                }
                catch (BridgeCommandException error)
                {
                    return new(Feature018HotkeyActionStatus.Submitted,
                        NativeRejectionReason("native_f10_followup_unavailable", error),
                        NativeRejectionDetails(error));
                }

                if (!IsF10ChooserReceipt(command, response, itemUuid))
                    return new(Feature018HotkeyActionStatus.SessionChanged,
                        "native_f10_chooser_identity_changed");
                bool awaiting = response.TryGetProperty("awaitingGameConfirmation", out var waiting) &&
                                waiting.ValueKind == JsonValueKind.True;
                if (!awaiting && ConfirmedReceiptMatches(command.Intent, response,
                        command.Session, command.ReinforceCount))
                    return new(Feature018HotkeyActionStatus.Confirmed,
                        "verified_native_game_state_observation", response.Clone());

                bool selected = response.TryGetProperty("ownerSelectedRallyObserved", out var chosen) &&
                                chosen.ValueKind == JsonValueKind.True;
                bool dispatched = response.TryGetProperty("requestDispatched", out var sent) &&
                                  sent.ValueKind == JsonValueKind.True;
                if (selected && !dispatched)
                {
                    bool uncertain = response.TryGetProperty("nativeSendOutcomeUncertain", out var unsure) &&
                                     unsure.ValueKind == JsonValueKind.True;
                    return new(Feature018HotkeyActionStatus.Submitted,
                        uncertain ? "native_f10_selection_send_outcome_unconfirmed" :
                            "native_f10_owner_selection_not_alliance_move", response.Clone());
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(Feature018HotkeyActionStatus.Submitted,
                "native_f10_owner_selection_or_ack_pending");
        }
        return new(Feature018HotkeyActionStatus.Submitted,
            "native_f10_owner_selection_or_ack_pending");
    }

    private static bool F9ReplyIdentityMatches(JsonElement evidence)
    {
        bool exact = evidence.TryGetProperty("replyExactItemUuidObserved", out var exactStack) &&
                     exactStack.ValueKind == JsonValueKind.True;
        bool fallback = evidence.TryGetProperty("replyItemIdFallbackObserved", out var itemIdFallback) &&
                        itemIdFallback.ValueKind == JsonValueKind.True;
        return exact != fallback &&
            evidence.TryGetProperty("replyItemIdentityCorrelated", out var correlated) &&
            correlated.ValueKind == JsonValueKind.True;
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
                queryOnly = true, authorizedUid = command.Session.GameUid,
                expectedGameUid = command.Session.GameUid,
                expectedServerId = command.Session.ServerId }, JsonOptions.Default)
            : JsonSerializer.SerializeToElement(new
            { action = ActionName(command.Intent.Kind), slot = command.Intent.Slot, queryOnly = true,
              expectedGameUid = command.Session.GameUid,
              expectedServerId = command.Session.ServerId }, JsonOptions.Default);
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
                        (command.Intent.Kind != Feature018HotkeyKind.AttackSquad ||
                         IsAttackChooserReceipt(command, gameState, sentUuid)) &&
                        !(gameState.TryGetProperty("awaitingGameConfirmation", out var stillPending) &&
                          stillPending.ValueKind == JsonValueKind.True) &&
                        ConfirmedReceiptMatches(command.Intent, gameState, command.Session, command.ReinforceCount))
                    {
                        if (gameState.GetProperty(uuidProperty).GetString() != sentUuid)
                            return new(Feature018HotkeyActionStatus.SessionChanged,
                                "native_action_target_changed_during_confirmation");
                        return new(Feature018HotkeyActionStatus.Confirmed,
                            "verified_native_game_state_observation", gameState.Clone());
                    }
                }
                catch (BridgeCommandException error) when (
                    error.Code is "GAME_CONNECTION_UNAVAILABLE" or "GAME_ACCOUNT_NOT_AUTHORIZED" or
                        "GAME_ACCOUNT_CHANGED")
                {
                    return new(Feature018HotkeyActionStatus.SessionChanged,
                        "authenticated_game_route_unavailable");
                }
                catch (BridgeCommandException error) when (error.Code == "GAME_ACTION_UNCONFIRMED")
                {
                    // The query may legitimately observe the pre-ACK march.
                    // An unconfirmed query is never a reason to send again.
                }
                catch (BridgeCommandException error)
                {
                    return new(Feature018HotkeyActionStatus.Submitted,
                        NativeRejectionReason("native_action_dispatched_followup_unavailable", error),
                        NativeRejectionDetails(error));
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
        Feature018HotkeyIntent intent, JsonElement evidence,
        Feature018HotkeySession session, int reinforceCount = 5)
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
                return observed.GetString() == "native_relocation_reply_item_and_world_position" &&
                    evidence.TryGetProperty("awaitingGameConfirmation", out var movePending) && movePending.ValueKind == JsonValueKind.False &&
                    evidence.TryGetProperty("action", out var movedAction) &&
                    movedAction.ValueKind == JsonValueKind.String && movedAction.GetString() == ActionName(intent.Kind) &&
                    evidence.TryGetProperty("requestDispatched", out var relocationSent) && relocationSent.ValueKind == JsonValueKind.True &&
                    evidence.TryGetProperty("relocationConfirmed", out var relocated) && relocated.ValueKind == JsonValueKind.True &&
                    evidence.TryGetProperty("replyObserved", out var reply) && reply.ValueKind == JsonValueKind.True &&
                    // v26 ItemUseHandle accepts either the exact opaque stack
                    // UUID or its original itemId fallback when response.uuid
                    // is genuinely absent. The Lua one-send/owner/poststate
                    // fence distinguishes that fallback from a wrong UUID.
                    F9ReplyIdentityMatches(evidence) &&
                    evidence.TryGetProperty("replyUseOperationCompatible", out var normalUse) && normalUse.ValueKind == JsonValueKind.True &&
                    evidence.TryGetProperty("independentWorldAndInventoryPoststateVerified", out var independentAfter) && independentAfter.ValueKind == JsonValueKind.True &&
                    evidence.TryGetProperty("worldPointChanged", out var moved) && moved.ValueKind == JsonValueKind.True &&
                    evidence.TryGetProperty("ownerUid", out var moveOwner) &&
                    moveOwner.ValueKind == JsonValueKind.String && moveOwner.GetString() == session.GameUid &&
                    evidence.TryGetProperty("sourceServerId", out var moveServer) && moveServer.TryGetInt32(out int moveServerId) &&
                    moveServerId == session.ServerId &&
                    evidence.TryGetProperty("beforeWorldPointId", out var beforePoint) && beforePoint.TryGetInt64(out long oldPoint) &&
                    evidence.TryGetProperty("afterWorldPointId", out var afterPoint) && afterPoint.TryGetInt64(out long newPoint) &&
                    oldPoint is >= 0 and < 10000000 && newPoint is >= 0 and < 10000000 && oldPoint != newPoint &&
                    IsNativeItemUseUuid(evidence, "itemUuid") && IsNativeUuid(evidence, "itemId") &&
                    evidence.TryGetProperty("beforeCount", out var beforeItem) && beforeItem.TryGetInt32(out int beforeMoveCount) &&
                    evidence.TryGetProperty("afterCount", out var afterItem) && afterItem.TryGetInt32(out int afterMoveCount) &&
                    beforeMoveCount > 0 && afterMoveCount == beforeMoveCount - 1;

            case Feature018HotkeyKind.AllianceRelocate:
                return observed.GetString() == "native_alliance_relocation_reply_world_position" &&
                    evidence.TryGetProperty("awaitingGameConfirmation", out var f10Pending) && f10Pending.ValueKind == JsonValueKind.False &&
                    evidence.TryGetProperty("action", out var f10Action) &&
                    f10Action.ValueKind == JsonValueKind.String && f10Action.GetString() == "allianceRelocate" &&
                    evidence.TryGetProperty("requestDispatched", out var f10Sent) && f10Sent.ValueKind == JsonValueKind.True &&
                    evidence.TryGetProperty("relocationConfirmed", out var f10Confirmed) && f10Confirmed.ValueKind == JsonValueKind.True &&
                    evidence.TryGetProperty("ownerSelectedRemoteRallyObserved", out var remoteSelected) && remoteSelected.ValueKind == JsonValueKind.True &&
                    evidence.TryGetProperty("allianceMoveRequestObserved", out var requestObserved) && requestObserved.ValueKind == JsonValueKind.True &&
                    evidence.TryGetProperty("allianceMoveSendCount", out var sendCount) && sendCount.TryGetInt32(out int sends) && sends == 1 &&
                    evidence.TryGetProperty("allianceMoveReplyObserved", out var replyObserved) && replyObserved.ValueKind == JsonValueKind.True &&
                    evidence.TryGetProperty("worldMvReason240Observed", out var reason240) && reason240.ValueKind == JsonValueKind.True &&
                    evidence.TryGetProperty("replyResultOneObserved", out var resultOne) && resultOne.ValueKind == JsonValueKind.True &&
                    evidence.TryGetProperty("serverAckAttributableByOneSendFence", out var attributed) && attributed.ValueKind == JsonValueKind.True &&
                    evidence.TryGetProperty("independentWorldAndInventoryPoststateVerified", out var f10Poststate) && f10Poststate.ValueKind == JsonValueKind.True &&
                    evidence.TryGetProperty("worldPointChanged", out var f10Moved) && f10Moved.ValueKind == JsonValueKind.True &&
                    evidence.TryGetProperty("ownerUid", out var f10Owner) &&
                    f10Owner.ValueKind == JsonValueKind.String && f10Owner.GetString() == session.GameUid &&
                    evidence.TryGetProperty("sourceServerId", out var f10Source) && f10Source.TryGetInt32(out int f10SourceServer) &&
                    f10SourceServer == session.ServerId &&
                    evidence.TryGetProperty("selectedRallyServerId", out var selectedServer) && selectedServer.TryGetInt32(out int selectedServerId) && selectedServerId > 0 &&
                    evidence.TryGetProperty("replyServerId", out var replyServer) && replyServer.TryGetInt32(out int replyServerId) && replyServerId == selectedServerId &&
                    evidence.TryGetProperty("beforeWorldPointId", out var f10Before) && f10Before.TryGetInt64(out long f10OldPoint) &&
                    evidence.TryGetProperty("afterWorldPointId", out var f10After) && f10After.TryGetInt64(out long f10NewPoint) &&
                    f10OldPoint is >= 0 and < 10000000 && f10NewPoint is >= 0 and < 10000000 && f10OldPoint != f10NewPoint &&
                    evidence.TryGetProperty("acknowledgedWorldPointId", out var acknowledgedPoint) && acknowledgedPoint.TryGetInt64(out long ackPoint) && ackPoint == f10NewPoint &&
                    IsNativeItemUseUuid(evidence, "itemUuid") && IsNativeUuid(evidence, "itemId") &&
                    evidence.TryGetProperty("beforeCount", out var f10BeforeItem) && f10BeforeItem.TryGetInt32(out int f10BeforeCount) &&
                    evidence.TryGetProperty("afterCount", out var f10AfterItem) && f10AfterItem.TryGetInt32(out int f10AfterCount) &&
                    f10BeforeCount > 0 && f10AfterCount == f10BeforeCount - 1;

            case Feature018HotkeyKind.AttackSquad:
                return observed.GetString() == "native_owned_formation_attack_target_transition" &&
                       evidence.TryGetProperty("slot", out JsonElement attackSlot) &&
                       attackSlot.TryGetInt32(out int actualAttackSlot) && actualAttackSlot == intent.Slot &&
                       IsNativeUuid(evidence, "marchUuid") && IsNativeUuid(evidence, "formationUuid") &&
                       IsNativeUuid(evidence, "targetUuid") &&
                       evidence.TryGetProperty("targetType", out JsonElement attackKind) &&
                       attackKind.TryGetInt32(out int nativeAttackKind) && nativeAttackKind > 0 &&
                       evidence.TryGetProperty("targetSource", out JsonElement targetSource) &&
                       targetSource.ValueKind == JsonValueKind.String &&
                       (targetSource.GetString() == "open_native_formation_selector" ||
                        (targetSource.GetString() == "native_physical_hover_original_selector" &&
                         evidence.TryGetProperty("currentCursorVerified", out var hoverVerified) &&
                         hoverVerified.ValueKind == JsonValueKind.True &&
                         evidence.TryGetProperty("nativeMarchGuidMatchesRequest", out var nativeGuid) &&
                         nativeGuid.ValueKind == JsonValueKind.True &&
                         evidence.TryGetProperty("serverAcknowledgementCorrelated", out var serverAck) &&
                         serverAck.ValueKind == JsonValueKind.True &&
                         evidence.TryGetProperty("nativeMarchPostStateVerified", out var hoverPoststate) &&
                         hoverPoststate.ValueKind == JsonValueKind.True &&
                         evidence.TryGetProperty("ownerUid", out var attackOwner) &&
                         attackOwner.ValueKind == JsonValueKind.String &&
                         attackOwner.GetString() == session.GameUid &&
                         evidence.TryGetProperty("sourceServerId", out var attackServer) &&
                         attackServer.TryGetInt32(out int actualAttackServer) &&
                         actualAttackServer == session.ServerId &&
                         AttackHasExactlyOneNativeRpc(evidence,
                             evidence.GetProperty("formationUuid").GetString()!)));
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
                bool recallProof =
                    observed.GetString() == "native_world_march_return_reply_and_target"
                        ? RecallCausalityMatches(evidence)
                        : observed.GetString() == "native_world_march_return_server_applied_poststate" &&
                          RecallServerAppliedPostStateMatches(evidence);
                return recallProof &&
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
                return observed.GetString() == "native_ParkourTeam_GetRemainUnitCount" &&
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
                       afterCount == beforeCount + reinforceCount &&
                       evidence.TryGetProperty("addedMembers", out JsonElement added) &&
                       added.TryGetInt32(out int addedCount) && addedCount == reinforceCount &&
                       evidence.TryGetProperty("unitIdentityVerified", out JsonElement verified) &&
                       verified.ValueKind == JsonValueKind.True &&
                       evidence.TryGetProperty("observedCreatedUnits", out JsonElement created) &&
                       created.TryGetInt32(out int createdCount) && createdCount >= 0 && createdCount <= reinforceCount &&
                       evidence.TryGetProperty("reservedMembers", out JsonElement reserve) &&
                       reserve.TryGetInt32(out int reservedCount) && reservedCount >= 0 && reservedCount <= reinforceCount &&
                       createdCount + reservedCount == reinforceCount &&
                       evidence.TryGetProperty("activeMemberCountBefore", out JsonElement activeBefore) &&
                       activeBefore.TryGetInt32(out int activeMembersBefore) && activeMembersBefore >= 0 &&
                       evidence.TryGetProperty("activeMemberCountAfter", out JsonElement activeAfter) &&
                       activeAfter.TryGetInt32(out int activeMembersAfter) &&
                       (long)activeMembersAfter == (long)activeMembersBefore + createdCount &&
                       evidence.TryGetProperty("reserveMemberCountBefore", out JsonElement reserveBefore) &&
                       reserveBefore.TryGetInt32(out int reservedMembersBefore) && reservedMembersBefore >= 0 &&
                       evidence.TryGetProperty("reserveMemberCountAfter", out JsonElement reserveAfter) &&
                       reserveAfter.TryGetInt32(out int reservedMembersAfter) &&
                       (long)reservedMembersAfter == (long)reservedMembersBefore + reservedCount &&
                       (long)beforeCount == (long)activeMembersBefore + reservedMembersBefore &&
                       (long)afterCount == (long)activeMembersAfter + reservedMembersAfter &&
                       evidence.TryGetProperty("nativeReserveVerified", out JsonElement reserveVerified) &&
                       reserveVerified.ValueKind == JsonValueKind.True &&
                       evidence.TryGetProperty("reserveIdentitySource", out JsonElement reserveSource) &&
                       reserveSource.ValueKind == JsonValueKind.String &&
                       reserveSource.GetString() == "native_ParkourTeam_overflowUnitCount" &&
                       evidence.TryGetProperty("guidAllocatorBefore", out JsonElement guidBefore) &&
                       guidBefore.TryGetInt64(out long allocatorBefore) && allocatorBefore > 0 &&
                       evidence.TryGetProperty("guidAllocatorAfter", out JsonElement guidAfter) &&
                       guidAfter.TryGetInt64(out long allocatorAfter) &&
                       allocatorAfter == allocatorBefore + createdCount &&
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

    private static bool RecallServerAppliedPostStateMatches(JsonElement evidence) =>
        evidence.TryGetProperty("requestPreviouslyDispatched", out JsonElement sent) &&
        sent.ValueKind == JsonValueKind.True &&
        evidence.TryGetProperty("nativePostStateObserved", out JsonElement poststate) &&
        poststate.ValueKind == JsonValueKind.True &&
        evidence.TryGetProperty("serverAppliedPostStateObserved", out JsonElement applied) &&
        applied.ValueKind == JsonValueKind.True &&
        evidence.TryGetProperty("priorTargetWasBackHome", out JsonElement alreadyReturning) &&
        alreadyReturning.ValueKind == JsonValueKind.False &&
        evidence.TryGetProperty("authoritativePostStateSource", out JsonElement source) &&
        source.ValueKind == JsonValueKind.String &&
        source.GetString() == "WorldMarch.GetMarchTargetType_after_dispatch" &&
        evidence.TryGetProperty("serverAcknowledgementCorrelated", out JsonElement ack) &&
        ack.ValueKind == JsonValueKind.False;

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
