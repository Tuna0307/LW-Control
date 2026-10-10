using Map317 = LWBridge.Map317;

namespace LWBridge.Desktop;

/// <summary>
/// Owns every profile-scoped native service retained independently of Home
/// selection until the host closes. Product composition uses the current-client
/// providers; deterministic checks may inject inert providers at this boundary.
/// </summary>
internal sealed class ProfileRuntimeOwner : IDisposable
{
    private bool transferred;
    private readonly bool ownsBridgeHostState;

    private ProfileRuntimeOwner(
        LocalConfigStore config,
        LWBridgeBackend backend,
        LWBridgeControlPipeHostState bridgeHostState,
        OverviewLifecycleService overviewLifecycle,
        Map317CommandService map317,
        MapAutoScanCommandService mapAutoScan,
        CityLayoutDraftCommandService cityLayoutDraft,
        ProfileSettingsCommandService profileSettings,
        ProfileRuntimeConfigStore runtimeConfigStore,
        string runtimeConfigPath,
        HotkeyConfigCommandService hotkeyConfig,
        VisualMetricsConfigCommandService visualMetricsConfig,
        EquipmentConfigCommandService equipmentConfig,
        MonsterAfkConfigCommandService monsterAfkConfig,
        AllianceGarrisonConfigCommandService allianceGarrisonConfig,
        ResourceAutomationConfigCommandService resourceAutomationConfig,
        AutomationStatusCommandService automationStatus,
        ClaimDelayConfigCommandService claimDelayConfig,
        ProfileWindowFocusService focus,
        bool ownsBridgeHostState)
    {
        Config = config;
        Backend = backend;
        BridgeHostState = bridgeHostState;
        OverviewLifecycle = overviewLifecycle;
        Map317 = map317;
        MapAutoScan = mapAutoScan;
        CityLayoutDraft = cityLayoutDraft;
        ProfileSettings = profileSettings;
        RuntimeConfigStore = runtimeConfigStore;
        RuntimeConfigPath = runtimeConfigPath;
        HotkeyConfig = hotkeyConfig;
        VisualMetricsConfig = visualMetricsConfig;
        EquipmentConfig = equipmentConfig;
        MonsterAfkConfig = monsterAfkConfig;
        AllianceGarrisonConfig = allianceGarrisonConfig;
        ResourceAutomationConfig = resourceAutomationConfig;
        AutomationStatus = automationStatus;
        ClaimDelayConfig = claimDelayConfig;
        Focus = focus;
        this.ownsBridgeHostState = ownsBridgeHostState;
    }

    internal LocalConfigStore Config { get; }
    internal LWBridgeBackend Backend { get; }
    internal LWBridgeControlPipeHostState BridgeHostState { get; }
    internal OverviewLifecycleService OverviewLifecycle { get; }
    internal Map317CommandService Map317 { get; }
    internal MapAutoScanCommandService MapAutoScan { get; }
    internal CityLayoutDraftCommandService CityLayoutDraft { get; }
    internal ProfileSettingsCommandService ProfileSettings { get; }
    internal ProfileRuntimeConfigStore RuntimeConfigStore { get; }
    internal string RuntimeConfigPath { get; }
    internal HotkeyConfigCommandService HotkeyConfig { get; }
    internal VisualMetricsConfigCommandService VisualMetricsConfig { get; }
    internal EquipmentConfigCommandService EquipmentConfig { get; }
    internal MonsterAfkConfigCommandService MonsterAfkConfig { get; }
    internal AllianceGarrisonConfigCommandService AllianceGarrisonConfig { get; }
    internal ResourceAutomationConfigCommandService ResourceAutomationConfig { get; }
    internal AutomationStatusCommandService AutomationStatus { get; }
    internal ClaimDelayConfigCommandService ClaimDelayConfig { get; }
    internal ProfileWindowFocusService Focus { get; }

