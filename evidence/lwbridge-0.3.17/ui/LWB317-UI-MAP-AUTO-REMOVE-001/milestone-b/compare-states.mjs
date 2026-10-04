import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath, pathToFileURL } from "node:url";

import { createOriginalHarness, optionsReply } from "../../LWB317-UI-MAP-INTERACTIONS-001/original/common.mjs";
import { byteAt, loadIndex, rawOf } from "../../LWB317-UI-MAP-INTERACTIONS-001/original/lib.mjs";
import { createHarness, nodeText, treeNodes } from "../../LWB317-UI-MAP-FILTER-LIFECYCLE-001/harness.mjs";

process.env.TZ = "Asia/Singapore";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const uiSrc = path.join(repo, "src/LWBridge.UI-0.3.17/src");
const assetsDir = path.join(repo, "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets");
const require = createRequire(path.join(repo, "src/LWBridge.UI-0.3.17/package.json"));
const React = require("react");
const { renderToStaticMarkup } = require("react-dom/server");
const { parse } = require("@babel/parser");

const sha256 = (bytes) => crypto.createHash("sha256").update(bytes).digest("hex");
const fileHash = (file) => sha256(fs.readFileSync(file));
const textHash = (text) => sha256(Buffer.from(text, "utf8"));
const rel = (file) => path.relative(repo, file).replaceAll("\\", "/");
const read = (file) => fs.readFileSync(file, "utf8");
const clone = (value) => JSON.parse(JSON.stringify(value));
const writeJson = (file, value) => fs.writeFileSync(file, `${JSON.stringify(value, null, 2)}\n`);
const ensureDir = (dir) => fs.mkdirSync(dir, { recursive: true });

const exe = path.resolve(repo, "../LW/lwbridge-0.3.17.exe");
const panelFile = path.join(assetsDir, "MapDataPanel-B4GXEND2.js");
const indexFile = path.join(assetsDir, "index-BVfnK1wp.js");
const originalCssFile = path.join(assetsDir, "index-rIL9Fpht.css");
const pageFile = path.join(uiSrc, "MapDataPage.jsx");
const autoConfigFile = path.join(uiSrc, "mapAutoConfig.js");
const mapBackendFile = path.join(uiSrc, "mapBackend.js");
const referenceCssFile = path.join(uiSrc, "reference.css");
const stylesCssFile = path.join(uiSrc, "styles.css");
const mainFile = path.join(uiSrc, "main.jsx");
const originalRuntimeFile = path.join(repo, "evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/original/original-runtime.mjs");
const currentHarnessFile = path.join(repo, "evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-FILTER-LIFECYCLE-001/harness.mjs");
const locatorFile = path.join(repo, "evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-AUTO-CONFIG-001/original-source-locators.json");

const expectedExe = "4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783";
const expectedPanel = "ce74345518be72e417a510b982c591729e5683f69751e190805598c4c05d3089";
const expectedIndex = "44c4e4043825b7db296b64171951b27176f8850ff8d7d337cc991df4765524c6";
const expectedCss = "3d87e9f65b39eace6a1a254bfc90a38acb72613d1fff7236c7cee7ab9bfaf545";
assert.equal(fileHash(exe), expectedExe, "reference EXE hash");
assert.equal(fileHash(panelFile), expectedPanel, "original MapDataPanel hash");
assert.equal(fileHash(indexFile), expectedIndex, "original index hash");
assert.equal(fileHash(originalCssFile), expectedCss, "original CSS hash");
assert.equal(fileHash(referenceCssFile), expectedCss, "current reference.css must remain exact recovered CSS");

