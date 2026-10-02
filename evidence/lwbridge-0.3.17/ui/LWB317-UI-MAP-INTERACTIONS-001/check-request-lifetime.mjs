// Milestone A — request lifetime/disposal ownership.
//
// Executes the unmodified MapDataPage callbacks and effects for three sources:
//   baseline   pre-navigation component (immutable, NAVIGATION-001/baseline.MapDataPage.jsx)
//   delivery   305240e as reviewed in PM-025 (immutable snapshot in this directory)
//   current    production src/LWBridge.UI-0.3.17/src/MapDataPage.jsx
// Each scenario defers the search promise, disposes the originating effect in a different way
// and then lets the obsolete request settle. Passing means state is byte-identical before and
// after the obsolete reply. Synthetic local responses; no native execution or browser proof.
import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { createHarness, deferred, repo } from "./harness.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const navigation = path.resolve(here, "../LWB317-UI-MAP-NAVIGATION-001");
const sha256 = (text) => crypto.createHash("sha256").update(text.replace(/\r\n/g, "\n")).digest("hex");
const read = (file) => fs.readFileSync(file, "utf8");

const SOURCES = [
  { label: "baseline", file: path.join(navigation, "baseline.MapDataPage.jsx"), hasTabCache: false },
  { label: "delivery-305240e", file: path.join(here, "delivery-305240e.MapDataPage.jsx"), hasTabCache: true },
  { label: "current", file: path.join(repo, "src/LWBridge.UI-0.3.17/src/MapDataPage.jsx"), hasTabCache: true },
];

const snapshot = (h) => ({
  rows: structuredClone(h.getState("rows")),
  total: h.getState("total"),
  loading: h.getState("loading"),
  queryError: h.getState("queryError"),
  page: h.getState("page"),
});
const unchanged = (before, after) => JSON.stringify(before) === JSON.stringify(after);
const obsoleteRows = (name) => ({ rows: [{ recordKey: name }], total: 1 });

// A second provider whose search is independently deferred, tagged for identification.
function secondApi(h, tag) {
  const pending = [];
  return {
    ...h.api,
    profileId: `${h.api.profileId}-${tag}`,
    search: (kind, query) => {
      const request = deferred();
      const entry = { id: `${tag}-${pending.length + 1}`, tag, kind, query: structuredClone(query), ...request };
      pending.push(entry);
      return request.promise;
    },
    pending,
  };
}

