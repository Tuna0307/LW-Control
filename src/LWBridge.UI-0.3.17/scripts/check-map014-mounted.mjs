import assert from "node:assert/strict";
import { createRequire } from "node:module";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { parseEscortMillions } from "../src/mapOwnerFilters.js";
import { mapResourceAmount, mapResourceTerrain, mapResourceStatus, mapNumber, mapRewardCount } from "../src/mapTablePresentation.js";
import { buildSearchPayload } from "../src/mapBackend.js";

for (const [kind, filters] of [["resource",{minLevel:5,maxLevel:5,resourceAmountState:"full",resourceTileState:"black"}],
    ["truck",{escortPowerMaxMillions:25,itemKeys:["reward:1:100","reward:1:500"]}]]) {
  const payload=buildSearchPayload(kind,{serverId:2212,...filters});
  for (const [key,value] of Object.entries(filters)) assert.deepEqual(payload.query[key],value,`Production bridge stripped ${key}`);
}

assert.deepEqual(parseEscortMillions("25.5"), { valid: true, value: 25.5 });
assert.deepEqual(parseEscortMillions(""), { valid: true, value: null });
for (const invalid of ["-1", "25e1", "Infinity", "NaN", " ", "1000000001"])
  assert.equal(parseEscortMillions(invalid).valid, false, `invalid cap ${invalid}`);
assert.equal(mapResourceAmount({ resourceDetailKnown: false, resourceRemainingAmount: null, resourceFullAmount: null }, "en"), "—");
assert.equal(mapResourceAmount({ resourceDetailKnown: true, resourceRemainingAmount: 200, resourceFullAmount: 8000 }, "en"), "200 / 8,000");
assert.equal(mapResourceAmount({ resourceDetailKnown: true, resourceRemainingAmount: null, resourceFullAmount: null }, "en"), "—");
assert.equal(mapResourceAmount({ resourceDetailKnown: true, resourceRemainingAmount: 9000, resourceFullAmount: 8000 }, "en"), "—");
assert.equal(mapResourceTerrain({ blackTileKnown: false, isBlackTile: false }, "en"), "Unknown");
assert.equal(mapResourceTerrain({ blackTileKnown: true, isBlackTile: true }, "en"), "Black tile");
assert.equal(mapResourceStatus({ rebuildGatherOccupancyKnown: false }, (key) => key, {}), "—");
for (const missing of [null, undefined, "", " "]) {
  assert.equal(mapNumber(missing, "en"), "-", "unknown native Truck power cannot display as zero");
  assert.equal(mapRewardCount(missing), "-", "unknown native reward count cannot display as zero");
}
assert.equal(mapNumber(0, "en"), "0", "verified zero remains a legitimate value");
assert.equal(mapRewardCount(0), "0", "verified zero reward count remains numeric");

const rootPath = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const req = createRequire(path.join(rootPath, "package.json"));
const { JSDOM } = req("jsdom");
const { build } = req("esbuild");
const React = req("react");
let createRoot;
const uiRoot = path.join(rootPath, "src");
const bundle = await build({ stdin: {
  contents: 'export {MapDataPage} from "./MapDataPage.jsx"; export {I18nProvider} from "./i18n.jsx"; export {createProfileGameAssetReader} from "./GameAssetImage.jsx";',
  resolveDir: uiRoot, loader: "jsx" }, bundle: true, write: false, platform: "browser",
  format: "cjs", jsx: "automatic", external: ["react", "react/*", "react-dom", "react-dom/*"], logLevel: "silent" });
