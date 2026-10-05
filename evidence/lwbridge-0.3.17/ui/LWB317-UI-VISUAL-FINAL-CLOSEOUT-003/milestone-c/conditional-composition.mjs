import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath, pathToFileURL } from "node:url";
import * as fixtures from "../../../../../src/LWBridge.UI-0.3.17/src/previewAutomationFixtures.js";
import * as contracts from "../../../../../src/LWBridge.UI-0.3.17/src/previewAutomationContracts.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../../");
const outputPath = path.join(here, "conditional-composition-results.json");
const verifyOnly = process.argv.includes("--verify");
const require = createRequire(path.join(repo, "src/LWBridge.UI-0.3.17/package.json"));
const { parse } = require("@babel/parser");
const sha = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
const fileSha = (file) => sha(fs.readFileSync(file));
const relative = (file) => path.relative(repo, file).replaceAll("\\", "/");
const read = (name) => fs.readFileSync(path.join(repo, name), "utf8");

const historicalRenderer = path.join(repo, "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-REMAINING-002/milestone-3/source-render/render-pairs.mjs");
const rendererSource = fs.readFileSync(historicalRenderer, "utf8");
assert.equal(sha(rendererSource), "3277540CC2ED974936EEC5D93E10F19D11B061D1711D8D779EFF3D71D151268E", "historical Automation renderer changed");

let rendererCode = rendererSource
  .replace(/^import .*;\r?\n/gm, "")
  .replaceAll("import.meta.url", JSON.stringify(pathToFileURL(historicalRenderer).href));
const persistenceStart = rendererCode.indexOf("fs.mkdirSync(path.join(outputRoot,'raw')");
assert.ok(persistenceStart > 0, "historical renderer persistence boundary missing");
rendererCode = rendererCode.slice(0, persistenceStart);

assert.ok(rendererCode.includes("if(binding==='H')value=['fixture-medal'];"), "historical H hook adapter changed");
assert.ok(rendererCode.includes("if(binding==='Ut')value=[];"), "historical Ut hook adapter changed");
rendererCode = rendererCode.replace("if(binding==='H')value=['fixture-medal'];", "if(binding==='H')value=caseData.preferred||['fixture-medal'];");
rendererCode = rendererCode.replace(
  "if(binding==='Ut')value=[];",
  "if(binding==='Ut')value=[]; if(caseData.aeHooks&&Object.hasOwn(caseData.aeHooks,binding))value=caseData.aeHooks[binding];",
);
const expandedHookAnchor = " if(caseData.expanded&&((context.name==='c'&&index===0)||(context.name==='AutomationCard'&&index===1)||(context.name==='ResourceGatherCard'&&index===0)||(context.name==='TradeStationCard'&&index===2)))value=true;";
assert.ok(rendererCode.includes(expandedHookAnchor), "historical expansion hook adapter changed");
rendererCode = rendererCode.replace(expandedHookAnchor, ` const componentHooks=caseData.componentHooks?.[context.name];\n if(componentHooks&&Object.hasOwn(componentHooks,index))value=componentHooks[index];\n${expandedHookAnchor}`);

const originalHook = "function hook(initial){const draft=structuredClone(typeof initial==='function'?initial():initial);const store={getSnapshot:()=>({draft,error:null,saving:false,dirty:false}),flush:()=>Promise.resolve(),receive(){},edit(){},pause(){}};return {...store.getSnapshot(),store};}";
assert.ok(rendererCode.includes(originalHook), "historical config hook adapter changed");
rendererCode = rendererCode.replace(originalHook, `function hook(initial,name){\n const base=structuredClone(typeof initial==='function'?initial():initial);\n const configIndex=context.configIndex??0;context.configIndex=configIndex+1;\n const override=caseData.currentDrafts?.[context.name]?.[configIndex];\n const draft=structuredClone(override===undefined?base:override);\n const status=caseData.storeStates?.[name]??{};\n const store={getSnapshot:()=>({draft,error:status.error??null,saving:status.saving===true,dirty:false}),flush:()=>Promise.resolve(),receive(){},edit(){},pause(){},refresh:()=>Promise.resolve()};\n return {...store.getSnapshot(),store};\n}`);

assert.ok(rendererCode.includes("const noop=()=>{};const store=(profile,name,draft)=>hook(draft).store;"), "historical store helper changed");
rendererCode = rendererCode.replace("const noop=()=>{};const store=(profile,name,draft)=>hook(draft).store;", "const noop=()=>{};const store=(profile,name,draft)=>hook(draft,name).store;");
rendererCode = rendererCode.replace("n:(name,draft)=>({...hook(draft),state:hook(draft).store})", "n:(name,draft)=>({...hook(draft,name),state:hook(draft,name).store})");

const oldCurrentNames = "['AutomationPage','automationCategories','automationCards','initialAutomationDraft','previewConstructionBuildingTypes','previewSoldierCamps','previewTrainRewards']";
const newCurrentNames = "['AutomationPage','AutomationCard','AutomationFields','ResourceGatherCard','TradeStationCard','automationCategories','automationCards','initialAutomationDraft','previewConstructionBuildingTypes','previewSoldierCamps','previewTrainRewards']";
assert.ok(rendererCode.includes(oldCurrentNames), "historical current module export list changed");
rendererCode = rendererCode.replace(oldCurrentNames, newCurrentNames);

const AsyncFunction = Object.getPrototypeOf(async function () {}).constructor;
const api = await new AsyncFunction(
  "fs", "path", "crypto", "assert", "createRequire", "fileURLToPath", "pathToFileURL",
  `${rendererCode}\nreturn {setup,inputFor,materialize,element,renderToStaticMarkup,seed(value){caseData=value;}};`,
)(fs, path, crypto, assert, createRequire, fileURLToPath, pathToFileURL);
const pair = api.setup("en");

const panelPath = path.join(repo, "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/AutomationPanel-BJ0gIqFh.js");
const cardPath = path.join(repo, "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/AutomationCard-LCx_jIi7.js");
const panelSource = fs.readFileSync(panelPath, "utf8");
const cardSource = fs.readFileSync(cardPath, "utf8");
assert.equal(sha(panelSource), "6DB2328191AF33E5631D507271C36D7008B287DAA7C8CD1A957B23EF6F871725", "recovered AutomationPanel asset changed");
assert.equal(sha(cardSource), "24ED773623C237F8219EFF5443FAC3BA7B55B6E99E168E52FF066FAF2ABE7A61", "recovered AutomationCard asset changed");

function functionAnchor(source, name) {
  const ast = parse(source, { sourceType: "module" });
  const node = ast.program.body.find((entry) => entry.type === "FunctionDeclaration" && entry.id?.name === name);
  assert.ok(node, `missing recovered function ${name}`);
  const raw = source.slice(node.start, node.end);
  return {
    name,
    byteOffset: Buffer.byteLength(source.slice(0, node.start)),
    byteLength: Buffer.byteLength(raw),
    sha256: sha(raw),
  };
}

