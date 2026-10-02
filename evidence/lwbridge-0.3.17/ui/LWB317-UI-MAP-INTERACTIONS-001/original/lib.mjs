// Hash-asserted loading + AST helpers for the ORIGINAL LWBridge 0.3.17 frontend assets.
// Everything here reads the asset bytes; nothing is re-implemented.
import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
export const originalDir = here;
export const campaignDir = path.resolve(here, "..");
export const repo = path.resolve(here, "../../../../..");
const require = createRequire(path.join(repo, "src/LWBridge.UI-0.3.17/package.json"));
export const { parse } = require("@babel/parser");
export const traverse = require("@babel/traverse").default;
export const { transformSync } = require("esbuild");

export const assetsRelative = "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets";
export const assetsDir = path.join(repo, assetsRelative);
export const ASSET_HASHES = {
  "MapDataPanel-B4GXEND2.js": "ce74345518be72e417a510b982c591729e5683f69751e190805598c4c05d3089",
  "index-BVfnK1wp.js": "44c4e4043825b7db296b64171951b27176f8850ff8d7d337cc991df4765524c6",
  "rewardDisplay-eZWrd6iS.js": "7f65dd3f5b81c96117af5e83e8310d6c6a3a48787b1f693d387020255c78e73e",
  "GameAssetImage-Diy9VTIr.js": "2f92a87c3268497df6425b1175db6140e10aabcbdfae02615f065d00e16458e0",
  "en-BisSXcTB.js": "0bf43d180eb93692a93b830b5d984e9d01ddeea524340f2334fc63cba527a731",
};
export const sha256Hex = (bytes) => crypto.createHash("sha256").update(bytes).digest("hex");

const cache = new Map();
export function readAsset(name) {
  if (cache.has(name)) return cache.get(name);
  const expected = ASSET_HASHES[name];
  assert.ok(expected, `no pinned hash for ${name}`);
  const bytes = fs.readFileSync(path.join(assetsDir, name));
  assert.equal(sha256Hex(bytes), expected, `${name}: SHA-256 mismatch`);
  const entry = { name, bytes, source: bytes.toString("utf8"), sha256: expected, relative: `${assetsRelative}/${name}` };
  cache.set(name, entry);
  return entry;
}

export const rawOf = (entry, node) => entry.source.slice(node.start, node.end);
// UTF-8 byte offset of a UTF-16 index of the asset source (the asset is one line and contains non-ASCII).
export function byteAt(entry, index) {
  if (!entry._prefix) {
    // lazily build a prefix table at 4096-char granularity for speed
    entry._prefix = [0];
    for (let i = 0; i < entry.source.length; i += 4096) {
      entry._prefix.push(entry._prefix[entry._prefix.length - 1] + Buffer.byteLength(entry.source.slice(i, i + 4096)));
    }
  }
  const block = Math.floor(index / 4096);
  return entry._prefix[block] + Buffer.byteLength(entry.source.slice(block * 4096, index));
}

export function walkAll(node, visit, parents = []) {
  if (!node || typeof node !== "object") return;
  if (Array.isArray(node)) { node.forEach((child) => walkAll(child, visit, parents)); return; }
  if (node.type) { visit(node, parents); parents = [...parents, node]; }
  for (const [key, value] of Object.entries(node)) {
    if (key === "loc" || key === "extra") continue;
    if (value && typeof value === "object") walkAll(value, visit, parents);
  }
}
// Visit nodes of a function body without descending into nested function scopes.
export function walkDirect(node, visit, root = node) {
  if (!node || typeof node !== "object") return;
  if (Array.isArray(node)) { node.forEach((child) => walkDirect(child, visit, root)); return; }
  if (node.type && node !== root && /Function/.test(node.type)) return;
  if (node.type) visit(node);
  for (const [key, value] of Object.entries(node)) {
    if (key === "loc" || key === "extra") continue;
    if (value && typeof value === "object") walkDirect(value, visit, root);
  }
}

