using System.Diagnostics;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using LWControl.Core;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace LWControl.Desktop;

/// <summary>
/// Browser-rendered host for the recovered Build 189 presentation layer.
/// The HTML/CSS/React bundle is preserved byte-for-byte; this host replaces only
/// the original native bridge with the rebuild's fail-closed capability boundary.
/// </summary>
internal sealed class ReferenceWebViewForm : Form
{
    private const string UiResourceName = "LWControl.Desktop.WebUi.ReferenceOverlay.html";
    private const string UiVirtualHost = "lwc-ui.local";
    private readonly string settingsPath;
    private readonly string webSettingsPath;
    private readonly string? smokeOutput;
    private readonly bool referenceOnly;
    private readonly bool smokeMode;
    private readonly WebView2 webView = new() { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.Transparent };
    private readonly System.Windows.Forms.Timer reconnectTimer = new();
    private readonly System.Windows.Forms.Timer hotkeyTimer = new();
    private readonly System.Windows.Forms.Timer automationTimer = new();
    private readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };
    private readonly TaskCompletionSource navigationReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource uiReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<string> sessionLog = [];
    private readonly RecoveredGameCommandBridge recoveredBridge = new();
    private readonly EquipmentSchemeStore equipmentSchemeStore;
    private readonly CancellationTokenSource commandWatchCancellation = new();
    private readonly ConcurrentDictionary<string, string> recoveredPendingCommands = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> nextAutomationAt = new(StringComparer.Ordinal);
    private readonly HashSet<Keys> gameplayHotkeysHeld = [];
    private readonly ShieldHotkeyStateMachine shieldHotkeyState = new();
    private DesktopAppearance appearance = new();
    private WebUiHostState hostState = new();
    private CurrentWorldMapScanRecord[] worldRecords = [];
    private int bootstrapSent;
    private bool initialized;
    private bool reconnectCheckActive;
    private bool menuEnabled = true;
    private bool menuToggleAwaitingRelease;
    private bool shieldUseHotkeyEnqueueing;
    private long lastMenuToggleTick;

    public ReferenceWebViewForm(string settingsPath, string? smokeOutput, bool referenceOnly, bool smokeMode)
    {
        this.settingsPath = Path.GetFullPath(settingsPath);
        webSettingsPath = this.settingsPath + ".web-ui.json";
        equipmentSchemeStore = new EquipmentSchemeStore(Path.Combine(
            Path.GetDirectoryName(this.settingsPath)!, "equipment-schemes.json"));
        this.smokeOutput = smokeOutput;
        this.referenceOnly = referenceOnly;
        this.smokeMode = smokeMode;

        Text = "LW CONTROL";
        BackColor = Color.FromArgb(11, 12, 16);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1280, 820);
        MinimumSize = Size.Empty;
        TopMost = !smokeMode;
        ShowInTaskbar = smokeMode;
        Controls.Add(webView);

        Directory.CreateDirectory(Path.GetDirectoryName(this.settingsPath)!);
        LoadPersistentUiState();
        reconnectTimer.Interval = 3000;
        reconnectTimer.Tick += async (_, _) => await CheckAutoReconnectAsync();
        if (hostState.AutoReconnectEnabled) reconnectTimer.Start();
        hotkeyTimer.Interval = 50;
        hotkeyTimer.Tick += OnHotkeyTick;
        if (!smokeMode) hotkeyTimer.Start();
        automationTimer.Interval = 1000;
        automationTimer.Tick += async (_, _) => await PumpContinuousAutomationsAsync();
        if (!smokeMode) automationTimer.Start();
        Shown += OnShown;
        FormClosing += OnFormClosing;
        SizeChanged += (_, _) => _ = PostLayoutAsync();
    }

    private async void OnShown(object? sender, EventArgs e)
    {
        try
        {
            await InitializeAsync();
            if (smokeMode)
            {
                await RunVisualSmokeAsync();
                Environment.ExitCode = 0;
                Close();
            }
        }
        catch (Exception ex)
        {
            Environment.ExitCode = 1;
            Log("WebView initialization failed: " + ex);
            if (smokeOutput is not null)
            {
                Directory.CreateDirectory(smokeOutput);
                File.WriteAllText(Path.Combine(smokeOutput, "webview-smoke-error.txt"), ex.ToString());
                Close();
            }
            else
            {
                MessageBox.Show(this, ex.Message, "LW Control", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private async Task InitializeAsync()
    {
        if (initialized) return;
        initialized = true;

        string userDataDirectory = smokeMode
            ? Path.Combine(smokeOutput ?? Path.GetTempPath(), referenceOnly ? "webview-reference-profile" : "webview-rebuild-profile")
            : Path.Combine(Path.GetDirectoryName(settingsPath)!, "WebView2");
        Directory.CreateDirectory(userDataDirectory);
        CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataDirectory);
        await webView.EnsureCoreWebView2Async(environment);
        ConfigureWebView(webView.CoreWebView2);
        await NavigateToRecoveredUiAsync();
        await navigationReady.Task.WaitAsync(TimeSpan.FromSeconds(15));
        await SeedAppearanceAsync();
        await EnsureRecoveredUiMountedAsync();
        await uiReady.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await WaitForShellAsync();
        if (!referenceOnly)
            await InstallCapabilityLayerAsync();
        await PostLayoutAsync();
        Log($"Recovered WebView UI mounted; referenceOnly={referenceOnly}; runtime={environment.BrowserVersionString}.");
    }

    private void ConfigureWebView(CoreWebView2 core)
    {
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.AreDevToolsEnabled = smokeMode;
        core.Settings.IsStatusBarEnabled = false;
        core.NavigationCompleted += (_, args) =>
        {
            if (args.IsSuccess) navigationReady.TrySetResult();
            else navigationReady.TrySetException(new InvalidOperationException($"WebView navigation failed: {args.WebErrorStatus}"));
        };
        core.WebMessageReceived += OnWebMessageReceived;
        core.NewWindowRequested += (_, args) => args.Handled = true;
    }

    private async Task NavigateToRecoveredUiAsync()
    {
        string uiDirectory = Path.Combine(Path.GetDirectoryName(settingsPath)!, "web-ui");
        Directory.CreateDirectory(uiDirectory);
        string documentPath = Path.Combine(uiDirectory, "reference-overlay.html");
        await File.WriteAllBytesAsync(documentPath, ReadRecoveredUiBytes());
        webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            UiVirtualHost, uiDirectory, CoreWebView2HostResourceAccessKind.DenyCors);
        webView.CoreWebView2.Navigate($"https://{UiVirtualHost}/reference-overlay.html");
    }

    private static byte[] ReadRecoveredUiBytes()
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(UiResourceName)
            ?? throw new InvalidOperationException("Recovered UI resource is missing from the desktop build.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private async Task SeedAppearanceAsync()
    {
        string language = appearance.Language == UiLanguage.English.ToString() ? "en" : "zh";
        string theme = appearance.Accent switch
        {
            "Cyan" => "theme-cyan",
            "Gold" => "theme-amber",
            "Rose" => "theme-rose",
            "Emerald" => "theme-emerald",
            _ => "theme-indigo",
        };
        string script = $"localStorage.setItem('lwc-language',{JsonSerializer.Serialize(language)});" +
                        $"localStorage.setItem('lwc-theme',{JsonSerializer.Serialize(theme)});true;";
        await webView.CoreWebView2.ExecuteScriptAsync(script);
    }

    private async Task EnsureRecoveredUiMountedAsync()
    {
        await Task.Delay(75);
        string result = await webView.CoreWebView2.ExecuteScriptAsync("""
            (() => {
              const root = document.getElementById('root');
              if (!root) return JSON.stringify({ok:false,error:'root_not_found'});
              if (root.childElementCount > 0) return JSON.stringify({ok:true,mode:'automatic'});
              const entry = document.querySelector('script[data-lwc-entry]');
              if (!entry?.textContent) return JSON.stringify({ok:false,error:'entry_not_found'});
              try {
                globalThis.__lwcBootErrors=[];
                window.addEventListener('error', e => globalThis.__lwcBootErrors.push(String(e.error?.stack||e.message||'window_error')));
                window.addEventListener('unhandledrejection', e => globalThis.__lwcBootErrors.push(String(e.reason?.stack||e.reason||'unhandled_rejection')));
                (new Function(entry.textContent))();
                return JSON.stringify({ok:true,mode:'host_eval'});
              } catch (error) {
                return JSON.stringify({ok:false,error:String(error?.stack||error?.message||error)});
              }
            })()
            """);
        for (int attempt = 0; attempt < 40; attempt++)
        {
            await Task.Delay(50);
            if (string.Equals(await webView.CoreWebView2.ExecuteScriptAsync(
                    "document.getElementById('root')?.childElementCount > 0"), "true", StringComparison.OrdinalIgnoreCase))
                return;
        }
        string errors = await webView.CoreWebView2.ExecuteScriptAsync("JSON.stringify(globalThis.__lwcBootErrors||[])");
        throw new InvalidOperationException($"Recovered UI did not mount: {result}; errors={errors}");
    }

    private async Task WaitForShellAsync()
    {
        for (int attempt = 0; attempt < 60; attempt++)
        {
            await Task.Delay(50);
            if (string.Equals(await webView.CoreWebView2.ExecuteScriptAsync(
                    "Boolean(document.querySelector('.overlay-shell') && document.querySelectorAll('[data-tab]').length===6)"),
                    "true", StringComparison.OrdinalIgnoreCase))
                return;
        }
        throw new InvalidOperationException("Recovered UI bootstrap did not produce the six-tab application shell.");
    }

    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(args.WebMessageAsJson);
            JsonElement root = document.RootElement;
            string? type = root.TryGetProperty("type", out JsonElement typeNode) ? typeNode.GetString() : null;
            if (type is null) return;
            switch (type)
            {
                case "ui_ready":
                    await SendBootstrapAsync();
                    uiReady.TrySetResult();
                    break;
                case "refresh_health":
                    await SendHealthAsync();
                    break;
                case "launch_game":
                    if (!smokeMode) await LaunchGameAsync();
                    else await PostBlockedCommandAsync("launch_game", "Smoke mode never launches the game.");
                    break;
                case "run_feature":
                    await HandleRunFeatureAsync(root);
                    break;
                case "query_world_intelligence":
                    await SendWorldIntelligenceAsync(root);
                    break;
                case "clear_world_intelligence":
                    worldRecords = [];
                    Post(new { type = "world_intelligence_clear_result", ok = true, deleted = 0 });
                    break;
                case "control_world_block_scan":
                    await ControlWorldBlockScanAsync(root);
                    break;
                case "set_feature_enabled":
                    await SetFeatureEnabledAsync(root);
                    break;
                case "save_feature_config":
                    await SaveFeatureConfigurationAsync(root);
                    break;
                case "reset_feature_runtime":
                    ResetFeatureRuntime(root);
                    break;
                case "set_menu_hotkey":
                    SaveMenuHotkey(root);
                    break;
                case "save_gameplay_hotkeys":
                    SaveGameplayHotkeys(root);
                    break;
                case "save_equipment_scheme":
                    await SaveEquipmentSchemeAsync(root);
                    break;
                case "export_logs":
                    await ExportLogsAsync();
                    break;
                case "begin_drag":
                    BeginWindowDrag();
                    break;
                case "begin_resize":
                    BeginWindowResize(root);
                    break;
                case "close_window":
                    Close();
                    break;
                case "rebuild_appearance_changed":
                    SaveAppearanceFromMessage(root);
                    break;
                case "map_scan_delta_ack":
                case "map_scan_delta_gap":
                    break;
                default:
                    Post(new { type = "host_error", message = $"Unsupported host message: {type}" });
                    break;
            }
        }
        catch (Exception ex)
        {
            Log("Web message failed: " + ex.Message);
            Post(new { type = "host_error", message = ex.Message });
        }
    }

    private async Task SendBootstrapAsync()
    {
        if (Interlocked.CompareExchange(ref bootstrapSent, 1, 0) != 0) return;
        var inspection = new LastWarStartupClient().Inspect();
        var features = ReferenceFeatureCatalog.All.Select(feature => new
        {
            id = feature.Id,
            title = feature.Name,
            description = feature.Description,
            category = feature.Group.ToString(),
            hotkey = (string?)null,
            implemented = feature.State != FeatureImplementationState.Pending,
        }).ToArray();
        var automation = ReferenceFeatureCatalog.All.ToDictionary(
            feature => feature.Id,
            feature => (object)new
            {
                config = hostState.FeatureConfigs.TryGetPropertyValue(feature.Id, out JsonNode? node) ? node : new JsonObject(),
                runtime = new
                {
                    enabled = hostState.FeatureEnabled.TryGetPropertyValue(feature.Id, out JsonNode? enabledNode)
                        && enabledNode is not null && enabledNode.GetValue<bool>(),
                    state = hostState.FeatureEnabled.TryGetPropertyValue(feature.Id, out JsonNode? stateNode)
                        && stateNode is not null && stateNode.GetValue<bool>() ? "Enabled" : "Idle",
                    nextRunAt = (string?)null,
                    lastRunAt = (string?)null,
                },
            }, StringComparer.Ordinal);
        Post(new
        {
            type = "bootstrap",
            smokeRun = smokeMode,
            appVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0",
            buildNumber = 189,
            authorizationExpiresAt = (string?)null,
            currentServerId = (int?)null,
            features,
            health = CreateHealthPayload(inspection),
            menuHotkey = new { key = hostState.MenuHotkey, options = new[] { "MouseRight" }, registered = !smokeMode },
            gameplayHotkeys = CurrentGameplayHotkeys(),
            equipmentSchemes = equipmentSchemeStore.Summaries,
            layout = CreateLayoutPayload(),
            featureAutomation = automation,
        });
    }

    private object CreateHealthPayload((bool GameRunning, CurrentWorldMapRuntimeInspection World, CurrentDailyTaskRuntimeInspection Daily) inspection) =>
        CreateRecoveredHealthPayload();

    private object CreateRecoveredHealthPayload()
    {
        LocalBridgeInspection bridge = recoveredBridge.Inspect();
        return new
        {
            gameRunning = bridge.GameRunning,
            gameProcessId = bridge.GameProcessId,
            queueWritable = bridge.BridgeHealthy && bridge.PendingCommandCount < 64,
            pendingCommands = bridge.PendingCommandCount,
            luaBridgeReady = bridge.BridgeHealthy,
            checkedAt = DateTimeOffset.UtcNow,
        };
    }

    private static int? FindGameProcessId()
    {
        try { return Process.GetProcessesByName("LastWar").FirstOrDefault()?.Id; }
        catch { return null; }
    }

    private async Task SendHealthAsync()
    {
        var inspection = new LastWarStartupClient().Inspect();
        Post(new { type = "health_update", currentServerId = (int?)null, health = CreateHealthPayload(inspection) });
        await Task.CompletedTask;
    }

    private async Task QueueRecoveredFeatureAsync(string featureId, Dictionary<string, string?> arguments)
    {
        LocalBridgeInspection inspection = recoveredBridge.Inspect();
        if (!inspection.BridgeHealthy)
        {
            await PostBlockedCommandAsync(featureId, $"Recovered game bridge is not ready: {inspection.StatusCode}.");
            return;
        }

        string commandId = NewCommandId("lw");
        RecoveredBridgeReceipt receipt = await recoveredBridge.EnqueueAsync(
            commandId, featureId, arguments, commandWatchCancellation.Token);
        if (!receipt.Accepted)
        {
            PostCommand(commandId, featureId, "Failed", receipt.Error ?? "Recovered bridge rejected the command.", false);
            return;
        }

        recoveredPendingCommands[commandId] = featureId;
        PostCommand(commandId, featureId, "AwaitingEvidence", "Command accepted by the recovered game bridge; waiting for correlated evidence.");
        _ = WatchRecoveredResultAsync(commandId, featureId, arguments);
        await SendHealthAsync();
    }

    private async Task WatchRecoveredResultAsync(string commandId, string featureId, Dictionary<string, string?> arguments)
    {
        try
        {
            TimeSpan timeout = featureId == "map_scan" ? TimeSpan.FromMinutes(12) : TimeSpan.FromMinutes(3);
            RecoveredBridgeResult? result = await recoveredBridge.WaitForResultAsync(
                commandId, timeout, commandWatchCancellation.Token);
            if (result is null)
            {
                PostCommand(commandId, featureId, "Failed", "Timed out waiting for correlated game evidence.", false);
                return;
            }
            if (!string.Equals(result.CommandId, commandId, StringComparison.Ordinal)
                || !string.Equals(result.FeatureId, featureId, StringComparison.Ordinal))
            {
                PostCommand(commandId, featureId, "Failed", "Game result identity did not match the queued command.", false);
                return;
            }

            string status = result.IsBlocked ? "Blocked" : result.IsSucceeded ? "Succeeded" : "Failed";
            PostCommand(commandId, featureId, status, result.Describe(), result.ContractComplete, result.Total);
            PostRecoveredFeatureOutput(commandId, featureId, arguments, result);
            await SendHealthAsync();
        }
        catch (OperationCanceledException) when (commandWatchCancellation.IsCancellationRequested) { }
        catch (Exception ex)
        {
            PostCommand(commandId, featureId, "Failed", ex.Message, false);
        }
        finally
        {
            recoveredPendingCommands.TryRemove(commandId, out _);
        }
    }

    private void PostRecoveredFeatureOutput(string commandId, string featureId,
        IReadOnlyDictionary<string, string?> arguments, RecoveredBridgeResult result)
    {
        if (result.Output.ValueKind != JsonValueKind.Object) return;
        if (arguments.TryGetValue("data_view", out string? dataView)
            && !string.IsNullOrWhiteSpace(dataView)
            && string.Equals(arguments.GetValueOrDefault("mode"), "state", StringComparison.Ordinal))
        {
            GameDataSnapshot snapshot = GameDataSnapshotBuilder.Build(dataView, featureId, result.Output);
            Post(new { type = "game_data_snapshot", commandId, payload = snapshot });
        }
        if (featureId == "team_swap_outfit")
        {
            string mode = arguments.GetValueOrDefault("mode") ?? string.Empty;
            try
            {
                if (string.Equals(mode, "state", StringComparison.Ordinal))
                {
                    EquipmentStatePreview preview = equipmentSchemeStore.Preview(result.Output);
                    if (preview.Available && result.IsSucceeded)
                    {
                        equipmentSchemeStore.ReconcileActiveTeams(preview);
                        PostEquipmentSchemes();
                    }
                    Post(new { type = "equipment_state_update", payload = preview });
                }
                else if (string.Equals(mode, "capture_scheme", StringComparison.Ordinal) && result.IsSucceeded)
                {
                    int slot = int.TryParse(arguments.GetValueOrDefault("scheme_slot"), out int parsed) ? parsed : 0;
                    EquipmentScheme scheme = equipmentSchemeStore.Capture(slot,
                        arguments.GetValueOrDefault("scheme_name"), result.Output);
                    PostEquipmentSchemes(true, $"Equipment scheme {scheme.Slot} captured.");
                }
                else if (string.Equals(mode, "apply_scheme", StringComparison.Ordinal) && result.IsSucceeded)
                {
                    int slot = int.TryParse(arguments.GetValueOrDefault("scheme_slot"), out int parsed) ? parsed : 0;
                    int? teamIndex = int.TryParse(arguments.GetValueOrDefault("team_index"), out int team) ? team : null;
                    if (equipmentSchemeStore.TryMarkVerifiedApplied(slot, teamIndex,
                            arguments.GetValueOrDefault("assignments_json"), DateTimeOffset.UtcNow))
                        PostEquipmentSchemes();
                }
                else if (string.Equals(mode, "swap", StringComparison.Ordinal) && result.IsSucceeded)
                {
                    equipmentSchemeStore.ClearActiveVerifications();
                    PostEquipmentSchemes();
                }
            }
            catch (InvalidOperationException ex)
            {
                Post(new { type = "host_error", message = ex.Message });
            }
        }
        if (featureId == "shield_display")
            Post(new { type = "shield_display_data", commandId, payload = result.Output });
        if (featureId == "map_scan" && arguments.GetValueOrDefault("mode") is "block_scan_start" or "block_scan_stop" or "block_scan_clear")
            Post(new { type = "world_block_scan_status", commandId, payload = result.Output });
    }

    private async Task ControlWorldBlockScanAsync(JsonElement root)
    {
        string action = StringProperty(root, "action", "start").ToLowerInvariant();
        if (action is not ("start" or "stop" or "clear"))
        {
            await PostBlockedCommandAsync("map_scan", "Invalid block scan action.");
            return;
        }

        Dictionary<string, string?> arguments = WorldBlockScanCommandArguments.Create(
            action,
            IntProperty(root, "concurrency", 4, 1, 20),
            IntProperty(root, "blockCount", 9, 1, 99),
            IntProperty(root, "blockSize", 64, 16, 512),
            StringProperty(root, "serverId"),
            IntProperty(root, "centerBlockX", 50, 0, 10000),
            IntProperty(root, "centerBlockY", 50, 0, 10000));
        Post(new
        {
            type = "world_block_scan_status",
            payload = new
            {
                state = action == "start" ? "queued" : action == "stop" ? "stopping" : "clearing",
                action,
                concurrency = arguments.GetValueOrDefault("block_concurrency"),
                blockCount = arguments.GetValueOrDefault("scan_block_count"),
                blockSize = arguments.GetValueOrDefault("scan_block_size"),
            },
        });
        await QueueRecoveredFeatureAsync("map_scan", arguments);
    }

    private async Task SetFeatureEnabledAsync(JsonElement root)
    {
        string featureId = StringProperty(root, "featureId");
        bool enabled = root.TryGetProperty("enabled", out JsonElement enabledNode)
            && enabledNode.ValueKind is JsonValueKind.True or JsonValueKind.False && enabledNode.GetBoolean();
        if (featureId is not ("auto_radar" or "auto_join_rally" or "daily_free_claims" or
            "troop_promotion" or "use_stamina_item" or "hospital_heal" or "apply_position" or "alliance_train"))
        {
            Post(new { type = "feature_config_result", featureId, ok = false, message = "feature_toggle_not_supported" });
            return;
        }
        hostState.FeatureEnabled[featureId] = enabled;
        if (FeatureConfig(featureId) is JsonObject savedConfig) savedConfig["enabled"] = enabled;
        SaveHostState();

        if (featureId == "auto_join_rally" || featureId == "alliance_train")
        {
            var arguments = new Dictionary<string, string?> { ["mode"] = enabled ? "start" : "stop" };
            RecoveredFeatureCommandArguments.Apply(featureId, FeatureConfig(featureId), enabled, arguments);
            if (featureId == "auto_join_rally")
                RecoveredFeatureCommandArguments.Apply("use_stamina_item", FeatureConfig("use_stamina_item"),
                    FeatureEnabled("use_stamina_item"), arguments);
            await QueueRecoveredFeatureAsync(featureId, arguments);
        }
        else if (!enabled && featureId is "auto_radar" or "daily_free_claims" or "troop_promotion")
        {
            var arguments = new Dictionary<string, string?> { ["mode"] = "stop" };
            RecoveredFeatureCommandArguments.Apply(featureId, FeatureConfig(featureId), enabled, arguments);
            await QueueRecoveredFeatureAsync(featureId, arguments);
        }
        else if (featureId == "use_stamina_item")
        {
            var arguments = new Dictionary<string, string?> { ["mode"] = "auto_configure" };
            RecoveredFeatureCommandArguments.Apply(featureId, FeatureConfig(featureId), enabled, arguments);
            await QueueRecoveredFeatureAsync(featureId, arguments);
        }

        Post(new
        {
            type = "feature_config_result", featureId, ok = true,
            message = enabled ? "feature_enabled" : "feature_disabled",
            payload = new { config = FeatureConfig(featureId) ?? new JsonObject(), runtime = new { enabled, state = enabled ? "Idle" : "Disabled" } },
        });
        PostAutomationUpdate(featureId);
    }

    private async Task HandleAutoReconnectAsync(string mode)
    {
        bool enabled = !string.Equals(mode, "disable", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(mode, "stop", StringComparison.OrdinalIgnoreCase);
        hostState.AutoReconnectEnabled = enabled;
        SaveHostState();
        if (enabled)
        {
            reconnectTimer.Start();
            await CheckAutoReconnectAsync();
        }
        else reconnectTimer.Stop();
        PostCommand(NewCommandId("reconnect"), "auto_reconnect", "Succeeded",
            enabled ? "Auto Reconnect enabled." : "Auto Reconnect disabled.", true);
    }

    private async Task CheckAutoReconnectAsync()
    {
        if (!hostState.AutoReconnectEnabled || reconnectCheckActive || smokeMode) return;
        reconnectCheckActive = true;
        try
        {
            if (!recoveredBridge.Inspect().GameRunning)
                await new LastWarStartupClient().StartAsync(Log);
        }
        catch (Exception ex) { Log("Auto Reconnect: " + ex.Message); }
        finally { reconnectCheckActive = false; }
    }

    private async Task LaunchGameAsync()
    {
        string commandId = NewCommandId("launch");
        PostCommand(commandId, "launch_game", "Queued", "Starting the official Last War client.");
        try
        {
            var result = await new LastWarStartupClient().StartAsync(Log);
            PostCommand(commandId, "launch_game", "Succeeded", $"Official game ready via {result.LaunchPath}.", true);
            await SendHealthAsync();
        }
        catch (Exception ex)
        {
            PostCommand(commandId, "launch_game", "Failed", ex.Message, false);
        }
    }

    private async Task HandleRunFeatureAsync(JsonElement root)
    {
        string featureId = StringProperty(root, "featureId");
        Dictionary<string, string?> arguments = ReadArguments(root);
        string mode = arguments.GetValueOrDefault("mode") ?? "";
        if (smokeMode)
        {
            await PostBlockedCommandAsync(featureId, "Smoke mode is fixture-only and never sends a game command.");
            return;
        }
        if (featureId == "map_scan" && mode == "scan")
        {
            await RunWorldScanAsync();
            return;
        }
        if (featureId == "map_scan" && mode == "focus")
        {
            await FocusWorldRecordAsync(arguments);
            return;
        }
        if (!ReferenceFeatureCatalog.All.Any(item => string.Equals(item.Id, featureId, StringComparison.Ordinal)))
        {
            await PostBlockedCommandAsync(featureId, "Unknown recovered feature ID; no game command was written.");
            return;
        }
        if (featureId == "auto_reconnect")
        {
            await HandleAutoReconnectAsync(mode);
            return;
        }

        if (featureId is "auto_join_rally" or "auto_radar" or "daily_free_claims" or "troop_promotion"
            && string.Equals(mode, "run_once", StringComparison.Ordinal)
            && !FeatureEnabled(featureId))
        {
            await PostBlockedCommandAsync(featureId, $"{featureId} is disabled. Enable it before running once.");
            return;
        }

        if (featureId == "team_swap_outfit" && string.Equals(mode, "apply_scheme", StringComparison.Ordinal))
        {
            int slot = int.TryParse(arguments.GetValueOrDefault("scheme_slot"), out int parsed) ? parsed : 0;
            int? teamIndex = int.TryParse(arguments.GetValueOrDefault("team_index"), out int team) ? team : null;
            try
            {
                arguments["assignments_json"] = equipmentSchemeStore.SerializeAssignments(slot, teamIndex);
                arguments.TryAdd("timeout_seconds", "15");
            }
            catch (InvalidOperationException ex)
            {
                await PostBlockedCommandAsync(featureId, ex.Message);
                return;
            }
        }

        RecoveredFeatureCommandArguments.Apply(featureId, FeatureConfig(featureId), FeatureEnabled(featureId), arguments);
        if (featureId is "auto_attack" or "auto_rally" or "auto_join_rally")
            RecoveredFeatureCommandArguments.Apply("use_stamina_item", FeatureConfig("use_stamina_item"),
                FeatureEnabled("use_stamina_item"), arguments);
        await QueueRecoveredFeatureAsync(featureId, arguments);
    }

    private async Task RunWorldScanAsync()
    {
        string commandId = NewCommandId("map-scan");
        PostCommand(commandId, "map_scan", "Queued", "World Scan queued.");
        try
        {
            var run = await new CurrentWorldMapScanClient().RunAsync();
            worldRecords = run.Result.PointRecords.ToArray();
            PostCommand(commandId, "map_scan", "Succeeded", $"World Scan completed with {worldRecords.Length:N0} records.", true, worldRecords.Length);
            Post(new { type = "map_scan_data", records = worldRecords.Select(ToWebRecord).ToArray() });
        }
        catch (Exception ex)
        {
            PostCommand(commandId, "map_scan", "Failed", ex.Message, false);
        }
    }

    private async Task FocusWorldRecordAsync(Dictionary<string, string?> arguments)
    {
        if (!int.TryParse(arguments.GetValueOrDefault("point_id"), out int pointId))
        {
            await PostBlockedCommandAsync("map_scan", "Map focus requires a previously scanned point ID.");
            return;
        }
        CurrentWorldMapScanRecord? record = worldRecords.FirstOrDefault(item => item.PointId == pointId);
        if (record is null)
        {
            await PostBlockedCommandAsync("map_scan", "Map focus target is not present in the current verified scan results.");
            return;
        }
        string commandId = NewCommandId("map-focus");
        PostCommand(commandId, "map_scan", "Queued", $"Locating X{record.X} Y{record.Y}.");
        try
        {
            var result = await new CurrentWorldMapFocusClient().FocusAsync(record);
            PostCommand(commandId, "map_scan", "Succeeded", $"Located X{result.X} Y{result.Y} via {result.Route}.", true);
        }
        catch (Exception ex)
        {
            PostCommand(commandId, "map_scan", "Failed", ex.Message, false);
        }
    }

    private async Task RunDailyTaskOnlyAsync()
    {
        string commandId = NewCommandId("daily-task");
        try
        {
            DailyClaimSettings settings = File.Exists(settingsPath) ? JsonFiles.Read<DailyClaimSettings>(settingsPath) : new DailyClaimSettings();
            settings.Validate();
            if (!settings.Enabled || !settings.EnabledKinds.Contains(ClaimKind.DailyTaskChest))
                throw new InvalidOperationException("Daily Task-only execution requires Daily Claims and the Daily Task Chest category to be enabled in the existing policy file.");
            PostCommand(commandId, "daily_free_claims", "Queued", "PARTIAL: running the proven Daily Task-only path.");
            var client = new CurrentDailyTaskRuntimeClient();
            if (client.Inspect().StatusCode != "ready")
                throw new InvalidOperationException("Daily Task runtime is not ready.");
            var result = await client.RunOnceAsync(settings.MaximumClaimsPerRun);
            if (result.State != "completed") throw new InvalidOperationException(result.Message);
            PostCommand(commandId, "daily_free_claims", "Succeeded", $"Daily Task-only path confirmed {result.ConfirmedClaims} claims.", true, result.ConfirmedClaims);
        }
        catch (Exception ex)
        {
            PostCommand(commandId, "daily_free_claims", "Failed", ex.Message, false);
        }
    }

    private Task PostBlockedCommandAsync(string featureId, string message)
    {
        PostCommand(NewCommandId("blocked"), featureId, "Blocked", message, false);
        return Task.CompletedTask;
    }

    private async Task SendWorldIntelligenceAsync(JsonElement root)
    {
        string kind = StringProperty(root, "kind", "all");
        string search = StringProperty(root, "search");
        int page = IntProperty(root, "page", 1, 1, 100000);
        int pageSize = IntProperty(root, "pageSize", 100, 1, 500);
        IEnumerable<CurrentWorldMapScanRecord> query = worldRecords;
        if (kind != "all") query = query.Where(item => item.Kind == kind);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(item => WebSearchText(item).Contains(search, StringComparison.OrdinalIgnoreCase));
        CurrentWorldMapScanRecord[] filtered = query.ToArray();
        CurrentWorldMapScanRecord[] rows = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToArray();
        Post(new
        {
            type = "world_intelligence_data",
            payload = new
            {
                records = rows.Select(ToWebRecord).ToArray(),
                stats = new
                {
                    total = worldRecords.Length,
                    players = worldRecords.Count(item => item.Kind == "player_base"),
                    resources = worldRecords.Count(item => item.Kind == "resource_point"),
                    monsters = worldRecords.Count(item => item.Kind == "monster"),
                    allianceTargets = worldRecords.Count(item => item.Kind == "alliance_building"),
                },
                servers = worldRecords.Select(item => item.ServerId.ToString()).Distinct().Order().ToArray(),
                total = filtered.Length,
                page,
                pageSize,
                totalPages = Math.Max(1, (int)Math.Ceiling(filtered.Length / (double)pageSize)),
                queriedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            },
        });
        await Task.CompletedTask;
    }

    private static object ToWebRecord(CurrentWorldMapScanRecord item) => new
    {
        kind = item.Kind,
        pointId = item.PointId,
        pointType = item.PointType,
        targetUuid = item.Uuid,
        serverId = item.ServerId,
        x = item.X,
        y = item.Y,
        name = item.Name,
        playerName = item.PlayerName,
        alliance = item.Alliance,
        level = item.Level,
        power = item.Power,
        recommendedPower = item.RecommendedPower,
        resourceType = item.ResourceType,
        resourceRemaining = item.ResourceRemaining,
        shieldKnown = item.Shield?.Known ?? false,
        shieldActive = item.Shield?.Active ?? false,
        shieldRemainingSeconds = item.Shield?.RemainingSeconds ?? 0,
        source = item.Source,
    };

    private static string WebSearchText(CurrentWorldMapScanRecord item) => string.Join(' ', new[]
    {
        item.DisplayName, item.PlayerName, item.Alliance, item.ResourceType, item.MonsterId,
        item.PointId.ToString(), item.X.ToString(), item.Y.ToString(),
    }.Where(value => !string.IsNullOrWhiteSpace(value)));

    private async Task SaveFeatureConfigurationAsync(JsonElement root)
    {
        string featureId = StringProperty(root, "featureId");
        if (featureId is not ("alliance_train" or "hospital_heal" or "apply_position" or "auto_attack" or
            "use_stamina_item" or "troop_promotion" or "daily_free_claims" or "auto_join_rally" or "auto_radar"))
        {
            Post(new { type = "feature_config_result", featureId, ok = false, message = "feature_configuration_request_invalid" });
            return;
        }
        JsonObject config = root.TryGetProperty("config", out JsonElement node) && node.ValueKind == JsonValueKind.Object
            ? JsonNode.Parse(node.GetRawText())?.AsObject() ?? new JsonObject() : new JsonObject();
        if (featureId != "auto_attack") config["enabled"] = FeatureEnabled(featureId);
        hostState.FeatureConfigs[featureId] = config;
        SaveHostState();

        if (featureId == "use_stamina_item")
        {
            var arguments = new Dictionary<string, string?> { ["mode"] = "auto_configure" };
            RecoveredFeatureCommandArguments.Apply(featureId, config, FeatureEnabled(featureId), arguments);
            await QueueRecoveredFeatureAsync(featureId, arguments);
        }
        else if (featureId == "alliance_train" && FeatureEnabled(featureId))
        {
            var arguments = new Dictionary<string, string?> { ["mode"] = "start" };
            RecoveredFeatureCommandArguments.Apply(featureId, config, true, arguments);
            await QueueRecoveredFeatureAsync(featureId, arguments);
        }
        Post(new
        {
            type = "feature_config_result", featureId, ok = true,
            message = "feature_configuration_saved",
            payload = new { config, runtime = new { enabled = FeatureEnabled(featureId), state = FeatureEnabled(featureId) ? "Idle" : "Disabled" } },
        });
        PostAutomationUpdate(featureId);
    }

    private void ResetFeatureRuntime(JsonElement root)
    {
        string featureId = StringProperty(root, "featureId");
        JsonNode config = hostState.FeatureConfigs.TryGetPropertyValue(featureId, out JsonNode? node) && node is not null ? node : new JsonObject();
        Post(new
        {
            type = "feature_runtime_reset_result", featureId, ok = true, message = "Local UI runtime reset.",
            payload = new { config, runtime = new { enabled = false, state = "Idle" } },
        });
    }

    private void SaveMenuHotkey(JsonElement root)
    {
        string key = StringProperty(root, "key", "MouseRight");
        if (!string.Equals(key, "MouseRight", StringComparison.OrdinalIgnoreCase))
        {
            Post(new { type = "menu_hotkey_result", ok = false, key = hostState.MenuHotkey, registered = true, message = "unsupported_menu_hotkey" });
            return;
        }
        key = "MouseRight";
        hostState.MenuHotkey = key;
        SaveHostState();
        Post(new { type = "menu_hotkey_result", ok = true, key, registered = true, message = "menu_hotkey_saved" });
    }

    private void SaveGameplayHotkeys(JsonElement root)
    {
        hostState.GameplayHotkeys = JsonNode.Parse(root.GetRawText())?.AsObject() ?? new JsonObject();
        hostState.GameplayHotkeys.Remove("type");
        SaveHostState();
        Post(new { type = "gameplay_hotkeys_result", ok = true, payload = CurrentGameplayHotkeys(), message = "gameplay_hotkeys_saved" });
    }

    private Task SaveEquipmentSchemeAsync(JsonElement root)
    {
        try
        {
            int slot = root.TryGetProperty("schemeSlot", out JsonElement slotNode) && slotNode.TryGetInt32(out int parsed) ? parsed : 0;
            string name = StringProperty(root, "schemeName", $"Scheme {slot}");
            EquipmentSchemeTeam[] teams = root.TryGetProperty("teams", out JsonElement teamsNode) && teamsNode.ValueKind == JsonValueKind.Array
                ? JsonSerializer.Deserialize<EquipmentSchemeTeam[]>(teamsNode.GetRawText(), jsonOptions) ?? [] : [];
            EquipmentScheme scheme = equipmentSchemeStore.SaveDraft(slot, name, teams);
            PostEquipmentSchemes(true, $"Equipment scheme {scheme.Slot} saved with {scheme.Assignments.Count} assignments.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or JsonException)
        {
            Post(new { type = "equipment_scheme_update", ok = false, schemes = equipmentSchemeStore.Summaries, message = ex.Message });
        }
        return Task.CompletedTask;
    }

    private async Task ExportLogsAsync()
    {
        string directory = smokeOutput ?? Path.Combine(Path.GetDirectoryName(settingsPath)!, "logs");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"LWControl-UI-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        await File.WriteAllLinesAsync(path, sessionLog);
        Post(new { type = "log_export_result", ok = true, path, message = "UI host log exported." });
    }

    private void LoadPersistentUiState()
    {
        string appearancePath = settingsPath + ".appearance.json";
        try
        {
            if (File.Exists(appearancePath))
            {
                appearance = JsonFiles.Read<DesktopAppearance>(appearancePath);
                appearance.Validate();
            }
        }
        catch { appearance = new DesktopAppearance(); }
        try
        {
            if (File.Exists(webSettingsPath))
                hostState = JsonSerializer.Deserialize<WebUiHostState>(File.ReadAllText(webSettingsPath), jsonOptions) ?? new();
        }
        catch { hostState = new(); }
    }

    private void SaveHostState()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(webSettingsPath)!);
        string temp = webSettingsPath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(hostState, jsonOptions));
        File.Move(temp, webSettingsPath, true);
    }

    private JsonObject? FeatureConfig(string featureId)
    {
        return hostState.FeatureConfigs.TryGetPropertyValue(featureId, out JsonNode? node)
            ? node as JsonObject : null;
    }

    private bool FeatureEnabled(string featureId)
    {
        return hostState.FeatureEnabled.TryGetPropertyValue(featureId, out JsonNode? node)
            && node is JsonValue value
            && value.TryGetValue<bool>(out bool enabled)
            && enabled;
    }

    private GameplayHotkeyOptions CurrentGameplayHotkeys()
    {
        try
        {
            if (hostState.GameplayHotkeys.Count == 0) return new GameplayHotkeyOptions();
            return JsonSerializer.Deserialize<GameplayHotkeyOptions>(hostState.GameplayHotkeys.ToJsonString(), jsonOptions)
                ?? new GameplayHotkeyOptions();
        }
        catch (JsonException)
        {
            return new GameplayHotkeyOptions();
        }
    }

    private object BuildAutomationPayload(string featureId)
    {
        bool enabled = FeatureEnabled(featureId);
        nextAutomationAt.TryGetValue(featureId, out DateTimeOffset nextRunAt);
        return new
        {
            config = FeatureConfig(featureId) ?? new JsonObject(),
            runtime = new
            {
                enabled,
                state = enabled ? (recoveredPendingCommands.Values.Contains(featureId) ? "Executing" : "Idle") : "Disabled",
                nextRunAt = nextRunAt == default ? null : nextRunAt.ToString("O"),
            },
        };
    }

    private void PostAutomationUpdate(string featureId) =>
        Post(new { type = "feature_automation_update", featureId, payload = BuildAutomationPayload(featureId) });

    private void PostEquipmentSchemes(bool ok = true, string? message = null) =>
        Post(new { type = "equipment_scheme_update", ok, schemes = equipmentSchemeStore.Summaries, message });

    private async Task PumpContinuousAutomationsAsync()
    {
        if (smokeMode || recoveredPendingCommands.Count > 0) return;
        LocalBridgeInspection health = recoveredBridge.Inspect();
        if (!health.BridgeHealthy) return;

        DateTimeOffset now = DateTimeOffset.UtcNow;
        foreach (string featureId in new[] { "auto_radar", "hospital_heal", "apply_position" })
        {
            if (!FeatureEnabled(featureId)) continue;
            if (nextAutomationAt.TryGetValue(featureId, out DateTimeOffset next) && now < next) continue;

            JsonObject? config = FeatureConfig(featureId);
            int interval = IntConfig(config, "intervalSeconds", featureId == "auto_radar" ? 30 : featureId == "hospital_heal" ? 20 : 60, 1, 3600);
            var arguments = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["mode"] = featureId == "auto_radar" ? "run_once" : featureId == "hospital_heal" ? "heal" : "apply_one",
                ["_continuous_automation"] = "true",
            };
            RecoveredFeatureCommandArguments.Apply(featureId, config, true, arguments);
            nextAutomationAt[featureId] = now.AddSeconds(interval);
            PostAutomationUpdate(featureId);
            await QueueRecoveredFeatureAsync(featureId, arguments);
            break;
        }
    }

    private static int IntConfig(JsonObject? config, string property, int fallback, int min, int max)
    {
        if (config?[property] is JsonValue value && value.TryGetValue<int>(out int number))
            return Math.Clamp(number, min, max);
        return fallback;
    }

    private void OnHotkeyTick(object? sender, EventArgs e)
    {
        ObserveMenuToggleInput();
        GameplayHotkeyOptions options = CurrentGameplayHotkeys();
        bool eligible = CanAcceptGameplayHotkey();

        ObserveGameplayKey(Keys.Q, eligible && options.QuickAttackQ, () => QueueQuickAttackHotkeyAsync(1, "Q", false));
        ObserveGameplayKey(Keys.W, eligible && options.QuickAttackW, () => QueueQuickAttackHotkeyAsync(2, "W", false));
        ObserveGameplayKey(Keys.E, eligible && options.QuickAttackE, () => QueueQuickAttackHotkeyAsync(3, "E", false));
        ObserveGameplayKey(Keys.R, eligible && options.QuickAttackR, () => QueueQuickAttackHotkeyAsync(4, "R", false));
        ObserveGameplayKey(Keys.A, eligible && options.QuickRecallA, () => QueueQuickAttackHotkeyAsync(1, "A", true));
        ObserveGameplayKey(Keys.S, eligible && options.QuickRecallS, () => QueueQuickAttackHotkeyAsync(2, "S", true));
        ObserveGameplayKey(Keys.D, eligible && options.QuickRecallD, () => QueueQuickAttackHotkeyAsync(3, "D", true));
        ObserveGameplayKey(Keys.F, eligible && options.QuickRecallF, () => QueueQuickAttackHotkeyAsync(4, "F", true));

        bool alt = IsKeyDown(Keys.Menu);
        ObserveGameplayKey(Keys.D1, eligible && alt && options.EquipmentScheme1, () => QueueEquipmentHotkeyAsync(1));
        ObserveGameplayKey(Keys.D2, eligible && alt && options.EquipmentScheme2, () => QueueEquipmentHotkeyAsync(2));
        ObserveGameplayKey(Keys.D3, eligible && alt && options.EquipmentScheme3, () => QueueEquipmentHotkeyAsync(3));
        ObserveGameplayKey(Keys.D4, eligible && alt && options.EquipmentScheme4, () => QueueEquipmentHotkeyAsync(4));

        ObserveGameplayKey(Keys.F6, eligible && options.Shield8HoursF6, () => QueueShieldUseHotkeyAsync(28800, "F6"));
        ObserveGameplayKey(Keys.F7, eligible && options.Shield12HoursF7, () => QueueShieldUseHotkeyAsync(43200, "F7"));
        ObserveGameplayKey(Keys.F8, eligible && options.Shield24HoursF8, () => QueueShieldUseHotkeyAsync(86400, "F8"));
        ObserveGameplayKey(Keys.F9, eligible && options.RandomTeleportF9, () => QueueRecoveredFeatureAsync("random_teleport",
            new Dictionary<string, string?> { ["mode"] = "random_once", ["hotkey"] = "F9", ["timeout_seconds"] = "12" }));

        ObserveShieldCountdownHotkey(eligible && options.ShieldCountdownSpace);
    }

    private void ObserveMenuToggleInput()
    {
        bool down = IsKeyDown(Keys.RButton);
        if (!down)
        {
            menuToggleAwaitingRelease = false;
            return;
        }
        if (menuToggleAwaitingRelease) return;
        menuToggleAwaitingRelease = true;
        if (!CanToggleMenu()) return;
        long now = Environment.TickCount64;
        if (now - lastMenuToggleTick < 250) return;
        lastMenuToggleTick = now;
        menuEnabled = !menuEnabled;
        Visible = menuEnabled;
        if (menuEnabled) Activate();
    }

    private bool CanToggleMenu()
    {
        nint foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero) return false;
        GetWindowThreadProcessId(foreground, out uint processId);
        int? gameProcessId = FindGameProcessId();
        return processId == (uint)Environment.ProcessId || (gameProcessId.HasValue && processId == (uint)gameProcessId.Value);
    }

    private bool CanAcceptGameplayHotkey()
    {
        if (menuEnabled) return false;
        nint foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero) return false;
        GetWindowThreadProcessId(foreground, out uint processId);
        int? gameProcessId = FindGameProcessId();
        return gameProcessId.HasValue && processId == (uint)gameProcessId.Value;
    }

    private void ObserveGameplayKey(Keys key, bool enabled, Func<Task> action)
    {
        bool down = IsKeyDown(key);
        if (!enabled || !down)
        {
            gameplayHotkeysHeld.Remove(key);
            return;
        }
        if (gameplayHotkeysHeld.Add(key)) _ = RunHotkeyActionAsync(action);
    }

    private async Task RunHotkeyActionAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) { Log("Gameplay hotkey failed: " + ex.Message); }
    }

    private Task QueueQuickAttackHotkeyAsync(int team, string hotkey, bool recall)
    {
        var arguments = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["mode"] = recall ? "recall" : "execute",
            ["team"] = team.ToString(),
            ["hotkey"] = hotkey,
            ["timeout_seconds"] = "12",
        };
        if (!recall)
        {
            arguments["target_mode"] = "hover";
            arguments["target_kind"] = "auto";
        }
        return QueueRecoveredFeatureAsync("quick_attack", arguments);
    }

    private Task QueueEquipmentHotkeyAsync(int slot)
    {
        try
        {
            return QueueRecoveredFeatureAsync("team_swap_outfit", new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["mode"] = "apply_scheme",
                ["scheme_slot"] = slot.ToString(),
                ["assignments_json"] = equipmentSchemeStore.SerializeAssignments(slot),
                ["timeout_seconds"] = "15",
            });
        }
        catch (InvalidOperationException ex)
        {
            return PostBlockedCommandAsync("team_swap_outfit", ex.Message);
        }
    }

    private Task QueueShieldUseHotkeyAsync(int durationSeconds, string hotkey) =>
        QueueRecoveredFeatureAsync("shield_display", new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["mode"] = "use_once",
            ["duration_seconds"] = durationSeconds.ToString(),
            ["hotkey"] = hotkey,
        });

    private void ObserveShieldCountdownHotkey(bool eligible)
    {
        ShieldHotkeyAction action = shieldHotkeyState.Observe(eligible, eligible && IsKeyDown(Keys.Space), DateTimeOffset.UtcNow);
        if (action != ShieldHotkeyAction.None && !shieldUseHotkeyEnqueueing)
            _ = QueueShieldHotkeyActionAsync(action);
    }

    private async Task QueueShieldHotkeyActionAsync(ShieldHotkeyAction action)
    {
        shieldUseHotkeyEnqueueing = true;
        try
        {
            string hotkeyAction = action switch
            {
                ShieldHotkeyAction.HotkeyDown => "hotkey_down",
                ShieldHotkeyAction.Read => "read",
                ShieldHotkeyAction.HotkeyUp => "hotkey_up",
                _ => throw new ArgumentOutOfRangeException(nameof(action)),
            };
            LocalBridgeInspection health = recoveredBridge.Inspect();
            if (!health.BridgeHealthy)
            {
                shieldHotkeyState.Complete(action, false, DateTimeOffset.UtcNow);
                return;
            }
            string commandId = NewCommandId("lw-shield");
            var arguments = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["mode"] = "state",
                ["hotkey_action"] = hotkeyAction,
                ["hotkey"] = "Space",
                ["source"] = "host_edge",
            };
            RecoveredBridgeReceipt receipt = await recoveredBridge.EnqueueAsync(commandId, "shield_display", arguments, commandWatchCancellation.Token);
            if (!receipt.Accepted)
            {
                shieldHotkeyState.Complete(action, false, DateTimeOffset.UtcNow);
                return;
            }
            RecoveredBridgeResult? result = await recoveredBridge.WaitForResultAsync(commandId, TimeSpan.FromSeconds(15), commandWatchCancellation.Token);
            bool succeeded = result is not null && result.IsSucceeded;
            shieldHotkeyState.Complete(action, succeeded, DateTimeOffset.UtcNow);
            if (result is not null) PostRecoveredFeatureOutput(commandId, "shield_display", arguments, result);
        }
        catch (OperationCanceledException) when (commandWatchCancellation.IsCancellationRequested) { }
        catch (Exception ex)
        {
            shieldHotkeyState.Complete(action, false, DateTimeOffset.UtcNow);
            Log("Shield hotkey failed: " + ex.Message);
        }
        finally { shieldUseHotkeyEnqueueing = false; }
    }

    private static bool IsKeyDown(Keys key) => (GetAsyncKeyState((int)key) & 0x8000) != 0;

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint windowHandle, out uint processId);

    private void SaveAppearanceFromMessage(JsonElement root)
    {
        string language = StringProperty(root, "language", "zh");
        string theme = StringProperty(root, "theme", "theme-indigo");
        string accent = theme switch
        {
            "theme-cyan" => "Cyan", "theme-amber" => "Gold", "theme-rose" => "Rose",
            "theme-emerald" => "Emerald", _ => "Indigo",
        };
        appearance = appearance with
        {
            Language = language == "en" ? UiLanguage.English.ToString() : UiLanguage.SimplifiedChinese.ToString(),
            Accent = accent,
        };
        appearance.Validate();
        JsonFiles.Write(settingsPath + ".appearance.json", appearance);
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        commandWatchCancellation.Cancel();
        reconnectTimer.Stop();
        reconnectTimer.Dispose();
        try { SaveHostState(); } catch { }
    }

    private async Task InstallCapabilityLayerAsync()
    {
        var states = ReferenceFeatureCatalog.All.ToDictionary(
            item => item.Id,
            item => item.State.ToString().ToUpperInvariant(), StringComparer.Ordinal);
        string stateJson = JsonSerializer.Serialize(states, jsonOptions);
        string script = CapabilityScript.Replace("__CAPABILITY_JSON__", stateJson, StringComparison.Ordinal);
        await webView.CoreWebView2.ExecuteScriptAsync(script);
    }

    private const string CapabilityScript = """
        (() => {
          const states=__CAPABILITY_JSON__;
          if (globalThis.__rebuildCapabilityInstalled) { globalThis.__applyRebuildCapabilities?.(); return true; }
          globalThis.__rebuildCapabilityInstalled=true;
          const style=document.createElement('style');
          style.id='rebuild-capability-style';
          style.textContent=`
            .rebuild-availability-badge{display:inline-flex;align-items:center;border:1px solid rgba(var(--primary-rgb),.34);border-radius:999px;padding:2px 7px;margin-left:7px;font-size:8px;font-weight:800;letter-spacing:.06em!important;line-height:1.35;color:var(--theme-primary-300);background:rgba(var(--primary-rgb),.10);vertical-align:middle}
            .rebuild-availability-badge[data-state="PENDING"]{color:#94a3b8;border-color:#64748b55;background:#64748b12}
            .rebuild-availability-badge[data-state="PARTIAL"]{color:#fbbf24;border-color:#f59e0b55;background:#f59e0b12}
            .rebuild-availability-badge[data-state="RECOVERED"]{color:#67e8f9;border-color:#06b6d455;background:#06b6d412}
            [data-rebuild-disabled="true"]{opacity:.38!important;cursor:not-allowed!important;filter:saturate(.35)!important}
          `;
          document.head.appendChild(style);
          const label=(state,lang)=>lang==='zh'?({AVAILABLE:'已验证',RECOVERED:'已恢复',PARTIAL:'部分可用',PENDING:'待实现'}[state]||state):state;
          const disable=(el,reason)=>{el.disabled=true;el.setAttribute('aria-disabled','true');el.dataset.rebuildDisabled='true';el.title=reason;};
          const release=(el)=>{if(el.dataset.rebuildDisabled==='true'){el.disabled=false;el.removeAttribute('aria-disabled');delete el.dataset.rebuildDisabled;el.title='';}};
          const apply=()=>{
            const lang=localStorage.getItem('lwc-language')==='en'?'en':'zh';
            for(const el of document.querySelectorAll('[data-run]')){
              const id=el.dataset.run||'',mode=el.dataset.mode||'',state=states[id]||'PENDING';
              if(state==='PENDING') disable(el,lang==='zh'?'该执行路径尚未恢复':'Execution path is not recovered'); else release(el);
            }
            for(const card of document.querySelectorAll('[data-feature-card][data-feature-id]')){
              const id=card.dataset.featureId,state=states[id]||'PENDING';
              let badge=card.querySelector(':scope .rebuild-availability-badge');
              if(!badge){badge=document.createElement('span');badge.className='rebuild-availability-badge';(card.querySelector('.feature-card-title-line h3')||card.querySelector('h3'))?.after(badge);}
              badge.dataset.state=state; const badgeText=label(state,lang); if(badge.textContent!==badgeText) badge.textContent=badgeText;
              for(const el of card.querySelectorAll('[data-run],[data-feature-main-action],button.action-primary,button.action-danger')){
                if(state==='PENDING') disable(el,lang==='zh'?'该执行路径尚未恢复':'Execution path is not recovered'); else release(el);
              }
              for(const el of card.querySelectorAll('[data-feature-automation-toggle] input,[data-feature-automation-toggle] button')){
                if(state==='PENDING') disable(el,lang==='zh'?'自动执行尚未恢复':'Automation is not recovered'); else release(el);
              }
            }
            const mapHeader=document.querySelector('[data-map-scan-panel] .data-content-header h2');
            if(mapHeader){let b=mapHeader.parentElement?.querySelector('.rebuild-availability-badge');if(!b){b=document.createElement('span');b.className='rebuild-availability-badge';b.dataset.state='AVAILABLE';mapHeader.after(b);}const text=label('AVAILABLE',lang);if(b.textContent!==text)b.textContent=text;}
            for(const el of document.querySelectorAll('.scan-actions button')) release(el);
            for(const el of document.querySelectorAll('[data-equipment-apply],[data-equipment-save-apply],[data-equipment-apply-team],[data-squad-quick-toggle]')) release(el);
          };
          globalThis.__applyRebuildCapabilities=apply;
          apply();
          new MutationObserver(()=>queueMicrotask(apply)).observe(document.getElementById('root'),{subtree:true,childList:true});
          let lastLang=localStorage.getItem('lwc-language')||'zh',lastTheme=localStorage.getItem('lwc-theme')||'theme-indigo';
          setInterval(()=>{const l=localStorage.getItem('lwc-language')||'zh',t=localStorage.getItem('lwc-theme')||'theme-indigo';if(l!==lastLang||t!==lastTheme){lastLang=l;lastTheme=t;window.chrome?.webview?.postMessage({type:'rebuild_appearance_changed',language:l,theme:t});apply();}},250);
          return true;
        })()
        """;

    private async Task RunVisualSmokeAsync()
    {
        if (smokeOutput is null) return;
        Directory.CreateDirectory(smokeOutput);
        var inventory = new List<object>();
        foreach ((string language, string langCode) in new[] { ("zh", "zh"), ("en", "en") })
        {
            await SetLanguageAsync(langCode);
            foreach (string tab in new[] { "home", "auto", "data", "ops", "doc", "set" })
            {
                await ClickAsync($"[data-tab='{tab}']");
                await Task.Delay(180);
                string name = $"{language}-{tab}-1320x840.png";
                await CaptureAsync(name, new Size(1320, 840));
                inventory.Add(new { language, tab, size = "1320x840", file = name });
            }

            await ClickAsync("[data-tab='auto']");
            string groupJsonForLanguage = await webView.CoreWebView2.ExecuteScriptAsync("JSON.stringify([...new Set([...document.querySelectorAll('[data-feature-group]')].map(x=>x.dataset.featureGroup))])");
            foreach (string group in ParseScriptStringArray(groupJsonForLanguage))
            {
                await ClickAsync($"[data-feature-group='{group}']");
                await Task.Delay(90);
                await CaptureAsync($"{language}-auto-{SafeFileName(group)}-1320x840.png", new Size(1320, 840));
            }

            await ClickAsync("[data-tab='ops']");
            for (int tabIndex = 0; tabIndex < 3; tabIndex++)
            {
                await webView.CoreWebView2.ExecuteScriptAsync($"[...document.querySelectorAll('.ops-workspace-tabs button')][{tabIndex}]?.click();true;");
                await Task.Delay(90);
                await CaptureAsync($"{language}-ops-subtab-{tabIndex + 1}-1320x840.png", new Size(1320, 840));
            }

            await ClickAsync("[data-tab='data']");
            string dataSourceJson = await webView.CoreWebView2.ExecuteScriptAsync("JSON.stringify([...document.querySelectorAll('[data-data-source]')].map(x=>x.dataset.dataSource))");
            foreach (string source in ParseScriptStringArray(dataSourceJson).Distinct(StringComparer.Ordinal))
            {
                await ClickAsync($"[data-data-source='{source}']");
                await Task.Delay(80);
                await CaptureAsync($"{language}-data-source-{SafeFileName(source)}-1320x840.png", new Size(1320, 840));
            }
        }

        await SetLanguageAsync("en");
        await ClickAsync("[data-tab='ops']");
        foreach ((string selector, string name) in new[]
        {
            ("button", "equipment"),
            ("button:nth-of-type(2)", "overview"),
            ("button:nth-of-type(3)", "afk"),
        })
        {
            await webView.CoreWebView2.ExecuteScriptAsync($"[...document.querySelectorAll('.ops-workspace-tabs button')].find((x,i)=>i==={(name == "equipment" ? 0 : name == "overview" ? 1 : 2)})?.click();true;");
            await Task.Delay(150);
            await CaptureAsync($"en-ops-{name}-1320x840.png", new Size(1320, 840));
        }

        await ClickAsync("[data-tab='auto']");
        string groupJson = await webView.CoreWebView2.ExecuteScriptAsync("JSON.stringify([...new Set([...document.querySelectorAll('[data-feature-group]')].map(x=>x.dataset.featureGroup))])");
        string[] groups = ParseScriptStringArray(groupJson);
        foreach (string group in groups)
        {
            await ClickAsync($"[data-feature-group='{group}']");
            await Task.Delay(130);
            await CaptureAsync($"en-auto-{SafeFileName(group)}-1320x840.png", new Size(1320, 840));
        }

        foreach (ReferenceFeature feature in ReferenceFeatureCatalog.All)
        {
            await webView.CoreWebView2.ExecuteScriptAsync($"window.dispatchEvent(new CustomEvent('lw:navigate-feature',{{detail:{JsonSerializer.Serialize(feature.Id)}}}));true;");
            await Task.Delay(90);
            await webView.CoreWebView2.ExecuteScriptAsync($"document.querySelector('[data-feature-id={JsonSerializer.Serialize(feature.Id)}]')?.scrollIntoView({{block:'center'}});true;");
            await Task.Delay(60);
            await CaptureAsync($"en-feature-{feature.Id}-1320x840.png", new Size(1320, 840));
        }

        foreach ((string feature, string selector) in new[]
        {
            ("auto_radar", "[data-auto-radar-settings-open]"),
            ("auto_join_rally", "[data-rally-settings-open]"),
            ("auto_attack", "[data-auto-attack-afk-settings] summary"),
        })
        {
            await webView.CoreWebView2.ExecuteScriptAsync($"window.dispatchEvent(new CustomEvent('lw:navigate-feature',{{detail:{JsonSerializer.Serialize(feature)}}}));true;");
            await Task.Delay(160);
            if (await SelectorExistsAsync(selector))
            {
                await ClickAsync(selector);
                await Task.Delay(120);
                await CaptureAsync($"en-config-{feature}-1320x840.png", new Size(1320, 840));
            }
        }

        await ClickAsync("[data-tab='home']");
        await ClickAsync("[data-evidence-toggle]");
        await Task.Delay(120);
        await CaptureAsync("en-evidence-drawer-1320x840.png", new Size(1320, 840));

        await ClickAsync("[data-tab='data']");
        Post(new { type = "__map_loading" });
        await Task.Delay(120);
        await CaptureAsync("en-data-loading-1320x840.png", new Size(1320, 840));
        Post(new { type = "world_intelligence_store_error", message = "Fixture-only intelligence store error" });
        await Task.Delay(120);
        await CaptureAsync("en-data-error-1320x840.png", new Size(1320, 840));
        await InjectWorldFixtureAsync();
        await Task.Delay(160);
        await CaptureAsync("en-data-populated-1320x840.png", new Size(1320, 840));
        await CaptureAsync("en-data-populated-1050x700.png", new Size(1050, 700));
        await CaptureAsync("en-data-below-1040-breakpoint-1039x700.png", new Size(1039, 700));
        await CaptureAsync("en-data-above-1040-breakpoint-1041x700.png", new Size(1041, 700));
        await CaptureAsync("en-data-below-1120-breakpoint-1119x700.png", new Size(1119, 700));
        await CaptureAsync("en-data-above-1120-breakpoint-1121x700.png", new Size(1121, 700));
        await CaptureAsync("en-data-below-1280-breakpoint-1279x840.png", new Size(1279, 840));
        await CaptureAsync("en-data-above-1280-breakpoint-1281x840.png", new Size(1281, 840));
        await CaptureAsync("en-data-below-800-breakpoint-799x700.png", new Size(799, 700));
        await CaptureAsync("en-data-above-800-breakpoint-801x700.png", new Size(801, 700));
        await CaptureAsync("en-data-below-760-breakpoint-759x700.png", new Size(759, 700));
        await CaptureAsync("en-data-above-760-breakpoint-761x700.png", new Size(761, 700));
        await CaptureAsync("en-data-below-620-breakpoint-619x700.png", new Size(619, 700));
        await CaptureAsync("en-data-above-620-breakpoint-621x700.png", new Size(621, 700));
        await CaptureAsync("en-data-below-660-height-1320x659.png", new Size(1320, 659));
        await CaptureAsync("en-data-above-660-height-1320x661.png", new Size(1320, 661));
        await CaptureAsync("en-data-below-650-height-1320x649.png", new Size(1320, 649));
        await CaptureAsync("en-data-above-650-height-1320x651.png", new Size(1320, 651));

        await ClickAsync("[data-tab='set']");
        foreach (string language in new[] { "zh", "en" })
        {
            await SetLanguageAsync(language);
            foreach (string theme in new[] { "theme-cyan", "theme-amber", "theme-indigo", "theme-rose", "theme-emerald" })
            {
                await webView.CoreWebView2.ExecuteScriptAsync($"localStorage.setItem('lwc-theme',{JsonSerializer.Serialize(theme)});document.documentElement.className={JsonSerializer.Serialize(theme)};true;");
                if (!referenceOnly) await webView.CoreWebView2.ExecuteScriptAsync("globalThis.__applyRebuildCapabilities?.();true;");
                await Task.Delay(90);
                await CaptureAsync($"{language}-settings-{theme}-1320x840.png", new Size(1320, 840));
            }
        }

        await SetLanguageAsync("en");
        await ClickAsync("[data-tab='auto']");
        await ClickAsync("[data-feature-group='daily']");
        await Task.Delay(120);
        int availabilityCount = referenceOnly ? 0 : await EvalIntAsync("document.querySelectorAll('.rebuild-availability-badge').length");
        int recoveredExecutionEnabled = referenceOnly ? 0 : await EvalIntAsync("document.querySelectorAll('[data-run]:not([disabled])').length");
        int pendingExecutionEnabled = referenceOnly ? 0 : await EvalIntAsync("[...document.querySelectorAll('[data-run]:not([disabled])')].filter(x=>x.dataset.rebuildDisabled==='true').length");

        var contract = new
        {
            exactReferenceHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(ReadRecoveredUiBytes())).ToLowerInvariant(),
            shell = await EvalBoolAsync("Boolean(document.querySelector('.overlay-shell'))"),
            tabCount = await EvalIntAsync("document.querySelectorAll('[data-tab]').length"),
            featureCount = ReferenceFeatureCatalog.All.Count,
            availabilityCount,
            recoveredExecutionEnabled = referenceOnly ? (int?)null : recoveredExecutionEnabled,
            pendingExecutionEnabled = referenceOnly ? (int?)null : pendingExecutionEnabled,
            captureCount = Directory.EnumerateFiles(smokeOutput, "*.png").Count(),
            referenceOnly,
            responsiveBreakpoints = new[] { 1280, 1120, 1040, 800, 760, 620 },
        };
        File.WriteAllText(Path.Combine(smokeOutput, "webview-contract.json"), JsonSerializer.Serialize(contract, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(Path.Combine(smokeOutput, "capture-inventory.json"), JsonSerializer.Serialize(inventory, new JsonSerializerOptions { WriteIndented = true }));
    }

    private async Task InjectWorldFixtureAsync()
    {
        Post(new
        {
            type = "world_intelligence_data",
            payload = new
            {
                records = new object[]
                {
                    new { kind="player_base", playerName="Commander Nova", alliance="VLT", level=31, power=12845000, x=541, y=232, pointId=101, serverId=1546, shieldKnown=true, shieldActive=false, source="visual_fixture" },
                    new { kind="player_base", playerName="Astra", alliance="NEX", level=28, power=7820000, x=588, y=219, pointId=102, serverId=1546, shieldKnown=true, shieldActive=true, shieldRemainingSeconds=7200, source="visual_fixture" },
                    new { kind="resource_point", resourceType="gold", level=6, resourceRemaining=920000, x=612, y=244, pointId=103, serverId=1546, source="visual_fixture" },
                    new { kind="monster", name="Elite Zombie", level=42, recommendedPower=9600000, x=633, y=271, pointId=104, serverId=1546, source="visual_fixture" },
                    new { kind="alliance_building", name="Alliance Outpost", alliance="VLT", level=5, x=498, y=205, pointId=105, serverId=1546, source="visual_fixture" },
                },
                stats = new { total=5, players=2, resources=1, monsters=1, allianceTargets=1 },
                servers = new[] { "1546" }, total=5, page=1, pageSize=100, totalPages=1, queriedAt=DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            },
        });
        await Task.CompletedTask;
    }

    private async Task SetLanguageAsync(string language)
    {
        string script = $"(() => {{const s=document.querySelector('[data-topbar-language-select]');if(!s)return false;s.value={JsonSerializer.Serialize(language)};s.dispatchEvent(new Event('change',{{bubbles:true}}));return true;}})()";
        await webView.CoreWebView2.ExecuteScriptAsync(script);
        await Task.Delay(120);
    }

    private async Task ClickAsync(string selector)
    {
        string script = $"(() => {{const x=document.querySelector({JsonSerializer.Serialize(selector)});if(!x)return false;x.click();return true;}})()";
        await webView.CoreWebView2.ExecuteScriptAsync(script);
    }

    private async Task<bool> SelectorExistsAsync(string selector) =>
        await EvalBoolAsync($"Boolean(document.querySelector({JsonSerializer.Serialize(selector)}))");

    private async Task CaptureAsync(string fileName, Size clientSize)
    {
        ClientSize = clientSize;
        await Task.Delay(180);
        string path = Path.Combine(smokeOutput!, fileName);
        await using FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.Read, 65536, FileOptions.Asynchronous);
        await webView.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, stream);
        await stream.FlushAsync();
    }

    private async Task<bool> EvalBoolAsync(string script) =>
        string.Equals(await webView.CoreWebView2.ExecuteScriptAsync(script), "true", StringComparison.OrdinalIgnoreCase);

    private async Task<int> EvalIntAsync(string script)
    {
        string raw = await webView.CoreWebView2.ExecuteScriptAsync(script);
        return int.TryParse(raw, out int value) ? value : 0;
    }

    private static string[] ParseScriptStringArray(string value)
    {
        string json = JsonSerializer.Deserialize<string>(value) ?? "[]";
        return JsonSerializer.Deserialize<string[]>(json) ?? [];
    }

    private static string SafeFileName(string value) => new(value.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '-').ToArray());

    private object CreateLayoutPayload() => new
    {
        width = ClientSize.Width,
        height = ClientSize.Height,
        dpiScale = DeviceDpi / 96d,
        scale = DeviceDpi / 96d,
    };

    private async Task PostLayoutAsync()
    {
        if (!initialized || webView.CoreWebView2 is null || bootstrapSent == 0) return;
        Post(new { type = "host_layout", layout = CreateLayoutPayload() });
        await Task.CompletedTask;
    }

    private void Post(object message)
    {
        if (IsDisposed || Disposing || webView.CoreWebView2 is null) return;
        webView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(message, jsonOptions));
    }

    private void PostCommand(string commandId, string featureId, string status, string message, bool? contractComplete = null, int total = 0) =>
        Post(new { type = "command_update", commandId, featureId, status, message, messageEn = message, total, contractComplete });

    private static string NewCommandId(string prefix) => $"{prefix}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";

    private static string CapabilityName(string featureId) => ReferenceFeatureCatalog.All.FirstOrDefault(item => item.Id == featureId)?.State switch
    {
        FeatureImplementationState.Available => "AVAILABLE",
        FeatureImplementationState.Recovered => "RECOVERED",
        FeatureImplementationState.Partial => "PARTIAL",
        _ => "PENDING",
    };

    private void Log(string message)
    {
        string line = $"{DateTimeOffset.Now:O} {message}";
        sessionLog.Add(line);
        Debug.WriteLine(line);
    }

    private static string StringProperty(JsonElement root, string name, string fallback = "") =>
        root.TryGetProperty(name, out JsonElement node) && node.ValueKind == JsonValueKind.String ? node.GetString() ?? fallback : fallback;

    private static int IntProperty(JsonElement root, string name, int fallback, int min, int max) =>
        root.TryGetProperty(name, out JsonElement node) && node.TryGetInt32(out int value) ? Math.Clamp(value, min, max) : fallback;

    private static Dictionary<string, string?> ReadArguments(JsonElement root)
    {
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (!root.TryGetProperty("arguments", out JsonElement args) || args.ValueKind != JsonValueKind.Object) return result;
        foreach (JsonProperty property in args.EnumerateObject())
            result[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString(),
                JsonValueKind.Number => property.Value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => null,
                _ => property.Value.GetRawText(),
            };
        return result;
    }

    private void BeginWindowDrag()
    {
        if (smokeMode) return;
        ReleaseCapture();
        SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
    }

    private void BeginWindowResize(JsonElement root)
    {
        if (smokeMode) return;
        string edge = StringProperty(root, "edge", "bottom-right");
        int hit = edge == "bottom-right" ? 17 : 17;
        ReleaseCapture();
        SendMessage(Handle, 0xA1, (IntPtr)hit, IntPtr.Zero);
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
}

internal sealed class WebUiHostState
{
    public string MenuHotkey { get; set; } = "MouseRight";
    public JsonObject GameplayHotkeys { get; set; } = new();
    public JsonObject FeatureConfigs { get; set; } = new();
    public JsonObject FeatureEnabled { get; set; } = new();
    public JsonArray EquipmentSchemes { get; set; } = [];
    public bool AutoReconnectEnabled { get; set; }
}
