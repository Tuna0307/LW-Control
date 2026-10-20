using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace LWBridge.Desktop;

internal sealed partial class LWBridgeWindow
{
    // R29 Part A packaged proof. The proof drives only the production WebView DOM
    // and WebView-internal viewport emulation; it never sends OS mouse/keyboard
    // input. Game metrics are independently confirmed through the exact owned
    // FEATURE018 exchange, then both persisted and live settings are restored.
    private async Task RunFeature018R29PartAProofAsync(
        CoreWebView2 core, string outputPath, string directory,
        OverviewMapScanSession owner, string uid, int serverId,
        CancellationToken token)
    {
        OverviewLifecycleService lifecycle = overviewLifecycleService
            ?? throw new InvalidOperationException("FEATURE018 lifecycle missing");
        VisualMetricsConfigCommandService metricsConfig = visualMetricsConfigService
            ?? throw new InvalidDataException("R29 Part A visual metrics config is unavailable.");
        var approved = new Feature018HotkeySession(owner.ProfileId, owner.SessionId, uid, serverId);
        var game = new Feature018GameCommandService(new Feature018OwnedGameExchange(lifecycle),
            equipmentConfigService, new Feature018AuthenticatedGameAccount(uid, serverId),
            profileRuntimeConfigStore);

        bool IsOwner() => Feature018IsApprovedAccount(uid, serverId) &&
            lifecycle.OwnsExactProcess(owner.GamePid) &&
            lifecycle.MatchesOwnedMapScanSession(owner) &&
            ReadFeature018AuthenticatedAccount(lifecycle, owner) == approved;
        void Owned()
        {
            token.ThrowIfCancellationRequested();
            if (!IsOwner())
                throw new InvalidDataException(
                    "R29 Part A proof lost the exact authenticated owned game session.");
        }

        async Task WaitDom(string expression, string stage, int polls = 300)
        {
            for (int i = 0; i < polls; i++)
            {
                Owned();
                try
                {
                    if (await core.ExecuteScriptAsync(expression) == "true") return;
                }
                catch (InvalidOperationException) when (i < polls - 1) { }
                await Task.Delay(100, token);
            }
            throw new InvalidDataException("R29 Part A WebView did not settle: " + stage);
        }
        async Task<JsonElement> ReadDom(string expression)
        {
            Owned();
            string raw = await core.ExecuteScriptAsync(expression);
            using JsonDocument parsed = JsonDocument.Parse(raw);
            return parsed.RootElement.Clone();
        }
        async Task Capture(string name)
        {
            Owned();
            await using var file = File.Create(Path.Combine(directory, name));
            await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, file);
        }
        async Task CaptureViewport(string name, int expectedWidth)
        {
            Owned();
            string response = await core.CallDevToolsProtocolMethodAsync(
                "Page.captureScreenshot",
                "{\"format\":\"png\",\"fromSurface\":true,\"captureBeyondViewport\":false}");
            using JsonDocument result = JsonDocument.Parse(response);
            string? encoded = result.RootElement.TryGetProperty("data", out JsonElement data) &&
                              data.ValueKind == JsonValueKind.String
                ? data.GetString() : null;
            byte[] png = string.IsNullOrWhiteSpace(encoded)
                ? Array.Empty<byte>() : Convert.FromBase64String(encoded);
            if (png.Length < 24 || png[0] != 0x89 || png[1] != 0x50 || png[2] != 0x4e || png[3] != 0x47)
                throw new InvalidDataException("R29 Part A viewport screenshot was not a PNG.");
            int pixelWidth = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
            if (pixelWidth != expectedWidth)
                throw new InvalidDataException(
                    $"R29 Part A viewport screenshot width was {pixelWidth}px, expected {expectedWidth}px.");
            await File.WriteAllBytesAsync(Path.Combine(directory, name), png, token);
        }
        async Task ReloadAsync()
        {
            Owned();
            const string sentinel = "__lwbridgeR29ReloadSentinel";
            if (await core.ExecuteScriptAsync(
                    $"window.{sentinel}='before-reload';window.{sentinel}==='before-reload'") != "true")
                throw new InvalidDataException("R29 Part A could not establish its WebView reload sentinel.");
            core.Reload();
            for (int i = 0; i < 300; i++)
            {
                Owned();
                try
                {
                    if (await core.ExecuteScriptAsync(
                            $"document.readyState==='complete'&&!!document.querySelector('.app-shell')&&typeof window.{sentinel}==='undefined'") == "true")
                        return;
                }
                catch (InvalidOperationException) when (i < 299) { }
                catch (System.Runtime.InteropServices.COMException) when (i < 299) { }
                await Task.Delay(100, token);
            }
            throw new InvalidDataException("R29 Part A WebView reload did not establish a fresh document.");
        }
        async Task SetViewport(int width)
        {
            Owned();
            await core.CallDevToolsProtocolMethodAsync(
                "Emulation.setDeviceMetricsOverride",
                JsonSerializer.Serialize(new
                {
                    width,
                    height = 900,
                    deviceScaleFactor = 1,
                    mobile = false,
                }, JsonOptions.Default));
            await WaitDom($"window.innerWidth === {width}", "viewport " + width, 100);
        }
        async Task ClearViewport()
        {
            try
            {
                await core.CallDevToolsProtocolMethodAsync(
                    "Emulation.clearDeviceMetricsOverride", "{}");
            }
            catch { }
        }
        static JsonElement ToElement(object? value) => value is JsonElement element
            ? element.Clone()
            : JsonSerializer.SerializeToElement(value, JsonOptions.Default);
        async Task<VisualMetricsConfig> ReadPersistedMetrics() =>
            (VisualMetricsConfig)(await metricsConfig.InvokeAsync(
                "visual_metrics_config_get",
                JsonSerializer.SerializeToElement(new { }, JsonOptions.Default), token)
                ?? throw new InvalidDataException("R29 Part A visual metrics config disappeared."));
        async Task WaitPersistedMetrics(bool showFps, bool showPing, string stage)
        {
            for (int i = 0; i < 100; i++)
            {
                Owned();
                VisualMetricsConfig value = await ReadPersistedMetrics();
                if (value.ShowFps == showFps && value.ShowPing == showPing) return;
                await Task.Delay(100, token);
            }
            throw new InvalidDataException("R29 Part A persisted visual metrics did not settle: " + stage);
        }
        async Task WaitVisualUi(bool showFps, bool showPing, string stage) => await WaitDom(
            "(()=>{const p=document.querySelector('.settings-panel .update-panel');" +
            "const b=p?[...p.querySelectorAll('button[role=switch]')]:[];" +
            $"return b.length===2&&!b[0].disabled&&!b[1].disabled&&b[0].getAttribute('aria-checked')==='{(showFps ? "true" : "false")}'&&b[1].getAttribute('aria-checked')==='{(showPing ? "true" : "false")}';}})()",
            stage, 200);
        async Task ClickVisualToggle(int index, bool expectedFps, bool expectedPing, string stage)
        {
            Owned();
            string clicked = await core.ExecuteScriptAsync(
                "(()=>{const p=document.querySelector('.settings-panel .update-panel');" +
                $"const b=p?[...p.querySelectorAll('button[role=switch]')][{index}]:null;" +
                "if(!b||b.disabled)return false;b.click();return true;})()");
            if (clicked != "true")
                throw new InvalidDataException("R29 Part A Settings visual toggle unavailable: " + stage);
            await WaitPersistedMetrics(expectedFps, expectedPing, stage + " persistence");
            await WaitVisualUi(expectedFps, expectedPing, stage + " UI");
        }
        async Task SwitchLanguage(string language)
        {
            string languageJson = JsonSerializer.Serialize(language, JsonOptions.Default);
            string switched = await core.ExecuteScriptAsync($$"""
                (() => {
                  const select = document.querySelector('.language-select select');
                  if (!select) return false;
                  if (select.value !== {{languageJson}}) {
                    Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype, 'value')
                      .set.call(select, {{languageJson}});
                    select.dispatchEvent(new Event('change', { bubbles: true }));
                  }
                  return true;
                })()
                """);
            if (switched != "true")
                throw new InvalidDataException("R29 Part A language selector unavailable: " + language);
            await WaitDom(
                $"document.querySelector('.language-select select')?.value === {languageJson}",
                "language " + language, 100);
        }
        async Task<JsonElement> SidebarSnapshot() => await ReadDom("""
            (() => ({
              asideVisible: !!document.querySelector('.profile-sidebar'),
              restoreVisible: !!document.querySelector('.profile-sidebar-restore'),
              compactItemCount: document.querySelectorAll('.profile-compact-item').length,
              profileItemCount: document.querySelectorAll('.profile-item').length,
              singleProfileLayout: document.querySelector('.app-layout')?.classList.contains('single-profile') === true,
              storedHidden: localStorage.getItem('lwbridge.profileSidebarHidden'),
              activeRoute: [...document.querySelectorAll('.side-nav button')].findIndex(b => b.classList.contains('active')),
            }))()
            """);
        async Task<JsonElement> SettingsSnapshot() => await ReadDom("""
            (() => {
              const panel = document.querySelector('.settings-panel .update-panel');
              const toggles = panel ? [...panel.querySelectorAll('button[role=switch]')] : [];
              return {
                width: window.innerWidth,
                language: document.querySelector('.language-select select')?.value || '',
                settingsVisible: !!document.querySelector('.settings-panel'),
                visualToggleCount: toggles.length,
                showFps: toggles[0]?.getAttribute('aria-checked') === 'true',
                showPing: toggles[1]?.getAttribute('aria-checked') === 'true',
                alert: panel?.querySelector('[role=alert]')?.textContent || '',
              };
            })()
            """);

