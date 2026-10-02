// Replay adapter for the actual production MapDataPage callbacks and effects.
//
// Why a new adapter: LWB317-UI-MAP-NAVIGATION-001/check-navigation.mjs hard-wires the
// import surface of MapDataPage.jsx (mapBackend.js + mapTablePresentation.js). Once the
// page imports further local modules that harness can no longer evaluate the page. This
// adapter reads the IMPORT DECLARATIONS of the source it is given, binds every relative
// `.js` import to the real module, replaces `.jsx` components with named stubs and
// provides a persistent hook runtime (state/ref/callback/memo/effect cells, React-ordered
// cleanup-then-effect commit, unmount, props replacement, controlled clock/timers).
// It evaluates the unmodified production source text; it never re-implements page logic.
//
// The interface of the old harness (label, requests, mount, settle, getState, getRef,
// clickTab, setPage, emitServer, resolveRequest, rejectRequest, requestCount,
// currentRequest) is preserved so historical scenario functions can be replayed.
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath, pathToFileURL } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
export const repo = path.resolve(here, "../../../..");
export const uiSrc = path.join(repo, "src/LWBridge.UI-0.3.17/src");
const require = createRequire(path.join(repo, "src/LWBridge.UI-0.3.17/package.json"));
const { parse } = require("@babel/parser");
const { transformSync } = require("esbuild");

export const tick = () => new Promise((resolve) => setImmediate(resolve));
export const deferred = () => {
  let resolve;
  let reject;
  const promise = new Promise((onResolve, onReject) => { resolve = onResolve; reject = onReject; });
  return { promise, resolve, reject };
};
const sameDeps = (left, right) =>
  Array.isArray(left) && Array.isArray(right) && left.length === right.length
  && left.every((value, index) => Object.is(value, right[index]));

export function walk(node, result = []) {
  if (!node || typeof node !== "object") return result;
  if (node.type) result.push(node);
  for (const value of Object.values(node)) {
    if (Array.isArray(value)) value.forEach((child) => walk(child, result));
    else if (value && typeof value === "object") walk(value, result);
  }
  return result;
}
export function treeNodes(node, result = []) {
  if (Array.isArray(node)) node.forEach((child) => treeNodes(child, result));
  else if (node && typeof node === "object") {
    result.push(node);
    treeNodes(node.props?.children, result);
  }
  return result;
}
export function nodeText(node) {
  if (node == null || typeof node === "boolean") return "";
  if (Array.isArray(node)) return node.map(nodeText).join("");
  if (typeof node === "object") return nodeText(node.props?.children);
  return String(node);
}

export function makeSummary(backend, serverId) {
  return {
    serverId,
    counts: Object.fromEntries(backend.MAP_KIND_KEYS.map((kind) => [kind, 60])),
    scanState: {
      ...backend.DEFAULT_SCAN_STATE,
      serverId,
      serverIdSource: serverId > 0 ? "fixture" : "none",
      selectedTypes: [...backend.MAP_KIND_KEYS],
      scanMode: "normal",
    },
  };
}

const moduleCache = new Map();
async function realModule(file) {
  if (!moduleCache.has(file)) moduleCache.set(file, await import(pathToFileURL(file).href));
  return moduleCache.get(file);
}