const sourceLocators = JSON.parse(read(locatorFile));
assert.equal(sourceLocators.assets.panel.sha256, expectedPanel, "AUTO-CONFIG panel pin");
assert.equal(sourceLocators.assets.index.sha256, expectedIndex, "AUTO-CONFIG index pin");
const autoCardLocator = sourceLocators.panel.autoCard;
const panelBytes = fs.readFileSync(panelFile);
const autoCardSlice = panelBytes.subarray(autoCardLocator.byteStart, autoCardLocator.byteEnd);
assert.equal(autoCardSlice.toString("utf8"), autoCardLocator.source, "AUTO-CONFIG original card byte slice");
const panelSource = read(panelFile);
const originalRemoveUse = needleLocator(panelSource, panelFile, 'children:(0,E.jsx)(se,{name:`remove`})', "original-server-chip-remove-use");
const originalTypeList = needleLocator(panelSource, panelFile, 'Ie=[{key:`city`,label:`map.playerCity`,enabled:!0},{key:`resource`,label:`map.resourcePoint`,enabled:!0},{key:`monster`,label:`map.monster`,enabled:!0},{key:`truck`,label:`map.truck`,enabled:!0},{key:`railway`,label:`map.allianceTrain`,enabled:!0},{key:`dispatch`,label:`map.secretTask`,enabled:!0},{key:`ghost`,label:`map.ghostScout`,enabled:!0},{key:`treasure`,label:`map.treasure`,enabled:!0}]', "original-auto-type-list");
const originalLastTypePredicate = needleLocator(panelSource, panelFile, 'disabled:x.selectedTypes.length===1&&x.selectedTypes[0]===e.key', "original-last-type-disabled-predicate");
const originalRunNowPredicate = needleLocator(panelSource, panelFile, 'disabled:!l||!x.enabled||Ce||C.isReading', "original-run-now-disabled-predicate");

const knownKinds = [...new Function(`return (${sourceLocators.index.knownKinds.source});`)()];
const originalDefault = new Function(`return (${sourceLocators.index.defaults.source});`)();
const autoModule = await import(pathToFileURL(autoConfigFile).href);
assert.deepEqual(clone(autoModule.DEFAULT_AUTO_SCAN_CONFIG), clone(originalDefault), "current Auto defaults match recovered defaults");

const NOW = Date.UTC(2026, 9, 4, 9, 15, 0);
const configured = {
  enabled: true,
  intervalMinutes: 90,
  serverIds: [8, 15, 120],
  selectedTypes: ["city", "truck", "treasure"],
  scanMode: "normal",
  returnToOriginalServer: false,
  nextRunAt: NOW + 90 * 60_000,
};
assert.deepEqual(autoModule.normalizeAutoScanConfig(configured), configured, "configured sample is source-valid/current-normalized");
assert.ok(configured.serverIds.every((id) => Number.isInteger(id) && id >= 1 && id <= 99999), "configured server IDs satisfy recovered parser");
assert.ok(configured.selectedTypes.every((kind) => knownKinds.includes(kind)), "configured selected types satisfy recovered kind set");

const catalogs = {};
for (const language of ["en", "ja"]) {
  const file = path.join(uiSrc, `locales/${language}.js`);
  catalogs[language] = { file, hash: fileHash(file), messages: (await import(pathToFileURL(file).href)).default };
}
function translator(language) {
  const messages = catalogs[language].messages;
  return (key, values = {}) => (messages[key] || key).replace(/\{(\w+)\}/g, (match, name) => String(values[name] ?? match));
}

const idleScanState = {
  serverId: 321,
  serverIdSource: "fixture",
  scanRunId: "",
  isReading: false,
  phase: "idle",
  selectedTypes: [...knownKinds],
  totalBlocks: 0,
  completedBlocks: 0,
  readBlocks: 0,
  failedBlocks: 0,
  unreadBlocks: 0,
  inflightBlocks: 0,
  scanMode: "normal",
  concurrency: 8,
  scanRate: 0,
  progressPercent: 0,
  lastError: null,
  startedAt: 0,
  updatedAt: 0,
};
const emptyCounts = Object.fromEntries(knownKinds.map((kind) => [kind, 0]));
const oneTypeConfig = { ...clone(configured), selectedTypes: ["treasure"] };
assert.deepEqual(autoModule.normalizeAutoScanConfig(oneTypeConfig), oneTypeConfig, "one-type sample remains source-valid/current-normalized");
const stateTemplates = [
  { id: "default", config: clone(originalDefault), online: true, autoScanRunning: false, scanState: clone(idleScanState) },
  { id: "configured-waiting", config: clone(configured), online: true, autoScanRunning: false, scanState: clone(idleScanState) },
  { id: "auto-running", config: clone(configured), online: true, autoScanRunning: true, scanState: clone(idleScanState) },
  { id: "manual-reading", config: clone(configured), online: true, autoScanRunning: false, scanState: { ...clone(idleScanState), isReading: true, phase: "reading", scanRunId: "manual-fixture" } },
  { id: "offline", config: clone(configured), online: false, autoScanRunning: false, scanState: clone(idleScanState) },
  { id: "one-remaining-type", config: oneTypeConfig, online: true, autoScanRunning: false, scanState: clone(idleScanState) },
];
const specs = ["en", "ja"].flatMap((language) => stateTemplates.map((state) => ({
  ...clone(state),
  language,
  theme: language === "en" ? "light" : "dark",
  width: language === "en" ? 1280 : 375,
  height: language === "en" ? 720 : 1000,
})));

