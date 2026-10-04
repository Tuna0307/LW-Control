import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const sourcePath = path.join(repo, "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js");
const source = fs.readFileSync(sourcePath);
const text = source.toString("utf8");
const sha256 = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();

assert.equal(sha256(source), "44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6");

function sliceBetween(start, end, from = 0) {
  const startIndex = text.indexOf(start, from);
  assert.ok(startIndex >= 0, `missing start marker: ${start}`);
  const endIndex = text.indexOf(end, startIndex + start.length);
  assert.ok(endIndex > startIndex, `missing end marker after: ${start}`);
  const value = text.slice(startIndex, endIndex);
  return {
    utf8ByteOffset: Buffer.byteLength(text.slice(0, startIndex)),
    byteLength: Buffer.byteLength(value),
    sha256: sha256(Buffer.from(value, "utf8")),
    text: value,
  };
}

function locate(exact, from = 0) {
  const index = text.indexOf(exact, from);
  assert.ok(index >= 0, `missing exact slice: ${exact.slice(0, 80)}`);
  return {
    utf8ByteOffset: Buffer.byteLength(text.slice(0, index)),
    byteLength: Buffer.byteLength(exact),
    sha256: sha256(Buffer.from(exact, "utf8")),
    text: exact,
  };
}

const draftEngine = sliceBetween("function T(e,t){", "var ee=new Map", 190000);
assert.equal(draftEngine.utf8ByteOffset, 191297);
const registry = sliceBetween("var ee=new Map", "var oe=``", 193000);
const registryLookup = locate("function re(e,t,n,r){", 193000);
assert.equal(registryLookup.utf8ByteOffset, 193714);
const hook = sliceBetween("function me(e,t,n,r=A()){", "var he=", 194000);
assert.equal(hook.utf8ByteOffset, 195132);
const saveError = sliceBetween("function Oe({state:e,label:t,disabled:n=!1})", "var ke=", 198000);
assert.equal(saveError.utf8ByteOffset, 199047);
const switchComponent = sliceBetween("function Bn({label:e,checked:t,disabled:n=!1,variant:r=`switch`,onChange:i})", "function Vn", 212000);
assert.equal(switchComponent.utf8ByteOffset, 213332);
const requestHelper = sliceBetween("function N(e,t){", "function Pe", 200000);
const profilePayload = locate("function P(e){return e?{profileId:e}:void 0}", 200000);
const statusRequest = locate("function ot(e){return N(`get_status`,P(e))}", 200000);
const automationRequest = locate("function ht(e,t,n){return N(`set_automation`,{...P(n),name:e,enabled:t})}", 200000);
const reconnectStore = locate("ue=me(`flag:autoForceUpdateReload`,ne,{read:async()=>(await ot(r.selectedProfileId)).config?.auto_force_update_reload??!1,write:async e=>(await ht(`autoForceUpdateReload`,e,r.selectedProfileId)).enabled},r.selectedProfileId),de=ue.draft", 330000);
const statusConsumer = sliceBetween("function Ot(e){", "async function At", 330000);
const editFlush = sliceBetween("async function It(e,t){", "function Lt", 300000);
assert.equal(editFlush.utf8ByteOffset, 366485);
const homeBinding = locate("autoReconnect:de,onAutoLaunchGameChange:n.setAutoLaunchGame,onAutoReconnectChange:e=>{It(`autoForceUpdateReload`,e)}", 300000);
const bannerAnchor = locate("profile-view-context`,children:[[O,ce,ue,j]", 300000);
assert.equal(bannerAnchor.utf8ByteOffset, 372793);
const bannerBinding = locate("[O,ce,ue,j].map((e,n)=>(0,M.jsx)(Oe,{state:e.state,label:t([`automation.weekendShield.title`,`automation.attackShield.title`,`automation.autoReconnect.title`,`automation.autoClosePopup.title`][n])},n))", 300000);

const makeDraft = new Function(`var w=e=>JSON.stringify(e);${draftEngine.text};return T;`)();
const deferred = () => {
  let resolve;
  let reject;
  const promise = new Promise((yes, no) => { resolve = yes; reject = no; });
  return { promise, resolve, reject };
};
const first = deferred();
const second = deferred();
let confirmed = false;
const writes = [];
let readCount = 0;
const store = makeDraft(false, {
  read: async () => { readCount += 1; return confirmed; },
  write: async (value) => {
    const gate = writes.length === 0 ? first : second;
    writes.push({ value, gate });
    const result = await gate.promise;
    confirmed = result;
    return result;
  },
});

