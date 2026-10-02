// Executed differential: the ORIGINAL component (original-runtime.mjs) versus the CURRENT canonical page
// (src/LWBridge.UI-0.3.17/src/MapDataPage.jsx run through ../harness.mjs createHarness). Read-only on the canonical
// source. Only observables that exist in both are compared (requests, page/rows/loading state, predicates).
//
//   node .../original/diff-canonical.mjs [--record]    (writes original/canonical-differential.json with --record)
import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { createHarness, deferred, repo, treeNodes, nodeText } from "../harness.mjs";
import { boot, drain, since, dispatchRow, cityRow, optionsReply, NOW } from "./common.mjs";
import { originalDir } from "./lib.mjs";

const record = process.argv.includes("--record");
const canonicalFile = path.join(repo, "src/LWBridge.UI-0.3.17/src/MapDataPage.jsx");
const canonicalSource = fs.readFileSync(canonicalFile, "utf8");
const out = { canonicalSha256: crypto.createHash("sha256").update(canonicalSource.replaceAll("\r\n", "\n")).digest("hex"), canonicalFile: "src/LWBridge.UI-0.3.17/src/MapDataPage.jsx", note: "observations of each implementation (canonical source = working tree at run time, hash recorded); 'differs' compares the listed observables only", scenarios: {} };

const canon = async (options = {}) => { const h = await createHarness(canonicalSource, "canonical", { now: NOW, ...options, props: { previewState: "", ...(options.props || {}) } }); await h.mount(); return h; };
const cq = (r) => ({ kind: r.kind, keyword: r.query.keyword, page: r.query.page, serverId: r.query.serverId });
const oq = (r) => ({ kind: r.kind, keyword: r.query.keyword, page: r.query.page, serverId: r.query.serverId });
async function drainCanon(h, reply = { rows: [], total: 0 }) {
  for (let i = 0; i < 10; i += 1) {
    const open = h.requests.filter((r) => !r.done); if (!open.length) return;
    for (const r of open) { r.done = true; r.resolve(reply); }
    await h.settle();
  }
}
const same = (a, b) => JSON.stringify(a) === JSON.stringify(b);
async function compare(id, description, original, canonical) {
  const o = await original(); const c = await canonical();
  out.scenarios[id] = { description, original: o, canonical: c, differs: !same(o, c) };
  console.log(`${same(o, c) ? "same  " : "DIFFERS"} ${id}`);
}
const canonSearch = (h) => h.findNodes((n) => n.type === "button" && nodeText(n) === "common.search")[0];
const canonInput = (h) => h.findNodes((n) => n.type === "input" && n.props["aria-label"] === "map.searchLabel")[0];
const canonTable = (h) => h.findNodes((n) => typeof n.type === "function" && n.type.name === "MapTable")[0];

await compare("mount-requests", "Search requests after mount (before any reply is resolved)",
  async () => { const h = await boot(); return { requests: h.requests.map(oq) }; },
  async () => { const h = await canon(); return { requests: h.requests.map(cq) }; });

await compare("mount-requests-after-options", "Search requests after mount once all replies were resolved",
  async () => { const h = await boot(); await drain(h); return { requestCount: h.requests.length }; },
  async () => { const h = await canon(); await drainCanon(h); return { requestCount: h.requests.length }; });

await compare("typing-no-request", "Typing three characters",
  async () => { const h = await boot(); await drain(h); const n = h.requests.length; for (const v of ["a", "ab", "abc"]) await h.typeKeyword(v); return { newRequests: h.requests.length - n }; },
  async () => { const h = await canon(); await drainCanon(h); const n = h.requests.length; for (const v of ["a", "ab", "abc"]) { canonInput(h).props.onChange({ target: { value: v } }); await h.settle(); } return { newRequests: h.requests.length - n }; });

