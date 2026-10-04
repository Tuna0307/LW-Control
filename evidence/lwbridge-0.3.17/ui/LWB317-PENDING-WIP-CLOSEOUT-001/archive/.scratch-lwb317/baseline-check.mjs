import assert from "node:assert/strict";
import { createConfigDraft } from "../src/LWBridge.UI-0.3.17/src/previewConfig.js";
import { activateTraining, initialAutomationDraft } from "../src/LWBridge.UI-0.3.17/src/previewAutomationContracts.js";
import { applyAfkTarget, makePreviewAfkProfile, normalizeJoinRestrictions, previewAfkProfileValid, previewAfkTargets } from "../src/LWBridge.UI-0.3.17/src/previewAfkContracts.js";
import { getMapPreviewProvider } from "../src/LWBridge.UI-0.3.17/src/mapPreviewApi.js";

const waitFor = async (predicate, label) => {
  for (let i = 0; i < 100; i += 1) {
    if (predicate()) return;
    await new Promise((resolve) => setImmediate(resolve));
  }
  throw new Error(`timed out waiting for ${label}`);
};

const writes = [];
const pending = [];
let server = { value: 0 };
const draft = createConfigDraft(server, {
  valid: (value) => Number.isInteger(value.value) && value.value >= 0,
  read: async () => structuredClone(server),
  write: (value) => new Promise((resolve) => {
    writes.push(structuredClone(value));
    pending.push(() => {
      server = structuredClone(value);
      resolve(structuredClone(value));
    });
  }),
});
draft.edit({ value: 1 }, false);
const saving = draft.flush(false);
await waitFor(() => pending.length === 1, "first config write");
draft.edit({ value: 2 }, false);
draft.flush(false).catch(() => {});
pending.shift()();
await waitFor(() => pending.length === 1 && writes.length === 2, "queued config write");
assert.deepEqual(draft.getSnapshot().draft, { value: 2 }, "old acknowledgement must not overwrite a newer draft");
pending.shift()();
await saving;
assert.deepEqual(draft.getSnapshot().confirmed, { value: 2 });
assert.equal(draft.getSnapshot().dirty, false);
draft.dispose();

let failOnce = true;
server = { value: 10 };
const retry = createConfigDraft(server, {
  valid: (value) => Number.isInteger(value.value),
  read: async () => structuredClone(server),
  write: async (value) => {
    if (failOnce) {
      failOnce = false;
      throw new Error("fixture save failed");
    }
    server = structuredClone(value);
    return structuredClone(value);
  },
});
retry.edit({ value: 11 }, false);
await assert.rejects(retry.flush(), /fixture save failed/);
assert.equal(retry.getSnapshot().draft.value, 11);
assert.equal(retry.getSnapshot().dirty, true);
assert.match(String(retry.getSnapshot().error), /fixture save failed/);
await retry.flush();
assert.equal(retry.getSnapshot().confirmed.value, 11);
retry.edit({ value: 12 }, false);
await retry.refresh(true);
assert.equal(retry.getSnapshot().draft.value, 11, "forced refresh discards an unsaved draft back to adapter truth");
retry.dispose();

const trainingDraft = { ...initialAutomationDraft("Auto Training", "automation-config"), trainingTotalCount: 1000 };
let ids = ["new-order", "new-activation"];
const freshTraining = activateTraining(trainingDraft, true, { orderId: "stale-order", total: 1000, completed: 5, reason: "unconfirmed" }, () => ids.shift());
assert.equal(freshTraining.orderId, "new-order");
assert.equal(freshTraining.activationId, "new-activation");
assert.equal(freshTraining.trainEnabled, true);
assert.equal(freshTraining.promoteEnabled, true);
assert.equal(freshTraining.collectEnabled, true);
ids = ["activation-only"];
const reusedTraining = activateTraining(trainingDraft, true, { orderId: "working-order", total: 1000, completed: 5, reason: "working" }, () => ids.shift());
assert.equal(reusedTraining.orderId, "working-order");
assert.equal(reusedTraining.activationId, "activation-only");

const joinDefaults = normalizeJoinRestrictions(undefined, 1, true);
assert.deepEqual(joinDefaults.delaySeconds, [1, 3]);
assert.equal(joinDefaults.maxWaitMinutes, 5);
assert.equal(joinDefaults.skipKicked, true);
const left = makePreviewAfkProfile("left", "Left", "farm", "steel");
const right = makePreviewAfkProfile("right", "Right", "farm", "food");
const gold = previewAfkTargets.find((target) => target.key === "gold");
const changedLeft = applyAfkTarget(left, gold);
assert.equal(changedLeft.targetKey, "gold");
assert.equal(right.targetKey, "food", "editing one AFK profile must not mutate another profile");
assert.equal(previewAfkProfileValid(left), true);
assert.equal(previewAfkProfileValid({ ...left, distanceFilterEnabled: true, maxDistance: 0 }), false);

const provider = getMapPreviewProvider("preview", "map-populated");
assert.ok(provider?.mapApi?.previewFixture);
const finalPage = await provider.mapApi.search("city", { page: 2, pageSize: 50, sorts: [{ sortBy: "updatedAt", sortOrder: "desc" }] });
assert.equal(finalPage.total, 52);
assert.equal(finalPage.rows.length, 2, "final Map page must preserve exact fixture pagination");
const zero = await provider.mapApi.search("city", { alliance: "fixture-no-such-alliance", page: 1, pageSize: 50 });
assert.equal(zero.total, 0);
assert.equal(zero.rows.length, 0);
await assert.rejects(provider.mapApi.search("city", { sorts: [{ sortBy: "unsupported", sortOrder: "desc" }] }), /unsupported city sort column/);
await assert.rejects(provider.mapApi.start(), /Browser preview cannot execute native Map action/);

console.log("LWB317_UI_CORRECT003_BASELINE_OK", {
  writes: writes.map((value) => value.value),
  training: { freshOrder: freshTraining.orderId, reusedOrder: reusedTraining.orderId },
  afk: { left: changedLeft.targetKey, right: right.targetKey },
  map: { total: finalPage.total, finalRows: finalPage.rows.length, zero: zero.total },
});
