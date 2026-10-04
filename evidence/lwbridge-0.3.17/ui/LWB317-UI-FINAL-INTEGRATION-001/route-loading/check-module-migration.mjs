import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import assert from "node:assert/strict";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";
const here = path.dirname(fileURLToPath(import.meta.url)), repo = path.resolve(here, "../../../../.."), ui = path.join(repo, "src/LWBridge.UI-0.3.17");
const req = createRequire(path.join(ui, "package.json")), { parse } = req("@babel/parser"), { buildSync } = req("esbuild");
const hash = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
const prepared = JSON.parse(fs.readFileSync(path.join(here, "module-preparation.json"), "utf8"));
const baseline = fs.readFileSync(path.join(here, "baseline-Pages.jsx"), "utf8");
assert.equal(hash(baseline), prepared.originalPagesSha256);
const declarations = new Map(), modules = [];
for (const entry of prepared.prepared) {
  const file = path.join(ui, "src", entry.file), source = fs.readFileSync(file, "utf8"), ast = parse(source, { sourceType: "module", plugins: ["jsx"] });
  modules.push({ file: entry.file, sha256: hash(source) });
  for (const statement of ast.program.body) {
    const node = statement.type === "ExportNamedDeclaration" ? statement.declaration : statement;
    if (node?.type === "FunctionDeclaration") { assert.ok(!declarations.has(node.id.name), "unique owner " + node.id.name); declarations.set(node.id.name, { file: entry.file, source, node }); }
    if (node?.type === "VariableDeclaration") for (const declaration of node.declarations) if (declaration.id.type === "Identifier") {
      assert.ok(!declarations.has(declaration.id.name), "unique owner " + declaration.id.name); declarations.set(declaration.id.name, { file: entry.file, source, node });
    }
  }
}
const proof = [];
for (const expected of prepared.declarations) {
  const actual = declarations.get(expected.name); assert.ok(actual, expected.name);
  const text = actual.source.slice(actual.node.start, actual.node.end);
  assert.equal(actual.file, expected.file, expected.name + " owner");
  assert.equal(hash(text), expected.originalSha256, expected.name + " body/declaration bytes");
  proof.push({ name: expected.name, file: actual.file, originalSha256: expected.originalSha256, currentSha256: hash(text), pass: true });
}
assert.equal(proof.length, 48);
buildSync({ entryPoints: prepared.prepared.map((entry) => path.join(ui, "src", entry.file)), bundle: true, write: false,
  outdir: path.join(here, "unwritten-import-validation"), format: "esm", platform: "browser", loader: { ".png": "dataurl" }, logLevel: "silent" });
const pages = fs.readFileSync(path.join(ui, "src/Pages.jsx"), "utf8"), planned = fs.readFileSync(path.join(here, "planned-Pages.jsx"), "utf8");
const migrated = hash(pages) === hash(planned);
if (process.argv.includes("--require-router")) assert.ok(migrated, "actual canonical router must match reviewed planned router");
const report = { marker: "LWB317_PAGE_MODULE_MIGRATION_OK", declarations: proof.length, uniqueOwners: true, importCompilation: "PASS", actualRouterMigrated: migrated,
  modules, proof, originalPagesSha256: hash(baseline), currentPagesSha256: hash(pages), plannedRouterSha256: hash(planned),
  limits: "Actual 48 canonical declaration bodies unchanged and imports compiled; does not prove route suspense/Activity/browser lifecycle, old evidence adapters or native/game functions." };
if (process.argv.includes("--record")) fs.writeFileSync(path.join(here, "module-migration-results.json"), JSON.stringify(report, null, 2) + "\n");
console.log(JSON.stringify({ marker: report.marker, declarations: proof.length, importCompilation: report.importCompilation, actualRouterMigrated: migrated }));
