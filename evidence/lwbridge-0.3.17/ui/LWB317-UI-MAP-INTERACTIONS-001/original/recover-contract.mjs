// AST-based, hash-asserted extraction of the exact expressions that define the interaction contract of the
// ORIGINAL LWBridge 0.3.17 Map panel (function R in MapDataPanel-B4GXEND2.js).
//
//   node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/original/recover-contract.mjs
//
// Writes original/contract.json and original/contract.md. Every item carries the exact source expression, its
// UTF-8 byte offsets inside the ORIGINAL asset, and the line in reference/MapDataPanel.pretty.js (layout-only
// re-print; the pretty file is regenerated in memory and asserted identical, never rewritten here).
// Identifiers are the ORIGINAL minified names; the re-print appends "2" to names that collide with module-level
// identifiers (original `L` is `L2` there).
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import {
  originalDir, loadPanel, loadIndex, loadPretty, readAsset, rawOf, byteAt, walkAll, traverse, parse, ASSET_HASHES,
} from "./lib.mjs";
import { STATE_SEMANTICS, REF_SEMANTICS, IMPORT_ALIASES } from "./semantics.mjs";

const P = loadPanel();
const E = P.entry;
const R = P.R;
const IDX = loadIndex();
const PR = loadPretty();
const raw = (node, entry = E) => rawOf(entry, node);
const calleeRaw = (node) => raw(node.callee).replace(/^\(|\)$/g, "");

// ---- scope-aware reference tables for R (Babel scope analysis) --------------------------------------------
let rPath;
traverse(P.ast, { FunctionDeclaration(p) { if (p.node === R) { rPath = p; p.stop(); } } });
assert.ok(rPath, "R path");
const rScope = rPath.scope;
const binding = (name) => {
  const b = rScope.getOwnBinding(name);
  assert.ok(b, `R-level binding ${name}`);
  return b;
};
const stateByValue = Object.fromEntries(STATE_SEMANTICS.map(([sem, value, setter]) => [value, { sem, setter }]));
const stateBySetter = Object.fromEntries(STATE_SEMANTICS.map(([sem, value, setter]) => [setter, { sem, value }]));
const refSem = Object.fromEntries(REF_SEMANTICS.map(([sem, value]) => [value, sem]));

const allNodes = [];
walkAll(R, (node, parents) => allNodes.push({ node, parents }));
const findAll = (pred) => allNodes.filter(({ node, parents }) => pred(node, parents)).map((x) => x.node);
const findOne = (label, pred) => {
  const found = findAll(pred);
  assert.equal(found.length, 1, `${label}: expected exactly 1 match, got ${found.length}`);
  return found[0];
};
const isUseEffect = (n) => n.type === "CallExpression" && calleeRaw(n) === "0,y.useEffect";
const effect = (label, marker) => findOne(`effect ${label}`, (n) => isUseEffect(n) && raw(n).includes(marker));
const fnDecl = (name) => findOne(`function ${name}`, (n) => n.type === "FunctionDeclaration" && n.id?.name === name);
const stateDecl = (value) => findOne(`state ${value}`, (n) => n.type === "VariableDeclarator" && n.id.type === "ArrayPattern" && n.id.elements[0]?.name === value);
const declarator = (name) => findOne(`declarator ${name}`, (n) => n.type === "VariableDeclarator" && n.id.type === "Identifier" && n.id.name === name);
const isJsx = (n) => n.type === "CallExpression" && /^0,E\.jsxs?$/.test(calleeRaw(n));
const jsxCall = (label, tag, pred = () => true) => findOne(`jsx ${label}`, (n) => isJsx(n) && raw(n.arguments[0]) === `\`${tag}\`` && pred(n));
const jsxCallAll = (tag, pred = () => true) => findAll((n) => isJsx(n) && raw(n.arguments[0]) === `\`${tag}\`` && pred(n));
const prop = (objectNode, key) => objectNode.properties.find((p) => p.type === "ObjectProperty" && (p.key.name === key || p.key.value === key));
const propsOf = (call) => call.arguments[1];

// ---- locators ------------------------------------------------------------------------------------------------
function lastDescendantStart(node) {
  let max = node.start;
  walkAll(node, (n) => { if (n.start > max) max = n.start; });
  return max;
}
function locate(node, entry = E) {
  const loc = { asset: entry.relative, sha256: entry.sha256, byteStart: byteAt(entry, node.start), byteEnd: byteAt(entry, node.end) };
  if (entry === E) {
    loc.prettyLineStart = PR.lineOf(node.start);
    loc.prettyLineEnd = PR.lineOf(lastDescendantStart(node));
    assert.ok(loc.prettyLineStart && loc.prettyLineEnd >= loc.prettyLineStart, "pretty lines");
    // anchor check: the first ASCII string/template literal of the expression must be inside the mapped pretty window
    const text = raw(node);
    const literal = text.match(/`([A-Za-z0-9_.:\-/ ]{4,})`/);
    if (literal) {
      const window = PR.lines.slice(loc.prettyLineStart - 1, loc.prettyLineEnd + 1).join("\n");
      assert.ok(window.includes(literal[0]), `pretty-line anchor ${literal[0]} not found near line ${loc.prettyLineStart}`);
    }
  }
  return loc;
}

