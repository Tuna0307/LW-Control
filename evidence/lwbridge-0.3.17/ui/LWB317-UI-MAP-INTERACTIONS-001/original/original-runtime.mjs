// Executes the ACTUAL original LWBridge 0.3.17 Map panel component `R` (exported MapDataPanel) from the
// asset bytes. Only the import header and the export footer of the module are rewritten:
//   - the three import declarations become the parameters of the evaluating function,
//   - `export{R as MapDataPanel};` becomes a `return` (plus read access to module-level helpers).
// Everything between (the component, its helpers and the child components) is the unmodified asset text.
//
// Dependencies are bound as follows (see original/contract.md alias table):
//   pure helpers (Ie, Fe, Lr+Ir, Ci, wi, Ti, __toESM u, rewardDisplay e) are the REAL functions, extracted
//   from the hash-pinned asset bytes by AST and executed;
//   every backend/native-facing import is an inert recording stub; the search stub (ce) returns deferred
//   promises recorded in `requests`.
// Nothing native is executed: stubs only record calls and return harness-controlled promises.
import assert from "node:assert/strict";
import {
  campaignDir, loadIndex, loadPanel, readAsset, rawOf, walkDirect, transformSync, originalDir,
} from "./lib.mjs";
import { STATE_SEMANTICS, REF_SEMANTICS, IMPORT_ALIASES, TAB_LABEL_KEYS } from "./semantics.mjs";
import { createHookRuntime, deferred, tick, treeNodes, nodeText } from "../harness.mjs";
import path from "node:path";
import { pathToFileURL } from "node:url";
import fs from "node:fs";

export { deferred, tick, treeNodes, nodeText, STATE_SEMANTICS, REF_SEMANTICS, IMPORT_ALIASES, TAB_LABEL_KEYS };

export const ORIGINAL_SHA256 = readAsset("MapDataPanel-B4GXEND2.js").sha256;

