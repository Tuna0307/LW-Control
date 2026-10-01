import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { createConfigDraft } from "../../../../src/LWBridge.UI-0.3.17/src/previewConfig.js";
import { automationDraftError, initialAutomationDraft } from "../../../../src/LWBridge.UI-0.3.17/src/previewAutomationContracts.js";
import { dispatchWeeklyQualities, railwayWeeklyQualities } from "../../../../src/LWBridge.UI-0.3.17/src/previewAutomationFixtures.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../..");
const panelPath = path.resolve(here, "../frontend-package/web/assets/AutomationPanel-BJ0gIqFh.js");
const cardPath = path.resolve(here, "../frontend-package/web/assets/AutomationCard-LCx_jIi7.js");
const pagesPath = path.join(repo, "src/LWBridge.UI-0.3.17/src/Pages.jsx");

const sha256 = (file) => crypto.createHash("sha256").update(fs.readFileSync(file)).digest("hex").toUpperCase();
const panel = fs.readFileSync(panelPath, "utf8");
const card = fs.readFileSync(cardPath, "utf8");
const pages = fs.readFileSync(pagesPath, "utf8");
const clone = (value) => structuredClone(value);
const byteOffset = (text, needle) => {
  const index = text.indexOf(needle);
  return index < 0 ? -1 : Buffer.byteLength(text.slice(0, index), "utf8");
};
const waitFor = async (predicate, label) => {
  for (let i = 0; i < 100; i += 1) {
    if (predicate()) return;
    await new Promise((resolve) => setImmediate(resolve));
  }
  throw new Error(`timed out waiting for ${label}`);
};

assert.equal(sha256(panelPath), "6DB2328191AF33E5631D507271C36D7008B287DAA7C8CD1A957B23EF6F871725");
assert.equal(sha256(cardPath), "24ED773623C237F8219EFF5443FAC3BA7B55B6E99E168E52FF066FAF2ABE7A61");
assert.equal(byteOffset(panel, "ge=[`ssr`,`ur`,`ssr`,`ssr`,`ssr`,`ur`,`ssr`]"), 18848);
assert.equal(byteOffset(panel, "_e=[`none`,`ur`,`none`,`none`,`none`,`ur`,`none`]"), 18893);
assert.equal(byteOffset(panel, "Cr=F?.railway?.weeklyQualities??ge,wr=F?.dispatch?.weeklyQualities??_e"), 32189);
assert.equal(byteOffset(panel, "$=e=>!l||_===e"), 32571);
assert.equal(byteOffset(panel, "Rr=Array.from({length:7}"), 32702);
assert.equal(byteOffset(panel, "disabled:$(e)||n"), 33030);
assert.equal(byteOffset(panel, "r[i]=n.target.value,W(e,{weeklyQualities:r})"), 33073);
assert.equal(byteOffset(panel, "async function W(e,t){P[e].edit(e=>({...e,...t}),!1),await P[e].flush().catch(()=>void 0)}"), 27880);
assert.match(card, /M=!l\|\|!!d/);

assert.deepEqual(railwayWeeklyQualities, ["ssr", "ur", "ssr", "ssr", "ssr", "ur", "ssr"]);
assert.deepEqual(dispatchWeeklyQualities, ["none", "ur", "none", "none", "none", "ur", "none"]);
assert.equal(initialAutomationDraft("Trucks", "automation-weekly-save-error").enabled, false);
assert.equal(initialAutomationDraft("Secret Task", "automation-secret-weekly-save-error").enabled, false);
assert.equal(automationDraftError("Trucks", initialAutomationDraft("Trucks", "automation-config")), "");
assert.equal(automationDraftError("Trucks", { ...initialAutomationDraft("Trucks", "automation-config"), weeklyQualities: ["ur"] }), "configSave.failed");
assert.equal(automationDraftError("Secret Task", { ...initialAutomationDraft("Secret Task", "automation-config"), weeklyQualities: ["none", "ur", "none", "bad", "none", "ur", "none"] }), "configSave.failed");