const index = loadIndex();
const recoveredIconNode = index.functions.Vr;
assert.ok(recoveredIconNode, "recovered Vr icon component");
const recoveredIconSource = rawOf(index.entry, recoveredIconNode);
const recoveredJsxRuntime = {
  Fragment: "Fragment",
  jsx: (type, props) => ({ type, props: { ...(props || {}) } }),
  jsxs: (type, props) => ({ type, props: { ...(props || {}) } }),
};
const RecoveredIcon = new Function("M", `${recoveredIconSource}\nreturn Vr;`)(recoveredJsxRuntime);

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
function nativeNodes(node, output = []) {
  if (Array.isArray(node)) { node.forEach((child) => nativeNodes(child, output)); return output; }
  if (node == null || typeof node === "boolean" || typeof node !== "object") return output;
  if (node.type === "Fragment") { nativeNodes(node.props?.children, output); return output; }
  if (typeof node.type === "string") output.push(node);
  nativeNodes(node.props?.children, output);
  return output;
}
function classHas(node, className) {
  return typeof node?.props?.className === "string" && node.props.className.split(/\s+/).filter(Boolean).includes(className);
}
function findClass(root, className) {
  return treeNodes(root).find((node) => classHas(node, className));
}
function expandOriginalDependencies(node) {
  if (Array.isArray(node)) return node.map(expandOriginalDependencies);
  if (node == null || typeof node === "boolean" || typeof node !== "object") return node;
  if (typeof node.type === "function") {
    assert.equal(node.type.name, "Icon", `unexpected original function dependency ${node.type.name}`);
    return expandOriginalDependencies(RecoveredIcon(node.props || {}));
  }
  return {
    ...node,
    props: { ...(node.props || {}), children: expandOriginalDependencies(node.props?.children) },
  };
}
function scopedCard(card) {
  return { type: "section", props: { className: "panel map-panel", children: [card] } };
}
function attr(node, name) {
  return node?.props?.[name] === undefined ? null : node.props[name];
}
function childTag(node) {
  const child = topChildren(node)[0];
  return child && typeof child === "object" ? child.type : null;
}
function presentationalView(card) {
  const master = findClass(card, "map-auto-scan-master");
  const serverField = findClass(card, "map-auto-scan-server-field");
  const serverInput = findClass(card, "map-auto-scan-server-input");
  const serverChips = findClass(card, "map-auto-scan-server-chips");
  const controls = findClass(card, "map-controls");
  const types = findClass(card, "map-types--compact");
  const options = findClass(card, "map-auto-scan-options");
  const masterNodes = nativeNodes(master);
  const serverNodes = nativeNodes(serverField);
  const optionNodes = nativeNodes(options);
  const interval = nativeNodes(card).find((node) => node.type === "input" && node.props?.type === "number");
  const speed = nativeNodes(card).find((node) => node.type === "select");
  const runNow = optionNodes.find((node) => node.type === "button");
  const returnLabel = optionNodes.find((node) => node.type === "label");
  const directSmall = topChildren(card).filter((node) => node?.type === "small");
  const chipNodes = topChildren(serverChips).filter((node) => node && typeof node === "object");
  const typeLabels = topChildren(types).filter((node) => node?.type === "label");
  const serverFieldChildren = topChildren(serverField);
  return {
    master: {
      checked: attr(masterNodes.find((node) => node.type === "input"), "checked"),
      strong: nodeText(masterNodes.find((node) => node.type === "strong")),
      status: nodeText(masterNodes.find((node) => node.type === "span")),
    },
    server: {
      label: nodeText(serverFieldChildren[0]),
      input: (() => {
        const input = nativeNodes(serverInput).find((node) => node.type === "input");
        return { value: attr(input, "value"), inputMode: attr(input, "inputMode"), placeholder: attr(input, "placeholder") };
      })(),
      add: (() => {
        const button = nativeNodes(serverInput).find((node) => node.type === "button");
        return { text: nodeText(button), type: attr(button, "type"), disabled: attr(button, "disabled") };
      })(),
      hint: nodeText(serverNodes.find((node) => node.type === "small")),
      chips: chipNodes.map((chip) => {
        const button = nativeNodes(chip).find((node) => node.type === "button");
        const child = topChildren(button)[0];
        const path = child && typeof child === "object" ? nativeNodes(child).find((node) => node.type === "path") : null;
        return {
          idText: topChildren(chip).filter((part) => typeof part !== "object").map(String).join(""),
          removeAriaLabel: attr(button, "aria-label"),
          removeChildTag: childTag(button),
          removeChildClassName: child && typeof child === "object" ? attr(child, "className") : null,
          removeViewBox: child && typeof child === "object" ? attr(child, "viewBox") : null,
          removeAriaHidden: child && typeof child === "object" ? attr(child, "aria-hidden") : null,
          removeFocusable: child && typeof child === "object" ? attr(child, "focusable") : null,
          removeChildText: child && typeof child === "object" ? nodeText(child) : String(child ?? ""),
          removePathD: attr(path, "d"),
        };
      }),
    },
    interval: { value: attr(interval, "value"), min: attr(interval, "min"), max: attr(interval, "max") },
    speed: {
      value: attr(speed, "value"),
      options: nativeNodes(speed).filter((node) => node.type === "option").map((node) => ({ value: attr(node, "value"), text: nodeText(node) })),
    },
    types: typeLabels.map((label) => {
      const input = nativeNodes(label).find((node) => node.type === "input");
      return { text: nodeText(label), checked: attr(input, "checked"), disabled: attr(input, "disabled") };
    }),
    options: {
      returnToOriginal: {
        text: nodeText(returnLabel),
        checked: attr(nativeNodes(returnLabel).find((node) => node.type === "input"), "checked"),
      },
      runNow: { text: nodeText(runNow), type: attr(runNow, "type"), className: attr(runNow, "className"), disabled: attr(runNow, "disabled") },
    },
    notice: nodeText(directSmall[0]),
    nextScan: nodeText(directSmall[1]),
    nativeTagCount: nativeNodes(card).length,
  };
}
function diffValues(original, current, at = "$", output = []) {
  if (Object.is(original, current)) return output;
  if (Array.isArray(original) || Array.isArray(current)) {
    if (!Array.isArray(original) || !Array.isArray(current)) { output.push({ path: at, original, current }); return output; }
    const max = Math.max(original.length, current.length);
    for (let i = 0; i < max; i += 1) diffValues(original[i], current[i], `${at}[${i}]`, output);
    return output;
  }
  if (original && current && typeof original === "object" && typeof current === "object") {
    for (const key of new Set([...Object.keys(original), ...Object.keys(current)])) diffValues(original[key], current[key], `${at}.${key}`, output);
    return output;
  }
  output.push({ path: at, original: original ?? null, current: current ?? null });
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
  for (const [name, value] of Object.entries(node.props || {})) if (name !== "children") props[name] = value;
  return React.createElement(node.type, props, ...children);
}
function renderMarkup(root) {
  return renderToStaticMarkup(toReact(root));
}
function makeDocument({ language, theme, title, markup, css }) {
  return `<!doctype html><html lang="${language}" data-theme="${theme}"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>${title}</title><style>${css}</style></head><body>${markup}</body></html>\n`;
}
async function enterAuto(harness, t, label) {
  const button = harness.findNodes((node) => node.type === "button" && node.props?.role === "tab" && nodeText(node) === t("map.autoScan"))[0];
  assert.ok(button, `${label}: Auto tab button`);
  assert.equal(button.props["aria-selected"], false, `${label}: Auto starts inactive`);
  button.props.onClick();
  await harness.settle();
  const selected = harness.findNodes((node) => node.type === "button" && node.props?.role === "tab" && nodeText(node) === t("map.autoScan"))[0];
  assert.equal(selected.props["aria-selected"], true, `${label}: Auto selected through actual callback`);
}
async function bootOriginal(spec) {
  const t = translator(spec.language);
  const summary = { serverId: 321, counts: emptyCounts, scanState: spec.scanState };
  const h = await createOriginalHarness({
    label: `auto-remove-b-original-${spec.language}-${spec.id}`,
    online: spec.online,
    now: NOW,
    language: spec.language,
    translate: t,
    serverId: 321,
    scanState: spec.scanState,
    summary,
    autoScanConfig: spec.config,
    autoScanRunning: spec.autoScanRunning,
    stubs: { dataOptions: { mode: "auto", value: optionsReply({ scanProgress: null }) } },
  });
  await h.mount();
  await enterAuto(h, t, `original/${spec.id}`);
  return h;
}
const pageSource = read(pageFile);
async function bootCurrent(spec) {
  const t = translator(spec.language);
  const summary = { serverId: 321, counts: emptyCounts, scanState: spec.scanState };
  const h = await createHarness(pageSource, `auto-remove-b-current-${spec.language}-${spec.id}`, {
    now: NOW,
    language: spec.language,
    serverId: 321,
    translate: t,
    props: {
      bridgeMode: "native",
      backendAvailable: true,
      online: spec.online,
      currentServerId: 321,
      previewState: "",
      activeTab: "city",
      onActiveTabChange: () => {},
      scanState: spec.scanState,
      summary,
      onState: () => {},
      onCounts: () => {},
      autoScanConfig: spec.config,
      autoScanRunning: spec.autoScanRunning,
      onAutoScanConfig: () => {},
    },
    dataOptions: optionsReply({ scanProgress: null })(321),
  });
  await h.mount();
  await enterAuto(h, t, `current/${spec.id}`);
  return h;
}

