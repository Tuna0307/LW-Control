// CONTROLLED / INERT frontend checks for the Ghost reward filter. Every row,
// option and reply below is a synthetic fixture served by an in-process fake
// Map API; none of it is genuine game evidence, and no Ghost plunder is invoked.
import assert from "node:assert/strict";
import { createRequire } from "node:module";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { buildGhostRewardQuery, ghostRewardsKnown, parseGhostRewardMinimum, pruneGhostRewards, setGhostRewardMinimum, toggleGhostReward } from "../src/mapGhostRewards.js";
import { buildSearchPayload } from "../src/mapBackend.js";
import { buildMapColumns, mapGhostServers, mapRewardName } from "../src/mapTablePresentation.js";
import { GHOST_REWARD_MESSAGES } from "../src/locales/map014-ghost-rewards.js";

// ---- pure helpers -----------------------------------------------------------
// Genuine native Skill Medal reward arrives named by its text key 180372.
assert.equal(mapRewardName({ key: "reward:27:5001", name: "180372" }, { "180372": "Skill Medal" }), "Skill Medal");
assert.equal(mapRewardName({ key: "reward:27:5001", name: "180372" }, {}), "180372", "unresolved key stays visible, never invented");
assert.equal(mapRewardName({ key: "reward:7:520004", name: "SR Coin Box" }, { "520004": "x" }), "SR Coin Box");
for (const [raw, expected] of [["", { valid: true, value: null }], ["  ", { valid: true, value: null }], ["1", { valid: true, value: 1 }],
  ["500", { valid: true, value: 500 }], ["9007199254740991", { valid: true, value: 9007199254740991 }]])
  assert.deepEqual(parseGhostRewardMinimum(raw), expected, `minimum ${JSON.stringify(raw)}`);
for (const bad of ["0", "-1", "1.5", "1e3", "0x10", "abc", "٣", "9007199254740992", "01"])
  assert.equal(parseGhostRewardMinimum(bad).valid, false, `minimum ${bad} must be rejected`);

let selection = toggleGhostReward([], "reward:1:100");
selection = toggleGhostReward(selection, "reward:1:200");
assert.deepEqual(selection.map((entry) => entry.key), ["reward:1:100", "reward:1:200"]);
assert.deepEqual(toggleGhostReward(selection, "reward:1:100").map((entry) => entry.key), ["reward:1:200"], "second click deselects");
assert.deepEqual(toggleGhostReward(selection, ""), [], "empty key clears");
assert.equal(Array.from({ length: 30 }, (_, i) => `k${i}`).reduce((acc, key) => toggleGhostReward(acc, key), []).length, 24, "selection is capped at the backend maximum");
assert.deepEqual(pruneGhostRewards(selection, [{ key: "reward:1:200" }]).map((entry) => entry.key), ["reward:1:200"], "other server cannot keep an unoffered reward");
assert.deepEqual(pruneGhostRewards(selection, undefined), [], "no offered rewards drops every selection");

const withMinimum = setGhostRewardMinimum(selection, "reward:1:100", "500");
assert.deepEqual(buildGhostRewardQuery(withMinimum, "all"), { valid: true, fragment: { rewardFilters: [{ key: "reward:1:100", minCount: 500 }, { key: "reward:1:200" }], rewardMatch: "all" } });
assert.equal(buildGhostRewardQuery(withMinimum, "any").fragment.rewardMatch, "any");
assert.equal(buildGhostRewardQuery(withMinimum, "bogus").fragment.rewardMatch, "all", "AND remains the default for any unexpected mode");
assert.deepEqual(buildGhostRewardQuery([], "any"), { valid: true, fragment: {} }, "no selection adds no constraint");
assert.equal(buildGhostRewardQuery(setGhostRewardMinimum(selection, "reward:1:100", "0"), "all").valid, false);

assert.equal(ghostRewardsKnown({ rewards: [{ key: "reward:1:100", count: 3 }] }), true);
for (const unknown of [{}, { rewards: [] }, { rewards: null }, { rewards: [{ name: "x", count: 1 }] }, { rewards: [{ key: "k", count: 0 }] }, { rewardConfig: "raw" }])
  assert.equal(ghostRewardsKnown(unknown), false, "unknown rewards must not be treated as known");

