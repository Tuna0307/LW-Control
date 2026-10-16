// CONTROLLED / INERT frontend checks for the passive Zombie Boss category. Every row,
// option and reply is a synthetic fixture served by an in-process fake Map API; none of it
// is genuine game evidence and no march/rally/Follow/attack is ever offered or invoked.
import assert from "node:assert/strict";
import { createRequire } from "node:module";
import path from "node:path";
import { fileURLToPath } from "node:url";
import {
  MAP_DEFAULT_SCAN_TYPES, MAP_KIND_KEYS, MAP_SCAN_TYPES, MAP_TABS,
  buildSearchPayload, buildStartPayload, normalizeSelectedTypes,
} from "../src/mapBackend.js";
import { buildMapColumns, zombieBossDiscoverer, zombieBossProtection, zombieBossRemaining } from "../src/mapTablePresentation.js";
import { ZOMBIE_BOSS_KEYS, ZOMBIE_BOSS_MESSAGES } from "../src/locales/map014-zombie-boss.js";

// ---- selection semantics ----------------------------------------------------
assert.deepEqual(MAP_SCAN_TYPES.map(({ key }) => key), ["city", "resource", "monster", "truck", "railway", "dispatch", "ghost", "treasure", "other", "zombie_boss"]);
// Zombie Boss follows Other so every existing tab position stays unchanged.
assert.deepEqual(MAP_TABS.map(({ key }) => key), ["city", "resource", "monster", "truck", "railway", "dispatch", "ghost", "treasure", "other", "zombie_boss"]);
assert.equal(MAP_DEFAULT_SCAN_TYPES.length, 8);
assert.equal(MAP_DEFAULT_SCAN_TYPES.includes("zombie_boss"), false, "Zombie Boss is opt-in; the implicit scan stays the eight recovered kinds");
assert.deepEqual(buildStartPayload(undefined, "normal").selectedTypes, MAP_DEFAULT_SCAN_TYPES);
assert.deepEqual(normalizeSelectedTypes(["zombie_boss", "zombie_boss"]), ["zombie_boss"]);
assert.deepEqual(normalizeSelectedTypes(["monster", "zombie_boss"]), ["monster", "zombie_boss"]);
assert.ok(MAP_KIND_KEYS.includes("zombie_boss"));

const payload = buildSearchPayload("zombie_boss", { serverId: 2212, minLevel: 80, maxLevel: 90, protectionState: "active", sorts: [{ sortBy: "remaining", sortOrder: "asc" }] });
assert.equal(payload.kind, "zombie_boss");
assert.deepEqual([payload.query.minLevel, payload.query.maxLevel, payload.query.protectionState], [80, 90, "active"], "bridge payload forwards level range and protection state");
assert.deepEqual(payload.query.sorts, [{ sortBy: "remaining", sortOrder: "asc" }]);
assert.equal(buildSearchPayload("zombie_boss", { serverId: 2212 }).query.protectionState, undefined, "no protection constraint unless chosen");

// ---- locale coverage (nine languages, same keys, non-empty) -----------------
for (const code of ["en", "zh-CN", "zh-TW", "ja", "ko", "vi", "id", "ru", "pt"]) {
  const set = ZOMBIE_BOSS_MESSAGES[code];
  assert.ok(set && Object.keys(set).length === ZOMBIE_BOSS_KEYS.length && ZOMBIE_BOSS_KEYS.every((key) => typeof set[key] === "string" && set[key].trim()), `Zombie Boss labels incomplete for ${code}`);
}
// The tab/scan label reuses the game's own installed wording (verified against locale build 23555 key 2901012).
assert.equal(ZOMBIE_BOSS_MESSAGES["zh-TW"]["map.zombieBoss"], "喪屍首領");
assert.equal(ZOMBIE_BOSS_MESSAGES["zh-CN"]["map.zombieBoss"], "丧尸首领");
assert.equal(ZOMBIE_BOSS_MESSAGES.en["map.zombieBoss"], "Zombie Boss");

