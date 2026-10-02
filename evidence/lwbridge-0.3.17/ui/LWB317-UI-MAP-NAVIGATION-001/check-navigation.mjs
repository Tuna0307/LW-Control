import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";
import * as backend from "../../../../src/LWBridge.UI-0.3.17/src/mapBackend.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../..");
const require = createRequire(path.join(repo, "src/LWBridge.UI-0.3.17/package.json"));
const { parse } = require("@babel/parser");
const { transformSync } = require("esbuild");
const contract = JSON.parse(fs.readFileSync(path.join(here, "source-contract.json"), "utf8"));

const sha256 = (value) => crypto.createHash("sha256").update(value).digest("hex");
const tick = () => new Promise((resolve) => setImmediate(resolve));
const deferred = () => {
  let resolve;
  let reject;
  const promise = new Promise((onResolve, onReject) => {
    resolve = onResolve;
    reject = onReject;
  });
  return { promise, resolve, reject };
};
const sameDeps = (left, right) =>
  Array.isArray(left)
  && Array.isArray(right)
  && left.length === right.length
  && left.every((value, index) => Object.is(value, right[index]));

function walk(node, result = []) {
  if (!node || typeof node !== "object") return result;
  if (node.type) result.push(node);
  for (const value of Object.values(node)) {
    if (Array.isArray(value)) value.forEach((child) => walk(child, result));
    else if (value && typeof value === "object") walk(value, result);
  }
  return result;
}

function treeNodes(node, result = []) {
  if (Array.isArray(node)) node.forEach((child) => treeNodes(child, result));
  else if (node && typeof node === "object") {
    result.push(node);
    treeNodes(node.props?.children, result);
  }
  return result;
}

function nodeText(node) {
  if (node == null || typeof node === "boolean") return "";
  if (Array.isArray(node)) return node.map(nodeText).join("");
  if (typeof node === "object") return nodeText(node.props?.children);
  return String(node);
}

