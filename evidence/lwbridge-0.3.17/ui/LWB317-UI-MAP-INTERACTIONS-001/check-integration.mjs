// Milestone D — combined behavior of the three milestones through the ACTUAL production callbacks/effects.
//
// Persistent hook adapter (harness.mjs) + deferred provider replies + controlled clock. Every expectation below is
// either (a) stated by the recovered original source (see original/contract.md and the differential scenarios in
// check-interactions.mjs) or (b) a pure consequence of combining two already-differential behaviors. Synthetic local
// responses; no native execution and no browser proof — browser observations live in browser-results.json.
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { bootCanonicalLike, SOURCES } from "./flavors.mjs";
import { GAME_TEXTS, RESOURCE_OPTIONS, dispatchRow, resolveAll, truckRow } from "./scenarios.mjs";
import { nodeText } from "./harness.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const results = [];
const check = async (id, fn) => {
  try {
    const observation = await fn();
    results.push({ id, status: "PASS", observation });
  } catch (error) {
    results.push({ id, status: "FAIL", message: error.message.split("\n")[0] });
    if (process.argv.includes("--strict")) throw error;
  }
};
const sourceArg = process.argv.find((arg) => arg.startsWith("--source="))?.slice(9) || "current";
assert.ok(sourceArg in SOURCES, `unknown source ${sourceArg}`);
const boot = (options) => bootCanonicalLike(SOURCES[sourceArg], sourceArg, options);
const scheduledProps = (run) => run.h.findNodes((node) => typeof node.type === "function" && node.type.name === "ScheduledPlunder")[0]?.props || null;
const tabText = (run, label) => run.d.tabTexts().find((text) => text.startsWith(label));
const message = (run) => run.d.messageText();
const call = (run, name) => run.h.calls.filter((entry) => entry.name === name);

const JOBS = {
  dispatchJobs: [
    { serverId: 321, uuid: "9001", taskKind: "dispatch", scheduleStatus: "scheduled", ownerName: "Fixture A" },
    { serverId: 321, uuid: "9002", taskKind: "dispatch", scheduleStatus: "failed", ownerName: "Fixture B" },
    { serverId: 321, uuid: "9003", taskKind: "ghost", scheduleStatus: "waiting_connection", ownerName: "Fixture C" },
  ],
  truckJobs: [{ serverId: 321, uuid: "501", scheduleStatus: "succeeded", jobId: "j1", scheduledAt: 1 }],
};

await check("cache restoration keeps the typed keyword and refreshes the restored page with it", async () => {
  const run = await boot({ tab: "truck" });
  await run.h.mount();
  await resolveAll(run, 130);
  await run.d.typeKeyword("k1");
  await run.d.clickSearch();
  await resolveAll(run, 130);
  await run.d.setPage(2);
  run.h.currentRequest().resolve({ rows: [{ recordKey: "t51", serverId: 321, uuid: "t51" }], total: 130 });
  await run.h.settle();
  await run.h.clickTab("railway");
  await resolveAll(run, 5);
  await run.h.clickTab("truck");
  const restored = { page: run.d.page(), rows: run.d.tableRows(), keyword: run.d.keywordValue() };
  const refresh = run.d.lastQuery();
  assert.equal(restored.page, 2);
  assert.deepEqual(restored.rows.map((row) => row.recordKey), ["t51"]);
  assert.equal(restored.keyword, "k1");
  assert.deepEqual({ kind: run.h.currentRequest().kind, keyword: refresh.keyword, page: refresh.page }, { kind: "truck", keyword: "k1", page: 2 });
  return { restored: { page: restored.page, rows: restored.rows.length, keyword: restored.keyword }, refresh };
});

await check("name selection survives tab cache restoration and refreshes with the name and restored page", async () => {
  const run = await boot({ tab: "resource", dataOptions: RESOURCE_OPTIONS, gameTexts: GAME_TEXTS });
  await run.h.mount();
  await resolveAll(run, 130);
  await run.d.selectName("100282");
  await resolveAll(run, 130);
  await run.d.setPage(2);
  run.h.currentRequest().resolve({ rows: [{ recordKey: "r51", serverId: 321, uuid: "r51" }], total: 130 });
  await run.h.settle();
  await run.h.clickTab("monster");
  await resolveAll(run, 3);
  await run.h.clickTab("resource");
  assert.equal(run.d.page(), 2);
  assert.equal(run.d.nameValue(), "100282");
  assert.deepEqual(run.d.tableRows().map((row) => row.recordKey), ["r51"]);
  const query = run.d.lastQuery();
  assert.deepEqual({ page: query.page, name: query.resourceNameKey, keyword: query.keyword }, { page: 2, name: "100282", keyword: undefined });
  return query;
});

