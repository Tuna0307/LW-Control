import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import {
  loadIndex,
  loadPanel,
  rawOf,
  walkAll,
} from "../../LWB317-UI-MAP-INTERACTIONS-001/original/lib.mjs";
import {
  createOriginalHarness,
  treeNodes,
  nodeText,
} from "../../LWB317-UI-MAP-INTERACTIONS-001/original/original-runtime.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const index = loadIndex();
const panel = loadPanel();
const rawIndex = (node) => rawOf(index.entry, node);
const rawPanel = (node) => rawOf(panel.entry, node);

function all(root) {
  const out = [];
  walkAll(root, (node, parents) => out.push({ node, parents }));
  return out;
}

function one(label, rows, predicate) {
  const found = rows.filter(({ node, parents }) => predicate(node, parents));
  assert.equal(found.length, 1, `${label}: expected one match, got ${found.length}`);
  return found[0].node;
}

const gi = index.functions.Gi;
const giNodes = all(gi);
const parentCallbackNode = one("Ct", giNodes, (node) => node.type === "FunctionDeclaration" && node.id?.name === "Ct");
const profileLoadEffectNode = one("profile Auto layout effect", giNodes, (node) =>
  node.type === "CallExpression"
  && rawIndex(node.callee).includes("useLayoutEffect")
  && rawIndex(node).includes("Ai(r.selectedProfileId)"));
const profileLoadCallbackSource = rawIndex(profileLoadEffectNode.arguments[0]);

function helperRuntime(initialStorage = {}) {
  const storage = new Map(Object.entries(initialStorage).map(([key, value]) => [key, String(value)]));
  const writes = [];
  const localStorage = {
    getItem: (key) => storage.has(key) ? storage.get(key) : null,
    setItem: (key, value) => {
      storage.set(key, String(value));
      writes.push([key, String(value)]);
    },
  };
  const helperNames = ["Ci", "wi", "Ti", "Ei", "Di", "Oi", "ki", "Ai", "ji"];
  const body = [
    `var xi=${rawIndex(index.variables.xi.declarator.init)};`,
    `var Si=${rawIndex(index.variables.Si.declarator.init)};`,
    ...helperNames.map((name) => rawIndex(index.functions[name])),
    `return {xi,Si,${helperNames.join(",")}};`,
  ].join("\n");
  const helpers = new Function("localStorage", body)(localStorage);
  return { ...helpers, storage, writes, localStorage };
}

function parentRuntime(runtime, { profileId = "p1", initial = runtime.Ei(null), now = 1_800_000_000_000 } = {}) {
  const Ce = { current: initial };
  const r = { selectedProfileId: profileId };
  const stateUpdates = [];
  let state = initial;
  const ye = (next) => { state = next; stateUpdates.push(next); };
  const fakeDate = { now: () => now };
  const Ct = new Function("Ce", "ye", "r", "Ei", "ji", "Date", `${rawIndex(parentCallbackNode)}\nreturn Ct;`)(
    Ce, ye, r, runtime.Ei, runtime.ji, fakeDate,
  );
  const loadProfile = new Function("Ai", "r", "Ce", "ye", `return (${profileLoadCallbackSource});`)(runtime.Ai, r, Ce, ye);
  return {
    apply: (next) => Ct(next),
    load: () => loadProfile(),
    get: () => state,
    ref: Ce,
    profile: r,
    stateUpdates,
    setNow: (value) => { now = value; },
  };
}

function findMaster(h) {
  const node = h.findNodes((entry) => entry.type === "label" && entry.props?.className === "map-auto-scan-master")[0];
  assert.ok(node, "master label");
  return node;
}

function findTargetInput(h) {
  const node = h.findNodes((entry) => entry.type === "input" && entry.props?.inputMode === "numeric")[0];
  assert.ok(node, "target server input");
  return node;
}