function makeSummary(serverId) {
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

function createHarness(source, label) {
  const ast = parse(source, { sourceType: "module", plugins: ["jsx"] });
  const pageNode = walk(ast).find((node) => node.type === "FunctionDeclaration" && node.id?.name === "MapDataPage");
  assert.ok(pageNode, `${label}: MapDataPage declaration`);
  const stateNames = walk(pageNode)
    .filter((node) =>
      node.type === "VariableDeclarator"
      && node.id?.type === "ArrayPattern"
      && node.init?.callee?.name === "useState")
    .map((node) => node.id.elements[0]?.name);
  const refNames = walk(pageNode)
    .filter((node) =>
      node.type === "VariableDeclarator"
      && node.id?.type === "Identifier"
      && node.init?.callee?.name === "useRef")
    .map((node) => node.id.name);
  const statements = ast.program.body
    .filter((node) => node.type !== "ImportDeclaration")
    .map((node) => node.type === "ExportNamedDeclaration" ? node.declaration : node)
    .filter(Boolean);
  const code = transformSync(
    statements.map((node) => source.slice(node.start, node.end)).join("\n"),
    { loader: "jsx", jsxFactory: "h", jsxFragment: "Fragment", target: "es2022" },
  ).code;

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
  let tree = null;
  let serverId = 321;
  let statusListener = null;
  let nextTimerId = 1;
  const requests = [];
  const intervals = new Map();
  const timeouts = new Map();
  const storage = new Map();

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
    if (!current || !sameDeps(current.deps, deps)) {
      effectCells[slot] = { ...(current || {}), fn, deps, pending: true };
    }
  };

  const fakeWindow = {
    localStorage: {
      getItem: (key) => storage.has(key) ? storage.get(key) : null,
      setItem: (key, value) => storage.set(key, String(value)),
    },
    setInterval: (fn, delay) => {
      const id = nextTimerId++;
      intervals.set(id, { fn, delay });
      return id;
    },
    clearInterval: (id) => intervals.delete(id),
    setTimeout: (fn, delay) => {
      const id = nextTimerId++;
      timeouts.set(id, { fn, delay });
      return id;
    },
    clearTimeout: (id) => timeouts.delete(id),
  };
  const fakeDocument = { documentElement: { lang: "en" } };
  const h = (type, props, ...children) => ({ type, props: { ...(props || {}), children } });
  const useI18n = () => ({ language: "en", t: (key, values = {}) => {
    if (key === "common.itemCount") return `${values.count} items`;
    if (key === "map.pageInfo") return `${values.page}/${values.total}`;
    return key;
  } });
  const presentation = {
    buildMapColumns: () => [],
    mapNumber: (value) => String(value ?? ""),
    mapResourceStatus: () => "",
    mapRewardCount: (value) => String(value ?? ""),
    mapRewardName: (item) => String(item?.name || ""),
    mapTaskLabel: () => "",
    mapTaskSelectable: () => false,
    mapTaskState: () => "",
  };
  const api = {
    profileId: "navigation-harness",
    summary: async () => makeSummary(serverId),
    dataOptions: async (requestedServerId) => ({
      serverId: requestedServerId,
      counts: {},
      alliances: [],
      names: { resource: [], monster: [] },
      dispatchLevels: [],
      rewardItems: { truck: [], railway: [] },
      treasureTypes: [],
      noAllianceCount: 0,
    }),
    listenScanStatus: (listener) => {
      statusListener = listener;
      return () => { if (statusListener === listener) statusListener = null; };
    },
    search: (kind, query) => {
      const request = deferred();
      requests.push({ id: requests.length + 1, kind, query: structuredClone(query), ...request });
      return request.promise;
    },
    scanStatus: async () => makeSummary(serverId).scanState,
    start: async () => makeSummary(serverId).scanState,
    stop: async () => makeSummary(serverId).scanState,
    clear: async () => makeSummary(serverId).scanState,
    jumpServer: async () => ({}),
    coordinateJump: async () => ({}),
    setPlayerMark: async () => ({}),
    exportCities: async () => ({}),
  };

  const names = [
    "useState", "useRef", "useCallback", "useMemo", "useEffect", "useI18n",
    "window", "document", "h", "Fragment",
    ...Object.keys(backend), ...Object.keys(presentation),
  ];
  const values = [
    useState, useRef, useCallback, useMemo, useEffect, useI18n,
    fakeWindow, fakeDocument, h, "Fragment",
    ...Object.values(backend), ...Object.values(presentation),
  ];
  const Page = new Function(...names, `${code}\nreturn MapDataPage;`)(...values);
  const props = {
    mapApi: api,
    bridgeMode: "preview",
    backendAvailable: true,
    online: false,
    currentServerId: 321,
    previewState: "map-truck",
    gameTexts: {},
  };

  function render() {
    stateCursor = 0;
    refCursor = 0;
    callbackCursor = 0;
    memoCursor = 0;
    effectCursor = 0;
    renderDirty = false;
    tree = Page(props);
  }

  function commitEffects() {
    let ran = false;
    for (const cell of effectCells) {
      if (!cell?.pending) continue;
      ran = true;
      cell.pending = false;
      cell.cleanup?.();
      const cleanup = cell.fn();
      cell.cleanup = typeof cleanup === "function" ? cleanup : undefined;
    }
    return ran;
  }

  async function settle(maxRounds = 20) {
    for (let round = 0; round < maxRounds; round += 1) {
      render();
      const effectsRan = commitEffects();
      await Promise.resolve();
      await Promise.resolve();
      await tick();
      if (!renderDirty) {
        return;
      }
    }
    throw new Error(`${label}: hook runtime did not settle`);
  }

  const getState = (name) => {
    const index = stateNames.indexOf(name);
    assert.ok(index >= 0, `${label}: state ${name}`);
    return stateCells[index];
  };
  const getRef = (name) => {
    const index = refNames.indexOf(name);
    return index >= 0 ? refCells[index] : undefined;
  };
  const clickTab = async (kind) => {
    render();
    const labelByKind = {
      city: "map.city",
      resource: "map.resource",
      monster: "map.monster",
      truck: "map.truck",
      railway: "map.allianceTrain",
      dispatch: "map.secretTask",
      ghost: "map.ghostScout",
      treasure: "map.treasure",
      scheduledPlunder: "map.scheduledPlunder",
    };
    const button = treeNodes(tree).find((node) =>
      node.type === "button"
      && node.props?.role === "tab"
      && nodeText(node).includes(labelByKind[kind]));
    assert.ok(button, `${label}: tab ${kind}`);
    button.props.onClick();
    await settle();
  };
  const setPageThroughProductionProp = async (nextPage) => {
    render();
    const pagination = treeNodes(tree).find((node) => typeof node.type === "function" && node.type.name === "Pagination");
    assert.ok(pagination, `${label}: Pagination caller`);
    pagination.props.onPage(nextPage);
    await settle();
  };
  const emitServer = async (nextServerId) => {
    assert.equal(typeof statusListener, "function", `${label}: scan status listener`);
    serverId = nextServerId;
    statusListener(makeSummary(nextServerId).scanState);
    await settle();
  };
  const resolveRequest = async (request, result) => {
    request.resolve(result);
    await settle();
  };
  const rejectRequest = async (request, error) => {
    request.reject(error);
    await settle();
  };

  return {
    label,
    requests,
    mount: settle,
    settle,
    getState,
    getRef,
    clickTab,
    setPage: setPageThroughProductionProp,
    emitServer,
    resolveRequest,
    rejectRequest,
    requestCount: () => requests.length,
    currentRequest: () => requests.at(-1),
  };
}