await check("Dispatch selection resets on tab change while the cached page and rows are restored", async () => {
  const run = await boot({ tab: "dispatch" });
  await run.h.mount();
  await resolveAll(run, 130);
  await run.d.setPage(2);
  run.h.currentRequest().resolve({ rows: [{ recordKey: "d51", serverId: 321, uuid: "d51" }], total: 130 });
  await run.h.settle();
  await run.d.toggleRow("dispatch", dispatchRow(1001));
  assert.deepEqual(run.d.selectionKeys(), ["321:1001"]);
  await run.h.clickTab("railway");
  await resolveAll(run, 5);
  await run.h.clickTab("dispatch");
  assert.deepEqual(run.d.selectionKeys(), []);
  assert.equal(run.d.page(), 2);
  assert.deepEqual(run.d.tableRows().map((row) => row.recordKey), ["d51"]);
  return { selection: run.d.selectionKeys(), page: run.d.page() };
});

await check("selection is retained across row refresh (connection change) and search while rows are replaced", async () => {
  const run = await boot({ tab: "truck" });
  await run.h.mount();
  await resolveAll(run, 55, [{ recordKey: "t1", serverId: 321, uuid: "501" }]);
  await run.d.toggleRow("truck", truckRow(501));
  await run.h.setProps({ online: true });
  await resolveAll(run, 55, [{ recordKey: "t1-fresh", serverId: 321, uuid: "501" }]);
  await run.d.clickSearch();
  await resolveAll(run, 55, []);
  assert.deepEqual(run.d.selectionKeys(), ["321:501"]);
  return run.d.selectionKeys();
});

await check("rapid navigation with deferred replies settles on the final tab only", async () => {
  const run = await boot({ tab: "truck" });
  await run.h.mount();
  const t0 = run.h.requests[0];
  await run.h.clickTab("railway"); const r1 = run.h.currentRequest();
  await run.h.clickTab("truck"); const t1 = run.h.currentRequest();
  await run.h.clickTab("dispatch"); const d1 = run.h.currentRequest();
  await run.h.clickTab("scheduledPlunder");
  const requestsAtScheduled = run.h.requests.length;
  await run.h.clickTab("dispatch"); const d2 = run.h.currentRequest();
  for (const stale of [t0, r1, t1, d1]) {
    stale.resolve({ rows: [{ recordKey: `stale-${stale.id}`, serverId: 321, uuid: "x" }], total: 1 });
    await run.h.settle();
    assert.deepEqual(run.d.tableRows(), [], `stale request ${stale.id} must not write rows`);
  }
  d2.resolve({ rows: [{ recordKey: "final", serverId: 321, uuid: "final" }], total: 1 });
  await run.h.settle();
  assert.deepEqual(run.d.tableRows().map((row) => row.recordKey), ["final"]);
  assert.equal(run.h.getState("loading"), false);
  assert.equal(run.h.requests.length, requestsAtScheduled + 1);
  return { requests: run.h.requests.length };
});

await check("Normal/Scheduled transitions: counts, list loading on entry, change events and normal state untouched", async () => {
  const run = await boot({ tab: "truck", jobs: JOBS });
  await run.h.mount();
  await resolveAll(run, 55, [{ recordKey: "t1", serverId: 321, uuid: "501" }]);
  assert.match(tabText(run, "map.scheduledPlunder"), /4$/);
  const listsAtMount = call(run, "listPlunderJobs").length;
  await run.h.clickTab("scheduledPlunder");
  assert.ok(call(run, "listPlunderJobs").length > listsAtMount, "entering Scheduled Plunder reloads the list");
  const props = scheduledProps(run);
  assert.equal(props.dispatchJobs.length, 3);
  assert.equal(props.truckJobs.length, 1);
  assert.match(nodeText(run.h.findNodes((node) => node.type === "span" && node.props?.className === "map-result-count")[0]), /^4 items$/);
  run.h.jobs.value = { dispatchJobs: JOBS.dispatchJobs.slice(0, 1), truckJobs: [] };
  await run.h.ctx.jobsChanged();
  await run.h.settle();
  assert.match(tabText(run, "map.scheduledPlunder"), /1$/);
  await run.h.clickTab("truck");
  assert.deepEqual(run.d.tableRows().map((row) => row.recordKey), ["t1"], "cached normal rows restored while the list refreshes");
  return { count: tabText(run, "map.scheduledPlunder") };
});

