// Negative / edge cases for topics 1-8 of the ORIGINAL Map panel. Every "observed" value is produced by executing
// the original component (original-runtime.mjs) or the AST-extracted pure expressions; nothing here is an expectation.
//
//   node .../original/negative-cases.mjs     -> writes original/negative-cases.md and original/negative-results.json
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import {
  boot, drain, since, summarize, definedKeys, dispatchRow, truckRow, cityRow, openTab, checkboxView, buttonView, optionsReply,
  NOW, SERVER, createOriginalHarness, nodeText, treeNodes, filterCheckbox,
} from "./common.mjs";
import { originalDir } from "./lib.mjs";

const cases = [];
async function neg(topic, id, description, fn) {
  let observed;
  try { observed = await fn(); } catch (error) { observed = { threw: String(error?.message || error).slice(0, 200) }; }
  cases.push({ topic, id, description, observed });
  console.log(`done ${id}`);
}
const pickSelect = async (h, key, value) => { const s = h.findSelect(key); assert.ok(s, `select ${key}`); s.props.onChange({ target: { value } }); await h.settle(); };
const last = (h, name) => h.callsNamed(name).at(-1);
const qshort = (r) => ({ kind: r.kind, keyword: r.query.keyword, page: r.query.page, serverId: r.query.serverId });
const snap = (h) => ({ tab: h.getState("tab"), page: h.getState("page"), total: h.getState("total"), rows: h.getState("rows").map((r) => r.uuid), loading: h.getState("loading") });
const err = (m) => new Error(m);

// ====================================================== topic 1: keyword / search issuing
await neg(1, "kw-empty-search", "Search with an empty keyword", async () => {
  const h = await boot(); await drain(h); const n = h.requests.length; await h.clickSearch();
  return { requests: since(h, n), keywordSent: JSON.stringify(h.requests.at(-1).query.keyword) };
});
await neg(1, "kw-whitespace-only", "Keyword of three spaces, then Search", async () => {
  const h = await boot(); await drain(h); await h.typeKeyword("   "); const n = h.requests.length; await h.clickSearch();
  return { keywordSent: JSON.stringify(h.requests.at(-1).query.keyword), requests: since(h, n).length };
});
await neg(1, "kw-unicode-and-very-long", "Emoji/CJK plus 5000 characters", async () => {
  const text = `😀漢字${"x".repeat(5000)}`;
  const h = await boot(); await drain(h); await h.typeKeyword(text); await h.clickSearch();
  const sent = h.requests.at(-1).query.keyword;
  return { sentEqualsTyped: sent === text, sentLength: sent.length, inputKeepsAll: h.findInput("map.searchLabel").props.value.length };
});
await neg(1, "kw-enter-key-and-form", "Enter key: handlers on the input, wrapping <form>", async () => {
  const h = await boot(); await drain(h);
  const input = h.findInput("map.searchLabel");
  return { inputProps: Object.keys(input.props), hasOnKeyDown: "onKeyDown" in input.props, formElementsInTree: h.findNodes((n) => n.type === "form").length };
});
await neg(1, "kw-typing-while-request-pending", "Keyword typed while the tab-entry request is still pending, then the old reply arrives", async () => {
  const h = await boot();
  const current = h.requests.at(-1); // request 1 is already superseded by the post-options request 2
  await h.typeKeyword("later");
  current.done = true; await h.resolveRequest(current, { rows: [cityRow("old-query-row")], total: 1 });
  return { pendingRequestIds: h.requests.map((r) => r.id), rowsApplied: h.getState("rows").map((r) => r.uuid), keywordInInput: h.getState("keyword"), newRequestsFromTyping: h.requests.length - 2 };
});
await neg(1, "kw-search-with-no-data-server", "Search clicked while the data server is 0 (no scan server)", async () => {
  const h = await boot(); await drain(h); await h.emitServer(0); const n = h.requests.length; await h.clickSearch();
  return { requests: since(h, n), page: h.getState("page"), loading: h.getState("loading") };
});
await neg(1, "kw-search-offline-and-scanning", "Search while offline and while a scan is reading", async () => {
  const h = await boot(); await drain(h);
  await h.setProps({ online: false }); let n = h.requests.length; await h.clickSearch(); const offline = since(h, n).length; await drain(h);
  await h.emitScanState({ isReading: true }); n = h.requests.length; await h.clickSearch(); const scanning = since(h, n).length;
  return { requestsWhileOffline: offline, requestsWhileScanning: scanning };
});
await neg(1, "kw-rapid-search-clicks", "Three quick Search clicks at page 1, replies out of order", async () => {
  const h = await boot(); await drain(h);
  const n = h.requests.length;
  for (let i = 0; i < 3; i += 1) { await h.typeKeyword(`k${i}`); await h.clickSearch(); }
  const [a, b, c] = h.requests.slice(n);
  b.done = true; await h.resolveRequest(b, { rows: [cityRow("b")], total: 1 });
  c.done = true; await h.resolveRequest(c, { rows: [cityRow("c")], total: 1 });
  a.done = true; await h.resolveRequest(a, { rows: [cityRow("a")], total: 1 });
  return { requests: h.requests.slice(n).map(qshort), rowsAfterAllReplies: h.getState("rows").map((r) => r.uuid) };
});
await neg(1, "kw-search-rejected", "Backend rejects the search", async () => {
  const h = await boot(); await drain(h, { rows: [cityRow("a")], total: 9 }); await h.typeKeyword("x"); await h.clickSearch();
  await h.rejectRequest(h.requests.at(-1), err("backend gone"));
  return { state: snap(h), log: h.logs.at(-1), message: h.getState("message") };
});
await neg(1, "kw-stale-rejection", "A superseded request rejects after a newer request succeeded", async () => {
  const h = await boot(); await drain(h); await h.typeKeyword("1"); await h.clickSearch(); const x = h.requests.at(-1);
  await h.typeKeyword("2"); await h.clickSearch(); const y = h.requests.at(-1);
  y.done = true; await h.resolveRequest(y, { rows: [cityRow("y")], total: 1 });
  const logsBefore = h.logs.length; x.done = true; await h.rejectRequest(x, err("late failure"));
  return { state: snap(h), newLogLines: h.logs.length - logsBefore };
});
await neg(1, "kw-stale-after-tab-change", "City request pending, tab switched, city reply arrives", async () => {
  const h = await boot(); await drain(h); await h.clickSearch(); const cityReq = h.requests.at(-1);
  await h.clickTab("monster"); const monsterReq = h.requests.at(-1);
  cityReq.done = true; await h.resolveRequest(cityReq, { rows: [cityRow("CITY")], total: 7 });
  const afterStale = snap(h);
  monsterReq.done = true; await h.resolveRequest(monsterReq, { rows: [{ uuid: "M", serverId: SERVER }], total: 1 });
  return { afterStaleCityReply: afterStale, afterMonsterReply: snap(h), cacheHasCity: [...h.getRef("tabCache").current.keys()] };
});

