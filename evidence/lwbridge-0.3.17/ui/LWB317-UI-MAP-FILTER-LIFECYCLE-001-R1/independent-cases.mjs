import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { createHarness, deferred, repo, tick } from "../LWB317-UI-MAP-FILTER-LIFECYCLE-001/harness.mjs";
import { createOriginalHarness, optionsReply } from "../LWB317-UI-MAP-INTERACTIONS-001/original/common.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const pagePath = path.join(repo, "src/LWBridge.UI-0.3.17/src/MapDataPage.jsx");
const source = fs.readFileSync(pagePath, "utf8");
const results = [];
const reply = (serverId, name) => optionsReply({ alliances: [{ name, count: 1 }], noAllianceCount: 1 })(serverId);
const lfHash = (text) => crypto.createHash("sha256").update(text.replace(/\r\n/g, "\n")).digest("hex");

async function currentHarness(label) {
  const optionCalls = [];
  const h = await createHarness(source, `r1-current-${label}`, {
    serverId: 321,
    props: { previewState: "map-city", online: false, currentServerId: 321 },
    apiExtensions: () => ({
      dataOptions: (sid) => {
        const d = deferred();
        const call = { sid, done: false, ...d };
        optionCalls.push(call);
        return call.promise;
      },
    }),
  });
  await h.mount();
  return { kind: "current", h, optionCalls };
}

async function originalHarness(label) {
  const h = await createOriginalHarness({
    label: `r1-original-${label}`,
    online: false,
    stubs: { dataOptions: { mode: "manual" } },
  });
  await h.mount();
  return { kind: "original", h };
}

async function pair(label) {
  return { original: await originalHarness(label), current: await currentHarness(label) };
}

function optionsCalls(run) {
  return run.kind === "current"
    ? run.optionCalls.map((call) => call.sid)
    : run.h.callsNamed("dataOptions").map((call) => call.args[0]);
}

function pendingOption(run, sid, first = false) {
  const calls = run.kind === "current"
    ? run.optionCalls.filter((call) => call.sid === sid && !call.done)
    : run.h.callsNamed("dataOptions").filter((call) => call.args[0] === sid && call.pending);
  const call = first ? calls[0] : calls.at(-1);
  assert.ok(call, `${run.kind}: pending options ${sid}`);
  return call;
}

async function resolveOption(run, call, value) {
  if (run.kind === "current") {
    call.done = true;
    call.resolve(value);
    await tick();
    await run.h.settle();
  } else {
    await run.h.resolveCall(call, value);
  }
}

async function resolveSearches(h, result = { rows: [], total: 0 }) {
  for (let round = 0; round < 20; round += 1) {
    const open = h.requests.filter((request) => !request.__r1Done);
    if (!open.length) return;
    for (const request of open) {
      request.__r1Done = true;
      request.resolve(typeof result === "function" ? result(request) : result);
    }
    await tick();
    await h.settle();
  }
  throw new Error(`${h.label}: search requests did not drain`);
}

function snapshot(run) {
  const h = run.h;
  const original = run.kind === "original";
  const cache = h.getRef(original ? "tabCache" : "tabViewCache").current;
  const alliances = original ? h.getState("alliances") : h.getState("options")?.alliances || [];
  return {
    serverId: h.getState(original ? "dataServerId" : "browseServerId"),
    scanServerId: original ? h.props.scanState.serverId : h.getState("scanState").serverId,
    optionServers: optionsCalls(run),
    alliances: alliances.map((item) => item.name),
    page: h.getState("page"),
    rows: structuredClone(h.getState("rows")),
    total: h.getState("total"),
    loading: h.getState("loading"),
    cacheKeys: [...cache.keys()],
  };
}

function equivalent(label, original, current) {
  assert.deepEqual(current, original, label);
  return { original, current };
}

async function primeCache(run) {
  await resolveSearches(run.h, { rows: [{ serverId: 321, uuid: `${run.kind}-city` }], total: 1 });
  await run.h.clickTab("resource");
  await resolveSearches(run.h, { rows: [{ serverId: 321, uuid: `${run.kind}-resource` }], total: 1 });
  const cache = run.h.getRef(run.kind === "original" ? "tabCache" : "tabViewCache").current;
  assert.ok(cache.size > 0, `${run.kind}: cache primed`);
}

// 1. A normal same-server reply applies its lists and does not move the data server.
{
  const runs = await pair("same-server");
  for (const run of Object.values(runs)) {
    await resolveOption(run, pendingOption(run, 321, true), reply(321, "SAME"));
    await resolveSearches(run.h, { rows: [{ serverId: 321, uuid: "same" }], total: 1 });
  }
  const compared = equivalent("same-server reply", snapshot(runs.original), snapshot(runs.current));
  assert.deepEqual(compared.current.optionServers, [321]);
  assert.deepEqual(compared.current.alliances, ["SAME"]);
  results.push({ case: "same-server options reply", pass: true, ...compared });
  await runs.original.h.unmount(); await runs.current.h.unmount();
}

