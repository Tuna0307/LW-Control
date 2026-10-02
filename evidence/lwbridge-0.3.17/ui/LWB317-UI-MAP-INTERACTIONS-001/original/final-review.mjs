// Second-round review: candidate deviations of the COMMITTED canonical page versus the ACTUAL original, each EXECUTED
// on both implementations through ../flavors.mjs (bootOriginal / bootCanonicalLike) and the shared ../driver.mjs.
//
//   node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/original/final-review.mjs [--record]
//
// Writes original/final-review-results.json with --record. Classification (DEFECT / DOCUMENTED / UNKNOWN) is the
// reviewer's judgement stored next to the observed data; `differs` is computed.
import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { createHarness, treeNodes, nodeText } from "../harness.mjs";
import { bootCanonicalLike, bootOriginal, SOURCES } from "../flavors.mjs";
import { NOW, T, dispatchRow, truckRow, RESOURCE_OPTIONS } from "../scenarios.mjs";
import { originalDir } from "./lib.mjs";

const record = process.argv.includes("--record");
const sha = (file) => crypto.createHash("sha256").update(fs.readFileSync(file, "utf8").replace(/\r\n/g, "\n")).digest("hex");
const results = { canonicalSource: "src/LWBridge.UI-0.3.17/src/MapDataPage.jsx", canonicalSha256: sha(SOURCES.current), candidates: [] };

const boot = {
  original: (o) => bootOriginal(o),
  canonical: (o) => bootCanonicalLike(SOURCES.current, "committed-canonical", o),
};
const same = (a, b) => JSON.stringify(a) === JSON.stringify(b);
const stable = (v) => (Array.isArray(v) ? v.map(stable) : v && typeof v === "object" ? Object.fromEntries(Object.keys(v).sort().map((k) => [k, stable(v[k])])) : v);

async function candidate(id, classification, description, options, run) {
  const observed = {};
  for (const kind of ["original", "canonical"]) {
    try {
      const impl = await boot[kind](typeof options === "function" ? options(kind) : options);
      impl.kind = kind;
      await impl.h.mount();
      observed[kind] = stable(await run(impl));
    } catch (error) { observed[kind] = { error: String(error.message || error).split("\n")[0] }; }
  }
  const differs = !same(observed.original, observed.canonical);
  results.candidates.push({ id, classification, description, differs, original: observed.original, canonical: observed.canonical });
  console.log(`${differs ? "DIFFERS" : "same   "} [${classification}] ${id}`);
}

// ---- helpers working on both implementations ------------------------------------------------------------------------
const q = (r) => ({ kind: r.kind, serverId: r.query.serverId, page: r.query.page, keyword: r.query.keyword });
const reqs = (h, from = 0) => h.requests.slice(from).map(q);
async function resolveAll(h, reply = { rows: [], total: 0 }) {
  for (let i = 0; i < 10; i += 1) {
    const open = h.requests.filter((r) => !r.settled);
    if (!open.length) return;
    for (const r of open) { r.settled = true; r.resolve(reply); }
    await h.settle();
  }
}
const selectNode = (h, label) => h.findNodes((n) => n.type === "select" && n.props?.["aria-label"] === label)[0];
async function setSelect(h, label, value) { const n = selectNode(h, label); assert.ok(n, `select ${label}`); n.props.onChange({ target: { value } }); await h.settle(); }
async function setCheckbox(h, labelText, checked) {
  const label = h.findNodes((n) => n.type === "label" && nodeText(n) === labelText)[0];
  const input = treeNodes(label).find((n) => n.type === "input");
  input.props.onChange({ target: { checked } }); await h.settle();
}
const buttonByText = (h, text) => h.findNodes((n) => n.type === "button" && nodeText(n) === text)[0];
const quiet = async (impl) => { await resolveAll(impl.h); };
const jobCalls = (h) => h.calls.filter((c) => c.name === "listPlunderJobs" || c.name === "plunderJobsList").length;
const textsOf = (d) => d.tabTexts();