function findIntervalInput(h) {
  const node = h.findNodes((entry) => entry.type === "input" && entry.props?.type === "number" && entry.props?.min === 20 && entry.props?.max === "1440")[0];
  assert.ok(node, "interval input");
  return node;
}

function findLabelByText(h, text) {
  const node = h.findNodes((entry) => entry.type === "label" && nodeText(entry).includes(text))[0];
  assert.ok(node, `label ${text}`);
  return node;
}

function lastAutoConfigCall(h) {
  return h.propCalls.filter((call) => call.name === "onAutoScanConfig").at(-1);
}

async function switchMode(h, key) {
  const button = h.findButton(key === "auto" ? "map.autoScan" : "map.manualScan");
  assert.ok(button, `${key} mode button`);
  button.props.onClick();
  await h.settle();
}

const cases = [];
async function runCase(name, fn) {
  const detail = await fn();
  cases.push({ name, status: "PASS", detail });
}

await runCase("defaults-and-normalization", async () => {
  const h = helperRuntime();
  const defaults = {
    enabled: false,
    intervalMinutes: 60,
    serverIds: [],
    selectedTypes: ["truck", "railway", "dispatch", "ghost", "treasure"],
    scanMode: "fast",
    returnToOriginalServer: true,
    nextRunAt: 0,
  };
  assert.deepEqual(h.Si, defaults);
  assert.deepEqual(h.Ei(null), defaults);
  assert.equal(h.Ei({ intervalMinutes: 19 }).intervalMinutes, 20);
  assert.equal(h.Ei({ intervalMinutes: 20.9 }).intervalMinutes, 20);
  assert.equal(h.Ei({ intervalMinutes: 1440.9 }).intervalMinutes, 1440);
  assert.equal(h.Ei({ intervalMinutes: 5000 }).intervalMinutes, 1440);
  assert.equal(Number.isNaN(h.Ei({ intervalMinutes: "bad" }).intervalMinutes), true);
  assert.deepEqual(h.Ei({ selectedTypes: ["ghost", "ghost", "bad", "city"] }).selectedTypes, ["ghost", "city"]);
  assert.deepEqual(h.Ei({ selectedTypes: [] }).selectedTypes, defaults.selectedTypes);
  assert.equal(h.Ei({ scanMode: "weird" }).scanMode, "fast");
  assert.equal(h.Ei({ returnToOriginalServer: 0 }).returnToOriginalServer, true);
  assert.equal(h.Ei({ returnToOriginalServer: false }).returnToOriginalServer, false);
  assert.equal(h.Ei({ enabled: 1 }).enabled, false);
  assert.equal(h.Ei({ enabled: true }).enabled, true);
  assert.equal(h.Ei({ nextRunAt: -10 }).nextRunAt, 0);
  return { defaults, malformedIntervalRemainsNaNUntilSerialized: true };
});

await runCase("server-parser-add-remove-contract", async () => {
  const h = helperRuntime();
  const parsed = h.Ci(" 8,15，120;121；122\n123 8 nope 0 100000 1e2 0x10 1.0 1.5 ");
  assert.deepEqual(parsed, [8, 15, 120, 121, 122, 123, 100, 16, 1]);
  assert.deepEqual(h.wi([2, 1], "1;3，4 2"), [2, 1, 3, 4]);
  assert.deepEqual(h.Ti([1, 2, 1, 3], 1), [2, 3]);
  assert.deepEqual(h.Ci(Array.from({ length: 25 }, (_, index) => index + 1).join(",")), Array.from({ length: 20 }, (_, index) => index + 1));
  assert.deepEqual(h.Di([], 321), [321]);
  assert.deepEqual(h.Di([], 0), []);
  assert.deepEqual(h.Di([9, 8], 321), [9, 8]);
  return { parsed, separators: ["space", "comma", "fullwidth comma", "semicolon", "fullwidth semicolon"], maxServers: 20 };
});

