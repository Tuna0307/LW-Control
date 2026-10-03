import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { fn, nodes, raw, read } from "../../LWB317-UI-AUTOMATION-AFK-CLOSEOUT-001/harness.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const taskRoot = path.dirname(here);
const repo = path.resolve(here, "../../../../..");
const hash = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();

const assets = {
  city: "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/CityLayoutPanel-DoNWkywK.js",
  hotkey: "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/HotkeyPanel-XA8idRHB.js",
  settings: "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/SettingsPanel-DqxIWv_E.js",
  index: "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js",
};
const source = Object.fromEntries(Object.entries(assets).map(([key, value]) => [key, read(value)]));

function locator(asset, text, node) {
  return {
    asset,
    utf8ByteOffset: Buffer.byteLength(text.slice(0, node.start)),
    utf8ByteLength: Buffer.byteLength(raw(text, node)),
    sha256: hash(raw(text, node)),
  };
}

function needleLocator(asset, text, needle) {
  const start = text.indexOf(needle);
  assert.ok(start >= 0, `Missing source needle: ${needle.slice(0, 80)}`);
  return {
    asset,
    utf8ByteOffset: Buffer.byteLength(text.slice(0, start)),
    utf8ByteLength: Buffer.byteLength(needle),
    sha256: hash(needle),
  };
}

function functionLocator(asset, text, name) {
  const node = fn(text, name);
  assert.ok(node, `Missing recovered function ${name}`);
  return locator(asset, text, node);
}

function functionContaining(asset, text, marker) {
  const node = nodes(text).find((entry) => entry.type === "FunctionDeclaration" && raw(text, entry).includes(marker));
  assert.ok(node, `Missing function containing ${marker}`);
  return locator(asset, text, node);
}

const locators = {
  "City.te.placementMap": functionLocator("CityLayoutPanel-DoNWkywK.js", source.city, "te"),
  "City.ne.setPlacement": functionLocator("CityLayoutPanel-DoNWkywK.js", source.city, "ne"),
  "City.m.occupiedAtTarget": functionLocator("CityLayoutPanel-DoNWkywK.js", source.city, "m"),
  "City.re.validate": functionLocator("CityLayoutPanel-DoNWkywK.js", source.city, "re"),
  "City.ie.regionForPoint": functionLocator("CityLayoutPanel-DoNWkywK.js", source.city, "ie"),
  "City.oe.gridRow": functionLocator("CityLayoutPanel-DoNWkywK.js", source.city, "oe"),
  "City.ue.panel": functionLocator("CityLayoutPanel-DoNWkywK.js", source.city, "ue"),
  "Hotkey.h.sheepStatusKey": functionLocator("HotkeyPanel-XA8idRHB.js", source.hotkey, "h"),
  "Hotkey.g.formatSheepElapsed": functionLocator("HotkeyPanel-XA8idRHB.js", source.hotkey, "g"),
  "Hotkey._.panel": functionLocator("HotkeyPanel-XA8idRHB.js", source.hotkey, "_"),
  "Settings.f.formatBytes": functionLocator("SettingsPanel-DqxIWv_E.js", source.settings, "f"),
  "Settings.p.feedback": functionLocator("SettingsPanel-DqxIWv_E.js", source.settings, "p"),
  "Settings.m.panel": functionLocator("SettingsPanel-DqxIWv_E.js", source.settings, "m"),
  "Index.Rn.updatePanel": functionContaining("index-BVfnK1wp.js", source.index, "update.currentVersion"),
  "Index.route.miniGames": needleLocator("index-BVfnK1wp.js", source.index, "o.has(`mini-games`)&&(0,M.jsx)(b.Activity,{mode:i===`mini-games`?`visible`:`hidden`,children:(0,M.jsx)(zi,{category:`miniGames`,online:at,onLog:F,sheepStatus:Ee?.tasks?.sheepGame})})"),
};

