using System.Text.Json;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;

namespace LWBridge.Desktop;

internal sealed partial class LWBridgeWindow
{
    // Opt-in MAP016 packaged acceptance: R18 removes old Map rows at startup.
    // Acquire fresh genuine Resource rows on home 2212 in THIS owned session,
    // then one owned Auto Resource acquisition on 2213, original-server return,
    // Clear 2213 through the actual WebView and reconstruct the Map page.
    // This does not click any game coordinate, claim, plunder, or account action.
    private async Task RunMap016SavedClearReopenProofAsync(
        CoreWebView2 core, string directory, CancellationToken token)
    {
        if (productionPaths?.ExplicitIsolation != true || map317CommandService is null ||
            mapAutoScanService is null || overviewLifecycleService is null ||
            Environment.GetEnvironmentVariable("LWBRIDGE_MAP016_SAVED_CLEAR_REOPEN") != "1")
            throw new InvalidOperationException("MAP016 Saved Clear requires the explicit isolated packaged owner.");

        const int home = 2212;
        const int remote = 2213;
        OverviewMapScanSession owner = overviewLifecycleService.GetReadyMapScanSession()
            ?? throw new InvalidDataException("MAP016 Saved Clear lacks a ready exact game owner.");
        void RequireOwner()
        {
            token.ThrowIfCancellationRequested();
            if (!overviewLifecycleService.MatchesOwnedMapScanSession(owner))
                throw new InvalidDataException("MAP016 Saved Clear original game session was replaced.");
        }
        bool pageReloadPending = false;
        async Task Wait(Func<Task<bool>> condition, string stage, int attempts = 300)
        {
            for (int i = 0; i < attempts; i++)
            {
                RequireOwner();
                try
                {
                    if (await condition()) return;
                }
                catch (COMException) when (pageReloadPending)
                {
                    // WebView2 script execution may be briefly unavailable
                    // while the already-owned WebView navigates on Reload.
                }
                await Task.Delay(100, token);
            }
            throw new TimeoutException("MAP016 Saved Clear did not settle: " + stage);
        }
        Task<bool> Dom(string script) => DomCore(script);
        async Task<bool> DomCore(string script) =>
            await core.ExecuteScriptAsync(script) == "true";

        async Task<int> ResourceTotal(int server)
        {
            RequireOwner();
            object? search = await map317CommandService.InvokeAsync(
                "map_search",
                JsonSerializer.SerializeToElement(
                    new { kind = "resource", query = new { serverId = server, page = 1, pageSize = 50 } },
                    JsonOptions.Default), token);
            JsonElement result = JsonSerializer.SerializeToElement(search, JsonOptions.Default);
            if (!result.TryGetProperty("rows", out JsonElement rows) ||
                rows.ValueKind != JsonValueKind.Array ||
                !result.TryGetProperty("total", out JsonElement total) ||
                !total.TryGetInt32(out int count) || count < 0)
                throw new InvalidDataException("Production Resource search did not return a valid saved record count.");
            return count;
        }
        async Task<int[]> Servers()
        {
            RequireOwner();
            object? response = await map317CommandService.InvokeAsync(
                "map_saved_servers", JsonSerializer.SerializeToElement(new { }, JsonOptions.Default), token);
            JsonElement result = JsonSerializer.SerializeToElement(response, JsonOptions.Default);
            if (!result.TryGetProperty("servers", out JsonElement values) || values.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("Production saved-server list was malformed.");
            return values.EnumerateArray().Select(x => x.GetInt32()).ToArray();
        }
        async Task Screenshot(string filename)
        {
            RequireOwner();
            await using var output = File.Create(Path.Combine(directory, filename));
            await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, output);
        }

        var stages = new List<object>();
        string receipt = Path.Combine(directory, "map016-saved-clear-reopen.json");
        string? homeRunId = null;
        async Task Save() =>
            await File.WriteAllTextAsync(receipt, JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                proof = "real_map016_saved_multi_server_clear_reopen",
                genuineGame = true,
                controlledProvider = false,
                owner.ProfileId, owner.SessionId, owner.GamePid, owner.GameStartedAtUtc,
                homeServer = home, remoteServer = remote, stages,
            }, new JsonSerializerOptions(JsonOptions.Default) { WriteIndented = true }), CancellationToken.None);

        // Never persist a native error's unbounded text, paths, target details,
        // challenge or request payload in this packaged acceptance receipt.
        // Preserve only a stable code derived from known product failure types.
        static string FailureCategory(string? error)
        {
            if (string.IsNullOrWhiteSpace(error)) return "missing_error_detail";
            if (error.Contains("Resource source hydration identity/configuration changed during acquisition.",
                    StringComparison.Ordinal))
                return "resource_hydration_source_identity_or_configuration_mismatch";
            if (error.Contains("Full-world Resource hydration validation failed:", StringComparison.Ordinal))
                return "resource_hydration_validation_failed";
            if (error.StartsWith("MAP_AUTO_SCAN_NOT_PUBLISHED:", StringComparison.Ordinal))
                return "remote_map_scan_not_published";
            if (error.StartsWith("MAP_AUTO_SCAN_OWNER_MISMATCH:", StringComparison.Ordinal))
                return "remote_map_scan_owner_mismatch";
            return "unclassified_auto_failure";
        }

        // The native writer already retains a first-failure receipt for this
        // exact scan. Report only bounded numeric provenance, never raw reply
        // content or fields such as rawReceiptLocator, challenge or error text.
        (string? ScanRunId, string? Status, object? Hydration) RemoteFailureEvidence()
        {
            if (!overviewLifecycleService.MatchesOwnedMapScanSession(owner))
                return (null, null, null);
            Map317.MapScanState recent = (Map317.MapScanState)map317CommandService.CreateStatus();
            string? runId = recent.ScanRunId;
            if (!Guid.TryParseExact(runId, "N", out _) ||
                string.Equals(runId, homeRunId, StringComparison.Ordinal))
                return (null, null, null);
            Map317.MapScanRun? persisted = map317CommandService.ReadPersistedScanRunForProof(runId);
            if (persisted is null || persisted.ServerId != remote ||
                persisted.SelectedTypes.Count != 1 || persisted.SelectedTypes[0] != "resource")
                return (null, null, null);
            string path = Path.Combine(overviewLifecycleService.LiveResourceRuntimeRoot,
                "resource-hydration-receipts", runId!, "first-failure.json");
            try
            {
                var file = new FileInfo(path);
                if (!file.Exists || file.Length is <= 0 or > 16_384)
                    return (runId, persisted.Status, null);
                using var document = JsonDocument.Parse(File.ReadAllBytes(path));
                JsonElement row = document.RootElement;
                if (row.GetProperty("schemaVersion").GetInt32() != 1 ||
                    row.GetProperty("scanRunId").GetString() != runId ||
                    row.GetProperty("serverId").GetInt32() != remote ||
                    row.GetProperty("phase").GetString() !=
                        "fullworld_resource_hydration_validation_failed")
                    return (runId, persisted.Status, null);
                static int? Count(JsonElement source, string field) =>
                    source.TryGetProperty(field, out JsonElement value) &&
                    value.ValueKind == JsonValueKind.Number &&
                    value.TryGetInt32(out int count) && count >= 0
                        ? count : null;
                return (runId, persisted.Status, new
                {
                    sourcePhase = "fullworld_resource_hydration_validation_failed",
                    nativeTargetCount = Count(row, "nativeTargetCount"),
                    nativeReadyCount = Count(row, "nativeReadyCount"),
                    nativeDetailCount = Count(row, "nativeDetailCount"),
                    currentIdleCount = Count(row, "currentIdleCount"),
                    observationTargetCount = Count(row, "observationTargetCount"),
                    observationReadyCount = Count(row, "observationReadyCount"),
                    observationDetailCount = Count(row, "observationDetailCount"),
                    observationHasError = row.TryGetProperty("observationError", out var nativeError) &&
                        nativeError.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined),
                });
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException
                or JsonException or InvalidOperationException or KeyNotFoundException)
            {
                // An unreadable/partial sidecar is not scan evidence. Do not
                // replace the original Auto failure with a diagnostic failure.
                return (runId, persisted.Status, null);
            }
        }

        int? live = overviewLifecycleService.GetLiveServerId();
        await Wait(() => Task.FromResult(overviewLifecycleService.GetLiveServerId() == home),
            "original home 2212 heartbeat", 450);
        MapAutoScanSnapshot initial = await mapAutoScanService.GetSnapshotAsync(token);
        if (initial.Running || initial.Config.Enabled || mapAutoScanService.IsCycleBusy ||
            initial.Config.ServerIds is { Count: > 0 })
            throw new InvalidDataException("Saved Clear proof requires a fresh disabled Auto schedule and no foreign target.");
        int initialHome = await ResourceTotal(home);
        int priorRemote = await ResourceTotal(remote);
        int[] beforeServers = await Servers();
        // Reimporting an old DB AFTER startup would defeat R18's session-only
        // admission. Insist on a genuinely empty published store and use the
        // actual Manual Map317 native scanner, not a fixture or DB mutation.
        if (initialHome != 0 || priorRemote != 0 || beforeServers.Length != 0 ||
            map317CommandService.IsScanActive)
            throw new InvalidDataException("Saved Clear needs R18-cleared Map data, idle Manual scan and no saved servers before acquiring home 2212.");
        string activePhase = "home_resource_acquisition";
        MapAutoScanSnapshot? observedAuto = null;
        try
        {
            RequireOwner();
            // Include slow/blocked status reads in the six-minute limit, not
            // just the 100-ms delays between polls. A timeout reaches finally
            // and stops only this accepted RunId under the transition gate.
            using var homeBudget = CancellationTokenSource.CreateLinkedTokenSource(token);
            homeBudget.CancelAfter(TimeSpan.FromMinutes(6));
            Map317.MapScanState homeStarted = (Map317.MapScanState)(await map317CommandService.InvokeAsync(
                "map_scan_start",
                JsonSerializer.SerializeToElement(new
                {
                    selectedTypes = new[] { "resource" },
                    scanMode = "fast",
                    resume = false,
                }, JsonOptions.Default), homeBudget.Token)
                ?? throw new InvalidDataException("Genuine home Resource scan returned no Map317 state."));
            homeRunId = homeStarted.ScanRunId;
            Map016SavedClearHomeRunProof.RequireStarted(homeStarted, home);

            Map317.MapScanState? homeCompleted = null;
            for (int attempt = 0; attempt < 3600; attempt++)
            {
                RequireOwner();
                var state = (Map317.MapScanState)(await map317CommandService.InvokeAsync(
                    "map_scan_status", JsonSerializer.SerializeToElement(new { }, JsonOptions.Default), homeBudget.Token)
                    ?? throw new InvalidDataException("Home Map317 scan status unavailable."));
                if (!string.Equals(state.ScanRunId, homeRunId, StringComparison.Ordinal) ||
                    state.ServerId != home || state.ServerIdSource != "live")
                    throw new InvalidDataException("Home Resource scan lost its exact live 2212 RunId.");
                if (!state.IsReading)
                {
                    Map016SavedClearHomeRunProof.RequireCompleted(state, homeStarted, home);
                    homeCompleted = state;
                    break;
                }
                if (state.Phase is "failed" or "cancelled" or "idle" or "completed")
                    throw new InvalidDataException("Home Resource scan claimed a terminal phase while still reading.");
                await Task.Delay(100, homeBudget.Token);
            }
            if (homeCompleted is null)
                throw new TimeoutException("Same-owned native home Resource scan did not finish within 360 seconds.");
            RequireOwner();
            Map317.MapScanRun? durable = map317CommandService.ReadPersistedScanRunForProof(homeRunId);
            Map016SavedClearHomeRunProof.RequireDurable(durable, homeRunId, home);

            // Preserve the actual game's independent native receipts BEFORE
            // the remote Auto scan can replace these source-owned file paths.
            string nativeRoot = overviewLifecycleService.LiveResourceRuntimeRoot;
            string nativePath = Path.Combine(nativeRoot, "camera-free-scan-result.json");
            string metricPath = Path.Combine(nativeRoot, "map-fullworld-probe-metrics.json");
            byte[] nativeBytes = await File.ReadAllBytesAsync(nativePath, token);
            byte[] metricBytes = await File.ReadAllBytesAsync(metricPath, token);
            using (JsonDocument native = JsonDocument.Parse(nativeBytes))
            using (JsonDocument metrics = JsonDocument.Parse(metricBytes))
                Map016SavedClearHomeRunProof.RequireNative(
                    native.RootElement, metrics.RootElement, homeRunId, home, owner);
            RequireOwner();
            const string homeNativeReceipt = "map016-home-native-resource-receipt.json";
            const string homeNativeMetrics = "map016-home-native-resource-metrics.json";
            await using (var copy = new FileStream(Path.Combine(directory, homeNativeReceipt),
                FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await copy.WriteAsync(nativeBytes, token);
            await using (var copy = new FileStream(Path.Combine(directory, homeNativeMetrics),
                FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await copy.WriteAsync(metricBytes, token);

            int original = await ResourceTotal(home);
            int[] homeServers = await Servers();
            if (original <= 0 || homeServers.Length != 1 || homeServers[0] != home ||
                await ResourceTotal(remote) != 0 || overviewLifecycleService.GetLiveServerId() != home)
                throw new InvalidDataException("New genuine home 2212 scan did not publish preserved Resource rows exclusively on home.");
            stages.Add(new {
                stage = "preserved_home", serverId = home, count = original, priorRemote,
                beforeServers = homeServers, live = overviewLifecycleService.GetLiveServerId(),
                initialHomeRows = initialHome, initialSavedServers = beforeServers,
                homeScanRunId = homeRunId, homeScan = homeCompleted,
                homeDurableStatus = durable!.Status,
                nativeAois = 10000, nativeReplies = 63, homeNativeReceipt, homeNativeMetrics,
                acquisition = "same_owned_session_genuine_manual_resource_after_r18_startup_clear",
            });
            await Save();

            // Existing production Auto service owns the native transition,
            // run token and return-to-home checks. No duplicate navigation
            // code or direct off-server camera request is introduced here.
            MapAutoScanSnapshot config = await mapAutoScanService.UpdateConfigAsync(
                initial.Config with
                {
                    Enabled = true,
                    IntervalMinutes = 20,
                    ServerIds = [remote],
                    SelectedTypes = ["resource"],
                    ScanMode = "fast",
                    ReturnToOriginalServer = true,
                }, token);
            if (config.Running || mapAutoScanService.IsCycleBusy ||
                !config.Config.Enabled || config.Config.ServerIds is not { Count: 1 } ||
                config.Config.ServerIds[0] != remote ||
                config.Config.SelectedTypes is not { Count: 1 } ||
                config.Config.SelectedTypes[0] != "resource" ||
                !config.Config.ReturnToOriginalServer)
                throw new InvalidDataException("Auto Resource-only 2213 configuration did not settle idle.");
            activePhase = "remote_auto_scan";
            DateTimeOffset requestedAt = DateTimeOffset.UtcNow;
            await mapAutoScanService.RunNowAsync(token);
            bool seenRunning = mapAutoScanService.IsCycleBusy, seenRemote = false;
            MapAutoScanSnapshot cycle = config;
            for (int i = 0; i < 6200; i++)
            {
                RequireOwner();
                cycle = await mapAutoScanService.GetSnapshotAsync(token);
                observedAuto = cycle;
                seenRunning |= cycle.Running || mapAutoScanService.IsCycleBusy;
                seenRemote |= overviewLifecycleService.GetLiveServerId() == remote;
                // A completed native/Auto failure is terminal, not something
                // to keep polling until the unrelated outer 8-minute UI proof
                // CTS fires. Distinguish this source failure in the receipt.
                if (!cycle.Running && !mapAutoScanService.IsCycleBusy &&
                    cycle.LastCycle is { } terminal &&
                    terminal.StartedAt >= requestedAt.ToUnixTimeMilliseconds() - 2000 &&
                    terminal.OriginalServerId == home &&
                    terminal.TargetServerIds.SequenceEqual([remote]) &&
                    terminal.Outcome != "completed")
                {
                    var source = RemoteFailureEvidence();
                    string category = FailureCategory(terminal.LastError ?? cycle.LastError);
                    stages.Add(new
                    {
                        stage = "remote_auto_terminal_failure",
                        outcome = terminal.Outcome,
                        errorCategory = category,
                        autoStartedAt = terminal.StartedAt,
                        autoCompletedAt = terminal.CompletedAt,
                        originalServerId = terminal.OriginalServerId,
                        targetServerIds = terminal.TargetServerIds,
                        completedServerIds = terminal.CompletedServerIds,
                        remoteScanRunId = source.ScanRunId,
                        remoteDurableStatus = source.Status,
                        hydration = source.Hydration,
                    });
                    await Save();
                    throw new InvalidDataException(
                        "MAP016 Saved Clear remote Auto ended before publication: " + category);
                }
                if (seenRunning && !cycle.Running && !mapAutoScanService.IsCycleBusy &&
                    cycle.LastCycle is { Outcome: "completed" } &&
                    cycle.LastCycle.StartedAt >= requestedAt.ToUnixTimeMilliseconds() - 2000 &&
                    cycle.LastCycle.OriginalServerId == home &&
                    cycle.LastCycle.TargetServerIds.SequenceEqual([remote]) &&
                    cycle.LastCycle.CompletedServerIds.SequenceEqual([remote]))
                    break;
                await Task.Delay(100, token);
            }
            if (!seenRunning || !seenRemote || cycle.Running || mapAutoScanService.IsCycleBusy ||
                cycle.LastCycle?.Outcome != "completed" || cycle.LastCycle.LastError is not null ||
                cycle.LastCycle.OriginalServerId != home ||
                !cycle.LastCycle.TargetServerIds.SequenceEqual([remote]) ||
                !cycle.LastCycle.CompletedServerIds.SequenceEqual([remote]))
                throw new InvalidDataException("Exact-owned Auto Resource 2213 cycle did not complete and restore.");
            await Wait(() => Task.FromResult(overviewLifecycleService.GetLiveServerId() == home),
                "Auto original server restored", 450);
            int scanned = await ResourceTotal(remote);
            int homeAfterScan = await ResourceTotal(home);
            int[] bothServers = await Servers();
            if (scanned <= 0 || homeAfterScan != original ||
                !bothServers.Contains(home) || !bothServers.Contains(remote))
                throw new InvalidDataException("Completed Auto scan did not publish two genuine saved servers.");
            stages.Add(new
            {
                stage = "two_server_auto_published", requestedAt,
                scan = cycle.LastCycle, observedRemote = seenRemote,
                liveServerId = overviewLifecycleService.GetLiveServerId(),
                homeRows = homeAfterScan, remoteRows = scanned, savedServers = bothServers,
            });
            await Save();

            activePhase = "remote_clear_and_webview_reopen";
            if (!await Dom("(()=>{const b=document.querySelectorAll('.map-scan-tabs button')[1];if(!b)return false;b.click();return true;})()"))
                throw new InvalidDataException("Actual packaged Auto tab is unavailable.");
            await Wait(() => Dom("!!document.querySelector('.map-auto-scan-card')"),
                "Auto tab mounted");
            await Wait(() => Dom("(()=>{const s=document.querySelector('select[data-map-saved-server]');return s&&[...s.options].some(o=>o.value==='2212')&&[...s.options].some(o=>o.value==='2213')&&[...s.options].some(o=>o.value==='all');})()"),
                "two saved-server choices and All servers", 400);
            if (!await Dom("(()=>{const s=document.querySelector('select[data-map-saved-server]');if(!s)return false;s.value='2213';s.dispatchEvent(new Event('change',{bubbles:true}));return true;})()"))
                throw new InvalidDataException("Could not select the saved 2213 Resource dataset.");
            await Wait(() => Dom("document.querySelector('select[data-map-saved-server]')?.value==='2213'"),
                "selected saved 2213");
            if (!await Dom("(()=>{const b=document.querySelectorAll('.map-tabs button')[1];if(!b)return false;b.click();return true;})()"))
                throw new InvalidDataException("Real saved-server Resource results tab is unavailable.");
            await Wait(() => Dom("!!document.querySelector('.map-table--resource[aria-busy=false]')"),
                "remote Resource saved table rendered");
            await Screenshot("map016-saved-before-clear.png");

            if (!await Dom("(()=>{const b=[...document.querySelectorAll('.map-auto-scan-options > button')].at(-1);if(!b||b.disabled)return false;b.click();return true;})()"))
                throw new InvalidDataException("Actual packaged Auto Clear button is unavailable or disabled.");
            await Wait(async () => await ResourceTotal(remote) == 0, "remote Clear persisted");
            int kept = await ResourceTotal(home);
            int[] cleared = await Servers();
            if (kept != original || !cleared.Contains(home) || cleared.Contains(remote))
                throw new InvalidDataException("Clear deleted home data or retained a phantom remote server.");
            await Wait(() => Dom("(()=>{const s=document.querySelector('select[data-map-saved-server]');return s&&[...s.options].some(o=>o.value==='2212')&&![...s.options].some(o=>o.value==='2213');})()"),
                "immediate UI saved dropdown refresh");
            await Screenshot("map016-saved-after-clear.png");
            stages.Add(new
            {
                stage = "clear_remote_preserve_home", clearedServerId = remote,
                remoteRows = 0, homeRows = kept, savedServers = cleared,
                liveServerId = overviewLifecycleService.GetLiveServerId(),
            });
            await Save();

            // An in-place real WebView reload remounts MapDataPage, forcing
            // its saved server hook to repopulate from the persisted backend.
            // It never creates a second game/app instance or clicks a game action.
            core.Reload();
            pageReloadPending = true;
            await Wait(() => Dom("!!document.querySelector('.map-panel .map-searchbar')&&!!document.querySelector('.map-scan-tabs')"),
                "Map page remounted after WebView reload", 450);
            await Wait(() => Dom("(()=>{const s=document.querySelector('select[data-map-saved-server]');return s&&[...s.options].some(o=>o.value==='2212')&&[...s.options].some(o=>o.value==='all')&&![...s.options].some(o=>o.value==='2213');})()"),
                "reloaded saved server list", 450);
            pageReloadPending = false;
            for (int scanTab = 0; scanTab < 2; scanTab++)
            {
                string selectTab = $"(()=>{{const b=document.querySelectorAll('.map-scan-tabs button')[{scanTab}];if(!b)return false;b.click();return true;}})()";
                if (!await Dom(selectTab))
                    throw new InvalidDataException("Reopened Map scan tab unavailable: " + scanTab);
                await Wait(() => Dom("(()=>{const s=document.querySelector('select[data-map-saved-server]');return s&&[...s.options].some(o=>o.value==='2212')&&[...s.options].some(o=>o.value==='all')&&![...s.options].some(o=>o.value==='2213');})()"),
                    "reopened Manual/Auto saved server list " + scanTab);
                await Screenshot(scanTab == 0 ? "map016-saved-reopened-manual.png" :
                    "map016-saved-reopened-auto.png");
            }
            RequireOwner();
            int[] reopened = await Servers();
            if (overviewLifecycleService.GetLiveServerId() != home ||
                (await ResourceTotal(remote)) != 0 || (await ResourceTotal(home)) != original ||
                !reopened.Contains(home) || reopened.Contains(remote))
                throw new InvalidDataException("Reopened Map UI failed original server or persisted Clear invariants.");
            await Screenshot("map016-saved-after-reopen.png");
            stages.Add(new
            {
                stage = "reopened_saved_dropdown", savedServers = reopened,
                absentServerId = remote, retainedServerId = home,
                retainedRows = original, liveServerId = home,
                reloadType = "same_exact_owned_app_webview",
            });
            await Save();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // The containing normal-UI proof uses one outer operation token,
            // typically eight minutes including initial login and home scan.
            // Its cancellation is not proof that the native source timed out.
            // Do not swallow cancellation or confuse it with an Auto success.
            var source = activePhase == "remote_auto_scan" ? RemoteFailureEvidence() : default;
            stages.Add(new
            {
                stage = "proof_operation_cancelled",
                activePhase,
                cancellationCategory = "outer_operation_token_cancelled",
                lastObservedAutoOutcome = observedAuto?.LastCycle?.Outcome,
                lastObservedAutoErrorCategory =
                    FailureCategory(observedAuto?.LastCycle?.LastError ?? observedAuto?.LastError),
                remoteScanRunId = source.ScanRunId,
                remoteDurableStatus = source.Status,
                hydration = source.Hydration,
            });
            await Save();
            throw;
        }
        finally
        {
            try
            {
                // The source-owned game may have retired while the page was
                // reloading. Never disable/cancel a successor's Auto owner.
                // Cleanup must still be possible if the proof operation token
                // expired, so use a separate bounded token after the owner check.
                using var cleanupBudget = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                void RequireCleanupOwner()
                {
                    if (!overviewLifecycleService.MatchesOwnedMapScanSession(owner))
                        throw new InvalidDataException("MAP016 Saved Clear original owner retired before cleanup; refusing successor mutation.");
                }
                RequireCleanupOwner();
                MapAutoScanSnapshot current = await mapAutoScanService.GetSnapshotAsync(cleanupBudget.Token);
                if (current.Config.Enabled)
                {
                    RequireCleanupOwner();
                    await mapAutoScanService.UpdateConfigAsync(
                        current.Config with { Enabled = false }, cleanupBudget.Token);
                }
                if (mapAutoScanService.IsCycleBusy)
                {
                    RequireCleanupOwner();
                    await mapAutoScanService.CancelAsync(cleanupBudget.Token);
                }
                RequireCleanupOwner();
                if (homeRunId is not null)
                {
                    // This method checks the exact active RunId while holding
                    // Map317's transition gate. Never Stop an independent
                    // Manual/Auto successor just because this proof timed out.
                    bool stoppedHome = await map317CommandService.StopAutoScanIfOwnedAsync(
                        homeRunId, cleanupBudget.Token);
                    if (stoppedHome &&
                        map317CommandService.ReadPersistedScanRunForProof(homeRunId)?.Status != "cancelled")
                        throw new InvalidDataException("MAP016 owned home Stop did not durably cancel the exact native run.");
                }
                RequireCleanupOwner();
                MapAutoScanSnapshot stopped = await mapAutoScanService.GetSnapshotAsync(cleanupBudget.Token);
                bool restored = !stopped.Running && !stopped.Config.Enabled &&
                    overviewLifecycleService.GetLiveServerId() == home;
                stages.Add(new { stage = "cleanup", restored, autoEnabled = stopped.Config.Enabled,
                    autoRunning = stopped.Running, liveServerId = overviewLifecycleService.GetLiveServerId() });
                if (!restored)
                    throw new InvalidDataException("MAP016 Save Clear cleanup did not prove original 2212 and disabled Auto.");
            }
            finally { await Save(); }
        }
    }
}

/// <summary>
/// Immutable admission predicates for MAP016's new same-session HOME scan.
/// Kept independent of WebView/game so an inert harness can exercise the
/// exact checks used before the remote Auto/Clear acceptance may proceed.
/// </summary>
internal static class Map016SavedClearHomeRunProof
{
    private static bool OnlyResource(IReadOnlyList<string> types) =>
        types.Count == 1 && types[0] == "resource";

    internal static void RequireStarted(Map317.MapScanState state, int home)
    {
        if (string.IsNullOrWhiteSpace(state.ScanRunId) ||
            state.ServerId != home || state.ServerIdSource != "live" ||
            !state.IsReading || !OnlyResource(state.SelectedTypes) ||
            state.ScanMode != "fast" || !state.NativeCaptureReady ||
            state.TotalBlocks != 2500 || state.FailedBlocks != 0 ||
            state.Error is not null)
            throw new InvalidDataException("Home Resource start was not accepted by the exact native 2500-block Map317 scanner.");
    }

    internal static void RequireCompleted(
        Map317.MapScanState state, Map317.MapScanState started, int home)
    {
        if (!string.Equals(state.ScanRunId, started.ScanRunId, StringComparison.Ordinal) ||
            state.ServerId != home || state.ServerIdSource != "live" ||
            state.IsReading || state.Phase != "completed" ||
            !OnlyResource(state.SelectedTypes) || state.ScanMode != "fast" ||
            !state.NativeCaptureReady || state.NativeDroppedRecords != 0 ||
            state.NativePendingRecords != 0 || state.TotalBlocks != 2500 ||
            state.CompletedBlocks != 2500 || state.ReadBlocks != 2500 ||
            state.FailedBlocks != 0 || state.UnreadBlocks != 0 ||
            state.InflightBlocks != 0 || state.Error is not null ||
            state.StartedAt != started.StartedAt)
            throw new InvalidDataException("Exact home Resource run did not durably complete 2500/2500 native blocks without losses.");
    }

    internal static void RequireDurable(Map317.MapScanRun? run, string runId, int home)
    {
        if (run is null || run.Id != runId || run.ServerId != home ||
            !OnlyResource(run.SelectedTypes) || run.Status != "completed" ||
            run.TotalBlocks != 2500 || run.CompletedBlocks != 2500 ||
            run.FailedBlocks != 0 || run.Error is not null ||
            run.CreatedAt <= 0 || run.UpdatedAt < run.CreatedAt)
            throw new InvalidDataException("Home Resource scan lacks its exact genuine completed SQLite Map317 run.");
    }

    internal static void RequireNative(
        JsonElement receipt, JsonElement metrics, string runId, int home,
        OverviewMapScanSession owner)
    {
        if (receipt.ValueKind != JsonValueKind.Object ||
            receipt.GetProperty("schemaVersion").GetInt32() != 1 ||
            receipt.GetProperty("probeVersion").GetString() != "lwbridge-live-resource-probe-2" ||
            receipt.GetProperty("state").GetString() != "proven" ||
            receipt.GetProperty("scanRunId").GetString() != runId ||
            receipt.GetProperty("profileId").GetString() != owner.ProfileId ||
            receipt.GetProperty("launchSessionId").GetString() != owner.SessionId ||
            receipt.GetProperty("challenge").GetString() != owner.Challenge ||
            receipt.GetProperty("gamePid").GetInt32() != owner.GamePid ||
            receipt.GetProperty("serverId").GetInt32() != home ||
            receipt.GetProperty("worldId").GetInt32() != 0 ||
            !receipt.GetProperty("requestId").GetString()!.StartsWith("direct", StringComparison.Ordinal) ||
            receipt.GetProperty("cameraStable").ValueKind != JsonValueKind.True ||
            receipt.GetProperty("curViewRestored").ValueKind != JsonValueKind.True ||
            receipt.GetProperty("completedAois").GetInt32() != 10000 ||
            receipt.GetProperty("repliesCaptured").GetInt32() != 63 ||
            receipt.GetProperty("requestsSent").GetInt32() is < 63 or > 65)
            throw new InvalidDataException("Home Resource native receipt has wrong owner, run, camera restoration or 10000-AOI direct coverage.");
        JsonElement batches = receipt.GetProperty("batches");
        if (batches.ValueKind != JsonValueKind.Array || batches.GetArrayLength() != 63)
            throw new InvalidDataException("Home Resource native receipt lacks 63 source replies.");
        var covered = new HashSet<int>();
        foreach (JsonElement batch in batches.EnumerateArray())
        {
            JsonElement indices = batch.GetProperty("indices");
            if (indices.ValueKind != JsonValueKind.Array ||
                indices.GetArrayLength() is < 1 or > 160)
                throw new InvalidDataException("Home Resource native AOI batch has an invalid footprint.");
            foreach (JsonElement index in indices.EnumerateArray())
            {
                if (!index.TryGetInt32(out int cell) ||
                    cell is < 0 or >= 10000 || !covered.Add(cell))
                    throw new InvalidDataException("Home Resource native AOIs overlap or have an invalid identity.");
            }
        }
        if (covered.Count != 10000)
            throw new InvalidDataException("Home Resource native source omitted AOIs.");
        if (metrics.ValueKind != JsonValueKind.Object ||
            metrics.GetProperty("source").GetString() != "CurrentClientMapBlockSource.ProbeCameraFreeMapAsync" ||
            metrics.GetProperty("phase").GetString() != "prepared" ||
            metrics.GetProperty("validatedProbeResponses").GetInt32() != 63 ||
            metrics.GetProperty("requiredAois").GetInt32() != 10000 ||
            metrics.GetProperty("uniqueCoveredAois").GetInt32() != 10000 ||
            metrics.GetProperty("acquisitionPolicy").GetString() != "direct")
            throw new InvalidDataException("Home Resource native prepared metrics are absent or not direct.");
        JsonElement identity = metrics.GetProperty("identity");
        if (identity.GetProperty("RunId").GetString() != runId ||
            identity.GetProperty("ServerId").GetInt32() != home ||
            identity.GetProperty("WorldId").GetInt32() != 0 ||
            identity.GetProperty("SelectedTypesKey").GetString() != "resource" ||
            identity.GetProperty("ScanMode").GetString() != "fast")
            throw new InvalidDataException("Home Resource prepared native metrics refer to a different run or selection.");
    }
}
