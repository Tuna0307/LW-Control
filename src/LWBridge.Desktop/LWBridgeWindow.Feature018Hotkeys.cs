using System.Globalization;
using System.Text.Json;
using System.Collections.Concurrent;

namespace LWBridge.Desktop;

internal sealed partial class LWBridgeWindow
{
    private readonly System.Windows.Forms.Timer feature018HotkeyMonitor = new() { Interval = 200 };
    private Feature018HotkeyCapture? feature018Capture;
    private Feature018HotkeyActions? feature018Actions;
    private Feature018HotkeySession? feature018Account;
    private OverviewMapScanSession? feature018Owner;
    private DateTimeOffset feature018InputObservedAt;
    private bool feature018InputClear;
    private bool feature018FocusPolling;
    // Live R2 proof must verify its explicitly authorized UID/server before
    // installing any game-window key capture. Normal interactive launches use
    // the existing per-owner account and text-focus checks below.
    private bool feature018R2AccountVerified;
    private Feature018HotkeySession? feature018R2ApprovedAccount;
    private readonly ConcurrentQueue<Feature018R2KeyOutcome> feature018R2KeyOutcomes = new();
    private readonly object feature018R2KeyEvidenceGate = new();
    private sealed record Feature018R2KeyOutcome(string Kind, int Slot,
        string Status, string Reason, DateTimeOffset ObservedAtUtc,
        JsonElement? NativeObservation = null);
    private HotkeyConfig feature018Preferences = new();

    private void StartFeature018HotkeyMonitoring()
    {
        if (capturePath is not null || liveProbePath is not null ||
            hostProbePath is not null || (normalUiLiveMapProofPath is not null &&
                (Environment.GetEnvironmentVariable("LWBRIDGE_FEATURE018_R2_PROOF") != "1" ||
                 !feature018R2AccountVerified)) ||
            normalUiLiveResourceProofPath is not null || mapUiIntegrationProofPath is not null ||
            homeMapCampaignProofPath is not null || map010NamesProofPath is not null)
            return;
        feature018HotkeyMonitor.Tick += Feature018HotkeyTick;
        feature018HotkeyMonitor.Start();
        StartFeature018ChestMonitoring();
    }

    private Feature018HotkeySession? ReadFeature018AuthenticatedAccount(
        OverviewLifecycleService lifecycle,
        OverviewMapScanSession session)
    {
        if (!lifecycle.MatchesOwnedMapScanSession(session)) return null;
        Feature018AuthenticatedGameAccount? account =
            Feature018OwnedGameExchange.ReadFreshAuthenticatedAccount(
                Path.Combine(lifecycle.RuntimeRoot, "heartbeat.json"), session);
        if (account is null || !lifecycle.MatchesOwnedMapScanSession(session)) return null;
        return new Feature018HotkeySession(session.ProfileId, session.SessionId,
            account.GameUid, account.ServerId);
    }

    private void RetireFeature018Hotkeys()
    {
        feature018Capture?.Dispose();
        feature018Capture = null;
        feature018Actions = null;
        feature018Account = null;
        feature018Owner = null;
        feature018InputClear = false;
        feature018InputObservedAt = default;
    }

    private bool Feature018InputAdmission(OverviewLifecycleService lifecycle,
        OverviewMapScanSession owner, Feature018HotkeySession account)
    {
        return !sessionClosed && feature018InputClear &&
               DateTimeOffset.UtcNow - feature018InputObservedAt < TimeSpan.FromMilliseconds(900) &&
               feature018Owner == owner && feature018Account == account &&
               lifecycle.MatchesOwnedMapScanSession(owner) &&
               ReadFeature018AuthenticatedAccount(lifecycle, owner) == account;
    }

