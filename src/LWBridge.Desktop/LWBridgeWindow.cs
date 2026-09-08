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
    private readonly string? hostProbePath;
    private readonly string initialView;
    private readonly string? language;
    private readonly string? theme;
    private readonly LWBridgeBackend backend;
    private readonly MapDataStore mapData;
    private readonly HostProbeCommandService? hostProbeService;
    private readonly string? isolatedConfigRoot;
    private long documentGeneration = 1;
    private DocumentSession documentSession = new(1, EventAllowlist);
    private bool documentReady;
    private bool sessionClosed;
    private int rejectedNavigationCount;
    private int lastClosedSubscriptionCount;
    private int lastClosedRequestCount;
    private readonly WebView2 webView = new()
    {
        Dock = DockStyle.Fill,
        DefaultBackgroundColor = Color.FromArgb(245, 245, 247),
    };

    public LWBridgeWindow(string? capturePath, string? liveProbePath, string? hostProbePath, string initialView, string? language, string? theme)
    {
        this.capturePath = capturePath;
        this.liveProbePath = liveProbePath;
        this.hostProbePath = hostProbePath;
        string[] views = ["overview", "automation", "map-data", "march", "city-layout", "hotkeys", "mini-games", "advanced", "settings"];
        if (!views.Contains(initialView)) throw new ArgumentException("Unknown --view: " + initialView);
        this.initialView = initialView;
        this.language = language;
        this.theme = theme;
        bool isolated = capturePath is not null || liveProbePath is not null || hostProbePath is not null;
        LocalConfigStore config;
        if (hostProbePath is not null)
        {
            isolatedConfigRoot = Path.Combine(Path.GetTempPath(), "lwbridge-host-probe-" + Guid.NewGuid().ToString("N"));
            config = new LocalConfigStore(isolatedConfigRoot);
        }
        else
        {
            config = new LocalConfigStore(persistent: !isolated);
        }
        mapData = isolated
            ? MapDataStore.CreateInMemory()
            : new MapDataStore(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LWBridgeRebuild", "profiles", config.Snapshot.ProfileId, "map-data.db"));
        hostProbeService = hostProbePath is null ? null : new HostProbeCommandService();
        backend = new LWBridgeBackend(config, asyncCommands: hostProbeService, mapData: mapData);
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
                "LWBridgeRebuild",
                capturePath is not null ? "Capture" :
                liveProbePath is not null ? "LiveProbe" :
                hostProbePath is not null ? "HostProbe" : "Presentation");
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataDirectory);
            await webView.EnsureCoreWebView2Async(environment);
            var core = webView.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            string bootstrapJson = JsonSerializer.Serialize(
                backend.GetBootstrap(
                    capturePath is not null,
                    documentSession.Id,
                    suppressAutoLaunch: liveProbePath is not null || hostProbePath is not null),
                JsonOptions.Default);
            await core.AddScriptToExecuteOnDocumentCreatedAsync(
                "window.__LWBridgeBootstrap=" + bootstrapJson + ";" +
                "(()=>{try{const s=new URL(location.href).searchParams.get('nativeSession');if(s)window.__LWBridgeBootstrap.sessionId=s;}catch{}})();");
            core.SetVirtualHostNameToFolderMapping("lwbridge.local", Path.Combine(AppContext.BaseDirectory, "WebUi"), CoreWebView2HostResourceAccessKind.DenyCors);
            core.NavigationStarting += OnNavigationStarting;
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
                if (args.IsSuccess)
                {
                    documentReady = true;
                    ready.TrySetResult();
                }
                else ready.TrySetException(new InvalidOperationException($"UI navigation failed: {args.WebErrorStatus}"));
            };
            string url = $"{UiOrigin}/index.html?view={Uri.EscapeDataString(initialView)}";
            if (language is not null) url += "&language=" + Uri.EscapeDataString(language);
            if (theme is not null) url += "&theme=" + Uri.EscapeDataString(theme);
            core.Navigate(WithDocumentSession(url, documentSession.Id));
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(15));
            if (hostProbePath is not null)
            {
                await RunHostProbeAsync(core, hostProbePath);
                Close();
                return;
            }
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
            if (capturePath is null && liveProbePath is null && hostProbePath is null)
                MessageBox.Show(this, ex.Message, "LWBridge", MessageBoxButtons.OK, MessageBoxIcon.Error);
            else
            {
                string artifactPath = capturePath ?? liveProbePath ?? hostProbePath!;
                Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);
                await File.WriteAllTextAsync(artifactPath + ".error.txt", ex.ToString());
            }
            Environment.ExitCode = 1;
            Close();
        }
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs args)
    {
        if (sessionClosed)
        {
            args.Cancel = true;
            return;
        }
        if (!args.Uri.StartsWith(UiOrigin + "/", StringComparison.Ordinal))
        {
            rejectedNavigationCount++;
            args.Cancel = true;
            return;
        }
        if (!documentReady) return;

        args.Cancel = true;
        RotateDocumentSession();
        string target = WithDocumentSession(args.Uri, documentSession.Id);
        BeginInvoke(new Action(() =>
        {
            if (!sessionClosed && webView.CoreWebView2 is not null)
                webView.CoreWebView2.Navigate(target);
        }));
    }

    private void RotateDocumentSession()
    {
        DocumentSession previous = documentSession;
        lastClosedSubscriptionCount = previous.Subscriptions.Count;
        lastClosedRequestCount = previous.Requests.ActiveCount;
        previous.Close();
        documentGeneration++;
        documentSession = new DocumentSession(documentGeneration, EventAllowlist);
        documentReady = false;
    }

    private static string WithDocumentSession(string url, string sessionId)
    {
        var builder = new UriBuilder(url);
        string[] existing = builder.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(part => !part.StartsWith("nativeSession=", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        string sessionPart = "nativeSession=" + Uri.EscapeDataString(sessionId);
        builder.Query = existing.Length == 0
            ? sessionPart
            : string.Join('&', existing) + "&" + sessionPart;
        return builder.Uri.AbsoluteUri;
    }

    private async Task RunHostProbeAsync(CoreWebView2 core, string outputPath)
    {
        if (hostProbeService is null || isolatedConfigRoot is null)
            throw new InvalidOperationException("Host probe service is unavailable.");

        string firstPhase = """
            window.__LWBridgeHostProbe = { pending: true, phase: 'first' };
            (async () => {
              try {
                const profileId = window.LWBridgePreview.profiles.selectedProfileId;
                const startedAt = performance.now();
                const slowStorage = window.LWBridgePreview.invoke('local_config_set', { autoLaunchGame: false });
                const timerDelayMs = await new Promise(resolve => setTimeout(() => resolve(performance.now() - startedAt), 50));
                await slowStorage;
                const slowStorageElapsedMs = performance.now() - startedAt;
                window.__LWBridgeHostProbeUnlisten = window.LWBridgePreview.listen('bridge://feedback-export-progress', () => {});
                window.__LWBridgeHostProbeDelayed = window.LWBridgePreview.invoke('diagnostic_host_delayed', { profileId });
                window.__LWBridgeHostProbeDelayed.catch(() => {});
                window.__LWBridgeHostProbe = {
                  pending: false,
                  phase: 'first',
                  oldSessionId: window.__LWBridgeBootstrap.sessionId,
                  timerDelayMs,
                  slowStorageElapsedMs
                };
              } catch (error) {
                window.__LWBridgeHostProbe = {
                  pending: false,
                  phase: 'first',
                  errorCode: error?.code || '',
                  error: String(error?.message || error)
                };
              }
            })();
            """;
        string configLockPath = Path.Combine(isolatedConfigRoot, "config.lock");
        using FileStream configLock = await AcquireExclusiveFileAsync(configLockPath, TimeSpan.FromSeconds(2));
        Task releaseConfigLock = Task.Run(async () =>
        {
            await Task.Delay(800).ConfigureAwait(false);
            configLock.Dispose();
        });
        await core.ExecuteScriptAsync(firstPhase);
        JsonElement first = await ReadHostProbeResultAsync(core, "first");
        await releaseConfigLock;
        string oldSessionId = first.GetProperty("oldSessionId").GetString()
            ?? throw new InvalidOperationException("Host probe did not expose the first document session ID.");
        double timerDelayMs = first.GetProperty("timerDelayMs").GetDouble();
        double slowStorageElapsedMs = first.GetProperty("slowStorageElapsedMs").GetDouble();

        for (int attempt = 0; attempt < 100 && hostProbeService.DelayedActive == 0; attempt++)
            await Task.Delay(20);
        if (hostProbeService.DelayedActive != 1)
            throw new InvalidOperationException("Host probe delayed request did not become active before reload.");

        Task reloaded = WaitForNextSuccessfulNavigationAsync(core);
        core.Reload();
        await reloaded.WaitAsync(TimeSpan.FromSeconds(15));

        for (int attempt = 0; attempt < 100 && hostProbeService.DelayedActive != 0; attempt++)
            await Task.Delay(20);
        if (hostProbeService.DelayedActive != 0 || hostProbeService.DelayedCancelled < 1)
            throw new InvalidOperationException("Reload did not cancel and drain the prior document request.");

        string oldSessionJson = JsonSerializer.Serialize(oldSessionId);
        string secondPhase = $$"""
            window.__LWBridgeHostProbe = { pending: true, phase: 'second' };
            (async () => {
              try {
                const profileId = window.LWBridgePreview.profiles.selectedProfileId;
                const before = await window.LWBridgePreview.invoke('diagnostic_host_state', { profileId });
                const config = await window.LWBridgePreview.invoke('local_config_get');
                let expectedErrorCode = '';
                try {
                  await window.LWBridgePreview.invoke('diagnostic_host_error', { profileId });
                } catch (error) {
                  expectedErrorCode = error?.code || '';
                }
                const staleRequestId = 'stale-' + Date.now();
                window.chrome.webview.postMessage({
                  kind: 'invoke',
                  sessionId: {{oldSessionJson}},
                  id: staleRequestId,
                  command: 'diagnostic_host_slow_sync',
                  payload: { profileId }
                });
                await new Promise(resolve => setTimeout(resolve, 250));
                const after = await window.LWBridgePreview.invoke('diagnostic_host_state', { profileId });
                window.__LWBridgeHostProbe = {
                  pending: false,
                  phase: 'second',
                  newSessionId: window.__LWBridgeBootstrap.sessionId,
                  bootstrapAutoLaunch: window.__LWBridgeBootstrap.autoLaunchGame,
                  before,
                  after,
                  config,
                  expectedErrorCode
                };
              } catch (error) {
                window.__LWBridgeHostProbe = {
                  pending: false,
                  phase: 'second',
                  errorCode: error?.code || '',
                  error: String(error?.message || error)
                };
              }
            })();
            """;
        await core.ExecuteScriptAsync(secondPhase);
        JsonElement second = await ReadHostProbeResultAsync(core, "second");
        string newSessionId = second.GetProperty("newSessionId").GetString()
            ?? throw new InvalidOperationException("Host probe did not expose the reloaded document session ID.");
        int slowBeforeStale = second.GetProperty("before").GetProperty("slowSyncStarted").GetInt32();
        int slowAfterStale = second.GetProperty("after").GetProperty("slowSyncStarted").GetInt32();
        string expectedErrorCode = second.GetProperty("expectedErrorCode").GetString() ?? string.Empty;
        bool bootstrapAutoLaunch = second.GetProperty("bootstrapAutoLaunch").ValueKind == JsonValueKind.True;

        string localSourceBeforeExternal = core.Source;
        string sessionBeforeExternal = documentSession.Id;
        int rejectedBefore = rejectedNavigationCount;
        core.Navigate("https://example.invalid/blocked-by-host-probe");
        for (int attempt = 0; attempt < 100 && rejectedNavigationCount == rejectedBefore; attempt++)
            await Task.Delay(20);
        bool externalNavigationRejected = rejectedNavigationCount > rejectedBefore &&
            core.Source.StartsWith(UiOrigin + "/", StringComparison.Ordinal) &&
            documentSession.Id == sessionBeforeExternal;

        bool slowStorageUiResponsive = timerDelayMs < 400 && slowStorageElapsedMs >= 700;
        bool sessionRotated = !string.Equals(oldSessionId, newSessionId, StringComparison.Ordinal) &&
            documentGeneration >= 2;
        bool reloadCancelledOldWork = hostProbeService.DelayedCancelled >= 1 &&
            hostProbeService.DelayedActive == 0 && lastClosedRequestCount >= 1;
        bool reloadResetSubscriptions = lastClosedSubscriptionCount >= 1;
        bool staleSessionIgnored = slowBeforeStale == 0 && slowAfterStale == 0;
        bool structuredError = expectedErrorCode == "DIAGNOSTIC_EXPECTED";
        bool startupAutoLaunchSuppressed = !bootstrapAutoLaunch;
        bool ok = slowStorageUiResponsive && sessionRotated && reloadCancelledOldWork && reloadResetSubscriptions &&
            staleSessionIgnored && structuredError && externalNavigationRejected && startupAutoLaunchSuppressed;

        var result = new
        {
            ok,
            mode = "isolated-native-host-probe",
            implementationPolicy = true,
            slowStorageUiResponsive,
            timerDelayMs,
            slowStorageElapsedMs,
            sessionRotated,
            oldSessionId,
            newSessionId,
            documentGeneration,
            reloadCancelledOldWork,
            reloadResetSubscriptions,
            lastClosedRequestCount,
            lastClosedSubscriptionCount,
            staleSessionIgnored,
            slowBeforeStale,
            slowAfterStale,
            structuredError,
            expectedErrorCode,
            externalNavigationRejected,
            localSourceBeforeExternal,
            sourceAfterExternal = core.Source,
            rejectedNavigationCount,
            service = hostProbeService.Snapshot(),
            startupAutoLaunchSuppressed,
            isolatedPersistentConfig = true,
            userConfigTouched = false,
            liveGameCommandsPerformed = false,
        };
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(result, JsonOptions.Indented));
        if (!ok)
            throw new InvalidOperationException("Isolated native host probe failed: " + JsonSerializer.Serialize(result, JsonOptions.Default));
    }

    private static async Task<FileStream> AcquireExclusiveFileAsync(string path, TimeSpan timeout)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        while (true)
        {
            try
            {
                return new FileStream(
                    path,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    FileOptions.None);
            }
            catch (IOException) when (stopwatch.Elapsed < timeout)
            {
                await Task.Delay(25);
            }
        }
    }

    private static async Task<JsonElement> ReadHostProbeResultAsync(CoreWebView2 core, string phase)
    {
        string? result = null;
        for (int attempt = 0; attempt < 150; attempt++)
        {
            try
            {
                string value = await core.ExecuteScriptAsync("JSON.stringify(window.__LWBridgeHostProbe || null)");
                result = JsonSerializer.Deserialize<string>(value);
                if (result is not null)
                {
                    using JsonDocument parsed = JsonDocument.Parse(result);
                    JsonElement root = parsed.RootElement;
                    if (root.TryGetProperty("phase", out JsonElement resultPhase) && resultPhase.GetString() == phase &&
                        root.TryGetProperty("pending", out JsonElement pending) && pending.ValueKind == JsonValueKind.False)
                    {
                        if (root.TryGetProperty("error", out JsonElement error))
                            throw new InvalidOperationException($"Host probe {phase} phase failed: {error.GetString()}");
                        return root.Clone();
                    }
                }
            }
            catch (InvalidOperationException) when (attempt < 149)
            {
                // A real reload can temporarily make ExecuteScriptAsync unavailable.
            }
            await Task.Delay(50);
        }
        throw new TimeoutException($"Host probe {phase} phase did not complete. Last result: {result}");
    }

    private static Task WaitForNextSuccessfulNavigationAsync(CoreWebView2 core)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Handler(object? sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            if (!args.IsSuccess) return;
            core.NavigationCompleted -= Handler;
            completion.TrySetResult();
        }
        core.NavigationCompleted += Handler;
        return completion.Task;
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

        DocumentSession session = documentSession;

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
                !string.Equals(messageSession, session.Id, StringComparison.Ordinal))
                return;
            if (!TryGetString(root, "kind", out string? kind)) return;

            switch (kind)
            {
                case "listen":
                    if (TryGetString(root, "event", out string? eventName) && eventName is not null)
                        session.Subscriptions.Listen(eventName);
                    return;
                case "unlisten":
                    if (TryGetString(root, "event", out string? removeEvent) && removeEvent is not null)
                        session.Subscriptions.Unlisten(removeEvent);
                    return;
                case "cancel":
                    if (TryGetString(root, "id", out string? cancelId) && cancelId is not null)
                        session.Requests.Cancel(cancelId);
                    return;
                case "invoke":
                    break;
                default:
                    return;
            }

            if (!TryGetString(root, "id", out string? id) || string.IsNullOrWhiteSpace(id)) return;
            if (!TryGetString(root, "command", out string? command) || string.IsNullOrWhiteSpace(command))
            {
                SendError(session, id, "INVALID_COMMAND", "command is required.");
                return;
            }

            JsonElement payload = root.TryGetProperty("payload", out JsonElement supplied)
                ? supplied.Clone()
                : EmptyObject();
            try
            {
                NativeRequestExecution execution = await session.Requests.ExecuteAsync(id, cancellationToken =>
                    command == "game_root_select"
                        ? SelectGameRootAsync(cancellationToken)
                        : Task.Run(() => backend.InvokeAsync(command, payload, cancellationToken), cancellationToken));
                if (!IsCurrentDocument(session)) return;
                if (execution.Status == NativeRequestExecutionStatus.Rejected)
                {
                    if (!sessionClosed)
                        SendError(session, id, "DUPLICATE_REQUEST_ID", "A request with this id is already active or the native session is closing.");
                    return;
                }
                if (execution.Status == NativeRequestExecutionStatus.Cancelled)
                {
                    SendError(session, id, "COMMAND_CANCELLED", "The command was cancelled.");
                    return;
                }
                if (sessionClosed) return;
                SendResult(session, id, execution.Result);
                if (command == "map_player_mark_set")
                    SendEvent(session, "bridge://player-mark-changed", execution.Result);
                if (command is "game_root_select" or "set_automation" or "local_config_set")
                    await EmitOverviewStateAsync(session);
            }
            catch (BridgeCommandException ex)
            {
                if (IsCurrentDocument(session))
                    SendError(session, id, ex.Code, ex.Message, ex.Details);
            }
            catch (Exception ex)
            {
                if (IsCurrentDocument(session))
                    SendError(session, id, "NATIVE_COMMAND_FAILED", ex.Message);
            }
        }
    }

    private async Task<object?> SelectGameRootAsync(CancellationToken cancellationToken)
    {
        GameRootStatus current = await Task.Run(backend.GetGameRootStatus, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select the Last War installation directory",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
        };
        if (!string.IsNullOrWhiteSpace(current.Path) && Directory.Exists(current.Path))
            dialog.InitialDirectory = current.Path;
        if (dialog.ShowDialog(this) != DialogResult.OK) return null;
        string selectedPath = dialog.SelectedPath;
        GameRootStatus selected = await Task.Run(() => backend.SaveGameRoot(selectedPath), cancellationToken);
        if (!selected.Valid)
            throw new BridgeCommandException(
                selected.Error ?? "GAME_ROOT_INVALID",
                "The selected directory is not a valid supported Last War installation.",
                selected);
        return selected;
    }

    private async Task EmitOverviewStateAsync(DocumentSession session)
    {
        if (!IsCurrentDocument(session)) return;
        if (session.Subscriptions.Contains("bridge://status"))
        {
            using JsonDocument scoped = JsonDocument.Parse(JsonSerializer.Serialize(new { profileId = backend.ProfileId }, JsonOptions.Default));
            object? status = await Task.Run(() => backend.InvokeAsync("get_status", scoped.RootElement.Clone(), CancellationToken.None));
            if (!IsCurrentDocument(session)) return;
            SendEvent(session, "bridge://status", status);
        }
        if (session.Subscriptions.Contains("bridge://game-recovery"))
            SendEvent(session, "bridge://game-recovery", new { state = "idle", error = (string?)null });
    }

    private void SendResult(DocumentSession session, string id, object? result) => SendMessage(session, new
    {
        kind = "response",
        sessionId = session.Id,
        id,
        ok = true,
        result,
    });

    private void SendError(DocumentSession session, string id, string code, string message, object? details = null) => SendMessage(session, new
    {
        kind = "response",
        sessionId = session.Id,
        id,
        ok = false,
        error = new { code, message, details },
    });

    private void SendEvent(DocumentSession session, string eventName, object? payload)
    {
        object eventPayload = ProfileScopedEvents.Contains(eventName)
            ? new { profileId = backend.ProfileId, payload }
            : payload ?? new { };
        SendMessage(session, new
        {
            kind = "event",
            sessionId = session.Id,
            @event = eventName,
            payload = eventPayload,
        });
    }

    private void SendMessage(DocumentSession session, object message)
    {
        if (!IsCurrentDocument(session)) return;
        if (webView.CoreWebView2 is null) return;
        webView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(message, JsonOptions.Default));
    }

    private bool IsCurrentDocument(DocumentSession session) =>
        !sessionClosed && ReferenceEquals(documentSession, session) && !session.IsClosed;

    private void OnFormClosed(object? sender, FormClosedEventArgs e)
    {
        sessionClosed = true;
        documentSession.Close();
        mapData.Dispose();
        if (isolatedConfigRoot is not null)
        {
            try { Directory.Delete(isolatedConfigRoot, recursive: true); }
            catch { }
        }
        if (webView.CoreWebView2 is not null)
        {
            webView.CoreWebView2.NavigationStarting -= OnNavigationStarting;
            webView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
        }
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

    private sealed class DocumentSession
    {
        private bool closed;

        public DocumentSession(long generation, IEnumerable<string> eventAllowlist)
        {
            Generation = generation;
            Id = Guid.NewGuid().ToString("N");
            Subscriptions = new NativeSubscriptionRegistry(eventAllowlist);
            Requests = new NativeRequestExecutor();
        }

        public string Id { get; }
        public long Generation { get; }
        public NativeSubscriptionRegistry Subscriptions { get; }
        public NativeRequestExecutor Requests { get; }
        public bool IsClosed => closed;

        public void Close()
        {
            if (closed) return;
            closed = true;
            Subscriptions.Close();
            Requests.Close();
        }
    }
}