function lineAt(source, index) {
  return source.slice(0, index).split(/\r?\n/).length;
}
function needleLocator(source, file, needle, id) {
  const index = source.indexOf(needle);
  assert.ok(index >= 0, `${id}: source needle`);
  assert.equal(source.indexOf(needle, index + 1), -1, `${id}: source needle must be unique`);
  return { id, file: rel(file), utf8ByteOffset: Buffer.byteLength(source.slice(0, index), "utf8"), byteLength: Buffer.byteLength(needle, "utf8"), sha256: textHash(needle), line: lineAt(source, index), text: needle };
}
function astLocator(source, file, node, id) {
  assert.ok(node?.start >= 0 && node?.end > node.start, `${id}: AST node`);
  const text = source.slice(node.start, node.end);
  return { id, file: rel(file), utf8ByteOffset: Buffer.byteLength(source.slice(0, node.start), "utf8"), byteLength: Buffer.byteLength(text, "utf8"), sha256: textHash(text), line: lineAt(source, node.start), text };
}
function walkAst(node, output = []) {
  if (!node || typeof node !== "object") return output;
  if (Array.isArray(node)) { node.forEach((child) => walkAst(child, output)); return output; }
  if (node.type) output.push(node);
  for (const [key, value] of Object.entries(node)) if (key !== "loc" && value && typeof value === "object") walkAst(value, output);
  return output;
}
function jsxClass(node) {
  const attr = node?.openingElement?.attributes?.find((item) => item.type === "JSXAttribute" && item.name?.name === "className");
  return attr?.value?.type === "StringLiteral" ? attr.value.value : null;
}
const pageAst = parse(pageSource, { sourceType: "module", plugins: ["jsx"] });
const currentCardNode = walkAst(pageAst).find((node) => node.type === "JSXElement" && jsxClass(node) === "map-auto-scan-card");
assert.ok(currentCardNode, "current Auto card JSX node");
const currentCardText = pageSource.slice(currentCardNode.start, currentCardNode.end);
const currentEmitNode = walkAst(pageAst).find((node) => node.type === "VariableDeclarator" && node.id?.name === "emitAutoConfig");
const currentAddNode = walkAst(pageAst).find((node) => node.type === "FunctionDeclaration" && node.id?.name === "addAutoServers");
const currentToggleNode = walkAst(pageAst).find((node) => node.type === "FunctionDeclaration" && node.id?.name === "toggleAutoType");
assert.ok(currentEmitNode && currentAddNode && currentToggleNode, "current Auto helper nodes");
const currentLastTypePredicate = needleLocator(pageSource, pageFile, 'disabled={autoConfig.selectedTypes.length === 1 && autoConfig.selectedTypes[0] === key}', "current-last-type-disabled-predicate");
const currentRunNowPredicate = needleLocator(pageSource, pageFile, 'disabled={!online || !autoConfig.enabled || autoRunning || scanState.isReading}', "current-run-now-disabled-predicate");
const currentIconNode = walkAst(currentCardNode).find((node) => node.type === "JSXElement"
  && node.openingElement?.name?.name === "svg"
  && jsxClass(node) === "ui-icon");
