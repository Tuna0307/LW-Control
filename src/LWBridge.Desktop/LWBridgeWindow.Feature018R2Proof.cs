using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace LWBridge.Desktop;

internal sealed partial class LWBridgeWindow
{
    // LEAD018-R2: each observation is an actual game-producer response tied to
    // the profile/session/PID/challenge. Keep failures in the receipt instead
    // of translating dispatched network messages into completed gameplay.
    private async Task RunFeature018R2PackagedUiProofAsync(CoreWebView2 core,
        string outputPath, string directory, OverviewMapScanSession owner,
        string uid, int serverId, CancellationToken token)
    {
        OverviewLifecycleService lifecycle = overviewLifecycleService
            ?? throw new InvalidOperationException("FEATURE018 lifecycle missing");
        HotkeyConfigCommandService prefs = hotkeyConfigService
            ?? throw new InvalidOperationException("FEATURE018 hotkey settings missing");
        var game = equipmentConfigService is null
            ? new Feature018GameCommandService(lifecycle)
            : new Feature018GameCommandService(lifecycle, equipmentConfigService);
        var approvedAccount = new Feature018HotkeySession(owner.ProfileId,
            owner.SessionId, uid, serverId);
        var evidence = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schemaVersion"] = 2,
            ["state"] = "partial",
            ["game"] = new { owner.ProfileId, owner.SessionId, owner.GamePid, uid, serverId,
                heartbeatVerified = true },
            ["startedAtUtc"] = DateTimeOffset.UtcNow,
        };
        using JsonDocument empty = JsonDocument.Parse("{}");
        HotkeyConfig? originalHotkeys = null;
        bool ApprovedOwner() =>
            Feature018IsApprovedAccount(uid, serverId) &&
            lifecycle.MatchesOwnedMapScanSession(owner) &&
            ReadFeature018AuthenticatedAccount(lifecycle, owner) == approvedAccount;
        void Owned()
        {
            token.ThrowIfCancellationRequested();
            if (!ApprovedOwner())
                throw new InvalidDataException(
                    "FEATURE018 R2 game session, UID or server left the approved sub-account.");
        }
        async Task Save()
        {
            evidence["lastObservedAtUtc"] = DateTimeOffset.UtcNow;
            await File.WriteAllTextAsync(outputPath,
                JsonSerializer.Serialize(evidence, new JsonSerializerOptions(JsonOptions.Default)
                { WriteIndented = true }), token);
        }
        async Task<JsonElement?> Command(string command, object? payload = null,
            string? receiptKey = null)
        {
            Owned();
            string key = receiptKey ?? command;
            try
            {
                JsonElement argument = payload is null ? empty.RootElement.Clone() :
                    JsonSerializer.SerializeToElement(payload, JsonOptions.Default);
                object? raw = await game.InvokeAsync(command, argument, token);
                Owned();
                if (raw is not JsonElement response)
                    throw new InvalidDataException("Native result is not a game JSON observation");
                evidence[key] = new { state = "returned", response };
                await Save();
                return response.Clone();
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                evidence[key] = new { state = "failed", type = error.GetType().Name,
                    message = error.Message,
                    code = error is BridgeCommandException native ? native.Code : null };
                await Save();
                return null;
            }
        }
        async Task Capture(string name)
        {
            Owned();
            await using var file = File.Create(Path.Combine(directory, name));
            await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, file);
        }
        async Task Nav(int index, string selector, string name)
        {
            Owned();
            if (await core.ExecuteScriptAsync($"(()=>{{const b=document.querySelectorAll('.side-nav button')[{index}];if(!b)return false;b.click();return true;}})()") != "true")
                throw new InvalidDataException($"Original {name} navigation button is missing");
            for (int i = 0; i < 100; i++)
            {
                Owned();
                if (await core.ExecuteScriptAsync($"!!document.querySelector('{selector}')") == "true") return;
                await Task.Delay(100, token);
            }
            throw new InvalidDataException($"Original {name} panel failed to render");
        }
        if (Environment.GetEnvironmentVariable("LWBRIDGE_FEATURE018_R22_EQUIPMENT_PROOF") == "1")
        {
            await RunFeature018R22EquipmentTabProofAsync(core, outputPath, directory,
                owner, uid, serverId, token);
            return;
        }
        if (Environment.GetEnvironmentVariable("LWBRIDGE_FEATURE018_R22_CITY_IMAGE_PROOF") == "1")
        {
            await RunFeature018R22CityImageProofAsync(core, outputPath, directory,
                owner, uid, serverId, token);
            return;
        }
        try
        {
            await Nav(4, ".city-layout-panel, .city-layout-empty", "City Layout");
            JsonElement? before = await Command("city_layout_snapshot_get", receiptKey: "citySnapshotBefore");
            await Command("city_layout_apply_status", receiptKey: "cityApplyBefore");
            // Exercise the actual mounted React City workbench against its
            // asynchronously loaded native data. Keep visual/UI observations
            // distinct from the native validation and building ACK receipts.
            for (int i = 0; i < 50; i++)
            {
                Owned();
                if (await core.ExecuteScriptAsync(
                        "document.querySelectorAll('.city-layout-grid .city-layout-cell').length")
                    == "10000") break;
                await Task.Delay(100, token);
            }
            const string CityMountedMetrics = "(()=>{const p=document.querySelector('.city-layout-panel');const g=p?.querySelector('.city-layout-grid');const z=p?.querySelector('.city-layout-zoom output');return {livePanel:!!p&&!p.hasAttribute('data-preview-fixture'),cellCount:g?.querySelectorAll('.city-layout-cell').length??0,buildingCount:g?.querySelectorAll('[data-city-building-id]').length??0,zoom:z?.textContent??null,selectedCount:g?.querySelectorAll('.city-layout-building.selected').length??0};})()";
            evidence["cityMountedBefore"] = JsonDocument.Parse(
                await core.ExecuteScriptAsync(CityMountedMetrics)).RootElement.Clone();
            if (before is { } uiCity &&
                uiCity.TryGetProperty("cells", out var uiCells) &&
                uiCells.ValueKind == JsonValueKind.Array &&
                uiCity.TryGetProperty("buildings", out var uiBuildings) &&
                uiBuildings.ValueKind == JsonValueKind.Array &&
                evidence["cityMountedBefore"] is JsonElement mountedCity)
            {
                evidence["cityMountedCountsMatchNative"] =
                    mountedCity.GetProperty("livePanel").GetBoolean() &&
                    mountedCity.GetProperty("cellCount").GetInt32() == uiCells.GetArrayLength() &&
                    mountedCity.GetProperty("buildingCount").GetInt32() == uiBuildings.GetArrayLength();
                if (mountedCity.GetProperty("livePanel").GetBoolean())
                {
                    bool zoomed = await core.ExecuteScriptAsync(
                        "(()=>{const b=document.querySelector('.city-layout-zoom button:nth-child(3)');if(!b||b.disabled)return false;b.click();return true;})()") == "true";
                    if (zoomed) await Task.Delay(100, token);
                    evidence["cityMountedZoomIn"] = new
                    {
                        clicked = zoomed,
                        before = mountedCity.GetProperty("zoom").GetString(),
                        after = JsonDocument.Parse(await core.ExecuteScriptAsync(
                            "document.querySelector('.city-layout-zoom output')?.textContent??null"))
                            .RootElement.GetString(),
                    };
                    // Test original mounted selection and keyboard clear on
                    // current native buildings, without dragging, saving or
                    // submitting any city change to the account.
                    bool selected = await core.ExecuteScriptAsync(
                        "(()=>{const b=document.querySelector('.city-layout-grid [data-city-building-id]');if(!b)return false;b.click();return true;})()") == "true";
                    if (selected) await Task.Delay(100, token);
                    var selectedMetrics = JsonDocument.Parse(await core.ExecuteScriptAsync(
                        CityMountedMetrics)).RootElement.Clone();
                    evidence["cityMountedSelect"] = new
                    {
                        clicked = selected,
                        selectedCount = selectedMetrics.GetProperty("selectedCount").GetInt32(),
                        nativeBuildingCount = uiBuildings.GetArrayLength(),
                    };
                    if (selected && selectedMetrics.GetProperty("selectedCount").GetInt32() > 0)
                        await Capture("feature018-r2-city-selected.png");
                    await core.ExecuteScriptAsync(
                        "window.dispatchEvent(new KeyboardEvent('keydown',{key:'Escape',bubbles:true}))");
                    await Task.Delay(60, token);
                    evidence["cityMountedEscapeClearsSelection"] =
                        JsonDocument.Parse(await core.ExecuteScriptAsync(
                            CityMountedMetrics)).RootElement.GetProperty("selectedCount").GetInt32() == 0;
                    bool zoomedOut = await core.ExecuteScriptAsync(
                        "(()=>{const b=document.querySelector('.city-layout-zoom button:first-child');if(!b||b.disabled)return false;b.click();return true;})()") == "true";
                    evidence["cityMountedZoomOut"] = new
                    {
                        clicked = zoomedOut,
                        after = JsonDocument.Parse(await core.ExecuteScriptAsync(
                            "document.querySelector('.city-layout-zoom output')?.textContent??null"))
                            .RootElement.GetString(),
                    };
                    bool fitted = await core.ExecuteScriptAsync(
                        "(()=>{const b=document.querySelector('.city-layout-zoom button:nth-child(4)');if(!b||b.disabled)return false;b.click();return true;})()") == "true";
                    evidence["cityMountedFitCanvas"] = new
                    {
                        clicked = fitted,
                        after = JsonDocument.Parse(await core.ExecuteScriptAsync(
                            "document.querySelector('.city-layout-zoom output')?.textContent??null"))
                            .RootElement.GetString(),
                    };
                }
            }
            await Capture("feature018-r2-city-before.png");
            if (cityLayoutDraftService is not null)
            {
                var draftRequest = JsonSerializer.SerializeToElement(
                    new { profileId = owner.ProfileId }, JsonOptions.Default);
                var originalDraft = (ProfileStateRecord?)await cityLayoutDraftService.InvokeAsync(
                    "city_layout_draft_get", draftRequest, token);
                evidence["cityDraftBefore"] = originalDraft;
                if (originalDraft is { Value: null, Revision: 0 } && before is { } freshCity)
                {
                    var tempDraft = new { version = 1,
                        baseRevision = freshCity.GetProperty("layoutRevision").GetString(),
                        placements = Array.Empty<object>(), updatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
                    var saved = (ProfileStateRecord?)await cityLayoutDraftService.InvokeAsync(
                        "city_layout_draft_save", JsonSerializer.SerializeToElement(new
                        { profileId = owner.ProfileId, revision = 0, value = tempDraft }, JsonOptions.Default), token);
                    var reread = (ProfileStateRecord?)await cityLayoutDraftService.InvokeAsync(
                        "city_layout_draft_get", draftRequest, token);
                    var cleared = (ProfileStateRecord?)await cityLayoutDraftService.InvokeAsync(
                        "city_layout_draft_clear", JsonSerializer.SerializeToElement(new
                        { profileId = owner.ProfileId, revision = saved?.Revision }, JsonOptions.Default), token);
                    evidence["cityDraftSaveReadClear"] = new { saved, reread, cleared,
                        persisted = saved?.Revision == reread?.Revision,
                        clearedToOriginal = cleared?.Revision == 0 && cleared?.Value is null };
                }
            }
            if (before is { } baselineCity)
            {
                bool candidateObserved = false;
                bool candidateValidated = false;
                int staleRevisions = 0;
                JsonElement city = baselineCity;
                // A vacant geometric neighbor is not necessarily inside the
                // game's secondary-building zone. Ask the genuine v26 native
                // validator for up to 96 different targets, never send an
                // unvalidated move. Abort if game revisions keep changing.
                for (int candidateOrdinal = 0; candidateOrdinal < 96; candidateOrdinal++)
                {
                    int nativeCandidates = city.TryGetProperty("moveCandidates", out var native) &&
                        native.ValueKind == JsonValueKind.Array ? native.GetArrayLength() : 0;
                    string? uuid;
                    int oldPoint, newPoint;
                    bool suggestedNative = candidateOrdinal < nativeCandidates;
                    if (suggestedNative)
                    {
                        JsonElement proposal = native[candidateOrdinal];
                        if (!proposal.TryGetProperty("uuid", out var proposedUuid) ||
                            proposedUuid.ValueKind != JsonValueKind.String ||
                            !proposal.TryGetProperty("fromPointId", out var from) ||
                            !from.TryGetInt32(out oldPoint) ||
                            !proposal.TryGetProperty("targetPointId", out var target) ||
                            !target.TryGetInt32(out newPoint) ||
                            oldPoint < 1 || newPoint < 1 ||
                            (uuid = proposedUuid.GetString()) is null ||
                            FindCityBuildingPoint(city, uuid) != oldPoint)
                        {
                            evidence["cityMoveCandidate"] = "Native proposed move was missing a current building identity or point";
                            break;
                        }
                    }
                    else if (!TryFindCityMove(city, candidateOrdinal - nativeCandidates,
                        out uuid, out oldPoint, out newPoint)) break;
                    // Two R3 sessions sent an unacknowledged legacy Replace
                    // packet for this exact native building. Even though the
                    // v26 Move sender has now been recovered, never transmit
                    // another command for that building while its earlier
                    // server result is unresolved. A distinct current-game
                    // building can be used for a separately admitted Move.
                    if (Environment.GetEnvironmentVariable(
                            "LWBRIDGE_FEATURE018_R3_CITY_ACTION_OPT_IN") == "1" &&
                        uuid == "1392040865799139725")
                    {
                        evidence["cityPreviousUnacknowledgedBuildingExcluded"] = uuid;
                        continue;
                    }
                    candidateObserved = true;
                if (suggestedNative) evidence["cityNativeCandidateValidated"] =
                    new { uuid, oldPoint, newPoint };
                var outbound = new { baseRevision = city.GetProperty("layoutRevision").GetString(),
                    placements = new[] { new { uuid, targetPointId = newPoint } } };
                JsonElement? validation = await Command("city_layout_validate", outbound,
                    "cityMoveValidation");
                evidence["cityMoveCandidatesValidated"] = candidateOrdinal + 1;
                // A valid placement must come from the same *current* native
                // building/zone revision that was sampled. One R3 packaged
                // session changed city revisions during a long candidate
                // search, creating mixed STALE/ZONE replies. Reacquire the
                // current source and retry only a fresh candidate index.
                if (validation is { } checkedValidation &&
                    checkedValidation.TryGetProperty("issues", out var issues) &&
                    issues.ValueKind == JsonValueKind.Array &&
                    issues.EnumerateArray().Any(issue =>
                        issue.TryGetProperty("code", out var issueCode) &&
                        issueCode.GetString() == "CITY_LAYOUT_STALE"))
                {
                    if (++staleRevisions > 3)
                    {
                        evidence["cityMoveCandidate"] = "Native City changed revisions repeatedly during validation";
                        break;
                    }
                    JsonElement? current = await Command("city_layout_snapshot_get",
                        receiptKey: "citySnapshotRevalidated");
                    if (current is not { } verified ||
                        !verified.TryGetProperty("isInCity", out var inCity) ||
                        inCity.ValueKind != JsonValueKind.True)
                    {
                        evidence["cityMoveCandidate"] = "Authoritative refreshed City snapshot unavailable";
                        break;
                    }
                    city = verified;
                    evidence["cityMoveStaleSnapshotsRefreshed"] = staleRevisions;
                    continue;
                }
                if (validation is { } valid && valid.TryGetProperty("valid", out var yes) &&
                    yes.ValueKind == JsonValueKind.True)
                {
                    candidateValidated = true;
                    // The two previously accepted live v26 packets for this
                    // owner had NO independently confirmed BuildManager ACK.
                    // The native sender returns void, so repeated submission
                    // could race a late server result even after restarting
                    // the game. Continue native validation and mounted UI
                    // proof without submitting another uncertain packet.
                    if (Environment.GetEnvironmentVariable(
                            "LWBRIDGE_FEATURE018_R3_CITY_ACTION_OPT_IN") != "1")
                    {
                        evidence["cityMoveSendWithheld"] =
                            "Earlier native building replacement has no correlated server ACK; manual fresh one-shot admission required";
                        break;
                    }
                    JsonElement? start = await Command("city_layout_apply_start", outbound,
                        "cityMoveStart");
                    if (start is { } request && request.TryGetProperty("accepted", out var accepted) &&
                        accepted.ValueKind == JsonValueKind.True)
                    {
                        await AwaitCityStatus(Command, Save, evidence, token, "cityMoveStatus");
                        JsonElement? after = await Command("city_layout_snapshot_get",
                            receiptKey: "citySnapshotAfter");
                        if (after is { } checkedCity)
                        {
                            int? observed = FindCityBuildingPoint(checkedCity, uuid!);
                            evidence["cityMoveAuthoritativePoint"] = new { uuid, oldPoint, newPoint,
                                observed, moved = observed == newPoint };
                            await Save();
                            if (observed == newPoint)
                            {
                                var back = new { baseRevision = checkedCity.GetProperty("layoutRevision").GetString(),
                                    placements = new[] { new { uuid, targetPointId = oldPoint } } };
                                JsonElement? undoValid = await Command("city_layout_validate", back,
                                    "cityRestoreValidation");
                                if (undoValid is { } uv && uv.GetProperty("valid").GetBoolean())
                                {
                                    JsonElement? restore = await Command("city_layout_apply_start", back,
                                        "cityRestoreStart");
                                    if (restore is { } rs && rs.TryGetProperty("accepted", out var ra) &&
                                        ra.ValueKind == JsonValueKind.True)
                                        await AwaitCityStatus(Command, Save, evidence, token,
                                            "cityRestoreStatus");
                                    await Command("city_layout_snapshot_get", receiptKey: "citySnapshotRestored");
                                }
                            }
                        }
                    }
                    break;
                }
                }
                if (!candidateValidated)
                    evidence["cityMoveCandidate"] = candidateObserved
                        ? "Each bounded geometrically valid candidate was rejected by the current native validator"
                        : "No source-valid vacant target or current city scene";
            }
            else evidence["cityMoveCandidate"] = "No source-valid vacant target or current city scene";
            await Save();
            await Capture("feature018-r2-city-after.png");

            await Nav(5, ".hotkey-panel[data-hotkey-category=hotkeys] .hotkey-switch", "Hotkeys");
            HotkeyConfig original = (HotkeyConfig)(await prefs.InvokeAsync("hotkey_config_get",
                empty.RootElement.Clone(), token) ?? throw new InvalidDataException("Hotkey config missing"));
            originalHotkeys = original;
            evidence["hotkeySettingsBefore"] = original;
            await Capture("feature018-r2-hotkeys.png");
            // Exercise a real UI switch and verify the native per-profile store.
            bool toggle = await core.ExecuteScriptAsync("(()=>{const b=document.querySelector('.hotkey-panel[data-hotkey-category=hotkeys] .hotkey-switch');if(!b||b.disabled)return false;b.click();return true;})()") == "true";
            bool persisted = false;
            if (toggle)
            {
                for (int attempt = 0; attempt < 80; attempt++)
                {
                    HotkeyConfig changed = (HotkeyConfig)(await prefs.InvokeAsync("hotkey_config_get",
                        empty.RootElement.Clone(), token) ?? original);
                    if (changed.Attack != original.Attack) { persisted = true; break; }
                    await Task.Delay(100, token);
                }
                await prefs.InvokeAsync("hotkey_config_save",
                    JsonSerializer.SerializeToElement(original, JsonOptions.Default), token);
            }
            evidence["hotkeyUiTogglePersisted"] = persisted;
            evidence["hotkeySettingsRestored"] = (HotkeyConfig)(await prefs.InvokeAsync(
                "hotkey_config_get", empty.RootElement.Clone(), token) ?? original) == original;
            await Command("feature018_input_focus", receiptKey: "gameInputFocus");
            await Command("feature018_shield_inventory_get", receiptKey: "r4NativeShieldInventory");
            // The R2 predecessor invoked the action API directly: that proves
            // nothing about the original Q/W/E/R etc game-window key bindings.
            // Exercise the *real foreground recognizer*, with the precise
            // approved game HWND, and distinguish physical key delivery,
            // recognized intent, native request and confirmed poststate.
            HotkeyConfig enabled = original with
            {
                Attack = true, Recall = true, ShieldOverlay = true,
                ShieldUse = true, Equipment = true, RandomRelocate = true,
                AllianceRelocate = true, FrontlineReinforce = true,
            };
            await prefs.InvokeAsync("hotkey_config_save",
                JsonSerializer.SerializeToElement(enabled, JsonOptions.Default), token);
            foreach (string binding in new[] {
                "Q","W","E","R","A","S","D","F","Space",
                "F6","F7","F8","Alt+1","Alt+2","Alt+3","Alt+4",
                "F9","F10","G" })
            {
                Owned();
                int previouslySeen = feature018R2KeyOutcomes.Count;
                Feature018HotkeyCapture? exactCapture = feature018Capture;
                string captureId = exactCapture?.CaptureId ?? "";
                long priorCaptureSequence = exactCapture?.LastObservedSequence ?? long.MaxValue;
                Feature018HotkeyIntent expected =
                    Feature018OwnedWindowKeys.ExpectedIntent(binding) ??
                    throw new InvalidDataException("Unmapped R2 binding " + binding);
                Feature018OwnedKeyReceipt delivered =
                    await Feature018OwnedWindowKeys.PressAsync(owner, binding,
                        ApprovedOwner, () => feature018InputClear &&
                            feature018Capture?.IsExactForegroundOwner() == true &&
                            DateTimeOffset.UtcNow - feature018InputObservedAt <
                                TimeSpan.FromMilliseconds(280), token);
                if (delivered.Pressed)
                {
                    // Native dispatch may legitimately take the full 8 s
                    // mailbox timeout; wait for this exact key's outcome
                    // before sending another physical game action. A late
                    // receipt from a prior key cannot count for this one.
                    for (int poll = 0; poll < 46; poll++)
                    {
                        Owned();
                        if (feature018R2KeyOutcomes.ToArray()
                            .Skip(previouslySeen).Any(result =>
                                result.Kind == expected.Kind.ToString() &&
                                result.Slot == expected.Slot)) break;
                        await Task.Delay(250, token);
                    }
                }
                Owned();
                var outcomes = feature018R2KeyOutcomes.ToArray()
                    .Skip(previouslySeen)
                    .Where(result => result.Kind == expected.Kind.ToString() &&
                        result.Slot == expected.Slot).ToArray();
                var candidates = outcomes.Where(result =>
                    Feature018KeyProofCorrelation.TemporalCandidate(delivered,
                        result.Kind, result.Slot, expected.Kind.ToString(),
                        expected.Slot, result.ObservedAtUtc)).ToArray();
                var captured = ReferenceEquals(exactCapture, feature018Capture) &&
                    exactCapture is not null
                    ? exactCapture.SnapshotCapturedIntents().Where(record =>
                        Feature018KeyProofCorrelation.CapturedIntentCandidate(delivered,
                            record, captureId, priorCaptureSequence, expected)).ToArray()
                    : Array.Empty<Feature018CapturedIntent>();
                evidence["hotkeyRealKey_" + binding] = new
                {
                    delivered, outcomes, temporalCandidates = candidates,
                    capturedIntents = captured,
                    ownedCaptureRecognitionObserved = captured.Length == 1,
                    // No intent-event unique ID ties the game's callback to
                    // this OS injection. Even a native provider Confirmed
                    // status from the same kind/slot could be another press.
                    recognized = false,
                    recognitionAttribution =
                        "capture_observation_and_os_key_window_only_origin_and_native_ack_not_proven",
                    temporalCandidateObserved = candidates.Length > 0,
                    confirmedByNativeProvider = false,
                    nativeProviderConfirmedCandidate = candidates.Any(item => item.Status == "Confirmed"),
                };
                await Save();
                if (!delivered.Pressed)
                {
                    // Holding Alt can transiently transfer foreground focus
                    // before the second key. The native sender always releases
                    // Alt in finally; retarget the exact owned HWND for the
                    // next independent test, retaining this binding as a
                    // negative. Never continue after an account/PID mismatch.
                    if (ApprovedOwner() && delivered.Observation is
                        "owned_window_changed_before_chord" or
                        "owned_window_changed_before_keydown" or
                        "game_foreground_activation_denied" or
                        "game_text_input_focus_could_not_be_qualified")
                    {
                        evidence["hotkeyFocusSkipped_" + binding] = delivered.Observation;
                        continue;
                    }
                    evidence["hotkeyWindowTestStopped"] = delivered.Observation;
                    break;
                }
            }
            await Capture("feature018-r2-hotkeys-after.png");

            // Current-v26 SOURCE_GAP experiments: use the authenticated
            // native read-only inspection doors even when a physical shortcut
            // could not be recognized. Never forge equipment UUIDs, a world
            // cursor target, or a mutable native action.
            for (int slot = 1; slot <= 4; slot++)
            {
                Owned();
                await Command("feature018_hotkey_action", new
                { action = "attack", slot, authorizedUid = uid },
                    "r3AttackTargetPreflightSlot" + slot);
                await Command("feature018_equipment_preflight", new { slot },
                    "r3EquipmentOwnerPreflightSlot" + slot);
            }
            await Command("feature018_hotkey_action", new
            { action = "randomRelocate", slot = 0 }, "r3RandomRelocationPreflight");
            await Command("feature018_hotkey_action", new
            { action = "allianceRelocate", slot = 0 }, "r3AllianceRelocationPreflight");

            await Nav(6, ".hotkey-panel[data-hotkey-category=miniGames] .mini-game-actions", "Mini Games");
            await Capture("feature018-r2-mini-before.png");
            JsonElement? landBefore = await Command("mini_land_status",
                receiptKey: "miniLandBefore");
            // The very first R3 one-shot UnlockUserLand was sent while native
            // next-cell inspection pointed at cell 5. Subsequent snapshots
            // exposed cell 6 but never proved cell 5's actual server state.
            // Query that *specific historical* cell read-only: a different
            // current next-cell never counts as an ACK on its own.
            await Command("mini_land_status", new { expectedCellId = 5 },
                "miniLandOriginalDispatchedCell5");
            int? exactLandCellId = null;
            if (landBefore is { } observedLand &&
                observedLand.TryGetProperty("cellId", out var observedCellId) &&
                observedCellId.TryGetInt32(out int cellId) && cellId is > 0 and <= 10000)
            {
                exactLandCellId = cellId;
                // Distinguish current-next-cell movement from the actual
                // previously submitted row. LandLockManager can advance
                // current-next before this status reply arrives.
                await Command("mini_land_status", new { expectedCellId = cellId },
                    "miniLandExactCellBefore");
            }
            // One uncertain accepted land dispatch was already observed in R3.
            // Future packaged verification runs are read-only until the
            // operator explicitly opts into a new one-shot land send.
            JsonElement? unlock = Environment.GetEnvironmentVariable(
                "LWBRIDGE_FEATURE018_R3_LAND_ACTION_OPT_IN") == "1"
                ? await Command("monopoly_cell_open", receiptKey: "miniLandUnlock")
                : null;
            if (unlock is null)
                evidence["miniLandUnlockSkipped"] = "prior_dispatched_land_ack_unconfirmed_no_auto_retry";
            if (exactLandCellId is { } pendingCellId)
                await Command("mini_land_status", new { expectedCellId = pendingCellId },
                    "miniLandExactCellAfter");
            if (unlock is { } u && u.TryGetProperty("requestDispatched", out var sent) &&
                sent.ValueKind == JsonValueKind.True)
            {
                for (int i = 0; i < 50; i++)
                {
                    JsonElement? state = await Command("mini_land_status",
                        exactLandCellId is { } sentCellId ? new { expectedCellId = sentCellId } : null,
                        receiptKey: "miniLandAfter");
                    if (state is { } land && land.TryGetProperty("state", out var current) &&
                        current.GetString() == "unlocked") break;
                    await Task.Delay(400, token);
                }
            }
            await Command("mini_sheep_status", receiptKey: "miniSheepBefore");
            await Command("mini_sheep_board", receiptKey: "miniSheepBoardBefore");
            await Command("feature018_sheep_automation_status", receiptKey: "miniSheepAutomationBefore");
            JsonElement? sheepStart = await Command("automation_start", new { taskName = "sheepGame" },
                "miniSheepStart");
            if (sheepStart is { } started && started.TryGetProperty("status", out _))
            {
                for (int i = 0; i < 90; i++)
                {
                    JsonElement? status = await Command("feature018_sheep_automation_status",
                        receiptKey: "miniSheepAutomationAfter");
                    if (status is { } current && current.TryGetProperty("terminal", out var terminal) &&
                        terminal.ValueKind == JsonValueKind.True) break;
                    await Task.Delay(500, token);
                }
                await Command("automation_stop", new { taskName = "sheepGame" },
                    "miniSheepStop");
            }
            await Command("mini_sheep_status", receiptKey: "miniSheepAfter");
            JsonElement? chest = await Command("mini_chest_position", receiptKey: "miniChestBefore");
            if (chest is { } c && c.TryGetProperty("markerEligible", out var eligible) &&
                eligible.ValueKind == JsonValueKind.True)
            {
                await Command("mini_chest_mark", receiptKey: "miniChestMarked");
                await Command("mini_chest_unmark", receiptKey: "miniChestUnmarked");
            }
            await Capture("feature018-r2-mini-after.png");
            evidence["state"] = "observed";
        }
        finally
        {
            if (originalHotkeys is not null)
            {
                try
                {
                    await prefs.InvokeAsync("hotkey_config_save",
                        JsonSerializer.SerializeToElement(originalHotkeys, JsonOptions.Default),
                        CancellationToken.None);
                    evidence["hotkeyFinalPreferencesRestored"] =
                        (HotkeyConfig?)await prefs.InvokeAsync("hotkey_config_get",
                            empty.RootElement.Clone(), CancellationToken.None) == originalHotkeys;
                }
                catch (Exception error)
                {
                    evidence["hotkeyFinalPreferencesRestoreError"] = error.Message;
                }
            }
            evidence["finishedAtUtc"] = DateTimeOffset.UtcNow;
            // A canceled admission must still leave a durable partial receipt.
            await File.WriteAllTextAsync(outputPath,
                JsonSerializer.Serialize(evidence,
                    new JsonSerializerOptions(JsonOptions.Default) { WriteIndented = true }),
                CancellationToken.None);
        }
    }

    private static int? FindCityBuildingPoint(JsonElement snapshot, string uuid)
    {
        if (!snapshot.TryGetProperty("buildings", out JsonElement buildings)) return null;
        foreach (JsonElement building in buildings.EnumerateArray())
            if (building.GetProperty("uuid").GetString() == uuid)
                return building.GetProperty("pointId").GetInt32();
        return null;
    }

    private static bool TryFindCityMove(JsonElement snapshot, int candidateOrdinal, out string? uuid,
        out int oldPoint, out int newPoint)
    {
        uuid = null; oldPoint = newPoint = 0;
        if (!snapshot.TryGetProperty("isInCity", out var city) ||
            city.ValueKind != JsonValueKind.True) return false;
        var cells = snapshot.GetProperty("cells").EnumerateArray()
            .ToDictionary(cell => cell.GetProperty("pointId").GetInt32(), cell => cell);
        var xy = cells.Values.ToDictionary(cell =>
            (cell.GetProperty("x").GetInt32(), cell.GetProperty("y").GetInt32()),
            cell => cell.GetProperty("pointId").GetInt32());
        var builds = snapshot.GetProperty("buildings").EnumerateArray().ToArray();
        var occupied = builds.SelectMany(building => building.GetProperty("occupiedPoints")
            .EnumerateArray().Select(point => point.GetInt32())).ToHashSet();
        foreach (var building in builds)
        {
            if (building.GetProperty("movable").ValueKind != JsonValueKind.True) continue;
            int old = building.GetProperty("pointId").GetInt32();
            if (!cells.TryGetValue(old, out var anchor)) continue;
            int ax = anchor.GetProperty("x").GetInt32();
            int ay = anchor.GetProperty("y").GetInt32();
            var shape = building.GetProperty("occupiedPoints").EnumerateArray()
                .Select(p => cells[p.GetInt32()]).Select(c =>
                    (x: c.GetProperty("x").GetInt32() - ax,
                     y: c.GetProperty("y").GetInt32() - ay)).ToArray();
            foreach ((int dx,int dy) in Enumerable.Range(1, 12).SelectMany(distance =>
                new[] { (distance,0),(-distance,0),(0,distance),(0,-distance),
                    (distance,distance),(-distance,-distance),
                    (distance,-distance),(-distance,distance) }))
            {
                if (!xy.TryGetValue((ax + dx, ay + dy), out int candidate)) continue;
                bool okay = true;
                foreach (var offset in shape)
                {
                    if (!xy.TryGetValue((ax + dx + offset.x, ay + dy + offset.y), out int target) ||
                        occupied.Contains(target) || !cells.TryGetValue(target, out JsonElement cell) ||
                        cell.GetProperty("unlocked").ValueKind != JsonValueKind.True ||
                        cell.GetProperty("obstructed").ValueKind == JsonValueKind.True ||
                        cell.GetProperty("road").ValueKind == JsonValueKind.True ||
                        (cell.GetProperty("flagOnly").ValueKind == JsonValueKind.True &&
                         building.GetProperty("isFlag").ValueKind != JsonValueKind.True))
                    { okay = false; break; }
                }
                if (!okay) continue;
                if (candidateOrdinal-- > 0) continue;
                uuid = building.GetProperty("uuid").GetString();
                oldPoint = old; newPoint = candidate;
                return true;
            }
        }
        return false;
    }

    private static async Task AwaitCityStatus(
        Func<string, object?, string?, Task<JsonElement?>> request,
        Func<Task> save, Dictionary<string, object?> evidence,
        CancellationToken token, string key)
    {
        for (int i = 0; i < 100; i++)
        {
            JsonElement? status = await request("city_layout_apply_status", null, key);
            if (status is { } s && s.TryGetProperty("state", out var state) &&
                state.GetString() is "succeeded" or "failed" or "cancelled") return;
            await Task.Delay(250, token);
        }
        evidence[key + "Timeout"] = true;
        await save();
    }
}
