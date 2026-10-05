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
const sha = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
const read = (relative) => fs.readFileSync(path.join(repo, relative), "utf8");
const readJson = (file) => JSON.parse(fs.readFileSync(file, "utf8"));

const assetRelative = "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js";
const assetBuffer = fs.readFileSync(path.join(repo, assetRelative));
const asset = assetBuffer.toString("utf8");
assert.equal(sha(assetBuffer), "44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6");
assert.equal(sha(fs.readFileSync("C:/Users/chimw/OneDrive/Desktop/Github/LW/lwbridge-0.3.17.exe")), "4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783");

const ast = parse(asset, { sourceType: "module" });
const functions = ast.program.body.filter((node) => node.type === "FunctionDeclaration");
const byteOffset = (index) => Buffer.byteLength(asset.slice(0, index));
const locatorAt = (name, expectedOffset) => {
  const node = functions.find((candidate) => candidate.id?.name === name && byteOffset(candidate.start) === expectedOffset);
  assert.ok(node, `missing exact ${name} at UTF-8 byte ${expectedOffset}`);
  const text = asset.slice(node.start, node.end);
  return { name, utf8ByteOffset: expectedOffset, utf8ByteLength: Buffer.byteLength(text), sha256: sha(text), text };
};

const source = {
  layout: locatorAt("Fn", 208687),
  dialog: locatorAt("In", 209017),
  home: locatorAt("qr", 336694),
  profiles: locatorAt("Yr", 339285),
  nav: locatorAt("Qr", 349778),
  header: locatorAt("si", 354710),
  shell: locatorAt("Gi", 361306),
  exit: locatorAt("Ji", 376111),
};

for (const token of ["Fn", "si", "Yr", "Qr", "b.Suspense", "profile-view-context", "Oe", "profile-switch-loading", "qr", "b.Activity"]) {
  assert.ok(source.shell.text.includes(token), `exact Gi missing ${token}`);
}
assert.ok(source.dialog.text.includes("preventDefault") && source.dialog.text.includes("Tab"), "exact shared dialog event rules missing");
assert.ok(source.nav.text.includes("onMouseEnter") || source.nav.text.includes("onPointerEnter") || source.nav.text.includes("onFocus"), "exact nav preload events missing");
assert.ok(source.layout.text.includes("profile-sidebar") && source.layout.text.includes("app-layout"), "exact Fn profile rail/layout ancestry missing");
const rootExitSlice = assetBuffer.subarray(377000, 377600).toString("utf8");
assert.ok(rootExitSlice.includes("Gi,{initialTheme:Yi}") && rootExitSlice.includes("qi,{})"), "exact root Gi/qi sibling composition missing");

const currentFiles = [
  "src/LWBridge.UI-0.3.17/src/App.jsx",
  "src/LWBridge.UI-0.3.17/src/HomePage.jsx",
  "src/LWBridge.UI-0.3.17/src/ShellPresentation.jsx",
  "src/LWBridge.UI-0.3.17/src/ProfileSidebar.jsx",
  "src/LWBridge.UI-0.3.17/src/ProfileSwitchState.jsx",
  "src/LWBridge.UI-0.3.17/src/AppExitDialog.jsx",
  "src/LWBridge.UI-0.3.17/src/sharedPageUI.jsx",
  "src/LWBridge.UI-0.3.17/src/NavIcon.jsx",
  "src/LWBridge.UI-0.3.17/src/shellState.js",
  "src/LWBridge.UI-0.3.17/src/profileConfigDraft.js",
];
const currentHashes = Object.fromEntries(currentFiles.map((relative) => [relative, sha(fs.readFileSync(path.join(repo, relative)))]));
const app = read(currentFiles[0]);
const home = read(currentFiles[1]);
const profile = read(currentFiles[3]);
const exit = read(currentFiles[5]);