    private async void Feature018HotkeyTick(object? sender, EventArgs e)
    {
        if (feature018FocusPolling || sessionClosed || IsDisposed) return;
        feature018FocusPolling = true;
        try
        {
            OverviewLifecycleService? lifecycle = overviewLifecycleService;
            OverviewMapScanSession? owner = lifecycle?.GetReadyMapScanSession();
            Feature018HotkeySession? account = lifecycle is not null && owner is not null
                ? ReadFeature018AuthenticatedAccount(lifecycle, owner) : null;
            if (lifecycle is null || owner is null || account is null || hotkeyConfigService is null)
            {
                RetireFeature018Hotkeys();
                return;
            }
            if (normalUiLiveMapProofPath is not null &&
                Environment.GetEnvironmentVariable("LWBRIDGE_FEATURE018_R2_PROOF") == "1" &&
                account != feature018R2ApprovedAccount)
            {
                RetireFeature018Hotkeys();
                return;
            }
            if (feature018Owner != owner || feature018Account != account ||
                feature018Capture is null)
            {
                RetireFeature018Hotkeys();
                if (!DateTimeOffset.TryParse(owner.GameStartedAtUtc,
                        CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal,
                        out DateTimeOffset launched)) return;
                var capture = new Feature018HotkeyCapture(owner.GamePid, owner.GamePath,
                    launched.UtcDateTime,
                    () => Feature018InputAdmission(lifecycle!, owner, account),
                    intent => _ = DispatchFeature018HotkeyAsync(intent));
                var gameCommands = equipmentConfigService is null
                    ? new Feature018GameCommandService(lifecycle!)
                    : new Feature018GameCommandService(lifecycle!, equipmentConfigService);
                feature018Actions = new Feature018HotkeyActions(account,
                    () => ReadFeature018AuthenticatedAccount(lifecycle, owner),
                    () => capture.IsExactForegroundOwner() &&
                          Feature018InputAdmission(lifecycle, owner, account),
                    () => feature018Preferences,
                    new Feature018HotkeyNativeProviders(gameCommands).Create());
                feature018Owner = owner;
                feature018Account = account;
                feature018Capture = capture;
                capture.Start();
            }
            // The Unity render HWND cannot establish that chat text input is
            // inactive. Poll the authenticated GAME-SIDE EventSystem inspector;
            // any missing, stale, false or unsupported result disarms keys.
            if (feature018Capture?.IsExactForegroundOwner() != true)
            {
                feature018InputClear = false;
                return;
            }
            using JsonDocument empty = JsonDocument.Parse("{}");
            object? rawConfig = await hotkeyConfigService.InvokeAsync("hotkey_config_get",
                empty.RootElement.Clone(), CancellationToken.None);
            if (rawConfig is not HotkeyConfig config) return;
            feature018Preferences = config;

            var commands = new Feature018GameCommandService(lifecycle!);
            // The current game's UpdateManager pump is typically 250 ms;
            // accept a delayed reply only at its receive time, then require a
            // separate <900 ms cached-focus gate on each pressed key. The game
            // rechecks text focus synchronously before every native action.
            // Keep the last fresh sample during a query; clearing it here
            // used to disarm the recognizer between every two replies.
            using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(900));
            object? rawFocus = await commands.InvokeAsync("feature018_input_focus",
                empty.RootElement.Clone(), timeout.Token);
            if (rawFocus is not JsonElement focus ||
                !focus.TryGetProperty("inputClear", out JsonElement isClear) ||
                isClear.ValueKind != JsonValueKind.True ||
                !lifecycle.MatchesOwnedMapScanSession(owner) ||
                ReadFeature018AuthenticatedAccount(lifecycle, owner) != account)
            {
                feature018InputClear = false;
                return;
            }
            feature018InputObservedAt = DateTimeOffset.UtcNow;
            feature018InputClear = true;
        }
        catch (Exception)
        {
            feature018InputClear = false;
        }
        finally { feature018FocusPolling = false; }
    }

    private async Task DispatchFeature018HotkeyAsync(Feature018HotkeyIntent intent)
    {
        Feature018HotkeyActions? actions = feature018Actions;
        if (actions is null || sessionClosed) return;
        try
        {
            Feature018HotkeyActionOutcome result = await actions.TryDispatchAsync(intent);
            if (intent.Kind == Feature018HotkeyKind.ShieldRemaining &&
                result.Status == Feature018HotkeyActionStatus.Confirmed &&
                result.AuthoritativeObservation is { } shield &&
                feature018Capture?.IsExactForegroundOwner() == true)
            {
                // Source-backed read-only MAIN_SHIELD countdown, displayed
                // without activating LWBridge or a different window.
                _ = Feature018ShieldStatusOverlay.ShowIfCurrentOwner(
                    feature018Capture, shield);
            }
            if (feature018R2ApprovedAccount is not null &&
                feature018R2ApprovedAccount == feature018Account)
                RecordFeature018R2KeyOutcome(new(intent.Kind.ToString(), intent.Slot,
                    result.Status.ToString(), result.Reason, DateTimeOffset.UtcNow,
                    result.AuthoritativeObservation));
            // A native request being sent does not prove gameplay succeeded;
            // leave outcomes in their actual statuses for diagnostics.
            if (result.Status is Feature018HotkeyActionStatus.Failed or
                Feature018HotkeyActionStatus.Submitted)
                System.Diagnostics.Debug.WriteLine("FEATURE018 hotkey: " + result.Status + ": " + result.Reason);
        }
        catch (Exception error)
        {
            if (feature018R2ApprovedAccount is not null &&
                feature018R2ApprovedAccount == feature018Account)
                RecordFeature018R2KeyOutcome(new(intent.Kind.ToString(), intent.Slot,
                    "Exception", error.GetType().Name, DateTimeOffset.UtcNow));
        }
    }

    private void RecordFeature018R2KeyOutcome(Feature018R2KeyOutcome outcome)
    {
        feature018R2KeyOutcomes.Enqueue(outcome);
        if (normalUiLiveMapProofPath is null ||
            Environment.GetEnvironmentVariable("LWBRIDGE_FEATURE018_STAGED_PROOF") != "1" ||
            feature018R2ApprovedAccount is null ||
            feature018R2ApprovedAccount != feature018Account ||
            feature018Owner is not { } owner)
            return;
        try
        {
            string evidence = JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                game = new
                {
                    owner.ProfileId,
                    owner.SessionId,
                    owner.GamePid,
                    account = feature018Account,
                },
                captureId = feature018Capture?.CaptureId,
                capturedIntents = feature018Capture?.SnapshotCapturedIntents() ??
                    Array.Empty<Feature018CapturedIntent>(),
                outcomes = feature018R2KeyOutcomes.ToArray(),
                observedAtUtc = DateTimeOffset.UtcNow,
            }, new JsonSerializerOptions(JsonOptions.Default) { WriteIndented = true });
            lock (feature018R2KeyEvidenceGate)
                File.WriteAllText(normalUiLiveMapProofPath + ".hotkeys.json", evidence);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private void StopFeature018HotkeyMonitoring()
    {
        feature018R2AccountVerified = false;
        feature018R2ApprovedAccount = null;
        feature018HotkeyMonitor.Stop();
        feature018HotkeyMonitor.Tick -= Feature018HotkeyTick;
        RetireFeature018Hotkeys();
        feature018HotkeyMonitor.Dispose();
        StopFeature018ChestMonitoring();
    }
}
