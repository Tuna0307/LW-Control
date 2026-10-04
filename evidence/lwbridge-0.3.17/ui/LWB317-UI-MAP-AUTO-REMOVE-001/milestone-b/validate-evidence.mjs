import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const readJson = (name) => JSON.parse(fs.readFileSync(path.join(here, name), "utf8"));
const hashBytes = (bytes) => crypto.createHash("sha256").update(bytes).digest("hex");
const hashFile = (file) => hashBytes(fs.readFileSync(file));
const fromRepo = (relative) => path.join(repo, relative);

function verifyLocator(locator) {
  const bytes = fs.readFileSync(fromRepo(locator.file));
  const slice = bytes.subarray(locator.utf8ByteOffset, locator.utf8ByteOffset + locator.byteLength);
  assert.equal(hashBytes(slice), locator.sha256, `locator hash ${locator.id}`);
  if (locator.text !== undefined) assert.equal(slice.toString("utf8"), locator.text, `locator text ${locator.id}`);
}

const pinned = readJson("pinned-inputs.json");
assert.equal(pinned.task, "LWB317-UI-MAP-AUTO-REMOVE-001");
assert.equal(pinned.milestone, "B");
assert.equal(pinned.evidenceState, "EXACT_BYTES / EXACT_CONTRACT");
assert.equal(hashFile(pinned.reference.exe.path), pinned.reference.exe.sha256, "EXE pin");
for (const key of ["panel", "index", "css"]) assert.equal(hashFile(fromRepo(pinned.reference[key].path)), pinned.reference[key].sha256, `reference ${key}`);
assert.equal(hashFile(fromRepo(pinned.reference.acceptedConfigLocators.path)), pinned.reference.acceptedConfigLocators.sha256, "accepted config locator pin");
for (const item of [...pinned.current.files, ...Object.values(pinned.current.locales), ...pinned.dependencies]) {
  assert.equal(hashFile(fromRepo(item.path)), item.sha256, `current/dependency pin ${item.path}`);
}
for (const locator of [
  pinned.reference.removeIconUse,
  pinned.reference.removeIconComponent,
  pinned.reference.typeList,
  pinned.reference.lastTypePredicate,
  pinned.reference.runNowPredicate,
  pinned.current.autoCard,
  pinned.current.removeIconChild,
  pinned.current.emitAutoConfig,
  pinned.current.addAutoServers,
  pinned.current.toggleAutoType,
  pinned.current.lastTypePredicate,
  pinned.current.runNowPredicate,
]) verifyLocator(locator);

assert.deepEqual(pinned.configValidation.recoveredKnownKinds, ["city", "resource", "monster", "truck", "railway", "dispatch", "ghost", "treasure"]);
assert.equal(pinned.current.referenceCssExactlyMatchesOriginal, true);
assert.deepEqual(pinned.current.cssImportOrder, ["./reference.css", "./styles.css"]);

const render = readJson("render-results.json");
assert.equal(render.marker, "LWB317_MAP_AUTO_REMOVE_B_STATES_COMPARED");
assert.deepEqual(render.states, ["default", "configured-waiting", "auto-running", "manual-reading", "offline", "one-remaining-type"]);
assert.deepEqual(render.languages, ["en", "ja"]);
assert.equal(render.pairs.length, 12);
assert.equal(render.totals.exactViews, 12);
assert.equal(render.totals.exactMarkup, 12);
assert.equal(render.totals.viewDifferences, 0);
assert.ok(render.pairs.every((pair) => pair.enteredAutoViaActualTabCallback && pair.exactViewMatch && pair.exactMarkupMatch && pair.differences.length === 0));

for (const pair of render.pairs) {
  for (const side of ["original", "current"]) {
    const raw = fs.readFileSync(fromRepo(pair.rawFiles[side]), "utf8").replace(/\r?\n$/, "");
    assert.equal(hashBytes(Buffer.from(raw, "utf8")), pair[`${side}MarkupSha256`], `${pair.pairId}/${side} raw markup`);
  }
}

for (const language of ["en", "ja"]) {
  const byState = Object.fromEntries(render.pairs.filter((pair) => pair.language === language).map((pair) => [pair.id, pair]));
  assert.equal(byState.default.currentView.options.runNow.disabled, true, `${language} default Run now`);
  assert.equal(byState["configured-waiting"].currentView.options.runNow.disabled, false, `${language} waiting Run now`);
  assert.equal(byState["auto-running"].currentView.options.runNow.disabled, true, `${language} running Run now`);
  assert.equal(byState["manual-reading"].currentView.options.runNow.disabled, true, `${language} Manual reading Run now`);
  assert.equal(byState.offline.currentView.options.runNow.disabled, true, `${language} offline Run now`);
  assert.equal(byState["one-remaining-type"].currentView.options.runNow.disabled, false, `${language} one-type Run now`);
  const types = byState["one-remaining-type"].currentView.types;
  assert.equal(types.filter((type) => type.checked).length, 1, `${language} one selected type`);
  assert.equal(types.filter((type) => type.disabled).length, 1, `${language} one disabled type`);
  assert.equal(types.find((type) => type.checked)?.disabled, true, `${language} selected last type disabled`);
}

const controls = readJson("control-results.json");
assert.equal(controls.marker, "LWB317_MAP_AUTO_REMOVE_B_CONTROLS_REPLAYED");
assert.equal(controls.records.length, 4);
assert.deepEqual(controls.conclusions.sourceOrder, ["city", "resource", "monster", "truck", "railway", "dispatch", "ghost", "treasure"]);
assert.deepEqual(controls.conclusions.addExpected, [8, 15, 120]);
assert.deepEqual(controls.conclusions.enterExpected, [8, 15, 120, 7, 9]);
assert.deepEqual(controls.conclusions.removeExpected, [8, 120, 7, 9]);
for (const language of ["en", "ja"]) {
  const original = controls.records.find((record) => record.language === language && record.side === "original");
  const current = controls.records.find((record) => record.language === language && record.side === "current");
  assert.ok(original && current, `${language} control sides`);
  for (const record of [original, current]) {
    assert.deepEqual(record.add.serverIds, [8, 15, 120]);
    assert.deepEqual(record.enter.serverIds, [8, 15, 120, 7, 9]);
    assert.equal(record.enter.preventDefaultCount, 1);
    assert.deepEqual(record.remove.serverIds, [8, 120, 7, 9]);
    assert.deepEqual(record.nativeMutatorCalls, []);
    assert.equal(record.runNowInvoked, false);
  }
  const comparable = (record) => {
    const copy = structuredClone(record);
    delete copy.side;
    delete copy.nativeMutatorCalls;
    return copy;
  };
  assert.deepEqual(comparable(current), comparable(original), `${language} control replay parity`);
}

console.log("LWB317_MAP_AUTO_REMOVE_B_EVIDENCE_OK pairs=12 exactViews=12 exactMarkup=12 controls=4 nativeMutators=0 runNowClicks=0");
