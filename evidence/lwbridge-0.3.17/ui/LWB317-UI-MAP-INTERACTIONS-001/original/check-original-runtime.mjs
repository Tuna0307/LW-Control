// Executes the ORIGINAL Map panel component (through original-runtime.mjs) and records what it does.
//
//   node .../original/check-original-runtime.mjs           # run, print summary, exit 1 on a failed claim
//   node .../original/check-original-runtime.mjs --record  # also write original/results.json
//
// `observed` is data produced by the original code. `claims` compare that data with the plain-language statements of
// contract.json (the reviewer's reading); a failed claim means the READING was wrong, never that the original is.
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import {
  boot, drain, since, summarize, definedKeys, dispatchRow, truckRow, cityRow, openTab, checkboxView, buttonView, filterCheckbox,
  optionsReply, NOW, SERVER, createOriginalHarness, nodeText, treeNodes,
} from "./common.mjs";
import { loadPanel, rawOf, originalDir } from "./lib.mjs";
import { ORIGINAL_SHA256 } from "./original-runtime.mjs";

const record = process.argv.includes("--record");
const show = process.argv.filter((a) => a.startsWith("--show=")).map((a) => a.slice(7));
const results = { evidenceNote: "observed = data produced by executing the original component R; claims = contract statements checked against it", sourceSha256: ORIGINAL_SHA256, scenarios: {} };
let failedClaims = 0;
let totalClaims = 0;

async function scenario(ids, description, fn) {
  const list = Array.isArray(ids) ? ids : [ids];
  const claims = [];
  const claim = (text, held, detail) => { claims.push({ text, held: held === true, ...(detail === undefined ? {} : { detail }) }); };
  let observed;
  try { observed = await fn(claim); } catch (error) { observed = { error: String(error?.stack || error) }; claim("scenario ran without exception", false); }
  results.scenarios[list[0]] = { description, observed, claims };
  for (const alias of list.slice(1)) results.scenarios[alias] = { sameAs: list[0] };
  for (const c of claims) { totalClaims += 1; if (!c.held) { failedClaims += 1; console.log(`FAIL [${list[0]}] ${c.text}`); } }
  console.log(`${claims.every((c) => c.held) ? "ok  " : "FAIL"} ${list.join(", ")} (${claims.length} claims)`);
  if (show.includes(list[0])) console.log(JSON.stringify(observed, null, 1));
}

const E = (name) => ({ target: { value: name } });
const pickSelect = async (h, key, value) => { const s = h.findSelect(key); assert.ok(s, `select ${key}`); s.props.onChange({ target: { value } }); await h.settle(); };
const stateSnap = (h, names) => Object.fromEntries(names.map((n) => [n, h.getState(n)]));
const baseSnap = (h) => ({ tab: h.getState("tab"), page: h.getState("page"), total: h.getState("total"), rows: h.getState("rows").map((r) => r.uuid), loading: h.getState("loading"), message: h.getState("message"), keyword: h.getState("keyword") });
const lastCall = (h, name) => h.callsNamed(name).at(-1);
const jobsReply = { dispatchJobs: [], truckJobs: [] };

// ======================================================================================================================
await scenario("harness-executes-original-bytes", "The harness evaluates the unmodified component text of the asset", async (claim) => {
  const P = loadPanel();
  const h = await boot();
  const sameText = h.component.toString() === rawOf(P.entry, P.R);
  claim("component source equals the asset bytes of function R", sameText);
  claim("pinned asset hash", h.sourceSha256 === "ce74345518be72e417a510b982c591729e5683f69751e190805598c4c05d3089");
  const types = [...new Set(treeNodes(h.tree()).filter((n) => typeof n.type === "function").map((n) => n.type.displayName || n.type.name))].sort();
  return { sourceSha256: h.sourceSha256, componentTextIdenticalToAsset: sameText, childComponentTypesInTree: types, importSources: h.importSources, stateCount: h.stateNames.length };
});

await scenario("kw-typing-no-request", "Typing in the keyword input: no request, no timer, no page change", async (claim) => {
  const h = await boot(); await drain(h);
  const n = h.requests.length; const timers0 = h.timers();
  const steps = [];
  for (const v of ["a", "ab", "abc"]) { await h.typeKeyword(v); steps.push({ typed: v, newRequests: h.requests.length - n, keyword: h.getState("keyword") }); }
  await h.advance(60000);
  const out = { requestsAfterTyping: steps.at(-1).newRequests, requestsAfterIdle60s: h.requests.length - n, steps, timersBefore: timers0, timersAfter: h.timers(), page: h.getState("page"), loading: h.getState("loading") };
  claim("no request while typing", out.requestsAfterTyping === 0);
  claim("no request after 60 s of idle fake time (no debounce)", out.requestsAfterIdle60s === 0);
  claim("no timers registered by typing", out.timersAfter.timeouts.length === 0 && out.timersAfter.intervals.length === 0);
  claim("keyword state holds the raw text", h.getState("keyword") === "abc");
  return out;
});

await scenario(["kw-shared-across-tabs", "kw-tab-change-uses-typed-keyword"], "Unsubmitted keyword survives tab changes and is sent by the tab-entry request", async (claim) => {
  const h = await boot(); await drain(h);
  await h.typeKeyword("abc");
  const n = h.requests.length;
  await h.clickTab("resource");
  const first = since(h, n);
  await drain(h);
  const m = h.requests.length;
  await h.clickTab("monster");
  const second = since(h, m);
  const out = { afterResourceTab: first, afterMonsterTab: second, keywordAfter: h.getState("keyword"), inputValueShown: h.findInput("map.searchLabel").props.value };
  claim("resource tab entry request carries the typed keyword", first.some((r) => r.kind === "resource" && r.keyword === "abc"));
  claim("monster tab entry request carries the typed keyword", second.some((r) => r.kind === "monster" && r.keyword === "abc"));
  claim("keyword input still shows the keyword", out.inputValueShown === "abc");
  return out;
});

await scenario("kw-filter-change-uses-typed-keyword", "A non-keyword filter change sends the typed (unsubmitted) keyword", async (claim) => {
  const h = await boot(); await drain(h);
  await h.typeKeyword("zzz");
  const n = h.requests.length;
  await pickSelect(h, "map.allianceFilter", "none");
  const reqs = h.requests.slice(n);
  const out = { newRequests: reqs.map((r) => ({ kind: r.kind, query: definedKeys(r.query).reduce((o, k) => ({ ...o, [k]: r.query[k] }), {}) })) };
  claim("exactly one new request", reqs.length === 1);
  claim("request has keyword zzz and withoutAlliance true", reqs[0]?.query.keyword === "zzz" && reqs[0]?.query.withoutAlliance === true);
  return out;
});

await scenario(["kw-clears-name-selection", "kw-first-keystroke-with-name-requests"], "Typing while a name is selected clears the selection; the first keystroke issues a request (At dependency), later ones do not", async (claim) => {
  const h = await boot(); await openTab(h, "resource");
  await h.selectName("r2"); await drain(h);
  const before = h.getState("nameSelection");
  const n = h.requests.length;
  await h.typeKeyword("a");
  const first = since(h, n);
  const nameAfterFirst = h.getState("nameSelection");
  await drain(h);
  const m = h.requests.length;
  await h.typeKeyword("ab");
  const out = { nameBefore: before, nameAfterFirstKeystroke: nameAfterFirst, requestsAfterFirstKeystroke: first, requestsAfterSecondKeystroke: since(h, m), selectValueShown: h.findSelect("common.name").props.value };
  claim("name selection cleared by typing", nameAfterFirst.resource === undefined);
  claim("first keystroke issued exactly one request, with the new keyword and no name key", first.length === 1 && first[0].keyword === "a" && first[0].resourceNameKey === undefined);
  claim("second keystroke issued no request", out.requestsAfterSecondKeystroke.length === 0);
  return out;
});

await scenario("kw-options-refresh-refires-search", "Every options reply replaces At with a new object and so re-fires the search effect", async (claim) => {
  const h = await boot({ stubs: { dataOptions: { mode: "manual" } } });
  const before = h.getState("nameSelection");
  const afterMount = h.requests.map(summarize);
  const optionsCall = h.callsNamed("dataOptions").at(-1);
  await h.resolveCall(optionsCall, optionsReply()(SERVER));
  const after = h.getState("nameSelection");
  const out = { requestsAtMountBeforeOptionsReply: afterMount, requestsAfterOptionsReply: h.requests.map(summarize), nameSelectionIdentityChanged: before !== after, nameSelectionBefore: before, nameSelectionAfter: after, queriesIdentical: JSON.stringify(h.requests[0].query) === JSON.stringify(h.requests[1].query) };
  claim("one request at mount", afterMount.length === 1);
  claim("a second, identical request after the options reply", h.requests.length === 2 && out.queriesIdentical);
  claim("At identity changed although content is the same", out.nameSelectionIdentityChanged);
  return out;
});

await scenario(["rr-stale-reply-dropped", "search-while-loading"], "Overlapping searches: only the newest request's reply is applied; Search is clickable while loading", async (claim) => {
  const h = await boot(); await drain(h);
  await h.typeKeyword("one"); await h.clickSearch();
  const x = h.requests.at(-1);
  const loadingAfterFirst = h.getState("loading");
  const searchDuringLoading = buttonView(h.findButton("common.search"));
  await h.typeKeyword("two"); await h.clickSearch();
  const y = h.requests.at(-1);
  y.done = true; await h.resolveRequest(y, { rows: [cityRow("Y")], total: 1 });
  const afterY = { rows: h.getState("rows").map((r) => r.uuid), total: h.getState("total"), loading: h.getState("loading") };
  x.done = true; await h.resolveRequest(x, { rows: [cityRow("X")], total: 5 });
  const afterX = { rows: h.getState("rows").map((r) => r.uuid), total: h.getState("total"), loading: h.getState("loading") };
  const out = { requestIds: [x.id, y.id], loadingAfterFirstSearch: loadingAfterFirst, searchButtonWhileLoading: searchDuringLoading, afterNewestReply: afterY, afterObsoleteReply: afterX };
  claim("Search button not disabled while loading", searchDuringLoading.disabled === undefined);
  claim("second Search issued a second request while the first was pending", y.id === x.id + 1);
  claim("obsolete reply did not change rows/total/loading", JSON.stringify(afterX) === JSON.stringify(afterY));
  return out;
});

await scenario("rr-page-clamp", "A reply whose total makes the requested page too high clamps the page and stores no rows", async (claim) => {
  const h = await boot(); await drain(h, { rows: [cityRow("a")], total: 230 });
  await h.setPage(5);
  const req = h.requests.at(-1); req.done = true;
  const before = baseSnap(h);
  await h.resolveRequest(req, { rows: [cityRow("late")], total: 120 });
  const afterClamp = baseSnap(h);
  const followUp = h.requests.filter((r) => !r.done).map(summarize);
  const out = { before, afterClamp, followUpRequests: followUp };
  claim("page clamped to ceil(120/50)=3", afterClamp.page === 3);
  claim("rows of the clamped reply not stored", JSON.stringify(afterClamp.rows) === JSON.stringify(before.rows));
  claim("loading cleared", afterClamp.loading === false || followUp.length === 1);
  claim("a follow-up request for page 3 is issued", followUp.length === 1 && followUp[0].page === 3);
  return out;
});

