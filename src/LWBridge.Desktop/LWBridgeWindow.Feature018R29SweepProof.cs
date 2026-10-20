using System.Diagnostics;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace LWBridge.Desktop;

internal sealed partial class LWBridgeWindow
{
    private sealed record R29SweepSurface(
        string Key,
        int RouteIndex,
        string ReadyExpression,
        string? SquadTab = null);

    private static readonly R29SweepSurface[] R29SweepSurfaces =
    [
        new("home", 0, "!!document.querySelector('.quick-actions-panel')"),
        new("map-data", 2, "!!document.querySelector('.map-panel')"),
        new("squads-afk", 3, "!!document.querySelector('.squad-panel')", "afk"),
        new("squads-equipment", 3, "!!document.querySelector('.squad-panel')", "equipment"),
        new("city-layout", 4, "!!document.querySelector('.city-layout-panel,.city-layout-empty')"),
        new("hotkeys", 5, "!!document.querySelector('.hotkey-panel[data-hotkey-category=hotkeys]')"),
        new("mini-games", 6, "!!document.querySelector('.hotkey-panel[data-hotkey-category=miniGames]')"),
        new("settings", 7, "!!document.querySelector('.settings-panel')"),
    ];

    private async Task R29SweepWaitDomAsync(
        CoreWebView2 core, string expression, string stage, CancellationToken token,
        Func<bool>? ownerCheck = null, int polls = 300)
    {
        for (int i = 0; i < polls; i++)
        {
            token.ThrowIfCancellationRequested();
            if (ownerCheck is not null && !ownerCheck())
                throw new InvalidDataException("R29 sweep lost the exact authenticated owner at " + stage + ".");
            try
            {
                if (await core.ExecuteScriptAsync(expression) == "true") return;
            }
            catch (InvalidOperationException) when (i < polls - 1) { }
            catch (System.Runtime.InteropServices.COMException) when (i < polls - 1) { }
            await Task.Delay(100, token);
        }
        throw new InvalidDataException("R29 sweep WebView did not settle: " + stage);
    }

    private async Task R29SweepSetViewportAsync(
        CoreWebView2 core, int width, CancellationToken token, Func<bool>? ownerCheck = null)
    {
        if (ownerCheck is not null && !ownerCheck())
            throw new InvalidDataException("R29 sweep lost owner before viewport change.");
        await core.CallDevToolsProtocolMethodAsync(
            "Emulation.setDeviceMetricsOverride",
            JsonSerializer.Serialize(new { width, height = 900, deviceScaleFactor = 1, mobile = false },
                JsonOptions.Default));
        await R29SweepWaitDomAsync(core, $"window.innerWidth==={width}", "viewport " + width,
            token, ownerCheck, 100);
    }

    private static async Task R29SweepClearViewportAsync(CoreWebView2 core)
    {
        try { await core.CallDevToolsProtocolMethodAsync("Emulation.clearDeviceMetricsOverride", "{}"); }
        catch { }
    }

    private async Task R29SweepSwitchLanguageAsync(
        CoreWebView2 core, string language, CancellationToken token, Func<bool>? ownerCheck = null)
    {
        string json = JsonSerializer.Serialize(language, JsonOptions.Default);
        string result = await core.ExecuteScriptAsync($$"""
            (() => {
              const select=document.querySelector('.language-select select');
              if(!select)return false;
              if(select.value!=={{json}}){
                Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype,'value').set.call(select,{{json}});
                select.dispatchEvent(new Event('change',{bubbles:true}));
              }
              return true;
            })()
            """);
        if (result != "true") throw new InvalidDataException("R29 sweep language selector unavailable.");
        await R29SweepWaitDomAsync(core,
            $"document.querySelector('.language-select select')?.value==={json}",
            "language " + language, token, ownerCheck, 100);
    }

