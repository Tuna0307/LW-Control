// Pin evidence plus reused harness/import dependencies; this never declares UI parity.
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../..");
const require = createRequire(path.join(repo, "src/LWBridge.UI-0.3.17/package.json"));
const { parse } = require("@babel/parser");
const relative = (file) => path.relative(repo, file).replaceAll("\\", "/");
const pin = (file) => {
  const bytes = fs.readFileSync(file);
  return { path: relative(file), bytes: bytes.length, sha256: crypto.createHash("sha256").update(bytes).digest("hex") };
};
const inputs = JSON.parse(fs.readFileSync(path.join(here, "pinned-inputs.json"), "utf8"));
const deps = new Set();
function visit(file) {
  file = path.resolve(file);
  if (deps.has(file)) return;
  deps.add(file);
  if (!/\.(mjs|js|jsx)$/.test(file)) return;
  const ast = parse(fs.readFileSync(file, "utf8"), { sourceType: "module", plugins: ["jsx"] });
  for (const node of ast.program.body) {
    if (!node.source?.value?.startsWith(".")) continue;
    const dependency = path.resolve(path.dirname(file), node.source.value);
    if (fs.existsSync(dependency)) visit(dependency);
  }
}
for (const filename of ["compare-renderers.mjs", "capture-browser.mjs", "serve-reference.mjs", "check-selection-counter-evidence.mjs"]) visit(path.join(here, filename));
visit(path.join(repo, "src/LWBridge.UI-0.3.17/src/MapDataPage.jsx"));
for (const name of ["index-BVfnK1wp.js", "rewardDisplay-eZWrd6iS.js", "GameAssetImage-Diy9VTIr.js", "en-BisSXcTB.js"]) deps.add(path.join(repo, "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets", name));
for (const file of inputs.current.files) deps.add(path.join(repo, file.path));
for (const file of Object.values(inputs.current.locales)) deps.add(path.join(repo, file.path));
const packetFiles = [];
function collect(dir) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    if (entry.name === "generated" || entry.name === "integrity-manifest.json") continue;
    const file = path.join(dir, entry.name);
    if (entry.isDirectory()) collect(file); else packetFiles.push(file);
  }
}
collect(here);
const manifest = {
  task: "LWB317-UI-MAP-MANUAL-VISUAL-001",
  tooling: { node: process.version, react: require("react").version, reactDom: require("react-dom").version, esbuild: require("esbuild").version, babelParser: require("@babel/parser/package.json").version },
  dependencies: [...deps].sort().map(pin),
  evidence: packetFiles.sort().map(pin),
  generatedDocuments: "Excluded from Git/integrity; reproduce with compare-renderers.mjs. Raw scoped HTML and screenshot hashes are pinned.",
};
fs.writeFileSync(path.join(here, "integrity-manifest.json"), `${JSON.stringify(manifest, null, 2)}\n`);
console.log(JSON.stringify({ marker: "LWB317_MANUAL_VISUAL_INTEGRITY_RECORDED", dependencies: manifest.dependencies.length, evidence: manifest.evidence.length }));