await scenario("search-button-page1-vs-page-gt-1", "Search at page 1 requests directly; at page>1 it only resets the page and the effect requests once", async (claim) => {
  const h = await boot(); await drain(h, { rows: [], total: 230 });
  await h.typeKeyword("abc");
  let n = h.requests.length;
  await h.clickSearch();
  const atPage1 = { newRequests: since(h, n), renderedPage: h.getState("page") };
  await drain(h, { rows: [], total: 230 });
  await h.setPage(3); await drain(h, { rows: [], total: 230 });
  await h.typeKeyword("xyz");
  n = h.requests.length;
  await h.clickSearch();
  const atPage3 = { newRequests: since(h, n), pageAfter: h.getState("page") };
  const out = { atPage1, atPage3 };
  claim("page 1: one request carrying the typed keyword, page unchanged", atPage1.newRequests.length === 1 && atPage1.newRequests[0].keyword === "abc" && atPage1.newRequests[0].page === 1);
  claim("page 3: exactly one request, page 1, keyword xyz; page state reset to 1", atPage3.newRequests.length === 1 && atPage3.newRequests[0].page === 1 && atPage3.newRequests[0].keyword === "xyz" && atPage3.pageAfter === 1);
  return out;
});

await scenario("search-button-no-disabled", "The Search button has no disabled predicate: observed in loading, idle, offline, scanning and busy states", async (claim) => {
  const h = await boot();
  const snaps = {};
  const probe = (name) => { const b = h.findButton("common.search"); snaps[name] = { disabled: b.props.disabled, propNames: Object.keys(b.props) }; };
  probe("loadingFirstRequestPending");
  await drain(h); probe("idle");
  await h.setProps({ online: false }); probe("offline");
  await h.setProps({ online: true });
  await h.emitScanState({ isReading: true }); probe("scanReading");
  await h.emitScanState({ isReading: false });
  await h.clickTab("dispatch"); await drain(h, { rows: [dispatchRow("1001")], total: 1 });
  await h.toggleRow(0);
  const manual = h.setStubBehavior("dispatchPlunderSchedule", { mode: "manual" });
  h.findButton("map.scheduleSelected", { count: 1 }).props.onClick(); await h.settle();
  snaps.scheduleBusy = { busyKey: h.getState("busyKey") }; probe("scheduleBusy");
  claim("disabled is undefined in every state", Object.values(snaps).every((s) => s.disabled === undefined || s.busyKey !== undefined));
  claim("props are exactly onClick and children", JSON.stringify(snaps.idle.propNames) === JSON.stringify(["onClick", "children"]));
  return snaps;
});

await scenario("export-city-query", "City export reuses nr(1,200); button/label/busy/message behaviour", async (claim) => {
  const h = await boot({ stubs: { cityExport: { mode: "manual" } } });
  await drain(h, { rows: [cityRow("a")], total: 230 });
  await h.setPage(2); await drain(h, { rows: [], total: 230 });
  await h.typeKeyword("kk");
  const btn = h.findButton("map.exportExcel");
  const idle = buttonView(btn);
  btn.props.onClick(); await h.settle();
  const call = lastCall(h, "cityExport");
  const busy = buttonView(h.findButton("map.exportingExcel"));
  await h.resolveCall(call, { canceled: false, rowCount: 7, path: "C:/x.xlsx" });
  const out = { idle, busyDuringExport: busy, queryKeys: definedKeys(call.args[0]), query: { keyword: call.args[0].keyword, page: call.args[0].page, pageSize: call.args[0].pageSize, serverId: call.args[0].serverId }, options: { sheetName: call.args[1].sheetName, headerCount: call.args[1].headers.length }, messageAfter: h.getState("message"), exportingAfter: h.getState("exporting") };
  claim("export query is nr(1,200) with the typed keyword", out.query.page === 1 && out.query.pageSize === 200 && out.query.keyword === "kk");
  claim("button disabled with busy label during export", busy.disabled === true);
  claim("success message set", out.messageAfter === "map.exportExcelSuccess");
  return out;
});

await scenario("nr-per-tab-fields", "Query projection per tab (defined keys) and with filters applied", async (claim) => {
  const viewer = { mode: "auto", value: () => ({ playerUid: "p1", allianceId: "a1", states: [], batch: null }) };
  const h = await boot({ stubs: { treasureClaimStatus: viewer, treasureStateRefreshAll: viewer } });
  await drain(h);
  const perTab = {};
  for (const kind of ["city", "resource", "monster", "truck", "railway", "dispatch", "ghost", "treasure"]) {
    const n = h.requests.length;
    if (kind !== "city") await h.clickTab(kind);
    await h.settle();
    const req = (kind === "city" ? h.requests : h.requests.slice(n)).find((r) => r.kind === kind);
    assert.ok(req, `request for tab ${kind}`);
    perTab[kind] = { kind: req.kind, definedKeys: definedKeys(req.query) };
    await drain(h);
  }
  // filters
  await h.clickTab("dispatch"); await drain(h);
  await pickSelect(h, "map.level", "5"); await pickSelect(h, "map.quality", "special"); await pickSelect(h, "common.status", "completed");
  filterCheckbox(h, "map.plunderableOnly").props.onChange({ target: { checked: true } }); await h.settle();
  const dispatchFiltered = h.requests.at(-1).query; await drain(h);
  await h.clickTab("truck"); await drain(h);
  await pickSelect(h, "map.quality", "reindeer");
  const truckReindeer = h.requests.at(-1).query; await drain(h);
  await h.clickTab("city"); await drain(h);
  await pickSelect(h, "map.allianceFilter", `name:${encodeURIComponent("Foo Bar")}`);
  const cityAlliance = h.requests.at(-1).query; await drain(h);
  filterCheckbox(h, "map.markedOnly").props.onChange({ target: { checked: true } }); await h.settle();
  const cityMarked = h.requests.at(-1).query; await drain(h);
  await h.clickTab("resource"); await drain(h); await h.selectName("r1");
  const resourceNamed = h.requests.at(-1).query; await drain(h);
  const pick = (q, keys) => Object.fromEntries(keys.map((k) => [k, q[k]]));
  const out = {
    perTab,
    dispatchFiltered: pick(dispatchFiltered, ["minLevel", "maxLevel", "quality", "specialOnly", "reindeerOnly", "completionStatus", "plunderableOnly"]),
    truckReindeer: pick(truckReindeer, ["quality", "specialOnly", "reindeerOnly", "plunderableOnly"]),
    cityAlliance: pick(cityAlliance, ["alliance", "withoutAlliance", "markedOnly"]), cityMarked: pick(cityMarked, ["alliance", "withoutAlliance", "markedOnly"]),
    resourceNamed: pick(resourceNamed, ["resourceNameKey", "monsterNameKey", "keyword"]),
    cityKeywordUntrimmed: null,
  };
  claim("dispatch level yields identical min/max (5/5)", out.dispatchFiltered.minLevel === 5 && out.dispatchFiltered.maxLevel === 5);
  claim("special quality is specialOnly with quality undefined", out.dispatchFiltered.specialOnly === true && out.dispatchFiltered.quality === undefined);
  claim("truck reindeer is reindeerOnly", out.truckReindeer.reindeerOnly === true && out.truckReindeer.quality === undefined);
  claim("city alliance decoded", out.cityAlliance.alliance === "Foo Bar");
  claim("city has no minLevel and resource tab has no alliance", !perTab.city.definedKeys.includes("minLevel") && !perTab.resource.definedKeys.includes("alliance"));
  await h.clickTab("city"); await drain(h);
  await h.typeKeyword("  Padded  "); await h.clickSearch();
  out.cityKeywordUntrimmed = JSON.stringify(h.requests.at(-1).query.keyword);
  claim("keyword is sent untrimmed", out.cityKeywordUntrimmed === JSON.stringify("  Padded  "));
  return out;
});

await scenario(["name-select-render", "name-option-text-derivation"], "Name select options and the text derivation (translation vs raw key, trimmed lookup)", async (claim) => {
  const names = { resource: [{ key: "r1", count: 3 }, { key: "r2", count: 5 }, { key: " r3 ", count: 1 }], monster: [] };
  const h = await boot({ stubs: { dataOptions: { mode: "auto", value: optionsReply({ names }) }, localize: { mode: "manual" } } });
  const cityHasSelect = !!h.findSelect("common.name");
  await openTab(h, "resource");
  const optionTexts = () => treeNodes(h.findSelect("common.name")).filter((n) => n.type === "option").map((o) => ({ value: o.props.value, text: nodeText(o) }));
  const noTexts = optionTexts();
  const locCalls = h.callsNamed("localize");
  const lastLoc = locCalls.at(-1);
  await h.resolveCall(lastLoc, { r1: "Iron", " r3 ": "Wood(untrimmed key)" });
  const rawKeyReply = optionTexts();
  const lastLoc2 = h.callsNamed("localize").at(-1);
  // re-run localize by changing rows
  await h.setPage(2); await drain(h, { rows: [], total: 230 });
  const loc3 = h.callsNamed("localize").at(-1);
  await h.resolveCall(loc3, { r1: "Iron", r3: "Wood(trimmed key)" });
  const trimmedReply = optionTexts();
  const out = { cityTabHasNameSelect: cityHasSelect, ariaLabel: h.translate("common.name"), optionsBeforeTranslation: noTexts, optionsAfterReplyKeyedByRawKey: rawKeyReply, optionsAfterReplyKeyedByTrimmedKey: trimmedReply, localizeKeysRequested: locCalls.at(-1).args[1], selectValue: h.findSelect("common.name").props.value };
  claim("no name select on the city tab", cityHasSelect === false);
  claim("first option is All names with empty value", noTexts[0].value === "" && noTexts[0].text === "map.allNames");
  claim("before translation the raw key + count is shown", noTexts[1].text === "r1 (3)");
  claim("translated text + count after reply", rawKeyReply[1].text === "Iron (3)");
  claim("untrimmed backend key does not match the trimmed lookup; raw key shown", rawKeyReply[3].text === " r3  (1)");
  claim("reply keyed by trimmed key is used", trimmedReply[3].text === "Wood(trimmed key) (1)");
  void lastLoc2;
  return out;
});

