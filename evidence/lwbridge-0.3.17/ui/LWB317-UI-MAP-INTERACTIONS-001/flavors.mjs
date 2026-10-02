// Implementation factories for the shared scenarios. Each returns { h, d } where `h` is a started-able harness
// (mount/settle/requests/clickTab/setPage/emitServer/setProps/advance) and `d` the implementation-agnostic driver.
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { createHarness, deferred, repo } from "./harness.mjs";
import { createDriver } from "./driver.mjs";
import { T } from "./scenarios.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
export const SOURCES = {
  baseline: path.join(here, "baseline-3e8617c.MapDataPage.jsx"),
  current: path.join(repo, "src/LWBridge.UI-0.3.17/src/MapDataPage.jsx"),
};

// Provider stubs for scheduling/sharing: deferred, recorded, never native.
const callStub = (ctx, name) => (...args) => {
  const call = deferred();
  ctx.apiCalls.push({ name, args: structuredClone(args), ...call });
  return call.promise;
};
export function schedulingExtensions(jobs) {
  return (ctx) => ({
    listPlunderJobs: async () => { ctx.apiCalls.push({ name: "listPlunderJobs", args: [] }); return structuredClone(jobs.value); },
    listenPlunderJobsChanged: (callback) => { ctx.jobsChanged = callback; return () => { ctx.jobsChanged = null; }; },
    scheduleDispatchPlunder: callStub(ctx, "scheduleDispatchPlunder"),
    scheduleTruckPlunder: callStub(ctx, "scheduleTruckPlunder"),
    shareDispatchToAlliance: callStub(ctx, "shareDispatchToAlliance"),
    cancelDispatchPlunder: callStub(ctx, "cancelDispatchPlunder"),
    cancelTruckPlunder: callStub(ctx, "cancelTruckPlunder"),
    clearDispatchPlunderHistory: callStub(ctx, "clearDispatchPlunderHistory"),
    clearTruckPlunderHistory: callStub(ctx, "clearTruckPlunderHistory"),
  });
}
export function listingExtensions(jobs) {
  return (ctx) => ({
    listPlunderJobs: async () => { ctx.apiCalls.push({ name: "listPlunderJobs", args: [] }); return structuredClone(jobs.value); },
    listenPlunderJobsChanged: (callback) => { ctx.jobsChanged = callback; return () => { ctx.jobsChanged = null; }; },
  });
}

export async function bootCanonicalLike(file, label, options = {}) {
  const jobs = { value: options.jobs || { dispatchJobs: [], truckJobs: [] } };
  const h = await createHarness(fs.readFileSync(file, "utf8"), label, {
    translate: T.translate,
    props: { previewState: options.tab === "scheduledPlunder" ? "map-scheduled" : `map-${options.tab || "city"}`, online: options.online ?? false, gameTexts: options.gameTexts ?? {}, ...(options.props || {}) },
    dataOptions: options.dataOptions,
    apiExtensions: options.schedulingProvider ? schedulingExtensions(jobs) : listingExtensions(jobs),
  });
  h.jobs = jobs;
  return { h, d: createDriver(h) };
}

// The ACTUAL original component (original/original-runtime.mjs executes the asset bytes).
export async function bootOriginal(options = {}) {
  const { createOriginalHarness } = await import("./original/original-runtime.mjs");
  const names = options.dataOptions?.names || { resource: [], monster: [] };
  const h = await createOriginalHarness({
    label: "original",
    tabMode: "controlled",
    initialTab: options.tab || "city",
    online: options.online ?? false,
    translate: T.translate,
    jobs: options.jobs,
    stubs: {
      dataOptions: { mode: "auto", value: (serverId) => ({ serverId, counts: {}, alliances: [], names, dispatchLevels: [], rewardItems: { truck: [], railway: [] }, treasureTypes: [], noAllianceCount: 0, scanProgress: null }) },
      localize: { mode: "auto", value: () => structuredClone(options.gameTexts || {}) },
      // Provider operations stay pending until the scenario resolves/rejects them (recorded only; nothing native).
      dispatchPlunderSchedule: { mode: "manual" }, truckPlunderSchedule: { mode: "manual" }, dispatchShareAlliance: { mode: "manual" },
      dispatchPlunderCancel: { mode: "manual" }, truckPlunderCancel: { mode: "manual" }, dispatchPlunderClear: { mode: "manual" }, truckPlunderClear: { mode: "manual" },
    },
  });
  return { h, d: createDriver(h) };
}