const originalFunctions = {
  Ae: functionAnchor(panelSource, "Ae"),
  pe: functionAnchor(panelSource, "pe"),
  me: functionAnchor(panelSource, "me"),
  he: functionAnchor(panelSource, "he"),
  card: functionAnchor(cardSource, "c"),
};
assert.deepEqual(originalFunctions.Ae, {
  name: "Ae", byteOffset: 21817, byteLength: 53453,
  sha256: "61F181D014CFA82BD35CF3BBAE1DB3096CBE502826DDCD4680AD4C46C11A4B68",
}, "exact recovered Ae identity changed");
assert.equal(originalFunctions.card.sha256, "A3FA760F0D42AA4A927C8B6E810407723F2BA314360CD35AC05D3DF08F639657", "exact recovered AutomationCard function changed");

const dependencyNames = [
  "src/LWBridge.UI-0.3.17/src/AutomationPage.jsx",
  "src/LWBridge.UI-0.3.17/src/AutomationMeta.jsx",
  "src/LWBridge.UI-0.3.17/src/DispatchAssistManual.jsx",
  "src/LWBridge.UI-0.3.17/src/previewConfigHook.jsx",
  "src/LWBridge.UI-0.3.17/src/previewAutomationContracts.js",
  "src/LWBridge.UI-0.3.17/src/previewAutomationFixtures.js",
  "src/LWBridge.UI-0.3.17/src/sharedPageUI.jsx",
  "src/LWBridge.UI-0.3.17/src/tradePurchaseHistory.js",
  "src/LWBridge.UI-0.3.17/src/locales/en.js",
  "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/AutomationPanel-BJ0gIqFh.js",
  "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/AutomationCard-LCx_jIi7.js",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-REMAINING-002/milestone-3/source-render/render-pairs.mjs",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/milestone-c/run-browser-current.mjs",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/milestone-c/browser-current-results.json",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/milestone-c/config-store-ownership.mjs",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/milestone-c/config-store-ownership-results.json",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/milestone-a/automation-corrections.mjs",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/milestone-a/automation-corrections-results.json",
];
const dependencies = dependencyNames.map((name) => ({ path: name, sha256: fileSha(path.join(repo, name)) }));
const dependencyHash = (name) => dependencies.find((entry) => entry.path === name)?.sha256;

const browserPath = path.join(here, "browser-current-results.json");
const browserResult = JSON.parse(fs.readFileSync(browserPath, "utf8"));
assert.equal(browserResult.marker, "LWB317_FINAL_CLOSEOUT_MILESTONE_C_BROWSER_CURRENT_OK", "fresh current browser packet marker changed");
for (const [sourceName, browserHash] of Object.entries(browserResult.sourceFiles || {})) {
  assert.ok(dependencyHash(sourceName), `browser-current source is outside conditional dependency closure: ${sourceName}`);
  assert.equal(String(browserHash).toUpperCase(), dependencyHash(sourceName), `browser-current packet is stale for ${sourceName}`);
}
assert.equal(
  String(browserResult.scriptSha256 || "").toUpperCase(),
  dependencyHash("evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/milestone-c/run-browser-current.mjs"),
  "browser-current runner drifted after the recorded browser packet",
);
const browserCase = (name) => {
  const found = browserResult.cases.find((entry) => entry.name === name);
  assert.ok(found, `missing fresh browser case: ${name}`);
  assert.equal(found.pass, true, `fresh browser case failed: ${name}`);
  return { name, actual: found.actual, expected: found.expected };
};
const ownershipPath = path.join(here, "config-store-ownership-results.json");
const ownershipResult = JSON.parse(fs.readFileSync(ownershipPath, "utf8"));
assert.equal(ownershipResult.marker, "LWB317_FINAL_CLOSEOUT_MILESTONE_C_CONFIG_STORE_OWNERSHIP_OK", "config-store ownership proof marker changed");
assert.deepEqual(ownershipResult.current, ownershipResult.original, "config-store ownership proof no longer matches recovered/current traces");

const milestoneAResultPath = path.join(repo, "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/milestone-a/automation-corrections-results.json");
assert.equal(fileSha(milestoneAResultPath), "34BA3D06A41C05C8D517CDF49C3838111F37C2716D9D8ECF53A4AA40A0EC001C", "Milestone A correction result changed");
assert.equal(fileSha(path.join(repo, "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/milestone-a/automation-corrections.mjs")), "2CF62CD80F5F81339EEE2754DEB647D24CAF1FC8D87F774286FFA94534D5C468", "Milestone A correction producer changed");
const milestoneA = JSON.parse(fs.readFileSync(milestoneAResultPath, "utf8"));
assert.equal(milestoneA.marker, "LWB317_FINAL_CLOSEOUT_AUTOMATION_CORRECTIONS_OK");
assert.deepEqual(milestoneA.trainDrag.afterDropPreferenceOrder, ["fixture-parts", "fixture-medal"], "Milestone A Train drag reorder proof changed");

const esc = (value) => String(value).replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
const count = (html, pattern) => typeof pattern === "string"
  ? (html.match(new RegExp(esc(pattern), "g")) || []).length
  : (html.match(pattern) || []).length;
const card = (html, title) => {
  const titleIndex = html.indexOf(`<h3>${title}</h3>`);
  assert.ok(titleIndex >= 0, `card not found: ${title}`);
  const start = html.lastIndexOf("<article", titleIndex);
  const end = html.indexOf("</article>", titleIndex);
  assert.ok(start >= 0 && end >= 0, `card bounds not found: ${title}`);
  return html.slice(start, end + "</article>".length);
};
const panelSubtitle = (html) => {
  const match = html.match(/<div class="panel-title"><h2>[\s\S]*?<\/h2><span class="([^"]+)">([^<]*)<\/span><\/div>/);
  assert.ok(match, "panel subtitle not found");
  return { className: match[1], text: match[2] };
};
const carriageGroups = (html) => [...html.matchAll(/<fieldset class="automation-compact-choice-group automation-carriage-choices"[^>]*>([\s\S]*?)<\/fieldset>/g)].map((match) => ({
  checked: count(match[0], /checked=""/g),
  disabled: count(match[0], /disabled=""/g),
}));
const stateText = (html) => html.match(/<span class="automation-state [^"]*">([^<]+)<\/span>/)?.[1] ?? null;
const actionText = (html) => html.match(/<button[^>]*class="automation-run-action"[^>]*>([^<]+)<\/button>/)?.[1] ?? null;
const categoryTabCount = (html) => {
  const block = html.match(/<div class="automation-categories"[^>]*>[\s\S]*?<\/div>/)?.[0] || "";
  return count(block, /role="tab"/g);
};
const errorBeforeConfig = (html) => {
  const e = html.indexOf("class=\"automation-error\"");
  const c = html.indexOf("class=\"automation-config\"");
  return e >= 0 && c >= 0 && e < c;
};
const alertCount = (html) => [...html.matchAll(/<div[^>]*\bautomation-error\b[^>]*>/g)]
  .filter(([tag]) => /\brole="alert"/.test(tag)).length;
const disabledCheckboxForLabel = (html, label) => {
  const match = html.match(new RegExp(`<label class="automation-checkbox-row"><input([^>]*)\\/?><span>${esc(label)}</span></label>`));
  assert.ok(match, `checkbox label not found: ${label}`);
  return /disabled=""/.test(match[1]);
};

function originalInput(category) {
  const input = api.inputFor(pair.current, category);
  input.online = true;
  return input;
}