await check("the page clock drives Scheduled Plunder once per second from the controlled clock", async () => {
  const run = await boot({ tab: "scheduledPlunder", jobs: JOBS });
  await run.h.mount();
  const start = scheduledProps(run).currentTime;
  await run.h.advance(999);
  assert.equal(scheduledProps(run).currentTime, start);
  await run.h.advance(1);
  assert.equal(scheduledProps(run).currentTime, start + 1000);
  await run.h.advance(5000);
  assert.equal(scheduledProps(run).currentTime, start + 6000);
  return { start, afterSixSeconds: scheduledProps(run).currentTime };
});

await check("fence: without a provider every job/schedule/share control stays disabled and calls nothing", async () => {
  const run = await boot({ tab: "dispatch", online: true });
  await run.h.mount();
  await resolveAll(run);
  await run.d.toggleRow("dispatch", dispatchRow(1001));
  assert.ok(run.d.scheduleButtonStates().every((state) => state.disabled));
  await run.h.clickTab("scheduledPlunder");
  assert.equal(scheduledProps(run).actionsEnabled, false);
  assert.equal(run.h.calls.filter((entry) => /schedule|cancel|clear|share/i.test(entry.name) && entry.name !== "listPlunderJobs").length, 0);
  return { fenced: true };
});

await check("schedule success: busy, selection cleared, list reloaded, navigation to Scheduled Plunder; failure keeps selection", async () => {
  const run = await boot({ tab: "dispatch", schedulingProvider: true, jobs: JOBS });
  await run.h.mount();
  await resolveAll(run);
  await run.d.toggleRow("dispatch", dispatchRow(1001));
  await run.d.toggleRow("dispatch", dispatchRow(1002));
  await run.d.setDelay("5");
  await run.d.clickButtonByText("map.scheduleSelected:2");
  const pending = call(run, "scheduleDispatchPlunder")[0];
  assert.equal(pending.args[1], 5);
  assert.deepEqual(pending.args[0].map((row) => [row.uuid, row.taskKind]), [["1001", "dispatch"], ["1002", "dispatch"]]);
  assert.equal(run.h.getState("busyKey"), "schedule");
  assert.ok(run.d.scheduleButtonStates()[0].disabled, "busy disables the schedule button; its label is unchanged");
  pending.resolve({});
  await run.h.settle();
  await new Promise((resolve) => setImmediate(resolve));
  await run.h.settle();
  assert.equal(run.h.getState("tab"), "scheduledPlunder");
  assert.equal(run.h.getState("busyKey"), "");
  assert.equal(scheduledProps(run).dispatchJobs.length, 3);
  await run.h.clickTab("dispatch");
  assert.deepEqual(run.d.selectionKeys(), []);

  const failing = await boot({ tab: "dispatch", schedulingProvider: true });
  await failing.h.mount();
  await resolveAll(failing);
  await failing.d.toggleRow("dispatch", dispatchRow(1001));
  await failing.d.clickButtonByText("map.scheduleSelected:1");
  call(failing, "scheduleDispatchPlunder")[0].reject(new Error("blocked"));
  await failing.h.settle();
  await new Promise((resolve) => setImmediate(resolve));
  await failing.h.settle();
  assert.equal(failing.h.getState("tab"), "dispatch");
  assert.deepEqual(failing.d.selectionKeys(), ["321:1001"]);
  assert.equal(failing.h.getState("busyKey"), "");
  return { navigated: true, failureKeepsSelection: true };
});