const payload = buildSearchPayload("ghost", { serverId: 2212, ...buildGhostRewardQuery(withMinimum, "any").fragment });
assert.deepEqual(payload.query.rewardFilters, [{ key: "reward:1:100", minCount: 500 }, { key: "reward:1:200" }], "production bridge must forward reward filters");
assert.equal(payload.query.rewardMatch, "any");

const english = (key, values = {}) => (GHOST_REWARD_MESSAGES.en[key] ?? key).replace(/\{(\w+)\}/g, (_, name) => String(values[name] ?? `{${name}}`));
assert.equal(mapGhostServers({ serverId: 2212, ownerServer: 2212, srcServerId: 2212 }, english), "2212", "same server shows only the current server");
assert.equal(mapGhostServers({ serverId: 2212, ownerServer: 2210 }, english), "2212 · owner server 2210");
assert.equal(mapGhostServers({ serverId: 2212, ownerServer: 2210, srcServerId: 2211 }, english), "2212 · owner server 2210 · source server 2211");
assert.equal(mapGhostServers({ serverId: 2212, ownerServer: 0, srcServerId: null }, english), "2212", "zero/unknown origin is not displayed as a server");
assert.equal(mapGhostServers({}, english), "-");
const ghostColumns = buildMapColumns("ghost", english, "en", {}, "", false).map((column) => column.label);
assert.ok(ghostColumns.includes(english("map.ghostServer")), "Ghost table lost its server/origin column");
assert.ok(!buildMapColumns("dispatch", english, "en", {}, "", false).some((column) => column.label === english("map.ghostServer")), "Dispatch table must stay unchanged");

// ---- mounted ---------------------------------------------------------------
const rootPath = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const req = createRequire(path.join(rootPath, "package.json"));
const { JSDOM } = req("jsdom");
const { build } = req("esbuild");
const React = req("react");
let createRoot;
const bundle = await build({ stdin: {
  contents: 'export {MapDataPage} from "./MapDataPage.jsx"; export {I18nProvider} from "./i18n.jsx";',
  resolveDir: path.join(rootPath, "src"), loader: "jsx" }, bundle: true, write: false, platform: "browser",
  format: "cjs", jsx: "automatic", external: ["react", "react/*", "react-dom", "react-dom/*"], logLevel: "silent" });
const loaded = { exports: {} };
new Function("require", "module", "exports", bundle.outputFiles[0].text)(req, loaded, loaded.exports);
const { MapDataPage, I18nProvider } = loaded.exports;
const flush = async (fn) => React.act(async () => { if (fn) await fn(); for (let i = 0; i < 24; i++) await Promise.resolve(); });

const GEM = "reward:1:100", CHIP = "reward:1:200", ONLY_B = "reward:1:900";
const item = (key, name, count) => ({ key, name, count, rewardType: 1, itemId: Number(key.split(":")[2]) });
const taskRows = {
  2212: [
    { uuid: "100000000000000001", serverId: 2212, ownerUid: "7001", ownerName: "Alice", x: 11, y: 12, level: 5, quality: 4, ownerServer: 2210, srcServerId: 2210,
      completionTime: Date.now() - 60000, taskExpireTime: Date.now() + 3600000, rewards: [item(GEM, "Controlled Gem", 500), item(CHIP, "Controlled Chip", 10)] },
    { uuid: "100000000000000002", serverId: 2212, ownerUid: "7002", ownerName: "Bob", x: 21, y: 22, level: 6, quality: 3, ownerServer: 2212,
      completionTime: Date.now() + 60000, taskExpireTime: Date.now() + 3600000, rewards: [item(GEM, "Controlled Gem", 499)] },
    // Unknown reward list: must display "Unknown" (never an empty/dash) and never match a reward filter.
    { uuid: "100000000000000003", serverId: 2212, ownerUid: "7003", ownerName: "Cy", x: 31, y: 32, level: 7, quality: 4, rewardConfig: "raw-77",
      completionTime: Date.now() - 60000, taskExpireTime: Date.now() + 3600000 },
  ],
  2213: [{ uuid: "100000000000000009", serverId: 2213, ownerUid: "7009", ownerName: "Zed", x: 1, y: 2, level: 5, quality: 4, rewards: [item(ONLY_B, "Server B Only", 7)] }],
  2214: [{ uuid: "100000000000000010", serverId: 2214, ownerUid: "7010", x: 3, y: 4, level: 5, quality: 4 }],
};
const optionItems = {
  2212: [{ key: GEM, name: "Controlled Gem", iconPath: "game/icon/gem", taskCount: 2, maxCount: 500 }, { key: CHIP, name: "Controlled Chip", iconPath: null, taskCount: 1, maxCount: 10 }],
  2213: [{ key: ONLY_B, name: "Server B Only", iconPath: null, taskCount: 1, maxCount: 7 }],
  2214: [],
};
const amountOf = (row, key) => (Array.isArray(row.rewards) ? row.rewards : []).filter((entry) => entry.key === key).reduce((sum, entry) => sum + entry.count, 0);
function controlledSearch(kind, query) {
  let rows = kind === "ghost" ? (taskRows[query.serverId] || []) : [];
  if (query.rewardFilters) {
    rows = rows.filter((row) => {
      const satisfied = (filter) => amountOf(row, filter.key) >= Math.max(filter.minCount || 0, 1);
      return query.rewardMatch === "any" ? query.rewardFilters.some(satisfied) : query.rewardFilters.every(satisfied);
    });
  }
  if (query.completionStatus === "completed") rows = rows.filter((row) => row.completionTime <= Date.now());
  if (query.quality === "ssr") rows = rows.filter((row) => row.quality === 4);
  return { rows, total: rows.length };
}