async function campaign(source, label, strict) {
  const h = createHarness(source, label);
  const failures = [];
  const check = (name, fn) => {
    try {
      fn();
    } catch (error) {
      failures.push({ name, message: error.message });
      if (strict) throw error;
    }
  };
  const checkAsync = async (name, fn) => {
    try {
      await fn();
    } catch (error) {
      failures.push({ name, message: error.message });
      if (strict) throw error;
    }
  };

  await h.mount();
  const initialTruck = h.currentRequest();
  check("initial Truck search uses server 321/page 1", () => {
    assert.equal(initialTruck.kind, "truck");
    assert.equal(initialTruck.query.serverId, 321);
    assert.equal(initialTruck.query.page, 1);
    assert.equal(h.getState("loading"), true);
  });
  await h.resolveRequest(initialTruck, {
    rows: [{ recordKey: "truck-1" }, { recordKey: "truck-2" }],
    total: 55,
  });

  await h.setPage(2);
  const truckPage2 = h.currentRequest();
  check("Truck page 2 request", () => assert.equal(truckPage2.query.page, 2));
  await h.resolveRequest(truckPage2, {
    rows: [{ recordKey: "truck-51" }, { recordKey: "truck-52" }],
    total: 55,
  });

  const beforeTrain = h.requestCount();
  await h.clickTab("railway");
  const trainPage1 = h.currentRequest();
  check("uncached Train starts empty/page1/loading and refreshes", () => {
    assert.equal(trainPage1.kind, "railway");
    assert.equal(trainPage1.query.page, 1);
    assert.equal(h.getState("page"), 1);
    assert.deepEqual(h.getState("rows"), []);
    assert.equal(h.getState("total"), 0);
    assert.equal(h.getState("loading"), true);
    assert.equal(h.requestCount(), beforeTrain + 1);
  });
  await h.resolveRequest(trainPage1, {
    rows: [{ recordKey: "train-1" }],
    total: 120,
  });
  await h.setPage(3);
  const trainPage3 = h.currentRequest();
  await h.resolveRequest(trainPage3, {
    rows: [{ recordKey: "train-101" }],
    total: 120,
  });

  await h.clickTab("truck");
  const truckRefresh = h.currentRequest();
  check("Truck page2 cached view restores before fresh search", () => {
    assert.equal(h.getState("page"), 2);
    assert.deepEqual(h.getState("rows"), [{ recordKey: "truck-51" }, { recordKey: "truck-52" }]);
    assert.equal(h.getState("total"), 55);
    assert.equal(h.getState("loading"), true);
    assert.equal(truckRefresh.kind, "truck");
    assert.equal(truckRefresh.query.page, 2);
  });
  await h.resolveRequest(truckRefresh, {
    rows: [{ recordKey: "truck-51-fresh" }],
    total: 55,
  });

  await h.clickTab("railway");
  const trainRefresh = h.currentRequest();
  check("independent Train page3 view restores", () => {
    assert.equal(h.getState("page"), 3);
    assert.deepEqual(h.getState("rows"), [{ recordKey: "train-101" }]);
    assert.equal(trainRefresh.query.page, 3);
  });
  await h.resolveRequest(trainRefresh, {
    rows: [{ recordKey: "train-101-fresh" }],
    total: 120,
  });

  const sameTabRequests = h.requestCount();
  const sameTabSnapshot = {
    page: h.getState("page"),
    rows: structuredClone(h.getState("rows")),
    total: h.getState("total"),
    loading: h.getState("loading"),
  };
  await h.clickTab("railway");
  check("active tab click is a no-op", () => {
    assert.equal(h.requestCount(), sameTabRequests);
    assert.deepEqual({
      page: h.getState("page"),
      rows: h.getState("rows"),
      total: h.getState("total"),
      loading: h.getState("loading"),
    }, sameTabSnapshot);
  });

  await h.clickTab("truck");
  const rapidA1 = h.currentRequest();
  await h.clickTab("railway");
  const rapidB = h.currentRequest();
  await h.clickTab("truck");
  const rapidA2 = h.currentRequest();
  check("rapid A/B/A final request owns loading", () => assert.equal(h.getState("loading"), true));
  const rapidGeneration = h.getRef("searchGeneration")?.current;
  check("rapid A/B/A creates three distinct requests", () => {
    assert.notEqual(rapidA1.id, rapidB.id);
    assert.notEqual(rapidB.id, rapidA2.id);
    assert.deepEqual([rapidA1.kind, rapidB.kind, rapidA2.kind], ["truck", "railway", "truck"]);
  });
  await h.resolveRequest(rapidA1, { rows: [{ recordKey: "obsolete-a" }], total: 1 });
  check("obsolete success cannot replace active view or clear loading", () => {
    assert.notDeepEqual(h.getState("rows"), [{ recordKey: "obsolete-a" }]);
    assert.equal(h.getState("loading"), true);
  });
  await h.rejectRequest(rapidB, Object.assign(new Error("obsolete failure"), { code: "OBSOLETE" }));
  check("obsolete failure/finally cannot clear active view/loading", () => {
    assert.equal(h.getState("queryError"), "");
    assert.equal(h.getState("loading"), true);
    if (rapidGeneration != null) assert.equal(h.getRef("searchGeneration")?.current, rapidGeneration);
  });
  await h.resolveRequest(rapidA2, { rows: [{ recordKey: "final-a" }], total: 55 });
  check("current rapid request wins", () => {
    if (rapidGeneration != null) assert.equal(h.getRef("searchGeneration")?.current, rapidGeneration, "active request generation changed before resolution");
    assert.equal(h.currentRequest().id, rapidA2.id, "unexpected replacement search started before active reply");
    assert.deepEqual(h.getState("rows"), [{ recordKey: "final-a" }]);
    assert.equal(h.getState("total"), 55);
    assert.equal(h.getState("loading"), false);
  });

  await h.clickTab("railway");
  const failureRequest = h.currentRequest();
  await h.rejectRequest(failureRequest, Object.assign(new Error("current failure"), { code: "MAP_QUERY_FAILED" }));
  check("current failure clears rows/total and reports error", () => {
    assert.deepEqual(h.getState("rows"), []);
    assert.equal(h.getState("total"), 0);
    assert.match(h.getState("queryError"), /MAP_QUERY_FAILED: current failure/);
    assert.equal(h.getState("loading"), false);
  });

  await h.setPage(2);
  const shrinking = h.currentRequest();
  await h.resolveRequest(shrinking, { rows: [], total: 20 });
  const clamped = h.currentRequest();
  check("shrinking result clamps page and re-queries without committing stale page", () => {
    assert.equal(h.getState("page"), 1);
    assert.equal(clamped.query.page, 1);
    assert.equal(h.getState("loading"), true);
  });
  await h.resolveRequest(clamped, { rows: [{ recordKey: "clamped-1" }], total: 20 });
  check("clamped page commits fresh result", () => {
    assert.deepEqual(h.getState("rows"), [{ recordKey: "clamped-1" }]);
    assert.equal(h.getState("total"), 20);
  });

  await h.emitServer(322);
  const server322 = h.currentRequest();
  check("positive server change invalidates all tab views", () => {
    assert.equal(h.getState("page"), 1);
    assert.deepEqual(h.getState("rows"), []);
    assert.equal(h.getState("total"), 0);
    assert.equal(h.getState("loading"), true);
    assert.equal(server322.query.serverId, 322);
    assert.equal(server322.query.page, 1);
  });
  await h.resolveRequest(server322, { rows: [{ recordKey: "server-322-train" }], total: 1 });
  await h.clickTab("truck");
  const truck322 = h.currentRequest();
  check("old-server Truck cache is not restored", () => {
    assert.equal(h.getState("page"), 1);
    assert.deepEqual(h.getState("rows"), []);
    assert.equal(h.getState("total"), 0);
    assert.equal(truck322.query.serverId, 322);
  });
  await h.resolveRequest(truck322, { rows: [{ recordKey: "server-322-truck" }], total: 1 });

  const beforeLossRequests = h.requestCount();
  await h.emitServer(0);
  check("server loss invalidates cache and leaves loading false with no search", () => {
    assert.equal(h.getState("page"), 1);
    assert.deepEqual(h.getState("rows"), []);
    assert.equal(h.getState("total"), 0);
    assert.equal(h.getState("loading"), false);
    assert.equal(h.requestCount(), beforeLossRequests);
  });

  await h.emitServer(321);
  const restoredServer = h.currentRequest();
  await h.resolveRequest(restoredServer, { rows: [{ recordKey: "server-restored-truck" }], total: 55 });
  await h.setPage(2);
  const restoredPage2 = h.currentRequest();
  await h.resolveRequest(restoredPage2, { rows: [{ recordKey: "server-restored-truck-51" }], total: 55 });
  const beforeScheduled = h.requestCount();
  await h.clickTab("scheduledPlunder");
  check("Scheduled Plunder entry is a navigation boundary", () => {
    assert.equal(h.getState("tab"), "scheduledPlunder");
    assert.equal(h.getState("page"), 2);
    assert.deepEqual(h.getState("rows"), [{ recordKey: "server-restored-truck-51" }]);
    assert.equal(h.getState("loading"), false);
    assert.equal(h.requestCount(), beforeScheduled);
  });
  await h.clickTab("truck");
  const afterScheduled = h.currentRequest();
  check("Scheduled Plunder exit restores normal view then refreshes", () => {
    assert.equal(h.getState("page"), 2);
    assert.deepEqual(h.getState("rows"), [{ recordKey: "server-restored-truck-51" }]);
    assert.equal(afterScheduled.kind, "truck");
    assert.equal(afterScheduled.query.page, 2);
    assert.equal(h.getState("loading"), true);
  });
  await h.resolveRequest(afterScheduled, { rows: [{ recordKey: "server-restored-truck-51-fresh" }], total: 55 });

  await checkAsync("final normal request settles cleanly", async () => {
    assert.equal(h.getState("loading"), false);
    assert.equal(h.getState("queryError"), "");
  });
  return { failures, requests: h.requests.map(({ kind, query }) => ({ kind, query })) };
}

