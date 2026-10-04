import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath, pathToFileURL } from "node:url";

import { createOriginalHarness, nodeText, optionsReply, treeNodes } from "../../LWB317-UI-MAP-INTERACTIONS-001/original/common.mjs";
import { loadPanel, parse, rawOf } from "../../LWB317-UI-MAP-INTERACTIONS-001/original/lib.mjs";
import { createHarness } from "../../LWB317-UI-MAP-FILTER-LIFECYCLE-001/harness.mjs";
import * as tableHelpers from "../../../../../src/LWBridge.UI-0.3.17/src/mapTablePresentation.js";
import { treasureName } from "../../../../../src/LWBridge.UI-0.3.17/src/mapTablePresentation.js";
import { selectionMembershipKey } from "../../../../../src/LWBridge.UI-0.3.17/src/mapInteractions.js";
import { plunderFixtureFor, plunderFixtureGameTexts } from "../../../../../src/LWBridge.UI-0.3.17/src/mapPlunderFixtures.js";

process.env.TZ = "Asia/Singapore";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const src = path.join(repo, "src/LWBridge.UI-0.3.17/src");
const assets = path.join(repo, "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets");
const baseline = path.resolve(here, "../../LWB317-UI-MAP-TOOLBAR-VISUAL-001/milestone-a/baseline");
const liveCurrent = true;
const outputRoot = here;
const rawDir = path.join(outputRoot, "raw");
fs.mkdirSync(rawDir, { recursive: true });

const require = createRequire(path.join(repo, "src/LWBridge.UI-0.3.17/package.json"));
const React = require("react");
const { renderToStaticMarkup } = require("react-dom/server");
const { transformSync, build } = require("esbuild");
const domRequire = createRequire("C:/Users/chimw/AppData/Local/Temp/lwb317-shell-mount-deps/package.json");
const { JSDOM } = domRequire("jsdom");

const hash = (value) => crypto.createHash("sha256").update(value).digest("hex");
const fileHash = (file) => hash(fs.readFileSync(file));
const rel = (file) => path.relative(repo, file).replaceAll("\\", "/");
const read = (file) => fs.readFileSync(file, "utf8").replaceAll("\r\n", "\n");
const writeJson = (file, value) => fs.writeFileSync(file, `${JSON.stringify(value, null, 2)}\n`);

const expected = {
  exe: "4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783",
  panel: "ce74345518be72e417a510b982c591729e5683f69751e190805598c4c05d3089",
  index: "44c4e4043825b7db296b64171951b27176f8850ff8d7d337cc991df4765524c6",
  css: "3d87e9f65b39eace6a1a254bfc90a38acb72613d1fff7236c7cee7ab9bfaf545",
  image: "2f92a87c3268497df6425b1175db6140e10aabcbdfae02615f065d00e16458e0",
};
const exe = path.resolve(repo, "../LW/lwbridge-0.3.17.exe");
const panelFile = path.join(assets, "MapDataPanel-B4GXEND2.js");
const indexFile = path.join(assets, "index-BVfnK1wp.js");
const cssFile = path.join(assets, "index-rIL9Fpht.css");
const imageFile = path.join(assets, "GameAssetImage-Diy9VTIr.js");
assert.equal(fileHash(exe), expected.exe);
assert.equal(fileHash(panelFile), expected.panel);
assert.equal(fileHash(indexFile), expected.index);
assert.equal(fileHash(cssFile), expected.css);
assert.equal(fileHash(imageFile), expected.image);

const baselineFiles = {
  page: path.join(baseline, "MapDataPage.jsx"),
  treasure: path.join(baseline, "MapTreasureTypeFilter.jsx"),
  retained: path.join(baseline, "MapRetainedGoodsFilter.jsx"),
};
const baselineHashes = Object.fromEntries(Object.entries(baselineFiles).map(([key, file]) => [key, fileHash(file)]));
assert.equal(baselineHashes.page, "d4fa16a6b7a214c2b6c14b13fe9b2064bf755d8f032ffb9101a9cf3346521ea6");
assert.equal(baselineHashes.treasure, "675e0bf8c2c7e6b6e7a5f90e368c6f8796b100581b24987eb6c7536bc4926ab7");
assert.equal(baselineHashes.retained, "d890612c6208ce306bb57eee759cfe79ad9cf86064b3b50526d65d0512657c51");

const currentFiles = liveCurrent ? {
  page: path.join(src, "MapDataPage.jsx"),
  treasure: path.join(src, "MapTreasureTypeFilter.jsx"),
  retained: path.join(src, "MapRetainedGoodsFilter.jsx"),
} : baselineFiles;
const pageSource = read(currentFiles.page);
const treasureSource = read(currentFiles.treasure);
const retainedSource = read(currentFiles.retained);

const catalogs = {};
for (const language of ["en", "ja"]) {
  const file = path.join(src, `locales/${language}.js`);
  catalogs[language] = { file, hash: fileHash(file), messages: (await import(pathToFileURL(file).href)).default };
}
const translator = (language) => {
  const messages = catalogs[language].messages;
  return (key, values = {}) => (messages[key] || key).replace(/\{(\w+)\}/g, (match, name) => String(values[name] ?? match));
};

