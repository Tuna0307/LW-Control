// Flavor-agnostic Map interaction scenarios (Milestone B).
//
// Each scenario receives `ctx.boot(options)` which returns a started harness for ONE implementation (original
// component, canonical page or pre-campaign baseline) wrapped by the shared driver, and returns plain JSON
// observations. The check script runs every scenario against every implementation and compares the
// observations; the scenarios themselves never branch on the implementation.
import { nodeText } from "./harness.mjs";

export const T = {
  translate(key, values = {}) {
    if (key === "common.itemCount") return `${values.count} items`;
    if (key === "map.pageInfo") return `${values.page}/${values.total}`;
    if (key === "map.scheduleSelected" || key === "map.scheduleSelectedTrucks") return `${key}:${values.count}`;
    return key;
  },
};

export const NOW = Date.UTC(2026, 9, 2, 12, 0, 0);
export const SECOND = 1000;
export const dispatchRow = (uuid, extra = {}) => ({
  serverId: 321, uuid: String(uuid), ownerName: `Owner ${uuid}`, quality: 3, level: 6,
  completionTime: NOW - 60_000, plunderAt: NOW - 30_000, taskExpireTime: NOW + 3_600_000, rewards: [], ...extra,
});
export const truckRow = (uuid, extra = {}) => ({
  serverId: 321, uuid: String(uuid), ownerName: `Truck ${uuid}`, quality: 3, maxLootCount: 3, robTimes: 0,
  protectTime: 0, arriveTs: NOW + 3_600_000, currentGoods: [], ...extra,
});

export const RESOURCE_OPTIONS = { names: { resource: [{ key: "100282", count: 3 }, { key: "100281", count: 2 }], monster: [{ key: "Fixture Monster A", count: 4 }, { key: "Fixture Monster B", count: 1 }] } };
export const GAME_TEXTS = { "100282": "Label Resource A", "Fixture Monster A": "Label Monster A" };

// `base` = requests already issued by mount. The original issues one more search than the canonical page right
// after mount (its options reply replaces the name-selection object, which is a search-effect dependency); that
// documented difference is excluded by counting requests relative to the post-mount base.
const initial = async (ctx, options = {}) => {
  const run = await ctx.boot(options);
  await run.h.mount();
  run.base = run.h.requests.length;
  return run;
};
const resolveAll = async (run, total = 1, rows = []) => {
  for (const request of run.h.requests) {
    if (!request.settled) { request.settled = true; request.resolve({ rows, total }); }
  }
  await run.h.settle();
};
const snap = (run) => ({
  keyword: run.d.keywordValue(),
  name: run.d.nameValue(),
  page: run.d.page(),
  requests: run.h.requests.length - (run.base ?? 0),
  lastQuery: run.d.lastQuery(),
});