// Canonical MapTable lives in MapDataPage.jsx and uses hooks; expose it via a harness whose source gets one extra
// statement (in memory only), then call it once on a fresh runtime (hook cursors start at zero).
async function canonicalTable(props, now) {
  const source = `${fs.readFileSync(SOURCES.current, "utf8")}\nglobalThis.__MapTableUnderTest = MapTable;\n`;
  const h = await createHarness(source, "table", { translate: T.translate, now });
  const out = globalThis.__MapTableUnderTest(props);
  return out;
}
function tableButtons(tree) {
  return treeNodes(tree).filter((n) => n.type === "button").map((n) => ({ cls: String(n.props.className || ""), text: nodeText(n), disabled: Boolean(n.props.disabled) }));
}
async function tableView(impl) {
  const { h, d } = impl;
  const node = impl.kind === "original" ? h.tableNode() : h.findNodes((n) => typeof n.type === "function" && n.type.name === "MapTable")[0];
  assert.ok(node, "table element");
  const tree = impl.kind === "original" ? h.expand(node) : await canonicalTable(node.props, h.now());
  void d;
  return tree;
}

// ===========================================================================================================
const NAMES = { names: { resource: [{ key: "r1", count: 3 }, { key: "r2", count: 5 }], monster: [] } };

await candidate("C01-server-change-at-page-3", "DEFECT?", "Server change while on page 3: every search request that follows",
  { tab: "city", online: true, dataOptions: NAMES },
  async ({ h, d }) => { await resolveAll(h, { rows: [], total: 230 }); await d.setPage(3); await resolveAll(h, { rows: [], total: 230 }); const n = h.requests.length; await h.emitServer(456); return { requests: reqs(h, n), page: d.page() }; });

await candidate("C02-options-refresh-validates-alliance-level-treasure", "DEFECT?", "Options refresh no longer offers the selected alliance / dispatch level: filter state afterwards",
  { tab: "city", online: true, dataOptions: { alliances: [{ name: "Foo", count: 2 }], noAllianceCount: 1, dispatchLevels: [5, 6] } },
  async ({ h, d, kind }) => {
    await resolveAll(h); await setSelect(h, "map.allianceFilter", kind === "original" ? `name:${encodeURIComponent("Foo")}` : "Foo");
    await resolveAll(h);
    const refreshed = { serverId: 321, counts: {}, alliances: [{ name: "Other", count: 1 }], names: { resource: [], monster: [] }, dispatchLevels: [], rewardItems: { truck: [], railway: [] }, treasureTypes: [], noAllianceCount: 0, scanProgress: null };
    if (kind === "original") h.setStubBehavior("dataOptions", { mode: "auto", value: () => refreshed }); else h.api.dataOptions = async () => refreshed;
    const n = h.requests.length;
    if (kind === "original") { await h.emitScanState({ isReading: true }); await h.emitScanState({ isReading: false }); } else await h.advance(5000);
    await resolveAll(h);
    const alliance = selectNode(h, "map.allianceFilter").props.value;
    return { allianceValueAfterRefresh: alliance === "Foo" || alliance === `name:${encodeURIComponent("Foo")}` ? "still-Foo" : alliance, lastQueryAlliance: h.requests.at(-1).query.alliance ?? null, requestsAfterRefresh: h.requests.length - n >= 1 };
  });

await candidate("C03-clear-data-resets-filters", "DEFECT?", "Clear-data: alliance, dispatch level, name selection and keyword afterwards",
  { tab: "dispatch", online: true, dataOptions: { ...NAMES, alliances: [{ name: "Foo", count: 2 }], dispatchLevels: [5, 6] } },
  async ({ h, d }) => {
    await resolveAll(h); await setSelect(h, "map.level", "5"); await resolveAll(h);
    await d.typeKeyword("kw"); await resolveAll(h);
    const before = selectNode(h, "map.level").props.value;
    const clear = buttonByText(h, "map.clearServer"); clear.props.onClick(); await h.settle(); await h.settle(); await resolveAll(h);
    return { levelBefore: before, levelAfter: selectNode(h, "map.level").props.value, keywordAfter: d.keywordValue(), lastQuery: Object.fromEntries(Object.entries(h.requests.at(-1).query).filter(([k]) => ["minLevel", "maxLevel", "keyword"].includes(k))) };
  });