// ====================================================== topic 2: Search button
await neg(2, "search-page3-pending-then-search", "Page-3 request still pending, Search clicked", async () => {
  const h = await boot(); await drain(h, { rows: [], total: 230 }); await h.setPage(3); const p3 = h.requests.at(-1);
  await h.clickSearch();
  return { requests: h.requests.slice(-2).map(qshort), pageNow: h.getState("page"), page3RequestIsStale: p3.id !== h.requests.at(-1).id };
});
await neg(2, "search-treasure-tab-waiting-for-viewer", "Treasure tab, online, viewer identity unknown: effect waits, Search button still requests", async () => {
  const h = await boot(); await h.clickTab("treasure");
  const afterEntry = h.requests.filter((r) => r.kind === "treasure").length;
  await h.clickSearch();
  const treasure = h.requests.filter((r) => r.kind === "treasure");
  return { treasureRequestsAfterTabEntry: afterEntry, treasureRequestsAfterSearchClick: treasure.length, viewerUidSent: treasure.at(-1)?.query.viewerUid };
});

// ====================================================== topic 3: name select
await neg(3, "name-unknown-value", "Select value not offered by the options", async () => {
  const h = await boot(); await openTab(h, "resource"); await h.selectName("nope");
  const sent = h.requests.at(-1).query.resourceNameKey; const shown = h.findSelect("common.name").props.value;
  await drain(h); await h.emitScanState({ isReading: true }); await h.emitScanState({ isReading: false });
  return { storedAndSent: sent, selectValueShown: shown, afterOptionsRefresh: h.getState("nameSelection") };
});
await neg(3, "name-empty-string", "Choose All (empty string)", async () => {
  const h = await boot(); await openTab(h, "resource"); await h.selectName("r1"); await drain(h); const n = h.requests.length; await h.selectName("");
  return { nameSelection: h.getState("nameSelection"), requests: since(h, n), keyword: h.getState("keyword") };
});
await neg(3, "name-select-while-loading", "Select while the tab-entry request is pending", async () => {
  const h = await boot(); await h.clickTab("resource"); const first = h.requests.at(-1); await h.selectName("r2"); const second = h.requests.at(-1);
  first.done = true; await h.resolveRequest(first, { rows: [{ uuid: "OLD", serverId: SERVER }], total: 1 });
  return { requests: [first, second].map((r) => ({ id: r.id, name: r.query.resourceNameKey })), rowsAfterObsoleteReply: h.getState("rows").map((r) => r.uuid) };
});
await neg(3, "name-key-with-whitespace", "Option key with surrounding whitespace is selected", async () => {
  const names = { resource: [{ key: " r3 ", count: 1 }], monster: [] };
  const h = await boot({ stubs: { dataOptions: { mode: "auto", value: optionsReply({ names }) } } }); await openTab(h, "resource"); await h.selectName(" r3 ");
  const sent = JSON.stringify(h.requests.at(-1).query.resourceNameKey); await drain(h); await h.emitScanState({ isReading: true }); await h.emitScanState({ isReading: false });
  return { sentKey: sent, keptAfterRefresh: JSON.stringify(h.getState("nameSelection").resource) };
});
await neg(3, "name-duplicate-option-keys", "Backend offers the same key twice", async () => {
  const names = { resource: [{ key: "r1", count: 1 }, { key: "r1", count: 2 }], monster: [] };
  const h = await boot({ stubs: { dataOptions: { mode: "auto", value: optionsReply({ names }) } } }); await openTab(h, "resource");
  const opts = treeNodes(h.findSelect("common.name")).filter((n) => n.type === "option").map((o) => ({ value: o.props.value, key: o.key, text: nodeText(o) }));
  await h.selectName("r1");
  return { options: opts, selected: h.getState("nameSelection") };
});
await neg(3, "name-typing-on-other-tab-keeps-this-tab-selection", "Resource selection survives typing on the monster tab", async () => {
  const h = await boot(); await openTab(h, "resource"); await h.selectName("r1"); await drain(h);
  await h.clickTab("monster"); await drain(h); await h.selectName("m1"); await drain(h); await h.typeKeyword("abc");
  return { nameSelection: h.getState("nameSelection") };
});
await neg(3, "name-options-refresh-empty-list", "Options refresh returns empty name lists while a name is selected", async () => {
  const h = await boot(); await openTab(h, "resource"); await h.selectName("r2"); await drain(h);
  h.setStubBehavior("dataOptions", { mode: "auto", value: optionsReply({ names: { resource: [], monster: [] } }) });
  await h.emitScanState({ isReading: true }); await h.emitScanState({ isReading: false });
  return { nameSelection: h.getState("nameSelection"), optionCount: treeNodes(h.findSelect("common.name")).filter((n) => n.type === "option").length };
});
await neg(3, "name-options-for-wrong-server-reply", "Options reply for a different server than the data server", async () => {
  const h = await boot({ stubs: { dataOptions: { mode: "manual" } } });
  const call = last(h, "dataOptions");
  await h.resolveCall(call, optionsReply()(999));
  return { dataServerIdAfter: h.getState("dataServerId"), namesApplied: h.getState("nameOptions").resource.length, newOptionCalls: h.callsNamed("dataOptions").map((c) => c.args[0]) };
});