assert.ok(currentIconNode, "current server-chip remove child");

const originalIconLocator = {
  id: "original-index-remove-icon-component",
  file: rel(indexFile),
  utf8ByteOffset: byteAt(index.entry, recoveredIconNode.start),
  byteLength: Buffer.byteLength(recoveredIconSource, "utf8"),
  sha256: textHash(recoveredIconSource),
  text: recoveredIconSource,
};
const currentCardLocator = {
  id: "current-auto-card",
  file: rel(pageFile),
  utf8ByteOffset: Buffer.byteLength(pageSource.slice(0, currentCardNode.start), "utf8"),
  byteLength: Buffer.byteLength(currentCardText, "utf8"),
  sha256: textHash(currentCardText),
  line: lineAt(pageSource, currentCardNode.start),
};
const currentIconText = pageSource.slice(currentIconNode.start, currentIconNode.end);
const currentIconLocator = {
  id: "current-server-chip-remove-child",
  file: rel(pageFile),
  utf8ByteOffset: Buffer.byteLength(pageSource.slice(0, currentIconNode.start), "utf8"),
  byteLength: Buffer.byteLength(currentIconText, "utf8"),
  sha256: textHash(currentIconText),
  line: lineAt(pageSource, currentIconNode.start),
  text: currentIconText,
};