// ---- presentation: unknown stays unknown, nothing invented -------------------
const en = ZOMBIE_BOSS_MESSAGES.en;
const t = (key, values = {}) => (en[key] ?? key).replace(/\{(\w+)\}/g, (_, name) => String(values[name] ?? ""));
const now = 1_800_000_000_000;
assert.equal(zombieBossRemaining({ refreshTime: now + 3_725_000 }, t, now), "01:02:05");
assert.equal(zombieBossRemaining({ refreshTime: now - 1 }, t, now), "Expired");
for (const unknown of [{}, { refreshTime: 0 }, { refreshTime: null }, { refreshTime: "" }]) assert.equal(zombieBossRemaining(unknown, t, now), "-");
assert.equal(zombieBossProtection({ monsterProtectionKnown: true, monsterProtectionActive: true, shieldEndTime: now + 61_000 }, t, now), "00:01:01");
assert.equal(zombieBossProtection({ monsterProtectionKnown: true, monsterProtectionActive: false }, t, now), "Not protected");
assert.equal(zombieBossProtection({ monsterProtectionKnown: true, monsterProtectionActive: true, shieldEndTime: now - 5 }, t, now), "Not protected", "a confirmed protection that has run out is not protected");
assert.equal(zombieBossProtection({ monsterProtectionKnown: false, shieldEndTime: now + 61_000 }, t, now), "-", "unconfirmed protection is never shown as a countdown");
assert.equal(zombieBossProtection({}, t, now), "-");
assert.equal(zombieBossDiscoverer({ ownerName: "Finder", allianceAbbr: "TST" }), "[TST] Finder");
assert.equal(zombieBossDiscoverer({ belongUid: "9001" }), "-", "a bare uid is not a discoverer name");

const columns = buildMapColumns("zombie_boss", t, "en", { 2901012: "Zombie Boss (game)" }, "", false);
const labels = columns.map((column) => column.label);
for (const key of ["map.zombieBossPower", "map.zombieBossRemaining", "map.zombieBossProtection", "map.zombieBossDiscoverer"]) assert.ok(labels.includes(key) || labels.includes(en[key]), `column ${key} missing`);
const sortable = columns.filter((column) => column.sortBy).map((column) => column.sortBy);
assert.deepEqual(sortable, ["level", "power", "remaining", "shield", "distance", "updatedAt"]);
assert.ok(columns[0].coordinate, "coordinate (Jump) column is first");
assert.ok(!columns.some((column) => column.select || column.mark || column.action || column.status), "no selection/mark/claim/action column on Zombie Boss rows");
assert.equal(columns[1].value({ monsterNameKey: "2901012" }), "Zombie Boss (game)", "name resolves from the game text key");
assert.equal(columns[1].value({}), "Zombie Boss", "unresolved name falls back to the localized category label");

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

const bosses = [
  { uuid: "7001", serverId: 2212, x: 11, y: 12, level: 85, monsterNameKey: "2901012", recommendPower: 34_500_000, refreshTime: Date.now() + 7_200_000,
    monsterProtectionKnown: true, monsterProtectionActive: true, shieldEndTime: Date.now() + 300_000, ownerName: "Finder", allianceAbbr: "TST", distanceFromHome: 20, updatedAt: Date.now() },
  { uuid: "7002", serverId: 2212, x: 21, y: 22, level: 40, monsterNameKey: "2901012", recommendPower: 16_339_000, refreshTime: Date.now() + 600_000,
    monsterProtectionKnown: true, monsterProtectionActive: false, distanceFromHome: 30, updatedAt: Date.now() },
  { uuid: "7003", serverId: 2212, x: 31, y: 32, level: 5, monsterNameKey: "2901012", distanceFromHome: 40, updatedAt: Date.now() },
];