const loaded = { exports: {} };
new Function("require", "module", "exports", bundle.outputFiles[0].text)(req, loaded, loaded.exports);
const { MapDataPage, I18nProvider } = loaded.exports;
{
  let selected = "A", calls = [];
  const bridge = { available:true, currentProfileOwner:()=>({profileId:selected}),
    isCurrentProfileOwner:(owner)=>owner.profileId===selected,
    invokeProfileScoped:async(command,request)=>{calls.push({command,request,profileId:selected});return {dataUrl:"data:image/png;base64,actual-controlled-png"};},
    invoke:()=>{throw new Error("Unscoped native images are rejected by production");} };
  const reader = loaded.exports.createProfileGameAssetReader(bridge);
  await reader({assetPath:"game-owned/item"});
  assert.equal(calls[0].profileId,"A");
  assert.equal(calls[0].command,"game_asset_image");
  selected="B";
  await assert.rejects(reader({assetPath:"retired-A-image"}),{code:"PROFILE_GENERATION_RETIRED"});
  assert.equal(calls.length,1,"Retired image request reached another profile");
}
const flush = async (fn) => React.act(async () => { if (fn) await fn(); for (let i = 0; i < 24; i++) await Promise.resolve(); });
const outcomes = [];
for (const [language, theme, width] of [["en", "light", 1280], ["ja", "dark", 375]]) {
  const dom = new JSDOM('<!doctype html><html><body><div id="root"></div></body></html>', { url: "http://127.0.0.1/map014" });
  for (const key of ["window", "document", "HTMLElement", "HTMLDialogElement", "Event", "MouseEvent", "Node", "MutationObserver", "localStorage"])
    globalThis[key] = key === "window" ? dom.window : dom.window[key];
  Object.defineProperty(globalThis, "navigator", { configurable: true, value: dom.window.navigator });
  globalThis.IS_REACT_ACT_ENVIRONMENT = true;
  // Initialize React's delegated change event plugin with a real JSDOM
  // document, otherwise input fields register only inert early feature tests.
  if (!createRoot) ({ createRoot } = req("react-dom/client"));
  window.matchMedia = () => ({ matches: width < 400, addListener() {}, removeListener() {} });
  Object.defineProperty(window, "innerWidth", { configurable: true, value: width });
  localStorage.setItem("lwbridge.language", language);
  localStorage.setItem("lwbridge.theme", theme);
  document.documentElement.dataset.theme = theme;
  const timers = new Map(); let timerId = 0;
  window.setInterval = (callback, delay) => { const id = ++timerId; timers.set(id, { callback, delay }); return id; };
  window.clearInterval = (id) => timers.delete(id);
  window.setTimeout = () => 1;
  window.clearTimeout = () => {};
  const queries = [], clears = [], follows = [];
  let heldFollow = null;
  const state = { serverId: 2212, serverIdSource: "live", isReading: false, phase: "idle", selectedTypes: ["resource", "truck"], scanMode: "normal" };
  const rewards = [{ key: "reward:1:100", name: "Genuine Item A", iconPath: null }, { key: "reward:1:500", name: "Genuine Item B", iconPath: null }];
  const sourceSupplies = [{ key: "supplies:1", count: 1, suppliesType: 1, treasureType: 0 },
    { key: "supplies:3", count: 1, suppliesType: 3, treasureType: 0 }];
  const resources = [
    { uuid: "resource-confirmed", serverId: 2212, x: 20, y: 30, level: 7,
      resourceDetailKnown: true, resourceRemainingAmount: 200, resourceFullAmount: 8000,
      blackTileKnown: true, isBlackTile: true, rebuildGatherOccupancyKnown: true, rebuildGatherOccupied: true },
    { uuid: "resource-unknown", serverId: 2212, x: 22, y: 32, level: 8,
      resourceDetailKnown: false, blackTileKnown: false, rebuildGatherOccupancyKnown: false },
  ];
  const supportedNames = {
    season_s2_ice_supplies_11: language === "ja" ? "散乱した物資" : "Scattered Supplies",
    "300039": language === "ja" ? "採集中" : "Gathering", "372138": language === "ja" ? "待機中" : "Idle",
  };
  // Normal/native mode ignores fixture gameTexts props and requires a verified
  // per-owner, per-root locale loader, even when Map options already contain a
  // known config type. Model that read contract without claiming real game IO.
  const gameTextRoot = "C:\\controlled\\map014-game";
  const gameTextLoader = async ({ language: requestedLanguage, keys }) => ({
    language: requestedLanguage, buildId: "CONTROLLED-v24", localeSha256: "controlled-locale-sha",
    englishSha256: "controlled-en-sha", gameRoot: gameTextRoot,
    texts: Object.fromEntries(keys.map((key) => [key, supportedNames[key] || key])),
  });
  const api = {
    dataOptions: async (serverId) => ({ serverId, counts: { resource: 2, truck: 1 }, names: { resource: [], monster: [] },
      alliances: [], noAllianceCount: 0, dispatchLevels: [], treasureTypes: sourceSupplies, rewardItems: { truck: rewards, railway: [] } }),
    search: async (kind, query) => { queries.push({ kind, query: structuredClone(query) }); return { rows: kind === "resource" ? resources : kind === "truck"
      ? [{ uuid: " t-1 ", serverId: 2212, marchUuid: "1234567", arriveTs: Date.now() + 3600000,
        quality: 5, power: 25000000, currentGoods: rewards.map((item) => ({ ...item, count: 2 })) }] : kind === "monster"
      ? [{uuid:"doom-42",marchUuid:"7654321",serverId:2212,x:20,y:30,monsterNameKey:"doom"},
         {uuid:"stationary-1",serverId:2212,x:21,y:31,monsterNameKey:"ordinary"}] : [],
      total: kind === "truck" ? 1 : kind === "resource" || kind === "monster" ? 2 : 0 }; },
    listPlunderJobs: async () => ({ dispatchJobs: [], truckJobs: [] }),
    marchFollow: async (row) => { follows.push(`${row.serverId}:${String(row.marchUuid)}`); if (heldFollow) await heldFollow; },
    scanStatus: async () => state,
    clear: async (serverId) => { clears.push(serverId); return state; },
  };
  let autoRunning = false;
  const root = createRoot(document.getElementById("root"));
  const render = () => flush(() => root.render(React.createElement(I18nProvider, { key: language }, React.createElement(MapDataPage,
    { mapApi: api, bridgeMode: "native", backendAvailable: true, online: true, currentServerId: 2212,
      onState() {}, onCounts() {}, scanState: state, summary: { serverId: 2212, counts: { resource: 2, truck: 1 } },
      autoScanRunning: autoRunning, gameTextLoader, gameTextOwner: "controlled-owner", gameTextRoot }))));
  const click = (el) => flush(() => { assert.ok(el, "mounted control missing"); assert.equal(el.disabled, false);
    el.dispatchEvent(new MouseEvent("click", { bubbles: true })); });
  const change = (el, value) => flush(() => { assert.ok(el, "mounted field missing");
    const proto = el.tagName === "INPUT" ? window.HTMLInputElement.prototype : window.HTMLSelectElement.prototype;
    Object.getOwnPropertyDescriptor(proto, "value").set.call(el, value);
    el.dispatchEvent(new window.Event("input", { bubbles: true })); el.dispatchEvent(new window.Event("change", { bubbles: true })); });
  const goto = (index) => click(document.querySelectorAll(".map-tabs [role=tab]")[index]);
  const search = () => click(document.querySelector(".map-searchbar > button"));
  const field = (name) => [...document.querySelectorAll(".map-searchbar [aria-label]")]
    .find((el) => el.getAttribute("aria-label")?.includes(name));
  await render(); await goto(1);
  const resourceTable = document.querySelector(".map-table--resource")?.textContent || "";
  assert.match(resourceTable, /200\s*\/\s*8,000/, "mounted Resource lacks genuine known amount");
  assert.ok(resourceTable.includes(language === "ja" ? "不明" : "Unknown"), "unconfirmed terrain misrepresented");
  assert.ok(!resourceTable.includes("0 / 0"), "unknown native amount became fabricated zero");
  const labels = language === "en" ? ["Min level", "Max level", "Resource amount", "Tile terrain"]
    : ["最低レベル", "最高レベル", "資源残量", "タイル地形"];
  await change(field(labels[0]), "7"); await change(field(labels[1]), "9");
  await change(field(labels[2]), "partial"); await change(field(labels[3]), "black"); await search();
  for (const [key, value] of Object.entries({ minLevel: 7, maxLevel: 9, resourceAmountState: "partial", resourceTileState: "black" }))
    assert.equal(queries.at(-1).query[key], value, `mounted Resource ${key}`);
  await goto(3);
  const taskCheckbox = document.querySelector(".map-table--truck input[type=checkbox]");
  assert.ok(taskCheckbox && !taskCheckbox.disabled && !taskCheckbox.checked);
  await flush(() => taskCheckbox.click());
  assert.equal(taskCheckbox.checked, true, "padded native Truck UUID must show as selected");
  await flush(() => taskCheckbox.click());
  assert.equal(taskCheckbox.checked, false, "padded native Truck UUID must deselect visibly");
  await click(document.querySelectorAll(".map-item-filter-menu button")[1]);
  await click(document.querySelectorAll(".map-item-filter-menu button")[2]);
  assert.match(document.querySelector(".map-item-filter summary").textContent, /\(2\)/);
  await change(field(language === "en" ? "Max escort power" : "護衛戦力"), "25.5"); await search();
  assert.deepEqual(queries.at(-1).query.itemKeys, ["reward:1:100", "reward:1:500"]);
  assert.equal(queries.at(-1).query.escortPowerMaxMillions, 25.5);
  await click(document.querySelector(".map-coordinate-button"));
  assert.equal(follows.at(-1), "2212:1234567", "mounted follow uses stable native identity");
  assert.ok([...timers.values()].some((entry) => entry.delay === 3000), "follow not started");
  await click(document.querySelector(".map-coordinate-button"));
  assert.ok(![...timers.values()].some((entry) => entry.delay === 3000), "Stop Follow retained poll");
  // Meaningful inverse: after the *source row's* arrival deadline, a live
  // scheduled observation must retire the same-UUID Follow without issuing
  // another native request. The clock is controlled only in this inert test.
  await click(document.querySelector(".map-coordinate-button"));
  const expiryPoll = [...timers.values()].find((entry) => entry.delay === 3000);
  assert.ok(expiryPoll, "expiry inverse lacked a started native Follow poll");
  const beforeExpiredPoll = follows.length;
  const realNow = Date.now;
  try {
    Date.now = () => realNow() + 2 * 3600000;
    await flush(() => expiryPoll.callback());
  } finally { Date.now = realNow; }
  assert.equal(follows.length, beforeExpiredPoll, "expired Follow issued a new native request");
  assert.ok(![...timers.values()].some((entry) => entry.delay === 3000),
    "expired same-UUID Follow retained its observation timer");
  let rejectRetiredFollow;
  heldFollow = new Promise((_, reject) => { rejectRetiredFollow = reject; });
  await click(document.querySelector(".map-coordinate-button"));
  await goto(1);
  await flush(() => rejectRetiredFollow(new Error("RETIRED_TRUCK_FOLLOW_FAILURE")));
  assert.ok(!document.body.textContent.includes("RETIRED_TRUCK_FOLLOW_FAILURE"), "retired Truck failure leaked into Resource tab");
  heldFollow = null;
  await goto(3);
  await click(document.querySelectorAll(".map-item-filter-menu button")[1]); await search();
  assert.deepEqual(queries.at(-1).query.itemKeys, ["reward:1:500"]);
  await click(document.querySelectorAll(".map-item-filter-menu button")[0]); await search();
  assert.equal(queries.at(-1).query.itemKeys, undefined, "clear did not remove reward constraints");
  await goto(2);
  await click(document.querySelector(".map-coordinate-button"));
  assert.equal(follows.at(-1), "2212:7654321", "moving Doom Walker uses game-owned march UID");
  await click(document.querySelector(".map-coordinate-button"));
  assert.ok(![...timers.values()].some((entry) => entry.delay === 3000), "Doom Follow Stop retained poll");
  assert.match(document.querySelectorAll(".map-coordinate-button")[1].textContent, /21,31/, "stationary Monster lost coordinate navigation");
  await goto(7); // Treasure: seasonal supplies are an existing native subkind, not a fictitious ninth scanner type.
  const scatteredName = language === "ja" ? "散乱した物資" : "Scattered Supplies";
  const typeOptions = [...document.querySelectorAll(".map-item-filter-menu button")].map((button) => button.textContent.trim());
  assert.ok(typeOptions.some((value) => value.includes(scatteredName)),
    `Scattered Supplies is absent from the native-discriminated Treasure filter: ${JSON.stringify({ typeOptions, tab: document.querySelector(".map-tabs [aria-selected=true]")?.textContent, knownText: document.body.textContent.includes(scatteredName) })}`);
  await click(document.querySelectorAll(".map-item-filter-menu button")[1]); await search();
  assert.equal(queries.at(-1).kind, "treasure");
  assert.equal(queries.at(-1).query.suppliesType, 1, "Scattered Supplies must filter native suppliesType=1");
  assert.equal(queries.at(-1).query.treasureType, 0, "ordinary Treasure type is distinct");
  await click(document.querySelectorAll(".map-scan-tabs [role=tab]")[1]);
  await click(document.querySelector(".map-auto-scan-options button:last-of-type"));
  assert.deepEqual(clears, [2212], "Auto Clear did not call canonical command");
  autoRunning = true; await render();
  assert.equal(document.querySelector(".map-auto-scan-options button:last-of-type").disabled, true);
  await flush(() => root.unmount());
  assert.equal(document.documentElement.dataset.theme, theme);
  outcomes.push({ language, theme, width, mountedQueries: queries.length, cleared: clears.length,
    followed: follows.length, remainingTimers: timers.size });
  dom.window.close();
}
console.log("MAP014_MOUNTED_OWNER_FILTER_FOLLOW_CLEAR_OK", JSON.stringify(outcomes));
