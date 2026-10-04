import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { fileURLToPath } from "node:url";
import { createRequire } from "node:module";
const here = path.dirname(fileURLToPath(import.meta.url)); const repo = path.resolve(here, "../../../../.."); const ui = path.join(repo, "src/LWBridge.UI-0.3.17");
const requireUi = createRequire(path.join(ui, "package.json")); const React = requireUi("react"); const M = requireUi("react/jsx-runtime");
const { renderToStaticMarkup } = requireUi("react-dom/server"); const { transformSync } = requireUi("esbuild");
const asset = fs.readFileSync(path.join(repo, "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js"), "utf8");
const source = fs.readFileSync(path.join(ui, "src/ProfileSwitchState.jsx"), "utf8");
const start = asset.indexOf("Xe?(0,M.jsx)(`div`,{className:`panel profile-switch-loading`");
const end = asset.indexOf(":(0,M.jsxs)(M.Fragment", start); assert.ok(start >= 0 && end > start);
const expression = asset.slice(start, end);
const sha = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
const catalogs = {}; let language = "en";
for (const code of ["en", "ja"]) catalogs[code] = new Function(fs.readFileSync(path.join(ui, `src/locales/${code}.js`), "utf8").replace("export default", "return"))();
const t = (key) => catalogs[language][key] || key;
const original = new Function("M", "translate", `return function Switch({loading}){let Xe=loading,t=translate;return ${expression}:null;};`)(M, t);
const current = new Function("h", "useI18n", `${transformSync(source.replace(/^import[^\n]+\n/gm, "").replace(/export /g, ""), { loader: "jsx", jsxFactory: "h", target: "es2022" }).code}\nreturn ProfileSwitchState;`)(React.createElement, () => ({ t }));
const cases = [];
for (const code of ["en", "ja"]) for (const loading of [false, true]) for (const error of [null, "READ_FAILED", { code: "INVALID_GAME_ROOT" }]) {
  language = code;
  const props = { loading, error };
  const originalMarkup = renderToStaticMarkup(React.createElement(original, props));
  const currentMarkup = renderToStaticMarkup(React.createElement(current, props));
  assert.equal(currentMarkup, originalMarkup);
  if (loading) assert.ok(currentMarkup.includes('class="panel profile-switch-loading"') && currentMarkup.includes(t("common.processing")));
  else assert.equal(currentMarkup, "");
  cases.push({ name: `${code}-${loading ? "pending" : "settled"}-${error ? "supplied-other-producer-error" : "no-error"}`, pass: true });
}
const result = { marker: "LWB317_FINAL_PROFILE_SWITCH_OK", cases: cases.length, details: cases, sourceSha256: sha(asset), productionSha256: sha(source),
  locator: { utf8ByteOffset: Buffer.byteLength(asset.slice(0, start)), utf8ByteLength: Buffer.byteLength(expression), sha256: sha(expression) },
  limits: ["Only exact loading renderer is proven", "Source has no dedicated profile-switch error panel", "Actual profile caching/native bootstrap producer remains separate", "No original protected-runtime pixels or live native profile request"] };
if (process.argv.includes("--record")) fs.writeFileSync(path.join(here, "switch-results.json"), `${JSON.stringify(result, null, 2)}\n`);
console.log(JSON.stringify(result));