// Persistent hook runtime shared by every adapter (canonical page and original component).
export function createHookRuntime() {
  const stateCells = [];
  const refCells = [];
  const callbackCells = [];
  const memoCells = [];
  const effectCells = [];
  let stateCursor = 0;
  let refCursor = 0;
  let callbackCursor = 0;
  let memoCursor = 0;
  let effectCursor = 0;
  let renderDirty = false;
  const setCell = (slot, value) => {
    const next = typeof value === "function" ? value(stateCells[slot]) : value;
    if (!Object.is(next, stateCells[slot])) {
      stateCells[slot] = next;
      renderDirty = true;
    }
  };
  const useState = (initial) => {
    const slot = stateCursor++;
    if (!(slot in stateCells)) stateCells[slot] = typeof initial === "function" ? initial() : initial;
    return [stateCells[slot], (value) => setCell(slot, value)];
  };
  const useRef = (initial) => {
    const slot = refCursor++;
    if (!(slot in refCells)) refCells[slot] = { current: initial };
    return refCells[slot];
  };
  const useCallback = (fn, deps) => {
    const slot = callbackCursor++;
    const current = callbackCells[slot];
    if (!current || !sameDeps(current.deps, deps)) callbackCells[slot] = { value: fn, deps };
    return callbackCells[slot].value;
  };
  const useMemo = (fn, deps) => {
    const slot = memoCursor++;
    const current = memoCells[slot];
    if (!current || !sameDeps(current.deps, deps)) memoCells[slot] = { value: fn(), deps };
    return memoCells[slot].value;
  };
  const useEffect = (fn, deps) => {
    const slot = effectCursor++;
    const current = effectCells[slot];
    if (!current || deps === undefined || !sameDeps(current.deps, deps)) {
      effectCells[slot] = { ...(current || {}), fn, deps, pending: true };
    }
  };
  return {
    hooks: { useState, useRef, useCallback, useMemo, useEffect, useLayoutEffect: useEffect, memo: (fn) => fn },
    stateCells,
    refCells,
    beginRender() {
      stateCursor = 0; refCursor = 0; callbackCursor = 0; memoCursor = 0; effectCursor = 0; renderDirty = false;
    },
    isDirty: () => renderDirty,
    // React order: every cleanup of a changed effect first, then every new effect body.
    commitEffects() {
      const pending = effectCells.filter((cell) => cell?.pending);
      for (const cell of pending) { cell.pending = false; cell.cleanup?.(); cell.cleanup = undefined; }
      for (const cell of pending) {
        const cleanup = cell.fn();
        cell.cleanup = typeof cleanup === "function" ? cleanup : undefined;
      }
      return pending.length > 0;
    },
    disposeEffects() {
      for (const cell of effectCells) { cell?.cleanup?.(); if (cell) cell.cleanup = undefined; }
    },
  };
}

export const TAB_LABEL_KEYS = {
  city: "map.city", resource: "map.resource", monster: "map.monster", truck: "map.truck",
  railway: "map.allianceTrain", dispatch: "map.secretTask", ghost: "map.ghostScout",
  treasure: "map.treasure", scheduledPlunder: "map.scheduledPlunder",
};

