import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { fileURLToPath } from "node:url";
import { require, read, compile, fn, raw, nodes, squad } from "../../LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-c/accepted-harness.mjs";
import * as fixtures from "../../../../../src/LWBridge.UI-0.3.17/src/previewAfkCloseoutFixtures.js";
import { refreshGarrisonTargets } from "../../../../../src/LWBridge.UI-0.3.17/src/previewAfkContracts.js";
import currentEn from "../../../../../src/LWBridge.UI-0.3.17/src/locales/en.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const React = require("react");
const jsx = require("react/jsx-runtime");
const { renderToStaticMarkup } = require("react-dom/server");
const h = React.createElement;
const Fragment = React.Fragment;
const hash = (value) => crypto.createHash("sha256").update(value).digest("hex");

const main = read("evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js");
const card = read("evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/AutomationCard-LCx_jIi7.js");
const sourceCss = read("evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-rIL9Fpht.css");
const currentCss = read("src/LWBridge.UI-0.3.17/src/reference.css");
const currentSquads = read("src/LWBridge.UI-0.3.17/src/SquadsPage.jsx");
const sharedUi = read("src/LWBridge.UI-0.3.17/src/sharedPageUI.jsx");

const localeAsset = read("evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/en-BisSXcTB.js");
const localeNode = nodes(localeAsset).find((node) => node.type === "VariableDeclarator" && node.id?.name === "t");
const sourceCatalog = new Function("e", `return (${raw(localeAsset, localeNode.init)});`)({});
const interpolate = (catalog) => (key, vars = {}) => String(catalog[key] ?? key).replace(/\{(\w+)\}/g, (match, name) => vars[name] ?? match);
const sourceT = interpolate(sourceCatalog);
const currentT = interpolate(currentEn);

const SourceSwitch = compile(main, "zn", { M: jsx });
const SourceToggle = compile(main, "Bn", { M: jsx, De: () => ({ t: sourceT }), zn: SourceSwitch });
const SourceIcon = compile(main, "Vr", { M: jsx });
const SourceCompact = compile(card, "l", { s: jsx, i: SourceIcon });
const SourceTargetKey = compile(squad, "M");
const SourceReorder = compile(squad, "de");
const SourceRefresh = compile(squad, "N");
const SourceBuildingName = compile(squad, "fe");

const sourceStore = (draft) => ({
  draft,
  confirmed: structuredClone(draft),
  error: null,
  state: {
    getSnapshot: () => ({ draft, confirmed: draft }),
    edit() {}, flush: async () => draft, refresh: async () => draft,
    runAction: async (action) => typeof action === "function" ? action() : undefined,
  },
});
const sourceDeps = {
  A: jsx, O: React, d: () => ({ language: "en", t: sourceT }), c: () => 1,
  j: { enabled: false, recallOnDisable: true, squadPriority: [], targets: [] },
  M: SourceTargetKey, de: SourceReorder, N: SourceRefresh, fe: SourceBuildingName,
  m: async () => ({}), S: async () => ({}), te: async () => ({ buildings: [], allies: [] }),
  f: async () => ({ squads: [] }), u: async () => ({}), C: async () => ({ tasks: {} }),
  re: async () => ({}), b: async () => ({}), _: (_t, _error) => sourceT("common.actionFailed"),
  w: SourceToggle, T: SourceIcon, ne: () => null, y: () => null,
};

const SourceGarrison = compile(squad, "pe", {
  ...sourceDeps,
  e: (_scope, initial) => sourceStore(initial),
});
const SourceZombieTable = compile(squad, "me", { A: jsx, d: () => ({ t: sourceT }) });
const SourceZombie = compile(squad, "he", {
  A: jsx, O: React, d: () => ({ language: "en", t: sourceT }), c: () => 1,
  e: (_scope, initial) => sourceStore(initial), m: async () => ({}), D: async () => ({}), C: async () => ({ tasks: {} }), u: async () => ({}),
  ie: SourceCompact, me: SourceZombieTable, ne: () => null,
});