const scenarios = [
  {
    name: "backend loss: pending success",
    run: async (h) => {
      await h.mount();
      const pending = h.currentRequest();
      await h.setProps({ backendAvailable: false });
      const before = snapshot(h);
      await h.resolveRequest(pending, obsoleteRows("obsolete-after-unavailable"));
      return { before, after: snapshot(h) };
    },
  },
  {
    name: "backend loss: pending rejection",
    run: async (h) => {
      await h.mount();
      const pending = h.currentRequest();
      await h.setProps({ backendAvailable: false });
      const before = snapshot(h);
      await h.rejectRequest(pending, Object.assign(new Error("obsolete-after-unavailable"), { code: "OBSOLETE" }));
      return { before, after: snapshot(h) };
    },
  },
  {
    name: "unmount: pending success",
    run: async (h) => {
      await h.mount();
      const pending = h.currentRequest();
      await h.unmount();
      const before = snapshot(h);
      pending.resolve(obsoleteRows("obsolete-after-unmount"));
      await new Promise((resolve) => setImmediate(resolve));
      return { before, after: snapshot(h) };
    },
  },
  {
    name: "unmount: pending rejection",
    run: async (h) => {
      await h.mount();
      const pending = h.currentRequest();
      await h.unmount();
      const before = snapshot(h);
      pending.reject(Object.assign(new Error("obsolete-after-unmount"), { code: "OBSOLETE" }));
      await new Promise((resolve) => setImmediate(resolve));
      return { before, after: snapshot(h) };
    },
  },
  {
    name: "provider replacement: old success after new request starts",
    run: async (h) => {
      await h.mount();
      const stale = h.currentRequest();
      const replacement = secondApi(h, "B");
      await h.setProps({ mapApi: replacement });
      const fresh = replacement.pending.at(-1);
      assert.ok(fresh, "replacement provider receives the new search");
      const before = snapshot(h);
      await h.resolveRequest(stale, obsoleteRows("obsolete-old-provider"));
      const afterStale = snapshot(h);
      await h.resolveRequest(fresh, { rows: [{ recordKey: "replacement-provider-row" }], total: 1 });
      const final = snapshot(h);
      return {
        before,
        after: afterStale,
        extra: final.rows[0]?.recordKey === "replacement-provider-row" && final.loading === false && final.total === 1,
      };
    },
  },
  {
    name: "provider replacement: old rejection after new request starts",
    run: async (h) => {
      await h.mount();
      const stale = h.currentRequest();
      const replacement = secondApi(h, "B");
      await h.setProps({ mapApi: replacement });
      const fresh = replacement.pending.at(-1);
      const before = snapshot(h);
      await h.rejectRequest(stale, Object.assign(new Error("obsolete-old-provider"), { code: "OBSOLETE" }));
      const afterStale = snapshot(h);
      await h.resolveRequest(fresh, { rows: [{ recordKey: "replacement-provider-row" }], total: 1 });
      return { before, after: afterStale, extra: h.getState("queryError") === "" && h.getState("loading") === false };
    },
  },
  {
    name: "re-entry: stale success while re-entered request is pending",
    run: async (h) => {
      await h.mount();
      const first = h.currentRequest();
      await h.setProps({ backendAvailable: false });
      await h.setProps({ backendAvailable: true });
      const second = h.currentRequest();
      assert.notEqual(second.id, first.id, "re-entry starts a new search");
      const before = snapshot(h);
      await h.resolveRequest(first, obsoleteRows("obsolete-first-entry"));
      const afterStale = snapshot(h);
      await h.resolveRequest(second, { rows: [{ recordKey: "re-entry-row" }], total: 1 });
      return { before, after: afterStale, extra: h.getState("rows")[0]?.recordKey === "re-entry-row" && h.getState("loading") === false };
    },
  },
  {
    name: "re-entry: stale rejection after re-entered request succeeded",
    run: async (h) => {
      await h.mount();
      const first = h.currentRequest();
      await h.setProps({ backendAvailable: false });
      await h.setProps({ backendAvailable: true });
      const second = h.currentRequest();
      await h.resolveRequest(second, { rows: [{ recordKey: "re-entry-row" }], total: 1 });
      const before = snapshot(h);
      await h.rejectRequest(first, Object.assign(new Error("obsolete-first-entry"), { code: "OBSOLETE" }));
      return { before, after: snapshot(h) };
    },
  },
  {
    name: "stale finally cannot clear the newer pending request's loading",
    run: async (h) => {
      await h.mount();
      const first = h.currentRequest();
      await h.setPage(2);
      const second = h.currentRequest();
      assert.equal(second.query.page, 2);
      const before = snapshot(h);
      await h.resolveRequest(first, obsoleteRows("obsolete-page-1"));
      const afterStale = snapshot(h);
      await h.resolveRequest(second, { rows: [{ recordKey: "page-2-row" }], total: 80 });
      return { before, after: afterStale, extra: h.getState("loading") === false && h.getState("rows")[0]?.recordKey === "page-2-row" };
    },
  },
  {
    name: "effect re-run (page change): stale rejection leaves newer pending untouched",
    run: async (h) => {
      await h.mount();
      const first = h.currentRequest();
      await h.setPage(2);
      const before = snapshot(h);
      await h.rejectRequest(first, Object.assign(new Error("obsolete-page-1"), { code: "OBSOLETE" }));
      return { before, after: snapshot(h) };
    },
  },
  {
    name: "server transition: pending success",
    run: async (h) => {
      await h.mount();
      const first = h.currentRequest();
      await h.emitServer(322);
      const before = snapshot(h);
      await h.resolveRequest(first, obsoleteRows("obsolete-server-321"));
      return { before, after: snapshot(h) };
    },
  },
  {
    name: "Scheduled Plunder entry: pending success",
    tabCacheOnly: true,
    run: async (h) => {
      await h.mount();
      const first = h.currentRequest();
      await h.clickTab("scheduledPlunder");
      const before = snapshot(h);
      await h.resolveRequest(first, obsoleteRows("obsolete-before-scheduled"));
      return { before, after: snapshot(h) };
    },
  },
  {
    name: "current request still applies after backend recovery (positive control)",
    positive: true,
    run: async (h) => {
      await h.mount();
      await h.setProps({ backendAvailable: false });
      await h.setProps({ backendAvailable: true });
      const second = h.currentRequest();
      await h.resolveRequest(second, { rows: [{ recordKey: "positive-control" }], total: 1 });
      return { before: null, after: snapshot(h), extra: h.getState("rows")[0]?.recordKey === "positive-control" && h.getState("loading") === false && h.getState("queryError") === "" };
    },
  },
];