for (const language of ["en", "ja"]) {
  const labels = GHOST_REWARD_MESSAGES[language];
  const dom = new JSDOM('<!doctype html><html><body><div id="root"></div></body></html>', { url: "http://127.0.0.1/map014-ghost" });
  for (const key of ["window", "document", "HTMLElement", "HTMLDialogElement", "Event", "MouseEvent", "Node", "MutationObserver", "localStorage"])
    globalThis[key] = key === "window" ? dom.window : dom.window[key];
  Object.defineProperty(globalThis, "navigator", { configurable: true, value: dom.window.navigator });
  globalThis.IS_REACT_ACT_ENVIRONMENT = true;
  if (!createRoot) ({ createRoot } = req("react-dom/client"));
  window.matchMedia = () => ({ matches: false, addListener() {}, removeListener() {} });
  localStorage.setItem("lwbridge.language", language);
  window.setInterval = () => 1; window.clearInterval = () => {}; window.setTimeout = () => 1; window.clearTimeout = () => {};
  const queries = [];
  const state = { serverId: 2212, serverIdSource: "live", isReading: false, phase: "idle", selectedTypes: ["ghost"], scanMode: "normal" };
  const api = {
    savedServers: async () => [2212, 2213, 2214],
    dataOptions: async (serverId) => ({ serverId, counts: { ghost: (taskRows[serverId] || []).length }, names: { resource: [], monster: [] }, alliances: [], noAllianceCount: 0,
      dispatchLevels: [], treasureTypes: [], rewardItems: { truck: [], railway: [], ghost: optionItems[serverId] || [] } }),
    search: async (kind, query) => { queries.push({ kind, query: structuredClone(query) }); return controlledSearch(kind, query); },
    listPlunderJobs: async () => ({ dispatchJobs: [], truckJobs: [] }),
    scanStatus: async () => state,
    prepareGhostPlunder: async () => { throw new Error("Ghost plunder must never be invoked by a reward filter check"); },
  };
  const root = createRoot(document.getElementById("root"));
  await flush(() => root.render(React.createElement(I18nProvider, { key: language }, React.createElement(MapDataPage,
    { mapApi: api, bridgeMode: "native", backendAvailable: true, online: true, currentServerId: 2212, onState() {}, onCounts() {},
      scanState: state, summary: { serverId: 2212, counts: { ghost: 3 } } }))));
  const click = (el) => flush(() => { assert.ok(el, "mounted control missing"); el.dispatchEvent(new MouseEvent("click", { bubbles: true })); });
  const change = (el, value) => flush(() => { assert.ok(el, "mounted field missing");
    const proto = el.tagName === "INPUT" ? window.HTMLInputElement.prototype : window.HTMLSelectElement.prototype;
    Object.getOwnPropertyDescriptor(proto, "value").set.call(el, value);
    el.dispatchEvent(new window.Event("input", { bubbles: true })); el.dispatchEvent(new window.Event("change", { bubbles: true })); });
  const bar = () => document.querySelector(".map-searchbar");
  const byLabel = (text) => [...bar().querySelectorAll("[aria-label]")].find((el) => el.getAttribute("aria-label").includes(text));
  const rewardButton = (key) => document.querySelector(`.map-ghost-reward-filter [data-map-reward-key="${key}"]`);
  const last = () => queries.at(-1);
  const dataRows = () => [...document.querySelectorAll(".map-table--ghost tbody tr")].filter((tr) => !tr.querySelector(".map-empty"));

  await click([...document.querySelectorAll(".map-tabs [role=tab]")].find((tab) => tab.getAttribute("aria-selected") !== "true" && tab.querySelector(".map-tab-label")?.textContent === (language === "en" ? "Ghost Ops" : "幽霊作戦")));
  assert.ok(document.querySelector(".map-table--ghost"), "Ghost tab did not mount");
  assert.equal(last().kind, "ghost");
  assert.equal(last().query.rewardFilters, undefined, "no reward constraint before a selection");

  // Presentation: identity/owner/server/expiry/rewards, and unknown is not empty.
  const table = document.querySelector(".map-table--ghost");
  const bodyRows = [...table.querySelectorAll("tbody tr")];
  assert.equal(bodyRows.length, 3);
  assert.match(bodyRows[0].textContent, /Alice/);
  assert.ok(bodyRows[0].textContent.includes(language === "en" ? "2212 · owner server 2210" : "2212 · 所有者サーバー 2210"), "owner server must be visible when it differs from the stored server");
  assert.ok(bodyRows[1].textContent.includes("2212") && !bodyRows[1].textContent.includes(labels["map.ghostOwnerServer"].replace("{server}", "2212")), "same-server owner is not repeated");
  assert.equal(bodyRows[0].querySelectorAll(".map-reward-item").length, 2, "known rewards render with their icons and amounts");
  assert.ok(bodyRows[0].querySelector(".map-reward-item").getAttribute("title").includes("Controlled Gem"), "real stored reward name is shown");
  assert.ok(bodyRows[2].querySelector(".map-reward-unknown"), "row without an authoritative reward list must say Unknown");
  assert.equal(bodyRows[2].querySelectorAll(".map-reward-item").length, 0);
  assert.ok(bodyRows[2].textContent.includes(labels["map.rewardsUnknown"]));
  const headers = [...table.querySelectorAll("thead th")].map((th) => th.textContent);
  assert.ok(headers.includes(labels["map.ghostServer"]));

  // Options: real names + game icons for every offered identity, with task counts.
  assert.equal(document.querySelectorAll(".map-ghost-reward-filter [data-map-reward-key]").length, 2);
  assert.ok(rewardButton(GEM).textContent.includes("Controlled Gem") && rewardButton(GEM).textContent.includes(labels["map.ghostRewardTaskCount"].replace("{count}", "2")));
  assert.ok(rewardButton(GEM).querySelector("img, .game-asset-placeholder"), "reward option lacks a game image slot");

  // Two selections: default semantics = ALL (AND). No OR unless chosen.
  await click(rewardButton(GEM)); await click(rewardButton(CHIP));
  assert.deepEqual(last().query.rewardFilters, [{ key: GEM }, { key: CHIP }]);
  assert.equal(last().query.rewardMatch, "all", "default must be AND");
  assert.equal(last().query.page, 1);
  assert.deepEqual(dataRows().length, 1, "only the task with both rewards remains");
  // Quantity thresholds (inclusive boundary is enforced by the backend; the UI sends exact integers).
  await change(byLabel(labels["map.ghostRewardMinimumFor"].replace("{name}", "Controlled Gem")), "500");
  assert.deepEqual(last().query.rewardFilters, [{ key: GEM, minCount: 500 }, { key: CHIP }]);
  assert.equal(last().query.rewardFilters[0].minCount, 500, "minimum is a number, not text");
  // Invalid minimum: visible error, and NO query that would silently drop the constraint.
  const issued = queries.length;
  for (const bad of ["0", "1.5", "-3", "abc"]) {
    await change(byLabel(labels["map.ghostRewardMinimumFor"].replace("{name}", "Controlled Gem")), bad);
    assert.ok(document.body.textContent.includes(labels["map.ghostRewardInvalidMinimum"]), `invalid minimum ${bad} not surfaced`);
    assert.equal(queries.length, issued, `invalid minimum ${bad} issued a widened query`);
  }
  await change(byLabel(labels["map.ghostRewardMinimumFor"].replace("{name}", "Controlled Gem")), "501");
  assert.equal(last().query.rewardFilters[0].minCount, 501);
  assert.equal(document.querySelectorAll("tbody tr .map-empty").length, 1, "501 > 500 leaves no task");
  assert.ok(!document.body.textContent.includes(labels["map.ghostRewardInvalidMinimum"]), "error cleared by a valid minimum");
  await change(byLabel(labels["map.ghostRewardMinimumFor"].replace("{name}", "Controlled Gem")), "500");

  // Explicit, labelled "any of".
  const matchSelect = byLabel(labels["map.ghostRewardMatch"]);
  assert.deepEqual([...matchSelect.options].map((option) => [option.value, option.textContent]), [["all", labels["map.ghostRewardMatchAll"]], ["any", labels["map.ghostRewardMatchAny"]]]);
  await change(matchSelect, "any");
  assert.equal(last().query.rewardMatch, "any");
  assert.equal(dataRows().length, 1, "Alice satisfies gem>=500 (any)");
  await change(matchSelect, "all");
  assert.equal(last().query.rewardMatch, "all");

  // Combination with existing Ghost filters, then pagination reset to page 1.
  await change(byLabel(language === "en" ? "Status" : "ステータス"), "completed");
  await change(byLabel(language === "en" ? "Quality" : "品質"), "ssr");
  assert.equal(last().query.completionStatus, "completed"); assert.equal(last().query.quality, "ssr");
  assert.deepEqual(last().query.rewardFilters, [{ key: GEM, minCount: 500 }, { key: CHIP }], "reward filter combines with status and quality");
  assert.equal(last().query.serverId, 2212);

  // Deselect one reward through its chip, then clear everything.
  await click(document.querySelector(`[data-map-ghost-reward-chip="${CHIP}"] > button`));
  assert.deepEqual(last().query.rewardFilters, [{ key: GEM, minCount: 500 }]);
  assert.equal(byLabel(labels["map.ghostRewardMatch"]), undefined, "match mode only matters with two or more rewards");
  await click(rewardButton(CHIP));
  await click([...document.querySelectorAll(".map-ghost-reward-filter .map-item-filter-menu > button:not([data-map-reward-key])")][0]);
  assert.equal(last().query.rewardFilters, undefined, "clear removes the reward constraint");
  assert.equal(last().query.rewardMatch, undefined);

  await change(byLabel(language === "en" ? "Status" : "ステータス"), "");
  await change(byLabel(language === "en" ? "Quality" : "品質"), "");
  assert.equal(last().query.completionStatus, undefined); assert.equal(last().query.quality, undefined);
  assert.equal(dataRows().length, 3, "clearing every filter restores all stored Ghost tasks (unknown included)");

  // Per-server isolation: switching the saved server drops the previous selection and options.
  await click(rewardButton(GEM));
  assert.deepEqual(last().query.rewardFilters, [{ key: GEM }]);
  await change(document.querySelector("[data-map-saved-server]"), "2213");
  assert.equal(last().query.serverId, 2213);
  assert.equal(last().query.rewardFilters, undefined, "server 2212 selection leaked into server 2213");
  assert.ok(rewardButton(ONLY_B) && !rewardButton(GEM), "server 2213 must offer only its own rewards");
  assert.equal(dataRows().length, 1);
  // A server whose Ghost rows have no authoritative reward lists explains why filtering is unavailable.
  await change(document.querySelector("[data-map-saved-server]"), "2214");
  assert.ok(document.body.textContent.includes(labels["map.ghostRewardNoData"]), "no-data explanation missing");
  assert.equal(document.querySelectorAll(".map-ghost-reward-filter [data-map-reward-key]").length, 0);
  assert.equal(document.querySelectorAll(".map-reward-unknown").length, 1, "unknown task still shown, never as an empty reward list");

  await flush(() => root.unmount());
  dom.window.close();
}
console.log("MAP014_GHOST_REWARD_FILTER_CONTROLLED_OK");
