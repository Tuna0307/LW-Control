import React, { useEffect, useRef, useState } from "react";
import { createRoot } from "react-dom/client";
import { I18nProvider } from "/src/i18n.jsx";
import { BrowserDialogRetainedPages } from "./browser-dialog-retained.jsx";
import "/src/reference.css";
import "/src/styles.css";

localStorage.setItem("lwbridge.language", "en");
document.documentElement.dataset.theme = "light";

function DialogHarness() {
  const [activeRoute, setActiveRoute] = useState("march");
  const [visitedRoutes, setVisitedRoutes] = useState(() => new Set(["march"]));
  const [phase, setPhase] = useState("ready");
  const hideTimer = useRef(null);
  const homeFocus = useRef(null);
  useEffect(() => () => { if (hideTimer.current !== null) window.clearTimeout(hideTimer.current); }, []);
  useEffect(() => {
    if (activeRoute === "overview" && phase === "hidden") homeFocus.current?.focus();
  }, [activeRoute, phase]);
  const armHide = () => {
    if (hideTimer.current !== null) window.clearTimeout(hideTimer.current);
    setPhase("armed-3000ms");
    hideTimer.current = window.setTimeout(() => {
      hideTimer.current = null;
      setVisitedRoutes((current) => new Set([...current, "overview"]));
      setActiveRoute("overview");
      setPhase("hidden");
    }, 3000);
  };
  const returnSquads = () => { setActiveRoute("march"); setPhase("returned"); };
  return (
    <main className="app-shell" data-proof="retained-native-equipment-dialog" data-route={activeRoute} data-phase={phase}>
      <header style={{ display: "flex", gap: 12, alignItems: "center", padding: 16 }}>
        <strong>Retained native dialog proof</strong>
        <button id="arm-hide" type="button" onClick={armHide}>Arm hide in 3 seconds</button>
        <button id="return-squads" type="button" onClick={returnSquads}>Return to Squads</button>
        <button id="visible-home-focus" ref={homeFocus} type="button" onClick={() => setPhase("home-button-clicked")}>Visible Home focus target</button>
        <output id="proof-phase">{phase}</output>
      </header>
      <p style={{ padding: "0 16px" }}>Arm hide, then click the actual Equipment Rename button before the timer fires. The route hides with its native dialog open. Check that Home releases modal blocking, then return to Squads.</p>
      <section className="main-view">
        <BrowserDialogRetainedPages activeRoute={activeRoute} visitedRoutes={visitedRoutes} selectedProfileId="retained-dialog-proof-profile" pageProps={{ bridgeMode: "preview", backendAvailable: false, online: false, previewState: "squads-equipment" }} />
      </section>
    </main>
  );
}

createRoot(document.getElementById("root")).render(<I18nProvider><DialogHarness /></I18nProvider>);