const originalPath = path.join(repo, contract.source.path);
assert.equal(sha256(fs.readFileSync(originalPath)), contract.source.sha256);
const baselinePath = path.join(here, "baseline.MapDataPage.jsx");
const currentPath = path.join(repo, "src/LWBridge.UI-0.3.17/src/MapDataPage.jsx");
const baseline = fs.readFileSync(baselinePath, "utf8");
const current = fs.readFileSync(currentPath, "utf8");
const baselineResult = await campaign(baseline, "baseline", false);
assert.ok(baselineResult.failures.length > 0, "baseline must reproduce at least one navigation defect");
const currentResult = await campaign(current, "current", true);
assert.deepEqual(currentResult.failures, []);

const output = {
  status: "PASS",
  source: contract.source,
  baseline: {
    path: "evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-NAVIGATION-001/baseline.MapDataPage.jsx",
    sha256: sha256(baseline),
    failures: baselineResult.failures,
  },
  current: {
    path: "src/LWBridge.UI-0.3.17/src/MapDataPage.jsx",
    sha256: sha256(current),
    failures: currentResult.failures,
  },
  scope: "Actual production MapDataPage callbacks and useEffect bodies with persistent hook state and deferred search promises; synthetic local Map responses only. No native/gameplay execution.",
  scenarios: [
    "Truck page 2 -> Train -> Truck restoration",
    "independent normal-tab pages",
    "cached/uncached loading",
    "active-tab no-op",
    "rapid A/B/A obsolete success/failure/finally",
    "current search failure",
    "shrinking page-count clamp/requery",
    "positive server switch and server loss",
    "Scheduled Plunder entry/exit navigation boundary",
  ],
};
if (process.argv.includes("--record")) {
  fs.writeFileSync(path.join(here, "navigation-results.json"), JSON.stringify(output, null, 2) + "\n");
}
console.log(`LWB317_MAP_NAVIGATION_OK baseline=${baselineResult.failures.length} current=0 requests=${currentResult.requests.length}`);