await candidate("C04-clear-data-resets-name-selection", "DEFECT?", "Clear-data on the resource tab with a selected name (options keep offering the name afterwards)",
  { tab: "resource", online: true, dataOptions: NAMES },
  async ({ h, d }) => {
    await resolveAll(h); await d.selectName("r2"); await resolveAll(h);
    const clear = buttonByText(h, "map.clearServer"); clear.props.onClick(); await h.settle(); await h.settle(); await resolveAll(h);
    return { nameAfterClear: d.nameValue() };
  });

await candidate("C05-share-failure-message", "DEFECT?", "Share rejects with an error that has a code: visible message text",
  { tab: "dispatch", online: true, schedulingProvider: true },
  async ({ h, d }) => {
    await resolveAll(h, { rows: [dispatchRow("1001")], total: 1 }); await d.toggleRow("dispatch", dispatchRow("1001"));
    await d.clickButtonByText("map.shareAlliance");
    const call = d.callsOf("share").at(-1); await d.rejectCall(call, Object.assign(new Error("game disconnected"), { code: "DISPATCH_PLUNDER_GAME_DISCONNECTED" }));
    return { message: d.messageText() };
  });

await candidate("C06-export-messages", "DEFECT?", "City export success and failure: message text",
  (kind) => ({ tab: "city", online: true, props: kind === "canonical" ? {} : {} }),
  async ({ h, d, kind }) => {
    await resolveAll(h);
    if (kind === "original") h.setStubBehavior("cityExport", { mode: "auto", value: () => ({ canceled: false, rowCount: 7, path: "C:/x.xlsx" }) }); else h.api.exportCities = async () => ({ canceled: false, rowCount: 7, path: "C:/x.xlsx" });
    const run = async () => { const b = h.findNodes((n) => n.type === "button" && (nodeText(n) === "map.exportExcel"))[0]; b.props.onClick(); await h.settle(); await h.settle(); };
    await run();
    const success = d.messageText();
    if (kind === "original") h.setStubBehavior("cityExport", { mode: "reject", error: Object.assign(new Error("boom"), { code: "X" }) }); else h.api.exportCities = async () => { throw Object.assign(new Error("boom"), { code: "X" }); };
    await run();
    const alerts = h.findNodes((n) => n.props?.role === "alert").map((n) => nodeText(n));
    return { success, afterFailureMessages: d.messageText(), afterFailureAlerts: alerts };
  });

await candidate("C07-scan-completion-and-progress-refetch", "DEFECT?", "Rows refetch while a scan reads (1 s) and when it completes",
  { tab: "city", online: true },
  async ({ h, d, kind }) => {
    await resolveAll(h);
    const scanning = { serverId: 321, isReading: true, phase: "reading", readBlocks: 1, totalBlocks: 10, selectedTypes: ["city"], scanMode: "normal", scanRunId: "r1" };
    const n0 = h.requests.length;
    if (kind === "original") await h.emitScanState(scanning); else { h.liveStatusListener()(scanning); await h.settle(); }
    await h.advance(1500);
    const during = h.requests.length - n0; await resolveAll(h);
    const n1 = h.requests.length;
    const done = { ...scanning, isReading: false, phase: "completed", readBlocks: 10 };
    if (kind === "original") await h.emitScanState(done); else { h.liveStatusListener()(done); await h.settle(); await h.settle(); }
    return { searchRequestsDuringScan: during, searchRequestsAtCompletion: h.requests.length - n1 };
  });

await candidate("C08-treasure-defaults-and-query", "DEFECT?", "Treasure tab: default checkboxes and query fields",
  (kind) => ({ tab: "treasure", online: false }),
  async ({ h, d }) => {
    await resolveAll(h);
    const labelChecked = (text) => { const label = h.findNodes((n) => n.type === "label" && nodeText(n) === text)[0]; return treeNodes(label).find((n) => n.type === "input").props.checked; };
    return { luckyChecked: labelChecked("map.prioritizeLuckyTreasures"), foreignChecked: labelChecked("map.showForeignRadarTreasures"), query: d.lastQuery(), rawQueryKeys: Object.keys(h.requests.at(-1).query).filter((k) => h.requests.at(-1).query[k] !== undefined).sort() };
  });

