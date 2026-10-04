import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath, pathToFileURL } from "node:url";

import { createOriginalHarness, optionsReply } from "../LWB317-UI-MAP-INTERACTIONS-001/original/common.mjs";
import { createHarness } from "../LWB317-UI-MAP-FILTER-LIFECYCLE-001/harness.mjs";
import { byteAt, loadPanel, parse, rawOf } from "../LWB317-UI-MAP-INTERACTIONS-001/original/lib.mjs";

process.env.TZ = "Asia/Singapore";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../..");
const uiSrc = path.join(repo, "src/LWBridge.UI-0.3.17/src");
const assetsDir = path.join(repo, "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets");
const require = createRequire(path.join(repo, "src/LWBridge.UI-0.3.17/package.json"));
const React = require("react");
const { renderToStaticMarkup } = require("react-dom/server");

const sha256 = (bytes) => crypto.createHash("sha256").update(bytes).digest("hex");
const fileHash = (file) => sha256(fs.readFileSync(file));
const textHash = (text) => sha256(Buffer.from(text, "utf8"));
const rel = (file) => path.relative(repo, file).replaceAll("\\", "/");
const read = (file) => fs.readFileSync(file, "utf8");
const ensureDir = (dir) => fs.mkdirSync(dir, { recursive: true });
const writeJson = (file, value) => fs.writeFileSync(file, `${JSON.stringify(value, null, 2)}\n`);

const exe = path.resolve(repo, "../LW/lwbridge-0.3.17.exe");
const panelFile = path.join(assetsDir, "MapDataPanel-B4GXEND2.js");
const originalCssFile = path.join(assetsDir, "index-rIL9Fpht.css");
const pageFile = path.join(uiSrc, "MapDataPage.jsx");
const presentationFile = path.join(uiSrc, "mapScanPresentation.js");
const fixtureFile = path.join(uiSrc, "mapScanHeaderFixtures.js");
const referenceCssFile = path.join(uiSrc, "reference.css");
const stylesCssFile = path.join(uiSrc, "styles.css");
const mainFile = path.join(uiSrc, "main.jsx");

const expectedExe = "4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783";
const expectedPanel = "ce74345518be72e417a510b982c591729e5683f69751e190805598c4c05d3089";
const expectedCss = "3d87e9f65b39eace6a1a254bfc90a38acb72613d1fff7236c7cee7ab9bfaf545";
assert.equal(fileHash(exe), expectedExe, "reference EXE hash");
assert.equal(fileHash(panelFile), expectedPanel, "original MapDataPanel hash");
assert.equal(fileHash(originalCssFile), expectedCss, "original CSS hash");
assert.equal(fileHash(referenceCssFile), expectedCss, "current reference.css must remain exact recovered CSS");

const mainSource = read(mainFile);
assert.ok(
  mainSource.indexOf('import "./reference.css";') < mainSource.indexOf('import "./styles.css";'),
  "current main.jsx CSS import order",
);

const pageSource = read(pageFile);
const originalCss = read(originalCssFile);
const currentCss = `${read(referenceCssFile)}\n${read(stylesCssFile)}`;
const rawDir = path.join(here, "raw");
const generatedDir = path.join(here, "generated");
ensureDir(rawDir);
ensureDir(generatedDir);

const NOW = Date.UTC(2026, 9, 4, 6, 30, 0);
const START = NOW - 65_000;
const KINDS = ["city", "resource", "monster", "truck", "railway", "dispatch", "ghost", "treasure"];
const emptyCounts = Object.fromEntries(KINDS.map((key) => [key, 0]));

function baseScan(overrides = {}) {
  return {
    serverId: 321,
    serverIdSource: "fixture",
    scanRunId: "visual-run",
    isReading: false,
    phase: "idle",
    selectedTypes: [...KINDS],
    totalBlocks: 100,
    completedBlocks: 0,
    readBlocks: 0,
    failedBlocks: 0,
    unreadBlocks: 100,
    inflightBlocks: 0,
    scanMode: "normal",
    concurrency: 8,
    scanRate: 0,
    progressPercent: 0,
    lastError: null,
    startedAt: 0,
    updatedAt: 0,
    ...overrides,
  };
}

