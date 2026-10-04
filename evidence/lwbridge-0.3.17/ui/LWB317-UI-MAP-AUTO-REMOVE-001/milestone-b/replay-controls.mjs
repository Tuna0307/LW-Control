import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

import { createOriginalHarness, optionsReply } from "../../LWB317-UI-MAP-INTERACTIONS-001/original/common.mjs";
import { createHarness, nodeText, treeNodes } from "../../LWB317-UI-MAP-FILTER-LIFECYCLE-001/harness.mjs";

process.env.TZ = "Asia/Singapore";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const uiSrc = path.join(repo, "src/LWBridge.UI-0.3.17/src");
const pageFile = path.join(uiSrc, "MapDataPage.jsx");
const locatorFile = path.join(repo, "evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-AUTO-CONFIG-001/original-source-locators.json");
const NOW = Date.UTC(2026, 9, 4, 9, 15, 0);
const clone = (value) => JSON.parse(JSON.stringify(value));
const writeJson = (file, value) => fs.writeFileSync(file, `${JSON.stringify(value, null, 2)}\n`);

const locators = JSON.parse(fs.readFileSync(locatorFile, "utf8"));
const knownKinds = [...new Function(`return (${locators.index.knownKinds.source});`)()];
assert.deepEqual(knownKinds, ["city", "resource", "monster", "truck", "railway", "dispatch", "ghost", "treasure"]);

const catalogs = {};
for (const language of ["en", "ja"]) {
  const file = path.join(uiSrc, `locales/${language}.js`);
  catalogs[language] = (await import(pathToFileURL(file).href)).default;
}
const translator = (language) => (key, values = {}) =>
  (catalogs[language][key] || key).replace(/\{(\w+)\}/g, (match, name) => String(values[name] ?? match));

const baseScanState = {
  serverId: 321,
  serverIdSource: "fixture",
  scanRunId: "",
  isReading: false,
  phase: "idle",
  selectedTypes: [...knownKinds],
  totalBlocks: 0,
  completedBlocks: 0,
  readBlocks: 0,
  failedBlocks: 0,
  unreadBlocks: 0,
  inflightBlocks: 0,
  scanMode: "normal",
  concurrency: 8,
  scanRate: 0,
  progressPercent: 0,
  lastError: null,
  startedAt: 0,
  updatedAt: 0,
};
const emptyCounts = Object.fromEntries(knownKinds.map((kind) => [kind, 0]));
const initialConfig = {
  enabled: true,
  intervalMinutes: 90,
  serverIds: [8],
  selectedTypes: ["city", "truck", "treasure"],
  scanMode: "normal",
  returnToOriginalServer: false,
  nextRunAt: NOW + 90 * 60_000,
};

function classHas(node, className) {
  return typeof node?.props?.className === "string" && node.props.className.split(/\s+/).includes(className);
}
async function enterAuto(h, t, label) {
  const tab = h.findNodes((node) => node.type === "button" && node.props?.role === "tab" && nodeText(node) === t("map.autoScan"))[0];
  assert.ok(tab, `${label}: Auto tab`);
  assert.equal(tab.props["aria-selected"], false, `${label}: Auto starts inactive`);
  tab.props.onClick();
  await h.settle();
  const selected = h.findNodes((node) => node.type === "button" && node.props?.role === "tab" && nodeText(node) === t("map.autoScan"))[0];
  assert.equal(selected.props["aria-selected"], true, `${label}: Auto selected via rendered callback`);
}
function autoInput(h) {
  const input = h.findNodes((node) => node.type === "input" && node.props?.inputMode === "numeric")[0];
  assert.ok(input, `${h.label}: Auto server input`);
  return input;
}
function addButton(h, t) {
  const button = h.findNodes((node) => node.type === "button" && nodeText(node) === t("common.add"))[0];
  assert.ok(button, `${h.label}: Add button`);
  return button;
}
function removeButton(h, t, id) {
  const aria = `${t("common.remove")} ${id}`;
  const button = h.findNodes((node) => node.type === "button" && node.props?.["aria-label"] === aria)[0];
  assert.ok(button, `${h.label}: remove ${id}`);
  return button;
}
function typeView(h) {
  const group = h.findNodes((node) => classHas(node, "map-types--compact"))[0];
  assert.ok(group, `${h.label}: type group`);
  const labels = treeNodes(group).filter((node) => node.type === "label");
  assert.equal(labels.length, knownKinds.length, `${h.label}: eight type labels`);
  return labels.map((label, index) => {
    const input = treeNodes(label).find((node) => node.type === "input" && node.props?.type === "checkbox");
    return { key: knownKinds[index], text: nodeText(label), checked: input.props.checked, disabled: input.props.disabled };
  });
}
function chipView(h) {
  const group = h.findNodes((node) => classHas(node, "map-auto-scan-server-chips"))[0];
  assert.ok(group, `${h.label}: chip group`);
  return treeNodes(group).filter((node) => node.type === "button").map((button) => ({ ariaLabel: button.props["aria-label"] }));
}

