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
const accepted = "c0d9082ee8f8b4b8985e8025657967a6898a4097";
const appPath = "src/LWBridge.UI-0.3.17/src/App.jsx";
const homePath = "src/LWBridge.UI-0.3.17/src/HomePage.jsx";
const helperPath = "src/LWBridge.UI-0.3.17/src/autoLaunchPreference.js";
const currentApp = fs.readFileSync(path.join(repo, appPath), "utf8");
const currentHome = fs.readFileSync(path.join(repo, homePath), "utf8");
const currentHelper = fs.readFileSync(path.join(repo, helperPath), "utf8");
const acceptedApp = execFileSync("git", ["show", `${accepted}:${appPath}`], { cwd: repo, encoding: "utf8" });
const acceptedHome = execFileSync("git", ["show", `${accepted}:${homePath}`], { cwd: repo, encoding: "utf8" });
const acceptedHelper = execFileSync("git", ["show", `${accepted}:${helperPath}`], { cwd: repo, encoding: "utf8" });
const normalized = (value) => value.replace(/\r\n/g, "\n");
const sha256 = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();

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

function variable(source, predicate, label) {
  const node = nodes(source).find(predicate);
  assert.ok(node, `missing ${label}`);
  return normalized(source.slice(node.start, node.end));
}

function jsxByText(source, tagName, marker) {
  return variable(source, (node) => node.type === "JSXElement"
    && node.openingElement?.name?.name === tagName
    && source.slice(node.start, node.end).includes(marker), `${tagName} ${marker}`);
}

function effectByText(source, marker) {
  return variable(source, (node) => node.type === "CallExpression" && node.callee?.name === "useEffect"
    && node.arguments?.[0] && source.slice(node.arguments[0].start, node.arguments[0].end).includes(marker), `effect ${marker}`);
}

assert.equal(normalized(currentHelper), normalized(acceptedHelper), "accepted Auto Launch storage helper changed");

const autoLaunchState = (source) => variable(source, (node) => node.type === "VariableDeclarator"
  && node.id?.type === "ArrayPattern"
  && node.id.elements?.[0]?.name === "autoLaunchGame"
  && node.id.elements?.[1]?.name === "setAutoLaunchGame", "Auto Launch state");
assert.equal(autoLaunchState(currentApp), autoLaunchState(acceptedApp), "accepted Auto Launch state initializer changed");
assert.equal(callback(currentApp, "updateAutoLaunch"), callback(acceptedApp, "updateAutoLaunch"), "accepted Auto Launch callback changed");
assert.equal(
  jsxByText(currentHome, "ToggleRow", 'label={t("auth.autoLaunchGame")}'),
  jsxByText(acceptedHome, "ToggleRow", 'label={t("auth.autoLaunchGame")}'),
  "accepted Auto Launch Home control changed",
);

const unchangedCallbacks = [
  "updateAutoScanConfig",
  "acknowledgeGameRootStatus",
  "refreshMapSummary",
  "selectRoute",
  "acknowledgeMapScan",
  "acknowledgeMapCounts",
  "selectGameRoot",
  "requestServerJump",
  "jumpServer",
];
const callbacks = unchangedCallbacks.map((name) => {
  const before = callback(acceptedApp, name);
  const after = callback(currentApp, name);
  assert.equal(after, before, `${name} changed while preserving accepted Unit A`);
  return { name, sha256: sha256(after) };
});

const selectedProfile = (source) => variable(source, (node) => node.type === "VariableDeclarator" && node.id?.name === "selectedProfileId", "selectedProfileId");
assert.equal(selectedProfile(currentApp), selectedProfile(acceptedApp), "selected-profile expression changed");
assert.equal(
  effectByText(currentApp, 'backendBridge.invoke("game_root_status", {})'),
  effectByText(acceptedApp, 'backendBridge.invoke("game_root_status", {})'),
  "game-root acknowledgement/profile effect changed",
);
assert.equal(
  jsxByText(currentApp, "RetainedPages", "activeRoute={activeRoute}"),
  jsxByText(acceptedApp, "RetainedPages", "activeRoute={activeRoute}"),
  "retained/lazy page ownership changed",
);
assert.equal(
  jsxByText(currentHome, "button", "onClick={onGameRootSelect}"),
  jsxByText(acceptedHome, "button", "onClick={onGameRootSelect}"),
  "game-root picker changed",
);

const currentRefresh = callback(currentApp, "refreshStatus");
for (const marker of [
  "mapApi.readStatus()",
  "mapApi.readProxyStatus()",
  'backendBridge.invoke("game_recovery_status"',
  'backendBridge.invoke("local_config_get", {})',
  "autoLaunchConfigPollGenerationRef.current",
  "autoLaunchCommittedRef.current",
]) assert.ok(currentRefresh.includes(marker), `current refreshStatus lost accepted Unit A marker ${marker}`);
assert.ok(!currentRefresh.includes('backendBridge.invoke("game_root_status"'), "periodic status/config polling took game-root ownership");

for (const marker of [
  'backendBridge.listen("bridge://game-recovery"',
  "mapApi.listenStatus",
  "mapApi.listenScanStatus",
  "window.setInterval(refreshStatus, 5000)",
]) assert.ok(currentApp.includes(marker), `App lost preserved listener/timer ${marker}`);

const report = {
  task: "LWB317-UI-HOME-PREFERENCE-LIFETIME-001B",
  date: "2026-10-04",
  result: "LWB317_UNIT_A_PRESERVATION_OK",
  acceptedCommit: accepted,
  autoLaunch: {
    helperSha256: sha256(normalized(currentHelper)),
    stateSha256: sha256(autoLaunchState(currentApp)),
    callbackSha256: sha256(callback(currentApp, "updateAutoLaunch")),
    homeToggleSha256: sha256(jsxByText(currentHome, "ToggleRow", 'label={t("auth.autoLaunchGame")}')),
  },
  unchangedCallbacks: callbacks,
  selectedProfileSha256: sha256(selectedProfile(currentApp)),
  refreshStatusPreservedUnitAMarkers: ["local_config_get", "autoLaunchConfigPollGenerationRef", "autoLaunchCommittedRef"],
  note: "001B intentionally changes Automatic Reconnection only. This adapter pins accepted 001A storage/state/callback/Home control exactly and rechecks surrounding ownership/listener behavior without rewriting any 001A evidence.",
};

fs.writeFileSync(path.join(here, "preservation-results.json"), `${JSON.stringify(report, null, 2)}\n`);
console.log(JSON.stringify({ result: report.result, unchangedCallbacks: callbacks.length }, null, 2));