const rawDir = path.join(here, "raw");
const generatedDir = path.join(here, "generated");
ensureDir(rawDir);
ensureDir(generatedDir);
const originalCss = read(originalCssFile);
const currentCss = `${read(referenceCssFile)}\n${read(stylesCssFile)}`;
const results = [];
for (const spec of specs) {
  const original = await bootOriginal(spec);
  const current = await bootCurrent(spec);
  try {
    const originalCard = expandOriginalDependencies(findClass(original.tree(), "map-auto-scan-card"));
    const currentCard = findClass(current.tree(), "map-auto-scan-card");
    assert.ok(originalCard && currentCard, `${spec.id}: both rendered Auto cards`);
    const originalView = presentationalView(originalCard);
    const currentView = presentationalView(currentCard);
    const differences = diffValues(originalView, currentView);
    const originalMarkup = renderMarkup(scopedCard(originalCard));
    const currentMarkup = renderMarkup(scopedCard(currentCard));
    const originalRaw = path.join(rawDir, `${spec.language}-${spec.id}-original.html`);
    const currentRaw = path.join(rawDir, `${spec.language}-${spec.id}-current.html`);
    fs.writeFileSync(originalRaw, `${originalMarkup}\n`);
    fs.writeFileSync(currentRaw, `${currentMarkup}\n`);
    const pairId = `${spec.language}-${spec.id}`;
    fs.writeFileSync(path.join(generatedDir, `${pairId}-original.html`), makeDocument({ language: spec.language, theme: spec.theme, title: `${pairId} original`, markup: originalMarkup, css: originalCss }));
    fs.writeFileSync(path.join(generatedDir, `${pairId}-current.html`), makeDocument({ language: spec.language, theme: spec.theme, title: `${pairId} current`, markup: currentMarkup, css: currentCss }));
    results.push({
      id: spec.id,
      pairId,
      language: spec.language,
      theme: spec.theme,
      viewport: { width: spec.width, height: spec.height, deviceScaleFactor: 1 },
      input: { now: NOW, online: spec.online, serverId: 321, autoScanRunning: spec.autoScanRunning, scanState: spec.scanState, config: spec.config },
      enteredAutoViaActualTabCallback: true,
      originalView,
      currentView,
      differences,
      exactViewMatch: differences.length === 0,
      originalMarkupSha256: textHash(originalMarkup),
      currentMarkupSha256: textHash(currentMarkup),
      exactMarkupMatch: originalMarkup === currentMarkup,
      rawFiles: { original: rel(originalRaw), current: rel(currentRaw) },
    });
  } finally {
    await original.unmount();
    await current.unmount();
  }
}

