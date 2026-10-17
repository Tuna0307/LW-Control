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
        var game = new Feature018GameCommandService(lifecycle);
        var evidence = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schemaVersion"] = 2,
            ["state"] = "partial",
            ["game"] = new { owner.ProfileId, owner.SessionId, owner.GamePid, uid, serverId,
                heartbeatVerified = true },
            ["startedAtUtc"] = DateTimeOffset.UtcNow,
        };
        using JsonDocument empty = JsonDocument.Parse("{}");
        void Owned()
        {
            token.ThrowIfCancellationRequested();
            if (!lifecycle.MatchesOwnedMapScanSession(owner))
                throw new InvalidDataException("FEATURE018 R2 game owner changed");
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
        try
        {
            await Nav(4, ".city-layout-panel, .city-layout-empty", "City Layout");
            JsonElement? before = await Command("city_layout_snapshot_get", receiptKey: "citySnapshotBefore");
            await Command("city_layout_apply_status", receiptKey: "cityApplyBefore");
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
            if (before is { } city && TryFindCityMove(city, out string? uuid,
                    out int oldPoint, out int newPoint))
            {
                var outbound = new { baseRevision = city.GetProperty("layoutRevision").GetString(),
                    placements = new[] { new { uuid, targetPointId = newPoint } } };
                JsonElement? validation = await Command("city_layout_validate", outbound,
                    "cityMoveValidation");
                if (validation is { } valid && valid.TryGetProperty("valid", out var yes) &&
                    yes.ValueKind == JsonValueKind.True)
                {
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
                }
            }
            else evidence["cityMoveCandidate"] = "No source-valid vacant target or current city scene";
            await Save();
            await Capture("feature018-r2-city-after.png");

            await Nav(5, ".hotkey-panel[data-hotkey-category=hotkeys] .hotkey-switch", "Hotkeys");
            HotkeyConfig original = (HotkeyConfig)(await prefs.InvokeAsync("hotkey_config_get",
                empty.RootElement.Clone(), token) ?? throw new InvalidDataException("Hotkey config missing"));
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
            foreach ((string action, int slot) in new[] {
                ("attack",1),("recall",1),("shieldOverlay",0),("shieldUse",8),
                ("equipment",1),("randomRelocate",0),("allianceRelocate",0),
                ("frontlineReinforce",5) })
                await Command("feature018_hotkey_action", new { action, slot },
                    $"gameHotkey_{action}_{slot}");
            await Capture("feature018-r2-hotkeys-after.png");

            await Nav(6, ".hotkey-panel[data-hotkey-category=miniGames] .mini-game-actions", "Mini Games");
            await Capture("feature018-r2-mini-before.png");
            await Command("mini_land_status", receiptKey: "miniLandBefore");
            JsonElement? unlock = await Command("monopoly_cell_open", receiptKey: "miniLandUnlock");
            if (unlock is { } u && u.TryGetProperty("requestDispatched", out var sent) &&
                sent.ValueKind == JsonValueKind.True)
            {
                for (int i = 0; i < 50; i++)
                {
                    JsonElement? state = await Command("mini_land_status", receiptKey: "miniLandAfter");
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
            evidence["finishedAtUtc"] = DateTimeOffset.UtcNow;
            await Save();
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

    private static bool TryFindCityMove(JsonElement snapshot, out string? uuid,
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
            foreach ((int dx,int dy) in new[] { (1,0),(-1,0),(0,1),(0,-1),
                (2,0),(-2,0),(0,2),(0,-2),(3,0),(-3,0),(0,3),(0,-3) })
            {
                if (!xy.TryGetValue((ax + dx, ay + dy), out int candidate)) continue;
                bool okay = true;
                foreach (var offset in shape)
                {
                    if (!xy.TryGetValue((ax + dx + offset.x, ay + dy + offset.y), out int target) ||
                        occupied.Contains(target) || !cells.TryGetValue(target, out JsonElement cell) ||
                        cell.GetProperty("unlocked").ValueKind != JsonValueKind.True ||
                        cell.GetProperty("road").ValueKind == JsonValueKind.True ||
                        (cell.GetProperty("flagOnly").ValueKind == JsonValueKind.True &&
                         building.GetProperty("isFlag").ValueKind != JsonValueKind.True))
                    { okay = false; break; }
                }
                if (!okay) continue;
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