const coreCases = [
  {
    id: "idle-unavailable",
    absent: true,
    online: false,
    backendAvailable: false,
    serverId: 0,
    speed: "normal",
  },
  {
    id: "idle-server",
    online: true,
    backendAvailable: true,
    serverId: 321,
    speed: "fast",
    scan: baseScan({ selectedTypes: ["city"], scanMode: "fast" }),
  },
  {
    id: "reading-low",
    online: true,
    backendAvailable: true,
    serverId: 321,
    speed: "normal",
    scan: baseScan({
      isReading: true,
      phase: "reading",
      progressPercent: 37.75,
      completedBlocks: 38,
      readBlocks: 38,
      unreadBlocks: 62,
      startedAt: START,
    }),
  },
  {
    id: "reading-high",
    online: true,
    backendAvailable: true,
    serverId: 321,
    speed: "fast",
    scan: baseScan({
      isReading: true,
      phase: "reading",
      scanMode: "fast",
      concurrency: 20,
      progressPercent: 72.5,
      completedBlocks: 73,
      readBlocks: 73,
      unreadBlocks: 27,
      startedAt: START,
    }),
  },
  {
    id: "completed-timing",
    online: true,
    backendAvailable: true,
    serverId: 321,
    speed: "normal",
    scan: baseScan({
      phase: "completed",
      progressPercent: 100,
      completedBlocks: 100,
      readBlocks: 100,
      unreadBlocks: 0,
    }),
    stored: {
      id: "visual-run",
      serverId: 321,
      status: "completed",
      createdAt: START,
      updatedAt: NOW - 1_000,
      error: null,
    },
  },
  {
    id: "error",
    online: true,
    backendAvailable: true,
    serverId: 321,
    speed: "normal",
    scan: baseScan({
      phase: "stopped",
      progressPercent: 49.5,
      completedBlocks: 50,
      readBlocks: 50,
      unreadBlocks: 50,
      startedAt: START,
      updatedAt: NOW - 2_000,
      lastError: "fixture scan error",
    }),
  },
];

const providerCases = [
  {
    id: "provider-backend-unavailable-with-server",
    online: true,
    backendAvailable: false,
    serverId: 321,
    speed: "normal",
    scan: baseScan(),
  },
  {
    id: "provider-auto-running-idle",
    online: true,
    backendAvailable: true,
    autoRunning: true,
    serverId: 321,
    speed: "normal",
    scan: baseScan(),
  },
  {
    id: "provider-backend-unavailable-reading",
    online: true,
    backendAvailable: false,
    serverId: 321,
    speed: "normal",
    scan: baseScan({ isReading: true, phase: "reading", progressPercent: 63, startedAt: START }),
  },
];

const browserPairs = [
  { id: "en-light-idle-unavailable", language: "en", theme: "light", caseId: "idle-unavailable", width: 1280, height: 720 },
  { id: "en-dark-reading-high", language: "en", theme: "dark", caseId: "reading-high", width: 375, height: 1000 },
  { id: "ja-light-completed-timing", language: "ja", theme: "light", caseId: "completed-timing", width: 1280, height: 720 },
  { id: "ja-dark-error", language: "ja", theme: "dark", caseId: "error", width: 375, height: 1000 },
];

const catalogs = {};
for (const language of ["en", "ja"]) {
  const file = path.join(uiSrc, `locales/${language}.js`);
  catalogs[language] = {
    file,
    hash: fileHash(file),
    messages: (await import(pathToFileURL(file).href)).default,
  };
}

function translator(language) {
  const messages = catalogs[language].messages;
  return (key, values = {}) => (messages[key] || key).replace(/\{(\w+)\}/g, (match, name) => String(values[name] ?? match));
}

function topChildren(node) {
  const out = [];
  const visit = (value) => {
    if (Array.isArray(value)) return value.forEach(visit);
    if (value == null || typeof value === "boolean") return;
    if (value?.type === "Fragment") return visit(value.props?.children);
    out.push(value);
  };
  visit(node?.props?.children);
  return out;
}

function scopeTree(root) {
  assert.equal(root?.type, "section", "Map root must be a section");
  const children = topChildren(root);
  const end = children.findIndex((child) => child?.props?.className === "map-search");
  assert.ok(end >= 0, "map-search boundary must exist");
  return {
    ...root,
    props: {
      ...root.props,
      children: children.slice(0, end),
    },
  };
}

