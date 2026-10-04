import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import assert from "node:assert/strict";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";
const here = path.dirname(fileURLToPath(import.meta.url)), repo = path.resolve(here, "../../../../.."), ui = path.join(repo, "src/LWBridge.UI-0.3.17");
const { parse } = createRequire(path.join(ui, "package.json"))("@babel/parser");
const pages = fs.readFileSync(path.join(ui, "src/Pages.jsx"), "utf8"), ast = parse(pages, { sourceType: "module", plugins: ["jsx"] });
const staticImports = ast.program.body.filter((node) => node.type === "ImportDeclaration").map((node) => node.source.value);
assert.deepEqual(staticImports, ["react", "./HomePage.jsx"]);
const manifestBytes = fs.readFileSync(path.join(ui, "dist/.vite/manifest.json")), manifest = JSON.parse(manifestBytes);
const modules = ["AutomationPage", "SquadsPage", "CityLayoutPage", "HotkeyPages", "MapRoutePage", "SettingsPage"].map((name) => "src/" + name + ".jsx");
for (const module of modules) {
  assert.ok(manifest["index.html"].dynamicImports.includes(module), module + " dynamic entry");
  assert.equal(manifest[module].isDynamicEntry, true);
}
const startup = new Set();
function followStatic(key) { if (startup.has(key)) return; startup.add(key); for (const dependency of manifest[key].imports || []) followStatic(dependency); }
followStatic("index.html");
for (const module of modules) assert.ok(!startup.has(module), module + " excluded from startup static graph");
assert.equal(modules.filter((module) => module.includes("HotkeyPages")).length, 1);
assert.match(pages, /hotkeys: loadHotkeyPages/); assert.match(pages, /"mini-games": loadHotkeyPages/);
assert.match(pages, /case "hotkeys": return <LazyHotkeyPanel/); assert.match(pages, /case "mini-games": return <LazyHotkeyPanel/);
const hash = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
const report = { marker: "LWB317_PRODUCTION_ROUTE_GRAPH_OK", staticImports, startupStaticKeys: [...startup], manifestSha256: hash(manifestBytes), PagesSha256: hash(pages),
  dynamicRouteChunks: modules.map((module) => { const file = manifest[module].file, bytes = fs.readFileSync(path.join(ui, "dist", file)); return { module, file, bytes: bytes.length, sha256: hash(bytes) }; }),
  limits: "Actual canonical router AST and built Vite manifest/bytes prove code splitting and startup graph; does not establish network timing, Suspense fallback or retained effects. Build/package gates are separate." };
if (process.argv.includes("--record")) fs.writeFileSync(path.join(here, "production-route-graph.json"), JSON.stringify(report, null, 2) + "\n");
console.log(JSON.stringify({ marker: report.marker, dynamicRouteChunks: report.dynamicRouteChunks.length, startupStaticKeys: report.startupStaticKeys }));