// ====================================================== topic 4: projection
await neg(4, "nr-level-not-a-number", "Dispatch level select receives 'abc' and '0'", async () => {
  const h = await boot(); await openTab(h, "dispatch");
  await pickSelect(h, "map.level", "abc"); const abc = h.requests.at(-1).query; await drain(h);
  await pickSelect(h, "map.level", "0"); const zero = h.requests.at(-1).query;
  return { abc: { minLevel: Number.isNaN(abc.minLevel) ? "NaN" : abc.minLevel, maxLevel: Number.isNaN(abc.maxLevel) ? "NaN" : abc.maxLevel }, zero: { minLevel: zero.minLevel, maxLevel: zero.maxLevel } };
});
await neg(4, "nr-alliance-values", "Alliance filter values: malformed percent-encoding, empty name, none", async () => {
  const h = await boot(); await drain(h); const out = {};
  for (const v of ["name:%E0%A4%A", "name:", "none", "name:Foo%20Bar", "bogus"]) { await pickSelect(h, "map.allianceFilter", v); const q = h.requests.at(-1).query; out[v] = { alliance: q.alliance, withoutAlliance: q.withoutAlliance }; await drain(h); }
  return out;
});
await neg(4, "nr-unknown-filter-values", "Unknown quality/status values pass straight through", async () => {
  const h = await boot(); await openTab(h, "dispatch");
  await pickSelect(h, "map.quality", "xyz"); const q1 = h.requests.at(-1).query; await drain(h);
  await pickSelect(h, "common.status", "foo"); const q2 = h.requests.at(-1).query;
  return { quality: q1.quality, specialOnly: q1.specialOnly, completionStatus: q2.completionStatus };
});
await neg(4, "nr-plunderable-per-tab", "plunderableOnly projection: railway vs ghost", async () => {
  const h = await boot({ stubs: { dataOptions: { mode: "auto", value: optionsReply() } } }); await openTab(h, "railway");
  filterCheckbox(h, "map.plunderableOnly").props.onChange({ target: { checked: true } }); await h.settle();
  const railway = h.requests.at(-1).query.plunderableOnly; await drain(h);
  await h.clickTab("ghost"); const ghostCheckbox = !!filterCheckbox(h, "map.plunderableOnly");
  return { railwayPlunderableOnly: railway, ghostHasPlunderableCheckbox: ghostCheckbox, ghostQueryPlunderableOnly: h.requests.at(-1).query.plunderableOnly };
});
await neg(4, "nr-filter-of-other-tab-ignored", "A quality chosen on truck does not leak into city/resource queries", async () => {
  const h = await boot(); await openTab(h, "truck"); await pickSelect(h, "map.quality", "ur"); await drain(h);
  await h.clickTab("city"); const city = h.requests.at(-1).query; await drain(h); await h.clickTab("truck");
  return { cityQuality: city.quality, truckQualityOnReturn: h.requests.at(-1).query.quality };
});
await neg(4, "nr-sort-click", "Sorting a column: page reset and sorts array", async () => {
  const h = await boot(); await openTab(h, "dispatch", [dispatchRow("1001")], 230); await h.setPage(3); await drain(h, { rows: [], total: 230 });
  h.tableNode().props.onSort("level"); await h.settle(); const q = h.requests.at(-1).query;
  h.tableNode().props.onSort("level"); await h.settle(); const q2 = h.requests.at(-1).query;
  h.tableNode().props.onSort("level"); await h.settle(); const q3 = h.requests.at(-1).query;
  return { afterFirst: { page: q.page, sorts: q.sorts }, afterSecond: q2.sorts, afterThird: q3.sorts };
});