await runCase("profile-scoped-load-malformed-and-change", async () => {
  const p1 = { enabled: true, intervalMinutes: 35, serverIds: [9, 8, 9], selectedTypes: ["city"], scanMode: "normal", returnToOriginalServer: false, nextRunAt: 1234 };
  const p2 = { enabled: false, intervalMinutes: 90, serverIds: [77], selectedTypes: ["truck", "ghost"], scanMode: "fast", returnToOriginalServer: true, nextRunAt: 0 };
  const h = helperRuntime({
    "lwbridge.mapAutoScan.p1": JSON.stringify(p1),
    "lwbridge.mapAutoScan.p2": JSON.stringify(p2),
    "lwbridge.mapAutoScan.bad-json": "{oops",
    "lwbridge.mapAutoScan.bad-servers": JSON.stringify({ serverIds: "9,8", enabled: true }),
    "lwbridge.mapAutoScan.bad-types": JSON.stringify({ selectedTypes: "city", enabled: true }),
    "lwbridge.mapAutoScan.bad-interval": JSON.stringify({ intervalMinutes: "oops", enabled: true }),
  });
  assert.deepEqual(h.Ai("missing"), h.Ei(null));
  assert.deepEqual(h.Ai("bad-json"), h.Ei(null));
  assert.deepEqual(h.Ai("bad-servers"), h.Ei(null));
  assert.deepEqual(h.Ai("bad-types"), h.Ei(null));
  assert.equal(Number.isNaN(h.Ai("bad-interval").intervalMinutes), true);
  const parent = parentRuntime(h, { profileId: "p1" });
  parent.load();
  assert.deepEqual(parent.get(), h.Ei(p1));
  parent.profile.selectedProfileId = "p2";
  parent.load();
  assert.deepEqual(parent.get(), h.Ei(p2));
  assert.equal(h.writes.length, 0, "profile load itself does not persist");
  return { keys: ["lwbridge.mapAutoScan.p1", "lwbridge.mapAutoScan.p2"], loadWrites: h.writes.length };
});

await runCase("parent-normalizes-user-edits-and-deadlines", async () => {
  const h = helperRuntime();
  const parent = parentRuntime(h, { profileId: "profile-A", now: 1_800_000_000_111 });
  parent.apply({ ...h.Si, enabled: true, intervalMinutes: 19, nextRunAt: 777 });
  assert.equal(parent.get().enabled, true);
  assert.equal(parent.get().intervalMinutes, 20);
  assert.equal(parent.get().nextRunAt, 1_800_000_000_111);
  parent.apply({ ...parent.get(), intervalMinutes: 2000, nextRunAt: 12345 });
  assert.equal(parent.get().intervalMinutes, 1440);
  assert.equal(parent.get().nextRunAt, 12345, "enabled-to-enabled edit keeps supplied deadline");
  parent.apply({ ...parent.get(), enabled: false, nextRunAt: 99999 });
  assert.equal(parent.get().nextRunAt, 0);
  parent.setNow(1_800_000_000_222);
  parent.apply({ ...parent.get(), enabled: true, nextRunAt: 555 });
  assert.equal(parent.get().nextRunAt, 1_800_000_000_222);
  assert.equal(h.writes.at(-1)[0], "lwbridge.mapAutoScan.profile-A");
  assert.deepEqual(JSON.parse(h.writes.at(-1)[1]), parent.get());
  return { writes: h.writes.length, final: parent.get() };
});

