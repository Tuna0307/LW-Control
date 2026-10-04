import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import assert from "node:assert/strict";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url)), repo = path.resolve(here, "../../../../.."), ui = path.join(repo, "src/LWBridge.UI-0.3.17"), src = path.join(ui, "src");
const { parse } = createRequire(path.join(ui, "package.json"))("@babel/parser");
const hash = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
const contract = JSON.parse(fs.readFileSync(path.join(here, "source-contract.json"), "utf8"));
const source = fs.readFileSync(path.join(src, "Pages.jsx"), "utf8");
assert.equal(hash(source), contract.current.sha256, "Pages source changed after dependency recovery; re-audit before moving");
const ast = parse(source, { sourceType: "module", plugins: ["jsx"] });
const declarations = [];
for (const statement of ast.program.body) {
  const node = statement.type === "ExportNamedDeclaration" ? statement.declaration : statement;
  if (node?.type === "FunctionDeclaration") declarations.push({ name: node.id.name, node });
  if (node?.type === "VariableDeclaration") for (const declaration of node.declarations) if (declaration.id.type === "Identifier") declarations.push({ name: declaration.id.name, node, declaration });
}
const byName = Object.fromEntries(declarations.map((entry) => [entry.name, entry]));
const groups = [
  { file: "sharedPageUI.jsx", names: ["Switch", "ToggleRow", "PanelTitle"], exports: ["Switch", "ToggleRow", "PanelTitle"] },
  { file: "HomePage.jsx", names: ["RECOVERY_ACTIVE_STATES", "previewHomeState", "translatedError", "HomePage"], exports: ["HomePage"] },
  { file: "AutomationPage.jsx", begin: "automationCategories", end: "SquadsPage", exports: ["AutomationPage"] },
  { file: "SquadsPage.jsx", begin: "SquadsPage", end: "CITY_LAYOUT_PREVIEW_STATES", exports: ["SquadsPage"] },
  { file: "CityLayoutPage.jsx", begin: "CITY_LAYOUT_PREVIEW_STATES", end: "HotkeyCard", exports: ["CityLayoutPage"] },
  { file: "HotkeyPages.jsx", begin: "HotkeyCard", end: "SETTINGS_PREVIEW_STATES", exports: ["RecoveredHotkeyPanel", "HotkeysPage", "MiniGamesPage"] },
  { file: "SettingsPage.jsx", begin: "SETTINGS_PREVIEW_STATES", end: "PageForRoute", exports: ["SettingsPage"] },
];
for (const group of groups) if (!group.names) group.names = declarations.filter((entry) => entry.node.start >= byName[group.begin].node.start && entry.node.start < byName[group.end].node.start).map((entry) => entry.name);
const allNames = groups.flatMap((group) => group.names);
assert.equal(new Set(allNames).size, allNames.length, "one canonical declaration owner");
assert.equal(allNames.length + 1, declarations.length, "every old declaration belongs to one module, except rewritten router");
const prepared = [], proof = [];
for (const group of groups) {
  const dependencies = new Set(group.names.flatMap((name) => contract.current.topLevelGraph.find((entry) => entry.name === name).dependencies));
  const imports = new Map();
  for (const dependency of dependencies) {
    const binding = contract.current.imports[dependency];
    if (!binding) continue;
    if (!imports.has(binding.module)) imports.set(binding.module, []);
    imports.get(binding.module).push({ local: dependency, imported: binding.imported });
  }
  const shared = groups[0].names.filter((name) => dependencies.has(name) && !group.names.includes(name));
  const lines = [...imports.entries()].map(([module, bindings]) => `import { ${bindings.sort((a, b) => a.local.localeCompare(b.local)).map((binding) => binding.imported === binding.local ? binding.local : binding.imported + " as " + binding.local).join(", ")} } from ${JSON.stringify(module)};`);
  if (shared.length) lines.push(`import { ${shared.join(", ")} } from "./sharedPageUI.jsx";`);
  const bodies = group.names.map((name) => {
    const entry = byName[name], text = source.slice(entry.node.start, entry.node.end);
    proof.push({ name, file: group.file, oldUtf8ByteOffset: Buffer.byteLength(source.slice(0, entry.node.start)), byteLength: Buffer.byteLength(text), originalSha256: hash(text) });
    return group.exports.includes(name) ? "export " + text : text;
  });
  const text = lines.join("\n") + "\n\n" + bodies.join("\n\n") + "\n";
  fs.writeFileSync(path.join(src, group.file), text);
  prepared.push({ file: group.file, sha256: hash(text), names: group.names, exports: group.exports });
}
const mapWrapper = `import { MapDataPage } from "./MapDataPage.jsx";
import { getMapPreviewProvider } from "./mapPreviewApi.js";

export function MapRoutePage(pageProps) {
  const previewProvider = getMapPreviewProvider(pageProps.bridgeMode, pageProps.previewState);
  return <MapDataPage {...pageProps} {...(previewProvider || {})} />;
}
`;
fs.writeFileSync(path.join(src, "MapRoutePage.jsx"), mapWrapper);
prepared.push({ file: "MapRoutePage.jsx", sha256: hash(mapWrapper), names: ["MapRoutePage"], exports: ["MapRoutePage"] });
const router = `import { lazy } from "react";
import { HomePage } from "./HomePage.jsx";

const loadAutomationPage = () => import("./AutomationPage.jsx");
const loadSquadsPage = () => import("./SquadsPage.jsx");
const loadCityLayoutPage = () => import("./CityLayoutPage.jsx");
const loadHotkeyPages = () => import("./HotkeyPages.jsx");
const loadMapRoutePage = () => import("./MapRoutePage.jsx");
const loadSettingsPage = () => import("./SettingsPage.jsx");

const LazyAutomationPage = lazy(() => loadAutomationPage().then((module) => ({ default: module.AutomationPage })));
const LazySquadsPage = lazy(() => loadSquadsPage().then((module) => ({ default: module.SquadsPage })));
const LazyCityLayoutPage = lazy(() => loadCityLayoutPage().then((module) => ({ default: module.CityLayoutPage })));
const LazyHotkeyPanel = lazy(() => loadHotkeyPages().then((module) => ({ default: module.RecoveredHotkeyPanel })));
const LazyMapRoutePage = lazy(() => loadMapRoutePage().then((module) => ({ default: module.MapRoutePage })));
const LazySettingsPage = lazy(() => loadSettingsPage().then((module) => ({ default: module.SettingsPage })));

const routeLoaders = {
  advanced: undefined,
  automation: loadAutomationPage,
  "city-layout": loadCityLayoutPage,
  hotkeys: loadHotkeyPages,
  "mini-games": loadHotkeyPages,
  "map-data": loadMapRoutePage,
  settings: loadSettingsPage,
  march: loadSquadsPage,
};

export function preloadRoute(routeKey) {
  routeLoaders[routeKey]?.().catch(() => {});
}

export function PageForRoute({ routeKey, ...pageProps }) {
  switch (routeKey) {
    case "overview": return <HomePage {...pageProps} />;
    case "automation": return <LazyAutomationPage {...pageProps} />;
    case "map-data": return <LazyMapRoutePage {...pageProps} />;
    case "march": return <LazySquadsPage {...pageProps} />;
    case "city-layout": return <LazyCityLayoutPage {...pageProps} />;
    case "hotkeys": return <LazyHotkeyPanel {...pageProps} />;
    case "mini-games": return <LazyHotkeyPanel {...pageProps} category="miniGames" />;
    case "settings": return <LazySettingsPage {...pageProps} />;
    default: return <HomePage {...pageProps} />;
  }
}
`;
fs.writeFileSync(path.join(here, "planned-Pages.jsx"), router);
fs.writeFileSync(path.join(here, "baseline-Pages.jsx"), source);
const report = { marker: "LWB317_PAGE_MODULES_PREPARED", originalPagesSha256: hash(source), declarations: proof, prepared,
  intentionallyChanged: ["PageForRoute becomes eager-Home/lazy-route dispatcher", "Map branch moved to source-equivalent MapRoutePage wrapper", "Hotkeys/Mini Games share stable lazy RecoveredHotkeyPanel"],
  plannedRouterSha256: hash(router), finalPagesMutation: "pending lead notification" };
fs.writeFileSync(path.join(here, "module-preparation.json"), JSON.stringify(report, null, 2) + "\n");
console.log(JSON.stringify({ marker: report.marker, movedDeclarations: proof.length, modules: prepared.length }));