// ====================================================== topic 5: tab / server / page
await neg(5, "tab-rapid-switch-out-of-order", "city -> monster -> truck without replies, replies arrive in the worst order", async () => {
  const h = await boot(); await drain(h);
  await h.clickSearch(); const cityReq = h.requests.at(-1);
  await h.clickTab("monster"); const monsterReq = h.requests.at(-1);
  await h.clickTab("truck"); const truckReq = h.requests.at(-1);
  truckReq.done = true; await h.resolveRequest(truckReq, { rows: [truckRow("T")], total: 1 });
  monsterReq.done = true; await h.resolveRequest(monsterReq, { rows: [{ uuid: "M", serverId: SERVER }], total: 9 });
  cityReq.done = true; await h.resolveRequest(cityReq, { rows: [cityRow("C")], total: 99 });
  return { finalState: snap(h), cache: Object.fromEntries([...h.getRef("tabCache").current.entries()].map(([k, v]) => [k, { page: v.page, rows: v.rows.map((r) => r.uuid), total: v.total }])) };
});
await neg(5, "tab-leave-while-loading", "Leave a tab before its first reply, come back", async () => {
  const h = await boot(); await drain(h); await h.clickTab("monster"); await h.clickTab("city");
  return { atReturn: snap(h), cacheKeys: [...h.getRef("tabCache").current.keys()] };
});
await neg(5, "server-change-same-id", "Scan state re-emitted with the same server id", async () => {
  const h = await boot(); await drain(h); const n = h.requests.length; await h.emitServer(SERVER);
  return { newRequests: since(h, n).length, generation: h.getRef("searchGeneration").current };
});
await neg(5, "server-flap", "Server 321 -> 0 -> 321 -> 456 quickly with replies for the old servers arriving late", async () => {
  const h = await boot(); await drain(h, { rows: [cityRow("a")], total: 5 });
  await h.emitServer(0); const afterZero = snap(h);
  await h.emitServer(321); const r321 = h.requests.at(-1);
  await h.emitServer(456); const r456 = h.requests.filter((r) => r.query.serverId === 456 && !r.done)[0];
  r321.done = true; await h.resolveRequest(r321, { rows: [cityRow("S321")], total: 1 });
  const afterLate321 = snap(h);
  r456.done = true; await h.resolveRequest(r456, { rows: [cityRow("S456")], total: 1 });
  return { afterZero, afterLate321, afterCurrent: snap(h), dataServerId: h.getState("dataServerId") };
});
await neg(5, "controlled-unknown-tab", "Controlled mode with an unknown activeTab", async () => {
  const h = await createOriginalHarness({ online: true, tabMode: "controlled", initialTab: "bogus", now: NOW, stubs: { dataOptions: { mode: "auto", value: optionsReply() } } });
  await h.mount();
  let tableThrows = null; try { h.expandedTable(); } catch (e) { tableThrows = String(e.message).slice(0, 80); }
  return { requestKinds: h.requests.map((r) => r.kind), activeTabButtons: h.findNodes((n) => n.type === "button" && n.props.className === "active").length, expandedTableThrows: tableThrows };
});
await neg(5, "no-scan-state-prop", "scanState prop null (parent has not loaded it yet)", async () => {
  const h = await createOriginalHarness({ online: true, now: NOW, props: { scanState: null, summary: null } }); await h.mount();
  return { requests: h.requests.length, dataServerId: h.getState("dataServerId"), loading: h.getState("loading"), tabCountText: nodeText(h.findNodes((n) => n.type === "span" && n.props.className === "map-tab-count")[0]), searchDisabled: h.findButton("common.search").props.disabled };
});
await neg(5, "backend-loss-offline-midsession", "online prop turns false while on dispatch with a selection", async () => {
  const h = await boot(); await openTab(h, "dispatch", [dispatchRow("1001")]); await h.toggleRow(0);
  const n = h.requests.length; await h.setProps({ online: false });
  return { newRequests: since(h, n).length, selectionKept: h.getState("selectedDispatchKeys"), schedule: buttonView(h.findButton("map.scheduleSelected", { count: 1 })), share: buttonView(h.findButton("map.shareAlliance")), jumpDisabledProp: h.tableNode().props.jumpDisabled };
});
await neg(5, "page-change-prop-beyond-range", "Pagination prop invoked with out-of-range pages", async () => {
  const h = await boot(); await drain(h, { rows: [], total: 120 });
  await h.setPage(99); const r99 = h.requests.at(-1); r99.done = true; await h.resolveRequest(r99, { rows: [], total: 120 });
  const afterClampAnswer = snap(h);
  await drain(h); await h.setPage(0); const q0 = h.requests.at(-1).query.page; await drain(h); await h.setPage(-3); const qn = h.requests.at(-1).query.page;
  return { page99: afterClampAnswer, page0RequestPage: q0, pageMinus3RequestPage: qn };
});