// 2. A current mismatched reply briefly redirects R, then C.serverId synchronizes it back.
{
  const runs = await pair("redirect");
  for (const run of Object.values(runs)) {
    await resolveOption(run, pendingOption(run, 321, true), reply(322, "WRONG"));
  }
  assert.deepEqual(optionsCalls(runs.original), [321, 322, 321]);
  assert.deepEqual(optionsCalls(runs.current), [321, 322, 321]);
  assert.deepEqual(snapshot(runs.original).alliances, []);
  assert.deepEqual(snapshot(runs.current).alliances, []);
  for (const run of Object.values(runs)) {
    const obsolete322 = pendingOption(run, 322);
    await resolveOption(run, pendingOption(run, 321), reply(321, "CURRENT"));
    await resolveOption(run, obsolete322, reply(322, "OBSOLETE-322"));
    await resolveSearches(run.h, { rows: [{ serverId: 321, uuid: "redirect-current" }], total: 1 });
  }
  const compared = equivalent("redirected reply synchronization", snapshot(runs.original), snapshot(runs.current));
  assert.equal(compared.current.serverId, 321);
  assert.deepEqual(compared.current.alliances, ["CURRENT"]);
  results.push({ case: "redirected options reply resynchronizes to scan server", pass: true, ...compared });
  await runs.original.h.unmount(); await runs.current.h.unmount();
}

// 3. Once an actual server transition owns a newer options generation, an old redirect is inert.
{
  const runs = await pair("obsolete-redirect");
  const old = {
    original: pendingOption(runs.original, 321, true),
    current: pendingOption(runs.current, 321, true),
  };
  await runs.original.h.emitServer(322);
  await runs.current.h.emitServer(322);
  await resolveOption(runs.original, old.original, reply(333, "OBSOLETE-REDIRECT"));
  await resolveOption(runs.current, old.current, reply(333, "OBSOLETE-REDIRECT"));
  for (const run of Object.values(runs)) {
    await resolveOption(run, pendingOption(run, 322), reply(322, "FRESH-322"));
    await resolveSearches(run.h, { rows: [{ serverId: 322, uuid: "fresh-322" }], total: 1 });
  }
  const compared = equivalent("obsolete redirect", snapshot(runs.original), snapshot(runs.current));
  assert.deepEqual(compared.current.optionServers, [321, 322]);
  assert.equal(compared.current.serverId, 322);
  assert.deepEqual(compared.current.alliances, ["FRESH-322"]);
  results.push({ case: "obsolete redirect response is generation-fenced", pass: true, ...compared });
  await runs.original.h.unmount(); await runs.current.h.unmount();
}

// 4. A real parent scan-server transition clears the normal-tab cache/view and owns the new server.
{
  const runs = await pair("server-transition");
  for (const run of Object.values(runs)) {
    await resolveOption(run, pendingOption(run, 321, true), reply(321, "INITIAL"));
    await primeCache(run);
  }
  await runs.original.h.emitServer(322);
  await runs.current.h.emitServer(322);
  const pendingOriginal = snapshot(runs.original);
  const pendingCurrent = snapshot(runs.current);
  equivalent("server transition pending state", pendingOriginal, pendingCurrent);
  assert.deepEqual(pendingCurrent.cacheKeys, []);
  assert.equal(pendingCurrent.page, 1);
  assert.deepEqual(pendingCurrent.rows, []);
  assert.equal(pendingCurrent.total, 0);
  assert.equal(pendingCurrent.loading, true);
  for (const run of Object.values(runs)) {
    await resolveOption(run, pendingOption(run, 322), reply(322, "SERVER-322"));
    await resolveSearches(run.h, { rows: [{ serverId: 322, uuid: "server-322" }], total: 1 });
  }
  const settled = equivalent("server transition settled state", snapshot(runs.original), snapshot(runs.current));
  assert.equal(settled.current.serverId, 322);
  assert.equal(settled.current.loading, false);
  results.push({ case: "actual scan-server transition owns cache/page/loading", pass: true, pending: { original: pendingOriginal, current: pendingCurrent }, settled });
  await runs.original.h.unmount(); await runs.current.h.unmount();
}

// 5. Loss of a positive scan server returns R to zero, clears cached rows, and retires the old options reply.
{
  const runs = await pair("server-loss");
  const stale = {
    original: pendingOption(runs.original, 321, true),
    current: pendingOption(runs.current, 321, true),
  };
  for (const run of Object.values(runs)) await primeCache(run);
  await runs.original.h.emitServer(0);
  await runs.current.h.emitServer(0);
  await resolveOption(runs.original, stale.original, reply(321, "STALE-AFTER-LOSS"));
  await resolveOption(runs.current, stale.current, reply(321, "STALE-AFTER-LOSS"));
  const compared = equivalent("server loss", snapshot(runs.original), snapshot(runs.current));
  assert.equal(compared.current.serverId, 0);
  assert.equal(compared.current.scanServerId, 0);
  assert.deepEqual(compared.current.cacheKeys, []);
  assert.equal(compared.current.page, 1);
  assert.deepEqual(compared.current.rows, []);
  assert.equal(compared.current.total, 0);
  assert.equal(compared.current.loading, false);
  assert.deepEqual(compared.current.alliances, ["STALE-AFTER-LOSS"]);
  results.push({ case: "positive scan-server loss clears view and retires options", pass: true, ...compared });
  await runs.original.h.unmount(); await runs.current.h.unmount();
}

const record = {
  source: {
    originalSha256: "ce74345518be72e417a510b982c591729e5683f69751e190805598c4c05d3089",
    currentPath: "src/LWBridge.UI-0.3.17/src/MapDataPage.jsx",
    currentSha256: lfHash(source),
  },
  cases: results,
};
fs.mkdirSync(here, { recursive: true });
fs.writeFileSync(path.join(here, "independent-results.json"), JSON.stringify(record, null, 2) + "\n");
console.log(`LWB317_UI_MAP_FILTER_LIFECYCLE_R1 cases=${results.length} failures=0`);