const inventory = [
  {
    page: "City Layout",
    count: 16,
    branches: [
      "offline",
      "online loading / no-data-or-error",
      "populated header/actions",
      "stale draft warning + discard",
      "outside-city warning",
      "grid/zoom/fit/name preview",
      "box/add/toggle selection",
      "single/group pointer movement",
      "local placement rejection",
      "Escape cleanup",
      "Undo/Redo + Ctrl/Cmd shortcuts",
      "restore-initial history transition",
      "500 ms draft persistence effect",
      "server validation result",
      "apply confirm/progress/cancel/result polling",
      "inspector/changes/footer",
    ],
    locators: ["City.ue.panel", "City.ne.setPlacement", "City.m.occupiedAtTarget", "City.re.validate"],
    unavailable: "Native city layout/profile draft providers and apply job are outside this UI-only assignment; preview fixtures may supply source-shaped data and inert acknowledgements.",
  },
  {
    page: "Hotkeys",
    count: 9,
    branches: [
      "seven-card order and recovered locale keys",
      "connected/disconnected status text",
      "initial config loading",
      "config load failure",
      "boolean shortcut toggle",
      "attack item speedup edit",
      "attack diamond speedup edit",
      "single-field save pending / unrelated-field preservation",
      "save success or rollback+error",
    ],
    locators: ["Hotkey._.panel"],
    unavailable: "No OS/global listener or gameplay hotkey injection is enabled; the supplied local config interaction is preview-only.",
  },
  {
    page: "Mini Games",
    count: 13,
    branches: [
      "shared Frontline config card",
      "config loading/failure while non-config cards remain",
      "treasure chest persisted config toggle + offline/pending gate",
      "Land idle/opening/success/failure presentation",
      "Sheep running vs stopped action label",
      "Sheep current level conditional",
      "Sheep elapsed conditional",
      "Sheep planned move progress",
      "Sheep solving/executing/completed",
      "Sheep daily/all/activity-ended",
      "Sheep ui-open/manual-conflict/solve-failed/unsupported",
      "Sheep refreshing/starting/opening/initial-delay processing",
      "Sheep action failure",
    ],
    locators: ["Index.route.miniGames", "Hotkey.h.sheepStatusKey", "Hotkey.g.formatSheepElapsed", "Hotkey._.panel"],
    unavailable: "Land and Sheep native producers/actions remain inert; controlled supplied runtime state is used for UI proof.",
  },
  {
    page: "Settings",
    count: 16,
    branches: [
      "visual metrics loading",
      "visual metrics loaded defaults",
      "optimistic single-field edit + both-controls pending disable",
      "visual save success",
      "visual load/save error",
      "conditional account/profile focus row",
      "feedback idle",
      "feedback exporting progress event",
      "feedback success size/path",
      "feedback failure",
      "feedback canceled-to-idle",
      "updater idle/checking/upToDate/available",
      "updater downloading progress/opening",
      "updater translated/default error",
      "published date/release notes/download directory",
      "manual check cooldown + download action predicate",
    ],
    locators: ["Settings.f.formatBytes", "Settings.p.feedback", "Settings.m.panel", "Index.Rn.updatePanel"],
    unavailable: "Diagnostic archive generation and updater check/download/open providers are fenced; preview state supplies presentation only.",
  },
];

const manifest = {
  workItem: "LWB317-UI-REMAINING-PAGES-001",
  referenceExecutable: "C:/Users/chimw/OneDrive/Desktop/Github/LW/lwbridge-0.3.17.exe",
  referenceExecutableSha256: "4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783",
  assets: Object.values(assets).map((asset) => ({ path: asset, sha256: hash(read(asset)) })),
  locators,
};

fs.writeFileSync(path.join(here, "source-manifest.json"), `${JSON.stringify(manifest, null, 2)}\n`);
fs.writeFileSync(path.join(here, "inventory.json"), `${JSON.stringify({ inventory }, null, 2)}\n`);
console.log(JSON.stringify({ result: "LWB317_REMAINING_INVENTORY_OK", inventory: inventory.map(({ page, count }) => ({ page, count })), locators: Object.keys(locators).length }, null, 2));
