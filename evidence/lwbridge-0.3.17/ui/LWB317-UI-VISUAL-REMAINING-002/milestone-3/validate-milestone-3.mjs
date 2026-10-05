import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../../");
const read = (relative) => fs.readFileSync(path.join(repo, relative), "utf8");
const sha = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
const req = createRequire(path.join(repo, "src/LWBridge.UI-0.3.17/package.json"));
const { parse } = req("@babel/parser");

const aeAssetPath = "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/AutomationPanel-BJ0gIqFh.js";
const cardAssetPath = "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/AutomationCard-LCx_jIi7.js";
const pagePath = "src/LWBridge.UI-0.3.17/src/AutomationPage.jsx";
const contractsPath = "src/LWBridge.UI-0.3.17/src/previewAutomationContracts.js";
const fixturesPath = "src/LWBridge.UI-0.3.17/src/previewAutomationFixtures.js";
const browserPath = "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-REMAINING-002/milestone-3/browser-current-results.json";
const sourceRenderRoot = "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-REMAINING-002/milestone-3/source-render";

function functionNode(source, name) {
  const ast = parse(source, { sourceType: "module", plugins: ["jsx"] });
  for (const node of ast.program.body) {
    const declaration = node.type === "ExportNamedDeclaration" ? node.declaration : node;
    if (declaration?.type === "FunctionDeclaration" && declaration.id?.name === name) return declaration;
  }
  throw new Error(`Missing function ${name}`);
}

const recovered = read(aeAssetPath);
const aeNode = functionNode(recovered, "Ae");
const aeRaw = recovered.slice(aeNode.start, aeNode.end);
const aeLocator = {
  utf8ByteOffset: Buffer.byteLength(recovered.slice(0, aeNode.start)),
  utf8ByteLength: Buffer.byteLength(aeRaw),
  sha256: sha(aeRaw),
};
assert.deepEqual(aeLocator, {
  utf8ByteOffset: 21817,
  utf8ByteLength: 53453,
  sha256: "61F181D014CFA82BD35CF3BBAE1DB3096CBE502826DDCD4680AD4C46C11A4B68",
}, "exact recovered Ae identity drifted");

const cardAsset = read(cardAssetPath);
assert.equal(sha(cardAsset), "24ED773623C237F8219EFF5443FAC3BA7B55B6E99E168E52FF066FAF2ABE7A61", "AutomationCard asset drifted");
const cardNode = functionNode(cardAsset, "c");
const cardRaw = cardAsset.slice(cardNode.start, cardNode.end);
assert.ok(cardRaw.indexOf("automation-error") < cardRaw.indexOf("automation-config`"), "recovered card error must precede config");

const baseline = {
  "pre-fix-AutomationPage.jsx": "E44F2F2F107C31F6B41B2BA52DC9B8D9BB1C90DFFC0574FE5F09D977EE0B4243",
  "pre-fix-previewAutomationContracts.js": "EA8F3811A813FB0762533E058AF6CDDE6D704F291EA7CA4B21718EC07EC863D7",
  "pre-fix-previewAutomationFixtures.js": "993F0708DAA88A3C8CA80FBC39555BF84DB021A3639C46F466E06C8F44F3FDF1",
};
for (const [name, expected] of Object.entries(baseline)) {
  const body = read(`evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-REMAINING-002/milestone-3/baseline/${name}`);
  assert.equal(sha(body), expected, `immutable baseline changed: ${name}`);
}
const failing = JSON.parse(read("evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-REMAINING-002/milestone-3/baseline/failing-baseline.json"));
assert.equal(failing.marker, "LWB317_REMAINING_M3_FAILING_BASELINE_CAPTURED");
assert.equal(failing.mismatches.length, 10);

