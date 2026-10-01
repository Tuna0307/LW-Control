import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { createConfigDraft } from "../../../../src/LWBridge.UI-0.3.17/src/previewConfig.js";
import { activateTraining, automationDraftError, initialAutomationDraft } from "../../../../src/LWBridge.UI-0.3.17/src/previewAutomationContracts.js";
import { applyAfkTarget, makePreviewAfkProfile, normalizeJoinRestrictions, previewAfkLevelOutOfRange, previewAfkProfileValid, previewAfkTargets } from "../../../../src/LWBridge.UI-0.3.17/src/previewAfkContracts.js";
import { dispatchWeeklyQualities, previewAssistJobs, previewAssistTasks, previewAutomationRuntime, previewResourceGatherConfig, previewTradeFixture, railwayWeeklyQualities, validPreviewResourceGatherConfig } from "../../../../src/LWBridge.UI-0.3.17/src/previewAutomationFixtures.js";
import { initialAfkToolbarConfig, previewGarrisonRuntime, previewMemberFixture, previewZombieBusRuntime } from "../../../../src/LWBridge.UI-0.3.17/src/previewAfkFixtures.js";
import { getMapPreviewProvider } from "../../../../src/LWBridge.UI-0.3.17/src/mapPreviewApi.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const pagesPath = path.resolve(here, "../../../../src/LWBridge.UI-0.3.17/src/Pages.jsx");
const pages = fs.readFileSync(pagesPath, "utf8");

const waitFor = async (predicate, label) => {
  for (let i = 0; i < 100; i += 1) {
    if (predicate()) return;
    await new Promise((resolve) => setImmediate(resolve));
  }
  throw new Error(`timed out waiting for ${label}`);
};

// Recovered config-store generation/acknowledgement semantics.
const writes = [];
const pending = [];
let server = { value: 0 };
const draft = createConfigDraft(server, {
  valid: (value) => Number.isInteger(value.value) && value.value >= 0,
  read: async () => structuredClone(server),
  write: (value) => new Promise((resolve) => {
    writes.push(structuredClone(value));
    pending.push(() => { server = structuredClone(value); resolve(structuredClone(value)); });
  }),
});
draft.edit({ value: 1 }, false);
const saving = draft.flush(false);
await waitFor(() => pending.length === 1, "first config write");
draft.edit({ value: 2 }, false);
draft.flush(false).catch(() => {});
pending.shift()();
await waitFor(() => pending.length === 1 && writes.length === 2, "queued config write");
assert.deepEqual(draft.getSnapshot().draft, { value: 2 });
pending.shift()();
await saving;
assert.deepEqual(draft.getSnapshot().confirmed, { value: 2 });
draft.dispose();

let failOnce = true;
server = { value: 10 };
const retry = createConfigDraft(server, {
  valid: (value) => Number.isInteger(value.value),
  read: async () => structuredClone(server),
  write: async (value) => {
    if (failOnce) { failOnce = false; throw new Error("fixture save failed"); }
    server = structuredClone(value);
    return structuredClone(value);
  },
});
retry.edit({ value: 11 }, false);
await assert.rejects(retry.flush(), /fixture save failed/);
assert.equal(retry.getSnapshot().dirty, true);
assert.equal(retry.getSnapshot().draft.value, 11);
await retry.flush();
retry.edit({ value: 12 }, false);
await retry.refresh(true);
assert.equal(retry.getSnapshot().draft.value, 11);
retry.dispose();

// Weekly quality is controlled source data and validates all seven days.
assert.deepEqual(railwayWeeklyQualities, ["ssr", "ur", "ssr", "ssr", "ssr", "ur", "ssr"]);
assert.deepEqual(dispatchWeeklyQualities, ["none", "ur", "none", "none", "none", "ur", "none"]);
assert.equal(automationDraftError("Trucks", { ...initialAutomationDraft("Trucks", "automation-config"), weeklyQualities: railwayWeeklyQualities }), "");
assert.equal(automationDraftError("Trucks", { ...initialAutomationDraft("Trucks", "automation-config"), weeklyQualities: ["ur"] }), "configSave.failed");
assert.equal(automationDraftError("Secret Task", { ...initialAutomationDraft("Secret Task", "automation-config"), dispatchAssistEnabled: true, assistQualities: [] }), "automation.dispatchAssistConfigError");
assert.match(pages, /<WeeklyQualityPreview enabled=\{enabled\} values=\{weekly\}/);

// Trade and Dispatch Assist positive/negative fixtures are deterministic and disclosed.
const tradePositive = previewTradeFixture("automation-trade-positive");
assert.equal(tradePositive.goods.length, 3);
assert.equal(tradePositive.purchases.length, 2);
assert.equal(tradePositive.goods.every((item) => item.name.startsWith("Fixture ")), true);
assert.equal(tradePositive.goods.some((item) => item.offers.every((offer) => offer.exclusive)), true);
assert.equal(previewTradeFixture("automation-trade-empty").goods.length, 0);
assert.equal(previewTradeFixture("automation-trade-loading").loading, true);
assert.equal(previewTradeFixture("automation-trade-error").error, true);
assert.equal(previewAssistTasks.length, 2);
assert.equal(previewAssistJobs.some((job) => job.scheduleStatus === "scheduled"), true);
assert.equal(previewAssistJobs.some((job) => job.scheduleStatus === "failed"), true);
assert.match(pages, /data-preview-action="presentation-only"/);

