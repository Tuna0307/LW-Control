import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath, pathToFileURL } from "node:url";
import {
  compile,
  flatten,
  Fragment,
  h,
  hooks,
  read,
} from "../../LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-c/accepted-harness.mjs";
import * as contracts from "../../../../../src/LWBridge.UI-0.3.17/src/previewAutomationContracts.js";
import * as fixtures from "../../../../../src/LWBridge.UI-0.3.17/src/previewAutomationFixtures.js";
import en from "../../../../../src/LWBridge.UI-0.3.17/src/locales/en.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../../");
const outputPath = path.join(here, "targeted-corrections-results.json");
const verifyOnly = process.argv.includes("--verify");
const sha = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
const relative = (file) => path.relative(repo, file).replaceAll("\\", "/");
const t = (key, vars = {}) => String(en[key] || key).replace(/\{(\w+)\}/g, (match, name) => vars[name] ?? match);

const historicalRenderer = path.join(repo, "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-REMAINING-002/milestone-3/source-render/render-pairs.mjs");
const rendererSource = fs.readFileSync(historicalRenderer, "utf8");
assert.equal(
  sha(rendererSource),
  "3277540CC2ED974936EEC5D93E10F19D11B061D1711D8D779EFF3D71D151268E",
  "historical Automation renderer changed",
);
let rendererCode = rendererSource
  .replace(/^import .*;\r?\n/gm, "")
  .replaceAll("import.meta.url", JSON.stringify(pathToFileURL(historicalRenderer).href));
const persistenceStart = rendererCode.indexOf("fs.mkdirSync(path.join(outputRoot,'raw')");
assert.ok(persistenceStart > 0, "historical renderer persistence boundary missing");
rendererCode = rendererCode.slice(0, persistenceStart);
rendererCode = rendererCode.replace(
  "if(binding==='H')value=['fixture-medal'];",
  "if(binding==='H')value=caseData.preferred||['fixture-medal']; if(binding==='Ct')value=caseData.dragged??null; if(binding==='Tt')value=caseData.over??null;",
);
const AsyncFunction = Object.getPrototypeOf(async function () {}).constructor;
const makeRenderer = (body) => new AsyncFunction(
  "fs",
  "path",
  "crypto",
  "assert",
  "createRequire",
  "fileURLToPath",
  "pathToFileURL",
  `${body}\nreturn {setup,inputFor,materialize,element,renderToStaticMarkup,seed(value){caseData=value;}};`,
)(fs, path, crypto, assert, createRequire, fileURLToPath, pathToFileURL);

const api = await makeRenderer(rendererCode);
const pair = api.setup("en");
const originalInput = api.inputFor(pair.current, "alliance");
originalInput.online = true;
originalInput.config.allianceTrainRide.preferredRewardKeys = ["fixture-medal", "fixture-parts"];

const pageSource = read("src/LWBridge.UI-0.3.17/src/AutomationPage.jsx");
const Icon = compile(pageSource, "AutomationIcon", { h, Fragment });
const fields = (stateHooks) => compile(pageSource, "AutomationFields", {
  h,
  Fragment,
  ...stateHooks,
  ...contracts,
  ...fixtures,
  useI18n: () => ({ t, language: "en" }),
  previewTrainRewards: pair.current.previewTrainRewards,
  AutomationIcon: Icon,
  GameAssetImage: () => null,
  ToggleRow: () => null,
  DispatchAssistManual: () => null,
});

const fixedCases = [];
for (const [id, normal, vip] of [
  ["missing", undefined, undefined],
  ["explicit-empty", [], []],
  ["explicit-populated", [1], [1, 2]],
]) {
  api.seed({ expanded: true });
  Object.assign(originalInput.config.allianceTrainRide, { selectionMode: "fixed", vipSelectionMode: "fixed" });
  if (normal === undefined) {
    delete originalInput.config.allianceTrainRide.fixedCarriageIds;
    delete originalInput.config.allianceTrainRide.vipFixedCarriageIds;
  } else {
    Object.assign(originalInput.config.allianceTrainRide, { fixedCarriageIds: normal, vipFixedCarriageIds: vip });
  }
  const original = api.renderToStaticMarkup(api.materialize(api.element(pair.original.Ae, originalInput)));
  const originalCounts = [...original.matchAll(/<fieldset class="automation-compact-choice-group automation-carriage-choices"[\s\S]*?<\/fieldset>/g)]
    .map((match) => (match[0].match(/checked=""/g) || []).length);
  const stateHooks = hooks();
  const Fields = fields(stateHooks);
  const draft = {
    enabled: false,
    trainMode: "fixed",
    vipTrainMode: "fixed",
    ...(normal === undefined ? {} : { normalFixedCarriageIds: normal, vipFixedCarriageIds: vip }),
  };
  stateHooks.begin();
  const currentTree = Fields({ title: "Automatic Alliance Train Boarding", enabled: true, previewState: "automation-config", config: { draft, store: {} } });
  const currentCounts = flatten(currentTree)
    .filter((node) => node.type === "fieldset" && String(node.props?.className).includes("carriage-choices"))
    .map((group) => flatten(group).filter((node) => node.type === "input" && node.props.checked === true).length);
  assert.deepEqual(currentCounts, originalCounts, `${id} fixed-carriage selection differs from recovered renderer`);
  fixedCases.push({ id, originalCounts, currentCounts });
}