    internal static ProfileRuntimeOwner Create(
        string profileId,
        LocalConfigStore config,
        string profileRoot,
        ProfileRegistryCommandService profileRegistryService,
        Map317.IMapProvider? mapProvider = null,
        Map317.IMapActionProvider? mapActionProvider = null,
        bool startPlunderWorkers = true,
        bool startAutoScheduler = true,
        bool startRecoveryMonitor = true,
        bool startBridgeTransport = true,
        OverviewLifecycleTestHooks? lifecycleTestHooks = null,
        GameInstallationTestHooks? installationTestHooks = null,
        ProxyStatusTestHooks? proxyStatusTestHooks = null,
        Func<bool>? bridgeReadyProvider = null,
        Func<bool>? mapAutoOnlineProvider = null,
        string? overviewRuntimeRoot = null,
        string? overviewEvidenceRoot = null,
        string? overviewBackupRoot = null,
        LWBridgeControlPipeHostState? sharedBridgeHostState = null,
        string? applicationDataRoot = null,
        OrderedProfileReconcileCommandService? startupReconcile = null,
        Func<int, bool>? foreignOwnedProcess = null,
        Func<string, bool>? foreignOwnedInstallation = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileRoot);
        ArgumentNullException.ThrowIfNull(profileRegistryService);