function functionSource(source, name) {
  const ast = parse(source, { sourceType: "module", plugins: ["jsx"] });
  const declaration = ast.program.body.map((node) => node.declaration || node)
    .find((node) => node.type === "FunctionDeclaration" && node.id?.name === name);
  assert.ok(declaration, `${name} declaration`);
  return source.slice(declaration.start, declaration.end);
}

function compileFilter(source, name, deps) {
  const code = transformSync(functionSource(source, name), { loader: "jsx", jsxFactory: "React.createElement", jsxFragment: "React.Fragment", target: "es2022" }).code;
  const names = ["React", ...Object.keys(deps)];
  const values = [React, ...Object.values(deps)];
  return new Function(...names, `${code}\nreturn ${name};`)(...values);
}

// Exact recovered GameAssetImage component, with an empty immutable image cache. SSR cannot dispatch reads.
const imageSource = read(imageFile);
const imageAst = parse(imageSource, { sourceType: "module" });
const imageFunction = (name) => {
  const node = imageAst.program.body.find((entry) => entry.type === "FunctionDeclaration" && entry.id?.name === name);
  assert.ok(node, `GameAssetImage ${name}`);
  return imageSource.slice(node.start, node.end);
};
const SourceImage = new Function("i", "a", "l", "y", `${imageFunction("b")}\n${imageFunction("x")}\nreturn x;`)(
  React,
  require("react/jsx-runtime"),
  new Map(),
  () => { throw new Error("toolbar SSR must not read images"); },
);

// Actual current GameAssetImage source, evaluated with real React for the retained-goods component.
const currentImageSource = read(path.join(src, "GameAssetImage.jsx"));
const currentImageCode = transformSync(currentImageSource
  .replace(/^import[^\n]+\n/gm, "")
  .replace(/export /g, ""), { loader: "jsx", jsxFactory: "React.createElement", jsxFragment: "React.Fragment", target: "es2022" }).code;
const CurrentImage = new Function(
  "React", "createContext", "useContext", "useEffect", "useRef", "useState", "window", "IntersectionObserver",
  `${currentImageCode}\nreturn GameAssetImage;`,
)(React, React.createContext, React.useContext, React.useEffect, React.useRef, React.useState, { setTimeout }, undefined);

// Exact recovered icon renderer. Full-page Auto compositions must retain the source SVG glyphs.
const indexSource = read(indexFile);
const indexAst = parse(indexSource, { sourceType: "module" });
const recoveredIconNode = indexAst.program.body.find((entry) => entry.type === "FunctionDeclaration" && entry.id?.name === "Vr");
assert.ok(recoveredIconNode, "recovered Vr icon component");
const recoveredIconSource = indexSource.slice(recoveredIconNode.start, recoveredIconNode.end);
const RecoveredIcon = new Function("M", `${recoveredIconSource}\nreturn Vr;`)(require("react/jsx-runtime"));

const panel = loadPanel();
const pageAst = parse(pageSource, { sourceType: "module", plugins: ["jsx"] });
function locator(source, file, node, name) {
  const text = source.slice(node.start, node.end);
  return {
    name,
    path: rel(file),
    utf8ByteOffset: Buffer.byteLength(source.slice(0, node.start), "utf8"),
    byteLength: Buffer.byteLength(text, "utf8"),
    sha256: hash(Buffer.from(text, "utf8")),
    line: source.slice(0, node.start).split("\n").length,
  };
}
const currentFunctions = Object.fromEntries(pageAst.program.body.map((n) => n.declaration || n)
  .filter((n) => n.type === "FunctionDeclaration").map((n) => [n.id.name, n]));
const treasureAst = parse(treasureSource, { sourceType: "module", plugins: ["jsx"] });
const retainedAst = parse(retainedSource, { sourceType: "module", plugins: ["jsx"] });
const treasureNode = treasureAst.program.body.map((n) => n.declaration || n).find((n) => n.id?.name === "MapTreasureTypeFilter");
const retainedNode = retainedAst.program.body.map((n) => n.declaration || n).find((n) => n.id?.name === "MapRetainedGoodsFilter");

const scanKeysNode = pageAst.program.body.filter((node) => node.type === "VariableDeclaration")
  .flatMap((node) => node.declarations).find((node) => node.id?.name === "SCAN_TYPE_LABEL_KEYS");
