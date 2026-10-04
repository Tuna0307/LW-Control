import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../../..");
const require = createRequire(path.join(repo, "src/LWBridge.UI-0.3.17/package.json"));
const { parse } = require("@babel/parser");
const traverse = require("@babel/traverse").default;
const sha = (text) => crypto.createHash("sha256").update(text).digest("hex").toUpperCase();
function walk(node, out = []) { if (!node || typeof node !== "object") return out; if (node.type) out.push(node); for (const value of Object.values(node)) if (Array.isArray(value)) value.forEach((child) => walk(child, out)); else if (value && typeof value === "object") walk(value, out); return out; }
function read(name) { const relativePath = `evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/${name}`; const text = fs.readFileSync(path.join(repo, relativePath), "utf8"); const ast = parse(text, { sourceType: "module" }); return { relativePath, sha256: sha(text), text, ast, locator(node) { const exact = text.slice(node.start, node.end); return { utf8ByteOffset: Buffer.byteLength(text.slice(0, node.start)), utf8ByteLength: Buffer.byteLength(exact), sha256: sha(exact), text: exact }; } }; }
const shell = read("index-BVfnK1wp.js");
const parent = shell.ast.program.body.find((node) => node.type === "FunctionDeclaration" && node.id.name === "Gi");
const topConstants = shell.ast.program.body.filter((node) => node.type === "VariableDeclaration").flatMap((node) => node.declarations).filter((node) => ["mi", "hi"].includes(node.id.name));
const nodes = walk(parent);
const parentStates = nodes.filter((node) => node.type === "VariableDeclarator" && node.id.type === "ArrayPattern" && ["Ge", "Ke", "Je"].includes(node.id.elements[0]?.name));
const multiProfile = nodes.find((node) => node.type === "VariableDeclarator" && node.id.name === "rt");
const bindings = nodes.filter((node) => node.type === "CallExpression" && ["Li", "Bi", "Hi"].includes(node.arguments[0]?.name));
const contextKey = nodes.filter((node) => node.type === "CallExpression" && node.arguments.some((arg) => arg.type === "MemberExpression" && arg.object?.name === "r" && arg.property?.name === "selectedProfileId") && shell.text.slice(node.start, node.end).includes("profile-view-context")).map((node) => shell.locator(node));
const components = {};
for (const [key, asset, name] of [["automation", "AutomationPanel-BJ0gIqFh.js", "Ae"], ["squads", "SquadPanel-HC3-DJei.js", "pd"], ["map", "MapDataPanel-B4GXEND2.js", null]]) {
  const source = read(asset);
  let entry = name && source.ast.program.body.find((node) => node.type === "FunctionDeclaration" && node.id.name === name);
  if (!entry) { const exp = source.ast.program.body.find((node) => node.type === "ExportNamedDeclaration"); const local = exp.specifiers.find((spec) => /MapDataPanel/.test(spec.exported.name))?.local.name || exp.specifiers[0].local.name; entry = source.ast.program.body.find((node) => node.type === "FunctionDeclaration" && node.id.name === local); }
  const all = walk(entry);
  let entryPath;
  traverse(source.ast, { FunctionDeclaration(p) { if (p.node === entry) entryPath = p; } });
  const controlledNames = new Set(entry.params[0].properties.filter((prop) => ["activeCategory", "onActiveCategoryChange", "activeTab", "onActiveTabChange"].includes(prop.key.name)).map((prop) => prop.value.name));
  const referencePaths = [...controlledNames].flatMap((controlledName) => entryPath.scope.getBinding(controlledName).referencePaths);
  const selections = { automation: ["at", "V", "B"], squads: ["_", "y", "C"], map: ["$e", "L"] }[key];
  const declarations = all.filter((node) => node.type === "VariableDeclarator" && (selections.includes(node.id.name) || (node.id.type === "ArrayPattern" && selections.includes(node.id.elements[0]?.name))));
  const selectionCallback = referencePaths.find((p) => p.node.name === [...controlledNames][1])?.findParent((p) => p.isArrowFunctionExpression() || p.isFunctionDeclaration());
  components[key] = { path: source.relativePath, sha256: source.sha256, entry: entry.id.name, params: source.locator(entry.params[0]), controlledNames: [...controlledNames], declarations: declarations.map((node) => source.locator(node)), selectionCallback: source.locator(selectionCallback.node), controlledReferences: referencePaths.map((p) => source.locator(p.node)) };
}
let parentPath;
traverse(shell.ast, { FunctionDeclaration(p) { if (p.node === parent) parentPath = p; } });
const parentReferences = Object.fromEntries(["Ge", "P", "Ke", "qe", "Je", "Ye"].map((name) => [name, parentPath.scope.getBinding(name).referencePaths.map((p) => shell.locator(p.node))]));
const result = { original: { path: shell.relativePath, sha256: shell.sha256, defaultConstants: topConstants.map((node) => shell.locator(node)), parentStates: parentStates.map((node) => shell.locator(node)), parentReferences, multiProfile: shell.locator(multiProfile), bindings: bindings.map((node) => shell.locator(node)), contextKey, components } };
fs.writeFileSync(path.join(here, "source-contract.json"), JSON.stringify(result, null, 2) + "\n");
console.log(JSON.stringify({ marker: "LWB317_PARENT_TAB_SOURCE_RECOVERED", states: parentStates.length, bindings: bindings.length, components: Object.keys(components) }));