        JsonElement initialUi = await ReadDom("""
            (() => ({
              sidebarHidden: localStorage.getItem('lwbridge.profileSidebarHidden'),
              language: document.querySelector('.language-select select')?.value || localStorage.getItem('lwbridge.language') || 'en',
              activeRoute: [...document.querySelectorAll('.side-nav button')].findIndex(b => b.classList.contains('active')),
            }))()
            """);
        string? originalSidebarStored = initialUi.GetProperty("sidebarHidden").ValueKind == JsonValueKind.String
            ? initialUi.GetProperty("sidebarHidden").GetString() : null;
        string originalLanguage = initialUi.GetProperty("language").GetString() ?? "en";
        int originalRoute = initialUi.GetProperty("activeRoute").GetInt32();
        VisualMetricsConfig originalMetrics = await ReadPersistedMetrics();

        var screenshots = new List<object>();
        JsonElement jsonNull = JsonSerializer.SerializeToElement<object?>(null, JsonOptions.Default);
        JsonElement sidebarVisible = jsonNull, sidebarHidden = jsonNull, sidebarRestored = jsonNull;
        JsonElement settingsEn = jsonNull, settingsZhCn = jsonNull, confirmedMetrics = jsonNull,
            restoredGameMetrics = jsonNull;
        int productionToggleClicks = 0;
        bool sidebarPersistenceReloaded = false;
        bool persistedMetricsRestored = false;
        bool liveMetricsRestored = false;
        Exception? proofError = null;
        Exception? restoreError = null;