await scenario(["name-select-request", "name-select-resets-page-and-keyword"], "Selecting a name: one request, page 1, keyword cleared", async (claim) => {
  const h = await boot(); await openTab(h, "resource", [], 230);
  await h.setPage(3); await drain(h, { rows: [], total: 230 });
  await h.typeKeyword("abc");
  const n = h.requests.length;
  await h.selectName("r2");
  const reqs = since(h, n);
  const out = { requests: reqs, page: h.getState("page"), keyword: h.getState("keyword"), nameSelection: h.getState("nameSelection") };
  claim("one request with name key r2, keyword '' and page 1", reqs.length === 1 && reqs[0].resourceNameKey === "r2" && reqs[0].keyword === "" && reqs[0].page === 1);
  claim("page and keyword reset", out.page === 1 && out.keyword === "");
  await drain(h);
  await h.selectName("");
  out.afterAll = { nameSelection: h.getState("nameSelection"), requests: since(h, n + 1) };
  claim("choosing All stores undefined and issues a request without name key", out.afterAll.nameSelection.resource === undefined && out.afterAll.requests.at(-1)?.resourceNameKey === undefined);
  return out;
});

await scenario("name-selection-persists-across-tabs", "Name selections are per tab and survive tab changes", async (claim) => {
  const h = await boot(); await openTab(h, "resource");
  await h.selectName("r2"); await drain(h);
  await h.clickTab("monster"); await drain(h);
  await h.selectName("m1"); await drain(h);
  await h.clickTab("resource");
  const out = { nameSelection: h.getState("nameSelection"), selectValueOnResource: h.findSelect("common.name").props.value, requestOnReturn: summarize(h.requests.at(-1)) };
  claim("both selections kept", out.nameSelection.resource === "r2" && out.nameSelection.monster === "m1");
  claim("return to resource requests with resourceNameKey r2", out.requestOnReturn.resourceNameKey === "r2");
  return out;
});

await scenario("name-invalid-after-refresh-cleared", "A selected name that disappears from the refreshed options is cleared; a request follows", async (claim) => {
  const h = await boot(); await openTab(h, "resource");
  await h.selectName("r2"); await drain(h);
  h.setStubBehavior("dataOptions", { mode: "auto", value: optionsReply({ names: { resource: [{ key: "r1", count: 3 }], monster: [] } }) });
  const n = h.requests.length;
  await h.emitScanState({ isReading: true });
  await h.emitScanState({ isReading: false });
  const reqs = since(h, n);
  const out = { nameSelectionAfter: h.getState("nameSelection"), requestsAfterRefresh: reqs, optionsCalls: h.callsNamed("dataOptions").length };
  claim("selection cleared", out.nameSelectionAfter.resource === undefined);
  claim("a request without the stale name key was issued", reqs.length >= 1 && reqs.at(-1).resourceNameKey === undefined);
  return out;
});

await scenario(["tab-change-effects", "tab-cache-restore", "tab-change-same-tab-noop"], "Tab change: what is reset/restored/kept; same-tab click is a no-op; Scheduled keeps the current page/rows", async (claim) => {
  const h = await boot({ stubs: { cityExport: { mode: "auto", value: () => ({ canceled: false, rowCount: 1, path: "p" }) } } });
  await drain(h, { rows: [cityRow("c0")], total: 230 });
  await h.setPage(3); await drain(h, { rows: [cityRow("c1")], total: 230 });
  await h.typeKeyword("kw");
  h.findButton("map.exportExcel").props.onClick(); await h.settle();
  const before = baseSnap(h);
  const genBefore = h.getRef("searchGeneration").current;
  const n = h.requests.length;
  await h.clickTab("monster");
  const afterMonster = { ...baseSnap(h), requests: since(h, n), searchGenerationDelta: h.getRef("searchGeneration").current - genBefore, cacheKeys: [...h.getRef("tabCache").current.keys()] };
  await drain(h, { rows: [{ uuid: "m1", serverId: SERVER }], total: 1 });
  const m = h.requests.length;
  await h.clickTab("city");
  const afterBack = { ...baseSnap(h), requests: since(h, m) };
  await drain(h, { rows: [cityRow("c1")], total: 230 });
  const k = h.requests.length; const renders = h.renderCount();
  await h.clickTab("city");
  const sameTab = { requestsDelta: h.requests.length - k, snapshot: baseSnap(h) };
  const j = h.callsNamed("plunderJobsList").length; const kk = h.requests.length;
  await h.clickTab("scheduledPlunder");
  const afterScheduled = { ...baseSnap(h), searchRequestsDelta: h.requests.length - kk, jobListCallsDelta: h.callsNamed("plunderJobsList").length - j };
  await h.clickTab("city");
  const backFromScheduled = { ...baseSnap(h), requests: since(h, kk) };
  void renders;
  const out = { before, afterMonster, afterBack, sameTab, afterScheduled, backFromScheduled };
  claim("before: page 3, keyword kw, message set", before.page === 3 && before.keyword === "kw" && before.message === "map.exportExcelSuccess");
  claim("entering a new tab: page 1, rows [], total 0, loading true, message cleared, keyword kept", afterMonster.page === 1 && afterMonster.rows.length === 0 && afterMonster.total === 0 && afterMonster.loading === true && afterMonster.message === "" && afterMonster.keyword === "kw");
  claim("tab entry issues a request with the kept keyword", afterMonster.requests.length === 1 && afterMonster.requests[0].kind === "monster" && afterMonster.requests[0].keyword === "kw");
  claim("search generation bumped", afterMonster.searchGenerationDelta >= 1);
  claim("returning restores the cached page/rows/total immediately and still refetches (loading true)", afterBack.page === 3 && afterBack.rows[0] === "c1" && afterBack.total === 230 && afterBack.loading === true && afterBack.requests.length === 1 && afterBack.requests[0].page === 3);
  claim("same-tab click changes nothing and requests nothing", sameTab.requestsDelta === 0);
  claim("Scheduled: no search request, jobs reloaded, page/rows/total kept, loading false", afterScheduled.searchRequestsDelta === 0 && afterScheduled.jobListCallsDelta === 1 && afterScheduled.page === 3 && afterScheduled.loading === false);
  claim("leaving Scheduled restores the city cache and refetches", backFromScheduled.page === 3 && backFromScheduled.requests.length === 1);
  return out;
});

// ---- selection -------------------------------------------------------------------------------------------------------
await scenario("selection-dispatch-toggle", "Dispatch/Ghost selection: key, payload, toggle, counts, taskKind", async (claim) => {
  const h = await boot(); await openTab(h, "dispatch", [dispatchRow("1001"), dispatchRow("1002")]);
  const initial = { keys: h.getState("selectedDispatchKeys"), boxes: checkboxView(h), label: nodeText(h.findButton("map.scheduleSelected", { count: 0 })), disabled: h.findButton("map.scheduleSelected", { count: 0 }).props.disabled };
  await h.toggleRow(0);
  const one = { keys: h.getState("selectedDispatchKeys"), boxes: checkboxView(h), payload: h.getState("dispatchSelection")["321:1001"], label: nodeText(h.findButton("map.scheduleSelected", { count: 1 })) };
  await h.toggleRow(1);
  const two = { keys: h.getState("selectedDispatchKeys") };
  await h.toggleRow(0);
  const backToOne = { keys: h.getState("selectedDispatchKeys") };
  await h.clickTab("ghost"); await drain(h, { rows: [dispatchRow("2001")], total: 1 });
  await h.toggleRow(0);
  const ghost = { payloadTaskKind: h.getState("dispatchSelection")["321:2001"]?.taskKind, keys: h.getState("selectedDispatchKeys") };
  const out = { initial, one, two, backToOne, ghost, tableProps: Object.keys(h.tableNode().props).sort() };
  claim("key is serverId:uuid; payload is the row plus taskKind dispatch", one.keys[0] === "321:1001" && one.payload.taskKind === "dispatch" && one.payload.ownerName === "Owner 1001");
  claim("checkbox shows checked; label counts keys", one.boxes[0].checked === true && one.boxes[1].checked === false && one.label === "map.scheduleSelected:1");
  claim("toggling again removes", backToOne.keys.length === 1 && backToOne.keys[0] === "321:1002");
  claim("ghost payload taskKind ghost", ghost.payloadTaskKind === "ghost");
  claim("schedule button disabled with no selection", initial.disabled === true);
  return out;
});

await scenario(["selection-dispatch-cleared-on-tab-change", "selection-truck-toggle", "selection-truck-survives-tab-change"], "Dispatch selection is cleared by ANY tab change; truck selection survives tab/page/refetch/scan completion", async (claim) => {
  const h = await boot(); await openTab(h, "dispatch", [dispatchRow("1001")]);
  await h.toggleRow(0);
  const sel0 = h.getState("selectedDispatchKeys");
  await h.clickTab("ghost");
  const onGhost = h.getState("selectedDispatchKeys"); await drain(h);
  await h.clickTab("dispatch"); await drain(h, { rows: [dispatchRow("1001")], total: 1 });
  const backOnDispatch = h.getState("selectedDispatchKeys");
  await openTab(h, "truck", [truckRow("t1"), truckRow("t2")]);
  await h.toggleRow(0);
  const truck = { keys: h.getState("selectedTruckKeys"), payload: h.getState("truckSelection")["321:t1"], hasTaskKind: "taskKind" in h.getState("truckSelection")["321:t1"], boxes: checkboxView(h), label: nodeText(h.findButton("map.scheduleSelectedTrucks", { count: 1 })) };
  await h.clickTab("city"); await drain(h);
  const afterCity = h.getState("selectedTruckKeys");
  await h.clickTab("truck"); await drain(h, { rows: [truckRow("t1"), truckRow("t2")], total: 2 });
  const backOnTruck = { keys: h.getState("selectedTruckKeys"), boxes: checkboxView(h) };
  await h.setPage(2); await drain(h, { rows: [truckRow("t3")], total: 120 });
  const afterPage = h.getState("selectedTruckKeys");
  await h.clickSearch(); await drain(h, { rows: [truckRow("t3")], total: 120 });
  const afterSearch = h.getState("selectedTruckKeys");
  await h.emitScanState({ isReading: true }); await h.emitScanState({ isReading: false }); await drain(h, { rows: [truckRow("t3")], total: 120 });
  const afterScan = h.getState("selectedTruckKeys");
  const out = { dispatch: { selectedBefore: sel0, afterTabChangeToGhost: onGhost, afterReturn: backOnDispatch }, truck, truckAfterCityTab: afterCity, truckBackOnTruck: backOnTruck, afterPageChange: afterPage, afterSearchRefetch: afterSearch, afterScanCompletion: afterScan };
  claim("dispatch selection had 1 key then 0 after tab change", sel0.length === 1 && onGhost.length === 0 && backOnDispatch.length === 0);
  claim("truck payload is the plain row (no taskKind) under serverId:uuid", truck.keys[0] === "321:t1" && truck.hasTaskKind === false && truck.label === "map.scheduleSelectedTrucks:1");
  claim("truck selection survives city tab, page change, refetch, scan completion", afterCity.length === 1 && afterPage.length === 1 && afterSearch.length === 1 && afterScan.length === 1);
  claim("returning to truck shows the row checked", backOnTruck.boxes[0].checked === true);
  return out;
});

