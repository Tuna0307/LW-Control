globalThis.GameAssetImage = ()=>null; // Actual image nodes separately source/pixel-verified; historical predicates do not inspect asset rendering.
import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { Fragment, compile, flatten, h, hooks, jsx, read, text } from "file:///C:/Users/chimw/OneDrive/Desktop/Github/LW-Control/evidence/lwbridge-0.3.17/ui/LWB317-UI-AUTOMATION-AFK-CLOSEOUT-001/harness.mjs";
import en from "file:///C:/Users/chimw/OneDrive/Desktop/Github/LW-Control/src/LWBridge.UI-0.3.17/src/locales/en.js";
import ja from "file:///C:/Users/chimw/OneDrive/Desktop/Github/LW-Control/src/LWBridge.UI-0.3.17/src/locales/ja.js";
import {
  formatDiagnosticBytes,
  previewUpdateStatus,
  updateDownloadVisible,
  updateManualCooldownSeconds,
  updateStatusBusy,
} from "file:///C:/Users/chimw/OneDrive/Desktop/Github/LW-Control/src/LWBridge.UI-0.3.17/src/previewRemainingPagesContracts.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const settingsSource = read("evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/SettingsPanel-DqxIWv_E.js");
const indexSource = read("evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js");
const pages = ["CityLayoutPage.jsx","HotkeyPages.jsx","SettingsPage.jsx","Pages.jsx"].map(file=>read("src/LWBridge.UI-0.3.17/src/"+file).replace(/^import .*?;\r?\n/gm,"" )).join("\n");
const contracts = read("src/LWBridge.UI-0.3.17/src/previewRemainingPagesContracts.js");
const hash = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
const tFor = (catalog) => (key, vars = {}) => String(catalog[key] ?? key).replace(/\{(\w+)\}/g, (match, name) => String(vars[name] ?? match));
const t = tFor(en);
const results = [];
const record = (name, count) => results.push({ name, count, result: "PASS" });

const originalFormatBytes = compile(settingsSource, "f");
const byteCases = [0, 1, 1023, 1024, 1536, 1024 * 1024 - 1, 1024 * 1024, 4_404_019, 50 * 1024 * 1024];
for (const bytes of byteCases) assert.equal(formatDiagnosticBytes(bytes), originalFormatBytes(bytes), `${bytes}`);
record("exact recovered diagnostic byte formatter", byteCases.length);

function ToggleStub() {}
function FeedbackStub() {}
function UpdateStub() {}

function renderOriginalSettings({ catalog = en, visual = { showFps: false, showPing: false, untouched: "keep" }, busy = false, error = "", showProfileFocus = true, focus = true, save = async (next) => next }) {
  const hook = hooks([visual, busy, error]);
  const OriginalSettings = compile(settingsSource, "m", {
    u: hook,
    d: jsx,
    o: () => ({ t: tFor(catalog) }),
    r: async () => visual,
    l: save,
    c: ToggleStub,
    p: FeedbackStub,
    s: UpdateStub,
  });
  const render = () => {
    hook.begin();
    return OriginalSettings({ showProfileFocus, focusGameOnProfileSelect: focus, onFocusGameOnProfileSelectChange: () => {} });
  };
  return { hook, render };
}

let runner = renderOriginalSettings({});
let tree = runner.render();
let toggles = flatten(tree).filter((node) => node.type === ToggleStub);
assert.equal(toggles.length, 3);
assert.equal(toggles[0].props.checked, false);
assert.equal(toggles[1].props.checked, false);
assert.equal(toggles[0].props.disabled, false);
assert.equal(toggles[2].props.checked, true);
record("actual recovered Settings visual/profile populated renderer", 5);

runner = renderOriginalSettings({ visual: null, error: t("settings.visualMetrics.loadFailed"), showProfileFocus: false });
tree = runner.render();
assert.equal(flatten(tree).filter((node) => node.type === ToggleStub).length, 0);
assert.ok(text(tree).includes(t("settings.visualMetrics.loadFailed")));
record("actual recovered Settings visual load-error renderer", 2);

runner = renderOriginalSettings({ busy: true, showProfileFocus: false });
tree = runner.render();
toggles = flatten(tree).filter((node) => node.type === ToggleStub);
assert.equal(toggles.length, 2);
assert.ok(toggles.every((node) => node.props.disabled === true));
record("actual recovered Settings shared visual busy gate", 2);

function renderOriginalFeedback(phase, result, progress, catalog = en) {
  const hook = hooks([phase, result, progress, { current: "" }]);
  const Feedback = compile(settingsSource, "p", {
    u: hook,
    d: jsx,
    o: () => ({ t: tFor(catalog) }),
    e: () => () => {},
    a: async () => ({}),
    f: originalFormatBytes,
    crypto: { randomUUID: () => "preview" },
  });
  hook.begin();
  return Feedback();
}