export const scenarios = [
  // ---- keyword typing versus submitting -------------------------------------------------------------------------
  {
    id: "keyword.typing-does-not-search",
    run: async (ctx) => {
      const run = await initial(ctx, { tab: "city" });
      await resolveAll(run);
      const before = snap(run);
      await run.d.typeKeyword("abc");
      await run.d.typeKeyword("abcd");
      return { before, after: snap(run) };
    },
  },
  {
    id: "keyword.search-click-on-page-1-searches-once-with-typed-text",
    run: async (ctx) => {
      const run = await initial(ctx, { tab: "city" });
      await resolveAll(run);
      await run.d.typeKeyword("abc");
      await run.d.clickSearch();
      return snap(run);
    },
  },
  {
    id: "keyword.search-click-on-page-2-goes-to-page-1-and-searches-once",
    run: async (ctx) => {
      const run = await initial(ctx, { tab: "city" });
      await resolveAll(run, 130);
      await run.d.setPage(2);
      await resolveAll(run, 130);
      await run.d.typeKeyword("xyz");
      const before = snap(run);
      await run.d.clickSearch();
      return { before, after: snap(run) };
    },
  },
  {
    id: "keyword.typed-but-unsubmitted-text-is-used-by-a-filter-change",
    run: async (ctx) => {
      const run = await initial(ctx, { tab: "city" });
      await resolveAll(run);
      await run.d.typeKeyword("typed");
      await run.d.setMarkedOnly(true);
      return snap(run);
    },
  },
  {
    id: "keyword.tab-change-keeps-input-and-queries-with-it",
    run: async (ctx) => {
      const run = await initial(ctx, { tab: "city" });
      await resolveAll(run);
      await run.d.typeKeyword("kw");
      await run.h.clickTab("monster");
      return snap(run);
    },
  },
  {
    id: "keyword.page-change-queries-with-typed-text",
    run: async (ctx) => {
      const run = await initial(ctx, { tab: "city" });
      await resolveAll(run, 130);
      await run.d.typeKeyword("pg");
      await run.d.setPage(2);
      return snap(run);
    },
  },
  {
    id: "keyword.no-timer-or-debounce-after-time-passes",
    run: async (ctx) => {
      const run = await initial(ctx, { tab: "city" });
      await resolveAll(run);
      await run.d.typeKeyword("slow");
      const before = snap(run);
      await run.h.advance(60_000);
      return { before, after: snap(run) };
    },
  },
  // ---- search loading / disabled ---------------------------------------------------------------------------------
  {
    id: "search.button-is-not-disabled-by-loading",
    run: async (ctx) => {
      const run = await initial(ctx, { tab: "city" });
      return { disabledWhileInitialRequestPending: run.d.searchDisabled() };
    },
  },
  {
    id: "search.click-while-loading-issues-a-newer-request-and-retires-the-older",
    run: async (ctx) => {
      const run = await initial(ctx, { tab: "city" });
      const first = run.h.currentRequest();
      await run.d.typeKeyword("again");
      await run.d.clickSearch();
      const second = run.h.currentRequest();
      first.resolve({ rows: [{ recordKey: "obsolete", serverId: 321, uuid: "obsolete" }], total: 1 });
      await run.h.settle();
      const afterStale = { rows: run.d.tableRows(), requests: run.h.requests.length - run.base };
      second.resolve({ rows: [{ recordKey: "fresh", serverId: 321, uuid: "fresh" }], total: 1 });
      await run.h.settle();
      return { afterStale, afterFresh: { rows: run.d.tableRows(), requests: run.h.requests.length - run.base } };
    },
  },
  {
    id: "search.connection-change-refreshes",
    run: async (ctx) => {
      const run = await initial(ctx, { tab: "city" });
      await resolveAll(run);
      const before = snap(run);
      await run.h.setProps({ online: true });
      return { before, after: snap(run) };
    },
  },
  {
    id: "search.failure-clears-rows-and-total",
    run: async (ctx) => {
      const run = await initial(ctx, { tab: "city" });
      await resolveAll(run, 3, [{ recordKey: "r", serverId: 321, uuid: "r" }]);
      await run.d.clickSearch();
      run.h.currentRequest().reject(new Error("boom"));
      await run.h.settle();
      return { rows: run.d.tableRows(), count: run.h.requests.length - run.base };
    },
  },
  // ---- Resource / Monster name selection ------------------------------------------------------------------------
  {
    id: "name.select-clears-keyword-resets-page-and-searches-once",
    run: async (ctx) => {
      const run = await initial(ctx, { tab: "resource", dataOptions: RESOURCE_OPTIONS });
      await resolveAll(run, 130);
      await run.d.setPage(3);
      await resolveAll(run, 130);
      await run.d.typeKeyword("foo");
      const before = snap(run);
      await run.d.selectName("100282");
      return { before, after: snap(run) };
    },
  },
  {
    id: "name.typing-after-selecting-drops-the-name-and-searches-with-the-text",
    run: async (ctx) => {
      const run = await initial(ctx, { tab: "resource", dataOptions: RESOURCE_OPTIONS });
      await resolveAll(run, 130);
      await run.d.selectName("100282");
      await resolveAll(run, 130);
      const before = snap(run);
      await run.d.typeKeyword("x");
      return { before, after: snap(run) };
    },
  },
  {
    id: "name.typing-without-a-selected-name-does-not-search",
    run: async (ctx) => {
      const run = await initial(ctx, { tab: "monster", dataOptions: RESOURCE_OPTIONS });
      await resolveAll(run);
      const before = snap(run);
      await run.d.typeKeyword("m");
      return { before, after: snap(run) };
    },
  },
  {
    id: "name.resource-and-monster-selections-are-independent",
    run: async (ctx) => {
      const run = await initial(ctx, { tab: "resource", dataOptions: RESOURCE_OPTIONS });
      await resolveAll(run);
      await run.d.selectName("100282");
      await resolveAll(run);
      await run.h.clickTab("monster");
      await resolveAll(run);
      const monsterBefore = run.d.nameValue();
      await run.d.selectName("Fixture Monster A");
      await resolveAll(run);
      await run.h.clickTab("resource");
      const resourceBack = snap(run);
      await resolveAll(run);
      await run.h.clickTab("monster");
      return { monsterBefore, resourceBack, monsterBack: snap(run) };
    },
  },
  {
    id: "name.option-text-uses-game-text-then-key",
    run: async (ctx) => {
      const run = await initial(ctx, { tab: "resource", dataOptions: RESOURCE_OPTIONS, gameTexts: GAME_TEXTS });
      await resolveAll(run);
      return run.d.nameOptions();
    },
  },
  // ---- selection ownership --------------------------------------------------------------------------------------
  {
    id: "selection.dispatch-toggle-membership-and-count",
    run: async (ctx) => {
      const run = await initial(ctx, { tab: "dispatch", schedulingProvider: true });
      await resolveAll(run);
      const rowA = dispatchRow(1001);
      const rowB = dispatchRow(1002);
      const steps = [];
      await run.d.toggleRow("dispatch", rowA); steps.push([run.d.selectionKeys(), run.d.scheduleButtonStates()]);
      await run.d.toggleRow("dispatch", rowB); steps.push([run.d.selectionKeys(), run.d.scheduleButtonStates()]);
      await run.d.toggleRow("dispatch", rowA); steps.push([run.d.selectionKeys(), run.d.scheduleButtonStates()]);
      return steps;
    },
  },
  {
    id: "selection.dispatch-survives-page-filter-sort-search-and-server-change",
    run: async (ctx) => {
      const run = await initial(ctx, { tab: "dispatch" });
      await resolveAll(run, 130);
      await run.d.toggleRow("dispatch", dispatchRow(1001));
      await run.d.setPage(2); await resolveAll(run, 130);
      const afterPage = run.d.selectionKeys();
      await run.d.typeKeyword("q"); await run.d.clickSearch(); await resolveAll(run, 130);
      const afterSearch = run.d.selectionKeys();
      await run.h.emitServer(322); await resolveAll(run, 10);
      const afterServer = run.d.selectionKeys();
      return { afterPage, afterSearch, afterServer };
    },
  },
  {
    id: "selection.dispatch-resets-on-any-tab-change-including-dispatch-ghost",
    run: async (ctx) => {
      const run = await initial(ctx, { tab: "dispatch" });
      await resolveAll(run);
      await run.d.toggleRow("dispatch", dispatchRow(1001));
      const before = run.d.selectionKeys();
      await run.h.clickTab("ghost"); await resolveAll(run);
      const ghost = run.d.selectionKeys();
      await run.d.toggleRow("ghost", dispatchRow(2001));
      const ghostSelected = run.d.selectionKeys();
      await run.h.clickTab("dispatch"); await resolveAll(run);
      return { before, ghost, ghostSelected, back: run.d.selectionKeys() };
    },
  },
  {
    id: "selection.truck-is-independent-and-persists-across-tabs-pages-and-servers",
    run: async (ctx) => {
      const run = await initial(ctx, { tab: "truck" });
      await resolveAll(run, 130);
      await run.d.toggleRow("truck", truckRow(501));
      const first = run.d.selectionKeys();
      await run.d.setPage(2); await resolveAll(run, 130);
      const afterPage = run.d.selectionKeys();
      await run.h.clickTab("railway"); await resolveAll(run);
      await run.h.clickTab("dispatch"); await resolveAll(run);
      await run.d.toggleRow("dispatch", dispatchRow(1001));
      await run.h.clickTab("truck"); await resolveAll(run);
      const afterTabs = run.d.selectionKeys();
      await run.h.emitServer(322); await resolveAll(run, 10);
      return { first, afterPage, afterTabs, afterServer: run.d.selectionKeys() };
    },
  },
  {
    id: "selection.payload-keys-and-identifier-edge-cases",
    run: async (ctx) => {
      const run = await initial(ctx, { tab: "dispatch" });
      await resolveAll(run);
      const out = [];
      await run.d.toggleRow("dispatch", dispatchRow(" 1001 "));          // stored with the untrimmed uuid
      out.push(run.d.selectionKeys());
      await run.d.toggleRow("dispatch", dispatchRow(" 1001 "));          // same stored key toggles it off
      out.push(run.d.selectionKeys());
      await run.d.toggleRow("dispatch", dispatchRow(undefined, { uuid: undefined }));
      out.push(run.d.selectionKeys());
      await run.d.toggleRow("dispatch", dispatchRow("", { ownerName: "dup" }));
      out.push(run.d.selectionKeys());                                   // duplicate empty uuid: same key
      await run.d.toggleRow("dispatch", { ...dispatchRow(7), serverId: 999 });
      out.push(run.d.selectionKeys());                                   // same uuid on another server: second entry
      return out;
    },
  },
  // ---- random delay and action predicates -----------------------------------------------------------------------
  {
    id: "delay.input-default-and-controlled-value",
    run: async (ctx) => {
      const run = await initial(ctx, { tab: "dispatch" });
      await resolveAll(run);
      const out = [run.d.delayState()];
      for (const text of ["15", "", "  ", "1e2", "-3"]) { await run.d.setDelay(text); out.push(run.d.delayState().value); }
      await run.h.clickTab("ghost");
      out.push(run.d.delayState().value);                                // shared by Dispatch and Ghost, not reset by tabs
      return out;
    },
  },
  {
    id: "delay.schedule-button-predicate-per-delay-text",
    run: async (ctx) => {
      const run = await initial(ctx, { tab: "dispatch", schedulingProvider: true });
      await resolveAll(run);
      await run.d.toggleRow("dispatch", dispatchRow(1001));
      const out = {};
      for (const text of ["0", "", " ", "5", "-1", "1.5", "1e2", "0x10", "9007199254740991", "9007199254740993", "abc", "Infinity"]) {
        await run.d.setDelay(text);
        out[JSON.stringify(text)] = run.d.scheduleButtonStates()[0].disabled;
      }
      return out;
    },
  },
  {
    id: "actions.schedule-and-share-predicates-with-and-without-selection-online-busy",
    run: async (ctx) => {
      const out = [];
      for (const online of [false, true]) {
        const run = await initial(ctx, { tab: "dispatch", online, schedulingProvider: true });
        await resolveAll(run);
        out.push({ online, empty: run.d.scheduleButtonStates() });
        await run.d.toggleRow("dispatch", dispatchRow(1001));
        out.push({ online, selected: run.d.scheduleButtonStates() });
      }
      return out;
    },
  },
  {
    id: "actions.truck-schedule-predicate",
    run: async (ctx) => {
      const run = await initial(ctx, { tab: "truck", schedulingProvider: true });
      await resolveAll(run);
      const empty = run.d.scheduleButtonStates();
      await run.d.toggleRow("truck", truckRow(501));
      return { empty, selected: run.d.scheduleButtonStates() };
    },
  },
];