// ====================================================== topic 6: selection
await neg(6, "sel-odd-identifiers", "Rows with null/undefined/number/object/missing-serverId identifiers (toggled through the production callbacks)", async () => {
  const rows = [
    ["uuid null", dispatchRow(null)], ["uuid undefined", dispatchRow(undefined)], ["uuid number 1001", dispatchRow(1001)], ["uuid number 0", dispatchRow(0)],
    ["uuid padded numeric", dispatchRow(" 7 ")], ["serverId missing", { ...dispatchRow("55"), serverId: undefined }], ["uuid boolean true", dispatchRow(true)],
  ];
  const h = await boot(); await openTab(h, "dispatch", rows.map(([, r]) => r));
  const boxes = checkboxView(h); const result = {};
  for (let i = 0; i < rows.length; i += 1) {
    const before = Object.keys(h.getState("dispatchSelection")).length;
    await h.toggleRow(i);
    const keys = h.getState("selectedDispatchKeys");
    result[rows[i][0]] = { disabled: boxes[i].disabled, checkedAfterToggle: checkboxView(h)[i].checked, storedKey: keys[keys.length - 1] ?? null, countAfter: keys.length, before };
    if (keys.length) await h.toggleRow(i);
  }
  return result;
});
await neg(6, "sel-snapshot-stale-after-refetch", "Selected row changes on the server; refetch shows new data; schedule sends the old snapshot", async () => {
  const h = await boot(); await openTab(h, "dispatch", [dispatchRow("1001", { plunderAt: NOW + 1000 })]); await h.toggleRow(0);
  await h.clickSearch(); await drain(h, { rows: [dispatchRow("1001", { plunderAt: NOW + 99999 })], total: 1 });
  h.findButton("map.scheduleSelected", { count: 1 }).props.onClick(); await h.settle();
  return { plunderAtSent: last(h, "dispatchPlunderSchedule").args[0][0].plunderAt - NOW, plunderAtShownNow: h.getState("rows")[0].plunderAt - NOW };
});
await neg(6, "sel-selected-row-leaves-the-list", "Selected row no longer returned by the backend", async () => {
  const h = await boot(); await openTab(h, "dispatch", [dispatchRow("1001")]); await h.toggleRow(0);
  await h.clickSearch(); await drain(h, { rows: [], total: 0 });
  return { selectedKeys: h.getState("selectedDispatchKeys"), schedule: buttonView(h.findButton("map.scheduleSelected", { count: 1 })) };
});
await neg(6, "sel-invalid-truck-via-callback", "Invalid truck row (empty uuid) toggled through the production callback (a real disabled checkbox cannot fire it)", async () => {
  const h = await boot(); await openTab(h, "truck", [truckRow("")]);
  const before = checkboxView(h)[0];
  await h.tableNode().props.onToggleTruck(truckRow("")); await h.settle();
  return { checkboxDisabled: before.disabled, storedKeys: h.getState("selectedTruckKeys"), scheduleButton: buttonView(h.findButton("map.scheduleSelectedTrucks", { count: 1 })) };
});
await neg(6, "sel-same-uuid-other-server", "Same uuid on two servers keeps separate entries", async () => {
  const h = await boot(); await openTab(h, "dispatch", [dispatchRow("1001"), dispatchRow("1001", { serverId: 456 })]);
  await h.toggleRow(0); await h.toggleRow(1);
  return { keys: h.getState("selectedDispatchKeys"), boxes: checkboxView(h).map((b) => b.checked) };
});
await neg(6, "sel-toggle-while-sharing", "Toggle a row while a share request is in flight", async () => {
  const h = await boot({ stubs: { dispatchShareAlliance: { mode: "manual" } } }); await openTab(h, "dispatch", [dispatchRow("1001"), dispatchRow("1002")]);
  await h.toggleRow(0); h.findButton("map.shareAlliance").props.onClick(); await h.settle();
  const boxesDuring = checkboxView(h).map((b) => b.disabled);
  await h.toggleRow(1);
  await h.resolveCall(last(h, "dispatchShareAlliance"), { shared: 1, failed: 0, sharedUuids: ["1001"] });
  return { checkboxesDisabledDuringShare: boxesDuring, keysAfter: h.getState("selectedDispatchKeys") };
});
await neg(6, "sel-share-reply-without-shared-uuids", "Share reply lacking sharedUuids", async () => {
  const h = await boot({ stubs: { dispatchShareAlliance: { mode: "auto", value: () => ({ shared: 1, failed: 0 }) } } }); await openTab(h, "dispatch", [dispatchRow("1001")]); await h.toggleRow(0);
  h.findButton("map.shareAlliance").props.onClick(); await h.settle();
  return { keysAfter: h.getState("selectedDispatchKeys"), message: h.getState("message"), sharing: h.getState("sharing") };
});