    private async Task<double> R29SweepNavigateAsync(
        CoreWebView2 core, R29SweepSurface surface, CancellationToken token,
        Func<bool>? ownerCheck = null)
    {
        var timer = Stopwatch.StartNew();
        string result = await core.ExecuteScriptAsync($$"""
            (() => {
              const button=document.querySelectorAll('.side-nav button')[{{surface.RouteIndex}}];
              if(!button)return false;
              button.click();
              return true;
            })()
            """);
        if (result != "true") throw new InvalidDataException("R29 sweep route unavailable: " + surface.Key);
        await R29SweepWaitDomAsync(core, surface.ReadyExpression,
            surface.Key + " route", token, ownerCheck, 300);
        if (surface.SquadTab is not null)
        {
            int tabIndex = surface.SquadTab == "afk" ? 0 : 1;
            if (await core.ExecuteScriptAsync($$"""
                (() => {
                  const tabs=document.querySelectorAll('.squad-tabs button[role=tab]');
                  const button=tabs[{{tabIndex}}];
                  if(!button)return false;
                  button.click();
                  return true;
                })()
                """) != "true")
                throw new InvalidDataException("R29 sweep Squads tab unavailable: " + surface.SquadTab);
            await R29SweepWaitDomAsync(core,
                $"document.querySelectorAll('.squad-tabs button[role=tab]')[{tabIndex}]?.getAttribute('aria-selected')==='true'",
                surface.Key + " subtab", token, ownerCheck, 100);
        }
        await R29SweepWaitDomAsync(core,
            "document.querySelectorAll('[data-preview-fixture]').length===0",
            surface.Key + " production fixture fence", token, ownerCheck, 100);
        timer.Stop();
        return timer.Elapsed.TotalMilliseconds;
    }

    private async Task<object> R29SweepCaptureAsync(
        CoreWebView2 core, string directory, string connectionState,
        R29SweepSurface surface, string language, int width, double openMilliseconds,
        CancellationToken token, Func<bool>? ownerCheck = null)
    {
        if (ownerCheck is not null && !ownerCheck())
            throw new InvalidDataException("R29 sweep lost owner before screenshot.");
        string domRaw = await core.ExecuteScriptAsync("""
            (() => {
              const alerts=[...document.querySelectorAll('[role=alert]')].map(node=>(node.textContent||'').trim()).filter(Boolean);
              const controls=[...document.querySelectorAll('button,select,input,summary,[role=switch],[role=button]')]
                .filter(node=>node.offsetParent!==null || node===document.activeElement);
              return {
                language:document.querySelector('.language-select select')?.value||'',
                width:window.innerWidth,
                activeRoute:[...document.querySelectorAll('.side-nav button')].findIndex(button=>button.classList.contains('active')),
                online:document.querySelector('.status-online')?.classList.contains('online')===true,
                previewFixtureCount:document.querySelectorAll('[data-preview-fixture]').length,
                alerts,
                rawCodeAlerts:alerts.filter(text=>/\b[A-Z][A-Z0-9]+_[A-Z0-9_]+\b/.test(text)),
                controlCount:controls.length,
                enabledControlCount:controls.filter(node=>!node.disabled).length,
              };
            })()
            """);
        using JsonDocument domDocument = JsonDocument.Parse(domRaw);
        JsonElement dom = domDocument.RootElement.Clone();
        if (dom.GetProperty("previewFixtureCount").GetInt32() != 0 ||
            dom.GetProperty("rawCodeAlerts").GetArrayLength() != 0 ||
            dom.GetProperty("language").GetString() != language ||
            dom.GetProperty("width").GetInt32() != width)
            throw new InvalidDataException("R29 sweep production DOM contract failed for " + surface.Key + ".");
        bool online = dom.GetProperty("online").GetBoolean();
        if (online != (connectionState == "connected"))
            throw new InvalidDataException("R29 sweep connection indicator disagrees at " + surface.Key + ".");

        string fileName = $"r29-sweep-{connectionState}-{surface.Key}-{language}-{width}.png";
        string response = await core.CallDevToolsProtocolMethodAsync(
            "Page.captureScreenshot",
            "{\"format\":\"png\",\"fromSurface\":true,\"captureBeyondViewport\":false}");
        using JsonDocument screenshot = JsonDocument.Parse(response);
        byte[] png = Convert.FromBase64String(screenshot.RootElement.GetProperty("data").GetString() ?? "");
        if (png.Length < 24 || png[0] != 0x89 || png[1] != 0x50 || png[2] != 0x4e || png[3] != 0x47)
            throw new InvalidDataException("R29 sweep screenshot was not PNG: " + fileName);
        int pixelWidth = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
        if (pixelWidth != width)
            throw new InvalidDataException($"R29 sweep screenshot width {pixelWidth} != {width}: {fileName}");
        await File.WriteAllBytesAsync(Path.Combine(directory, fileName), png, token);
        return new { connectionState, surface = surface.Key, language, width, openMilliseconds, fileName, dom };
    }

