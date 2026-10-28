using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace LWBridge.Desktop;

internal sealed partial class LWBridgeWindow : Form
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
        "bridge://local-map-auto-scan-changed",
        "bridge://hotkey-outcome",
    ];
    private static readonly HashSet<string> ProfileScopedEvents = new(StringComparer.Ordinal)
    {
        "bridge://status",
        "bridge://map-scan-status",
        "bridge://automation-status",
        "bridge://resource-automation-status",
        "bridge://game-recovery",
        "bridge://player-mark-changed",
        "bridge://local-map-auto-scan-changed",
        "bridge://hotkey-outcome",
    };

    private readonly string? capturePath;
    private readonly string? normalUiLiveMapProofPath;
    private readonly string initialView;
    private readonly string? language;
    private readonly string? theme;
    private readonly string uiRootPath;
    private readonly bool liveUiReloadAllowed;
    private LWBridgeBackend backend = null!;
    private LWBridgeControlPipeHostState? bridgeHostState;
    private OverviewLifecycleService? overviewLifecycleService;
    private Map317CommandService? map317CommandService;
    private MapAutoScanCommandService? mapAutoScanService;
    private CityLayoutDraftCommandService? cityLayoutDraftService;
    private readonly ProfileRegistryCommandService? profileRegistryService;
    private ProfileSettingsCommandService? profileSettingsService;
    private HotkeyConfigCommandService? hotkeyConfigService;
    private VisualMetricsConfigCommandService? visualMetricsConfigService;
    private EquipmentConfigCommandService? equipmentConfigService;
    private MonsterAfkConfigCommandService? monsterAfkConfigService;
    private AutomationStatusCommandService? automationStatusService;
    private ClaimDelayConfigCommandService? claimDelayConfigService;
    private ProfileRuntimeConfigStore? profileRuntimeConfigStore;
    private string? profileRuntimeConfigPath;
    private readonly SemaphoreSlim profileSwapGate = new(1, 1);
    // Runtime ownership is independent of whichever profile the Home view displays.
    // Each entry remains alive across selection changes until host shutdown.
    private readonly Dictionary<string, RetainedProfileRuntime> retainedProfileRuntimes =
        new(StringComparer.Ordinal);
    private readonly OrderedProfileReconcileCommandService? startupProfileReconcile;
    private readonly DesktopApplicationPaths? productionPaths;
    private readonly string? productionApplicationRoot;
    private readonly string? primaryProfileId;
    private readonly LWBridgeLocalConfig? primaryProfileSeed;
    private LocalConfigStore activeProfileConfig;
    private long profileRuntimeGeneration = 1;
    private Action<OverviewRecoveryStatus>? overviewRecoveryHandler;
    private Action<object>? manualMapScanStatusHandler;
    private Action? mapPlayerMarkHandler;
    private Action<MapAutoScanSnapshot>? mapAutoScanHandler;
    private readonly WindowThemeService windowThemeService = new();
    private readonly string? isolatedConfigRoot;
    private long documentGeneration = 1;
    private DocumentSession documentSession = new(1, EventAllowlist);
    private bool documentReady;
    private bool sessionClosed;
    private int profileRuntimeClosed;
    private int rejectedNavigationCount;
    private int lastClosedSubscriptionCount;
    private int lastClosedRequestCount;
    private int postedWebMessageCount;
    private readonly object normalUiProofGate = new();
    private long normalUiProofSearchSequence;
    private NormalUiResourceProofSearchObservation? normalUiProofSearchObservation;
    // F-07 per-owner admission serializes native Start/Stop against Delete
    // across the entire invocation, not merely the backend lookup. Sibling
    // profiles keep independent gates and may still operate concurrently.
    private readonly Dictionary<string, SemaphoreSlim> profileMutationAdmissionGates = new(StringComparer.Ordinal);
    private readonly WebView2 webView = new()
    {
        Dock = DockStyle.Fill,
        DefaultBackgroundColor = Color.FromArgb(245, 245, 247),
    };

    public LWBridgeWindow(
        string? capturePath,
        string initialView,
        string? language,
        string? theme,
        string? normalUiLiveMapProofPath = null,
        string? isolatedRootPath = null,
        string? testUiRootPath = null)
    {
        this.capturePath = capturePath;
        this.normalUiLiveMapProofPath = normalUiLiveMapProofPath;
        string[] views = ["overview", "automation", "map-data", "march", "city-layout", "hotkeys", "mini-games", "advanced", "settings"];
        if (!views.Contains(initialView)) throw new ArgumentException("Unknown --view: " + initialView);
        this.initialView = initialView;
        this.language = language;
        this.theme = theme;
        // testUiRootPath is a check-only seam: composition checks build the
        // window without a canonical browser build. The program never passes it.
        DesktopUiSelection uiSelection = DesktopUiContentRoot.Select(
            AppContext.BaseDirectory,
            testUiRootPath,
            allowProofOverride: testUiRootPath is not null);
        this.uiRootPath = uiSelection.RootPath;
        bool isolated = capturePath is not null;
        // Live screen reload only for the normal owner app (never proofs, captures or probes).
        liveUiReloadAllowed = !isolated && uiSelection.Mode == "canonical-production" &&
            Environment.GetEnvironmentVariable("LWBRIDGE_FEATURE018_STAGED_PROOF") != "1";
        if (isolated && isolatedRootPath is not null)
            throw new ArgumentException("--isolated-root is restricted to normal production composition.");
        productionPaths = isolated
            ? null
            : DesktopApplicationPaths.Create(isolatedRootPath);
        LocalConfigStore config;
        if (isolated)
        {
            isolatedConfigRoot = Path.Combine(
                Path.GetTempPath(),
                "lwbridge-capture-probe-" + Guid.NewGuid().ToString("N"));
            config = new LocalConfigStore(isolatedConfigRoot);
        }
        else
        {
            config = new LocalConfigStore(productionPaths!.Root);
        }
        productionApplicationRoot = isolated ? null : productionPaths!.Root;
        primaryProfileId = productionApplicationRoot is null ? null : config.Snapshot.ProfileId;
        primaryProfileSeed = productionApplicationRoot is null ? null : config.Snapshot;
        activeProfileConfig = config;
        string? controllerDatabasePath = productionApplicationRoot is null
            ? null
            : Path.Combine(productionApplicationRoot, "controller.db");
        ProfileWindowFocusService? profileWindowFocus =
            controllerDatabasePath is null
                ? null
                : new ProfileWindowFocusService(
                    config.Snapshot.ProfileId,
                    new GameInstallationService(config));
        profileRegistryService = controllerDatabasePath is null
            ? null
            : new ProfileRegistryCommandService(
                config.Snapshot.ProfileId,
                controllerDatabasePath,
                "Local Game",
                // OWN_DESIGN F-07: a local four-profile metadata ceiling is
                // independent of original commercial entitlement. A real
                // second game's compatibility is NOT established by it.
                maxProfiles: 4,
                focusProfile: profileWindowFocus is null
                    ? null
                    : profileWindowFocus.TryFocus,
                selectProfileOwner: SelectProfileOwnerAsync,
                deleteProfileUnderOwnerGate: DeleteStoppedLocalProfileAsync);
        startupProfileReconcile = profileRegistryService is null ? null :
            new OrderedProfileReconcileCommandService(
                profileRegistryService, ReconcileProfileOwnerAsync, UpdateAndRestartProfileOwnerAsync);
        string? profileDatabasePath = isolated
            ? null
            : productionPaths!.ProfileDatabasePath(config.Snapshot.ProfileId);
        cityLayoutDraftService = profileDatabasePath is null
            ? null
            : new CityLayoutDraftCommandService(
                config.Snapshot.ProfileId,
                profileDatabasePath);
        profileSettingsService = profileDatabasePath is null
            ? null
            : new ProfileSettingsCommandService(
                config.Snapshot.ProfileId,
                profileDatabasePath);
        profileRuntimeConfigPath = isolated
            ? null
            : productionPaths!.RuntimeConfigPath(config.Snapshot.ProfileId);
        profileRuntimeConfigStore =
            profileRuntimeConfigPath is null
                ? null
                : new ProfileRuntimeConfigStore(profileRuntimeConfigPath);
        hotkeyConfigService = profileRuntimeConfigStore is null
            ? null
            : new HotkeyConfigCommandService(profileRuntimeConfigStore);
        visualMetricsConfigService = profileRuntimeConfigStore is null
            ? null
            : new VisualMetricsConfigCommandService(profileRuntimeConfigStore);
        equipmentConfigService = profileRuntimeConfigStore is null
            ? null
            : new EquipmentConfigCommandService(profileRuntimeConfigStore);
        monsterAfkConfigService = profileRuntimeConfigStore is null
            ? null
            : new MonsterAfkConfigCommandService(profileRuntimeConfigStore);
        automationStatusService = profileRuntimeConfigStore is null || profileRuntimeConfigPath is null
            ? null
            : new AutomationStatusCommandService(
                profileRuntimeConfigStore,
                Path.Combine(
                    Path.GetDirectoryName(profileRuntimeConfigPath)!,
                    "automation-status.json"));
        claimDelayConfigService = profileRuntimeConfigStore is null
            ? null
            : new ClaimDelayConfigCommandService(
                profileRuntimeConfigStore);
        if (!isolated)
        {
            // Fresh host ownership must stay bound to the exact public/native-selected
            // installation. GetStatus() may deliberately fall back to another strict
            // detected installation when the saved picker root is only weak-valid.
            // That fallback is useful for legacy diagnostics, but it must never choose
            // which installation receives the lifecycle/control-pipe ownership.
            GameRootStatus liveGameRoot = new GameInstallationService(config).GetLaunchAdmissionStatus();
            // R18 isolated background proof must not contend for the owner's
            // normal app listener. Keep the same SID ACL/handshake and installation
            // ownership gates on a private, source-valid 16-hex pipe identity.
            string? isolatedProofPipe = productionPaths?.ExplicitIsolation == true &&
                Environment.GetEnvironmentVariable("LWBRIDGE_FEATURE018_BACKGROUND_ONLY") == "1"
                ? LWBridgeControlPipeContract.FullPathPrefix + Convert.ToHexString(
                    System.Security.Cryptography.SHA256.HashData(
                        System.Text.Encoding.UTF8.GetBytes(productionPaths.Root))).ToLowerInvariant()[..16]
                : null;
            bridgeHostState = new LWBridgeControlPipeHostState(isolatedProofPipe);
            if (liveGameRoot.Valid)
            {
                string expectedClientPath =
                    LWBridgeControlPipeClientPathContract
                        .BuildExpectedGameExecutablePath(liveGameRoot.Path);
                _ = bridgeHostState.StartRpcTransport(
                    OverviewLifecycleService.BridgeVersion,
                    expectedClientPath);
            }
            overviewLifecycleService = new OverviewLifecycleService(
                config.Snapshot.ProfileId,
                liveGameRoot.Valid ? liveGameRoot.Path : null,
                config: config,
                bridgeHostState: bridgeHostState,
                enableBridgeControlPipeLaunchBinding: true,
                applicationDataRoot: productionApplicationRoot,
                foreignOwnedProcess: pid => IsAnotherProfileOwnedProcess(
                    config.Snapshot.ProfileId, pid),
                foreignOwnedInstallation: root => IsAnotherProfileOwnedInstallation(
                    config.Snapshot.ProfileId, root));
            string map317DatabasePath =
                productionPaths!.MapDatabasePath(config.Snapshot.ProfileId);
            map317CommandService = new Map317CommandService(
                map317DatabasePath,
                overviewLifecycleService);
            mapAutoScanService = profileRuntimeConfigPath is null
                ? null
                : new MapAutoScanCommandService(
                    Path.Combine(
                        Path.GetDirectoryName(profileRuntimeConfigPath)!,
                        "map-auto-scan.json"),
                    new MapAutoScanExecutionBoundary
                    {
                        IsOnline = () => string.Equals(
                            overviewLifecycleService.CurrentConnectionState,
                            "connected",
                            StringComparison.Ordinal),
                        IsMapScanActive = () => map317CommandService.IsScanActive,
                        ReadStatusAsync = map317CommandService.ReadAutoScanStatusAsync,
                        StartTargetScanAsync = map317CommandService.StartAutoScanTargetAsync,
                        StartTargetScanIfStillOwnedAsync =
                            map317CommandService.StartAutoScanTargetIfStillOwnedAsync,
                        ReturnServerAsync = map317CommandService.ReturnAutoScanToServerAsync,
                        ReturnServerIfStillOwnedAsync =
                            map317CommandService.ReturnAutoScanToServerIfStillOwnedAsync,
                        StopScanIfOwnedAsync = map317CommandService.StopAutoScanIfOwnedAsync,
                    });
            if (mapAutoScanService is not null)
            {
                map317CommandService.BindAutoScanCycleBusy(() => mapAutoScanService.IsCycleBusy);
                map317CommandService.BindAutoScanClearLease(mapAutoScanService.AcquireMapClearLease);
            }
        }
        else
        {
            bridgeHostState = null;
            overviewLifecycleService = null;
            map317CommandService = null;
            mapAutoScanService = null;
        }
        INativeAsyncCommandService? productionCommands = null;
        {
            var services = new List<INativeAsyncCommandService>();
            if (profileRegistryService is not null)
                services.Add(startupProfileReconcile!);
            if (overviewLifecycleService is not null) services.Add(overviewLifecycleService);
            if (map317CommandService is not null) services.Add(map317CommandService);
            if (mapAutoScanService is not null) services.Add(mapAutoScanService);
            if (cityLayoutDraftService is not null) services.Add(cityLayoutDraftService);
            if (profileRegistryService is not null) services.Add(profileRegistryService);
            if (profileSettingsService is not null) services.Add(profileSettingsService);
            if (hotkeyConfigService is not null) services.Add(hotkeyConfigService);
            if (overviewLifecycleService is not null)
            {
                var featureCommands = equipmentConfigService is null
                    ? new Feature018GameCommandService(overviewLifecycleService)
                    : new Feature018GameCommandService(overviewLifecycleService, equipmentConfigService, profileRuntimeConfigStore);
                services.Add(featureCommands);
                if (equipmentConfigService is not null && profileRuntimeConfigStore is not null)
                    automationStatusService?.SetRuntimeStartup(Feature018RuntimeStartup.Create(
                        overviewLifecycleService, equipmentConfigService, profileRuntimeConfigStore));
                automationStatusService?.SetNativeStatusReader(async token => JsonSerializer.SerializeToNode(
                    await featureCommands.InvokeAsync("feature018_afk_status", JsonSerializer.SerializeToElement(new { }), token).ConfigureAwait(false)));
                automationStatusService?.SetNativeChatStatusReader(async token => JsonSerializer.SerializeToNode(
                    await featureCommands.InvokeAsync("feature018_chat_status", JsonSerializer.SerializeToElement(new { }), token).ConfigureAwait(false)));
            }
            if (visualMetricsConfigService is not null) services.Add(visualMetricsConfigService);
            if (equipmentConfigService is not null) services.Add(equipmentConfigService);
            if (monsterAfkConfigService is not null) services.Add(monsterAfkConfigService);
            if (profileRuntimeConfigStore is not null) services.Add(new ChatAutomationConfigCommandService(profileRuntimeConfigStore));
            if (profileRuntimeConfigStore is not null) services.Add(new AutomationTaskConfigCommandService(profileRuntimeConfigStore));
            if (automationStatusService is not null) services.Add(automationStatusService);
            if (claimDelayConfigService is not null) services.Add(claimDelayConfigService);
            productionCommands = services.Count switch
            {
                0 => null,
                1 => services[0],
                _ => new CompositeAsyncCommandService([.. services]),
            };
        }
        backend = new LWBridgeBackend(
            config,
            asyncCommands: productionCommands,
            overviewLifecycle: overviewLifecycleService,
            mapScanStatusProvider: map317CommandService is null
                ? null
                : map317CommandService.CreateStatus,
            bridgeHostState: bridgeHostState,
            runtimeTasksProvider: profileRuntimeConfigStore is null
                ? null
                : profileRuntimeConfigStore.ReadTasksSnapshot,
            profileRuntimeDirectory: profileRuntimeConfigPath is null
                ? null
                : Path.GetDirectoryName(profileRuntimeConfigPath),
            applicationDataRoot: productionApplicationRoot);
        if (profileRegistryService is not null && overviewLifecycleService is not null)
            retainedProfileRuntimes.Add(backend.ProfileId, CaptureActiveProfileRuntime());
        AttachProfileRuntimeEvents();
        if (!isolated && profileRegistryService is not null)
        {
            string selectedProfileId = profileRegistryService.Snapshot.SelectedProfileId;
            if (!string.IsNullOrWhiteSpace(selectedProfileId) &&
                !string.Equals(selectedProfileId, backend.ProfileId, StringComparison.Ordinal))
            {
                SelectProfileOwnerAsync(selectedProfileId, focusGame: false, CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
            }
        }
        Text = "lwbridge";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1120, 720);
        MinimumSize = new Size(900, 640);
        BackColor = Color.FromArgb(245, 245, 247);
        Controls.Add(webView);
        Shown += OnShown;
        FormClosed += OnFormClosed;
        StartFeature018HotkeyMonitoring();
        InitializeHome004IsolatedRouteFault(isolatedRootPath);
    }

    private async Task SelectProfileOwnerAsync(
        string profileId,
        bool focusGame,
        CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref profileRuntimeClosed) != 0 || sessionClosed)
            throw new BridgeCommandException("APP_SHUTTING_DOWN", "APP_SHUTTING_DOWN");
        if (productionApplicationRoot is null ||
            primaryProfileId is null ||
            primaryProfileSeed is null ||
            profileRegistryService is null)
        {
            throw new BridgeCommandException(
                "PROFILE_RUNTIME_UNAVAILABLE",
                "PROFILE_RUNTIME_UNAVAILABLE");
        }

        await profileSwapGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Volatile.Read(ref profileRuntimeClosed) != 0 || sessionClosed)
                throw new BridgeCommandException("APP_SHUTTING_DOWN", "APP_SHUTTING_DOWN");
            if (string.Equals(backend.ProfileId, profileId, StringComparison.Ordinal))
            {
                if (focusGame)
                {
                    new ProfileWindowFocusService(
                        profileId,
                        new GameInstallationService(activeProfileConfig))
                        .TryFocus(profileId);
                }
                return;
            }

            RetainedProfileRuntime next = GetOrCreateProfileRuntime(profileId);
            cancellationToken.ThrowIfCancellationRequested();
            if (Volatile.Read(ref profileRuntimeClosed) != 0 || sessionClosed)
                throw new BridgeCommandException("APP_SHUTTING_DOWN", "APP_SHUTTING_DOWN");

            // A view selection has no authority to retire another profile's
            // process, recovery loop, lease or route. Only UI event subscriptions
            // and the selected backend change; the owner remains in the registry.
            DetachProfileRuntimeEvents();
            Interlocked.Increment(ref profileRuntimeGeneration);
            ActivateProfileRuntime(next);
            AttachProfileRuntimeEvents();
            if (focusGame)
                new ProfileWindowFocusService(profileId,
                    new GameInstallationService(next.Config)).TryFocus(profileId);
        }
        finally
        {
            profileSwapGate.Release();
        }
    }

    private async Task DeleteStoppedLocalProfileAsync(
        string profileId, Action deleteRegistryRow, CancellationToken cancellationToken)
    {
        // Both owner resolution for Start and profile deletion take this gate.
        // A removed profile cannot race a newly admitted lifecycle Start.
        await profileSwapGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref profileRuntimeClosed) != 0 || sessionClosed)
                throw new BridgeCommandException("APP_SHUTTING_DOWN", "APP_SHUTTING_DOWN");
            ProfileRegistryEntry? entry = profileRegistryService?.Snapshot.Profiles.FirstOrDefault(
                item => string.Equals(item.Id, profileId, StringComparison.Ordinal));
            if (entry is null)
                throw new BridgeCommandException("PROFILE_NOT_FOUND", "PROFILE_NOT_FOUND");
            if (entry.IsPrimary)
                throw new BridgeCommandException("PROFILE_PRIMARY_FIXED", "The primary local profile cannot be deleted.");
            if (string.Equals(profileRegistryService!.Snapshot.SelectedProfileId, profileId, StringComparison.Ordinal))
                throw new BridgeCommandException("PROFILE_SELECTED", "Select another profile before deleting this one.");

            RetainedProfileRuntime runtime = GetOrCreateProfileRuntime(profileId);
            if (!runtime.Lifecycle.CanRetireStoppedLocalProfile() ||
                runtime.Lifecycle.RuntimeManaged || runtime.Lifecycle.RepairRequired ||
                !string.Equals(runtime.Lifecycle.CurrentRecoveryStatus.State, "idle", StringComparison.Ordinal))
                throw new BridgeCommandException("PROFILE_STILL_ACTIVE",
                    "Stop this profile and complete its recovery/restoration before deleting it.");
            deleteRegistryRow(); // The exact validation and mutation share the gate.
            // Close the now-unreachable stopped owner's monitor/services. Do
            // not delete its files: verified backups remain owner evidence.
            RetainedProfileRuntime? removed = null;
            lock (retainedProfileRuntimes)
                retainedProfileRuntimes.Remove(profileId, out removed);
            if (removed is not null)
            {
                removed.AutomationStatus?.Dispose();
                DisposeRetiredProfileRuntime(removed.Auto, removed.Map,
                    removed.Drafts, removed.Settings, removed.Lifecycle, bridgeHost: null);
            }
        }
        finally { profileSwapGate.Release(); }
    }

    private SemaphoreSlim GetProfileMutationAdmissionGate(string profileId)
    {
        lock (profileMutationAdmissionGates)
        {
            if (!profileMutationAdmissionGates.TryGetValue(profileId, out SemaphoreSlim? gate))
                profileMutationAdmissionGates[profileId] = gate = new SemaphoreSlim(1, 1);
            return gate;
        }
    }

    private async Task<object?> ReconcileProfileOwnerAsync(
        string profileId, JsonElement payload, CancellationToken cancellationToken)
    {
        SemaphoreSlim ownerAdmission = GetProfileMutationAdmissionGate(profileId);
        await ownerAdmission.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
        OverviewLifecycleService lifecycle;
        await profileSwapGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref profileRuntimeClosed) != 0 || sessionClosed)
                throw new BridgeCommandException("APP_SHUTTING_DOWN", "APP_SHUTTING_DOWN");
            lifecycle = GetOrCreateProfileRuntime(profileId).Lifecycle;
        }
        finally { profileSwapGate.Release(); }
        return await lifecycle.InvokeAsync("profile_instances_reconcile", payload, cancellationToken)
            .ConfigureAwait(false);
        }
        finally { ownerAdmission.Release(); }
    }

    private async Task<object?> UpdateAndRestartProfileOwnerAsync(
        string profileId, JsonElement payload, CancellationToken cancellationToken)
    {
        SemaphoreSlim ownerAdmission = GetProfileMutationAdmissionGate(profileId);
        await ownerAdmission.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
        OverviewLifecycleService lifecycle;
        await profileSwapGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref profileRuntimeClosed) != 0 || sessionClosed)
                throw new BridgeCommandException("APP_SHUTTING_DOWN", "APP_SHUTTING_DOWN");
            lifecycle = GetOrCreateProfileRuntime(profileId).Lifecycle;
        }
        finally { profileSwapGate.Release(); }
        RecordHome004F06Dispatch(
            lifecycle.RepairRequired ? "owner-journal-valid" : "owner-journal-unavailable",
            "profile_instances_update_and_restart");
        object? repairResponse = await lifecycle.InvokeAsync(
            "profile_instances_update_and_restart", payload, cancellationToken).ConfigureAwait(false);
        if (home004F06DispatchPath is not null)
        {
            JsonElement parsed = JsonSerializer.SerializeToElement(repairResponse, JsonOptions.Default);
            string code = parsed.ValueKind == JsonValueKind.Object &&
                parsed.TryGetProperty("errors", out JsonElement errors) &&
                errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0 &&
                errors[0].TryGetProperty("error", out JsonElement error)
                ? error.GetString() ?? "error-unavailable" : "none";
            int successful = parsed.ValueKind == JsonValueKind.Object &&
                parsed.TryGetProperty("restarted", out JsonElement restarted) &&
                restarted.ValueKind == JsonValueKind.Array ? restarted.GetArrayLength() : -1;
            RecordHome004F06Dispatch($"owner-result:restarted={successful}:error={code}",
                "profile_instances_update_and_restart");
        }
        return repairResponse;
        }
        finally { ownerAdmission.Release(); }
    }

    // Must be invoked under profileSwapGate after initial composition.
    private RetainedProfileRuntime GetOrCreateProfileRuntime(string profileId)
    {
        lock (retainedProfileRuntimes)
        {
            if (retainedProfileRuntimes.TryGetValue(profileId, out RetainedProfileRuntime? existing))
                return existing;
        }
        using ProfileRuntimeOwner owner = BuildProfileRuntimeReplacement(profileId);
        var runtime = new RetainedProfileRuntime(
            owner.Config, owner.Backend, owner.BridgeHostState, owner.OverviewLifecycle,
            owner.Map317, owner.MapAutoScan, owner.CityLayoutDraft, owner.ProfileSettings,
            owner.RuntimeConfigStore, owner.RuntimeConfigPath, owner.HotkeyConfig,
            owner.VisualMetricsConfig, owner.EquipmentConfig, owner.MonsterAfkConfig,
            owner.AutomationStatus, owner.ClaimDelayConfig);
        lock (retainedProfileRuntimes)
            retainedProfileRuntimes.Add(profileId, runtime);
        owner.TransferOwnership();
        return runtime;
    }

    private static bool IsExplicitHomeOwnerCommand(string command) =>
        command is "profile_instance_status" or "profile_instance_start" or "profile_instance_stop";

    private async Task<LWBridgeBackend> ResolveHomeCommandBackendAsync(
        string command, JsonElement payload, LWBridgeBackend selectedBackend,
        CancellationToken cancellationToken)
    {
        if (!IsExplicitHomeOwnerCommand(command) ||
            payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty("profileId", out JsonElement ownerElement) ||
            ownerElement.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(ownerElement.GetString()))
            return selectedBackend; // Preserve the selected backend's original payload validation.

        string ownerId = ownerElement.GetString()!;
        if (profileRegistryService is null || productionApplicationRoot is null)
            return selectedBackend; // Existing isolated/single-profile backend rejection.

        await profileSwapGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref profileRuntimeClosed) != 0 || sessionClosed)
                throw new BridgeCommandException("APP_SHUTTING_DOWN", "APP_SHUTTING_DOWN");
            ProfileRegistryCommandService.ValidateProfileId(ownerId);
            ProfileRegistryEntry? entry = profileRegistryService.Snapshot.Profiles.FirstOrDefault(
                item => string.Equals(item.Id, ownerId, StringComparison.Ordinal));
            if (entry is null)
                throw new BridgeCommandException("PROFILE_NOT_FOUND", "PROFILE_NOT_FOUND");
            if (command == "profile_instance_start" && (!entry.Enabled || entry.LockedReason is not null))
                throw new BridgeCommandException("PROFILE_LOCKED", "PROFILE_LOCKED");
            if (string.Equals(ownerId, selectedBackend.ProfileId, StringComparison.Ordinal))
                return selectedBackend;
            // A retained owner owns its own PID, journal, recovery and lifecycle.
            // Selecting a profile for display never participates in this lookup.
            return GetOrCreateProfileRuntime(ownerId).Backend;
        }
        finally { profileSwapGate.Release(); }
    }

    private bool IsAnotherProfileOwnedProcess(string ownerId, int pid)
    {
        RetainedProfileRuntime[] others;
        lock (retainedProfileRuntimes)
            others = retainedProfileRuntimes.Where(entry =>
                    !string.Equals(entry.Key, ownerId, StringComparison.Ordinal))
                .Select(entry => entry.Value).ToArray();
        return others.Any(runtime => runtime.Lifecycle.OwnsExactProcess(pid));
    }

    private bool IsAnotherProfileOwnedInstallation(string ownerId, string root)
    {
        RetainedProfileRuntime[] others;
        lock (retainedProfileRuntimes)
            others = retainedProfileRuntimes.Where(entry =>
                    !string.Equals(entry.Key, ownerId, StringComparison.Ordinal))
                .Select(entry => entry.Value).ToArray();
        // The package installed by run_overview_bridge.py is located under
        // this Windows user's LocalLow profile for every game root. Until an
        // independently verified per-installation bridge exists, a different
        // root is not a different mutable script owner.
        return others.Any(runtime =>
            runtime.Lifecycle.HasCapturedInstallation(root) ||
            runtime.Lifecycle.HasCapturedSharedLuaPackage());
    }

    private sealed record RetainedProfileRuntime(
        LocalConfigStore Config,
        LWBridgeBackend Backend,
        LWBridgeControlPipeHostState? BridgeHost,
        OverviewLifecycleService Lifecycle,
        Map317CommandService? Map,
        MapAutoScanCommandService? Auto,
        CityLayoutDraftCommandService? Drafts,
        ProfileSettingsCommandService? Settings,
        ProfileRuntimeConfigStore? RuntimeConfig,
        string? RuntimeConfigPath,
        HotkeyConfigCommandService? Hotkeys,
        VisualMetricsConfigCommandService? VisualMetrics,
        EquipmentConfigCommandService? Equipment,
        MonsterAfkConfigCommandService? MonsterAfk,
        AutomationStatusCommandService? AutomationStatus,
        ClaimDelayConfigCommandService? ClaimDelay);

    private RetainedProfileRuntime CaptureActiveProfileRuntime() => new(
        activeProfileConfig, backend, bridgeHostState, overviewLifecycleService!,
        map317CommandService, mapAutoScanService, cityLayoutDraftService,
        profileSettingsService, profileRuntimeConfigStore, profileRuntimeConfigPath,
        hotkeyConfigService, visualMetricsConfigService, equipmentConfigService,
        monsterAfkConfigService,
        automationStatusService, claimDelayConfigService);

    private void ActivateProfileRuntime(RetainedProfileRuntime runtime)
    {
        activeProfileConfig = runtime.Config;
        backend = runtime.Backend;
        bridgeHostState = runtime.BridgeHost;
        overviewLifecycleService = runtime.Lifecycle;
        map317CommandService = runtime.Map;
        mapAutoScanService = runtime.Auto;
        cityLayoutDraftService = runtime.Drafts;
        profileSettingsService = runtime.Settings;
        profileRuntimeConfigStore = runtime.RuntimeConfig;
        profileRuntimeConfigPath = runtime.RuntimeConfigPath;
        hotkeyConfigService = runtime.Hotkeys;
        visualMetricsConfigService = runtime.VisualMetrics;
        equipmentConfigService = runtime.Equipment;
        monsterAfkConfigService = runtime.MonsterAfk;
        automationStatusService = runtime.AutomationStatus;
        claimDelayConfigService = runtime.ClaimDelay;
    }

    private LocalConfigStore CreateProfileConfigStore(string profileId)
    {
        if (productionApplicationRoot is null ||
            primaryProfileId is null ||
            primaryProfileSeed is null)
        {
            throw new InvalidOperationException("Production profile storage is unavailable.");
        }

        if (string.Equals(profileId, primaryProfileId, StringComparison.Ordinal))
            return new LocalConfigStore(productionApplicationRoot);

        string configRoot = Path.Combine(
            productionApplicationRoot,
            "profiles",
            profileId,
            "local-config");
        LWBridgeLocalConfig seed = primaryProfileSeed with
        {
            ProfileId = profileId,
            GameDesiredRunning = false,
        };
        return new LocalConfigStore(configRoot, initialValue: seed);
    }

    private ProfileRuntimeOwner BuildProfileRuntimeReplacement(string profileId)
    {
        if (productionApplicationRoot is null || profileRegistryService is null)
            throw new InvalidOperationException("Production profile runtime is unavailable.");

        LocalConfigStore config = CreateProfileConfigStore(profileId);
        string profileRoot = Path.Combine(productionApplicationRoot, "profiles", profileId);
        return ProfileRuntimeOwner.Create(
            profileId,
            config,
            profileRoot,
            profileRegistryService,
            sharedBridgeHostState: bridgeHostState,
            // Python and the injected Lua derive every Overview path from this
            // same application root. Sibling runtime overrides split the host
            // registration/lease from the game-side reader for selected profiles.
            applicationDataRoot: Path.Combine(profileRoot, "current-client"),
            startupReconcile: startupProfileReconcile,
            foreignOwnedProcess: pid => IsAnotherProfileOwnedProcess(profileId, pid),
            foreignOwnedInstallation: root => IsAnotherProfileOwnedInstallation(profileId, root));
    }

    private void AttachProfileRuntimeEvents()
    {
        long generation = Volatile.Read(ref profileRuntimeGeneration);
        if (overviewLifecycleService is not null)
        {
            overviewRecoveryHandler = status =>
                OnOverviewRecoveryStatusChanged(generation, status);
            overviewLifecycleService.RecoveryStatusChanged += overviewRecoveryHandler;
        }
        if (map317CommandService is not null)
        {
            manualMapScanStatusHandler = status =>
                OnManualMapScanStatusChanged(generation, status);
            mapPlayerMarkHandler = () => OnMap317PlayerMarkChanged(generation);
            map317CommandService.ScanStatusChanged += manualMapScanStatusHandler;
            map317CommandService.PlayerMarkChanged += mapPlayerMarkHandler;
        }
        if (mapAutoScanService is not null)
        {
            mapAutoScanHandler = snapshot => OnMapAutoScanStateChanged(generation, snapshot);
            mapAutoScanService.StateChanged += mapAutoScanHandler;
        }
    }

    private void DetachProfileRuntimeEvents()
    {
        if (overviewLifecycleService is not null && overviewRecoveryHandler is not null)
            overviewLifecycleService.RecoveryStatusChanged -= overviewRecoveryHandler;
        if (map317CommandService is not null)
        {
            if (manualMapScanStatusHandler is not null)
                map317CommandService.ScanStatusChanged -= manualMapScanStatusHandler;
            if (mapPlayerMarkHandler is not null)
                map317CommandService.PlayerMarkChanged -= mapPlayerMarkHandler;
        }
        if (mapAutoScanService is not null && mapAutoScanHandler is not null)
            mapAutoScanService.StateChanged -= mapAutoScanHandler;
        overviewRecoveryHandler = null;
        manualMapScanStatusHandler = null;
        mapPlayerMarkHandler = null;
        mapAutoScanHandler = null;
    }

    private bool IsCurrentProfileRuntimeGeneration(long generation) =>
        generation == Volatile.Read(ref profileRuntimeGeneration);

    private static void DisposeRetiredProfileRuntime(
        MapAutoScanCommandService? auto,
        Map317CommandService? map,
        CityLayoutDraftCommandService? drafts,
        ProfileSettingsCommandService? settings,
        OverviewLifecycleService? lifecycle,
        LWBridgeControlPipeHostState? bridgeHost)
    {
        try { auto?.Dispose(); } catch { }
        try { map?.Dispose(); } catch { }
        try { drafts?.Dispose(); } catch { }
        try { settings?.Dispose(); } catch { }
        try { lifecycle?.Close(); } catch { }
        try { bridgeHost?.Close(); } catch { }
    }

    private async void OnShown(object? sender, EventArgs e)
    {
        try
        {
            if (normalUiLiveMapProofPath is not null)
                await File.WriteAllTextAsync(normalUiLiveMapProofPath + ".progress.txt", "WebView initialization started");
            string webViewMode = capturePath is not null ? "Capture" : "Presentation";
            string userDataDirectory = isolatedConfigRoot is not null
                ? Path.Combine(isolatedConfigRoot, webViewMode)
                : productionPaths is not null
                    ? productionPaths.WebViewUserDataRoot(webViewMode)
                    : Path.Combine(DesktopApplicationPaths.DefaultRoot, webViewMode);
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataDirectory);
            await webView.EnsureCoreWebView2Async(environment);
            var core = webView.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            string bootstrapJson = JsonSerializer.Serialize(
                backend.GetBootstrap(
                    capturePath is not null,
                    documentSession.Id),
                JsonOptions.Default);
            await core.AddScriptToExecuteOnDocumentCreatedAsync(
                "window.__LWBridgeBootstrap=" + bootstrapJson + ";" +
                "(()=>{try{const s=new URL(location.href).searchParams.get('nativeSession');if(s)window.__LWBridgeBootstrap.sessionId=s;}catch{}})();");
            if (capturePath is not null)
                await core.AddScriptToExecuteOnDocumentCreatedAsync("""
                    window.__LWBridgeCaptureErrors = [];
                    window.addEventListener('error', event => window.__LWBridgeCaptureErrors.push(String(event.message)));
                    window.addEventListener('unhandledrejection', event => window.__LWBridgeCaptureErrors.push(String(event.reason)));
                    """);
            core.SetVirtualHostNameToFolderMapping("lwbridge.local", uiRootPath, CoreWebView2HostResourceAccessKind.DenyCors);
            StartLiveUiReload(core);
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
            if (normalUiLiveMapProofPath is not null)
            {
                if (productionPaths?.ExplicitIsolation == true &&
                    Environment.GetEnvironmentVariable("LWBRIDGE_STARTUP_HOME_PROOF") == "1")
                {
                    await RunStartupHomeProofAsync(core, normalUiLiveMapProofPath);
                    Close();
                    return;
                }
                if (!string.Equals(initialView, "map-data", StringComparison.Ordinal))
                    throw new InvalidOperationException("--normal-ui-live-map-proof requires --view map-data.");
                await RunNormalUiProductionMapProofAsync(core, normalUiLiveMapProofPath);
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
                string diagnostics = await core.ExecuteScriptAsync("JSON.stringify({view:new URL(location.href).searchParams.get('view'),mode:window.__LWBridgeBootstrap.mode,homeRendered:!!document.querySelector('.quick-actions-panel'),errors:window.__LWBridgeCaptureErrors,text:document.body.innerText})");
                Directory.CreateDirectory(Path.GetDirectoryName(capturePath)!);
                await File.WriteAllTextAsync(Path.ChangeExtension(capturePath, ".json"), JsonSerializer.Deserialize<string>(diagnostics));
                await using (var output = File.Create(capturePath))
                    await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, output);
                Close();
            }
        }
        catch (Exception ex)
        {
            if (capturePath is null && normalUiLiveMapProofPath is null)
                MessageBox.Show(this, ex.Message, "LWBridge", MessageBoxButtons.OK, MessageBoxIcon.Error);
            else
            {
                string artifactPath = capturePath ?? normalUiLiveMapProofPath!;
                Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);
                await File.WriteAllTextAsync(artifactPath + ".error.txt", ex.ToString());
            }
            Environment.ExitCode = 1;
            if (!IsDisposed) Close();
        }
    }

    internal static string[] ReadNormalUiProofSelectedTypes(JsonElement completedStatus)
    {
        // Receipt metadata must describe the completed native scan, not the
        // proof's usual six-kind selection or its current environment flags.
        if (!completedStatus.TryGetProperty("selectedTypes", out JsonElement selection) ||
            selection.ValueKind != JsonValueKind.Array || selection.GetArrayLength() == 0)
            throw new InvalidDataException("Completed packaged Map proof has no actual scan selection.");
        var kinds = new List<string>();
        foreach (JsonElement item in selection.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String ||
                item.GetString() is not { } kind || !MapScanContract.AllTypes.Contains(kind) ||
                kinds.Contains(kind, StringComparer.Ordinal))
                throw new InvalidDataException("Completed packaged Map proof has an invalid actual scan selection.");
            kinds.Add(kind);
        }
        return kinds.ToArray();
    }

    // R8-066: end-to-end proof of the normal production Map path. It keeps the
    // actual Map317CommandService wired into the recovered WebView and only
    // automates the same UI controls a user would press.
    private async Task RunNormalUiProductionMapProofAsync(
        CoreWebView2 core,
        string outputPath)
    {
        if (map317CommandService is null || overviewLifecycleService is null)
            throw new InvalidOperationException("The normal Map Data window does not have the production scan/lifecycle services.");

        string fullOutputPath = Path.GetFullPath(outputPath);
        string directory = Path.GetDirectoryName(fullOutputPath)!;
        Directory.CreateDirectory(directory);
        bool ownerFixNativeProof = normalUiLiveMapProofPath is not null && productionPaths?.ExplicitIsolation == true &&
            Environment.GetEnvironmentVariable("LWBRIDGE_MAP014_NATIVE_UI") == "1";
        bool ownerFixRemoteProof = ownerFixNativeProof &&
            Environment.GetEnvironmentVariable("LWBRIDGE_MAP014_REMOTE_UI") == "1";
        string? qualifiedKind = ownerFixNativeProof && !ownerFixRemoteProof
            ? Environment.GetEnvironmentVariable("LWBRIDGE_MAP014_SELECTION_UI") : null;
        if (string.IsNullOrEmpty(qualifiedKind)) qualifiedKind = null;
        if (qualifiedKind is not (null or "city" or "other" or "railway" or "all-nontruck" or "truck" or "all-nine" or "all-ten" or "truck-stop"))
            throw new InvalidOperationException("MAP014 packaged selection proof requested an unsupported kind.");
        string desiredSelection = qualifiedKind switch
        {
            "city" => "index === 0",
            "other" => "index === 8",
            "railway" => "index === 4",
            "truck" or "truck-stop" => "index === 3",
            "all-nine" => "index !== 8",
            "all-ten" => "true",
            // Opt-in MAP-015 genuine direct AOI proof: every map kind except
            // moving Truck and seasonal Other, in the recovered UI tab order.
            "all-nontruck" => "index !== 3 && index !== 8",
            _ when ownerFixNativeProof && !ownerFixRemoteProof && Environment.GetEnvironmentVariable("LWBRIDGE_MAP014_NEW_KINDS_UI") == "1" => "index < 7 || index === 9",
            _ when ownerFixNativeProof && !ownerFixRemoteProof => "index < 6",
            _ => "index === 1",
        };

        bool controlsReady = false;
        for (int attempt = 0; attempt < 150; attempt++)
        {
            if (await core.ExecuteScriptAsync(
                "document.querySelectorAll('.panel.map-panel .map-header .map-category-menu input[type=checkbox]').length === 10 && !!document.querySelector('.map-header .map-actions button.primary')") == "true")
            {
                controlsReady = true;
                break;
            }
            await Task.Delay(100);
        }
        if (!controlsReady)
            throw new InvalidOperationException("The normal production Map Data manual scan controls did not render.");

        string selectionResult = await core.ExecuteScriptAsync("""
            (() => {
              const boxes = [...document.querySelectorAll('.panel.map-panel .map-header .map-category-menu input[type=checkbox]')];
              if (boxes.length !== 10) return false;
              boxes.forEach((box, index) => {
                const shouldBeChecked = DESIRED_SELECTION;
                if (box.checked !== shouldBeChecked) box.click();
              });
              return true;
            })()
            """.Replace("DESIRED_SELECTION", desiredSelection, StringComparison.Ordinal));
        if (selectionResult != "true")
            throw new InvalidOperationException("The normal production Map Data resource-only selection could not be established.");
        bool selectionSettled = false;
        for (int attempt = 0; attempt < 50; attempt++)
        {
            if (await core.ExecuteScriptAsync("(()=>{const boxes=[...document.querySelectorAll('.panel.map-panel .map-header .map-category-menu input[type=checkbox]')];return boxes.length===10 && boxes.every((box,index)=>box.checked === (DESIRED_SELECTION));})()".Replace("DESIRED_SELECTION", desiredSelection, StringComparison.Ordinal)) == "true")
            { selectionSettled = true; break; }
            await Task.Delay(50);
        }
        if (!selectionSettled) throw new InvalidDataException("Actual production Resource selection did not settle.");

        // R29 runs its genuine disconnected production matrix before this proof
        // owns any game process. The harness then clicks the production Home
        // Start control; the ordinary lifecycle loop below must still observe
        // and authenticate that exact instance before connected work is admitted.
        if (ownerFixNativeProof && !ownerFixRemoteProof &&
            Environment.GetEnvironmentVariable("LWBRIDGE_R29_SWEEP_PROOF") == "1")
        {
            using var disconnectedSweepCts = new CancellationTokenSource(TimeSpan.FromMinutes(4));
            await RunFeature018R29DisconnectedSweepAsync(core, directory, disconnectedSweepCts.Token);
        }

        // MAP-016 always chooses the fastest qualified scanner. The speed radio
        // group is gone, so verify the real owner UI reflects its removal.
        if (await core.ExecuteScriptAsync("document.querySelector('[name=map-scan-speed]') === null") != "true")
            throw new InvalidDataException("MAP016 packaged Map exposed a retired scan-speed selector.");

        bool intervalAcceptance = ownerFixNativeProof && !ownerFixRemoteProof &&
            Environment.GetEnvironmentVariable("LWBRIDGE_MAP016_AUTO_INTERVAL") == "1";
        // The staged FEATURE018 queue intentionally permits 13 minutes after
        // authentication; its outer lifecycle timeout must not cancel it at 8.
        bool feature018StagedProof = ownerFixNativeProof && !ownerFixRemoteProof &&
            Environment.GetEnvironmentVariable("LWBRIDGE_FEATURE018_UI_PROOF") == "1" &&
            Environment.GetEnvironmentVariable("LWBRIDGE_FEATURE018_STAGED_PROOF") == "1";
        using var operationCts = new CancellationTokenSource(TimeSpan.FromMinutes(
            Environment.GetEnvironmentVariable("LWBRIDGE_R29_PERF_PROOF") == "1" ? 45 :
            intervalAcceptance ? 28 : ownerFixRemoteProof ? 14 : feature018StagedProof ? 16 : 8));
        string? instanceId = null;
        OverviewMapScanSession? ownedMapSession = null;
        JsonElement startJson = default;
        try
        {
            // The recovered Overview UI may already be reconciling startup while this
            // proof begins. Observe that app-owned lifecycle first; only issue Start
            // after the service is stably stopped so we never race normal startup.
            int stableNoInstancePolls = 0;
            bool connected = false;
            for (int attempt = 0; attempt < 2400; attempt++)
            {
                operationCts.Token.ThrowIfCancellationRequested();
                object? nativeStatus = overviewLifecycleService.CreateProfileInstanceStatus();
                if (nativeStatus is null)
                {
                    stableNoInstancePolls++;
                    if (stableNoInstancePolls >= 20)
                    {
                        using JsonDocument empty = JsonDocument.Parse("{}");
                        await overviewLifecycleService.InvokeAsync(
                            "profile_instance_start", empty.RootElement.Clone(), operationCts.Token);
                        object? startedNativeStatus = overviewLifecycleService.CreateProfileInstanceStatus();
                        if (startedNativeStatus is null)
                            throw new InvalidDataException("Production UI proof Start completed without a native instance record.");
                        startJson = JsonSerializer.SerializeToElement(startedNativeStatus, JsonOptions.Default);
                        instanceId = ReadStatusString(startJson, "instanceId");
                        string? startedConnectionState = ReadStatusString(startJson, "connectionState");
                        if (!string.Equals(startedConnectionState, "connected", StringComparison.Ordinal) ||
                            string.IsNullOrWhiteSpace(instanceId))
                            throw new InvalidDataException(
                                $"Production UI proof game lifecycle did not reach connected state: {startedConnectionState ?? "null"}.");
                        connected = true;
                        break;
                    }
                }
                else
                {
                    stableNoInstancePolls = 0;
                    startJson = JsonSerializer.SerializeToElement(nativeStatus, JsonOptions.Default);
                    string phase = ReadStatusString(startJson, "phase") ?? string.Empty;
                    string? connectionState = ReadStatusString(startJson, "connectionState");
                    instanceId = ReadStatusString(startJson, "instanceId");
                    string? lifecycleError = ReadStatusString(startJson, "lastError");

                    if (phase == "running" &&
                        string.Equals(connectionState, "connected", StringComparison.Ordinal) &&
                        !string.IsNullOrWhiteSpace(instanceId))
                    {
                        connected = true;
                        break;
                    }

                    if (!string.IsNullOrWhiteSpace(lifecycleError))
                        throw new InvalidOperationException("Production UI lifecycle entered error: " + lifecycleError);
                }
                await Task.Delay(100, operationCts.Token);
            }
            if (!connected || string.IsNullOrWhiteSpace(instanceId))
                throw new TimeoutException("The normal production UI lifecycle did not reach connected state within the proof bound.");
            ownedMapSession = overviewLifecycleService.GetReadyMapScanSession()
                ?? throw new InvalidDataException("The normal production UI lifecycle reached connected state without a ready owned Map session.");

            if (ownerFixNativeProof && !ownerFixRemoteProof &&
                Environment.GetEnvironmentVariable("LWBRIDGE_FEATURE018_UI_PROOF") == "1")
            {
                await RunFeature018PackagedUiProofAsync(core, fullOutputPath, directory,
                    ownedMapSession, operationCts.Token);
                return;
            }
            if (ownerFixNativeProof && !ownerFixRemoteProof &&
                !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LWBRIDGE_MAP017_UI_MODE")))
            {
                await RunMap017UiProofAsync(core, fullOutputPath, directory, ownedMapSession, operationCts.Token);
                return;
            }

            if (intervalAcceptance)
            {
                await RunMap016AutoIntervalUiProofAsync(
                    core, fullOutputPath, directory, ownedMapSession, operationCts.Token);
                return;
            }

            bool autoAllTenProof = Environment.GetEnvironmentVariable("LWBRIDGE_MAP016_AUTO_ALL_TEN") == "1";
            if (ownerFixNativeProof && !ownerFixRemoteProof &&
                (autoAllTenProof || Environment.GetEnvironmentVariable("LWBRIDGE_MAP016_AUTO_ALL_NINE") == "1"))
            {
                await RunMap016AutoAllNineUiProofAsync(
                    core, fullOutputPath, directory, ownedMapSession, operationCts.Token,
                    includeOther: autoAllTenProof);
                return;
            }
            if (ownerFixNativeProof && !ownerFixRemoteProof &&
                Environment.GetEnvironmentVariable("LWBRIDGE_MAP016_SAVED_CLEAR_REOPEN") == "1")
            {
                await RunMap016SavedClearReopenProofAsync(core, directory, operationCts.Token);
                await File.WriteAllTextAsync(fullOutputPath, JsonSerializer.Serialize(new {
                    state = "proven", proof = "real_map016_saved_multi_server_clear_reopen",
                    gamePid = ownedMapSession.GamePid,
                    profileId = ownedMapSession.ProfileId,
                    generatedAt = DateTimeOffset.UtcNow,
                }, JsonOptions.Default), operationCts.Token);
                return;
            }

            if (ownerFixRemoteProof && Environment.GetEnvironmentVariable("LWBRIDGE_MAP014_REMOTE_SAVED_HOME") == "1")
            {
                await RunMap014NativeRemoteUiProofAsync(core, directory, operationCts.Token);
                bool remoteRailway = Environment.GetEnvironmentVariable("LWBRIDGE_MAP016_REMOTE_RAILWAY") == "1";
                await File.WriteAllTextAsync(fullOutputPath, JsonSerializer.Serialize(new {
                    state="proven", proof=remoteRailway
                        ? "real_native_auto_remote_railway_2211_fast_625_return"
                        : "real_native_auto_remote_scan_row_jump_and_clear",
                    newHomeMapScans=0,
                    newRemoteMapScans=remoteRailway || Environment.GetEnvironmentVariable("LWBRIDGE_MAP014_REMOTE_REUSE")!="1" ? 1 : 0,
                    gamePid=ownedMapSession.GamePid,
                    profileId=ownedMapSession.ProfileId, generatedAt=DateTimeOffset.UtcNow
                },JsonOptions.Default),operationCts.Token);
                return;
            }
            if (ownerFixNativeProof && Environment.GetEnvironmentVariable("LWBRIDGE_MAP014_BROWSE_ONLY") == "1")
            {
                await RunMap014NativeUiExtrasAsync(core, directory, operationCts.Token);
                await File.WriteAllTextAsync(fullOutputPath, JsonSerializer.Serialize(new {
                    state="proven", proof="real_native_ui_browse_existing_genuine_dataset",
                    newMapScans=0, gamePid=ownedMapSession.GamePid, profileId=ownedMapSession.ProfileId,
                    sourceData="preserved completed genuine MAP014 scan", generatedAt=DateTimeOffset.UtcNow
                },JsonOptions.Default),operationCts.Token);
                return;
            }

            JsonElement initialStatus = JsonSerializer.SerializeToElement(map317CommandService.CreateStatus(), JsonOptions.Default);
            string initialRunId = ReadStatusString(initialStatus, "scanRunId") ?? string.Empty;
            JsonElement completedStatus = default;
            bool completed = false;

            string clickResult = "false";
            for (int attempt = 0; attempt < 150; attempt++)
            {
                clickResult = await core.ExecuteScriptAsync("""
                    (() => {
                      const button = document.querySelector('.map-header .map-actions button.primary');
                      if (!button || button.disabled) return false;
                      button.click();
                      return true;
                    })()
                    """);
                if (clickResult == "true") break;
                await Task.Delay(100, operationCts.Token);
            }
            if (clickResult != "true")
                throw new InvalidOperationException("The normal production Map Data Start Reading button could not be clicked.");

            if (qualifiedKind == "truck-stop")
            {
                // Wait for this owned sweep to enter native acquisition before
                // using the real Stop command. The correlated native cancel
                // receipt and durable DB status are inspected by the audit.
                string startedPath = Path.Combine(overviewLifecycleService.LiveResourceRuntimeRoot,
                    "fast-truck-scan-started.json");
                JsonElement startedStatus = default;
                JsonElement nativeStarted = default;
                string stopRunId = string.Empty;
                for (int attempt = 0; attempt < 200; attempt++)
                {
                    operationCts.Token.ThrowIfCancellationRequested();
                    JsonElement status = JsonSerializer.SerializeToElement(map317CommandService.CreateStatus(), JsonOptions.Default);
                    string liveId = ReadStatusString(status, "scanRunId") ?? string.Empty;
                    if (liveId.Length > 0 && liveId != initialRunId &&
                        status.TryGetProperty("isReading", out var active) && active.ValueKind == JsonValueKind.True &&
                        File.Exists(startedPath))
                    {
                        try
                        {
                            using JsonDocument nativeDocument = JsonDocument.Parse(
                                await File.ReadAllTextAsync(startedPath, operationCts.Token));
                            JsonElement marker = nativeDocument.RootElement;
                            if (ReadStatusString(marker, "scanRunId") == liveId &&
                                ReadStatusString(marker, "profileId") == ownedMapSession!.ProfileId &&
                                ReadStatusString(marker, "launchSessionId") == ownedMapSession.SessionId &&
                                ReadStatusInt32(marker, "gamePid") == ownedMapSession.GamePid &&
                                ReadStatusInt32(marker, "requestsSent") == 20 &&
                                marker.TryGetProperty("nativeActive", out var nativeActive) &&
                                nativeActive.ValueKind == JsonValueKind.True)
                            { stopRunId = liveId; startedStatus = status; nativeStarted = marker.Clone(); break; }
                        }
                        catch (IOException) { } // Atomic native JSON may be crossing this poll.
                        catch (JsonException) { }
                    }
                    await Task.Delay(150, operationCts.Token);
                }
                if (stopRunId.Length == 0)
                    throw new InvalidDataException("Packaged Stop proof lacked 20 source-attributed native Truck sends within its live run.");
                // Three manual Map actions have stable semantic order:
                // Start, Stop, Clear. Visible text is localized, so an English
                // label lookup cannot prove or operate the Chinese UI.
                string stopClicked = "false";
                for (int attempt = 0; attempt < 100; attempt++)
                {
                    stopClicked = await core.ExecuteScriptAsync("(()=>{const b=document.querySelectorAll('.map-header .map-actions > button')[1];if(!b||b.disabled)return false;b.click();return true;})()");
                    if (stopClicked == "true") break;
                    await Task.Delay(100, operationCts.Token);
                }
                if (stopClicked != "true")
                    throw new InvalidDataException("Packaged Truck Stop control was unavailable.");
                JsonElement stoppedStatus = default;
                for (int attempt = 0; attempt < 150; attempt++)
                {
                    stoppedStatus = JsonSerializer.SerializeToElement(map317CommandService.CreateStatus(), JsonOptions.Default);
                    // MapScanStateMachine.StopCoreAsync publishes Phase=idle
                    // after committing the durable SQLite status=cancelled.
                    // The Python native/SQLite auditor independently checks
                    // that durable run and exact native cancelled receipt.
                    if (ReadStatusString(stoppedStatus, "phase") == "idle" &&
                        ReadStatusString(stoppedStatus, "scanRunId") == stopRunId &&
                        stoppedStatus.TryGetProperty("isReading", out var reading) &&
                        reading.ValueKind == JsonValueKind.False) break;
                    await Task.Delay(100, operationCts.Token);
                }
                if (ReadStatusString(stoppedStatus, "phase") != "idle" ||
                    ReadStatusString(stoppedStatus, "scanRunId") != stopRunId ||
                    !stoppedStatus.TryGetProperty("isReading", out var stoppedReading) ||
                    stoppedReading.ValueKind != JsonValueKind.False)
                    throw new InvalidDataException("Packaged Truck Stop did not reach the source-owned idle boundary.");
                await File.WriteAllTextAsync(fullOutputPath, JsonSerializer.Serialize(new {
                    schemaVersion=1, state="proven", proof="actual_packaged_ui_truck_stop_mid_sweep",
                    genuineGame=true, controlledProvider=false, selection=qualifiedKind,
                    stopRunId, startedStatus, nativeStarted, stoppedStatus,
                    gamePid=ownedMapSession!.GamePid, profileId=ownedMapSession.ProfileId,
                }, new JsonSerializerOptions(JsonOptions.Default) { WriteIndented=true }), operationCts.Token);
                return;
            }

            for (int attempt = 0; attempt < 3600; attempt++)
            {
                operationCts.Token.ThrowIfCancellationRequested();
                JsonElement status = JsonSerializer.SerializeToElement(map317CommandService.CreateStatus(), JsonOptions.Default);
                string runId = ReadStatusString(status, "scanRunId") ?? string.Empty;
                string phase = ReadStatusString(status, "phase") ?? string.Empty;
                string? error = ReadStatusString(status, "lastError");
                bool isReading = status.TryGetProperty("isReading", out JsonElement readingValue) &&
                    readingValue.ValueKind == JsonValueKind.True;
                int readBlocks = ReadStatusInt32(status, "readBlocks") ?? 0;
                int failedBlocks = ReadStatusInt32(status, "failedBlocks") ?? 0;
                int unreadBlocks = ReadStatusInt32(status, "unreadBlocks") ?? 0;
                if (!string.IsNullOrWhiteSpace(error))
                    throw new InvalidOperationException("The normal production Map scan failed: " + error);
                int totalBlocks = ReadStatusInt32(status, "totalBlocks") ?? 0;
                if (!isReading && phase == "completed" && runId.Length > 0 && runId != initialRunId &&
                    totalBlocks > 0 && readBlocks == totalBlocks)
                {
                    if (failedBlocks != 0 || unreadBlocks != 0)
                        throw new InvalidDataException(
                            $"Production UI scan completed with failed={failedBlocks}, unread={unreadBlocks}.");
                    completedStatus = status;
                    completed = true;
                    break;
                }
                await Task.Delay(100, operationCts.Token);
            }
            if (!completed)
                throw new TimeoutException("The normal production Map scan did not complete within the proof bound.");

            string[] actualSelectedTypes = ReadNormalUiProofSelectedTypes(completedStatus);
            if (ReadStatusString(completedStatus, "scanMode") != "fast")
                throw new InvalidDataException("MAP016 packaged Map did not use its automatic fast scanner.");

            if (qualifiedKind is "all-nontruck" or "all-nine" or "all-ten")
            {
                string[] expectedKinds = qualifiedKind switch
                {
                    "all-ten" => ["city", "resource", "monster", "truck", "railway", "dispatch", "ghost", "treasure", "other", "zombie_boss"],
                    "all-nine" => ["city", "resource", "monster", "truck", "railway", "dispatch", "ghost", "treasure", "zombie_boss"],
                    _ => ["city", "resource", "monster", "railway", "dispatch", "ghost", "treasure", "zombie_boss"],
                };
                if (!actualSelectedTypes.SequenceEqual(expectedKinds, StringComparer.Ordinal))
                    throw new InvalidDataException("MAP015 packaged full scan did not select exactly the requested map kinds.");
                if (ReadStatusInt32(completedStatus, "totalBlocks") != 2500 ||
                    ReadStatusInt32(completedStatus, "readBlocks") != 2500)
                    throw new InvalidDataException("MAP015 packaged non-Truck scan did not complete every map block.");
                object browse = await RunMap015AllNonTruckUiProofAsync(
                    core, directory, completedStatus, ownedMapSession!, operationCts.Token,
                    includeTruck: qualifiedKind is "all-nine" or "all-ten",
                    includeOther: qualifiedKind == "all-ten");
                await File.WriteAllTextAsync(fullOutputPath, JsonSerializer.Serialize(new
                {
                    schemaVersion = 1, state = "proven",
                    proof = qualifiedKind == "all-ten"
                        ? "actual_packaged_ui_all_ten_fullworld_scan_and_results"
                        : "actual_packaged_ui_all_nontruck_fullworld_scan_and_results",
                    genuineGame = true, controlledProvider = false, selection = qualifiedKind,
                    selectedTypes = actualSelectedTypes,
                    serverId = ReadStatusInt32(completedStatus, "serverId"),
                    profileId = backend.ProfileId, gamePid = ownedMapSession!.GamePid,
                    completedStatus, browse,
                }, new JsonSerializerOptions(JsonOptions.Default) { WriteIndented = true }), operationCts.Token);
                return;
            }

            if (qualifiedKind == "truck")
            {
                if (!actualSelectedTypes.SequenceEqual(["truck"], StringComparer.Ordinal))
                    throw new InvalidDataException("Packaged Truck-only selected unexpected map kinds.");
                object browse = await RunMap015TruckOnlyUiProofAsync(
                    core, directory, completedStatus, ownedMapSession!, operationCts.Token);
                await File.WriteAllTextAsync(fullOutputPath, JsonSerializer.Serialize(new {
                    schemaVersion=1,state="proven",proof="actual_packaged_ui_truck_only_scan_and_filter",
                    genuineGame=true,controlledProvider=false,selection=qualifiedKind,
                    selectedTypes=actualSelectedTypes,completedStatus,browse,
                    gamePid=ownedMapSession!.GamePid,profileId=ownedMapSession.ProfileId,
                },new JsonSerializerOptions(JsonOptions.Default){WriteIndented=true}), operationCts.Token);
                return;
            }

            if (qualifiedKind is "city" or "other" or "railway")
            {
                if (!completedStatus.TryGetProperty("selectedTypes", out JsonElement selected) ||
                    selected.ValueKind != JsonValueKind.Array ||
                    selected.GetArrayLength() != 1 || selected[0].GetString() != qualifiedKind)
                    throw new InvalidDataException("Packaged source scan selected unexpected map kinds.");
                int selectedTab = qualifiedKind switch { "city" => 0, "railway" => 4, _ => 8 };
                long beforeSearch = GetNormalUiProofSearchSequence();
                string tabClicked = await core.ExecuteScriptAsync($"(()=>{{const tab=document.querySelectorAll('.map-tabs button')[{selectedTab}];if(!tab)return false;tab.click();return true;}})()");
                if (tabClicked != "true")
                    throw new InvalidDataException("Packaged Map selection does not expose its actual results tab.");
                NormalUiResourceProofSearchObservation? selectedResponse = null;
                for (int attempt = 0; attempt < 250; attempt++)
                {
                    operationCts.Token.ThrowIfCancellationRequested();
                    var seen = ReadNormalUiProofSearchAfter(beforeSearch);
                    if (seen is not null && MapDataQueryContract.NormalizeSearch(seen.Payload).Kind == qualifiedKind &&
                        await core.ExecuteScriptAsync($"!!document.querySelector('.map-table--{qualifiedKind}[aria-busy=false]')") == "true")
                    {
                        selectedResponse = seen;
                        break;
                    }
                    await Task.Delay(100, operationCts.Token);
                }
                if (selectedResponse is null)
                {
                    long retryFrom = GetNormalUiProofSearchSequence();
                    await core.ExecuteScriptAsync("document.querySelector('.map-searchbar > button')?.click()");
                    for (int attempt = 0; attempt < 200; attempt++)
                    {
                        var seen = ReadNormalUiProofSearchAfter(retryFrom);
                        if (seen is not null && MapDataQueryContract.NormalizeSearch(seen.Payload).Kind == qualifiedKind)
                        { selectedResponse = seen; break; }
                        await Task.Delay(100, operationCts.Token);
                    }
                }
                if (selectedResponse is null)
                    throw new InvalidDataException("Packaged selection had no genuine current-server map_search response: " + qualifiedKind);
                int actualServer = ReadStatusInt32(completedStatus, "serverId") ?? 0;
                var selectedQuery = MapDataQueryContract.NormalizeSearch(selectedResponse.Payload);
                if (selectedQuery.ServerId != actualServer || selectedQuery.Kind != qualifiedKind)
                    throw new InvalidDataException("Packaged selection results are not tied to the completed owned server.");
                JsonElement selectedRows = selectedResponse.Result.GetProperty("rows");
                int selectedTotal = selectedResponse.Result.GetProperty("total").GetInt32();
                if (qualifiedKind == "city" && selectedTotal == 0)
                    throw new InvalidDataException("Completed genuine City scan published zero native players.");
                foreach (JsonElement row in selectedRows.EnumerateArray())
                {
                    if (qualifiedKind == "city" && (!row.TryGetProperty("ownerUid", out var ownerId) ||
                        ownerId.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(ownerId.GetString())))
                        throw new InvalidDataException("Published packaged City has no native owner identity.");
                    if (qualifiedKind == "other" && (!row.TryGetProperty("otherSubtype", out var kind) ||
                        kind.ValueKind != JsonValueKind.String ||
                        !(kind.GetString()!.StartsWith("supplies:", StringComparison.Ordinal) || kind.GetString() == "event:27000")))
                        throw new InvalidDataException("Published packaged Other event lacks native source subtype.");
                }
                string mountedDom = await core.ExecuteScriptAsync($"({{activeTab:document.querySelector('.map-tabs [aria-selected=true]')?.textContent,tablePresent:!!document.querySelector('.map-table--{qualifiedKind}'),displayedRows:document.querySelectorAll('.map-table--{qualifiedKind} tbody tr').length}})");
                string snapshot = Path.Combine(directory, "map014-" + qualifiedKind + "-packaged.png");
                await using (var image = File.Create(snapshot))
                    await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, image);
                await File.WriteAllTextAsync(fullOutputPath, JsonSerializer.Serialize(new
                {
                    schemaVersion = 1, state = "proven",
                    proof = qualifiedKind == "railway"
                        ? "actual_packaged_ui_fast_railway_fullworld_scan_and_results"
                        : "actual_packaged_ui_fullworld_selected_scan_and_results",
                    genuineGame = true, controlledProvider = false, selection = qualifiedKind,
                    selectedTypes = actualSelectedTypes,
                    serverId = actualServer, completedStatus, selectedResponse, total = selectedTotal,
                    sourceIdentifiedPositiveAvailable = selectedTotal > 0,
                    mountedDom = JsonSerializer.Deserialize<JsonElement>(mountedDom), screenshot = snapshot,
                }, new JsonSerializerOptions(JsonOptions.Default) { WriteIndented = true }), operationCts.Token);
                return;
            }

            bool resourceTabReady = false;
            for (int attempt = 0; attempt < 150; attempt++)
            {
                if (await core.ExecuteScriptAsync("document.querySelectorAll('.map-tabs button').length > 1") == "true")
                {
                    resourceTabReady = true;
                    break;
                }
                await Task.Delay(100, operationCts.Token);
            }
            if (!resourceTabReady)
                throw new InvalidOperationException("The normal production Map Resource tab did not render.");
            long beforeResourceTabSequence = GetNormalUiProofSearchSequence();
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
                await Task.Delay(100, operationCts.Token);
            }
            if (!resourceSearchSettled)
                throw new TimeoutException("The normal production Resource tab did not settle.");

            NormalUiResourceProofSearchObservation? observation = ReadNormalUiProofSearchAfter(beforeResourceTabSequence);
            long beforeSearchSequence = GetNormalUiProofSearchSequence();
            if (observation is null)
            {
                string searchClick = await core.ExecuteScriptAsync("""
                    (() => {
                      const button = document.querySelector('.map-searchbar > button');
                      if (!button || button.disabled) return false;
                      button.click();
                      return true;
                    })()
                    """);
                if (searchClick != "true")
                    throw new InvalidOperationException("The normal production Resource Search button could not be clicked.");
            }
            for (int attempt = 0; attempt < 200; attempt++)
            {
                observation ??= ReadNormalUiProofSearchAfter(beforeSearchSequence);
                if (observation is not null) break;
                await Task.Delay(100, operationCts.Token);
            }
            if (observation is null)
                throw new TimeoutException("The normal production Resource Search did not produce a native map_search response.");

            MapDataQueryOptions query = MapDataQueryContract.NormalizeSearch(observation.Payload);
            int completedServerId = ReadStatusInt32(completedStatus, "serverId") ?? 0;
            if (query.Kind != "resource" || query.Page != 1 || query.ServerId != completedServerId)
                throw new InvalidDataException("The normal production Resource Search query did not match the completed live scan.");
            if (!observation.Payload.TryGetProperty("profileId", out JsonElement searchProfile) ||
                searchProfile.ValueKind != JsonValueKind.String ||
                !string.Equals(searchProfile.GetString(), backend.ProfileId, StringComparison.Ordinal))
                throw new InvalidDataException("The normal production Resource Search did not preserve the selected profile identity.");

            JsonElement result = observation.Result;
            if (result.ValueKind != JsonValueKind.Object ||
                !result.TryGetProperty("rows", out JsonElement rows) || rows.ValueKind != JsonValueKind.Array ||
                !result.TryGetProperty("total", out JsonElement totalValue) || !totalValue.TryGetInt32(out int total) || total < 1)
                throw new InvalidDataException("The normal production Resource Search returned no live rows.");
            JsonElement queryRow = rows.EnumerateArray().First().Clone();

            int RequiredInt(JsonElement row, string name)
            {
                if (!row.TryGetProperty(name, out JsonElement value) ||
                    value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int parsed))
                    throw new InvalidDataException($"Production Resource row is missing integer {name}.");
                return parsed;
            }
            long RequiredLong(JsonElement row, string name)
            {
                if (!row.TryGetProperty(name, out JsonElement value) ||
                    value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out long parsed))
                    throw new InvalidDataException($"Production Resource row is missing integer {name}.");
                return parsed;
            }
            string RequiredString(JsonElement row, string name)
            {
                if (!row.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(value.GetString()))
                    throw new InvalidDataException($"Production Resource row is missing string {name}.");
                return value.GetString()!;
            }

            int? level = null;
            if (queryRow.TryGetProperty("level", out JsonElement levelValue) &&
                levelValue.ValueKind == JsonValueKind.Number && levelValue.TryGetInt32(out int parsedLevel))
                level = parsedLevel;

            var expected = new NormalUiResourceProofExpected(
                RequiredInt(queryRow, "serverId"),
                RequiredString(queryRow, "recordKey"),
                RequiredInt(queryRow, "pointIndex"),
                RequiredInt(queryRow, "x"),
                RequiredInt(queryRow, "y"),
                level,
                RequiredLong(queryRow, "updatedAt"),
                string.Empty,
                string.Empty,
                null,
                backend.ProfileId,
                instanceId,
                startJson.TryGetProperty("pid", out JsonElement pidValue) && pidValue.TryGetInt32(out int gamePid) ? gamePid : null,
                null);

            string renderedTimeJson = await core.ExecuteScriptAsync(
                $"new Date({expected.UpdatedAt.ToString(System.Globalization.CultureInfo.InvariantCulture)}).toLocaleString(document.documentElement.lang || undefined)");
            string? expectedUpdatedText = JsonSerializer.Deserialize<string?>(renderedTimeJson);

            NormalUiResourceProofMatch? rendered = null;
            NormalUiResourceProofTableSnapshot? lastRenderSnapshot = null;
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
                            lastRenderSnapshot = snapshot;
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
                await Task.Delay(100, operationCts.Token);
            }
            if (rendered is null)
            {
                string debugPath = fullOutputPath + ".render-debug.json";
                await File.WriteAllTextAsync(debugPath, JsonSerializer.Serialize(new
                {
                    queryRow,
                    expected = new
                    {
                        coordinate = $"{expected.X},{expected.Y}",
                        level = expected.Level?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-",
                        expected.UpdatedAt,
                        expectedUpdatedText,
                    },
                    snapshot = lastRenderSnapshot,
                    rejection = lastRenderRejection,
                }, new JsonSerializerOptions(JsonOptions.Default) { WriteIndented = true }));
                string debugScreenshotPath = fullOutputPath + ".render-debug.png";
                await using (var debugOutput = File.Create(debugScreenshotPath))
                    await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, debugOutput);
                throw new InvalidOperationException(
                    "The normal production Resource table never rendered the queried live row. " + lastRenderRejection);
            }

            await Task.Delay(350, operationCts.Token);
            string stem = Path.GetFileNameWithoutExtension(fullOutputPath);
            string screenshotPath = Path.Combine(directory, stem + ".png");
            await using (var output = File.Create(screenshotPath))
                await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, output);

            if (ownerFixRemoteProof) await RunMap014NativeRemoteUiProofAsync(core, directory, operationCts.Token);
            else if (ownerFixNativeProof) await RunMap014NativeUiExtrasAsync(core, directory, operationCts.Token);

            await File.WriteAllTextAsync(fullOutputPath, JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                checkpoint = "LWB-R8-066",
                state = "proven",
                proof = "normal_production_map_window_start_scan_search_render",
                windowMode = "persistent_normal_map_data",
                profileId = backend.ProfileId,
                instanceId,
                connectionState = ReadStatusString(startJson, "connectionState"),
                gamePid = expected.GamePid,
                selectedTypes = actualSelectedTypes,
                scan = new
                {
                    scanRunId = ReadStatusString(completedStatus, "scanRunId"),
                    serverId = completedServerId,
                    scanMode = ReadStatusString(completedStatus, "scanMode"),
                    concurrency = ReadStatusInt32(completedStatus, "concurrency"),
                    totalBlocks = ReadStatusInt32(completedStatus, "totalBlocks"),
                    readBlocks = ReadStatusInt32(completedStatus, "readBlocks"),
                    failedBlocks = ReadStatusInt32(completedStatus, "failedBlocks"),
                    unreadBlocks = ReadStatusInt32(completedStatus, "unreadBlocks"),
                    phase = ReadStatusString(completedStatus, "phase"),
                },
                search = new
                {
                    observation.RequestId,
                    observation.Payload,
                    total,
                    row = queryRow,
                },
                rendered = new
                {
                    rendered.RowText,
                    cells = rendered.Cells,
                },
                screenshotPath,
                generatedAt = DateTimeOffset.UtcNow,
            }, new JsonSerializerOptions(JsonOptions.Default) { WriteIndented = true }));
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(instanceId))
            {
                using var stopCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                using JsonDocument stopPayload = JsonDocument.Parse(
                    JsonSerializer.Serialize(new { instanceId }, JsonOptions.Default));
                try
                {
                    await overviewLifecycleService.InvokeAsync(
                        "profile_instance_stop", stopPayload.RootElement.Clone(), stopCts.Token);
                }
                catch (Exception stopError)
                {
                    Console.Error.WriteLine("NORMAL_UI_PRODUCTION_MAP_STOP_FAILED: " + stopError.Message);
                }
            }
        }
    }

    private long GetNormalUiProofSearchSequence()
    {
        lock (normalUiProofGate) return normalUiProofSearchSequence;
    }

    // Opt-in isolated host trace: distinguishes a request never sent by the
    // WebView from a received but delayed/cancelled/retired native request.
    private void TraceNormalUiMapProofSearch(string id, string command, string stage, JsonElement? payload = null)
    {
        if (normalUiLiveMapProofPath is null || productionPaths?.ExplicitIsolation != true ||
            Environment.GetEnvironmentVariable("LWBRIDGE_MAP014_NATIVE_UI") != "1" ||
            command != "map_search") return;
        try
        {
            string line = JsonSerializer.Serialize(new { requestId = id, stage,
                payload, at = DateTimeOffset.UtcNow }, JsonOptions.Default);
            lock (normalUiProofGate)
                File.AppendAllText(normalUiLiveMapProofPath + ".command-trace.jsonl", line + Environment.NewLine);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Console.Error.WriteLine("Isolated Map search trace unavailable: " + error.Message);
        }
    }

    private void RecordNormalUiMapProofCommandError(string id, string command, JsonElement payload,
        string code, string message)
    {
        if (normalUiLiveMapProofPath is null || productionPaths?.ExplicitIsolation != true ||
            Environment.GetEnvironmentVariable("LWBRIDGE_MAP014_NATIVE_UI") != "1" ||
            command != "map_search") return;
        try
        {
            string receipt=JsonSerializer.Serialize(new {requestId=id,command,payload,code,message,
                at=DateTimeOffset.UtcNow},JsonOptions.Default);
            lock(normalUiProofGate)
                File.AppendAllText(normalUiLiveMapProofPath+".command-errors.jsonl",receipt+Environment.NewLine);
        }
        catch(Exception error) when(error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Console.Error.WriteLine("Isolated Map query error observation unavailable: "+error.Message);
        }
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
        if (normalUiLiveMapProofPath is null)
            return;
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

    private static string? ReadStatusString(JsonElement status, string name) =>
        status.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? ReadStatusInt32(JsonElement status, string name) =>
        status.TryGetProperty(name, out JsonElement value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out int parsed)
            ? parsed
            : null;

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
            if (!TryGetString(root, "sessionId", out string? messageSession) || messageSession is null) return;
            if (!TryGetString(root, "kind", out string? kind)) return;

            if (!string.Equals(messageSession, session.Id, StringComparison.Ordinal)) return;

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
            TraceNormalUiMapProofSearch(id, command, "received", payload);
            RecordHome004F06Dispatch("received", command);
            LWBridgeBackend requestBackend = backend;
            long requestProfileGeneration = Volatile.Read(ref profileRuntimeGeneration);
            bool explicitHomeOwnerCommand = IsExplicitHomeOwnerCommand(command);
            // Controller registry writes, including Add/Delete, are independent
            // of the displayed Home runtime. A successful durable mutation
            // must not be reported as PROFILE_GENERATION_RETIRED merely because
            // the view selected B while the reply to A was pending. Selected
            // runtime/status replies remain generation-fenced.
            bool independentProfileOwnerReply = explicitHomeOwnerCommand ||
                command is "profile_instances_update_and_restart" or
                    "profile_create" or "profile_delete" or "profile_primary_set" or
                    "profile_note_set" or "profile_reorder" or "profile_enabled_set";
            try
            {
                NativeRequestExecution execution = await session.Requests.ExecuteAsync(id, cancellationToken =>
                    command == "game_root_select"
                        ? SelectGameRootAsync(requestBackend, requestProfileGeneration, cancellationToken)
                        : command == "set_window_theme"
                            ? Task.FromResult(
                                windowThemeService.Apply(Handle, payload))
                        : command == "map_city_export" && map317CommandService is not null
                            ? ExportMap317CityAsync(payload, cancellationToken)
                        : Task.Run(async () =>
                        {
                            SemaphoreSlim? ownerAdmission = null;
                            if (command is "profile_instance_start" or "profile_instance_stop" or "profile_delete" &&
                                payload.ValueKind == JsonValueKind.Object &&
                                payload.TryGetProperty("profileId", out JsonElement targetProfile) &&
                                targetProfile.ValueKind == JsonValueKind.String &&
                                !string.IsNullOrWhiteSpace(targetProfile.GetString()))
                            {
                                ownerAdmission = GetProfileMutationAdmissionGate(targetProfile.GetString()!);
                                await ownerAdmission.WaitAsync(cancellationToken).ConfigureAwait(false);
                            }
                            try
                            {
                            LWBridgeBackend targetBackend = await ResolveHomeCommandBackendAsync(
                                command, payload, requestBackend, cancellationToken).ConfigureAwait(false);
                            return await targetBackend.InvokeAsync(command, payload, cancellationToken).ConfigureAwait(false);
                            }
                            finally { ownerAdmission?.Release(); }
                        }, cancellationToken));
                if (!IsCurrentDocument(session)) return;
                TraceNormalUiMapProofSearch(id, command, "executed:" + execution.Status, payload);
                if (execution.Status == NativeRequestExecutionStatus.Rejected)
                {
                    RecordNormalUiMapProofCommandError(id, command, payload, "DUPLICATE_REQUEST_ID",
                        "Request was rejected by the native request ledger.");
                    if (!sessionClosed)
                        SendError(session, id, "DUPLICATE_REQUEST_ID", "A request with this id is already active or the native session is closing.");
                    return;
                }
                if (execution.Status == NativeRequestExecutionStatus.Cancelled)
                {
                    RecordNormalUiMapProofCommandError(id, command, payload, "COMMAND_CANCELLED",
                        "Request was cancelled by the native request ledger.");
                    SendError(session, id, "COMMAND_CANCELLED", "The command was cancelled.");
                    return;
                }
                if (sessionClosed)
                {
                    TraceNormalUiMapProofSearch(id, command, "closed_before_reply", payload);
                    return;
                }
                if (command != "profile_select" && !independentProfileOwnerReply &&
                    (requestProfileGeneration != Volatile.Read(ref profileRuntimeGeneration) ||
                     !ReferenceEquals(requestBackend, backend)))
                {
                    // The old owner may still be running and complete successfully,
                    // but its reply cannot update the newly selected Home view.
                    TraceNormalUiMapProofSearch(id, command, "profile_generation_retired", payload);
                    SendError(session, id, "PROFILE_GENERATION_RETIRED",
                        "The selected profile changed before the command completed.");
                    return;
                }
                if (command == "game_root_select")
                {
                    await profileSwapGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
                    try
                    {
                        ThrowIfProfileRequestRetired(requestBackend, requestProfileGeneration);
                        if (!IsCurrentDocument(session) || sessionClosed) return;
                        SendResult(session, id, execution.Result);
                        await EmitOverviewStateAsync(session).ConfigureAwait(false);
                    }
                    finally
                    {
                        profileSwapGate.Release();
                    }
                    return;
                }
                if (command == "map_search" && normalUiLiveMapProofPath is not null)
                    RecordNormalUiProofSearch(id, payload, execution.Result);
                TraceNormalUiMapProofSearch(id, command, "sending_result", payload);
                SendResult(session, id, execution.Result);
                RecordHome004F06Dispatch("result", command);
                if (command == "map_player_mark_set" && map317CommandService is null)
                    SendEvent(session, "bridge://player-mark-changed", execution.Result);
                if (command is "game_root_select" or "profile_instance_start" or "profile_instance_stop" or "set_automation" or "local_config_set")
                    await EmitOverviewStateAsync(session);
            }
            catch (BridgeCommandException ex)
            {
                RecordHome004F06Dispatch("error:" + ex.Code, command);
                RecordNormalUiMapProofCommandError(id, command, payload, ex.Code, ex.Message);
                if (IsCurrentDocument(session))
                {
                    if (command != "profile_select" && !independentProfileOwnerReply &&
                        (requestProfileGeneration != Volatile.Read(ref profileRuntimeGeneration) ||
                         !ReferenceEquals(requestBackend, backend)))
                    {
                        SendError(session, id, "PROFILE_GENERATION_RETIRED",
                            "The selected profile changed before the command completed.");
                        return;
                    }
                    SendError(session, id, ex.Code, ex.Message, ex.Details);
                }
            }
            catch (Exception ex)
            {
                RecordHome004F06Dispatch("exception:" + ex.GetType().Name, command);
                RecordNormalUiMapProofCommandError(id, command, payload, "NATIVE_COMMAND_FAILED", ex.Message);
                if (IsCurrentDocument(session))
                {
                    if (command != "profile_select" && !independentProfileOwnerReply &&
                        (requestProfileGeneration != Volatile.Read(ref profileRuntimeGeneration) ||
                         !ReferenceEquals(requestBackend, backend)))
                        SendError(session, id, "PROFILE_GENERATION_RETIRED",
                            "The selected profile changed before the command completed.");
                    else
                        SendError(session, id, "NATIVE_COMMAND_FAILED", ex.Message);
                }
            }
        }
    }

    private async Task<object?> SelectGameRootAsync(
        LWBridgeBackend requestBackend,
        long requestProfileGeneration,
        CancellationToken cancellationToken)
    {
        ThrowIfProfileRequestRetired(requestBackend, requestProfileGeneration);
        GameRootStatus current = await Task.Run(requestBackend.GetGameRootStatus, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfProfileRequestRetired(requestBackend, requestProfileGeneration);

        using var dialog = new FolderBrowserDialog
        {
            Description = "Select the Last War installation directory",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
        };
        if (!string.IsNullOrWhiteSpace(current.Path) && Directory.Exists(current.Path))
            dialog.InitialDirectory = current.Path;
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            ThrowIfProfileRequestRetired(requestBackend, requestProfileGeneration);
            return requestBackend.CreateGameRootSelectionCanceled();
        }

        string selectedPath = dialog.SelectedPath;
        NativeGameRootSelectionResult selected = await SaveGameRootSelectionIfCurrentAsync(
            requestBackend,
            requestProfileGeneration,
            selectedPath,
            cancellationToken);
        return selected;
    }

    private void ThrowIfProfileRequestRetired(
        LWBridgeBackend requestBackend,
        long requestProfileGeneration)
    {
        if (requestProfileGeneration == Volatile.Read(ref profileRuntimeGeneration) &&
            ReferenceEquals(requestBackend, backend))
            return;
        throw new BridgeCommandException(
            "PROFILE_GENERATION_RETIRED",
            "The selected profile changed before the command completed.");
    }

    private async Task<NativeGameRootSelectionResult> SaveGameRootSelectionIfCurrentAsync(
        LWBridgeBackend requestBackend,
        long requestProfileGeneration,
        string selectedPath,
        CancellationToken cancellationToken)
    {
        await profileSwapGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfProfileRequestRetired(requestBackend, requestProfileGeneration);
            return await Task.Run(
                () => requestBackend.SaveNativeGameRootSelection(selectedPath),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            profileSwapGate.Release();
        }
    }

    private async Task<object?> ExportMap317CityAsync(
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        Map317CommandService service = map317CommandService
            ?? throw new InvalidOperationException("Map317 export service is unavailable.");
        Map317CityExportRequest request = service.PrepareCityExport(payload);
        cancellationToken.ThrowIfCancellationRequested();


        string selectedPath;
        try
        {
            using var dialog = new SaveFileDialog
            {
                FileName = request.DefaultFileName,
                Filter = "Excel workbook|*.xlsx",
                DefaultExt = "xlsx",
                AddExtension = true,
                OverwritePrompt = true,
                CheckPathExists = true,
            };
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return new { canceled = true, rowCount = 0, path = (string?)null };
            selectedPath = dialog.FileName;
        }
        catch
        {
            return new { canceled = true, rowCount = 0, path = (string?)null };
        }

        cancellationToken.ThrowIfCancellationRequested();
        return await Task.Run(
            () => service.WriteCityExport(request, selectedPath),
            cancellationToken);
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
            SendEvent(session, "bridge://game-recovery", overviewLifecycleService?.CurrentRecoveryStatus ??
                new OverviewRecoveryStatus("idle", null, false, false, 0, null, 0, null, null, 0, false));
    }

    private void OnManualMapScanStatusChanged(long generation, object status)
    {
        if (sessionClosed || IsDisposed || !IsCurrentProfileRuntimeGeneration(generation)) return;
        void Publish()
        {
            if (!IsCurrentProfileRuntimeGeneration(generation)) return;
            DocumentSession session = documentSession;
            if (IsCurrentDocument(session) && session.Subscriptions.Contains("bridge://map-scan-status"))
                SendEvent(session, "bridge://map-scan-status", status);
        }
        try
        {
            if (InvokeRequired) BeginInvoke(Publish);
            else Publish();
        }
        catch (InvalidOperationException) { }
    }

    private void OnMap317PlayerMarkChanged(long generation)
    {
        if (sessionClosed || IsDisposed || !IsCurrentProfileRuntimeGeneration(generation)) return;
        void Publish()
        {
            if (!IsCurrentProfileRuntimeGeneration(generation)) return;
            DocumentSession session = documentSession;
            if (IsCurrentDocument(session) &&
                session.Subscriptions.Contains("bridge://player-mark-changed"))
                SendEvent(session, "bridge://player-mark-changed", new { });
        }
        try
        {
            if (InvokeRequired) BeginInvoke(Publish);
            else Publish();
        }
        catch (InvalidOperationException) { }
    }

    private void OnMapAutoScanStateChanged(long generation, MapAutoScanSnapshot snapshot)
    {
        if (sessionClosed || IsDisposed || !IsCurrentProfileRuntimeGeneration(generation)) return;
        void Publish()
        {
            if (!IsCurrentProfileRuntimeGeneration(generation)) return;
            DocumentSession session = documentSession;
            if (IsCurrentDocument(session) &&
                session.Subscriptions.Contains("bridge://local-map-auto-scan-changed"))
            {
                SendEvent(session, "bridge://local-map-auto-scan-changed", snapshot);
            }
        }
        try
        {
            if (InvokeRequired) BeginInvoke(Publish);
            else Publish();
        }
        catch (InvalidOperationException) { }
    }

    private void OnOverviewRecoveryStatusChanged(long generation, OverviewRecoveryStatus status)
    {
        if (sessionClosed || IsDisposed || !IsCurrentProfileRuntimeGeneration(generation)) return;
        void Publish()
        {
            if (!IsCurrentProfileRuntimeGeneration(generation)) return;
            DocumentSession session = documentSession;
            if (IsCurrentDocument(session) && session.Subscriptions.Contains("bridge://game-recovery"))
                SendEvent(session, "bridge://game-recovery", status);
        }
        try
        {
            if (InvokeRequired) BeginInvoke(Publish);
            else Publish();
        }
        catch (InvalidOperationException) { }
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
        StopLiveUiReload();
        StopFeature018HotkeyMonitoring();
        automationStatusService?.Dispose();
        home004RouteFaultTimer?.Stop();
        home004RouteFaultTimer?.Dispose();
        Interlocked.Exchange(ref profileRuntimeClosed, 1);
        sessionClosed = true;
        documentSession.Close();
        // Serialize shutdown with profile replacement. A selector that already owns
        // the gate finishes transferring before cleanup; a queued selector observes
        // the closed request/runtime state and cannot install a post-close owner.
        profileSwapGate.Wait();
        try
        {
            DetachProfileRuntimeEvents();
            // Every retained runtime owns its own recovery monitor and local
            // backing state. Close them all before closing the shared transport;
            // selection history must not determine which ones survive shutdown.
            foreach (RetainedProfileRuntime runtime in retainedProfileRuntimes.Values)
            {
                runtime.AutomationStatus?.Dispose();
                if (ReferenceEquals(runtime.Lifecycle, overviewLifecycleService))
                    continue;
                DisposeRetiredProfileRuntime(
                    runtime.Auto, runtime.Map, runtime.Drafts, runtime.Settings,
                    runtime.Lifecycle, bridgeHost: null);
            }
            lock (retainedProfileRuntimes)
                retainedProfileRuntimes.Clear();
            // Drain Map-owned workers before closing the game lifecycle they depend on.
            try { mapAutoScanService?.Dispose(); } catch { }
            try { map317CommandService?.Dispose(); } catch { }
            try { cityLayoutDraftService?.Dispose(); } catch { }
            try { profileSettingsService?.Dispose(); } catch { }
            try { overviewLifecycleService?.Close(); } catch { }
            // The application window remains the final owner of the currently active
            // shared control-pipe host; profile replacement closes retired hosts earlier.
            bridgeHostState?.Close();
            profileRegistryService?.Dispose();
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
        finally
        {
            profileSwapGate.Release();
        }
    }

    internal void FinalizeIsolatedProbeStorage()
    {
        if (isolatedConfigRoot is null) return;
        // FormClosed runs before WebView disposal. Retry after the message loop
        // ends, when its temporary browser cache no longer has an active owner.
        webView.Dispose();
        for (int attempt = 0; attempt < 40 && Directory.Exists(isolatedConfigRoot); attempt++)
        {
            try { Directory.Delete(isolatedConfigRoot, recursive: true); }
            catch (IOException) when (attempt < 39) { Thread.Sleep(50); }
            catch (UnauthorizedAccessException) when (attempt < 39) { Thread.Sleep(50); }
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
