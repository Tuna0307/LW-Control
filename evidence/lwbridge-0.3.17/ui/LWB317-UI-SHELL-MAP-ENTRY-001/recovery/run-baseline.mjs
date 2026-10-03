import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
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
const sha256 = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
const baselinePath = path.join(here, "baseline-74ceaf7-App.jsx");
const assetPath = path.join(repo, "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js");
const resultPath = path.join(here, "baseline-results.json");

function originalNavigation() {
  const asset = fs.readFileSync(assetPath);
  const start = asset.indexOf(Buffer.from("function Tt(e){"), 360000);
  assert.equal(start, 364377, "exact original Tt byte start");
  const sourceEnd = asset.indexOf(Buffer.from("function Et(){"), start);
  assert.ok(sourceEnd > start, "exact original Tt end marker");
  const source = asset.subarray(start, sourceEnd).toString("utf8");
  return {
    source,
    make(activeRoute, events, gate = deferred()) {
      const refresh = () => {
        events.push({ event: "summary:start", activeRoute });
        return gate.promise;
      };
      const log = (message) => events.push({ event: "log", message });
      const preload = (routeKey) => events.push({ event: "preload", routeKey });
      const transition = (callback) => {
        events.push({ event: "transition:start", activeRoute });
        callback();
      };
      const setVisited = (updater) => {
        const next = updater(new Set([activeRoute]));
        events.push({ event: "visited", values: [...next] });
      };
      const setActive = (routeKey) => {
        events.push({ event: "active:set", from: activeRoute, to: routeKey });
        activeRoute = routeKey;
      };
      const fn = new Function("i", "_t", "F", "Wi", "c", "s", "a", `${source}\nreturn Tt;`)(activeRoute, refresh, log, preload, transition, setVisited, setActive);
      return { fn, gate, getActive: () => activeRoute };
    },
  };
}

function stateNamesForApp(source) {
  const ast = parse(source, { sourceType: "module", plugins: ["jsx"] });
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
  return { ast, names };
}

async function createAppHarness(source, label, options = {}) {
  const { ast, names: stateNames } = stateNamesForApp(source);
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
  const stateIndex = (name) => {
    const index = stateNames.indexOf(name);
    assert.ok(index >= 0, `${label}: state ${name}`);
    return index;
  };
  const activeRouteValue = () => runtime.stateCells[stateIndex("activeRoute")];
  const useTransition = () => [false, (callback) => {
    events.push({ event: "transition:start", activeRoute: activeRouteValue() });
    callback();
  }];
  const storage = new Map();
  const localStorage = {
    getItem: (key) => storage.has(key) ? storage.get(key) : null,
    setItem: (key, value) => storage.set(key, String(value)),
    removeItem: (key) => storage.delete(key),
  };
  const window = {
    location: { search: options.search || "" }, localStorage,
    setInterval: () => 1, clearInterval: () => {}, setTimeout: () => 1, clearTimeout: () => {}, matchMedia: () => ({ matches: true }),
  };
  const document = { documentElement: { dataset: {} }, addEventListener: () => {}, removeEventListener: () => {} };
  const backendBridge = {
    mode: "native", available: options.available !== false, profileId: options.profileId || "profile-a",
    listen: () => () => {},
    invoke: async (command) => {
      if (command === "game_root_status") return { valid: true, root: "C:/Fixture/Game" };
      if (command === "game_recovery_status") return { state: "idle" };
      if (command === "local_config_get") return { autoLaunchGame: false, autoReconnect: false };
      return {};
    },
  };
  const mapApi = {
    readStatus: async () => ({ xluaOnline: false }),
    readProxyStatus: async () => ({ gameRunning: true, repairRequired: false }),
    listenStatus: () => () => {}, listenScanStatus: () => () => {}, importServerJumpHistory: async () => [], setServerJumpHistory: async (x) => x,
    summary: () => {
      const request = deferred();
      const record = { id: summaryRequests.length + 1, activeRoute: activeRouteValue(), profileId: backendBridge.profileId, ...request };
      summaryRequests.push(record);
      events.push({ event: "summary:start", id: record.id, activeRoute: record.activeRoute, profileId: record.profileId });
      return request.promise;
    },
  };
  const createMapApi = () => mapApi;
  const connectionState = () => "disconnected";
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
    useState, useRef, useCallback, useEffect, useLayoutEffect, useTransition, Activity, Fragment,
    offlineDot, onlineDot, backendBridge, LANGUAGES, useI18n, DEFAULT_SCAN_STATE, connectionState, createMapApi,
    AUTO_SCAN_DEFAULT_TYPES, applyAutoScanConfigEdit, loadAutoScanConfig, normalizeAutoScanConfig, saveAutoScanConfig,
    NavIcon, PageForRoute, initialRouteKey, routes, window, document, localStorage, h,
  };
  const App = new Function(...Object.keys(bindings), `${code}\nreturn App;`)(...Object.values(bindings));
  let tree = null;
  let unmounted = false;

  function render() {
    runtime.beginRender();
    tree = App();
  }
  async function settle(maxRounds = 30) {
    if (unmounted) return;
    for (let round = 0; round < maxRounds; round += 1) {
      render();
      runtime.commitEffects();
      await Promise.resolve();
      await Promise.resolve();
      await tick();
      if (!runtime.isDirty()) return;
    }
    throw new Error(`${label}: did not settle`);
  }
  function routeButton(routeKey) {
    render();
    const nav = treeNodes(tree).find((node) => node.type === "nav" && node.props?.className === "side-nav");
    assert.ok(nav, `${label}: nav`);
    const route = routes.find((entry) => entry.key === routeKey);
    const button = treeNodes(nav).find((node) => node.type === "button" && nodeText(node).includes(route.labelKey));
    assert.ok(button, `${label}: route ${routeKey}`);
    return button;
  }
  async function clickRoute(routeKey) {
    events.push({ event: "click", routeKey, activeRoute: activeRouteValue() });
    routeButton(routeKey).props.onClick();
    await settle();
  }
  async function resolveRequest(request, serverId = 321) {
    request.resolve({ serverId, counts: {}, scanState: { ...DEFAULT_SCAN_STATE, serverId } });
    await settle();
  }
  return {
    backendBridge, events, summaryRequests, mount: settle, clickRoute, resolveRequest,
    getActiveRoute: activeRouteValue,
    clearEvents: () => { events.length = 0; },
    unmount: async () => { runtime.disposeEffects(); unmounted = true; await tick(); },
  };
}