await candidate("C09-alliance-name-none-collision", "DEFECT?", "Alliance literally named none: choosing it must filter that alliance, not 'without alliance'",
  { tab: "city", online: true, dataOptions: { alliances: [{ name: "none", count: 2 }], noAllianceCount: 1 } },
  async ({ h, d, kind }) => {
    await resolveAll(h);
    await setSelect(h, "map.allianceFilter", kind === "original" ? `name:${encodeURIComponent("none")}` : "none");
    const query = h.requests.at(-1).query;
    return { alliance: query.alliance ?? null, withoutAlliance: query.withoutAlliance ?? null };
  });

await candidate("C10-job-list-load-counts", "DEFECT?", "Plunder job list loads: mount, Scheduled entry, change event, after schedule success",
  { tab: "dispatch", online: true, schedulingProvider: true },
  async ({ h, d }) => {
    await resolveAll(h, { rows: [dispatchRow("1001")], total: 1 });
    const mount = jobCalls(h);
    await h.clickTab("scheduledPlunder"); const entry = jobCalls(h) - mount;
    await d.emitJobsChanged(); const event = jobCalls(h) - mount - entry;
    await h.clickTab("dispatch"); await resolveAll(h, { rows: [dispatchRow("1001")], total: 1 });
    await d.toggleRow("dispatch", dispatchRow("1001"));
    const before = jobCalls(h);
    await d.clickButtonByText("map.scheduleSelected:1");
    await d.resolveCall(d.callsOf("scheduleDispatch").at(-1), undefined);
    return { mount, entry, event, afterScheduleSuccess: jobCalls(h) - before, tab: h.getState("tab") };
  });

await candidate("C11-online-toggle-refetch", "DEFECT?", "online prop false -> true refetches",
  { tab: "city", online: false },
  async ({ h }) => { await resolveAll(h); const n = h.requests.length; await h.setProps({ online: true }); return { newRequests: h.requests.length - n }; });

await candidate("C12-name-select-at-page-3", "DEFECT?", "Select a name at page 3: requests",
  { tab: "resource", online: true, dataOptions: NAMES },
  async ({ h, d }) => { await resolveAll(h, { rows: [], total: 230 }); await d.setPage(3); await resolveAll(h, { rows: [], total: 230 }); await d.typeKeyword("abc"); const n = h.requests.length; await d.selectName("r2"); return { requests: reqs(h, n), keyword: d.keywordValue(), page: d.page() }; });

await candidate("C13-sort-at-page-3", "DEFECT?", "Sort click at page 3: page and request",
  { tab: "dispatch", online: true },
  async ({ h, d }) => {
    await resolveAll(h, { rows: [dispatchRow("1001")], total: 230 }); await d.setPage(3); await resolveAll(h, { rows: [dispatchRow("1001")], total: 230 });
    const n = h.requests.length; const props = d.tableProps(); props.onSort("level"); await h.settle();
    return { requests: h.requests.slice(n).map((r) => ({ page: r.query.page, sorts: r.query.sorts })), page: d.page() };
  });

await candidate("C14-tab-counts-after-clear-data", "DEFECT?", "Tab count badges at mount and after clear-data",
  { tab: "city", online: true },
  async ({ h, d, kind }) => {
    await resolveAll(h); const mount = d.tabTexts();
    // The harness summary stub is static; a real provider answers zeros after a clear, so model that.
    if (kind === "canonical") h.api.summary = async () => ({ serverId: 321, counts: Object.fromEntries(["city", "resource", "monster", "truck", "railway", "dispatch", "ghost", "treasure"].map((k) => [k, 0])), scanState: { serverId: 321, isReading: false, phase: "idle", selectedTypes: ["city"], scanMode: "normal" } });
    buttonByText(h, "map.clearServer").props.onClick(); await h.settle(); await h.settle(); await resolveAll(h);
    return { mount, afterClear: d.tabTexts() };
  });

await candidate("C15-mark-button-predicate", "DEFECT?", "Player-mark button in the city table: disabled offline / while scanning / with a busy action",
  { tab: "city", online: false },
  async (impl) => {
    const { h, d } = impl;
    const rows = [{ serverId: 321, uuid: "c1", ownerUid: "u1", ownerName: "A", x: 3, y: 4, marked: false, updatedAt: NOW }, { serverId: 321, uuid: "c2", ownerUid: "", ownerName: "B", x: 5, y: 6, marked: false, updatedAt: NOW }];
    await resolveAll(h, { rows, total: 2 });
    const marks = async () => tableButtons(await tableView(impl)).filter((b) => b.cls.includes("map-mark-button")).map((b) => b.disabled);
    const out = { offline: await marks() };
    await h.setProps({ online: true }); out.online = await marks();
    if (impl.kind === "original") await h.emitScanState({ isReading: true });
    if (impl.kind === "canonical") { h.liveStatusListener()({ serverId: 321, isReading: true, phase: "reading", readBlocks: 1, totalBlocks: 10, selectedTypes: ["city"], scanMode: "normal" }); await h.settle(); }
    out.scanReading = await marks();
    return out;
  });

