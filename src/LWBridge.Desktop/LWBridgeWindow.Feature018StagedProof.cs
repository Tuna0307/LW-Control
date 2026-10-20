using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace LWBridge.Desktop;

internal sealed partial class LWBridgeWindow
{
    // Opt-in isolated proof driver. Every operation uses the production command
    // services and exact owned HWND; it cannot evaluate supplied code or choose
    // a foreign game/account. The queue is consumed once, including failures.
    private async Task RunFeature018StagedProofAsync(CoreWebView2 core,
        string outputPath, string directory, OverviewMapScanSession owner,
        string uid, int serverId, CancellationToken token)
    {
        var lifecycle = overviewLifecycleService!;
        var game = new Feature018GameCommandService(new Feature018OwnedGameExchange(lifecycle),
            equipmentConfigService, new Feature018AuthenticatedGameAccount(uid, serverId), profileRuntimeConfigStore);
        var approved = new Feature018HotkeySession(owner.ProfileId, owner.SessionId, uid, serverId);
        bool IsOwner() => Feature018IsApprovedAccount(uid, serverId) &&
            lifecycle.OwnsExactProcess(owner.GamePid) &&
            lifecycle.MatchesOwnedMapScanSession(owner) &&
            ReadFeature018AuthenticatedAccount(lifecycle, owner) == approved;
        async Task SaveOwnerLoss(string reason, DateTimeOffset? missingSince)
        {
            bool processOwned = lifecycle.OwnsExactProcess(owner.GamePid);
            bool sessionMatches = lifecycle.MatchesOwnedMapScanSession(owner);
            Feature018HotkeySession? account =
                processOwned && sessionMatches
                    ? ReadFeature018AuthenticatedAccount(lifecycle, owner)
                    : null;
            await File.WriteAllTextAsync(
                Path.Combine(directory, "feature018-owner-loss.json"),
                JsonSerializer.Serialize(new
                {
                    schemaVersion = 1,
                    state = "failed",
                    reason,
                    observedAtUtc = DateTimeOffset.UtcNow,
                    missingSinceUtc = missingSince,
                    game = new
                    {
                        owner.ProfileId,
                        owner.SessionId,
                        owner.GamePid,
                        owner.GamePath,
                        owner.GameStartedAtUtc,
                        uid,
                        serverId,
                    },
                    approvedAccount = Feature018IsApprovedAccount(uid, serverId),
                    exactOwnedProcess = processOwned,
                    ownedSessionMatches = sessionMatches,
                    authenticatedAccount = account,
                }, new JsonSerializerOptions(JsonOptions.Default) { WriteIndented = true }),
                CancellationToken.None);
        }
        void Owned()
        {
            token.ThrowIfCancellationRequested();
            if (IsOwner()) return;
            if (!lifecycle.OwnsExactProcess(owner.GamePid))
                throw new InvalidDataException("Staged proof exact owned game process is unavailable.");
            throw new InvalidDataException("Staged proof lost its configured authenticated proof account/session owner.");
        }
        async Task WaitOwnedAsync()
        {
            DateTimeOffset until = DateTimeOffset.UtcNow.AddSeconds(30);
            while (!IsOwner())
            {
                token.ThrowIfCancellationRequested();
                var account = ReadFeature018AuthenticatedAccount(lifecycle, owner);
                if (!lifecycle.OwnsExactProcess(owner.GamePid) || !lifecycle.MatchesOwnedMapScanSession(owner) ||
                    (account is not null && account != approved) || DateTimeOffset.UtcNow >= until)
                    throw new InvalidDataException("Staged proof owner changed or remained unavailable; no operation replayed.");
                await Task.Delay(100, token);
            }
        }
        var log = new List<object>();
        var consumed = new HashSet<string>(StringComparer.Ordinal);
        string queue = Path.Combine(directory, "feature018-live-request.json");
        async Task Save(string state = "running") => await File.WriteAllTextAsync(outputPath,
            JsonSerializer.Serialize(new { schemaVersion = 3, state,
                game = new { owner.ProfileId, owner.SessionId, owner.GamePid, uid, serverId },
                lastObservedAtUtc = DateTimeOffset.UtcNow, operations = log },
                new JsonSerializerOptions(JsonOptions.Default) { WriteIndented = true }), token);
        using var empty = JsonDocument.Parse("{}");
        HotkeyConfig original = (HotkeyConfig)(await hotkeyConfigService!.InvokeAsync(
            "hotkey_config_get", empty.RootElement, token))!;
        object? originalEquipment = equipmentConfigService is null ? null :
            await equipmentConfigService.InvokeAsync("equipment_config_get", empty.RootElement, token);
        try
        {
            await hotkeyConfigService.InvokeAsync("hotkey_config_save",
                JsonSerializer.SerializeToElement(original with { Attack = true, Recall = true,
                    ShieldUse = true, Equipment = true,
                    RandomRelocate = true, AllianceRelocate = true }, JsonOptions.Default), token);
            await Save();
            DateTimeOffset expires = DateTimeOffset.UtcNow.AddMinutes(35);
            DateTimeOffset? missingOwnerSince = null;
            while (DateTimeOffset.UtcNow < expires)
            {
                token.ThrowIfCancellationRequested();
                // A heartbeat may briefly disappear while the native producer
                // rewrites it at login/scene transitions. A missing heartbeat
                // must never admit commands, but it must not abort the owned
                // proof before any operation is queued either.
                if (!IsOwner())
                {
                    if (ReadFeature018AuthenticatedAccount(lifecycle, owner) is { } changed && changed != approved)
                        throw new InvalidDataException("Staged proof authenticated UID/server changed.");
                    missingOwnerSince ??= DateTimeOffset.UtcNow;
                    if (!lifecycle.OwnsExactProcess(owner.GamePid))
                    {
                        await SaveOwnerLoss("exact_owned_game_process_unavailable", missingOwnerSince);
                        throw new InvalidDataException(
                            "Staged proof exact owned game process is unavailable.");
                    }
                    if (DateTimeOffset.UtcNow - missingOwnerSince > TimeSpan.FromSeconds(60))
                    {
                        await SaveOwnerLoss("authenticated_owner_unavailable_60_seconds",
                            missingOwnerSince);
                        throw new InvalidDataException(
                            "Staged proof configured authenticated proof account/session owner was unavailable for 60 seconds while the exact game process remained owned.");
                    }
                    await Task.Delay(200, token);
                    continue;
                }
                missingOwnerSince = null;
                if (!File.Exists(queue)) { await Task.Delay(200, token); continue; }
                JsonDocument request;
                try { request = JsonDocument.Parse(await File.ReadAllTextAsync(queue, token)); }
                catch (IOException) { await Task.Delay(200, token); continue; }
                catch (JsonException) { await Task.Delay(200, token); continue; }
                using (request)
                {
                    var root = request.RootElement;
                    string id = root.GetProperty("id").GetString()!;
                    if (!consumed.Add(id)) { await Task.Delay(200, token); continue; }
                    string kind = root.GetProperty("kind").GetString()!;
                    if (kind == "finish") { log.Add(new { id, kind, state = "finished" }); break; }
                    try
                    {
                        if (Environment.GetEnvironmentVariable("LWBRIDGE_FEATURE018_BACKGROUND_ONLY") == "1" &&
                            kind is not ("command" or "mapCommand" or "profileCommand" or "equipmentConfig" or "cityApplyCancel" or "capture" or "cityNames" or "r23Ui"))
                            throw new InvalidDataException("R18 background proof forbids input, show and window operations.");
                        await WaitOwnedAsync();
                        object? result;
                        if (kind == "r23Ui")
                        {
                            string surface = root.GetProperty("surface").GetString()!;
                            string receipt = Path.Combine(directory, "r23-" + surface + "-ui.json");
                            if (File.Exists(receipt)) throw new InvalidDataException("Preserve one-shot R23 UI proof.");
                            if (surface == "equipment")
                                await RunFeature018R26EquipmentTabProofAsync(core, receipt, directory, owner, uid, serverId, token);
                            else if (surface == "city")
                                await RunFeature018R23CityImageProofAsync(core, receipt, directory, owner, uid, serverId, token);
                            else if (surface == "chat")
                                await RunFeature018R23ChatTabProofAsync(core, receipt, directory, owner, uid, serverId, token);
                            else throw new InvalidDataException("Unsupported R23 UI surface");
                            result = JsonDocument.Parse(await File.ReadAllTextAsync(receipt, token)).RootElement.Clone();
                        }
                        else if (kind == "command")
                        {
                            string command = root.GetProperty("command").GetString()!;
                            if (command.StartsWith("mini_land_", StringComparison.Ordinal) ||
                                command == "monopoly_cell_open")
                                throw new InvalidDataException("R14 forbids all live Land Cell requests");
                            result = await game.InvokeAsync(command,
                                root.TryGetProperty("payload", out var payload) ? payload : empty.RootElement,
                                token);
                        }
                        else if (kind == "mapCommand")
                        {
                            string command = root.GetProperty("command").GetString()!;
                            if (command is not ("map_coordinate_jump" or "map_scan_start" or "map_scan_status" or "map_search" or "map_summary"))
                                throw new InvalidDataException("Unsupported R18 Map proof command");
                            result = await map317CommandService!.InvokeAsync(command,
                                root.TryGetProperty("payload", out var payload) ? payload : empty.RootElement, token);
                        }
                        else if (kind == "profileCommand")
                        {
                            string command = root.GetProperty("command").GetString()!;
                            JsonElement payload = root.TryGetProperty("payload", out var arg) ? arg : empty.RootElement;
                            result = command switch
                            {
                                "monster_afk_config_save" or "stamina_potion_config_save" => await monsterAfkConfigService!.InvokeAsync(command, payload, token),
                                "automation_status" => await automationStatusService!.InvokeAsync(command, payload, token),
                                _ => throw new InvalidDataException("Unsupported R18 profile proof command"),
                            };
                        }
                        else if (kind == "cityApplyCancel")
                        {
                            // The live server can settle all three local City swap
                            // moves inside the staged queue's 200 ms outer poll.
                            // Exercise the exact production Start + Cancel services
                            // back-to-back in this sole controller instead of adding
                            // a synthetic Lua/game cancellation path.
                            JsonElement payload = root.GetProperty("payload");
                            object? started = await game.InvokeAsync(
                                "city_layout_apply_start", payload, token);
                            Owned();
                            JsonElement startedJson = JsonSerializer.SerializeToElement(
                                started, JsonOptions.Default);
                            if (!startedJson.TryGetProperty("status", out JsonElement status) ||
                                !status.TryGetProperty("jobId", out JsonElement rawJob) ||
                                rawJob.ValueKind != JsonValueKind.String ||
                                string.IsNullOrWhiteSpace(rawJob.GetString()))
                                throw new InvalidDataException(
                                    "City start did not return a job ID for immediate Cancel.");
                            string jobId = rawJob.GetString()!;
                            object? cancelled = await game.InvokeAsync(
                                "city_layout_apply_cancel",
                                JsonSerializer.SerializeToElement(new { jobId }, JsonOptions.Default),
                                token);
                            result = new { started, cancelled, immediateCancel = true };
                        }
                        else if (kind == "cityNames")
                        {
                            // R21 background-only visual proof. This drives only
                            // the isolated app WebView: no OS input, focus/window
                            // operation or game mutation is performed. The City
                            // page itself reads native snapshot/name data through
                            // the production authenticated bridge.
                            if (await core.ExecuteScriptAsync(
                                "(()=>{const b=document.querySelectorAll('.side-nav button')[4];if(!b)return false;b.click();return true;})()") != "true")
                                throw new InvalidDataException("R21 City Layout navigation is unavailable");

                            async Task<JsonElement> CaptureCityLanguage(string language, string fileName)
                            {
                                string languageJson = JsonSerializer.Serialize(language, JsonOptions.Default);
                                string switched = await core.ExecuteScriptAsync($$"""
                                    (() => {
                                      const select = document.querySelector('.language-select select');
                                      if (!select) return false;
                                      if (select.value !== {{languageJson}}) {
                                        select.value = {{languageJson}};
                                        select.dispatchEvent(new Event('change', { bubbles: true }));
                                      }
                                      return true;
                                    })()
                                    """);
                                if (switched != "true")
                                    throw new InvalidDataException("R21 app language selector is unavailable");

                                JsonElement settled = default;
                                for (int poll = 0; poll < 180; poll++)
                                {
                                    Owned();
                                    string raw = await core.ExecuteScriptAsync("""
                                        (() => {
                                          const panel = document.querySelector('.city-layout-panel');
                                          const buildings = [...document.querySelectorAll('.city-layout-grid [data-city-building-id]')];
                                          const titles = buildings.map(b => b.title || '');
                                          const levels = buildings.map(b => b.querySelector('.city-layout-building-level')?.textContent || '');
                                          const visibleNames = [...document.querySelectorAll('.city-layout-building-name')].map(n => n.textContent || '');
                                          const loading = !!document.querySelector('.city-layout-name-preview[role=status]');
                                          const error = document.querySelector('.city-layout-error[role=alert]')?.textContent || '';
                                          return {
                                            language: document.documentElement.lang || '',
                                            livePanel: !!panel && !panel.hasAttribute('data-preview-fixture'),
                                            zoom: document.querySelector('.city-layout-zoom output')?.textContent || '',
                                            buildingCount: buildings.length,
                                            loading,
                                            error,
                                            titles,
                                            levels,
                                            visibleNames,
                                            fallbackIdCount: titles.filter(v => /^#\\d+/.test(v)).length,
                                            rawNameKeyCount: titles.filter(v => /(?:^|\\s)(?:native_|building_)/i.test(v)).length,
                                            readableLevelCount: levels.filter(v => /^\\d+$/.test(v)).length,
                                          };
                                        })()
                                        """);
                                    using JsonDocument metricsDoc = JsonDocument.Parse(raw);
                                    JsonElement metrics = metricsDoc.RootElement;
                                    bool ready = metrics.GetProperty("language").GetString() == language &&
                                        metrics.GetProperty("livePanel").GetBoolean() &&
                                        metrics.GetProperty("zoom").GetString() == "100%" &&
                                        metrics.GetProperty("buildingCount").GetInt32() > 0 &&
                                        !metrics.GetProperty("loading").GetBoolean() &&
                                        string.IsNullOrEmpty(metrics.GetProperty("error").GetString()) &&
                                        metrics.GetProperty("fallbackIdCount").GetInt32() == 0 &&
                                        metrics.GetProperty("rawNameKeyCount").GetInt32() == 0 &&
                                        metrics.GetProperty("readableLevelCount").GetInt32() == metrics.GetProperty("buildingCount").GetInt32() &&
                                        metrics.GetProperty("visibleNames").GetArrayLength() > 0;
                                    if (ready) { settled = metrics.Clone(); break; }
                                    await Task.Delay(100, token);
                                }
                                if (settled.ValueKind == JsonValueKind.Undefined)
                                    throw new InvalidDataException("R21 City names did not settle at 100% zoom in " + language);
                                await using (var image = File.Create(Path.Combine(directory, fileName)))
                                    await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, image);
                                return settled;
                            }

                            JsonElement english = await CaptureCityLanguage("en", "feature018-r21-city-en.png");
                            JsonElement chinese = await CaptureCityLanguage("zh-CN", "feature018-r21-city-zh-CN.png");
                            string? englishFirst = english.GetProperty("titles").EnumerateArray().FirstOrDefault().GetString();
                            string? chineseFirst = chinese.GetProperty("titles").EnumerateArray().FirstOrDefault().GetString();
                            bool localizedDifference = !string.IsNullOrWhiteSpace(englishFirst) &&
                                !string.IsNullOrWhiteSpace(chineseFirst) && englishFirst != chineseFirst;
                            if (!localizedDifference)
                                throw new InvalidDataException("R21 City English/Chinese names did not differ");
                            result = new { english, chinese, localizedDifference,
                                screenshots = new[] { "feature018-r21-city-en.png", "feature018-r21-city-zh-CN.png" },
                                gameMutationDispatched = false };
                        }
                        else if (kind == "equipmentConfig")
                        {
                            string command = root.GetProperty("command").GetString()!;
                            if (command is not ("equipment_config_get" or "equipment_config_save") ||
                                equipmentConfigService is null)
                                throw new InvalidDataException("Unsupported equipment config operation");
                            result = await equipmentConfigService.InvokeAsync(command,
                                root.TryGetProperty("payload", out var payload) ? payload : empty.RootElement, token);
                        }
                        else if (kind == "key")
                        {
                            string binding = root.GetProperty("binding").GetString()!;
                            Feature018HotkeyIntent expected = Feature018OwnedWindowKeys.ExpectedIntent(binding)
                                ?? throw new InvalidDataException("Unknown original hotkey binding");
                            int before = feature018R2KeyOutcomes.Count;
                            Feature018HotkeyCapture? exactCapture = feature018Capture;
                            string captureId = exactCapture?.CaptureId ?? "";
                            long priorCaptureSequence = exactCapture?.LastObservedSequence ?? long.MaxValue;
                            var sent = await Feature018OwnedWindowKeys.PressAsync(owner, binding,
                                IsOwner, () => feature018InputClear &&
                                    feature018Capture?.IsExactForegroundOwner() == true &&
                                    DateTimeOffset.UtcNow - feature018InputObservedAt < TimeSpan.FromMilliseconds(850), token);
                            if (sent.Pressed)
                                for (int i = 0; i < 180 && !feature018R2KeyOutcomes.ToArray().Skip(before).Any(
                                    item => item.Kind == expected.Kind.ToString() && item.Slot == expected.Slot); i++)
                                { Owned(); await Task.Delay(250, token); }
                            var observed = feature018R2KeyOutcomes.ToArray().Skip(before).Where(
                                item => item.Kind == expected.Kind.ToString() && item.Slot == expected.Slot).ToArray();
                            var captured = ReferenceEquals(exactCapture, feature018Capture) &&
                                exactCapture is not null
                                ? exactCapture.SnapshotCapturedIntents().Where(record =>
                                    Feature018KeyProofCorrelation.CapturedIntentCandidate(sent,
                                        record, captureId, priorCaptureSequence, expected)).ToArray()
                                : Array.Empty<Feature018CapturedIntent>();
                            result = new { delivered = sent,
                                outcomes = observed,
                                capturedIntents = captured,
                                ownedCaptureRecognitionObserved = captured.Length == 1,
                                temporalCandidates = observed.Where(item =>
                                    Feature018KeyProofCorrelation.TemporalCandidate(sent,
                                        item.Kind, item.Slot, expected.Kind.ToString(),
                                        expected.Slot, item.ObservedAtUtc)).ToArray(),
                                recognized = false,
                                recognitionAttribution =
                                    "capture_observation_and_os_key_window_only_origin_and_native_ack_not_proven" };
                        }
                        else if (kind == "observedKeys")
                        {
                            // Read-only evidence for physical keys delivered by the
                            // Windows computer-use tool, outside the harness sender.
                            result = new { outcomes = feature018R2KeyOutcomes.ToArray(),
                                capturedIntents = feature018Capture?.SnapshotCapturedIntents(),
                                captureId = feature018Capture?.CaptureId,
                                foregroundOwned = feature018Capture?.IsExactForegroundOwner(),
                                inputClear = feature018InputClear,
                                inputObservedAtUtc = feature018InputObservedAt,
                                authenticatedAccount = ReadFeature018AuthenticatedAccount(lifecycle, owner),
                                preferences = feature018Preferences,
                                observedAtUtc = DateTimeOffset.UtcNow,
                                inputOriginProvenByThisRead = false };
                        }
                        else if (kind == "show")
                        {
                            Show();
                            WindowState = FormWindowState.Normal;
                            result = new { visible = Visible, ownedAppOnly = true };
                        }
                        else if (kind == "tab")
                        {
                            int index = root.GetProperty("index").GetInt32();
                            if (index is < 4 or > 6) throw new InvalidDataException("Feature tab out of scope");
                            result = await core.ExecuteScriptAsync($"(()=>{{const b=document.querySelectorAll('.side-nav button')[{index}];if(!b)return false;b.click();return true;}})()");
                            await Task.Delay(500, token);
                        }
                        else if (kind == "capture")
                        {
                            string file = "feature018-r2-stage-" + consumed.Count + ".png";
                            await using var output = File.Create(Path.Combine(directory, file));
                            await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, output);
                            result = file;
                        }
                        else throw new InvalidDataException("Unsupported proof operation");
                        await WaitOwnedAsync();
                        log.Add(new { id, kind, request = root.Clone(), state = "returned", result,
                            observedAtUtc = DateTimeOffset.UtcNow });
                    }
                    catch (Exception error) when (error is not OperationCanceledException)
                    {
                        log.Add(new { id, kind, request = root.Clone(), state = "failed",
                            code = error is BridgeCommandException native ? native.Code : null,
                            details = error is BridgeCommandException diagnostic ? diagnostic.Details : null,
                            error = error.Message, observedAtUtc = DateTimeOffset.UtcNow });
                    }
                    await Save();
                }
            }
            await Save("partial");
        }
        finally
        {
            await hotkeyConfigService.InvokeAsync("hotkey_config_save",
                JsonSerializer.SerializeToElement(original, JsonOptions.Default), CancellationToken.None);
            if (originalEquipment is not null && equipmentConfigService is not null)
                await equipmentConfigService.InvokeAsync("equipment_config_save",
                    JsonSerializer.SerializeToElement(originalEquipment, JsonOptions.Default), CancellationToken.None);
        }
    }
}