for (const language of ["en", "zh-TW"]) {
  const dom = new JSDOM('<!doctype html><html><body><div id="root"></div></body></html>', { url: "http://127.0.0.1/map014-zombie" });
  for (const key of ["window", "document", "HTMLElement", "HTMLDialogElement", "Event", "MouseEvent", "Node", "MutationObserver", "localStorage"])
    globalThis[key] = key === "window" ? dom.window : dom.window[key];
  Object.defineProperty(globalThis, "navigator", { configurable: true, value: dom.window.navigator });
  globalThis.IS_REACT_ACT_ENVIRONMENT = true;
  if (!createRoot) ({ createRoot } = req("react-dom/client"));
  window.matchMedia = () => ({ matches: false, addListener() {}, removeListener() {} });
  localStorage.setItem("lwbridge.language", language);
  window.setInterval = () => 1; window.clearInterval = () => {}; window.setTimeout = () => 1; window.clearTimeout = () => {};
  const text = ZOMBIE_BOSS_MESSAGES[language];
  const queries = [];
  const state = { serverId: 2212, serverIdSource: "live", isReading: false, phase: "idle", selectedTypes: ["city"], scanMode: "normal" };
  const api = {
    savedServers: async () => [2212],
    dataOptions: async (serverId) => ({ serverId, counts: { zombie_boss: 3, monster: 9 }, names: { resource: [], monster: [] }, alliances: [], noAllianceCount: 0, dispatchLevels: [], treasureTypes: [], rewardItems: { truck: [], railway: [], ghost: [] } }),
    search: async (kind, query) => {
      queries.push({ kind, query: structuredClone(query) });
      let rows = kind === "zombie_boss" ? bosses : [];
      if (query.minLevel != null) rows = rows.filter((row) => row.level >= query.minLevel);
      if (query.protectionState === "active") rows = rows.filter((row) => row.monsterProtectionKnown && row.monsterProtectionActive);
      return { rows, total: rows.length };
    },
    listPlunderJobs: async () => ({ dispatchJobs: [], truckJobs: [] }),
    scanStatus: async () => state,
    follow: async () => { throw new Error("Zombie Boss rows must never offer Follow"); },
  };
  const root = createRoot(document.getElementById("root"));
  await flush(() => root.render(React.createElement(I18nProvider, { key: language }, React.createElement(MapDataPage,
    { mapApi: api, bridgeMode: "native", backendAvailable: true, online: true, currentServerId: 2212, onState() {}, onCounts() {},
      scanState: state, summary: { serverId: 2212, counts: { zombie_boss: 3, monster: 9 } } }))));
  const click = (el) => flush(() => { assert.ok(el, "mounted control missing"); el.dispatchEvent(new MouseEvent("click", { bubbles: true })); });
  const change = (el, value) => flush(() => { assert.ok(el, "mounted field missing");
    const proto = el.tagName === "INPUT" ? window.HTMLInputElement.prototype : window.HTMLSelectElement.prototype;
    Object.getOwnPropertyDescriptor(proto, "value").set.call(el, value);
    el.dispatchEvent(new window.Event("input", { bubbles: true })); el.dispatchEvent(new window.Event("change", { bubbles: true })); });

  // Scan-type checkbox exists, labelled, and is not part of the implicit selection.
  const scanCheckbox = [...document.querySelectorAll(".map-category-menu label")].find((label) => label.textContent.trim() === text["map.zombieBoss"]);
  assert.ok(scanCheckbox, `${language}: scan type list lacks Zombie Boss`);
  assert.equal(scanCheckbox.querySelector("input").checked, false, "Zombie Boss is not checked by default");

  // Dedicated result tab with its own count.
  const tab = [...document.querySelectorAll(".map-tabs [role=tab]")].find((entry) => entry.querySelector(".map-tab-label")?.textContent === text["map.zombieBoss"]);
  assert.ok(tab, `${language}: Zombie Boss result tab missing`);
  assert.equal(tab.querySelector(".map-tab-count")?.textContent, "3");
  await click(tab);
  assert.ok(document.querySelector(".map-table--zombie_boss"), "Zombie Boss table did not mount");
  assert.equal(queries.at(-1).kind, "zombie_boss");
  assert.equal(queries.at(-1).query.protectionState, undefined);
  const table = document.querySelector(".map-table--zombie_boss");
  const rows = [...table.querySelectorAll("tbody tr")];
  assert.equal(rows.length, 3);
  assert.ok(rows[0].textContent.includes("34,500,000") || rows[0].textContent.includes("34.500.000") || rows[0].textContent.includes("34500000"), "template power shown");
  assert.match(rows[0].textContent, /01:5\d:\d\d|02:00:00|01:59:\d\d/, "remaining lifetime counts down from refreshTime");
  assert.ok(rows[0].textContent.includes("Finder"));
  assert.ok(rows[1].textContent.includes(text["map.zombieBossNotProtected"]), "confirmed inactive protection is labelled");
  assert.ok([...rows[2].querySelectorAll("td")].filter((td) => td.textContent === "-").length >= 4, "unknown power/lifetime/protection/discoverer render as '-'");
  // Jump only: no Follow/Stop and exactly the coordinate button.
  assert.equal(rows[0].querySelectorAll("button.map-coordinate-button").length, 1);
  assert.ok(!/Follow|跟隨|追蹤/.test(rows[0].querySelector("button.map-coordinate-button").textContent), "no Follow action on Zombie Boss");
  assert.ok(!table.querySelector("input[type=checkbox]"), "no row selection / claim checkbox");

  // Filters: inclusive level range + protection state, page reset to 1.
  const bar = document.querySelector(".map-searchbar");
  const minSelect = bar.querySelector(`select[aria-label="${text["map.zombieBossMinLevel"]}"]`);
  const maxSelect = bar.querySelector(`select[aria-label="${text["map.zombieBossMaxLevel"]}"]`);
  assert.deepEqual([...minSelect.options].map((option) => option.value), ["", ...Array.from({ length: 30 }, (_, i) => String((i + 1) * 5))], "level choices are the game's steps of 5 (5..150)");
  assert.equal(minSelect.options[0].textContent, text["map.zombieBossAnyLevel"]);
  await change(minSelect, "40");
  assert.equal(queries.at(-1).query.minLevel, 40);
  assert.equal(queries.at(-1).query.page, 1);
  await change(maxSelect, "85");
  assert.equal(queries.at(-1).query.maxLevel, 85);
  assert.equal(queries.at(-1).query.minLevel, 40, "min and max level combine");
  await change(maxSelect, "");
  assert.equal(queries.at(-1).query.maxLevel, undefined, "clearing a level bound drops it");
  await change(bar.querySelector(`select[aria-label="${text["map.zombieBossProtectionFilter"]}"]`), "active");
  assert.equal(queries.at(-1).query.protectionState, "active");
  assert.equal([...document.querySelectorAll(".map-table--zombie_boss tbody tr")].filter((tr) => !tr.querySelector(".map-empty")).length, 1);
  await change(bar.querySelector(`select[aria-label="${text["map.zombieBossProtectionFilter"]}"]`), "");
  assert.equal(queries.at(-1).query.protectionState, undefined, "clearing the filter drops the constraint");

  // Remaining-lifetime sort.
  const remainingSort = [...document.querySelectorAll(".map-table--zombie_boss thead th button")].find((button) => button.textContent.includes(text["map.zombieBossRemaining"]));
  assert.ok(remainingSort, "remaining lifetime column is sortable");
  await click(remainingSort);
  assert.equal(queries.at(-1).query.sorts[0].sortBy, "remaining");
  await flush(() => root.unmount());
}
console.log("MAP014_ZOMBIE_BOSS_FRONTEND_CHECKS_OK");