await runCase("manual-auto-switch-preserves-shared-header-and-draft", async () => {
  const now = 1_800_000_100_000;
  const h = await createOriginalHarness({
    label: "auto-switch",
    now,
    online: true,
    serverId: 321,
    scanState: { isReading: true, startedAt: now - 5_000, progressPercent: 20 },
  });
  await h.mount();
  assert.equal(h.getState("scanModeTab"), "manual");
  assert.ok(nodeText(h.tree()).includes("map.startTime"));
  assert.equal(h.findNodes((entry) => entry.type === "div" && entry.props?.className === "map-scan-summary").length, 1);
  const before = h.propCalls.filter((call) => call.name === "onAutoScanConfig").length;
  await switchMode(h, "auto");
  assert.equal(h.getState("scanModeTab"), "auto");
  assert.ok(nodeText(h.tree()).includes("map.startTime"));
  assert.equal(h.findNodes((entry) => entry.type === "div" && entry.props?.className === "map-scan-summary").length, 1);
  const input = findTargetInput(h);
  input.props.onChange({ target: { value: "9;10" } });
  await h.settle();
  await switchMode(h, "manual");
  await switchMode(h, "auto");
  assert.equal(findTargetInput(h).props.value, "9;10");
  assert.equal(h.propCalls.filter((call) => call.name === "onAutoScanConfig").length, before);
  return { defaultMode: "manual", autoDraftAfterRoundTrip: "9;10", sharedTimingAndSummary: true };
});

await runCase("master-status-and-running-editability", async () => {
  const h = await createOriginalHarness({ label: "auto-master", online: true, serverId: 321 });
  await h.mount();
  await switchMode(h, "auto");
  await h.setProps({ autoScanConfig: { ...h.props.autoScanConfig, enabled: false }, autoScanRunning: false });
  let master = findMaster(h);
  assert.ok(nodeText(master).includes("map.autoScanDisabled"));
  await h.setProps({ autoScanConfig: { ...h.props.autoScanConfig, enabled: true }, autoScanRunning: false });
  master = findMaster(h);
  assert.ok(nodeText(master).includes("map.autoScanWaiting"));
  await h.setProps({ autoScanConfig: { ...h.props.autoScanConfig, enabled: false }, autoScanRunning: true });
  master = findMaster(h);
  assert.ok(nodeText(master).includes("map.autoScanRunning"));
  assert.equal(master.props.children[0].props.disabled, undefined);
  assert.equal(findTargetInput(h).props.disabled, undefined);
  assert.equal(findIntervalInput(h).props.disabled, undefined);
  const speedLabel = findLabelByText(h, "map.speed");
  assert.equal(speedLabel.props.children[1].props.disabled, undefined);
  const returnLabel = findLabelByText(h, "map.returnAfterAutoScan");
  assert.equal(returnLabel.props.children[0].props.disabled, undefined);
  return { precedence: ["running", "waiting when enabled", "disabled"], configFieldsDisabledByAutoRunning: false };
});

await runCase("target-server-ui-add-enter-chips-placeholder", async () => {
  const h = await createOriginalHarness({ label: "auto-server-ui", online: true, serverId: 321 });
  await h.mount();
  await switchMode(h, "auto");
  let input = findTargetInput(h);
  assert.equal(input.props.placeholder, "321");
  input.props.onChange({ target: { value: "bad;0;100000" } });
  await h.settle();
  let prevented = false;
  input = findTargetInput(h);
  input.props.onKeyDown({ key: "Enter", preventDefault: () => { prevented = true; } });
  await h.settle();
  assert.equal(prevented, true);
  assert.equal(h.getState("autoScanServerInput"), "bad;0;100000");
  assert.equal(lastAutoConfigCall(h), undefined);
  input = findTargetInput(h);
  input.props.onChange({ target: { value: "9；8 9,7" } });
  await h.settle();
  findTargetInput(h).props.onKeyDown({ key: "Enter", preventDefault() {} });
  await h.settle();
  assert.deepEqual(h.props.autoScanConfig.serverIds, [9, 8, 7]);
  assert.equal(h.getState("autoScanServerInput"), "");
  const chips = h.findNodes((entry) => entry.type === "span" && entry.props?.className === "map-auto-scan-server-chips")[0];
  assert.ok(chips);
  assert.deepEqual(chips.props.children.map((chip) => chip.props.children[0]), [9, 8, 7]);
  chips.props.children[1].props.children[1].props.onClick();
  await h.settle();
  assert.deepEqual(h.props.autoScanConfig.serverIds, [9, 7]);
  const zero = await createOriginalHarness({ label: "auto-server-placeholder-zero", serverId: 0 });
  await zero.mount();
  await switchMode(zero, "auto");
  assert.equal(findTargetInput(zero).props.placeholder, "8, 15, 120");
  return { currentServerPlaceholder: "321", fallbackPlaceholder: "8, 15, 120", added: [9, 8, 7], afterRemove: [9, 7] };
});