store.edit(true, false);
const firstFlush = store.flush();
assert.deepEqual(store.getSnapshot(), { draft: true, confirmed: false, dirty: true, saving: true, error: null });
store.edit(false, false);
const secondFlush = store.flush();
assert.equal(secondFlush, firstFlush, "concurrent flush must join the active save loop");
assert.equal(writes.length, 1);
assert.equal(store.getSnapshot().draft, false);
first.resolve(true);
await Promise.resolve();
await Promise.resolve();
assert.equal(writes.length, 2, "newer draft must serialize after the first acknowledgement");
assert.equal(writes[1].value, false);
second.reject(new Error("SECOND_WRITE_FAILED"));
await assert.rejects(firstFlush, /SECOND_WRITE_FAILED/);
const failed = store.getSnapshot();
assert.equal(failed.confirmed, true);
assert.equal(failed.draft, false);
assert.equal(failed.dirty, true);
assert.equal(failed.saving, false);
assert.match(String(failed.error), /SECOND_WRITE_FAILED/);

const retry = deferred();
const oldAdapter = {
  read: async () => { readCount += 1; return confirmed; },
  write: async (value) => { writes.push({ value, gate: retry }); const result = await retry.promise; confirmed = result; return result; },
};
store.setAdapter(oldAdapter);
const retryPromise = store.flush();
assert.equal(store.getSnapshot().saving, true);
assert.equal(store.getSnapshot().error, null);
retry.resolve(false);
await retryPromise;
assert.deepEqual(store.getSnapshot(), { draft: false, confirmed: false, dirty: false, saving: false, error: null });

store.edit(true, false);
confirmed = false;
await store.refresh(true);
assert.deepEqual(store.getSnapshot(), { draft: false, confirmed: false, dirty: false, saving: false, error: null });
assert.ok(readCount >= 1);

const report = {
  task: "LWB317-UI-HOME-PREFERENCE-LIFETIME-001B",
  date: "2026-10-04",
  classification: "EXACT_BYTES / EXACT_CONTRACT",
  source: {
    path: path.relative(repo, sourcePath).replaceAll("\\", "/"),
    sha256: sha256(source),
    draftEngine,
    profileRegistry: registry,
    profileRegistryLookup: registryLookup,
    profileDraftHook: hook,
    saveError,
    switchComponent,
    requestHelper,
    profilePayload,
    statusRequest,
    automationRequest,
    reconnectStore,
    statusConsumer,
    editFlush,
    homeBinding,
    bannerAnchor,
    bannerBinding,
  },
  contract: {
    initialDefault: false,
    storeKey: "flag:autoForceUpdateReload",
    ownerKey: "[selectedProfileId, storeKey]",
    read: "get_status(profileId).config.auto_force_update_reload ?? false",
    write: "set_automation({ profileId, name: 'autoForceUpdateReload', enabled }).enabled",
    edit: "edit(value, false) changes draft immediately, then flush()",
    concurrentWrite: "active flush is shared; the save loop serializes the newer generation after the first acknowledgement",
    latestFailure: "confirmed remains the last acknowledged value; draft remains the failed newer value, dirty=true, error is retained",
    retry: "flush() retries the dirty draft and clears error while saving",
    discard: "refresh(true) reads the adapter and replaces draft/confirmed, clearing error",
    incomingWhileDirtyOrSaving: "receive(value) records the incoming identity but does not refresh while dirty/saving/action-pending",
    homeChecked: "ue.draft",
    disabledBySaving: false,
    bannerOrder: ["autoWeekendShield", "autoAttackShield", "autoForceUpdateReload", "autoClosePopup"],
  },
  execution: {
    firstEditVisibleBeforeAck: true,
    draftAfterSecondEditWhileSaving: false,
    writesBeforeFirstAck: [true],
    serializedWrites: [true, false],
    failedSnapshot: { draft: failed.draft, confirmed: failed.confirmed, dirty: failed.dirty, saving: failed.saving, error: String(failed.error) },
    retrySettled: store.getSnapshot(),
  },
  limit: "This executes the recovered frontend draft engine and request contract only. It does not establish per-profile persistence in the clone desktop adapter or access the protected original runtime.",
};

fs.mkdirSync(here, { recursive: true });
fs.writeFileSync(path.join(here, "source-recovery.json"), `${JSON.stringify(report, null, 2)}\n`);
console.log("LWB317_HOME_RECONNECT_SOURCE_RECOVERY_OK");