await candidate("C16-coordinate-and-jump-buttons", "DOCUMENTED", "Coordinate jump button disabled predicate (offline / online)",
  { tab: "city", online: false },
  async (impl) => {
    const { h } = impl;
    const rows = [{ serverId: 321, uuid: "c1", ownerUid: "u1", x: 3, y: 4, updatedAt: NOW }];
    await resolveAll(h, { rows, total: 1 });
    const jump = async () => tableButtons(await tableView(impl)).filter((b) => b.cls.includes("map-coordinate-button")).map((b) => b.disabled);
    const out = { offline: await jump() }; await h.setProps({ online: true }); out.online = await jump(); return out;
  });

await candidate("C17-checkbox-expiry-clock", "DEFECT?", "Selected dispatch row expires while the tab is open: checkbox disabled/checked after 6 s",
  { tab: "dispatch", online: true },
  async (impl) => {
    const { h, d } = impl;
    const row = dispatchRow("1001", { taskExpireTime: NOW + 5000, plunderAt: NOW + 3_600_000 });
    await resolveAll(h, { rows: [row], total: 1 });
    await d.toggleRow("dispatch", row);
    const box = async () => { const b = treeNodes(await tableView(impl)).find((n) => n.type === "input" && n.props.type === "checkbox"); return { checked: b.props.checked, disabled: b.props.disabled }; };
    const before = await box(); await h.advance(6000); return { before, after: await box() };
  });

await candidate("C18-ticker-intervals", "DEFECT?", "Active 1 s intervals per tab (page + table clocks together)",
  { tab: "city", online: true },
  async ({ h }) => {
    const out = {};
    for (const tab of ["city", "truck", "dispatch", "railway", "scheduledPlunder"]) {
      if (tab !== "city") await h.clickTab(tab);
      await resolveAll(h);
      out[tab] = h.timers ? h.timers().intervals : "n/a";
    }
    return out;
  });

await candidate("C19-schedule-handler-busy-and-failure", "DEFECT?", "Schedule / truck schedule busy keys, disabled states, failure keeps selection and busy cleared",
  { tab: "dispatch", online: true, schedulingProvider: true },
  async ({ h, d }) => {
    const row = dispatchRow("1001"); await resolveAll(h, { rows: [row], total: 1 }); await d.toggleRow("dispatch", row);
    await d.clickButtonByText("map.scheduleSelected:1");
    const during = { buttons: d.scheduleButtonStates(), call: d.callsOf("scheduleDispatch").length };
    await d.rejectCall(d.callsOf("scheduleDispatch").at(-1), new Error("boom"));
    return { during, after: d.scheduleButtonStates(), keys: d.selectionKeys(), tab: h.getState("tab") };
  });

await candidate("C20-cancel-clear-busy", "DEFECT?", "Scheduled actions: busy key and calls for cancel/clear",
  { tab: "scheduledPlunder", online: true, schedulingProvider: true, jobs: { dispatchJobs: [{ serverId: 321, uuid: "1001", taskKind: "dispatch", scheduleStatus: "scheduled" }, { serverId: 321, uuid: "g1", taskKind: "ghost", scheduleStatus: "scheduled" }, { serverId: 321, uuid: "1002", taskKind: "dispatch", scheduleStatus: "succeeded" }], truckJobs: [{ serverId: 321, uuid: "t1", jobId: "j", scheduledAt: NOW, scheduleStatus: "scheduled", maxLootCount: 3, arriveTs: NOW + 99999 }] } },
  async ({ h, d }) => {
    await h.settle(); await d.settleFlow();
    const s = d.scheduled();
    s.cancelDispatch(s.ghostJobs[0]); await d.settleFlow();
    const ghostCancel = { args: d.callsOf("cancelDispatch").at(-1).args, busy: d.scheduled().busyKey };
    await d.resolveCall(d.callsOf("cancelDispatch").at(-1), undefined);
    d.scheduled().clear("ghost"); await d.settleFlow();
    const clear = { args: [typeof d.callsOf("clearDispatch").at(-1).args[0], d.callsOf("clearDispatch").at(-1).args[1]], busy: d.scheduled().busyKey };
    await d.resolveCall(d.callsOf("clearDispatch").at(-1), undefined);
    d.scheduled().cancelTruck(d.scheduled().truckJobs[0]); await d.settleFlow();
    return { ghostCancel, clear, truckBusy: d.scheduled().busyKey, afterAll: d.scheduled().busyKey };
  });

