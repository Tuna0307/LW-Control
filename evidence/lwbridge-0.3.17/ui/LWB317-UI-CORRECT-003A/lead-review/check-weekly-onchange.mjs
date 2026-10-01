import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import assert from "node:assert/strict";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";
import { createConfigDraft } from "../../../../../src/LWBridge.UI-0.3.17/src/previewConfig.js";
import { initialAutomationDraft } from "../../../../../src/LWBridge.UI-0.3.17/src/previewAutomationContracts.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, "../../../../..");
const ui = path.join(root, "src/LWBridge.UI-0.3.17");
const require = createRequire(path.join(ui, "package.json"));
const { parse } = require("@babel/parser");
const pages = fs.readFileSync(path.join(ui, "src/Pages.jsx"), "utf8");
const configHook = fs.readFileSync(path.join(ui, "src/previewConfigHook.jsx"), "utf8");
const ast = parse(pages, { sourceType: "module", plugins: ["jsx"] });
const callbacks = [];
function visit(node) {
  if (!node || typeof node !== "object") return;
  if (node.type === "JSXOpeningElement" && node.name?.name === "WeeklyQualityPreview") {
    const attribute = node.attributes.find((entry) => entry.name?.name === "onChange");
    const expression = attribute.value.expression;
    callbacks.push({ expression: pages.slice(expression.start, expression.end), line: expression.loc.start.line });
  }
  for (const value of Object.values(node)) {
    if (Array.isArray(value)) value.forEach(visit);
    else if (value && typeof value === "object") visit(value);
  }
}
visit(ast);
if (callbacks.length !== 2) throw Error(`Expected the two weekly UI callbacks; found ${callbacks.length}`);

const configHookAst = parse(configHook, { sourceType: "module", plugins: ["jsx"] });
const errorCallbacks = [];
function visitConfigHook(node) {
  if (!node || typeof node !== "object") return;
  if (node.type === "JSXOpeningElement" && node.name?.name === "button") {
    const attribute = node.attributes.find((entry) => entry.name?.name === "onClick");
    if (attribute?.value?.expression) errorCallbacks.push(configHook.slice(attribute.value.expression.start, attribute.value.expression.end));
  }
  for (const value of Object.values(node)) {
    if (Array.isArray(value)) value.forEach(visitConfigHook);
    else if (value && typeof value === "object") visitConfigHook(value);
  }
}
visitConfigHook(configHookAst);
if (errorCallbacks.length !== 2) throw Error(`Expected Retry and Discard callbacks; found ${errorCallbacks.length}`);
const [retryExpression, discardExpression] = errorCallbacks;

const clone = (value) => structuredClone(value);
const waitFor = async (predicate, label) => {
  for (let attempt = 0; attempt < 100; attempt += 1) {
    if (predicate()) return;
    await new Promise((resolve) => setImmediate(resolve));
  }
  throw Error(`Timed out waiting for ${label}`);
};
const makeWeeklyHandler = (expression, store) => new Function("config", `return (${expression});`)({ store });
const makeConfigErrorHandler = (expression, store) => new Function("config", `return (${expression});`)({ store });