let panelCache;
export function loadPanel() {
  if (panelCache) return panelCache;
  const entry = readAsset("MapDataPanel-B4GXEND2.js");
  const ast = parse(entry.source, { sourceType: "module" });
  const body = ast.program.body;
  const R = body.find((node) => node.type === "FunctionDeclaration" && node.id.name === "R");
  assert.ok(R, "component R");
  const imports = body.filter((node) => node.type === "ImportDeclaration");
  const exportDecl = body.find((node) => node.type === "ExportNamedDeclaration");
  assert.equal(rawOf(entry, exportDecl), "export{R as MapDataPanel};");
  const firstBody = body.find((node) => node.type !== "ImportDeclaration");
  const lastBody = body.filter((node) => node !== exportDecl).at(-1);
  const topLevel = Object.fromEntries(body.filter((n) => n.type === "FunctionDeclaration").map((n) => [n.id.name, n]));
  panelCache = { entry, ast, R, imports, exportDecl, firstBody, lastBody, topLevel };
  return panelCache;
}

let indexCache;
export function loadIndex() {
  if (indexCache) return indexCache;
  const entry = readAsset("index-BVfnK1wp.js");
  const ast = parse(entry.source, { sourceType: "module" });
  const exportDecl = ast.program.body.find((n) => n.type === "ExportNamedDeclaration" && n.specifiers.length);
  const exportedToInternal = Object.fromEntries(exportDecl.specifiers.map((s) => [s.exported.name, s.local.name]));
  const functions = {};
  const variables = {};
  for (const node of ast.program.body) {
    const decl = node.type === "ExportNamedDeclaration" ? node.declaration : node;
    if (!decl) continue;
    if (decl.type === "FunctionDeclaration") functions[decl.id.name] = decl;
    if (decl.type === "VariableDeclaration") for (const d of decl.declarations) if (d.id.type === "Identifier") variables[d.id.name] = { declarator: d, statement: decl };
  }
  indexCache = { entry, ast, exportedToInternal, functions, variables };
  return indexCache;
}

// ---- source map from esbuild's re-print to the original byte positions --------------------
const B64 = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
function decodeVlq(segment) {
  const out = [];
  let shift = 0;
  let value = 0;
  for (const ch of segment) {
    const digit = B64.indexOf(ch);
    assert.ok(digit >= 0, "bad VLQ");
    value += (digit & 31) << shift;
    if (digit & 32) shift += 5;
    else { out.push(value & 1 ? -(value >> 1) : value >> 1); value = 0; shift = 0; }
  }
  return out;
}
let prettyCache;
// Regenerates the layout-only re-print in memory (never writes) and asserts it equals the committed
// reference/MapDataPanel.pretty.js, then returns original-index -> pretty-line lookup.
export function loadPretty() {
  if (prettyCache) return prettyCache;
  const entry = readAsset("MapDataPanel-B4GXEND2.js");
  const result = transformSync(entry.source, { loader: "js", minify: false, target: "esnext", sourcemap: "external" });
  const referencePath = path.join(campaignDir, "reference/MapDataPanel.pretty.js");
  const reference = fs.readFileSync(referencePath, "utf8");
  assert.equal(result.code, reference, "in-memory re-print differs from reference/MapDataPanel.pretty.js");
  const map = JSON.parse(result.map);
  const segments = []; // { origCol, genLine }
  let prevOrigCol = 0;
  map.mappings.split(";").forEach((line, genLine) => {
    let genCol = 0;
    if (!line) return;
    for (const seg of line.split(",")) {
      const fields = decodeVlq(seg);
      genCol += fields[0];
      if (fields.length >= 4) {
        prevOrigCol += fields[3];
        // fields[2] is the original line delta; the asset is a single line so it stays 0.
        segments.push({ origCol: prevOrigCol, genLine, genCol });
      }
    }
  });
  segments.sort((a, b) => a.origCol - b.origCol || a.genLine - b.genLine);
  const lines = reference.split("\n");
  const lineOf = (index) => {
    // first mapped token at or after `index`
    let lo = 0;
    let hi = segments.length - 1;
    let found = segments.length;
    while (lo <= hi) {
      const mid = (lo + hi) >> 1;
      if (segments[mid].origCol >= index) { found = mid; hi = mid - 1; } else lo = mid + 1;
    }
    return found < segments.length ? segments[found].genLine + 1 : null;
  };
  prettyCache = { lines, lineOf, referencePath, sha256: sha256Hex(Buffer.from(reference)) };
  return prettyCache;
}