await candidate("C21-selection-after-clear-data", "DEFECT?", "Dispatch and truck selections after clear-data",
  { tab: "truck", online: true },
  async ({ h, d }) => {
    await resolveAll(h, { rows: [truckRow("t1")], total: 1 }); await d.toggleRow("truck", truckRow("t1"));
    buttonByText(h, "map.clearServer").props.onClick(); await h.settle(); await h.settle(); await resolveAll(h);
    return { truckKeys: d.selectionKeys() };
  });

await candidate("C22-search-error-message", "DOCUMENTED", "Search failure visible surface",
  { tab: "city", online: true },
  async ({ h, d }) => { await resolveAll(h); await d.clickSearch(); h.requests.at(-1).reject(new Error("backend gone")); await h.settle(); return { alerts: h.findNodes((n) => n.props?.role === "alert").length, rows: d.tableRows().length }; });

await candidate("C23-quality-filter-per-tab-and-reset", "DEFECT?", "Quality filter kept per tab, not applied to city; page reset on change",
  { tab: "truck", online: true },
  async ({ h, d }) => {
    await resolveAll(h, { rows: [], total: 230 }); await d.setPage(3); await resolveAll(h, { rows: [], total: 230 });
    await setSelect(h, "map.quality", "ur"); const afterQuality = { page: d.page(), q: d.lastQuery() }; await resolveAll(h);
    await h.clickTab("city"); await resolveAll(h); const city = d.lastQuery(); await h.clickTab("truck");
    return { afterQuality, cityQuery: city, back: d.lastQuery() };
  });

await candidate("C24-item-filter-sort-cleanup", "DEFECT?", "Clearing the item filter removes the itemCount sort",
  { tab: "truck", online: true, dataOptions: { rewardItems: { truck: [{ key: "gold", name: "Gold" }], railway: [] } } },
  async ({ h, d }) => {
    await resolveAll(h);
    const nodeItem = h.findNodes((n) => typeof n.type === "function" && n.props && ("onChange" in n.props) && ("allLabel" in n.props || n.type.name === "ct"))[0];
    if (nodeItem) { nodeItem.props.onChange("gold"); await h.settle(); } else { await setSelect(h, "map.itemFilter", "gold"); }
    await resolveAll(h); d.tableProps().onSort("itemCount"); await h.settle(); await resolveAll(h);
    const sortsBefore = h.requests.at(-1).query.sorts;
    const n2 = h.findNodes((n) => typeof n.type === "function" && n.props && ("onChange" in n.props) && ("allLabel" in n.props || n.type.name === "ct"))[0];
    if (n2) { n2.props.onChange(""); await h.settle(); } else { await setSelect(h, "map.itemFilter", ""); }
    return { sortsBefore, sortsAfter: h.requests.at(-1).query.sorts, itemKeyAfter: h.requests.at(-1).query.itemKey ?? null };
  });

await candidate("C25-pagination-prop-range", "DEFECT?", "Pagination onPage with 0 and -3",
  { tab: "city", online: true },
  async ({ h, d }) => { await resolveAll(h, { rows: [], total: 120 }); await d.setPage(0); const a = h.requests.at(-1).query.page; await resolveAll(h); await d.setPage(-3); return { zero: a, minus3: h.requests.at(-1).query.page }; });