// Gather config is a persisted task draft rather than component-only settings.
assert.equal(validPreviewResourceGatherConfig(previewResourceGatherConfig("automation-config")), true);
assert.equal(validPreviewResourceGatherConfig({ ...previewResourceGatherConfig("automation-config"), manualResumeDelaySeconds: 59 }), false);

// Runtime summary/status fixtures exercise timing/result/shield branches.
assert.equal(previewAutomationRuntime("Trucks", "automation-runtime-running").running, true);
assert.equal(previewAutomationRuntime("Secret Task", "automation-runtime-error").state, "error");
assert.equal(previewAutomationRuntime("Weekend Shield", "automation-config").shielded, true);
assert.equal(previewAutomationRuntime("Attack Shield", "automation-shield-pending").pendingReason, "fixture_attack_detected");

// Training stale/working order semantics remain intact after the completion work.
const trainingDraft = { ...initialAutomationDraft("Auto Training", "automation-config"), trainingTotalCount: 1000 };
let ids = ["new-order", "new-activation"];
const freshTraining = activateTraining(trainingDraft, true, { orderId: "stale-order", total: 1000, completed: 5, reason: "unconfirmed" }, () => ids.shift());
assert.equal(freshTraining.orderId, "new-order");
ids = ["activation-only"];
const reusedTraining = activateTraining(trainingDraft, true, { orderId: "working-order", total: 1000, completed: 5, reason: "working" }, () => ids.shift());
assert.equal(reusedTraining.orderId, "working-order");

// AFK target/range/profile independence and member variants.
const left = makePreviewAfkProfile("left", "Fixture Left", "farm", "steel");
const right = makePreviewAfkProfile("right", "Fixture Right", "farm", "food");
const gold = previewAfkTargets.find((target) => target.key === "gold");
assert.equal(applyAfkTarget(left, gold).targetKey, "gold");
assert.equal(right.targetKey, "food");
assert.equal(previewAfkProfileValid(left), true);
assert.equal(previewAfkLevelOutOfRange({ ...left, levelFilterEnabled: true, minLevel: 11, maxLevel: 12 }, previewAfkTargets.find((target) => target.key === "steel")), true);
assert.equal(previewAfkLevelOutOfRange({ ...left, levelFilterEnabled: true, progressiveLevels: true, minLevel: 10, maxLevel: 99 }, previewAfkTargets.find((target) => target.key === "steel")), false);
assert.equal(previewMemberFixture("squads-profile-members-loading").ready, false);
assert.equal(previewMemberFixture("squads-profile-members-failed").failed, true);
assert.equal(previewMemberFixture("squads-profile-members-offline").online, false);
assert.equal(previewMemberFixture("squads-profile-members-empty").members.length, 1);
assert.equal(previewMemberFixture("squads-profile-members-self").selfUid, "10000");

// Toolbar defaults and runtime variants.
const toolbar = initialAfkToolbarConfig("squads-profile-config");
assert.equal(toolbar.minStamina, 50);
assert.deepEqual(toolbar.allianceDrill.squadIndexes, [1]);
assert.deepEqual(normalizeJoinRestrictions(toolbar.allianceDrill.joinRestrictions, 1, true).delaySeconds, [1, 3]);
assert.equal(previewGarrisonRuntime("squads-profile-garrison-running").guardingCount, 1);
assert.equal(previewGarrisonRuntime("squads-profile-garrison-unavailable").buildings.every((building) => !building.available), true);
assert.equal(previewZombieBusRuntime("squads-profile-zombie-running").assignments.length, 2);
assert.equal(previewZombieBusRuntime("squads-profile-zombie-error").state, "error");

// Map C1/C2/C3 fixture/action fencing remains intact.
const provider = getMapPreviewProvider("preview", "map-populated");
assert.ok(provider?.mapApi?.previewFixture);
const finalPage = await provider.mapApi.search("city", { page: 2, pageSize: 50, sorts: [{ sortBy: "updatedAt", sortOrder: "desc" }] });
assert.equal(finalPage.total, 52);
assert.equal(finalPage.rows.length, 2);
const zero = await provider.mapApi.search("city", { alliance: "fixture-no-such-alliance", page: 1, pageSize: 50 });
assert.equal(zero.total, 0);
await assert.rejects(provider.mapApi.search("city", { sorts: [{ sortBy: "unsupported", sortOrder: "desc" }] }), /unsupported city sort column/);
await assert.rejects(provider.mapApi.start(), /Browser preview cannot execute native Map action/);

console.log("LWB317_UI_CORRECT003_STATE_OK", {
  configWrites: writes.map((value) => value.value),
  trade: { goods: tradePositive.goods.length, purchases: tradePositive.purchases.length },
  assist: { tasks: previewAssistTasks.length, jobs: previewAssistJobs.length },
  afkTargets: previewAfkTargets.length,
  map: { total: finalPage.total, finalRows: finalPage.rows.length, zero: zero.total },
});