const ancestry = [
  '<header className="top-bar">',
  "app-layout",
  '<aside className="profile-sidebar">',
  "<ProfileSidebar",
  '<nav className="side-nav"',
  '<section className="main-view">',
  "<Suspense",
  '<div className="profile-view-context">',
  "<ShellConfigSaveErrors",
  "<ProfileSwitchState",
  "<RetainedPages",
];
let previous = -1;
for (const token of ancestry) {
  const at = app.indexOf(token);
  assert.ok(at > previous, `current App shell ancestry missing/out of order: ${token}`);
  previous = at;
}
const mainClose = app.lastIndexOf("</main>");
const exitPrompt = app.indexOf("<AppExitPrompt");
const previewExit = app.indexOf("<AppExitDialog", exitPrompt);
assert.ok(mainClose >= 0 && exitPrompt > mainClose && previewExit > exitPrompt, "current exit prompt/dialog must be siblings after app-shell");
assert.ok(app.includes('onMouseEnter={() => preloadRoute(route.key)}') && app.includes('onFocus={() => preloadRoute(route.key)}'), "current nav preload rules missing");
assert.ok(app.includes('activeCategory: automationCategory') && app.includes('activeTab: mapTab') && app.includes('activeTab: squadTab'), "current parent-owned route selections missing");
assert.ok(app.includes('[autoWeekendShieldStore, autoAttackShieldStore, autoReconnectStore, autoClosePopupStore]'), "current four shell flag stores missing/reordered");
assert.ok(app.includes('"auto_weekend_shield",') && app.includes('"auto_attack_shield",') && app.includes('"auto_close_popup",'), "current shell flag source keys missing");
assert.ok(app.includes('previewProfileCache.current.has(id)') && app.includes('setPreviewProfileLoading(!cached)') && app.includes('previewProfileCache.current.add(id)'), "current local uncached/cached profile transition missing");
assert.ok(profile.includes("event.preventDefault(); if (!busy) onClose();") && profile.includes('event.key !== "Tab"'), "current profile modal event rules changed");
assert.ok(exit.includes("event.preventDefault(); if (!busy) onClose?.();") && exit.includes('event.key !== "Tab"'), "current exit modal event rules changed");
assert.ok(home.includes("const lifecycleProviderAvailable = false"), "Home native lifecycle fence changed");
assert.ok(home.includes("disabled={state.autoLaunchGame == null || !state.production}") && home.includes("disabled={state.autoReconnect == null || !state.production}"), "Home preference availability fences changed");
const autoLaunchBody = app.slice(app.indexOf("const updateAutoLaunch"), app.indexOf("const updateAutoReconnect"));
assert.ok(!autoLaunchBody.includes('setGameActionError("")'), "Auto Launch must not clear shared lifecycle action error");
const refreshBody = app.slice(app.indexOf("const refreshStatus"), app.indexOf("const selectRoute"));
assert.ok(!refreshBody.includes("game_recovery_status"), "recurring refresh must not own recovery state");
assert.ok(app.includes('backendBridge.invoke("game_recovery_status", { profileId })'), "selected-profile initial recovery read missing");

const homeRoot = path.join(here, "home-source-render");
const render = readJson(path.join(homeRoot, "render-results.json"));
assert.equal(render.marker, "LWB317_REMAINING_M4_HOME_SOURCE_RENDER_BUILT");
assert.equal(render.reference.sha256.toUpperCase(), sha(assetBuffer));
assert.equal(render.cases, 42);
assert.equal(render.matched, 34);
for (const entry of render.current) assert.equal(entry.sha256.toUpperCase(), currentHashes[entry.path], `stale Home source render: ${entry.path}`);
const expectedDifferences = [
  "en/repair", "en/running-connected", "en/stopped", "en/unavailable",
  "ja/repair", "ja/running-connected", "ja/stopped", "ja/unavailable",
];
const actualDifferences = render.records.filter((record) => record.differences.length).map((record) => `${record.language}/${record.name}`).sort();
assert.deepEqual(actualDifferences, expectedDifferences);
for (const record of render.records.filter((entry) => entry.differences.length)) {
  for (const difference of record.differences) {
    const stripDisabled = (entry) => ({ ...entry, attrs: entry.attrs.filter(([name]) => name !== "disabled") });
    assert.deepEqual(stripDisabled(difference.actual), stripDisabled(difference.expected), `${record.language}/${record.name} has non-fence structural difference`);
    assert.ok(difference.actual.attrs.some(([name]) => name === "disabled"), `${record.language}/${record.name} current side is not the fenced control`);
    assert.ok(!difference.expected.attrs.some(([name]) => name === "disabled"), `${record.language}/${record.name} original side unexpectedly disabled`);
  }
}

const pixelPacket = readJson(path.join(homeRoot, "pixels/pixel-results.json"));
const pairPacket = readJson(path.join(homeRoot, "pixels/pairs.json"));
assert.equal(pixelPacket.pairs, 40);
assert.equal(pixelPacket.exactRequired, 24);
assert.equal(pixelPacket.exactRequiredPass, 24);
assert.equal(pixelPacket.availabilityFencePairs, 16);
assert.equal(pixelPacket.availabilityFenceChanged, 16);
assert.equal(pairPacket.errors.length, 0);
for (const row of pixelPacket.rows) {
  assert.deepEqual(row.currentRoot, row.originalRoot, `${row.id} Home root geometry differs`);
  if (row.classification === "exact-required") assert.equal(row.changedPixels, 0, `${row.id} should be pixel exact`);
  else assert.ok(row.changedPixels > 0, `${row.id} expected visible disabled-state fence`);
}

