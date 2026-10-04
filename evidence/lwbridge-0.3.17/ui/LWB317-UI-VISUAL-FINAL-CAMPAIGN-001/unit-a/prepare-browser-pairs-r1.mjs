import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const milestoneB = here;
const src = path.join(repo, "src/LWBridge.UI-0.3.17/src");
const rawDir = path.join(here, "raw");
const generatedDir = path.join(here, "generated");
fs.mkdirSync(rawDir, { recursive: true });
fs.mkdirSync(generatedDir, { recursive: true });

const hashBytes = (bytes) => crypto.createHash("sha256").update(bytes).digest("hex");
const hashFile = (file) => hashBytes(fs.readFileSync(file));
const rel = (file) => path.relative(repo, file).replaceAll("\\", "/");
const writeJson = (file, value) => fs.writeFileSync(file, `${JSON.stringify(value, null, 2)}\n`);
const firstLine = (file) => fs.readFileSync(file, "utf8").split(/\r?\n/, 1)[0];

const referenceCssFile = path.join(src, "reference.css");
const stylesCssFile = path.join(src, "styles.css");
const currentPageFile = path.join(src, "MapDataPage.jsx");
const combinedCss = `${fs.readFileSync(referenceCssFile, "utf8")}\n${fs.readFileSync(stylesCssFile, "utf8")}`;
const cssSha256 = hashBytes(Buffer.from(combinedCss, "utf8"));

const selected = [
  { id: "treasure-en-light-desktop", source: "en-configured-treasure", language: "en", theme: "light", width: 1280, height: 720, tab: "treasure" },
  { id: "treasure-en-dark-desktop", source: "en-configured-treasure", language: "en", theme: "dark", width: 1280, height: 720, tab: "treasure" },
  { id: "treasure-ja-light-375", source: "ja-configured-treasure", language: "ja", theme: "light", width: 375, height: 1000, tab: "treasure" },
  { id: "treasure-ja-dark-desktop", source: "ja-configured-treasure", language: "ja", theme: "dark", width: 1280, height: 720, tab: "treasure" },
];

const makeDocument = ({ spec, markup, side }) => `<!doctype html>
<html lang="${spec.language}" data-theme="${spec.theme}">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width,initial-scale=1">
  <title>${spec.id} ${side}</title>
  <style>${combinedCss}</style>
</head>
<body>
  <section class="main-view">
    <section class="panel map-panel" data-proof-case="${spec.id}">${markup}</section>
  </section>
</body>
</html>
`;

const pairs = [];
for (const spec of selected) {
  const sourceFiles = {
    original: path.join(milestoneB, "raw", `${spec.source}-original.html`),
    current: path.join(milestoneB, "raw", `${spec.source}-current.html`),
  };
  assert.ok(fs.existsSync(sourceFiles.original), `${spec.id}: original renderer output`);
  assert.ok(fs.existsSync(sourceFiles.current), `${spec.id}: current renderer output`);
  const markup = { original: firstLine(sourceFiles.original), current: firstLine(sourceFiles.current) };
  for (const side of ["original", "current"]) {
    assert.ok(markup[side].startsWith('<div class="map-search">'), `${spec.id}/${side}: scoped map-search root`);
    const rawFile = path.join(rawDir, `${spec.id}-${side}.html`);
    const generatedFile = path.join(generatedDir, `${spec.id}-${side}.html`);
    fs.writeFileSync(rawFile, `${markup[side]}\n`);
    fs.writeFileSync(generatedFile, makeDocument({ spec, markup: markup[side], side }));
  }
  const rawFiles = {
    original: path.join(rawDir, `${spec.id}-original.html`),
    current: path.join(rawDir, `${spec.id}-current.html`),
  };
  const generatedFiles = {
    original: path.join(generatedDir, `${spec.id}-original.html`),
    current: path.join(generatedDir, `${spec.id}-current.html`),
  };
  pairs.push({
    ...spec,
    viewport: { width: spec.width, height: spec.height, deviceScaleFactor: 1 },
    sourceFiles: Object.fromEntries(Object.entries(sourceFiles).map(([side, file]) => [side, { path: rel(file), sha256: hashFile(file) }])),
    rawFiles: Object.fromEntries(Object.entries(rawFiles).map(([side, file]) => [side, { path: rel(file), sha256: hashFile(file) }])),
    generatedFiles: Object.fromEntries(Object.entries(generatedFiles).map(([side, file]) => [side, { path: rel(file), sha256: hashFile(file) }])),
    rawExact: markup.original === markup.current,
  });
}

const manifest = {
  task: "LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-a",
  milestone: "C",
  marker: "LWB317_VISUAL_FINAL_UNIT_A_BROWSER_PAIRS_R1",
  generatedAt: "2026-10-04",
  scope: "Scoped .map-search renderer output inside real .main-view > .panel.map-panel ancestry. Table/job-table bodies remain omitted by assignment scope; normal pagination retains its actual direct .map-search ancestry.",
  fixedEnvironment: { timezone: "Asia/Singapore", deviceScaleFactor: 1, cssOrder: [rel(referenceCssFile), rel(stylesCssFile)], combinedCssSha256: cssSha256 },
  production: {
    mapDataPage: { path: rel(currentPageFile), sha256: hashFile(currentPageFile) },
    referenceCss: { path: rel(referenceCssFile), sha256: hashFile(referenceCssFile) },
    stylesCss: { path: rel(stylesCssFile), sha256: hashFile(stylesCssFile) },
  },
  parentEvidence: {
    currentResults: { path: rel(path.join(milestoneB, "current-results.json")), sha256: hashFile(path.join(milestoneB, "current-results.json")) },
    currentManifest: { path: rel(path.join(milestoneB, "current-manifest.json")), sha256: hashFile(path.join(milestoneB, "current-manifest.json")) },
    correctionResults: { path: rel(path.join(milestoneB, "correction-results.json")), sha256: hashFile(path.join(milestoneB, "correction-results.json")) },
  },
  pairs,
};
writeJson(path.join(here, "browser-pairs-r1.json"), manifest);
console.log(JSON.stringify({ marker: manifest.marker, pairs: pairs.length, rawExactPairs: pairs.filter((pair) => pair.rawExact).length, cssSha256 }, null, 2));

