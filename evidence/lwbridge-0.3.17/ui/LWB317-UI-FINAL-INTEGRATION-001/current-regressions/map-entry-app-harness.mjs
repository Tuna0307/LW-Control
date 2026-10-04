import * as ShellState from "../../../../../src/LWBridge.UI-0.3.17/src/shellState.js";
import * as ShellTheme from "../../../../../src/LWBridge.UI-0.3.17/src/shellTheme.js";
import assert from "node:assert/strict";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";
import { createHookRuntime, deferred, nodeText, tick, treeNodes } from "../../LWB317-UI-MAP-REFRESH-FEEDBACK-001/harness.mjs";
import { DEFAULT_SCAN_STATE } from "../../../../../src/LWBridge.UI-0.3.17/src/mapBackend.js";
import {
  AUTO_SCAN_DEFAULT_TYPES,
  applyAutoScanConfigEdit,
  loadAutoScanConfig,
  normalizeAutoScanConfig,
  saveAutoScanConfig,
} from "../../../../../src/LWBridge.UI-0.3.17/src/mapAutoConfig.js";
import { routes, initialRouteKey } from "../../../../../src/LWBridge.UI-0.3.17/src/routes.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const requireFromUi = createRequire(path.join(repo, "src/LWBridge.UI-0.3.17/package.json"));
const { parse } = requireFromUi("@babel/parser");
const { transformSync } = requireFromUi("esbuild");

function appStateNames(ast) {
  let app = null;
  for (const entry of ast.program.body) {
    const node = entry.type === "ExportNamedDeclaration" ? entry.declaration : entry;
    if (node?.type === "FunctionDeclaration" && node.id?.name === "App") app = node;
  }
  assert.ok(app, "App declaration");
  const names = [];
  const walk = (node) => {
    if (!node || typeof node !== "object") return;
    if (node.type === "VariableDeclarator" && node.id?.type === "ArrayPattern" && node.init?.callee?.name === "useState") names.push(node.id.elements[0]?.name);
    for (const value of Object.values(node)) {
      if (Array.isArray(value)) value.forEach(walk);
      else if (value && typeof value === "object") walk(value);
    }
  };
  walk(app);
  return names;
}