// ---------------------------------------------------------------------------------------------
// Static extraction from the asset bytes (cached across harness instances).
let staticCache;
function loadStatic() {
  if (staticCache) return staticCache;
  const panel = loadPanel();
  const index = loadIndex();
  const reward = readAsset("rewardDisplay-eZWrd6iS.js");

  // --- state / ref declarations of R, asserted against semantics.mjs
  const states = [];
  const refs = [];
  walkDirect(panel.R.body, (node) => {
    if (node.type !== "VariableDeclarator" || node.init?.type !== "CallExpression") return;
    const callee = rawOf(panel.entry, node.init.callee).replace(/^\(|\)$/g, "");
    const arg = node.init.arguments[0];
    if (callee === "0,y.useState") {
      states.push({ value: node.id.elements[0].name, setter: node.id.elements[1].name, init: arg ? rawOf(panel.entry, arg) : "" });
    } else if (callee === "0,y.useRef") {
      refs.push({ value: node.id.name, init: arg ? rawOf(panel.entry, arg) : "" });
    }
  }, panel.R.body);
  assert.equal(states.length, STATE_SEMANTICS.length, "useState count");
  STATE_SEMANTICS.forEach(([semantic, value, setter, init], i) => {
    assert.deepEqual(states[i], { value, setter, init }, `useState #${i} (${semantic}) differs from the asset`);
  });
  assert.equal(refs.length, REF_SEMANTICS.length, "useRef count");
  REF_SEMANTICS.forEach(([semantic, value, init], i) => {
    assert.deepEqual(refs[i], { value, init }, `useRef #${i} (${semantic}) differs from the asset`);
  });
  const memoCalls = [];
  walkDirect(panel.ast.program, (node) => {
    if (node.type === "CallExpression" && rawOf(panel.entry, node.callee).replace(/^\(|\)$/g, "") === "0,y.memo") memoCalls.push(node);
  }, panel.ast.program);
  // memo is only used by the table component `at` (module-level `var at=(0,y.memo)(function...)`)
  const allMemo = [];
  (function visit(node) {
    if (!node || typeof node !== "object") return;
    if (Array.isArray(node)) { node.forEach(visit); return; }
    if (node.type === "CallExpression" && rawOf(panel.entry, node.callee).replace(/^\(|\)$/g, "") === "0,y.memo") allMemo.push(node);
    for (const [k, v] of Object.entries(node)) if (k !== "loc" && v && typeof v === "object") visit(v);
  })(panel.ast.program);
  assert.equal(allMemo.length, 1, "exactly one y.memo call (table component at)");
  const atDecl = panel.ast.program.body.find((n) => n.type === "VariableDeclaration" && n.declarations.some((d) => d.id.name === "at"));
  assert.ok(atDecl && rawOf(panel.entry, atDecl).startsWith("var at=(0,y.memo)("), "memo is bound to `at`");

  // --- the module body (everything between the import header and export footer), exact text
  const body = panel.entry.source.slice(panel.firstBody.start, panel.lastBody.end);

  // --- real pure helpers from index-BVfnK1wp.js
  const pickFns = (names) => names.map((name) => {
    const node = index.functions[name];
    assert.ok(node, `index function ${name}`);
    return rawOf(index.entry, node);
  }).join("\n");
  const pure = new Function(`${pickFns(["Ie", "Fe", "Lr", "Ir", "Ci", "wi", "Ti"])}\nreturn { Ie, Fe, Lr, Ir, Ci, wi, Ti };`)();
  const toEsmStatement = index.variables.u.statement; // `var e=Object.create,...,u=(n,r,a)=>...,d=...;`
  assert.ok(rawOf(index.entry, toEsmStatement).startsWith("var e=Object.create,"), "toESM statement");
  const toESM = new Function(`${rawOf(index.entry, toEsmStatement)}\nreturn u;`)();
  const rewardFn = reward.source.replace(/export\{e as t\};?$/, "") + "\nreturn e;";
  assert.notEqual(rewardFn, reward.source, "rewardDisplay export footer rewritten");
  const rewardDisplay = new Function(rewardFn.replace(/^import\{[^}]*\}from"[^"]*";?/, ""))();
  const defaultAutoScan = new Function(`return (${rawOf(index.entry, index.variables.Si.declarator.init)});`)();

  // --- internal map: alias -> index internal name (via the export statement)
  const aliasInternals = {};
  for (const [alias, meta] of Object.entries(IMPORT_ALIASES)) {
    if (meta.from) continue;
    aliasInternals[alias] = index.exportedToInternal[meta.exported];
    assert.ok(aliasInternals[alias], `index export ${meta.exported}`);
  }
  // verify the panel's import statement still binds these exported names to these aliases
  const bound = {};
  for (const decl of panel.imports) for (const s of decl.specifiers) bound[s.local.name] = { from: decl.source.value, exported: s.imported.name };
  for (const [alias, meta] of Object.entries(IMPORT_ALIASES)) {
    assert.equal(bound[alias]?.exported, meta.exported, `import alias ${alias} -> ${meta.exported}`);
  }
  assert.equal(Object.keys(bound).length, Object.keys(IMPORT_ALIASES).length, "alias count");

  // --- Si / Ve defaults and en catalog are read lazily
  staticCache = { panel, index, states, refs, body, pure, toESM, rewardDisplay, defaultAutoScan, aliasInternals };
  return staticCache;
}

let enMessages;
// The English catalog en-BisSXcTB.js spreads `e.en` where e is the index export `s` (internal Fr): the
// error.* / auth.error.* / update.error.* tables. Fr and its data tables (jr, Mr, Nr, Pr) are extracted from
// the pinned index asset and executed.
function loadEnglish() {
  if (enMessages) return enMessages;
  const index = loadIndex();
  const entry = readAsset("en-BisSXcTB.js");
  const tables = ["jr", "Mr", "Nr", "Pr", "Fr"].map((n) => `var ${rawOf(index.entry, index.variables[n].declarator)};`).join("\n");
  assert.equal(index.exportedToInternal.s, "Fr", "index export s is Fr");
  const errorTables = new Function(`${tables}\nreturn Fr;`)();
  const code = `${entry.source.replace(/^import\{[^}]*\}from"[^"]*";/, "").replace(/export\{t as default\};?$/, "")}\nreturn t;`;
  enMessages = new Function("e", code)(errorTables);
  return enMessages;
}