const days = Array.from({ length: 7 }, (_, index) => new Intl.DateTimeFormat("en", { weekday: "short", timeZone: "UTC" }).format(new Date(Date.UTC(2024, 0, index + 1))));
assert.deepEqual(days, ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"]);
assert.match(pages, /disabled=\{!enabled \|\| running\}/);
assert.doesNotMatch(pages, /WeeklyQualityPreview[^\n]*saving=\{config\.saving\}/);

const writes = [];
const pending = [];
let server = { weeklyQualities: clone(railwayWeeklyQualities) };
const deferred = createConfigDraft(server, {
  valid: (value) => automationDraftError("Trucks", { ...initialAutomationDraft("Trucks", "automation-config"), ...value }) === "",
  read: async () => clone(server),
  write: (value) => new Promise((resolve) => {
    const written = clone(value);
    writes.push(written);
    pending.push(() => {
      server = clone(written);
      resolve(clone(written));
    });
  }),
});

deferred.edit((draft) => {
  const weeklyQualities = [...draft.weeklyQualities];
  weeklyQualities[0] = "ur";
  return { ...draft, weeklyQualities };
}, false);
const firstSave = deferred.flush(false);
await waitFor(() => pending.length === 1, "first deferred weekly write");
assert.equal(deferred.getSnapshot().saving, true);
assert.deepEqual(deferred.getSnapshot().draft.weeklyQualities, ["ur", "ur", "ssr", "ssr", "ssr", "ur", "ssr"]);

deferred.edit((draft) => {
  const weeklyQualities = [...draft.weeklyQualities];
  weeklyQualities[2] = "none";
  return { ...draft, weeklyQualities };
}, false);
deferred.flush(false).catch(() => {});
assert.equal(deferred.getSnapshot().saving, true);
assert.deepEqual(deferred.getSnapshot().draft.weeklyQualities, ["ur", "ur", "none", "ssr", "ssr", "ur", "ssr"]);
pending.shift()();
await waitFor(() => pending.length === 1 && writes.length === 2, "queued weekly write");
pending.shift()();
await firstSave;
assert.equal(deferred.getSnapshot().saving, false);
assert.deepEqual(deferred.getSnapshot().confirmed.weeklyQualities, ["ur", "ur", "none", "ssr", "ssr", "ur", "ssr"]);
deferred.dispose();

let failOnce = true;
server = { weeklyQualities: clone(railwayWeeklyQualities) };
const retry = createConfigDraft(server, {
  valid: () => true,
  read: async () => clone(server),
  write: async (value) => {
    if (failOnce) {
      failOnce = false;
      throw new Error("fixture weekly save failed");
    }
    server = clone(value);
    return clone(value);
  },
});
retry.edit((draft) => ({ ...draft, weeklyQualities: ["ur", ...draft.weeklyQualities.slice(1)] }), false);
await assert.rejects(retry.flush(), /fixture weekly save failed/);
assert.equal(retry.getSnapshot().dirty, true);
assert.equal(retry.getSnapshot().error?.message, "fixture weekly save failed");
assert.equal(retry.getSnapshot().draft.weeklyQualities[0], "ur");
await retry.flush();
assert.equal(retry.getSnapshot().dirty, false);
assert.equal(retry.getSnapshot().confirmed.weeklyQualities[0], "ur");
retry.dispose();

server = { weeklyQualities: clone(dispatchWeeklyQualities) };
const discard = createConfigDraft(server, {
  valid: () => true,
  read: async () => clone(server),
  write: async () => {
    throw new Error("fixture weekly save failed");
  },
});
discard.edit((draft) => ({ ...draft, weeklyQualities: ["ssr", ...draft.weeklyQualities.slice(1)] }), false);
await assert.rejects(discard.flush(), /fixture weekly save failed/);
assert.equal(discard.getSnapshot().draft.weeklyQualities[0], "ssr");
await discard.refresh(true);
assert.equal(discard.getSnapshot().dirty, false);
assert.equal(discard.getSnapshot().error, null);
assert.deepEqual(discard.getSnapshot().draft.weeklyQualities, dispatchWeeklyQualities);
discard.dispose();

console.log("LWB317_UI_CORRECT003A_WEEKLY_OK", {
  days,
  railwayWeeklyQualities,
  dispatchWeeklyQualities,
  deferredWrites: writes.map((entry) => entry.weeklyQualities),
});