export async function createAppHarness(source, label, options = {}) {
  const ast = parse(source, { sourceType: "module", plugins: ["jsx"] });
  const stateNames = appStateNames(ast);
  const statements = ast.program.body
    .filter((node) => node.type !== "ImportDeclaration")
    .map((node) => node.type === "ExportNamedDeclaration" ? node.declaration : node)
    .filter(Boolean);
  const code = transformSync(statements.map((node) => source.slice(node.start, node.end)).join("\n"), {
    loader: "jsx", jsxFactory: "h", jsxFragment: "Fragment", target: "es2022",
  }).code;

  const runtime = createHookRuntime();
  const { useState, useRef, useCallback, useEffect, useLayoutEffect } = runtime.hooks;
  const events = [];
  const summaryRequests = [];
  const intervals = new Map();
  const storage = new Map();
  let timerId = 0;
  let clock = options.now ?? Date.UTC(2026, 9, 4, 0, 0, 0);
  let tree = null;
  let unmounted = false;
  const onlineRef = { value: options.online === true };

  const stateIndex = (name) => {
    const index = stateNames.indexOf(name);
    assert.ok(index >= 0, `${label}: state ${name}`);
    return index;
  };
  const getState = (name) => runtime.stateCells[stateIndex(name)];
  const activeRoute = () => getState("activeRoute");
  const useTransition = () => [false, (callback) => {
    events.push({ event: "transition:start", activeRoute: activeRoute() });
    callback();
  }];
  const localStorage = {
    getItem: (key) => storage.has(key) ? storage.get(key) : null,
    setItem: (key, value) => storage.set(key, String(value)),
    removeItem: (key) => storage.delete(key),
  };
  const window = {
    location: { search: options.search || "" },
    localStorage,
    setInterval: (fn, delay) => {
      const id = ++timerId;
      intervals.set(id, { fn, delay, next: clock + delay });
      return id;
    },
    clearInterval: (id) => intervals.delete(id),
    setTimeout: () => ++timerId,
    clearTimeout: () => {},
    matchMedia: () => ({ matches: true }),
  };
  const document = {
    documentElement: { dataset: {} },
    addEventListener: () => {},
    removeEventListener: () => {},
  };
  const backendBridge = {
    mode: "native",
    available: options.available !== false,
    profileId: options.profileId || "profile-a",
    listen: () => () => {},
    invoke: async (command) => {
      if (command === "game_root_status") return { valid: true, root: "C:/Fixture/Game" };
      if (command === "game_recovery_status") return { state: "idle" };
      if (command === "local_config_get") return { autoLaunchGame: false, autoReconnect: false };
      return {};
    },
  };
  let statusListener = null;
  let scanListener = null;
  const mapApi = {
    readStatus: async () => ({ xluaOnline: onlineRef.value }),
    readProxyStatus: async () => ({ gameRunning: true, repairRequired: false }),
    listenStatus: (listener) => { statusListener = listener; return () => { if (statusListener === listener) statusListener = null; }; },
    listenScanStatus: (listener) => { scanListener = listener; return () => { if (scanListener === listener) scanListener = null; }; },
    importServerJumpHistory: async () => [],
    setServerJumpHistory: async (history) => history,
    jumpServer: async (serverId) => ({ changed: true, previousServerId: serverId, serverId }),
    summary: () => {
      const request = deferred();
      const record = {
        id: summaryRequests.length + 1,
        activeRoute: activeRoute(),
        profileId: backendBridge.profileId,
        ...request,
      };
      summaryRequests.push(record);
      events.push({ event: "summary:start", id: record.id, activeRoute: record.activeRoute, profileId: record.profileId });
      return request.promise;
    },
  };

  const createMapApi = () => mapApi;
  const connectionState = () => onlineRef.value ? "connected" : "disconnected";
  const useI18n = () => ({ language: "en", setLanguage: () => {}, t: (key) => key });
  const LANGUAGES = [{ code: "en", label: "English" }];
  function NavIcon() { return null; }
  function PageForRoute() { return null; }
  const offlineDot = "offline.png";
  const onlineDot = "online.png";
  const Fragment = "Fragment";
  const Activity = "Activity";
  const h = (type, props, ...children) => ({ type, props: { ...(props || {}), children } });
  const bindings = {
    ...ShellState, ...ShellTheme,
    flushSync: callback => callback(),
    TopVersion: () => null, ShellConfigSaveErrors: () => null,
    ProfileSidebar: () => null, AppExitDialog: () => null, AppExitPrompt: () => null,
    ProfileSwitchState: () => null, GameAssetImageProvider: ({children}) => children,
    useState, useRef, useCallback, useEffect, useLayoutEffect, useTransition, Activity, Fragment,
    offlineDot, onlineDot, backendBridge, LANGUAGES, useI18n, DEFAULT_SCAN_STATE, connectionState, createMapApi,
    AUTO_SCAN_DEFAULT_TYPES, applyAutoScanConfigEdit, loadAutoScanConfig, normalizeAutoScanConfig, saveAutoScanConfig,
    NavIcon, PageForRoute, initialRouteKey, routes, window, document, localStorage, Date: { now: () => clock }, h,
  };
  const App = new Function(...Object.keys(bindings), `${code}\nreturn App;`)(...Object.values(bindings));

  function render() {
    runtime.beginRender();
    tree = App();
  }
  async function settle(maxRounds = 40) {
    if (unmounted) return;
    for (let round = 0; round < maxRounds; round += 1) {
      render();
      runtime.commitEffects();
      await Promise.resolve();
      await Promise.resolve();
      await tick();
      if (!runtime.isDirty()) return;
    }
    throw new Error(`${label}: hook runtime did not settle`);
  }
  function routeButton(routeKey) {
    render();
    const nav = treeNodes(tree).find((node) => node.type === "nav" && node.props?.className === "side-nav");
    assert.ok(nav, `${label}: nav`);
    const route = routes.find((candidate) => candidate.key === routeKey);
    const button = treeNodes(nav).find((node) => node.type === "button" && nodeText(node).includes(route.labelKey));
    assert.ok(button, `${label}: route ${routeKey}`);
    return button;
  }
  async function clickRoute(routeKey) {
    events.push({ event: "click", routeKey, activeRoute: activeRoute() });
    routeButton(routeKey).props.onClick();
    await settle();
  }
  async function resolveSummary(request, serverId = 321, count = 1) {
    request.resolve({
      serverId,
      counts: Object.fromEntries(["city", "resource", "monster", "truck", "railway", "dispatch", "ghost", "treasure"].map((key) => [key, count])),
      scanState: { ...DEFAULT_SCAN_STATE, serverId },
    });
    await settle();
  }
  async function rejectSummary(request, message = "controlled summary rejection") {
    request.reject(new Error(message));
    await settle();
  }
  async function setProfileId(profileId) {
    backendBridge.profileId = profileId;
    await settle();
  }
  async function emitScan(scan) {
    assert.equal(typeof scanListener, "function", `${label}: scan listener`);
    scanListener({ ...DEFAULT_SCAN_STATE, ...scan });
    await settle();
  }
  async function advance(ms) {
    const target = clock + ms;
    for (;;) {
      const due = [...intervals.entries()]
        .map(([id, timer]) => ({ id, ...timer }))
        .filter((timer) => timer.next <= target)
        .sort((left, right) => left.next - right.next || left.id - right.id)[0];
      if (!due) break;
      clock = due.next;
      const current = intervals.get(due.id);
      if (!current) continue;
      current.next += current.delay;
      current.fn();
      await settle();
    }
    clock = target;
    await settle();
  }

  return {
    backendBridge, onlineRef, events, summaryRequests, intervals,
    mount: settle, clickRoute, resolveSummary, rejectSummary, setProfileId, emitScan, advance,
    getState, getActiveRoute: activeRoute, clearEvents: () => { events.length = 0; },
    unmount: async () => { runtime.disposeEffects(); unmounted = true; await tick(); },
  };
}
