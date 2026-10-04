import { lazy } from "react";
import { HomePage } from "./HomePage.jsx";

const loadAutomationPage = () => import("./AutomationPage.jsx");
const loadSquadsPage = () => import("./SquadsPage.jsx");
const loadCityLayoutPage = () => import("./CityLayoutPage.jsx");
const loadHotkeyPages = () => import("./HotkeyPages.jsx");
const loadMapRoutePage = () => import("./MapRoutePage.jsx");
const loadSettingsPage = () => import("./SettingsPage.jsx");

const LazyAutomationPage = lazy(() => loadAutomationPage().then((module) => ({ default: module.AutomationPage })));
const LazySquadsPage = lazy(() => loadSquadsPage().then((module) => ({ default: module.SquadsPage })));
const LazyCityLayoutPage = lazy(() => loadCityLayoutPage().then((module) => ({ default: module.CityLayoutPage })));
const LazyHotkeyPanel = lazy(() => loadHotkeyPages().then((module) => ({ default: module.RecoveredHotkeyPanel })));
const LazyMapRoutePage = lazy(() => loadMapRoutePage().then((module) => ({ default: module.MapRoutePage })));
const LazySettingsPage = lazy(() => loadSettingsPage().then((module) => ({ default: module.SettingsPage })));

const routeLoaders = {
  advanced: undefined,
  automation: loadAutomationPage,
  "city-layout": loadCityLayoutPage,
  hotkeys: loadHotkeyPages,
  "mini-games": loadHotkeyPages,
  "map-data": loadMapRoutePage,
  settings: loadSettingsPage,
  march: loadSquadsPage,
};

export function preloadRoute(routeKey) {
  routeLoaders[routeKey]?.().catch(() => {});
}

export function PageForRoute({ routeKey, ...pageProps }) {
  switch (routeKey) {
    case "overview": return <HomePage {...pageProps} />;
    case "automation": return <LazyAutomationPage {...pageProps} />;
    case "map-data": return <LazyMapRoutePage {...pageProps} />;
    case "march": return <LazySquadsPage {...pageProps} />;
    case "city-layout": return <LazyCityLayoutPage {...pageProps} />;
    case "hotkeys": return <LazyHotkeyPanel {...pageProps} />;
    case "mini-games": return <LazyHotkeyPanel {...pageProps} category="miniGames" />;
    case "settings": return <LazySettingsPage {...pageProps} />;
    default: return <HomePage {...pageProps} />;
  }
}