function textOf(node) {
  if (node == null || typeof node === "boolean") return "";
  if (Array.isArray(node)) return node.map(textOf).join("");
  if (typeof node === "object") return textOf(node.props?.children);
  return String(node);
}

function nativeNodes(node, output = []) {
  if (Array.isArray(node)) {
    node.forEach((child) => nativeNodes(child, output));
    return output;
  }
  if (node == null || typeof node === "boolean" || typeof node !== "object") return output;
  if (node.type === "Fragment") {
    nativeNodes(node.props?.children, output);
    return output;
  }
  if (typeof node.type === "string") output.push(node);
  nativeNodes(node.props?.children, output);
  return output;
}

function classHas(node, className) {
  return typeof node?.props?.className === "string"
    && node.props.className.split(/\s+/).filter(Boolean).includes(className);
}

function findClass(root, className) {
  return nativeNodes(root).find((node) => classHas(node, className));
}

function attr(node, name) {
  return node?.props?.[name] === undefined ? null : node.props[name];
}

function presentationalView(root) {
  const nodes = nativeNodes(root);
  const tabs = findClass(root, "map-scan-tabs");
  const header = findClass(root, "map-header");
  const actions = findClass(root, "map-actions");
  const timing = findClass(root, "map-scan-timing");
  const speed = findClass(root, "map-speed-toggle");
  const summary = findClass(root, "map-scan-summary");
  const error = findClass(root, "map-scan-error");
  const controls = nodes.filter((node) => classHas(node, "map-controls")).at(-1);
  const typeGroup = controls ? nativeNodes(controls).find((node) => classHas(node, "map-types--compact")) : null;
  const actionButtons = actions ? nativeNodes(actions).filter((node) => node.type === "button") : [];
  const typeLabels = typeGroup ? nativeNodes(typeGroup).filter((node) => node.type === "label") : [];

  return {
    root: {
      className: attr(root, "className"),
      bridgeMode: attr(root, "data-bridge-mode"),
      previewFixture: attr(root, "data-preview-fixture"),
    },
    scanTabs: tabs ? nativeNodes(tabs).filter((node) => node.type === "button").map((node) => ({
      text: textOf(node),
      type: attr(node, "type"),
      role: attr(node, "role"),
      className: attr(node, "className"),
      ariaSelected: attr(node, "aria-selected"),
    })) : null,
    header: {
      title: header ? textOf(nativeNodes(header).find((node) => node.type === "h2")) : null,
      timing: timing ? {
        text: textOf(timing),
        spans: nativeNodes(timing).filter((node) => node.type === "span").map((node) => ({
          className: attr(node, "className"),
          title: attr(node, "title"),
          text: textOf(node),
        })),
        times: nativeNodes(timing).filter((node) => node.type === "time").map((node) => ({
          dateTime: attr(node, "dateTime"),
          text: textOf(node),
        })),
      } : null,
      speed: speed ? {
        className: attr(speed, "className"),
        disabled: attr(speed, "disabled"),
        ariaLabel: attr(speed, "aria-label"),
        radios: nativeNodes(speed).filter((node) => node.type === "input").map((node) => ({
          type: attr(node, "type"),
          name: attr(node, "name"),
          checked: attr(node, "checked"),
        })),
      } : null,
      actions: actionButtons.map((node) => ({
        text: textOf(node),
        type: attr(node, "type"),
        className: attr(node, "className"),
        disabled: attr(node, "disabled"),
      })),
    },
    summary: summary ? {
      text: textOf(summary),
      status: textOf(findClass(summary, "map-status-pill")),
      statusClassName: attr(findClass(summary, "map-status-pill"), "className"),
      progressClassName: attr(findClass(summary, "map-progress"), "className"),
      progress: (() => {
        const progress = nativeNodes(summary).find((node) => node.type === "progress");
        return progress ? {
          max: attr(progress, "max"),
          value: attr(progress, "value"),
          ariaLabel: attr(progress, "aria-label"),
        } : null;
      })(),
    } : null,
    error: error ? {
      role: attr(error, "role"),
      text: textOf(error),
    } : null,
    scanTypes: typeLabels.map((label) => {
      const labelNodes = nativeNodes(label);
      const input = labelNodes.find((node) => node.type === "input");
      return {
        text: textOf(label),
        labelClassName: attr(label, "className"),
        childElementTypes: topChildren(label).filter((child) => typeof child === "object").map((child) => child.type),
        checked: attr(input, "checked"),
        disabled: attr(input, "disabled"),
      };
    }),
  };
}

