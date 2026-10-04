import * as ShellState from "../../../../../src/LWBridge.UI-0.3.17/src/shellState.js";
import * as ShellTheme from "../../../../../src/LWBridge.UI-0.3.17/src/shellTheme.js";
// Shell-local location adapter of preserved continuation-snapshots/app-harness.mjs. Activity is inert; this proves App ownership only.
import assert from "node:assert/strict";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";
import {
  createHookRuntime,
  deferred,
  tick,
  treeNodes,
} from "../../LWB317-UI-MAP-REFRESH-FEEDBACK-001/harness.mjs";
import {
  DEFAULT_SCAN_STATE,
  connectionState,
} from "../../../../../src/LWBridge.UI-0.3.17/src/mapBackend.js";
import {
  AUTO_SCAN_DEFAULT_TYPES,
  applyAutoScanConfigEdit,
  loadAutoScanConfig,
  normalizeAutoScanConfig,
  saveAutoScanConfig,
} from "../../../../../src/LWBridge.UI-0.3.17/src/mapAutoConfig.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const requireFromUi = createRequire(path.join(repo, "src/LWBridge.UI-0.3.17/package.json"));

// Resolve the parser/build packages from the UI workspace even when this packet is
// executed from the repository root.
const babelParser = requireFromUi("@babel/parser");
const esbuild = requireFromUi("esbuild");

const sameObject = (value) => value && typeof value === "object" ? { ...value } : value;

export function mapSummary(serverId, count, patch = {}) {
  return {
    serverId,
    counts: Object.fromEntries(["city", "resource", "monster", "truck", "railway", "dispatch", "ghost", "treasure"].map((key) => [key, count])),
    scanState: {
      ...DEFAULT_SCAN_STATE,
      serverId,
      serverIdSource: serverId > 0 ? "fixture" : "none",
      selectedTypes: [...DEFAULT_SCAN_STATE.selectedTypes],
      ...patch,
    },
  };
}