// Re-decode every saved PNG pair without mutating evidence.
const python = String.raw`
import json, sys
from pathlib import Path
from PIL import Image, ImageChops
import numpy as np
root=Path(sys.argv[1])
packet=json.loads((root/'pairs.json').read_text(encoding='utf-8'))
rows=[]
for pair in packet['pairs']:
    a=Image.open(root/pair['original']['screenshot']).convert('RGB')
    b=Image.open(root/pair['current']['screenshot']).convert('RGB')
    size=(max(a.width,b.width),max(a.height,b.height))
    aa=Image.new('RGB',size,(255,0,255));bb=Image.new('RGB',size,(0,255,255));aa.paste(a,(0,0));bb.paste(b,(0,0))
    changed=int(np.any(np.asarray(ImageChops.difference(aa,bb)),axis=2).sum())
    rows.append([pair['id'],changed])
print(json.dumps(rows))
`;
const decoded = JSON.parse(execFileSync("python", ["-c", python, path.join(homeRoot, "pixels")], { encoding: "utf8" }));
const recordedPixels = new Map(pixelPacket.rows.map((row) => [row.id, row.changedPixels]));
for (const [id, changed] of decoded) assert.equal(changed, recordedPixels.get(id), `pixel decode changed: ${id}`);

const browserPacket = readJson(path.join(here, "browser-current-results.json"));
assert.equal(browserPacket.marker, "LWB317_REMAINING_M4_BROWSER_CURRENT_OK");
assert.equal(browserPacket.cases.length, 124);
assert.equal(browserPacket.screenshots.length, 18);
assert.equal(browserPacket.consoleIssues.length, 0);
for (const requiredCase of [
  "uncached local profile selection enters recovered loading branch",
  "cached Local 1 return bypasses loading",
  "cached Local 2 return bypasses loading",
  "exit dialog is recovered sibling of app-shell",
  "en-light-desktop/home-running-disconnected Home fixture identity",
  "ja-dark-narrow/home-running-disconnected Home fixture identity",
]) assert.ok(browserPacket.cases.some((entry) => entry.name === requiredCase), `missing browser case: ${requiredCase}`);
for (const [relative, expected] of Object.entries(browserPacket.sourceFiles)) assert.equal(currentHashes[relative], expected, `stale M4 browser packet: ${relative}`);
for (const screenshot of browserPacket.screenshots) {
  const bytes = fs.readFileSync(path.join(here, "browser-current", screenshot.file));
  assert.equal(sha(bytes), screenshot.sha256);
  assert.equal(bytes.length, screenshot.length);
}

// Bind the still-current exact isolated component packets without rewriting them.
const profileResult = readJson(path.join(repo, "evidence/lwbridge-0.3.17/ui/LWB317-UI-FINAL-INTEGRATION-001/profiles/results.json"));
const exitResult = readJson(path.join(repo, "evidence/lwbridge-0.3.17/ui/LWB317-UI-FINAL-INTEGRATION-001/profiles/exit-results.json"));
const switchResult = readJson(path.join(repo, "evidence/lwbridge-0.3.17/ui/LWB317-UI-FINAL-INTEGRATION-001/profiles/switch-results.json"));
assert.equal(profileResult.cases, 98); assert.ok(profileResult.details.every((entry) => entry.pass));
assert.equal(profileResult.productionSha256, currentHashes["src/LWBridge.UI-0.3.17/src/ProfileSidebar.jsx"]);
assert.equal(exitResult.cases, 33); assert.ok(exitResult.details.every((entry) => entry.pass));
assert.equal(exitResult.productionSha256, currentHashes["src/LWBridge.UI-0.3.17/src/AppExitDialog.jsx"]);
assert.equal(switchResult.cases, 12); assert.ok(switchResult.details.every((entry) => entry.pass));
assert.equal(switchResult.productionSha256, currentHashes["src/LWBridge.UI-0.3.17/src/ProfileSwitchState.jsx"]);

const report = {
  marker: "LWB317_REMAINING_M4_VALIDATION_OK",
  source: Object.fromEntries(Object.entries(source).map(([key, value]) => [key, { utf8ByteOffset: value.utf8ByteOffset, utf8ByteLength: value.utf8ByteLength, sha256: value.sha256 }])),
  current: currentHashes,
  home: { sourceCases: 42, exactStructure: 34, availabilityFenceCases: 8, pixelPairs: 40, exactRequiredPixels: "24/24", fencePixelPairs: 16 },
  exactIsolated: { profiles: 98, exit: 33, profileSwitch: 12 },
  browser: { assertions: browserPacket.cases.length, screenshots: browserPacket.screenshots.length, consoleIssues: 0 },
  limits: [
    "Home Start/Stop/Repair and production-unavailable preference controls remain explicitly disabled because no verified native lifecycle/provider is connected; exact original geometry/text is otherwise retained and the difference is bounded to disabled attributes.",
    "Historical Home packets pin earlier App/component hashes and remain immutable; this task-local exact renderer/browser packet is the current source/local evidence.",
  ],
};
fs.writeFileSync(path.join(here, "validation-results.json"), JSON.stringify(report, null, 2) + "\n");
console.log(JSON.stringify(report, null, 2));