function diffValues(expected, actual, at = "$", output = []) {
  if (Object.is(expected, actual)) return output;
  if (Array.isArray(expected) || Array.isArray(actual)) {
    if (!Array.isArray(expected) || !Array.isArray(actual)) {
      output.push({ path: at, original: expected, current: actual });
      return output;
    }
    const count = Math.max(expected.length, actual.length);
    for (let i = 0; i < count; i += 1) diffValues(expected[i], actual[i], `${at}[${i}]`, output);
    return output;
  }
  const expectedObject = expected && typeof expected === "object";
  const actualObject = actual && typeof actual === "object";
  if (expectedObject || actualObject) {
    if (!expectedObject || !actualObject) {
      output.push({ path: at, original: expected, current: actual });
      return output;
    }
    for (const key of new Set([...Object.keys(expected), ...Object.keys(actual)])) {
      diffValues(expected[key], actual[key], `${at}.${key}`, output);
    }
    return output;
  }
  output.push({ path: at, original: expected, current: actual });
  return output;
}

function toReact(node, key = "root") {
  if (node == null || typeof node === "boolean") return null;
  if (Array.isArray(node)) return node.map((child, index) => toReact(child, `${key}-${index}`));
  if (typeof node !== "object") return node;
  const children = topChildren(node).map((child, index) => toReact(child, `${key}-${index}`));
  if (node.type === "Fragment") return React.createElement(React.Fragment, { key }, ...children);
  assert.equal(typeof node.type, "string", `scoped element must be native, got ${typeof node.type}`);
  const props = { key };
  for (const [name, value] of Object.entries(node.props || {})) {
    if (name === "children") continue;
    props[name] = value;
  }
  return React.createElement(node.type, props, ...children);
}

function renderMarkup(root) {
  return renderToStaticMarkup(toReact(root));
}

function makeDocument({ language, theme, title, markup, css }) {
  return `<!doctype html><html lang="${language}" data-theme="${theme}"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>${title}</title><style>${css}</style></head><body>${markup}</body></html>\n`;
}

function summary(scan) {
  return scan ? { serverId: scan.serverId, counts: { ...emptyCounts }, scanState: scan } : null;
}

async function bootOriginal(spec, language) {
  const t = translator(language);
  const scan = spec.scan || baseScan({ serverId: spec.serverId || 0, serverIdSource: spec.serverId ? "fixture" : "none" });
  const stored = spec.stored || null;
  const h = await createOriginalHarness({
    online: spec.online,
    now: NOW,
    language,
    translate: t,
    serverId: spec.serverId,
    scanState: scan,
    summary: spec.absent ? null : summary(scan),
    autoScanRunning: spec.autoRunning === true,
    storage: { "lwbridge.mapScanMode": spec.speed || "normal" },
    stubs: {
      dataOptions: { mode: "auto", value: optionsReply({ scanProgress: stored }) },
    },
  });
  await h.mount();
  if (spec.absent) await h.setProps({ scanState: undefined, summary: null });
  return h;
}

async function bootCurrent(spec, language) {
  const t = translator(language);
  const scan = spec.scan || baseScan({ serverId: spec.serverId || 0, serverIdSource: spec.serverId ? "fixture" : "none" });
  const h = await createHarness(pageSource, `manual-visual-${spec.id}-${language}`, {
    now: NOW,
    language,
    serverId: spec.serverId,
    translate: t,
    storage: { "lwbridge.mapScanMode": spec.speed || "normal" },
    props: {
      bridgeMode: "native",
      backendAvailable: spec.backendAvailable,
      online: spec.online,
      currentServerId: spec.serverId,
      previewState: "",
      activeTab: "city",
      onActiveTabChange: () => {},
      scanState: spec.absent ? null : scan,
      summary: spec.absent ? null : summary(scan),
      onState: spec.absent ? null : () => {},
      onCounts: () => {},
      autoScanRunning: spec.autoRunning === true,
    },
    dataOptions: optionsReply({ scanProgress: spec.stored || null })(spec.serverId || 0),
  });
  await h.mount();
  return h;
}

