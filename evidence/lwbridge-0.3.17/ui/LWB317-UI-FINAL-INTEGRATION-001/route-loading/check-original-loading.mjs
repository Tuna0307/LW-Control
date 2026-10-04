import fs from "node:fs";
import path from "node:path";
import assert from "node:assert/strict";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";
const here = path.dirname(fileURLToPath(import.meta.url)), repo = path.resolve(here, "../../../../.."), ui = path.join(repo, "src/LWBridge.UI-0.3.17");
const req = createRequire(path.join(ui, "package.json")), M = req("react/jsx-runtime");
const contract = JSON.parse(fs.readFileSync(path.join(here, "source-contract.json"), "utf8"));
const results = [];
const routes = ["overview", "automation", "city-layout", "hotkeys", "mini-games", "map-data", "settings", "march", "advanced"];
function environment({ active = "overview", visited = new Set(["overview"]), reject = false } = {}) {
  const log = [];
  const loader = (name) => () => { log.push("load:" + name); return reject ? Promise.reject(new Error("inert import rejection")) : Promise.resolve({}); };
  const bindings = { Mi: loader("automation"), Ni: loader("city-layout"), Pi: loader("hotkeys+mini-games"), Fi: loader("map-data"), Ii: loader("settings"), I: loader("march") };
  const preload = new Function(...Object.keys(bindings), "const " + contract.original.loaders.Ui.text + ";" + contract.original.functions.preload.text + ";return Wi;")(...Object.values(bindings));
  let resolveSummary, rejectSummary;
  const summary = new Promise((resolve, reject) => { resolveSummary = resolve; rejectSummary = reject; });
  const state = { active, visited };
  const select = new Function("i", "_t", "F", "Wi", "c", "s", "a", contract.original.functions.select.text + ";return Tt;")(
    active, () => { log.push("summary:start"); return summary; }, (message) => log.push("log:" + message), preload,
    (callback) => { log.push("transition"); callback(); }, (callback) => { state.visited = callback(state.visited); log.push("visited"); },
    (route) => { state.active = route; log.push("active:" + route); });
  return { log, preload, select, state, resolveSummary, rejectSummary };
}
for (const route of routes) {
  const e = environment();
  e.preload(route);
  assert.deepEqual(e.log, ["overview", "advanced"].includes(route) ? [] : ["load:" + (["hotkeys", "mini-games"].includes(route) ? "hotkeys+mini-games" : route)]);
  results.push({ name: "preload:" + route, pass: true, trace: e.log });
}
const rejection = environment({ reject: true });
rejection.preload("automation"); await Promise.resolve(); await Promise.resolve();
results.push({ name: "rejected preload is swallowed", pass: true, trace: rejection.log });
const same = environment(); same.select("overview"); assert.deepEqual(same.log, []);
results.push({ name: "same active route performs no preload/summary/transition", pass: true });
const map = environment(); const previous = map.state.visited; map.select("map-data");
assert.deepEqual(map.log, ["summary:start", "load:map-data", "transition", "visited", "active:map-data"]);
assert.notEqual(map.state.visited, previous); assert.equal(map.state.active, "map-data"); assert.ok(map.state.visited.has("map-data"));
assert.equal(map.resolveSummary instanceof Function, true);
results.push({ name: "map summary initiated then preload then transition; unresolved summary never delays route", pass: true, trace: [...map.log] });
map.rejectSummary(new Error("inert failure")); await Promise.resolve(); await Promise.resolve();
assert.ok(map.log.at(-1).startsWith("log:map summary error "));
results.push({ name: "map summary rejection logs independently of route commit", pass: true, trace: [...map.log] });
const revisited = new Set(["overview", "settings"]), revisit = environment({ visited: revisited }); revisit.select("settings"); assert.equal(revisit.state.visited, revisited);
results.push({ name: "visited route retains Set identity", pass: true, trace: revisit.log });
const Qr = new Function("M", "De", "Xr", "const " + contract.original.loaders.Zr.text + ";" + contract.original.functions.navigation.text + ";return Qr;")(M, () => ({ t: (key) => key }), () => null);
const calls = [];
const nav = Qr({ active: "overview", showAdvanced: false, onSelect: (route) => calls.push("select:" + route), onPreload: (route) => calls.push("preload:" + route) });
for (const button of nav.props.children[1]) {
  button.props.onMouseEnter(); button.props.onFocus(); button.props.onClick();
}
assert.equal(calls.length, 24);
for (let index = 0; index < calls.length; index += 3) {
  assert.equal(calls[index], calls[index + 1]); assert.equal(calls[index + 2].replace("select:", "preload:"), calls[index]);
}
results.push({ name: "all eight source nav buttons preload on hover and focus; click selects", pass: true, trace: calls });
const report = { marker: "LWB317_ORIGINAL_ROUTE_LOADING_OK", cases: results.length, results,
  sourceIdentity: contract.original.sha256, locators: { navigation: contract.original.functions.navigation, preload: contract.original.functions.preload, select: contract.original.functions.select },
  limits: "Executes exact original navigation/preload/select source with inert loaders/summary and controlled state callbacks. Does not implement current lazy pages or establish React Suspense/Activity/browser download timing; those need mounted canonical and browser proof after migration." };
if (process.argv.includes("--record")) fs.writeFileSync(path.join(here, "original-loading-results.json"), JSON.stringify(report, null, 2) + "\n");
console.log(JSON.stringify({ marker: report.marker, cases: results.length }));