await compare("search-at-page-3", "Search click at page 3 with a typed keyword",
  async () => { const h = await boot(); await drain(h, { rows: [], total: 230 }); await h.setPage(3); await drain(h, { rows: [], total: 230 }); await h.typeKeyword("xyz"); const n = h.requests.length; await h.clickSearch(); return { requests: h.requests.slice(n).map(oq), page: h.getState("page") }; },
  async () => { const h = await canon(); await drainCanon(h, { rows: [], total: 230 }); await h.setPage(3); await drainCanon(h, { rows: [], total: 230 }); canonInput(h).props.onChange({ target: { value: "xyz" } }); await h.settle(); const n = h.requests.length; canonSearch(h).props.onClick(); await h.settle(); return { requests: h.requests.slice(n).map(cq), page: h.getState("page") }; });

await compare("search-with-no-data-server", "Server id 0 (no scan server): Search button state and click",
  async () => { const h = await boot(); await drain(h); await h.emitServer(0); const n = h.requests.length; const b = h.findButton("common.search"); const disabled = b.props.disabled; b.props.onClick(); await h.settle(); return { disabled: disabled ?? false, requestsAfterClick: h.requests.slice(n).map(oq) }; },
  async () => { const h = await canon(); await drainCanon(h); await h.emitServer(0); const n = h.requests.length; const b = canonSearch(h); const disabled = b.props.disabled; b.props.onClick(); await h.settle(); return { disabled: disabled ?? false, requestsAfterClick: h.requests.slice(n).map(cq) }; });

await compare("search-button-while-offline-backend-unavailable", "Search button when the backend/provider is unavailable (backendAvailable=false) and when offline",
  async () => { const h = await boot(); await drain(h); await h.setProps({ online: false }); const offline = h.findButton("common.search").props.disabled ?? false; return { offline }; },
  async () => { const h = await canon(); await drainCanon(h); await h.setProps({ online: false }); const offline = canonSearch(h).props.disabled ?? false; await h.setProps({ backendAvailable: false }); const unavailable = canonSearch(h).props.disabled ?? false; return { offline, backendUnavailableCanonicalOnly: unavailable }; });

await compare("scan-completion-refetch", "Rows refetch when a scan completes (isReading true -> false)",
  async () => { const h = await boot(); await drain(h); await h.emitScanState({ isReading: true }); await h.advance(1500); const duringScan = h.requests.filter((r) => !r.done).length; await drain(h); const n = h.requests.length; await h.emitScanState({ isReading: false }); return { requestsDuringScanAfter1500ms: duringScan, requestsAtCompletion: h.requests.length - n }; },
  async () => {
    const h = await canon(); await drainCanon(h);
    const listener = h.liveStatusListener();
    listener({ ...h.props.mapApi ? {} : {}, serverId: 321, isReading: true, phase: "scanning", readBlocks: 1, totalBlocks: 10, selectedTypes: ["city"], scanMode: "normal" }); await h.settle();
    await h.advance(1500); const duringScan = h.requests.filter((r) => !r.done).length; await drainCanon(h);
    const n = h.requests.length; h.liveStatusListener()({ serverId: 321, isReading: false, phase: "completed", readBlocks: 10, totalBlocks: 10, selectedTypes: ["city"], scanMode: "normal" }); await h.settle(); await h.settle();
    return { requestsDuringScanAfter1500ms: duringScan, requestsAtCompletion: h.requests.length - n };
  });

await compare("message-cleared-on-tab-change", "Share message after a tab change",
  async () => {
    const h = await boot({ stubs: { dispatchShareAlliance: { mode: "auto", value: () => ({ shared: 1, failed: 0, sharedUuids: ["1001"] }) } } });
    await h.clickTab("dispatch"); await drain(h, { rows: [dispatchRow("1001")], total: 1 }); await h.toggleRow(0);
    h.findButton("map.shareAlliance").props.onClick(); await h.settle(); const before = h.getState("message");
    await h.clickTab("ghost"); return { messageBefore: before, messageAfterTabChange: h.getState("message") };
  },
  async () => {
    const h = await canon(); h.api.shareDispatchToAlliance = async () => ({ shared: 1, failed: 0, sharedUuids: ["1001"] }); h.api.scheduleDispatchPlunder = async () => undefined;
    await h.setProps({ online: true }); await h.clickTab("dispatch"); await drainCanon(h, { rows: [dispatchRow("1001")], total: 1 });
    canonTable(h).props.onSelect(dispatchRow("1001")); await h.settle();
    const share = h.findNodes((n) => n.type === "button" && nodeText(n) === "map.shareAlliance")[0]; share.props.onClick(); await h.settle();
    const before = h.getState("actionMessage"); await h.clickTab("ghost");
    return { messageBefore: before, messageAfterTabChange: h.getState("actionMessage") };
  });

