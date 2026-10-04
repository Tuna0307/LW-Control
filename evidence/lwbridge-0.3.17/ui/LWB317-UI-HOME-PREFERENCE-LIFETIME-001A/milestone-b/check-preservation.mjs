import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const ui = path.join(repo, "src/LWBridge.UI-0.3.17");
const requireUi = createRequire(path.join(ui, "package.json"));
const { parse } = requireUi("@babel/parser");
const dispatch = "52dd38a6c5f003ef6f9caebf681d3fbd1ed824b5";
const appPath = "src/LWBridge.UI-0.3.17/src/App.jsx";
const homePath = "src/LWBridge.UI-0.3.17/src/HomePage.jsx";
const currentApp = fs.readFileSync(path.join(repo, appPath), "utf8");
const currentHome = fs.readFileSync(path.join(repo, homePath), "utf8");
const baselineApp = execFileSync("git", ["show", `${dispatch}:${appPath}`], { cwd: repo, encoding: "utf8" });
const baselineHome = execFileSync("git", ["show", `${dispatch}:${homePath}`], { cwd: repo, encoding: "utf8" });
const sha256 = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
const normalized = (value) => value.replace(/\r\n/g, "\n");

function walk(node, list = []) {
  if (!node || typeof node !== "object") return list;
  if (node.type) list.push(node);
  for (const value of Object.values(node)) {
    if (Array.isArray(value)) value.forEach((child) => walk(child, list));
    else if (value && typeof value === "object") walk(value, list);
  }
  return list;
}

function nodes(source) {
  return walk(parse(source, { sourceType: "module", plugins: ["jsx"] }));
}

function callback(source, name) {
  const node = nodes(source).find((candidate) => candidate.type === "VariableDeclarator" && candidate.id?.name === name);
  assert.ok(node?.init?.arguments?.[0], `missing callback ${name}`);
  return normalized(source.slice(node.init.arguments[0].start, node.init.arguments[0].end));
}

function variableInitializer(source, name) {
  const node = nodes(source).find((candidate) => candidate.type === "VariableDeclarator" && candidate.id?.name === name);
  assert.ok(node?.init, `missing variable ${name}`);
  return normalized(source.slice(node.init.start, node.init.end));
}

function jsxByText(source, tagName, marker) {
  const node = nodes(source).find((candidate) => candidate.type === "JSXElement"
    && candidate.openingElement?.name?.name === tagName
    && source.slice(candidate.start, candidate.end).includes(marker));
  assert.ok(node, `missing ${tagName} ${marker}`);
  return normalized(source.slice(node.start, node.end));
}

function effectByText(source, marker) {
  const node = nodes(source).find((candidate) => candidate.type === "CallExpression" && candidate.callee?.name === "useEffect"
    && candidate.arguments?.[0] && source.slice(candidate.arguments[0].start, candidate.arguments[0].end).includes(marker));
  assert.ok(node, `missing effect ${marker}`);
  return normalized(source.slice(node.start, node.end));
}

const exactCallbacks = [
  "updateAutoScanConfig",
  "acknowledgeGameRootStatus",
  "refreshMapSummary",
  "selectRoute",
  "acknowledgeMapScan",
  "acknowledgeMapCounts",
  "updateAutoReconnect",
  "selectGameRoot",
  "requestServerJump",
  "jumpServer"
];
const callbackResults = exactCallbacks.map((name) => {
  const before = callback(baselineApp, name);
  const after = callback(currentApp, name);
  assert.equal(after, before, `${name} changed outside assigned Auto Launch lifetime scope`);
  return { name, sha256: sha256(after) };
});

const selectedProfileBefore = variableInitializer(baselineApp, "selectedProfileId");
const selectedProfileAfter = variableInitializer(currentApp, "selectedProfileId");
assert.equal(selectedProfileAfter, selectedProfileBefore, "profile selection expression changed");

const rootEffectBefore = effectByText(baselineApp, 'backendBridge.invoke("game_root_status", {})');
const rootEffectAfter = effectByText(currentApp, 'backendBridge.invoke("game_root_status", {})');
assert.equal(rootEffectAfter, rootEffectBefore, "root acknowledgement/profile effect changed");

const retainedBefore = jsxByText(baselineApp, "RetainedPages", "activeRoute={activeRoute}");
const retainedAfter = jsxByText(currentApp, "RetainedPages", "activeRoute={activeRoute}");
assert.equal(retainedAfter, retainedBefore, "lazy/retained page ownership changed");

const reconnectBefore = jsxByText(baselineHome, "ToggleRow", 'label={t("automation.autoReconnect.title")}');
const reconnectAfter = jsxByText(currentHome, "ToggleRow", 'label={t("automation.autoReconnect.title")}');
assert.equal(reconnectAfter, reconnectBefore, "Automatic Reconnection Home control changed");

const rootPickerBefore = jsxByText(baselineHome, "button", "onClick={onGameRootSelect}");
const rootPickerAfter = jsxByText(currentHome, "button", "onClick={onGameRootSelect}");
assert.equal(rootPickerAfter, rootPickerBefore, "game-root picker control changed");

const refresh = callback(currentApp, "refreshStatus");
for (const marker of [
  "mapApi.readStatus()",
  "mapApi.readProxyStatus()",
  'backendBridge.invoke("game_recovery_status"',
  'backendBridge.invoke("local_config_get", {})',
  "setLocalConfig(configResult.value)"
]) assert.ok(refresh.includes(marker), `refreshStatus lost ${marker}`);
assert.ok(!refresh.includes('backendBridge.invoke("game_root_status"'), "periodic config/status polling must not take root ownership");

for (const marker of [
  'backendBridge.listen("bridge://game-recovery"',
  "mapApi.listenStatus",
  "mapApi.listenScanStatus",
  "window.setInterval(refreshStatus, 5000)"
]) assert.ok(currentApp.includes(marker), `App lost preserved listener/timer ${marker}`);

const report = {
  task: "LWB317-UI-HOME-PREFERENCE-LIFETIME-001A",
  date: "2026-10-04",
  result: "LWB317_HOME_PREFERENCE_PRESERVATION_OK",
  dispatch,
  exactCallbacks: callbackResults,
  selectedProfileExpression: selectedProfileAfter,
  rootAcknowledgementEffectSha256: sha256(rootEffectAfter),
  retainedPagesSha256: sha256(retainedAfter),
  autoReconnectToggleSha256: sha256(reconnectAfter),
  rootPickerSha256: sha256(rootPickerAfter),
  refreshStatusPreservedInventory: ["readStatus", "readProxyStatus", "game_recovery_status", "local_config_get", "setLocalConfig"],
  listenersAndTimer: ["bridge://game-recovery", "mapApi.listenStatus", "mapApi.listenScanStatus", "refreshStatus@5000ms"],
  note: "refreshStatus itself intentionally differs only to update the native-confirmed Auto Launch rollback reference; mounted current tests prove that this does not give the poll ownership of the visible local Auto Launch value.",
};

fs.writeFileSync(path.join(here, "preservation-results.json"), `${JSON.stringify(report, null, 2)}\n`);
console.log(JSON.stringify({ result: report.result, exactCallbacks: report.exactCallbacks.length }, null, 2));