api.seed({ expanded: true });
Object.assign(originalInput.config.allianceTrainRide, {
  fixedCarriageIds: [1],
  vipFixedCarriageIds: [1, 2],
});
delete originalInput.config.allianceTrainRide.selectionMode;
delete originalInput.config.allianceTrainRide.vipSelectionMode;
const originalLegacyModes = api.renderToStaticMarkup(api.materialize(api.element(pair.original.Ae, originalInput)));
const originalLegacyCarriageGroups = (originalLegacyModes.match(/automation-carriage-choices/g) || []).length;
const legacyHooks = hooks();
const LegacyFields = fields(legacyHooks);
legacyHooks.begin();
const currentLegacyTree = LegacyFields({
  title: "Automatic Alliance Train Boarding",
  enabled: true,
  previewState: "automation-config",
  config: {
    draft: { enabled: false, normalFixedCarriageIds: [1], vipFixedCarriageIds: [1, 2] },
    store: {},
  },
});
const currentLegacyCarriageGroups = flatten(currentLegacyTree)
  .filter((node) => node.type === "fieldset" && String(node.props?.className).includes("carriage-choices")).length;
assert.equal(originalLegacyCarriageGroups, 2, "recovered legacy Train mode derivation changed");
assert.equal(currentLegacyCarriageGroups, originalLegacyCarriageGroups, "missing Train modes must derive fixed mode from populated fixed-carriage arrays");
fixedCases.push({
  id: "missing-modes-populated-arrays",
  originalCarriageGroups: originalLegacyCarriageGroups,
  currentCarriageGroups: currentLegacyCarriageGroups,
});

Object.assign(originalInput.config.allianceTrainRide, {
  selectionMode: "reward",
  vipSelectionMode: "reward",
  preferredRewardKeys: ["fixture-medal", "fixture-parts"],
});
api.seed({ expanded: true, preferred: ["fixture-medal", "fixture-parts"], dragged: "fixture-medal", over: "fixture-parts" });
const originalDrag = api.renderToStaticMarkup(api.materialize(api.element(pair.original.Ae, originalInput)));
assert.ok(originalDrag.includes("automation-preference-item selected drag-over"), "recovered Train target class missing");

const dragHooks = hooks();
const DragFields = fields(dragHooks);
let dragDraft = {
  ...contracts.initialAutomationDraft("Automatic Alliance Train Boarding", "automation-config"),
  preferredRewardKeys: ["fixture-medal", "fixture-parts"],
};
const dragStore = {
  getSnapshot: () => ({ draft: dragDraft }),
  edit(update) { dragDraft = typeof update === "function" ? update(dragDraft) : update; },
  flush: () => Promise.resolve(),
};
const renderDrag = () => {
  dragHooks.begin();
  return DragFields({
    title: "Automatic Alliance Train Boarding",
    enabled: true,
    previewState: "automation-config",
    config: { get draft() { return dragDraft; }, store: dragStore },
  });
};
const rewardNodes = (tree) => flatten(tree).filter((node) => String(node.props?.className).startsWith("automation-preference-item"));
const dataTransfer = {};
rewardNodes(renderDrag())[0].props.onDragStart({ dataTransfer });
rewardNodes(renderDrag())[1].props.onDragOver({ preventDefault() {}, dataTransfer });
const targetClasses = rewardNodes(renderDrag()).map((node) => node.props.className);
assert.match(targetClasses[1], /\bdrag-over\b/, "current Train target feedback missing");
rewardNodes(renderDrag())[1].props.onDrop({ preventDefault() {} });
const afterDropClasses = rewardNodes(renderDrag()).map((node) => node.props.className);
assert.ok(afterDropClasses.every((value) => !/\b(?:dragging|drag-over)\b/.test(value)), "drop must clear source and target feedback");
assert.deepEqual(dragDraft.preferredRewardKeys, ["fixture-parts", "fixture-medal"], "drop must preserve recovered reorder/save semantics");
rewardNodes(renderDrag())[0].props.onDragStart({ dataTransfer: {} });
rewardNodes(renderDrag())[1].props.onDragOver({ preventDefault() {}, dataTransfer: {} });
rewardNodes(renderDrag())[0].props.onDragEnd();
const afterEndClasses = rewardNodes(renderDrag()).map((node) => node.props.className);
assert.ok(afterEndClasses.every((value) => !/\b(?:dragging|drag-over)\b/.test(value)), "drag end must clear source and target feedback");