async function executeCase(spec, language) {
  const original = await bootOriginal(spec, language);
  const current = await bootCurrent(spec, language);
  try {
    const originalScope = scopeTree(original.tree());
    const currentScope = scopeTree(current.tree());
    const originalView = presentationalView(originalScope);
    const currentView = presentationalView(currentScope);
    const differences = diffValues(originalView, currentView);
    const originalMarkup = renderMarkup(originalScope);
    const currentMarkup = renderMarkup(currentScope);
    const baseName = `${language}-${spec.id}`;
    fs.writeFileSync(path.join(rawDir, `${baseName}-original.html`), `${originalMarkup}\n`);
    fs.writeFileSync(path.join(rawDir, `${baseName}-current.html`), `${currentMarkup}\n`);
    return {
      language,
      caseId: spec.id,
      input: {
        absent: spec.absent === true,
        online: spec.online,
        backendAvailable: spec.backendAvailable,
        autoRunning: spec.autoRunning === true,
        serverId: spec.serverId,
        speed: spec.speed,
        scan: spec.absent ? null : spec.scan,
        stored: spec.stored || null,
        now: NOW,
      },
      originalView,
      currentView,
      differences,
      exactViewMatch: differences.length === 0,
      originalMarkupSha256: textHash(originalMarkup),
      currentMarkupSha256: textHash(currentMarkup),
      exactMarkupMatch: originalMarkup === currentMarkup,
      rawFiles: {
        original: rel(path.join(rawDir, `${baseName}-original.html`)),
        current: rel(path.join(rawDir, `${baseName}-current.html`)),
      },
      originalMarkup,
      currentMarkup,
    };
  } finally {
    await original.unmount();
    await current.unmount();
  }
}

function lineAt(source, index) {
  return source.slice(0, index).split(/\r?\n/).length;
}

function needleLocator(source, file, needle, id) {
  const index = source.indexOf(needle);
  assert.ok(index >= 0, `${id}: source needle`);
  assert.equal(source.indexOf(needle, index + 1), -1, `${id}: source needle must be unique`);
  return {
    id,
    file: rel(file),
    utf8ByteOffset: Buffer.byteLength(source.slice(0, index), "utf8"),
    byteLength: Buffer.byteLength(needle, "utf8"),
    sha256: textHash(needle),
    line: lineAt(source, index),
    text: needle,
  };
}

function astLocator(entry, node, name) {
  const text = rawOf(entry, node);
  return {
    name,
    file: entry.relative,
    utf8ByteOffset: byteAt(entry, node.start),
    byteLength: Buffer.byteLength(text, "utf8"),
    sha256: textHash(text),
  };
}

const panel = loadPanel();
const originalReturn = panel.R.body.body.find((node) => node.type === "ReturnStatement");
assert.ok(originalReturn, "original R return statement");
const ieDeclarator = panel.ast.program.body
  .filter((node) => node.type === "VariableDeclaration")
  .flatMap((node) => node.declarations)
  .find((node) => node.id?.name === "Ie");
assert.ok(ieDeclarator, "original Ie scan type declaration");
const originalLocators = [
  astLocator(panel.entry, panel.R, "MapDataPanel R"),
  astLocator(panel.entry, originalReturn, "MapDataPanel R final return"),
  astLocator(panel.entry, ieDeclarator, "Ie scan types"),
  ...["N", "Je", "Ye", "Xe", "Ze", "P"].map((name) => astLocator(panel.entry, panel.topLevel[name], `${name} timing helper`)),
  needleLocator(panel.entry.source, panelFile, 'className:`panel map-panel`', "original-map-panel-root"),
  needleLocator(panel.entry.source, panelFile, 'className:`map-speed-toggle${F===`fast`?` fast`:``}', "original-speed-toggle"),
  needleLocator(panel.entry.source, panelFile, '(0,E.jsx)(`button`,{className:C.isReading?``:`primary`,onClick:$n,disabled:C.isReading,children:S(`map.startReading`)})', "original-start-button"),
  needleLocator(panel.entry.source, panelFile, '(0,E.jsx)(`button`,{className:C.isReading?`danger`:``,onClick:er,disabled:!C.isReading,children:S(`common.stop`)})', "original-stop-button"),
  needleLocator(panel.entry.source, panelFile, '(0,E.jsx)(`button`,{onClick:tr,disabled:!R||C.isReading,children:S(`map.clearServer`)})', "original-clear-button"),
  needleLocator(panel.entry.source, panelFile, 'disabled:C.isReading,children:S(`map.startReading`)', "original-start-disabled"),
  needleLocator(panel.entry.source, panelFile, 'disabled:!C.isReading,children:S(`common.stop`)', "original-stop-disabled"),
  needleLocator(panel.entry.source, panelFile, 'disabled:!R||C.isReading,children:S(`map.clearServer`)', "original-clear-disabled"),
  needleLocator(panel.entry.source, panelFile, 'className:e.enabled?``:`disabled`', "original-scan-type-label-class"),
  needleLocator(panel.entry.source, panelFile, 'type:`checkbox`,disabled:!e.enabled,checked:Ke.includes(e.key)', "original-scan-type-input"),
];

