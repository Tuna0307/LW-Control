import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { fileURLToPath, pathToFileURL } from "node:url";
const here = path.dirname(fileURLToPath(import.meta.url)), repo = path.resolve(here, "../../../../.."), ui = path.join(repo, "src/LWBridge.UI-0.3.17/src");
const sha = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
const names = ["sharedPageUI.jsx", "HomePage.jsx", "AutomationPage.jsx", "SquadsPage.jsx", "CityLayoutPage.jsx", "HotkeyPages.jsx", "SettingsPage.jsx"];
const modules = names.map((name) => ({ path: "src/LWBridge.UI-0.3.17/src/" + name, sha256: sha(fs.readFileSync(path.join(ui, name))) }));
const flatten = names.map((name) => fs.readFileSync(path.join(ui, name), "utf8").replace(/^import[^\n]+\n/gm, "")).join("\n");
const base = path.join(repo, "evidence/lwbridge-0.3.17/ui");
const harnessFile = path.join(base, "LWB317-UI-AUTOMATION-AFK-CLOSEOUT-001/harness.mjs");
let harness = fs.readFileSync(harnessFile, "utf8");
assert.ok(harness.includes("export const here = path.dirname(fileURLToPath(import.meta.url));"));
harness = harness.replace("export const here = path.dirname(fileURLToPath(import.meta.url));", `export const here = ${JSON.stringify(path.dirname(harnessFile))};`);
assert.ok(harness.includes("export const read = file => fs.readFileSync(path.join(repo,file),'utf8');"));
harness = harness.replace("export const read = file => fs.readFileSync(path.join(repo,file),'utf8');", `export const read = file => file === 'src/LWBridge.UI-0.3.17/src/Pages.jsx' ? ${JSON.stringify(flatten)} : fs.readFileSync(path.join(repo,file),'utf8');`);
const wrapper = fs.readFileSync(path.join(ui, "EquipmentMotion.jsx"), "utf8").replace(/^import[^\n]+\n/gm, "").replace(/^export \{[^\n]+\n/gm, "").replace(/export /g, "");
const bindings = `${wrapper}\nconst inertMotion = new Proxy({}, {get:(_target,name)=>String(name)});\nconst inertGameAssetImage = ({className,alt}) => ({type:'span',props:{className:className+' game-asset-placeholder','aria-label':alt}});\n`;
harness = harness.replace("export function compile(source,name,env={}) {", `${bindings}\nexport function compile(source,name,env={}) {
 if(name==='b' && !fn(source,name) && source.includes('GameAssetImage as b')) {
   const imageSource=read('src/LWBridge.UI-0.3.17/src/GameAssetImage.jsx');
   const imageHooks=hooks();
   return compile(imageSource,'GameAssetImage',{h,...imageHooks,useContext:()=>null,GameAssetReaderContext:null,cacheFor:()=>null,normalizeGameAssetSource:compile(imageSource,'normalizeGameAssetSource')});
 }
 env={motion:inertMotion,AnimatePresence:'Fragment',useReducedMotion:()=>false,equipmentMotionProps,GameAssetImage:inertGameAssetImage,...env};`);
const data = (source) => "data:text/javascript;base64," + Buffer.from(source).toString("base64");
const harnessUrl = data(harness);
const scriptPaths = ["LWB317-UI-EQUIPMENT-CLOSEOUT-001/check-closeout.mjs", "LWB317-UI-EQUIPMENT-CLOSEOUT-001-R1/check-r1.mjs", "LWB317-UI-AUTOMATION-AFK-CLOSEOUT-001/check-closeout.mjs"];
const historical = scriptPaths.map((name) => ({ path: "evidence/lwbridge-0.3.17/ui/" + name, sha256: sha(fs.readFileSync(path.join(base, name))) }));
const oldArgv = process.argv, oldLog = console.log, reports = [];
const wantsRecord = process.argv.includes("--record");
process.argv = process.argv.filter((arg) => arg !== "--record");
try {
  for (const name of scriptPaths) {
    const file = path.join(base, name), original = fs.readFileSync(file, "utf8"); let adapted = original;
    adapted = adapted.replace(/const here\s*=\s*path\.dirname\(fileURLToPath\(import\.meta\.url\)\);/, `const here = ${JSON.stringify(path.dirname(file))};`);
    adapted = adapted.replace(/from\s*(['"])(\.{1,2}\/[^'"]+)\1/g, (_match, quote, relative) => {
      const target = path.resolve(path.dirname(file), relative);
      return "from " + quote + (target === harnessFile ? harnessUrl : pathToFileURL(target).href) + quote;
    });
    const output = []; console.log = (...args) => output.push(args.join(" "));
    try { await import(data(adapted)); }
    catch (error) { throw new Error(`${name}: ${error.name}: ${error.message}`); }
    const parsed = output.map((line) => { try { return JSON.parse(line); } catch { return null; } }).filter(Boolean);
    assert.equal(parsed.length, 1, name); const report = parsed[0];
    assert.ok(report.results.every((entry) => entry.result === "PASS"), name);
    reports.push({ script: name, marker: report.result, assertions: report.results.reduce((sum, entry) => sum + entry.count, 0), results: report.results });
  }
} finally { console.log = oldLog; process.argv = oldArgv; }
assert.deepEqual(reports.map((report) => report.assertions), [105, 61, 729]);
for (const file of historical) assert.equal(sha(fs.readFileSync(path.join(repo, file.path))), file.sha256);
const result = { marker: "LWB317_CURRENT_EQUIPMENT_AFK_REPLAY_OK", modules, flattenedSha256: sha(flatten), historical, reports,
  adapter: ["Redirect only canonical Pages read to declarations from the actual split modules plus sharedPageUI", "Retain historical baseline reads unchanged", "Inject binding-only inert Motion/reduced/image adapters into the existing standalone function compiler", "Resolve removed Assist local b through its actual GameAssetImage import and compile actual component/normalizer with an absent reader; image lifecycle and original caller parity have separate proof", "Preserve every historical behavioral assertion and original-source oracle", "Disable historical --record writes; write this separate replay record only"],
  limits: "Callback/render/effect regression replay uses inert Motion element identities and false reduced snapshot. Real original/current motion and true-reduced lifetimes are tested separately by check-motion; actual callsite/handler/content invariants by check-integration. No historical fixed file-hash validation is relabeled current." };
if (wantsRecord) fs.writeFileSync(path.join(here, "replay-results.json"), JSON.stringify(result, null, 2) + "\n");
console.log(JSON.stringify({ marker: result.marker, reports: reports.map(({script,assertions})=>({script,assertions})) }));