await scenario(["selection-survives-server-change", "server-change-effects"], "Server change: data server switched, page/rows/total reset, tab cache cleared; keyword/selection/name/delay/message retained; stale in-flight reply ignored", async (claim) => {
  const h = await boot({ stubs: { cityExport: { mode: "auto", value: () => ({ canceled: false, rowCount: 1, path: "p" }) } } });
  await openTab(h, "dispatch", [dispatchRow("1001")], 230);
  await h.toggleRow(0);
  await h.setRandomDelay("7");
  await h.typeKeyword("kw");
  await h.setPage(2); await drain(h, { rows: [dispatchRow("1002")], total: 230 });
  await openTab(h, "truck", [truckRow("t1")]);
  await h.toggleRow(0);
  await h.clickTab("resource"); await drain(h); await h.selectName("r2"); await drain(h);
  await h.clickTab("dispatch"); await drain(h, { rows: [dispatchRow("1001")], total: 1 });
  await h.toggleRow(0);
  await h.typeKeyword("kw"); // typed last: selecting a name clears the keyword
  const pendingOld = h.requests.length;
  await h.clickSearch(); const stale = h.requests.at(-1);
  const before = { ...baseSnap(h), dispatchKeys: h.getState("selectedDispatchKeys"), truckKeys: h.getState("selectedTruckKeys"), delay: h.getState("randomDelay"), nameSelection: h.getState("nameSelection"), cacheKeys: [...h.getRef("tabCache").current.keys()], dataServerId: h.getState("dataServerId") };
  const optionCallsBefore = h.callsNamed("dataOptions").length;
  await h.emitServer(456);
  const afterSwitch = { ...baseSnap(h), dispatchKeys: h.getState("selectedDispatchKeys"), truckKeys: h.getState("selectedTruckKeys"), delay: h.getState("randomDelay"), nameSelection: h.getState("nameSelection"), cacheKeys: [...h.getRef("tabCache").current.keys()], dataServerId: h.getState("dataServerId"), newRequests: since(h, pendingOld + 1), optionCalls: h.callsNamed("dataOptions").slice(optionCallsBefore).map((c) => c.args[0]) };
  stale.done = true; await h.resolveRequest(stale, { rows: [dispatchRow("OLD")], total: 99 });
  const afterStale = baseSnap(h);
  const out = { before, afterSwitch, afterStaleReply: afterStale };
  claim("data server follows scan state, page 1, rows [], total 0, loading true", afterSwitch.dataServerId === 456 && afterSwitch.page === 1 && afterSwitch.rows.length === 0 && afterSwitch.total === 0 && afterSwitch.loading === true);
  claim("tab cache cleared by the server effect", afterSwitch.cacheKeys.length === 0);
  claim("keyword, delay, name selection, both selections retained", afterSwitch.keyword === "kw" && afterSwitch.delay === "7" && afterSwitch.nameSelection.resource === "r2" && afterSwitch.dispatchKeys.length === 1 && afterSwitch.truckKeys.length === 1);
  claim("a request for the new server is issued and options re-fetched", afterSwitch.newRequests.some((r) => r.serverId === 456) && afterSwitch.optionCalls.includes(456));
  claim("stale reply of the old server is ignored", JSON.stringify(afterStale.rows) === JSON.stringify(afterSwitch.rows) && afterStale.total === afterSwitch.total);
  return out;
});

await scenario("server-loss-no-request", "Server id 0 (scan state without server): no search request, rows cleared, loading false; recovery requests again", async (claim) => {
  const h = await boot(); await drain(h, { rows: [cityRow("a")], total: 10 });
  await h.typeKeyword("kw");
  const n = h.requests.length; const optionsBefore = h.callsNamed("dataOptions").length;
  await h.emitServer(0);
  const lost = { ...baseSnap(h), newRequests: since(h, n), dataServerId: h.getState("dataServerId"), optionCalls: h.callsNamed("dataOptions").length - optionsBefore, tabCountsLoaded: h.getState("countsLoaded"), searchButtonDisabled: h.findButton("common.search").props.disabled, exportDisabled: h.findButton("map.exportExcel").props.disabled, clearServerDisabled: h.findButton("map.clearServer").props.disabled };
  await h.emitServer(789);
  const recovered = { ...baseSnap(h), newRequests: since(h, n), dataServerId: h.getState("dataServerId") };
  const out = { lost, recovered };
  claim("no search request at server 0; rows cleared; loading false", lost.newRequests.length === 0 && lost.rows.length === 0 && lost.total === 0 && lost.loading === false && lost.dataServerId === 0);
  claim("Search stays enabled; export and clear-server are disabled", lost.searchButtonDisabled === undefined && lost.exportDisabled === true && lost.clearServerDisabled === true);
  claim("recovery requests the new server with the kept keyword", recovered.newRequests.some((r) => r.serverId === 789 && r.keyword === "kw"));
  return out;
});

await scenario("clear-data-resets", "Clear-data handler: what is reset and what is kept", async (claim) => {
  const h = await boot();
  await openTab(h, "dispatch", [dispatchRow("1001")], 230);
  await h.toggleRow(0); await h.setRandomDelay("5");
  await openTab(h, "truck", [truckRow("t1")]); await h.toggleRow(0);
  await h.clickTab("resource"); await drain(h); await h.selectName("r2"); await drain(h);
  await h.clickTab("dispatch"); await drain(h, { rows: [dispatchRow("1001")], total: 1 }); await h.toggleRow(0);
  await h.typeKeyword("kw");
  const n = h.requests.length;
  const btn = h.findButton("map.clearServer");
  const disabled = btn.props.disabled;
  btn.props.onClick(); await h.settle();
  const scanClearCall = lastCall(h, "scanClear");
  const out = { clearButtonDisabledBefore: disabled, scanClearArgs: scanClearCall.args, after: { ...baseSnap(h), dispatchKeys: h.getState("selectedDispatchKeys"), truckKeys: h.getState("selectedTruckKeys"), nameSelection: h.getState("nameSelection"), delay: h.getState("randomDelay"), nameOptions: h.getState("nameOptions"), rowsRevision: h.getState("rowsRevision"), optionsRevision: h.getState("optionsRevision") }, newRequests: since(h, n), logs: h.logs.slice(-2) };
  claim("scan_clear called with the data server", scanClearCall.args[0] === SERVER);
  claim("both selections cleared, name selection reset", out.after.dispatchKeys.length === 0 && out.after.truckKeys.length === 0 && out.after.nameSelection.resource === undefined);
  claim("keyword and random delay are kept", out.after.keyword === "kw" && out.after.delay === "5");
  claim("rows revision bumped -> a refetch request follows", out.after.rowsRevision >= 1 && out.newRequests.length >= 1);
  return out;
});

await scenario("selection-key-trim-mismatch", "Stored key is untrimmed, lookup key trimmed: whitespace-padded uuids toggle but never show checked", async (claim) => {
  const h = await boot(); await openTab(h, "dispatch", [dispatchRow(" 1003 ")]);
  const box0 = checkboxView(h)[0];
  await h.toggleRow(0);
  const after = { keys: h.getState("selectedDispatchKeys"), box: checkboxView(h)[0], scheduleDisabled: h.findButton("map.scheduleSelected", { count: 1 })?.props.disabled, label: nodeText(h.findButton("map.scheduleSelected", { count: 1 })) };
  await h.toggleRow(0);
  const again = { keys: h.getState("selectedDispatchKeys") };
  await openTab(h, "truck", [truckRow(" t9 ")]);
  await h.toggleRow(0);
  const truck = { keys: h.getState("selectedTruckKeys"), box: checkboxView(h)[0] };
  const out = { dispatch: { boxBefore: box0, afterToggle: after, afterSecondToggle: again }, truck };
  claim("padded numeric uuid is enabled", box0.disabled === false);
  claim("stored key is untrimmed", after.keys[0] === "321: 1003 ");
  claim("checkbox does not show checked although counted", after.box.checked === false && after.label === "map.scheduleSelected:1" && after.scheduleDisabled === false);
  claim("second toggle removes the entry", again.keys.length === 0);
  claim("truck behaves the same", truck.keys[0] === "321: t9 " && truck.box.checked === false);
  return out;
});

await scenario("selection-duplicate-ids", "Duplicate serverId:uuid rows share one entry", async (claim) => {
  const h = await boot(); await openTab(h, "dispatch", [dispatchRow("1001", { ownerName: "first" }), dispatchRow("1001", { ownerName: "second" })]);
  await h.toggleRow(0);
  const after0 = { keys: h.getState("selectedDispatchKeys"), boxes: checkboxView(h), payloadOwner: h.getState("dispatchSelection")["321:1001"].ownerName };
  await h.toggleRow(1);
  const after1 = { keys: h.getState("selectedDispatchKeys"), boxes: checkboxView(h) };
  const out = { after0, after1 };
  claim("both checkboxes checked, one entry, payload of the toggled row", after0.boxes.every((b) => b.checked) && after0.keys.length === 1 && after0.payloadOwner === "first");
  claim("toggling the duplicate removes the shared entry", after1.keys.length === 0);
  return out;
});