const pageAst = parse(pageSource, { sourceType: "module", plugins: ["jsx"] });
const pageExport = pageAst.program.body.find((node) => node.type === "ExportNamedDeclaration" && node.declaration?.id?.name === "MapDataPage");
assert.ok(pageExport, "current MapDataPage function");
const pageFn = pageExport.declaration;
const currentReturn = pageFn.body.body.find((node) => node.type === "ReturnStatement");
assert.ok(currentReturn, "current MapDataPage final return");
const currentAstLocator = (node, name) => {
  const text = pageSource.slice(node.start, node.end);
  return {
    name,
    file: rel(pageFile),
    utf8ByteOffset: Buffer.byteLength(pageSource.slice(0, node.start), "utf8"),
    byteLength: Buffer.byteLength(text, "utf8"),
    sha256: textHash(text),
    line: lineAt(pageSource, node.start),
  };
};
const currentLocators = [
  currentAstLocator(pageFn, "MapDataPage"),
  currentAstLocator(currentReturn, "MapDataPage final return"),
  needleLocator(pageSource, pageFile, 'data-bridge-mode={bridgeMode}', "current-map-panel-bridge-mode"),
  needleLocator(pageSource, pageFile, 'disabled={scanState.isReading || autoRunning}', "current-speed-toggle"),
  needleLocator(pageSource, pageFile, '<button type="button" className={!scanState.isReading ? "primary" : ""} disabled={!online || scanState.isReading || autoRunning} onClick={startScan}>{t("map.startReading")}</button>', "current-start-button"),
  needleLocator(pageSource, pageFile, '<button type="button" className={scanState.isReading ? "danger" : ""} disabled={!backendAvailable || !scanState.isReading} onClick={stopScan}>{t("common.stop")}</button>', "current-stop-button"),
  needleLocator(pageSource, pageFile, '<button type="button" disabled={!backendAvailable || !dataServerId || scanState.isReading || autoRunning} onClick={clearData}>{t("map.clearServer")}</button>', "current-clear-button"),
  needleLocator(pageSource, pageFile, 'disabled={!online || scanState.isReading || autoRunning}', "current-start-disabled"),
  needleLocator(pageSource, pageFile, 'disabled={!backendAvailable || !scanState.isReading}', "current-stop-disabled"),
  needleLocator(pageSource, pageFile, 'disabled={!backendAvailable || !dataServerId || scanState.isReading || autoRunning}', "current-clear-disabled"),
  needleLocator(pageSource, pageFile, 'disabled={selectedTypes.length === 1 && selectedTypes[0] === key}', "current-scan-type-input"),
  needleLocator(pageSource, pageFile, '<span>{t(SCAN_TYPE_LABEL_KEYS[key])}</span>', "current-scan-type-label-span"),
];

const coreResults = [];
const executed = new Map();
for (const language of ["en", "ja"]) {
  for (const spec of coreCases) {
    const result = await executeCase(spec, language);
    coreResults.push(result);
    executed.set(`${language}/${spec.id}`, result);
  }
}
assert.equal(coreResults.length, 12, "six state families x two languages");

const providerResults = [];
for (const spec of providerCases) providerResults.push(await executeCase(spec, "en"));

for (const pair of browserPairs) {
  const result = executed.get(`${pair.language}/${pair.caseId}`);
  assert.ok(result, `browser source result ${pair.id}`);
  const originalDoc = makeDocument({
    language: pair.language,
    theme: pair.theme,
    title: `${pair.id} original`,
    markup: result.originalMarkup,
    css: originalCss,
  });
  const currentDoc = makeDocument({
    language: pair.language,
    theme: pair.theme,
    title: `${pair.id} current`,
    markup: result.currentMarkup,
    css: currentCss,
  });
  fs.writeFileSync(path.join(generatedDir, `${pair.id}-original.html`), originalDoc);
  fs.writeFileSync(path.join(generatedDir, `${pair.id}-current.html`), currentDoc);
}