tree = renderOriginalFeedback("exporting", null, { exportId: "x", state: "finalizing", processedBytes: 92, totalBytes: 100, percent: 92 });
assert.ok(text(tree).includes(t("feedback.progress.finalizing")));
assert.ok(text(tree).includes("92%"));
assert.equal(flatten(tree).filter((node) => node.type === "progress")[0].props.value, 92);
assert.equal(flatten(tree).filter((node) => node.type === "button")[0].props.disabled, true);
tree = renderOriginalFeedback("success", { archiveBytes: 4_404_019, path: "Preview/diagnostics.zip" }, null);
assert.ok(text(tree).includes(t("feedback.success", { size: formatDiagnosticBytes(4_404_019), path: "Preview/diagnostics.zip" })));
tree = renderOriginalFeedback("error", null, null);
assert.ok(text(tree).includes(t("feedback.failed")));
record("actual recovered feedback exporting/success/error renderer", 6);

const updateBase = {
  phase: "idle",
  currentVersion: "0.3.17",
  latestVersion: null,
  releaseNotes: "",
  publishedAt: null,
  progress: null,
  message: null,
  nextManualCheckAt: null,
  downloadDirectory: "Preview/updates",
};

function renderOriginalUpdater(status, now = Date.parse("2026-10-03T10:00:00.000Z"), catalog = en) {
  const hook = hooks([status, now]);
  const Updater = compile(indexSource, "Rn", {
    b: hook,
    M: jsx,
    De: () => ({ language: catalog === ja ? "ja" : "en", t: tFor(catalog) }),
    Ln: updateBase,
    dt: () => () => {},
    ct: async () => status,
    lt: async () => status,
    ut: async () => status,
    window: { setInterval: () => 1, clearInterval: () => {} },
  });
  hook.begin();
  return Updater({ compact: false });
}

const updaterCases = [
  previewUpdateStatus("settings-update-checking"),
  previewUpdateStatus("settings-update-available"),
  previewUpdateStatus("settings-update-downloading"),
  previewUpdateStatus("settings-update-opening"),
  previewUpdateStatus("settings-update-error"),
];
for (const status of updaterCases) {
  tree = renderOriginalUpdater(status);
  assert.ok(text(tree).includes(t("update.currentVersion", { version: "0.3.17" })));
  assert.equal(updateStatusBusy(status), ["checking", "downloading", "opening"].includes(status.phase));
  assert.equal(updateDownloadVisible(status), status.phase === "available" || (status.phase === "error" && status.latestVersion !== null && status.latestVersion !== status.currentVersion));
}
record("actual recovered updater renderer and current predicates across phases", updaterCases.length * 3);

const cooldownNow = Date.parse("2026-10-03T10:00:00.000Z");
const cooldownStatus = { ...updateBase, phase: "upToDate", latestVersion: "0.3.17", nextManualCheckAt: new Date(cooldownNow + 30_000).toISOString() };
tree = renderOriginalUpdater(cooldownStatus, cooldownNow);
const originalCheckButton = flatten(tree).filter((node) => node.type === "button")[0];
assert.equal(updateManualCooldownSeconds(cooldownStatus, cooldownNow), 30);
assert.equal(originalCheckButton.props.disabled, true);
assert.equal(text(originalCheckButton), t("update.checkCooldown", { seconds: 30 }));
record("exact recovered manual update cooldown presentation", 3);

const SETTINGS_PREVIEW_STATES = new Set([
  "settings-multiprofile", "settings-complete", "settings-visual-loading", "settings-visual-error", "settings-visual-saving", "settings-visual-save-error",
  "settings-feedback", "settings-feedback-preparing", "settings-feedback-exporting", "settings-feedback-finalizing", "settings-feedback-success", "settings-feedback-error", "settings-feedback-canceled",
  "settings-update-idle", "settings-update-checking", "settings-update-available", "settings-update-downloading", "settings-update-opening", "settings-update-error", "settings-update-check-error", "settings-update-download-error", "settings-update-cooldown",
]);
const initialFeedbackState = compile(pages, "initialFeedbackState");

function renderProductionSettings(previewState, catalog = en) {
  const hook = hooks();
  const fakeWindow = { setInterval: () => 1, clearInterval: () => {} };
  const Settings = compile(pages, "SettingsPage", {
    h, Fragment, ...hook,
    useI18n: () => ({ language: catalog === ja ? "ja" : "en", t: tFor(catalog) }),
    SETTINGS_PREVIEW_STATES,
    initialFeedbackState,
    PanelTitle: "PanelTitle",
    ToggleRow: "ToggleRow",
    formatDiagnosticBytes,
    previewUpdateStatus,
    updateDownloadVisible,
    updateManualCooldownSeconds,
    updateStatusBusy,
    window: fakeWindow,
  });
  const render = () => {
    hook.begin();
    return Settings({ previewState, showProfileFocus: false, focusGameOnProfileSelect: true, onFocusGameOnProfileSelectChange: null });
  };
  return { hook, render };
}

