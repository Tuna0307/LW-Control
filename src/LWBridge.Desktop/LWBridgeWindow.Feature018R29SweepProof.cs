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
        token.ThrowIfCancellationRequested();
        if (ownerCheck is not null && !ownerCheck())
            throw new InvalidDataException("R29 sweep lost owner before language change.");
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
        token.ThrowIfCancellationRequested();
        if (ownerCheck is not null && !ownerCheck())
            throw new InvalidDataException("R29 sweep lost owner before navigation.");
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
        string rootSelector = surface.Key switch
        {
            "home" => ".quick-actions-panel",
            "map-data" => ".map-panel",
            "squads-afk" or "squads-equipment" => ".squad-panel",
            "city-layout" => ".city-layout-panel,.city-layout-empty",
            "hotkeys" => ".hotkey-panel[data-hotkey-category=hotkeys]",
            "mini-games" => ".hotkey-panel[data-hotkey-category=miniGames]",
            "settings" => ".settings-panel",
            _ => throw new InvalidDataException("Unknown R29 sweep route."),
        };
        // RetainedPages keeps visited routes in hidden React Activity trees.
        // A matching hidden node is not evidence that navigation committed.
        await R29SweepWaitDomAsync(core,
            $"document.querySelectorAll('.side-nav button')[{surface.RouteIndex}]?.classList.contains('active')===true&&({surface.ReadyExpression})&&[...document.querySelectorAll({JsonSerializer.Serialize(rootSelector)})].some(b=>b.getClientRects().length>0)",
            surface.Key + " route", token, ownerCheck, 300);
        if (surface.SquadTab is not null)
        {
            if (ownerCheck is not null && !ownerCheck())
                throw new InvalidDataException("R29 sweep lost owner before Squads subtab change.");
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
        if (ownerCheck is not null)
        {
            string? dataReady = surface.Key switch
            {
                "squads-afk" => "document.querySelector('.monster-afk-layout[data-native-config-ready=true]')?.getClientRects().length>0&&!!document.querySelector('.monster-afk-add-control>button:not(:disabled)')",
                "squads-equipment" => "document.querySelector('[data-native-equipment]')?.getClientRects().length>0&&document.querySelectorAll('[data-equipment-position]').length>0&&document.querySelectorAll('[data-equipment-preset]').length===4",
                "city-layout" => "document.querySelector('.city-layout-panel')?.getClientRects().length>0&&document.querySelectorAll('[data-city-building-id]').length>0",
                "hotkeys" => "document.querySelector('.hotkey-panel[data-hotkey-category=hotkeys] .hotkey-switch:not(:disabled)')?.getClientRects().length>0",
                "mini-games" => "document.querySelector('.hotkey-panel[data-hotkey-category=miniGames] .hotkey-switch:not(:disabled)')?.getClientRects().length>0",
                "settings" => "document.querySelectorAll('.settings-panel button[role=switch]').length>=2",
                _ => null,
            };
            if (dataReady is not null)
                await R29SweepWaitDomAsync(core, dataReady, surface.Key + " first native data", token, ownerCheck, 300);
        }
        if (ownerCheck is not null && !ownerCheck())
            throw new InvalidDataException("R29 sweep lost owner before rendered-state settling.");
        await core.ExecuteScriptAsync("window.__r29RouteCommitted=false;requestAnimationFrame(()=>requestAnimationFrame(()=>{window.__r29RouteCommitted=true;}));true");
        await R29SweepWaitDomAsync(core, "window.__r29RouteCommitted===true", surface.Key + " rendered state", token, ownerCheck, 100);
        if (surface.Key == "city-layout" && ownerCheck is not null)
            await R29SweepWaitDomAsync(core, """
                (()=>{const v=document.querySelector('.city-layout-grid-viewport');if(!v)return false;
                  const vr=v.getBoundingClientRect();
                  const shown=[...v.querySelectorAll('.city-layout-building')].filter(b=>{const r=b.getBoundingClientRect();return r.right>vr.left&&r.left<vr.right&&r.bottom>vr.top&&r.top<vr.bottom;});
                  return shown.length>0&&shown.every(b=>{const i=b.querySelector('img.city-layout-grid-icon');return !!i&&i.complete&&i.naturalWidth>0;});})()
                """, "City visible building icons decoded", token, ownerCheck, 300);
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
              return 'ok';
            })()
            """);
        if (JsonSerializer.Deserialize<string>(shellActions, JsonOptions.Default) != "ok")
            throw new InvalidDataException("R29 disconnected shell controls were unavailable.");
        foreach (string selector in new[] { ".theme-toggle[aria-pressed]", ".server-jump > button" })
            for (int click = 0; click < 2; click++)
            {
                await core.ExecuteScriptAsync($"document.querySelector({JsonSerializer.Serialize(selector)})?.click()");
                await Task.Delay(100, token);
            }
        await core.ExecuteScriptAsync("document.querySelector('.profile-collapse')?.click()");
        await R29SweepWaitDomAsync(core, "!!document.querySelector('.profile-sidebar-restore')", "disconnected sidebar hidden", token);
        await core.ExecuteScriptAsync("document.querySelector('.profile-sidebar-restore')?.click()");
        await R29SweepWaitDomAsync(core, "!!document.querySelector('.profile-sidebar')", "disconnected sidebar restored", token);

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

        // Each action reacquires its element after React commits. Counts of
        // discovered controls are not evidence that those controls were pressed.
        static string Element(string selector, int index = 0) =>
            $"document.querySelectorAll({JsonSerializer.Serialize(selector)})[{index}]";
        async Task<string> Read(string expression)
        {
            Owned();
            return await core.ExecuteScriptAsync(expression);
        }
        async Task Wait(string expression, string name, int polls = 300) =>
            await R29SweepWaitDomAsync(core, expression, name, token, IsOwner, polls);
        async Task<bool> Click(string element, string name, string ready, bool optional = false)
        {
            Owned();
            string result = await core.ExecuteScriptAsync($$"""
                (()=>{const b={{element}};if(!b||b.disabled||b.getClientRects().length===0)return false;b.click();return true;})()
                """);
            if (result != "true")
            {
                if (!optional) throw new InvalidDataException("R29 safe control unavailable: " + name);
                actions.Add(new { name, exercised = false, reason = "absent, hidden, or disabled in current native state" });
                return false;
            }
            // ExecuteScript completion is not a React commit. In particular,
            // a second click in the same render can reuse the old toggle value.
            Owned();
            await core.ExecuteScriptAsync("window.__r29SweepCommitted=false;requestAnimationFrame(()=>requestAnimationFrame(()=>{window.__r29SweepCommitted=true;}));true");
            await Wait($"window.__r29SweepCommitted===true&&({ready})", name);
            return true;
        }
        async Task SetInput(string element, string value, string name)
        {
            Owned();
            if (await core.ExecuteScriptAsync($$"""
                (()=>{const b={{element}};if(!b||b.disabled)return false;
                  Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(b,{{JsonSerializer.Serialize(value)}});
                  b.dispatchEvent(new Event('input',{bubbles:true}));b.dispatchEvent(new Event('change',{bubbles:true}));return true;})()
                """) != "true") throw new InvalidDataException("R29 input unavailable: " + name);
            Owned();
            await core.ExecuteScriptAsync("window.__r29SweepCommitted=false;requestAnimationFrame(()=>requestAnimationFrame(()=>{window.__r29SweepCommitted=true;}));true");
            await Wait($"window.__r29SweepCommitted===true&&({element})?.value==={JsonSerializer.Serialize(value)}", name + " rendered value");
        }
        async Task SetSelect(string element, string jsonValue, string name)
        {
            Owned();
            if (await core.ExecuteScriptAsync($$"""
                (()=>{const b={{element}};if(!b||b.disabled)return false;
                  if(![...b.options].some(o=>!o.disabled&&o.value==={{jsonValue}}))return false;
                  Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype,'value').set.call(b,{{jsonValue}});
                  b.dispatchEvent(new Event('change',{bubbles:true}));return true;})()
                """) != "true") throw new InvalidDataException("R29 dropdown unavailable: " + name);
            Owned();
            await core.ExecuteScriptAsync("window.__r29SweepCommitted=false;requestAnimationFrame(()=>requestAnimationFrame(()=>{window.__r29SweepCommitted=true;}));true");
            await Wait($"window.__r29SweepCommitted===true&&({element})?.value==={jsonValue}", name + " rendered selection");
        }
        // Restores the observed value even if the changed-state assertion fails.
        // Rendered restoration is recorded separately from disk/native proof.
        async Task ToggleAndRestore(string element, string name, string state)
        {
            string State() => $"(()=>{{const b={element};return b?({state}):null;}})()";
            string before = await Read(State());
            if ((before != "true" && before != "false") || await Read($"!!({element})?.disabled") == "true")
            {
                actions.Add(new { name, exercised = false, reason = "configuration control unavailable" });
                return;
            }
            bool attempted = false;
            try
            {
                attempted = true;
                await Click(element, name + " change", $"!({element})?.disabled&&({State()})!=={before}");
                // Home preferences can acknowledge asynchronously without a disabled state.
                await Task.Delay(250, token);
                await Wait($"!({element})?.disabled&&({State()})!=={before}", name + " changed state retained");
            }
            finally
            {
                if (attempted)
                {
                    await Wait($"!!({element})&&!({element}).disabled", name + " restoration ready");
                    if (await Read(State()) != before)
                        await Click(element, name + " restore", $"!({element})?.disabled&&({State()})==={before}");
                    await Task.Delay(250, token);
                    await Wait($"({State()})==={before}", name + " restored state retained");
                }
            }
            actions.Add(new { name, exercised = true, renderedStateRestored = true, persistenceIndependentlyVerified = false });
        }

        await ToggleAndRestore(Element(".theme-toggle[aria-pressed]"), "theme toggle", "b.getAttribute('aria-pressed')==='true'");
        await Click(Element(".server-jump > button"), "server chooser open", "!!document.querySelector('.server-jump-popover')");
        // Zero is rejected in App.jumpServer before any native request. Never
        // submit a valid target or a history/return button in this sweep.
        string targetBefore = await Read("document.querySelector('.server-jump-form input').value");
        await SetInput(Element(".server-jump-form input"), "0", "server chooser invalid id");
        if (await Click(Element(".server-jump-form > button"), "server chooser invalid-id validation",
            "!!document.querySelector('.server-jump-error')?.textContent.trim()", optional: true))
        {
            string error = await Read("document.querySelector('.server-jump-error').textContent.trim()");
            if (System.Text.RegularExpressions.Regex.IsMatch(JsonSerializer.Deserialize<string>(error) ?? "", @"\b[A-Z][A-Z0-9]+_[A-Z0-9_]+\b"))
                throw new InvalidDataException("R29 chooser exposed an untranslated error code.");
            actions.Add(new { name = "server chooser invalid-id validation", exercised = true, nativeJumpRequested = false, error });
        }
        await SetInput(Element(".server-jump-form input"), JsonSerializer.Deserialize<string>(targetBefore) ?? "", "server chooser input restore");
        await Click(Element(".server-jump > button"), "server chooser close", "!document.querySelector('.server-jump-popover')");
        await Click(Element(".server-jump > button"), "server chooser reopen/error recovery", "!!document.querySelector('.server-jump-popover')&&!document.querySelector('.server-jump-error')");
        await Click(Element(".server-jump > button"), "server chooser final close", "!document.querySelector('.server-jump-popover')");
        actions.Add(new { name = "server chooser open/close and error recovery", exercised = true, inputRestored = true });

        if (await Click(Element(".profile-collapse"), "sidebar hide", "!!document.querySelector('.profile-sidebar-restore')", optional: true))
        {
            await Click(Element(".profile-sidebar-restore"), "sidebar restore", "!!document.querySelector('.profile-sidebar')&&!document.querySelector('.profile-sidebar-restore')");
            actions.Add(new { name = "sidebar hide/restore", exercised = true, renderedStateRestored = true });
        }
        const string noteEdit = ".profile-row.active .profile-note-edit:not(.profile-enable-toggle)";
        if (await Click(Element(noteEdit), "sidebar note editor open",
            "!!document.querySelector('dialog[open] #profile-note-title')", optional: true))
        {
            string noteBefore = await Read("document.querySelector('.profile-dialog input').value");
            await SetInput(Element(".profile-dialog input"), "R29 unsaved note", "sidebar note unsaved edit");
            await Click(Element(".profile-dialog-actions button[type=button]"), "sidebar note cancel", "!document.querySelector('dialog[open] #profile-note-title')");
            await Click(Element(noteEdit), "sidebar note reopen", "!!document.querySelector('dialog[open] #profile-note-title')");
            await Wait($"document.querySelector('.profile-dialog input').value==={noteBefore}", "sidebar cancelled note unchanged");
            await Click(Element(".profile-dialog-actions button[type=button]"), "sidebar note final cancel", "!document.querySelector('dialog[open] #profile-note-title')");
            actions.Add(new { name = "sidebar note edit/cancel", exercised = true, persistedNoteUnchanged = true });
        }

        R29SweepSurface home = R29SweepSurfaces.First(surface => surface.Key == "home");
        await R29SweepNavigateAsync(core, home, token, IsOwner);
        for (int i = 0; i < 2; i++)
            await ToggleAndRestore(Element(".quick-actions-panel button[role=switch]", i),
                i == 0 ? "Home auto-launch preference" : "Home reconnect preference", "b.getAttribute('aria-checked')==='true'");

        // Both modes and every result tab commit separately. Only view/filter
        // controls are selected below; no row, claim, export, Clear or Auto Run.
        R29SweepSurface map = R29SweepSurfaces.First(surface => surface.Key == "map-data");
        await R29SweepNavigateAsync(core, map, token, IsOwner);
        string scanSelector = ".map-scan-tabs button[role=tab]";
        string resultSelector = ".map-tabs button[role=tab]";
        string scanBefore = await Read("[...document.querySelectorAll('.map-scan-tabs button')].findIndex(b=>b.getAttribute('aria-selected')==='true')");
        string resultBefore = await Read("[...document.querySelectorAll('.map-tabs button')].findIndex(b=>b.getAttribute('aria-selected')==='true')");
        for (int i = 0; i < 2; i++)
        {
            await Click(Element(scanSelector, i), "Map mode " + i,
                $"({Element(scanSelector, i)})?.getAttribute('aria-selected')==='true'&&{(i == 1 ? "!!" : "!")}document.querySelector('.map-auto-scan-card')");
            actions.Add(new { name = "Map scan mode", index = i, exercised = true });
        }
        await Click(Element(scanSelector), "Map manual mode", $"({Element(scanSelector)})?.getAttribute('aria-selected')==='true'&&!document.querySelector('.map-auto-scan-card')");
        // Scan-type details open/close only; selected types never change here.
        await Click(Element(".map-actions .map-category-picker > summary"), "Map scan-type menu open", "document.querySelector('.map-actions .map-category-picker')?.open===true");
        await Click(Element(".map-actions .map-category-picker > summary"), "Map scan-type menu close", "document.querySelector('.map-actions .map-category-picker')?.open===false");
        int resultCount = JsonSerializer.Deserialize<int>(await Read($"document.querySelectorAll({JsonSerializer.Serialize(resultSelector)}).length"));
        for (int tab = 0; tab < resultCount; tab++)
        {
            await Click(Element(resultSelector, tab), "Map result tab " + tab, $"({Element(resultSelector, tab)})?.getAttribute('aria-selected')==='true'");
            string keyword = Element(".map-searchbar > input");
            string keywordBefore = await Read($"({keyword}).value");
            int selectCount = JsonSerializer.Deserialize<int>(await Read("document.querySelectorAll('.map-searchbar select').length"));
            var originalFilters = new List<(string Element, string Value)>();
            for (int index = 0; index < selectCount; index++)
            {
                string select = Element(".map-searchbar select", index);
                string before = await Read($"({select}).value");
                originalFilters.Add((select, before));
                string alternate = await Read($"(()=>{{const b={select};return [...b.options].find(o=>!o.disabled&&o.value!==b.value)?.value??null;}})()");
                if (alternate == "null")
                {
                    actions.Add(new { name = "Map filter dropdown", tab, index, exercised = false, reason = "no alternate option" });
                    continue;
                }
                try { await SetSelect(select, alternate, "Map dropdown change"); }
                finally { await SetSelect(select, before, "Map dropdown restore"); }
                actions.Add(new { name = "Map filter dropdown", tab, index, exercised = true, renderedStateRestored = true });
            }
            int detailCount = JsonSerializer.Deserialize<int>(await Read("document.querySelectorAll('.map-searchbar details.map-item-filter').length"));
            for (int index = 0; index < detailCount; index++)
            {
                string detail = Element(".map-searchbar details.map-item-filter", index);
                string initialOpen = await Read($"({detail}).open");
                await Click($"({detail}).querySelector('summary')", "Map item-filter menu change", $"({detail})?.open!=={initialOpen}");
                await Click($"({detail}).querySelector('summary')", "Map item-filter menu restore", $"({detail})?.open==={initialOpen}");
                actions.Add(new { name = "Map item-filter menu", tab, index, exercised = true, openStateRestored = true });
            }
            int checkCount = JsonSerializer.Deserialize<int>(await Read("document.querySelectorAll('.map-searchbar input[type=checkbox]').length"));
            for (int index = 0; index < checkCount; index++)
                await ToggleAndRestore(Element(".map-searchbar input[type=checkbox]", index), $"Map view filter tab {tab} checkbox {index}", "b.checked");
            await SetInput(keyword, "R29-no-match", "Map keyword edit");
            // The first direct searchbar button is Search in current source;
            // Export and Treasure previews are separate later buttons.
            bool searched = await Click(Element(".map-searchbar > button"), "Map keyword Search tab " + tab,
                "!!document.querySelector('.map-result-count')&&document.querySelector('.map-table')?.getAttribute('aria-busy')==='false'", optional: true);
            await SetInput(keyword, JsonSerializer.Deserialize<string>(keywordBefore) ?? "", "Map keyword restore");
            // Typing clears the Resource/Monster name filter; put those view
            // filters back before searching the original view again.
            foreach (var filter in originalFilters)
                if (await Read($"({filter.Element})?.value") != filter.Value)
                    await SetSelect(filter.Element, filter.Value, "Map keyword side-effect filter restore");
            if (searched) await Click(Element(".map-searchbar > button"), "Map original Search tab " + tab,
                "!!document.querySelector('.map-result-count')&&document.querySelector('.map-table')?.getAttribute('aria-busy')==='false'");
            actions.Add(new { name = "Map result tab/search", tab, exercised = true, searchExercised = searched, keywordRestored = true });
        }
        // A passive manual scan is the only native work initiated in this tab.
        bool scanStarted = await Click(Element(".map-actions button.primary"), "Map manual Start",
            "!!document.querySelector('.map-actions button.danger:not(:disabled)')", optional: true);
        if (scanStarted)
        {
            await Click(Element(".map-actions button.danger"), "Map manual Stop", "!document.querySelector('.map-actions button.danger:not(:disabled)')");
            actions.Add(new { name = "Map manual Start/Stop", exercised = true, stopped = true, retainedDatasetRestored = false });
        }
        await Click(Element(resultSelector, int.Parse(resultBefore, System.Globalization.CultureInfo.InvariantCulture)), "Map result selection restore",
            $"({Element(resultSelector, int.Parse(resultBefore, System.Globalization.CultureInfo.InvariantCulture))})?.getAttribute('aria-selected')==='true'");
        await Click(Element(scanSelector, int.Parse(scanBefore, System.Globalization.CultureInfo.InvariantCulture)), "Map mode restore",
            $"({Element(scanSelector, int.Parse(scanBefore, System.Globalization.CultureInfo.InvariantCulture))})?.getAttribute('aria-selected')==='true'");

        R29SweepSurface city = R29SweepSurfaces.First(surface => surface.Key == "city-layout");
        await R29SweepNavigateAsync(core, city, token, IsOwner);
        if (await Click(Element(".city-layout-actions button"), "City Refresh",
            "document.querySelector('.city-layout-actions button')?.disabled===false&&document.querySelectorAll('[data-city-building-id]').length>0", optional: true))
            actions.Add(new { name = "City Refresh", exercised = true, nativeLayoutRendered = true });
        string gridZoom = "document.querySelector('.city-layout-grid').style.gridTemplateColumns.match(/([0-9]+)px/)?.[1]";
        string originalZoom = await Read(gridZoom);
        async Task Zoom(int index, string name)
        {
            string before = await Read(gridZoom);
            if (await Click(Element(".city-layout-zoom button", index), name, $"({gridZoom})!=={before}", optional: true))
                actions.Add(new { name, exercised = true, cellPixels = await Read(gridZoom) });
        }
        await Zoom(0, "City zoom out");
        await Zoom(1, "City zoom in");
        // Fit may already equal the current zoom, so assert its calculated bound.
        string fit = "(()=>{const e=document.querySelector('.city-layout-grid-viewport'),g=document.querySelector('.city-layout-grid');const c=Number(g.style.gridTemplateColumns.match(/repeat\\((\\d+)/)?.[1]),r=Number(g.style.gridTemplateRows.match(/repeat\\((\\d+)/)?.[1]);return String(Math.max(4,Math.min(72,Math.floor(Math.min((e.clientWidth-28)/c,(e.clientHeight-28)/r)))));})()";
        string fitted = await Read(fit);
        await Click(Element(".city-layout-zoom button", 2), "City fit canvas", $"({gridZoom})==={fitted}");
        // Restore exactly, including a non-multiple-of-four initial Fit zoom.
        int desiredZoom = int.Parse(JsonSerializer.Deserialize<string>(originalZoom) ?? "24", System.Globalization.CultureInfo.InvariantCulture);
        for (int step = 0; step < 20 && await Read(gridZoom) != originalZoom; step++)
        {
            int currentZoom = int.Parse(JsonSerializer.Deserialize<string>(await Read(gridZoom)) ?? "0", System.Globalization.CultureInfo.InvariantCulture);
            if (Math.Abs(currentZoom - desiredZoom) < 4) break;
            await Zoom(currentZoom < desiredZoom ? 1 : 0, "City zoom restore step");
        }
        actions.Add(new { name = "City fit/zoom restoration", exercised = true, originalCellPixels = desiredZoom,
            renderedZoomRestored = await Read(gridZoom) == originalZoom,
            reason = "Fit may change zoom modulo four; native placements are never edited" });

        R29SweepSurface equipment = R29SweepSurfaces.First(surface => surface.Key == "squads-equipment");
        await R29SweepNavigateAsync(core, equipment, token, IsOwner);
        string refreshBefore = await Read("document.querySelector('.squad-panel').dataset.equipmentRefreshCount");
        if (await Click(Element(".squad-header button"), "Equipment Refresh",
            $"document.querySelector('.squad-panel').dataset.equipmentRefreshCount!=={refreshBefore}&&document.querySelector('[data-equipment-sync]')?.dataset.equipmentSync==='synced'", optional: true))
            actions.Add(new { name = "Equipment Refresh", exercised = true, nativeSyncRendered = true });
        string presets = ".equipment-live-presets [data-equipment-preset]";
        string presetBefore = await Read("document.querySelector('.equipment-live-presets [data-equipment-preset].active')?.dataset.equipmentPreset??null");
        int presetCount = JsonSerializer.Deserialize<int>(await Read($"document.querySelectorAll({JsonSerializer.Serialize(presets)}).length"));
        for (int i = 0; i < presetCount; i++)
        {
            bool pressed = await Click(Element(presets, i), "Equipment preset selection " + i,
                $"({Element(presets, i)})?.classList.contains('active')===true", optional: true);
            if (pressed) actions.Add(new { name = "Equipment preset selection", index = i, exercised = true, activeSelectionRendered = true });
        }
        if (presetBefore != "null")
        {
            string originalPreset = $"document.querySelector('.equipment-live-presets [data-equipment-preset=' + {presetBefore} + ']')";
            await Click(originalPreset, "Equipment preset selection restore", $"({originalPreset})?.classList.contains('active')===true");
            actions.Add(new { name = "Equipment preset selection restore", exercised = true, selectionRestored = true, gearEdited = false });
        }

        R29SweepSurface afk = R29SweepSurfaces.First(surface => surface.Key == "squads-afk");
        await R29SweepNavigateAsync(core, afk, token, IsOwner);
        string cards = ".monster-afk-toolbar > .monster-afk-compact-card[role=button]";
        int cardCount = JsonSerializer.Deserialize<int>(await Read($"document.querySelectorAll({JsonSerializer.Serialize(cards)}).length"));
        int cardBefore = JsonSerializer.Deserialize<int>(await Read($"[...document.querySelectorAll({JsonSerializer.Serialize(cards)})].findIndex(b=>b.classList.contains('is-selected'))"));
        for (int i = 0; i < cardCount; i++)
        {
            await Click(Element(cards, i), "AFK settings card " + i, $"({Element(cards, i)})?.classList.contains('is-selected')===true");
            actions.Add(new { name = "AFK settings card", index = i, exercised = true, masterOrPotionOrDrillToggled = false });
        }
        if (cardBefore >= 0 && await Read($"({Element(cards, cardBefore)})?.classList.contains('is-selected')===true") != "true")
            await Click(Element(cards, cardBefore), "AFK settings card restore", $"({Element(cards, cardBefore)})?.classList.contains('is-selected')===true");
        // Drill settings hide the profile controls. Open the master settings
        // view temporarily when needed, then restore the original card later.
        if (cardCount > 0 && await Read("!!document.querySelector('.monster-afk-add-control')") != "true")
            await Click(Element(cards), "AFK profile controls view", $"({Element(cards)})?.classList.contains('is-selected')===true");
        // The config and native target catalogue arrive independently. Wait for
        // the Add button rather than claiming a disabled button was exercised.
        bool addReady = false;
        for (int poll = 0; poll < 600; poll++)
        {
            if (await Read("document.querySelector('.monster-afk-add-control > button')?.disabled===false") == "true") { addReady = true; break; }
            await Task.Delay(100, token);
        }
        if (addReady)
        {
            await Click(Element(".monster-afk-add-control > button"), "AFK Add menu open", "document.querySelector('.monster-afk-add-control > button')?.getAttribute('aria-expanded')==='true'&&!!document.querySelector('.monster-afk-add-menu')");
            await Click(Element(".monster-afk-add-control > button"), "AFK Add menu close", "document.querySelector('.monster-afk-add-control > button')?.getAttribute('aria-expanded')==='false'&&!document.querySelector('.monster-afk-add-menu')");
            actions.Add(new { name = "AFK Add menu", exercised = true, catalogueReady = true, strategyAdded = false });
        }
        else actions.Add(new { name = "AFK Add menu", exercised = false, reason = "native target catalogue did not enable Add within 60 seconds" });
        if (await Click(Element(".monster-afk-profile-select"), "AFK profile selection", "document.querySelector('.monster-afk-profile-select')?.getAttribute('aria-pressed')==='true'&&!!document.querySelector('.monster-afk-editor')", optional: true))
            actions.Add(new { name = "AFK profile selection", exercised = true, configEdited = false, originalEditorSelectionRestored = false });
        if (cardBefore >= 0 && await Read($"({Element(cards, cardBefore)})?.classList.contains('is-selected')===true") != "true")
            await Click(Element(cards, cardBefore), "AFK final settings view restore", $"({Element(cards, cardBefore)})?.classList.contains('is-selected')===true");

        R29SweepSurface hotkeys = R29SweepSurfaces.First(surface => surface.Key == "hotkeys");
        await R29SweepNavigateAsync(core, hotkeys, token, IsOwner);
        const string hotkeySwitches = ".hotkey-panel[data-hotkey-category=hotkeys] .hotkey-switch";
        int hotkeySwitchCount = JsonSerializer.Deserialize<int>(await Read($"document.querySelectorAll({JsonSerializer.Serialize(hotkeySwitches)}).length"));
        for (int i = 0; i < hotkeySwitchCount; i++)
            await ToggleAndRestore(Element(hotkeySwitches, i), "Hotkey configuration switch " + i, "b.querySelector('.ui-switch')?.classList.contains('is-on')===true");

        R29SweepSurface mini = R29SweepSurfaces.First(surface => surface.Key == "mini-games");
        await R29SweepNavigateAsync(core, mini, token, IsOwner);
        // Only the source-identified G card, never another Mini Games switch.
        string gCard = "[...document.querySelectorAll('.hotkey-panel[data-hotkey-category=miniGames] .hotkey-card')].find(b=>b.querySelector('kbd')?.textContent.trim()==='G')";
        await ToggleAndRestore($"({gCard})?.querySelector('.hotkey-switch')", "Mini G config switch", "b.querySelector('.ui-switch')?.classList.contains('is-on')===true");
        string gInput = $"({gCard})?.querySelector('input[type=number]')";
        string gBefore = await Read($"({gInput})?.value??null");
        if (gBefore != "null" && await Read($"({gInput})?.disabled===false") == "true")
        {
            string original = JsonSerializer.Deserialize<string>(gBefore) ?? "5";
            string alternate = original == "50" ? "49" : "50";
            async Task CommitG(string value)
            {
                await SetInput(gInput, value, "G reinforcement count edit");
                Owned();
                await core.ExecuteScriptAsync($"({gInput}).dispatchEvent(new FocusEvent('focusout',{{bubbles:true}}))");
                Owned();
                await core.ExecuteScriptAsync("window.__r29SweepCommitted=false;requestAnimationFrame(()=>requestAnimationFrame(()=>{window.__r29SweepCommitted=true;}));true");
                await Wait($"window.__r29SweepCommitted===true&&({gInput})?.disabled===false&&({gInput})?.value==={JsonSerializer.Serialize(value)}", "G reinforcement count blur save");
                await Task.Delay(250, token);
                await Wait($"({gInput})?.disabled===false&&({gInput})?.value==={JsonSerializer.Serialize(value)}", "G count acknowledged value retained");
            }
            try { await CommitG(alternate); }
            finally { await CommitG(original); }
            // React Activity reactivates the config-get effect on route return.
            await R29SweepNavigateAsync(core, hotkeys, token, IsOwner);
            await R29SweepNavigateAsync(core, mini, token, IsOwner);
            await Wait($"({gInput})?.value==={gBefore}&&({gInput})?.disabled===false", "G count saved restoration after route return");
            actions.Add(new { name = "G count change/blur/restore", exercised = true, restoredValueAfterRouteReturn = original, keyPressed = false });
        }
        else actions.Add(new { name = "G count change/blur/restore", exercised = false, reason = "G count input unavailable" });

        R29SweepSurface settings = R29SweepSurfaces.First(surface => surface.Key == "settings");
        await R29SweepNavigateAsync(core, settings, token, IsOwner);
        // The focus section follows visual metrics only when profiles are shown.
        // Its own section selector avoids indexing into visual metrics switches.
        await ToggleAndRestore(Element(".settings-panel > .update-panel:nth-of-type(2) .toggle-row"),
            "Settings account-focus preference", "b.getAttribute('aria-checked')==='true'");
        actions.Add(new { name = "Settings FPS/ping", exercised = false, reason = "separate Part-A exact-owner proof; not repeated in safe-control sweep" });

        // Finish via the actual Home Close Game button. If the R29 shell fix adds
        // an in-app confirmation, accept it through DOM only. Ownership is
        // checked before both clicks; its loss is expected after confirmation.
        await R29SweepNavigateAsync(core, home, token, IsOwner);
        Owned();
        string closeClicked = await core.ExecuteScriptAsync("""
            (() => {
              const buttons=[...document.querySelectorAll('.quick-actions-panel .game-controls button')];
              // A repair/update layout has no primary Start and is excluded.
              const close=buttons[0]?.classList.contains('primary')?buttons[1]:null;
              if(!close||close.disabled)return false;close.click();return true;
            })()
            """);
        if (closeClicked != "true") throw new InvalidDataException("R29 Home Close Game control unavailable.");
        await Wait("!!document.querySelector('.home-close-confirm[role=dialog] .scheme-modal-actions button.danger')", "Home Close Game confirmation");
        Owned();
        if (await core.ExecuteScriptAsync("(()=>{const b=document.querySelector('.home-close-confirm[role=dialog] .scheme-modal-actions button.danger');if(!b||b.disabled)return false;b.click();return true;})()") != "true")
            throw new InvalidDataException("R29 Home Close Game confirmation unavailable.");
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
