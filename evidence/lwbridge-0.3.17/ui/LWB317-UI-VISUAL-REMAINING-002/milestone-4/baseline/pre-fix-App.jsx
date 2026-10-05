import { Activity, Fragment, Suspense, useCallback, useEffect, useLayoutEffect, useRef, useState, useSyncExternalStore, useTransition } from "react";
import { flushSync } from "react-dom";
import { toggleShellTheme } from "./shellTheme.js";
import { TopVersion, ShellConfigSaveErrors } from "./ShellPresentation.jsx";
import { initialShellUpdateStatus, initialShellProfiles, previewShellFlagStores, initialProfileFocus, saveProfileFocus } from "./shellState.js";
import { ProfileSidebar } from "./ProfileSidebar.jsx";
import { AppExitDialog, AppExitPrompt } from "./AppExitDialog.jsx";
import { ProfileSwitchState } from "./ProfileSwitchState.jsx";
import { GameAssetImageProvider } from "./GameAssetImage.jsx";
import offlineDot from "./assets/dot-offline.png";
import onlineDot from "./assets/dot-online.png";
import { backendBridge } from "./backendBridge.js";
import { LANGUAGES, useI18n } from "./i18n.jsx";
import { DEFAULT_SCAN_STATE, connectionState, createMapApi } from "./mapBackend.js";
import {
  AUTO_SCAN_DEFAULT_TYPES,
  applyAutoScanConfigEdit,
  loadAutoScanConfig,
  normalizeAutoScanConfig,
  saveAutoScanConfig,
} from "./mapAutoConfig.js";
import { NavIcon } from "./NavIcon.jsx";
import { PageForRoute, preloadRoute } from "./Pages.jsx";
import { initialRouteKey, routes } from "./routes.js";
import { readAutoLaunchGamePreference, writeAutoLaunchGamePreference } from "./autoLaunchPreference.js";
import { createAutomationFlagAdapter, createProfileConfigDraftRegistry } from "./profileConfigDraft.js";

const THEME_KEY = "lwbridge.theme";
const LEGACY_SERVER_HISTORY_KEY = "lastwar.serverJumpHistory";
const AUTO_RECONNECT_FLAG_KEY = "flag:autoForceUpdateReload";
const mapApi = createMapApi(backendBridge);
// Existing read-only local image contract; no new native producer is introduced.
// Browser previews never invoke it or lend their synthetic images to this reader.
const nativeAssetReader = backendBridge.available ? (request) => backendBridge.invoke("game_asset_image", request) : null;

function legacyServerHistory() {
  try {
    const parsed = JSON.parse(localStorage.getItem(LEGACY_SERVER_HISTORY_KEY) || "[]");
    return Array.isArray(parsed)
      ? parsed.map(Number).filter((value, index, values) => Number.isInteger(value) && value >= 1 && value <= 99999 && values.indexOf(value) === index).slice(0, 5)
      : [];
  } catch {
    return [];
  }
}

function initialRoute() {
  const params = new URLSearchParams(window.location.search);
  const previewPage = params.get("previewPage");
  const requested = params.get("view") || previewPage;
  return routes.some((route) => route.key === requested) ? requested : initialRouteKey;
}

function initialTheme() {
  try {
    if (backendBridge.mode === "preview") {
      const requested = new URLSearchParams(window.location.search).get("previewTheme");
      if (requested === "dark" || requested === "light") return requested;
    }
    return localStorage.getItem(THEME_KEY) === "dark" ? "dark" : "light";
  } catch {
    return "light";
  }
}

function initialAutoScanConfig(profileId, previewState) {
  if (previewState === "map-auto-scheduled" || previewState === "map-auto-running") {
    return normalizeAutoScanConfig({
      enabled: true,
      intervalMinutes: 60,
      serverIds: [321, 322],
      selectedTypes: AUTO_SCAN_DEFAULT_TYPES,
      scanMode: "fast",
      returnToOriginalServer: true,
      nextRunAt: 1_893_456_000_000,
    });
  }
  return loadAutoScanConfig(profileId, window.localStorage);
}