const isPlainTab = (value) => typeof value === "string";
const cloneArgs = (args) => {
  try { return structuredClone(args); } catch { return args.map((a) => (typeof a === "function" ? "[function]" : a)); }
};

export const STUB_NAMES = Object.values(IMPORT_ALIASES).map((m) => m.stub).filter(Boolean);

// options (all optional):
//   label, tabMode: "uncontrolled" | "controlled", initialTab (controlled only), online (default false),
//   serverId (default 321), scanState (partial merged over Ve), summary (object | null | undefined=auto),
//   autoScanConfig, autoScanRunning, now, language, translate ("canonical" | "en" | (key, values) => string),
//   jobs {dispatchJobs, truckJobs}, storage {key: value}, props {extra prop overrides},
//   stubs { <stubName>: { mode: "auto"|"manual"|"reject", value, error } },
//   parent { applyOnState: true, applyOnCounts: true }  (what the App does with onState/onCounts)
export async function createOriginalHarness(options = {}) {
  const S = loadStatic();
  const label = options.label || "original";
  const panel = S.panel;
  const runtime = createHookRuntime();
  const { stateCells, refCells } = runtime;

  // --- hook indirection so child components can be rendered in their own throwaway runtime --------
  let active = runtime;
  let epoch = 0; // bumped by show(): every effect dependency list changes once, so every effect is re-created
  const hooksDelegate = {
    useState: (...a) => active.hooks.useState(...a),
    useRef: (...a) => active.hooks.useRef(...a),
    useCallback: (...a) => active.hooks.useCallback(...a),
    useMemo: (...a) => active.hooks.useMemo(...a),
    useEffect: (fn, deps) => active.hooks.useEffect(fn, deps && active === runtime ? [epoch, ...deps] : deps),
    useLayoutEffect: (fn, deps) => active.hooks.useEffect(fn, deps && active === runtime ? [epoch, ...deps] : deps),
    // memo returns the function itself; displayName only labels the single memo use (table component `at`).
    memo: (fn) => { fn.displayName = "at"; return fn; },
  };

  // --- fake clock / window / storage -----------------------------------------------------------
  let nextTimerId = 1;
  let clock = options.now ?? Date.UTC(2026, 9, 2, 12, 0, 0);
  const intervals = new Map();
  const timeouts = new Map();
  const storage = new Map(Object.entries(options.storage || {}));
  const storageWrites = [];
  class FakeDate extends Date {
    constructor(...args) { if (args.length === 0) super(clock); else super(...args); }
    static now() { return clock; }
  }
  const fakeWindow = {
    setInterval: (fn, delay) => { const id = nextTimerId++; intervals.set(id, { fn, delay, next: clock + delay }); return id; },
    clearInterval: (id) => { intervals.delete(id); },
    setTimeout: (fn, delay) => { const id = nextTimerId++; timeouts.set(id, { fn, delay, at: clock + delay }); return id; },
    clearTimeout: (id) => { timeouts.delete(id); },
  };
  const fakeLocalStorage = {
    getItem: (key) => (storage.has(key) ? storage.get(key) : null),
    setItem: (key, value) => { storage.set(key, String(value)); storageWrites.push([key, String(value)]); },
  };

  // --- i18n --------------------------------------------------------------------------------------
  const language = options.language || "en";
  const interpolate = (message, values = {}) => message.replace(/\{(\w+)\}/g, (_, name) => String(values[name] ?? `{${name}}`));
  let translate;
  if (typeof options.translate === "function") translate = options.translate;
  else if (options.translate === "en") {
    const messages = loadEnglish();
    // identical to the asset's I18nProvider t(): (messages[key]||key).replace(/\{(\w+)\}/g, ...)
    translate = (key, values = {}) => interpolate(messages[key] || key, values);
  } else {
    translate = (key, values = {}) => {
      if (key === "common.itemCount") return `${values.count} items`;
      if (key === "map.pageInfo") return `${values.page}/${values.total}`;
      if (key === "map.scheduleSelected" || key === "map.scheduleSelectedTrucks") return `${key}:${values.count}`;
      return key;
    };
  }
  const i18n = { language, t: translate };

  // --- JSX runtime (plain element objects) -------------------------------------------------------
  const FragmentType = "Fragment";
  const jsx = (type, props, key) => ({ type, key: key === undefined ? null : String(key), props: { ...(props || {}) } });
  const jsxRuntime = { jsx, jsxs: jsx, Fragment: FragmentType };

  // --- stubs ----------------------------------------------------------------------------------------
  const calls = [];
  const requests = [];
  const pending = [];
  const listeners = [];
  const logs = [];
  const propCalls = [];
  const behavior = { ...(options.stubs || {}) };
  let callSeq = 0;
  let jobs = structuredClone(options.jobs || { dispatchJobs: [], truckJobs: [] });
  let serverId = options.serverId ?? 321;

  const mkIcon = () => { function Icon() { return null; } return Icon; };
  const mkAsset = () => { function GameAssetImage() { return null; } return GameAssetImage; };

  let props; // declared early: default factories read the live props
  const defaults = {
    marchFollow: (arg) => ({ serverId: arg?.serverId, marchUuid: arg?.marchUuid }),
    treasureClaimStatus: () => ({ playerUid: "", allianceId: "", states: [], batch: null }),
    truckPlunderClear: () => undefined,
    localize: () => ({}),
    scanClear: (sid) => ({ ...props.scanState, serverId: sid }),
    plunderJobsList: () => structuredClone(jobs),
    coordinateJump: (arg) => ({ serverId: arg?.serverId, x: arg?.x, y: arg?.y }),
    dispatchPlunderCancel: () => undefined,
    scanStop: () => ({ ...props.scanState, isReading: false }),
    dispatchPlunderSchedule: () => undefined,
    truckPlunderSchedule: () => undefined,
    dispatchShareAlliance: (rows) => ({ shared: rows.length, failed: 0, sharedUuids: rows.map((r) => String(r.uuid || "")) }),
    cityExport: () => ({ canceled: true, rowCount: 0, path: "" }),
    playerMarkSet: () => undefined,
    treasureStateRefresh: () => ({ states: [] }),
    treasureStateRefreshAll: () => ({ playerUid: "", allianceId: "", states: [] }),
    truckPlunderCancel: () => undefined,
    scanStart: (arg) => ({ ...props.scanState, isReading: true, selectedTypes: arg?.selectedTypes, scanMode: arg?.scanMode }),
    dispatchPlunderClear: () => undefined,
    treasureClaim: () => ({ eligible: 0, queued: 0, skipped: 0 }),
    dataOptions: (sid) => ({
      serverId: sid, counts: {}, alliances: [], names: { resource: [], monster: [] }, dispatchLevels: [],
      rewardItems: { truck: [], railway: [] }, treasureTypes: [], noAllianceCount: 0, scanProgress: null,
    }),
  };
  function stub(name) {
    return (...args) => {
      const call = { id: ++callSeq, name, alias: aliasOf(name), args: cloneArgs(args) };
      calls.push(call);
      const spec = behavior[name] || { mode: "auto" };
      if (spec.mode === "reject") { call.settled = "rejected"; return Promise.reject(spec.error ?? new Error(`${name} rejected by harness`)); }
      if (spec.mode === "manual") {
        const d = deferred();
        call.pending = true;
        call.resolve = (value) => { call.pending = false; call.settled = "resolved"; d.resolve(value); };
        call.reject = (error) => { call.pending = false; call.settled = "rejected"; d.reject(error); };
        pending.push(call);
        return d.promise;
      }
      call.settled = "resolved";
      const value = spec.value !== undefined ? (typeof spec.value === "function" ? spec.value(...args) : spec.value) : defaults[name]?.(...args);
      return Promise.resolve(value);
    };
  }
  const aliasOfMap = Object.fromEntries(Object.entries(IMPORT_ALIASES).filter(([, m]) => m.stub).map(([alias, m]) => [m.stub, alias]));
  function aliasOf(name) { return aliasOfMap[name]; }

  // search stub: always a deferred promise recorded in `requests`
  const searchStub = (kind, query) => {
    const request = deferred();
    const entry = { id: requests.length + 1, kind, query: structuredClone(query), resolve: request.resolve, reject: request.reject };
    requests.push(entry);
    calls.push({ id: ++callSeq, name: "search", alias: "ce", args: [kind, structuredClone(query)], requestId: entry.id });
    return request.promise;
  };
  const listenStub = (name, handler) => {
    const record = { name, handler, active: true, id: listeners.length + 1 };
    listeners.push(record);
    calls.push({ id: ++callSeq, name: "listen", alias: "p", args: [name] });
    return () => { record.active = false; calls.push({ id: ++callSeq, name: "unlisten", alias: "p", args: [name] }); };
  };

  const bindings = {};
  for (const [alias, meta] of Object.entries(IMPORT_ALIASES)) {
    if (meta.stub === "search") bindings[alias] = searchStub;
    else if (meta.stub === "listen") bindings[alias] = listenStub;
    else if (meta.stub) bindings[alias] = stub(meta.stub);
  }
  bindings.r = S.pure.Ie; bindings.l = S.pure.Fe; bindings.m = S.pure.Lr; bindings.oe = S.pure.Ci;
  bindings.ue = S.pure.wi; bindings.le = S.pure.Ti; bindings._ = S.rewardDisplay;
  bindings.a = S.toESM; bindings.o = () => hooksDelegate; bindings.i = () => jsxRuntime;
  bindings.u = () => i18n;
  bindings.se = mkIcon(); bindings.v = mkAsset();
  assert.deepEqual(Object.keys(bindings).sort(), Object.keys(IMPORT_ALIASES).sort(), "every import alias bound");

  const names = [...Object.keys(IMPORT_ALIASES), "localStorage", "window", "Date"];
  const values = [...Object.keys(IMPORT_ALIASES).map((k) => bindings[k]), fakeLocalStorage, fakeWindow, FakeDate];
  const internalsNames = ["Ve", "Se", "ye", "be", "j", "M", "rt", "Ue", "A", "We", "Ge", "F", "P", "tt", "Ae", "nt", "Ie", "D", "ze", "Be", "Re", "O", "x", "we", "C", "Je", "I", "L", "N", "xe"];
  const factory = new Function(...names, `"use strict";\n${S.body}\nreturn { MapDataPanel: R, internals: { ${internalsNames.filter((n) => n !== "Ie").join(", ")} } };`);
  const loaded = factory(...values);
  const Component = loaded.MapDataPanel;
  assert.equal(typeof Component, "function");

  // --- default props (what the original App passes; see contract.md) -----------------------------------
  const defaultScanState = () => ({
    ...loaded.internals.Ve, selectedTypes: [...loaded.internals.Ve.selectedTypes],
    serverId, serverIdSource: serverId > 0 ? "fixture" : "none", ...(options.scanState || {}),
  });
  const makeSummary = (sid, scanState) => ({
    serverId: sid,
    counts: Object.fromEntries(["city", "resource", "monster", "truck", "railway", "dispatch", "ghost", "treasure"].map((k) => [k, 60])),
    scanState,
  });
  const controlled = options.tabMode === "controlled";
  let parentDirty = false;
  const initialScan = defaultScanState();
  props = {
    activeTab: controlled ? (options.initialTab || "city") : undefined,
    onActiveTabChange: controlled ? (next) => {
      propCalls.push({ name: "onActiveTabChange", args: [next] });
      props.activeTab = next; parentDirty = true;
    } : undefined,
    scanState: initialScan,
    summary: options.summary === undefined ? makeSummary(initialScan.serverId, initialScan) : options.summary,
    online: options.online ?? false,
    onState: (next) => {
      propCalls.push({ name: "onState", args: [structuredClone(next)] });
      if (options.parent?.applyOnState !== false) {
        props.scanState = next; parentDirty = true;
        if (props.summary && props.summary.serverId === next.serverId) props.summary = { ...props.summary, scanState: next };
      }
    },
    onCounts: (sid, counts) => {
      propCalls.push({ name: "onCounts", args: [sid, structuredClone(counts)] });
      if (options.parent?.applyOnCounts !== false) {
        if (props.summary?.serverId === sid) { props.summary = { ...props.summary, counts }; parentDirty = true; }
        else if (props.scanState?.serverId === sid) { props.summary = { serverId: sid, counts, scanState: props.scanState }; parentDirty = true; }
      }
    },
    onLog: (message) => { logs.push(message); propCalls.push({ name: "onLog", args: [message] }); },
    autoScanConfig: structuredClone(options.autoScanConfig || S.defaultAutoScan),
    autoScanRunning: options.autoScanRunning ?? false,
    onAutoScanConfig: (next) => { propCalls.push({ name: "onAutoScanConfig", args: [structuredClone(next)] }); props.autoScanConfig = next; parentDirty = true; },
    ...(options.props || {}),
  };
  serverId = props.scanState?.serverId ?? serverId;

  let tree = null;
  let renderCount = 0;
  let unmounted = false;
  let hidden = false;

  function render() {
    runtime.beginRender();
    renderCount += 1;
    active = runtime;
    tree = Component(props);
  }
  async function settle(maxRounds = 40) {
    if (unmounted) return;
    for (let round = 0; round < maxRounds; round += 1) {
      parentDirty = false;
      render();
      if (!hidden) runtime.commitEffects();
      await Promise.resolve();
      await Promise.resolve();
      await tick();
      if (!runtime.isDirty() && !parentDirty) return;
    }
    throw new Error(`${label}: hook runtime did not settle`);
  }
  async function unmount() { runtime.disposeEffects(); unmounted = true; }

  // React 19 <Activity mode="hidden"> (the original App wraps this page in it): effects are disconnected
  // while hidden, state is preserved, and every effect is re-created on reveal. MODELLED, not validated.
  async function hide() { hidden = true; runtime.disposeEffects(); await settle(); }
  async function show() { hidden = false; epoch += 1; await settle(); }

  async function advance(ms) {
    const target = clock + ms;
    for (;;) {
      const dueI = [...intervals.entries()].map(([id, t]) => ({ kind: "i", id, at: t.next, t }));
      const dueT = [...timeouts.entries()].map(([id, t]) => ({ kind: "t", id, at: t.at, t }));
      const due = [...dueI, ...dueT].filter((x) => x.at <= target).sort((a, b) => a.at - b.at || a.id - b.id)[0];
      if (!due) break;
      clock = Math.max(clock, due.at);
      if (due.kind === "i") { due.t.next += due.t.delay; due.t.fn(); } else { timeouts.delete(due.id); due.t.fn(); }
      await settle();
    }
    clock = target;
    await settle();
  }

  // --- introspection ------------------------------------------------------------------------------------
  const stateIndexBySemantic = Object.fromEntries(STATE_SEMANTICS.map(([sem], i) => [sem, i]));
  const stateIndexByOriginal = Object.fromEntries(STATE_SEMANTICS.map(([, orig], i) => [orig, i]));
  const refIndexBySemantic = Object.fromEntries(REF_SEMANTICS.map(([sem], i) => [sem, i]));
  const refIndexByOriginal = Object.fromEntries(REF_SEMANTICS.map(([, orig], i) => [orig, i]));
  function getState(name) {
    if (name === "tab") return props.activeTab ?? stateCells[stateIndexBySemantic.uncontrolledTab];
    const at = () => stateCells[stateIndexBySemantic.nameSelection];
    if (name === "resourceNameKey") return at().resource;
    if (name === "monsterNameKey") return at().monster;
    if (name === "selectedDispatchKeys") return Object.keys(stateCells[stateIndexBySemantic.dispatchSelection]);
    if (name === "selectedTruckKeys") return Object.keys(stateCells[stateIndexBySemantic.truckSelection]);
    const index = name in stateIndexBySemantic ? stateIndexBySemantic[name] : stateIndexByOriginal[name];
    assert.ok(index !== undefined, `${label}: state ${name}`);
    return stateCells[index];
  }
  const hasState = (name) => ["tab", "resourceNameKey", "monsterNameKey", "selectedDispatchKeys", "selectedTruckKeys"].includes(name) || name in stateIndexBySemantic || name in stateIndexByOriginal;
  function getRef(name) {
    const index = name in refIndexBySemantic ? refIndexBySemantic[name] : refIndexByOriginal[name];
    return index === undefined ? undefined : refCells[index];
  }
  const findNodes = (predicate) => treeNodes(tree).filter(predicate);
  const textOf = nodeText;

  // --- scenario helpers that drive the original's own handlers via the rendered tree -----------------
  const isTabButton = (node, key) => node.type === "button" && node.props?.onClick
    && Array.isArray(node.props.children) && node.props.children[0]?.props?.className === "map-tab-label"
    && nodeText(node.props.children[0]) === translate(TAB_LABEL_KEYS[key]);
  async function clickTab(kind) {
    render();
    const button = findNodes((node) => isTabButton(node, kind))[0];
    assert.ok(button, `${label}: tab ${kind}`);
    button.props.onClick();
    await settle();
  }
  function paginationNode() {
    render();
    const node = findNodes((n) => typeof n.type === "function" && n.type.name === "it")[0];
    return node;
  }
  async function setPage(next) {
    const node = paginationNode();
    assert.ok(node, `${label}: Pagination element`);
    node.props.onPage(next);
    await settle();
  }
  function tableNode() {
    render();
    return findNodes((n) => typeof n.type === "function" && n.type.displayName === "at")[0];
  }
  async function emitServer(nextServerId, opts = {}) {
    serverId = nextServerId;
    const next = { ...props.scanState, serverId: nextServerId, serverIdSource: nextServerId > 0 ? "fixture" : "none" };
    props.scanState = next;
    if (opts.summary !== false && props.summary !== null) props.summary = makeSummary(nextServerId, next);
    await settle();
  }
  async function emitScanState(next) {
    props.scanState = { ...props.scanState, ...next };
    if (props.summary && props.summary.serverId === props.scanState.serverId) props.summary = { ...props.summary, scanState: props.scanState };
    serverId = props.scanState.serverId;
    await settle();
  }
  async function setProps(next) { Object.assign(props, next); await settle(); }
  async function resolveRequest(request, result) { request.resolve(result); await settle(); }
  async function rejectRequest(request, error) { request.reject(error); await settle(); }
  async function resolveCall(call, value) { call.resolve(value); await settle(); }
  async function rejectCall(call, error) { call.reject(error); await settle(); }
  function setJobs(next) { jobs = structuredClone({ dispatchJobs: next.dispatchJobs ?? [], truckJobs: next.truckJobs ?? [] }); }
  async function emitEvent(name, payload) {
    for (const record of listeners.filter((r) => r.active && r.name === name)) record.handler(payload);
    await settle();
  }
  function setStubBehavior(name, spec) { if (spec) behavior[name] = spec; else delete behavior[name]; }

  // Render a function-component element (e.g. the table `at`, scheduled groups `ot`/`st`) in a throwaway
  // hook runtime so its own predicates (checkbox checked/disabled, buttons) can be inspected and invoked.
  function expand(node) {
    assert.equal(typeof node?.type, "function", "expand: function component element expected");
    const scratch = createHookRuntime();
    const previous = active;
    active = scratch;
    try { scratch.beginRender(); return node.type(node.props); } finally { active = previous; }
  }
  function expandedTable() { render(); const node = tableNode(); return node ? expand(node) : null; }
  function tableCheckboxes() {
    const out = expandedTable();
    if (!out) return [];
    return treeNodes(out).filter((n) => n.type === "input" && n.props?.type === "checkbox").map((n, row) => ({
      row, ariaLabel: n.props["aria-label"], checked: n.props.checked, disabled: n.props.disabled, toggle: () => n.props.onChange(),
    }));
  }
  async function toggleRow(rowIndex) {
    const box = tableCheckboxes()[rowIndex];
    assert.ok(box, `${label}: checkbox ${rowIndex}`);
    box.toggle();
    await settle();
    return box;
  }
  function scheduledGroups() {
    render();
    return findNodes((n) => typeof n.type === "function" && (n.type.name === "ot" || n.type.name === "st")).map((n) => ({
      component: n.type.name, kind: n.props.kind || (n.type.name === "st" ? "truck" : "dispatch"), props: n.props, element: n,
    }));
  }
  const findButton = (key, values) => {
    render();
    const text = translate(key, values);
    return findNodes((n) => n.type === "button" && textOf(n) === text)[0];
  };
  const findInput = (labelKey) => {
    render();
    const text = translate(labelKey);
    return findNodes((n) => n.type === "input" && n.props?.["aria-label"] === text)[0];
  };
  const findSelect = (labelKey) => {
    render();
    const text = translate(labelKey);
    return findNodes((n) => n.type === "select" && n.props?.["aria-label"] === text)[0];
  };
  async function typeKeyword(value) {
    const input = findInput("map.searchLabel");
    assert.ok(input, `${label}: keyword input`);
    input.props.onChange({ target: { value } });
    await settle();
  }
  async function clickSearch() {
    const button = findButton("common.search");
    assert.ok(button, `${label}: search button`);
    button.props.onClick();
    await settle();
    return button;
  }
  async function selectName(value) {
    const select = findSelect("common.name");
    assert.ok(select, `${label}: name select`);
    select.props.onChange({ target: { value } });
    await settle();
  }
  async function setRandomDelay(value) {
    const input = findInput("map.randomDelaySeconds");
    assert.ok(input, `${label}: random delay input`);
    input.props.onChange({ target: { value } });
    await settle();
  }

  return {
    label, requests, calls, pending, listeners, logs, propCalls, props, storage, storageWrites,
    sourceSha256: ORIGINAL_SHA256, component: Component, moduleBody: S.body,
    mount: settle, settle, unmount, advance, setProps, hide, show,
    tree: () => { render(); return tree; },
    findNodes: (predicate) => { render(); return findNodes(predicate); },
    getState, hasState, getRef, clickTab, setPage, emitServer, emitScanState, emitEvent, setJobs,
    resolveRequest, rejectRequest, resolveCall, rejectCall, setStubBehavior,
    requestCount: () => requests.length,
    currentRequest: () => requests.at(-1),
    callsNamed: (name) => calls.filter((c) => c.name === name),
    expand, expandedTable, tableNode, tableCheckboxes, toggleRow, scheduledGroups,
    findButton, findInput, findSelect, typeKeyword, clickSearch, selectName, setRandomDelay,
    renderCount: () => renderCount,
    now: () => clock, setNow: (value) => { clock = value; },
    timers: () => ({ intervals: [...intervals.values()].map((t) => t.delay), timeouts: [...timeouts.values()].map((t) => t.delay) }),
    stateNames: STATE_SEMANTICS.map(([sem]) => sem), refNames: REF_SEMANTICS.map(([sem]) => sem),
    importSources: panel.imports.map((d) => d.source.value),
    internals: loaded.internals, translate,
    liveListener: (name) => listeners.filter((r) => r.active && r.name === name).length,
    isControlled: controlled,
  };
}