const invalidCode = rendererCode.replace(
  "const draft=structuredClone(typeof initial==='function'?initial():initial);",
  "const draft=structuredClone(typeof initial==='function'?initial():initial);if(draft&&Object.hasOwn(draft,'maxBuilders'))draft.maxBuilders=0;",
);
const invalidApi = await makeRenderer(invalidCode);
const invalidPair = invalidApi.setup("en");
invalidApi.seed({ expanded: true });
const invalidInput = invalidApi.inputFor(invalidPair.current, "daily");
invalidInput.online = true;
invalidInput.config.construction.maxBuilders = 0;
const originalInvalidHtml = invalidApi.renderToStaticMarkup(invalidApi.materialize(invalidApi.element(invalidPair.original.Ae, invalidInput)));
const currentInvalidHtml = invalidApi.renderToStaticMarkup(invalidApi.materialize(invalidApi.element(invalidPair.current.AutomationPage, { activeCategory: "daily", previewState: "automation-config" })));

const playwrightRequire = createRequire("C:/Users/chimw/AppData/Local/Temp/lwbridge-playwright/package.json");
const { chromium } = playwrightRequire("playwright");
const browser = await chromium.launch({ executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe", headless: true });
const geometry = {};
try {
  for (const [side, html] of [["original", originalInvalidHtml], ["current", currentInvalidHtml]]) {
    const context = await browser.newContext({ viewport: { width: 1280, height: 900 } });
    const page = await context.newPage();
    const css = side === "original"
      ? read("evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-rIL9Fpht.css")
      : `${read("src/LWBridge.UI-0.3.17/src/reference.css")}\n${read("src/LWBridge.UI-0.3.17/src/styles.css")}`;
    await page.setContent(`<!doctype html><html data-theme="light"><head><style>${css}</style></head><body>${html}</body></html>`);
    geometry[side] = await page.locator(".automation-error").filter({ hasText: "Enter a builder limit" }).evaluate((node) => ({
      tag: node.tagName,
      margin: getComputedStyle(node).margin,
      y: node.getBoundingClientRect().y,
      height: node.getBoundingClientRect().height,
      nextY: node.nextElementSibling?.getBoundingClientRect().y ?? null,
    }));
    await context.close();
  }
} finally {
  await browser.close();
}
assert.deepEqual(geometry.current, geometry.original, "generic Automation error composition/spacing differs from recovered card");

// Fresh Milestone C re-proves the source-error page-title class against the
// exact recovered Ae instead of repinning the now-stale Milestone A result.
const sourceStoreCode = rendererCode.replace(
  "const noop=()=>{};const store=(profile,name,draft)=>hook(draft).store;",
  "const noop=()=>{};const store=(profile,name,draft)=>{const state=caseData.sourceStores?.[name]??{};const value=structuredClone(draft);return {getSnapshot:()=>({draft:value,error:state.error??null,saving:state.saving??false,dirty:false}),flush:()=>Promise.resolve(),receive(){},edit(){},pause(){}};};",
);
assert.notEqual(sourceStoreCode, rendererCode, "source store seam for error/saving proof not found");
const sourceStoreApi = await makeRenderer(sourceStoreCode);
const sourceStorePair = sourceStoreApi.setup("en");

sourceStoreApi.seed({ expanded: true, sourceStores: { "task:construction": { error: "fixture-save-error" }, "task:treatment": { saving: true } } });
const sourceErrorInput = sourceStoreApi.inputFor(sourceStorePair.current, "daily");
sourceErrorInput.online = true;
const sourceErrorHtml = sourceStoreApi.renderToStaticMarkup(sourceStoreApi.materialize(sourceStoreApi.element(sourceStorePair.original.Ae, sourceErrorInput)));
assert.ok(sourceErrorHtml.includes('class="error-text"'), "recovered Ae save-error subtitle must use error-text");

sourceStoreApi.seed({ expanded: true, sourceStores: { "task:treatment": { saving: true } } });
const sourceSavingInput = sourceStoreApi.inputFor(sourceStorePair.current, "daily");
sourceSavingInput.online = true;
const sourceSavingHtml = sourceStoreApi.renderToStaticMarkup(sourceStoreApi.materialize(sourceStoreApi.element(sourceStorePair.original.Ae, sourceSavingInput)));
assert.ok(!sourceSavingHtml.includes('class="error-text"'), "recovered Ae saving-only subtitle must not use error-text");

const browserResultsPath = path.join(here, "browser-current-results.json");
const browserResults = JSON.parse(fs.readFileSync(browserResultsPath, "utf8"));
const browserCase = (name) => {
  const item = browserResults.cases.find((entry) => entry.name === name);
  assert.ok(item, `fresh Milestone C browser assertion missing: ${name}`);
  assert.equal(item.pass, true, `fresh Milestone C browser assertion failed: ${name}`);
  return item.actual;
};
assert.equal(browserCase("save-error panel subtitle uses recovered error-text class"), 1);
assert.equal(browserCase("save-error panel subtitle does not retain muted class"), 0);
assert.equal(browserCase("saving panel subtitle remains muted"), 1);
assert.equal(browserCase("saving panel subtitle is not error-text"), 0);

// Exact recovered Resources busy owner: resourceBusy drives both shared-card
// actionBusy and config disabled for one named card. Current keeps the native
// action fenced but must match Running / Running… / disabled presentation.
api.seed({ expanded: true });
const sourceBusyInput = api.inputFor(pair.current, "resources");
sourceBusyInput.online = true;
sourceBusyInput.resourceBusy = "buildingResources";
const sourceBusyHtml = api.renderToStaticMarkup(api.materialize(api.element(pair.original.Ae, sourceBusyInput)));
const sourceBusyFieldsets = [...sourceBusyHtml.matchAll(/<fieldset[^>]*automation-config-body[^>]*>/g)].map((match) => match[0]);
const sourceBusyMetrics = {
  runningStates: (sourceBusyHtml.match(/automation-state state-running/g) || []).length,
  runningActions: (sourceBusyHtml.match(/>Running…<\/button>/g) || []).length,
  disabledConfigFieldsets: sourceBusyFieldsets.filter((tag) => /\bdisabled=""/.test(tag)).length,
};
assert.deepEqual(sourceBusyMetrics, { runningStates: 1, runningActions: 1, disabledConfigFieldsets: 1 }, "recovered Resources busy composition changed");
const currentBusyMetrics = {
  runningState: browserCase("generic busy card presents Running state"),
  runningAction: browserCase("generic busy card presents Running ellipsis action"),
  headerDisabled: browserCase("generic busy disables header switch"),
  configDisabledAttribute: browserCase("generic busy disables config fieldset"),
  enabledConfigControls: browserCase("generic busy leaves no enabled config controls"),
  providerActionDisabled: browserCase("generic busy keeps provider-fenced action disabled"),
};
assert.deepEqual(currentBusyMetrics, {
  runningState: "Running",
  runningAction: "Running…",
  headerDisabled: true,
  configDisabledAttribute: "",
  enabledConfigControls: 0,
  providerActionDisabled: true,
});

// Exact recovered Ae filters invalid VIP carriage ids before both checked-state
// and the two-choice cap. [1,99] therefore selects only 1 and must not disable
// valid 2/3/4 as though the invalid 99 consumed the second slot.
api.seed({ expanded: true });
Object.assign(originalInput.config.allianceTrainRide, {
  selectionMode: "reward",
  vipSelectionMode: "fixed",
  fixedCarriageIds: [],
  vipFixedCarriageIds: [1, 99],
  preferredRewardKeys: ["fixture-medal"],
});
const originalVipInvalidHtml = api.renderToStaticMarkup(api.materialize(api.element(pair.original.Ae, originalInput)));
const originalVipGroups = [...originalVipInvalidHtml.matchAll(/<fieldset class="automation-compact-choice-group automation-carriage-choices"[\s\S]*?<\/fieldset>/g)];
assert.equal(originalVipGroups.length, 1, "recovered invalid-VIP fixture must render only VIP fixed carriage choices");
const originalVipInputs = [...originalVipGroups[0][0].matchAll(/<input[^>]*type="checkbox"[^>]*>/g)].map((match) => match[0]);
const originalVipInvalidMetrics = {
  checked: originalVipInputs.filter((tag) => /\bchecked=""/.test(tag)).length,
  disabled: originalVipInputs.filter((tag) => /\bdisabled=""/.test(tag)).length,
};

const vipHooks = hooks();
const VipFields = fields(vipHooks);
vipHooks.begin();
const currentVipTree = VipFields({
  title: "Automatic Alliance Train Boarding",
  enabled: true,
  previewState: "automation-config",
  config: {
    draft: {
      enabled: false,
      trainMode: "reward",
      vipTrainMode: "fixed",
      normalFixedCarriageIds: [],
      vipFixedCarriageIds: [1, 99],
      preferredRewardKeys: ["fixture-medal"],
      preferRewardQuantity: false,
      autoAcceptVip: false,
      thanksMode: "like",
      ticketCount: "1",
    },
    store: {},
  },
});
const currentVipGroup = flatten(currentVipTree).find((node) => node.type === "fieldset" && String(node.props?.className).includes("carriage-choices"));
assert.ok(currentVipGroup, "current invalid-VIP fixture must render VIP fixed carriage choices");
const currentVipInputs = flatten(currentVipGroup).filter((node) => node.type === "input" && node.props.type === "checkbox");
const currentVipInvalidMetrics = {
  checked: currentVipInputs.filter((node) => node.props.checked === true).length,
  disabled: currentVipInputs.filter((node) => node.props.disabled === true).length,
};
assert.deepEqual(currentVipInvalidMetrics, originalVipInvalidMetrics, "invalid VIP carriage filtering/cap differs from recovered Ae");

const dependencies = [
  "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/AutomationPanel-BJ0gIqFh.js",
  "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/AutomationCard-LCx_jIi7.js",
  "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-rIL9Fpht.css",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-REMAINING-002/milestone-3/source-render/render-pairs.mjs",
  "src/LWBridge.UI-0.3.17/src/AutomationPage.jsx",
  "src/LWBridge.UI-0.3.17/src/sharedPageUI.jsx",
  "src/LWBridge.UI-0.3.17/src/previewAutomationContracts.js",
  "src/LWBridge.UI-0.3.17/src/previewAutomationFixtures.js",
  "src/LWBridge.UI-0.3.17/src/reference.css",
  "src/LWBridge.UI-0.3.17/src/styles.css",
  "src/LWBridge.UI-0.3.17/src/locales/en.js",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/milestone-c/browser-current-results.json",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/milestone-c/run-browser-current.mjs",
].map((name) => {
  const file = path.join(repo, name);
  return { path: relative(file), sha256: sha(fs.readFileSync(file)) };
});
const result = {
  marker: "LWB317_FINAL_CLOSEOUT_MILESTONE_C_TARGETED_CORRECTIONS_OK",
  fixedCarriages: fixedCases,
  trainDrag: {
    originalHasTarget: true,
    currentTargetClass: targetClasses[1],
    afterDropClasses,
    afterDropPreferenceOrder: dragDraft.preferredRewardKeys,
    afterDragEndClasses: afterEndClasses,
  },
  genericErrorGeometry: geometry,
  pageStatusComposition: {
    originalErrorClass: "error-text",
    originalSavingClass: "muted",
    currentErrorTextCount: browserCase("save-error panel subtitle uses recovered error-text class"),
    currentSavingMutedCount: browserCase("saving panel subtitle remains muted"),
  },
  genericBusy: { original: sourceBusyMetrics, current: currentBusyMetrics },
  invalidVipFixedCarriages: { original: originalVipInvalidMetrics, current: currentVipInvalidMetrics },
  dependencies,
  scriptSha256: sha(fs.readFileSync(fileURLToPath(import.meta.url))),
};

if (verifyOnly) {
  const recorded = JSON.parse(fs.readFileSync(outputPath, "utf8"));
  assert.deepEqual(result, recorded, "recorded Automation correction proof is stale");
} else {
  fs.mkdirSync(here, { recursive: true });
  fs.writeFileSync(outputPath, `${JSON.stringify(result, null, 2)}\n`);
}
console.log(JSON.stringify({ marker: result.marker, fixedCases: fixedCases.length, geometry: geometry.current, busy: result.genericBusy.original, vipInvalid: result.invalidVipFixedCarriages.current, verified: verifyOnly }));