await scenario("selection-checkbox-predicates", "Checkbox disabled predicate matrix (dispatch/ghost and truck)", async (claim) => {
  const variants = [
    ["ok", dispatchRow("1")], ["nonDigitUuid", dispatchRow("abc")], ["emptyUuid", dispatchRow("")], ["decimalUuid", dispatchRow("1.5")],
    ["noCompletionTime", dispatchRow("2", { completionTime: 0 })], ["noPlunderAt", dispatchRow("3", { plunderAt: 0 })],
    ["expired", dispatchRow("4", { taskExpireTime: NOW - 1 })], ["full", dispatchRow("5", { stolenCount: 3, maxStealCount: 3 })],
    ["pendingFutureCompletion", dispatchRow("6", { completionTime: NOW + 5000 })], ["protectedWindow", dispatchRow("7", { completionTime: NOW - 1000, plunderAt: NOW + 1000 })],
    ["secondsTimestamps", dispatchRow("8", { completionTime: Math.floor((NOW - 60000) / 1000), plunderAt: Math.floor((NOW + 3600000) / 1000) })],
    ["paddedNumericUuid", dispatchRow(" 9 ")], ["maxStealZero", dispatchRow("10", { maxStealCount: 0, stolenCount: 5 })],
  ];
  const h = await boot(); await openTab(h, "dispatch", variants.map(([, r]) => r));
  const dispatch = Object.fromEntries(checkboxView(h).map((b, i) => [variants[i][0], { disabled: b.disabled }]));
  await h.clickTab("ghost"); await drain(h, { rows: variants.map(([, r]) => r), total: variants.length });
  const ghost = Object.fromEntries(checkboxView(h).map((b, i) => [variants[i][0], { disabled: b.disabled }]));
  const tv = [
    ["ok", truckRow("t1")], ["emptyUuid", truckRow("")], ["blankUuid", truckRow("   ")], ["serverZero", truckRow("t2", { serverId: 0 })], ["serverFraction", truckRow("t3", { serverId: 1.5 })],
    ["expired", truckRow("t4", { arriveTs: NOW - 1 })], ["arriveNow", truckRow("t5", { arriveTs: NOW })], ["full", truckRow("t6", { robTimes: 3 })],
    ["specialURFullAfterOne", truckRow("t7", { isSpecialURQuality: true, robTimes: 1 })], ["protected", truckRow("t8", { protectTime: NOW + 5000 })], ["noMaxLoot", truckRow("t9", { maxLootCount: 0, robTimes: 9 })],
  ];
  await openTab(h, "truck", tv.map(([, r]) => r));
  const truck = Object.fromEntries(checkboxView(h).map((b, i) => [tv[i][0], { disabled: b.disabled }]));
  const out = { dispatch, ghost, truck };
  claim("dispatch: enabled only when uuid is digits AND completionTime>0 AND plunderAt>0 AND not expired/full", dispatch.ok.disabled === false && dispatch.nonDigitUuid.disabled && dispatch.emptyUuid.disabled && dispatch.decimalUuid.disabled && dispatch.noCompletionTime.disabled && dispatch.noPlunderAt.disabled && dispatch.expired.disabled && dispatch.full.disabled);
  claim("pending/protected windows and padded numeric uuid are enabled", dispatch.pendingFutureCompletion.disabled === false && dispatch.protectedWindow.disabled === false && dispatch.paddedNumericUuid.disabled === false && dispatch.secondsTimestamps.disabled === false);
  claim("ghost uses the same predicate", JSON.stringify(Object.keys(dispatch).map((k) => dispatch[k].disabled)) === JSON.stringify(Object.keys(ghost).map((k) => ghost[k].disabled)));
  claim("truck: invalid/expired/full disabled; protected enabled", truck.ok.disabled === false && truck.emptyUuid.disabled && truck.blankUuid.disabled && truck.serverZero.disabled && truck.serverFraction.disabled && truck.expired.disabled && truck.arriveNow.disabled && truck.full.disabled && truck.specialURFullAfterOne.disabled && truck.protected.disabled === false && truck.noMaxLoot.disabled === false);
  return out;
});

await scenario("selection-disabled-stays-selected", "A selected row that becomes disabled (expiry) stays checked and is still scheduled", async (claim) => {
  const h = await boot(); await openTab(h, "dispatch", [dispatchRow("1001", { taskExpireTime: NOW + 5000 })]);
  await h.toggleRow(0);
  const before = { box: checkboxView(h)[0] };
  await h.advance(6000);
  const after = { box: checkboxView(h)[0], keys: h.getState("selectedDispatchKeys"), scheduleDisabled: h.findButton("map.scheduleSelected", { count: 1 }).props.disabled };
  h.findButton("map.scheduleSelected", { count: 1 }).props.onClick(); await h.settle();
  const call = lastCall(h, "dispatchPlunderSchedule");
  const out = { before, after, scheduledRowUuids: call?.args[0].map((r) => r.uuid) };
  claim("after expiry: checkbox disabled AND checked, selection retained, Schedule enabled", before.box.disabled === false && after.box.disabled === true && after.box.checked === true && after.keys.length === 1 && after.scheduleDisabled === false);
  claim("the expired row is still passed to the schedule call", JSON.stringify(out.scheduledRowUuids) === JSON.stringify(["1001"]));
  return out;
});

await scenario("selection-across-page", "Selections span pages; scheduling uses stored snapshots", async (claim) => {
  const h = await boot(); await openTab(h, "dispatch", [dispatchRow("1001")], 120);
  await h.toggleRow(0);
  await h.setPage(2); await drain(h, { rows: [dispatchRow("2001")], total: 120 });
  const onPage2 = { keys: h.getState("selectedDispatchKeys"), boxes: checkboxView(h) };
  await h.toggleRow(0);
  const survive = {};
  await h.clickSearch(); await drain(h, { rows: [dispatchRow("2001")], total: 120 }); survive.afterSearchRefetch = h.getState("selectedDispatchKeys").length;
  await h.setPage(1); await drain(h, { rows: [dispatchRow("1001")], total: 120 }); survive.afterPageChange = h.getState("selectedDispatchKeys").length;
  await h.emitScanState({ isReading: true }); await h.emitScanState({ isReading: false }); await drain(h, { rows: [dispatchRow("1001")], total: 120 }); survive.afterScanCompletion = h.getState("selectedDispatchKeys").length;
  await h.emitServer(456); survive.afterServerChange = h.getState("selectedDispatchKeys").length;
  h.findButton("map.scheduleSelected", { count: 2 }).props.onClick(); await h.settle();
  const call = lastCall(h, "dispatchPlunderSchedule");
  const out = { onPage2, dispatchSelectionSizeAfter: survive, scheduleArgs: { rowUuids: call.args[0].map((r) => r.uuid), delay: call.args[1] } };
  claim("dispatch selection survives Search refetch, page change, scan completion and server change", Object.values(survive).every((n) => n === 2));
  claim("page-1 selection kept on page 2", onPage2.keys.includes("321:1001") && onPage2.boxes[0].checked === false);
  claim("schedule receives both rows (one not on screen)", out.scheduleArgs.rowUuids.length === 2);
  return out;
});

// ---- random delay ---------------------------------------------------------------------------------------------------
const DELAYS = ["0", "", " ", "   ", "5", "-1", "-0", "1.5", "1e2", "0x10", "abc", "Infinity", "1e400", "9007199254740991", "9007199254740992", "9007199254740993", "007", "+3"];
const contractJson = JSON.parse(fs.readFileSync(path.join(originalDir, "contract.json"), "utf8"));
const exprOf = (id) => contractJson.items.find((i) => i.id === id).expression;
const evalDelay = new Function("mn", `let ${exprOf("delay-parse-Gn")},${exprOf("delay-valid-Kn")};return {Gn,Kn};`);
await scenario(["delay-parse-matrix", "schedule-button-disabled-predicate"], "Random delay strings: Schedule button predicate, value passed to the schedule call, and the extracted Gn/Kn expressions", async (claim) => {
  const matrix = {};
  for (const s of DELAYS) {
    const h = await boot(); await openTab(h, "dispatch", [dispatchRow("1001")]);
    await h.toggleRow(0);
    await h.setRandomDelay(s);
    const pure = evalDelay(s);
    const inputValueBefore = h.findInput("map.randomDelaySeconds").props.value;
    const b = h.findButton("map.scheduleSelected", { count: 1 });
    const disabled = b.props.disabled;
    let passed = "(not called)"; let passedIsNegativeZero = false;
    if (!disabled) { b.props.onClick(); await h.settle(); const c = lastCall(h, "dispatchPlunderSchedule"); passed = c.args[1]; passedIsNegativeZero = Object.is(passed, -0); }
    else { b.props.onClick(); await h.settle(); passed = h.callsNamed("dispatchPlunderSchedule").length ? "(called)" : "(not called)"; }
    matrix[JSON.stringify(s)] = { inputValueShown: inputValueBefore, Gn: !Number.isFinite(pure.Gn) ? String(pure.Gn) : Object.is(pure.Gn, -0) ? "-0" : pure.Gn, Kn: pure.Kn, buttonDisabled: disabled, valuePassedToSchedule: passed === undefined ? "undefined" : passedIsNegativeZero ? "-0" : (typeof passed === "number" && !Number.isFinite(passed) ? String(passed) : passed), pureExpressionAgreesWithButton: disabled === !pure.Kn };
  }
  const h = await boot(); await openTab(h, "dispatch", [dispatchRow("1001")]);
  const conditions = {};
  conditions.noSelection = buttonView(h.findButton("map.scheduleSelected", { count: 0 }));
  await h.toggleRow(0);
  conditions.selected = buttonView(h.findButton("map.scheduleSelected", { count: 1 }));
  await h.setProps({ online: false }); conditions.offline = buttonView(h.findButton("map.scheduleSelected", { count: 1 })); await h.setProps({ online: true });
  await h.emitScanState({ isReading: true }); conditions.scanReading = buttonView(h.findButton("map.scheduleSelected", { count: 1 })); await h.emitScanState({ isReading: false });
  h.setStubBehavior("dispatchPlunderSchedule", { mode: "manual" });
  h.findButton("map.scheduleSelected", { count: 1 }).props.onClick(); await h.settle();
  conditions.scheduleInFlight = { ...buttonView(h.findButton("map.scheduleSelected", { count: 1 })), busyKey: h.getState("busyKey"), shareDisabled: h.findButton("map.shareAlliance")?.props.disabled };
  const out = { matrix, conditions };
  claim("every pure Gn/Kn evaluation agrees with the button predicate", Object.values(matrix).every((m) => m.pureExpressionAgreesWithButton));
  claim("'' and whitespace are valid (Number -> 0)", matrix['""'].buttonDisabled === false && matrix['" "'].buttonDisabled === false && matrix['""'].valuePassedToSchedule === 0);
  claim("-1, 1.5, abc, Infinity, 2^53 and above are invalid", ["-1", "1.5", "abc", "Infinity", "1e400", "9007199254740992", "9007199254740993"].every((s) => matrix[JSON.stringify(s)].buttonDisabled === true));
  claim("1e2 and 0x10 are valid and the parsed number is passed", matrix['"1e2"'].valuePassedToSchedule === 100 && matrix['"0x10"'].valuePassedToSchedule === 16);
  claim("2^53-1 valid", matrix['"9007199254740991"'].buttonDisabled === false);
  claim("online and scan state do not affect Schedule", conditions.offline.disabled === false && conditions.scanReading.disabled === false);
  claim("in-flight schedule disables Schedule and Share", conditions.scheduleInFlight.disabled === true && conditions.scheduleInFlight.busyKey === "schedule" && conditions.scheduleInFlight.shareDisabled === true);
  return out;
});

await scenario("delay-shared-and-persistent", "One delay state for Dispatch and Ghost; survives tab/page changes and a successful schedule", async (claim) => {
  const h = await boot(); await openTab(h, "dispatch", [dispatchRow("1001")]);
  await h.setRandomDelay("7");
  const onDispatch = h.findInput("map.randomDelaySeconds").props.value;
  await h.clickTab("ghost"); await drain(h, { rows: [dispatchRow("2001")], total: 1 });
  const onGhost = h.findInput("map.randomDelaySeconds").props.value;
  await h.toggleRow(0); h.findButton("map.scheduleSelected", { count: 1 }).props.onClick(); await h.settle();
  const afterSchedule = h.getState("randomDelay");
  await h.clickTab("truck"); const truckHasInput = !!h.findInput("map.randomDelaySeconds");
  const attrs = (() => { const i = null; return i; })();
  const out = { onDispatch, onGhost, afterSchedule, truckTabHasDelayInput: truckHasInput, attrs };
  claim("same value shown on both tabs", onDispatch === "7" && onGhost === "7");
  claim("kept after scheduling and tab changes", afterSchedule === "7");
  claim("no delay input on the truck tab", truckHasInput === false);
  const h2 = await boot(); await openTab(h2, "dispatch", [dispatchRow("1001")]);
  const inp = h2.findInput("map.randomDelaySeconds");
  out.inputAttributes = { type: inp.props.type, min: inp.props.min, step: inp.props.step, value: inp.props.value, ariaLabel: inp.props["aria-label"] };
  claim("input is type=number min=0 step=1 value '0'", out.inputAttributes.type === "number" && out.inputAttributes.min === "0" && out.inputAttributes.step === "1" && out.inputAttributes.value === "0");
  return out;
});

