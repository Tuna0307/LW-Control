import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { createAppHarness } from "./app-harness.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../..");
const appSource = fs.readFileSync(path.join(repo, "src/LWBridge.UI-0.3.17/src/App.jsx"), "utf8");

const p1 = {
  enabled: false,
  intervalMinutes: 30,
  serverIds: [8, 15],
  selectedTypes: ["truck", "treasure"],
  scanMode: "normal",
  returnToOriginalServer: false,
  nextRunAt: 0,
};
const p2 = {
  enabled: true,
  intervalMinutes: 90,
  serverIds: [120],
  selectedTypes: ["city"],
  scanMode: "fast",
  returnToOriginalServer: true,
  nextRunAt: 1_800_000_100_000,
};
const initialStorage = {
  "lwbridge.mapAutoScan.profile-a": JSON.stringify(p1),
  "lwbridge.mapAutoScan.profile-b": JSON.stringify(p2),
};

const results = [];
function pass(name, detail) {
  results.push({ name, status: "PASS", detail });
}
const autoWrites = (harness) => harness.storageWrites.filter(([key]) => key.startsWith("lwbridge.mapAutoScan."));

const mounted = await createAppHarness(appSource, "auto-profile-mounted", {
  profileId: "profile-a",
  storage: initialStorage,
  now: 1_800_000_000_000,
});
await mounted.mount();
assert.deepEqual(mounted.pageProps().autoScanConfig, p1);
assert.equal(autoWrites(mounted).length, 0, "mount/load must not persist Auto config");
pass("initial-profile-load-no-write", { profileId: "profile-a", autoWrites: autoWrites(mounted).length });

let props = mounted.pageProps();
props.onAutoScanConfig({ ...props.autoScanConfig, intervalMinutes: 19 });
await mounted.settle();
assert.equal(mounted.pageProps().autoScanConfig.intervalMinutes, 20);
assert.equal(autoWrites(mounted).length, 1);
assert.equal(autoWrites(mounted)[0][0], "lwbridge.mapAutoScan.profile-a");
assert.equal(JSON.parse(autoWrites(mounted)[0][1]).intervalMinutes, 20);
pass("user-edit-normalizes-and-writes-active-profile", { key: autoWrites(mounted)[0][0], storedInterval: 20 });

const writesBeforeProfileB = autoWrites(mounted).length;
await mounted.setProfileId("profile-b");
assert.deepEqual(mounted.pageProps().autoScanConfig, p2);
assert.equal(autoWrites(mounted).length, writesBeforeProfileB, "profile switch load must not write previous config into new key");
assert.deepEqual(JSON.parse(mounted.storage.get("lwbridge.mapAutoScan.profile-b")), p2);
pass("same-mount-profile-switch-loads-without-leak", { from: "profile-a", to: "profile-b", addedWrites: 0 });

props = mounted.pageProps();
props.onAutoScanConfig({ ...props.autoScanConfig, returnToOriginalServer: false });
await mounted.settle();
assert.equal(autoWrites(mounted).at(-1)[0], "lwbridge.mapAutoScan.profile-b");
assert.equal(JSON.parse(autoWrites(mounted).at(-1)[1]).returnToOriginalServer, false);
pass("post-switch-edit-writes-new-profile-only", { key: autoWrites(mounted).at(-1)[0] });

const writesBeforeReturn = autoWrites(mounted).length;
await mounted.setProfileId("profile-a");
assert.equal(mounted.pageProps().autoScanConfig.intervalMinutes, 20, "returning to profile-a reloads its persisted edit");
assert.equal(autoWrites(mounted).length, writesBeforeReturn);
pass("switch-back-reloads-own-persisted-config", { profileId: "profile-a", intervalMinutes: 20, addedWrites: 0 });

const storageAfterMounted = Object.fromEntries(mounted.storage);
await mounted.unmount();
const remounted = await createAppHarness(appSource, "auto-profile-remount", {
  profileId: "profile-b",
  storage: storageAfterMounted,
  now: 1_800_000_200_000,
});
await remounted.mount();
assert.equal(remounted.pageProps().autoScanConfig.returnToOriginalServer, false);
assert.equal(autoWrites(remounted).length, 0, "StrictMode-style remount must not write Auto config before a user edit");
pass("remount-load-no-prior-profile-write", { profileId: "profile-b", writes: 0 });
await remounted.unmount();

assert.match(appSource, /useLayoutEffect\(/);
assert.match(appSource, /loadAutoScanConfig\(profileId, window\.localStorage\)/);
assert.match(appSource, /saveAutoScanConfig\(selectedProfileId, next, window\.localStorage\)/);
pass("production-source-ownership-boundary", { layoutLoad: true, selectedProfileSave: true });

const report = {
  status: "PASS",
  scope: "Actual production App body executed in the persistent hook adapter. Profile changes rerender the same mount; then a fresh mount replays stored data. No native/gameplay calls are used for Auto config.",
  cases: results,
};
fs.writeFileSync(path.join(here, "profile-ownership-results.json"), `${JSON.stringify(report, null, 2)}\n`);
console.log(`PASS ${results.length}/${results.length} profile ownership cases`);