const currentPage = read(pagePath);
const currentContracts = read(contractsPath);
const currentFixtures = read(fixturesPath);
const sourceContracts = [
  ["Ae uses one discovered squad index set", /([A-Za-z_$][\w$]*)\.length>0&&Wt\(\1\)/.test(aeRaw)],
  ["Ae feeds discovered squads to Alliance Gather", aeRaw.includes("squadPriority??[],or=new Set(ar),sr=[...ar,...Ut.filter")],
  ["Ae feeds discovered squads to Treasure", aeRaw.includes("dispatchSquads??[],ee=new Set(u),d=[...u,...Ut.filter")],
  ["Ae Treasure uses priority rows", aeRaw.includes("automation-squad-priority-item") && aeRaw.includes("dispatchSquads")],
  ["Ae maps config errors before panel title", aeRaw.indexOf("Object.entries(P).map") < aeRaw.indexOf("panel-title")],
  ["Ae has separate Daily Activity compositions", (aeRaw.match(/\.has\(`daily`\)/g) ?? []).length === 2],
  ["Ae owns Alliance Gather error on card", /title:`automation\.allianceGather\.title`[\s\S]{0,1800}error:Ft\|\|void 0/.test(aeRaw)],
  ["Ae owns Secret Assist error inside settings", /vr&&on&&\(0,S\.jsx\)\(`p`,\{className:`error-text`/.test(aeRaw)],
  ["Ae chat immediate save helper", /I\[e\]\.edit\(t,!1\),await I\[e\]\.flush/.test(aeRaw)],
  ["Ae owns Dispatch Assist in its own draft store", aeRaw.includes("P.dispatchAssist.getSnapshot().draft.qualities") && aeRaw.includes("P.dispatchAssist.edit")],
  ["Ae Dispatch Assist toggle commits through its own immediate helper", aeRaw.includes('W(`dispatchAssist`,{autoHelp:e,qualities:t,delaySeconds:[n,r],intervalSeconds:i})')],
  ["Ae Railway running disables departure-insufficient edit", aeRaw.includes('disabled:$(`railway`)||X?.running,onChange:e=>{W(`railway`,{departWhenTicketsInsufficient:e})')],
  ["Ae construction target uses immediate task helper", aeRaw.includes("automation.construction.targetEnabled") && aeRaw.includes('W(`construction`,{targetEnabled:e})')],
  ["Ae Ghost join uses immediate task helper", aeRaw.includes('W(`ghostRecon`,{autoJoinAlliance:e})')],
];
for (const [name, pass] of sourceContracts) assert.equal(pass, true, name);

const currentContractsChecks = [
  ["current centralizes discovered squad fixture", currentPage.includes("previewAutomationAvailableSquads(previewState)")],
  ["current Alliance Gather uses availableSquads", currentPage.includes("...availableSquads.filter((index) => !selected.includes(index))")],
  ["current Treasure uses availableSquads", currentPage.includes("...availableSquads.filter((index) => !selectedDispatchSquads.includes(index))")],
  ["current Treasure uses recovered priority row", currentPage.includes("automation-squad-priority-item") && currentPage.includes("draggedTreasureSquad")],
  ["current Treasure immediate save", currentPage.includes("config.store.edit((draft) => ({ ...draft, dispatchSquads: next }), false)")],
  ["current chat toggles immediate save", currentPage.includes('setNow("replyEnabled", value)') && currentPage.includes('setNow("treasureAutoDispatchEnabled", value)') === false && currentPage.includes('setNow("treasureDispatchEnabled", value)')],
  ["current card-owned local error precedes config", currentPage.indexOf("{fieldError ? <p role=\"alert\" className=\"automation-error\"") < currentPage.indexOf("{(hasSettings || actionLabel) ? <div className=\"automation-config\"")],
  ["current Resource Gather card error precedes config", (() => { const start = currentPage.indexOf("function ResourceGatherCard"); const error = currentPage.indexOf('{error ? <p className="automation-error" role="alert">', start); const config = currentPage.indexOf('<div className="automation-config">', start); return start >= 0 && error > start && error < config; })()],
  ["current global config errors precede panel title", (() => { const start = currentPage.indexOf("export function AutomationPage"); const error = currentPage.indexOf("Object.entries(configStates).map", start); const title = currentPage.indexOf("<PanelTitle", start); return start >= 0 && error > start && error < title; })()],
  ["current removes invented runtime action-failed alerts", !currentPage.includes('previewState === "automation-runtime-error"')],
  ["current keeps two Daily Activity groups", (currentPage.match(/visitedCategories\.has\("daily"\)/g) ?? []).length === 2],
  ["fixtures include non-contiguous and empty squad discovery", currentFixtures.includes('previewState === "automation-squads-34"') && currentFixtures.includes('"automation-squads-empty"')],
  ["contracts use non-contiguous fixture in gather and treasure", (currentContracts.match(/automation-squads-34/g) ?? []).length >= 2],
  ["Official Application local validation is source constrained", currentContracts.includes("automation.positionError")],
  ["Railway/Secret delay validation uses source error", currentContracts.includes('key === "delayMinutes" ? "automation.dailyDelayError"')],
  ["current separates Dispatch Assist draft ownership", currentPage.includes('initialAutomationDraft("Dispatch Assist", previewState)') && currentPage.includes('onConfigStatus?.("Dispatch Assist"') && currentContracts.includes('title === "Dispatch Assist"') && !currentContracts.includes("dispatchAssistEnabled")],
  ["current Assist immediate saves are isolated", currentPage.includes('commitAssistNow({ autoHelp: value })') && currentPage.includes('commitAssistNow({ qualities })') && currentPage.includes('assistConfig.store.edit(next, false)')],
  ["current immediate task controls use setNow/checkNow", currentPage.includes('checkNow("autoClaimCompleted", true)') && currentPage.includes('setNow("positionId", event.target.value)') && currentPage.includes('checkNow("collectRewards", false)') && currentPage.includes('setNow("ghostFilter", filter)') && currentPage.includes('setNow("allianceGatherSquads", next)')],
  ["current Railway departure setting disables while running", currentPage.includes('disabled={!enabled || runtime.running === true}') && currentPage.includes('checkNow("departWhenTicketsInsufficient")')],
  ["current retains last non-empty discovered squad provider", currentPage.includes('if (previewEnabled && discoveredSquads.length > 0) setAvailableSquads(discoveredSquads)')],
  ["current Trade fetch error preserves recovered non-alert DOM", currentPage.includes('{fixture.error ? <div className="automation-error">{fixture.error}</div> : null}') && !currentPage.includes('{fixture.error ? <div className="automation-error" role="alert">')],
];
for (const [name, pass] of currentContractsChecks) assert.equal(pass, true, name);

const browser = JSON.parse(read(browserPath));
assert.equal(browser.marker, "LWB317_REMAINING_M3_BROWSER_CURRENT_OK");
assert.ok(browser.cases.length >= 148, "browser matrix unexpectedly shrank");
assert.ok(browser.screenshots.length >= 23, "browser screenshot matrix unexpectedly shrank");
assert.equal(browser.console.filter((entry) => entry.type === "error" || entry.type === "pageerror").length, 0, "browser console errors");
const expectedCurrentHashes = {
  [pagePath]: sha(currentPage),
  [contractsPath]: sha(currentContracts),
  [fixturesPath]: sha(currentFixtures),
};
for (const [relative, expected] of Object.entries(expectedCurrentHashes)) {
  assert.equal(String(browser.sourceFiles?.[relative] ?? "").toUpperCase(), expected, `stale browser packet for ${relative}`);
}

const sourcePixels = JSON.parse(read(`${sourceRenderRoot}/current/decoded-pixel-results.json`));
const sourcePairs = JSON.parse(read(`${sourceRenderRoot}/current/pairs.json`));
const sourceDependencies = JSON.parse(read(`${sourceRenderRoot}/dependencies.json`));
assert.equal(sourcePixels.pairs, 56, "source-render pair matrix unexpectedly changed");
assert.equal(sourcePixels.exactPixels, 56, "source-render original/current pixels are not exact");
assert.equal(sourcePixels.cases.reduce((total, entry) => total + entry.changedPixels, 0), 0, "source-render has changed pixels");
assert.equal(sourcePairs.errors.length, 0, "source-render browser console issues");
const sourcePageDependency = sourceDependencies.files.find((entry) => entry.path === pagePath);
assert.ok(sourcePageDependency, "source-render dependency packet missing AutomationPage.jsx");
assert.equal(String(sourcePageDependency.sha256).toUpperCase(), sha(currentPage), "stale source-render packet for AutomationPage.jsx");

const result = {
  marker: "LWB317_REMAINING_M3_VALIDATION_OK",
  recoveredAe: aeLocator,
  recoveredAutomationCard: { sha256: sha(cardAsset), functionSha256: sha(cardRaw) },
  immutableBaseline: baseline,
  current: expectedCurrentHashes,
  sourceContracts: sourceContracts.length,
  currentContracts: currentContractsChecks.length,
  browser: { assertions: browser.cases.length, screenshots: browser.screenshots.length, consoleErrors: 0, version: browser.browser.version },
  sourceRenderer: { pairs: sourcePixels.pairs, exactPixels: sourcePixels.exactPixels, changedPixels: 0, consoleIssues: sourcePairs.errors.length },
  inheritedAcceptedUnitC: "Lower-helper exact original-side replays remain preserved; this task-local gate supersedes only current-only whole-composition expectations proved incorrect by exact Ae.",
};
fs.writeFileSync(path.join(here, "validation-results.json"), `${JSON.stringify(result, null, 2)}\n`);
console.log(JSON.stringify(result, null, 2));
