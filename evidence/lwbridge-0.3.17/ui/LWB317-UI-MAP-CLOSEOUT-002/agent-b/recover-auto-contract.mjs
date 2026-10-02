import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import {
  loadIndex,
  loadPanel,
  readAsset,
  rawOf,
  byteAt,
  walkAll,
} from "../../LWB317-UI-MAP-INTERACTIONS-001/original/lib.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const index = loadIndex();
const panel = loadPanel();
const english = readAsset("en-BisSXcTB.js");

const rawIndex = (node) => rawOf(index.entry, node);
const rawPanel = (node) => rawOf(panel.entry, node);

function all(root) {
  const out = [];
  walkAll(root, (node, parents) => out.push({ node, parents }));
  return out;
}

function one(label, rows, predicate) {
  const found = rows.filter(({ node, parents }) => predicate(node, parents));
  assert.equal(found.length, 1, `${label}: expected exactly one match, found ${found.length}`);
  return found[0].node;
}

function sourceLocator(entry, node, source, note) {
  const text = source(node);
  return {
    asset: entry.relative,
    sha256: entry.sha256,
    byteStart: byteAt(entry, node.start),
    byteEnd: byteAt(entry, node.end),
    source: text,
    note,
  };
}

function literalLocator(entry, key) {
  const marker = `${JSON.stringify(key)}:`;
  const start = entry.source.indexOf(marker);
  assert.ok(start >= 0, `English catalog key ${key}`);
  const comma = entry.source.indexOf(",", start);
  assert.ok(comma > start, `English catalog value terminator ${key}`);
  const end = comma + 1;
  return {
    asset: entry.relative,
    sha256: entry.sha256,
    byteStart: Buffer.byteLength(entry.source.slice(0, start)),
    byteEnd: Buffer.byteLength(entry.source.slice(0, end)),
    source: entry.source.slice(start, end),
  };
}

const indexNodes = all(index.ast.program);
const gi = index.functions.Gi;
assert.ok(gi, "App component Gi");
const giNodes = all(gi);
const r = panel.R;
assert.ok(r, "MapDataPanel R");
const rNodes = all(r);

function nestedFn(rows, name) {
  return one(`function ${name}`, rows, (node) => node.type === "FunctionDeclaration" && node.id?.name === name);
}

function callWith(rows, source, marker) {
  return one(`call containing ${marker}`, rows, (node) => node.type === "CallExpression" && source(node).includes(marker));
}

function jsxClass(rows, source, className) {
  return one(`jsx class ${className}`, rows, (node) => {
    if (node.type !== "CallExpression") return false;
    const callee = source(node.callee).replace(/^\(|\)$/g, "");
    if (!/(?:^|,)\w+\.jsxs?$/.test(callee)) return false;
    const props = node.arguments[1];
    if (props?.type !== "ObjectExpression") return false;
    const classProp = props.properties.find((entry) => entry.type === "ObjectProperty" && (entry.key.name || entry.key.value) === "className");
    return classProp ? source(classProp.value) === `\`${className}\`` : false;
  });
}

const autoDefaults = index.variables.Si?.declarator?.init;
const autoKinds = index.variables.xi?.declarator?.init;
assert.ok(autoDefaults && autoKinds, "Si/xi auto config declarations");

const helperNames = ["Ci", "wi", "Ti", "Ei", "Di", "Oi", "ki", "Ai", "ji"];
const helperLocators = Object.fromEntries(helperNames.map((name) => {
  const node = index.functions[name];
  assert.ok(node, `index helper ${name}`);
  return [name, sourceLocator(index.entry, node, rawIndex, {
    Ci: "parse server input",
    wi: "append parsed server IDs",
    Ti: "remove server ID",
    Ei: "normalize Auto Scan config",
    Di: "fallback target list to current server",
    Oi: "advance next-run deadline",
    ki: "Auto Scan admission predicate",
    Ai: "profile-scoped config load",
    ji: "profile-scoped normalized config save",
  }[name])];
}));

const ct = nestedFn(giNodes, "Ct");
const profileLoadEffect = one("profile Auto config layout effect", giNodes, (node) =>
  node.type === "CallExpression"
  && rawIndex(node.callee).includes("useLayoutEffect")
  && rawIndex(node).includes("Ai(r.selectedProfileId)"));
const mapPanelProducer = one("MapDataPanel producer", giNodes, (node) =>
  node.type === "CallExpression"
  && rawIndex(node.callee).replace(/^\(|\)$/g, "") === "0,M.jsx"
  && rawIndex(node.arguments[0]) === "Bi"
  && rawIndex(node).includes("autoScanConfig:ve")
  && rawIndex(node).includes("autoScanRunning:be")
  && rawIndex(node).includes("onAutoScanConfig:Ct"));

const scanModeState = one("scan mode tab state", rNodes, (node) =>
  node.type === "VariableDeclarator"
  && node.id?.type === "ArrayPattern"
  && node.id.elements[0]?.name === "Y");
