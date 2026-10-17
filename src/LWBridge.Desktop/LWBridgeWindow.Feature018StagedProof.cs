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
        var game = equipmentConfigService is null
            ? new Feature018GameCommandService(lifecycle)
            : new Feature018GameCommandService(lifecycle, equipmentConfigService);
        var approved = new Feature018HotkeySession(owner.ProfileId, owner.SessionId, uid, serverId);
        bool IsOwner() => Feature018IsApprovedAccount(uid, serverId) &&
            lifecycle.MatchesOwnedMapScanSession(owner) &&
            ReadFeature018AuthenticatedAccount(lifecycle, owner) == approved;
        void Owned()
        {
            token.ThrowIfCancellationRequested();
            if (!IsOwner()) throw new InvalidDataException("Staged proof lost its exact 2289 owner.");
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
                    ShieldOverlay = true, ShieldUse = true, Equipment = true,
                    RandomRelocate = true, AllianceRelocate = true }, JsonOptions.Default), token);
            await Save();
            DateTimeOffset expires = DateTimeOffset.UtcNow.AddMinutes(13);
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
                    missingOwnerSince ??= DateTimeOffset.UtcNow;
                    if (DateTimeOffset.UtcNow - missingOwnerSince > TimeSpan.FromSeconds(15))
                        throw new InvalidDataException("Staged proof lost its exact 2289 owner for 15 seconds.");
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
                        Owned();
                        object? result;
                        if (kind == "command")
                        {
                            string command = root.GetProperty("command").GetString()!;
                            result = await game.InvokeAsync(command,
                                root.TryGetProperty("payload", out var payload) ? payload : empty.RootElement,
                                token);
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
                                    DateTimeOffset.UtcNow - feature018InputObservedAt < TimeSpan.FromMilliseconds(280), token);
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
                        Owned();
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