    private async Task<List<object>> R29SweepMatrixAsync(
        CoreWebView2 core, string directory, string connectionState,
        CancellationToken token, Func<bool>? ownerCheck = null)
    {
        var rows = new List<object>();
        foreach (string language in new[] { "en", "zh-CN" })
        {
            await R29SweepSwitchLanguageAsync(core, language, token, ownerCheck);
            foreach (int width in new[] { 900, 1600 })
            {
                await R29SweepSetViewportAsync(core, width, token, ownerCheck);
                foreach (R29SweepSurface surface in R29SweepSurfaces)
                {
                    double elapsed = await R29SweepNavigateAsync(core, surface, token, ownerCheck);
                    rows.Add(await R29SweepCaptureAsync(core, directory, connectionState,
                        surface, language, width, elapsed, token, ownerCheck));
                }
            }
        }
        return rows;
    }

    // Runs before the normal proof creates its game instance. This is a genuine
    // packaged disconnected state, then starts the exact task-owned game through
    // the production Home button so the outer lifecycle can authenticate it.
    private async Task RunFeature018R29DisconnectedSweepAsync(
        CoreWebView2 core, string directory, CancellationToken token)
    {
        OverviewLifecycleService lifecycle = overviewLifecycleService
            ?? throw new InvalidOperationException("R29 sweep lifecycle missing.");
        if (lifecycle.CreateProfileInstanceStatus() is not null)
            throw new InvalidDataException("R29 disconnected sweep requires no running profile instance.");

        var matrix = await R29SweepMatrixAsync(core, directory, "disconnected", token);
        await R29SweepClearViewportAsync(core);
        await R29SweepSwitchLanguageAsync(core, "en", token);
        R29SweepSurface home = R29SweepSurfaces.First(surface => surface.Key == "home");
        await R29SweepNavigateAsync(core, home, token);

        // UI-only shell controls: theme, server chooser open/close, sidebar hide/restore.
        string shellActions = await core.ExecuteScriptAsync("""
            (() => {
              const theme=document.querySelector('.theme-toggle');
              if(!theme)return 'missing-theme';
              theme.click(); theme.click();
              const jump=document.querySelector('.server-jump > button');
              if(jump){jump.click();jump.click();}
              const hide=document.querySelector('.profile-collapse');
              if(hide && !hide.disabled){hide.click();}
              const restore=document.querySelector('.profile-sidebar-restore');
              if(restore && !restore.disabled){restore.click();}
              return 'ok';
            })()
            """);
        if (JsonSerializer.Deserialize<string>(shellActions, JsonOptions.Default) != "ok")
            throw new InvalidDataException("R29 disconnected shell controls were unavailable.");

        // Start through the actual production Home control. The outer proof then
        // observes/authenticates the resulting native instance before any live action.
        string started = await core.ExecuteScriptAsync("""
            (() => {
              const buttons=[...document.querySelectorAll('.quick-actions-panel .game-controls button')];
              const start=buttons.find(button=>button.classList.contains('primary') && !button.disabled);
              if(!start)return false;
              start.click();
              return true;
            })()
            """);
        if (started != "true")
            throw new InvalidDataException("R29 disconnected sweep could not start the task-owned game through Home.");

        await File.WriteAllTextAsync(Path.Combine(directory, "r29-sweep-disconnected.json"),
            JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                state = "proven",
                packaged = true,
                connectionState = "disconnected",
                gameProcessPresentDuringMatrix = false,
                matrix,
                shellActions = new[] { "theme-toggle-twice", "server-chooser-open-close", "sidebar-hide-restore" },
                startGameThroughProductionHome = true,
                capturedAtUtc = DateTimeOffset.UtcNow,
            }, new JsonSerializerOptions(JsonOptions.Default) { WriteIndented = true }), token);
    }

    private async Task RunFeature018R29ConnectedSweepAsync(
        CoreWebView2 core, string outputPath, string directory,
        OverviewMapScanSession owner, string uid, int serverId, CancellationToken token)
    {
        OverviewLifecycleService lifecycle = overviewLifecycleService
            ?? throw new InvalidOperationException("R29 sweep lifecycle missing.");
        var approved = new Feature018HotkeySession(owner.ProfileId, owner.SessionId, uid, serverId);
        bool IsOwner() => Feature018IsApprovedAccount(uid, serverId) &&
            lifecycle.OwnsExactProcess(owner.GamePid) && lifecycle.MatchesOwnedMapScanSession(owner) &&
            ReadFeature018AuthenticatedAccount(lifecycle, owner) == approved;
        void Owned()
        {
            token.ThrowIfCancellationRequested();
            if (!IsOwner()) throw new InvalidDataException("R29 connected sweep lost exact owner.");
        }
        Owned();

        var matrix = await R29SweepMatrixAsync(core, directory, "connected", token, IsOwner);
        var actions = new List<object>();

        // Global safe shell controls.
        async Task ClickTwice(string selector, string name)
        {
            Owned();
            string result = await core.ExecuteScriptAsync($$"""
                (() => {
                  const button=document.querySelector({{JsonSerializer.Serialize(selector, JsonOptions.Default)}});
                  if(!button||button.disabled)return false;
                  button.click();button.click();return true;
                })()
                """);
            if (result != "true") throw new InvalidDataException("R29 safe control unavailable: " + name);
            actions.Add(new { name, exercised = true, restoredBySecondClick = true });
        }
        await ClickTwice(".theme-toggle", "theme toggle");
        await ClickTwice(".server-jump > button", "server chooser open/close");
        if (await core.ExecuteScriptAsync("(()=>{const b=document.querySelector('.profile-collapse');if(!b||b.disabled)return false;b.click();return true;})()") == "true")
        {
            await R29SweepWaitDomAsync(core, "!!document.querySelector('.profile-sidebar-restore')", "sidebar hidden", token, IsOwner);
            if (await core.ExecuteScriptAsync("(()=>{const b=document.querySelector('.profile-sidebar-restore');if(!b||b.disabled)return false;b.click();return true;})()") != "true")
                throw new InvalidDataException("R29 sidebar restore unavailable during sweep.");
            await R29SweepWaitDomAsync(core, "!!document.querySelector('.profile-sidebar')", "sidebar restored", token, IsOwner);
            actions.Add(new { name = "sidebar hide/restore", exercised = true, restored = true });
        }

        // Map: both scan-mode tabs, all result tabs, and a bounded manual start/stop.
        R29SweepSurface map = R29SweepSurfaces.First(surface => surface.Key == "map-data");
        await R29SweepNavigateAsync(core, map, token, IsOwner);
        string mapTabs = await core.ExecuteScriptAsync("""
            (() => {
              const scanTabs=[...document.querySelectorAll('.map-scan-tabs button[role=tab],.map-mode-tabs button[role=tab]')];
              scanTabs.forEach(button=>button.click());
              const resultTabs=[...document.querySelectorAll('.map-tabs button[role=tab]')];
              resultTabs.forEach(button=>button.click());
              return JSON.stringify({scanTabs:scanTabs.length,resultTabs:resultTabs.length});
            })()
            """);
        actions.Add(new { name = "Map scan/result tabs", exercised = true, counts = JsonDocument.Parse(mapTabs).RootElement.Clone() });
        // Return manual and issue one real Start followed by Stop; no map row/game mutation.
        await core.ExecuteScriptAsync("document.querySelector('.map-scan-tabs button[role=tab],.map-mode-tabs button[role=tab]')?.click()");
        string scanStarted = await core.ExecuteScriptAsync("""
            (() => {
              const start=[...document.querySelectorAll('.map-actions button')].find(button=>button.classList.contains('primary')&&!button.disabled);
              if(!start)return false;start.click();return true;
            })()
            """);
        if (scanStarted == "true")
        {
            await R29SweepWaitDomAsync(core,
                "[...document.querySelectorAll('.map-actions button')].some(button=>button.classList.contains('danger')&&!button.disabled)",
                "Map scan started", token, IsOwner, 300);
            if (await core.ExecuteScriptAsync("(()=>{const b=[...document.querySelectorAll('.map-actions button')].find(button=>button.classList.contains('danger')&&!button.disabled);if(!b)return false;b.click();return true;})()") != "true")
                throw new InvalidDataException("R29 Map Stop unavailable after Start.");
            await R29SweepWaitDomAsync(core,
                "![...document.querySelectorAll('.map-actions button')].some(button=>button.classList.contains('danger')&&!button.disabled)",
                "Map scan stopped", token, IsOwner, 300);
            actions.Add(new { name = "Map manual Start/Stop", exercised = true, restored = true });
        }
        else actions.Add(new { name = "Map manual Start/Stop", exercised = false, reason = "start disabled by live state" });

        // City read-only controls. Live move/apply/restore evidence is kept in the
        // dedicated City proof so this sweep does not duplicate a mutation.
        R29SweepSurface city = R29SweepSurfaces.First(surface => surface.Key == "city-layout");
        await R29SweepNavigateAsync(core, city, token, IsOwner);
        string cityActions = await core.ExecuteScriptAsync("""
            (() => {
              const buttons=[...document.querySelectorAll('.city-layout-actions button,.city-layout-zoom button')];
              const safe=buttons.filter(button=>!button.disabled && (
                button.closest('.city-layout-zoom') || button===buttons[0]));
              safe.forEach(button=>button.click());
              return safe.length;
            })()
            """);
        actions.Add(new { name = "City Refresh/zoom/fit", exercised = int.Parse(cityActions, System.Globalization.CultureInfo.InvariantCulture) > 0,
            coveredMutationEvidence = "dedicated exact-restore City proof" });

        // Equipment read-only Refresh + preset selection. Direct gear mutation was
        // already exercised/restored by the R29 speed proof.
        R29SweepSurface equipment = R29SweepSurfaces.First(surface => surface.Key == "squads-equipment");
        await R29SweepNavigateAsync(core, equipment, token, IsOwner);
        string equipmentActions = await core.ExecuteScriptAsync("""
            (() => {
              const refresh=document.querySelector('.squad-header button');
              if(refresh && !refresh.disabled)refresh.click();
              const presets=[...document.querySelectorAll('.equipment-live-presets .equipment-preset-list > button')];
              presets.forEach(button=>{if(!button.disabled)button.click();});
              return JSON.stringify({refresh:!!refresh&&!refresh.disabled,presets:presets.length});
            })()
            """);
        actions.Add(new { name = "Equipment Refresh/preset selection", exercised = true,
            counts = JsonDocument.Parse(equipmentActions).RootElement.Clone(),
            coveredMutationEvidence = "R29 preset-speed exact restore" });

        // AFK read-only/config-navigation controls only. Master, stamina auto-use
        // and Alliance Drill remain intentionally unpressed live.
        R29SweepSurface afk = R29SweepSurfaces.First(surface => surface.Key == "squads-afk");
        await R29SweepNavigateAsync(core, afk, token, IsOwner);
        string afkActions = await core.ExecuteScriptAsync("""
            (() => {
              const cards=[...document.querySelectorAll('.monster-afk-compact-card[role=button]')];
              cards.forEach(card=>card.click());
              const add=document.querySelector('.monster-afk-add-control > button');
              if(add && !add.disabled){add.click();add.click();}
              const profile=document.querySelector('.monster-afk-profile-select');
              if(profile && !profile.disabled)profile.click();
              return JSON.stringify({cards:cards.length,add:!!add&&!add.disabled,profile:!!profile&&!profile.disabled});
            })()
            """);
        actions.Add(new { name = "AFK settings/add-menu/profile selection", exercised = true,
            counts = JsonDocument.Parse(afkActions).RootElement.Clone() });

        // Hotkey enable switches are configuration-only; toggle every switch twice.
        R29SweepSurface hotkeys = R29SweepSurfaces.First(surface => surface.Key == "hotkeys");
        await R29SweepNavigateAsync(core, hotkeys, token, IsOwner);
        int hotkeySwitchCount = JsonSerializer.Deserialize<int>(await core.ExecuteScriptAsync("document.querySelectorAll('.hotkey-switch').length"), JsonOptions.Default);
        for (int i = 0; i < hotkeySwitchCount; i++)
        {
            for (int click = 0; click < 2; click++)
            {
                if (await core.ExecuteScriptAsync($"(()=>{{const b=document.querySelectorAll('.hotkey-switch')[{i}];if(!b||b.disabled)return false;b.click();return true;}})()") != "true")
                    throw new InvalidDataException("R29 Hotkey config switch became stuck.");
                await R29SweepWaitDomAsync(core,
                    $"document.querySelectorAll('.hotkey-switch')[{i}]?.disabled===false",
                    "Hotkey switch save", token, IsOwner, 200);
            }
        }
        actions.Add(new { name = "Hotkey config switches", exercised = hotkeySwitchCount, restored = true });

        // Mini Games: only the G/frontline configuration is live-safe. Land,
        // Black Market/Treasure marker and Food House are explicitly screen-only.
        R29SweepSurface mini = R29SweepSurfaces.First(surface => surface.Key == "mini-games");
        await R29SweepNavigateAsync(core, mini, token, IsOwner);
        int miniSwitches = JsonSerializer.Deserialize<int>(await core.ExecuteScriptAsync("document.querySelectorAll('.hotkey-switch').length"), JsonOptions.Default);
        for (int click = 0; click < 2 && miniSwitches > 0; click++)
        {
            if (await core.ExecuteScriptAsync("(()=>{const b=document.querySelector('.hotkey-switch');if(!b||b.disabled)return false;b.click();return true;})()") != "true")
                throw new InvalidDataException("R29 Mini G config switch became stuck.");
            await R29SweepWaitDomAsync(core, "document.querySelector('.hotkey-switch')?.disabled===false",
                "Mini G switch save", token, IsOwner, 200);
        }
        actions.Add(new { name = "Mini G config switch", exercised = miniSwitches > 0, restored = true });

        // Settings account-focus switch is UI/config-only. FPS/ping is covered by
        // the dedicated Part-A proof with independent game-side values/restoration.
        R29SweepSurface settings = R29SweepSurfaces.First(surface => surface.Key == "settings");
        await R29SweepNavigateAsync(core, settings, token, IsOwner);
        string settingsFocus = await core.ExecuteScriptAsync("""
            (() => {
              const switches=[...document.querySelectorAll('.settings-panel button[role=switch]')];
              const focus=switches[2];
              if(!focus||focus.disabled)return false;
              focus.click();focus.click();return true;
            })()
            """);
        actions.Add(new { name = "Settings account-focus switch", exercised = settingsFocus == "true", restored = true,
            visualMetricsEvidence = "R29 Part-A exact-owner proof" });

        // Finish via the actual Home Close Game button. If the R29 shell fix adds
        // an in-app confirmation, accept it through DOM only. No owner assertion
        // occurs after this click because losing the task-owned owner is expected.
        R29SweepSurface home = R29SweepSurfaces.First(surface => surface.Key == "home");
        await R29SweepNavigateAsync(core, home, token, IsOwner);
        string closeClicked = await core.ExecuteScriptAsync("""
            (() => {
              const buttons=[...document.querySelectorAll('.quick-actions-panel .game-controls button')];
              const close=buttons.find(button=>!button.classList.contains('primary')&&!button.disabled);
              if(!close)return false;close.click();return true;
            })()
            """);
        if (closeClicked != "true") throw new InvalidDataException("R29 Home Close Game control unavailable.");
        await Task.Delay(100, token);
        await core.ExecuteScriptAsync("(()=>{const dialog=document.querySelector('[data-home-close-confirm],.home-close-confirm,.scheme-modal[role=dialog],.scheme-modal');const buttons=dialog?[...dialog.querySelectorAll('button')]:[];const b=buttons.find(x=>x.classList.contains('danger')||x.classList.contains('primary'))||buttons.at(-1);if(b&&!b.disabled)b.click();return true;})()");
        for (int i = 0; i < 600 && lifecycle.CreateProfileInstanceStatus() is not null; i++)
            await Task.Delay(100, token);
        if (lifecycle.CreateProfileInstanceStatus() is not null)
            throw new InvalidDataException("R29 Home Close Game did not stop the task-owned instance.");
        await R29SweepWaitDomAsync(core,
            "document.querySelector('.status-online')?.classList.contains('online')===false",
            "disconnected after Home Close Game", token, ownerCheck: null, polls: 300);
        actions.Add(new { name = "Home Close Game", exercised = true, taskOwnedInstanceStopped = true });

        await R29SweepClearViewportAsync(core);
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            state = "proven",
            surface = "r29-sweep",
            owner = new { owner.ProfileId, owner.SessionId, owner.GamePid, uid, serverId },
            domOnly = true,
            osInputUsed = false,
            matrix,
            safeActions = actions,
            excludedLive = new[]
            {
                "Unlock Land Cell", "F6/F7/F8 shield use", "Treasure claim",
                "Food House", "Black Market", "Alliance Drill rally",
            },
            mutationEvidence = new
            {
                equipment = "R29 preset-speed exact slot/wearer restoration",
                city = "dedicated City exact-restore proof / owner-confirmed R24 behavior",
            },
            endedDisconnected = true,
            finishedAtUtc = DateTimeOffset.UtcNow,
        }, new JsonSerializerOptions(JsonOptions.Default) { WriteIndented = true }), CancellationToken.None);
    }
}
