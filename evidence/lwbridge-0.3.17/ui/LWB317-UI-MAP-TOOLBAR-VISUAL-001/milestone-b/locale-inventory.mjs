import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const src = path.join(repo, "src/LWBridge.UI-0.3.17/src");
const pageFile = path.join(src, "MapDataPage.jsx");
const treasureFile = path.join(src, "MapTreasureTypeFilter.jsx");
const retainedFile = path.join(src, "MapRetainedGoodsFilter.jsx");
const source = fs.readFileSync(pageFile, "utf8").replaceAll("\r\n", "\n");
const hash = (file) => crypto.createHash("sha256").update(fs.readFileSync(file)).digest("hex");

function between(text, start, end, label) {
  const from = text.indexOf(start);
  const to = text.indexOf(end, from + start.length);
  assert.ok(from >= 0 && to > from, `${label} slice`);
  return text.slice(from, to + end.length);
}

const assigned = [
  between(source, "const TAB_LABEL_KEYS", "});", "tab labels"),
  between(source, "function Pagination", "}\n\nconst PREVIEW_TAB_BY_STATE", "pagination"),
  source.slice(source.indexOf('<div className="map-search">'), source.lastIndexOf("</section>")),
  fs.readFileSync(treasureFile, "utf8"),
  fs.readFileSync(retainedFile, "utf8"),
].join("\n");

const keys = [...new Set([...assigned.matchAll(/"((?:map|common|squad)\.[A-Za-z0-9_.-]+)"/g)].map((match) => match[1]))].sort();
const languages = ["en", "zh-CN", "zh-TW", "ja", "ko", "vi", "id", "ru", "pt"];
const catalogs = {};
for (const language of languages) {
  const file = path.join(src, `locales/${language}.js`);
  catalogs[language] = { file, messages: (await import(pathToFileURL(file).href)).default };
}
for (const key of keys) assert.equal(typeof catalogs.en.messages[key], "string", `English toolbar key ${key}`);

const inventory = languages.map((language) => {
  const { file, messages } = catalogs[language];
  const rows = keys.map((key) => ({
    key,
    own: Object.prototype.hasOwnProperty.call(messages, key),
    value: messages[key] ?? catalogs.en.messages[key],
    fallback: !Object.prototype.hasOwnProperty.call(messages, key),
  }));
  return {
    language,
    path: path.relative(repo, file).replaceAll("\\", "/"),
    sha256: hash(file),
    keyCount: rows.length,
    ownCount: rows.filter((row) => row.own).length,
    fallbackCount: rows.filter((row) => row.fallback).length,
    rows,
  };
});

const report = {
  task: "LWB317-UI-MAP-TOOLBAR-VISUAL-001",
  marker: "LWB317_MAP_TOOLBAR_LOCALE_INVENTORY_OK",
  production: [pageFile, treasureFile, retainedFile].map((file) => ({ path: path.relative(repo, file).replaceAll("\\", "/"), sha256: hash(file) })),
  assignedKeyCount: keys.length,
  keys,
  locales: inventory,
  note: "Inventory only. Locale catalogs are not edited; missing locale-owned entries use the existing English fallback contract.",
};
fs.writeFileSync(path.join(here, "locale-inventory.json"), `${JSON.stringify(report, null, 2)}\n`);
console.log(JSON.stringify({ marker: report.marker, keys: keys.length, locales: inventory.map(({ language, ownCount, fallbackCount }) => ({ language, ownCount, fallbackCount })) }, null, 2));