await check("truck schedule success clears only the Truck selection", async () => {
  const run = await boot({ tab: "truck", schedulingProvider: true });
  await run.h.mount();
  await resolveAll(run);
  await run.d.toggleRow("truck", truckRow(501));
  await run.d.clickButtonByText("map.scheduleSelectedTrucks:1");
  const pending = call(run, "scheduleTruckPlunder")[0];
  assert.deepEqual(pending.args[0].map((row) => row.uuid), ["501"]);
  assert.equal(run.h.getState("busyKey"), "schedule-truck");
  pending.resolve({});
  await run.h.settle();
  await new Promise((resolve) => setImmediate(resolve));
  await run.h.settle();
  assert.equal(run.h.getState("tab"), "scheduledPlunder");
  await run.h.clickTab("truck");
  assert.deepEqual(run.d.selectionKeys(), []);
  return { cleared: true };
});

await check("share: label flips while sharing, only shared uuids leave the selection, partial/success messages", async () => {
  const run = await boot({ tab: "dispatch", online: true, schedulingProvider: true });
  await run.h.mount();
  await resolveAll(run);
  await run.d.toggleRow("dispatch", dispatchRow(1001));
  await run.d.toggleRow("dispatch", dispatchRow(1002));
  await run.d.clickButtonByText("map.shareAlliance");
  const pending = call(run, "shareDispatchToAlliance")[0];
  assert.equal(run.d.scheduleButtonStates()[1].text, "map.sharingAlliance");
  assert.ok(run.d.scheduleButtonStates().every((state) => state.disabled), "sharing disables schedule and share");
  pending.resolve({ shared: 1, failed: 1, sharedUuids: ["1001"] });
  await run.h.settle();
  await new Promise((resolve) => setImmediate(resolve));
  await run.h.settle();
  assert.deepEqual(run.d.selectionKeys(), ["321:1002"]);
  assert.deepEqual(message(run), ["map.shareAlliancePartial"]);
  assert.equal(run.d.scheduleButtonStates()[1].text, "map.shareAlliance");
  return { selection: run.d.selectionKeys(), message: message(run) };
});

await check("Scheduled actions: busy keys, ghost cancel identity, reload, error message and fences", async () => {
  const run = await boot({ tab: "scheduledPlunder", schedulingProvider: true, jobs: JOBS });
  await run.h.mount();
  const props = scheduledProps(run);
  assert.equal(props.actionsEnabled, true);
  props.onCancelDispatch(JOBS.dispatchJobs[2]);
  await run.h.settle();
  const cancel = call(run, "cancelDispatchPlunder")[0];
  assert.deepEqual(cancel.args, [321, "ghost:9003"]);
  assert.equal(run.h.getState("busyKey"), "321:9003");
  cancel.resolve({});
  await run.h.settle();
  await new Promise((resolve) => setImmediate(resolve));
  await run.h.settle();
  assert.equal(run.h.getState("busyKey"), "");
  scheduledProps(run).onClear("truck");
  await run.h.settle();
  assert.equal(run.h.getState("busyKey"), "clear:truck");
  const clear = call(run, "clearTruckPlunderHistory")[0];
  clear.reject(Object.assign(new Error("Browser preview cannot execute native Map action"), { code: "PREVIEW_NATIVE_ACTION_BLOCKED" }));
  await run.h.settle();
  await new Promise((resolve) => setImmediate(resolve));
  await run.h.settle();
  assert.equal(run.h.getState("busyKey"), "");
  assert.deepEqual(message(run), ["PREVIEW_NATIVE_ACTION_BLOCKED: Browser preview cannot execute native Map action"]);
  return { message: message(run) };
});

const failed = results.filter((entry) => entry.status !== "PASS");
if (process.argv.includes("--record")) fs.writeFileSync(path.join(here, sourceArg === "current" ? "integration-results.json" : `integration-${sourceArg}-results.json`), JSON.stringify({ source: sourceArg, status: failed.length ? "FAIL" : "PASS", scope: "Actual production MapDataPage callbacks/effects in the hook replay adapter; synthetic deferred provider replies and controlled clock. Not browser/native evidence.", results }, null, 2) + "\n");
for (const entry of results) console.log(`${entry.status} ${entry.id}${entry.message ? ` :: ${entry.message}` : ""}`);
console.log(failed.length ? `LWB317_INTEGRATION_FAIL ${failed.length}/${results.length}` : `LWB317_INTEGRATION_OK ${results.length} scenarios`);
if (failed.length) process.exitCode = 1;
