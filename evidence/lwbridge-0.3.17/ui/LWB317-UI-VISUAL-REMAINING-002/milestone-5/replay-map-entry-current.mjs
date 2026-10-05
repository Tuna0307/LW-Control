import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { createAppHarness } from "./map-entry-current-harness.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const appPath = path.join(repo, "src/LWBridge.UI-0.3.17/src/App.jsx");
const source = fs.readFileSync(appPath, "utf8");
const sha256 = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
const cases = [];

async function mounted(label, options = {}) {
  const harness = await createAppHarness(source, `m5 ${label}`, options);
  await harness.mount();
  return harness;
}

{
  const h = await mounted("different-route rejected entry");
  assert.equal(h.summaryRequests.length, 1, "profile bootstrap request");
  await h.resolveSummary(h.summaryRequests[0], 321);
  h.clearEvents();
  await h.clickRoute("map-data");
  const entry = h.summaryRequests.at(-1);
  assert.equal(h.getActiveRoute(), "map-data", "navigation activates while summary remains pending");
  assert.deepEqual(h.events.slice(0, 4).map((event) => event.event), ["click", "summary:start", "preload", "transition:start"]);
  await h.rejectSummary(entry);
  assert.equal(h.getActiveRoute(), "map-data", "summary rejection does not roll back navigation");
  cases.push({ name: "summary starts before transition and rejection is non-blocking", events: structuredClone(h.events) });
  await h.unmount();
}

{
  const h = await mounted("active Map no-op");
  await h.resolveSummary(h.summaryRequests[0], 321);
  await h.clickRoute("map-data");
  const requestCount = h.summaryRequests.length;
  h.clearEvents();
  await h.clickRoute("map-data");
  assert.equal(h.summaryRequests.length, requestCount, "active Map re-click must not create an entry summary");
  assert.deepEqual(h.events, [{ event: "click", routeKey: "map-data", activeRoute: "map-data" }]);
  await h.rejectSummary(h.summaryRequests.at(-1));
  cases.push({ name: "active Map re-click remains inert", requestCount });
  await h.unmount();
}

{
  const h = await mounted("unavailable bridge", { available: false });
  assert.equal(h.summaryRequests.length, 0);
  h.clearEvents();
  await h.clickRoute("map-data");
  assert.equal(h.summaryRequests.length, 0, "unavailable provider must fence summary transport");
  assert.equal(h.getActiveRoute(), "map-data", "local navigation remains available");
  assert.deepEqual(h.events.map((event) => event.event), ["click", "preload", "transition:start"]);
  cases.push({ name: "unavailable provider fences transport without fencing navigation", events: structuredClone(h.events) });
  await h.unmount();
}

{
  const h = await mounted("obsolete profile reply");
  await h.resolveSummary(h.summaryRequests[0], 321);
  await h.clickRoute("map-data");
  const oldEntry = h.summaryRequests.at(-1);
  await h.setProfileId("profile-b");
  const newBootstrap = h.summaryRequests.at(-1);
  assert.equal(newBootstrap.profileId, "profile-b", "replacement profile owns the new bootstrap request");
  assert.equal(h.getState("mapSummary"), null, "profile replacement clears prior summary state");
  await h.resolveSummary(oldEntry, 411, 9);
  assert.equal(h.getState("mapSummary"), null, "obsolete profile response stays retired");
  await h.resolveSummary(newBootstrap, 422, 10);
  assert.equal(h.getState("mapSummary").serverId, 422, "replacement profile response becomes authoritative");
  cases.push({ name: "profile replacement retires old Map request ownership", oldRequest: oldEntry.id, newRequest: newBootstrap.id });
  await h.unmount();
}

console.log(JSON.stringify({
  marker: "LWB317_REMAINING_M5_MAP_ENTRY_CURRENT_OK",
  appSha256: sha256(source),
  cases,
  limits: "Current App source is executed through the accepted inert Map-entry harness. No native/network/gameplay action is invoked.",
}));
