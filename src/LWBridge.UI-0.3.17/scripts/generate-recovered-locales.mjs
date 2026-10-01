import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const projectRoot = path.resolve(here, "..", "..", "..");
const sourceRoot = path.join(projectRoot, "evidence", "lwbridge-0.3.17", "ui", "frontend-package", "web", "assets");
const outputRoot = path.join(here, "..", "src", "locales");

const sources = {
  en: "en-BisSXcTB.js",
  "zh-CN": "zh-CN-ByrbNejR.js",
  "zh-TW": "zh-TW-B9GVGLk3.js",
  ja: "ja-UrbzJu-m.js",
  ko: "ko-SaFvwwmo.js",
  vi: "vi-BAUQ1cwP.js",
  id: "id-D9oeC8zM.js",
  ru: "ru-YncvPDv4.js",
  pt: "pt-BaH2E9ai.js",
};

fs.mkdirSync(outputRoot, { recursive: true });

for (const [language, fileName] of Object.entries(sources)) {
  const source = fs.readFileSync(path.join(sourceRoot, fileName), "utf8");
  const start = source.indexOf("var t={");
  const end = source.lastIndexOf("};export{t as default};");
  if (start < 0 || end < 0 || end <= start) {
    throw new Error(`Unable to locate recovered locale object in ${fileName}`);
  }

  let body = source.slice(start + "var t={".length, end);
  body = body.replace(/\.\.\.e(?:\.[A-Za-z0-9_-]+|\[[^\]]+\]),?/g, "");

  const banner = [
    "// Generated from the exact recovered LWBridge 0.3.17 locale asset.",
    `// Source: evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/${fileName}`,
    "// Do not hand-edit; run scripts/generate-recovered-locales.mjs.",
    "export default {",
  ].join("\n");
  fs.writeFileSync(path.join(outputRoot, `${language}.js`), `${banner}${body}};\n`, "utf8");
}

console.log(`Generated ${Object.keys(sources).length} recovered locale modules.`);