        try
        {
            // Keep the visual sidebar proof deterministic regardless of the user's
            // prior persisted choice, then prove the hidden choice survives a full
            // WebView page reload without restarting the exact game owner process.
            JsonElement initialSidebar = await SidebarSnapshot();
            if (!initialSidebar.GetProperty("asideVisible").GetBoolean())
            {
                if (!initialSidebar.GetProperty("restoreVisible").GetBoolean())
                    throw new InvalidDataException(
                        "R29 Part A requires a multi-profile-capable account sidebar.");
                await core.ExecuteScriptAsync("document.querySelector('.profile-sidebar-restore')?.click()");
            }
            await WaitDom(
                "!!document.querySelector('.profile-sidebar')&&!document.querySelector('.profile-sidebar-restore')&&document.querySelectorAll('.profile-compact-item').length===0",
                "account sidebar visible");
            sidebarVisible = await SidebarSnapshot();
            await Capture("r29-part-a-sidebar-visible.png");
            screenshots.Add(new { file = "r29-part-a-sidebar-visible.png", stage = "sidebar-visible" });

            if (await core.ExecuteScriptAsync(
                "(()=>{const b=document.querySelector('.profile-collapse');if(!b||b.disabled)return false;b.click();return true;})()") != "true")
                throw new InvalidDataException("R29 Part A sidebar hide control is unavailable.");
            await WaitDom(
                "!document.querySelector('.profile-sidebar')&&!!document.querySelector('.profile-sidebar-restore')&&document.querySelectorAll('.profile-compact-item').length===0&&document.querySelector('.app-layout')?.classList.contains('single-profile')===true&&localStorage.getItem('lwbridge.profileSidebarHidden')==='true'",
                "account sidebar hidden");

            await ReloadAsync();
            await WaitDom(
                "!document.querySelector('.profile-sidebar')&&!!document.querySelector('.profile-sidebar-restore')&&document.querySelectorAll('.profile-compact-item').length===0&&document.querySelector('.app-layout')?.classList.contains('single-profile')===true&&localStorage.getItem('lwbridge.profileSidebarHidden')==='true'",
                "persisted hidden sidebar after WebView reload", 300);
            sidebarPersistenceReloaded = true;
            sidebarHidden = await SidebarSnapshot();
            await Capture("r29-part-a-sidebar-hidden.png");
            screenshots.Add(new { file = "r29-part-a-sidebar-hidden.png", stage = "sidebar-hidden-after-reload" });

            if (await core.ExecuteScriptAsync(
                "(()=>{const b=document.querySelector('.profile-sidebar-restore');if(!b||b.disabled)return false;b.click();return true;})()") != "true")
                throw new InvalidDataException("R29 Part A external sidebar restore control is unavailable.");
            await WaitDom(
                "!!document.querySelector('.profile-sidebar')&&!document.querySelector('.profile-sidebar-restore')&&document.querySelectorAll('.profile-compact-item').length===0&&localStorage.getItem('lwbridge.profileSidebarHidden')==='false'",
                "account sidebar restored");
            sidebarRestored = await SidebarSnapshot();
            await Capture("r29-part-a-sidebar-restored.png");
            screenshots.Add(new { file = "r29-part-a-sidebar-restored.png", stage = "sidebar-restored" });

            // Settings is the eighth recovered route. Drive the actual production
            // switches, not a proof-only command, to establish persisted UI intent.
            if (await core.ExecuteScriptAsync(
                "(()=>{const b=document.querySelectorAll('.side-nav button')[7];if(!b)return false;b.click();return true;})()") != "true")
                throw new InvalidDataException("R29 Part A Settings navigation is unavailable.");
            await WaitDom("!!document.querySelector('.settings-panel')", "Settings route", 300);
            await WaitVisualUi(originalMetrics.ShowFps, originalMetrics.ShowPing,
                "initial Settings visual metrics");

            bool currentFps = originalMetrics.ShowFps;
            bool currentPing = originalMetrics.ShowPing;
            if (!currentFps)
            {
                currentFps = true;
                await ClickVisualToggle(0, currentFps, currentPing, "enable FPS");
                productionToggleClicks++;
            }
            if (!currentPing)
            {
                currentPing = true;
                await ClickVisualToggle(1, currentFps, currentPing, "enable Ping");
                productionToggleClicks++;
            }
            // Even when both settings were already enabled, exercise a real
            // production switch and return it to enabled before the metric read.
            if (productionToggleClicks == 0)
            {
                currentFps = false;
                await ClickVisualToggle(0, currentFps, currentPing, "exercise FPS off");
                productionToggleClicks++;
                currentFps = true;
                await ClickVisualToggle(0, currentFps, currentPing, "exercise FPS on");
                productionToggleClicks++;
            }

            object? confirmed = await game.InvokeAsync(
                "feature018_visual_metrics_configure",
                JsonSerializer.SerializeToElement(new
                {
                    showFps = true,
                    showPing = true,
                    profileId = owner.ProfileId,
                }, JsonOptions.Default), token);
            confirmedMetrics = ToElement(confirmed);
            if (confirmedMetrics.ValueKind != JsonValueKind.Object ||
                !confirmedMetrics.TryGetProperty("renderPath", out JsonElement renderPath) ||
                renderPath.ValueKind != JsonValueKind.String ||
                renderPath.GetString() != "GameFramework/UI/UIContainer/LWBridgeOverviewReady/VisualMetrics" ||
                !confirmedMetrics.TryGetProperty("fps", out JsonElement fps) ||
                fps.ValueKind != JsonValueKind.Number || !fps.TryGetDouble(out double fpsValue) ||
                !double.IsFinite(fpsValue) || fpsValue <= 0 ||
                !confirmedMetrics.TryGetProperty("pingMilliseconds", out JsonElement ping) ||
                ping.ValueKind != JsonValueKind.Number || !ping.TryGetDouble(out double pingValue) ||
                !double.IsFinite(pingValue) || pingValue < 0)
                throw new InvalidDataException(
                    "R29 Part A game did not confirm a rendered numeric FPS/ping sample.");

            await SwitchLanguage("en");
            await SetViewport(900);
            await core.ExecuteScriptAsync("document.querySelector('.settings-panel')?.scrollIntoView({block:'start'})");
            settingsEn = await SettingsSnapshot();
            if (settingsEn.GetProperty("width").GetInt32() != 900 ||
                settingsEn.GetProperty("language").GetString() != "en" ||
                settingsEn.GetProperty("visualToggleCount").GetInt32() != 2 ||
                !settingsEn.GetProperty("showFps").GetBoolean() ||
                !settingsEn.GetProperty("showPing").GetBoolean() ||
                !string.IsNullOrEmpty(settingsEn.GetProperty("alert").GetString()))
                throw new InvalidDataException("R29 Part A EN 900px Settings state is incomplete.");
            await CaptureViewport("r29-part-a-settings-en-900.png", 900);
            screenshots.Add(new { file = "r29-part-a-settings-en-900.png", stage = "settings-en", width = 900 });

            await SwitchLanguage("zh-CN");
            await SetViewport(1600);
            await core.ExecuteScriptAsync("document.querySelector('.settings-panel')?.scrollIntoView({block:'start'})");
            settingsZhCn = await SettingsSnapshot();
            if (settingsZhCn.GetProperty("width").GetInt32() != 1600 ||
                settingsZhCn.GetProperty("language").GetString() != "zh-CN" ||
                settingsZhCn.GetProperty("visualToggleCount").GetInt32() != 2 ||
                !settingsZhCn.GetProperty("showFps").GetBoolean() ||
                !settingsZhCn.GetProperty("showPing").GetBoolean() ||
                !string.IsNullOrEmpty(settingsZhCn.GetProperty("alert").GetString()))
                throw new InvalidDataException("R29 Part A zh-CN 1600px Settings state is incomplete.");
            await CaptureViewport("r29-part-a-settings-zh-CN-1600.png", 1600);
            screenshots.Add(new { file = "r29-part-a-settings-zh-CN-1600.png", stage = "settings-zh-CN", width = 1600 });

            // Return through production Settings controls before the native
            // safety restoration below. This leaves React, persistence and game
            // state aligned in the successful path.
            if (currentPing != originalMetrics.ShowPing)
            {
                currentPing = originalMetrics.ShowPing;
                await ClickVisualToggle(1, currentFps, currentPing, "restore Ping");
                productionToggleClicks++;
            }
            if (currentFps != originalMetrics.ShowFps)
            {
                currentFps = originalMetrics.ShowFps;
                await ClickVisualToggle(0, currentFps, currentPing, "restore FPS");
                productionToggleClicks++;
            }
        }
        catch (Exception error) { proofError = error; }
        finally
        {
            await ClearViewport();

            // Restore persisted settings even after a partial proof. Restore the
            // live game only while the exact original owner/session is still valid.
            try
            {
                object? saved = await metricsConfig.InvokeAsync(
                    "visual_metrics_config_save",
                    JsonSerializer.SerializeToElement(new
                    {
                        showFps = originalMetrics.ShowFps,
                        showPing = originalMetrics.ShowPing,
                    }, JsonOptions.Default), CancellationToken.None);
                VisualMetricsConfig restored = (VisualMetricsConfig)(saved
                    ?? throw new InvalidDataException("R29 Part A metrics restore returned no config."));
                persistedMetricsRestored = restored == originalMetrics;
                if (!persistedMetricsRestored)
                    throw new InvalidDataException("R29 Part A persisted metric settings did not restore exactly.");

                if (!IsOwner())
                    throw new InvalidDataException(
                        "R29 Part A owner changed before live metric restoration.");
                restoredGameMetrics = ToElement(await game.InvokeAsync(
                    "feature018_visual_metrics_configure",
                    JsonSerializer.SerializeToElement(new
                    {
                        showFps = originalMetrics.ShowFps,
                        showPing = originalMetrics.ShowPing,
                        profileId = owner.ProfileId,
                    }, JsonOptions.Default), CancellationToken.None));
                liveMetricsRestored = restoredGameMetrics.ValueKind == JsonValueKind.Object &&
                    restoredGameMetrics.TryGetProperty("showFps", out JsonElement restoredFps) &&
                    restoredFps.ValueKind is JsonValueKind.True or JsonValueKind.False &&
                    restoredFps.GetBoolean() == originalMetrics.ShowFps &&
                    restoredGameMetrics.TryGetProperty("showPing", out JsonElement restoredPing) &&
                    restoredPing.ValueKind is JsonValueKind.True or JsonValueKind.False &&
                    restoredPing.GetBoolean() == originalMetrics.ShowPing;
                if (!liveMetricsRestored)
                    throw new InvalidDataException("R29 Part A live metric settings did not restore exactly.");
            }
            catch (Exception error) { restoreError = error; }

            // Restore non-game UI preferences touched by the proof. These are
            // WebView DOM actions/localStorage only; no process/window input.
            try
            {
                string sidebarValue = JsonSerializer.Serialize(originalSidebarStored, JsonOptions.Default);
                await core.ExecuteScriptAsync(originalSidebarStored is null
                    ? "localStorage.removeItem('lwbridge.profileSidebarHidden')"
                    : "localStorage.setItem('lwbridge.profileSidebarHidden'," + sidebarValue + ")");
                if (originalSidebarStored == "true")
                    await core.ExecuteScriptAsync("document.querySelector('.profile-collapse')?.click()");
                else
                    await core.ExecuteScriptAsync("document.querySelector('.profile-sidebar-restore')?.click()");
                await SwitchLanguage(originalLanguage);
                if (originalRoute is >= 0 and < 8)
                    await core.ExecuteScriptAsync(
                        $"document.querySelectorAll('.side-nav button')[{originalRoute}]?.click()");
            }
            catch { }
        }