await runCase("target-server-ui-valid-input-at-cap-clears", async () => {
  const servers = Array.from({ length: 20 }, (_, index) => index + 1);
  const h = await createOriginalHarness({
    label: "auto-server-cap",
    serverId: 321,
    autoScanConfig: { ...helperRuntime().Si, serverIds: servers },
  });
  await h.mount();
  await switchMode(h, "auto");
  findTargetInput(h).props.onChange({ target: { value: "21" } });
  await h.settle();
  const add = h.findButton("common.add");
  assert.equal(add.props.disabled, false);
  add.props.onClick();
  await h.settle();
  assert.deepEqual(h.props.autoScanConfig.serverIds, servers);
  assert.equal(h.getState("autoScanServerInput"), "");
  return { count: h.props.autoScanConfig.serverIds.length, inputCleared: true };
});

await runCase("panel-raw-interval-parent-normalized-interval", async () => {
  const h = await createOriginalHarness({ label: "auto-interval", serverId: 321 });
  await h.mount();
  await switchMode(h, "auto");
  findIntervalInput(h).props.onChange({ target: { value: "19" } });
  await h.settle();
  const rawEdit = lastAutoConfigCall(h).args[0];
  assert.equal(rawEdit.intervalMinutes, 19);
  const helpers = helperRuntime();
  const parent = parentRuntime(helpers, { profileId: "p", initial: helpers.Si });
  parent.apply(rawEdit);
  assert.equal(parent.get().intervalMinutes, 20);
  findIntervalInput(h).props.onChange({ target: { value: "" } });
  await h.settle();
  assert.equal(lastAutoConfigCall(h).args[0].intervalMinutes, 0);
  parent.apply(lastAutoConfigCall(h).args[0]);
  assert.equal(parent.get().intervalMinutes, 20);
  return { panelEmits: [19, 0], parentStores: 20 };
});

await runCase("scan-types-default-order-and-last-selection", async () => {
  const helpers = helperRuntime();
  const h = await createOriginalHarness({ label: "auto-types", serverId: 321, autoScanConfig: helpers.Si });
  await h.mount();
  await switchMode(h, "auto");
  const labels = treeNodes(h.tree()).filter((entry) => entry.type === "label" && Array.isArray(entry.props?.children)
    && entry.props.children[0]?.type === "input" && entry.props.children[0]?.props?.type === "checkbox"
    && String(nodeText(entry)).startsWith("map."));
  const scanLabels = labels.filter((entry) => [
    "map.playerCity", "map.resourcePoint", "map.monster", "map.truck", "map.allianceTrain", "map.secretTask", "map.ghostScout", "map.treasure",
  ].includes(nodeText(entry)));
  assert.equal(scanLabels.length, 8);
  assert.deepEqual(h.props.autoScanConfig.selectedTypes, ["truck", "railway", "dispatch", "ghost", "treasure"]);
  await h.setProps({ autoScanConfig: { ...h.props.autoScanConfig, selectedTypes: ["city"] } });
  const city = treeNodes(h.tree()).find((entry) => entry.type === "label" && nodeText(entry) === "map.playerCity");
  assert.equal(city.props.children[0].props.disabled, true);
  const resource = treeNodes(h.tree()).find((entry) => entry.type === "label" && nodeText(entry) === "map.resourcePoint");
  resource.props.children[0].props.onChange({ target: { checked: true } });
  await h.settle();
  assert.deepEqual(h.props.autoScanConfig.selectedTypes, ["city", "resource"]);
  const city2 = treeNodes(h.tree()).find((entry) => entry.type === "label" && nodeText(entry) === "map.playerCity");
  city2.props.children[0].props.onChange({ target: { checked: false } });
  await h.settle();
  assert.deepEqual(h.props.autoScanConfig.selectedTypes, ["resource"]);
  return { renderedKinds: 8, defaults: helpers.Si.selectedTypes, final: ["resource"] };
});

