using System.Diagnostics;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace LWBridge.Desktop;

internal sealed partial class LWBridgeWindow
{
    // Isolated, opt-in DOM/native observations. Never emits OS input or invokes
    // Land, shields, event claims, relocation or an equipment/building edit.
    private async Task RunFeature018R29PerformanceProofAsync(CoreWebView2 core,
        string outputPath, string directory, OverviewMapScanSession owner,
        string uid, int serverId, CancellationToken token, bool scrollOnly = false, bool memoryOnly = false)
    {
        var lifecycle = overviewLifecycleService!;
        var approved = new Feature018HotkeySession(owner.ProfileId, owner.SessionId, uid, serverId);
        bool IsOwner() => Feature018IsApprovedAccount(uid, serverId) &&
            lifecycle.OwnsExactProcess(owner.GamePid) && lifecycle.MatchesOwnedMapScanSession(owner) &&
            ReadFeature018AuthenticatedAccount(lifecycle, owner) == approved;
        var game = new Feature018GameCommandService(new Feature018OwnedGameExchange(lifecycle),
            equipmentConfigService, new Feature018AuthenticatedGameAccount(uid, serverId), profileRuntimeConfigStore);
        using var empty = JsonDocument.Parse("{}");
        var phases = new List<object>();
        string phaseFile = Path.Combine(directory, "r29-perf-phase.json");
        async Task<JsonElement?> NativeMetrics()
        {
            try
            {
                using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "overview-bridge", "r29-perf.json"), token));
                return doc.RootElement.Clone();
            }
            catch (IOException) { return null; }
            catch (JsonException) { return null; }
        }
        async Task Hold(string phase, int seconds)
        {
            if (!IsOwner()) throw new InvalidDataException("R29 performance lost exact owner before " + phase);
            var before = await NativeMetrics();
            string heapBefore = await core.CallDevToolsProtocolMethodAsync("Runtime.getHeapUsage", "{}");
            await File.WriteAllTextAsync(phaseFile, JsonSerializer.Serialize(new { phase, seconds, startedAtUtc = DateTimeOffset.UtcNow }, JsonOptions.Default), token);
            var timer = Stopwatch.StartNew();
            for (int i = 0; i < seconds; i++)
            {
                token.ThrowIfCancellationRequested();
                if (!lifecycle.OwnsExactProcess(owner.GamePid) || !lifecycle.MatchesOwnedMapScanSession(owner))
                    throw new InvalidDataException("R29 performance exact session ended during " + phase);
                await Task.Delay(1000, token);
            }
            phases.Add(new { phase, seconds = timer.Elapsed.TotalSeconds, nativeBefore = before,
                nativeAfter = await NativeMetrics(), heapBefore,
                heapAfter = await core.CallDevToolsProtocolMethodAsync("Runtime.getHeapUsage", "{}") });
            await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(new { state = "running", phases }, JsonOptions.Default), token);
        }
        async Task Scroll(string surface)
        {
            await core.ExecuteScriptAsync("""
                (()=>{window.__r29Scroll={done:false,times:[],count:0,changed:0};
                const candidates=[document.querySelector('.city-layout-grid-viewport'),document.querySelector('.map-table-scroll'),document.querySelector('.main-view'),document.scrollingElement].filter(n=>n&&n.getClientRects().length>0);
                const node=candidates.find(n=>n.scrollHeight>n.clientHeight+5||n.scrollWidth>n.clientWidth+5)||candidates[0];
                const origin=node.scrollTop,originX=node.scrollLeft;let prior=performance.now(),started=prior;
                window.__r29Scroll.container=node.className||node.tagName;
                window.__r29Scroll.maxY=node.scrollHeight-node.clientHeight;
                window.__r29Scroll.maxX=node.scrollWidth-node.clientWidth;
                function step(now){const s=window.__r29Scroll;s.times.push(now-prior);prior=now;s.count++;
                  const y=node.scrollTop,x=node.scrollLeft;
                  node.scrollTop=Math.max(0,(Math.sin((now-started)/350)+1)/2*(node.scrollHeight-node.clientHeight));
                  node.scrollLeft=Math.max(0,(Math.sin((now-started)/350)+1)/2*(node.scrollWidth-node.clientWidth));
                  if(node.scrollTop!==y||node.scrollLeft!==x)s.changed++;
                  if(now-started<2000)requestAnimationFrame(step);else{node.scrollTop=origin;node.scrollLeft=originX;s.done=true;}}
                requestAnimationFrame(step);return true;})()
                """);
            await R29SweepWaitDomAsync(core, "window.__r29Scroll?.done===true", surface + " scroll", token, IsOwner);
            string timing = await core.ExecuteScriptAsync("window.__r29Scroll");
            using var timingDoc = JsonDocument.Parse(timing);
            if (timingDoc.RootElement.GetProperty("changed").GetInt32() < 2)
                throw new InvalidDataException("R29 scroll sample did not move a scroll container: " + surface);
            phases.Add(new { phase = surface + "-scroll", timing = JsonDocument.Parse(timing).RootElement.Clone(),
                dom = JsonDocument.Parse(await core.ExecuteScriptAsync("(()=>{const count=s=>[...document.querySelectorAll(s)].filter(n=>n.getClientRects().length>0).length;return {mapRows:count('.map-table tbody tr'),buildings:count('[data-city-building-id]'),renderedCells:count('.city-layout-cell'),equipmentPositions:count('[data-equipment-position]')};})()")).RootElement.Clone() });
        }
        await R29SweepSwitchLanguageAsync(core, "en", token, IsOwner);
        await R29SweepSetViewportAsync(core, 1600, token, IsOwner);
        if (memoryOnly)
        {
            await R29SweepNavigateAsync(core, R29SweepSurfaces.First(s => s.Key == "home"), token, IsOwner);
            await Hold("memory-idle-30m", 1800);
            await R29SweepClearViewportAsync(core);
            await File.WriteAllTextAsync(phaseFile, "{\"phase\":\"finished\"}", token);
            await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(new { state = "proven", uid, serverId,
                osInputUsed = false, phases, finishedAtUtc = DateTimeOffset.UtcNow }, new JsonSerializerOptions(JsonOptions.Default) { WriteIndented = true }), token);
            return;
        }
        if (scrollOnly)
        {
            foreach (string surface in new[] { "squads-equipment", "map-data", "city-layout" })
            {
                double elapsed = await R29SweepNavigateAsync(core, R29SweepSurfaces.First(s => s.Key == surface), token, IsOwner);
                phases.Add(new { phase = surface + "-open", elapsedMilliseconds = elapsed });
                await Scroll(surface);
            }
            await R29SweepClearViewportAsync(core);
            await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(new { state = "proven", phases }, new JsonSerializerOptions(JsonOptions.Default) { WriteIndented = true }), token);
            return;
        }
        await R29SweepNavigateAsync(core, R29SweepSurfaces.First(s => s.Key == "home"), token, IsOwner);
        await Hold("idle-home", 60);
        await R29SweepNavigateAsync(core, R29SweepSurfaces.First(s => s.Key == "squads-equipment"), token, IsOwner);
        await Hold("equipment-polling", 45);
        await Scroll("equipment");
        await R29SweepNavigateAsync(core, R29SweepSurfaces.First(s => s.Key == "squads-afk"), token, IsOwner);
        phases.Add(new { phase = "afk-native-observation", catalog = await game.InvokeAsync("monster_catalog_options", empty.RootElement, token),
            status = await game.InvokeAsync("feature018_afk_status", empty.RootElement, token) });
        await Hold("afk-status-polling", 45);
        // A read-only Chat status configures the listener from the isolated
        // profile's saved settings. No Run/claim operation is issued.
        phases.Add(new { phase = "chat-listener", status = await game.InvokeAsync("feature018_chat_status", empty.RootElement, token) });
        await Hold("chat-listener", 45);
        await R29SweepNavigateAsync(core, R29SweepSurfaces.First(s => s.Key == "map-data"), token, IsOwner);
        await core.ExecuteScriptAsync("document.querySelector('.map-scan-tabs button')?.click()");
        await Task.Delay(100, token);
        await core.ExecuteScriptAsync("(()=>{const b=document.querySelector('.map-actions button.primary');if(!b||b.disabled)return false;b.click();return true;})()");
        await Hold("map-scan", 60);
        await core.ExecuteScriptAsync("(()=>{const b=document.querySelector('.map-actions button.danger');if(b&&!b.disabled)b.click();return true;})()");
        await R29SweepWaitDomAsync(core, "!document.querySelector('.map-actions button.danger:not(:disabled)')", "performance scan Stop", token, IsOwner, 600);
        await Scroll("map");
        await R29SweepNavigateAsync(core, R29SweepSurfaces.First(s => s.Key == "city-layout"), token, IsOwner);
        await Scroll("city");
        await R29SweepNavigateAsync(core, R29SweepSurfaces.First(s => s.Key == "home"), token, IsOwner);
        // Real thirty-minute run, without forced GC. The external passive
        // sampler records the full process tree while this phase is active.
        await Hold("memory-idle-30m", 1800);
        await R29SweepClearViewportAsync(core);
        await File.WriteAllTextAsync(phaseFile, "{\"phase\":\"finished\"}", token);
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(new { state = "proven", uid, serverId,
            osInputUsed = false, phases, finishedAtUtc = DateTimeOffset.UtcNow }, new JsonSerializerOptions(JsonOptions.Default) { WriteIndented = true }), token);
    }
}