// Invoke the actual callback expressions extracted from Pages.jsx against the
// real config store. No test-authored flush, blur, or debounce wait is added.
const results = [];
for (const [index, callback] of callbacks.entries()) {
  const title = ["Trucks", "Secret Task"][index];
  const initial = initialAutomationDraft(title, "automation-config");
  let server = clone(initial);
  const writes = [];
  const pendingReplies = [];
  const store = createConfigDraft(clone(initial), {
    valid: () => true,
    read: async () => clone(server),
    write: (draft) => new Promise((resolve) => {
      const written = clone(draft);
      writes.push(written);
      pendingReplies.push(() => {
        server = clone(written);
        resolve(clone(written));
      });
    }),
  });
  const onChange = makeWeeklyHandler(callback.expression, store);
  const first = [...initial.weeklyQualities];
  first[0] = first[0] === "ur" ? "ssr" : "ur";
  const firstChange = onChange(first);
  assert.equal(writes.length, 1, `${title} must dispatch immediately`);
  assert.equal(store.getSnapshot().saving, true, `${title} must enter saving immediately`);
  assert.deepEqual(store.getSnapshot().draft.weeklyQualities, first);

  const second = [...first];
  second[2] = second[2] === "none" ? "ssr" : "none";
  const secondChange = onChange(second);
  assert.equal(writes.length, 1, `${title} second edit must queue behind the unfinished write`);
  assert.equal(store.getSnapshot().saving, true, `${title} selector path remains editable while saving`);
  assert.deepEqual(store.getSnapshot().draft.weeklyQualities, second);

  pendingReplies.shift()();
  await waitFor(() => writes.length === 2 && pendingReplies.length === 1, `${title} queued second write`);
  assert.deepEqual(store.getSnapshot().confirmed.weeklyQualities, first, `${title} older acknowledgement updates confirmed only`);
  assert.deepEqual(store.getSnapshot().draft.weeklyQualities, second, `${title} older acknowledgement must not overwrite the newer edit`);
  assert.equal(store.getSnapshot().saving, true);

  pendingReplies.shift()();
  await Promise.all([firstChange, secondChange]);
  assert.equal(store.getSnapshot().saving, false);
  assert.equal(store.getSnapshot().dirty, false);
  assert.deepEqual(store.getSnapshot().confirmed.weeklyQualities, second);

  results.push({
    title,
    line: callback.line,
    callback: callback.expression,
    immediateWrites: 1,
    writes: writes.map((entry) => entry.weeklyQualities),
    secondEditWhileSaving: true,
    olderAckPreservedNewerDraft: true,
    finalConfirmed: store.getSnapshot().confirmed.weeklyQualities,
  });
  store.dispose();
}

const recovery = [];
{
  const title = "Trucks";
  const initial = initialAutomationDraft(title, "automation-config");
  let server = clone(initial);
  let failFirst = true;
  const store = createConfigDraft(clone(initial), {
    valid: () => true,
    read: async () => clone(server),
    write: async (draft) => {
      if (failFirst) {
        failFirst = false;
        throw Error("fixture weekly save failed");
      }
      server = clone(draft);
      return clone(server);
    },
  });
  const next = [...initial.weeklyQualities];
  next[0] = next[0] === "ur" ? "ssr" : "ur";
  const onChange = makeWeeklyHandler(callbacks[0].expression, store);
  await onChange(next);
  assert.equal(store.getSnapshot().dirty, true);
  assert.match(store.getSnapshot().error?.message ?? "", /fixture weekly save failed/);
  const retry = makeConfigErrorHandler(retryExpression, store);
  await retry();
  assert.equal(store.getSnapshot().dirty, false);
  assert.equal(store.getSnapshot().error, null);
  assert.deepEqual(store.getSnapshot().confirmed.weeklyQualities, next);
  recovery.push({ title, failedFromActualWeeklyHandler: true, action: "Retry", recovered: true });
  store.dispose();
}
{
  const title = "Secret Task";
  const initial = initialAutomationDraft(title, "automation-config");
  const store = createConfigDraft(clone(initial), {
    valid: () => true,
    read: async () => clone(initial),
    write: async () => { throw Error("fixture weekly save failed"); },
  });
  const next = [...initial.weeklyQualities];
  next[0] = next[0] === "none" ? "ssr" : "none";
  const onChange = makeWeeklyHandler(callbacks[1].expression, store);
  await onChange(next);
  assert.equal(store.getSnapshot().dirty, true);
  assert.match(store.getSnapshot().error?.message ?? "", /fixture weekly save failed/);
  const discard = makeConfigErrorHandler(discardExpression, store);
  await discard();
  assert.equal(store.getSnapshot().dirty, false);
  assert.equal(store.getSnapshot().error, null);
  assert.deepEqual(store.getSnapshot().draft.weeklyQualities, initial.weeklyQualities);
  recovery.push({ title, failedFromActualWeeklyHandler: true, action: "Discard", recovered: true });
  store.dispose();
}

console.log(JSON.stringify({
  sourceSha256: crypto.createHash("sha256").update(pages).digest("hex"),
  configHookSha256: crypto.createHash("sha256").update(configHook).digest("hex"),
  results,
  recovery,
}, null, 2));
