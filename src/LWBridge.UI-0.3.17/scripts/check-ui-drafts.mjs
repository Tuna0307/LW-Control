import assert from "node:assert/strict";
import { createConfigDraft } from "../src/previewConfig.js";
import { normalizeJoinRestrictions, validJoinRestrictions, makePreviewAfkProfile, previewAfkProfileValid, applyAfkTarget, previewAfkTargets } from "../src/previewAfkContracts.js";
import { initialAutomationDraft, automationDraftError, activateTraining } from "../src/previewAutomationContracts.js";

// Exercise the actual recovered store with deferred replies, rather than marker
// strings or immediate-success mocks that miss edits made during a save.
let writes = [];
let confirmed = { value: 0 };
const store = createConfigDraft(confirmed, {
  valid: (draft) => Number.isInteger(draft.value) && draft.value >= 0,
  read: async () => structuredClone(confirmed),
  write: (draft) => new Promise((resolve, reject) => writes.push({ draft, resolve: (value) => { confirmed = value; resolve(value); }, reject })),
});
store.edit({ value: 1 }, false);
const firstSave = store.flush();
assert.equal(store.getSnapshot().saving, true);
store.edit({ value: 2 }, false);
writes[0].resolve({ value: 1 });
await Promise.resolve();
assert.equal(store.getSnapshot().draft.value, 2, "old save acknowledgment overwrote a newer draft");
assert.equal(store.getSnapshot().confirmed.value, 1);
writes[1].resolve({ value: 2 });
await firstSave;
assert.equal(store.getSnapshot().dirty, false);
store.edit({ value: 3 }, false);
const failedSave = store.flush();
writes[2].reject(new Error("fixture failure"));
await assert.rejects(failedSave);
assert.equal(store.getSnapshot().draft.value, 3);
assert.equal(store.getSnapshot().confirmed.value, 2);
assert.equal(store.getSnapshot().dirty, true);
assert.equal(store.getSnapshot().saving, false);
const retry = store.flush();
writes[3].resolve({ value: 3 });
await retry;
assert.equal(store.getSnapshot().error, null);
store.edit({ value: -1 }, false);
await assert.rejects(store.flush(), /CONFIG_DRAFT_INVALID/);
assert.equal(writes.length, 4, "invalid draft dispatched a write");
await store.refresh(true);
assert.deepEqual(store.getSnapshot().draft, { value: 3 });
assert.equal(store.getSnapshot().dirty, false);
store.dispose();

const freshJoin = normalizeJoinRestrictions(undefined, 1, true);
assert.deepEqual(freshJoin, { enabled: false, mode: "delay", slotRange: [2, 2], slotDelaySeconds: 0, delaySeconds: [1, 3], maxWaitMinutes: 5, leaderListMode: "off", leaders: [], skipSoloLeader: false, skipKicked: true });
assert.equal(validJoinRestrictions(freshJoin), true);
assert.equal(validJoinRestrictions({ ...freshJoin, delaySeconds: [0.001, 3] }), false);
assert.equal(validJoinRestrictions({ ...freshJoin, delaySeconds: [4, 3] }), false);
assert.equal(validJoinRestrictions({ ...freshJoin, slotRange: [2, 6] }), false);
assert.equal(validJoinRestrictions({ ...freshJoin, leaders: [{ uid: "10001" }, { uid: "10001" }] }), false);
assert.equal(validJoinRestrictions({ ...freshJoin, leaders: [{ uid: "0" }] }), false);
assert.deepEqual(normalizeJoinRestrictions({ mode: "slotRange", slotRange: [3, 5] }).slotRange, [3, 3]);

const profileA = makePreviewAfkProfile("a", "A", "join");
const profileB = makePreviewAfkProfile("b", "B", "join");
assert.notEqual(profileA.joinRestrictions, profileB.joinRestrictions);
profileA.joinRestrictions.leaders.push({ uid: "10001", name: "Avery" });
profileA.joinRestrictions.delaySeconds[0] = 2;
assert.equal(profileB.joinRestrictions.leaders.length, 0);
assert.equal(profileB.joinRestrictions.delaySeconds[0], 1);
const changedTarget = applyAfkTarget(makePreviewAfkProfile("farm", "Farm"), previewAfkTargets.find((target) => target.key === "food"));
assert.equal(changedTarget.targetKey, "food");
assert.equal(changedTarget.monsterNameKey, "fixture-food");
assert.equal(changedTarget.source, "search");
assert.equal(changedTarget.squadIndexes[0], 1);
assert.equal(previewAfkProfileValid(profileA), true);
for (const patch of [{ name: " " }, { squadIndexes: [] }, { executionLimit: NaN }, { executionLimit: 0.1 }, { levelFilterEnabled: true, minLevel: 5, maxLevel: 2 }, { distanceFilterEnabled: true, maxDistance: 0 }, { customTarget: true, targetKey: "query:", targetNameQuery: " " }]) assert.equal(previewAfkProfileValid({ ...profileA, ...patch }), false);

assert.deepEqual(initialAutomationDraft("Automatic Construction"), {});
assert.equal(initialAutomationDraft("Auto Training").trainingTotalCount, 0);
assert.deepEqual(initialAutomationDraft("Automatic Construction", "automation-config").constructionBuildingTypeIds, [1101, 1201]);
assert.equal(initialAutomationDraft("Auto Training", "automation-config").trainingTotalCount, 1000);
assert.equal(automationDraftError("Automatic Construction", { maxBuilders: 21 }), "automation.builderLimitError");
assert.equal(automationDraftError("Automatic Construction", { maxBuilders: 2 }), "");
assert.equal(automationDraftError("Auto Training", { trainingTotalCount: 1000001 }), "automation.soldierTraining.quantityError");
const activeTraining = activateTraining({ trainingTotalCount: 1000 }, true, { orderId: "previous", total: 1000, completed: 400, reason: "working" }, () => "new-id");
assert.equal(activeTraining.orderId, "previous");
assert.equal(activeTraining.activationId, "new-id");
assert.equal(activeTraining.collectEnabled, true);
assert.equal(activateTraining({ trainingTotalCount: 1000 }, true, { orderId: "previous", total: 1000, completed: 400, reason: "unconfirmed" }, () => "new-id").orderId, "new-id");
assert.equal(activateTraining({ trainingTotalCount: 0 }, true, null).enabled, undefined);
assert.equal(automationDraftError("Treasure", { claimMin: 5, claimMax: 1 }), "automation.treasureDelayError");
assert.equal(automationDraftError("Red Packet", { replyEnabled: true, replies: " " }), "automation.replyRequired");
assert.equal(automationDraftError("Treasure", { treasureDispatchEnabled: true, dispatchSquads: [] }), "automation.treasureDispatchSquadRequired");
assert.equal(automationDraftError("Dispatch Assist", { autoHelp: true, qualities: [], delaySeconds: [0, 0], intervalSeconds: 30 }), "automation.dispatchAssistConfigError");
assert.equal(automationDraftError("Dispatch Assist", { autoHelp: false, qualities: ["ssr"], delaySeconds: [7, 3], intervalSeconds: 30 }), "automation.dispatchAssistConfigError");
console.log("LWB317_UI_DRAFT_CHECKS_OK: concurrent edits, ack, error/retry/discard, no invalid dispatch, join defaults/precision, profile independence and card validation");