// ====================================================== topic 7: delay
const DELAYS = ["", " ", "0", "-1", "-0", "1.5", "1e2", "0x10", "0b11", "9007199254740991", "9007199254740992", "9007199254740993", "1e21", "Infinity", "NaN", "abc", "1,5", "٣"];
await neg(7, "delay-strings", "Random delay strings: button state, parsed value passed to the schedule call (one fresh page per string)", async () => {
  const out = {};
  for (const s of DELAYS) {
    const h = await boot(); await openTab(h, "dispatch", [dispatchRow("1001")]); await h.toggleRow(0); await h.setRandomDelay(s);
    const b = h.findButton("map.scheduleSelected", { count: 1 });
    const disabled = b.props.disabled;
    b.props.onClick(); await h.settle();
    const c = h.callsNamed("dispatchPlunderSchedule")[0];
    const v = c?.args[1];
    out[JSON.stringify(s)] = { disabled, passed: c ? (Object.is(v, -0) ? "-0" : v) : "(not called)" };
  }
  return out;
});
await neg(7, "delay-changed-while-in-flight", "Delay edited after Schedule was clicked", async () => {
  const h = await boot({ stubs: { dispatchPlunderSchedule: { mode: "manual" } } }); await openTab(h, "dispatch", [dispatchRow("1001")]); await h.toggleRow(0); await h.setRandomDelay("4");
  h.findButton("map.scheduleSelected", { count: 1 }).props.onClick(); await h.settle(); await h.setRandomDelay("99");
  return { argSent: last(h, "dispatchPlunderSchedule").args[1], inputNow: h.getState("randomDelay") };
});
await neg(7, "delay-nonempty-selection-empty", "Valid delay but no selection", async () => {
  const h = await boot(); await openTab(h, "dispatch", [dispatchRow("1001")]); await h.setRandomDelay("3");
  const b = h.findButton("map.scheduleSelected", { count: 0 }); b.props.onClick(); await h.settle();
  return { disabled: b.props.disabled, calls: h.callsNamed("dispatchPlunderSchedule").length };
});