const results = [];
for (const source of SOURCES) {
  const text = read(source.file);
  for (const scenario of scenarios) {
    if (scenario.tabCacheOnly && !source.hasTabCache) continue;
    const harness = await createHarness(text, source.label);
    const outcome = await scenario.run(harness);
    const ignored = scenario.positive ? null : unchanged(outcome.before, outcome.after);
    const passed = scenario.positive ? outcome.extra === true : ignored && (outcome.extra === undefined || outcome.extra === true);
    results.push({
      label: source.label,
      scenario: scenario.name,
      before: outcome.before,
      after: outcome.after,
      staleReplyIgnored: ignored,
      followUpCurrentRequestApplies: outcome.extra ?? null,
      passed,
    });
    await harness.unmount();
  }
}

const by = (label) => results.filter((entry) => entry.label === label);
const failing = (label) => by(label).filter((entry) => !entry.passed).map((entry) => entry.scenario);
const baselineFailing = failing("baseline");
const deliveryFailing = failing("delivery-305240e");
const currentFailing = failing("current");

// Distinguishing evidence: baseline passes everything; the reviewed delivery fails exactly the
// disposal scenarios (the two PM-025 cases plus unmount); current passes everything.
assert.deepEqual(baselineFailing, [], "pre-navigation baseline must satisfy every lifetime scenario");
for (const required of [
  "backend loss: pending success",
  "backend loss: pending rejection",
  "unmount: pending success",
  "unmount: pending rejection",
]) assert.ok(deliveryFailing.includes(required), `delivery 305240e must fail: ${required}`);
assert.deepEqual(currentFailing, [], "current production must satisfy every lifetime scenario");

const output = {
  status: "PASS",
  scope: "Actual MapDataPage callbacks/effects in the persistent hook replay adapter (harness.mjs); synthetic deferred local responses. Not browser or native evidence.",
  sources: Object.fromEntries(SOURCES.map((source) => [source.label, { path: path.relative(repo, source.file).replaceAll("\\", "/"), sha256LfNormalized: sha256(read(source.file)) }])),
  scenarioCount: scenarios.length,
  failures: { baseline: baselineFailing, "delivery-305240e": deliveryFailing, current: currentFailing },
  results,
};
if (process.argv.includes("--record")) fs.writeFileSync(path.join(here, "request-lifetime-results.json"), JSON.stringify(output, null, 2) + "\n");
console.log(`LWB317_REQUEST_LIFETIME_OK baselineFailures=0 deliveryFailures=${deliveryFailing.length} currentFailures=0 scenarios=${results.length}`);
