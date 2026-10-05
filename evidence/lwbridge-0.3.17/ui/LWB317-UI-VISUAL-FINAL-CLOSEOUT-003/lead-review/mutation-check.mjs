import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath, pathToFileURL } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../../");
const requireUi = createRequire(path.join(repo, "src/LWBridge.UI-0.3.17/package.json"));
const parser = requireUi("@babel/parser");

function nodes(source, jsx = true) {
  const result = [];
  const walk = (node) => {
    if (!node || typeof node !== "object") return;
    if (node.type) result.push(node);
    for (const value of Object.values(node)) {
      if (Array.isArray(value)) value.forEach(walk);
      else if (value && typeof value === "object") walk(value);
    }
  };
  walk(parser.parse(source, { sourceType: "module", plugins: jsx ? ["jsx"] : [] }));
  return result;
}
function evaluate(source, node, environment, prefix = "") {
  return new Function(...Object.keys(environment), `${prefix}return (${source.slice(node.start, node.end)});`)(...Object.values(environment));
}
function deferred() {
  let resolve;
  const promise = new Promise((onResolve) => { resolve = onResolve; });
  return { promise, resolve };
}
async function settle() { await Promise.resolve(); await new Promise((resolve) => setTimeout(resolve, 0)); }

async function appSemantics(source) {
  const parsed = nodes(source);
  const { unwrapProfileEvent } = await import(pathToFileURL(path.join(repo, "src/LWBridge.UI-0.3.17/src/mapBackend.js")).href);
  const recoveryEffect = parsed.find((node) => node.type === "CallExpression" && node.callee.name === "useEffect" && source.slice(node.start, node.end).includes("game_recovery_status"))?.arguments[0];
  const readStatusNode = parsed.find((node) => node.type === "VariableDeclarator" && node.id.name === "readStatusSnapshot")?.init?.arguments?.[0];
  const pollEffect = parsed.find((node) => node.type === "CallExpression" && node.callee.name === "useEffect" && source.slice(node.start, node.end).includes("const pollStatus = async () =>"))?.arguments[0];
  const callbacks = parsed.find((node) => node.type === "VariableDeclarator" && node.id.name === "profilePreviewCallbacks");
  const selectNode = callbacks?.init?.consequent?.properties?.find((node) => node.key.name === "onSelect")?.value;
  if (!recoveryEffect || !readStatusNode || !pollEffect || !selectNode) return { recoveryOwned: false, pollOwned: false, loadingOwned: false };

  let listener;
  const accepted = [];
  const selectedRef = { current: "A" };
  const cleanup = evaluate(source, recoveryEffect, {
    backendBridge: { available: true, listen: (_name, callback) => { listener = callback; return () => {}; }, invoke: () => new Promise(() => {}) },
    selectedProfileId: "A", selectedProfileIdRef: selectedRef, setGameRecoveryStatus: (value) => accepted.push(value), unwrapProfileEvent,
  })();
  listener({ profileId: "B", payload: { state: "bad" } });
  listener({ profileId: "A", payload: { state: "good" } });
  selectedRef.current = "B";
  listener({ profileId: "A", payload: { state: "late" } });
  cleanup();
  const recoveryOwned = accepted.length === 1 && accepted[0]?.state === "good";

  const proxyRequests = [];
  const writes = [];
  const refs = { reconnectStatusGeneration: { current: 0 }, autoLaunchSaveRevisionRef: { current: 0 }, autoLaunchNativeCommitEpochRef: { current: 0 }, autoLaunchConfigPollGenerationRef: { current: 0 }, autoLaunchCommittedRef: { current: false } };
  const backendBridge = { available: true, invoke: () => Promise.resolve({ autoLaunchGame: true }) };
  const mapApi = { readStatus: () => Promise.resolve({}), readProxyStatus: () => { const request = deferred(); proxyRequests.push(request); return request.promise; }, listenStatus: () => () => {}, listenScanStatus: () => () => {} };
  const readStatusSnapshot = evaluate(source, readStatusNode, { backendBridge, ...refs, mapApi, acknowledgeRuntimeStatus: (value) => writes.push(value), setProxyStatus: (value) => writes.push(value), setConnectionError: (value) => writes.push(value) });
  const pollRef = { current: "A" };
  let tick;
  const effect = evaluate(source, pollEffect, { backendBridge, selectedProfileId: "A", selectedProfileIdRef: pollRef, mapApi, acknowledgeRuntimeStatus: (value) => writes.push(value), acknowledgeMapScan() {}, readStatusSnapshot, window: { setInterval: (callback) => { tick = callback; return 1; }, clearInterval() {} } });
  const stop = effect();
  tick();
  const overlapRequests = proxyRequests.length;
  pollRef.current = "B";
  proxyRequests[0]?.resolve({ gameRunning: true });
  await settle();
  stop();
  const pollOwned = overlapRequests === 1 && writes.length === 0;

  let selected = "A", loading = false;
  const frames = [], cache = new Set(["A"]), generation = { current: 0 };
  const select = evaluate(source, selectNode, {
    shellProfiles: { selectedProfileId: "A" }, previewProfileCache: { current: cache }, previewProfileLoadGeneration: generation,
    setPreviewProfileLoading: (value) => { loading = value; }, setShellProfiles: (change) => { selected = change({ selectedProfileId: selected }).selectedProfileId; },
    window: { requestAnimationFrame: (callback) => frames.push(callback) },
  });
  const b = select("B"); frames.shift()?.(); const c = select("C"); frames.shift()?.(); await b;
  const afterB = { selected, loading, pending: frames.length };
  frames.shift()?.(); frames.shift()?.(); await c;
  const loadingOwned = afterB.selected === "C" && afterB.loading === true && afterB.pending === 1 && loading === false;
  return { recoveryOwned, pollOwned, loadingOwned };
}