await candidate("C26-stale-selection-snapshot", "DEFECT?", "Selected row refreshed by the backend: payload scheduled is the old snapshot",
  { tab: "dispatch", online: true, schedulingProvider: true },
  async ({ h, d }) => {
    await resolveAll(h, { rows: [dispatchRow("1001", { plunderAt: NOW + 1000 })], total: 1 }); await d.toggleRow("dispatch", dispatchRow("1001", { plunderAt: NOW + 1000 }));
    await d.clickSearch(); await resolveAll(h, { rows: [dispatchRow("1001", { plunderAt: NOW + 99999 })], total: 1 });
    await d.clickButtonByText("map.scheduleSelected:1");
    return { plunderAtSent: d.callsOf("scheduleDispatch").at(-1).args[0][0].plunderAt - NOW };
  });

await candidate("C04b-clear-data-name-with-real-backend-names-empty", "DEFECT?", "Clear-data on the resource tab when the backend then offers no names (what a cleared server returns)",
  { tab: "resource", online: true, dataOptions: NAMES },
  async ({ h, d, kind }) => {
    await resolveAll(h); await d.selectName("r2"); await resolveAll(h);
    const empty = { serverId: 321, counts: {}, alliances: [], names: { resource: [], monster: [] }, dispatchLevels: [], rewardItems: { truck: [], railway: [] }, treasureTypes: [], noAllianceCount: 0, scanProgress: null };
    if (kind === "original") h.setStubBehavior("dataOptions", { mode: "auto", value: () => empty }); else h.api.dataOptions = async () => empty;
    buttonByText(h, "map.clearServer").props.onClick(); await h.settle(); await h.settle(); await resolveAll(h);
    return { nameAfterClear: d.nameValue(), lastQueryResourceNameKey: h.requests.at(-1).query.resourceNameKey ?? null };
  });

await candidate("C27-scan-start-error-banner", "DEFECT?", "Start-scan failure with a coded error: banner text",
  { tab: "city", online: true },
  async ({ h, kind }) => {
    await resolveAll(h);
    const error = Object.assign(new Error("GAME_DISCONNECTED"), { code: "GAME_DISCONNECTED" });
    if (kind === "original") h.setStubBehavior("scanStart", { mode: "reject", error }); else h.api.start = async () => { throw error; };
    buttonByText(h, "map.startReading").props.onClick(); await h.settle(); await h.settle();
    return { alerts: h.findNodes((n) => n.props?.role === "alert").map((n) => nodeText(n)) };
  });

results.differing =results.candidates.filter((c) => c.differs).map((c) => c.id);
console.log(`\ndiffering candidates: ${results.differing.join(", ") || "(none)"}`);
if (record) { fs.writeFileSync(path.join(originalDir, "final-review-results.json"), `${JSON.stringify(results, null, 2)}\n`); console.log("final-review-results.json written"); }
void RESOURCE_OPTIONS; void textsOf; void quiet;

// C28: persistence of the treasure checkboxes (original writes localStorage; canonical page has no such key).
{
  const { createOriginalHarness } = await import("./original-runtime.mjs");
  const h = await createOriginalHarness({ online: false, storage: { "lwbridge.mapLuckyTreasurePriority": "false", "lwbridge.mapIncludeForeignRadarTreasures": "true" } });
  await h.mount();
  await h.clickTab("treasure");
  const checked = (text) => { const label = h.findNodes((n) => n.type === "label" && nodeText(n) === text)[0]; return treeNodes(label).find((n) => n.type === "input").props.checked; };
  const entry = {
    id: "C28-treasure-checkbox-persistence", classification: "DEFECT?", description: "Treasure checkboxes initialise from / persist to localStorage in the original",
    original: { luckyFromStorageFalse: checked("map.prioritizeLuckyTreasures"), foreignFromStorageTrue: checked("map.showForeignRadarTreasures"), storageKeysWritten: [...new Set(h.storageWrites.map(([k]) => k))].sort() },
    canonical: { localStorageKeysInPage: [...new Set([...fs.readFileSync(SOURCES.current, "utf8").matchAll(/localStorage\.(?:get|set)Item\(([^,)]+)/g)].map((m) => m[1]))] },
  };
  entry.differs = true;
  results.candidates.push(entry);
  console.log("DIFFERS [DEFECT?] C28-treasure-checkbox-persistence", JSON.stringify(entry.canonical));
  if (record) fs.writeFileSync(path.join(originalDir, "final-review-results.json"), `${JSON.stringify(results, null, 2)}\n`);
}