const CurrentSettingsGlyph = compile(currentSquads, "SettingsGlyph", { h, Fragment });
const CurrentCompact = compile(currentSquads, "CompactAfkCard", { h, Fragment, useI18n: () => ({ t: currentT }), SettingsGlyph: CurrentSettingsGlyph });
const CurrentSwitch = compile(sharedUi, "Switch", { h, Fragment });
const CurrentToggle = compile(sharedUi, "ToggleRow", { h, Fragment, useI18n: () => ({ t: currentT }), Switch: CurrentSwitch });
const CurrentGarrison = compile(currentSquads, "GarrisonPreviewSettings", {
  h, Fragment,
  useState: React.useState, useEffect: React.useEffect, useRef: React.useRef,
  useI18n: () => ({ t: currentT }),
  previewGarrisonRuntime: fixtures.previewGarrisonRuntime,
  previewGarrisonMemberFixture: fixtures.previewGarrisonMemberFixture,
  refreshGarrisonTargets,
  ToggleRow: CurrentToggle,
  PreviewConfigError: () => null,
});
const CurrentZombie = compile(currentSquads, "ZombieBusPreviewSettings", {
  h, Fragment, useI18n: () => ({ t: currentT }), previewZombieBusRuntime: fixtures.previewZombieBusRuntime, PreviewConfigError: () => null,
});

const pairDir = path.join(here, "composition-pairs");
fs.mkdirSync(pairDir, { recursive: true });
const cases = [];

function htmlPage(markup, css) {
  return `<!doctype html><html lang="en" data-theme="light"><head><meta charset="utf-8"><style>${css}</style></head><body><section class="panel squad-panel"><div class="monster-afk-layout"><div class="monster-afk-toolbar">${markup}</div></div></section></body></html>`;
}

{
  const state = "squads-profile-garrison-discovery-pending";
  const config = fixtures.initialAfkToolbarConfig(state).garrison;
  const original = renderToStaticMarkup(h(SourceGarrison, { online: true, serverId: 321, config, open: true, onOpenChange() {}, onLog() {} }));
  const runtime = fixtures.previewGarrisonRuntime(state);
  const current = renderToStaticMarkup(h(Fragment, null,
    h(CurrentCompact, {
      title: currentT("garrison.title"), description: currentT("garrison.description"),
      summary: currentT("garrison.summary", { active: runtime.guardingCount, total: Math.min(config.squadPriority.length, config.targets.length) }),
      enabled: config.enabled, disabled: false, settingsOpen: true, onSettings() {}, onToggle() {},
    }),
    h(CurrentGarrison, { disabled: false, value: config, previewState: state, config: null, actionError: "", availableSquads: [], onChange() {} }),
  ));
  cases.push({ id: "garrison-pending", original, current, allowedDifference: "Current Run Now remains disabled/presentation-only because the native action provider is intentionally fenced." });
}

{
  const state = "squads-profile-zombie-waiting";
  const config = fixtures.initialAfkToolbarConfig(state).zombieBus;
  const original = renderToStaticMarkup(h(SourceZombie, { online: true, config, open: true, onOpenChange() {}, onLog() {} }));
  const current = renderToStaticMarkup(h(Fragment, null,
    h(CurrentCompact, {
      title: currentT("zombieBus.title"), description: currentT("zombieBus.description"), summary: currentT("zombieBus.waiting"),
      enabled: config.enabled, disabled: false, settingsOpen: true, onSettings() {}, onToggle() {},
    }),
    h(CurrentZombie, { previewState: state, open: true, config: null }),
  ));
  cases.push({ id: "zombie-waiting", original, current, allowedDifference: "None expected for this source-local waiting composition." });
}

for (const item of cases) for (const side of ["original", "current"]) {
  const markup = item[side];
  const css = side === "original" ? sourceCss : currentCss;
  fs.writeFileSync(path.join(pairDir, `${item.id}-${side}.html`), htmlPage(markup, css));
}

const sourceSlices = ["pe", "he"].map((name) => {
  const node = fn(squad, name), source = raw(squad, node);
  return { name, utf8ByteOffset: Buffer.byteLength(squad.slice(0, node.start)), utf8ByteLength: Buffer.byteLength(source), sha256: hash(source) };
});
fs.writeFileSync(path.join(here, "composition-pair-render.json"), `${JSON.stringify({
  marker: "LWB317_REMAINING_M2_COMPOSITION_PAIR_RENDER",
  cases: cases.map(({ id, original, current, allowedDifference }) => ({ id, allowedDifference, originalHtmlSha256: hash(original), currentHtmlSha256: hash(current), rawEqual: original === current })),
  sourceSlices,
  scope: "Executed exact recovered pe/he with React SSR under inert initial source-valid inputs and actual canonical current CompactAfkCard/GarrisonPreviewSettings/ZombieBusPreviewSettings. Source/current CSS and locale catalogs are independent.",
}, null, 2)}\n`);
console.log(JSON.stringify({ marker: "LWB317_REMAINING_M2_COMPOSITION_PAIR_RENDER", cases: cases.length, rawEqual: cases.filter((item) => item.original === item.current).length }, null, 2));