assert.ok(scanKeysNode, "current SCAN_TYPE_LABEL_KEYS");
const scanTypeLabelKeys = new Function(`return (${pageSource.slice(scanKeysNode.init.start, scanKeysNode.init.end)});`)();
const currentMapTableCode = transformSync(
  `${functionSource(pageSource, "coordinateText")}\n${functionSource(pageSource, "MapTable")}`,
  { loader: "jsx", jsxFactory: "React.createElement", jsxFragment: "React.Fragment", target: "es2022" },
).code;
globalThis.__LWB317_UNIT_B_I18N__ = { current: { language: "en", t: (key) => key } };
const CurrentMapTable = new Function(
  "React", "useI18n", "useMemo", "useState", "useEffect",
  "buildMapColumns", "mapNumber", "mapResourceStatus", "mapRewardCount", "mapRewardName", "mapTaskLabel", "mapTaskSelectable", "mapTaskState",
  "selectionMembershipKey", "GameAssetImage", "SCAN_TYPE_LABEL_KEYS", "EMPTY_GAME_TEXTS",
  `${currentMapTableCode}\nreturn MapTable;`,
)(
  React, () => globalThis.__LWB317_UNIT_B_I18N__.current, React.useMemo, React.useState, React.useEffect,
  tableHelpers.buildMapColumns, tableHelpers.mapNumber, tableHelpers.mapResourceStatus, tableHelpers.mapRewardCount,
  tableHelpers.mapRewardName, tableHelpers.mapTaskLabel, tableHelpers.mapTaskSelectable, tableHelpers.mapTaskState,
  selectionMembershipKey, CurrentImage, scanTypeLabelKeys, {},
);