const mainSource = read(mainFile);
assert.ok(mainSource.indexOf('import "./reference.css";') < mainSource.indexOf('import "./styles.css";'), "current CSS import order");
const pinnedInputs = {
  task: "LWB317-UI-MAP-AUTO-REMOVE-001",
  milestone: "B",
  generatedAt: "2026-10-04",
  evidenceState: "EXACT_BYTES / EXACT_CONTRACT",
  reference: {
    exe: { path: exe, sha256: fileHash(exe) },
    panel: { path: rel(panelFile), sha256: fileHash(panelFile), bytes: fs.statSync(panelFile).size },
    index: { path: rel(indexFile), sha256: fileHash(indexFile), bytes: fs.statSync(indexFile).size },
    css: { path: rel(originalCssFile), sha256: fileHash(originalCssFile), bytes: fs.statSync(originalCssFile).size },
    autoCard: { ...autoCardLocator, sliceSha256: sha256(autoCardSlice) },
    removeIconUse: originalRemoveUse,
    removeIconComponent: originalIconLocator,
    typeList: originalTypeList,
    lastTypePredicate: originalLastTypePredicate,
    runNowPredicate: originalRunNowPredicate,
    acceptedConfigLocators: { path: rel(locatorFile), sha256: fileHash(locatorFile) },
  },
  current: {
    files: [pageFile, autoConfigFile, mapBackendFile, referenceCssFile, stylesCssFile, mainFile].map((file) => ({ path: rel(file), sha256: fileHash(file), bytes: fs.statSync(file).size })),
    locales: Object.fromEntries(Object.entries(catalogs).map(([language, item]) => [language, { path: rel(item.file), sha256: item.hash }])),
    cssImportOrder: ["./reference.css", "./styles.css"],
    referenceCssExactlyMatchesOriginal: fileHash(referenceCssFile) === fileHash(originalCssFile),
    autoCard: currentCardLocator,
    removeIconChild: currentIconLocator,
    emitAutoConfig: astLocator(pageSource, pageFile, currentEmitNode, "current-emit-auto-config"),
    addAutoServers: astLocator(pageSource, pageFile, currentAddNode, "current-add-auto-servers"),
    toggleAutoType: astLocator(pageSource, pageFile, currentToggleNode, "current-toggle-auto-type"),
    lastTypePredicate: currentLastTypePredicate,
    runNowPredicate: currentRunNowPredicate,
  },
  dependencies: [originalRuntimeFile, currentHarnessFile].map((file) => ({ path: rel(file), sha256: fileHash(file), bytes: fs.statSync(file).size })),
  configs: { default: clone(originalDefault), configured, oneType: oneTypeConfig },
  configValidation: {
    currentDefaultEqualsRecoveredDefault: true,
    configuredNormalizesUnchanged: true,
    recoveredKnownKinds: knownKinds,
    recoveredServerRange: [1, 99999],
  },
  harness: {
    timezone: process.env.TZ,
    now: NOW,
    clockIso: new Date(NOW).toISOString(),
    original: "Existing INTERACTIONS original runtime executes the recovered MapDataPanel body. This packet additionally executes recovered index function Vr for the remove SVG so server-chip geometry is not based on the harness's inert Icon stub.",
    current: "Existing FILTER-LIFECYCLE harness executes the unmodified current MapDataPage.jsx function and real relative .js imports.",
    translation: "Current exact-recovered EN/JA catalogs are injected into both executed renderers, matching the accepted MANUAL-VISUAL method.",
    container: "The executed .map-auto-scan-card is scoped under an identical <section class=\"panel map-panel\"> directly under body. No surrounding Map header/summary/search/table surface is included.",
    actionBoundary: "The actual Auto tab callback is invoked. Run now, scan, native/provider and gameplay handlers are never invoked.",
  },
};
writeJson(path.join(here, "pinned-inputs.json"), pinnedInputs);
writeJson(path.join(here, "render-results.json"), {
  marker: "LWB317_MAP_AUTO_REMOVE_B_STATES_COMPARED",
  generatedAt: "2026-10-04",
  pairs: results,
  states: stateTemplates.map((state) => state.id),
  languages: ["en", "ja"],
  totals: {
    pairs: results.length,
    exactViews: results.filter((item) => item.exactViewMatch).length,
    exactMarkup: results.filter((item) => item.exactMarkupMatch).length,
    viewDifferences: results.reduce((sum, item) => sum + item.differences.length, 0),
  },
});