const serverInputState = one("auto server input state", rNodes, (node) =>
  node.type === "VariableDeclarator"
  && node.id?.type === "ArrayPattern"
  && node.id.elements[0]?.name === "zn");
const mergeCallback = nestedFn(rNodes, "$" );
const addServersCallback = nestedFn(rNodes, "lr");
const scanModeTabs = jsxClass(rNodes, rawPanel, "map-scan-tabs");
const autoCard = jsxClass(rNodes, rawPanel, "map-auto-scan-card");
const scanTiming = jsxClass(rNodes, rawPanel, "map-scan-timing");
const scanSummary = jsxClass(rNodes, rawPanel, "map-scan-summary");
const dateFormatter = panel.topLevel.N;
assert.ok(dateFormatter, "panel date formatter N");

assert.ok(rawPanel(autoCard).includes("placeholder:C.serverId>0?String(C.serverId):`8, 15, 120`"));
assert.ok(rawPanel(autoCard).includes("disabled:oe(zn).length===0"));
assert.ok(rawPanel(autoCard).includes("x.enabled&&x.nextRunAt>0?N(x.nextRunAt,Te):`-`"));
assert.ok(!rawPanel(autoCard).includes("autoScanReset"), "original Auto card has no reset action token");
assert.ok(rawIndex(profileLoadEffect).includes("Ce.current=e,ye(e)"));
assert.ok(rawIndex(ct).includes("n.enabled&&!t.enabled&&(n.nextRunAt=Date.now())"));
assert.ok(rawIndex(ct).includes("n.enabled||(n.nextRunAt=0)"));

const contract = {
  evidenceState: "EXACT_BYTES",
  target: "LWBridge 0.3.17 Auto Scan frontend/config contract",
  assets: {
    index: { asset: index.entry.relative, sha256: index.entry.sha256 },
    panel: { asset: panel.entry.relative, sha256: panel.entry.sha256 },
    english: { asset: english.relative, sha256: english.sha256 },
  },
  index: {
    knownKinds: sourceLocator(index.entry, autoKinds, rawIndex, "known Auto Scan config kind set"),
    defaults: sourceLocator(index.entry, autoDefaults, rawIndex, "default Auto Scan config"),
    helpers: helperLocators,
    parentConfigCallback: sourceLocator(index.entry, ct, rawIndex, "App callback: normalize, enable/disable deadline, update state/ref, persist"),
    profileLoadEffect: sourceLocator(index.entry, profileLoadEffect, rawIndex, "profile change: load selected profile config into state/ref"),
    mapPanelProducer: sourceLocator(index.entry, mapPanelProducer, rawIndex, "App passes Auto config/running/callback into MapDataPanel"),
  },
  panel: {
    scanModeState: sourceLocator(panel.entry, scanModeState, rawPanel, "Manual/Auto local tab defaults to manual"),
    serverInputState: sourceLocator(panel.entry, serverInputState, rawPanel, "target-server draft input defaults empty"),
    mergeCallback: sourceLocator(panel.entry, mergeCallback, rawPanel, "MapDataPanel emits shallow-merged config edits without normalizing"),
    addServersCallback: sourceLocator(panel.entry, addServersCallback, rawPanel, "Add/Enter callback parses, appends and clears only when at least one valid ID parses"),
    dateFormatter: sourceLocator(panel.entry, dateFormatter, rawPanel, "next-run and date title locale formatter; seconds below 1e12, otherwise milliseconds"),
    scanModeTabs: sourceLocator(panel.entry, scanModeTabs, rawPanel, "Manual/Auto tab controls"),
    autoCard: sourceLocator(panel.entry, autoCard, rawPanel, "complete Auto Scan card JSX contract"),
    scanTiming: sourceLocator(panel.entry, scanTiming, rawPanel, "shared scan timing header rendered outside Manual/Auto card branch"),
    scanSummary: sourceLocator(panel.entry, scanSummary, rawPanel, "shared scan summary rendered outside Manual/Auto branch"),
  },
  englishLabels: Object.fromEntries([
    "map.manualScan",
    "map.autoScan",
    "map.enableAutoScan",
    "map.autoScanRunning",
    "map.autoScanWaiting",
    "map.autoScanDisabled",
    "map.targetServers",
    "map.targetServersHint",
    "map.scanIntervalMinutes",
    "map.returnAfterAutoScan",
    "map.runAutoScanNow",
    "map.autoScanNavigationNotice",
    "map.nextAutoScan",
  ].map((key) => [key, literalLocator(english, key)])),
};

fs.mkdirSync(here, { recursive: true });
const output = path.join(here, "source-locators.json");
fs.writeFileSync(output, `${JSON.stringify(contract, null, 2)}\n`);
console.log(`wrote ${path.relative(process.cwd(), output)}`);
console.log(`index ${index.entry.sha256}`);
console.log(`panel ${panel.entry.sha256}`);
console.log(`locators ${helperNames.length + 12 + Object.keys(contract.englishLabels).length}`);
