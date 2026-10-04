import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { createRequire } from 'node:module';
import { fileURLToPath, pathToFileURL } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
let root = here;
while (!fs.existsSync(path.join(root, 'AGENTS.md'))) root = path.dirname(root);
const src = path.join(root, 'src/LWBridge.UI-0.3.17/src');
const require = createRequire(path.join(src, '../package.json'));
const React = require('react');
const E = require('react/jsx-runtime');
const { renderToStaticMarkup } = require('react-dom/server');
const { transformSync } = require('esbuild');
const hash = value => crypto.createHash('sha256').update(value).digest('hex');
const locators = JSON.parse(fs.readFileSync(path.join(here, 'source-locators.json'), 'utf8'));
const originalBytes = fs.readFileSync(path.join(root, locators.originalPath));
assert.equal(hash(originalBytes), locators.originalSHA256);
const originalSlice = originalBytes.subarray(locators.byteOffset, locators.byteOffset + locators.byteLength);
assert.equal(hash(originalSlice), locators.sliceSHA256);
assert.equal(originalBytes.subarray(locators.byteOffset + locators.byteLength, locators.byteOffset + locators.byteLength + locators.followingBranch.length).toString(), locators.followingBranch);
const original = originalSlice.toString().replace(/,$/, '');
const main = fs.readFileSync(path.join(root, 'evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js'), 'utf8');
assert.equal(hash(Buffer.from(main)), locators.translationSource.sha256);
const translationBytes = Buffer.from(main).subarray(locators.translationSource.byteOffset, locators.translationSource.byteOffset + locators.translationSource.byteLength);
assert.equal(hash(translationBytes), locators.translationSource.sliceSHA256);
const irStart = main.lastIndexOf('function Ir(');
const lrStart = main.lastIndexOf('function Lr(');
const lrEnd = main.indexOf('var Rr=', lrStart);
assert.ok(irStart > 0 && lrStart > irStart && lrEnd > lrStart);
const originalTranslate = new Function(`${main.slice(irStart, lrEnd)};return Lr;`)();
const originalRender = new Function('E', 'm', 'bn', 'Qn', 'S', `return E.jsxs(E.Fragment,{children:[${original}]});`);
function extractBlock(text) {
  const start = text.indexOf('      {scanError ? <div className="map-scan-error"');
  const end = text.indexOf('      {scanTab === "manual" ?', start);
  assert.ok(start >= 0 && end > start);
  return text.slice(start, end);
}
function compile(block) {
  const output = transformSync(`export function render({scanError,scanView,backendAvailable,online,bridgeMode,t,translateActionError}) {return <>${block.trim()}</>;}`, { loader: 'jsx', format: 'cjs', jsx: 'automatic' }).code;
  const module = { exports: {} };
  new Function('require', 'module', 'exports', output)(require, module, module.exports);
  return module.exports.render;
}
const baselineBytes = fs.readFileSync(path.join(here, 'baseline-notices.jsx.txt'));
assert.equal(hash(baselineBytes), locators.baselineBlockSHA256);
const baseline = compile(baselineBytes.toString());
const currentBytes = fs.readFileSync(path.join(src, 'MapDataPage.jsx'));
const current = compile(extractBlock(currentBytes.toString()));
const { translateActionError } = await import(pathToFileURL(path.join(src, 'mapInteractions.js')));
const languages = ['en', 'zh-CN', 'zh-TW', 'ja', 'ko', 'vi', 'id', 'ru', 'pt'];
const modes = [
  { bridgeMode: 'preview', backendAvailable: false, online: false },
  { bridgeMode: 'native-unavailable', backendAvailable: false, online: false },
  { bridgeMode: 'native', backendAvailable: true, online: false },
  { bridgeMode: 'native', backendAvailable: true, online: true },
];
const errors = [
  { scanError: '', scanView: { error: '' } },
  { scanError: 'UNKNOWN_SYNTHETIC', scanView: { error: '' } },
  { scanError: '', scanView: { error: 'UNKNOWN_SYNTHETIC' } },
  { scanError: 'INVALID_GAME_ROOT', scanView: { error: 'UNKNOWN_SYNTHETIC' } },
];
let baselineFailures = 0;
const cases = [];
for (const language of languages) {
  const messages = (await import(pathToFileURL(path.join(src, `locales/${language}.js`)))).default;
  const t = key => messages[key] || key;
  for (const [modeIndex, mode] of modes.entries()) for (const [errorIndex, error] of errors.entries()) {
    const args = { ...mode, ...error, t, translateActionError };
    const expected = renderToStaticMarkup(originalRender(E, originalTranslate, error.scanError, error.scanView.error, t));
    const before = renderToStaticMarkup(baseline(args));
    const after = renderToStaticMarkup(current(args));
    const baselineMismatch = before !== expected;
    baselineFailures += Number(baselineMismatch);
    assert.equal(after, expected, `${language}/${modeIndex}/${errorIndex}`);
    cases.push({ language, modeIndex, errorIndex, baselineMismatch, currentMatches: true });
  }
}
assert.equal(cases.length, 144);
assert.equal(baselineFailures, 27);
const result = { status: 'PASS', cases: cases.length, baselineFailures, currentFailures: 0, productionSHA256: hash(currentBytes), originalRenderer: locators, scope: 'Actual exact original and compiled canonical JSX notice-region renders, not full-page/native/pixel proof', results: cases };
if (process.argv.includes('--record')) fs.writeFileSync(path.join(here, 'notice-results.json'), `${JSON.stringify(result, null, 2)}\n`);
console.log(`LWB317_FINAL_MAP_NOTICES_OK cases=${cases.length} baselineFailures=${baselineFailures} currentFailures=0`);