// ====================================================== topic 8: action buttons
await neg(8, "btn-double-invocation", "onClick invoked twice from the same render (no re-entry guard besides the disabled attribute)", async () => {
  const out = {};
  {
    const h = await boot({ stubs: { dispatchPlunderSchedule: { mode: "manual" } } }); await openTab(h, "dispatch", [dispatchRow("1001")]); await h.toggleRow(0);
    const b = h.findButton("map.scheduleSelected", { count: 1 }); b.props.onClick(); b.props.onClick(); await h.settle();
    out.schedule = h.callsNamed("dispatchPlunderSchedule").length;
  }
  {
    const h = await boot({ stubs: { dispatchShareAlliance: { mode: "manual" } } }); await openTab(h, "dispatch", [dispatchRow("1001")]); await h.toggleRow(0);
    const b = h.findButton("map.shareAlliance"); b.props.onClick(); b.props.onClick(); await h.settle();
    out.share = h.callsNamed("dispatchShareAlliance").length;
  }
  {
    const h = await boot({ stubs: { truckPlunderSchedule: { mode: "manual" } } }); await openTab(h, "truck", [truckRow("t1")]); await h.toggleRow(0);
    const b = h.findButton("map.scheduleSelectedTrucks", { count: 1 }); b.props.onClick(); b.props.onClick(); await h.settle();
    out.truckSchedule = h.callsNamed("truckPlunderSchedule").length;
  }
  return out;
});
await neg(8, "btn-schedule-job-list-fails", "Schedule succeeds but the job list refresh fails", async () => {
  const h = await boot();
  await openTab(h, "dispatch", [dispatchRow("1001")]); await h.toggleRow(0);
  h.setStubBehavior("plunderJobsList", { mode: "reject", error: err("list gone") });
  const logsBefore = h.logs.length;
  h.findButton("map.scheduleSelected", { count: 1 }).props.onClick(); await h.settle();
  return { tab: h.getState("tab"), selection: h.getState("selectedDispatchKeys"), busyKey: h.getState("busyKey"), logsInOrder: h.logs.slice(logsBefore), jobListCalls: h.callsNamed("plunderJobsList").length };
});
await neg(8, "btn-cancel-and-clear-failures", "Cancel / clear / plunder-again failures", async () => {
  const jobs = { dispatchJobs: [{ serverId: SERVER, uuid: "1001", taskKind: "dispatch", scheduleStatus: "scheduled" }, { serverId: SERVER, uuid: "1002", taskKind: "dispatch", scheduleStatus: "succeeded" }], truckJobs: [{ serverId: SERVER, uuid: "t1", jobId: "j1", scheduledAt: NOW, scheduleStatus: "succeeded", maxLootCount: 3, robTimes: 1, arriveTs: NOW + 60000 }] };
  const h = await boot({ jobs, translate: "en", stubs: { dispatchPlunderCancel: { mode: "reject", error: err("cancel failed") }, dispatchPlunderClear: { mode: "reject", error: err("GAME_DISCONNECTED") }, truckPlunderSchedule: { mode: "reject", error: err("again failed") } } });
  await drain(h); await h.clickTab("scheduledPlunder");
  const groups = h.scheduledGroups();
  const btns = (g) => treeNodes(h.expand(g.element)).filter((n) => n.type === "button");
  const find = (g, text) => btns(g).find((b) => nodeText(b) === text);
  const j0 = h.callsNamed("plunderJobsList").length;
  find(groups[0], h.translate("common.cancel")).props.onClick(); await h.settle();
  const afterCancel = { busyKey: h.getState("busyKey"), message: h.getState("message"), logs: h.logs.slice(-1), jobListReloaded: h.callsNamed("plunderJobsList").length - j0 };
  find(h.scheduledGroups()[0], h.translate("map.clearPlunderHistory")).props.onClick(); await h.settle();
  const afterClear = { busyKey: h.getState("busyKey"), message: h.getState("message"), jobListReloaded: h.callsNamed("plunderJobsList").length - j0 };
  find(h.scheduledGroups()[2], h.translate("map.plunderAgain")).props.onClick(); await h.settle();
  const afterAgain = { busyKey: h.getState("busyKey"), message: h.getState("message"), logs: h.logs.slice(-1) };
  return { afterCancel, afterClear, afterAgain };
});
await neg(8, "btn-treasure-claim-failure-and-empty", "Treasure claim rejects; claim with queued=0; row claim", async () => {
  const viewer = { mode: "auto", value: () => ({ playerUid: "p1", allianceId: "a1", states: [], batch: null }) };
  const h = await boot({ translate: "en", stubs: { treasureClaimStatus: viewer, treasureStateRefreshAll: { mode: "auto", value: () => ({ playerUid: "p1", allianceId: "a1", states: [] }) }, treasureClaim: { mode: "reject", error: err("GAME_DISCONNECTED") } } });
  await openTab(h, "treasure", [{ uuid: "900", serverId: SERVER, suppliesType: 1, complete: true, playerClaimState: "unclaimed", worldClaimState: "charging" }]);
  h.findButton("map.claimTreasureBoxes").props.onClick(); await h.settle();
  const failed = { message: h.getState("treasureClaimMessage"), busy: h.getState("treasureClaimBusy") };
  h.setStubBehavior("treasureClaim", { mode: "auto", value: () => ({ eligible: 0, queued: 0, skipped: 3 }) });
  const polls = h.callsNamed("treasureClaimStatus").length;
  h.findButton("map.claimTreasureBoxes").props.onClick(); await h.settle(); await h.advance(5000);
  const empty = { message: h.getState("treasureClaimMessage"), pollCallsAfter5s: h.callsNamed("treasureClaimStatus").length - polls };
  const rowBtn = treeNodes(h.expandedTable()).filter((n) => n.type === "button" && n.props.className === "map-schedule-button")[0];
  rowBtn.props.onClick(); await h.settle();
  return { failed, queuedZero: empty, rowClaimArgs: last(h, "treasureClaim").args };
});
await neg(8, "btn-export-guards", "Export: canceled dialog, no data server, called from a non-city tab", async () => {
  const h = await boot({ stubs: { cityExport: { mode: "auto", value: () => ({ canceled: true }) } } }); await drain(h);
  h.findButton("map.exportExcel").props.onClick(); await h.settle();
  const canceled = { message: h.getState("message"), exporting: h.getState("exporting"), calls: h.callsNamed("cityExport").length };
  await h.emitServer(0);
  const atServer0 = h.findButton("map.exportExcel"); const disabledAtServer0 = atServer0.props.disabled;
  atServer0.props.onClick(); await h.settle(); // fresh handler of a render with no data server
  const noServerCalls = h.callsNamed("cityExport").length;
  await h.emitServer(SERVER); await drain(h);
  const staleHandler = h.findButton("map.exportExcel").props.onClick; // closure of a render on the city tab
  await h.clickTab("monster");
  staleHandler(); await h.settle();
  return { canceled, disabledAtServer0, callsAfterFreshHandlerAtServer0: noServerCalls, callsAfterStaleCityClosureInvokedOnMonsterTab: h.callsNamed("cityExport").length, exportButtonRenderedOnMonsterTab: !!h.findButton("map.exportExcel") };
});
await neg(8, "btn-jump-handlers", "Coordinate jump / march follow guards", async () => {
  const h = await boot(); await openTab(h, "dispatch", [dispatchRow("1001")]);
  const cj = h.tableNode().props.onCoordinateJump;
  await cj({ ...dispatchRow("1001"), serverId: 999 }); await h.settle();
  const stale = { calls: h.callsNamed("coordinateJump").length, log: h.logs.at(-1) };
  await cj({ ...dispatchRow("1001"), x: 0, y: 5 }); await h.settle();
  const invalid = { calls: h.callsNamed("coordinateJump").length };
  await cj(dispatchRow("1001")); await h.settle();
  const ok = { calls: h.callsNamed("coordinateJump").map((c) => c.args[0]), log: h.logs.at(-1) };
  await openTab(h, "truck", [truckRow("t1", { marchUuid: "m1" })]);
  await h.tableNode().props.onCoordinateJump(truckRow("t1", { marchUuid: "m1" })); await h.settle();
  return { staleServer: stale, invalidCoordinates: invalid, valid: ok, truckFollow: h.callsNamed("marchFollow").map((c) => c.args[0]) };
});

