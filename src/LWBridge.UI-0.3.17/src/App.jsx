import { useCallback, useEffect, useState } from "react";
import offlineDot from "./assets/dot-offline.png";
import onlineDot from "./assets/dot-online.png";
import { backendBridge } from "./backendBridge.js";
import { connectionState, createMapApi } from "./mapBackend.js";
import { NavIcon } from "./NavIcon.jsx";
import { PageForRoute } from "./Pages.jsx";
import { initialRouteKey, routes } from "./routes.js";

const THEME_KEY = "lwbridge.theme";
const mapApi = createMapApi(backendBridge);

function initialRoute() {
  const params = new URLSearchParams(window.location.search);
  const previewPage = params.get("previewPage");
  const requested = params.get("view") || previewPage;
  return routes.some((route) => route.key === requested) ? requested : initialRouteKey;
}

function initialTheme() {
  try {
    return localStorage.getItem(THEME_KEY) === "dark" ? "dark" : "light";
  } catch {
    return "light";
  }
}

export function App() {
  const [activeRoute, setActiveRoute] = useState(initialRoute);
  const [theme, setTheme] = useState(initialTheme);
  const [language, setLanguage] = useState("en");
  const [serverJumpOpen, setServerJumpOpen] = useState(false);
  const [runtimeStatus, setRuntimeStatus] = useState(null);
  const [proxyStatus, setProxyStatus] = useState(null);
  const [currentServerId, setCurrentServerId] = useState(0);
  const [connectionError, setConnectionError] = useState("");

  const refreshStatus = useCallback(async () => {
    if (!backendBridge.available) return;
    const [statusResult, proxyResult, scanResult] = await Promise.allSettled([
      mapApi.readStatus(),
      mapApi.readProxyStatus(),
      mapApi.scanStatus(),
    ]);
    if (statusResult.status === "fulfilled") setRuntimeStatus(statusResult.value);
    if (proxyResult.status === "fulfilled") setProxyStatus(proxyResult.value);
    if (scanResult.status === "fulfilled" && scanResult.value.serverId > 0) {
      setCurrentServerId(scanResult.value.serverId);
    }
    if (statusResult.status === "rejected" || proxyResult.status === "rejected") {
      const failure = statusResult.status === "rejected" ? statusResult.reason : proxyResult.reason;
      setConnectionError(failure?.message || String(failure));
    } else {
      setConnectionError("");
    }
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
      if (!closed && scan.serverId > 0) setCurrentServerId(scan.serverId);
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

  const bridgeState = connectionError
    ? "unavailable"
    : connectionState(runtimeStatus, proxyStatus, backendBridge.mode);
  const online = bridgeState === "connected";
  const stateText = {
    connected: "Connected",
    disconnected: "Disconnected",
    stopped: "Stopped",
    preview: "Preview",
    unavailable: "Unavailable",
    checking: "Checking",
  }[bridgeState] || "Checking";
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
            <span>Pending tasks</span>
            <strong>{Number.isFinite(pendingTasks) ? pendingTasks : 0}</strong>
          </div>
        </section>

        <div className="top-actions">
          <button
            className="theme-toggle"
            type="button"
            title={theme === "dark" ? "Switch to light theme" : "Switch to dark theme"}
            aria-label={theme === "dark" ? "Switch to light theme" : "Switch to dark theme"}
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
            <span>Language</span>
            <select
              value={language}
              aria-label="Language"
              onChange={(event) => setLanguage(event.target.value)}
            >
              <option value="en">English</option>
              <option value="zh-CN">Chinese (Simplified)</option>
              <option value="zh-TW">Chinese (Traditional)</option>
              <option value="ja">Japanese</option>
              <option value="ko">Korean</option>
              <option value="vi">Vietnamese</option>
              <option value="id">Indonesian</option>
              <option value="ru">Русский</option>
              <option value="pt">Português</option>
            </select>
          </label>

          <div className="server-jump">
            <button
              className="top-action secondary"
              type="button"
              onClick={() => setServerJumpOpen((value) => !value)}
            >
              Cross-server
            </button>
            {serverJumpOpen ? (
              <section className="server-jump-popover" aria-label="Cross-server map jump">
                <div className="server-jump-heading">
                  <strong>Jump to Server</strong>
                  <span>Current: {currentServerId || "-"}</span>
                </div>
                <div className="server-jump-form">
                  <input
                    inputMode="numeric"
                    min="1"
                    max="99999"
                    placeholder="Target server ID"
                    type="number"
                  />
                  <button className="top-action primary" type="button" disabled>
                    Jump
                  </button>
                </div>
                <p className="server-jump-error">
                  {online ? "Cross-server scanning is unavailable." : "Game disconnected"}
                </p>
              </section>
            ) : null}
          </div>

          <button
            className="top-action secondary"
            type="button"
            disabled={!backendBridge.available}
            onClick={refreshStatus}
          >
            Refresh Status
          </button>
        </div>
      </header>

      <div className="app-layout single-profile">
        <nav className="side-nav" aria-label="Navigation">
          <div className="nav-heading">
            <strong>Navigation</strong>
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
              <span className="nav-label">{route.label}</span>
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
          />
        </section>
      </div>
    </main>
  );
}