        string fullProfileRoot = Path.GetFullPath(profileRoot);
        string profileDatabasePath = Path.Combine(fullProfileRoot, "profile.db");
        string runtimeConfigPath = Path.Combine(fullProfileRoot, "runtime", "config.json");
        var runtimeConfigStore = new ProfileRuntimeConfigStore(runtimeConfigPath);
        var drafts = new CityLayoutDraftCommandService(profileId, profileDatabasePath);
        var settings = new ProfileSettingsCommandService(profileId, profileDatabasePath);
        var hotkeys = new HotkeyConfigCommandService(runtimeConfigStore);
        var visualMetrics = new VisualMetricsConfigCommandService(runtimeConfigStore);
        var equipment = new EquipmentConfigCommandService(runtimeConfigStore);
        var monsterAfk = new MonsterAfkConfigCommandService(runtimeConfigStore);
        var garrison = new AllianceGarrisonConfigCommandService(runtimeConfigStore);
        string automationStatusPath = Path.Combine(
            fullProfileRoot,
            "runtime",
            "automation-status.json");
        var resourceAutomation = new ResourceAutomationConfigCommandService(
            runtimeConfigStore,
            automationStatusPath);
        var automationStatus = new AutomationStatusCommandService(
            runtimeConfigStore,
            automationStatusPath);
        var claimDelay = new ClaimDelayConfigCommandService(runtimeConfigStore);
        bool ownsBridgeHostState = sharedBridgeHostState is null;
        var bridgeHost = sharedBridgeHostState ?? new LWBridgeControlPipeHostState();
        OverviewLifecycleService? lifecycle = null;
        Map317CommandService? map = null;
        MapAutoScanCommandService? auto = null;
        try
        {
            GameRootStatus liveGameRoot =
                new GameInstallationService(config, installationTestHooks)
                    .GetLaunchAdmissionStatus();
            if (startBridgeTransport && ownsBridgeHostState && liveGameRoot.Valid)
            {
                string expectedClientPath =
                    LWBridgeControlPipeClientPathContract
                        .BuildExpectedGameExecutablePath(liveGameRoot.Path);
                _ = bridgeHost.StartRpcTransport(
                    OverviewLifecycleService.BridgeVersion,
                    expectedClientPath);
            }

            lifecycle = new OverviewLifecycleService(
                profileId,
                liveGameRoot.Valid ? liveGameRoot.Path : null,
                requireCurrentClientEvidence: lifecycleTestHooks is null ? null : false,
                config: config,
                testHooks: lifecycleTestHooks,
                startRecoveryMonitor: startRecoveryMonitor,
                bridgeHostState: bridgeHost,
                enableBridgeControlPipeLaunchBinding: startBridgeTransport,
                runtimeRoot: overviewRuntimeRoot,
                evidenceRoot: overviewEvidenceRoot,
                backupRoot: overviewBackupRoot,
                applicationDataRoot: applicationDataRoot,
                foreignOwnedProcess: foreignOwnedProcess,
                foreignOwnedInstallation: foreignOwnedInstallation);
            map = mapProvider is null
                ? new Map317CommandService(
                    Path.Combine(fullProfileRoot, "map-data", "map-data.db"),
                    lifecycle)
                : new Map317CommandService(
                    Path.Combine(fullProfileRoot, "map-data", "map-data.db"),
                    mapProvider,
                    mapActionProvider ?? LWBridge.Map317.UnavailableMapActionProvider.Instance,
                    startPlunderWorkers);
            auto = new MapAutoScanCommandService(
                Path.Combine(fullProfileRoot, "runtime", "map-auto-scan.json"),
                new MapAutoScanExecutionBoundary
                {
                    IsOnline = mapAutoOnlineProvider ?? (() => string.Equals(
                        lifecycle.CurrentConnectionState,
                        "connected",
                        StringComparison.Ordinal)),
                    IsMapScanActive = () => map.IsScanActive,
                    ReadStatusAsync = map.ReadAutoScanStatusAsync,
                    StartTargetScanAsync = map.StartAutoScanTargetAsync,
                    ReturnServerAsync = map.ReturnAutoScanToServerAsync,
                    StopScanIfOwnedAsync = map.StopAutoScanIfOwnedAsync,
                },
                startScheduler: startAutoScheduler);

            var orderedReconcile = startupReconcile ?? new OrderedProfileReconcileCommandService(
                profileRegistryService,
                (ownerId, payload, token) =>
                    string.Equals(ownerId, profileId, StringComparison.Ordinal)
                        ? lifecycle.InvokeAsync("profile_instances_reconcile", payload, token)
                        : Task.FromException<object?>(new BridgeCommandException(
                            "PROFILE_RUNTIME_UNAVAILABLE",
                            "The requested profile has no active local runtime owner.")));
            INativeAsyncCommandService[] commands =
            [
                orderedReconcile,
                lifecycle,
                map,
                auto,
                drafts,
                profileRegistryService,
                settings,
                hotkeys,
                visualMetrics,
                equipment,
                monsterAfk,
                garrison,
                resourceAutomation,
                automationStatus,
                claimDelay,
            ];
            var backend = new LWBridgeBackend(
                config,
                asyncCommands: new CompositeAsyncCommandService(commands),
                overviewLifecycle: lifecycle,
                mapScanStatusProvider: map.CreateStatus,
                bridgeHostState: bridgeHost,
                runtimeTasksProvider: runtimeConfigStore.ReadTasksSnapshot,
                profileRuntimeDirectory: Path.GetDirectoryName(runtimeConfigPath),
                installationTestHooks: installationTestHooks,
                proxyStatusTestHooks: proxyStatusTestHooks,
                bridgeReadyProvider: bridgeReadyProvider,
                applicationDataRoot: applicationDataRoot);
            var focus = new ProfileWindowFocusService(
                profileId,
                new GameInstallationService(config, installationTestHooks));
            return new ProfileRuntimeOwner(
                config,
                backend,
                bridgeHost,
                lifecycle,
                map,
                auto,
                drafts,
                settings,
                runtimeConfigStore,
                runtimeConfigPath,
                hotkeys,
                visualMetrics,
                equipment,
                monsterAfk,
                garrison,
                resourceAutomation,
                automationStatus,
                claimDelay,
                focus,
                ownsBridgeHostState);
        }
        catch
        {
            try { auto?.Dispose(); } catch { }
            try { map?.Dispose(); } catch { }
            try { drafts.Dispose(); } catch { }
            try { settings.Dispose(); } catch { }
            try { lifecycle?.Close(); } catch { }
            if (ownsBridgeHostState)
                try { bridgeHost.Close(); } catch { }
            throw;
        }
    }

    internal void TransferOwnership() => transferred = true;

    public void Dispose()
    {
        if (transferred) return;
        try { MapAutoScan.Dispose(); } catch { }
        try { Map317.Dispose(); } catch { }
        try { CityLayoutDraft.Dispose(); } catch { }
        try { ProfileSettings.Dispose(); } catch { }
        try { OverviewLifecycle.Close(); } catch { }
        if (ownsBridgeHostState)
            try { BridgeHostState.Close(); } catch { }
    }
}