async function bootOriginal(language) {
  const t = translator(language);
  const summary = { serverId: 321, counts: emptyCounts, scanState: baseScanState };
  const h = await createOriginalHarness({
    label: `auto-remove-b-controls-original-${language}`,
    online: true,
    now: NOW,
    language,
    translate: t,
    serverId: 321,
    scanState: baseScanState,
    summary,
    autoScanConfig: clone(initialConfig),
    autoScanRunning: false,
    stubs: { dataOptions: { mode: "auto", value: optionsReply({ scanProgress: null }) } },
  });
  await h.mount();
  await enterAuto(h, t, h.label);
  return { h, t, applyEmission: async () => {} };
}

const pageSource = fs.readFileSync(pageFile, "utf8");
async function bootCurrent(language) {
  const t = translator(language);
  const emissions = [];
  const summary = { serverId: 321, counts: emptyCounts, scanState: baseScanState };
  const h = await createHarness(pageSource, `auto-remove-b-controls-current-${language}`, {
    now: NOW,
    language,
    serverId: 321,
    translate: t,
    props: {
      bridgeMode: "native",
      backendAvailable: true,
      online: true,
      currentServerId: 321,
      previewState: "",
      activeTab: "city",
      onActiveTabChange: () => {},
      scanState: baseScanState,
      summary,
      onState: () => {},
      onCounts: () => {},
      autoScanConfig: clone(initialConfig),
      autoScanRunning: false,
      onAutoScanConfig: (next) => emissions.push(clone(next)),
    },
    dataOptions: optionsReply({ scanProgress: null })(321),
  });
  await h.mount();
  await enterAuto(h, t, h.label);
  return {
    h,
    t,
    emissions,
    applyEmission: async () => {
      assert.ok(emissions.length > 0, `${h.label}: expected inert parent emission`);
      await h.setProps({ autoScanConfig: clone(emissions.at(-1)) });
    },
  };
}

const nativeMutatorNames = new Set([
  "scanStart", "scanStop", "scanClear", "coordinateJump", "cityExport", "playerMarkSet",
  "dispatchShareAlliance", "dispatchPlunderSchedule", "dispatchPlunderCancel", "dispatchPlunderClear",
  "truckPlunderSchedule", "truckPlunderCancel", "truckPlunderClear", "treasureClaim",
  "treasureStateRefresh", "treasureStateRefreshAll", "marchFollow",
]);