runner = renderProductionSettings("settings-complete");
tree = runner.render();
toggles = flatten(tree).filter((node) => node.type === "ToggleRow");
assert.equal(toggles.length, 3);
toggles[0].props.onChange(true);
await Promise.resolve();
await Promise.resolve();
tree = runner.render();
toggles = flatten(tree).filter((node) => node.type === "ToggleRow");
assert.equal(toggles[0].props.checked, true);
assert.ok(toggles.slice(0, 2).every((node) => node.props.disabled === false));
record("production visual save success and profile row callbacks", 4);

runner = renderProductionSettings("settings-visual-save-error", ja);
tree = runner.render();
toggles = flatten(tree).filter((node) => node.type === "ToggleRow");
toggles[0].props.onChange(true);
await Promise.resolve();
await Promise.resolve();
tree = runner.render();
toggles = flatten(tree).filter((node) => node.type === "ToggleRow");
assert.equal(toggles[0].props.checked, false);
assert.ok(text(tree).includes(tFor(ja)("settings.visualMetrics.saveFailed")));
record("production visual optimistic rollback/error in Japanese", 2);

runner = renderProductionSettings("settings-feedback-canceled");
tree = runner.render();
let button = flatten(tree).filter((node) => node.type === "button").find((node) => text(node) === t("feedback.export"));
button.props.onClick();
await Promise.resolve();
await Promise.resolve();
tree = runner.render();
assert.ok(!flatten(tree).some((node) => node.props?.className === "feedback-export-progress"));
assert.ok(flatten(tree).filter((node) => node.type === "button").some((node) => text(node) === t("feedback.export")));
record("production feedback canceled-to-idle callback", 2);

runner = renderProductionSettings("settings-feedback-success");
tree = runner.render();
assert.ok(text(tree).includes(t("feedback.success", { size: formatDiagnosticBytes(4_404_019), path: "Preview/diagnostics.zip" })));
record("production feedback exact byte-sized success", 1);

runner = renderProductionSettings("settings-update-available");
tree = runner.render();
assert.ok(text(tree).includes("Preview release notes for 0.3.18."));
assert.ok(text(tree).includes(t("update.downloadDirectory", { path: "Preview/updates" })));
button = flatten(tree).filter((node) => node.type === "button").find((node) => text(node) === t("update.downloadAndOpen"));
assert.ok(button);
button.props.onClick();
await Promise.resolve();
await Promise.resolve();
tree = runner.render();
assert.ok(text(tree).includes(t("update.opening")));
record("production updater metadata/download-to-opening callback", 4);

runner = renderProductionSettings("settings-update-cooldown");
tree = runner.render();
button = flatten(tree).filter((node) => node.type === "button").find((node) => text(node).includes("30"));
assert.ok(button);
assert.equal(button.props.disabled, true);
record("production updater cooldown gate", 2);

runner = renderProductionSettings("settings-update-error", ja);
tree = runner.render();
assert.ok(text(tree).includes(tFor(ja)("update.error.UPDATE_STATUS_FAILED")) || text(tree).includes(tFor(ja)("update.error.default")));
assert.ok(flatten(tree).filter((node) => node.type === "button").some((node) => text(node) === tFor(ja)("update.downloadAndOpen")));
record("production updater translated error with newer-version download", 2);

const sourceGuards = [
  "visualBusy",
  "previousVisual",
  "settings.visualMetrics.saveFailed",
  "feedback.progress.${feedbackProgress.state}",
  "feedbackProgress.percent",
  "update.publishedAt",
  "update.downloadDirectory",
  "update.checkCooldown",
  "updateBusy",
  "showUpdateDownload",
  "update.downloadAndOpen",
];
for (const guard of sourceGuards) assert.ok(pages.includes(guard), `Missing production guard ${guard}`);
assert.ok(!pages.includes('t("update.releaseNotes")'));
record("production Settings source ownership guards", sourceGuards.length + 1);

const report = {
  result: "LWB317_SETTINGS_SOURCE_LOCAL_OK",
  results,
  current: { pagesSha256: hash(pages), contractsSha256: hash(contracts) },
  limits: "Recovered Settings/Feedback/Updater renderers and helper predicates are executed directly. Current export and updater actions settle preview-local state only; no diagnostic archive is written and no update is downloaded, opened, installed or restarted.",
};
if (process.argv.includes("--record")) fs.writeFileSync(path.join(here, "results.json"), `${JSON.stringify(report, null, 2)}\n`);
console.log(JSON.stringify(report, null, 2));
