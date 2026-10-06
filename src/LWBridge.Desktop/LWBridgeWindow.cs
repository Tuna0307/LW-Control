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
        "bridge://dispatch-plunder-changed",
        "bridge://truck-plunder-changed",
        "bridge://local-map-auto-scan-changed",
    ];
    private static readonly HashSet<string> ProfileScopedEvents = new(StringComparer.Ordinal)
    {
        "bridge://status",
        "bridge://map-scan-status",
        "bridge://automation-status",
        "bridge://resource-automation-status",
        "bridge://game-recovery",
        "bridge://player-mark-changed",
        "bridge://dispatch-plunder-changed",
        "bridge://truck-plunder-changed",
        "bridge://local-map-auto-scan-changed",
    };

    private readonly string? capturePath;
    private readonly string? liveProbePath;
    private readonly string? hostProbePath;
    private readonly FirstLiveResultImport? firstLiveResult;
    private readonly string? normalUiLiveResourceProofPath;
    private readonly string? normalUiLiveMapProofPath;
    private readonly string? mapUiIntegrationProofPath;
    private readonly string? homeMapCampaignProofPath;
    private readonly bool homeMapCampaignNarrow;
    private readonly OwnerEvidenceRecorder? ownerEvidence;
    private CancellationTokenSource? ownerEvidenceRenderCapture;
    private readonly string initialView;
    private readonly string? language;
    private readonly string? theme;
    private readonly string uiRootPath;
    private LWBridgeBackend backend = null!;
    private readonly MapDataStore? mapData;
    private readonly HostProbeCommandService? hostProbeService;
    private LWBridgeControlPipeHostState? bridgeHostState;
    private OverviewLifecycleService? overviewLifecycleService;
    private readonly LiveResourceProbeCommandService? liveResourceService;
    private Map317CommandService? map317CommandService;
    private MapAutoScanCommandService? mapAutoScanService;
    private CityLayoutDraftCommandService? cityLayoutDraftService;
    private readonly ProfileRegistryCommandService? profileRegistryService;
    private ProfileSettingsCommandService? profileSettingsService;
    private HotkeyConfigCommandService? hotkeyConfigService;
    private VisualMetricsConfigCommandService? visualMetricsConfigService;
    private EquipmentConfigCommandService? equipmentConfigService;
    private MonsterAfkConfigCommandService? monsterAfkConfigService;
    private AllianceGarrisonConfigCommandService? allianceGarrisonConfigService;
    private ResourceAutomationConfigCommandService? resourceAutomationConfigService;
    private AutomationStatusCommandService? automationStatusService;
    private ClaimDelayConfigCommandService? claimDelayConfigService;
    private ProfileRuntimeConfigStore? profileRuntimeConfigStore;
    private string? profileRuntimeConfigPath;
    private readonly SemaphoreSlim profileSwapGate = new(1, 1);
    private readonly string? productionApplicationRoot;
    private readonly string? primaryProfileId;
    private readonly LWBridgeLocalConfig? primaryProfileSeed;
    private LocalConfigStore activeProfileConfig;
    private long profileRuntimeGeneration = 1;
    private Action<OverviewRecoveryStatus>? overviewRecoveryHandler;
    private Action<object>? resourceAutomationStatusHandler;
    private Action<object>? manualMapScanStatusHandler;
    private Action? mapPlayerMarkHandler;
    private Action? dispatchPlunderHandler;
    private Action? truckPlunderHandler;
    private Action<MapAutoScanSnapshot>? mapAutoScanHandler;
    private readonly WindowThemeService windowThemeService = new();
    private readonly string? isolatedConfigRoot;
    private readonly bool sessionScopedMapData;
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
    private readonly object homeMapCampaignCommandGate = new();
    private string? homeMapCampaignDelayedCommand;
    private TaskCompletionSource<HomeMapCampaignDelayedRequest>? homeMapCampaignDelayedEntered;
    private TaskCompletionSource? homeMapCampaignDelayedRelease;
    private HomeMapCampaignDelayedRequest? homeMapCampaignDelayedObservation;
    private string? homeMapCampaignRejectedCommand;
    private string homeMapCampaignExportMode = "cancel";
    private readonly List<string> homeMapCampaignShutdownFailures = [];
    private readonly object homeMapCampaignEventGate = new();
    private readonly List<HomeMapCampaignEventObservation> homeMapCampaignEvents = [];
    private bool homeMapCampaignConnected = true;
    private int homeMapCampaignScanStartCount;
    private int homeMapCampaignScanStopCount;
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
        string? normalUiLiveResourceProofPath = null,
        string? normalUiLiveMapProofPath = null,
        string? ownerEvidencePath = null,
        string? uiRootPath = null,
        string? mapUiIntegrationProofPath = null,
        string? homeMapCampaignProofPath = null,
        bool homeMapCampaignNarrow = false,
        bool useLegacyUi = false)
    {
        this.capturePath = capturePath;
        this.liveProbePath = liveProbePath;
        this.hostProbePath = hostProbePath;
        this.normalUiLiveResourceProofPath = normalUiLiveResourceProofPath;
        this.normalUiLiveMapProofPath = normalUiLiveMapProofPath;
        this.mapUiIntegrationProofPath = mapUiIntegrationProofPath;
        this.homeMapCampaignProofPath = homeMapCampaignProofPath;
        this.homeMapCampaignNarrow = homeMapCampaignNarrow;
        ownerEvidence = ownerEvidencePath is null ? null : new OwnerEvidenceRecorder(ownerEvidencePath);
        string[] views = ["overview", "automation", "map-data", "march", "city-layout", "hotkeys", "mini-games", "advanced", "settings"];
        if (!views.Contains(initialView)) throw new ArgumentException("Unknown --view: " + initialView);
        this.initialView = initialView;
        this.language = language;
        this.theme = theme;
        DesktopUiSelection uiSelection = DesktopUiContentRoot.Select(
            AppContext.BaseDirectory,
            useLegacyUi,
            uiRootPath,
            allowProofOverride: mapUiIntegrationProofPath is not null);
        this.uiRootPath = uiSelection.RootPath;
        bool isolated = capturePath is not null || liveProbePath is not null || hostProbePath is not null || firstLiveResultPath is not null || homeMapCampaignProofPath is not null;
        // The legacy store is retained only by the isolated replay fixtures and
        // the dedicated legacy live-resource proof. Normal production Map owns
        // exactly the recovered per-profile Map317 database below.
        sessionScopedMapData = !isolated && normalUiLiveResourceProofPath is not null;
        LocalConfigStore config;
        if (hostProbePath is not null || homeMapCampaignProofPath is not null)
        {
            isolatedConfigRoot = Path.Combine(
                Path.GetTempPath(),
                (homeMapCampaignProofPath is not null ? "lwb317-home-map-campaign-" : "lwbridge-host-probe-") + Guid.NewGuid().ToString("N"));
            config = homeMapCampaignProofPath is null
                ? new LocalConfigStore(isolatedConfigRoot)
                : new LocalConfigStore(
                    isolatedConfigRoot,
                    initialValue: LWBridgeLocalConfig.CreateDefault() with
                    {
                        ProfileId = "campaign-A",
                        AutoLaunchGame = true,
                        AutoReconnect = false,
                        GameDesiredRunning = false,
                    });
        }
        else
        {
            config = new LocalConfigStore(persistent: !isolated);
        }
        productionApplicationRoot = homeMapCampaignProofPath is not null
            ? isolatedConfigRoot
            : isolated
                ? null
                : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LWBridgeRebuild");
        primaryProfileId = productionApplicationRoot is null ? null : config.Snapshot.ProfileId;
        primaryProfileSeed = productionApplicationRoot is null ? null : config.Snapshot;
        activeProfileConfig = config;
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
                : normalUiLiveResourceProofPath is not null
                    ? new MapDataStore(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "LWBridgeRebuild", "profiles", config.Snapshot.ProfileId, "map-data.db"))
                    : null;
        }
        if (sessionScopedMapData)
        {
            // OWNER WORKFLOW R7-147: published scan rows are session data. Clear
            // leftovers from either a prior normal close or an interrupted process
            // before exposing any map summary. Durable marks/settings/jobs remain.
            mapData!.ClearAllScanData();
        }
        hostProbeService = hostProbePath is null ? null : new HostProbeCommandService();
        string? controllerDatabasePath = productionApplicationRoot is null
            ? null
            : Path.Combine(productionApplicationRoot, "controller.db");
        if (homeMapCampaignProofPath is not null && controllerDatabasePath is not null)
        {
            using var proofRegistry = new ProfileRegistryStore(controllerDatabasePath);
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            proofRegistry.EnsureLocalProfile("campaign-A", "Campaign A", now);
            proofRegistry.EnsureSecondaryProfile("campaign-B", "Campaign B", 1, now + 1);
            proofRegistry.SelectProfile("campaign-A");
            SeedHomeMapCampaignProofDatabase(
                Path.Combine(productionApplicationRoot!, "profiles", "campaign-A", "map-data", "map-data.db"),
                317,
                "A");
            SeedHomeMapCampaignProofDatabase(
                Path.Combine(productionApplicationRoot!, "profiles", "campaign-B", "map-data", "map-data.db"),
                318,
                "B");
        }
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
                maxProfiles: homeMapCampaignProofPath is not null ? 2 : 1,
                focusProfile: profileWindowFocus is null
                    ? null
                    : profileWindowFocus.TryFocus,
                selectProfileOwner: SelectProfileOwnerAsync);
        string? profileDatabasePath = isolated
            ? null
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LWBridgeRebuild", "profiles", config.Snapshot.ProfileId, "profile.db");
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
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LWBridgeRebuild", "profiles", config.Snapshot.ProfileId,
                "runtime", "config.json");
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
        allianceGarrisonConfigService = profileRuntimeConfigStore is null
            ? null
            : new AllianceGarrisonConfigCommandService(profileRuntimeConfigStore);
        resourceAutomationConfigService = profileRuntimeConfigStore is null || profileRuntimeConfigPath is null
            ? null
            : new ResourceAutomationConfigCommandService(
                profileRuntimeConfigStore,
                Path.Combine(
                    Path.GetDirectoryName(profileRuntimeConfigPath)!,
                    "automation-status.json"));
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
            bridgeHostState = new LWBridgeControlPipeHostState();
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
                enableBridgeControlPipeLaunchBinding: true);
            if (normalUiLiveResourceProofPath is null)
            {
                string map317DatabasePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "LWBridgeRebuild", "profiles", config.Snapshot.ProfileId,
                    "map-data", "map-data.db");
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
                            ReturnServerAsync = map317CommandService.ReturnAutoScanToServerAsync,
                            StopScanIfOwnedAsync = map317CommandService.StopAutoScanIfOwnedAsync,
                        });
                liveResourceService = null;
            }
            else
            {
                // This mode is a dedicated legacy acquisition proof. Keep its
                // command ownership isolated so Map317 cannot shadow the proof
                // service's scan lifecycle in the production command composite.
                map317CommandService = null;
                mapAutoScanService = null;
                liveResourceService = new LiveResourceProbeCommandService(
                    mapData!,
                    gameRoot: liveGameRoot.Valid ? liveGameRoot.Path : null,
                    profileId: config.Snapshot.ProfileId);
            }
        }
        else if (homeMapCampaignProofPath is not null)
        {
            liveResourceService = null;
            using ProfileRuntimeOwner owner = CreateHomeMapCampaignRuntimeOwner(
                config.Snapshot.ProfileId,
                config,
                Path.Combine(productionApplicationRoot!, "profiles", config.Snapshot.ProfileId));
            backend = owner.Backend;
            bridgeHostState = owner.BridgeHostState;
            overviewLifecycleService = owner.OverviewLifecycle;
            map317CommandService = owner.Map317;
            mapAutoScanService = owner.MapAutoScan;
            cityLayoutDraftService = owner.CityLayoutDraft;
            profileSettingsService = owner.ProfileSettings;
            profileRuntimeConfigStore = owner.RuntimeConfigStore;
            profileRuntimeConfigPath = owner.RuntimeConfigPath;
            hotkeyConfigService = owner.HotkeyConfig;
            visualMetricsConfigService = owner.VisualMetricsConfig;
            equipmentConfigService = owner.EquipmentConfig;
            monsterAfkConfigService = owner.MonsterAfkConfig;
            allianceGarrisonConfigService = owner.AllianceGarrisonConfig;
            resourceAutomationConfigService = owner.ResourceAutomationConfig;
            automationStatusService = owner.AutomationStatus;
            claimDelayConfigService = owner.ClaimDelayConfig;
            owner.TransferOwnership();
        }
        else
        {
            bridgeHostState = null;
            overviewLifecycleService = null;
            liveResourceService = null;
            map317CommandService = null;
            mapAutoScanService = null;
        }
        INativeAsyncCommandService? productionCommands = hostProbeService;
        if (homeMapCampaignProofPath is null && productionCommands is null)
        {
            var services = new List<INativeAsyncCommandService>();
            if (overviewLifecycleService is not null) services.Add(overviewLifecycleService);
            if (map317CommandService is not null) services.Add(map317CommandService);
            if (mapAutoScanService is not null) services.Add(mapAutoScanService);
            if (liveResourceService is not null) services.Add(liveResourceService);
            if (cityLayoutDraftService is not null) services.Add(cityLayoutDraftService);
            if (profileRegistryService is not null) services.Add(profileRegistryService);
            if (profileSettingsService is not null) services.Add(profileSettingsService);
            if (hotkeyConfigService is not null) services.Add(hotkeyConfigService);
            if (visualMetricsConfigService is not null) services.Add(visualMetricsConfigService);
            if (equipmentConfigService is not null) services.Add(equipmentConfigService);
            if (monsterAfkConfigService is not null) services.Add(monsterAfkConfigService);
            if (allianceGarrisonConfigService is not null) services.Add(allianceGarrisonConfigService);
            if (resourceAutomationConfigService is not null) services.Add(resourceAutomationConfigService);
            if (automationStatusService is not null) services.Add(automationStatusService);
            if (claimDelayConfigService is not null) services.Add(claimDelayConfigService);
            productionCommands = services.Count switch
            {
                0 => null,
                1 => services[0],
                _ => new CompositeAsyncCommandService([.. services]),
            };
        }
        if (homeMapCampaignProofPath is null)
        {
            backend = new LWBridgeBackend(
                config,
                asyncCommands: productionCommands,
                mapData: mapData,
                firstLiveResultServerId: firstLiveResult?.ServerId,
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
                    : Path.GetDirectoryName(profileRuntimeConfigPath));
        }
        AttachProfileRuntimeEvents();
        if (!isolated &&
            normalUiLiveResourceProofPath is null &&
            profileRegistryService is not null)
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
        ClientSize = homeMapCampaignNarrow ? new Size(900, 720) : new Size(1120, 720);
        MinimumSize = new Size(900, 640);
        BackColor = Color.FromArgb(245, 245, 247);
        Controls.Add(webView);
        ownerEvidence?.Record("session-start", new { processId = Environment.ProcessId, profileId = config.Snapshot.ProfileId, initialView });
        Shown += OnShown;
        FormClosed += OnFormClosed;
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
            profileRegistryService is null ||
            normalUiLiveResourceProofPath is not null)
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

            using ProfileRuntimeOwner next = BuildProfileRuntimeReplacement(profileId);
            cancellationToken.ThrowIfCancellationRequested();
            if (Volatile.Read(ref profileRuntimeClosed) != 0 || sessionClosed)
                throw new BridgeCommandException("APP_SHUTTING_DOWN", "APP_SHUTTING_DOWN");

            LWBridgeControlPipeHostState? oldBridgeHost = bridgeHostState;
            OverviewLifecycleService? oldLifecycle = overviewLifecycleService;
            Map317CommandService? oldMap = map317CommandService;
            MapAutoScanCommandService? oldAuto = mapAutoScanService;
            CityLayoutDraftCommandService? oldDrafts = cityLayoutDraftService;
            ProfileSettingsCommandService? oldSettings = profileSettingsService;

            DetachProfileRuntimeEvents();
            Interlocked.Increment(ref profileRuntimeGeneration);

            activeProfileConfig = next.Config;
            backend = next.Backend;
            bridgeHostState = next.BridgeHostState;
            overviewLifecycleService = next.OverviewLifecycle;
            map317CommandService = next.Map317;
            mapAutoScanService = next.MapAutoScan;
            cityLayoutDraftService = next.CityLayoutDraft;
            profileSettingsService = next.ProfileSettings;
            profileRuntimeConfigStore = next.RuntimeConfigStore;
            profileRuntimeConfigPath = next.RuntimeConfigPath;
            hotkeyConfigService = next.HotkeyConfig;
            visualMetricsConfigService = next.VisualMetricsConfig;
            equipmentConfigService = next.EquipmentConfig;
            monsterAfkConfigService = next.MonsterAfkConfig;
            allianceGarrisonConfigService = next.AllianceGarrisonConfig;
            resourceAutomationConfigService = next.ResourceAutomationConfig;
            automationStatusService = next.AutomationStatus;
            claimDelayConfigService = next.ClaimDelayConfig;
            next.TransferOwnership();
            AttachProfileRuntimeEvents();

            DisposeRetiredProfileRuntime(
                oldAuto,
                oldMap,
                oldDrafts,
                oldSettings,
                oldLifecycle,
                oldBridgeHost);

            if (focusGame)
                next.Focus.TryFocus(profileId);
        }
        finally
        {
            profileSwapGate.Release();
        }
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
        if (homeMapCampaignProofPath is not null)
            return CreateHomeMapCampaignRuntimeOwner(profileId, config, profileRoot);
        return ProfileRuntimeOwner.Create(
            profileId,
            config,
            profileRoot,
            profileRegistryService);
    }

    private ProfileRuntimeOwner CreateHomeMapCampaignRuntimeOwner(
        string profileId,
        LocalConfigStore config,
        string profileRoot)
    {
        if (profileRegistryService is null || isolatedConfigRoot is null)
            throw new InvalidOperationException("Isolated Home/Map campaign runtime is unavailable.");

        int serverId = string.Equals(profileId, "campaign-B", StringComparison.Ordinal) ? 318 : 317;
        int currentServerId = serverId;
        var provider = new LWBridge.Map317.MapProviderAdapter(
            _ => ValueTask.FromResult(new LWBridge.Map317.MapProviderContext(
                true, true, Volatile.Read(ref currentServerId), "live", 1, 100, 100, 1)),
            _ => ValueTask.FromResult(new LWBridge.Map317.MapProviderContext(
                true, true, Volatile.Read(ref currentServerId), "live", 1, 100, 100, 1)),
            (_, _) =>
            {
                Interlocked.Increment(ref homeMapCampaignScanStartCount);
                return ValueTask.FromResult(new LWBridge.Map317.MapProviderStartResult(
                    true, 1, true, 0, 0));
            },
            _ =>
            {
                Interlocked.Increment(ref homeMapCampaignScanStopCount);
                return ValueTask.CompletedTask;
            });
        static LWBridge.Map317.BridgeCommandException ProtectedUnavailable() =>
            new("GAME_CONNECTION_UNAVAILABLE", "game connection unavailable");
        var actionProvider = new LWBridge.Map317.MapActionProviderAdapter
        {
            GotoWorldCoordinate = (_, _, _, _) => ValueTask.CompletedTask,
            GotoWorldMarch = (_, _, _) => ValueTask.CompletedTask,
            GetCurrentServerId = _ => ValueTask.FromResult(Volatile.Read(ref currentServerId)),
            GotoServer = (targetServerId, _) =>
            {
                Volatile.Write(ref currentServerId, targetServerId);
                return ValueTask.CompletedTask;
            },
            InspectTreasureStates = (_, _, _) => ValueTask.FromException<LWBridge.Map317.TreasureInspectionResult>(ProtectedUnavailable()),
            GetTreasureClaimStatus = _ => ValueTask.FromException<JsonElement>(ProtectedUnavailable()),
            ClaimTreasures = (_, _) => ValueTask.FromException<JsonElement>(ProtectedUnavailable()),
            ShareDispatchTaskToAlliance = (_, _) => ValueTask.FromResult(new LWBridge.Map317.DispatchShareProviderResult(false)),
            PrepareGhostPlunderTasks = (_, _) => ValueTask.FromException<IReadOnlyList<JsonElement>>(ProtectedUnavailable()),
            GetMapPlunderServerDayStart = _ => ValueTask.FromResult<LWBridge.Map317.MapPlunderServerDayProviderResult?>(null),
            ArmDispatchPlunder = (jobs, _) => ValueTask.FromResult<IReadOnlyList<LWBridge.Map317.DispatchPlunderArmResult>>(
                jobs.Select(job => new LWBridge.Map317.DispatchPlunderArmResult(job.Kind, job.ServerId, job.TaskUuid, true)).ToArray()),
            ArmTruckPlunder = (_, _) => ValueTask.FromResult(new LWBridge.Map317.TruckPlunderArmResult(true)),
            DrainDispatchPlunderResults = _ => ValueTask.FromResult<IReadOnlyList<LWBridge.Map317.DispatchPlunderResultEvent>>(Array.Empty<LWBridge.Map317.DispatchPlunderResultEvent>()),
            DrainTruckPlunderResults = _ => ValueTask.FromResult<IReadOnlyList<LWBridge.Map317.TruckPlunderResultEvent>>(Array.Empty<LWBridge.Map317.TruckPlunderResultEvent>()),
            ClearTruckPlunderPending = (_, _, _, _) => ValueTask.CompletedTask,
        };
        var installationHooks = new GameInstallationTestHooks
        {
            DefaultRoot = Path.Combine(isolatedConfigRoot, "missing-default"),
            GetEnvironmentVariable = _ => null,
            NearbyRoot = Path.Combine(isolatedConfigRoot, "missing-nearby"),
            LocalAppData = Path.Combine(isolatedConfigRoot, "missing-local"),
            DiscoveredCandidates = Array.Empty<GameRootCandidate>(),
        };
        return ProfileRuntimeOwner.Create(
            profileId,
            config,
            profileRoot,
            profileRegistryService,
            mapProvider: provider,
            mapActionProvider: actionProvider,
            startPlunderWorkers: true,
            startAutoScheduler: true,
            startRecoveryMonitor: false,
            startBridgeTransport: false,
            installationTestHooks: installationHooks,
            proxyStatusTestHooks: new ProxyStatusTestHooks
            {
                GameRunning = () => homeMapCampaignConnected,
                RuntimeManaged = () => homeMapCampaignConnected,
                ResourceDirectory = Path.Combine(isolatedConfigRoot, "missing-proxy-resources"),
                FileExists = _ => false,
            },
            bridgeReadyProvider: () => homeMapCampaignConnected,
            mapAutoOnlineProvider: () => homeMapCampaignConnected);
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
        if (resourceAutomationConfigService is not null)
        {
            resourceAutomationStatusHandler = status =>
                OnResourceAutomationStatusChanged(generation, status);
            resourceAutomationConfigService.StatusChanged += resourceAutomationStatusHandler;
        }
        if (map317CommandService is not null)
        {
            manualMapScanStatusHandler = status =>
                OnManualMapScanStatusChanged(generation, status);
            mapPlayerMarkHandler = () => OnMap317PlayerMarkChanged(generation);
            dispatchPlunderHandler = () => OnDispatchPlunderChanged(generation);
            truckPlunderHandler = () => OnTruckPlunderChanged(generation);
            map317CommandService.ScanStatusChanged += manualMapScanStatusHandler;
            map317CommandService.PlayerMarkChanged += mapPlayerMarkHandler;
            map317CommandService.DispatchPlunderChanged += dispatchPlunderHandler;
            map317CommandService.TruckPlunderChanged += truckPlunderHandler;
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
        if (resourceAutomationConfigService is not null && resourceAutomationStatusHandler is not null)
            resourceAutomationConfigService.StatusChanged -= resourceAutomationStatusHandler;
        if (map317CommandService is not null)
        {
            if (manualMapScanStatusHandler is not null)
                map317CommandService.ScanStatusChanged -= manualMapScanStatusHandler;
            if (mapPlayerMarkHandler is not null)
                map317CommandService.PlayerMarkChanged -= mapPlayerMarkHandler;
            if (dispatchPlunderHandler is not null)
                map317CommandService.DispatchPlunderChanged -= dispatchPlunderHandler;
            if (truckPlunderHandler is not null)
                map317CommandService.TruckPlunderChanged -= truckPlunderHandler;
        }
        if (mapAutoScanService is not null && mapAutoScanHandler is not null)
            mapAutoScanService.StateChanged -= mapAutoScanHandler;
        overviewRecoveryHandler = null;
        resourceAutomationStatusHandler = null;
        manualMapScanStatusHandler = null;
        mapPlayerMarkHandler = null;
        dispatchPlunderHandler = null;
        truckPlunderHandler = null;
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
            string userDataDirectory = homeMapCampaignProofPath is not null
                ? Path.Combine(isolatedConfigRoot!, "webview-user-data")
                : Path.Combine(
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
                    suppressAutoLaunch: liveProbePath is not null || hostProbePath is not null || firstLiveResult is not null || ownerEvidence is not null || homeMapCampaignProofPath is not null),
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
            core.SetVirtualHostNameToFolderMapping("lwbridge.local", uiRootPath, CoreWebView2HostResourceAccessKind.DenyCors);
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
            if (homeMapCampaignProofPath is not null)
            {
                await core.AddScriptToExecuteOnDocumentCreatedAsync("""
                    (() => {
                      const issues = [];
                      window.__LWB317CampaignIssues = issues;
                      const originalError = console.error.bind(console);
                      console.error = (...args) => {
                        issues.push({ kind: 'console.error', message: args.map(value => String(value)).join(' ') });
                        originalError(...args);
                      };
                      window.addEventListener('error', event => {
                        issues.push({ kind: 'window.error', message: String(event.message || event.error || 'unknown') });
                      });
                      window.addEventListener('unhandledrejection', event => {
                        issues.push({ kind: 'unhandledrejection', message: String(event.reason?.message || event.reason || 'unknown') });
                      });
                    })();
                    """);
            }
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
            if (mapUiIntegrationProofPath is not null)
            {
                if (!string.Equals(initialView, "map-data", StringComparison.Ordinal))
                    throw new InvalidOperationException("--map-ui-integration-proof requires --view map-data.");
                await RunNormalUiProductionMapProofAsync(core, mapUiIntegrationProofPath, exerciseIntegrationAcceptance: true);
                Close();
                return;
            }
            if (homeMapCampaignProofPath is not null)
            {
                if (!string.Equals(initialView, "map-data", StringComparison.Ordinal))
                    throw new InvalidOperationException("--home-map-campaign-proof requires --view map-data.");
                await RunHomeMapCampaignProofAsync(core, homeMapCampaignProofPath);
                Close();
                return;
            }
            if (normalUiLiveMapProofPath is not null)
            {
                if (!string.Equals(initialView, "map-data", StringComparison.Ordinal))
                    throw new InvalidOperationException("--normal-ui-live-map-proof requires --view map-data.");
                await RunNormalUiProductionMapProofAsync(core, normalUiLiveMapProofPath);
                Close();
                return;
            }
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
            if (capturePath is null && liveProbePath is null && hostProbePath is null && normalUiLiveResourceProofPath is null && normalUiLiveMapProofPath is null && mapUiIntegrationProofPath is null && homeMapCampaignProofPath is null)
                MessageBox.Show(this, ex.Message, "LWBridge", MessageBoxButtons.OK, MessageBoxIcon.Error);
            else
            {
                string artifactPath = capturePath ?? liveProbePath ?? hostProbePath ?? normalUiLiveResourceProofPath ?? normalUiLiveMapProofPath ?? mapUiIntegrationProofPath ?? homeMapCampaignProofPath!;
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

    // R8-066: end-to-end proof of the normal production Map path. Unlike the
    // historical bounded resource helper proof above, this keeps the actual
    // Map317CommandService is wired into the recovered WebView and only
    // automates the same UI controls a user would press.
    private async Task RunNormalUiProductionMapProofAsync(
        CoreWebView2 core,
        string outputPath,
        bool exerciseIntegrationAcceptance = false)
    {
        if (map317CommandService is null || overviewLifecycleService is null)
            throw new InvalidOperationException("The normal Map Data window does not have the production scan/lifecycle services.");

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
            throw new InvalidOperationException("The normal production Map Data manual scan controls did not render.");

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
            throw new InvalidOperationException("The normal production Map Data resource-only selection could not be established.");

        using var operationCts = new CancellationTokenSource(TimeSpan.FromMinutes(8));
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

            object? integrationAcceptance = null;
            if (exerciseIntegrationAcceptance)
            {
                string cleanUiProjectJson = await core.ExecuteScriptAsync(
                    "document.querySelector('.app-shell')?.dataset.uiProject || null");
                string? cleanUiProject = JsonSerializer.Deserialize<string?>(cleanUiProjectJson);
                string bridgeModeJson = await core.ExecuteScriptAsync(
                    "document.querySelector('.panel.map-panel')?.dataset.bridgeMode || null");
                string? bridgeMode = JsonSerializer.Deserialize<string?>(bridgeModeJson);
                if (!string.Equals(cleanUiProject, "LWBridge.UI-0.3.17", StringComparison.Ordinal) ||
                    !string.Equals(bridgeMode, "native", StringComparison.Ordinal))
                    throw new InvalidDataException("The integration proof did not load the clean reconstructed UI through the native Desktop host.");

                async Task<NormalUiResourceProofSearchObservation> WaitForSearchAfter(long sequence, string label)
                {
                    for (int attempt = 0; attempt < 300; attempt++)
                    {
                        NormalUiResourceProofSearchObservation? found = ReadNormalUiProofSearchAfter(sequence);
                        if (found is not null) return found;
                        await Task.Delay(100, operationCts.Token);
                    }
                    throw new TimeoutException($"The integration UI {label} did not produce a native map_search response.");
                }

                static (int Total, JsonElement Rows) RequireSearchResult(
                    NormalUiResourceProofSearchObservation search,
                    string label)
                {
                    JsonElement root = search.Result;
                    if (root.ValueKind != JsonValueKind.Object ||
                        !root.TryGetProperty("total", out JsonElement totalValue) || !totalValue.TryGetInt32(out int foundTotal) ||
                        !root.TryGetProperty("rows", out JsonElement foundRows) || foundRows.ValueKind != JsonValueKind.Array)
                        throw new InvalidDataException($"The integration UI {label} returned a malformed map_search result.");
                    return (foundTotal, foundRows.Clone());
                }

                if (total <= 50)
                    throw new InvalidDataException($"The live Resource population ({total}) is too small to exercise recovered page-2 pagination.");
                var firstPageKeys = rows.EnumerateArray()
                    .Select(row => RequiredString(row, "recordKey"))
                    .ToHashSet(StringComparer.Ordinal);
                long beforePageTwo = GetNormalUiProofSearchSequence();
                string pageClick = await core.ExecuteScriptAsync("""
                    (() => {
                      const button = [...document.querySelectorAll('.map-pagination button')]
                        .find(candidate => candidate.textContent.trim() === 'Next');
                      if (!button || button.disabled) return false;
                      button.click();
                      return true;
                    })()
                    """);
                if (pageClick != "true")
                    throw new InvalidOperationException("The clean Resource UI did not expose an enabled Next pagination control.");
                NormalUiResourceProofSearchObservation pageTwo = await WaitForSearchAfter(beforePageTwo, "page-2 request");
                MapDataQueryOptions pageTwoQuery = MapDataQueryContract.NormalizeSearch(pageTwo.Payload);
                (int pageTwoTotal, JsonElement pageTwoRows) = RequireSearchResult(pageTwo, "page-2 request");
                if (pageTwoQuery.Kind != "resource" || pageTwoQuery.Page != 2 || pageTwoQuery.ServerId != completedServerId ||
                    pageTwoTotal != total || pageTwoRows.GetArrayLength() == 0)
                    throw new InvalidDataException("The clean Resource UI page-2 request/result did not preserve the recovered paging contract.");
                var pageTwoKeys = pageTwoRows.EnumerateArray()
                    .Select(row => RequiredString(row, "recordKey"))
                    .ToHashSet(StringComparer.Ordinal);
                if (firstPageKeys.Overlaps(pageTwoKeys))
                    throw new InvalidDataException("The clean Resource UI duplicated a page-1 record on page 2.");

                long beforeFilter = GetNormalUiProofSearchSequence();
                string selectedFilterJson = "null";
                for (int attempt = 0; attempt < 150; attempt++)
                {
                    selectedFilterJson = await core.ExecuteScriptAsync("""
                        (() => {
                          const select = document.querySelector('select[aria-label="Resource name"]');
                          if (!select) return null;
                          const option = [...select.options].find(candidate => candidate.value);
                          if (!option) return null;
                          select.value = option.value;
                          select.dispatchEvent(new Event('change', { bubbles: true }));
                          return option.value;
                        })()
                        """);
                    if (selectedFilterJson != "null") break;
                    await Task.Delay(100, operationCts.Token);
                }
                string? selectedFilter = JsonSerializer.Deserialize<string?>(selectedFilterJson);
                if (string.IsNullOrWhiteSpace(selectedFilter))
                    throw new InvalidDataException("The clean Resource UI did not expose a recovered Resource-name filter option.");
                NormalUiResourceProofSearchObservation filteredSearch = await WaitForSearchAfter(beforeFilter, "Resource-name filter");
                MapDataQueryOptions filteredQuery = MapDataQueryContract.NormalizeSearch(filteredSearch.Payload);
                (int filteredTotal, JsonElement filteredRows) = RequireSearchResult(filteredSearch, "Resource-name filter");
                if (filteredQuery.Kind != "resource" || filteredQuery.Page != 1 ||
                    filteredQuery.ServerId != completedServerId ||
                    !string.Equals(filteredQuery.ResourceNameKey, selectedFilter, StringComparison.Ordinal) ||
                    filteredTotal < 1 || filteredRows.GetArrayLength() == 0)
                    throw new InvalidDataException("The clean Resource UI Resource-name filter did not preserve the recovered query contract.");
                foreach (JsonElement filteredRow in filteredRows.EnumerateArray())
                {
                    if (!filteredRow.TryGetProperty("resourceNameKey", out JsonElement nameValue) ||
                        nameValue.ValueKind != JsonValueKind.String ||
                        !string.Equals(nameValue.GetString(), selectedFilter, StringComparison.Ordinal))
                        throw new InvalidDataException("The clean Resource UI Resource-name filter returned a row from another Resource name.");
                }

                long beforeClear = GetNormalUiProofSearchSequence();
                string clearClick = await core.ExecuteScriptAsync("""
                    (() => {
                      const button = [...document.querySelectorAll('.map-header .map-actions button')]
                        .find(candidate => candidate.textContent.trim() === 'Clear Map Data');
                      if (!button || button.disabled) return false;
                      button.click();
                      return true;
                    })()
                    """);
                if (clearClick != "true")
                    throw new InvalidOperationException("The clean Resource UI Clear Map Data button could not be clicked.");

                JsonElement clearedStatus = default;
                bool clearCompleted = false;
                for (int attempt = 0; attempt < 300; attempt++)
                {
                    clearedStatus = JsonSerializer.SerializeToElement(map317CommandService.CreateStatus(), JsonOptions.Default);
                    bool reading = clearedStatus.TryGetProperty("isReading", out JsonElement readingValue) && readingValue.ValueKind == JsonValueKind.True;
                    if (!reading && (ReadStatusInt32(clearedStatus, "serverId") ?? -1) == 0)
                    {
                        clearCompleted = true;
                        break;
                    }
                    await Task.Delay(100, operationCts.Token);
                }
                if (!clearCompleted)
                    throw new TimeoutException("The clean Resource UI clear request did not reach the backend's cleared idle state.");

                NormalUiResourceProofSearchObservation clearedSearch = await WaitForSearchAfter(beforeClear, "post-clear Resource refresh");
                MapDataQueryOptions clearedQuery = MapDataQueryContract.NormalizeSearch(clearedSearch.Payload);
                (int clearedTotal, JsonElement clearedRows) = RequireSearchResult(clearedSearch, "post-clear Resource refresh");
                if (clearedQuery.Kind != "resource" || clearedQuery.ServerId != completedServerId ||
                    clearedTotal != 0 || clearedRows.GetArrayLength() != 0)
                    throw new InvalidDataException("The clean Resource UI retained persisted Resource rows after map_scan_clear.");

                string clearedDomJson = await core.ExecuteScriptAsync("""
                    (() => {
                      const tab = document.querySelectorAll('.map-tabs button')[1];
                      const table = document.querySelector('.map-table--resource');
                      return {
                        count: tab?.querySelector('.map-tab-count')?.textContent?.trim() || null,
                        empty: !!table?.querySelector('tbody td.map-empty'),
                        busy: table?.getAttribute('aria-busy') === 'true'
                      };
                    })()
                    """);
                using JsonDocument clearedDomDoc = JsonDocument.Parse(clearedDomJson);
                JsonElement clearedDom = clearedDomDoc.RootElement.Clone();
                if (!clearedDom.TryGetProperty("count", out JsonElement countValue) || countValue.GetString() != "0" ||
                    !clearedDom.TryGetProperty("empty", out JsonElement emptyValue) || emptyValue.ValueKind != JsonValueKind.True ||
                    !clearedDom.TryGetProperty("busy", out JsonElement busyValue) || busyValue.ValueKind != JsonValueKind.False)
                    throw new InvalidDataException("The clean Resource UI did not render zero count/empty rows after backend clear.");

                await overviewLifecycleService.WaitForHealthyMapScanSessionAsync(
                    ownedMapSession,
                    operationCts.Token).ConfigureAwait(false);
                int? postClearServerId = overviewLifecycleService.GetLiveServerId();
                if (postClearServerId != completedServerId)
                    throw new InvalidDataException(
                        $"The clean Resource UI clear preserved Map readiness but live server {postClearServerId?.ToString() ?? "null"} did not match scan server {completedServerId}.");

                string uiConnectionText = string.Empty;
                for (int attempt = 0; attempt < 100; attempt++)
                {
                    string textJson = await core.ExecuteScriptAsync(
                        "document.querySelector('.status-card.status-online strong')?.textContent?.trim() || ''");
                    uiConnectionText = JsonSerializer.Deserialize<string?>(textJson) ?? string.Empty;
                    if (string.Equals(uiConnectionText, "Connected", StringComparison.Ordinal)) break;
                    await Task.Delay(100, operationCts.Token);
                }
                if (!string.Equals(uiConnectionText, "Connected", StringComparison.Ordinal))
                    throw new InvalidDataException(
                        $"Map readiness remained healthy after clear, but the clean UI connection indicator settled at '{uiConnectionText}'.");

                string postClearScreenshotPath = Path.Combine(directory, stem + "-post-clear.png");
                await using (var postClearOutput = File.Create(postClearScreenshotPath))
                    await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, postClearOutput);

                integrationAcceptance = new
                {
                    cleanUiProject,
                    bridgeMode,
                    pagination = new
                    {
                        pageTwo.RequestId,
                        pageTwo.Payload,
                        total = pageTwoTotal,
                        rows = pageTwoRows.GetArrayLength(),
                        duplicateKeysFromPageOne = 0,
                    },
                    resourceFilter = new
                    {
                        resourceNameKey = selectedFilter,
                        filteredSearch.RequestId,
                        filteredSearch.Payload,
                        total = filteredTotal,
                        rows = filteredRows.GetArrayLength(),
                    },
                    clear = new
                    {
                        status = clearedStatus,
                        clearedSearch.RequestId,
                        clearedSearch.Payload,
                        total = clearedTotal,
                        rendered = clearedDom,
                        mapSessionMatches = overviewLifecycleService.MatchesOwnedMapScanSession(ownedMapSession),
                        liveServerId = postClearServerId,
                        uiConnectionText,
                        screenshotPath = postClearScreenshotPath,
                    },
                };
            }

            await File.WriteAllTextAsync(fullOutputPath, JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                checkpoint = exerciseIntegrationAcceptance ? "LWB317-MAP-UI-INTEGRATION-001" : "LWB-R8-066",
                state = "proven",
                proof = exerciseIntegrationAcceptance
                    ? "clean_reconstructed_map_ui_live_backend_scan_search_page_filter_clear"
                    : "normal_production_map_window_start_scan_search_render",
                windowMode = "persistent_normal_map_data",
                profileId = backend.ProfileId,
                instanceId,
                connectionState = ReadStatusString(startJson, "connectionState"),
                gamePid = expected.GamePid,
                selectedTypes = new[] { "resource" },
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
                integrationAcceptance,
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
        if (normalUiLiveResourceProofPath is null &&
            normalUiLiveMapProofPath is null &&
            mapUiIntegrationProofPath is null)
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
        status.TryGetProperty(name, out JsonElement value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt64(out long parsed)
            ? parsed
            : null;

    private static int? ReadStatusInt32(JsonElement status, string name) =>
        status.TryGetProperty(name, out JsonElement value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out int parsed)
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

    private Task<HomeMapCampaignDelayedRequest> ArmHomeMapCampaignCommandDelay(string command)
    {
        lock (homeMapCampaignCommandGate)
        {
            if (homeMapCampaignDelayedCommand is not null)
                throw new InvalidOperationException("A campaign command delay is already armed.");
            homeMapCampaignDelayedCommand = command;
            homeMapCampaignDelayedObservation = null;
            homeMapCampaignDelayedEntered = new TaskCompletionSource<HomeMapCampaignDelayedRequest>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            homeMapCampaignDelayedRelease = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            return homeMapCampaignDelayedEntered.Task;
        }
    }

    private void ReleaseHomeMapCampaignCommandDelay()
    {
        TaskCompletionSource? release;
        lock (homeMapCampaignCommandGate)
            release = homeMapCampaignDelayedRelease;
        release?.TrySetResult();
    }

    private async Task WaitForHomeMapCampaignCommandReleaseAsync(
        string command,
        string profileId,
        long generation,
        CancellationToken cancellationToken)
    {
        Task? releaseTask = null;
        HomeMapCampaignDelayedRequest? observation = null;
        lock (homeMapCampaignCommandGate)
        {
            if (homeMapCampaignProofPath is null ||
                !string.Equals(homeMapCampaignDelayedCommand, command, StringComparison.Ordinal))
                return;

            homeMapCampaignDelayedCommand = null;
            observation = new HomeMapCampaignDelayedRequest(
                command,
                profileId,
                generation,
                Cancelled: false);
            homeMapCampaignDelayedObservation = observation;
            homeMapCampaignDelayedEntered?.TrySetResult(observation);
            releaseTask = homeMapCampaignDelayedRelease?.Task;
        }

        if (releaseTask is null) return;
        try
        {
            await releaseTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            lock (homeMapCampaignCommandGate)
            {
                if (observation is not null)
                    homeMapCampaignDelayedObservation = observation with { Cancelled = true };
            }
            throw;
        }
        finally
        {
            lock (homeMapCampaignCommandGate)
            {
                homeMapCampaignDelayedEntered = null;
                homeMapCampaignDelayedRelease = null;
            }
        }
    }

    private HomeMapCampaignDelayedRequest? GetHomeMapCampaignDelayedObservation()
    {
        lock (homeMapCampaignCommandGate)
            return homeMapCampaignDelayedObservation;
    }

    private void RejectNextHomeMapCampaignCommand(string command)
    {
        lock (homeMapCampaignCommandGate)
            homeMapCampaignRejectedCommand = command;
    }

    private void ThrowIfHomeMapCampaignCommandRejected(string command)
    {
        lock (homeMapCampaignCommandGate)
        {
            if (!string.Equals(homeMapCampaignRejectedCommand, command, StringComparison.Ordinal))
                return;
            homeMapCampaignRejectedCommand = null;
        }
        throw new BridgeCommandException(
            "CAMPAIGN_PROOF_REJECTED",
            "Isolated campaign provider rejected the requested status observation.");
    }

    private static void SeedHomeMapCampaignProofDatabase(
        string databasePath,
        int serverId,
        string suffix)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        string runId = "isolated-home-map-campaign-" + suffix;
        using var store = new LWBridge.Map317.MapStore(databasePath);
        store.InsertScanRun(new LWBridge.Map317.MapScanRun(
            runId, serverId, LWBridge.Map317.MapKinds.All, "running", 1, 0, 0, now, now, null));

        LWBridge.Map317.MapRecord Record(
            string kind,
            string key,
            string uuid,
            string name,
            int pointIndex,
            object data) =>
            new(
                kind, serverId, key, pointIndex, uuid, name, null,
                kind is "city" or "resource" or "monster" or "dispatch" or "ghost" ? 10 : null,
                kind is "truck" or "railway" or "dispatch" or "ghost" ? 4 : null,
                null, pointIndex, null, now + pointIndex,
                JsonSerializer.Serialize(data, JsonOptions.Default));

        LWBridge.Map317.MapRecord[] rows =
        [
            Record("city", $"campaign-city-{suffix}", $"city-{suffix}", $"Campaign City {suffix}", 1, new
            {
                serverId, recordKey = $"campaign-city-{suffix}", x = 101, y = 202,
                ownerName = $"Campaign City {suffix}", ownerUid = $"campaign-owner-{suffix}", uuid = $"city-{suffix}",
                allianceName = "ISO", level = 30, health = 999999, updatedAt = now + 1,
            }),
            Record("resource", $"campaign-resource-{suffix}", $"resource-{suffix}", $"Campaign Resource {suffix}", 2, new
            {
                serverId, recordKey = $"campaign-resource-{suffix}", uuid = $"resource-{suffix}",
                resourceNameKey = "resource.iron", level = 10, updatedAt = now + 2,
            }),
            Record("monster", $"campaign-monster-{suffix}", $"monster-{suffix}", $"Campaign Monster {suffix}", 3, new
            {
                serverId, recordKey = $"campaign-monster-{suffix}", uuid = $"monster-{suffix}",
                monsterNameKey = "monster.doom", level = 20, updatedAt = now + 3,
            }),
            Record("truck", $"campaign-truck-{suffix}", $"truck-{suffix}", $"Campaign Truck {suffix}", 4, new
            {
                serverId, recordKey = $"campaign-truck-{suffix}", uuid = $"truck-{suffix}", quality = 4,
                remainingLootCount = 2, maxLootCount = 2, robTimes = 0, updatedAt = now + 4,
            }),
            Record("railway", $"campaign-railway-{suffix}", $"railway-{suffix}", $"Campaign Train {suffix}", 5, new
            {
                serverId, recordKey = $"campaign-railway-{suffix}", uuid = $"railway-{suffix}", quality = 4,
                remainingLootCount = 1, maxLootCount = 2, robTimes = 1, updatedAt = now + 5,
            }),
            Record("dispatch", $"campaign-dispatch-{suffix}", $"dispatch-{suffix}", $"Campaign Dispatch {suffix}", 6, new
            {
                serverId, recordKey = $"campaign-dispatch-{suffix}", uuid = $"dispatch-{suffix}", level = 5,
                quality = 4, completionTime = now - 1_000, taskExpireTime = now + 60_000,
                stolenCount = 0, maxStealCount = 2, updatedAt = now + 6,
            }),
            Record("ghost", $"campaign-ghost-{suffix}", $"ghost-{suffix}", $"Campaign Ghost {suffix}", 7, new
            {
                serverId, recordKey = $"campaign-ghost-{suffix}", uuid = $"ghost-{suffix}", level = 6,
                quality = 4, completionTime = now + 10_000, updatedAt = now + 7,
            }),
            Record("treasure", $"campaign-treasure-{suffix}", $"treasure-{suffix}", $"Campaign Treasure {suffix}", 8, new
            {
                serverId, recordKey = $"campaign-treasure-{suffix}", uuid = $"treasure-{suffix}",
                treasureType = 5, suppliesType = 0, treasureNameKey = "treasure.five",
                complete = true, updatedAt = now + 8,
            }),
        ];
        foreach (LWBridge.Map317.MapRecord row in rows)
            store.StageRecord(runId, row);
        store.UpdateScanProgress(runId, 1, 0, null, now + 20);
        store.CompleteScan(runId, now + 21);
    }

    private async Task RunHomeMapCampaignProofAsync(CoreWebView2 core, string outputPath)
    {
        if (map317CommandService is null || mapAutoScanService is null || isolatedConfigRoot is null)
            throw new InvalidOperationException("The isolated Home/Map campaign proof services were not composed.");

        async Task WaitForDomAsync(string expression, string label, int attempts = 200)
        {
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                try
                {
                    if (await core.ExecuteScriptAsync($"Boolean({expression})") == "true") return;
                }
                catch (InvalidOperationException) when (attempt < attempts - 1)
                {
                    // A controlled reload makes ExecuteScriptAsync briefly unavailable.
                }
                await Task.Delay(50);
            }
            throw new TimeoutException("Campaign UI did not reach: " + label);
        }

        async Task<JsonElement> ReadDomAsync(string expression)
        {
            string value = await core.ExecuteScriptAsync(expression);
            using JsonDocument document = JsonDocument.Parse(value);
            return document.RootElement.Clone();
        }

        async Task RequireUiActionAsync(string script, string label)
        {
            if (await core.ExecuteScriptAsync(script) != "true")
                throw new InvalidOperationException("Campaign UI control was unavailable: " + label);
        }

        async Task WaitForRequestsToDrainAsync(string label)
        {
            for (int attempt = 0; attempt < 200; attempt++)
            {
                if (documentSession.Requests.ActiveCount == 0) return;
                await Task.Delay(25);
            }
            throw new TimeoutException("Campaign native requests did not drain after " + label + ".");
        }

        async Task WaitForProfileAsync(string profileId, string label)
        {
            for (int attempt = 0; attempt < 200; attempt++)
            {
                if (string.Equals(backend.ProfileId, profileId, StringComparison.Ordinal)) break;
                await Task.Delay(25);
            }
            if (!string.Equals(backend.ProfileId, profileId, StringComparison.Ordinal))
                throw new TimeoutException("Campaign native owner did not switch to " + profileId + " during " + label + ".");
            string displayNameJson = JsonSerializer.Serialize(
                string.Equals(profileId, "campaign-A", StringComparison.Ordinal) ? "Campaign A" : "Campaign B",
                JsonOptions.Default);
            await WaitForDomAsync(
                $"[...document.querySelectorAll('.profile-compact-item')].some(button => button.classList.contains('active') && button.querySelector('strong')?.textContent?.includes({displayNameJson}))",
                label + " active profile control");
        }

        async Task SelectProfileAsync(string displayName, string profileId, string label)
        {
            string nameJson = JsonSerializer.Serialize(displayName, JsonOptions.Default);
            await RequireUiActionAsync($$"""
                (() => {
                  const button = [...document.querySelectorAll('.profile-compact-item')]
                    .find(item => item.querySelector('strong')?.textContent?.includes({{nameJson}}));
                  if (!button || button.disabled) return false;
                  button.click();
                  return true;
                })()
                """, label);
            await WaitForProfileAsync(profileId, label);
        }

        async Task SelectMapTabAsync(int index, string kind)
        {
            await RequireUiActionAsync($$"""
                (() => {
                  const button = document.querySelectorAll('.map-tabs button')[{{index}}];
                  if (!button) return false;
                  button.click();
                  return true;
                })()
                """, "Map tab " + kind);
            await WaitForDomAsync(
                $"document.querySelectorAll('.map-tabs button')[{index}]?.getAttribute('aria-selected') === 'true' && document.querySelector('.map-table--{kind}')",
                "Map tab " + kind + " selection");
        }

        async Task SetAutoIntervalAsync(int interval, int serverId, string label)
        {
            await RequireUiActionAsync("""
                (() => {
                  const button = document.querySelectorAll('.map-scan-tabs button')[1];
                  if (!button) return false;
                  button.click();
                  return true;
                })()
                """, label + " Auto tab");
            await WaitForDomAsync("!!document.querySelector('.map-auto-scan-card')", label + " Auto card");
            await RequireUiActionAsync($$"""
                (() => {
                  const interval = document.querySelector('.map-auto-scan-grid input[type="number"]');
                  if (!interval) return false;
                  const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')?.set;
                  setter?.call(interval, '{{interval}}');
                  interval.dispatchEvent(new Event('input', { bubbles: true }));
                  return true;
                })()
                """, label + " interval control");
            await RequireUiActionAsync($$"""
                (() => {
                  const input = document.querySelector('.map-auto-scan-server-input input');
                  const add = document.querySelector('.map-auto-scan-server-input button');
                  if (!input || !add) return false;
                  const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')?.set;
                  setter?.call(input, '{{serverId}}');
                  input.dispatchEvent(new Event('input', { bubbles: true }));
                  input.dispatchEvent(new Event('change', { bubbles: true }));
                  return true;
                })()
                """, label + " target server input");
            await WaitForDomAsync(
                $"document.querySelector('.map-auto-scan-server-input button')?.disabled === false",
                label + " target server admission");
            await RequireUiActionAsync("""
                (() => {
                  const add = document.querySelector('.map-auto-scan-server-input button');
                  if (!add || add.disabled) return false;
                  add.click();
                  return true;
                })()
                """, label + " target server Add");

            for (int attempt = 0; attempt < 200; attempt++)
            {
                MapAutoScanSnapshot snapshot = await mapAutoScanService!.GetSnapshotAsync().ConfigureAwait(true);
                if (snapshot.Config.IntervalMinutes == interval && snapshot.Config.ServerIds?.Contains(serverId) == true)
                    return;
                await Task.Delay(25);
            }
            throw new TimeoutException(label + " Auto config did not persist through the native service.");
        }

        async Task ClickMapSearchAsync(string label)
        {
            await RequireUiActionAsync("""
                (() => {
                  const button = document.querySelector('.map-searchbar > button');
                  if (!button || button.disabled) return false;
                  button.click();
                  return true;
                })()
                """, label);
        }

        async Task<JsonElement> VisitRouteAsync(int index, string key, string readyExpression)
        {
            await RequireUiActionAsync($$"""
                (() => {
                  const button = document.querySelectorAll('.side-nav button')[{{index}}];
                  if (!button || button.disabled) return false;
                  button.click();
                  return true;
                })()
                """, "route " + key);
            await WaitForDomAsync(
                $"document.querySelectorAll('.side-nav button')[{index}]?.getAttribute('aria-current') === 'page' && Boolean({readyExpression})",
                "route " + key + " rendered");
            return await ReadDomAsync($$"""
                (() => ({
                  key: '{{key}}',
                  label: document.querySelectorAll('.side-nav .nav-label')[{{index}}]?.textContent?.trim() || '',
                  active: document.querySelectorAll('.side-nav button')[{{index}}]?.getAttribute('aria-current') === 'page'
                }))()
                """);
        }

        string expectedLanguage = string.IsNullOrWhiteSpace(language)
            ? homeMapCampaignNarrow ? "ja" : "en"
            : language;
        string expectedTheme = string.IsNullOrWhiteSpace(theme)
            ? homeMapCampaignNarrow ? "dark" : "light"
            : theme;
        if (expectedLanguage is not ("en" or "ja"))
            throw new InvalidOperationException("Campaign packaged proof requires --language en or --language ja.");
        if (expectedTheme is not ("light" or "dark"))
            throw new InvalidOperationException("Campaign packaged proof requires --theme light or --theme dark.");

        await WaitForDomAsync(
            "!!document.querySelector('.app-shell') && document.querySelector('.panel.map-panel')?.dataset.bridgeMode === 'native' && document.querySelectorAll('.profile-compact-item').length === 2",
            "packaged native Map UI and native profile controls");

        string languageJson = JsonSerializer.Serialize(expectedLanguage, JsonOptions.Default);
        await RequireUiActionAsync($$"""
            (() => {
              const select = document.querySelector('.language-select select');
              if (!select) return false;
              select.value = {{languageJson}};
              select.dispatchEvent(new Event('change', { bubbles: true }));
              return true;
            })()
            """, "language select");
        await WaitForDomAsync(
            $"document.documentElement.lang === {languageJson} && document.querySelector('.language-select select')?.value === {languageJson}",
            "actual " + expectedLanguage + " locale");

        string initialTheme = JsonSerializer.Deserialize<string>(
            await core.ExecuteScriptAsync("document.documentElement.dataset.theme || ''")) ?? string.Empty;
        int themeControlClicks = 0;
        async Task ClickThemeAsync()
        {
            await RequireUiActionAsync("""
                (() => {
                  const button = document.querySelector('.theme-toggle');
                  if (!button || button.disabled) return false;
                  button.click();
                  return true;
                })()
                """, "theme toggle");
            themeControlClicks++;
        }
        if (string.Equals(initialTheme, expectedTheme, StringComparison.Ordinal))
        {
            await ClickThemeAsync();
            await WaitForDomAsync($"document.documentElement.dataset.theme !== '{expectedTheme}'", "theme toggle away from target");
        }
        await ClickThemeAsync();
        await WaitForDomAsync(
            $"document.documentElement.dataset.theme === '{expectedTheme}' && document.querySelector('.theme-toggle')?.getAttribute('aria-pressed') === '{(expectedTheme == "dark" ? "true" : "false")}'",
            "actual " + expectedTheme + " theme");

        JsonElement shellSnapshot = await ReadDomAsync("""
            (() => ({
              uiProject: document.querySelector('.app-shell')?.dataset.uiProject || '',
              bridgeMode: document.querySelector('.panel.map-panel')?.dataset.bridgeMode || '',
              language: document.documentElement.lang || '',
              languageControl: document.querySelector('.language-select select')?.value || '',
              theme: document.documentElement.dataset.theme || '',
              themePressed: document.querySelector('.theme-toggle')?.getAttribute('aria-pressed') || '',
              navigation: [...document.querySelectorAll('.side-nav .nav-label')].map(node => node.textContent?.trim() || ''),
              viewport: { width: window.innerWidth, height: window.innerHeight, scrollWidth: document.documentElement.scrollWidth }
            }))()
            """);
        if (shellSnapshot.GetProperty("uiProject").GetString() != "LWBridge.UI-0.3.17" ||
            shellSnapshot.GetProperty("bridgeMode").GetString() != "native" ||
            shellSnapshot.GetProperty("language").GetString() != expectedLanguage ||
            shellSnapshot.GetProperty("theme").GetString() != expectedTheme)
            throw new InvalidDataException("Campaign proof did not converge the actual packaged UI locale/theme/native bridge state.");
        string mapNavText = shellSnapshot.GetProperty("navigation")[2].GetString() ?? string.Empty;
        if ((expectedLanguage == "en" && mapNavText != "Map Data") ||
            (expectedLanguage == "ja" && (string.IsNullOrWhiteSpace(mapNavText) || mapNavText == "Map Data")))
            throw new InvalidDataException("Campaign proof locale selector did not change rendered navigation text.");

        long firstAProfileGeneration = Volatile.Read(ref profileRuntimeGeneration);
        if (backend.ProfileId != "campaign-A")
            throw new InvalidDataException("Campaign proof did not start with profile A as the native owner.");

        var routeProof = new List<JsonElement>();
        routeProof.Add(await VisitRouteAsync(0, "overview", "document.querySelector('.quick-actions-panel')"));
        routeProof.Add(await VisitRouteAsync(1, "automation", "document.querySelector('.automation-categories')"));
        routeProof.Add(await VisitRouteAsync(2, "map-data", "document.querySelector('.panel.map-panel')"));
        routeProof.Add(await VisitRouteAsync(3, "march", "document.querySelector('.squad-panel')"));
        routeProof.Add(await VisitRouteAsync(4, "city-layout", "document.querySelector('.city-layout-panel, .city-layout-empty')"));
        routeProof.Add(await VisitRouteAsync(5, "hotkeys", "document.querySelector('.hotkey-panel[data-hotkey-category=\"hotkeys\"]')"));
        routeProof.Add(await VisitRouteAsync(6, "mini-games", "document.querySelector('.hotkey-panel[data-hotkey-category=\"miniGames\"]')"));
        routeProof.Add(await VisitRouteAsync(7, "settings", "document.querySelector('.settings-panel')"));
        routeProof.Add(await VisitRouteAsync(2, "map-data-return", "document.querySelector('.panel.map-panel')"));

        string[] mapKinds = ["city", "resource", "monster", "truck", "railway", "dispatch", "ghost", "treasure"];
        var tabProof = new List<object>();
        for (int index = 0; index < mapKinds.Length; index++)
        {
            string kind = mapKinds[index];
            await SelectMapTabAsync(index, kind);
            await WaitForDomAsync(
                $"document.querySelectorAll('.map-tabs .map-tab-count')[{index}]?.textContent?.trim() === '1' && document.querySelector('.map-table--{kind} tbody tr')",
                "profile A " + kind + " row");
            JsonElement tab = await ReadDomAsync($$"""
                (() => ({
                  kind: '{{kind}}',
                  label: document.querySelectorAll('.map-tabs .map-tab-label')[{{index}}]?.textContent?.trim() || '',
                  count: document.querySelectorAll('.map-tabs .map-tab-count')[{{index}}]?.textContent?.trim() || '',
                  firstRow: document.querySelector('.map-table--{{kind}} tbody tr')?.innerText || ''
                }))()
                """);
            tabProof.Add(tab);
        }

        await RequireUiActionAsync("""
            (() => {
              const button = document.querySelectorAll('.side-nav button')[0];
              if (!button) return false;
              button.click();
              return true;
            })()
            """, "Home navigation control");
        await WaitForDomAsync("!!document.querySelector('.quick-actions-panel .toggle-row')", "Home Auto Launch control");
        await WaitForDomAsync(
            "document.querySelectorAll('.quick-actions-panel .toggle-row')[0]?.getAttribute('aria-checked') === 'true'",
            "profile A Auto Launch initial state");
        Task<HomeMapCampaignDelayedRequest> delayedAutoLaunchEntered = ArmHomeMapCampaignCommandDelay("local_config_set");
        await RequireUiActionAsync("""
            (() => {
              const toggle = document.querySelectorAll('.quick-actions-panel .toggle-row')[0];
              if (!toggle || toggle.disabled) return false;
              toggle.click();
              return true;
            })()
            """, "Home Auto Launch toggle");
        HomeMapCampaignDelayedRequest delayedAutoLaunch =
            await delayedAutoLaunchEntered.WaitAsync(TimeSpan.FromSeconds(5));
        if (delayedAutoLaunch.ProfileGeneration != firstAProfileGeneration || delayedAutoLaunch.ProfileId != "campaign-A")
            throw new InvalidDataException("Profile A Auto Launch save was not captured against the first-A owner generation.");
        await WaitForDomAsync(
            "document.querySelectorAll('.quick-actions-panel .toggle-row')[0]?.getAttribute('aria-checked') === 'false'",
            "profile A optimistic Auto Launch false");

        await SelectProfileAsync("Campaign B", "campaign-B", "Auto Launch A to B while A save is deferred");
        await WaitForDomAsync(
            "document.querySelectorAll('.quick-actions-panel .toggle-row')[0]?.getAttribute('aria-checked') === 'true'",
            "profile B Auto Launch remains true while A save is deferred");
        if (activeProfileConfig.Snapshot.AutoLaunchGame != true)
            throw new InvalidDataException("Profile B native Auto Launch preference was corrupted before old A acknowledgement.");
        ReleaseHomeMapCampaignCommandDelay();
        await WaitForRequestsToDrainAsync("old profile A Auto Launch acknowledgement");
        await Task.Delay(100);
        JsonElement profileBAfterOldAutoLaunch = await ReadDomAsync("""
            (() => ({
              checked: document.querySelectorAll('.quick-actions-panel .toggle-row')[0]?.getAttribute('aria-checked') || '',
              activeProfile: [...document.querySelectorAll('.profile-compact-item')].find(button => button.classList.contains('active'))?.querySelector('strong')?.textContent?.trim() || ''
            }))()
            """);
        if (profileBAfterOldAutoLaunch.GetProperty("checked").GetString() != "true" ||
            activeProfileConfig.Snapshot.AutoLaunchGame != true)
            throw new InvalidDataException("Late profile A Auto Launch acknowledgement overwrote profile B intent.");

        await SelectProfileAsync("Campaign A", "campaign-A", "Auto Launch B to A persistence check");
        long initialProfileGeneration = Volatile.Read(ref profileRuntimeGeneration);
        if (initialProfileGeneration <= firstAProfileGeneration)
            throw new InvalidDataException("Profile A Auto Launch persistence check did not replace the native owner generation.");
        await WaitForDomAsync(
            "document.querySelectorAll('.quick-actions-panel .toggle-row')[0]?.getAttribute('aria-checked') === 'false'",
            "returned profile A persisted Auto Launch false");
        if (activeProfileConfig.Snapshot.AutoLaunchGame)
            throw new InvalidDataException("Profile A deferred Auto Launch save did not persist to its own native config.");

        RejectNextHomeMapCampaignCommand("local_config_set");
        await RequireUiActionAsync("""
            (() => {
              const toggle = document.querySelectorAll('.quick-actions-panel .toggle-row')[0];
              if (!toggle || toggle.disabled) return false;
              toggle.click();
              return true;
            })()
            """, "Home Auto Launch rejected save control");
        await WaitForDomAsync(
            "document.querySelectorAll('.quick-actions-panel .toggle-row')[0]?.getAttribute('aria-checked') === 'false' && !!document.querySelector('.game-root-error')",
            "profile A rejected Auto Launch rolls back to committed false");
        if (activeProfileConfig.Snapshot.AutoLaunchGame)
            throw new InvalidDataException("Rejected profile A Auto Launch save mutated the native committed preference.");
        JsonElement rejectedAutoLaunchDom = await ReadDomAsync("""
            (() => ({
              checked: document.querySelectorAll('.quick-actions-panel .toggle-row')[0]?.getAttribute('aria-checked') || '',
              error: document.querySelector('.game-root-error')?.textContent?.trim() || ''
            }))()
            """);

        await RequireUiActionAsync("""
            (() => {
              const button = document.querySelectorAll('.side-nav button')[2];
              if (!button) return false;
              button.click();
              return true;
            })()
            """, "Map navigation control");
        await WaitForDomAsync("!!document.querySelector('.panel.map-panel')", "Map return after Home edit");
        await SetAutoIntervalAsync(45, 317, "profile A");
        int scanStartsBeforeAuto = Volatile.Read(ref homeMapCampaignScanStartCount);
        int scanStopsBeforeAuto = Volatile.Read(ref homeMapCampaignScanStopCount);
        await RequireUiActionAsync("""
            (() => {
              const input = document.querySelector('.map-auto-scan-master input[type="checkbox"]');
              if (!input || input.disabled || input.checked) return false;
              input.click();
              return true;
            })()
            """, "profile A enable Auto Scan control");
        bool autoStarted = false;
        for (int attempt = 0; attempt < 200; attempt++)
        {
            MapAutoScanSnapshot snapshot = await mapAutoScanService.GetSnapshotAsync().ConfigureAwait(true);
            if (snapshot.Running && snapshot.OwnsActiveScan && map317CommandService.IsScanActive &&
                Volatile.Read(ref homeMapCampaignScanStartCount) > scanStartsBeforeAuto)
            {
                autoStarted = true;
                break;
            }
            await Task.Delay(25);
        }
        if (!autoStarted)
            throw new TimeoutException("Real packaged Auto Scan control did not start the inert native provider.");
        await RequireUiActionAsync("""
            (() => {
              const input = document.querySelector('.map-auto-scan-master input[type="checkbox"]');
              if (!input || input.disabled || !input.checked) return false;
              input.click();
              return true;
            })()
            """, "profile A disable running Auto Scan control");
        await mapAutoScanService.WaitForIdleAsync(new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token)
            .ConfigureAwait(true);
        MapAutoScanSnapshot canceledAutoSnapshot = await mapAutoScanService.GetSnapshotAsync().ConfigureAwait(true);
        if (canceledAutoSnapshot.Config.Enabled || canceledAutoSnapshot.Running || map317CommandService.IsScanActive ||
            Volatile.Read(ref homeMapCampaignScanStopCount) <= scanStopsBeforeAuto)
            throw new InvalidDataException("Packaged Auto Scan cancellation did not retire the exact inert provider run.");
        await WaitForDomAsync(
            "document.querySelector('.status-card.status-online')?.classList.contains('online') === true",
            "fresh paired status connected before deferred result");
        JsonElement connectedStatusDom = await ReadDomAsync("""
            (() => ({ text: document.querySelector('.status-card.status-online strong')?.textContent?.trim() || '', online: document.querySelector('.status-card.status-online')?.classList.contains('online') === true }))()
            """);
        int messagesBeforeStatusProof = postedWebMessageCount;

        Task<HomeMapCampaignDelayedRequest> pendingStatus = ArmHomeMapCampaignCommandDelay("get_status");
        await RequireUiActionAsync("""
            (() => {
              const buttons = [...document.querySelectorAll('.top-actions button.top-action.secondary')];
              const refresh = buttons.at(-1);
              if (!refresh || refresh.disabled) return false;
              refresh.click();
              return true;
            })()
            """, "Refresh status control for deferred result");
        HomeMapCampaignDelayedRequest deferredStatus = await pendingStatus.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitForDomAsync(
            "!document.querySelector('.status-card.status-online')?.classList.contains('online')",
            "deferred status invalidates connected state");
        JsonElement deferredStatusDom = await ReadDomAsync("""
            (() => ({ text: document.querySelector('.status-card.status-online strong')?.textContent?.trim() || '', online: document.querySelector('.status-card.status-online')?.classList.contains('online') === true }))()
            """);
        ReleaseHomeMapCampaignCommandDelay();
        await WaitForRequestsToDrainAsync("deferred status release");

        RejectNextHomeMapCampaignCommand("proxy_status");
        await RequireUiActionAsync("""
            (() => {
              const refresh = [...document.querySelectorAll('.top-actions button.top-action.secondary')].at(-1);
              if (!refresh || refresh.disabled) return false;
              refresh.click();
              return true;
            })()
            """, "Refresh status control for rejection");
        await WaitForDomAsync(
            "document.querySelector('.status-card.status-online strong')?.textContent?.trim() === 'Unavailable'",
            "rejected paired status becomes unavailable");
        JsonElement rejectedStatusDom = await ReadDomAsync("""
            (() => ({ text: document.querySelector('.status-card.status-online strong')?.textContent?.trim() || '', online: document.querySelector('.status-card.status-online')?.classList.contains('online') === true }))()
            """);
        string deferredStatusTextJson = JsonSerializer.Serialize(
            deferredStatusDom.GetProperty("text").GetString() ?? string.Empty,
            JsonOptions.Default);
        string rejectedStatusTextJson = JsonSerializer.Serialize(
            rejectedStatusDom.GetProperty("text").GetString() ?? string.Empty,
            JsonOptions.Default);
        homeMapCampaignConnected = false;
        await RequireUiActionAsync("""
            (() => {
              const refresh = [...document.querySelectorAll('.top-actions button.top-action.secondary')].at(-1);
              if (!refresh || refresh.disabled) return false;
              refresh.click();
              return true;
            })()
            """, "Refresh status control for disconnected state");
        await WaitForRequestsToDrainAsync("disconnected status refresh");
        string disconnectedStatusPredicate =
            "(() => { const card = document.querySelector('.status-card.status-online'); " +
            "const text = card?.querySelector('strong')?.textContent?.trim() || ''; " +
            "return text.length > 0" +
            $" && text !== {deferredStatusTextJson}" +
            $" && text !== {rejectedStatusTextJson}" +
            " && !card?.classList.contains('online'); })()";
        await WaitForDomAsync(
            disconnectedStatusPredicate,
            "fresh paired status disconnected/stopped state");
        JsonElement disconnectedStatusDom = await ReadDomAsync("""
            (() => ({ text: document.querySelector('.status-card.status-online strong')?.textContent?.trim() || '', online: document.querySelector('.status-card.status-online')?.classList.contains('online') === true }))()
            """);

        homeMapCampaignConnected = true;
        await RequireUiActionAsync("""
            (() => {
              const refresh = [...document.querySelectorAll('.top-actions button.top-action.secondary')].at(-1);
              if (!refresh || refresh.disabled) return false;
              refresh.click();
              return true;
            })()
            """, "Refresh status control for connected recovery");
        await WaitForDomAsync(
            "document.querySelector('.status-card.status-online')?.classList.contains('online') === true",
            "fresh paired status recovers connected");
        JsonElement recoveredStatusDom = await ReadDomAsync("""
            (() => ({ text: document.querySelector('.status-card.status-online strong')?.textContent?.trim() || '', online: document.querySelector('.status-card.status-online')?.classList.contains('online') === true }))()
            """);

        await SelectMapTabAsync(0, "city");
        Task<HomeMapCampaignDelayedRequest> oldASearchEntered = ArmHomeMapCampaignCommandDelay("map_search");
        await ClickMapSearchAsync("profile A delayed Search control");
        HomeMapCampaignDelayedRequest oldASearch = await oldASearchEntered.WaitAsync(TimeSpan.FromSeconds(5));
        await SelectProfileAsync("Campaign B", "campaign-B", "A to B profile control");
        long profileBGeneration = Volatile.Read(ref profileRuntimeGeneration);
        if (profileBGeneration <= initialProfileGeneration || oldASearch.ProfileGeneration != initialProfileGeneration)
            throw new InvalidDataException("Campaign proof did not capture the first-A request against the first-A native generation.");
        ReleaseHomeMapCampaignCommandDelay();
        await Task.Delay(150);
        await WaitForDomAsync(
            "document.querySelectorAll('.map-tabs .map-tab-count')[0]?.textContent?.trim() === '1' && document.querySelector('.map-table--city tbody tr')?.innerText?.includes('Campaign City B')",
            "profile B Map owner after delayed A release");
        if (activeProfileConfig.Snapshot.AutoLaunchGame != true)
            throw new InvalidDataException("Profile B incorrectly inherited profile A Auto Launch edit.");
        JsonElement profileBAfterOldA = await ReadDomAsync("""
            (() => ({
              activeProfile: [...document.querySelectorAll('.profile-compact-item')].find(button => button.classList.contains('active'))?.querySelector('strong')?.textContent?.trim() || '',
              cityRow: document.querySelector('.map-table--city tbody tr')?.innerText || ''
            }))()
            """);
        if ((profileBAfterOldA.GetProperty("cityRow").GetString() ?? string.Empty).Contains("Campaign City A", StringComparison.Ordinal))
            throw new InvalidDataException("Delayed first-A Map response revived after native owner replacement to B.");
        await SetAutoIntervalAsync(55, 318, "profile B");

        await SelectMapTabAsync(0, "city");
        homeMapCampaignExportMode = "cancel";
        await RequireUiActionAsync("""
            (() => {
              const buttons = [...document.querySelectorAll('.map-searchbar > button')];
              const exportButton = buttons[1];
              if (!exportButton || exportButton.disabled) return false;
              exportButton.click();
              return true;
            })()
            """, "city Export cancel control");
        await WaitForRequestsToDrainAsync("export cancellation");
        string exportCancelMessage = JsonSerializer.Deserialize<string>(
            await core.ExecuteScriptAsync("document.querySelector('.map-claim-result')?.textContent?.trim() || ''")) ?? string.Empty;

        homeMapCampaignExportMode = "fail";
        await RequireUiActionAsync("""
            (() => {
              const exportButton = [...document.querySelectorAll('.map-searchbar > button')][1];
              if (!exportButton || exportButton.disabled) return false;
              exportButton.click();
              return true;
            })()
            """, "city Export failure control");
        await WaitForDomAsync("(document.querySelector('.map-claim-result')?.textContent?.trim() || '').length > 0", "city Export failure surfaced");
        string exportFailureMessage = JsonSerializer.Deserialize<string>(
            await core.ExecuteScriptAsync("document.querySelector('.map-claim-result')?.textContent?.trim() || ''")) ?? string.Empty;

        homeMapCampaignExportMode = "success";
        string proofExportPath = Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(outputPath))!,
            Path.GetFileNameWithoutExtension(outputPath) + "-ui-export.xlsx");
        if (File.Exists(proofExportPath)) File.Delete(proofExportPath);
        await RequireUiActionAsync("""
            (() => {
              const exportButton = [...document.querySelectorAll('.map-searchbar > button')][1];
              if (!exportButton || exportButton.disabled) return false;
              exportButton.click();
              return true;
            })()
            """, "city Export success control");
        for (int attempt = 0; attempt < 200 && !File.Exists(proofExportPath); attempt++)
            await Task.Delay(25);
        if (!File.Exists(proofExportPath))
            throw new TimeoutException("Campaign UI Export success did not write the isolated workbook.");
        string exportFailureJson = JsonSerializer.Serialize(exportFailureMessage, JsonOptions.Default);
        await WaitForDomAsync(
            $"(document.querySelector('.map-claim-result')?.textContent?.trim() || '').length > 0 && document.querySelector('.map-claim-result')?.textContent?.trim() !== {exportFailureJson}",
            "city Export success surfaced");
        string exportSuccessMessage = JsonSerializer.Deserialize<string>(
            await core.ExecuteScriptAsync("document.querySelector('.map-claim-result')?.textContent?.trim() || ''")) ?? string.Empty;

        await RequireUiActionAsync("""
            (() => {
              const manual = document.querySelectorAll('.map-scan-tabs button')[0];
              if (!manual) return false;
              manual.click();
              return true;
            })()
            """, "Manual scan tab before Clear");
        await WaitForDomAsync("document.querySelectorAll('.map-scan-tabs button')[0]?.getAttribute('aria-selected') === 'true'", "Manual scan tab");
        await RequireUiActionAsync("""
            (() => {
              const buttons = [...document.querySelectorAll('.map-actions > button')];
              const clear = buttons.at(-1);
              if (!clear || clear.disabled) return false;
              clear.click();
              return true;
            })()
            """, "profile B Clear control");
        object? clearNativeSummary = null;
        bool nativeClearConverged = false;
        for (int attempt = 0; attempt < 120; attempt++)
        {
            clearNativeSummary = await backend.InvokeAsync(
                "map_summary",
                JsonSerializer.SerializeToElement(new { profileId = backend.ProfileId }, JsonOptions.Default),
                CancellationToken.None);
            JsonElement nativeSummaryElement = JsonSerializer.SerializeToElement(clearNativeSummary, JsonOptions.Default);
            JsonElement nativeCounts = nativeSummaryElement.GetProperty("counts");
            nativeClearConverged = LWBridge.Map317.MapKinds.All.All(
                kind => nativeCounts.GetProperty(kind).GetInt32() == 0);
            if (nativeClearConverged) break;
            await Task.Delay(25);
        }
        await WaitForDomAsync(
            "document.querySelector('.map-table--city')?.getAttribute('aria-busy') !== 'true' && !(document.querySelector('.map-table--city tbody tr')?.innerText || '').includes('Campaign City B')",
            "profile B Clear table convergence");
        JsonElement clearDom = await ReadDomAsync("""
            (() => ({
              counts: [...document.querySelectorAll('.map-tabs .map-tab-count')].slice(0,8).map(node => node.textContent?.trim() || ''),
              cityRow: document.querySelector('.map-table--city tbody tr')?.innerText || '',
              error: document.querySelector('.map-scan-error')?.textContent?.trim() || ''
            }))()
            """);
        if (!nativeClearConverged ||
            !string.IsNullOrWhiteSpace(clearDom.GetProperty("error").GetString()) ||
            (clearDom.GetProperty("cityRow").GetString() ?? string.Empty).Contains("Campaign City B", StringComparison.Ordinal))
            throw new InvalidDataException(
                "Profile B Clear control failed: dom=" + clearDom.GetRawText() +
                "; activeRequests=" + documentSession.Requests.ActiveCount +
                "; nativeSummary=" + JsonSerializer.Serialize(clearNativeSummary, JsonOptions.Default));

        await SelectProfileAsync("Campaign A", "campaign-A", "B to A profile control");
        long returnedAGeneration = Volatile.Read(ref profileRuntimeGeneration);
        if (returnedAGeneration <= profileBGeneration || returnedAGeneration == oldASearch.ProfileGeneration)
            throw new InvalidDataException("Returned A did not receive a new native owner generation.");
        if (activeProfileConfig.Snapshot.AutoLaunchGame)
            throw new InvalidDataException("Returned A did not reopen its persisted Home Auto Launch preference.");
        MapAutoScanSnapshot returnedAAuto = await mapAutoScanService.GetSnapshotAsync().ConfigureAwait(true);
        if (returnedAAuto.Config.IntervalMinutes != 45 || returnedAAuto.Config.ServerIds?.Contains(317) != true)
            throw new InvalidDataException("Returned A did not reopen its persisted Auto Scan state.");
        await SelectMapTabAsync(0, "city");
        await WaitForDomAsync(
            "document.querySelectorAll('.map-tabs .map-tab-count')[0]?.textContent?.trim() === '1' && document.querySelector('.map-table--city tbody tr')?.innerText?.includes('Campaign City A')",
            "returned A Map persistence");

        await SelectProfileAsync("Campaign B", "campaign-B", "returned A to B before document restart");
        long preReloadBGeneration = Volatile.Read(ref profileRuntimeGeneration);
        await SelectMapTabAsync(0, "city");
        Task<HomeMapCampaignDelayedRequest> cancelEntered = ArmHomeMapCampaignCommandDelay("map_search");
        await ClickMapSearchAsync("profile B Search before document restart");
        HomeMapCampaignDelayedRequest cancelledRequest = await cancelEntered.WaitAsync(TimeSpan.FromSeconds(5));
        Task nextNavigation = WaitForNextSuccessfulNavigationAsync(core);
        core.Reload();
        await nextNavigation.WaitAsync(TimeSpan.FromSeconds(10));
        for (int attempt = 0; attempt < 200; attempt++)
        {
            HomeMapCampaignDelayedRequest? observation = GetHomeMapCampaignDelayedObservation();
            if (observation?.Cancelled == true) break;
            await Task.Delay(25);
        }
        HomeMapCampaignDelayedRequest? cancellationObservation = GetHomeMapCampaignDelayedObservation();
        if (cancellationObservation?.Cancelled != true || cancellationObservation.ProfileGeneration != preReloadBGeneration)
            throw new InvalidDataException("Document replacement did not cancel the exact active profile-B request.");
        ReleaseHomeMapCampaignCommandDelay();
        await WaitForDomAsync(
            "!!document.querySelector('.app-shell') && document.querySelectorAll('.profile-compact-item').length === 2",
            "packaged UI after document restart");
        await WaitForProfileAsync("campaign-B", "persisted B selection after document restart");
        string registrySelectedAfterRestart = profileRegistryService?.Snapshot.SelectedProfileId ?? string.Empty;
        if (registrySelectedAfterRestart != "campaign-B")
            throw new InvalidDataException("Document restart did not preserve native registry selection B.");
        await WaitForDomAsync(
            $"document.documentElement.lang === {languageJson} && document.documentElement.dataset.theme === '{expectedTheme}'",
            "locale/theme persistence after document restart");
        JsonElement restartSnapshot = await ReadDomAsync("""
            (() => ({
              language: document.documentElement.lang || '',
              theme: document.documentElement.dataset.theme || '',
              activeProfile: [...document.querySelectorAll('.profile-compact-item')].find(button => button.classList.contains('active'))?.querySelector('strong')?.textContent?.trim() || '',
              sessionId: window.__LWBridgeBootstrap?.sessionId || ''
            }))()
            """);

        await SelectProfileAsync("Campaign A", "campaign-A", "final B to A profile control after restart");
        long finalAGeneration = Volatile.Read(ref profileRuntimeGeneration);
        await RequireUiActionAsync("""
            (() => {
              const button = document.querySelectorAll('.side-nav button')[2];
              if (!button) return false;
              button.click();
              return true;
            })()
            """, "final Map navigation");
        await SelectMapTabAsync(0, "city");
        await WaitForDomAsync(
            "document.querySelector('.map-table--city tbody tr')?.innerText?.includes('Campaign City A')",
            "final A Map row");

        JsonElement issues = await ReadDomAsync("window.__LWB317CampaignIssues || []");
        if (issues.GetArrayLength() != 0)
            throw new InvalidDataException("Campaign packaged UI recorded console/page issues: " + issues.GetRawText());
        JsonElement finalDom = await ReadDomAsync("""
            (() => ({
              language: document.documentElement.lang || '',
              theme: document.documentElement.dataset.theme || '',
              activeProfile: [...document.querySelectorAll('.profile-compact-item')].find(button => button.classList.contains('active'))?.querySelector('strong')?.textContent?.trim() || '',
              cityRow: document.querySelector('.map-table--city tbody tr')?.innerText || '',
              viewport: { width: window.innerWidth, height: window.innerHeight, scrollWidth: document.documentElement.scrollWidth }
            }))()
            """);
        JsonElement viewport = finalDom.GetProperty("viewport");
        int viewportWidth = viewport.GetProperty("width").GetInt32();
        int viewportScrollWidth = viewport.GetProperty("scrollWidth").GetInt32();
        if (homeMapCampaignNarrow && (viewportWidth > 900 || viewportScrollWidth > viewportWidth))
            throw new InvalidDataException("Campaign proof narrow layout overflowed its measured desktop viewport.");

        string executablePath = Path.GetFullPath(Application.ExecutablePath);
        string managedAssemblyPath = Path.ChangeExtension(executablePath, ".dll");
        string indexPath = Path.Combine(uiRootPath, "index.html");
        string executableSha256 = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(executablePath)))
            .ToLowerInvariant();
        string managedAssemblySha256 = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(managedAssemblyPath)))
            .ToLowerInvariant();
        string uiIndexSha256 = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(indexPath)))
            .ToLowerInvariant();
        ProductionUiBuildIdentity packageIdentity = DesktopUiContentRoot.ReadCanonicalIdentity(uiRootPath);
        string fullPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        HomeMapCampaignEventObservation[] eventProof;
        lock (homeMapCampaignEventGate)
            eventProof = homeMapCampaignEvents.ToArray();
        if (!eventProof.Any(item => item.EventName == "bridge://local-map-auto-scan-changed") ||
            !eventProof.Any(item => item.EventName == "bridge://map-scan-status"))
            throw new InvalidDataException("Campaign packaged proof did not observe named Auto/Map native events.");
        var proof = new
        {
            schemaVersion = 3,
            checkpoint = "LWB317-FUNCTION-HOME-MAP-CAMPAIGN-001-RECOVERY-001-K",
            state = "proven",
            mode = "isolated-package-pinned-real-ui-controls",
            externalGameActions = 0,
            package = new
            {
                executablePath,
                executableSha256,
                managedAssemblyPath,
                managedAssemblySha256,
                uiRootPath = Path.GetFullPath(uiRootPath),
                uiIndexSha256,
                buildIdentity = packageIdentity,
                uiProject = shellSnapshot.GetProperty("uiProject").GetString(),
                bridgeMode = shellSnapshot.GetProperty("bridgeMode").GetString(),
            },
            appearance = new
            {
                expectedLanguage,
                expectedTheme,
                themeControlClicks,
                initial = shellSnapshot,
                afterRestart = restartSnapshot,
                final = finalDom,
            },
            routes = routeProof,
            map = new
            {
                allEightTabs = tabProof,
                profileBWasClearedThroughUi = true,
                profileBClearDom = clearDom,
                profileBClearNativeSummary = clearNativeSummary,
                returnedAStillHasSeededCity = true,
            },
            home = new
            {
                profileAAutoLaunchPersisted = activeProfileConfig.Snapshot.AutoLaunchGame == false,
                delayedAutoLaunchRequest = delayedAutoLaunch,
                profileBAfterOldAutoLaunch,
                rejectedAutoLaunch = rejectedAutoLaunchDom,
                statusConnected = connectedStatusDom,
                statusDeferred = deferredStatusDom,
                deferredRequest = deferredStatus,
                statusRejected = rejectedStatusDom,
                statusDisconnected = disconnectedStatusDom,
                statusRecovered = recoveredStatusDom,
            },
            autoScan = new
            {
                profileAInterval = returnedAAuto.Config.IntervalMinutes,
                profileAServerIds = returnedAAuto.Config.ServerIds,
                profileBInterval = 55,
                profileBServerId = 318,
                inertProviderStarts = Volatile.Read(ref homeMapCampaignScanStartCount),
                inertProviderStops = Volatile.Read(ref homeMapCampaignScanStopCount),
                cancelSnapshot = canceledAutoSnapshot,
                nativeMessagesDuringStatusAndLater = postedWebMessageCount - messagesBeforeStatusProof,
            },
            runtimeComposition = new
            {
                autoSchedulerEnabled = true,
                plunderWorkersEnabled = true,
                recoveryMonitorEnabled = false,
                bridgeTransportEnabled = false,
                namedEvents = eventProof,
            },
            export = new
            {
                cancelMessage = exportCancelMessage,
                failureMessage = exportFailureMessage,
                successMessage = exportSuccessMessage,
                successPath = proofExportPath,
                successBytes = new FileInfo(proofExportPath).Length,
            },
            profileOwnership = new
            {
                initialProfileGeneration,
                profileBGeneration,
                returnedAGeneration,
                preReloadBGeneration,
                finalAGeneration,
                delayedFirstARequest = oldASearch,
                bAfterFirstARelease = profileBAfterOldA,
                cancellationRequest = cancelledRequest,
                cancellationObservation,
                persistedSelectedProfileAfterRestart = registrySelectedAfterRestart,
            },
            documentLifetime = new
            {
                generation = documentGeneration,
                lastClosedRequestCount,
                lastClosedSubscriptionCount,
                currentActiveRequests = documentSession.Requests.ActiveCount,
                currentSubscriptions = documentSession.Subscriptions.Count,
                postedWebMessageCount,
            },
            browserIssues = issues,
        };
        await File.WriteAllTextAsync(fullPath, JsonSerializer.Serialize(proof, JsonOptions.Indented));
        string screenshotPath = Path.ChangeExtension(fullPath, ".png");
        await using var output = File.Create(screenshotPath);
        await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, output);
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

    private void BeginOwnerEvidenceRenderCapture(string requestId, JsonElement payload, object? result)
    {
        if (ownerEvidence is null || webView.CoreWebView2 is null) return;
        bool resourceSearch = OwnerEvidenceResourceContract.IsResourceSearch(payload);
        bool citySearch = OwnerEvidenceResourceContract.IsCitySearch(payload);
        if (!resourceSearch && !citySearch) return;
        JsonElement resultElement = JsonSerializer.SerializeToElement(result, JsonOptions.Default);

        ownerEvidenceRenderCapture?.Cancel();
        ownerEvidenceRenderCapture?.Dispose();
        ownerEvidenceRenderCapture = new CancellationTokenSource();
        if (citySearch)
            _ = CaptureOwnerEvidenceCityRenderAsync(requestId, payload.Clone(), resultElement.Clone(), ownerEvidenceRenderCapture.Token);
        else
            _ = CaptureOwnerEvidenceRenderAsync(requestId, payload.Clone(), resultElement.Clone(), ownerEvidenceRenderCapture.Token);
    }

    private async Task CaptureOwnerEvidenceCityRenderAsync(
        string requestId, JsonElement payload, JsonElement result, CancellationToken cancellationToken)
    {
        if (ownerEvidence is null || webView.CoreWebView2 is not { } core) return;
        OwnerEvidenceCityTableSnapshot? lastSnapshot = null;
        string? expectedUpdatedText = null;
        OwnerEvidenceResourceTarget? target = OwnerEvidenceResourceContract.TryGetFirstTarget(result);
        bool resultIsEmpty = OwnerEvidenceResourceContract.IsEmptyResult(result);
        object sanitizedResult = OwnerEvidenceResourceContract.SanitizeCitySearchResult(result);
        try
        {
            if (target is not null)
            {
                string renderedTimeJson = await core.ExecuteScriptAsync(
                    $"new Date({target.UpdatedAt.ToString(System.Globalization.CultureInfo.InvariantCulture)}).toLocaleString(document.documentElement.lang || undefined)");
                expectedUpdatedText = JsonSerializer.Deserialize<string?>(renderedTimeJson);
            }

            for (int attempt = 0; attempt < 20; attempt++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    ownerEvidence.RecordCityRender(requestId, payload, sanitizedResult, lastSnapshot, false, "superseded-by-new-city-search", target, expectedUpdatedText);
                    return;
                }
                string snapshotJson = await core.ExecuteScriptAsync(OwnerEvidenceResourceContract.CityTableSnapshotScript);
                if (snapshotJson != "null")
                {
                    lastSnapshot = JsonSerializer.Deserialize<OwnerEvidenceCityTableSnapshot>(snapshotJson, JsonOptions.Default);
                    if (lastSnapshot is not null && OwnerEvidenceResourceContract.IsCityCorrelated(target, resultIsEmpty, lastSnapshot, expectedUpdatedText))
                    {
                        ownerEvidence.RecordCityRender(requestId, payload, sanitizedResult, lastSnapshot, true, null, target, expectedUpdatedText);
                        return;
                    }
                }
                await Task.Delay(100, CancellationToken.None);
            }
            ownerEvidence.RecordCityRender(requestId, payload, sanitizedResult, lastSnapshot, false, "city-render-not-correlated-within-passive-observation-bound", target, expectedUpdatedText);
        }
        catch (Exception ex)
        {
            ownerEvidence.Record("city-render-capture-error", new { requestId, error = ex.GetType().Name, ex.Message });
        }
    }

    private async Task CaptureOwnerEvidenceRenderAsync(
        string requestId, JsonElement payload, JsonElement result, CancellationToken cancellationToken)
    {
        if (ownerEvidence is null || webView.CoreWebView2 is not { } core) return;
        NormalUiResourceProofTableSnapshot? lastSnapshot = null;
        string? expectedUpdatedText = null;
        OwnerEvidenceResourceTarget? target = OwnerEvidenceResourceContract.TryGetFirstTarget(result);
        bool resultIsEmpty = OwnerEvidenceResourceContract.IsEmptyResult(result);
        try
        {
            if (target is not null)
            {
                string renderedTimeJson = await core.ExecuteScriptAsync(
                    $"new Date({target.UpdatedAt.ToString(System.Globalization.CultureInfo.InvariantCulture)}).toLocaleString(document.documentElement.lang || undefined)");
                expectedUpdatedText = JsonSerializer.Deserialize<string?>(renderedTimeJson);
            }

            // IMPLEMENTATION POLICY: passive owner evidence polls only the already-rendering
            // Resource table for up to five seconds. It never clicks, searches or starts a scan.
            for (int attempt = 0; attempt < 50; attempt++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    ownerEvidence.RecordRender(requestId, payload, result, lastSnapshot, false, "superseded-by-new-resource-search", target, expectedUpdatedText);
                    return;
                }
                string snapshotJson = await core.ExecuteScriptAsync(NormalUiResourceProofContract.ResourceTableSnapshotScript);
                if (snapshotJson != "null")
                {
                    lastSnapshot = JsonSerializer.Deserialize<NormalUiResourceProofTableSnapshot>(snapshotJson, JsonOptions.Default);
                    if (lastSnapshot is not null && OwnerEvidenceResourceContract.IsCorrelated(target, resultIsEmpty, lastSnapshot, expectedUpdatedText))
                    {
                        ownerEvidence.RecordRender(requestId, payload, result, lastSnapshot, true, null, target, expectedUpdatedText);
                        return;
                    }
                }
                await Task.Delay(100, CancellationToken.None);
            }
            ownerEvidence.RecordRender(requestId, payload, result, lastSnapshot, false, "render-not-correlated-within-passive-observation-bound", target, expectedUpdatedText);
        }
        catch (Exception ex)
        {
            ownerEvidence.Record("resource-render-capture-error", new { requestId, error = ex.GetType().Name, ex.Message });
        }
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
            if (ownerEvidence is not null && OwnerEvidenceResourceContract.IsBlockedOwnerCommand(command))
            {
                ownerEvidence.Record("owner-command-blocked", new { requestId = id, command });
                SendError(session, id, "OWNER_EVIDENCE_READ_ONLY",
                    "This owner evidence session is read-only. Do not use scan or state-changing Map Data actions.");
                return;
            }
            try
            {
                LWBridgeBackend requestBackend = backend;
                long requestProfileGeneration = Volatile.Read(ref profileRuntimeGeneration);
                NativeRequestExecution execution = await session.Requests.ExecuteAsync(id, cancellationToken =>
                    command == "game_root_select"
                        ? SelectGameRootAsync(cancellationToken)
                        : command == "set_window_theme"
                            ? Task.FromResult(
                                windowThemeService.Apply(Handle, payload))
                        : command == "map_city_export"
                            ? map317CommandService is not null
                                ? ExportMap317CityAsync(payload, cancellationToken)
                                : ExportCityAsync(payload, cancellationToken)
                        : command == "game_root_status" && hostProbeService?.ForceMissingGameRoot == true
                            ? Task.FromResult<object?>(new GameRootStatus(
                                false, string.Empty, "host-probe", "GAME_ROOT_NOT_FOUND",
                                null, null, null, null))
                        : Task.Run(async () =>
                        {
                            if (hostProbeService is not null)
                                await hostProbeService.BeforeProductionCommandAsync(command, cancellationToken).ConfigureAwait(false);
                            await WaitForHomeMapCampaignCommandReleaseAsync(
                                command,
                                requestBackend.ProfileId,
                                requestProfileGeneration,
                                cancellationToken).ConfigureAwait(false);
                            ThrowIfHomeMapCampaignCommandRejected(command);
                            return await requestBackend.InvokeAsync(command, payload, cancellationToken).ConfigureAwait(false);
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
                if (command == "map_search" && (normalUiLiveResourceProofPath is not null || normalUiLiveMapProofPath is not null || mapUiIntegrationProofPath is not null))
                    RecordNormalUiProofSearch(id, payload, execution.Result);
                if (command == "map_search" && ownerEvidence is not null && OwnerEvidenceResourceContract.IsResourceSearch(payload))
                    ownerEvidence.RecordSearch(id, payload, execution.Result);
                if (command == "map_search" && ownerEvidence is not null && OwnerEvidenceResourceContract.IsCitySearch(payload))
                    ownerEvidence.RecordCitySearch(id, payload, execution.Result);
                SendResult(session, id, execution.Result);
                if (command == "map_search" && ownerEvidence is not null)
                    BeginOwnerEvidenceRenderCapture(id, payload, execution.Result);
                if (command == "map_player_mark_set" && map317CommandService is null)
                    SendEvent(session, "bridge://player-mark-changed", execution.Result);
                if (command is "game_root_select" or "profile_instance_start" or "profile_instance_stop" or "set_automation" or "local_config_set")
                    await EmitOverviewStateAsync(session);
            }
            catch (BridgeCommandException ex)
            {
                if (IsCurrentDocument(session))
                {
                    if (ownerEvidence is not null && command == "map_summary")
                        ownerEvidence.RecordCommandError(id, command, ex.Code, ex.Message, ex.Details);
                    SendError(session, id, ex.Code, ex.Message, ex.Details);
                }
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
                return backend.CreateGameRootSelectionCanceled();
            }

            if (isolatedConfigRoot is null)
                throw new InvalidOperationException("Host probe picker requires isolated storage.");
            string invalidRoot = Path.Combine(isolatedConfigRoot, "invalid-game-root");
            Directory.CreateDirectory(invalidRoot);
            NativeGameRootSelectionResult invalid = await Task.Run(
                () => backend.SaveNativeGameRootSelection(invalidRoot),
                cancellationToken);
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
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return backend.CreateGameRootSelectionCanceled();

        string selectedPath = dialog.SelectedPath;
        NativeGameRootSelectionResult selected = await Task.Run(
            () => backend.SaveNativeGameRootSelection(selectedPath),
            cancellationToken);
        return selected;
    }

    private async Task<object?> ExportMap317CityAsync(
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        Map317CommandService service = map317CommandService
            ?? throw new InvalidOperationException("Map317 export service is unavailable.");
        Map317CityExportRequest request = service.PrepareCityExport(payload);
        cancellationToken.ThrowIfCancellationRequested();

        if (homeMapCampaignProofPath is not null)
        {
            if (string.Equals(homeMapCampaignExportMode, "cancel", StringComparison.Ordinal))
                return new { canceled = true, rowCount = 0, path = (string?)null };

            string proofDirectory = Path.GetDirectoryName(Path.GetFullPath(homeMapCampaignProofPath))!;
            if (string.Equals(homeMapCampaignExportMode, "fail", StringComparison.Ordinal))
            {
                string blocker = Path.Combine(isolatedConfigRoot!, "export-blocker");
                File.WriteAllText(blocker, "not-a-directory");
                return await Task.Run(
                    () => service.WriteCityExport(request, Path.Combine(blocker, "cities.xlsx")),
                    cancellationToken);
            }

            string proofExportPath = Path.Combine(
                proofDirectory,
                Path.GetFileNameWithoutExtension(homeMapCampaignProofPath) + "-ui-export.xlsx");
            return await Task.Run(
                () => service.WriteCityExport(request, proofExportPath),
                cancellationToken);
        }

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

    private async Task<object?> ExportCityAsync(
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        CityExportRequest request = backend.PrepareCityExport(payload);
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
                return LWBridgeBackend.CreateCityExportCanceledResult();
            selectedPath = dialog.FileName;
        }
        catch
        {
            // RECOVERED LWB-R7-059: rfd 0.16 synchronous save_file() maps both
            // user cancellation and dialog build/show/get-result failure to None.
            return LWBridgeBackend.CreateCityExportCanceledResult();
        }

        cancellationToken.ThrowIfCancellationRequested();
        return await Task.Run(
            () => backend.WriteCityExport(request, selectedPath),
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

    private void OnResourceAutomationStatusChanged(long generation, object status)
    {
        if (sessionClosed || IsDisposed || !IsCurrentProfileRuntimeGeneration(generation)) return;
        void Publish()
        {
            if (!IsCurrentProfileRuntimeGeneration(generation)) return;
            DocumentSession session = documentSession;
            if (IsCurrentDocument(session) &&
                session.Subscriptions.Contains("bridge://resource-automation-status"))
            {
                SendEvent(
                    session,
                    "bridge://resource-automation-status",
                    status);
            }
        }

        try
        {
            if (InvokeRequired) BeginInvoke(Publish);
            else Publish();
        }
        catch (InvalidOperationException) { }
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

    private void OnDispatchPlunderChanged(long generation)
    {
        if (sessionClosed || IsDisposed || !IsCurrentProfileRuntimeGeneration(generation)) return;
        void Publish()
        {
            if (!IsCurrentProfileRuntimeGeneration(generation)) return;
            DocumentSession session = documentSession;
            if (IsCurrentDocument(session) &&
                session.Subscriptions.Contains("bridge://dispatch-plunder-changed"))
            {
                SendEvent(session, "bridge://dispatch-plunder-changed", new { });
            }
        }
        try
        {
            if (InvokeRequired) BeginInvoke(Publish);
            else Publish();
        }
        catch (InvalidOperationException) { }
    }

    private void OnTruckPlunderChanged(long generation)
    {
        if (sessionClosed || IsDisposed || !IsCurrentProfileRuntimeGeneration(generation)) return;
        void Publish()
        {
            if (!IsCurrentProfileRuntimeGeneration(generation)) return;
            DocumentSession session = documentSession;
            if (IsCurrentDocument(session) &&
                session.Subscriptions.Contains("bridge://truck-plunder-changed"))
            {
                SendEvent(session, "bridge://truck-plunder-changed", new { });
            }
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
        if (homeMapCampaignProofPath is not null)
        {
            lock (homeMapCampaignEventGate)
                homeMapCampaignEvents.Add(new HomeMapCampaignEventObservation(
                    eventName,
                    backend.ProfileId,
                    Volatile.Read(ref profileRuntimeGeneration),
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        }
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
            if (homeMapCampaignProofPath is not null)
            {
                void Cleanup(string name, Action action)
                {
                    try { action(); }
                    catch (Exception error)
                    {
                        homeMapCampaignShutdownFailures.Add($"{name}:{error.GetType().Name}:{error.Message}");
                    }
                }

                // The proof records each current-owner shutdown boundary independently.
                // Drain Map-owned workers before closing the lifecycle they depend on.
                Cleanup("auto-scan", () => mapAutoScanService?.Dispose());
                Cleanup("map317", () => map317CommandService?.Dispose());
                Cleanup("city-layout-drafts", () => cityLayoutDraftService?.Dispose());
                Cleanup("profile-settings", () => profileSettingsService?.Dispose());
                Cleanup("overview-lifecycle", () => overviewLifecycleService?.Close());
                Cleanup("bridge-host", () => bridgeHostState?.Close());
                Cleanup("live-resource", () => liveResourceService?.Close());
                Cleanup("profile-registry", () => profileRegistryService?.Dispose());
            }
            else
            {
                // Drain Map-owned workers before closing the game lifecycle they depend on.
                try { mapAutoScanService?.Dispose(); } catch { }
                try { map317CommandService?.Dispose(); } catch { }
                try { cityLayoutDraftService?.Dispose(); } catch { }
                try { profileSettingsService?.Dispose(); } catch { }
                try { overviewLifecycleService?.Close(); } catch { }
                // The application window remains the final owner of the currently active
                // shared control-pipe host; profile replacement closes retired hosts earlier.
                bridgeHostState?.Close();
                liveResourceService?.Close();
                profileRegistryService?.Dispose();
            }
            ownerEvidenceRenderCapture?.Cancel();
            ownerEvidenceRenderCapture?.Dispose();
            ownerEvidence?.Record("session-end", new { processId = Environment.ProcessId });
            ownerEvidence?.Dispose();
            if (sessionScopedMapData)
            {
                try { mapData?.ClearAllScanData(); }
                catch { }
            }
            mapData?.Dispose();
            if (isolatedConfigRoot is not null && homeMapCampaignProofPath is null)
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

    internal void FinalizeHomeMapCampaignProof()
    {
        if (homeMapCampaignProofPath is null || isolatedConfigRoot is null) return;

        // Microsoft.Data.Sqlite pools disposed file-backed connections by default.
        // The isolated proof owns the whole process, so release those library-managed
        // handles after every runtime/service has been disposed before deleting its
        // temporary root. Production process lifetime is unchanged.
        try { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); }
        catch (Exception error)
        {
            homeMapCampaignShutdownFailures.Add(
                $"sqlite-pools:{error.GetType().Name}:{error.Message}");
        }

        bool isolatedRootRemoved = !Directory.Exists(isolatedConfigRoot);
        Exception? deleteFailure = null;
        for (int attempt = 0; !isolatedRootRemoved && attempt < 40; attempt++)
        {
            try
            {
                Directory.Delete(isolatedConfigRoot, recursive: true);
                isolatedRootRemoved = !Directory.Exists(isolatedConfigRoot);
                deleteFailure = null;
            }
            catch (Exception error)
            {
                deleteFailure = error;
                Thread.Sleep(50);
            }
        }
        if (!isolatedRootRemoved && deleteFailure is not null)
            homeMapCampaignShutdownFailures.Add(
                $"isolated-root:{deleteFailure.GetType().Name}:{deleteFailure.Message}");

        string proofPath = Path.GetFullPath(homeMapCampaignProofPath);
        if (!File.Exists(proofPath)) return;

        Dictionary<string, JsonElement>? proof = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            File.ReadAllText(proofPath), JsonOptions.Default);
        if (proof is null) return;
        proof["shutdown"] = JsonSerializer.SerializeToElement(new
        {
            sessionClosed,
            requestRegistryClosed = documentSession.Requests.IsClosed,
            activeRequests = documentSession.Requests.ActiveCount,
            activeSubscriptions = documentSession.Subscriptions.Count,
            profileRuntimeEventsDetached = overviewRecoveryHandler is null &&
                resourceAutomationStatusHandler is null &&
                manualMapScanStatusHandler is null &&
                mapPlayerMarkHandler is null &&
                dispatchPlunderHandler is null &&
                truckPlunderHandler is null &&
                mapAutoScanHandler is null,
            isolatedRootRemoved,
            cleanupFailures = homeMapCampaignShutdownFailures.ToArray(),
        }, JsonOptions.Default);
        File.WriteAllText(proofPath, JsonSerializer.Serialize(proof, JsonOptions.Indented));
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

    private sealed record HomeMapCampaignDelayedRequest(
        string Command,
        string ProfileId,
        long ProfileGeneration,
        bool Cancelled);

    private sealed record HomeMapCampaignEventObservation(
        string EventName,
        string ProfileId,
        long ProfileGeneration,
        long TimestampMilliseconds);
}
