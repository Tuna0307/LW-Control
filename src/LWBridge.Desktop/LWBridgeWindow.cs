using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace LWBridge.Desktop;

internal sealed class LWBridgeWindow : Form
{
    private const string UiOrigin = "https://lwbridge.local";
    private static readonly HashSet<string> EventAllowlist =
    [
        "bridge://status",
        "bridge://map-scan-status",
        "bridge://automation-status",
        "bridge://resource-automation-status",
        "bridge://game-recovery",
        "bridge://update-status",
        "bridge://feedback-export-progress",
        "bridge://player-mark-changed",
    ];
    private static readonly HashSet<string> ProfileScopedEvents = new(StringComparer.Ordinal)
    {
        "bridge://status",
        "bridge://map-scan-status",
        "bridge://automation-status",
        "bridge://resource-automation-status",
        "bridge://game-recovery",
        "bridge://player-mark-changed",
    };

    private readonly string? capturePath;
    private readonly string? liveProbePath;
    private readonly string initialView;
    private readonly string? language;
    private readonly string? theme;
    private readonly string sessionId = Guid.NewGuid().ToString("N");
    private readonly LWBridgeBackend backend;
    private readonly MapDataStore mapData;
    private readonly NativeSubscriptionRegistry subscriptions = new(EventAllowlist);
    private readonly NativeRequestExecutor activeRequests = new();
    private bool sessionClosed;
    private readonly WebView2 webView = new()
    {
        Dock = DockStyle.Fill,
        DefaultBackgroundColor = Color.FromArgb(245, 245, 247),
    };

    public LWBridgeWindow(string? capturePath, string? liveProbePath, string initialView, string? language, string? theme)
    {
        this.capturePath = capturePath;
        this.liveProbePath = liveProbePath;
        string[] views = ["overview", "automation", "map-data", "march", "city-layout", "hotkeys", "mini-games", "advanced", "settings"];
        if (!views.Contains(initialView)) throw new ArgumentException("Unknown --view: " + initialView);
        this.initialView = initialView;
        this.language = language;
        this.theme = theme;
        bool isolated = capturePath is not null || liveProbePath is not null;
        var config = new LocalConfigStore(persistent: !isolated);
        mapData = isolated
            ? MapDataStore.CreateInMemory()
            : new MapDataStore(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LWBridgeRebuild", "profiles", config.Snapshot.ProfileId, "map-data.db"));
        backend = new LWBridgeBackend(config, mapData: mapData);
        Text = "lwbridge";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1120, 720);
        MinimumSize = new Size(900, 640);
        BackColor = Color.FromArgb(245, 245, 247);
        Controls.Add(webView);
        Shown += OnShown;
        FormClosed += OnFormClosed;
    }