function renderOriginal(category, mutate = () => {}, seed = {}) {
  const input = originalInput(category);
  mutate(input);
  api.seed(seed);
  return api.renderToStaticMarkup(api.materialize(api.element(pair.original.Ae, input)));
}
function renderCurrentPage(category, previewState = "automation-config", seed = {}) {
  api.seed(seed);
  return api.renderToStaticMarkup(api.materialize(api.element(pair.current.AutomationPage, { activeCategory: category, previewState })));
}
function configFor(draft) {
  let value = structuredClone(draft);
  const store = {
    getSnapshot: () => ({ draft: value, error: null, saving: false, dirty: false }),
    edit(update) { value = typeof update === "function" ? update(value) : update; },
    flush: () => Promise.resolve(), pause() {}, refresh: () => Promise.resolve(), receive() {},
  };
  return { get draft() { return value; }, store, error: null, saving: false, dirty: false };
}
function renderCurrentFields(title, draft, { assistDraft, availableSquads = [1, 2, 3, 4], previewState = "automation-config", seed = {} } = {}) {
  const config = configFor(draft);
  const assistConfig = configFor(assistDraft ?? draft);
  api.seed(seed);
  return api.renderToStaticMarkup(api.materialize(api.element(pair.current.AutomationFields, {
    title, enabled: true, previewState, config, assistConfig, availableSquads, onLocalError() {},
  })));
}
function renderCurrentCard(title, draft, previewState = "automation-config", { assistDraft, seed = {}, availableSquads = [1, 2, 3, 4] } = {}) {
  api.seed({
    ...seed,
    currentDrafts: {
      ...(seed.currentDrafts || {}),
      AutomationCard: [draft, assistDraft ?? contracts.initialAutomationDraft("Dispatch Assist", previewState)],
    },
  });
  return api.renderToStaticMarkup(api.materialize(api.element(pair.current.AutomationCard, {
    title, description: title, previewEnabled: true, previewState, availableSquads, onConfigStatus() {},
  })));
}
function renderCurrentResourceGather(previewState, { draft, availableSquads = [], seed = {} } = {}) {
  api.seed({ ...seed, currentDrafts: { ...(seed.currentDrafts || {}), ResourceGatherCard: draft ? [draft] : undefined } });
  return api.renderToStaticMarkup(api.materialize(api.element(pair.current.ResourceGatherCard, {
    previewEnabled: true, previewState, availableSquads, onConfigStatus() {},
  })));
}
function renderCurrentTrade(previewState, { draft, tab = "goods", expanded = true } = {}) {
  api.seed({
    currentDrafts: { TradeStationCard: draft ? [draft] : undefined },
    componentHooks: { TradeStationCard: { 0: tab, 1: false, 2: expanded } },
  });
  return api.renderToStaticMarkup(api.materialize(api.element(pair.current.TradeStationCard, { previewEnabled: true, previewState })));
}

const cases = [];
function addCase(id, category, original, current, detail = undefined) {
  assert.deepEqual(current, original, `${id}: recovered/current conditional metrics differ`);
  cases.push({ id, category, original, current, pass: true, ...(detail ? { detail } : {}) });
}