// ====================================================== output
fs.writeFileSync(path.join(originalDir, "negative-results.json"), `${JSON.stringify(cases, null, 2)}\n`);
const esc = (s) => String(s).replace(/\|/g, "\\|").replace(/\n/g, " ");
const topics = { 1: "Keyword and search issuing", 2: "Search button", 3: "Resource/Monster name select", 4: "Query projection", 5: "Tab / page / server / refresh / stale replies / backend loss", 6: "Selection", 7: "Random delay", 8: "Action buttons" };
const md = ["# Negative and edge cases of the ORIGINAL Map panel (observed by executing the original)", "",
  "Generated by `original/negative-cases.mjs` (re-run it; do not edit by hand). Every result column is data produced by the unmodified original component `R` (SHA-256 `ce74345518be72e417a510b982c591729e5683f69751e190805598c4c05d3089`) run through `original-runtime.mjs` with stubbed, recording dependencies, or by the AST-extracted `Gn`/`Kn` expressions. Nothing is an expectation. Notes: (1) the harness invokes `props.onChange(...)` directly, so browser-side input sanitisation (e.g. `type=number` rejecting `0x10`) and the inability to click a disabled control are NOT modelled; where a case calls a callback of a disabled control it says so. (2) Messages use the harness key translator unless a case says English. (3) All times are fake-clock values.", ""];
for (const [t, title] of Object.entries(topics)) {
  md.push(`## Topic ${t}: ${title}`, "", "| id | case | observed in the original |", "|---|---|---|");
  for (const c of cases.filter((x) => String(x.topic) === t)) md.push(`| \`${c.id}\` | ${esc(c.description)} | \`${esc(JSON.stringify(c.observed))}\` |`);
  md.push("");
}
fs.writeFileSync(path.join(originalDir, "negative-cases.md"), `${md.join("\n")}\n`);
console.log(`cases=${cases.length}; wrote negative-cases.md and negative-results.json`);
