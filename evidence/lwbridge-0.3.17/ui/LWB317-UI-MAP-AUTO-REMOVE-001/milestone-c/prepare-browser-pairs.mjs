import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const milestoneB = path.resolve(here, "../milestone-b");
const rawDir = path.join(here, "raw");
const generatedDir = path.join(here, "generated");
fs.mkdirSync(rawDir, { recursive: true });
fs.mkdirSync(generatedDir, { recursive: true });

const readJson = (file) => JSON.parse(fs.readFileSync(file, "utf8"));
const hashBytes = (bytes) => crypto.createHash("sha256").update(bytes).digest("hex");
const hashFile = (file) => hashBytes(fs.readFileSync(file));
const rel = (file) => path.relative(repo, file).replaceAll("\\", "/");
const writeJson = (file, value) => fs.writeFileSync(file, `${JSON.stringify(value, null, 2)}\n`);

const render = readJson(path.join(milestoneB, "render-results.json"));
const pinnedB = readJson(path.join(milestoneB, "pinned-inputs.json"));
assert.equal(render.marker, "LWB317_MAP_AUTO_REMOVE_B_STATES_COMPARED");
assert.equal(pinnedB.milestone, "B");

const selected = [
  { id: "en-light-default", sourceId: "default", language: "en", theme: "light", width: 1280, height: 720 },
  { id: "ja-dark-configured", sourceId: "configured-waiting", language: "ja", theme: "dark", width: 375, height: 1000 },
  { id: "ja-light-running", sourceId: "auto-running", language: "ja", theme: "light", width: 375, height: 1000 },
  { id: "en-dark-offline", sourceId: "offline", language: "en", theme: "dark", width: 1280, height: 720 },
];

const originalCssFile = path.join(repo, pinnedB.reference.css.path);
const referenceCss = pinnedB.current.files.find((item) => item.path.endsWith("/reference.css"));
const stylesCss = pinnedB.current.files.find((item) => item.path.endsWith("/styles.css"));
assert.ok(referenceCss && stylesCss, "current CSS pins");
const originalCss = fs.readFileSync(originalCssFile, "utf8");
const currentCss = `${fs.readFileSync(path.join(repo, referenceCss.path), "utf8")}\n${fs.readFileSync(path.join(repo, stylesCss.path), "utf8")}`;

const makeDocument = ({ language, theme, title, markup, css }) =>
  `<!doctype html><html lang="${language}" data-theme="${theme}"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>${title}</title><style>${css}</style></head><body>${markup}</body></html>\n`;

function lineDiff(left, right) {
  if (left === right) return [];
  const a = left.split(/\r?\n/);
  const b = right.split(/\r?\n/);
  const max = Math.max(a.length, b.length);
  const out = [];
  for (let i = 0; i < max; i += 1) {
    if (a[i] !== b[i]) out.push({ line: i + 1, original: a[i] ?? null, current: b[i] ?? null });
  }
  return out;
}