const expectedCards = { daily: 7, alliance: 8, resourceGather: 1, resources: 2, chat: 3, trade: 1, system: 2 };
const adapterInputPins = Object.fromEntries(Object.keys(expectedCards).map((category) => {
  const input = originalInput(category);
  return [category, sha(JSON.stringify(input))];
}));
assert.deepEqual(adapterInputPins, {
  daily: "DD62296FBA425EAB2D9556D8A57484F6C25D815354BF4311374E2E0F262A3C0B",
  alliance: "A895EA997CE909E3066BCCFF45AEAAE2766801659FC3DB644A27211F06A4FA7F",
  resourceGather: "A0FBFB9A8CCEDE01C125E7DB0432309656D733F0C7FF4C6EE6E6817FD69B1B62",
  resources: "A2631B6FDDBD3169EB2EB7FCD5BC58AFD72467E050998DC5E70837E7122D2BAC",
  chat: "B57E0FEF13D2B36544551EF33D668E65A8511EFFA1AEC7895EDD83F1E954D0E3",
  trade: "FD848E3485F0CE48B942E240049EAE6F3A44AFF048EB992B9DB89DCB3492595F",
  system: "7888E7E648C31E5CEEA320532478E7962CC7F1AD3E5E129F63E2A1E5B483A2BE",
}, "historical adapter matched-input baseline drifted; re-audit the recovered/current input contract before repinning");
const categoryCoverage = {};
for (const [category, expected] of Object.entries(expectedCards)) {
  const original = renderOriginal(category, () => {}, { expanded: true });
  const current = renderCurrentPage(category, "", { expanded: true });
  const metrics = {
    categoryTabs: categoryTabCount(original),
    cards: count(original, /<article class="automation-card/g),
  };
  const currentMetrics = {
    categoryTabs: categoryTabCount(current),
    cards: count(current, /<article class="automation-card/g),
  };
  assert.equal(metrics.categoryTabs, 7, `${category}: recovered top-level category count changed`);
  assert.equal(metrics.cards, expected, `${category}: recovered card count changed`);
  assert.deepEqual(currentMetrics, metrics, `${category}: baseline category composition differs`);
  categoryCoverage[category] = { original: metrics, current: currentMetrics };
}

// G1: source store precedence error > saving, exact error subtitle class, and card-local error ancestry.
{
  const source = renderOriginal("daily", (input) => { input.config.construction.maxBuilders = 0; }, {
    expanded: true,
    storeStates: { "task:construction": { error: "FAIL" }, "task:treatment": { saving: true } },
  });
  const current = renderCurrentPage("daily", "automation-config", {
    expanded: true,
    componentHooks: { AutomationPage: { 2: { "Automatic Construction": { state: "error", store: {} }, "Automatic Treatment": { state: "saving", store: {} } } } },
  });
  const currentConstruction = renderCurrentCard("Automatic Construction", { enabled: false, constructionTargetEnabled: true, constructionBuildingTypeIds: [1101], constructionTargetLevel: "30", maxBuilders: "0", autoClaimCompleted: true }, "automation-config", { seed: { expanded: true } });
  const sm = { subtitle: panelSubtitle(source), localErrorBeforeConfig: errorBeforeConfig(card(source, "Automatic Construction")) };
  const cm = { subtitle: panelSubtitle(current), localErrorBeforeConfig: errorBeforeConfig(currentConstruction) };
  addCase("G1-error-over-saving", "daily", sm, cm);
}

// G2: saving-only page state remains muted.
{
  const source = renderOriginal("daily", () => {}, { storeStates: { "task:treatment": { saving: true } } });
  const current = renderCurrentPage("daily", "automation-config", { componentHooks: { AutomationPage: { 2: { "Automatic Treatment": { state: "saving", store: {} } } } } });
  addCase("G2-saving-only", "daily", panelSubtitle(source), panelSubtitle(current));
}

// D1: positive Daily composition plus manual Assist task/job ancestry.
{
  const assistFixture = fixtures.previewAssistFixture("automation-assist-schedule");
  const source = renderOriginal("daily", (input) => { input.config.soldierTraining.totalCount = 1000; }, { expanded: true, aeHooks: { Gt: assistFixture } });
  const current = renderCurrentPage("daily", "automation-assist-schedule", { expanded: true });
  const sm = { cards: count(source, /<article class="automation-card/g), camps: count(source, /soldier-training-camp"/g), trainingProgress: count(card(source, "Auto Training"), /role="status"/g), assistRows: count(source, /automation-assist-task-row/g), assistQueueRows: count(source, /automation-inline-status/g) };
  const cm = { cards: count(current, /<article class="automation-card/g), camps: count(current, /soldier-training-camp"/g), trainingProgress: count(card(current, "Auto Training"), /role="status"/g), assistRows: count(current, /automation-assist-task-row/g), assistQueueRows: count(current, /automation-inline-status/g) };
  addCase("D1-daily-positive-manual-assist", "daily", sm, cm);
}

// D2: all source-visible Training conditional edges, grouped as one finite case.
{
  const variants = [];
  const specs = [
    ["no-camps", "automation-training-no-camps", (input) => { input.status.tasks.soldierTraining.camps = []; }, {}, (html) => ({ camps: count(html, /soldier-training-camp"/g), noCamps: count(html, /No barracks available\. Connect the game and refresh\./g) })],
    ["data-unavailable", "automation-training-data-unavailable", (input) => { input.status.tasks.soldierTraining.reason = "data_unavailable"; }, {}, (html) => ({ dataStatus: count(html, /role="status"/g) })],
    ["open-failed", "automation-training-open-failed", () => {}, { componentHooks: { he: { 1: true } } }, (html) => ({ alerts: count(html, /role="alert"/g) })],
    ["unavailable-level", "automation-training-unavailable-level", (input) => { input.config.soldierTraining.targetLevel = 9; }, {}, (html) => ({ disabledNine: count(html, /<option value="9" disabled=""/g) })],
    ["runtime-error", "automation-training-error", (input) => { input.status.tasks.soldierTraining.state = "error"; input.status.tasks.soldierTraining.order.reason = "unconfirmed"; }, {}, (html) => ({ alerts: count(html, /role="alert"/g), state: stateText(html) })],
    ["no-order", "automation-training-no-order", (input) => { input.status.tasks.soldierTraining.order = null; }, {}, (html) => ({ progress: count(html, /soldier-training-fields[\s\S]*?role="status"/g) })],
    ["invalid-quantity", "automation-validation-error", (input) => { input.config.soldierTraining.totalCount = 1000001; }, {}, (html) => ({ invalid: count(html, /aria-invalid="true"/g), alerts: count(html, /role="alert"/g) })],
  ];
  for (const [id, state, mutate, seed, metric] of specs) {
    const sourcePage = renderOriginal("daily", mutate, { expanded: true, ...seed });
    const currentPage = renderCurrentPage("daily", state, { expanded: true });
    const sourceCard = card(sourcePage, "Auto Training");
    const currentCard = card(currentPage, "Auto Training");
    const original = metric(sourceCard), current = metric(currentCard);
    assert.deepEqual(current, original, `D2 ${id}: Training edge differs`);
    if (id === "no-camps") {
      assert.equal(original.camps, 0, "D2 recovered no-camps branch must render zero camp rows");
      assert.equal(current.camps, 0, "D2 current no-camps branch must render zero camp rows");
      assert.equal(original.noCamps, 1, "D2 recovered no-camps fallback must render once");
      assert.equal(current.noCamps, 1, "D2 current no-camps fallback must render once");
    }
    variants.push({ id, original, current });
  }
  cases.push({ id: "D2-training-edges", category: "daily", variants, pass: true });
}

// D3: Railway/Secret running, active Ghost, and Assist busy control fences.
{
  const assistBusy = fixtures.previewAssistFixture("automation-assist-busy");
  const source = renderOriginal("daily", (input) => {
    Object.assign(input.config.railway, { enabled: true });
    Object.assign(input.config.dispatch, { autoExecute: true, collectRewards: true });
    Object.assign(input.config.ghostRecon, { autoStartOwn: true, autoJoinAlliance: true, autoClaimRewards: true, allianceFilter: "ur" });
    Object.assign(input.status.tasks.railway, { state: "running", running: true });
    Object.assign(input.status.tasks.dispatch, { state: "running", running: true });
    Object.assign(input.status.tasks.ghostRecon, { state: "running" });
  }, { expanded: true, aeHooks: { Gt: assistBusy, rn: "fixture-busy" } });
  const currentPage = renderCurrentPage("daily", "automation-runtime-running", { expanded: true });
  const currentGhost = renderCurrentCard("Ghost Ops", { enabled: true, ghostJoinEnabled: true, ghostFilter: "ur", ghostClaimRewards: true }, "automation-runtime-running", { seed: { expanded: true } });
  const currentAssistBusy = renderCurrentPage("daily", "automation-assist-busy", { expanded: true });
  const railwaySource = card(source, "Trucks"), railwayCurrent = card(currentPage, "Trucks");
  const secretSource = card(source, "Secret Task"), secretCurrent = card(currentPage, "Secret Task");
  const ghostSource = card(source, "Ghost Ops");
  const assistSource = card(source, "Secret Task"), assistCurrent = card(currentAssistBusy, "Secret Task");
  const sm = {
    railway: { state: stateText(railwaySource), action: actionText(railwaySource), disabledWeekly: count(railwaySource, /<select[^>]*disabled=""/g), disabledDeparture: disabledCheckboxForLabel(railwaySource, "Depart after free refreshes when refresh tickets are insufficient") },
    secret: { state: stateText(secretSource), action: actionText(secretSource), disabledWeekly: count(secretSource, /automation-weekly-quality[\s\S]*?<\/div>/g) ? count(secretSource.match(/automation-weekly-quality[\s\S]*?<\/div>/)?.[0] || "", /<select[^>]*disabled=""/g) : 0 },
    ghost: { state: stateText(ghostSource), filterChecked: count(ghostSource, /name="ghost-alliance-filter"[^>]*checked=""/g), inlineStatus: count(ghostSource, /automation-inline-status/g) },
    assistBusy: { rows: count(assistSource, /automation-assist-task-row/g), disabled: count(assistSource, /disabled=""/g) },
  };
  const cm = {
    railway: { state: stateText(railwayCurrent), action: actionText(railwayCurrent), disabledWeekly: count(railwayCurrent, /<select[^>]*disabled=""/g), disabledDeparture: disabledCheckboxForLabel(railwayCurrent, "Depart after free refreshes when refresh tickets are insufficient") },
    secret: { state: stateText(secretCurrent), action: actionText(secretCurrent), disabledWeekly: count(secretCurrent, /automation-weekly-quality[\s\S]*?<\/div>/g) ? count(secretCurrent.match(/automation-weekly-quality[\s\S]*?<\/div>/)?.[0] || "", /<select[^>]*disabled=""/g) : 0 },
    ghost: { state: stateText(currentGhost), filterChecked: count(currentGhost, /name="preview-ghost-filter"[^>]*checked=""/g), inlineStatus: count(currentGhost, /automation-inline-status/g) },
    assistBusy: { rows: count(assistCurrent, /automation-assist-task-row/g), disabled: count(assistCurrent, /disabled=""/g) },
  };
  // Disabled-count details differ because the source card also disables provider-fenced outer controls; compare the branch-specific invariants.
  assert.deepEqual(cm.railway, sm.railway, "D3 Railway running differs");
  assert.equal(sm.railway.disabledDeparture, true, "D3 recovered Railway running must disable depart-when-insufficient");
  assert.equal(cm.railway.disabledDeparture, true, "D3 current Railway running must disable depart-when-insufficient");
  assert.deepEqual(cm.secret, sm.secret, "D3 Secret running differs");
  assert.deepEqual(cm.ghost, sm.ghost, "D3 Ghost running differs");
  assert.equal(cm.assistBusy.rows, sm.assistBusy.rows, "D3 Assist busy row count differs");
  assert.ok(sm.assistBusy.disabled > 0 && cm.assistBusy.disabled > 0, "D3 Assist busy controls must be disabled");
  cases.push({ id: "D3-running-and-assist-busy", category: "daily", original: sm, current: cm, pass: true });
}

// D4: active runtime-error cards route failure through card state, without invented card alerts.
{
  const source = renderOriginal("daily", (input) => {
    Object.assign(input.config.railway, { enabled: true });
    Object.assign(input.config.dispatch, { autoExecute: true });
    Object.assign(input.config.ghostRecon, { autoStartOwn: true, autoJoinAlliance: true, autoClaimRewards: true });
    for (const key of ["railway", "dispatch", "ghostRecon"]) Object.assign(input.status.tasks[key], { state: "error", running: false });
  }, { expanded: true });
  const sourceMetrics = Object.fromEntries([["Trucks", "railway"], ["Secret Task", "secret"], ["Ghost Ops", "ghost"]].map(([title, key]) => {
    const html = card(source, title); return [key, { state: stateText(html), alerts: count(html, /class="automation-error"/g) }];
  }));
  const currentMetrics = {};
  for (const [title, key, draft] of [
    ["Trucks", "railway", { enabled: true, delayMinutes: "2", weeklyQualities: fixtures.railwayWeeklyQualities, departWhenTicketsInsufficient: false }],
    ["Secret Task", "secret", { enabled: true, collectRewards: false, delayMinutes: "3", weeklyQualities: fixtures.dispatchWeeklyQualities }],
    ["Ghost Ops", "ghost", { enabled: true, ghostJoinEnabled: true, ghostFilter: "special", ghostClaimRewards: true }],
  ]) {
    const html = renderCurrentCard(title, draft, "automation-runtime-error", { seed: { expanded: true } });
    currentMetrics[key] = { state: stateText(html), alerts: count(html, /class="automation-error"/g) };
  }
  addCase("D4-active-runtime-error", "daily", sourceMetrics, currentMetrics);
}

// D5: generic Resource action busy follows recovered shared-card Running/config-disabled composition.
{
  const source = renderOriginal("resources", (input) => { input.resourceBusy = "buildingResources"; }, { expanded: true });
  const current = renderCurrentPage("resources", "automation-action-busy", { expanded: true });
  const s = card(source, "Building Resource Collection"), c = card(current, "Building Resource Collection");
  const metric = (html) => ({ state: stateText(html), action: actionText(html), switchDisabled: /automation-header-switch[^>]*disabled=""/.test(html), fieldsetDisabled: /automation-config-body[^>]*disabled=""/.test(html) });
  addCase("D5-generic-resources-busy", "resources", metric(s), metric(c), { browser: [browserCase("generic busy card presents Running state"), browserCase("generic busy card presents Running ellipsis action"), browserCase("generic busy disables header switch"), browserCase("generic busy disables config fieldset")] });
}

// D6: valid automatic Dispatch Assist conditional fields.
{
  const assist = { autoHelp: true, qualities: ["ssr", "ur"], delaySeconds: ["1", "5"], intervalSeconds: "30" };
  const source = renderOriginal("daily", (input) => { input.config.dispatchAssist = { ...assist }; }, { expanded: true, aeHooks: { on: "" } });
  const current = renderCurrentFields("Secret Task", contracts.initialAutomationDraft("Secret Task", "automation-config"), { assistDraft: assist });
  const sm = { choices: count(card(source, "Secret Task"), /automation-squad-choices/g), numeric: count(card(source, "Secret Task"), /type="number"/g), localErrors: count(card(source, "Secret Task"), /class="error-text"/g) };
  const cm = { choices: count(current, /automation-squad-choices/g), numeric: count(current, /type="number"/g), localErrors: count(current, /class="error-text"/g) };
  addCase("D6-assist-auto-valid", "daily", sm, cm);
}

// D7: invalid Assist keeps its local error and independent global draft-store ownership.
{
  const assist = { autoHelp: true, qualities: [], delaySeconds: ["10", "1"], intervalSeconds: "30" };
  const source = renderOriginal("daily", (input) => { input.config.dispatchAssist = { ...assist }; }, { expanded: true, aeHooks: { on: "automation.dispatchAssistConfigError" }, storeStates: { "task:dispatchAssist": { error: "FAIL" } } });
  const current = renderCurrentFields("Secret Task", contracts.initialAutomationDraft("Secret Task", "automation-config"), { assistDraft: assist });
  const sm = { localErrors: count(card(source, "Secret Task"), /class="error-text"/g) };
  const cm = { localErrors: count(current, /class="error-text"/g) };
  addCase("D7-assist-invalid-independent-error", "daily", sm, cm, { browser: [
    browserCase("Assist save error has separate Dispatch Assist ownership"),
    browserCase("Assist save error exposes separate Retry and Discard"),
    browserCase("Assist failure uses exact Assist label"),
    browserCase("Assist Discard clears only Assist store error"),
    browserCase("Secret Task draft survives independent Assist Discard"),
  ] });
}

function trainSource(config, aeHooks = {}) {
  return renderOriginal("alliance", (input) => { Object.assign(input.config.allianceTrainRide, config); }, { expanded: true, aeHooks });
}
function trainCurrent(draft, seed = {}) {
  return renderCurrentFields("Automatic Alliance Train Boarding", draft, { seed });
}

// A1: reward mode, reward drag feedback, VIP/additional fields, and Alliance Gather priority drag.
{
  const source = renderOriginal("alliance", (input) => {
    Object.assign(input.config.allianceTrainRide, { selectionMode: "reward", vipSelectionMode: "reward", preferredRewardKeys: ["fixture-medal", "fixture-parts"], preferRewardQuantity: true, autoAcceptVip: true, thanksMode: "tickets", ticketCount: 2 });
    Object.assign(input.config.allianceGather, { squadPriority: [3] });
  }, { expanded: true, aeHooks: { H: ["fixture-medal", "fixture-parts"], Ct: "fixture-medal", Tt: "fixture-parts", Lt: true, zt: "tickets", Ut: [3, 4], jt: 3, Nt: 4 } });
  const train = trainCurrent({ enabled: false, trainMode: "reward", vipTrainMode: "reward", normalFixedCarriageIds: [], vipFixedCarriageIds: [], preferredRewardKeys: ["fixture-medal", "fixture-parts"], preferRewardQuantity: true, autoAcceptVip: true, thanksMode: "tickets", ticketCount: "2" }, { componentHooks: { AutomationFields: { 0: "all", 1: false, 2: "fixture-medal", 3: "fixture-parts" } } });
  const gather = renderCurrentFields("Alliance Gathering Dispatch", { enabled: false, allianceGatherSquads: [3] }, { availableSquads: [3, 4], seed: { componentHooks: { AutomationFields: { 0: "all", 1: false, 2: null, 3: null, 4: null, 5: null, 6: 3, 7: 4 } } } });
  const trainCard = card(source, "Automatic Alliance Train Boarding"), gatherCard = card(source, "Alliance Gathering Dispatch");
  const sm = { rewardDragTarget: count(trainCard, /automation-preference-item selected drag-over/g), rewardDragSource: count(trainCard, /automation-preference-item selected dragging/g), ticketCountSelect: count(trainCard, /<label>Ticket count<select/g), gatherRows: count(gatherCard, /automation-squad-priority-item/g), gatherDragTarget: count(gatherCard, /drag-over/g), gatherDragSource: count(gatherCard, /dragging/g) };
  const cm = { rewardDragTarget: count(train, /automation-preference-item selected drag-over/g), rewardDragSource: count(train, /automation-preference-item selected dragging/g), ticketCountSelect: count(train, /<label>Ticket count<select/g), gatherRows: count(gather, /automation-squad-priority-item/g), gatherDragTarget: count(gather, /drag-over/g), gatherDragSource: count(gather, /dragging/g) };
  assert.equal(sm.ticketCountSelect, 1, "A1 recovered tickets thank mode must mount Ticket count select");
  assert.equal(cm.ticketCountSelect, 1, "A1 current tickets thank mode must mount Ticket count select");
  addCase("A1-train-reward-drag-and-gather", "alliance", sm, cm, { dragBinding: { resultSha256: fileSha(milestoneAResultPath), afterDropPreferenceOrder: milestoneA.trainDrag.afterDropPreferenceOrder } });
}

// A2: explicit fixed/fixed selections.
{
  const source = trainSource({ selectionMode: "fixed", vipSelectionMode: "fixed", fixedCarriageIds: [1], vipFixedCarriageIds: [1, 2] });
  const current = trainCurrent({ enabled: false, trainMode: "fixed", vipTrainMode: "fixed", normalFixedCarriageIds: [1], vipFixedCarriageIds: [1, 2], preferredRewardKeys: [], thanksMode: "like" });
  addCase("A2-train-fixed-explicit", "alliance", carriageGroups(card(source, "Automatic Alliance Train Boarding")), carriageGroups(current));
}

// A3: explicit fixed modes with missing arrays plus Alliance Gather local required error.
{
  const source = renderOriginal("alliance", (input) => {
    Object.assign(input.config.allianceTrainRide, { selectionMode: "fixed", vipSelectionMode: "fixed" });
    delete input.config.allianceTrainRide.fixedCarriageIds; delete input.config.allianceTrainRide.vipFixedCarriageIds;
    input.config.allianceGather = { enabled: false, squadPriority: [] };
  }, { expanded: true, aeHooks: { Ft: "automation.allianceGatherSquadRequired", Ut: [] } });
  const train = trainCurrent({ enabled: false, trainMode: "fixed", vipTrainMode: "fixed", preferredRewardKeys: [], thanksMode: "like" });
  const gather = renderCurrentCard("Alliance Gathering Dispatch", { enabled: false, allianceGatherSquads: [] }, "automation-config", { availableSquads: [], seed: { componentHooks: { AutomationCard: { 0: "automation.allianceGatherSquadRequired", 1: true } } } });
  const sm = { groups: carriageGroups(card(source, "Automatic Alliance Train Boarding")), gatherError: count(card(source, "Alliance Gathering Dispatch"), /class="automation-error"/g) };
  const cm = { groups: carriageGroups(train), gatherError: count(gather, /class="automation-error"/g) };
  addCase("A3-train-missing-arrays-gather-required", "alliance", sm, cm);
}

// A4: missing mode fields derive fixed mode from populated valid arrays.
{
  const source = renderOriginal("alliance", (input) => {
    Object.assign(input.config.allianceTrainRide, { fixedCarriageIds: [1], vipFixedCarriageIds: [1, 2] });
    delete input.config.allianceTrainRide.selectionMode;
    delete input.config.allianceTrainRide.vipSelectionMode;
  }, { expanded: true });
  const current = trainCurrent({ enabled: false, normalFixedCarriageIds: [1], vipFixedCarriageIds: [1, 2], preferredRewardKeys: [], thanksMode: "like" });
  addCase("A4-train-missing-modes-inferred-fixed", "alliance", carriageGroups(card(source, "Automatic Alliance Train Boarding")), carriageGroups(current));
}

// A5: invalid VIP carriage IDs do not count toward the recovered two-valid-carriage cap.
{
  const source = trainSource({ selectionMode: "reward", vipSelectionMode: "fixed", vipFixedCarriageIds: [1, 99] });
  const current = trainCurrent({ enabled: false, trainMode: "reward", vipTrainMode: "fixed", normalFixedCarriageIds: [], vipFixedCarriageIds: [1, 99], preferredRewardKeys: [], thanksMode: "like" });
  addCase("A5-train-vip-invalid-id-filter", "alliance", carriageGroups(card(source, "Automatic Alliance Train Boarding")), carriageGroups(current));
}

// RG1: recovered Resource Gather runtime state family.
{
  const variants = [];
  for (const state of ["automation-gather-runtime_wait", "automation-gather-manual_wait", "automation-gather-shield_paused", "automation-gather-recalling", "automation-gather-recall_failed", "automation-gather-state_unconfirmed"]) {
    const source = renderOriginal("resourceGather", (input) => { input.config.resourceGather = fixtures.previewResourceGatherConfig(state); input.status.tasks.resourceGather = fixtures.previewResourceGatherRuntime(state); }, { expanded: true, aeHooks: { Ut: [1, 2] } });
    const current = renderCurrentResourceGather(state, { availableSquads: [1, 2], seed: { componentHooks: { ResourceGatherCard: { 0: true, 1: "" } } } });
    const metric = (html) => ({ rows: count(html, /automation-resource-gather-squad/g), stateSpans: count(html, /automation-resource-gather-state muted/g), detailRows: count(html, /automation-resource-gather-state muted[\s\S]*?<small>/g) });
    const original = metric(source), currentMetrics = metric(current);
    assert.deepEqual(currentMetrics, original, `RG1 ${state}: resource-gather state differs`);
    variants.push({ state, original, current: currentMetrics });
  }
  cases.push({ id: "RG1-resource-gather-runtime-family", category: "resourceGather", variants, pass: true });
}

// RG2: no squads and attempted-enable required error.
{
  const source = renderOriginal("resourceGather", (input) => { input.config.resourceGather = fixtures.previewResourceGatherConfig("automation-gather-no-squads"); input.status.tasks.resourceGather = fixtures.previewResourceGatherRuntime("automation-gather-no-squads"); }, { expanded: true, componentHooks: { me: { 1: "automation.resourceGather.squadRequired" } }, aeHooks: { Ut: [] } });
  const current = renderCurrentResourceGather("automation-gather-no-squads", { availableSquads: [], seed: { componentHooks: { ResourceGatherCard: { 0: true, 1: "automation.resourceGather.squadRequired" } } } });
  const metric = (html) => ({ rows: count(html, /automation-resource-gather-squad/g), loading: count(html, /<p class="muted">/g), errors: count(html, /class="automation-error" role="alert"/g) });
  addCase("RG2-resource-gather-no-squads-required", "resourceGather", metric(source), metric(current));
}

// R1: one valid and one invalid Resource Claim interval, preserving card-local error ancestry.
{
  const source = renderOriginal("resources", (input) => { input.resourceStatus.tasks.buildingResources.intervalMinutes = 60; input.resourceStatus.tasks.armedTruckReward.intervalMinutes = 0; }, { expanded: true });
  const buildingCurrent = renderCurrentCard("Building Resource Collection", { enabled: false, intervalMinutes: "60" }, "automation-config", { seed: { expanded: true } });
  const armedCurrent = renderCurrentCard("Armed Truck", { enabled: false, intervalMinutes: "0" }, "automation-config", { seed: { expanded: true } });
  const sm = { buildingErrors: count(card(source, "Building Resource Collection"), /class="automation-error"/g), armedErrors: count(card(source, "Armed Truck"), /class="automation-error"/g), armedBeforeConfig: errorBeforeConfig(card(source, "Armed Truck")) };
  const cm = { buildingErrors: count(buildingCurrent, /class="automation-error"/g), armedErrors: count(armedCurrent, /class="automation-error"/g), armedBeforeConfig: errorBeforeConfig(armedCurrent) };
  addCase("R1-resource-claim-invalid-interval", "resources", sm, cm);
}

// C1: Chat conditional fields, including Treasure reply/search/dispatch and discovered priority.
{
  const source = renderOriginal("chat", (input) => {
    input.redPacketConfig = { enabled: false, claimDelaySeconds: [1, 3], replyEnabled: true, replyDelaySeconds: [2, 5], replyPhrases: ["ok"] };
    input.fireworksConfig = { enabled: false, claimDelaySeconds: [1, 3], replyEnabled: false, replyDelaySeconds: [2, 5], replyPhrases: [] };
    input.treasureConfig = { enabled: false, claimDelaySeconds: [1, 3], replyEnabled: true, replyDelaySeconds: [2, 5], replyPhrases: ["ok"], searchEnabled: true, dispatchEnabled: true, dispatchDelaySeconds: [2, 5], dispatchRetrySeconds: 30, dispatchSquadPriority: [3] };
  }, { expanded: true, aeHooks: { Ut: [3, 4], Dt: 3, kt: 4 } });
  const red = renderCurrentFields("Red Packet", { enabled: false, claimMin: "1", claimMax: "3", replyEnabled: true, replyMin: "2", replyMax: "5", replies: "ok" });
  const fire = renderCurrentFields("Fireworks / Egg", { enabled: false, claimMin: "1", claimMax: "3", replyEnabled: false, replyMin: "2", replyMax: "5", replies: "" });
  const treasure = renderCurrentFields("Treasure", { enabled: false, claimMin: "1", claimMax: "3", replyEnabled: true, replyMin: "2", replyMax: "5", replies: "ok", treasureSearchEnabled: true, treasureDispatchEnabled: true, dispatchMin: "2", dispatchMax: "5", dispatchRetry: "30", dispatchSquads: [3] }, { availableSquads: [3, 4], seed: { componentHooks: { AutomationFields: { 0: "all", 1: false, 2: null, 3: null, 4: 3, 5: 4 } } } });
  const sm = { redSubsettings: count(card(source, "Red Packet"), /automation-subsettings/g), fireSubsettings: count(card(source, "Fireworks / Egg"), /automation-subsettings/g), treasureSubsettings: count(card(source, "Treasure"), /automation-subsettings/g), treasureRows: count(card(source, "Treasure"), /automation-squad-priority-item/g), treasureDragTarget: count(card(source, "Treasure"), /drag-over/g), treasureDragSource: count(card(source, "Treasure"), /dragging/g) };
  const cm = { redSubsettings: count(red, /automation-subsettings/g), fireSubsettings: count(fire, /automation-subsettings/g), treasureSubsettings: count(treasure, /automation-subsettings/g), treasureRows: count(treasure, /automation-squad-priority-item/g), treasureDragTarget: count(treasure, /drag-over/g), treasureDragSource: count(treasure, /dragging/g) };
  addCase("C1-chat-treasure-conditionals", "chat", sm, cm);
}

// C2: invalid Chat/Treasure conditions preserve recovered card-level error surface.
{
  const source = renderOriginal("chat", (input) => {
    input.redPacketConfig = { enabled: false, claimDelaySeconds: [8, 2], replyEnabled: false, replyDelaySeconds: [2, 5], replyPhrases: [] };
    input.fireworksConfig = { enabled: false, claimDelaySeconds: [1, 2], replyEnabled: true, replyDelaySeconds: [2, 5], replyPhrases: [] };
    input.treasureConfig = { enabled: false, claimDelaySeconds: [1, 2], replyEnabled: false, replyDelaySeconds: [2, 5], replyPhrases: [], searchEnabled: false, dispatchEnabled: true, dispatchDelaySeconds: [2, 5], dispatchRetrySeconds: 30, dispatchSquadPriority: [] };
  }, { expanded: true, aeHooks: { tt: { redPacket: "fixture-red-packet-invalid", fireworks: "fixture-fireworks-invalid", treasure: "fixture-treasure-invalid" } } });
  const currentDrafts = [
    ["Red Packet", { enabled: false, claimMin: "8", claimMax: "2", replyEnabled: false, replyMin: "2", replyMax: "5", replies: "" }],
    ["Fireworks / Egg", { enabled: false, claimMin: "1", claimMax: "2", replyEnabled: true, replyMin: "2", replyMax: "5", replies: "" }],
    ["Treasure", { enabled: false, claimMin: "1", claimMax: "2", replyEnabled: false, replyMin: "2", replyMax: "5", replies: "", treasureSearchEnabled: false, treasureDispatchEnabled: true, dispatchMin: "2", dispatchMax: "5", dispatchRetry: "30", dispatchSquads: [] }],
  ];
  const original = Object.fromEntries(currentDrafts.map(([title]) => [title, alertCount(card(source, title))]));
  const current = Object.fromEntries(currentDrafts.map(([title, draft]) => [title, alertCount(renderCurrentCard(title, draft, "automation-config", { seed: { expanded: true } }))]));
  for (const title of currentDrafts.map(([title]) => title)) {
    assert.ok(original[title] > 0, `C2 recovered ${title} invalid branch must surface an alert`);
    assert.ok(current[title] > 0, `C2 current ${title} invalid branch must surface an alert`);
  }
  addCase("C2-chat-invalid-errors", "chat", original, current);
}

const tradeConfigSource = { enabled: true, crossServerEnabled: true, selectedItemIds: [7001], selectedCurrencyIds: [15, 650053] };
const tradeConfigCurrent = { enabled: true, crossServerEnabled: true, selectedItemIds: [7001], selectedCurrencyIds: [15, 650053] };
const sourceTradeStatus = (state = "success", purchases = fixtures.previewTradePurchases) => ({ detectedCount: 3, attemptedCount: 2, succeededCount: 2, lastResult: { state }, purchases });
function originalTrade({ items, tab = "goods", loading = false, error = "", purchases = fixtures.previewTradePurchases, gameTexts = {} }) {
  return renderOriginal("trade", (input) => { input.tradeStationConfig = { ...tradeConfigSource }; input.status.services.tradeStation = sourceTradeStatus("success", purchases); }, { expanded: true, componentHooks: { pe: { 0: items, 1: tab, 2: false, 3: gameTexts, 4: loading, 5: error } } });
}
const tradeMetric = (html) => {
  const tabBlock = html.match(/<div class="trade-station-tabs"[\s\S]*?<\/div>/)?.[0] || "";
  return { goods: count(html, /trade-station-good selected/g), tabs: count(tabBlock, /role="tab"/g), loading: html.includes("trade-station-goods") && html.includes("Loading"), errors: count(html, /class="automation-error"/g), alertErrors: count(html, /class="automation-error" role="alert"/g), purchaseDays: count(html, /trade-station-purchase-day"/g), purchases: count(html, /trade-station-purchase"/g) };
};

// T1: populated Goods tab.
{
  const source = originalTrade({ items: fixtures.previewTradeGoods });
  const current = renderCurrentTrade("automation-trade-positive", { draft: tradeConfigCurrent, tab: "goods" });
  const sm = tradeMetric(source), cm = tradeMetric(current); delete sm.loading; delete cm.loading;
  addCase("T1-trade-goods-populated", "trade", sm, cm);
}

// T2: Purchased/history reverse grouping and rows.
{
  const historyFixture = fixtures.previewTradeFixture("automation-trade-history");
  const source = originalTrade({ items: fixtures.previewTradeGoods, tab: "purchases", purchases: historyFixture.purchases, gameTexts: historyFixture.gameTexts });
  const current = renderCurrentTrade("automation-trade-history", { draft: tradeConfigCurrent, tab: "purchases" });
  const metric = (html) => ({ purchaseDays: count(html, /trade-station-purchase-day"/g), purchases: count(html, /trade-station-purchase"/g), confirmedTimeout: count(html, /confirmed after timeout|Confirmed after timeout/gi) });
  addCase("T2-trade-purchased-history", "trade", metric(source), metric(current));
}

// T3: empty fetch-error branch keeps exact non-alert error DOM.
{
  const source = originalTrade({ items: [], loading: false, error: "Fixture Trade goods request failed" });
  const current = renderCurrentTrade("automation-trade-error", { draft: tradeConfigCurrent, tab: "goods" });
  const metric = (html) => ({ noGoods: /trade-station-goods[\s\S]*?<span class="muted">/.test(html), errors: count(html, /class="automation-error"/g), alertErrors: count(html, /class="automation-error" role="alert"/g), goods: count(html, /trade-station-good-grid[\s\S]*?trade-station-good selected/g) });
  addCase("T3-trade-fetch-error-empty", "trade", metric(source), metric(current));
}

// T4: retained goods persist through loading and failed-refresh presentations.
{
  const variants = [];
  for (const [id, state, loading, error] of [["loading", "automation-trade-loading-retained", true, ""], ["error", "automation-trade-error-retained", false, "Fixture Trade goods request failed"]]) {
    const source = originalTrade({ items: fixtures.previewTradeGoods, loading, error });
    const current = renderCurrentTrade(state, { draft: tradeConfigCurrent, tab: "goods" });
    const metric = (html) => ({ selectedGoods: count(html, /trade-station-good selected/g), loadingCopy: count(html, /<span class="muted">[^<]*Loading[^<]*<\/span>/gi), errors: count(html, /class="automation-error"/g) });
    const original = metric(source), currentMetrics = metric(current);
    assert.deepEqual(currentMetrics, original, `T4 ${id}: retained Trade branch differs`);
    variants.push({ id, original, current: currentMetrics });
  }
  cases.push({ id: "T4-trade-retained-loading-error", category: "trade", variants, pass: true });
}

// S1: Weekend/Attack Shield enabled, attack-pending, and unshielded status branches.
{
  const variants = [];
  for (const [id, state, shielded] of [["pending", "automation-shield-pending", true], ["unshielded", "automation-shield-unshielded", false]]) {
    const source = renderOriginal("system", (input) => {
      input.autoWeekendShield = true; input.autoAttackShield = true;
      input.status.services.shield = { shielded, shieldEndAt: Date.UTC(2026, 9, 6, 0, 0), inWeekendWindow: true, weekendStartAt: Date.UTC(2026, 9, 5, 0, 0), weekendEndAt: Date.UTC(2026, 9, 6, 0, 0), pendingReason: id === "pending" ? "fixture_attack_detected" : "-" };
    }, { expanded: true });
    const weekend = renderCurrentCard("Weekend Shield", { enabled: true }, state, { seed: { expanded: true } });
    const attack = renderCurrentCard("Attack Shield", { enabled: true }, state, { seed: { expanded: true } });
    const original = { weekendState: stateText(card(source, "Weekend Shield")), attackState: stateText(card(source, "Attack Shield")), weekendSummary: count(card(source, "Weekend Shield"), /automation-card-summary/g), attackPending: card(source, "Attack Shield").includes(id === "pending" ? "fixture_attack_detected" : "-") };
    const current = { weekendState: stateText(weekend), attackState: stateText(attack), weekendSummary: count(weekend, /automation-card-summary/g), attackPending: attack.includes(id === "pending" ? "fixture_attack_detected" : "-") };
    assert.deepEqual(current, original, `S1 ${id}: shield branch differs`);
    variants.push({ id, original, current });
  }
  cases.push({ id: "S1-system-shield-states", category: "system", variants, pass: true });
}

assert.equal(cases.length, 24, "conditional matrix must remain the finite 24-case set");

const result = {
  task: "LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/milestone-c",
  marker: "LWB317_FINAL_CLOSEOUT_MILESTONE_C_CONDITIONAL_COMPOSITION_OK",
  caseCount: cases.length,
  categoryCoverage,
  cases,
  originalFunctions,
  adapterInputPins,
  dependencies,
  browserBinding: {
    marker: browserResult.marker,
    resultSha256: fileSha(browserPath),
    sourceFiles: browserResult.sourceFiles,
    runnerSha256: String(browserResult.scriptSha256).toUpperCase(),
  },
  configStoreOwnershipBinding: {
    marker: ownershipResult.marker,
    resultSha256: fileSha(ownershipPath),
    producerSha256: dependencyHash("evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/milestone-c/config-store-ownership.mjs"),
    recoveredFunctionSha256: ownershipResult.source.recoveredFunction.sha256,
    currentSourceSha256: ownershipResult.source.current.sha256,
  },
  limits: [
    "Recovered AutomationPanel Ae and its materialized he/me/pe/shared-card children execute from the untouched recovered assets through the SHA-fenced historical renderer adapter.",
    "Effects/native transports remain inert. Source-valid post-effect hook state is injected only for finite conditional presentation branches; no gameplay/native actions run.",
    "Milestone A is referenced only for already-recorded Train drag reorder interaction semantics under explicit producer/result SHA fences; its stale current Automation dependency is not used as current-source proof.",
    "Fresh Milestone C browser evidence is used only for independent Dispatch Assist save/Retry/Discard interaction ownership and as supplemental busy confirmation; every recorded browser source file plus the browser runner is bound to the live dependency closure.",
    "Config-store ownership executes exact recovered function T and current createConfigDraft with two independently deferred stores plus a newer-draft/older-ack sequence; native persistence stays inert.",
  ],
  scriptSha256: sha(fs.readFileSync(fileURLToPath(import.meta.url))),
};

if (verifyOnly) {
  const recorded = JSON.parse(fs.readFileSync(outputPath, "utf8"));
  assert.deepEqual(result, recorded, "recorded conditional composition proof is stale");
} else {
  fs.writeFileSync(outputPath, `${JSON.stringify(result, null, 2)}\n`);
}

console.log(JSON.stringify({ marker: result.marker, cases: result.caseCount, categories: Object.keys(result.categoryCoverage).length, verified: verifyOnly }));
