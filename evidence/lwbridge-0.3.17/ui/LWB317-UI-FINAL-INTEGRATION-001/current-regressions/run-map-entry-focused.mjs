import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { createAppHarness } from "./map-entry-app-harness.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const appPath = path.join(repo, "src/LWBridge.UI-0.3.17/src/App.jsx");
const source = fs.readFileSync(appPath, "utf8");
const sha256 = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
const cases = [];
const pass = (name, details = {}) => cases.push({ name, status: "PASS", ...details });

async function mounted(label, options = {}) {
  const h = await createAppHarness(source, label, options);
  await h.mount();
  return h;
}

{
  const h = await mounted("different-route entry");
  assert.equal(h.summaryRequests.length, 1, "profile bootstrap request");
  await h.resolveSummary(h.summaryRequests[0], 321);
  h.clearEvents();
  await h.clickRoute("map-data");
  assert.equal(h.summaryRequests.length, 2);
  assert.equal(h.getActiveRoute(), "map-data");
  assert.deepEqual(h.events.slice(0, 3), [
    { event: "click", routeKey: "map-data", activeRoute: "overview" },
    { event: "summary:start", id: 2, activeRoute: "overview", profileId: "profile-a" },
    { event: "transition:start", activeRoute: "overview" },
  ]);
  pass("different-route Map entry starts summary before transition and navigation is already active while request is pending", { events: structuredClone(h.events) });
  await h.unmount();
}

{
  const h = await mounted("active Map no-op");
  await h.resolveSummary(h.summaryRequests[0], 321);
  await h.clickRoute("map-data");
  const count = h.summaryRequests.length;
  h.clearEvents();
  await h.clickRoute("map-data");
  assert.equal(h.summaryRequests.length, count);
  assert.deepEqual(h.events, [{ event: "click", routeKey: "map-data", activeRoute: "map-data" }]);
  pass("already-active Map selection is inert", { requestCount: count, events: structuredClone(h.events) });
  await h.rejectSummary(h.summaryRequests.at(-1));
  await h.unmount();
}

{
  const h = await mounted("non-Map route");
  await h.resolveSummary(h.summaryRequests[0], 321);
  const count = h.summaryRequests.length;
  h.clearEvents();
  await h.clickRoute("automation");
  assert.equal(h.summaryRequests.length, count);
  assert.equal(h.getActiveRoute(), "automation");
  assert.deepEqual(h.events.map((entry) => entry.event), ["click", "transition:start"]);
  pass("non-Map navigation does not start summary", { events: structuredClone(h.events) });
  await h.unmount();
}

{
  const h = await mounted("deferred success");
  await h.resolveSummary(h.summaryRequests[0], 321);
  h.clearEvents();
  await h.clickRoute("map-data");
  const request = h.summaryRequests.at(-1);
  assert.equal(h.getActiveRoute(), "map-data");
  assert.equal(h.getState("mapSummary").serverId, 321);
  await h.resolveSummary(request, 322, 7);
  assert.equal(h.getState("mapSummary").serverId, 322);
  assert.equal(h.getState("mapSummary").counts.city, 7);
  pass("deferred Map-entry summary settles after navigation and updates current Map state", { request: { id: request.id, activeRoute: request.activeRoute }, serverId: h.getState("mapSummary").serverId });
  await h.unmount();
}

{
  const h = await mounted("rejected entry");
  await h.resolveSummary(h.summaryRequests[0], 321);
  h.clearEvents();
  await h.clickRoute("map-data");
  const request = h.summaryRequests.at(-1);
  assert.equal(h.getActiveRoute(), "map-data");
  await h.rejectSummary(request);
  assert.equal(h.getActiveRoute(), "map-data");
  assert.equal(h.getState("mapSummary").serverId, 321);
  pass("Map-entry summary rejection does not block or roll back navigation", { events: structuredClone(h.events) });
  await h.unmount();
}

{
  const h = await mounted("leave and return pending");
  await h.resolveSummary(h.summaryRequests[0], 321);
  await h.clickRoute("map-data");
  const firstEntry = h.summaryRequests.at(-1);
  await h.clickRoute("overview");
  await h.clickRoute("map-data");
  const secondEntry = h.summaryRequests.at(-1);
  assert.notEqual(firstEntry.id, secondEntry.id);
  await h.resolveSummary(firstEntry, 401, 4);
  assert.equal(h.getState("mapSummary").serverId, 321, "older pending entry reply fenced by generation");
  await h.resolveSummary(secondEntry, 402, 5);
  assert.equal(h.getState("mapSummary").serverId, 402);
  pass("leave/return while first entry is pending keeps existing generation fencing without canceling navigation", { firstRequest: firstEntry.id, secondRequest: secondEntry.id, finalServerId: 402 });
  await h.unmount();
}

