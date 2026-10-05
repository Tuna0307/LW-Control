import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";
import { createConfigDraft } from "../../../../../src/LWBridge.UI-0.3.17/src/previewConfig.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../../");
const outputPath = path.join(here, "config-store-ownership-results.json");
const verifyOnly = process.argv.includes("--verify");
const sha = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
const clone = (value) => structuredClone(value);
const relative = (file) => path.relative(repo, file).replaceAll("\\", "/");

const indexPath = path.join(repo, "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js");
const currentPath = path.join(repo, "src/LWBridge.UI-0.3.17/src/previewConfig.js");
const indexSource = fs.readFileSync(indexPath, "utf8");
const require = createRequire(path.join(repo, "src/LWBridge.UI-0.3.17/package.json"));
const { parse } = require("@babel/parser");
const ast = parse(indexSource, { sourceType: "module" });
const originalNode = ast.program.body.find((node) => node.type === "FunctionDeclaration" && node.id?.name === "T" && node.start > 180000 && node.start < 205000);
assert.ok(originalNode, "recovered config-store function T not found at expected source region");
const originalSource = indexSource.slice(originalNode.start, originalNode.end);
const originalFactory = new Function("w", `return (${originalSource});`)((value) => JSON.stringify(value));

function deferred() {
  let resolve;
  const promise = new Promise((done) => { resolve = done; });
  return { promise, resolve };
}

async function waitFor(predicate, label) {
  for (let index = 0; index < 100; index += 1) {
    if (predicate()) return;
    await new Promise((resolve) => setImmediate(resolve));
  }
  throw new Error(`timed out waiting for ${label}`);
}

function snapshot(store) {
  const value = store.getSnapshot();
  return {
    draft: clone(value.draft),
    confirmed: clone(value.confirmed),
    dirty: value.dirty,
    saving: value.saving,
    error: value.error ? String(value.error.message || value.error) : null,
  };
}

async function trace(factory) {
  const events = [];
  const makeAdapter = (name) => {
    let confirmed = { value: 0 };
    const pending = [];
    return {
      pending,
      adapter: {
        valid: (value) => Number.isInteger(value.value) && value.value >= 0,
        read: async () => clone(confirmed),
        write(value) {
          const gate = deferred();
          const written = clone(value);
          pending.push({
            value: written,
            resolve() {
              confirmed = clone(written);
              events.push(`${name}:ack:${written.value}`);
              gate.resolve(clone(confirmed));
            },
          });
          return gate.promise;
        },
      },
    };
  };

  const mainAdapter = makeAdapter("main");
  const assistAdapter = makeAdapter("assist");
  const main = factory({ value: 0 }, mainAdapter.adapter);
  const assist = factory({ value: 0 }, assistAdapter.adapter);

  main.edit({ value: 11 }, false);
  assist.edit({ value: 22 }, false);
  const mainSave = main.flush(false);
  const assistSave = assist.flush(false);
  assert.equal(mainAdapter.pending.length, 1, "main first write starts immediately");
  assert.equal(assistAdapter.pending.length, 1, "assist first write starts immediately");
  const concurrentStart = { main: snapshot(main), assist: snapshot(assist) };
  assert.equal(concurrentStart.main.saving, true);
  assert.equal(concurrentStart.assist.saving, true);

  assistAdapter.pending.shift().resolve();
  await assistSave;
  const assistFirst = { main: snapshot(main), assist: snapshot(assist), events: [...events] };
  assert.equal(assistFirst.main.saving, true, "Assist acknowledgement must not complete Main store");
  assert.equal(assistFirst.assist.saving, false, "Assist acknowledgement completes only Assist store");
  assert.deepEqual(assistFirst.events, ["assist:ack:22"]);

  mainAdapter.pending.shift().resolve();
  await mainSave;
  const independentDone = { main: snapshot(main), assist: snapshot(assist), events: [...events] };
  assert.deepEqual(independentDone.events, ["assist:ack:22", "main:ack:11"]);
  assert.equal(independentDone.main.confirmed.value, 11);
  assert.equal(independentDone.assist.confirmed.value, 22);

  main.edit({ value: 31 }, false);
  const queuedSave = main.flush(false);
  assert.equal(mainAdapter.pending.length, 1);
  main.edit({ value: 32 }, false);
  const sameSave = main.flush(false);
  assert.equal(sameSave, queuedSave, "same-store overlapping flushes share the active save promise");
  const firstQueued = mainAdapter.pending.shift();
  assert.equal(firstQueued.value.value, 31);
  firstQueued.resolve();
  await waitFor(() => mainAdapter.pending.length === 1, "newer queued Main write");
  const afterOlderAck = snapshot(main);
  assert.equal(afterOlderAck.confirmed.value, 31, "older acknowledgement updates confirmed state");
  assert.equal(afterOlderAck.draft.value, 32, "older acknowledgement must not overwrite newer draft");
  const secondQueued = mainAdapter.pending.shift();
  assert.equal(secondQueued.value.value, 32);
  secondQueued.resolve();
  await queuedSave;
  const queuedDone = snapshot(main);
  assert.equal(queuedDone.confirmed.value, 32);
  assert.equal(queuedDone.draft.value, 32);
  assert.equal(queuedDone.dirty, false);

  assist.edit({ value: 44 }, false);
  assist.receive({ value: 99 });
  await new Promise((resolve) => setImmediate(resolve));
  const receiveWhileDirty = snapshot(assist);
  assert.equal(receiveWhileDirty.draft.value, 44, "incoming acknowledgement must not replace dirty Assist draft");
  assert.equal(receiveWhileDirty.confirmed.value, 22, "dirty Assist store keeps its confirmed generation");

  main.dispose();
  assist.dispose();
  return { concurrentStart, assistFirst, independentDone, afterOlderAck, queuedDone, receiveWhileDirty, events };
}

const original = await trace(originalFactory);
const current = await trace(createConfigDraft);
assert.deepEqual(current, original, "current config-store ownership/generation trace differs from exact recovered function T");

const result = {
  marker: "LWB317_FINAL_CLOSEOUT_MILESTONE_C_CONFIG_STORE_OWNERSHIP_OK",
  original,
  current,
  source: {
    recoveredIndex: { path: relative(indexPath), sha256: sha(indexSource) },
    recoveredFunction: { name: "T", byteOffset: Buffer.byteLength(indexSource.slice(0, originalNode.start)), byteLength: Buffer.byteLength(originalSource), sha256: sha(originalSource) },
    current: { path: relative(currentPath), sha256: sha(fs.readFileSync(currentPath)) },
  },
  limits: "Exact recovered source/local config-store function T versus current createConfigDraft with inert deferred adapters only; no native config persistence or provider action is invoked.",
};

if (verifyOnly) {
  assert.deepEqual(result, JSON.parse(fs.readFileSync(outputPath, "utf8")), "recorded Milestone C config-store ownership proof is stale");
} else {
  fs.writeFileSync(outputPath, `${JSON.stringify(result, null, 2)}\n`);
}
console.log(JSON.stringify({ marker: result.marker, recoveredFunctionSha256: result.source.recoveredFunction.sha256, verified: verifyOnly }, null, 2));