// ---- reference / context helpers -----------------------------------------------------------------------------
function describeAncestors(path) {
  const out = [];
  for (let p = path.parentPath; p; p = p.parentPath) {
    const n = p.node;
    if (n === R) { out.push("R"); break; }
    if (n.type === "FunctionDeclaration") out.push(`function ${n.id.name}`);
    else if (n.type === "ArrowFunctionExpression" || n.type === "FunctionExpression") {
      const parent = p.parentPath?.node;
      if (parent?.type === "CallExpression" && isUseEffect(parent) && parent.arguments[0] === n) out.push(`useEffect@${byteAt(E, parent.start)}`);
      else if (parent?.type === "CallExpression" && /^0,y\.use(Callback|Memo)$/.test(calleeRaw(parent))) {
        const decl = p.parentPath.parentPath?.node;
        out.push(`${calleeRaw(parent).slice(4)}:${decl?.id?.name || "anonymous"}`);
      } else if (parent?.type === "ObjectProperty") out.push(`prop:${parent.key.name || parent.key.value}`);
      else if (parent?.type === "CallExpression") out.push(`callback-arg-of:${raw(parent.callee).slice(0, 24)}`);
      else out.push("arrow");
    }
  }
  return out.reverse();
}
function statementContext(path) {
  // smallest enclosing expression statement / call expression for a readable excerpt
  let p = path;
  while (p.parentPath && !/Statement$/.test(p.node.type) && p.parentPath.node !== R) p = p.parentPath;
  return raw(p.node).slice(0, 240);
}
function refsOf(name) {
  const b = binding(name);
  return b.referencePaths.map((p) => {
    let propKey = null;
    let jsxHint = null;
    for (let q = p.parentPath; q && q.node !== R; q = q.parentPath) {
      if (!propKey && q.node.type === "ObjectProperty" && !/Function/.test(q.parentPath.parentPath?.node?.type || "")) propKey = q.node.key.name || q.node.key.value;
      if (q.node.type === "CallExpression" && isJsx(q.node)) {
        const label = raw(q.node).match(/S\(`([\w.]+)`/);
        jsxHint = `${raw(q.node.arguments[0]).replace(/`/g, "")}${label ? `:${label[1]}` : ""}`;
        break;
      }
    }
    return {
      byteStart: byteAt(E, p.node.start),
      where: describeAncestors(p).join(" > "),
      jsx: jsxHint,
      prop: propKey,
      excerpt: raw(p.parentPath.node).slice(0, 200),
    };
  });
}
// Which R-level bindings does the node (range) reference? -> {name, kind}
function referencedBindings(node) {
  const out = new Map();
  for (const [name, b] of Object.entries(rScope.bindings)) {
    for (const p of b.referencePaths) {
      if (p.node.start >= node.start && p.node.end <= node.end) out.set(name, (out.get(name) || 0) + 1);
    }
  }
  return out;
}
function bindingKind(name) {
  if (stateByValue[name]) return `state:${stateByValue[name].sem}`;
  if (stateBySetter[name]) return `setter:${stateBySetter[name].sem}`;
  if (refSem[name]) return `ref:${refSem[name]}`;
  const b = binding(name);
  if (b.kind === "param") return `prop`;
  if (b.path.isFunctionDeclaration()) return "function";
  return "local";
}
function setterCalls(node) {
  const out = [];
  for (const [setter, info] of Object.entries(stateBySetter)) {
    const b = binding(setter);
    for (const p of b.referencePaths) {
      if (p.node.start >= node.start && p.node.end <= node.end && p.parentPath.isCallExpression({ callee: p.node })) {
        out.push({ state: info.sem, setter, call: raw(p.parentPath.node).slice(0, 160), byteStart: byteAt(E, p.node.start) });
      }
    }
  }
  return out.sort((a, b) => a.byteStart - b.byteStart);
}

// ---- items --------------------------------------------------------------------------------------------------------
const items = [];
function item(id, topic, title, node, statement, opts = {}) {
  const entry = opts.entry || E;
  assert.ok(!items.some((i) => i.id === id), `duplicate id ${id}`);
  const rec = {
    id, topic, title, evidenceState: opts.state || "EXACT_BYTES",
    locator: locate(node, entry), expression: raw(node, entry),
  };
  if (opts.analysis) rec.analysis = opts.analysis;
  rec.statement = typeof statement === "function" ? statement(rec) : statement;
  if (opts.confirmedBy) rec.confirmedBy = opts.confirmedBy;
  items.push(rec);
  return rec;
}
const depsOf = (effectNode) => {
  const arr = effectNode.arguments[1];
  assert.equal(arr.type, "ArrayExpression");
  return arr.elements.map((e) => raw(e));
};

// =====================================================================================================================
// TOPIC 1 -- keyword input and search issuing
// =====================================================================================================================
const keywordState = stateDecl("et");
const keywordInput = jsxCall("keyword input", "input", (n) => raw(n).includes("\"aria-label\":S(`map.searchLabel`)") && raw(n).includes("value:et"));
const keywordOnChange = prop(propsOf(keywordInput), "onChange").value;
const searchEffect = effect("search", "rr(B)}");
const searchDepsRaw = depsOf(searchEffect);
const rrFn = fnDecl("rr");
const nrFn = fnDecl("nr");
const irFn = fnDecl("ir");
const rrCalls = findAll((n) => n.type === "CallExpression" && n.callee.type === "Identifier" && n.callee.name === "rr");
const ceCalls = findAll((n) => n.type === "CallExpression" && n.callee.type === "Identifier" && n.callee.name === "ce");
assert.equal(ceCalls.length, 1, "the search import ce is called exactly once");
assert.ok(ceCalls[0].start > rrFn.start && ceCalls[0].end < rrFn.end, "ce is called inside rr");
const timerSites = [];
walkAll(P.ast.program, (n, parents) => {
  if (n.type === "CallExpression" && n.callee.type === "MemberExpression" && raw(n.callee.object) === "window" && /^(setTimeout|setInterval|clearTimeout|clearInterval)$/.test(n.callee.property.name)) {
    const fnParents = parents.filter((p) => p.type === "FunctionDeclaration").map((p) => p.id.name);
    timerSites.push({ api: n.callee.property.name, byteStart: byteAt(E, n.start), insideFunction: fnParents.join(" > ") || "(module)", excerpt: raw(n).slice(0, 120) });
  }
});
const bareTimerIdentifiers = [];
walkAll(P.ast.program, (n, parents) => {
  const parent = parents.at(-1);
  if (parent?.type === "MemberExpression" && parent.property === n && !parent.computed) return; // window.setTimeout is accounted for in timerSites
  if (n.type === "Identifier" && /^(setTimeout|setInterval|clearTimeout|clearInterval|requestAnimationFrame|queueMicrotask|debounce|throttle)$/.test(n.name)) bareTimerIdentifiers.push(n.name);
});
const searchDeps = (() => {
  const keys = new Set(searchDepsRaw);
  const readIn = new Set();
  for (const range of [searchEffect.arguments[0], rrFn, nrFn]) for (const name of referencedBindings(range).keys()) readIn.add(name);
  const readKinds = [...readIn].filter((n) => /^(state|prop|local)/.test(bindingKind(n))).sort();
  const notInDeps = readKinds.filter((n) => !keys.has(n));
  const presentWithKinds = searchDepsRaw.map((d) => ({ dep: d, kind: bindingKind(d.split(".")[0]) }));
  return { depsRaw: searchDepsRaw, presentWithKinds, readByEffectRrNr: readKinds, readButNotInDeps: notInDeps.map((n) => ({ name: n, kind: bindingKind(n) })) };
})();
assert.ok(!searchDepsRaw.includes("et"), "keyword (et) is not a dependency of the search effect");

item("kw-state", 1, "Keyword state", keywordState,
  "Keyword is a single string state `et` (setter `nt`), initial empty string, shared by every tab (not stored per tab), never trimmed or normalised, not persisted.",
  { confirmedBy: ["kw-typing-no-request", "kw-shared-across-tabs"] });
item("kw-input", 1, "Keyword input element", keywordInput,
  (rec) => `Controlled text input (value=et). Its props are exactly [${rec.analysis.propNames.join(", ")}]: no onKeyDown/Enter handler, no disabled attribute, aria-label and placeholder are both map.searchLabel.`,
  { analysis: { propNames: propsOf(keywordInput).properties.map((p) => p.key.name || p.key.value) }, confirmedBy: ["kw-typing-no-request"] });
assert.deepEqual(propsOf(keywordInput).properties.map((p) => p.key.name || p.key.value), ["value", "onChange", "aria-label", "placeholder"]);
item("kw-onchange", 1, "Keyword onChange", keywordOnChange,
  "onChange stores e.target.value verbatim (nt). Additionally, ONLY when the active tab is resource or monster (Ge(L)) and a name is currently selected for that tab (At[L] truthy), it clears that tab's name selection (jt sets At[L]=undefined). It does not touch the page, does not issue a request itself and has no debounce.",
  { confirmedBy: ["kw-typing-no-request", "kw-clears-name-selection", "kw-first-keystroke-with-name-requests"] });
item("kw-search-effect", 1, "Search effect", searchEffect,
  "Effect that issues the (non-Scheduled) search: skipped on the Scheduled tab and on the treasure tab while online with lucky-first on and no viewer uid known yet (the viewer identity is being fetched; the Search button and rr do not wait); with no data server (R falsy) it bumps the request generation and clears page/rows/total instead of requesting; otherwise calls rr(B) with the page of the current render.",
  { confirmedBy: ["kw-tab-change-uses-typed-keyword", "kw-filter-change-uses-typed-keyword"] });
item("kw-search-deps", 1, "Search effect dependency array (complete)", searchEffect.arguments[1],
  (rec) => `Complete dependency array: ${rec.expression}. Present: tab L, alliance filter Ot, data server R, page B, quality It, item filter Rt, treasure type Bt, completion status Ht, plunderable Wt, dispatch level Kt, name selection At, sorts Jt, marked-only Mt, player-mark revision Pt, rows revision gn, include-foreign-radar Nn, online l, lucky-first J, treasure viewer In. ABSENT although read by the effect/rr/nr closure: ${rec.analysis.readButNotInDeps.map((d) => `${d.name} (${d.kind})`).join(", ")}. In particular the keyword et is NOT a dependency.`,
  { analysis: searchDeps, confirmedBy: ["kw-typing-no-request", "kw-filter-change-uses-typed-keyword", "kw-options-refresh-refires-search"] });
item("kw-rr", 1, "Search request function rr", rrFn,
  "rr is the only function that calls the search import ce. It bumps the generation T, sets loading, awaits ce(L, nr(page)), applies the reply only if its generation is still current, and clamps the page when it exceeds ceil(total/50) (sets page to the last page and returns WITHOUT storing rows).",
  { confirmedBy: ["rr-stale-reply-dropped", "rr-page-clamp"] });
item("kw-rr-callsites", 1, "All call sites of rr", rrCalls[0],
  (rec) => `rr( ) is called from exactly ${rec.analysis.sites.length} sites: ${rec.analysis.sites.map((s) => s.where || "R").join(" | ")}. The search import ce itself is called exactly once, inside rr (byte ${byteAt(E, ceCalls[0].start)}). Search-issuing paths are therefore: the search effect, the Search button (page 1 only), and page changes/filter changes through the effect dependencies.`,
  { analysis: { sites: rrCalls.map((n) => ({ byteStart: byteAt(E, n.start), excerpt: raw(n), where: (() => { let found = ""; traverse(P.ast, { CallExpression(p) { if (p.node === n) { found = describeAncestors(p).join(" > "); p.stop(); } } }); return found; })() })), ceCallByte: byteAt(E, ceCalls[0].start) }, confirmedBy: ["search-button-page1-vs-page-gt-1"] });
item("kw-timers", 1, "Timer inventory (no debounce/throttle)", findOne("scan-progress setTimeout", (n) => n.type === "CallExpression" && raw(n).startsWith("window.setTimeout(()=>{X.current=null")),
  (rec) => `The asset contains exactly ${rec.analysis.timerSites.length} window timer call sites (${rec.analysis.timerSites.map((s) => `${s.api}@${s.byteStart} in ${s.insideFunction}`).join("; ")}); none lies in the keyword/search path (nt, rr, nr, Search button). Identifiers setTimeout/setInterval/debounce/throttle/requestAnimationFrame/queueMicrotask never occur bare. There is NO debounce, throttle or delay for search.`,
  { analysis: { timerSites, bareTimerIdentifiers }, confirmedBy: ["kw-typing-no-request"] });
assert.equal(bareTimerIdentifiers.length, 0);
item("kw-nr-keyword", 1, "Keyword in the query projection", prop(findAll((n) => n.type === "ReturnStatement" && n.argument?.type === "ObjectExpression" && nrFn.start < n.start && n.end < nrFn.end)[0].argument, "keyword"),
  "nr reads the keyword state et directly from the render closure and passes it as-is (no trim, no lower-casing). Whenever the search effect re-runs because some OTHER dependency changed, the keyword sent is whatever string is currently in the input (typed but never submitted text included).",
  { confirmedBy: ["kw-filter-change-uses-typed-keyword", "kw-tab-change-uses-typed-keyword"] });
{
  const exportFn = fnDecl("fr");
  const call = findOne("nr(1,200) in fr", (n) => n.type === "CallExpression" && n.callee.type === "Identifier" && n.callee.name === "nr" && n.start > exportFn.start && n.end < exportFn.end);
  item("kw-export-uses-nr", 1, "City export reuses nr (not a search request)", call,
    "fr (Export Excel, city tab only) builds its query with nr(1,200): same keyword/filters, page 1, pageSize 200. It does not call ce.", { confirmedBy: ["export-city-query"] });
}

// =====================================================================================================================
// TOPIC 2 -- Search button
// =====================================================================================================================
const searchButton = jsxCall("search button", "button", (n) => raw(n).includes("children:S(`common.search`)"));
const searchButtonProps = propsOf(searchButton).properties.map((p) => p.key.name || p.key.value);
assert.deepEqual(searchButtonProps, ["onClick", "children"]);
const searchOnClick = prop(propsOf(searchButton), "onClick").value;
item("search-button", 2, "Search button element", searchButton,
  "Plain <button> with exactly the props [onClick, children]. It has NO disabled attribute and no type attribute: it is always enabled (also while loading, while a scan runs, offline and with an empty keyword). Label is common.search (no busy-state label).",
  { analysis: { propNames: searchButtonProps }, confirmedBy: ["search-button-no-disabled", "search-while-loading"] });
item("search-onclick", 2, "Search button onClick", searchOnClick,
  "If the current page B is 1 it calls rr(1) directly (a request with the current keyword/filters; no state changes, so the effect does not also fire). If B is not 1 it only calls V(1): the page returns to 1 and the dependency change of B makes the search effect issue the request. Keyword is not reset; selections are not reset.",
  { confirmedBy: ["search-button-page1-vs-page-gt-1"] });

// =====================================================================================================================
// TOPIC 3 -- resource / monster name select
// =====================================================================================================================
const nameSelect = jsxCall("name select", "select", (n) => raw(n).includes("\"aria-label\":S(`common.name`)"));
const nameSelectOnChange = prop(propsOf(nameSelect), "onChange").value;
const progNodes = [];
walkAll(P.ast.program, (node, parents) => progNodes.push({ node, parents }));
const findProg = (label, pred) => {
  const found = progNodes.filter(({ node, parents }) => pred(node, parents)).map((x) => x.node);
  assert.equal(found.length, 1, `${label}: expected exactly 1 match in program, got ${found.length}`);
  return found[0];
};
const topFn = (name) => { const n = P.topLevel[name]; assert.ok(n, `module function ${name}`); return n; };
const programScope = rScope.parent;
const aliasStub = Object.fromEntries(Object.entries(IMPORT_ALIASES).map(([alias, m]) => [alias, m.stub || m.pure || m.react || m.component]));
// import-alias calls inside a node: only references that resolve to the MODULE-level alias (not shadowed in R)
function importCalls(node) {
  const out = [];
  for (const alias of Object.keys(IMPORT_ALIASES)) {
    const b = programScope.getOwnBinding(alias);
    if (!b) continue;
    for (const p of b.referencePaths) {
      if (p.node.start >= node.start && p.node.end <= node.end) {
        out.push({ alias, import: aliasStub[alias], byteStart: byteAt(E, p.node.start), call: raw(p.parentPath.node).slice(0, 140) });
      }
    }
  }
  return out.sort((a, b) => a.byteStart - b.byteStart);
}
const predicateRefs = (node) => [...referencedBindings(node).keys()].sort().map((n) => `${n}=${bindingKind(n)}`);
const stateWriters = {};
for (const [sem, , setter] of STATE_SEMANTICS) {
  const b = binding(setter);
  stateWriters[sem] = b.referencePaths.map((p) => ({
    where: describeAncestors(p).join(" > "),
    call: raw(p.parentPath.node).slice(0, 120),
    byteStart: byteAt(E, p.node.start),
  })).sort((a, c) => a.byteStart - c.byteStart);
}
const effectNames = new Map();
const nameEffect = (name, node) => effectNames.set(`useEffect@${byteAt(E, node.start)}`, name);
const effects = {
  settingsScanMode: effect("scanMode persist", "localStorage.setItem(Me,F)"),
  settingsForeign: effect("foreign persist", "localStorage.setItem(Ne,String(Nn))"),
  settingsLucky: effect("lucky persist", "localStorage.setItem(Pe,String(J))"),
  treasureContext: effect("treasure context", "treasure context error"),
  callbackRefs: effect("callback refs", "De.current=_,ke.current=v,w.current=b"),
  countsFromSummary: effect("counts from summary", "vt({}),bt(!1)"),
  scanTypesFromState: effect("scan types", "P.current||qe(rt(C.selectedTypes))"),
  serverChange: effect("server change", "D.current.clear(),C.serverId<=0"),
  playerMarkListener: effect("player mark listener", "player-mark-changed"),
  jobsMountAndEvents: effect("jobs mount", "dispatch-plunder-changed"),
  clockTicker: effect("clock ticker", "window.setInterval(()=>an(Date.now()),1e3)"),
  tabChange: effect("tab change", "on({}),L==="),
  scanComplete: effect("scan complete", "_e(`scan-complete`)"),
  scanProgress: effect("scan progress", "_e(`scan-progress`)"),
  scanTimerCleanup: effect("scan timer cleanup", "()=>()=>{X.current"),
  search: searchEffect,
  options: effect("options", "ge(R).then"),
  localize: effect("localize", "s(Te,[...e])"),
};
assert.equal(findAll(isUseEffect).length, Object.keys(effects).length, "every useEffect of R is classified");
for (const [name, node] of Object.entries(effects)) nameEffect(name, node);
const labelWhere = (where) => where.replace(/useEffect@\d+/g, (m) => `effect:${effectNames.get(m) || m}`);
for (const list of Object.values(stateWriters)) for (const w of list) w.where = labelWhere(w.where);

// =====================================================================================================================
// TOPIC 3 (continued)
// =====================================================================================================================
const nameOptionMap = findOne("name options map", (n) => n.type === "CallExpression" && raw(n).startsWith("pt[L].map("));
const jFn = topFn("j");
const GeFn = topFn("Ge");
const optionsEffect = effects.options;
const nameValidation = findOne("name validation jt", (n) => n.type === "CallExpression" && n.callee.type === "Identifier" && n.callee.name === "jt" && raw(n).includes("t.names.resource.some"));
item("name-state", 3, "Name selection state", stateDecl("At"),
  (rec) => `Resource/Monster name selection is one object state At = {resource?, monster?} (initial {}); undefined means "All names". It is keyed by tab, so resource and monster keep independent selections, and neither is reset by tab change, page change or server change. Writers: ${[...new Set(rec.analysis.writers.map((w) => w.where))].join("; ")}.`,
  { analysis: { writers: stateWriters.nameSelection }, confirmedBy: ["name-selection-persists-across-tabs"] });
item("name-options-state", 3, "Name options (data) state", stateDecl("pt"),
  "pt = {resource:[{key,count}], monster:[{key,count}]}, initial empty lists; replaced only by the map_data_options reply (names) or by clear-data. Option keys/counts come from the backend, never from the visible rows.",
  { analysis: { writers: stateWriters.nameOptions } });
item("name-select", 3, "Name select element", nameSelect,
  "Rendered only when Ge(L) (resource or monster tab). aria-label common.name; value = At[L] || ''. First option has value '' and label map.allNames; then one <option> per entry of pt[L]. It is not disabled in any state.",
  { analysis: { propNames: propsOf(nameSelect).properties.map((p) => p.key.name || p.key.value), predicate: raw(GeFn) }, confirmedBy: ["name-select-render"] });
item("name-option-text", 3, "Option rendering / text derivation", nameOptionMap,
  "Each option: value = entry.key, key = entry.key, children = [ j(Qt, key, key), ' (', count, ')' ]. j(gameTexts, key, fallback) returns gameTexts[key.trim()] when that is non-empty and different from the trimmed key, otherwise the fallback = the RAW key (no 'unknown' text). So text is 'translatedName (count)' or 'rawKey (count)'; translations come from Qt (lastwar_localize), keyed by the trimmed key.",
  { confirmedBy: ["name-select-render", "name-option-text-derivation"] });
item("name-j", 3, "Text-derivation helper j", jFn,
  "j(e,t,n): r=String(t||'').trim(); empty key -> fallback n; otherwise e[r] if truthy and !== r, else n.", { confirmedBy: ["name-option-text-derivation"] });
item("name-onchange", 3, "Name select onChange", nameSelectOnChange,
  "Sets At[L] to the chosen value (undefined when '' = All), clears the keyword (nt('')) and sets page 1 (V(1)). It does not call rr: because At changes (and B too when page != 1) the search effect fires once with the empty keyword and the name key. Selecting 'All' also clears the keyword.",
  { confirmedBy: ["name-select-request", "name-select-resets-page-and-keyword"] });
item("name-refresh-validation", 3, "Validation of a selected name when options refresh", nameValidation,
  "On every successful map_data_options reply At is replaced by a NEW object: each tab's selection is kept only if its key is still present (exact ===) in the new option list, else undefined. Because the object is always new (even when nothing changed) At's identity changes and the search effect, which depends on At, issues ANOTHER search request after every options reply (mount, scan completion, clear-data).",
  { confirmedBy: ["kw-options-refresh-refires-search", "name-invalid-after-refresh-cleared"] });
item("name-options-effect", 3, "Options effect (map_data_options)", optionsEffect,
  "Runs when the data server R or the options revision vn changes (mount, server change, scan completion, clear-data); skipped when R<=0. Guarded by generation O: stale replies are ignored; if the reply's serverId != R the data server is switched to the reply's serverId and nothing else is applied. Otherwise it sets alliances, name options, dispatch levels, counts (and tells the parent through onCounts), reward items, treasure types, no-alliance count, scan progress, validates the alliance filter, name selection, dispatch level and treasure type against the new lists.",
  { analysis: { deps: depsOf(optionsEffect), importCalls: importCalls(optionsEffect) }, confirmedBy: ["kw-options-refresh-refires-search"] });
item("name-localize-effect", 3, "Game text (translation) effect", effects.localize,
  "Clears gameTexts ($t({})) on every run and, when the tab has keys to translate (resource: offered name keys + row resourceNameKey + status keys 300039/372138; monster: offered name keys + row monsterNameKey; other tabs: reward name keys etc.), requests lastwar_localize; the reply is applied only if still current (generation M). Runs when [L, language, name options, scheduled dispatch jobs, rows, treasure types, scheduled truck jobs] change.",
  { analysis: { deps: depsOf(effects.localize) }, confirmedBy: ["name-option-text-derivation"] });

// =====================================================================================================================
// TOPIC 4 -- query projection nr
// =====================================================================================================================
const nrReturn = findAll((n) => n.type === "ReturnStatement" && n.argument?.type === "ObjectExpression" && nrFn.start < n.start && n.end < nrFn.end)[0].argument;
const nrFields = nrReturn.properties.map((p) => ({ key: p.key.name, expression: raw(p.value), byteStart: byteAt(E, p.start) }));
const nrLocals = findAll((n) => n.type === "VariableDeclarator" && nrFn.start < n.start && n.end < nrFn.end).map((d) => ({ name: raw(d.id), init: raw(d.init) }));
item("nr-projection", 4, "Query projection nr (every field)", nrFn,
  (rec) => `nr(page=B, pageSize=50) returns exactly these fields in this order: ${rec.analysis.fields.map((f) => f.key).join(", ")}. Common fields (every tab): serverId=R, keyword=et (raw, untrimmed), page, pageSize, sorts=Jt[tab]. Tab-specific: resourceNameKey (resource tab only) / monsterNameKey (monster tab only) = At[tab]; alliance/withoutAlliance/markedOnly: city only; quality/specialOnly/reindeerOnly: tabs truck, railway, dispatch, ghost; itemKey: truck/railway; completionStatus: dispatch/ghost; plunderableOnly: true only for truck/railway/dispatch when its checkbox is on, else undefined; treasureType/suppliesType (via the treasure type key Bt looked up in Ct), includeForeignRadarTreasures, luckyFirst, viewerUid, viewerAllianceId: treasure only; minLevel and maxLevel (both Number(Kt), never a range): dispatch only when a level is chosen. L==='scheduledPlunder' is projected as 'city'. No field is trimmed or normalised; undefined fields are omitted by JSON serialisation.`,
  { analysis: { fields: nrFields, locals: nrLocals, helpers: ["ve", "We", "Ue", "A", "tt"].map((n) => ({ name: n, expression: raw(topFn(n)) })) }, confirmedBy: ["nr-per-tab-fields", "kw-filter-change-uses-typed-keyword", "export-city-query"] });
for (const helper of ["ve", "We", "Ue", "A", "tt"]) item(`nr-helper-${helper}`, 4, `nr helper ${helper}`, topFn(helper), `Module helper ${helper} used by nr (see expression).`, { confirmedBy: ["nr-per-tab-fields"] });

// =====================================================================================================================
// TOPIC 5 -- tab handler ir and tab/server effects
// =====================================================================================================================
const yeFn = topFn("ye");
item("tab-ir", 5, "Tab handler ir", irFn,
  "No-op when the target equals the current tab L. Otherwise: bump the search generation T (invalidates any in-flight search) and the treasure-refresh generation Le; save the leaving tab's {page, rows, total} in the tab cache D (not for Scheduled); restore the target tab's cached {page, rows, total} (default {page:1, rows:[], total:0}; when the target is Scheduled the current page/rows/total are kept); change the tab through onActiveTabChange when the parent controls it, else through the internal state; set loading true only when entering a real tab that has no cache entry (the search effect that follows the tab change sets it true again through rr, so every tab entry shows loading until the reply); clear the message text En. NOT reset by ir: keyword et, name selections At, every filter and sort, truck selection sn, random delay mn, busy key pn, sharing/exporting flags, treasure claim message wn, scan error bn. (Dispatch selection G is cleared by the tab-change effect, not by ir.)",
  { analysis: { setterCalls: setterCalls(irFn), helperYe: raw(yeFn) }, confirmedBy: ["tab-change-effects", "tab-cache-restore", "tab-change-same-tab-noop"] });
item("tab-cache-helper", 5, "Tab cache helper ye", yeFn,
  "ye(cache, leavingTab, snapshot, targetTab): stores the snapshot under the leaving tab (when not Scheduled) and returns the target tab's stored entry or an empty default; with no target tab (Scheduled) it returns the snapshot unchanged.", { confirmedBy: ["tab-cache-restore"] });
item("tab-effect-change", 5, "Effect on tab change ([L])", effects.tabChange,
  "On every change of L (and on mount): clears the whole dispatch/ghost selection on({}) and, when the new tab is Scheduled, reloads the plunder job lists with Q(). It does not clear the truck selection, keyword, name selections, random delay or message.",
  { analysis: { deps: depsOf(effects.tabChange) }, confirmedBy: ["tab-change-effects", "selection-dispatch-cleared-on-tab-change", "selection-truck-survives-tab-change"] });
item("server-effect", 5, "Effect on server change ([R, C.serverId])", effects.serverChange,
  "When the scan-state server C.serverId differs from the data server R: bump search generation T and clear the tab cache D; then if C.serverId<=0 set R=0, page 1, rows [], total 0, loading false; else set R=C.serverId, page 1, rows [], total 0, loading true. Not reset: keyword, name selections, filters, sorts, dispatch and truck selections, random delay, message, treasure claim message, scheduled job lists.",
  { analysis: { deps: depsOf(effects.serverChange), setterCalls: setterCalls(effects.serverChange) }, confirmedBy: ["server-change-effects", "selection-survives-server-change"] });
item("search-effect-tab-server", 5, "Search effect reaction to server loss", effects.search,
  "When R is falsy (no data server) the search effect does not request: it bumps T and sets page 1, rows [], total 0 (loading is cleared by the server effect).", { confirmedBy: ["server-loss-no-request"] });
item("tab-ticker-effect", 5, "Clock ticker effect", effects.clockTicker,
  "Runs on [L, C.isReading]. Unless a scan is reading or the tab is dispatch/ghost/truck/scheduledPlunder it does nothing (no interval). Otherwise it sets currentTime to Date.now() immediately and every 1000 ms until cleanup.",
  { analysis: { deps: depsOf(effects.clockTicker) }, confirmedBy: ["scheduled-ticker-conditions"] });
item("tr-clear-data", 5, "Clear-data handler tr", fnDecl("tr"),
  "Clears scanError, calls scan_clear(R); on success: replaces scan state, resets alliances/name options/levels/counts/reward items/treasure types/no-alliance/scan progress/alliance filter/name selection/dispatch level/item filter/treasure type, sets R, clears the tab cache, page 1, rows [], total 0, loading false, clears BOTH the dispatch selection on({}) and the truck selection cn({}), bumps rows and options revisions. On failure only scanError and a log line.",
  { analysis: { setterCalls: setterCalls(fnDecl("tr")), importCalls: importCalls(fnDecl("tr")) }, confirmedBy: ["clear-data-resets"] });

// =====================================================================================================================
// TOPIC 6 -- selection
// =====================================================================================================================
const orDecl = declarator("or");
const srDecl = declarator("sr");
const atFnDecl = findProg("at memo", (n) => n.type === "VariableDeclarator" && n.id.type === "Identifier" && n.id.name === "at");
const meFn = findProg("me checkbox fn", (n) => n.type === "FunctionDeclaration" && n.id.name === "me" && n.start > atFnDecl.start && n.end < atFnDecl.end);
const atJsx = findOne("at element", (n) => isJsx(n) && raw(n.arguments[0]) === "at");
const IeFn = IDX.functions.Ie;
const FeFn = IDX.functions.Fe;
assert.ok(IeFn && FeFn);
const keyTemplate = "`${serverId}:${uuid||''}`";
item("sel-dispatch-state", 6, "Dispatch/Ghost selection collection", stateDecl("G"),
  (rec) => `One object state G (initial {}) keyed by ${keyTemplate} with the toggled ROW (plus taskKind) as value. The same single collection serves Dispatch AND Ghost. Writers: ${[...new Set(rec.analysis.writers.map((w) => `${w.where.replace(/^R > /, "")} [${w.call}]`))].join("; ")}. Counts used in the UI are Object.keys(G).length.`,
  { analysis: { writers: stateWriters.dispatchSelection, references: refsOf("G") }, confirmedBy: ["selection-dispatch-toggle", "selection-dispatch-cleared-on-tab-change", "share-success-prunes-and-message", "schedule-dispatch-success-clears"] });
item("sel-truck-state", 6, "Truck selection collection", stateDecl("sn"),
  (rec) => `One object state sn (initial {}) keyed by ${keyTemplate} with the toggled row as value. Writers: ${[...new Set(rec.analysis.writers.map((w) => `${w.where.replace(/^R > /, "")} [${w.call}]`))].join("; ")}. It is NOT cleared by tab change, page change, refetch, server change or scan completion; only by successful truck scheduling and by clear-data.`,
  { analysis: { writers: stateWriters.truckSelection, references: refsOf("sn") }, confirmedBy: ["selection-truck-toggle", "selection-truck-survives-tab-change", "selection-survives-server-change", "schedule-truck-success-clears"] });
item("sel-toggle-dispatch", 6, "Dispatch/Ghost toggle callback or", orDecl,
  "Stored key = `${row.serverId}:${row.uuid||''}` (NOT trimmed). If the key exists it is deleted, otherwise it is added with the full row object received (snapshot at toggle time). useCallback with no dependencies; no validation of the row. Rows whose uuid has surrounding whitespace are stored under an untrimmed key, which the checkbox lookup (trimmed) never finds.",
  { confirmedBy: ["selection-dispatch-toggle", "selection-key-trim-mismatch", "selection-duplicate-ids"] });
item("sel-toggle-truck", 6, "Truck toggle callback sr", srDecl,
  "Identical construction to `or` but on the truck collection sn (same untrimmed key, same snapshot payload, no validation).", { confirmedBy: ["selection-truck-toggle", "selection-key-trim-mismatch"] });
item("sel-key-sets", 6, "Key sets passed to the table (Un, Wn)", findOne("Un/Wn", (n) => n.type === "VariableDeclarator" && n.id.name === "Un"),
  "Un = new Set(Object.keys(G)) and Wn = new Set(Object.keys(sn)) memoised on G/sn; they are passed to the table as selectedDispatchKeys / selectedTruckKeys. The table only reads membership.", { confirmedBy: ["selection-dispatch-toggle"] });
item("sel-checkbox", 6, "Table checkbox (function me inside at)", meFn,
  "Row checkbox. Truck kind: checked = selectedTruckKeys.has(`${serverId}:${trim(uuid)}`), disabled = state of Ie(row,currentTime) in [invalid, full, expired], onChange -> onToggleTruck(row). Dispatch/Ghost kinds: checked = selectedDispatchKeys.has(`${serverId}:${trim(uuid)}`), disabled = uuid(trimmed) is not all digits OR completionTime not a positive number OR plunderAt not a positive number OR F(row,currentTime) in [expired, full], onChange -> onToggleDispatch({...row, taskKind: ghost ? 'ghost' : 'dispatch'}). The lookup key is built from the TRIMMED uuid while the stored key (or/sr) is untrimmed. A disabled checkbox that is already selected stays checked and selected (no pruning).",
  { analysis: { importIe: raw(IeFn, IDX.entry), importFe: raw(FeFn, IDX.entry), moduleF: raw(topFn("F")), moduleP: raw(topFn("P")) }, confirmedBy: ["selection-checkbox-predicates", "selection-key-trim-mismatch", "selection-disabled-stays-selected"] });
item("sel-at-element", 6, "Table element props", atJsx,
  "The table is mounted only when L is not Scheduled, receiving selectedDispatchKeys=Un, selectedTruckKeys=Wn, onToggleDispatch=or, onToggleTruck=sr, rows=H, loading=Xt, currentTime=rn, sortState=Jt[L], onSort=ar and more. Both selection sets are passed for every tab; the table picks the relevant one by kind.", { confirmedBy: ["selection-dispatch-toggle"] });
item("sel-ie-truck-state", 6, "Truck row state function Ie (index asset)", IeFn,
  "Ie(row,now): invalid when uuid (trimmed) is empty or serverId is not a positive integer; expired when arriveTs>0 and <= now; full when robTimes >= max loot; protected when protectTime > now; else ready.", { entry: IDX.entry, confirmedBy: ["selection-checkbox-predicates"] });
item("sel-F-dispatch-state", 6, "Dispatch task state function F", topFn("F"),
  "F(row,now): expired when taskExpireTime reached; full when stolenCount >= maxStealCount (>0); pending when no completionTime or now < completionTime; protected when now < plunderAt; else ready.", { confirmedBy: ["selection-checkbox-predicates"] });
{
  const countRefs = [...refsOf("G"), ...refsOf("sn")].filter((r) => /Object\.(keys|values)/.test(r.excerpt));
  item("sel-derived-counts", 6, "Counts/values derived from the collections", srDecl,
    "Labels and predicates use Object.keys(G).length / Object.keys(sn).length (number of distinct stored keys, not rows on screen); scheduling uses Object.values(G) / Object.values(sn) (snapshots, including rows no longer on the current page).",
    { analysis: { references: countRefs }, confirmedBy: ["schedule-button-disabled-predicate", "selection-across-page"] });
}
item("sel-writers-all", 6, "Every writer of both selection setters", orDecl,
  "Complete list of setter call sites for on/cn is in analysis (dispatch: tab-change effect, toggle, schedule success, share success prune, clear-data; truck: toggle, schedule success, clear-data).",
  { analysis: { dispatch: stateWriters.dispatchSelection, truck: stateWriters.truckSelection }, confirmedBy: ["selection-dispatch-cleared-on-tab-change", "selection-truck-survives-tab-change"] });

// =====================================================================================================================
// TOPIC 7 -- random delay
// =====================================================================================================================
const mnState = stateDecl("mn");
const GnDecl = declarator("Gn");
const KnDecl = declarator("Kn");
const delayInput = jsxCall("random delay input", "input", (n) => raw(n).includes("\"aria-label\":S(`map.randomDelaySeconds`)"));
const drFn = fnDecl("dr");
const OnFn = IDX.functions.On;
item("delay-state", 7, "Random delay state", mnState,
  "mn is a raw STRING state, initial '0', shared by Dispatch and Ghost (single state, not per tab). Written only by the input's onChange; not reset by tab change, page change, schedule success, clear or server change.",
  { analysis: { writers: stateWriters.randomDelay }, confirmedBy: ["delay-shared-and-persistent"] });
item("delay-input", 7, "Random delay input", delayInput,
  "<input type=number min='0' step='1' aria-label=map.randomDelaySeconds value=mn onChange=hn(e.target.value)>, rendered only on dispatch/ghost tabs. onChange stores the raw string with no clamping or normalisation. (A real browser additionally sanitises type=number text; the harness passes strings straight to onChange.)",
  { analysis: { propNames: propsOf(delayInput).properties.map((p) => p.key.name || p.key.value) }, confirmedBy: ["delay-parse-matrix"] });
item("delay-parse-Gn", 7, "Parsed delay Gn", GnDecl, "Gn = Number(mn): '' and whitespace-only strings parse to 0, '1e2' to 100, '0x10' to 16, '-1' to -1, '1.5' to 1.5.", { confirmedBy: ["delay-parse-matrix"] });
item("delay-valid-Kn", 7, "Validity Kn", KnDecl, "Kn = Number.isSafeInteger(Gn) && Gn >= 0: valid = a safe non-negative integer (so '', ' ', '1e2', '0x10', '-0' valid; '-1', '1.5', 'abc', '9007199254740992' and above, 'Infinity' invalid).", { confirmedBy: ["delay-parse-matrix"] });
item("delay-schedule-handler", 7, "Schedule handler dr (uses Gn/Kn)", drFn,
  "dr(): rows = Object.values(G); returns immediately (no state change, no message) when there are no rows or when the delay is invalid (!Kn); otherwise busyKey='schedule', awaits h(rows, Gn) -- the parsed NUMBER Gn, not the string -- then clears the selection on({}), reloads the job lists Q(), switches to the Scheduled tab ir('scheduledPlunder'), logs 'scheduled N secret task plunder jobs'; on failure only logs 'dispatch plunder schedule error ...' (selection kept); busyKey is cleared in finally. Share (pr) does not read the delay.",
  { analysis: { setterCalls: setterCalls(drFn), importCalls: importCalls(drFn), references: { mn: refsOf("mn"), Gn: refsOf("Gn"), Kn: refsOf("Kn") } }, confirmedBy: ["schedule-dispatch-success-clears", "schedule-dispatch-failure-keeps-selection", "delay-parse-matrix"] });
item("delay-native-schedule-fn", 7, "Imported schedule function h = On (index asset; stubbed in the harness)", OnFn,
  "On(rows, t=0) computes, per row, a random delay in [0, min(t, task-expiry window, overflow guard)] using Math.random and shifts plunderAt by it. It is native-facing IPC (map_dispatch_plunder_schedule) and is only RECORDED by the harness; the UI contract is the argument pair (rows, Gn).", { entry: IDX.entry });

// =====================================================================================================================
// TOPIC 8 -- action buttons
// =====================================================================================================================
function button(id, label, node, statement, extra = {}) {
  const propNode = propsOf(node);
  const dis = prop(propNode, "disabled");
  const kids = prop(propNode, "children");
  const analysis = {
    propNames: propNode.properties.map((p) => p.key.name || p.key.value),
    disabledExpression: dis ? raw(dis.value) : null,
    disabledReads: dis && extra.inR !== false ? predicateRefs(dis.value) : [],
    labelExpression: kids ? raw(kids.value) : null,
    onClick: prop(propNode, "onClick") ? raw(prop(propNode, "onClick").value) : null,
    ...(extra.analysis || {}),
  };
  return item(id, 8, label, node, statement, { analysis, confirmedBy: extra.confirmedBy });
}
const scheduleBtn = jsxCall("schedule dispatch button", "button", (n) => raw(n).includes("S(`map.scheduleSelected`,"));
const shareBtn = jsxCall("share button", "button", (n) => raw(n).includes("map.sharingAlliance"));
const truckBtn = jsxCall("truck schedule button", "button", (n) => raw(n).includes("map.scheduleSelectedTrucks"));
const exportBtn = jsxCall("export button", "button", (n) => raw(n).includes("map.exportingExcel"));
const claimBoxes = jsxCall("claim boxes", "button", (n) => raw(n).includes("map.claimTreasureBoxes"));
const claimSeason = jsxCall("claim season", "button", (n) => raw(n).includes("map.claimSeasonTreasures"));
button("btn-schedule-dispatch", "Schedule selected (dispatch/ghost)", scheduleBtn,
  "Shown on dispatch and ghost. disabled = no selection OR busyKey==='schedule' OR sharing OR the delay is invalid (!Kn). It does NOT depend on online, scan state, pagination, loading or the selection's validity. Label map.scheduleSelected with {count: number of selected keys}; there is no busy-state label. onClick=dr.",
  { confirmedBy: ["schedule-button-disabled-predicate"] });
button("btn-share-alliance", "Share to alliance (dispatch only)", shareBtn,
  "Shown on dispatch only. disabled = !online OR scan reading OR no selection OR busyKey==='schedule' OR sharing. Label map.shareAlliance, or map.sharingAlliance while sharing (kn). It does not depend on the random delay. onClick=pr. The online prop (game connection) is a native-availability gate used directly by this original UI predicate.",
  { confirmedBy: ["share-button-disabled-predicate"] });
button("btn-schedule-truck", "Schedule selected trucks", truckBtn,
  "Shown on the truck tab. disabled = no truck selection OR busyKey==='schedule-truck'. No dependency on online, scan state, sharing or the dispatch busy key. Label map.scheduleSelectedTrucks {count}; no busy-state label. onClick=ur.",
  { confirmedBy: ["truck-schedule-button-predicate"] });
button("btn-export", "Export Excel (city)", exportBtn,
  "Shown on the city tab. disabled = exporting OR R<=0 OR scan reading. Label map.exportExcel / map.exportingExcel while exporting (Dn). onClick=fr. Does not depend on online.", { confirmedBy: ["export-city-query"] });
button("btn-claim-boxes", "Claim treasure boxes", claimBoxes,
  "Treasure tab. disabled = !Ae({online, isReading, dataServerId: R, currentServerId: C.serverId, busy: Sn}) where Ae = online && !isReading && dataServerId>0 && currentServerId===dataServerId && !busy. Label map.claimTreasureBoxes / map.claimingTreasures while busy (Sn). onClick=cr('boxes').", { confirmedBy: ["treasure-claim-button-predicate"] });
button("btn-claim-season", "Claim season treasures", claimSeason,
  "Treasure tab. Same predicate as claim boxes; label map.claimSeasonTreasures / map.claimingTreasures. onClick=cr('season').", { confirmedBy: ["treasure-claim-button-predicate"] });
{
  const geFn = findProg("treasure row claim fn", (n) => n.type === "FunctionDeclaration" && n.id.name === "ge" && n.start > atFnDecl.start && n.end < atFnDecl.end);
  const geBtn = findProg("treasure row claim button", (n) => isJsx(n) && raw(n.arguments[0]) === "`button`" && n.start > geFn.start && n.end < geFn.end);
  item("btn-claim-row", 8, "Per-row treasure claim button (inside at)", geBtn,
    "disabled = (claim disabled from Ae) OR uuid empty/'0' OR (supply type not 1/3/4 AND row.complete!==true) OR player state in [dispatching, scouting, digging, claiming, claimed] OR world state depleted/expired OR claim blocked for other alliance. Label map.claimTreasure / map.claimingTreasure (while busy). onClick=onClaimTreasure(row) -> cr('single', row).",
    { analysis: { disabledExpression: raw(prop(propsOf(geBtn), "disabled").value) }, confirmedBy: ["treasure-row-claim-predicate"] });
  item("fn-Ae", 8, "Claim availability Ae", topFn("Ae"), "Ae(e) = online && !isReading && dataServerId > 0 && currentServerId === dataServerId && !busy.", { confirmedBy: ["treasure-claim-button-predicate"] });
}
const handlerSpecs = [
  ["dr", "Schedule dispatch/ghost handler", "dr(): rows = Object.values(G); guarded by !Kn; busy='schedule'; success clears G, reloads jobs, switches to Scheduled (see delay-schedule-handler).", ["schedule-dispatch-success-clears"]],
  ["pr", "Share to alliance handler", "pr(): rows = Object.values(G); no rows -> return. Else sharing=true, message cleared; await re(rows); on success removes from G exactly the entries whose String(uuid||'') is in reply.sharedUuids (others stay selected), sets message map.shareAllianceSuccess {count: shared} or map.shareAlliancePartial {shared, failed} when failed>0; on failure message = translated error m(S,e) and selection kept; finally sharing=false. Does not touch the busy key or the Scheduled list.", ["share-success-prunes-and-message", "share-failure-keeps-selection"]],
  ["ur", "Schedule trucks handler", "ur(): rows = Object.values(sn); none -> return. Else busyKey='schedule-truck'; await ne(rows); on success clears sn, reloads jobs Q(), switches to Scheduled (ir), logs; on failure only logs and keeps the selection; finally busyKey=''.", ["schedule-truck-success-clears", "schedule-truck-failure-keeps-selection"]],
  ["mr", "Clear history handler", "mr(kind): busyKey='clear:'+kind, message cleared; kind==='truck' -> n(now) else me(now, kind); then Q(); on failure message = translated error; finally busyKey=''.", ["scheduled-clear-and-cancel"]],
  ["hr", "Cancel (dispatch/ghost) handler", "hr(job): busyKey=serverId:uuid; ee(serverId, ghost ? 'ghost:'+uuid : uuid) then Q(); failure only logs; finally busyKey=''.", ["scheduled-clear-and-cancel"]],
  ["gr", "Cancel (truck) handler", "gr(job): busyKey='truck:'+serverId:uuid; fe(serverId, uuid) then Q(); failure only logs; finally busyKey=''.", ["scheduled-clear-and-cancel"]],
  ["_r", "Plunder again (truck) handler", "_r(job): busyKey='truck:'+serverId:uuid; ne([job]) then Q(); failure only logs; finally busyKey=''. It stays on the Scheduled tab.", ["scheduled-clear-and-cancel"]],
  ["cr", "Treasure claim handler", "cr(scope,row): treasureClaimBusy=true, treasureClaimMessage cleared; await he(R, scope, J, uuid); sets message map.treasuresQueued; when queued>0 polls once per second (up to 1800 times) the claim status, merging states into rows, until batch.state!=='running' (then bumps rows revision); on failure message = translated error; finally busy=false.", ["treasure-claim-flow"]],
  ["fr", "City export handler", "fr(): city tab with R>0 only; exporting=true, message cleared; await ie(nr(1,200), {...}); unless canceled sets message map.exportExcelSuccess; failure sets translated message; finally exporting=false.", ["export-city-query"]],
];
for (const [name, label, statement, confirmed] of handlerSpecs) {
  const fn = fnDecl(name);
  item(`fn-${name}`, 8, label, fn, statement, { analysis: { setterCalls: setterCalls(fn), importCalls: importCalls(fn) }, confirmedBy: confirmed });
}

// ---- children inside ot / st (scheduled tables)
const otFn = topFn("ot");
const stFn = topFn("st");
const inFn = (fn, text, tag = "button") => findProg(`${fn.id.name}:${text}`, (n) => isJsx(n) && raw(n.arguments[0]) === `\`${tag}\`` && n.start > fn.start && n.end < fn.end && raw(n).includes(text));
button("btn-secret-clear", "Scheduled secret/ghost: clear history button (ot)", inFn(otFn, "map.clearPlunderHistory"),
  "disabled = any busy key set OR no job in {succeeded, failed, cancelled, expired}; onClick -> onClear(kind) = mr(kind). Label map.clearPlunderHistory (no busy label).", { confirmedBy: ["scheduled-clear-and-cancel"], inR: false });
button("btn-secret-cancel", "Scheduled secret/ghost: Cancel button (ot)", inFn(otFn, "common.cancel"),
  "Shown only for jobs scheduled|waiting_connection; disabled = busyKey === `${serverId}:${uuid}`; onClick -> onCancel(job)=hr. Label common.cancel.", { confirmedBy: ["scheduled-clear-and-cancel"], inR: false });
button("btn-truck-clear", "Scheduled truck: clear history button (st)", inFn(stFn, "map.clearPlunderHistory"),
  "disabled = any busy key set OR no truck job in {succeeded, failed, cancelled, expired}; onClick -> mr('truck').", { confirmedBy: ["scheduled-clear-and-cancel"], inR: false });
button("btn-truck-cancel", "Scheduled truck: Cancel button (st)", inFn(stFn, "common.cancel"),
  "Shown only for scheduled|waiting_connection; disabled = busyKey === `truck:${serverId}:${uuid}`; onClick -> gr.", { confirmedBy: ["scheduled-clear-and-cancel"], inR: false });
button("btn-truck-again", "Scheduled truck: Plunder again button (st)", inFn(stFn, "map.plunderAgain"),
  "Shown when online AND the job succeeded AND the truck state is not invalid/full/expired AND no job of the same serverId:uuid is currently active (scheduled|waiting_connection|running); disabled = busyKey === `truck:${serverId}:${uuid}`; onClick -> _r (re-schedules the job row). Depends on the online prop (native availability).", { confirmedBy: ["scheduled-clear-and-cancel"], inR: false });

// =====================================================================================================================
// TOPIC 9 -- Scheduled Plunder data flow
// =====================================================================================================================
const QFn = fnDecl("Q");
item("sched-Q", 9, "Job list loader Q", QFn,
  "Q(): awaits d() (map_plunder_jobs_list) and stores dispatchJobs (ln) and truckJobs (dn) wholesale; on failure only logs 'dispatch plunder list error ...' (lists unchanged). No generation guard: replies are applied in arrival order.",
  { analysis: { setterCalls: setterCalls(QFn), importCalls: importCalls(QFn), refsQ: refsOf("Q") }, confirmedBy: ["scheduled-loads-on-entry", "scheduled-stale-job-reply-applies-in-arrival-order"] });
item("sched-mount-events", 9, "Mount + event effect", effects.jobsMountAndEvents,
  "On mount: Q() immediately, then subscribes to bridge://dispatch-plunder-changed and bridge://truck-plunder-changed, both calling Q (payload ignored); unsubscribes on cleanup. Runs once per mount (deps []).",
  { analysis: { deps: depsOf(effects.jobsMountAndEvents) }, confirmedBy: ["scheduled-loads-on-mount-and-events"] });
item("sched-tab-entry", 9, "Tab-entry reload", effects.tabChange,
  "Entering the Scheduled tab (L changes to scheduledPlunder) runs Q() again via the tab-change effect; other tabs do not load jobs.", { confirmedBy: ["scheduled-loads-on-entry"] });
item("sched-tab-count", 9, "Scheduled tab count", findOne("scheduled tab count span", (n) => isJsx(n) && raw(n.arguments[0]) === "`span`" && raw(n.arguments[1]) === "{className:`map-tab-count`,children:ln.length+dn.length}"),
  "The Scheduled tab badge is ln.length + dn.length (dispatch+ghost jobs plus truck jobs), always shown, independent of the data-server counts (other tabs show _t[kind]||0, or an em dash until counts are loaded). The result-count span on the Scheduled tab shows the same sum.", { confirmedBy: ["scheduled-count-and-render-order"] });
{
  const calls = findAll((n) => isJsx(n) && ["ot", "st"].includes(raw(n.arguments[0])));
  assert.equal(calls.length, 3);
  assert.ok(calls[0].start < calls[1].start && calls[1].start < calls[2].start);
  item("sched-render-order", 9, "Scheduled tab three-group render order", calls[0],
    "On the Scheduled tab the groups render in this order: (1) ot with jobs = dispatch jobs whose taskKind!=='ghost' (title map.secretTask), (2) ot kind='ghost' with jobs whose taskKind==='ghost', (3) st with the truck jobs. Each group has its own count, clear button and empty row (map.empty). Rows keep the order the backend returned (no sorting in the UI).",
    { analysis: { groups: calls.map((n) => ({ component: raw(n.arguments[0]), expression: raw(n).slice(0, 220), byteStart: byteAt(E, n.start) })) }, confirmedBy: ["scheduled-count-and-render-order"] });
}
item("sched-ticker", 9, "currentTime ticker", effects.clockTicker,
  "currentTime (rn) is refreshed to Date.now() immediately and then each second only while a scan is reading or the tab is dispatch, ghost, truck or scheduledPlunder; otherwise the interval is not running and rn keeps its last value.", { confirmedBy: ["scheduled-ticker-conditions"] });
item("sched-jobs-state", 9, "Job list states", stateDecl("ln"),
  "ln = dispatch/ghost jobs, dn = truck jobs (both initial []); written only by Q (never pruned or sorted by the UI).", { analysis: { writersDispatch: stateWriters.scheduledDispatchJobs, writersTruck: stateWriters.scheduledTruckJobs } });

// ---- which UI predicates read the native-availability inputs (online prop l, scan-reading C.isReading) -----------------------
{
  const onlineProp = findOne("online prop", (n) => n.type === "ObjectProperty" && n.key.name === "online" && n.value.name === "l" && n.start < R.body.start);
  const readingRefs = findAll((n) => n.type === "MemberExpression" && raw(n) === "C.isReading").map((n) => byteAt(E, n.start));
  const onlineRefs = refsOf("l");
  item("native-online-reads", 8, "Where the online prop (l) and scan-reading flag (C.isReading) are read", onlineProp,
    (rec) => `The online prop is read at ${rec.analysis.onlineReads.length} sites: ${rec.analysis.onlineReads.map((r) => `${r.where.replace(/^R > /, "")}${r.jsx ? ` ${r.jsx}` : ""}${r.prop ? ` ${r.prop}` : ""} [${r.excerpt.slice(0, 50)}]`).join("; ")}. C.isReading (a scan is running) is read at ${rec.analysis.readingByteStarts.length} sites. The search effect and rr read it only for the treasure tab (viewer-identity wait and treasure state refresh). Everything not listed there (Search button, Schedule selected, Schedule trucks, Cancel, Clear history, name select/filters, pagination, selection checkboxes) never reads online and only the sites above read the scan flag, so those controls are not gated by native availability in the original.`,
    { analysis: { onlineReads: onlineRefs, readingByteStarts: readingRefs }, confirmedBy: ["schedule-button-disabled-predicate", "share-button-disabled-predicate", "truck-schedule-button-predicate", "treasure-claim-button-predicate"] });
}

// =====================================================================================================================
// APP-LEVEL PARENT (index asset): how the original App renders MapDataPanel
// =====================================================================================================================
const IA = IDX.entry;
const idxNodes = [];
walkAll(IDX.ast.program, (node, parents) => idxNodes.push({ node, parents }));
const idxRaw = (n) => rawOf(IA, n);
const findIdx = (label, pred) => {
  const found = idxNodes.filter(({ node, parents }) => pred(node, parents)).map((x) => x.node);
  assert.equal(found.length, 1, `${label}: expected 1 index match, got ${found.length}`);
  return found[0];
};
const activityNode = findIdx("App Activity wrapper", (n) => n.type === "CallExpression" && idxRaw(n.callee).replace(/^\(|\)$/g, "") === "0,M.jsx" && idxRaw(n.arguments[0]) === "b.Activity" && idxRaw(n).includes("Bi,{activeTab"));
item("app-usage", 10, "How the original App renders MapDataPanel", activityNode,
  "The Map page is mounted lazily the first time map-data is visited and then kept mounted inside <Activity mode=visible|hidden>. Props: activeTab = rt ? Ke : undefined and onActiveTabChange = rt ? qe : undefined (controlled by the App tab state Ke, initial 'city', only when the entitlement allows more than one profile: rt = (entitlement.maxProfiles ?? 1) > 1; otherwise UNCONTROLLED); scanState = pe (null until first loaded, then R falls back to Ve), summary = ge, online = at (game connected), onState = pt, onCounts = bt, onLog = F, autoScanConfig = ve, autoScanRunning = be, onAutoScanConfig = Ct. While hidden React disconnects all effects (timers, listeners) and re-runs them all on reveal (state kept): modelled by the harness hide()/show(), IMPLEMENTED_NOT_VALIDATED against a browser.",
  { entry: IA, analysis: { activityExpression: idxRaw(activityNode) }, confirmedBy: ["app-controlled-vs-uncontrolled-tab", "activity-hide-show-modelled"] });
item("app-controlled-flag", 10, "Controlled-tab flag rt", findIdx("rt declarator", (n) => n.type === "VariableDeclarator" && n.id.type === "Identifier" && n.id.name === "rt" && idxRaw(n).includes("se(r.entitlement)")),
  "rt = se(r.entitlement) where se(e) = (e?.maxProfiles ?? 1) > 1. Single-profile entitlement => rt=false => activeTab/onActiveTabChange are undefined (uncontrolled mode); multi-profile => controlled by App state Ke.", { entry: IA });
item("app-se", 10, "se(entitlement)", findIdx("se maxProfiles", (n) => n.type === "FunctionDeclaration" && n.id.name === "se" && idxRaw(n).includes("maxProfiles")), "Returns true when maxProfiles > 1.", { entry: IA });
for (const [name, text] of [["pt", "function pt(e){let t=Se.current"], ["bt", "function bt(e,t){_e(n=>"], ["F", "function F(e){ft(e)"]]) {
  const fn = findIdx(`App ${name}`, (n) => n.type === "FunctionDeclaration" && n.id.name === name && idxRaw(n).startsWith(text));
  item(`app-${name}`, 10, `App callback ${name}`, fn,
    name === "pt" ? "onState: stores the scan state in App state, mirrors it into the summary of the same server and, when a scan has just finished, refreshes the summary. The harness applies the new scanState prop (option parent.applyOnState)."
      : name === "bt" ? "onCounts: updates summary.counts for the given server. The harness applies it (option parent.applyOnCounts)." : "onLog: forwards the line to the log; the harness records it in `logs`.",
    { entry: IA });
}
item("app-Si", 10, "Default auto-scan config Si", (() => { const d = IDX.variables.Si.declarator; assert.ok(idxRaw(d).startsWith("Si={enabled:")); return d; })(), "Default autoScanConfig passed by the App initially (harness default).", { entry: IA });

// =====================================================================================================================
// ALIAS TABLE (import header)
// =====================================================================================================================
const aliasRows = [];
const bound = {};
for (const decl of P.imports) for (const s of decl.specifiers) bound[s.local.name] = { from: decl.source.value, exported: s.imported.name, byteStart: byteAt(E, s.start) };
for (const [alias, meta] of Object.entries(IMPORT_ALIASES)) {
  const b = bound[alias];
  assert.equal(b.exported, meta.exported);
  const row = { alias, from: b.from, exported: meta.exported, harness: meta.stub ? `stub:${meta.stub}` : meta.pure ? `pure:${meta.pure}` : meta.react ? `react:${meta.react}` : `component:${meta.component}`, note: meta.note, importByte: b.byteStart };
  if (!meta.from) {
    const internal = IDX.exportedToInternal[meta.exported];
    row.internal = internal;
    const fn = IDX.functions[internal];
    const variable = IDX.variables[internal];
    if (fn || variable) {
      row.indexByteStart = byteAt(IA, (fn || variable.statement).start);
      row.indexExpression = idxRaw(fn || variable.declarator).slice(0, 360);
    }
  }
  aliasRows.push(row);
}

// =====================================================================================================================
// STATE / REF TABLES with declarations
// =====================================================================================================================
const stateRows = STATE_SEMANTICS.map(([sem, value, setter, init], index) => {
  const d = stateDecl(value);
  return { index, semantic: sem, original: value, setter, initializer: init, declaration: raw(d), byteStart: byteAt(E, d.start), prettyLine: PR.lineOf(d.start) };
});
const refRows = REF_SEMANTICS.map(([sem, value, init], index) => {
  const d = declarator(value);
  return { index, semantic: sem, original: value, initializer: init, declaration: raw(d), byteStart: byteAt(E, d.start) };
});

// ---- timer-path assertion: no timer site lies inside the search path ---------------------------------------------
for (const site of timerSites) {
  for (const node of [nrFn, rrFn, keywordOnChange, searchOnClick, searchEffect, nameSelectOnChange]) {
    const lo = byteAt(E, node.start); const hi = byteAt(E, node.end);
    assert.ok(site.byteStart < lo || site.byteStart >= hi, `timer site ${site.api}@${site.byteStart} inside the search path`);
  }
}

// =====================================================================================================================
// OUTPUT
// =====================================================================================================================
const topics = {
  1: "Keyword input and search issuing", 2: "Search button", 3: "Resource/Monster name select", 4: "Query projection nr",
  5: "Tab handler ir and tab/server effects", 6: "Selection (Dispatch/Ghost and Truck)", 7: "Random delay", 8: "Action buttons and handlers",
  9: "Scheduled Plunder data flow", 10: "Parent (App) integration",
};
const contract = {
  evidenceState: "EXACT_BYTES (expressions/offsets); statements are the reviewer's reading, executed in check-original-runtime.mjs",
  generatedBy: "original/recover-contract.mjs",
  sources: Object.fromEntries(Object.keys(ASSET_HASHES).map((name) => [name, ASSET_HASHES[name]])),
  prettyReference: { path: "evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/reference/MapDataPanel.pretty.js", sha256: PR.sha256 },
  notes: [
    "Byte offsets are UTF-8 offsets into the ORIGINAL asset named in each locator.",
    "Identifiers are original minified names; the pretty file appends 2 to names colliding with module-level identifiers.",
  ],
  topics,
  items,
  stateWriters,
  tables: { imports: aliasRows, states: stateRows, refs: refRows },
};
fs.writeFileSync(path.join(originalDir, "contract.json"), `${JSON.stringify(contract, null, 2)}\n`);

// ---- contract.md (generated tables) ---------------------------------------------------------------------------------
const esc = (s) => String(s).replace(/\|/g, "\\|").replace(/\n/g, " ");
const md = [];
md.push("# Original LWBridge 0.3.17 Map panel: interaction contract (independent review)");
md.push("");
md.push("Generated by `original/recover-contract.mjs` from the hash-pinned assets (do not edit by hand). Source: `MapDataPanel-B4GXEND2.js` SHA-256 `" + ASSET_HASHES["MapDataPanel-B4GXEND2.js"] + "`, `index-BVfnK1wp.js` SHA-256 `" + ASSET_HASHES["index-BVfnK1wp.js"] + "`.");
md.push("");
md.push("Locators are UTF-8 byte offsets (start-end) into the ORIGINAL asset plus the line range in `reference/MapDataPanel.pretty.js` (layout-only re-print; original identifiers, except that locals colliding with module-level names get a `2` suffix, e.g. original `L` is `L2`). Evidence state `EXACT_BYTES` applies to the quoted expression and offsets (see `contract.json`); the plain-language statement is the reviewer's reading, which `check-original-runtime.mjs` executes (ids in the last column; see `results.json`).");
md.push("");
md.push("## How the original component is executed (original-runtime.mjs)");
md.push("");
md.push("- The three import declarations of the asset become the parameters of one evaluating function; `export{R as MapDataPanel};` becomes a `return`. Everything between (module helpers, child components `at`/`ot`/`st`/`it`/`ct`/`lt`, and `R`) is the unmodified asset text; `check-original-runtime.mjs` asserts `component.toString()` equals the asset bytes of `R`. The module is evaluated in strict mode like an ES module. Free globals of the module are exactly `Date`, `localStorage`, `window` (plus builtins); they are bound to a fake clock/storage/timer set (`advance(ms)` fires intervals and timeouts in order).");
md.push("- `y = a(o())`: `a` is the REAL `__toESM` extracted from `index-BVfnK1wp.js` (statement `var e=Object.create,...,u=...`), `o()` returns the hook delegate (`useState/useRef/useCallback/useMemo/useEffect` of `createHookRuntime` from `../harness.mjs`, React-ordered cleanup-then-effect commit). `memo(fn)` returns the function itself (its `displayName` is set to `at`, the only memo use in the asset). `E = i()` is a JSX runtime producing plain `{type, key, props}` objects; function components (`at` table, `it` pagination, `ot`/`st` scheduled groups, `ct`, `lt`) stay as typed elements and can be rendered separately with `expand(node)` (own throwaway hook runtime) so their props and checkbox/button predicates can be driven.");
md.push("- `useI18n()` returns `{language, t}`. Default `t` returns the key (with `common.itemCount`, `map.pageInfo` and `map.scheduleSelected*` interpolated like the canonical harness); option `translate: 'en'` uses the real English catalog `en-BisSXcTB.js` (its `...e.en` error tables are taken from the index asset) with the asset's own `{name}` interpolation.");
md.push("- Pure imports (`r`=Ie, `l`=Fe, `m`=Lr(+Ir), `oe`=Ci, `ue`=wi, `le`=Ti, `_`=reward count formatter) are the REAL functions executed from the asset bytes. Every native/backend-facing import is an inert recording stub: calls are appended to `calls` as `{id,name,alias,args}`; per-stub behaviour is `auto` (resolves a harness-chosen default, listed below), `manual` (deferred, resolved with `resolveCall`/`rejectCall`) or `reject`. The search stub `ce` always returns a deferred promise recorded in `requests` (`{id,kind,query,resolve,reject}`). Event subscriptions (`p`) are recorded in `listeners` and fired with `emitEvent(name)`.");
md.push("- Stub default replies are HARNESS choices (the real reply shapes beyond the fields `R` reads are UNKNOWN here): `dataOptions` -> empty lists/counts (the checks install the richer `optionsReply` of common.mjs), `plunderJobsList` -> the jobs set with `setJobs` (cloned per call), `dispatchShareAlliance` -> `{shared: rows, failed: 0, sharedUuids: all}`, `treasureClaim` -> `{eligible:0,queued:0,skipped:0}`, `treasureClaimStatus`/`treasureStateRefreshAll` -> empty viewer `{playerUid:''}` unless overridden (an empty viewer makes the online treasure tab wait for identity, see `search-treasure-tab-waiting-for-viewer`), `cityExport` -> `{canceled:true}`, `scanClear`/`scanStart`/`scanStop` -> the current scan state, others -> undefined.");
md.push("");
md.push("## Parent (App) props: the choice made by the harness");
md.push("");
md.push("Read from `app-usage` below. The original App renders `<MapDataPanel activeTab={rt?Ke:void 0} onActiveTabChange={rt?qe:void 0} scanState={pe} summary={ge} online={at} onState={pt} onCounts={bt} onLog={F} autoScanConfig={ve} autoScanRunning={be} onAutoScanConfig={Ct}/>` inside `<Activity>`; `rt` is true only for multi-profile entitlements. Harness: `tabMode:'uncontrolled'` (default; `activeTab`/`onActiveTabChange` undefined, tab kept in the internal state `uncontrolledTab`) or `tabMode:'controlled'` (the harness plays the App: `activeTab` prop + `onActiveTabChange` updating it; initial tab `initialTab`, default `city`). `scanState` defaults to the module default `Ve` with `serverId` 321 (`options.scanState` merges over it; `null` reproduces the App before the first load), `summary` to `{serverId, counts: 60 per kind, scanState}` (`null` = absent), `online` to `false` (canonical-harness default), `autoScanConfig` to the App default `Si`, `autoScanRunning` false. `onState`/`onCounts` are applied to the props like the App's `pt`/`bt` do (`parent.applyOnState/applyOnCounts:false` disables), `onLog` is recorded in `logs`; every prop callback call is also kept in `propCalls`. `emitServer(id)` replaces `scanState.serverId` (and the summary) and re-renders; `emitScanState(partial)` merges into the scan state. `hide()/show()` model the `Activity` wrapper (effects disconnected while hidden, all re-created on reveal) and are IMPLEMENTED_NOT_VALIDATED.");
md.push("");
md.push("## UNKNOWN / not determined from the asset");
md.push("");
md.push("- The reply shapes and timing of every stubbed native/backend command beyond the fields read by `R` (e.g. the real `map_search` row schema, `map_data_options.scanProgress`). The harness defaults are not claims about the backend.");
md.push("- Browser behaviour of `<input type=number>` sanitisation, focus handling, React batching/StrictMode double effects and the exact `Activity` semantics: modelled, not executed in a browser.");
md.push("- Any behaviour inside the stubbed imported functions (e.g. the random per-row delay computed by `On`, the 7-field mapping of `Sn`, the `Pe` profile filtering).");
md.push("");
for (const [topic, title] of Object.entries(topics)) {
  md.push(`## Topic ${topic}: ${title}`);
  md.push("");
  md.push("| id | what it establishes | evidence state | locator (bytes; pretty lines) | executed confirmation |");
  md.push("|---|---|---|---|---|");
  for (const it of items.filter((i) => String(i.topic) === topic)) {
    const loc = it.locator;
    const asset = loc.asset.endsWith("index-BVfnK1wp.js") ? "index " : "";
    const lines = loc.prettyLineStart ? `; L${loc.prettyLineStart}${loc.prettyLineEnd !== loc.prettyLineStart ? `-${loc.prettyLineEnd}` : ""}` : "";
    md.push(`| \`${it.id}\` | ${esc(it.statement)} | ${it.evidenceState} | ${asset}${loc.byteStart}-${loc.byteEnd}${lines} | ${esc((it.confirmedBy || []).join(", ") || "-")} |`);
  }
  md.push("");
}
md.push("## Import alias table (first import line of the asset)");
md.push("");
md.push("`y = a(o())` is `__toESM(require_react())`; `E = i()` is the JSX runtime. Alias -> exported name in `index-BVfnK1wp.js` -> the minified internal declaration that `export{<internal> as <exported>}` binds -> what the harness binds. `pure:` = the REAL function extracted from the asset bytes and executed; `stub:` = inert recording stub (nothing native runs).");
md.push("");
md.push("| alias | exported | index internal | harness binding | index byte | note |");
md.push("|---|---|---|---|---|---|");
for (const r of aliasRows) md.push(`| \`${r.alias}\` | \`${r.exported}\` (${r.from.replace("./", "")}) | ${r.internal ? `\`${r.internal}\`` : "-"} | ${r.harness} | ${r.indexByteStart ?? "-"} | ${esc(r.note)} |`);
md.push("");
md.push("## State map (getState semantic names)");
md.push("");
md.push("Position in the sequence of `(0,y.useState)` declarators of `R`; `getState(name)` accepts the semantic name or the original value identifier. Extra semantic names: `tab` (= `activeTab ?? uncontrolledTab`, the original `L`), `resourceNameKey`/`monsterNameKey` (= `nameSelection.resource/.monster`), `selectedDispatchKeys`/`selectedTruckKeys` (= `Object.keys(...)` of the selections).");
md.push("");
md.push("| # | semantic | original / setter | useState declaration (exact) | byte |");
md.push("|---|---|---|---|---|");
for (const r of stateRows) md.push(`| ${r.index} | \`${r.semantic}\` | \`${r.original}\` / \`${r.setter}\` | \`${esc(r.declaration)}\` | ${r.byteStart} |`);
md.push("");
md.push("## Ref map (getRef)");
md.push("");
md.push("| # | semantic | original | declaration | byte |");
md.push("|---|---|---|---|---|");
for (const r of refRows) md.push(`| ${r.index} | \`${r.semantic}\` | \`${r.original}\` | \`${esc(r.declaration)}\` | ${r.byteStart} |`);
md.push("");
md.push("## Who writes each state (computed from Babel scope references)");
md.push("");
md.push("| state | writers (function / effect : setter call) |");
md.push("|---|---|");
for (const [sem, list] of Object.entries(stateWriters)) {
  md.push(`| \`${sem}\` | ${esc(list.map((w) => `${w.where.replace(/^R > /, "")}: \`${w.call}\``).join("; ") || "(only initial value)")} |`);
}
md.push("");
fs.writeFileSync(path.join(originalDir, "contract.md"), `${md.join("\n")}\n`);
console.log(`items=${items.length} topics=${Object.keys(topics).length} states=${stateRows.length}`);
console.log(`contract.json + contract.md written under ${originalDir}`);
