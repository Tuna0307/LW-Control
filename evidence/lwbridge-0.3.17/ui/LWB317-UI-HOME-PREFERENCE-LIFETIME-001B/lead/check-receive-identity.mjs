import assert from "node:assert/strict";
import fs from "node:fs";
import crypto from "node:crypto";
import { createConfigDraft } from "../../../../../src/LWBridge.UI-0.3.17/src/previewConfig.js";

const base = new URL("../", import.meta.url);
const recovered = JSON.parse(fs.readFileSync(new URL("recovery/source-recovery.json", base)));
const source = fs.readFileSync(new URL("../../frontend-package/web/assets/index-BVfnK1wp.js", import.meta.url));
const hash = (bytes) => crypto.createHash("sha256").update(bytes).digest("hex").toUpperCase();
assert.equal(hash(source), recovered.source.sha256);
const locator = recovered.source.draftEngine;
const text = source.subarray(locator.utf8ByteOffset, locator.utf8ByteOffset + locator.byteLength);
assert.equal(hash(text), locator.sha256);
const original = new Function(`var w=e=>JSON.stringify(e);${text};return T;`)();
const gate = () => { let resolve; const promise = new Promise((yes) => { resolve = yes; }); return { promise, resolve }; };

async function trace(makeDraft) {
  const first = gate(), second = gate();
  let writes = 0, reads = 0;
  const store = makeDraft(false, { read: async () => { reads++; return true; },
    write: async () => (++writes === 1 ? first : second).promise });
  store.edit(true, false);
  const save = store.flush();
  store.receive(true);
  store.edit(false, false);
  store.flush();
  first.resolve(true);
  await Promise.resolve(); await Promise.resolve();
  second.resolve(false);
  await save;
  store.receive(true);
  await Promise.resolve(); await Promise.resolve();
  const repeated = { reads, draft: store.getSnapshot().draft };
  store.receive(false);
  store.receive(true);
  await Promise.resolve(); await Promise.resolve();
  const changed = { reads, draft: store.getSnapshot().draft };
  store.dispose();
  return { repeated, changed };
}
const expected = await trace(original), current = await trace(createConfigDraft);
assert.deepEqual(current, expected);
assert.deepEqual(expected, { repeated: { reads: 0, draft: false }, changed: { reads: 1, draft: true } });
const result = { task: "LWB317-UI-HOME-PREFERENCE-LIFETIME-001B", result: "LWB317_RECONNECT_RECEIVE_IDENTITY_OK",
  original: expected, current, source: locator,
  interpretation: "Repeated incoming identity remains ignored after a concurrent write; a changed identity enables source refresh. Preserve this recovered behavior rather than redesigning receive()." };
fs.writeFileSync(new URL("receive-identity-results.json", import.meta.url), JSON.stringify(result, null, 2) + "\n");
console.log(result.result);