{
  const h = await mounted("obsolete profile reply");
  await h.resolveSummary(h.summaryRequests[0], 321);
  await h.clickRoute("map-data");
  const oldEntry = h.summaryRequests.at(-1);
  await h.setProfileId("profile-b");
  const profileBootstrap = h.summaryRequests.at(-1);
  assert.equal(profileBootstrap.profileId, "profile-b");
  assert.equal(h.getState("mapSummary"), null);
  await h.resolveSummary(oldEntry, 411, 9);
  assert.equal(h.getState("mapSummary"), null, "old profile reply ignored");
  await h.resolveSummary(profileBootstrap, 422, 10);
  assert.equal(h.getState("mapSummary").serverId, 422);
  pass("obsolete-profile entry reply is ignored and new profile bootstrap remains authoritative", { oldRequest: oldEntry.id, newRequest: profileBootstrap.id });
  await h.unmount();
}

{
  const h = await mounted("bridge unavailable", { available: false });
  assert.equal(h.summaryRequests.length, 0);
  h.clearEvents();
  await h.clickRoute("map-data");
  assert.equal(h.summaryRequests.length, 0);
  assert.equal(h.getActiveRoute(), "map-data");
  assert.deepEqual(h.events.map((entry) => entry.event), ["click", "transition:start"]);
  pass("unavailable native bridge fences summary transport but does not fence local navigation", { events: structuredClone(h.events) });
  await h.unmount();
}

{
  const h = await mounted("direct initial Map", { search: "?view=map-data" });
  assert.equal(h.getActiveRoute(), "map-data");
  assert.equal(h.summaryRequests.length, 1, "direct initial Map uses profile/bootstrap owner only");
  await h.resolveSummary(h.summaryRequests[0], 321);
  pass("direct initial Map route does not synthesize a navigation-entry request", { requestCount: h.summaryRequests.length, events: structuredClone(h.events) });
  await h.unmount();
}

{
  const h = await mounted("connected poll attribution", { online: true });
  assert.equal(h.summaryRequests.length, 2, "profile bootstrap plus connected poll immediate request");
  const bootstrap = h.summaryRequests[0];
  const initialPoll = h.summaryRequests[1];
  await h.resolveSummary(bootstrap, 311);
  await h.resolveSummary(initialPoll, 312);
  h.clearEvents();
  await h.clickRoute("map-data");
  const entry = h.summaryRequests.at(-1);
  assert.equal(entry.activeRoute, "overview");
  await h.resolveSummary(entry, 313);
  const beforePoll = h.summaryRequests.length;
  h.clearEvents();
  await h.advance(5000);
  assert.ok(h.summaryRequests.length > beforePoll, "connected five-second poll still owns summary requests");
  const poll = h.summaryRequests.at(-1);
  assert.equal(poll.activeRoute, "map-data");
  await h.resolveSummary(poll, 314);
  pass("profile bootstrap, Map-entry request and independent connected polling remain separately attributable", { requestIds: { bootstrap: bootstrap.id, initialPoll: initialPoll.id, entry: entry.id, nextPoll: poll.id } });
  await h.unmount();
}

const report = {
  result: "LWB317_MAP_ENTRY_FOCUSED_OK",
  source: { path: "src/LWBridge.UI-0.3.17/src/App.jsx", sha256: sha256(source), bytes: Buffer.byteLength(source) },
  cases,
  limits: "Actual current App source is parsed and executed with its production callbacks/effects. Page/icon children and bridge/map transport are controlled inert bindings; no network, native provider, Last War, server jump, scan start, update, or gameplay action is invoked. The harness uses disconnected mode except the explicit polling-attribution case, so bootstrap, entry and poll requests can be distinguished.",
};
fs.writeFileSync(path.join(here, "map-entry-focused-results.json"), `${JSON.stringify(report, null, 2)}\n`);
console.log(`LWB317_MAP_ENTRY_FOCUSED_OK cases=${cases.length}`);