        object ownerReceipt = new
        {
            owner.ProfileId,
            owner.SessionId,
            owner.GamePid,
            owner.GamePath,
            owner.GameStartedAtUtc,
            uid,
            serverId,
        };
        var receiptOptions = new JsonSerializerOptions(JsonOptions.Default) { WriteIndented = true };
        if (proofError is not null || restoreError is not null)
        {
            await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                state = "partial",
                surface = "r29-part-a",
                proofError = proofError?.Message,
                restoreError = restoreError?.Message,
                owner = ownerReceipt,
                domOnly = true,
                osInputUsed = false,
                screenshots,
                sidebarProof = new { sidebarVisible, sidebarHidden, sidebarRestored, sidebarPersistenceReloaded },
                visualMetrics = new
                {
                    originalPersisted = originalMetrics,
                    productionToggleClicks,
                    confirmedResponse = confirmedMetrics,
                    metricSource = new
                    {
                        confirmedBy = "Feature018OwnedGameExchange state=confirmed response",
                        fps = "UnityEngine.Time.smoothDeltaTime -> unscaledDeltaTime",
                        ping = confirmedMetrics.ValueKind == JsonValueKind.Object &&
                               confirmedMetrics.TryGetProperty("pingSource", out JsonElement source)
                            ? source.GetString() : null,
                    },
                    persistedMetricsRestored,
                    liveMetricsRestored,
                    restoredGameResponse = restoredGameMetrics,
                },
            }, receiptOptions), CancellationToken.None);
            if (restoreError is not null)
                throw new InvalidDataException("R29 Part A restoration failed.", restoreError);
            throw proofError!;
        }

        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            state = "proven",
            surface = "r29-part-a",
            proof = "current_tree_webview_sidebar_persistence_settings_localization_and_confirmed_visual_metrics",
            owner = ownerReceipt,
            domOnly = true,
            osInputUsed = false,
            viewportMethod = "CoreWebView2 DevTools Emulation.setDeviceMetricsOverride + Page.captureScreenshot; PNG widths verified (no OS window/input operation)",
            screenshots,
            sidebarProof = new
            {
                visible = sidebarVisible,
                hiddenAfterReload = sidebarHidden,
                restored = sidebarRestored,
                persistenceExercisedByWebViewReload = sidebarPersistenceReloaded,
                originalStoredPreference = originalSidebarStored,
            },
            settingsProof = new
            {
                en900 = settingsEn,
                zhCn1600 = settingsZhCn,
            },
            visualMetrics = new
            {
                originalPersisted = originalMetrics,
                productionToggleClicks,
                confirmedResponse = confirmedMetrics,
                metricSource = new
                {
                    confirmedBy = "Feature018OwnedGameExchange state=confirmed response",
                    renderPath = confirmedMetrics.GetProperty("renderPath").GetString(),
                    fps = "UnityEngine.Time.smoothDeltaTime -> unscaledDeltaTime",
                    ping = confirmedMetrics.TryGetProperty("pingSource", out JsonElement pingSource)
                        ? pingSource.GetString() : null,
                    fpsValue = confirmedMetrics.GetProperty("fps").GetDouble(),
                    pingMilliseconds = confirmedMetrics.GetProperty("pingMilliseconds").GetDouble(),
                },
                restoredSettings = new
                {
                    persisted = persistedMetricsRestored,
                    liveGame = liveMetricsRestored,
                    persistedValue = await ReadPersistedMetrics(),
                    gameResponse = restoredGameMetrics,
                },
            },
            finishedAtUtc = DateTimeOffset.UtcNow,
        }, receiptOptions), token);
    }
}