    private async void OnShown(object? sender, EventArgs e)
    {
        try
        {
            string userDataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LWBridgeRebuild", capturePath is not null ? "Capture" : liveProbePath is not null ? "LiveProbe" : "Presentation");
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataDirectory);
            await webView.EnsureCoreWebView2Async(environment);
            var core = webView.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            string bootstrapJson = JsonSerializer.Serialize(
                backend.GetBootstrap(capturePath is not null, sessionId, suppressAutoLaunch: liveProbePath is not null),
                JsonOptions.Default);
            await core.AddScriptToExecuteOnDocumentCreatedAsync(
                "window.__LWBridgeBootstrap=" + bootstrapJson + ";");
            core.SetVirtualHostNameToFolderMapping("lwbridge.local", Path.Combine(AppContext.BaseDirectory, "WebUi"), CoreWebView2HostResourceAccessKind.DenyCors);
            core.NavigationStarting += (_, args) => args.Cancel = !args.Uri.StartsWith(UiOrigin + "/", StringComparison.Ordinal);
            core.NewWindowRequested += (_, args) => args.Handled = true;
            core.WebMessageReceived += OnWebMessageReceived;
            core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            core.WebResourceRequested += (_, args) =>
            {
                if (!args.Request.Uri.StartsWith(UiOrigin + "/", StringComparison.Ordinal))
                    args.Response = environment.CreateWebResourceResponse(null, 403, "Local UI only", "");
            };
            if (capturePath is not null)
                await core.AddScriptToExecuteOnDocumentCreatedAsync("localStorage.clear();");
            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            core.NavigationCompleted += (_, args) =>
            {
                if (args.IsSuccess) ready.TrySetResult();
                else ready.TrySetException(new InvalidOperationException($"UI navigation failed: {args.WebErrorStatus}"));
            };
            string url = $"{UiOrigin}/index.html?view={Uri.EscapeDataString(initialView)}";
            if (language is not null) url += "&language=" + Uri.EscapeDataString(language);
            if (theme is not null) url += "&theme=" + Uri.EscapeDataString(theme);
            core.Navigate(url);
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(15));
            if (liveProbePath is not null)
            {
                await RunLiveReadOnlyProbeAsync(core, liveProbePath);
                Close();
                return;
            }
            if (capturePath is not null)
            {
                bool rendered = false;
                for (int attempt = 0; attempt < 120; attempt++)
                {
                    if (await core.ExecuteScriptAsync("!!document.querySelector('.main-view .panel') && !document.querySelector('.profile-switch-loading')") == "true")
                    { rendered = true; break; }
                    await Task.Delay(100);
                }
                if (!rendered) throw new InvalidOperationException("The recovered feature page did not render.");
                await Task.Delay(600);
                string diagnostics = await core.ExecuteScriptAsync("JSON.stringify({view:window.LWBridgePreview.view,errors:window.LWBridgePreview.failures,commands:window.LWBridgePreview.calls,text:document.body.innerText})");
                Directory.CreateDirectory(Path.GetDirectoryName(capturePath)!);
                await File.WriteAllTextAsync(Path.ChangeExtension(capturePath, ".json"), JsonSerializer.Deserialize<string>(diagnostics));
                await using (var output = File.Create(capturePath))
                    await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, output);
                Close();
            }
        }
        catch (Exception ex)
        {
            if (capturePath is null && liveProbePath is null)
                MessageBox.Show(this, ex.Message, "LWBridge", MessageBoxButtons.OK, MessageBoxIcon.Error);
            else
            {
                string artifactPath = capturePath ?? liveProbePath!;
                Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);
                await File.WriteAllTextAsync(artifactPath + ".error.txt", ex.ToString());
            }
            Environment.ExitCode = 1;
            Close();
        }
    }

    private static async Task RunLiveReadOnlyProbeAsync(CoreWebView2 core, string outputPath)
    {
        string script = """
            window.__LWBridgeLiveProbe = { pending: true };
            (async () => {
              try {
                const [root, proxy, status, recovery] = await Promise.all([
                  window.LWBridgePreview.invoke('game_root_status'),
                  window.LWBridgePreview.invoke('proxy_status', { profileId: window.LWBridgePreview.profiles.selectedProfileId }),
                  window.LWBridgePreview.invoke('get_status', { profileId: window.LWBridgePreview.profiles.selectedProfileId }),
                  window.LWBridgePreview.invoke('game_recovery_status', { profileId: window.LWBridgePreview.profiles.selectedProfileId })
                ]);
                window.__LWBridgeLiveProbe = {
                  pending: false,
                  ok: true,
                  mode: window.LWBridgePreview.mode,
                  profileId: window.LWBridgePreview.profiles.selectedProfileId,
                  root, proxy, status, recovery,
                  calls: window.LWBridgePreview.calls,
                  failures: window.LWBridgePreview.failures
                };
              } catch (error) {
                window.__LWBridgeLiveProbe = {
                  pending: false,
                  ok: false,
                  code: error?.code || '',
                  message: String(error?.message || error),
                  calls: window.LWBridgePreview.calls,
                  failures: window.LWBridgePreview.failures
                };
              }
            })();
            """;
        await core.ExecuteScriptAsync(script);
        string? result = null;
        for (int attempt = 0; attempt < 100; attempt++)
        {
            string value = await core.ExecuteScriptAsync("JSON.stringify(window.__LWBridgeLiveProbe || null)");
            result = JsonSerializer.Deserialize<string>(value);
            if (result is not null)
            {
                using JsonDocument parsed = JsonDocument.Parse(result);
                if (parsed.RootElement.TryGetProperty("pending", out JsonElement pending) && pending.ValueKind == JsonValueKind.False)
                    break;
            }
            await Task.Delay(100);
        }
        if (result is null) throw new InvalidOperationException("Live read-only probe did not return a result.");
        using (JsonDocument parsed = JsonDocument.Parse(result))
        {
            if (!parsed.RootElement.TryGetProperty("pending", out JsonElement pending) || pending.ValueKind != JsonValueKind.False)
                throw new TimeoutException("Live read-only probe timed out.");
            if (!parsed.RootElement.TryGetProperty("ok", out JsonElement ok) || ok.ValueKind != JsonValueKind.True)
                throw new InvalidOperationException("Live read-only probe failed: " + result);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        using JsonDocument pretty = JsonDocument.Parse(result);
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(pretty.RootElement, JsonOptions.Indented));
    }

    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        if (sessionClosed) return;
        if (capturePath is not null) return;
        if (!args.Source.StartsWith(UiOrigin + "/", StringComparison.OrdinalIgnoreCase)) return;

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(args.WebMessageAsJson);
        }
        catch (JsonException)
        {
            return;
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return;
            if (!TryGetString(root, "sessionId", out string? messageSession) ||
                !string.Equals(messageSession, sessionId, StringComparison.Ordinal))
                return;
            if (!TryGetString(root, "kind", out string? kind)) return;

            switch (kind)
            {
                case "listen":
                    if (TryGetString(root, "event", out string? eventName) && eventName is not null)
                        subscriptions.Listen(eventName);
                    return;
                case "unlisten":
                    if (TryGetString(root, "event", out string? removeEvent) && removeEvent is not null)
                        subscriptions.Unlisten(removeEvent);
                    return;
                case "cancel":
                    if (TryGetString(root, "id", out string? cancelId) && cancelId is not null)
                        activeRequests.Cancel(cancelId);
                    return;
                case "invoke":
                    break;
                default:
                    return;
            }

            if (!TryGetString(root, "id", out string? id) || string.IsNullOrWhiteSpace(id)) return;
            if (!TryGetString(root, "command", out string? command) || string.IsNullOrWhiteSpace(command))
            {
                SendError(id, "INVALID_COMMAND", "command is required.");
                return;
            }

            JsonElement payload = root.TryGetProperty("payload", out JsonElement supplied)
                ? supplied.Clone()
                : EmptyObject();
            try
            {
                NativeRequestExecution execution = await activeRequests.ExecuteAsync(id, cancellationToken =>
                    command == "game_root_select"
                        ? Task.FromResult(SelectGameRoot())
                        : backend.InvokeAsync(command, payload, cancellationToken));
                if (execution.Status == NativeRequestExecutionStatus.Rejected)
                {
                    if (!sessionClosed)
                        SendError(id, "DUPLICATE_REQUEST_ID", "A request with this id is already active or the native session is closing.");
                    return;
                }
                if (execution.Status == NativeRequestExecutionStatus.Cancelled)
                {
                    SendError(id, "COMMAND_CANCELLED", "The command was cancelled.");
                    return;
                }
                if (sessionClosed) return;
                SendResult(id, execution.Result);
                if (command == "map_player_mark_set")
                    SendEvent("bridge://player-mark-changed", execution.Result);
                if (command is "game_root_select" or "set_automation" or "local_config_set")
                    EmitOverviewState();
            }
            catch (BridgeCommandException ex)
            {
                SendError(id, ex.Code, ex.Message, ex.Details);
            }
            catch (Exception ex)
            {
                SendError(id, "NATIVE_COMMAND_FAILED", ex.Message);
            }
        }
    }

    private object? SelectGameRoot()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select the Last War installation directory",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
        };
        GameRootStatus current = backend.GetGameRootStatus();
        if (!string.IsNullOrWhiteSpace(current.Path) && Directory.Exists(current.Path))
            dialog.InitialDirectory = current.Path;
        if (dialog.ShowDialog(this) != DialogResult.OK) return null;
        GameRootStatus selected = backend.SaveGameRoot(dialog.SelectedPath);
        if (!selected.Valid)
            throw new BridgeCommandException(
                selected.Error ?? "GAME_ROOT_INVALID",
                "The selected directory is not a valid supported Last War installation.",
                selected);
        return selected;
    }

    private void EmitOverviewState()
    {
        if (subscriptions.Contains("bridge://status"))
        {
            using JsonDocument scoped = JsonDocument.Parse(JsonSerializer.Serialize(new { profileId = backend.ProfileId }, JsonOptions.Default));
            object? status = backend.InvokeAsync("get_status", scoped.RootElement.Clone(), CancellationToken.None)
                .GetAwaiter().GetResult();
            SendEvent("bridge://status", status);
        }
        if (subscriptions.Contains("bridge://game-recovery"))
            SendEvent("bridge://game-recovery", new { state = "idle", error = (string?)null });
    }

    private void SendResult(string id, object? result) => SendMessage(new
    {
        kind = "response",
        sessionId,
        id,
        ok = true,
        result,
    });

    private void SendError(string id, string code, string message, object? details = null) => SendMessage(new
    {
        kind = "response",
        sessionId,
        id,
        ok = false,
        error = new { code, message, details },
    });

    private void SendEvent(string eventName, object? payload)
    {
        object eventPayload = ProfileScopedEvents.Contains(eventName)
            ? new { profileId = backend.ProfileId, payload }
            : payload ?? new { };
        SendMessage(new
        {
            kind = "event",
            sessionId,
            @event = eventName,
            payload = eventPayload,
        });
    }

    private void SendMessage(object message)
    {
        if (sessionClosed) return;
        if (webView.CoreWebView2 is null) return;
        webView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(message, JsonOptions.Default));
    }

    private void OnFormClosed(object? sender, FormClosedEventArgs e)
    {
        sessionClosed = true;
        subscriptions.Close();
        activeRequests.Close();
        mapData.Dispose();
        if (webView.CoreWebView2 is not null)
            webView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
    }

    private static bool TryGetString(JsonElement element, string name, out string? value)
    {
        value = null;
        if (!element.TryGetProperty(name, out JsonElement property) || property.ValueKind != JsonValueKind.String)
            return false;
        value = property.GetString();
        return value is not null;
    }

    private static JsonElement EmptyObject()
    {
        using JsonDocument document = JsonDocument.Parse("{}");
        return document.RootElement.Clone();
    }
}
