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
    private readonly FirstLiveResultImport? firstLiveResult;
    private readonly string? normalUiLiveResourceProofPath;
    private readonly string initialView;
    private readonly string? language;
    private readonly string? theme;
    private readonly LWBridgeBackend backend;
    private readonly MapDataStore mapData;
    private readonly HostProbeCommandService? hostProbeService;
    private readonly LiveResourceProbeCommandService? liveResourceService;
    private readonly string? isolatedConfigRoot;
    private long documentGeneration = 1;
    private DocumentSession documentSession = new(1, EventAllowlist);
    private bool documentReady;
    private bool sessionClosed;
    private int rejectedNavigationCount;
    private int lastClosedSubscriptionCount;
    private int lastClosedRequestCount;
    private int postedWebMessageCount;
    private readonly object normalUiProofGate = new();
    private long normalUiProofSearchSequence;
    private NormalUiResourceProofSearchObservation? normalUiProofSearchObservation;
    private readonly WebView2 webView = new()
    {
        Dock = DockStyle.Fill,
        DefaultBackgroundColor = Color.FromArgb(245, 245, 247),
    };

    public event EventHandler? HostProbeFinished;

    public LWBridgeWindow(
        string? capturePath,
        string? liveProbePath,
        string? hostProbePath,
        string initialView,
        string? language,
        string? theme,
        string? firstLiveResultPath = null,
        string? normalUiLiveResourceProofPath = null)
    {
        this.capturePath = capturePath;
        this.liveProbePath = liveProbePath;
        this.hostProbePath = hostProbePath;
        this.normalUiLiveResourceProofPath = normalUiLiveResourceProofPath;
        string[] views = ["overview", "automation", "map-data", "march", "city-layout", "hotkeys", "mini-games", "advanced", "settings"];
        if (!views.Contains(initialView)) throw new ArgumentException("Unknown --view: " + initialView);
        this.initialView = initialView;
        this.language = language;
        this.theme = theme;
        bool isolated = capturePath is not null || liveProbePath is not null || hostProbePath is not null || firstLiveResultPath is not null;
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
        if (firstLiveResultPath is not null)
        {
            FirstLiveReplay replay = FirstLiveResultImporter.CreateIsolatedReplay(firstLiveResultPath);
            mapData = replay.Store;
            firstLiveResult = replay.Import;
        }
        else
        {
            mapData = isolated
                ? MapDataStore.CreateInMemory()
                : new MapDataStore(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "LWBridgeRebuild", "profiles", config.Snapshot.ProfileId, "map-data.db"));
        }
        hostProbeService = hostProbePath is null ? null : new HostProbeCommandService();
        if (!isolated)
        {
            GameRootStatus liveGameRoot = new GameInstallationService(config).GetStatus();
            liveResourceService = new LiveResourceProbeCommandService(
                mapData,
                gameRoot: liveGameRoot.Valid ? liveGameRoot.Path : null,
                profileId: config.Snapshot.ProfileId);
        }
        else
        {
            liveResourceService = null;
        }
        backend = new LWBridgeBackend(
            config,
            asyncCommands: hostProbeService ?? (INativeAsyncCommandService?)liveResourceService,
            mapData: mapData,
            firstLiveResultServerId: firstLiveResult?.ServerId);
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
                hostProbePath is not null ? "HostProbe" :
                firstLiveResult is not null ? "FirstLiveResult" : "Presentation");
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataDirectory);
            await webView.EnsureCoreWebView2Async(environment);
            var core = webView.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            string bootstrapJson = JsonSerializer.Serialize(
                backend.GetBootstrap(
                    capturePath is not null && firstLiveResult is null,
                    documentSession.Id,
                    suppressAutoLaunch: liveProbePath is not null || hostProbePath is not null || firstLiveResult is not null),
                JsonOptions.Default);
            await core.AddScriptToExecuteOnDocumentCreatedAsync(
                "window.__LWBridgeBootstrap=" + bootstrapJson + ";" +
                "(()=>{try{const s=new URL(location.href).searchParams.get('nativeSession');if(s)window.__LWBridgeBootstrap.sessionId=s;}catch{}})();");
            if (firstLiveResult is not null)
            {
                string capturedAt = JsonSerializer.Serialize(
                    DateTimeOffset.FromUnixTimeMilliseconds(firstLiveResult.CapturedAtUnixMilliseconds)
                        .UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"));
                string replaySource = JsonSerializer.Serialize(Path.GetFileName(firstLiveResult.SourcePath));
                string captureSha256 = JsonSerializer.Serialize(firstLiveResult.CaptureSha256);
                string probeVersion = JsonSerializer.Serialize(firstLiveResult.ProbeVersion ?? "unknown");
                await core.AddScriptToExecuteOnDocumentCreatedAsync($$"""
                    (() => {
                      const capturedAt = {{capturedAt}};
                      const replaySource = {{replaySource}};
                      const captureSha256 = {{captureSha256}};
                      const probeVersion = {{probeVersion}};
                      const applyReplayMode = () => {
                        const panel = document.querySelector('.panel.map-panel');
                        if (!panel) return;
                        let banner = panel.querySelector('.first-live-replay-banner');
                        if (!banner) {
                          banner = document.createElement('div');
                          banner.className = 'first-live-replay-banner';
                          banner.setAttribute('role', 'status');
                          banner.style.cssText = 'margin:0 0 12px;padding:10px 12px;border:1px solid currentColor;border-radius:8px;line-height:1.45;';
                          const title = document.createElement('strong');
                          title.textContent = 'Saved capture replay';
                          const detail = document.createElement('div');
                          detail.textContent = `Captured ${capturedAt} · ${replaySource} · probe ${probeVersion} · SHA-256 ${captureSha256}`;
                          const warning = document.createElement('div');
                          warning.textContent = 'This view replays saved data. Scan controls are disabled and do not reacquire the game.';
                          banner.append(title, detail, warning);
                          panel.prepend(banner);
                        }
                        for (const control of panel.querySelectorAll(
                          '.map-scan-tabs button, .map-header .map-actions button, .map-header .map-actions input, .map-header .map-actions select, .map-auto-scan-card button, .map-auto-scan-card input, .map-auto-scan-card select, .panel.map-panel > .map-controls input')) {
                          control.disabled = true;
                          control.setAttribute('aria-disabled', 'true');
                          control.title = 'Disabled in saved capture replay mode';
                        }
                      };
                      new MutationObserver(applyReplayMode).observe(document, {
                        childList: true,
                        subtree: true,
                        characterData: true
                      });
                      document.addEventListener('DOMContentLoaded', applyReplayMode);
                    })();
                    """);
            }
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
            if (normalUiLiveResourceProofPath is not null)
            {
                if (!string.Equals(initialView, "map-data", StringComparison.Ordinal))
                    throw new InvalidOperationException("--normal-ui-live-resource-proof requires --view map-data.");
                await RunNormalUiLiveResourceProofAsync(core, normalUiLiveResourceProofPath);
                Close();
                return;
            }
            if (firstLiveResult is not null && string.Equals(initialView, "map-data", StringComparison.Ordinal))
                await SelectFirstLiveResourceAsync(core);
            if (hostProbePath is not null)
            {
                await RunHostProbeAsync(core, hostProbePath);
                if (!IsDisposed) Close();
                HostProbeFinished?.Invoke(this, EventArgs.Empty);
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
            if (capturePath is null && liveProbePath is null && hostProbePath is null && normalUiLiveResourceProofPath is null)
                MessageBox.Show(this, ex.Message, "LWBridge", MessageBoxButtons.OK, MessageBoxIcon.Error);
            else
            {
                string artifactPath = capturePath ?? liveProbePath ?? hostProbePath ?? normalUiLiveResourceProofPath!;
                Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);
                await File.WriteAllTextAsync(artifactPath + ".error.txt", ex.ToString());
            }
            Environment.ExitCode = 1;
            if (!IsDisposed) Close();
            if (hostProbePath is not null)
                HostProbeFinished?.Invoke(this, EventArgs.Empty);
        }
    }

    // IMPLEMENTATION POLICY LWB-PM12-008: diagnostic-only orchestration that drives
    // the recovered manual Map Data controls and records correlated rendered proof.
    // It does not change map scan semantics or make this bounded route a full scan.
    private async Task RunNormalUiLiveResourceProofAsync(CoreWebView2 core, string outputPath)
    {
        if (liveResourceService is null)
            throw new InvalidOperationException("The normal Map Data window does not have the bounded live resource service.");

        string fullOutputPath = Path.GetFullPath(outputPath);
        string directory = Path.GetDirectoryName(fullOutputPath)!;
        Directory.CreateDirectory(directory);

        bool controlsReady = false;
        for (int attempt = 0; attempt < 150; attempt++)
        {
            if (await core.ExecuteScriptAsync(
                "document.querySelectorAll('.panel.map-panel > .map-controls .map-types input[type=checkbox]').length === 8 && !!document.querySelector('.map-header .map-actions button.primary')") == "true")
            {
                controlsReady = true;
                break;
            }
            await Task.Delay(100);
        }
        if (!controlsReady)
            throw new InvalidOperationException("The normal Map Data manual scan controls did not render.");

        string selectionResult = await core.ExecuteScriptAsync("""
            (() => {
              const boxes = [...document.querySelectorAll('.panel.map-panel > .map-controls .map-types input[type=checkbox]')];
              if (boxes.length !== 8) return false;
              boxes.forEach((box, index) => {
                const shouldBeChecked = index === 1;
                if (box.checked !== shouldBeChecked) box.click();
              });
              return boxes.every((box, index) => box.checked === (index === 1));
            })()
            """);
        if (selectionResult != "true")
            throw new InvalidOperationException("The normal Map Data resource-only scan selection could not be established.");

        JsonElement initialStatus = JsonSerializer.SerializeToElement(liveResourceService.CreateStatus(), JsonOptions.Default);
        string initialRunId = ReadStatusString(initialStatus, "scanRunId") ?? "";
        long initialCapturedAt = ReadStatusInt64(initialStatus, "liveResourceCapturedAt") ?? 0;

        UiLiveAcquisitionProof first = await RunUiLiveAcquisitionAsync(core, initialRunId, initialCapturedAt, "first");
        UiLiveAcquisitionProof second = await RunUiLiveAcquisitionAsync(core, first.ScanRunId, first.CapturedAtUnixMilliseconds, "second");

        if (string.Equals(first.ScanRunId, second.ScanRunId, StringComparison.Ordinal) ||
            second.CapturedAtUnixMilliseconds <= first.CapturedAtUnixMilliseconds)
        {
            throw new InvalidDataException("The second normal-window scan did not produce a distinct fresh acquisition.");
        }

        await File.WriteAllTextAsync(fullOutputPath, JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            findingId = "LWB-PM12-008",
            state = "proven",
            proof = "normal_window_start_scan_render_refresh",
            windowMode = "persistent_normal_map_data",
            selectedTypes = new[] { "resource" },
            startButtonClicks = 2,
            first,
            second,
            generatedAt = DateTimeOffset.UtcNow,
        }, new JsonSerializerOptions(JsonOptions.Default) { WriteIndented = true }));
    }

    private async Task<UiLiveAcquisitionProof> RunUiLiveAcquisitionAsync(
        CoreWebView2 core,
        string previousRunId,
        long previousCapturedAt,
        string label)
    {
        string clickResult = await core.ExecuteScriptAsync("""
            (() => {
              const button = document.querySelector('.map-header .map-actions button.primary');
              if (!button || button.disabled) return false;
              button.click();
              return true;
            })()
            """);
        if (clickResult != "true")
            throw new InvalidOperationException($"The {label} normal-window Start Reading button could not be clicked.");

        JsonElement completedStatus = default;
        bool completed = false;
        for (int attempt = 0; attempt < 1600; attempt++)
        {
            JsonElement status = JsonSerializer.SerializeToElement(liveResourceService!.CreateStatus(), JsonOptions.Default);
            string runId = ReadStatusString(status, "scanRunId") ?? "";
            long capturedAt = ReadStatusInt64(status, "liveResourceCapturedAt") ?? 0;
            bool isReading = status.TryGetProperty("isReading", out JsonElement readingValue) && readingValue.ValueKind == JsonValueKind.True;
            string phase = ReadStatusString(status, "phase") ?? "";
            string? error = ReadStatusString(status, "lastError");
            if (!string.IsNullOrWhiteSpace(error))
                throw new InvalidOperationException($"The {label} normal-window acquisition failed: {error}");
            if (!isReading && phase == "idle" && runId.Length > 0 && runId != previousRunId && capturedAt > previousCapturedAt)
            {
                completedStatus = status;
                completed = true;
                break;
            }
            await Task.Delay(100);
        }
        if (!completed)
            throw new TimeoutException($"The {label} normal-window live resource acquisition did not complete within the proof bound.");

        NormalUiResourceProofExpected expected = ReadNormalUiProofExpected(completedStatus);

        bool resourceTabReady = false;
        for (int attempt = 0; attempt < 150; attempt++)
        {
            if (await core.ExecuteScriptAsync("document.querySelectorAll('.map-tabs button').length > 1") == "true")
            {
                resourceTabReady = true;
                break;
            }
            await Task.Delay(100);
        }
        if (!resourceTabReady)
            throw new InvalidOperationException("The normal Map Data resource tab did not render.");
        await core.ExecuteScriptAsync("document.querySelectorAll('.map-tabs button')[1]?.click();");

        bool resourceSearchSettled = false;
        for (int attempt = 0; attempt < 200; attempt++)
        {
            if (await core.ExecuteScriptAsync(
                "(()=>{const table=document.querySelector('.map-table--resource');return !!table && table.getAttribute('aria-busy') !== 'true';})()") == "true")
            {
                resourceSearchSettled = true;
                break;
            }
            await Task.Delay(100);
        }
        if (!resourceSearchSettled)
            throw new TimeoutException($"The {label} Resource tab did not settle before the explicit Search proof action.");

        long beforeSearchSequence = GetNormalUiProofSearchSequence();
        string searchClick = await core.ExecuteScriptAsync("""
            (() => {
              const button = document.querySelector('.map-searchbar > button');
              if (!button || button.disabled) return false;
              button.click();
              return true;
            })()
            """);
        if (searchClick != "true")
            throw new InvalidOperationException($"The {label} normal-window Resource Search button could not be clicked.");

        NormalUiResourceProofSearchObservation? observation = null;
        for (int attempt = 0; attempt < 200; attempt++)
        {
            observation = ReadNormalUiProofSearchAfter(beforeSearchSequence);
            if (observation is not null) break;
            await Task.Delay(100);
        }
        if (observation is null)
            throw new TimeoutException($"The {label} normal-window Resource Search did not produce a native map_search response.");

        JsonElement queryRow = NormalUiResourceProofContract.RequireCorrelatedSearchRow(expected, observation);
        string renderedTimeJson = await core.ExecuteScriptAsync(
            $"new Date({expected.UpdatedAt.ToString(System.Globalization.CultureInfo.InvariantCulture)}).toLocaleString(document.documentElement.lang || undefined)");
        string? expectedUpdatedText = JsonSerializer.Deserialize<string?>(renderedTimeJson);

        NormalUiResourceProofMatch? rendered = null;
        string? lastRenderRejection = null;
        for (int attempt = 0; attempt < 200; attempt++)
        {
            string snapshotJson = await core.ExecuteScriptAsync(NormalUiResourceProofContract.ResourceTableSnapshotScript);
            if (snapshotJson != "null")
            {
                try
                {
                    NormalUiResourceProofTableSnapshot? snapshot = JsonSerializer.Deserialize<NormalUiResourceProofTableSnapshot>(
                        snapshotJson, JsonOptions.Default);
                    if (snapshot is not null)
                    {
                        rendered = NormalUiResourceProofContract.RequireRenderedRow(
                            expected, queryRow, snapshot, expectedUpdatedText ?? string.Empty);
                        break;
                    }
                }
                catch (InvalidDataException ex)
                {
                    lastRenderRejection = ex.Message;
                }
            }
            await Task.Delay(100);
        }
        if (rendered is null)
            throw new InvalidOperationException(
                $"The {label} normal-window resource table never rendered the exact acquired/query row. {lastRenderRejection}");

        await Task.Delay(350);
        string proofPath = Path.GetFullPath(normalUiLiveResourceProofPath!);
        string stem = Path.GetFileNameWithoutExtension(proofPath);
        string screenshotPath = Path.Combine(Path.GetDirectoryName(proofPath)!, $"{stem}-{label}.png");
        await using (var output = File.Create(screenshotPath))
            await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, output);

        return new UiLiveAcquisitionProof(
            ReadStatusString(completedStatus, "scanRunId")!,
            ReadStatusInt64(completedStatus, "liveResourceCapturedAt")!.Value,
            ReadStatusInt32(completedStatus, "liveResourceAcquisitionOrdinal"),
            expected.ServerId,
            expected.RecordKey,
            expected.PointIndex,
            expected.X,
            expected.Y,
            expected.Level,
            expected.ResultPath,
            expected.ResultSha256,
            expected.ProbeVersion,
            expected.ProfileId,
            expected.LaunchSessionId,
            expected.GamePid,
            expected.DeclaredSourceCaptureSha256,
            observation.Sequence,
            observation.RequestId,
            observation.Payload.Clone(),
            queryRow.Clone(),
            rendered.RowText,
            rendered.Cells.ToArray(),
            screenshotPath);
    }

    private NormalUiResourceProofExpected ReadNormalUiProofExpected(JsonElement completedStatus)
    {
        string runId = ReadStatusString(completedStatus, "scanRunId")
            ?? throw new InvalidDataException("Completed resource status is missing scanRunId.");
        long capturedAt = ReadStatusInt64(completedStatus, "liveResourceCapturedAt")
            ?? throw new InvalidDataException("Completed resource status is missing liveResourceCapturedAt.");
        int? statusOrdinal = ReadStatusInt32(completedStatus, "liveResourceAcquisitionOrdinal");

        JsonElement helper = liveResourceService!.LastHelperResult
            ?? throw new InvalidDataException("Completed resource status has no correlated helper result.");
        string helperProfileId = ReadRequiredProofString(helper, "profileId", "profile identity");
        string helperLaunchSessionId = ReadRequiredProofString(helper, "launchSessionId", "launch session identity");
        if (!helper.TryGetProperty("gamePid", out JsonElement helperPidValue) ||
            !helperPidValue.TryGetInt32(out int helperGamePid) || helperGamePid <= 0)
            throw new InvalidDataException("Completed resource helper result has no positive game PID identity.");

        string resultPath = liveResourceService.LiveResultPath;
        FirstLivePreparedResource prepared = LiveResourceProbeCommandService.PrepareCorrelatedResult(
            resultPath,
            runId,
            out int? resultOrdinal,
            expectedServerId: liveResourceService.CurrentServerId,
            nowUtc: DateTimeOffset.UtcNow,
            expectedProfileId: helperProfileId,
            expectedLaunchSessionId: helperLaunchSessionId,
            expectedGamePid: helperGamePid);
        FirstLiveResultImport import = prepared.Import;
        if (import.CapturedAtUnixMilliseconds != capturedAt || import.ServerId != liveResourceService.CurrentServerId)
            throw new InvalidDataException("Immutable live resource result does not match the completed service server/time identity.");
        if (resultOrdinal != statusOrdinal)
            throw new InvalidDataException("Immutable live resource result acquisition ordinal does not match completed service status.");

        return new NormalUiResourceProofExpected(
            import.ServerId,
            import.RecordKey,
            import.PointIndex,
            import.X,
            import.Y,
            import.Level,
            import.CapturedAtUnixMilliseconds,
            resultPath,
            import.CaptureSha256,
            import.ProbeVersion,
            helperProfileId,
            helperLaunchSessionId,
            helperGamePid,
            import.DeclaredSourceCaptureSha256);
    }

    private long GetNormalUiProofSearchSequence()
    {
        lock (normalUiProofGate) return normalUiProofSearchSequence;
    }

    private NormalUiResourceProofSearchObservation? ReadNormalUiProofSearchAfter(long sequence)
    {
        lock (normalUiProofGate)
        {
            return normalUiProofSearchObservation is { } observation && observation.Sequence > sequence
                ? observation
                : null;
        }
    }

    private void RecordNormalUiProofSearch(string requestId, JsonElement payload, object? result)
    {
        if (normalUiLiveResourceProofPath is null) return;
        JsonElement resultElement = JsonSerializer.SerializeToElement(result, JsonOptions.Default);
        lock (normalUiProofGate)
        {
            normalUiProofSearchSequence++;
            normalUiProofSearchObservation = new NormalUiResourceProofSearchObservation(
                normalUiProofSearchSequence,
                requestId,
                payload.Clone(),
                resultElement.Clone());
        }
    }

    private static string ReadRequiredProofString(JsonElement value, string name, string description) =>
        ReadOptionalString(value, name) is { Length: > 0 } text
            ? text
            : throw new InvalidDataException($"Completed resource helper result has no {description}.");

    private static string? ReadOptionalString(JsonElement value, string name) =>
        value.TryGetProperty(name, out JsonElement property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static string? ReadStatusString(JsonElement status, string name) =>
        status.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static long? ReadStatusInt64(JsonElement status, string name) =>
        status.TryGetProperty(name, out JsonElement value) && value.TryGetInt64(out long parsed)
            ? parsed
            : null;

    private static int? ReadStatusInt32(JsonElement status, string name) =>
        status.TryGetProperty(name, out JsonElement value) && value.TryGetInt32(out int parsed)
            ? parsed
            : null;

    private sealed record UiLiveAcquisitionProof(
        string ScanRunId,
        long CapturedAtUnixMilliseconds,
        int? AcquisitionOrdinal,
        int ServerId,
        string RecordKey,
        int PointIndex,
        int X,
        int Y,
        int? Level,
        string ResultPath,
        string ResultSha256,
        string? ProbeVersion,
        string? ProfileId,
        string? LaunchSessionId,
        int? GamePid,
        string? DeclaredSourceCaptureSha256,
        long SearchSequence,
        string SearchRequestId,
        JsonElement SearchPayload,
        JsonElement SearchRow,
        string RowText,
        IReadOnlyList<string> RenderedCells,
        string ScreenshotPath);

    private async Task SelectFirstLiveResourceAsync(CoreWebView2 core)
    {
        if (firstLiveResult is null) return;
        bool resourceTabReady = false;
        for (int attempt = 0; attempt < 120; attempt++)
        {
            if (await core.ExecuteScriptAsync("document.querySelectorAll('.map-tabs button').length > 1") == "true")
            {
                resourceTabReady = true;
                break;
            }
            await Task.Delay(100);
        }
        if (!resourceTabReady)
            throw new InvalidOperationException("The Map Data resource tab did not render for saved capture replay.");

        await core.ExecuteScriptAsync("document.querySelectorAll('.map-tabs button')[1]?.click();");
        string expectedCoordinate = JsonSerializer.Serialize($"{firstLiveResult.X},{firstLiveResult.Y}");
        for (int attempt = 0; attempt < 120; attempt++)
        {
            if (await core.ExecuteScriptAsync($"document.body.innerText.includes({expectedCoordinate})") == "true")
                return;
            await Task.Delay(100);
        }
        throw new InvalidOperationException("The saved-capture resource row did not render in Map Data.");
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

        int delayedStartedBeforeDuplicate = hostProbeService.DelayedStarted;
        string duplicatePhase = """
            window.__LWBridgeHostProbe = { pending: true, phase: 'duplicate' };
            (async () => {
              try {
                const native = window.chrome.webview;
                const profileId = window.LWBridgePreview.profiles.selectedProfileId;
                const id = 'duplicate-' + (crypto.randomUUID ? crypto.randomUUID() : Date.now());
                const responses = [];
                const onMessage = event => {
                  let message = event.data;
                  if (typeof message === 'string') {
                    try { message = JSON.parse(message); } catch { return; }
                  }
                  if (message?.kind === 'response' && message.id === id) responses.push({
                    ok: message.ok === true,
                    code: message.error?.code || ''
                  });
                };
                native.addEventListener('message', onMessage);
                const request = {
                  kind: 'invoke', sessionId: window.__LWBridgeBootstrap.sessionId, id,
                  command: 'diagnostic_host_delayed', payload: { profileId }
                };
                native.postMessage(request);
                native.postMessage(request);
                for (let attempt = 0; attempt < 100 && !responses.some(r => r.code === 'DUPLICATE_REQUEST_ID'); attempt++)
                  await new Promise(resolve => setTimeout(resolve, 20));
                native.postMessage({kind: 'cancel', sessionId: window.__LWBridgeBootstrap.sessionId, id});
                for (let attempt = 0; attempt < 100 && responses.length < 2; attempt++)
                  await new Promise(resolve => setTimeout(resolve, 20));
                native.removeEventListener('message', onMessage);
                window.__LWBridgeHostProbe = { pending: false, phase: 'duplicate', responses };
              } catch (error) {
                window.__LWBridgeHostProbe = { pending: false, phase: 'duplicate', error: String(error?.message || error) };
              }
            })();
            """;
        await core.ExecuteScriptAsync(duplicatePhase);
        JsonElement duplicate = await ReadHostProbeResultAsync(core, "duplicate");
        for (int attempt = 0; attempt < 100 && hostProbeService.DelayedActive != 0; attempt++)
            await Task.Delay(20);
        string[] duplicateCodes = duplicate.GetProperty("responses").EnumerateArray()
            .Select(item => item.GetProperty("code").GetString() ?? string.Empty)
            .ToArray();
        bool duplicateRequestRejected = hostProbeService.DelayedStarted == delayedStartedBeforeDuplicate + 1 &&
            hostProbeService.DelayedActive == 0 &&
            duplicateCodes.Count(code => code == "DUPLICATE_REQUEST_ID") == 1 &&
            duplicateCodes.Count(code => code == "COMMAND_CANCELLED") == 1;
        int delayedCancelledBeforeReload = hostProbeService.DelayedCancelled;

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
        if (hostProbeService.DelayedActive != 0 || hostProbeService.DelayedCancelled <= delayedCancelledBeforeReload)
            throw new InvalidOperationException("Reload did not cancel and drain the prior document request.");
        int reloadClosedRequestCount = lastClosedRequestCount;
        int reloadClosedSubscriptionCount = lastClosedSubscriptionCount;

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

        hostProbeService.QueueConfigSave(delayMs: 180, fail: true);
        string rollbackPhase = """
            window.__LWBridgeHostProbe = { pending: true, phase: 'rollback' };
            (async () => {
              const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
              const toggle = () => document.querySelector('.quick-actions-panel .toggle-row[role="switch"]');
              try {
                for (let attempt = 0; attempt < 100 && !toggle(); attempt++) await sleep(20);
                if (!toggle()) throw new Error('auto-launch switch not rendered');
                const beforeChecked = toggle().getAttribute('aria-checked') === 'true';
                const beforeConfig = await window.LWBridgePreview.invoke('local_config_get');
                toggle().click();
                await sleep(40);
                const optimisticChecked = toggle().getAttribute('aria-checked') === 'true';
                for (let attempt = 0; attempt < 100 && (toggle().getAttribute('aria-checked') === 'true') !== beforeChecked; attempt++) await sleep(20);
                const afterChecked = toggle().getAttribute('aria-checked') === 'true';
                const afterConfig = await window.LWBridgePreview.invoke('local_config_get');
                const readVisibleError = () => [...document.querySelectorAll('.profile-error')]
                  .map(node => node.textContent?.trim() || '').filter(Boolean).join(' ');
                let visibleErrorText = readVisibleError();
                for (let attempt = 0; attempt < 100 && !visibleErrorText; attempt++) {
                  await sleep(20);
                  visibleErrorText = readVisibleError();
                }
                window.__LWBridgeHostProbe = {
                  pending: false, phase: 'rollback', beforeChecked, optimisticChecked,
                  afterChecked, beforeConfig, afterConfig, visibleErrorText
                };
              } catch (error) {
                window.__LWBridgeHostProbe = { pending: false, phase: 'rollback', error: String(error?.message || error) };
              }
            })();
            """;
        await core.ExecuteScriptAsync(rollbackPhase);
        JsonElement rollback = await ReadHostProbeResultAsync(core, "rollback");
        bool rollbackBefore = rollback.GetProperty("beforeChecked").GetBoolean();
        bool rollbackOptimistic = rollback.GetProperty("optimisticChecked").GetBoolean();
        bool rollbackAfter = rollback.GetProperty("afterChecked").GetBoolean();
        bool rollbackConfigBefore = rollback.GetProperty("beforeConfig").GetProperty("autoLaunchGame").GetBoolean();
        bool rollbackConfigAfter = rollback.GetProperty("afterConfig").GetProperty("autoLaunchGame").GetBoolean();
        string preferenceRollbackErrorText = rollback.GetProperty("visibleErrorText").GetString() ?? string.Empty;
        bool preferenceRollbackErrorVisible = !string.IsNullOrWhiteSpace(preferenceRollbackErrorText);
        bool preferenceRollbackVisible = rollbackOptimistic != rollbackBefore && rollbackAfter == rollbackBefore &&
            rollbackConfigBefore == rollbackConfigAfter && rollbackConfigAfter == rollbackAfter;

        hostProbeService.QueueConfigSave(delayMs: 240);
        hostProbeService.QueueConfigSave();
        string overlapPhase = """
            window.__LWBridgeHostProbe = { pending: true, phase: 'overlap' };
            (async () => {
              const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
              const toggle = () => document.querySelector('.quick-actions-panel .toggle-row[role="switch"]');
              try {
                const startState = await window.LWBridgePreview.invoke('diagnostic_host_state', {profileId: window.LWBridgePreview.profiles.selectedProfileId});
                const initialChecked = toggle().getAttribute('aria-checked') === 'true';
                toggle().click();
                for (let attempt = 0; attempt < 50 && (toggle().getAttribute('aria-checked') === 'true') === initialChecked; attempt++) await sleep(10);
                const firstDraftChecked = toggle().getAttribute('aria-checked') === 'true';
                toggle().click();
                const latestIntendedChecked = initialChecked;
                let endState = startState;
                for (let attempt = 0; attempt < 120; attempt++) {
                  await sleep(20);
                  endState = await window.LWBridgePreview.invoke('diagnostic_host_state', {profileId: window.LWBridgePreview.profiles.selectedProfileId});
                  if (endState.configSaveStarted >= startState.configSaveStarted + 2 && endState.configSaveActive === 0) break;
                }
                const finalChecked = toggle().getAttribute('aria-checked') === 'true';
                const finalConfig = await window.LWBridgePreview.invoke('local_config_get');
                window.__LWBridgeHostProbe = {
                  pending: false, phase: 'overlap', initialChecked, firstDraftChecked,
                  latestIntendedChecked, finalChecked, finalConfig, startState, endState
                };
              } catch (error) {
                window.__LWBridgeHostProbe = { pending: false, phase: 'overlap', error: String(error?.message || error) };
              }
            })();
            """;
        await core.ExecuteScriptAsync(overlapPhase);
        JsonElement overlap = await ReadHostProbeResultAsync(core, "overlap");
        bool overlapInitial = overlap.GetProperty("initialChecked").GetBoolean();
        bool overlapFirstDraft = overlap.GetProperty("firstDraftChecked").GetBoolean();
        bool overlapLatest = overlap.GetProperty("latestIntendedChecked").GetBoolean();
        bool overlapFinal = overlap.GetProperty("finalChecked").GetBoolean();
        bool overlapConfig = overlap.GetProperty("finalConfig").GetProperty("autoLaunchGame").GetBoolean();
        int overlapStartedBefore = overlap.GetProperty("startState").GetProperty("configSaveStarted").GetInt32();
        int overlapStartedAfter = overlap.GetProperty("endState").GetProperty("configSaveStarted").GetInt32();
        int overlapMaxActive = overlap.GetProperty("endState").GetProperty("configSaveMaxActive").GetInt32();
        bool overlappingPreferenceSavesOrdered = overlapFirstDraft != overlapInitial && overlapLatest == overlapInitial &&
            overlapFinal == overlapLatest && overlapConfig == overlapLatest &&
            overlapStartedAfter >= overlapStartedBefore + 2 && overlapMaxActive == 1;

        async Task<JsonElement> RunPreferenceFailureSequenceAsync(string phase)
        {
            string script = """
                window.__LWBridgeHostProbe = { pending: true, phase: '__PHASE__' };
                (async () => {
                  const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
                  const toggle = () => document.querySelector('.quick-actions-panel .toggle-row[role="switch"]');
                  const readVisibleError = () => [...document.querySelectorAll('.profile-error')]
                    .map(node => node.textContent?.trim() || '').filter(Boolean).join(' ');
                  try {
                    for (let attempt = 0; attempt < 100 && !toggle(); attempt++) await sleep(20);
                    if (!toggle()) throw new Error('auto-launch switch not rendered');
                    const startState = await window.LWBridgePreview.invoke('diagnostic_host_state', {profileId: window.LWBridgePreview.profiles.selectedProfileId});
                    const initialChecked = toggle().getAttribute('aria-checked') === 'true';
                    toggle().click();
                    for (let attempt = 0; attempt < 50 && (toggle().getAttribute('aria-checked') === 'true') === initialChecked; attempt++) await sleep(10);
                    const firstDraftChecked = toggle().getAttribute('aria-checked') === 'true';
                    toggle().click();
                    for (let attempt = 0; attempt < 50 && (toggle().getAttribute('aria-checked') === 'true') !== initialChecked; attempt++) await sleep(10);
                    const secondDraftChecked = toggle().getAttribute('aria-checked') === 'true';
                    let endState = startState;
                    for (let attempt = 0; attempt < 150; attempt++) {
                      await sleep(20);
                      endState = await window.LWBridgePreview.invoke('diagnostic_host_state', {profileId: window.LWBridgePreview.profiles.selectedProfileId});
                      if (endState.configSaveStarted >= startState.configSaveStarted + 2 && endState.configSaveActive === 0) break;
                    }
                    await sleep(60);
                    const finalChecked = toggle().getAttribute('aria-checked') === 'true';
                    const finalConfig = await window.LWBridgePreview.invoke('local_config_get');
                    const visibleErrorText = readVisibleError();
                    window.__LWBridgeHostProbe = {
                      pending: false, phase: '__PHASE__', initialChecked, firstDraftChecked,
                      secondDraftChecked, finalChecked, finalConfig, visibleErrorText, startState, endState
                    };
                  } catch (error) {
                    window.__LWBridgeHostProbe = { pending: false, phase: '__PHASE__', error: String(error?.message || error) };
                  }
                })();
                """.Replace("__PHASE__", phase, StringComparison.Ordinal);
            await core.ExecuteScriptAsync(script);
            return await ReadHostProbeResultAsync(core, phase);
        }

        hostProbeService.QueueConfigSave(delayMs: 160, fail: true);
        hostProbeService.QueueConfigSave(fail: true);
        JsonElement bothFailed = await RunPreferenceFailureSequenceAsync("preference-both-fail");
        bool bothFailedInitial = bothFailed.GetProperty("initialChecked").GetBoolean();
        bool bothFailedFirstDraft = bothFailed.GetProperty("firstDraftChecked").GetBoolean();
        bool bothFailedSecondDraft = bothFailed.GetProperty("secondDraftChecked").GetBoolean();
        bool bothFailedFinal = bothFailed.GetProperty("finalChecked").GetBoolean();
        bool bothFailedConfig = bothFailed.GetProperty("finalConfig").GetProperty("autoLaunchGame").GetBoolean();
        string bothFailedErrorText = bothFailed.GetProperty("visibleErrorText").GetString() ?? string.Empty;
        bool preferenceBothFailedReconciled = bothFailedFirstDraft != bothFailedInitial &&
            bothFailedSecondDraft == bothFailedInitial && bothFailedFinal == bothFailedInitial &&
            bothFailedConfig == bothFailedInitial && !string.IsNullOrWhiteSpace(bothFailedErrorText);

        hostProbeService.QueueConfigSave(delayMs: 160, fail: true);
        hostProbeService.QueueConfigSave();
        JsonElement failThenSuccess = await RunPreferenceFailureSequenceAsync("preference-fail-success");
        bool failThenSuccessInitial = failThenSuccess.GetProperty("initialChecked").GetBoolean();
        bool failThenSuccessFirstDraft = failThenSuccess.GetProperty("firstDraftChecked").GetBoolean();
        bool failThenSuccessSecondDraft = failThenSuccess.GetProperty("secondDraftChecked").GetBoolean();
        bool failThenSuccessFinal = failThenSuccess.GetProperty("finalChecked").GetBoolean();
        bool failThenSuccessConfig = failThenSuccess.GetProperty("finalConfig").GetProperty("autoLaunchGame").GetBoolean();
        string failThenSuccessErrorText = failThenSuccess.GetProperty("visibleErrorText").GetString() ?? string.Empty;
        bool preferenceFailThenSuccessReconciled = failThenSuccessFirstDraft != failThenSuccessInitial &&
            failThenSuccessSecondDraft == failThenSuccessInitial && failThenSuccessFinal == failThenSuccessInitial &&
            failThenSuccessConfig == failThenSuccessInitial && string.IsNullOrWhiteSpace(failThenSuccessErrorText);

        hostProbeService.QueueConfigSave(delayMs: 160);
        hostProbeService.QueueConfigSave(fail: true);
        JsonElement successThenFail = await RunPreferenceFailureSequenceAsync("preference-success-fail");
        bool successThenFailInitial = successThenFail.GetProperty("initialChecked").GetBoolean();
        bool successThenFailFirstDraft = successThenFail.GetProperty("firstDraftChecked").GetBoolean();
        bool successThenFailSecondDraft = successThenFail.GetProperty("secondDraftChecked").GetBoolean();
        bool successThenFailFinal = successThenFail.GetProperty("finalChecked").GetBoolean();
        bool successThenFailConfig = successThenFail.GetProperty("finalConfig").GetProperty("autoLaunchGame").GetBoolean();
        string successThenFailErrorText = successThenFail.GetProperty("visibleErrorText").GetString() ?? string.Empty;
        bool preferenceSuccessThenFailReconciled = successThenFailFirstDraft != successThenFailInitial &&
            successThenFailSecondDraft == successThenFailInitial && successThenFailFinal == successThenFailFirstDraft &&
            successThenFailConfig == successThenFailFirstDraft && !string.IsNullOrWhiteSpace(successThenFailErrorText);

        hostProbeService.QueueConfigSave();
        string preferenceRecoveryPhase = """
            window.__LWBridgeHostProbe = { pending: true, phase: 'preference-recovery' };
            (async () => {
              const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
              const toggle = () => document.querySelector('.quick-actions-panel .toggle-row[role="switch"]');
              const readVisibleError = () => [...document.querySelectorAll('.profile-error')]
                .map(node => node.textContent?.trim() || '').filter(Boolean).join(' ');
              try {
                const startState = await window.LWBridgePreview.invoke('diagnostic_host_state', {profileId: window.LWBridgePreview.profiles.selectedProfileId});
                const initialChecked = toggle().getAttribute('aria-checked') === 'true';
                const errorBefore = readVisibleError();
                toggle().click();
                for (let attempt = 0; attempt < 50 && (toggle().getAttribute('aria-checked') === 'true') === initialChecked; attempt++) await sleep(10);
                const optimisticChecked = toggle().getAttribute('aria-checked') === 'true';
                let endState = startState;
                for (let attempt = 0; attempt < 120; attempt++) {
                  await sleep(20);
                  endState = await window.LWBridgePreview.invoke('diagnostic_host_state', {profileId: window.LWBridgePreview.profiles.selectedProfileId});
                  if (endState.configSaveStarted >= startState.configSaveStarted + 1 && endState.configSaveActive === 0) break;
                }
                await sleep(60);
                const finalChecked = toggle().getAttribute('aria-checked') === 'true';
                const finalConfig = await window.LWBridgePreview.invoke('local_config_get');
                const errorAfter = readVisibleError();
                window.__LWBridgeHostProbe = {
                  pending: false, phase: 'preference-recovery', initialChecked, optimisticChecked,
                  finalChecked, finalConfig, errorBefore, errorAfter, startState, endState
                };
              } catch (error) {
                window.__LWBridgeHostProbe = { pending: false, phase: 'preference-recovery', error: String(error?.message || error) };
              }
            })();
            """;
        await core.ExecuteScriptAsync(preferenceRecoveryPhase);
        JsonElement preferenceRecovery = await ReadHostProbeResultAsync(core, "preference-recovery");
        bool preferenceRecoveryInitial = preferenceRecovery.GetProperty("initialChecked").GetBoolean();
        bool preferenceRecoveryOptimistic = preferenceRecovery.GetProperty("optimisticChecked").GetBoolean();
        bool preferenceRecoveryFinal = preferenceRecovery.GetProperty("finalChecked").GetBoolean();
        bool preferenceRecoveryConfig = preferenceRecovery.GetProperty("finalConfig").GetProperty("autoLaunchGame").GetBoolean();
        string preferenceRecoveryErrorBefore = preferenceRecovery.GetProperty("errorBefore").GetString() ?? string.Empty;
        string preferenceRecoveryErrorAfter = preferenceRecovery.GetProperty("errorAfter").GetString() ?? string.Empty;
        bool preferenceRecoveryClearsError = !string.IsNullOrWhiteSpace(preferenceRecoveryErrorBefore) &&
            preferenceRecoveryOptimistic != preferenceRecoveryInitial &&
            preferenceRecoveryFinal == preferenceRecoveryOptimistic && preferenceRecoveryConfig == preferenceRecoveryFinal &&
            string.IsNullOrWhiteSpace(preferenceRecoveryErrorAfter);

        hostProbeService.SetForceMissingGameRoot(true);
        Task pickerReloaded = WaitForNextSuccessfulNavigationAsync(core);
        core.Reload();
        await pickerReloaded.WaitAsync(TimeSpan.FromSeconds(15));
        hostProbeService.QueuePicker(HostProbePickerOutcome.Cancel);
        hostProbeService.QueuePicker(HostProbePickerOutcome.Invalid);
        string pickerPhase = """
            window.__LWBridgeHostProbe = { pending: true, phase: 'picker' };
            (async () => {
              const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
              const button = () => document.querySelector('.game-root-missing button');
              const message = () => document.querySelector('.game-root-missing span')?.textContent || '';
              try {
                for (let attempt = 0; attempt < 100 && !button(); attempt++) await sleep(20);
                if (!button()) throw new Error('game-root picker button not rendered');
                const baselineText = message();
                const startState = await window.LWBridgePreview.invoke('diagnostic_host_state', {profileId: window.LWBridgePreview.profiles.selectedProfileId});
                button().click();
                await sleep(40);
                const cancelBusy = button().disabled === true;
                for (let attempt = 0; attempt < 100 && button().disabled; attempt++) await sleep(20);
                const afterCancelText = message();
                button().click();
                await sleep(40);
                const invalidBusy = button().disabled === true;
                for (let attempt = 0; attempt < 100 && button().disabled; attempt++) await sleep(20);
                const afterInvalidText = message();
                const endState = await window.LWBridgePreview.invoke('diagnostic_host_state', {profileId: window.LWBridgePreview.profiles.selectedProfileId});
                window.__LWBridgeHostProbe = {
                  pending: false, phase: 'picker', baselineText, cancelBusy, afterCancelText,
                  invalidBusy, afterInvalidText, startState, endState
                };
              } catch (error) {
                window.__LWBridgeHostProbe = { pending: false, phase: 'picker', error: String(error?.message || error) };
              }
            })();
            """;
        await core.ExecuteScriptAsync(pickerPhase);
        JsonElement picker = await ReadHostProbeResultAsync(core, "picker");
        string pickerBaselineText = picker.GetProperty("baselineText").GetString() ?? string.Empty;
        string pickerAfterCancelText = picker.GetProperty("afterCancelText").GetString() ?? string.Empty;
        string pickerAfterInvalidText = picker.GetProperty("afterInvalidText").GetString() ?? string.Empty;
        bool pickerCancelBusy = picker.GetProperty("cancelBusy").GetBoolean();
        bool pickerInvalidBusy = picker.GetProperty("invalidBusy").GetBoolean();
        int pickerCancelledBefore = picker.GetProperty("startState").GetProperty("pickerCancelled").GetInt32();
        int pickerInvalidBefore = picker.GetProperty("startState").GetProperty("pickerInvalid").GetInt32();
        int pickerCancelledAfter = picker.GetProperty("endState").GetProperty("pickerCancelled").GetInt32();
        int pickerInvalidAfter = picker.GetProperty("endState").GetProperty("pickerInvalid").GetInt32();
        bool pickerCancelAndInvalidHandled = pickerCancelBusy && pickerInvalidBusy &&
            pickerAfterCancelText == pickerBaselineText && !string.IsNullOrWhiteSpace(pickerAfterInvalidText) &&
            pickerAfterInvalidText != pickerBaselineText && pickerCancelledAfter == pickerCancelledBefore + 1 &&
            pickerInvalidAfter == pickerInvalidBefore + 1;

        string localSourceBeforeExternal = core.Source;
        string sessionBeforeExternal = documentSession.Id;
        int rejectedBefore = rejectedNavigationCount;
        core.Navigate("https://example.invalid/blocked-by-host-probe");
        for (int attempt = 0; attempt < 100 && rejectedNavigationCount == rejectedBefore; attempt++)
            await Task.Delay(20);
        bool externalNavigationRejected = rejectedNavigationCount > rejectedBefore &&
            core.Source.StartsWith(UiOrigin + "/", StringComparison.Ordinal) &&
            documentSession.Id == sessionBeforeExternal;
        string sourceAfterExternal = core.Source;

        string latePhase = """
            window.__LWBridgeHostProbeLate = { started: true };
            window.LWBridgePreview.invoke('diagnostic_host_late', {
              profileId: window.LWBridgePreview.profiles.selectedProfileId
            }).then(
              () => { window.__LWBridgeHostProbeLate.completed = true; },
              error => { window.__LWBridgeHostProbeLate.error = error?.code || String(error); }
            );
            """;
        int lateStartedBefore = hostProbeService.LateStarted;
        await core.ExecuteScriptAsync(latePhase);
        for (int attempt = 0; attempt < 100 && hostProbeService.LateStarted <= lateStartedBefore; attempt++)
            await Task.Delay(20);
        if (hostProbeService.LateStarted <= lateStartedBefore)
            throw new InvalidOperationException("Host probe late request did not start before window close.");
        int activeRequestsBeforeClose = documentSession.Requests.ActiveCount;
        int postedMessagesBeforeClose = postedWebMessageCount;
        Close();
        hostProbeService.ReleaseLate();
        for (int attempt = 0; attempt < 100 && hostProbeService.LateCompleted < 1; attempt++)
            await Task.Delay(20);
        for (int attempt = 0; attempt < 100 && documentSession.Requests.ActiveCount != 0; attempt++)
            await Task.Delay(20);
        int postedMessagesAfterLateCompletion = postedWebMessageCount;
        int activeRequestsAfterLateCompletion = documentSession.Requests.ActiveCount;
        bool closedWindowLateResponseSuppressed = sessionClosed && activeRequestsBeforeClose >= 1 &&
            hostProbeService.LateCompleted >= 1 && activeRequestsAfterLateCompletion == 0 &&
            postedMessagesAfterLateCompletion == postedMessagesBeforeClose;

        bool slowStorageUiResponsive = timerDelayMs < 400 && slowStorageElapsedMs >= 700;
        bool sessionRotated = !string.Equals(oldSessionId, newSessionId, StringComparison.Ordinal) &&
            documentGeneration >= 2;
        bool reloadCancelledOldWork = hostProbeService.DelayedCancelled > delayedCancelledBeforeReload &&
            hostProbeService.DelayedActive == 0 && reloadClosedRequestCount >= 1;
        bool reloadResetSubscriptions = reloadClosedSubscriptionCount >= 1;
        bool staleSessionIgnored = slowBeforeStale == 0 && slowAfterStale == 0;
        bool structuredError = expectedErrorCode == "DIAGNOSTIC_EXPECTED";
        bool startupAutoLaunchSuppressed = !bootstrapAutoLaunch;
        bool ok = slowStorageUiResponsive && sessionRotated && reloadCancelledOldWork && reloadResetSubscriptions &&
            staleSessionIgnored && structuredError && externalNavigationRejected && startupAutoLaunchSuppressed &&
            duplicateRequestRejected && preferenceRollbackVisible && preferenceRollbackErrorVisible &&
            overlappingPreferenceSavesOrdered && preferenceBothFailedReconciled &&
            preferenceFailThenSuccessReconciled && preferenceSuccessThenFailReconciled &&
            preferenceRecoveryClearsError &&
            pickerCancelAndInvalidHandled && closedWindowLateResponseSuppressed;

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
            lastClosedRequestCount = reloadClosedRequestCount,
            lastClosedSubscriptionCount = reloadClosedSubscriptionCount,
            staleSessionIgnored,
            slowBeforeStale,
            slowAfterStale,
            structuredError,
            expectedErrorCode,
            duplicateRequestRejected,
            duplicateCodes,
            preferenceRollbackVisible,
            preferenceRollbackErrorVisible,
            preferenceRollbackErrorText,
            preferenceRollbackBefore = rollbackBefore,
            preferenceRollbackOptimistic = rollbackOptimistic,
            preferenceRollbackAfter = rollbackAfter,
            preferenceConfigBefore = rollbackConfigBefore,
            preferenceConfigAfter = rollbackConfigAfter,
            overlappingPreferenceSavesOrdered,
            overlapInitial,
            overlapFirstDraft,
            overlapLatest,
            overlapFinal,
            overlapConfig,
            overlapStartedBefore,
            overlapStartedAfter,
            overlapMaxActive,
            preferenceBothFailedReconciled,
            bothFailedInitial,
            bothFailedFirstDraft,
            bothFailedSecondDraft,
            bothFailedFinal,
            bothFailedConfig,
            bothFailedErrorText,
            preferenceFailThenSuccessReconciled,
            failThenSuccessInitial,
            failThenSuccessFirstDraft,
            failThenSuccessSecondDraft,
            failThenSuccessFinal,
            failThenSuccessConfig,
            failThenSuccessErrorText,
            preferenceSuccessThenFailReconciled,
            successThenFailInitial,
            successThenFailFirstDraft,
            successThenFailSecondDraft,
            successThenFailFinal,
            successThenFailConfig,
            successThenFailErrorText,
            preferenceRecoveryClearsError,
            preferenceRecoveryInitial,
            preferenceRecoveryOptimistic,
            preferenceRecoveryFinal,
            preferenceRecoveryConfig,
            preferenceRecoveryErrorBefore,
            preferenceRecoveryErrorAfter,
            pickerCancelAndInvalidHandled,
            pickerCancelBusy,
            pickerInvalidBusy,
            pickerBaselineText,
            pickerAfterCancelText,
            pickerAfterInvalidText,
            pickerCancelledBefore,
            pickerCancelledAfter,
            pickerInvalidBefore,
            pickerInvalidAfter,
            externalNavigationRejected,
            localSourceBeforeExternal,
            sourceAfterExternal,
            rejectedNavigationCount,
            closedWindowLateResponseSuppressed,
            activeRequestsBeforeClose,
            activeRequestsAfterLateCompletion,
            postedMessagesBeforeClose,
            postedMessagesAfterLateCompletion,
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
        if (capturePath is not null && firstLiveResult is null) return;
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
                        : command == "game_root_status" && hostProbeService?.ForceMissingGameRoot == true
                            ? Task.FromResult<object?>(new GameRootStatus(
                                false, string.Empty, "host-probe", "GAME_ROOT_NOT_FOUND",
                                null, null, null, null))
                        : Task.Run(async () =>
                        {
                            if (hostProbeService is not null)
                                await hostProbeService.BeforeProductionCommandAsync(command, cancellationToken).ConfigureAwait(false);
                            return await backend.InvokeAsync(command, payload, cancellationToken).ConfigureAwait(false);
                        }, cancellationToken));
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
                if (command == "map_search" && normalUiLiveResourceProofPath is not null)
                    RecordNormalUiProofSearch(id, payload, execution.Result);
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

        if (hostProbeService?.TryTakePicker(out HostProbePickerOutcome probeOutcome) == true)
        {
            hostProbeService.RecordPickerStarted();
            await Task.Delay(180, cancellationToken);
            if (probeOutcome == HostProbePickerOutcome.Cancel)
            {
                hostProbeService.RecordPickerCancelled();
                return new { canceled = true };
            }

            if (isolatedConfigRoot is null)
                throw new InvalidOperationException("Host probe picker requires isolated storage.");
            string invalidRoot = Path.Combine(isolatedConfigRoot, "invalid-game-root");
            Directory.CreateDirectory(invalidRoot);
            GameRootStatus invalid = await Task.Run(() => backend.SaveGameRoot(invalidRoot), cancellationToken);
            hostProbeService.RecordPickerInvalid();
            return invalid;
        }

        using var dialog = new FolderBrowserDialog
        {
            Description = "Select the Last War installation directory",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
        };
        if (!string.IsNullOrWhiteSpace(current.Path) && Directory.Exists(current.Path))
            dialog.InitialDirectory = current.Path;
        if (dialog.ShowDialog(this) != DialogResult.OK) return new { canceled = true };
        string selectedPath = dialog.SelectedPath;
        GameRootStatus selected = await Task.Run(() => backend.SaveGameRoot(selectedPath), cancellationToken);
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
        postedWebMessageCount++;
        webView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(message, JsonOptions.Default));
    }

    private bool IsCurrentDocument(DocumentSession session) =>
        !sessionClosed && ReferenceEquals(documentSession, session) && !session.IsClosed;

    private void OnFormClosed(object? sender, FormClosedEventArgs e)
    {
        sessionClosed = true;
        documentSession.Close();
        liveResourceService?.Close();
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
