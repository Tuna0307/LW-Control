import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import assert from "node:assert/strict";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url)), repo = path.resolve(here, "../../../../.."), ui = path.join(repo, "src/LWBridge.UI-0.3.17");
const req = createRequire(path.join(ui, "package.json")), { parse } = req("@babel/parser"), traverse = req("@babel/traverse").default;
const sourcePath = "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js";
const source = fs.readFileSync(path.join(repo, sourcePath), "utf8"), ast = parse(source, { sourceType: "module" });
const hash = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
function locator(node, text = source) { return { utf8ByteOffset: Buffer.byteLength(text.slice(0, node.start)), utf8ByteLength: Buffer.byteLength(text.slice(node.start, node.end)), sha256: hash(text.slice(node.start, node.end)), text: text.slice(node.start, node.end) }; }
const topFunctions = Object.fromEntries(ast.program.body.filter((node) => node.type === "FunctionDeclaration").map((node) => [node.id.name, node]));
const topVariables = Object.fromEntries(ast.program.body.filter((node) => node.type === "VariableDeclaration").flatMap((node) => node.declarations).filter((node) => node.id.type === "Identifier").map((node) => [node.id.name, node]));
const gi = topFunctions.Gi;
const routeSelect = gi.body.body.find((node) => node.type === "FunctionDeclaration" && node.id.name === "Tt");
const all = [];
function walk(node) { if (!node || typeof node !== "object") return; if (node.type) all.push(node); for (const value of Object.values(node)) Array.isArray(value) ? value.forEach(walk) : value && typeof value === "object" && walk(value); }
walk(gi);
const suspense = all.find((node) => node.type === "CallExpression" && node.arguments?.[0]?.type === "MemberExpression" && node.arguments[0].object.name === "b" && node.arguments[0].property.name === "Suspense");
assert.ok(suspense);
const currentPath = "src/LWBridge.UI-0.3.17/src/Pages.jsx", current = fs.readFileSync(path.join(repo, currentPath), "utf8"), currentAst = parse(current, { sourceType: "module", plugins: ["jsx"] });
const entries = [];
for (const statement of currentAst.program.body) {
  const node = statement.type === "ExportNamedDeclaration" ? statement.declaration : statement;
  if (node?.type === "FunctionDeclaration") entries.push({ name: node.id.name, node, dependencies: new Set() });
  if (node?.type === "VariableDeclaration") for (const declaration of node.declarations) if (declaration.id.type === "Identifier") entries.push({ name: declaration.id.name, node: declaration, dependencies: new Set() });
}
const imports = {};
for (const statement of currentAst.program.body.filter((node) => node.type === "ImportDeclaration")) for (const specifier of statement.specifiers) imports[specifier.local.name] = { module: statement.source.value, imported: specifier.imported?.name || "default" };
traverse(currentAst, { ReferencedIdentifier(reference) {
  const binding = reference.scope.getBinding(reference.node.name);
  if (!binding || !binding.scope.path.isProgram()) return;
  const entry = entries.find((item) => reference.node.start >= item.node.start && reference.node.end <= item.node.end);
  if (entry && reference.node.name !== entry.name) entry.dependencies.add(reference.node.name);
} });
const graph = entries.map((entry) => ({ name: entry.name, ...locator(entry.node, current), dependencies: [...entry.dependencies].sort() }));
for (const entry of graph) delete entry.text;
const contract = { original: { path: sourcePath, sha256: hash(source), functions: { navigation: locator(topFunctions.Qr), preload: locator(topFunctions.Wi), select: locator(routeSelect) },
  loaders: Object.fromEntries(["Mi", "Ni", "Pi", "Fi", "Ii", "I", "Li", "Ri", "zi", "Bi", "Vi", "Hi", "Ui", "Zr"].map((name) => [name, locator(topVariables[name])])), suspense: locator(suspense) },
  current: { path: currentPath, sha256: hash(current), imports, topLevelGraph: graph },
  recovered: ["Qr preloads on both mouse-enter and focus, selects on click", "Wi optionally calls route loader and ignores rejected preload", "Tt same-route no-op; Map starts summary request without await, then preload, then transition", "Transition retains prior visited Set when route already visited; copies/adds first visit then commits active route", "Home is eager; six lazy panel modules; Hotkeys/Mini share Hotkey module and lazy component type", "Single Suspense fallback wraps profile-view-context including save errors, switch state and visited Activity children", "Fallback is panel > muted common.processing", "Visited Activity modes and profile-key context remain the lifetime owners; imports must not move those boundaries"],
  limits: "Exact local source and dependency graph only. No original protected runtime observation or current lazy route implementation proven. Original Vite preload helper and browser module cache are not replaced by an invented retry/cache policy." };
fs.writeFileSync(path.join(here, "source-contract.json"), JSON.stringify(contract, null, 2) + "\n");
console.log(JSON.stringify({ marker: "LWB317_ROUTE_LOADING_SOURCE_RECOVERED", functions: graph.length, imports: Object.keys(imports).length, sourceSha256: hash(source) }));