await runCase("speed-return-run-now-and-next-run", async () => {
  const helpers = helperRuntime();
  const now = 1_800_000_321_000;
  const h = await createOriginalHarness({ label: "auto-options", online: true, serverId: 321, now, autoScanConfig: { ...helpers.Si, enabled: true, nextRunAt: 1_234_567_890 } });
  await h.mount();
  await switchMode(h, "auto");
  const speedLabel = findLabelByText(h, "map.speed");
  speedLabel.props.children[1].props.onChange({ target: { value: "unexpected" } });
  await h.settle();
  assert.equal(h.props.autoScanConfig.scanMode, "normal");
  const returnLabel = findLabelByText(h, "map.returnAfterAutoScan");
  returnLabel.props.children[0].props.onChange({ target: { checked: false } });
  await h.settle();
  assert.equal(h.props.autoScanConfig.returnToOriginalServer, false);
  let runNow = h.findButton("map.runAutoScanNow");
  assert.equal(runNow.props.disabled, false);
  runNow.props.onClick();
  await h.settle();
  assert.equal(h.props.autoScanConfig.nextRunAt, now);
  const formatN = new Function(`${rawPanel(panel.topLevel.N)}\nreturn N;`)();
  const secondsText = formatN(1_234_567_890, "en");
  assert.equal(secondsText, new Date(1_234_567_890_000).toLocaleString("en"));
  assert.ok(nodeText(h.tree()).includes(formatN(now, "en")));
  await h.setProps({ autoScanConfig: { ...h.props.autoScanConfig, enabled: false, nextRunAt: now } });
  const nextLine = h.findNodes((entry) => entry.type === "small" && nodeText(entry).startsWith("map.nextAutoScan"))[0];
  assert.equal(nodeText(nextLine), "map.nextAutoScan: -");
  runNow = h.findButton("map.runAutoScanNow");
  assert.equal(runNow.props.disabled, true);
  return { invalidSpeedUiMapsTo: "normal", returnToggle: false, runNowTimestamp: now, secondsTimestampFormatting: secondsText, disabledPositiveDeadlineDisplays: "-" };
});

await runCase("no-explicit-auto-reset-control", async () => {
  const source = rawPanel(one("auto card", all(panel.R), (node) => node.type === "CallExpression" && rawPanel(node).startsWith("(0,E.jsxs)(`div`,{className:`map-auto-scan-card`")));
  assert.equal(/reset/i.test(source), false);
  assert.ok(source.includes("S(`common.add`)"));
  assert.ok(source.includes("S(`map.runAutoScanNow`)"));
  return { explicitResetControl: false, resetLikeTransitions: ["missing/malformed profile load -> defaults", "disable -> nextRunAt 0", "profile change -> selected profile load"] };
});

const report = {
  evidenceState: "EXACT_BYTES_EXECUTED",
  indexSha256: index.entry.sha256,
  panelSha256: panel.entry.sha256,
  passed: cases.length,
  failed: 0,
  cases,
};
fs.mkdirSync(here, { recursive: true });
const output = path.join(here, "case-results.json");
fs.writeFileSync(output, `${JSON.stringify(report, null, 2)}\n`);
console.log(`PASS ${cases.length}/${cases.length} Auto Scan frontend/config recovery cases`);
console.log(`wrote ${path.relative(process.cwd(), output)}`);
