import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { createRequire } from "node:module";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const req = createRequire(path.join(repo, "src/LWBridge.UI-0.3.17/package.json"));
const { parse } = req("@babel/parser");
const sha256 = (data) => crypto.createHash("sha256").update(data).digest("hex");
const locales = ["en", "id", "ja", "ko", "pt", "ru", "vi", "zh-CN", "zh-TW"];
const sourceFiles = [
  "src/LWBridge.UI-0.3.17/src/AutomationPage.jsx",
  "src/LWBridge.UI-0.3.17/src/AutomationMeta.jsx",
  "src/LWBridge.UI-0.3.17/src/DispatchAssistManual.jsx",
  "src/LWBridge.UI-0.3.17/src/tradePurchaseHistory.js",
];

function staticKey(node) {
  if (!node) return null;
  if (node.type === "StringLiteral") return node.value;
  if (node.type === "TemplateLiteral" && node.expressions.length === 0) return node.quasis[0]?.value?.cooked ?? null;
  return null;
}

function sourceKeys(source) {
  const keys = new Set();
  const ast = parse(source, { sourceType: "module", plugins: ["jsx"] });
  const visit = (node) => {
    if (!node || typeof node !== "object") return;
    if (node.type === "CallExpression") {
      const callee = node.callee;
      const name = callee?.type === "Identifier" ? callee.name : null;
      if (["t", "M", "S"].includes(name)) {
        const key = staticKey(node.arguments?.[0]);
        if (key && /^(automation|common|settings|status|configSave|squad)\./.test(key)) keys.add(key);
      }
    }
    for (const value of Object.values(node)) {
      if (Array.isArray(value)) value.forEach(visit);
      else if (value && typeof value === "object") visit(value);
    }
  };
  visit(ast);
  return keys;
}

const used = new Set();
const sourceManifest = [];
for (const relative of sourceFiles) {
  const full = path.join(repo, relative);
  const source = fs.readFileSync(full, "utf8");
  sourceKeys(source).forEach((key) => used.add(key));
  sourceManifest.push({ path: relative.replaceAll("\\", "/"), sha256: sha256(source) });
}

// Dynamic/template lookups are source-backed families; include every Automation
// catalog entry so those values are inventoried in all nine locale modules too.
const catalogs = {};
const localeManifest = [];
for (const locale of locales) {
  const relative = `src/LWBridge.UI-0.3.17/src/locales/${locale}.js`;
  const full = path.join(repo, relative);
  const source = fs.readFileSync(full, "utf8");
  const mod = await import(`${pathToFileURL(full).href}?unitc=${sha256(source).slice(0, 12)}`);
  catalogs[locale] = mod.default;
  localeManifest.push({ locale, path: relative, sha256: sha256(source), keyCount: Object.keys(mod.default).length });
}

for (const key of Object.keys(catalogs.en)) {
  if (/^(automation|configSave)\./.test(key)) used.add(key);
}

const keys = [...used].sort();
const missing = [];
const values = {};
for (const key of keys) {
  values[key] = {};
  for (const locale of locales) {
    const exists = Object.prototype.hasOwnProperty.call(catalogs[locale], key);
    if (!exists) missing.push({ locale, key });
    values[key][locale] = exists ? catalogs[locale][key] : null;
  }
}

const missingByKey = new Map();
for (const entry of missing) {
  if (!missingByKey.has(entry.key)) missingByKey.set(entry.key, []);
  missingByKey.get(entry.key).push(entry.locale);
}
const fallbackKeys = [...missingByKey.entries()]
  .filter(([, missingLocales]) => missingLocales.length === locales.length)
  .map(([key]) => key)
  .sort();
const partialMissing = missing.filter((entry) => !fallbackKeys.includes(entry.key));

assert.equal(locales.length, 9);
assert.equal(partialMissing.length, 0, `Automation locale keys partially missing: ${JSON.stringify(partialMissing.slice(0, 20))}`);
assert.deepEqual(fallbackKeys, ["common.loading"], "unexpected all-locale i18n fallback keys");
assert.ok(keys.length > 100, `unexpectedly small Automation locale inventory: ${keys.length}`);

const output = {
  result: "LWB317_VISUAL_FINAL_UNIT_C_LOCALES_OK",
  locales,
  sourceFiles: sourceManifest,
  localeFiles: localeManifest,
  keyCount: keys.length,
  missing,
  fallbackKeys,
  partialMissing,
  values,
  limits: "Catalog inventory proves key/value presence across all nine current locale modules except source-proven common.loading, which is absent from all nine and intentionally exercises i18n t(key) fallback-to-key behavior. Browser rendering is exercised in EN/JA only, as required; it does not claim all-nine browser coverage.",
};
fs.writeFileSync(path.join(here, "locale-inventory.json"), `${JSON.stringify(output, null, 2)}\n`);
console.log(JSON.stringify({ result: output.result, locales: locales.length, keys: keys.length, partialMissing: partialMissing.length, fallbackKeys }, null, 2));