const pairRecords = [];
for (const spec of selected) {
  const sourcePair = render.pairs.find((pair) => pair.language === spec.language && pair.id === spec.sourceId);
  assert.ok(sourcePair, `${spec.id}: milestone B source pair`);
  assert.equal(sourcePair.exactViewMatch, true, `${spec.id}: source view match`);
  assert.equal(sourcePair.exactMarkupMatch, true, `${spec.id}: source markup match`);
  assert.deepEqual(sourcePair.differences, [], `${spec.id}: source render differences`);

  const sourceOriginal = path.join(repo, sourcePair.rawFiles.original);
  const sourceCurrent = path.join(repo, sourcePair.rawFiles.current);
  const originalMarkup = fs.readFileSync(sourceOriginal, "utf8").replace(/\r?\n$/, "");
  const currentMarkup = fs.readFileSync(sourceCurrent, "utf8").replace(/\r?\n$/, "");
  assert.equal(hashBytes(Buffer.from(originalMarkup, "utf8")), sourcePair.originalMarkupSha256, `${spec.id}: original source raw hash`);
  assert.equal(hashBytes(Buffer.from(currentMarkup, "utf8")), sourcePair.currentMarkupSha256, `${spec.id}: current source raw hash`);

  const rawOriginal = path.join(rawDir, `${spec.id}-original.html`);
  const rawCurrent = path.join(rawDir, `${spec.id}-current.html`);
  fs.writeFileSync(rawOriginal, `${originalMarkup}\n`);
  fs.writeFileSync(rawCurrent, `${currentMarkup}\n`);

  const generatedOriginal = path.join(generatedDir, `${spec.id}-original.html`);
  const generatedCurrent = path.join(generatedDir, `${spec.id}-current.html`);
  fs.writeFileSync(generatedOriginal, makeDocument({ language: spec.language, theme: spec.theme, title: `${spec.id} original`, markup: originalMarkup, css: originalCss }));
  fs.writeFileSync(generatedCurrent, makeDocument({ language: spec.language, theme: spec.theme, title: `${spec.id} current`, markup: currentMarkup, css: currentCss }));

  pairRecords.push({
    ...spec,
    input: sourcePair.input,
    sourcePairId: sourcePair.pairId,
    sourceRawFiles: sourcePair.rawFiles,
    rawFiles: { original: rel(rawOriginal), current: rel(rawCurrent) },
    generatedFiles: { original: rel(generatedOriginal), current: rel(generatedCurrent) },
    raw: {
      originalSha256: hashBytes(Buffer.from(originalMarkup, "utf8")),
      currentSha256: hashBytes(Buffer.from(currentMarkup, "utf8")),
      exactBytes: originalMarkup === currentMarkup,
      differences: lineDiff(originalMarkup, currentMarkup),
    },
  });
}

assert.ok(pairRecords.every((pair) => pair.raw.exactBytes && pair.raw.differences.length === 0), "all four selected raw Auto-card pairs are exact before browser styling");

writeJson(path.join(here, "browser-pairs.json"), {
  marker: "LWB317_MAP_AUTO_REMOVE_C_BROWSER_PAIRS_PREPARED",
  generatedAt: "2026-10-04",
  pairs: pairRecords.map(({ id, sourceId, language, theme, width, height }) => ({ id, caseId: sourceId, language, theme, width, height })),
});
writeJson(path.join(here, "raw-comparison.json"), {
  marker: "LWB317_MAP_AUTO_REMOVE_C_RAW_COMPARED",
  generatedAt: "2026-10-04",
  pairs: pairRecords,
});
writeJson(path.join(here, "pinned-inputs.json"), {
  task: "LWB317-UI-MAP-AUTO-REMOVE-001",
  milestone: "C",
  generatedAt: "2026-10-04",
  evidenceState: "EXACT_BYTES / EXACT_CONTRACT + browser measurement",
  reference: pinnedB.reference,
  current: pinnedB.current,
  dependencies: pinnedB.dependencies,
  harness: pinnedB.harness,
  parentEvidence: {
    milestoneBRender: { path: rel(path.join(milestoneB, "render-results.json")), sha256: hashFile(path.join(milestoneB, "render-results.json")) },
    milestoneBPins: { path: rel(path.join(milestoneB, "pinned-inputs.json")), sha256: hashFile(path.join(milestoneB, "pinned-inputs.json")) },
    milestoneBControls: { path: rel(path.join(milestoneB, "control-results.json")), sha256: hashFile(path.join(milestoneB, "control-results.json")) },
  },
  css: {
    original: { path: pinnedB.reference.css.path, sha256: hashFile(originalCssFile) },
    currentOrder: [referenceCss.path, stylesCss.path],
    referenceCssExactlyMatchesOriginal: hashFile(path.join(repo, referenceCss.path)) === hashFile(originalCssFile),
  },
  pairs: pairRecords.map((pair) => ({
    id: pair.id,
    caseId: pair.sourceId,
    language: pair.language,
    theme: pair.theme,
    viewport: { width: pair.width, height: pair.height, deviceScaleFactor: 1 },
    input: pair.input,
    raw: pair.raw,
    rawFiles: pair.rawFiles,
    generatedFiles: pair.generatedFiles,
  })),
});

console.log("LWB317_MAP_AUTO_REMOVE_C_PREPARED pairs=4 rawExact=4 rawDifferences=0 themes=light,dark languages=en,ja viewports=1280x720,375x1000");