// ---- action flows: provider calls, busy/label/message ownership, selection retention, navigation -------------------
export const JOBS = {
  dispatchJobs: [
    { serverId: 321, uuid: "9001", taskKind: "dispatch", scheduleStatus: "scheduled", ownerName: "Fixture A", quality: 3, completionTime: NOW - 1000, plunderAt: NOW - 500, rewards: [] },
    { serverId: 321, uuid: "9002", taskKind: "dispatch", scheduleStatus: "failed", ownerName: "Fixture B", quality: 2, lastError: "E000000", completionTime: NOW - 1000, plunderAt: NOW - 500, rewards: [] },
    { serverId: 321, uuid: "9003", taskKind: "ghost", scheduleStatus: "waiting_connection", ownerName: "Fixture C", quality: 4, completionTime: NOW - 1000, plunderAt: NOW - 500, rewards: [] },
  ],
  truckJobs: [{ serverId: 321, uuid: "501", scheduleStatus: "succeeded", jobId: "j1", scheduledAt: 1, ownerName: "Truck 501", quality: 3, maxLootCount: 3, robTimes: 1, protectTime: 0, arriveTs: NOW + 3_600_000 }],
};
const rowsOf = (call) => call.args[0].map((row) => [row.uuid, row.taskKind ?? null]);
const view = (run) => {
  const scheduled = run.d.scheduled();
  return {
    // normal-tab counts come from each harness summary stub and are not part of this comparison; the Scheduled count is
    active: run.d.activeTabText().map((text) => text.startsWith("map.scheduledPlunder") ? text : text.replace(/\d+$/, "")), selection: run.d.selectionKeys(), message: run.d.messageText(), buttons: run.d.scheduleButtonStates(),
    scheduledTab: run.d.tabTexts().at(-1), busyKey: scheduled?.busyKey ?? null,
    jobs: scheduled ? [scheduled.dispatchJobs.length, scheduled.ghostJobs.length, scheduled.truckJobs.length] : null,
  };
};
const flows = [
  {
    id: "flow.schedule-dispatch-success-busy-then-clear-reload-and-navigate",
    run: async (ctx) => {
      const run = await initial(ctx, { tab: "dispatch", schedulingProvider: true, jobs: JOBS });
      await resolveAll(run);
      await run.d.toggleRow("dispatch", dispatchRow(1001)); await run.d.toggleRow("dispatch", dispatchRow(1002));
      await run.d.setDelay("5");
      await run.d.clickButtonByText("map.scheduleSelected:2");
      const call = run.d.callsOf("scheduleDispatch")[0];
      const pending = { args: [rowsOf(call), call.args[1]], view: view(run) };
      await run.d.resolveCall(call, undefined);
      const done = view(run);
      await run.h.clickTab("dispatch"); await resolveAll(run);
      return { pending, done, selectionAfterReturn: run.d.selectionKeys() };
    },
  },
  {
    id: "flow.schedule-dispatch-failure-keeps-selection-and-tab",
    run: async (ctx) => {
      const run = await initial(ctx, { tab: "dispatch", schedulingProvider: true });
      await resolveAll(run);
      await run.d.toggleRow("dispatch", dispatchRow(1001));
      await run.d.clickButtonByText("map.scheduleSelected:1");
      await run.d.rejectCall(run.d.callsOf("scheduleDispatch")[0], new Error("boom"));
      return view(run);
    },
  },
  {
    id: "flow.schedule-with-invalid-delay-never-calls-the-provider",
    run: async (ctx) => {
      const run = await initial(ctx, { tab: "dispatch", schedulingProvider: true });
      await resolveAll(run);
      await run.d.toggleRow("dispatch", dispatchRow(1001));
      await run.d.setDelay("-1");
      return { states: run.d.scheduleButtonStates(), calls: run.d.callsOf("scheduleDispatch").length };
    },
  },
  {
    id: "flow.schedule-truck-success-clears-only-truck-selection",
    run: async (ctx) => {
      const run = await initial(ctx, { tab: "truck", schedulingProvider: true, jobs: JOBS });
      await resolveAll(run);
      await run.d.toggleRow("truck", truckRow(501));
      await run.d.clickButtonByText("map.scheduleSelectedTrucks:1");
      const call = run.d.callsOf("scheduleTruck")[0];
      const pending = { rows: rowsOf(call), view: view(run) };
      await run.d.resolveCall(call, undefined);
      const done = view(run);
      await run.h.clickTab("truck"); await resolveAll(run);
      return { pending, done, selectionAfterReturn: run.d.selectionKeys() };
    },
  },
  {
    id: "flow.share-label-selection-pruning-and-partial-message",
    run: async (ctx) => {
      const run = await initial(ctx, { tab: "dispatch", online: true, schedulingProvider: true });
      await resolveAll(run);
      await run.d.toggleRow("dispatch", dispatchRow(1001)); await run.d.toggleRow("dispatch", dispatchRow(1002));
      await run.d.clickButtonByText("map.shareAlliance");
      const call = run.d.callsOf("share")[0];
      const pending = { rows: rowsOf(call), buttons: run.d.scheduleButtonStates() };
      await run.d.resolveCall(call, { shared: 1, failed: 1, sharedUuids: ["1001"] });
      return { pending, done: view(run) };
    },
  },
  {
    id: "flow.share-full-success-message-and-selection",
    run: async (ctx) => {
      const run = await initial(ctx, { tab: "dispatch", online: true, schedulingProvider: true });
      await resolveAll(run);
      await run.d.toggleRow("dispatch", dispatchRow(1001));
      await run.d.clickButtonByText("map.shareAlliance");
      await run.d.resolveCall(run.d.callsOf("share")[0], { shared: 1, failed: 0, sharedUuids: ["1001"] });
      return view(run);
    },
  },
  {
    id: "flow.scheduled-tab-lists-counts-and-reloads-on-entry-and-change-events",
    run: async (ctx) => {
      const run = await initial(ctx, { tab: "truck", schedulingProvider: true, jobs: JOBS });
      await resolveAll(run);
      const afterMount = { tab: run.d.tabTexts().at(-1) };
      await run.h.clickTab("scheduledPlunder");
      const entered = view(run);
      run.d.setJobs({ dispatchJobs: JOBS.dispatchJobs.slice(0, 1), truckJobs: [] });
      await run.d.emitJobsChanged();
      const changed = view(run);
      return { afterMount, entered, changed, resultCount: run.h.findNodes((node) => node.type === "span" && node.props?.className === "map-result-count").map((node) => nodeText(node)) };
    },
  },
  {
    id: "flow.scheduled-cancel-identities-busy-keys-and-reload",
    run: async (ctx) => {
      const run = await initial(ctx, { tab: "scheduledPlunder", schedulingProvider: true, jobs: JOBS });
      const out = {};
      run.d.scheduled().cancelDispatch(JOBS.dispatchJobs[2]); await run.d.settleFlow();
      const ghost = run.d.callsOf("cancelDispatch").at(-1);
      out.ghost = { args: ghost.args, busy: view(run).busyKey };
      await run.d.resolveCall(ghost, undefined); out.afterGhost = view(run).busyKey;
      run.d.scheduled().cancelDispatch(JOBS.dispatchJobs[0]); await run.d.settleFlow();
      const plain = run.d.callsOf("cancelDispatch").at(-1);
      out.dispatch = { args: plain.args, busy: view(run).busyKey };
      await run.d.resolveCall(plain, undefined);
      run.d.scheduled().cancelTruck(JOBS.truckJobs[0]); await run.d.settleFlow();
      const truck = run.d.callsOf("cancelTruck").at(-1);
      out.truck = { args: truck.args, busy: view(run).busyKey };
      await run.d.rejectCall(truck, new Error("cancel failed"));
      out.afterTruckFailure = view(run).busyKey;
      return out;
    },
  },
  {
    id: "flow.scheduled-plunder-again-and-clear-history",
    run: async (ctx) => {
      const run = await initial(ctx, { tab: "scheduledPlunder", schedulingProvider: true, online: true, jobs: JOBS });
      const out = {};
      run.d.scheduled().again(JOBS.truckJobs[0]); await run.d.settleFlow();
      const again = run.d.callsOf("scheduleTruck").at(-1);
      out.again = { rows: rowsOf(again), busy: view(run).busyKey, stillScheduled: view(run).active };
      await run.d.resolveCall(again, undefined);
      run.d.scheduled().clear("ghost"); await run.d.settleFlow();
      const clearDispatch = run.d.callsOf("clearDispatch").at(-1);
      out.clearGhost = { kind: clearDispatch.args[1], busy: view(run).busyKey, hasBefore: typeof clearDispatch.args[0] };
      await run.d.resolveCall(clearDispatch, undefined);
      run.d.scheduled().clear("truck"); await run.d.settleFlow();
      const clearTruck = run.d.callsOf("clearTruck").at(-1);
      out.clearTruck = { busy: view(run).busyKey, argc: clearTruck.args.length };
      await run.d.resolveCall(clearTruck, undefined);
      out.done = view(run).busyKey;
      return out;
    },
  },
  {
    id: "flow.action-message-is-cleared-by-a-tab-change",
    run: async (ctx) => {
      const run = await initial(ctx, { tab: "dispatch", online: true, schedulingProvider: true });
      await resolveAll(run);
      await run.d.toggleRow("dispatch", dispatchRow(1001));
      await run.d.clickButtonByText("map.shareAlliance");
      await run.d.resolveCall(run.d.callsOf("share")[0], { shared: 1, failed: 0, sharedUuids: ["1001"] });
      const shown = run.d.messageText();
      await run.h.clickTab("ghost");
      return { shown, afterTabChange: run.d.messageText() };
    },
  },
  {
    id: "flow.share-failure-message-uses-the-original-error-translator",
    run: async (ctx) => {
      const run = await initial(ctx, { tab: "dispatch", online: true, schedulingProvider: true });
      await resolveAll(run);
      await run.d.toggleRow("dispatch", dispatchRow(1001));
      await run.d.clickButtonByText("map.shareAlliance");
      await run.d.rejectCall(run.d.callsOf("share")[0], Object.assign(new Error("game disconnected"), { code: "UNMAPPED_QA_CODE" }));
      return view(run);
    },
  },
  {
    id: "flow.clear-failure-message-uses-the-original-error-translator",
    run: async (ctx) => {
      const run = await initial(ctx, { tab: "scheduledPlunder", schedulingProvider: true, jobs: JOBS });
      run.d.scheduled().clear("truck"); await run.d.settleFlow();
      await run.d.rejectCall(run.d.callsOf("clearTruck").at(-1), new Error("boom"));
      return { message: run.d.messageText(), busy: view(run).busyKey };
    },
  },
  {
    id: "navigation.server-change-on-page-3-first-request-uses-page-1",
    run: async (ctx) => {
      const run = await initial(ctx, { tab: "city" });
      await resolveAll(run, 230);
      await run.d.setPage(3);
      await resolveAll(run, 230);
      const before = run.h.requests.length;
      await run.h.emitServer(456);
      const issued = run.h.requests.slice(before).map((request) => ({ server: request.query.serverId, page: request.query.page }));
      return { first: issued[0], pagesIssued: [...new Set(issued.map((entry) => entry.page))], finalPage: run.d.page() };
    },
  },
];
scenarios.push(...flows);

export { snap, resolveAll, initial, nodeText };