export async function createAppHarness(source, label, options = {}) {
  // Parse the production App itself. Imported presentation components are inert;
  // state producers, callbacks and effects below are the exact compiled App body.
  const ast = babelParser.parse(source, { sourceType: "module", plugins: ["jsx"] });
  const appNode = (() => {
    for (const entry of ast.program.body) {
      const node = entry.type === "ExportNamedDeclaration" ? entry.declaration : entry;
      if (node?.type === "FunctionDeclaration" && node.id?.name === "App") return node;
    }
    return null;
  })();
  assert.ok(appNode, `${label}: App declaration`);

  const stateNames = [];
  const walk = (node) => {
    if (!node || typeof node !== "object") return;
    if (node.type === "VariableDeclarator" && node.id?.type === "ArrayPattern" && node.init?.callee?.name === "useState") {
      stateNames.push(node.id.elements[0]?.name);
    }
    for (const value of Object.values(node)) {
      if (Array.isArray(value)) value.forEach(walk);
      else if (value && typeof value === "object") walk(value);
    }
  };
  walk(appNode);

  const statements = ast.program.body
    .filter((node) => node.type !== "ImportDeclaration")
    .map((node) => node.type === "ExportNamedDeclaration" ? node.declaration : node)
    .filter(Boolean);
  const code = esbuild.transformSync(
    statements.map((node) => source.slice(node.start, node.end)).join("\n"),
    { loader: "jsx", jsxFactory: "h", jsxFragment: "Fragment", target: "es2022" },
  ).code;

  const runtime = createHookRuntime();
  const { useState, useRef, useCallback, useEffect, useLayoutEffect } = runtime.hooks;
  const useTransition = () => [false, (callback) => callback()];
  let clock = options.now ?? Date.UTC(2026, 9, 3, 0, 0, 0);
  let nextTimerId = 1;
  let renderCount = 0;
  let tree = null;
  let unmounted = false;
  let statusListener = null;
  let scanListener = null;
  let statusUnsubscribes = 0;
  let scanUnsubscribes = 0;
  let statusSubscribes = 0;
  let scanSubscribes = 0;
  let statusValue = options.status || { xluaOnline: true };
  let proxyValue = options.proxyStatus || { gameRunning: true, repairRequired: false };
  const intervals = new Map();
  const timeouts = new Map();
  const storage = new Map(Object.entries(options.storage || {}).map(([key, value]) => [key, String(value)]));
  const storageWrites = [];
  const bridgeListeners = new Map();
  const summaryRequests = [];
  const bridgeCalls = [];

  const fakeStorage = {
    getItem: (key) => storage.has(key) ? storage.get(key) : null,
    setItem: (key, value) => {
      storage.set(key, String(value));
      storageWrites.push([key, String(value)]);
    },
    removeItem: (key) => storage.delete(key),
  };
  const fakeWindow = {
    location: { search: options.search || "" },
    localStorage: fakeStorage,
    setInterval: (fn, delay) => {
      const id = nextTimerId++;
      intervals.set(id, { fn, delay, next: clock + delay });
      return id;
    },
    clearInterval: (id) => intervals.delete(id),
    setTimeout: (fn, delay) => {
      const id = nextTimerId++;
      timeouts.set(id, { fn, at: clock + delay });
      return id;
    },
    clearTimeout: (id) => timeouts.delete(id),
    matchMedia: () => ({ matches: true }),
  };
  const fakeDocument = {
    documentElement: { dataset: {} },
  };

  const backendBridge = {
    mode: "native",
    available: true,
    profileId: options.profileId || "profile-a",
    invoke: async (command, payload = {}) => {
      bridgeCalls.push({ command, payload: structuredClone(payload) });
      if (command === "game_root_status") return { valid: true, root: "C:/Fixture/Game" };
      if (command === "game_recovery_status") return { state: "idle" };
      if (command === "local_config_get") return { autoLaunchGame: false, autoReconnect: false };
      return {};
    },
    listen: (eventName, listener) => {
      bridgeListeners.set(eventName, listener);
      return () => {
        if (bridgeListeners.get(eventName) === listener) bridgeListeners.delete(eventName);
      };
    },
  };

  const api = {
    profileId: backendBridge.profileId,
    readStatus: async () => statusValue,
    readProxyStatus: async () => proxyValue,
    summary: () => {
      const request = deferred();
      summaryRequests.push({
        id: summaryRequests.length + 1,
        profileId: backendBridge.profileId,
        ...request,
      });
      return request.promise;
    },
    listenStatus: (listener) => {
      statusListener = listener;
      statusSubscribes += 1;
      return () => {
        statusUnsubscribes += 1;
        if (statusListener === listener) statusListener = null;
      };
    },
    listenScanStatus: (listener) => {
      scanListener = listener;
      scanSubscribes += 1;
      return () => {
        scanUnsubscribes += 1;
        if (scanListener === listener) scanListener = null;
      };
    },
    importServerJumpHistory: async () => [],
    setServerJumpHistory: async (history) => history,
    jumpServer: async (serverId) => ({ changed: true, previousServerId: serverId, serverId }),
  };

  const createMapApi = () => api;
  const stableSetLanguage = () => {};
  const stableTranslate = (key) => key;
  const useI18n = () => ({ language: "en", setLanguage: stableSetLanguage, t: stableTranslate });
  const LANGUAGES = [{ code: "en", label: "English" }];
  const routes = [
    { key: "overview", labelKey: "nav.overview" },
    { key: "map-data", labelKey: "nav.mapData" },
  ];
  const initialRouteKey = "overview";
  function NavIcon() { return null; }
  function PageForRoute() { return null; }
  const offlineDot = "offline.png";
  const onlineDot = "online.png";
  const h = (type, props, ...children) => ({ type, props: { ...(props || {}), children } });
  const Fragment = "Fragment";
  const Activity = "Activity";

  const bindings = {
    ...ShellState, ...ShellTheme,
    flushSync: callback => callback(),
    TopVersion: () => null, ShellConfigSaveErrors: () => null,
    ProfileSidebar: () => null, AppExitDialog: () => null, AppExitPrompt: () => null,
    ProfileSwitchState: () => null, GameAssetImageProvider: ({children}) => children,
    useState, useRef, useCallback, useEffect, useLayoutEffect, useTransition,
    Activity,
    offlineDot, onlineDot, backendBridge, LANGUAGES, useI18n,
    DEFAULT_SCAN_STATE, connectionState, createMapApi,
    AUTO_SCAN_DEFAULT_TYPES, applyAutoScanConfigEdit, loadAutoScanConfig, normalizeAutoScanConfig, saveAutoScanConfig,
    NavIcon, PageForRoute, initialRouteKey, routes,
    window: fakeWindow, document: fakeDocument, localStorage: fakeStorage, Date: { now: () => clock },
    h, Fragment,
  };
  const names = Object.keys(bindings);
  const values = Object.values(bindings);
  const App = new Function(...names, `${code}\nreturn App;`)(...values);

  function render() {
    runtime.beginRender();
    renderCount += 1;
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
    throw new Error(`${label}: App hook runtime did not settle`);
  }

  async function advance(ms) {
    const target = clock + ms;
    for (;;) {
      const dueIntervals = [...intervals.entries()].map(([id, timer]) => ({ kind: "interval", id, at: timer.next, timer }));
      const dueTimeouts = [...timeouts.entries()].map(([id, timer]) => ({ kind: "timeout", id, at: timer.at, timer }));
      const due = [...dueIntervals, ...dueTimeouts]
        .filter((entry) => entry.at <= target)
        .sort((left, right) => left.at - right.at || left.id - right.id)[0];
      if (!due) break;
      clock = due.at;
      if (due.kind === "interval") {
        if (!intervals.has(due.id)) continue;
        due.timer.next += due.timer.delay;
        due.timer.fn();
      } else {
        if (!timeouts.has(due.id)) continue;
        timeouts.delete(due.id);
        due.timer.fn();
      }
      await settle();
    }
    clock = target;
    await settle();
  }

  const getState = (name) => {
    const index = stateNames.indexOf(name);
    assert.ok(index >= 0, `${label}: state ${name}`);
    return runtime.stateCells[index];
  };
  const pageProps = () => {
    render();
    const retained = treeNodes(tree).find((candidate) => candidate.type?.name === "RetainedPages");
    const pageTree = retained ? retained.type(retained.props) : tree;
    const node = treeNodes(pageTree).find((candidate) => candidate.type === PageForRoute && candidate.props.routeKey === getState("activeRoute"));
    assert.ok(node, `${label}: PageForRoute boundary`);
    return node.props;
  };
  const emitStatus = async (next) => {
    statusValue = sameObject(next);
    assert.equal(typeof statusListener, "function", `${label}: status listener`);
    statusListener(statusValue);
    await settle();
  };
  const emitScan = async (next) => {
    assert.equal(typeof scanListener, "function", `${label}: scan listener`);
    scanListener(structuredClone(next));
    await settle();
  };
  const setProfileId = async (profileId) => {
    backendBridge.profileId = profileId;
    // The real application rerenders when profile context changes. This isolated
    // App harness uses a status acknowledgement as the render trigger.
    await emitStatus({ ...statusValue, harnessRevision: (statusValue.harnessRevision || 0) + 1 });
  };
  const resolveSummary = async (request, value) => {
    request.resolve(structuredClone(value));
    await settle();
  };
  const rejectSummary = async (request, error) => {
    request.reject(error);
    await settle();
  };
  const unmount = async () => {
    runtime.disposeEffects();
    unmounted = true;
    await Promise.resolve();
    await tick();
  };

  return {
    api,
    backendBridge,
    bridgeCalls,
    storage,
    storageWrites,
    summaryRequests,
    mount: settle,
    settle,
    advance,
    emitStatus,
    emitScan,
    setProfileId,
    resolveSummary,
    rejectSummary,
    unmount,
    getState,
    pageProps,
    now: () => clock,
    renderCount: () => renderCount,
    intervals: () => intervals.size,
    timeouts: () => timeouts.size,
    listenerCounts: () => ({ statusSubscribes, statusUnsubscribes, scanSubscribes, scanUnsubscribes }),
  };
}