const relevantSelectors = [
  ".map-scan-tabs",
  ".map-header",
  ".map-actions",
  ".map-scan-timing",
  ".map-speed-toggle",
  ".map-scan-summary",
  ".map-status-pill",
  ".map-scan-error",
  ".map-progress",
  ".map-controls",
  ".map-types",
  ".map-types--compact",
];
assert.ok(relevantSelectors.every((selector) => originalCss.includes(selector)), "original relevant CSS selectors");
assert.ok(relevantSelectors.every((selector) => read(referenceCssFile).includes(selector)), "current relevant CSS selectors");
assert.ok(relevantSelectors.every((selector) => !read(stylesCssFile).includes(selector)), "styles.css has no scoped overrides");

const pinnedInputs = {
  task: "LWB317-UI-MAP-MANUAL-VISUAL-001",
  generatedAt: "2026-10-04",
  reference: {
    exe: { path: exe, sha256: fileHash(exe) },
    panel: { path: rel(panelFile), sha256: fileHash(panelFile), bytes: fs.statSync(panelFile).size },
    css: { path: rel(originalCssFile), sha256: fileHash(originalCssFile), bytes: fs.statSync(originalCssFile).size },
    locators: originalLocators,
  },
  current: {
    files: [pageFile, presentationFile, fixtureFile, referenceCssFile, stylesCssFile, mainFile].map((file) => ({
      path: rel(file),
      sha256: fileHash(file),
      bytes: fs.statSync(file).size,
    })),
    locales: Object.fromEntries(Object.entries(catalogs).map(([language, item]) => [language, { path: rel(item.file), sha256: item.hash }])),
    cssImportOrder: ["./reference.css", "./styles.css"],
    referenceCssExactlyMatchesOriginal: fileHash(referenceCssFile) === fileHash(originalCssFile),
    scopedStylesOverrides: [],
    locators: currentLocators,
  },
  harness: {
    timezone: process.env.TZ,
    now: NOW,
    clockIso: new Date(NOW).toISOString(),
    translation: "Current exact-recovered en/ja catalogs are injected into both executed renderers so text inputs are identical.",
    hooks: "Existing persistent hook runtimes execute the unmodified original R body and actual current MapDataPage function.",
    surroundingContainer: "Browser artifacts render the executed scoped map-panel directly under body; the wider application shell/navigation is intentionally excluded.",
    unavailableProducers: "All backend/native-facing original imports and current mapApi calls are inert harness stubs; no action handler is invoked.",
  },
  browserPairs,
};
writeJson(path.join(here, "pinned-inputs.json"), pinnedInputs);

const results = {
  marker: "LWB317_MAP_MANUAL_VISUAL_RENDERERS_EXECUTED",
  clock: { now: NOW, iso: new Date(NOW).toISOString(), timezone: process.env.TZ },
  css: {
    originalSha256: fileHash(originalCssFile),
    currentReferenceSha256: fileHash(referenceCssFile),
    currentStylesSha256: fileHash(stylesCssFile),
    exactRecoveredReferenceCss: fileHash(originalCssFile) === fileHash(referenceCssFile),
    currentMainImportOrder: ["reference.css", "styles.css"],
    scopedStylesOverrides: [],
  },
  coreCaseCount: coreResults.length,
  coreResults: coreResults.map(({ originalMarkup, currentMarkup, ...result }) => result),
  providerFenceCaseCount: providerResults.length,
  providerFenceResults: providerResults.map(({ originalMarkup, currentMarkup, ...result }) => result),
  browserPairs,
};
writeJson(path.join(here, "render-results.json"), results);

console.log(JSON.stringify({
  marker: results.marker,
  coreCases: results.coreCaseCount,
  coreExactViews: results.coreResults.filter((item) => item.exactViewMatch).length,
  coreExactMarkup: results.coreResults.filter((item) => item.exactMarkupMatch).length,
  providerCases: results.providerFenceCaseCount,
  cssExact: results.css.exactRecoveredReferenceCss,
  browserPairs: browserPairs.length,
}));