assert.equal(results.length, 12, "milestone B has six state families in EN and JA");
assert.ok(results.every((result) => result.exactViewMatch && result.exactMarkupMatch && result.differences.length === 0), "all state/language source renders match exactly");
for (const language of ["en", "ja"]) {
  const byId = Object.fromEntries(results.filter((result) => result.language === language).map((result) => [result.id, result]));
  assert.equal(byId.default.currentView.master.checked, false, `${language} default master disabled`);
  assert.equal(byId.default.currentView.options.runNow.disabled, true, `${language} default Run now disabled`);
  assert.equal(byId["configured-waiting"].currentView.master.checked, true, `${language} waiting master enabled`);
  assert.equal(byId["configured-waiting"].currentView.options.runNow.disabled, false, `${language} waiting Run now enabled`);
  assert.equal(byId["auto-running"].currentView.options.runNow.disabled, true, `${language} running Run now disabled`);
  assert.notEqual(byId["auto-running"].currentView.master.status, byId["configured-waiting"].currentView.master.status, `${language} running status differs from waiting`);
  assert.equal(byId["manual-reading"].currentView.options.runNow.disabled, true, `${language} Manual reading Run now disabled`);
  assert.equal(byId.offline.currentView.options.runNow.disabled, true, `${language} offline Run now disabled`);
  assert.equal(byId["one-remaining-type"].currentView.options.runNow.disabled, false, `${language} one-type Run now otherwise enabled`);
  const types = byId["one-remaining-type"].currentView.types;
  assert.equal(types.filter((type) => type.checked).length, 1, `${language} one checked type`);
  assert.equal(types.filter((type) => type.disabled).length, 1, `${language} one disabled type`);
  assert.ok(types.find((type) => type.checked)?.disabled, `${language} checked last type is protected`);
}
for (const result of results.filter((item) => item.currentView.server.chips.length > 0)) {
  assert.ok(result.currentView.server.chips.every((chip) => chip.removeChildTag === "svg"
    && chip.removeChildClassName === "ui-icon"
    && chip.removeViewBox === "0 0 16 16"
    && chip.removeAriaHidden === "true"
    && chip.removeFocusable === "false"
    && chip.removePathD === "m4 4 8 8M12 4l-8 8"), `${result.pairId} exact recovered remove SVG`);
}

console.log(JSON.stringify({
  marker: "LWB317_MAP_AUTO_REMOVE_B_STATES_OK",
  pairs: results.length,
  exactViews: results.filter((item) => item.exactViewMatch).length,
  exactMarkup: results.filter((item) => item.exactMarkupMatch).length,
  differences: results.map((item) => ({ id: item.id, count: item.differences.length, paths: item.differences.map((diff) => diff.path) })),
}));
