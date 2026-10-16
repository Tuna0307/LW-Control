using System.Text.Json;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;

namespace LWBridge.Desktop;

internal sealed partial class LWBridgeWindow
{
    // Opt-in MAP016 packaged acceptance: genuine preserved 2212 Resource data,
    // one owned Auto Resource acquisition on 2213, original-server return, then
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

        int? live = overviewLifecycleService.GetLiveServerId();
        await Wait(() => Task.FromResult(overviewLifecycleService.GetLiveServerId() == home),
            "original home 2212 heartbeat", 450);
        MapAutoScanSnapshot initial = await mapAutoScanService.GetSnapshotAsync(token);
        if (initial.Running || initial.Config.Enabled || mapAutoScanService.IsCycleBusy ||
            initial.Config.ServerIds is { Count: > 0 })
            throw new InvalidDataException("Saved Clear proof requires a fresh disabled Auto schedule and no foreign target.");
        int original = await ResourceTotal(home);
        int priorRemote = await ResourceTotal(remote);
        int[] beforeServers = await Servers();
        if (original <= 0 || priorRemote != 0 || !beforeServers.Contains(home) ||
            beforeServers.Contains(remote))
            throw new InvalidDataException("Saved Clear requires genuine preserved home 2212 rows and no pre-existing 2213 data.");
        stages.Add(new { stage = "preserved_home", serverId = home, count = original, priorRemote, beforeServers, live });
        await Save();

        try
        {
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
            DateTimeOffset requestedAt = DateTimeOffset.UtcNow;
            await mapAutoScanService.RunNowAsync(token);
            bool seenRunning = mapAutoScanService.IsCycleBusy, seenRemote = false;
            MapAutoScanSnapshot cycle = config;
            for (int i = 0; i < 6200; i++)
            {
                RequireOwner();
                cycle = await mapAutoScanService.GetSnapshotAsync(token);
                seenRunning |= cycle.Running || mapAutoScanService.IsCycleBusy;
                seenRemote |= overviewLifecycleService.GetLiveServerId() == remote;
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