await scenario(["schedule-dispatch-success-clears", "schedule-dispatch-failure-keeps-selection"], "Schedule (dispatch): busy state, call arguments, success path and failure path", async (claim) => {
  const run = async (mode) => {
    const h = await boot({ stubs: { dispatchPlunderSchedule: { mode: "manual" } } });
    await openTab(h, "dispatch", [dispatchRow("1001"), dispatchRow("1002")]);
    await h.toggleRow(0); await h.toggleRow(1); await h.setRandomDelay("3");
    const jobCallsBefore = h.callsNamed("plunderJobsList").length;
    h.findButton("map.scheduleSelected", { count: 2 }).props.onClick(); await h.settle();
    const call = lastCall(h, "dispatchPlunderSchedule");
    const during = { busyKey: h.getState("busyKey"), scheduleButton: buttonView(h.findButton("map.scheduleSelected", { count: 2 })), shareButton: buttonView(h.findButton("map.shareAlliance")), args: { rowUuids: call.args[0].map((r) => r.uuid), taskKinds: call.args[0].map((r) => r.taskKind), delay: call.args[1] } };
    if (mode === "success") await h.resolveCall(call, undefined); else await h.rejectCall(call, new Error("boom"));
    return { during, after: { busyKey: h.getState("busyKey"), tab: h.getState("tab"), selectedKeys: h.getState("selectedDispatchKeys"), message: h.getState("message"), delay: h.getState("randomDelay"), jobListCalls: h.callsNamed("plunderJobsList").length - jobCallsBefore, logs: h.logs.slice(-2) } };
  };
  const success = await run("success"); const failure = await run("reject");
  const out = { success, failure };
  claim("during: busyKey schedule, Schedule+Share disabled, call args rows + numeric delay 3", success.during.busyKey === "schedule" && success.during.scheduleButton.disabled === true && success.during.shareButton.disabled === true && success.during.args.delay === 3 && success.during.args.rowUuids.length === 2);
  claim("success: selection cleared, switched to Scheduled, job list loaded, busy cleared", success.after.selectedKeys.length === 0 && success.after.tab === "scheduledPlunder" && success.after.jobListCalls >= 2 && success.after.busyKey === "");
  claim("success log line", success.after.logs.some((l) => l === "scheduled 2 secret task plunder jobs"));
  claim("failure: selection kept, stays on tab, busy cleared, only a log line", failure.after.selectedKeys.length === 2 && failure.after.tab === "dispatch" && failure.after.busyKey === "" && failure.after.message === "" && failure.after.logs.some((l) => l.startsWith("dispatch plunder schedule error")));
  return out;
});

await scenario(["schedule-truck-success-clears", "schedule-truck-failure-keeps-selection", "truck-schedule-button-predicate"], "Schedule trucks: predicate, busy key, success/failure", async (claim) => {
  const h = await boot({ stubs: { truckPlunderSchedule: { mode: "manual" } } }); await openTab(h, "truck", [truckRow("t1"), truckRow("t2")]);
  const none = buttonView(h.findButton("map.scheduleSelectedTrucks", { count: 0 }));
  await h.toggleRow(0); await h.toggleRow(1);
  const two = buttonView(h.findButton("map.scheduleSelectedTrucks", { count: 2 }));
  await h.setProps({ online: false }); const offline = buttonView(h.findButton("map.scheduleSelectedTrucks", { count: 2 })); await h.setProps({ online: true });
  await h.emitScanState({ isReading: true }); const reading = buttonView(h.findButton("map.scheduleSelectedTrucks", { count: 2 })); await h.emitScanState({ isReading: false });
  h.findButton("map.scheduleSelectedTrucks", { count: 2 }).props.onClick(); await h.settle();
  const call = lastCall(h, "truckPlunderSchedule");
  const during = { busyKey: h.getState("busyKey"), button: buttonView(h.findButton("map.scheduleSelectedTrucks", { count: 2 })), rowUuids: call.args[0].map((r) => r.uuid) };
  await h.rejectCall(call, new Error("nope"));
  const failure = { selectedKeys: h.getState("selectedTruckKeys"), tab: h.getState("tab"), busyKey: h.getState("busyKey"), logs: h.logs.slice(-1) };
  h.findButton("map.scheduleSelectedTrucks", { count: 2 }).props.onClick(); await h.settle();
  const call2 = lastCall(h, "truckPlunderSchedule");
  const j = h.callsNamed("plunderJobsList").length;
  await h.resolveCall(call2, undefined);
  const success = { selectedKeys: h.getState("selectedTruckKeys"), tab: h.getState("tab"), busyKey: h.getState("busyKey"), logs: h.logs.slice(-1), jobListCallsDelta: h.callsNamed("plunderJobsList").length - j };
  const out = { none, two, offline, reading, during, failure, success };
  claim("disabled only without selection or while schedule-truck is busy", none.disabled === true && two.disabled === false && offline.disabled === false && reading.disabled === false && during.button.disabled === true && during.busyKey === "schedule-truck");
  claim("failure keeps selection and tab", failure.selectedKeys.length === 2 && failure.tab === "truck" && failure.busyKey === "");
  claim("success clears selection and switches to Scheduled", success.selectedKeys.length === 0 && success.tab === "scheduledPlunder" && success.busyKey === "" && success.logs[0] === "scheduled 2 truck plunder jobs");
  return out;
});

await scenario(["share-button-disabled-predicate"], "Share to alliance predicate matrix", async (claim) => {
  const h = await boot({ stubs: { dispatchShareAlliance: { mode: "manual" } } }); await openTab(h, "dispatch", [dispatchRow("1001")]);
  const m = {};
  const share = () => buttonView(h.findButton("map.shareAlliance") || h.findButton("map.sharingAlliance"));
  m.noSelection = share();
  await h.toggleRow(0);
  m.selectedOnline = share();
  await h.setProps({ online: false }); m.offline = share(); await h.setProps({ online: true });
  await h.emitScanState({ isReading: true }); m.scanReading = share(); await h.emitScanState({ isReading: false });
  await h.setRandomDelay("-1"); m.invalidDelay = { share: share(), schedule: buttonView(h.findButton("map.scheduleSelected", { count: 1 })) }; await h.setRandomDelay("0");
  h.findButton("map.shareAlliance").props.onClick(); await h.settle();
  m.sharing = { share: share(), schedule: buttonView(h.findButton("map.scheduleSelected", { count: 1 })), sharingFlag: h.getState("sharing") };
  await h.clickTab("ghost"); await drain(h, { rows: [dispatchRow("2001")], total: 1 });
  m.ghostTabHasShare = !!(h.findButton("map.shareAlliance") || h.findButton("map.sharingAlliance"));
  claim("disabled when no selection, offline, scan reading, sharing; enabled otherwise", m.noSelection.disabled === true && m.selectedOnline.disabled === false && m.offline.disabled === true && m.scanReading.disabled === true && m.sharing.share.disabled === true);
  claim("independent of the delay while Schedule is disabled by it", m.invalidDelay.share.disabled === false && m.invalidDelay.schedule.disabled === true);
  claim("sharing label and Schedule disabled during sharing; no share on ghost", m.sharing.share.text === "map.sharingAlliance" && m.sharing.schedule.disabled === true && m.ghostTabHasShare === false);
  return m;
});

await scenario(["share-success-prunes-and-message", "share-failure-keeps-selection"], "Share: reply prunes exactly sharedUuids (string compare, untrimmed), messages, failure keeps selection", async (claim) => {
  const rows = [dispatchRow("1001"), dispatchRow("1002"), dispatchRow(" 1003 "), dispatchRow("1004")];
  const run = async (reply, rejectWith) => {
    const h = await boot({ stubs: { dispatchShareAlliance: { mode: "manual" } }, translate: "en" }); await openTab(h, "dispatch", rows);
    for (let i = 0; i < 4; i += 1) await h.toggleRow(i);
    h.findButton("map.shareAlliance").props.onClick; // eslint-disable-line no-unused-expressions
    const btn = h.findNodes((n) => n.type === "button" && nodeText(n) === h.translate("map.shareAlliance"))[0];
    btn.props.onClick(); await h.settle();
    const call = lastCall(h, "dispatchShareAlliance");
    const rowArgs = call.args[0];
    if (rejectWith) await h.rejectCall(call, rejectWith); else await h.resolveCall(call, reply);
    return { sentRows: rowArgs.map((r) => ({ uuid: r.uuid, taskKind: r.taskKind })), keys: h.getState("selectedDispatchKeys"), message: h.getState("message"), sharing: h.getState("sharing"), busyKey: h.getState("busyKey") };
  };
  const partial = await run({ shared: 2, failed: 1, sharedUuids: ["1001", "1003"] });
  const full = await run({ shared: 1, failed: 0, sharedUuids: ["1001"] });
  const failed = await run(null, new Error("DISPATCH_PLUNDER_GAME_DISCONNECTED"));
  const out = { partial, full, failed };
  claim("the share call receives the stored row snapshots (with taskKind) in selection order; the 7-field IPC mapping is inside the stubbed imported function", JSON.stringify(partial.sentRows.map((r) => [r.uuid, r.taskKind])) === JSON.stringify([["1001", "dispatch"], ["1002", "dispatch"], [" 1003 ", "dispatch"], ["1004", "dispatch"]]));
  claim("partial: 1001 pruned, padded ' 1003 ' NOT pruned (untrimmed compare), partial message", !partial.keys.includes("321:1001") && partial.keys.includes("321: 1003 ") && partial.keys.length === 3 && /2/.test(partial.message) && /1/.test(partial.message));
  claim("full success message and other rows kept", full.keys.length === 3 && full.sharing === false);
  claim("failure keeps all 4 and sets a translated error message", failed.keys.length === 4 && failed.message !== "" && failed.sharing === false);
  return out;
});