await compare("search-error-visible", "Search failure: visible error text and rows",
  async () => { const h = await boot(); await drain(h); await h.clickSearch(); await h.rejectRequest(h.requests.at(-1), new Error("backend gone")); return { rows: h.getState("rows").length, visibleAlerts: h.findNodes((n) => n.props?.role === "alert").length, log: h.logs.at(-1) }; },
  async () => { const h = await canon(); await drainCanon(h); canonSearch(h).props.onClick(); await h.settle(); h.requests.at(-1).reject(new Error("backend gone")); await h.settle(); return { rows: h.getState("rows").length, visibleAlerts: h.findNodes((n) => n.props?.role === "alert").length, log: "(no log surface)" }; });

await compare("name-first-keystroke", "Typing while a resource name is selected: requests caused by the first keystroke",
  async () => { const h = await boot(); await h.clickTab("resource"); await drain(h); await h.selectName("r2"); await drain(h); const n = h.requests.length; await h.typeKeyword("a"); const first = h.requests.length - n; await drain(h); await h.typeKeyword("ab"); return { afterFirst: first, afterSecond: h.requests.length - n - first }; },
  async () => {
    const h = await canon(); h.api.dataOptions = async (sid) => ({ serverId: sid, counts: {}, alliances: [], names: { resource: [{ key: "r2", count: 1 }], monster: [] }, dispatchLevels: [], rewardItems: { truck: [], railway: [] }, treasureTypes: [], noAllianceCount: 0 });
    await h.clickTab("resource"); await drainCanon(h);
    const select = h.findNodes((n) => n.type === "select" && n.props["aria-label"] === "common.name")[0]; select.props.onChange({ target: { value: "r2" } }); await h.settle(); await drainCanon(h);
    const n = h.requests.length; canonInput(h).props.onChange({ target: { value: "a" } }); await h.settle(); const first = h.requests.length - n; await drainCanon(h);
    canonInput(h).props.onChange({ target: { value: "ab" } }); await h.settle(); return { afterFirst: first, afterSecond: h.requests.length - n - first };
  });

await compare("truck-selection-survives-tab-change", "Truck selection after truck -> city -> truck",
  async () => { const h = await boot(); await h.clickTab("truck"); await drain(h, { rows: [{ uuid: "t1", serverId: 321, arriveTs: NOW + 99999, maxLootCount: 3 }], total: 1 }); await h.toggleRow(0); await h.clickTab("city"); await drain(h); await h.clickTab("truck"); return { keys: h.getState("selectedTruckKeys") }; },
  async () => { const h = await canon(); await h.clickTab("truck"); await drainCanon(h, { rows: [{ uuid: "t1", serverId: 321, arriveTs: NOW + 99999, maxLootCount: 3 }], total: 1 }); canonTable(h).props.onSelect({ uuid: "t1", serverId: 321 }); await h.settle(); await h.clickTab("city"); await drainCanon(h); await h.clickTab("truck"); return { keys: Object.keys(h.getState("truckSelection")) }; });

const different = Object.entries(out.scenarios).filter(([, s]) => s.differs).map(([id]) => id);
out.differs = different;
console.log(`\ndiffering scenarios: ${different.join(", ") || "(none)"}`);
if (record) { fs.writeFileSync(path.join(originalDir, "canonical-differential.json"), `${JSON.stringify(out, null, 2)}\n`); console.log("canonical-differential.json written"); }
void deferred; void cityRow; void optionsReply; void treeNodes; void assert;