async function replay(side, language) {
  const boot = side === "original" ? await bootOriginal(language) : await bootCurrent(language);
  const { h, t, applyEmission } = boot;
  try {
    const transcript = {
      side,
      language,
      labels: {
        add: nodeText(addButton(h, t)),
        remove8: removeButton(h, t, 8).props["aria-label"],
      },
      typeOrder: typeView(h),
      initialServerIds: clone(h.props.autoScanConfig.serverIds),
      add: {},
      enter: {},
      remove: {},
    };

    let input = autoInput(h);
    input.props.onChange({ target: { value: "8, 15, 15, 120; 0 100000 abc" } });
    await h.settle();
    transcript.add.draft = autoInput(h).props.value;
    transcript.add.disabledBefore = addButton(h, t).props.disabled;
    assert.equal(transcript.add.disabledBefore, false, `${h.label}: Add enabled for valid parsed IDs`);
    addButton(h, t).props.onClick();
    await h.settle();
    await applyEmission();
    transcript.add.serverIds = clone(h.props.autoScanConfig.serverIds);
    transcript.add.inputAfter = autoInput(h).props.value;
    transcript.add.chips = chipView(h);
    assert.deepEqual(transcript.add.serverIds, [8, 15, 120], `${h.label}: Add deduplicates and preserves order`);
    assert.equal(transcript.add.inputAfter, "", `${h.label}: Add clears draft`);

    input = autoInput(h);
    input.props.onChange({ target: { value: "120，7 15;9" } });
    await h.settle();
    let prevented = 0;
    autoInput(h).props.onKeyDown({ key: "Enter", preventDefault: () => { prevented += 1; } });
    await h.settle();
    await applyEmission();
    transcript.enter.preventDefaultCount = prevented;
    transcript.enter.serverIds = clone(h.props.autoScanConfig.serverIds);
    transcript.enter.inputAfter = autoInput(h).props.value;
    transcript.enter.chips = chipView(h);
    assert.equal(prevented, 1, `${h.label}: Enter is prevented`);
    assert.deepEqual(transcript.enter.serverIds, [8, 15, 120, 7, 9], `${h.label}: Enter appends unique IDs in recovered order`);
    assert.equal(transcript.enter.inputAfter, "", `${h.label}: Enter clears draft`);

    removeButton(h, t, 15).props.onClick();
    await h.settle();
    await applyEmission();
    transcript.remove.serverIds = clone(h.props.autoScanConfig.serverIds);
    transcript.remove.chips = chipView(h);
    assert.deepEqual(transcript.remove.serverIds, [8, 120, 7, 9], `${h.label}: remove callback removes only selected ID`);

    const calls = side === "original" ? h.calls : h.calls;
    transcript.nativeMutatorCalls = calls.filter((call) => nativeMutatorNames.has(call.name)).map((call) => call.name);
    assert.deepEqual(transcript.nativeMutatorCalls, [], `${h.label}: server configuration replay stays behind native/gameplay fences`);
    transcript.runNowInvoked = false;
    return transcript;
  } finally {
    await h.unmount();
  }
}

function comparable(transcript) {
  const copy = clone(transcript);
  delete copy.side;
  delete copy.nativeMutatorCalls;
  return copy;
}

const records = [];
for (const language of ["en", "ja"]) {
  const original = await replay("original", language);
  const current = await replay("current", language);
  assert.deepEqual(comparable(current), comparable(original), `${language}: current control callbacks match recovered original`);
  records.push(original, current);
}

writeJson(path.join(here, "control-results.json"), {
  marker: "LWB317_MAP_AUTO_REMOVE_B_CONTROLS_REPLAYED",
  generatedAt: "2026-10-04",
  source: {
    knownKinds: locators.index.knownKinds,
    append: locators.index.helpers.wi,
    remove: locators.index.helpers.Ti,
    panelAdd: locators.panel.addServersCallback,
    autoCard: locators.panel.autoCard,
  },
  records,
  conclusions: {
    languages: ["en", "ja"],
    sourceOrder: knownKinds,
    addExpected: [8, 15, 120],
    enterExpected: [8, 15, 120, 7, 9],
    removeExpected: [8, 120, 7, 9],
    lastTypeProtection: "Rendered disabled checkbox verified in compare-states.mjs; disabled handler is not artificially invoked.",
    runNow: "Predicate states verified in compare-states.mjs; Run now callback is never invoked.",
    nativeFence: "No native/gameplay mutator call was produced by Add, Enter, deduplication or removal replay.",
  },
});

console.log("LWB317_MAP_AUTO_REMOVE_B_CONTROLS_OK languages=2 sides=2 add=8,15,120 enter=8,15,120,7,9 remove=8,120,7,9 nativeMutators=0 runNowClicks=0");