// ---- treasure ------------------------------------------------------------------------------------------------------
const viewerStub = { mode: "auto", value: () => ({ playerUid: "p1", allianceId: "a1", states: [], batch: null }) };
const treasureRow = (uuid, extra = {}) => ({ uuid, serverId: SERVER, x: 5, y: 6, suppliesType: 1, complete: true, playerClaimState: "unclaimed", worldClaimState: "charging", remainingBoxes: 3, updatedAt: NOW - 1, ...extra });
await scenario(["treasure-claim-button-predicate", "treasure-row-claim-predicate", "treasure-claim-flow"], "Treasure claim buttons and flow", async (claim) => {
  let claimStarted = false; let polls = 0;
  const status = () => { if (!claimStarted) return { playerUid: "p1", allianceId: "a1", states: [], batch: null }; polls += 1; return { playerUid: "p1", allianceId: "a1", states: [], batch: { state: polls < 2 ? "running" : "done", queued: 2 } }; };
  const h = await boot({ stubs: { treasureClaimStatus: { mode: "auto", value: status }, treasureStateRefreshAll: { mode: "auto", value: () => ({ playerUid: "p1", allianceId: "a1", states: [] }) }, treasureClaim: { mode: "auto", value: () => { claimStarted = true; return { eligible: 2, queued: 2, skipped: 0 }; } } } });
  await openTab(h, "treasure", [treasureRow("900")]);
  const boxes = () => buttonView(h.findButton("map.claimTreasureBoxes") || h.findButton("map.claimingTreasures"));
  const m = {};
  m.idle = boxes();
  await h.setProps({ online: false }); m.offline = boxes(); await h.setProps({ online: true });
  await h.emitScanState({ isReading: true }); m.reading = boxes(); await h.emitScanState({ isReading: false });
  await h.emitServer(0); m.serverZero = boxes(); await h.emitServer(SERVER); await drain(h, { rows: [treasureRow("900")], total: 1 });
  // row buttons
  const rowVariants = [["ok", treasureRow("900")], ["uuidZero", treasureRow("0")], ["emptyUuid", treasureRow("")], ["otherSupplyIncomplete", treasureRow("901", { suppliesType: 2, complete: false })], ["otherSupplyComplete", treasureRow("902", { suppliesType: 2, complete: true })], ["claimed", treasureRow("903", { playerClaimState: "claimed" })], ["claiming", treasureRow("904", { playerClaimState: "claiming" })], ["depleted", treasureRow("905", { worldClaimState: "depleted" })], ["expired", treasureRow("906", { worldClaimState: "expired" })], ["otherAlliance", treasureRow("907", { claimBlockReason: "other_alliance" })]];
  await drain(h);
  await h.setPage(2);
  await drain(h, { rows: rowVariants.map(([, r]) => r), total: 60 });
  const tableOut = h.expandedTable();
  const rowButtons = treeNodes(tableOut).filter((n) => n.type === "button" && n.props.className === "map-schedule-button");
  m.rowButtons = Object.fromEntries(rowButtons.map((b, i) => [rowVariants[i][0], { disabled: b.props.disabled, text: nodeText(b) }]));
  // flow
  await h.setPage(1); await drain(h, { rows: [treasureRow("900")], total: 1 });
  const n = h.requests.length; const statusCalls = h.callsNamed("treasureClaimStatus").length;
  h.findButton("map.claimTreasureBoxes").props.onClick(); await h.settle();
  const claimCall = lastCall(h, "treasureClaim");
  m.flow = { claimArgs: claimCall.args, afterClick: { message: h.getState("treasureClaimMessage"), busy: h.getState("treasureClaimBusy"), buttonText: nodeText(h.findButton("map.claimingTreasures")), buttonDisabled: h.findButton("map.claimingTreasures").props.disabled }, pollCallsRightAfterClick: h.callsNamed("treasureClaimStatus").length - statusCalls };
  await h.advance(1000); m.flow.after1s = { pollCalls: h.callsNamed("treasureClaimStatus").length - statusCalls, message: h.getState("treasureClaimMessage"), busy: h.getState("treasureClaimBusy") };
  await h.advance(1000); m.flow.after2s = { pollCalls: h.callsNamed("treasureClaimStatus").length - statusCalls, message: h.getState("treasureClaimMessage"), busy: h.getState("treasureClaimBusy"), rowsRevision: h.getState("rowsRevision"), newSearchRequests: h.requests.length - n };
  claim("claim buttons: enabled idle; disabled offline, scanning and with no data server", m.idle.disabled === false && m.offline.disabled === true && m.reading.disabled === true && m.serverZero.disabled === true);
  claim("row button matrix: ok enabled; uuid 0/empty, incomplete other supply, claimed/claiming, depleted/expired, other alliance disabled", m.rowButtons.ok.disabled === false && m.rowButtons.uuidZero.disabled && m.rowButtons.emptyUuid.disabled && m.rowButtons.otherSupplyIncomplete.disabled && m.rowButtons.otherSupplyComplete.disabled === false && m.rowButtons.claimed.disabled && m.rowButtons.claiming.disabled && m.rowButtons.depleted.disabled && m.rowButtons.expired.disabled && m.rowButtons.otherAlliance.disabled);
  claim("single claim passes (serverId, scope, luckyFirst, '')", m.flow.claimArgs[0] === SERVER && m.flow.claimArgs[1] === "boxes" && m.flow.claimArgs[2] === true && m.flow.claimArgs[3] === "");
  claim("busy label/disabled during claim and polling once per second until not running", m.flow.afterClick.buttonDisabled === true && m.flow.after1s.pollCalls === 1 && m.flow.after2s.pollCalls === 2 && m.flow.after2s.busy === false);
  return m;
});

// ---- Scheduled Plunder ------------------------------------------------------------------------------------------------
const sjob = (uuid, extra = {}) => ({ serverId: SERVER, uuid, taskKind: "dispatch", scheduleStatus: "scheduled", ownerName: `O${uuid}`, quality: 3, completionTime: NOW - 1, plunderAt: NOW + 5000, taskExpireTime: NOW + 7200000, rewards: [], ...extra });
const tjob = (uuid, extra = {}) => ({ serverId: SERVER, uuid, jobId: `j-${uuid}`, scheduledAt: NOW, scheduleStatus: "succeeded", ownerName: `T${uuid}`, quality: 3, robTimes: 1, maxLootCount: 3, arriveTs: NOW + 60000, protectTime: 0, plunderRewards: [], ...extra });

await scenario("scheduled-loads-on-mount-and-events", "Job list loading at mount and on events; listener lifecycle", async (claim) => {
  const h = await boot();
  const listeners = h.listeners.map((l) => ({ name: l.name, active: l.active }));
  const atMount = h.callsNamed("plunderJobsList").length;
  const n = h.requests.length;
  await h.emitEvent("bridge://dispatch-plunder-changed"); const afterDispatchEvent = h.callsNamed("plunderJobsList").length;
  await h.emitEvent("bridge://truck-plunder-changed"); const afterTruckEvent = h.callsNamed("plunderJobsList").length;
  await h.emitEvent("bridge://player-mark-changed"); const afterMarkEvent = h.callsNamed("plunderJobsList").length;
  const markRefetch = since(h, n);
  await h.emitEvent("bridge://unknown"); const afterUnknown = h.callsNamed("plunderJobsList").length;
  await h.unmount();
  const out = { listeners, jobListCallsAtMount: atMount, afterDispatchEvent, afterTruckEvent, afterPlayerMarkEvent: afterMarkEvent, searchRequestsAfterPlayerMarkEvent: markRefetch, afterUnknownEvent: afterUnknown, activeListenersAfterUnmount: h.listeners.filter((l) => l.active).length };
  claim("three subscriptions at mount, one job list load", listeners.length === 3 && atMount === 1);
  claim("dispatch and truck events each reload jobs", afterDispatchEvent === 2 && afterTruckEvent === 3);
  claim("player-mark event does not reload jobs but refetches the search", afterMarkEvent === 3 && markRefetch.length === 1);
  claim("unknown event: nothing; unmount removes all listeners", afterUnknown === 3 && out.activeListenersAfterUnmount === 0);
  return out;
});

await scenario(["scheduled-loads-on-entry", "scheduled-stale-job-reply-applies-in-arrival-order"], "Scheduled tab entry loads jobs; job replies are applied in arrival order", async (claim) => {
  const h = await boot({ jobs: { dispatchJobs: [sjob("1001")], truckJobs: [tjob("t1")] } }); await drain(h);
  const countAtCity = nodeText(h.findNodes((n) => n.type === "span" && n.props.className === "map-tab-count").at(-1));
  const n = h.requests.length; const j = h.callsNamed("plunderJobsList").length;
  await h.clickTab("scheduledPlunder");
  const entry = { jobListCallsDelta: h.callsNamed("plunderJobsList").length - j, searchRequestsDelta: h.requests.length - n, tabCountBefore: countAtCity, tabCountAfter: nodeText(h.findNodes((n2) => n2.type === "span" && n2.props.className === "map-tab-count").at(-1)) };
  const j2 = h.callsNamed("plunderJobsList").length;
  await h.clickTab("truck"); await drain(h);
  entry.otherTabEntryJobListCalls = h.callsNamed("plunderJobsList").length - j2;
  // arrival order
  const h2 = await boot({ stubs: { plunderJobsList: { mode: "manual" } } });
  const c1 = lastCall(h2, "plunderJobsList");
  await h2.clickTab("scheduledPlunder");
  const c2 = lastCall(h2, "plunderJobsList");
  await h2.resolveCall(c2, { dispatchJobs: [sjob("NEW")], truckJobs: [] });
  const afterNewest = h2.getState("scheduledDispatchJobs").map((x) => x.uuid);
  await h2.resolveCall(c1, { dispatchJobs: [sjob("OLD")], truckJobs: [] });
  const afterOlderArrives = h2.getState("scheduledDispatchJobs").map((x) => x.uuid);
  const out = { entry, arrival: { callIds: [c1.id, c2.id], afterNewestReply: afterNewest, afterOlderReplyArrivesLast: afterOlderArrives } };
  claim("entering Scheduled reloads once and issues no search; entering another tab does not reload jobs", entry.jobListCallsDelta === 1 && entry.searchRequestsDelta === 0 && entry.otherTabEntryJobListCalls === 0);
  claim("count badge is dispatch+truck job total", /2$/.test(entry.tabCountAfter));
  claim("an older reply that arrives last overwrites the newer one (no generation guard)", afterNewest[0] === "NEW" && afterOlderArrives[0] === "OLD");
  return out;
});