// options: { baseDir, stubTranslate(key, values) -> string, now, props, makeApi(backend, ctx) }
export async function createHarness(source, label, options = {}) {
  const baseDir = options.baseDir || uiSrc;
  const backend = await realModule(path.join(uiSrc, "mapBackend.js"));
  const ast = parse(source, { sourceType: "module", plugins: ["jsx"] });
  const pageNode = walk(ast).find((node) => node.type === "FunctionDeclaration" && node.id?.name === "MapDataPage");
  assert.ok(pageNode, `${label}: MapDataPage declaration`);
  const stateNames = walk(pageNode)
    .filter((node) => node.type === "VariableDeclarator" && node.id?.type === "ArrayPattern" && node.init?.callee?.name === "useState")
    .map((node) => node.id.elements[0]?.name);
  const refNames = walk(pageNode)
    .filter((node) => node.type === "VariableDeclarator" && node.id?.type === "Identifier" && node.init?.callee?.name === "useRef")
    .map((node) => node.id.name);

  // ---- bind the page's own import declarations -------------------------------------
  const bindings = {};
  const importSources = [];
  for (const node of ast.program.body.filter((entry) => entry.type === "ImportDeclaration")) {
    const from = node.source.value;
    importSources.push(from);
    if (from === "react") continue; // hooks are supplied by the runtime below
    const file = path.resolve(baseDir, from);
    for (const specifier of node.specifiers) {
      const imported = specifier.imported?.name || "default";
      const local = specifier.local.name;
      if (from.endsWith(".jsx")) {
        // Presentational components are inert in the callback/effect runtime; the tree keeps them
        // as typed elements so their props (production callbacks) can be invoked.
        const stub = { [imported]: function () { return null; } }[imported];
        bindings[local] = stub;
      } else if (from.endsWith(".js")) {
        const module = await realModule(file);
        assert.ok(imported in module, `${label}: ${from} has no export ${imported}`);
        bindings[local] = module[imported];
      } else {
        throw new Error(`${label}: unsupported import ${from}`);
      }
    }
  }
  const statements = ast.program.body
    .filter((node) => node.type !== "ImportDeclaration")
    .map((node) => node.type === "ExportNamedDeclaration" ? node.declaration : node)
    .filter(Boolean);
  const code = transformSync(
    statements.map((node) => source.slice(node.start, node.end)).join("\n"),
    { loader: "jsx", jsxFactory: "h", jsxFragment: "Fragment", target: "es2022" },
  ).code;

  // ---- persistent hook runtime -----------------------------------------------------
  const runtime = createHookRuntime();
  const { stateCells, refCells } = runtime;
  const { useState, useRef, useCallback, useMemo, useEffect } = runtime.hooks;
  let tree = null;
  let serverId = options.serverId ?? 321;
  let statusListener = null;
  let nextTimerId = 1;
  let clock = options.now ?? Date.UTC(2026, 9, 2, 12, 0, 0);
  let unmounted = false;
  let renderCount = 0;
  const requests = [];
  const intervals = new Map();
  const timeouts = new Map();
  const storage = new Map();
  const apiCalls = [];

  class FakeDate extends Date {
    constructor(...args) { if (args.length === 0) super(clock); else super(...args); }
    static now() { return clock; }
  }
  const fakeWindow = {
    localStorage: {
      getItem: (key) => storage.has(key) ? storage.get(key) : null,
      setItem: (key, value) => storage.set(key, String(value)),
    },
    setInterval: (fn, delay) => { const id = nextTimerId++; intervals.set(id, { fn, delay, next: clock + delay }); return id; },
    clearInterval: (id) => intervals.delete(id),
    setTimeout: (fn, delay) => { const id = nextTimerId++; timeouts.set(id, { fn, delay, at: clock + delay }); return id; },
    clearTimeout: (id) => timeouts.delete(id),
  };
  const fakeDocument = { documentElement: { lang: options.language || "en" } };
  const h = (type, props, ...children) => ({ type, props: { ...(props || {}), children } });
  const translate = options.translate || ((key, values = {}) => {
    if (key === "common.itemCount") return `${values.count} items`;
    if (key === "map.pageInfo") return `${values.page}/${values.total}`;
    if (key === "map.scheduleSelected" || key === "map.scheduleSelectedTrucks") return `${key}:${values.count}`;
    return key;
  });
  const useI18n = () => ({ language: options.language || "en", t: translate });

  const makeApi = options.makeApi || ((ctx) => ({
    profileId: "interactions-harness",
    summary: async () => makeSummary(backend, ctx.getServerId()),
    dataOptions: async (requestedServerId) => ({
      serverId: requestedServerId,
      counts: {},
      alliances: [],
      names: { resource: [], monster: [] },
      dispatchLevels: [],
      rewardItems: { truck: [], railway: [] },
      treasureTypes: [],
      noAllianceCount: 0,
      ...(options.dataOptions || {}),
    }),
    listenScanStatus: (listener) => {
      ctx.setStatusListener(listener);
      return () => ctx.clearStatusListener(listener);
    },
    search: (kind, query) => {
      const request = deferred();
      const entry = { id: requests.length + 1, kind, query: structuredClone(query), ...request };
      requests.push(entry);
      return request.promise;
    },
    scanStatus: async () => makeSummary(backend, ctx.getServerId()).scanState,
    start: async () => makeSummary(backend, ctx.getServerId()).scanState,
    stop: async () => makeSummary(backend, ctx.getServerId()).scanState,
    clear: async () => makeSummary(backend, ctx.getServerId()).scanState,
    jumpServer: async () => ({}),
    coordinateJump: async () => ({}),
    setPlayerMark: async () => ({}),
    exportCities: async () => ({}),
  }));
  const ctx = {
    getServerId: () => serverId,
    setStatusListener: (listener) => { statusListener = listener; },
    clearStatusListener: (listener) => { if (statusListener === listener) statusListener = null; },
    requests,
    apiCalls,
  };
  const api = makeApi(ctx);
  Object.assign(api, options.apiExtensions?.(ctx) || {});

  const hookBindings = { useState, useRef, useCallback, useMemo, useEffect, useI18n };
  const names = [
    ...Object.keys(hookBindings), "window", "document", "Date", "h", "Fragment", ...Object.keys(bindings),
  ];
  const values = [
    ...Object.values(hookBindings), fakeWindow, fakeDocument, FakeDate, h, "Fragment", ...Object.values(bindings),
  ];
  // useI18n is imported from i18n.jsx by the page: that import is a .jsx stub above, so the
  // hook binding must win. Remove the stub from the binding list.
  const dedupNames = [];
  const dedupValues = [];
  names.forEach((name, index) => {
    if (dedupNames.includes(name)) {
      dedupValues[dedupNames.indexOf(name)] = hookBindings[name] ?? dedupValues[dedupNames.indexOf(name)];
      return;
    }
    dedupNames.push(name);
    dedupValues.push(values[index]);
  });
  const Page = new Function(...dedupNames, `${code}\nreturn MapDataPage;`)(...dedupValues);
  const props = {
    mapApi: api,
    bridgeMode: "preview",
    backendAvailable: true,
    online: false,
    currentServerId: 321,
    previewState: "map-truck",
    gameTexts: {},
    ...(options.props || {}),
  };

  function render() {
    runtime.beginRender();
    renderCount += 1;
    tree = Page(props);
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
    throw new Error(`${label}: hook runtime did not settle`);
  }

  async function unmount() {
    runtime.disposeEffects();
    unmounted = true;
  }

  async function advance(ms) {
    const target = clock + ms;
    for (;;) {
      const due = [...intervals.entries()].filter(([, timer]) => timer.next <= target).sort((a, b) => a[1].next - b[1].next)[0];
      if (!due) break;
      clock = due[1].next;
      due[1].next += due[1].delay;
      due[1].fn();
    }
    clock = target;
    await settle();
  }

  const getState = (name) => {
    const index = stateNames.indexOf(name);
    assert.ok(index >= 0, `${label}: state ${name}`);
    return stateCells[index];
  };
  const hasState = (name) => stateNames.includes(name);
  const getRef = (name) => {
    const index = refNames.indexOf(name);
    return index >= 0 ? refCells[index] : undefined;
  };
  const findNodes = (predicate) => treeNodes(tree).filter(predicate);
  const clickTab = async (kind) => {
    render();
    const button = findNodes((node) => node.type === "button" && node.props?.role === "tab" && nodeText(node).includes(TAB_LABEL_KEYS[kind]))[0];
    assert.ok(button, `${label}: tab ${kind}`);
    button.props.onClick();
    await settle();
  };
  const setPageThroughProductionProp = async (nextPage) => {
    render();
    const pagination = findNodes((node) => typeof node.type === "function" && node.type.name === "Pagination")[0];
    assert.ok(pagination, `${label}: Pagination caller`);
    pagination.props.onPage(nextPage);
    await settle();
  };
  const emitServer = async (nextServerId) => {
    assert.equal(typeof statusListener, "function", `${label}: scan status listener`);
    serverId = nextServerId;
    statusListener(makeSummary(backend, nextServerId).scanState);
    await settle();
  };
  const resolveRequest = async (request, result) => { request.resolve(result); await settle(); };
  const rejectRequest = async (request, error) => { request.reject(error); await settle(); };
  const setProps = async (next) => { Object.assign(props, next); await settle(); };

  return {
    label, requests, calls: apiCalls, ctx, api, props, mount: settle, settle, unmount, advance, setProps,
    getState, hasState, getRef, clickTab, setPage: setPageThroughProductionProp, emitServer,
    resolveRequest, rejectRequest,
    requestCount: () => requests.length,
    currentRequest: () => requests.at(-1),
    tree: () => { render(); return tree; },
    findNodes: (predicate) => { render(); return findNodes(predicate); },
    renderCount: () => renderCount,
    now: () => clock,
    setNow: (value) => { clock = value; },
    stateNames, refNames, importSources,
    liveStatusListener: () => statusListener,
  };
}