const cases = [];
const original = originalNavigation();

{
  const events = [];
  const gate = deferred();
  const h = original.make("overview", events, gate);
  const returned = h.fn("map-data");
  assert.equal(returned, undefined, "original Tt is synchronous/non-awaiting");
  assert.deepEqual(events.slice(0, 3).map((entry) => entry.event), ["summary:start", "preload", "transition:start"]);
  assert.equal(h.getActive(), "map-data");
  gate.reject(new Error("controlled rejection"));
  await Promise.resolve();
  assert.equal(events.at(-1).event, "log");
  cases.push({ name: "original different-route Map entry starts summary before preload/transition and rejection does not block navigation", status: "PASS", events });
}

{
  const events = [];
  const h = original.make("map-data", events);
  h.fn("map-data");
  assert.deepEqual(events, []);
  cases.push({ name: "original active Map selection is inert", status: "PASS", events });
}

{
  const events = [];
  const h = original.make("overview", events);
  h.fn("automation");
  assert.deepEqual(events.map((entry) => entry.event), ["preload", "transition:start", "visited", "active:set"]);
  cases.push({ name: "original non-Map selection does not request summary", status: "PASS", events });
}

const baselineSource = fs.readFileSync(baselinePath, "utf8");
{
  const h = await createAppHarness(baselineSource, "baseline overview");
  await h.mount();
  assert.equal(h.summaryRequests.length, 1, "profile bootstrap owns one initial summary request");
  await h.resolveRequest(h.summaryRequests[0]);
  h.clearEvents();
  await h.clickRoute("map-data");
  assert.equal(h.summaryRequests.length, 2, "baseline adds one route-effect summary request");
  assert.deepEqual(h.events.map((entry) => entry.event), ["click", "transition:start", "summary:start"]);
  assert.equal(h.events.at(-1).activeRoute, "map-data", "baseline request starts only after Map is active");
  cases.push({ name: "baseline App has failing post-transition Map entry ordering", status: "EXPECTED_BASELINE_FAILURE", events: structuredClone(h.events) });
  await h.unmount();
}

{
  const h = await createAppHarness(baselineSource, "baseline initial map", { search: "?view=map-data" });
  await h.mount();
  assert.equal(h.getActiveRoute(), "map-data");
  assert.equal(h.summaryRequests.length, 2, "baseline initial Map duplicates bootstrap through route effect");
  cases.push({ name: "baseline direct initial Map route has bootstrap plus active-route request", status: "EXPECTED_BASELINE_FAILURE", requestCount: h.summaryRequests.length, events: structuredClone(h.events) });
  await h.unmount();
}

const report = {
  result: "LWB317_MAP_ENTRY_BASELINE_OK",
  source: {
    originalAsset: { path: "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js", sha256: sha256(fs.readFileSync(assetPath)) },
    originalTtByteStart: 364377,
    originalTtSha256: sha256(original.source),
    baselineApp: { path: "evidence/lwbridge-0.3.17/ui/LWB317-UI-SHELL-MAP-ENTRY-001/recovery/baseline-74ceaf7-App.jsx", sha256: sha256(baselineSource) },
  },
  cases,
  interpretation: "Exact original Tt starts map summary, then route preload, then React transition. It attaches catch and returns without awaiting settlement. Dispatch App instead transitions first and an activeRoute effect starts summary afterward; direct initial Map also receives an extra route-effect request in addition to the profile bootstrap request.",
  limits: "Original Tt is executed verbatim from the pinned asset with inert summary/log/preload/transition/setter bindings. Baseline App is the immutable dispatch App source executed through its actual callbacks/effects with a persistent hook runtime, inert page/icon children, controlled native bridge/map API, no network/native/gameplay. Connected polling is held disconnected so bootstrap and entry traffic are distinguishable.",
};
fs.writeFileSync(resultPath, `${JSON.stringify(report, null, 2)}\n`);
console.log(`LWB317_MAP_ENTRY_BASELINE_OK cases=${cases.length}`);