await scenario(["scheduled-count-and-render-order"], "Scheduled tab: tab count, three groups in order, rows in backend order", async (claim) => {
  const jobs = { dispatchJobs: [sjob("2", { taskKind: "dispatch" }), sjob("1", { taskKind: "dispatch" }), sjob("g1", { taskKind: "ghost" })], truckJobs: [tjob("tb"), tjob("ta")] };
  const h = await boot({ jobs }); await drain(h);
  const counts = (h) => h.findNodes((n) => n.type === "span" && n.props.className === "map-tab-count").map((n) => nodeText(n));
  const tabCounts = counts(h);
  await h.clickTab("scheduledPlunder");
  const groups = h.scheduledGroups().map((g) => ({ component: g.component, kind: g.kind, jobUuids: g.props.jobs.map((j) => j.uuid) }));
  const resultCount = h.findNodes((n) => n.type === "span" && n.props.className === "map-result-count").map((n) => nodeText(n));
  const expanded = h.scheduledGroups().map((g) => { const t = h.expand(g.element); return treeNodes(t).filter((n) => n.type === "strong" && String(n.props.className || "").includes("map-scheduled-kind")).map((n) => nodeText(n)); });
  const out = { tabCountsAcrossTabs: tabCounts, groupsInRenderOrder: groups, groupTitles: expanded, resultCountOnScheduledTab: resultCount, searchbarInputPresent: !!h.findInput("map.searchLabel") };
  claim("scheduled badge is the job total (5)", tabCounts.at(-1) === "5");
  claim("groups: secret(dispatch, non-ghost), ghost, truck in that order with backend row order", groups.length === 3 && groups[0].component === "ot" && groups[0].kind === "dispatch" && JSON.stringify(groups[0].jobUuids) === JSON.stringify(["2", "1"]) && groups[1].kind === "ghost" && JSON.stringify(groups[1].jobUuids) === JSON.stringify(["g1"]) && groups[2].component === "st" && JSON.stringify(groups[2].jobUuids) === JSON.stringify(["tb", "ta"]));
  claim("no keyword input on the Scheduled tab; result count shows the total", out.searchbarInputPresent === false && resultCount[0] === "5 items");
  return out;
});

await scenario("scheduled-ticker-conditions", "currentTime ticker runs only for reading scans and dispatch/ghost/truck/scheduledPlunder tabs", async (claim) => {
  const h = await boot(); await drain(h);
  const perTab = {};
  for (const kind of ["city", "resource", "monster", "truck", "railway", "dispatch", "ghost", "treasure", "scheduledPlunder"]) {
    if (kind !== "city") await h.clickTab(kind);
    await drain(h);
    const t0 = h.getState("currentTime");
    await h.advance(2000);
    perTab[kind] = { intervals: h.timers().intervals, currentTimeAdvanced: h.getState("currentTime") - t0 };
  }
  await h.clickTab("city"); await drain(h);
  await h.emitScanState({ isReading: true });
  const cityReading = { intervals: h.timers().intervals, scanProgressTimeouts: h.timers().timeouts };
  await h.emitScanState({ isReading: false });
  const cityAfter = { intervals: h.timers().intervals };
  const out = { perTab, cityWhileReading: cityReading, cityAfterReading: cityAfter };
  claim("interval (1000 ms) only on truck, dispatch, ghost, scheduledPlunder", ["truck", "dispatch", "ghost", "scheduledPlunder"].every((k) => perTab[k].intervals.length === 1 && perTab[k].intervals[0] === 1000) && ["city", "resource", "monster", "railway", "treasure"].every((k) => perTab[k].intervals.length === 0));
  claim("currentTime advances only where the interval runs", perTab.dispatch.currentTimeAdvanced === 2000 && perTab.city.currentTimeAdvanced === 0);
  claim("a reading scan starts the interval on any tab and stops with it", cityReading.intervals.length === 1 && cityAfter.intervals.length === 0);
  return out;
});

await scenario("scheduled-clear-and-cancel", "Scheduled groups: clear/cancel/plunder-again predicates and calls", async (claim) => {
  const jobs = {
    dispatchJobs: [sjob("1001", { scheduleStatus: "scheduled" }), sjob("1002", { scheduleStatus: "succeeded" }), sjob("g1", { taskKind: "ghost", scheduleStatus: "waiting_connection" }), sjob("g2", { taskKind: "ghost", scheduleStatus: "failed", lastError: "DISPATCH_PLUNDER_GAME_DISCONNECTED" })],
    truckJobs: [tjob("t1", { scheduleStatus: "succeeded" }), tjob("t2", { scheduleStatus: "scheduled" }), tjob("t3", { scheduleStatus: "succeeded" }), tjob("t3", { jobId: "j-t3b", scheduleStatus: "scheduled" })],
  };
  const h = await boot({ jobs, stubs: { dispatchPlunderClear: { mode: "manual" }, truckPlunderCancel: { mode: "manual" } } }); await drain(h);
  await h.clickTab("scheduledPlunder");
  const view = () => h.scheduledGroups().map((g) => {
    const tree = h.expand(g.element);
    const buttons = treeNodes(tree).filter((n) => n.type === "button");
    return { component: g.component, kind: g.kind, buttons: buttons.map((b) => ({ text: nodeText(b), disabled: b.props.disabled })) };
  });
  const initial = view();
  // clear secret
  const secretGroup = h.scheduledGroups()[0]; const secretTree = h.expand(secretGroup.element);
  treeNodes(secretTree).find((n) => n.type === "button" && nodeText(n) === "map.clearPlunderHistory").props.onClick(); await h.settle();
  const clearCall = lastCall(h, "dispatchPlunderClear");
  const duringClear = { busyKey: h.getState("busyKey"), args: [typeof clearCall.args[0], clearCall.args[1]], view: view() };
  await h.resolveCall(clearCall, undefined);
  const afterClear = { busyKey: h.getState("busyKey"), jobListReloaded: h.callsNamed("plunderJobsList").length };
  // cancel truck t2
  const truckGroup = h.scheduledGroups()[2];
  const truckTree = h.expand(truckGroup.element);
  const cancelBtn = treeNodes(truckTree).filter((n) => n.type === "button" && nodeText(n) === "common.cancel")[0];
  cancelBtn.props.onClick(); await h.settle();
  const tCancel = lastCall(h, "truckPlunderCancel");
  const duringTruckCancel = { busyKey: h.getState("busyKey"), args: tCancel.args };
  await h.resolveCall(tCancel, undefined);
  // plunder again t1 (succeeded, no active duplicate) vs t3 (active duplicate)
  await h.setProps({ online: false }); const offlineView = view()[2]; await h.setProps({ online: true });
  const againBtn = treeNodes(h.expand(h.scheduledGroups()[2].element)).filter((n) => n.type === "button" && nodeText(n) === "map.plunderAgain");
  againBtn[0].props.onClick(); await h.settle();
  const again = lastCall(h, "truckPlunderSchedule");
  const out = { initial, duringClear, afterClear, duringTruckCancel, truckViewOffline: offlineView, plunderAgainButtonsOnline: againBtn.length, plunderAgainCall: { rowUuids: again.args[0].map((r) => r.uuid), count: again.args[0].length }, busyAfterAgain: h.getState("busyKey") };
  claim("secret group: Cancel only for scheduled (not for succeeded); clear enabled because a finished job exists", initial[0].buttons.filter((b) => b.text === "common.cancel").length === 1 && initial[0].buttons[0].text === "map.clearPlunderHistory" && initial[0].buttons[0].disabled === false);
  claim("ghost group Cancel for waiting_connection; truck group has Cancel x2 and Plunder again for the only inactive succeeded truck", initial[1].buttons.filter((b) => b.text === "common.cancel").length === 1 && initial[2].buttons.filter((b) => b.text === "common.cancel").length === 2 && initial[2].buttons.filter((b) => b.text === "map.plunderAgain").length === 1);
  claim("clear call: (timestamp, 'dispatch'), busyKey clear:dispatch disables every clear button", duringClear.args[0] === "number" && duringClear.args[1] === "dispatch" && duringClear.busyKey === "clear:dispatch" && duringClear.view.every((g) => g.buttons[0].disabled === true));
  claim("truck cancel call (serverId, uuid) with busyKey truck:serverId:uuid", JSON.stringify(duringTruckCancel.args) === JSON.stringify([SERVER, "t2"]) && duringTruckCancel.busyKey === `truck:${SERVER}:t2`);
  claim("Plunder again hidden while offline; calls schedule with exactly the job row", offlineView.buttons.filter((b) => b.text === "map.plunderAgain").length === 0 && out.plunderAgainCall.count === 1 && out.plunderAgainCall.rowUuids[0] === "t1");
  return out;
});

await scenario(["app-controlled-vs-uncontrolled-tab", "activity-hide-show-modelled"], "Uncontrolled vs controlled tab and the Activity hide/show model", async (claim) => {
  const u = await boot(); await drain(u);
  await u.clickTab("monster");
  const uncontrolled = { tab: u.getState("tab"), internalState: u.getState("uncontrolledTab"), propCalls: u.propCalls.filter((c) => c.name === "onActiveTabChange").length };
  const c = await boot({ tabMode: "controlled" }); await drain(c);
  await c.clickTab("monster");
  const controlled = { tab: c.getState("tab"), internalState: c.getState("uncontrolledTab"), propCalls: c.propCalls.filter((x) => x.name === "onActiveTabChange").map((x) => x.args[0]), requests: since(c, 0).map((r) => r.kind) };
  // hide / show
  const h = await boot(); await drain(h);
  const before = { requests: h.requests.length, jobCalls: h.callsNamed("plunderJobsList").length, optionCalls: h.callsNamed("dataOptions").length, activeListeners: h.listeners.filter((l) => l.active).length };
  await h.hide();
  const hidden = { activeListeners: h.listeners.filter((l) => l.active).length, intervals: h.timers().intervals.length };
  await h.show();
  const shown = { requestsDelta: h.requests.length - before.requests, jobCallsDelta: h.callsNamed("plunderJobsList").length - before.jobCalls, optionCallsDelta: h.callsNamed("dataOptions").length - before.optionCalls, activeListeners: h.listeners.filter((l) => l.active).length };
  const out = { uncontrolled, controlled, activity: { before, hidden, shown, status: "IMPLEMENTED_NOT_VALIDATED (models React Activity: effects disconnected while hidden, all re-created on reveal)" } };
  claim("uncontrolled: internal state drives the tab; the prop callback is never called", uncontrolled.tab === "monster" && uncontrolled.internalState === "monster" && uncontrolled.propCalls === 0);
  claim("controlled: the parent value drives the tab, internal state untouched, callback called", controlled.tab === "monster" && controlled.internalState === "city" && controlled.propCalls.length === 1 && controlled.propCalls[0] === "monster");
  claim("hide: listeners released; show: all effects rerun (search, jobs, options requested again)", hidden.activeListeners === 0 && shown.activeListeners === 3 && shown.requestsDelta >= 1 && shown.jobCallsDelta === 1 && shown.optionCallsDelta === 1);
  return out;
});

// ---- contract cross-check ------------------------------------------------------------------------------------------------
const referenced = new Set(contractJson.items.flatMap((i) => i.confirmedBy || []));
const missing = [...referenced].filter((id) => !(id in results.scenarios));
results.contractCrossCheck = { referencedConfirmationIds: [...referenced].sort(), missingScenarioIds: missing };
if (missing.length) { console.log(`MISSING scenario ids referenced by contract.json: ${missing.join(", ")}`); failedClaims += missing.length; }
results.summary = { scenarios: Object.keys(results.scenarios).length, claims: totalClaims, failedClaims };
console.log(`\n${results.summary.scenarios} scenario ids, ${totalClaims} claims, ${failedClaims} failed`);
if (record) { fs.writeFileSync(path.join(originalDir, "results.json"), `${JSON.stringify(results, null, 2)}\n`); console.log("results.json written"); }
process.exit(failedClaims ? 1 : 0);
