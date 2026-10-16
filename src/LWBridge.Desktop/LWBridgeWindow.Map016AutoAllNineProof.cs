using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace LWBridge.Desktop;

internal sealed partial class LWBridgeWindow
{
    // MAP-016 opt-in, isolated, REAL Auto Run Now proof. This is deliberately
    // separate from Manual Start and cannot be called by ordinary app startup.
    // The normal proof dispatcher must explicitly gate this method on
    // LWBRIDGE_MAP016_AUTO_ALL_NINE=1 after obtaining the owned game session.
    private async Task RunMap016AutoAllNineUiProofAsync(
        CoreWebView2 core, string outputPath, string directory,
        OverviewMapScanSession proofSession, CancellationToken token)
    {
        if (productionPaths?.ExplicitIsolation != true || mapAutoScanService is null ||
            map317CommandService is null || overviewLifecycleService is null)
            throw new InvalidOperationException("MAP016 Auto all-nine proof requires isolated production owners.");

        const int homeServer = 2212;
        const int targetServer = 2211;
        string[] kinds =
            ["city", "resource", "monster", "truck", "railway", "dispatch", "ghost", "treasure", "zombie_boss"];
        bool ExactlyNineTypes(IReadOnlyList<string>? types) => types is not null &&
            types.Count == kinds.Length &&
            types.Distinct(StringComparer.Ordinal).Count() == kinds.Length &&
            types.ToHashSet(StringComparer.Ordinal).SetEquals(kinds);
        bool launchedByProof = false;
        int runNowClicks = 0;
        DateTimeOffset? triggeredAt = null;
        string? runId = null;
        void RequireOwner()
        {
            if (!overviewLifecycleService.MatchesOwnedMapScanSession(proofSession))
                throw new InvalidDataException("MAP016 Auto all-nine proof lost its exact owned game session.");
        }
        async Task<bool> Dom(string script) => await core.ExecuteScriptAsync(script) == "true";
        async Task RequireUi(Func<Task<bool>> condition, string stage, int polls = 300)
        {
            for (int i = 0; i < polls; i++)
            {
                token.ThrowIfCancellationRequested();
                RequireOwner();
                if (await condition().ConfigureAwait(true)) return;
                await Task.Delay(100, token);
            }
            throw new TimeoutException("MAP016 Auto all-nine UI never settled: " + stage);
        }

        RequireOwner();
        await RequireUi(() => Task.FromResult(overviewLifecycleService.GetLiveServerId() == homeServer),
            "initial home server 2212", 450);
        MapAutoScanSnapshot initialAuto = await mapAutoScanService.GetSnapshotAsync(token);
        JsonElement initialManual = JsonSerializer.SerializeToElement(
            map317CommandService.CreateStatus(), JsonOptions.Default);
        if (initialAuto.Config.Enabled || initialAuto.Running || mapAutoScanService.IsCycleBusy ||
            initialAuto.Config.ServerIds is { Count: > 0 } ||
            initialManual.TryGetProperty("isReading", out JsonElement reading) &&
            reading.ValueKind == JsonValueKind.True)
            throw new InvalidDataException("MAP016 Auto proof requires idle, disabled and fresh isolated scan owners.");
        var originalCycle = initialAuto.LastCycle;
        try
        {
            if (!await Dom("(()=>{const b=document.querySelectorAll('.map-scan-tabs button')[1];if(!b)return false;b.click();return true;})()"))
                throw new InvalidDataException("MAP016 production Auto tab is missing.");
            await RequireUi(() => Dom("!!document.querySelector('.map-auto-scan-card')"),
                "production Auto card");
            if (!await Dom("(()=>{const i=document.querySelector('.map-auto-scan-server-input input');if(!i)return false;Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(i,'2211');i.dispatchEvent(new Event('input',{bubbles:true}));return true;})()"))
                throw new InvalidDataException("MAP016 Auto target server input is missing.");
            await RequireUi(() => Dom("!!document.querySelector('.map-auto-scan-server-input button:not(:disabled)')"),
                "target server Add");
            if (!await Dom("(()=>{const b=document.querySelector('.map-auto-scan-server-input button:not(:disabled)');if(!b)return false;b.click();return true;})()"))
                throw new InvalidDataException("MAP016 Auto target server Add failed.");
            await RequireUi(() => Dom("[...document.querySelectorAll('.map-auto-scan-server-chips > span')].some(x=>x.textContent.includes('2211'))"),
                "persisted target 2211");

            // Each React click closes over the previous render's config. A
            // forEach of multiple clicks in one ExecuteScriptAsync silently
            // overwrites earlier selections. Commit and observe EACH native
            // updated kind before touching the next checkbox.
            for (int index = 0; index < 10; index++)
            {
                if (index == 8) continue;
                string select = $"(()=>{{const b=document.querySelectorAll('.map-auto-scan-card .map-category-menu input[type=checkbox]')[{index}];if(!b)return false;if(!b.checked)b.click();return true;}})()";
                if (!await Dom(select))
                    throw new InvalidDataException("MAP016 Auto category checkbox missing: " + index);
                int selectedIndex = index;
                await RequireUi(async () =>
                {
                    var snapshot = await mapAutoScanService.GetSnapshotAsync(token);
                    return snapshot.Config.SelectedTypes?.Contains(kinds[selectedIndex < 8 ? selectedIndex : selectedIndex - 1],
                        StringComparer.Ordinal) == true &&
                        await Dom($"document.querySelectorAll('.map-auto-scan-card .map-category-menu input[type=checkbox]')[{selectedIndex}]?.checked===true");
                }, "native persisted Auto category " + index);
            }
            if (!await Dom("(()=>{const b=document.querySelectorAll('.map-auto-scan-card .map-category-menu input[type=checkbox]')[8];if(!b)return false;if(b.checked)b.click();return true;})()"))
                throw new InvalidDataException("MAP016 Auto Other category deselection failed.");
            await RequireUi(() => Dom("(()=>{const a=[...document.querySelectorAll('.map-auto-scan-card .map-category-menu input[type=checkbox]')];return a.length===10&&a.every((b,i)=>b.checked===(i!==8));})()"),
                "exact nine Auto categories");
            if (!await Dom("(()=>{const b=document.querySelector('.map-auto-return input[type=checkbox]');if(!b)return false;if(!b.checked)b.click();return true;})()"))
                throw new InvalidDataException("MAP016 Auto original-server return toggle missing.");
            if (!await Dom("(()=>{const b=document.querySelector('.map-auto-scan-master input[type=checkbox]');if(!b)return false;if(!b.checked)b.click();return true;})()"))
                throw new InvalidDataException("MAP016 Auto master enable toggle missing.");

            MapAutoScanSnapshot configured = initialAuto;
            await RequireUi(async () =>
            {
                configured = await mapAutoScanService.GetSnapshotAsync(token);
                return configured.Config.Enabled && configured.Config.ScanMode == "fast" &&
                    configured.Config.ReturnToOriginalServer &&
                    configured.Config.ServerIds is { Count: 1 } &&
                    configured.Config.ServerIds[0] == targetServer &&
                    ExactlyNineTypes(configured.Config.SelectedTypes);
            }, "native persisted nine-kind configuration");
            RequireOwner();
            if (configured.Running || mapAutoScanService.IsCycleBusy ||
                configured.LastCycle != originalCycle ||
                configured.Config.NextRunAt <= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() ||
                overviewLifecycleService.GetLiveServerId() != homeServer)
                throw new InvalidDataException("MAP016 Auto ran by interval or changed server before explicit Run Now.");

            // Exactly ONE click of the actual Auto UI action, not the Manual
            // Start button and not a scheduler CheckDue synthetic call.
            await RequireUi(() => Dom(
                "!!document.querySelector('.map-auto-scan-options button.primary:not(:disabled)')"),
                "real Auto Run Now button enabled after exact persisted configuration", 450);
            triggeredAt = DateTimeOffset.UtcNow;
            if (!await Dom("(()=>{const b=document.querySelector('.map-auto-scan-options button.primary:not(:disabled)');if(!b)return false;b.click();return true;})()"))
                throw new InvalidDataException("MAP016 explicit Auto Run Now button unavailable.");
            runNowClicks++;
            launchedByProof = true;

            bool sawRunning = false;
            bool sawRemote = false;
            MapAutoScanSnapshot cycle = configured;
            for (int poll = 0; poll < 6200; poll++)
            {
                token.ThrowIfCancellationRequested();
                RequireOwner();
                cycle = await mapAutoScanService.GetSnapshotAsync(token);
                if (cycle.Running) sawRunning = true;
                if (overviewLifecycleService.GetLiveServerId() == targetServer) sawRemote = true;
                if (sawRunning && !cycle.Running && cycle.LastCycle is { StartedAt: > 0 } &&
                    cycle.LastCycle.StartedAt >= triggeredAt.Value.ToUnixTimeMilliseconds() - 2000)
                    break;
                await Task.Delay(100, token);
            }
            if (runNowClicks != 1 || !sawRunning || !sawRemote || cycle.Running ||
                cycle.LastCycle?.Outcome != "completed" || cycle.LastCycle.LastError is not null ||
                cycle.LastCycle.OriginalServerId != homeServer ||
                !cycle.LastCycle.TargetServerIds.SequenceEqual([targetServer]) ||
                !cycle.LastCycle.CompletedServerIds.SequenceEqual([targetServer]))
                throw new InvalidDataException("MAP016 Auto Run Now did not publish exactly the intended remote target.");
            await RequireUi(() => Task.FromResult(overviewLifecycleService.GetLiveServerId() == homeServer),
                "original server restored after Auto Run Now", 450);
            RequireOwner();

            string fastTruckPath = Path.Combine(overviewLifecycleService.LiveResourceRuntimeRoot,
                "fast-truck-scan-result.json");
            using JsonDocument nativeTruck = JsonDocument.Parse(await File.ReadAllTextAsync(fastTruckPath, token));
            JsonElement truck = nativeTruck.RootElement;
            runId = ReadStatusString(truck, "scanRunId");
            if (string.IsNullOrWhiteSpace(runId) || ReadStatusString(truck, "state") != "proven" ||
                ReadStatusString(truck, "profileId") != proofSession.ProfileId ||
                ReadStatusString(truck, "launchSessionId") != proofSession.SessionId ||
                ReadStatusString(truck, "challenge") != proofSession.Challenge ||
                ReadStatusInt32(truck, "gamePid") != proofSession.GamePid ||
                ReadStatusInt32(truck, "serverId") != targetServer ||
                ReadStatusInt32(truck, "rectanglesCompleted") != 625 ||
                ReadStatusInt32(truck, "unprovenRectangles") != 0 ||
                ReadStatusInt32(truck, "initialRequestsSent") != 625 ||
                ReadStatusInt32(truck, "railwayRecordCount") is null ||
                truck.GetProperty("railwayIncluded").ValueKind != JsonValueKind.True ||
                truck.GetProperty("cameraStable").ValueKind != JsonValueKind.True ||
                truck.GetProperty("curViewRestored").ValueKind != JsonValueKind.True)
                throw new InvalidDataException("MAP016 Auto did not retain its exact native Truck/Railway source.");

            LWBridge.Map317.MapScanRun persisted = map317CommandService.ReadPersistedScanRunForProof(runId)
                ?? throw new InvalidDataException("MAP016 Auto run not found in native SQLite store.");
            if (persisted.Id != runId || persisted.ServerId != targetServer ||
                persisted.Status != "completed" || persisted.Error is not null ||
                !ExactlyNineTypes(persisted.SelectedTypes) ||
                persisted.TotalBlocks != 2500 || persisted.CompletedBlocks != 2500 ||
                persisted.FailedBlocks != 0)
                throw new InvalidDataException("MAP016 Auto published incomplete or wrong SQLite scan run.");

            JsonElement completedStatus = JsonSerializer.SerializeToElement(new
            {
                scanRunId = persisted.Id, serverId = persisted.ServerId,
                phase = persisted.Status, selectedTypes = persisted.SelectedTypes,
                totalBlocks = persisted.TotalBlocks, completedBlocks = persisted.CompletedBlocks,
                readBlocks = persisted.CompletedBlocks, failedBlocks = persisted.FailedBlocks,
                unreadBlocks = persisted.TotalBlocks - persisted.CompletedBlocks - persisted.FailedBlocks,
                scanMode = "fast", isReading = false,
            }, JsonOptions.Default);

            // Auto has returned home; browse the target via Saved Server, not by
            // moving back to 2211. Reuse the exact nine-category native/source
            // and mounted-UI verifier already accepted for Manual all-nine.
            await RequireUi(() => Dom("[...document.querySelectorAll('select[data-map-saved-server] option')].some(o=>o.value==='2211')"),
                "published target Saved Server");
            if (!await Dom("(()=>{const s=document.querySelector('select[data-map-saved-server]');if(!s)return false;s.value='2211';s.dispatchEvent(new Event('change',{bubbles:true}));return true;})()"))
                throw new InvalidDataException("MAP016 Auto cannot select target Saved Server.");
            await RequireUi(() => Dom("document.querySelector('select[data-map-saved-server]')?.value==='2211'"),
                "saved remote 2211 selection");
            object browse = await RunMap015AllNonTruckUiProofAsync(
                core, directory, completedStatus, proofSession, token, includeTruck: true);

            string metricsPath = Path.Combine(overviewLifecycleService.LiveResourceRuntimeRoot,
                "map-engine-stage-metrics.json");
            using JsonDocument stageDocument = JsonDocument.Parse(
                await File.ReadAllTextAsync(metricsPath, token));
            JsonElement stage = stageDocument.RootElement;
            if (ReadStatusString(stage, "runId") != runId ||
                ReadStatusInt32(stage, "serverId") != targetServer ||
                ReadStatusString(stage, "scanMode") != "fast" ||
                !stage.TryGetProperty("wholeEngineMilliseconds", out JsonElement totalDuration) ||
                !totalDuration.TryGetDouble(out double milliseconds) || milliseconds <= 0)
                throw new InvalidDataException("MAP016 Auto native timing record does not belong to the completed run.");

            MapAutoScanSnapshot cleanup = await mapAutoScanService.UpdateConfigAsync(
                cycle.Config with { Enabled = false }, token);
            if (cleanup.Config.Enabled || cleanup.Running || mapAutoScanService.IsCycleBusy ||
                overviewLifecycleService.GetLiveServerId() != homeServer)
                throw new InvalidDataException("MAP016 Auto did not return to its disabled original-server state.");
            await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(new
            {
                schemaVersion = 1, state = "proven",
                proof = "actual_packaged_ui_auto_all_nine_run_now_fullworld_scan_and_results",
                genuineGame = true, controlledProvider = false,
                selection = "auto-all-nine", selectedTypes = kinds,
                serverId = targetServer, profileId = proofSession.ProfileId,
                gamePid = proofSession.GamePid, completedStatus, browse,
                auto = new
                {
                    originalServerId = homeServer, targetServerId = targetServer,
                    restoredServerId = homeServer, runNowClicks,
                    noIntervalRunBeforeExplicit = true, sawRunning, sawRemote,
                    configured.Config.ServerIds, configured.Config.SelectedTypes,
                    configured.Config.ReturnToOriginalServer, configured.Config.ScanMode,
                    triggeredAt, cycle.LastCycle,
                    cleanupEnabled = cleanup.Config.Enabled,
                    cleanupRunning = cleanup.Running
                },
                timings = stage.Clone()
            }, new JsonSerializerOptions(JsonOptions.Default) { WriteIndented = true }), token);
        }
        finally
        {
            // If this particular proof armed Auto, cancel only that owned Auto
            // cycle on failure. The scheduler itself must settle Stop and
            // ownership-scoped original-server return; never create a second
            // independent map/game controller.
            if (mapAutoScanService is not null && overviewLifecycleService.MatchesOwnedMapScanSession(proofSession))
            {
                try
                {
                    if (launchedByProof && mapAutoScanService.IsCycleBusy)
                        await mapAutoScanService.CancelAsync(CancellationToken.None);
                    MapAutoScanSnapshot current = await mapAutoScanService.GetSnapshotAsync(CancellationToken.None);
                    if (current.Config.Enabled)
                        await mapAutoScanService.UpdateConfigAsync(
                            current.Config with { Enabled = false }, CancellationToken.None);
                }
                catch (Exception error)
                {
                    Console.Error.WriteLine("MAP016_AUTO_ALL_NINE_CLEANUP_FAILED: " + error.Message);
                }
            }
        }
    }
}