function bSemantics(squads, join) {
  const parsedSquads = nodes(squads);
  const availableTargets = parsedSquads.find((node) => node.type === "VariableDeclarator" && node.id?.name === "availableTargets" && node.init?.type === "ConditionalExpression");
  const loadingConditional = availableTargets?.init
    && squads.slice(availableTargets.init.test.start, availableTargets.init.test.end).includes("squads-profile-target-loading")
    && availableTargets.init.consequent?.type === "ArrayExpression"
    && availableTargets.init.consequent.elements.length === 0;
  const joinNodes = nodes(join);
  const nativeDialogCall = joinNodes.some((node) => node.type === "CallExpression"
    && node.callee?.type === "MemberExpression"
    && node.callee.object?.name === "A"
    && node.callee.property?.name === "jsx"
    && node.arguments?.[0]?.type === "StringLiteral"
    && node.arguments[0].value === "dialog");
  const showModal = joinNodes.some((node) => node.type === "CallExpression" && join.slice(node.start, node.end).includes("showModal()"));
  const focusRestore = joinNodes.some((node) => node.type === "CallExpression" && join.slice(node.start, node.end).includes("focus({ preventScroll: true })"));
  return { loadingStartsEmpty: Boolean(loadingConditional), nativeJoinDialog: nativeDialogCall && showModal && focusRestore };
}

function aAutomationSemantics(source) {
  const parsed = nodes(source);
  const emptyDefault = (stateName) => parsed.some((node) => node.type === "VariableDeclarator"
    && node.id?.type === "ArrayPattern"
    && node.id.elements?.[0]?.name === stateName
    && node.init?.type === "CallExpression"
    && node.init.callee?.name === "fieldState"
    && node.init.arguments?.[1]?.type === "ArrayExpression"
    && node.init.arguments[1].elements.length === 0);
  return { trainMissingArraysSelectZero: emptyDefault("normalFixedCarriageIds") && emptyDefault("vipFixedCarriageIds") };
}

async function cSemantics(fixturesSource, automationSource, appSource = "") {
  const uri = `data:text/javascript;base64,${Buffer.from(fixturesSource).toString("base64")}`;
  const fixture = await import(`${uri}#milestone-e`);
  const loadingRetained = fixture.previewTradeFixture("automation-trade-loading-retained");
  const errorRetained = fixture.previewTradeFixture("automation-trade-error-retained");
  const squads = fixture.previewAutomationAvailableSquads("automation-squads-34");
  const retainedTrade = loadingRetained.loading === true && loadingRetained.goods.length > 0 && Boolean(errorRetained.error) && errorRetained.goods.length > 0;
  const discoveredSquads = JSON.stringify(squads) === JSON.stringify([3, 4]);
  const categoryActivity = nodes(automationSource).filter((node) => node.type === "JSXIdentifier" && node.name === "Activity").length >= 7;
  const profileScopedDrafts = automationSource.includes('const profileScope = profileId || "default";')
    && automationSource.includes("automation:${profileScope}:${previewState}:${title}")
    && automationSource.includes("automation:${profileScope}:${previewState}:${title}:dispatchAssist")
    && automationSource.includes('automation:${profileId || "default"}:gather:${previewState}')
    && automationSource.includes('automation:${profileId || "default"}:trade:${previewState}')
    && appSource.includes("automation: { profileId: selectedProfileId, activeCategory: automationCategory");
  return { retainedTrade, discoveredSquads, categoryActivity, profileScopedDrafts };
}