function RetainedPages({ activeRoute, visitedRoutes, selectedProfileId, pageProps, pagePropsByRoute }) {
  return (
    <Fragment key={selectedProfileId}>
      {routes.map((route) => visitedRoutes.has(route.key) ? (
        <Activity key={route.key} mode={route.key === activeRoute ? "visible" : "hidden"}>
          <PageForRoute routeKey={route.key} {...pageProps} {...pagePropsByRoute?.[route.key]} />
        </Activity>
      ) : null)}
    </Fragment>
  );
}

export function App({ shellFlagStates = null, subscribeCloseRequests = null, confirmExit = null, profileSwitchLoading = false } = {}) {
  const { language, setLanguage, t } = useI18n();
  const previewState = backendBridge.mode === "preview" ? new URLSearchParams(window.location.search).get("previewState") || "" : "";
  const profilePreview = backendBridge.mode === "preview" && previewState.startsWith("shell-profiles");
  const [shellProfiles, setShellProfiles] = useState(() => initialShellProfiles(backendBridge.mode, previewState, window.__LWBridgeBootstrap));
  const selectedProfileId = profilePreview ? shellProfiles.selectedProfileId : backendBridge.profileId;
  const [previewFlagStates] = useState(() => previewShellFlagStores(backendBridge.mode, previewState));
  const showProfiles = shellProfiles.maxProfiles > 1;
  const [focusGameOnProfileSelect, setFocusGameOnProfileSelect] = useState(() => initialProfileFocus(localStorage));
  const updateProfileFocus = (value) => { saveProfileFocus(localStorage, value); setFocusGameOnProfileSelect(value); };
  const switchLoading = profileSwitchLoading || (backendBridge.mode === "preview" && previewState === "shell-profiles-loading");
  const nextPreviewProfileId = useRef(3);
  const [previewExitCount, setPreviewExitCount] = useState(() => backendBridge.mode === "preview" && previewState.startsWith("shell-exit") ? 2 : null);
  const profilePreviewCallbacks = profilePreview ? {
    onSelect: async (id) => { setShellProfiles((current) => ({ ...current, selectedProfileId: id })); },
    onCreate: async () => { const id = `preview-local-${nextPreviewProfileId.current++}`; setShellProfiles((current) => {
      return { ...current, profiles: [...current.profiles, { id, displayName: `Local ${current.profiles.length + 1}`, note: "", serverId: 0, enabled: true }] };
    }); },
    onRemove: async (id) => { setShellProfiles((current) => {
      const profiles = current.profiles.filter((profile) => profile.id !== id);
      return { ...current, profiles, selectedProfileId: current.selectedProfileId === id ? profiles[0]?.id || "" : current.selectedProfileId };
    }); },
    onReorder: async (ids) => { setShellProfiles((current) => ({ ...current,
      profiles: ids.map((id) => current.profiles.find((profile) => profile.id === id)) })); },
    onUpdateNote: async (id, note) => { setShellProfiles((current) => ({ ...current,
      profiles: current.profiles.map((profile) => profile.id === id ? { ...profile, note } : profile) })); },
  } : {};
  const [activeRoute, setActiveRoute] = useState(initialRoute);
  const [visitedRoutes, setVisitedRoutes] = useState(() => new Set([initialRoute()]));
  const [automationCategory, setAutomationCategory] = useState("daily");
  const [mapTab, setMapTab] = useState("city");
  const [squadTab, setSquadTab] = useState("afk");
  const [, startRouteTransition] = useTransition();
  const [theme, setTheme] = useState(initialTheme);
  const [shellUpdateStatus, setShellUpdateStatus] = useState(() => initialShellUpdateStatus(backendBridge.mode, previewState));
  const [serverJumpOpen, setServerJumpOpen] = useState(false);
  const [runtimeStatus, setRuntimeStatus] = useState(null);
  const [autoReconnectIncoming, setAutoReconnectIncoming] = useState(false);
  const [proxyStatus, setProxyStatus] = useState(null);
  const [gameRootStatus, setGameRootStatus] = useState(null);
  const [gameRecoveryStatus, setGameRecoveryStatus] = useState(null);
  const [autoLaunchGame, setAutoLaunchGame] = useState(() => readAutoLaunchGamePreference(localStorage));
  const [homeBusy, setHomeBusy] = useState("");
  const [gameRootError, setGameRootError] = useState("");
  const [gameActionError, setGameActionError] = useState("");
  const [currentServerId, setCurrentServerId] = useState(0);
  const [mapRuntime, setMapRuntime] = useState(() => ({ ...DEFAULT_SCAN_STATE }));
  const [mapSummary, setMapSummary] = useState(null);
  const [serverTarget, setServerTarget] = useState("");
  const [serverJumpBusy, setServerJumpBusy] = useState(0);
  const [serverJumpError, setServerJumpError] = useState("");
  const [serverHistory, setServerHistory] = useState([]);
  const [connectionError, setConnectionError] = useState("");
  const [autoScanConfig, setAutoScanConfig] = useState(() => initialAutoScanConfig(selectedProfileId, previewState));
  const [autoScanRunning, setAutoScanRunning] = useState(() => previewState === "map-auto-running");
  const mapReadingRef = useRef(false);
  const mapRuntimeRef = useRef({ ...DEFAULT_SCAN_STATE });
  const mapSummaryGeneration = useRef(0);
  const autoScanConfigRef = useRef(autoScanConfig);
  const serverJumpRef = useRef(null);
  const serverHistoryRef = useRef([]);
  const autoLaunchCommittedRef = useRef(autoLaunchGame);
  const autoLaunchSaveRevisionRef = useRef(0);
  const autoLaunchSaveChainRef = useRef(Promise.resolve());
  const autoLaunchNativeCommitEpochRef = useRef(0);
  const autoLaunchConfigPollGenerationRef = useRef(0);
  const reconnectStatusGeneration = useRef(0);
  const [profileConfigDrafts] = useState(() => createProfileConfigDraftRegistry());
  const autoReconnectStore = profileConfigDrafts.get(
    selectedProfileId,
    AUTO_RECONNECT_FLAG_KEY,
    autoReconnectIncoming,
    createAutomationFlagAdapter(
      backendBridge,
      selectedProfileId,
      "autoForceUpdateReload",
      "auto_force_update_reload",
      false,
    ),
  );
  const autoReconnectSnapshot = useSyncExternalStore(
    autoReconnectStore.subscribe,
    autoReconnectStore.getSnapshot,
    autoReconnectStore.getSnapshot,
  );

  useEffect(() => {
    autoReconnectStore.receive(autoReconnectIncoming);
  }, [autoReconnectIncoming, autoReconnectStore]);

  useLayoutEffect(() => {
    reconnectStatusGeneration.current += 1;
    return () => { reconnectStatusGeneration.current += 1; };
  }, [selectedProfileId]);

  const acknowledgeRuntimeStatus = useCallback((status, generation = reconnectStatusGeneration.current) => {
    if (generation !== reconnectStatusGeneration.current || selectedProfileId !== backendBridge.profileId) return;
    setRuntimeStatus(status);
    const reconnect = status?.config?.auto_force_update_reload;
    if (typeof reconnect === "boolean") setAutoReconnectIncoming(reconnect);
  }, [selectedProfileId]);

  useLayoutEffect(() => {
    const next = initialAutoScanConfig(selectedProfileId, previewState);
    autoScanConfigRef.current = next;
    setAutoScanConfig(next);
  }, [selectedProfileId]);

  const updateAutoScanConfig = useCallback((candidate) => {
    const next = applyAutoScanConfigEdit(autoScanConfigRef.current, candidate, Date.now());
    autoScanConfigRef.current = next;
    setAutoScanConfig(next);
    saveAutoScanConfig(selectedProfileId, next, window.localStorage);
  }, [selectedProfileId]);

  useEffect(() => {
    if (backendBridge.mode !== "preview") return;
    const requested = new URLSearchParams(window.location.search).get("previewLanguage");
    if (LANGUAGES.some(({ code }) => code === requested) && requested !== language) setLanguage(requested);
  }, [language, setLanguage]);

  const acknowledgeGameRootStatus = useCallback((next) => {
    setGameRootStatus(next);
    setGameRootError("");
  }, []);

  const refreshMapSummary = useCallback(async () => {
    if (!backendBridge.available) return null;
    const generation = mapSummaryGeneration.current + 1;
    mapSummaryGeneration.current = generation;
    const profileId = selectedProfileId;
    const summary = await mapApi.summary();
    if (generation !== mapSummaryGeneration.current || profileId !== backendBridge.profileId) return summary;
    setMapSummary(summary);
    mapReadingRef.current = summary.scanState?.isReading === true;
    mapRuntimeRef.current = summary.scanState;
    setMapRuntime(summary.scanState);
    setCurrentServerId(summary.scanState?.serverId > 0 ? summary.scanState.serverId : 0);
    return summary;
  }, [selectedProfileId]);

  const selectRoute = useCallback((routeKey) => {
    if (routeKey === activeRoute) return;
    if (routeKey === "map-data") refreshMapSummary().catch(() => {});
    preloadRoute(routeKey);
    startRouteTransition(() => {
      setVisitedRoutes((current) => {
        if (current.has(routeKey)) return current;
        const next = new Set(current);
        next.add(routeKey);
        return next;
      });
      setActiveRoute(routeKey);
    });
  }, [activeRoute, refreshMapSummary, startRouteTransition]);

  const acknowledgeMapScan = useCallback((scan) => {
    if (!scan) return;
    const wasReading = mapReadingRef.current;
    mapReadingRef.current = scan.isReading === true;
    mapRuntimeRef.current = scan;
    setCurrentServerId(scan.serverId > 0 ? scan.serverId : 0);
    setMapRuntime(scan);
    setMapSummary((current) => current?.serverId === scan.serverId ? { ...current, scanState: scan } : current);
    if (wasReading && !scan.isReading) refreshMapSummary().catch(() => {});
  }, [refreshMapSummary]);

  const acknowledgeMapCounts = useCallback((serverId, counts) => {
    setMapSummary((current) => {
      if (current?.serverId === serverId) return { ...current, counts };
      if (mapRuntimeRef.current?.serverId === serverId) return { serverId, counts, scanState: mapRuntimeRef.current };
      return current;
    });
  }, []);

  const refreshStatus = useCallback(async () => {
    if (!backendBridge.available) return;
    const reconnectGeneration = reconnectStatusGeneration.current;
    const autoLaunchRevision = autoLaunchSaveRevisionRef.current;
    const autoLaunchNativeCommitEpoch = autoLaunchNativeCommitEpochRef.current;
    const autoLaunchConfigPollGeneration = autoLaunchConfigPollGenerationRef.current + 1;
    autoLaunchConfigPollGenerationRef.current = autoLaunchConfigPollGeneration;
    const [statusResult, proxyResult, recoveryResult, configResult] = await Promise.allSettled([
      mapApi.readStatus(),
      mapApi.readProxyStatus(),
      backendBridge.invoke("game_recovery_status", backendBridge.profileId ? { profileId: backendBridge.profileId } : {}),
      backendBridge.invoke("local_config_get", {}),
    ]);
    if (statusResult.status === "fulfilled") {
      acknowledgeRuntimeStatus(statusResult.value, reconnectGeneration);
    }
    if (proxyResult.status === "fulfilled") setProxyStatus(proxyResult.value);
    if (recoveryResult.status === "fulfilled") setGameRecoveryStatus(recoveryResult.value);
    if (configResult.status === "fulfilled") {
      if (autoLaunchConfigPollGenerationRef.current === autoLaunchConfigPollGeneration
        && autoLaunchSaveRevisionRef.current === autoLaunchRevision
        && autoLaunchNativeCommitEpochRef.current === autoLaunchNativeCommitEpoch
        && typeof configResult.value?.autoLaunchGame === "boolean") {
        autoLaunchCommittedRef.current = configResult.value.autoLaunchGame;
      }
    }
    if (statusResult.status === "rejected" || proxyResult.status === "rejected") {
      const failure = statusResult.status === "rejected" ? statusResult.reason : proxyResult.reason;
      setConnectionError(failure?.message || String(failure));
    } else {
      setConnectionError("");
    }
  }, [acknowledgeRuntimeStatus]);

  useEffect(() => {
    if (!backendBridge.available) return undefined;
    return backendBridge.listen("bridge://game-recovery", (event) => {
      const payload = event && typeof event === "object" && "payload" in event ? event.payload : event;
      if (payload) setGameRecoveryStatus(payload);
    });
  }, []);

  useEffect(() => {
    if (!backendBridge.available || !selectedProfileId) return;
    backendBridge.invoke("game_root_status", {})
      .then(acknowledgeGameRootStatus)
      .catch((error) => setGameRootError(error?.message || String(error)));
  }, [acknowledgeGameRootStatus, selectedProfileId]);

  useEffect(() => {
    document.documentElement.dataset.theme = theme;
    try {
      localStorage.setItem(THEME_KEY, theme);
    } catch {
      // Static preview remains usable when storage is unavailable.
    }
  }, [theme]);

  useEffect(() => {
    if (!backendBridge.available) return undefined;
    let closed = false;
    const stop = backendBridge.listen("bridge://update-status", (status) => {
      if (!closed && status) setShellUpdateStatus(status);
    });
    backendBridge.invoke("update_status", {}).then((status) => {
      if (!closed) setShellUpdateStatus(status);
    }).catch(() => {});
    return () => { closed = true; stop(); };
  }, []);

  useEffect(() => {
    if (!backendBridge.available) return undefined;
    let closed = false;
    const unsubscribeStatus = mapApi.listenStatus((status) => {
      if (!closed) acknowledgeRuntimeStatus(status);
    });
    const unsubscribeScan = mapApi.listenScanStatus((scan) => {
      if (!closed) acknowledgeMapScan(scan);
    });
    refreshStatus();
    const timer = window.setInterval(refreshStatus, 5000);
    return () => {
      closed = true;
      window.clearInterval(timer);
      unsubscribeStatus();
      unsubscribeScan();
    };
  }, [acknowledgeMapScan, acknowledgeRuntimeStatus, refreshStatus]);

  useEffect(() => {
    if (!backendBridge.available || !selectedProfileId) return undefined;
    mapReadingRef.current = false;
    mapRuntimeRef.current = { ...DEFAULT_SCAN_STATE };
    setMapRuntime({ ...DEFAULT_SCAN_STATE });
    setMapSummary(null);
    refreshMapSummary().catch(() => {});
    return () => { mapSummaryGeneration.current += 1; };
  }, [refreshMapSummary, selectedProfileId]);

  useEffect(() => {
    if (!backendBridge.available || !selectedProfileId) return undefined;
    let closed = false;
    serverHistoryRef.current = [];
    setServerHistory([]);
    setServerJumpError("");
    mapApi.importServerJumpHistory(legacyServerHistory()).then((history) => {
      if (!closed) {
        serverHistoryRef.current = history;
        setServerHistory(history);
      }
      try { localStorage.removeItem(LEGACY_SERVER_HISTORY_KEY); } catch {}
    }).catch(() => {
      if (!closed) setServerJumpError(t("common.actionFailed"));
    });
    return () => { closed = true; };
  }, [selectedProfileId]);

  useEffect(() => {
    if (!serverJumpOpen) return undefined;
    const closeOutside = (event) => {
      if (!serverJumpRef.current?.contains(event.target)) setServerJumpOpen(false);
    };
    document.addEventListener("pointerdown", closeOutside);
    return () => document.removeEventListener("pointerdown", closeOutside);
  }, [serverJumpOpen]);

  const bridgeState = connectionError
    ? "unavailable"
    : connectionState(runtimeStatus, proxyStatus, backendBridge.mode);
  const online = bridgeState === "connected";

  useEffect(() => {
    if (!online || !backendBridge.available) return undefined;
    let closed = false;
    let polling = false;
    const poll = async () => {
      if (closed || polling) return;
      polling = true;
      try {
        await refreshMapSummary();
      } catch {
        // The reference parent logs summary failures without replacing the current Map state.
      } finally {
        polling = false;
      }
    };
    poll();
    const timer = window.setInterval(poll, 5000);
    return () => {
      closed = true;
      window.clearInterval(timer);
    };
  }, [online, refreshMapSummary]);

  const updateAutoLaunch = useCallback((value) => {
    if (!backendBridge.available) return undefined;
    const enabled = value === true;
    const revision = autoLaunchSaveRevisionRef.current + 1;
    autoLaunchSaveRevisionRef.current = revision;
    writeAutoLaunchGamePreference(localStorage, enabled);
    setAutoLaunchGame(enabled);
    setGameActionError("");

    const save = autoLaunchSaveChainRef.current
      .catch(() => undefined)
      .then(() => backendBridge.invoke("local_config_set", { autoLaunchGame: enabled }))
      .then((next) => {
        if (typeof next?.autoLaunchGame !== "boolean") {
          throw new Error("local_config_set returned an invalid autoLaunchGame value.");
        }
        autoLaunchNativeCommitEpochRef.current += 1;
        autoLaunchCommittedRef.current = next.autoLaunchGame;
        if (autoLaunchSaveRevisionRef.current === revision) {
          writeAutoLaunchGamePreference(localStorage, next.autoLaunchGame);
          setAutoLaunchGame(next.autoLaunchGame);
        }
        return next;
      })
      .catch((error) => {
        if (autoLaunchSaveRevisionRef.current === revision) {
          writeAutoLaunchGamePreference(localStorage, autoLaunchCommittedRef.current);
          setAutoLaunchGame(autoLaunchCommittedRef.current);
          setGameActionError(error?.message || String(error));
        }
        return null;
      });

    autoLaunchSaveChainRef.current = save;
    return save;
  }, []);

  const updateAutoReconnect = useCallback(async (value) => {
    if (!backendBridge.available) return;
    autoReconnectStore.edit(value, false);
    await autoReconnectStore.flush().catch(() => {});
  }, [autoReconnectStore]);

  const selectGameRoot = useCallback(async () => {
    if (!backendBridge.available) return;
    setHomeBusy("gameRoot");
    try {
      const selection = await backendBridge.invoke("game_root_select", {});
      if (selection.canceled) return;
      if (!selection.valid) {
        setGameRootError("INVALID_GAME_ROOT");
        return;
      }
      const next = await backendBridge.invoke("game_root_status", {});
      acknowledgeGameRootStatus(next);
    } catch (error) {
      setGameRootError(error?.message || String(error));
    } finally {
      setHomeBusy("");
    }
  }, [acknowledgeGameRootStatus]);

  const requestServerJump = useCallback(async (serverId) => {
    const result = await mapApi.jumpServer(serverId);
    try {
      await refreshMapSummary();
    } catch {
      // The reference parent logs this refresh failure and still acknowledges the jump result.
    }
    return result;
  }, [refreshMapSummary]);

  const jumpServer = useCallback(async (serverId) => {
    if (!Number.isInteger(serverId) || serverId < 1 || serverId > 99999) {
      setServerJumpError(t("server.invalidId"));
      return;
    }
    if (!online) {
      setServerJumpError(t("status.gameDisconnected"));
      return;
    }
    if (mapRuntime.isReading) {
      setServerJumpError(t("server.stopScanFirst"));
      return;
    }
    setServerJumpBusy(serverId);
    setServerJumpError("");
    try {
      const result = await requestServerJump(serverId);
      if (result.changed) {
        const history = [result.serverId, ...serverHistoryRef.current.filter((value) => value !== result.serverId)].slice(0, 5);
        const persisted = await mapApi.setServerJumpHistory(history);
        serverHistoryRef.current = persisted;
        setServerHistory(persisted);
      }
      setServerTarget("");
      setServerJumpOpen(false);
    } catch {
      setServerJumpError(t("common.actionFailed"));
    } finally {
      setServerJumpBusy(0);
    }
  }, [mapRuntime.isReading, online, requestServerJump, t]);

  const serverJumpBlockedText = online
    ? mapRuntime.isReading ? t("server.stopScanFirst") : ""
    : t("status.gameDisconnected");
  const serverJumpBusyActive = serverJumpBusy > 0;

  const stateText = {
    connected: t("status.connected"),
    disconnected: t("status.disconnected"),
    stopped: t("setup.gameStopped"),
    preview: "Preview",
    unavailable: "Unavailable",
    checking: t("status.checking"),
  }[bridgeState] || t("status.checking");
  const pendingTasks = Number(runtimeStatus?.pending ?? 0);
  const pageProps = {
    mapApi,
    bridgeMode: backendBridge.mode,
    backendAvailable: backendBridge.available,
    online,
    currentServerId,
    scanState: mapRuntime,
    summary: mapSummary,
    onState: acknowledgeMapScan,
    onCounts: acknowledgeMapCounts,
    autoScanConfig,
    autoScanRunning,
    onAutoScanConfig: updateAutoScanConfig,
    onAutoScanRunningChange: setAutoScanRunning,
    previewState,
    showProfileFocus: showProfiles,
    focusGameOnProfileSelect,
    onFocusGameOnProfileSelectChange: updateProfileFocus,
    homeState: {
      rootResolved: gameRootStatus !== null,
      gameRootStatus,
      proxyStatus,
      online,
      gameRecoveryStatus,
      autoLaunchGame,
      autoReconnect: autoReconnectSnapshot.draft,
      busy: homeBusy,
      gameRootError,
      gameActionError,
      production: backendBridge.available,
    },
    onAutoLaunchGameChange: updateAutoLaunch,
    onAutoReconnectChange: updateAutoReconnect,
    onGameRootSelect: selectGameRoot,
  };
  const pagePropsByRoute = showProfiles ? {
    automation: { activeCategory: automationCategory, onActiveCategoryChange: setAutomationCategory },
    "map-data": { activeTab: mapTab, onActiveTabChange: setMapTab },
    march: { activeTab: squadTab, onActiveTabChange: setSquadTab },
  } : undefined;

  return (
    <GameAssetImageProvider readImage={nativeAssetReader}><main className="app-shell" data-reference-version="0.3.17" data-ui-project="LWBridge.UI-0.3.17">
      <header className="top-bar">
        <div className="brand-lockup">
          <div className="brand-mark" aria-hidden="true">
            <span>LW</span>
          </div>
          <div className="brand-copy">
            <h1>lwbridge</h1>
            <TopVersion status={shellUpdateStatus} />
          </div>
        </div>

        <section className="status-strip" aria-label="Status">
          <div className={`status-card status-online${online ? " online" : ""}`}>
            <strong>
              <img src={online ? onlineDot : offlineDot} alt="" aria-hidden="true" />
              {stateText}
            </strong>
          </div>
          <div className="status-card">
            <span>{t("status.pendingTasks")}</span>
            <strong>{Number.isFinite(pendingTasks) ? pendingTasks : 0}</strong>
          </div>
        </section>

        <div className="top-actions">
          <button
            className="theme-toggle"
            type="button"
            title={t(theme === "dark" ? "theme.switchToLight" : "theme.switchToDark")}
            aria-label={t(theme === "dark" ? "theme.switchToLight" : "theme.switchToDark")}
            aria-pressed={theme === "dark"}
            onClick={() => toggleShellTheme(theme, setTheme, document, window, localStorage, flushSync)}
          >
            {theme === "dark" ? (
              <svg viewBox="0 0 24 24" aria-hidden="true">
                <circle cx="12" cy="12" r="3.5" />
                <path d="M12 2v2M12 20v2M4.93 4.93l1.42 1.42M17.65 17.65l1.42 1.42M2 12h2M20 12h2M4.93 19.07l1.42-1.42M17.65 6.35l1.42-1.42" />
              </svg>
            ) : (
              <svg viewBox="0 0 24 24" aria-hidden="true">
                <path d="M20.4 15.1A8.5 8.5 0 0 1 8.9 3.6 8.5 8.5 0 1 0 20.4 15.1Z" />
              </svg>
            )}
          </button>

          <label className="language-select">
            <span>{t("top.language")}</span>
            <select
              value={language}
              aria-label={t("top.language")}
              onChange={(event) => setLanguage(event.target.value)}
            >
              {LANGUAGES.map(({ code, name }) => <option value={code} key={code}>{name}</option>)}
            </select>
          </label>

          <div className="server-jump" ref={serverJumpRef}>
            <button
              className="top-action secondary"
              type="button"
              onClick={() => setServerJumpOpen((value) => !value)}
            >
              {currentServerId > 0 ? t("server.switchLabelWithId", { server: currentServerId }) : t("server.switchLabel")}
            </button>
            {serverJumpOpen ? (
              <section className="server-jump-popover" aria-label={t("server.dialogLabel")}>
                <div className="server-jump-heading">
                  <strong>{t("server.title")}</strong>
                  <span>{t("server.current", { server: currentServerId || "-" })}</span>
                </div>
                <div className="server-jump-form">
                  <input
                    inputMode="numeric"
                    min="1"
                    max="99999"
                    placeholder={t("server.targetPlaceholder")}
                    type="number"
                    value={serverTarget}
                    onChange={(event) => setServerTarget(event.target.value)}
                    onKeyDown={(event) => {
                      if (event.key === "Enter" && !serverJumpBusyActive && !serverJumpBlockedText) jumpServer(Number(serverTarget));
                    }}
                  />
                  <button className="top-action primary" type="button" disabled={serverJumpBusyActive || !!serverJumpBlockedText} onClick={() => jumpServer(Number(serverTarget))}>
                    {serverJumpBusyActive ? t("server.switching", { server: serverJumpBusy }) : t("server.switchAction")}
                  </button>
                </div>
                {mapRuntime.homeServerId > 0 && currentServerId !== mapRuntime.homeServerId ? (
                  <div className="server-jump-home"><span>{t("server.home", { server: mapRuntime.homeServerId })}</span><button type="button" disabled={serverJumpBusyActive || !!serverJumpBlockedText} onClick={() => jumpServer(mapRuntime.homeServerId)}>{t("server.returnHome")}</button></div>
                ) : null}
                {serverJumpBlockedText ? <p className="server-jump-error">{serverJumpBlockedText}</p> : null}
                {serverJumpError ? <p className="server-jump-error">{serverJumpError}</p> : null}
                {mapRuntime.seasonServerIds?.length ? <div className="server-jump-history"><span>{t("server.seasonServers")}</span><div>{mapRuntime.seasonServerIds.map((serverId) => <button type="button" key={serverId} disabled={serverJumpBusyActive || !!serverJumpBlockedText || serverId === currentServerId} onClick={() => jumpServer(serverId)}>{serverId}</button>)}</div></div> : null}
                {mapRuntime.truckMatchServerIds?.length ? <div className="server-jump-history"><span>{t("server.plunderableServers")}</span><div>{mapRuntime.truckMatchServerIds.map((serverId) => <button type="button" key={serverId} disabled={serverJumpBusyActive || !!serverJumpBlockedText || serverId === currentServerId} onClick={() => jumpServer(serverId)}>{serverId}</button>)}</div></div> : null}
                {serverHistory.length ? <div className="server-jump-history"><span>{t("server.recent")}</span><div>{serverHistory.map((serverId) => <button type="button" key={serverId} disabled={serverJumpBusyActive || !!serverJumpBlockedText} onClick={() => jumpServer(serverId)}>{serverId}</button>)}</div></div> : null}
              </section>
            ) : null}
          </div>

          <button
            className="top-action secondary"
            type="button"
            disabled={!backendBridge.available}
            onClick={refreshStatus}
          >
            {t("top.refreshStatus")}
          </button>
        </div>
      </header>

      <AppExitPrompt subscribeCloseRequests={subscribeCloseRequests} confirmExit={confirmExit} />
      {previewExitCount !== null ? <AppExitDialog instanceCount={previewExitCount} busy={previewState === "shell-exit-busy"}
        onCancel={() => setPreviewExitCount(null)} /> : null}
      <div className={`app-layout${showProfiles ? "" : " single-profile"}`}>
        {showProfiles ? <ProfileSidebar state={shellProfiles} focusGameOnProfileSelect={focusGameOnProfileSelect} {...profilePreviewCallbacks} /> : null}
        <nav className="side-nav" aria-label="Navigation">
          <div className="nav-heading">
            <strong>{t("nav.title")}</strong>
          </div>
          {routes.map((route) => (
            <button
              key={route.key}
              type="button"
              className={route.key === activeRoute ? "active" : ""}
              aria-current={route.key === activeRoute ? "page" : undefined}
              onMouseEnter={() => preloadRoute(route.key)}
              onFocus={() => preloadRoute(route.key)}
              onClick={() => selectRoute(route.key)}
            >
              <span className="nav-icon" aria-hidden="true">
                <NavIcon name={route.key} />
              </span>
              <span className="nav-label">{t(route.labelKey)}</span>
            </button>
          ))}
        </nav>

        <section className="main-view">
          <Suspense fallback={<div className="panel"><span className="muted">{t("common.processing")}</span></div>}>
            <div className="profile-view-context">
              <ShellConfigSaveErrors states={shellFlagStates || (previewFlagStates.length ? previewFlagStates : [null, null, autoReconnectStore, null])} />
              {switchLoading ? <ProfileSwitchState loading /> : <RetainedPages
                activeRoute={activeRoute}
                visitedRoutes={visitedRoutes}
                selectedProfileId={selectedProfileId}
                pageProps={pageProps}
                pagePropsByRoute={pagePropsByRoute}
              />}
            </div>
          </Suspense>
        </section>
      </div>
    </main></GameAssetImageProvider>
  );
}
