import { useCallback, useEffect, useState } from "react";
import offlineDot from "./assets/dot-offline.png";
import onlineDot from "./assets/dot-online.png";
import { backendBridge } from "./backendBridge.js";
import { LANGUAGES, useI18n } from "./i18n.jsx";
import { connectionState, createMapApi } from "./mapBackend.js";
import { NavIcon } from "./NavIcon.jsx";
import { PageForRoute } from "./Pages.jsx";
import { initialRouteKey, routes } from "./routes.js";

const THEME_KEY = "lwbridge.theme";
const LEGACY_SERVER_HISTORY_KEY = "lastwar.serverJumpHistory";
const mapApi = createMapApi(backendBridge);

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

export function App() {
  const { language, setLanguage, t } = useI18n();
  const [activeRoute, setActiveRoute] = useState(initialRoute);
  const [theme, setTheme] = useState(initialTheme);
  const [serverJumpOpen, setServerJumpOpen] = useState(false);
  const [runtimeStatus, setRuntimeStatus] = useState(null);
  const [proxyStatus, setProxyStatus] = useState(null);
  const [gameRootStatus, setGameRootStatus] = useState(null);
  const [gameRecoveryStatus, setGameRecoveryStatus] = useState(null);
  const [localConfig, setLocalConfig] = useState(null);
  const [homeBusy, setHomeBusy] = useState("");
  const [gameRootError, setGameRootError] = useState("");
  const [gameActionError, setGameActionError] = useState("");
  const [currentServerId, setCurrentServerId] = useState(0);
  const [mapRuntime, setMapRuntime] = useState({ isReading: false, homeServerId: 0, seasonServerIds: [], truckMatchServerIds: [] });
  const [serverTarget, setServerTarget] = useState("");
  const [serverJumpBusy, setServerJumpBusy] = useState(0);
  const [serverJumpError, setServerJumpError] = useState("");
  const [serverHistory, setServerHistory] = useState([]);
  const [connectionError, setConnectionError] = useState("");

  useEffect(() => {
    if (backendBridge.mode !== "preview") return;
    const requested = new URLSearchParams(window.location.search).get("previewLanguage");
    if (LANGUAGES.some(({ code }) => code === requested) && requested !== language) setLanguage(requested);
  }, [language, setLanguage]);

  const refreshStatus = useCallback(async () => {
    if (!backendBridge.available) return;
    const [statusResult, proxyResult, scanResult, rootResult, recoveryResult, configResult] = await Promise.allSettled([
      mapApi.readStatus(),
      mapApi.readProxyStatus(),
      mapApi.scanStatus(),
      backendBridge.invoke("game_root_status", {}),
      backendBridge.invoke("game_recovery_status", backendBridge.profileId ? { profileId: backendBridge.profileId } : {}),
      backendBridge.invoke("local_config_get", {}),
    ]);
    if (statusResult.status === "fulfilled") setRuntimeStatus(statusResult.value);
    if (proxyResult.status === "fulfilled") setProxyStatus(proxyResult.value);
    if (scanResult.status === "fulfilled" && scanResult.value.serverId > 0) {
      setCurrentServerId(scanResult.value.serverId);
    }
    if (scanResult.status === "fulfilled") setMapRuntime(scanResult.value);
    if (rootResult.status === "fulfilled") setGameRootStatus(rootResult.value);
    if (recoveryResult.status === "fulfilled") setGameRecoveryStatus(recoveryResult.value);
    if (configResult.status === "fulfilled") setLocalConfig(configResult.value);
    if (statusResult.status === "rejected" || proxyResult.status === "rejected") {
      const failure = statusResult.status === "rejected" ? statusResult.reason : proxyResult.reason;
      setConnectionError(failure?.message || String(failure));
    } else {
      setConnectionError("");
    }
  }, []);

  useEffect(() => {
    if (!backendBridge.available) return undefined;
    return backendBridge.listen("bridge://game-recovery", (event) => {
      const payload = event && typeof event === "object" && "payload" in event ? event.payload : event;
      if (payload) setGameRecoveryStatus(payload);
    });
  }, []);

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
    const unsubscribeStatus = mapApi.listenStatus((status) => {
      if (!closed) setRuntimeStatus(status);
    });
    const unsubscribeScan = mapApi.listenScanStatus((scan) => {
      if (!closed) {
        if (scan.serverId > 0) setCurrentServerId(scan.serverId);
        setMapRuntime((current) => ({ ...current, ...scan }));
      }
    });
    refreshStatus();
    const timer = window.setInterval(refreshStatus, 5000);
    return () => {
      closed = true;
      window.clearInterval(timer);
      unsubscribeStatus();
      unsubscribeScan();
    };
  }, [refreshStatus]);

  useEffect(() => {
    if (!backendBridge.available) return undefined;
    let closed = false;
    mapApi.importServerJumpHistory(legacyServerHistory()).then((history) => {
      if (!closed) setServerHistory(Array.isArray(history) ? history : []);
      try { localStorage.removeItem(LEGACY_SERVER_HISTORY_KEY); } catch {}
    }).catch(() => {
      if (!closed) setServerJumpError("Action failed");
    });
    return () => { closed = true; };
  }, []);

  const bridgeState = connectionError
    ? "unavailable"
    : connectionState(runtimeStatus, proxyStatus, backendBridge.mode);
  const online = bridgeState === "connected";

  const updateAutoLaunch = useCallback(async (value) => {
    if (!backendBridge.available) return;
    setHomeBusy("autoLaunchGame");
    setGameActionError("");
    try {
      const next = await backendBridge.invoke("local_config_set", { autoLaunchGame: value });
      setLocalConfig(next);
    } catch (error) {
      setGameActionError(error?.message || String(error));
    } finally {
      setHomeBusy("");
    }
  }, []);

  const updateAutoReconnect = useCallback(async (value) => {
    if (!backendBridge.available) return;
    setHomeBusy("autoReconnect");
    setGameActionError("");
    try {
      await backendBridge.invokeProfileScoped("set_automation", { name: "autoForceUpdateReload", enabled: value });
      setLocalConfig((current) => ({ ...(current || {}), autoReconnect: value }));
    } catch (error) {
      setGameActionError(error?.message || String(error));
    } finally {
      setHomeBusy("");
    }
  }, []);

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
      setGameRootStatus(next);
      setGameRootError("");
    } catch (error) {
      setGameRootError(error?.message || String(error));
    } finally {
      setHomeBusy("");
    }
  }, []);

  const jumpServer = useCallback(async (serverId) => {
    if (!Number.isInteger(serverId) || serverId < 1 || serverId > 99999) {
      setServerJumpError("Invalid server ID");
      return;
    }
    if (!online) {
      setServerJumpError("Game disconnected");
      return;
    }
    if (mapRuntime.isReading) {
      setServerJumpError("Stop the map scan first");
      return;
    }
    setServerJumpBusy(serverId);
    setServerJumpError("");
    try {
      const result = await mapApi.jumpServer(serverId);
      if (result?.changed) {
        const history = [Number(result.serverId), ...serverHistory.filter((value) => value !== Number(result.serverId))].slice(0, 5);
        const persisted = await mapApi.setServerJumpHistory(history);
        setServerHistory(Array.isArray(persisted) ? persisted : history);
      }
      setServerTarget("");
      setServerJumpOpen(false);
      await refreshStatus();
    } catch {
      setServerJumpError("Action failed");
    } finally {
      setServerJumpBusy(0);
    }
  }, [mapRuntime.isReading, online, refreshStatus, serverHistory]);

  const stateText = {
    connected: t("status.connected"),
    disconnected: t("status.disconnected"),
    stopped: t("setup.gameStopped"),
    preview: "Preview",
    unavailable: "Unavailable",
    checking: t("status.checking"),
  }[bridgeState] || t("status.checking");
  const pendingTasks = Number(runtimeStatus?.pending ?? 0);

  return (
    <main className="app-shell" data-reference-version="0.3.17" data-ui-project="LWBridge.UI-0.3.17">
      <header className="top-bar">
        <div className="brand-lockup">
          <div className="brand-mark" aria-hidden="true">
            <span>LW</span>
          </div>
          <div className="brand-copy">
            <h1>lwbridge</h1>
            <div className="top-version-row">
              <span>v0.3.17</span>
            </div>
          </div>
        </div>

        <section className="status-strip" aria-label="Status">
          <div className="status-card status-online">
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
            onClick={() => setTheme((value) => (value === "dark" ? "light" : "dark"))}
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

          <div className="server-jump">
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
                      if (event.key === "Enter" && !serverJumpBusy) jumpServer(Number(serverTarget));
                    }}
                  />
                  <button className="top-action primary" type="button" disabled={serverJumpBusy > 0 || !online || mapRuntime.isReading} onClick={() => jumpServer(Number(serverTarget))}>
                    {serverJumpBusy > 0 ? t("server.switching", { server: serverJumpBusy }) : t("server.switchAction")}
                  </button>
                </div>
                {mapRuntime.homeServerId > 0 && currentServerId !== mapRuntime.homeServerId ? (
                  <div className="server-jump-home"><span>{t("server.home", { server: mapRuntime.homeServerId })}</span><button type="button" disabled={serverJumpBusy > 0 || !online || mapRuntime.isReading} onClick={() => jumpServer(mapRuntime.homeServerId)}>{t("server.returnHome")}</button></div>
                ) : null}
                {!online ? <p className="server-jump-error">{t("status.gameDisconnected")}</p> : null}
                {online && mapRuntime.isReading ? <p className="server-jump-error">{t("server.stopScanFirst")}</p> : null}
                {serverJumpError ? <p className="server-jump-error">{serverJumpError}</p> : null}
                {mapRuntime.seasonServerIds?.length ? <div className="server-jump-history"><span>{t("server.seasonServers")}</span><div>{mapRuntime.seasonServerIds.map((serverId) => <button type="button" key={serverId} disabled={serverJumpBusy > 0 || !online || mapRuntime.isReading || serverId === currentServerId} onClick={() => jumpServer(serverId)}>{serverId}</button>)}</div></div> : null}
                {mapRuntime.truckMatchServerIds?.length ? <div className="server-jump-history"><span>{t("server.plunderableServers")}</span><div>{mapRuntime.truckMatchServerIds.map((serverId) => <button type="button" key={serverId} disabled={serverJumpBusy > 0 || !online || mapRuntime.isReading || serverId === currentServerId} onClick={() => jumpServer(serverId)}>{serverId}</button>)}</div></div> : null}
                {serverHistory.length ? <div className="server-jump-history"><span>{t("server.recent")}</span><div>{serverHistory.map((serverId) => <button type="button" key={serverId} disabled={serverJumpBusy > 0 || !online || mapRuntime.isReading} onClick={() => jumpServer(serverId)}>{serverId}</button>)}</div></div> : null}
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

      <div className="app-layout single-profile">
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
              onClick={() => setActiveRoute(route.key)}
            >
              <span className="nav-icon" aria-hidden="true">
                <NavIcon name={route.key} />
              </span>
              <span className="nav-label">{t(route.labelKey)}</span>
            </button>
          ))}
        </nav>

        <section className="main-view">
          <PageForRoute
            routeKey={activeRoute}
            mapApi={mapApi}
            bridgeMode={backendBridge.mode}
            backendAvailable={backendBridge.available}
            online={online}
            currentServerId={currentServerId}
            previewState={backendBridge.mode === "preview" ? new URLSearchParams(window.location.search).get("previewState") || "" : ""}
            homeState={{
              rootResolved: gameRootStatus !== null,
              gameRootStatus,
              proxyStatus,
              online,
              gameRecoveryStatus,
              autoLaunchGame: localConfig?.autoLaunchGame,
              autoReconnect: localConfig?.autoReconnect,
              busy: homeBusy,
              gameRootError,
              gameActionError,
              production: backendBridge.available,
            }}
            onAutoLaunchGameChange={updateAutoLaunch}
            onAutoReconnectChange={updateAutoReconnect}
            onGameRootSelect={selectGameRoot}
          />
        </section>
      </div>
    </main>
  );
}