const scheduledSources = Object.fromEntries(
  ["ScheduledPlunder.jsx", "GameAssetImage.jsx", "mapPlunderPresentation.js", "mapTablePresentation.js"]
    .map((file) => [file, read(path.join(src, file))]),
);
const scheduledI18nStub = "export function useI18n() { return globalThis.__LWB317_UNIT_B_I18N__.current; }\n";
const scheduledVirtual = {
  name: "unit-b-current-scheduled",
  setup(builder) {
    builder.onResolve({ filter: /^\.\// }, (args) => ({ path: args.path.slice(2), namespace: "unit-b" }));
    builder.onLoad({ filter: /.*/, namespace: "unit-b" }, (args) => {
      if (args.path === "i18n.jsx") return { contents: scheduledI18nStub, loader: "jsx" };
      assert.ok(scheduledSources[args.path] !== undefined, `unexpected Scheduled import ${args.path}`);
      return { contents: scheduledSources[args.path], loader: args.path.endsWith(".jsx") ? "jsx" : "js" };
    });
  },
};
const scheduledBundle = await build({
  stdin: { contents: 'export { ScheduledPlunder } from "./ScheduledPlunder.jsx";', resolveDir: src, sourcefile: "unit-b-scheduled-entry.js", loader: "js" },
  bundle: true, format: "cjs", platform: "node", write: false, jsx: "automatic",
  external: ["react", "react/jsx-runtime"], plugins: [scheduledVirtual], logLevel: "silent",
});
const scheduledModule = { exports: {} };
new Function("require", "module", "exports", scheduledBundle.outputFiles[0].text)(require, scheduledModule, scheduledModule.exports);
const CurrentScheduledPlunder = scheduledModule.exports.ScheduledPlunder;

const referenceManifest = {
  exe: { path: exe, sha256: fileHash(exe) },
  panel: { path: rel(panelFile), sha256: fileHash(panelFile) },
  index: { path: rel(indexFile), sha256: fileHash(indexFile) },
  css: { path: rel(cssFile), sha256: fileHash(cssFile) },
  image: { path: rel(imageFile), sha256: fileHash(imageFile) },
  locators: ["R", "ct", "lt", "it"].map((name) => locator(panel.entry.source, panelFile, panel.topLevel[name], `original ${name}`)),
};
const harnessManifest = {
  timezone: process.env.TZ,
  languages: Object.fromEntries(Object.entries(catalogs).map(([language, entry]) => [language, { path: rel(entry.file), sha256: entry.hash }])),
  note: "Both sides execute their actual page callbacks and full Map/Scheduled bodies. Original ct/lt/it/at/ot/st and current filters/Pagination/MapTable/ScheduledPlunder are expanded from source; original/current GameAssetImage implementations are source-executed under empty SSR image caches. The recovered localize provider is stubbed with the same deterministic game-text fixture supplied to current, and current SSR executes under the same fixed clock as the recovered harness.",
};
const sourceUnderTest = {
  files: Object.entries(currentFiles).map(([name, file]) => ({ name, path: rel(file), sha256: fileHash(file), bytes: fs.statSync(file).size })),
  locators: [
    locator(pageSource, currentFiles.page, currentFunctions.MapDataPage, `${liveCurrent ? "current" : "baseline"} MapDataPage`),
    locator(pageSource, currentFiles.page, currentFunctions.Pagination, `${liveCurrent ? "current" : "baseline"} Pagination`),
    locator(treasureSource, currentFiles.treasure, treasureNode, `${liveCurrent ? "current" : "baseline"} MapTreasureTypeFilter`),
    locator(retainedSource, currentFiles.retained, retainedNode, `${liveCurrent ? "current" : "baseline"} MapRetainedGoodsFilter`),
  ],
};
const manifest = liveCurrent ? {
  task: "LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-b",
  mode: "corrected-production",
  checkpoint: "8bfd58efdc0502e6c732ba9f7c7ba35b8e521f4d",
  reference: referenceManifest,
  sourceUnderTest,
  immutableBaseline: Object.entries(baselineFiles).map(([name, file]) => ({ name, path: rel(file), sha256: fileHash(file), bytes: fs.statSync(file).size })),
  harness: harnessManifest,
} : {
  task: "LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-b",
  checkpoint: "8bfd58efdc0502e6c732ba9f7c7ba35b8e521f4d",
  reference: referenceManifest,
  baseline: {
    files: sourceUnderTest.files,
    locators: sourceUnderTest.locators,
  },
  harness: harnessManifest,
};
writeJson(path.join(outputRoot, liveCurrent ? "current-manifest.json" : "baseline-manifest.json"), manifest);

const configuredOptions = optionsReply({
  counts: { city: 1505, resource: 1404, monster: 1303, truck: 1202, railway: 1101, dispatch: 1606, ghost: 1707, treasure: 1808 },
  alliances: [{ name: "Alpha One", count: 14 }, { name: "Beta Two", count: 7 }],
  noAllianceCount: 3,
  names: {
    resource: [{ key: "fixture.resource.iron", count: 13 }, { key: "fixture.resource.food", count: 8 }],
    monster: [{ key: "fixture.monster.doom", count: 12 }, { key: "fixture.monster.walker", count: 5 }],
  },
  dispatchLevels: [1, 5, 9],
  rewardItems: {
    truck: [{ key: "gold", name: "Gold", count: 3, iconPath: "Item/Gold.png" }, { key: "iron", name: "Iron", count: 2, iconPath: "Item/Iron.png" }],
    railway: [{ key: "food", name: "Food", count: 4, iconPath: "Item/Food.png" }],
  },
  treasureTypes: [
    { key: "1:0", treasureType: 1, suppliesType: 0, treasureNameKey: "", count: 15 },
    { key: "2:3", treasureType: 2, suppliesType: 3, treasureNameKey: "", count: 6 },
  ],
});
const defaultOptions = optionsReply({ counts: { city: 0, resource: 0, monster: 0, truck: 0, railway: 0, dispatch: 0, ghost: 0, treasure: 0 } });
const KINDS = ["city", "resource", "monster", "truck", "railway", "dispatch", "ghost", "treasure"];
const ALL_TABS = [...KINDS, "scheduledPlunder"];
const NOW = Date.UTC(2026, 9, 4, 11, 30, 0);

const rowFor = (kind, index = 1) => ({
  uuid: String(1000 + index), serverId: 321, x: 10 + index, y: 20 + index, ownerName: `Fixture ${kind} ${index}`,
  ownerUid: `uid-${index}`, allianceName: "Alpha One", allianceAbbr: "A1", allianceId: "alliance-1",
  quality: 4, level: 5, health: 98765, distanceFromHome: 42, completionTime: NOW - 60_000, plunderAt: NOW - 30_000,
  taskExpireTime: NOW + 3_600_000, updatedAt: NOW - 1_000, arriveTs: NOW + 600_000, power: 123456,
  stolenCount: 0, maxStealCount: 3, robTimes: 0, maxLootCount: 3, protectTime: 0,
  resourceNameKey: "fixture.resource.iron", monsterNameKey: "fixture.monster.doom", taskKind: kind,
  rewards: [{ key: "gold", name: "Gold", iconPath: "Item/Gold.png", count: 1250 }],
  currentGoods: [{ key: "iron", name: "Iron", iconPath: "Item/Iron.png", count: 2500 }],
  treasureType: 1, suppliesType: 0, remainingBoxes: 3, rewardedCount: 1, diggingCount: 1,
  worldClaimState: "charging", chargePercent: 0.5, playerClaimState: "unclaimed", expireTime: NOW + 3_600_000,
});

function nativeTopChildren(root) {
  const out = [];
  const visit = (value) => {
    if (Array.isArray(value)) return value.forEach(visit);
    if (value == null || typeof value === "boolean") return;
    if (value?.type === "Fragment") return visit(value.props?.children);
    out.push(value);
  };
  visit(root?.props?.children);
  return out;
}

function classHas(node, className) {
  return typeof node?.props?.className === "string" && node.props.className.split(/\s+/).filter(Boolean).includes(className);
}

function cleanProps(props = {}) {
  const out = {};
  for (const [key, value] of Object.entries(props)) {
    if (key === "children" || key === "ref" || key === "key") continue;
    out[key] = value;
  }
  return out;
}

function makeCurrentComponents(language) {
  const t = translator(language);
  globalThis.__LWB317_UNIT_B_I18N__.current = { language, t };
  const useI18n = () => ({ language, t });
  return {
    MapTreasureTypeFilter: compileFilter(treasureSource, "MapTreasureTypeFilter", { useRef: React.useRef, useI18n, treasureName }),
    MapRetainedGoodsFilter: compileFilter(retainedSource, "MapRetainedGoodsFilter", { useRef: React.useRef, GameAssetImage: CurrentImage }),
    MapTable: CurrentMapTable,
    ScheduledPlunder: CurrentScheduledPlunder,
  };
}

function toOriginalReact(node, harness, key = "root") {
  if (node == null || typeof node === "boolean") return null;
  if (Array.isArray(node)) return node.map((child, index) => toOriginalReact(child, harness, `${key}-${index}`));
  if (typeof node !== "object") return node;
  if (node.type === "Fragment") return React.createElement(React.Fragment, { key }, toOriginalReact(node.props?.children, harness, `${key}-f`));
  if (typeof node.type === "function") {
    if (node.type.name === "GameAssetImage") return React.createElement(SourceImage, { key, ...cleanProps(node.props) });
    if (node.type.name === "Icon") return React.createElement(RecoveredIcon, { key, ...cleanProps(node.props) });
    return toOriginalReact(harness.expand(node), harness, `${key}-expanded`);
  }
  if (typeof node.type !== "string") return null;
  const children = nativeTopChildren(node).map((child, index) => toOriginalReact(child, harness, `${key}-${index}`));
  return React.createElement(node.type, { key, ...cleanProps(node.props) }, ...children);
}

function toCurrentReact(node, components, key = "root") {
  if (node == null || typeof node === "boolean") return null;
  if (Array.isArray(node)) return node.map((child, index) => toCurrentReact(child, components, `${key}-${index}`));
  if (typeof node !== "object") return node;
  if (node.type === "Fragment") return React.createElement(React.Fragment, { key }, toCurrentReact(node.props?.children, components, `${key}-f`));
  if (typeof node.type === "function") {
    if (node.type.name === "MapTable") return React.createElement(components.MapTable, { key, ...cleanProps(node.props) });
    if (node.type.name === "ScheduledPlunder") return React.createElement(components.ScheduledPlunder, { key, ...cleanProps(node.props) });
    if (node.type.name === "MapTreasureTypeFilter") return React.createElement(components.MapTreasureTypeFilter, { key, ...cleanProps(node.props) });
    if (node.type.name === "MapRetainedGoodsFilter") return React.createElement(components.MapRetainedGoodsFilter, { key, ...cleanProps(node.props) });
    if (node.type.name === "Pagination") return toCurrentReact(node.type(node.props), components, `${key}-expanded`);
    return null;
  }
  if (typeof node.type !== "string") return null;
  const children = nativeTopChildren(node).map((child, index) => toCurrentReact(child, components, `${key}-${index}`));
  return React.createElement(node.type, { key, ...cleanProps(node.props) }, ...children);
}

function toolbarElements(tree) {
  const top = nativeTopChildren(tree);
  const searchIndex = top.findIndex((node) => classHas(node, "map-search"));
  assert.ok(searchIndex >= 0, "map-search exists");
  const search = top[searchIndex];
  const errors = top.map((node, index) => ({ node, index })).filter(({ node }) => classHas(node, "map-scan-error"));
  const pagination = treeNodes(tree).find((node) => typeof node.type === "function" && (node.type.name === "it" || node.type.name === "Pagination"));
  return { search, searchIndex, errors, pagination };
}

function markupOriginal(tree, harness) {
  return renderToStaticMarkup(toOriginalReact(tree, harness));
}

function markupCurrent(tree, components) {
  const realNow = Date.now;
  Date.now = () => NOW;
  try {
    return renderToStaticMarkup(toCurrentReact(tree, components));
  } finally {
    Date.now = realNow;
  }
}

function describe(markup) {
  const dom = new JSDOM(`<body>${markup}</body>`);
  const result = [...dom.window.document.body.querySelectorAll("*")].map((element) => ({
    tag: element.tagName.toLowerCase(),
    className: element.getAttribute("class") || "",
    role: element.getAttribute("role"),
    ariaLabel: element.getAttribute("aria-label"),
    ariaSelected: element.getAttribute("aria-selected"),
    title: element.getAttribute("title"),
    type: element.getAttribute("type"),
    value: "value" in element ? String(element.value) : element.getAttribute("value"),
    checked: "checked" in element ? element.checked : null,
    disabled: "disabled" in element ? element.disabled : null,
    directText: [...element.childNodes].filter((node) => node.nodeType === 3).map((node) => node.textContent).join(""),
  }));
  dom.window.close();
  return result;
}

function diffValues(left, right, at = "$", out = []) {
  if (Object.is(left, right)) return out;
  if (Array.isArray(left) || Array.isArray(right)) {
    if (!Array.isArray(left) || !Array.isArray(right)) { out.push({ path: at, original: left, current: right }); return out; }
    for (let i = 0; i < Math.max(left.length, right.length); i += 1) diffValues(left[i], right[i], `${at}[${i}]`, out);
    return out;
  }
  if ((left && typeof left === "object") || (right && typeof right === "object")) {
    if (!(left && typeof left === "object") || !(right && typeof right === "object")) { out.push({ path: at, original: left, current: right }); return out; }
    for (const key of new Set([...Object.keys(left), ...Object.keys(right)])) diffValues(left[key], right[key], `${at}.${key}`, out);
    return out;
  }
  out.push({ path: at, original: left, current: right });
  return out;
}

async function drain(h, reply) {
  for (let round = 0; round < 20; round += 1) {
    const open = h.requests.filter((request) => !request.__toolbarDone);
    if (open.length === 0) return;
    for (const request of open) {
      request.__toolbarDone = true;
      await h.resolveRequest(request, typeof reply === "function" ? reply(request) : reply);
    }
  }
  throw new Error("toolbar search drain did not quiesce");
}

function findNative(h, predicate) {
  return h.findNodes((node) => typeof node.type === "string" && predicate(node))[0];
}

async function setSelect(h, ariaLabel, value) {
  const select = findNative(h, (node) => node.type === "select" && node.props?.["aria-label"] === ariaLabel);
  assert.ok(select, `select ${ariaLabel}`);
  select.props.onChange({ target: { value } });
  await h.settle();
}

async function setCheckboxByText(h, text, checked) {
  const label = findNative(h, (node) => node.type === "label" && nodeText(node).trim() === text);
  assert.ok(label, `checkbox label ${text}`);
  const input = treeNodes(label).find((node) => node.type === "input" && node.props?.type === "checkbox");
  assert.ok(input, `checkbox ${text}`);
  input.props.onChange({ target: { checked } });
  await h.settle();
}

async function setKeyword(h, t, value) {
  const input = findNative(h, (node) => node.type === "input" && node.props?.["aria-label"] === t("map.searchLabel"));
  assert.ok(input, "keyword input");
  input.props.onChange({ target: { value } });
  await h.settle();
}

function filterNode(h, name) {
  return h.findNodes((node) => typeof node.type === "function" && node.type.name === name)[0];
}

async function chooseFilter(h, names, value) {
  const node = names.map((name) => filterNode(h, name)).find(Boolean);
  assert.ok(node, `filter ${names.join("/")}`);
  node.props.onChange(value);
  await h.settle();
}

async function configure(h, side, tab, language) {
  const t = translator(language);
  await setKeyword(h, t, `${tab}-needle`);
  if (tab === "city") {
    await setSelect(h, t("map.allianceFilter"), "none");
    await setCheckboxByText(h, t("map.markedOnly"), true);
  } else if (tab === "resource" || tab === "monster") {
    await setSelect(h, t("common.name"), tab === "resource" ? "fixture.resource.iron" : "fixture.monster.doom");
  } else if (tab === "truck") {
    await setSelect(h, t("map.quality"), "reindeer");
    await chooseFilter(h, side === "original" ? ["ct"] : ["MapRetainedGoodsFilter"], "gold");
    await setCheckboxByText(h, t("map.plunderableOnly"), true);
    const table = h.findNodes((node) => typeof node.type === "function" && (node.type.displayName === "at" || node.type.name === "MapTable"))[0];
    assert.ok(table, "truck table caller");
    if (side === "original") table.props.onToggleTruck(rowFor("truck"));
    else table.props.onSelect(rowFor("truck"));
    await h.settle();
  } else if (tab === "railway") {
    await setSelect(h, t("map.quality"), "ur");
    await chooseFilter(h, side === "original" ? ["ct"] : ["MapRetainedGoodsFilter"], "food");
    await setCheckboxByText(h, t("map.plunderableOnly"), true);
  } else if (tab === "dispatch") {
    await setSelect(h, t("common.status"), "completed");
    await setSelect(h, t("map.level"), "5");
    await setSelect(h, t("map.quality"), "special");
    await setCheckboxByText(h, t("map.plunderableOnly"), true);
    const delay = findNative(h, (node) => node.type === "input" && node.props?.["aria-label"] === t("map.randomDelaySeconds"));
    delay.props.onChange({ target: { value: "17" } }); await h.settle();
    const table = h.findNodes((node) => typeof node.type === "function" && (node.type.displayName === "at" || node.type.name === "MapTable"))[0];
    assert.ok(table, "dispatch table caller");
    if (side === "original") table.props.onToggleDispatch({ ...rowFor("dispatch"), taskKind: "dispatch" });
    else table.props.onSelect(rowFor("dispatch"));
    await h.settle();
  } else if (tab === "ghost") {
    await setSelect(h, t("common.status"), "pending");
    await setSelect(h, t("map.quality"), "sr");
    const delay = findNative(h, (node) => node.type === "input" && node.props?.["aria-label"] === t("map.randomDelaySeconds"));
    delay.props.onChange({ target: { value: "9" } }); await h.settle();
    const table = h.findNodes((node) => typeof node.type === "function" && (node.type.displayName === "at" || node.type.name === "MapTable"))[0];
    assert.ok(table, "ghost table caller");
    if (side === "original") table.props.onToggleDispatch({ ...rowFor("ghost"), taskKind: "ghost" });
    else table.props.onSelect(rowFor("ghost"));
    await h.settle();
  } else if (tab === "treasure") {
    await chooseFilter(h, side === "original" ? ["lt"] : ["MapTreasureTypeFilter"], "2:3");
    await setCheckboxByText(h, t("map.showForeignRadarTreasures"), true);
    await setCheckboxByText(h, t("map.prioritizeLuckyTreasures"), false);
  }
}

function summary(counts) {
  return { serverId: 321, counts, scanState: { serverId: 321, serverIdSource: "fixture", isReading: false, phase: "idle", selectedTypes: [...KINDS], scanMode: "normal" } };
}

async function bootOriginal(language, options, tab, env = {}) {
  const t = translator(language);
  const serverId = env.serverId ?? 321;
  const optionValue = options(serverId);
  const h = await createOriginalHarness({
    online: env.online ?? true, now: NOW, language, translate: t, serverId,
    summary: env.summary === null ? null : summary(optionValue.counts),
    jobs: env.jobs || { dispatchJobs: [], truckJobs: [] },
    autoScanConfig: env.autoScanConfig,
    props: { ...(env.props || {}) },
    stubs: {
      dataOptions: { mode: "auto", value: options },
      localize: { mode: "auto", value: env.gameTexts || {} },
    },
  });
  await h.mount();
  if (tab !== "city") await h.clickTab(tab);
  return h;
}

async function bootCurrent(language, options, tab, env = {}) {
  const t = translator(language);
  const serverId = env.serverId ?? 321;
  const value = options(serverId);
  const state = { ...summary(value.counts).scanState, serverId };
  const h = await createHarness(pageSource, `toolbar-${language}-${tab}`, {
    baseDir: src, now: NOW, language, translate: t, serverId, dataOptions: value,
    apiExtensions: () => ({
      listPlunderJobs: async () => structuredClone(env.jobs || { dispatchJobs: [], truckJobs: [] }),
      ...(env.actionProvider === false ? {} : {
        scheduleDispatchPlunder: async () => {},
        scheduleTruckPlunder: async () => {},
        shareDispatchToAlliance: async () => ({ shared: 0, failed: 0, sharedUuids: [] }),
        cancelDispatchPlunder: async () => {},
        cancelTruckPlunder: async () => {},
        clearDispatchPlunderHistory: async () => {},
        clearTruckPlunderHistory: async () => {},
      }),
    }),
    props: {
      bridgeMode: "native", backendAvailable: env.backendAvailable ?? true, online: env.online ?? true, currentServerId: serverId,
      activeTab: undefined, onActiveTabChange: undefined, previewState: "", gameTexts: env.gameTexts || {},
      scanState: state, summary: env.summary === null ? null : { ...summary(value.counts), serverId, scanState: state },
      onState: () => {}, onCounts: () => {}, autoScanRunning: false,
      ...(env.autoScanConfig ? { autoScanConfig: env.autoScanConfig, onAutoScanConfig: () => {} } : {}),
      ...(env.props || {}),
    },
  });
  await h.mount();
  if (tab !== "city") {
    const labelKeys = {
      resource: "map.resource", monster: "map.monster", truck: "map.truck", railway: "map.allianceTrain",
      dispatch: "map.secretTask", ghost: "map.ghostScout", treasure: "map.treasure", scheduledPlunder: "map.scheduledPlunder",
    };
    const expectedLabel = t(labelKeys[tab]);
    const button = h.findNodes((node) => node.type === "button" && node.props?.role === "tab"
      && treeNodes(node).some((child) => child.props?.className === "map-tab-label" && nodeText(child) === expectedLabel))[0];
    assert.ok(button, `current tab ${tab}`);
    button.props.onClick();
    await h.settle();
  }
  return h;
}

async function clickSearch(h, t) {
  const button = findNative(h, (node) => node.type === "button" && nodeText(node) === t("common.search"));
  assert.ok(button, "Search button");
  button.props.onClick();
  await h.settle();
}

async function switchScanMode(h, language, mode) {
  const t = translator(language);
  const expected = t(mode === "auto" ? "map.autoScan" : "map.manualScan");
  const button = h.findNodes((node) => node.type === "button" && node.props?.role === "tab" && nodeText(node).trim() === expected)[0];
  assert.ok(button, `scan mode ${mode}`);
  button.props.onClick();
  await h.settle();
}

async function executeCase({ id, language, tab, state = "empty", configured = false, total = null, page = null, scanMode = "manual", env = {} }) {
  const t = translator(language);
  const options = configured ? configuredOptions : defaultOptions;
  const original = await bootOriginal(language, options, tab, env);
  const current = await bootCurrent(language, options, tab, env);
  const components = makeCurrentComponents(language);
  try {
    if (scanMode === "auto") {
      await switchScanMode(original, language, "auto");
      await switchScanMode(current, language, "auto");
    }
    const result = {
      rows: state === "populated" && tab !== "scheduledPlunder" ? [rowFor(tab)] : [],
      total: total ?? (state === "populated" ? 151 : configured ? configuredOptions(321).counts[tab] || 0 : 0),
    };
    if (configured && tab !== "scheduledPlunder") {
      await configure(original, "original", tab, language);
      await configure(current, "current", tab, language);
    }
    if (tab !== "scheduledPlunder") {
      if (state === "error") {
        await drain(original, { rows: [], total: 0 });
        await drain(current, { rows: [], total: 0 });
        await clickSearch(original, t);
        await clickSearch(current, t);
        const originalRequest = original.requests.findLast((request) => !request.__toolbarDone);
        const currentRequest = current.requests.findLast((request) => !request.__toolbarDone);
        assert.ok(originalRequest && currentRequest, "reject request exists");
        originalRequest.__toolbarDone = true; currentRequest.__toolbarDone = true;
        await original.rejectRequest(originalRequest, new Error("fixture integrated query error"));
        await current.rejectRequest(currentRequest, new Error("fixture integrated query error"));
      } else if (state !== "loading") {
        await drain(original, result);
        await drain(current, result);
        if (page != null) {
          await original.setPage(page); await current.setPage(page);
          await drain(original, result); await drain(current, result);
        }
      }
    }
    globalThis.__LWB317_UNIT_B_I18N__.current = { language, t };
    const originalMarkup = markupOriginal(original.tree(), original);
    globalThis.__LWB317_UNIT_B_I18N__.current = { language, t };
    const currentMarkup = markupCurrent(current.tree(), components);
    const originalView = describe(originalMarkup);
    const currentView = describe(currentMarkup);
    const differences = diffValues(originalView, currentView);
    const base = `${language}-${id}`;
    fs.writeFileSync(path.join(rawDir, `${base}-original.html`), `${originalMarkup}\n`);
    fs.writeFileSync(path.join(rawDir, `${base}-current.html`), `${currentMarkup}\n`);
    return {
      id, language, tab, state, configured, total: result.total, page, scanMode,
      original: { markupSha256: hash(originalMarkup), elementCount: originalView.length },
      current: { markupSha256: hash(currentMarkup), elementCount: currentView.length },
      differences,
      exactViewMatch: differences.length === 0,
      raw: { original: rel(path.join(rawDir, `${base}-original.html`)), current: rel(path.join(rawDir, `${base}-current.html`)) },
    };
  } finally {
    await original.unmount();
    await current.unmount();
  }
}

const requiredCases = [];
for (const language of ["en", "ja"]) {
  for (const tab of KINDS) for (const state of ["empty", "loading", "populated", "error"]) {
    requiredCases.push({ id: `${tab}-${state}`, language, tab, state, configured: state === "populated" });
  }
  for (const fixtureName of ["map-scheduled", "map-scheduled-populated", "map-scheduled-conditional"]) {
    const fixture = plunderFixtureFor(fixtureName, NOW);
    requiredCases.push({
      id: fixtureName,
      language,
      tab: "scheduledPlunder",
      state: fixtureName === "map-scheduled" ? "empty" : "populated",
      env: { jobs: fixture, gameTexts: plunderFixtureGameTexts, online: fixture.online },
    });
  }
}
assert.equal(requiredCases.length, 70, "full Map/Scheduled required state matrix");

const configuredAuto = {
  enabled: true,
  intervalMinutes: 90,
  serverIds: [8, 15, 120],
  selectedTypes: ["city", "truck", "treasure"],
  scanMode: "normal",
  returnToOriginalServer: false,
  nextRunAt: NOW + 3_600_000,
};
const supplementalCases = [
  { id: "auto-configured", language: "en", tab: "city", state: "populated", configured: true, scanMode: "auto", env: { autoScanConfig: configuredAuto } },
  { id: "auto-configured", language: "ja", tab: "treasure", state: "populated", configured: true, scanMode: "auto", env: { autoScanConfig: configuredAuto } },
  { id: "missing-server", language: "en", tab: "city", state: "empty", env: { serverId: 0, summary: null, online: false, backendAvailable: true } },
  { id: "backend-unavailable", language: "ja", tab: "city", state: "empty", env: { serverId: 321, online: false, backendAvailable: false } },
  { id: "scheduled-provider-fenced", language: "en", tab: "scheduledPlunder", state: "populated", env: { ...plunderFixtureFor("map-scheduled-populated", NOW), jobs: plunderFixtureFor("map-scheduled-populated", NOW), gameTexts: plunderFixtureGameTexts, actionProvider: false } },
];

const cases = [];
for (const spec of [...requiredCases, ...supplementalCases]) cases.push(await executeCase(spec));

const result = {
  marker: "LWB317_VISUAL_FINAL_UNIT_B_INTEGRATED_MAP_EXECUTED",
  requiredCaseCount: requiredCases.length,
  supplementalCaseCount: supplementalCases.length,
  caseCount: cases.length,
  exactViewMatches: cases.filter((item) => item.exactViewMatch).length,
  differingCases: cases.filter((item) => !item.exactViewMatch).map((item) => ({ id: item.id, language: item.language, tab: item.tab, differenceCount: item.differences.length })),
  cases,
};
writeJson(path.join(outputRoot, liveCurrent ? "current-results.json" : "baseline-results.json"), result);
console.log(JSON.stringify({ marker: result.marker, requiredCases: result.requiredCaseCount, supplementalCases: result.supplementalCaseCount, exact: result.exactViewMatches, differing: result.differingCases }, null, 2));