export async function runMutationCheck() {
  const appPath = path.join(repo, "src/LWBridge.UI-0.3.17/src/App.jsx");
  const automationPath = path.join(repo, "src/LWBridge.UI-0.3.17/src/AutomationPage.jsx");
  const fixturesPath = path.join(repo, "src/LWBridge.UI-0.3.17/src/previewAutomationFixtures.js");
  const squadsPath = path.join(repo, "src/LWBridge.UI-0.3.17/src/SquadsPage.jsx");
  const joinPath = path.join(repo, "src/LWBridge.UI-0.3.17/src/RallyJoinSettings.jsx");
  const app = fs.readFileSync(appPath, "utf8"), automation = fs.readFileSync(automationPath, "utf8"), fixtures = fs.readFileSync(fixturesPath, "utf8"), squads = fs.readFileSync(squadsPath, "utf8"), join = fs.readFileSync(joinPath, "utf8");
  const baseline = { A: { ...await appSemantics(app), ...aAutomationSemantics(automation) }, B: bSemantics(squads, join), C: await cSemantics(fixtures, automation, app) };
  assert.ok(Object.values(baseline.A).every(Boolean), JSON.stringify(baseline.A));
  assert.ok(Object.values(baseline.B).every(Boolean), JSON.stringify(baseline.B));
  assert.ok(Object.values(baseline.C).every(Boolean), JSON.stringify(baseline.C));

  const mutations = [];
  async function detect(name, milestone, mutate, evaluator, contract) {
    let detected = false;
    try {
      const value = await evaluator(...mutate());
      detected = value[contract] === false;
    } catch {
      detected = true;
    }
    mutations.push({ name, milestone, contract, detected });
    assert.equal(detected, true, `${name} was not detected`);
  }

  await detect("accept cross-profile recovery envelope", "A", () => [app.replace("const payload = unwrapProfileEvent(event, profileId);", "const payload = event?.payload ?? event;")], appSemantics, "recoveryOwned");
  await detect("remove recurring status in-flight fence", "A", () => [app.replace("if (inFlight) return;", "if (false) return;")], appSemantics, "pollOwned");
  await detect("let abandoned profile clear current loading", "A", () => [app.replace("if (previewProfileLoadGeneration.current === loadGeneration) setPreviewProfileLoading(false);", "setPreviewProfileLoading(false);")], appSemantics, "loadingOwned");
  await detect("restore invented Train missing-array selections", "A", () => [automation.replace('fieldState("normalFixedCarriageIds", [], true)', 'fieldState("normalFixedCarriageIds", [1], true)')], aAutomationSemantics, "trainMissingArraysSelectZero");
  await detect("populate AFK targets during first loading", "B", () => [squads.replace('previewState === "squads-profile-target-loading"\n    ? []', 'previewState === "squads-profile-target-loading"\n    ? previewAfkTargets') , join], bSemantics, "loadingStartsEmpty");
  await detect("replace recovered Join dialog with div", "B", () => [squads, join.replace('return A.jsx("dialog", {', 'return A.jsx("div", {')], bSemantics, "nativeJoinDialog");
  await detect("drop retained Trade goods during refresh/error", "C", () => [fixtures.replace(/const retainedGoods = [^;]+;/, "const retainedGoods = false;"), automation, app], cSemantics, "retainedTrade");
  await detect("collapse Automation category Activities", "C", () => [fixtures, automation.replaceAll("<Activity mode=", "<div data-mutated-mode=").replaceAll("</Activity>", "</div>"), app], cSemantics, "categoryActivity");
  await detect("remove Automation profile identity from config-store scopes", "C", () => [fixtures, automation.replaceAll("${profileScope}:", "default:"), app], cSemantics, "profileScopedDrafts");

  return { marker: "LWB317_FINAL_CLOSEOUT_MILESTONE_E_MUTATIONS_DETECTED", baseline, mutations };
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const result = await runMutationCheck();
  console.log(JSON.stringify(result, null, 2));
}
